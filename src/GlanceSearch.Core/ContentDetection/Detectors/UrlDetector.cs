using System.Text.RegularExpressions;

namespace GlanceSearch.Core.ContentDetection.Detectors;

/// <summary>
/// Detects URLs in OCR text using regex patterns.
/// Supports http/https, www., and common TLDs.
/// </summary>
public static partial class UrlDetector
{
    // Matches http(s):// URLs and www. URLs
    [GeneratedRegex(
        @"(?:https?://|www\.)[^\s<>""'\)\]\}]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlPattern();

    /// <summary>Finds all URLs in the given text.</summary>
    public static List<string> Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var matches = UrlPattern().Matches(text);
        var urls = new List<string>();

        foreach (Match match in matches)
        {
            var url = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')');

            // Ensure www. URLs have a protocol
            if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            if (!urls.Contains(url, StringComparer.OrdinalIgnoreCase))
                urls.Add(url);
        }

        return urls;
    }
}
