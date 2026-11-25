using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace CIITStackLab.WebApp.Services;

public sealed record MeetIceServer(string[] Urls, string? Username, string? Credential);

public sealed class MeetIceServerService(
    HttpClient client,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<MeetIceServerService> logger)
{
    private const int CredentialGraceSeconds = 300;
    private const int MaxCredentialSeconds = 48 * 60 * 60;

    public async Task<IReadOnlyList<MeetIceServer>> GetAsync(
        int sessionId, string? userId, DateTime endsAtUtc, CancellationToken cancellationToken)
    {
        var iceServers = configuration.GetSection("WebRtc:IceServers")
            .GetChildren()
            .Select(server => new MeetIceServer(
                server["Urls"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                server["Username"],
                server["Credential"]))
            .Where(server => server.Urls.Length > 0)
            .ToList();

        if (iceServers.Count == 0)
            iceServers.Add(new MeetIceServer(["stun:stun.l.google.com:19302"], null, null));

        var cloudflare = configuration.GetSection("WebRtc:CloudflareTurn");
        if (!bool.TryParse(cloudflare["Enabled"], out var enabled) || !enabled || string.IsNullOrWhiteSpace(userId))
            return iceServers;

        var keyId = cloudflare["KeyId"]?.Trim();
        var apiToken = cloudflare["ApiToken"]?.Trim();
        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(apiToken) || apiToken.Any(char.IsWhiteSpace))
        {
            logger.LogWarning("Cloudflare TURN is enabled without a valid key ID and API token.");
            return iceServers;
        }

        var cacheKey = new TurnCredentialCacheKey(keyId, sessionId, userId, endsAtUtc);
        if (!cache.TryGetValue(cacheKey, out IReadOnlyList<MeetIceServer>? generatedServers))
        {
            var ttl = (int)Math.Clamp(
                Math.Ceiling((endsAtUtc - DateTime.UtcNow).TotalSeconds) + CredentialGraceSeconds,
                CredentialGraceSeconds,
                MaxCredentialSeconds);
            generatedServers = await GenerateCredentialsAsync(keyId, apiToken, ttl, cancellationToken);
            if (generatedServers is not null)
                cache.Set(cacheKey, generatedServers, TimeSpan.FromSeconds(ttl - 60));
        }

        if (generatedServers is not null)
            iceServers.AddRange(generatedServers);

        return iceServers;
    }

    private async Task<IReadOnlyList<MeetIceServer>?> GenerateCredentialsAsync(
        string keyId, string apiToken, int ttl, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"v1/turn/keys/{Uri.EscapeDataString(keyId)}/credentials/generate-ice-servers")
            {
                Content = JsonContent.Create(new { ttl })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Cloudflare TURN credential generation returned HTTP {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<CloudflareIceResponse>(
                content, JsonSerializerOptions.Web, cancellationToken);
            var iceServers = new List<MeetIceServer>();
            var hasRelayServer = false;
            foreach (var server in result?.IceServers ?? [])
            {
                if (server?.Urls is null) continue;

                var urls = server.Urls.Where(IsBrowserIceUrl).ToArray();
                if (urls.Length == 0) continue;

                var isRelayServer = urls.Any(IsTurnUrl);
                if (isRelayServer && (string.IsNullOrWhiteSpace(server.Username) || string.IsNullOrWhiteSpace(server.Credential)))
                    continue;

                iceServers.Add(server with { Urls = urls });
                hasRelayServer |= isRelayServer;
            }

            if (hasRelayServer)
                return iceServers;

            logger.LogWarning("Cloudflare TURN returned no usable relay server.");
        }
        catch (Exception error) when (error is HttpRequestException or JsonException
            || (error is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning("Cloudflare TURN credential generation failed ({FailureType}).", error.GetType().Name);
        }

        return null;
    }

    private static bool IsBrowserIceUrl(string url) =>
        !string.IsNullOrWhiteSpace(url)
        && (IsTurnUrl(url) || url.StartsWith("stun:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("stuns:", StringComparison.OrdinalIgnoreCase))
        // Web browsers block port 53, which may be returned among the alternatives.
        && !url.Split('?', 2)[0].EndsWith(":53", StringComparison.Ordinal);

    private static bool IsTurnUrl(string url) =>
        url.StartsWith("turn:", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("turns:", StringComparison.OrdinalIgnoreCase);

    private sealed record CloudflareIceResponse(MeetIceServer?[]? IceServers);

    private readonly record struct TurnCredentialCacheKey(string KeyId, int SessionId, string UserId, DateTime EndsAtUtc);
}
