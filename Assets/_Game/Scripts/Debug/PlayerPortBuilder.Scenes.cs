#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// The scene half of the player port. The Player is an object in each scene (there is no Player prefab), so each of
    /// the four scenes is opened, changed and saved: the old body comes off; the Male and the Female go on as children
    /// (the pack's prefabs, unpacked so the pack's own mover and capsule can be taken off them), fitted and given their
    /// controllers; the move scripts are added; references to the old Animator are cleared (each script finds the body
    /// in use by itself); first person is added where it was missing and set to V; the wake-up starts where the new
    /// clip lies still; a death no longer respawns at once (PlayerHurt plays the fall first, then asks the Respawn); and
    /// BodyFade is added (it hides the body while the camera is inside it, D-0065).
    /// </summary>
    public static partial class PlayerPortBuilder
    {
        private static void LandInScene(string path, PlayerBodySettings settings, List<RuntimeAnimatorController> controllers)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var changes = new List<string>();
            PlayerMotor motor = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                motor = root.GetComponentInChildren<PlayerMotor>(true);
                if (motor != null) break;
            }
            if (!Check(motor != null, $"{path}: has a Player with a PlayerMotor")) return;
            GameObject player = motor.gameObject;

            // The old body: any child with an Animator that is not one of ours.
            var ours = new HashSet<string>();
            foreach (BodySpec spec in Bodies) ours.Add(spec.child);
            for (int i = player.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = player.transform.GetChild(i);
                if (ours.Contains(child.name) || child.GetComponentInChildren<Animator>(true) == null) continue;
                changes.Add("took off the old body '" + child.name + "'");
                Object.DestroyImmediate(child.gameObject);
            }

            for (int i = 0; i < Bodies.Length; i++)
            {
                BodySpec spec = Bodies[i];
                PlayerBodySettings.Body body = settings.bodies[i];
                Transform have = player.transform.Find(spec.child);
                GameObject go = have != null ? have.gameObject : null;
                if (go != null && !IsCharacter(go, spec.pack))
                {
                    changes.Add(spec.child + " was the wrong character: replaced");
                    Object.DestroyImmediate(go);
                    go = null;
                }
                if (go == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefab);
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
                    // Unpacked one level: the pack's own mover and capsule are on the prefab's root and must come off.
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                    go.name = spec.child;
                    changes.Add("put on " + spec.child);
                }
                foreach (MonoBehaviour script in go.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(script);
                foreach (Collider collider in go.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                go.transform.localPosition = Vector3.up * body.lift;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * body.fit;
                go.SetActive(true);
                var animator = go.GetComponentInChildren<Animator>(true);
                if (!Check(animator != null && IsCharacter(go, spec.pack), $"{path}: {spec.child} is the pack's {spec.pack} and has its Animator")) continue;
                animator.runtimeAnimatorController = controllers[i];
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // the one body always animates (as CarryPose asks)
                animator.updateMode = AnimatorUpdateMode.Normal;
                EditorUtility.SetDirty(animator);
                Drawn(go, out float feet, out float top);
                feet += body.lift; top += body.lift;   // Drawn measures from the body's own origin
                Check(Mathf.Abs(feet) < 0.02f && top > 1.5f && top < 2.2f, $"{path}: {spec.child} stands on the player's feet (soles {feet:F3}, top {top:F2}, fit {body.fit:F3}, lift {body.lift:F3})");
                go.SetActive(i == 0);   // the Male shows by default; PlayerBody switches at run time
            }

            foreach (string added in new[]
            {
                Ensure<PlayerBody>(player), Ensure<PlayerStance>(player), Ensure<PlayerMoves>(player),
                Ensure<PlayerAnimation>(player), Ensure<PlayerHurt>(player), Ensure<BodyFade>(player),
            })
                if (added != null) changes.Add(added);

            // The old Animator is gone: every script finds the body in use by itself.
            foreach (MonoBehaviour script in player.GetComponents<MonoBehaviour>())
            {
                if (script == null) continue;
                var so = new SerializedObject(script);
                SerializedProperty p = so.FindProperty("animator");
                if (p == null || p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                p.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                changes.Add(script.GetType().Name + ".animator cleared");
            }

            // First person everywhere, on V.
            var firstPerson = player.GetComponent<FirstPersonMode>();
            if (firstPerson == null)
            {
                firstPerson = player.AddComponent<FirstPersonMode>();
                changes.Add("added FirstPersonMode");
            }
            var fp = new SerializedObject(firstPerson);
            if (fp.FindProperty("settings").objectReferenceValue == null)
                fp.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FirstPersonSettings>(FirstPersonSettingsPath);
            if (fp.FindProperty("toggleKey").intValue != (int)Key.V) { fp.FindProperty("toggleKey").intValue = (int)Key.V; changes.Add("first person on V"); }
            fp.ApplyModifiedPropertiesWithoutUndo();
            Check(fp.FindProperty("settings").objectReferenceValue != null, $"{path}: first person has its settings");

            // The wake-up starts where the new death clip lies still.
            var wake = player.GetComponent<WakeUp>();
            if (wake != null)
            {
                var so = new SerializedObject(wake);
                if (!Mathf.Approximately(so.FindProperty("startAt").floatValue, WakeStartAt) || !Mathf.Approximately(so.FindProperty("duration").floatValue, WakeSeconds))
                {
                    so.FindProperty("startAt").floatValue = WakeStartAt;
                    so.FindProperty("duration").floatValue = WakeSeconds;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changes.Add($"WakeUp starts at {WakeStartAt:F2} of the backwards state (the clip's {WakeFrom} point, lying), {WakeSeconds} s");
                }
            }

            // A death plays the fall first: PlayerHurt asks the Respawn afterwards, so the scene's own "died -> Respawn.Reset" goes.
            var health = player.GetComponent<Damageable>();
            if (health != null)
            {
                for (int i = health.OnDiedEvent.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    if (!(health.OnDiedEvent.GetPersistentTarget(i) is Respawn) || health.OnDiedEvent.GetPersistentMethodName(i) != "Reset") continue;
                    UnityEventTools.RemovePersistentListener(health.OnDiedEvent, i);
                    EditorUtility.SetDirty(health);
                    changes.Add("Damageable.onDied no longer calls Respawn.Reset (PlayerHurt does, after the fall)");
                }
            }

            // The hint under "what you carry": put-down moved from C to X.
            foreach (InteractPrompt prompt in Object.FindObjectsByType<InteractPrompt>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(prompt);
                SerializedProperty hint = so.FindProperty("heldHint");
                if (hint == null || !hint.stringValue.Contains("C  Put down")) continue;
                hint.stringValue = hint.stringValue.Replace("C  Put down", "X  Put down");
                so.ApplyModifiedPropertiesWithoutUndo();
                changes.Add("the carry hint says X  Put down");
            }

            int missing = 0;
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true)) missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            Check(missing == 0, $"{path}: no missing scripts on the Player ({missing})");
            Check(player.GetComponentsInChildren<Animator>(true).Length == Bodies.Length, $"{path}: the Player has {Bodies.Length} bodies and no other Animator");

            // Always saved: the fit and the controllers are written again on every run (an unchanged scene saves the same file).
            EditorSceneManager.MarkSceneDirty(scene);
            Check(EditorSceneManager.SaveScene(scene), $"{path}: saved");
            Say($"{path}: " + (changes.Count > 0 ? string.Join("; ", changes) : "bodies fitted again, nothing else to change"));
        }

        /// <summary>True if the body's drawn parts are the pack's character of that name.</summary>
        private static bool IsCharacter(GameObject body, string pack)
        {
            foreach (SkinnedMeshRenderer skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.name.StartsWith(pack)) return true;
            return false;
        }

        private static string Ensure<T>(GameObject player) where T : Component
        {
            if (player.GetComponent<T>() != null) return null;
            player.AddComponent<T>();
            return "added " + typeof(T).Name;
        }
    }
}
#endif
