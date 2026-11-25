using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// One zone of the house or the game (D-0224): a row of the Sticker Chart, and the place the objective card names. A
    /// zone is a box in the house (the arrival card's trigger with the same id, or a no-card ZoneBox the chart maker
    /// places) or a whole scene (the Raceway, the Colour Corner). It is VISITED (saved per save slot as zone:&lt;id&gt;) the
    /// first time this PC's player is inside it; until then the chart shows its stars as "???".
    /// </summary>
    [Serializable]
    public sealed class ChartZone
    {
        [Tooltip("Saved per save slot as zone:<id>; also the id of its box in the house (an ArrivalCard's id, or a ZoneBox's). Never reuse one for a different zone.")]
        public string id = "";
        [Tooltip("Its name on the chart and on the objective card. Placeholder wording: his to rewrite.")] public string title = "";
        [Tooltip("The word the stars and the steps use for it (ObjectiveSet > Gold stars > Room, The route > Room). Matched without caring about capitals.")]
        public string room = "";
        [Tooltip("Its row's place on the chart: lower comes first.")] public int order;
        [Tooltip("The scene it is in (Greybox for the house's rooms).")] public string scene = ObjectiveSet.House;
        [Tooltip("The whole scene is the zone (the Raceway, the Colour Corner): no box, it is the zone as soon as the scene is played.")]
        public bool wholeScene;
        [Tooltip("Visited from the start of every game (the bedroom: the toy wakes there), and the zone the house starts in.")]
        public bool fromStart;
    }

    /// <summary>
    /// The Sticker Chart's rows and their look-ups (D-0224). The rows live on Resources/ObjectiveSet; these are the
    /// defaults the asset starts with and the chart maker adds when an id is missing (it never overwrites what he wrote).
    /// Every word is a PLACEHOLDER for him to rewrite.
    /// </summary>
    public static class ChartWords
    {
        public const string Bedroom = "Bedroom", Hallway = "Hallway", Playroom = "Playroom", Wardrobe = "Wardrobe",
            Window = "Window", Downstairs = "Downstairs", Raceway = "Raceway", ColourCorner = "Colour Corner";

        /// <summary>The eight zones, matching the stars' Room words. The four house rooms with an arrival card use its box
        /// (same id); the bedroom and the window get a no-card ZoneBox from the chart maker.</summary>
        public static ChartZone[] DefaultZones() => new[]
        {
            new ChartZone { id = "bedroom", title = Bedroom, room = Bedroom, order = 0, fromStart = true },
            new ChartZone { id = "hallway", title = Hallway, room = Hallway, order = 1 },
            new ChartZone { id = "playroom", title = Playroom, room = Playroom, order = 2 },
            new ChartZone { id = "wardrobe", title = Wardrobe, room = Wardrobe, order = 3 },
            new ChartZone { id = "window", title = Window, room = Window, order = 4 },
            new ChartZone { id = "downstairs", title = Downstairs, room = Downstairs, order = 5 },
            new ChartZone { id = "raceway", title = Raceway, room = Raceway, order = 6, scene = ObjectiveSet.Raceway, wholeScene = true },
            new ChartZone { id = "colour-corner", title = ColourCorner, room = ColourCorner, order = 7, scene = ObjectiveSet.ColourCorner, wholeScene = true },
        };

        /// <summary>The zone each of D-0220's main steps happens in, by the step's id; "" for an id it does not know.</summary>
        public static string DefaultStepRoom(string stepId)
        {
            switch (stepId)
            {
                case "wake-box": case "little-door": case "through-door": return Bedroom;
                case "tall-end": return Hallway;
                case "core-a": case "safe": case "core-b": case "wardrobe": return Playroom;
                case "core-c": return Wardrobe;
                case "sill": return Window;
                default: return "";
            }
        }

        /// <summary>The step's zone word: its Room on the asset, or the default for its id when that is empty.</summary>
        public static string StepRoom(ObjectiveStep step)
        {
            if (step == null) return "";
            return string.IsNullOrWhiteSpace(step.room) ? DefaultStepRoom(step.id) : step.room.Trim();
        }

        /// <summary>True when two Room words name the same zone (spaces round them and capitals do not count).</summary>
        public static bool SameRoom(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary><paramref name="format"/> with two numbers and a name, or the words as they are if he wrote a broken {}.</summary>
        public static string Format(string format, int a, int b, string name)
        {
            if (string.IsNullOrEmpty(format)) return "";
            try { return string.Format(format, a, b, name ?? ""); }
            catch (FormatException) { return format; }
        }
    }
}
