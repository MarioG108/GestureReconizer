using System.Numerics;
using GestureControl.Core.Models;
using GestureControl.Gestures.Classifiers;
using Xunit;

namespace GestureControl.Tests;

public class DynamicGestureClassifierTests
{
    private readonly DynamicGestureClassifier _classifier = new(minDistance: 0.15f, minVelocity: 0.40f);

    [Fact]
    public void ClassifyTrajectory_WhenMovingFastLeft_ReturnsSwipeLeft()
    {
        var now = DateTime.UtcNow;
        var points = new HandTrajectoryPoint[]
        {
            new(new Vector3(0.70f, 0.50f, 0.0f), now, Vector2.Zero),
            new(new Vector3(0.55f, 0.50f, 0.0f), now.AddMilliseconds(70), new Vector2(-2.1f, 0)),
            new(new Vector3(0.40f, 0.50f, 0.0f), now.AddMilliseconds(140), new Vector2(-2.1f, 0)),
            new(new Vector3(0.30f, 0.50f, 0.0f), now.AddMilliseconds(200), new Vector2(-2.1f, 0))
        };

        var gesture = _classifier.ClassifyTrajectory(points.AsSpan(), out float confidence);

        Assert.Equal(HandGestureType.SwipeLeft, gesture);
        Assert.True(confidence >= 0.60f, $"Confidence should be >= 0.60, got {confidence}");
    }

    [Fact]
    public void ClassifyTrajectory_WhenMovingFastRight_ReturnsSwipeRight()
    {
        var now = DateTime.UtcNow;
        var points = new HandTrajectoryPoint[]
        {
            new(new Vector3(0.30f, 0.50f, 0.0f), now, Vector2.Zero),
            new(new Vector3(0.45f, 0.50f, 0.0f), now.AddMilliseconds(70), new Vector2(2.1f, 0)),
            new(new Vector3(0.60f, 0.50f, 0.0f), now.AddMilliseconds(140), new Vector2(2.1f, 0)),
            new(new Vector3(0.70f, 0.50f, 0.0f), now.AddMilliseconds(200), new Vector2(2.1f, 0))
        };

        var gesture = _classifier.ClassifyTrajectory(points.AsSpan(), out float confidence);

        Assert.Equal(HandGestureType.SwipeRight, gesture);
        Assert.True(confidence >= 0.60f, $"Confidence should be >= 0.60, got {confidence}");
    }

    [Fact]
    public void ClassifyTrajectory_WhenMovingFastUp_ReturnsSwipeUp()
    {
        var now = DateTime.UtcNow;
        // In screen coords, Y decreases upwards
        var points = new HandTrajectoryPoint[]
        {
            new(new Vector3(0.50f, 0.70f, 0.0f), now, Vector2.Zero),
            new(new Vector3(0.50f, 0.55f, 0.0f), now.AddMilliseconds(70), new Vector2(0, -2.1f)),
            new(new Vector3(0.50f, 0.40f, 0.0f), now.AddMilliseconds(140), new Vector2(0, -2.1f)),
            new(new Vector3(0.50f, 0.30f, 0.0f), now.AddMilliseconds(200), new Vector2(0, -2.1f))
        };

        var gesture = _classifier.ClassifyTrajectory(points.AsSpan(), out float confidence);

        Assert.Equal(HandGestureType.SwipeUp, gesture);
        Assert.True(confidence >= 0.60f, $"Confidence should be >= 0.60, got {confidence}");
    }

    [Fact]
    public void ClassifyTrajectory_WhenMovingFastDown_ReturnsSwipeDown()
    {
        var now = DateTime.UtcNow;
        // In screen coords, Y increases downwards
        var points = new HandTrajectoryPoint[]
        {
            new(new Vector3(0.50f, 0.30f, 0.0f), now, Vector2.Zero),
            new(new Vector3(0.50f, 0.45f, 0.0f), now.AddMilliseconds(70), new Vector2(0, 2.1f)),
            new(new Vector3(0.50f, 0.60f, 0.0f), now.AddMilliseconds(140), new Vector2(0, 2.1f)),
            new(new Vector3(0.50f, 0.70f, 0.0f), now.AddMilliseconds(200), new Vector2(0, 2.1f))
        };

        var gesture = _classifier.ClassifyTrajectory(points.AsSpan(), out float confidence);

        Assert.Equal(HandGestureType.SwipeDown, gesture);
        Assert.True(confidence >= 0.60f, $"Confidence should be >= 0.60, got {confidence}");
    }

    [Fact]
    public void ClassifyTrajectory_WhenMovementTooSlow_ReturnsNone()
    {
        var now = DateTime.UtcNow;
        // Moved 0.4 distance, but took 1.5 seconds (speed ~0.26 u/s < 0.40 u/s threshold and dt > 0.55s)
        var points = new HandTrajectoryPoint[]
        {
            new(new Vector3(0.30f, 0.50f, 0.0f), now, Vector2.Zero),
            new(new Vector3(0.50f, 0.50f, 0.0f), now.AddMilliseconds(750), new Vector2(0.26f, 0)),
            new(new Vector3(0.70f, 0.50f, 0.0f), now.AddMilliseconds(1500), new Vector2(0.26f, 0))
        };

        var gesture = _classifier.ClassifyTrajectory(points.AsSpan(), out _);

        Assert.Equal(HandGestureType.None, gesture);
    }

    [Fact]
    public void ClassifyTrajectory_WhenDistanceTooSmall_ReturnsNone()
    {
        var now = DateTime.UtcNow;
        // Distance only 0.05 (< 0.15 threshold)
        var points = new HandTrajectoryPoint[]
        {
            new(new Vector3(0.50f, 0.50f, 0.0f), now, Vector2.Zero),
            new(new Vector3(0.52f, 0.50f, 0.0f), now.AddMilliseconds(50), new Vector2(0.4f, 0)),
            new(new Vector3(0.55f, 0.50f, 0.0f), now.AddMilliseconds(100), new Vector2(0.4f, 0))
        };

        var gesture = _classifier.ClassifyTrajectory(points.AsSpan(), out _);

        Assert.Equal(HandGestureType.None, gesture);
    }

    [Fact]
    public void ClassifyTrajectory_WhenMovementIsDiagonal_RejectsAmbiguous()
    {
        var now = DateTime.UtcNow;
        // Equal displacement in X and Y (45 degrees)
        var points = new HandTrajectoryPoint[]
        {
            new(new Vector3(0.20f, 0.20f, 0.0f), now, Vector2.Zero),
            new(new Vector3(0.35f, 0.35f, 0.0f), now.AddMilliseconds(100), new Vector2(1.5f, 1.5f)),
            new(new Vector3(0.50f, 0.50f, 0.0f), now.AddMilliseconds(200), new Vector2(1.5f, 1.5f))
        };

        var gesture = _classifier.ClassifyTrajectory(points.AsSpan(), out _);

        Assert.Equal(HandGestureType.None, gesture);
    }
}
