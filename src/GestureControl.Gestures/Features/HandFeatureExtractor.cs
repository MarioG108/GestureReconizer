using System.Numerics;
using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;

namespace GestureControl.Gestures.Features;

/// <summary>
/// High-performance feature extractor for hand landmarks.
/// Operates on ReadOnlySpan<HandLandmark> with SIMD vector intrinsics.
/// Step 5 in Section 8.2 of the technical pipeline.
/// </summary>
public class HandFeatureExtractor : IFeatureExtractor
{
    public HandFeatures ExtractFeatures(
        ReadOnlySpan<HandLandmark> landmarks,
        HandPose? previousPose = null,
        float deltaTimeSeconds = 0.033f)
    {
        if (landmarks.Length != 21)
        {
            throw new ArgumentException("Must supply 21 landmarks.", nameof(landmarks));
        }

        var wrist = landmarks[(int)HandLandmarkType.Wrist];

        // 1. Evaluate finger extension
        // A finger is considered extended if the tip is further from the wrist than the PIP joint.
        bool isThumbExtended = IsFingerExtended(landmarks, HandLandmarkType.ThumbMcp, HandLandmarkType.ThumbTip, wrist);
        bool isIndexExtended = IsFingerExtended(landmarks, HandLandmarkType.IndexFingerPip, HandLandmarkType.IndexFingerTip, wrist);
        bool isMiddleExtended = IsFingerExtended(landmarks, HandLandmarkType.MiddleFingerPip, HandLandmarkType.MiddleFingerTip, wrist);
        bool isRingExtended = IsFingerExtended(landmarks, HandLandmarkType.RingFingerPip, HandLandmarkType.RingFingerTip, wrist);
        bool isPinkyExtended = IsFingerExtended(landmarks, HandLandmarkType.PinkyFingerPip, HandLandmarkType.PinkyFingerTip, wrist);

        // 2. Pinch distances
        var thumbTip = landmarks[(int)HandLandmarkType.ThumbTip];
        var indexTip = landmarks[(int)HandLandmarkType.IndexFingerTip];
        var middleTip = landmarks[(int)HandLandmarkType.MiddleFingerTip];
        var pinkyTip = landmarks[(int)HandLandmarkType.PinkyFingerTip];

        float pinchDistance = thumbTip.DistanceTo(indexTip);
        float pinchMiddleDistance = thumbTip.DistanceTo(middleTip);
        float handSpan = thumbTip.DistanceTo(pinkyTip);

        // 3. Palm Normal Vector via Cross Product
        // Vector 1: Wrist -> Index MCP
        // Vector 2: Wrist -> Pinky MCP
        var indexMcp = landmarks[(int)HandLandmarkType.IndexFingerMcp];
        var pinkyMcp = landmarks[(int)HandLandmarkType.PinkyFingerMcp];

        Vector3 v1 = indexMcp.AsVector3() - wrist.AsVector3();
        Vector3 v2 = pinkyMcp.AsVector3() - wrist.AsVector3();
        Vector3 cross = Vector3.Cross(v1, v2);
        Vector3 palmNormal = cross.LengthSquared() > 1e-6f ? Vector3.Normalize(cross) : Vector3.UnitZ;

        // 4. Palm Center and Velocity
        Vector3 palmCenter = (wrist.AsVector3() + indexMcp.AsVector3() + pinkyMcp.AsVector3()) / 3.0f;
        Vector2 velocity = Vector2.Zero;
        float speed = 0f;

        if (previousPose != null && deltaTimeSeconds > 0.001f)
        {
            Vector3 prevCenter = previousPose.PalmCenter;
            float dx = (palmCenter.X - prevCenter.X) / deltaTimeSeconds;
            float dy = (palmCenter.Y - prevCenter.Y) / deltaTimeSeconds;
            velocity = new Vector2(dx, dy);
            speed = MathF.Sqrt(dx * dx + dy * dy);
        }

        return new HandFeatures(
            IsThumbExtended: isThumbExtended,
            IsIndexExtended: isIndexExtended,
            IsMiddleExtended: isMiddleExtended,
            IsRingExtended: isRingExtended,
            IsPinkyExtended: isPinkyExtended,
            PinchDistance: pinchDistance,
            PinchMiddleDistance: pinchMiddleDistance,
            HandSpan: handSpan,
            PalmNormal: palmNormal,
            Velocity: velocity,
            Speed: speed);
    }

    public HandFeatures ExtractFeatures(
        HandPose pose,
        HandPose? previousPose = null,
        float deltaTimeSeconds = 0.033f)
    {
        return ExtractFeatures(pose.AsSpan(), previousPose, deltaTimeSeconds);
    }

    private static bool IsFingerExtended(
        ReadOnlySpan<HandLandmark> landmarks,
        HandLandmarkType jointType,
        HandLandmarkType tipType,
        HandLandmark wrist)
    {
        var joint = landmarks[(int)jointType];
        var tip = landmarks[(int)tipType];

        float distWristToTip = wrist.DistanceTo(tip);
        float distWristToJoint = wrist.DistanceTo(joint);

        // Extended if tip is clearly further from wrist than the joint
        return distWristToTip > distWristToJoint * 1.15f;
    }
}
