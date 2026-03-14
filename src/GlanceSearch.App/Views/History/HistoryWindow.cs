using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlanceSearch.App.Theme;
using GlanceSearch.Infrastructure.Persistence;
using GlanceSearch.Shared;
using Serilog;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using MessageBox = System.Windows.MessageBox;
using Image = System.Windows.Controls.Image;

namespace GlanceSearch.App.Views.History;

/// <summary>
/// History window showing past captures in a searchable grid.
/// </summary>
public class HistoryWindow : Window
{
    private readonly HistoryService _historyService;
    private WrapPanel _itemsPanel = null!;
    private TextBox _searchBox = null!;
    private TextBlock _statusLabel = null!;
    private bool _showPinnedOnly = false;

    public HistoryWindow(HistoryService historyService)
    {
        _historyService = historyService;
        InitializeComponent();
        _ = LoadHistoryAsync();
    }

    private void InitializeComponent()
    {
        var T = ThemeService.Current;
        Title = "📜 GlanceSearch History";
        Width = 800;
        Height = 550;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = T.BackgroundBrush;
        Foreground = T.TextBrush;

        var mainStack = new DockPanel();

        // --- Top toolbar ---
        var toolbar = new Grid
        {
            Margin = new Thickness(16, 12, 16, 8)
        };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        DockPanel.SetDock(toolbar, Dock.Top);

        // Search box
        _searchBox = new TextBox
        {
            FontSize = 13,
            Padding = new Thickness(8, 6, 8, 6),
            Background = T.SurfaceBrush,
            Foreground = T.TextBrush,
            BorderBrush = T.BorderBrush,
            BorderThickness = new Thickness(1),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        };
        // Placeholder text via adorner (simplified: just handle GotFocus/LostFocus)
        _searchBox.Tag = "🔍 Search history...";
        _searchBox.Text = "🔍 Search history...";
        _searchBox.Foreground = new SolidColorBrush(T.TextMuted);
        _searchBox.GotFocus += (_, _) =>
        {
            if (_searchBox.Text == (string)_searchBox.Tag)
            {
                _searchBox.Text = "";
                _searchBox.Foreground = new SolidColorBrush(T.TextColor);
            }
        };
        _searchBox.LostFocus += (_, _) =>
        {
            if (string.IsNullOrEmpty(_searchBox.Text))
            {
                _searchBox.Text = (string)_searchBox.Tag;
                _searchBox.Foreground = new SolidColorBrush(T.TextMuted);
            }
        };
        _searchBox.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter) await SearchAsync();
        };
        Grid.SetColumn(_searchBox, 0);
        toolbar.Children.Add(_searchBox);

        // Pinned filter toggle
        var pinnedBtn = CreateThemedButton("📌  Pinned Only", T.SurfaceHover, T.TextColor, T.Accent);
        pinnedBtn.Margin = new Thickness(8, 0, 0, 0);
        pinnedBtn.VerticalAlignment = System.Windows.VerticalAlignment.Center;
        pinnedBtn.Click += async (_, _) =>
        {
            _showPinnedOnly = !_showPinnedOnly;
            // Update button appearance via Tag flag
            pinnedBtn.Tag = _showPinnedOnly;
            UpdatePinnedBtnStyle(pinnedBtn);
            await LoadHistoryAsync();
        };
        Grid.SetColumn(pinnedBtn, 1);
        toolbar.Children.Add(pinnedBtn);

        // Clear all button
        var clearBtn = CreateThemedButton("🗑  Clear All", T.SurfaceHover, T.TextColor, Color.FromRgb(210, 55, 55));
        clearBtn.Margin = new Thickness(4, 0, 0, 0);
        clearBtn.VerticalAlignment = System.Windows.VerticalAlignment.Center;
        clearBtn.Click += async (_, _) =>
        {
            var result = MessageBox.Show(
                "Delete all history items?",
                "Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _historyService.ClearAllAsync();
                await LoadHistoryAsync();
            }
        };
        Grid.SetColumn(clearBtn, 2);
        toolbar.Children.Add(clearBtn);

        mainStack.Children.Add(toolbar);

        // --- Status bar ---
        _statusLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(T.TextMuted),
            Margin = new Thickness(16, 0, 16, 8),
        };
        DockPanel.SetDock(_statusLabel, Dock.Bottom);
        mainStack.Children.Add(_statusLabel);

        // --- Scrollable items panel ---
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(8, 0, 8, 0)
        };

        _itemsPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
        };

        scrollViewer.Content = _itemsPanel;
        mainStack.Children.Add(scrollViewer);

        Content = mainStack;

        // Keyboard shortcuts
        KeyDown += OnKeyDown;
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var items = _showPinnedOnly
                ? await _historyService.GetPinnedAsync()
                : await _historyService.GetRecentAsync(200);

            _itemsPanel.Children.Clear();

            if (items.Count == 0)
            {
                _itemsPanel.Children.Add(new TextBlock
                {
                    Text = _showPinnedOnly
                        ? "No pinned items."
                        : "No history yet. Capture something to get started!",
                    Foreground = new SolidColorBrush(ThemeService.Current.TextMuted),
                    FontSize = 14,
                    Margin = new Thickness(16, 32, 16, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }
            else
            {
                foreach (var item in items)
                    _itemsPanel.Children.Add(CreateHistoryCard(item));
            }

            // Update status
            var totalCount = await _historyService.GetCountAsync();
            var storageBytes = _historyService.GetStorageSize();
            var storageMb = storageBytes / (1024.0 * 1024.0);
            _statusLabel.Text = $"Showing {items.Count} items  •  {totalCount} total  •  Using {storageMb:F1} MB";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load history");
        }
    }

    private async Task SearchAsync()
    {
        var query = _searchBox.Text;
        if (query == (string)_searchBox.Tag) query = "";

        try
        {
            var items = string.IsNullOrWhiteSpace(query)
                ? await _historyService.GetRecentAsync(200)
                : await _historyService.SearchAsync(query);

            _itemsPanel.Children.Clear();
            foreach (var item in items)
                _itemsPanel.Children.Add(CreateHistoryCard(item));

            _statusLabel.Text = string.IsNullOrWhiteSpace(query)
                ? $"Showing {items.Count} items"
                : $"Found {items.Count} items for \"{query}\"";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "History search failed");
        }
    }

    private Border CreateHistoryCard(CaptureHistoryEntity item)
    {
        var T = ThemeService.Current;
        var card = new Border
        {
            Width = 170,
            Height = 190,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(T.Surface),
            BorderBrush = new SolidColorBrush(T.Border),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4),
            Padding = new Thickness(8),
            Cursor = Cursors.Hand,
        };

        var stack = new StackPanel();

        // Thumbnail
        if (!string.IsNullOrEmpty(item.ThumbnailPath) && File.Exists(item.ThumbnailPath))
        {
            try
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.UriSource = new Uri(item.ThumbnailPath, UriKind.Absolute);
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                var img = new Image
                {
                    Source = bitmapImage,
                    MaxHeight = 80,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var imgBorder = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    ClipToBounds = true,
                    Child = img,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stack.Children.Add(imgBorder);
            }
            catch { /* skip broken image */ }
        }

        // Text snippet
        var textSnippet = item.ExtractedText ?? "";
        if (textSnippet.Length > 60) textSnippet = textSnippet[..57] + "...";

        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(textSnippet)
                ? "(No text)"
                : $"\"{textSnippet}\"",
            Foreground = new SolidColorBrush(T.TextColor),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 40,
        });

        // Timestamp
        var timeAgo = GetTimeAgo(item.CreatedAt);
        stack.Children.Add(new TextBlock
        {
            Text = timeAgo,
            Foreground = new SolidColorBrush(T.TextMuted),
            FontSize = 10,
            Margin = new Thickness(0, 2, 0, 0)
        });

        // Pin indicator
        if (item.IsPinned)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "📌 Pinned",
                Foreground = new SolidColorBrush(T.Accent),
                FontSize = 10,
            });
        }

        card.Child = stack;

        // Hover effect
        card.MouseEnter += (_, _) =>
            card.Background = new SolidColorBrush(ThemeService.Current.SurfaceHover);
        card.MouseLeave += (_, _) =>
            card.Background = new SolidColorBrush(ThemeService.Current.Surface);

        // Right-click context menu
        var contextMenu = new ContextMenu
        {
            Background = new SolidColorBrush(ThemeService.Current.Surface),
            Foreground = new SolidColorBrush(ThemeService.Current.TextColor),
        };

        var copyItem = new MenuItem { Header = "📋 Copy Text" };
        copyItem.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(item.ExtractedText))
                Clipboard.SetText(item.ExtractedText);
        };

        var pinItem = new MenuItem
        {
            Header = item.IsPinned ? "📌 Unpin" : "📌 Pin"
        };
        pinItem.Click += async (_, _) =>
        {
            await _historyService.TogglePinAsync(item.Id);
            await LoadHistoryAsync();
        };

        var deleteItem = new MenuItem { Header = "🗑 Delete" };
        deleteItem.Click += async (_, _) =>
        {
            await _historyService.DeleteAsync(item.Id);
            await LoadHistoryAsync();
        };

        contextMenu.Items.Add(copyItem);
        contextMenu.Items.Add(pinItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(deleteItem);
        card.ContextMenu = contextMenu;

        // Click to open full detail view
        card.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 1 && e.ChangedButton == MouseButton.Left)
            {
                var detail = new HistoryDetailWindow(item, _historyService, async () => await LoadHistoryAsync());
                detail.Owner = this;
                detail.ShowDialog();
                e.Handled = true;
            }
        };

        return card;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _searchBox.Focus();
            if (_searchBox.Text == (string)_searchBox.Tag)
            {
                _searchBox.Text = "";
                _searchBox.Foreground = new SolidColorBrush(ThemeService.Current.TextColor);
            }
            e.Handled = true;
        }
    }

    private static string GetTimeAgo(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;
        if (elapsed.TotalMinutes < 1) return "Just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours} hr ago";
        if (elapsed.TotalDays < 2) return "Yesterday";
        if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays} days ago";
        return utcTime.ToLocalTime().ToString("MMM d, yyyy");
    }

    // ─── Themed Button helper ─────────────────────────────────────────────────
    /// <summary>
    /// Builds a small toolbar button with a proper ControlTemplate so hover works
    /// in both light and dark mode without WPF Aero overriding background.
    /// </summary>
    private static Button CreateThemedButton(string text, Color bg, Color fg, Color hoverBg)
    {
        var borderFef = new FrameworkElementFactory(typeof(Border));
        borderFef.Name = "bd";
        borderFef.SetValue(Border.BackgroundProperty, new SolidColorBrush(bg));
        borderFef.SetValue(Border.BorderBrushProperty,
            new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)));
        borderFef.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFef.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFef.SetValue(Border.SnapsToDevicePixelsProperty, true);

        var cpFef = new FrameworkElementFactory(typeof(ContentPresenter));
        cpFef.SetValue(ContentPresenter.MarginProperty, new Thickness(10, 5, 10, 5));
        cpFef.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cpFef.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFef.AppendChild(cpFef);

        var template = new ControlTemplate(typeof(Button));
        template.VisualTree = borderFef;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush(hoverBg), "bd"));
        template.Triggers.Add(hoverTrigger);

        var pressedTrigger = new Trigger {
            Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb(200, hoverBg.R, hoverBg.G, hoverBg.B)), "bd"));
        template.Triggers.Add(pressedTrigger);

        return new Button
        {
            Template = template,
            Content = new TextBlock { Text = text, FontSize = 12,
                Foreground = new SolidColorBrush(fg) },
            Cursor = Cursors.Hand,
            Tag = false, // used as active-state flag for pinned button
        };
    }

    private static void UpdatePinnedBtnStyle(Button btn)
    {
        var isPinned = btn.Tag is true;
        var T = ThemeService.Current;
        var bg = isPinned ? T.Accent : T.SurfaceHover;
        var fg = isPinned ? Colors.White : T.TextColor;
        if (btn.Template.FindName("bd", btn) is Border bd)
            bd.Background = new SolidColorBrush(bg);
        if (btn.Content is TextBlock tb)
            tb.Foreground = new SolidColorBrush(fg);
    }
}
