using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GlanceSearch.Infrastructure.Persistence;
using GlanceSearch.Infrastructure.Settings;
using GlanceSearch.Shared;
using GlanceSearch.Shared.Models;
using GlanceSearch.App.Theme;
using Serilog;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using ComboBox = System.Windows.Controls.ComboBox;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Label = System.Windows.Controls.Label;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace GlanceSearch.App.Views.Settings;

/// <summary>
/// Settings window with tabbed navigation for all app configuration.
/// Settings persist when switching tabs and on window close.
/// </summary>
public class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly HistoryService _historyService;
    private readonly AppSettings _settings;
    private StackPanel _contentPanel = null!;
    private readonly Dictionary<string, Button> _tabButtons = new();
    private string _activeTab = "General";

    /// <summary>
    /// Raised when the settings window closes after applying changes.
    /// Subscribe to this in App.xaml.cs to re-apply runtime settings (registry, OCR engine, etc.).
    /// </summary>
    public event EventHandler? SettingsChanged;

    // UI fields — may be null if their tab hasn't been shown
    private CheckBox? _launchAtStartup;
    private ComboBox? _themeCombo;
    private ComboBox? _searchEngineCombo;
    private CheckBox? _soundEffects;
    private CheckBox? _checkUpdates;

    private TextBox? _captureHotkey;
    private TextBox? _historyHotkey;

    private ComboBox? _selectionModeCombo;
    private Slider? _overlayOpacity;
    private CheckBox? _autoDismiss;

    private ComboBox? _ocrEngineCombo;
    private CheckBox? _autoCopyText;
    private CheckBox? _preserveFormatting;

    private Slider? _retentionDays;
    private Slider? _maxItems;
    private CheckBox? _historyEnabled;

    private ComboBox? _translationTargetCombo;

    public SettingsWindow(SettingsService settingsService, HistoryService historyService)
    {
        _settingsService = settingsService;
        _historyService = historyService;
        _settings = settingsService.Current;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Title = "⚙ GlanceSearch Settings";
        Width = 780;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        BuildLayout("General");

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };

        Closing += (_, _) =>
        {
            ApplyCurrentTab();
            _settingsService.Save();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>
    /// Builds (or rebuilds) the entire window layout. Called on first load
    /// and again whenever the user switches theme so colours update live.
    /// </summary>
    private void BuildLayout(string activeTab)
    {
        var T = ThemeService.Current;
        Background = T.BackgroundBrush;
        Foreground = T.TextBrush;

        _tabButtons.Clear();

        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // --- Sidebar ---
        var sidebar = new StackPanel
        {
            Background = new SolidColorBrush(T.IsDarkMode
                ? Color.FromRgb(35, 35, 35)
                : Color.FromRgb(235, 235, 240)),
        };

        sidebar.Children.Add(new TextBlock
        {
            Text = "⚙ Settings",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = T.TextBrush,
            Margin = new Thickness(16, 20, 0, 20),
        });

        string[] tabs = ["General", "Hotkeys", "Capture", "OCR", "Translation", "History", "About"];
        string[] icons = ["🏠", "⌨", "📷", "📝", "🌐", "📜", "ℹ"];
        for (int i = 0; i < tabs.Length; i++)
        {
            var tabName = tabs[i];
            var btn = CreateTabButton($"{icons[i]}  {tabName}");
            btn.Click += (_, _) => SwitchTab(tabName);
            _tabButtons[tabName] = btn;
            sidebar.Children.Add(btn);
        }

        Grid.SetColumn(sidebar, 0);
        mainGrid.Children.Add(sidebar);

        // --- Content area ---
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(20, 16, 20, 16)
        };

        _contentPanel = new StackPanel();
        scrollViewer.Content = _contentPanel;
        Grid.SetColumn(scrollViewer, 1);
        mainGrid.Children.Add(scrollViewer);

        Content = mainGrid;
        SwitchTab(activeTab);
    }

    private void SwitchTab(string tab)
    {
        // Save current tab's values BEFORE switching away
        ApplyCurrentTab();

        _activeTab = tab;
        _contentPanel.Children.Clear();

        // Clear field references (they'll be re-created for the new tab)
        ClearFieldReferences();

        // Update button highlighting
        foreach (var (name, btn) in _tabButtons)
        {
            btn.Background = name == tab
                ? new SolidColorBrush(ThemeService.Current.Accent)
                : Brushes.Transparent;
        }

        switch (tab)
        {
            case "General": BuildGeneralPage(); break;
            case "Hotkeys": BuildHotkeysPage(); break;
            case "Capture": BuildCapturePage(); break;
            case "OCR": BuildOcrPage(); break;
            case "Translation": BuildTranslationPage(); break;
            case "History": BuildHistoryPage(); break;
            case "About": BuildAboutPage(); break;
        }
    }

    private void ClearFieldReferences()
    {
        _launchAtStartup = null;
        _themeCombo = null;
        _searchEngineCombo = null;
        _soundEffects = null;
        _checkUpdates = null;
        _captureHotkey = null;
        _historyHotkey = null;
        _selectionModeCombo = null;
        _overlayOpacity = null;
        _autoDismiss = null;
        _ocrEngineCombo = null;
        _autoCopyText = null;
        _preserveFormatting = null;
        _retentionDays = null;
        _maxItems = null;
        _historyEnabled = null;
        _translationTargetCombo = null;
    }

    #region Tab Pages

    private void BuildGeneralPage()
    {
        AddSectionHeader("General Settings");

        _launchAtStartup = AddCheckBox("Launch at Windows startup", _settings.General.LaunchAtStartup);
        _themeCombo = AddComboBox("Theme", ["System", "Dark", "Light"],
            _settings.General.Theme == "dark" ? 1 : _settings.General.Theme == "light" ? 2 : 0);
        _searchEngineCombo = AddComboBox("Default Search Engine",
            ["Google", "Bing", "DuckDuckGo", "Brave"],
            _settings.General.SearchEngine switch
            {
                "bing" => 1, "duckduckgo" => 2, "brave" => 3, _ => 0
            });
        _soundEffects = AddCheckBox("Sound effects", _settings.General.SoundEffects);
        _checkUpdates = AddCheckBox("Check for updates automatically", _settings.General.CheckForUpdates);

        var manualUpdateBtn = CreateButton("🔄  Check for Updates Now", isAccent: true);
        manualUpdateBtn.Margin = new Thickness(0, 8, 0, 16);
        manualUpdateBtn.HorizontalAlignment = HorizontalAlignment.Left;
        manualUpdateBtn.Click += async (_, _) =>
        {
            ToastService.Show("Checking...", "Checking GitHub for updates...", ToastType.Info);
            await GlanceSearch.App.Update.UpdateChecker.CheckForUpdatesAsync(_settingsService, true);
        };
        _contentPanel.Children.Add(manualUpdateBtn);

        // Apply theme change immediately — no restart needed
        _themeCombo.SelectionChanged += (_, _) =>
        {
            ApplyCurrentTab();
            _settingsService.Save();
            var newTheme = _themeCombo.SelectedIndex switch { 1 => "dark", 2 => "light", _ => "system" };
            ThemeService.InitializeCurrent(newTheme);
            BuildLayout(_activeTab);
        };

        AddSectionHeader("Backup & Restore");
        
        var exportBtn = CreateButton("💾  Export Settings...");
        exportBtn.Margin = new Thickness(0, 0, 8, 0);
        exportBtn.HorizontalAlignment = HorizontalAlignment.Left;
        exportBtn.Click += (_, _) =>
        {
            ApplyCurrentTab();
            _settingsService.Save();
            var json = System.Text.Json.JsonSerializer.Serialize(_settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            
            var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "JSON Files (*.json)|*.json", FileName = "GlanceSearchSettings.json" };
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, json);
                ToastService.Show("Export Successful", "Your settings have been saved.", ToastType.Success);
            }
        };

        var importBtn = CreateButton("📂  Import Settings...");
        importBtn.HorizontalAlignment = HorizontalAlignment.Left;
        importBtn.Click += (_, _) =>
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "JSON Files (*.json)|*.json" };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var destination = Constants.SettingsFilePath;
                    File.Copy(ofd.FileName, destination, true);
                    ToastService.Show("Import Successful", "Restarting to apply settings...", ToastType.Success);
                    
                    // Delay shutdown slightly so toast can be seen
                    System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ => 
                        System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown())
                    );
                }
                catch (Exception ex)
                {
                    ToastService.Show("Import Failed", ex.Message, ToastType.Error);
                }
            }
        };

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        btnRow.Children.Add(exportBtn);
        btnRow.Children.Add(importBtn);
        _contentPanel.Children.Add(btnRow);
    }

    private void BuildHotkeysPage()
    {
        AddSectionHeader("Keyboard Shortcuts");

        _captureHotkey = AddTextInput("Capture Hotkey", _settings.Hotkeys.Capture);
        _historyHotkey = AddTextInput("History Hotkey", _settings.Hotkeys.HistoryWindow);

        AddLabel("Note: Changes require app restart.", 11, ThemeService.Current.TextMuted);
    }

    private void BuildCapturePage()
    {
        AddSectionHeader("Capture Settings");

        _selectionModeCombo = AddComboBox("Default Selection Mode",
            ["Rectangle", "Freehand"],
            _settings.Capture.DefaultSelectionMode == "freehand" ? 1 : 0);
        AddHint("Rectangle is precise; Freehand lets you draw any shape.");

        _overlayOpacity = AddSlider("Overlay Opacity", 0, 1, _settings.Capture.OverlayOpacity);
        AddHint("Controls how dark the screen dims during capture (0 = transparent, 1 = fully dark).");

        _autoDismiss = AddCheckBox("Auto-dismiss after action", _settings.Capture.AutoDismissAfterAction);
        AddHint("When on, the action panel closes automatically after Copy / Translate / etc.");
    }

    private void BuildOcrPage()
    {
        AddSectionHeader("OCR Settings");

        _ocrEngineCombo = AddComboBox("OCR Engine", ["Windows OCR", "Tesseract"],
            _settings.Ocr.Engine == "tesseract" ? 1 : 0);
        _autoCopyText = AddCheckBox("Auto-copy extracted text", _settings.Ocr.AutoCopyText);
        AddHint("Automatically copies OCR text to clipboard as soon as the action panel opens.");
        _preserveFormatting = AddCheckBox("Preserve line formatting", _settings.Ocr.PreserveFormatting);
        AddHint("When on, keeps original line breaks and spacing. When off, collapses whitespace into a single line.");

        AddLabel("Languages: " + string.Join(", ", _settings.Ocr.PreferredLanguages),
            11, Color.FromRgb(120, 120, 120));

        // Tesseract status and download
        AddLabel("", 8, Colors.Transparent);
        var tessdataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata", "eng.traineddata");
        var tesseractAvailable = System.IO.File.Exists(tessdataPath);

        var statusLabel = new TextBlock
        {
            Text = tesseractAvailable
                ? "✅ Tesseract trained data: Installed"
                : "⚠️ Tesseract trained data: Not found (required for Tesseract engine)",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(tesseractAvailable
                ? Color.FromRgb(72, 199, 72)    // bright green — readable in dark + light
                : Color.FromRgb(255, 196, 0)),  // bright amber — readable in dark + light
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        _contentPanel.Children.Add(statusLabel);

        if (!tesseractAvailable)
        {
            var downloadBtn = CreateButton("⬇️  Download Tesseract Data (~15 MB)", isAccent: true);
            downloadBtn.HorizontalAlignment = HorizontalAlignment.Left;
            downloadBtn.Margin = new Thickness(0, 0, 0, 8);
            downloadBtn.Click += async (_, _) =>
            {
                downloadBtn.IsEnabled = false;
                if (downloadBtn.Content is TextBlock dtb) dtb.Text = "⏳  Downloading...";
                statusLabel.Text = "⏳ Downloading Tesseract trained data...";
                try
                {
                    var ocrService = new GlanceSearch.Core.OCR.OcrService();
                    ocrService.Initialize();
                    await ocrService.EnsureTesseractDataAsync();
                    statusLabel.Text = "✅ Tesseract trained data: Installed";
                    statusLabel.Foreground = new SolidColorBrush(Color.FromRgb(72, 199, 72));
                    ToastService.Show("Download Complete", "Tesseract data downloaded successfully.", ToastType.Success);
                    if (downloadBtn.Content is TextBlock stb) stb.Text = "✅  Downloaded";
                }
                catch (Exception ex)
                {
                    statusLabel.Text = $"❌ Download failed: {ex.Message}";
                    statusLabel.Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                    downloadBtn.IsEnabled = true;
                    if (downloadBtn.Content is TextBlock rtb) rtb.Text = "⬇️  Retry Download";
                }
            };
            _contentPanel.Children.Add(downloadBtn);
        }

        AddLabel("Note: Tesseract offers better accuracy for code and complex text. Windows OCR is faster for general use.", 11, ThemeService.Current.TextMuted);
    }

    private void BuildTranslationPage()
    {
        AddSectionHeader("Translation Settings");

        AddLabel("Translation is powered by Google Translate — free, no API key required.", 12, ThemeService.Current.TextSecondary);
        AddLabel("", 8, Colors.Transparent);
        
        // Build target languages dropdown manually for more options
        var targetLangs = GlanceSearch.Infrastructure.Translation.TranslationService.SupportedLanguages
            .Where(kvp => kvp.Key != "auto")
            .Select(kvp => kvp.Value)
            .ToArray();
            
        var targetCodes = GlanceSearch.Infrastructure.Translation.TranslationService.SupportedLanguages
            .Where(kvp => kvp.Key != "auto")
            .Select(kvp => kvp.Key)
            .ToList();
            
        var currentIdx = targetCodes.IndexOf(_settings.Translation.DefaultTargetLanguage);
        if (currentIdx < 0) currentIdx = 0; // fallback to English

        _translationTargetCombo = AddComboBox("Default Target Language", targetLangs, currentIdx);
        // Store the codes list in Tag
        _translationTargetCombo.Tag = targetCodes;
    }

    private void BuildHistoryPage()
    {
        AddSectionHeader("History Settings");

        _historyEnabled = AddCheckBox("Enable capture history", _settings.History.Enabled);
        AddHint("When disabled, the \"Save to History\" button is hidden from the action panel.");
        _retentionDays = AddSlider("Retention (days)", 7, 365, _settings.History.RetentionDays);
        AddHint("Captures older than this are automatically deleted at startup.");
        _maxItems = AddSlider("Max items", 100, 10000, _settings.History.MaxItems);
        AddHint("When the limit is reached, the oldest captures are purged automatically.");

        var storageBytes = _historyService.GetStorageSize();
        var storageMb = storageBytes / (1024.0 * 1024.0);
        AddLabel($"Current storage usage: {storageMb:F1} MB / {_settings.History.MaxStorageMb} MB",
            11, Color.FromRgb(120, 120, 120));

        var clearBtn = CreateButton("🗑  Clear All History", isDanger: true);
        clearBtn.Margin = new Thickness(0, 12, 0, 0);
        clearBtn.HorizontalAlignment = HorizontalAlignment.Left;
        clearBtn.Click += async (_, _) =>
        {
            var result = MessageBox.Show("Delete all history and captured images?",
                "Clear History", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _historyService.ClearAllAsync();
                ToastService.Show("History Cleared", "All captured items have been deleted.", ToastType.Success);
            }
        };
        _contentPanel.Children.Add(clearBtn);
    }

    private void BuildAboutPage()
    {
        var T = ThemeService.Current;
        AddSectionHeader("About GlanceSearch");

        AddLabel($"Version: {Constants.AppVersion}", 14, T.TextColor);
        AddLabel(Constants.AppTagline, 12, T.TextSecondary);
        AddLabel("", 6, Colors.Transparent);

        // Buy Me a Coffee section
        var bmacBorder = new Border
        {
            Background = new SolidColorBrush(T.Surface),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 16),
            MaxWidth = 480,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromRgb(255, 213, 0)),
            BorderThickness = new Thickness(1)
        };
        var bmacStack = new StackPanel();
        bmacStack.Children.Add(new TextBlock
        {
            Text = "☕ Support GlanceSearch",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = T.TextBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });
        bmacStack.Children.Add(new TextBlock
        {
            Text = "GlanceSearch is 100% free with no ads. If you find it useful, consider supporting the developer with a coffee!",
            FontSize = 12,
            Foreground = T.TextSecondaryBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });
        var bmacBtn = new Button
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 221, 0)),
            Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(20, 8, 20, 8),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        bmacBtn.Content = new TextBlock
        {
            Text = "☕ Buy Me a Coffee",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0))
        };
        bmacBtn.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://buymeacoffee.com/morningapplabs") { UseShellExecute = true }); }
            catch { }
        };
        bmacStack.Children.Add(bmacBtn);
        bmacBorder.Child = bmacStack;
        _contentPanel.Children.Add(bmacBorder);

        var linksPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };

        var githubBtn = CreateButton("🌐  GitHub Repository");
        githubBtn.Margin = new Thickness(0, 0, 0, 6);
        githubBtn.HorizontalAlignment = HorizontalAlignment.Left;
        githubBtn.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://github.com/MorningAppLabs/GlanceSearch") { UseShellExecute = true }); }
            catch { }
        };
        linksPanel.Children.Add(githubBtn);

        var privacyBtn = CreateButton("📄  Privacy Policy");
        privacyBtn.Margin = new Thickness(0, 0, 0, 6);
        privacyBtn.HorizontalAlignment = HorizontalAlignment.Left;
        privacyBtn.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://github.com/MorningAppLabs/GlanceSearch/blob/main/PRIVACY.md") { UseShellExecute = true }); }
            catch { }
        };
        linksPanel.Children.Add(privacyBtn);

        _contentPanel.Children.Add(linksPanel);

        AddLabel("", 12, Colors.Transparent);
        
        var replayBtn = CreateButton("✨  Replay Welcome Setup", isAccent: true);
        replayBtn.HorizontalAlignment = HorizontalAlignment.Left;
        replayBtn.Margin = new Thickness(0, 0, 0, 10);
        replayBtn.Click += (_, _) =>
        {
            var onboarding = new GlanceSearch.App.Views.Onboarding.OnboardingWindow(_settingsService);
            onboarding.ShowDialog();
        };
        _contentPanel.Children.Add(replayBtn);
        var resetBtn = CreateButton("⚠  Reset All Settings", isDanger: true);
        resetBtn.HorizontalAlignment = HorizontalAlignment.Left;
        resetBtn.Click += (_, _) =>
        {
            var result = MessageBox.Show("Reset all settings to defaults?",
                "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _settingsService.Reset();
                Close();
            }
        };
        _contentPanel.Children.Add(resetBtn);

        // File paths (useful for debugging / support)
        AddLabel("", 12, Colors.Transparent);
        AddLabel("📂 Data Locations", 13, T.TextColor);
        AddLabel($"Settings: {Constants.SettingsFilePath}", 10, T.TextMuted);
        AddLabel($"Database: {Constants.HistoryDbPath}", 10, T.TextMuted);
        AddLabel($"Logs: {Constants.LogsPath}", 10, T.TextMuted);
    }

    #endregion

    #region Apply Settings (per-tab to preserve across tab switches)

    private void ApplyCurrentTab()
    {
        try
        {
            // General
            if (_launchAtStartup != null) _settings.General.LaunchAtStartup = _launchAtStartup.IsChecked == true;
            if (_themeCombo != null) _settings.General.Theme = _themeCombo.SelectedIndex switch
            {
                1 => "dark", 2 => "light", _ => "system"
            };
            if (_searchEngineCombo != null) _settings.General.SearchEngine = _searchEngineCombo.SelectedIndex switch
            {
                1 => "bing", 2 => "duckduckgo", 3 => "brave", _ => "google"
            };
            if (_soundEffects != null) _settings.General.SoundEffects = _soundEffects.IsChecked == true;
            if (_checkUpdates != null) _settings.General.CheckForUpdates = _checkUpdates.IsChecked == true;

            // Hotkeys
            if (_captureHotkey != null) _settings.Hotkeys.Capture = _captureHotkey.Text;
            if (_historyHotkey != null) _settings.Hotkeys.HistoryWindow = _historyHotkey.Text;

            // Capture
            if (_selectionModeCombo != null) _settings.Capture.DefaultSelectionMode =
                _selectionModeCombo.SelectedIndex == 1 ? "freehand" : "rectangle";
            if (_overlayOpacity != null) _settings.Capture.OverlayOpacity = _overlayOpacity.Value;
            if (_autoDismiss != null) _settings.Capture.AutoDismissAfterAction = _autoDismiss.IsChecked == true;

            // OCR
            if (_ocrEngineCombo != null) _settings.Ocr.Engine =
                _ocrEngineCombo.SelectedIndex == 1 ? "tesseract" : "windows";
            if (_autoCopyText != null) _settings.Ocr.AutoCopyText = _autoCopyText.IsChecked == true;
            if (_preserveFormatting != null) _settings.Ocr.PreserveFormatting = _preserveFormatting.IsChecked == true;

            // History
            if (_historyEnabled != null) _settings.History.Enabled = _historyEnabled.IsChecked == true;
            if (_retentionDays != null) _settings.History.RetentionDays = (int)_retentionDays.Value;
            if (_maxItems != null) _settings.History.MaxItems = (int)_maxItems.Value;

            // Translation
            if (_translationTargetCombo != null && _translationTargetCombo.Tag is System.Collections.Generic.List<string> codes)
            {
                var idx = _translationTargetCombo.SelectedIndex;
                if (idx >= 0 && idx < codes.Count)
                    _settings.Translation.DefaultTargetLanguage = codes[idx];
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply settings");
        }
    }

    #endregion

    #region UI Builders

    private Button CreateTabButton(string text)
    {
        var T = ThemeService.Current;

        // ControlTemplate with TemplateBinding so btn.Background propagates to the rendered border
        var borderFef = new FrameworkElementFactory(typeof(Border));
        borderFef.SetValue(Border.BackgroundProperty,
            new TemplateBindingExtension(Button.BackgroundProperty));

        var cpFef = new FrameworkElementFactory(typeof(ContentPresenter));
        cpFef.SetValue(ContentPresenter.MarginProperty, new Thickness(16, 10, 16, 10));
        cpFef.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        cpFef.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFef.AppendChild(cpFef);

        var template = new ControlTemplate(typeof(Button));
        template.VisualTree = borderFef;

        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = T.TextBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var btn = new Button
        {
            Template = template,
            Content = textBlock,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
        };

        // Hover only affects inactive buttons — active tab keeps its Accent background
        btn.MouseEnter += (_, _) =>
        {
            if (_tabButtons.TryGetValue(_activeTab, out var active) && btn != active)
                btn.Background = new SolidColorBrush(ThemeService.Current.SurfaceHover);
        };
        btn.MouseLeave += (_, _) =>
        {
            if (_tabButtons.TryGetValue(_activeTab, out var active) && btn != active)
                btn.Background = Brushes.Transparent;
        };

        return btn;
    }

    private void AddSectionHeader(string title)
    {
        _contentPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = ThemeService.Current.TextBrush,
            Margin = new Thickness(0, 0, 0, 16),
        });
    }

    private CheckBox AddCheckBox(string label, bool isChecked)
    {
        var T = ThemeService.Current;
        var cb = new CheckBox
        {
            Content = new TextBlock { Text = label, FontSize = 13, Foreground = T.TextBrush },
            IsChecked = isChecked,
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = T.TextBrush,
        };
        _contentPanel.Children.Add(cb);
        return cb;
    }

    private ComboBox AddComboBox(string label, string[] items, int selectedIndex)
    {
        var T = ThemeService.Current;
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = T.TextSecondaryBrush,
            Margin = new Thickness(0, 0, 0, 4),
        });

        var combo = new ComboBox
        {
            FontSize = 13,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        foreach (var item in items)
        {
            combo.Items.Add(new ComboBoxItem
            {
                Content = item,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                FontSize = 13,
            });
        }

        combo.SelectedIndex = selectedIndex;
        panel.Children.Add(combo);
        _contentPanel.Children.Add(panel);
        return combo;
    }

    private TextBox AddTextInput(string label, string value)
    {
        var T = ThemeService.Current;
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = T.TextSecondaryBrush,
            Margin = new Thickness(0, 0, 0, 4),
        });

        var textBox = new TextBox
        {
            Text = value,
            FontSize = 13,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = T.SurfaceBrush,
            Foreground = T.TextBrush,
            BorderBrush = T.BorderBrush,
            Padding = new Thickness(6, 4, 6, 4),
        };

        panel.Children.Add(textBox);
        _contentPanel.Children.Add(panel);
        return textBox;
    }

    private Slider AddSlider(string label, double min, double max, double value)
    {
        var T = ThemeService.Current;
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var valueLabel = new TextBlock
        {
            Text = $"{value:F0}",
            FontSize = 12,
            Foreground = T.TextBrush,
        };

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
        headerPanel.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            FontSize = 12,
            Foreground = T.TextSecondaryBrush,
        });
        headerPanel.Children.Add(valueLabel);
        panel.Children.Add(headerPanel);

        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Width = 250,
            HorizontalAlignment = HorizontalAlignment.Left,
            TickFrequency = max <= 1 ? 0.05 : (max > 1000 ? 100 : 1),
            IsSnapToTickEnabled = max > 1,
            Margin = new Thickness(0, 4, 0, 0),
        };

        slider.ValueChanged += (_, e) =>
        {
            valueLabel.Text = max <= 1 ? $"{e.NewValue:F2}" : $"{e.NewValue:F0}";
        };

        panel.Children.Add(slider);
        _contentPanel.Children.Add(panel);
        return slider;
    }

    private void AddLabel(string text, double fontSize, Color color)
    {
        _contentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = new SolidColorBrush(color),
            Margin = new Thickness(0, 0, 0, 4),
            TextWrapping = TextWrapping.Wrap,
        });
    }

    /// <summary>
    /// Adds a small muted hint line below a setting to explain what it does.
    /// </summary>
    private void AddHint(string text)
    {
        _contentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(ThemeService.Current.TextMuted),
            Margin = new Thickness(2, -6, 0, 10),
            TextWrapping = TextWrapping.Wrap,
        });
    }

    /// <summary>
    /// Creates a themed button with a proper WPF ControlTemplate so hover and pressed
    /// states work correctly in both light and dark mode without the default Aero style interfering.
    /// </summary>
    private Button CreateButton(string text, bool isAccent = false, bool isDanger = false)
    {
        var T = ThemeService.Current;

        var bg = isDanger  ? Color.FromRgb(180, 40, 40)
               : isAccent  ? T.Accent
               :             T.Surface;
        var fg = (isDanger || isAccent) ? Colors.White : T.TextColor;
        var hoverBg = isDanger  ? Color.FromRgb(210, 55, 55)
                    : isAccent  ? Color.FromRgb(
                          (byte)Math.Min(255, T.Accent.R + 22),
                          (byte)Math.Min(255, T.Accent.G + 18),
                          (byte)Math.Min(255, T.Accent.B + 15))
                    : T.SurfaceHover;
        var pressedBg = isDanger  ? Color.FromRgb(148, 28, 28)
                      : isAccent  ? Color.FromRgb(
                            (byte)Math.Max(0, T.Accent.R - 18),
                            (byte)Math.Max(0, T.Accent.G - 15),
                            (byte)Math.Max(0, T.Accent.B - 12))
                      : T.Border;

        // ControlTemplate eliminates WPF Aero hover override
        var borderFef = new FrameworkElementFactory(typeof(Border));
        borderFef.Name = "bd";
        borderFef.SetValue(Border.BackgroundProperty, new SolidColorBrush(bg));
        borderFef.SetValue(Border.BorderBrushProperty, new SolidColorBrush(T.Border));
        borderFef.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFef.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFef.SetValue(Border.SnapsToDevicePixelsProperty, true);

        var cpFef = new FrameworkElementFactory(typeof(ContentPresenter));
        cpFef.SetValue(ContentPresenter.MarginProperty, new Thickness(14, 7, 14, 7));
        cpFef.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cpFef.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFef.AppendChild(cpFef);

        var template = new ControlTemplate(typeof(Button));
        template.VisualTree = borderFef;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBg), "bd"));
        template.Triggers.Add(hoverTrigger);

        var pressedTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(pressedBg), "bd"));
        template.Triggers.Add(pressedTrigger);

        var btn = new Button
        {
            Template = template,
            Content = new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(fg) },
            Cursor = Cursors.Hand,
            Focusable = true,
        };
        return btn;
    }

    #endregion
}
