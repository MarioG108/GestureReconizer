namespace GestureControl.Core.Interfaces;

public record CameraFrameMetadata(
    int Width,
    int Height,
    int Stride,
    DateTime Timestamp,
    long FrameNumber);

/// <summary>
/// Abstraction for camera video acquisition.
/// Supports real webcam devices and synthetic/test frames.
/// </summary>
public interface ICameraService : IAsyncDisposable
{
    bool IsRunning { get; }
    int FrameWidth { get; }
    int FrameHeight { get; }
    int TargetFps { get; }

    /// <summary>
    /// Event triggered when a new frame is captured from the camera.
    /// The buffer is passed as a ReadOnlyMemory to allow zero-copy consumption.
    /// </summary>
    event EventHandler<(ReadOnlyMemory<byte> Buffer, CameraFrameMetadata Metadata)>? FrameCaptured;

    Task StartAsync(int cameraIndex = 0, int width = 640, int height = 480, int fps = 30, CancellationToken cancellationToken = default);
    Task StopAsync();
}
