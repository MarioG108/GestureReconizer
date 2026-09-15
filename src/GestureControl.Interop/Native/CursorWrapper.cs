namespace GestureControl.Interop.Native;

/// <summary>
/// Provides Win32 cursor wrapping for infinite 360-degree viewport navigation (Orbit and Pan).
/// Centers and locks the physical cursor while transmitting relative mouse motion to 3D applications,
/// preventing the cursor from reaching the monitor boundaries.
/// </summary>
public sealed class CursorWrapper
{
    private static readonly Lazy<CursorWrapper> _instance = new(() => new CursorWrapper());
    public static CursorWrapper Instance => _instance.Value;

    private readonly object _lock = new();
    private bool _isWrapping = false;
    private int _anchorX;
    private int _anchorY;
    private float _accumX = 0f;
    private float _accumY = 0f;

    public bool IsWrapping
    {
        get { lock (_lock) return _isWrapping; }
    }

    public (int X, int Y) Anchor
    {
        get { lock (_lock) return (_anchorX, _anchorY); }
    }

    /// <summary>
    /// Begins cursor wrapping, capturing the current screen position as the return anchor.
    /// </summary>
    public void BeginWrap(int? customX = null, int? customY = null)
    {
        lock (_lock)
        {
            if (_isWrapping) return;

            if (customX.HasValue && customY.HasValue)
            {
                _anchorX = customX.Value;
                _anchorY = customY.Value;
            }
            else
            {
                if (Win32Input.GetCursorPos(out var pt))
                {
                    _anchorX = pt.X;
                    _anchorY = pt.Y;
                }
                else
                {
                    _anchorX = 960;
                    _anchorY = 540;
                }
            }

            _accumX = 0f;
            _accumY = 0f;
            _isWrapping = true;
        }
    }

    /// <summary>
    /// Dispatches relative mouse motion while immediately recentering the cursor at the anchor point.
    /// </summary>
    public void ApplyRelativeDelta(float deltaX, float deltaY)
    {
        lock (_lock)
        {
            if (!_isWrapping)
            {
                Win32Input.SendMouseMove((int)MathF.Round(deltaX), (int)MathF.Round(deltaY));
                return;
            }

            _accumX += deltaX;
            _accumY += deltaY;

            int sendX = (int)_accumX;
            int sendY = (int)_accumY;

            if (sendX != 0 || sendY != 0)
            {
                _accumX -= sendX;
                _accumY -= sendY;

                // 1. Send relative delta to application viewport
                Win32Input.SendMouseMove(sendX, sendY);

                // 2. Immediately reset cursor to anchor coordinates so it never hits screen edges
                Win32Input.SetCursorPos(_anchorX, _anchorY);
            }
        }
    }

    /// <summary>
    /// Ends cursor wrapping and restores cursor to the original anchor position.
    /// </summary>
    public void EndWrap()
    {
        lock (_lock)
        {
            if (!_isWrapping) return;

            Win32Input.SetCursorPos(_anchorX, _anchorY);
            _isWrapping = false;
            _accumX = 0f;
            _accumY = 0f;
        }
    }

    /// <summary>
    /// Forces reset of wrapping state without moving cursor.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _isWrapping = false;
            _accumX = 0f;
            _accumY = 0f;
        }
    }
}
