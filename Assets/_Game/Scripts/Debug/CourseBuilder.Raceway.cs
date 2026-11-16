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
        const float RwWiden = 2.25f;       // the road between the rails, as a multiple of the pack piece's (8.4 wide at 1): 2.25 = 18.9, about eight car widths (a car is 2.17), room for a pack of four racing side by side (D-0058; 1.5 = 12.5 was cramped). The rails keep their size.
        const float RwGateTall = 0.8f;   // D-0058: was 1.6. At the 2.25 road gate 1 (on the straight under the spiral) reached the spiral overhead at 1.6 and 1.2; 0.8 = 5.4 m: clear     // a gate or the finish arch is stretched across the road, but grows no taller than this times its own size
        const float RwWall = 2f;           // the walls beside the road, above its surface (they show, unlike the playroom's guards)
        const int RwSlices = 24;           // cuts along a swept piece: under 2 degrees a facet on a 45 degree piece, so the springs do not feel them

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
            /// side of where it went in (negative = to the left), so the way out clears the way in. The road is wound at a
            /// slant, so a car that drives straight stays in the middle of it; it also comes out a little further on
            /// (<see cref="RwLoopForward"/>).</summary>
            public static RwSeg Loop(float radius, float shift) => new RwSeg(RwKind.Loop, radius, shift);
            /// <summary>A spiral: <paramref name="turns"/> full turns (negative = to the left) at <paramref name="radius"/>,
            /// climbing <paramref name="risePerTurn"/> each turn, leaning in by <paramref name="bank"/>.</summary>
            public static RwSeg Helix(float turns, float radius, float risePerTurn, float bank) => new RwSeg(RwKind.Helix, turns, radius, risePerTurn, bank);
            /// <summary>Down by <paramref name="drop"/>: over a crest, down a slope of <paramref name="degrees"/>, and level again; both curves at <paramref name="radius"/>.</summary>
            public static RwSeg Dive(float drop, float degrees, float radius) => new RwSeg(RwKind.Dive, drop, degrees, radius);
            /// <summary>Something set down here: "Start", "Finish", "Gate", "Boost", or a piece of dressing on the straight
            /// that follows (<see cref="RwDress"/>): "Stars", "Heart", "Cones", "Stack", "Flags"; or on the straight just
            /// laid: "Tyres", "Arrows".</summary>
            public static RwSeg Mark(string tag) => new RwSeg(RwKind.Mark, tag: tag);
        }

        /// <summary>How far forward of its way in a slanted loop lets the road out (a loop wound straight would be 0).</summary>
        static float RwLoopForward(float radius, float shift)
        {
            float slant = Mathf.Asin(Mathf.Clamp(Mathf.Abs(shift) / (2f * Mathf.PI * radius), 0f, 0.9f));
            return Mathf.Abs(shift) * Mathf.Tan(slant);
        }

        /// <summary>How far forward a dive carries the road: the two curves plus the slope between them.</summary>
        static float RwDiveRun(float drop, float degrees, float radius)
        {
            float rad = degrees * Mathf.Deg2Rad, curves = 2f * radius * (1f - Mathf.Cos(rad));
            return 2f * radius * Mathf.Sin(rad) + Mathf.Max(0f, drop - curves) / Mathf.Tan(rad);
        }

        // ---- the circuit: anticlockwise, starting at the west end of the south straight, heading east ----
        // The two long sides must be the same length for it to close. South: 150 of straight plus the little the slanted
        // loop carries the road forward. North: two 25s, the dive's run, and one straight that takes up what is left.
        // The loop comes out RwLoopShift to the left (it must clear the road's own width: checked at build), so the east
        // hairpin's radius is half of that less than the west one's.
        const float RwSide = 150f, RwLoopRadius = 28f, RwLoopShift = 27f, RwWestRadius = 60f, RwClimb = 32f, RwDiveAngle = 30f, RwDiveRadius = 40f;
        static readonly Vector3 RwStart = new Vector3(-75f, RwBase, -80f);

        static readonly RwSeg[] RwCircuit =
        {
            RwSeg.Mark("Start"),
            RwSeg.Mark("Flags"),
            RwSeg.Straight(25f),
            RwSeg.Mark("Finish"),
            RwSeg.Mark("Stars"),
            RwSeg.Straight(50f),
            RwSeg.Mark("Boost"),
            RwSeg.Straight(25f),
            RwSeg.Loop(RwLoopRadius, -RwLoopShift),
            RwSeg.Mark("Cones"),
            RwSeg.Straight(40f),
            RwSeg.Mark("Gate"),            // on open road, well past the loop and clear of the spiral beside this straight;
            RwSeg.Straight(10f),           // after the hairpin the spiral's first turn passes right over the road
            RwSeg.Mark("Arrows"),
            RwSeg.Turn(-180f, RwWestRadius - RwLoopShift * 0.5f, 20f),
            RwSeg.Mark("Stars"),
            RwSeg.Straight(25f),
            RwSeg.Helix(-2f, 40f, RwClimb * 0.5f, 10f),
            RwSeg.Mark("Gate"),
            RwSeg.Mark("Stack"),
            RwSeg.Straight(25f),
            RwSeg.Dive(RwClimb, RwDiveAngle, RwDiveRadius),
            RwSeg.Mark("Boost"),
            RwSeg.Mark("Heart"),
            RwSeg.Straight(RwSide + RwLoopForward(RwLoopRadius, RwLoopShift) - 50f - RwDiveRun(RwClimb, RwDiveAngle, RwDiveRadius)),
            RwSeg.Mark("Gate"),
            RwSeg.Mark("Tyres"),
            RwSeg.Mark("Arrows"),
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
            public TrackBender.Shape Shape;      // the piece as laid here: the pack's, with its road widened by RwWiden (mesh units)
            public float Half, RoadHalf;         // half the whole width, and half the road between the rails, in room units
            public RwCursor Cursor = new RwCursor();
            public readonly List<RwPiece> Pieces = new List<RwPiece>();
            public readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
            public int Count;
            // the race (D-0061, CourseBuilder.RaceField.cs): the middle of the road as it is laid, where the finish line
            // falls in that list, and the cars stood on the grid
            public readonly List<(Vector3 at, Quaternion turn)> Line = new List<(Vector3, Quaternion)>();
            public int FinishAt = -1;
            public readonly List<(ArcadeCar car, string name, Vector3 at, Quaternion turn)> GridCars = new List<(ArcadeCar, string, Vector3, Quaternion)>();
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
            Transform smash = Child(root, "Smash"), pickups = Child(root, "Pickups");   // loose things to drive through; things to collect
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
            layer.Shape = TrackBender.Widened(kit.Shape, RwWiden); layer.Half = layer.Shape.OuterHalf * XwScale; layer.RoadHalf = layer.Shape.RoadHalf * XwScale;
            if (RwLoopShift < layer.Half * 2f + 1f) Debug.LogError($"Raceway: the loop comes out {RwLoopShift} to the side but the track is {layer.Half * 2f:F1} wide: its way out would run into its way in. Raise RwLoopShift.");
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
                        for (int i = 0; i < 8; i++)
                        {
                            RwLoopPiece(layer, seg.A, seg.B);
                            if (i == 3) RwPickup(pickups, "NitroStar", layer.Cursor.Pos + layer.Cursor.Turn * Vector3.up * 0.02f, layer.Cursor.Turn);   // one at the very top
                        }
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
                            XwBookSteps(props, "Steps", layer.Cursor.Pos + fwd * 12f + right * layer.Half, -right, RwBase);
                            RwGridCars(layer, cars);   // four cars on the grid: the player takes one, rivals drive the rest (D-0061)
                            var player = GameObject.Find("Player");
                            if (player != null) player.transform.SetPositionAndRotation(layer.Cursor.Pos + fwd * 12f + right * 52f - Vector3.up * (RwBase - 0.3f), Quaternion.LookRotation(-right));
                        }
                        else if (seg.Tag == "Finish" || seg.Tag == "Gate" || seg.Tag == "Boost")
                        {
                            if (seg.Tag == "Finish") layer.FinishAt = layer.Line.Count;   // the next point of the middle line is the finish line
                            RwProp(layer, props, seg.Tag, ref gate);
                        }
                        else RwDress(layer, smash, pickups, props, seg.Tag);
                        break;
                }
            }
            float gap = (layer.Cursor.Pos - startPos).magnitude, off = Quaternion.Angle(layer.Cursor.Turn, startTurn);
            float lap = 0f; foreach (var p in layer.Pieces) for (int i = 1; i < p.Samples.Count; i++) lap += (p.Samples[i].at - p.Samples[i - 1].at).magnitude;
            Log.AppendLine($"raceway circuit: {layer.Pieces.Count} pieces, about {lap / 0.8f:F0} units a lap; closes with a gap of {gap:F3} and {off:F2} degrees; {layer.Meshes.Count} swept shapes");

            // ---- the race: the path the rivals follow, baked from what was just laid, and the grid (D-0061) ----
            RwRace(layer, root, track, props, smash);

            // ---- pillars: under each piece's start where it is level enough and nothing but floor is below ----
            Physics.SyncTransforms();
            int stood = 0; float depth = kit.Shape.Depth * XwScale;
            foreach (var piece in layer.Pieces)
            {
                Vector3 up = piece.StartTurn * Vector3.up; if (up.y < 0.9f) continue;
                Vector3 under = piece.Start - up * (depth + 0.05f);
                if (!Physics.Raycast(under, Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore) || !hit.collider.transform.IsChildOf(shell)) continue;
                float height = under.y - hit.point.y; if (height < 1f) continue;
                // the whole pillar must stand free, not only its middle line: another stretch of road may pass beside it
                float girth = XwPillarWidth * 0.5f;
                if (height > 2f * girth + 1f && Physics.CheckCapsule(hit.point + Vector3.up * (girth + 0.4f), under - Vector3.up * (girth + 0.3f), girth, ~0, QueryTriggerInteraction.Ignore)) continue;
                var pillar = Spawn(XwPillar, "Pillar", pillars, hit.point, Vector3.zero, 1f);
                foreach (var body in pillar.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(body);
                var size = B(pillar).size;
                pillar.transform.localScale = new Vector3(XwPillarWidth / Mathf.Max(0.01f, size.x), height / Mathf.Max(0.01f, size.y), XwPillarWidth / Mathf.Max(0.01f, size.z));
                var pb = B(pillar); pillar.transform.position += new Vector3(hit.point.x - pb.center.x, hit.point.y - pb.min.y, hit.point.z - pb.center.z);
                stood++;
            }

            // ---- is the road really there, the right way up, with a car's worth of air above it ----
            Physics.SyncTransforms();
            int rays = 0, holes = 0, blocked = 0; float worst = 0f, roadHalf = layer.RoadHalf;
            const float laneHigh = 2.15f;   // from 0.45 over the road (a car's clearance) to 2.6 (over its roof)
            var blockedBy = new List<string>();
            int clipped = 0; float outerHalf = layer.Half, slab = kit.Shape.Depth * XwScale;
            for (int index = 0; index < layer.Pieces.Count; index++)
            {
                var piece = layer.Pieces[index];
                // its neighbours butt against it; everything else that touches its body or walls is a clip
                Transform before = layer.Pieces[(index - 1 + layer.Pieces.Count) % layer.Pieces.Count].Go.transform, after = layer.Pieces[(index + 1) % layer.Pieces.Count].Go.transform;
                foreach (var sample in piece.Samples)
                {
                    Vector3 up = sample.turn * Vector3.up, right = sample.turn * Vector3.right;
                    // inside the slab (not its skin: pillars meet its underside, the steps lap its lip) and inside each wall
                    var bodies = new (Vector3 centre, Vector3 half)[]
                    {
                        (sample.at - up * (slab * 0.5f), new Vector3(outerHalf - 0.75f, slab * 0.5f - 0.3f, 1.2f)),
                        (sample.at + up * (RwWall * 0.5f + 0.1f) - right * (roadHalf + (outerHalf - roadHalf) * 0.5f), new Vector3((outerHalf - roadHalf) * 0.5f - 0.2f, RwWall * 0.5f - 0.2f, 1.2f)),
                        (sample.at + up * (RwWall * 0.5f + 0.1f) + right * (roadHalf + (outerHalf - roadHalf) * 0.5f), new Vector3((outerHalf - roadHalf) * 0.5f - 0.2f, RwWall * 0.5f - 0.2f, 1.2f)),
                    };
                    foreach (var body in bodies)
                        foreach (var c in Physics.OverlapBox(body.centre, body.half, sample.turn, ~0, QueryTriggerInteraction.Ignore))
                        {
                            Transform t = c.transform;
                            if (t.IsChildOf(piece.Go.transform) || t.IsChildOf(before) || t.IsChildOf(after) || t.IsChildOf(cars)) continue;
                            var who = t; while (who.parent != null && who.parent != props && who.parent != scenery && who.parent != track && who.parent != pillars && who.parent != smash && who.parent != pickups) who = who.parent;
                            string line = $"  CLIPS: {who.name} stands in the body or a wall of {piece.Go.name}";
                            if (!blockedBy.Contains(line)) { blockedBy.Add(line); Log.AppendLine(line); clipped++; }
                        }
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
                    // the whole lane between the walls, from a car's clearance to well over its roof. Only the parked racers
                    // are let off: an arch or a pad must leave the lane clear too, on its own piece and on any piece that
                    // passes over or beside it (gate 1's top came up through the spiral's first turn: D-0054).
                    foreach (var c in Physics.OverlapBox(sample.at + up * (0.45f + laneHigh * 0.5f), new Vector3(roadHalf - 0.15f, laneHigh * 0.5f, 1.2f), sample.turn, ~0, QueryTriggerInteraction.Ignore))
                    {
                        // the parked racers, and what was put in the lane on purpose (cones, blocks, pickups), are let off
                        if (c.transform.IsChildOf(cars) || c.transform.IsChildOf(smash) || c.transform.IsChildOf(pickups) || c.transform.IsChildOf(piece.Go.transform)) continue;
                        var who = c.transform; while (who.parent != null && who.parent != props && who.parent != scenery && who.parent != track && who.parent != pillars) who = who.parent;
                        string line = $"  LANE BLOCKED on {piece.Go.name} by {who.name}";
                        if (!blockedBy.Contains(line)) { blockedBy.Add(line); Log.AppendLine(line); }
                        blocked++; break;
                    }
                }
            }
            Log.AppendLine($"  road check: {rays} rays, {holes} missed or off the road's height (worst {worst:F3}), {blocked} places with something in the lane, {clipped} things standing in the road's body or walls; pillars {stood}");
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        // ---------- laying ----------

        /// <summary>Straight pieces for <paramref name="length"/>: whole pieces, stretched a little to fit. Each is the
        /// pack's straight with its road widened (one shared mesh), with a wall each side (not on the right of the first
        /// one when <paramref name="openFirst"/>).</summary>
        static void RwStraights(RwLayer layer, float length, bool openFirst)
        {
            var kit = layer.Kit; var shape = layer.Shape;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / kit.PieceLength)); float each = length / count, stretch = each / shape.Length;
            TrackBender.Path straight = (float t, out Vector3 position, out Quaternion r) => { position = new Vector3(0f, 0f, shape.Length * t); r = Quaternion.identity; };
            for (int i = 0; i < count; i++)
            {
                Quaternion turn = layer.Cursor.Turn;
                var road = RwMesh(layer, "Straight", () => TrackBender.Sweep(layer.Source, "straight", true, true, 2, straight, RwWiden));
                var go = new GameObject("S" + layer.Count++); go.transform.SetParent(layer.Parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = road;
                go.AddComponent<MeshRenderer>().sharedMaterial = kit.Material;
                go.AddComponent<MeshCollider>().sharedMesh = road;
                go.transform.localScale = new Vector3(XwScale, XwScale, stretch);
                go.transform.SetPositionAndRotation(layer.Cursor.Pos, turn);
                bool right = !(openFirst && i == 0);
                var walls = RwMesh(layer, right ? "Wall_Straight" : "Wall_Straight_LeftOnly", () => TrackBender.SweepWalls(shape, "walls", RwWall / XwScale, true, right, 2, straight, layer.RailUv));
                RwWalls(layer, go, walls, Vector3.zero);
                var piece = new RwPiece { Go = go, Start = layer.Cursor.Pos, StartTurn = turn };
                for (int k = 0; k < 5; k++) piece.Samples.Add((layer.Cursor.Pos + turn * new Vector3(0f, 0f, each * (0.1f + 0.2f * k)), turn));
                layer.Pieces.Add(piece);
                Vector3 from = layer.Cursor.Pos;
                RwLine(layer, Mathf.Max(1, Mathf.RoundToInt(each / RwPathRawStep)), t => (from + turn * new Vector3(0f, 0f, each * t), turn));
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

        /// <summary>
        /// One eighth of a loop a car can drive without steering. The road is a strip wound at a slant round a cylinder
        /// lying on its side: on such a strip "straight on" stays in the middle (unrolled, it is a straight line), where a
        /// loop that simply drifts sideways slides out from under a car going straight and rubs it along a wall all the way
        /// round (his first drive, D-0053). Each eighth is the same shape, so one mesh serves. After eight the road points
        /// the way it came in, <paramref name="shift"/> to the side and a little further on.
        /// </summary>
        static void RwLoopPiece(RwLayer layer, float radius, float shift)
        {
            if (Mathf.Abs(layer.Cursor.Roll) > 0.01f) Debug.LogError("Raceway: a loop needs a road that is not leaning; ease the lean out first.");
            float r = radius / XwScale;
            float slant = Mathf.Asin(Mathf.Clamp(Mathf.Abs(shift) / (2f * Mathf.PI * radius), 0f, 0.9f));
            Vector3 ahead = Vector3.forward, aside = Vector3.right * (shift < 0f ? -1f : 1f), up = Vector3.up;
            Vector3 round = Mathf.Cos(slant) * ahead - Mathf.Sin(slant) * aside;    // the way round the cylinder, at its foot
            Vector3 along = Mathf.Sin(slant) * ahead + Mathf.Cos(slant) * aside;    // the cylinder's axis
            TrackBender.Path path = (float t, out Vector3 position, out Quaternion turn) =>
            {
                float a = t * Mathf.PI * 0.25f;
                position = along * (r * a * Mathf.Tan(slant)) + round * (r * Mathf.Sin(a)) + up * (r * (1f - Mathf.Cos(a)));
                Vector3 tangent = Mathf.Cos(slant) * (Mathf.Cos(a) * round + Mathf.Sin(a) * up) + Mathf.Sin(slant) * along;
                Vector3 normal = -Mathf.Sin(a) * round + Mathf.Cos(a) * up;
                turn = Quaternion.LookRotation(tangent, normal);
            };
            RwSwept(layer, $"Loop_R{radius:F1}_s{shift:F2}", "L", path, true);
            path(1f, out _, out Quaternion end);
            layer.Cursor.Level *= end;
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
            var road = RwMesh(layer, key, () => TrackBender.Sweep(layer.Source, key, true, true, RwSlices, path, RwWiden));
            var walls = RwMesh(layer, key + "_Walls", () => TrackBender.SweepWalls(layer.Shape, key + "_Walls", RwWall / XwScale, true, true, RwSlices, path, layer.RailUv));
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
            Vector3 start = layer.Cursor.Pos;
            RwLine(layer, RwPathRawCuts, t => { path(t, out Vector3 p, out Quaternion r); return (start + turn * (p * XwScale), turn * r); });
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
            string file = (key + "_w" + RwWiden.ToString("F2")).Replace('.', '_').Replace('-', 'm'), path = RwMeshDir + "/" + file + ".asset";   // one asset per shape and width
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
            // the gap between the posts: the walls plus room for the posts' own thickness, which grows with the stretch
            // (D-0058: at the 2.25 road a fixed 1.5 put the stretched posts into the walls)
            float clear = layer.Half * 2f + 1.5f + 0.1f * layer.Half;
            // as wide as the road needs, but no taller than RwGateTall times its own size: a gate scaled evenly to span a wide
            // road stands tall enough to reach the loop and the spiral beside it (the clip check caught that, D-0056)
            float wide = clear / (tag == "Finish" ? RaceBuilder.FinishOpening : RaceBuilder.GateOpening), tall = Mathf.Min(wide, RwGateTall);
            if (tag == "Finish" || tag == "Gate") go.transform.localScale = new Vector3(wide, tall, tall);
            go.transform.SetPositionAndRotation(layer.Cursor.Pos + layer.Cursor.Turn * Vector3.up * 0.03f, layer.Cursor.Turn);
        }

        // ---------- dressing (D-0055: a fast pass; a permanent one comes later) ----------

        const string CyberTrackProps = "Assets/_Imports/ithappy/Modular_Cyber_Racing_Cars/Prefabs/Track Props/";
        const float RwTyreThick = 0.43f;   // one of the pack's tyres lying flat (measured)
        const string InteractablePieces = "Assets/_Game/Prefabs/Pieces/Interactables/";

        /// <summary>What a mark puts on the straight that follows it, measured from the cursor: across the road (right is
        /// positive) and along it. Loose things go under Smash (light: a car drives through them and is not hurt),
        /// pickups under Pickups, fixed things under Props.</summary>
        static void RwDress(RwLayer layer, Transform smash, Transform pickups, Transform props, string tag)
        {
            Vector3 at = layer.Cursor.Pos; Quaternion turn = layer.Cursor.Turn;
            Vector3 On(float across, float along, float up = 0f) => at + turn * new Vector3(across, up, along);
            switch (tag)
            {
                case "Stars":   // three nitro stars weaving across the lane (the piece stands on the road: its star floats above its own base)
                    foreach (var (across, along) in new[] { (-0.45f, 8f), (0f, 15f), (0.45f, 22f) }) RwPickup(pickups, "NitroStar", On(across * layer.RoadHalf, along, 0.02f), turn);
                    break;
                case "Heart":
                    RwPickup(pickups, "RepairHeart", On(0f, 9f, 0.02f), turn);
                    break;
                case "Cones":   // a slalom
                    for (int i = 0; i < 4; i++) RwLoose(smash, CyberTrackProps + "Cone_0" + (1 + i % 4) + ".prefab", "Cone", On((i % 2 == 0 ? -1f : 1f) * layer.RoadHalf * 0.5f, 4f + i * 5f), turn, 0f, 0.15f);
                    break;
                case "Tyres":   // two stacks of four tyres at each edge of the straight just laid, narrowing the road into the bend
                    foreach (float side in new[] { -1f, 1f })
                        for (int stack = 0; stack < 2; stack++)
                            for (int k = 0; k < 4; k++)
                                RwLoose(smash, CyberTrackProps + "Barrier_Wheel_0" + (1 + (stack * 4 + k) % 8) + ".prefab", "Tyre", On(side * (layer.RoadHalf - 1f - stack * 1.2f), -6f - stack * 1.4f), turn, k * RwTyreThick, 0.3f);
                    break;
                case "Arrows":   // three chevrons painted on the straight just laid, pointing on into the bend
                    for (int i = 0; i < 3; i++)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CyberTrackProps + "Arrow_0" + (3 + i % 2) + ".prefab"); if (asset == null) continue;
                        var arrow = (GameObject)PrefabUtility.InstantiatePrefab(asset, props); arrow.name = "Arrow";
                        arrow.transform.localScale = Vector3.one * 1.5f;
                        // the pack's chevron points along its own -Z (seen in a still), and it is a flat decal: laid just clear of the road
                        arrow.transform.SetPositionAndRotation(On(0f, -18f + i * 6f, 0.04f), turn * Quaternion.Euler(0f, 180f, 0f));
                    }
                    break;
                case "Stack":   // a wall of blocks right across the lane, three rows, each one block shorter
                {
                    string[] cubes = { "Color_Cube_01", "Color_Cube_03" };
                    const float size = 1.5f, gap = 0.08f;   // the pack's colour cube
                    int across = Mathf.Max(3, Mathf.FloorToInt((layer.RoadHalf * 2f - 0.8f) / (size + gap))), n = 0;
                    for (int row = 0; row < 3; row++)
                        for (int i = 0; i < across - row; i++)
                        {
                            var asset = RwPlayroomPrefab(cubes[n++ % cubes.Length]); if (asset == null) continue;
                            var cube = (GameObject)PrefabUtility.InstantiatePrefab(asset, smash); cube.name = "Block";
                            RwSet(cube, On((i - (across - 1 - row) * 0.5f) * (size + gap), 12f), turn, row * size);
                            Push(cube, Pushable.Weight.Light, 0.12f);
                            cube.AddComponent<LooseProp>();
                        }
                    break;
                }
                case "Flags":   // along the inner wall of the start straight (the steps arrive on the outer side)
                    for (int i = 0; i < 3; i++)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CyberTrackProps + "Flag_0" + (1 + i % 2) + ".prefab"); if (asset == null) continue;
                        var flag = (GameObject)PrefabUtility.InstantiatePrefab(asset, props); flag.name = "Flag";
                        RwSet(flag, On(-(layer.RoadHalf + (layer.Half - layer.RoadHalf) * 0.5f), 3f + i * 8f), turn, RwWall);
                    }
                    break;
                default:
                    Log.AppendLine("  UNKNOWN MARK " + tag);
                    break;
            }
        }

        static GameObject RwPlayroomPrefab(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Prefab", new[] { Pack }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            Log.AppendLine("  MISSING prefab " + name);
            return null;
        }

        /// <summary>Stands a thing on the road at <paramref name="on"/> (a point on the road's surface), its lowest point
        /// <paramref name="lift"/> above it, its middle over that point. For level road.</summary>
        static void RwSet(GameObject go, Vector3 on, Quaternion turn, float lift)
        {
            go.transform.SetPositionAndRotation(on, turn);
            var b = B(go);
            go.transform.position += new Vector3(on.x - b.center.x, on.y + lift + 0.02f - b.min.y, on.z - b.center.z);
        }

        /// <summary>A loose, light thing in the lane: a car drives through it (ArcadeCarSettings.smashMass).</summary>
        static void RwLoose(Transform parent, string assetPath, string name, Vector3 on, Quaternion turn, float lift, float mass)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null) { Log.AppendLine("  MISSING prefab " + assetPath); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent); go.name = name;
            RwSet(go, on, turn, lift);
            Push(go, Pushable.Weight.Light, mass);
            go.AddComponent<LooseProp>();   // F9 stands it back up
        }

        static void RwPickup(Transform parent, string piece, Vector3 at, Quaternion turn)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(InteractablePieces + piece + ".prefab");
            if (asset == null) { Log.AppendLine($"  NO PIECE {piece} (run Shift Jam > Workshop > Build Boost Pieces)"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent); go.name = piece;
            go.transform.SetPositionAndRotation(at, turn);
        }

        [MenuItem("Shift Jam/Raceway/Render Raceway Stills")]
        public static void RacewayStillsMenu()
        {
            if (SceneManager.GetActiveScene().path != RacewayScenePath) { Debug.LogError("Open Raceway.unity first."); return; }
            Log.Clear();
            RacewayStills();
            Debug.Log("RACEWAY STILLS\n" + Log);
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
                ("dress_start", RwStart + new Vector3(-4f, 5f, -1f), RwStart + new Vector3(40f, 1f, 0f)),
                ("dress_cones", new Vector3(46f, RwBase + 4f, -66f), new Vector3(66f, RwBase + 0.5f, -64f)),
                ("dress_stack", new Vector3(60f, RwBase + RwClimb + 4f, 42f), new Vector3(38f, RwBase + RwClimb + 1f, 40f)),
                ("dress_heart", new Vector3(-40f, RwBase + 5f, 43f), new Vector3(-64f, RwBase + 1f, 40f)),
                ("dress_arrows", new Vector3(-62f, RwBase + 14f, 52f), new Vector3(-72f, RwBase, 40f)),
            };
            foreach (var v in views) Shot(dir + "/" + v.name + ".png", v.eye, Quaternion.LookRotation(v.at - v.eye), 60f, 1280, 720);
            // the grid, for the project's own stills folder (docs/stills is not in git)
            string docs = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../docs/stills")); System.IO.Directory.CreateDirectory(docs);
            Vector3 gridEye = RwStart + new Vector3(-9f, 8f, -10f), gridAt = RwStart + new Vector3(20f, 0.5f, 0f);
            Shot(docs + "/race_grid.png", gridEye, Quaternion.LookRotation(gridAt - gridEye), 60f, 1280, 720);
            Log.AppendLine("stills in " + dir + " and " + docs + "/race_grid.png");
        }
    }
}
#endif
