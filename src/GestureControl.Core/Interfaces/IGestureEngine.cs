using GestureControl.Core.Models;

namespace GestureControl.Core.Interfaces;

public record GestureEvent(
    HandGestureType Gesture,
    float Confidence,
    HandPose Pose,
    ActionCommand? SuggestedAction,
    DateTime Timestamp,
    bool IsConfirmed);

/// <summary>
/// Intent and Safety engine.
/// Step 7 in Section 8.2 and Section 8.3 of the technical pipeline.
/// Manages state, dead zones, activation/deactivation toggle, hold duration, and cooldowns.
/// </summary>
public interface IGestureEngine
{
    bool IsActive { get; set; }
    AppProfile CurrentProfile { get; set; }

    /// <summary>
    /// Processes a new classified gesture frame, applying the safety rules.
    /// Returns a confirmed GestureEvent if the gesture passes hold time, cooldown, and deadzone checks.
    /// </summary>
    GestureEvent? ProcessFrame(
        HandPose pose,
        HandFeatures features,
        HandGestureType rawGesture,
        float confidence);

    void Reset();
}
