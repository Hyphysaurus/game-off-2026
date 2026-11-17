using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// How the player holds a blaster in third person, as numbers: the test room's rule. The gun is fixed in the right
    /// hand. At rest the arm is the animation's and the gun hangs with it. While aim is held, and for a moment after
    /// each shot, the arm comes up and points the gun at the crosshair. Lengths are in the player's own units (the 1.8
    /// body) and scale with the shift. Lives at Resources/WeaponHold; without the asset these defaults are used.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Weapon Hold Settings", fileName = "WeaponHold")]
    public sealed class WeaponHoldSettings : ScriptableObject
    {
        [Header("When the arm is up")]
        [Tooltip("Seconds for the arm to come up. A shot waits for it, so it leaves along the barrel (the test room: 0.12).")]
        public float raiseTime = 0.12f;
        [Tooltip("Seconds for the arm to come back down (the test room: 0.25).")]
        public float lowerTime = 0.25f;
        [Tooltip("Seconds the arm stays up after the last shot (the test room: 1).")]
        public float lowerDelay = 1f;
        [Range(0.5f, 1f), Tooltip("How far up the arm must be before a shot leaves.")]
        public float raisedAt = 0.95f;

        [Header("The arm")]
        [Range(0.3f, 1f), Tooltip("How far the hand reaches, as a share of the arm's length. Below 1 keeps a bend in the elbow.")]
        public float reach = 0.9f;
        [Tooltip("Which way the elbow points, in the body's own space (x right, y up, z forward).")]
        public Vector3 elbowDirection = new Vector3(0.5f, -1f, -0.3f);
        [Tooltip("How far up the gun can point, degrees.")]
        public float maxPitchUp = 60f;
        [Tooltip("How far down, degrees. The camera looks down 80 and the gun follows it.")]
        public float maxPitchDown = 85f;
        [Tooltip("How far to either side of the body's facing the gun can point, degrees.")]
        public float maxTurn = 80f;

        [Header("Keeping the hand out of the body")]
        [Tooltip("The body seen from above, an oval round the hips: half its width (the test room: 0.24 m on a 1.92 m body).")]
        public float bodyHalfWidth = 0.225f;
        [Tooltip("How far the oval's front is from its middle (the test room: 0.17 m).")]
        public float bodyHalfDepth = 0.16f;

        [Header("The gun in the fist")]
        [Tooltip("Extra turn of every gun in the hand, degrees, on top of the hold worked out from the hand's bones (barrel along the knuckles, top towards the thumb).")]
        public Vector3 gripEuler;
        [Tooltip("The barrel is lined up on the target itself, not held parallel to the line from the shoulder. Closer than this many times the barrel's distance from that line, the correction fades out.")]
        public float nearestTrueAim = 4f;

        private static WeaponHoldSettings _defaults;

        /// <summary>Resources/WeaponHold, or the defaults above when the asset is missing.</summary>
        public static WeaponHoldSettings Load()
        {
            var asset = Resources.Load<WeaponHoldSettings>("WeaponHold");
            if (asset != null) return asset;
            if (_defaults == null) { _defaults = CreateInstance<WeaponHoldSettings>(); _defaults.hideFlags = HideFlags.HideAndDontSave; }
            return _defaults;
        }
    }
}
