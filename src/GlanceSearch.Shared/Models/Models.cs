using System.Text.Json.Serialization;

namespace GlanceSearch.Shared.Models;

/// <summary>
/// Represents the result of an OCR operation.
/// </summary>
public class OcrResult
{
    public string ExtractedText { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string DetectedLanguage { get; set; } = "en";
    public string EngineUsed { get; set; } = "Windows";
    public List<OcrLine> Lines { get; set; } = [];
    public bool IsEmpty => string.IsNullOrWhiteSpace(ExtractedText);
}

public class OcrLine
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public Rect BoundingBox { get; set; }
}

public struct Rect
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public Rect(double x, double y, double width, double height)
    {
        X = x; Y = y; Width = width; Height = height;
    }
}

/// <summary>
/// Application settings model matching PRD JSON schema.
/// </summary>
public class AppSettings
{
    public string Schema { get; set; } = "glancesearch-settings-v1";
    public int Version { get; set; } = 1;
    public GeneralSettings General { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public CaptureSettings Capture { get; set; } = new();
    public OcrSettings Ocr { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();
    public HistorySettings History { get; set; } = new();
    public OnboardingSettings Onboarding { get; set; } = new();
}

public class GeneralSettings
{
    public string LastVersion { get; set; } = "";
    public bool LaunchAtStartup { get; set; } = false;
    public string Theme { get; set; } = "system";
    public bool SoundEffects { get; set; } = true;
    public string NotificationStyle { get; set; } = "toast";
    public string SearchEngine { get; set; } = "google";
    public string CustomSearchUrl { get; set; } = "";
    public bool CheckForUpdates { get; set; } = true;
    public bool SendTelemetry { get; set; } = false;
}

public class HotkeySettings
{
    public string Capture { get; set; } = "Ctrl+Shift+G";
    public string QuickOcr { get; set; } = "Ctrl+Shift+C";
    public string HistoryWindow { get; set; } = "Ctrl+Shift+H";
}

public class CaptureSettings
{
    public string DefaultSelectionMode { get; set; } = "rectangle";
    public bool ShowMagnifier { get; set; } = false;
    public double OverlayOpacity { get; set; } = 0.4;
    public string SelectionBorderColor { get; set; } = "#0078D4";
    public bool AutoDismissAfterAction { get; set; } = true;
    public bool CaptureSound { get; set; } = true;
}

public class OcrSettings
{
    public string Engine { get; set; } = "windows";
    public List<string> PreferredLanguages { get; set; } = ["en-US"];
    public bool PreserveFormatting { get; set; } = true;
    public bool AutoCopyText { get; set; } = false;
}

public class TranslationSettings
{
    public string Service { get; set; } = "free"; // free (MyMemory), deepl, google, azure
    
    [JsonIgnore]
    public string ApiKey { get; set; } = "";
    
    [JsonPropertyName("ApiKey")]
    public string EncryptedApiKey { get; set; } = "";
    
    public string DefaultTargetLanguage { get; set; } = "en";
    public bool AutoTranslate { get; set; } = false;
    public bool ShowOriginalAndTranslation { get; set; } = true;
    public string LastSourceLanguage { get; set; } = "auto";
    public string LastTargetLanguage { get; set; } = "en";
}

public class HistorySettings
{
    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 90;
    public int MaxItems { get; set; } = 1000;
    public int MaxStorageMb { get; set; } = 500;
    public bool PauseHistory { get; set; } = false;
}

public class OnboardingSettings
{
    public bool Completed { get; set; } = false;
    public string? CompletedVersion { get; set; }
    /// <summary>UTC time the BMAC prompt was last shown. Null = never shown.</summary>
    public DateTime? BuyMeACoffeeLastShown { get; set; } = null;
}
