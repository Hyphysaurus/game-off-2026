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

            DarkStretchTest(room);   // D-0215 over D-0151: a re-run keeps the no-sign test

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("HALLWAY LIGHT BEATS (D-0151)\n" + Log);
        }

        // ---- D-0215: the dark stretch as the test of what the bedroom taught (no sign says "shine your light")
        const string ChargeHintLine = "Dark. Shine your light on it.";   // D-0151's line: it comes back after 20 s by the sleeping core
        const float ChargeHintLinger = 20f, ChargeHintNear = 12f;
        /// <summary>The charge sign's words as the House builder first wrote them (before D-0151 added the light line).</summary>
        const string ChargeSignWords = "A core fills your <b>shift charge</b>.\n<b>F</b>  shift anywhere there is room.\nToo low in here: try it.";
        const string DarkOrbName = "ChargeCoreDark";
        static readonly Vector3 DarkOrbAt = new Vector3(209f, 2.5f, -144f);   // off the lane by the north wall, just past the pencil bridge (east floor x 206..232), before the pins and the tall end

        /// <summary>
        /// D-0215. Safe to run again (sets the same, replaces what it placed).
        /// 1. The spare flashlight (x 98) stays; its tape sign (Sign2_Light) shows only to a player with no flashlight.
        /// 2. The stretch's sleeping charge core says nothing (its breath and hum guide); after 20 s by it, the old line comes back.
        ///    The charge sign loses its "Dark? Shine your light on it." line (back to the House builder's words).
        /// 3. One extra sleeping charge orb (ChargeOnly, unlimited) off the lane, found by its breath and hum: a refill before
        ///    the tall end's F. A light wakes it, as the stretch's own core.
        /// </summary>
        static void DarkStretchTest(Transform room)
        {
            // 1. The spare light's sign.
            Transform lightSign = room.Find("Sign2_Light");
            if (lightSign != null)
            {
                bool had = lightSign.GetComponent<NoLightSign>() != null;
                if (!had) lightSign.gameObject.AddComponent<NoLightSign>();
                Log.AppendLine($"HALL Sign2_Light: NoLightSign {(had ? "was there" : "ADDED")}: it shows only to a player with no flashlight (the spare at x {HallLightX} stays)");
            }
            else Log.AppendLine("HALL Sign2_Light NOT FOUND (run Land Hallway Light Beats)");

            // 2. The stretch's charge core: silent, then the line after lingering.
            Transform coreT = room.Find("ChargeCore");
            var core = coreT != null ? coreT.GetComponent<ShiftSource>() : null;
            if (core != null)
            {
                var so = new SerializedObject(core);
                string was = so.FindProperty("dormantPrompt").stringValue;
                so.FindProperty("dormantPrompt").stringValue = "";
                so.FindProperty("sleepCues").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (!coreT.TryGetComponent(out SleepHint hint)) hint = coreT.gameObject.AddComponent<SleepHint>();
                var hso = new SerializedObject(hint);
                hso.FindProperty("line").stringValue = ChargeHintLine;
                hso.FindProperty("linger").floatValue = ChargeHintLinger;
                hso.FindProperty("near").floatValue = ChargeHintNear;
                hso.FindProperty("tip").boolValue = true;
                hso.ApplyModifiedPropertiesWithoutUndo();
                Log.AppendLine($"HALL ChargeCore at {coreT.position}: its sleeping prompt \"{was}\" -> none (E finds nothing until it wakes); SleepHint: \"{ChargeHintLine}\" comes back as its prompt and once as a tip after {ChargeHintLinger} s in all within {ChargeHintNear} of it (toy units).");
            }
            else Log.AppendLine("HALL ChargeCore NOT FOUND");
            Transform sign = room.Find("Sign3_Charge");
            if (sign != null)
            {
                Vector3 at = sign.position, toViewer = -sign.forward; int index = sign.GetSiblingIndex();
                Object.DestroyImmediate(sign.gameObject);
                TapeSign("Sign3_Charge", room, at, toViewer, ChargeSignWords, 8f, 3.6f).transform.SetSiblingIndex(index);
                Log.AppendLine("HALL Sign3_Charge: \"A core fills your shift charge. / F shift anywhere there is room. / Too low in here: try it.\" (the light line gone; was \"... / Dark? Shine your light on it. / ...\")");
            }
            else Log.AppendLine("HALL Sign3_Charge NOT FOUND");

            // 3. The extra sleeping orb.
            Remove(room, DarkOrbName);
            var coreMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/ShiftCore.mat");
            GameObject extra = Core(DarkOrbName, room, DarkOrbAt, ShiftSource.Mode.ChargeOnly, coreMat, true);
            var extraCore = extra.GetComponent<ShiftSource>();
            var eso = new SerializedObject(extraCore);
            eso.FindProperty("dormantPrompt").stringValue = "";
            eso.FindProperty("sleepCues").boolValue = true;
            eso.ApplyModifiedPropertiesWithoutUndo();
            Transform middle = new GameObject("LightPoint").transform;
            middle.SetParent(extra.transform, false);
            middle.position = B(extra).center;
            var target = Target(extra, middle, 0.5f, LightTarget.FocusRule.Any, 0f, false);
            UnityEventTools.AddVoidPersistentListener(target.OnFilled, extraCore.Awaken);
            var stretch = room.Find("DarkStretch");
            var zone = stretch != null ? stretch.GetComponent<DarkZone>() : null;
            if (zone != null) Log.AppendLine($"ZONE DarkStretch: not changed (lamps {zone.LightsTo}, ambient {zone.AmbientTo}, box {zone.Size} round {stretch.position}).");
            Log.AppendLine($"HALL {DarkOrbName}: a sleeping ChargeOnly orb (unlimited) at {extra.transform.position}, no prompt, LightTarget (0.5 s, any light) -> Awaken; {(zone != null ? (zone.Contains(extra.transform.position) ? "inside DarkStretch" : "NOT inside DarkStretch") : "DarkStretch NOT FOUND")}; {Apart(extra.transform, room, "Pin")} from the nearest pin, {Apart(extra.transform, room, "ChargeCoreTall")} from ChargeCoreTall (awake), {Apart(extra.transform, room, "CP_afterBridge")} from the checkpoint after the bridge.");
        }

        /// <summary>The flat distance from <paramref name="from"/> to the nearest child of <paramref name="room"/> called
        /// <paramref name="name"/>, as words.</summary>
        static string Apart(Transform from, Transform room, string name)
        {
            float best = float.MaxValue;
            foreach (Transform other in room)
            {
                if (other.name != name) continue;
                Vector3 d = other.position - from.position; d.y = 0f;
                best = Mathf.Min(best, d.magnitude);
            }
            return best < float.MaxValue ? best.ToString("F1") : "(no " + name + ")";
        }

        const float BeamIntensityKid = 20000f;   // D-0153 his "it's pretty dark, we need to amplify the beam": was 3000
        const float BeamRangeKid = 350f;         // was 90 / 120: a toy's light now reaches 10 toy units, a kid's 350
        const string StunPrefab = "Assets/Polygon Arsenal/Prefabs/Combat/Melee & Sword/Stun/Stun1.prefab";
        static readonly string[] DazzleClips =
        {
            "Assets/_Imports/ithappy/Playroom/Character_Voice_Pack/Male/Vocal_Sounds/Startled_Male_v1.wav",
            "Assets/_Imports/ithappy/Playroom/Character_Voice_Pack/Male/Vocal_Sounds/Startled_Male_v2.wav",
        };

        /// <summary>
        /// D-0153: every toy soldier in the open scene shows a stun effect and cries out when dazzled; every flashlight (the
        /// prefab and the scene's) has the brighter, longer beam. Safe to run again.
        /// Menu: Shift Jam / House / Soldier Dazzle FX + Brighter Beams (D-0153).
        /// </summary>
        [MenuItem("Shift Jam/House/Soldier Dazzle FX + Brighter Beams (D-0153)")]
        public static void DazzleAndBeams()
        {
            Log.Clear();
            var stun = AssetDatabase.LoadAssetAtPath<GameObject>(StunPrefab);
            var clips = new System.Collections.Generic.List<AudioClip>();
            foreach (string c in DazzleClips) { var a = AssetDatabase.LoadAssetAtPath<AudioClip>(c); if (a != null) clips.Add(a); }
            int soldiers = 0;
            foreach (var s in Object.FindObjectsByType<ToySoldier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(s);
                so.FindProperty("dazzleEffect").objectReferenceValue = stun;
                var list = so.FindProperty("dazzleSounds"); list.arraySize = clips.Count;
                for (int i = 0; i < clips.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                soldiers++;
            }
            Log.AppendLine($"{soldiers} soldier(s): stun effect {(stun != null ? stun.name : "NOT FOUND")}, {clips.Count} sound(s)");

            // The prefab, then every flashlight in the scene (their own values are overrides: set them too).
            GameObject root = PrefabUtility.LoadPrefabContents(FlashlightPrefabBuilder.PrefabPath);
            Beam(root.GetComponent<Flashlight>());
            PrefabUtility.SaveAsPrefabAsset(root, FlashlightPrefabBuilder.PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            int torches = 0;
            foreach (var f in Object.FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Beam(f); torches++; }
            Log.AppendLine($"beam {BeamIntensityKid} bright, {BeamRangeKid} reach at kid size: the prefab and {torches} flashlight(s)");
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("DAZZLE + BEAMS (D-0153)\n" + Log);
        }

        static void Beam(Flashlight f)
        {
            if (f == null) return;
            var so = new SerializedObject(f);
            so.FindProperty("beamIntensity").floatValue = BeamIntensityKid;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (f.Beam != null) { var lo = new SerializedObject(f.Beam); lo.FindProperty("m_Range").floatValue = BeamRangeKid; lo.ApplyModifiedPropertiesWithoutUndo(); }
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
