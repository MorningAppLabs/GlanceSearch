using System.Text.RegularExpressions;

namespace GlanceSearch.Core.ContentDetection.Detectors;

/// <summary>
/// Detects email addresses in OCR text.
/// </summary>
public static partial class EmailDetector
{
    [GeneratedRegex(
        @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    /// <summary>Finds all email addresses in the given text.</summary>
    public static List<string> Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var matches = EmailPattern().Matches(text);
        var emails = new List<string>();

        foreach (Match match in matches)
        {
            var email = match.Value.TrimEnd('.', ',', ';');
            if (!emails.Contains(email, StringComparer.OrdinalIgnoreCase))
                emails.Add(email);
        }

        return emails;
    }
}
