using System.Numerics;

namespace GestureControl.Core.Models;

/// <summary>
/// Lightweight structure representing a point in a hand trajectory over time.
/// Used by dynamic gesture classifiers to detect directional swipes, speed, and acceleration.
/// </summary>
public readonly record struct HandTrajectoryPoint(
    Vector3 Position,
    DateTime Timestamp,
    Vector2 Velocity)
{
    public float X => Position.X;
    public float Y => Position.Y;
    public float Z => Position.Z;
}
