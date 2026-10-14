using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

    public MuxVideoService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<MuxDirectUploadDto> CreateDirectUploadAsync(
        string corsOrigin,
        CancellationToken cancellationToken = default)
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

        using var response = await client.PostAsJsonAsync(
            "video/v1/uploads",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Mux direct upload creation failed with status {(int)response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MuxUploadResponse>(cancellationToken);
        var upload = result?.Data;
        if (upload is null || string.IsNullOrWhiteSpace(upload.Id) || string.IsNullOrWhiteSpace(upload.Url))
            throw new InvalidOperationException("Mux returned an invalid direct upload response.");

        return new MuxDirectUploadDto(upload.Id, upload.Url);
    }

    public async Task<MuxVideoUploadResult> CompleteDirectUploadAsync(
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uploadId))
            throw new ArgumentException("A Mux upload ID is required.", nameof(uploadId));

        var client = CreateAuthenticatedClient();
        var assetId = await WaitForAssetAsync(client, uploadId, cancellationToken);
        var playbackId = await WaitForPlaybackIdAsync(client, assetId, cancellationToken);

        return new MuxVideoUploadResult(assetId, playbackId);
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

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{tokenId}:{tokenSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        return client;
    }

    private async Task<string> WaitForAssetAsync(
        HttpClient client,
        string uploadId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(ProcessingTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(
                $"video/v1/uploads/{Uri.EscapeDataString(uploadId)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Mux upload status check failed with status {(int)response.StatusCode}: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<MuxUploadResponse>(cancellationToken);
            var upload = result?.Data;
            if (upload is null)
                throw new InvalidOperationException("Mux returned an invalid upload status response.");

            if (string.Equals(upload.Status, "errored", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mux reported an error while processing the uploaded video.");

            if (!string.IsNullOrWhiteSpace(upload.AssetId))
                return upload.AssetId;

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new TimeoutException("Mux did not finish processing the video within the allowed time.");
    }

    private async Task<string> WaitForPlaybackIdAsync(
        HttpClient client,
        string assetId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(ProcessingTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(
                $"video/v1/assets/{Uri.EscapeDataString(assetId)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Mux asset status check failed with status {(int)response.StatusCode}: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<MuxAssetResponse>(cancellationToken);
            var asset = result?.Data;
            if (asset is null)
                throw new InvalidOperationException("Mux returned an invalid asset status response.");

            if (string.Equals(asset.Status, "errored", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mux reported an error while processing the video asset.");

            var playbackId = asset.PlaybackIds?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Id))?.Id;
            if (!string.IsNullOrWhiteSpace(playbackId))
                return playbackId;

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new TimeoutException("Mux did not provide a playback ID within the allowed time.");
    }

    private sealed class MuxUploadResponse
    {
        public MuxUploadData? Data { get; set; }
    }

    private sealed class MuxUploadData
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
        public string? Status { get; set; }
        public string? AssetId { get; set; }
    }

    private sealed class MuxAssetResponse
    {
        public MuxAssetData? Data { get; set; }
    }

    private sealed class MuxAssetData
    {
        public string? Status { get; set; }
        public List<MuxPlaybackId>? PlaybackIds { get; set; }
    }

    private sealed class MuxPlaybackId
    {
        public string? Id { get; set; }
    }
}
