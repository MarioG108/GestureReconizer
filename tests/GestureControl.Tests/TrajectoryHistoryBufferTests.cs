using System.Numerics;
using GestureControl.Core.Models;
using GestureControl.Gestures.Classifiers;
using Xunit;

namespace GestureControl.Tests;

public class TrajectoryHistoryBufferTests
{
    [Fact]
    public void AddSample_WrapsAroundCorrectlyWhenExceedingCapacity()
    {
        var buffer = new TrajectoryHistoryBuffer(capacity: 8);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            buffer.AddSample(new Vector3(i * 0.1f, 0, 0), baseTime.AddMilliseconds(i * 30));
        }

        Assert.Equal(8, buffer.Count);

        var dest = new HandTrajectoryPoint[8];
        int copied = buffer.CopyTo(dest);
        Assert.Equal(8, copied);

        // The last point should be the 14th point (X = 1.4)
        Assert.Equal(1.4f, dest[^1].Position.X, precision: 3);
        // The oldest point in the window should be the 7th point (X = 0.7)
        Assert.Equal(0.7f, dest[0].Position.X, precision: 3);
    }

    [Fact]
    public void TryGetWindowMetrics_ComputesNetDisplacementAndAverageVelocity()
    {
        var buffer = new TrajectoryHistoryBuffer(capacity: 10);
        var baseTime = DateTime.UtcNow;

        buffer.AddSample(new Vector3(0.1f, 0.2f, 0), baseTime);
        buffer.AddSample(new Vector3(0.3f, 0.2f, 0), baseTime.AddMilliseconds(100));
        buffer.AddSample(new Vector3(0.5f, 0.2f, 0), baseTime.AddMilliseconds(200));

        bool ok = buffer.TryGetWindowMetrics(
            window: TimeSpan.FromSeconds(0.30),
            out var displacement,
            out float elapsedSec,
            out float avgSpeed,
            out int samplesInWindow);

        Assert.True(ok);
        Assert.Equal(0.4f, displacement.X, precision: 3);
        Assert.Equal(0.0f, displacement.Y, precision: 3);
        Assert.Equal(3, samplesInWindow);
        Assert.True(avgSpeed > 1.8f && avgSpeed < 2.2f, $"Expected ~2.0 u/s, got {avgSpeed}");
        Assert.True(elapsedSec >= 0.19f && elapsedSec <= 0.21f);
    }
}
