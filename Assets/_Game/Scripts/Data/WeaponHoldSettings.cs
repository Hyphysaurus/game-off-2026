using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// How the player holds a blaster in third person, as numbers: the test room's rule. The gun is fixed in the right
    /// hand. A pistol: at rest the arm is the animation's and the gun hangs with it; while aim is held, and for a moment
    /// after each shot, the arm comes up and points the gun at the crosshair. A long gun (Weapon > Two Handed): both
    /// hands stay on it; it is carried low across the body and comes up to the shoulder to aim. Lengths are in the
    /// player's own units (the 1.8 body) and scale with the shift. Lives at Resources/WeaponHold; without the asset
    /// these defaults are used.
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

        [Header("A long gun: both hands")]
        [Tooltip("Seconds for both hands to come onto a long gun, or let go of it (the test room: 0.2).")]
        public float holdTime = 0.2f;
        [Tooltip("Which way the right elbow points with a long gun, in the body's own space (x right, y up, z forward).")]
        public Vector3 longElbow = new Vector3(0.8f, -0.7f, -0.2f);
        [Tooltip("Which way the support (left) elbow points.")]
        public Vector3 supportElbow = new Vector3(-0.9f, -0.6f, -0.1f);

        [Header("A long gun: aimed at the shoulder")]
        [Tooltip("Degrees the chest turns to the right so the support shoulder comes forward. The head stays facing forward.")]
        public float aimChestTurn = 35f;
        [Tooltip("Where the end of the stock sits from the right shoulder, in the gun's own axes: x to the gun's right (negative is towards the middle of the chest), y up, z forward.")]
        public Vector3 shoulderPocket = new Vector3(-0.028f, -0.056f, 0.028f);

        [Header("A long gun: carried")]
        [Tooltip("Where the grip sits from the chest bone, in the body's own space: x right, y up, z forward.")]
        public Vector3 carryPosition = new Vector3(0.11f, -0.15f, 0.19f);
        [Tooltip("The barrel points this many degrees to the left.")]
        public float carryLeftDegrees = 40f;
        [Tooltip("The barrel points this many degrees down.")]
        public float carryDownDegrees = 30f;
        [Tooltip("Degrees the chest turns to the right while carrying.")]
        public float carryChestTurn = 12f;

        [Header("A long gun: the support hand")]
        [Tooltip("The support hand never holds further forward of the grip than this, so short arms can reach it.")]
        public float longestSupportReach = 0.28f;
        [Tooltip("Out of reach, the hold slides back along the barrel towards the grip, but never closer to it than this.")]
        public float closestSupport = 0.094f;
        [Tooltip("How far the palm's middle sits from the gun.")]
        public float palmDepth = 0.028f;
        [Tooltip("Degrees the palm rolls round the barrel towards the support side, so the hand cups the gun from below and beside it. 0 puts the hand flat underneath.")]
        public float palmRollDegrees = 35f;
        [Tooltip("How strongly the support hand's fingers point along the barrel instead of simply following the forearm.")]
        public float fingersAlongBarrel = 0.8f;

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
