using System.Numerics;
using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;

namespace GestureControl.Gestures.Classifiers;

/// <summary>
/// Rule-based dynamic gesture classifier that detects directional swipes (Left, Right, Up, Down)
/// from hand trajectory history in real time.
/// </summary>
public sealed class DynamicGestureClassifier : IDynamicGestureClassifier
{
    public float MinSwipeDistance { get; set; } = 0.15f;
    public float MinSwipeVelocity { get; set; } = 0.40f;

    public DynamicGestureClassifier(float minDistance = 0.15f, float minVelocity = 0.40f)
    {
        MinSwipeDistance = minDistance;
        MinSwipeVelocity = minVelocity;
    }

    public HandGestureType ClassifyTrajectory(ReadOnlySpan<HandTrajectoryPoint> history, out float confidence)
    {
        confidence = 0f;
        if (history.Length < 3)
        {
            return HandGestureType.None;
        }

        ref readonly var oldest = ref history[0];
        ref readonly var newest = ref history[^1];

        double dt = (newest.Timestamp - oldest.Timestamp).TotalSeconds;
        // Swipes must occur within an ergonomic window (80ms to 550ms)
        if (dt < 0.06 || dt > 0.55)
        {
            return HandGestureType.None;
        }

        float dx = newest.X - oldest.X;
        float dy = newest.Y - oldest.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float speed = dist / (float)dt;

        if (dist < MinSwipeDistance || speed < MinSwipeVelocity)
        {
            return HandGestureType.None;
        }

        // Directional dominance check (prevent diagonal false positives)
        float absX = MathF.Abs(dx);
        float absY = MathF.Abs(dy);

        HandGestureType detected;
        if (absX >= absY * 1.3f)
        {
            // Horizontal swipe
            detected = dx < 0 ? HandGestureType.SwipeLeft : HandGestureType.SwipeRight;
        }
        else if (absY >= absX * 1.3f)
        {
            // Vertical swipe (in screen coords, Y decreases upwards)
            detected = dy < 0 ? HandGestureType.SwipeUp : HandGestureType.SwipeDown;
        }
        else
        {
            // Diagonal / ambiguous gesture -> reject
            return HandGestureType.None;
        }

        confidence = Math.Clamp(0.60f + (speed / (MinSwipeVelocity * 2.0f)) * 0.35f, 0.60f, 0.98f);
        return detected;
    }

    public void Reset()
    {
        // Stateless evaluation over history span
    }
}
