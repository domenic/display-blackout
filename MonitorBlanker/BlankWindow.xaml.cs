using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace MonitorBlanker;

public sealed partial class BlankWindow : Window
{
    private static readonly HWND HwndTopmost = new(new IntPtr(-1));

    public BlankWindow(RectInt32 bounds)
    {
        InitializeComponent();

        var hwnd = (HWND)WindowNative.GetWindowHandle(this);

        // Remove caption and thick frame
        var style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_THICKFRAME);
        SetWindowLongChecked(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)style);

        // Make tool window (hidden from Alt+Tab) and no-activate
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        SetWindowLongChecked(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);

        // Position on target display and make topmost
        PInvoke.SetWindowPos(
            hwnd,
            HwndTopmost,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    private static void SetWindowLongChecked(HWND hwnd, WINDOW_LONG_PTR_INDEX index, int value)
    {
        // Clear last error before call
        Marshal.SetLastPInvokeError(0);
        int result = PInvoke.SetWindowLong(hwnd, index, value);

        // SetWindowLong returns 0 on failure, but 0 can also be a valid previous value.
        // We need to check GetLastError to distinguish.
        if (result == 0)
        {
            int error = Marshal.GetLastPInvokeError();
            if (error != 0)
            {
                throw new Win32Exception(error);
            }
        }
    }
}
