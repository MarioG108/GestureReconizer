using System.Numerics;
using GestureControl.Core.Models;
using GestureControl.Gestures.Features;
using GestureControl.Tests.TestHelpers;
using Xunit;

namespace GestureControl.Tests;

public class PalmDepthEstimatorTests
{
    [Fact]
    public void ComputeRawDepthMetric_CalculatesConsistentScale()
    {
        var pose = HandPoseGenerator.CreateOpenHand();
        float metric = PalmDepthEstimator.ComputeRawDepthMetric(pose);

        Assert.True(metric > 0.05f, $"Metric should be positive and realistic, but was {metric}");
    }

    [Fact]
    public void TryComputeDepthDelta_FirstFrameEstablishesAnchor_ReturnsFalse()
    {
        var estimator = new PalmDepthEstimator(smoothingAlpha: 0.5f);
        var pose = HandPoseGenerator.CreateOpenHand();

        bool hasZoom = estimator.TryComputeDepthDelta(
            pose,
            dt: 0.033f,
            deadZone: 0.025f,
            sensitivity: 1.0f,
            out float zoomDelta);

        Assert.False(hasZoom);
        Assert.Equal(0f, zoomDelta);
        Assert.True(estimator.IsAnchored);
    }

    [Fact]
    public void TryComputeDepthDelta_DeadZone_SuppressesSmallMovements()
    {
        var estimator = new PalmDepthEstimator(smoothingAlpha: 1.0f); // Pure immediate response for testing
        var pose = HandPoseGenerator.CreateOpenHand();

        // Frame 1: anchor
        estimator.TryComputeDepthDelta(pose, 0.033f, 0.05f, 1.0f, out _);

        // Frame 2: tiny modification within 0.05 dead zone
        var modifiedLandmarks = pose.Landmarks.ToArray();
        // Shift middle MCP slightly by 0.005
        modifiedLandmarks[(int)HandLandmarkType.MiddleFingerMcp] = new HandLandmark(
            HandLandmarkType.MiddleFingerMcp,
            modifiedLandmarks[(int)HandLandmarkType.MiddleFingerMcp].X,
            modifiedLandmarks[(int)HandLandmarkType.MiddleFingerMcp].Y + 0.005f,
            modifiedLandmarks[(int)HandLandmarkType.MiddleFingerMcp].Z);

        var pose2 = new HandPose(modifiedLandmarks, pose.Handedness, pose.Confidence, pose.Timestamp.AddMilliseconds(33));

        bool hasZoom = estimator.TryComputeDepthDelta(pose2, 0.033f, 0.05f, 1.0f, out float zoomDelta);

        Assert.False(hasZoom);
        Assert.Equal(0f, zoomDelta);
    }

    [Fact]
    public void TryComputeDepthDelta_HandMovingCloser_TriggersPositiveZoomIn()
    {
        var estimator = new PalmDepthEstimator(smoothingAlpha: 0.8f);
        var pose = HandPoseGenerator.CreateOpenHand();

        // Frame 1: anchor
        estimator.TryComputeDepthDelta(pose, 0.033f, 0.02f, 1.0f, out _);

        // Frame 2: hand moved closer (palm scaled up by 1.3x and Z decreased)
        var modifiedLandmarks = new HandLandmark[21];
        var wrist = pose.Wrist;
        for (int i = 0; i < 21; i++)
        {
            var lm = pose.Landmarks[i];
            float rx = (lm.X - wrist.X) * 1.35f + wrist.X;
            float ry = (lm.Y - wrist.Y) * 1.35f + wrist.Y;
            float rz = (lm.Z - wrist.Z) * 1.35f + wrist.Z - 0.05f;
            modifiedLandmarks[i] = new HandLandmark((HandLandmarkType)i, rx, ry, rz);
        }

        var closerPose = new HandPose(modifiedLandmarks, pose.Handedness, pose.Confidence, pose.Timestamp.AddMilliseconds(33));

        bool hasZoom = estimator.TryComputeDepthDelta(closerPose, 0.033f, 0.02f, 1.0f, out float zoomDelta);

        Assert.True(hasZoom);
        Assert.True(zoomDelta > 0f, $"Expected positive zoomDelta (Zoom In) but got {zoomDelta}");
    }

    [Fact]
    public void TryComputeDepthDelta_HandMovingFurther_TriggersNegativeZoomOut()
    {
        var estimator = new PalmDepthEstimator(smoothingAlpha: 0.8f);
        var pose = HandPoseGenerator.CreateOpenHand();

        // Frame 1: anchor
        estimator.TryComputeDepthDelta(pose, 0.033f, 0.02f, 1.0f, out _);

        // Frame 2: hand moved further away (palm scaled down to 0.7x and Z increased)
        var modifiedLandmarks = new HandLandmark[21];
        var wrist = pose.Wrist;
        for (int i = 0; i < 21; i++)
        {
            var lm = pose.Landmarks[i];
            float rx = (lm.X - wrist.X) * 0.65f + wrist.X;
            float ry = (lm.Y - wrist.Y) * 0.65f + wrist.Y;
            float rz = (lm.Z - wrist.Z) * 0.65f + wrist.Z + 0.05f;
            modifiedLandmarks[i] = new HandLandmark((HandLandmarkType)i, rx, ry, rz);
        }

        var furtherPose = new HandPose(modifiedLandmarks, pose.Handedness, pose.Confidence, pose.Timestamp.AddMilliseconds(33));

        bool hasZoom = estimator.TryComputeDepthDelta(furtherPose, 0.033f, 0.02f, 1.0f, out float zoomDelta);

        Assert.True(hasZoom);
        Assert.True(zoomDelta < 0f, $"Expected negative zoomDelta (Zoom Out) but got {zoomDelta}");
    }

    [Fact]
    public void Reset_ClearsAnchorAndSmoothedDelta()
    {
        var estimator = new PalmDepthEstimator();
        var pose = HandPoseGenerator.CreateOpenHand();

        estimator.TryComputeDepthDelta(pose, 0.033f, 0.02f, 1.0f, out _);
        Assert.True(estimator.IsAnchored);

        estimator.Reset();
        Assert.False(estimator.IsAnchored);
        Assert.Equal(0f, estimator.SmoothedDelta);
    }
}
