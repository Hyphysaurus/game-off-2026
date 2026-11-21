#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The living room, cleaned up and grown (D-0105, his "begin cleaning up the living room scene area and expand it"):
    /// the downstairs is 10 x 6 tiles now (650 x 390), one open floor in five parts:
    /// - the living room round the bridge's gap (couch under it, a second couch across the coffee table, the fire on the
    ///   west wall, an armchair and a floor lamp, the tree by the window);
    /// - an entry hall in the north-west: the front door (shut: set dressing for now), a mat, a coat stand, a closet;
    /// - the stairs up along the north wall into a dark stairwell, a baby gate across the bottom (dressing: not climbed);
    /// - the kitchen in the south-east: one counter run flush on the east wall (fridge, cabinets, stove, sink) with wall
    ///   cabinets and a tiled backsplash over it, an island with stools;
    /// - a dining nook in the north-east: a table with chairs, a hutch, a hanging lamp.
    /// The room itself: painted walls (cream over a sage wainscot, white rail and skirting) instead of the playroom's
    /// cloud wallpaper, oak planks (tile in the kitchen), window frames with daylight behind the glass.
    /// The materials live in Data/Downstairs; the plank texture is made from the Playroom pack's wood (so it stays in the
    /// git-ignored Generated folder), the kitchen tile is drawn here. Called by Land Downstairs (D-0103).
    /// </summary>
    public static partial class CourseBuilder
    {
        const string DownData = "Assets/_Game/Data/Downstairs";
        const string PlanksTex = "Assets/_Imports/Generated/Downstairs/Planks.png";
        const string WoodSource = "Assets/_Imports/ithappy/Playroom/Textures/Wood_Light.png";
        const int StairTileX = 6, StairTileZ = 5;                  // the ceiling tile the stairs go up through (x 420..485, z -5..60)
        const float StairX0 = 270f, StairRun = 10f, StairZ0 = 6f;  // 21 steps rising east along the north wall
        const int StairSteps = 21;

        static float DownX1 => DownX0 + DownTilesX * Tile;
        static float DownZ1 => DownZ0 + DownTilesZ * Tile;
        static bool IsKitchenTile(float cx, float cz) => cx > 485f && cz < -70f;

        /// <summary>The downstairs' own materials, made once and kept in Data/Downstairs.</summary>
        static class DownMats
        {
            public static Material Paint, Wainscot, Trim, Planks, KitchenTile, Backsplash, Daylight, LampGlow, Stair;

            public static void Load()
            {
                if (!AssetDatabase.IsValidFolder(DownData)) AssetDatabase.CreateFolder("Assets/_Game/Data", "Downstairs");
                Paint = Mat("Down_Paint", new Color(0.96f, 0.9f, 0.8f), null, 0.15f);
                Wainscot = Mat("Down_Wainscot", new Color(0.62f, 0.72f, 0.6f), null, 0.25f);
                Trim = Mat("Down_Trim", new Color(0.97f, 0.96f, 0.93f), null, 0.4f);
                Planks = Mat("Down_Planks", Color.white, PlankTexture(), 0.35f);
                KitchenTile = Mat("Down_KitchenTile", Color.white, TileTexture(), 0.55f);
                Backsplash = Mat("Down_Backsplash", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(DownData + "/KitchenTile.png"), 0.7f);
                Stair = Mat("Down_Stair", new Color(0.85f, 0.75f, 0.62f), AssetDatabase.LoadAssetAtPath<Texture2D>(PlanksTex), 0.35f);
                Daylight = Mat("Down_Daylight", new Color(0.78f, 0.9f, 1f), null, 0f, true);
                LampGlow = Mat("Down_LampGlow", new Color(1f, 0.93f, 0.78f), null, 0f, true);
            }

            static Material Mat(string name, Color c, Texture2D tex, float smooth, bool unlit = false)
            {
                string path = $"{DownData}/{name}.mat";
                var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path); }
                m.shader = shader;
                m.SetColor("_BaseColor", c);
                m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                EditorUtility.SetDirty(m);
                return m;
            }

            /// <summary>Oak boards, 8 to a floor tile, from the pack's light wood: each board its own stretch of the grain,
            /// a shade lighter or darker, a dark seam down its edges and two staggered end joints. Repeats top to bottom.</summary>
            static Texture2D PlankTexture()
            {
                var have = AssetDatabase.LoadAssetAtPath<Texture2D>(PlanksTex);
                if (have != null) return have;
                var src = AssetDatabase.LoadAssetAtPath<Texture2D>(WoodSource);
                if (src == null) { Log.AppendLine("living room: no Wood_Light: plain floor"); return null; }
                const int N = 1024, Boards = 8, W = N / Boards;
                var wood = Readable(src, N);
                var rng = new System.Random(105);
                var px = new Color[N * N];
                var tone = new Color(0.86f, 0.7f, 0.52f);
                for (int b = 0; b < Boards; b++)
                {
                    int a = rng.Next(0, N), c = (a + rng.Next(N / 3, 2 * N / 3)) % N;   // the two end joints
                    int ox0 = rng.Next(0, N), oy0 = rng.Next(0, N), ox1 = rng.Next(0, N), oy1 = rng.Next(0, N);
                    float k0 = 0.9f + (float)rng.NextDouble() * 0.2f, k1 = 0.9f + (float)rng.NextDouble() * 0.2f;
                    for (int y = 0; y < N; y++)
                    {
                        bool first = a < c ? (y >= a && y < c) : (y >= a || y < c);
                        int ox = first ? ox0 : ox1, oy = first ? oy0 : oy1; float k = first ? k0 : k1;
                        bool joint = Mathf.Min(Mathf.Abs(y - a), N - Mathf.Abs(y - a)) < 2 || Mathf.Min(Mathf.Abs(y - c), N - Mathf.Abs(y - c)) < 2;
                        for (int lx = 0; lx < W; lx++)
                        {
                            var s = wood[((oy + y) % N) * N + (ox + lx) % N];
                            var col = new Color(s.r * tone.r * k, s.g * tone.g * k, s.b * tone.b * k, 1f);
                            if (lx < 2 || lx >= W - 2 || joint) col *= 0.5f;
                            col.a = 1f;
                            px[y * N + b * W + lx] = col;
                        }
                    }
                }
                return Save(PlanksTex, N, px);
            }

            /// <summary>Kitchen floor: cream and sage squares, 4 to a floor tile, with grout.</summary>
            static Texture2D TileTexture()
            {
                string path = DownData + "/KitchenTile.png";
                var have = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (have != null) return have;
                const int N = 512, Sq = N / 4, Grout = 3;
                Color cream = new Color(0.95f, 0.92f, 0.84f), sage = new Color(0.6f, 0.7f, 0.6f), grout = new Color(0.72f, 0.7f, 0.66f);
                var px = new Color[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        int lx = x % Sq, ly = y % Sq;
                        bool g = lx < Grout || ly < Grout;
                        px[y * N + x] = g ? grout : (((x / Sq) + (y / Sq)) % 2 == 0 ? cream : sage);
                    }
                return Save(path, N, px);
            }

            static Color[] Readable(Texture2D tex, int n)
            {
                var rt = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(tex, rt); var prev = RenderTexture.active; RenderTexture.active = rt;
                var copy = new Texture2D(n, n, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, n, n), 0, 0); copy.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                var px = copy.GetPixels(); Object.DestroyImmediate(copy); return px;
            }

            static Texture2D Save(string path, int n, Color[] px)
            {
                var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
                t.SetPixels(px); t.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null) { imp.wrapMode = TextureWrapMode.Repeat; imp.anisoLevel = 4; imp.SaveAndReimport(); }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        /// <summary>The Christmas pack's cabinets come without their doors: each door is its own prefab, hinged at its own
        /// origin. Where they hang on the body (its unscaled space) and which way, as the pack's demo scene has them.</summary>
        static readonly Dictionary<string, (string door, Vector3 at, float yaw)[]> PackDoors = new Dictionary<string, (string, Vector3, float)[]>
        {
            { "Kitchen_Cabinet_001", new[] { ("Kitchen_Cabinet_001_door_001", new Vector3(0.494f, 0.353f, 0.638f), 0f), ("Kitchen_Cabinet_001_door_002", new Vector3(-0.494f, 0.353f, 0.638f), 0f) } },
            { "Kitchen_Cabinet_002", new[] { ("Kitchen_Cabinet_002_door_001", new Vector3(0.282f, 0.407f, 0.319f), 0f), ("Kitchen_Cabinet_002_door_002", new Vector3(-0.282f, 0.407f, 0.319f), 0f) } },
            { "Kitchen_sink_001", new[] { ("Kitchen_sink_door_001", new Vector3(0.388f, 0.353f, 0.638f), 270f), ("Kitchen_sink_door_002", new Vector3(-0.388f, 0.353f, 0.638f), 270f) } },
            { "Kitchen_stove_001", new[] { ("Kitchen_stove_door_001", new Vector3(0f, 0.117f, 0.585f), 0f) } },
            { "Cabinet_001", new[] { ("Cabinet_001_door_001", new Vector3(0.49f, 0.463f, 0.184f), 0f), ("Cabinet_001_door_002", new Vector3(-0.49f, 0.463f, 0.184f), 0f) } },
        };

        static void HangDoors(GameObject body, string prefab)
        {
            if (!PackDoors.TryGetValue(prefab, out var doors)) return;
            foreach (var (name, at, yaw) in doors)
            {
                var path = AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/ithappy" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
                if (path == null) { Log.AppendLine("living room: MISSING door " + name); continue; }
                var d = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), body.transform);
                d.transform.localPosition = at; d.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); d.transform.localScale = Vector3.one;
            }
        }

        static void Paint(GameObject go, Material m)
        {
            if (go == null || m == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
        }

        /// <summary>A room wall module: paint on both faces, trim colour on its edges (its first slot).</summary>
        static void PaintWall(GameObject wall)
        {
            if (wall == null) return;
            foreach (var r in wall.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mats[i] != null && mats[i].name.StartsWith("Wall_Edge") ? DownMats.Trim : DownMats.Paint;
                r.sharedMaterials = mats;
            }
        }

        // ---- light: one warm light per part of the floor, the fire, a glow under the gap and one in the stairwell
        static void DownstairsLights(Transform root)
        {
            var l = new GameObject("Lights").transform; l.SetParent(root, false);
            float hi = DownCeiling - 14f;
            PointLight("LivingLight", l, new Vector3(165f, hi, -200f), 300f, 2600f, new Color(1f, 0.86f, 0.68f), true);
            PointLight("EntryLight", l, new Vector3(140f, hi, 5f), 220f, 1200f, new Color(1f, 0.88f, 0.72f));
            PointLight("KitchenLight", l, new Vector3(580f, hi, -200f), 300f, 2600f, new Color(0.95f, 0.96f, 1f), true);
            PointLight("DiningLight", l, new Vector3(585f, DownCeiling - 45f, 5f), 200f, 1300f, new Color(1f, 0.84f, 0.62f));
            PointLight("FireGlow", l, new Vector3(DownX0 + 30f, DownFloor + 25f, -205f), 120f, 900f, new Color(1f, 0.55f, 0.25f));
            PointLight("UnderGap", l, new Vector3((GapX0 + GapX1) / 2f, DownFloor + 60f, LaneZ), 160f, 700f, new Color(1f, 0.8f, 0.55f));
            PointLight("Upstairs", l, new Vector3(452f, -14f, 30f), 60f, 150f, new Color(1f, 0.85f, 0.65f));
        }

        // ---- the room's dressing: trim, windows, the front door, the stairs
        static void Dress(Transform root)
        {
            var d = new GameObject("Dressing").transform; d.SetParent(root, false);
            float x0 = DownX0, x1 = DownX1, z0 = DownZ0, z1 = DownZ1;
            // trim round the walls, broken for the toy door home (south, x 190), the front door (west), the stairs (north)
            // and the counter run (east, which gets a tiled backsplash instead)
            Trim(d, "S", Vector3.back, x0, x1, z0, new[] { (160f, 220f) });
            Trim(d, "N", Vector3.forward, x0, x1, z1, new[] { (262f, 486f) });
            Trim(d, "W", Vector3.left, z0, z1, x0, new[] { (-58f, 18f) });
            Trim(d, "E", Vector3.right, z0, z1, x1, new[] { (z0, -25f) });
            // windows: a frame on the wall, daylight behind the glass
            int w = 0;
            void Window(Vector3 dirOut, float along)
            {
                Vector3 face = dirOut.x != 0f ? new Vector3(dirOut.x > 0f ? x1 : x0, 0f, along) : new Vector3(along, 0f, dirOut.z > 0f ? z1 : z0);
                var win = Spawn("Window_01", "Window_" + (++w), d, new Vector3(face.x, DownFloor + 55f, face.z) + dirOut * 3.73f, Quaternion.LookRotation(-dirOut).eulerAngles, 1f);
                Quad("Daylight", win.transform, new Vector3(face.x, DownFloor + 85f, face.z) - dirOut * 0.3f, Quaternion.LookRotation(dirOut).eulerAngles, new Vector3(40f, 56f, 1f), DownMats.Daylight);
            }
            float T(int i, float from) => from + (i + 0.5f) * Tile;
            foreach (int i in new[] { 0, 4, 8 }) Window(Vector3.back, T(i, x0));
            foreach (int i in new[] { 1, 8 }) Window(Vector3.forward, T(i, x0));
            foreach (int k in new[] { 0, 3 }) Window(Vector3.left, T(k, z0));
            Window(Vector3.right, T(5, z0));
            Log.AppendLine($"living room: trim round the walls, {w} windows");
            FrontDoor(d);
            Stairs(d);
        }

        /// <summary>Skirting, a sage wainscot and a rail along one wall's inner face from <paramref name="from"/> to
        /// <paramref name="to"/>, less the <paramref name="skip"/> stretches. The east and west runs stop short of the
        /// corners by each strip's own depth, so the strips meet the north and south ones without overlapping.</summary>
        static void Trim(Transform parent, string side, Vector3 dirOut, float from, float to, float face, (float a, float b)[] skip)
        {
            (string name, float y0, float y1, float depth, Material m)[] strips =
            {
                ("Skirting", 0f, 7f, 1.6f, DownMats.Trim), ("Wainscot", 7f, 46f, 0.8f, DownMats.Wainscot), ("Rail", 46f, 49f, 1.6f, DownMats.Trim),
            };
            bool alongZ = dirOut.x != 0f;
            foreach (var s in strips)
            {
                float lo = from + (alongZ ? s.depth : 0f), hi = to - (alongZ ? s.depth : 0f);
                var runs = new List<(float, float)> { (lo, hi) };
                foreach (var (a, b) in skip)
                {
                    var next = new List<(float, float)>();
                    foreach (var (p, q) in runs)
                    {
                        if (b <= p || a >= q) { next.Add((p, q)); continue; }
                        if (a > p) next.Add((p, a));
                        if (b < q) next.Add((b, q));
                    }
                    runs = next;
                }
                foreach (var (p, q) in runs)
                {
                    float mid = (p + q) / 2f, len = q - p, c = face - (alongZ ? dirOut.x : dirOut.z) * s.depth / 2f, yc = DownFloor + (s.y0 + s.y1) / 2f;
                    Vector3 at = alongZ ? new Vector3(c, yc, mid) : new Vector3(mid, yc, c);
                    Vector3 size = alongZ ? new Vector3(s.depth, s.y1 - s.y0, len) : new Vector3(len, s.y1 - s.y0, s.depth);
                    Cube($"{s.name}_{side}", parent, at, size, s.m);
                }
            }
        }

        /// <summary>The house's front door on the west wall of the entry hall: shut, set dressing for now (the way out to
        /// the yard later), a mat in front of it.</summary>
        static void FrontDoor(Transform parent)
        {
            var door = Furniture(parent, "Door_001", new Vector3(DownX0 + 10f, DownFloor, -20f), 90f, 125f);
            if (door != null) { door.name = "FrontDoor"; Flush(door, Vector3.left, DownX0 + 0.2f); }
            var mat = Furniture(parent, "Carpet_002", new Vector3(DownX0 + 30f, DownFloor, -20f), 90f);
            if (mat != null) { mat.name = "DoorMat"; Shrink(mat, 0.4f); }
        }

        /// <summary>The stairs up: solid wooden steps along the north wall, rising east into a dark stairwell (the ceiling
        /// tile over the top is left out, Shell), a banister on the open side and a baby gate across the bottom.</summary>
        static void Stairs(Transform parent)
        {
            var st = new GameObject("Stairs").transform; st.SetParent(parent, false);
            float rise = (DownCeiling - DownFloor) / StairSteps, zN = DownZ1 - 0.5f, width = zN - StairZ0, zc = (StairZ0 + zN) / 2f;
            for (int i = 0; i < StairSteps; i++)
            {
                float top = rise * (i + 1);
                Cube("Step_" + (i + 1), st, new Vector3(StairX0 + (i + 0.5f) * StairRun, DownFloor + top / 2f, zc), new Vector3(StairRun, top, width), DownMats.Stair);
            }
            // the banister on the south edge: a post every third step and a rail along the slope, stopping in the stairwell
            const float postH = 30f; int lastPost = 18;
            for (int i = 0; i <= lastPost; i += 3)
                Cube("Post", st, new Vector3(StairX0 + (i + 0.5f) * StairRun, DownFloor + rise * (i + 1) + postH / 2f, StairZ0 + 1.2f), new Vector3(1.8f, postH, 1.8f), DownMats.Trim);
            Vector3 r0 = new Vector3(StairX0 + 0.5f * StairRun, DownFloor + rise + postH, StairZ0 + 1.2f), r1 = new Vector3(StairX0 + (lastPost + 0.5f) * StairRun, DownFloor + rise * (lastPost + 1) + postH, StairZ0 + 1.2f);
            var rail = Cube("Banister", st, (r0 + r1) / 2f, new Vector3(2.2f, 2.2f, (r1 - r0).magnitude + 2f), DownMats.Stair);
            rail.transform.rotation = Quaternion.LookRotation(r1 - r0);
            // the stairwell: four dark walls and a lid over the missing ceiling tile, inset from its neighbours' edges
            var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/Greybox_Dark.mat");
            float hx0 = DownX0 + StairTileX * Tile, hx1 = hx0 + Tile, hz0 = DownZ0 + StairTileZ * Tile, hz1 = hz0 + Tile, lid = -8f, bot = DownCeiling - 0.5f, h = lid - bot;
            Cube("WellW", st, new Vector3(hx0 + 1f, bot + h / 2f, (hz0 + hz1) / 2f), new Vector3(1f, h, Tile - 1f), dark);
            Cube("WellE", st, new Vector3(hx1 - 1f, bot + h / 2f, (hz0 + hz1) / 2f), new Vector3(1f, h, Tile - 1f), dark);
            Cube("WellS", st, new Vector3((hx0 + hx1) / 2f, bot + h / 2f, hz0 + 1f), new Vector3(Tile - 3f, h, 1f), dark);
            Cube("WellN", st, new Vector3((hx0 + hx1) / 2f, bot + h / 2f, hz1 - 1f), new Vector3(Tile - 3f, h, 1f), dark);
            Cube("WellLid", st, new Vector3((hx0 + hx1) / 2f, lid + 0.5f, (hz0 + hz1) / 2f), new Vector3(Tile - 1f, 1f, Tile - 1f), dark);
            // the baby gate: posts, two rails and bars, and a clear wall to the ceiling (nobody goes up yet)
            float gx = StairX0 - 4f, gh = 40f;
            var gate = new GameObject("BabyGate").transform; gate.SetParent(st, false);
            Cube("PostS", gate, new Vector3(gx, DownFloor + gh / 2f, StairZ0 + 1f), new Vector3(2.5f, gh, 2.5f), DownMats.Trim);
            Cube("PostN", gate, new Vector3(gx, DownFloor + gh / 2f, zN - 1.5f), new Vector3(2.5f, gh, 2.5f), DownMats.Trim);
            Cube("RailLow", gate, new Vector3(gx, DownFloor + 4f, zc), new Vector3(1.5f, 1.5f, width - 3f), DownMats.Trim);
            Cube("RailHigh", gate, new Vector3(gx, DownFloor + gh - 2f, zc), new Vector3(1.5f, 1.5f, width - 3f), DownMats.Trim);
            for (float z = StairZ0 + 6f; z < zN - 4f; z += 6f) Cube("Bar", gate, new Vector3(gx, DownFloor + gh / 2f, z), new Vector3(0.8f, gh - 6f, 0.8f), DownMats.Trim);
            var block = new GameObject("GateBlock"); block.transform.SetParent(gate, false);
            block.transform.position = new Vector3(gx, (DownFloor + DownCeiling) / 2f, zc);
            block.AddComponent<BoxCollider>().size = new Vector3(3f, DownCeiling - DownFloor, width);
            Log.AppendLine($"living room: {StairSteps} stairs x {StairX0}..{StairX0 + StairSteps * StairRun} up into the stairwell at x {hx0}..{hx1}, a baby gate at x {gx}");
        }

        // ---- furniture, part by part
        static void Furnish(Transform root)
        {
            var f = new GameObject("Furniture").transform; f.SetParent(root, false);
            float y = DownFloor, gx = (GapX0 + GapX1) / 2f, x0 = DownX0, x1 = DownX1, z0 = DownZ0, z1 = DownZ1;

            // living: the couch right under the gap (a soft place to come down) and one across the coffee table from it,
            // the fire on the west wall at the sofas' end, an armchair and a lamp, the tree by the west window
            Furniture(f, "Carpet_001", new Vector3(gx, y, -205f), 0f);
            Furniture(f, "Couch_08", new Vector3(gx, y, LaneZ), 180f);
            Furniture(f, "Coffee_Table_03", new Vector3(gx, y, -205f), 0f);
            Furniture(f, "Couch_11", new Vector3(gx, y, -262f), 0f);
            var fire = Furniture(f, "Fireplace_001", new Vector3(x0 + 20f, y, -205f), 90f, 128f); Flush(fire, Vector3.left, x0 + 0.2f);
            Furniture(f, "Armchair_18", new Vector3(95f, y, -265f), 45f);
            Furniture(f, "Light_001", new Vector3(55f, y, -300f), 0f);
            Furniture(f, "Light_05", new Vector3(gx + 80f, y, LaneZ), 0f);
            Furniture(f, "Christmass_tree_indoor_001", new Vector3(x0 + 45f, y, -100f), 0f, 115f);
            var plant = Furniture(f, "Plants_15", new Vector3(x0 + 20f, y, z0 + 15f), 0f); Flush(plant, Vector3.back, z0 + 0.5f);
            var pic = Furniture(f, "Picture_17", new Vector3(257f, y + 70f, z0 + 4f), 0f); Flush(pic, Vector3.back, z0 + 0.2f);

            // entry hall: a coat stand in the corner by the front door, a closet on the north wall past the window, a tall
            // plant before the stairs' gate
            Furniture(f, "Hanger_001", new Vector3(x0 + 20f, y, z1 - 20f), 0f);
            var closet = Furniture(f, "Closet_02", new Vector3(180f, y, z1 - 15f), 180f); Flush(closet, Vector3.forward, z1 - 0.2f);
            Furniture(f, "Plants_19", new Vector3(236f, y, z1 - 28f), 0f);

            // kitchen: one run flush on the east wall from the south corner, wall cabinets over the base cabinets, the
            // backsplash behind, things on the counters; the island in the middle with stools on its west side
            string[] run = { "Fridge_01", "Kitchen_Cabinet_001", "Kitchen_stove_001", "Kitchen_Cabinet_001", "Kitchen_sink_001", "Kitchen_Cabinet_001" };
            float cursor = z0 + 1f; var placed = new List<GameObject>();
            foreach (var piece in run)
            {
                var go = Furniture(f, piece, new Vector3(x1 - 25f, y, cursor + 25f), -90f);
                if (go == null) continue;
                var b = B(go); go.transform.position += new Vector3(0f, 0f, cursor - b.min.z);
                Flush(go, Vector3.right, x1 - 0.2f);
                cursor = B(go).max.z + 0.3f; placed.Add(go);
            }
            float runEnd = cursor;
            // the backsplash: a quad (its UVs run along it), tiled so the squares are 8 across as on the floor
            float bl = runEnd - z0 - 1f;
            DownMats.Backsplash.SetTextureScale("_BaseMap", new Vector2(bl / 32f, 34f / 32f)); EditorUtility.SetDirty(DownMats.Backsplash);
            Quad("Backsplash", f, new Vector3(x1 - 0.15f, y + 64f, (z0 + runEnd) / 2f), Quaternion.LookRotation(Vector3.right).eulerAngles, new Vector3(bl, 34f, 1f), DownMats.Backsplash);
            int top = 0;
            foreach (var go in placed)
            {
                if (!go.name.StartsWith("Kitchen_Cabinet_001")) continue;
                var b = B(go);
                var up = Furniture(f, "Kitchen_Cabinet_002", new Vector3(b.center.x, y + 84f, b.center.z), -90f);
                if (up != null) { up.name = "WallCabinet"; Flush(up, Vector3.right, x1 - 0.2f); }
                string[] things = { "Microwave_01", "Cutting_board_02", "Mixer_08" };
                var t = Furniture(f, things[top++ % things.Length], new Vector3(b.center.x - 3f, b.max.y, b.center.z), -90f);
            }
            var stove = placed.Find(g => g.name.StartsWith("Kitchen_stove"));
            if (stove != null) { var b = B(stove); Furniture(f, "Teapot_001", new Vector3(b.center.x, b.max.y, b.center.z - 8f), 0f); }
            var island = Furniture(f, "Kitchen_Table_09", new Vector3(560f, y, -200f), 90f);
            for (int i = 0; i < 3; i++) Furniture(f, "Kitchen_stool_003", new Vector3(527f, y, -230f + i * 30f), 90f);
            if (island != null)
            {
                float t = B(island).max.y;
                string[] food = { "Burger_001", "Donut_002", "Cheesecake_001", "Watermelon_002", "Soda_002", "Croissant_001" };
                for (int i = 0; i < food.Length; i++) Furniture(f, food[i], new Vector3(552f + (i % 2) * 16f, t, -230f + (i / 2) * 30f), i * 50f);
            }

            // dining: the long table, four chairs round it, the hutch on the north wall under the window, a hanging lamp
            var table = Furniture(f, "Kitchen_Table_001", new Vector3(585f, y, 5f), 0f);
            if (table != null) table.name = "DiningTable";
            foreach (var (cx, cz, yaw) in new[] { (563f, -30f, 0f), (607f, -30f, 0f), (563f, 40f, 180f), (607f, 40f, 180f) })
                Furniture(f, "Armchair_02", new Vector3(cx, y, cz), yaw);
            var hutch = Furniture(f, "Cabinet_001", new Vector3(585f, y, z1 - 15f), 180f); Flush(hutch, Vector3.forward, z1 - 0.2f);
            if (hutch != null) { var b = B(hutch); Furniture(f, "Bowl_001", new Vector3(b.center.x - 15f, b.max.y, b.center.z), 0f); Furniture(f, "Teapot_001", new Vector3(b.center.x + 15f, b.max.y, b.center.z), 0f); }
            var lamp = Furniture(f, "Light_003", new Vector3(585f, y, 5f), 0f, 30f);
            if (lamp != null) lamp.transform.position += Vector3.up * (DownCeiling - B(lamp).max.y);
            // the kitchen's ceiling light: a flat glowing panel
            var panel = Cube("CeilingPanel", f, new Vector3(580f, DownCeiling - 0.6f, -200f), new Vector3(40f, 1f, 40f), DownMats.LampGlow);
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            Log.AppendLine($"living room: furnished ({f.childCount} pieces), the counter run z {z0}..{runEnd:F0}");
        }

        /// <summary>Moves <paramref name="go"/> so its bounds touch the plane <paramref name="at"/> on the
        /// <paramref name="dirOut"/> side (a wall it should stand against).</summary>
        static void Flush(GameObject go, Vector3 dirOut, float at)
        {
            if (go == null) return;
            var b = B(go);
            if (dirOut.x > 0f) go.transform.position += Vector3.right * (at - b.max.x);
            else if (dirOut.x < 0f) go.transform.position += Vector3.right * (at - b.min.x);
            else if (dirOut.z > 0f) go.transform.position += Vector3.forward * (at - b.max.z);
            else go.transform.position += Vector3.forward * (at - b.min.z);
        }

        /// <summary>Scales <paramref name="go"/> by <paramref name="k"/> about its footprint's centre, keeping it on the floor.</summary>
        static void Shrink(GameObject go, float k)
        {
            var b0 = B(go);
            go.transform.localScale *= k;
            var b1 = B(go);
            go.transform.position += new Vector3(b0.center.x - b1.center.x, b0.min.y - b1.min.y, b0.center.z - b1.center.z);
        }
    }
}
#endif
