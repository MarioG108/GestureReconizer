using System.Text.Json;
using GestureControl.Core.Models;
using GestureControl.Gestures.Engine;
using GestureControl.Gestures.Features;
using GestureControl.Tests.TestHelpers;
using Xunit;

namespace GestureControl.Tests;

public class MouseTrackingTests
{
    private readonly HandFeatureExtractor _extractor = new();

    [Fact]
    public void WhenMouseTrackingEnabled_PalmMovementEmitsMouseMoveRelative()
    {
        var profile = AppProfile.CreateDefaultGlobal();
        profile.DeadZoneRadius = 0.02f;
        var engine = new GestureEngine(profile);
        Assert.True(engine.EnableMouseTracking);

        var t0 = DateTime.UtcNow;
        var pose1 = HandPoseGenerator.CreateOpenHand(wristX: 0.5f, wristY: 0.7f);
        var features1 = _extractor.ExtractFeatures(pose1);

        // Frame 1: Initial anchor
        var ev1 = engine.ProcessFrame(
            new HandPose(pose1.Landmarks, pose1.Handedness, pose1.Confidence, t0),
            features1,
            HandGestureType.OpenHand,
            0.95f);

        // Frame 2: Move palm rightward (+0.05) and upward (-0.03) beyond deadzone
        var pose2 = HandPoseGenerator.CreateOpenHand(wristX: 0.55f, wristY: 0.67f);
        var features2 = _extractor.ExtractFeatures(pose2);
        var ev2 = engine.ProcessFrame(
            new HandPose(pose2.Landmarks, pose2.Handedness, pose2.Confidence, t0.AddMilliseconds(33)),
            features2,
            HandGestureType.OpenHand,
            0.95f);

        Assert.NotNull(ev2);
        Assert.NotNull(ev2.SuggestedAction);
        Assert.Equal(ActionCommandType.MouseMoveRelative, ev2.SuggestedAction.Type);
        Assert.True(ev2.SuggestedAction.DeltaX > 0, "DeltaX must be positive for rightward palm movement");
        Assert.True(ev2.SuggestedAction.DeltaY < 0, "DeltaY must be negative for upward palm movement");
    }

    [Fact]
    public void WhenMouseTrackingDisabled_PalmMovementDoesNotEmitMove()
    {
        var profile = AppProfile.CreateDefaultGlobal();
        profile.DeadZoneRadius = 0.02f;
        var engine = new GestureEngine(profile);
        engine.EnableMouseTracking = false;

        var t0 = DateTime.UtcNow;
        var pose1 = HandPoseGenerator.CreateOpenHand(wristX: 0.5f, wristY: 0.7f);
        var features1 = _extractor.ExtractFeatures(pose1);

        engine.ProcessFrame(
            new HandPose(pose1.Landmarks, pose1.Handedness, pose1.Confidence, t0),
            features1,
            HandGestureType.OpenHand,
            0.95f);

        // Frame 2: Move hand significantly
        var pose2 = HandPoseGenerator.CreateOpenHand(wristX: 0.60f, wristY: 0.60f);
        var features2 = _extractor.ExtractFeatures(pose2);
        var ev2 = engine.ProcessFrame(
            new HandPose(pose2.Landmarks, pose2.Handedness, pose2.Confidence, t0.AddMilliseconds(33)),
            features2,
            HandGestureType.OpenHand,
            0.95f);

        // With tracking disabled, no MouseMoveRelative action should be generated
        if (ev2 != null && ev2.SuggestedAction != null)
        {
            Assert.NotEqual(ActionCommandType.MouseMoveRelative, ev2.SuggestedAction.Type);
        }
    }

