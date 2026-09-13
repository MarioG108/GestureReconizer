using GestureControl.Core.Models;

namespace GestureControl.Core.Interfaces;

/// <summary>
/// Classifies a gesture from features and landmarks.
/// Step 6 in Section 8.2 of the technical pipeline.
/// </summary>
public interface IGestureClassifier
{
    GestureDetectionResult Classify(HandPose pose, in HandFeatures features);
    HandGestureType Classify(ReadOnlySpan<HandLandmark> landmarks, in HandFeatures features);
}
