using System.Numerics;
using GestureControl.Core.Models;

namespace GestureControl.Gestures.Classifiers;

/// <summary>
/// Circular fixed-size buffer tracking the recent hand trajectory points for dynamic gesture recognition.
/// Implements zero heap allocation per frame.
/// </summary>
public sealed class TrajectoryHistoryBuffer
{
    private readonly HandTrajectoryPoint[] _buffer;
    private int _head = 0;
    private int _count = 0;
    private readonly int _capacity;
    private readonly object _lock = new();

    public int Count => _count;
    public int Capacity => _capacity;

    public TrajectoryHistoryBuffer(int capacity = 32)
    {
        _capacity = Math.Max(8, capacity);
        _buffer = new HandTrajectoryPoint[_capacity];
    }

    /// <summary>
    /// Adds a new spatial sample point to the trajectory history.
    /// Calculates instant velocity relative to the most recent sample.
    /// </summary>
    public void AddSample(Vector3 position, DateTime timestamp)
    {
        lock (_lock)
        {
            Vector2 velocity = Vector2.Zero;
            if (_count > 0)
            {
                int prevIndex = (_head - 1 + _capacity) % _capacity;
                ref readonly var prev = ref _buffer[prevIndex];
                double dt = (timestamp - prev.Timestamp).TotalSeconds;
                if (dt > 0.001 && dt < 1.0)
                {
                    float vx = (position.X - prev.X) / (float)dt;
                    float vy = (position.Y - prev.Y) / (float)dt;
                    velocity = new Vector2(vx, vy);
                }
            }

            _buffer[_head] = new HandTrajectoryPoint(position, timestamp, velocity);
            _head = (_head + 1) % _capacity;
            if (_count < _capacity)
            {
                _count++;
            }
        }
    }

    /// <summary>
    /// Copies recent trajectory points ordered from oldest to newest into the destination span.
    /// Returns the number of points copied.
    /// </summary>
    public int CopyTo(Span<HandTrajectoryPoint> destination)
    {
        lock (_lock)
        {
            int toCopy = Math.Min(_count, destination.Length);
            if (toCopy == 0) return 0;

            int start = (_head - _count + _capacity) % _capacity;
            for (int i = 0; i < toCopy; i++)
            {
                int idx = (start + i) % _capacity;
                destination[i] = _buffer[idx];
            }
            return toCopy;
        }
    }

    /// <summary>
    /// Computes displacement and average speed within the specified time window up to the latest point.
    /// </summary>
    public bool TryGetWindowMetrics(
        TimeSpan window,
        out Vector2 displacement,
        out float elapsedSeconds,
        out float avgSpeed,
        out int samplesInWindow)
    {
        displacement = Vector2.Zero;
        elapsedSeconds = 0f;
        avgSpeed = 0f;
        samplesInWindow = 0;

        lock (_lock)
        {
            if (_count < 2) return false;

            int newestIdx = (_head - 1 + _capacity) % _capacity;
            ref readonly var newest = ref _buffer[newestIdx];
            DateTime cutoff = newest.Timestamp - window;

            // Find oldest sample within window
            int oldestIdx = newestIdx;
            int countInWindow = 1;

            for (int i = 1; i < _count; i++)
            {
                int curr = (_head - 1 - i + _capacity * 2) % _capacity;
                if (_buffer[curr].Timestamp < cutoff)
                {
                    break;
                }
                oldestIdx = curr;
                countInWindow++;
            }

            if (countInWindow < 2) return false;

            ref readonly var oldest = ref _buffer[oldestIdx];
            double dt = (newest.Timestamp - oldest.Timestamp).TotalSeconds;
            if (dt < 0.03) return false; // Minimum time interval to avoid division instability

            elapsedSeconds = (float)dt;
            displacement = new Vector2(newest.X - oldest.X, newest.Y - oldest.Y);
            avgSpeed = displacement.Length() / elapsedSeconds;
            samplesInWindow = countInWindow;

            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _head = 0;
            _count = 0;
        }
    }
}
