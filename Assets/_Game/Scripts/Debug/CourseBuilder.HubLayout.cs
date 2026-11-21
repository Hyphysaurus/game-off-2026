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
    /// The hub cleared to a plan (D-0107, his "the main hub room is verrrrrry cluttered... we need to be able to have a
    /// clear plan and layout"). The playroom's beats all stay where they are; what goes is the seeded floor scatter
    /// (BuildPlayroom's Scatter calls: about 200 papers, pencils, cards, cups, blocks, puzzles, notebooks, drawings and
    /// loose books spread over the whole floor). The layout it leaves:
    /// - the expressway is the ring road, its four stops the ways on and off (entry, desk, army maze, wardrobe);
    /// - the zones round it: entry + save (W), desk + glider (NW), army fort + core A (NE), couch (E), wardrobe (SE),
    ///   castle (S), armory + the Raceway door (SW / W), the race track under the fan in the middle;
    /// - what stays of the scatter is gathered: a craft pile on the floor by the desk, a block pile and a laid puzzle
    ///   in the blocks corner, books only on the shelves; no loose paper planes (only the glider is a plane);
    /// - each foam-mat cluster is one colour, a zone's floor mark;
    /// - a tape sign per zone, big enough to read as a toy across the room and as a kid.
    /// Only plain props are touched (a mesh, colliders, a body, Pushable, Damageable, Breakable): nothing with a
    /// script of its own. Idempotent. Menu: Shift Jam / House / Land Hub Layout (D-0107).
    /// </summary>
    public static partial class CourseBuilder
    {
        static readonly System.Type[] PlainProp = { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(BoxCollider), typeof(Rigidbody), typeof(Pushable), typeof(Damageable), typeof(Breakable) };

        /// <summary>Scatter families: name prefix, the pile it goes to ("" = none: all go), how many stay.</summary>
        static readonly (string pile, int keep, string[] names)[] HubFamilies =
        {
            ("craft", 14, new[] { "Note_Paper", "PlayCard", "Pencil", "Chalk", "Eraser", "Clip", "Notebook", "Paper_Block", "Matchbox", "Icecream_Stick", "Bottle_Cap" }),
            ("blocks", 16, new[] { "Color_Cube", "Wooden_Cube", "Wooden_Cylinder", "Wooden_Pyramid", "Constructor", "Cube_Alphabet", "Wooden_Cone", "Wooden_HalfSphere" }),
            ("puzzle", 5, new[] { "Puzzle" }),
            ("craft", 3, new[] { "Cup_03", "Can_02", "Paper_Glass", "CD" }),
            ("", 0, new[] { "Paper_Plane", "Drawing" }),
        };

        [MenuItem("Shift Jam/House/Land Hub Layout (D-0107)")]
        public static void LandHubLayout()
        {
            Log.Clear();
            var active = SceneManager.GetActiveScene();
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            var r4t = GameObject.Find("Room/Course/R4_Playroom");
            if (r4t == null) { Debug.LogError("D-0107: no R4_Playroom"); return; }
            Transform r4 = r4t.transform;
            Vector3 c = r4.position + new Vector3(0f, 0f, 0f);
            var floor = r4.Cast<Transform>().Where(t => t.name.StartsWith("Floor")).Select(t => B(t.gameObject)).ToList();
            if (floor.Count > 0) { var fb = floor[0]; foreach (var f in floor) fb.Encapsulate(f); c = new Vector3(fb.center.x, 0f, fb.center.z); }
            int before = r4.childCount;

            // the piles: on clear floor, each beside its zone
            var piles = new Dictionary<string, Vector3>
            {
                { "craft", new Vector3(c.x - 82f, 0f, c.z + 85f) },     // the floor by the desk, east of the ruler and the box ramps
                { "blocks", new Vector3(c.x - 160f, 0f, c.z - 85f) },   // the blocks corner, between the Raceway door and the armory
                { "puzzle", new Vector3(c.x - 140f, 0f, c.z - 62f) },
            };
            var keep = new Dictionary<string, List<GameObject>>();
            int gone = 0;
            foreach (var (pile, count, names) in HubFamilies)
            {
                var members = r4.Cast<Transform>().Where(t => names.Any(n => t.name.StartsWith(n)) && Plain(t.gameObject)).Select(t => t.gameObject).ToList();
                Vector3 to = pile != "" ? piles[pile] : c;
                var ordered = members.OrderBy(g => (g.transform.position - to).sqrMagnitude).ToList();
                if (!keep.ContainsKey(pile)) keep[pile] = new List<GameObject>();
                int kept = 0;
                foreach (var g in ordered)
                {
                    if (pile != "" && kept < count) { keep[pile].Add(g); kept++; continue; }
                    Object.DestroyImmediate(g); gone++;
                }
            }
            // books: the ones on the two shelves stay; floor books and the kid's floor stacks go
            var shelves = r4.Cast<Transform>().Where(t => t.name == "Shelf" || t.name == "Shelf2").Select(t => B(t.gameObject)).ToList();
            foreach (var bk in r4.Cast<Transform>().Where(t => t.name.StartsWith("Book_") && Plain(t.gameObject)).Select(t => t.gameObject).ToList())
            {
                Vector3 p = bk.transform.position;
                if (shelves.Any(s => p.x >= s.min.x - 1f && p.x <= s.max.x + 1f && p.z >= s.min.z - 1f && p.z <= s.max.z + 1f)) continue;
                Object.DestroyImmediate(bk); gone++;
            }
            Log.AppendLine($"hub: {gone} loose things gone ({before} -> {r4.childCount} in the room)");

            // the kept ones, gathered: laid in a tight spiral on clear floor at their pile
            Physics.SyncTransforms();
            foreach (var kv in keep)
            {
                if (kv.Key == "" || kv.Value.Count == 0) continue;
                int placed = Gather(kv.Value, piles[kv.Key], kv.Key == "puzzle" ? 0f : 1f);
                Log.AppendLine($"hub: {kv.Key} pile {placed}/{kv.Value.Count} at {piles[kv.Key]}");
            }

            MatColours(r4);
            ZoneSigns(r4, c);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("D-0107 HUB LAYOUT LANDED\n" + Log);
        }

        /// <summary>Only a plain prop: no script of its own anywhere in it.</summary>
        static bool Plain(GameObject g) => g.GetComponentsInChildren<Component>(true).All(k => k != null && PlainProp.Any(t => t.IsInstanceOfType(k)));

        /// <summary>Lays <paramref name="items"/> round <paramref name="at"/>, nearest first, each on the floor where it
        /// touches nothing but the floor and the pile itself. <paramref name="jitter"/> turns them a little each.
        /// A thing that finds no room goes. Returns how many were laid.</summary>
        static int Gather(List<GameObject> items, Vector3 at, float jitter)
        {
            var rng = new System.Random(107);
            int laid = 0;
            var mine = new HashSet<Collider>(items.SelectMany(g => g.GetComponentsInChildren<Collider>()));
            foreach (var g in items)
            {
                foreach (var col in g.GetComponentsInChildren<Collider>()) col.enabled = false;
                g.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f * jitter + (jitter == 0f ? 90f * (laid % 4) : 0f), 0f);
                bool ok = false;
                for (int k = 0; k < 60 && !ok; k++)
                {
                    float a = k * 2.4f, r = 1.6f * Mathf.Sqrt(k);
                    g.transform.position = new Vector3(at.x + Mathf.Cos(a) * r, 0f, at.z + Mathf.Sin(a) * r);
                    Rest(g, 0.01f, null);
                    var b = B(g); b.Expand(new Vector3(-0.1f, -0.05f, -0.1f));
                    ok = !Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Any(h => !h.transform.IsChildOf(g.transform) && !h.name.StartsWith("Floor") && !h.name.StartsWith("Mat"));
                }
                foreach (var col in g.GetComponentsInChildren<Collider>()) col.enabled = true;
                Physics.SyncTransforms();
                if (ok) laid++; else Object.DestroyImmediate(g);
            }
            return laid;
        }

        /// <summary>Every 2x2 cluster of foam mats becomes one colour (the pack's four, in turn), so each reads as one mark.</summary>
        static void MatColours(Transform r4)
        {
            var mats = r4.Cast<Transform>().Where(t => t.name == "Mat").ToList();
            var clusters = new List<List<Transform>>();
            foreach (var m in mats)
            {
                var near = clusters.FirstOrDefault(cl => cl.Any(o => (o.position - m.position).sqrMagnitude < 30f * 30f));
                if (near != null) near.Add(m); else clusters.Add(new List<Transform> { m });
            }
            string[] colour = { "Mat_01", "Mat_02", "Mat_03", "Mat_04" };
            for (int i = 0; i < clusters.Count; i++)
                foreach (var m in clusters[i])
                {
                    var g = Spawn(colour[i % 4], "Mat", r4, m.position, m.eulerAngles, 1f);
                    g.transform.localScale = m.localScale;
                    g.transform.SetSiblingIndex(m.GetSiblingIndex());
                    Object.DestroyImmediate(m.gameObject);
                }
            Log.AppendLine($"hub: {mats.Count} mats in {clusters.Count} clusters, one colour each");
        }

        /// <summary>A tape sign per zone on the wall behind it, 20 up: a toy reads it across the floor, a kid at the chest.</summary>
        static void ZoneSigns(Transform r4, Vector3 c)
        {
            var old = r4.Find("ZoneSigns"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("ZoneSigns").transform; root.SetParent(r4, false);
            float n = c.z + R4HalfZ - 0.3f, s = c.z - R4HalfZ + 0.3f, e = c.x + R4HalfX - 0.3f;
            const float y = 20f, w = 26f, h = 8f;
            TapeSign("Zone_Desk", root, new Vector3(c.x - 80f, y + 30f, n), Vector3.back, "DESK\nthe paper plane", w, h);
            TapeSign("Zone_Army", root, new Vector3(c.x + 125f, y + 20f, n), Vector3.back, "ARMY FORT\ncore inside", w, h);
            TapeSign("Zone_Couch", root, new Vector3(e, y + 40f, c.z + 20f), Vector3.left, "COUCH\nride the fan up", w, h);
            TapeSign("Zone_Wardrobe", root, new Vector3(c.x + 150f, y, s), Vector3.forward, "WARDROBE\nhole at its foot", w, h);
            TapeSign("Zone_Castle", root, new Vector3(c.x + 40f, y, s), Vector3.forward, "CASTLE", w, h * 0.6f);
            TapeSign("Zone_Blocks", root, new Vector3(c.x - 120f, y, s), Vector3.forward, "BLOCKS", w, h * 0.6f);
            Log.AppendLine("hub: 6 zone signs");
        }
    }
}
#endif
