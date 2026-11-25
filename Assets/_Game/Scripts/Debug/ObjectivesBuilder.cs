#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The objective layer's maker (D-0220). Menu: Shift Jam > Objectives > Build Objectives (D-0220).
    /// (1) Makes Resources/ObjectiveSet with the defaults, or refreshes it: a step or star the defaults have and the asset
    /// lacks is added (in route order); nothing he has written is changed. (2) In the OPEN scene (Greybox for the house):
    /// one ObjectiveHook per hook rule under a top-level "ObjectiveHooks (D-0220)", and each named part's UnityEvent (a
    /// core's On Used, a doorway's On Used, the letters' On All Found, a squad's On Routed, a skittle's or soldier's On
    /// Died, a ring's On Boost, the lights' On Lights On, the star lamp's On Woken) wired to it as a persistent listener.
    /// A second run reuses the hooks and replaces every listener that points at a hook (no duplicates; the parts' other
    /// listeners are left alone); a hook no rule uses any more is deleted. Marks the scene changed; it never saves it
    /// (Ctrl+S keeps it) and never opens a scene. (3) Reports every step and star: what it listens to and whether each
    /// object was found (and has the part it needs).
    /// The report's first line says whether this was the first run or a second; one that starts "[Objectives] NOT" changed
    /// nothing.
    /// </summary>
    public static class ObjectivesBuilder
    {
        public const string Tag = "[Objectives]";
        public const string AssetPath = "Assets/_Game/Resources/ObjectiveSet.asset";
        public const string HooksName = "ObjectiveHooks (D-0220)";

        private const int ModeVoid = 1, ModeInt = 3, RuntimeOnly = 2;   // PersistentListenerMode.Void / Int, UnityEventCallState.RuntimeOnly

        /// <summary>One listener to put on a part's event.</summary>
        private struct Wire
        {
            public Component part;
            public string field;
            public ObjectiveHook hook;
            public bool counted;
            public int index;
        }

        [MenuItem("Shift Jam/Objectives/Build Objectives (D-0220)")]
        public static void BuildMenu()
        {
            string report = Build();
            if (report.StartsWith(Tag + " NOT")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>Makes or refreshes the asset, wires the hooks in the open scene, and returns the report (one fact a line).</summary>
        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Tag + " NOT built: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return Tag + " NOT built: no scene is open. Open Greybox and run this again. Nothing was changed.";

            var log = new StringBuilder();
            GameObject holder = FindRoot(scene, HooksName);
            bool again = holder != null;
            log.AppendLine($"{Tag} Build Objectives (D-0220) in '{scene.name}': {(again ? "SECOND run: the hooks are reused and their listeners replaced (no duplicates)" : "first run")}.");

            // 1. The asset.
            ObjectiveSet set = AssetDatabase.LoadAssetAtPath<ObjectiveSet>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ObjectiveSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
                AssetDatabase.SaveAssets();
                log.AppendLine($"ASSET: made {AssetPath} with the defaults: {set.steps.Length} steps, {set.stars.Length} stars. Every word in it is a PLACEHOLDER for him to rewrite.");
            }
            else
            {
                string added = Refresh(set);
                if (added.Length > 0) { EditorUtility.SetDirty(set); AssetDatabase.SaveAssets(); }
                log.AppendLine($"ASSET: {AssetPath} kept as he left it ({set.steps.Length} steps, {set.stars.Length} stars, On {(set.on ? "ticked" : "UNTICKED: nothing counts in Play")}){(added.Length > 0 ? "; added from the defaults: " + added : "; nothing missing")}.");
            }
            if (set.stars.Length != 10) log.AppendLine($"NOTE: {set.stars.Length} stars, not 10: the HUD and the end card count what the asset holds.");

            // 2. Every step and star: what it listens to, whether its objects are here; the hook rules' wiring is gathered.
            var wires = new List<Wire>();
            var hooksUsed = new HashSet<GameObject>();
            int hooksMade = 0;
            for (int i = 0; i < set.steps.Length; i++)
            {
                ObjectiveStep s = set.steps[i];
                if (s == null) continue;
                log.AppendLine($"STEP {i + 1} '{s.id}': \"{s.text}\"{(string.IsNullOrWhiteSpace(s.target) ? "" : " (target " + s.target + (Objectives.Resolve(scene, s.target) != null ? ", found" : ", NOT FOUND here") + ")")}");
                Rules(scene, s.doneWhen, s.id, ObjectiveHook.Kind.Step, ref holder, wires, hooksUsed, ref hooksMade, log);
            }
            for (int i = 0; i < set.stars.Length; i++)
            {
                ObjectiveStar s = set.stars[i];
                if (s == null) continue;
                log.AppendLine($"STAR {i + 1} '{s.id}': \"{s.title}\" ({s.room})");
                Rules(scene, s.doneWhen, s.id, ObjectiveHook.Kind.Star, ref holder, wires, hooksUsed, ref hooksMade, log);
            }

            // 3. The listeners: every part's old hook listeners off (also any pointing at a deleted hook), then ours on.
            int wired = 0, removed = 0;
            var parts = new List<(Component part, string field)>();
            foreach (Wire w in wires) if (!parts.Contains((w.part, w.field))) parts.Add((w.part, w.field));
            foreach ((Component part, string field) in parts)
            {
                var so = new SerializedObject(part);
                SerializedProperty calls = so.FindProperty(field + ".m_PersistentCalls.m_Calls");
                if (calls == null) { log.AppendLine($"  NOT WIRED: {Path(part.transform)} has no '{field}' event on its {part.GetType().Name}."); continue; }
                for (int c = calls.arraySize - 1; c >= 0; c--)
                {
                    SerializedProperty call = calls.GetArrayElementAtIndex(c);
                    Object target = call.FindPropertyRelative("m_Target").objectReferenceValue;
                    string typeName = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue ?? "";
                    if (target is ObjectiveHook || (target == null && typeName.StartsWith(typeof(ObjectiveHook).FullName)))
                    {
                        calls.DeleteArrayElementAtIndex(c);
                        removed++;
                    }
                }
                foreach (Wire w in wires)
                {
                    if (w.part != part || w.field != field) continue;
                    calls.arraySize++;
                    SerializedProperty call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
                    call.FindPropertyRelative("m_Target").objectReferenceValue = w.hook;
                    call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(ObjectiveHook).AssemblyQualifiedName;
                    call.FindPropertyRelative("m_MethodName").stringValue = w.counted ? nameof(ObjectiveHook.FirePart) : nameof(ObjectiveHook.Fire);
                    call.FindPropertyRelative("m_Mode").intValue = w.counted ? ModeInt : ModeVoid;
                    call.FindPropertyRelative("m_CallState").intValue = RuntimeOnly;
                    // A new array element copies the one before it: clear every argument, then set the part's number.
                    SerializedProperty args = call.FindPropertyRelative("m_Arguments");
                    args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
                    args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = "";
                    args.FindPropertyRelative("m_IntArgument").intValue = w.counted ? w.index : 0;
                    args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
                    args.FindPropertyRelative("m_StringArgument").stringValue = "";
                    args.FindPropertyRelative("m_BoolArgument").boolValue = false;
                    wired++;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(part)) PrefabUtility.RecordPrefabInstancePropertyModifications(part);
            }

            // 4. Hooks no rule uses any more go (their parts' listeners were taken off above if the part is still named).
            int deleted = 0;
            if (holder != null)
                for (int c = holder.transform.childCount - 1; c >= 0; c--)
                {
                    GameObject child = holder.transform.GetChild(c).gameObject;
                    if (hooksUsed.Contains(child)) continue;
                    Object.DestroyImmediate(child);
                    deleted++;
                }

            if (holder != null || removed > 0) EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine($"HOOKS: {hooksUsed.Count} under '{HooksName}' ({hooksMade} made now{(deleted > 0 ? ", " + deleted + " unused deleted" : "")}); {wired} listeners wired{(removed > 0 ? ", " + removed + " old ones replaced" : "")}.");
            log.AppendLine(holder != null ? $"'{scene.name}' MARKED CHANGED, not saved: Ctrl+S keeps it." : $"No hook rule names a part in '{scene.name}': the scene was not touched.");
            log.AppendLine("OFF: untick On on Resources/ObjectiveSet (nothing counts, no HUD); untick or delete one hook to drop just that step or star's wiring.");
            return log.ToString();
        }

        /// <summary>Reports one step's or star's rules and gathers the hook rules' wiring.</summary>
        private static void Rules(Scene scene, ObjectiveRule[] rules, string id, ObjectiveHook.Kind kind, ref GameObject holder, List<Wire> wires,
            HashSet<GameObject> hooksUsed, ref int hooksMade, StringBuilder log)
        {
            if (rules == null || rules.Length == 0) { log.AppendLine("  NO RULE: it can never be done."); return; }
            for (int r = 0; r < rules.Length; r++)
            {
                ObjectiveRule rule = rules[r];
                if (rule == null) continue;
                string where = string.IsNullOrEmpty(rule.scene) ? "any scene" : rule.scene;
                string[] objects = rule.objects ?? new string[0];
                if (ObjectiveSet.IsSceneWide(rule.when))
                {
                    log.AppendLine($"  {(r > 0 ? "or " : "")}{rule.when} in {where}: heard in code ({Heard(rule.when)}); {InBuild(rule.scene)}.");
                    continue;
                }
                bool here = string.IsNullOrEmpty(rule.scene) || rule.scene == scene.name;
                bool hook = ObjectiveSet.IsHook(rule.when);
                int need = rule.needed <= 0 ? objects.Length : Mathf.Min(rule.needed, objects.Length);
                log.AppendLine($"  {(r > 0 ? "or " : "")}{rule.when} in {where}{(hook ? $", hook ({(need > 1 ? need + " different of " + objects.Length : "any one of " + objects.Length)})" : ", GameEvents")}:");
                if (objects.Length == 0) { log.AppendLine("    NO OBJECTS listed: it can never fire."); continue; }
                if (!here) { log.AppendLine($"    in '{rule.scene}', not the open scene: not checked or wired (open it and run this again)."); continue; }

                ObjectiveHook hookPart = null;
                for (int o = 0; o < objects.Length; o++)
                {
                    Transform t = Objectives.Resolve(scene, objects[o]);
                    if (t == null) { log.AppendLine($"    {objects[o]}: NOT FOUND"); continue; }
                    if (!hook) { log.AppendLine($"    {objects[o]}: found{Has(rule.when, t)}"); continue; }
                    Component part = PartFor(rule.when, t, out string field, out string typeName);
                    if (part == null) { log.AppendLine($"    {objects[o]}: found, but NO {typeName} on it or under it: not wired"); continue; }
                    if (hookPart == null) hookPart = HookFor(scene, ref holder, kind, id, r, rule.when, need, hooksUsed, ref hooksMade);
                    wires.Add(new Wire { part = part, field = field, hook = hookPart, counted = need > 1, index = o });
                    log.AppendLine($"    {objects[o]}: found; {typeName}.{field} on {Path(part.transform)} -> {hookPart.name}.{(need > 1 ? "FirePart(" + o + ")" : "Fire()")}");
                }
                if (hook && need > 1 && hookPart != null && wires.FindAll(w => w.hook == hookPart).Count < need)
                    log.AppendLine($"    WARNING: fewer than {need} parts wired: the hook can never count to {need}.");
            }
        }

        /// <summary>The hook for one rule, made or reused (named by kind, id and rule number).</summary>
        private static ObjectiveHook HookFor(Scene scene, ref GameObject holder, ObjectiveHook.Kind kind, string id, int rule, ObjectiveEvent when, int need,
            HashSet<GameObject> hooksUsed, ref int hooksMade)
        {
            if (holder == null)
            {
                holder = new GameObject(HooksName);
                SceneManager.MoveGameObjectToScene(holder, scene);
            }
            string name = $"{kind} {id} #{rule + 1} {when}";
            Transform child = holder.transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(holder.transform, false);
                hooksMade++;
            }
            ObjectiveHook hook = child.GetComponent<ObjectiveHook>();
            if (hook == null) hook = child.gameObject.AddComponent<ObjectiveHook>();
            hook.Setup(id, kind, need);
            EditorUtility.SetDirty(hook);
            hooksUsed.Add(child.gameObject);
            return hook;
        }

        /// <summary>The component whose UnityEvent a hook kind listens to, on the object or under it.</summary>
        private static Component PartFor(ObjectiveEvent when, Transform t, out string field, out string typeName)
        {
            switch (when)
            {
                case ObjectiveEvent.CoreUsed: field = "onUsed"; typeName = nameof(ShiftSource); return t.GetComponentInChildren<ShiftSource>(true);
                case ObjectiveEvent.PortalUsed: field = "onUsed"; typeName = nameof(Portal); return t.GetComponentInChildren<Portal>(true);
                case ObjectiveEvent.LettersFound: field = "onAllFound"; typeName = nameof(FlatZone); return t.GetComponentInChildren<FlatZone>(true);
                case ObjectiveEvent.SquadRouted: field = "onRouted"; typeName = nameof(SquadGate); return t.GetComponentInChildren<SquadGate>(true);
                case ObjectiveEvent.KnockedDown: field = "onDied"; typeName = nameof(Damageable); return t.GetComponentInChildren<Damageable>(true);
                case ObjectiveEvent.RingPassed: field = "onBoost"; typeName = nameof(BoostPad); return t.GetComponentInChildren<BoostPad>(true);
                case ObjectiveEvent.LightsOn: field = "onLightsOn"; typeName = nameof(RoomLights); return t.GetComponentInChildren<RoomLights>(true);
                case ObjectiveEvent.LampWoken: field = "onWoken"; typeName = nameof(StarLamp); return t.GetComponentInChildren<StarLamp>(true);
                default: field = ""; typeName = "?"; return null;
            }
        }

        /// <summary>For a GameEvents rule: whether the object carries what raises that event.</summary>
        private static string Has(ObjectiveEvent when, Transform t)
        {
            switch (when)
            {
                case ObjectiveEvent.SourceAwakened:
                {
                    var core = t.GetComponent<ShiftSource>();
                    if (core == null) return ", but NO ShiftSource on it";
                    bool dormant = new SerializedObject(core).FindProperty("startDormant").boolValue;
                    return dormant ? ", a ShiftSource that starts asleep" : ", but its ShiftSource starts AWAKE: it never wakes, so this never fires";
                }
                case ObjectiveEvent.SwitchPressed: return t.GetComponent<Switch>() != null ? ", a Switch" : ", but NO Switch on it";
                case ObjectiveEvent.DoorOpening: return t.GetComponentInParent<Door>() != null || t.GetComponentInChildren<Door>(true) != null ? ", a Door" : ", but NO Door on it";
                case ObjectiveEvent.CheckpointClaimed: return t.GetComponent<Checkpoint>() != null ? ", a Checkpoint" : ", but NO Checkpoint on it";
                case ObjectiveEvent.GoalReached: return t.GetComponent<Goal>() != null ? ", a Goal" : ", but NO Goal on it";
                case ObjectiveEvent.PickedUp:
                case ObjectiveEvent.SetDown:
                    return t.GetComponentInChildren<Carryable>(true) != null || t.GetComponentInChildren<Rigidbody>(true) != null ? ", something the hands can take" : ", but nothing the hands can take (no Carryable or Rigidbody)";
                default: return "";
            }
        }

        private static string Heard(ObjectiveEvent when) =>
            when == ObjectiveEvent.RaceWon ? "RaceState.Finished with the player's car home first" : "a ColourCorner's Changed once it is Finished";

        private static string InBuild(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return "any scene";
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.enabled && s.path.EndsWith("/" + sceneName + ".unity")) return $"'{sceneName}' is in Build Settings";
            return $"'{sceneName}' is NOT in Build Settings (enabled): it cannot be reached in a built game";
        }

        /// <summary>A step or star the defaults have and the asset lacks (by id) goes in at its default place. Returns the
        /// ids added, or "".</summary>
        private static string Refresh(ObjectiveSet set)
        {
            var added = new StringBuilder();
            var steps = new List<ObjectiveStep>(set.steps ?? new ObjectiveStep[0]);
            ObjectiveStep[] defaultSteps = ObjectiveSet.DefaultSteps();
            for (int i = 0; i < defaultSteps.Length; i++)
            {
                if (steps.Exists(s => s != null && s.id == defaultSteps[i].id)) continue;
                int at = i == 0 ? 0 : steps.FindIndex(s => s != null && s.id == defaultSteps[i - 1].id) + 1;
                if (at <= 0 && i > 0) at = steps.Count;
                steps.Insert(Mathf.Clamp(at, 0, steps.Count), defaultSteps[i]);
                added.Append(added.Length > 0 ? ", " : "").Append("step " + defaultSteps[i].id);
            }
            var stars = new List<ObjectiveStar>(set.stars ?? new ObjectiveStar[0]);
            foreach (ObjectiveStar d in ObjectiveSet.DefaultStars())
            {
                if (stars.Exists(s => s != null && s.id == d.id)) continue;
                stars.Add(d);
                added.Append(added.Length > 0 ? ", " : "").Append("star " + d.id);
            }
            set.steps = steps.ToArray();
            set.stars = stars.ToArray();
            return added.ToString();
        }

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
