using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Every number, sound and word of the claw machine you can play (D-0229): how the round goes (the time limit, the
    /// steering), how the claw moves (drop, fingers, rise, carry, the swing), what wins (the overlap needed, the grab
    /// chance, the slip of a near miss), how a won prize comes out, the framed view, the sounds (the pack's own SFX) and the
    /// words (placeholders). Lengths and speeds are in the MACHINE's own units, the claw pack's metres: 1 unit is the
    /// machine's scale in the room (28.54 now), so the numbers hold if the machine is ever resized. The pack's own numbers
    /// are given in the tooltips.
    /// Loaded from Resources/ClawGameSettings; Shift Jam > House > Land Claw Game (D-0229) makes the asset (an existing asset
    /// keeps its numbers: only an empty sound is filled and a missing prize name added).
    /// Every player-facing word here is a PLACEHOLDER for him to rewrite (in-game text by Claude counts for Steam's AI
    /// disclosure).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Claw Game Settings", fileName = "ClawGameSettings")]
    public sealed class ClawGameSettings : ScriptableObject
    {
        /// <summary>The name the asset is loaded by: Resources/ClawGameSettings.</summary>
        public const string ResourceName = "ClawGameSettings";

        /// <summary>A prize's name in the "you won" toast, by its monster kind.</summary>
        [Serializable]
        public sealed class PrizeWords
        {
            [Tooltip("The prize's monster kind (MonsterSettings > Kinds > Name).")] public string kind = "";
            [Tooltip("Its name in the toast. Placeholder: his to rewrite.")] public string words = "";
        }

        [Header("Switch")]
        [Tooltip("OFF SWITCH: unticked, the claw machine is a still prop again (no prompt, nothing moves).")]
        public bool on = true;
        [Tooltip("Online (a party in the house). Ticked: each PC's machine plays for its own player, nothing networked (the others see a kid standing still and never see the prizes won there). Unticked: no prompt online.")]
        public bool online;

        [Header("Words (placeholders: his to rewrite)")]
        [Tooltip("Beside the E key when the kid stands at the machine's front.")] public string playPrompt = "Play";
        [Tooltip("Beside the E key when every prize has been won.")] public string emptyPrompt = "Empty";
        [Tooltip("The toast when E is pressed at an empty machine.")] public string emptyToast = "No prizes left in the claw machine.";
        [Tooltip("The panel's heading while playing.")] public string hudTitle = "CLAW MACHINE";
        [Tooltip("The panel's line of keys while playing. <color=...> tints a word.")]
        public string hudKeys = "<color=#8FE3FF>Move</color> steer  <color=#8FE3FF>E/Space</color> drop  <color=#8FE3FF>Esc</color> give up  <color=#7F8C99>(pad: stick, A/Y, B)</color>";
        [Tooltip("The panel's time left. {0} = whole seconds left.")] public string hudTimeFormat = "{0}";
        [Tooltip("The toast when a prize drops into the chute. {0} = the prize's name (Prize Names).")] public string wonToast = "You won the {0}!";
        [Tooltip("The toast when the claw comes up empty. Empty = no toast (the miss sound only).")] public string missToast = "Missed.";
        [Tooltip("The toast when a held prize slips out on the way up. Empty = no toast.")] public string slipToast = "So close! It slipped.";
        [Tooltip("Each prize's name by its monster kind. A kind not listed is named from the kind itself (\"BlueSlime\" says \"Blue Slime\").")]
        public PrizeWords[] prizeNames = DefaultPrizeNames();

        [Header("The round")]
        [Min(1f), Tooltip("Seconds of steering before the claw drops by itself.")] public float timeLimit = 20f;
        [Min(0.01f), Tooltip("How fast the claw is steered across the case, machine units a second (the pack: 0.286).")] public float steerSpeed = 0.29f;
        [Min(0f), Tooltip("Seconds the steering takes to reach full speed, and to stop (0 = at once, as the pack).")] public float steerEase = 0.12f;
        [Range(0f, 0.9f), Tooltip("Stick or key movement under this share counts as none.")] public float deadZone = 0.15f;
        [Min(0f), Tooltip("The maker: room kept between the open claw (or the carriage and gantry) and the glass, machine units.")] public float wallGap = 0.02f;
        [Min(0.5f), Tooltip("The round ends if the player is moved further than this from the play spot (a knock, a respawn), machine units.")] public float leaveDistance = 3f;
        [Min(0.2f), Tooltip("The kid must stand in front of the machine within this of the glass's front to play, machine units.")] public float playReach = 2.2f;

        [Header("The claw")]
        [Min(0.01f), Tooltip("How fast the claw goes down, machine units a second (the pack: 0.216).")] public float dropSpeed = 0.3f;
        [Min(0.01f), Tooltip("How fast it comes back up (the pack: 0.18).")] public float riseSpeed = 0.25f;
        [Min(0.01f), Tooltip("How fast it carries a prize to the chute and goes home after, machine units a second (the pack: 0.286).")] public float carrySpeed = 0.35f;
        [Tooltip("The fingers' open angle, degrees about each finger's own x (the pack: -43).")] public float fingerOpen = -43f;
        [Tooltip("The fingers' fully closed angle, degrees (the pack: 23).")] public float fingerClosed = 23f;
        [Min(1f), Tooltip("How fast the fingers turn, degrees a second (the pack: 55).")] public float fingerSpeed = 90f;
        [Min(0f), Tooltip("Seconds the claw waits at the bottom before closing, and after closing before it rises.")] public float settleSeconds = 0.25f;
        [Min(0f), Tooltip("Room kept between the fingertips and the floor at the claw's lowest, machine units.")] public float floorGap = 0.008f;
        [Min(0f), Tooltip("The claw's middle stops this far over the top of the prize it lands on, machine units.")] public float topGap = 0.004f;
        [Min(0f), Tooltip("The fingers stop this far out from the prize's side when they close on it, machine units.")] public float fingerGap = 0.01f;

        [Header("The swing")]
        [Range(0f, 15f), Tooltip("Degrees the rod and claw swing at most (the pack: 5).")] public float maxSwing = 5f;
        [Min(0f), Tooltip("How hard a change of the claw's speed sets it swinging: degrees a second per machine unit a second.")] public float swingKick = 45f;
        [Min(0f), Tooltip("How stiffly it swings back to hanging straight.")] public float swingSpring = 30f;
        [Min(0f), Tooltip("How fast a swing dies away.")] public float swingDamping = 3f;
        [Min(0f), Tooltip("The jolt when the fingers take hold of a prize, degrees a second.")] public float grabJolt = 12f;
        [Range(0f, 45f), Tooltip("Degrees the lever tilts at full steer (the pack: 22).")] public float leverTilt = 22f;
        [Min(0.001f), Tooltip("Seconds the lever takes to follow the steering (the pack: 0.045).")] public float leverEase = 0.05f;
        [Min(0f), Tooltip("How far the button goes in on the drop, machine units (the pack: 0.012).")] public float buttonDepth = 0.012f;
        [Min(0.02f), Tooltip("Seconds the button takes to go in and come back out (the pack: 0.24).")] public float buttonSeconds = 0.24f;

        [Header("The grab")]
        [Min(0.001f), Tooltip("How far from the claw's middle a prize's middle may be and still be held, machine units (the pack: 0.17). The overlap is 1 dead centre and 0 at this distance.")]
        public float graspReach = 0.12f;
        [Range(0f, 1f), Tooltip("The least overlap for the fingers to take hold at all.")] public float overlapNeeded = 0.3f;
        [Range(0f, 1f), Tooltip("The chance the fingers take hold when the overlap is enough.")] public float grabChance = 0.75f;
        [Range(0f, 1f), Tooltip("At or over this overlap a held prize never slips (a clean grab).")] public float firmOverlap = 0.7f;
        [Range(0f, 1f), Tooltip("The chance a prize held with less than Firm Overlap slips out on the way up (a near miss).")] public float slipChance = 0.5f;
        [Tooltip("Where on the way up a slip happens: a share of the rise, from x to y.")] public Vector2 slipBetween = new Vector2(0.25f, 0.75f);
        [Min(0f), Tooltip("Seconds a slipped prize takes to drop back where it was taken from.")] public float slipFallSeconds = 0.35f;

        [Header("The prize coming out")]
        [Min(0.05f), Tooltip("Seconds a let-go prize takes to fall into the chute's hole.")] public float fallSeconds = 0.35f;
        [Range(0f, 90f), Tooltip("How far it tumbles as it falls into the hole, degrees at most each way.")] public float fallTumble = 30f;
        [Tooltip("The chute flap's open angle, degrees about its own x (the pack: -100).")] public float hatchOpenAngle = -100f;
        [Min(0.05f), Tooltip("Seconds the flap takes to open (the pack: 0.45).")] public float hatchOpenSeconds = 0.45f;
        [Min(0.05f), Tooltip("Seconds it takes to close (the pack: 0.4).")] public float hatchCloseSeconds = 0.4f;
        [Min(0f), Tooltip("How far behind the flap's outside a won prize starts, inside the machine, machine units (the pack: 0.43).")] public float insideDepth = 0.43f;
        [Min(0.05f), Tooltip("Seconds the prize takes to come out through the flap.")] public float exitSeconds = 0.45f;
        [Min(0.05f), Tooltip("Seconds it takes from the flap down to the floor.")] public float landSeconds = 0.3f;
        [Min(0f), Tooltip("Its roll on landing, machine units a second, away from the machine (and the kid).")] public float rollSpeed = 0.6f;
        [Min(0f), Tooltip("Its tumble on landing, turns a second at most.")] public float rollSpin = 1.5f;
        [Min(0.01f), Tooltip("A won prize's weight (the kid's grab lifts up to 20; first person's hands have their own limit).")] public float prizeMass = 1f;
        [Min(0f), Tooltip("Seconds the chute's view stays on the prize once it is on the floor.")] public float showSeconds = 1.4f;
        [Min(0.01f), Tooltip("Won prizes put back out front on a reload stand this far apart, machine units.")] public float wonSpacing = 0.24f;

        [Header("The view")]
        [Range(10f, 90f), Tooltip("The framed views' field of view (up and down), degrees. The maker fits the glass into it; re-run the maker after a change.")]
        public float viewFieldOfView = 40f;
        [Range(-10f, 60f), Tooltip("The maker: how far the glass's view looks down, degrees.")] public float viewPitch = 22f;
        [Range(-60f, 60f), Tooltip("The maker: the glass's view turned this many degrees round the machine from straight in front.")] public float viewYaw;
        [Range(0f, 0.4f), Tooltip("The maker: room kept round the glass in the view, a share of the frame.")] public float viewMargin = 0.08f;
        [Range(-10f, 70f), Tooltip("The maker: how far the chute's view looks down, degrees.")] public float chuteViewPitch = 28f;
        [Min(0.1f), Tooltip("The maker: how much the chute's view holds, top to bottom, machine units.")] public float chuteViewSpan = 1.1f;
        [Min(0f), Tooltip("A framed view is pulled in this far short of a wall, machine units (also the ball the way is swept with).")] public float viewClearance = 0.04f;
        [Min(0.01f), Tooltip("Seconds the view takes to swing to the glass.")] public float blendIn = 0.6f;
        [Min(0.01f), Tooltip("Seconds it takes to swing back to the player's own view.")] public float blendOut = 0.8f;
        [Min(0.01f), Tooltip("Seconds it takes to swing from the glass to the chute.")] public float chuteBlend = 0.55f;
        [Min(0f), Tooltip("Seconds the glass stays in view after a miss or a slip, before the view swings back.")] public float missHold = 0.8f;
        [Range(0.05f, 0.95f), Tooltip("The player's body is hidden once the swing to the glass is this far along (it stands between the camera and the glass), and shown again when the swing back passes the same point.")]
        public float hideBodyAt = 0.5f;

        [Header("Sounds (the claw pack's own SFX; the maker fills empty ones)")]
        [Tooltip("The start and the drop: Button_v4.")] public CourseSounds.Cue button = new CourseSounds.Cue(null, 0.7f, 0.05f);
        [Tooltip("Played round while the claw moves across: Claw_Movement_v1.")] public CourseSounds.Cue moveLoop = new CourseSounds.Cue(null, 0.45f);
        [Tooltip("Played round while the claw goes down or up: Claw_Down_v5.")] public CourseSounds.Cue dropLoop = new CourseSounds.Cue(null, 0.5f);
        [Tooltip("Played round while the fingers turn: Claw_Movement_v2.")] public CourseSounds.Cue fingerLoop = new CourseSounds.Cue(null, 0.4f);
        [Tooltip("A prize let go over the chute: Toy_Drop_v2.")] public CourseSounds.Cue toyDrop = new CourseSounds.Cue(null, 0.8f, 0.05f);
        [Tooltip("A win: Win_Sound_v2.")] public CourseSounds.Cue win = new CourseSounds.Cue(null, 0.8f);
        [Tooltip("A miss: Lose_Sound_v2.")] public CourseSounds.Cue miss = new CourseSounds.Cue(null, 0.7f);
        [Tooltip("A slip: Toy_Stuck_v3.")] public CourseSounds.Cue slip = new CourseSounds.Cue(null, 0.7f, 0.05f);
        [Tooltip("The won prize landing on the floor: the Playroom's Ball_Bounce_v1 (a soft thump). None = silent.")] public CourseSounds.Cue land = new CourseSounds.Cue(null, 0.6f, 0.1f);
        [Range(0f, 1f), Tooltip("How much the machine's sounds come from the machine (1) rather than everywhere (0).")] public float soundSpace = 0.75f;
        [Min(0.01f), Tooltip("Full volume within this of the machine, machine units.")] public float soundNear = 1.5f;
        [Min(0.1f), Tooltip("Silent past this, machine units.")] public float soundFar = 9f;

        [Header("The gold star (D-0229)")]
        [Tooltip("The star's id on Resources/ObjectiveSet (saved per slot as star:<id>). Empty = no star.")] public string starId = "claw-champ";
        [Tooltip("Its title, written once when the maker adds it (his edits on ObjectiveSet are kept). Placeholder.")] public string starTitle = "Claw champ";
        [Tooltip("Its zone on the Sticker Chart, written once when the maker adds it.")] public string starRoom = "Bedroom";

        [Header("Saving")]
        [Tooltip("A won prize is saved per slot under this plus its object's name (SaveGame's seen list): a reload does not put it back in the case; New Game does.")]
        public string saveKey = "claw:won:";

        private static ClawGameSettings _loaded;

        /// <summary>Each Play loads afresh: the editor may enter Play without reloading scripts, and a soundless default made
        /// before the maker made the asset must not outlive it.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Forget() { _loaded = null; }

        /// <summary>Resources/ClawGameSettings, or the defaults (with no sounds) when the maker has not made it.</summary>
        public static ClawGameSettings Load()
        {
            if (_loaded == null) _loaded = Resources.Load<ClawGameSettings>(ResourceName);
            if (_loaded == null)
            {
                Debug.LogWarning("CLAW GAME no Resources/" + ResourceName + ": run Shift Jam > House > Land Claw Game (D-0229). Using defaults with no sounds.");
                _loaded = CreateInstance<ClawGameSettings>();
            }
            return _loaded;
        }

        /// <summary>The prize's name for the toast: its kind's words, else the kind split at its capitals.</summary>
        public string NameOf(string kind)
        {
            if (prizeNames != null)
                for (int i = 0; i < prizeNames.Length; i++)
                    if (prizeNames[i] != null && prizeNames[i].kind == kind && !string.IsNullOrEmpty(prizeNames[i].words)) return prizeNames[i].words;
            if (string.IsNullOrEmpty(kind)) return "prize";
            var sb = new System.Text.StringBuilder(kind.Length + 4);
            for (int i = 0; i < kind.Length; i++)
            {
                if (i > 0 && char.IsUpper(kind[i]) && char.IsLower(kind[i - 1])) sb.Append(' ');
                sb.Append(kind[i]);
            }
            return sb.ToString();
        }

        /// <summary><paramref name="format"/> with {0} filled, or the words as they are if he wrote a broken {}.</summary>
        public static string Fill(string format, object value)
        {
            if (string.IsNullOrEmpty(format)) return "";
            try { return string.Format(format, value); }
            catch (FormatException) { return format; }
        }

        /// <summary>The prizes' names as first written (the kinds D-0223 put in the case). Placeholders.</summary>
        public static PrizeWords[] DefaultPrizeNames() => new[]
        {
            new PrizeWords { kind = "Slime", words = "Slime" },
            new PrizeWords { kind = "TurtleShell", words = "Turtle Shell" },
            new PrizeWords { kind = "BlueSlime", words = "Blue Slime" },
            new PrizeWords { kind = "StarFish", words = "Starfish" },
            new PrizeWords { kind = "Beholder", words = "Beholder" },
            new PrizeWords { kind = "ChestMonster", words = "Chest Monster" },
            new PrizeWords { kind = "PartyC01", words = "Party Monster" },
            new PrizeWords { kind = "PartyP01", words = "Party Monster" },
        };
    }
}
