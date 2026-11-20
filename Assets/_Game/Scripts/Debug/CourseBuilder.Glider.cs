#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Roadmap item 2, the glider (D-0095): Resources/Glide (GlideSettings: the Playroom pack's Paper_Plane_01, turned
    /// nose-ahead from its own shape, and its sounds, all from packs already in the project) and the paper plane lying on
    /// the playroom's desk beside core B (GliderPickup, a small sign at a toy's eye). The kid climbs the desk, shrinks at
    /// core B, and the toy can glide down instead of taking the ruler ramps (they stay). Not in the hallway: its drops are
    /// the lesson there. BuildPlayroom places the plane too, so a full course build keeps it.
    /// Batch: -executeMethod ShiftJam.CourseBuilder.LandGlider (-nographics is fine). Greybox and the asset saved.
    /// </summary>
    public static partial class CourseBuilder
    {
        const string GlidePath = "Assets/_Game/Resources/Glide.asset";
        const string PlanePiece = "Paper_Plane_01";
        const string WindLoopPath = "Assets/Procedural Worlds/Ambient Sounds/Audio/Sounds/Ambient/sfx-ambient-wind_0-0_lp.wav";

        public static void LandGlider()
        {
            Log.Clear();
            var glide = GlideAsset();
            EditorSceneManager.OpenScene(ScenePath);
            Physics.SyncTransforms();
            var r4 = GameObject.Find("Room/Course/R4_Playroom");
            var coreB = r4 != null ? r4.transform.Find("CoreB_Shrink") : null;
            var table = r4 != null ? r4.transform.Find("Table") : null;
            if (coreB == null || table == null) { Debug.LogError("D-0095: no R4 / CoreB_Shrink / Table in " + ScenePath); EditorApplication.Exit(1); return; }
            DeskGlider(r4.transform, coreB.gameObject, B(table.gameObject), glide);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("D-0095 GLIDER LANDED\n" + Log);
        }

        /// <summary>Resources/Glide: made once with the plane and its sounds (his tuning is kept on later runs; only empty
        /// slots are filled). The plane is turned so its nose points +Z, read from its own collider's shape.</summary>
        static GlideSettings GlideAsset()
        {
            var g = AssetDatabase.LoadAssetAtPath<GlideSettings>(GlidePath);
            bool made = g == null;
            if (made) { g = ScriptableObject.CreateInstance<GlideSettings>(); AssetDatabase.CreateAsset(g, GlidePath); }
            if (g.planePrefab == null) g.planePrefab = FindPrefab(PlanePiece);
            if (made || g.planeEuler == Vector3.zero) { g.planeEuler = PlaneNoseEuler(g.planePrefab, out string how); Log.AppendLine("glide: plane turned " + g.planeEuler + " (" + how + ")"); }
            if (g.open.clip == null) g.open = new CourseSounds.Cue(Clip("Paper_Rustle_v2"), 0.5f, 0.06f);
            if (g.fold.clip == null) g.fold = new CourseSounds.Cue(Clip("Paper_Rustle_v1"), 0.35f, 0.08f);
            if (g.windLoop == null) g.windLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(WindLoopPath);
            EditorUtility.SetDirty(g);
            Log.AppendLine($"glide: {(made ? "made" : "kept")} {GlidePath}: plane {(g.planePrefab != null ? g.planePrefab.name : "NONE")}, open {(g.open.clip != null ? g.open.clip.name : "-")}, fold {(g.fold.clip != null ? g.fold.clip.name : "-")}, wind {(g.windLoop != null ? g.windLoop.name : "NONE")}; sink {g.sinkSpeed}, speed {g.glideSpeed}, turn {g.turnRate}");
            return g;
        }

        static GameObject FindPrefab(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { Pack }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Log.AppendLine("MISSING prefab " + name);
            return null;
        }

        /// <summary>The turn about up that points the plane's nose along +Z. A dart is narrow at the nose and wide at the
        /// tail: rays down across a copy of it (its collider is the mesh) measure its width near each end of its long axis.</summary>
        static Vector3 PlaneNoseEuler(GameObject prefab, out string how)
        {
            how = "no prefab";
            if (prefab == null) return Vector3.zero;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(0f, -900f, 0f), Quaternion.identity);
                Physics.SyncTransforms();
                var cols = go.GetComponentsInChildren<Collider>();
                Bounds b = B(go);
                bool alongX = b.size.x >= b.size.z;
                float len = alongX ? b.size.x : b.size.z, wide = alongX ? b.size.z : b.size.x;
                // the width of the plane in the first and the last fifth of its length, from rays straight down
                float[] spanLo = { float.MaxValue, float.MinValue }, spanHi = { float.MaxValue, float.MinValue };
                int hits = 0;
                for (int i = 0; i <= 40; i++)
                for (int j = 0; j <= 40; j++)
                {
                    float a = -0.5f + i / 40f, c = -0.5f + j / 40f;   // a along the length, c across
                    Vector3 p = b.center + (alongX ? new Vector3(a * len, 0f, c * wide) : new Vector3(c * wide, 0f, a * len));
                    var ray = new Ray(new Vector3(p.x, b.max.y + 5f, p.z), Vector3.down);
                    bool hit = cols.Any(col => col.Raycast(ray, out _, b.size.y + 10f));
                    if (!hit) continue;
                    hits++;
                    float across = c * wide;
                    if (a < -0.3f) { spanLo[0] = Mathf.Min(spanLo[0], across); spanLo[1] = Mathf.Max(spanLo[1], across); }
                    if (a > 0.3f) { spanHi[0] = Mathf.Min(spanHi[0], across); spanHi[1] = Mathf.Max(spanHi[1], across); }
                }
                float wLo = spanLo[1] - spanLo[0], wHi = spanHi[1] - spanHi[0];
                // the nose is the narrow end: low end narrow -> nose toward -axis
                bool noseLow = wLo < wHi;
                Vector3 nose = alongX ? (noseLow ? Vector3.left : Vector3.right) : (noseLow ? Vector3.back : Vector3.forward);
                float yaw = Quaternion.FromToRotation(nose, Vector3.forward).eulerAngles.y;
                how = $"{hits} rays hit; {(alongX ? "long along x" : "long along z")} ({len:F2} x {wide:F2} x {b.size.y:F2}); width {wLo:F2} at the low end, {wHi:F2} at the high end; nose {nose}";
                return new Vector3(0f, Mathf.Round(yaw), 0f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The paper plane on the desk: beside core B toward the desk's south edge (the way a glide goes, into the
        /// room), on clear desk top, its nose to the room; its colliders off (the trigger box takes E; a toy walks over paper);
        /// a sign at a toy's eye. Replaced whole on each run.</summary>
        static void DeskGlider(Transform r4, GameObject coreB, Bounds desk, GlideSettings glide)
        {
            var old = r4.Find("Glider_Desk"); if (old != null) Object.DestroyImmediate(old.gameObject);
            Physics.SyncTransforms();
            if (glide == null || glide.planePrefab == null) { Log.AppendLine("glider: NO plane prefab: nothing placed"); return; }
            var plane = (GameObject)PrefabUtility.InstantiatePrefab(glide.planePrefab);
            plane.name = "PaperPlane";
            foreach (var col in plane.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            plane.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(glide.planeEuler);   // nose to the room (south, -z)
            Bounds pb = B(plane);
            float s = glide.planeLength / Mathf.Max(0.01f, pb.size.z);
            plane.transform.localScale = Vector3.one * s;
            pb = B(plane);
            // a clear spot on the desk top: nearest to a point south-east of the core, clear of the edge by 3 and of everything on the desk
            Vector3 core = coreB.transform.position, want = new Vector3(core.x + 9f, desk.max.y, core.z - 4f);
            Vector3 half = new Vector3(pb.extents.x + 0.6f, 2f, pb.extents.z + 0.6f);
            var spots = new List<Vector3>();
            for (float dx = -24f; dx <= 24f; dx += 1f)
            for (float dz = -14f; dz <= 14f; dz += 1f) spots.Add(new Vector3(core.x + dx, desk.max.y, core.z + dz));
            Vector3? at = null; string why = "";
            foreach (var p in spots.OrderBy(p => (p - want).sqrMagnitude))
            {
                if (p.x - half.x < desk.min.x + 3f || p.x + half.x > desk.max.x - 3f || p.z - half.z < desk.min.z + 3f || p.z + half.z > desk.max.z - 3f) continue;
                if (new Vector2(p.x - core.x, p.z - core.z).magnitude < 7f) continue;   // not on the core
                float top = FloorUnder(p + Vector3.up * 3f, plane.transform);
                if (Mathf.Abs(top - desk.max.y) > 0.4f) continue;                      // the desk's own top, not a thing on it
                var inWay = Physics.OverlapBox(new Vector3(p.x, desk.max.y + half.y + 0.15f, p.z), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => !c.transform.IsChildOf(plane.transform) && !c.transform.IsChildOf(coreB.transform)).ToList();
                if (inWay.Count > 0) { why = string.Join(", ", inWay.Select(c => c.name).Distinct().Take(4)); continue; }
                at = p; break;
            }
            Vector3 spot = at ?? want;
            if (at == null) Log.AppendLine("glider: NO CLEAR SPOT on the desk (last in the way: " + why + "); placed at " + want);
            plane.transform.position += new Vector3(spot.x - pb.center.x, desk.max.y + 0.02f - pb.min.y, spot.z - pb.center.z);
            pb = B(plane);
            // the pickup: a trigger box over the plane for E (a little taller, so a toy standing beside it finds it); the plane
            // and its card under Display, hidden while the player has a glider
            var root = Child(r4, "Glider_Desk");
            root.position = pb.center;
            var box = root.gameObject.AddComponent<BoxCollider>(); box.isTrigger = true;
            Vector3 rs = root.lossyScale;
            box.center = Vector3.zero;
            box.size = new Vector3((pb.size.x + 1f) / Mathf.Max(1e-4f, rs.x), Mathf.Max(3f, pb.size.y + 2f) / Mathf.Max(1e-4f, rs.y), (pb.size.z + 1f) / Mathf.Max(1e-4f, rs.z));
            var display = Child(root, "Display");
            plane.transform.SetParent(display, true);
            var pickup = root.gameObject.AddComponent<GliderPickup>();
            Set(pickup, "display", display.gameObject);
            // a card beside it, at a toy's eye, facing the core (where the toy is made)
            Vector3 toCore = new Vector3(core.x - spot.x, 0f, core.z - spot.z);
            Vector3 side = Vector3.Cross(Vector3.up, toCore.normalized);
            Sign("Sign_Glider", display, new Vector3(spot.x, desk.max.y + 2.6f, spot.z) + side * (pb.extents.x + 2.2f), toCore, "PAPER PLANE\n<b>E</b>  take it", 3.6f, 1.8f);
            Physics.SyncTransforms();
            Log.AppendLine($"glider: paper plane on the desk at {Fmt(pb.center)} ({pb.size.x:F1} x {pb.size.y:F1} x {pb.size.z:F1}, scale {s:F2}), core B at {Fmt(core)} ({new Vector2(pb.center.x - core.x, pb.center.z - core.z).magnitude:F1} away), desk x {desk.min.x:F1}..{desk.max.x:F1} z {desk.min.z:F1}..{desk.max.z:F1} top {desk.max.y:F1}");
        }

        static string Fmt(Vector3 v) => $"({v.x:F1}, {v.y:F1}, {v.z:F1})";
    }
}
#endif
