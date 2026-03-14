namespace GlanceSearch.Shared;

/// <summary>
/// Application-wide constants.
/// </summary>
public static class Constants
{
    public const string AppName = "GlanceSearch";
    public const string AppVersion = "1.0.0";
    public const string AppTagline = "See it. Select it. Do anything with it.";
    public const string MutexName = "Global\\GlanceSearch_SingleInstance_Mutex";
    public const string BuyMeACoffeeUrl = "https://buymeacoffee.com/morningapplabs";
    public const string GitHubUrl = "https://github.com/MorningAppLabs/GlanceSearch";

    // Default hotkey: Ctrl+Shift+G
    public const int DefaultHotkeyModifiers = 0x0006; // MOD_CONTROL | MOD_SHIFT
    public const int DefaultHotkeyKey = 0x47;          // VK_G

    // Paths
    public static string AppDataPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    public static string SettingsFilePath =>
        Path.Combine(AppDataPath, "settings.json");

    public static string HistoryDbPath =>
        Path.Combine(AppDataPath, "history.db");

    public static string CapturesPath =>
        Path.Combine(AppDataPath, "captures");

    public static string LogsPath =>
        Path.Combine(AppDataPath, "logs");

    // UI
    public const double OverlayOpacity = 0.4;
    public const int MinSelectionSize = 20;
    public const double ActionPanelMaxWidth = 450;
    public const double ActionPanelMaxHeight = 500;
    public const int PanelCornerRadius = 12;

    // Animations
    public const int OverlayFadeInMs = 150;
    public const int OverlayFadeOutMs = 100;
    public const int PanelAppearMs = 200;
    public const int PanelDismissMs = 150;

    // Search URLs
    public static readonly Dictionary<string, string> SearchEngineUrls = new()
    {
        ["google"] = "https://www.google.com/search?q={0}",
        ["bing"] = "https://www.bing.com/search?q={0}",
        ["duckduckgo"] = "https://duckduckgo.com/?q={0}",
        ["brave"] = "https://search.brave.com/search?q={0}",
    };
}
