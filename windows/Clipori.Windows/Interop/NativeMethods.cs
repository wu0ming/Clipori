using System.Runtime.InteropServices;

namespace Clipori.Windows.Interop;

internal static class NativeMethods
{
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_CONTROL = 0x0002, MOD_ALT = 0x0001, VK_V = 0x56, HOTKEY_ID = 0xC101;
    public const int SW_RESTORE = 9;

    [DllImport("user32.dll", SetLastError = true)] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("kernel32.dll", ExactSpelling = true)] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO info);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")] public static extern uint GetDoubleClickTime();
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint nInputs, [In] INPUT[] inputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
        public RECT rcCaret;
    }

    // INPUT is 40 bytes on Windows x64: 4-byte type, 4-byte padding,
    // and a 32-byte union containing KEYBDINPUT/MOUSEINPUT/HARDWAREINPUT.
    // A too-small struct makes SendInput return ERROR_INVALID_PARAMETER (87).
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public UIntPtr dwExtraInfo; }
    private const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 0x0002;
    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    public static bool AnyPasteModifierHeld()
    {
        foreach (int key in new[] { VK_SHIFT, VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN })
            if ((GetAsyncKeyState(key) & 0x8000) != 0) return true;
        return false;
    }
    private static INPUT Key(ushort key, bool up) => new()
    { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = key, dwFlags = up ? KEYEVENTF_KEYUP : 0 } };

    public static bool SendControlV()
    {
        var events = new[] { Key(0x11, false), Key(0x56, false), Key(0x56, true), Key(0x11, true) };
        int inputSize = Marshal.SizeOf<INPUT>();
        if (IntPtr.Size != 8 || inputSize != 40)
            throw new PlatformNotSupportedException("Clipori paste requires a valid Windows x64 INPUT layout.");
        return SendInput((uint)events.Length, events, inputSize) == events.Length;
    }

    public static IntPtr CaptureFocusedControl(IntPtr parentWindow)
    {
        if (parentWindow == IntPtr.Zero || !IsWindow(parentWindow)) return IntPtr.Zero;
        uint targetThread = GetWindowThreadProcessId(parentWindow, out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        return GetGUIThreadInfo(targetThread, ref info) && IsWindow(info.hwndFocus) ? info.hwndFocus : IntPtr.Zero;
    }

    // Window-activation and virtual-key injection follow Win32 techniques used in CopyQ.
    // AttachThreadInput is always detached, even if focus restoration fails.
    public static bool RestoreForegroundAndFocus(IntPtr target, IntPtr focusedChild)
    {
        if (target == IntPtr.Zero || !IsWindow(target)) return false;
        if (IsIconic(target)) ShowWindow(target, SW_RESTORE);

        // First attempt needs no thread attachment in the common case.
        SetForegroundWindow(target);
        if (GetForegroundWindow() != target)
        {
            // CopyQ also attaches to the current foreground input thread
            // when ordinary SetForegroundWindow is rejected by Windows.
            uint thisThread = GetCurrentThreadId(); // kernel32.dll, NOT user32.dll
            IntPtr foreground = GetForegroundWindow();
            uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
            if (foregroundThread == 0 || foregroundThread == thisThread ||
                !AttachThreadInput(thisThread, foregroundThread, true))
                return false;
            try { SetForegroundWindow(target); }
            finally { AttachThreadInput(thisThread, foregroundThread, false); }
        }
        if (GetForegroundWindow() != target) return false;

        // Many applications automatically restore their focused child window.
        // Only explicitly restore focus when it did not come back.
        if (focusedChild == IntPtr.Zero || !IsWindow(focusedChild)) return true;
        uint ownerThread = GetWindowThreadProcessId(focusedChild, out _);
        if (ownerThread == 0) return true;
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (GetGUIThreadInfo(ownerThread, ref info) && info.hwndFocus == focusedChild)
            return true;

        uint currentThread = GetCurrentThreadId();
        if (ownerThread == currentThread)
        {
            SetFocus(focusedChild);
            return true;
        }
        if (!AttachThreadInput(currentThread, ownerThread, true))
            return true; // Target window is foreground; allow its own focus policy.
        try { SetFocus(focusedChild); }
        finally { AttachThreadInput(currentThread, ownerThread, false); }
        return true;
    }

    public static string ForegroundProcessName()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint processId);
            return System.Diagnostics.Process.GetProcessById((int)processId).ProcessName;
        }
        catch { return "未知应用"; }
    }
}
