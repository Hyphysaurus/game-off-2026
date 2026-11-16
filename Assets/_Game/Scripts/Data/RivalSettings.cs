using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>One rival's character: how fast it dares to go and where on the road it likes to be.</summary>
    [Serializable]
    public sealed class RivalProfile
    {
        [Range(0.5f, 1f), Tooltip("Share of the car's top speed it holds on open road. 1 = flat out.")] public float pace = 1f;
        [Range(0.5f, 1.5f), Tooltip("Times the road's suggested speed in bends. Lower = more careful, higher = braver.")] public float cornerPace = 1f;
        [Range(-1f, 1f), Tooltip("Its lane: -1 = hard left, 0 = the middle, 1 = hard right, of the room the walls leave.")] public float lane;
        [Range(0f, 1f), Tooltip("How full the nitro meter must be before it lights it. Lower = it uses nitro sooner.")] public float nitroFrom = 0.35f;

        public RivalProfile() { }
        public RivalProfile(float pace, float cornerPace, float lane, float nitroFrom)
        { this.pace = pace; this.cornerPace = cornerPace; this.lane = lane; this.nitroFrom = nitroFrom; }
    }

    /// <summary>
    /// The race with rivals, as numbers (D-0061): the countdown, how a rival follows the road (RivalDriver), how it keeps
    /// off other cars (RivalLane), each rival's character, the rubber band that keeps the pack near the player, and when
    /// and how a car in trouble is put back on the road (RaceRescue). Loaded from Resources/Rivals; the raceway's builder
    /// makes that asset once and never overwrites it, and the defaults here are used when it is missing. Speeds are
    /// units per second and lengths are units, at car scale 1 (a car is about 4 long and 2.2 wide; top speed is 32).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Rival Settings", fileName = "Rivals")]
    public sealed class RivalSettings : ScriptableObject
    {
        /// <summary>The name the asset is loaded by: Resources/Rivals.</summary>
        public const string ResourceName = "Rivals";

        [Header("The start")]
        [Min(0f), Tooltip("Seconds on the grid before the count begins (the camera settles, the cars sit on their springs).")] public float lineUpSeconds = 1f;
        [Range(1, 5), Tooltip("The count starts from this number.")] public int counts = 3;
        [Min(0.2f), Tooltip("Seconds each number stays up.")] public float countSeconds = 1f;
        [Min(0f), Tooltip("Seconds GO! stays on the screen.")] public float goSeconds = 1f;

        [Header("Countdown sounds (made in code unless a clip is given)")]
        [Tooltip("Optional one-shot for 3, 2, 1 (the default is the drift chime, played low).")] public AudioClip countClip;
        [Tooltip("Optional one-shot for GO (the default is the same chime, played high). The finish line's own start sound plays as well.")] public AudioClip goClip;
        [Range(0f, 1f)] public float countVolume = 0.6f;
        [Tooltip("Pitch of the 3, 2, 1 sound.")] public float countPitch = 0.75f;
        [Tooltip("Pitch of the GO sound.")] public float goPitch = 1.5f;

        [Header("The finish")]
        [Min(0f), Tooltip("Seconds the results card stays up after the player finishes. Then the rivals are put back on the grid.")] public float resultsSeconds = 10f;
        [Min(0f), Tooltip("Seconds a rival that has finished drives on before it is put back on the grid (the player is still racing).")] public float coolDownSeconds = 6f;
        [Range(0.2f, 1f), Tooltip("Share of its racing pace a finished rival drives on at.")] public float coolDownPace = 0.6f;
        [Min(0f), Tooltip("A rival is put back on the grid only when no moving car will reach the grid within this many seconds (so it never appears in front of someone).")] public float homeNotice = 3f;

        [Header("Following the road")]
        [Tooltip("How far ahead along the road a rival aims, standing still.")] public float lookAhead = 7f;
        [Tooltip("Aim this many seconds of its own speed further ahead. Higher = smoother and lazier in bends, lower = twitchier.")] public float lookAheadPerSpeed = 0.35f;
        [Range(2f, 60f), Tooltip("Degrees between its nose and its aim that mean full lock. Lower = sharper steering.")] public float steerFullAngle = 20f;
        [Range(0.3f, 2f), Tooltip("Times the road's suggested speed, for every rival. Lower = the whole pack slows more for bends.")] public float cornerSpeedFactor = 1f;
        [Tooltip("How far ahead it reads the road's speeds, so it slows in time.")] public float brakeLook = 60f;
        [Tooltip("The slowing it counts on when it plans a bend, units per second squared (the car coasts at 6 and brakes at 45).")] public float brakeDecel = 18f;
        [Tooltip("Faster than it wants by this much, it brakes; by less, it only lifts.")] public float brakeOver = 3f;
        [Range(20f, 180f), Tooltip("Pointing this many degrees off the road or more, it crawls round instead of charging.")] public float lostAngle = 70f;
        [Tooltip("The speed it crawls round at when pointing the wrong way.")] public float turnRoundSpeed = 9f;

        [Header("Lanes and other cars")]
        [Tooltip("It stays this far from each wall (half a car is 1.1).")] public float wallMargin = 2.5f;
        [Tooltip("How fast it moves sideways to a new lane, units per second.")] public float laneChange = 6f;
        [Tooltip("The gap it wants, middle to middle, when it passes another car (a car is 2.2 wide).")] public float carClearance = 3.2f;
        [Tooltip("A car's length, bumper to bumper, with a little air.")] public float carLength = 5f;
        [Range(0f, 2f), Tooltip("A car level with it, or behind by up to this many car lengths, still counts as in its way (it does not cut across that car's nose); further back, the car behind is the one that gives way.")] public float giveWay = 1f;
        [Tooltip("A car ahead matters from this far away, standing start.")] public float avoidMin = 10f;
        [Tooltip("...plus this many seconds of the speed it closes at.")] public float avoidSeconds = 1.3f;
        [Tooltip("A car's width: two cars whose middles are closer than this, side to side, are in line.")] public float carWidth = 2.3f;
        [Tooltip("The air it keeps to the bumper of a car it is in line behind.")] public float followAir = 1.5f;
        [Min(0f), Tooltip("It does not sit in line behind a car that is closer than this many seconds of its own speed: it moves to the side (the car ahead may brake).")] public float followSeconds = 0.6f;

        [Header("Nitro")]
        [Tooltip("It lights nitro only when the road ahead allows this many times its top speed (a straight).")] public float nitroRoad = 1.3f;
        [Range(0f, 1f), Tooltip("...and it is already going this share of its top speed.")] public float nitroMinSpeed = 0.6f;
        [Range(0f, 1f), Tooltip("...and its steering is no further over than this.")] public float nitroMaxSteer = 0.25f;

        [Header("Rivals: one character per grid slot, pole first (the slot the player takes goes unused)")]
        [Tooltip("The lanes keep the grid's order from left to right (slot 3, slot 1, slot 2, slot 4), so nobody crosses over at the start. Every bend of the raceway turns left, so a lane further left is a shorter lap (about 19 units for each unit of road): the inside lane has the gentlest pace. The car on pole is the quickest rival: taking the pole car yourself leaves the three gentler ones.")]
        public RivalProfile[] rivals =
        {
            new RivalProfile(1f, 1f, -0.13f, 0.3f),
            new RivalProfile(0.98f, 0.98f, 0.13f, 0.4f),
            new RivalProfile(0.95f, 0.95f, -0.4f, 0.5f),
            new RivalProfile(0.98f, 0.97f, 0.4f, 0.6f),
        };

        [Header("Rubber band (gentle: keeps the pack near the player)")]
        [Tooltip("Off = every rival drives its own race whatever the player does.")] public bool rubberBand = true;
        [Tooltip("The gap to the player, along the road, at which the band pulls its hardest.")] public float rubberDistance = 150f;
        [Range(0f, 0.5f), Tooltip("A rival this far AHEAD of the player eases off by this share of its pace.")] public float rubberEase = 0.1f;
        [Range(0f, 0.5f), Tooltip("A rival this far BEHIND the player pushes by this share (never past the car's own top speed).")] public float rubberPush = 0.06f;

        [Header("Trouble (when a car is put back on the road)")]
        [Tooltip("A rival that has not come this far along the road...")] public float stuckProgress = 3f;
        [Tooltip("...in this many seconds is stuck.")] public float stuckSeconds = 3.5f;
        [Tooltip("Seconds a rival stopped during the race (against a wall, or behind a car with no way round) waits before it backs up.")] public float blockedSeconds = 0.8f;
        [Tooltip("Seconds it backs up for.")] public float reverseSeconds = 0.9f;
        [Tooltip("Slower than this it counts as stopped.")] public float blockedSpeed = 1.5f;
        [Tooltip("Further outside the walls than this counts as off the road.")] public float offRoadMargin = 2f;
        [Tooltip("Lower than the road by this much counts as off the road.")] public float offRoadDrop = 3f;
        [Tooltip("Seconds a rival may be off the road before it is put back.")] public float offRoadSeconds = 1.5f;
        [Tooltip("Seconds the PLAYER's car may be off the road during a race before it is put back at the last gate.")] public float playerOffRoadSeconds = 3f;
        [Tooltip("Seconds a rival may lie on its roof (against the road's own up) before it is put back.")] public float flippedSeconds = 1.5f;
        [Tooltip("Seconds a wrecked car lies in pieces during a race before it is put back whole.")] public float wreckSeconds = 2.5f;

        [Header("Back on the road")]
        [Tooltip("How far above the road a car is set down.")] public float rescueLift = 0.3f;
        [Range(0f, 1f), Tooltip("It is sent on its way at this share of the road's suggested speed (never above its own top speed).")] public float rescueSpeed = 0.5f;
        [Tooltip("No other car may be nearer than this where it is set down; else it is set down further to the side.")] public float rescueClear = 5f;
        [Min(0f), Tooltip("...and no moving car in that lane may be due there within this many seconds (it is not set down in front of someone at speed).")] public float rescueNotice = 1f;
        [Tooltip("The player's car is set down this far past the last gate it took (or the line).")] public float rescueAhead = 8f;

        /// <summary>The character of the rival on grid slot <paramref name="index"/> (the list wraps round when it is short).</summary>
        public RivalProfile Profile(int index)
        {
            if (rivals == null || rivals.Length == 0) return new RivalProfile();
            return rivals[((index % rivals.Length) + rivals.Length) % rivals.Length] ?? new RivalProfile();
        }

        /// <summary>Resources/Rivals, or the built-in defaults when there is no such asset.</summary>
        public static RivalSettings Load()
        {
            var settings = Resources.Load<RivalSettings>(ResourceName);
            if (settings == null)
            {
                settings = CreateInstance<RivalSettings>();
                settings.name = ResourceName + " (defaults)";
            }
            return settings;
        }
    }
}
