#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShiftJam
{
    /// <summary>
    /// Editor maker for the funny soldier (D-0187): a toy soldier with a living body. Menu "Shift Jam > Characters > Build
    /// Funny Soldier (D-0187)" (batch: -executeMethod ShiftJam.SoldierBodyPieces.BuildAll) makes
    /// Prefabs/Pieces/Interactables/FunnySoldier, a prefab VARIANT of ToySoldier (so every number of the soldier stays the
    /// base's), with:
    ///  - a FunnyBody copy under the soldier's Visual, and every other child of Visual (the plastic model, which a variant
    ///    cannot remove) switched off;
    ///  - the player's controller (Animation/PlayerMale) on the body's Animator, root motion off, always animated (its hit
    ///    shapes ride its bones, seen or not);
    ///  - SoldierBody on the root, linked to the body.
    /// The first time it is put together in a scene of its own (the open scene never holds it) and the SoldierBody's
    /// numbers that depend on the rig are started: its height from the soldier's walking capsule, its blaster
    /// (Prefabs/Weapons/DartPistol) at the part's own grip (seated on the funny body's hand). The body is dressed
    /// in the practice colour for the prefab's own look, and fitted to the height.
    /// Later runs open the prefab and bring only what this tool owns up to date (the body there, the plastic model off,
    /// the controller, root motion and culling, the link, the body fitted to the height set on it). Numbers tuned on
    /// SoldierBody, and the look shown on the prefab's body, are never touched. Refused in Play; nothing in an open scene
    /// is touched or saved. It ends with one line, "SoldierBodyPieces: built ..." (or "... NOT built ...").
    /// </summary>
    public static class SoldierBodyPieces
    {
        private const string Tag = "[SoldierBody]";
        private const string PrefabDir = "Assets/_Game/Prefabs/Pieces/Interactables";
        /// <summary>The plastic toy soldier the funny one is a variant of.</summary>
        public const string SoldierPath = PrefabDir + "/ToySoldier.prefab";
        /// <summary>Where the funny soldier (a variant of ToySoldier) is made.</summary>
        public const string PrefabPath = PrefabDir + "/FunnySoldier.prefab";
        /// <summary>The player's own controller, which the body walks, falls and gets up with.</summary>
        public const string ControllerPath = "Assets/_Game/Animation/PlayerMale.controller";
        /// <summary>The blaster the body holds when the prefab is first made.</summary>
        public const string BlasterPath = "Assets/_Game/Prefabs/Weapons/DartPistol.prefab";
        private const string PrefabName = "FunnySoldier";
        private const string BodyName = "FunnyBody";
        private const int PreviewSeed = 1;   // the prefab's own look (each soldier rolls its own from its post as it wakes)

        /// <summary>Makes or updates the prefab (see the class notes) and logs one closing line. Refused in Play.
        /// Batch: -executeMethod ShiftJam.SoldierBodyPieces.BuildAll</summary>
        [MenuItem("Shift Jam/Characters/Build Funny Soldier (D-0187)")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("SoldierBodyPieces: NOT built: stop Play first; nothing was changed.");
                return;
            }
            GameObject prefab = MakePrefab(out string report);
            if (prefab == null)
            {
                Debug.LogError("SoldierBodyPieces: NOT built: " + report);
                return;
            }
            Debug.Log("SoldierBodyPieces: built " + PrefabPath + ": " + report);
        }

        /// <summary>Makes Prefabs/Pieces/Interactables/FunnySoldier or brings it up to date in place, and returns the saved
        /// prefab; null when it could not (<paramref name="report"/> says why). <paramref name="report"/> otherwise says
        /// what it did, for the log.</summary>
        public static GameObject MakePrefab(out string report)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { report = "stop Play first; nothing was changed."; return null; }
            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPath);
            if (soldier == null) { report = "no " + SoldierPath + "; nothing was changed."; return null; }
            var funny = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPieces.BodyPath);
            if (funny == null) { report = "no " + CharacterPieces.BodyPath + "; run Shift Jam > Characters > Build Characters first. Nothing was changed."; return null; }
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null) Debug.LogWarning(Tag + " No " + ControllerPath + " (run Shift Jam > Player > Build Player Port): the body's Animator keeps the controller it has, and the body stands still.");
            var blaster = AssetDatabase.LoadAssetAtPath<Weapon>(BlasterPath);
            if (blaster == null) Debug.LogWarning(Tag + " No weapon at " + BlasterPath + ": a newly made soldier is empty-handed.");

            WorkshopPieces.EnsureFolder(PrefabDir);
            bool made = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            bool saved;
            string done;
            if (made)
            {
                // The first time: a ToySoldier copy put together in a scene of its own and saved as a variant of it.
                Scene bench = EditorSceneManager.NewPreviewScene();
                try
                {
                    var copy = (GameObject)PrefabUtility.InstantiatePrefab(soldier, bench);
                    copy.name = PrefabName;
                    done = Configure(copy, funny, controller, blaster);
                    PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath, out saved);
                    Object.DestroyImmediate(copy);
                }
                finally { EditorSceneManager.ClosePreviewScene(bench); }
            }
            else
            {
                // Later: opened and brought up to date in place; what is tuned on it stays.
                GameObject open = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    done = Configure(open, funny, controller, blaster);
                    PrefabUtility.SaveAsPrefabAsset(open, PrefabPath, out saved);
                }
                finally { PrefabUtility.UnloadPrefabContents(open); }
            }
            if (!saved) { report = "it could not be saved at " + PrefabPath + " (" + done + ")."; return null; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            bool variant = prefab != null && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant
                           && PrefabUtility.GetCorrespondingObjectFromSource(prefab) == soldier;
            report = (made ? "made" : "brought up to date") + (variant ? ", a variant of " + SoldierPath : ", NOT a variant of ToySoldier (unpacked by hand?)") + "; " + done;
            return prefab;
        }

        /// <summary>What the tool owns on the soldier (see the class notes). Returns a short report for the log.</summary>
        private static string Configure(GameObject root, GameObject funny, RuntimeAnimatorController controller, Weapon blaster)
        {
            var log = new StringBuilder();
            if (!root.TryGetComponent(out ToySoldier toy)) return "NO ToySoldier on " + root.name + ": nothing set";
            Transform visual = new SerializedObject(toy).FindProperty("visual").objectReferenceValue as Transform;
            if (visual == null && root.transform.childCount > 0) visual = root.transform.GetChild(0);   // ToySoldier's own rule
            if (visual == null) return "NO Visual under " + root.name + ": nothing set";

            // The body under Visual.
            CharacterBody body = visual.GetComponentInChildren<CharacterBody>(true);
            bool newBody = body == null;
            if (newBody)
            {
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(funny, visual);
                copy.name = BodyName;
                copy.TryGetComponent(out body);
            }
            if (body == null) return "NO CharacterBody on " + CharacterPieces.BodyPath + ": nothing set";
            log.Append(newBody ? "FunnyBody added under Visual" : "FunnyBody kept");

            // The plastic model (and anything else under Visual) off: a variant cannot remove the base's children.
            int off = 0;
            foreach (Transform child in visual)
            {
                if (child == body.transform || !child.gameObject.activeSelf) continue;
                child.gameObject.SetActive(false);
                Record(child.gameObject);
                off++;
            }
            log.Append(", " + off + " plastic part(s) switched off");

            // The body's Animator: the player's controller, no root motion, always animated.
            Animator animator = body.Animator;
            if (animator != null)
            {
                if (controller != null) animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Record(animator);
                log.Append(", controller " + (animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "none") + " (always animated)");
            }
            else log.Append(", NO Animator under the body (is the Funny Characters pack missing?)");

            // SoldierBody on the root, linked to the body; the rig-dependent numbers only when it is new.
            bool newPart = !root.TryGetComponent(out SoldierBody part);
            if (newPart) part = root.AddComponent<SoldierBody>();
            var so = new SerializedObject(part);
            so.FindProperty("body").objectReferenceValue = body;
            if (newPart)
            {
                so.FindProperty("blaster").objectReferenceValue = blaster;
                if (root.TryGetComponent(out CharacterController cc) && cc.height > 0f) so.FindProperty("height").floatValue = cc.height;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            float height = so.FindProperty("height").floatValue;

            // Dressed for the prefab's own look (new body only: a look set on it later is kept), and fitted.
            if (newBody) body.WearRolled(PreviewSeed, null, CharacterLookRules.Default.practiceColour);
            part.Fit();
            Record(body.transform);
            log.Append(", SoldierBody " + (newPart ? "added" : "kept") + ", body fitted to " + height.ToString("F2") + " tall");

            if (newPart)
            {
                // The grip is the part's own (seated by eye on the funny body's hand). The player's own grip, read on this
                // hand (SoldierBody.HoldAsPlayer, its ⋮ menu), leaves the handle in front of the chunkier fist.
                log.Append(", the blaster in its fist at the part's own grip");
            }
            log.Append(", blaster " + (new SerializedObject(part).FindProperty("blaster").objectReferenceValue is Weapon w ? w.name : "none"));
            return log.ToString();
        }

        /// <summary>A change to a part of a prefab copy, kept as that copy's override.</summary>
        private static void Record(Object changed)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(changed)) PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
            EditorUtility.SetDirty(changed);
        }
    }
}
#endif
