#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        static readonly Vector3 C4 = new Vector3(480f, 0f, -140f);     // playroom centre; walls x 285..675, z -302.5..22.5
        const float R4HalfX = 195f, R4HalfZ = 162.5f;
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
        // R4: the playroom (+ R6 window and garden on its north wall)
        static void BuildPlayroom(Transform r4, Transform r6, Material dark, Material grass, Material glow, Material coreMat, out Transform entry, out Transform wardrobeFront)
        {
            Vector3 c = C4;
            // shell: 6x5 floor and ceiling tiles, Wall_01 pieces, pillars
            float[] xs = { -162.5f, -97.5f, -32.5f, 32.5f, 97.5f, 162.5f };
            float[] zs = { -130f, -65f, 0f, 65f, 130f };
            foreach (float x in xs) foreach (float z in zs)
            {
                Spawn("Floor_01", "Floor", r4, c + new Vector3(x, 0, z), Vector3.zero, 1f);
                Spawn("Ceiling_01", "Ceiling", r4, c + new Vector3(x, 130f, z), Vector3.zero, 1f);
            }
            foreach (float x in xs)
            {
                var s = Spawn("Wall_01", "Wall_S", r4, c + new Vector3(x, 0, -R4HalfZ), Vector3.zero, 1f); var bs = B(s); s.transform.position += new Vector3(0, -bs.min.y, (c.z - R4HalfZ) - bs.max.z);
                if (Mathf.Abs(x) > 1f)   // the north wall leaves the window bay (x -32.5 and 32.5 pieces) to the cubes below
                {
                    var n = Spawn("Wall_01", "Wall_N", r4, c + new Vector3(x, 0, R4HalfZ), new Vector3(0, 180, 0), 1f); var bn = B(n); n.transform.position += new Vector3(0, -bn.min.y, (c.z + R4HalfZ) - bn.min.z);
                }
            }
            // north wall centre: a 130-wide bay built from cubes with a 47x64 hole at y 55..119 for the window
            float nz = c.z + R4HalfZ + 2.5f;
            Cube("WallN_left", r4, c + new Vector3(-65f + (65f - 23.5f) / 2f, 65f, 0) + new Vector3(0, 0, nz - c.z), new Vector3(65f - 23.5f, 130f, 5f), dark);
            Cube("WallN_right", r4, c + new Vector3(65f - (65f - 23.5f) / 2f, 65f, 0) + new Vector3(0, 0, nz - c.z), new Vector3(65f - 23.5f, 130f, 5f), dark);
            Cube("WallN_below", r4, new Vector3(c.x, 27.5f, nz), new Vector3(47f, 55f, 5f), dark);
            Cube("WallN_above", r4, new Vector3(c.x, 124.5f, nz), new Vector3(47f, 11f, 5f), dark);
            foreach (float z in zs)
            {
                var w = Spawn("Wall_01", "Wall_W", r4, c + new Vector3(-R4HalfX, 0, z), new Vector3(0, 90, 0), 1f); var bw = B(w); w.transform.position += new Vector3((c.x - R4HalfX) - bw.max.x, -bw.min.y, 0);
                var e = Spawn("Wall_01", "Wall_E", r4, c + new Vector3(R4HalfX, 0, z), new Vector3(0, -90, 0), 1f); var be = B(e); e.transform.position += new Vector3((c.x + R4HalfX) - be.min.x, -be.min.y, 0);
            }
            foreach (float x in new[] { -R4HalfX - 2.5f, R4HalfX + 2.5f }) foreach (float z in new[] { -R4HalfZ - 2.5f, R4HalfZ + 2.5f })
            { var p = Spawn("Wall_05", "Pillar", r4, c + new Vector3(x, 0, z), Vector3.zero, 1f); var bp = B(p); p.transform.position += new Vector3(c.x + x - bp.center.x, -bp.min.y, c.z + z - bp.center.z); }
            PointLight("RoomLight", r4, c + new Vector3(0, 95f, 0), 520f, 3.5f, new Color(1f, 0.93f, 0.82f), true);
            Spawn("Planet_Lamp_01", "PlanetLamp", r4, c + new Vector3(0, 130f, 0), Vector3.zero, 1f);

            // --- Workplace (NW): table against the north wall, computer/keyboard/globe on it, chair at its south edge
            var table = Spawn("Table_01", "Table", r4, c + new Vector3(-140f, 0, 110f), Vector3.zero, 1f); var tb = B(table);
            table.transform.position += new Vector3(0, -tb.min.y, (c.z + R4HalfZ - 1f) - tb.max.z); tb = B(table);
            RestOn(Spawn("Computer_01", "Computer", r4, new Vector3(tb.min.x + 12f, 0, tb.center.z + 8f), new Vector3(0, 20, 0), 1f), tb.max.y);
            RestOn(Spawn("Keyboard_01", "Keyboard", r4, new Vector3(tb.center.x + 6f, 0, tb.center.z - 10f), new Vector3(0, -5, 0), 1f), tb.max.y);
            RestOn(Spawn("Globe_01", "Globe", r4, new Vector3(tb.max.x - 14f, 0, tb.center.z + 10f), Vector3.zero, 1f), tb.max.y);
            var chair = Spawn("Chair_01", "Chair", r4, new Vector3(tb.center.x, 0, tb.min.z - 9f), new Vector3(0, 180, 0), 1f); Rest(chair, 0f, null);
            var cb = B(chair);
            // the way down for a toy: chair (22) -> box 12 -> floor; the kid lays the ruler table -> box_04 (28) beside the chair
            var box04 = Rest(Spawn("Cardboard_Box_04", "Box28", r4, new Vector3(cb.max.x + 18f, 0, tb.min.z - 14f), new Vector3(0, 10, 0), 1f), 0f, null);
            var box06 = Rest(Spawn("Cardboard_Box_06", "Box12", r4, new Vector3(cb.min.x - 10f, 0, cb.center.z - 2f), new Vector3(0, -15, 0), 1f), 0f, null);
            var ruler = Rest(Spawn("Ruler_Big_01", "Ruler", r4, new Vector3(tb.max.x + 20f, 0, tb.min.z - 30f), new Vector3(0, 35, 0), 1f), 0f, null);
            var rc = ruler.AddComponent<Carryable>(); Set(rc, "gripOffset", new Vector3(0, 0, -6f)); Set(rc, "plank", true);
            ruler.AddComponent<Carried>();   // kid-only pick-up prompt
            // core B: shrink, on the table, near the south edge
            var coreB = Core("CoreB_Shrink", r4, new Vector3(tb.center.x - 20f, tb.max.y + 2.5f, tb.min.z + 10f), ShiftSource.Mode.ShrinkOnly, coreMat, false);
            Checkpoint("CP_table", r4, new Vector3(tb.center.x, tb.max.y + 3f, tb.center.z), new Vector3(tb.size.x - 4f, 6f, tb.size.z - 4f), ShiftSize.Toy);

            // --- Toy Army (NE): a sandbag maze with core A in the middle
            Vector3 mz = c + new Vector3(135f, 0, 95f);
            SandbagRing(r4, mz, 36f, 90f);      // outer ring, gap on the south side
            SandbagRing(r4, mz, 18f, 270f);     // inner ring, gap on the north side
            var coreA = Core("CoreA_Grow", r4, mz + new Vector3(0, 2.5f, 0), ShiftSource.Mode.GrowOnly, coreMat, false);
            Rest(Spawn("Army_WatchTower_01", "WatchTower", r4, mz + new Vector3(0, 0, -46f), Vector3.zero, 1f), 0f, null);
            Rest(Spawn("Army_Tent_01", "Tent", r4, mz + new Vector3(-50f, 0, 10f), new Vector3(0, 30, 0), 1f), 0f, null);
            Rest(Spawn("Army_Tent_02", "Tent2", r4, mz + new Vector3(48f, 0, -20f), new Vector3(0, -40, 0), 1f), 0f, null);
            foreach (var (n, off) in new[] { ("Army_Soldier_01", new Vector3(-44f, 0, -30f)), ("Army_Soldier_02", new Vector3(44f, 0, 30f)), ("Army_Soldier_03", new Vector3(-30f, 0, 44f)), ("Army_Soldier_04", new Vector3(30f, 0, -44f)) })
                Rest(Spawn(n, n, r4, mz + off, new Vector3(0, Random.Range(0, 360), 0), 1f), 0f, null);

            // --- centre: the racing track
            Rest(Spawn("RaceTrack_01", "RaceTrack", r4, c + new Vector3(0, 0, -20f), Vector3.zero, 1f), 0f, null);
            Rest(Spawn("Car_01", "Car", r4, c + new Vector3(-26f, 0, -40f), new Vector3(0, 60, 0), 1f), 0f, null);
            // --- Castle (S of centre)
            Vector3 cz = c + new Vector3(40f, 0, -110f);
            Rest(Spawn("Castle_01", "Castle", r4, cz, Vector3.zero, 1f), 0f, null);
            foreach (var (off, yaw) in new[] { (new Vector3(-16f, 0, 0), 90f), (new Vector3(16f, 0, 0), 90f), (new Vector3(0, 0, 16f), 0f), (new Vector3(0, 0, -16f), 0f) })
                Rest(Spawn("Castle_Wall_01", "CastleWall", r4, cz + off, new Vector3(0, yaw, 0), 1f), 0f, null);
            Rest(Spawn("Castle_Tower_01", "CastleTower", r4, cz + new Vector3(-16f, 0, 16f), Vector3.zero, 1f), 0f, null);
            Rest(Spawn("Castle_Tower_02", "CastleTower2", r4, cz + new Vector3(16f, 0, -16f), Vector3.zero, 1f), 0f, null);
            // --- Couch (E)
            var sofa = Spawn("Sofa_01", "Sofa", r4, c + new Vector3(170f, 0, 20f), new Vector3(0, -90, 0), 1f); var sb2 = B(sofa); sofa.transform.position += new Vector3((c.x + R4HalfX - 1f) - sb2.max.x, -sb2.min.y, 0);
            Rest(Spawn("Pillow_01", "Pillow", r4, c + new Vector3(160f, 0, 40f), new Vector3(0, 15, 0), 1f), B(sofa).max.y - 18f, null);
            // --- Hoop (W) + ball, near the entry
            var hoop = Spawn("Basketball_Hoop_01", "Hoop", r4, c + new Vector3(-180f, 0, -60f), new Vector3(0, 90, 0), 1f); var hb = B(hoop);
            hoop.transform.position += new Vector3((c.x - R4HalfX + 1f) - hb.min.x, 22f - hb.min.y, 0);
            Rest(Spawn("Ball_01", "Ball", r4, c + new Vector3(-150f, 0, -80f), Vector3.zero, 1f), 0f, null);
            // --- Wardrobe (SE) against the east wall, doors as Door components; the exit hole at its foot behind the Safe
            var wardrobe = Spawn("Wardrobe_01", "Wardrobe", r4, c + new Vector3(160f, 0, -125f), new Vector3(0, -90, 0), 1f); var wb = B(wardrobe);
            wardrobe.transform.position += new Vector3((c.x + R4HalfX - 1f) - wb.max.x, -wb.min.y, 0); wb = B(wardrobe);
            int doorCount = 0;
            foreach (Transform ch in wardrobe.GetComponentsInChildren<Transform>()) if (ch != wardrobe.transform && ch.name.Contains("Door")) { var d = ch.gameObject.AddComponent<Door>(); Set(d, "openAngle", ch.name.Contains("Left") ? -110f : 110f); Set(d, "duration", 0.9f); doorCount++; }
            Log.AppendLine("wardrobe doors found: " + doorCount);
            float holeX = wb.min.x - 2f;
            Quad("HoleGlow", r4, new Vector3(wb.min.x - 0.2f, 1.8f, wb.center.z), new Vector3(0, 90, 0), new Vector3(1.6f, 3.6f, 1f), glow);
            var holeTrig = Trigger("WardrobeHole", r4, new Vector3(wb.min.x - 1.2f, 1.6f, wb.center.z), new Vector3(2f, 3f, 2f));
            var hp = holeTrig.AddComponent<Portal>(); Set(hp, "requiredSize", (int)ShiftSize.Toy);
            PointLight("HoleLight", r4, new Vector3(wb.min.x - 4f, 2.5f, wb.center.z), 16f, 2f, new Color(1f, 0.85f, 0.55f));
            var safe = Rest(Spawn("Safe_01", "Safe", r4, new Vector3(wb.min.x - 9f, 0, wb.center.z), new Vector3(0, 90, 0), 1f), 0f, null);
            var sc = safe.AddComponent<Carryable>(); Set(sc, "gripOffset", new Vector3(0, -4f, 6f)); safe.AddComponent<Carried>();
            wardrobeFront = Point("WardrobeFront", r4, new Vector3(wb.min.x - 40f, 0, wb.center.z), 0f);   // facing north, toward the window
            // --- the entry from R3: west wall, at the lane's z
            entry = Point("Entry", r4, new Vector3(c.x - R4HalfX + 8f, 0, LaneZ), 90f);
            Quad("EntryGlow", r4, new Vector3(c.x - R4HalfX + 0.2f, 1.8f, LaneZ), new Vector3(0, -90, 0), new Vector3(1.6f, 3.6f, 1f), glow);
            // dressing: mats, blocks, drawings
            foreach (var (n, off, yaw) in new[] { ("Mat_01", new Vector3(-60f, 0, 60f), 0f), ("Mat_02", new Vector3(80f, 0, -60f), 90f), ("Mat_03", new Vector3(-100f, 0, -120f), 0f), ("Mat_04", new Vector3(120f, 0, 40f), 0f) })
                Rest(Spawn(n, n, r4, c + off, new Vector3(0, yaw, 0), 1f), 0f, null);
            for (int i = 1; i <= 4; i++) Rest(Spawn("Cube_Alphabet_0" + i, "Block" + i, r4, c + new Vector3(-80f + i * 9f, 0, -150f + (i % 2) * 6f), new Vector3(0, i * 25f, 0), 1f), 0f, null);
            foreach (var (n, x, yaw) in new[] { ("Drawing_01", -120f, 0f), ("Drawing_05", -60f, 0f), ("Poster_03", 90f, 0f) })
            { var dr = Spawn(n, n, r4, c + new Vector3(x, 75f, -R4HalfZ + 0.6f), new Vector3(0, yaw, 0), 1f); var db = B(dr); dr.transform.position += new Vector3(0, 0, (c.z - R4HalfZ + 0.3f) - db.min.z); }

            // ---- R6: the window on the north wall, a sill, the last core, the goal, and the garden outside
            var win = Spawn("Window_01", "Window", r6, new Vector3(c.x, 55f, nz), new Vector3(0, 180, 0), 1f); var nb = B(win); win.transform.position += new Vector3(0, 0, (nz - 2.5f - 4f) - nb.min.z);
            Cube("Sill", r6, new Vector3(c.x, 53f, nz - 2.5f - 6f), new Vector3(44f, 4f, 12f), dark);
            var coreD = Core("CoreD_Window", r6, new Vector3(c.x - 12f, 55f + 2.5f, nz - 2.5f - 6f), ShiftSource.Mode.Toggle, coreMat, false);
            var goal = Trigger("Goal", r6, new Vector3(c.x + 8f, 57f, nz - 2.5f - 6f), new Vector3(16f, 6f, 10f));
            var gc = goal.AddComponent<Goal>(); Set(gc, "requiredSize", (int)ShiftSize.Toy);
            Set(gc, "message", "");
            var flow = BuildHud();
            if (flow != null) { var gso = new SerializedObject(gc); var calls = gso.FindProperty("onReached.m_PersistentCalls.m_Calls"); calls.arraySize = 1; Listener(calls.GetArrayElementAtIndex(0), flow, typeof(GameFlow).AssemblyQualifiedName, "End"); gso.ApplyModifiedPropertiesWithoutUndo(); }
            Cube("Ground", r6, new Vector3(700f, -1.5f, nz + 400f), new Vector3(1000f, 2f, 800f), grass);
            var rnd = new System.Random(7);
            for (int i = 0; i < 24; i++)
            {
                float tx = 260f + (float)(rnd.NextDouble() * 880), tz = nz + 60f + (float)(rnd.NextDouble() * 600);
                var tree = Spawn("Toy_Tree_01", "GardenTree", r6, new Vector3(tx, 0, tz), new Vector3(0, (float)(rnd.NextDouble() * 360), 0), 35f); Rest(tree, 0f, null);
            }
            PointLight("GardenSun", r6, new Vector3(c.x, 200f, nz + 250f), 900f, 2.5f, new Color(1f, 0.95f, 0.85f));
            Log.AppendLine("R4 playroom + R6 window built at " + c);
        }

        // =====================================================================================
        // R5: inside the wardrobe
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
