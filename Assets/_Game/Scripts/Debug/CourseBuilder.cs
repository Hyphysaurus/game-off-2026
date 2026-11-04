#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Builds Rooms 3–6 of docs/COURSE.md into Greybox.unity under Room/Course, by prefab bounds, idempotently:
    /// running it again deletes the previous Course container and rebuilds. Menu: Shift Jam / Build Course.
    /// Batch: -executeMethod ShiftJam.CourseBuilder.BuildAll. Editor only; nothing of this ships.
    /// </summary>
    public static class CourseBuilder
    {
        const string ScenePath = "Assets/_Game/Scenes/Greybox.unity";
        const string Pack = "Assets/_Imports/ithappy/Playroom";
        static readonly StringBuilder Log = new StringBuilder();

        // ---- world layout (pack units) ----
        static readonly Vector3 C4 = new Vector3(480f, 0f, -140f);     // playroom centre; the showroom is 4x5 tiles: x 347.5..612.5, z -305..25
        static readonly Vector3 C5 = new Vector3(480f, 0f, -700f);     // wardrobe interior centre
        const float LaneZ = -150f, LaneX0 = -20f, LaneX1 = 281f;       // skirting run, inside the bedroom's south wall

        [MenuItem("Shift Jam/Build Course (Rooms 3-6)")]
        public static void BuildFromMenu() { Build(); }

        public static void BuildAll()
        {
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            Build();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("COURSE BUILD DONE\n" + Log);
        }

        public static void Build()
        {
            Log.Length = 0;
            var room = GameObject.Find("Room") ?? new GameObject("Room");
            var old = room.transform.Find("Course"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var course = new GameObject("Course").transform; course.SetParent(room.transform, false);

            var player = GameObject.Find("Player");
            var respawn = player.GetComponent<Respawn>();
            Set(respawn, "fatalFallHeight", 14f);

            Material dark = Mat("Assets/_Game/Data/Greybox_Dark.mat", new Color(0.22f, 0.2f, 0.19f));
            Material wood = Mat("Assets/_Game/Data/Wardrobe_Wood.mat", new Color(0.36f, 0.23f, 0.13f));
            Material grass = Mat("Assets/_Game/Data/Garden_Grass.mat", new Color(0.33f, 0.52f, 0.22f));
            Material glowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/DoorGlow.mat");
            Material coreMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/ShiftCore.mat");

            Transform r3 = Child(course, "R3_SkirtingRun"), r4 = Child(course, "R4_Playroom"), r5 = Child(course, "R5_Wardrobe"), r6 = Child(course, "R6_Window");
            Transform r3Start = BuildLane(r3, dark, glowMat);
            Transform r4Entry = null, r5Start = null, wardrobeFront = null;
            BuildPlayroom(r4, r6, dark, grass, glowMat, coreMat, out r4Entry, out wardrobeFront);
            r5Start = BuildWardrobe(r5, wood, glowMat, coreMat);

            // ---- portals between rooms ----
            // bedroom toy door -> R3 start (replace the old Goal with a Portal)
            var toyGoal = GameObject.Find("Room/Bedroom/Goal");
            if (toyGoal != null)
            {
                var g = toyGoal.GetComponent<Goal>(); if (g != null) Object.DestroyImmediate(g);
                var p = toyGoal.GetComponent<Portal>() ?? toyGoal.AddComponent<Portal>();
                Set(p, "target", r3Start); Set(p, "requiredSize", (int)ShiftSize.Toy);
                Log.AppendLine("toy door -> R3");
            }
            // R3 end -> R4 entry
            var laneExit = r3.Find("ExitPortal"); if (laneExit != null) Set(laneExit.GetComponent<Portal>(), "target", r4Entry);
            // R4 hole under the wardrobe -> R5 start
            var hole = r4.Find("WardrobeHole"); if (hole != null) Set(hole.GetComponent<Portal>(), "target", r5Start);
            // R5 top -> R4 in front of the wardrobe (kid), opening the wardrobe doors
            var top = r5.Find("TopPortal");
            if (top != null)
            {
                var tp = top.GetComponent<Portal>(); Set(tp, "target", wardrobeFront);
                var doors = r4.GetComponentsInChildren<Door>();
                var so = new SerializedObject(tp); var calls = so.FindProperty("onUsed.m_PersistentCalls.m_Calls"); calls.arraySize = doors.Length;
                for (int i = 0; i < doors.Length; i++) Listener(calls.GetArrayElementAtIndex(i), doors[i], typeof(Door).AssemblyQualifiedName, "Open");
                so.ApplyModifiedPropertiesWithoutUndo();
                Log.AppendLine("R5 top -> wardrobe front, opens " + doors.Length + " doors");
            }
            EditorSceneManager.MarkAllScenesDirty();
            Log.AppendLine("built");
        }

        // =====================================================================================
        // R3: the skirting run
        static Transform BuildLane(Transform r3, Material dark, Material glow)
        {
            float z = LaneZ, w = 20f, h = 14f;
            float x0 = LaneX0, x1 = LaneX1;
            // floor in pieces, leaving two pits: 38..72 (pencil bridge) and 100..103.5 (hop) and 230..234.5 (sprint)
            float[][] floors = { new[] { x0, 38f }, new[] { 72f, 100f }, new[] { 103.5f, 230f }, new[] { 234.5f, x1 } };
            foreach (var f in floors) Cube("Floor", r3, new Vector3((f[0] + f[1]) / 2f, -0.5f, z), new Vector3(f[1] - f[0], 1f, w), dark);
            Cube("PitFloor", r3, new Vector3((x0 + x1) / 2f, -25f, z), new Vector3(x1 - x0, 1f, w), dark);   // 24 down: a fall past 14 = respawn
            Cube("WallN", r3, new Vector3((x0 + x1) / 2f, h / 2f, z + w / 2f + 0.5f), new Vector3(x1 - x0, h + 26f, 1f), dark);
            Cube("WallS", r3, new Vector3((x0 + x1) / 2f, h / 2f, z - w / 2f - 0.5f), new Vector3(x1 - x0, h + 26f, 1f), dark);
            Cube("WallW", r3, new Vector3(x0 - 0.5f, h / 2f, z), new Vector3(1f, h + 26f, w + 2f), dark);
            Cube("WallE", r3, new Vector3(x1 + 0.5f, h / 2f, z), new Vector3(1f, h + 26f, w + 2f), dark);
            Cube("Ceiling", r3, new Vector3((x0 + x1) / 2f, h + 0.5f, z), new Vector3(x1 - x0, 1f, w), dark);
            // lights along the lane
            foreach (float lx in new[] { 0f, 90f, 170f, 250f }) PointLight("LaneLight", r3, new Vector3(lx, h - 2f, z), 45f, 1.6f, new Color(1f, 0.85f, 0.6f));

            // 1. pencil bridge over the first pit: five pencils end to end (7 long each) at the lane's centre line
            for (int i = 0; i < 5; i++) Rest(Spawn("Pencil_01", "Pencil_" + i, r3, new Vector3(37f + 3.5f + i * 7f, 0f, z), new Vector3(0, 90, 0), 1f), 0f, null);
            // 2. hop gap at 100..103.5, then a checkpoint
            Checkpoint("CP_afterHop", r3, new Vector3(112f, 2f, z), new Vector3(6f, 4f, w));
            // 3. book stairs up onto a 12-high box and down the far side (each riser 1.6)
            var box = Rest(Spawn("Cardboard_Box_06", "StepBox", r3, new Vector3(142f, 0f, z), Vector3.zero, 1f), 0f, null);
            float boxTop = B(box).max.y;
            BookStairs(r3, new Vector3(128f, 0f, z), Vector3.right, 0f, boxTop, "UpStairs");
            BookStairs(r3, new Vector3(B(box).max.x + 1f, 0f, z), Vector3.right, boxTop, 0f, "DownStairs");
            // 4. three pins standing in the lane, walk between them (no hazard yet: dressing + a squeeze)
            foreach (float px in new[] { 180f, 190f, 200f })
            {
                var pin = Rest(Spawn("Pin_01", "Pin", r3, new Vector3(px, 0f, z + ((px / 10f) % 2 == 0 ? 3f : -3f)), new Vector3(-90, 0, 0), 1f), 0f, null);
                var hz = Trigger("Hazard", pin.transform, B(pin).center, new Vector3(1.6f, B(pin).size.y, 1.6f)); hz.AddComponent<Hazard>();
            }
            // 5. sprint gap 230..234.5 with a checkpoint before and after
            Checkpoint("CP_beforeSprint", r3, new Vector3(222f, 2f, z), new Vector3(6f, 4f, w));
            Checkpoint("CP_afterSprint", r3, new Vector3(244f, 2f, z), new Vector3(6f, 4f, w));
            // dressing: a lantern, chalk, a leaning CD, clips
            Rest(Spawn("Night_Lamp_02", "Lantern", r3, new Vector3(160f, 0f, z + 6f), Vector3.zero, 1f), 0f, null);
            Rest(Spawn("Chalk_01", "Chalk", r3, new Vector3(20f, 0f, z - 6f), new Vector3(0, 30, 0), 1f), 0f, null);
            Rest(Spawn("Clip_01", "Clip", r3, new Vector3(115f, 0f, z + 7f), new Vector3(0, 70, 0), 1f), 0f, null);
            Rest(Spawn("Clip_02", "Clip2", r3, new Vector3(260f, 0f, z - 7f), new Vector3(0, 20, 0), 1f), 0f, null);
            // exit: a glow on the east wall, a point light, the portal
            var q = Quad("ExitGlow", r3, new Vector3(x1 - 0.1f, 3f, z), new Vector3(0, -90, 0), new Vector3(6f, 6f, 1f), glow);
            PointLight("ExitLight", r3, new Vector3(x1 - 6f, 4f, z), 20f, 2.5f, new Color(1f, 0.85f, 0.55f));
            var exit = Trigger("ExitPortal", r3, new Vector3(x1 - 2f, 3f, z), new Vector3(2f, 6f, 8f));
            var portal = exit.AddComponent<Portal>(); Set(portal, "requiredSize", (int)ShiftSize.Toy);
            // start point (where the toy door drops you), facing east
            var start = Point("Start", r3, new Vector3(x0 + 6f, 0f, z), 90f);
            Checkpoint("CP_start", r3, new Vector3(x0 + 10f, 2f, z), new Vector3(6f, 4f, w));
            Log.AppendLine("R3 lane built x " + x0 + ".." + x1 + " at z " + z);
            return start;
        }

        // =====================================================================================
        // R4: the pack's own showroom (URP Demonstration.unity, its "Demonstration" root) moved into Greybox at C4 and turned
        // 180° so its window wall faces north, the garden side. Our beats are seated on its furniture, found by prefab name:
        // core B on the desk (Table_01) with the way down on its free side, the sandbag maze over the army camp, the exit hole
        // at the wardrobe's foot with the showroom's own Safe moved in front, the entry beside the real door, R6 on a window.
        const string DemoScene = Pack + "/Scenes/URP_Scenes/Demonstration.unity";
        const float ShowHalfX = 132.5f, ShowHalfZ = 165f;      // 4 x 5 tiles
        static readonly string[] Structural = { "Floor_", "Ceiling_", "Wall_", "Mat_", "Window_", "Curtain_", "Doorway_", "Socket_", "Panel_", "Poster_", "Drawing_", "Painting_", "Sticker_", "Wire_", "Bookshelf_", "Shelf_", "Clock_", "Planet_Lamp", "Basketball_Hoop" };

        static void BuildPlayroom(Transform r4, Transform r6, Material dark, Material grass, Material glow, Material coreMat, out Transform entry, out Transform wardrobeFront)
        {
            Vector3 c = C4; float nz = c.z + ShowHalfZ;
            entry = null; wardrobeFront = null;
            var show = Transplant(r4, c);
            if (show == null) return;
            PointLight("RoomLight", r4, c + new Vector3(0, 95f, 0), 520f, 2.2f, new Color(1f, 0.93f, 0.82f), true);

            // --- Workplace: core B on the desk; the way down for a toy on the desk's free side (the side away from the wall)
            var table = Demo(show, "Table_01"); if (table == null) { Log.AppendLine("showroom: no Table_01"); return; }
            var tb = B(table);
            bool freeWest = (tb.min.x - (c.x - ShowHalfX)) > ((c.x + ShowHalfX) - tb.max.x);
            float dir = freeWest ? -1f : 1f, face = freeWest ? tb.min.x : tb.max.x;
            Log.AppendLine("desk " + tb.min + ".." + tb.max + " free side " + (freeWest ? "west" : "east"));
            Clear(show, new Bounds(new Vector3(face + dir * 30f, 20f, tb.center.z), new Vector3(60f, 40f, tb.size.z + 24f)), table);
            var chair = Spawn("Chair_01", "Chair", r4, new Vector3(face + dir * 12f, 0, tb.center.z), new Vector3(0, freeWest ? -90 : 90, 0), 1f); Rest(chair, 0f, null);
            var cb = B(chair);
            // the way down from the desk (36) for a toy: a stack of two closed boxes (24) at the core's end of the desk, then one
            // closed box (12), then the floor: three drops of 12 under the 14 fall rule (every taller box in the pack has open flaps)
            Vector3 coreSpot = new Vector3(face - dir * 9f, tb.max.y + 2.5f, tb.min.z + 12f);
            var stackA = Rest(Spawn("Cardboard_Box_05", "Box12a", r4, new Vector3(face + dir * 9f, 0, coreSpot.z), new Vector3(0, 8, 0), 1f), 0f, null);
            var stackB = Rest(Spawn("Cardboard_Box_05", "Box24", r4, new Vector3(face + dir * 9f, 0, coreSpot.z), new Vector3(0, -6, 0), 1f), B(stackA).max.y, null);
            var box06 = Rest(Spawn("Cardboard_Box_06", "Box12", r4, new Vector3(face + dir * 25f, 0, coreSpot.z + 9f), new Vector3(0, -15, 0), 1f), 0f, null);
            var ruler = Rest(Spawn("Ruler_Big_01", "Ruler", r4, new Vector3(face + dir * 36f, 0, tb.min.z - 4f), new Vector3(0, 35, 0), 1f), 0f, null);
            var rc = ruler.AddComponent<Carryable>(); Set(rc, "gripOffset", new Vector3(0, 0, -6f)); Set(rc, "plank", true);
            ruler.AddComponent<Carried>();   // kid-only pick-up prompt
            Clear(show, new Bounds(new Vector3(coreSpot.x, tb.max.y + 6f, coreSpot.z), new Vector3(18f, 12f, 20f)), table);   // the desk top around it
            var coreB = Core("CoreB_Shrink", r4, coreSpot, ShiftSource.Mode.ShrinkOnly, coreMat, false);
            Checkpoint("CP_table", r4, new Vector3(tb.center.x, tb.max.y + 3f, tb.center.z), new Vector3(tb.size.x - 4f, 6f, tb.size.z - 4f), ShiftSize.Toy);

            // --- Toy Army: the sandbag maze with core A in the middle, over the showroom's army camp (its tents and tanks go, its
            //     sandbags, brick walls, wire, ladders and soldiers around it stay)
            Vector3 mz = c + new Vector3(55f, 0, -30f);
            Clear(show, new Bounds(mz + Vector3.up * 15f, new Vector3(84f, 30f, 84f)));
            SandbagRing(r4, mz, 36f, 90f);      // outer ring, gap on the south side
            SandbagRing(r4, mz, 18f, 270f);     // inner ring, gap on the north side
            var coreA = Core("CoreA_Grow", r4, mz + new Vector3(0, 2.5f, 0), ShiftSource.Mode.GrowOnly, coreMat, false);
            Rest(Spawn("Army_WatchTower_01", "WatchTower", r4, mz + new Vector3(0, 0, -46f), Vector3.zero, 1f), 0f, null);
            Rest(Spawn("Army_Tent_01", "Tent", r4, mz + new Vector3(-50f, 0, 10f), new Vector3(0, 30, 0), 1f), 0f, null);
            Rest(Spawn("Army_Tent_02", "Tent2", r4, mz + new Vector3(48f, 0, -20f), new Vector3(0, -40, 0), 1f), 0f, null);
            foreach (var (n, off) in new[] { ("Army_Soldier_01", new Vector3(-44f, 0, -30f)), ("Army_Soldier_02", new Vector3(44f, 0, 30f)), ("Army_Soldier_03", new Vector3(-30f, 0, 44f)), ("Army_Soldier_04", new Vector3(30f, 0, -44f)) })
                Rest(Spawn(n, n, r4, mz + off, new Vector3(0, Random.Range(0, 360), 0), 1f), 0f, null);

            // --- Wardrobe: doors as Door components; the exit hole at its foot on the room side, the showroom's Safe moved in front
            var wardrobe = Demo(show, "Wardrobe_01"); if (wardrobe == null) { Log.AppendLine("showroom: no Wardrobe_01"); return; }
            var wb = B(wardrobe);
            bool roomEast = wb.center.x < c.x; float wdir = roomEast ? 1f : -1f, wface = roomEast ? wb.max.x : wb.min.x;
            int doorCount = 0;
            foreach (Transform ch in wardrobe.GetComponentsInChildren<Transform>()) if (ch != wardrobe.transform && ch.name.Contains("Door")) { var d = ch.gameObject.AddComponent<Door>(); Set(d, "openAngle", ch.name.Contains("Left") ? -110f : 110f); Set(d, "duration", 0.9f); doorCount++; }
            Log.AppendLine("wardrobe doors found: " + doorCount + ", room side " + (roomEast ? "east" : "west"));
            var safe = Demo(show, "Safe_01");
            Clear(show, new Bounds(new Vector3(wface + wdir * 20f, 10f, wb.center.z), new Vector3(40f, 20f, 44f)), wardrobe, safe);
            Quad("HoleGlow", r4, new Vector3(wface + wdir * 0.2f, 1.8f, wb.center.z), new Vector3(0, roomEast ? -90 : 90, 0), new Vector3(1.6f, 3.6f, 1f), glow);
            var holeTrig = Trigger("WardrobeHole", r4, new Vector3(wface + wdir * 1.2f, 1.6f, wb.center.z), new Vector3(2f, 3f, 2f));
            var hp = holeTrig.AddComponent<Portal>(); Set(hp, "requiredSize", (int)ShiftSize.Toy);
            PointLight("HoleLight", r4, new Vector3(wface + wdir * 4f, 2.5f, wb.center.z), 16f, 2f, new Color(1f, 0.85f, 0.55f));
            if (safe == null) safe = Spawn("Safe_01", "Safe", r4, Vector3.zero, Vector3.zero, 1f);
            safe.name = "Safe"; safe.transform.rotation = Quaternion.Euler(0, roomEast ? -90 : 90, 0);
            safe.transform.position = new Vector3(wface + wdir * 9f, 0, wb.center.z); Rest(safe, 0f, null);
            var sc = safe.GetComponent<Carryable>() ?? safe.AddComponent<Carryable>(); Set(sc, "gripOffset", new Vector3(0, -4f, 6f));
            if (safe.GetComponent<Carried>() == null) safe.AddComponent<Carried>();
            wardrobeFront = Point("WardrobeFront", r4, new Vector3(wface + wdir * 40f, 0, wb.center.z), roomEast ? 90f : -90f);

            // --- the entry from R3: a mouse hole in the east wall, south of the real door
            Vector3 ep = new Vector3(c.x + ShowHalfX - 8f, 0, c.z - 127f);
            Clear(show, new Bounds(ep + Vector3.up * 8f, new Vector3(32f, 16f, 30f)));
            entry = Point("Entry", r4, ep, -90f);
            Quad("EntryGlow", r4, new Vector3(c.x + ShowHalfX - 0.3f, 1.8f, ep.z), new Vector3(0, 90, 0), new Vector3(1.6f, 3.6f, 1f), glow);
            Checkpoint("CP_entry", r4, ep + Vector3.up * 2f, new Vector3(10f, 4f, 10f), ShiftSize.Toy);

            // ---- R6: the showroom window farthest from the entry (the west one), a sill, the last core, the goal, the garden
            var win = Demo(show, "Window_01", c + new Vector3(-65f, 43f, ShowHalfZ));
            if (win == null) { Log.AppendLine("showroom: no Window_01"); return; }
            var nb = B(win); float sillY = Mathf.Max(nb.min.y, 40f);
            Log.AppendLine("window " + nb.min + ".." + nb.max + " sill " + sillY);
            Cube("Sill", r6, new Vector3(nb.center.x, sillY - 2f, nz - 2.5f - 6f), new Vector3(44f, 4f, 12f), dark);
            var coreD = Core("CoreD_Window", r6, new Vector3(nb.center.x - 12f, sillY + 2.5f, nz - 2.5f - 6f), ShiftSource.Mode.Toggle, coreMat, false);
            var goal = Trigger("Goal", r6, new Vector3(nb.center.x + 8f, sillY + 2f, nz - 2.5f - 6f), new Vector3(16f, 6f, 10f));
            var gc = goal.AddComponent<Goal>(); Set(gc, "requiredSize", (int)ShiftSize.Toy);
            Set(gc, "message", "");
            var flow = BuildHud();
            if (flow != null) { var gso = new SerializedObject(gc); var calls = gso.FindProperty("onReached.m_PersistentCalls.m_Calls"); calls.arraySize = 1; Listener(calls.GetArrayElementAtIndex(0), flow, typeof(GameFlow).AssemblyQualifiedName, "End"); gso.ApplyModifiedPropertiesWithoutUndo(); }
            Cube("Ground", r6, new Vector3(c.x + 220f, -1.5f, nz + 400f), new Vector3(1000f, 2f, 800f), grass);
            var rnd = new System.Random(7);
            for (int i = 0; i < 24; i++)
            {
                float tx = c.x - 220f + (float)(rnd.NextDouble() * 880), tz = nz + 60f + (float)(rnd.NextDouble() * 600);
                var tree = Spawn("Toy_Tree_01", "GardenTree", r6, new Vector3(tx, 0, tz), new Vector3(0, (float)(rnd.NextDouble() * 360), 0), 35f); Rest(tree, 0f, null);
            }
            PointLight("GardenSun", r6, new Vector3(c.x, 200f, nz + 250f), 900f, 2.5f, new Color(1f, 0.95f, 0.85f));
            Log.AppendLine("R4 showroom + R6 window built at " + c);
        }

        /// <summary>Opens the pack's showroom scene beside ours, moves its "Demonstration" root into our scene under r4 at c, turned
        /// 180°, and closes the showroom again. Its renderers forget the showroom's lightmaps and its lights go realtime, so the
        /// room lights the way our scene does.</summary>
        static Transform Transplant(Transform r4, Vector3 c)
        {
            var main = r4.gameObject.scene;
            var demo = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);
            GameObject root = null;
            foreach (var go in demo.GetRootGameObjects()) if (go.name == "Demonstration") root = go;
            if (root == null) { Log.AppendLine("showroom: no Demonstration root in " + DemoScene); EditorSceneManager.CloseScene(demo, true); return null; }
            SceneManager.MoveGameObjectToScene(root, main);
            EditorSceneManager.CloseScene(demo, true);
            root.name = "Showroom"; root.transform.SetParent(r4, true);
            root.transform.SetPositionAndRotation(c, Quaternion.Euler(0, 180f, 0));
            int lights = 0, renderers = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { r.lightmapIndex = -1; r.realtimeLightmapIndex = -1; renderers++; }
            foreach (var l in root.GetComponentsInChildren<Light>(true)) { l.lightmapBakeType = LightmapBakeType.Realtime; lights++; }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            Log.AppendLine("showroom: " + root.transform.childCount + " things, " + renderers + " renderers, " + lights + " lights moved in");
            return root.transform;
        }

        static string SourceName(GameObject go) { var src = PrefabUtility.GetCorrespondingObjectFromSource(go); return src != null ? src.name : go.name; }

        /// <summary>A direct child of the showroom made from the named prefab; with a point, the nearest such child.</summary>
        static GameObject Demo(Transform show, string prefab, Vector3? nearest = null)
        {
            GameObject best = null; float bd = float.MaxValue;
            foreach (Transform ch in show)
            {
                if (SourceName(ch.gameObject) != prefab) continue;
                if (nearest == null) return ch.gameObject;
                float d = (ch.position - nearest.Value).sqrMagnitude; if (d < bd) { bd = d; best = ch.gameObject; }
            }
            return best;
        }

        /// <summary>Removes the showroom's loose things whose bounds touch the box, keeping the room itself (floor, walls, what hangs
        /// on them) and whatever is passed in keep.</summary>
        static int Clear(Transform show, Bounds box, params GameObject[] keep)
        {
            var doomed = new List<GameObject>();
            foreach (Transform ch in show)
            {
                var go = ch.gameObject; bool kept = false; foreach (var g in keep) if (g == go) kept = true; if (kept) continue;
                string n = SourceName(go); bool structural = false; foreach (var p in Structural) if (n.StartsWith(p)) structural = true; if (structural) continue;
                var b = B(go); if (b.size == Vector3.zero) continue;
                if (b.Intersects(box)) doomed.Add(go);
            }
            foreach (var go in doomed) Object.DestroyImmediate(go);
            Log.AppendLine("cleared " + doomed.Count + " at " + box.center + " size " + box.size);
            return doomed.Count;
        }

        // =====================================================================================
        // Stills: a look at R4 without opening the editor. Batch: -executeMethod ShiftJam.CourseBuilder.BuildAndRender (needs a GPU:
        // no -nographics). Files land in docs/stills.
        public static void BuildAndRender() { BuildAll(); Stills(); }

        [MenuItem("Shift Jam/Render R4 Stills")]
        public static void Stills()
        {
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            Vector3 c = C4; string dir = "docs/stills"; System.IO.Directory.CreateDirectory(dir);
            var ceilings = new List<GameObject>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.gameObject.name.StartsWith("Ceiling") && r.gameObject.activeSelf && (r.transform.position - c).magnitude < 400f) { ceilings.Add(r.gameObject); r.gameObject.SetActive(false); }
            try
            {
                Shot(dir + "/r4_top.png", c + new Vector3(0, 300f, 0), Quaternion.Euler(90, 0, 0), 0f, 1400, 1700, 175f);
                Vector3 eye1 = c + new Vector3(110f, 60f, -120f); Shot(dir + "/r4_from_entry.png", eye1, Quaternion.LookRotation(c + new Vector3(-60f, 5f, 30f) - eye1), 70f, 1600, 900);
                Vector3 eye2 = c + new Vector3(-65f, 90f, 150f); Shot(dir + "/r4_from_window.png", eye2, Quaternion.LookRotation(c + new Vector3(50f, 0f, -90f) - eye2), 70f, 1600, 900);
                Vector3 eye3 = c + new Vector3(118f, 2.4f, -127f); Shot(dir + "/r4_toy_eye.png", eye3, Quaternion.LookRotation(new Vector3(-1f, -0.04f, 0.35f)), 70f, 1600, 900);
                Vector3 eye4 = c + new Vector3(40f, 70f, 60f); Shot(dir + "/r4_desk.png", eye4, Quaternion.LookRotation(c + new Vector3(100f, 30f, 107f) - eye4), 60f, 1600, 900);
                var book = new GameObject("LookBook").transform; float lx = c.x - 120f, lz = c.z + 260f;
                string[] cands = { "Cardboard_Box_01", "Cardboard_Box_02", "Cardboard_Box_03", "Cardboard_Box_04", "Cardboard_Box_05", "Cardboard_Box_06", "Chair_01", "Paper_Block_01", "Matchbox_01", "Wooden_Cube_01" };
                var labels = new System.Text.StringBuilder();
                foreach (var n in cands) { var g = Rest(Spawn(n, n, book, new Vector3(lx, 0, lz), Vector3.zero, 1f), 0f, null); var gb = B(g); labels.Append(n + " " + gb.size.x.ToString("0.0") + "x" + gb.size.y.ToString("0.0") + "x" + gb.size.z.ToString("0.0") + "; "); lx += gb.size.x + 8f; }
                Debug.Log("LOOKBOOK " + labels);
                Vector3 eye6 = new Vector3(c.x - 120f + (lx - (c.x - 120f)) * 0.5f, 42f, lz - 90f); Shot(dir + "/lookbook_boxes.png", eye6, Quaternion.LookRotation(new Vector3(c.x - 120f + (lx - (c.x - 120f)) * 0.5f, 8f, lz) - eye6), 55f, 1600, 700);
                Object.DestroyImmediate(book.gameObject);
                Vector3 eye5 = c + new Vector3(-30f, 40f, -100f); Shot(dir + "/r4_wardrobe.png", eye5, Quaternion.LookRotation(c + new Vector3(-105f, 15f, -128f) - eye5), 60f, 1600, 900);
            }
            finally { foreach (var g in ceilings) g.SetActive(true); }
            Debug.Log("STILLS DONE " + dir);
        }

        static void Shot(string file, Vector3 at, Quaternion facing, float fov, int w, int h, float ortho = 0f)
        {
            var holder = new GameObject("StillCam"); var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            try
            {
                var cam = holder.AddComponent<Camera>(); cam.transform.SetPositionAndRotation(at, facing);
                cam.nearClipPlane = 0.5f; cam.farClipPlane = 2000f; cam.targetTexture = rt;
                if (ortho > 0f) { cam.orthographic = true; cam.orthographicSize = ortho; } else cam.fieldOfView = fov;
                var data = cam.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = prev;
                System.IO.File.WriteAllBytes(file, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            }
            finally { Object.DestroyImmediate(holder); rt.Release(); Object.DestroyImmediate(rt); }
        }


        static Transform BuildWardrobe(Transform r5, Material wood, Material glow, Material coreMat)
        {
            Vector3 c = C5; float hw = 97.5f, hd = 60f, h = 170f;
            Cube("Floor", r5, c + new Vector3(0, -0.5f, 0), new Vector3(hw * 2, 1f, hd * 2), wood);
            Cube("Ceiling", r5, c + new Vector3(0, h + 0.5f, 0), new Vector3(hw * 2, 1f, hd * 2), wood);
            Cube("WallBack", r5, c + new Vector3(0, h / 2f, hd + 0.5f), new Vector3(hw * 2, h, 1f), wood);
            Cube("WallFront", r5, c + new Vector3(0, h / 2f, -hd - 0.5f), new Vector3(hw * 2, h, 1f), wood);   // the doors, from inside
            Cube("WallL", r5, c + new Vector3(-hw - 0.5f, h / 2f, 0), new Vector3(1f, h, hd * 2 + 2f), wood);
            Cube("WallR", r5, c + new Vector3(hw + 0.5f, h / 2f, 0), new Vector3(1f, h, hd * 2 + 2f), wood);
            // three shelves along the back half, 40 apart; the climb zigzags up them on book stairs
            float[] shelfY = { 40f, 80f };
            Cube("Shelf_" + shelfY[0], r5, c + new Vector3(0, shelfY[0] - 1f, hd / 2f), new Vector3(hw * 2, 2f, hd), wood);               // full depth
            Cube("Shelf_" + shelfY[1], r5, c + new Vector3(0, shelfY[1] - 1f, hd * 0.75f), new Vector3(hw * 2, 2f, hd / 2f), wood);         // back half only: open air above shelf 1's front
            // flight 0: floor -> shelf 1, running +x along the back wall from the left
            BookStairs(r5, c + new Vector3(-hw + 6f, 0, hd * 0.5f), Vector3.right, 0f, shelfY[0], "Flight0");
            // flight 1 is a trampoline: a pillow on shelf 1's open front, under the lip of shelf 2; drift back in the air to land on it
            var pillow = Rest(Spawn("Pillow_01", "Trampoline", r5, c + new Vector3(hw - 30f, 0, hd * 0.2f), new Vector3(0, 10, 0), 1f), shelfY[0], null);
            var pb = B(pillow);
            var padTrig = Trigger("Pad", pillow.transform, new Vector3(pb.center.x, pb.max.y + 0.6f, pb.center.z), new Vector3(pb.size.x * 0.8f, 1.6f, pb.size.z * 0.8f));
            var pad = padTrig.AddComponent<LaunchPad>(); Set(pad, "height", (shelfY[1] - pb.max.y) + 10f); Set(pad, "visual", pillow.transform);
            Quad("PadGlow", pillow.transform, new Vector3(pb.center.x, pb.max.y + 0.3f, pb.center.z), new Vector3(90, 0, 0), Vector3.one * (pb.size.x * 0.9f), glow);
            Checkpoint("CP_shelf" + shelfY[0], r5, c + new Vector3(0, shelfY[0] + 2f, hd * 0.5f), new Vector3(hw * 2 - 10f, 4f, hd - 4f), ShiftSize.Toy);
            Checkpoint("CP_shelf" + shelfY[1], r5, c + new Vector3(0, shelfY[1] + 2f, hd * 0.75f), new Vector3(hw * 2 - 10f, 4f, hd / 2f - 4f), ShiftSize.Toy);
            // dressing: boxes and a pillow on the shelves, costumes hanging in the front half (static)
            Rest(Spawn("Cardboard_Box_02", "Box18", r5, c + new Vector3(30f, 0, hd * 0.5f), new Vector3(0, 15, 0), 1f), shelfY[0], null);
            Rest(Spawn("Cardboard_Box_06", "Box12", r5, c + new Vector3(-40f, 0, hd * 0.75f), new Vector3(0, -20, 0), 1f), shelfY[1], null);
            PointLight("DimLamp", r5, c + new Vector3(0, shelfY[1] + 10f, hd * 0.3f), 60f, 0.5f, new Color(1f, 0.8f, 0.55f));
            // the flashlight on shelf 2, lying on its side with the head pointing up at the top shelf's core, switch to the front
            var torch = Spawn("Flashlight_01", "WardrobeTorch", r5, c + new Vector3(-20f, 0, hd * 0.72f), Vector3.zero, 1f);
            // lying on shelf 2 with the head (+Z) along +x toward core C and the switch (+Y) rolled to face the front (-z)
            torch.transform.rotation = Quaternion.AngleAxis(90f, Vector3.up) * Quaternion.AngleAxis(-90f, Vector3.forward);
            Rest(torch, shelfY[1], null);
            var mf = torch.GetComponentInChildren<MeshFilter>(); Bounds mb = mf.sharedMesh.bounds;
            var beamGo = new GameObject("Beam"); beamGo.transform.SetParent(torch.transform, false); beamGo.transform.localPosition = new Vector3(mb.center.x, mb.center.y, mb.max.z + 0.4f);
            var beam = beamGo.AddComponent<Light>(); beam.type = LightType.Spot; beam.spotAngle = 50f; beam.innerSpotAngle = 30f; beam.range = 90f; beam.intensity = 4f; beam.shadows = LightShadows.Soft; beam.enabled = false;
            var carry = torch.AddComponent<Carryable>();
            var fl = torch.AddComponent<Flashlight>(); Set(fl, "beam", beam); Set(fl, "startOn", false); Set(fl, "toyUsesE", false);
            // the push switch: a trigger over the bump with a Safe_Button cap that slides
            var sw = new GameObject("PushSwitch"); sw.transform.SetParent(torch.transform, false); sw.transform.localPosition = new Vector3(0, 1.21f, -0.25f); sw.transform.localRotation = Quaternion.identity;
            var swc = sw.AddComponent<BoxCollider>(); swc.isTrigger = true; swc.size = new Vector3(2.2f, 1.4f, 2.2f); swc.center = new Vector3(0, 0.3f, 0);
            var cap = Spawn("Safe_Button_01", "Cap", sw.transform, Vector3.zero, Vector3.zero, 1.2f); cap.transform.localPosition = new Vector3(0, 0.15f, 0); cap.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            foreach (var col in cap.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            var ps = sw.AddComponent<PushSwitch>(); Set(ps, "cap", cap.transform); Set(ps, "travel", 0.25f);
            { var so = new SerializedObject(ps); var calls = so.FindProperty("onPressed.m_PersistentCalls.m_Calls"); calls.arraySize = 1; Listener(calls.GetArrayElementAtIndex(0), fl, typeof(Flashlight).AssemblyQualifiedName, "Toggle"); so.ApplyModifiedPropertiesWithoutUndo(); }
            // core C on the top shelf, dormant until lit
            var coreC = Core("CoreC_Grow", r5, c + new Vector3(80f, shelfY[1] + 2.5f, hd * 0.72f), ShiftSource.Mode.GrowOnly, coreMat, true);
            coreC.AddComponent<LightSensor>();
            // the way out once you are a kid: a portal at the top (kid size) back to the playroom, in front of the wardrobe
            var topTrig = Trigger("TopPortal", r5, c + new Vector3(0, shelfY[1] + 40f, 0), new Vector3(hw * 2, 60f, hd * 2));
            var tp = topTrig.AddComponent<Portal>(); Set(tp, "requiredSize", (int)ShiftSize.Kid); Set(tp, "cooldown", 2f);
            var start = Point("Start", r5, c + new Vector3(-hw + 10f, 0, -hd * 0.3f), 90f);
            Checkpoint("CP_start", r5, c + new Vector3(-hw + 14f, 2f, -hd * 0.3f), new Vector3(8f, 4f, 20f), ShiftSize.Toy);
            Quad("EntryGlow", r5, c + new Vector3(-hw + 0.2f, 1.8f, -hd * 0.3f), new Vector3(0, -90, 0), new Vector3(1.6f, 3.6f, 1f), glow);
            Log.AppendLine("R5 wardrobe built at " + c + " (torch beam -> core C: " + Vector3.Angle(beamGo.transform.forward, coreC.transform.position - beamGo.transform.position).ToString("F0") + " deg off axis)");
            return start;
        }

        // =====================================================================================
        // helpers
        static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static Transform Point(string name, Transform parent, Vector3 pos, float yaw) { var t = Child(parent, name); t.position = pos; t.rotation = Quaternion.Euler(0, yaw, 0); return t; }

        static GameObject Spawn(string prefabName, string name, Transform parent, Vector3 pos, Vector3 euler, float scale)
        {
            string[] guids = AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { Pack });
            string path = null;
            foreach (var g in guids) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p) == prefabName) { path = p; break; } }
            if (path == null) { Log.AppendLine("MISSING prefab " + prefabName); var dummy = new GameObject(name + "_MISSING"); dummy.transform.SetParent(parent, false); dummy.transform.position = pos; return dummy; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            go.name = name; go.transform.localScale = Vector3.one * scale; go.transform.rotation = Quaternion.Euler(euler); go.transform.position = pos;
            return go;
        }

        static Bounds B(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            bool any = false; Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in rs) { if (r is ParticleSystemRenderer) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return b;
        }

        /// <summary>Drops the thing so its lowest point sits on y (or on the surface found below it, if a mask is given).</summary>
        static GameObject Rest(GameObject go, float y, object _)
        {
            var b = B(go); go.transform.position += Vector3.up * (y - b.min.y); return go;
        }
        static GameObject RestOn(GameObject go, float y) => Rest(go, y, null);

        static GameObject Cube(string name, Transform parent, Vector3 centre, Vector3 size, Material m)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube); c.name = name; c.transform.SetParent(parent, false); c.transform.position = centre; c.transform.localScale = size;
            if (m != null) c.GetComponent<Renderer>().sharedMaterial = m; return c;
        }
        static GameObject Quad(string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = name; Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false); q.transform.position = pos; q.transform.rotation = Quaternion.Euler(euler); q.transform.localScale = scale;
            if (m != null) q.GetComponent<Renderer>().sharedMaterial = m; return q;
        }
        static GameObject Trigger(string name, Transform parent, Vector3 centre, Vector3 size)
        {
            var t = new GameObject(name); t.transform.SetParent(parent, false); t.transform.position = centre;
            var bc = t.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size; return t;
        }
        static void Checkpoint(string name, Transform parent, Vector3 centre, Vector3 size, ShiftSize? size1 = null)
        {
            var t = Trigger(name, parent, centre, size); var cp = t.AddComponent<Checkpoint>();
            if (size1.HasValue) { Set(cp, "anySize", false); Set(cp, "requiredSize", (int)size1.Value); }
            // a small AMBER dot at the trigger's floor that lights when claimed (cores are cyan: never the same look)
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/CheckpointGlow.mat");
            if (glowMat == null) { glowMat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/OrbGlow.mat")); glowMat.SetColor("_BaseColor", new Color(1f, 0.72f, 0.25f, 0.9f)); AssetDatabase.CreateAsset(glowMat, "Assets/_Game/Data/CheckpointGlow.mat"); }
            var marker = Quad("Marker", t.transform, centre + Vector3.up * (0.8f - size.y * 0.5f), Vector3.zero, Vector3.one * 1.4f, glowMat);
            marker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; marker.AddComponent<Billboard>();
            PointLight("MarkerLight", marker.transform, marker.transform.position, 4f, 0.5f, new Color(1f, 0.75f, 0.3f));
            marker.SetActive(false);
            Set(cp, "litWhenClaimed", marker);
        }

        /// <summary>The HUD frame: a full-screen fade image, a start card and an end card (TMP), GameFlow to run them.</summary>
        static GameFlow BuildHud()
        {
            var hud = GameObject.Find("HUD"); if (hud == null) { Log.AppendLine("no HUD canvas"); return null; }
            foreach (var n in new[] { "Fade", "StartCard", "EndCard" }) { var o = hud.transform.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
            var fadeGo = new GameObject("Fade", typeof(RectTransform)); fadeGo.transform.SetParent(hud.transform, false);
            var frt = fadeGo.GetComponent<RectTransform>(); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            var img = fadeGo.AddComponent<UnityEngine.UI.Image>(); img.color = new Color(0, 0, 0, 0); img.raycastTarget = false;
            var fade = fadeGo.AddComponent<ScreenFade>();
            GameObject Card(string name, string title, string sub)
            {
                var card = new GameObject(name, typeof(RectTransform)); card.transform.SetParent(hud.transform, false);
                var rt = card.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var t1 = new GameObject("Title", typeof(RectTransform)); t1.transform.SetParent(card.transform, false);
                var r1 = t1.GetComponent<RectTransform>(); r1.anchorMin = new Vector2(0.5f, 0.5f); r1.anchorMax = new Vector2(0.5f, 0.5f); r1.sizeDelta = new Vector2(1400f, 200f); r1.anchoredPosition = new Vector2(0, 60f);
                var tx = t1.AddComponent<TMPro.TextMeshProUGUI>(); tx.text = title; tx.fontSize = 120f; tx.alignment = TMPro.TextAlignmentOptions.Center; tx.color = Color.white;
                var t2 = new GameObject("Sub", typeof(RectTransform)); t2.transform.SetParent(card.transform, false);
                var r2 = t2.GetComponent<RectTransform>(); r2.anchorMin = new Vector2(0.5f, 0.5f); r2.anchorMax = new Vector2(0.5f, 0.5f); r2.sizeDelta = new Vector2(1400f, 100f); r2.anchoredPosition = new Vector2(0, -60f);
                var sx = t2.AddComponent<TMPro.TextMeshProUGUI>(); sx.text = sub; sx.fontSize = 40f; sx.alignment = TMPro.TextAlignmentOptions.Center; sx.color = new Color(1, 1, 1, 0.85f);
                return card;
            }
            var start = Card("StartCard", "SHIFT", "press any key");
            var end = Card("EndCard", "You made it to the window.", "R to play again");
            end.SetActive(false);
            var flow = hud.GetComponent<GameFlow>() ?? hud.AddComponent<GameFlow>();
            Set(flow, "startCard", start); Set(flow, "endCard", end); Set(flow, "fade", fade);
            Log.AppendLine("HUD: fade + cards + GameFlow");
            return flow;
        }
        static Light PointLight(string name, Transform parent, Vector3 pos, float range, float intensity, Color colour, bool shadows = false)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = pos;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = colour; l.shadows = shadows ? LightShadows.Soft : LightShadows.None; return l;
        }
        static GameObject Core(string name, Transform parent, Vector3 pos, ShiftSource.Mode mode, Material coreMat, bool dormant)
        {
            var core = Spawn("Wooden_Sphere_01", name, parent, pos, Vector3.zero, 1f);
            var r = core.GetComponent<MeshRenderer>(); if (r != null) { r.sharedMaterial = coreMat; r.enabled = false; }
            var src = core.AddComponent<ShiftSource>(); Set(src, "mode", (int)mode); Set(src, "startDormant", dormant);
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/OrbGlow.mat"); var sparkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/Spark.mat");
            var glow = Quad("Glow", core.transform, pos, Vector3.zero, Vector3.one * 14f, glowMat); glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; glow.AddComponent<Billboard>();
            var sparksGo = new GameObject("Sparks"); sparksGo.transform.SetParent(core.transform, false);
            var ps = sparksGo.AddComponent<ParticleSystem>(); var main = ps.main; main.startLifetime = 1.4f; main.startSpeed = 1.2f; main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); main.startColor = new Color(0.6f, 1f, 1f); main.maxParticles = 60; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -0.02f;
            var em = ps.emission; em.rateOverTime = 10f; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 2.8f;
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.4f, 0.9f, 1f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) }); col.color = g;
            var pr = sparksGo.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = sparkMat; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var ember = Quad("Ember", core.transform, pos, Vector3.zero, Vector3.one * 5f, glowMat); ember.AddComponent<Billboard>();
            var el = PointLight("EmberLight", ember.transform, pos, 10f, 0.8f, new Color(0.5f, 0.9f, 1f));
            var so = new SerializedObject(src);
            var a = so.FindProperty("awakeOnly"); a.arraySize = 2; a.GetArrayElementAtIndex(0).objectReferenceValue = glow; a.GetArrayElementAtIndex(1).objectReferenceValue = sparksGo;
            var d = so.FindProperty("dormantOnly"); d.arraySize = 1; d.GetArrayElementAtIndex(0).objectReferenceValue = ember;
            so.ApplyModifiedPropertiesWithoutUndo();
            glow.SetActive(!dormant); sparksGo.SetActive(!dormant); ember.SetActive(dormant);
            return core;
        }
        /// <summary>A staircase of books from the floor (y0) up to y1 along dir: risers of at most 1.3 (a 2.0 hop with margin), 1.5 along, each book overlapping the last.</summary>
        static void BookStairs(Transform parent, Vector3 from, Vector3 dir, float y0, float y1, string name)
        {
            var holder = Child(parent, name); int n = Mathf.CeilToInt(Mathf.Abs(y1 - y0) / 1.3f); float step = (y1 - y0) / Mathf.Max(1, n);
            string[] books = { "Book_01", "Book_02", "Book_03", "Book_04", "Book_06", "Book_07", "Book_08", "Book_09", "Book_10", "Book_11", "Book_12" };
            float yaw = Quaternion.LookRotation(dir).eulerAngles.y;
            for (int i = 0; i <= n; i++)
            {
                var b = Spawn(books[i % books.Length], "Book" + i, holder, from + dir * (i * 1.5f), new Vector3(0, yaw, 0), 1f);
                var bb = B(b);
                b.transform.position += Vector3.up * ((y0 + i * step) - bb.min.y);
            }
        }
        static void SandbagRing(Transform parent, Vector3 centre, float half, float gapAngle)
        {
            // a square ring of Army_Sandbags_01 (9 long) with one side's middle piece left out (the gap faces gapAngle: 90 = south... pack dir)
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90f; Vector3 normal = Quaternion.Euler(0, yaw, 0) * Vector3.forward; Vector3 along = Quaternion.Euler(0, yaw, 0) * Vector3.right;
                int pieces = Mathf.RoundToInt(half * 2f / 9f);
                for (int i = 0; i < pieces; i++)
                {
                    bool middle = i == pieces / 2;
                    if (middle && Mathf.Abs(Mathf.DeltaAngle(yaw, gapAngle)) < 1f) continue;
                    Vector3 p = centre + normal * half + along * (-half + 4.5f + i * 9f);
                    Rest(Spawn("Army_Sandbags_01", "Sandbag", parent, p, new Vector3(0, yaw, 0), 1f), centre.y, null);
                }
            }
        }
        static Material Mat(string path, Color c)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", c); AssetDatabase.CreateAsset(m, path); }
            return m;
        }
        static void Set(Object target, string field, object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target); var p = so.FindProperty(field);
            if (p == null) { Log.AppendLine("no field " + field + " on " + target.GetType().Name); return; }
            switch (value)
            {
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
                case Vector3 v: p.vector3Value = v; break;
                case Object o: p.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Listener(SerializedProperty call, Object target, string typeName, string method)
        {
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeName;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
            call.FindPropertyRelative("m_Mode").intValue = 1;      // void
            call.FindPropertyRelative("m_CallState").intValue = 2; // runtime only
        }
    }
}
#endif
