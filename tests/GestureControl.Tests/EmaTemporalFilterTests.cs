using GestureControl.Core.Models;
using GestureControl.Gestures.Filters;
using GestureControl.Tests.TestHelpers;
using Xunit;

namespace GestureControl.Tests;

public class EmaTemporalFilterTests
{
    [Fact]
    public void Smooth_FirstFrame_PassesThroughIdentical()
    {
        var filter = new EmaTemporalFilter(alpha: 0.5f);
        var initialPose = HandPoseGenerator.CreateOpenHand();

        var smoothed = filter.Smooth(initialPose);

        Assert.Equal(initialPose.Wrist.X, smoothed.Wrist.X, precision: 4);
        Assert.Equal(initialPose.Wrist.Y, smoothed.Wrist.Y, precision: 4);
    }

    [Fact]
    public void Smooth_ZeroAllocationSpan_WorksWithoutHeapAllocation()
    {
        var filter = new EmaTemporalFilter(alpha: 0.5f);
        var initialPose = HandPoseGenerator.CreateOpenHand();

        Span<HandLandmark> destination = stackalloc HandLandmark[21];
        filter.Smooth(initialPose.AsSpan(), destination);

        Assert.Equal(initialPose.Wrist.X, destination[0].X, precision: 4);
        Assert.Equal(initialPose.Wrist.Y, destination[0].Y, precision: 4);
    }

    [Fact]
    public void Smooth_SuddenJitter_ReducesDisplacement()
    {
        var filter = new EmaTemporalFilter(alpha: 0.2f); // High smoothing
        var pose1 = HandPoseGenerator.CreateOpenHand(wristX: 0.5f, wristY: 0.5f);
        filter.Smooth(pose1);

        // Frame 2 has a sudden jitter spike to 0.7f
        var pose2 = HandPoseGenerator.CreateOpenHand(wristX: 0.7f, wristY: 0.5f);
        var smoothed2 = filter.Smooth(pose2);

        // EMA with alpha=0.2 should produce: 0.2 * 0.7 + 0.8 * 0.5 = 0.14 + 0.40 = 0.54
        Assert.True(smoothed2.Wrist.X < 0.60f, $"Expected smoothed X to be dampened, was {smoothed2.Wrist.X}");
        Assert.Equal(0.54f, smoothed2.Wrist.X, precision: 2);
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        var filter = new EmaTemporalFilter(alpha: 0.2f);
        var pose1 = HandPoseGenerator.CreateOpenHand(wristX: 0.5f, wristY: 0.5f);
        filter.Smooth(pose1);

        filter.Reset();

        // After reset, next frame acts as the first frame again
        var pose2 = HandPoseGenerator.CreateOpenHand(wristX: 0.8f, wristY: 0.5f);
        var smoothed2 = filter.Smooth(pose2);

        Assert.Equal(0.8f, smoothed2.Wrist.X, precision: 3);
    }
}
