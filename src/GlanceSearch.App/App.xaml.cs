using System.Threading;
using System.Windows;
using System.Windows.Interop;
using GlanceSearch.App.Theme;
using GlanceSearch.App.Views.ActionPanel;
using GlanceSearch.App.Views.History;
using GlanceSearch.App.Views.Onboarding;
using GlanceSearch.App.Views.Overlay;
using GlanceSearch.Core.ContentDetection;
using GlanceSearch.Core.OCR;
using GlanceSearch.Core.Selection;
using GlanceSearch.Infrastructure.Persistence;
using GlanceSearch.Infrastructure.Platform;
using GlanceSearch.Infrastructure.Settings;
using GlanceSearch.Infrastructure.Translation;
using GlanceSearch.Shared;
using Serilog;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace GlanceSearch.App;

/// <summary>
/// Application entry point.
/// Initializes services, system tray, and global hotkeys.
/// Runs as a tray-only app without a main window.
/// </summary>
public partial class App : Application
{
    // Single instance mutex
    private Mutex? _singleInstanceMutex;

    // Services
    private ScreenCaptureService _captureService = null!;
    private SelectionService _selectionService = null!;
    private OcrService _ocrService = null!;
    private HotkeyService _hotkeyService = null!;
    private TrayIconService _trayIconService = null!;
    private SettingsService _settingsService = null!;
    private ContentClassifier _contentClassifier = null!;
    private HistoryService _historyService = null!;
    private TranslationService _translationService = null!;

    // Hidden window for hotkey message pump
    private Window? _hiddenWindow;
    private bool _isCapturing;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Single instance guard
        if (!EnsureSingleInstance())
        {
            Shutdown();
            return;
        }

        // 2. Initialize logging
        InitializeLogging();
        Log.Information("GlanceSearch {Version} starting...", Constants.AppVersion);

        // 3. Ensure AppData directories exist
        EnsureDirectories();

        // 4. Load settings
        _settingsService = new SettingsService();
        _settingsService.Load();

        // 4.1 Initialize global theme
        ThemeService.InitializeCurrent(_settingsService.Current.General.Theme);

        // 4.2 Configure sound effects
        SoundService.SetEnabled(_settingsService.Current.General.SoundEffects);

        // 5. Initialize services
        _captureService = new ScreenCaptureService();
        _selectionService = new SelectionService();
        _ocrService = new OcrService();
        _ocrService.Initialize();
        _ocrService.SetEngine(_settingsService.Current.Ocr.Engine);
        _contentClassifier = new ContentClassifier();
        _historyService = new HistoryService();
        _translationService = new TranslationService();

        // 6. Initialize system tray
        _trayIconService = new TrayIconService();
        
        System.Drawing.Icon? appIcon = null;
        try
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
            if (streamInfo != null)
            {
                appIcon = new System.Drawing.Icon(streamInfo.Stream);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load embedded app.ico resource.");
        }

        _trayIconService.Initialize(appIcon);
        _trayIconService.CaptureRequested += (_, _) => StartCapture();
        _trayIconService.SettingsRequested += (_, _) => ShowSettings();
        _trayIconService.HistoryRequested += (_, _) => ShowHistory();
        _trayIconService.AboutRequested += (_, _) => ShowAbout();
        _trayIconService.SupportRequested += (_, _) => OpenBuyMeACoffee();
        _trayIconService.QuitRequested += (_, _) => QuitApplication();

        // 7. Onboarding check
        if (!_settingsService.Current.Onboarding.Completed)
        {
            var onboarding = new OnboardingWindow(_settingsService);
            onboarding.ShowDialog();
        }
        else if (_settingsService.Current.General.LastVersion != Constants.AppVersion)
        {
            var changelog = new GlanceSearch.App.Views.Settings.ChangelogWindow();
            changelog.ShowDialog();
        }

