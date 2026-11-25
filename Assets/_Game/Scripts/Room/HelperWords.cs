using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>One room's arrival card (D-0221): the room's name big and one line of what to do, once per save slot, when
    /// the player first comes into the box. The objectives' helper maker places a trigger at the box.</summary>
    [Serializable]
    public sealed class ArrivalWords
    {
        [Tooltip("Never reuse one for a different room: the card is remembered per save slot as toast:arrival:<id>.")] public string id = "";
        [Tooltip("The room's name, big. Placeholder wording: his to rewrite. Empty = no card for this room.")] public string title = "";
        [Tooltip("One line of what to do, small under the name. Placeholder wording: his to rewrite. Empty = the name alone.")] public string line = "";
        [Tooltip("The card's colour.")] public Color colour = Color.white;
        [Tooltip("Wait until the player stands on the ground (a room you glide down into shows its card on landing).")] public bool onGround;
        [Tooltip("The middle of the box that counts as arriving, in the world (the maker puts the trigger here).")] public Vector3 centre;
        [Tooltip("The box's size, in the world.")] public Vector3 size = Vector3.one;
    }

    /// <summary>Where a core is, in words (D-0221): "No charge" names the nearest one that can refill the player.</summary>
    [Serializable]
    public sealed class CorePlace
    {
        [Tooltip("The core's object name (its CoreMarker's Place).")] public string core = "";
        [Tooltip("Where it is, read after the asset's No Charge Format ('in the army fort'). Placeholder wording: his to rewrite. Empty = the old words for this core.")]
        public string where = "";
    }

    /// <summary>A place where a kid does not fit, and where there is room (D-0221): "No room to grow" in here says so.</summary>
    [Serializable]
    public sealed class GrowRoom
    {
        [Tooltip("A short name for the maker's report.")] public string id = "";
        [Tooltip("Said instead of 'No room to grow' while the player is in the box. Placeholder wording: his to rewrite. Empty = the old words.")]
        public string words = "";
        [Tooltip("The middle of the box, in the world.")] public Vector3 centre;
        [Tooltip("The box's size, in the world.")] public Vector3 size = Vector3.one;
    }

    /// <summary>What Gap, Pit and Dab say while a main step is the current one (D-0221).</summary>
    [Serializable]
    public sealed class StepTalk
    {
        [Tooltip("The main step's id (ObjectiveSet > The route).")] public string step = "";
        [Tooltip("Their line while that step is the current one. Placeholder wording: his to rewrite. Empty = their own lines only.")]
        public string line = "";
    }

    /// <summary>
    /// The helpers' words and their look-ups (D-0221: arrival cards, refusals that point, the house characters' step lines).
    /// The words live on Resources/ObjectiveSet; these are the defaults the asset starts with and the maker adds when an id
    /// is missing (it never overwrites what he wrote). Every word is a PLACEHOLDER for him to rewrite.
    /// </summary>
    public static class HelperWords
    {
        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        /// <summary>The four rooms' cards. Boxes measured from Greybox on 25 Nov: the hallway's lane (not its tall end),
        /// the playroom, the wardrobe's inside, and the whole downstairs (under its ceiling).</summary>
        public static ArrivalWords[] DefaultArrivals() => new[]
        {
            new ArrivalWords { id = "hallway", title = "THE HALLWAY", line = "Follow it to the tall end", colour = Hex(0x8ECAE6),
                centre = new Vector3(86f, 7f, -150f), size = new Vector3(292f, 16f, 21f) },
            new ArrivalWords { id = "playroom", title = "THE PLAYROOM", line = "Grow, lift the safe, shrink into the wardrobe", colour = Hex(0xFFD166),
                centre = new Vector3(480f, 65f, -140f), size = new Vector3(390f, 130f, 325f) },
            new ArrivalWords { id = "wardrobe", title = "THE WARDROBE", line = "Climb to the top shelf", colour = Hex(0xC4B5FD),
                centre = new Vector3(480f, 85f, -700f), size = new Vector3(196f, 170f, 121f) },
            new ArrivalWords { id = "downstairs", title = "THE LIVING ROOM", line = "Knock every tan soldier down once", colour = Hex(0xE9C89A), onGround = true,
                centre = new Vector3(355f, -95f, -135f), size = new Vector3(650f, 130f, 390f) },
        };

        /// <summary>The ten cores in Greybox on 25 Nov, by object name.</summary>
        public static CorePlace[] DefaultCorePlaces() => new[]
        {
            new CorePlace { core = "ShiftCore", where = "in the toy box" },
            new CorePlace { core = "ShiftCore2", where = "up on the bed" },
            new CorePlace { core = "ChargeCore", where = "by the pencil bridge" },
            new CorePlace { core = "ChargeCoreDark", where = "in the dark past the bridge" },
            new CorePlace { core = "ChargeCoreTall", where = "at the hallway's tall end" },
            new CorePlace { core = "CoreE_Shelf", where = "on the shelf at the hallway's tall end" },
            new CorePlace { core = "CoreA_Grow", where = "in the army fort" },
            new CorePlace { core = "CoreB_Shrink", where = "on the desk" },
            new CorePlace { core = "CoreC_Grow", where = "on the wardrobe's top shelf" },
            new CorePlace { core = "CoreD_Window", where = "on the window sill" },
        };

        /// <summary>The hallway's lane is toy-high all the way to its tall end (Room/Course/R3_SkirtingRun's walls and ceiling).</summary>
        public static GrowRoom[] DefaultGrowRooms() => new[]
        {
            new GrowRoom { id = "hallway", words = "No room to grow here. The hallway's tall end has room.",
                centre = new Vector3(86f, 7f, -150f), size = new Vector3(292f, 16f, 21f) },
        };

        /// <summary>One line per main step of D-0220's route.</summary>
        public static StepTalk[] DefaultStepTalk() => new[]
        {
            new StepTalk { step = "wake-box", line = "The core in your box is asleep. Shine the flashlight on it." },
            new StepTalk { step = "little-door", line = "The switch on the shelf opens the little door." },
            new StepTalk { step = "through-door", line = "The little door is open. Go on through." },
            new StepTalk { step = "tall-end", line = "Follow the hallway to its tall end. There's room to grow there." },
            new StepTalk { step = "core-a", line = "There's a grow core in the army fort." },
            new StepTalk { step = "safe", line = "Lift the safe off the hole under the wardrobe." },
            new StepTalk { step = "core-b", line = "The shrink core is up on the desk." },
            new StepTalk { step = "wardrobe", line = "Small again? In you go, under the wardrobe." },
            new StepTalk { step = "core-c", line = "Climb to the top shelf. That core grows you out." },
            new StepTalk { step = "sill", line = "Up onto the window sill!" },
        };

        /// <summary>The card for <paramref name="id"/>, or null.</summary>
        public static ArrivalWords Arrival(ObjectiveSet set, string id)
        {
            if (set == null || set.arrivals == null || string.IsNullOrEmpty(id)) return null;
            foreach (ArrivalWords a in set.arrivals) if (a != null && a.id == id) return a;
            return null;
        }

        /// <summary>Where the core named <paramref name="core"/> is, or "".</summary>
        public static string CorePlaceOf(ObjectiveSet set, string core)
        {
            if (set == null || set.corePlaces == null || string.IsNullOrEmpty(core)) return "";
            foreach (CorePlace p in set.corePlaces) if (p != null && p.core == core) return p.where ?? "";
            return "";
        }

        /// <summary>The words of the first grow room whose box holds <paramref name="point"/>, or "".</summary>
        public static string GrowWordsAt(ObjectiveSet set, Vector3 point)
        {
            if (set == null || set.growRooms == null) return "";
            foreach (GrowRoom r in set.growRooms)
                if (r != null && !string.IsNullOrWhiteSpace(r.words) && new Bounds(r.centre, r.size).Contains(point)) return r.words;
            return "";
        }

        /// <summary>The house characters' line for the step <paramref name="stepId"/>, or "".</summary>
        public static string StepLine(ObjectiveSet set, string stepId)
        {
            if (set == null || set.stepTalk == null || string.IsNullOrEmpty(stepId)) return "";
            foreach (StepTalk t in set.stepTalk) if (t != null && t.step == stepId) return t.line ?? "";
            return "";
        }

        /// <summary><paramref name="format"/> with one value, or the words as they are if he wrote a broken {}.</summary>
        public static string Format(string format, string value)
        {
            if (string.IsNullOrEmpty(format)) return value ?? "";
            try { return string.Format(format, value ?? ""); }
            catch (FormatException) { return format; }
        }
    }
}
