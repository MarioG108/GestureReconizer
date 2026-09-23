using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;
using GestureControl.Interop.Native;

namespace GestureControl.Actions.Dispatchers;

/// <summary>
/// Dispatches ActionCommands to the Windows OS using Win32 SendInput.
/// Step 8 in Section 8.2 of the technical architecture.
/// </summary>
public class ActionDispatcher : IActionDispatcher
{
    public bool IsEnabled { get; set; } = true;

    public ValueTask ExecuteAsync(ActionCommand command, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || command == null || command.Type == ActionCommandType.None)
            return ValueTask.CompletedTask;

        switch (command.Type)
        {
            case ActionCommandType.MouseMoveRelative:
                Win32Input.SendMouseMove((int)MathF.Round(command.DeltaX), (int)MathF.Round(command.DeltaY));
                break;

            case ActionCommandType.MouseLeftClick:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_LEFTDOWN);
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_LEFTUP);
                break;

            case ActionCommandType.MouseLeftDown:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_LEFTDOWN);
                break;

            case ActionCommandType.MouseLeftUp:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_LEFTUP);
                break;

            case ActionCommandType.MouseRightClick:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_RIGHTDOWN);
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_RIGHTUP);
                break;

            case ActionCommandType.MouseRightDown:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_RIGHTDOWN);
                break;

            case ActionCommandType.MouseRightUp:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_RIGHTUP);
                break;

            case ActionCommandType.MouseMiddleClick:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_MIDDLEDOWN);
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_MIDDLEUP);
                break;

            case ActionCommandType.MouseMiddleDown:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_MIDDLEDOWN);
                break;

            case ActionCommandType.MouseMiddleUp:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_MIDDLEUP);
                break;

            case ActionCommandType.MouseScroll:
                Win32Input.SendMouseWheel(command.ScrollDelta);
                break;

            case ActionCommandType.KeyCombination:
                Win32Input.SendKeyCombination(command.VirtualKeyCode, command.Modifiers);
                break;

            case ActionCommandType.MouseMoveRelativeWithWrap:
                CursorWrapper.Instance.ApplyRelativeDelta(command.DeltaX, command.DeltaY);
                break;

            case ActionCommandType.BeginCursorWrap:
                CursorWrapper.Instance.BeginWrap();
                break;

            case ActionCommandType.EndCursorWrap:
                CursorWrapper.Instance.EndWrap();
                break;

            case ActionCommandType.PanStart:
                CursorWrapper.Instance.BeginWrap();
                Win32Input.SendKeyDown(Win32Input.VK_SHIFT);
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_MIDDLEDOWN);
                break;

            case ActionCommandType.PanEnd:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_MIDDLEUP);
                Win32Input.SendKeyUp(Win32Input.VK_SHIFT);
                CursorWrapper.Instance.EndWrap();
                break;

            case ActionCommandType.OrbitStart:
                CursorWrapper.Instance.BeginWrap();
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_MIDDLEDOWN);
                break;

            case ActionCommandType.OrbitEnd:
                Win32Input.SendMouseButton(Win32Input.MOUSEEVENTF_MIDDLEUP);
                CursorWrapper.Instance.EndWrap();
                break;

            case ActionCommandType.KeyDown:
                Win32Input.SendKeyDown(command.VirtualKeyCode);
                break;

            case ActionCommandType.KeyUp:
                Win32Input.SendKeyUp(command.VirtualKeyCode);
                break;
        }

        return ValueTask.CompletedTask;
    }
}
