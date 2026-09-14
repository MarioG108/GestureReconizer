using GestureControl.Vision.Camera;
using GestureControl.Vision.Tracking;
using Xunit;

namespace GestureControl.Tests;

public class VisionTrackerTests
{
    [Fact]
    public void OnnxHandTracker_WhenModelAbsent_ReturnsEmptyHands()
    {
        using var tracker = new OnnxHandTracker("non_existent_model.onnx");
        Assert.False(tracker.IsModelLoaded);

        var dummyFrame = new byte[640 * 480 * 3];
        var hands = tracker.TrackHands(dummyFrame, 640, 480, 640 * 3);

        // Crucial safety guarantee: Absent model must NEVER emit phantom hands or clicks
        Assert.Empty(hands);
    }

    [Fact]
    public void OnnxHandTracker_RealModel_EmptyFrame_DetectsZeroHands()
    {
        using var tracker = new OnnxHandTracker();
        Assert.True(tracker.IsModelLoaded, "Model hand_landmark.onnx should be discovered and loaded");

        var blackFrame = new byte[640 * 480 * 3];
        var hands = tracker.TrackHands(blackFrame, 640, 480, 640 * 3);

        // Empty frame has presence confidence ~0.03%, strictly rejected
        Assert.Empty(hands);
    }

    [Fact]
    public void OnnxHandTracker_GenerateSimulatedPose_ProducesValid21Landmarks()
    {
        using var tracker = new OnnxHandTracker();
        var pose = tracker.GenerateSimulatedPose(640, 480);

        Assert.Equal(21, pose.Landmarks.Count);
        Assert.True(pose.Confidence > 0.8f);
    }

    [Fact]
    public async Task OpenCvCameraService_SimulatedCapture_EmitsFrames()
    {
        await using var camera = new OpenCvCameraService();
        int frameCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        camera.FrameCaptured += (_, e) =>
        {
            frameCount++;
            if (frameCount >= 3)
            {
                tcs.TrySetResult(true);
            }
        };

        // Start with invalid index -1 to force simulated camera
        await camera.StartAsync(cameraIndex: -1, width: 320, height: 240, fps: 30);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        await camera.StopAsync();

        Assert.Equal(tcs.Task, completed);
        Assert.True(frameCount >= 3, $"Expected at least 3 frames, got {frameCount}");
    }


}
