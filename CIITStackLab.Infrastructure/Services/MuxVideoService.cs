using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CIITStackLab.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CIITStackLab.Infrastructure.Services;

public sealed class MuxVideoService : IMuxVideoService
{
    private const string MuxApiBaseUrl = "https://api.mux.com/";

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

        var tokenId = _configuration["Mux:TokenId"];
        var tokenSecret = _configuration["Mux:TokenSecret"];

        if (string.IsNullOrWhiteSpace(tokenId) || string.IsNullOrWhiteSpace(tokenSecret))
            throw new InvalidOperationException("Mux credentials are not configured.");

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(MuxApiBaseUrl);

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{tokenId}:{tokenSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

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

        var result = await response.Content.ReadFromJsonAsync<MuxDirectUploadResponse>(cancellationToken);
        if (result?.Data is null ||
            string.IsNullOrWhiteSpace(result.Data.Id) ||
            string.IsNullOrWhiteSpace(result.Data.Url))
        {
            throw new InvalidOperationException("Mux returned an invalid direct upload response.");
        }

        return new MuxDirectUploadDto(
            result.Data.Id,
            result.Data.Url,
            result.Data.Status ?? "waiting");
    }

    private sealed class MuxDirectUploadResponse
    {
        public MuxDirectUploadData? Data { get; set; }
    }

    private sealed class MuxDirectUploadData
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
        public string? Status { get; set; }
    }
}
