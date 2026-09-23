using System.Diagnostics;
using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;
using OpenCvSharp;

namespace GestureControl.Vision.Camera;

/// <summary>
/// Camera service implementing OpenCvSharp VideoCapture with DirectShow low-latency mode.
/// Supports hardware webcams and provides simulated fallback frames when no webcam is attached.
/// Section 6 & 14 of architecture.
/// </summary>
public class OpenCvCameraService : ICameraService
{
    private VideoCapture? _capture;
    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private bool _isRunning;
    private long _frameIndex;
    private readonly byte[][] _bufferPool = new byte[3][];

    public bool IsRunning => _isRunning;
    public int FrameWidth { get; private set; } = 640;
    public int FrameHeight { get; private set; } = 480;
    public int TargetFps { get; private set; } = 30;

    public event EventHandler<(ReadOnlyMemory<byte> Buffer, CameraFrameMetadata Metadata)>? FrameCaptured;

    public Task StartAsync(int cameraIndex = 0, int width = 640, int height = 480, int fps = 30, CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            return Task.CompletedTask;

        FrameWidth = width;
        FrameHeight = height;
        TargetFps = fps;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

        // Try opening physical camera
        try
        {
            _capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
            if (_capture.IsOpened())
            {
                _capture.Set(VideoCaptureProperties.FrameWidth, width);
                _capture.Set(VideoCaptureProperties.FrameHeight, height);
                _capture.Set(VideoCaptureProperties.Fps, fps);
                _capture.Set(VideoCaptureProperties.BufferSize, 1); // Crucial: avoid internal driver frame buffering latency
            }
            else
            {
                _capture.Dispose();
                _capture = null;
            }
        }
        catch
        {
            _capture = null;
        }

        _captureTask = Task.Run(() => CaptureLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        using var frameMat = new Mat();
        using var rgbMat = new Mat();
        var frameDelay = TimeSpan.FromMilliseconds(1000.0 / TargetFps);
        var sw = Stopwatch.StartNew();

        while (!ct.IsCancellationRequested && _isRunning)
        {
            sw.Restart();
            DateTime now = DateTime.UtcNow;
            _frameIndex++;

            if (_capture != null && _capture.IsOpened())
            {
                if (_capture.Read(frameMat) && !frameMat.Empty())
                {
                    // Mirror horizontally for natural webcam interaction (selfie mode)
                    Cv2.Flip(frameMat, frameMat, FlipMode.Y);

                    // Convert BGR to RGB for WPF compatibility and standard ONNX models
                    Cv2.CvtColor(frameMat, rgbMat, ColorConversionCodes.BGR2RGB);

                    int totalBytes = (int)(rgbMat.Total() * rgbMat.ElemSize());
                    int poolIdx = (int)(_frameIndex % 3);
                    if (_bufferPool[poolIdx] == null || _bufferPool[poolIdx].Length != totalBytes)
                    {
                        _bufferPool[poolIdx] = new byte[totalBytes];
                    }
                    var managedBuffer = _bufferPool[poolIdx];
                    System.Runtime.InteropServices.Marshal.Copy(rgbMat.Data, managedBuffer, 0, totalBytes);

                    var metadata = new CameraFrameMetadata(
                        Width: rgbMat.Width,
                        Height: rgbMat.Height,
                        Stride: (int)rgbMat.Step(),
                        Timestamp: now,
                        FrameNumber: _frameIndex);

                    FrameCaptured?.Invoke(this, (new ReadOnlyMemory<byte>(managedBuffer, 0, totalBytes), metadata));
                }
            }
            else
            {
                // Simulated frame generator (canvas with dark-blue background and grid)
                using var simMat = new Mat(FrameHeight, FrameWidth, MatType.CV_8UC3, new Scalar(25, 30, 45));
                
                // Draw simulated guide box
                Cv2.Rectangle(simMat, new Rect(FrameWidth / 4, FrameHeight / 4, FrameWidth / 2, FrameHeight / 2), new Scalar(60, 80, 120), 2);
                Cv2.PutText(simMat, "Simulated Camera Feed (No hardware webcam detected)", new Point(20, 30),
                    HersheyFonts.HersheySimplex, 0.5, new Scalar(180, 200, 220), 1);

                int totalBytes = (int)(simMat.Total() * simMat.ElemSize());
                int poolIdx = (int)(_frameIndex % 3);
                if (_bufferPool[poolIdx] == null || _bufferPool[poolIdx].Length != totalBytes)
                {
                    _bufferPool[poolIdx] = new byte[totalBytes];
                }
                var managedBuffer = _bufferPool[poolIdx];
                System.Runtime.InteropServices.Marshal.Copy(simMat.Data, managedBuffer, 0, totalBytes);

                var metadata = new CameraFrameMetadata(
                    Width: FrameWidth,
                    Height: FrameHeight,
                    Stride: (int)simMat.Step(),
                    Timestamp: now,
                    FrameNumber: _frameIndex);

                FrameCaptured?.Invoke(this, (new ReadOnlyMemory<byte>(managedBuffer, 0, totalBytes), metadata));
            }

            var elapsed = sw.Elapsed;
            if (elapsed < frameDelay)
            {
                try
                {
                    await Task.Delay(frameDelay - elapsed, ct);
                }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        if (_cts != null)
        {
            _cts.Cancel();
        }

        if (_captureTask != null)
        {
            try
            {
                await _captureTask;
            }
            catch (OperationCanceledException) { }
            _captureTask = null;
        }

        _capture?.Release();
        _capture?.Dispose();
        _capture = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
