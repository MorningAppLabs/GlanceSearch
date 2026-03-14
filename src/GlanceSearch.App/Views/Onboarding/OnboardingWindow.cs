using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlanceSearch.Infrastructure.Settings;
using GlanceSearch.Shared;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using Label = System.Windows.Controls.Label;
using TextBox = System.Windows.Controls.TextBox;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace GlanceSearch.App.Views.Onboarding;

/// <summary>
/// First-launch onboarding wizard.
/// Guides the user through 5 easy steps to configure the app.
/// </summary>
public class OnboardingWindow : Window
{
    private readonly SettingsService _settingsService;
    private int _currentStep = 1;
    private const int MaxSteps = 5;

    private ContentControl _stepContent = null!;
    private Button _nextButton = null!;
    private Button _skipButton = null!;
    private TextBlock _stepIndicator = null!;

    public OnboardingWindow(SettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        BuildStep1_Welcome();
    }

    private void InitializeComponent()
    {
        Title = "Welcome to GlanceSearch";
        Width = 600;
        Height = 450;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        Foreground = Brushes.White;

        var mainGrid = new Grid { Margin = new Thickness(24) };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

        // Content Area
        _stepContent = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(_stepContent, 0);
        mainGrid.Children.Add(_stepContent);

        // Footer Area
        var footerGrid = new Grid { Margin = new Thickness(0, 24, 0, 0) };
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Skip
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Indicator
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Next
        Grid.SetRow(footerGrid, 1);

        _skipButton = new Button
        {
            Content = "Skip Setup",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            FontSize = 14,
            Padding = new Thickness(8, 4, 8, 4)
        };
        _skipButton.Click += (_, _) => FinishOnboarding();
        Grid.SetColumn(_skipButton, 0);
        footerGrid.Children.Add(_skipButton);

        _stepIndicator = new TextBlock
        {
            Text = "Step 1 of 5",
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13
        };
        Grid.SetColumn(_stepIndicator, 1);
        footerGrid.Children.Add(_stepIndicator);

        _nextButton = new Button
        {
            Content = "Next ➔",
            Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(16, 8, 16, 8),
            Width = 100
        };
        _nextButton.Click += (_, _) => NextStep();
        Grid.SetColumn(_nextButton, 2);
        footerGrid.Children.Add(_nextButton);

        mainGrid.Children.Add(footerGrid);
        Content = mainGrid;
    }

