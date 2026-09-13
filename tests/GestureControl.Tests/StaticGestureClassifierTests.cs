using GestureControl.Core.Models;
using GestureControl.Gestures.Classifiers;
using GestureControl.Gestures.Features;
using GestureControl.Tests.TestHelpers;
using Xunit;

namespace GestureControl.Tests;

public class StaticGestureClassifierTests
{
    private readonly HandFeatureExtractor _extractor = new();
    private readonly StaticGestureClassifier _classifier = new();

    [Fact]
    public void Classify_OpenHand_ReturnsOpenHand()
    {
        var pose = HandPoseGenerator.CreateOpenHand();
        var features = _extractor.ExtractFeatures(pose);

        var result = _classifier.Classify(pose, in features);

        Assert.Equal(HandGestureType.OpenHand, result.Gesture);
        Assert.True(result.Confidence > 0.8f);
    }

    [Fact]
    public void Classify_Fist_ReturnsFist()
    {
        var pose = HandPoseGenerator.CreateFist();
        var features = _extractor.ExtractFeatures(pose);

        var result = _classifier.Classify(pose, in features);

        Assert.Equal(HandGestureType.Fist, result.Gesture);
    }

    [Fact]
    public void Classify_Pinch_ReturnsPinch()
    {
        var pose = HandPoseGenerator.CreatePinch();
        var features = _extractor.ExtractFeatures(pose);

        var result = _classifier.Classify(pose, in features);

        Assert.Equal(HandGestureType.Pinch, result.Gesture);
    }

    [Fact]
    public void Classify_IndexPoint_ReturnsIndexPoint()
    {
        var pose = HandPoseGenerator.CreateIndexPoint();
        var features = _extractor.ExtractFeatures(pose);

        var result = _classifier.Classify(pose, in features);

        Assert.Equal(HandGestureType.IndexPoint, result.Gesture);
    }

    [Fact]
    public void Classify_TwoFingersPeace_ReturnsTwoFingersPeace()
    {
        var pose = HandPoseGenerator.CreateTwoFingersPeace();
        var features = _extractor.ExtractFeatures(pose);

        var result = _classifier.Classify(pose, in features);

        Assert.Equal(HandGestureType.TwoFingersPeace, result.Gesture);
    }

    [Fact]
    public void Classify_LateralPalm_ReturnsLateralPalm()
    {
        var pose = HandPoseGenerator.CreateLateralPalm();
        var features = _extractor.ExtractFeatures(pose);

        var result = _classifier.Classify(pose, in features);

        Assert.Equal(HandGestureType.LateralPalm, result.Gesture);
    }
}
