using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Every number of the paint: the ball and its shell, the paint inside it, the splat it leaves (spread, stretch, the
    /// spatter ring, the drops, the runs, the broken shell), how long paint stays and how much a scene keeps, the grey
    /// toys that take their colour back, the Colour Corner and the soldiers that rub the colour off again.
    /// Ported from the Godot test room (paint_ball.gd, paint_splat.gd, colourable.gd, colour_corner.gd, mischief_bot.gd)
    /// with its numbers. Lengths and speeds are for a toy-size shooter (scale 1); a bigger shooter's paint is the same
    /// paint, scaled up with it. Loaded from Resources/PaintSettings; Shift Jam > Paint > Build Paint makes the asset
    /// and fills in the pictures, materials and sounds (the numbers on an existing asset are kept).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Paint Settings", fileName = "PaintSettings")]
    public sealed class PaintSettings : ScriptableObject
    {
        /// <summary>The name the asset is loaded by: Resources/PaintSettings.</summary>
        public const string ResourceName = "PaintSettings";

        /// <summary>One paint picture: its colour-and-shape image and, for thick paint, its bumps.</summary>
        [Serializable]
        public struct Look
        {
            public Texture2D albedo;
            [Tooltip("None for flat paint (shell flakes, the skip mark).")] public Texture2D normal;
        }

        /// <summary>The splat pictures drawn for one impact angle (or for soft things).</summary>
        [Serializable]
        public sealed class Family
        {
            [Tooltip("The start of its file names: head_0_albedo.png is family 'head'.")] public string name;
            [Tooltip("The impact angle it was drawn for, degrees between the path and the surface. Ignored for the soft family.")] public float drawnAt;
            [Tooltip("Where the ball first touched, down the picture: 0 the top row, 1 the bottom. Downrange is down the picture.")] [Range(0f, 1f)] public float anchor = 0.5f;
            [Tooltip("Its pictures: the first half calm (slow hits), the second half rich (fast hits: long spines, satellite spots).")] public Look[] looks = Array.Empty<Look>();
        }

        private static PaintSettings _loaded;

        /// <summary>The settings asset, or built-in defaults (with no pictures, so no paint shows) when it is missing.</summary>
        public static PaintSettings Load()
        {
            if (_loaded == null) _loaded = Resources.Load<PaintSettings>(ResourceName);
            if (_loaded == null)
            {
                Debug.LogWarning("PAINT no Resources/" + ResourceName + ": run Shift Jam > Paint > Build Paint. Using defaults with no pictures.");
                _loaded = CreateInstance<PaintSettings>();
            }
            return _loaded;
        }

        [Header("The ball")]
        [Tooltip("The paint inside. Each ball takes one.")]
        public Color[] fillColours =
        {
            new Color(1f, 0.25f, 0.55f), new Color(0.2f, 0.8f, 1f), new Color(1f, 0.85f, 0.15f),
            new Color(0.5f, 0.9f, 0.25f), new Color(1f, 0.5f, 0.1f), new Color(0.65f, 0.4f, 1f),
        };
        [Tooltip("The shell, seen in flight. Each ball takes one that is not its paint's hue.")]
        public Color[] shellColours =
        {
            new Color(0.96f, 0.93f, 0.82f), new Color(1f, 0.78f, 0.1f), new Color(1f, 0.55f, 0.1f),
            new Color(1f, 0.2f, 0.6f), new Color(0.6f, 0.95f, 0.2f), new Color(0.35f, 0.7f, 1f), new Color(0.08f, 0.08f, 0.1f),
        };
        [Tooltip("A shell colour counts as 'not the paint's hue' when its hue is this far from it (0..0.5 round the colour wheel)...")] public float shellHueApart = 0.12f;
        [Tooltip("...or when it has hardly any hue at all: saturation under this (cream, black).")] public float shellPlainBelow = 0.25f;
        [Tooltip("The shell when no listed one is unlike the paint.")] public Color shellFallback = new Color(0.96f, 0.93f, 0.82f);
        [Tooltip("A real paint ball's width at toy size.")] public float realDiameter = 0.02f;
        [Tooltip("The speed into a hard surface that breaks half the shells.")] public float shellStrength = 10f;
        [Tooltip("Each shell is up to this share stronger or weaker than the last.")] [Range(0f, 0.9f)] public float strengthSpread = 0.15f;
        [Tooltip("Soft things need this many times the speed to break a shell.")] public float softStrength = 2f;
        [Tooltip("A shell never breaks under this share of its strength, and always from this share of it (half the time in the middle).")] public Vector2 breakRange = new Vector2(0.5f, 1.5f);
        [Tooltip("A ball never breaks slower than this into a surface: it skips off if it still can, and otherwise it is spent (gone, no paint, the hit does not count).")] public float minBreakSpeed = 2f;
        [Tooltip("On: glancing and slow hits can skip the ball off whole. A scene's PaintSplats can switch skips off for itself.")] public bool skips = true;
        [Tooltip("A ball skips off at most this many times; whatever it meets next breaks it.")] public int mostSkips = 2;
        [Tooltip("A skip keeps this share of the speed into the surface (turned back out of it)...")] [Range(0f, 1f)] public float skipKeepIn = 0.35f;
        [Tooltip("...and this share of the speed along it.")] [Range(0f, 1f)] public float skipKeepAlong = 0.8f;
        [Tooltip("After a skip the ball starts again this many of its own widths off the surface.")] public float skipLift = 1f;
        [Tooltip("Multiplies the weapon's damage for a paint ball.")] public float damageScale = 1f;
        [Tooltip("Multiplies the weapon's shove for a paint ball.")] public float knockScale = 1f;
        [Tooltip("On: the air slows the ball like a real one (about 18 units per second squared at 32 for a toy-size ball).")] public bool realDrag;
        [Tooltip("Real drag: the air's density, kg/m3.")] public float airDensity = 1.2f;
        [Tooltip("Real drag: a ball's drag coefficient.")] public float dragCoefficient = 0.47f;
        [Tooltip("Real drag: a paint ball's density, kg/m3.")] public float ballDensity = 1180f;
        [Tooltip("The streak behind the ball: its length...")] public float trailLength = 0.9f;
        [Tooltip("...its width...")] public float trailWidth = 0.035f;
        [Tooltip("...and how solid it is (it wears the shell's colour).")] [Range(0f, 1f)] public float trailAlpha = 0.6f;
        [Tooltip("What a ball's ray, a drop's ray and the far-side search can meet.")] public LayerMask hitMask = ~0;

        [Header("The paint (a real fluid: the splat's size comes from these)")]
        [Tooltip("Density, kg/m3.")] public float fillDensity = 1120f;
        [Tooltip("How thick it is, Pa s. Thicker paint spreads less.")] public float fillViscosity = 0.1f;
        [Tooltip("Surface tension, N/m.")] public float surfaceTension = 0.045f;
        [Tooltip("How it wets a surface, degrees: 0 spreads flat, 90 beads up.")] public float contactAngle = 45f;
        [Tooltip("Multiplies every splat's size after the spread formula (1 = the formula's own size; 1.6 is his 'expand the splatter impact').")] public float spreadScale = 1.6f;
        [Tooltip("Under this speed into the surface the formula is held: slower drops would spread wider again.")] public float slowestSpread = 0.5f;
        [Tooltip("Seconds paint stays.")] public float splatLife = 45f;
        [Tooltip("Paint fades out over the last seconds of its life.")] public float fadeTime = 3f;

        [Header("The splat")]
        [Tooltip("The splat pictures by impact angle (head-on to glancing). Filled by the builder.")] public Family[] families = Array.Empty<Family>();
        [Tooltip("The pictures for soft things: the paint soaks in. Filled by the builder.")] public Family soft = new Family { name = "soft", anchor = 0.5f };
        [Tooltip("The dense core spans this share of a splat picture's width.")] public float coreShare = 0.36f;
        [Tooltip("Splats on soft things are this share of the size.")] public float softScale = 0.6f;
        [Tooltip("Splats turn from calm to rich pictures around this speed into the surface...")] public float richSpeed = 18f;
        [Tooltip("...from this share of it (never rich) to this share of it (always rich).")] public Vector2 richRange = new Vector2(0.6f, 1.4f);
        [Tooltip("A path flatter than this (degrees to the surface) stretches the paint no more than this would.")] public float flattest = 8f;
        [Tooltip("Within this many degrees of square-on a hit has no downrange: the splat turns any way.")] public float squareOn = 85f;
        [Tooltip("A splat spreads from this share of its size to full...")] [Range(0f, 1f)] public float spreadStart = 0.35f;
        [Tooltip("...in this many seconds.")] public float spreadTime = 0.08f;
        [Tooltip("Ours, not the test room's: the splat and its shell swell this share past their full size as they finish spreading (0.15 = to 115 %), then settle back. 0 = no pop: they stop at full size, as in the test room.")] [Range(0f, 0.5f)] public float spreadPop = 0.15f;
        [Tooltip("...and settle back to full size in this many seconds. 0 = no pop.")] public float spreadSettle = 0.12f;
        [Tooltip("The broken shell covers this many core widths.")] public float flakesShare = 1.2f;
        [Tooltip("Shell flakes are drawn over the paint: decals with a higher number are drawn later.")] public int flakesDrawOrder = 1;

        [Header("How deep paint reaches (it wraps the drawn model, which can sit a little off its hit shape)")]
        [Tooltip("Paint always reaches at least half this share of the core's width above the hit and as far into what it hit.")] public float depth = 0.4f;
        [Tooltip("Paint reaches this far above the hit (for a ball reachBall wide; it scales with the ball).")] public float reachUp = 0.05f;
        [Tooltip("Paint reaches into what it hit as far as the far side of the shape that was hit, but never more than this (for a ball reachBall wide).")] public float reachIn = 0.7f;
        [Tooltip("The ball width those two are for.")] public float reachBall = 0.02f;
        [Tooltip("The least a splat, a skip mark and a drop's spot reach either way.")] public Vector3 leastReach = new Vector3(0.01f, 0.005f, 0.004f);
        [Tooltip("Finding the far side of what was hit: the most rays to cast...")] public int farSideRays = 8;
        [Tooltip("...and the step past each face.")] public float farSideStep = 0.001f;
        [Tooltip("Paint fades on faces turned away from it: full up to the first angle, gone at the second (degrees, URP's decal angle fade; 180 for the first switches it off).")]
        public Vector2 angleFade = new Vector2(0f, 62f);

        [Header("The skip mark")]
        [Tooltip("The faint smear a skipped ball leaves. Filled by the builder.")] public Look skipMark;
        [Tooltip("A skipped ball's mark is this many ball widths across.")] public float markWidth = 1.2f;
        [Tooltip("A mark is never stretched longer than this many times its width.")] public float markLongest = 6f;

        [Header("The spatter ring (small spots flung round each splat)")]
        [Tooltip("The drop pictures, for the ring and for drops that land. Filled by the builder.")] public Look[] drops = Array.Empty<Look>();
        [Tooltip("The drop's spot spans this share of a drop picture's width.")] public float dropCoreShare = 0.36f;
        [Tooltip("The fewest and the most spots (more the harder the hit).")] public Vector2Int spatterCount = new Vector2Int(5, 12);
        [Tooltip("How far out from the splat's middle, in half-widths of the splat.")] public Vector2 spatterOut = new Vector2(0.6f, 1.5f);
        [Tooltip("How big, in core widths.")] public Vector2 spatterSize = new Vector2(0.07f, 0.2f);
        [Tooltip("How long they stretch outward.")] public Vector2 spatterStretch = new Vector2(1f, 2.6f);
        [Tooltip("How far the ring leans downrange on an angled hit, in half-widths per unit of stretch.")] public float spatterLean = 0.5f;
        [Tooltip("A spot spreads this many times as slowly as the splat...")] public float spatterSpreadSlow = 1.5f;
        [Tooltip("...starting up to this many seconds after it.")] public float spatterDelay = 0.05f;
        [Tooltip("Soft things throw this share of the spots.")] [Range(0f, 1f)] public float softSpatter = 0.4f;

        [Header("The drops thrown off a break")]
        [Tooltip("The fewest and the most drops.")] public Vector2Int dropCount = new Vector2Int(6, 14);
        [Tooltip("The speed into the surface that throws the most (drops and spatter).")] public float fullSpraySpeed = 40f;
        [Tooltip("Each drop's width, as a share of the ball's.")] public Vector2 dropSize = new Vector2(0.1f, 0.26f);
        [Tooltip("A drop keeps this share of the ball's speed along the surface...")] public Vector2 dropCarry = new Vector2(0.3f, 0.7f);
        [Tooltip("...and is kicked out across it by this share of the speed into it...")] public Vector2 dropOut = new Vector2(0.08f, 0.25f);
        [Tooltip("...and up off it by this share.")] public Vector2 dropUp = new Vector2(0.04f, 0.12f);
        [Tooltip("Soft things throw this share of the drops.")] [Range(0f, 1f)] public float softDrops = 0.5f;
        [Tooltip("The most drops in the air at once in a scene.")] public int mostDropsInAir = 256;
        [Tooltip("Seconds a drop may fly before it is dropped.")] public float dropFlight = 1.5f;
        [Tooltip("How fast drops fall, units per second squared at toy size.")] public float dropGravity = 9.8f;
        [Tooltip("A drop starts its own width plus this off the surface.")] public float dropLift = 0.002f;

        [Header("Runs down a steep face")]
        [Tooltip("The run pictures (a trickle ending in a bead). Filled by the builder.")] public Look[] runs = Array.Empty<Look>();
        [Tooltip("Paint runs down faces whose normal points lower than this (0.5: steeper than 60 degrees).")] public float runSteepness = 0.5f;
        [Tooltip("Under an overhang steeper than this (the length of 'down' along the face) paint drips off instead of running.")] public float runOverhang = 0.6f;
        [Tooltip("The fewest and the most runs.")] public Vector2Int runCount = new Vector2Int(1, 3);
        [Tooltip("A run's picture is this many times as long as it is wide; a run keeps that shape as it grows, so its bead stays round.")] public float runShape = 4f;
        [Tooltip("How long a run grows, in core widths.")] public Vector2 runLength = new Vector2(0.5f, 0.88f);
        [Tooltip("The seconds it takes to get there.")] public Vector2 runTime = new Vector2(1f, 2.5f);
        [Tooltip("Runs leave this far out to the core's rim...")] public float runStart = 0.85f;
        [Tooltip("...fan up to this many degrees either side of straight down...")] public float runFan = 35f;
        [Tooltip("...each this many degrees off even...")] public float runJitter = 8f;
        [Tooltip("...and start up to this many seconds after the splat has spread.")] public float runDelay = 0.3f;
        [Tooltip("A growing run is never thinner than this.")] public float runThinnest = 0.001f;

        [Header("Broken shell")]
        [Tooltip("The shell flake pictures, laid on the splat. Filled by the builder.")] public Look[] flakes = Array.Empty<Look>();

        [Header("How much paint a scene keeps")]
        [Tooltip("The most paint decals at once: splats, broken shell, runs and skip marks. Past it the oldest fades out.")] public int mostPaint = 560;
        [Tooltip("The most drop and spatter spots at once. Past it the oldest fades out; a drop never pushes out other paint.")] public int mostDrops = 300;
        [Tooltip("Paint pushed out by the budget fades over this many seconds...")] public float pushedFade = 0.5f;
        [Tooltip("...a drop, only a speck, over this many.")] public float pushedDropFade = 0.15f;

        [Header("The burst on a break (shell bits, and a mist of the paint)")]
        [Tooltip("Shell bits thrown.")] public int burstAmount = 22;
        [Tooltip("Their speed.")] public float burstSpeed = 5f;
        [Tooltip("Their size, for a ball burstBall wide (they shrink with a smaller ball).")] public float burstSize = 0.04f;
        [Tooltip("The ball width burstSize is for.")] public float burstBall = 0.12f;
        [Tooltip("How fast the bits fall.")] public float burstGravity = 9f;
        [Tooltip("Seconds a bit lasts.")] public float burstSeconds = 0.45f;
        [Tooltip("The bits fly within this many degrees of straight off the surface.")] public float burstCone = 60f;
        [Tooltip("The slowest bit, as a share of the speed; the smallest, as a share of the size.")] public Vector2 burstLeast = new Vector2(0.4f, 0.5f);
        [Tooltip("The paint mist: this share of the shell bits' count...")] public float mistShare = 1.25f;
        [Tooltip("...thrown this many times as fast...")] public float mistSpeed = 1.6f;
        [Tooltip("...each this many times as big.")] public float mistSize = 1.4f;
        [Tooltip("The most bits in the air at once.")] public int mostBits = 600;

        [Header("Looks (filled by the builder)")]
        [Tooltip("The decal material every paint material is copied from (ShiftJam/PaintDecal).")] public Material decalMaterial;
        [Tooltip("How glossy wet paint is (0 dull, 1 a mirror). Godot's roughness 0.15.")] [Range(0f, 1f)] public float wetGloss = 0.85f;
        [Tooltip("How much thick paint's bumps replace the surface's own.")] [Range(0f, 1f)] public float normalBlend = 1f;
        [Tooltip("Paint further than this from the camera is not drawn.")] public float drawDistance = 1000f;
        [Tooltip("The ball in flight, drops in the air and the bits: a ball mesh.")] public Mesh beadMesh;
        [Tooltip("Drops in the air: a lit particle material that takes each drop's colour.")] public Material dropMaterial;
        [Tooltip("Burst bits: an unlit particle material that takes each bit's colour.")] public Material bitsMaterial;

        [Header("Sounds (filled by the builder; Case Portman, kept out of git)")]
        public AudioClip[] fireClips = Array.Empty<AudioClip>();
        public AudioClip[] hitClips = Array.Empty<AudioClip>();
        [Tooltip("Each sound plays up to this many times higher or lower.")] public float pitchSpread = 1.1f;
        [Range(0f, 1f)] public float hitVolume = 1f;
        [Tooltip("0 = heard the same everywhere, 1 = fully placed in the room.")] [Range(0f, 1f)] public float soundInRoom = 0.6f;

        [Header("Grey toys (Colourable)")]
        [Tooltip("The shader a grey toy's materials are swapped to (ShiftJam/Greyable). Filled by the builder.")] public Shader greyableShader;
        [Tooltip("What 'no colour' looks like: the brightness of the true colour, pulled toward this grey...")] public Color grey = new Color(0.62f, 0.62f, 0.66f);
        [Tooltip("...by this much.")] [Range(0f, 1f)] public float greyPull = 0.45f;
        [Tooltip("With no hit count of its own, a thing needs one paint hit per this many units of its longest side...")] public float sizePerHit = 4f;
        [Tooltip("...at least and at most this many.")] public Vector2Int hitsRange = new Vector2Int(1, 6);
        [Tooltip("The glow in its own colour when paint lands: how strong on a hit, and on the hit that finishes it.")] public Vector2 flash = new Vector2(0.4f, 1f);
        [Tooltip("How bright the glow is.")] public float flashGain = 1.6f;
        [Tooltip("Seconds the glow lasts.")] public float flashSeconds = 0.5f;

        [Header("Colour Corner")]
        [Tooltip("Screen saturation when everything is grey, and when everything is restored (1 = as drawn).")] public Vector2 saturation = new Vector2(0.7f, 1.1f);
        [Tooltip("The room lights' strength when everything is grey, and when everything is restored (1 = as built).")] public Vector2 light = new Vector2(0.7f, 1.25f);
        [Tooltip("The corner is done at this share of all its colour.")] [Range(0f, 1f)] public float doneShare = 0.999f;
        [Tooltip("The colours of the burst when one thing is fully restored.")]
        public Color[] confetti = { new Color(1f, 0.3f, 0.5f), new Color(0.2f, 0.8f, 1f), new Color(1f, 0.85f, 0.2f), new Color(0.5f, 0.9f, 0.3f) };
        [Tooltip("The burst over one restored thing: bits, speed, size, seconds.")] public Vector4 restoredBurst = new Vector4(18f, 7f, 0.12f, 0.7f);
        [Tooltip("The burst over every thing when the corner is done: bits, speed, size, seconds.")] public Vector4 finishedBurst = new Vector4(10f, 8f, 0.14f, 0.9f);
        public AudioClip restoreClip;
        public AudioClip finishedClip;

        [Header("The soldiers that rub the colour off")]
        [Tooltip("Seconds a soldier spends on one thing.")] public float smudgeSeconds = 3f;
        [Tooltip("How much colour it takes each second.")] public float smudgePerSecond = 0.12f;
        [Tooltip("It never walks further than this from where it started.")] public float roam = 24f;
        public float walkSpeed = 1.1f;
        [Tooltip("How quickly it turns to the way it walks (per second).")] public float turnSpeed = 6f;
        [Tooltip("It starts rubbing this far from a thing's edge.")] public float reachGap = 1.4f;
        [Tooltip("It only goes for things with more colour than this.")] [Range(0f, 1f)] public float leastColour = 0.05f;
        [Tooltip("It picks one of this many nearest coloured things.")] public int nearestChoices = 4;
        [Tooltip("It feels this far ahead for something in the way...")] public float feelAhead = 1.6f;
        [Tooltip("...at this height above its feet.")] public float feelHeight = 1f;
        [Tooltip("Seconds it steps round what is in the way...")] public float avoidSeconds = 0.9f;
        [Tooltip("...leaning this much to the side.")] public float avoidLean = 1.5f;
        [Tooltip("Not this far in this many seconds: it gives up on that thing.")] public Vector2 stuck = new Vector2(0.5f, 2.5f);
        [Tooltip("Seconds it stops when hit.")] public float hitPause = 0.4f;
        [Tooltip("Seconds it lies knocked down before it gets up.")] public float downSeconds = 6f;
        [Tooltip("Seconds it takes to tip over.")] public float toppleSeconds = 0.3f;
        [Tooltip("How fast it falls, units per second squared.")] public float soldierGravity = 25f;
    }
}
