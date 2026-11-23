#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// D-0151 (systems plan step 8): the hallway's dark stretch, made only of parts, placed once into Greybox; after that the
    /// scene is the truth (move them in the editor). Safe to run again: it replaces what it placed.
    /// - A toy-sized flashlight (the Flashlight prefab, Made At Toy) on the floor past the darts lesson, with a tape sign.
    /// - A DarkZone from the soldier to the tall end (the lantern kept lit, KeepLit; checkpoints and embers stay lit).
    /// - The hall soldier: a LightTarget at its chest, On Filled -> Dazzle (focus the light, then shoot: Alan Wake 2).
    /// - The charge core starts dormant: a LightTarget wakes it (On Filled -> Awaken); its sign says so.
    /// Menu: Shift Jam / House / Land Hallway Light Beats (D-0151).
    /// </summary>
    public static partial class CourseBuilder
    {
        const float HallLightX = 98f;                     // the flashlight on the floor, past the darts checkpoint (95)
        const string ShiftSettingsPath = "Assets/_Game/Data/ShiftSettings.asset";

        [MenuItem("Shift Jam/House/Land Hallway Light Beats (D-0151)")]
        public static void LandHallwayLight()
        {
            Log.Clear();
            var active = SceneManager.GetActiveScene();
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            var r3 = GameObject.Find("Room/Course/R3_SkirtingRun");
            if (r3 == null) { Debug.LogError("D-0151: no R3_SkirtingRun in " + ScenePath); return; }
            Transform room = r3.transform;

            // 1. The light, toy-sized, and its sign.
            var torchAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPrefabBuilder.PrefabPath);
            var shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftSettingsPath);
            if (torchAsset == null || shift == null) { Debug.LogError("D-0151: no Flashlight prefab (run Make Flashlight Prefab) or no ShiftSettings"); return; }
            float toy = shift.kidScale > 0f ? shift.toyScale / shift.kidScale : 1f;
            Remove(room, "HallFlashlight");
            var torch = (GameObject)PrefabUtility.InstantiatePrefab(torchAsset, room);
            torch.name = "HallFlashlight";
            torch.transform.localScale = Vector3.one * toy;
            torch.transform.SetPositionAndRotation(new Vector3(HallLightX, 0f, -146.5f), Quaternion.Euler(0f, 205f, 0f));
            Bounds tb = B(torch);
            torch.transform.position += Vector3.up * (0.005f - tb.min.y);   // lying on the floor (its top is y 0)
            var cso = new SerializedObject(torch.GetComponent<Carryable>());
            cso.FindProperty("madeAt").enumValueIndex = (int)Carryable.MadeAt.Toy;
            cso.ApplyModifiedPropertiesWithoutUndo();
            var fso = new SerializedObject(torch.GetComponent<Flashlight>());
            fso.FindProperty("startOn").boolValue = false;
            fso.ApplyModifiedPropertiesWithoutUndo();
            Log.AppendLine($"HallFlashlight: toy-sized ({toy:F4}) at {torch.transform.position}, {B(torch).size}");
            Remove(room, "Sign2_Light");
            TapeSign("Sign2_Light", room, new Vector3(HallLightX + 4f, 3.2f, -140.15f), Vector3.back,
                "A light!  <b>E</b> take it   <b>L</b> on / off\n<b>Right click</b> focus: it blinds and wakes things", 9f, 3f);

            // 2. The dark stretch: the soldier to the tall end, down into the gap's shaft. The lantern stays lit.
            Remove(room, "DarkStretch");
            var dark = new GameObject("DarkStretch");
            dark.transform.SetParent(room, true);
            dark.transform.SetPositionAndRotation(new Vector3(173f, -8f, -150f), Quaternion.identity);
            var zone = dark.AddComponent<DarkZone>();
            var zso = new SerializedObject(zone);
            zso.FindProperty("centre").vector3Value = Vector3.zero;
            zso.FindProperty("size").vector3Value = new Vector3(122f, 50f, 26f);   // x 112..234, y -33..17, z -163..-137
            zso.ApplyModifiedPropertiesWithoutUndo();
            Transform lantern = room.Find("LanternLight");
            if (lantern != null && lantern.GetComponent<KeepLit>() == null) lantern.gameObject.AddComponent<KeepLit>();
            Log.AppendLine($"DarkStretch: x 112..234; lantern {(lantern != null ? "kept lit" : "NOT FOUND")}");

            // 3. The soldier: focus the light on it, it is blinded.
            var soldier = room.Find("HallSoldier") != null ? room.Find("HallSoldier").GetComponent<ToySoldier>() : null;
            if (soldier != null)
            {
                Transform chest = soldier.transform.Find("LightPoint");
                if (chest == null) { chest = new GameObject("LightPoint").transform; chest.SetParent(soldier.transform, false); }
                Bounds sb = B(soldier.gameObject);
                chest.position = new Vector3(sb.center.x, sb.min.y + sb.size.y * 0.65f, sb.center.z);
                var target = Target(soldier.gameObject, chest, 0.6f, LightTarget.FocusRule.FasterFocused, 0.5f, true);
                UnityEventTools.AddVoidPersistentListener(target.OnFilled, soldier.Dazzle);
                Log.AppendLine("HallSoldier: LightTarget at its chest, On Filled -> Dazzle");
            }
            else Log.AppendLine("NO HallSoldier");

            // 4. The charge core sleeps until lit.
            var coreT = room.Find("ChargeCore");
            var core = coreT != null ? coreT.GetComponent<ShiftSource>() : null;
            if (core != null)
            {
                var so = new SerializedObject(core);
                so.FindProperty("startDormant").boolValue = true;
                so.FindProperty("dormantPrompt").stringValue = "Dark. Shine your light on it.";
                so.ApplyModifiedPropertiesWithoutUndo();
                Transform middle = coreT.Find("LightPoint");
                if (middle == null) { middle = new GameObject("LightPoint").transform; middle.SetParent(coreT, false); }
                middle.position = B(coreT.gameObject).center;
                var target = Target(coreT.gameObject, middle, 0.5f, LightTarget.FocusRule.Any, 0f, false);
                UnityEventTools.AddVoidPersistentListener(target.OnFilled, core.Awaken);
                Log.AppendLine("ChargeCore: starts dormant; LightTarget, On Filled -> Awaken");
                Transform sign = room.Find("Sign3_Charge");
                if (sign != null)
                {
                    Vector3 at = sign.position, toViewer = -sign.forward; int index = sign.GetSiblingIndex();
                    Object.DestroyImmediate(sign.gameObject);
                    TapeSign("Sign3_Charge", room, at, toViewer, "A core fills your <b>shift charge</b>.\nDark? <b>Shine your light</b> on it.\n<b>F</b>  shift anywhere there is room.", 8f, 3.6f).transform.SetSiblingIndex(index);
                }
            }
            else Log.AppendLine("NO ChargeCore");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("HALLWAY LIGHT BEATS (D-0151)\n" + Log);
        }

        /// <summary>A LightTarget on <paramref name="go"/> set up as given, its old wiring cleared (so a re-run does not stack it).</summary>
        static LightTarget Target(GameObject go, Transform point, float fill, LightTarget.FocusRule rule, float drain, bool repeatable)
        {
            if (!go.TryGetComponent(out LightTarget target)) target = go.AddComponent<LightTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("fillSeconds").floatValue = fill;
            so.FindProperty("focus").enumValueIndex = (int)rule;
            so.FindProperty("drainSeconds").floatValue = drain;
            so.FindProperty("repeatable").boolValue = repeatable;
            so.FindProperty("point").objectReferenceValue = point;
            so.ApplyModifiedPropertiesWithoutUndo();
            while (target.OnFilled.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(target.OnFilled, 0);
            return target;
        }

        static void Remove(Transform parent, string name)
        {
            for (Transform t = parent.Find(name); t != null; t = parent.Find(name)) Object.DestroyImmediate(t.gameObject);
        }
    }
}
#endif
