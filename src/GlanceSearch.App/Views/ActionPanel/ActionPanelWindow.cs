using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using GlanceSearch.Infrastructure.Persistence;
using GlanceSearch.Infrastructure.Settings;
using GlanceSearch.Infrastructure.Translation;
using GlanceSearch.Shared;
using GlanceSearch.Shared.Models;
using Serilog;
using GlanceSearch.App.Theme;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace GlanceSearch.App.Views.ActionPanel;

/// <summary>
/// Floating action panel that shows extracted text, smart content detection, and contextual actions.
/// Appears after a region is captured and OCR'd.
/// </summary>
public class ActionPanelWindow : Window
{
    private readonly OcrResult _ocrResult;
    private readonly Bitmap? _capturedImage;
    private readonly SettingsService _settingsService;
    private readonly ContentDetectionResult _detection;
    private readonly HistoryService _historyService;
    private readonly TranslationService _translationService;
    private readonly System.Drawing.Rectangle _selectionBounds;
    private TextBox _textBox = null!;
    private Border _mainBorder = null!;
    private bool _isReady = false;
    private bool _isDismissing = false;
    private bool _isHandlingDialog = false;

    public ActionPanelWindow(
        OcrResult ocrResult,
        Bitmap? capturedImage,
        System.Drawing.Point screenPosition,
        SettingsService settingsService,
        ContentDetectionResult detection,
        HistoryService historyService,
        TranslationService translationService,
        System.Drawing.Rectangle selectionBounds)
    {
        _ocrResult = ocrResult;
        _capturedImage = capturedImage;
        _settingsService = settingsService;
        _detection = detection;
        _historyService = historyService;
        _translationService = translationService;
        _selectionBounds = selectionBounds;

        InitializeComponent(screenPosition);
    }

    private void InitializeComponent(System.Drawing.Point screenPosition)
    {
        // Window properties
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        Width = 420;
        MaxHeight = Constants.ActionPanelMaxHeight;

        // Smart positioning: prefer bottom-right of selection, avoid off-screen
        var screenBounds = SystemParameters.WorkArea;
        double posX = screenPosition.X + 20;
        double posY = screenPosition.Y + 20;

        if (posX + Width > screenBounds.Right)
            posX = screenPosition.X - Width - 20;
        if (posY + 400 > screenBounds.Bottom)
            posY = screenBounds.Bottom - 420;

        posX = Math.Max(screenBounds.Left, posX);
        posY = Math.Max(screenBounds.Top, posY);

        Left = posX;
        Top = posY;

        // Main border (rounded panel)
        _mainBorder = new Border
        {
            CornerRadius = new CornerRadius(Constants.PanelCornerRadius),
            Background = new SolidColorBrush(ThemeService.Current.Surface),
            BorderBrush = new SolidColorBrush(ThemeService.Current.Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16),
            Effect = new DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 4,
                Opacity = 0.3,
                Color = Colors.Black
            },
            RenderTransform = new ScaleTransform(0.95, 0.95),
            RenderTransformOrigin = new System.Windows.Point(0.5, 0)
        };

