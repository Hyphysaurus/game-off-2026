using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What finishes a step or earns a star (D-0220). The first seven are facts the game already announces (GameEvents):
    /// the objects are matched by name or path when the event names one of them. The next nine are UnityEvents on
    /// scene parts: the objectives maker (Shift Jam > Objectives) wires each listed object's event to an ObjectiveHook,
    /// so nothing in those parts' scripts changes. The last two are heard in code, in their own scene (Scene).
    /// New kinds go at the END (the asset stores the number).
    /// </summary>
    public enum ObjectiveEvent
    {
        /// <summary>A sleeping core woke (a flashlight, a beam): GameEvents.SourceAwakened.</summary>
        SourceAwakened,
        /// <summary>A switch was pressed: GameEvents.SwitchPressed.</summary>
        SwitchPressed,
        /// <summary>A door began to open: GameEvents.DoorOpening.</summary>
        DoorOpening,
        /// <summary>The player claimed a checkpoint: GameEvents.CheckpointClaimed.</summary>
        CheckpointClaimed,
        /// <summary>The player reached a Goal: GameEvents.GoalReached.</summary>
        GoalReached,
        /// <summary>Something was taken into the hands: GameEvents.PickedUp.</summary>
        PickedUp,
        /// <summary>Something held was set down: GameEvents.SetDown.</summary>
        SetDown,
        /// <summary>Hook: a core was used (it shifted the player or filled the charge): ShiftSource On Used.</summary>
        CoreUsed,
        /// <summary>Hook: a doorway took the player through: Portal On Used.</summary>
        PortalUsed,
        /// <summary>Hook: a Flat Zone's last letter was found: FlatZone On All Found.</summary>
        LettersFound,
        /// <summary>Hook: every soldier of a squad fell once: SquadGate On Routed.</summary>
        SquadRouted,
        /// <summary>Hook: a thing with health was knocked down (a soldier, a skittle): Damageable On Died.</summary>
        KnockedDown,
        /// <summary>Hook: a boost ring or pad pushed something through: BoostPad On Boost.</summary>
        RingPassed,
        /// <summary>Hook: a dark room's lights came on by its switch: RoomLights On Lights On.</summary>
        LightsOn,
        /// <summary>Hook: the star lamp was woken by a light: StarLamp On Woken.</summary>
        LampWoken,
        /// <summary>In code: the player's car finished a race first (RaceState.Finished, RaceField.Player's rank 1; a race
        /// with no other cars counts on finishing).</summary>
        RaceWon,
        /// <summary>In code: a Colour Corner reached its end (all the colour back).</summary>
        ColourBack,
    }

    /// <summary>One way a step or star is done (D-0220): an event, the objects it must name, the scene it happens in.</summary>
    [Serializable]
    public sealed class ObjectiveRule
    {
        [Tooltip("What happens.")] public ObjectiveEvent when;
        [Tooltip("The objects it happens to: a path from the scene's top (Room/Bedroom/ShelfSwitch) or a bare name (matched "
               + "anywhere). Any one counts for the GameEvents kinds; the hook kinds wire every one. Empty for Race Won and "
               + "Colour Back.")]
        public string[] objects = new string[0];
        [Tooltip("The scene's name it happens in (Greybox, Raceway, ColourCorner). Empty = any scene.")] public string scene = "";
        [Tooltip("Hook kinds only: how many DIFFERENT objects of the list must fire, in one visit to the scene (0 = every one "
               + "of them; 1 = the first). Ignored by the other kinds.")]
        [Min(0)] public int needed = 1;
    }

    /// <summary>A main step of the route (D-0220): what the HUD says, and what finishes it.</summary>
    [Serializable]
    public sealed class ObjectiveStep
    {
        [Tooltip("Saved per save slot as step:<id>. Keep it short and never reuse one for a different step.")] public string id = "";
        [Tooltip("The HUD's second line while this is the step. Placeholder wording: his to rewrite.")] public string text = "";
        [Tooltip("Where it happens (a path in its scene): Objectives.CurrentTarget. Empty = the first object of the first rule.")]
        public string target = "";
        [Tooltip("Any one of these finishes it.")] public ObjectiveRule[] doneWhen = new ObjectiveRule[0];
    }

    /// <summary>A gold star (D-0220): optional, never needed for the ending.</summary>
    [Serializable]
    public sealed class ObjectiveStar
    {
        [Tooltip("Saved per save slot as star:<id>. Never reuse one for a different star.")] public string id = "";
        [Tooltip("Its name: the toast's title when it is earned. Placeholder wording: his to rewrite.")] public string title = "";
        [Tooltip("The room it belongs to (for a chart or a hint later; not shown yet).")] public string room = "";
        [Tooltip("Any one of these earns it.")] public ObjectiveRule[] doneWhen = new ObjectiveRule[0];
    }

    /// <summary>
    /// The objective layer's words and rules (D-0220, his brother's playtest: "there aren't any clear objectives"): the big
    /// goal, the main steps in route order (the HUD shows the first unfinished one; a later one finished marks the earlier
    /// ones done), and the ten gold stars (all optional). Read from Resources/ObjectiveSet (made by Shift Jam > Objectives >
    /// Build Objectives); without the asset these defaults are used. Every player-facing word here is a PLACEHOLDER for
    /// him to rewrite (in-game text written by Claude counts for Steam's AI disclosure).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Objective Set", fileName = "ObjectiveSet")]
    public sealed class ObjectiveSet : ScriptableObject
    {
        public const string House = "Greybox", Raceway = "Raceway", ColourCorner = "ColourCorner";

        [Tooltip("OFF SWITCH: unticked, nothing is marked or awarded and the objectives HUD stays hidden.")] public bool on = true;

        [Header("Words (placeholders, his to rewrite)")]
        [Tooltip("The big goal: the HUD's first line in the house, the same all game.")] public string goal = "Back on the window sill";
        [Tooltip("The toast when a star is earned (its title is the star's name). {0} = stars held now, {1} = how many there are.")]
        public string starToast = "Gold star {0} of {1}. Shift charge full.";
        [Tooltip("The HUD's star counter beside its star. {0} = stars held, {1} = how many there are.")] public string counterFormat = "{0} / {1}";
        [Tooltip("The end card's line under its title, beside a star. {0} = stars held, {1} = how many there are. Empty = no line.")]
        public string endCardFormat = "{0} of {1} gold stars";

        [Header("Feel")]
        [Tooltip("Played as a star is earned, with the toast's own 'got it' sound. None = only the toast's.")]
        public UiAudio.Kind starSound = UiAudio.Kind.Win;
        [Tooltip("Seconds a finished step shows its tick before the next one slides in.")] [Min(0f)] public float tickSeconds = 0.9f;
        [Tooltip("Seconds the next step takes to slide in.")] [Min(0.01f)] public float slideSeconds = 0.35f;
        [Tooltip("Seconds the star counter's pop takes when a star is earned.")] [Min(0.01f)] public float popSeconds = 0.45f;
        [Tooltip("Seconds the HUD takes to fade in or out (menus, the title, the opening).")] [Min(0.01f)] public float fadeSeconds = 0.25f;

        [Header("The route (main steps, in order)")]
        public ObjectiveStep[] steps = DefaultSteps();

        [Header("Gold stars (all optional)")]
        public ObjectiveStar[] stars = DefaultStars();

        // ---- D-0221: the helpers (arrival cards, refusals that point, the house characters' step lines, the stall nudge,
        // the downstairs crate). Words are PLACEHOLDERS for him to rewrite. The helpers maker (Shift Jam > Objectives > Land
        // Helpers) adds a missing card, core, room or step line by its id and never overwrites what he wrote.

        [Header("Arrival cards (D-0221)")]
        [Tooltip("OFF SWITCH: unticked, no arrival card shows.")] public bool arrivalsOn = true;
        [Tooltip("The line's letters on the card, as a share (percent) of the room name's.")] [Range(10f, 100f)] public float arrivalLineSize = 40f;
        [Tooltip("Seconds the card waits once the way is clear (no other big card, no menu, no camera move, no Flat Zone) before it shows.")]
        [Min(0f)] public float arrivalSettle = 0.6f;
        [Tooltip("One card per room: its name, one line of what to do, its colour, and the box that counts as arriving.")]
        public ArrivalWords[] arrivals = HelperWords.DefaultArrivals();

        [Header("Refusals that point (D-0221)")]
        [Tooltip("OFF SWITCH: unticked, a refused shift says the old words.")] public bool refusalsPoint = true;
        [Tooltip("Said instead of 'No charge: a core refills it' when the nearest awake core that would refill the player is known. {0} = its place words.")]
        public string emptyPointFormat = "No charge. The nearest core is {0}.";
        [Tooltip("Each core's place words, by its object name. A core needs a CoreMarker (the maker adds them) to be pointed at.")]
        public CorePlace[] corePlaces = HelperWords.DefaultCorePlaces();
        [Tooltip("Boxes where a kid does not fit, and the words that say where there is room.")]
        public GrowRoom[] growRooms = HelperWords.DefaultGrowRooms();

        [Header("Gap, Pit and Dab say the step (D-0221)")]
        [Tooltip("OFF SWITCH: unticked, the house characters say only their own lines and show no '!'.")] public bool friendsSayStep = true;
        [Tooltip("Their line for each main step (by its id). The first E after a step changes says it; then their own lines, then it again.")]
        public StepTalk[] stepTalk = HelperWords.DefaultStepTalk();
        [Tooltip("The '!' over a character until it has been talked to once for the current step. Unticked: no '!'.")] public bool stepMarkOn = true;
        [Tooltip("The '!' colour (the toasts' gold).")] public Color stepMarkColour = new Color32(0xff, 0xd1, 0x66, 0xff);
        [Tooltip("The '!' height, as a share of the character's height.")] [Min(0.01f)] public float stepMarkSize = 0.35f;
        [Tooltip("How far over the character's head the '!' floats, as a share of its height.")] [Min(0f)] public float stepMarkGap = 0.12f;
        [Tooltip("How far the '!' bobs up and down, as a share of the character's height.")] [Min(0f)] public float stepMarkBob = 0.06f;
        [Tooltip("Bobs a second.")] [Min(0f)] public float stepMarkBobsPerSecond = 1.4f;

        [Header("The stall nudge (D-0221)")]
        [Tooltip("OFF SWITCH: unticked, no nudge.")] public bool nudgeOn = true;
        [Tooltip("Seconds of play with no step done and no star earned before the goal card pulses and the step is said again. The clock stops under menus, the opening, big cards and Flat Zones.")]
        [Min(1f)] public float nudgeSeconds = 45f;
        [Tooltip("The tip that repeats the step. {0} = the step's words.")] public string nudgeFormat = "Next: {0}";
        [Tooltip("Seconds the goal card's pulse takes.")] [Min(0.05f)] public float pulseSeconds = 0.9f;
        [Tooltip("How much the goal card swells at the pulse's top (0.12 = 12% bigger).")] [Min(0f)] public float pulseSwell = 0.12f;
        [Tooltip("Swells in one pulse.")] [Min(1)] public int pulseBeats = 2;

        [Header("The downstairs crate and light (D-0221)")]
        [Tooltip("Where the glide down from the hallway's pit comes to the floor (his brother's landing, 25 Nov: 366, -160, -200). The maker stands the ammo box here, on the floor found under it.")]
        public Vector3 landingSpot = new Vector3(366f, -160f, -200f);
        [Tooltip("The ammo box's magazines for each blaster carried (the fort's MazeAmmo gives 1). 0 = fill every reserve to the top (and the grenades).")]
        [Min(0)] public int crateMagazines = 3;
        [Tooltip("Seconds before the taken box comes back (MazeAmmo's 15).")] [Min(0f)] public float crateRespawnSeconds = 15f;
        [Tooltip("The warm light over the box: its colour.")] public Color landingLightColour = new Color(1f, 0.78f, 0.5f);
        [Tooltip("The light's height over the floor, in the world.")] [Min(0f)] public float landingLightHeight = 22f;
        [Tooltip("The light's reach, in the world.")] [Min(0f)] public float landingLightRange = 70f;
        [Tooltip("The light's brightness (the room's own lamps are 700 to 2600, much higher up).")] [Min(0f)] public float landingLightIntensity = 250f;

        /// <summary>True for the kinds the maker wires to an ObjectiveHook.</summary>
        public static bool IsHook(ObjectiveEvent e) => e >= ObjectiveEvent.CoreUsed && e <= ObjectiveEvent.LampWoken;

        /// <summary>True for the kinds heard in code in their own scene (no object named).</summary>
        public static bool IsSceneWide(ObjectiveEvent e) => e == ObjectiveEvent.RaceWon || e == ObjectiveEvent.ColourBack;

        private static ObjectiveSet _loaded;

        /// <summary>Resources/ObjectiveSet, or the defaults.</summary>
        public static ObjectiveSet Load()
        {
            if (_loaded == null) _loaded = Resources.Load<ObjectiveSet>("ObjectiveSet");
            if (_loaded == null) _loaded = CreateInstance<ObjectiveSet>();
            return _loaded;
        }

        private static ObjectiveRule Rule(ObjectiveEvent when, string scene, int needed, params string[] objects) =>
            new ObjectiveRule { when = when, scene = scene, needed = needed, objects = objects };

        private static ObjectiveRule Rule(ObjectiveEvent when, params string[] objects) => Rule(when, House, 1, objects);

        /// <summary>The route as built on 25 Nov (every object checked by name in Greybox): the defaults the maker writes.</summary>
        public static ObjectiveStep[] DefaultSteps() => new[]
        {
            new ObjectiveStep { id = "wake-box", text = "Shine the flashlight on the box core",
                doneWhen = new[] { Rule(ObjectiveEvent.SourceAwakened, "Room/ToyBox/ShiftCore") } },
            new ObjectiveStep { id = "little-door", text = "Open the little door: the switch on the shelf",
                doneWhen = new[] { Rule(ObjectiveEvent.SwitchPressed, "Room/Bedroom/ShelfSwitch") } },
            new ObjectiveStep { id = "through-door", text = "Go through the little door", target = "Room/Bedroom/Goal",
                doneWhen = new[] { Rule(ObjectiveEvent.PortalUsed, "Room/Bedroom/Goal"), Rule(ObjectiveEvent.CheckpointClaimed, "Room/Course/R3_SkirtingRun/CP_start") } },
            new ObjectiveStep { id = "tall-end", text = "Follow the hallway to its tall end",
                doneWhen = new[] { Rule(ObjectiveEvent.CheckpointClaimed, "Room/Course/R3_SkirtingRun/CP_tall") } },
            new ObjectiveStep { id = "core-a", text = "Grow: the core in the army fort",
                doneWhen = new[] { Rule(ObjectiveEvent.CoreUsed, "Room/Course/R4_Playroom/CoreA_Grow") } },
            new ObjectiveStep { id = "safe", text = "Lift the safe off the wardrobe hole",
                doneWhen = new[] { Rule(ObjectiveEvent.PickedUp, "Room/Course/R4_Playroom/Safe") } },
            new ObjectiveStep { id = "core-b", text = "Shrink: the core on the desk",
                doneWhen = new[] { Rule(ObjectiveEvent.CoreUsed, "Room/Course/R4_Playroom/CoreB_Shrink") } },
            new ObjectiveStep { id = "wardrobe", text = "Go into the hole under the wardrobe", target = "Room/Course/R4_Playroom/WardrobeHole",
                doneWhen = new[] { Rule(ObjectiveEvent.PortalUsed, "Room/Course/R4_Playroom/WardrobeHole"), Rule(ObjectiveEvent.CheckpointClaimed, "Room/Course/R5_Wardrobe/CP_start") } },
            new ObjectiveStep { id = "core-c", text = "Grow: the core at the top", target = "Room/Course/R5_Wardrobe/CoreC_Grow",
                doneWhen = new[] { Rule(ObjectiveEvent.CoreUsed, "Room/Course/R5_Wardrobe/CoreC_Grow"), Rule(ObjectiveEvent.PortalUsed, "Room/Course/R5_Wardrobe/TopPortal") } },
            new ObjectiveStep { id = "sill", text = "Get onto the window sill",
                doneWhen = new[] { Rule(ObjectiveEvent.GoalReached, "Room/Course/R6_Window/Goal") } },
        };

        /// <summary>The ten gold stars (every trigger checked against the game as it stands, 25 Nov; NOTES.md has the
        /// swap): the defaults the maker writes.</summary>
        public static ObjectiveStar[] DefaultStars() => new[]
        {
            new ObjectiveStar { id = "light-bedroom", title = "Light up the bedroom", room = "Bedroom",
                doneWhen = new[] { Rule(ObjectiveEvent.LightsOn, "Room/Bedroom/DarkBedroom"), Rule(ObjectiveEvent.LampWoken, "Room/Bedroom/NightLamp") } },
            new ObjectiveStar { id = "skittles", title = "Skittle strike", room = "Hallway",
                doneWhen = new[] { Rule(ObjectiveEvent.KnockedDown, House, 0, "Room/Course/R3_SkirtingRun/Skittle_1", "Room/Course/R3_SkirtingRun/Skittle_2", "Room/Course/R3_SkirtingRun/Skittle_3") } },
            new ObjectiveStar { id = "shift-letters", title = "Spell S-H-I-F-T", room = "Hallway",
                doneWhen = new[] { Rule(ObjectiveEvent.LettersFound, "FlatZone_Hall") } },
            new ObjectiveStar { id = "fort", title = "Clear the sandbag fort", room = "Playroom",
                doneWhen = new[] { Rule(ObjectiveEvent.KnockedDown, House, 0, "Room/Course/R4_Playroom/ArmyBeat/Soldier_NE", "Room/Course/R4_Playroom/ArmyBeat/Soldier_NW", "Room/Course/R4_Playroom/ArmyBeat/Soldier_W", "Room/Course/R4_Playroom/ArmyBeat/Soldier_Core") } },
            new ObjectiveStar { id = "tan-splinter", title = "Rout the Tan Splinter", room = "Downstairs",
                doneWhen = new[] { Rule(ObjectiveEvent.SquadRouted, "Room/Course/Downstairs/TanSplinter") } },
            new ObjectiveStar { id = "rings", title = "Through all three rings", room = "Playroom",
                doneWhen = new[] { Rule(ObjectiveEvent.RingPassed, House, 0, "Room/Course/R4_Playroom/GlideBoosters/Ring_1", "Room/Course/R4_Playroom/GlideBoosters/Ring_2", "Room/Course/R4_Playroom/GlideBoosters/Ring_3") } },
            new ObjectiveStar { id = "raceway", title = "Win the Raceway", room = "Raceway",
                doneWhen = new[] { Rule(ObjectiveEvent.RaceWon, Raceway, 1) } },
            new ObjectiveStar { id = "colour", title = "Bring the colour back", room = "Colour Corner",
                doneWhen = new[] { Rule(ObjectiveEvent.ColourBack, ColourCorner, 1) } },
            new ObjectiveStar { id = "top-shelf", title = "Shine on the top shelf", room = "Wardrobe",
                doneWhen = new[] { Rule(ObjectiveEvent.SourceAwakened, "Room/Course/R5_Wardrobe/CoreC_Grow") } },
            new ObjectiveStar { id = "sill", title = "Back on the sill", room = "Window",
                doneWhen = new[] { Rule(ObjectiveEvent.GoalReached, "Room/Course/R6_Window/Goal") } },
        };
    }
}
