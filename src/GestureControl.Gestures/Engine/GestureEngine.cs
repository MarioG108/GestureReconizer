using System.Numerics;
using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;
using GestureControl.Gestures.Classifiers;
using GestureControl.Gestures.Features;

namespace GestureControl.Gestures.Engine;

/// <summary>
/// Intent and Operational Safety Engine.
/// Step 7 in Section 8.2 & Section 8.3 of the technical architecture.
/// Handles:
/// - Activation / Deactivation toggle
/// - Minimum hold duration (anti-glitch / anti-bounce)
/// - Cooldown timers per gesture/command
/// - Neutral dead-zones for continuous movement
/// - 3D Spatial Viewport Navigation (Orbit, Pan, Zoom) with Infinite Cursor Wrapping
/// </summary>
public class GestureEngine : IGestureEngine
{
    private HandGestureType _candidateGesture = HandGestureType.None;
    private DateTime _candidateStartTime = DateTime.MinValue;
    private DateTime _lastTriggerTime = DateTime.MinValue;
    private DateTime _lastSwipeTriggerTime = DateTime.MinValue;
    private HandGestureType _lastTriggeredGesture = HandGestureType.None;
    private Vector2? _neutralAnchorPosition = null;
    private Vector2? _mouseAnchorPosition = null;
    private DateTime _clickFreezeUntil = DateTime.MinValue;

    private readonly TrajectoryHistoryBuffer _trajectoryBuffer = new(32);
    private readonly DynamicGestureClassifier _dynamicClassifier = new();
    private readonly HandTrajectoryPoint[] _tempTrajectoryArray = new HandTrajectoryPoint[32];
    private readonly PalmDepthEstimator _palmDepthEstimator = new();

    private Navigation3DState _currentNav3DState = Navigation3DState.None;
    private DateTime _lastFrameTime = DateTime.MinValue;

    public bool IsActive { get; set; } = true;
    
    private AppProfile _currentProfile;
    public AppProfile CurrentProfile
    {
        get => _currentProfile;
        set
        {
            _currentProfile = value;
            EnableMouseTracking = value.EnableMouseTracking;
        }
    }

    public float? DeadZoneRadiusOverride { get; set; }
    public int? HoldDurationMsOverride { get; set; }
    public float? MinSwipeDistanceOverride { get; set; }
    public float? MinSwipeVelocityOverride { get; set; }

    public Navigation3DState Active3DState => _currentNav3DState;
    public PalmDepthEstimator DepthEstimator => _palmDepthEstimator;

    public event EventHandler<bool>? MouseTrackingStateChanged;
    private bool _enableMouseTracking = true;
    public bool EnableMouseTracking
    {
        get => _enableMouseTracking;
        set
        {
            if (_enableMouseTracking != value)
            {
                _enableMouseTracking = value;
                if (!_enableMouseTracking)
                {
                    _mouseAnchorPosition = null;
                }
                MouseTrackingStateChanged?.Invoke(this, value);
            }
        }
    }

    public GestureEngine(AppProfile? initialProfile = null)
    {
        _currentProfile = initialProfile ?? AppProfile.CreateDefaultGlobal();
        _enableMouseTracking = _currentProfile.EnableMouseTracking;
    }

