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
        private const string HudPath = CameraDir + "/HubHud.prefab";
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
            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath);
            if (playerAsset != null) SaveNetPlayerVariant(playerAsset, null);   // one player for every scene (D-0143)
            else SavePlayer(player);
            SaveCamera(rig.gameObject);
            var hud = GameObject.Find("HUD");
            if (hud != null) SaveHud(hud);
            MakeStadium(body);
            EditorSceneManager.OpenScene(CoursePath, OpenSceneMode.Single);
            Debug.Log($"PartyBuilder: NetPlayer, HubCamera and {NetSession.PartyScene} built (toy body {body:0.##} units). Greybox reopened.");
        }

        /// <summary>Batch (D-0121): the editor need not be open. Opens Greybox, rebuilds the NetPlayer and the Stadium, builds
        /// the next Windows version (BuildTools) and quits (exit 1 on a failed build).
        /// -executeMethod ShiftJam.PartyBuilder.RebuildAndBuildBatch</summary>
        public static void RebuildAndBuildBatch()
        {
            EditorSceneManager.OpenScene(CoursePath, OpenSceneMode.Single);
            Build();
            int next = int.TryParse(PlayerSettings.bundleVersion, out int now) ? now + 1 : 1;
            bool ok = BuildTools.Windows(next);
            LLMUnity.LLMBuilder.Reset();   // put LLMUnity's moved libraries back now: quitting skips its own restore (3.4 GB left in LLMUnityBuild, 21 Nov)
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>The online player as a variant of the one Player prefab (D-0143): what every player has comes from it,
        /// and only the online parts are its own. The asset keeps its path and id, so the session's list still finds it.</summary>
        public static bool SaveNetPlayerVariant(GameObject playerAsset, System.Text.StringBuilder log)
        {
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(playerAsset);
            Online(copy);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(copy, PlayerPath, out bool ok);
            Object.DestroyImmediate(copy);
            // Saved from a scene instance, the network id on disk is the instance's; reimported, Netcode works out the
            // asset's own, and the rewrite stores it, so a build and the editor agree on it.
            AssetDatabase.ImportAsset(PlayerPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ForceReserializeAssets(new[] { PlayerPath });
            saved = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            bool variant = ok && saved != null && PrefabUtility.GetPrefabAssetType(saved) == PrefabAssetType.Variant;
            string line = variant ? $"NetPlayer: rebuilt as a variant of {PlayerPrefabBuilder.PrefabPath}" : "NetPlayer: NOT saved as a variant";
            if (log != null) log.AppendLine(line); else Debug.Log("PartyBuilder: " + line);
            return variant;
        }

        /// <summary>The old way, before the Player prefab: a plain copy of Greybox's player.</summary>
        private static void SavePlayer(GameObject player)
        {
            GameObject copy = Object.Instantiate(player);
            Online(copy);
            PrefabUtility.SaveAsPrefabAsset(copy, PlayerPath);
            Object.DestroyImmediate(copy);
        }

        /// <summary>Makes a player the online one: no course wake-up or room warps, the network parts on.</summary>
        private static void Online(GameObject copy)
        {
            copy.name = "NetPlayer";
            copy.transform.position = Vector3.zero;
            foreach (var c in copy.GetComponents<WakeUp>()) Object.DestroyImmediate(c);     // the course's wake-in-the-box
            foreach (var c in copy.GetComponents<ShiftWarp>()) Object.DestroyImmediate(c);  // the course's room warps
            var no = copy.AddComponent<NetworkObject>();
            no.AutoObjectParentSync = false;   // a car seat parents the driver to the car here; the position still syncs in world space (D-0116)
            var nt = copy.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;   // the owner moves; nobody waits on the host to walk
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = true;      // the shift scales the root
            nt.Interpolate = true;
            var na = copy.AddComponent<NetworkAnimator>();
            na.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;
            na.Animator = copy.GetComponentInChildren<Animator>(true);
            copy.AddComponent<NetPlayer>();
            copy.AddComponent<NetHealth>();   // D-0112: health and shield seen by everyone, hits handed to the owner
            var combat = copy.AddComponent<NetCombat>();   // D-0117: shots and the weapon in hand seen by everyone
            var cso = new SerializedObject(combat);
            var shots = new[] { "FoamDart", "PaintBall" }
                .Select(n => AssetDatabase.LoadAssetAtPath<Projectile>("Assets/_Game/Prefabs/Combat/" + n + ".prefab")).Where(x => x != null).ToArray();
            var shotList = cso.FindProperty("shots"); shotList.arraySize = shots.Length;
            for (int i = 0; i < shots.Length; i++) shotList.GetArrayElementAtIndex(i).objectReferenceValue = shots[i];
            var weapons = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<Weapon>(AssetDatabase.GUIDToAssetPath(g))).Where(x => x != null).ToArray();
            var weaponList = cso.FindProperty("weapons"); weaponList.arraySize = weapons.Length;
            for (int i = 0; i < weapons.Length; i++) weaponList.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            cso.ApplyModifiedPropertiesWithoutUndo();
            copy.AddComponent<NetNameTag>();   // D-0117: name, colour and health over other players' heads
            copy.AddComponent<NetWorld>();     // D-0131: the host runs the soldiers, props and pickups; their messages ride here
            Debug.Log($"PartyBuilder: NetCombat knows {shots.Length} shots, {weapons.Length} weapons ({string.Join(", ", weapons.Select(w => w.name))}).");
            if (copy.TryGetComponent(out Respawn respawn))
            {
                var so = new SerializedObject(respawn);
                so.FindProperty("loadSave").boolValue = false;   // the course's save slot would warp the player out of the hub
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>The course's HUD for the Stadium (D-0125): the "E" prompt (it finds this PC's own player by itself) and
        /// the screen fade (teleports, respawns). The course's goal text, start and end cards and its GameFlow stay out.</summary>
        private static void SaveHud(GameObject hud)
        {
            GameObject copy = Object.Instantiate(hud);
            copy.name = "HubHud";
            foreach (var flow in copy.GetComponentsInChildren<GameFlow>(true)) Object.DestroyImmediate(flow);
            foreach (string part in new[] { "StartCard", "EndCard", "GoalText" })
            {
                var t = copy.transform.Find(part);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(copy, HudPath);
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
            var packSky = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Imports/ithappy/Battle_Stadium/Skyboxes/Skybox_4.mat");   // the pack's own sky (D-0115)
            RenderSettings.skybox = packSky != null ? packSky : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.78f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.7f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.31f, 0.27f);

            StadiumLayout.Build(scene, body);   // the stadium, the field, the play pieces and the spawns (D-0113)
            AddRaceway(scene, body);            // the race track beside it, its cars online (D-0116)
            HouseDoor(scene, new Vector3(-(StadiumLayout.FieldRadius - 2.2f), 0f, -5f), Quaternion.Euler(0f, 90f, 0f), body);   // the arena's door to the house (D-0134): the race gate's twin across the middle
            // Versus shots are decided at the click here (D-0172, D-0176). The rule is made with the scene: this scene is
            // made afresh on every ship, and a rule placed in it by hand (as it first was) would be gone from the build.
            new GameObject("ShotRules").AddComponent<ShotRules>();   // the part's own default is Instant
            Vector3 centre = Vector3.zero;

            var hudAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if (hudAsset != null)
            {
                var hudCopy = (GameObject)PrefabUtility.InstantiatePrefab(hudAsset, scene);
                PrefabUtility.UnpackPrefabInstance(hudCopy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                hudCopy.name = "HUD";
            }

            var camAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
            var cam = (GameObject)PrefabUtility.InstantiatePrefab(camAsset, scene);
            PrefabUtility.UnpackPrefabInstance(cam, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            cam.transform.position = centre + new Vector3(0f, 3f * body, -6f * body);
            cam.transform.LookAt(centre);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuild(ScenePath);
        }

        private const string RacewayPath = "Assets/_Game/Scenes/Raceway.unity";
        private const float RacewayGap = 250f;   // far enough that the raceway room's 130-high walls stay under the stadium rim seen from the field

        /// <summary>
        /// The Raceway beside the stadium (D-0116, his "a race track in the multiplayer scene"): its whole built room
        /// (track, gates, finish line, garage, cars) copied in east of the stadium, the rival field switched off (each PC
        /// would run its own rivals), every car made a networked car (CarNet: the driver owns it). A gate on the arena's
        /// east side goes to the track, a gate at the track's start comes back.
        /// </summary>
        private static void AddRaceway(Scene scene, float body)
        {
            Scene source = EditorSceneManager.OpenScene(RacewayPath, OpenSceneMode.Additive);
            GameObject from = null; Vector3 arrive = Vector3.zero; Quaternion arriveFacing = Quaternion.identity;
            foreach (var r in source.GetRootGameObjects())
            {
                if (r.name == "Raceway") from = r;
                if (r.name == "Player") { arrive = r.transform.position; arriveFacing = r.transform.rotation; }
            }
            if (from == null) { EditorSceneManager.CloseScene(source, true); Debug.LogWarning("PartyBuilder: Raceway has no Raceway root."); return; }
            GameObject track = Object.Instantiate(from);
            track.name = "Raceway";
            SceneManager.MoveGameObjectToScene(track, scene);
            EditorSceneManager.CloseScene(source, true);

            // East of the stadium, clear of it.
            Bounds stadium = default; bool any = false;
            foreach (var r in scene.GetRootGameObjects()) if (r != track && r.name == "Stadium") { stadium = BoundsOf(r); any = true; }
            Bounds rw = BoundsOf(track);
            float shiftX = (any ? stadium.max.x : 80f) + RacewayGap - rw.min.x;
            Vector3 shift = new Vector3(shiftX, -rw.min.y + 0f, -rw.center.z);
            shift.y = 0f;   // the raceway keeps its own floor height (its floor is at 0 like ours)
            track.transform.position += shift;
            arrive += shift;

            foreach (var g in track.GetComponentsInChildren<RaceGrid>(true)) g.enabled = false;   // no rivals online
            // The room's walls and corner pillars stand 130 high and showed over the stadium's rim: unseen here, still solid
            // (they keep the cars on the floor). The floor, the furniture and the track stay as built.
            Transform roomShell = track.transform.Find("Shell");
            if (roomShell != null)
                foreach (Transform part in roomShell)
                    if (part.name.StartsWith("Wall_") || part.name.StartsWith("Pillar"))
                        foreach (var r in part.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            int cars = 0;
            foreach (var car in track.GetComponentsInChildren<ArcadeCar>(true))
            {
                var go = car.gameObject;
                go.transform.SetParent(null, true);   // a networked object at the scene's top
                SceneManager.MoveGameObjectToScene(go, scene);
                if (!go.TryGetComponent(out NetworkObject _)) go.AddComponent<NetworkObject>();
                var nt = go.GetComponent<NetworkTransform>(); if (nt == null) nt = go.AddComponent<NetworkTransform>();
                nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
                nt.Interpolate = true;
                if (!go.TryGetComponent(out NetworkRigidbody _)) go.AddComponent<NetworkRigidbody>();
                if (!go.TryGetComponent(out CarNet _)) go.AddComponent<CarNet>();
                cars++;
            }

            // The gates: an inflatable checkpoint arch on the arena's east edge, and one at the raceway's start.
            var toTrack = Gate(scene, "ToRaceway", new Vector3(StadiumLayout.FieldRadius - 2.2f, 0f, 5f), Quaternion.Euler(0f, -90f, 0f), body);   // on the rim, wherever the field's size puts it
            var outPoint = new GameObject("RacewayArrive").transform;
            SceneManager.MoveGameObjectToScene(outPoint.gameObject, scene);
            outPoint.SetPositionAndRotation(arrive + Vector3.up * 0.3f, arriveFacing);
            toTrack.SetTarget(outPoint);

            var backGate = Gate(scene, "ToStadium", arrive + arriveFacing * (Vector3.back * 4f * body), arriveFacing, body);
            var backPoint = new GameObject("StadiumArrive").transform;
            SceneManager.MoveGameObjectToScene(backPoint.gameObject, scene);
            backPoint.SetPositionAndRotation(new Vector3(19f, 0.3f, 5f), Quaternion.Euler(0f, -90f, 0f));
            backGate.SetTarget(backPoint);
            Debug.Log($"PartyBuilder: Raceway copied {shiftX:0} east of the stadium, {cars} cars online.");
        }

        private static HubTeleport Gate(Scene scene, string name, Vector3 at, Quaternion facing, float body)
        {
            var go = new GameObject(name, typeof(BoxCollider));
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetPositionAndRotation(at, facing);
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(body * 1.6f, body * 1.4f, body * 0.6f); box.center = new Vector3(0f, body * 0.7f, 0f);
            var arch = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Imports/ithappy/Battle_Stadium/Prefabs/Inflatable/checkpoint_001.prefab");
            if (arch != null)
            {
                var a = (GameObject)PrefabUtility.InstantiatePrefab(arch, go.transform);
                a.transform.localPosition = Vector3.zero; a.transform.localRotation = Quaternion.identity;
                a.transform.localScale = arch.transform.localScale * 0.3f;
            }
            return go.AddComponent<HubTeleport>();
        }

        /// <summary>
        /// The door to the house (D-0134, co-op step 3): an inflatable arch whose E takes the whole party to the playroom
        /// (NetTravel), landing at its garage door: the one that leads back to this Stadium's race track. A HubDoor, as the
        /// house's own doors are; the Stadium is only ever played online.
        /// </summary>
        private static void HouseDoor(Scene scene, Vector3 at, Quaternion facing, float body)
        {
            var go = new GameObject("ToHouse", typeof(BoxCollider));
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetPositionAndRotation(at, facing);
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(body * 1.6f, body * 1.4f, body * 0.6f); box.center = new Vector3(0f, body * 0.7f, 0f);
            var arch = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Imports/ithappy/Battle_Stadium/Prefabs/Inflatable/checkpoint_001.prefab");
            if (arch != null)
            {
                var a = (GameObject)PrefabUtility.InstantiatePrefab(arch, go.transform);
                a.transform.localPosition = Vector3.zero; a.transform.localRotation = Quaternion.identity;
                a.transform.localScale = arch.transform.localScale * 0.3f;
            }
            var door = go.AddComponent<HubDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("id").stringValue = "stadium.house";
            so.FindProperty("scene").stringValue = NetSession.HouseScene;
            so.FindProperty("arriveAt").stringValue = "playroom.raceway";
            so.FindProperty("prompt").stringValue = "Into the house";
            so.FindProperty("fade").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
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
