using System.Windows.Media;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;

namespace GlanceSearch.App.Theme;

/// <summary>
/// Manages application theme (light/dark/system detection).
/// Provides color palettes and responds to Windows theme changes.
/// Access the current theme via ThemeService.Current anywhere in the app.
/// </summary>
public class ThemeService
{
    // ── Static singleton ───────────────────────────────────────────────────────
    private static ThemeService? _current;

    /// <summary>Global theme instance initialized in App.xaml.cs at startup.</summary>
    public static ThemeService Current
    {
        get => _current ??= new ThemeService();
        private set => _current = value;
    }

    /// <summary>Initialize the global instance from the user's theme setting.</summary>
    public static void InitializeCurrent(string themeSetting)
    {
        Current = new ThemeService();
        Current.Initialize(themeSetting);
    }

    // ── Instance ───────────────────────────────────────────────────────────────
    public event EventHandler? ThemeChanged;

    private bool _isDarkMode = true;

    public bool IsDarkMode => _isDarkMode;

    /// <summary>
    /// Initialize theme based on settings.
    /// </summary>
    public void Initialize(string themeSetting)
    {
        _isDarkMode = themeSetting switch
        {
            "light" => false,
            "dark" => true,
            _ => DetectSystemTheme()
        };
    }

    /// <summary>
    /// Update the theme when settings change.
    /// </summary>
    public void SetTheme(string themeSetting)
    {
        var wasDark = _isDarkMode;
        Initialize(themeSetting);

        if (wasDark != _isDarkMode)
            ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Detect Windows system theme (dark/light).
    /// </summary>
    public static bool DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0; // 0 = dark, 1 = light
        }
        catch
        {
            return true; // Default to dark
        }
    }

    #region Color Palettes

    // --- Dark Theme ---
    public static class Dark
    {
        public static Color Background => Color.FromRgb(28, 28, 28);
        public static Color Surface => Color.FromRgb(45, 45, 45);
        public static Color SurfaceHover => Color.FromRgb(61, 61, 61);
        public static Color Border => Color.FromRgb(64, 64, 64);
        public static Color Text => Color.FromRgb(255, 255, 255);
        public static Color TextSecondary => Color.FromRgb(158, 158, 158);
        public static Color TextMuted => Color.FromRgb(120, 120, 120);
        public static Color Accent => Color.FromRgb(0, 120, 212);
        public static Color AccentHover => Color.FromRgb(0, 150, 255);
        public static Color Success => Color.FromRgb(15, 123, 15);
        public static Color Danger => Color.FromRgb(180, 40, 40);
        public static Color OverlayDim => Color.FromArgb(102, 0, 0, 0); // 40% black
    }

    // --- Light Theme ---
    public static class Light
    {
        public static Color Background => Color.FromRgb(249, 249, 249);
        public static Color Surface => Color.FromRgb(255, 255, 255);
        public static Color SurfaceHover => Color.FromRgb(240, 240, 240);
        public static Color Border => Color.FromRgb(220, 220, 220);
        public static Color Text => Color.FromRgb(32, 32, 32);
        public static Color TextSecondary => Color.FromRgb(97, 97, 97);
        public static Color TextMuted => Color.FromRgb(150, 150, 150);
        public static Color Accent => Color.FromRgb(0, 100, 200);
        public static Color AccentHover => Color.FromRgb(0, 80, 170);
        public static Color Success => Color.FromRgb(20, 140, 20);
        public static Color Danger => Color.FromRgb(200, 50, 50);
        public static Color OverlayDim => Color.FromArgb(77, 0, 0, 0); // 30% black
    }

    #endregion

    #region Current Theme Accessors

    public Color Background => _isDarkMode ? Dark.Background : Light.Background;
    public Color Surface => _isDarkMode ? Dark.Surface : Light.Surface;
    public Color SurfaceHover => _isDarkMode ? Dark.SurfaceHover : Light.SurfaceHover;
    public Color Border => _isDarkMode ? Dark.Border : Light.Border;
    public Color TextColor => _isDarkMode ? Dark.Text : Light.Text;
    public Color TextSecondary => _isDarkMode ? Dark.TextSecondary : Light.TextSecondary;
    public Color TextMuted => _isDarkMode ? Dark.TextMuted : Light.TextMuted;
    public Color Accent => _isDarkMode ? Dark.Accent : Light.Accent;
    public Color AccentHover => _isDarkMode ? Dark.AccentHover : Light.AccentHover;
    public Color Success => _isDarkMode ? Dark.Success : Light.Success;
    public Color Danger => _isDarkMode ? Dark.Danger : Light.Danger;

    // Brush helpers
    public SolidColorBrush BackgroundBrush => new(Background);
    public SolidColorBrush SurfaceBrush => new(Surface);
    public SolidColorBrush BorderBrush => new(Border);
    public SolidColorBrush TextBrush => new(TextColor);
    public SolidColorBrush TextSecondaryBrush => new(TextSecondary);
    public SolidColorBrush AccentBrush => new(Accent);

    #endregion
}
