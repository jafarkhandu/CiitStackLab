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
                playback_policies = new[] { "signed" },
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

        if (string.IsNullOrWhiteSpace(upload.AssetId))
            return new MuxVideoStatusDto("uploading", 0, null, "Mux is finalizing the uploaded video.");

        var asset = await GetAssetAsync(client, upload.AssetId, cancellationToken);
        if (string.Equals(asset.Status, "errored", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Mux reported an error while processing the video asset.");

        var playbackId = asset.PlaybackIds?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Id))?.Id;
        var progress = Math.Clamp(asset.Progress?.Progress ?? 0, 0, 100);

        if (string.Equals(asset.Status, "ready", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(playbackId))
            return new MuxVideoStatusDto("ready", 100, playbackId, "Video is ready for playback.");

        var message = string.Equals(asset.Progress?.State, "transcoding", StringComparison.OrdinalIgnoreCase)
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
            if (string.Equals(status.Stage, "ready", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(status.PlaybackId))
            {
                var client = CreateAuthenticatedClient();
                var upload = await GetUploadAsync(client, uploadId, cancellationToken);
                if (string.IsNullOrWhiteSpace(upload.AssetId))
                    throw new InvalidOperationException("Mux reported the video as ready without an asset ID.");

                return new MuxVideoUploadResult(upload.AssetId, status.PlaybackId);
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new TimeoutException("Mux did not finish processing the video within the allowed time.");
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

    private sealed class MuxAssetResponse { public MuxAssetData? Data { get; set; } }

    private sealed class MuxAssetData
    {
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

    private sealed class MuxPlaybackId { public string? Id { get; set; } }
}
