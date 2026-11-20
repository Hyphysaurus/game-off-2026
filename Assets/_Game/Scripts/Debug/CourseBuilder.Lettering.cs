#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The 3D lettering sample (D-0098): the hallway's first sign as marker letters on a strip of packing tape (they pop
    /// on as the toy comes near), "THIS WAY UP" and "FRAGILE" stamped inside the toy box (they tumble off when it bursts),
    /// and the tall end's sign worded so the way out is not looked for in the ceiling (D-0097). Idempotent.
    /// Menu: Shift Jam / Text / Land Lettering Sample. Batch: -executeMethod ShiftJam.CourseBuilder.LandLettering.
    /// </summary>
    public static partial class CourseBuilder
    {
        const string LetterFonts = "Assets/Plugins/Tiny Giant Studio/Modular 3D Text/Fonts/";
        const string MarkerFont = LetterFonts + "Permanent Marker Regular.asset";
        const string StampFont = LetterFonts + "Bungee.asset";
        const string GrowKidWords = "Up on the shelf: the core makes you small.\nThe way out: the glowing hole in the end wall, on the shelf.";

        [MenuItem("Shift Jam/Text/Land Lettering Sample")]
        public static void LandLettering()
        {
            Log.Clear();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var r3 = GameObject.Find("Room/Course/R3_SkirtingRun");
            if (r3 == null) { Debug.LogError("D-0098: no R3 in " + ScenePath); if (Application.isBatchMode) EditorApplication.Exit(1); return; }

            // the first sign: marker on tape, where the card was
            var old = r3.transform.Find("Sign1_Move");
            if (old != null)
            {
                Vector3 at = old.position; Vector3 toViewer = -old.forward;
                Object.DestroyImmediate(old.gameObject);
                TapeSign("Sign1_Move", r3.transform, at, toViewer, "MOVE  W A S D\nJUMP  SPACE", 7f, 3f);
                Log.AppendLine($"Sign1_Move is tape at {at}");
            }
            else Log.AppendLine("no Sign1_Move: run LandHouse first");

            // the tall end's sign
            var grow = r3.transform.Find("Sign3_GrowKid");
            var tmp = grow != null ? grow.GetComponentInChildren<TextMeshPro>() : null;
            if (tmp != null) { tmp.text = GrowKidWords; EditorUtility.SetDirty(tmp); Log.AppendLine("Sign3_GrowKid reworded"); }

            BoxStamps();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("D-0098 LETTERING LANDED\n" + Log);
        }

        /// <summary>A sign as a strip of packing tape with marker letters on it, read from <paramref name="toViewer"/>.</summary>
        static GameObject TapeSign(string name, Transform parent, Vector3 centre, Vector3 toViewer, string words, float width, float height)
        {
            toViewer.y = 0f; toViewer.Normalize();
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(-toViewer));   // read from its -Z side, as the cards
            var tape = Mat("Assets/_Game/Data/PackingTape.mat", new Color(0.78f, 0.62f, 0.38f));
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube); strip.name = "Tape"; Object.DestroyImmediate(strip.GetComponent<Collider>());
            strip.transform.SetParent(root.transform, false);
            strip.transform.localPosition = new Vector3(0f, 0f, 0.04f);
            strip.transform.localRotation = Quaternion.Euler(0f, 0f, -1.5f);   // stuck on by hand
            strip.transform.localScale = new Vector3(width * 1.08f, height * 1.05f, 0.05f);
            strip.GetComponent<Renderer>().sharedMaterial = tape;
            Letters(root.transform, "Lettering", new Vector3(0f, 0f, -0.02f), words, width * 0.9f, MarkerFont, Ink("Assets/_Game/Data/MarkerInk.mat", new Color(0.09f, 0.09f, 0.12f)), Lettering.Reveal.WhenNear, 0.12f);
            return root;
        }

        /// <summary>Ink stamped on the inside of the toy box's long walls, readable from inside (the opening looks at them).</summary>
        static void BoxStamps()
        {
            var box = GameObject.Find("Room/ToyBox");
            if (box == null) { Log.AppendLine("no Room/ToyBox: no stamps"); return; }
            var stampInk = Ink("Assets/_Game/Data/StampInk.mat", new Color(0.62f, 0.12f, 0.1f));
            int n = 0;
            foreach (var (wall, words, up, w) in new[] { ("BackWall", "THIS WAY UP", 5f, 18f), ("FrontWall", "FRAGILE", 2f, 14f) })
            {
                var t = box.transform.Find(wall); if (t == null) continue;
                foreach (Transform c in t) if (c.name.StartsWith("Stamp")) { Object.DestroyImmediate(c.gameObject); break; }
                var floor = box.transform.Find("Floor");
                Vector3 mid = floor != null ? new Vector3(floor.position.x, t.position.y, floor.position.z) : box.transform.position;
                Vector3 inward = mid - t.position; inward.y = 0f; inward.Normalize();
                var mesh = t.Find("Mesh"); float half = mesh != null ? mesh.lossyScale.z * 0.5f : 0.5f;
                var holder = new GameObject("Stamp_" + words.Replace(' ', '_'));
                holder.transform.SetParent(t, false);
                holder.transform.SetPositionAndRotation(t.position + inward * (half + 0.03f) + Vector3.up * up, Quaternion.LookRotation(-inward));
                Letters(holder.transform, "Lettering", Vector3.zero, words, w, StampFont, stampInk, Lettering.Reveal.Always, 0.06f);
                n++;
            }
            Log.AppendLine($"{n} stamps in the toy box");
        }

        static void Letters(Transform parent, string name, Vector3 local, string words, float width, string fontPath, Material ink, Lettering.Reveal reveal, float depth)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = local;
            var l = go.AddComponent<Lettering>();
            var so = new SerializedObject(l);
            so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TinyGiantStudio.Text.Font>(fontPath);
            so.FindProperty("material").objectReferenceValue = ink;
            so.FindProperty("words").stringValue = words;
            so.FindProperty("width").floatValue = width;
            so.FindProperty("depth").floatValue = depth;
            so.FindProperty("reveal").enumValueIndex = (int)reveal;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (so.FindProperty("font").objectReferenceValue == null) Log.AppendLine("MISSING FONT " + fontPath);
        }

        static Material Ink(string path, Color c)
        {
            var m = Mat(path, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            return m;
        }
    }
}
#endif
