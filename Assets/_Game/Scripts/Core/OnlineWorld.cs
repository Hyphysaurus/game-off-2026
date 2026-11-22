using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The house as an online world (D-0125, his "co-op as a sandbox is fine"). When a scene other than the Stadium loads
    /// while the network runs (host or a friend joining: NetSession.InParty, true before the session is set up on a
    /// joining PC), it is made a sandbox:
    ///  - the scene's own single-player Player (no NetworkObject) is removed before its Start: the online players stand
    ///    in for it, and the scene camera is bound to this PC's own one (NetPlayer);
    ///  - every Door opens and every dormant core wakes: no gates, no puzzles in the way;
    ///  - spawns: the course's own arrival spots become NetSpawnPoints (the playroom's Entry first, everyone's landing;
    ///    the wardrobe front and the hallway's arrivals for comebacks).
    /// The shift costs nothing online (ShiftCharge), doors to other rooms wait for group travel (HubDoor), the title
    /// never opens (TitleMenu). In every party world, the Stadium too, the host runs the soldiers, breakable props and
    /// pickups for everyone (CoopWorld, D-0131).
    /// </summary>
    public static class OnlineWorld
    {
        /// <summary>Arrival spots of the house, in spawn order (the first is everyone's landing).</summary>
        private static readonly string[] Spawns =
        {
            "Room/Course/R4_Playroom/Entry",
            "Room/Course/R4_Playroom/WardrobeFront",
            "Room/Course/R3_SkirtingRun/FromPlayroom",
            "Room/Course/R3_SkirtingRun/Start",
        };

        /// <summary>True while this scene runs with the online sandbox rules.</summary>
        public static bool Active { get; private set; }

        private static int _appliedHandle = -1;   // the scene the rules were last put on (Apply runs at most once a scene)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            Active = false;
            _appliedHandle = -1;
            CoopWorld.Clear();
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            Active = false;
            CoopWorld.Clear();   // the last world's soldiers, props and pickups went with it
            Apply(scene);
        }

        /// <summary>
        /// Put the online rules on the active scene if they are not on it yet (D-0126). A scene load is not enough: a friend
        /// who joins from the house's own title screen is already in the house, and Netcode deliberately does not load the
        /// active scene again (DefaultSceneManagerHandler.ClientShouldPassThrough), so no load event ever comes. The own
        /// online player calls this as it appears.
        /// </summary>
        public static void EnsureApplied() => Apply(SceneManager.GetActiveScene());

        private static void Apply(Scene scene)
        {
            if (!NetSession.InParty || !scene.IsValid() || !scene.isLoaded) return;
            bool house = scene.name != NetSession.PartyScene;   // the Stadium is built for the party: none of the house's rules
            if (scene.handle == _appliedHandle) { Active = house; return; }
            _appliedHandle = scene.handle;
            CoopWorld.Bind(scene);   // every party world: the host runs its soldiers, props and pickups (D-0131); before the player is removed below
            if (!house) return;
            Active = true;

            // The title (and the party list over it) may still be up underneath: the friend joined from it and no load came
            // to clear it. It had stopped time, frozen the camera and freed the mouse: undo all three.
            foreach (var title in Object.FindObjectsByType<TitleMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (title != null) Object.Destroy(title.gameObject);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            foreach (var cam in Object.FindObjectsByType<ShoulderCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!cam.enabled) cam.enabled = true;   // its OnEnable locks the mouse again

            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var motor in root.GetComponentsInChildren<PlayerMotor>(true))
                    if (motor.GetComponentInParent<NetworkObject>() == null)
                    {
                        motor.gameObject.SetActive(false);   // off now, so its Start (HUDs, the charge) never runs; gone at the frame's end
                        Object.Destroy(motor.gameObject);
                        removed++;
                    }

            int doors = 0;
            foreach (var door in Object.FindObjectsByType<Door>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!door.IsOpen) { door.Open(); doors++; }
            int cores = 0;
            foreach (var core in Object.FindObjectsByType<ShiftSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (core.Dormant) { core.Awaken(); cores++; }

            int spawns = 0;
            if (Object.FindFirstObjectByType<NetSpawnPoint>() == null)
            {
                var holder = new GameObject("OnlineSpawns");
                SceneManager.MoveGameObjectToScene(holder, scene);
                foreach (string path in Spawns)
                {
                    var marker = GameObject.Find(path);
                    if (marker == null) continue;
                    var go = new GameObject("Spawn_" + marker.name);
                    go.transform.SetParent(holder.transform, false);
                    go.transform.SetPositionAndRotation(Grounded(marker.transform.position), Quaternion.Euler(0f, marker.transform.eulerAngles.y, 0f));
                    var point = go.AddComponent<NetSpawnPoint>();
                    if (spawns == 0) point.MarkFirst(true);
                    spawns++;
                }
            }
            Debug.Log($"OnlineWorld: {scene.name} as an online sandbox: {removed} single-player player(s) removed, {doors} doors opened, {cores} cores woken, {spawns} spawns.");
        }

        /// <summary>The floor under an arrival marker (markers can float a little).</summary>
        private static Vector3 Grounded(Vector3 at)
        {
            return Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : at;
        }
    }
}
