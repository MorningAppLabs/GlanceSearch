using System.Text.RegularExpressions;

namespace GlanceSearch.Core.ContentDetection.Detectors;

/// <summary>
/// Detects phone numbers in OCR text.
/// Supports international formats, US, EU, and common patterns.
/// </summary>
public static partial class PhoneDetector
{
    // Matches common phone patterns:
    // +1 (555) 123-4567, +44 20 7946 0958, (555) 123-4567, 555-123-4567, +91 98765 43210
    [GeneratedRegex(
        @"(?:\+?\d{1,3}[\s\-.]?)?\(?\d{2,4}\)?[\s\-.]?\d{3,4}[\s\-.]?\d{3,4}",
        RegexOptions.Compiled)]
    private static partial Regex PhonePattern();

    /// <summary>Finds all phone numbers in the given text.</summary>
    public static List<string> Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var matches = PhonePattern().Matches(text);
        var phones = new List<string>();

        foreach (Match match in matches)
        {
            var phone = match.Value.Trim();

            // Must have at least 7 digits to be a valid phone number
            var digitCount = phone.Count(char.IsDigit);
            if (digitCount < 7 || digitCount > 15) continue;

            // Avoid matching things that look like dates, IPs, version numbers
            if (phone.Contains('.') && phone.Count(c => c == '.') >= 2) continue;

            if (!phones.Contains(phone))
                phones.Add(phone);
        }

        return phones;
    }
}
