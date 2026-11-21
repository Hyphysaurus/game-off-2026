#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Downstairs (D-0103, his "a scene underneath ... retrace steps ... jump down into the larger house"): an open-plan
    /// living room and kitchen one storey under the tutorial hallway, held by the Toy Army's splinter faction (tan).
    /// - The reveal: the pencil bridge's pit loses its floor under the bridge, and the room glows 160 below. Dropping in
    ///   breaks a toy (the fall rule); the paper plane from the playroom desk carries one down (glider-gated).
    /// - The way back to the hallway from the playroom: the mouse hole the hallway lets out at (Portal, toy only), arriving
    ///   on the hallway floor just past the bridge.
    /// - What says so: a toy standing by the gap (ProblemHint, its words), a tape note on the wall over the gap, and one on
    ///   the playroom desk by the plane.
    /// - The room: the Playroom pack's room modules (as the bedroom), furniture from Cute_Furniture_Free, Christmas_Pack_FREE
    ///   and Food_Free scaled to the house (a real metre is 55 here, as the bedroom's bed), warm light.
    /// - The arena: six tan soldiers (the pack's soldier on a tan copy of its colour sheet) round the kitchen, sandbag cover,
    ///   cups and cans that break. Knock each down once (SquadGate) and the toy door home opens: a copy of the bedroom's,
    ///   leading back to the playroom.
    /// Everything lives under Room/Course/Downstairs (and a few named pieces in R3/R4); running it again replaces them.
    /// Menu: Shift Jam / House / Land Downstairs (D-0103).
    /// </summary>
    public static partial class CourseBuilder
    {
        const float DownFloor = -160f, DownCeiling = -30f, DownX0 = 30f, DownZ0 = -330f, Tile = 65f;
        const int DownTilesX = 10, DownTilesZ = 6;                // D-0105: 650 x 390 (living, entry hall, stairs, kitchen, dining)
        const float GapX0 = HallBridge0, GapX1 = HallBridge1;      // the pencil bridge's drop
        const float Metre = 55f;                                   // a real metre in the house (the bedroom's bed is 1.9 m and 109 long... 57)
        const string SplinterTex = "Assets/_Imports/Generated/Army/Army_Color_Splinter.png";
        const string SplinterMat = "Assets/_Game/Data/Army_Splinter.mat";

        /// <summary>Batch: the downstairs, then the breakables (the arena's cups and cans are made again with it).</summary>
        public static void LandDownstairsBatch()
        {
            LandDownstairs();
            BreakableBuilder.Build();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Shift Jam/House/Land Downstairs (D-0103)")]
        public static void LandDownstairs()
        {
            Log.Clear();
            var active = SceneManager.GetActiveScene();
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            var course = GameObject.Find("Room/Course");
            var r3 = course != null ? course.transform.Find("R3_SkirtingRun") : null;
            var r4 = course != null ? course.transform.Find("R4_Playroom") : null;
            var bed = GameObject.Find("Room/Bedroom");
            if (r3 == null || r4 == null || bed == null) { Debug.LogError("D-0103: no R3 / R4 / Bedroom"); return; }

            var old = course.transform.Find("Downstairs"); if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (var n in new[] { "PitFloorWest", "PitFloorEast", "GapShaft", "GapNpc", "GapNote", "FromPlayroom" })
            { var o = r3.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
            foreach (var n in new[] { "BackToHallway", "GliderNote" }) { var o = r4.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
            var root = new GameObject("Downstairs").transform; root.SetParent(course.transform, false);

            OpenTheGap(r3);
            // the player's "fell out of the world" height was -20: anything below the hallway's pits went back to the
            // checkpoint, and the room is 160 down (his "restarting when i try gliding down")
            var player = GameObject.Find("Player");
            var respawn = player != null ? player.GetComponent<Respawn>() : null;
            if (respawn != null)
            {
                var so = new SerializedObject(respawn); var kill = so.FindProperty("killHeight");
                if (kill != null && kill.floatValue > DownFloor - 40f) { Log.AppendLine($"player: fall-out height {kill.floatValue} -> {DownFloor - 40f}"); kill.floatValue = DownFloor - 40f; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            else Log.AppendLine("player: NO Respawn: fall-out height not changed");
            Shell(root, bed.transform);
            DownstairsLights(root);
            Dress(root);
            Furnish(root);
            var squad = Arena(root);
            DoorHome(root, bed.transform, r4, squad);
            WayBack(r3, r4);
            Pointers(r3, r4);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("D-0103 DOWNSTAIRS LANDED\n" + Log);
        }

        // ---- the gap: the pit's one long floor cut either side of the bridge, a short shaft down to the room's ceiling
        static void OpenTheGap(Transform r3)
        {
            var pit = r3.Find("PitFloor");
            Material m = null;
            if (pit != null)
            {
                var b = pit.GetComponent<Renderer>().bounds; m = pit.GetComponent<Renderer>().sharedMaterial;
                Object.DestroyImmediate(pit.gameObject);
                Cube("PitFloorWest", r3, new Vector3((b.min.x + GapX0) / 2f, b.center.y, b.center.z), new Vector3(GapX0 - b.min.x, b.size.y, b.size.z), m);
                Cube("PitFloorEast", r3, new Vector3((GapX1 + b.max.x) / 2f, b.center.y, b.center.z), new Vector3(b.max.x - GapX1, b.size.y, b.size.z), m);
                Log.AppendLine($"gap: the pit floor cut at x {GapX0}..{GapX1}");
            }
            else Log.AppendLine("gap: no PitFloor (already cut?)");
            var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/Greybox_Dark.mat");
            var shaft = new GameObject("GapShaft").transform; shaft.SetParent(r3, false);
            // the shaft stops on top of the ceiling strips round the hole (their top is DownCeiling + 1): reaching into them
            // put faces in one plane at the hole's edges, and they flickered (his "the flickering on the entry way")
            float zs = LaneZ - HallW / 2f, zn = LaneZ + HallW / 2f, top = -13f, bottom = DownCeiling + 1f, h = top - bottom;
            Cube("ShaftS", shaft, new Vector3((GapX0 + GapX1) / 2f, bottom + h / 2f, zs - 0.5f), new Vector3(GapX1 - GapX0 + 2f, h, 1f), dark);
            Cube("ShaftN", shaft, new Vector3((GapX0 + GapX1) / 2f, bottom + h / 2f, zn + 0.5f), new Vector3(GapX1 - GapX0 + 2f, h, 1f), dark);
            Cube("ShaftW", shaft, new Vector3(GapX0 - 0.5f, (bottom - 25.5f) / 2f, LaneZ), new Vector3(1f, -25.5f - bottom, HallW), dark);
            Cube("ShaftE", shaft, new Vector3(GapX1 + 0.5f, (bottom - 25.5f) / 2f, LaneZ), new Vector3(1f, -25.5f - bottom, HallW), dark);
        }

        // ---- the room: the bedroom's modules, the same way round
        static void Shell(Transform root, Transform bedroom)
        {
            DownMats.Load();
            var shell = new GameObject("Shell").transform; shell.SetParent(root, false);
            float x1 = DownX0 + DownTilesX * Tile, z1 = DownZ0 + DownTilesZ * Tile;
            for (int i = 0; i < DownTilesX; i++)
                for (int k = 0; k < DownTilesZ; k++)
                {
                    float cx = DownX0 + (i + 0.5f) * Tile, cz = DownZ0 + (k + 0.5f) * Tile;
                    var fl = Spawn("Floor_01", $"Floor_{i}_{k}", shell, new Vector3(cx, DownFloor, cz), new Vector3(0f, ((i * 7 + k * 3) % 2) * 180f, 0f), 1f);
                    Paint(fl, IsKitchenTile(cx, cz) ? DownMats.KitchenTile : DownMats.Planks);
                    if (i == StairTileX && k == StairTileZ) continue;   // the stairwell: no ceiling (LivingRoom.Stairs boxes it)
                    bool underGap = cx - Tile / 2f < GapX1 && cx + Tile / 2f > GapX0 && cz - Tile / 2f < LaneZ + HallW / 2f && cz + Tile / 2f > LaneZ - HallW / 2f;
                    if (!underGap) { Spawn("Ceiling_01", $"Ceiling_{i}_{k}", shell, new Vector3(cx, DownCeiling, cz), Vector3.zero, 1f); continue; }
                    // the tile under the gap: ceiling strips round the hole
                    var cm = CeilingMat();
                    float tx0 = cx - Tile / 2f, tx1 = cx + Tile / 2f, tz0 = cz - Tile / 2f, tz1 = cz + Tile / 2f;
                    float gz0 = LaneZ - HallW / 2f, gz1 = LaneZ + HallW / 2f;
                    Cube("CeilingW", shell, new Vector3((tx0 + GapX0) / 2f, DownCeiling + 0.5f, cz), new Vector3(GapX0 - tx0, 1f, Tile), cm);
                    Cube("CeilingE", shell, new Vector3((GapX1 + tx1) / 2f, DownCeiling + 0.5f, cz), new Vector3(tx1 - GapX1, 1f, Tile), cm);
                    Cube("CeilingS", shell, new Vector3((GapX0 + GapX1) / 2f, DownCeiling + 0.5f, (tz0 + gz0) / 2f), new Vector3(GapX1 - GapX0, 1f, gz0 - tz0), cm);
                    Cube("CeilingN", shell, new Vector3((GapX0 + GapX1) / 2f, DownCeiling + 0.5f, (gz1 + tz1) / 2f), new Vector3(GapX1 - GapX0, 1f, tz1 - gz1), cm);
                }
            // walls: each side copies the bedroom's wall on that side (its turn, and how far out it stands from the floor's edge)
            void Side(string side, Vector3 dir, float from, float to, float edge)
            {
                var sample = bedroom.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Wall_" + side));
                Quaternion turn = sample != null ? sample.rotation : Quaternion.LookRotation(-dir);
                float out_ = sample != null ? Mathf.Abs(Vector3.Dot(sample.position, dir)) - 130f : 3f;
                int n = Mathf.RoundToInt((to - from) / Tile);
                for (int i = 0; i < n; i++)
                {
                    float along = from + (i + 0.5f) * Tile;
                    Vector3 p = dir.x != 0f ? new Vector3(edge + dir.x * out_, DownFloor, along) : new Vector3(along, DownFloor, edge + dir.z * out_);
                    var w = Spawn("Wall_01", $"Wall_{side}_{i}", shell, p, turn.eulerAngles, 1f);
                    PaintWall(w);
                }
            }
            Side("South", Vector3.back, DownX0, x1, DownZ0);
            Side("North", Vector3.forward, DownX0, x1, z1);
            Side("West", Vector3.left, DownZ0, z1, DownX0);
            Side("East", Vector3.right, DownZ0, z1, x1);
            foreach (var (px, pz) in new[] { (DownX0, DownZ0), (DownX0, z1), (x1, DownZ0), (x1, z1) })
                PaintWall(Spawn("Wall_05", "Pillar", shell, new Vector3(px + Mathf.Sign(px - (DownX0 + x1) / 2f) * 2.5f, DownFloor, pz + Mathf.Sign(pz - (DownZ0 + z1) / 2f) * 2.5f), Vector3.zero, 1f));
            Log.AppendLine($"downstairs: {DownTilesX} x {DownTilesZ} tiles, x {DownX0}..{x1}, z {DownZ0}..{z1}, floor {DownFloor}, ceiling {DownCeiling}");
        }

        static Material CeilingMat()
        {
            var g = AssetDatabase.FindAssets("Ceiling_01 t:Prefab", new[] { Pack }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "Ceiling_01");
            var go = g != null ? AssetDatabase.LoadAssetAtPath<GameObject>(g) : null;
            return go != null ? go.GetComponentInChildren<Renderer>().sharedMaterial : null;
        }

        // ---- furniture, scaled from real metres to the house (and kept under the ceiling)
        static GameObject Furniture(Transform parent, string prefab, Vector3 at, float yaw, float maxHeight = 120f)
        {
            var path = AssetDatabase.FindAssets(prefab + " t:Prefab", new[] { "Assets/ithappy", "Assets/_Imports/ithappy" })
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == prefab);
            if (path == null) { Log.AppendLine("downstairs: MISSING " + prefab); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            go.name = prefab;
            HangDoors(go, prefab);
            go.transform.localScale = Vector3.one * Metre;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = at;
            var b = B(go);
            if (b.size.y > maxHeight) { go.transform.localScale *= maxHeight / b.size.y; b = B(go); }
            go.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; }
            return go;
        }

        // ---- the splinter faction: tan soldiers, sandbags, things to break, and the gate their rout opens
        static SquadGate Arena(Transform root)
        {
            var arena = new GameObject("TanSplinter").transform; arena.SetParent(root, false);
            var gate = arena.gameObject.AddComponent<SquadGate>();
            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPiece);
            var tan = SplinterMaterial();
            float y = DownFloor + 0.02f;
            Vector3[] posts =
            {
                // the kitchen: round the island and along the counters
                new Vector3(510f, y, -265f), new Vector3(600f, y, -305f), new Vector3(625f, y, -160f),
                new Vector3(495f, y, -135f), new Vector3(640f, y, -235f), new Vector3(560f, y, -95f),
                // the dining nook
                new Vector3(525f, y, 35f), new Vector3(650f, y, 40f), new Vector3(640f, y, -40f),
            };
            var dining = root.Find("Furniture/DiningTable");
            int count = 0;
            void Post(Vector3 at, string name, bool officer)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(soldier, arena); s.name = name;
                Vector3 toGap = new Vector3((GapX0 + GapX1) / 2f, at.y, LaneZ) - at; toGap.y = 0f;
                s.transform.SetPositionAndRotation(at, Quaternion.LookRotation(toGap));
                var ts = s.GetComponent<ToySoldier>();
                Set(ts, "wanderRadius", officer ? 0.5f : 3f); Set(ts, "sightRange", officer ? 40f : 30f); Set(ts, "fireRange", officer ? 34f : 26f);
                var hp = s.GetComponent<Damageable>(); if (hp != null) Set(hp, "maxHealth", officer ? 140f : 60f);
                if (officer) s.transform.localScale *= 1.3f;
                if (tan != null) foreach (var r in s.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.StartsWith("Army_Color") ? tan : m).ToArray();
                count++;
            }
            for (int i = 0; i < posts.Length && soldier != null; i++) Post(posts[i], "TanSoldier_" + (i + 1), false);
            // the officer stands on the dining table, over the nook
            if (soldier != null)
            {
                Vector3 top = dining != null ? new Vector3(B(dining.gameObject).center.x, B(dining.gameObject).max.y + 0.02f, B(dining.gameObject).center.z) : new Vector3(585f, y, 10f);
                Post(top, "TanOfficer", true);
            }
            // cover: a ring west of the island, a bastion by the fridge, a wall across the nook's mouth, the flag by the hutch
            SandbagRing(arena, new Vector3(480f, DownFloor, -200f), 12f, 90f);
            SandbagRing(arena, new Vector3(600f, DownFloor, -270f), 10f, 180f);
            SandbagRing(arena, new Vector3(560f, DownFloor, -45f), 10f, 0f);
            var flag = Spawn("Army_Flag_Stand_01", "TanFlag", arena, new Vector3(655f, DownFloor, 15f), new Vector3(0f, -90f, 0f), 1f); Rest(flag, DownFloor, null);
            if (tan != null) foreach (var r in flag.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.StartsWith("Army_Color") ? tan : m).ToArray();
            // things to break (BreakableBuilder lists them by name)
            string[] loose = { "Cup_03", "Can_02", "Cup_03", "Can_02", "Matchbox_01", "Wooden_Cube_01", "Color_Cube_03", "Wooden_Cylinder_01" };
            for (int i = 0; i < loose.Length; i++)
            {
                var p = Spawn(loose[i], loose[i], arena, new Vector3(470f + (i % 4) * 30f, DownFloor, -300f + (i / 4) * 45f), new Vector3(0f, i * 40f, 0f), 1f);
                Rest(p, DownFloor, null);
            }
            Log.AppendLine($"arena: {count} tan soldiers (one an officer on the dining table), sandbags, {loose.Length} things to break (run Build Breakables to cut new ones)");
            return gate;
        }

        /// <summary>The pack's colour sheet with its greens turned tan (in the git-ignored Generated folder: it is made from
        /// the pack), on a copy of its material.</summary>
        static Material SplinterMaterial()
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>(Pack + "/Materials/Army_Color_1.mat");
            if (src == null) { Log.AppendLine("arena: no Army_Color_1: soldiers stay green"); return null; }
            var tex = src.GetTexture("_BaseMap") as Texture2D;
            if (tex != null && AssetDatabase.LoadAssetAtPath<Texture2D>(SplinterTex) == null)
            {
                var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(tex, rt); var prev = RenderTexture.active; RenderTexture.active = rt;
                var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); copy.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                var px = copy.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    Color.RGBToHSV(px[i], out float h, out float s, out float v);
                    if (h > 0.17f && h < 0.45f && s > 0.15f)   // the greens: plastic army green to tan
                    {
                        var c = Color.HSVToRGB(0.095f, Mathf.Clamp01(s * 0.75f), Mathf.Clamp01(v * 1.25f + 0.08f)); c.a = px[i].a; px[i] = c;
                    }
                }
                copy.SetPixels(px); copy.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(SplinterTex));
                File.WriteAllBytes(SplinterTex, copy.EncodeToPNG());
                Object.DestroyImmediate(copy);
                AssetDatabase.ImportAsset(SplinterTex);
            }
            var tanTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SplinterTex);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SplinterMat);
            if (mat == null) { mat = new Material(src) { name = "Army_Splinter" }; AssetDatabase.CreateAsset(mat, SplinterMat); }
            mat.CopyPropertiesFromMaterial(src);
            if (tanTex != null) mat.SetTexture("_BaseMap", tanTex);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---- the toy door home: the bedroom's doorway and door again, on the room's south wall, opened by the rout
        static void DoorHome(Transform root, Transform bedroom, Transform r4, SquadGate gate)
        {
            var doorway = bedroom.Find("ToyDoorway"); var glow = bedroom.Find("DoorGlow"); var light = bedroom.Find("DoorLight"); var goal = bedroom.Find("Goal");
            var entry = r4.Find("Entry");
            if (doorway == null || goal == null || entry == null) { Log.AppendLine("door home: NOT MADE (no bedroom doorway / goal or playroom entry)"); return; }
            var home = new GameObject("DoorHome").transform; home.SetParent(root, false);
            Vector3 bedIn = bedroom.position - doorway.position; bedIn.y = 0f;
            bedIn = Mathf.Abs(bedIn.x) > Mathf.Abs(bedIn.z) ? new Vector3(Mathf.Sign(bedIn.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(bedIn.z));
            Quaternion fromRot = Quaternion.LookRotation(bedIn), toRot = Quaternion.LookRotation(Vector3.forward);   // the south wall, facing into the room
            Vector3 toPos = new Vector3(190f, DownFloor, DownZ0);
            Vector3 Map(Vector3 p) => toPos + toRot * (Quaternion.Inverse(fromRot) * (p - doorway.position));
            Quaternion MapRot(Quaternion q) => toRot * Quaternion.Inverse(fromRot) * q;
            GameObject Copy(Transform src, string name) { var c = Object.Instantiate(src.gameObject, home, true); c.name = name; c.transform.SetPositionAndRotation(Map(src.position), MapRot(src.rotation)); return c; }
            var dw = Copy(doorway, "Doorway");
            if (glow != null) Copy(glow, "Glow");
            if (light != null) Copy(light, "Light");
            var portalGo = Copy(goal, "Portal");
            var portal = portalGo.GetComponent<Portal>();
            if (portal != null) { Set(portal, "target", entry); Set(portal, "requiredSize", (int)ShiftSize.Toy); }
            var door = dw.GetComponentInChildren<Door>(true);
            var hint = dw.GetComponentInChildren<ProblemHint>(true);
            if (hint != null) Set(hint, "message", "Shut. The Tan Splinter holds this room.");
            if (gate != null && door != null) { var so = new SerializedObject(gate); var d = so.FindProperty("doors"); d.arraySize = 1; d.GetArrayElementAtIndex(0).objectReferenceValue = door; so.ApplyModifiedPropertiesWithoutUndo(); }
            Log.AppendLine($"door home: at {toPos} on the south wall, opened by the rout, to the playroom's entry");
        }

        // ---- the playroom's mouse hole back into the hallway, landing just past the bridge, facing it
        static void WayBack(Transform r3, Transform r4)
        {
            var entry = r4.Find("Entry"); var glow = r4.Find("EntryGlow");
            if (entry == null) { Log.AppendLine("way back: no R4 Entry"); return; }
            var arrive = new GameObject("FromPlayroom").transform; arrive.SetParent(r3, false);
            arrive.SetPositionAndRotation(new Vector3(HallBridge1 + 8f, 0f, LaneZ), Quaternion.LookRotation(Vector3.left));
            // in FRONT of the wall's face (found by a ray from the arrival point), never behind it: the first cut put the
            // trigger 1.5 behind the glow, inside the wall, where no toy can reach (his "i cant go back into the hallway")
            Vector3 hole = glow != null ? glow.position : entry.position + Vector3.left * 8f;
            float face = hole.x;
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(entry.position + Vector3.up * 1.5f, Vector3.left, 20f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).ToArray();
            if (hits.Length > 0) face = hits[0].point.x;
            var back = Trigger("BackToHallway", r4, new Vector3(face + 1.6f, 1.8f, hole.z), new Vector3(2.4f, 3.6f, 3f));
            var p = back.AddComponent<Portal>(); Set(p, "target", arrive); Set(p, "requiredSize", (int)ShiftSize.Toy);
            Log.AppendLine($"way back: the playroom's mouse hole ({back.transform.position}) to the hallway at {arrive.position}, facing the bridge");
        }

        // ---- what says so: a toy by the gap, a note over it, a note by the plane
        static void Pointers(Transform r3, Transform r4)
        {
            const string words = "The living room is down there. The Tan Splinter took it.\nToo far to fall. With a paper plane: drop through,\nthen open it (Jump) once you are below the floor.";
            var path = AssetDatabase.FindAssets("Basic_Character t:Prefab", new[] { "Assets/_Imports/ithappy" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == "Basic_Character");
            Vector3 at = new Vector3(GapX0 - 16f, 0f, LaneZ - HallW / 2f + 3f);   // west of the hallway's lantern (x 163..169), clear of the charge core
            if (path != null)
            {
                var npc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), r3); npc.name = "GapNpc";
                npc.transform.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.left));
                var b = B(npc); npc.transform.localScale *= 3.4f / Mathf.Max(0.01f, b.size.y);   // a toy's height
                Rest(npc, 0f, null);
                var hint = npc.AddComponent<BoxCollider>(); hint.isTrigger = true; hint.size = new Vector3(14f, 6f, 14f) / npc.transform.localScale.x; hint.center = Vector3.up * 3f / npc.transform.localScale.x;
                var ph = npc.AddComponent<ProblemHint>(); Set(ph, "message", words); Set(ph, "anySize", true);
            }
            else Log.AppendLine("pointers: no Basic_Character: the note only");
            TapeSign("GapNote", r3, new Vector3((GapX0 + GapX1) / 2f, 4.2f, LaneZ + HallW / 2f - 0.15f), Vector3.back, "LIVING ROOM BELOW\nDROP THROUGH, THEN\nOPEN THE PLANE", 8f, 3.4f);
            var glider = r4.GetComponentInChildren<GliderPickup>(true);
            if (glider != null)
            {
                var g = glider.transform.position;
                TapeSign("GliderNote", r4, g + new Vector3(0f, 2.5f, -6f), Vector3.back, "GLIDE DOWN FROM\nTHE PENCIL BRIDGE", 7f, 2.6f);
            }
            Log.AppendLine("pointers: the toy by the gap, the note over it, the note by the plane");
        }
    }
}
#endif