        // Enable drag to reposition the panel
        _mainBorder.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        };

        // Content stack
        var stack = new StackPanel();

        // --- Selection preview thumbnail ---
        if (_capturedImage != null)
        {
            var thumbnailBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                MaxHeight = 120,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var thumbnail = new System.Windows.Controls.Image
            {
                Source = ConvertBitmapToSource(_capturedImage),
                Stretch = Stretch.Uniform,
                MaxHeight = 120
            };
            thumbnailBorder.Child = thumbnail;
            stack.Children.Add(thumbnailBorder);
        }

        // --- Extracted text section ---
        var textHeader = new Grid { Margin = new Thickness(0, 8, 0, 4) };
        textHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        textHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = _ocrResult.IsEmpty ? "No text detected" : "Extracted Text:",
            Foreground = new SolidColorBrush(ThemeService.Current.TextSecondary),
            FontSize = 12,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 0);
        textHeader.Children.Add(label);

        if (!_ocrResult.IsEmpty)
        {
            var copySmall = CreateActionButton("📋", "Copy", () => CopyText());
            Grid.SetColumn(copySmall, 1);
            textHeader.Children.Add(copySmall);
        }
        stack.Children.Add(textHeader);

        // Editable text box
        var rawText = _ocrResult.ExtractedText ?? "";
        // PreserveFormatting: when OFF, collapse runs of whitespace to single spaces
        var displayText = (!string.IsNullOrEmpty(rawText) && !_settingsService.Current.Ocr.PreserveFormatting)
            ? System.Text.RegularExpressions.Regex.Replace(rawText.Trim(), @"[ \t]+", " ")
            : rawText;

        _textBox = new TextBox
        {
            Text = displayText,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            MaxHeight = 150,
            TabIndex = 1,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 13,
            FontFamily = new System.Windows.Media.FontFamily(_detection.IsCode ? "Cascadia Code, Consolas" : "Segoe UI"),
            Foreground = new SolidColorBrush(ThemeService.Current.TextColor),
            Background = new SolidColorBrush(ThemeService.Current.Background),
            BorderBrush = new SolidColorBrush(ThemeService.Current.Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            IsReadOnly = _ocrResult.IsEmpty
        };
        var textBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = _textBox
        };
        stack.Children.Add(textBorder);

        // --- Smart Content Detection section ---
        if (_detection.HasSmartContent)
        {
            stack.Children.Add(BuildSmartContentSection());
        }

        // --- Action buttons ---
        var buttonPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 0)
        };

        // Core actions (text-based)
        if (!_ocrResult.IsEmpty)
        {
            buttonPanel.Children.Add(CreateActionButton("📋 Copy", "Ctrl+C", () => CopyText()));
            buttonPanel.Children.Add(CreateActionButton("🔍 Search", "Ctrl+S", () => SearchText()));
            buttonPanel.Children.Add(CreateActionButton("🌐 Translate", "Ctrl+T", () => TranslateText()));
        }

        // Always available actions
        buttonPanel.Children.Add(CreateActionButton("📷 Image Search", null, () => ImageSearch()));
        
        if (_capturedImage != null)
        {
            buttonPanel.Children.Add(CreateActionButton("💾 Save Image", "Ctrl+I", () => SaveImage()));
        }

        // Only show Save to History if history is enabled
        if (_settingsService.Current.History.Enabled)
        {
            buttonPanel.Children.Add(CreateActionButton("📌 Save to History", "Ctrl+D", () => SaveToHistory()));
        }

        stack.Children.Add(buttonPanel);

        _mainBorder.Child = stack;
        Content = _mainBorder;

        // Appear animation — animate the border, not the Window
        Opacity = 0;

        Loaded += (_, _) =>
        {
            var scaleXAnim = new DoubleAnimation(0.95, 1, TimeSpan.FromMilliseconds(Constants.PanelAppearMs))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleYAnim = new DoubleAnimation(0.95, 1, TimeSpan.FromMilliseconds(Constants.PanelAppearMs))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(Constants.PanelAppearMs));

            if (_mainBorder.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            }
            BeginAnimation(OpacityProperty, fadeAnim);

            _textBox.Focus();

            // AutoCopyText: copy extracted text to clipboard as soon as the panel appears
            if (_settingsService.Current.Ocr.AutoCopyText && !_ocrResult.IsEmpty)
            {
                try { System.Windows.Clipboard.SetText(_textBox.Text); }
                catch { /* clipboard may be locked */ }
            }

            // Allow Deactivated dismissal only after panel is fully shown
            var readyTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            readyTimer.Tick += (_, _) =>
            {
                _isReady = true;
                readyTimer.Stop();
            };
            readyTimer.Start();
        };

        // Keyboard shortcuts
        KeyDown += OnKeyDown;

        // Click outside to dismiss (only after panel is ready)
        Deactivated += (_, _) =>
        {
            if (_isReady && !_isHandlingDialog) DismissPanel();
        };
    }

    #region Smart Content UI

    /// <summary>
    /// Build the smart content detection section with contextual actions.
    /// </summary>
    private UIElement BuildSmartContentSection()
    {
        var section = new StackPanel
        {
            Margin = new Thickness(0, 8, 0, 0)
        };

        // Separator line
        section.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(64, 64, 64)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        // --- QR/Barcode ---
        if (_detection.QrBarcodeContent != null)
        {
            section.Children.Add(CreateDetectedLabel("📱 QR/Barcode Detected"));
            var qrPanel = CreateDetectedItemPanel(_detection.QrBarcodeContent);
            if (Uri.TryCreate(_detection.QrBarcodeContent, UriKind.Absolute, out _))
            {
                qrPanel.Children.Add(CreateSmartButton("🔗 Open", () =>
                {
                    OpenUrl(_detection.QrBarcodeContent);
                    DismissPanel();
                }));
            }
            qrPanel.Children.Add(CreateSmartButton("📋 Copy", () =>
            {
                Clipboard.SetText(_detection.QrBarcodeContent);
                ShowFeedback("✅ QR content copied");
            }));
            section.Children.Add(qrPanel);
        }

        // --- URLs ---
        foreach (var url in _detection.Urls.Take(3))
        {
            section.Children.Add(CreateDetectedLabel("🔗 URL Detected"));
            var urlPanel = CreateDetectedItemPanel(url);
            urlPanel.Children.Add(CreateSmartButton("Open", () =>
            {
                OpenUrl(url);
                DismissPanel();
            }));
            urlPanel.Children.Add(CreateSmartButton("Copy", () =>
            {
                Clipboard.SetText(url);
                ShowFeedback("✅ URL copied");
            }));
            section.Children.Add(urlPanel);
        }

        // --- Emails ---
        foreach (var email in _detection.Emails.Take(3))
        {
            section.Children.Add(CreateDetectedLabel("📧 Email Detected"));
            var emailPanel = CreateDetectedItemPanel(email);
            emailPanel.Children.Add(CreateSmartButton("Send", () =>
            {
                OpenUrl($"mailto:{email}");
                DismissPanel();
            }));
            emailPanel.Children.Add(CreateSmartButton("Copy", () =>
            {
                Clipboard.SetText(email);
                ShowFeedback("✅ Email copied");
            }));
            section.Children.Add(emailPanel);
        }

        // --- Phone Numbers ---
        foreach (var phone in _detection.PhoneNumbers.Take(3))
        {
            section.Children.Add(CreateDetectedLabel("📞 Phone Number"));
            var phonePanel = CreateDetectedItemPanel(phone);
            phonePanel.Children.Add(CreateSmartButton("Copy", () =>
            {
                Clipboard.SetText(phone);
                ShowFeedback("✅ Phone number copied");
            }));
            section.Children.Add(phonePanel);
        }

        // --- Colors ---
        if (_detection.Colors.Count > 0)
        {
            section.Children.Add(CreateDetectedLabel("🎨 Color Detected"));
            foreach (var color in _detection.Colors.Take(5))
            {
                var colorPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 0, 4)
                };

                // Color swatch preview
                colorPanel.Children.Add(CreateColorSwatch(color));

                colorPanel.Children.Add(new TextBlock
                {
                    Text = color,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 8, 0)
                });

                colorPanel.Children.Add(CreateSmartButton("Copy", () =>
                {
                    Clipboard.SetText(color);
                    ShowFeedback("✅ Color copied");
                }));

                section.Children.Add(colorPanel);
            }
        }

        // --- Code ---
        if (_detection.IsCode)
        {
            section.Children.Add(CreateDetectedLabel("💻 Code Snippet Detected"));
        }

        return section;
    }

    private TextBlock CreateDetectedLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)), // Accent blue
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 2)
        };
    }

    private WrapPanel CreateDetectedItemPanel(string value)
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 4)
        };

        panel.Children.Add(new TextBlock
        {
            Text = value.Length > 60 ? value[..57] + "..." : value,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            MaxWidth = 260,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        return panel;
    }

    private Button CreateSmartButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = Brushes.White
            },
            Background = new SolidColorBrush(Color.FromRgb(0, 100, 180)),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 4, 0),
            Cursor = Cursors.Hand
        };

        btn.Click += (_, _) => onClick();

        btn.MouseEnter += (_, _) =>
            btn.Background = new SolidColorBrush(Color.FromRgb(0, 130, 220));
        btn.MouseLeave += (_, _) =>
            btn.Background = new SolidColorBrush(Color.FromRgb(0, 100, 180));

        return btn;
    }

    private Border CreateColorSwatch(string colorValue)
    {
        System.Windows.Media.Color swatchColor;
        try
        {
            if (colorValue.StartsWith('#'))
            {
                swatchColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString(colorValue);
            }
            else
            {
                swatchColor = Color.FromRgb(128, 128, 128); // Fallback for rgb()/hsl()
            }
        }
        catch
        {
            swatchColor = Color.FromRgb(128, 128, 128);
        }

        return new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(swatchColor),
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    #endregion

    #region Actions

    private void CopyText()
    {
        var text = _textBox.Text;
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
            ShowFeedback("✅ Copied to clipboard");
        }
    }

    private void SearchText()
    {
        var text = _textBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var url = _settingsService.GetSearchUrl(text);
        OpenUrl(url);
        DismissPanel();
    }

    private async void TranslateText()
    {
        var text = _textBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var targetLang = _settingsService.Current.Translation.DefaultTargetLanguage;

        // Try inline translation first using the built-in translation service
        try
        {
            ShowTranslationLoading(true);
            var result = await _translationService.TranslateAsync(
                text, targetLang, "auto", _settingsService.Current.Translation);

            if (result.Success && !string.IsNullOrWhiteSpace(result.TranslatedText))
            {
                ShowInlineTranslation(result.TranslatedText, result.DetectedSourceLanguage, targetLang);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Inline translation failed, falling back to browser");
        }
        finally
        {
            ShowTranslationLoading(false);
        }

        // Fallback: open Google Translate in browser
        var escapedText = Uri.EscapeDataString(text);
        var url = $"https://translate.google.com/?sl=auto&tl={targetLang}&text={escapedText}";
        OpenUrl(url);
        DismissPanel();
    }

    private TextBlock? _translationLoadingText;
    private Border? _translationSection;

    private void ShowTranslationLoading(bool show)
    {
        if (show)
        {
            _translationLoadingText = new TextBlock
            {
                Text = "🔄 Translating...",
                Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 4)
            };
            if (_mainBorder.Child is StackPanel stack)
                stack.Children.Add(_translationLoadingText);
        }
        else if (_translationLoadingText != null)
        {
            if (_mainBorder.Child is StackPanel stack)
                stack.Children.Remove(_translationLoadingText);
            _translationLoadingText = null;
        }
    }

    private void ShowInlineTranslation(string translatedText, string sourceLang, string targetLang)
    {
        if (_translationSection != null && _mainBorder.Child is StackPanel existingStack)
        {
            existingStack.Children.Remove(_translationSection);
        }

        // Get language display names
        var sourceName = GlanceSearch.Infrastructure.Translation.TranslationService.SupportedLanguages
            .TryGetValue(sourceLang, out var sn) ? sn : sourceLang;
        var targetName = GlanceSearch.Infrastructure.Translation.TranslationService.SupportedLanguages
            .TryGetValue(targetLang, out var tn) ? tn : targetLang;

        var section = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

        // Separator
        section.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(64, 64, 64)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Header with language info
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerText = new TextBlock
        {
            Text = $"🌐 Translation ({sourceName} → {targetName})",
            Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetColumn(headerText, 0);
        headerGrid.Children.Add(headerText);

        var copyTransBtn = CreateSmartButton("📋 Copy", () =>
        {
            Clipboard.SetText(translatedText);
            ShowFeedback("✅ Translation copied");
        });
        Grid.SetColumn(copyTransBtn, 1);
        headerGrid.Children.Add(copyTransBtn);
        section.Children.Add(headerGrid);

        // Translation text
        var translationBox = new TextBox
        {
            Text = translatedText,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            MaxHeight = 100,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 13,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6)
        };
        var translationBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = translationBox
        };
        section.Children.Add(translationBorder);

        _translationSection = new Border { Child = section };

        if (_mainBorder.Child is StackPanel stack)
        {
            // Insert before the button panel (last child)
            var insertIndex = stack.Children.Count > 0 ? stack.Children.Count - 1 : 0;
            stack.Children.Insert(insertIndex, _translationSection);
        }
    }

    private void ImageSearch()
    {
        if (_capturedImage != null)
        {
            try
            {
                // Copy image to clipboard for user to paste into Google Lens
                var wpfImage = ConvertBitmapToSource(_capturedImage);
                Clipboard.SetImage((BitmapSource)wpfImage);

                // Open Google Lens search-by-image page directly
                // This opens the "Search any image with Google Lens" page with drag/upload option
                OpenUrl("https://lens.google.com/");
                GlanceSearch.App.Theme.ToastService.Show("Image Copied", "Image copied to clipboard! Paste or upload it into Google Lens to search.", GlanceSearch.App.Theme.ToastType.Info);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Image search failed");
                GlanceSearch.App.Theme.ToastService.Show("Search Failed", "Could not copy image for search.", GlanceSearch.App.Theme.ToastType.Error);
            }
        }
        DismissPanel();
    }

    private void SaveImage()
    {
        if (_capturedImage != null)
        {
            try
            {
                _isHandlingDialog = true;
                
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG Image|*.png|JPEG Image|*.jpg",
                    Title = "Save Captured Image",
                    FileName = $"GlanceSearch_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (sfd.ShowDialog() == true)
                {
                    var format = sfd.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        ? ImageFormat.Jpeg
                        : ImageFormat.Png;

                    // Ensure minimum Full HD (1920x1080) resolution for saved images
                    var imageToSave = EnsureFullHDResolution(_capturedImage);
                    try
                    {
                        if (format == ImageFormat.Jpeg)
                        {
                            // Save JPEG with high quality (95)
                            var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                                .First(e => e.FormatID == ImageFormat.Jpeg.Guid);
                            var encoderParams = new EncoderParameters(1);
                            encoderParams.Param[0] = new EncoderParameter(
                                System.Drawing.Imaging.Encoder.Quality, 95L);
                            imageToSave.Save(sfd.FileName, jpegEncoder, encoderParams);
                        }
                        else
                        {
                            imageToSave.Save(sfd.FileName, format);
                        }
                    }
                    finally
                    {
                        if (imageToSave != _capturedImage)
                            imageToSave.Dispose();
                    }
                    GlanceSearch.App.Theme.ToastService.Show("Image Saved", "Image saved in high quality.", GlanceSearch.App.Theme.ToastType.Success);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save image");
                GlanceSearch.App.Theme.ToastService.Show("Save Failed", "Could not save the image.", GlanceSearch.App.Theme.ToastType.Error);
            }
            finally
            {
                _isHandlingDialog = false;
            }
        }
        DismissPanel();
    }

    /// <summary>
    /// Upscale image to at least Full HD resolution (1920x1080) while preserving aspect ratio.
    /// If the image is already larger, return it as-is.
    /// </summary>
    private Bitmap EnsureFullHDResolution(Bitmap source)
    {
        const int minWidth = 1920;
        const int minHeight = 1080;

        if (source.Width >= minWidth && source.Height >= minHeight)
            return source;

        // Calculate scale to reach at least Full HD on the smaller dimension
        double scaleX = (double)minWidth / source.Width;
        double scaleY = (double)minHeight / source.Height;
        double scale = Math.Max(scaleX, scaleY);
        // Don't downscale if already bigger on one axis
        if (scale < 1.0) scale = 1.0;
        // Cap upscaling to 4x to avoid excessively large images
        if (scale > 4.0) scale = 4.0;

        int newWidth = (int)(source.Width * scale);
        int newHeight = (int)(source.Height * scale);

        var upscaled = new Bitmap(newWidth, newHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        upscaled.SetResolution(96, 96);
        using (var g = System.Drawing.Graphics.FromImage(upscaled))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.DrawImage(source, 0, 0, newWidth, newHeight);
        }
        return upscaled;
    }

    private async void SaveToHistory()
    {
        try
        {
            await _historyService.SaveCaptureAsync(
                _capturedImage,
                _ocrResult,
                _detection,
                _selectionBounds);
            ShowFeedback("📌 Saved to history");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save to history");
            ShowFeedback("❌ Save failed");
        }
    }

    #endregion

    #region Keyboard

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DismissPanel();
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && !_textBox.IsFocused)
        {
            CopyText();
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchText();
            e.Handled = true;
        }
        else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TranslateText();
            e.Handled = true;
        }
        else if (e.Key == Key.I && Keyboard.Modifiers == ModifierKeys.Control && _capturedImage != null)
        {
            SaveImage();
            e.Handled = true;
        }
        else if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveToHistory();
            e.Handled = true;
        }
    }

    #endregion

    #region Helpers

    private Button CreateActionButton(string text, string? shortcut, Action onClick)
    {
        var T = ThemeService.Current;
        var sp = new StackPanel { Orientation = Orientation.Vertical };
        sp.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = T.TextBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        if (shortcut != null)
        {
            sp.Children.Add(new TextBlock
            {
                Text = shortcut,
                FontSize = 9,
                Foreground = new SolidColorBrush(T.TextMuted),
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }

        var btn = new Button
        {
            Content = sp,
            Background = new SolidColorBrush(T.SurfaceHover),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 6, 6),
            Cursor = Cursors.Hand,
            Foreground = T.TextBrush,
            IsTabStop = true
        };

        AutomationProperties.SetName(btn, text.Replace("📋", "").Replace("🔍", "").Replace("🌐", "").Replace("📷", "").Replace("📌", "").Trim());
        if (shortcut != null) AutomationProperties.SetHelpText(btn, $"Shortcut: {shortcut}");

        btn.Click += (_, _) => onClick();

        // Hover effect
        btn.MouseEnter += (_, _) =>
        {
            var hov = ThemeService.Current;
            btn.Background = hov.IsDarkMode
                ? new SolidColorBrush(Color.FromRgb(80, 80, 80))
                : new SolidColorBrush(Color.FromRgb(210, 215, 225));
        };
        btn.MouseLeave += (_, _) =>
            btn.Background = new SolidColorBrush(ThemeService.Current.SurfaceHover);

        return btn;
    }

    private void ShowFeedback(string message)
    {
        var type = message.Contains("❌") ? GlanceSearch.App.Theme.ToastType.Error : GlanceSearch.App.Theme.ToastType.Success;
        var strippedMessage = message.Replace("✅ ", "").Replace("❌ ", "").Replace("📷 ", "").Replace("📌 ", "");
        GlanceSearch.App.Theme.ToastService.Show("Action Successful", strippedMessage, type);

        if (_settingsService.Current.Capture.AutoDismissAfterAction)
            DismissPanel();
    }


    private void DismissPanel()
    {
        if (_isDismissing) return;
        _isDismissing = true;

        var fadeAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(Constants.PanelDismissMs));
        fadeAnim.Completed += (_, _) => Close();

        if (_mainBorder.RenderTransform is ScaleTransform st)
        {
            var scaleXAnim = new DoubleAnimation(1, 0.95, TimeSpan.FromMilliseconds(Constants.PanelDismissMs));
            var scaleYAnim = new DoubleAnimation(1, 0.95, TimeSpan.FromMilliseconds(Constants.PanelDismissMs));
            st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
        }
        BeginAnimation(OpacityProperty, fadeAnim);
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open URL: {Url}", url);
        }
    }

    private BitmapSource ConvertBitmapToSource(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = ms;
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        return bitmapImage;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // Dispose the captured bitmap to prevent GDI handle leaks
        _capturedImage?.Dispose();
    }

    #endregion
}
