using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// How the player holds a blaster, as numbers: the test room's rule. Third person: the gun is fixed in the right
    /// hand. A pistol: at rest the arm is the animation's and the gun hangs with it; while aim is held, and for a moment
    /// after each shot, the arm comes up and points the gun at the crosshair. A long gun (Weapon > Two Handed): both
    /// hands stay on it; it is carried low across the body and comes up to the shoulder to aim. First person: the gun
    /// sits low in front of the eye, the body stands under the eye with its hands on it, and aim brings the gun's own
    /// sight to the middle of the view. Lengths are in the
    /// player's own units and scale with the shift; the test room's metres turn into these at 0.99 (the same rig,
    /// fitted at 0.99 of the pack's size). Lives at Resources/WeaponHold; without the asset
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
        [Tooltip("A long gun: how far to either side of the body's facing it can point, degrees.")]
        public float maxTurn = 80f;
        [Tooltip("One-handed (a pistol, the flashlight): how far the right arm points across the body, to the left, degrees. A real arm gets about this far before the shoulders have to turn; past it the body turns instead (D-0136).")]
        public float maxTurnAcross = 35f;
        [Tooltip("One-handed: how far across the body it points when pointing fully up (Max Pitch Up), degrees. In between it narrows from Max Turn Across: pointed up and across, a real arm would cross the face, so the shoulders turn sooner (D-0137).")]
        public float maxTurnAcrossUp = 10f;
        [Tooltip("One-handed: how far the right arm points out to its own side, degrees.")]
        public float maxTurnOut = 100f;
        [Header("Waist twist, one-handed, third person (D-0141)")]
        [Tooltip("The body does not turn to the aim: the waist twists this share of the way toward it, and the arm reaches the rest from the twisted chest.")]
        [Range(0f, 1f)] public float twistShare = 0.5f;
        [Tooltip("The most the waist twists to the left (the tool across the body), degrees.")]
        public float maxTwistAcross = 45f;
        [Tooltip("The most the waist twists to the right (the tool out to its own side), degrees.")]
        public float maxTwistOut = 50f;
        [Tooltip("How quickly the twist follows the aim (higher = snappier).")]
        public float twistRate = 10f;

        [Header("Keeping the hand out of the body")]
        [Tooltip("The body seen from above, an oval round the hips: half its width (the test room: 0.24 m).")]
        public float bodyHalfWidth = 0.238f;
        [Tooltip("How far the oval's front is from its middle (the test room: 0.17 m).")]
        public float bodyHalfDepth = 0.168f;
        [Tooltip("The chest seen from above, an oval round the chest bone, from the hips up to the neck: half its width. A one-handed arm's elbow and forearm are kept out of it (D-0136); the hand goes further forward instead.")]
        public float chestHalfWidth = 0.15f;
        [Tooltip("How far the chest oval's front is from the chest bone.")]
        public float chestHalfDepth = 0.12f;
        [Tooltip("The head kept clear of a one-handed arm's fist and forearm (D-0137): a ball this far above the head bone...")]
        public float headCentreUp = 0.1f;
        [Tooltip("...this wide (its radius, including the room a fist needs). Inside it, the hand goes further forward.")]
        public float headClear = 0.2f;

        [Header("The gun in the fist")]
        [Tooltip("Where the middle of a gun's grip sits from the wrist, along the hand's own directions: x along the knuckles, y towards the thumb's side, z the way the palm faces. Rig metres. Measured in the test room (its fist centre on the Playroom hand).")]
        public Vector3 fistCentre = new Vector3(0.0772f, -0.0041f, 0.0483f);
        [Tooltip("Which way the barrel points in the hand, the same three directions. Measured in the test room: mostly along the knuckles, tipped towards the palm.")]
        public Vector3 barrelInHand = new Vector3(0.8963f, 0.1406f, 0.4207f);
        [Tooltip("Which way the gun's top points in the hand. Measured in the test room: mostly to the thumb's side, tipped away from the palm.")]
        public Vector3 topInHand = new Vector3(0.126f, 0.8287f, -0.5453f);
        [Tooltip("From the point a long gun's carry and shoulder positions are measured to, to the middle of its grip, in the gun's axes (the test room's fist centre on its hand prop bone).")]
        public Vector3 gripFromHolder = new Vector3(0f, -0.02f, -0.012f);
        [Tooltip("Extra turn of every gun in the hand, degrees, on top of the hold above.")]
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
        public Vector3 shoulderPocket = new Vector3(-0.03f, -0.059f, 0.03f);

        [Header("A long gun: carried")]
        [Tooltip("Where the grip sits from the chest bone, in the body's own space: x right, y up, z forward.")]
        public Vector3 carryPosition = new Vector3(0.119f, -0.158f, 0.198f);
        [Tooltip("The barrel points this many degrees to the left.")]
        public float carryLeftDegrees = 40f;
        [Tooltip("The barrel points this many degrees down.")]
        public float carryDownDegrees = 30f;
        [Tooltip("Degrees the chest turns to the right while carrying.")]
        public float carryChestTurn = 12f;

        [Header("A long gun: the support hand")]
        [Tooltip("The support hand never holds further forward of the grip than this, so short arms can reach it.")]
        public float longestSupportReach = 0.297f;
        [Tooltip("Out of reach, the hold slides back along the barrel towards the grip, but never closer to it than this.")]
        public float closestSupport = 0.099f;
        [Range(0.9f, 0.98f), Tooltip("First person: the body moves towards the gun until the support hand's wrist is within this share of the left arm's length, so the hold never has to slide back along the barrel as you step or look (D-0165). Higher: the body moves less and the arm is straighter. Lower: the wrist bends more (under 0.96 the SMG's passes its limit and the hand starts to rock).")]
        public float supportReach = 0.97f;
        [Tooltip("First person: looking up, the body stops coming towards the gun for the support hand between these two angles above level (degrees): at the first it still comes all the way, past the second not at all, and the hold slides back along the barrel as it used to. Looking steeply up, the body would otherwise rise into the view (the neck and the collar).")]
        public Vector2 supportReachLookUp = new Vector2(40f, 60f);
        [Tooltip("How far the palm's middle sits from the gun.")]
        public float palmDepth = 0.03f;
        [Tooltip("Degrees the palm rolls round the barrel towards the support side, so the hand cups the gun from below and beside it. 0 puts the hand flat underneath.")]
        public float palmRollDegrees = 35f;
        [Tooltip("How strongly the support hand's fingers point along the barrel instead of simply following the forearm.")]
        public float fingersAlongBarrel = 0.8f;
        [Range(0f, 1f), Tooltip("How much of the support hand's roll the forearm takes up (the test room: 0.6). Without it the sleeve pinches at the cuff, because the hand turns and the forearm does not.")]
        public float forearmRoll = 0.6f;
        [Tooltip("The furthest the support hand rolls at the wrist, degrees (the test room: 80). Asked for more, the hand sits a little off the gun instead.")]
        public float wristRoll = 80f;
        [Tooltip("The furthest it bends at the wrist, degrees (the test room: 55).")]
        public float wristBend = 55f;

        [Header("The fingers")]
        [Tooltip("Seconds for the fingers to close round the grip, or open (the test room: 0.15).")]
        public float gripTime = 0.15f;
        [Tooltip("Degrees each bone of the three gripping fingers bends towards the palm (the test room: 70).")]
        public float fingerCurl = 70f;
        [Tooltip("The trigger finger bends less, so it rests on the trigger instead of wrapping the grip (the test room: 35).")]
        public float triggerCurl = 35f;
        [Tooltip("Degrees the thumb folds across the grip (the test room: 25).")]
        public float thumbCurl = 25f;
        [Tooltip("A long gun: degrees each bone of the support hand's four fingers bends under the barrel (the test room: 40). Its thumb is left as the animation has it.")]
        public float supportCurl = 40f;

        [Header("A handle in the fist (melee, the flashlight)")]
        [Tooltip("Where the middle of a handle sits from the wrist, along the hand's own directions (x along the knuckles, y towards the thumb's side, z the way the palm faces). Rig metres. The middle of the closed fist, as a gun's grip.")]
        public Vector3 handleCentre = new Vector3(0.0772f, -0.0041f, 0.0483f);
        [Tooltip("Which way the blade or the beam leaves the fist, the same three directions: a handle runs through the closed fingers, out past the thumb, and a held tool is cocked forward at the wrist, so half to the thumb's side and half along the knuckles. (Straight to the thumb's side, a tool pointed ahead bent the wrist 90 degrees.)")]
        public Vector3 handleInHand = new Vector3(1f, 1f, 0f);
        [Tooltip("Which way the held thing's top (its +Y) points in the hand: back along the knuckles (pointed ahead, the knuckles lean down and forward, the thumb up and forward).")]
        public Vector3 handleTopInHand = new Vector3(-1f, 0f, 0f);
        [Tooltip("Degrees each finger bone bends round a handle: x the three gripping fingers, y the index finger (no trigger: it wraps too), z the thumb.")]
        public Vector3 handleCurl = new Vector3(70f, 70f, 35f);
        [Header("A tool in the fist (the flashlight, D-0132)")]
        [Tooltip("Degrees a held tool leaves the fist further to the thumb's side than a blade does (Handle In Hand, turned towards the thumb). Higher: its handle end dips under the wrist instead of lying along the forearm, and the wrist cocks a little more to point it.")]
        public float toolTilt = 10f;
        [Tooltip("A tool with no Grip marker is held at its handle end: how far in from that end the middle of the fist closes, rig metres. About half a fist, so the end just clears the little finger. (Before: a quarter of its length in, and the rest ran back into the wrist.)")]
        public float toolEndInFist = 0.05f;
        [Tooltip("A worn thing (the foam finger): where its root sits from the wrist, the hand's own directions, rig metres. The hand is folded away inside it.")]
        public Vector3 wornCentre = new Vector3(0.06f, 0f, 0f);
        [Tooltip("A worn thing: which way its +Z (the pointing finger) points in the hand. Along the knuckles, as the hand's own fingers.")]
        public Vector3 wornAlong = new Vector3(1f, 0f, 0f);
        [Tooltip("A worn thing: which way its +Y points in the hand. The thumb's side.")]
        public Vector3 wornTop = new Vector3(0f, 1f, 0f);
        [Range(0f, 1f), Tooltip("A melee swing turns the arm at the shoulder; this share of it is the wrist's, on top (a flick at the end of the arm).")]
        public float swingWrist = 0.3f;

        [Header("A reload")]
        [Tooltip("Third person: where the gun sits from the chest bone while reloading, in the body's own space: x right, y up, z forward (the test room: 0.04 to the right, 0.12 down, 0.24 forward).")]
        public Vector3 reloadPosition = new Vector3(0.04f, -0.119f, 0.238f);
        [Tooltip("First person: where it sits from the camera while reloading: x right, y up, z forward.")]
        public Vector3 reloadViewPosition = new Vector3(0.05f, -0.198f, 0.297f);
        [Tooltip("Degrees the barrel tips up while reloading (the test room: 25).")]
        public float reloadUpDegrees = 25f;
        [Tooltip("Degrees the gun rolls so its magazine comes round towards the free hand (the test room: 35).")]
        public float reloadRollDegrees = 35f;

        [Header("The kick of a shot (a gun of recoil 1, the pistol; each gun multiplies these by its own Recoil)")]
        [Tooltip("Degrees the barrel flips up on a shot (the test room: 4). Higher: a bigger flip.")]
        public float recoilUpDegrees = 4f;
        [Tooltip("How far the gun jumps back along its barrel on a shot, in the player's own units (the test room: 0.03 m). Higher: a longer jump.")]
        public float recoilBack = 0.0297f;
        [Tooltip("How quickly the kick settles: each second it falls to 1 / e this many times over (the test room: 14). Higher: snappier.")]
        public float recoilSettle = 14f;
        [Min(0f), Tooltip("Third person (D-0177): degrees the view widens on a shot and eases back, for a gun of recoil 1. The middle of the view does not move, so the aim is not touched. 0 = none.")]
        public float cameraKickFov = 0.7f;
        [Range(0f, 1f), Tooltip("Third person (D-0177): how hard a shot rattles the camera, as a share of the strongest shake (Resources/VehicleCamera > Shake: 2 degrees of tilt at 1), for a gun of recoil 1. It tilts the view a hair, and with it the aim: keep it small (0.05 is a tenth of a degree). 0 = none.")]
        public float cameraKickShake = 0.05f;
        [Min(0f), Tooltip("Third person (D-0177): seconds the camera's kick lasts. 0 = no kick at all, as before.")]
        public float cameraKickSeconds = 0.14f;

        [Header("First person: the sight to the eye")]
        [Tooltip("Seconds for the gun's sight to come up to the eye when aim is held, and to go back down (the test room: 0.15).")]
        public float sightTime = 0.15f;
        [Tooltip("How far in front of the eye the sight sits when aiming down it (the test room: 0.31 m).")]
        public float sightDistance = 0.307f;
        [Tooltip("A gun whose sight is found by its mount and not by its glass: the middle of the sight is this far above the mount (the test room: 0.03 m).")]
        public float sightRise = 0.0297f;
        [Range(0f, 1f), Tooltip("The dot crosshair hides once the sight is this far up: the gun's own sight takes over (the test room: 0.6).")]
        public float dotHidesAt = 0.6f;

        [Header("First person: the body under the eye")]
        [Tooltip("The eye sits this far above the head bone (the test room: 0.2 m).")]
        public float eyeAboveHead = 0.198f;
        [Tooltip("The eye sits this far in front of the head bone (the test room: 0.12 m).")]
        public float eyeForward = 0.119f;
        [Tooltip("Looking straight down, the body stands this much further back still, so the chest stays behind the gun and out of the view (less at a shallower look, none looking level). Ours: the test room's view stops 55 degrees down, this one goes on to 89.")]
        public float backLookingDown = 0.34f;
        [Range(0f, 1f), Tooltip("How much of the look up the chest leans back by, so the shoulders stay behind the gun (the test room: 0.6). 0 keeps the chest upright.")]
        public float chestFollowsView = 0.6f;

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
