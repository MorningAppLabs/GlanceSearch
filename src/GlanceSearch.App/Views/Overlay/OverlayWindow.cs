using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using GlanceSearch.Core.OCR;
using GlanceSearch.Core.Selection;
using GlanceSearch.Shared;
using GlanceSearch.Shared.Models;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using SelectionMode = GlanceSearch.Shared.SelectionMode;

namespace GlanceSearch.App.Views.Overlay;

/// <summary>
/// Fullscreen overlay window for screen capture and selection.
/// Displays the captured screen with a dark tint overlay.
/// Supports freehand and rectangle selection modes.
/// </summary>
public partial class OverlayWindow : Window
{
    // Services
    private readonly SelectionService _selectionService;
    private readonly OcrService _ocrService;
    private readonly double _overlayOpacity;

    // Capture state
    private Bitmap? _screenCapture;
    private BitmapSource? _screenImage;

    // Selection state
    private bool _isSelecting;
    private System.Windows.Point _selectionStart;
    private System.Windows.Point _selectionEnd;
    private List<System.Windows.Point> _freehandPoints = [];
    private SelectionMode _selectionMode = SelectionMode.Rectangle;

    // Results callback
    public event EventHandler<CaptureResult>? CaptureCompleted;

    public OverlayWindow(Bitmap screenCapture, SelectionService selectionService, OcrService ocrService, SelectionMode defaultMode, double overlayOpacity = 0.4)
    {
        _screenCapture = screenCapture;
        _selectionService = selectionService;
        _ocrService = ocrService;
        _selectionMode = defaultMode;
        _overlayOpacity = Math.Clamp(overlayOpacity, 0.1, 0.9);

        InitializeComponent();
        SetupWindow();
        SetScreenImage();
    }

