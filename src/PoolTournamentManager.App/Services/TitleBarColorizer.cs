using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PoolTournamentManager.App.Services;

/// <summary>
/// Colors a window's native title bar via DWM (Windows 11 22000+) so it can match the app's own
/// palette instead of always following the plain system light/dark caption color. WPF has no
/// managed API for this - the title bar is drawn by the OS compositor, not by anything in our
/// own Window.Background/Style, so this has to go through native interop.
/// </summary>
public static class TitleBarColorizer
{
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const uint DwmwaColorDefault = 0xFFFFFFFF;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    /// <summary>
    /// Sets the title bar's caption/text color. Pass null for either color to reset that
    /// attribute back to the system default. No-ops safely on Windows versions that don't
    /// support these attributes (pre-Windows 11) or before the window's HWND exists yet.
    /// </summary>
    public static void Apply(Window window, Color? captionColor, Color? textColor)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var captionValue = captionColor is { } c ? ToColorRef(c) : unchecked((int)DwmwaColorDefault);
        DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref captionValue, sizeof(int));

        var textValue = textColor is { } t ? ToColorRef(t) : unchecked((int)DwmwaColorDefault);
        DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref textValue, sizeof(int));
    }

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);
}
