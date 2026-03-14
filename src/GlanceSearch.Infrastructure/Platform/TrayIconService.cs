using System.Windows.Forms;
using GlanceSearch.Shared;

namespace GlanceSearch.Infrastructure.Platform;

/// <summary>
/// Manages the system tray (notification area) icon and context menu.
/// </summary>
public class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private bool _disposed;

    public event EventHandler? CaptureRequested;
    public event EventHandler? HistoryRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? SupportRequested;
    public event EventHandler? QuitRequested;

    /// <summary>
    /// Initialize the system tray icon and context menu.
    /// </summary>
    public void Initialize(System.Drawing.Icon? appIcon = null)
    {
        _notifyIcon = new NotifyIcon
        {
            Text = $"{Constants.AppName} — {Constants.AppTagline}",
            Visible = true,
            Icon = appIcon ?? CreateDefaultIcon()
        };

        _notifyIcon.MouseClick += OnTrayIconClick;
        _notifyIcon.ContextMenuStrip = CreateContextMenu();
    }

    /// <summary>
    /// Create the tray icon context menu.
    /// </summary>
    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var captureItem = new ToolStripMenuItem("🔍 Capture Screen", null, (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty))
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+G",
            Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
        };
        menu.Items.Add(captureItem);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("📜 History", null, (_, _) => HistoryRequested?.Invoke(this, EventArgs.Empty))
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+H"
        });

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("⚙️ Settings", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("ℹ️ About GlanceSearch", null, (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("☕ Buy Me a Coffee", null, (_, _) => SupportRequested?.Invoke(this, EventArgs.Empty)));

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("❌ Quit", null, (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty)));

        return menu;
    }

    /// <summary>
    /// Handle tray icon clicks.
    /// Left-click: activate capture.
    /// </summary>
    private void OnTrayIconClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            CaptureRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Show a toast notification near the tray.
    /// </summary>
    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
    {
        _notifyIcon?.ShowBalloonTip(timeout, title, message, icon);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <summary>
    /// Creates a fallback tray icon (a blue circle with an initial).
    /// </summary>
    private System.Drawing.Icon CreateDefaultIcon()
    {
        var bmp = new System.Drawing.Bitmap(32, 32);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 212));
        g.FillEllipse(brush, 1, 1, 30, 30);

        using var font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
        var sf = new System.Drawing.StringFormat
        {
            Alignment = System.Drawing.StringAlignment.Center,
            LineAlignment = System.Drawing.StringAlignment.Center
        };
        g.DrawString("G", font, textBrush, new System.Drawing.RectangleF(0, 0, 32, 32), sf);

        var hIcon = bmp.GetHicon();
        var icon = System.Drawing.Icon.FromHandle(hIcon);
        var clonedIcon = (System.Drawing.Icon)icon.Clone();
        DestroyIcon(hIcon); // Free the native HICON handle to prevent GDI leak
        return clonedIcon;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
