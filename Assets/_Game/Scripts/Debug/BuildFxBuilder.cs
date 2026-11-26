#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Art = Artifice;

namespace ShiftJam
{
    /// <summary>
    /// "Land Build Effects (D-0230)": the maker of the Artificer build effects (TitleBuildIn: the title builds itself;
    /// RebuildOnReset: the toy rebuilds itself after a fall). Both cut their pieces at run time, so nothing is baked or
    /// saved from his licensed models: this makes Resources/BuildFx when it is missing (his tuning on an existing one is
    /// kept; only an empty Course Sounds is filled), then checks and counts, so a look at the report says whether the
    /// effects will have something to build:
    /// - the title: the menu model prefab's five letters (MenuTitle), each readable, and Artificer's cut of each one made
    ///   here the same way the title makes it at run time, piece count against pixel count (one piece a pixel = right);
    /// - the toy: the Player prefab's bodies (each Animator), their skinned parts, and the piece counts of each way of
    ///   cutting (parts, texture patches, facets), from the pose the prefab stands in.
    /// Nothing else is written: no scene is touched (Respawn adds RebuildOnReset to the Player at run time), no prefab
    /// is saved, no Artificer build data is made. A second run makes nothing new and reports the same.
    /// Menu: Shift Jam > FX > Land Build Effects (D-0230). Batch or a check: ShiftJam.BuildFxBuilder.Land() returns the report.
    /// </summary>
    public static class BuildFxBuilder
    {
        private const string Asset = "Assets/_Game/Resources/" + BuildFxSettings.ResourceName + ".asset";
        private const string Sounds = "Assets/_Game/Data/CourseSounds.asset";
        private const string PlayerPath = "Assets/_Game/Prefabs/Player/Player.prefab";

        [MenuItem("Shift Jam/FX/Land Build Effects (D-0230)")]
        private static void Menu() => Debug.Log(Land());

        /// <summary>Makes or keeps Resources/BuildFx, checks the title's letters and the toy's body, and returns the report.</summary>
        public static string Land()
        {
            var log = new StringBuilder("D-0230 BUILD FX (Artificer)\n");
            BuildFxSettings fx = Settings(log);
            Title(fx, log);
            Toy(fx, log);
            log.Append("No scene, prefab or Artificer build data was written (the effects cut their pieces at run time).");
            return log.ToString();
        }

        /// <summary>Resources/BuildFx: made when missing, else kept as it is; an empty Course Sounds is filled.</summary>
        private static BuildFxSettings Settings(StringBuilder log)
        {
            var fx = AssetDatabase.LoadAssetAtPath<BuildFxSettings>(Asset);
            bool made = fx == null;
            if (made)
            {
                fx = ScriptableObject.CreateInstance<BuildFxSettings>();
                AssetDatabase.CreateAsset(fx, Asset);
            }
            if (fx.courseSounds == null)
            {
                fx.courseSounds = AssetDatabase.LoadAssetAtPath<CourseSounds>(Sounds);
                if (fx.courseSounds == null)
                {
                    string[] found = AssetDatabase.FindAssets("t:CourseSounds");
                    if (found.Length > 0) fx.courseSounds = AssetDatabase.LoadAssetAtPath<CourseSounds>(AssetDatabase.GUIDToAssetPath(found[0]));
                }
                if (fx.courseSounds != null) { EditorUtility.SetDirty(fx); log.Append("Course Sounds linked: ").Append(AssetDatabase.GetAssetPath(fx.courseSounds)).Append('\n'); }
            }
            AssetDatabase.SaveAssets();
            log.Append(made ? "MADE " : "KEPT ").Append(Asset).Append(": title ").Append(fx.titleOn ? "on" : "OFF")
               .Append(fx.titleEveryTime ? " (every time)" : " (once a launch)")
               .Append(", the build ").Append(fx.TitleSpan.ToString("F2")).Append(" s").Append(fx.followMusic && fx.courseSounds != null ? " (the music's rise)" : " (Title Seconds)")
               .Append("; rebuild ").Append(fx.rebuildOn ? "on" : "OFF").Append(fx.rebuildKid ? " (toy and kid)" : " (toy only)")
               .Append(", cut by ").Append(fx.rebuildPieces).Append(", cover ").Append(fx.moveCover).Append('\n');
            return fx;
        }

