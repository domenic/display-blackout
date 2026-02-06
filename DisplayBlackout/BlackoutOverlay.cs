using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;

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
    private const uint WS_EX_TRANSPARENT = 0x00000020;

    private static readonly object s_classLock = new();
    private static bool s_classRegistered;
    private static WndProcDelegate? s_wndProc;

    private uint _exStyle;
    private nint _hwnd1;
    private nint _hwnd2;

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    public BlackoutOverlay(RectInt32 bounds, int opacityPercent = 100, bool clickThrough = false)
    {
        EnsureWindowClassRegistered();

        // See class remarks for why we use two windows instead of one.
        int halfHeight = bounds.Height / 2;

        // WS_EX_LAYERED (0x00080000) enables per-window alpha transparency
        // WS_EX_TRANSPARENT (0x00000020) makes the window click-through (added conditionally)
        _exStyle = 0x00000080 | 0x00000008 | 0x08000000 | 0x00080000; // WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_LAYERED
        if (clickThrough)
        {
            _exStyle |= WS_EX_TRANSPARENT;
        }

        _hwnd1 = CreateWindowExW(
            _exStyle,
            WindowClassName,
            null,
            0x80000000, // WS_POPUP
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
            0x80000000, // WS_POPUP
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

        ShowWindow(_hwnd1, 4); // SW_SHOWNOACTIVATE
        ShowWindow(_hwnd2, 4); // SW_SHOWNOACTIVATE
    }

    /// <summary>
    /// Sets the opacity of the overlay windows.
    /// </summary>
    /// <param name="opacityPercent">Opacity percentage from 0 (transparent) to 100 (opaque).</param>
    public void SetOpacity(int opacityPercent)
    {
        byte alpha = (byte)(opacityPercent * 255 / 100);
        const uint LWA_ALPHA = 0x00000002;

        if (_hwnd1 != 0)
        {
            SetLayeredWindowAttributes(_hwnd1, 0, alpha, LWA_ALPHA);
        }
        if (_hwnd2 != 0)
        {
            SetLayeredWindowAttributes(_hwnd2, 0, alpha, LWA_ALPHA);
        }
    }

    /// <summary>
    /// Sets whether the overlay windows are click-through.
    /// </summary>
    /// <param name="clickThrough">If true, mouse events pass through to windows underneath.</param>
    public void SetClickThrough(bool clickThrough)
    {
        uint newExStyle = clickThrough
            ? _exStyle | WS_EX_TRANSPARENT
            : _exStyle & ~WS_EX_TRANSPARENT;

        if (newExStyle == _exStyle) return;
        _exStyle = newExStyle;

        const int GWL_EXSTYLE = -20;
        if (_hwnd1 != 0)
        {
            SetWindowLongPtrW(_hwnd1, GWL_EXSTYLE, (nint)_exStyle);
        }
        if (_hwnd2 != 0)
        {
            SetWindowLongPtrW(_hwnd2, GWL_EXSTYLE, (nint)_exStyle);
        }
    }

    /// <summary>
    /// Brings the overlay windows to the front, ensuring they stay topmost.
    /// </summary>
    public void BringToFront()
    {
        const int HWND_TOPMOST = -1;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE;

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
                hCursor = LoadCursorW(0, 32512), // IDC_ARROW
                hbrBackground = GetStockObject(4), // BLACK_BRUSH
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string? lpszClassName;
        public nint hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint CreateWindowExW(
        uint dwExStyle, string lpClassName, string? lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hWnd);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetModuleHandleW(string? lpModuleName);

    [LibraryImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetStockObject(int i);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint LoadCursorW(nint hInstance, int lpCursorName);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
