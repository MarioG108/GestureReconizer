using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;

namespace GestureControl.Gestures.Classifiers;

/// <summary>
/// Rule-based static gesture classifier covering the 6 core gestures from Section 8.1:
/// OpenHand, Fist, Pinch, IndexPoint, TwoFingersPeace, LateralPalm.
/// Step 6 in Section 8.2 of the technical pipeline.
/// </summary>
public class StaticGestureClassifier : IGestureClassifier
{
    public float PinchThreshold { get; set; } = 0.065f; // Normalized coordinate distance threshold for pinch
    public float LateralAngleThreshold { get; set; } = 0.35f; // Z component of normal vector

    public HandGestureType Classify(ReadOnlySpan<HandLandmark> landmarks, in HandFeatures features)
    {
        // 1. Fist: all 4 fingers folded into palm
        if (features.AreAllFingersFolded)
        {
            return HandGestureType.Fist;
        }

        // 2. Pinch detection (thumb tip and index tip close together, but hand not in fist)
        if (features.PinchDistance <= PinchThreshold)
        {
            return HandGestureType.Pinch;
        }

        // 3. Index Point: only index finger extended
        if (features.IsOnlyIndexExtended)
        {
            return HandGestureType.IndexPoint;
        }

        // 4. Two Fingers / Peace: index and middle extended
        if (features.AreOnlyIndexAndMiddleExtended)
        {
            return HandGestureType.TwoFingersPeace;
        }

        // 5. Lateral Palm (hand turned sideways to camera)
        if (features.ExtendedFingerCount >= 3 && MathF.Abs(features.PalmNormal.Z) < LateralAngleThreshold)
        {
            return HandGestureType.LateralPalm;
        }

        // 6. Open Hand: all fingers extended
        if (features.AreAllFingersExtended)
        {
            return HandGestureType.OpenHand;
        }

        return HandGestureType.None;
    }

    public GestureDetectionResult Classify(HandPose pose, in HandFeatures features)
    {
        var gesture = Classify(pose.AsSpan(), in features);
        float confidence = gesture != HandGestureType.None ? 0.95f : 0.0f;

        return new GestureDetectionResult(
            Gesture: gesture,
            Confidence: confidence,
            Pose: pose,
            Features: features,
            Timestamp: pose.Timestamp);
    }
}
