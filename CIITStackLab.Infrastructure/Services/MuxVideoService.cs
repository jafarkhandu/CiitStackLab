using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using CIITStackLab.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CIITStackLab.Infrastructure.Services;

public sealed class MuxVideoService : IMuxVideoService
{
    private const string MuxApiBaseUrl = "https://api.mux.com/";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(15);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public MuxVideoService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<MuxDirectUploadDto> CreateDirectUploadAsync(string corsOrigin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(corsOrigin))
            throw new ArgumentException("CORS origin is required.", nameof(corsOrigin));

        var client = CreateAuthenticatedClient();
        var request = new
        {
            cors_origin = corsOrigin,
            new_asset_settings = new
            {
                playback_policies = new[] { "public" },
                video_quality = "basic"
            }
        };

        using var response = await client.PostAsJsonAsync("video/v1/uploads", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Mux direct upload creation failed with status {(int)response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MuxUploadResponse>(cancellationToken);
        var upload = result?.Data;
        if (upload is null || string.IsNullOrWhiteSpace(upload.Id) || string.IsNullOrWhiteSpace(upload.Url))
            throw new InvalidOperationException("Mux returned an invalid direct upload response.");

        return new MuxDirectUploadDto(upload.Id, upload.Url);
    }

    public async Task<MuxVideoStatusDto> GetDirectUploadStatusAsync(string uploadId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uploadId))
            throw new ArgumentException("A Mux upload ID is required.", nameof(uploadId));

        var client = CreateAuthenticatedClient();
        var upload = await GetUploadAsync(client, uploadId, cancellationToken);

