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
    ///  - Backdrop at Back scale (0.2): the six shell sections (modelled in place round the origin, so all six at the
    ///    middle make the ring), six stands with their crowd turned round it, the rim ring, two giant towers, balloons
    ///    and an airship. The pack's people come out about two toys tall: giants watching toys fight.
    ///  - The field: the pack's flat race-track disc as the floor, Field units across (the shell's inner edge), with a
    ///    plain collider under it and an invisible wall round it.
    ///  - Play pieces at Prop scale (0.4), so an inflatable wall is about one toy tall: cover lines, big blocks, arch
    ///    tunnels, flank platforms with trampolines (the game's LaunchPad) and a low stage in the middle. Laid for the
    ///    south half and turned half round for the north: the two teams' sides match.
    ///  - Spawns: the First point on the stage, eight more round the field for comebacks (NetSpawnPoint.Safest).
    /// </summary>
    public static class StadiumLayout
    {
        private const string Root = "Assets/_Imports/ithappy/Battle_Stadium/Prefabs/";
        private const float Back = 0.2f, Prop = 0.4f;
        private const float FieldRadius = 25.4f;   // the shell's inner edge at Back scale (127 pack units)

        /// <summary>One play piece for the south half: prefab, place (x, z) in units, turn, scale (Prop if 0), and
        /// whether it is mirrored to the north (pieces on the middle line are placed once).</summary>
        private readonly struct Piece
        {
            public readonly string Path; public readonly Vector2 At; public readonly float Turn, Scale; public readonly bool Mirror;
            public Piece(string path, float x, float z, float turn = 0f, float scale = 0f, bool mirror = true)
            { Path = path; At = new Vector2(x, z); Turn = turn; Scale = scale; Mirror = mirror; }
        }

        private static readonly Piece[] South =
        {
            // the base: an arch to come out of, big blocks either side, a long block behind
            new Piece("Inflatable/arch_004", 0f, -17f, 0f),
            new Piece("Stadium/box_012", -6f, -18.5f, 0f),
            new Piece("Stadium/box_012", 6f, -18.5f, 0f),
            new Piece("Stadium/box_011", 0f, -22.5f, 0f, Back * 1.6f),
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
            new Piece("Inflatable/slide_001", -17f, -15.5f, 0f),
            new Piece("Inflatable/slide_001", 17f, -15.5f, 0f),
            // the middle: a low stage with four short walls round it
            new Piece("Inflatable/octagon_002", 0f, 0f, 0f, 0.15f, false),
            new Piece("Inflatable/wall_004", 0f, -6f, 0f),
            new Piece("Inflatable/wall_006", -6f, 0f, 90f, 0f, false),
            new Piece("Inflatable/wall_007", 6f, 0f, 90f, 0f, false),
            // dressing
            new Piece("Inflatable/flag_002", -3f, -23.5f, 0f, Back),
            new Piece("Inflatable/flag_002", 3f, -23.5f, 0f, Back),
            new Piece("Inflatable/tower_palm_001", -10f, -20f, 0f, 0.6f),
            new Piece("Inflatable/tower_palm_001", 10f, -20f, 0f, 0.6f),
        };

        /// <summary>Trampolines (with the game's LaunchPad): at the foot of each flank platform, and mirrored.</summary>
        private static readonly Vector2[] Trampolines = { new Vector2(-17f, -4f), new Vector2(17f, -4f) };
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
            Place("Inflatable/tower_016", backdrop, new Vector3(-21f, 0f, 9f), 0f, 0.25f, true);
            Place("Inflatable/tower_016", backdrop, new Vector3(21f, 0f, -9f), 0f, 0.25f, true);
            Place("Other/air_balloon_001", backdrop, new Vector3(-38f, 34f, 18f), 30f, 0.35f, false);
            Place("Other/air_balloon_001", backdrop, new Vector3(42f, 40f, -12f), -40f, 0.3f, false);
            Place("Other/air_balloon_001", backdrop, new Vector3(10f, 46f, 44f), 0f, 0.3f, false);
            Place("Other/airship_001", backdrop, new Vector3(-6f, 38f, -40f), 70f, 0.45f, false);

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
            var under = new GameObject("FieldCollider", typeof(BoxCollider));   // a floor whatever the disc does
            under.transform.SetParent(field);
            var box = under.GetComponent<BoxCollider>();
            box.size = new Vector3(FieldRadius * 2.2f, 1f, FieldRadius * 2.2f); box.center = new Vector3(0f, -0.5f, 0f);
            Wall(field, FieldRadius + 0.3f, 40f, 32);

            // ---------------------------------------------------------------- play pieces, south then turned for the north
            foreach (var p in South)
            {
                float scale = p.Scale > 0f ? p.Scale : Prop;
                Place(p.Path, play, new Vector3(p.At.x, 0f, p.At.y), p.Turn, scale, true, grounded: true);
                if (p.Mirror) Place(p.Path, play, new Vector3(-p.At.x, 0f, -p.At.y), p.Turn + 180f, scale, true, grounded: true);
            }
            foreach (var t in Trampolines)
            {
                Trampoline(play, new Vector3(t.x, 0f, t.y), body);
                Trampoline(play, new Vector3(-t.x, 0f, -t.y), body);
            }

            // ---------------------------------------------------------------- spawns
            var spawns = new GameObject("Spawns").transform; spawns.SetParent(root);
            var first = Spawn(spawns, new Vector3(0f, 0f, 0f), 0f);
            first.MarkFirst(true);
            Raise(first.transform);   // onto the stage
            float r = FieldRadius - 4f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 22.5f;
                var pos = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, r);
                var s = Spawn(spawns, pos, a + 180f);   // facing the middle
                Raise(s.transform);
            }
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
