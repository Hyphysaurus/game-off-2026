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
    /// The Raceway (D-0050): a room of its own (Raceway.unity, a sandbox like the Workshop) holding a circuit with height
    /// in it: a vertical loop, two banked hairpins, a two-turn spiral up to a high deck and a steep dive back down. The
    /// circuit is written as a list of segments (<see cref="RwCircuit"/>) and laid piece by piece from a start pose;
    /// straights are the pack's own track piece, everything bent is swept from it by <see cref="TrackBender.Sweep"/> and
    /// saved under <see cref="RwMeshDir"/> (pack-derived, out of git). The whole room is rebuilt on every run.
    /// </summary>
    public static partial class CourseBuilder
    {
        const string RacewayScenePath = "Assets/_Game/Scenes/Raceway.unity";
        const string WorkshopScenePath = "Assets/_Game/Scenes/Workshop.unity";
        const string RwMeshDir = "Assets/_Imports/Generated/Raceway";
        const string TraversalPieces = "Assets/_Game/Prefabs/Pieces/Traversal/";

        const float RwBase = 10f;          // the road's height on the level stretches: the toy walks underneath
        const float RwWall = 2f;           // the walls beside the road, above its surface (they show, unlike the playroom's guards)
        const int RwSlices = 14;           // cuts along a swept piece: enough that a 45 degree bend reads as round

        enum RwKind { Straight, Turn, Loop, Helix, Dive, Mark }

        /// <summary>One segment of a circuit. Lengths in room units, angles in degrees; a negative turn is to the left.</summary>
        readonly struct RwSeg
        {
            public readonly RwKind Kind; public readonly float A, B, C, D; public readonly string Tag;
            RwSeg(RwKind kind, float a = 0f, float b = 0f, float c = 0f, float d = 0f, string tag = null) { Kind = kind; A = a; B = b; C = c; D = d; Tag = tag; }
            /// <summary>Straight on for <paramref name="length"/>.</summary>
            public static RwSeg Straight(float length) => new RwSeg(RwKind.Straight, length);
            /// <summary>A flat bend of <paramref name="degrees"/> (a multiple of 45) at <paramref name="radius"/>, leaning
            /// into the bend by <paramref name="bank"/> degrees; the lean eases in over the first piece and out over the last.</summary>
            public static RwSeg Turn(float degrees, float radius, float bank) => new RwSeg(RwKind.Turn, degrees, radius, bank);
            /// <summary>A full vertical loop of <paramref name="radius"/> that comes out <paramref name="shift"/> to the
            /// side of where it went in (negative = to the left), so the way out clears the way in.</summary>
            public static RwSeg Loop(float radius, float shift) => new RwSeg(RwKind.Loop, radius, shift);
            /// <summary>A spiral: <paramref name="turns"/> full turns (negative = to the left) at <paramref name="radius"/>,
            /// climbing <paramref name="risePerTurn"/> each turn, leaning in by <paramref name="bank"/>.</summary>
            public static RwSeg Helix(float turns, float radius, float risePerTurn, float bank) => new RwSeg(RwKind.Helix, turns, radius, risePerTurn, bank);
            /// <summary>Down by <paramref name="drop"/>: over a crest, down a slope of <paramref name="degrees"/>, and level again; both curves at <paramref name="radius"/>.</summary>
            public static RwSeg Dive(float drop, float degrees, float radius) => new RwSeg(RwKind.Dive, drop, degrees, radius);
            /// <summary>Something set down here: "Start", "Finish", "Gate" or "Boost".</summary>
            public static RwSeg Mark(string tag) => new RwSeg(RwKind.Mark, tag: tag);
        }

        /// <summary>How far forward a dive carries the road: the two curves plus the slope between them.</summary>
        static float RwDiveRun(float drop, float degrees, float radius)
        {
            float rad = degrees * Mathf.Deg2Rad, curves = 2f * radius * (1f - Mathf.Cos(rad));
            return 2f * radius * Mathf.Sin(rad) + Mathf.Max(0f, drop - curves) / Mathf.Tan(rad);
        }

        // ---- the circuit: anticlockwise, starting at the west end of the south straight, heading east ----
        // The two long sides must be the same length for it to close. South: 150 of straight (the loop adds none).
        // North: two 25s, the dive's run, and one straight that takes up what is left.
        // The loop comes out 16 to the left, so the east hairpin's radius is 8 less than the west one's.
        const float RwSide = 150f, RwLoopShift = 16f, RwWestRadius = 60f, RwClimb = 32f, RwDiveAngle = 30f, RwDiveRadius = 40f;
        static readonly Vector3 RwStart = new Vector3(-75f, RwBase, -80f);

        static readonly RwSeg[] RwCircuit =
        {
            RwSeg.Mark("Start"),
            RwSeg.Straight(25f),
            RwSeg.Mark("Finish"),
            RwSeg.Straight(50f),
            RwSeg.Mark("Boost"),
            RwSeg.Straight(25f),
            RwSeg.Loop(28f, -RwLoopShift),
            RwSeg.Straight(50f),
            RwSeg.Turn(-180f, RwWestRadius - RwLoopShift * 0.5f, 20f),
            RwSeg.Mark("Gate"),
            RwSeg.Straight(25f),
            RwSeg.Helix(-2f, 40f, RwClimb * 0.5f, 10f),
            RwSeg.Mark("Gate"),
            RwSeg.Straight(25f),
            RwSeg.Dive(RwClimb, RwDiveAngle, RwDiveRadius),
            RwSeg.Mark("Boost"),
            RwSeg.Straight(RwSide - 50f - RwDiveRun(RwClimb, RwDiveAngle, RwDiveRadius)),
            RwSeg.Mark("Gate"),
            RwSeg.Turn(-180f, RwWestRadius, 20f),
        };

        /// <summary>Where the layer stands: a point, the road's heading and slope there (no lean in it), and the lean.</summary>
        sealed class RwCursor
        {
            public Vector3 Pos; public Quaternion Level = Quaternion.identity; public float Roll;
            public Quaternion Turn => Level * Quaternion.AngleAxis(Roll, Vector3.forward);
        }

        sealed class RwPiece
        {
            public GameObject Go;
            public readonly List<(Vector3 at, Quaternion turn)> Samples = new List<(Vector3, Quaternion)>();   // points along its road, for the checks
            public Vector3 Start; public Quaternion StartTurn;
        }

        sealed class RwLayer
        {
            public XwKit Kit; public Mesh Source; public Vector2 RailUv; public Transform Parent;
            public RwCursor Cursor = new RwCursor();
            public readonly List<RwPiece> Pieces = new List<RwPiece>();
            public readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
            public int Count;
        }

        [MenuItem("Shift Jam/Raceway/Build Raceway Room")]
        public static void BuildRacewayRoomMenu()
        {
            if (SceneManager.GetActiveScene().path != RacewayScenePath) { Debug.LogError("Open Raceway.unity first (this builds into the active scene). No Raceway.unity yet: run the batch LandRaceway once."); return; }
            Log.Clear();
            BuildRacewayRoom();
            Debug.Log("RACEWAY BUILT\n" + Log);
        }

        /// <summary>Batch: -executeMethod ShiftJam.CourseBuilder.LandRaceway (no -nographics: it renders stills). Makes
        /// Raceway.unity from the Workshop scene the first time (its player, camera and Build Mode; the Workshop's own room
        /// is removed), builds the room and the circuit, saves, adds the scene to the build list, stills into
        /// %TEMP%/sj_d0050_stills.</summary>
        public static void LandRaceway()
        {
            Log.Clear();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RacewayScenePath) == null && !AssetDatabase.CopyAsset(WorkshopScenePath, RacewayScenePath))
            { Debug.LogError("D-0050: could not make " + RacewayScenePath + " from " + WorkshopScenePath); EditorApplication.Exit(1); return; }
            EditorSceneManager.OpenScene(RacewayScenePath);
            BuildRacewayRoom();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == RacewayScenePath)) { scenes.Add(new EditorBuildSettingsScene(RacewayScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets();
            RacewayStills();
            Debug.Log("D-0050 LANDED\n" + Log);
        }

        static void BuildRacewayRoom()
        {
            foreach (var name in new[] { "Workshop", "Raceway" }) { var old = GameObject.Find(name); if (old != null) Object.DestroyImmediate(old); }
            var kit = XwMakeKit(); if (kit == null) return;
            var root = new GameObject("Raceway").transform;
            Transform shell = Child(root, "Shell"), scenery = Child(root, "Scenery"), track = Child(root, "Track"), props = Child(root, "Props"), pillars = Child(root, "Pillars"), cars = Child(root, "Cars");
            SandboxShell(shell, Vector3.zero, WorkshopHalf);

            // plain furniture against the walls, as the room the track was laid out in
            GameObject Against(string prefab, string name, Vector3 at, float yaw)
            {
                var go = Rest(Spawn(prefab, name, scenery, at, new Vector3(0, yaw, 0), 1f), 0f, null); var b = B(go);
                float h = WorkshopHalf; Vector3 push = Vector3.zero;
                if (at.z > h - 80f) push.z = (h - 1f) - b.max.z; else if (at.z < -h + 80f) push.z = (-h + 1f) - b.min.z;
                if (at.x > h - 80f) push.x = (h - 1f) - b.max.x; else if (at.x < -h + 80f) push.x = (-h + 1f) - b.min.x;
                go.transform.position += push; return go;
            }
            Against("Bed_01", "Bed", new Vector3(-190f, 0, 200f), 0f);
            Against("Table_01", "Desk", new Vector3(60f, 0, 240f), 0f);
            Against("Sofa_01", "Sofa", new Vector3(250f, 0, 40f), -90f);
            Against("Wardrobe_01", "Wardrobe", new Vector3(230f, 0, -230f), -90f);
            Against("Shelf_01", "Shelf", new Vector3(-250f, 0, -40f), 90f);

            // ---- lay the circuit ----
            var filter = kit.Straight.GetComponentInChildren<MeshFilter>();
            var layer = new RwLayer { Kit = kit, Source = filter.sharedMesh, RailUv = TrackBender.RailUv(filter.sharedMesh), Parent = track };
            layer.Cursor.Pos = RwStart; layer.Cursor.Level = Quaternion.LookRotation(Vector3.right);
            Vector3 startPos = layer.Cursor.Pos; Quaternion startTurn = layer.Cursor.Turn;
            int gate = 0; bool openStart = false;
            foreach (var seg in RwCircuit)
            {
                switch (seg.Kind)
                {
                    case RwKind.Straight:
                        RwStraights(layer, seg.A, openStart); openStart = false; break;
                    case RwKind.Turn:
                        RwTurn(layer, seg.A, seg.B, 0f, seg.C); break;
                    case RwKind.Helix:
                    {
                        float pitch = Mathf.Atan(seg.C / (2f * Mathf.PI * seg.B)) * Mathf.Rad2Deg;
                        layer.Cursor.Level *= Quaternion.Euler(-pitch, 0f, 0f);       // onto the spiral's slope (a few degrees: no easing piece)
                        RwTurn(layer, 360f * seg.A, seg.B, pitch, seg.D);
                        layer.Cursor.Level *= Quaternion.Euler(pitch, 0f, 0f);
                        break;
                    }
                    case RwKind.Loop:
                        for (int i = 0; i < 8; i++) RwArc(layer, 45f, seg.A, seg.B / 8f, true);
                        break;
                    case RwKind.Dive:
                    {
                        float rad = seg.B * Mathf.Deg2Rad, curves = 2f * seg.C * (1f - Mathf.Cos(rad));
                        RwArc(layer, -seg.B, seg.C, 0f, false);   // the crest is where the car is meant to fly
                        float slope = Mathf.Max(0f, seg.A - curves) / Mathf.Sin(rad);
                        if (slope > 1f) RwStraights(layer, slope, false);
                        RwArc(layer, seg.B, seg.C, 0f, false);
                        break;
                    }
                    case RwKind.Mark:
                        if (seg.Tag == "Start")
                        {
                            openStart = true;   // the first piece has no wall on the outside: the steps arrive there
                            Vector3 fwd = layer.Cursor.Turn * Vector3.forward, right = layer.Cursor.Turn * Vector3.right;
                            XwBookSteps(props, "Steps", layer.Cursor.Pos + fwd * 12f + right * kit.Half, -right, RwBase);
                            string[] rides = { "CyberCar14", "CyberCar05", "CyberCar09" };
                            for (int i = 0; i < rides.Length; i++)
                            {
                                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CarPieces + rides[i] + ".prefab");
                                if (asset == null) { Log.AppendLine("  NO CAR " + rides[i] + " (run Shift Jam > Workshop > Build Combat & Vehicle Pieces)"); continue; }
                                var car = (GameObject)PrefabUtility.InstantiatePrefab(asset, cars); car.name = "Racer" + (i + 1);
                                car.transform.SetPositionAndRotation(layer.Cursor.Pos + fwd * (5f + i * 6.5f) + right * (i % 2 == 0 ? -1.9f : 1.9f) + Vector3.up * 0.02f, layer.Cursor.Turn);
                            }
                            var player = GameObject.Find("Player");
                            if (player != null) player.transform.SetPositionAndRotation(layer.Cursor.Pos + fwd * 12f + right * 52f - Vector3.up * (RwBase - 0.3f), Quaternion.LookRotation(-right));
                        }
                        else RwProp(layer, props, seg.Tag, ref gate);
                        break;
                }
            }
            float gap = (layer.Cursor.Pos - startPos).magnitude, off = Quaternion.Angle(layer.Cursor.Turn, startTurn);
            float lap = 0f; foreach (var p in layer.Pieces) for (int i = 1; i < p.Samples.Count; i++) lap += (p.Samples[i].at - p.Samples[i - 1].at).magnitude;
            Log.AppendLine($"raceway circuit: {layer.Pieces.Count} pieces, about {lap / 0.8f:F0} units a lap; closes with a gap of {gap:F3} and {off:F2} degrees; {layer.Meshes.Count} swept shapes");

            // ---- pillars: under each piece's start where it is level enough and nothing but floor is below ----
            Physics.SyncTransforms();
            int stood = 0; float depth = kit.Shape.Depth * XwScale;
            foreach (var piece in layer.Pieces)
            {
                Vector3 up = piece.StartTurn * Vector3.up; if (up.y < 0.9f) continue;
                Vector3 under = piece.Start - up * (depth + 0.05f);
                if (!Physics.Raycast(under, Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore) || !hit.collider.transform.IsChildOf(shell)) continue;
                float height = under.y - hit.point.y; if (height < 1f) continue;
                var pillar = Spawn(XwPillar, "Pillar", pillars, hit.point, Vector3.zero, 1f);
                foreach (var body in pillar.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(body);
                var size = B(pillar).size;
                pillar.transform.localScale = new Vector3(XwPillarWidth / Mathf.Max(0.01f, size.x), height / Mathf.Max(0.01f, size.y), XwPillarWidth / Mathf.Max(0.01f, size.z));
                var pb = B(pillar); pillar.transform.position += new Vector3(hit.point.x - pb.center.x, hit.point.y - pb.min.y, hit.point.z - pb.center.z);
                stood++;
            }

            // ---- is the road really there, the right way up, with a car's worth of air above it ----
            Physics.SyncTransforms();
            int rays = 0, holes = 0, blocked = 0; float worst = 0f, roadHalf = kit.Shape.RoadHalf * XwScale;
            foreach (var piece in layer.Pieces)
                foreach (var sample in piece.Samples)
                {
                    Vector3 up = sample.turn * Vector3.up, right = sample.turn * Vector3.right;
                    foreach (float across in new[] { -0.5f, 0f, 0.5f })
                    {
                        rays++;
                        Vector3 from = sample.at + right * (across * roadHalf) + up * 3f; bool found = false;
                        foreach (var h in Physics.RaycastAll(from, -up, 6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                        {
                            if (!h.collider.transform.IsChildOf(piece.Go.transform)) continue;
                            found = true; worst = Mathf.Max(worst, Mathf.Abs(h.distance - 3f));
                            if (Mathf.Abs(h.distance - 3f) > 0.15f) holes++;
                            break;
                        }
                        if (!found) holes++;
                    }
                    foreach (var c in Physics.OverlapBox(sample.at + up * 1.3f, new Vector3(1.1f, 0.85f, 1.2f), sample.turn, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (c.transform.IsChildOf(cars) || c.transform.IsChildOf(props)) continue;   // parked racers, arches and pads belong there
                        blocked++; Log.AppendLine($"  LANE BLOCKED on {piece.Go.name} by {c.name}"); break;
                    }
                }
            Log.AppendLine($"  road check: {rays} rays, {holes} missed or off the road's height (worst {worst:F3}), {blocked} places with something in the lane; pillars {stood}");
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        // ---------- laying ----------

        /// <summary>Straight pieces for <paramref name="length"/>: whole pieces, stretched a little to fit. They are the
        /// pack's own prefab, with a wall each side (not on the right of the first one when <paramref name="openFirst"/>).</summary>
        static void RwStraights(RwLayer layer, float length, bool openFirst)
        {
            var kit = layer.Kit; var shape = kit.Shape;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / kit.PieceLength)); float each = length / count, stretch = each / shape.Length;
            for (int i = 0; i < count; i++)
            {
                Quaternion turn = layer.Cursor.Turn;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(kit.Straight, layer.Parent); go.name = "S" + layer.Count++;
                go.transform.localScale = new Vector3(XwScale, XwScale, stretch);
                go.transform.SetPositionAndRotation(layer.Cursor.Pos - turn * new Vector3(0f, 0f, shape.Start * stretch), turn);
                bool right = !(openFirst && i == 0);
                var walls = RwMesh(layer, right ? "Wall_Straight" : "Wall_Straight_LeftOnly", () => TrackBender.SweepWalls(shape, "walls", RwWall / XwScale, true, right, 2,
                    (float t, out Vector3 position, out Quaternion r) => { position = new Vector3(0f, 0f, shape.Length * t); r = Quaternion.identity; }, layer.RailUv));
                RwWalls(layer, go, walls, new Vector3(0f, 0f, shape.Start));
                var piece = new RwPiece { Go = go, Start = layer.Cursor.Pos, StartTurn = turn };
                for (int k = 0; k < 5; k++) piece.Samples.Add((layer.Cursor.Pos + turn * new Vector3(0f, 0f, each * (0.1f + 0.2f * k)), turn));
                layer.Pieces.Add(piece);
                layer.Cursor.Pos += turn * new Vector3(0f, 0f, each);
            }
        }

        /// <summary>A bend of <paramref name="degrees"/> in 45 degree pieces at <paramref name="radius"/>, on a constant
        /// slope of <paramref name="pitch"/> degrees (the cursor must already be on that slope), leaning in by
        /// <paramref name="bank"/>: the lean eases in over the first piece and out over the last.</summary>
        static void RwTurn(RwLayer layer, float degrees, float radius, float pitch, float bank)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(degrees) / 45f));
            float each = degrees / count, lean = count > 1 ? bank * (degrees < 0f ? 1f : -1f) : 0f;   // a left bend lifts the right (outer) side
            float r = radius / XwScale;
            Quaternion slope = Quaternion.Euler(-pitch, 0f, 0f);
            for (int i = 0; i < count; i++)
            {
                float roll0 = i == 0 ? 0f : lean, roll1 = i == count - 1 ? 0f : lean;
                TrackBender.Path path = RwYawPath(each, r, pitch, roll0, roll1);
                RwSwept(layer, $"Yaw_{each:F1}_R{radius:F1}_p{pitch:F2}_r{roll0:F0}_{roll1:F0}", "T", path, bank > 0f || pitch != 0f);
                layer.Cursor.Level *= Quaternion.Inverse(slope) * Quaternion.Euler(0f, each, 0f) * slope;
                layer.Cursor.Roll = roll1;
            }
        }

        /// <summary>A curve in the upright plane: <paramref name="degrees"/> up (positive) or over and down (negative) at
        /// <paramref name="radius"/>, drifting <paramref name="shift"/> to the side as it goes. The road must not be leaning.
        /// <paramref name="sticky"/> marks it as a piece the car holds on to (a loop).</summary>
        static void RwArc(RwLayer layer, float degrees, float radius, float shift, bool sticky)
        {
            if (Mathf.Abs(layer.Cursor.Roll) > 0.01f) Debug.LogError("Raceway: an up-or-down curve needs a road that is not leaning; ease the lean out first.");
            float r = radius / XwScale, side = shift / XwScale;
            TrackBender.Path path = (float t, out Vector3 position, out Quaternion turn) =>
            {
                float a = degrees * t, rad = Mathf.Abs(a) * Mathf.Deg2Rad;
                position = new Vector3(side * t, Mathf.Sign(degrees) * r * (1f - Mathf.Cos(rad)), r * Mathf.Sin(rad));
                turn = Quaternion.Euler(-a, 0f, 0f);
            };
            RwSwept(layer, $"Arc_{degrees:F1}_R{radius:F1}_s{shift:F2}", "A", path, sticky);
            layer.Cursor.Level *= Quaternion.Euler(-degrees, 0f, 0f);
        }

        /// <summary>The path of a flat or climbing bend, measured from its own start: <paramref name="degrees"/> round
        /// (positive = right) at <paramref name="radius"/>, on a slope of <paramref name="pitch"/>, the lean going from
        /// <paramref name="roll0"/> to <paramref name="roll1"/>. It turns about the upright, not about the leaning road.</summary>
        static TrackBender.Path RwYawPath(float degrees, float radius, float pitch, float roll0, float roll1)
        {
            Quaternion slope = Quaternion.Euler(-pitch, 0f, 0f);
            Quaternion back = Quaternion.Inverse(slope * Quaternion.AngleAxis(roll0, Vector3.forward));
            float climb = Mathf.Tan(pitch * Mathf.Deg2Rad);
            return (float t, out Vector3 position, out Quaternion turn) =>
            {
                float a = degrees * t;
                Vector3 flat = TrackBender.Bend(Vector3.zero, a, radius);
                float run = radius * Mathf.Abs(a) * Mathf.Deg2Rad;
                position = back * new Vector3(flat.x, run * climb, flat.z);
                turn = back * Quaternion.Euler(0f, a, 0f) * slope * Quaternion.AngleAxis(Mathf.Lerp(roll0, roll1, t), Vector3.forward);
            };
        }

        /// <summary>Sets a swept piece down at the cursor and moves the cursor's point to its end (the caller turns the
        /// cursor). The mesh and its walls are made once per shape and shared.</summary>
        static void RwSwept(RwLayer layer, string key, string prefix, TrackBender.Path path, bool sticky)
        {
            var kit = layer.Kit; Quaternion turn = layer.Cursor.Turn;
            var road = RwMesh(layer, key, () => TrackBender.Sweep(layer.Source, key, true, true, RwSlices, path));
            var walls = RwMesh(layer, key + "_Walls", () => TrackBender.SweepWalls(kit.Shape, key + "_Walls", RwWall / XwScale, true, true, RwSlices, path, layer.RailUv));
            var go = new GameObject(prefix + layer.Count++); go.transform.SetParent(layer.Parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = road;
            go.AddComponent<MeshRenderer>().sharedMaterial = kit.Material;
            go.AddComponent<MeshCollider>().sharedMesh = road;
            go.transform.localScale = Vector3.one * XwScale;
            go.transform.SetPositionAndRotation(layer.Cursor.Pos, turn);
            if (sticky) go.AddComponent<StickyTrack>();   // the car holds on here (ArcadeCar's air step reads it)
            RwWalls(layer, go, walls, Vector3.zero);
            var piece = new RwPiece { Go = go, Start = layer.Cursor.Pos, StartTurn = turn };
            for (int k = 0; k < 5; k++)
            {
                path(0.1f + 0.2f * k, out Vector3 p, out Quaternion r);
                piece.Samples.Add((layer.Cursor.Pos + turn * (p * XwScale), turn * r));
            }
            layer.Pieces.Add(piece);
            path(1f, out Vector3 end, out _);
            layer.Cursor.Pos += turn * (end * XwScale);
        }

        static void RwWalls(RwLayer layer, GameObject piece, Mesh walls, Vector3 localPosition)
        {
            var w = new GameObject("Walls"); w.transform.SetParent(piece.transform, false); w.transform.localPosition = localPosition;
            w.AddComponent<MeshFilter>().sharedMesh = walls;
            w.AddComponent<MeshRenderer>().sharedMaterial = layer.Kit.Material;
            w.AddComponent<MeshCollider>().sharedMesh = walls;
        }

        /// <summary>One mesh asset per shape, made the first time it is asked for in a build and remade in place after.</summary>
        static Mesh RwMesh(RwLayer layer, string key, System.Func<Mesh> make)
        {
            if (layer.Meshes.TryGetValue(key, out Mesh have)) return have;
            if (!AssetDatabase.IsValidFolder(RwMeshDir))
            {
                string parent = "Assets";
                foreach (var part in RwMeshDir.Substring("Assets/".Length).Split('/'))
                {
                    if (!AssetDatabase.IsValidFolder(parent + "/" + part)) AssetDatabase.CreateFolder(parent, part);
                    parent += "/" + part;
                }
            }
            string file = key.Replace('.', '_').Replace('-', 'm'), path = RwMeshDir + "/" + file + ".asset";
            Mesh made = make(); made.name = file;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(made, path);
            else { existing.Clear(); EditorUtility.CopySerialized(made, existing); existing.name = file; EditorUtility.SetDirty(existing); Object.DestroyImmediate(made); made = existing; }
            layer.Meshes[key] = made;
            return made;
        }

        /// <summary>A race or boost piece across the road at the cursor: the finish arch and the numbered gates are sized
        /// so their posts stand just outside the walls; the boost pad lies on the road.</summary>
        static void RwProp(RwLayer layer, Transform parent, string tag, ref int gate)
        {
            string prefab = tag == "Finish" ? "RaceFinish" : tag == "Gate" ? "RaceGate_" + (++gate) : tag == "Boost" ? "BoostArrow" : null;
            var asset = prefab != null ? AssetDatabase.LoadAssetAtPath<GameObject>(TraversalPieces + prefab + ".prefab") : null;
            if (asset == null) { Log.AppendLine($"  NO PIECE for mark {tag} ({prefab}): run Shift Jam > Workshop > Build Race Pieces / Build Boost Pieces"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent); go.name = tag + (tag == "Gate" ? gate.ToString() : "");
            float clear = layer.Kit.Half * 2f + 1.5f;
            if (tag == "Finish") go.transform.localScale = Vector3.one * (clear / RaceBuilder.FinishOpening);
            else if (tag == "Gate") go.transform.localScale = Vector3.one * (clear / RaceBuilder.GateOpening);
            go.transform.SetPositionAndRotation(layer.Cursor.Pos + layer.Cursor.Turn * Vector3.up * 0.03f, layer.Cursor.Turn);
        }

        // ---------- stills ----------

        static void RacewayStills()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sj_d0050_stills"); System.IO.Directory.CreateDirectory(dir);   // not the project Temp: Unity empties it on exit
            Shot(dir + "/top.png", new Vector3(0f, 400f, 0f), Quaternion.Euler(90f, 0f, 0f), 0f, 1400, 1400, 270f);
            Vector3 look = new Vector3(0f, 20f, -20f);
            (string name, Vector3 eye, Vector3 at)[] views =
            {
                ("from_sw", new Vector3(-230f, 110f, -230f), look),
                ("from_se", new Vector3(230f, 100f, -220f), look),
                ("from_ne", new Vector3(220f, 120f, 200f), look),
                ("loop", RwStart + new Vector3(60f, 14f, -70f), RwStart + new Vector3(105f, 28f, 0f)),
                ("helix", new Vector3(130f, 60f, 110f), new Vector3(50f, 26f, 0f)),
                ("dive", new Vector3(-60f, 30f, 120f), new Vector3(-20f, 24f, 40f)),
                ("start", RwStart + new Vector3(-22f, 9f, -34f), RwStart + new Vector3(25f, 2f, 0f)),
                ("toy_at_steps", RwStart + new Vector3(12f, -RwBase + 2.2f, -52f), RwStart + new Vector3(12f, -2f, 0f)),
            };
            foreach (var v in views) Shot(dir + "/" + v.name + ".png", v.eye, Quaternion.LookRotation(v.at - v.eye), 60f, 1280, 720);
            Log.AppendLine("stills in " + dir);
        }
    }
}
#endif
