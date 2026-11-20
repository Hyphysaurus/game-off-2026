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
