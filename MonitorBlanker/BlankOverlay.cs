using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace MonitorBlanker;

/// <summary>
/// Pure Win32 windows for blanking monitors. Uses raw Win32 instead of XAML to avoid
/// overhead and white flash on creation.
/// </summary>
/// <remarks>
/// Each overlay uses two half-screen windows instead of one fullscreen window. This prevents
/// Windows from detecting a "fullscreen app" and automatically enabling Focus Assist (Do Not
/// Disturb), which would suppress notifications system-wide.
/// </remarks>
public sealed partial class BlankOverlay : IDisposable
{
    private const string WindowClassName = "MonitorBlankerOverlay";
    private static readonly object s_classLock = new();
    private static bool s_classRegistered;
    private static WndProcDelegate? s_wndProc;

    private nint _hwnd1;
    private nint _hwnd2;
    private bool _disposed;

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    public BlankOverlay(RectInt32 bounds)
    {
        EnsureWindowClassRegistered();

        // See class remarks for why we use two windows instead of one.
        int halfHeight = bounds.Height / 2;

        _hwnd1 = CreateWindowExW(
            0x00000080 | 0x00000008 | 0x08000000, // WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE
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
            0x00000080 | 0x00000008 | 0x08000000, // WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE
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

        ShowWindow(_hwnd1, 4); // SW_SHOWNOACTIVATE
        ShowWindow(_hwnd2, 4); // SW_SHOWNOACTIVATE
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
        if (_disposed) return;
        _disposed = true;

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
}
