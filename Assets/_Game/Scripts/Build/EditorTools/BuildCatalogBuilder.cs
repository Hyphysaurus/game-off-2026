#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Workshop / Build Catalog: scans the Playroom pack and Assets/_Game/Prefabs/Pieces/&lt;Category&gt;,
    /// renders a thumbnail per prefab (Unity's asset preview, saved as PNG in Art/Thumbs), adds the shape pieces, makes
    /// the two materials Build Mode needs, and writes Resources/BuildCatalog. Run it again any time: existing thumbnails
    /// are kept (Rebuild Thumbnails redraws them), hand-edited names and scales are kept, vanished prefabs drop out.
    /// Editor only; nothing of this ships.
    /// </summary>
    public static class BuildCatalogBuilder
    {
        private const string Pack = "Assets/_Imports/ithappy/Playroom/Prefabs";
        private const string Pieces = "Assets/_Game/Prefabs/Pieces";
        private const string ThumbDir = "Assets/_Game/Art/Thumbs";
        private const string CatalogPath = "Assets/_Game/Resources/BuildCatalog.asset";
        private const string ShapeMatPath = "Assets/_Game/Data/BuildShape.mat";
        private const string GhostMatPath = "Assets/_Game/Data/BuildGhost.mat";
        private const int ThumbSize = 128, InFlight = 32;
        private const double ThumbTimeout = 20.0;

        // Pack folders and the tab they go in. Characters/Faces are expression sprites, not props.
        private static readonly (string folder, BuildCategory category)[] PackFolders =
        {
            ("Furniture", BuildCategory.Furniture), ("Decor", BuildCategory.Furniture), ("Boxes", BuildCategory.Furniture), ("Room_Modules", BuildCategory.Furniture),
            ("Toys", BuildCategory.Toys), ("Wooden_Toys", BuildCategory.Toys), ("Army", BuildCategory.Toys), ("Characters", BuildCategory.Toys),
            ("Paper", BuildCategory.Clutter), ("Stationery", BuildCategory.Clutter), ("Other", BuildCategory.Clutter),
        };

        // Loose parts of bigger props that have no whole prefab of their own name to hang off (see IsPart).
        private static readonly string[] PartWords =
        {
            "/Faces/", "Keyboard_Key_", "Safe_Button_", "Safe_Door_", "Safe_Handle_", "Office_Chair_", "Night_Lamp_Button_", "Night_Lamp_Tape_", "Night_Lamp_Sphere_",
        };

        private static readonly BuildCategory[] PieceCategories = { BuildCategory.Traversal, BuildCategory.Vehicles, BuildCategory.Weapons, BuildCategory.Interactables };

        private sealed class Found
        {
            public string id, name, path;
            public BuildCategory category;
            public GameObject prefab;
            public string Png => ThumbDir + "/" + id.Replace('/', '_') + ".png";
        }

        [MenuItem("Shift Jam/Workshop/Build Catalog")]
        public static void BuildFromMenu() => Run(false);

        [MenuItem("Shift Jam/Workshop/Rebuild Thumbnails")]
        public static void RebuildThumbnails() => Run(true);

        [MenuItem("Shift Jam/Workshop/Open Layouts Folder")]
        public static void OpenLayouts()
        {
            Directory.CreateDirectory(BuildLayouts.Folder);
            EditorUtility.RevealInFinder(BuildLayouts.Folder);
        }

        private static void Run(bool redrawThumbs)
        {
            EnsureFolder(ThumbDir); EnsureFolder("Assets/_Game/Resources"); EnsureFolder("Assets/_Game/Data");
            foreach (var c in PieceCategories) EnsureFolder(Pieces + "/" + c);
            Material shapeMat = ShapeMaterial(), ghostMat = GhostMaterial();

            var found = Scan();
            var shapes = ShapeEntries();
            foreach (var s in shapes)
            {
                string png = ThumbDir + "/" + s.id.Replace('/', '_') + ".png";
                if (redrawThumbs || !File.Exists(png)) WritePng(png, RenderShape(s, shapeMat));
            }
            var jobs = found.Where(f => redrawThumbs || !File.Exists(f.Png)).ToList();
            if (jobs.Count == 0) { Finish(found, shapes, shapeMat, ghostMat); return; }
            new ThumbBatch(jobs, () => Finish(found, shapes, shapeMat, ghostMat)).Start();
        }

        // ------------------------------------------------------------------ scanning

        private static List<Found> Scan()
        {
            var list = new List<Found>();
            foreach (var (folder, category) in PackFolders)
            {
                string dir = Pack + "/" + folder;
                if (!AssetDatabase.IsValidFolder(dir)) { Debug.LogWarning("Build Catalog: missing pack folder " + dir); continue; }
                var paths = AssetDatabase.FindAssets("t:Prefab", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
                var names = new HashSet<string>(AssetDatabase.FindAssets("t:Prefab", new[] { Pack }).Select(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g))));
                foreach (var path in paths)
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (IsPart(path, name, names)) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null || prefab.GetComponentInChildren<Renderer>(true) == null) continue;
                    list.Add(new Found { id = "Playroom/" + path.Substring(Pack.Length + 1, path.Length - Pack.Length - 1 - ".prefab".Length), name = Pretty(name), path = path, category = category, prefab = prefab });
                }
            }
            foreach (var c in PieceCategories)
            {
                string dir = Pieces + "/" + c;
                foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    string rel = path.Substring(Pieces.Length + 1, path.Length - Pieces.Length - 1 - ".prefab".Length);
                    list.Add(new Found { id = "Pieces/" + rel, name = Pretty(Path.GetFileNameWithoutExtension(path)), path = path, category = c, prefab = prefab });
                }
            }
            return list;
        }

        /// <summary>A loose part: "Car_01_Wheel_FL" when "Car_01" exists, or one of the known part names.</summary>
        private static bool IsPart(string path, string name, HashSet<string> all)
        {
            foreach (var w in PartWords) if (path.Contains(w)) return true;
            int cut = name.LastIndexOf('_');
            while (cut > 0)
            {
                if (all.Contains(name.Substring(0, cut))) return true;
                cut = name.LastIndexOf('_', cut - 1);
            }
            return false;
        }

        /// <summary>"Bedside_Table_01" → "Bedside Table 1".</summary>
        private static string Pretty(string name)
        {
            var parts = name.Split('_').Where(p => p.Length > 0).Select(p => int.TryParse(p, out int n) ? n.ToString() : p);
            return string.Join(" ", parts);
        }

        private static List<BuildCatalog.Entry> ShapeEntries()
        {
            BuildCatalog.Entry S(string id, string name, ShapeKind k, Vector3 size, int steps = 6) =>
                new BuildCatalog.Entry { id = "Shape/" + id, displayName = name, category = BuildCategory.Shapes, shape = k, shapeSize = size, shapeSteps = steps, defaultScale = 1f };
            return new List<BuildCatalog.Entry>
            {
                S("Platform", "Platform", ShapeKind.Box, new Vector3(12f, 2f, 12f)),
                S("Block", "Block", ShapeKind.Box, new Vector3(6f, 6f, 6f)),
                S("Ramp", "Ramp", ShapeKind.Ramp, new Vector3(10f, 5f, 16f)),
                S("Stairs", "Stairs", ShapeKind.Stairs, new Vector3(10f, 6f, 12f), 8),
                S("Pillar", "Pillar", ShapeKind.Pillar, new Vector3(4f, 16f, 4f)),
                S("Wall", "Wall", ShapeKind.Wall, new Vector3(24f, 12f, 1f)),
            };
        }

        // ------------------------------------------------------------------ writing the catalogue

        private static void Finish(List<Found> found, List<BuildCatalog.Entry> shapes, Material shapeMat, Material ghostMat)
        {
            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<BuildCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BuildCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var list = catalog.EditorEntries;
            var old = new Dictionary<string, BuildCatalog.Entry>();
            foreach (var e in list) if (e != null && !string.IsNullOrEmpty(e.id)) old[e.id] = e;
            list.Clear();

            int thumbs = 0;
            foreach (var s in shapes)
            {
                Keep(s, old);
                s.thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(ThumbDir + "/" + s.id.Replace('/', '_') + ".png");
                if (s.thumbnail != null) thumbs++;
                list.Add(s);
            }
            foreach (var f in found)
            {
                var e = new BuildCatalog.Entry { id = f.id, displayName = f.name, category = f.category, prefab = f.prefab, defaultScale = 1f };
                Keep(e, old);
                e.thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(f.Png);
                if (e.thumbnail != null) thumbs++;
                list.Add(e);
            }
            catalog.EditorSetMaterials(shapeMat, ghostMat);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            var counts = string.Join(", ", list.GroupBy(e => e.category).Select(g => $"{g.Key} {g.Count()}"));
            Debug.Log($"Build Catalog: {list.Count} entries ({counts}), {thumbs} thumbnails → {CatalogPath}");
        }

        /// <summary>Hand edits survive a rebuild: the old name and scale are kept.</summary>
        private static void Keep(BuildCatalog.Entry e, Dictionary<string, BuildCatalog.Entry> old)
        {
            if (!old.TryGetValue(e.id, out var was)) return;
            if (!string.IsNullOrEmpty(was.displayName)) e.displayName = was.displayName;
            if (was.defaultScale > 0f) e.defaultScale = was.defaultScale;
            if (e.IsShape && was.IsShape) { e.shapeSize = was.shapeSize; e.shapeSteps = was.shapeSteps; }
        }

        // ------------------------------------------------------------------ materials

        private static Material ShapeMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(ShapeMatPath);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(m, ShapeMatPath);
            return m;
        }

        /// <summary>URP Unlit, transparent, both sides, no depth write, no shadows: for the ghost and the selection boxes.</summary>
        private static Material GhostMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);
            bool made = m == null;
            if (made) m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.45f));
            if (made) AssetDatabase.CreateAsset(m, GhostMatPath); else EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ thumbnails

        private static Texture2D RenderShape(BuildCatalog.Entry e, Material material)
        {
            var mesh = new Mesh();
            ShapeMesh.Fill(mesh, e.shape, e.shapeSize, e.shapeSteps);
            var pru = new PreviewRenderUtility();
            try
            {
                var cam = pru.camera;
                cam.fieldOfView = 30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1000f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f, 1f);
                Bounds b = mesh.bounds;
                Quaternion look = Quaternion.Euler(28f, -140f, 0f);
                float reach = b.extents.magnitude / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                cam.transform.SetPositionAndRotation(b.center - look * Vector3.forward * reach, look);
                pru.lights[0].intensity = 1.3f; pru.lights[0].transform.rotation = Quaternion.Euler(45f, -120f, 0f);
                pru.lights[1].intensity = 0.5f;
                pru.ambientColor = new Color(0.35f, 0.35f, 0.38f);
                pru.BeginStaticPreview(new Rect(0, 0, ThumbSize, ThumbSize));
                pru.DrawMesh(mesh, Matrix4x4.identity, material, 0);
                pru.Render(true, false);
                return pru.EndStaticPreview();
            }
            finally
            {
                pru.Cleanup();
                Object.DestroyImmediate(mesh);
            }
        }

        private static void WritePng(string assetPath, Texture source)
        {
            if (source == null) { Debug.LogWarning("Build Catalog: no picture for " + assetPath); return; }
            int w = source.width, h = source.height;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.GetFullPath(assetPath), copy.EncodeToPNG());
            Object.DestroyImmediate(copy);
        }

        /// <summary>
        /// Asks Unity for asset previews a few dozen at a time and saves each as it arrives. Previews load in the
        /// background, so this runs from the editor's update loop with a progress bar (cancel keeps what is done).
        /// </summary>
        private sealed class ThumbBatch
        {
            private readonly List<Found> _todo;
            private readonly System.Action _done;
            private readonly Dictionary<Found, double> _asked = new Dictionary<Found, double>();
            private int _next, _saved, _failed;

            public ThumbBatch(List<Found> todo, System.Action done) { _todo = todo; _done = done; }

            public void Start()
            {
                AssetPreview.SetPreviewTextureCacheSize(Mathf.Max(256, InFlight * 4));
                EditorApplication.update += Step;
            }

            private void Step()
            {
                double now = EditorApplication.timeSinceStartup;
                while (_asked.Count < InFlight && _next < _todo.Count)
                {
                    var f = _todo[_next++];
                    AssetPreview.GetAssetPreview(f.prefab);
                    _asked[f] = now;
                }
                foreach (var f in _asked.Keys.ToList())
                {
                    var tex = AssetPreview.GetAssetPreview(f.prefab);
                    if (tex != null) { WritePng(f.Png, tex); _saved++; _asked.Remove(f); }
                    else if (now - _asked[f] > ThumbTimeout) { Debug.LogWarning("Build Catalog: preview timed out for " + f.path); _failed++; _asked.Remove(f); }
                }
                int finished = _saved + _failed;
                bool cancel = EditorUtility.DisplayCancelableProgressBar("Build Catalog", $"Thumbnails {finished} / {_todo.Count}", _todo.Count == 0 ? 1f : finished / (float)_todo.Count);
                if (cancel || (_next >= _todo.Count && _asked.Count == 0))
                {
                    EditorApplication.update -= Step;
                    EditorUtility.ClearProgressBar();
                    Debug.Log($"Build Catalog: {_saved} thumbnails saved, {_failed} failed{(cancel ? " (cancelled)" : "")}.");
                    _done();
                }
            }
        }

        /// <summary>Thumbnails import as small UI pictures: no mip maps, clamped, alpha kept.</summary>
        public sealed class ThumbImport : AssetPostprocessor
        {
            private void OnPreprocessTexture()
            {
                if (!assetPath.StartsWith(ThumbDir + "/")) return;
                var ti = (TextureImporter)assetImporter;
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.maxTextureSize = ThumbSize;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
