using GestureControl.Core.Models;

namespace GestureControl.Core.Interfaces;

/// <summary>
/// Extracts derived geometric and kinematic features from hand landmarks.
/// Step 5 in Section 8.2 of the technical pipeline.
/// </summary>
public interface IFeatureExtractor
{
    /// <summary>
    /// Computes features using a zero-allocation span of the 21 landmarks.
    /// </summary>
    HandFeatures ExtractFeatures(
        ReadOnlySpan<HandLandmark> landmarks,
        HandPose? previousPose = null,
        float deltaTimeSeconds = 0.033f);

    HandFeatures ExtractFeatures(
        HandPose pose,
        HandPose? previousPose = null,
        float deltaTimeSeconds = 0.033f);
}
