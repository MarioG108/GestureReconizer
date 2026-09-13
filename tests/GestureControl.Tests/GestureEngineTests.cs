using GestureControl.Core.Models;
using GestureControl.Gestures.Engine;
using GestureControl.Gestures.Features;
using GestureControl.Tests.TestHelpers;
using Xunit;

namespace GestureControl.Tests;

public class GestureEngineTests
{
    private readonly HandFeatureExtractor _extractor = new();

    [Fact]
    public void ProcessFrame_RequiresHoldDuration_BeforeTriggeringDiscreteGesture()
    {
        var engine = new GestureEngine();
        engine.CurrentProfile.MinimumHoldDurationMs = 150;

        var pose = HandPoseGenerator.CreatePinch();
        var features = _extractor.ExtractFeatures(pose);

        var startTime = DateTime.UtcNow;

        // Frame 1 at t = 0 ms -> should NOT trigger yet
        var pose1 = new HandPose(pose.Landmarks, pose.Handedness, pose.Confidence, startTime);
        var event1 = engine.ProcessFrame(pose1, features, HandGestureType.Pinch, 0.95f);
        Assert.Null(event1);

        // Frame 2 at t = 50 ms -> should NOT trigger yet
        var pose2 = new HandPose(pose.Landmarks, pose.Handedness, pose.Confidence, startTime.AddMilliseconds(50));
        var event2 = engine.ProcessFrame(pose2, features, HandGestureType.Pinch, 0.95f);
        Assert.Null(event2);

        // Frame 3 at t = 160 ms (> 150 ms hold) -> MUST trigger!
        var pose3 = new HandPose(pose.Landmarks, pose.Handedness, pose.Confidence, startTime.AddMilliseconds(160));
        var event3 = engine.ProcessFrame(pose3, features, HandGestureType.Pinch, 0.95f);
        Assert.NotNull(event3);
        Assert.True(event3.IsConfirmed);
        Assert.Equal(HandGestureType.Pinch, event3.Gesture);
        Assert.Equal(ActionCommandType.MouseLeftClick, event3.SuggestedAction?.Type);
    }

    [Fact]
    public void ProcessFrame_AppliesCooldown_BetweenDiscreteGestures()
    {
        var engine = new GestureEngine();
        engine.CurrentProfile.MinimumHoldDurationMs = 50;
        engine.CurrentProfile.CooldownMs = 300;

        var pose = HandPoseGenerator.CreatePinch();
        var features = _extractor.ExtractFeatures(pose);

        var t0 = DateTime.UtcNow;

        // Trigger first pinch at t0 + 60ms
        engine.ProcessFrame(new HandPose(pose.Landmarks, pose.Handedness, pose.Confidence, t0), features, HandGestureType.Pinch, 0.95f);
        var firstTrigger = engine.ProcessFrame(new HandPose(pose.Landmarks, pose.Handedness, pose.Confidence, t0.AddMilliseconds(60)), features, HandGestureType.Pinch, 0.95f);
        Assert.NotNull(firstTrigger);

        // Attempt another pinch at t0 + 120ms (within 300ms cooldown) -> should be blocked!
        var blockedTrigger = engine.ProcessFrame(new HandPose(pose.Landmarks, pose.Handedness, pose.Confidence, t0.AddMilliseconds(120)), features, HandGestureType.Pinch, 0.95f);
        Assert.Null(blockedTrigger);
    }

    [Fact]
    public void ProcessFrame_DeadZone_SuppressesSmallMovements()
    {
        var engine = new GestureEngine();
        engine.CurrentProfile.DeadZoneRadius = 0.05f; // 5% radius deadzone

        var t0 = DateTime.UtcNow;
        var pose1 = HandPoseGenerator.CreateIndexPoint(wristX: 0.5f, wristY: 0.5f);
        var features1 = _extractor.ExtractFeatures(pose1);

        // Anchor frame
        var e1 = engine.ProcessFrame(pose1, features1, HandGestureType.IndexPoint, 0.95f);
        Assert.NotNull(e1);
        Assert.Null(e1.SuggestedAction); // First frame anchors position

        // Tiny movement within dead zone (dist = 0.01 < 0.05)
        var pose2 = HandPoseGenerator.CreateIndexPoint(wristX: 0.51f, wristY: 0.5f);
        var features2 = _extractor.ExtractFeatures(pose2);
        var e2 = engine.ProcessFrame(pose2, features2, HandGestureType.IndexPoint, 0.95f);
        Assert.NotNull(e2);
        Assert.Null(e2.SuggestedAction); // Dead zone suppresses cursor movement!

        // Significant movement outside dead zone (dist = 0.08 > 0.05)
        var pose3 = HandPoseGenerator.CreateIndexPoint(wristX: 0.58f, wristY: 0.5f);
        var features3 = _extractor.ExtractFeatures(pose3);
        var e3 = engine.ProcessFrame(pose3, features3, HandGestureType.IndexPoint, 0.95f);
        Assert.NotNull(e3);
        Assert.NotNull(e3.SuggestedAction);
        Assert.Equal(ActionCommandType.MouseMoveRelative, e3.SuggestedAction.Type);
    }

    [Fact]
    public void ProcessFrame_ActivationToggle_ActivatesOnLongHold()
    {
        var engine = new GestureEngine { IsActive = false }; // Start inactive

        var pose = HandPoseGenerator.CreateOpenHand();
        var features = _extractor.ExtractFeatures(pose);
        var t0 = DateTime.UtcNow;

        // t = 0 ms -> not activated yet
        var e0 = engine.ProcessFrame(new HandPose(pose.Landmarks, pose.Handedness, pose.Confidence, t0), features, HandGestureType.OpenHand, 0.95f);
        Assert.Null(e0);
        Assert.False(engine.IsActive);

        // t = 500 ms -> not activated yet
        var e1 = engine.ProcessFrame(new HandPose(pose.Landmarks, pose.Handedness, pose.Confidence, t0.AddMilliseconds(500)), features, HandGestureType.OpenHand, 0.95f);
        Assert.Null(e1);
        Assert.False(engine.IsActive);

        // t = 1050 ms -> holding OpenHand > 1000 ms activates engine!
        var e2 = engine.ProcessFrame(new HandPose(pose.Landmarks, pose.Handedness, pose.Confidence, t0.AddMilliseconds(1050)), features, HandGestureType.OpenHand, 0.95f);
        Assert.NotNull(e2);
        Assert.True(engine.IsActive, "Engine should now be active!");
    }
}
