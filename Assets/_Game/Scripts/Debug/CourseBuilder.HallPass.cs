#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// D-0099, his 20 Nov playtest notes, landed in place (nothing else in the hallway is rebuilt):
    /// - Every sign in the tutorial hallway is marker on packing tape (Lettering, D-0098), not a printed card.
    /// - The way out of the tall end is the bedroom's own puzzle again: a toy doorway in the end wall on the shelf, its door
    ///   shut, a kid-size switch on the shelf's front that opens it (by hand, or by a dart: the switch's Damageable), the
    ///   door glow and light behind it. Copied from the bedroom's pieces, so both doors look and work the same.
    /// - A hub door's name over the door is slid clear of the playroom's props (the RACEWAY card ran through the hoop).
    /// - The Expressway's guards go on the CarGuard layer (shots and the camera pass through them; cars still bump them).
    /// The full builders (LandHouse, the Expressway) do the same on a rebuild. Menu: Shift Jam / House / Land Hallway Pass.
    /// Batch: -executeMethod ShiftJam.CourseBuilder.LandHallwayPass.
    /// </summary>
    public static partial class CourseBuilder
    {
        const float ExitSwitchZ = -178f;   // the switch on the shelf's front: south of the shelf core (z -162) and the door (z -150)
        const float ExitSwitchUp = 22f;    // its middle above the floor, as the bedroom's (D-0085): a kid's hand

        [MenuItem("Shift Jam/House/Land Hallway Pass (D-0099)")]
        public static void LandHallwayPass()
        {
            Log.Clear();
            var active = SceneManager.GetActiveScene();
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            var r3 = GameObject.Find("Room/Course/R3_SkirtingRun");
            var r4 = GameObject.Find("Room/Course/R4_Playroom");
            if (r3 == null || r4 == null) { Debug.LogError("D-0099: no R3 / R4 in " + ScenePath); if (Application.isBatchMode) EditorApplication.Exit(1); return; }

            EnsureLayer(Layers.CarGuardName);
            int guards = 0;
            foreach (var box in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (box.name.StartsWith("Guard") && box.GetComponent<Renderer>() == null && box.transform.IsChildOf(r4.transform)) { GuardLayer(box.gameObject); guards++; }
            Log.AppendLine($"{guards} Expressway guards on the {Layers.CarGuardName} layer ({Layers.CarGuard})");

            MarkerSigns(r3.transform);
            HallLessonFixes(r3.transform);
            HallDressing(r3.transform);
            var exit = r3.transform.Find("ExitPortal");
            if (exit != null) HallExitDoor(r3.transform, exit.gameObject); else Log.AppendLine("NO ExitPortal in R3: run LandHouse");
            var doors = r4.transform.Find("Hub_Doors");
            if (doors != null)
                foreach (Transform holder in doors)
                {
                    var sign = holder.Find("Sign_Title"); var door = holder.Find("Door");
                    if (sign == null || door == null) continue;
                    ClearOfProps(sign.gameObject, r4.transform, holder);
                }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("D-0099 HALLWAY PASS LANDED\n" + Log);
        }

        /// <summary>The layer, made in the first free user slot if the project has none of that name.</summary>
        static void EnsureLayer(string name)
        {
            if (LayerMask.NameToLayer(name) >= 0) return;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tags.ApplyModifiedPropertiesWithoutUndo();
                Log.AppendLine($"layer {i} is now {name}");
                return;
            }
            Log.AppendLine("NO FREE LAYER for " + name);
        }

        /// <summary>Every card sign straight under <paramref name="room"/> becomes marker on tape, same place, size and words.</summary>
        static void MarkerSigns(Transform room)
        {
            foreach (Transform t in room.Cast<Transform>().ToArray())
            {
                if (!t.name.StartsWith("Sign")) continue;
                var tmp = t.GetComponentInChildren<TextMeshPro>(true);
                if (tmp == null) continue;   // already tape
                Vector3 at = t.position, toViewer = -t.forward;
                Vector2 size = tmp.rectTransform.sizeDelta;
                string words = t.name == "Sign3_GrowKid" ? GrowKidWords : tmp.text;
                string name = t.name; int index = t.GetSiblingIndex();
                Object.DestroyImmediate(t.gameObject);
                TapeSign(name, room, at, toViewer, words, size.x, size.y).transform.SetSiblingIndex(index);
                Log.AppendLine($"{name}: tape ({size.x:F0} x {size.y:F0}) \"{Regex.Replace(words, "<[^>]+>", "").Replace("\n", " / ")}\"");
            }
        }

        /// <summary>The way out of the tall end, as the bedroom's: a toy doorway against the end wall on the shelf with its
        /// door shut, the glow and light behind it, <paramref name="exit"/> (the Portal) in the doorway behind the door, and
        /// a kid's switch on the shelf's front that opens the door. Copies of the bedroom's pieces. Idempotent.</summary>
        static void HallExitDoor(Transform r3, GameObject exit)
        {
            foreach (var n in new[] { "ExitGlow", "ExitDoorway", "ExitDoorGlow", "ExitDoorLight", "ExitSwitch" })
            { var old = r3.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }

            var bed = GameObject.Find("Room/Bedroom");
            var doorway = bed != null ? bed.transform.Find("ToyDoorway") : null;
            var goal = bed != null ? bed.transform.Find("Goal") : null;
            var sw = bed != null ? bed.transform.Find("ShelfSwitch") : null;
            var wallE = r3.Find("TallWallE"); var shelf = r3.Find("TallShelf");
            if (doorway == null || goal == null || sw == null || wallE == null || shelf == null)
            { Log.AppendLine($"EXIT DOOR NOT MADE: doorway {doorway != null}, goal {goal != null}, switch {sw != null}, end wall {wallE != null}, shelf {shelf != null}"); return; }

            // the bedroom's frame: at its doorway, facing into the bedroom; the hallway's: at the end wall on the shelf, facing the shaft
            Vector3 bedIn = bed.transform.position - doorway.position; bedIn.y = 0f;
            bedIn = Mathf.Abs(bedIn.x) > Mathf.Abs(bedIn.z) ? new Vector3(Mathf.Sign(bedIn.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(bedIn.z));
            Quaternion fromRot = Quaternion.LookRotation(bedIn); Vector3 fromPos = doorway.position;
            Bounds wb = wallE.GetComponent<Renderer>().bounds, sb = shelf.GetComponent<Renderer>().bounds;
            Quaternion toRot = Quaternion.LookRotation(Vector3.left);
            Vector3 toPos = new Vector3(wb.min.x, sb.max.y, HallHoleZ);
            Vector3 Map(Vector3 p) => toPos + toRot * (Quaternion.Inverse(fromRot) * (p - fromPos));
            Quaternion MapRot(Quaternion q) => toRot * Quaternion.Inverse(fromRot) * q;
            // The bedroom's doorway stands out from its wall; its glow is the piece nearest the wall: that glow lands
            // just in front of the end wall's face, and the rest comes with it.
            var bedGlow = bed.transform.Find("DoorGlow");
            // The face is the first solid thing a ray from the shaft meets at the door's height: the playroom's west wall
            // (Wall_W, x 280) stands a unit in front of the hallway's own end wall, and hid the old glowing hole.
            float face = wb.min.x;
            Physics.SyncTransforms();
            var ray = new Ray(new Vector3(sb.min.x + 1f, sb.max.y + 1.8f, HallHoleZ), Vector3.right);
            var hits = Physics.RaycastAll(ray, wb.max.x - ray.origin.x + 1f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !h.collider.transform.IsChildOf(r3) || h.collider.transform == wallE).OrderBy(h => h.distance).ToArray();
            if (hits.Length > 0) { face = hits[0].point.x; Log.AppendLine($"exit: the end face is {hits[0].collider.name} at x {face:F2}"); }
            if (bedGlow != null) toPos.x -= Map(bedGlow.position).x - (face - 0.05f);
            GameObject Copy(Transform src, string name)
            {
                var c = Object.Instantiate(src.gameObject, r3, true); c.name = name;
                c.transform.SetPositionAndRotation(Map(src.position), MapRot(src.rotation));
                return c;
            }

            var newDoorway = Copy(doorway, "ExitDoorway");
            if (bedGlow != null) Copy(bedGlow, "ExitDoorGlow");
            var bedLight = bed.transform.Find("DoorLight"); if (bedLight != null) Copy(bedLight, "ExitDoorLight");
            var door = newDoorway.GetComponentInChildren<Door>(true);
            var hint = newDoorway.GetComponentInChildren<ProblemHint>(true);
            if (hint != null) Log.AppendLine($"exit door's hint: \"{new SerializedObject(hint).FindProperty("message").stringValue}\" (door {(new SerializedObject(hint).FindProperty("door").objectReferenceValue == door ? "this one" : "NOT this one")})");

            // the Portal where the bedroom's Goal is: in the doorway, behind the door
            exit.transform.SetPositionAndRotation(Map(goal.position), MapRot(goal.rotation));
            var gb = goal.GetComponent<BoxCollider>(); var eb = exit.GetComponent<BoxCollider>();
            if (gb != null && eb != null) { eb.center = gb.center; eb.size = Vector3.Scale(gb.size, goal.lossyScale); eb.isTrigger = true; }
            exit.transform.localScale = Vector3.one;
            // the old warm light, now in front of the door so it reads from the shaft's floor
            var beacon = r3.Find("ExitLight"); if (beacon != null) beacon.position = toPos + Vector3.left * 5f + Vector3.up * 4f;

            // the switch: on the shelf's front, facing the shaft, the bedroom's height
            var newSwitch = Object.Instantiate(sw.gameObject, r3, true); newSwitch.name = "ExitSwitch";
            newSwitch.transform.rotation = Quaternion.LookRotation(Vector3.left) * Quaternion.Inverse(Quaternion.LookRotation(Flat(sw.forward))) * sw.rotation;
            newSwitch.transform.position = new Vector3(sb.min.x - 1f, ExitSwitchUp, ExitSwitchZ);
            Physics.SyncTransforms();
            var swb = B(newSwitch);
            newSwitch.transform.position += new Vector3(sb.min.x - swb.max.x, ExitSwitchUp - swb.center.y, 0f);   // its back flat on the shelf
            var so = new SerializedObject(newSwitch.GetComponent<Switch>());
            var list = so.FindProperty("doors"); list.arraySize = 1; list.GetArrayElementAtIndex(0).objectReferenceValue = door;
            so.ApplyModifiedPropertiesWithoutUndo();
            Log.AppendLine($"exit: toy doorway at {toPos} facing the shaft, door {(door != null ? "shut" : "MISSING")}, portal box {eb?.size}, switch at {newSwitch.transform.position} (kid, or a dart)");
        }

        /// <summary>D-0100, his second walk: the book stairs' top books no longer share the box's top face (they flickered),
        /// the pistol on its stand and the ammo box glint (and the stand has its own light), and the soldier that "shoots
        /// back" stands guard facing the way the toy comes, sees and fires further, and takes about eight darts.</summary>
        static void HallLessonFixes(Transform r3)
        {
            var stairs = r3.Find("UpStairs"); var box = r3.Find("StepBox");
            if (stairs != null && box != null) SinkTopBooks(stairs, box);
            if (stairs != null) StaggerBookSides(stairs);

            TallEndCharger(r3);
            var glint = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/PuzzleGlint.mat");
            var stand = r3.Find("PistolStand");
            if (stand != null)
            {
                Glint(stand.gameObject, glint, new Color(1f, 0.82f, 0.35f, 1f), 1.2f, 2.2f);
                var old = stand.Find("StandLight"); if (old != null) Object.DestroyImmediate(old.gameObject);
                var b = B(stand.gameObject);
                PointLight("StandLight", stand, new Vector3(b.center.x, b.max.y + 2.5f, b.center.z), 10f, 12f, new Color(1f, 0.86f, 0.62f));
                Log.AppendLine($"pistol stand: glint and light over {b.center}");
            }
            var ammoBox = r3.Find("HallAmmo");
            if (ammoBox != null) Glint(ammoBox.gameObject, glint, new Color(1f, 0.82f, 0.35f, 0.55f), 0.9f, 1.6f);

            var soldier = r3.Find("HallSoldier");
            if (soldier != null)
            {
                var ts = soldier.GetComponent<ToySoldier>();
                Set(ts, "wanderRadius", 0f); Set(ts, "sightRange", 32f); Set(ts, "fireRange", 28f);
                soldier.rotation = Quaternion.LookRotation(Vector3.left);   // facing the way the toy comes
                var hp = soldier.GetComponent<Damageable>(); if (hp != null) Set(hp, "maxHealth", 120f);
                Log.AppendLine("hall soldier: on guard facing west, sight 32, fire 28, health 120");
            }
        }

        // ---- the hallway's dressing (D-0100): things lost behind the skirting, from the Playroom pack at its own scale (giant
        // to the toy), clustered against the walls in the stretches between the lessons. x, the wall (-1 south, +1 north),
        // and what lies there. Never on the walking line, a drop, a sign or a lesson's piece.
        static readonly (float x, int wall, string[] props)[] HallClutter =
        {
            (-52f, -1, new[] { "Pencil_01", "Eraser_02", "Bottle_Cap_01", "Constructor_07" }),
            (-38f, -1, new[] { "Chalk_02", "Puzzle_07", "Office_Button_01" }),
            (28f, -1, new[] { "Car_02", "Bottle_Cap_02", "Keyboard_Key_12", "Match_01" }),
            (36f, 1, new[] { "Marker_Red_01", "Marker_Red_01_Cap", "Chess_White_Pawn_01" }),
            (46f, -1, new[] { "Paper_Plane_01", "Note_Paper_02", "Constructor_12" }),
            (80f, 1, new[] { "Battery_03", "Clip_02", "Sharpener_01" }),
            (98f, 1, new[] { "Color_Cube_01", "Chess_Black_Hoss_01", "Puzzle_03" }),
            (120f, -1, new[] { "Wooden_Sphere_01", "Pencil_02", "PlayCard_01", "Constructor_03" }),
            (150f, -1, new[] { "Matchbox_01", "Match_02", "Eraser_01", "Toy_Tree_01" }),
            (214f, -1, new[] { "TapeRoll_03", "Chalk_04", "Bottle_Cap_01", "Battery_01" }),
            (222f, 1, new[] { "Toy_Sign_01", "Keyboard_Key_74", "Car_01" }),
            (238f, -1, new[] { "Wooden_Rocket_01", "Wooden_Cube_02", "Wooden_Cylinder_01", "Ruler_Big_02" }),   // the shaft's floor: a kid's size
        };

        static void HallDressing(Transform r3)
        {
            var old = r3.Find("Dressing"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Dressing").transform; root.SetParent(r3, false);
            Physics.SyncTransforms();
            var rng = new System.Random(20);
            float zs = LaneZ - HallW / 2f, zn = LaneZ + HallW / 2f;
            int placed = 0, skipped = 0;
            foreach (var (x, wall, props) in HallClutter)
            {
                bool shaft = x >= HallTall0;
                float wallZ = shaft ? HallTallZ0 : (wall < 0 ? zs : zn);
                float along = 0f;
                foreach (var name in props)
                {
                    var go = Spawn(name, "Lost_" + name, root, new Vector3(x + along, 30f, wallZ), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), 1f);
                    if (go.name.EndsWith("_MISSING")) { Object.DestroyImmediate(go); skipped++; continue; }
                    foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = false;   // loose: darts knock them about
                    bool ok = false; string why = "";
                    for (int attempt = 0; attempt < 4 && !ok; attempt++)
                    {
                        var b = B(go);
                        // against its wall: the near side 0.4 off the wall's face, the far side never past 4 into the hallway
                        float inward = wall < 0 || shaft ? 1f : -1f;
                        float near = inward > 0 ? b.min.z : b.max.z;
                        go.transform.position += Vector3.forward * ((wallZ + inward * (0.4f + (float)rng.NextDouble() * 0.8f)) - near);
                        b = B(go);
                        if (!shaft && b.size.z > 3.5f)
                        {
                            // long things lie along the wall, a little askew
                            go.transform.rotation = Quaternion.Euler(0f, 90f + ((float)rng.NextDouble() * 30f - 15f), 0f) * Quaternion.Euler(0f, go.transform.eulerAngles.y > 180f ? 180f : 0f, 0f);
                            b = B(go); near = inward > 0 ? b.min.z : b.max.z;
                            go.transform.position += Vector3.forward * ((wallZ + inward * 0.5f) - near);
                            b = B(go);
                        }
                        if (!shaft && b.size.z > 4.5f) { why = $"too deep {b.size.z:F1}"; break; }   // too deep to keep off the walking line
                        // the floor under it (the drops have none at 0)
                        var hitDown = Physics.RaycastAll(new Vector3(b.center.x, shaft ? 100f : HallH - 1.5f, b.center.z), Vector3.down, shaft ? 140f : 40f, ~0, QueryTriggerInteraction.Ignore)
                            .Where(h => !h.collider.transform.IsChildOf(go.transform)).OrderBy(h => h.distance).ToArray();
                        if (hitDown.Length == 0 || hitDown[0].point.y < -0.5f || hitDown[0].point.y > 0.5f) { why = hitDown.Length == 0 ? "no floor" : $"floor {hitDown[0].collider.name} at {hitDown[0].point.y:F2}"; go.transform.position += Vector3.right * 2.5f; continue; }
                        Rest(go, hitDown[0].point.y + 0.01f, null);
                        b = B(go); Physics.SyncTransforms();
                        var inWay = Physics.OverlapBox(b.center, b.extents * 0.95f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                            .Where(c => !c.transform.IsChildOf(go.transform) && Mathf.Max(c.bounds.size.x, c.bounds.size.z) < 30f).ToArray();
                        if (inWay.Length == 0) ok = true;
                        else { why = "in the way: " + string.Join(",", inWay.Select(c => c.name).Distinct().Take(3)); go.transform.position += Vector3.right * 2.5f; }
                    }
                    if (!ok) { if (skipped < 12) Log.AppendLine($"  skipped {name} at x {x}: {why}"); Object.DestroyImmediate(go); skipped++; continue; }
                    placed++;
                    along += Mathf.Max(1.5f, B(go).size.x) + 0.6f + (float)rng.NextDouble();
                    Physics.SyncTransforms();
                }
            }
            Log.AppendLine($"hallway dressing: {placed} lost things placed, {skipped} skipped (no floor, in the way, or too deep)");
        }

        /// <summary>Every book of <paramref name="stairs"/> whose top lies on <paramref name="box"/>'s top (inside it) goes
        /// 3 cm down: two faces at one height flicker.</summary>
        static void SinkTopBooks(Transform stairs, Transform box)
        {
            float top = B(box.gameObject).max.y; int n = 0;
            foreach (Transform book in stairs)
            {
                var r = book.GetComponentInChildren<Renderer>(); if (r == null) continue;
                if (Mathf.Abs(r.bounds.max.y - top) > 0.01f) continue;
                book.position += Vector3.down * 0.03f; n++;
            }
            Log.AppendLine($"{stairs.name}: {n} books sunk under the box's top ({top:F3})");
        }

        /// <summary>A second charge-only core at the tall end's mouth (D-0103): the way back from the playroom lands past
        /// the bridge, where the hallway's own core (x 162) is behind the toy, and growing in the shaft wants a charge.</summary>
        static void TallEndCharger(Transform r3)
        {
            var old = r3.Find("ChargeCoreTall"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var coreMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/ShiftCore.mat");
            var c = Core("ChargeCoreTall", r3, new Vector3(HallTall0 - 6f, 2.5f, LaneZ - 5f), ShiftSource.Mode.ChargeOnly, coreMat, false);
            Log.AppendLine($"tall end: a charge core at {c.transform.position} (unlimited)");
        }

        /// <summary>The stair books are one depth and lie in a row, each over the next: their long sides shared a plane and
        /// flickered. Any book whose side lies within 1 cm of an overlapping book's moves 4 cm off it (alternately each
        /// way), until none do. Idempotent: a row already apart is left alone.</summary>
        static void StaggerBookSides(Transform stairs)
        {
            var books = stairs.Cast<Transform>().Where(t => t.GetComponentInChildren<Renderer>() != null).ToList();
            int moved = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                bool any = false;
                Physics.SyncTransforms();
                for (int i = 1; i < books.Count; i++)
                {
                    var bi = books[i].GetComponentInChildren<Renderer>().bounds;
                    for (int j = 0; j < i; j++)
                    {
                        var bj = books[j].GetComponentInChildren<Renderer>().bounds;
                        bool overlap = bi.min.x < bj.max.x && bj.min.x < bi.max.x && bi.min.y < bj.max.y && bj.min.y < bi.max.y;
                        bool shared = Mathf.Abs(bi.min.z - bj.min.z) < 0.01f || Mathf.Abs(bi.max.z - bj.max.z) < 0.01f;
                        if (!overlap || !shared) continue;
                        books[i].position += Vector3.forward * (i % 2 == 0 ? 0.04f : -0.04f) * (1 + pass);
                        moved++; any = true;
                        break;
                    }
                }
                if (!any) break;
            }
            Log.AppendLine($"{stairs.name}: {moved} book moves so no two overlapping books share a side");
        }

        static void Glint(GameObject on, Material material, Color color, float min, float max)
        {
            var h = on.GetComponent<Highlight>(); if (h == null) h = on.AddComponent<Highlight>();
            var so = new SerializedObject(h);
            so.FindProperty("material").objectReferenceValue = material;
            so.FindProperty("color").colorValue = color;
            so.FindProperty("minSize").floatValue = min;
            so.FindProperty("maxSize").floatValue = max;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

        /// <summary>Slides a sign along its wall, or up it, until it runs through none of the room's props (not its own
        /// <paramref name="own"/> pieces, not the room's shell). The nearest clear place wins.</summary>
        static void ClearOfProps(GameObject sign, Transform room, Transform own)
        {
            var card = sign.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Card" || r.name == "Tape");
            if (card == null) return;
            Physics.SyncTransforms();
            var props = room.GetComponentsInChildren<Renderer>()
                .Where(r => r.enabled && !r.transform.IsChildOf(own) && !(r is ParticleSystemRenderer) && !OpenNames.Any(n => r.name.StartsWith(n) || (r.transform.parent != null && r.transform.parent.name.StartsWith(n)))
                            && r.bounds.size.x < 80f && r.bounds.size.z < 80f)
                .ToArray();
            Vector3 home = sign.transform.position, along = sign.transform.right;
            Bounds At(Vector3 shift) { var b = card.bounds; b.center += shift; b.Expand(0.6f); return b; }
            string Hit(Vector3 shift) { var b = At(shift); var r = props.FirstOrDefault(p => p.bounds.Intersects(b)); return r != null ? r.name : null; }
            string first = Hit(Vector3.zero);
            if (first == null) { Log.AppendLine($"{own.name}/{sign.name}: clear where it is"); return; }
            Vector3 best = Vector3.zero; float bestCost = float.MaxValue;
            for (float up = 0f; up <= 24f; up += 3f)
                for (float side = 0f; side <= 40f; side += 2f)
                    foreach (float dir in new[] { 1f, -1f })
                    {
                        Vector3 shift = along * side * dir + Vector3.up * up;
                        float cost = side + up * 1.5f;
                        if (cost >= bestCost || Hit(shift) != null) continue;
                        best = shift; bestCost = cost;
                    }
            if (bestCost == float.MaxValue) { Log.AppendLine($"{own.name}/{sign.name}: runs into {first}, and no clear place within 40 along or 24 up"); return; }
            sign.transform.position = home + best;
            Log.AppendLine($"{own.name}/{sign.name}: ran into {first}; moved {Vector3.Dot(best, along):F0} along, {best.y:F0} up");
        }
    }
}
#endif
