using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Every number of the ropes a toy swings on (D-0170), in the toy's own units (a toy is 1.8 tall): taking hold, the
    /// swing, meeting a wall, climbing and sliding, letting go, the hands, the rope's own chain and tube, and the words.
    /// WHY one asset: every Rope and every RopeRider read the same one (Resources/Rope), so a swing feels the same on
    /// any rope in any scene and he tunes it in one place. The rider multiplies lengths and speeds by the player's
    /// size (PlayerMotor.Scale); a rope multiplies them by its own size (its transform's scale).
    /// The numbers come from the Goblin game's ropes (its D-0051, D-0052), converted for the toy's fall of 25.
    /// After his first play (23 Nov): a wider catch (Catch Reach 1, Leave Reach 1.4 with it), a grab that settles at a
    /// usable length (Hang Hold), the hands lower so both arms reach (Hand Offset y 1.42, Min Hold 1.7 with it), and
    /// fingers that close (Grip Curl). A changed default here does NOT change a value already saved in
    /// Resources/Rope.asset: Shift Jam > Traversal > Set Rope Numbers writes them there.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Rope Settings", fileName = "Rope")]
    public sealed class RopeSettings : ScriptableObject
    {
        [Header("Taking hold (own units: a toy is 1.8 tall)")]
        [Tooltip("In the air: the hands catch a rope this near them. 1 = a path 2 wide catches when walking off a ledge toward a rope (0.7 was a path 1.4 wide).")]
        public float catchReach = 1f;
        [Tooltip("The highest the hands may hold: this far below where the rope is tied, so the head stays under what it hangs from. 1.7 keeps the capsule's top (0.38 over the hands) off the ceiling at the widest swing: 1.7 x cos 75 = 0.44.")]
        public float minHold = 1.7f;
        [Tooltip("Where a grab settles: after taking hold higher than this far below where the rope is tied, the toy lets itself down to here by itself, keeping its swing (a rope held just under the ceiling is a tiny pendulum). Sprint pressed on the rope climbs instead and ends it. On a shorter rope it is the rope's end.")]
        public float hangHold = 5f;
        [Tooltip("Seconds the body takes to get onto the rope from where it was.")]
        public float settleSeconds = 0.2f;
        [Tooltip("A rope is only caught after this long off the ground, so walking past one never grabs it.")]
        public float catchAirSeconds = 0.12f;
        [Tooltip("After letting go, and again after landing, the same rope is not caught for this long, even once the hands are clear of it (it swings back past you).")]
        public float regrabSeconds = 0.55f;
        [Tooltip("After a let-go the same rope cannot catch you again until the hands have been this far from it, or you have landed. Without it, letting go while hanging still falls along the rope and is caught again half a second later.")]
        public float leaveReach = 1.4f;
        [Tooltip("Where the hands hold the rope, from the feet: x right, y up, z forward. Live: change it while hanging. The body's arms are 0.52 long from shoulders about 1.3 up: much higher or further out and the upper hand cannot reach the rope.")]
        public Vector3 handOffset = new Vector3(0f, 1.42f, 0.36f);

        [Header("Swinging")]
        [Tooltip("The swing's gravity as a share of the player's own fall (25). Lower = a slower, floatier swing.")]
        public float gravityShare = 0.75f;
        [Tooltip("How hard the stick pushes the swing, own units a second per second, by the camera.")]
        public float push = 7.6f;
        [Tooltip("How fast a swing dies down with no push, per second.")]
        public float damping = 0.28f;
        [Range(10f, 89f), Tooltip("The swing never goes further out than this, degrees from straight down.")]
        public float maxAngle = 75f;
        [Tooltip("Degrees a second the body turns to face where the stick pushes.")]
        public float turnSpeed = 360f;

        [Header("Meeting a wall")]
        [Range(0f, 1f), Tooltip("Share of the speed into a wall that comes back out: the toy kicks off it. 0 = it just stops.")]
        public float bumpBounce = 0.6f;
        [Tooltip("Slower than this into a wall there is no kick, only a stop (own units a second).")]
        public float bounceFrom = 1f;
        [Tooltip("The body counts as stopped by something when it ends this far from where the rope sent it.")]
        public float bumpSlack = 0.03f;
        [Tooltip("The fastest the rope may move the body, own units a second (getting on from a step away).")]
        public float maxPullSpeed = 40f;

        [Header("Up and down")]
        [Tooltip("Climbing with Sprint held, own units a second. A shorter rope swings quicker.")]
        public float climbSpeed = 1.4f;
        [Tooltip("Sliding with Crouch held, and the speed the toy lets itself down to Hang Hold after a high grab. A longer rope swings wider.")]
        public float slideSpeed = 3f;

        [Header("Letting go")]
        [Tooltip("Extra upward speed on a Jump let-go.")]
        public float releaseLift = 2.8f;
        [Tooltip("In the air after a let-go: how hard the stick bends the path, own units a second per second.")]
        public float flingSteer = 5f;
        [Tooltip("...and how fast the fling's speed wears off, per second.")]
        public float flingDrag = 0.15f;

        [Header("Hands")]
        [Tooltip("How far apart the two hands hold, along the rope (the right one above).")]
        public float handsApart = 0.16f;
        [Tooltip("Seconds the hands take to go on and come off.")]
        public float handsFade = 0.2f;
        [Tooltip("How far the fingers close round the rope, degrees a finger bone: x the three gripping fingers, y the first finger, z the thumb across the palm. 0 leaves the hands open.")]
        public Vector3 gripCurl = new Vector3(70f, 70f, 35f);

        [Header("The rope itself (looks only)")]
        [Tooltip("Length of one link of the chain: shorter = bendier, a little more work.")]
        public float linkLength = 0.25f;
        [Tooltip("Gravity on the rope's own chain, own units a second per second.")]
        public float ropeGravity = 18.75f;
        [Tooltip("Air drag on the chain, per second: higher = the whip dies sooner.")]
        public float ropeDrag = 0.68f;
        [Range(0.1f, 1f), Tooltip("How hard the links hold their length each pass: below 1 the rope gives a little.")]
        public float linkStiffness = 0.9f;
        [Range(1, 30), Tooltip("Passes a step at holding the links' length: more = less stretchy.")]
        public int solverPasses = 14;
        [Tooltip("Chain steps a second (fixed, so it looks the same at any frame rate).")]
        public float stepsPerSecond = 120f;
        [Tooltip("The tube's radius.")]
        public float thickness = 0.06f;
        [Range(3, 12), Tooltip("Sides of the tube.")]
        public int sides = 6;
        [Tooltip("Radius of the unseen handle E finds the rope by. Thin on purpose: a wide one would win E from things beside it.")]
        public float gripRadius = 0.25f;
        [Tooltip("Slower than this everywhere...")]
        public float restSpeed = 0.05f;
        [Tooltip("...for this long, and a free rope stops being worked out until someone takes it.")]
        public float restSeconds = 1.5f;

        [Header("Words (his to change)")]
        [Tooltip("Title of the tip shown the first time.")]
        public string keysTitle = "On a rope";
        [TextArea, Tooltip("The tip shown the first time a rope is grabbed. Bold words show as key caps.")]
        public string keysHint = "<b>Move</b> swing   <b>Shift</b> climb   <b>C</b> slide   <b>Space</b> let go";

        private static RopeSettings _defaults;

        /// <summary>Resources/Rope, or the defaults above when the asset is missing (a scene opened before
        /// Shift Jam > Traversal > Make Rope Piece has run still gets a rope that works).</summary>
        public static RopeSettings Load()
        {
            var asset = Resources.Load<RopeSettings>("Rope");
            if (asset != null) return asset;
            if (_defaults == null) { _defaults = CreateInstance<RopeSettings>(); _defaults.hideFlags = HideFlags.HideAndDontSave; }
            return _defaults;
        }
    }
}
