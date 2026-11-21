using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The third-person camera, as numbers. Lengths are in the player's own units and scale with the shift. The test
    /// room's metres turn into these at 0.99: the body here is the same rig, fitted at 0.99 of the pack's size (the
    /// capsules differ, 1.8 against 1.92, but the bodies do not). Three views, eased into one another: wide with empty hands (Framing), over the shoulder with a weapon
    /// drawn, and closer while aim is held. The kid multipliers apply to all three.
    /// </summary>
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

        [Header("Weapon drawn: over the shoulder (the test room's numbers as body heights of a 1.8 body)")]
        [Tooltip("How far behind the pivot the camera sits with a weapon drawn (the test room: 1.9 m).")]
        public float armedDistance = 1.88f;
        [Tooltip("Sideways offset of the pivot with a weapon drawn (the test room: 0.8 m).")]
        public float armedShoulderOffset = 0.79f;
        [Tooltip("How much higher the pivot sits with a weapon drawn (the test room: 0.3 m, its pivot 1.7 m up).")]
        public float armedRise = 0.28f;
        [Tooltip("Field of view with a weapon drawn, degrees, at either size (the test room: 75). The wide view keeps Field Of View and Kid Field Of View.")]
        public float armedFieldOfView = 75f;

        [Header("Aim held")]
        [Tooltip("How far behind the pivot the camera sits while aim is held (the test room: 1.3 m).")]
        public float aimDistance = 1.29f;
        [Tooltip("Field of view while aim is held, degrees, at either size (the test room: 55).")]
        public float aimFieldOfView = 55f;
        [Tooltip("How quickly the camera moves between the views. Higher is snappier.")]
        public float viewBlend = 8f;

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

        [Header("Follow behind (on foot, D-0124)")]
        [Tooltip("While the player moves with no mouse or stick look, the camera eases round behind the way they are moving (his \"it doesn't turn behind me\"). Never while armed: the body already faces the camera's way.")]
        public bool followBehind = true;
        [Min(0f), Tooltip("Seconds after the last mouse or stick look before the camera starts following.")]
        public float followDelay = 0.5f;
        [Min(0f), Tooltip("Degrees a second the camera turns at a full run (less when slower).")]
        public float followRate = 150f;
        [Range(0f, 180f), Tooltip("Running back toward the camera by more than this many degrees: it does not turn (no whip round when backing up).")]
        public float followMaxAngle = 135f;
        [Range(0f, 1f), Tooltip("Below this share of the run speed it does not follow (shuffling about leaves the view alone).")]
        public float followMinSpeed = 0.15f;

        [Header("Collision")]
        [Tooltip("Smallest radius of the ball the camera is swept with to keep it out of walls.")]
        public float collisionRadius = 0.2f;
        [Tooltip("The ball is never smaller than the reach of the near plane's corners times this: 1 just touches, less and the view cuts through a wall the camera rests against.")]
        public float wallGap = 1.15f;

        [Header("Own body")]
        [Tooltip("Pushed in by a wall the camera ends up inside the player. The body is hidden while the camera is closer than this to the body's middle line (feet to head). The test room: 0.6 m.")]
        public float hideBodyWithin = 0.56f;
        [Range(0f, 1f), Tooltip("Once hidden, the body comes back only when the camera is this share further out, so it does not flicker at the limit.")]
        public float hideBodySlack = 0.2f;
        [Tooltip("Seconds the body takes to fade out or back in. 0 = at once.")]
        public float bodyFadeSeconds = 0.15f;
    }
}
