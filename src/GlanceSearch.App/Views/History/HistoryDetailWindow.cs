using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlanceSearch.App.Theme;
using GlanceSearch.Infrastructure.Persistence;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using FontFamily = System.Windows.Media.FontFamily;
using TextBox = System.Windows.Controls.TextBox;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;

namespace GlanceSearch.App.Views.History;

/// <summary>
/// Full-detail view for a single history capture.
/// Shows the full image, complete extracted text, and action buttons.
/// </summary>
public class HistoryDetailWindow : Window
{
    private readonly CaptureHistoryEntity _item;
    private readonly HistoryService _historyService;
    private readonly Action? _onChanged;

    public HistoryDetailWindow(CaptureHistoryEntity item, HistoryService historyService, Action? onChanged = null)
    {
        _item = item;
        _historyService = historyService;
        _onChanged = onChanged;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        var T = ThemeService.Current;
        var timeStr = FormatTime(_item.CreatedAt);

        Title = $"📜 {timeStr}";
        Width = 740;
        Height = 540;
        MinWidth = 480;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = T.BackgroundBrush;
        Foreground = T.TextBrush;
        ResizeMode = ResizeMode.CanResize;

        var root = new DockPanel { LastChildFill = true };

        // ── Header bar ──────────────────────────────────────────────────────
        var headerBg = T.IsDarkMode ? Color.FromRgb(35, 35, 35) : Color.FromRgb(225, 228, 234);
        var header = new Border
        {
            Background = new SolidColorBrush(headerBg),
            BorderBrush = T.BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 16, 10),
        };

        var headerStack = new StackPanel();
        headerStack.Children.Add(new TextBlock
        {
            Text = timeStr,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = T.TextBrush,
        });

        var metaParts = new List<string>();
        if (!string.IsNullOrEmpty(_item.SourceWindowTitle))
            metaParts.Add($"📌 {_item.SourceWindowTitle}");
        if (!string.IsNullOrEmpty(_item.OcrEngine))
        {
            var conf = _item.OcrConfidence > 0 ? $"  ({_item.OcrConfidence:P0} confidence)" : "";
            metaParts.Add($"🔍 {_item.OcrEngine} OCR{conf}");
        }
        if (_item.SelectionWidth > 0 && _item.SelectionHeight > 0)
            metaParts.Add($"📐 {_item.SelectionWidth}×{_item.SelectionHeight} px");

        if (metaParts.Count > 0)
        {
            headerStack.Children.Add(new TextBlock
            {
                Text = string.Join("   •   ", metaParts),
                FontSize = 11,
                Foreground = new SolidColorBrush(T.TextMuted),
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        header.Child = headerStack;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Bottom action bar ────────────────────────────────────────────────
        var actionBar = new Border
        {
            Background = new SolidColorBrush(headerBg),
            BorderBrush = T.BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 8, 12, 8),
        };

        var actionDock = new DockPanel { LastChildFill = false };

        // Close docked to the right
        var closeBtn = MakeBtn("✕  Close", T.Surface, T.TextColor);
        closeBtn.Click += (_, _) => Close();
        DockPanel.SetDock(closeBtn, Dock.Right);
        actionDock.Children.Add(closeBtn);

        // Left-side action buttons
        var leftBtns = new StackPanel { Orientation = Orientation.Horizontal };

        if (!string.IsNullOrEmpty(_item.ExtractedText))
        {
            var copyTextBtn = MakeBtn("📋  Copy Text", T.Accent, Colors.White);
            copyTextBtn.ToolTip = "Copy all extracted text to clipboard";
            copyTextBtn.Click += (_, _) =>
            {
                Clipboard.SetText(_item.ExtractedText!);
                ToastService.Show("Copied", "Text copied to clipboard.", ToastType.Success);
            };
            leftBtns.Children.Add(copyTextBtn);
        }

        var hasImage = !string.IsNullOrEmpty(_item.SelectionImagePath) && File.Exists(_item.SelectionImagePath);
        if (hasImage)
        {
            var copyImgBtn = MakeBtn("🖼  Copy Image", T.Surface, T.TextColor);
            copyImgBtn.ToolTip = "Copy the captured image to clipboard";
            copyImgBtn.Click += (_, _) =>
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(_item.SelectionImagePath);
                    bmp.EndInit();
                    bmp.Freeze();
                    Clipboard.SetImage(bmp);
                    ToastService.Show("Copied", "Image copied to clipboard.", ToastType.Success);
                }
                catch { /* ignore */ }
            };
            leftBtns.Children.Add(copyImgBtn);
        }

