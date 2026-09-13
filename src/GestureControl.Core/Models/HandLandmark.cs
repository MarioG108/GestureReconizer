using System.Numerics;

namespace GestureControl.Core.Models;

/// <summary>
/// Represents a single 3D point landmark of a detected hand.
/// Normalized coordinates: X and Y are in [0.0, 1.0] relative to frame dimensions.
/// Z represents depth relative to the wrist (smaller is closer to the camera).
/// </summary>
public readonly record struct HandLandmark(
    HandLandmarkType Type,
    float X,
    float Y,
    float Z,
    float Visibility = 1.0f)
{
    public Vector3 AsVector3() => new(X, Y, Z);

    public float DistanceTo(HandLandmark other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        float dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public float Distance2DTo(HandLandmark other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
