using System.Runtime.InteropServices;

namespace DisplayBlackout;

/// <summary>
/// Win32 P/Invoke declarations, structs, enums, and constants shared across the application.
/// </summary>
internal static partial class NativeMethods
{
    // Structs

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEXW
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

    // Delegates

    public delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    public delegate void WinEventDelegate(
        nint hWinEventHook, uint eventType, nint hwnd, int idObject,
        int idChild, uint idEventThread, uint dwmsEventTime);

    // Enums

    [Flags]
    public enum WINDOW_EX_STYLE : uint
    {
        WS_EX_TOPMOST     = 0x00000008,
        WS_EX_TRANSPARENT = 0x00000020,
        WS_EX_TOOLWINDOW  = 0x00000080,
        WS_EX_LAYERED     = 0x00080000,
        WS_EX_NOACTIVATE  = 0x08000000,
    }

    [Flags]
    public enum WINDOW_STYLE : uint
    {
        WS_POPUP = 0x80000000,
    }

    public enum SHOW_WINDOW_CMD
    {
        SW_SHOWNOACTIVATE = 4,
    }

    public enum WINDOW_LONG_PTR_INDEX
    {
        GWL_EXSTYLE = -20,
    }

    [Flags]
    public enum LAYERED_WINDOW_ATTRIBUTES_FLAGS : uint
    {
        LWA_ALPHA = 0x00000002,
    }

    [Flags]
    public enum SET_WINDOW_POS_FLAGS : uint
    {
        SWP_NOSIZE     = 0x0001,
        SWP_NOMOVE     = 0x0002,
        SWP_NOACTIVATE = 0x0010,
    }

    public enum GET_STOCK_OBJECT_FLAGS
    {
        BLACK_BRUSH = 4,
    }

    [Flags]
    public enum HOT_KEY_MODIFIERS : uint
    {
        MOD_SHIFT = 0x0004,
        MOD_WIN   = 0x0008,
    }

    public enum VIRTUAL_KEY : uint
    {
        VK_B = 0x42,
    }

    // Constants

    public const int HWND_TOPMOST = -1;
    public const int IDC_ARROW = 32512;
    public const uint WM_HOTKEY = 0x0312;
    public const uint WM_DISPLAYCHANGE = 0x007E;
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_OBJECT_FOCUS = 0x8005;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    // P/Invoke declarations

    // DllImport (not LibraryImport) because WNDCLASSEXW contains string fields that the
    // LibraryImport source generator cannot marshal in a ref struct parameter (SYSLIB1051).
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint CreateWindowExW(
        WINDOW_EX_STYLE dwExStyle, string lpClassName, string? lpWindowName, WINDOW_STYLE dwStyle,
        int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hWnd, SHOW_WINDOW_CMD nCmdShow);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hWnd);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint GetModuleHandleW(string? lpModuleName);

    [LibraryImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint GetStockObject(GET_STOCK_OBJECT_FLAGS i);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint LoadCursorW(nint hInstance, int lpCursorName);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, LAYERED_WINDOW_ATTRIBUTES_FLAGS dwFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint SetWindowLongPtrW(nint hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint hWnd, int id, HOT_KEY_MODIFIERS fsModifiers, VIRTUAL_KEY vk);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint SetWinEventHook(
        uint eventMin, uint eventMax, nint hmodWinEventProc,
        WinEventDelegate pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(nint hWinEventHook);
}
