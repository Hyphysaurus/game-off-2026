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
    ///  - Scenes/Stadium: the online hub's first cut. Battle Stadium's bowl sized to the toy (its floor about
    ///    StadiumAcross body heights across), the one spawn in the middle, a sun and sky light. Added to the build.
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
        private const string BowlPath = "Assets/_Imports/ithappy/Battle_Stadium/Prefabs/Stadium/battle_stadium_001.prefab";
        private const float StadiumAcross = 60f;   // the bowl's width in the toy's body heights

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

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1f, 0.95f, 0.85f); sun.intensity = 1.3f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.6f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.28f);

            // Battle Stadium's bowl is a ring of stands with an open middle (probed 21 Nov): the field is ours, a round
            // floor under the ring, its top level with the ring's foot. The real arena dressing is the Stadium pass.
            var bowlAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BowlPath);
            float across = StadiumAcross * body, ground = 0f;
            if (bowlAsset != null)
            {
                var bowl = (GameObject)PrefabUtility.InstantiatePrefab(bowlAsset, scene);
                bowl.name = "Bowl";
                Bounds b = BoundsOf(bowl);
                float wide = Mathf.Max(b.size.x, b.size.z);
                if (wide > 1e-3f) bowl.transform.localScale *= across / wide;
                b = BoundsOf(bowl);
                bowl.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);   // middle on the origin, foot on 0
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Field";
            Object.DestroyImmediate(floor.GetComponent<Collider>());   // a cylinder's own collider is a capsule: rounded on top
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            float thick = body * 0.5f;
            floor.transform.localScale = new Vector3(across, thick * 0.5f, across);   // the cylinder mesh is 2 tall
            floor.transform.position = new Vector3(0f, ground - thick * 0.5f, 0f);
            Vector3 centre = new Vector3(0f, ground, 0f);

            var spawn = new GameObject("Spawn").AddComponent<NetSpawnPoint>();
            spawn.transform.position = centre;

            var camAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
            var cam = (GameObject)PrefabUtility.InstantiatePrefab(camAsset, scene);
            PrefabUtility.UnpackPrefabInstance(cam, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            cam.transform.position = centre + new Vector3(0f, 2f * body, -4f * body);
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
