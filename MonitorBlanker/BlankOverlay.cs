using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace MonitorBlanker;

/// <summary>
/// A pure Win32 window for blanking monitors. No XAML overhead, no white flash.
/// </summary>
public sealed partial class BlankOverlay : IDisposable
{
    private const string WindowClassName = "MonitorBlankerOverlay";
    private static readonly object s_classLock = new();
    private static bool s_classRegistered;
    private static WndProcDelegate? s_wndProc;

    private nint _hwnd;
    private bool _disposed;

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    public BlankOverlay(RectInt32 bounds)
    {
        EnsureWindowClassRegistered();

        _hwnd = CreateWindowExW(
            0x00000080 | 0x00000008 | 0x08000000, // WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE
            WindowClassName,
            null,
            0x80000000, // WS_POPUP
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            0,
            0,
            0,
            0);

        if (_hwnd == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        ShowWindow(_hwnd, 4); // SW_SHOWNOACTIVATE
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

        if (_hwnd != 0)
        {
            DestroyWindow(_hwnd);
            _hwnd = 0;
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
}
