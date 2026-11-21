#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Party / Build Net Player + Stadium (D-0111). Run with Greybox open. Makes:
    ///  - Resources/Net/NetPlayer: a copy of Greybox's Player with a NetworkObject, owner-driven NetworkTransform (size
    ///    included) and NetworkAnimator, and NetPlayer. Course-only parts (WakeUp, ShiftWarp) are left off.
    ///  - Prefabs/Net/HubCamera: a copy of Greybox's camera (ShoulderCamera with no player yet: NetPlayer binds it).
    ///  - Scenes/Stadium: the online hub (StadiumLayout, D-0113): Battle Stadium's stadium round a field of play
    ///    pieces, the spawns, a late-afternoon sun and sky. Added to the build.
    /// Run it again any time: it rebuilds all three. Greybox is reopened at the end, untouched.
    /// </summary>
    public static class PartyBuilder
    {
        private const string NetDir = "Assets/_Game/Resources/Net";
        private const string PlayerPath = NetDir + "/NetPlayer.prefab";
        private const string CameraDir = "Assets/_Game/Prefabs/Net";
        private const string CameraPath = CameraDir + "/HubCamera.prefab";
        private const string ScenePath = "Assets/_Game/Scenes/" + NetSession.PartyScene + ".unity";
        private const string CoursePath = "Assets/_Game/Scenes/Greybox.unity";

        [MenuItem("Shift Jam/Party/Build Net Player + Stadium (D-0111)")]
        public static void Build()
        {
            Scene course = SceneManager.GetActiveScene();
            if (course.path != CoursePath) { Debug.LogError("PartyBuilder: open Greybox first."); return; }
            if (course.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GameObject player = GameObject.Find("Player");
            var rig = Object.FindFirstObjectByType<ShoulderCamera>();
            if (player == null || rig == null) { Debug.LogError("PartyBuilder: Greybox has no Player or no ShoulderCamera."); return; }

            Ensure(NetDir); Ensure(CameraDir);
            var cc = player.GetComponent<CharacterController>();
            float body = cc != null ? cc.height * player.transform.lossyScale.y : 1f;
            SavePlayer(player);
            SaveCamera(rig.gameObject);
            MakeStadium(body);
            EditorSceneManager.OpenScene(CoursePath, OpenSceneMode.Single);
            Debug.Log($"PartyBuilder: NetPlayer, HubCamera and {NetSession.PartyScene} built (toy body {body:0.##} units). Greybox reopened.");
        }

        private static void SavePlayer(GameObject player)
        {
            GameObject copy = Object.Instantiate(player);
            copy.name = "NetPlayer";
            copy.transform.position = Vector3.zero;
            foreach (var c in copy.GetComponents<WakeUp>()) Object.DestroyImmediate(c);     // the course's wake-in-the-box
            foreach (var c in copy.GetComponents<ShiftWarp>()) Object.DestroyImmediate(c);  // the course's room warps
            copy.AddComponent<NetworkObject>();
            var nt = copy.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;   // the owner moves; nobody waits on the host to walk
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = true;      // the shift scales the root
            nt.Interpolate = true;
            var na = copy.AddComponent<NetworkAnimator>();
            na.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;
            na.Animator = copy.GetComponentInChildren<Animator>(true);
            copy.AddComponent<NetPlayer>();
            copy.AddComponent<NetHealth>();   // D-0112: health and shield seen by everyone, hits handed to the owner
            if (copy.TryGetComponent(out Respawn respawn))
            {
                var so = new SerializedObject(respawn);
                so.FindProperty("loadSave").boolValue = false;   // the course's save slot would warp the player out of the hub
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(copy, PlayerPath);
            Object.DestroyImmediate(copy);
        }

        private static void SaveCamera(GameObject cam)
        {
            GameObject copy = Object.Instantiate(cam);
            copy.name = "HubCamera";
            copy.tag = "MainCamera";
            PrefabUtility.SaveAsPrefabAsset(copy, CameraPath);
            Object.DestroyImmediate(copy);
        }

        private static void MakeStadium(float body)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Late afternoon (his pick, 21 Nov): a low warm sun from the west, a warm sky light. COZY comes with the
            // visual pass's sky step.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1f, 0.86f, 0.68f); sun.intensity = 1.4f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(32f, -62f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.78f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.7f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.31f, 0.27f);

            StadiumLayout.Build(scene, body);   // the stadium, the field, the play pieces and the spawns (D-0113)
            Vector3 centre = Vector3.zero;

            var camAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
            var cam = (GameObject)PrefabUtility.InstantiatePrefab(camAsset, scene);
            PrefabUtility.UnpackPrefabInstance(cam, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            cam.transform.position = centre + new Vector3(0f, 3f * body, -6f * body);
            cam.transform.LookAt(centre);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuild(ScenePath);
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static void AddToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
