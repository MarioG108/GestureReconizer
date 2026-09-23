using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace GestureControl.Vision.Tracking;

/// <summary>
/// MediaPipe Hand Landmark tracker powered by ONNX Runtime with CPU/DirectML execution.
/// Follows Section 7 & 8 of the technical architecture.
/// Provides fallback synthetic tracking if the ONNX model is not yet placed in models/ directory.
/// </summary>
public class OnnxHandTracker : IHandTracker
{
    private readonly InferenceSession? _session;
    private readonly bool _isLoaded;
    private readonly DenseTensor<float> _tensor = new(new[] { 1, 3, 224, 224 });
    private readonly List<NamedOnnxValue> _inputs;
    private RectF? _lastHandRoi;
    private int _roiTrackingFrames = 0;
    private float _simulatedPhase;

    private readonly record struct RectF(float X, float Y, float Width, float Height);

    public bool IsModelLoaded => _isLoaded;
    public string TrackerName => _isLoaded ? "ONNX MediaPipe Hand Tracker" : "Simulated Hand Tracker";

    public OnnxHandTracker(string? modelPath = null)
    {
        string? resolvedPath;
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            resolvedPath = File.Exists(modelPath) ? modelPath : null;
        }
        else
        {
            string[] candidatePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "hand_landmark.onnx"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hand_landmark.onnx"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\models\hand_landmark.onnx")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\models\hand_landmark.onnx")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "models", "hand_landmark.onnx")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "hand_landmark.onnx"))
            };
            resolvedPath = candidatePaths.FirstOrDefault(File.Exists);
        }

        if (resolvedPath != null)
        {
            try
            {
                var options = new SessionOptions
                {
                    IntraOpNumThreads = Environment.ProcessorCount >= 4 ? 4 : 2,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode = ExecutionMode.ORT_PARALLEL
                };
                _session = new InferenceSession(resolvedPath, options);
                _isLoaded = true;
            }
            catch
            {
                _session = null;
                _isLoaded = false;
            }
        }
        else
        {
            _session = null;
            _isLoaded = false;
        }

        _inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_1", _tensor)
        };
    }

    public IReadOnlyList<HandPose> TrackHands(ReadOnlySpan<byte> rgbFrame, int width, int height, int stride)
    {
        if (_session != null && _isLoaded)
        {
            return RunInference(rgbFrame, width, height, stride);
        }

        // When ONNX model is absent or not loaded, return empty to prevent phantom clicks
        return Array.Empty<HandPose>();
    }

    private IReadOnlyList<HandPose> RunInference(ReadOnlySpan<byte> rgbFrame, int width, int height, int stride)
    {
        if (_session == null || width <= 0 || height <= 0 || rgbFrame.IsEmpty)
            return Array.Empty<HandPose>();

        const int targetDim = 224;

        // 1. Determine Region of Interest (ROI)
        // If an active hand was tracked in previous frames, zoom in to its bounding box + margin.
        // Otherwise, use a center-square crop that preserves the 1:1 aspect ratio (eliminating 33% distortion).
        float originX, originY, roiWidth, roiHeight;

        if (_lastHandRoi.HasValue)
        {
            originX = _lastHandRoi.Value.X;
            originY = _lastHandRoi.Value.Y;
            roiWidth = _lastHandRoi.Value.Width;
            roiHeight = _lastHandRoi.Value.Height;
        }
        else
        {
            float boxSize = Math.Min(width, height);
            originX = (width - boxSize) * 0.5f;
            originY = (height - boxSize) * 0.5f;
            roiWidth = boxSize;
            roiHeight = boxSize;
        }

        float stepX = roiWidth / targetDim;
        float stepY = roiHeight / targetDim;

        // 2. Populate zero-allocation tensor from the ROI
        for (int y = 0; y < targetDim; y++)
        {
            int srcY = Math.Clamp((int)(originY + (y * stepY)), 0, height - 1);
            int rowOffset = srcY * stride;

            for (int x = 0; x < targetDim; x++)
            {
                int srcX = Math.Clamp((int)(originX + (x * stepX)), 0, width - 1);
                int pixelOffset = rowOffset + (srcX * 3);

                if (pixelOffset + 2 < rgbFrame.Length)
                {
                    _tensor[0, 0, y, x] = rgbFrame[pixelOffset] / 255.0f;     // R
                    _tensor[0, 1, y, x] = rgbFrame[pixelOffset + 1] / 255.0f; // G
                    _tensor[0, 2, y, x] = rgbFrame[pixelOffset + 2] / 255.0f; // B
                }
            }
        }

        using var results = _session.Run(_inputs);
        var rList = results.ToList();
        if (rList.Count < 3)
        {
            _lastHandRoi = null;
            return Array.Empty<HandPose>();
        }

        var landmarksTensor = rList[0].AsTensor<float>(); // Identity: [1, 63] (21 points x, y, z)
        var presenceTensor = rList[1].AsTensor<float>();  // Identity_1: [1, 1] (hand presence probability)
        var handednessTensor = rList[2].AsTensor<float>(); // Identity_2: [1, 1] (handedness probability)

        float presence = presenceTensor.GetValue(0);

        // Hysteresis: lower retention threshold (0.25) when actively tracking ROI, 0.40 for initial detection
        float minPresence = _lastHandRoi.HasValue ? 0.25f : 0.40f;
        if (presence < minPresence)
        {
            _lastHandRoi = null;
            _roiTrackingFrames = 0;
            return Array.Empty<HandPose>();
        }

        float handednessVal = handednessTensor.GetValue(0);
        var handedness = handednessVal >= 0.5f ? Handedness.Right : Handedness.Left;

        var landmarks = new HandLandmark[21];
        float minPx = float.MaxValue, maxPx = float.MinValue;
        float minPy = float.MaxValue, maxPy = float.MinValue;

        for (int i = 0; i < 21; i++)
        {
            // Model outputs coordinates in pixel space [0..224] of the input tensor
            float rawX = landmarksTensor.GetValue(i * 3);
            float rawY = landmarksTensor.GetValue(i * 3 + 1);
            float rawZ = landmarksTensor.GetValue(i * 3 + 2);

            // Map from ROI coordinates back to full camera frame pixel coordinates
            float pixelX = originX + ((rawX / targetDim) * roiWidth);
            float pixelY = originY + ((rawY / targetDim) * roiHeight);

            if (pixelX < minPx) minPx = pixelX;
            if (pixelX > maxPx) maxPx = pixelX;
            if (pixelY < minPy) minPy = pixelY;
            if (pixelY > maxPy) maxPy = pixelY;

            // Normalized to [0..1] of full camera frame
            float lx = Math.Clamp(pixelX / width, 0.0f, 1.0f);
            float ly = Math.Clamp(pixelY / height, 0.0f, 1.0f);
            float lz = (rawZ / targetDim) * (roiWidth / width);

            landmarks[i] = new HandLandmark((HandLandmarkType)i, lx, ly, lz);
        }

        // 3. Compute predicted ROI for next frame
        float handW = maxPx - minPx;
        float handH = maxPy - minPy;
        float handCenterPx = (minPx + maxPx) * 0.5f;
        float handCenterPy = (minPy + maxPy) * 0.5f;

        float handSpan = MathF.Max(handW, handH);
        float nextRoiSize = Math.Clamp(handSpan * 1.6f, 110f, Math.Min(width, height));
        float nextOriginX = Math.Clamp(handCenterPx - (nextRoiSize * 0.5f), 0f, width - nextRoiSize);
        float nextOriginY = Math.Clamp(handCenterPy - (nextRoiSize * 0.5f), 0f, height - nextRoiSize);

        _lastHandRoi = new RectF(nextOriginX, nextOriginY, nextRoiSize, nextRoiSize);
        _roiTrackingFrames++;

        return new[] { new HandPose(landmarks, handedness, presence, DateTime.UtcNow) };
    }

    /// <summary>
    /// Explicit helper for synthetic offline tests if needed.
    /// </summary>
    public HandPose GenerateSimulatedPose(int width = 640, int height = 480)
    {
        _simulatedPhase += 0.03f;
        float centerX = 0.5f + (MathF.Cos(_simulatedPhase) * 0.1f);
        float centerY = 0.55f + (MathF.Sin(_simulatedPhase * 0.7f) * 0.08f);

        var landmarks = new HandLandmark[21];
        landmarks[(int)HandLandmarkType.Wrist] = new(HandLandmarkType.Wrist, centerX, centerY + 0.15f, 0f);

        for (int f = 0; f < 5; f++)
        {
            float angle = -0.5f + (f * 0.25f);

            int mcp = 1 + (f * 4);
            int pip = mcp + 1;
            int dip = mcp + 2;
            int tip = mcp + 3;

            landmarks[mcp] = new((HandLandmarkType)mcp, centerX + (MathF.Sin(angle) * 0.05f), centerY - 0.02f, 0f);
            landmarks[pip] = new((HandLandmarkType)pip, centerX + (MathF.Sin(angle) * 0.09f), centerY - 0.07f, -0.01f);
            landmarks[dip] = new((HandLandmarkType)dip, centerX + (MathF.Sin(angle) * 0.12f), centerY - 0.11f, -0.02f);
            landmarks[tip] = new((HandLandmarkType)tip, centerX + (MathF.Sin(angle) * 0.15f), centerY - 0.15f, -0.03f);
        }

        return new HandPose(landmarks, Handedness.Right, 0.95f, DateTime.UtcNow);
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
