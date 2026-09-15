using System.Numerics;
using GestureControl.Core.Models;
using GestureControl.Gestures.Engine;
using GestureControl.Gestures.Features;
using GestureControl.Tests.TestHelpers;
using Xunit;

namespace GestureControl.Tests;

public class Navigation3DEngineTests
{
    private readonly HandFeatureExtractor _extractor = new();

    [Fact]
    public void ProcessFrame_BlenderProfile_FistEntersOrbit_WithOrbitStart()
    {
        var blenderProfile = AppProfile.CreateBlenderProfile();
        var engine = new GestureEngine(blenderProfile);

        var pose = HandPoseGenerator.CreateFist();
        var features = _extractor.ExtractFeatures(pose);

        var ev = engine.ProcessFrame(pose, features, HandGestureType.Fist, 0.95f);

        Assert.NotNull(ev);
        Assert.Equal(Navigation3DState.Orbit, engine.Active3DState);
        Assert.Equal(Navigation3DState.Orbit, ev.Navigation3D);
        Assert.NotNull(ev.SuggestedAction);
        Assert.Equal(ActionCommandType.OrbitStart, ev.SuggestedAction.Type);
    }

    [Fact]
    public void ProcessFrame_BlenderProfile_FistContinuousDrag_EmitsMoveMouseWithWrap()
    {
        var blenderProfile = AppProfile.CreateBlenderProfile();
        blenderProfile.DeadZoneRadius = 0.02f;
        var engine = new GestureEngine(blenderProfile);

        var t0 = DateTime.UtcNow;
        var pose1 = HandPoseGenerator.CreateFist(wristX: 0.5f, wristY: 0.5f);
        var features1 = _extractor.ExtractFeatures(pose1);

        // Frame 1: Enter Orbit
        var ev1 = engine.ProcessFrame(new HandPose(pose1.Landmarks, pose1.Handedness, pose1.Confidence, t0), features1, HandGestureType.Fist, 0.95f);
        Assert.NotNull(ev1);
        Assert.Equal(ActionCommandType.OrbitStart, ev1.SuggestedAction?.Type);

        // Frame 2: Move fist significantly to the right and up (+0.08, -0.05)
        var pose2 = HandPoseGenerator.CreateFist(wristX: 0.58f, wristY: 0.45f);
        var features2 = _extractor.ExtractFeatures(pose2);
        var ev2 = engine.ProcessFrame(new HandPose(pose2.Landmarks, pose2.Handedness, pose2.Confidence, t0.AddMilliseconds(33)), features2, HandGestureType.Fist, 0.95f);

        Assert.NotNull(ev2);
        Assert.Equal(Navigation3DState.Orbit, engine.Active3DState);
        Assert.NotNull(ev2.SuggestedAction);
        Assert.Equal(ActionCommandType.MouseMoveRelativeWithWrap, ev2.SuggestedAction.Type);
        Assert.True(ev2.SuggestedAction.DeltaX > 0, "DeltaX should be positive for rightward motion");
        Assert.True(ev2.SuggestedAction.DeltaY < 0, "DeltaY should be negative for upward motion");
    }

    [Fact]
    public void ProcessFrame_BlenderProfile_FistRelease_EmitsOrbitEnd()
    {
        var blenderProfile = AppProfile.CreateBlenderProfile();
        var engine = new GestureEngine(blenderProfile);

        var t0 = DateTime.UtcNow;
        var fistPose = HandPoseGenerator.CreateFist();
        var fistFeatures = _extractor.ExtractFeatures(fistPose);

        // Enter Orbit
        engine.ProcessFrame(new HandPose(fistPose.Landmarks, fistPose.Handedness, fistPose.Confidence, t0), fistFeatures, HandGestureType.Fist, 0.95f);
        Assert.Equal(Navigation3DState.Orbit, engine.Active3DState);

        // Release Fist to LateralPalm
        var palmPose = HandPoseGenerator.CreateLateralPalm();
        var palmFeatures = _extractor.ExtractFeatures(palmPose);
        var releaseEv = engine.ProcessFrame(new HandPose(palmPose.Landmarks, palmPose.Handedness, palmPose.Confidence, t0.AddMilliseconds(33)), palmFeatures, HandGestureType.LateralPalm, 0.90f);

        Assert.NotNull(releaseEv);
        Assert.Equal(Navigation3DState.None, engine.Active3DState);
        Assert.NotNull(releaseEv.SuggestedAction);
        Assert.Equal(ActionCommandType.OrbitEnd, releaseEv.SuggestedAction.Type);
    }

