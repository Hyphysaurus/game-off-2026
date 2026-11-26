using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The build effects made with Artificer (D-0230): the SHIFT title building itself out of its pixel cubes on the title
    /// screen (TitleBuildIn), and the toy coming apart where it fell and building itself again at the checkpoint
    /// (RebuildOnReset, instead of the black cut), and a Flat Zone's S-H-I-F-T letter bursting into its cubes when it is
    /// found (LetterBurst). One asset: Resources/BuildFx, made by Shift Jam > FX > Land Build Effects (D-0230) when it is
    /// missing (never overwritten), his to tune in the Inspector. Each effect reads it when it starts, so a number changed
    /// in Play shows the next time one plays.
    /// Title distances are metres of the menu model (the title is about 1.5 m wide, 0.7 m tall); the toy's are body
    /// heights (the toy's own height, or the kid's), so one set of numbers fits both sizes; a letter's are letter heights.
    /// OFF SWITCHES: Title On (the title shows at once, as before), Rebuild On (the old black cut at every reset) and
    /// Letter Burst On (the old pop).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Build FX Settings", fileName = "BuildFx")]
    public sealed class BuildFxSettings : ScriptableObject
    {
        public const string ResourceName = "BuildFx";

        /// <summary>The order the pieces set off in.</summary>
        public enum Order { FromPoint, AlongPath, Random }
        /// <summary>Where a title cube comes from: all of them out of one point, along curves; or each from its own
        /// place round the title, straight in.</summary>
        public enum TitleFlight { FromPoint, FromAround }
        /// <summary>How the toy is cut into pieces: its parts as modelled, each patch of its texture, or each flat facet.</summary>
        public enum Pieces { Parts, TexturePatches, Facets }
        /// <summary>What covers the camera's jump from where the toy fell to the checkpoint.</summary>
        public enum Cover { Cut, ShiftWarp, BlackDip }
        /// <summary>Which end of the toy builds first.</summary>
        public enum ToyOrder { FeetUp, HeadDown, Random }

        [Header("The title builds itself (D-0230)")]
        [Tooltip("The SHIFT letters fly in cube by cube on the title screen before the menu's slabs come up. Off = the title shows at once, as before.")]
        public bool titleOn = true;
        [Tooltip("Plays every time the title opens. Off = once per game launch: a return to the title shows it built.")]
        public bool titleEveryTime;
        [Tooltip("The course's sounds (Data/CourseSounds): its Music In (the seconds the title music takes to rise) times the build when Follow Music is on. The maker fills it.")]
        public CourseSounds courseSounds;
        [Tooltip("The last cube lands as the title music reaches full (CourseSounds > Music In, 2.5 s). Off, or without Course Sounds: Title Seconds.")]
        public bool followMusic = true;
        [Min(0.2f), Tooltip("Seconds from the first cube setting off to the last one landing, when the build does not follow the music.")]
        public float titleSeconds = 2.5f;
        [Min(0f), Tooltip("Seconds of empty title before the first cube sets off (the first frames after a scene load are long; a little wait keeps them from jumping the start).")]
        public float titleDelay = 0.15f;
        [Tooltip("Seconds one cube takes to fly to its place: each takes a time between these two (smallest, largest).")]
        public Vector2 titlePieceSeconds = new Vector2(0.45f, 0.8f);

        [Tooltip("The order the cubes set off in: nearest a point first (Title Order Point), along a line (Title Order Path: the cubes nearest its start first), or at random.")]
        public Order titleOrder = Order.AlongPath;
        [Tooltip("For From Point: metres across and up from the title's middle. The cubes nearest it set off first.")]
        public Vector2 titleOrderPoint = new Vector2(0f, -0.35f);
        [Tooltip("For Along Path: points across and up from the title's middle, metres, in order. The cubes go in the order their nearest point on the line comes (left to right by default).")]
        public Vector2[] titleOrderPath = { new Vector2(-0.9f, 0.1f), new Vector2(0f, -0.1f), new Vector2(0.9f, 0.1f) };

        [Tooltip("Where the cubes come from: all out of one point along curves (From Point), or each from its own place round the title, straight in (From Around).")]
        public TitleFlight titleFlight = TitleFlight.FromPoint;
        [Tooltip("For From Point: the point, metres across and up from the title's middle (below the title: they rise into it).")]
        public Vector2 titleFlightPoint = new Vector2(0f, -0.75f);
        [Tooltip("For From Point: how far toward the viewer that point is, metres (the cubes come out of the screen at you a little).")]
        public float titleFlightToward = 0.25f;
        [Min(0f), Tooltip("For From Point: the cubes leave from anywhere in a box this wide round the point, metres. 0 = all from the very point.")]
        public float titleSpread = 0.3f;
        [Min(0f), Tooltip("For From Point: how far each curve first heads straight at the title before it bends to its cube's place, metres. 0 = straight lines.")]
        public float titleCurveStart = 0.5f;
        [Min(0f), Tooltip("For From Point: how far a curve overshoots its cube's place and swings back in, metres. 0 = it comes straight in.")]
        public float titleCurveEnd = 0.05f;
        [Tooltip("For From Around: how far out each cube starts, metres from its place, away from the title's middle (smallest, largest).")]
        public Vector2 titleAround = new Vector2(0.5f, 1.2f);

        [Tooltip("The most a cube is turned when it sets off, degrees about each axis (each cube a random turn up to this), unwinding as it lands.")]
        public Vector3 titleSpin = new Vector3(360f, 360f, 180f);
        [Range(0f, 1f), Tooltip("A cube's size as it sets off, as a share of its own: it grows to full as it lands. 1 = full size the whole way.")]
        public float titleStartScale = 0.2f;
        [Tooltip("How a cube moves along its way, its turn unwinding with it: 0 to 1 over its flight. Steep at the start and flat at the end = it shoots out and settles softly.")]
        public AnimationCurve titleLanding = new AnimationCurve(new Keyframe(0f, 0f, 2.6f, 2.6f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("The toy rebuilds itself after a fall (D-0230)")]
        [Tooltip("At a reset the toy comes apart where it fell and builds itself at the checkpoint before control returns. Off = the short black cut, as before.")]
        public bool rebuildOn = true;
        [Tooltip("The kid comes apart and builds itself too (it is the same body, bigger). Off = the kid's resets keep the black cut.")]
        public bool rebuildKid = true;
        [Tooltip("How the body is cut into pieces: its parts as modelled (fewest, biggest), each patch of its texture (the default: clothes, face, shoes come off as they are coloured), or each flat facet (most, smallest).")]
        public Pieces rebuildPieces = Pieces.TexturePatches;

        [Header("  Coming apart (where it fell)")]
        [Min(0.01f), Tooltip("Seconds over which the pieces let go, one after another. Short = it bursts at once.")]
        public float apartSeconds = 0.08f;
        [Tooltip("Seconds a piece flies before it is gone (it shrinks away at the end): each a time between these two (smallest, largest).")]
        public Vector2 apartPieceSeconds = new Vector2(0.6f, 0.95f);
        [Tooltip("How fast a piece flies off, body heights a second (smallest, largest).")]
        public Vector2 apartSpeed = new Vector2(2f, 4.5f);
        [Tooltip("The point the pieces burst away from, body heights from the feet: across (right), up, forward. Below the feet = they all fly up and out.")]
        public Vector3 apartFrom = new Vector3(0f, -0.25f, 0f);
        [Tooltip("How fast a piece tumbles, degrees a second about each axis (each piece a random spin up to this).")]
        public Vector3 apartSpin = new Vector3(540f, 540f, 540f);
        [Min(0f), Tooltip("How fast the pieces fall back, body heights a second each second.")]
        public float apartGravity = 7f;
        [Range(0f, 1f), Tooltip("How much of its speed a piece keeps when it hits the floor or a wall.")]
        public float apartBounce = 0.35f;
        [Tooltip("What the pieces bounce off (the player's own layers are always left out).")]
        public LayerMask apartBounceOn = Physics.DefaultRaycastLayers;
        [Range(0f, 1f), Tooltip("When in its flight a piece starts to shrink away, as a share of it (0.6 = the last 40 %).")]
        public float apartShrinkFrom = 0.6f;
        [Min(0f), Tooltip("Seconds of nothing after the last piece is gone, before the move to the checkpoint.")]
        public float apartHold = 0.12f;

        [Header("  The move to the checkpoint")]
        [Tooltip("What covers the camera's jump to the checkpoint: a plain cut, the shift's own warp on the screen, or a quick black dip.")]
        public Cover moveCover = Cover.ShiftWarp;
        [Min(0f), Tooltip("Seconds the warp or the dip lasts (the dip: half to black, half back). 0 = a plain cut.")]
        public float moveSeconds = 0.45f;

        [Header("  Building again (at the checkpoint)")]
        [Min(0.05f), Tooltip("Seconds from the first piece setting off to the last one setting off.")]
        public float gatherSeconds = 0.55f;
        [Tooltip("Seconds one piece takes to fly to its place: each a time between these two (smallest, largest).")]
        public Vector2 gatherPieceSeconds = new Vector2(0.35f, 0.6f);
        [Tooltip("Which end of the body builds first: from the feet up, from the head down, or at random.")]
        public ToyOrder gatherOrder = ToyOrder.FeetUp;
        [Tooltip("The point the pieces gather round, body heights from the feet: across, up, forward. Each piece starts out beyond its place, away from this point.")]
        public Vector3 gatherFrom = new Vector3(0f, 0.5f, 0f);
        [Tooltip("Stretches the way out from that point, per axis (across, up, forward): 1, 1, 1 = from all round; 1, 0, 1 = flat round the body; 0, 1, 0 = straight up and down.")]
        public Vector3 gatherSpread = new Vector3(1f, 0.6f, 1f);
        [Tooltip("How far out a piece starts, body heights from its place (smallest, largest).")]
        public Vector2 gatherDistance = new Vector2(0.8f, 1.6f);
        [Tooltip("The most a piece is turned when it sets off, degrees about each axis, unwinding as it lands.")]
        public Vector3 gatherSpin = new Vector3(360f, 360f, 360f);
        [Range(0f, 1f), Tooltip("A piece's size as it sets off, as a share of its own: it grows to full as it lands.")]
        public float gatherStartScale = 0.3f;
        [Tooltip("How a piece moves along its way, 0 to 1 over its flight (steep then flat = it snaps in and settles).")]
        public AnimationCurve gatherLanding = new AnimationCurve(new Keyframe(0f, 0f, 2.2f, 2.2f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("A found S-H-I-F-T letter bursts into its cubes (D-0230)")]
        [Tooltip("A Flat Zone letter taken by the toy bursts into its pixel cubes and leaves its ghost at once. Off = the pop (a punch and a flash), as before.")]
        public bool letterBurstOn = true;
        [Min(0.01f), Tooltip("Seconds over which the cubes let go, one after another. Short = it bursts at once.")]
        public float letterBurstSeconds = 0.06f;
        [Tooltip("Seconds a cube flies before it is gone (it shrinks away at the end): each a time between these two (smallest, largest).")]
        public Vector2 letterCubeSeconds = new Vector2(0.5f, 0.9f);
        [Tooltip("How fast a cube flies off, letter heights a second (smallest, largest).")]
        public Vector2 letterSpeed = new Vector2(0.8f, 2.2f);
        [Tooltip("How fast a cube tumbles, degrees a second about each axis (each a random spin up to this).")]
        public Vector3 letterSpin = new Vector3(720f, 720f, 720f);
        [Min(0f), Tooltip("How fast the cubes fall, letter heights a second each second.")]
        public float letterGravity = 3f;
        [Range(0f, 1f), Tooltip("How much of its speed a cube keeps when it hits the floor or a wall (they bounce off what the toy's pieces bounce off: Apart Bounce On).")]
        public float letterBounce = 0.4f;
        [Range(0f, 1f), Tooltip("When in its flight a cube starts to shrink away, as a share of it.")]
        public float letterShrinkFrom = 0.55f;

        private static BuildFxSettings _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Forget() => _loaded = null;

        /// <summary>Resources/BuildFx, or the defaults above when there is no such asset (both effects on, the title on its
        /// own seconds). Loaded once and kept.</summary>
        public static BuildFxSettings Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<BuildFxSettings>(ResourceName);
            if (_loaded == null)
            {
                _loaded = CreateInstance<BuildFxSettings>();
                _loaded.name = ResourceName + " (defaults)";
                _loaded.hideFlags = HideFlags.DontSave;
            }
            return _loaded;
        }

        /// <summary>The seconds from the first title cube setting off to the last one landing: the title music's rise, or
        /// Title Seconds.</summary>
        public float TitleSpan => followMusic && courseSounds != null && courseSounds.musicIn > 0.2f ? courseSounds.musicIn : titleSeconds;
    }
}