    [Fact]
    public void WhenLateralPalmDetected_ActsAsClutchAndResetsAnchor()
    {
        var profile = AppProfile.CreateDefaultGlobal();
        profile.DeadZoneRadius = 0.02f;
        var engine = new GestureEngine(profile);

        var t0 = DateTime.UtcNow;

        // Frame 1: Hand moving in normal tracking
        var pose1 = HandPoseGenerator.CreateOpenHand(wristX: 0.5f, wristY: 0.7f);
        var features1 = _extractor.ExtractFeatures(pose1);
        engine.ProcessFrame(new HandPose(pose1.Landmarks, pose1.Handedness, pose1.Confidence, t0), features1, HandGestureType.OpenHand, 0.95f);

        // Frame 2: User rotates hand to LateralPalm (Clutch engaged) while moving sideways
        var pose2 = HandPoseGenerator.CreateLateralPalm(wristX: 0.65f, wristY: 0.7f);
        var features2 = _extractor.ExtractFeatures(pose2);
        var ev2 = engine.ProcessFrame(
            new HandPose(pose2.Landmarks, pose2.Handedness, pose2.Confidence, t0.AddMilliseconds(33)),
            features2,
            HandGestureType.LateralPalm,
            0.95f);

        // Clutch must not move the mouse
        if (ev2 != null && ev2.SuggestedAction != null)
        {
            Assert.NotEqual(ActionCommandType.MouseMoveRelative, ev2.SuggestedAction.Type);
        }

        // Frame 3: Returning from clutch resets anchor; first frame should not produce massive delta jump
        var pose3 = HandPoseGenerator.CreateOpenHand(wristX: 0.65f, wristY: 0.7f);
        var features3 = _extractor.ExtractFeatures(pose3);
        var ev3 = engine.ProcessFrame(
            new HandPose(pose3.Landmarks, pose3.Handedness, pose3.Confidence, t0.AddMilliseconds(66)),
            features3,
            HandGestureType.OpenHand,
            0.95f);

        // Anchor is re-established on frame 3, so displacement is 0 (no move emitted)
        if (ev3 != null && ev3.SuggestedAction != null)
        {
            Assert.NotEqual(ActionCommandType.MouseMoveRelative, ev3.SuggestedAction.Type);
        }
    }

    [Fact]
    public void WhenPinchDetected_ClickFreezeSuppressesJitterDuringClick()
    {
        var profile = AppProfile.CreateDefaultGlobal();
        var engine = new GestureEngine(profile);

        var t0 = DateTime.UtcNow;

        // Frame 1: Open hand
        var pose1 = HandPoseGenerator.CreateOpenHand(wristX: 0.5f, wristY: 0.7f);
        var features1 = _extractor.ExtractFeatures(pose1);
        engine.ProcessFrame(new HandPose(pose1.Landmarks, pose1.Handedness, pose1.Confidence, t0), features1, HandGestureType.OpenHand, 0.95f);

        // Frame 2: Pinch onset at +30ms (within 100ms click freeze window) with slight hand tremor
        var pose2 = HandPoseGenerator.CreatePinch(wristX: 0.54f, wristY: 0.72f);
        var features2 = _extractor.ExtractFeatures(pose2);
        var ev2 = engine.ProcessFrame(
            new HandPose(pose2.Landmarks, pose2.Handedness, pose2.Confidence, t0.AddMilliseconds(30)),
            features2,
            HandGestureType.Pinch,
            0.95f);

        // MoveAction should be suppressed during click freeze window
        if (ev2 != null && ev2.SuggestedAction != null)
        {
            Assert.NotEqual(ActionCommandType.MouseMoveRelative, ev2.SuggestedAction.Type);
        }
    }

    [Fact]
    public void AppProfile_SerializesMouseTrackingFlagCorrectly()
    {
        var global = AppProfile.CreateDefaultGlobal();
        Assert.True(global.EnableMouseTracking);

        var blender = AppProfile.CreateBlenderProfile();
        Assert.False(blender.EnableMouseTracking);

        // Serialize and deserialize
        string json = JsonSerializer.Serialize(blender);
        var deserialized = JsonSerializer.Deserialize<AppProfile>(json);

        Assert.NotNull(deserialized);
        Assert.False(deserialized.EnableMouseTracking);
        Assert.Equal(blender.Id, deserialized.Id);
    }

