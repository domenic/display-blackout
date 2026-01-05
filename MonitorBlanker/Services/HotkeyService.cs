using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace MonitorBlanker.Services;

public sealed partial class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private const uint WM_HOTKEY = 0x0312;
    private const int GWL_WNDPROC = -4;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_B = 0x42;

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    private nint _hwnd;
    private WndProcDelegate? _newWndProc;
    private nint _oldWndProc;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    ~HotkeyService()
    {
        Dispose(disposing: false);
    }

    public void Register(Window window)
    {
        _hwnd = WindowNative.GetWindowHandle(window);

        // Subclass the window to receive WM_HOTKEY
        _newWndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_newWndProc));

        // Register Win+Shift+B
        if (!RegisterHotKey(_hwnd, HotkeyId, MOD_WIN | MOD_SHIFT, VK_B))
        {
            int error = Marshal.GetLastPInvokeError();
            throw new Win32Exception(error, "Failed to register hotkey Win+Shift+B. It may be in use by another application.");
        }
    }

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_HOTKEY && (int)wParam == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        return CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        // Unmanaged cleanup (runs in both disposing and finalizer)
        if (_hwnd != 0)
        {
            UnregisterHotKey(_hwnd, HotkeyId);

            // Restore original window proc
            if (_oldWndProc != 0)
            {
                SetWindowLongPtr(_hwnd, GWL_WNDPROC, _oldWndProc);
            }
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);
}
