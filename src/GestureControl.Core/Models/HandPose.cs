using System.Numerics;

namespace GestureControl.Core.Models;

/// <summary>
/// Represents the full 3D pose of a detected hand at a specific instant in time.
/// </summary>
public class HandPose
{
    private readonly HandLandmark[] _landmarks;

    public HandPose(
        ReadOnlySpan<HandLandmark> landmarks,
        Handedness handedness = Handedness.Unknown,
        float confidence = 1.0f,
        DateTime? timestamp = null)
    {
        if (landmarks.Length != 21)
        {
            throw new ArgumentException("A hand pose must contain exactly 21 landmarks.", nameof(landmarks));
        }

        _landmarks = landmarks.ToArray();
        Handedness = handedness;
        Confidence = confidence;
        Timestamp = timestamp ?? DateTime.UtcNow;
    }

    public HandPose(
        IReadOnlyList<HandLandmark> landmarks,
        Handedness handedness = Handedness.Unknown,
        float confidence = 1.0f,
        DateTime? timestamp = null)
    {
        if (landmarks == null || landmarks.Count != 21)
        {
            throw new ArgumentException("A hand pose must contain exactly 21 landmarks.", nameof(landmarks));
        }

        _landmarks = [.. landmarks];
        Handedness = handedness;
        Confidence = confidence;
        Timestamp = timestamp ?? DateTime.UtcNow;
    }

    public IReadOnlyList<HandLandmark> Landmarks => _landmarks;

    /// <summary>
    /// Returns a zero-allocation ReadOnlySpan over the 21 hand landmarks.
    /// </summary>
    public ReadOnlySpan<HandLandmark> AsSpan() => _landmarks.AsSpan();

    public Handedness Handedness { get; }
    public float Confidence { get; }
    public DateTime Timestamp { get; }

    public HandLandmark GetLandmark(HandLandmarkType type) => _landmarks[(int)type];

    // Frequently used landmark accessors
    public HandLandmark Wrist => _landmarks[(int)HandLandmarkType.Wrist];

    // Thumb
    public HandLandmark ThumbCmc => _landmarks[(int)HandLandmarkType.ThumbCmc];
    public HandLandmark ThumbMcp => _landmarks[(int)HandLandmarkType.ThumbMcp];
    public HandLandmark ThumbIp => _landmarks[(int)HandLandmarkType.ThumbIp];
    public HandLandmark ThumbTip => _landmarks[(int)HandLandmarkType.ThumbTip];

    // Index
    public HandLandmark IndexMcp => _landmarks[(int)HandLandmarkType.IndexFingerMcp];
    public HandLandmark IndexPip => _landmarks[(int)HandLandmarkType.IndexFingerPip];
    public HandLandmark IndexDip => _landmarks[(int)HandLandmarkType.IndexFingerDip];
    public HandLandmark IndexTip => _landmarks[(int)HandLandmarkType.IndexFingerTip];

    // Middle
    public HandLandmark MiddleMcp => _landmarks[(int)HandLandmarkType.MiddleFingerMcp];
    public HandLandmark MiddlePip => _landmarks[(int)HandLandmarkType.MiddleFingerPip];
    public HandLandmark MiddleDip => _landmarks[(int)HandLandmarkType.MiddleFingerDip];
    public HandLandmark MiddleTip => _landmarks[(int)HandLandmarkType.MiddleFingerTip];

    // Ring
    public HandLandmark RingMcp => _landmarks[(int)HandLandmarkType.RingFingerMcp];
    public HandLandmark RingPip => _landmarks[(int)HandLandmarkType.RingFingerPip];
    public HandLandmark RingDip => _landmarks[(int)HandLandmarkType.RingFingerDip];
    public HandLandmark RingTip => _landmarks[(int)HandLandmarkType.RingFingerTip];

    // Pinky
    public HandLandmark PinkyMcp => _landmarks[(int)HandLandmarkType.PinkyFingerMcp];
    public HandLandmark PinkyPip => _landmarks[(int)HandLandmarkType.PinkyFingerPip];
    public HandLandmark PinkyDip => _landmarks[(int)HandLandmarkType.PinkyFingerDip];
    public HandLandmark PinkyTip => _landmarks[(int)HandLandmarkType.PinkyFingerTip];

    /// <summary>
    /// Calculates the geometric center of the palm (average of Wrist, Index MCP, and Pinky MCP).
    /// </summary>
    public Vector3 PalmCenter
    {
        get
        {
            var w = Wrist.AsVector3();
            var i = IndexMcp.AsVector3();
            var p = PinkyMcp.AsVector3();
            return (w + i + p) / 3.0f;
        }
    }
}
