using GestureControl.Core.Models;

namespace GestureControl.Core.Interfaces;

/// <summary>
/// Dispatches action commands to the OS or active application via Win32 SendInput or hooks.
/// Step 8 in Section 8.2 of the technical pipeline.
/// </summary>
public interface IActionDispatcher
{
    ValueTask ExecuteAsync(ActionCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages application profiles and tracks active foreground window.
/// </summary>
public interface IProfileManager
{
    AppProfile ActiveProfile { get; }
    IReadOnlyList<AppProfile> AvailableProfiles { get; }

    void SetActiveProfile(string profileId);
    void UpdateActiveWindow(string windowTitle, string processName);
    event EventHandler<AppProfile>? ProfileChanged;
}
