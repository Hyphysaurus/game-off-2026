#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShiftJam
{
    /// <summary>
    /// Editor makers for the funny characters' look system (D-0183). Two menus:
    /// "Build Characters (D-0183)" (batch: -executeMethod ShiftJam.CharacterPieces.BuildAll) does, in order:
    ///  1. BakeCatalog: writes the parts list (Resources/CharacterCatalog) from the Funny Characters pack: every part
    ///     file of the twelve part folders, the folders in slot order and each folder's files in name order (ordinal: the
    ///     same on every machine). It references the pack's meshes and materials and copies nothing. Run twice, it writes
    ///     the same list.
    ///  2. MakeRules: makes the default look rules (Resources/CharacterLookRules) when there are none. Never overwrites
    ///     them: their numbers are the owner's to tune.
    ///  3. MakeBody: makes the body prefab (Prefabs/Characters/FunnyBody): the pack's rig nested as a prefab, CharacterBody
    ///     on the root with the default rules, dressed with seed 1 and no colour, the rig's Animator with root motion off,
    ///     always animating, its humanoid avatar kept and no controller. A FunnyBody already there is opened and brought up
    ///     to date in place (so copies of it placed in scenes keep their overrides).
    /// It ends with one line, "CharacterPieces: built ..." (or "CharacterPieces: NOT built ...").
    /// "Line Up Twelve Looks" puts twelve FunnyBody copies in a row in the open scene (seeds 1 to 12, the first two in Blue
    /// and Orange) under one root, LooksLineUp, replacing an earlier one. It saves nothing.
    /// The pack is git-ignored: nothing of it is copied into a tracked folder; the assets made here only reference it.
    /// </summary>
    public static class CharacterPieces
    {
        private const string Pack = "Assets/_Imports/ithappy/Funny_Characters";
        private const string Meshes = Pack + "/Meshes";
        private const string RigPath = Meshes + "/Basic_Character.fbx";
        private const string ResourcesDir = "Assets/_Game/Resources";
        /// <summary>Where the parts list is written.</summary>
        public const string CatalogPath = ResourcesDir + "/CharacterCatalog.asset";
        /// <summary>Where the default look rules are made (once).</summary>
        public const string RulesPath = ResourcesDir + "/CharacterLookRules.asset";
        private const string BodyDir = "Assets/_Game/Prefabs/Characters";
        /// <summary>Where the body prefab is made.</summary>
        public const string BodyPath = BodyDir + "/FunnyBody.prefab";
        private const string BodyName = "FunnyBody";
        private const int BodySeed = 1;

        private const string LineUpName = "LooksLineUp";
        private const int LineUpCount = 12;
        private const float LineUpSpacing = 2.2f;   // a T-posed figure is 1.82 wide
        private static readonly string[] LineUpColours = { "Blue", "Orange" };   // the first figures' colours; the rest none

        // The pack's part folders, in slot order: the order the parts list is written in. Changing it changes every
        // rolled look (the roll picks by place in the list).
        private static readonly (string folder, CharacterSlot slot)[] Folders =
        {
            ("Body", CharacterSlot.Body), ("Hairs", CharacterSlot.Hair), ("Hats", CharacterSlot.Hat), ("Glasses", CharacterSlot.Glasses),
            ("Eyebrows", CharacterSlot.Eyebrow), ("Mustache", CharacterSlot.Mustache), ("Outerwear", CharacterSlot.Outerwear), ("Pants", CharacterSlot.Pants),
            ("Shoe", CharacterSlot.Shoe), ("Gloves", CharacterSlot.Glove), ("Backpack", CharacterSlot.Backpack), ("Full_Body", CharacterSlot.FullBody),
        };

        /// <summary>The three steps in order (see the class notes) and one closing line. Refused in Play.
        /// Batch: -executeMethod ShiftJam.CharacterPieces.BuildAll</summary>
        [MenuItem("Shift Jam/Characters/Build Characters (D-0183)")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("CharacterPieces: NOT built: stop Play first (the running game reads the parts list); nothing was changed.");
                return;
            }
            CharacterCatalog catalog = BakeCatalog();
            if (catalog == null)
            {
                Debug.LogError("CharacterPieces: NOT built: the parts list could not be baked (see the error above).");
                return;
            }
            CharacterLookRules rules = MakeRules();
            GameObject body = MakeBody();
            if (rules == null || body == null)
            {
                Debug.LogError("CharacterPieces: NOT built: " + (rules == null ? "the rules asset" : "the body prefab") + " could not be made (see the error above).");
                return;
            }
            var dressed = body.GetComponent<CharacterBody>();
            Debug.Log("CharacterPieces: built " + catalog.parts.Count + " parts into " + CatalogPath + ", rules at " + RulesPath + ", " + BodyPath
                + " (seed " + BodySeed + ": " + (dressed != null ? dressed.LookText : "?") + ").");
        }

        // ---------- 1. the parts list ----------

        /// <summary>Writes Resources/CharacterCatalog from the pack (see the class notes) and returns it; null, with the
        /// old list left as it was, when the pack is not in the project or no part was found. Logs the count per slot,
        /// warns for a part file with no skinned mesh or with bones that do not match the rig's, and names the parts
        /// added or dropped since the last bake.</summary>
        public static CharacterCatalog BakeCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Characters] Bake: stop Play first; nothing was changed.");
                return null;
            }
            if (!AssetDatabase.IsValidFolder(Meshes))
            {
                Debug.LogError("[Characters] Bake: the Funny Characters pack is not in the project (" + Meshes + "); nothing was changed.");
                return null;
            }
            string[] rigBones = RigBoneNames();
            if (rigBones == null) return null;

            var parts = new List<CharacterCatalog.Part>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var perSlot = new List<string>();
            int warned = 0;
            foreach ((string folder, CharacterSlot slot) in Folders)
            {
                int count = 0;
                foreach (string path in Files(Meshes + "/" + folder, ".fbx"))
                {
                    string id = Path.GetFileNameWithoutExtension(path);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    SkinnedMeshRenderer smr = model != null ? FirstSkinned(model) : null;
                    if (smr == null)
                    {
                        Debug.LogWarning("[Characters] Bake: " + path + " has no skinned mesh; left out.");
                        warned++;
                        continue;
                    }
                    if (!SameBones(smr, rigBones))
                    {
                        Debug.LogWarning("[Characters] Bake: " + path + " is not skinned to the rig's " + rigBones.Length + " bones in the rig's order, so it cannot go on the slot as it is; left out.");
                        warned++;
                        continue;
                    }
                    if (!ids.Add(id))
                    {
                        Debug.LogWarning("[Characters] Bake: a second part named " + id + " (" + path + "); left out (ids must be one of a kind).");
                        warned++;
                        continue;
                    }
                    Mesh mesh = smr.sharedMesh;
                    ReadName(id, slot, out string colour, out string kind);
                    parts.Add(new CharacterCatalog.Part
                    {
                        id = id,
                        slot = slot,
                        mesh = mesh,
                        materials = smr.sharedMaterials,
                        colour = colour,
                        kind = kind,
                        triangles = Triangles(mesh),
                        top = mesh.bounds.max.y,
                    });
                    count++;
                }
                perSlot.Add(CharacterSlots.TextName(slot) + " " + count);
            }
            if (parts.Count == 0)
            {
                Debug.LogError("[Characters] Bake: no part was found under " + Meshes + "; nothing was changed.");
                return null;
            }

            WorkshopPieces.EnsureFolder(ResourcesDir);
            CharacterCatalog catalog = WorkshopPieces.SettingsAsset<CharacterCatalog>(CatalogPath);
            var before = new HashSet<string>(StringComparer.Ordinal);
            if (catalog.parts != null) foreach (CharacterCatalog.Part old in catalog.parts) if (old != null && !string.IsNullOrEmpty(old.id)) before.Add(old.id);
            var added = new List<string>();
            foreach (CharacterCatalog.Part part in parts) if (!before.Contains(part.id)) added.Add(part.id);
            var dropped = new List<string>();
            foreach (string id in before) if (!ids.Contains(id)) dropped.Add(id);
            dropped.Sort(string.CompareOrdinal);

            catalog.parts = parts;
            catalog.Forget();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            string change = before.Count == 0 ? "a new list"
                : added.Count == 0 && dropped.Count == 0 ? "the same parts as before"
                : (added.Count > 0 ? "new: " + Names(added) : "") + (added.Count > 0 && dropped.Count > 0 ? "; " : "") + (dropped.Count > 0 ? "dropped: " + Names(dropped) : "");
            Debug.Log("[Characters] Baked " + parts.Count + " parts into " + CatalogPath + " (" + string.Join(", ", perSlot) + "); " + warned + " files left out; " + change + "."
                + (added.Count > 0 && before.Count > 0 ? " New parts shift the rolled looks (looks saved as text are unchanged)." : ""));
            return catalog;
        }

        /// <summary>The names of the rig's bones its Body slot is skinned to, in order; null (logged) when the rig is
        /// missing or has no Body slot.</summary>
        private static string[] RigBoneNames()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            Transform slot = rig != null ? rig.transform.Find(CharacterSlots.ObjectName(CharacterSlot.Body)) : null;
            if (slot == null || !slot.TryGetComponent(out SkinnedMeshRenderer smr) || smr.bones == null || smr.bones.Length == 0)
            {
                Debug.LogError("[Characters] Bake: the pack's rig (" + RigPath + ") is missing or has no skinned Body slot; nothing was changed.");
                return null;
            }
            Transform[] bones = smr.bones;
            var names = new string[bones.Length];
            for (int i = 0; i < bones.Length; i++) names[i] = bones[i] != null ? bones[i].name : "";
            return names;
        }

        /// <summary>True when the part is skinned to the rig's bones by the same names in the same order, with as many
        /// bind poses: then its mesh can go on the slot's renderer as it is.</summary>
        private static bool SameBones(SkinnedMeshRenderer smr, string[] rigBones)
        {
            Transform[] bones = smr.bones;
            if (bones == null || bones.Length != rigBones.Length || smr.sharedMesh.bindposes.Length != rigBones.Length) return false;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] == null || !string.Equals(bones[i].name, rigBones[i], StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>The first skinned renderer with a mesh in the model, or null.</summary>
        private static SkinnedMeshRenderer FirstSkinned(GameObject model)
        {
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) return smr;
            return null;
        }

        /// <summary>Every triangle of every sub-mesh.</summary>
        private static int Triangles(Mesh mesh)
        {
            long indices = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
            return (int)(indices / 3);
        }

        /// <summary>The colour and kind words in a part's file name: "&lt;Slot&gt;_&lt;words&gt;_&lt;number&gt;" (the slot as its rig
        /// object is named). For shoes the words are the kind (Sneaker), for every other slot the colour (Light_Blue). A
        /// name with no words between the slot and the number (Hat_001), or not starting with the slot (the full-body suits:
        /// Banana_001), has neither.</summary>
        private static void ReadName(string id, CharacterSlot slot, out string colour, out string kind)
        {
            colour = "";
            kind = "";
            string stem = id;
            int last = id.LastIndexOf('_');
            if (last > 0 && last < id.Length - 1 && Digits(id, last + 1)) stem = id.Substring(0, last);
            string prefix = CharacterSlots.ObjectName(slot) + "_";
            if (stem.Length <= prefix.Length || !stem.StartsWith(prefix, StringComparison.Ordinal)) return;
            string words = stem.Substring(prefix.Length);
            if (slot == CharacterSlot.Shoe) kind = words;
            else colour = words;
        }

        private static bool Digits(string text, int from)
        {
            for (int i = from; i < text.Length; i++) if (text[i] < '0' || text[i] > '9') return false;
            return true;
        }

        /// <summary>Every file with this ending directly in a folder, with forward slashes, in ordinal FILE NAME order
        /// (the same on every machine, so two bakes give the same list).</summary>
        private static List<string> Files(string folder, string ending)
        {
            var found = new List<string>();
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning("[Characters] Bake: no folder " + folder + "; that slot has no parts.");
                return found;
            }
            foreach (string file in Directory.GetFiles(folder, "*" + ending, SearchOption.TopDirectoryOnly))
                if (file.EndsWith(ending, StringComparison.OrdinalIgnoreCase)) found.Add(file.Replace('\\', '/'));
            found.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
            return found;
        }

        /// <summary>A short list of names for the log: the first eight, then how many more.</summary>
        private static string Names(List<string> names)
        {
            const int shown = 8;
            if (names.Count <= shown) return string.Join(", ", names);
            return string.Join(", ", names.GetRange(0, shown)) + " and " + (names.Count - shown) + " more";
        }

        // ---------- 2. the rules ----------

        /// <summary>Resources/CharacterLookRules: made with the numbers written in CharacterLookRules.cs when there is
        /// none; one already there is returned untouched (its numbers are the owner's).</summary>
        public static CharacterLookRules MakeRules()
        {
            var rules = AssetDatabase.LoadAssetAtPath<CharacterLookRules>(RulesPath);
            if (rules != null)
            {
                Debug.Log("[Characters] Look rules already at " + RulesPath + " and left alone (your numbers are kept).");
                return rules;
            }
            WorkshopPieces.EnsureFolder(ResourcesDir);
            rules = ScriptableObject.CreateInstance<CharacterLookRules>();
            AssetDatabase.CreateAsset(rules, RulesPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Characters] Look rules made at " + RulesPath + " with the default numbers. Tune them there; the build never writes them again.");
            return rules;
        }

        // ---------- 3. the body prefab ----------

        /// <summary>Makes (or brings up to date in place) Prefabs/Characters/FunnyBody, as the class notes say, and
        /// returns the saved prefab; null (logged) when the pack's rig, its humanoid avatar or the parts list is missing.</summary>
        public static GameObject MakeBody()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Characters] FunnyBody: stop Play first; nothing was made.");
                return null;
            }
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            var rigAnimator = rig != null ? rig.GetComponent<Animator>() : null;
            if (rigAnimator == null || rigAnimator.avatar == null || !rigAnimator.avatar.isHuman)
            {
                Debug.LogError("[Characters] FunnyBody was NOT made: the pack's rig (" + RigPath + ") is missing or has no humanoid avatar.");
                return null;
            }
            CharacterCatalog catalog = CharacterCatalog.Load();
            if (catalog == null || !catalog.Ready)
            {
                Debug.LogError("[Characters] FunnyBody was NOT made: no parts list to dress it with; bake it first (BakeCatalog).");
                return null;
            }
            CharacterLookRules rules = AssetDatabase.LoadAssetAtPath<CharacterLookRules>(RulesPath);
            if (rules == null) rules = MakeRules();

            WorkshopPieces.EnsureFolder(BodyDir);
            bool made = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath) == null)
            {
                // The first time: a shell (root + CharacterBody + the rig nested), saved bare, then dressed below like
                // any later run, inside the prefab's own editing scene (no open scene is touched by the dressing).
                // The shell is put together in a scene of its own, so the scene that is open (his, with the editor in use)
                // never holds it, not even for a moment, and is never marked changed.
                var workbench = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                bool shellSaved;
                try
                {
                    var shell = new GameObject(BodyName);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shell, workbench);
                    shell.AddComponent<CharacterBody>();
                    NestRig(rig, shell.transform);
                    PrefabUtility.SaveAsPrefabAsset(shell, BodyPath, out shellSaved);
                    Object.DestroyImmediate(shell);
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(workbench); }
                if (!shellSaved)
                {
                    Debug.LogError("[Characters] FunnyBody was NOT made: the prefab could not be saved at " + BodyPath + ".");
                    return null;
                }
                made = true;
            }

            GameObject open = PrefabUtility.LoadPrefabContents(BodyPath);
            string look, avatarName;
            float height;
            bool saved;
            try
            {
                if (!open.TryGetComponent(out CharacterBody body)) body = open.AddComponent<CharacterBody>();
                Animator animator = open.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    // The rig was taken out by hand: put it back.
                    NestRig(rig, open.transform);
                    animator = open.GetComponentInChildren<Animator>(true);
                }
                // The rig keeps its avatar (never set here) and gets no controller here; its hit shapes (later) hang on
                // bones, so it animates even out of view, and the body is moved by its owner, not by the clips.
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                avatarName = animator.avatar != null ? animator.avatar.name + (animator.avatar.isHuman ? " (humanoid)" : " (NOT humanoid)") : "none";
                if (PrefabUtility.IsPartOfPrefabInstance(animator)) PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

                body.WearRolled(BodySeed, rules, "");
                look = body.LookText;
                height = body.Height;
                PrefabUtility.SaveAsPrefabAsset(open, BodyPath, out saved);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(open);
            }
            if (!saved)
            {
                Debug.LogError("[Characters] FunnyBody was NOT saved at " + BodyPath + ".");
                return null;
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath);
            Debug.Log("[Characters] FunnyBody " + (made ? "made" : "brought up to date") + " at " + BodyPath + ": the pack's rig nested, avatar " + avatarName
                + ", root motion off, always animating, no controller; dressed with seed " + BodySeed + " (" + look + "), " + Triangles(catalog, look) + " triangles, "
                + height.ToString("F2") + " tall.");
            return prefab;
        }

        /// <summary>The pack's rig as a nested prefab under <paramref name="parent"/>, at its origin.</summary>
        private static void NestRig(GameObject rig, Transform parent)
        {
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(rig, parent);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
        }

        /// <summary>How many triangles a look's text draws (for the log).</summary>
        private static int Triangles(CharacterCatalog catalog, string lookText)
        {
            if (!CharacterLook.TryParse(lookText, out CharacterLook look)) return 0;
            int total = 0;
            for (int i = 0; i < CharacterSlots.Count; i++)
            {
                CharacterCatalog.Part part = catalog.PartById(look.Get((CharacterSlot)i));
                if (part != null) total += part.triangles;
            }
            return total;
        }

        // ---------- the line-up ----------

        /// <summary>Twelve FunnyBody copies in a row in the open scene, under one root named LooksLineUp (an earlier one
        /// is replaced): seeds 1 to 12, the first two rolled Blue and Orange, by the default rules. Placed round the Scene
        /// view's centre (the world's origin when there is no Scene view), facing +Z. Undoable; the scene is not saved.</summary>
        [MenuItem("Shift Jam/Characters/Line Up Twelve Looks")]
        public static void LineUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Characters] Line Up: stop Play first; nothing was placed.");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath);
            if (prefab == null)
            {
                Debug.LogError("[Characters] Line Up: no " + BodyPath + "; run Shift Jam > Characters > Build Characters first.");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == LineUpName) Undo.DestroyObjectImmediate(root);

            var lineUp = new GameObject(LineUpName);
            Undo.RegisterCreatedObjectUndo(lineUp, "Line Up Twelve Looks");
            SceneView view = SceneView.lastActiveSceneView;
            lineUp.transform.position = view != null ? view.pivot : Vector3.zero;

            var lines = new List<string>();
            for (int i = 0; i < LineUpCount; i++)
            {
                int seed = i + 1;
                string colour = i < LineUpColours.Length ? LineUpColours[i] : "";
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, lineUp.transform);
                copy.name = BodyName + "_" + seed + (colour.Length > 0 ? "_" + colour : "");
                copy.transform.localPosition = new Vector3((i - (LineUpCount - 1) * 0.5f) * LineUpSpacing, 0f, 0f);
                copy.transform.localRotation = Quaternion.identity;
                var body = copy.GetComponent<CharacterBody>();
                if (body == null) continue;
                body.WearRolled(seed, null, colour);
                lines.Add(seed + (colour.Length > 0 ? " " + colour : "") + ": " + body.LookText + " (" + body.Height.ToString("F2") + " tall)");
            }
            Selection.activeGameObject = lineUp;
            Debug.Log("[Characters] LooksLineUp: " + LineUpCount + " looks at " + lineUp.transform.position + " (nothing saved):\n" + string.Join("\n", lines));
        }
    }
}
#endif
