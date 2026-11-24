#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShiftJam
{
    /// <summary>
    /// Editor maker for the bots' pieces (D-0188). Menu "Shift Jam > Bots > Build Bot Pieces (D-0188)" (batch:
    /// -executeMethod ShiftJam.BotPieces.BuildAll) does, in order:
    ///  1. Resources/BotSkills: the bots' levels (Easy, Normal, Hard, with the numbers written in BotSkillSettings.cs),
    ///     made only when missing. One already there is never written: its numbers are the owner's.
    ///  2. Prefabs/Pieces/Online/BotGround: a BotGround with its own defaults (drop it in any scene and size its box),
    ///     made only when missing, in a scene of its own.
    ///  3. Prefabs/Pieces/Interactables/FunnySoldier gains a BotBrain if it has none (the plastic ToySoldier it is a
    ///     variant of stays without one). A project without the prefab (or its pack) is told so, and the rest goes on.
    ///  4. Prefabs/Pieces/Online/TeamMatch (PartyBuilder makes it with the Stadium; it may not be there yet): its Bot
    ///     Skills set to the asset of step 1 when it is empty. One the owner set is left as it is.
    /// Safe to run twice: each step does nothing when its work is there. Refused in Play; no open scene is touched or
    /// saved. It ends with one line, "BotPieces: built ..." (or "BotPieces: NOT built ...").
    /// </summary>
    public static class BotPieces
    {
        private const string ResourcesDir = "Assets/_Game/Resources";
        /// <summary>Where the bots' levels are made.</summary>
        public const string SkillsPath = ResourcesDir + "/" + BotSkillSettings.ResourceName + ".asset";
        private const string OnlineDir = "Assets/_Game/Prefabs/Pieces/Online";
        /// <summary>Where the drop-in BotGround prefab is made.</summary>
        public const string GroundPrefabPath = OnlineDir + "/BotGround.prefab";
        private const string MatchPrefabPath = OnlineDir + "/TeamMatch.prefab";

        /// <summary>Makes or brings up to date the bots' pieces (see the class notes) and logs one closing line.</summary>
        [MenuItem("Shift Jam/Bots/Build Bot Pieces (D-0188)")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("BotPieces: NOT built: stop Play first; nothing was changed.");
                return;
            }
            var said = new List<string>();
            BotSkillSettings skills = MakeSkills(said);
            MakeGround(said);
            GiveSoldierABrain(said);
            PointMatchAtSkills(skills, said);
            AssetDatabase.SaveAssets();
            Debug.Log("BotPieces: built " + string.Join("; ", said) + ".");
        }

        /// <summary>Resources/BotSkills, made with the three starting levels when there is none; one already there is
        /// returned untouched.</summary>
        private static BotSkillSettings MakeSkills(List<string> said)
        {
            var skills = AssetDatabase.LoadAssetAtPath<BotSkillSettings>(SkillsPath);
            if (skills != null)
            {
                said.Add(SkillsPath + " kept (" + (skills.levels != null ? skills.levels.Length : 0) + " levels; your numbers are untouched)");
                return skills;
            }
            WorkshopPieces.EnsureFolder(ResourcesDir);
            skills = ScriptableObject.CreateInstance<BotSkillSettings>();
            AssetDatabase.CreateAsset(skills, SkillsPath);
            said.Add(SkillsPath + " made (" + LevelNames(skills) + "; a match starts on " + StartName(skills) + ")");
            return skills;
        }

        /// <summary>Prefabs/Pieces/Online/BotGround, made in a scene of its own when there is none.</summary>
        private static void MakeGround(List<string> said)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GroundPrefabPath) != null)
            {
                said.Add(GroundPrefabPath + " kept");
                return;
            }
            WorkshopPieces.EnsureFolder(OnlineDir);
            bool saved = false;
            Scene bench = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject made = ObjectFactory.CreateGameObject(bench, HideFlags.None, "BotGround", typeof(BotGround));
                PrefabUtility.SaveAsPrefabAsset(made, GroundPrefabPath, out saved);
                Object.DestroyImmediate(made);
            }
            finally { EditorSceneManager.ClosePreviewScene(bench); }
            said.Add(GroundPrefabPath + (saved ? " made (its own defaults: size its box over the floor in each scene)" : " NOT made (it could not be saved)"));
        }

        /// <summary>The funny soldier gains a BotBrain if it has none.</summary>
        private static void GiveSoldierABrain(List<string> said)
        {
            string path = SoldierBodyPieces.PrefabPath;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                said.Add("no " + path + " (Shift Jam > Characters > Build Funny Soldier makes it; the Stadium's layout gives its soldiers a brain itself)");
                return;
            }
            if (prefab.GetComponent<BotBrain>() != null)
            {
                said.Add(path + " already has a BotBrain");
                return;
            }
            if (prefab.GetComponent<ToySoldier>() == null)
            {
                said.Add(path + " has NO ToySoldier: no BotBrain added");
                return;
            }
            using (var open = new PrefabUtility.EditPrefabContentsScope(path))
            {
                if (!open.prefabContentsRoot.TryGetComponent(out BotBrain _)) open.prefabContentsRoot.AddComponent<BotBrain>();
            }
            said.Add(path + " gained a BotBrain");
        }

        /// <summary>The team match prefab's Bot Skills set to <paramref name="skills"/> when it is empty.</summary>
        private static void PointMatchAtSkills(BotSkillSettings skills, List<string> said)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MatchPrefabPath);
            if (prefab == null)
            {
                said.Add("no " + MatchPrefabPath + " yet (Shift Jam > Party > Build Net Player + Stadium makes it; a match with none set loads Resources/BotSkills itself)");
                return;
            }
            if (!prefab.TryGetComponent(out TeamMatch match))
            {
                said.Add(MatchPrefabPath + " has NO TeamMatch: its Bot Skills not set");
                return;
            }
            if (new SerializedObject(match).FindProperty("botSkills").objectReferenceValue != null)
            {
                said.Add(MatchPrefabPath + "'s Bot Skills already set (kept)");
                return;
            }
            if (skills == null)
            {
                said.Add(MatchPrefabPath + "'s Bot Skills left empty (no asset to point at)");
                return;
            }
            using (var open = new PrefabUtility.EditPrefabContentsScope(MatchPrefabPath))
            {
                if (open.prefabContentsRoot.TryGetComponent(out TeamMatch inside))
                {
                    var so = new SerializedObject(inside);
                    so.FindProperty("botSkills").objectReferenceValue = skills;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            said.Add(MatchPrefabPath + "'s Bot Skills set to " + SkillsPath);
        }

        private static string LevelNames(BotSkillSettings skills)
        {
            if (skills.levels == null || skills.levels.Length == 0) return "no levels";
            var names = new List<string>();
            foreach (BotSkill level in skills.levels) names.Add(level != null ? level.name : "(empty)");
            return string.Join(", ", names);
        }

        private static string StartName(BotSkillSettings skills)
        {
            if (skills.levels == null || skills.levels.Length == 0) return "the match's own level";
            BotSkill first = skills.levels[Mathf.Clamp(skills.startLevel, 0, skills.levels.Length - 1)];
            return first != null ? first.name : "(empty)";
        }
    }
}
#endif