        /// <summary>The title's letters on the menu model prefab, and Artificer's cut of each.</summary>
        private static void Title(BuildFxSettings fx, StringBuilder log)
        {
            var model = Resources.Load<MenuModel>(MenuModel.PrefabPath);
            if (model == null) { log.Append("TITLE: no Resources/").Append(MenuModel.PrefabPath).Append(" prefab: the menu draws itself, nothing to build (the title shows as before).\n"); return; }
            if (!model.TryGetComponent(out MenuTitle title)) { log.Append("TITLE: the menu model has no MenuTitle: flat title art, nothing to build.\n"); return; }
            SerializedProperty letters = new SerializedObject(title).FindProperty("letters");
            if (letters == null || letters.arraySize == 0) { log.Append("TITLE: MenuTitle has no letters (run Shift Jam > Menu > Build Title Menu Model): nothing to build.\n"); return; }
            int all = 0;
            for (int i = 0; i < letters.arraySize; i++)
            {
                var shape = letters.GetArrayElementAtIndex(i).objectReferenceValue as Mesh;
                if (shape == null) { log.Append("TITLE: letter ").Append(i + 1).Append(" is empty\n"); continue; }
                if (!shape.isReadable) { log.Append("TITLE: ").Append(shape.name).Append(" is NOT readable: Artificer cannot cut it in a build (MenuPieces makes it readable; run Build Title Menu Model)\n"); continue; }
                int pixels = Pixels(shape);
                int pieces = Count(shape, a => TitleBuildIn.CutByPixel(a));
                all += pieces;
                log.Append("TITLE: ").Append(shape.name).Append(": ").Append(pieces).Append(" cubes for ").Append(pixels).Append(" pixels")
                   .Append(pieces == pixels ? " (one a pixel)\n" : "  <- NOT one a pixel: the cut keys need a look\n");
            }
            log.Append("TITLE: ").Append(all).Append(" cubes in all, ").Append(fx.titleOrder).Append(" order, ").Append(fx.titleFlight).Append('\n');
        }

        /// <summary>The Player prefab's bodies and how many pieces each way of cutting makes of them.</summary>
        private static void Toy(BuildFxSettings fx, StringBuilder log)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) == null) { log.Append("TOY: no ").Append(PlayerPath).Append(": not checked\n"); return; }
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
            var baked = new List<Mesh>();
            try
            {
                log.Append("TOY: ").Append(PlayerPath).Append(player.GetComponent<Respawn>() != null ? " has Respawn (it adds RebuildOnReset at run time)" : " has NO Respawn: no reset, nothing to rebuild").Append('\n');
                foreach (Animator body in player.GetComponentsInChildren<Animator>(true))
                {
                    SkinnedMeshRenderer[] skins = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (skins.Length == 0) continue;
                    var parts = new List<Mesh>();
                    float low = float.PositiveInfinity, high = float.NegativeInfinity;
                    foreach (SkinnedMeshRenderer skin in skins)
                    {
                        if (skin.sharedMesh == null) continue;
                        var mesh = new Mesh { name = "~BuildFxBake", hideFlags = HideFlags.HideAndDontSave };
                        skin.BakeMesh(mesh, true);
                        baked.Add(mesh);
                        parts.Add(mesh);
                        Bounds b = mesh.bounds;   // the part's own units, as the points the cut keys are (near enough for a tolerance)
                        low = Mathf.Min(low, b.min.y); high = Mathf.Max(high, b.max.y);
                    }
                    float height = high > low ? high - low : 1f;
                    log.Append("TOY: body ").Append(body.name).Append(body.gameObject.activeSelf ? "" : " (off in the prefab)").Append(": ").Append(parts.Count).Append(" skinned parts; pieces");
                    foreach (BuildFxSettings.Pieces way in new[] { BuildFxSettings.Pieces.Parts, BuildFxSettings.Pieces.TexturePatches, BuildFxSettings.Pieces.Facets })
                    {
                        int n = 0;
                        foreach (Mesh m in parts) n += Count(m, a => RebuildOnReset.CutBy(a, way, height));
                        log.Append(' ').Append(way).Append(' ').Append(n).Append(way == fx.rebuildPieces ? " (in use)" : "").Append(';');
                    }
                    log.Append('\n');
                }
            }
            finally
            {
                foreach (Mesh m in baked) Object.DestroyImmediate(m);
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        /// <summary>How many pieces Artificer's cut makes of a shape, set up by <paramref name="cut"/>: one throwaway object
        /// carrying the shape, its renderer, Artificer's id mark and an Artificer, cut by Artificer's own splitter, gone
        /// after. Nothing is saved (Artificer's BuildData would write an asset at edit time; the splitter alone does not).</summary>
        private static int Count(Mesh shape, System.Action<Art.Artificer> cut)
        {
            var go = new GameObject("~BuildFxCount") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = shape;
                var look = go.AddComponent<MeshRenderer>();
                look.sharedMaterials = new Material[Mathf.Max(1, shape.subMeshCount)];
                var mark = go.AddComponent<Art.ObjectID>();
                mark.id = 0; mark.rend = look;
                var a = go.AddComponent<Art.Artificer>();
                a.target = go;
                cut(a);
                a.random = new System.Random(0);
                List<Art.MeshElement> pieces = Art.GameObjectMeshSplitter.SplitGameObject(a, go, Vector3.zero, null, false);
                return pieces != null ? pieces.Count : 0;
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>How many pixels a title letter holds: every pixel's box carries its own place in the sheet as its UV.</summary>
        private static int Pixels(Mesh shape)
        {
            var places = new HashSet<Vector2>();
            foreach (Vector2 uv in shape.uv) places.Add(uv);
            return places.Count;
        }
    }
}
#endif
