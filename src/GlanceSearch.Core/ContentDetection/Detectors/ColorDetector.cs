using System.Text.RegularExpressions;

namespace GlanceSearch.Core.ContentDetection.Detectors;

/// <summary>
/// Detects color values in OCR text.
/// Supports hex (#FF5733, #fff), RGB, RGBA, HSL patterns.
/// </summary>
public static partial class ColorDetector
{
    // Hex colors: #RGB, #RRGGBB, #RRGGBBAA
    [GeneratedRegex(
        @"#(?:[0-9a-fA-F]{3}){1,2}(?:[0-9a-fA-F]{2})?(?!\w)",
        RegexOptions.Compiled)]
    private static partial Regex HexColorPattern();

    // rgb(r, g, b) or rgba(r, g, b, a)
    [GeneratedRegex(
        @"rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(?:,\s*[\d.]+\s*)?\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RgbColorPattern();

    // hsl(h, s%, l%) or hsla(h, s%, l%, a)
    [GeneratedRegex(
        @"hsla?\(\s*\d{1,3}\s*,\s*\d{1,3}%\s*,\s*\d{1,3}%\s*(?:,\s*[\d.]+\s*)?\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HslColorPattern();

    /// <summary>Finds all color values in the given text.</summary>
    public static List<string> Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var colors = new List<string>();

        foreach (Match m in HexColorPattern().Matches(text))
            if (!colors.Contains(m.Value, StringComparer.OrdinalIgnoreCase))
                colors.Add(m.Value);

        foreach (Match m in RgbColorPattern().Matches(text))
            if (!colors.Contains(m.Value, StringComparer.OrdinalIgnoreCase))
                colors.Add(m.Value);

        foreach (Match m in HslColorPattern().Matches(text))
            if (!colors.Contains(m.Value, StringComparer.OrdinalIgnoreCase))
                colors.Add(m.Value);

        return colors;
    }
}
