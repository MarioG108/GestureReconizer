using GestureControl.Core.Models;

namespace GestureControl.Core.Interfaces;

/// <summary>
/// Temporal smoothing filter (EMA / Kalman / OneEuro) applied to hand landmarks.
/// Step 4 in Section 8.2 of the technical pipeline.
/// </summary>
public interface ITemporalFilter
{
    float Alpha { get; set; }

    /// <summary>
    /// Smooths a hand pose, writing directly to the destination span to achieve zero heap allocation.
    /// </summary>
    void Smooth(ReadOnlySpan<HandLandmark> current, Span<HandLandmark> destination);

    /// <summary>
    /// Smooths a HandPose and returns a new smoothed HandPose.
    /// </summary>
    HandPose Smooth(HandPose rawPose);

    /// <summary>
    /// Resets historical state (e.g., when a hand leaves the frame).
    /// </summary>
    void Reset();
}
