using System.Diagnostics;
using System.Threading.Channels;
using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;

namespace GestureControl.Core.Pipeline;

public record PipelineFrame(
    byte[] ImageBuffer,
    int Width,
    int Height,
    int Stride,
    DateTime Timestamp,
    long FrameNumber);

public record PipelineStatistics(
    float Fps,
    float LatencyMs,
    HandGestureType CurrentGesture,
    bool IsActive,
    string ActiveProfileName,
    long ProcessedFrames,
    float Confidence = 0f);

/// <summary>
/// Asynchronous parallel multi-stage pipeline using Bounded Channels and zero-allocation Spans.
/// Coordinates camera capture, hand tracking, landmark smoothing, feature extraction, safety checks, and OS input dispatching.
/// </summary>
public class GesturePipelineOrchestrator : IAsyncDisposable
{
    private readonly ICameraService _cameraService;
    private readonly IHandTracker _handTracker;
    private readonly ITemporalFilter _filter;
    private readonly IFeatureExtractor _extractor;
    private readonly IGestureClassifier _classifier;
    private readonly IGestureEngine _engine;
    private readonly IActionDispatcher _dispatcher;
    private readonly IProfileManager _profileManager;

    private readonly Channel<PipelineFrame> _frameChannel;
    private readonly CancellationTokenSource _cts = new();
    private readonly HandLandmark[] _landmarkBuffer = new HandLandmark[21];
    private Task? _processingTask;

    private long _processedFrames;
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private float _currentFps;
    private float _currentLatencyMs;
    private HandGestureType _currentGesture = HandGestureType.None;
    private float _currentConfidence = 0f;

    public event EventHandler<PipelineStatistics>? StatisticsUpdated;
    public event EventHandler<GestureEvent>? GestureTriggered;
    public event EventHandler<HandPose?>? HandPoseDetected;

    public GesturePipelineOrchestrator(
        ICameraService cameraService,
        IHandTracker handTracker,
        ITemporalFilter filter,
        IFeatureExtractor extractor,
        IGestureClassifier classifier,
        IGestureEngine engine,
        IActionDispatcher dispatcher,
        IProfileManager profileManager)
    {
        _cameraService = cameraService;
        _handTracker = handTracker;
        _filter = filter;
        _extractor = extractor;
        _classifier = classifier;
        _engine = engine;
        _dispatcher = dispatcher;
        _profileManager = profileManager;

        // Bounded channel with DropOldest ensures we never accumulate latency or consume excess memory if processing slows down
        var channelOptions = new BoundedChannelOptions(2)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true
        };
        _frameChannel = Channel.CreateBounded<PipelineFrame>(channelOptions);

