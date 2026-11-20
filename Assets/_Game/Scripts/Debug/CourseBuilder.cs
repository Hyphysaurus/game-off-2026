#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
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
    public static partial class CourseBuilder
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
            MatBeds(r4);   // one smooth collider under each group of mats (D-0049), before anything is placed or checked against them
            r5Start = BuildWardrobe(r5, wood, glowMat, coreMat);
            BuildCarCourse();   // the stunt run lives in R4 too (D-0026); it parks the car at its start
            BuildExpressway(r4);   // the raised loop round the room (D-0048), after everything it has to route round is standing

            // ---- portals between rooms ----
            WireLane(r3, r3Start, r4, r4Entry);
            WireWardrobe(r4, r5, r5Start, wardrobeFront);
            PuzzleCues();
            PlaceRides(course);
            ArmyBeat(r4);   // the weapons beat (D-0082), last: it places round what stands
            EditorSceneManager.MarkAllScenesDirty();
            Log.AppendLine("built");
        }

        /// <summary>The wardrobe's two doors: the hole under it (R4) to R5's start, and R5's top (kid) to the wardrobe's front in
        /// R4, opening the wardrobe's doors as it is used.</summary>
        static void WireWardrobe(Transform r4, Transform r5, Transform r5Start, Transform wardrobeFront)
        {
            var hole = r4.Find("WardrobeHole"); if (hole != null) Set(hole.GetComponent<Portal>(), "target", r5Start);
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
        }

        /// <summary>The lane's two doors: the bedroom's toy door (its old Goal becomes a Portal) to the lane's start, the
        /// lane's exit to the playroom entry, and the first time through the exit the playroom reveal plays (D-0023).</summary>
        static void WireLane(Transform r3, Transform r3Start, Transform r4, Transform r4Entry)
        {
            var toyGoal = GameObject.Find("Room/Bedroom/Goal");
            if (toyGoal != null)
            {
                var g = toyGoal.GetComponent<Goal>(); if (g != null) Object.DestroyImmediate(g);
                var p = toyGoal.GetComponent<Portal>() ?? toyGoal.AddComponent<Portal>();
                Set(p, "target", r3Start); Set(p, "requiredSize", (int)ShiftSize.Toy);
                Log.AppendLine("toy door -> R3");
            }
            var laneExit = r3.Find("ExitPortal"); if (laneExit != null) Set(laneExit.GetComponent<Portal>(), "target", r4Entry);
            var reveal = r4.Find("Reveal"); if (laneExit != null && reveal != null)
            {
                var pso = new SerializedObject(laneExit.GetComponent<Portal>()); var calls = pso.FindProperty("onUsed.m_PersistentCalls.m_Calls");
                calls.arraySize = 1; Listener(calls.GetArrayElementAtIndex(0), reveal.GetComponent<RoomReveal>(), typeof(RoomReveal).AssemblyQualifiedName, "Play"); pso.ApplyModifiedPropertiesWithoutUndo();
                Log.AppendLine("lane exit -> playroom reveal");
            }
        }

        // =====================================================================================
        // R3: the skirting run
        static Transform BuildLane(Transform r3, Material dark, Material glow)
        {
            float z = LaneZ, w = 20f, h = 14f;
            float x0 = LaneX0, x1 = LaneX1;
            // floor in pieces, leaving three pits: 38..72 (pencil bridge), 100..103.5 (hop) and 229..234.5 (sprint). The sprint gap is
            // 5.5: the toy jumps 4.4 at a walk and 6.4 at a sprint (jump 2.0, walk 5.5, sprint 8), so it needs Shift; at 4.5 a
            // walking jump made it (walk check 19 Nov, D-0081)
            float[][] floors = { new[] { x0, 38f }, new[] { 72f, 100f }, new[] { 103.5f, 229f }, new[] { 234.5f, x1 } };
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
            // 2. hop gap at 100..103.5, then a checkpoint on the landing strip (the book stairs start after it, at 112: they
            //    started at the gap's far edge, so the hop landed on a book 2 high and fell back in: walk check 19 Nov, D-0081)
            Checkpoint("CP_afterHop", r3, new Vector3(107f, 2f, z), new Vector3(6f, 4f, w));
            // 3. book stairs up onto a 12-high box (ten risers of 1.2 on 2.4-deep treads: easy hops, 5 Nov his "can't climb"),
            //    then a 12 drop off the far side (under the 14 fall rule)
            var box = Rest(Spawn("Cardboard_Box_06", "StepBox", r3, new Vector3(142f, 0f, z), Vector3.zero, 1f), 0f, null);
            float boxTop = B(box).max.y;
            int stairBooks = Mathf.CeilToInt(boxTop / 1.2f);   // BookStairs' own count: books 0..n, the last one against the box
            box.transform.position += Vector3.right * ((112f + (stairBooks + 0.5f) * 2.4f) - B(box).min.x);
            BookStairs(r3, new Vector3(B(box).min.x - (stairBooks + 0.5f) * 2.4f + 1.2f, 0f, z), Vector3.right, 0f, boxTop, "UpStairs");
            {   // the books are long (they lie along the lane): slide the box and the stairs on together so the first book starts at
                // 110, leaving the hop's landing (103.5..110) clear floor with its checkpoint
                var stairs = r3.Find("UpStairs"); float lead = 110f - B(stairs.gameObject).min.x;
                stairs.position += Vector3.right * lead; box.transform.position += Vector3.right * lead;
            }
            // 4. three pins standing in the lane, walk between them (no hazard yet: dressing + a squeeze)
            foreach (float px in new[] { 180f, 190f, 200f })
            {
                var pin = Rest(Spawn("Pin_01", "Pin", r3, new Vector3(px, 0f, z + ((px / 10f) % 2 == 0 ? 3f : -3f)), new Vector3(-90, 0, 0), 1f), 0f, null);
                var hz = Trigger("Hazard", pin.transform, B(pin).center, new Vector3(1.6f, B(pin).size.y, 1.6f)); hz.AddComponent<Hazard>();
            }
            // 5. sprint gap 229..234.5 with a checkpoint before and after
            Checkpoint("CP_beforeSprint", r3, new Vector3(222f, 2f, z), new Vector3(6f, 4f, w));
            Checkpoint("CP_afterSprint", r3, new Vector3(244f, 2f, z), new Vector3(6f, 4f, w));
            // dressing: a lantern, chalk, a leaning CD, clips
            Rest(Spawn("Night_Lamp_02", "Lantern", r3, new Vector3(170f, 0f, z + 6f), Vector3.zero, 1f), 0f, null);   // past the step box (moved east, D-0081)
            Rest(Spawn("Chalk_01", "Chalk", r3, new Vector3(20f, 0f, z - 6f), new Vector3(0, 30, 0), 1f), 0f, null);
            Rest(Spawn("Clip_01", "Clip", r3, new Vector3(115f, 0f, z + 7f), new Vector3(0, 70, 0), 1f), 0f, null);
            Rest(Spawn("Clip_02", "Clip2", r3, new Vector3(260f, 0f, z - 7f), new Vector3(0, 20, 0), 1f), 0f, null);
            // exit: a glow on the east wall, a point light, the portal
            var q = Quad("ExitGlow", r3, new Vector3(x1 - 0.1f, 3f, z), new Vector3(0, -90, 0), new Vector3(6f, 6f, 1f), glow);
            PointLight("ExitLight", r3, new Vector3(x1 - 6f, 4f, z), 20f, 2.5f, new Color(1f, 0.85f, 0.55f));
            var exit = Trigger("ExitPortal", r3, new Vector3(x1 - 2f, 3f, z), new Vector3(2f, 6f, 8f));
            var portal = exit.AddComponent<Portal>(); Set(portal, "requiredSize", (int)ShiftSize.Toy);
            // the room beyond, heard before it is seen: its hum grows as you near the hole
            var hum = exit.AddComponent<AudioSource>(); hum.clip = Clip("Room_Ambience_Loop_v1"); hum.loop = true; hum.playOnAwake = true; hum.spatialBlend = 1f; hum.rolloffMode = AudioRolloffMode.Linear; hum.minDistance = 4f; hum.maxDistance = 90f; hum.volume = 0.7f;
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
                if (Mathf.Abs(x) > 40f)   // the north wall leaves the window bay (the x -32.5 and 32.5 tiles) to the Wall_03 window piece
                {
                    var n = Spawn("Wall_01", "Wall_N", r4, c + new Vector3(x, 0, R4HalfZ), new Vector3(0, 180, 0), 1f); var bn = B(n); n.transform.position += new Vector3(0, -bn.min.y, (c.z + R4HalfZ) - bn.min.z);
                }
            }
            // north wall centre: the pack's window wall piece (Wall_03, 130 wide with the opening in its middle) between the tiles
            float nz = c.z + R4HalfZ + 2.5f;
            var wallWin = Spawn("Wall_03", "Wall_N_Window", r4, c + new Vector3(0, 0, R4HalfZ), new Vector3(0, 180, 0), 1f); var bww = B(wallWin); wallWin.transform.position += new Vector3(0, -bww.min.y, (c.z + R4HalfZ) - bww.min.z);
            foreach (float z in zs)
            {
                var w = Spawn("Wall_01", "Wall_W", r4, c + new Vector3(-R4HalfX, 0, z), new Vector3(0, 90, 0), 1f); var bw = B(w); w.transform.position += new Vector3((c.x - R4HalfX) - bw.max.x, -bw.min.y, 0);
                var e = Spawn("Wall_01", "Wall_E", r4, c + new Vector3(R4HalfX, 0, z), new Vector3(0, -90, 0), 1f); var be = B(e); e.transform.position += new Vector3((c.x + R4HalfX) - be.min.x, -be.min.y, 0);
            }
            foreach (float x in new[] { -R4HalfX - 2.5f, R4HalfX + 2.5f }) foreach (float z in new[] { -R4HalfZ - 2.5f, R4HalfZ + 2.5f })
            { var p = Spawn("Wall_05", "Pillar", r4, c + new Vector3(x, 0, z), Vector3.zero, 1f); var bp = B(p); p.transform.position += new Vector3(c.x + x - bp.center.x, -bp.min.y, c.z + z - bp.center.z); }
            PointLight("RoomLight", r4, c + new Vector3(0, 95f, 0), 520f, 4.5f, new Color(1f, 0.93f, 0.82f), true);
            PointLight("FillNW", r4, c + new Vector3(-120f, 100f, 70f), 320f, 1.6f, new Color(1f, 0.9f, 0.75f));
            PointLight("FillSE", r4, c + new Vector3(120f, 100f, -70f), 320f, 1.6f, new Color(1f, 0.9f, 0.75f));
            Spawn("Planet_Lamp_01", "PlanetLamp", r4, c + new Vector3(0, 130f, 0), Vector3.zero, 1f);

            // --- Workplace (NW): table against the north wall, computer/keyboard/globe on it, chair at its south edge
            var table = Spawn("Table_01", "Table", r4, c + new Vector3(-140f, 0, 110f), Vector3.zero, 1f); var tb = B(table);
            table.transform.position += new Vector3(0, -tb.min.y, (c.z + R4HalfZ - 1f) - tb.max.z); tb = B(table);
            RestOn(Spawn("Computer_01", "Computer", r4, new Vector3(tb.min.x + 12f, 0, tb.center.z + 8f), new Vector3(0, 20, 0), 1f), tb.max.y);
            RestOn(Spawn("Keyboard_01", "Keyboard", r4, new Vector3(tb.center.x + 6f, 0, tb.center.z - 10f), new Vector3(0, -5, 0), 1f), tb.max.y);
            RestOn(Spawn("Globe_01", "Globe", r4, new Vector3(tb.max.x - 14f, 0, tb.center.z + 10f), Vector3.zero, 1f), tb.max.y);
            var chair = Spawn("Chair_01", "Chair", r4, new Vector3(tb.center.x, 0, tb.min.z - 9f), new Vector3(0, 180, 0), 1f); Rest(chair, 0f, null);
            var cb = B(chair);
            // the toy's way up AND down the desk (36): three ruler ramps zigzagging over a 12 box and a 24 stack of closed boxes
            // (every taller box in the pack has open flaps: a trap). Rulers at 1.5x (23.4 long) climb 12 at about 30°, under the
            // capsule's 45°; each lies with its TOP surface flush with both ends (no lip at the foot). Fixed live 5 Nov (D-0021).
            var stackA = Rest(Spawn("Cardboard_Box_05", "Box12a", r4, new Vector3(cb.max.x + 18f, 0, tb.min.z - 20f), Vector3.zero, 1f), 0f, null);
            var stackB = Rest(Spawn("Cardboard_Box_05", "Box24", r4, stackA.transform.position, Vector3.zero, 1f), B(stackA).max.y, null);
            var box06 = Rest(Spawn("Cardboard_Box_06", "Box12", r4, stackA.transform.position, Vector3.zero, 1f), 0f, null);
            {
                var sb0 = B(stackA); float boxH = sb0.size.y, stackTop = 2f * boxH, stackX = sb0.center.x;
                Vector3 top3 = new Vector3(stackX, tb.max.y + 0.03f, tb.min.z);   // tops sit ON the upper edge: no poke through the side
                float run3 = Ramp("Ruler_Big_05", "Ramp3", r4, top3, Vector3.forward, tb.max.y - stackTop);
                float stackCz = top3.z - run3 + 1.5f - sb0.extents.z;
                foreach (var g in new[] { stackA, stackB }) g.transform.position += new Vector3(0, 0, stackCz - B(g).center.z);
                var sb = B(stackB);
                Vector3 top2 = new Vector3(sb.max.x, stackTop + 0.03f, stackCz);
                float run2 = Ramp("Ruler_Big_03", "Ramp2", r4, top2, Vector3.left, stackTop - boxH);
                var bb0 = B(box06); box06.transform.position += new Vector3(top2.x + run2 - 1.5f + bb0.extents.x - bb0.center.x, 0, stackCz - bb0.center.z);
                var bb = B(box06);
                Ramp("Ruler_Big_02", "Ramp1", r4, new Vector3(bb.center.x, bb.max.y + 0.03f, bb.min.z), Vector3.forward, bb.max.y);
            }
            var ruler = Rest(Spawn("Ruler_Big_01", "Ruler", r4, new Vector3(cb.min.x - 14f, 0, tb.min.z - 26f), new Vector3(0, 35, 0), 1f), 0f, null);   // west of the stool, clear of the ramps
            var rc = ruler.AddComponent<Carryable>(); Set(rc, "plank", true);   // carried flat in both hands, grips from its own box (D-0042)
            ruler.AddComponent<Carried>();   // kid-only pick-up prompt
            // core B: shrink, on the table, near the south edge
            var coreB = Core("CoreB_Shrink", r4, new Vector3(tb.center.x - 20f, tb.max.y + 2.5f, tb.min.z + 10f), ShiftSource.Mode.ShrinkOnly, coreMat, false);
            Checkpoint("CP_table", r4, new Vector3(tb.center.x, tb.max.y + 3f, tb.center.z), new Vector3(tb.size.x - 4f, 6f, tb.size.z - 4f), ShiftSize.Toy);

            // --- Toy Army (NE): a sandbag maze with core A in the middle
            Vector3 mz = c + new Vector3(125f, 0, 112f);   // north of the sofa (the ring clipped its corner at z 95, scan 5 Nov)
            SandbagRing(r4, mz, 36f, 90f);      // outer ring, gap on the south side
            SandbagRing(r4, mz, 18f, 270f);     // inner ring, gap on the north side
            var coreA = Core("CoreA_Grow", r4, mz + new Vector3(0, 2.5f, 0), ShiftSource.Mode.GrowOnly, coreMat, false);
            Rest(Spawn("Army_WatchTower_01", "WatchTower", r4, mz + new Vector3(0, 0, -46f), Vector3.zero, 1f), 0f, null);
            Rest(Spawn("Army_Tent_01", "Tent", r4, mz + new Vector3(-50f, 0, 10f), new Vector3(0, 30, 0), 1f), 0f, null);
            Rest(Spawn("Army_Tent_02", "Tent2", r4, mz + new Vector3(48f, 0, -20f), new Vector3(0, -40, 0), 1f), 0f, null);
            foreach (var (n, off) in new[] { ("Army_Soldier_01", new Vector3(-44f, 0, -30f)), ("Army_Soldier_02", new Vector3(44f, 0, 30f)), ("Army_Soldier_03", new Vector3(-30f, 0, 44f)), ("Army_Soldier_04", new Vector3(20f, 0, 50f)) })
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
            safe.AddComponent<Carryable>(); safe.AddComponent<Carried>();   // grabbed and carried in the arms (D-0042)
            wardrobeFront = Point("WardrobeFront", r4, new Vector3(wb.min.x - 40f, 0, wb.center.z), 0f);   // facing north, toward the window
            // --- the entry from R3: west wall, at the lane's z
            entry = Point("Entry", r4, new Vector3(c.x - R4HalfX + 8f, 0, LaneZ), 90f);
            Quad("EntryGlow", r4, new Vector3(c.x - R4HalfX + 0.2f, 1.8f, LaneZ), new Vector3(0, -90, 0), new Vector3(1.6f, 3.6f, 1f), glow);
            // the reveal (D-0023): camera keys as children (move and turn them in the editor to re-shoot it). From low behind the
            // toy in the hole, along the floor, up past the desk, over the maze to the window, and home behind the toy.
            {
                var rv = Child(r4, "Reveal"); Vector3 e = new Vector3(c.x - R4HalfX + 8f, 0, LaneZ);
                Vector3 desk = new Vector3(tb.center.x, tb.max.y, tb.center.z), window = c + new Vector3(0f, 70f, R4HalfZ);
                void Key(string n, Vector3 at, Vector3 look) { var k = Child(rv, n); k.position = at; k.rotation = Quaternion.LookRotation(look - at, Vector3.up); }
                Key("1_InTheHole", e + new Vector3(-6f, 2.2f, 0f), e + new Vector3(30f, 2.6f, 0f));
                Key("2_AlongTheFloor", e + new Vector3(30f, 4.5f, 5f), c + new Vector3(-30f, 14f, 40f));
                Key("3_UpPastTheDesk", c + new Vector3(-70f, 42f, -70f), desk);
                Key("4_OverTheRoom", c + new Vector3(25f, 80f, -105f), window);
                Key("5_TheMaze", mz + new Vector3(-50f, 26f, -48f), mz + Vector3.up * 3f);
                Key("6_BackToYou", e + new Vector3(-3f, 6f, -4f), e + new Vector3(25f, 3f, 0f));
                var rr = rv.gameObject.AddComponent<RoomReveal>();
                var bright = new List<Light>(); foreach (var l in r4.GetComponentsInChildren<Light>()) if (l.range >= 100f) bright.Add(l);
                SetArray(rr, "lights", bright.ToArray()); Set(rr, "music", Clip("A_Secret_Garden_Of_Light_v1"));
                Log.AppendLine("reveal: 6 keys, " + bright.Count + " lights");
            }
            // the save point (D-0022): a night lamp beside the mouse hole that lights when you touch it; Continue starts here
            {
                Vector3 sp = new Vector3(c.x - R4HalfX + 16f, 0, LaneZ + 12f);
                var lamp = Rest(Spawn("Night_Lamp_04", "SaveLamp", r4, sp, new Vector3(0, 90, 0), 1f), 0f, null); var lb = B(lamp);
                var lampLight = PointLight("SaveLight", lamp.transform, new Vector3(lb.center.x, lb.max.y, lb.center.z), 30f, 2.5f, new Color(1f, 0.82f, 0.5f));
                var trig = Trigger("SavePoint", r4, new Vector3(sp.x + 2f, 2f, sp.z - 6f), new Vector3(14f, 4f, 14f));
                var cp = trig.AddComponent<Checkpoint>(); Set(cp, "savePoint", true); Set(cp, "repeat", true); Set(cp, "saveLabel", "R4_Playroom"); Set(cp, "litWhenClaimed", lampLight.gameObject);
                var point = Point("SaveSpot", trig.transform, new Vector3(sp.x + 6f, 0.2f, sp.z - 8f), 90f); Set(cp, "point", point);
            }
            // a drivable toy car (D-0022): the wooden push car, big enough to sit in, parked by the entry facing into the room
            {
                var car = Rest(Spawn("Wooden_Car_01", "ToyCar", r4, new Vector3(c.x - R4HalfX + 34f, 0, LaneZ - 10f), new Vector3(0, 90, 0), 1f), 0f, null);
                var tc = car.AddComponent<ToyCar>(); Set(tc, "rollLoop", Clip("Toy_Car_Roll_v1")); Set(tc, "crash", Clip("Toy_Car_Crash_v1")); WorkshopPieces.WireBoostParts(tc);
                var bc = car.AddComponent<BoxCollider>(); bc.isTrigger = true; var cb2 = B(car); bc.center = car.transform.InverseTransformPoint(cb2.center); bc.size = new Vector3(cb2.size.x, cb2.size.y, cb2.size.z) + Vector3.one * 2f;
            }
            // ---- dressing, our own composition with the pack's props (D-0019: no showroom copy): one zone per corner and side,
            //      wall art on all four walls, mats in 2x2 clusters, then seeded floor clutter everywhere except the reserved beats
            var reserved = new List<Bounds>
            {
                new Bounds(mz + Vector3.up * 10f, new Vector3(96f, 20f, 96f)),                                   // the maze and its gaps
                new Bounds(new Vector3(tb.center.x, 10f, tb.min.z - 24f), new Vector3(tb.size.x + 50f, 20f, 60f)),   // the way down from the desk
                new Bounds(new Vector3(wb.min.x - 20f, 10f, wb.center.z), new Vector3(44f, 20f, 40f)),           // the safe and the hole
                new Bounds(new Vector3(c.x - R4HalfX + 16f, 10f, LaneZ), new Vector3(34f, 20f, 30f)),            // the entry
            };
            bool Free(Vector3 p) { foreach (var b in reserved) if (b.Contains(p)) return false; return true; }
            var rng = new System.Random(11);
            float Rnd(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            // what is already standing in the room: furniture, beats, the maze. Dressing never lands inside any of it (scan 5 Nov:
            // 118 overlaps when it only avoided the beats). Floors, walls, ceilings, mats and glow quads do not count.
            string[] open = { "Floor", "Ceiling", "Wall", "Pillar", "Mat", "Entry", "Hole", "Glow", "RoomLight", "Fill", "PlanetLamp", "CP_", "Showroom" };
            var blocked = new List<Bounds>();
            foreach (Transform t in r4) { bool skipIt = false; foreach (var o in open) if (t.name.StartsWith(o)) skipIt = true; if (skipIt || (t.TryGetComponent(out Collider col) && col.isTrigger)) continue; var bb0 = B(t.gameObject); if (bb0.size.sqrMagnitude > 0.01f) blocked.Add(bb0); }
            // a small tolerance for touching, shrunk in x and z only, never more than a third of a thin thing (paper is 0.1 thick:
            // shrinking every axis turned its box inside out and let everything through)
            bool Clear(Bounds b) { foreach (var k in blocked) { var m = k; m.Expand(new Vector3(-Mathf.Min(0.4f, m.size.x * 0.3f), 0f, -Mathf.Min(0.4f, m.size.z * 0.3f))); m.Expand(new Vector3(0f, 0.02f, 0f)); if (m.Intersects(b)) return false; } return true; }
            // a prop at pos (resting at restY), nudged outward in a small spiral until it touches nothing; skipped if it never fits
            GameObject Prop(string prefab, Vector3 pos, float yaw, float restY = 0f, string name = null, float reach = 18f)
            {
                var g = Rest(Spawn(prefab, name ?? prefab, r4, pos, new Vector3(0, yaw, 0), 1f), restY, null);
                for (int k = 0; k < 14; k++)
                {
                    var b = B(g);
                    if (Clear(b)) { blocked.Add(b); return g; }
                    float a = k * 2.4f, r = reach * (k + 1) / 14f;
                    g.transform.position = new Vector3(pos.x + Mathf.Cos(a) * r, g.transform.position.y, pos.z + Mathf.Sin(a) * r);
                }
                Object.DestroyImmediate(g); return null;
            }
            // the top surface of a piece of furniture under (x, z): a ray onto its own colliders, not its bounding box
            float Top(GameObject on, float x, float z)
            {
                Physics.SyncTransforms(); var bb = B(on);
                foreach (var h in Physics.RaycastAll(new Vector3(x, bb.max.y + 5f, z), Vector3.down, bb.size.y + 10f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                    if (h.collider.transform.IsChildOf(on.transform)) return h.point.y;
                return bb.max.y;
            }
            void Scatter(string[] prefabs, Vector3 centre, float half, int count, Pushable.Weight? push = null)
            {
                for (int i = 0, tries = 0; i < count && tries < count * 8; tries++)
                {
                    var p = new Vector3(centre.x + Rnd(-half, half), 0, centre.z + Rnd(-half, half));
                    if (Mathf.Abs(p.x - c.x) > R4HalfX - 6f || Mathf.Abs(p.z - c.z) > R4HalfZ - 6f || !Free(p)) continue;
                    var g = Prop(prefabs[rng.Next(prefabs.Length)], p, Rnd(0, 360), 0f, null, 8f); if (g == null) continue; i++;
                    if (push.HasValue) Push(g, push.Value, 0.5f);
                }
            }
            // on the desk: lamp, mouse, mug, markers, notes (the core keeps its corner)
            var deskLamp = RestOn(Spawn("Lamp_01_Base", "DeskLamp", r4, new Vector3(tb.max.x - 30f, 0, tb.max.z - 8f), new Vector3(0, -140, 0), 1f), tb.max.y); var dlb = B(deskLamp);
            var dlLight = PointLight("LampLight", deskLamp.transform, new Vector3(dlb.center.x, dlb.max.y - 2f, dlb.center.z), 60f, 2.5f, new Color(1f, 0.9f, 0.7f)); dlLight.enabled = false;
            React(deskLamp, Reactor.Response.ToggleLight, "Switch on", lamp: dlLight, continuous: true);
            var globeGo = r4.Find("Globe"); if (globeGo != null) React(globeGo.gameObject, Reactor.Response.Spin, "Spin");
            // the keyboard is a musical floor for the toy (D-0022): keys sink and click along a pentatonic scale
            var kbGo = r4.Find("Keyboard"); if (kbGo != null) { var kk = kbGo.gameObject.AddComponent<KeyboardKeys>(); SetArray(kk, "clicks", Clip("Keyboard_Click_v1"), Clip("Keyboard_Click_v2")); }
            var pcGo = r4.Find("Computer"); if (pcGo != null) { var pcb = B(pcGo.gameObject); var pcLight = PointLight("ScreenLight", pcGo, new Vector3(pcb.center.x, pcb.center.y, pcb.min.z - 6f), 40f, 2f, new Color(0.5f, 0.7f, 1f)); pcLight.enabled = false; React(pcGo.gameObject, Reactor.Response.ToggleLight, "Switch on", lamp: pcLight, continuous: true); }
            Prop("Mouse_01", new Vector3(tb.center.x + 22f, 0, tb.center.z - 6f), 15f, tb.max.y, "Mouse", 6f);
            Prop("Cup_02", new Vector3(tb.center.x - 4f, 0, tb.max.z - 8f), 0f, tb.max.y, "Mug", 6f);
            Prop("Marker_Red_01", new Vector3(tb.center.x - 14f, 0, tb.max.z - 6f), 70f, tb.max.y, "Marker", 6f);
            Prop("Marker_Blue_01", new Vector3(tb.center.x - 10f, 0, tb.max.z - 11f), 40f, tb.max.y, "Marker2", 6f);
            Prop("Note_Paper_03", new Vector3(tb.max.x - 10f, 0, tb.min.z + 10f), 20f, tb.max.y, "Note", 6f);
            Prop("Calculator_01", new Vector3(tb.center.x - 2f, 0, tb.min.z + 9f), -30f, tb.max.y, "Calc", 6f);
            // west wall by the hoop: a floor shelf with books and a toy on top, a skateboard and a ball
            var shelf = Spawn("Shelf_01", "Shelf", r4, c + new Vector3(-R4HalfX + 12f, 0, 60f), new Vector3(0, 90, 0), 1f); Rest(shelf, 0f, null); var shb = B(shelf);
            shelf.transform.position += new Vector3((c.x - R4HalfX + 1f) - shb.min.x, 0, 0); shb = B(shelf);
            foreach (var (pair, off) in new[] { (new[] { "Book_02", "Book_05" }, -12f), (new[] { "Book_09", "Book_11" }, 2f) })
            {
                float top = Top(shelf, shb.center.x, shb.center.z + off);
                foreach (var n in pair) { var bk = Spawn(n, n, r4, new Vector3(shb.center.x, 0, shb.center.z + off), new Vector3(0, 90 + Rnd(-8, 8), 0), 1f); RestOn(bk, top); top = B(bk).max.y; }
            }
            React(RestOn(Spawn("Toy_Rabbit_01", "Rabbit", r4, new Vector3(shb.center.x, 0, shb.max.z - 8f), new Vector3(0, 120, 0), 1f), Top(shelf, shb.center.x, shb.max.z - 8f)), Reactor.Response.Bob, "Poke");
            blocked.Add(B(shelf));
            Push(Prop("Skateboard_01", c + new Vector3(-R4HalfX + 14f, 0, 10f), 80f), Pushable.Weight.Heavy, 3f);
            Push(Prop("Basketball_Ball_01", c + new Vector3(-150f, 0, -40f), 0f), Pushable.Weight.Heavy, 2f);
            Push(Prop("Dumbbell_01", c + new Vector3(-165f, 0, 100f), 30f), Pushable.Weight.Heavy, 6f);
            React(Prop("Pinwheel_01", c + new Vector3(-160f, 0, 30f), 20f), Reactor.Response.Spin, "Spin");
            // the race track: cars, ramps, a road loop (the track itself is placed above)
            Push(Prop("Car_02", c + new Vector3(18f, 0, -12f), -120f), Pushable.Weight.Light, 1f); Push(Prop("Car_03", c + new Vector3(-40f, 0, 8f), 200f), Pushable.Weight.Light, 1f);
            Prop("Ramp_01", c + new Vector3(40f, 0, 10f), 90f); Prop("RoadLoop_01", c + new Vector3(-60f, 0, -30f), 0f);
            Prop("Train_Station_01", c + new Vector3(-20f, 0, 36f), -90f);
            // the army camp around the maze: a truck, a helicopter, a flag, ladders, loose sandbags, wire, soldiers on stands
            Prop("Army_Truck_01", mz + new Vector3(-62f, 0, -40f), 150f); Prop("Army_Helicopter_01", mz + new Vector3(58f, 0, 44f), -60f);
            Prop("Army_Flag_Stand_01", mz + new Vector3(50f, 0, -50f), 0f); Prop("Army_Ladder_01", mz + new Vector3(-50f, 0, 52f), 30f);
            Prop("Army_BarbedWire_Fence_01", mz + new Vector3(-58f, 0, 20f), 80f); Prop("Army_BarbedWire_Fence_01", mz + new Vector3(-58f, 0, 32f), 85f);
            foreach (var (n, off) in new[] { ("Army_Sandbags_03", new Vector3(56f, 0, 10f)), ("Army_Sandbags_05", new Vector3(-56f, 0, -10f)), ("Army_Sandbags_09", new Vector3(20f, 0, 58f)), ("Army_Sandbags_10", new Vector3(-20f, 0, -58f)) }) Prop(n, mz + off, Rnd(0, 360));
            foreach (var (n, off) in new[] { ("Army_Soldier_05_Platform", new Vector3(40f, 0, 54f)), ("Army_Soldier_06_Platform", new Vector3(-40f, 0, -54f)), ("Army_Soldier_07_Platform", new Vector3(58f, 0, -20f)) }) Prop(n, mz + off, Rnd(0, 360));
            // the castle: two more towers, skittles bowled over beside it, a soldier on the wall
            Prop("Castle_Tower_02", cz + new Vector3(-30f, 0, -6f), 0f); Prop("Castle_Tower_02", cz + new Vector3(30f, 0, 6f), 0f);
            foreach (var (n, off) in new[] { ("Wooden_Skittle_01", new Vector3(-44f, 0, -24f)), ("Wooden_Skittle_02", new Vector3(-50f, 0, -18f)), ("Wooden_Skittle_01", new Vector3(-40f, 0, -14f)) }) Push(Prop(n, cz + off, Rnd(0, 360)), Pushable.Weight.Light, 0.8f);
            Push(Prop("Ball_01", cz + new Vector3(-62f, 0, -30f), 0f), Pushable.Weight.Light, 0.6f);
            // the couch: pillows, a bear, the chessboard with a few pieces on the seat, a cat on the floor
            var sofaB = B(sofa); float seatX = sofaB.center.x - sofaB.size.x * 0.14f;   // the seat's middle, in front of the backrest
            float SeatY(float z) => Top(sofa, seatX, z);
            Carry(RestOn(Spawn("Pillow_01", "Pillow2", r4, new Vector3(seatX, 0, sofaB.min.z + 15f), new Vector3(0, -20, 0), 1f), SeatY(sofaB.min.z + 15f)));
            Carry(RestOn(Spawn("Toy_Bear_01", "Bear", r4, new Vector3(seatX + 2f, 0, sofaB.center.z + 12f), new Vector3(0, -100, 0), 1f), SeatY(sofaB.center.z + 12f)));
            var pad = RestOn(Spawn("Chesspad_01", "Chesspad", r4, new Vector3(seatX, 0, sofaB.center.z - 13f), new Vector3(0, 10, 0), 1f), SeatY(sofaB.center.z - 13f)); var padB = B(pad);
            foreach (var (n, off) in new[] { ("Chess_White_King_01", new Vector3(-3f, 0, 2f)), ("Chess_Black_Queen_01", new Vector3(3f, 0, -3f)), ("Chess_White_Pawn_01", new Vector3(-5f, 0, -5f)), ("Chess_Black_Hoss_01", new Vector3(5f, 0, 4f)) }) RestOn(Spawn(n, n, r4, padB.center + off, Vector3.zero, 1f), Top(pad, padB.center.x + off.x, padB.center.z + off.z));
            var firstPillow = r4.Find("Pillow"); if (firstPillow != null) { firstPillow.position = new Vector3(seatX, 0, sofaB.max.z - 15f); RestOn(firstPillow.gameObject, SeatY(sofaB.max.z - 15f)); }
            CouchPillowPads(r4);
            blocked.Add(sofaB);
            React(Prop("Toy_Cat_01", c + new Vector3(140f, 0, 70f), -140f), Reactor.Response.Squash, "Pet");
            // a broken desk fan on its back in front of the sofa, propped up by a book so it blows up and toward the seat: a
            // toy that walks into the wind rides it onto the cushions, a kid steps over it (D-0021)
            var fanProp = Spawn("Fan_Base_01", "BrokenFan", r4, new Vector3(sofaB.min.x - 42f, 0, sofaB.center.z), new Vector3(-50f, 90f, 0), 1f); Rest(fanProp, 0f, null); var fpb = B(fanProp);
            var names = new StringBuilder(); foreach (Transform ch in fanProp.GetComponentsInChildren<Transform>()) names.Append(ch.name).Append(' ');
            Log.AppendLine("fan " + fpb.size + " children: " + names);
            Transform fanBlades = null; foreach (Transform ch in fanProp.GetComponentsInChildren<Transform>()) if (ch != fanProp.transform && ch.name.Contains("Propeller")) { fanBlades = ch; break; }
            Rest(Spawn("Book_09", "FanProp", r4, new Vector3(fpb.min.x - 2f, 0, fpb.center.z), new Vector3(0, 0, 0), 1f), 0f, null);
            Vector3 wind = fanProp.transform.forward;   // the fan's face, pitched up by the tilt
            var column = new GameObject("Column"); column.transform.SetParent(r4, false);
            column.transform.rotation = Quaternion.FromToRotation(Vector3.up, wind);
            column.transform.position = new Vector3(fpb.center.x, 0, fpb.center.z) + wind * 34f + Vector3.up * 2f;
            var colBox = column.AddComponent<BoxCollider>(); colBox.isTrigger = true; colBox.size = new Vector3(14f, 70f, 14f);
            var updraft = column.AddComponent<Updraft>(); if (fanBlades != null) Set(updraft, "blades", fanBlades);
            reserved.Add(new Bounds(new Vector3(fpb.center.x + 20f, 10f, fpb.center.z), new Vector3(70f, 20f, 30f)));
            Log.AppendLine("updraft at " + column.transform.position + " blowing " + wind);
            // the wardrobe corner: a shelf with a book stack and the clock above; the safe keeps its spot
            var shelf2 = Spawn("Shelf_01", "Shelf2", r4, new Vector3(wb.center.x, 0, wb.min.z - 30f), new Vector3(0, -90, 0), 1f); Rest(shelf2, 0f, null); var s2b = B(shelf2);
            shelf2.transform.position += new Vector3((c.x + R4HalfX - 1f) - s2b.max.x, 0, 0); s2b = B(shelf2);
            { float stackTop = Top(shelf2, s2b.center.x, s2b.center.z + 4f); foreach (var n in new[] { "Book_01", "Book_04", "Book_07" }) { var bk = Spawn(n, n, r4, new Vector3(s2b.center.x, 0, s2b.center.z + 4f), new Vector3(0, Rnd(-10, 10), 0), 1f); RestOn(bk, stackTop); stackTop = B(bk).max.y; } }
            var nightLamp = RestOn(Spawn("Night_Lamp_02", "NightLamp", r4, new Vector3(s2b.center.x, 0, s2b.min.z + 8f), new Vector3(0, -90, 0), 1f), Top(shelf2, s2b.center.x, s2b.min.z + 8f)); var nlb = B(nightLamp);
            blocked.Add(B(shelf2));
            var nlLight = PointLight("NightLight", nightLamp.transform, new Vector3(nlb.center.x, nlb.max.y, nlb.center.z), 80f, 2.5f, new Color(1f, 0.75f, 0.45f)); nlLight.enabled = false;
            React(nightLamp, Reactor.Response.ToggleLight, "Switch on", lamp: nlLight, continuous: true);
            // SW corner: wooden toys and building blocks, the challenge zone
            Vector3 sw = c + new Vector3(-120f, 0, -100f);
            React(Prop("Wooden_Bear_01", sw + new Vector3(0, 0, 10f), 40f), Reactor.Response.Bob, "Poke");
            Push(Prop("Wooden_Train_01", sw + new Vector3(30f, 0, -20f), -70f), Pushable.Weight.Heavy, 2f); Push(Prop("Wooden_Wagon_01", sw + new Vector3(42f, 0, -28f), -70f), Pushable.Weight.Heavy, 2f);
            Push(Prop("Wooden_Ship_01", sw + new Vector3(-30f, 0, -30f), 20f), Pushable.Weight.Heavy, 2f); Push(Prop("Wooden_Plane_01", sw + new Vector3(-40f, 0, 20f), 160f), Pushable.Weight.Heavy, 2f); Prop("Toy_Pyramid_01", sw + new Vector3(20f, 0, 30f), 0f);
            Scatter(new[] { "Color_Cube_01", "Color_Cube_02", "Color_Cube_03", "Wooden_Cube_01", "Wooden_Cube_02", "Wooden_Cylinder_01", "Wooden_Pyramid_02", "Constructor_01", "Constructor_03", "Constructor_05" }, sw, 45f, 26, Pushable.Weight.Light);
            Scatter(new[] { "Puzzle_01", "Puzzle_04", "Puzzle_07", "Puzzle_12", "Puzzle_15" }, sw + new Vector3(60f, 0, 50f), 22f, 5);
            // mats: three 2x2 clusters of puzzle mats on the open floor
            var mat0 = Spawn("Mat_01", "Mat", r4, c + new Vector3(-100f, 0, 40f), Vector3.zero, 1f); Rest(mat0, 0f, null); float ms = B(mat0).size.x;
            Object.DestroyImmediate(mat0);
            foreach (var (cx, cz2) in new[] { (-100f, 40f), (90f, -110f), (-40f, -115f), (-150f, -20f), (60f, -25f), (30f, 100f) })   // none on the maze (the (150, 90) cluster covered its walls, 5 Nov)
                for (int i = 0; i < 4; i++) { var m = Spawn("Mat_0" + (1 + (i + (int)Mathf.Abs(cx)) % 4), "Mat", r4, c + new Vector3(cx + (i % 2 - 0.5f) * ms, 0, cz2 + (i / 2 - 0.5f) * ms), new Vector3(0, 90 * ((i * 3) % 4), 0), 1f); Rest(m, 0.05f, null); }
            // wall art: posters and drawings on all four walls, a clock, sockets; the north wall's window bay stays bare
            void OnWall(string prefab, char wall, float along, float y, float yaw)
            {
                Vector3 pos = wall == 'S' ? c + new Vector3(along, y, -R4HalfZ) : wall == 'N' ? c + new Vector3(along, y, R4HalfZ) : wall == 'W' ? c + new Vector3(-R4HalfX, y, along) : c + new Vector3(R4HalfX, y, along);
                var g = Spawn(prefab, prefab, r4, pos, new Vector3(0, yaw, 0), 1f); var gb = B(g);
                if (wall == 'S') g.transform.position += new Vector3(0, 0, (c.z - R4HalfZ + 0.3f) - gb.min.z);
                else if (wall == 'N') g.transform.position += new Vector3(0, 0, (c.z + R4HalfZ - 0.3f) - gb.max.z);
                else if (wall == 'W') g.transform.position += new Vector3((c.x - R4HalfX + 0.3f) - gb.min.x, 0, 0);
                else g.transform.position += new Vector3((c.x + R4HalfX - 0.3f) - gb.max.x, 0, 0);
            }
            foreach (var (n, x, y) in new[] { ("Poster_03", -120f, 80f), ("Poster_09", 60f, 82f), ("Poster_14", 150f, 84f), ("Painting_01", -40f, 90f), ("Poster_07", 180f, 70f) }) OnWall(n, 'S', x, y, 0f);
            foreach (var (n, x, y) in new[] { ("Poster_01", -150f, 84f), ("Poster_11", 172f, 86f), ("Poster_10", -100f, 92f), ("Painting_02", 105f, 94f) }) OnWall(n, 'N', x, y, 180f);
            foreach (var (n, z, y) in new[] { ("Poster_06", -20f, 86f), ("Clock_01", -70f, 100f), ("Poster_13", 120f, 84f), ("Poster_15", -130f, 80f) }) OnWall(n, 'W', z, y, 90f);
            foreach (var (n, z, y) in new[] { ("Poster_12", 110f, 82f), ("Poster_05", -30f, 88f), ("Poster_02", 60f, 96f), ("Poster_08", -120f, 84f) }) OnWall(n, 'E', z, y, -90f);
            Scatter(new[] { "Drawing_02", "Drawing_05", "Drawing_12", "Drawing_17", "Drawing_21", "Drawing_33", "Drawing_40", "Drawing_48" }, c, 170f, 12);   // kids' drawings: loose paper on the floor
            foreach (var (z, wall) in new[] { (-30f, 'W'), (100f, 'E') }) OnWall("Socket_01", wall, z, 12f, wall == 'W' ? 90f : -90f);
            // the ceiling fan at the room's centre, the planet lamp moved off it
            var fan = Spawn("Fan_01", "CeilingFan", r4, c + new Vector3(0, 100f, 0), Vector3.zero, 1f); var fb = B(fan); fan.transform.position += new Vector3(0, (c.y + 129.5f) - fb.max.y, 0);
            Transform blades = null; foreach (Transform ch in fan.GetComponentsInChildren<Transform>()) if (ch != fan.transform && ch.name.Contains("Propeller")) { blades = ch; break; }
            var fanReactor = React(fan, Reactor.Response.Spin, "Pull", Reactor.Need.Kid, continuous: true, part: blades); Set(fanReactor, "spinSpeed", 360f);
            var fanSwitch = Spawn("Switch_02", "FanSwitch", r4, c + new Vector3(R4HalfX, 50f, 60f), new Vector3(0, -90, 0), 1f); var fsb = B(fanSwitch); fanSwitch.transform.position += new Vector3((c.x + R4HalfX - 0.3f) - fsb.max.x, 0, 0);
            var sw2 = fanSwitch.AddComponent<Switch>(); Set(sw2, "once", false); Set(sw2, "requiredSize", (int)ShiftSize.Kid);
            { var sso = new SerializedObject(sw2); var calls = sso.FindProperty("onActivated.m_PersistentCalls.m_Calls"); calls.arraySize = 1; Listener(calls.GetArrayElementAtIndex(0), fanReactor, typeof(Reactor).AssemblyQualifiedName, "Use"); sso.ApplyModifiedPropertiesWithoutUndo(); }
            var planet = r4.Find("PlanetLamp"); if (planet != null) planet.position = c + new Vector3(-110f, 130f, 100f);
            // floor clutter: paper, pencils, chalk, cards, the small stuff of a lived-in floor, nowhere a beat needs clear ground
            Scatter(new[] { "Note_Paper_01", "Note_Paper_02", "Note_Paper_04", "Note_Paper_05", "Note_Paper_06", "PlayCard_01", "PlayCard_01" }, c, 175f, 60);
            Scatter(new[] { "Pencil_01", "Pencil_02", "Chalk_01", "Chalk_03", "Chalk_05", "Eraser_01", "Clip_02", "Icecream_Stick_01", "Matchbox_01", "Bottle_Cap_01" }, c, 175f, 50, Pushable.Weight.Light);
            Scatter(new[] { "Paper_Plane_01", "Paper_Plane_02", "CD_01", "CD_02", "Cup_03", "Paper_Glass_01", "Can_02" }, c, 165f, 22, Pushable.Weight.Light);
            // no Baloon_xx (particle emitters, 900 balloons each) and no Sticker_01 (a wall decal: it stands up on a floor)
            Scatter(new[] { "Cube_Alphabet_01", "Cube_Alphabet_02", "Cube_Alphabet_03", "Color_Cube_01", "Color_Cube_03", "Wooden_Cone_01", "Wooden_HalfSphere_01" }, c + new Vector3(-30f, 0, -40f), 140f, 24, Pushable.Weight.Light);
            Scatter(new[] { "Notebook_01", "Notebook_02", "Notebook_03", "Notebook_04", "Paper_Block_01", "Puzzle_03", "Puzzle_09", "Puzzle_18" }, c, 165f, 18);
            Scatter(new[] { "Book_03", "Book_06", "Book_08", "Book_10", "Book_12" }, c + new Vector3(60f, 0, 60f), 120f, 10, Pushable.Weight.Heavy);
            // a few book stacks and open boxes where a kid left them
            foreach (var (bx, bz) in new[] { (-70f, 110f), (130f, -20f), (-150f, -140f), (40f, -150f) })
            {
                var at = c + new Vector3(bx, 0, bz); if (!Free(at)) continue;
                var first = Prop("Book_02", at, Rnd(-25, 25), 0f, null, 10f); if (first == null) continue; float top = B(first).max.y;
                foreach (var n in new[] { "Book_07", "Book_11" }) { var bk = Spawn(n, n, r4, first.transform.position, new Vector3(0, Rnd(-25, 25), 0), 1f); RestOn(bk, top); top = B(bk).max.y; blocked.Add(B(bk)); }
            }
            foreach (var (n, bx, bz, yaw) in new[] { ("Cardboard_Box_01", -175f, 140f, 20f), ("Cardboard_Box_03", 170f, 140f, -30f), ("Cardboard_Box_02", 175f, -140f, 100f) }) Carry(Prop(n, c + new Vector3(bx, 0, bz), yaw));
            Log.AppendLine("R4 dressed: our composition, pack props");

            // ---- R6: the window on the north wall, a sill, the last core, the goal, and the garden outside
            var win = Spawn("Window_01", "Window", r6, new Vector3(c.x, 43f, c.z + R4HalfZ - 2f), new Vector3(0, 180, 0), 1f); var nb = B(win);
            float sillY = Mathf.Max(nb.min.y, 40f); Log.AppendLine("window " + nb.min + ".." + nb.max + " sill " + sillY);
            Cube("Sill", r6, new Vector3(c.x, sillY - 2f, nz - 2.5f - 6f), new Vector3(44f, 4f, 12f), Mat("Assets/_Game/Data/Wardrobe_Wood.mat", new Color(0.36f, 0.23f, 0.13f)));
            var coreD = Core("CoreD_Window", r6, new Vector3(c.x - 12f, sillY + 2.5f, nz - 2.5f - 6f), ShiftSource.Mode.Toggle, coreMat, false);
            Set(coreD.GetComponent<ShiftSource>(), "standWithin", CoreStandWithin);   // used from the sill only, so the toy is shrunk at the goal (D-0084)
            var goal = Trigger("Goal", r6, new Vector3(c.x + 8f, sillY + 2f, nz - 2.5f - 6f), new Vector3(16f, 6f, 10f));
            var gc = goal.AddComponent<Goal>(); Set(gc, "requiredSize", (int)ShiftSize.Toy);
            Set(gc, "message", "");
            var flow = BuildHud();
            // the one balloon beat (D-0022): a single burst of balloons rising from the sill when the toy gets home
            var party = Spawn("Baloon_01", "GoalBalloons", r6, new Vector3(c.x, sillY + 1f, nz - 2.5f - 6f), Vector3.zero, 1f);
            ParticleSystem balloons = null;
            foreach (var ps in party.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main; main.loop = false; main.playOnAwake = false; main.maxParticles = 60; main.duration = 1f;
                var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); if (balloons == null) balloons = ps;
            }
            {
                var gso = new SerializedObject(gc); var calls = gso.FindProperty("onReached.m_PersistentCalls.m_Calls");
                calls.arraySize = (flow != null ? 1 : 0) + (balloons != null ? 1 : 0); int ci = 0;
                if (flow != null) Listener(calls.GetArrayElementAtIndex(ci++), flow, typeof(GameFlow).AssemblyQualifiedName, "End");
                if (balloons != null) Listener(calls.GetArrayElementAtIndex(ci++), balloons, typeof(ParticleSystem).AssemblyQualifiedName, "Play");
                gso.ApplyModifiedPropertiesWithoutUndo();
            }
            Cube("Ground", r6, new Vector3(700f, -1.5f, nz + 400f), new Vector3(1000f, 2f, 800f), grass);
            var rnd = new System.Random(7);
            var trees = new List<Vector2>();
            for (int i = 0, tries = 0; i < 24 && tries < 400; tries++)
            {
                float tx = 260f + (float)(rnd.NextDouble() * 880), tz = nz + 60f + (float)(rnd.NextDouble() * 600);
                if (trees.Any(t => (t - new Vector2(tx, tz)).sqrMagnitude < 95f * 95f)) continue;   // a crown is ~90 across at 35x
                trees.Add(new Vector2(tx, tz)); i++;
                var tree = Spawn("Toy_Tree_01", "GardenTree", r6, new Vector3(tx, 0, tz), new Vector3(0, (float)(rnd.NextDouble() * 360), 0), 35f); Rest(tree, 0f, null);
            }
            PointLight("GardenSun", r6, new Vector3(c.x, 200f, nz + 250f), 900f, 2.5f, new Color(1f, 0.95f, 0.85f));
            Log.AppendLine("R4 playroom + R6 window built at " + c);
        }

        // =====================================================================================
        // R5: inside the wardrobe

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
                string[] cands = { "Wall_01", "Wall_02", "Wall_03", "Wall_04", "Wall_05", "Window_01", "Doorway_01", "Fan_01", "Cardboard_Box_01", "Cardboard_Box_02", "Cardboard_Box_03", "Cardboard_Box_04", "Cardboard_Box_05", "Cardboard_Box_06", "Chair_01", "Paper_Block_01", "Matchbox_01", "Wooden_Cube_01" };
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
            NoShiftInside(r5);
            // three shelves along the back half, 40 apart; the climb zigzags up them on book stairs
            float[] shelfY = { 40f, 80f };
            Cube("Shelf_" + shelfY[0], r5, c + new Vector3(0, shelfY[0] - 1f, hd / 2f), new Vector3(hw * 2, 2f, hd), wood);               // full depth
            Cube("Shelf_" + shelfY[1], r5, c + new Vector3(0, shelfY[1] - 1f, hd * 0.75f), new Vector3(hw * 2, 2f, hd / 2f), wood);         // back half only: open air above shelf 1's front
            // flight 0: floor -> shelf 1, running +x along the back wall from the left
            BookStairs(r5, c + new Vector3(-hw + 6f, 0, hd * 0.5f), Vector3.right, 0f, shelfY[0], "Flight0");
            {   // shelf 1 covers the back half (z 0..+60): a flight laid along the back wall ran UNDER it, and its top books met the
                // shelf's underside, not its top. It stands in front of the shelf's edge instead, so its top book (40) meets the
                // shelf's top (walk check 19 Nov, D-0081)
                var flight = r5.Find("Flight0"); flight.position += Vector3.forward * ((c.z - 0.05f) - B(flight.gameObject).max.z);
            }
            // flight 1 is a trampoline: a pillow on shelf 1's open front, just in front of the lip of shelf 2 (z +30); drift back in
            // the air to land on it. It stood at z +12: 18 to drift once over the lip, and the toy drifts about 14 at a sprint
            // (sprint 8 since D-0039) in the time it is above shelf 2, so it fell short every time (walk check 19 Nov, D-0081).
            // At +21 it is about 8: a walk makes it.
            var pillow = Rest(Spawn("Pillow_01", "Trampoline", r5, c + new Vector3(hw - 30f, 0, hd * 0.35f), new Vector3(0, 10, 0), 1f), shelfY[0], null);
            PillowPad(pillow, (shelfY[1] - B(pillow).max.y) + 10f);
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
            var beam = beamGo.AddComponent<Light>(); beam.type = LightType.Spot; beam.spotAngle = 50f; beam.innerSpotAngle = 30f; beam.range = 120f;   // core C is 95 from the beam's start: at 90 the beam never reached it (walk check 19 Nov, D-0081) beam.intensity = 4f; beam.shadows = LightShadows.Soft; beam.enabled = false;
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
            var end = Card("EndCard", "You made it to the window.", EndCardChoices);
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
        /// <summary>A staircase of books from the floor (y0) up to y1 along dir, 2.4 along each: every book's TOP is one even
        /// riser (at most 1.2) above the last, whatever the book's thickness (the pack's books run 1 to 2.4 thick; set by
        /// their bottoms the risers came out 0.4 to 2.4, and 2.3 / 2.4 are over the toy's 2.0 jump: walk check 19 Nov, D-0081).
        /// The last book's top is y1.</summary>
        static void BookStairs(Transform parent, Vector3 from, Vector3 dir, float y0, float y1, string name)
        {
            var holder = Child(parent, name); int n = Mathf.CeilToInt(Mathf.Abs(y1 - y0) / 1.2f); float step = (y1 - y0) / Mathf.Max(1, n);
            string[] books = { "Book_01", "Book_02", "Book_03", "Book_04", "Book_06", "Book_07", "Book_08", "Book_09", "Book_10", "Book_11", "Book_12" };
            float yaw = Quaternion.LookRotation(dir).eulerAngles.y;
            // The books lie along dir and are longer than a tread, and each title is a different length: every book's BACK end goes
            // at from + i * 2.4, so each one shows exactly one 2.4 tread before the next begins. Set by their middles, a longer
            // book reached back over the one before it and the first step was two or three risers high (D-0081).
            Vector3 along = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
            for (int i = 0; i <= n; i++)
            {
                var b = Spawn(books[i % books.Length], "Book" + i, holder, from + dir * (i * 2.4f), new Vector3(0, yaw, 0), 1f);
                { var b0 = B(b); float back = Vector3.Dot(b0.center - b0.min, along); float off = Vector3.Dot(b0.center - (from + dir * (i * 2.4f)), dir);
                  b.transform.position += dir * (back - off); }
                var bb = B(b);
                float top = y0 + (i + 1) * step;   // book 0's top is one riser up; the book after the last tops out at y1 + step (none)
                if (i == n) top = y1;
                b.transform.position += Vector3.up * (top - bb.max.y);
            }
        }
        /// <summary>A square ring of Army_Sandbags_01 (9 long) with one side's middle piece left out (the gap faces gapAngle:
        /// 90 = the +x side, 270 = the -x side), <paramref name="layers"/> sandbags high. One sandbag is 1.57 high and the toy
        /// jumps 2.0 (D-0039), so a ring one sandbag high is hopped: two (3.1) make it a maze again (walk check 19 Nov, D-0081).</summary>
        static void SandbagRing(Transform parent, Vector3 centre, float half, float gapAngle, int layers = 2)
        {
            for (int layer = 0; layer < layers; layer++)
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90f; Vector3 normal = Quaternion.Euler(0, yaw, 0) * Vector3.forward; Vector3 along = Quaternion.Euler(0, yaw, 0) * Vector3.right;
                int pieces = Mathf.RoundToInt(half * 2f / 9f);
                for (int i = 0; i < pieces; i++)
                {
                    bool middle = i == pieces / 2;
                    if (middle && Mathf.Abs(Mathf.DeltaAngle(yaw, gapAngle)) < 1f) continue;
                    Vector3 p = centre + normal * half + along * (-half + 4.5f + i * 9f);
                    var bag = Spawn("Army_Sandbags_01", "Sandbag", parent, p, new Vector3(0, yaw, 0), 1f);
                    Rest(bag, centre.y + layer * B(bag).size.y, null);
                }
            }
        }
        /// <summary>A ruler (at 1.5x) laid as a ramp whose TOP surface runs from the floor point up to <paramref name="top"/>,
        /// climbing along <paramref name="heading"/> by <paramref name="rise"/>: flush at both ends. Returns the horizontal run.</summary>
        static float Ramp(string prefab, string name, Transform parent, Vector3 top, Vector3 heading, float rise)
        {
            var go = Spawn(prefab, name, parent, Vector3.zero, Vector3.zero, 1.5f); var b = B(go);
            bool alongX = b.size.x >= b.size.z; float length = Mathf.Max(b.size.x, b.size.z), thick = b.size.y;
            float run = Mathf.Sqrt(Mathf.Max(1f, length * length - rise * rise));
            Vector3 flat = new Vector3(heading.x, 0, heading.z).normalized;
            Vector3 from = top - flat * run - Vector3.up * rise, dir = (top - from).normalized;
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            go.transform.rotation = look * (alongX ? Quaternion.Euler(0, -90f, 0) : Quaternion.identity);
            Vector3 want = (from + top) * 0.5f - (look * Vector3.up) * (thick * 0.5f);
            go.transform.position += want - B(go).center;
            Log.AppendLine(name + " from " + from + " to " + top + " slope " + Mathf.Atan2(rise, run) * Mathf.Rad2Deg + " deg");
            return run;
        }

        /// <summary>A loose prop the player shoves: convex colliders, a body, a weight class (D-0020).</summary>
        static GameObject Push(GameObject go, Pushable.Weight weight, float mass)
        {
            if (go == null) return null;
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>()) mc.convex = true;
            if (go.GetComponentInChildren<Collider>() == null) go.AddComponent<BoxCollider>();
            if (!go.TryGetComponent(out Rigidbody _)) go.AddComponent<Rigidbody>();
            if (!go.TryGetComponent(out Pushable p)) p = go.AddComponent<Pushable>();
            Set(p, "weight", (int)weight); Set(p, "mass", mass);
            return go;
        }
        /// <summary>A prop that answers E (D-0020).</summary>
        static Reactor React(GameObject go, Reactor.Response response, string prompt, Reactor.Need need = Reactor.Need.Any, bool continuous = false, Transform part = null, Light lamp = null)
        {
            if (go == null) return null;
            if (!go.TryGetComponent(out Reactor re)) re = go.AddComponent<Reactor>();
            Set(re, "response", (int)response); Set(re, "prompt", prompt); Set(re, "need", (int)need); Set(re, "continuous", continuous);
            Set(re, "wrongSizePrompt", need == Reactor.Need.Kid ? "Too small to reach" : "Too big");
            if (part != null) Set(re, "part", part);
            if (lamp != null) Set(re, "lamp", lamp);
            return re;
        }
        static AudioClip Clip(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:AudioClip", new[] { Pack })) { var path = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<AudioClip>(path); }
            Log.AppendLine("no clip " + name); return null;
        }
        static void SetArray(Object target, string field, params Object[] items)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field); if (p == null) return;
            p.arraySize = items.Length; for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Remakes Room/ToyBox as a real cardboard box (D-0022): four walls in the pack's corrugated cardboard, two top
        /// flaps hinged on the long walls (children of them, so they fold out with them), packing tape over the seam and down
        /// the ends, a shipping label; BoxBurst wired with flaps, dust and sounds. Keeps the floor, flashlight, bricks and core.</summary>
        [MenuItem("Shift Jam/Rebuild Toy Box")]
        public static void RebuildToyBox()
        {
            var box = GameObject.Find("Room/ToyBox"); if (box == null) { Debug.LogError("no Room/ToyBox"); return; }
            foreach (var n in new[] { "FrontWall", "BackWall", "LeftWall", "RightWall", "Lid" }) { var o = box.transform.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
            var floor = box.transform.Find("Floor");
            var card = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/ToyBoxCardboard.mat");
            if (card == null) { card = new Material(AssetDatabase.LoadAssetAtPath<Material>(Pack + "/Materials/Cardboard_1.mat")); card.SetTextureScale("_BaseMap", new Vector2(2.5f, 2.3f)); AssetDatabase.CreateAsset(card, "Assets/_Game/Data/ToyBoxCardboard.mat"); }
            var tape = Mat("Assets/_Game/Data/PackingTape.mat", new Color(0.78f, 0.62f, 0.38f)); tape.SetFloat("_Smoothness", 0.75f);
            if (floor != null) floor.GetComponent<Renderer>().sharedMaterial = card;
            // the box: x -15..15, z -32..-8 around (0, -20), walls 28 high and 1 thick standing on the floor
            // the box stands on its own floor board (the ToyBox object's origin is not the box's middle)
            Vector3 c = floor != null ? new Vector3(floor.position.x, 0f, floor.position.z) : new Vector3(box.transform.position.x, 0f, box.transform.position.z);
            const float W = 30f, D = 24f, H = 28f, T = 1f;
            // A board is an UNSCALED holder with a stretched cube named "Mesh" under it: holders can parent other boards without
            // passing their stretch on (a flap folding inside a 32x28x1 cube skewed to 363 long, 5 Nov).
            GameObject Panel(string name, Transform parent, Vector3 centre, Vector3 size, Material m, bool solid = true)
            {
                var holder = new GameObject(name); holder.transform.SetParent(parent, false); holder.transform.position = centre; holder.transform.rotation = Quaternion.identity;
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "Mesh"; g.transform.SetParent(holder.transform, false);
                g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = m;
                if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
                return holder;
            }
            var front = Panel("FrontWall", box.transform, c + new Vector3(0, H / 2f, D / 2f + T / 2f), new Vector3(W + 2f * T, H, T), card);
            var back = Panel("BackWall", box.transform, c + new Vector3(0, H / 2f, -D / 2f - T / 2f), new Vector3(W + 2f * T, H, T), card);
            var left = Panel("LeftWall", box.transform, c + new Vector3(-W / 2f + T / 2f, H / 2f, 0), new Vector3(T, H, D), card);
            var right = Panel("RightWall", box.transform, c + new Vector3(W / 2f - T / 2f, H / 2f, 0), new Vector3(T, H, D), card);
            var flapF = Panel("FlapFront", front.transform, c + new Vector3(0, H + T / 2f, D / 4f + T / 2f), new Vector3(W + 2f * T, T, D / 2f + T), card);
            var flapB = Panel("FlapBack", back.transform, c + new Vector3(0, H + T / 2f, -D / 4f - T / 2f), new Vector3(W + 2f * T, T, D / 2f + T), card);
            // packing tape: a strip on each flap over the seam, and down each end wall
            Panel("Tape", flapF.transform, c + new Vector3(0, H + T + 0.06f, 1.5f), new Vector3(W * 0.9f, 0.12f, 3f), tape, false);
            Panel("Tape", flapB.transform, c + new Vector3(0, H + T + 0.06f, -1.5f), new Vector3(W * 0.9f, 0.12f, 3f), tape, false);
            Panel("Tape", front.transform, c + new Vector3(0, H - 4f, D / 2f + T + 0.06f), new Vector3(3f, 8f, 0.12f), tape, false);
            Panel("Tape", back.transform, c + new Vector3(0, H - 4f, -D / 2f - T - 0.06f), new Vector3(3f, 8f, 0.12f), tape, false);
            // a plain shipping label on the outside of the front wall (the pack's sticky note read as a giant green square)
            var paper = Mat("Assets/_Game/Data/ShippingLabel.mat", new Color(0.93f, 0.91f, 0.84f));
            var ink = Mat("Assets/_Game/Data/LabelInk.mat", new Color(0.25f, 0.22f, 0.2f));
            var label = Panel("Label", front.transform, c + new Vector3(-7f, 12f, D / 2f + T + 0.06f), new Vector3(8f, 5f, 0.1f), paper, false);
            for (int i = 0; i < 3; i++) Panel("Line", label.transform, c + new Vector3(-7.6f + (i == 2 ? -1f : 0f), 13.2f - i * 1.2f, D / 2f + T + 0.12f), new Vector3(i == 2 ? 4f : 6f, 0.35f, 0.05f), ink, false);
            var bb = box.GetComponent<BoxBurst>() ?? box.AddComponent<BoxBurst>();
            var so = new SerializedObject(bb);
            var pp = so.FindProperty("panels"); pp.arraySize = 4; pp.GetArrayElementAtIndex(0).objectReferenceValue = front.transform; pp.GetArrayElementAtIndex(1).objectReferenceValue = back.transform; pp.GetArrayElementAtIndex(2).objectReferenceValue = left.transform; pp.GetArrayElementAtIndex(3).objectReferenceValue = right.transform;
            var fp = so.FindProperty("flaps"); fp.arraySize = 2; fp.GetArrayElementAtIndex(0).objectReferenceValue = flapF.transform; fp.GetArrayElementAtIndex(1).objectReferenceValue = flapB.transform;
            so.FindProperty("lid").objectReferenceValue = null;
            var dustGuid = AssetDatabase.FindAssets("FX_Dust t:Prefab", new[] { Pack }); if (dustGuid.Length > 0) so.FindProperty("dust").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(dustGuid[0]));
            so.FindProperty("popSound").objectReferenceValue = Clip("Paper_Rustle_v1");
            so.FindProperty("landSound").objectReferenceValue = Clip("Books_Falling_v1");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(box.scene);
            Debug.Log("TOY BOX REBUILT: 4 walls, 2 flaps, tape, label; BoxBurst wired");
        }

        /// <summary>Couch pillows (D-0025): the kid carries them where they are needed, the toy bounces on them. Each throws
        /// the toy clear of the sofa's back wherever it is put down. Skips a pillow that already has its pad.</summary>
        static int CouchPillowPads(Transform r4)
        {
            var sofa = r4.Find("Sofa"); int made = 0;
            foreach (var n in new[] { "Pillow", "Pillow2" })
            {
                var p = r4.Find(n); if (p == null || p.Find("Pad") != null) continue;
                Carry(p.gameObject);
                float h = sofa != null ? B(sofa.gameObject).max.y - B(p.gameObject).max.y + 6f : 30f;
                PillowPad(p.gameObject, Mathf.Max(h, 12f)); made++;
            }
            return made;
        }

        /// <summary>A toy-only launch pad on a pillow: a trigger over its top, a soft glow on its crown, the pillow squashes on
        /// each bounce. Pad and glow are the pillow's children, so they travel when the kid carries it (the Carrier switches them
        /// off in hand).</summary>
        static void PillowPad(GameObject pillow, float height)
        {
            var pb = PillowBounds(pillow);
            var padTrig = Trigger("Pad", pillow.transform, new Vector3(pb.center.x, pb.max.y + 0.6f, pb.center.z), new Vector3(pb.size.x * 0.8f, 1.6f, pb.size.z * 0.8f));
            var pad = padTrig.AddComponent<LaunchPad>(); Set(pad, "height", height); Set(pad, "visual", pillow.transform);
            FitPadGlow(pillow);
            FitSideBounce(pillow);
        }

        const float SideMargin = 1f;   // how far the side trigger reaches out from the pillow, world units at toy scale

        /// <summary>D-0039: a toy-only side trigger wrapping the pillow, floor to crown, a little wider than it. Running into
        /// it throws the toy just over the crown and inward, onto the pad (LaunchPad side mode). Measured in the pillow's own
        /// space, so it turns and scales with it. Adds it, or re-fits the one already there.</summary>
        static void FitSideBounce(GameObject pillow)
        {
            var old = pillow.transform.Find("Side");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (!BuildBounds.Local(pillow.transform, out Bounds lb)) return;
            float s = Mathf.Max(1e-4f, Mathf.Abs(pillow.transform.lossyScale.x));
            var t = new GameObject("Side"); t.transform.SetParent(pillow.transform, false);
            t.transform.localPosition = lb.center;
            var bc = t.AddComponent<BoxCollider>(); bc.isTrigger = true;
            bc.size = new Vector3(lb.size.x + 2f * SideMargin / s, lb.size.y, lb.size.z + 2f * SideMargin / s);
            var side = t.AddComponent<LaunchPad>(); Set(side, "side", true);
        }

        /// <summary>Gives every pillow pad in the open scenes and in the PillowPad piece its side bounce. Safe to run again.</summary>
        [MenuItem("Shift Jam/Add Pillow Side Bounces")]
        public static void AddPillowSideBounces()
        {
            var log = new StringBuilder("PILLOW SIDE BOUNCES:");
            int n = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i); if (!s.isLoaded) continue;
                var pillows = new List<GameObject>();
                foreach (var root in s.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name == "PadGlow" && t.parent != null) pillows.Add(t.parent.gameObject);
                foreach (var p in pillows) { FitSideBounce(p); n++; log.Append($" {s.name}/{p.name} {DescribeSide(p)};"); }
                if (pillows.Count > 0) EditorSceneManager.MarkSceneDirty(s);
            }
            const string piece = "Assets/_Game/Prefabs/Pieces/Traversal/PillowPad.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(piece) != null)
            {
                var root = PrefabUtility.LoadPrefabContents(piece);
                var pillows = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "PadGlow" && t.parent != null).Select(t => t.parent.gameObject).ToList();
                foreach (var p in pillows) { FitSideBounce(p); n++; log.Append($" PillowPad.prefab/{p.name} {DescribeSide(p)};"); }
                PrefabUtility.SaveAsPrefabAsset(root, piece);
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log(log.Append($" {n} pillow(s). Save the scene(s).").ToString());
        }

        /// <summary>D-0039: a starter red pair of car portals in the Workshop, on clear floor away from the race loop and the
        /// boost lane, as far apart as the room allows, each facing the room's middle. Skips if a red portal is already
        /// there. The search reads every drawn thing's box, so it never lands a hoop inside furniture.</summary>
        [MenuItem("Shift Jam/Workshop/Place Starter Car Portals")]
        public static void PlaceStarterPortals()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Workshop") { Debug.LogError("Open Workshop.unity first."); return; }
            var layout = GameObject.Find("Workshop/Layout");
            if (layout == null) { Debug.LogError("No Workshop/Layout."); return; }
            if (Object.FindObjectsByType<CarPortal>(FindObjectsSortMode.None).Any(p => p.Colour == PortalColour.Red)) { Debug.Log("CAR PORTALS: a red pair is already placed."); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Pieces/Traversal/CarPortal_Red.prefab");
            if (prefab == null) { Debug.LogError("Run Shift Jam > Workshop > Build Car Portals first."); return; }

            var solids = new List<Bounds>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds; if (b.max.y < 0.6f) continue;   // floor tiles, rugs and floor glows
                solids.Add(b);
            }
            // keep the race loop (finish (0,30), gates out to x +-130, z 150) and the boost lane (x 190) free
            bool Reserved(Vector3 p) => (Mathf.Abs(p.x) < 165f && p.z > -5f && p.z < 185f) || (p.x > 165f && p.x < 220f && p.z < 110f);
            bool Clear(Vector3 p, float yaw)
            {
                // the hoop plus a run-in and a run-out lane, 14 wide and 50 long along its facing
                bool alongZ = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) < 0.5f;
                var box = new Bounds(p + Vector3.up * 5f, alongZ ? new Vector3(14f, 9f, 50f) : new Vector3(50f, 9f, 14f));
                if (Mathf.Abs(box.min.x) > 250f || Mathf.Abs(box.max.x) > 250f || Mathf.Abs(box.min.z) > 250f || Mathf.Abs(box.max.z) > 250f) return false;
                foreach (var s in solids) if (s.Intersects(box)) return false;
                return true;
            }
            float FacingMiddle(Vector3 p) => Mathf.Abs(p.x) > Mathf.Abs(p.z) ? (p.x > 0f ? 270f : 90f) : (p.z > 0f ? 180f : 0f);
            var spots = new List<(Vector3 p, float yaw)>();
            for (float x = -220f; x <= 220f; x += 10f)
                for (float z = -220f; z <= 220f; z += 10f)
                {
                    var p = new Vector3(x, 0f, z); if (Reserved(p)) continue;
                    float yaw = FacingMiddle(p);
                    if (Clear(p, yaw)) spots.Add((p, yaw));
                }
            if (spots.Count < 2) { Debug.LogError($"CAR PORTALS: only {spots.Count} clear spot(s); place them in Build Mode."); return; }
            var pick = spots[0]; var other = spots[1]; float best = 0f;
            foreach (var a in spots) foreach (var c in spots)
            {
                float d = (a.p - c.p).sqrMagnitude; if (d > best) { best = d; pick = a; other = c; }
            }
            foreach (var (p, yaw, n) in new[] { (pick.p, pick.yaw, "CarPortal_Red_A"), (other.p, other.yaw, "CarPortal_Red_B") })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, layout.transform);
                go.name = n; go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"CAR PORTALS: red pair at ({pick.p.x:0},{pick.p.z:0}) yaw {pick.yaw:0} and ({other.p.x:0},{other.p.z:0}) yaw {other.yaw:0}; {spots.Count} clear spots. Save the scene.");
        }

        /// <summary>D-0043: the Workshop had no shift core, so outside the editor (no F2 Pocket Core) a player there could never
        /// change size. Nine course-style cyan Toggle cores on a 3x3 grid (x and z at -170 / 0 / +170), each slid to the
        /// nearest clear floor within 80 units (furniture boxes, the race loop and the boost lane kept free, 60 apart), so the
        /// toy is never far from one. Re-running replaces them (names WorkshopCore_*); a grid spot with no clear floor is
        /// skipped and logged.</summary>
        [MenuItem("Shift Jam/Workshop/Place Workshop Cores")]
        public static void PlaceWorkshopCores()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Workshop") { Debug.LogError("Open Workshop.unity first."); return; }
            var layout = GameObject.Find("Workshop/Layout");
            if (layout == null) { Debug.LogError("No Workshop/Layout."); return; }
            var coreMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/ShiftCore.mat");
            foreach (Transform t in layout.transform.Cast<Transform>().ToList())
                if (t.name.StartsWith("WorkshopCore_", System.StringComparison.Ordinal)) Object.DestroyImmediate(t.gameObject);

            var solids = new List<Bounds>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds; if (b.max.y < 0.6f) continue;   // floor tiles, rugs and floor glows
                solids.Add(b);
            }
            // the same keep-outs as the car portals: the race loop and the boost lane
            bool Reserved(Vector3 p) => (Mathf.Abs(p.x) < 165f && p.z > -5f && p.z < 185f) || (p.x > 165f && p.x < 220f && p.z < 110f);
            bool Clear(Vector3 p)
            {
                var box = new Bounds(p + Vector3.up * 4f, new Vector3(16f, 8f, 16f));   // the core's glow plus room to walk up to it
                if (Mathf.Abs(box.min.x) > 245f || Mathf.Abs(box.max.x) > 245f || Mathf.Abs(box.min.z) > 245f || Mathf.Abs(box.max.z) > 245f) return false;
                foreach (var s in solids) if (s.Intersects(box)) return false;
                return true;
            }
            var placed = new List<Vector3>(); var report = new StringBuilder();
            int n = 0;
            foreach (float gz in new[] { -170f, 0f, 170f })
                foreach (float gx in new[] { -170f, 0f, 170f })
                {
                    var target = new Vector3(gx, 0f, gz); Vector3? best = null; float bestD = float.MaxValue;
                    for (float x = gx - 80f; x <= gx + 80f; x += 5f)
                        for (float z = gz - 80f; z <= gz + 80f; z += 5f)
                        {
                            var p = new Vector3(x, 0f, z); float d = (p - target).sqrMagnitude;
                            if (d > 80f * 80f || d >= bestD || Reserved(p) || !Clear(p)) continue;
                            if (placed.Any(q => (q - p).sqrMagnitude < 60f * 60f)) continue;
                            best = p; bestD = d;
                        }
                    if (best == null) { report.AppendLine($"  grid ({gx:0},{gz:0}): no clear floor, skipped"); continue; }
                    var at = best.Value; placed.Add(at); n++;
                    Core("WorkshopCore_" + n, layout.transform, at + Vector3.up * 2.5f, ShiftSource.Mode.Toggle, coreMat, false);
                    report.AppendLine($"  WorkshopCore_{n} at ({at.x:0},{at.z:0}) for grid ({gx:0},{gz:0})");
                }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"WORKSHOP CORES: {n} placed\n{report}{Log}");
        }

        /// <summary>Batch: -executeMethod ShiftJam.CourseBuilder.LandWorkshopCores (no -nographics: it renders a still). Opens
        /// the Workshop, places its cores, saves, and draws a top-down still into %TEMP%/sj_d0043_stills.</summary>
        public static void LandWorkshopCores()
        {
            Log.Clear();
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Workshop.unity");
            PlaceWorkshopCores();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sj_d0043_stills"); System.IO.Directory.CreateDirectory(dir);
            Shot(dir + "/workshop_cores_top.png", new Vector3(0f, 400f, 0f), Quaternion.Euler(90f, 0f, 0f), 0f, 1400, 1400, 270f);
            var first = GameObject.Find("Workshop/Layout/WorkshopCore_1");
            if (first != null)
            {
                Vector3 c = first.transform.position, eye = c + new Vector3(18f, 10f, -22f);
                Shot(dir + "/workshop_core_1.png", eye, Quaternion.LookRotation(c - eye), 55f, 1280, 720);
            }
            Debug.Log("D-0043 LANDED: stills in " + dir);
        }

        /// <summary>
        /// D-0046: what is a puzzle piece, and what each problem needs. A gold glint (Highlight) on every Switch, every
        /// Flashlight and the playroom's Safe and Ruler; a ProblemHint at each door a switch opens ("Shut. A switch opens
        /// it.") and at the wardrobe hole while the safe blocks it (toy only); every dormant core reads "Dark. It needs
        /// light." Scene-wide, so the bedroom (placed by hand on 3 Nov) gets them too. Safe to run again. All wording is a
        /// placeholder.
        /// </summary>
        static void PuzzleCues()
        {
            Material glint = GlintMaterial();
            int marks = 0, hints = 0, dark = 0;
            void Mark(Component piece)
            {
                if (!piece.gameObject.TryGetComponent(out Highlight h)) h = piece.gameObject.AddComponent<Highlight>();
                Set(h, "material", glint);
                marks++;
            }
            foreach (var sw in Object.FindObjectsByType<Switch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Mark(sw);
                var doors = new SerializedObject(sw).FindProperty("doors");
                for (int i = 0; doors != null && i < doors.arraySize; i++)
                    if (doors.GetArrayElementAtIndex(i).objectReferenceValue is Door door)
                    {
                        var hint = HintAt(door.transform, "DoorHint", "Shut. A switch opens it.");
                        Set(hint, "door", door);
                        hints++;
                    }
            }
            foreach (var light in Object.FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Mark(light);
            foreach (var c in Object.FindObjectsByType<Carryable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.name == "Safe" || c.name == "Ruler") Mark(c);
            var hole = GameObject.Find("Room/Course/R4_Playroom/WardrobeHole");
            var safe = GameObject.Find("Room/Course/R4_Playroom/Safe");
            if (hole != null && safe != null)
            {
                var hint = HintAt(hole.transform, "HoleHint", "The safe is in the way. Too heavy for a toy.");
                Set(hint, "anySize", false); Set(hint, "onlyFor", (int)ShiftSize.Toy); Set(hint, "blocker", safe.transform);
                hints++;
            }
            foreach (var core in Object.FindObjectsByType<ShiftSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(core);
                if (!so.FindProperty("startDormant").boolValue) continue;
                so.FindProperty("dormantPrompt").stringValue = "Dark. It needs light.";
                so.ApplyModifiedPropertiesWithoutUndo();
                dark++;
            }
            Log.AppendLine($"Puzzle cues: {marks} glints, {hints} hints, {dark} dark cores");
        }

        /// <summary>A ProblemHint box beside (not under: a door swings) the thing, covering it with room to walk up to it.</summary>
        static ProblemHint HintAt(Transform thing, string label, string message)
        {
            string name = label + "_" + thing.name;
            var parent = thing.parent;
            var old = parent != null ? parent.Find(name) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var renderers = thing.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(thing.position, Vector3.zero);
            if (renderers.Length > 0) { b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = b.center;
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true;
            Vector3 lossy = go.transform.lossyScale;
            Vector3 size = Vector3.Max(b.size + new Vector3(4f, 2f, 4f), new Vector3(6f, 6f, 6f));
            box.size = new Vector3(size.x / Mathf.Max(1e-4f, lossy.x), size.y / Mathf.Max(1e-4f, lossy.y), size.z / Mathf.Max(1e-4f, lossy.z));
            var hint = go.AddComponent<ProblemHint>();
            Set(hint, "message", message);
            return hint;
        }

        /// <summary>The glint's material: the spark's (his drawing replaces the texture), made once as its own asset.</summary>
        static Material GlintMaterial()
        {
            const string path = "Assets/_Game/Data/PuzzleGlint.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var spark = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/Spark.mat");
            if (spark == null) { Log.AppendLine("no Spark.mat: glints left without a material"); return null; }
            m = new Material(spark) { name = "PuzzleGlint" };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Batch: -executeMethod ShiftJam.CourseBuilder.LandPuzzleCues (-nographics is fine). D-0046 without a full
        /// course rebuild: PuzzleCues in Greybox, saved.</summary>
        public static void LandPuzzleCues()
        {
            Log.Clear();
            EditorSceneManager.OpenScene(ScenePath);
            PuzzleCues();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("D-0046 LANDED\n" + Log);
        }

        // =====================================================================================
        // Rides (D-0047): a Cyber car waits where each long toy walk starts
        // =====================================================================================
        const string CarPieces = "Assets/_Game/Prefabs/Pieces/Vehicles/";

        /// <summary>The rides, one row each. <c>from</c> is the scene object the toy's walk starts at and <c>off</c> the way the
        /// toy comes off it (world, flat). The car waits <c>ahead</c> units beyond that face and <c>aside</c> units to the right
        /// of that way, its nose toward <c>toward</c>, the object the walk ends at. Add a row for another ride.</summary>
        static readonly (string name, string piece, string from, Vector3 off, float ahead, float aside, string toward)[] Rides =
        {
            ("Ride_Bedroom", "CyberCar06", "Room/Bedroom/StepBook_1", Vector3.left, 7f, 0f, "Room/Bedroom/Goal"),
            // the playroom's cars wait on the expressway's stops (D-0048, CourseBuilder.Expressway.cs): its floor is mats and clutter
        };

        /// <summary>
        /// D-0047: parks a Cyber car (the Workshop's car pieces, as prefab instances) at the start of each long toy walk, under
        /// Room/Course/Rides. Each spot is read from the scene's own objects (the <see cref="Rides"/> table), so it follows a
        /// rebuilt room. A spot that something already stands in is nudged outward in a small spiral until the car fits.
        /// Replaces the previous Rides; nothing else is touched.
        /// </summary>
        static void PlaceRides(Transform course)
        {
            var old = course.Find("Rides"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var rides = Child(course, "Rides");
            foreach (var r in Rides)
            {
                var from = GameObject.Find(r.from); var toward = GameObject.Find(r.toward);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CarPieces + r.piece + ".prefab");
                if (from == null || toward == null || asset == null)
                {
                    Log.AppendLine($"RIDE SKIPPED {r.name}: " + (from == null ? "no " + r.from : toward == null ? "no " + r.toward : "no piece " + r.piece + " (run Shift Jam > Workshop > Build Combat & Vehicle Pieces)"));
                    continue;
                }
                var fb = B(from);
                Vector3 off = new Vector3(r.off.x, 0f, r.off.z).normalized, right = Vector3.Cross(Vector3.up, off);
                float reach = Mathf.Abs(off.x) * fb.extents.x + Mathf.Abs(off.z) * fb.extents.z;   // from the object's middle to the face the toy leaves by
                Vector3 want = new Vector3(fb.center.x, 0f, fb.center.z) + off * (reach + r.ahead) + right * r.aside;
                Vector3 goal = toward.transform.position;

                var car = (GameObject)PrefabUtility.InstantiatePrefab(asset, rides); car.name = r.name;
                bool fits = false; Vector3 at = want; float ground = 0f;
                for (int k = 0; k < 20 && !fits; k++)
                {
                    if (k > 0) { float a = k * 2.4f, rad = 14f * k / 19f; at = want + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad); }
                    Vector3 flat = new Vector3(goal.x - at.x, 0f, goal.z - at.z);
                    car.transform.SetPositionAndRotation(at, flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : Quaternion.identity);
                    fits = RideFits(car, out ground);
                }
                if (!fits)
                {
                    at = want; Vector3 flat = new Vector3(goal.x - at.x, 0f, goal.z - at.z);
                    car.transform.SetPositionAndRotation(at, flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : Quaternion.identity);
                    RideFits(car, out ground);
                }
                car.transform.position = new Vector3(at.x, ground + 0.02f, at.z);
                float walk = new Vector3(goal.x - at.x, 0f, goal.z - at.z).magnitude; var cb = B(car);
                Log.AppendLine($"{(fits ? "ride" : "RIDE BLOCKED (left at its first spot)")} {r.name} = {r.piece} at {car.transform.position} yaw {car.transform.eulerAngles.y:F0}, moved {(at - want).magnitude:F1} from its first spot, drawn {cb.size.x:F1} x {cb.size.y:F1} x {cb.size.z:F1}, {walk:F0} units to {toward.name}");
            }
            EditorSceneManager.MarkSceneDirty(course.gameObject.scene);
        }

        /// <summary>Whether the car stands free where it is: nothing solid inside its drawn box from just above the ground up
        /// (thin things under 0.4, a mat or a sheet of paper, do not count; it rests on them). <paramref name="ground"/> is the
        /// height of what is under its middle.</summary>
        static bool RideFits(GameObject car, out float ground)
        {
            Physics.SyncTransforms();
            var b = B(car);
            ground = 0f;
            foreach (var h in Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 1f, b.center.z), Vector3.down, b.size.y + 3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (!h.collider.transform.IsChildOf(car.transform)) { ground = h.point.y; break; }
            Vector3 half = new Vector3(b.extents.x + 0.5f, Mathf.Max(0.1f, b.extents.y - 0.2f), b.extents.z + 0.5f);
            Vector3 centre = new Vector3(b.center.x, b.min.y + 0.4f + half.y, b.center.z);
            foreach (var c in Physics.OverlapBox(centre, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(car.transform)) return false;
            return true;
        }

        /// <summary>Batch: -executeMethod ShiftJam.CourseBuilder.LandRides (no -nographics: it renders stills). D-0047 without a
        /// full course rebuild: the rides in Greybox, saved, and two stills of each into %TEMP%/sj_d0047_stills.</summary>
        public static void LandRides()
        {
            Log.Clear();
            EditorSceneManager.OpenScene(ScenePath);
            var course = GameObject.Find("Room/Course");
            if (course == null) { Debug.LogError("D-0047: no Room/Course in " + ScenePath); EditorApplication.Exit(1); return; }
            PlaceRides(course.transform);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sj_d0047_stills"); System.IO.Directory.CreateDirectory(dir);   // not the project Temp: Unity empties it on exit
            foreach (Transform car in course.transform.Find("Rides"))
            {
                Vector3 mid = car.position + Vector3.up * 1f;
                Vector3 near = car.position + car.rotation * new Vector3(-6f, 4.5f, -10f);    // over the toy's shoulder, down the car's nose
                Shot(dir + "/" + car.name + "_near.png", near, Quaternion.LookRotation(mid + car.forward * 6f - near), 60f, 1280, 720);
                Vector3 far = car.position + car.rotation * new Vector3(-25f, 45f, -60f);     // the leg ahead of it
                Shot(dir + "/" + car.name + "_far.png", far, Quaternion.LookRotation(mid + car.forward * 40f - far), 60f, 1280, 720);
            }
            Debug.Log("D-0047 LANDED: stills in " + dir + "\n" + Log);
        }

        /// <summary>
        /// D-0045: the wardrobe interior is a NoShiftZone (grown on its floor, a kid would be shut inside; core C still
        /// works). The zone is the room's own box, from C5 and the sizes BuildWardrobe uses. Replaces an older one.
        /// </summary>
        static void NoShiftInside(Transform r5)
        {
            const float hw = 97.5f, hd = 60f, h = 170f;
            var old = r5.Find("NoShift"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var zone = new GameObject("NoShift");
            zone.transform.SetParent(r5, false);
            zone.transform.position = C5 + new Vector3(0f, h / 2f, 0f);
            var box = zone.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(hw * 2f, h, hd * 2f);
            zone.AddComponent<NoShiftZone>();
            Log.AppendLine("R5 NoShift zone " + box.size + " at " + zone.transform.position);
        }

        /// <summary>Batch: -executeMethod ShiftJam.CourseBuilder.LandShiftCharge (-nographics is fine). D-0045 without a full
        /// course rebuild: the wardrobe's NoShift zone in Greybox (saved) and the refused sound in CourseSounds.</summary>
        public static void LandShiftCharge()
        {
            Log.Clear();
            EditorSceneManager.OpenScene(ScenePath);
            var r5 = GameObject.Find("Room/Course/R5_Wardrobe");
            if (r5 == null) { Debug.LogError("D-0045: no Room/Course/R5_Wardrobe in " + ScenePath); EditorApplication.Exit(1); return; }
            NoShiftInside(r5.transform);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            CourseSoundsBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("D-0045 LANDED\n" + Log);
        }

        /// <summary>Batch: -executeMethod ShiftJam.CourseBuilder.LandPortalsAndPillows (no -nographics: it renders the catalogue
        /// and stills). Builds the car portals, re-runs the catalogue, gives every pillow its side bounce in both scenes,
        /// places the starter red pair, saves, and renders stills of each portal into %TEMP%/sj_d0039_stills.</summary>
        public static void LandPortalsAndPillows()
        {
            RacingPieces.BuildCarPortalsMenu();
            BuildCatalogBuilder.BuildNoWait();
            EditorSceneManager.OpenScene(ScenePath);
            AddPillowSideBounces();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Workshop.unity");
            AddPillowSideBounces();
            PlaceStarterPortals();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sj_d0039_stills"); System.IO.Directory.CreateDirectory(dir);   // not the project Temp: Unity empties it on exit
            Shot(dir + "/workshop_top.png", new Vector3(0f, 400f, 0f), Quaternion.Euler(90f, 0f, 0f), 0f, 1400, 1400, 270f);
            foreach (var p in Object.FindObjectsByType<CarPortal>(FindObjectsSortMode.None))
            {
                var root = p.transform.parent; Vector3 at = root.position;
                Vector3 eye = at + root.rotation * new Vector3(-9f, 6f, -16f);
                Shot(dir + "/" + root.name + ".png", eye, Quaternion.LookRotation(at + Vector3.up * 3f - eye), 60f, 1280, 720);
            }
            foreach (var t in Object.FindObjectsByType<LaunchPad>(FindObjectsSortMode.None))
            {
                if (t.name != "Side") continue;
                var pillow = t.transform.parent; var b = t.GetComponent<Collider>().bounds;
                Vector3 eye = b.center + new Vector3(b.size.x * 1.4f, b.size.y * 1.2f, -b.size.z * 1.6f);
                Shot(dir + "/pillow_" + pillow.name + ".png", eye, Quaternion.LookRotation(b.center - eye), 55f, 1280, 720);
                break;
            }
            Debug.Log("D-0039 LANDED: stills in " + dir);
        }

        static string DescribeSide(GameObject pillow)
        {
            var t = pillow.transform.Find("Side"); var c = t != null ? t.GetComponent<Collider>() : null;
            return c != null ? $"side {c.bounds.size.x:0.#}x{c.bounds.size.y:0.#}x{c.bounds.size.z:0.#} top {c.bounds.max.y:0.#}" : "no side";
        }

        const string PadGlowPath = "Assets/_Game/Data/PadGlow.mat";
        const float PadGlowSize = 0.65f;                                          // share of the pillow's narrower side
        static readonly Color PadGlowColour = new Color(1f, 0.74f, 0.42f, 0.8f);   // the door glow's warm hue, soft

        /// <summary>The pad's mark: the cores' soft round mote, warm, lying flat on the pillow's crown and no wider than its
        /// flatter middle, so nothing hangs in the air. Adds it, or re-fits the one already there (6 Nov: it was a square of the
        /// opaque door-strip material, 0.9 of the pillow wide, floating 0.3 over the crown).</summary>
        static void FitPadGlow(GameObject pillow)
        {
            var pb = PillowBounds(pillow);
            var t = pillow.transform.Find("PadGlow");
            if (t == null) t = Quad("PadGlow", pillow.transform, Vector3.zero, Vector3.zero, Vector3.one, null).transform;
            t.position = new Vector3(pb.center.x, pb.max.y + 0.05f, pb.center.z);
            t.rotation = Quaternion.Euler(90f, 0f, 0f);
            float parentScale = Mathf.Max(1e-4f, Mathf.Abs(pillow.transform.lossyScale.x));
            t.localScale = Vector3.one * (Mathf.Min(pb.size.x, pb.size.z) * PadGlowSize / parentScale);
            var r = t.GetComponent<Renderer>();
            r.sharedMaterial = PadGlowMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>The pillow's own renderers, its glow left out.</summary>
        static Bounds PillowBounds(GameObject pillow)
        {
            var glow = pillow.transform.Find("PadGlow");
            bool any = false; Bounds b = new Bounds(pillow.transform.position, Vector3.zero);
            foreach (var r in pillow.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || (glow != null && r.transform.IsChildOf(glow))) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>PadGlow.mat: a copy of OrbGlow.mat (the cores' soft round mote, additive) tinted warm. Made once.</summary>
        static Material PadGlowMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PadGlowPath);
            if (mat != null) return mat;
            if (!AssetDatabase.CopyAsset("Assets/_Game/Data/OrbGlow.mat", PadGlowPath))
            {
                Debug.LogWarning("Could not copy OrbGlow.mat for the pad glow; using OrbGlow itself.");
                return AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/OrbGlow.mat");
            }
            mat = AssetDatabase.LoadAssetAtPath<Material>(PadGlowPath);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", PadGlowColour);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", PadGlowColour);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>Re-fits every pad glow in the open scenes and in the PillowPad piece. Safe to run again.</summary>
        [MenuItem("Shift Jam/Refit Pad Glows")]
        public static void RefitPadGlows()
        {
            var log = new StringBuilder("PAD GLOWS:");
            int n = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i); if (!s.isLoaded) continue;
                var pillows = new List<GameObject>();
                foreach (var root in s.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name == "PadGlow" && t.parent != null) pillows.Add(t.parent.gameObject);
                foreach (var p in pillows) { FitPadGlow(p); n++; log.Append($" {s.name}/{p.name} {DescribePad(p)};"); }
                if (pillows.Count > 0) EditorSceneManager.MarkSceneDirty(s);
            }
            const string piece = "Assets/_Game/Prefabs/Pieces/Traversal/PillowPad.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(piece) != null)
            {
                var root = PrefabUtility.LoadPrefabContents(piece);
                var pillows = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "PadGlow" && t.parent != null).Select(t => t.parent.gameObject).ToList();
                foreach (var p in pillows) { FitPadGlow(p); n++; log.Append($" PillowPad.prefab/{p.name} {DescribePad(p)};"); }
                PrefabUtility.SaveAsPrefabAsset(root, piece);
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log(log.Append($" {n} re-fitted. Save the scene(s).").ToString());
        }

        static string DescribePad(GameObject pillow)
        {
            var b = PillowBounds(pillow); var g = pillow.transform.Find("PadGlow");
            return $"pillow {b.size.x:0.#}x{b.size.y:0.#}x{b.size.z:0.#}, glow {(g != null ? g.lossyScale.x : 0f):0.#} wide at crown+{(g != null ? g.position.y - b.max.y : 0f):0.##}";
        }

        [MenuItem("Shift Jam/Make Pillows Pads")]
        public static void MakePillowsPads()
        {
            var r4 = GameObject.Find("Room/Course/R4_Playroom"); if (r4 == null) { Debug.LogError("no Room/Course/R4_Playroom"); return; }
            int made = CouchPillowPads(r4.transform);
            EditorSceneManager.MarkSceneDirty(r4.scene);
            Debug.Log($"PILLOW PADS: {made} couch pillow(s) now carryable + toy launch pads");
        }

        // ------------------------------------------------------------------ the Workshop (D-0027)
        // A sandbox room in its own scene (Workshop.unity): an 8x8-tile floor (520 x 520) with Wall_01 walls, no ceiling so
        // the Build Mode camera sees in from above, warm lights, and a starter set of plain furniture under
        // Workshop/Layout (everything there is movable in Build Mode). The player starts in the middle as the toy.
        const float WorkshopHalf = 260f;

        /// <summary>The shell of a sandbox room (the Workshop, the Raceway): an 8 x 8 tile floor, Wall_01 walls, corner
        /// pillars, no ceiling so a camera can look in from above, and three warm lights.</summary>
        static void SandboxShell(Transform shell, Vector3 c, float h)
        {
            for (int i = 0; i < 8; i++) for (int j = 0; j < 8; j++)
                Spawn("Floor_01", "Floor", shell, c + new Vector3(-h + 32.5f + i * 65f, 0, -h + 32.5f + j * 65f), Vector3.zero, 1f);
            for (int i = 0; i < 8; i++)
            {
                float a = -h + 32.5f + i * 65f;
                var s = Spawn("Wall_01", "Wall_S", shell, c + new Vector3(a, 0, -h), Vector3.zero, 1f); var bs = B(s); s.transform.position += new Vector3(0, -bs.min.y, (c.z - h) - bs.max.z);
                var n = Spawn("Wall_01", "Wall_N", shell, c + new Vector3(a, 0, h), new Vector3(0, 180, 0), 1f); var bn = B(n); n.transform.position += new Vector3(0, -bn.min.y, (c.z + h) - bn.min.z);
                var w = Spawn("Wall_01", "Wall_W", shell, c + new Vector3(-h, 0, a), new Vector3(0, 90, 0), 1f); var bw = B(w); w.transform.position += new Vector3((c.x - h) - bw.max.x, -bw.min.y, 0);
                var e = Spawn("Wall_01", "Wall_E", shell, c + new Vector3(h, 0, a), new Vector3(0, -90, 0), 1f); var be = B(e); e.transform.position += new Vector3((c.x + h) - be.min.x, -be.min.y, 0);
            }
            foreach (float x in new[] { -h - 2.5f, h + 2.5f }) foreach (float z in new[] { -h - 2.5f, h + 2.5f })
            { var p = Spawn("Wall_05", "Pillar", shell, c + new Vector3(x, 0, z), Vector3.zero, 1f); var bp = B(p); p.transform.position += new Vector3(c.x + x - bp.center.x, -bp.min.y, c.z + z - bp.center.z); }
            PointLight("RoomLight", shell, c + new Vector3(0, 140f, 0), 650f, 4f, new Color(1f, 0.93f, 0.82f), true);
            PointLight("FillNW", shell, c + new Vector3(-150f, 120f, 150f), 380f, 1.5f, new Color(1f, 0.9f, 0.75f));
            PointLight("FillSE", shell, c + new Vector3(150f, 120f, -150f), 380f, 1.5f, new Color(1f, 0.9f, 0.75f));
        }

        [MenuItem("Shift Jam/Workshop/Build Workshop Room")]
        public static void BuildWorkshopRoom()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Workshop") { Debug.LogError("Open Workshop.unity first (this builds into the active scene)."); return; }
            Log.Clear();
            var oldRoot = GameObject.Find("Workshop"); if (oldRoot != null) Object.DestroyImmediate(oldRoot);
            var root = new GameObject("Workshop").transform;
            var shell = Child(root, "Shell"); var layout = Child(root, "Layout");
            Vector3 c = Vector3.zero; float h = WorkshopHalf;
            SandboxShell(shell, c, h);

            // starter furniture along the walls, the middle left open for building
            GameObject Against(string prefab, string name, Vector3 at, float yaw)
            {
                var go = Rest(Spawn(prefab, name, layout, at, new Vector3(0, yaw, 0), 1f), 0f, null); var b = B(go);
                Vector3 push = Vector3.zero;   // slide flush to the nearest wall
                if (at.z > h - 80f) push.z = (h - 1f) - b.max.z; else if (at.z < -h + 80f) push.z = (-h + 1f) - b.min.z;
                if (at.x > h - 80f) push.x = (h - 1f) - b.max.x; else if (at.x < -h + 80f) push.x = (-h + 1f) - b.min.x;
                go.transform.position += push; return go;
            }
            Against("Bed_01", "Bed", new Vector3(-190f, 0, 200f), 0f);
            Against("Bedside_Table_01", "BedsideTable", new Vector3(-120f, 0, 240f), 0f);
            Against("Table_01", "Desk", new Vector3(40f, 0, 240f), 0f);
            Rest(Spawn("Chair_01", "Chair", layout, new Vector3(40f, 0, 205f), new Vector3(0, 180, 0), 1f), 0f, null);
            Against("Shelf_01", "Shelf", new Vector3(-250f, 0, 20f), 90f);
            Against("Bookshelf_01", "Bookshelf", new Vector3(-250f, 0, -90f), 90f);
            Against("Sofa_01", "Sofa", new Vector3(250f, 0, 40f), -90f);
            Against("Wardrobe_01", "Wardrobe", new Vector3(230f, 0, -230f), -90f);
            foreach (var (n, x, z, yaw) in new[] { ("Cardboard_Box_01", -200f, -220f, 15f), ("Cardboard_Box_05", -170f, -230f, -10f), ("Cardboard_Box_06", -140f, -235f, 5f) })
                Rest(Spawn(n, n, layout, new Vector3(x, 0, z), new Vector3(0, yaw, 0), 1f), 0f, null);
            var pillow = Rest(Spawn("Pillow_01", "Pillow", layout, new Vector3(-60f, 0, -60f), new Vector3(0, 20, 0), 1f), 0f, null);
            Carry(pillow); PillowPad(pillow, 30f);
            for (int i = 0; i < 4; i++) Rest(Spawn("Mat_0" + (1 + i), "Mat", layout, new Vector3(60f + (i % 2) * 25.5f, 0, -40f + (i / 2) * 25.5f), Vector3.zero, 1f), 0.05f, null);
            MatBeds(layout);   // D-0049
            // the toy car, parked facing north by the south wall
            var car = Rest(Spawn("Wooden_Car_01", "ToyCar", layout, new Vector3(0f, 0, -200f), Vector3.zero, 1f), 0f, null);
            var tc = car.AddComponent<ToyCar>(); Set(tc, "rollLoop", Clip("Toy_Car_Roll_v1")); Set(tc, "crash", Clip("Toy_Car_Crash_v1")); WorkshopPieces.WireBoostParts(tc);
            var tbc = car.AddComponent<BoxCollider>(); tbc.isTrigger = true; var cbb = B(car); tbc.center = car.transform.InverseTransformPoint(cbb.center); tbc.size = cbb.size + Vector3.one * 2f;
            Wedge("Kicker", layout, new Vector3(0f, 0f, -120f), Vector3.forward, 4f, 12f, 24f);

            // the player starts in the middle as the toy, facing north
            var player = GameObject.Find("Player");
            if (player != null) { player.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, -150f), Quaternion.identity); }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("WORKSHOP ROOM BUILT\n" + Log);
        }

        /// <summary>Traversal pieces for the Workshop catalogue (Prefabs/Pieces/Traversal): the course's own parts as prefabs,
        /// pivot at the bottom centre: a pillow pad (kid carries, toy bounces), a small kicker, a climb ramp up to box height,
        /// an upright updraft fan and a book staircase. Re-running overwrites them (same GUIDs).</summary>
        [MenuItem("Shift Jam/Workshop/Build Traversal Pieces")]
        public static void BuildTraversalPieces()
        {
            const string dir = "Assets/_Game/Prefabs/Pieces/Traversal";
            System.IO.Directory.CreateDirectory(dir);
            void Save(Transform root) { PrefabUtility.SaveAsPrefabAsset(root.gameObject, $"{dir}/{root.name}.prefab"); Object.DestroyImmediate(root.gameObject); }
            Transform Root(string n) => new GameObject(n).transform;

            var pad = Root("PillowPad"); var pillow = Rest(Spawn("Pillow_01", "Pillow", pad, Vector3.zero, Vector3.zero, 1f), 0f, null);
            Carry(pillow); PillowPad(pillow, 30f); Save(pad);

            var kick = Root("KickerSmall"); Wedge("Wedge", kick, new Vector3(0f, 0f, 6f), Vector3.forward, 4f, 12f, 16f); Save(kick);
            var climb = Root("ClimbRamp"); Wedge("Wedge", climb, new Vector3(0f, 0f, 17f), Vector3.forward, 12.2f, 34f, 30f); Save(climb);
            var land = Root("LandingRamp"); Wedge("Wedge", land, new Vector3(0f, 0f, -17f), Vector3.back, 12.2f, 34f, 30f); Save(land);

            var fan = Root("UpdraftFan");
            var fanProp = Spawn("Fan_Base_01", "Fan", fan, Vector3.zero, new Vector3(-90f, 0f, 0f), 1f); Rest(fanProp, 0f, null); var fb = B(fanProp);
            fanProp.transform.position += new Vector3(-fb.center.x, 0f, -fb.center.z);
            Transform blades = null; foreach (Transform ch in fanProp.GetComponentsInChildren<Transform>()) if (ch != fanProp.transform && ch.name.Contains("Propeller")) { blades = ch; break; }
            var column = new GameObject("Column"); column.transform.SetParent(fan, false); column.transform.localPosition = new Vector3(0f, B(fanProp).max.y + 35f, 0f);
            var colBox = column.AddComponent<BoxCollider>(); colBox.isTrigger = true; colBox.size = new Vector3(14f, 70f, 14f);
            var up = column.AddComponent<Updraft>(); if (blades != null) Set(up, "blades", blades);
            Save(fan);

            var stairs = Root("BookStairs"); BookStairs(stairs, new Vector3(0f, 0f, -12f), Vector3.forward, 0f, 12f, "Books"); Save(stairs);
            AssetDatabase.SaveAssets();
            Debug.Log("TRAVERSAL PIECES: PillowPad, KickerSmall, ClimbRamp, LandingRamp, UpdraftFan, BookStairs -> " + dir);
        }

        // ------------------------------------------------------------------ the car course (D-0026)
        // A stunt run for the toy car in the playroom's clear north-south corridor (x 505..535, between the race track and
        // the broken fan; the maze/camp starts at x 540 north of z -76, the castle at z -229): north-bound from just above
        // the castle: a short run-up, a climb onto a deck of four cardboard boxes (12.2 high), a kicker off the deck's end
        // over a 20-unit gap, a landing ramp back to the floor at z ~ -82. Numbers from the car (top speed 26, gravity 25):
        // ~0.8 s of air, peak ~15 up; the landing ramp's top sits where the arc comes down. Removes NOTHING from the room:
        // small loose props under a piece are moved to the lane's side (listed in the log).
        const float CarLaneX = 520f;

        [MenuItem("Shift Jam/Build Car Course")]
        public static void BuildCarCourse()
        {
            var r4 = GameObject.Find("Room/Course/R4_Playroom"); if (r4 == null) { Debug.LogError("no Room/Course/R4_Playroom"); return; }
            var old = r4.transform.Find("CarCourse"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var course = Child(r4.transform, "CarCourse");
            float x = CarLaneX, w = 30f;
            Vector3 north = Vector3.forward;
            Wedge("Climb", course, new Vector3(x, 0f, -166f), north, 12.2f, 34f, w);
            float deckTop = 0f;
            for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++)
            {
                var box = Rest(Spawn("Cardboard_Box_05", $"Deck{i}{j}", course, new Vector3(x - 7.55f + j * 15.1f, 0f, -158.6f + i * 14.8f), Vector3.zero, 1f), 0f, null);
                deckTop = Mathf.Max(deckTop, B(box).max.y);
            }
            Wedge("Kicker", course, new Vector3(x, deckTop - 0.25f, -136.4f), north, 3f, 10f, w);
            Wedge("Landing", course, new Vector3(x, 0f, -116.4f), Vector3.back, deckTop, 34f, w);   // high end faces the gap
            Rest(Spawn("Toy_Sign_01", "StartSign", course, new Vector3(x - w * 0.5f - 4f, 0f, -214f), new Vector3(0, 90, 0), 1f), 0f, null);
            // move small loose props out from under the pieces (never deletes)
            var lane = new Bounds(new Vector3(x, 10f, -150f), new Vector3(w + 4f, 20f, 140f));
            int moved = 0;
            foreach (Transform t in r4.transform.Cast<Transform>().ToArray())
            {
                if (t == course || t.name == "ToyCar" || t.name == "Mat" || t.name.StartsWith("Floor") || t.name.StartsWith("Wall")) continue;
                if (t.GetComponentsInChildren<Renderer>().Length == 0) continue;
                var b = B(t.gameObject); if (b.size.y > 10f || b.size.x > 25f || b.size.z > 25f) continue;
                if (!lane.Contains(new Vector3(b.center.x, 10f, b.center.z))) continue;
                float side = b.center.x < x ? x - w * 0.5f - 6f - b.extents.x : x + w * 0.5f + 6f + b.extents.x;
                t.position += Vector3.right * (side - b.center.x); moved++;
                Log.AppendLine("moved aside " + t.name);
            }
            // the car waits at the start, nose north (the nose is where its front wheels are)
            var car = r4.transform.Find("ToyCar");
            if (car != null)
            {
                car.rotation = Quaternion.identity;
                Transform front = null, rear = null;
                foreach (var t in car.GetComponentsInChildren<Transform>()) { if (t.name.Contains("Front") || t.name.EndsWith("_FL") || t.name.EndsWith("_FR")) front = t; if (t.name.Contains("Rear") || t.name.EndsWith("_RL") || t.name.EndsWith("_RR")) rear = t; }
                if (front != null && rear != null && Vector3.Dot(front.position - rear.position, Vector3.forward) < 0f) car.rotation = Quaternion.Euler(0f, 180f, 0f);
                var cb = B(car.gameObject); car.position += new Vector3(x - cb.center.x, -cb.min.y, -216f - cb.center.z);
            }
            EditorSceneManager.MarkSceneDirty(r4.scene);
            Debug.Log($"CAR COURSE BUILT: deck top {deckTop:F1}, moved aside {moved}\n" + Log);
        }

        /// <summary>A wedge ramp from the pack's Ramp_02, stretched to <paramref name="width"/> x <paramref name="rise"/> x
        /// <paramref name="length"/>, climbing along <paramref name="climb"/> with its high edge at <paramref name="high"/>
        /// (x/z), resting its base on high.y. The wedge's own slope axis and high end are read from the mesh, so the
        /// prefab's orientation does not matter.</summary>
        static GameObject Wedge(string name, Transform parent, Vector3 high, Vector3 climb, float rise, float length, float width, bool leadIn = true)
        {
            var go = Spawn("Ramp_02", name, parent, Vector3.zero, Vector3.zero, 1f);
            var mf = go.GetComponentInChildren<MeshFilter>(); var mb = mf.sharedMesh.bounds;
            WedgeShape(mf.sharedMesh, out Vector3 upLocal, out bool alongZ, out float lip);
            Vector3 size = mb.size;
            float sy = rise / (size.y - lip);
            go.transform.localScale = alongZ ? new Vector3(width / size.x, sy, length / size.z) : new Vector3(length / size.x, sy, width / size.z);
            Vector3 flat = new Vector3(climb.x, 0f, climb.z).normalized, cross = new Vector3(-flat.z, 0f, flat.x);
            go.transform.rotation = Quaternion.Euler(0f, Quaternion.FromToRotation(upLocal, flat).eulerAngles.y, 0f);
            var b = B(go);
            Vector3 highEdge = b.center + flat * (Mathf.Abs(flat.x) * b.extents.x + Mathf.Abs(flat.z) * b.extents.z);
            go.transform.position += flat * Vector3.Dot(high - highEdge, flat) + cross * Vector3.Dot(high - b.center, cross) + Vector3.up * (high.y - b.min.y - lip * sy);
            go.AddComponent<RestSink>().Set(lip);   // so Build Mode keeps the toe sunk when the ramp is placed or moved
            Log.AppendLine($"{name}: rise {rise:F1} over {length:F0} = {Mathf.Atan2(rise, length) * Mathf.Rad2Deg:F0} deg, lip {lip * sy:F2} sunk");
            if (leadIn) LeadIn(name, parent, high, climb, rise, length, width);
            return go;
        }

        /// <summary>The pack wedge's shape from its vertices: the local way it climbs and the raised lip at its low edge (mesh
        /// units). Scaled up, that lip is a wall taller than the player's step, so Wedge sizes the slope (not the whole mesh)
        /// to the rise and sinks the lip under whatever it sits on.</summary>
        static void WedgeShape(Mesh mesh, out Vector3 upLocal, out bool alongZ, out float lip)
        {
            var mb = mesh.bounds;
            Vector3[] v = mesh.vertices; float xPos = 0, xNeg = 0, zPos = 0, zNeg = 0; int nxp = 0, nxn = 0, nzp = 0, nzn = 0;
            foreach (var p in v)
            {
                if (p.x > mb.center.x + mb.extents.x * 0.6f) { xPos += p.y; nxp++; } else if (p.x < mb.center.x - mb.extents.x * 0.6f) { xNeg += p.y; nxn++; }
                if (p.z > mb.center.z + mb.extents.z * 0.6f) { zPos += p.y; nzp++; } else if (p.z < mb.center.z - mb.extents.z * 0.6f) { zNeg += p.y; nzn++; }
            }
            float dx = (nxp > 0 ? xPos / nxp : 0) - (nxn > 0 ? xNeg / nxn : 0), dz = (nzp > 0 ? zPos / nzp : 0) - (nzn > 0 ? zNeg / nzn : 0);
            alongZ = Mathf.Abs(dz) >= Mathf.Abs(dx);
            upLocal = alongZ ? (dz > 0 ? Vector3.forward : Vector3.back) : (dx > 0 ? Vector3.right : Vector3.left);   // the local way it climbs
            Vector3 size = mb.size;
            float lowEnd = alongZ ? (upLocal.z > 0 ? mb.min.z : mb.max.z) : (upLocal.x > 0 ? mb.min.x : mb.max.x);
            float band = (alongZ ? size.z : size.x) * 0.08f;
            lip = 0f;
            foreach (var p in v) if (Mathf.Abs((alongZ ? p.z : p.x) - lowEnd) <= band) lip = Mathf.Max(lip, p.y - mb.min.y);
            lip = Mathf.Min(lip, size.y * 0.25f);
        }

        /// <summary>
        /// A gentler start for a wedge ramp (D-0038): a thin wedge at half the slope runs in from the floor and meets the slope,
        /// so a car takes the climb in two half-bends instead of one sharp one and its springs no longer bottom out at the foot
        /// (the same at a landing ramp's run-out). It reaches out a fifth of the ramp's length (at most 8) past the toe; its
        /// high end hides inside the ramp.
        /// </summary>
        static void LeadIn(string name, Transform parent, Vector3 high, Vector3 climb, float rise, float length, float width)
        {
            if (rise < 0.5f || length < 2f) return;
            float tanT = rise / length, tanH = Mathf.Tan(Mathf.Atan(tanT) * 0.5f);
            float before = Mathf.Min(length * 0.2f, 8f);
            float onto = before * tanH / Mathf.Max(1e-4f, tanT - tanH);   // how far up the slope the two surfaces meet
            float h = onto * tanT;
            Vector3 flat = new Vector3(climb.x, 0f, climb.z).normalized;
            Vector3 toe = high - flat * length;                             // the slope's foot, at the ramp's base height
            Wedge(name + "Toe", parent, toe + flat * onto, climb, h, before + onto, width, false);
        }

        /// <summary>Adds the gentler start (D-0038) to the wedge ramps already standing in the open scenes without one: the ones
        /// built straight into a scene (the Workshop's kicker, Greybox's car course). Piece copies (ClimbRamp, LandingRamp,
        /// KickerSmall) take theirs from Build Traversal Pieces. Safe to run again.</summary>
        [MenuItem("Shift Jam/Workshop/Add Ramp Lead-ins (open scenes)")]
        public static void AddRampLeadIns()
        {
            Log.Clear();
            int added = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i); if (!s.isLoaded) continue;
                var ramps = new List<GameObject>();
                foreach (var root in s.GetRootGameObjects())
                    foreach (var sink in root.GetComponentsInChildren<RestSink>(true))
                    {
                        var go = sink.gameObject;
                        if (go.name.EndsWith("Toe") || go.transform.parent == null) continue;
                        if (PrefabUtility.GetOutermostPrefabInstanceRoot(go) != go) continue;   // inside a piece copy: its prefab carries it
                        var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                        if (source == null || source.name != "Ramp_02") continue;
                        if (go.transform.parent.Find(go.name + "Toe") != null) continue;
                        ramps.Add(go);
                    }
                foreach (var go in ramps) { LeadInFor(go); added++; }
                if (ramps.Count > 0) EditorSceneManager.MarkSceneDirty(s);
            }
            Debug.Log($"RAMP LEAD-INS: {added} added. Save the scene(s).\n{Log}");
        }

        /// <summary>A lead-in for a wedge already standing in a scene: its rise, length, width, base and high edge read back
        /// from its mesh, scale and place (Wedge in reverse).</summary>
        static void LeadInFor(GameObject go)
        {
            var mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh; var mb = mesh.bounds;
            WedgeShape(mesh, out Vector3 upLocal, out bool alongZ, out float lip);
            Vector3 scale = go.transform.lossyScale;
            float length = Mathf.Abs(alongZ ? mb.size.z * scale.z : mb.size.x * scale.x);
            float width = Mathf.Abs(alongZ ? mb.size.x * scale.x : mb.size.z * scale.z);
            float rise = (mb.size.y - lip) * Mathf.Abs(scale.y);
            Vector3 flat = Vector3.ProjectOnPlane(go.transform.rotation * upLocal, Vector3.up).normalized;
            var b = B(go);
            Vector3 highEdge = b.center + flat * (Mathf.Abs(flat.x) * b.extents.x + Mathf.Abs(flat.z) * b.extents.z);
            highEdge.y = b.min.y + lip * Mathf.Abs(scale.y);   // the slope's base: the top of the sunk lip
            LeadIn(go.name, go.transform.parent, highEdge, flat, rise, length, width);
        }

        /// <summary>A kid-carryable prop with the pick-up prompt.</summary>
        static GameObject Carry(GameObject go)
        {
            if (go == null) return null;
            if (!go.TryGetComponent(out Carryable _)) go.AddComponent<Carryable>();
            if (!go.TryGetComponent(out Carried _)) go.AddComponent<Carried>();
            return go;
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
