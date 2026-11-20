#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The vertical slice's fixes to the bedroom (R2), the window (R6) and the end card (D-0083 to D-0086). The bedroom
    /// was laid by hand-run code on 3 Nov and is not rebuilt by Build; this moves its pieces in place, from what it
    /// measures, so running it again changes nothing.
    /// - R2: the bed core sits on the mattress beside the step box and is used only from up there (ShiftSource's Stand
    ///   Within): a kid on the floor reached it (56 ahead, 60 up) and the bed's way down was skipped. The step box and
    ///   its three books slide east until the box's top meets the mattress's edge: there was a 2 wide slot to the floor
    ///   between them.
    /// - R2: the shelf switch takes a shot (a Damageable) and moves off the shelf to the bare wall just past it, where a
    ///   toy on the floor can see it; a dart pistol on its stand at the foot of the step books.
    /// - R6: core D is used only from the sill, so the toy is shrunk at the goal and not on the floor 41 below it.
    /// - The end card's second choice: the playroom (the Workshop).
    /// Batch: -executeMethod ShiftJam.CourseBuilder.LandSlice (-nographics is fine). Greybox saved.
    /// </summary>
    public static partial class CourseBuilder
    {
        /// <summary>How far below a core's middle the player's feet may be and still use it, for the cores that must be
        /// climbed to (the bed's, core D on the sill). A core's middle is 2.5 over what it sits on.</summary>
        const float CoreStandWithin = 6f;
        /// <summary>The bedroom switch's middle above the floor (D-0085).</summary>
        const float SwitchHeight = 22f;
        const string EndCardChoices = "R / A   play again          P / Y   the playroom is open";

        public static void LandSlice()
        {
            Log.Clear();
            EditorSceneManager.OpenScene(ScenePath);
            Physics.SyncTransforms();
            var bedroom = GameObject.Find("Room/Bedroom");
            if (bedroom == null) { Debug.LogError("D-0083: no Room/Bedroom in " + ScenePath); EditorApplication.Exit(1); return; }
            var room = bedroom.transform;
            SliceBedroom(room);
            Physics.SyncTransforms();
            SliceSwitch(room);

            var coreD = GameObject.Find("Room/Course/R6_Window/CoreD_Window");
            if (coreD != null) { Set(coreD.GetComponent<ShiftSource>(), "standWithin", CoreStandWithin); Log.AppendLine($"R6: core D at {coreD.transform.position} used from the sill only (within {CoreStandWithin})"); }
            else Log.AppendLine("R6: NO CoreD_Window");

            var hud = GameObject.Find("HUD"); var sub = hud != null ? hud.transform.Find("EndCard/Sub") : null;   // the card is switched off: Find misses it
            var subText = sub != null ? sub.GetComponent<TMPro.TMP_Text>() : null;
            if (subText != null) { subText.text = EndCardChoices; EditorUtility.SetDirty(subText); Log.AppendLine("end card: \"" + EndCardChoices + "\""); }
            else Log.AppendLine("end card: NO HUD/EndCard/Sub");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("D-0083 SLICE LANDED\n" + Log);
        }

        /// <summary>The top of the bed's own collider under (x, z), or NaN.</summary>
        static float BedTop(Transform bed, float x, float z)
        {
            foreach (var h in Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (h.collider.transform.IsChildOf(bed)) return h.point.y;
            return float.NaN;
        }

        static void SliceBedroom(Transform room)
        {
            var bed = room.Find("Bed"); var box = room.Find("StepBox");
            var books = Enumerable.Range(1, 3).Select(i => room.Find("StepBook_" + i)).Where(t => t != null).ToArray();
            var core = room.GetComponentsInChildren<ShiftSource>(true).FirstOrDefault();
            if (bed == null || box == null || core == null) { Log.AppendLine("R2: NO Bed / StepBox / bed core"); return; }

            // The mattress's west edge where the box stands: the first x (going east) where the bed is as high as the
            // box's top less 1, at the box's middle and 6 either side of it.
            var bb = B(box.gameObject);
            float want = bb.max.y - 1f, edge = float.NaN;
            for (float x = bb.max.x - 3f; x < bb.max.x + 10f; x += 0.1f)
            {
                bool all = true;
                foreach (float dz in new[] { -6f, 0f, 6f }) { float y = BedTop(bed, x, bb.center.z + dz); if (float.IsNaN(y) || y < want) { all = false; break; } }
                if (all) { edge = x; break; }
            }
            if (float.IsNaN(edge)) { Log.AppendLine($"R2: the mattress's edge not found east of the box ({bb.max.x:F1}); box left as it was"); }
            else
            {
                // The box's top reaches just under the mattress's rounded edge: no slot to the floor between them.
                float delta = edge + 0.2f - bb.max.x;
                box.position += Vector3.right * delta;
                foreach (var b in books) b.position += Vector3.right * delta;
                Log.AppendLine($"R2: mattress edge at x {edge:F1} (bed {want + 1f:F1} high there); step box and its {books.Length} books moved {delta:F2} east: box x {bb.min.x + delta:F1}..{bb.max.x + delta:F1}, top {bb.max.y:F1}");
            }
            Physics.SyncTransforms();

            // The core on the mattress, 14 in from that edge, level with the box: the toy it makes walks off onto the box.
            bb = B(box.gameObject);
            float cx = (float.IsNaN(edge) ? bb.max.x : edge) + 14f, cz = bb.center.z;
            float top = BedTop(bed, cx, cz);
            if (float.IsNaN(top)) { Log.AppendLine($"R2: no mattress at ({cx:F1}, {cz:F1}); core left at {core.transform.position}"); }
            else
            {
                core.transform.position = new Vector3(cx, top + 2.5f, cz);
                Log.AppendLine($"R2: bed core {core.name} now at {core.transform.position} (mattress {top:F1}), used from the bed only (within {CoreStandWithin})");
            }
            Set(core, "standWithin", CoreStandWithin);
            EditorUtility.SetDirty(core.transform);
            EditorUtility.SetDirty(box); foreach (var b in books) EditorUtility.SetDirty(b);

            // A dart pistol on its stand (the playroom's piece) at the foot of the step books, where the toy comes down.
            var old = room.Find("PistolStand"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var pistol = AssetDatabase.LoadAssetAtPath<GameObject>(PistolPiece);
            if (pistol == null) { Log.AppendLine("R2: NO " + PistolPiece); return; }
            var lowBooks = books.Length > 0 ? books.Select(b => B(b.gameObject)).Aggregate((a, c) => { a.Encapsulate(c); return a; }) : bb;
            var stand = (GameObject)PrefabUtility.InstantiatePrefab(pistol, room); stand.name = "PistolStand";
            PlaceFree(stand, new Vector3(lowBooks.min.x - 6f, 0f, lowBooks.min.z - 8f), Quaternion.LookRotation(Vector3.right));
            Log.AppendLine($"R2: pistol stand at {stand.transform.position}");
        }

        static void SliceSwitch(Transform room)
        {
            var sw = room.GetComponentsInChildren<Switch>(true).FirstOrDefault(); var shelf = room.Find("Shelf");
            if (sw == null || shelf == null) { Log.AppendLine("R2: NO switch / Shelf"); return; }
            // Off the shelf onto the bare wall just past its north end, and lower (its middle 22 up, not 56): over the
            // shelf a shot from the floor had to come from 105 away to clear the shelf's top, and the third-person
            // camera looks up 25 degrees at most, so a toy's dart tops out about 36 up however far back it stands.
            // At 22 a toy hits it from 60 to 90 away; it is still far over a toy's reach (1.7), and a kid reaches it.
            var sb = B(shelf.gameObject); var wb = B(sw.gameObject);
            float z = sb.max.z + 4f + wb.extents.z + 6f;
            sw.transform.position += new Vector3(0f, SwitchHeight - wb.center.y, z - wb.center.z);
            EditorUtility.SetDirty(sw.transform);
            var health = sw.GetComponent<Damageable>(); if (health == null) health = sw.gameObject.AddComponent<Damageable>();
            Set(health, "takeKnockback", false);
            Set(health, "maxHealth", 100000f);   // it never runs out: every dart presses (once pressed, it stays Done)
            Log.AppendLine($"R2: switch {sw.name} moved to {B(sw.gameObject).center} (the shelf ends at z {sb.max.z:F1}); a shot presses it (Damageable)");
        }
    }
}
#endif
