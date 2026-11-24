using System;

namespace ShiftJam
{
    /// <summary>
    /// The twelve places a funny character wears a part (D-0183). Each one is a slot object directly under the Funny
    /// Characters rig (named as here, but FullBody's object is "Full_Body"). The numbers are fixed: a saved look and the
    /// look roll go by them, so a new slot goes at the END and none is ever reordered.
    /// </summary>
    public enum CharacterSlot
    {
        Body = 0,
        Hair = 1,
        Hat = 2,
        Glasses = 3,
        Eyebrow = 4,
        Mustache = 5,
        Outerwear = 6,
        Pants = 7,
        Shoe = 8,
        Glove = 9,
        Backpack = 10,
        FullBody = 11,
    }

    /// <summary>Names and counts for <see cref="CharacterSlot"/> (D-0183), with no allocation (an enum's ToString makes a
    /// new string every time).</summary>
    public static class CharacterSlots
    {
        /// <summary>How many slots there are.</summary>
        public const int Count = 12;

        // The slot's name in a look's text, then the name of its object under the rig (and the start of its parts'
        // file names). Index = the slot's number.
        private static readonly string[] TextNames = { "Body", "Hair", "Hat", "Glasses", "Eyebrow", "Mustache", "Outerwear", "Pants", "Shoe", "Glove", "Backpack", "FullBody" };
        private static readonly string[] ObjectNames = { "Body", "Hair", "Hat", "Glasses", "Eyebrow", "Mustache", "Outerwear", "Pants", "Shoe", "Glove", "Backpack", "Full_Body" };

        /// <summary>True for one of the twelve slots (false for a number cast from outside the range).</summary>
        public static bool IsValid(CharacterSlot slot) => (int)slot >= 0 && (int)slot < Count;

        /// <summary>The slot's name as a look's text writes it ("FullBody"). "" for a slot outside the range.</summary>
        public static string TextName(CharacterSlot slot) => IsValid(slot) ? TextNames[(int)slot] : "";

        /// <summary>The name of the slot's object directly under the rig ("Full_Body"); every part file of that slot
        /// starts with it too. "" for a slot outside the range.</summary>
        public static string ObjectName(CharacterSlot slot) => IsValid(slot) ? ObjectNames[(int)slot] : "";

        /// <summary>The slot with this name: its text name or its object name, any case. False when no slot has it.</summary>
        public static bool TryParse(string name, out CharacterSlot slot)
        {
            slot = CharacterSlot.Body;
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < Count; i++)
            {
                if (string.Equals(name, TextNames[i], StringComparison.OrdinalIgnoreCase) || string.Equals(name, ObjectNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    slot = (CharacterSlot)i;
                    return true;
                }
            }
            return false;
        }
    }
}