        if (string.Equals(upload.Status, "errored", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Mux reported an error while processing the uploaded video.");

        if (string.Equals(upload.Status, "cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(upload.Status, "timed_out", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The Mux video upload is no longer available for processing.");
        }

        var assetId = upload.AssetId;

        // Mux exposes the asset_id on the Direct Upload once the upload reaches
        // asset_created. The asset list endpoint also supports an upload_id
        // filter, so use it as a reliable fallback if the upload response has
        // not exposed asset_id yet.
        if (string.IsNullOrWhiteSpace(assetId))
        {
            var asset = await FindAssetByUploadIdAsync(client, uploadId, cancellationToken);
            assetId = asset?.Id;
        }

        if (string.IsNullOrWhiteSpace(assetId))
            return new MuxVideoStatusDto("uploading", 0, null, "Mux is finalizing the uploaded video.");

        var assetDetails = await GetAssetAsync(client, assetId, cancellationToken);
        if (string.Equals(assetDetails.Status, "errored", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Mux reported an error while processing the video asset.");

        var playbackId = assetDetails.PlaybackIds?
            .FirstOrDefault(x => string.Equals(x.Policy, "public", StringComparison.OrdinalIgnoreCase))?.Id
            ?? assetDetails.PlaybackIds?
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Id))?.Id;
        var progress = Math.Clamp(assetDetails.Progress?.Progress ?? 0, 0, 100);

        if (string.Equals(assetDetails.Status, "ready", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(playbackId))
        {
            return new MuxVideoStatusDto("ready", 100, playbackId, "Video is ready for playback.");
        }

        var message = string.Equals(assetDetails.Progress?.State, "transcoding", StringComparison.OrdinalIgnoreCase)
            ? "Mux is transcoding the video."
            : "Mux is processing the video and preparing playback.";

        return new MuxVideoStatusDto("processing", progress, null, message);
    }

    public async Task<MuxVideoUploadResult> CompleteDirectUploadAsync(string uploadId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uploadId))
            throw new ArgumentException("A Mux upload ID is required.", nameof(uploadId));

        var deadline = DateTimeOffset.UtcNow.Add(ProcessingTimeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var status = await GetDirectUploadStatusAsync(uploadId, cancellationToken);
            if (string.Equals(status.Stage, "ready", StringComparison.OrdinalIgnoreCase))
            {
                var client = CreateAuthenticatedClient();
                var upload = await GetUploadAsync(client, uploadId, cancellationToken);
                var assetId = upload.AssetId;

                if (string.IsNullOrWhiteSpace(assetId))
                {
                    var asset = await FindAssetByUploadIdAsync(client, uploadId, cancellationToken);
                    assetId = asset?.Id;
                }

                if (string.IsNullOrWhiteSpace(assetId))
                    throw new InvalidOperationException("Mux reported the video as ready without an asset ID.");

                var assetDetails = await GetAssetAsync(client, assetId, cancellationToken);
                var playbackId = await EnsurePublicPlaybackIdAsync(
                    client,
                    assetId,
                    assetDetails.PlaybackIds,
                    cancellationToken);

                return new MuxVideoUploadResult(assetId, playbackId);
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new TimeoutException("Mux did not finish processing the video within the allowed time.");
    }

    public async Task<string> EnsurePublicPlaybackIdAsync(
        string playbackId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playbackId))
            throw new ArgumentException("A Mux playback ID is required.", nameof(playbackId));

        var client = CreateAuthenticatedClient();
        using var response = await client.GetAsync(
            $"video/v1/playback-ids/{Uri.EscapeDataString(playbackId)}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Mux playback lookup failed with status {(int)response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MuxPlaybackLookupResponse>(cancellationToken);
        var assetId = result?.Data?.Object?.Id;
        if (string.IsNullOrWhiteSpace(assetId))
            throw new InvalidOperationException("Mux did not return an asset for the playback ID.");

        var assetDetails = await GetAssetAsync(client, assetId, cancellationToken);
        if (!string.Equals(assetDetails.Status, "ready", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Mux video is not ready for playback yet.");

        return await EnsurePublicPlaybackIdAsync(
            client,
            assetId,
            assetDetails.PlaybackIds,
            cancellationToken);
    }

    private async Task<string> EnsurePublicPlaybackIdAsync(
        HttpClient client,
        string assetId,
        List<MuxPlaybackId>? playbackIds,
        CancellationToken cancellationToken)
    {
        var existingPublicPlaybackId = playbackIds?
            .FirstOrDefault(x =>
                string.Equals(x.Policy, "public", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(x.Id))?.Id;

        if (!string.IsNullOrWhiteSpace(existingPublicPlaybackId))
            return existingPublicPlaybackId;

        var request = new { policy = "public" };
        using var response = await client.PostAsJsonAsync(
            $"video/v1/assets/{Uri.EscapeDataString(assetId)}/playback-ids",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Mux public playback setup failed with status {(int)response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MuxPlaybackIdResponse>(cancellationToken);
        var playbackId = result?.Data?.Id;
        if (string.IsNullOrWhiteSpace(playbackId))
            throw new InvalidOperationException("Mux created the public playback ID without returning its ID.");

        return playbackId;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var tokenId = _configuration["Mux:TokenId"];
        var tokenSecret = _configuration["Mux:TokenSecret"];
        if (string.IsNullOrWhiteSpace(tokenId) || string.IsNullOrWhiteSpace(tokenSecret))
            throw new InvalidOperationException("Mux credentials are not configured.");

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(MuxApiBaseUrl);
        client.Timeout = Timeout.InfiniteTimeSpan;
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{tokenId}:{tokenSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return client;
    }

    private async Task<MuxUploadData> GetUploadAsync(HttpClient client, string uploadId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"video/v1/uploads/{Uri.EscapeDataString(uploadId)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Mux upload status check failed with status {(int)response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MuxUploadResponse>(cancellationToken);
        return result?.Data ?? throw new InvalidOperationException("Mux returned an invalid upload status response.");
    }

    private async Task<MuxAssetData?> FindAssetByUploadIdAsync(
        HttpClient client,
        string uploadId,
        CancellationToken cancellationToken)
    {
        var query = $"video/v1/assets?upload_id={Uri.EscapeDataString(uploadId)}&limit=1";
        using var response = await client.GetAsync(query, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Mux asset lookup failed with status {(int)response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MuxAssetListResponse>(cancellationToken);
        return result?.Data?.FirstOrDefault();
    }

    private async Task<MuxAssetData> GetAssetAsync(HttpClient client, string assetId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"video/v1/assets/{Uri.EscapeDataString(assetId)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Mux asset status check failed with status {(int)response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MuxAssetResponse>(cancellationToken);
        return result?.Data ?? throw new InvalidOperationException("Mux returned an invalid asset status response.");
    }

    private sealed class MuxUploadResponse { public MuxUploadData? Data { get; set; } }

    private sealed class MuxUploadData
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
        public string? Status { get; set; }

        [JsonPropertyName("asset_id")]
        public string? AssetId { get; set; }
    }

    private sealed class MuxAssetListResponse { public List<MuxAssetData>? Data { get; set; } }

    private sealed class MuxAssetResponse { public MuxAssetData? Data { get; set; } }

    private sealed class MuxAssetData
    {
        public string? Id { get; set; }
        public string? Status { get; set; }

        [JsonPropertyName("playback_ids")]
        public List<MuxPlaybackId>? PlaybackIds { get; set; }

        public MuxAssetProgress? Progress { get; set; }
    }

    private sealed class MuxAssetProgress
    {
        public string? State { get; set; }
        public int Progress { get; set; }
    }

    private sealed class MuxPlaybackId
    {
        public string? Id { get; set; }
        public string? Policy { get; set; }
    }

    private sealed class MuxPlaybackIdResponse
    {
        public MuxPlaybackId? Data { get; set; }
    }

    private sealed class MuxPlaybackLookupResponse
    {
        public MuxPlaybackLookupData? Data { get; set; }
    }

    private sealed class MuxPlaybackLookupData
    {
        public MuxPlaybackObject? Object { get; set; }
    }

    private sealed class MuxPlaybackObject
    {
        public string? Type { get; set; }
        public string? Id { get; set; }
    }
}
