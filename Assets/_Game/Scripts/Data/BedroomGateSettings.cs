using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The start hub's new order (D-0241), his words: "small door still locked until monsters are first addressed, room
    /// lights cannot go on and claw machine cannot be used until this happens", and "the dart gun should be found under the
    /// bed". The bedroom gate (BedroomGate) holds the shelf switch, the wall switch and the claw machine shut until every
    /// monster under the bed has been knocked out once; the gun snatch (GunSnatch) shows a monster taking the dart gun under
    /// the bed the first time the player grows; the gun glints in a flashlight's beam. Every number and word of those is
    /// here. Every word is a PLACEHOLDER for him to rewrite (in-game text written by Claude counts for Steam's AI
    /// disclosure), apart from the goal line, which is his own. Read from Resources/BedroomGateSettings at run time; the
    /// maker (Shift Jam > House > Land Bedroom Gate (D-0241)) makes the asset and never overwrites what is on it.
    /// Sizes are toy units (the toy stands about 2 tall, the kid 63).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Bedroom Gate Settings", fileName = "BedroomGateSettings")]
    public sealed class BedroomGateSettings : ScriptableObject
    {
        public const string ResourceName = "BedroomGateSettings";

        private static BedroomGateSettings _loaded;

        /// <summary>Resources/BedroomGateSettings, or the defaults (with a warning) when the maker has not made it.</summary>
        public static BedroomGateSettings Load()
        {
            if (_loaded == null) _loaded = Resources.Load<BedroomGateSettings>(ResourceName);
            if (_loaded == null)
            {
                Debug.LogWarning("BEDROOM GATE no Resources/" + ResourceName + ": run Shift Jam > House > Land Bedroom Gate (D-0241). Using the defaults.");
                _loaded = CreateInstance<BedroomGateSettings>();
            }
            return _loaded;
        }

        [Header("The lock: switch")]
        [Tooltip("OFF SWITCH for the lock: unticked, nothing is held shut (the shelf switch, the wall switch and the claw machine work as before D-0241). Online the lock is always open (a party is a sandbox).")]
        public bool on = true;
        [Tooltip("Saved per save slot under this name (SaveGame's seen list) once every monster has been knocked out: Continue finds the room open; New Game and the console's \"bedroom reset\" forget it.")]
        public string saveKey = "bedroom:monsters-cleared";
        [Tooltip("On: only a dart's knockout counts (the shot that finished it, heard the same moment as the knockout). Off: any knockout counts (a punch, a kick, a grenade).")]
        public bool dartsOnly = true;
        [Tooltip("The monsters' holder under the bedroom, found by this name when the gate's own Monsters field is empty (a run of Land Bedroom Monsters makes that holder new).")]
        public string monstersHolder = "BedMonsters";

        [Header("The lock: words while it is shut (placeholders)")]
        [Tooltip("Beside the E key on the shelf switch, the wall switch and the claw machine while the lock holds. Empty = their own words.")]
        public string shutPrompt = "Not yet";
        [Tooltip("The refusal toast at the shelf switch (E, or a dart that hits it).")]
        [TextArea(1, 3)] public string shelfLine = "Not yet: the monsters under the bed first.";
        [Tooltip("The refusal toast at the wall switch (the room's lights).")]
        [TextArea(1, 3)] public string lightsLine = "The lights stay off until the monsters under the bed are dealt with.";
        [Tooltip("The refusal toast at the claw machine.")]
        [TextArea(1, 3)] public string clawLine = "No games yet: the monsters under the bed first.";
        [Tooltip("Seconds before the same thing shows its refusal toast again (darts hit the shelf switch fast).")]
        [Min(0f)] public float refuseEvery = 2f;

        [Header("The lock: counting (placeholders)")]
        [Tooltip("A toast each time a monster is knocked out for the first time (not for the last one: the opening's toast says that). {0} = knocked out so far, {1} = how many there are. Empty = no toast.")]
        public string countFormat = "Monsters knocked out: {0} of {1}";
        [Tooltip("Darts Only: said once per save slot the first time a knockout that is not a dart's happens. Empty = nothing.")]
        [TextArea(1, 3)] public string notCountedLine = "Only the dart gun knocks them out for the count.";

        [Header("The lock: the moment it opens (placeholders)")]
        [Tooltip("The toast when the last monster is knocked out once (the gold star's own toast comes with it). Empty = no toast.")]
        [TextArea(1, 3)] public string openLine = "Every monster knocked out once: the little door, the lights and the claw machine work now.";
        [Tooltip("The small title over that toast. Empty = the toast's own label.")]
        public string openTitle = "";

        [Header("The gun snatch")]
        [Tooltip("OFF SWITCH for the snatch: unticked, no moment: the gun waits under the bed from the start (its glint still shows it).")]
        public bool snatchOn = true;
        [Tooltip("Saved per save slot under this name once the snatch has played: Continue finds the gun under the bed; New Game puts it back out by the bed.")]
        public string snatchKey = "bedroom:gun-snatched";
        [Tooltip("What the player says as the monster takes the gun (a speech sticker). Placeholder. Empty = nothing.")]
        public string snatchLine = "Hey! My dart gun!";
        [Tooltip("Seconds after the first growth ends (the box has burst) before the moment starts.")]
        [Min(0f)] public float startDelay = 0.4f;
        [Tooltip("The moment plays only when the player is within this many toy units of the gun (the bedroom), so an old save that grows elsewhere never turns the view across the house.")]
        [Min(1f)] public float watchRadius = 170f;
        [Tooltip("The monster's run out to the gun, toy units a second (the toy walks 7, a chase is 4.5).")]
        [Min(0.5f)] public float runSpeed = 10f;
        [Tooltip("Its walk backwards dragging the gun from where it took it to the bed's mouth (in view), toy units a second.")]
        [Min(0.5f)] public float dragSpeed = 5f;
        [Tooltip("Its walk on under the bed to where it leaves the gun (out of view by then; the gun's stand appears when it gets there), toy units a second.")]
        [Min(0.5f)] public float carrySpeed = 9f;
        [Tooltip("Seconds it stands at the gun taking it (its attack clip and a squeak; the line is said).")]
        [Min(0f)] public float grabSeconds = 0.6f;
        [Tooltip("Seconds it waits where it leaves the gun before it walks home.")]
        [Min(0f)] public float dropSeconds = 0.4f;
        [Tooltip("How far in front of the monster's body the dragged gun rides (toy units).")]
        [Min(0f)] public float gripGap = 0.4f;

        [Header("The gun snatch: the view (as the camera turns to a switch's door)")]
        [Tooltip("Seconds the view takes to turn to the gun. The monster starts its run once the turn is done.")]
        [Min(0.05f)] public float turnInSeconds = 0.45f;
        [Tooltip("Seconds the view stays on the bed's mouth after the gun has gone under it.")]
        [Min(0f)] public float holdAfterMouth = 0.7f;
        [Tooltip("Seconds the view takes to turn back. The player has the keys again at its end.")]
        [Min(0.05f)] public float turnOutSeconds = 0.45f;
        [Tooltip("The share of the view's height the gun's stretch of floor (gun to the bed's mouth) fills.")]
        [Range(0.1f, 1f)] public float frameShare = 0.6f;
        [Tooltip("The narrowest the view may zoom to, degrees (the switch's door turn stops at 18; the gun is toy-sized and the kid looks from far).")]
        [Range(2f, 60f)] public float minFov = 8f;
        [Tooltip("Toy units added round the gun's stretch of floor when framing it.")]
        [Min(0f)] public float frameMargin = 2f;
        [Tooltip("A small light on the gun during the moment (from the view's turn to the drop), so it reads in the dark room: its colour.")]
        public Color gunLightColour = new Color(1f, 0.85f, 0.6f);
        [Tooltip("Its brightness (the toy's own glow in the dark room is 1.2).")]
        [Min(0f)] public float gunLightIntensity = 6f;
        [Tooltip("Its reach, toy units.")]
        [Min(0.1f)] public float gunLightRange = 8f;
        [Tooltip("The gun's glow during the moment, as a share of its full glint (0 = none).")]
        [Range(0f, 1f)] public float snatchGlow = 0.6f;

        [Header("The gun under the bed")]
        [Tooltip("On: the gun under the bed can be taken only by a toy-sized player (a kid's hand does not reach under the bed; a kid's E reaches about 46 along the floor from its feet, about 36 past its own body, and the bed is about 60 wide). Off: whoever reaches it.")]
        public bool toyOnly = true;
        [Tooltip("The glint when a flashlight's beam finds the gun (over 1 blooms).")]
        [ColorUsage(false, true)] public Color glintColour = new Color(2.4f, 2f, 1.2f, 1f);
        [Tooltip("Seconds of beam that bring the glint to full (any beam).")]
        [Min(0.01f)] public float glintFillSeconds = 0.25f;
        [Tooltip("Seconds it fades once the beam leaves.")]
        [Min(0f)] public float glintDrainSeconds = 1.2f;

        [Header("The maker's layout (Land Bedroom Gate)")]
        [Tooltip("How far out from the bed's mouth, toward the toy box, the gun lies before the snatch (toy units).")]
        [Min(1f)] public float gunOut = 10f;
        [Tooltip("Toy units between the grid points the maker tries for the gun's place under the bed.")]
        [Min(0.5f)] public float pickupGridStep = 3f;
        [Tooltip("The gun under the bed keeps at least this far from the monsters' kept way to the flashlight (its middle line), toy units.")]
        [Min(0f)] public float pickupLaneClear = 8f;
        [Tooltip("...and this far from each monster's spot (past its wander and its body).")]
        [Min(0f)] public float pickupMonsterClear = 4f;
        [Tooltip("...and this far from the bed's own flashlight, ammo, grenades and note.")]
        [Min(0f)] public float pickupThingsClear = 5f;
        [Tooltip("The least room over the gun's floor under the bed for a toy to reach it (the toy is 1.8 tall).")]
        [Min(0.5f)] public float pickupHeadroom = 3f;

        [Header("The steps, the star and the opening's line (the maker writes them once; his words are kept)")]
        [Tooltip("The new step after the box core's: its id (saved per slot as step:<id>).")]
        public string gunStepId = "under-bed";
        [Tooltip("Its words on the HUD. Placeholder.")]
        public string gunStepText = "Find what's under the bed";
        [Tooltip("The new step after that one: its id.")]
        public string gateStepId = "knock-out";
        [Tooltip("Its words on the HUD. Placeholder.")]
        public string gateStepText = "Knock out the monsters";
        [Tooltip("The box core's step (wake-box) gets these words when it still has its old placeholder. Placeholder.")]
        public string wakeBoxText = "Turn the light onto the core";
        [Tooltip("The gold star the gate's opening earns (D-0228's \"monsters\"): its id on Resources/ObjectiveSet.")]
        public string starId = "monsters";
        [Tooltip("The opening's second line (OpeningSettings > Goal Line): HIS words, written when that line still has an old placeholder.")]
        public string goalLine = "Am I in a box?";
    }
}
