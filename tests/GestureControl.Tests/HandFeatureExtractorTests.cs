using GestureControl.Gestures.Features;
using GestureControl.Tests.TestHelpers;
using Xunit;

namespace GestureControl.Tests;

public class HandFeatureExtractorTests
{
    private readonly HandFeatureExtractor _extractor = new();

    [Fact]
    public void ExtractFeatures_OpenHand_IdentifiesAllFingersExtended()
    {
        var pose = HandPoseGenerator.CreateOpenHand();
        var features = _extractor.ExtractFeatures(pose);

        Assert.True(features.IsThumbExtended, "Thumb should be extended");
        Assert.True(features.IsIndexExtended, "Index should be extended");
        Assert.True(features.IsMiddleExtended, "Middle should be extended");
        Assert.True(features.IsRingExtended, "Ring should be extended");
        Assert.True(features.IsPinkyExtended, "Pinky should be extended");
        Assert.True(features.AreAllFingersExtended, "All fingers should be marked extended");
        Assert.Equal(5, features.ExtendedFingerCount);
    }

    [Fact]
    public void ExtractFeatures_Fist_IdentifiesAllFingersFolded()
    {
        var pose = HandPoseGenerator.CreateFist();
        var features = _extractor.ExtractFeatures(pose);

        Assert.False(features.IsIndexExtended, "Index should be folded");
        Assert.False(features.IsMiddleExtended, "Middle should be folded");
        Assert.False(features.IsRingExtended, "Ring should be folded");
        Assert.False(features.IsPinkyExtended, "Pinky should be folded");
        Assert.True(features.AreAllFingersFolded, "Fingers should be marked folded");
    }

    [Fact]
    public void ExtractFeatures_IndexPoint_IdentifiesOnlyIndexExtended()
    {
        var pose = HandPoseGenerator.CreateIndexPoint();
        var features = _extractor.ExtractFeatures(pose);

        Assert.True(features.IsIndexExtended, "Index should be extended");
        Assert.False(features.IsMiddleExtended, "Middle should be folded");
        Assert.False(features.IsRingExtended, "Ring should be folded");
        Assert.False(features.IsPinkyExtended, "Pinky should be folded");
        Assert.True(features.IsOnlyIndexExtended, "Only index should be extended");
    }

    [Fact]
    public void ExtractFeatures_TwoFingersPeace_IdentifiesIndexAndMiddleOnly()
    {
        var pose = HandPoseGenerator.CreateTwoFingersPeace();
        var features = _extractor.ExtractFeatures(pose);

        Assert.True(features.IsIndexExtended, "Index should be extended");
        Assert.True(features.IsMiddleExtended, "Middle should be extended");
        Assert.False(features.IsRingExtended, "Ring should be folded");
        Assert.False(features.IsPinkyExtended, "Pinky should be folded");
        Assert.True(features.AreOnlyIndexAndMiddleExtended, "Index and Middle should be the only extended fingers");
    }

    [Fact]
    public void ExtractFeatures_Pinch_CalculatesSmallPinchDistance()
    {
        var pose = HandPoseGenerator.CreatePinch();
        var features = _extractor.ExtractFeatures(pose);

        Assert.True(features.PinchDistance < 0.05f, $"Pinch distance was {features.PinchDistance}, expected < 0.05");
    }

    [Fact]
    public void ExtractFeatures_Span_MatchesPoseFeaturesEqually()
    {
        var pose = HandPoseGenerator.CreateOpenHand();

        var featFromPose = _extractor.ExtractFeatures(pose);
        var featFromSpan = _extractor.ExtractFeatures(pose.AsSpan());

        Assert.Equal(featFromPose.ExtendedFingerCount, featFromSpan.ExtendedFingerCount);
        Assert.Equal(featFromPose.PinchDistance, featFromSpan.PinchDistance, precision: 5);
        Assert.Equal(featFromPose.HandSpan, featFromSpan.HandSpan, precision: 5);
        Assert.Equal(featFromPose.PalmNormal.Z, featFromSpan.PalmNormal.Z, precision: 4);
    }
}
