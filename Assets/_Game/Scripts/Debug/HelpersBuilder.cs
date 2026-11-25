#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShiftJam
{
    /// <summary>
    /// The objective helpers' maker (D-0221). Menu: Shift Jam > Objectives > Land Helpers (D-0221).
    /// (1) Resources/ObjectiveSet: any default arrival card, core place, grow room or step line whose id is missing is added;
    /// nothing he has written is changed (made with every default if there is no asset yet). (2) In the OPEN scene (Greybox):
    /// a top-level "ObjectiveHelpers (D-0221)" holding one ArrivalCard trigger per card (at the asset's box, on the Ignore
    /// Raycast layer) and, downstairs, an ammo box (the game's own AmmoBox, MazeAmmo's prefab, with the asset's magazines and
    /// respawn) on the floor under the asset's landing spot with a warm point light over it. (3) A CoreMarker on every core
    /// (its name and, from its Mode, which size it refills). A second run deletes the holder and makes it again (no
    /// duplicates) and updates the markers in place. Marks the scene changed; never saves it (Ctrl+S keeps it) and never
    /// opens a scene. (4) Reports: every card, core and character, the crate's spot and what it gives, how many darts
    /// routing the Tan Splinter takes, and whether its "{0} to go" lines reach the player.
    /// The report's first line says whether this was the first run or a second; one that starts "[Helpers] NOT" changed
    /// nothing.
    /// </summary>
    public static class HelpersBuilder
    {
        public const string Tag = "[Helpers]";
        public const string RootName = "ObjectiveHelpers (D-0221)";
        private const string AmmoPiece = "Assets/_Game/Prefabs/Pieces/Weapons/AmmoBox.prefab";
        private const string SquadPath = "Room/Course/Downstairs/TanSplinter";
        private const string CoursePath = "Room/Course";
        private const int IgnoreRaycast = 2;
        private const float FloorLookUp = 60f, FloorLookDown = 140f, OnFloor = 0.02f;
        private static readonly string[] Blasters = { "DartPistol", "DartSMG", "DartShotgun", "DartRifle", "DartSniper" };
        private const string BlasterFolder = "Assets/_Game/Prefabs/Weapons/";

        [MenuItem("Shift Jam/Objectives/Land Helpers (D-0221)")]
        public static void LandMenu()
        {
            string report = Land();
            if (report.StartsWith(Tag + " NOT")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>Refreshes the asset, lands the helpers in the open scene, and returns the report (one fact a line).</summary>
        public static string Land()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Tag + " NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return Tag + " NOT landed: no scene is open. Open Greybox and run this again. Nothing was changed.";
            if (Objectives.Resolve(scene, CoursePath) == null)
                return Tag + $" NOT landed: '{scene.name}' has no {CoursePath} (the house). Open Greybox and run this again. Nothing was changed.";

            var log = new StringBuilder();
            GameObject old = FindRoot(scene, RootName);
            bool again = old != null;
            log.AppendLine($"{Tag} Land Helpers (D-0221) in '{scene.name}': {(again ? "SECOND run: '" + RootName + "' deleted and made again (no duplicates); core markers updated in place" : "first run")}.");

            ObjectiveSet set = Asset(log);

            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Physics.SyncTransforms();

            Arrivals(scene, set, root.transform, log);
            Refusals(scene, set, log);
            Characters(scene, set, log);
            Downstairs(scene, set, root.transform, log);
            Squad(scene, set, log);

            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine($"'{scene.name}' MARKED CHANGED, not saved: Ctrl+S keeps it.");
            log.AppendLine("OFF: ObjectiveSet > Arrivals On, Refusals Point, Friends Say Step (and Step Mark On for the '!'), Nudge On; the crate and light: delete '"
                           + RootName + "/Downstairs'; one card: delete its trigger, or empty its title on the asset.");
            return log.ToString();
        }

        // ---------- 1. the asset ----------

        private static ObjectiveSet Asset(StringBuilder log)
        {
            ObjectiveSet set = AssetDatabase.LoadAssetAtPath<ObjectiveSet>(ObjectivesBuilder.AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ObjectiveSet>();
                AssetDatabase.CreateAsset(set, ObjectivesBuilder.AssetPath);
                AssetDatabase.SaveAssets();
                log.AppendLine($"ASSET: made {ObjectivesBuilder.AssetPath} with every default (the D-0220 steps and stars too): run Build Objectives (D-0220) for its hooks. Every word in it is a PLACEHOLDER.");
                return set;
            }
            var added = new StringBuilder();
            set.arrivals = AddMissing(set.arrivals, HelperWords.DefaultArrivals(), a => a.id, "card", added);
            set.corePlaces = AddMissing(set.corePlaces, HelperWords.DefaultCorePlaces(), p => p.core, "core", added);
            set.growRooms = AddMissing(set.growRooms, HelperWords.DefaultGrowRooms(), r => r.id, "grow room", added);
            set.stepTalk = AddMissing(set.stepTalk, HelperWords.DefaultStepTalk(), t => t.step, "step line", added);
            // Written either way: an asset saved before D-0221 holds the new fields' defaults only in memory until saved.
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            log.AppendLine($"ASSET: {ObjectivesBuilder.AssetPath} kept as he left it ({Count(set.arrivals)} cards, {Count(set.corePlaces)} core places, {Count(set.growRooms)} grow rooms, {Count(set.stepTalk)} step lines){(added.Length > 0 ? "; added from the defaults: " + added : "; nothing missing")}; written.");
            if (!set.on) log.AppendLine("NOTE: ObjectiveSet's On is UNTICKED: no helper shows in Play (they all follow Objectives.Live).");
            return set;
        }

        /// <summary>Every default whose key the asset lacks, added at the end; what he wrote is left as it is.</summary>
        private static T[] AddMissing<T>(T[] have, T[] defaults, Func<T, string> key, string what, StringBuilder added) where T : class
        {
            var list = new List<T>(have ?? new T[0]);
            foreach (T d in defaults)
            {
                string k = key(d);
                if (list.Exists(x => x != null && key(x) == k)) continue;
                list.Add(d);
                added.Append(added.Length > 0 ? ", " : "").Append(what + " " + k);
            }
            return list.ToArray();
        }

        private static int Count<T>(T[] array) => array != null ? array.Length : 0;

        // ---------- 2. the arrival cards ----------

        private static void Arrivals(Scene scene, ObjectiveSet set, Transform root, StringBuilder log)
        {
            var holder = new GameObject("Arrivals").transform;
            holder.SetParent(root, false);
            var checkpoints = new List<Checkpoint>();
            foreach (GameObject top in scene.GetRootGameObjects()) checkpoints.AddRange(top.GetComponentsInChildren<Checkpoint>(true));
            int made = 0;
            foreach (ArrivalWords a in set.arrivals ?? new ArrivalWords[0])
            {
                if (a == null || string.IsNullOrWhiteSpace(a.id)) continue;
                if (string.IsNullOrWhiteSpace(a.title)) { log.AppendLine($"CARD '{a.id}': no title on the asset: no trigger (no card)."); continue; }
                var go = new GameObject("Arrival " + a.id) { layer = IgnoreRaycast };
                go.transform.SetParent(holder, false);
                go.transform.position = a.centre;
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(Mathf.Abs(a.size.x), Mathf.Abs(a.size.y), Mathf.Abs(a.size.z));
                go.AddComponent<ArrivalCard>().Setup(a.id);
                made++;
                var bounds = new Bounds(a.centre, box.size);
                var inside = new StringBuilder();
                int n = 0;
                foreach (Checkpoint c in checkpoints)
                    if (bounds.Contains(c.transform.position)) { if (n < 3) inside.Append(n > 0 ? ", " : "").Append(c.name); n++; }
                log.AppendLine($"CARD '{a.id}': \"{a.title}\" / \"{a.line}\"{(a.onGround ? " (waits for the ground)" : "")}; trigger at {a.centre} size {box.size}; checkpoints inside: {(n > 0 ? n + " (" + inside + (n > 3 ? ", ..." : "") + ")" : "none")}.");
            }
            log.AppendLine($"ARRIVALS: {made} triggers under '{RootName}/Arrivals'; each card once per save slot (toast:{ObjectiveHelpers.ArrivalKey}<id>), when no other big card, menu, reveal or Flat Zone is up.");
        }

        // ---------- 3. the refusals: a marker on every core ----------

        private static void Refusals(Scene scene, ObjectiveSet set, StringBuilder log)
        {
            var cores = new List<ShiftSource>();
            foreach (GameObject top in scene.GetRootGameObjects()) cores.AddRange(top.GetComponentsInChildren<ShiftSource>(true));
            int made = 0, kept = 0, worded = 0;
            foreach (ShiftSource core in cores)
            {
                var so = new SerializedObject(core);
                SerializedProperty modeProp = so.FindProperty("mode");
                var mode = modeProp != null ? (ShiftSource.Mode)modeProp.enumValueIndex : ShiftSource.Mode.Toggle;
                SerializedProperty dormantProp = so.FindProperty("startDormant");
                bool dormant = dormantProp != null && dormantProp.boolValue;
                bool toy = mode != ShiftSource.Mode.ShrinkOnly, kid = mode != ShiftSource.Mode.GrowOnly;
                CoreMarker marker = core.GetComponent<CoreMarker>();
                if (marker == null) { marker = core.gameObject.AddComponent<CoreMarker>(); made++; } else kept++;
                marker.Setup(core.name, toy, kid);
                EditorUtility.SetDirty(marker);
                if (PrefabUtility.IsPartOfPrefabInstance(marker)) PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
                string where = HelperWords.CorePlaceOf(set, core.name);
                if (!string.IsNullOrWhiteSpace(where)) worded++;
                log.AppendLine($"CORE {Path(core.transform)}: {mode}{(dormant ? ", starts asleep" : "")}, refills a {(toy && kid ? "toy or a kid" : toy ? "toy" : "kid")}; "
                               + (string.IsNullOrWhiteSpace(where) ? "NO place words on the asset (never named: the old words)" : "\"" + HelperWords.Format(set.emptyPointFormat, where) + "\""));
            }
            log.AppendLine($"REFUSALS: {cores.Count} cores, {made} markers made, {kept} updated; {worded} have place words. 'No charge' names the nearest awake one that would refill the player's size; else the old words.");
            foreach (GrowRoom r in set.growRooms ?? new GrowRoom[0])
                if (r != null) log.AppendLine($"GROW ROOM '{r.id}': box at {r.centre} size {r.size}: \"{r.words}\" instead of 'No room to grow'.");
        }

        // ---------- 4. the house characters ----------

        private static void Characters(Scene scene, ObjectiveSet set, StringBuilder log)
        {
            var found = new List<HouseCharacter>();
            foreach (GameObject top in scene.GetRootGameObjects()) found.AddRange(top.GetComponentsInChildren<HouseCharacter>(true));
            foreach (HouseCharacter h in found)
            {
                var so = new SerializedObject(h);
                SerializedProperty say = so.FindProperty("sayStep");
                SerializedProperty lines = so.FindProperty("lines");
                log.AppendLine($"CHARACTER {Path(h.transform)} ('{h.CharacterName}'): Say Step {(say == null || say.boolValue ? "ticked" : "UNTICKED (its own lines only)")}, {(lines != null ? lines.arraySize : 0)} own lines.");
            }
            int missing = 0;
            foreach (ObjectiveStep s in set.steps ?? new ObjectiveStep[0])
                if (s != null && string.IsNullOrWhiteSpace(HelperWords.StepLine(set, s.id))) { missing++; log.AppendLine($"  step '{s.id}' has NO step line: they say their own lines then."); }
            log.AppendLine($"CHARACTERS: {found.Count} found; the first E for a step says its line (then their own, then it again); a '!' until then. {(set.steps != null ? set.steps.Length : 0) - missing} of {(set.steps != null ? set.steps.Length : 0)} steps have a line.");
        }

        // ---------- 5. downstairs: the ammo box and the warm light ----------

        private static void Downstairs(Scene scene, ObjectiveSet set, Transform root, StringBuilder log)
        {
            var holder = new GameObject("Downstairs").transform;
            holder.SetParent(root, false);
            Vector3 spot = set.landingSpot;
            Vector3 floor = spot;
            string under = "nothing (placed at the asset's spot as it is)";
            if (Physics.Raycast(spot + Vector3.up * FloorLookUp, Vector3.down, out RaycastHit hit, FloorLookDown, ~0, QueryTriggerInteraction.Ignore))
            {
                floor = hit.point;
                under = Path(hit.collider.transform);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AmmoPiece);
            if (prefab == null) log.AppendLine($"CRATE: NOT MADE: no {AmmoPiece} (run Shift Jam > Workshop > Build Ammo Pieces).");
            else
            {
                var crate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
                crate.name = "DownstairsAmmo";
                crate.transform.SetPositionAndRotation(floor + Vector3.up * OnFloor, Quaternion.identity);
                AmmoBox box = crate.GetComponent<AmmoBox>();
                if (box != null)
                {
                    var so = new SerializedObject(box);
                    so.FindProperty("magazines").intValue = Mathf.Max(0, set.crateMagazines);
                    so.FindProperty("respawnSeconds").floatValue = Mathf.Max(0f, set.crateRespawnSeconds);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                }
                log.AppendLine($"CRATE: {Path(crate.transform)} (AmmoBox, MazeAmmo's prefab) at {crate.transform.position}, on {under}; magazines {set.crateMagazines}{(set.crateMagazines <= 0 ? " (fills every reserve to the top, and the grenades)" : "")}, back after {set.crateRespawnSeconds} s{(box == null ? "; NO AmmoBox on the prefab!" : "")}.");
                log.AppendLine("  It gives each blaster CARRIED (none carried: nothing): " + Gains(set.crateMagazines) + ".");
            }

            var lightGo = new GameObject("LandingLight");
            lightGo.transform.SetParent(holder, false);
            lightGo.transform.position = floor + Vector3.up * set.landingLightHeight;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = set.landingLightColour;
            light.range = set.landingLightRange;
            light.intensity = set.landingLightIntensity;
            light.shadows = LightShadows.None;
            log.AppendLine($"LIGHT: {Path(lightGo.transform)} at {lightGo.transform.position}: point, range {light.range}, intensity {light.intensity}, colour {light.color}, no shadows.");
        }

        /// <summary>"DartSMG +60 (reserve most 120)" for each blaster prefab, for a box of <paramref name="magazines"/>.</summary>
        private static string Gains(int magazines)
        {
            var sb = new StringBuilder();
            foreach (string name in Blasters)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(BlasterFolder + name + ".prefab");
                BlasterWeapon gun = go != null ? go.GetComponentInChildren<BlasterWeapon>(true) : null;
                if (gun == null) continue;
                var so = new SerializedObject(gun);
                int size = so.FindProperty("magazine.size").intValue, most = so.FindProperty("magazine.reserveMax").intValue;
                int gain = magazines <= 0 ? most : Mathf.Min(most, size * magazines);
                sb.Append(sb.Length > 0 ? ", " : "").Append($"{name} +{gain} (magazine {size}, reserve most {most})");
            }
            return sb.Length > 0 ? sb.ToString() : "no blaster prefabs found under " + BlasterFolder;
        }

        // ---------- 6. the Tan Splinter: the darts it takes, and its toasts ----------

        private static void Squad(Scene scene, ObjectiveSet set, StringBuilder log)
        {
            Transform squad = Objectives.Resolve(scene, SquadPath);
            SquadGate gate = squad != null ? squad.GetComponentInChildren<SquadGate>(true) : null;
            if (gate == null) { log.AppendLine($"SQUAD: NO SquadGate at {SquadPath} (run Land Downstairs): the star cannot be won."); return; }
            var gso = new SerializedObject(gate);
            var soldiers = new List<ToySoldier>();
            SerializedProperty listed = gso.FindProperty("soldiers");
            for (int i = 0; listed != null && i < listed.arraySize; i++)
                if (listed.GetArrayElementAtIndex(i).objectReferenceValue is ToySoldier t) soldiers.Add(t);
            bool fromChildren = soldiers.Count == 0;
            if (fromChildren) soldiers.AddRange(gate.GetComponentsInChildren<ToySoldier>(true));

            float dart = 15f, pellet = 7.5f;
            int pellets = 8;
            var smg = AssetDatabase.LoadAssetAtPath<GameObject>(BlasterFolder + "DartSMG.prefab");
            var shotgun = AssetDatabase.LoadAssetAtPath<GameObject>(BlasterFolder + "DartShotgun.prefab");
            if (smg != null && smg.GetComponentInChildren<BlasterWeapon>(true) is BlasterWeapon g1) dart = new SerializedObject(g1).FindProperty("damage").floatValue;
            if (shotgun != null && shotgun.GetComponentInChildren<BlasterWeapon>(true) is BlasterWeapon g2)
            {
                var s2 = new SerializedObject(g2);
                pellet = s2.FindProperty("damage").floatValue;
                pellets = Mathf.Max(1, s2.FindProperty("pellets").intValue);
            }
            int darts = 0, shells = 0, officers = 0;
            var health = new StringBuilder();
            foreach (ToySoldier s in soldiers)
            {
                Damageable hp = s.GetComponent<Damageable>();
                float most = hp != null ? new SerializedObject(hp).FindProperty("maxHealth").floatValue : 0f;
                if (s.name.Contains("Officer")) officers++;
                darts += Mathf.CeilToInt(most / Mathf.Max(0.01f, dart));
                shells += Mathf.CeilToInt(Mathf.CeilToInt(most / Mathf.Max(0.01f, pellet)) / (float)pellets);
                health.Append(health.Length > 0 ? ", " : "").Append($"{s.name} {most}");
            }
            log.AppendLine($"SQUAD: {soldiers.Count} in the Tan Splinter ({soldiers.Count - officers} soldiers, {officers} officer){(fromChildren ? ", found under it" : ", listed on its SquadGate")}: health {health}.");
            log.AppendLine($"  Routing them (each knocked down ONCE) takes at least {darts} dart hits at {dart} a dart (pistol, SMG), or {shells} shotgun shells with every pellet landing ({pellets} x {pellet}); misses and soldiers who get up again (no extra need) aside. Punches and kicks count too.");

            string left = gso.FindProperty("left").stringValue, routed = gso.FindProperty("routed").stringValue;
            var shifters = new List<Shifter>();
            foreach (GameObject top in scene.GetRootGameObjects()) shifters.AddRange(top.GetComponentsInChildren<Shifter>(true));
            log.AppendLine($"  Its toasts: \"{left}\" ({(string.IsNullOrEmpty(left) ? "EMPTY: none shows" : "Warning")}) as each falls, \"{routed}\" (Got) at the last. SquadGate shows them through the FIRST ShiftCharge in the scene"
                           + $" (FindFirstObjectByType): in '{scene.name}' there {(shifters.Count == 1 ? "is one Shifter, " + Path(shifters[0].transform) + ", whose ShiftCharge (added at Start) has the HUD: so offline they reach the player" : "are " + shifters.Count + " Shifters: the first ShiftCharge found may not be the player's (the toast then shows nowhere)")}."
                           + " Online, other players' copies carry ShiftCharges without a HUD and the first found may be one of them (objectives are off online anyway).");

            SerializedProperty calls = gso.FindProperty("onRouted.m_PersistentCalls.m_Calls");
            int hooked = 0;
            for (int i = 0; calls != null && i < calls.arraySize; i++)
                if (calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target").objectReferenceValue is ObjectiveHook) hooked++;
            log.AppendLine($"  The 'tan-splinter' star: {(hooked > 0 ? "its On Routed calls an ObjectiveHook (D-0220): routing them earns it" : "On Routed calls NO ObjectiveHook: run Build Objectives (D-0220) or the star never comes")}.");
        }

        // ---------- helpers ----------

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
            return null;
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
#endif
