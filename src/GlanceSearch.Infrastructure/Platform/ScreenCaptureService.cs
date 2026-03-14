using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GlanceSearch.Infrastructure.Platform;

/// <summary>
/// Captures the screen using GDI+ BitBlt.
/// Supports multi-monitor setups with per-monitor DPI awareness.
/// </summary>
public class ScreenCaptureService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SRCCOPY = 0x00CC0020;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>
    /// Capture the entire virtual screen (all monitors).
    /// </summary>
    public Bitmap CaptureFullScreen()
    {
        int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        return CaptureRegion(x, y, width, height);
    }

    /// <summary>
    /// Capture a specific region of the screen.
    /// </summary>
    public Bitmap CaptureRegion(int x, int y, int width, int height)
    {
        IntPtr hDesktopDC = GetDC(IntPtr.Zero);
        IntPtr hMemDC = CreateCompatibleDC(hDesktopDC);
        IntPtr hBitmap = CreateCompatibleBitmap(hDesktopDC, width, height);
        IntPtr hOldBitmap = SelectObject(hMemDC, hBitmap);

        BitBlt(hMemDC, 0, 0, width, height, hDesktopDC, x, y, SRCCOPY);

        SelectObject(hMemDC, hOldBitmap);
        var bitmap = Image.FromHbitmap(hBitmap);

        DeleteObject(hBitmap);
        DeleteDC(hMemDC);
        ReleaseDC(IntPtr.Zero, hDesktopDC);

        return bitmap;
    }

    /// <summary>
    /// Get the virtual screen bounds (encompasses all monitors).
    /// </summary>
    public Rectangle GetVirtualScreenBounds()
    {
        return new Rectangle(
            GetSystemMetrics(SM_XVIRTUALSCREEN),
            GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_CXVIRTUALSCREEN),
            GetSystemMetrics(SM_CYVIRTUALSCREEN)
        );
    }

    /// <summary>
    /// Get information about all connected screens.
    /// </summary>
    public IEnumerable<ScreenInfo> GetScreens()
    {
        return Screen.AllScreens.Select((s, i) => new ScreenInfo
        {
            Index = i,
            Bounds = s.Bounds,
            IsPrimary = s.Primary,
            DeviceName = s.DeviceName
        });
    }
}

public class ScreenInfo
{
    public int Index { get; set; }
    public Rectangle Bounds { get; set; }
    public bool IsPrimary { get; set; }
    public string DeviceName { get; set; } = "";
}