    [Fact]
    public void ProcessFrame_BlenderProfile_TwoFingersPeace_EntersPanAndExitsPan()
    {
        var blenderProfile = AppProfile.CreateBlenderProfile();
        blenderProfile.DeadZoneRadius = 0.02f;
        var engine = new GestureEngine(blenderProfile);

        var t0 = DateTime.UtcNow;
        var peacePose = HandPoseGenerator.CreateTwoFingersPeace(wristX: 0.5f, wristY: 0.5f);
        var peaceFeatures = _extractor.ExtractFeatures(peacePose);

        // Frame 1: Enter Pan
        var ev1 = engine.ProcessFrame(new HandPose(peacePose.Landmarks, peacePose.Handedness, peacePose.Confidence, t0), peaceFeatures, HandGestureType.TwoFingersPeace, 0.95f);
        Assert.NotNull(ev1);
        Assert.Equal(Navigation3DState.Pan, engine.Active3DState);
        Assert.NotNull(ev1.SuggestedAction);
        Assert.Equal(ActionCommandType.PanStart, ev1.SuggestedAction.Type);

        // Frame 2: Move Pan
        var peacePose2 = HandPoseGenerator.CreateTwoFingersPeace(wristX: 0.55f, wristY: 0.55f);
        var peaceFeatures2 = _extractor.ExtractFeatures(peacePose2);
        var ev2 = engine.ProcessFrame(new HandPose(peacePose2.Landmarks, peacePose2.Handedness, peacePose2.Confidence, t0.AddMilliseconds(33)), peaceFeatures2, HandGestureType.TwoFingersPeace, 0.95f);
        Assert.NotNull(ev2);
        Assert.Equal(ActionCommandType.MouseMoveRelativeWithWrap, ev2.SuggestedAction?.Type);

        // Frame 3: Release Pan
        var palmPose = HandPoseGenerator.CreateLateralPalm();
        var palmFeatures = _extractor.ExtractFeatures(palmPose);
        var releaseEv = engine.ProcessFrame(new HandPose(palmPose.Landmarks, palmPose.Handedness, palmPose.Confidence, t0.AddMilliseconds(66)), palmFeatures, HandGestureType.LateralPalm, 0.90f);

        Assert.NotNull(releaseEv);
        Assert.Equal(Navigation3DState.None, engine.Active3DState);
        Assert.NotNull(releaseEv.SuggestedAction);
        Assert.Equal(ActionCommandType.PanEnd, releaseEv.SuggestedAction.Type);
    }

    [Fact]
    public void ProcessFrame_BlenderProfile_PalmDepthZoom_GeneratesScrollActions()
    {
        var blenderProfile = AppProfile.CreateBlenderProfile();
        blenderProfile.PalmDepthDeadZone = 0.01f;
        blenderProfile.ZoomDepthSensitivity = 1.5f;
        var engine = new GestureEngine(blenderProfile);

        var t0 = DateTime.UtcNow;
        var pose1 = HandPoseGenerator.CreateOpenHand();
        var features1 = _extractor.ExtractFeatures(pose1);

        // Frame 1: anchors baseline depth
        engine.ProcessFrame(new HandPose(pose1.Landmarks, pose1.Handedness, pose1.Confidence, t0), features1, HandGestureType.OpenHand, 0.95f);

        // Frame 2: scale palm up to simulate hand closer to camera
        var modifiedLandmarks = new HandLandmark[21];
        var wrist = pose1.Wrist;
        for (int i = 0; i < 21; i++)
        {
            var lm = pose1.Landmarks[i];
            float rx = (lm.X - wrist.X) * 1.4f + wrist.X;
            float ry = (lm.Y - wrist.Y) * 1.4f + wrist.Y;
            float rz = (lm.Z - wrist.Z) * 1.4f + wrist.Z - 0.08f;
            modifiedLandmarks[i] = new HandLandmark((HandLandmarkType)i, rx, ry, rz);
        }

        var closerPose = new HandPose(modifiedLandmarks, pose1.Handedness, pose1.Confidence, t0.AddMilliseconds(33));
        var closerFeatures = _extractor.ExtractFeatures(closerPose);

        var zoomEv = engine.ProcessFrame(closerPose, closerFeatures, HandGestureType.OpenHand, 0.95f);

        Assert.NotNull(zoomEv);
        Assert.Equal(Navigation3DState.Zoom, zoomEv.Navigation3D);
        Assert.NotNull(zoomEv.SuggestedAction);
        Assert.Equal(ActionCommandType.MouseScroll, zoomEv.SuggestedAction.Type);
        Assert.True(zoomEv.SuggestedAction.ScrollDelta > 0, "Zoom in should produce positive scroll delta");
    }

    [Fact]
    public void Reset_ClearsActive3DState()
    {
        var blenderProfile = AppProfile.CreateBlenderProfile();
        var engine = new GestureEngine(blenderProfile);

        var pose = HandPoseGenerator.CreateFist();
        var features = _extractor.ExtractFeatures(pose);

        engine.ProcessFrame(pose, features, HandGestureType.Fist, 0.95f);
        Assert.Equal(Navigation3DState.Orbit, engine.Active3DState);

        engine.Reset();
        Assert.Equal(Navigation3DState.None, engine.Active3DState);
    }
}
