using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The game's own list of every Funny Characters part (D-0183): each part once, with the slot it goes on, its mesh
    /// and materials, the colour or kind word in its file name, how heavy it is and how high it reaches. WHY the game
    /// keeps its own list: the pack is not in the public repo and its own tool exists only inside the editor, so a built
    /// game cannot look through the pack's folders. Shift Jam > Characters > Build Characters writes this asset
    /// (Resources/CharacterCatalog) from the pack; it references the pack's meshes and copies nothing. Never edit it by
    /// hand: the next build writes it again.
    /// A part's id is its file name, which never changes: a saved look names its parts by id.
    /// </summary>
    public sealed class CharacterCatalog : ScriptableObject
    {
        /// <summary>One part: one mesh that goes on one slot of the rig.</summary>
        [Serializable]
        public sealed class Part
        {
            [Tooltip("The part's file name without its ending (Body_Blue_001). A saved look names the part by this.")]
            public string id = "";
            [Tooltip("The slot of the rig it goes on.")]
            public CharacterSlot slot;
            [Tooltip("The pack's mesh, put on the slot's renderer as it is (every part is skinned to the same bones).")]
            public Mesh mesh;
            [Tooltip("The materials on the part's own renderer in its file, in its order (most parts: the one shared Material; bodies add Eye).")]
            public Material[] materials = new Material[0];
            [Tooltip("The colour word in the file name (Blue, Light_Blue); empty when the name has none.")]
            public string colour = "";
            [Tooltip("The kind word in the file name (shoes only: Boot, Skate, Slipper, Sneaker); empty when the name has none.")]
            public string kind = "";
            [Tooltip("How many triangles the mesh has: how heavy it is to draw.")]
            public int triangles;
            [Tooltip("How high the mesh reaches above the soles, standing at scale 1 (the top of its box).")]
            public float top;
        }

        [Tooltip("Every part of the pack: the slots in their order, each slot's files in name order. Written by Shift Jam > Characters > Build Characters.")]
        public List<Part> parts = new List<Part>();

        private static CharacterCatalog _loaded;
        private static bool _looked;
        private static readonly List<Part> NoParts = new List<Part>();

        // Looked up by id and by slot. Built the first time one is asked for and never saved with the asset, so the list
        // above stays the only truth.
        [NonSerialized] private Dictionary<string, Part> _byId;
        [NonSerialized] private List<Part>[] _bySlot;
        [NonSerialized] private int _usable;

        /// <summary>Resources/CharacterCatalog, or null when the asset is missing (a body then keeps what it was saved
        /// wearing). Loaded once while the game runs; in the editor outside Play a missing one is looked for again, so a
        /// build made a moment ago is found.</summary>
        public static CharacterCatalog Load()
        {
            if (!_looked || (_loaded == null && !Application.isPlaying))
            {
                _looked = true;
                _loaded = Resources.Load<CharacterCatalog>("CharacterCatalog");
            }
            return _loaded;
        }

        /// <summary>True when at least one part has its mesh (false in a copy of the game without the pack: every mesh
        /// reference is then empty).</summary>
        public bool Ready
        {
            get { Index(); return _usable > 0; }
        }

        /// <summary>The part with this id (exact, case counts); null when unknown or empty.</summary>
        public Part PartById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Index();
            return _byId.TryGetValue(id, out Part part) ? part : null;
        }

        /// <summary>The parts of one slot that have their mesh, in the list's order (the order the look roll picks
        /// from). Empty for a slot with none. Do not change the list.</summary>
        public IReadOnlyList<Part> PartsOf(CharacterSlot slot)
        {
            if (!CharacterSlots.IsValid(slot)) return NoParts;
            Index();
            return _bySlot[(int)slot];
        }

        /// <summary>Drops the lookup tables, so the next question reads the list again. The build calls it after
        /// rewriting the list, and so does any change made in the Inspector.</summary>
        public void Forget()
        {
            _byId = null;
            _bySlot = null;
            _usable = 0;
        }

        private void OnValidate() { Forget(); }

        /// <summary>Builds the lookup tables once. A row with an empty id is left out; of two rows with the same id the
        /// first counts. A part without its mesh can still be found by id but is never offered to the roll.</summary>
        private void Index()
        {
            if (_byId != null && _bySlot != null && _bySlot.Length == CharacterSlots.Count) return;
            _byId = new Dictionary<string, Part>(StringComparer.Ordinal);
            _bySlot = new List<Part>[CharacterSlots.Count];
            for (int i = 0; i < _bySlot.Length; i++) _bySlot[i] = new List<Part>();
            _usable = 0;
            if (parts == null) return;
            for (int i = 0; i < parts.Count; i++)
            {
                Part part = parts[i];
                if (part == null || string.IsNullOrEmpty(part.id) || _byId.ContainsKey(part.id)) continue;
                _byId.Add(part.id, part);
                if (part.mesh == null || !CharacterSlots.IsValid(part.slot)) continue;
                _bySlot[(int)part.slot].Add(part);
                _usable++;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _loaded = null; _looked = false; }
    }
}
