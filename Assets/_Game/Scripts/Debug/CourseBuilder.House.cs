#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Roadmap item 1, "one house" (D-0090, D-0091).
    /// - The tunnel (old R3, the skirting run, toy only, 14 high) is the tutorial hallway: its lessons in order, each with a
    ///   short sign. (1) Moving: the hop, the book stairs, the sprint gap. (2) Darts: a dart pistol on its stand, an ammo
    ///   box, three practice skittles that tip over, then one toy soldier that shoots back (its darts only push). (3) The
    ///   shift charge: a core that only fills the charge (ShiftSource ChargeOnly) and a sign teaching F; F in the tunnel
    ///   says "No room to grow". Then the second half's test (the pencil bridge over a drop, the pins) and the tall end: a
    ///   shaft where growing fits, a shelf only a kid can get onto, a core up there that makes the kid small again, and
    ///   the hole out at the shelf's level into the playroom. The whole tunnel stays walkable as a toy up to the shaft.
    /// - The playroom is the hub: doors in its walls (HubDoor) to the Raceway (a toy garage) and the Colour Corner, each with a door
    ///   home there that comes back out at the same playroom door (the Workshop waits: it may fold into the playroom). Size, the weapons in hand with
    ///   their ammo, and the shift charge go through (HouseCarry).
    /// - The bedroom's pistol stand (D-0085) is gone; the switch still takes E or a dart.
    /// Batch: -executeMethod ShiftJam.CourseBuilder.LandHouse (-nographics is fine). Greybox, the Raceway and the Colour Corner saved.
    /// </summary>
    public static partial class CourseBuilder
    {
        // ---- the hallway, along x at z -150 (pack units). Toy: jump 4.4 far at a walk, 6.4 at a sprint, 2.0 up.
        const float HallW = 20f, HallH = 14f;
        const float HallHop0 = -32f, HallHop1 = -28.5f;            // 3.5: a standing jump
        const float HallStairs = -22f;                             // the first book of the stairs onto the 12 box
        const float HallSprint0 = 68f, HallSprint1 = 73.5f;        // 5.5: a walk falls, a sprint makes it (D-0081)
        const float HallStand = 89f, HallSkittles = 106f, HallCover = 128f, HallSoldier = 142f;
        const float HallCore = 162f;                               // the charge core
        const float HallBridge0 = 172f, HallBridge1 = 206f;        // the pencil bridge's drop
        const float HallTall0 = 232f;                              // the tall end starts here and runs to LaneX1
        const float HallTallZ0 = -196f, HallTallTop = 130f;        // its south wall; its north wall is the hallway's
        const float HallShelfX = 263f, HallShelfTop = 40f;         // a kid jumps 67: the shelf is a kid's step, a toy's wall
        const float HallHoleZ = -150f, HallShelfCoreZ = -162f;

        const string SkittlePiece = "Assets/_Game/Prefabs/Pieces/Interactables/PracticeSkittle.prefab";
        const string SignFontPath = "Assets/_Game/Resources/HUD/Fredoka SDF.asset";
        const string SignCardPath = "Assets/_Game/Data/SignCard.mat";
        const string DoorPiece = "Door_01";

        // =====================================================================================
        static Transform BuildHallway(Transform r3, Material dark, Material glow)
        {
            float z = LaneZ, w = HallW, h = HallH, x0 = LaneX0, x1 = LaneX1;
            float zs = z - w / 2f, zn = z + w / 2f;   // the hallway's south and north sides (-160, -140)

            // ---- shell: floor in pieces round the three drops, the drop's floor 24 down (a fall past 14 = respawn)
            float[][] floors = { new[] { x0, HallHop0 }, new[] { HallHop1, HallSprint0 }, new[] { HallSprint1, HallBridge0 }, new[] { HallBridge1, HallTall0 } };
            foreach (var f in floors) Cube("Floor", r3, new Vector3((f[0] + f[1]) / 2f, -0.5f, z), new Vector3(f[1] - f[0], 1f, w), dark);
            Cube("PitFloor", r3, new Vector3((x0 + HallTall0) / 2f, -25f, z), new Vector3(HallTall0 - x0, 1f, w), dark);
            Cube("WallN", r3, new Vector3((x0 + HallTall0) / 2f, h / 2f, zn + 0.5f), new Vector3(HallTall0 - x0, h + 26f, 1f), dark);
            Cube("WallS", r3, new Vector3((x0 + HallTall0) / 2f, h / 2f, zs - 0.5f), new Vector3(HallTall0 - x0, h + 26f, 1f), dark);
            Cube("WallW", r3, new Vector3(x0 - 0.5f, h / 2f, z), new Vector3(1f, h + 26f, w + 2f), dark);
            Cube("Ceiling", r3, new Vector3((x0 + HallTall0) / 2f, h + 0.5f, z), new Vector3(HallTall0 - x0, 1f, w), dark);
            foreach (float lx in new[] { -40f, 40f, 115f, 165f, 200f }) PointLight("LaneLight", r3, new Vector3(lx, h - 2f, z), 45f, 1.6f, new Color(1f, 0.85f, 0.6f));

            // ---- the tall end: a shaft 49 long, 56 across, 130 high (the playroom's ceiling): a kid is 63 tall, 21 across
            float tz0 = HallTallZ0, tzc = (tz0 + zn) / 2f, tl = x1 - HallTall0, tw = zn - tz0, th = HallTallTop;
            Cube("TallFloor", r3, new Vector3((HallTall0 + x1) / 2f, -0.5f, tzc), new Vector3(tl, 1f, tw), dark);
            Cube("TallWallN", r3, new Vector3((HallTall0 + x1) / 2f, th / 2f - 6f, zn + 0.5f), new Vector3(tl + 2f, th + 12f, 1f), dark);
            Cube("TallWallS", r3, new Vector3((HallTall0 + x1) / 2f, th / 2f - 6f, tz0 - 0.5f), new Vector3(tl + 2f, th + 12f, 1f), dark);
            Cube("TallWallE", r3, new Vector3(x1 + 0.5f, th / 2f - 6f, tzc), new Vector3(1f, th + 12f, tw + 2f), dark);
            Cube("TallWallW", r3, new Vector3(HallTall0 - 0.5f, th / 2f - 6f, (tz0 + zs) / 2f), new Vector3(1f, th + 12f, zs - tz0), dark);   // beside the hallway's mouth
            Cube("TallWallW_Over", r3, new Vector3(HallTall0 - 0.5f, (h + th) / 2f, z), new Vector3(1f, th - h, w), dark);                     // over it
            Cube("TallCeiling", r3, new Vector3((HallTall0 + x1) / 2f, th + 0.5f, tzc), new Vector3(tl, 1f, tw), dark);
            var shelf = Cube("TallShelf", r3, new Vector3((HallShelfX + x1) / 2f, HallShelfTop / 2f, tzc), new Vector3(x1 - HallShelfX, HallShelfTop, tw), dark);
            PointLight("TallLight", r3, new Vector3((HallTall0 + HallShelfX) / 2f, th - 15f, tzc), 200f, 2f, new Color(1f, 0.7f, 0.4f), true);

            // ---- start (where the toy door drops you), facing east
            var start = Point("Start", r3, new Vector3(x0 + 6f, 0f, z), 90f);
            Checkpoint("CP_start", r3, new Vector3(x0 + 10f, 2f, z), new Vector3(6f, 4f, w));

            // ==== lesson 1: moving. The hop, the book stairs, the sprint gap (as walk-checked 19 Nov, D-0081)
            Sign("Sign1_Move", r3, new Vector3(x0 + 16f, 3.2f, zn - 0.15f), Vector3.back, "<b>MOVE</b>  W A S D\n<b>JUMP</b>  Space", 7f, 3f);
            Checkpoint("CP_afterHop", r3, new Vector3((HallHop1 + HallStairs) / 2f, 2f, z), new Vector3(HallStairs - HallHop1, 4f, w));
            var box = Rest(Spawn("Cardboard_Box_06", "StepBox", r3, new Vector3(HallStairs + 32f, 0f, z), Vector3.zero, 1f), 0f, null);
            float boxTop = B(box).max.y;
            int stairBooks = Mathf.CeilToInt(boxTop / 1.2f);
            box.transform.position += Vector3.right * ((HallStairs + 2f + (stairBooks + 0.5f) * 2.4f) - B(box).min.x);
            BookStairs(r3, new Vector3(B(box).min.x - (stairBooks + 0.5f) * 2.4f + 1.2f, 0f, z), Vector3.right, 0f, boxTop, "UpStairs");
            {   // the first book starts at HallStairs, leaving the hop's landing clear floor with its checkpoint
                var stairs = r3.Find("UpStairs"); float lead = HallStairs - B(stairs.gameObject).min.x;
                stairs.position += Vector3.right * lead; box.transform.position += Vector3.right * lead;
            }
            float boxEnd = B(box).max.x;
            Log.AppendLine($"hallway: book stairs from {HallStairs} onto the {boxTop:F1} box, box ends at x {boxEnd:F1}{(boxEnd > 42f ? "  (OVER 42: the flat run before the sprint gap is short)" : "")}");
            Sign("Sign1_Sprint", r3, new Vector3(HallSprint0 - 12f, 3.2f, zn - 0.15f), Vector3.back, "Too far to jump?\nHold <b>Shift</b> to run, then jump", 7f, 3f);
            Checkpoint("CP_beforeSprint", r3, new Vector3(HallSprint0 - 6f, 2f, z), new Vector3(6f, 4f, w));
            Checkpoint("CP_afterSprint", r3, new Vector3(HallSprint1 + 6f, 2f, z), new Vector3(6f, 4f, w));

            // ==== lesson 2: darts. The pistol on its stand, an ammo box, skittles that tip over, one soldier that shoots back
            Sign("Sign2_Darts", r3, new Vector3(HallStand - 5f, 3.2f, zn - 0.15f), Vector3.back, "<b>E</b>  take the dart pistol\n<b>Click</b> shoot   <b>Right click</b> aim   <b>R</b> reload", 8f, 3f);
            var pistol = AssetDatabase.LoadAssetAtPath<GameObject>(PistolPiece);
            var ammo = AssetDatabase.LoadAssetAtPath<GameObject>(AmmoPiece);
            var skittle = AssetDatabase.LoadAssetAtPath<GameObject>(SkittlePiece);
            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPiece);
            if (pistol != null) { var s = (GameObject)PrefabUtility.InstantiatePrefab(pistol, r3); s.name = "PistolStand"; s.transform.SetPositionAndRotation(new Vector3(HallStand, 0f, z - 5f), Quaternion.LookRotation(Vector3.left)); Rest(s, 0.02f, null); }
            else Log.AppendLine("hallway: NO " + PistolPiece);
            if (ammo != null) { var a = (GameObject)PrefabUtility.InstantiatePrefab(ammo, r3); a.name = "HallAmmo"; a.transform.SetPositionAndRotation(new Vector3(HallStand + 2f, 0f, z + 6f), Quaternion.identity); Rest(a, 0.02f, null); }
            else Log.AppendLine("hallway: NO " + AmmoPiece);
            Checkpoint("CP_darts", r3, new Vector3(HallStand + 6f, 2f, z), new Vector3(4f, 4f, w));
            if (skittle != null)
            {
                var spots = new[] { new Vector3(HallSkittles, 0f, z + 5f), new Vector3(HallSkittles + 4f, 0f, z - 5f), new Vector3(HallSkittles + 8f, 0f, z) };
                for (int i = 0; i < spots.Length; i++)
                {
                    var t = (GameObject)PrefabUtility.InstantiatePrefab(skittle, r3); t.name = "Skittle_" + (i + 1);
                    t.transform.SetPositionAndRotation(spots[i], Quaternion.LookRotation(Vector3.left)); Rest(t, 0.02f, null);
                }
            }
            else Log.AppendLine("hallway: NO " + SkittlePiece);
            Sign("Sign2_Soldier", r3, new Vector3(HallCover - 8f, 3.2f, zn - 0.15f), Vector3.back, "This one shoots back.\nIts darts only push you.", 7f, 3f);
            var cover = Rest(Spawn("Cardboard_Box_02", "Cover", r3, new Vector3(HallCover, 0f, z + 5f), new Vector3(0, 15, 0), 1f), 0f, null);
            if (B(cover).max.y > h - 1f) Log.AppendLine($"hallway: the cover box is {B(cover).size.y:F1} high (the hallway is {h})");
            if (soldier != null)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(soldier, r3); s.name = "HallSoldier";
                s.transform.SetPositionAndRotation(new Vector3(HallSoldier, 0.02f, z - 4f), Quaternion.LookRotation(Vector3.left));
                Set(s.GetComponent<ToySoldier>(), "wanderRadius", 2.5f);   // it keeps to its end of the hallway
            }
            else Log.AppendLine("hallway: NO " + SoldierPiece);
            Checkpoint("CP_afterSoldier", r3, new Vector3(HallSoldier + 10f, 2f, z), new Vector3(4f, 4f, w));

            // ==== lesson 3: the shift charge. A core that only fills it, and F (which the tunnel is too low for)
            Material coreMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/ShiftCore.mat");
            Sign("Sign3_Charge", r3, new Vector3(HallCore - 6f, 3.4f, zn - 0.15f), Vector3.back, "A core fills your <b>shift charge</b>.\n<b>F</b>  shift anywhere there is room.\nToo low in here: try it.", 8f, 3.6f);
            var charger = Core("ChargeCore", r3, new Vector3(HallCore, 2.5f, z + 5f), ShiftSource.Mode.ChargeOnly, coreMat, false);
            Checkpoint("CP_charge", r3, new Vector3(HallCore - 4f, 2f, z), new Vector3(4f, 4f, w));
            Rest(Spawn("Night_Lamp_02", "Lantern", r3, new Vector3(HallCore + 4f, 0f, z - 6f), Vector3.zero, 1f), 0f, null);

            // ==== the test: the pencil bridge over a drop, the pins (a touch puts you back)
            int pencils = Mathf.CeilToInt((HallBridge1 - HallBridge0 + 1f) / 7f);
            for (int i = 0; i < pencils; i++) Rest(Spawn("Pencil_01", "Pencil_" + i, r3, new Vector3(HallBridge0 - 1f + 3.5f + i * 7f, 0f, z), new Vector3(0, 90, 0), 1f), 0f, null);
            Checkpoint("CP_afterBridge", r3, new Vector3(HallBridge1 + 3f, 2f, z), new Vector3(4f, 4f, w));
            foreach (float px in new[] { 214f, 219f, 224f })
            {
                var pin = Rest(Spawn("Pin_01", "Pin", r3, new Vector3(px, 0f, z + (Mathf.RoundToInt(px) % 2 == 0 ? 3f : -3f)), new Vector3(-90, 0, 0), 1f), 0f, null);
                var hz = Trigger("Hazard", pin.transform, B(pin).center, new Vector3(1.6f, B(pin).size.y, 1.6f)); hz.AddComponent<Hazard>();
            }

            // ==== the tall end: grow (F), up onto the shelf, the shelf's core makes you small, out through the hole
            Checkpoint("CP_tall", r3, new Vector3(HallTall0 + 6f, 2f, z), new Vector3(6f, 4f, w), ShiftSize.Toy);
            Sign("Sign3_Grow", r3, new Vector3(HallTall0 + 10f, 3.6f, zn - 0.15f), Vector3.back, "Room to grow here.\nStep into the middle and press <b>F</b>.\nUp on the shelf a core makes you small again.", 9f, 3.8f);
            Sign("Sign3_GrowKid", r3, new Vector3(HallTall0 + 16f, 62f, zn - 0.15f), Vector3.back, "Up on the shelf: the core makes you small.\nThe way out is the hole up there.", 50f, 14f);
            var shelfCore = Core("CoreE_Shelf", r3, new Vector3((HallShelfX + x1) / 2f - 1f, HallShelfTop + 2.5f, HallShelfCoreZ), ShiftSource.Mode.ShrinkOnly, coreMat, false);
            Set(shelfCore.GetComponent<ShiftSource>(), "standWithin", CoreStandWithin);   // from the shelf only (D-0083): a kid on the floor reaches 60 up
            Quad("ExitGlow", r3, new Vector3(x1 - 0.1f, HallShelfTop + 3f, HallHoleZ), new Vector3(0, -90, 0), new Vector3(6f, 6f, 1f), glow);
            PointLight("ExitLight", r3, new Vector3(x1 - 6f, HallShelfTop + 4f, HallHoleZ), 20f, 2.5f, new Color(1f, 0.85f, 0.55f));
            var exit = Trigger("ExitPortal", r3, new Vector3(x1 - 2f, HallShelfTop + 3f, HallHoleZ), new Vector3(2f, 6f, 8f));
            var portal = exit.AddComponent<Portal>(); Set(portal, "requiredSize", (int)ShiftSize.Toy);
            var hum = exit.AddComponent<AudioSource>(); hum.clip = Clip("Room_Ambience_Loop_v1"); hum.loop = true; hum.playOnAwake = true; hum.spatialBlend = 1f; hum.rolloffMode = AudioRolloffMode.Linear; hum.minDistance = 4f; hum.maxDistance = 90f; hum.volume = 0.7f;
            Log.AppendLine($"hallway built x {x0}..{x1} at z {z}; tall end x {HallTall0}..{x1}, z {tz0}..{zn}, {th} high; shelf top {HallShelfTop} from x {HallShelfX}; charge core at {charger.transform.position}, shelf core at {shelfCore.transform.position}; exit at the shelf's level");
            return start;
        }

        /// <summary>A plain sign: a card with text (TextMeshPro in the world), facing <paramref name="toViewer"/>. Placeholder
        /// until the 3D text service (roadmap item 4).</summary>
        static GameObject Sign(string name, Transform parent, Vector3 centre, Vector3 toViewer, string text, float width, float height)
        {
            toViewer.y = 0f; toViewer.Normalize();
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(-toViewer));   // TMP reads from its -Z side
            var card = GameObject.CreatePrimitive(PrimitiveType.Cube); card.name = "Card"; Object.DestroyImmediate(card.GetComponent<Collider>());
            card.transform.SetParent(root.transform, false); card.transform.localPosition = new Vector3(0f, 0f, 0.08f); card.transform.localScale = new Vector3(width + height * 0.12f, height * 1.12f, 0.1f);
            card.GetComponent<Renderer>().sharedMaterial = SignCard();
            var tgo = new GameObject("Text", typeof(RectTransform)); tgo.transform.SetParent(root.transform, false);
            var tmp = tgo.AddComponent<TextMeshPro>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SignFontPath); if (font != null) tmp.font = font;
            tmp.rectTransform.sizeDelta = new Vector2(width, height); tmp.rectTransform.localPosition = Vector3.zero;
            tmp.text = text; tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.2f; tmp.fontSizeMax = 200f;
            tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(0.16f, 0.12f, 0.1f); tmp.enableWordWrapping = true;
            return root;
        }

        static Material SignCard()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(SignCardPath);
            if (m != null) return m;
            var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/Greybox_Dark.mat");
            m = dark != null ? new Material(dark) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "SignCard"; m.SetColor("_BaseColor", new Color(0.93f, 0.88f, 0.76f)); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(m, SignCardPath);
            return m;
        }

        // =====================================================================================
        // The hub doors
        sealed class HubPlan
        {
            public string id, scene, arriveAt, homeId, prompt, title, homePrompt; public Vector3 pref; public string[] dress;
        }

        static readonly HubPlan[] Hubs =
        {
            // The Workshop may fold into the playroom itself (his call, 20 Nov): no door for it yet. A row here (id, scene,
            // home id, spot on the wall, dressing) is all another door needs.
            new HubPlan { id = "playroom.raceway", scene = TitleMenu.RacewayScene, homeId = "raceway.home", prompt = "Go into the Raceway", title = "RACEWAY", homePrompt = "Back to the playroom",
                          pref = new Vector3(415f, 0f, -302.5f), dress = new[] { "Car_02", "Car_04", "Toy_Sign_01", "Wooden_Cone_01" } },
            new HubPlan { id = "playroom.colours", scene = TitleMenu.ColourCornerScene, homeId = "colours.home", prompt = "Go into the Colour Corner", title = "COLOUR CORNER", homePrompt = "Back to the playroom",
                          pref = new Vector3(675f, 0f, -45f), dress = new[] { "Paint_Red_01", "Paint_Blue_01", "Paint_Yellow_01", "Paint_Green_01", "Marker_Pink_01" } },
        };

        /// <summary>The playroom's four walls: a point on the inside face, the way into the room, and the run of the wall.</summary>
        static IEnumerable<(Vector3 face, Vector3 inward, Vector3 along, float from, float to)> PlayroomWalls()
        {
            Vector3 c = C4;
            yield return (new Vector3(c.x - R4HalfX, 0f, 0f), Vector3.right, Vector3.forward, c.z - R4HalfZ, c.z + R4HalfZ);   // west
            yield return (new Vector3(0f, 0f, c.z - R4HalfZ), Vector3.forward, Vector3.right, c.x - R4HalfX, c.x + R4HalfX);   // south
            yield return (new Vector3(c.x + R4HalfX, 0f, 0f), Vector3.left, Vector3.forward, c.z - R4HalfZ, c.z + R4HalfZ);    // east
            yield return (new Vector3(0f, 0f, c.z + R4HalfZ), Vector3.back, Vector3.right, c.x - R4HalfX, c.x + R4HalfX);      // north
        }

        static readonly string[] OpenNames = { "Floor", "Ceiling", "Wall", "Pillar", "Mat", "Entry", "Hole", "Glow", "RoomLight", "Fill", "PlanetLamp", "CP_", "Showroom", "Drawing", "Poster", "Hub_" };

        static bool OpenThing(Collider col, Transform room)
        {
            for (var t = col.transform; t != null && t != room; t = t.parent)
                foreach (var n in OpenNames) if (t.name.StartsWith(n)) return true;
            return false;
        }

        /// <summary>The free stretch of playroom wall nearest the plan's spot for a door this wide and high with a clear floor
        /// <paramref name="depth"/> deep in front of it (no furniture, no ride, no beat). False if no wall has one.</summary>
        static bool FindDoorSpot(Transform r4, Vector3 pref, float width, float height, float depth, List<Vector3> taken, out Vector3 at, out Vector3 inward)
        {
            var keep = new[] { new Vector3(C4.x - R4HalfX, 0f, LaneZ) };   // the mouse hole in from the hallway
            var hole = r4.Find("WardrobeHole");
            var cands = new List<(float d, Vector3 p, Vector3 n)>();
            foreach (var wall in PlayroomWalls())
                for (float s = wall.from + width / 2f + 12f; s <= wall.to - width / 2f - 12f; s += 5f)
                {
                    Vector3 p = wall.face + wall.along * s; p.y = 0f;
                    cands.Add(((p - pref).magnitude, p, wall.inward));
                }
            foreach (var cand in cands.OrderBy(k => k.d))
            {
                Vector3 p = cand.p, n = cand.n;
                if (keep.Any(k => (k - p).magnitude < width / 2f + 40f)) continue;
                if (hole != null && (new Vector3(hole.position.x, 0f, hole.position.z) - p).magnitude < width / 2f + 50f) continue;
                if (taken.Any(k => (k - p).magnitude < width + 30f)) continue;
                Vector3 centre = p + n * (depth / 2f + 0.6f) + Vector3.up * (height / 2f + 0.6f);
                Vector3 half = new Vector3(width / 2f + 5f, height / 2f, depth / 2f);
                var rot = Quaternion.LookRotation(n);
                bool free = Physics.OverlapBox(centre, half, rot, ~0, QueryTriggerInteraction.Ignore).All(col => OpenThing(col, r4));
                if (!free) continue;
                at = p; inward = n; return true;
            }
            at = pref; inward = Vector3.right; return false;
        }

        /// <summary>A door prop (the pack's Door_01) stood against a wall face or on its own, its broad side to
        /// <paramref name="inward"/>, at most <paramref name="maxHeight"/> high. Returns its drawn bounds.</summary>
        static Bounds DoorProp(Transform parent, string name, Vector3 at, Vector3 inward, float maxHeight)
        {
            var door = Spawn(DoorPiece, name, parent, at, Vector3.zero, 1f);
            var b0 = B(door);
            if (b0.size.y > maxHeight) { door.transform.localScale *= maxHeight / b0.size.y; b0 = B(door); }
            // the thin side faces the room
            float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
            door.transform.rotation = Quaternion.Euler(0f, yaw + (b0.size.x < b0.size.z ? 90f : 0f), 0f);
            var b = B(door);
            float back = Vector3.Dot(b.center - at, inward) - Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(inward.x), 0f, Mathf.Abs(inward.z))));
            door.transform.position += -inward * back + new Vector3(at.x - b.center.x, 0f, at.z - b.center.z) - inward * Vector3.Dot(new Vector3(at.x - b.center.x, 0f, at.z - b.center.z), inward) + Vector3.up * (at.y - b.min.y);
            door.transform.position += inward * 0.3f;
            return B(door);
        }

        /// <summary>The HubDoor on its own object: a trigger over the door's front for E, the arrive point in front.</summary>
        static HubDoor HubDoorAt(Transform parent, string name, Bounds door, Vector3 inward, string id, string scene, string arriveAt, string prompt, Vector3 arrivePos, float arriveYaw)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            Vector3 along = Vector3.Cross(Vector3.up, inward);
            float width = Mathf.Abs(Vector3.Dot(door.size, new Vector3(Mathf.Abs(along.x), 0f, Mathf.Abs(along.z))));
            float front = Vector3.Dot(door.center, inward) + Mathf.Abs(Vector3.Dot(door.extents, new Vector3(Mathf.Abs(inward.x), 0f, Mathf.Abs(inward.z))));
            Vector3 foot = new Vector3(door.center.x, door.min.y, door.center.z);
            foot += inward * (front - Vector3.Dot(foot, inward));
            go.transform.SetPositionAndRotation(foot + inward * 1.5f + Vector3.up * (door.size.y / 2f), Quaternion.LookRotation(inward));
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(Mathf.Max(4f, width * 0.8f), door.size.y, 3f);
            var hub = go.AddComponent<HubDoor>();
            var arrive = Point("Arrive", go.transform, arrivePos, arriveYaw);
            Set(hub, "id", id); Set(hub, "scene", scene); Set(hub, "arriveAt", arriveAt); Set(hub, "prompt", prompt); Set(hub, "arrive", arrive);
            return hub;
        }

        static void PlayroomDoors(Transform r4)
        {
            var old = r4.Find("Hub_Doors"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = Child(r4, "Hub_Doors");
            Physics.SyncTransforms();
            // the door's own size, measured once
            var probe = Spawn(DoorPiece, "Hub_Probe", root, Vector3.zero, Vector3.zero, 1f); var pb = B(probe); Object.DestroyImmediate(probe);
            float scale = pb.size.y > 100f ? 100f / pb.size.y : 1f;
            float width = Mathf.Max(pb.size.x, pb.size.z) * scale, height = pb.size.y * scale;
            Log.AppendLine($"hub: {DoorPiece} is {pb.size.x:F1} x {pb.size.y:F1} x {pb.size.z:F1}; used {width:F1} wide, {height:F1} high");
            var taken = new List<Vector3>();
            foreach (var plan in Hubs)
            {
                if (!FindDoorSpot(r4, plan.pref, width + 30f, Mathf.Min(height, 90f), 44f, taken, out Vector3 at, out Vector3 inward))
                { Log.AppendLine($"hub: NO FREE WALL for {plan.id}; nothing placed"); continue; }
                taken.Add(at);
                var holder = Child(root, "Hub_" + plan.title.Replace(" ", ""));
                var door = DoorProp(holder, "Door", at, inward, 100f);
                Vector3 along = Vector3.Cross(Vector3.up, inward);
                Vector3 arrive = at + inward * 28f;
                var hub = HubDoorAt(holder, "HubDoor", door, inward, plan.id, plan.scene, plan.homeId, plan.prompt, arrive, Quaternion.LookRotation(inward).eulerAngles.y);
                // the name over the door for a kid, and at a toy's eye beside it
                Sign("Sign_Title", holder, new Vector3(door.center.x, door.max.y + 7f, door.center.z) + inward * (Vector3.Dot(door.extents, new Vector3(Mathf.Abs(inward.x), 0f, Mathf.Abs(inward.z))) + 0.4f), inward, plan.title, Mathf.Max(30f, width), 9f);
                float side = Mathf.Abs(Vector3.Dot(door.extents, new Vector3(Mathf.Abs(along.x), 0f, Mathf.Abs(along.z)))) + 3.5f;
                Sign("Sign_Toy", holder, at + along * side + inward * 0.4f + Vector3.up * 3.2f, inward, plan.title + "\n<b>E</b>  go in", 5f, 2.6f);
                // its dressing: the room it leads to, in the playroom's own props, either side of the door
                for (int i = 0; i < plan.dress.Length; i++)
                {
                    float sgn = i % 2 == 0 ? -1f : 1f; float step = side + 8f + (i / 2) * 9f;
                    var prop = Spawn(plan.dress[i], "Dress_" + plan.dress[i], holder, at + along * sgn * step + inward * 7f, new Vector3(0f, Random.Range(-25f, 25f) + Quaternion.LookRotation(inward).eulerAngles.y, 0f), 1f);
                    Rest(prop, 0f, null);
                }
                Physics.SyncTransforms();
                Log.AppendLine($"hub: {plan.id} door at {at} facing {inward} (door {door.size}), arrive {arrive}, -> {plan.scene} / {plan.homeId}");
            }
        }

        /// <summary>A room's door home: the pack's door standing on its own a few steps behind where the player starts, facing
        /// the start; coming back from the playroom puts you at the start.</summary>
        static void LabDoorHome(string scenePath, HubPlan plan)
        {
            EditorSceneManager.OpenScene(scenePath);
            var player = GameObject.Find("Player");
            if (player == null) { Log.AppendLine("hub: NO Player in " + scenePath); return; }
            var old = GameObject.Find("Hub_DoorHome"); if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Hub_DoorHome").transform;
            Physics.SyncTransforms();
            Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 start = player.transform.position;
            float ground = FloorUnder(start + Vector3.up * 2f, player.transform);
            Vector3 at = new Vector3(start.x, ground, start.z) - fwd * 14f;
            var door = DoorProp(root, "Door", at, fwd, 40f);
            Vector3 arrive = new Vector3(start.x, ground + 0.02f, start.z);
            var hub = HubDoorAt(root, "HubDoor", door, fwd, plan.homeId, "Greybox", plan.id, plan.homePrompt, arrive, Quaternion.LookRotation(fwd).eulerAngles.y);
            Vector3 along = Vector3.Cross(Vector3.up, fwd);
            float side = Mathf.Abs(Vector3.Dot(door.extents, new Vector3(Mathf.Abs(along.x), 0f, Mathf.Abs(along.z)))) + 3f;
            Sign("Sign_Home", root, at + along * side + fwd * 0.6f + Vector3.up * 3.2f, fwd, "THE PLAYROOM\n<b>E</b>  go back", 5f, 2.6f);
            var hits = Physics.OverlapBox(door.center, door.extents * 0.95f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Where(c => !c.transform.IsChildOf(root) && !c.name.StartsWith("Floor") && !c.name.StartsWith("Mat")).Select(c => c.name).Distinct().ToList();
            Log.AppendLine($"hub: {scenePath}: door home at {door.center} (size {door.size}), facing the start {start}; arrive at the start; overlaps: {(hits.Count == 0 ? "none" : string.Join(", ", hits))}");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        public static void LandHouse()
        {
            Log.Clear();
            EditorSceneManager.OpenScene(ScenePath);
            var course = GameObject.Find("Room/Course");
            var r3 = course != null ? course.transform.Find("R3_SkirtingRun") : null;
            var r4 = course != null ? course.transform.Find("R4_Playroom") : null;
            var entry = r4 != null ? r4.Find("Entry") : null;
            if (r3 == null || r4 == null || entry == null) { Debug.LogError("D-0090: no R3 / R4 / R4 Entry in " + ScenePath); EditorApplication.Exit(1); return; }
            Material dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/Greybox_Dark.mat");
            Material glowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/DoorGlow.mat");
            foreach (Transform t in r3.Cast<Transform>().ToArray()) Object.DestroyImmediate(t.gameObject);
            Transform r3Start = BuildHallway(r3, dark, glowMat);
            WireLane(r3, r3Start, r4, entry);
            var stand = GameObject.Find("Room/Bedroom/PistolStand"); if (stand != null) { Object.DestroyImmediate(stand); Log.AppendLine("R2: the bedroom pistol stand taken out"); }
            PlayroomDoors(r4);
            LightRooms();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            foreach (var plan in Hubs) LabDoorHome("Assets/_Game/Scenes/" + plan.scene + ".unity", plan);
            AssetDatabase.SaveAssets();
            Debug.Log("D-0090 HOUSE LANDED\n" + Log);
        }
    }
}
#endif
