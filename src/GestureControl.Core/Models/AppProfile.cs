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

    // Dynamic swipe parameters
    public float MinSwipeDistance { get; set; } = 0.15f; // Minimum normalized distance for swipe
    public float MinSwipeVelocity { get; set; } = 0.40f; // Minimum normalized velocity (units/sec)
    public int SwipeCooldownMs { get; set; } = 400; // Cooldown after a swipe trigger to avoid duplicates

    // 3D Navigation parameters (Fase 3)
    public bool Enable3DNavigation { get; set; } = false;
    public bool EnableInfiniteCursorWrap { get; set; } = true;
    public float OrbitSensitivity { get; set; } = 1.2f;
    public float PanSensitivity { get; set; } = 1.0f;
    public float ZoomDepthSensitivity { get; set; } = 1.0f;
    public float PalmDepthDeadZone { get; set; } = 0.025f;

    // Mouse Tracking toggle (Fase 3 - Control de Ratón Continuo)
    public bool EnableMouseTracking { get; set; } = true;

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

        // 10 Gestures mapped in Global Profile:
        profile.GestureBindings[HandGestureType.OpenHand] = ActionCommand.NoneWithDescription("Activar / Pausar Control (Toggle Arm)");
        profile.GestureBindings[HandGestureType.IndexPoint] = ActionCommand.NoneWithDescription("Seguimiento de Cursor");
        profile.GestureBindings[HandGestureType.LateralPalm] = ActionCommand.NoneWithDescription("Modo Neutro");
        profile.GestureBindings[HandGestureType.Pinch] = ActionCommand.LeftClick();
        profile.GestureBindings[HandGestureType.TwoFingersPeace] = ActionCommand.RightClick();
        profile.GestureBindings[HandGestureType.Fist] = ActionCommand.MiddleClick();

        // Dynamic Swipes for Global profile:
        profile.GestureBindings[HandGestureType.SwipeLeft] = ActionCommand.Hotkey(0x25, KeyModifiers.Alt, "Navegar Atrás (Alt+Izquierda)");
        profile.GestureBindings[HandGestureType.SwipeRight] = ActionCommand.Hotkey(0x27, KeyModifiers.Alt, "Navegar Adelante (Alt+Derecha)");
        profile.GestureBindings[HandGestureType.SwipeUp] = ActionCommand.Hotkey(0x21, KeyModifiers.None, "Re Pág (Page Up)");
        profile.GestureBindings[HandGestureType.SwipeDown] = ActionCommand.Hotkey(0x22, KeyModifiers.None, "Av Pág (Page Down)");

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
            MouseSpeedMultiplier = 2.0f,
            Enable3DNavigation = true,
            EnableInfiniteCursorWrap = true,
            OrbitSensitivity = 1.2f,
            PanSensitivity = 1.0f,
            ZoomDepthSensitivity = 1.0f,
            PalmDepthDeadZone = 0.025f,
            EnableMouseTracking = false
        };

        // 10 Gestures mapped in Blender Profile:
        profile.GestureBindings[HandGestureType.OpenHand] = ActionCommand.NoneWithDescription("Activar / Pausar Control (Toggle Arm)");
        profile.GestureBindings[HandGestureType.IndexPoint] = ActionCommand.NoneWithDescription("Seguimiento de Cursor");
        profile.GestureBindings[HandGestureType.LateralPalm] = ActionCommand.NoneWithDescription("Modo Neutro (Descanso)");
        profile.GestureBindings[HandGestureType.Pinch] = ActionCommand.LeftClick();
        profile.GestureBindings[HandGestureType.Fist] = ActionCommand.MiddleDown();
        profile.GestureBindings[HandGestureType.TwoFingersPeace] = ActionCommand.Hotkey(0x6E, KeyModifiers.None, "Centrar Selección (Num .)");
        profile.GestureBindings[HandGestureType.SwipeLeft] = ActionCommand.Hotkey(0x5A, KeyModifiers.Control, "Deshacer (Ctrl+Z)");
        profile.GestureBindings[HandGestureType.SwipeRight] = ActionCommand.Hotkey(0x5A, KeyModifiers.Control | KeyModifiers.Shift, "Rehacer (Ctrl+Shift+Z)");
        profile.GestureBindings[HandGestureType.SwipeUp] = ActionCommand.Hotkey(0x67, KeyModifiers.None, "Vista Superior (Num 7)");
        profile.GestureBindings[HandGestureType.SwipeDown] = ActionCommand.Hotkey(0x61, KeyModifiers.None, "Vista Frontal (Num 1)");

        return profile;
    }
}
