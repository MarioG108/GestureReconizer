namespace GestureControl.Core.Models;

public enum ActionCommandType
{
    None = 0,
    MouseMoveRelative = 1,
    MouseMoveAbsolute = 2,
    MouseLeftClick = 3,
    MouseLeftDown = 4,
    MouseLeftUp = 5,
    MouseRightClick = 6,
    MouseRightDown = 7,
    MouseRightUp = 8,
    MouseMiddleClick = 9,
    MouseMiddleDown = 10,
    MouseMiddleUp = 11,
    MouseScroll = 12,
    KeyPress = 13,
    KeyDown = 14,
    KeyUp = 15,
    KeyCombination = 16,
    CustomMacro = 17
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
    Windows = 8
}

/// <summary>
/// Logical action command dispatched to the OS or active application.
/// </summary>
public record ActionCommand(
    ActionCommandType Type,
    float DeltaX = 0f,
    float DeltaY = 0f,
    float AbsoluteX = 0f,
    float AbsoluteY = 0f,
    int ScrollDelta = 0,
    ushort VirtualKeyCode = 0,
    KeyModifiers Modifiers = KeyModifiers.None,
    string Description = "")
{
    public static ActionCommand None => new(ActionCommandType.None);

    public static ActionCommand MoveMouse(float dx, float dy) =>
        new(ActionCommandType.MouseMoveRelative, DeltaX: dx, DeltaY: dy, Description: $"Move mouse ({dx:F1}, {dy:F1})");

    public static ActionCommand LeftClick() =>
        new(ActionCommandType.MouseLeftClick, Description: "Left Click");

    public static ActionCommand LeftDown() =>
        new(ActionCommandType.MouseLeftDown, Description: "Left Down (Drag/Select)");

    public static ActionCommand LeftUp() =>
        new(ActionCommandType.MouseLeftUp, Description: "Left Up");

    public static ActionCommand MiddleClick() =>
        new(ActionCommandType.MouseMiddleClick, Description: "Middle Click (Orbit/Pan)");

    public static ActionCommand MiddleDown() =>
        new(ActionCommandType.MouseMiddleDown, Description: "Middle Down (Orbit start)");

    public static ActionCommand MiddleUp() =>
        new(ActionCommandType.MouseMiddleUp, Description: "Middle Up (Orbit release)");

    public static ActionCommand RightClick() =>
        new(ActionCommandType.MouseRightClick, Description: "Right Click");

    public static ActionCommand RightDown() =>
        new(ActionCommandType.MouseRightDown, Description: "Right Down");

    public static ActionCommand RightUp() =>
        new(ActionCommandType.MouseRightUp, Description: "Right Up");

    public static ActionCommand Scroll(int delta) =>
        new(ActionCommandType.MouseScroll, ScrollDelta: delta, Description: $"Scroll {delta}");

    public static ActionCommand Hotkey(ushort vkCode, KeyModifiers modifiers = KeyModifiers.None, string desc = "") =>
        new(ActionCommandType.KeyCombination, VirtualKeyCode: vkCode, Modifiers: modifiers, Description: desc);
}
