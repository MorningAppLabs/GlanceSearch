using System.Text.RegularExpressions;

namespace GlanceSearch.Core.ContentDetection.Detectors;

/// <summary>
/// Heuristic code snippet detector.
/// Identifies text that looks like programming source code.
/// </summary>
public static partial class CodeDetector
{
    // Common syntax characters that strongly indicate code
    private static readonly string[] CodeIndicators =
    [
        "=>", "->", "!=", "==", "<=", ">=", "&&", "||",
        "++", "--", "<<", ">>", "::", "/**", "*/",
        "public ", "private ", "protected ", "static ", "void ",
        "class ", "interface ", "enum ", "struct ", "namespace ",
        "function ", "const ", "let ", "var ",
        "import ", "export ", "return ", "throw ",
        "if (", "for (", "while (", "foreach (", "switch (",
        "try {", "catch (", "finally {",
        "def ", "self.", "lambda ", "print(",
        "Console.", "System.", "std::", "#include",
        "SELECT ", "FROM ", "WHERE ", "INSERT ", "UPDATE ",
    ];

    // Patterns that indicate code (braces, brackets, semicolons at line ends)
    [GeneratedRegex(@"[{};]\s*$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex LineEndingSyntax();

    [GeneratedRegex(@"^\s{2,}\S", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex IndentedLines();

    /// <summary>
    /// Determines if the text appears to be a code snippet.
    /// Returns a confidence score (0.0 to 1.0).
    /// </summary>
    public static (bool IsCode, double Confidence) Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (false, 0);

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return (false, 0);

        double score = 0;

        // Check for code indicator keywords/syntax
        int indicatorCount = 0;
        foreach (var indicator in CodeIndicators)
        {
            if (text.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                indicatorCount++;
        }
        score += Math.Min(indicatorCount * 0.15, 0.6);

        // Check for line-ending syntax characters (;, {, })
        int syntaxLines = LineEndingSyntax().Matches(text).Count;
        double syntaxRatio = (double)syntaxLines / lines.Length;
        score += syntaxRatio * 0.3;

        // Check for consistent indentation (hallmark of code)
        int indentedCount = IndentedLines().Matches(text).Count;
        double indentRatio = (double)indentedCount / lines.Length;
        score += indentRatio * 0.2;

        // Penalty for very short text (less likely to be code)
        if (text.Length < 30) score *= 0.5;

        score = Math.Min(score, 1.0);
        return (score >= 0.35, score);
    }
}
