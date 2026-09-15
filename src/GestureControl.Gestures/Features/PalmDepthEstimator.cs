using System.Numerics;
using GestureControl.Core.Models;

namespace GestureControl.Gestures.Features;

/// <summary>
/// Estimates continuous relative 3D depth and zoom velocity from anatomical palm dimensions
/// (wrist to knuckle span) combined with Landmark Z coordinates, filtered through an EMA smoother.
/// Guarantees zero heap allocation per frame.
/// </summary>
public sealed class PalmDepthEstimator
{
    private float? _anchorMetric;
    private float _smoothedDelta = 0f;
    private readonly float _smoothingAlpha;

    public bool IsAnchored => _anchorMetric.HasValue;
    public float SmoothedDelta => _smoothedDelta;

    public PalmDepthEstimator(float smoothingAlpha = 0.35f)
    {
        _smoothingAlpha = Math.Clamp(smoothingAlpha, 0.05f, 0.95f);
    }

    /// <summary>
    /// Computes the instantaneous anatomical palm scale and optical depth metric.
    /// </summary>
    public static float ComputeRawDepthMetric(HandPose pose)
    {
        var wrist = pose.Wrist;
        var middleMcp = pose.MiddleMcp;
        var indexMcp = pose.IndexMcp;
        var pinkyMcp = pose.PinkyMcp;

        // 1. Longitudinal hand span (Wrist to Middle MCP)
        float dxLong = middleMcp.X - wrist.X;
        float dyLong = middleMcp.Y - wrist.Y;
        float dzLong = middleMcp.Z - wrist.Z;
        float distLong = MathF.Sqrt(dxLong * dxLong + dyLong * dyLong + dzLong * dzLong);

        // 2. Transverse palm span (Index MCP to Pinky MCP)
        float dxTrans = pinkyMcp.X - indexMcp.X;
        float dyTrans = pinkyMcp.Y - indexMcp.Y;
        float dzTrans = pinkyMcp.Z - indexMcp.Z;
        float distTrans = MathF.Sqrt(dxTrans * dxTrans + dyTrans * dyTrans + dzTrans * dzTrans);

        // 3. Composite optical palm size
        float palmScale = (distLong + distTrans) * 0.5f;

        // 4. Combined metric: larger palm scale or smaller Z means closer to camera
        return palmScale - (wrist.Z * 0.30f);
    }

    /// <summary>
    /// Updates the depth estimator with the current hand pose.
    /// Returns true if a significant zoom delta outside the deadzone was produced.
    /// </summary>
    public bool TryComputeDepthDelta(
        HandPose pose,
        float dt,
        float deadZone,
        float sensitivity,
        out float zoomDelta)
    {
        zoomDelta = 0f;
        float currentMetric = ComputeRawDepthMetric(pose);

        if (!_anchorMetric.HasValue)
        {
            _anchorMetric = currentMetric;
            _smoothedDelta = 0f;
            return false;
        }

        // Relative change from neutral anchor
        float rawDelta = currentMetric - _anchorMetric.Value;

        // Temporal EMA smoothing
        _smoothedDelta = (_smoothingAlpha * rawDelta) + ((1.0f - _smoothingAlpha) * _smoothedDelta);

        // Deadzone thresholding
        float absDelta = MathF.Abs(_smoothedDelta);
        if (absDelta <= deadZone)
        {
            return false;
        }

        // Exceeded dead zone: calculate directional zoom speed
        float excess = _smoothedDelta > 0
            ? _smoothedDelta - deadZone
            : _smoothedDelta + deadZone;

        // Positive delta = hand closer = Zoom In
        // Negative delta = hand further = Zoom Out
        float zoomVelocity = Math.Clamp(excess * 12.0f, -1.0f, 1.0f);
        zoomDelta = zoomVelocity * sensitivity * Math.Clamp(dt * 30.0f, 0.1f, 2.0f);

        return MathF.Abs(zoomDelta) > 0.0001f;
    }

    /// <summary>
    /// Calibrates the current hand pose as the neutral resting anchor.
    /// </summary>
    public void CalibrateAnchor(HandPose pose)
    {
        _anchorMetric = ComputeRawDepthMetric(pose);
        _smoothedDelta = 0f;
    }

    /// <summary>
    /// Resets the anchor and smoothing history.
    /// </summary>
    public void Reset()
    {
        _anchorMetric = null;
        _smoothedDelta = 0f;
    }
}
