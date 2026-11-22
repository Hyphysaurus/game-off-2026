#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    public static partial class CourseBuilder
    {
        /// <summary>How far the landing book of a book ladder sits under the last step's top (toy units, D-0139).</summary>
        const float BookLandingSink = 0.05f;
        const string BookStairsPrefab = "Assets/_Game/Prefabs/Pieces/Traversal/BookStairs.prefab";

        /// <summary>
        /// Book ladders already in the scenes (D-0139): the last two books (the last step and the landing past it) had their
        /// tops at the same height, so where they overlap the two covers flickered. Sinks the landing book by
        /// BookLandingSink in every open scene and in the BookStairs piece; the steps keep their heights. A ladder already
        /// right is left alone, so it is safe to run again. Saves what it changed.
        /// </summary>
        [MenuItem("Shift Jam/House/Fix Book Ladder Tops (D-0139)")]
        public static void FixBookLadderTops()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Book ladder tops: stop Play first."); return; }
            int fixedScenes = 0, ladders = 0;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                int here = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) here += SinkLanding(t);
                if (here == 0) continue;
                ladders += here; fixedScenes++;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            int inPiece = 0;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BookStairsPrefab) != null)
            {
                GameObject piece = PrefabUtility.LoadPrefabContents(BookStairsPrefab);
                foreach (Transform t in piece.GetComponentsInChildren<Transform>(true)) inPiece += SinkLanding(t);
                if (inPiece > 0) PrefabUtility.SaveAsPrefabAsset(piece, BookStairsPrefab);
                PrefabUtility.UnloadPrefabContents(piece);
            }
            Debug.Log($"Book ladder tops: {ladders} ladder(s) fixed in {fixedScenes} scene(s), {inPiece} in the BookStairs piece.");
        }

        /// <summary>A ladder holder (children Book0, Book1, ...): when its last book tops out level with the one before,
        /// the last goes down by BookLandingSink. Returns 1 when it moved one.</summary>
        static int SinkLanding(Transform holder)
        {
            if (holder.Find("Book0") == null || holder.Find("Book1") == null) return 0;
            int last = 1;
            while (holder.Find("Book" + (last + 1)) != null) last++;
            GameObject landing = holder.Find("Book" + last).gameObject, step = holder.Find("Book" + (last - 1)).gameObject;
            if (landing.GetComponentInChildren<Renderer>() == null || step.GetComponentInChildren<Renderer>() == null) return 0;
            if (Mathf.Abs(B(landing).max.y - B(step).max.y) > 0.01f) return 0;
            Undo.RecordObject(landing.transform, "Sink book ladder landing");
            landing.transform.position += Vector3.down * BookLandingSink;
            Debug.Log($"Book ladder tops: {LadderPath(holder)} {landing.name} down {BookLandingSink}");
            return 1;
        }

        static string LadderPath(Transform t) => t.parent == null ? t.name : LadderPath(t.parent) + "/" + t.name;
    }
}
#endif
