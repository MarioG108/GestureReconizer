namespace GestureControl.Core.Models;

/// <summary>
/// Result of gesture classification on a single frame.
/// </summary>
public record GestureDetectionResult(
    HandGestureType Gesture,
    float Confidence,
    HandPose Pose,
    HandFeatures Features,
    DateTime Timestamp);
