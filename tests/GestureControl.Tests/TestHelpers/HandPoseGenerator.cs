using System.Numerics;
using GestureControl.Core.Models;

namespace GestureControl.Tests.TestHelpers;

public static class HandPoseGenerator
{
    /// <summary>
    /// Generates a synthetic 21-landmark hand pose representing an open hand facing the camera.
    /// </summary>
    public static HandPose CreateOpenHand(float wristX = 0.5f, float wristY = 0.7f)
    {
        var landmarks = new HandLandmark[21];
        landmarks[(int)HandLandmarkType.Wrist] = new(HandLandmarkType.Wrist, wristX, wristY, 0f);

        // Thumb extended outward
        landmarks[(int)HandLandmarkType.ThumbCmc] = new(HandLandmarkType.ThumbCmc, wristX - 0.05f, wristY - 0.05f, -0.01f);
        landmarks[(int)HandLandmarkType.ThumbMcp] = new(HandLandmarkType.ThumbMcp, wristX - 0.09f, wristY - 0.09f, -0.02f);
        landmarks[(int)HandLandmarkType.ThumbIp] = new(HandLandmarkType.ThumbIp, wristX - 0.12f, wristY - 0.13f, -0.03f);
        landmarks[(int)HandLandmarkType.ThumbTip] = new(HandLandmarkType.ThumbTip, wristX - 0.15f, wristY - 0.17f, -0.04f);

        // Index extended upward
        landmarks[(int)HandLandmarkType.IndexFingerMcp] = new(HandLandmarkType.IndexFingerMcp, wristX - 0.04f, wristY - 0.12f, 0f);
        landmarks[(int)HandLandmarkType.IndexFingerPip] = new(HandLandmarkType.IndexFingerPip, wristX - 0.04f, wristY - 0.18f, -0.01f);
        landmarks[(int)HandLandmarkType.IndexFingerDip] = new(HandLandmarkType.IndexFingerDip, wristX - 0.04f, wristY - 0.23f, -0.02f);
        landmarks[(int)HandLandmarkType.IndexFingerTip] = new(HandLandmarkType.IndexFingerTip, wristX - 0.04f, wristY - 0.28f, -0.03f);

        // Middle extended upward
        landmarks[(int)HandLandmarkType.MiddleFingerMcp] = new(HandLandmarkType.MiddleFingerMcp, wristX, wristY - 0.13f, 0f);
        landmarks[(int)HandLandmarkType.MiddleFingerPip] = new(HandLandmarkType.MiddleFingerPip, wristX, wristY - 0.20f, -0.01f);
        landmarks[(int)HandLandmarkType.MiddleFingerDip] = new(HandLandmarkType.MiddleFingerDip, wristX, wristY - 0.26f, -0.02f);
        landmarks[(int)HandLandmarkType.MiddleFingerTip] = new(HandLandmarkType.MiddleFingerTip, wristX, wristY - 0.31f, -0.03f);

        // Ring extended upward
        landmarks[(int)HandLandmarkType.RingFingerMcp] = new(HandLandmarkType.RingFingerMcp, wristX + 0.04f, wristY - 0.12f, 0f);
        landmarks[(int)HandLandmarkType.RingFingerPip] = new(HandLandmarkType.RingFingerPip, wristX + 0.04f, wristY - 0.18f, -0.01f);
        landmarks[(int)HandLandmarkType.RingFingerDip] = new(HandLandmarkType.RingFingerDip, wristX + 0.04f, wristY - 0.23f, -0.02f);
        landmarks[(int)HandLandmarkType.RingFingerTip] = new(HandLandmarkType.RingFingerTip, wristX + 0.04f, wristY - 0.28f, -0.03f);

        // Pinky extended upward
        landmarks[(int)HandLandmarkType.PinkyFingerMcp] = new(HandLandmarkType.PinkyFingerMcp, wristX + 0.08f, wristY - 0.10f, 0f);
        landmarks[(int)HandLandmarkType.PinkyFingerPip] = new(HandLandmarkType.PinkyFingerPip, wristX + 0.08f, wristY - 0.15f, -0.01f);
        landmarks[(int)HandLandmarkType.PinkyFingerDip] = new(HandLandmarkType.PinkyFingerDip, wristX + 0.08f, wristY - 0.19f, -0.02f);
        landmarks[(int)HandLandmarkType.PinkyFingerTip] = new(HandLandmarkType.PinkyFingerTip, wristX + 0.08f, wristY - 0.23f, -0.03f);

        return new HandPose(landmarks, Handedness.Right, 0.98f);
    }

