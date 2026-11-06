using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>The tabs of the build catalogue. Only tabs with entries are shown.</summary>
    public enum BuildCategory { Furniture, Toys, Clutter, Shapes, Traversal, Vehicles, Weapons, Interactables }

    /// <summary>The generated shape pieces. None = the entry is a prefab.</summary>
    public enum ShapeKind { None, Box, Ramp, Stairs, Pillar, Wall }

    /// <summary>
    /// Everything Build Mode can place: an id, a name, a tab, and either a prefab or a shape. Written by the editor menu
    /// Shift Jam / Workshop / Build Catalog into Resources/BuildCatalog, read at run time. Layout files store the id, so
    /// keep ids stable (the builder makes them from the prefab path).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Build Catalog", fileName = "BuildCatalog")]
    public sealed class BuildCatalog : ScriptableObject
    {
        public const string ResourcePath = "BuildCatalog";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable key saved in layout files.")] public string id;
            [Tooltip("Name under the thumbnail.")] public string displayName;
            public BuildCategory category;
            [Tooltip("What gets spawned. Empty for shapes.")] public GameObject prefab;
            [Tooltip("Picture in the catalogue. Optional: the name shows without it.")] public Texture2D thumbnail;
            [Tooltip("Multiplies the prefab's own scale when placed.")] public float defaultScale = 1f;
            [Tooltip("A generated shape instead of a prefab.")] public ShapeKind shape = ShapeKind.None;
            [Tooltip("Shape size: width, height, depth, in world units.")] public Vector3 shapeSize = new Vector3(10f, 2f, 10f);
            [Tooltip("Stairs only: how many steps.")] public int shapeSteps = 6;

            public bool IsShape => shape != ShapeKind.None;
        }

        [SerializeField, Tooltip("Every placeable thing, in catalogue order.")] private List<Entry> entries = new List<Entry>();
        [SerializeField, Tooltip("Plain lit material the shape pieces use; tinted per piece.")] private Material shapeMaterial;
        [SerializeField, Tooltip("See-through unlit material for the placement ghost and the selection boxes.")] private Material ghostMaterial;

        private Dictionary<string, Entry> _byId;

        public IReadOnlyList<Entry> Entries => entries;
        public Material ShapeMaterial => shapeMaterial;
        public Material GhostMaterial => ghostMaterial;

        public static BuildCatalog Load() => Resources.Load<BuildCatalog>(ResourcePath);

        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, Entry>();
                foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.id)) _byId[e.id] = e;
            }
            return _byId.TryGetValue(id, out var found) ? found : null;
        }

        private void OnValidate() { _byId = null; }

#if UNITY_EDITOR
        /// <summary>Editor builder access: the list itself and the two materials.</summary>
        public List<Entry> EditorEntries { get { _byId = null; return entries; } }
        public void EditorSetMaterials(Material shape, Material ghost) { shapeMaterial = shape; ghostMaterial = ghost; }
#endif
    }
}
