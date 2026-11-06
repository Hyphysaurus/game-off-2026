using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>One saved arrangement: everything placed, plus every scene prop that was moved or removed.</summary>
    [Serializable]
    public sealed class LayoutFile
    {
        /// <summary>Name start of the scene groups the editor's Bake Layout makes ("Baked_&lt;layout&gt;").</summary>
        public const string BakedPrefix = "Baked_";

        public int version = 2;   // 2: bakedRoots
        public string scene;
        public string saved;
        public List<LayoutItem> items = new List<LayoutItem>();
        /// <summary>The Baked_ groups that were in the scene when this was saved: the file only lists what changed on top of
        /// them. Empty in files from before version 2.</summary>
        public List<string> bakedRoots = new List<string>();
    }

    /// <summary>One thing in a layout. Exactly one of id (catalogue), cloneOf (copy of a scene prop) or sceneKey (the prop itself) is set.</summary>
    [Serializable]
    public sealed class LayoutItem
    {
        public string id;
        public string cloneOf;
        public string sceneKey;
        public bool removed;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        public bool hasShape;
        public Vector3 shapeSize;
        public int shapeSteps;
        public Color shapeColor = Color.white;

        public static LayoutItem From(in BuildState s, string id, string cloneOf, string sceneKey) => new LayoutItem
        {
            id = id, cloneOf = cloneOf, sceneKey = sceneKey, removed = !s.active,
            position = s.position, rotation = s.rotation, scale = s.scale,
            hasShape = s.hasShape, shapeSize = s.shapeSize, shapeSteps = s.shapeSteps, shapeColor = s.shapeColor,
        };

        public BuildState ToState() => new BuildState
        {
            position = position, rotation = rotation, scale = scale, active = !removed,
            hasShape = hasShape, shapeSize = shapeSize, shapeSteps = shapeSteps, shapeColor = shapeColor,
        };
    }

    /// <summary>Named layout files in persistentDataPath/Layouts, one JSON each.</summary>
    public static class BuildLayouts
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Layouts");

        public static string PathOf(string name) => Path.Combine(Folder, Clean(name) + ".json");

        /// <summary>Keeps letters, digits, space, dash and underscore; empty becomes "Layout".</summary>
        public static string Clean(string name)
        {
            var chars = (name ?? "").Trim().Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray();
            var s = new string(chars).Trim();
            if (s.Length > 48) s = s.Substring(0, 48);
            return s.Length == 0 ? "Layout" : s;
        }

        /// <summary>Saved names, newest first.</summary>
        public static List<string> List()
        {
            if (!Directory.Exists(Folder)) return new List<string>();
            return new DirectoryInfo(Folder).GetFiles("*.json")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => Path.GetFileNameWithoutExtension(f.Name))
                .ToList();
        }

        public static string Save(string name, LayoutFile file)
        {
            Directory.CreateDirectory(Folder);
            string path = PathOf(name);
            File.WriteAllText(path, JsonUtility.ToJson(file, true));
            return path;
        }

        public static bool TryLoad(string name, out LayoutFile file) => TryRead(PathOf(name), out file);

        public static bool TryRead(string path, out LayoutFile file)
        {
            file = null;
            try
            {
                if (!File.Exists(path)) return false;
                file = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(path));
                return file != null && file.items != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Layout '{path}' could not be read: {e.Message}");
                return false;
            }
        }

        public static void Delete(string name)
        {
            string path = PathOf(name);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