    /// <summary>
    /// Generates a synthetic fist pose (all fingers folded toward palm).
    /// </summary>
    public static HandPose CreateFist(float wristX = 0.5f, float wristY = 0.7f)
    {
        var landmarks = new HandLandmark[21];
        landmarks[(int)HandLandmarkType.Wrist] = new(HandLandmarkType.Wrist, wristX, wristY, 0f);

        // Thumb folded
        landmarks[(int)HandLandmarkType.ThumbCmc] = new(HandLandmarkType.ThumbCmc, wristX - 0.03f, wristY - 0.04f, 0f);
        landmarks[(int)HandLandmarkType.ThumbMcp] = new(HandLandmarkType.ThumbMcp, wristX - 0.05f, wristY - 0.07f, 0f);
        landmarks[(int)HandLandmarkType.ThumbIp] = new(HandLandmarkType.ThumbIp, wristX - 0.03f, wristY - 0.08f, 0f);
        landmarks[(int)HandLandmarkType.ThumbTip] = new(HandLandmarkType.ThumbTip, wristX - 0.01f, wristY - 0.08f, 0f);

        // Index folded
        landmarks[(int)HandLandmarkType.IndexFingerMcp] = new(HandLandmarkType.IndexFingerMcp, wristX - 0.03f, wristY - 0.10f, 0f);
        landmarks[(int)HandLandmarkType.IndexFingerPip] = new(HandLandmarkType.IndexFingerPip, wristX - 0.03f, wristY - 0.13f, 0f);
        landmarks[(int)HandLandmarkType.IndexFingerDip] = new(HandLandmarkType.IndexFingerDip, wristX - 0.03f, wristY - 0.11f, 0f);
        landmarks[(int)HandLandmarkType.IndexFingerTip] = new(HandLandmarkType.IndexFingerTip, wristX - 0.03f, wristY - 0.09f, 0f);

        // Middle folded
        landmarks[(int)HandLandmarkType.MiddleFingerMcp] = new(HandLandmarkType.MiddleFingerMcp, wristX, wristY - 0.10f, 0f);
        landmarks[(int)HandLandmarkType.MiddleFingerPip] = new(HandLandmarkType.MiddleFingerPip, wristX, wristY - 0.13f, 0f);
        landmarks[(int)HandLandmarkType.MiddleFingerDip] = new(HandLandmarkType.MiddleFingerDip, wristX, wristY - 0.11f, 0f);
        landmarks[(int)HandLandmarkType.MiddleFingerTip] = new(HandLandmarkType.MiddleFingerTip, wristX, wristY - 0.09f, 0f);

        // Ring folded
        landmarks[(int)HandLandmarkType.RingFingerMcp] = new(HandLandmarkType.RingFingerMcp, wristX + 0.03f, wristY - 0.10f, 0f);
        landmarks[(int)HandLandmarkType.RingFingerPip] = new(HandLandmarkType.RingFingerPip, wristX + 0.03f, wristY - 0.13f, 0f);
        landmarks[(int)HandLandmarkType.RingFingerDip] = new(HandLandmarkType.RingFingerDip, wristX + 0.03f, wristY - 0.11f, 0f);
        landmarks[(int)HandLandmarkType.RingFingerTip] = new(HandLandmarkType.RingFingerTip, wristX + 0.03f, wristY - 0.09f, 0f);

        // Pinky folded
        landmarks[(int)HandLandmarkType.PinkyFingerMcp] = new(HandLandmarkType.PinkyFingerMcp, wristX + 0.06f, wristY - 0.09f, 0f);
        landmarks[(int)HandLandmarkType.PinkyFingerPip] = new(HandLandmarkType.PinkyFingerPip, wristX + 0.06f, wristY - 0.12f, 0f);
        landmarks[(int)HandLandmarkType.PinkyFingerDip] = new(HandLandmarkType.PinkyFingerDip, wristX + 0.06f, wristY - 0.10f, 0f);
        landmarks[(int)HandLandmarkType.PinkyFingerTip] = new(HandLandmarkType.PinkyFingerTip, wristX + 0.06f, wristY - 0.08f, 0f);

        return new HandPose(landmarks, Handedness.Right, 0.98f);
    }