        // 7.1 Update trailing version number
        if (_settingsService.Current.General.LastVersion != Constants.AppVersion)
        {
            _settingsService.Current.General.LastVersion = Constants.AppVersion;
            _settingsService.Save();
        }

        // 7.2 Apply LaunchAtStartup setting to Windows registry
        ApplyLaunchAtStartup(_settingsService.Current.General.LaunchAtStartup);

        // 7.3 Show Buy Me a Coffee dialog — re-shown every ~30 days
        if (_settingsService.Current.Onboarding.Completed)
        {
            var lastShown = _settingsService.Current.Onboarding.BuyMeACoffeeLastShown;
            if (lastShown == null || (DateTime.UtcNow - lastShown.Value).TotalDays >= 30)
            {
                ShowBuyMeACoffeeDialog();
                _settingsService.Current.Onboarding.BuyMeACoffeeLastShown = DateTime.UtcNow;
                _settingsService.Save();
            }
        }

        // 7.4 Run history auto-purge on startup
        _ = RunHistoryPurgeAsync();

        // 8. Create hidden window for hotkey message pump + register global hotkey
        CreateHiddenWindow();

        // 9. Show startup notification
        if (_settingsService.Current.General.LaunchAtStartup == false && 
            _settingsService.Current.Onboarding.Completed)
        {
            _trayIconService.ShowNotification(
                Constants.AppName,
                $"Running in the background. Press {_settingsService.Current.Hotkeys.Capture} to capture.\n{Constants.AppTagline}");
        }

        // 10. Check for updates
        _ = GlanceSearch.App.Update.UpdateChecker.CheckForUpdatesAsync(_settingsService);

