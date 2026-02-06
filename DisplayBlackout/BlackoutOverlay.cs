using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;
using static DisplayBlackout.NativeMethods;

namespace DisplayBlackout;

/// <summary>
/// Pure Win32 windows for blacking out monitors. Uses raw Win32 instead of XAML to avoid
/// overhead and white flash on creation.
/// </summary>
/// <remarks>
/// Each overlay uses two half-screen windows instead of one fullscreen window. This prevents
/// Windows from detecting a "fullscreen app" and automatically enabling Focus Assist (Do Not
/// Disturb), which would suppress notifications system-wide.
/// </remarks>
public sealed partial class BlackoutOverlay : IDisposable
{
    private const string WindowClassName = "DisplayBlackoutOverlay";

    private static readonly object s_classLock = new();
    private static bool s_classRegistered;
    private static WndProcDelegate? s_wndProc;

    private WINDOW_EX_STYLE _exStyle;
    private nint _hwnd1;
    private nint _hwnd2;

    public BlackoutOverlay(RectInt32 bounds, int opacityPercent = 100, bool clickThrough = false)
    {
        EnsureWindowClassRegistered();

        try
        {
            // See class remarks for why we use two windows instead of one.
            int halfHeight = bounds.Height / 2;

            _exStyle = WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                       WINDOW_EX_STYLE.WS_EX_TOPMOST |
                       WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
                       WINDOW_EX_STYLE.WS_EX_LAYERED;
            if (clickThrough)
            {
                _exStyle |= WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
            }

            _hwnd1 = CreateWindowExW(
                _exStyle,
                WindowClassName,
                null,
                WINDOW_STYLE.WS_POPUP,
                bounds.X,
                bounds.Y,
                bounds.Width,
                halfHeight,
                0,
                0,
                0,
                0);

            if (_hwnd1 == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            _hwnd2 = CreateWindowExW(
                _exStyle,
                WindowClassName,
                null,
                WINDOW_STYLE.WS_POPUP,
                bounds.X,
                bounds.Y + halfHeight,
                bounds.Width,
                bounds.Height - halfHeight,
                0,
                0,
                0,
                0);

            if (_hwnd2 == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            // Set initial opacity
            SetOpacity(opacityPercent);

            ShowWindow(_hwnd1, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
            ShowWindow(_hwnd2, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Sets the opacity of the overlay windows.
    /// </summary>
    /// <param name="opacityPercent">Opacity percentage from 0 (transparent) to 100 (opaque).</param>
    public void SetOpacity(int opacityPercent)
    {
        byte alpha = (byte)(opacityPercent * 255 / 100);

        if (_hwnd1 != 0 && !SetLayeredWindowAttributes(_hwnd1, 0, alpha, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        if (_hwnd2 != 0 && !SetLayeredWindowAttributes(_hwnd2, 0, alpha, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>
    /// Sets whether the overlay windows are click-through.
    /// </summary>
    /// <param name="clickThrough">If true, mouse events pass through to windows underneath.</param>
    public void SetClickThrough(bool clickThrough)
    {
        var newExStyle = clickThrough
            ? _exStyle | WINDOW_EX_STYLE.WS_EX_TRANSPARENT
            : _exStyle & ~WINDOW_EX_STYLE.WS_EX_TRANSPARENT;

        if (newExStyle == _exStyle) return;
        _exStyle = newExStyle;

        if (_hwnd1 != 0)
        {
            SetWindowLongPtrW(_hwnd1, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (nint)_exStyle);
        }
        if (_hwnd2 != 0)
        {
            SetWindowLongPtrW(_hwnd2, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (nint)_exStyle);
        }
    }

    /// <summary>
    /// Brings the overlay windows to the front, ensuring they stay topmost.
    /// </summary>
    public void BringToFront()
    {
        const SET_WINDOW_POS_FLAGS flags =
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;

        // Best-effort: called frequently on focus changes, so we don't throw on failure.
        if (_hwnd1 != 0)
        {
            SetWindowPos(_hwnd1, HWND_TOPMOST, 0, 0, 0, 0, flags);
        }
        if (_hwnd2 != 0)
        {
            SetWindowPos(_hwnd2, HWND_TOPMOST, 0, 0, 0, 0, flags);
        }
    }

    private static void EnsureWindowClassRegistered()
    {
        lock (s_classLock)
        {
            if (s_classRegistered) return;

            // Keep the delegate alive for the lifetime of the app
            s_wndProc = WndProc;

            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
                hInstance = GetModuleHandleW(null),
                hCursor = LoadCursorW(0, IDC_ARROW),
                hbrBackground = GetStockObject(GET_STOCK_OBJECT_FLAGS.BLACK_BRUSH),
                lpszClassName = WindowClassName
            };

            if (RegisterClassExW(ref wc) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            s_classRegistered = true;
        }
    }

    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hwnd1 != 0)
        {
            DestroyWindow(_hwnd1);
            _hwnd1 = 0;
        }

        if (_hwnd2 != 0)
        {
            DestroyWindow(_hwnd2);
            _hwnd2 = 0;
        }
    }
}
