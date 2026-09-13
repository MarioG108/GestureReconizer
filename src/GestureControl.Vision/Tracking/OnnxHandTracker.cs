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
    private float _simulatedPhase;

    public bool IsModelLoaded => _isLoaded;
    public string TrackerName => _isLoaded ? "ONNX MediaPipe Hand Tracker" : "Simulated Hand Tracker";

    public OnnxHandTracker(string? modelPath = null)
    {
        string path = modelPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "hand_landmark.onnx");

        if (File.Exists(path))
        {
            try
            {
                var options = new SessionOptions
                {
                    IntraOpNumThreads = Environment.ProcessorCount >= 4 ? 4 : 2,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode = ExecutionMode.ORT_PARALLEL
                };
                _session = new InferenceSession(path, options);
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
    }

    public IReadOnlyList<HandPose> TrackHands(ReadOnlySpan<byte> rgbFrame, int width, int height, int stride)
    {
        if (_session != null && _isLoaded)
        {
            return RunInference(rgbFrame, width, height, stride);
        }

        // Graceful fallback: synthetic interactive tracker for testing and validation
        return GenerateSimulatedPose(width, height);
    }

    private IReadOnlyList<HandPose> RunInference(ReadOnlySpan<byte> rgbFrame, int width, int height, int stride)
    {
        if (_session == null)
            return Array.Empty<HandPose>();

        // Model expects [1, 3, 224, 224] float normalized [0, 1]
        const int targetDim = 224;
        var tensor = new DenseTensor<float>(new[] { 1, 3, targetDim, targetDim });

        float scaleX = (float)width / targetDim;
        float scaleY = (float)height / targetDim;

        // Populate tensor with nearest neighbor downsampling directly from ReadOnlySpan
        for (int y = 0; y < targetDim; y++)
        {
            int srcY = Math.Min((int)(y * scaleY), height - 1);
            int rowOffset = srcY * stride;

            for (int x = 0; x < targetDim; x++)
            {
                int srcX = Math.Min((int)(x * scaleX), width - 1);
                int pixelOffset = rowOffset + (srcX * 3);

                if (pixelOffset + 2 < rgbFrame.Length)
                {
                    tensor[0, 0, y, x] = rgbFrame[pixelOffset] / 255.0f;     // R
                    tensor[0, 1, y, x] = rgbFrame[pixelOffset + 1] / 255.0f; // G
                    tensor[0, 2, y, x] = rgbFrame[pixelOffset + 2] / 255.0f; // B
                }
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_1", tensor)
        };

        using var results = _session.Run(inputs);
        var output = results.FirstOrDefault()?.AsTensor<float>();

        if (output == null || output.Length < 63)
            return Array.Empty<HandPose>();

        var landmarks = new HandLandmark[21];
        for (int i = 0; i < 21; i++)
        {
            float lx = output.GetValue(i * 3) / targetDim;
            float ly = output.GetValue(i * 3 + 1) / targetDim;
            float lz = output.GetValue(i * 3 + 2) / targetDim;

            landmarks[i] = new HandLandmark((HandLandmarkType)i, lx, ly, lz);
        }

        return new[] { new HandPose(landmarks, Handedness.Right, 0.92f) };
    }

    private IReadOnlyList<HandPose> GenerateSimulatedPose(int width, int height)
    {
        _simulatedPhase += 0.03f;
        float centerX = 0.5f + (MathF.Cos(_simulatedPhase) * 0.1f);
        float centerY = 0.55f + (MathF.Sin(_simulatedPhase * 0.7f) * 0.08f);

        var landmarks = new HandLandmark[21];
        landmarks[(int)HandLandmarkType.Wrist] = new(HandLandmarkType.Wrist, centerX, centerY + 0.15f, 0f);

        // Simulated open hand with slight finger movement
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

        return new[] { new HandPose(landmarks, Handedness.Right, 0.95f) };
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
