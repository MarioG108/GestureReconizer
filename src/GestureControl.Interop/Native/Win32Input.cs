using System.Runtime.InteropServices;
using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;

namespace GestureControl.Interop.Native;

/// <summary>
/// Win32 SendInput and Mouse/Keyboard P/Invoke interop with modern C# [LibraryImport] / DllImport.
/// Implements Step 8 in Section 8.2 and Section 11 of the technical architecture.
/// </summary>
public static partial class Win32Input
{
    public const int INPUT_MOUSE = 0;
    public const int INPUT_KEYBOARD = 1;

    public const uint MOUSEEVENTF_MOVE = 0x0001;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint MOUSEEVENTF_WHEEL = 0x0800;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    public const uint KEYEVENTF_SCANCODE = 0x0008;

    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_MENU = 0x12; // Alt
    public const ushort VK_LWIN = 0x5B;

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, [Out] System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Injects a relative mouse movement.
    /// Uses stack-allocated Span or array to send the input directly to Win32.
    /// </summary>
    public static void SendMouseMove(int deltaX, int deltaY)
    {
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].u.mi = new MOUSEINPUT
        {
            dx = deltaX,
            dy = deltaY,
            dwFlags = MOUSEEVENTF_MOVE
        };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Injects mouse button events (down, up, or both).
    /// </summary>
    public static void SendMouseButton(uint flags, uint mouseData = 0)
    {
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].u.mi = new MOUSEINPUT
        {
            dwFlags = flags,
            mouseData = mouseData
        };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Injects a mouse scroll wheel event.
    /// </summary>
    public static void SendMouseWheel(int scrollDelta)
    {
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].u.mi = new MOUSEINPUT
        {
            dwFlags = MOUSEEVENTF_WHEEL,
            mouseData = unchecked((uint)scrollDelta)
        };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Injects a hotkey combination (e.g. Shift + Middle Click, or Ctrl + Z).
    /// </summary>
    public static void SendKeyCombination(ushort vkCode, KeyModifiers modifiers)
    {
        var inputList = new List<INPUT>();

        // Modifiers DOWN
        if (modifiers.HasFlag(KeyModifiers.Control))
            inputList.Add(CreateKeyInput(VK_CONTROL, isKeyUp: false));
        if (modifiers.HasFlag(KeyModifiers.Shift))
            inputList.Add(CreateKeyInput(VK_SHIFT, isKeyUp: false));
        if (modifiers.HasFlag(KeyModifiers.Alt))
            inputList.Add(CreateKeyInput(VK_MENU, isKeyUp: false));
        if (modifiers.HasFlag(KeyModifiers.Windows))
            inputList.Add(CreateKeyInput(VK_LWIN, isKeyUp: false));

        // Main key DOWN & UP
        if (vkCode != 0)
        {
            inputList.Add(CreateKeyInput(vkCode, isKeyUp: false));
            inputList.Add(CreateKeyInput(vkCode, isKeyUp: true));
        }

        // Modifiers UP (reverse order)
        if (modifiers.HasFlag(KeyModifiers.Windows))
            inputList.Add(CreateKeyInput(VK_LWIN, isKeyUp: true));
        if (modifiers.HasFlag(KeyModifiers.Alt))
            inputList.Add(CreateKeyInput(VK_MENU, isKeyUp: true));
        if (modifiers.HasFlag(KeyModifiers.Shift))
            inputList.Add(CreateKeyInput(VK_SHIFT, isKeyUp: true));
        if (modifiers.HasFlag(KeyModifiers.Control))
            inputList.Add(CreateKeyInput(VK_CONTROL, isKeyUp: true));

        var arr = inputList.ToArray();
        SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
    }

    private static INPUT CreateKeyInput(ushort vk, bool isKeyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = isKeyUp ? KEYEVENTF_KEYUP : 0
                }
            }
        };
    }
}
