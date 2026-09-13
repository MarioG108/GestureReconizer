using System.Numerics;

namespace GestureControl.Core.Models;

/// <summary>
/// Derived geometric and kinematic features computed from 3D hand landmarks.
/// Step 5 in Section 8.2 of the technical pipeline.
/// </summary>
public record HandFeatures(
    bool IsThumbExtended,
    bool IsIndexExtended,
    bool IsMiddleExtended,
    bool IsRingExtended,
    bool IsPinkyExtended,
    float PinchDistance,
    float PinchMiddleDistance,
    float HandSpan,
    Vector3 PalmNormal,
    Vector2 Velocity,
    float Speed)
{
    public int ExtendedFingerCount =>
        (IsThumbExtended ? 1 : 0) +
        (IsIndexExtended ? 1 : 0) +
        (IsMiddleExtended ? 1 : 0) +
        (IsRingExtended ? 1 : 0) +
        (IsPinkyExtended ? 1 : 0);

    public bool AreOnlyIndexAndMiddleExtended =>
        !IsThumbExtended && IsIndexExtended && IsMiddleExtended && !IsRingExtended && !IsPinkyExtended;

    public bool IsOnlyIndexExtended =>
        !IsThumbExtended && IsIndexExtended && !IsMiddleExtended && !IsRingExtended && !IsPinkyExtended;

    public bool AreAllFingersExtended =>
        IsThumbExtended && IsIndexExtended && IsMiddleExtended && IsRingExtended && IsPinkyExtended;

    public bool AreAllFingersFolded =>
        !IsIndexExtended && !IsMiddleExtended && !IsRingExtended && !IsPinkyExtended;
}
