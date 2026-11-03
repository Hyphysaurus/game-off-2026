using UnityEngine;

namespace ShiftJam
{
    /// <summary>The over-the-shoulder camera, as numbers. Distances are for kid size and scale with the shift.</summary>
    [CreateAssetMenu(menuName = "Shift Jam/Camera Settings", fileName = "CameraSettings")]
    public sealed class CameraSettings : ScriptableObject
    {
        [Header("Framing (metres at kid size)")]
        public float distance = 4.5f;
        [Tooltip("Height above the player's feet the camera orbits around.")]
        public float pivotHeight = 1.3f;
        [Tooltip("Sideways offset of the pivot, positive = player sits left of centre.")]
        public float shoulderOffset = 0.4f;
        public float fieldOfView = 60f;

        [Header("Kid size")]
        [Tooltip("Distance at kid size as a share of the scaled toy distance. 1 = same framing scaled up (the camera lives in the walls); 0.5 = half as far back.")]
        [Range(0.2f, 1f)] public float kidDistanceMultiplier = 0.5f;
        [Tooltip("Field of view at kid size. Blends from Field Of View during a shift.")]
        public float kidFieldOfView = 70f;
        [Tooltip("Near clip plane at kid size; it shrinks with the player so toy-size views do not clip.")]
        public float nearClip = 0.08f;

        [Header("Look")]
        public float minPitch = -25f;
        public float maxPitch = 70f;
        public float startPitch = 15f;
        [Tooltip("Degrees per mouse pixel.")]
        public float mouseSensitivity = 0.1f;
        [Tooltip("Degrees per second at full stick.")]
        public float padSensitivity = 200f;
        public bool invertY;

        [Header("Collision")]
        [Tooltip("Radius of the probe that keeps the camera out of walls.")]
        public float collisionRadius = 0.2f;
        public float minDistance = 0.3f;
    }
}