        _profileManager.ProfileChanged += (_, profile) => _engine.CurrentProfile = profile;
        _cameraService.FrameCaptured += OnFrameCaptured;
    }

    private void OnFrameCaptured(object? sender, (ReadOnlyMemory<byte> Buffer, CameraFrameMetadata Metadata) e)
    {
        // Copy memory slice to frame packet and push into channel
        var frame = new PipelineFrame(
            ImageBuffer: e.Buffer.ToArray(),
            Width: e.Metadata.Width,
            Height: e.Metadata.Height,
            Stride: e.Metadata.Stride,
            Timestamp: e.Metadata.Timestamp,
            FrameNumber: e.Metadata.FrameNumber);

        _frameChannel.Writer.TryWrite(frame);
    }

    private bool _isRunning;
    public bool IsRunning => _isRunning;

    public Task StartAsync(int cameraIndex = 0, int width = 640, int height = 480, int fps = 30)
    {
        _isRunning = true;
        _processingTask = Task.Run(() => ProcessPipelineLoopAsync(_cts.Token));
        return _cameraService.StartAsync(cameraIndex, width, height, fps, _cts.Token);
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        await _cameraService.StopAsync();
    }

    private async Task ProcessPipelineLoopAsync(CancellationToken ct)
    {
        HandPose? previousPose = null;
        var latencyStopwatch = new Stopwatch();

        while (!ct.IsCancellationRequested && await _frameChannel.Reader.WaitToReadAsync(ct))
        {
            while (_frameChannel.Reader.TryRead(out var frame))
            {
                latencyStopwatch.Restart();

                try
                {
                    // 1. Hand Detection & Landmark Tracking (using ReadOnlySpan of raw frame)
                    ReadOnlySpan<byte> frameSpan = frame.ImageBuffer.AsSpan();
                    var hands = _handTracker.TrackHands(frameSpan, frame.Width, frame.Height, frame.Stride);

                    if (hands.Count > 0)
                    {
                        var rawPose = hands[0];

                        // 2. Temporal Smoothing with zero-allocation Span buffer
                        _filter.Smooth(rawPose.AsSpan(), _landmarkBuffer.AsSpan());
                        var smoothedPose = new HandPose(_landmarkBuffer.AsSpan(), rawPose.Handedness, rawPose.Confidence, rawPose.Timestamp);

                        HandPoseDetected?.Invoke(this, smoothedPose);

                        // 3. Feature Extraction on zero-allocation ReadOnlySpan
                        float dt = previousPose != null
                            ? (float)(smoothedPose.Timestamp - previousPose.Timestamp).TotalSeconds
                            : 0.033f;
                        var features = _extractor.ExtractFeatures(smoothedPose.AsSpan(), previousPose, dt);
                        previousPose = smoothedPose;

                        // 4. Gesture Classification
                        var classification = _classifier.Classify(smoothedPose, in features);
                        _currentGesture = classification.Gesture;
                        _currentConfidence = classification.Confidence;

                        // 5. Intent and Safety Engine (Cooldowns, Hold times, Dead zones, Swipes)
                        var gestureEvent = _engine.ProcessFrame(
                            smoothedPose,
                            features,
                            classification.Gesture,
                            classification.Confidence);

                        if (gestureEvent != null && gestureEvent.IsConfirmed)
                        {
                            _currentGesture = gestureEvent.Gesture;
                            _currentConfidence = gestureEvent.Confidence;
                            GestureTriggered?.Invoke(this, gestureEvent);

                            // 6. Action Dispatcher (SendInput Win32)
                            if (gestureEvent.SuggestedAction != null)
                            {
                                await _dispatcher.ExecuteAsync(gestureEvent.SuggestedAction, ct);
                            }
                        }
                    }
                    else
                    {
                        _currentGesture = HandGestureType.None;
                        _currentConfidence = 0f;
                        _filter.Reset();
                        _engine.Reset();
                        previousPose = null;
                        HandPoseDetected?.Invoke(this, null);
                    }
                }
                catch (Exception)
                {
                    // Log and prevent loop termination on single frame failure
                }

                latencyStopwatch.Stop();
                _currentLatencyMs = (float)latencyStopwatch.Elapsed.TotalMilliseconds;
                _processedFrames++;

                if (_fpsStopwatch.ElapsedMilliseconds >= 500)
                {
                    _currentFps = _processedFrames / (float)_fpsStopwatch.Elapsed.TotalSeconds;
                    _fpsStopwatch.Restart();
                    _processedFrames = 0;
                }

                StatisticsUpdated?.Invoke(this, new PipelineStatistics(
                    Fps: _currentFps,
                    LatencyMs: _currentLatencyMs,
                    CurrentGesture: _currentGesture,
                    IsActive: _engine.IsActive,
                    ActiveProfileName: _engine.CurrentProfile.Name,
                    ProcessedFrames: frame.FrameNumber,
                    Confidence: _currentConfidence));
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _frameChannel.Writer.Complete();

        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException) { }
        }

        await _cameraService.StopAsync();
        _cameraService.FrameCaptured -= OnFrameCaptured;
        await _cameraService.DisposeAsync();
        _handTracker.Dispose();
        _cts.Dispose();
    }
}
