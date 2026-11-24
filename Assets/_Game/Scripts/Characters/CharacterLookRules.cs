using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// How a funny character's look is rolled (D-0183): how often each part is worn, the heaviest part a slot may
    /// take, which bodies go with which colour word, and what never goes together. The default rules live at
    /// Resources/CharacterLookRules; Shift Jam > Characters > Build Characters makes that asset once and never writes it
    /// again, so every number here is yours to tune. A body can be given other rules (say, plainer ones for the house).
    /// Two rules are fixed, not numbers: a body is always worn, and a full-body suit takes every slot but the body (the
    /// pack's own rule).
    /// Changing a number changes the looks rolled from then on, on every PC alike; a look saved as text stays as it is.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Character Look Rules", fileName = "CharacterLookRules")]
    public sealed class CharacterLookRules : ScriptableObject
    {
        /// <summary>The heaviest part one slot may take.</summary>
        [Serializable]
        public struct SlotLimit
        {
            [Tooltip("The slot this limit is for.")]
            public CharacterSlot slot;
            [Tooltip("The most triangles a part on this slot may have. 0 = no limit. If no part is light enough, the limit is ignored for that slot.")]
            public int triangles;
        }

        /// <summary>The bodies one colour word may use.</summary>
        [Serializable]
        public sealed class ColourBodies
        {
            [Tooltip("The colour word (as in the part file names: Blue, Orange, Light_Blue). Any case.")]
            public string colour = "";
            [Tooltip("The ids of the bodies this colour may use (Body_Orange_001). Bodies of the colour that look wrong can be left out. None found: any body named with the colour word; none of those: any body.")]
            public string[] bodies = new string[0];
        }

        [Header("How often each part is worn (0 = never, 1 = always; a body is always worn)")]
        [Tooltip("Share of characters with eyebrows.")]
        [Range(0f, 1f)] public float eyebrow = 1f;
        [Tooltip("Share of characters with a top (shirt, jacket).")]
        [Range(0f, 1f)] public float outerwear = 1f;
        [Tooltip("Share of characters with trousers.")]
        [Range(0f, 1f)] public float pants = 1f;
        [Tooltip("Share of characters with shoes.")]
        [Range(0f, 1f)] public float shoe = 1f;
        [Tooltip("Share of characters with hair.")]
        [Range(0f, 1f)] public float hair = 0.7f;
        [Tooltip("Share of characters with glasses.")]
        [Range(0f, 1f)] public float glasses = 0.3f;
        [Tooltip("Share of characters with a mustache (its colour by chance).")]
        [Range(0f, 1f)] public float mustache = 0.3f;
        [Tooltip("Share of characters with gloves.")]
        [Range(0f, 1f)] public float glove = 0.4f;
        [Tooltip("Share of characters with a backpack.")]
        [Range(0f, 1f)] public float backpack = 0.1f;
        [Tooltip("Share of characters in a full-body suit (a banana, a shark...). A suit takes every slot but the body.")]
        [Range(0f, 1f)] public float fullBody = 0f;

        [Header("Hats")]
        [Tooltip("Share of characters WITHOUT hair who wear a hat.")]
        [Range(0f, 1f)] public float hatWhenNoHair = 1f;
        [Tooltip("Share of characters WITH hair who wear a hat (only over hair no taller than the next number).")]
        [Range(0f, 1f)] public float hatOverHair = 0.25f;
        [Tooltip("The tallest hair a hat may sit over: how high the hair reaches above the soles at scale 1 (the head's top is 1.72; hair up to 1.8 stays under a hat).")]
        public float tallestHairUnderHat = 1.8f;

        [Header("Heaviest part allowed (triangles)")]
        [Tooltip("The most triangles a part may have, per slot. A slot not listed (or 0) has no limit.")]
        public SlotLimit[] heaviest =
        {
            new SlotLimit { slot = CharacterSlot.Hair, triangles = 3000 },
            new SlotLimit { slot = CharacterSlot.Shoe, triangles = 3500 },
            new SlotLimit { slot = CharacterSlot.Backpack, triangles = 3000 },
            new SlotLimit { slot = CharacterSlot.Hat, triangles = 2000 },
            new SlotLimit { slot = CharacterSlot.FullBody, triangles = 6000 },
        };

        [Header("Colours")]
        [Tooltip("Per colour word, the bodies that colour may use. A colour word (a team's) sets the body, the top and the trousers to that colour; a colour not listed here uses the bodies named with it.")]
        public ColourBodies[] colourBodies =
        {
            new ColourBodies { colour = "Blue", bodies = new[] { "Body_Blue_001", "Body_Blue_002", "Body_Blue_003" } },
            new ColourBodies { colour = "Orange", bodies = new[] { "Body_Orange_001", "Body_Orange_002" } },   // Body_Orange_003 reads brown
        };
        [Tooltip("The colour word of the practice look (the practice soldiers).")]
        public string practiceColour = "Green";

        private static CharacterLookRules _loaded;
        private static bool _looked;
        private static CharacterLookRules _builtIn;

        /// <summary>The default rules: Resources/CharacterLookRules, or (when that asset is missing) these numbers as
        /// written in this file. Never null.</summary>
        public static CharacterLookRules Default
        {
            get
            {
                if (!_looked || (_loaded == null && !Application.isPlaying))
                {
                    _looked = true;
                    _loaded = Resources.Load<CharacterLookRules>("CharacterLookRules");
                }
                if (_loaded != null) return _loaded;
                if (_builtIn == null)
                {
                    _builtIn = CreateInstance<CharacterLookRules>();
                    _builtIn.name = "CharacterLookRules (built in)";
                    _builtIn.hideFlags = HideFlags.HideAndDontSave;
                }
                return _builtIn;
            }
        }

        /// <summary>The share of characters (0..1) who wear something on this slot. The body is always 1. The hat is
        /// decided by <see cref="hatWhenNoHair"/> and <see cref="hatOverHair"/>, so it is not answered here (0).</summary>
        public float ChanceOf(CharacterSlot slot)
        {
            switch (slot)
            {
                case CharacterSlot.Body: return 1f;
                case CharacterSlot.Hair: return hair;
                case CharacterSlot.Glasses: return glasses;
                case CharacterSlot.Eyebrow: return eyebrow;
                case CharacterSlot.Mustache: return mustache;
                case CharacterSlot.Outerwear: return outerwear;
                case CharacterSlot.Pants: return pants;
                case CharacterSlot.Shoe: return shoe;
                case CharacterSlot.Glove: return glove;
                case CharacterSlot.Backpack: return backpack;
                case CharacterSlot.FullBody: return fullBody;
                default: return 0f;
            }
        }

        /// <summary>The most triangles a part on this slot may have; 0 = no limit. Of two rows for one slot the first
        /// counts.</summary>
        public int HeaviestFor(CharacterSlot slot)
        {
            if (heaviest == null) return 0;
            for (int i = 0; i < heaviest.Length; i++)
                if (heaviest[i].slot == slot) return Mathf.Max(0, heaviest[i].triangles);
            return 0;
        }

        /// <summary>The body ids listed for this colour word (any case), or null when the word is empty or not listed.
        /// Of two rows for one word the first counts.</summary>
        public string[] BodiesFor(string colour)
        {
            if (string.IsNullOrEmpty(colour) || colourBodies == null) return null;
            for (int i = 0; i < colourBodies.Length; i++)
            {
                ColourBodies row = colourBodies[i];
                if (row != null && string.Equals(row.colour != null ? row.colour.Trim() : "", colour, StringComparison.OrdinalIgnoreCase)) return row.bodies;
            }
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _loaded = null; _looked = false; }
    }
}
