#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The playroom's expressway (D-0048): a raised loop of the pack's orange toy track that a toy car laps above the mats
    /// and the floor clutter, with stops where the toy gets on and off. Straights are the pack's own piece
    /// (RoadLoop_02); corners and the open-sided pieces are made from it by <see cref="TrackBender"/> and saved under
    /// <see cref="XwMeshDir"/> (pack-derived, so out of git like the packs). Everything is laid from the three tables
    /// below; it is rebuilt whole, under R4_Playroom/Expressway, on every run.
    /// </summary>
    public static partial class CourseBuilder
    {
        const string XwSource = Pack + "/Prefabs/Toys/RoadLoop_02.prefab";   // the flat straight; RoadLoop_03 looks the same from above but tapers 0.5 along its length
        const string XwMeshDir = "Assets/_Imports/Generated/Expressway";
        const string XwPillar = "Wooden_Cylinder_01";

        const float XwScale = 2.5f;        // the pack's track is 4.4 wide; at this scale the lane is about four cars wide
        const float XwHeight = 8f;         // the road's height: over the mats (0.73), books and blocks; under the kid's step (10.5)
        const float XwRadius = 35f;        // a corner's centre line
        const float XwGuard = 2f;          // collision-only guard above the road along every kept rail (road + guard stays under the kid's step)
        const int XwRampPieces = 2;        // pieces in a ramp down to the floor
        const float XwPillarWidth = 4.5f;
        const float XwStepRise = 1.2f, XwStepRun = 2.4f;   // the book steps: one book, one hop (D-0008)

        /// <summary>The loop's corners in order, playroom-local (x east, z north, from the room's middle). Each corner turns
        /// 45 or 90 degrees and is rounded at <see cref="XwRadius"/>.</summary>
        static readonly Vector2[] XwLoop =
        {
            new Vector2(-135f, -145f),    // south-west
            new Vector2(75f, -145f),      // south-east
            new Vector2(75f, 72f),        // north-east
            new Vector2(-20f, 72f),       // north, then the cut across in front of the desk
            new Vector2(-40.5f, 51.5f),
            new Vector2(-135f, 51.5f),    // north-west
        };

        enum XwStopKind
        {
            /// <summary>A second lane beside the loop with a ramp to the floor at each end.</summary>
            Slip,
            /// <summary>A second lane beside the loop to park in.</summary>
            LayBy,
            /// <summary>Only the loop's own rail is opened, to step off.</summary>
            Drop,
        }

        /// <summary>The stops, on the loop's outer side. <c>run</c> is the straight after that corner of
        /// <see cref="XwLoop"/>, <c>piece</c> the first of <c>count</c> pieces along it. <c>stairs</c> puts book steps from
        /// the floor to the first of them; <c>car</c> parks that car piece on the last.</summary>
        static readonly (string name, int run, int piece, int count, XwStopKind kind, bool stairs, string car)[] XwStops =
        {
            ("Entry", 5, 1, 2, XwStopKind.Slip, true, "CyberCar14"),
            ("Desk", 4, 0, 1, XwStopKind.LayBy, true, "CyberCar05"),
            ("Maze", 1, 5, 1, XwStopKind.Drop, false, null),
            ("Wardrobe", 1, 0, 1, XwStopKind.LayBy, true, "CyberCar09"),
        };

        sealed class XwKit
        {
            public GameObject Straight;
            public Material Material;
            public TrackBender.Shape Shape;
            public Mesh OpenLeft, OpenRight, Bare, CurveLeft, CurveRight;
            public float Lane => 2f * Shape.OuterHalf * XwScale;            // centre to centre of two lanes side by side
            public float Half => Shape.OuterHalf * XwScale;
            public float Under => XwHeight - Shape.Depth * XwScale;         // the deck's underside
            public float PieceLength => Shape.Length * XwScale;
        }

        /// <summary>One laid piece, as the checks need it: its box in the world (a corner is three boxes along its chord).</summary>
        sealed class XwPiece
        {
            public GameObject Go;
            public bool Ramp;
            public readonly List<(Vector3 centre, Quaternion turn, Vector3 half)> Boxes = new List<(Vector3, Quaternion, Vector3)>();
        }

        [MenuItem("Shift Jam/Build Expressway (playroom)")]
        public static void BuildExpresswayMenu()
        {
            var r4 = GameObject.Find("Room/Course/R4_Playroom");
            if (r4 == null) { Debug.LogError("no Room/Course/R4_Playroom: open Greybox and run Shift Jam > Build Course first"); return; }
            Log.Clear();
            BuildExpressway(r4.transform);
            Debug.Log("EXPRESSWAY BUILT\n" + Log);
        }

        /// <summary>Batch: -executeMethod ShiftJam.CourseBuilder.LandExpressway (no -nographics: it renders stills). D-0048
        /// without a full course rebuild: the expressway and the rides in Greybox, saved, stills into
        /// %TEMP%/sj_d0048_stills.</summary>
        public static void LandExpressway()
        {
            Log.Clear();
            EditorSceneManager.OpenScene(ScenePath);
            var course = GameObject.Find("Room/Course"); var r4 = GameObject.Find("Room/Course/R4_Playroom");
            if (course == null || r4 == null) { Debug.LogError("D-0048: no Room/Course/R4_Playroom in " + ScenePath); EditorApplication.Exit(1); return; }
            BuildExpressway(r4.transform);
            PlaceRides(course.transform);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            ExpresswayStills(r4.transform);
            Debug.Log("D-0048 LANDED\n" + Log);
        }

        static void BuildExpressway(Transform r4)
        {
            var old = r4.Find("Expressway"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var kit = XwMakeKit(); if (kit == null) return;
            var root = Child(r4, "Expressway");
            Transform loop = Child(root, "Loop"), stops = Child(root, "Stops"), pillars = Child(root, "Pillars");
            var pieces = new List<XwPiece>();
            var keepOut = new List<Bounds>();           // where dressing may not stand
            var pillarSpots = new List<Vector3>();

            // ---- the loop's geometry: every corner rounded, the straights between cut into whole pieces ----
            int n = XwLoop.Length;
            Vector3 Corner(int i) { var p = XwLoop[(i % n + n) % n]; return C4 + new Vector3(p.x, XwHeight, p.y); }
            var dirOut = new Vector3[n]; var turn = new float[n]; var tangent = new float[n];
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 din = (Corner(i) - Corner(i - 1)).normalized; dirOut[i] = (Corner(i + 1) - Corner(i)).normalized;
                turn[i] = Vector3.SignedAngle(din, dirOut[i], Vector3.up);   // positive = to the right
                float steps = Mathf.Abs(turn[i]) / 45f;
                if (Mathf.Abs(steps - Mathf.Round(steps)) > 0.02f || Mathf.Round(steps) < 1f) { Debug.LogError($"Expressway: corner {i} turns {turn[i]:F1} degrees; corners must turn 45 or 90."); return; }
                tangent[i] = XwRadius * Mathf.Tan(Mathf.Abs(turn[i]) * 0.5f * Mathf.Deg2Rad);
                total += turn[i];
            }
            float outer = total < 0f ? 1f : -1f;   // a loop that turns left has its outside on the right
            var runCount = new int[n]; var runLength = new float[n];
            for (int i = 0; i < n; i++)
            {
                float len = (Corner(i + 1) - Corner(i)).magnitude - tangent[i] - tangent[(i + 1) % n];
                if (len < -0.5f) { Debug.LogError($"Expressway: no room between corners {i} and {(i + 1) % n} for both bends ({len:F1})."); return; }
                runCount[i] = len < 1f ? 0 : Mathf.Max(1, Mathf.RoundToInt(len / kit.PieceLength));
                runLength[i] = runCount[i] > 0 ? len / runCount[i] : 0f;
            }
            foreach (var s in XwStops)
                if (s.run < 0 || s.run >= n || s.piece < 0 || s.piece + s.count > runCount[s.run]) { Debug.LogError($"Expressway: stop {s.name} asks for pieces {s.piece}..{s.piece + s.count - 1} of run {s.run}, which has {(s.run >= 0 && s.run < n ? runCount[s.run] : 0)}."); return; }
            bool Opened(int run, int piece) { foreach (var s in XwStops) if (s.run == run && piece >= s.piece && piece < s.piece + s.count) return true; return false; }

            // ---- lay the loop ----
            float gap = 0f, lap = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 at = Corner(i) + dirOut[i] * tangent[i];
                for (int p = 0; p < runCount[i]; p++)
                {
                    bool open = Opened(i, p);
                    bool keepRight = !(open && outer > 0f), keepLeft = !(open && outer < 0f);
                    pieces.Add(XwStraight(kit, loop, $"Run{i}_{p}", at, dirOut[i], 0f, runLength[i], keepLeft, keepRight, keepLeft, keepRight, false));
                    pillarSpots.Add(at + dirOut[i] * (runLength[i] * 0.5f));
                    at += dirOut[i] * runLength[i]; lap += runLength[i];
                }
                int next = (i + 1) % n; Vector3 heading = dirOut[i];
                gap = Mathf.Max(gap, (at - (Corner(i + 1) - dirOut[i] * tangent[next])).magnitude);
                int bends = Mathf.RoundToInt(Mathf.Abs(turn[next]) / 45f);
                for (int b = 0; b < bends; b++)
                {
                    pillarSpots.Add(at);
                    pieces.Add(XwCurve(kit, loop, $"Corner{next}_{b}", ref at, ref heading, turn[next] > 0f));
                    lap += XwRadius * 45f * Mathf.Deg2Rad;
                }
                gap = Mathf.Max(gap, (at - (Corner(i + 1) + dirOut[next] * tangent[next])).magnitude);
            }
            Log.AppendLine($"expressway loop: {pieces.Count} pieces, {lap:F0} units round, road at {XwHeight}, underside {kit.Under:F2}, lanes {kit.Lane:F2} apart; worst joint gap {gap:F3}");

            // ---- the stops ----
            var cars = new List<(string stop, string piece, Vector3 at, Vector3 facing)>();
            foreach (var s in XwStops)
            {
                Vector3 dir = dirOut[s.run], side = Vector3.Cross(Vector3.up, dir) * outer;   // toward the loop's outside
                float len = runLength[s.run];
                Vector3 first = Corner(s.run) + dir * (tangent[s.run] + s.piece * len);    // where the stop's first loop piece starts
                var holder = Child(stops, s.name);
                if (s.kind != XwStopKind.Drop)
                {
                    Vector3 lane = first + side * kit.Lane;
                    for (int p = 0; p < s.count; p++)
                    {
                        // the loop is on this lane's inner side: that rail is pressed flat; the outer one too where the steps arrive
                        bool stepsHere = s.stairs && p == 0;
                        bool keepOuter = !stepsHere;
                        bool keepRight = outer > 0f ? keepOuter : false, keepLeft = outer > 0f ? false : keepOuter;
                        bool endGuards = s.kind == XwStopKind.LayBy;
                        pieces.Add(XwStraight(kit, holder, $"Lane{p}", lane + dir * (p * len), dir, 0f, len, keepLeft, keepRight, keepLeft, keepRight, endGuards && (p == 0 || p == s.count - 1), endGuards && p == 0, endGuards && p == s.count - 1));
                        pillarSpots.Add(lane + dir * ((p + 0.5f) * len));
                    }
                    if (s.kind == XwStopKind.Slip)
                    {
                        float pitch = Mathf.Asin(Mathf.Clamp01(XwHeight / (XwRampPieces * len))) * Mathf.Rad2Deg;
                        for (int end = 0; end < 2; end++)
                        {
                            // end 0 runs back from the lane's start, end 1 on from its end; either way the loop is on the ramp's inner side
                            Vector3 heading = end == 0 ? -dir : dir, at = end == 0 ? lane : lane + dir * (s.count * len);
                            bool loopOnRight = Vector3.Dot(Vector3.Cross(Vector3.up, heading), side) < 0f;
                            Vector3 fall = Quaternion.AngleAxis(pitch, Vector3.Cross(Vector3.up, heading)) * heading;
                            for (int p = 0; p < XwRampPieces; p++)
                            {
                                var ramp = XwStraight(kit, holder, $"Ramp{end}_{p}", at, heading, pitch, len, loopOnRight, !loopOnRight, loopOnRight, !loopOnRight, false);
                                ramp.Ramp = true; pieces.Add(ramp);
                                at += fall * len;
                            }
                            Log.AppendLine($"  {s.name}: ramp {end} lands at {at - C4} (playroom-local), slope {pitch:F1} deg");
                        }
                    }
                }
                Vector3 edge = first + dir * (len * 0.5f) + side * ((s.kind == XwStopKind.Drop ? 0f : kit.Lane) + kit.Half);
                if (s.stairs)
                {
                    var steps = XwBookSteps(holder, "Steps", edge, -side, XwHeight);
                    var sb = B(steps); sb.Expand(new Vector3(2f, 0f, 2f)); keepOut.Add(XwColumn(sb, -5f));
                }
                if (s.car != null && s.kind != XwStopKind.Drop)
                    cars.Add((s.name, s.car, first + side * kit.Lane + dir * ((s.count - 0.5f) * len), -dir));
                Log.AppendLine($"  stop {s.name} ({s.kind}): run {s.run}, pieces {s.piece}..{s.piece + s.count - 1}, outer edge at {edge - C4} (playroom-local){(s.stairs ? ", book steps" : "")}{(s.car != null ? ", " + s.car : "")}");
            }

            // ---- make room: dressing that stands in the way is moved aside, never deleted ----
            foreach (var piece in pieces)
            {
                var pb = B(piece.Go); pb.Expand(new Vector3(1f, 0f, 1f));
                keepOut.Add(XwColumn(pb, piece.Ramp ? -5f : kit.Under - 0.3f));
            }
            foreach (var spot in pillarSpots) keepOut.Add(XwColumn(new Bounds(new Vector3(spot.x, 0f, spot.z), new Vector3(XwPillarWidth + 1.5f, 1f, XwPillarWidth + 1.5f)), -5f));
            int moved = XwMakeRoom(r4, root, keepOut);

            // ---- pillars, where nothing fixed stands ----
            Physics.SyncTransforms();
            int stood = 0;
            foreach (var spot in pillarSpots)
            {
                Vector3 foot = new Vector3(spot.x, 0f, spot.z); float half = XwPillarWidth * 0.5f, top = kit.Under - 0.25f;
                bool blocked = false;
                foreach (var c in Physics.OverlapBox(foot + Vector3.up * ((top + 0.9f) * 0.5f), new Vector3(half, (top - 0.9f) * 0.5f, half), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    if (!c.transform.IsChildOf(pillars)) { blocked = true; break; }
                if (blocked) continue;
                var pillar = Spawn(XwPillar, "Pillar", pillars, foot, Vector3.zero, 1f);
                foreach (var body in pillar.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(body);
                var size = B(pillar).size;
                pillar.transform.localScale = new Vector3(XwPillarWidth / Mathf.Max(0.01f, size.x), kit.Under / Mathf.Max(0.01f, size.y), XwPillarWidth / Mathf.Max(0.01f, size.z));
                var pb = B(pillar); pillar.transform.position += new Vector3(foot.x - pb.center.x, -pb.min.y, foot.z - pb.center.z);
                stood++;
            }

            // ---- the cars, parked on their lanes, facing the way the stops are listed ----
            var carHolder = Child(root, "Cars");
            foreach (var c in cars)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CarPieces + c.piece + ".prefab");
                if (asset == null) { Log.AppendLine($"  NO CAR at {c.stop}: no piece {c.piece} (run Shift Jam > Workshop > Build Combat & Vehicle Pieces)"); continue; }
                var car = (GameObject)PrefabUtility.InstantiatePrefab(asset, carHolder); car.name = "Ride_" + c.stop;
                car.transform.SetPositionAndRotation(new Vector3(c.at.x, XwHeight + 0.02f, c.at.z), Quaternion.LookRotation(c.facing));
            }

            // ---- what still stands in the road ----
            Physics.SyncTransforms();
            var clashes = new List<string>();
            foreach (var piece in pieces)
                foreach (var box in piece.Boxes)
                    foreach (var c in Physics.OverlapBox(box.centre, box.half, box.turn, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (c.transform.IsChildOf(root)) continue;
                        var top = c.transform; while (top.parent != null && top.parent != r4) top = top.parent;
                        if (piece.Ramp && (top.name.StartsWith("Floor") || top.name == "Mat")) continue;   // a ramp ends in the floor
                        string line = $"{piece.Go.name} meets {top.name} (its top at {c.bounds.max.y:F1})";
                        if (!clashes.Contains(line)) clashes.Add(line);
                    }
            // ---- is the road really there: rays onto it down every piece, and a car's worth of clear air above it ----
            int samples = 0, holes = 0, blockedLanes = 0; float worst = 0f;
            foreach (var piece in pieces)
                foreach (var box in piece.Boxes)
                {
                    float roadHalf = kit.Shape.RoadHalf * XwScale;
                    foreach (float along in new[] { -0.8f, -0.4f, 0f, 0.4f, 0.8f })
                        foreach (float across in new[] { -0.5f, 0f, 0.5f })
                        {
                            // a box reaches from its middle up to the guard's top: one unit above that, the road is XwGuard + 1 below
                            Vector3 from = box.centre + box.turn * new Vector3(across * roadHalf, box.half.y + 1f, along * box.half.z);
                            samples++;
                            bool found = false;
                            foreach (var h in Physics.RaycastAll(from, box.turn * Vector3.down, XwGuard + 3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                            {
                                if (!h.collider.transform.IsChildOf(loop) && !h.collider.transform.IsChildOf(stops)) continue;
                                found = true; worst = Mathf.Max(worst, Mathf.Abs(h.distance - (XwGuard + 1f)));
                                if (Mathf.Abs(h.distance - (XwGuard + 1f)) > 0.12f) holes++;
                                break;
                            }
                            if (!found) holes++;
                        }
                    // a car's body (about 2.2 wide, from its clearance to its roof) down the middle of the lane
                    Vector3 lane = box.centre + box.turn * new Vector3(0f, -box.half.y + kit.Shape.Depth * XwScale - 0.15f + 1.3f, 0f);
                    foreach (var c in Physics.OverlapBox(lane, new Vector3(1.1f, 0.85f, box.half.z), box.turn, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (c.transform.IsChildOf(carHolder) || (c.transform.parent == piece.Go.transform && (c.name == "GuardStart" || c.name == "GuardEnd"))) continue;   // a parked car, or a lay-by's own end stop
                        blockedLanes++; Log.AppendLine($"  LANE BLOCKED on {piece.Go.name} by {c.name}"); break;
                    }
                }
            Log.AppendLine($"  road check: {samples} rays, {holes} missed or off the road's height (worst {worst:F3}), {blockedLanes} stretches with something in the lane");
            Log.AppendLine($"  moved aside {moved}, pillars {stood} of {pillarSpots.Count}, cars {cars.Count}");
            Log.AppendLine(clashes.Count == 0 ? "  road clear: nothing fixed stands in it" : "  IN THE ROAD (" + clashes.Count + "):\n    " + string.Join("\n    ", clashes));
            EditorSceneManager.MarkSceneDirty(r4.gameObject.scene);
        }

        // ---------- the kit ----------

        /// <summary>Loads the pack's straight and makes (or remakes in place, GUIDs kept) the five pieces the pack lacks.</summary>
        static XwKit XwMakeKit()
        {
            var straight = AssetDatabase.LoadAssetAtPath<GameObject>(XwSource);
            var filter = straight != null ? straight.GetComponentInChildren<MeshFilter>() : null;
            var renderer = straight != null ? straight.GetComponentInChildren<MeshRenderer>() : null;
            if (filter == null || filter.sharedMesh == null || renderer == null) { Debug.LogError("Expressway: no straight track piece at " + XwSource); return null; }
            Mesh source = filter.sharedMesh;
            var kit = new XwKit { Straight = straight, Material = renderer.sharedMaterial, Shape = TrackBender.Measure(source) };
            if (!AssetDatabase.IsValidFolder(XwMeshDir))
            {
                string parent = "Assets";
                foreach (var part in XwMeshDir.Substring("Assets/".Length).Split('/'))
                {
                    if (!AssetDatabase.IsValidFolder(parent + "/" + part)) AssetDatabase.CreateFolder(parent, part);
                    parent += "/" + part;
                }
            }
            float radius = XwRadius / XwScale;
            kit.OpenLeft = XwSave(TrackBender.Make(source, "Track_OpenLeft", false, true, 0f, 0f, 0), "Track_OpenLeft");
            kit.OpenRight = XwSave(TrackBender.Make(source, "Track_OpenRight", true, false, 0f, 0f, 0), "Track_OpenRight");
            kit.Bare = XwSave(TrackBender.Make(source, "Track_Bare", false, false, 0f, 0f, 0), "Track_Bare");
            kit.CurveLeft = XwSave(TrackBender.Make(source, "Track_Curve45_Left", true, true, -45f, radius, 12), "Track_Curve45_Left");
            kit.CurveRight = XwSave(TrackBender.Make(source, "Track_Curve45_Right", true, true, 45f, radius, 12), "Track_Curve45_Right");
            AssetDatabase.SaveAssets();
            Log.AppendLine($"expressway kit from {source.name}: road half {kit.Shape.RoadHalf:F2}, outer half {kit.Shape.OuterHalf:F2}, length {kit.Shape.Length:F2}, depth {kit.Shape.Depth:F2}; curve {kit.CurveLeft.vertexCount} verts");
            return kit;
        }

        static Mesh XwSave(Mesh made, string name)
        {
            string path = XwMeshDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(made, path); return made; }
            existing.Clear();
            EditorUtility.CopySerialized(made, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(made);
            return existing;
        }

        // ---------- pieces ----------

        /// <summary>A straight piece from <paramref name="start"/> along <paramref name="heading"/>, tipped nose-down by
        /// <paramref name="pitch"/> degrees, <paramref name="length"/> long. With both rails kept it is the pack's own
        /// prefab; otherwise one of the open-sided copies. A guard stands over each rail that asks for one.</summary>
        static XwPiece XwStraight(XwKit kit, Transform parent, string name, Vector3 start, Vector3 heading, float pitch, float length,
            bool keepLeft, bool keepRight, bool guardLeft, bool guardRight, bool endGuards, bool guardStart = false, bool guardEnd = false)
        {
            Quaternion turn = Quaternion.LookRotation(heading) * Quaternion.Euler(pitch, 0f, 0f);
            float stretch = length / kit.Shape.Length;
            GameObject go; float zStart;
            if (keepLeft && keepRight)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(kit.Straight, parent); go.name = name; zStart = kit.Shape.Start;
            }
            else
            {
                go = new GameObject(name); go.transform.SetParent(parent, false); zStart = 0f;
                Mesh mesh = keepLeft ? kit.OpenRight : keepRight ? kit.OpenLeft : kit.Bare;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = kit.Material;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            go.transform.localScale = new Vector3(XwScale, XwScale, stretch);
            go.transform.SetPositionAndRotation(start - turn * new Vector3(0f, 0f, zStart * stretch), turn);

            float railWidth = kit.Shape.OuterHalf - kit.Shape.RoadHalf, guard = XwGuard / XwScale, mid = zStart + kit.Shape.Length * 0.5f;
            void Guard(string label, Vector3 centre, Vector3 size)
            {
                var g = new GameObject(label); g.transform.SetParent(go.transform, false);
                var box = g.AddComponent<BoxCollider>(); box.center = centre; box.size = size;
            }
            if (guardLeft && keepLeft) Guard("GuardLeft", new Vector3(-(kit.Shape.RoadHalf + railWidth * 0.5f), guard * 0.5f, mid), new Vector3(railWidth, guard, kit.Shape.Length));
            if (guardRight && keepRight) Guard("GuardRight", new Vector3(kit.Shape.RoadHalf + railWidth * 0.5f, guard * 0.5f, mid), new Vector3(railWidth, guard, kit.Shape.Length));
            if (endGuards)
            {
                float thick = 0.4f / stretch;   // 0.4 in the world, whatever the piece's stretch
                if (guardStart) Guard("GuardStart", new Vector3(0f, guard * 0.5f, zStart + thick * 0.5f), new Vector3(kit.Shape.OuterHalf * 2f, guard, thick));
                if (guardEnd) Guard("GuardEnd", new Vector3(0f, guard * 0.5f, zStart + kit.Shape.Length - thick * 0.5f), new Vector3(kit.Shape.OuterHalf * 2f, guard, thick));
            }

            var piece = new XwPiece { Go = go };
            piece.Boxes.Add(XwBox(kit, start, turn, length));
            return piece;
        }

        /// <summary>A 45 degree corner piece from <paramref name="at"/> along <paramref name="heading"/>; both are moved on
        /// to where it ends. Guards follow both rails in three straight stretches.</summary>
        static XwPiece XwCurve(XwKit kit, Transform parent, string name, ref Vector3 at, ref Vector3 heading, bool right)
        {
            float degrees = right ? 45f : -45f, radius = XwRadius / XwScale;
            Quaternion turn = Quaternion.LookRotation(heading);
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            Mesh mesh = right ? kit.CurveRight : kit.CurveLeft;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = kit.Material;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.transform.localScale = Vector3.one * XwScale;
            go.transform.SetPositionAndRotation(at, turn);

            var piece = new XwPiece { Go = go };
            float railWidth = kit.Shape.OuterHalf - kit.Shape.RoadHalf, guard = XwGuard / XwScale;
            const int stretches = 3;
            for (int j = 0; j < stretches; j++)
            {
                float a0 = degrees * j / stretches, a1 = degrees * (j + 1) / stretches;
                foreach (float sideX in new[] { -(kit.Shape.RoadHalf + railWidth * 0.5f), kit.Shape.RoadHalf + railWidth * 0.5f })
                {
                    Vector3 p0 = TrackBender.Bend(new Vector3(sideX, guard * 0.5f, 0f), a0, radius), p1 = TrackBender.Bend(new Vector3(sideX, guard * 0.5f, 0f), a1, radius);
                    var g = new GameObject(sideX < 0f ? "GuardLeft" : "GuardRight"); g.transform.SetParent(go.transform, false);
                    g.transform.localPosition = (p0 + p1) * 0.5f; g.transform.localRotation = Quaternion.LookRotation(p1 - p0);
                    g.AddComponent<BoxCollider>().size = new Vector3(railWidth, guard, (p1 - p0).magnitude + 0.05f);
                }
                Vector3 c0 = at + turn * (TrackBender.Bend(Vector3.zero, a0, radius) * XwScale), c1 = at + turn * (TrackBender.Bend(Vector3.zero, a1, radius) * XwScale);
                piece.Boxes.Add(XwBox(kit, c0, Quaternion.LookRotation(c1 - c0), (c1 - c0).magnitude));
            }
            at += turn * (TrackBender.Bend(Vector3.zero, degrees, radius) * XwScale);
            heading = Quaternion.Euler(0f, degrees, 0f) * heading;
            return piece;
        }

        /// <summary>The space a stretch of road takes, for the checks: from just above its underside to the top of its guard.</summary>
        static (Vector3 centre, Quaternion turn, Vector3 half) XwBox(XwKit kit, Vector3 start, Quaternion turn, float length)
        {
            float depth = kit.Shape.Depth * XwScale - 0.15f;
            Vector3 centre = start + turn * new Vector3(0f, (XwGuard - depth) * 0.5f, length * 0.5f);
            return (centre, turn, new Vector3(kit.Half, (XwGuard + depth) * 0.5f, length * 0.5f));
        }

        /// <summary>Book steps up to a deck's edge: each book one hop higher and one tread nearer, the top one level with
        /// the road and touching <paramref name="edge"/>. The books overlap like a fanned pile, so nothing shows underneath.</summary>
        static GameObject XwBookSteps(Transform parent, string name, Vector3 edge, Vector3 climb, float top)
        {
            var holder = Child(parent, name);
            int count = Mathf.CeilToInt(top / XwStepRise); float rise = top / count;
            string[] books = { "Book_01", "Book_02", "Book_03", "Book_04", "Book_06", "Book_07", "Book_08", "Book_09", "Book_10", "Book_11", "Book_12" };
            Vector3 up = new Vector3(climb.x, 0f, climb.z).normalized;
            float yaw = Quaternion.LookRotation(up).eulerAngles.y;
            for (int i = 0; i < count; i++)
            {
                var book = Spawn(books[i % books.Length], "Book" + i, holder, edge, new Vector3(0f, yaw, 0f), 1f);
                var bb = B(book);
                float reach = Mathf.Abs(up.x) * bb.extents.x + Mathf.Abs(up.z) * bb.extents.z;
                Vector3 face = new Vector3(edge.x, 0f, edge.z) - up * ((count - 1 - i) * XwStepRun) + up * 0.6f;   // the top book laps the deck's lip a little
                Vector3 centre = face - up * reach;
                book.transform.position += new Vector3(centre.x - bb.center.x, (i + 1) * rise - bb.max.y, centre.z - bb.center.z);
            }
            return holder.gameObject;
        }

        // ---------- making room ----------

        /// <summary>A keep-out column over a footprint: everything from <paramref name="fromY"/> up.</summary>
        static Bounds XwColumn(Bounds footprint, float fromY)
        {
            var b = new Bounds(); b.SetMinMax(new Vector3(footprint.min.x, fromY, footprint.min.z), new Vector3(footprint.max.x, 500f, footprint.max.z));
            return b;
        }

        /// <summary>Dressing is what the room's dresser scattered: it carries the pack prefab's own name (Book_11,
        /// Wooden_Cylinder_01). The room's beats have names of their own and stay put; the hoop's ball is the one
        /// exception that may roll aside.</summary>
        static bool XwIsDressing(Transform t)
        {
            if (t.name != "Ball" && (!Regex.IsMatch(t.name, @"_\d\d$") || t.name.StartsWith("Army_"))) return false;
            if (t.GetComponentInChildren<ShiftSource>() != null || t.GetComponentInChildren<Carryable>() != null || t.GetComponentInChildren<Portal>() != null
                || t.GetComponentInChildren<Checkpoint>() != null || t.GetComponentInChildren<Door>() != null || t.GetComponentInChildren<Updraft>() != null
                || t.GetComponentInChildren<LaunchPad>() != null || t.GetComponentInChildren<ToyCar>() != null || t.GetComponentInChildren<Light>() != null) return false;
            if (t.GetComponentsInChildren<Renderer>().Length == 0) return false;
            var b = B(t.gameObject);
            return b.size.x <= 30f && b.size.z <= 30f && b.min.y <= 6f;
        }

        /// <summary>Moves the dressing that stands in a keep-out to the nearest free floor: outward in a spiral until its
        /// box is out of every keep-out, inside the room and touching nothing solid. Things stacked on one another (a pile
        /// of books) move as one; a thing resting on furniture is left where it is. Returns how many piles moved.</summary>
        static int XwMakeRoom(Transform r4, Transform expressway, List<Bounds> keepOut)
        {
            Physics.SyncTransforms();
            var props = new List<(Transform t, Bounds b)>();
            foreach (Transform t in r4) if (t != expressway && XwIsDressing(t)) props.Add((t, B(t.gameObject)));
            props.Sort((x, y) => x.b.min.y.CompareTo(y.b.min.y));
            // piles: whatever rests on another piece of dressing joins that piece's pile
            const float onFloor = 0.6f;
            var pileOf = new int[props.Count]; var piles = new List<List<int>>();
            for (int i = 0; i < props.Count; i++)
            {
                pileOf[i] = -1;
                if (props[i].b.min.y <= onFloor) { pileOf[i] = piles.Count; piles.Add(new List<int> { i }); continue; }
                for (int j = i - 1; j >= 0 && pileOf[i] < 0; j--)
                {
                    var under = props[j].b; Vector3 c = props[i].b.center;
                    if (pileOf[j] >= 0 && c.x > under.min.x && c.x < under.max.x && c.z > under.min.z && c.z < under.max.z && Mathf.Abs(props[i].b.min.y - under.max.y) < onFloor)
                    { pileOf[i] = pileOf[j]; piles[pileOf[j]].Add(i); }
                }
            }
            int moved = 0;
            foreach (var pile in piles)
            {
                if (!pile.Any(i => keepOut.Any(o => o.Intersects(props[i].b)))) continue;
                Bounds all = props[pile[0]].b; foreach (int i in pile) all.Encapsulate(props[i].b);
                string label = props[pile[0]].t.name + (pile.Count > 1 ? " and " + (pile.Count - 1) + " on it" : "");
                bool placed = false;
                for (int k = 1; k <= 90 && !placed; k++)
                {
                    float angle = k * 2.4f, reach = 3f + k * 1.2f;
                    Vector3 shift = new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
                    var nb = new Bounds(all.center + shift, all.size);
                    if (Mathf.Abs(nb.center.x - C4.x) + nb.extents.x > R4HalfX - 4f || Mathf.Abs(nb.center.z - C4.z) + nb.extents.z > R4HalfZ - 4f) continue;
                    if (keepOut.Any(o => o.Intersects(nb))) continue;
                    Vector3 half = new Vector3(nb.extents.x, Mathf.Max(0.1f, nb.extents.y - 0.3f), nb.extents.z);
                    bool free = true;
                    foreach (var hit in Physics.OverlapBox(new Vector3(nb.center.x, nb.min.y + 0.6f + half.y, nb.center.z), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                        if (!pile.Any(i => hit.transform.IsChildOf(props[i].t))) { free = false; break; }
                    if (!free) continue;
                    foreach (int i in pile) props[i].t.position += shift;
                    placed = true;
                    Physics.SyncTransforms();
                    Log.AppendLine($"  moved aside {label} from {all.center - C4} by {shift.magnitude:F0}");
                }
                if (placed) moved++; else Log.AppendLine($"  COULD NOT MOVE {label} at {all.center - C4}: no free floor within reach");
            }
            return moved;
        }

        // ---------- stills ----------

        /// <summary>Stills of the expressway into %TEMP%/sj_d0048_stills: the room from above (ceilings hidden), each stop,
        /// and the view down the road from a parked car.</summary>
        static void ExpresswayStills(Transform r4)
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sj_d0048_stills"); System.IO.Directory.CreateDirectory(dir);   // not the project Temp: Unity empties it on exit
            var root = r4.Find("Expressway"); if (root == null) return;
            var ceilings = new List<GameObject>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.gameObject.name.StartsWith("Ceiling") && r.gameObject.activeSelf && (r.transform.position - C4).magnitude < 400f) { ceilings.Add(r.gameObject); r.gameObject.SetActive(false); }
            try
            {
                Shot(dir + "/top.png", C4 + new Vector3(0f, 300f, 0f), Quaternion.Euler(90f, 0f, 0f), 0f, 1680, 1400, 166f);
                Vector3 eye = C4 + new Vector3(-170f, 110f, -145f); Shot(dir + "/over_sw.png", eye, Quaternion.LookRotation(C4 + new Vector3(-20f, 0f, 0f) - eye), 70f, 1600, 900);
                eye = C4 + new Vector3(175f, 110f, 145f); Shot(dir + "/over_ne.png", eye, Quaternion.LookRotation(C4 + new Vector3(10f, 0f, -10f) - eye), 70f, 1600, 900);
            }
            finally { foreach (var g in ceilings) g.SetActive(true); }
            var stops = root.Find("Stops");
            if (stops != null)
                foreach (Transform stop in stops)
                {
                    var sb = B(stop.gameObject); Vector3 mid = sb.center; Vector3 away = (mid - C4); away.y = 0f; away = away.sqrMagnitude > 1f ? away.normalized : Vector3.forward;
                    // from inside the room looking out at the stop, and from a toy's eye on the floor beside it
                    Vector3 eye = new Vector3(mid.x, 0f, mid.z) - away * 42f + Vector3.Cross(Vector3.up, away) * 30f + Vector3.up * 22f;
                    Shot(dir + "/stop_" + stop.name + ".png", eye, Quaternion.LookRotation(new Vector3(mid.x, XwHeight * 0.6f, mid.z) - eye), 60f, 1280, 720);
                    Vector3 toy = new Vector3(mid.x, 0f, mid.z) + Vector3.Cross(Vector3.up, away) * 34f + away * 6f + Vector3.up * 2.2f;
                    Shot(dir + "/stop_" + stop.name + "_toy.png", toy, Quaternion.LookRotation(new Vector3(mid.x, XwHeight * 0.7f, mid.z) - toy), 70f, 1280, 720);
                }
            var cars = root.Find("Cars");
            if (cars != null)
                foreach (Transform car in cars)
                {
                    Vector3 eye = car.position + car.rotation * new Vector3(0f, 3.2f, -8f);
                    Shot(dir + "/drive_" + car.name + ".png", eye, Quaternion.LookRotation(car.position + car.forward * 14f + Vector3.up - eye), 65f, 1280, 720);
                }
            Log.AppendLine("stills in " + dir);
        }
    }
}
#endif
