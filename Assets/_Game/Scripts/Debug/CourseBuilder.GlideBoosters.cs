#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Air boosters for the glider (D-0104), from the traversal pieces already made: a desk fan (UpdraftFan, its wind
    /// carries a gliding toy up) off the east edge of the playroom desk where the plane lies, three boost rings (BoostRing:
    /// BoostPad now also pushes a gliding toy along its flight) in a line south off the top of that column, and one ring
    /// lying flat under the pencil bridge's gap downstairs, flown through on the way down. Idempotent (everything under a
    /// "GlideBoosters" object per room). Menu: Shift Jam / House / Land Glide Boosters (D-0104).
    /// </summary>
    public static partial class CourseBuilder
    {
        const string FanPiece = "Assets/_Game/Prefabs/Pieces/Traversal/UpdraftFan.prefab";
        const string RingPiece = "Assets/_Game/Prefabs/Pieces/Traversal/BoostRing.prefab";
        const float RingSize = 3f;   // the piece is 2.5 across at toy scale: 7.5 is a target from the air

        [MenuItem("Shift Jam/House/Land Glide Boosters (D-0104)")]
        public static void LandGlideBoosters()
        {
            Log.Clear();
            var active = SceneManager.GetActiveScene();
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            var r4 = GameObject.Find("Room/Course/R4_Playroom");
            var down = GameObject.Find("Room/Course/Downstairs");
            var fan = AssetDatabase.LoadAssetAtPath<GameObject>(FanPiece);
            var ring = AssetDatabase.LoadAssetAtPath<GameObject>(RingPiece);
            if (r4 == null || fan == null || ring == null) { Debug.LogError($"D-0104: R4 {(r4 != null)}, fan {(fan != null)}, ring {(ring != null)}"); return; }

            var root = Fresh(r4.transform, "GlideBoosters");
            var f = (GameObject)PrefabUtility.InstantiatePrefab(fan, root);
            f.name = "DeskFan"; f.transform.SetPositionAndRotation(new Vector3(405f, 0f, -8f), Quaternion.Euler(0f, -90f, 0f));
            Log.AppendLine($"boosters: a desk fan at {f.transform.position}, its column 23..93 up");
            Vector3[] path = { new Vector3(425f, 82f, -45f), new Vector3(445f, 74f, -95f), new Vector3(465f, 66f, -145f) };
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 next = i + 1 < path.Length ? path[i + 1] : path[i] + (path[i] - path[i - 1]);
                Ring(ring, root, "Ring_" + (i + 1), path[i], Quaternion.LookRotation(next - path[i]));
            }
            if (down != null)
            {
                var droot = Fresh(down.transform, "GlideBoosters");
                Ring(ring, droot, "GapRing", new Vector3((HallBridge0 + HallBridge1) / 2f, -60f, LaneZ), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            }
            else Log.AppendLine("boosters: no Downstairs (run Land Downstairs first): no gap ring");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("D-0104 GLIDE BOOSTERS LANDED\n" + Log);
        }

        static Transform Fresh(Transform parent, string name)
        {
            var old = parent.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject);
            var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }

        static void Ring(GameObject piece, Transform parent, string name, Vector3 at, Quaternion facing)
        {
            var r = (GameObject)PrefabUtility.InstantiatePrefab(piece, parent);
            r.name = name;
            r.transform.localScale = Vector3.one * RingSize;
            r.transform.SetPositionAndRotation(at, facing);
            var sink = r.GetComponent<RestSink>(); if (sink != null) sink.enabled = false;   // it hangs in the air: nothing to rest on
            Log.AppendLine($"boosters: {name} at {at}");
        }
    }
}
#endif
