using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The game's own parts list for its guns (D-0167, the armory looks bench): which skins there are, which parts a
    /// gun may take, and where on the gun each one goes. WHY the game keeps its own list: the weapons pack has one,
    /// but the pack's scripts only exist inside the Unity editor and the pack is not in the public repo, so a built
    /// game cannot read it. Shift Jam > Armory > Bake Parts List copies what the game needs out of the pack into this
    /// asset (Resources/ArmoryCatalog), and the game reads only this. Game code never names a gun, a skin or a part:
    /// every name comes from here.
    /// The Fit and Stats numbers on a part are the home for working parts and full builds (what a part does to the
    /// aim, the recoil, the magazine). Nothing reads them yet: D-0167 is looks only.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Armory Catalog", fileName = "ArmoryCatalog")]
    public sealed class ArmoryCatalog : ScriptableObject
    {
        /// <summary>One colour scheme: every colour part of a gun is drawn with its material.</summary>
        [Serializable]
        public sealed class Skin
        {
            [Tooltip("The pack skin config's file name (ColorSet_01). A saved look names a skin by this.")]
            public string id;
            [Tooltip("The name shown on the bench (the pack's display name, like Ash).")]
            public string label;
            [Tooltip("The pack material every colour part of the gun is drawn with.")]
            public Material material;
        }

        /// <summary>One thing a part hangs on the gun, and the mount it goes on.</summary>
        [Serializable]
        public sealed class Piece
        {
            [Tooltip("The pack's slot name. The piece goes on the gun's child whose name starts with Mount Prefix and ends with this.")]
            public string slot;
            [Tooltip("The pack prefab that is hung there.")]
            public GameObject prefab;
        }

        /// <summary>What a part changes about how the gun is held and aimed. For working parts; all zero = no change.</summary>
        [Serializable]
        public struct PartFit
        {
            [Tooltip("Not read yet (looks only, D-0167). Degrees the gun rolls over as the sight comes to the eye with this part on. 0 = no change.")]
            public float sightRoll;
            [Tooltip("Not read yet (looks only, D-0167). Field of view while aimed with this part on, degrees. 0 = no change.")]
            public float sightFieldOfView;
            [Tooltip("Not read yet (looks only, D-0167). How far in front of the eye the sight sits with this part on. 0 = no change.")]
            public float sightDistance;
            [Tooltip("Not read yet (looks only, D-0167). How much longer the barrel is with this part on, units at toy size. 0 = no change.")]
            public float muzzleLength;
            [Tooltip("Not read yet (looks only, D-0167). Where the support hand holds with this part on, in the gun's own axes. Zero = no change.")]
            public Vector3 holdPoint;
        }

        /// <summary>What a part changes about how the gun shoots. For full builds; all zero = no change.</summary>
        [Serializable]
        public struct PartStats
        {
            [Tooltip("Not read yet (looks only, D-0167). Recoil with this part on, as percent more (below 0: less). 0 = no change.")]
            public float recoilPercent;
            [Tooltip("Not read yet (looks only, D-0167). Spread with this part on, as percent more (below 0: less). 0 = no change.")]
            public float spreadPercent;
            [Tooltip("Not read yet (looks only, D-0167). Reload time with this part on, as percent more (below 0: less). 0 = no change.")]
            public float reloadPercent;
            [Tooltip("Not read yet (looks only, D-0167). Walking speed with this part on, as percent more (below 0: less). 0 = no change.")]
            public float walkPercent;
            [Tooltip("Not read yet (looks only, D-0167). Rounds this part adds to the magazine (below 0: takes away). 0 = no change.")]
            public int magazineRounds;
        }

        /// <summary>One part a gun can take: what is hung where, and (later) what it changes.</summary>
        [Serializable]
        public sealed class Part
        {
            [Tooltip("The pack attachment config's file name. A saved look names a part by this.")]
            public string id;
            [Tooltip("The name shown on the bench. Change it here and the next bake keeps it.")]
            public string label;
            [Tooltip("The kind of part (the pack's attachment type name). It must be the id of one of the Types below.")]
            public string type;
            [Tooltip("What this part hangs on the gun, and on which mount.")]
            public Piece[] pieces = new Piece[0];
            [Tooltip("Not read yet (looks only, D-0167). What this part changes about the hold and the aim. The bake never overwrites it.")]
            public PartFit fit;
            [Tooltip("Not read yet (looks only, D-0167). What this part changes about the shooting. The bake never overwrites it.")]
            public PartStats stats;
        }

        /// <summary>One kind of part: one row on the bench.</summary>
        [Serializable]
        public sealed class PartType
        {
            [Tooltip("The pack's attachment type name. A saved look names the row by this.")]
            public string id;
            [Tooltip("The name of this row on the bench. Change it here and the next bake keeps it.")]
            public string label;
            [Tooltip("Optional. Hung on the mount first; the part then hangs from it (the pack hangs a charm from a hook and chain). Empty: the part goes straight on the mount.")]
            public GameObject hook;
            [Tooltip("The name of the child inside the hook that the part hangs from. Not found or empty: the hook itself.")]
            public string hookMount;
        }

        /// <summary>One gun the game uses, and what it may take.</summary>
        [Serializable]
        public sealed class Gun
        {
            [Tooltip("The pack gun's name. It is also the name of the model object nested inside the game's weapon prefab: that is how a gun in the game is matched to this row.")]
            public string id;
            [Tooltip("The pack's ready-made build of this gun: the copy that turns above the bench.")]
            public GameObject display;
            [Tooltip("The id of the skin the gun comes in. Choosing it again on the bench clears the saved choice.")]
            public string skin;
            [Tooltip("The ids of the parts this gun may take, in the pack's order.")]
            public string[] parts = new string[0];
        }

        [Tooltip("What the name of every mount point on a pack gun starts with. A part goes on the child whose name starts with this and ends with the part's slot (the pack's own rule; it also finds the shotgun's misspelt mounts).")]
        public string mountPrefix = "Place_";
        [Tooltip("A material whose name contains this is the gun's colour: a skin replaces it (the pack's own rule). Glass, lenses, reticles, metal and charms do not carry it and are left alone.")]
        public string skinMark = "ColorSet";
        [Tooltip("The name of the skin row on the bench.")]
        public string skinLabel = "Skin";
        [Tooltip("What a row shows when nothing of that kind is fitted.")]
        public string noneLabel = "None";
        [Tooltip("Every skin of the pack, in the pack's order. Written by Shift Jam > Armory > Bake Parts List.")]
        public List<Skin> skins = new List<Skin>();
        [Tooltip("The kinds of part the game can fit today, one bench row each. Written by the bake.")]
        public List<PartType> types = new List<PartType>();
        [Tooltip("Every part the game offers. Written by the bake; a label you change here is kept by the next bake.")]
        public List<Part> parts = new List<Part>();
        [Tooltip("The guns the game uses (found inside Prefabs/Weapons) and what each may take. Written by the bake.")]
        public List<Gun> guns = new List<Gun>();

        private static ArmoryCatalog _loaded;
        private static bool _looked;
        private static readonly List<Transform> Found = new List<Transform>();

        // Looked up by id. Built the first time one is asked for and never saved with the asset, so the lists above
        // stay the only truth (D-0167).
        [NonSerialized] private Dictionary<string, Gun> _gunById;
        [NonSerialized] private Dictionary<string, int> _skinAt;
        [NonSerialized] private Dictionary<string, Part> _partById;

        /// <summary>Resources/ArmoryCatalog, or null when the asset is missing (guns then stay as they come and the
        /// bench says it has no parts list). Loaded once, as SightLook is.</summary>
        public static ArmoryCatalog Load()
        {
            if (!_looked) { _looked = true; _loaded = Resources.Load<ArmoryCatalog>("ArmoryCatalog"); }
            return _loaded;
        }

        /// <summary>The row of the gun whose model sits under <paramref name="root"/> (the root itself counts), or
        /// null when it holds no gun this list knows. A gun is known by the NAME of its model object, because the
        /// game's weapon prefab nests the pack's model under that name: nothing has to be set per prefab.</summary>
        public Gun GunOf(Transform root)
        {
            if (root == null) return null;
            Index();
            Gun gun = null;
            root.GetComponentsInChildren(true, Found);
            for (int i = 0; i < Found.Count && gun == null; i++) _gunById.TryGetValue(Found[i].name, out gun);
            Found.Clear();
            return gun;
        }

        /// <summary>The skin with this id; null when unknown or empty.</summary>
        public Skin SkinById(string id)
        {
            int at = SkinIndex(id);
            return at >= 0 ? skins[at] : null;
        }

        /// <summary>Where the skin with this id is in the list; -1 when unknown or empty.</summary>
        public int SkinIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            Index();
            return _skinAt.TryGetValue(id, out int at) && at < skins.Count ? at : -1;
        }

        /// <summary>The part with this id; null when unknown or empty.</summary>
        public Part PartById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Index();
            return _partById.TryGetValue(id, out Part part) ? part : null;
        }

        /// <summary>True for a material that is a gun's colour (its name holds the skin mark): the one kind of
        /// material a skin may replace. An empty mark matches nothing, so a blanked field can never paint a lens.</summary>
        public bool IsSkin(Material m)
        {
            return m != null && !string.IsNullOrEmpty(skinMark) && m.name.IndexOf(skinMark, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Drops the lookup tables, so the next question reads the lists again. The bake calls it after
        /// rewriting them, and so does any change made in the Inspector.</summary>
        public void Forget()
        {
            _gunById = null;
            _skinAt = null;
            _partById = null;
        }

        private void OnValidate() { Forget(); }

        /// <summary>Builds the lookup tables once. A row with an empty id is left out (it could never be named by a
        /// saved look); of two rows with the same id the first one counts.</summary>
        private void Index()
        {
            if (_gunById != null) return;
            _gunById = new Dictionary<string, Gun>();
            _skinAt = new Dictionary<string, int>();
            _partById = new Dictionary<string, Part>();
            if (guns != null)
                for (int i = 0; i < guns.Count; i++)
                    if (guns[i] != null && !string.IsNullOrEmpty(guns[i].id) && !_gunById.ContainsKey(guns[i].id)) _gunById.Add(guns[i].id, guns[i]);
            if (skins != null)
                for (int i = 0; i < skins.Count; i++)
                    if (skins[i] != null && !string.IsNullOrEmpty(skins[i].id) && !_skinAt.ContainsKey(skins[i].id)) _skinAt.Add(skins[i].id, i);
            if (parts != null)
                for (int i = 0; i < parts.Count; i++)
                    if (parts[i] != null && !string.IsNullOrEmpty(parts[i].id) && !_partById.ContainsKey(parts[i].id)) _partById.Add(parts[i].id, parts[i]);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _loaded = null; _looked = false; }
    }
}
