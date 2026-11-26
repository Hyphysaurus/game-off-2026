using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Every number of the monsters under the bed (D-0222, D-0228) and the claw machine's prizes (D-0223): the light rule
    /// (the wall switch freezes them, the star lamp makes them sleepy), how far they see, how they wander, spot and chase,
    /// the wind-up before a bump and the bump itself (never a hurt: the toy is only pushed back), how they flinch, flee and
    /// go dizzy in a beam, how they are knocked down and stay down, how a frozen one tips over, their size, eyes, glow,
    /// "!" and sounds, and the state names each kind's own Animator uses (the pack's controllers have no parameters: states
    /// are played by name). Also the maker's layout numbers. Lengths and speeds are toy units (the toy is about 2 tall and
    /// walks 7 a second), whatever size a monster is drawn at.
    /// Loaded from Resources/MonsterSettings; Shift Jam > House > Land Bedroom Monsters (D-0222, D-0223, D-0228) makes the
    /// asset (the numbers on an existing asset are kept, apart from the D-0228 pass below; a missing kind is added).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Monster Settings", fileName = "MonsterSettings")]
    public sealed class MonsterSettings : ScriptableObject
    {
        /// <summary>The name the asset is loaded by: Resources/MonsterSettings.</summary>
        public const string ResourceName = "MonsterSettings";
        /// <summary>The maker pass this file's numbers belong to (see <see cref="pass"/>).</summary>
        public const int CurrentPass = 228;

        /// <summary>One kind of monster: its Animator's state names, its eyes, and the pack prefab the maker builds it from.</summary>
        [Serializable]
        public sealed class Kind
        {
            [Tooltip("The kind's name: the monster component looks itself up by it, and the maker names the prefab after it (Prefabs/Monsters/<Name>Monster).")]
            public string name;
            [Tooltip("The pack prefab the maker builds this kind's prefab variant from (the maker only; it stays in the git-ignored pack folder).")]
            public string packPrefab;
            [Tooltip("Animator state: standing about, and wandering near its spot between walks.")] public string idle = "IdleNormal";
            [Tooltip("Animator state: on guard, the toy in sight but out of reach (at the end of its leash), and cowering at the back of the cave while a light is on it.")] public string alert = "IdleBattle";
            [Tooltip("Animator state: walking about near its spot, and the walk home.")] public string walk = "WalkFWD";
            [Tooltip("Animator state: the chase, and running from a beam or the kid.")] public string run = "RunFWD";
            [Tooltip("Animator state: backing off after a bump.")] public string back = "WalkBWD";
            [Tooltip("Animator state: the bump (its wind-up is a hold early in this clip).")] public string attack = "Attack01";
            [Tooltip("Animator state: the flinch (a beam, a hit that does not knock it down).")] public string hit = "GetHit";
            [Tooltip("Animator state: cowering at the back of the cave while a light is on it.")] public string hide = "IdleBattle";
            [Tooltip("Animator state: knocked down (played once, it lies at its end).")] public string die = "Die";
            [Tooltip("Animator state frozen in the wall switch's light (an ordinary toy). The chest's IdleChest is a shut chest.")] public string rest = "IdleNormal";
            [Tooltip("Animator state (D-0228): the moment it spots the toy (with the \"!\" and a squeak). Empty or not in the controller: the alert state.")] public string sense = "";
            [Tooltip("Animator state (D-0228): dizzy after a second in a beam (one hit knocks it down). Empty or not in the controller: the hide state.")] public string dizzy = "";
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
                back = prefix + "WalkBWD"; attack = prefix + "Attack01"; hit = prefix + "GetHit"; hide = prefix + "IdleBattle";
                die = prefix + "Die"; rest = prefix + "IdleNormal"; sense = prefix + "SenseSomethingST"; dizzy = prefix + "Dizzy";
            }
        }

        /// <summary>Where one of the cave's monsters stands, measured along the way from the cave's mouth to the flashlight.</summary>
        [Serializable]
        public struct CaveSpot
        {
            [Tooltip("Which kind stands here (a Kind's name).")] public string kind;
            [Tooltip("How far along the way from the cave's mouth (0) to the bed's flashlight (1); past 1 is deeper than the flashlight.")] public float along;
            [Tooltip("Which side of that way: 1 or -1 (the maker tries the other side if this one is not free).")] public float side;
            [Tooltip("Toy units from the way's middle line out to the spot. At least Lane Half Width + Wander Radius + its body's radius + 0.5, or the maker pushes it out.")] public float apart;

            public CaveSpot(string kind, float along, float side, float apart) { this.kind = kind; this.along = along; this.side = side; this.apart = apart; }
        }

        private static MonsterSettings _loaded;

        /// <summary>The asset, or defaults with a warning when it has not been made.</summary>
        public static MonsterSettings Load()
        {
            if (_loaded == null) _loaded = Resources.Load<MonsterSettings>(ResourceName);
            if (_loaded == null)
            {
                Debug.LogWarning("MONSTERS no Resources/" + ResourceName + ": run Shift Jam > House > Land Bedroom Monsters (D-0222, D-0223, D-0228). Using defaults with no sounds.");
                _loaded = CreateInstance<MonsterSettings>();
            }
            return _loaded;
        }

        [Header("Switch")]
        [Tooltip("Off: every monster and prize stays a still toy (frozen in its rest pose, no eyes, no moving, no bumping). The off switch for D-0222, D-0223 and D-0228.")]
        public bool on = true;
        [Tooltip("Online (a party in the house): on, each PC's monsters react to that PC's own player only (nothing networked: two players see their own monsters); off, they stay still toys while online.")]
        public bool online = true;
        [Tooltip("The maker's pass these numbers were last brought up to (228 = D-0228: leash 14, sight 20, the cowering state no longer the dizzy one). Below 228 the maker raises them once and sets this; leave it alone.")]
        public int pass;

        [Header("The light rule (D-0228)")]
        [Range(0.1f, 1f), Tooltip("In the star lamp's starlight they are SLEEPY (awake, slower): every speed is this share of its own. Only the wall switch's full lights freeze them.")]
        public float sleepySpeed = 0.55f;
        [Range(0.1f, 1f), Tooltip("Sleepy, they notice the toy only within this share of Sight.")] public float sleepySight = 0.5f;
        [Range(0.1f, 1f), Tooltip("Sleepy, their eye glints are this share of their size (heavy-lidded).")] public float sleepyEyes = 0.6f;

        [Header("Size (D-0228)")]
        [Min(0.1f), Tooltip("The cave's monsters are drawn this many times their pack size (the maker sets their scale; a slime then reads beside the 2-tall toy). Every other number here stays in toy units.")]
        public float caveScale = 1.8f;

        [Header("Seeing the toy")]
        [Min(0.5f), Tooltip("How far a monster notices a toy-sized player it can see (toy units).")] public float sight = 20f;
        [Min(1f), Tooltip("Once it is after the toy, it keeps track of it within this many times Sight.")] public float keepTrack = 1.5f;
        [Min(0.1f), Tooltip("Seconds without seeing the toy before it gives up and walks home.")] public float loseAfter = 2f;
        [Min(1f), Tooltip("A kid-sized player this near (level distance, toy units) sends it running to hide, as a beam does (the kid cannot fit under the bed anyway).")] public float kidReach = 50f;
        [Min(0.05f), Tooltip("Seconds between looks for the player (line of sight is a raycast: not every frame).")] public float lookEvery = 0.2f;
        [Min(0.02f), Tooltip("Seconds between checks for a beam on it.")] public float lightEvery = 0.1f;

        [Header("Wandering (nothing seen)")]
        [Min(0f), Tooltip("It walks about within this many toy units of its spot (never into the way to the flashlight). 0 = it stands at its spot.")] public float wanderRadius = 3f;
        [Min(0f), Tooltip("Least seconds it stands before the next little walk.")] public float wanderPauseMin = 1.5f;
        [Min(0f), Tooltip("Most seconds it stands before the next little walk.")] public float wanderPauseMax = 4f;
        [Min(0.5f), Tooltip("Most seconds one little walk takes before it stops where it is.")] public float wanderSeconds = 4f;

        [Header("Spotting the toy (D-0228)")]
        [Min(0f), Tooltip("Seconds it stops and stares (its sense state, the \"!\", a squeak) before the chase.")] public float senseSeconds = 0.7f;
        [Min(0f), Tooltip("Seen again within this many seconds of the last spotting: straight back to the chase, no second \"!\".")] public float spotAgainAfter = 5f;
        [Tooltip("The mark that pops over its head when it spots the toy.")] public string markText = "!";
        [Tooltip("The mark's colour.")] public Color markColour = new Color32(0xff, 0x5a, 0x3c, 0xff);
        [Min(0.01f), Tooltip("The mark's height, as a share of the monster's height.")] public float markSize = 0.5f;
        [Min(0f), Tooltip("How far over its head the mark floats, as a share of its height.")] public float markGap = 0.15f;
        [Min(0.01f), Tooltip("Seconds the mark takes to pop up (it overshoots, then settles).")] public float markPopSeconds = 0.22f;
        [Min(1f), Tooltip("How far it overshoots at the top of the pop (1.35 = 35% bigger).")] public float markOvershoot = 1.35f;
        [Min(0f), Tooltip("Seconds the mark stays once it has popped.")] public float markHoldSeconds = 1.1f;
        [Min(0.01f), Tooltip("Seconds it takes to shrink away.")] public float markOutSeconds = 0.18f;

        [Header("Moving")]
        [Min(0f), Tooltip("How far from its spot it may chase (toy units): it never leaves this, nor the cave.")] public float leash = 14f;
        [Min(0f), Tooltip("Walking about and walking home, toy units a second (the toy walks 7).")] public float creepSpeed = 2.2f;
        [Min(0f), Tooltip("The chase, toy units a second (the toy walks 7: it can always get away).")] public float chaseSpeed = 4.5f;
        [Min(0f), Tooltip("Running from a beam or the kid, toy units a second.")] public float runSpeed = 6f;
        [Min(0f), Tooltip("Backing off after a bump, toy units a second.")] public float backSpeed = 2.5f;
        [Min(1f), Tooltip("Degrees a second it turns.")] public float turnSpeed = 300f;
        [Min(0f), Tooltip("Gravity, toy units a second squared (the toy's is 25).")] public float gravity = 25f;
        [Min(0.1f), Tooltip("Seconds without getting anywhere on its way home before it is put back on its spot at once (it is dark: nobody sees the hop).")] public float stuckSeconds = 3f;

        [Header("The bump (no damage, no charge lost)")]
        [Min(0f), Tooltip("The gap between its body and the toy's at which it starts its wind-up (toy units).")] public float bumpGap = 0.5f;
        [Min(0f), Tooltip("Seconds into its attack clip at which the clip is held still: the wind-up's pose, the tell.")] public float windupPoseAt = 0.12f;
        [Min(0f), Tooltip("Seconds from the start of the wind-up to the swing (the toy's time to step away).")] public float windupSeconds = 0.6f;
        [Min(0f), Tooltip("Seconds from the swing to the push.")] public float strikeSeconds = 0.15f;
        [Min(0f), Tooltip("Seconds it stays in the bump after the push before it backs off.")] public float bumpFollow = 0.3f;
        [Min(0f), Tooltip("How hard the toy is pushed back, away from it (the toy's units a second; the toy walks 7). Gentle.")] public float bumpPush = 8f;
        [Min(0f), Tooltip("And up, a hop (the toy's units a second: 3.5 lifts about 0.25).")] public float bumpLift = 3.5f;
        [Min(0f), Tooltip("The toy must still be within this many times the bump's reach when the push lands, or the bump misses (the dodge).")] public float bumpMissShare = 1.5f;
        [Min(0f), Tooltip("Seconds it backs off after a bump.")] public float backSeconds = 1f;
        [Min(0f), Tooltip("Seconds before the same monster may wind up again.")] public float bumpCooldown = 3f;
        [Range(1, 8), Tooltip("No more than this many bumps from all the monsters together...")] public int bumpsAtMost = 2;
        [Min(0.1f), Tooltip("...in any this many seconds.")] public float bumpWindow = 4f;
        [Range(0f, 1f), Tooltip("The bump's sound volume.")] public float bumpVolume = 0.55f;

        [Header("The beam (and the kid)")]
        [Min(0f), Tooltip("Seconds it flinches (its hit clip and a squeak) before it runs.")] public float flinchSeconds = 0.4f;
        [Min(0f), Tooltip("How far it runs from the light (toy units), toward the back of the cave and never out of it.")] public float fleeDistance = 14f;
        [Range(0f, 3f), Tooltip("How much it bends its run toward the cave's back (0 = straight away from the light).")] public float fleeToBack = 0.6f;
        [Min(0.1f), Tooltip("Most seconds it runs before it stops and hides where it is.")] public float fleeSeconds = 4f;
        [Min(0f), Tooltip("Seconds after the light (or the kid) leaves it before it walks back to its spot.")] public float returnAfter = 3f;
        [Min(0f), Tooltip("Seconds before the same monster squeaks again from a beam.")] public float squeakEvery = 1.5f;
        [Min(0.05f), Tooltip("D-0228: seconds of beam on it (added up) before it is DIZZY.")] public float dizzyAfter = 1f;
        [Min(0.05f), Tooltip("Seconds without the beam after which that count starts again.")] public float beamForget = 0.5f;
        [Min(0.1f), Tooltip("Seconds it stays dizzy (its dizzy clip, standing where it is).")] public float dizzySeconds = 3f;
        [Min(1f), Tooltip("While dizzy every hit counts this many times over (10: any one dart, punch or kick knocks it down).")] public float dizzyHitScale = 10f;

        [Header("Knocked down (darts, punches, kicks)")]
        [Min(1f), Tooltip("Its health: a dart takes 15, a punch 10, a kick 20 (30 = two darts, three punches, two kicks).")] public float health = 30f;
        [Min(0f), Tooltip("Seconds after a hit during which further hits are ignored.")] public float hitSafeSeconds = 0.1f;
        [Min(0f), Tooltip("Seconds it stops and flinches when a hit does not knock it down.")] public float hurtSeconds = 0.5f;
        [Tooltip("D-0228: on, a knocked-down monster stays down for the rest of the visit (a reload, a New Game or the console's 'bedroom reset' brings it back). Off: it gets up at its spot after Down Seconds (D-0222).")]
        public bool stayDown = true;
        [Min(0.5f), Tooltip("Stay Down off: seconds it lies knocked down before it gets up again at its spot.")] public float downSeconds = 5f;

        [Header("Frozen in the wall switch's light")]
        [Min(0f), Tooltip("Seconds it takes to settle into its rest pose when the room lights come on (a Continue on a lit slot: at once).")] public float freezeSeconds = 0.25f;
        [Range(0f, 90f), Tooltip("D-0228: a hit on a frozen one tips it over like a toy, by this many degrees (no health lost, it does not count as knocked down).")] public float tipAngle = 80f;
        [Min(0.01f), Tooltip("Seconds the tip-over takes.")] public float tipSeconds = 0.3f;
        [Min(0f), Tooltip("Seconds it lies tipped over (another hit starts the count again).")] public float tipLie = 4f;
        [Min(0.01f), Tooltip("Seconds it takes to right itself.")] public float tipRightSeconds = 0.35f;

        [Header("The eyes (a glint so it can be found in the dark)")]
        [Tooltip("The calm glint's colour (over 1 glows under bloom). The maker writes it into Eye Calm.")] public Color glint = new Color(1.6f, 1.25f, 0.3f, 1f);
        [Tooltip("D-0228: the glint while it is after the toy (spotting, chasing, winding up, bumping). The maker writes it into Eye Hunt.")] public Color glintHunt = new Color(2.2f, 0.35f, 0.2f, 1f);
        [Tooltip("D-0228: the glint while it is dizzy. The maker writes it into Eye Dizzy.")] public Color glintDizzy = new Color(0.7f, 1.7f, 0.45f, 1f);
        [Tooltip("The calm glint material (URP Unlit, made by the maker). Empty: the glint keeps the prefab's.")] public Material eyeCalm;
        [Tooltip("The hunting glint material (made by the maker). Empty: the calm one.")] public Material eyeHunt;
        [Tooltip("The dizzy glint material (made by the maker). Empty: the calm one.")] public Material eyeDizzy;
        [Min(0f), Tooltip("Breaths a second (the glint swells and shrinks a little).")] public float glintBreath = 0.4f;
        [Range(0f, 0.9f), Tooltip("How much each breath shrinks it (0 = steady).")] public float glintDepth = 0.35f;
        [Min(0.2f), Tooltip("About how many seconds between blinks (each one a little different).")] public float blinkEvery = 3.5f;
        [Min(0f), Tooltip("How long a blink keeps the eyes shut.")] public float blinkSeconds = 0.12f;
        [Tooltip("The prizes in the claw machine glint too.")] public bool prizeGlint = true;

        [Header("The glow round the body (D-0228)")]
        [Tooltip("A soft glow behind each cave monster while it is awake in the dark or the starlight (the shift orb's own glow shader on a camera-facing square), so its shape reads. Off in the wall switch's light and when it is down. Off = no glow.")]
        public bool haloOn = true;
        [Tooltip("The calm glow's colour. The maker writes it into Halo Calm.")] public Color haloCalmColour = new Color(0.45f, 0.3f, 1f, 1f);
        [Tooltip("The glow while it is after the toy. The maker writes it into Halo Hunt.")] public Color haloHuntColour = new Color(1f, 0.25f, 0.15f, 1f);
        [Tooltip("The glow while it is dizzy. The maker writes it into Halo Dizzy.")] public Color haloDizzyColour = new Color(0.45f, 1f, 0.35f, 1f);
        [Tooltip("The calm glow material (ShiftJam/ShiftOrbAura, made by the maker).")] public Material haloCalm;
        [Tooltip("The hunting glow material (made by the maker). Empty: the calm one.")] public Material haloHunt;
        [Tooltip("The dizzy glow material (made by the maker). Empty: the calm one.")] public Material haloDizzy;
        [Range(0f, 3f), Tooltip("How bright the glow is next to the body (the maker writes it into the glow materials).")] public float haloStrength = 0.45f;
        [Range(0.5f, 8f), Tooltip("How fast it fades away from the body (higher = tighter; the maker writes it into the glow materials).")] public float haloReach = 2.5f;
        [Min(1f), Tooltip("The glow's square is this many times the body's width and height (the maker sizes it).")] public float haloSpread = 3f;

        [Header("Sounds (heard from where the monster is)")]
        [Tooltip("The flinch from a beam: the Playroom's Toy_Squeak_Single_v1.")] public CourseSounds.Cue squeak = new CourseSounds.Cue(null, 0.55f, 0.15f);
        [Tooltip("D-0228: spotting the toy, with the \"!\" (the maker fills the same squeak; a higher pitch reads as a gasp).")] public CourseSounds.Cue spotSqueak = new CourseSounds.Cue(null, 0.6f, 0.1f);
        [Range(0.5f, 2f), Tooltip("The spotting squeak's pitch (before its spread).")] public float spotPitch = 1.35f;
        [Tooltip("D-0228: knocked down, with the soldiers' burst and thud (the maker fills the same squeak, played low).")] public CourseSounds.Cue downSqueak = new CourseSounds.Cue(null, 0.7f, 0.05f);
        [Range(0.5f, 2f), Tooltip("The knocked-down squeak's pitch (before its spread).")] public float downPitch = 0.75f;
        [Tooltip("The bump, and a frozen one tipping over: the Playroom's Ball_Bounce_v1, a soft rubbery thump.")] public CourseSounds.Cue bump = new CourseSounds.Cue(null, 0.55f, 0.1f);
        [Min(0.1f), Tooltip("Full volume within this many toy units.")] public float soundNear = 2f;
        [Min(1f), Tooltip("Silent past this many toy units.")] public float soundFar = 30f;

        [Header("The cave's layout (the maker)")]
        [Min(0f), Tooltip("Half the width of the way from the cave's mouth to the flashlight that no monster stands, waits, wanders or hides in (toy units; the toy is about 1 across). A chase may cross it.")] public float laneHalfWidth = 2.5f;
        [Min(0f), Tooltip("No monster's body ever comes within this many toy units of the flashlight's spot, even in a chase: the toy can always walk up and take it.")] public float flashlightClear = 3f;
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

        [Header("The gold star (D-0228, the maker)")]
        [Tooltip("The star's id on Resources/ObjectiveSet (saved per slot as star:<id>). The maker adds it there once; his title and room are never overwritten.")]
        public string starId = "monsters";
        [Tooltip("Its title when the maker first adds it. Placeholder wording: his to rewrite on the ObjectiveSet.")] public string starTitle = "Monsters under the bed";
        [Tooltip("Its zone (a Sticker Chart row's Room word) when the maker first adds it.")] public string starRoom = "Bedroom";

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
            blue.sense = "BlueSlime_SenseSomethingStart";
            var star = new Kind("StarFish", couple + "StarFishPolyart.prefab", "StarFish_", new[] { "Eye01_L", "Eye01_R" });
            star.walk = "StarFish_MoveFWD"; star.back = "StarFish_MoveBWD"; star.run = "StarFish_MoveFWD"; star.sense = "StarFish_SenseSomethingStart";
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
                hit = "GetHit", hide = "Idle03", die = "Die01", rest = "Idle03", sense = "", dizzy = "Dizzy",
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