        Log.Information("GlanceSearch started successfully. Listening for hotkey.");
    }

    /// <summary>
    /// Ensure only one instance of the app is running.
    /// </summary>
    private bool EnsureSingleInstance()
    {
        _singleInstanceMutex = new Mutex(true, Constants.MutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            MessageBox.Show(
                "GlanceSearch is already running.\nCheck the system tray (notification area).",
                Constants.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Initialize Serilog file logging.
    /// </summary>
    private void InitializeLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                System.IO.Path.Combine(Constants.LogsPath, "glancesearch-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10_000_000)
            .CreateLogger();
    }

    /// <summary>
    /// Create required AppData directories.
    /// </summary>
    private void EnsureDirectories()
    {
        System.IO.Directory.CreateDirectory(Constants.AppDataPath);
        System.IO.Directory.CreateDirectory(Constants.CapturesPath);
        System.IO.Directory.CreateDirectory(Constants.LogsPath);
    }

    /// <summary>
    /// Create a hidden window for receiving hotkey WM_HOTKEY messages.
    /// </summary>
    private void CreateHiddenWindow()
    {
        _hiddenWindow = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };

        _hiddenWindow.Show();
        _hiddenWindow.Hide();

        var handle = new WindowInteropHelper(_hiddenWindow).Handle;

        _hotkeyService = new HotkeyService();
        _hotkeyService.Initialize(handle);

        // Register capture hotkey from settings (try custom string, fallback to default Ctrl+Shift+G)
        var captureHotkeyStr = _settingsService.Current.Hotkeys.Capture;
        var (success, _) = _hotkeyService.RegisterHotkeyFromString(captureHotkeyStr, () =>
        {
            Dispatcher.Invoke(() => StartCapture());
        });

        if (!success)
        {
            // Fallback to default
            (success, _) = _hotkeyService.RegisterCaptureHotkey(() =>
            {
                Dispatcher.Invoke(() => StartCapture());
            });
        }

        // Also register history hotkey if configured
        var historyHotkeyStr = _settingsService.Current.Hotkeys.HistoryWindow;
        if (!string.IsNullOrWhiteSpace(historyHotkeyStr))
        {
            _hotkeyService.RegisterHotkeyFromString(historyHotkeyStr, () =>
            {
                Dispatcher.Invoke(() => ShowHistory());
            });
        }

        if (!success)
        {
            Log.Warning("Failed to register capture hotkey '{Hotkey}'. It may be used by another application.", captureHotkeyStr);
            _trayIconService.ShowNotification(
                "Hotkey Conflict",
                $"{captureHotkeyStr} is already in use. Click the tray icon to capture, or change the hotkey in Settings.",
                System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    /// <summary>
    /// Start the screen capture process.
    /// </summary>
    private void StartCapture()
    {
        if (_isCapturing) return;
        _isCapturing = true;

        try
        {
            Log.Debug("Starting capture...");

            // Capture the screen
            var screenshot = _captureService.CaptureFullScreen();

            // Parse default selection mode
            var defaultMode = _settingsService.Current.Capture.DefaultSelectionMode == "rectangle"
                ? Shared.SelectionMode.Rectangle
                : Shared.SelectionMode.Freehand;

            // Show overlay
            var overlay = new OverlayWindow(screenshot, _selectionService, _ocrService, defaultMode,
                _settingsService.Current.Capture.OverlayOpacity);

            overlay.CaptureCompleted += (_, result) =>
            {
                _isCapturing = false;
                SoundService.PlayCapture();
                ShowActionPanel(result);
            };

            overlay.Closed += (_, _) =>
            {
                _isCapturing = false;
            };

            overlay.Show();
            overlay.Activate();
            overlay.Focus();
        }
        catch (Exception ex)
        {
            _isCapturing = false;
            Log.Error(ex, "Failed to start capture");
            _trayIconService.ShowNotification(
                "Capture Error",
                "Couldn't capture screen. Please try again.",
                System.Windows.Forms.ToolTipIcon.Error);
        }
    }

    /// <summary>
    /// Show the action panel with OCR results and smart content detection.
    /// </summary>
    private void ShowActionPanel(CaptureResult result)
    {
        try
        {
            // Run content classification on OCR text + captured image
            var detection = _contentClassifier.Classify(
                result.OcrResult.ExtractedText,
                result.CroppedImage);
            result.ContentDetection = detection;

            var panel = new ActionPanelWindow(
                result.OcrResult,
                result.CroppedImage,
                result.ScreenPosition,
                _settingsService,
                detection,
                _historyService,
                _translationService,
                result.SelectionBounds);

            panel.Show();
            panel.Activate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show action panel");
        }
    }

    /// <summary>
    /// Show settings window.
    /// </summary>
    private void ShowSettings()
    {
        try
        {
            var settingsWindow = new GlanceSearch.App.Views.Settings.SettingsWindow(
                _settingsService, _historyService);
            settingsWindow.SettingsChanged += (_, _) => OnSettingsChanged();
            settingsWindow.Show();
            settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show settings window");
        }
    }

    /// <summary>
    /// Re-apply runtime settings after the Settings window saves changes.
    /// </summary>
    private void OnSettingsChanged()
    {
        try
        {
            // Apply LaunchAtStartup to registry
            ApplyLaunchAtStartup(_settingsService.Current.General.LaunchAtStartup);

            // Apply OCR engine change
            _ocrService.SetEngine(_settingsService.Current.Ocr.Engine);

            // Apply theme change (takes effect on next window open)
            ThemeService.InitializeCurrent(_settingsService.Current.General.Theme);

            // Apply sound effects change
            SoundService.SetEnabled(_settingsService.Current.General.SoundEffects);

            Log.Debug("Settings applied at runtime.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error applying settings at runtime");
        }
    }

    /// <summary>
    /// Show history window.
    /// </summary>
    private void ShowHistory()
    {
        try
        {
            var historyWindow = new HistoryWindow(_historyService);
            historyWindow.Show();
            historyWindow.Activate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show history window");
        }
    }

    /// <summary>
    /// Open Buy Me a Coffee page.
    /// </summary>
    private void OpenBuyMeACoffee()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Constants.BuyMeACoffeeUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open Buy Me a Coffee URL");
        }
    }

    /// <summary>
    /// Show a friendly BMAC dialog once on first launch after onboarding.
    /// </summary>
    private void ShowBuyMeACoffeeDialog()
    {
        try
        {
            var T = GlanceSearch.App.Theme.ThemeService.Current;

            var bmacWindow = new Window
            {
                Title = "Support GlanceSearch",
                Width = 500,
                Height = 370,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = new System.Windows.Media.SolidColorBrush(T.Background),
                Foreground = T.TextBrush,
                WindowStyle = WindowStyle.ToolWindow
            };

            var root = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(36, 28, 36, 28),
                VerticalAlignment = VerticalAlignment.Center,
            };

            root.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "☕  Enjoying GlanceSearch?",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = T.TextBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            });

            root.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "GlanceSearch is built and maintained by a solo developer.\nNo investors. No subscriptions. No ads. Just clean, useful software.\n\nIf it saves you time — even once a week — a small coffee goes a long\nway toward keeping this project alive, bug-free, and improving.",
                FontSize = 13,
                Foreground = new System.Windows.Media.SolidColorBrush(T.TextSecondary),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 22),
                LineHeight = 22,
            });

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0),
            };

            var supportBtn = new System.Windows.Controls.Button
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 213, 0)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24, 11, 24, 11),
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            supportBtn.Content = new System.Windows.Controls.TextBlock
            {
                Text = "☕  Buy Me a Coffee",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(20, 13, 0))
            };
            supportBtn.Click += (_, _) => { OpenBuyMeACoffee(); bmacWindow.Close(); };
            btnPanel.Children.Add(supportBtn);

            var laterBtn = new System.Windows.Controls.Button
            {
                Background = new System.Windows.Media.SolidColorBrush(T.Surface),
                BorderBrush = new System.Windows.Media.SolidColorBrush(T.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 11, 20, 11),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            laterBtn.Content = new System.Windows.Controls.TextBlock
            {
                Text = "Maybe Later",
                FontSize = 13,
                Foreground = new System.Windows.Media.SolidColorBrush(T.TextSecondary)
            };
            laterBtn.Click += (_, _) => bmacWindow.Close();
            btnPanel.Children.Add(laterBtn);

            root.Children.Add(btnPanel);
            bmacWindow.Content = root;
            bmacWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to show Buy Me a Coffee dialog");
        }
    }

    /// <summary>
    /// Run history auto-purge based on retention settings.
    /// </summary>
    private async Task RunHistoryPurgeAsync()
    {
        try
        {
            if (_settingsService.Current.History.Enabled)
            {
                await _historyService.PurgeAsync(
                    _settingsService.Current.History.RetentionDays,
                    _settingsService.Current.History.MaxItems);
                Log.Debug("History auto-purge completed");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "History auto-purge failed");
        }
    }

    /// <summary>
    /// Apply the LaunchAtStartup setting to the Windows registry.
    /// </summary>
    private void ApplyLaunchAtStartup(bool enabled)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(Constants.AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(Constants.AppName, false);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update startup registry entry");
        }
    }

    /// <summary>
    /// Show About dialog.
    /// </summary>
    private void ShowAbout()
    {
        MessageBox.Show(
            $"{Constants.AppName} v{Constants.AppVersion}\n\n" +
            $"{Constants.AppTagline}\n\n" +
            $"Built with .NET 8 + WPF\n" +
            $"Windows OCR • Smart Content Detection\n\n" +
            $"Settings: {Constants.SettingsFilePath}\n" +
            $"Database: {Constants.HistoryDbPath}\n" +
            $"Logs: {Constants.LogsPath}",
            $"About {Constants.AppName}",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>
    /// Clean shutdown.
    /// </summary>
    private void QuitApplication()
    {
        Log.Information("GlanceSearch shutting down.");

        _hotkeyService?.Dispose();
        _trayIconService?.Dispose();
        _translationService?.Dispose();
        _hiddenWindow?.Close();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        Log.CloseAndFlush();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIconService?.Dispose();
        _translationService?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
