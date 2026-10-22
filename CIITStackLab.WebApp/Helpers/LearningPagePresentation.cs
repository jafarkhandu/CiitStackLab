using System.Text.RegularExpressions;

namespace CIITStackLab.WebApp.Helpers;

/// <summary>
/// Shared presentation helpers used by the Learn page and learning-path sidebar.
/// </summary>
public static class LearningPagePresentation
{
    public static string FormatLearningTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var title = value.Trim();
        title = Regex.Replace(title, @"(?<=[a-z0-9])(?=[A-Z])", " ");
        title = Regex.Replace(title, @"(?<=[A-Z])(?=[A-Z][a-z])", " ");
        title = Regex.Replace(title, @"(?<=\d)(?=[A-Z])", " ");
        title = Regex.Replace(title, @"(?<=\d)(?=[a-z])", " ");
        title = Regex.Replace(title, @"(?i)andthe", "and the");
        return Regex.Replace(title, @"\s{2,}", " ");
    }

    public static string? ResolveMuxPlaybackId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.Trim('/');
            if (uri.Host.Equals("player.mux.com", StringComparison.OrdinalIgnoreCase))
            {
                candidate = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            }
            else if (uri.Host.Equals("stream.mux.com", StringComparison.OrdinalIgnoreCase))
            {
                candidate = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                candidate = Path.GetFileNameWithoutExtension(candidate);
            }
            else if (path.StartsWith("uploads/videos/", StringComparison.OrdinalIgnoreCase))
            {
                candidate = path["uploads/videos/".Length..];
            }
            else
            {
                return null;
            }
        }
        else
        {
            candidate = candidate.TrimStart('/');
            if (candidate.StartsWith("uploads/videos/", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate["uploads/videos/".Length..];
            }
            else if (candidate.Contains('/') || candidate.Contains('.'))
            {
                return null;
            }
        }

        return Regex.IsMatch(candidate, @"^[A-Za-z0-9_-]{20,100}$")
            ? candidate
            : null;
    }

    public static string ResolveResourceUrl(string? value, string folder)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var resource = value.Trim().Replace('\\', '/');
        if (Uri.TryCreate(resource, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return resource;
        }

        if (resource.StartsWith("~/", StringComparison.Ordinal))
        {
            return resource[1..];
        }

        if (resource.StartsWith("/", StringComparison.Ordinal))
        {
            return resource;
        }

        if (resource.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return "/" + resource;
        }

        return $"/uploads/{folder}/{Uri.EscapeDataString(Path.GetFileName(resource))}";
    }
}