    private void InitializeComponent()
    {
        // Programmatic WPF window setup (no XAML needed for this dynamic window)
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Cursor = Cursors.Cross;

        // Main grid
        var grid = new Grid();
        Content = grid;

        // Background image
        _backgroundImage = new System.Windows.Controls.Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        grid.Children.Add(_backgroundImage);

        // Dark overlay
        _overlayRect = new System.Windows.Shapes.Rectangle
        {
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(_overlayOpacity * 255), 0, 0, 0)),
            Opacity = 0
        };
        grid.Children.Add(_overlayRect);

        // Canvas for selection drawing
        _selectionCanvas = new Canvas
        {
            Background = System.Windows.Media.Brushes.Transparent
        };
        grid.Children.Add(_selectionCanvas);

        // Mode indicator
        _modeIndicator = new TextBlock
        {
            Text = GetModeText(),
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            Padding = new Thickness(12, 6, 12, 6),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 45, 45, 45)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(20, 0, 0, 20)
        };
        grid.Children.Add(_modeIndicator);

        // Hint text
        _hintText = new TextBlock
        {
            Text = "Press Tab to toggle mode · Escape to cancel",
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 255, 255, 255)),
            FontSize = 12,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 20, 24)
        };
        grid.Children.Add(_hintText);

        // Events
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        KeyDown += OnKeyDown;
    }

    private System.Windows.Controls.Image _backgroundImage = null!;
    private System.Windows.Shapes.Rectangle _overlayRect = null!;
    private Canvas _selectionCanvas = null!;
    private TextBlock _modeIndicator = null!;
    private TextBlock _hintText = null!;

    private void SetupWindow()
    {
        // Span all monitors
        var virtualScreen = new System.Drawing.Rectangle(
            (int)SystemParameters.VirtualScreenLeft,
            (int)SystemParameters.VirtualScreenTop,
            (int)SystemParameters.VirtualScreenWidth,
            (int)SystemParameters.VirtualScreenHeight
        );

        Left = virtualScreen.X;
        Top = virtualScreen.Y;
        Width = virtualScreen.Width;
        Height = virtualScreen.Height;
    }

    private void SetScreenImage()
    {
        if (_screenCapture == null) return;

        _screenImage = ConvertBitmapToSource(_screenCapture);
        _backgroundImage.Source = _screenImage;

        // Fade in overlay
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(Constants.OverlayFadeInMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        _overlayRect.BeginAnimation(OpacityProperty, fadeIn);
    }

    #region Mouse handlers

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isSelecting = true;
        _selectionStart = e.GetPosition(this);
        _freehandPoints.Clear();
        _freehandPoints.Add(_selectionStart);
        _selectionCanvas.Children.Clear();
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isSelecting) return;

        var currentPos = e.GetPosition(this);

        if (_selectionMode == SelectionMode.Rectangle)
        {
            _selectionEnd = currentPos;
            DrawRectangleSelection();
        }
        else
        {
            _freehandPoints.Add(currentPos);
            DrawFreehandSelection();
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;
        _isSelecting = false;
        ReleaseMouseCapture();

        _selectionEnd = e.GetPosition(this);

        // Get bounding box
        System.Drawing.Rectangle bounds;
        if (_selectionMode == SelectionMode.Rectangle)
        {
            bounds = GetRectangleBounds();
        }
        else
        {
            var points = _freehandPoints.Select(p => new System.Drawing.Point((int)p.X, (int)p.Y)).ToList();
            bounds = _selectionService.GetBoundingBox(points);
        }

        // Validate minimum size
        if (!_selectionService.IsValidSelection(bounds))
        {
            // Selection too small — flash red feedback
            ShowTooSmallFeedback();
            return;
        }

        // Crop and process
        ProcessSelection(bounds);
    }

    #endregion

    #region Drawing

    private void DrawRectangleSelection()
    {
        _selectionCanvas.Children.Clear();

        var rect = GetSelectionRect();
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // Clear area (show through overlay)
        var selectionRect = new System.Windows.Shapes.Rectangle
        {
            Width = rect.Width,
            Height = rect.Height,
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Fill = System.Windows.Media.Brushes.Transparent
        };

        Canvas.SetLeft(selectionRect, rect.X);
        Canvas.SetTop(selectionRect, rect.Y);
        _selectionCanvas.Children.Add(selectionRect);

        // Size indicator
        var sizeText = new TextBlock
        {
            Text = $"{(int)rect.Width} × {(int)rect.Height}",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 11,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
            Padding = new Thickness(4, 2, 4, 2)
        };
        Canvas.SetLeft(sizeText, rect.X);
        Canvas.SetTop(sizeText, rect.Y + rect.Height + 4);
        _selectionCanvas.Children.Add(sizeText);
    }

    private void DrawFreehandSelection()
    {
        if (_freehandPoints.Count < 2) return;

        _selectionCanvas.Children.Clear();

        var polyline = new System.Windows.Shapes.Polyline
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Fill = System.Windows.Media.Brushes.Transparent,
            Points = new PointCollection(_freehandPoints)
        };

        _selectionCanvas.Children.Add(polyline);
    }

    #endregion

    #region Keyboard

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                DismissOverlay();
                break;

            case Key.Tab:
                ToggleSelectionMode();
                e.Handled = true;
                break;

            case Key.A when Keyboard.Modifiers == ModifierKeys.Control:
                SelectFullScreen();
                e.Handled = true;
                break;
        }
    }

    private void ToggleSelectionMode()
    {
        _selectionMode = _selectionMode == SelectionMode.Rectangle
            ? SelectionMode.Freehand
            : SelectionMode.Rectangle;

        _modeIndicator.Text = GetModeText();
    }

    private string GetModeText() => _selectionMode == SelectionMode.Rectangle
        ? "▪ Rectangle Mode (Tab to switch)"
        : "◯ Freehand Mode (Tab to switch)";

    private void SelectFullScreen()
    {
        var bounds = new System.Drawing.Rectangle(0, 0,
            (int)SystemParameters.VirtualScreenWidth,
            (int)SystemParameters.VirtualScreenHeight);
        ProcessSelection(bounds);
    }

    #endregion

    #region Processing

    private async void ProcessSelection(System.Drawing.Rectangle bounds)
    {
        if (_screenCapture == null) return;

        Bitmap? cropped = null;
        GlanceSearch.Shared.Models.OcrResult ocrResult;

        try
        {
            // Crop selection
            cropped = _selectionService.CropSelection(_screenCapture, bounds);

            // Run OCR
            ocrResult = await _ocrService.RecognizeAsync(cropped);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCR failed: {ex.Message}");
            // OCR failed — still show panel with image but empty text
            ocrResult = new GlanceSearch.Shared.Models.OcrResult
            {
                ExtractedText = "",
                Confidence = 0,
                EngineUsed = $"Error: {ex.Message}"
            };
        }

        // Close overlay first, THEN fire completion event
        // This prevents focus-shift issues with the action panel
        var captureResult = new CaptureResult
        {
            CroppedImage = cropped,
            OcrResult = ocrResult,
            SelectionBounds = bounds,
            ScreenPosition = new System.Drawing.Point((int)_selectionStart.X, (int)_selectionStart.Y),
            Mode = _selectionMode
        };

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(Constants.OverlayFadeOutMs));
        fadeOut.Completed += (_, _) =>
        {
            // Close overlay first
            Close();

            // Then show action panel (after overlay is gone)
            Dispatcher.BeginInvoke(() =>
            {
                CaptureCompleted?.Invoke(this, captureResult);
            }, System.Windows.Threading.DispatcherPriority.Background);
        };

        _overlayRect.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ShowTooSmallFeedback()
    {
        // Brief flash feedback for too-small selection
        var tooltip = new TextBlock
        {
            Text = "Selection too small. Try selecting a larger area.",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 196, 43, 28)),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ((Grid)Content).Children.Add(tooltip);

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        timer.Tick += (_, _) =>
        {
            ((Grid)Content).Children.Remove(tooltip);
            timer.Stop();
        };
        timer.Start();
    }

    private void DismissOverlay()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(Constants.OverlayFadeOutMs));
        fadeOut.Completed += (_, _) => Close();
        _overlayRect.BeginAnimation(OpacityProperty, fadeOut);
    }

    #endregion

    #region Helpers

    private Windows.Foundation.Rect GetSelectionRect()
    {
        double x = Math.Min(_selectionStart.X, _selectionEnd.X);
        double y = Math.Min(_selectionStart.Y, _selectionEnd.Y);
        double w = Math.Abs(_selectionEnd.X - _selectionStart.X);
        double h = Math.Abs(_selectionEnd.Y - _selectionStart.Y);
        return new Windows.Foundation.Rect(x, y, w, h);
    }

    private System.Drawing.Rectangle GetRectangleBounds()
    {
        var rect = GetSelectionRect();
        return new System.Drawing.Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
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

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _screenCapture?.Dispose();
        _screenCapture = null;
    }
}

/// <summary>
/// Result of a capture operation.
/// </summary>
public class CaptureResult
{
    public Bitmap? CroppedImage { get; set; }
    public OcrResult OcrResult { get; set; } = new();
    public System.Drawing.Rectangle SelectionBounds { get; set; }
    public System.Drawing.Point ScreenPosition { get; set; }
    public SelectionMode Mode { get; set; }
    public ContentDetectionResult ContentDetection { get; set; } = new();
}

