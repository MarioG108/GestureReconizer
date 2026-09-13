namespace GestureControl.Core.Models;

/// <summary>
/// Application profile mapping gestures to commands, dead zones, and sensitivity settings.
/// </summary>
public class AppProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Global";
    public string ProcessName { get; set; } = "*"; // "*" matches any foreground window
    public string Description { get; set; } = "Default Global Profile";

    // Sensitivity & Dead zone parameters (Section 8.3)
    public float DeadZoneRadius { get; set; } = 0.04f; // 4% of viewport width/height around neutral
    public float MouseSpeedMultiplier { get; set; } = 1.5f;
    public float SmoothingFactor { get; set; } = 0.5f; // EMA alpha
    public int CooldownMs { get; set; } = 250; // Minimum time between consecutive discrete gestures
    public int MinimumHoldDurationMs { get; set; } = 150; // Minimum hold time to confirm discrete gesture

    // Gesture mappings
    public Dictionary<HandGestureType, ActionCommand> GestureBindings { get; set; } = [];

    public static AppProfile CreateDefaultGlobal()
    {
        var profile = new AppProfile
        {
            Id = "global_default",
            Name = "Global System",
            ProcessName = "*",
            Description = "Standard desktop cursor and mouse clicks"
        };

        // Default Bindings:
        // IndexPoint -> Cursor movement (handled smoothly by pipeline)
        // Pinch -> Left Click
        // Fist -> Middle Click / Hold
        // OpenHand -> Neutral / Move without click
        // TwoFingersPeace -> Right Click
        profile.GestureBindings[HandGestureType.Pinch] = ActionCommand.LeftClick();
        profile.GestureBindings[HandGestureType.TwoFingersPeace] = ActionCommand.RightClick();
        profile.GestureBindings[HandGestureType.Fist] = ActionCommand.MiddleClick();

        return profile;
    }

    public static AppProfile CreateBlenderProfile()
    {
        var profile = new AppProfile
        {
            Id = "blender_default",
            Name = "Blender 3D",
            ProcessName = "blender",
            Description = "Blender 3D Viewport navigation (Orbit, Pan, Zoom, Frame)",
            DeadZoneRadius = 0.03f,
            MouseSpeedMultiplier = 2.0f
        };

        // In Blender:
        // Fist -> Middle Down (Orbit viewport)
        // Pinch -> Shift + Middle Down (Pan viewport)
        // TwoFingersPeace -> Numpad '.' (0x6E = Frame Selected)
        // SwipeUp / SwipeDown -> Zoom
        profile.GestureBindings[HandGestureType.Fist] = ActionCommand.MiddleDown();
        profile.GestureBindings[HandGestureType.OpenHand] = ActionCommand.MiddleUp();
        profile.GestureBindings[HandGestureType.TwoFingersPeace] = ActionCommand.Hotkey(0x6E, KeyModifiers.None, "Frame Selected (Num .)");

        return profile;
    }
}
