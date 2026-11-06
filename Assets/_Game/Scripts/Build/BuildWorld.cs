using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// What Build Mode can do to the scene: spawn from the catalogue, make placement ghosts, copy, and turn the current
    /// arrangement into a layout file and back. It holds no input and no UI.
    /// </summary>
    public sealed class BuildWorld
    {
        private static readonly Color DefaultShapeColor = new Color(0.86f, 0.84f, 0.8f);

        private readonly Transform _placedRoot;
        private readonly Transform _player;

        public BuildCatalog Catalog { get; }

        public BuildWorld(BuildCatalog catalog, Transform player)
        {
            Catalog = catalog;
            _player = player;
            _placedRoot = new GameObject("BuildPlaced").transform;
        }

        public bool IsPlayer(Transform t) => _player != null && t.IsChildOf(_player);
        public Transform Player => _player;

        private static readonly RaycastHit[] Hits = new RaycastHit[64];

        /// <summary>
        /// The nearest solid surface under a screen point, skipping the player and anything in <paramref name="ignore"/>
        /// (and their children). With nothing under the mouse, the flat plane at <paramref name="fallbackY"/> is used.
        /// </summary>
        public bool Surface(Camera cam, Vector2 screen, HashSet<Transform> ignore, float fallbackY, out Vector3 point, out Vector3 normal)
        {
            Ray ray = cam.ScreenPointToRay(screen);
            int n = Physics.RaycastNonAlloc(ray, Hits, cam.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; point = default; normal = Vector3.up;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.distance >= best || IsPlayer(h.transform) || Ignored(h.transform, ignore)) continue;
                best = h.distance; point = h.point; normal = h.normal; found = true;
            }
            if (found) return true;
            var plane = new Plane(Vector3.up, new Vector3(0f, fallbackY, 0f));
            if (plane.Raycast(ray, out float enter)) { point = ray.GetPoint(enter); normal = Vector3.up; return true; }
            return false;
        }

        /// <summary>The buildable under a screen point: the nearest solid hit (not the player), if it is buildable and shown.</summary>
        public Buildable Pick(Camera cam, Vector2 screen)
        {
            Ray ray = cam.ScreenPointToRay(screen);
            int n = Physics.RaycastNonAlloc(ray, Hits, cam.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Transform nearest = null;
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].distance >= best || IsPlayer(Hits[i].transform)) continue;
                best = Hits[i].distance; nearest = Hits[i].transform;
            }
            if (nearest == null) return null;
            var b = nearest.GetComponentInParent<Buildable>();
            return b != null && b.isActiveAndEnabled ? b : null;
        }

        private static bool Ignored(Transform t, HashSet<Transform> ignore)
        {
            if (ignore == null || ignore.Count == 0) return false;
            for (var at = t; at != null; at = at.parent) if (ignore.Contains(at)) return true;
            return false;
        }

        // ------------------------------------------------------------------ spawning

        /// <summary>Puts a real copy of the entry in the world, marked buildable. Physics and gameplay scripts run as usual.</summary>
        public Buildable Spawn(BuildCatalog.Entry e, Vector3 position, Quaternion rotation)
        {
            GameObject go;
            if (e.IsShape) go = MakeShape(e.shape, e.shapeSize, e.shapeSteps, DefaultShapeColor, Catalog != null ? Catalog.ShapeMaterial : null, true);
            else
            {
                go = Object.Instantiate(e.prefab, position, rotation, _placedRoot);
                go.transform.localScale = e.prefab.transform.localScale * (e.defaultScale > 0f ? e.defaultScale : 1f);
            }
            go.name = e.displayName;
            go.transform.SetParent(_placedRoot, true);
            go.transform.SetPositionAndRotation(position, rotation);
            if (!go.TryGetComponent(out Buildable b)) b = go.AddComponent<Buildable>();
            b.InitPlaced(e.id);
            return b;
        }

        /// <summary>A copy of a buildable, beside the original (same parent, so its scale means the same).</summary>
        public Buildable Duplicate(Buildable source)
        {
            var parent = source.transform.parent;
            var go = Object.Instantiate(source.gameObject, source.transform.position, source.transform.rotation, parent);
            go.name = source.name.Replace(" (copy)", "") + " (copy)";
            go.transform.localScale = source.transform.localScale;
            var b = go.GetComponent<Buildable>();
            b.InitCopyOf(source);
            return b;
        }

        /// <summary>The placement ghost: the entry's look with nothing alive in it (no scripts, colliders or bodies).</summary>
        public GameObject MakeGhost(BuildCatalog.Entry e)
        {
            Material ghostMat = Catalog != null ? Catalog.GhostMaterial : null;
            GameObject ghost;
            if (e.IsShape) { ghost = MakeShape(e.shape, e.shapeSize, e.shapeSteps, DefaultShapeColor, ghostMat, false); ghost.transform.SetParent(null, true); }
            else
            {
                // Spawned under a switched-off holder so no Awake or OnEnable ever runs, then stripped and let out.
                var holder = new GameObject("GhostHolder");
                holder.SetActive(false);
                ghost = Object.Instantiate(e.prefab, holder.transform);
                ghost.transform.localScale = e.prefab.transform.localScale * (e.defaultScale > 0f ? e.defaultScale : 1f);
                Strip(ghost);
                ghost.transform.SetParent(null, true);
                Object.Destroy(holder);
            }
            ghost.name = "BuildGhost";
            if (ghostMat != null)
            {
                foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = ghostMat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            return ghost;
        }

        private GameObject MakeShape(ShapeKind kind, Vector3 size, int steps, Color color, Material material, bool solid)
        {
            var go = new GameObject("Shape_" + kind);
            go.SetActive(false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            if (material != null) r.sharedMaterial = material;
            if (solid) go.AddComponent<MeshCollider>();
            var shape = go.AddComponent<BuildShape>();
            shape.Set(kind, size, steps, color);
            go.transform.SetParent(_placedRoot, false);
            go.SetActive(true);
            return go;
        }

        /// <summary>Removes everything but the drawing: scripts first (they may need the rest), then bodies and colliders.</summary>
        private static void Strip(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.TryGetComponent(out ParticleSystem ps)) Object.DestroyImmediate(ps);
                var comps = t.GetComponents<Component>();
                for (int i = comps.Length - 1; i >= 0; i--)
                {
                    var c = comps[i];
                    if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is LODGroup) continue;
                    if (c is MonoBehaviour || c is Joint) Object.DestroyImmediate(c);
                }
                comps = t.GetComponents<Component>();
                for (int i = comps.Length - 1; i >= 0; i--)
                {
                    var c = comps[i];
                    if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is LODGroup) continue;
                    Object.DestroyImmediate(c);
                }
            }
        }

        // ------------------------------------------------------------------ layouts

        /// <summary>Destroys everything placed or copied and puts scene props back as the scene started.</summary>
        public void Reset()
        {
            var all = new List<Buildable>(Buildable.All);
            foreach (var b in all)
            {
                if (b == null) continue;
                if (b.IsSceneOriginal) b.Apply(b.Original);
                else Object.Destroy(b.gameObject);
            }
        }

        public LayoutFile Capture()
        {
            var file = new LayoutFile { scene = SceneManager.GetActiveScene().name, saved = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            foreach (var b in Buildable.All)
            {
                if (b == null) continue;
                var s = b.Capture();
                if (b.IsSceneOriginal)
                {
                    if (string.IsNullOrEmpty(b.SceneKey) || s.SameAs(b.Original)) continue;   // untouched props are not written
                    file.items.Add(LayoutItem.From(s, null, null, b.SceneKey));
                }
                else if (s.active) file.items.Add(LayoutItem.From(s, b.CatalogId, b.CloneOf, null));
            }
            return file;
        }

        /// <summary>Resets, then rebuilds the file's arrangement. Returns how many items could not be found.</summary>
        public int Apply(LayoutFile file)
        {
            Reset();
            int missing = 0;
            var scene = SceneManager.GetActiveScene();
            var byKey = new Dictionary<string, Buildable>();
            foreach (var b in Buildable.All) if (b != null && b.IsSceneOriginal && !string.IsNullOrEmpty(b.SceneKey)) byKey[b.SceneKey] = b;

            foreach (var item in file.items)
            {
                Buildable made = null;
                if (!string.IsNullOrEmpty(item.sceneKey))
                {
                    if (byKey.TryGetValue(item.sceneKey, out var prop)) prop.Apply(item.ToState());
                    else missing++;
                    continue;
                }
                if (!string.IsNullOrEmpty(item.cloneOf))
                {
                    if (byKey.TryGetValue(item.cloneOf, out var source)) made = Duplicate(source);
                    else if (ScenePath.Find(scene, item.cloneOf) is Transform found && found.TryGetComponent(out Buildable fb)) made = Duplicate(fb);
                }
                else
                {
                    var entry = Catalog != null ? Catalog.Find(item.id) : null;
                    if (entry != null) made = Spawn(entry, item.position, item.rotation);
                }
                if (made == null) { missing++; continue; }
                made.Apply(item.ToState());
            }
            Physics.SyncTransforms();
            return missing;
        }
    }
}