    /// <summary>
    /// Generates an Index Pointing pose (index extended, others folded).
    /// </summary>
    public static HandPose CreateIndexPoint(float wristX = 0.5f, float wristY = 0.7f)
    {
        var fist = CreateFist(wristX, wristY);
        var landmarks = fist.Landmarks.ToArray();

        // Extend index finger
        landmarks[(int)HandLandmarkType.IndexFingerMcp] = new(HandLandmarkType.IndexFingerMcp, wristX - 0.04f, wristY - 0.12f, 0f);
        landmarks[(int)HandLandmarkType.IndexFingerPip] = new(HandLandmarkType.IndexFingerPip, wristX - 0.04f, wristY - 0.18f, -0.01f);
        landmarks[(int)HandLandmarkType.IndexFingerDip] = new(HandLandmarkType.IndexFingerDip, wristX - 0.04f, wristY - 0.23f, -0.02f);
        landmarks[(int)HandLandmarkType.IndexFingerTip] = new(HandLandmarkType.IndexFingerTip, wristX - 0.04f, wristY - 0.28f, -0.03f);

        return new HandPose(landmarks, Handedness.Right, 0.98f);
    }

    /// <summary>
    /// Generates a Two Fingers / Peace pose (index and middle extended).
    /// </summary>
    public static HandPose CreateTwoFingersPeace(float wristX = 0.5f, float wristY = 0.7f)
    {
        var point = CreateIndexPoint(wristX, wristY);
        var landmarks = point.Landmarks.ToArray();

        // Also extend middle finger
        landmarks[(int)HandLandmarkType.MiddleFingerMcp] = new(HandLandmarkType.MiddleFingerMcp, wristX, wristY - 0.13f, 0f);
        landmarks[(int)HandLandmarkType.MiddleFingerPip] = new(HandLandmarkType.MiddleFingerPip, wristX, wristY - 0.20f, -0.01f);
        landmarks[(int)HandLandmarkType.MiddleFingerDip] = new(HandLandmarkType.MiddleFingerDip, wristX, wristY - 0.26f, -0.02f);
        landmarks[(int)HandLandmarkType.MiddleFingerTip] = new(HandLandmarkType.MiddleFingerTip, wristX, wristY - 0.31f, -0.03f);

        return new HandPose(landmarks, Handedness.Right, 0.98f);
    }

    /// <summary>
    /// Generates a Pinch pose (Thumb tip touching Index tip).
    /// </summary>
    public static HandPose CreatePinch(float wristX = 0.5f, float wristY = 0.7f)
    {
        var point = CreateIndexPoint(wristX, wristY);
        var landmarks = point.Landmarks.ToArray();

        // Move thumb tip close to index tip
        var indexTip = landmarks[(int)HandLandmarkType.IndexFingerTip];
        landmarks[(int)HandLandmarkType.ThumbTip] = new(
            HandLandmarkType.ThumbTip,
            indexTip.X - 0.02f,
            indexTip.Y + 0.01f,
            indexTip.Z);

        return new HandPose(landmarks, Handedness.Right, 0.98f);
    }

    /// <summary>
    /// Generates a Lateral Palm pose (hand rotated 90 degrees, normal vector pointing sideways).
    /// </summary>
    public static HandPose CreateLateralPalm(float wristX = 0.5f, float wristY = 0.7f)
    {
        var open = CreateOpenHand(wristX, wristY);
        var landmarks = open.Landmarks.ToArray();

        // Rotate landmarks so normal faces X axis instead of -Z axis
        // i.e., index is behind pinky in depth Z
        landmarks[(int)HandLandmarkType.IndexFingerMcp] = new(HandLandmarkType.IndexFingerMcp, wristX, wristY - 0.12f, 0.06f);
        landmarks[(int)HandLandmarkType.PinkyFingerMcp] = new(HandLandmarkType.PinkyFingerMcp, wristX, wristY - 0.10f, -0.06f);

        return new HandPose(landmarks, Handedness.Right, 0.98f);
    }
}
