using GestureControl.Core.Models;

namespace GestureControl.Core.Interfaces;

/// <summary>
/// Abstraction for hand landmark detection and tracking models (e.g. ONNX Runtime, MediaPipe).
/// </summary>
public interface IHandTracker : IDisposable
{
    string TrackerName { get; }
    bool IsModelLoaded { get; }

    /// <summary>
    /// Detects hands and extracts 3D landmarks from a camera frame.
    /// Uses ReadOnlySpan to avoid intermediate byte array allocations.
    /// </summary>
    IReadOnlyList<HandPose> TrackHands(
        ReadOnlySpan<byte> frameRgbOrBgr,
        int width,
        int height,
        int stride);
}
