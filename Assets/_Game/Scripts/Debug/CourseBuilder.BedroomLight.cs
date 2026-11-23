#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// D-0155, his "what else we can intro in the very first bedroom... wasted potential", then "proceed with 1 and 2".
    /// Made only of parts, placed once into Greybox (then the scene is the truth). Safe to run again.
    /// 1. The toy box: its grow core starts dark; the giant flashlight lying lit beside the toy is pushable (a physics body
    ///    held level, Pushable Light), so the toy shoves it round until its beam lands on the core (LightTarget -> Awaken).
    ///    The light's first lesson, in the first minute.
    /// 2. Under the bed, a toy's cave: a DarkZone, a toy-sized flashlight (the player's own, earlier than the hallway's),
    ///    darts and a grenade crate, and a note on the wall only a light shows (LightTarget + LightReveal).
    /// Menu: Shift Jam / House / Land Bedroom Light Beats (D-0155).
    /// </summary>
    public static partial class CourseBuilder
    {
        const string AmmoBoxPrefab = "Assets/_Game/Prefabs/Pieces/Weapons/AmmoBox.prefab";
        const string SupplyCratePrefab = "Assets/_Game/Prefabs/Pieces/Weapons/SupplyCrate.prefab";
        const string TurntablePrefab = "Assets/_Imports/ithappy/Battle_Stadium/Prefabs/Obstacles/obstacle_14_001.prefab";

        /// <summary>A child anywhere under <paramref name="root"/> by name (the light may already ride a turntable).</summary>
        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        [MenuItem("Shift Jam/House/Land Bedroom Light Beats (D-0155)")]
        public static void LandBedroomLight()
        {
            Log.Clear();
            var active = SceneManager.GetActiveScene();
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            var toyBox = GameObject.Find("Room/ToyBox");
            var bedroom = GameObject.Find("Room/Bedroom");
            if (toyBox == null || bedroom == null) { Debug.LogError("D-0155: no Room/ToyBox or Room/Bedroom"); return; }

            // 1. The giant flashlight rides a turntable (D-0158, his "C would be cool if we have the assets": Battle Stadium's
            // spinning disc, its own spin off). The toy grabs the rim (E) and turns it: look where the beam should go. The
            // disc only turns on its hub, so the light cannot slide, roll or jam. The box's core stays dark until lit.
            Transform torch = FindDeep(toyBox.transform, "Flashlight_01");
            var core = toyBox.transform.Find("ShiftCore") != null ? toyBox.transform.Find("ShiftCore").GetComponent<ShiftSource>() : null;
            Transform floor = toyBox.transform.Find("Floor");
            float floorTop = floor != null ? B(floor.gameObject).max.y : 1f;
            Vector3 hub = new Vector3(-1f, floorTop, -21f);
            if (torch != null) torch.SetParent(toyBox.transform, true);   // out of an old turntable before it goes
            Remove(toyBox.transform, "Turntable");
            foreach (var (name, at) in new[] { ("Constructor_01", new Vector3(-12f, 1f, -29f)), ("Constructor_02", new Vector3(-12f, 1f, -25.5f)) })
            {
                Transform block = toyBox.transform.Find(name);
                if (block != null) block.position = at;   // the toy blocks go to the back-left corner, out of the way
            }
            var table = new GameObject("Turntable").transform;
            table.SetParent(toyBox.transform, true);
            table.SetPositionAndRotation(hub, Quaternion.identity);
            var discAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TurntablePrefab);
            if (discAsset != null && torch != null)
            {
                var disc = (GameObject)PrefabUtility.InstantiatePrefab(discAsset, table);
                disc.name = "Disc";
                float across = 14f;
                disc.transform.localScale = Vector3.one * (across / 8.5f);   // the pack's disc is 8.5 across: the 12-long light lies on it
                disc.transform.localPosition = Vector3.zero;
                disc.transform.position += Vector3.up * (floorTop - B(disc).min.y);
                foreach (var spin in disc.GetComponentsInChildren<MonoBehaviour>(true))
                    if (spin != null && spin.GetType().Name == "RotationScript") { var rso = new SerializedObject(spin); rso.FindProperty("rotationSpeed").floatValue = 0f; rso.ApplyModifiedPropertiesWithoutUndo(); }
                float top = B(disc).max.y;
                var body = table.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true; body.interpolation = RigidbodyInterpolation.Interpolate;
                // The light lies across the disc, riding it: no loose body of its own, no grab of its own.
                if (torch.TryGetComponent(out Grabbable oldGrab)) Object.DestroyImmediate(oldGrab);
                if (torch.TryGetComponent(out Pushable shove)) Object.DestroyImmediate(shove);
                if (torch.TryGetComponent(out Rigidbody loose)) Object.DestroyImmediate(loose);
                torch.SetParent(table, true);
                torch.rotation = Quaternion.Euler(0f, 33f, 0f);
                torch.position = new Vector3(hub.x, torch.position.y, hub.z);
                Bounds lb = B(torch.gameObject);
                torch.position += new Vector3(hub.x - lb.center.x, top + 0.02f - lb.min.y, hub.z - lb.center.z);
                var grab = table.gameObject.AddComponent<Grabbable>();
                var gso = new SerializedObject(grab);
                gso.FindProperty("prompt").stringValue = "Turn";
                gso.FindProperty("anchored").boolValue = true;
                gso.FindProperty("canPush").boolValue = false;
                gso.FindProperty("turnSpeed").floatValue = 40f;
                var torchLight = torch.GetComponent<Flashlight>();
                gso.FindProperty("aim").objectReferenceValue = torchLight != null && torchLight.Beam != null ? torchLight.Beam.transform : torch;
                gso.ApplyModifiedPropertiesWithoutUndo();
                Log.AppendLine($"turntable at {hub} ({across} across), the giant flashlight riding it at {torch.position}; toy blocks moved to the back-left corner");
            }
            else Log.AppendLine("NO turntable prefab or NO Flashlight_01");
            if (core != null)
            {
                var so = new SerializedObject(core);
                so.FindProperty("startDormant").boolValue = true;
                so.FindProperty("dormantPrompt").stringValue = "Dark. Turn the big light on its turntable (E) onto this.";
                so.ApplyModifiedPropertiesWithoutUndo();
                Transform middle = core.transform.Find("LightPoint");
                if (middle == null) { middle = new GameObject("LightPoint").transform; middle.SetParent(core.transform, false); }
                middle.position = B(core.gameObject).center;
                var target = Target(core.gameObject, middle, 0.6f, LightTarget.FocusRule.Any, 0f, false);
                UnityEventTools.AddVoidPersistentListener(target.OnFilled, core.Awaken);
                Log.AppendLine("toy box core: starts dark; LightTarget, On Filled -> Awaken");
            }
            else Log.AppendLine("NO ShiftCore in the toy box");

            // 2. Under the bed.
            var bed = bedroom.transform.Find("Bed");
            if (bed == null) { Debug.LogError("D-0155: no Bed"); return; }
            Bounds bb = B(bed.gameObject);
            float under = 8.5f;   // the frame's underside is 8.6 to 9.8 above the floor
            Remove(bedroom.transform, "UnderBed");
            var cave = new GameObject("UnderBed").transform;
            cave.SetParent(bedroom.transform, true);
            cave.position = new Vector3(bb.center.x, 0f, bb.center.z);
            var zone = new GameObject("UnderBedDark").AddComponent<DarkZone>();
            zone.transform.SetParent(cave, false);
            zone.transform.localPosition = Vector3.up * (under * 0.5f);
            var zso = new SerializedObject(zone);
            zso.FindProperty("size").vector3Value = new Vector3(bb.size.x - 2f, under, bb.size.z - 2f);
            zso.FindProperty("ambientTo").floatValue = 0.15f;
            zso.ApplyModifiedPropertiesWithoutUndo();

            var torchAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPrefabBuilder.PrefabPath);
            var shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftSettingsPath);
            float toy = shift != null && shift.kidScale > 0f ? shift.toyScale / shift.kidScale : 1f;
            var mine = (GameObject)PrefabUtility.InstantiatePrefab(torchAsset, cave);
            mine.name = "BedFlashlight";
            mine.transform.localScale = Vector3.one * toy;
            mine.transform.SetPositionAndRotation(new Vector3(bb.center.x + 10f, 0f, bb.center.z - 20f), Quaternion.Euler(0f, 130f, 0f));
            mine.transform.position += Vector3.up * (0.005f - B(mine).min.y);
            var cso = new SerializedObject(mine.GetComponent<Carryable>());
            cso.FindProperty("madeAt").enumValueIndex = (int)Carryable.MadeAt.Toy;
            cso.ApplyModifiedPropertiesWithoutUndo();
            Log.AppendLine($"BedFlashlight (toy-sized) at {mine.transform.position}");

            PlaceOnFloor(AmmoBoxPrefab, "BedAmmo", cave, new Vector3(bb.center.x - 4f, 0f, bb.center.z + 25f), 30f);
            PlaceOnFloor(SupplyCratePrefab, "BedGrenades", cave, new Vector3(bb.center.x + 20f, 0f, bb.center.z + 38f), -15f);

            // The note on the wall at the bed's head, shown only by a light.
            Vector3 from = new Vector3(bb.max.x - 6f, 3f, bb.center.z - 5f);
            if (Physics.Raycast(from, Vector3.right, out RaycastHit wall, 20f))
            {
                var note = TapeSign("BedNote", cave, wall.point - Vector3.right * 0.08f, wall.normal, "Lights out at 8!\n- Mom", 6f, 2.6f);
                note.transform.SetParent(cave, true);
                if (!note.TryGetComponent(out LightReveal reveal)) reveal = note.AddComponent<LightReveal>();
                var target = Target(note, note.transform, 0.3f, LightTarget.FocusRule.Any, 1.5f, true);
                while (target.OnFill.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(target.OnFill, 0);
                UnityEventTools.AddPersistentListener(target.OnFill, reveal.Show);
                Log.AppendLine($"BedNote on the wall at {wall.point}: shown by a light");
            }
            else Log.AppendLine("NO wall found east of the bed for the note");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("BEDROOM LIGHT BEATS (D-0155)\n" + Log);
        }

        static void PlaceOnFloor(string prefabPath, string name, Transform parent, Vector3 at, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null) { Log.AppendLine($"NO {prefabPath}"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            go.transform.position += Vector3.up * (0.005f - B(go).min.y);
            Log.AppendLine($"{name} at {go.transform.position}, {B(go).size}");
        }
    }
}
#endif
