using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CIITStackLab.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CIITStackLab.Infrastructure.Services;

public sealed class VimeoVideoService : IVimeoVideoService
{
    private const string VimeoApiBaseUrl = "https://api.vimeo.com/";
    private const string ApiVersionHeader = "application/vnd.vimeo.*+json;version=3.4";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public VimeoVideoService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<VimeoDirectUploadDto> CreateDirectUploadAsync(
        string fileName,
        long fileSize,
        string title,
        string? existingVideoUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A video file name is required.", nameof(fileName));

        if (fileSize <= 0)
            throw new ArgumentException("The video file is empty.", nameof(fileSize));

        if (fileSize > 300L * 1024 * 1024 * 1024)
            throw new InvalidOperationException("The video file exceeds Vimeo's maximum supported file size.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A video title is required.", nameof(title));

        var client = CreateAuthenticatedClient();
        HttpResponseMessage response;

        if (TryExtractVideoId(existingVideoUrl, out var existingVideoId))
        {
            var request = new
            {
                file_name = fileName,
                upload = new
                {
                    status = "in_progress",
                    size = fileSize,
                    approach = "tus"
                }
            };

            response = await client.PostAsJsonAsync(
                $"videos/{Uri.EscapeDataString(existingVideoId)}/versions",
                request,
                cancellationToken);
        }
        else
        {
            var request = new
            {
                upload = new
                {
                    approach = "tus",
                    size = fileSize
                },
                name = title.Trim(),
                privacy = new
                {
                    view = "anybody",
                    embed = "public",
                    download = false
                }
            };

            response = await client.PostAsJsonAsync(
                "me/videos",
                request,
                cancellationToken);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Vimeo upload preparation failed with status {(int)response.StatusCode}: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<VimeoVideoResponse>(
                cancellationToken);

            var videoId = ExtractVideoId(result?.Uri);
            var uploadUrl = result?.Upload?.UploadLink;

            if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(uploadUrl))
                throw new InvalidOperationException("Vimeo returned an invalid TUS upload response.");

            return new VimeoDirectUploadDto(
                videoId,
                uploadUrl,
                BuildPlayerUrl(videoId));
        }
    }

    public async Task<VimeoVideoStatusDto> GetVideoStatusAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId) || !videoId.All(char.IsDigit))
            throw new ArgumentException("A valid Vimeo video ID is required.", nameof(videoId));

        var client = CreateAuthenticatedClient();
        using var response = await client.GetAsync(
            $"videos/{Uri.EscapeDataString(videoId)}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Vimeo video status check failed with status {(int)response.StatusCode}: {error}");
        }

        var video = await response.Content.ReadFromJsonAsync<VimeoVideoResponse>(
            cancellationToken);

        if (video is null)
            throw new InvalidOperationException("Vimeo returned an invalid video status response.");

        var uploadStatus = video.Upload?.Status;
        var transcodeStatus = video.Transcode?.Status;
        var videoStatus = video.Status;

        if (string.Equals(uploadStatus, "error", StringComparison.OrdinalIgnoreCase)
            || string.Equals(transcodeStatus, "error", StringComparison.OrdinalIgnoreCase)
            || string.Equals(videoStatus, "uploading_error", StringComparison.OrdinalIgnoreCase)
            || string.Equals(videoStatus, "transcoding_error", StringComparison.OrdinalIgnoreCase))
        {
            var message = video.Upload?.Error
                ?? "Vimeo could not finish processing the video.";

            return new VimeoVideoStatusDto("error", 0, null, message);
        }

        if (string.Equals(transcodeStatus, "complete", StringComparison.OrdinalIgnoreCase)
            && video.IsPlayable == true)
        {
            return new VimeoVideoStatusDto(
                "ready",
                100,
                BuildPlayerUrl(videoId),
                "Video is ready for playback.");
        }

        return new VimeoVideoStatusDto(
            "processing",
            0,
            null,
            "Vimeo is processing the video and preparing playback.");
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var accessToken = _configuration["Vimeo:AccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Vimeo access token is not configured.");

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(VimeoApiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(ApiVersionHeader));

        return client;
    }

    private static string BuildPlayerUrl(string videoId)
        => $"https://player.vimeo.com/video/{Uri.EscapeDataString(videoId)}";

    private static string? ExtractVideoId(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            uri,
            @"/videos/(?<id>\d+)(?:/|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Groups["id"].Value : null;
    }

    private static bool TryExtractVideoId(string? playerUrl, out string videoId)
    {
        videoId = string.Empty;

        if (string.IsNullOrWhiteSpace(playerUrl)
            || !Uri.TryCreate(playerUrl, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, "player.vimeo.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 2
            || !string.Equals(segments[0], "video", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(segments[1])
            || !segments[1].All(char.IsDigit))
        {
            return false;
        }

        videoId = segments[1];
        return true;
    }

    private sealed class VimeoVideoResponse
    {
        public string? Uri { get; set; }
        public string? Status { get; set; }

        [JsonPropertyName("is_playable")]
        public bool? IsPlayable { get; set; }

        public VimeoUploadInfo? Upload { get; set; }
        public VimeoTranscodeInfo? Transcode { get; set; }
    }

    private sealed class VimeoUploadInfo
    {
        [JsonPropertyName("upload_link")]
        public string? UploadLink { get; set; }

        public string? Status { get; set; }
        public string? Error { get; set; }
    }

    private sealed class VimeoTranscodeInfo
    {
        public string? Status { get; set; }
    }
}