    private void BuildStep1_Welcome()
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        
        panel.Children.Add(new TextBlock
        {
            Text = "👋 Welcome to GlanceSearch",
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 16),
            TextAlignment = TextAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = "See it. Select it. Do anything with it.",
            FontSize = 18,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 150, 255)),
            Margin = new Thickness(0, 0, 0, 24),
            TextAlignment = TextAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = "The fastest way to interact with anything on your screen.\nLet's get you set up in less than 60 seconds.",
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400
        });

        _stepContent.Content = panel;
        UpdateFooter();
    }

    private void BuildStep2_Hotkey()
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        
        panel.Children.Add(new TextBlock
        {
            Text = "⌨️ Your Superpower",
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 16),
            TextAlignment = TextAlignment.Center
        });

        var hotkeyStr = _settingsService.Current.Hotkeys.Capture;
        
        var hotkeyBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24, 16, 24, 16),
            Margin = new Thickness(0, 0, 0, 24),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        hotkeyBorder.Child = new TextBlock
        {
            Text = hotkeyStr,
            FontSize = 24,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 100))
        };
        panel.Children.Add(hotkeyBorder);

        panel.Children.Add(new TextBlock
        {
            Text = "Press this anytime, anywhere to activate GlanceSearch.\nIt works instantly over any application or video.",
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 450
        });

        _stepContent.Content = panel;
        UpdateFooter();
    }

    private void BuildStep3_Demo()
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        
        panel.Children.Add(new TextBlock
        {
            Text = "🎯 Try It Out",
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 16),
            TextAlignment = TextAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = "When the app is running, your screen will dim slightly.\nJust draw a rectangle or freehand circle around whatever you want.",
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 24)
        });

        var demoBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 50, 60)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            BorderThickness = new Thickness(2, 2, 2, 2),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16)
        };
        
        demoBox.Child = new TextBlock
        {
            Text = "Extract me: The magic words are 'Open Sesame'.",
            FontSize = 16,
            FontStyle = FontStyles.Italic,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center
        };
        panel.Children.Add(demoBox);

        _stepContent.Content = panel;
        UpdateFooter();
    }

    private void BuildStep4_Preferences()
    {
        // Wrap in ScrollViewer to prevent clipping on small/high-DPI screens
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0)
        };

        var panel = new StackPanel { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
        
        panel.Children.Add(new TextBlock
        {
            Text = "⚙️ Preferences",
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 16),
            TextAlignment = TextAlignment.Center
        });

        // Search Engine
        panel.Children.Add(new TextBlock { Text = "Default Search Engine", Margin = new Thickness(0, 0, 0, 4), Foreground = Brushes.Gray, FontSize = 13 });
        var searchCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 12), FontSize = 13, Background = Brushes.White };
        searchCombo.Items.Add(new ComboBoxItem { Content = "Google", Tag = "google", Foreground = Brushes.Black });
        searchCombo.Items.Add(new ComboBoxItem { Content = "Bing", Tag = "bing", Foreground = Brushes.Black });
        searchCombo.Items.Add(new ComboBoxItem { Content = "DuckDuckGo", Tag = "duckduckgo", Foreground = Brushes.Black });
        searchCombo.Items.Add(new ComboBoxItem { Content = "Brave", Tag = "brave", Foreground = Brushes.Black });
        searchCombo.SelectedIndex = 0;
        
        if (_settingsService.Current.General.SearchEngine == "bing") searchCombo.SelectedIndex = 1;
        if (_settingsService.Current.General.SearchEngine == "duckduckgo") searchCombo.SelectedIndex = 2;
        if (_settingsService.Current.General.SearchEngine == "brave") searchCombo.SelectedIndex = 3;
        
        searchCombo.SelectionChanged += (_, _) => {
            if (searchCombo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                _settingsService.Current.General.SearchEngine = val;
                _settingsService.Save();
            }
        };
        panel.Children.Add(searchCombo);

        // Selection Mode
        panel.Children.Add(new TextBlock { Text = "Default Selection Mode", Margin = new Thickness(0, 0, 0, 4), Foreground = Brushes.Gray, FontSize = 13 });
        var selectionCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 12), FontSize = 13, Background = Brushes.White };
        selectionCombo.Items.Add(new ComboBoxItem { Content = "Rectangle (Standard)", Tag = "rectangle", Foreground = Brushes.Black });
        selectionCombo.Items.Add(new ComboBoxItem { Content = "Freehand (Draw around shapes)", Tag = "freehand", Foreground = Brushes.Black });
        selectionCombo.SelectedIndex = _settingsService.Current.Capture.DefaultSelectionMode == "freehand" ? 1 : 0;
        
        selectionCombo.SelectionChanged += (_, _) => {
            if (selectionCombo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                _settingsService.Current.Capture.DefaultSelectionMode = val;
                _settingsService.Save();
            }
        };
        panel.Children.Add(selectionCombo);

        // Default OCR
        panel.Children.Add(new TextBlock { Text = "Default OCR Engine", Margin = new Thickness(0, 0, 0, 4), Foreground = Brushes.Gray, FontSize = 13 });
        var ocrCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 12), FontSize = 13, Background = Brushes.White };
        ocrCombo.Items.Add(new ComboBoxItem { Content = "Windows OCR (Fast, offline)", Tag = "windows", Foreground = Brushes.Black });
        ocrCombo.Items.Add(new ComboBoxItem { Content = "Tesseract (Best for code & accuracy)", Tag = "tesseract", Foreground = Brushes.Black });
        ocrCombo.SelectedIndex = _settingsService.Current.Ocr.Engine == "tesseract" ? 1 : 0;
        
        ocrCombo.SelectionChanged += (_, _) => {
            if (ocrCombo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                _settingsService.Current.Ocr.Engine = val;
                _settingsService.Save();
            }
        };
        panel.Children.Add(ocrCombo);

        // Startup
        var startupCheck = new CheckBox
        {
            Content = "Start GlanceSearch automatically with Windows",
            IsChecked = _settingsService.Current.General.LaunchAtStartup,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 12),
            FontSize = 13
        };
        startupCheck.Checked += (_, _) => { _settingsService.Current.General.LaunchAtStartup = true; _settingsService.Save(); };
        startupCheck.Unchecked += (_, _) => { _settingsService.Current.General.LaunchAtStartup = false; _settingsService.Save(); };
        panel.Children.Add(startupCheck);

        // Privacy note
        panel.Children.Add(new TextBlock
        {
            Text = "🔒 Privacy: Extracted text and images never leave your PC unless you explicitly press 'Search' or 'Translate'.",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });

        scrollViewer.Content = panel;
        _stepContent.Content = scrollViewer;
        UpdateFooter();
    }

    private void BuildStep5_Ready()
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        
        panel.Children.Add(new TextBlock
        {
            Text = "🎉 You're All Set!",
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 16),
            TextAlignment = TextAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = "GlanceSearch is running silently in your system tray.",
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 24)
        });

        var hotkeyBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(24, 12, 24, 12),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hotkeyBorder.Child = new TextBlock
        {
            Text = $"Press {_settingsService.Current.Hotkeys.Capture} to Begin",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        };
        panel.Children.Add(hotkeyBorder);

        _stepContent.Content = panel;
        UpdateFooter();
    }

    private void NextStep()
    {
        if (_currentStep == MaxSteps)
        {
            FinishOnboarding();
            return;
        }

        _currentStep++;
        
        switch (_currentStep)
        {
            case 2: BuildStep2_Hotkey(); break;
            case 3: BuildStep3_Demo(); break;
            case 4: BuildStep4_Preferences(); break;
            case 5: BuildStep5_Ready(); break;
        }
    }

    private void UpdateFooter()
    {
        _stepIndicator.Text = $"Step {_currentStep} of {MaxSteps}";
        _skipButton.Visibility = _currentStep == MaxSteps ? Visibility.Hidden : Visibility.Visible;
        
        if (_currentStep == MaxSteps)
        {
            _nextButton.Content = "Finish ✔️";
            _nextButton.Background = new SolidColorBrush(Color.FromRgb(0, 150, 100)); // Green for final step
        }
    }

    private void FinishOnboarding()
    {
        _settingsService.Current.Onboarding.Completed = true;
        _settingsService.Current.Onboarding.CompletedVersion = Constants.AppVersion;
        _settingsService.Save();
        
        Close();
    }
}
