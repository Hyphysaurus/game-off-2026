using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Every number of the monsters under the bed (D-0222) and the claw machine's prizes (D-0223): how far they see, how
    /// close they creep, how hard they bump (never a hurt: the toy is only pushed back), how they flinch from a beam and
    /// how long they hide, how long one lies knocked down, their eye glint, their sounds, and the state names each kind's
    /// own Animator uses (the pack's controllers have no parameters: states are played by name). Also the maker's layout
    /// numbers (where the cave's monsters stand, how big a prize is). Lengths and speeds are toy units (a toy is about 2
    /// tall) for a monster at scale 1 and grow with its scale.
    /// Loaded from Resources/MonsterSettings; Shift Jam > House > Land Bedroom Monsters (D-0222, D-0223) makes the asset
    /// (the numbers on an existing asset are kept; a kind missing from the list is added with its defaults).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Monster Settings", fileName = "MonsterSettings")]
    public sealed class MonsterSettings : ScriptableObject
    {
        /// <summary>The name the asset is loaded by: Resources/MonsterSettings.</summary>
        public const string ResourceName = "MonsterSettings";

        /// <summary>One kind of monster: its Animator's state names, its eyes, and the pack prefab the maker builds it from.</summary>
        [Serializable]
        public sealed class Kind
        {
            [Tooltip("The kind's name: the monster component looks itself up by it, and the maker names the prefab after it (Prefabs/Monsters/<Name>Monster).")]
            public string name;
            [Tooltip("The pack prefab the maker builds this kind's prefab variant from (the maker only; it stays in the git-ignored pack folder).")]
            public string packPrefab;
            [Tooltip("Animator state: standing about in the dark.")] public string idle = "IdleNormal";
            [Tooltip("Animator state: on guard, the toy in sight but out of reach (at the end of its leash).")] public string alert = "IdleBattle";
            [Tooltip("Animator state: the creep toward the toy, and the walk home.")] public string walk = "WalkFWD";
            [Tooltip("Animator state: running from a beam or the kid.")] public string run = "RunFWD";
            [Tooltip("Animator state: backing off after a bump.")] public string back = "WalkBWD";
            [Tooltip("Animator state: the bump.")] public string attack = "Attack01";
            [Tooltip("Animator state: the flinch (a beam, a dart that does not knock it down).")] public string hit = "GetHit";
            [Tooltip("Animator state: hiding at the back of the cave while the light is on it.")] public string hide = "Dizzy";
            [Tooltip("Animator state: knocked down (played once, it lies at its end).")] public string die = "Die";
            [Tooltip("Animator state frozen in the light (an ordinary toy). The chest's IdleChest is a shut chest.")] public string rest = "IdleNormal";
            [Range(0f, 1f), Tooltip("Where in the Rest state's clip the frozen pose is taken (0 its first frame).")] public float restAt;
            [Tooltip("The bones the eye glints ride on (the maker finds each by name and puts a glint on the body's front straight ahead of it). None found: the glint rides the root at Eye Height.")]
            public string[] eyeBones = Array.Empty<string>();
            [Range(0f, 0.5f), Tooltip("0 = one glint straight ahead of each eye bone; above 0 = two, this share of the body's height apart (left and right).")]
            public float eyePairGap;
            [Range(0.01f, 0.2f), Tooltip("Each glint's radius, as a share of the body's height.")] public float eyeSize = 0.06f;
            [Range(0f, 1f), Tooltip("No eye bone found: the glint's height on the body, as a share of its height.")] public float eyeHeight = 0.6f;

            public Kind() { }

            public Kind(string name, string packPrefab, string prefix, string[] eyeBones)
            {
                this.name = name; this.packPrefab = packPrefab; this.eyeBones = eyeBones;
                idle = prefix + "IdleNormal"; alert = prefix + "IdleBattle"; walk = prefix + "WalkFWD"; run = prefix + "RunFWD";
                back = prefix + "WalkBWD"; attack = prefix + "Attack01"; hit = prefix + "GetHit"; hide = prefix + "Dizzy";
                die = prefix + "Die"; rest = prefix + "IdleNormal";
            }
        }

        /// <summary>Where one of the cave's monsters stands, measured along the way from the cave's mouth to the flashlight.</summary>
        [Serializable]
        public struct CaveSpot
        {
            [Tooltip("Which kind stands here (a Kind's name).")] public string kind;
            [Tooltip("How far along the way from the cave's mouth (0) to the bed's flashlight (1); past 1 is deeper than the flashlight.")] public float along;
            [Tooltip("Which side of that way: 1 or -1 (the maker tries the other side if this one is not free).")] public float side;
            [Tooltip("Toy units from the way's middle line out to the spot. At least Lane Half Width + Leash + its body's radius, or the maker pushes it out.")] public float apart;

            public CaveSpot(string kind, float along, float side, float apart) { this.kind = kind; this.along = along; this.side = side; this.apart = apart; }
        }

        private static MonsterSettings _loaded;

        /// <summary>The asset, or defaults with a warning when it has not been made.</summary>
        public static MonsterSettings Load()
        {
            if (_loaded == null) _loaded = Resources.Load<MonsterSettings>(ResourceName);
            if (_loaded == null)
            {
                Debug.LogWarning("MONSTERS no Resources/" + ResourceName + ": run Shift Jam > House > Land Bedroom Monsters (D-0222, D-0223). Using defaults with no sounds.");
                _loaded = CreateInstance<MonsterSettings>();
            }
            return _loaded;
        }

        [Header("Switch")]
        [Tooltip("Off: every monster and prize stays a still toy (frozen in its rest pose, no eyes, no moving, no bumping). The off switch for D-0222 and D-0223.")]
        public bool on = true;
        [Tooltip("Online (a party in the house): on, each PC's monsters react to that PC's own player only (nothing networked: two players see their own monsters); off, they stay still toys while online.")]
        public bool online = true;

        [Header("Seeing the toy")]
        [Min(0.5f), Tooltip("How far a monster notices a toy-sized player it can see (toy units).")] public float sight = 14f;
        [Min(0.1f), Tooltip("Seconds without seeing the toy before it gives up and walks home.")] public float loseAfter = 2f;
        [Min(1f), Tooltip("A kid-sized player this near (level distance, toy units) sends it running to hide, as a beam does (the kid cannot fit under the bed anyway).")] public float kidReach = 50f;
        [Min(0.05f), Tooltip("Seconds between looks for the player (line of sight is a raycast: not every frame).")] public float lookEvery = 0.2f;
        [Min(0.02f), Tooltip("Seconds between checks for a beam on it.")] public float lightEvery = 0.1f;

        [Header("Moving")]
        [Min(0f), Tooltip("How far from its spot it may creep (toy units): it never leaves this, nor the cave.")] public float leash = 5f;
        [Min(0f), Tooltip("Creeping and walking home, toy units a second (the toy walks 7).")] public float creepSpeed = 2.2f;
        [Min(0f), Tooltip("Running from a beam or the kid, toy units a second.")] public float runSpeed = 6f;
        [Min(0f), Tooltip("Backing off after a bump, toy units a second.")] public float backSpeed = 2.5f;
        [Min(1f), Tooltip("Degrees a second it turns.")] public float turnSpeed = 300f;
        [Min(0f), Tooltip("Gravity, toy units a second squared (the toy's is 25).")] public float gravity = 25f;
        [Min(0.1f), Tooltip("Seconds without getting anywhere on its way home before it is put back on its spot at once (it is dark: nobody sees the hop).")] public float stuckSeconds = 3f;

        [Header("The bump (no damage, no charge lost)")]
        [Min(0f), Tooltip("The gap between its body and the toy's at which it bumps (toy units).")] public float bumpGap = 0.5f;
        [Min(0f), Tooltip("Seconds from the start of its bump to the push (the swing).")] public float bumpWindup = 0.3f;
        [Min(0f), Tooltip("Seconds it stays in the bump after the push before it backs off.")] public float bumpFollow = 0.3f;
        [Min(0f), Tooltip("How hard the toy is pushed back, away from it (the toy's units a second; the toy walks 7). Gentle.")] public float bumpPush = 8f;
        [Min(0f), Tooltip("And up, a hop (the toy's units a second: 3.5 lifts about 0.25).")] public float bumpLift = 3.5f;
        [Min(0f), Tooltip("The toy must still be within this many times the bump's reach when the swing lands, or the bump misses.")] public float bumpMissShare = 1.5f;
        [Min(0f), Tooltip("Seconds it backs off after a bump.")] public float backSeconds = 1f;
        [Min(0f), Tooltip("Seconds before the same monster may bump again.")] public float bumpCooldown = 3f;
        [Range(1, 8), Tooltip("No more than this many bumps from all the monsters together...")] public int bumpsAtMost = 2;
        [Min(0.1f), Tooltip("...in any this many seconds.")] public float bumpWindow = 4f;
        [Range(0f, 1f), Tooltip("The bump's sound volume.")] public float bumpVolume = 0.55f;

        [Header("The beam (and the kid)")]
        [Min(0f), Tooltip("Seconds it flinches (its hit clip and a squeak) before it runs.")] public float flinchSeconds = 0.4f;
        [Min(0f), Tooltip("How far it runs from the light (toy units), toward the back of the cave and never out of it.")] public float fleeDistance = 14f;
        [Range(0f, 3f), Tooltip("How much it bends its run toward the cave's back (0 = straight away from the light).")] public float fleeToBack = 0.6f;
        [Min(0.1f), Tooltip("Most seconds it runs before it stops and hides where it is.")] public float fleeSeconds = 4f;
        [Min(0f), Tooltip("Seconds after the light (or the kid) leaves it before it walks back to its spot.")] public float returnAfter = 3f;
        [Min(0f), Tooltip("Seconds before the same monster squeaks again.")] public float squeakEvery = 1.5f;

        [Header("Knocked down (darts, punches, kicks)")]
        [Min(1f), Tooltip("Its health: a dart takes 15, a punch 10, a kick 20.")] public float health = 30f;
        [Min(0f), Tooltip("Seconds after a hit during which further hits are ignored.")] public float hitSafeSeconds = 0.1f;
        [Min(0f), Tooltip("Seconds it stops and flinches when a hit does not knock it down.")] public float hurtSeconds = 0.5f;
        [Min(0.5f), Tooltip("Seconds it lies knocked down before it gets up again at its spot.")] public float downSeconds = 5f;

        [Header("Frozen in the light")]
        [Min(0f), Tooltip("Seconds it takes to settle into its rest pose when the room lights come on (a Continue on a lit slot: at once).")] public float freezeSeconds = 0.25f;

        [Header("The eyes (a glint so it can be found in the dark)")]
        [Tooltip("The glint's colour (over 1 glows under bloom). The maker writes it into the glint's material.")] public Color glint = new Color(1.6f, 1.25f, 0.3f, 1f);
        [Min(0f), Tooltip("Breaths a second (the glint swells and shrinks a little).")] public float glintBreath = 0.4f;
        [Range(0f, 0.9f), Tooltip("How much each breath shrinks it (0 = steady).")] public float glintDepth = 0.35f;
        [Min(0.2f), Tooltip("About how many seconds between blinks (each one a little different).")] public float blinkEvery = 3.5f;
        [Min(0f), Tooltip("How long a blink keeps the eyes shut.")] public float blinkSeconds = 0.12f;
        [Tooltip("The prizes in the claw machine glint too.")] public bool prizeGlint = true;

        [Header("Sounds (heard from where the monster is)")]
        [Tooltip("The flinch from a beam: the Playroom's Toy_Squeak_Single_v1.")] public CourseSounds.Cue squeak = new CourseSounds.Cue(null, 0.55f, 0.15f);
        [Tooltip("The bump: the Playroom's Ball_Bounce_v1, a soft rubbery thump.")] public CourseSounds.Cue bump = new CourseSounds.Cue(null, 0.55f, 0.1f);
        [Min(0.1f), Tooltip("Full volume within this many toy units.")] public float soundNear = 2f;
        [Min(1f), Tooltip("Silent past this many toy units.")] public float soundFar = 30f;

        [Header("The cave's layout (the maker)")]
        [Min(0f), Tooltip("Half the width of the way from the cave's mouth to the flashlight that no monster ever stands in (toy units; the toy is about 1 across).")] public float laneHalfWidth = 2.5f;
        [Min(0f), Tooltip("Least room kept between the flashlight and any monster's leash edge (toy units).")] public float flashlightClear = 3f;
        [Tooltip("Where the cave's monsters stand (3 to 5).")]
        public CaveSpot[] caveSpots =
        {
            new CaveSpot("Slime", 0.35f, 1f, 9f),
            new CaveSpot("TurtleShell", 0.65f, -1f, 9f),
            new CaveSpot("Slime", 1.2f, 1f, 10f),
            new CaveSpot("BlueSlime", 1.25f, -1f, 9f),
        };

        [Header("The prizes (the maker)")]
        [Tooltip("The kinds sitting in the claw machine's glass case, one prize each (6 to 8).")]
        public string[] prizes = { "Slime", "TurtleShell", "BlueSlime", "StarFish", "Beholder", "ChestMonster", "PartyC01", "PartyP01" };
        [Range(0.05f, 0.6f), Tooltip("A prize's height, as a share of the glass case's inside height.")] public float prizeTall = 0.22f;
        [Range(0.05f, 0.6f), Tooltip("Its widest, as a share of the case's narrower side (a wide one is made smaller).")] public float prizeWide = 0.3f;
        [Range(0f, 0.2f), Tooltip("Room kept inside the glass all round, as a share of the case's narrower side.")] public float prizeMargin = 0.04f;
        [Range(0f, 60f), Tooltip("Each prize faces the machine's front, turned by up to this many degrees either way.")] public float prizeTurn = 25f;

        [Header("Kinds")]
        [Tooltip("Every kind's state names and eyes. A monster whose kind is not here uses the first.")]
        public Kind[] kinds = DefaultKinds();

        /// <summary>The kinds as first made: the three cave kinds and the prizes' five more.</summary>
        public static Kind[] DefaultKinds()
        {
            const string duo = "Assets/_Imports/DungeonMason/RPG Monster DUO PBR Polyart/Prefabs/PolyartDefault/";
            const string couple = "Assets/_Imports/DungeonMason/RPGMonsterCouplePBRPolyart/Prefab/";
            const string partners = "Assets/_Imports/DungeonMason/RPGMonsterPartnersPBRPolyart/Prefabs/Character/";
            const string party = "Assets/_Imports/DungeonMason/PartyMonsterDuo/Prefab/";
            var slime = new Kind("Slime", duo + "SlimePolyart.prefab", "", new[] { "EyeBall" });
            var shell = new Kind("TurtleShell", duo + "TurtleShellPolyart.prefab", "", new[] { "Eyeball" });
            var blue = new Kind("BlueSlime", couple + "BlueSlimePolyart.prefab", "BlueSlime_", new[] { "Head" });
            var star = new Kind("StarFish", couple + "StarFishPolyart.prefab", "StarFish_", new[] { "Eye01_L", "Eye01_R" });
            star.walk = "StarFish_MoveFWD"; star.back = "StarFish_MoveBWD"; star.run = "StarFish_MoveFWD";
            var beholder = new Kind("Beholder", partners + "BeholderPolyartDefault.prefab", "", new[] { "EyeBallCTRL" });
            beholder.run = "Run";
            var chest = new Kind("ChestMonster", partners + "ChestMonsterPolyartDefault.prefab", "", new[] { "EyeBallCTRL" });
            chest.run = "Run"; chest.rest = "IdleChest";
            var c01 = Party("PartyC01", party + "C01.prefab");
            var p01 = Party("PartyP01", party + "P01.prefab");
            return new[] { slime, shell, blue, star, beholder, chest, c01, p01 };
        }

        private static Kind Party(string name, string path)
        {
            return new Kind
            {
                name = name, packPrefab = path, eyeBones = new[] { "head" }, eyePairGap = 0.12f, eyeSize = 0.035f,
                idle = "Idle03", alert = "Idle03", walk = "Run01FWD", run = "Run01FWD", back = "Run01BWD", attack = "Attack01",
                hit = "GetHit", hide = "Dizzy", die = "Die01", rest = "Idle03",
            };
        }

        /// <summary>The kind by its name; the first kind when there is no such one (null only for an empty list).</summary>
        public Kind Find(string kindName)
        {
            if (kinds == null || kinds.Length == 0) return null;
            for (int i = 0; i < kinds.Length; i++)
                if (kinds[i] != null && kinds[i].name == kindName) return kinds[i];
            return kinds[0];
        }

        /// <summary>True when the list has a kind of exactly this name.</summary>
        public bool Has(string kindName)
        {
            if (kinds == null) return false;
            for (int i = 0; i < kinds.Length; i++) if (kinds[i] != null && kinds[i].name == kindName) return true;
            return false;
        }
    }
}
