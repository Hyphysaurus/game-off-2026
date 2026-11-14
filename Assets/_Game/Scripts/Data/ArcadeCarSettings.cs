using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// How an ArcadeCar drives, as numbers shared by every car that points at the asset. Lengths and speeds are for a car
    /// at transform scale 1 (a toy's car); a car scaled up drives proportionally faster and sits on longer springs.
    /// Forces are written as accelerations, so the car feels the same under the project's strong prop gravity.
    /// Top speed is <see cref="TopSpeed"/> (maxSpeed x topSpeedScale) and the engine's pull follows a curve: strong up to a
    /// knee, then fading toward the top, so the last stretch is earned (<see cref="AccelerationAt"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Arcade Car Settings", fileName = "ArcadeCarSettings")]
    public sealed class ArcadeCarSettings : ScriptableObject
    {
        [Header("Engine (units at car scale 1)")]
        [Tooltip("Base top speed. The car's real top speed is this times topSpeedScale.")] public float maxSpeed = 24f;
        [Tooltip("Top speed = maxSpeed x this (24 x 1.3333 = 32). A scale and not a new maxSpeed so the asset saved with 24 goes faster without being edited. To fold it in later: set maxSpeed to the real top speed and this to 1.")]
        public float topSpeedScale = 1.3333f;
        public float reverseSpeed = 9f;
        [Tooltip("Speeding up, units per second squared (forwards it is shaped by the curve below; reverse uses it as it is).")] public float acceleration = 22f;
        [Tooltip("Pressing against the way you roll, units per second squared.")] public float braking = 45f;
        [Tooltip("Rolling with no throttle, units per second squared.")] public float coastDrag = 6f;

        [Header("Acceleration curve (forwards)")]
        [Tooltip("Pull from a standstill up to the knee, times 'acceleration'. Higher = punchier off the line.")] public float accelLow = 1.3f;
        [Range(0.1f, 0.95f), Tooltip("Share of top speed where the pull starts to fade. Lower = the slow last stretch starts earlier.")] public float accelKnee = 0.7f;
        [Range(0.02f, 1f), Tooltip("Pull left at top speed, times 'acceleration' (it fades in a straight line from the knee). Lower = the top takes longer to reach.")] public float accelAtTop = 0.1f;

        [Header("Steering and grip")]
        [Tooltip("Turn rate at full lock, degrees per second.")] public float steerRate = 120f;
        [Tooltip("Speed (units per second at scale 1) at which steering reaches full rate; slower turns slower, like real wheels.")]
        public float steerFullSpeed = 6f;
        [Tooltip("How fast sideways sliding is cancelled, per second. High = on rails.")] public float grip = 9f;
        [Tooltip("Grip while the handbrake (Jump) is held: low = drift.")] public float handbrakeGrip = 1.5f;
        [Tooltip("Wheel turn shown on the front wheels at full lock, degrees.")] public float visualSteerAngle = 28f;

        [Header("Suspension (units at car scale 1)")]
        [Tooltip("Spring travel each way from rest.")] public float suspensionTravel = 0.18f;
        [Tooltip("Wheel radius if it cannot be measured from the wheel mesh.")] public float wheelRadius = 0.45f;
        [Tooltip("Spring damping, per second.")] public float damping = 7f;
        [Tooltip("Bump stop (D-0038): the last share of the spring's travel that pushes much harder, so a fast ramp foot or a hard landing lifts the body instead of slamming it.")]
        [Range(0.05f, 1f)] public float bumpZone = 0.3f;
        [Tooltip("Bump stop strength at the end of the travel, times the car's weight per wheel (up to three times that past it).")] public float bumpStiffness = 3f;
        [Tooltip("Bump stop damping while it is squeezed, per second: soaks the hit without a bounce back.")] public float bumpDamping = 15f;
        [Tooltip("Turns a car upright in the air, so it lands on its wheels.")] public float airUpright = 4f;
        [Tooltip("Extra pull down at speed, as a share of gravity, full at maxSpeed (the base, not the scaled top): keeps it on the track over crests.")] public float downforce = 0.4f;

        [Header("Air control (no wheel on the ground)")]
        [Tooltip("Nose down/up from the stick's forward/back, radians per second squared at full stick. The self-righting (airUpright) pulls against it: 2.5 against airUpright 4 holds about 40 degrees at most.")] public float airPitch = 2.5f;
        [Tooltip("Turn left/right from the stick, radians per second squared at full stick (the body's spin damping caps it near 60 degrees a second).")] public float airYaw = 2f;
        [Tooltip("With the handbrake (Jump) held in the air the stick rolls the car instead of turning it, radians per second squared at full stick.")] public float airRoll = 2.5f;

        [Header("Landing")]
        [Tooltip("Seconds with no wheel down before touching down counts as a landing (shorter hops over bumps do not).")] public float landMinAir = 0.15f;
        [Tooltip("Clean landing: top speed times this for a moment when the car lands wheels-first, lined up with the ground, with the throttle held. 1 or less = off.")] public float cleanLandingBoost = 1.15f;
        [Tooltip("Clean landing: seconds the little burst lasts.")] public float cleanLandingSeconds = 0.5f;
        [Tooltip("Clean landing: least seconds in the air (a real jump, not a kerb).")] public float cleanLandingMinAir = 0.45f;
        [Range(1f, 45f), Tooltip("Clean landing: most degrees between the car's up and the ground it lands on.")] public float cleanLandingAngle = 18f;

        [Header("Drift boost")]
        [Tooltip("Holding a handbrake slide charges up to two levels; letting go of the handbrake fires a burst.")] public DriftBoostTuning drift = new DriftBoostTuning();

        [Header("Damage")]
        public float health = 150f;
        [Tooltip("Impact speed INTO the surface (units per second at scale 1, along the contact normal) below which a crash does nothing.")] public float crashMinSpeed = 14f;
        [Tooltip("Impacts softer than this (same measure) are not reported to the feedback (camera shake, rumble). No damage is involved.")] public float feelMinImpact = 4f;
        public float crashDamagePerSpeed = 3f;
        [Tooltip("Loose things no heavier than this (cones, toy blocks) are driven through: felt, but they do not damage the car. Another car weighs 4.")] public float smashMass = 1.5f;
        [Tooltip("The most a loose, light thing (smashMass) is felt as, same measure as the impact (crash feel starts at 5, full at 24): a cone is a tap, never a crash (D-0058).")] public float smashFeel = 7f;

        [Header("Walls (D-0058)")]
        [Range(0f, 80f), Tooltip("Hits on a wall (anything fixed and upright) shallower than this many degrees glance: the car slides along it, keeps most of its speed and is not hurt. Steeper hits are crashes. 0 = off.")] public float wallGlanceAngle = 40f;
        [Range(0f, 1f), Tooltip("Share of its speed a glance at the steepest glancing angle costs (less for shallower ones).")] public float wallScrapeLoss = 0.2f;
        [Tooltip("Speed the wall gives back outward on a glance, units per second at scale 1, so the car comes off it instead of rubbing.")] public float wallPushOff = 1.2f;
        [Tooltip("Seconds after touching a wall during which the spin the hit gave is taken away and the nose eases along the wall.")] public float wallSettle = 0.25f;
        [Tooltip("Seconds the nose takes to turn along the wall after a glance. Lower = snappier.")] public float wallAlignSeconds = 0.18f;
        [Tooltip("Speed above which hitting something with health hurts it.")] public float ramMinSpeed = 5f;
        public float ramDamagePerSpeed = 6f;
        public float ramMaxDamage = 80f;
        [Tooltip("Shove given to whatever is rammed, units per second at scale 1.")] public float ramKnockback = 9f;
        [Tooltip("Seconds a wreck sits smoking before it is put back at its start, repaired. 0 = never.")] public float wreckRespawn = 5f;
        [Tooltip("Mending while driving (D-0038): seconds without a hit before health starts to climb back.")] public float regenDelay = 4f;
        [Tooltip("Mending while driving: share of full health mended per second.")] public float regenPerSecond = 0.05f;
        [Tooltip("Smoke from the bonnet below this share of full health, thicker as it drops, thick while wrecked.")] [Range(0f, 1f)] public float smokeBelow = 0.3f;

        [Header("Seat")]
        [Tooltip("Above this speed (units per second at scale 1) E brakes first and lets you out once slow.")] public float maxExitSpeed = 5f;
        [Tooltip("Largest driver scale relative to the car's scale (a kid cannot fold into a toy car).")] public float maxDriverScale = 1.5f;
        [Tooltip("Chase camera pull-back, times the on-foot distance.")] public float chaseDistance = 1.8f;

        [Header("Boosts (D-0037)")]
        [Tooltip("Bursts, nitro, the pull-back launch and the effects' size.")] public BoostTuning boost = new BoostTuning();
        [Tooltip("Pull-back wind: share of gravity pressed on the rear axle at full wind, so the car squats back on its springs.")]
        public float windSquat = 0.35f;

        [Header("Engine sound")]
        [Tooltip("The engine's looping sound. Empty = a low hum made by code (ProceduralClips.Engine); drop a loop here to use a recording instead.")] public AudioClip engineClip;
        [Tooltip("Engine loop pitch standing still (x) and at top speed (y).")] public Vector2 enginePitch = new Vector2(0.7f, 1.6f);
        [Tooltip("Engine loop volume standing still (x) and at top speed (y).")] public Vector2 engineVolume = new Vector2(0.2f, 0.45f);
        [Tooltip("Pitch added for each top speed's worth of speed past the top (a 1.5x boost adds half of this), so a boost is heard.")] public float engineOverPitch = 0.6f;

        /// <summary>The car's real top speed at scale 1, before boosts: maxSpeed x topSpeedScale.</summary>
        public float TopSpeed => maxSpeed * Mathf.Max(0.01f, topSpeedScale);

        /// <summary>The engine's forward pull, units per second squared at scale 1, at <paramref name="speedShare"/> of top
        /// speed: accelLow x acceleration up to the knee, then a straight fade to accelAtTop x acceleration at the top (and
        /// past it, where only a boost pushes).</summary>
        public float AccelerationAt(float speedShare)
        {
            float knee = Mathf.Clamp(accelKnee, 0.1f, 0.95f);
            float k = speedShare <= knee ? accelLow : Mathf.Lerp(accelLow, accelAtTop, (speedShare - knee) / (1f - knee));
            return acceleration * Mathf.Max(0.02f, k);
        }
    }
}
