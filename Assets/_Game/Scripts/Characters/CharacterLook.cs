using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What one funny character wears (D-0183): one part id a slot, "" = nothing on that slot. A plain value: it knows
    /// nothing about meshes (CharacterBody puts it on) and can be saved, sent and compared as one line of text.
    ///
    /// The text: "Slot=id" for each worn slot, in slot order, joined by ';' (for example
    /// "Body=Body_Blue_001;Eyebrow=Eyebrow_003;Outerwear=Outerwear_Blue_002;Pants=Pants_Blue_006;Shoe=Shoe_Sneaker_018").
    /// An empty text is a look with nothing worn.
    ///
    /// The roll (<see cref="Roll(CharacterCatalog, CharacterLookRules, int, string)"/>) gives the same look from the same
    /// seed, rules, colour and parts list on every PC and in every run: it uses its own small number maker, never Unity's
    /// or .NET's. HOW A ROLL DRAWS (changing any of this changes every rolled character in every scene):
    ///  1. Each slot has its own stream of numbers, started from the seed and the slot's number, so a change to one
    ///     slot's chance never changes what another slot gets.
    ///  2. The slots are decided in slot order (Body, Hair, Hat, Glasses, Eyebrow, Mustache, Outerwear, Pants, Shoe, Glove,
    ///     Backpack, FullBody). In each slot's stream the FIRST number says whether the slot is worn (it is drawn even for
    ///     a slot that is always worn); only when it is, the SECOND number picks the part.
    ///  3. The part is picked from that slot's parts in the parts list's order, narrowed in this order: parts within the
    ///     slot's triangle limit (none: the limit is ignored); then, for Body, Outerwear and Pants when a colour word is
    ///     given, the parts of that colour (for Body: the rules' bodies for the word first, then bodies named with the
    ///     word; none: no narrowing).
    ///  4. The hat looks at the hair already picked: no hair = the no-hair chance; hair taller than the rules allow = no
    ///     hat; else the over-hair chance.
    ///  5. A full-body suit, when worn, then takes off everything but the body.
    /// </summary>
    [Serializable]
    public sealed class CharacterLook
    {
        private const char EntrySeparator = ';';
        private const char NameSeparator = '=';
        // A chance is compared out of this many steps (one in a million).
        private const int ChanceSteps = 1000000;

        [Tooltip("The part id worn on each slot, by the slot's number; empty = nothing worn there.")]
        [SerializeField] private string[] parts = NewParts();

        // Scratch lists for the roll's narrowing: reused, so a roll makes no garbage beyond the look itself.
        private static readonly List<CharacterCatalog.Part> Pool = new List<CharacterCatalog.Part>();
        private static readonly List<CharacterCatalog.Part> Narrow = new List<CharacterCatalog.Part>();
        private static readonly StringBuilder Text = new StringBuilder();

        /// <summary>A look with nothing worn.</summary>
        public CharacterLook() { }

        /// <summary>A copy of another look (nothing worn when it is null).</summary>
        public CharacterLook(CharacterLook other) { CopyFrom(other); }

        /// <summary>The part id on a slot; "" = nothing worn there (or a slot outside the range).</summary>
        public string this[CharacterSlot slot]
        {
            get => Get(slot);
            set => Set(slot, value);
        }

        /// <summary>The part id on a slot; "" = nothing worn there (or a slot outside the range).</summary>
        public string Get(CharacterSlot slot)
        {
            int i = (int)slot;
            if (parts == null || i < 0 || i >= parts.Length) return "";
            return parts[i] ?? "";
        }

        /// <summary>Puts a part id on a slot (null or "" = nothing). A slot outside the range is ignored.</summary>
        public void Set(CharacterSlot slot, string id)
        {
            if (!CharacterSlots.IsValid(slot)) return;
            Grow();
            parts[(int)slot] = id != null ? id.Trim() : "";
        }

        /// <summary>True when nothing is worn on any slot.</summary>
        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < CharacterSlots.Count; i++) if (Get((CharacterSlot)i).Length > 0) return false;
                return true;
            }
        }

        /// <summary>Takes everything off.</summary>
        public void Clear()
        {
            Grow();
            for (int i = 0; i < parts.Length; i++) parts[i] = "";
        }

        /// <summary>Wears exactly what another look wears (nothing when it is null).</summary>
        public void CopyFrom(CharacterLook other)
        {
            for (int i = 0; i < CharacterSlots.Count; i++) Set((CharacterSlot)i, other != null ? other.Get((CharacterSlot)i) : "");
        }

        /// <summary>True when both looks wear the same id on every slot (a null look wears nothing).</summary>
        public bool SameAs(CharacterLook other)
        {
            for (int i = 0; i < CharacterSlots.Count; i++)
                if (!string.Equals(Get((CharacterSlot)i), other != null ? other.Get((CharacterSlot)i) : "", StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>The look as one line of text (see the class notes); "" when nothing is worn.</summary>
        public string ToText()
        {
            Text.Clear();
            for (int i = 0; i < CharacterSlots.Count; i++)
            {
                string id = Get((CharacterSlot)i);
                if (id.Length == 0) continue;
                if (Text.Length > 0) Text.Append(EntrySeparator);
                Text.Append(CharacterSlots.TextName((CharacterSlot)i)).Append(NameSeparator).Append(id);
            }
            return Text.ToString();
        }

        public override string ToString() => ToText();

        /// <summary>Reads a look's text (see the class notes). Spaces round names are ignored, a slot named twice keeps
        /// the last, "Slot=" means nothing on that slot, and a slot may also be named as its rig object (Full_Body).
        /// Whether the ids are parts the game has is NOT checked here: CharacterBody skips the ones it does not know.
        /// False (and <paramref name="look"/> empty) when an entry has no '=' or names no slot. Null or "" reads as a
        /// look with nothing worn.</summary>
        public static bool TryParse(string text, out CharacterLook look)
        {
            look = new CharacterLook();
            if (string.IsNullOrEmpty(text)) return true;
            string[] entries = text.Split(EntrySeparator);
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                if (entry.Length == 0) continue;
                int at = entry.IndexOf(NameSeparator);
                if (at <= 0 || !CharacterSlots.TryParse(entry.Substring(0, at).Trim(), out CharacterSlot slot))
                {
                    look.Clear();
                    return false;
                }
                look.Set(slot, entry.Substring(at + 1));
            }
            return true;
        }

        /// <summary>A look made by chance from <paramref name="seed"/> (see the class notes for how it draws).
        /// <paramref name="rules"/> null = the default rules. <paramref name="colour"/> (a word like Blue; null or "" =
        /// none) sets the body, the top and the trousers to that colour where the parts list has it. Nothing is worn when
        /// <paramref name="catalog"/> is null.</summary>
        public static CharacterLook Roll(CharacterCatalog catalog, CharacterLookRules rules, int seed, string colour)
        {
            var look = new CharacterLook();
            Roll(catalog, rules, seed, colour, look);
            return look;
        }

        /// <summary>The same roll, written into a look that already exists (everything on it is replaced), so nothing new
        /// is made. Does nothing when <paramref name="into"/> is null.</summary>
        public static void Roll(CharacterCatalog catalog, CharacterLookRules rules, int seed, string colour, CharacterLook into)
        {
            if (into == null) return;
            into.Clear();
            if (catalog == null) return;
            if (rules == null) rules = CharacterLookRules.Default;
            string word = colour != null ? colour.Trim() : "";
            float hairTop = 0f;
            for (int s = 0; s < CharacterSlots.Count; s++)
            {
                var slot = (CharacterSlot)s;
                var dice = new Dice(seed, slot);
                uint wornDraw = dice.Next();   // draw 1: is this slot worn
                bool worn;
                if (slot == CharacterSlot.Hat)
                {
                    if (hairTop <= 0f) worn = Pass(wornDraw, rules.hatWhenNoHair);
                    else worn = hairTop <= rules.tallestHairUnderHat && Pass(wornDraw, rules.hatOverHair);
                }
                else worn = Pass(wornDraw, rules.ChanceOf(slot));
                if (!worn) continue;
                CharacterCatalog.Part part = Pick(catalog, rules, slot, word, ref dice);   // draw 2: which part
                if (part == null) continue;
                into.Set(slot, part.id);
                if (slot == CharacterSlot.Hair) hairTop = Mathf.Max(part.top, float.Epsilon);
            }
            if (into.Get(CharacterSlot.FullBody).Length > 0)
                for (int s = 0; s < CharacterSlots.Count; s++)
                    if (s != (int)CharacterSlot.Body && s != (int)CharacterSlot.FullBody) into.Set((CharacterSlot)s, "");
        }

        /// <summary>True for the slots a colour word sets.</summary>
        private static bool Coloured(CharacterSlot slot) => slot == CharacterSlot.Body || slot == CharacterSlot.Outerwear || slot == CharacterSlot.Pants;

        /// <summary>One part for the slot, by the dice's next number, from the slot's parts narrowed as the class notes
        /// say. Null when the slot has no parts.</summary>
        private static CharacterCatalog.Part Pick(CharacterCatalog catalog, CharacterLookRules rules, CharacterSlot slot, string word, ref Dice dice)
        {
            IReadOnlyList<CharacterCatalog.Part> all = catalog.PartsOf(slot);
            if (all.Count == 0) return null;
            int limit = rules.HeaviestFor(slot);
            Pool.Clear();
            for (int i = 0; i < all.Count; i++) if (limit <= 0 || all[i].triangles <= limit) Pool.Add(all[i]);
            if (Pool.Count == 0) for (int i = 0; i < all.Count; i++) Pool.Add(all[i]);

            if (word.Length > 0 && Coloured(slot))
            {
                Narrow.Clear();
                string[] bodies = slot == CharacterSlot.Body ? rules.BodiesFor(word) : null;
                if (bodies != null)
                    for (int i = 0; i < Pool.Count; i++) if (Listed(bodies, Pool[i].id)) Narrow.Add(Pool[i]);
                if (Narrow.Count == 0)
                    for (int i = 0; i < Pool.Count; i++) if (string.Equals(Pool[i].colour, word, StringComparison.OrdinalIgnoreCase)) Narrow.Add(Pool[i]);
                if (Narrow.Count > 0)
                {
                    Pool.Clear();
                    Pool.AddRange(Narrow);
                }
                Narrow.Clear();
            }
            CharacterCatalog.Part picked = Pool[dice.Below(Pool.Count)];
            Pool.Clear();
            return picked;
        }

        private static bool Listed(string[] ids, string id)
        {
            for (int i = 0; i < ids.Length; i++) if (ids[i] != null && string.Equals(ids[i].Trim(), id, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>True when the draw falls under the chance (0..1), counted in whole steps so every PC agrees.</summary>
        private static bool Pass(uint draw, float chance)
        {
            int threshold = (int)Math.Round(Mathf.Clamp01(chance) * (double)ChanceSteps);
            return (int)(((ulong)draw * ChanceSteps) >> 32) < threshold;
        }

        private static string[] NewParts()
        {
            var fresh = new string[CharacterSlots.Count];
            for (int i = 0; i < fresh.Length; i++) fresh[i] = "";
            return fresh;
        }

        /// <summary>Makes the id list twelve long. A list read back from an old save (or emptied by a script reload)
        /// can be shorter.</summary>
        private void Grow()
        {
            if (parts != null && parts.Length >= CharacterSlots.Count) return;
            string[] fresh = NewParts();
            if (parts != null) for (int i = 0; i < parts.Length; i++) fresh[i] = parts[i] ?? "";
            parts = fresh;
        }

        /// <summary>The roll's own number maker: a 32-bit counter run through a fixed mixing function (Wellons'
        /// lowbias32). The same seed and slot give the same numbers on every PC, in every run and every Unity version.</summary>
        private struct Dice
        {
            private const uint Step = 0x9E3779B9u;
            private uint _state;

            public Dice(int seed, CharacterSlot slot)
            {
                unchecked { _state = Mix((uint)seed ^ Mix((uint)slot + Step)); }
            }

            /// <summary>The next number, any of the 2^32.</summary>
            public uint Next()
            {
                unchecked
                {
                    _state += Step;
                    return Mix(_state);
                }
            }

            /// <summary>The next number as a whole number from 0 to <paramref name="count"/> - 1 (0 when count is 1 or
            /// less; a number is drawn either way).</summary>
            public int Below(int count)
            {
                uint draw = Next();
                return count <= 1 ? 0 : (int)(((ulong)draw * (ulong)count) >> 32);
            }

            private static uint Mix(uint x)
            {
                unchecked
                {
                    x ^= x >> 16;
                    x *= 0x7FEB352Du;
                    x ^= x >> 15;
                    x *= 0x846CA68Bu;
                    x ^= x >> 16;
                    return x;
                }
            }
        }
    }
}
