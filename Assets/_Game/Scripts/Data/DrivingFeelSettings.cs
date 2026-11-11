using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// How driving feels beyond the handling: the camera's kicks, the speed lines, the wind, skid and landing sounds, the
    /// controller rumble and the tyre trails. DrivingFeel and its parts read it. Loaded from Resources/DrivingFeel when
    /// that asset exists (Create > Shift Jam > Driving Feel Settings, saved as Resources/DrivingFeel); the defaults here
    /// are the shipped values when it does not, so nothing has to be authored. Speeds are shares of the driven car's own
    /// top speed (1 = top speed, past 1 = boosted); fall and impact speeds are units per second at car scale 1, the same
    /// measure as ArcadeCarSettings.crashMinSpeed. The handling itself is in ArcadeCarSettings and the camera's steady
    /// speed look in VehicleCameraSettings.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Driving Feel Settings", fileName = "DrivingFeel")]
    public sealed class DrivingFeelSettings : ScriptableObject
    {
        /// <summary>The name the asset is loaded by: Resources/DrivingFeel.</summary>
        public const string ResourceName = "DrivingFeel";

        [Header("Boost kick (a burst or nitro starting)")]
        [Tooltip("Top speed multiplier that counts as a full-strength boost (nitro is 1.5); smaller bursts kick proportionally less.")] public float boostFull = 1.5f;
        [Tooltip("Field of view kicked out at a full boost, degrees. Higher = a harder lurch.")] public float boostFovPunch = 5f;
        [Tooltip("Seconds the kick takes to ease back.")] public float boostFovSeconds = 0.5f;
        [Range(0f, 1f), Tooltip("Camera shake at a full boost (1 = the camera's strongest).")] public float boostShake = 0.25f;
        public float boostShakeSeconds = 0.3f;

        [Header("Landing (by the speed the car comes down into the ground)")]
        [Tooltip("Softer landings than this do nothing (a kerb is about 3; a jump off a 20 degree ramp at top speed lands at about 11).")] public float landFrom = 3f;
        [Tooltip("A landing this hard or harder is full strength (14 is where a crash starts to hurt).")] public float landFull = 14f;
        [Range(0f, 1f), Tooltip("Camera shake at a full-strength landing.")] public float landShake = 0.8f;
        public float landShakeSeconds = 0.35f;

        [Header("Crash (by the speed into the surface)")]
        [Tooltip("Softer knocks than this do nothing (the car itself reports nothing under ArcadeCarSettings.feelMinImpact).")] public float crashFrom = 5f;
        [Tooltip("A hit this hard or harder is full strength.")] public float crashFull = 24f;
        [Range(0f, 1f), Tooltip("Camera shake at a full-strength crash.")] public float crashShake = 1f;
        public float crashShakeSeconds = 0.45f;

        [Header("Speed lines (screen overlay)")]
        [Tooltip("Share of top speed where the lines start to show.")] public float linesFrom = 0.8f;
        [Tooltip("Share of top speed where they reach linesAlpha.")] public float linesFullAt = 1f;
        [Range(0f, 1f), Tooltip("How strong the lines are at top speed without a boost.")] public float linesAlpha = 0.22f;
        [Range(0f, 1f), Tooltip("How strong they are while a boost or nitro pushes.")] public float linesBoostAlpha = 0.6f;
        [Tooltip("Share of top speed below which even a boost shows no lines (a launch from standing); they are at boost strength by linesFrom.")] public float linesBoostFrom = 0.4f;
        public Color linesColour = Color.white;
        [Tooltip("Their colour while boosting.")] public Color linesBoostColour = new Color(0.72f, 0.94f, 1f);
        [Tooltip("Times a second the streaks jump to a new place. Higher = busier.")] public float linesFlicker = 18f;
        [Tooltip("How fast they fade in and out, per second.")] public float linesFade = 5f;
        [Range(8, 200), Tooltip("How many streaks are drawn into the overlay (read once, when it is first made).")] public int linesCount = 80;
        [Tooltip("Where streaks begin, as a share of the way from the middle of the screen to its corner: nearest (x) and furthest (y). Lower = lines reach further in.")]
        public Vector2 linesInner = new Vector2(0.36f, 0.64f);

        [Header("Wind (a noise loop made in code unless a clip is given)")]
        [Tooltip("Optional loop to use instead of the generated wind.")] public AudioClip windClip;
        [Tooltip("Share of top speed where the wind starts to be heard.")] public float windFrom = 0.25f;
        [Range(0f, 1f), Tooltip("Wind volume at top speed.")] public float windVolume = 0.3f;
        [Range(0f, 1f), Tooltip("Volume added at a full boost's speed.")] public float windBoostVolume = 0.25f;
        [Tooltip("Wind pitch standing still (x) and at a full boost's speed (y).")] public Vector2 windPitch = new Vector2(0.7f, 1.5f);

        [Header("Tyre skid (a noise loop made in code unless a clip is given)")]
        [Tooltip("Optional loop to use instead of the generated skid.")] public AudioClip skidClip;
        [Tooltip("Sideways speed, as a share of top speed, where the skid starts to be heard (tyre smoke starts at 0.25).")] public float skidFrom = 0.2f;
        [Tooltip("Sideways speed, as a share of top speed, where it is at full volume.")] public float skidFull = 0.5f;
        [Range(0f, 1f)] public float skidVolume = 0.35f;
        [Tooltip("Skid pitch standing still (x) and at top speed (y).")] public Vector2 skidPitch = new Vector2(0.85f, 1.2f);

        [Header("Landing thump (made in code unless a clip is given)")]
        [Tooltip("Optional one-shot to use instead of the generated thump.")] public AudioClip landClip;
        [Range(0f, 1f), Tooltip("Volume at a full-strength landing.")] public float landVolume = 0.8f;
        [Range(0f, 0.3f), Tooltip("Each thump's pitch is 1 plus or minus up to this.")] public float landPitchSpread = 0.08f;
        [Tooltip("How fast the loops' volumes follow, per second.")] public float soundFade = 6f;

        [Header("Controller rumble")]
        [Tooltip("Off = the pad never rumbles while driving.")] public bool rumble = true;
        [Range(0f, 1f), Tooltip("Engine hum at top speed (the heavy motor).")] public float rumbleEngine = 0.07f;
        [Range(0f, 1f), Tooltip("Added while a boost or nitro pushes (the light motor).")] public float rumbleBoost = 0.3f;
        [Range(0f, 1f), Tooltip("Added at a full slide on the ground (the light motor).")] public float rumbleSkid = 0.15f;
        [Range(0f, 1f), Tooltip("Kick when a boost starts, at a full boost.")] public float rumbleBoostKick = 0.5f;
        [Range(0f, 1f), Tooltip("Kick at a full-strength landing.")] public float rumbleLand = 0.8f;
        [Range(0f, 1f), Tooltip("Kick at a full-strength crash.")] public float rumbleCrash = 1f;
        [Tooltip("Seconds a kick takes to die away.")] public float rumbleKickSeconds = 0.25f;

        [Header("Tyre trails (units at car scale 1)")]
        [Tooltip("Width of a skid mark. Higher = fatter marks.")] public float skidWidth = 0.3f;
        [Tooltip("Seconds a skid mark stays before it has faded.")] public float skidSeconds = 2f;
        [Tooltip("A plain slide's mark (dark rubber).")] public Color skidColour = new Color(0.07f, 0.07f, 0.08f, 0.5f);
        [Tooltip("The mark while drift level one is banked.")] public Color driftOneColour = new Color(0.35f, 0.85f, 1f, 0.9f);
        [Tooltip("The mark while drift level two is banked.")] public Color driftTwoColour = new Color(1f, 0.55f, 0.15f, 0.95f);
        [Tooltip("How far above the ground the marks are drawn, so they do not flicker into it.")] public float trailLift = 0.04f;
        [Tooltip("Width of the streak behind each rear wheel while boosting.")] public float boostTrailWidth = 0.26f;
        [Tooltip("Seconds a boost streak lasts (its length is this times the speed).")] public float boostTrailSeconds = 0.3f;
        public Color boostTrailColour = new Color(0.6f, 0.92f, 1f, 0.75f);
        [Tooltip("A wheel that moves further than this in one frame was teleported (a portal, a reset): its trail is cut instead of drawn across the room.")]
        public float trailBreak = 6f;

        /// <summary>Resources/DrivingFeel, or the built-in defaults when there is no such asset.</summary>
        public static DrivingFeelSettings Load()
        {
            var settings = Resources.Load<DrivingFeelSettings>(ResourceName);
            if (settings == null)
            {
                settings = CreateInstance<DrivingFeelSettings>();
                settings.name = ResourceName + " (defaults)";
            }
            return settings;
        }
    }
}