        var pinLabel = _item.IsPinned ? "📌  Unpin" : "📌  Pin";
        var pinBtn = MakeBtn(pinLabel, T.Surface, T.TextColor);
        pinBtn.Click += async (_, _) =>
        {
            await _historyService.TogglePinAsync(_item.Id);
            _onChanged?.Invoke();
            Close();
        };
        leftBtns.Children.Add(pinBtn);

        var deleteBtn = MakeBtn("🗑  Delete", Color.FromRgb(190, 45, 45), Colors.White);
        deleteBtn.Click += async (_, _) =>
        {
            var r = MessageBox.Show(
                "Delete this capture from history?", "Delete Capture",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
            {
                await _historyService.DeleteAsync(_item.Id);
                _onChanged?.Invoke();
                Close();
            }
        };
        leftBtns.Children.Add(deleteBtn);

        DockPanel.SetDock(leftBtns, Dock.Left);
        actionDock.Children.Add(leftBtns);

        actionBar.Child = actionDock;
        DockPanel.SetDock(actionBar, Dock.Bottom);
        root.Children.Add(actionBar);

        // ── Main content area ────────────────────────────────────────────────
        // Grid: left = image (if exists), right = text
        var contentGrid = new Grid();

        if (hasImage)
        {
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 180 });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) }); // splitter
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star), MinWidth = 200 });

            // Image pane
            var imgScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(8),
            };

            try
            {
                var bmpImage = new BitmapImage();
                bmpImage.BeginInit();
                bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                bmpImage.UriSource = new Uri(_item.SelectionImagePath!);
                bmpImage.EndInit();
                bmpImage.Freeze();

                var imgCtrl = new Image
                {
                    Source = bmpImage,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                };
                imgScroll.Content = imgCtrl;
            }
            catch
            {
                imgScroll.Content = new TextBlock
                {
                    Text = "⚠ Image could not be loaded.",
                    Foreground = new SolidColorBrush(T.TextMuted),
                    Margin = new Thickness(12),
                    FontSize = 12,
                };
            }

            Grid.SetColumn(imgScroll, 0);
            contentGrid.Children.Add(imgScroll);

            // Divider
            var divider = new GridSplitter
            {
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = T.BorderBrush,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            };
            Grid.SetColumn(divider, 1);
            contentGrid.Children.Add(divider);

            // Text pane column
            Grid.SetColumn(BuildTextPane(T), 2);
            contentGrid.Children.Add(BuildTextPane(T));
        }
        else
        {
            // No image — full-width text pane
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var tp = BuildTextPane(T);
            Grid.SetColumn(tp, 0);
            contentGrid.Children.Add(tp);
        }

        root.Children.Add(contentGrid);
        Content = root;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private FrameworkElement BuildTextPane(ThemeService T)
    {
        var hasText = !string.IsNullOrWhiteSpace(_item.ExtractedText);

        var textBox = new TextBox
        {
            Text = hasText ? _item.ExtractedText : "(No text was extracted from this capture.)",
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 13,
            FontFamily = new FontFamily("Segoe UI"),
            Background = T.SurfaceBrush,
            Foreground = hasText ? T.TextBrush : new SolidColorBrush(T.TextMuted),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14),
            SelectionBrush = new SolidColorBrush(T.Accent),
        };

        return new Border
        {
            Background = T.SurfaceBrush,
            BorderBrush = T.BorderBrush,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0),
            Child = textBox,
        };
    }

    private static Button MakeBtn(string text, Color bg, Color fg)
    {
        var hover = Color.FromArgb(255,
            (byte)Math.Min(255, bg.R + 18),
            (byte)Math.Min(255, bg.G + 18),
            (byte)Math.Min(255, bg.B + 18));

        var btn = new Button
        {
            Content = new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(fg) },
            Background = new SolidColorBrush(bg),
            Foreground = new SolidColorBrush(fg),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 6, 0),
            Cursor = Cursors.Hand,
        };
        btn.MouseEnter += (_, _) => btn.Background = new SolidColorBrush(hover);
        btn.MouseLeave += (_, _) => btn.Background = new SolidColorBrush(bg);
        return btn;
    }

    private static string FormatTime(DateTime utc)
    {
        var local = utc.ToLocalTime();
        var diff = DateTime.Now - local;
        var dateStr = diff.TotalDays < 1 ? "Today" :
                      diff.TotalDays < 2 ? "Yesterday" :
                      diff.TotalDays < 7 ? $"{(int)diff.TotalDays} days ago" :
                      local.ToString("MMM d, yyyy");
        return $"{dateStr} at {local:h:mm tt}";
    }
}
