using System.Numerics;
using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;
using GestureControl.Gestures.Classifiers;

namespace GestureControl.Gestures.Engine;

/// <summary>
/// Intent and Operational Safety Engine.
/// Step 7 in Section 8.2 & Section 8.3 of the technical architecture.
/// Handles:
/// - Activation / Deactivation toggle
/// - Minimum hold duration (anti-glitch / anti-bounce)
/// - Cooldown timers per gesture/command
/// - Neutral dead-zones for continuous movement
/// </summary>
public class GestureEngine : IGestureEngine
{
    private HandGestureType _candidateGesture = HandGestureType.None;
    private DateTime _candidateStartTime = DateTime.MinValue;
    private DateTime _lastTriggerTime = DateTime.MinValue;
    private DateTime _lastSwipeTriggerTime = DateTime.MinValue;
    private HandGestureType _lastTriggeredGesture = HandGestureType.None;
    private Vector2? _neutralAnchorPosition = null;

    private readonly TrajectoryHistoryBuffer _trajectoryBuffer = new(32);
    private readonly DynamicGestureClassifier _dynamicClassifier = new();
    private readonly HandTrajectoryPoint[] _tempTrajectoryArray = new HandTrajectoryPoint[32];

    public bool IsActive { get; set; } = true;
    public AppProfile CurrentProfile { get; set; }
    public float? DeadZoneRadiusOverride { get; set; }
    public int? HoldDurationMsOverride { get; set; }
    public float? MinSwipeDistanceOverride { get; set; }
    public float? MinSwipeVelocityOverride { get; set; }

    public GestureEngine(AppProfile? initialProfile = null)
    {
        CurrentProfile = initialProfile ?? AppProfile.CreateDefaultGlobal();
    }

    public GestureEvent? ProcessFrame(
        HandPose pose,
        HandFeatures features,
        HandGestureType rawGesture,
        float confidence)
    {
        var now = pose.Timestamp;

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

        // Update palm trajectory history for dynamic gestures
        var palmCenter = pose.PalmCenter;
        _trajectoryBuffer.AddSample(new Vector3(palmCenter.X, palmCenter.Y, palmCenter.Z), now);

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

        // 2. Continuous Cursor Movement (IndexPoint)
        if (rawGesture == HandGestureType.IndexPoint)
        {
            var indexTip = pose.IndexTip;
            var currentPos = new Vector2(indexTip.X, indexTip.Y);

            if (!_neutralAnchorPosition.HasValue)
            {
                _neutralAnchorPosition = currentPos;
            }

            Vector2 diff = currentPos - _neutralAnchorPosition.Value;
            float dist = diff.Length();

            ActionCommand? moveAction = null;
            // Apply dead zone
            float effectiveDeadZone = DeadZoneRadiusOverride ?? CurrentProfile.DeadZoneRadius;
            if (dist > effectiveDeadZone)
            {
                float dx = diff.X * CurrentProfile.MouseSpeedMultiplier * 1920f;
                float dy = diff.Y * CurrentProfile.MouseSpeedMultiplier * 1080f;
                moveAction = ActionCommand.MoveMouse(dx, dy);
                _neutralAnchorPosition = currentPos; // Advance anchor
            }

            return new GestureEvent(
                Gesture: HandGestureType.IndexPoint,
                Confidence: confidence,
                Pose: pose,
                SuggestedAction: moveAction,
                Timestamp: now,
                IsConfirmed: true);
        }

        // Reset continuous anchor when not pointing
        _neutralAnchorPosition = null;

        // 2. Discrete Gestures (Pinch, Fist, TwoFingersPeace, etc.)
        if (rawGesture == HandGestureType.None)
        {
            _candidateGesture = HandGestureType.None;
            return null;
        }

        // Check cooldown from last triggered discrete command
        if ((now - _lastTriggerTime).TotalMilliseconds < CurrentProfile.CooldownMs)
        {
            return null;
        }

        // Validate candidate hold time
        if (_candidateGesture != rawGesture)
        {
            _candidateGesture = rawGesture;
            _candidateStartTime = now;
            return null; // Not held long enough yet
        }

        double holdDuration = (now - _candidateStartTime).TotalMilliseconds;
        int effectiveHold = HoldDurationMsOverride ?? CurrentProfile.MinimumHoldDurationMs;
        if (holdDuration < effectiveHold)
        {
            return null; // Awaiting threshold
        }

        // Gesture confirmed!
        _lastTriggerTime = now;
        _lastTriggeredGesture = rawGesture;
        _candidateGesture = HandGestureType.None; // Reset candidate to require re-trigger or hold

        // Map gesture to command via active profile
        CurrentProfile.GestureBindings.TryGetValue(rawGesture, out var action);

        return new GestureEvent(
            Gesture: rawGesture,
            Confidence: confidence,
            Pose: pose,
            SuggestedAction: action,
            Timestamp: now,
            IsConfirmed: true);
    }

    public void Reset()
    {
        _candidateGesture = HandGestureType.None;
        _candidateStartTime = DateTime.MinValue;
        _neutralAnchorPosition = null;
        _lastTriggerTime = DateTime.MinValue;
        _lastSwipeTriggerTime = DateTime.MinValue;
        _trajectoryBuffer.Clear();
    }
}
