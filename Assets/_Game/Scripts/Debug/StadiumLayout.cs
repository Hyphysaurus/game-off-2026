#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The Stadium hub's layout (D-0113), built from Battle Stadium's pieces by PartyBuilder. My layout, not the pack's
    /// demo scene (his "optimize based on the assets"):
    ///  - Backdrop at Back scale (0.32 since D-0181, 0.2 before): the six shell sections (modelled in place round the origin, so all six at the
    ///    middle make the ring), six stands with their crowd turned round it, the rim ring, two giant towers, balloons
    ///    and an airship. The pack's people come out about two toys tall: giants watching toys fight.
    ///  - The field: the pack's flat race-track disc as the floor, Field units across (the shell's inner edge), with a
    ///    plain collider under it and an invisible wall round it.
    ///  - Play pieces at Prop scale (0.4), so an inflatable wall is about one toy tall: cover lines, big blocks, arch
    ///    tunnels, flank platforms with trampolines (the game's LaunchPad) and a low stage in the middle. Laid for the
    ///    south half and turned half round for the north: the two teams' sides match.
    ///  - Spawns: the First point on the stage, eight more round the field for comebacks (NetSpawnPoint.Safest), each
    ///    belonging to the half it stands in for team matches, and a landing point at each base (D-0182).
    ///  - D-0181: the whole plan 1.6 times wider (the field 81 across, 51 before) with the play pieces kept toy-sized
    ///    and more cover added, after his "feeling a bit small and cramped".
    /// </summary>
    public static class StadiumLayout
    {
        private const string Root = "Assets/_Imports/ithappy/Battle_Stadium/Prefabs/";
        /// <summary>How many times wider the whole plan is than the first layout's (D-0181, his "feeling a bit small and
        /// cramped"; the toy is also faster than when the field was first laid out). The plan below is still written
        /// in the first layout's units and is spread by this as it is built. The play pieces keep their size (a wall
        /// stays about one toy tall), so it is the ground between them that opens up; pieces that belong together (a
        /// base's blocks with its arch, a slide with its platform, the stage's walls) keep their distances (By).</summary>
        private const float Grow = 1.6f;
        private const float FirstBack = 0.2f;   // the backdrop's scale in the first layout; a play piece sized by it keeps that size
        private const float Back = FirstBack * Grow, Prop = 0.4f;
        /// <summary>The field's radius: the shell's inner edge at Back scale (127 pack units). 40.6 since D-0181; 25.4 before.</summary>
        public const float FieldRadius = 127f * Back;

        /// <summary>A place of the plan in the built field: spread from the middle by Grow, unless the thing belongs
        /// with another, in which case that other's place is spread and the distance between the two is kept.</summary>
        private static Vector2 Spread(Vector2 at, bool held, Vector2 with) => held ? with * Grow + (at - with) : at * Grow;

        /// <summary>One play piece for the south half: prefab, place (x, z) in the plan's units, turn, scale (Prop if 0),
        /// and whether it is mirrored to the north (pieces on the middle line are placed once).</summary>
        private readonly struct Piece
        {
            public readonly string Path; public readonly Vector2 At, With; public readonly float Turn, Scale; public readonly bool Mirror, Held, Built;
            public Piece(string path, float x, float z, float turn = 0f, float scale = 0f, bool mirror = true)
                : this(path, new Vector2(x, z), default, false, false, turn, scale, mirror) { }
            private Piece(string path, Vector2 at, Vector2 with, bool held, bool built, float turn, float scale, bool mirror)
            { Path = path; At = at; With = with; Held = held; Built = built; Turn = turn; Scale = scale; Mirror = mirror; }
            /// <summary>It belongs with the thing at (x, z) of the plan and keeps its distance from it.</summary>
            public Piece By(float x, float z) => new Piece(Path, At, new Vector2(x, z), true, false, Turn, Scale, Mirror);
            /// <summary>Its place is given in the built field's own units already (the pieces added for the wider field).</summary>
            public Piece AsBuilt() => new Piece(Path, At, default, false, true, Turn, Scale, Mirror);
            /// <summary>Where it stands in the built field.</summary>
            public Vector2 Place => Built ? At : Spread(At, Held, With);
        }

        private static readonly Piece[] South =
        {
            // the base: an arch to come out of, big blocks either side, a long block behind
            new Piece("Inflatable/arch_004", 0f, -17f, 0f),
            new Piece("Stadium/box_012", -6f, -18.5f, 0f).By(0f, -17f),
            new Piece("Stadium/box_012", 6f, -18.5f, 0f).By(0f, -17f),
            new Piece("Stadium/box_011", 0f, -22.5f, 0f, FirstBack * 1.6f).By(0f, -17f),
            // the lanes: cover lines, low boxes, tunnels across the middle
            new Piece("Inflatable/wall_003", -8f, -10f, 0f),
            new Piece("Inflatable/wall_003", 8f, -10f, 0f),
            new Piece("Inflatable/wall_009", -13f, -4f, 90f),
            new Piece("Inflatable/wall_010", 13f, -4f, 90f),
            new Piece("Inflatable/color_box_001", -4.5f, -7f, 15f),
            new Piece("Inflatable/color_box_004", 4.5f, -7f, -15f),
            new Piece("Inflatable/color_box_006", -2.5f, -12.5f, 90f),
            new Piece("Inflatable/color_box_002", 2.5f, -12.5f, 90f),
            new Piece("Inflatable/arch_005", -9f, 0f, 90f, 0f, false),
            new Piece("Inflatable/arch_006", 9f, 0f, 90f, 0f, false),
            // the flanks: high platforms with a trampoline up, a slide down
            new Piece("Inflatable/platform_001", -17f, -9f, 90f),
            new Piece("Inflatable/platform_002", 17f, -9f, 90f),
            new Piece("Inflatable/slide_001", -17f, -15.5f, 0f).By(-17f, -9f),
            new Piece("Inflatable/slide_001", 17f, -15.5f, 0f).By(17f, -9f),
            // the middle: a low stage with four short walls round it
            new Piece("Inflatable/octagon_002", 0f, 0f, 0f, 0.15f, false),
            new Piece("Inflatable/wall_004", 0f, -6f, 0f).By(0f, 0f),
            new Piece("Inflatable/wall_006", -6f, 0f, 90f, 0f, false).By(0f, 0f),
            new Piece("Inflatable/wall_007", 6f, 0f, 90f, 0f, false).By(0f, 0f),
            // dressing
            new Piece("Inflatable/flag_002", -3f, -23.5f, 0f, Back).By(0f, -17f),
            new Piece("Inflatable/flag_002", 3f, -23.5f, 0f, Back).By(0f, -17f),
            new Piece("Inflatable/tower_palm_001", -10f, -20f, 0f, 0.6f).By(0f, -17f),
            new Piece("Inflatable/tower_palm_001", 10f, -20f, 0f, 0.6f).By(0f, -17f),

            // ---- more cover for the wider field (D-0181), in the BUILT field's own units. Shots land at once in this
            // scene, so open ground is dangerous ground: these break the long lines the spread layout would leave. Placed
            // from the pieces' measured footprints (a wall is 4.8 long, 0.55 thick, 1.8 tall; a colour box 1.6 by 2.4,
            // 0.8 tall), each at least a toy's width clear of its neighbours, the pickups and the two gates.
            // the middle lane, between the stage and the base
            new Piece("Inflatable/wall_011", 0f, -13.5f, 0f).AsBuilt(),
            // across the two diagonals through the middle: the stage and its rails are too low to hide a toy
            new Piece("Inflatable/wall_012", -9f, -6.5f, 50f).AsBuilt(),
            new Piece("Inflatable/wall_013", 9f, -6.5f, -50f).AsBuilt(),
            // north of each ammo box: something to stand behind while restocking
            new Piece("Inflatable/wall_014", -19f, -17.8f, 30f).AsBuilt(),
            new Piece("Inflatable/wall_015", 19f, -17.8f, -30f).AsBuilt(),
            // a stall round each charge pickup on the outer flank (its twin comes with the other half). It also cuts
            // the long line along the rim between the two halves' flank spawns.
            new Piece("Inflatable/wall_009", -33.5f, -3f, 0f).AsBuilt(),
            new Piece("Inflatable/wall_010", 33.5f, -3f, 0f).AsBuilt(),
            // low boxes: a first hop of cover out of the base's side, and in the corners of the outer ring
            new Piece("Inflatable/color_box_003", -17.5f, -27f, 20f).AsBuilt(),
            new Piece("Inflatable/color_box_005", 17.5f, -27f, -20f).AsBuilt(),
            new Piece("Inflatable/color_box_007", -24f, -28f, 45f).AsBuilt(),
            new Piece("Inflatable/color_box_008", 24f, -28f, -45f).AsBuilt(),
        };

        /// <summary>The rook towers: the place the plan gives each, and the flank platform it stands against.</summary>
        private static readonly (Vector2 At, Vector2 With)[] Towers =
        {
            (new Vector2(-21f, 9f), new Vector2(-17f, 9f)),
            (new Vector2(21f, -9f), new Vector2(17f, -9f)),
        };

        /// <summary>Trampolines (with the game's LaunchPad): at the foot of each flank platform (the place the plan
        /// gives them, and the platform's), and mirrored.</summary>
        private static readonly (Vector2 At, Vector2 With)[] Trampolines =
        {
            (new Vector2(-17f, -4f), new Vector2(-17f, -9f)),
            (new Vector2(17f, -4f), new Vector2(17f, -9f)),
        };
        private const float TrampolineHeight = 7f;   // the player's own units (a toy: 1 = half its height)

        public static void Build(Scene scene, float body)
        {
            var root = new GameObject("Stadium").transform;
            var backdrop = new GameObject("Backdrop").transform; backdrop.SetParent(root);
            var field = new GameObject("Field").transform; field.SetParent(root);
            var play = new GameObject("Play").transform; play.SetParent(root);

            // ---------------------------------------------------------------- backdrop
            for (int i = 1; i <= 6; i++) Place("Stadium/stadium_00" + i, backdrop, Vector3.zero, 0f, Back, true);
            for (int i = 0; i < 6; i++)
            {
                Place("Stadium/stand_001", backdrop, Vector3.zero, i * 60f, Back, true);
                Place("Stadium/stand_viewers_00" + (i % 2 + 1), backdrop, Vector3.zero, i * 60f, Back, true);
            }
            Place("Stadium/battle_stadium_003", backdrop, Vector3.zero, 0f, Back, true);
            // The two rook towers stand against a flank platform each: part of that cluster, so they keep their first
            // size and their distance from it (grown with the shell they swallowed half the platform's side).
            foreach (var tower in Towers)
            {
                Vector2 at = Spread(tower.At, true, tower.With);
                Place("Inflatable/tower_016", backdrop, new Vector3(at.x, 0f, at.y), 0f, 0.25f, true);
            }
            // what floats over the field grows with the shell
            Place("Other/air_balloon_001", backdrop, new Vector3(-38f, 34f, 18f) * Grow, 30f, 0.35f * Grow, false);
            Place("Other/air_balloon_001", backdrop, new Vector3(42f, 40f, -12f) * Grow, -40f, 0.3f * Grow, false);
            Place("Other/air_balloon_001", backdrop, new Vector3(10f, 46f, 44f) * Grow, 0f, 0.3f * Grow, false);
            Place("Other/airship_001", backdrop, new Vector3(-6f, 38f, -40f) * Grow, 70f, 0.45f * Grow, false);

            // ---------------------------------------------------------------- the field
            var track = Place("Stadium/race_track_circle_002", field, Vector3.zero, 0f, 1f, true);
            if (track != null)
            {
                Bounds b = BoundsOf(track);
                float across = Mathf.Max(b.size.x, b.size.z);
                track.transform.localScale *= (FieldRadius * 2f + 1f) / across;
                b = BoundsOf(track);
                track.transform.position -= new Vector3(b.center.x, b.max.y, b.center.z);   // its top at 0
            }
            // The track disc is a ring (open in the middle, found 21 Nov in his play): a field of our own under it, warm
            // sand under the bright inflatables, seen and stood on everywhere (D-0115).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "FieldFloor";
            ground.transform.SetParent(field);
            Object.DestroyImmediate(ground.GetComponent<Collider>());   // a cylinder's own collider is a capsule
            ground.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            ground.transform.localScale = new Vector3(FieldRadius * 2f + 1f, 0.25f, FieldRadius * 2f + 1f);   // the mesh is 2 tall
            ground.transform.position = new Vector3(0f, -0.26f, 0f);   // its top just under the track's
            ground.GetComponent<MeshRenderer>().sharedMaterial = FieldMaterial();
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);
            var under = new GameObject("FieldCollider", typeof(BoxCollider));   // a floor whatever the disc does
            under.transform.SetParent(field);
            var box = under.GetComponent<BoxCollider>();
            box.size = new Vector3(FieldRadius * 2.2f, 1f, FieldRadius * 2.2f); box.center = new Vector3(0f, -0.5f, 0f);
            Wall(field, FieldRadius + 0.3f, 40f, 32);

            // ---------------------------------------------------------------- play pieces, south then turned for the north
            foreach (var p in South)
            {
                float scale = p.Scale > 0f ? p.Scale : Prop;
                Vector2 at = p.Place;
                Place(p.Path, play, new Vector3(at.x, 0f, at.y), p.Turn, scale, true, grounded: true);
                if (p.Mirror) Place(p.Path, play, new Vector3(-at.x, 0f, -at.y), p.Turn + 180f, scale, true, grounded: true);
            }
            foreach (var spot in Trampolines)
            {
                Vector2 t = Spread(spot.At, true, spot.With);
                Trampoline(play, new Vector3(t.x, 0f, t.y), body);
                Trampoline(play, new Vector3(-t.x, 0f, -t.y), body);
            }

            // ---------------------------------------------------------------- pickups, spread and mirrored (D-0115)
            var pickups = new GameObject("Pickups").transform; pickups.SetParent(root);
            foreach (var w in Stands)
            {
                Vector2 at = w.Place;
                Stand(pickups, w.Prefab, new Vector3(at.x, 0f, at.y), w.Turn, w.Up);
                if (w.Mirror) Stand(pickups, w.Prefab, new Vector3(-at.x, 0f, -at.y), w.Turn + 180f, w.Up);
            }
            foreach (var g in Gifts)
            {
                Vector2 at = g.Place;
                Gift(pickups, g, new Vector3(at.x, 0f, at.y), body);
                if (g.Mirror) Gift(pickups, g, new Vector3(-at.x, 0f, -at.y), body);
            }
            CheckFloor(body);

            // ---------------------------------------------------------------- practice: toy soldiers to fight alone (D-0114)
            // Online the host runs them for everyone (CoopWorld). In a team match they are the bots (D-0182, TeamMatch):
            // each on the team of its half, walking its match route toward the middle while it plays.
            var soldierAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Pieces/Interactables/ToySoldier.prefab");
            if (soldierAsset != null)
            {
                var practice = new GameObject("Practice").transform; practice.SetParent(root);
                // Out at the far edges, two by each base, so nobody spawns in their sight (his "a bunch of army soldiers
                // instantly shooting at me", 21 Nov): you go to them. Shorter sight, slower to fire, softer darts.
                // The south half's two first, then the same two turned half round for the north (the soldiers' order
                // here is the order CoopWorld numbers them by: keep it).
                var made = new List<(ToySoldier soldier, Vector2[] route)>();
                foreach (bool north in new[] { false, true })
                    foreach (var guard in Guards)
                    {
                        Vector2 by = Spread(guard.Plan, true, new Vector2(0f, Mathf.Sign(guard.Plan.y) * 17f));   // beside their base, as the plan had them
                        if (north) by = -by;
                        var at = new Vector3(by.x, 0f, by.y);
                        var soldier = (GameObject)PrefabUtility.InstantiatePrefab(soldierAsset, practice);
                        soldier.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-at.normalized, Vector3.up));
                        var so = new SerializedObject(soldier.GetComponent<ToySoldier>());
                        so.FindProperty("sightRange").floatValue = 12f;
                        so.FindProperty("fireRange").floatValue = 10f;
                        so.FindProperty("reactionTime").floatValue = 1.2f;
                        so.FindProperty("damage").floatValue = 6f;
                        so.FindProperty("wanderRadius").floatValue = 2.5f;   // they stay at their corner
                        so.FindProperty("matchOrder").intValue = guard.Order;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        // The route starts at the post; the north's is the south's turned half round, as every piece is.
                        var route = new Vector2[guard.Route.Length + 1];
                        route[0] = by;
                        for (int i = 0; i < guard.Route.Length; i++) route[i + 1] = north ? -guard.Route[i] : guard.Route[i];
                        made.Add((soldier.GetComponent<ToySoldier>(), route));
                    }
                // The routes' points: plain markers in the Practice group after the soldiers (under a soldier they would
                // walk with it), one group a soldier.
                foreach (var (soldier, route) in made)
                {
                    var holder = new GameObject("MatchRoute_" + soldier.transform.GetSiblingIndex()).transform;
                    holder.SetParent(practice);
                    var so = new SerializedObject(soldier);
                    SerializedProperty points = so.FindProperty("matchRoute");
                    points.arraySize = route.Length;
                    for (int i = 0; i < route.Length; i++)
                    {
                        var point = new GameObject("Point" + i).transform;
                        point.SetParent(holder);
                        point.position = new Vector3(route[i].x, 0f, route[i].y);
                        points.GetArrayElementAtIndex(i).objectReferenceValue = point;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // ---------------------------------------------------------------- spawns
            var spawns = new GameObject("Spawns").transform; spawns.SetParent(root);
            var first = Spawn(spawns, new Vector3(0f, 0f, 0f), 0f);
            first.MarkFirst(true);
            Raise(first.transform);   // onto the stage
            // Eight more round the field, four a half, each on open ground: a spot inside or against a piece slides out
            // toward the rim, then in toward the middle, to the nearest free one, and faces the middle or the nearest
            // open way beside it. (Until D-0181 these were lifted onto whatever stood there, as the stage's is, and two
            // of them sat on top of the rook towers.)
            float r = FieldRadius - 4f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 22.5f;
                Vector3 way = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Vector3 pos = way * r; bool free = false;
                foreach (float slide in SpawnSlides)
                    if (OpenGround(way * (r + slide))) { pos = way * (r + slide); free = true; break; }
                if (!free) Debug.LogWarning($"StadiumLayout: no open ground for the spawn at {a:0.#} degrees; it stands at ({pos.x:0.#}, {pos.z:0.#}) all the same.");
                // In a team match (D-0182) a ring spawn is a comeback point of the half it stands in: Blue the south, Orange the north.
                Spawn(spawns, pos, Facing(pos, a + 180f)).SetTeam(pos.z < 0f ? TeamMatch.Blue : TeamMatch.Orange, false);
            }
            // Each team's landing (D-0182): where the whole team stands at GO, just outside its base's arch mouth, facing
            // up the field. Laid for the south (Blue) and turned half round for the north (Orange), as the play pieces are.
            Vector2 land = Spread(TeamLanding, true, TeamLandingBy);
            Spawn(spawns, new Vector3(land.x, 0f, land.y), 0f).SetTeam(TeamMatch.Blue, true);
            Spawn(spawns, new Vector3(-land.x, 0f, -land.y), 180f).SetTeam(TeamMatch.Orange, true);
        }

        /// <summary>A practice soldier of the south half (its north twin is it turned half round): its post in the plan's
        /// units (held by its base's arch, as the plan had it), which of its team plays first in a team match (0 first),
        /// and its match route from the post toward the middle, in the BUILT field's units (D-0182). Every leg was
        /// checked against the rebuilt scene's pieces (the smallest gap 0.98; a soldier is about 0.3 in radius).</summary>
        private readonly struct Guard
        {
            public readonly Vector2 Plan; public readonly int Order; public readonly Vector2[] Route;
            public Guard(Vector2 plan, int order, Vector2[] route) { Plan = plan; Order = order; Route = route; }
        }

        private static readonly Guard[] Guards =
        {
            // the west soldier: first to play, up the centre lane past the stage's west side
            new Guard(new Vector2(-13.5f, -18.5f), 0, new[]
            {
                new Vector2(-9.5f, -21.5f), new Vector2(-4.6f, -16.5f), new Vector2(-4.3f, -8f), new Vector2(-8.3f, -2.5f),
                new Vector2(-8.3f, 3f), new Vector2(-4.3f, 8f), new Vector2(-4.3f, 16.5f),
            }),
            // the east soldier: second, along the east flank
            new Guard(new Vector2(13.5f, -18.5f), 1, new[]
            {
                new Vector2(15f, -24f), new Vector2(22.2f, -20.5f), new Vector2(22.2f, -10.5f), new Vector2(24f, -6f),
                new Vector2(24f, 6f), new Vector2(22.2f, 10.5f), new Vector2(22.2f, 20.5f),
            }),
        };

        /// <summary>The south team's landing in the plan's units, and the base's arch it belongs with (it keeps its
        /// distance from the arch as the field is spread): built at (0, -21.6), 5.6 in front of the arch, on ground left
        /// open 2.5 each way for the team to stand round it (NetPlayer spreads players 1.2 body heights round it).</summary>
        private static readonly Vector2 TeamLanding = new Vector2(0f, -11.4f), TeamLandingBy = new Vector2(0f, -17f);

        /// <summary>How far a ring spawn may slide along its own line from the middle to find open ground: out toward
        /// the rim first (further from the fight), then in.</summary>
        private static readonly float[] SpawnSlides = { 0f, 1f, 2f, 3f, -1f, -2f, -3f };
        private const float SpawnRoom = 1.4f;   // open ground a spawn needs round it: a toy and a step to each side
        private const float SpawnStep = 0.3f;   // ground no higher than this is the floor, not a piece

        /// <summary>The ways a ring spawn may face, as turns from "toward the middle", in the order they are tried.</summary>
        private static readonly float[] SpawnTurns = { 0f, -30f, 30f, -60f, 60f, -90f, 90f };
        private const float SpawnView = 6f;     // open ground a spawn wants ahead of it
        private const float SpawnEye = 1.2f;    // the height that is looked along: over a step, not over a wall

        /// <summary>The way a ring spawn faces: toward the middle, unless something stands in the way within SpawnView;
        /// then the nearest open way to either side, so nobody comes back with their nose against a wall.</summary>
        private static float Facing(Vector3 at, float toMiddle)
        {
            Physics.SyncTransforms();
            foreach (float turn in SpawnTurns)
            {
                Vector3 way = Quaternion.Euler(0f, toMiddle + turn, 0f) * Vector3.forward;
                if (!Physics.SphereCast(at + Vector3.up * SpawnEye, 0.3f, way, out _, SpawnView, ~0, QueryTriggerInteraction.Ignore))
                    return toMiddle + turn;
            }
            return toMiddle;
        }

        /// <summary>True when a toy can come back here: the floor under it, and nothing solid within SpawnRoom.</summary>
        private static bool OpenGround(Vector3 at)
        {
            Physics.SyncTransforms();
            if (!Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (hit.point.y > SpawnStep) return false;
            // a capsule that starts just above the floor, so the floor itself does not count
            return !Physics.CheckCapsule(at + Vector3.up * (SpawnRoom + SpawnStep), at + Vector3.up * 3f, SpawnRoom, ~0, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- pickups (D-0115)

        private readonly struct StandSpot
        {
            public readonly string Prefab; public readonly Vector2 At, With; public readonly float Turn; public readonly bool Mirror, Up, Held;
            /// <param name="up">On top of whatever stands there (a platform); else on the floor, under any roof (D-0120).</param>
            public StandSpot(string prefab, float x, float z, float turn = 0f, bool mirror = true, bool up = false) : this(prefab, new Vector2(x, z), default, false, turn, mirror, up) { }
            private StandSpot(string prefab, Vector2 at, Vector2 with, bool held, float turn, bool mirror, bool up) { Prefab = prefab; At = at; With = with; Held = held; Turn = turn; Mirror = mirror; Up = up; }
            /// <summary>It belongs with the thing at (x, z) of the plan (a base's arch, the stage) and keeps its distance from it.</summary>
            public StandSpot By(float x, float z) => new StandSpot(Prefab, At, new Vector2(x, z), true, Turn, Mirror, Up);
            public Vector2 Place => Spread(At, Held, With);
        }

        private readonly struct GiftSpot
        {
            public readonly PlayerPickup.Kind Kind; public readonly string Model; public readonly float Amount, Respawn; public readonly Vector2 At, With; public readonly bool Mirror, Held;
            public GiftSpot(PlayerPickup.Kind kind, string model, float amount, float respawn, float x, float z, bool mirror = true)
                : this(kind, model, amount, respawn, new Vector2(x, z), default, false, mirror) { }
            private GiftSpot(PlayerPickup.Kind kind, string model, float amount, float respawn, Vector2 at, Vector2 with, bool held, bool mirror)
            { Kind = kind; Model = model; Amount = amount; Respawn = respawn; At = at; With = with; Held = held; Mirror = mirror; }
            public GiftSpot By(float x, float z) => new GiftSpot(Kind, Model, Amount, Respawn, At, new Vector2(x, z), true, Mirror);
            public Vector2 Place => Spread(At, Held, With);
        }

        private const string Weapons = "Assets/_Game/Prefabs/Pieces/Weapons/";

        /// <summary>Weapon stands and ammo, south half (turned half round for the north). Pistols at the bases, the SMG
        /// up on the flank platforms (trampoline up), shotguns in the cross tunnels, melee near the middle, ammo boxes in
        /// the lanes, a supply crate at each base.</summary>
        private static readonly StandSpot[] Stands =
        {
            new StandSpot("DartPistolPickup", 0f, -15f).By(0f, -17f),    // inside the base arch: you run through it
            new StandSpot("DartSMGPickup", -17f, -9f, 0f, true, true),   // up on the flank platform (trampoline)
            new StandSpot("DartShotgunPickup", -9f, 0f, 90f, false),
            new StandSpot("DartShotgunPickup", 9f, 0f, 90f, false),
            new StandSpot("ToyKnifePickup", 0f, -9f).By(0f, 0f),         // by the stage
            new StandSpot("FoamFingerPickup", 3.5f, 0f).By(0f, 0f),
            new StandSpot("AmmoBox", -12f, -13f),
            new StandSpot("AmmoBox", 12f, -13f),
            new StandSpot("SupplyCrate", 0f, -19.5f).By(0f, -17f),       // at the back of the base arch
        };

        /// <summary>Heals, shields, grenades and charge, south half (turned half round for the north).</summary>
        private static readonly GiftSpot[] Gifts =
        {
            new GiftSpot(PlayerPickup.Kind.Heal, "Props/heart_001", 50f, 20f, -14f, -1.5f),
            new GiftSpot(PlayerPickup.Kind.Heal, "Props/heart_001", 50f, 20f, 7f, -14f),
            new GiftSpot(PlayerPickup.Kind.Shield, "Props/ring_001", 0f, 25f, -3f, 3f).By(0f, 0f),   // by the stage
            new GiftSpot(PlayerPickup.Kind.Grenades, "Props/bomb_001", 2f, 20f, 17f, -9f),
            new GiftSpot(PlayerPickup.Kind.Charge, "Props/star_001", 0f, 30f, -21.5f, 0f),
        };

        private static void Stand(Transform parent, string prefab, Vector3 at, float turn, bool up)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Weapons + prefab + ".prefab");
            if (asset == null) { Debug.LogWarning("StadiumLayout: no " + prefab); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            // On the floor unless it is meant to be up on something: a stand dropped from the sky landed on the arches'
            // roofs, out of reach (his "I can't reach any of the guns", 21 Nov).
            go.transform.SetPositionAndRotation(up ? Surface(at) : Floor(at), Quaternion.Euler(0f, turn, 0f));
        }

        /// <summary>The lowest thing to stand on at (x, z): cast from just above the floor, so roofs are not found.</summary>
        private static Vector3 Floor(Vector3 at)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(at.x, 0.6f, at.z), Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : new Vector3(at.x, 0f, at.z);
        }

        private static void Gift(Transform parent, GiftSpot g, Vector3 at, float body)
        {
            var go = new GameObject(g.Kind + "Pickup", typeof(SphereCollider));
            go.transform.SetParent(parent);
            Vector3 ground = g.Kind == PlayerPickup.Kind.Grenades ? Surface(at) : Floor(at);   // the bomb is up on the flank platform
            go.transform.position = ground + Vector3.up * (body * 0.55f);
            var col = go.GetComponent<SphereCollider>(); col.isTrigger = true; col.radius = body * 0.5f;
            var model = Place(g.Model, go.transform, go.transform.position, 0f, 0.45f, false);
            var pickup = go.AddComponent<PlayerPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("kind").enumValueIndex = (int)g.Kind;
            so.FindProperty("amount").floatValue = g.Amount;
            so.FindProperty("respawnSeconds").floatValue = g.Respawn;
            if (model != null) so.FindProperty("model").objectReferenceValue = model;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private const string FieldMaterialPath = "Assets/_Game/Data/Stadium/Stadium_Field.mat";

        /// <summary>The field's floor: URP Lit in warm sand, made once and kept (tune it in the Inspector).</summary>
        private static Material FieldMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FieldMaterialPath);
            if (mat != null) return mat;
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Stadium")) AssetDatabase.CreateFolder("Assets/_Game/Data", "Stadium");
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Stadium_Field" };
            mat.SetColor("_BaseColor", new Color(0.88f, 0.78f, 0.6f));
            mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, FieldMaterialPath);
            return mat;
        }

        /// <summary>The top of whatever stands at (x, z): the floor, a platform, the stage.</summary>
        private static Vector3 Surface(Vector3 at)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(at.x, 30f, at.z), Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : at;
        }

        /// <summary>Every spot of the field has ground under it, a little below 0 at most (a hole or a gap is logged).</summary>
        private static void CheckFloor(float body)
        {
            Physics.SyncTransforms();
            int holes = 0, tested = 0;
            for (float x = -FieldRadius + 0.5f; x <= FieldRadius - 0.5f; x += 1f)
                for (float z = -FieldRadius + 0.5f; z <= FieldRadius - 0.5f; z += 1f)
                {
                    if (x * x + z * z > (FieldRadius - 0.5f) * (FieldRadius - 0.5f)) continue;
                    tested++;
                    if (!Physics.Raycast(new Vector3(x, 30f, z), Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore) || hit.point.y < -0.05f)
                    {
                        holes++;
                        if (holes <= 5) Debug.LogWarning($"StadiumLayout: no floor at ({x:0.#}, {z:0.#})");
                    }
                }
            Debug.Log($"StadiumLayout: floor checked at {tested} spots, {holes} without ground.");
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject Place(string path, Transform parent, Vector3 at, float turn, float scale, bool isStatic, bool grounded = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + path + ".prefab");
            if (asset == null) { Debug.LogWarning("StadiumLayout: no " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localScale = asset.transform.localScale * scale;
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, turn, 0f));
            if (grounded)
            {
                Bounds b = BoundsOf(go);   // stand it on the floor whatever its pivot
                go.transform.position += Vector3.up * (at.y - b.min.y);
            }
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        private static void Trampoline(Transform parent, Vector3 at, float body)
        {
            var go = Place("Obstacles/trampoline_3_001", parent, at, 0f, Prop, false, grounded: true);
            if (go == null) return;
            Bounds b = BoundsOf(go);
            var pad = new GameObject("Pad", typeof(BoxCollider));
            pad.transform.SetParent(go.transform, true);
            pad.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
            var col = pad.GetComponent<BoxCollider>();
            col.isTrigger = true;
            Vector3 lossy = pad.transform.lossyScale;
            col.size = new Vector3(b.size.x * 0.7f / lossy.x, body * 0.3f / lossy.y, b.size.z * 0.7f / lossy.z);
            var launch = pad.AddComponent<LaunchPad>();
            var so = new SerializedObject(launch);
            so.FindProperty("anySize").boolValue = true;
            so.FindProperty("height").floatValue = TrampolineHeight;
            so.FindProperty("visual").objectReferenceValue = go.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static NetSpawnPoint Spawn(Transform parent, Vector3 at, float turn)
        {
            var go = new GameObject("Spawn", typeof(NetSpawnPoint));
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, turn, 0f));
            return go.GetComponent<NetSpawnPoint>();
        }

        /// <summary>Lifts a spawn onto whatever is under it (the stage), so nobody spawns inside a piece.</summary>
        private static void Raise(Transform t)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(t.position + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                t.position = hit.point;
        }

        /// <summary>A ring of tall invisible boxes round the field: nobody leaves it, nothing is seen.</summary>
        private static void Wall(Transform parent, float radius, float height, int sides)
        {
            var ring = new GameObject("Boundary").transform; ring.SetParent(parent);
            float side = 2f * Mathf.PI * radius / sides * 1.1f;
            for (int i = 0; i < sides; i++)
            {
                float a = i * 360f / sides;
                var go = new GameObject("Wall" + i, typeof(BoxCollider));
                go.transform.SetParent(ring);
                go.transform.SetPositionAndRotation(Quaternion.Euler(0f, a, 0f) * new Vector3(0f, height * 0.5f, radius + 0.5f), Quaternion.Euler(0f, a, 0f));
                go.GetComponent<BoxCollider>().size = new Vector3(side, height, 1f);
            }
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
#endif
