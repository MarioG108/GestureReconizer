using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;

namespace GestureControl.Actions.Profiles;

/// <summary>
/// Profile Manager supporting automatic context switching for Blender, Unity, and Unreal.
/// Section 9 of the technical architecture.
/// </summary>
public class ProfileManager : IProfileManager
{
    private readonly List<AppProfile> _profiles = [];
    private AppProfile _activeProfile;

    public AppProfile ActiveProfile => _activeProfile;
    public IReadOnlyList<AppProfile> AvailableProfiles => _profiles.AsReadOnly();

    public event EventHandler<AppProfile>? ProfileChanged;

    public ProfileManager()
    {
        var global = AppProfile.CreateDefaultGlobal();
        var blender = AppProfile.CreateBlenderProfile();

        var unity = new AppProfile
        {
            Id = "unity_default",
            Name = "Unity Editor",
            ProcessName = "unity",
            Description = "Unity Editor scene navigation (W/E/R, Play, Focus F)"
        };
        unity.GestureBindings[HandGestureType.Pinch] = ActionCommand.LeftClick();
        unity.GestureBindings[HandGestureType.Fist] = ActionCommand.RightDown(); // Flythrough camera in Unity
        unity.GestureBindings[HandGestureType.OpenHand] = ActionCommand.RightUp();
        unity.GestureBindings[HandGestureType.TwoFingersPeace] = ActionCommand.Hotkey(0x46, KeyModifiers.None, "Focus Selected (F)");

        var unreal = new AppProfile
        {
            Id = "unreal_default",
            Name = "Unreal Editor",
            ProcessName = "unrealeditor",
            Description = "Unreal Editor viewport navigation"
        };
        unreal.GestureBindings[HandGestureType.Pinch] = ActionCommand.LeftClick();
        unreal.GestureBindings[HandGestureType.Fist] = ActionCommand.RightDown();
        unreal.GestureBindings[HandGestureType.OpenHand] = ActionCommand.RightUp();
        unreal.GestureBindings[HandGestureType.TwoFingersPeace] = ActionCommand.Hotkey(0x46, KeyModifiers.None, "Focus Selected (F)");

        _profiles.Add(global);
        _profiles.Add(blender);
        _profiles.Add(unity);
        _profiles.Add(unreal);

        _activeProfile = global;
    }

    public void SetActiveProfile(string profileId)
    {
        var found = _profiles.FirstOrDefault(p => p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        if (found != null && found != _activeProfile)
        {
            _activeProfile = found;
            ProfileChanged?.Invoke(this, _activeProfile);
        }
    }

    public void UpdateActiveWindow(string windowTitle, string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return;

        var matchingProfile = _profiles.FirstOrDefault(p =>
            p.ProcessName != "*" &&
            processName.Contains(p.ProcessName, StringComparison.OrdinalIgnoreCase));

        var targetProfile = matchingProfile ?? _profiles.First(p => p.ProcessName == "*");
        if (targetProfile != _activeProfile)
        {
            _activeProfile = targetProfile;
            ProfileChanged?.Invoke(this, _activeProfile);
        }
    }
}
