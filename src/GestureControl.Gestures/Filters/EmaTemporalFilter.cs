using System.Numerics;
using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;

namespace GestureControl.Gestures.Filters;

/// <summary>
/// High-performance Exponential Moving Average (EMA) filter for 3D hand landmarks.
/// Eliminates high-frequency sensor jitter while maintaining low-latency response.
/// Optimized with zero heap allocations using Spans.
/// </summary>
public class EmaTemporalFilter : ITemporalFilter
{
    private readonly Vector3[] _history = new Vector3[21];
    private bool _hasHistory = false;

    public float Alpha { get; set; }

    public EmaTemporalFilter(float alpha = 0.5f)
    {
        Alpha = Math.Clamp(alpha, 0.01f, 1.0f);
    }

    /// <summary>
    /// Smooths the 21 landmarks into the destination span without any heap allocations.
    /// </summary>
    public void Smooth(ReadOnlySpan<HandLandmark> current, Span<HandLandmark> destination)
    {
        if (current.Length != 21 || destination.Length < 21)
        {
            throw new ArgumentException("Landmark spans must be of length 21.");
        }

        if (!_hasHistory)
        {
            for (int i = 0; i < 21; i++)
            {
                _history[i] = current[i].AsVector3();
                destination[i] = current[i];
            }
            _hasHistory = true;
            return;
        }

        float a = Alpha;
        float oneMinusA = 1.0f - a;

        for (int i = 0; i < 21; i++)
        {
            Vector3 raw = current[i].AsVector3();
            // EMA: S_t = a * X_t + (1 - a) * S_{t-1}
            Vector3 smoothed = (raw * a) + (_history[i] * oneMinusA);
            _history[i] = smoothed;

            destination[i] = new HandLandmark(
                current[i].Type,
                smoothed.X,
                smoothed.Y,
                smoothed.Z,
                current[i].Visibility);
        }
    }

    /// <summary>
    /// Smooths a HandPose using a stack-allocated buffer, allocating only the final HandPose instance.
    /// </summary>
    public HandPose Smooth(HandPose rawPose)
    {
        Span<HandLandmark> smoothed = stackalloc HandLandmark[21];
        Smooth(rawPose.AsSpan(), smoothed);
        return new HandPose(smoothed, rawPose.Handedness, rawPose.Confidence, rawPose.Timestamp);
    }

    public void Reset()
    {
        _hasHistory = false;
        Array.Clear(_history, 0, _history.Length);
    }
}