    public GestureEvent? ProcessFrame(
        HandPose pose,
        HandFeatures features,
        HandGestureType rawGesture,
        float confidence)
    {
        var now = pose.Timestamp;
        float dt = _lastFrameTime > DateTime.MinValue ? (float)(now - _lastFrameTime).TotalSeconds : 0.033f;
        _lastFrameTime = now;

        // If inactive, only listen for activation gesture (OpenHand held for 1 second)
        if (!IsActive)
        {
            if (rawGesture == HandGestureType.OpenHand)
            {
                if (_candidateGesture != HandGestureType.OpenHand)
                {
                    _candidateGesture = HandGestureType.OpenHand;
                    _candidateStartTime = now;
                }
                else if ((now - _candidateStartTime).TotalMilliseconds >= 1000)
                {
                    IsActive = true;
                    _candidateGesture = HandGestureType.None;
                    _lastTriggerTime = now;
                    return new GestureEvent(
                        Gesture: HandGestureType.OpenHand,
                        Confidence: 1.0f,
                        Pose: pose,
                        SuggestedAction: ActionCommand.None,
                        Timestamp: now,
                        IsConfirmed: true);
                }
            }
            else
            {
                _candidateGesture = HandGestureType.None;
            }

            return null;
        }

        // --- 3D Spatial Navigation State Transitions ---
        if (CurrentProfile.Enable3DNavigation)
        {
            // Transition out of Orbit
            if (_currentNav3DState == Navigation3DState.Orbit && rawGesture != HandGestureType.Fist)
            {
                _currentNav3DState = Navigation3DState.None;
                _neutralAnchorPosition = null;
                _palmDepthEstimator.Reset();
                var exitAction = CurrentProfile.EnableInfiniteCursorWrap
                    ? ActionCommand.OrbitEnd()
                    : ActionCommand.MiddleUp();
                return new GestureEvent(HandGestureType.Fist, confidence, pose, exitAction, now, true, Navigation3DState.None);
            }

            // Transition out of Pan
            if (_currentNav3DState == Navigation3DState.Pan && rawGesture != HandGestureType.TwoFingersPeace)
            {
                _currentNav3DState = Navigation3DState.None;
                _neutralAnchorPosition = null;
                _palmDepthEstimator.Reset();
                return new GestureEvent(HandGestureType.TwoFingersPeace, confidence, pose, ActionCommand.PanEnd(), now, true, Navigation3DState.None);
            }

            // 1. Orbit Mode (Fist held)
            if (rawGesture == HandGestureType.Fist)
            {
                var palmCenter = pose.PalmCenter;
                var currentPos = new Vector2(palmCenter.X, palmCenter.Y);

                if (_currentNav3DState != Navigation3DState.Orbit)
                {
                    _currentNav3DState = Navigation3DState.Orbit;
                    _neutralAnchorPosition = currentPos;
                    _palmDepthEstimator.Reset();
                    var enterAction = CurrentProfile.EnableInfiniteCursorWrap
                        ? ActionCommand.OrbitStart()
                        : ActionCommand.MiddleDown();
                    return new GestureEvent(HandGestureType.Fist, confidence, pose, enterAction, now, true, Navigation3DState.Orbit);
                }

                if (!_neutralAnchorPosition.HasValue)
                {
                    _neutralAnchorPosition = currentPos;
                }

                Vector2 diff = currentPos - _neutralAnchorPosition.Value;
                float dist = diff.Length();
                float effectiveDeadZone = DeadZoneRadiusOverride ?? CurrentProfile.DeadZoneRadius;
                ActionCommand? orbitMoveAction = null;

                if (dist > effectiveDeadZone)
                {
                    float dx = diff.X * CurrentProfile.OrbitSensitivity * 1920f;
                    float dy = diff.Y * CurrentProfile.OrbitSensitivity * 1080f;
                    orbitMoveAction = CurrentProfile.EnableInfiniteCursorWrap
                        ? ActionCommand.MoveMouseWithWrap(dx, dy)
                        : ActionCommand.MoveMouse(dx, dy);
                    _neutralAnchorPosition = currentPos;
                }

                return new GestureEvent(HandGestureType.Fist, confidence, pose, orbitMoveAction, now, true, Navigation3DState.Orbit);
            }

            // 2. Pan Mode (TwoFingersPeace held)
            if (rawGesture == HandGestureType.TwoFingersPeace)
            {
                var palmCenter = pose.PalmCenter;
                var currentPos = new Vector2(palmCenter.X, palmCenter.Y);

                if (_currentNav3DState != Navigation3DState.Pan)
                {
                    _currentNav3DState = Navigation3DState.Pan;
                    _neutralAnchorPosition = currentPos;
                    _palmDepthEstimator.Reset();
                    return new GestureEvent(HandGestureType.TwoFingersPeace, confidence, pose, ActionCommand.PanStart(), now, true, Navigation3DState.Pan);
                }

                if (!_neutralAnchorPosition.HasValue)
                {
                    _neutralAnchorPosition = currentPos;
                }

                Vector2 diff = currentPos - _neutralAnchorPosition.Value;
                float dist = diff.Length();
                float effectiveDeadZone = DeadZoneRadiusOverride ?? CurrentProfile.DeadZoneRadius;
                ActionCommand? panMoveAction = null;

                if (dist > effectiveDeadZone)
                {
                    float dx = diff.X * CurrentProfile.PanSensitivity * 1920f;
                    float dy = diff.Y * CurrentProfile.PanSensitivity * 1080f;
                    panMoveAction = CurrentProfile.EnableInfiniteCursorWrap
                        ? ActionCommand.MoveMouseWithWrap(dx, dy)
                        : ActionCommand.MoveMouse(dx, dy);
                    _neutralAnchorPosition = currentPos;
                }

                return new GestureEvent(HandGestureType.TwoFingersPeace, confidence, pose, panMoveAction, now, true, Navigation3DState.Pan);
            }

            // 3. Continuous Zoom Mode via Optical Palm Depth (OpenHand / LateralPalm)
            if (rawGesture == HandGestureType.OpenHand || rawGesture == HandGestureType.LateralPalm)
            {
                float deadZone = CurrentProfile.PalmDepthDeadZone;
                float sensitivity = CurrentProfile.ZoomDepthSensitivity;
                if (_palmDepthEstimator.TryComputeDepthDelta(pose, dt, deadZone, sensitivity, out float zoomDelta))
                {
                    int scrollTicks = (int)MathF.Round(zoomDelta * 120f);
                    if (scrollTicks != 0)
                    {
                        return new GestureEvent(
                            rawGesture,
                            confidence,
                            pose,
                            ActionCommand.Scroll(scrollTicks),
                            now,
                            true,
                            Navigation3DState.Zoom);
                    }
                }
            }
            else
            {
                _palmDepthEstimator.Reset();
            }
        }

        // Update palm trajectory history for dynamic gestures
        var currentPalm = pose.PalmCenter;
        _trajectoryBuffer.AddSample(new Vector3(currentPalm.X, currentPalm.Y, currentPalm.Z), now);

        // 1. Evaluate Dynamic Gestures (Swipes)
        if (rawGesture != HandGestureType.Fist && rawGesture != HandGestureType.IndexPoint)
        {
            _dynamicClassifier.MinSwipeDistance = MinSwipeDistanceOverride ?? CurrentProfile.MinSwipeDistance;
            _dynamicClassifier.MinSwipeVelocity = MinSwipeVelocityOverride ?? CurrentProfile.MinSwipeVelocity;

            int count = _trajectoryBuffer.CopyTo(_tempTrajectoryArray);
            if (count >= 3)
            {
                var dynGesture = _dynamicClassifier.ClassifyTrajectory(_tempTrajectoryArray.AsSpan(0, count), out float dynConfidence);
                if (dynGesture != HandGestureType.None)
                {
                    if ((now - _lastSwipeTriggerTime).TotalMilliseconds >= CurrentProfile.SwipeCooldownMs)
                    {
                        _lastSwipeTriggerTime = now;
                        _lastTriggerTime = now;
                        _trajectoryBuffer.Clear();
                        _candidateGesture = HandGestureType.None;

                        CurrentProfile.GestureBindings.TryGetValue(dynGesture, out var swipeAction);
                        return new GestureEvent(
                            Gesture: dynGesture,
                            Confidence: dynConfidence,
                            Pose: pose,
                            SuggestedAction: swipeAction,
                            Timestamp: now,
                            IsConfirmed: true);
                    }
                }
            }
        }

        // 2. Continuous Cursor Movement (PalmCenter with Clutch & Click Freeze)
        ActionCommand? moveAction = null;
        if (EnableMouseTracking && _currentNav3DState == Navigation3DState.None)
        {
            if (rawGesture == HandGestureType.LateralPalm)
            {
                // Clutch active: pause movement and reset anchor to allow repositioning hand
                _mouseAnchorPosition = null;
            }
            else
            {
                // Click Freeze handling: suppress jitter during Pinch / TwoFingersPeace
                if (rawGesture == HandGestureType.Pinch || rawGesture == HandGestureType.TwoFingersPeace)
                {
                    if (_candidateGesture != rawGesture)
                    {
                        _clickFreezeUntil = now.AddMilliseconds(100);
                    }
                }

                var palm = pose.PalmCenter;
                var currentPos = new Vector2(palm.X, palm.Y);

                if (now < _clickFreezeUntil)
                {
                    // During freeze, keep anchor updated to current position so there is no post-freeze jump
                    _mouseAnchorPosition = currentPos;
                }
                else
                {
                    if (!_mouseAnchorPosition.HasValue)
                    {
                        _mouseAnchorPosition = currentPos;
                    }

                    Vector2 diff = currentPos - _mouseAnchorPosition.Value;
                    float dist = diff.Length();
                    float effectiveDeadZone = DeadZoneRadiusOverride ?? CurrentProfile.DeadZoneRadius;

                    if (dist > effectiveDeadZone)
                    {
                        // Hysteresis deadband: advance anchor to boundary of deadzone sphere.
                        // This prevents giant jumps and eliminates the staircase stutter on slow movements.
                        Vector2 dir = diff / dist;
                        float excess = dist - effectiveDeadZone;
                        Vector2 baseDelta = dir * excess;
                        _mouseAnchorPosition = currentPos - (dir * effectiveDeadZone);

                        // 1. Distance compensation via anatomical palm scale
                        // Nominal palm span is ~0.18 at typical desk distance (~75 cm)
                        float palmScale = PalmDepthEstimator.ComputeRawDepthMetric(pose);
                        float scaleFactor = Math.Clamp(0.18f / MathF.Max(0.06f, palmScale), 0.7f, 2.0f);

                        // 2. Velocity-dependent ballistics / mouse acceleration:
                        // Slow movements (excess < 0.01) provide 1:1 pixel precision for buttons and links.
                        // Faster movements (excess > 0.03) scale progressively so a subtle 5-8 cm motion covers the full screen.
                        float accelerationMultiplier = 1.0f + MathF.Min(2.5f, MathF.Pow(excess * 25.0f, 1.3f));

                        float totalMultiplier = CurrentProfile.MouseSpeedMultiplier * scaleFactor * accelerationMultiplier;
                        float dx = baseDelta.X * totalMultiplier * 1920f;
                        float dy = baseDelta.Y * totalMultiplier * 1080f;
                        moveAction = ActionCommand.MoveMouse(dx, dy);
                    }
                }
            }
        }
        else
        {
            _mouseAnchorPosition = null;
        }

        // 3. Continuous IndexPoint Navigation (Specific Pointing Mode & Backward Compatibility)
        if (rawGesture == HandGestureType.IndexPoint)
        {
            return new GestureEvent(HandGestureType.IndexPoint, confidence, pose, moveAction, now, true);
        }

        // 4. Discrete Gestures (Pinch, Fist in non-3D mode, LateralPalm, etc.)
        if (rawGesture == HandGestureType.None)
        {
            _candidateGesture = HandGestureType.None;
            return moveAction != null
                ? new GestureEvent(HandGestureType.None, confidence, pose, moveAction, now, true)
                : null;
        }

        // Check cooldown from last triggered discrete command
        if ((now - _lastTriggerTime).TotalMilliseconds < CurrentProfile.CooldownMs)
        {
            return moveAction != null
                ? new GestureEvent(rawGesture, confidence, pose, moveAction, now, true)
                : null;
        }

        // Validate candidate hold time
        if (_candidateGesture != rawGesture)
        {
            _candidateGesture = rawGesture;
            _candidateStartTime = now;
            return moveAction != null
                ? new GestureEvent(rawGesture, confidence, pose, moveAction, now, true)
                : null;
        }

        double holdDuration = (now - _candidateStartTime).TotalMilliseconds;
        int effectiveHold = HoldDurationMsOverride ?? CurrentProfile.MinimumHoldDurationMs;
        if (holdDuration < effectiveHold)
        {
            return moveAction != null
                ? new GestureEvent(rawGesture, confidence, pose, moveAction, now, true)
                : null;
        }

        // Gesture confirmed!
        _lastTriggerTime = now;
        _lastTriggeredGesture = rawGesture;
        _candidateGesture = HandGestureType.None; // Reset candidate to require re-trigger or hold

        // Map gesture to command via active profile
        CurrentProfile.GestureBindings.TryGetValue(rawGesture, out var action);
        var finalAction = (action != null && action.Type != ActionCommandType.None)
            ? action
            : (moveAction ?? action);

        return new GestureEvent(
            Gesture: rawGesture,
            Confidence: confidence,
            Pose: pose,
            SuggestedAction: finalAction,
            Timestamp: now,
            IsConfirmed: true);
    }

    public void Reset()
    {
        _candidateGesture = HandGestureType.None;
        _candidateStartTime = DateTime.MinValue;
        _neutralAnchorPosition = null;
        _mouseAnchorPosition = null;
        _clickFreezeUntil = DateTime.MinValue;
        _lastTriggerTime = DateTime.MinValue;
        _lastSwipeTriggerTime = DateTime.MinValue;
        _trajectoryBuffer.Clear();
        _palmDepthEstimator.Reset();
        _currentNav3DState = Navigation3DState.None;
        _lastFrameTime = DateTime.MinValue;
    }
}
