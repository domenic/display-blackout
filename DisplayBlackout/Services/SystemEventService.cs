using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DisplayBlackout.Services;

/// <summary>
/// Listens for system-level events (hotkeys, display changes) using a message-only Win32 window.
/// </summary>
public sealed partial class SystemEventService : IDisposable
{
    private const string WindowClassName = "DisplayBlackoutSystemEvents";
    private const int HotkeyId = 1;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_DISPLAYCHANGE = 0x007E;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_B = 0x42;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint EVENT_OBJECT_FOCUS = 0x8005;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private static readonly WndProcDelegate s_wndProc = WndProc;
    private static readonly WinEventDelegate s_winEventProc = WinEventProc;

    public static SystemEventService Instance { get; } = new();

    private nint _hwnd;
    private nint _foregroundHook;
    private nint _focusHook;

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);
    private delegate void WinEventDelegate(
        nint hWinEventHook, uint eventType, nint hwnd, int idObject,
        int idChild, uint idEventThread, uint dwmsEventTime);

    public event EventHandler? HotkeyPressed;
    public event EventHandler? DisplayChanged;
    public event EventHandler? FocusChanged;

    static SystemEventService() { }

    private SystemEventService()
    {
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
            hInstance = GetModuleHandleW(null),
            lpszClassName = WindowClassName
        };

        if (RegisterClassExW(ref wc) == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        // Create a hidden window to receive system broadcast messages like WM_DISPLAYCHANGE
        // (Message-only windows with HWND_MESSAGE don't receive broadcasts)
        _hwnd = CreateWindowExW(
            0x00000080, // WS_EX_TOOLWINDOW (no taskbar button)
            WindowClassName,
            null,
            0,
            0, 0, 0, 0,
            0,
            0, 0, 0);

        if (_hwnd == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        // Register Win+Shift+B hotkey
        if (!RegisterHotKey(_hwnd, HotkeyId, MOD_WIN | MOD_SHIFT, VK_B))
        {
            int error = Marshal.GetLastPInvokeError();
            DestroyWindow(_hwnd);
            throw new Win32Exception(error, "Failed to register hotkey Win+Shift+B. It may be in use by another application.");
        }

        // Install WinEvent hooks for focus changes
        _foregroundHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            0,
            s_winEventProc,
            0,
            0,
            WINEVENT_OUTOFCONTEXT);
        _focusHook = SetWinEventHook(
            EVENT_OBJECT_FOCUS,
            EVENT_OBJECT_FOCUS,
            0,
            s_winEventProc,
            0,
            0,
            WINEVENT_OUTOFCONTEXT);
    }

    private static void WinEventProc(
        nint hWinEventHook, uint eventType, nint hwnd, int idObject,
        int idChild, uint idEventThread, uint dwmsEventTime)
    {
        Instance.FocusChanged?.Invoke(Instance, EventArgs.Empty);
    }

    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_HOTKEY && (int)wParam == HotkeyId)
        {
            Instance.HotkeyPressed?.Invoke(Instance, EventArgs.Empty);
            return 0;
        }

        if (msg == WM_DISPLAYCHANGE)
        {
            Instance.DisplayChanged?.Invoke(Instance, EventArgs.Empty);
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_foregroundHook != 0)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = 0;
        }
        if (_focusHook != 0)
        {
            UnhookWinEvent(_focusHook);
            _focusHook = 0;
        }
        if (_hwnd != 0)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
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
    private static partial bool DestroyWindow(nint hWnd);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetModuleHandleW(string? lpModuleName);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint SetWinEventHook(
        uint eventMin, uint eventMax, nint hmodWinEventProc,
        WinEventDelegate pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWinEvent(nint hWinEventHook);
}