    [Fact]
    public void WhenMovingOutsideDeadZone_HysteresisDeadbandProducesSmoothOutputWithoutJumping()
    {
        var profile = AppProfile.CreateDefaultGlobal();
        profile.DeadZoneRadius = 0.04f;
        var engine = new GestureEngine(profile);

        var t0 = DateTime.UtcNow;

        // Frame 1: Anchor at 0.50f
        var pose1 = HandPoseGenerator.CreateOpenHand(wristX: 0.50f, wristY: 0.70f);
        var f1 = _extractor.ExtractFeatures(pose1);
        engine.ProcessFrame(new HandPose(pose1.Landmarks, pose1.Handedness, pose1.Confidence, t0), f1, HandGestureType.OpenHand, 0.95f);

        // Frame 2: Move to 0.55f (dist = 0.05 > 0.04)
        var pose2 = HandPoseGenerator.CreateOpenHand(wristX: 0.55f, wristY: 0.70f);
        var f2 = _extractor.ExtractFeatures(pose2);
        var ev2 = engine.ProcessFrame(
            new HandPose(pose2.Landmarks, pose2.Handedness, pose2.Confidence, t0.AddMilliseconds(33)),
            f2,
            HandGestureType.OpenHand,
            0.95f);

        Assert.NotNull(ev2);
        Assert.NotNull(ev2.SuggestedAction);
        Assert.Equal(ActionCommandType.MouseMoveRelative, ev2.SuggestedAction.Type);

        // With deadband subtraction, excess distance is 0.01 (0.05 - 0.04), NOT 0.05.
        // dx should be proportional to 0.01 * multiplier, avoiding the monster 0.05 jump.
        float dx = ev2.SuggestedAction.DeltaX;
        Assert.True(dx > 0, "dx should be positive");
        // For excess = 0.01: baseDelta = 0.01 * 1.5 * ~1.0 * ~1.0 * 1920 ~= 28 pixels
        // If it were the old bug (jumping full 0.05): it would be 0.05 * 1.5 * 1920 = 144 pixels!
        Assert.True(dx < 70f, $"dx was {dx}, expected smooth deadband start < 70px instead of 144px monster jump");
    }

    [Fact]
    public void WhenMovingFast_BallisticAccelerationAppliesProgressiveGain()
    {
        var profile = AppProfile.CreateDefaultGlobal();
        profile.DeadZoneRadius = 0.02f;
        var engine = new GestureEngine(profile);

        var t0 = DateTime.UtcNow;

        // Anchor
        var pose0 = HandPoseGenerator.CreateOpenHand(wristX: 0.50f, wristY: 0.70f);
        var f0 = _extractor.ExtractFeatures(pose0);
        engine.ProcessFrame(new HandPose(pose0.Landmarks, pose0.Handedness, pose0.Confidence, t0), f0, HandGestureType.OpenHand, 0.95f);

        // Fast flick: moving by 0.12 (excess = 0.10)
        var poseFlick = HandPoseGenerator.CreateOpenHand(wristX: 0.62f, wristY: 0.70f);
        var fFlick = _extractor.ExtractFeatures(poseFlick);
        var evFlick = engine.ProcessFrame(
            new HandPose(poseFlick.Landmarks, poseFlick.Handedness, poseFlick.Confidence, t0.AddMilliseconds(33)),
            fFlick,
            HandGestureType.OpenHand,
            0.95f);

        Assert.NotNull(evFlick?.SuggestedAction);
        float flickDx = evFlick.SuggestedAction.DeltaX;

        // Ballistic acceleration should provide progressive multiplier for flick gestures
        Assert.True(flickDx > 300f, $"Fast flick dx ({flickDx}) should exceed linear displacement");
    }
}
