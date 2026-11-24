using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// One level of the team match's bots (D-0188): how far they see and fire, how fast they react, move and aim, how they
    /// fire their bursts, hunt, help, fall back and dodge. The numbers are the owner's to tune on Resources/BotSkills.
    /// Distances and speeds are at a soldier's own size (a bigger soldier sees, walks and keeps further, as it does
    /// everything else). A "from / to" pair is a range: each time it is needed a number between the two is picked.
    /// </summary>
    [Serializable]
    public sealed class BotSkill
    {
        [Tooltip("The level's name: shown to the host in warm-up, said in the log.")]
        public string name = "Normal";

        [Header("Senses and darts (each soldier gets these as it joins a match)")]
        [Min(0f), Tooltip("How far it sees, units.")] public float sightRange = 20f;
        [Min(0f), Tooltip("How far it fires, units.")] public float fireRange = 16f;
        [Min(0f), Tooltip("Seconds an enemy must stay seen before it reacts.")] public float reactionTime = 0.8f;
        [Min(0f), Tooltip("Health one of its darts takes off.")] public float damage = 9f;

        [Header("Moving")]
        [Min(0.1f), Tooltip("How fast it walks, units a second (a player walks 4, sprints 7).")] public float moveSpeed = 3.6f;
        [Range(0f, 1f), Tooltip("How much of a fight it spends moving: 0 = it stands and shoots, 1 = it never stands still.")] public float strafeShare = 0.7f;
        [Tooltip("Seconds it strafes one way before it changes sides (from / to).")] public Vector2 strafeSeconds = new Vector2(0.7f, 1.8f);
        [Tooltip("The distance it fights at (near / far): it closes in from further than far and backs off from nearer than near, units.")] public Vector2 keepRange = new Vector2(5f, 11f);

        [Header("Aim (degrees off: each dart misses its point by up to this)")]
        [Min(0f), Tooltip("Its error on first sight of a quarry, degrees.")] public float aimErrorFirst = 7f;
        [Min(0f), Tooltip("Its error once settled on that quarry, degrees.")] public float aimErrorSteady = 2.2f;
        [Min(0f), Tooltip("Seconds of unbroken sight of the same quarry for its error to settle from First to Steady.")] public float aimSettleSeconds = 1.6f;
        [Min(0f), Tooltip("Degrees more error for each unit a second the quarry moves.")] public float aimErrorPerSpeed = 0.35f;
        [Range(0f, 1.5f), Tooltip("How much of the quarry's movement during the dart's flight it aims ahead for: 0 = it aims where the quarry is, 1 = where it will be.")] public float lead = 0.5f;

        [Header("Bursts")]
        [Tooltip("Darts in one burst (from / to).")] public Vector2Int burstShots = new Vector2Int(2, 3);
        [Min(0.05f), Tooltip("Seconds between the darts of a burst.")] public float burstGap = 0.32f;
        [Tooltip("Seconds it rests after a burst (from / to).")] public Vector2 burstRest = new Vector2(0.8f, 1.5f);

        [Header("Hunting, helping, looking about")]
        [Min(0f), Tooltip("Seconds it hunts a quarry it lost sight of (going to where it last saw it, then looking about) before it gives up.")] public float searchSeconds = 6f;
        [Min(0f), Tooltip("A teammate fighting this near is helped: it goes to where that teammate's enemy is, units.")] public float helpRange = 18f;
        [Tooltip("Seconds it stops to look left and right at a goal (from / to).")] public Vector2 lookSeconds = new Vector2(1f, 2.5f);

        [Header("Falling back")]
        [Range(0f, 1f), Tooltip("Under fire with less than this share of its health, it runs for cover. 0 = it never falls back.")] public float fallBackBelow = 0.4f;
        [Min(0f), Tooltip("Seconds it holds at cover before it goes back out.")] public float holdSeconds = 4f;
        [Min(1), Tooltip("How many spots round it are tried for cover.")] public int coverTries = 12;
        [Min(0f), Tooltip("The nearest a cover spot may be, units.")] public float coverNear = 4f;
        [Min(0f), Tooltip("The furthest a cover spot may be, units.")] public float coverFar = 10f;
        [Min(0f), Tooltip("Seconds out of fire before it starts to mend.")] public float healAfter = 4f;
        [Min(0f), Tooltip("Health it mends a second at cover. 0 = it never mends.")] public float healPerSecond = 6f;

        [Header("Grenades")]
        [Range(0f, 1f), Tooltip("The chance it notices a grenade whose blast would reach it (rolled once a grenade): 1 = always.")] public float dodgeChance = 0.7f;
        [Min(0f), Tooltip("Seconds from noticing a grenade to starting to run.")] public float dodgeDelay = 0.4f;

        /// <summary>A copy to change without touching this one.</summary>
        public BotSkill Copy() => (BotSkill)MemberwiseClone();

        /// <summary>The starting Easy level (the owner's to tune on the asset).</summary>
        public static BotSkill Easy() => new BotSkill
        {
            name = "Easy", sightRange = 18f, fireRange = 14f, reactionTime = 1.1f, damage = 7f,
            moveSpeed = 3f, strafeShare = 0.35f, strafeSeconds = new Vector2(1f, 2.4f), keepRange = new Vector2(6f, 12f),
            aimErrorFirst = 10f, aimErrorSteady = 4f, aimSettleSeconds = 2.2f, aimErrorPerSpeed = 0.5f, lead = 0.2f,
            burstShots = new Vector2Int(1, 2), burstGap = 0.4f, burstRest = new Vector2(1.2f, 2f),
            searchSeconds = 4f, helpRange = 12f, fallBackBelow = 0.25f, holdSeconds = 3f, healAfter = 4f, healPerSecond = 0f,
            dodgeChance = 0.3f, dodgeDelay = 0.6f,
        };

        /// <summary>The starting Normal level (the field defaults above).</summary>
        public static BotSkill Normal() => new BotSkill();

        /// <summary>The starting Hard level.</summary>
        public static BotSkill Hard() => new BotSkill
        {
            name = "Hard", sightRange = 24f, fireRange = 19f, reactionTime = 0.45f, damage = 10f,
            moveSpeed = 4.3f, strafeShare = 0.95f, strafeSeconds = new Vector2(0.5f, 1.3f), keepRange = new Vector2(4f, 10f),
            aimErrorFirst = 4.5f, aimErrorSteady = 1.2f, aimSettleSeconds = 1f, aimErrorPerSpeed = 0.2f, lead = 0.9f,
            burstShots = new Vector2Int(3, 4), burstGap = 0.26f, burstRest = new Vector2(0.5f, 1f),
            searchSeconds = 8f, helpRange = 26f, fallBackBelow = 0.5f, holdSeconds = 5f, healAfter = 4f, healPerSecond = 8f,
            dodgeChance = 1f, dodgeDelay = 0.25f,
        };
    }

    /// <summary>
    /// The team match's bot levels (D-0188): a list of levels (BotSkill) and the one a match starts on the first time a
    /// PC hosts one. The host goes to the next level in warm-up with a key (TeamMatch), and that PC remembers its pick.
    /// Lives at Resources/BotSkills (made by Shift Jam > Bots > Build Bot Pieces with Easy, Normal and Hard, then never
    /// written again: the numbers are the owner's). A match part can point at another asset; with none at all the match
    /// makes one level from its own four bot numbers.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Bot Skills", fileName = "BotSkills")]
    public sealed class BotSkillSettings : ScriptableObject
    {
        /// <summary>Where the asset is looked for under a Resources folder.</summary>
        public const string ResourceName = "BotSkills";

        [Tooltip("The levels, in the order the host's key goes through them. Starting numbers, yours to tune.")]
        public BotSkill[] levels = { BotSkill.Easy(), BotSkill.Normal(), BotSkill.Hard() };
        [Min(0), Tooltip("The level a match starts on the first time a PC hosts one (0 = the first). After that the PC remembers the host's last pick.")]
        public int startLevel = 1;

        /// <summary>Resources/BotSkills, or null when the project has none (a match then makes its own level).</summary>
        public static BotSkillSettings Load() => Resources.Load<BotSkillSettings>(ResourceName);
    }
}
