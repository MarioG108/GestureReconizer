using GestureControl.Core.Models;

namespace GestureControl.Core.Interfaces;

/// <summary>
/// Contract for dynamic/temporal gesture classifiers that evaluate sequences of trajectory points.
/// Implements dynamic gesture family detection (SwipeLeft, SwipeRight, SwipeUp, SwipeDown).
/// </summary>
public interface IDynamicGestureClassifier
{
    /// <summary>
    /// Evaluates the recent trajectory history and returns a dynamic gesture if detected.
    /// </summary>
    /// <param name="history">Ordered span of trajectory points, oldest to newest.</param>
    /// <param name="confidence">Output confidence score [0..1].</param>
    /// <returns>Detected dynamic gesture or HandGestureType.None.</returns>
    HandGestureType ClassifyTrajectory(ReadOnlySpan<HandTrajectoryPoint> history, out float confidence);

    /// <summary>
    /// Resets any accumulated temporal state or triggers.
    /// </summary>
    void Reset();

    /// <summary>
    /// Minimum displacement distance in normalized coordinates (0..1) required to trigger a swipe.
    /// </summary>
    float MinSwipeDistance { get; set; }

    /// <summary>
    /// Minimum velocity in normalized coordinates per second required to trigger a swipe.
    /// </summary>
    float MinSwipeVelocity { get; set; }
}
