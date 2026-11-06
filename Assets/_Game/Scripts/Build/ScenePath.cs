using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// A name path to an object in a scene, e.g. "Room/Course/R4_Playroom/Chair~1" (~1 = the second sibling with that
    /// name). Layout files use it to find props that belong to the scene, at run time and when baking in the editor.
    /// </summary>
    public static class ScenePath
    {
        private static readonly List<GameObject> Roots = new List<GameObject>();

        public static string Of(Transform t)
        {
            var parts = new List<string>();
            for (var at = t; at != null; at = at.parent) parts.Add(Segment(at));
            parts.Reverse();
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++) { if (i > 0) sb.Append('/'); sb.Append(parts[i]); }
            return sb.ToString();
        }

        public static Transform Find(Scene scene, string path)
        {
            if (string.IsNullOrEmpty(path) || !scene.IsValid()) return null;
            string[] parts = path.Split('/');
            Parse(parts[0], out string name, out int nth);
            scene.GetRootGameObjects(Roots);
            Transform at = null;
            int seen = 0;
            foreach (var root in Roots)
            {
                if (root.name != name) continue;
                if (seen++ == nth) { at = root.transform; break; }
            }
            for (int p = 1; p < parts.Length && at != null; p++)
            {
                Parse(parts[p], out name, out nth);
                Transform next = null;
                seen = 0;
                for (int c = 0; c < at.childCount; c++)
                {
                    var child = at.GetChild(c);
                    if (child.name != name) continue;
                    if (seen++ == nth) { next = child; break; }
                }
                at = next;
            }
            return at;
        }

        private static string Segment(Transform t)
        {
            int nth = 0;
            if (t.parent != null)
            {
                for (int c = 0; c < t.parent.childCount; c++)
                {
                    var sibling = t.parent.GetChild(c);
                    if (sibling == t) break;
                    if (sibling.name == t.name) nth++;
                }
            }
            else
            {
                t.gameObject.scene.GetRootGameObjects(Roots);
                foreach (var root in Roots)
                {
                    if (root.transform == t) break;
                    if (root.name == t.name) nth++;
                }
            }
            return nth == 0 ? t.name : t.name + "~" + nth;
        }

        private static void Parse(string segment, out string name, out int nth)
        {
            nth = 0; name = segment;
            int tilde = segment.LastIndexOf('~');
            if (tilde > 0 && int.TryParse(segment.Substring(tilde + 1), out int n)) { name = segment.Substring(0, tilde); nth = n; }
        }
    }
}
