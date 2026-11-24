using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Co-op step 3 (D-0134): doors take the group. Online, a player who uses a door between rooms (HubDoor: the playroom's
    /// doors, the Colour Corner's way home, the Stadium's door to the house) asks the host, which starts a countdown on
    /// every screen ("Going to the Colour Corner in 5", NetScoreboard); at zero the host loads that room for the whole
    /// party and everyone lands together at the door it leads to (OnlineWorld puts the first spawn there). The players
    /// themselves go with the load (their size, weapons, charge and glider are on them); a tool in the hand stays in its
    /// room. Through the countdown everyone is kept out of cars: a seat parents its driver to the car, and the car goes
    /// with the old scene. Online the Raceway room is the Stadium's track (its cars are networked there, D-0116): the
    /// playroom's garage door takes the party to it. One trip at a time; a press during the countdown changes nothing.
    /// </summary>
    public static class NetTravel
    {
        /// <summary>Seconds from the press to the load.</summary>
        public const float CountdownSeconds = 5f;
        /// <summary>The Stadium's point at the start of its copy of the Raceway (PartyBuilder).</summary>
        public const string TrackArrival = "RacewayArrive";

        private static bool _going, _arriving;
        private static string _scene, _arriveAt;
        private static float _leavesAt;

        /// <summary>True while a countdown runs.</summary>
        public static bool Going => _going && NetSession.InParty;
        /// <summary>Where the party is going, as the countdown says it ("the Colour Corner").</summary>
        public static string Label { get; private set; } = "";
        /// <summary>Seconds until the load.</summary>
        public static float SecondsLeft => _going ? Mathf.Max(0f, _leavesAt - Time.unscaledTime) : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetAll() { _going = _arriving = false; _scene = _arriveAt = null; Label = ""; }

        /// <summary>This PC's player used a door to <paramref name="scene"/> (arriving at the door or point named
        /// <paramref name="arriveAt"/> there): ask the host to take everyone (on the host, take them).</summary>
        public static void Ask(string scene, string arriveAt)
        {
            if (!NetSession.InParty || _going || string.IsNullOrEmpty(scene)) return;
            Route(ref scene, ref arriveAt);
            var nm = NetworkManager.Singleton;
            if (nm.IsServer) Begin(scene, arriveAt);
            else if (NetWorld.Local != null && NetWorld.Local.IsSpawned) NetWorld.Local.TravelRpc(scene, arriveAt ?? "");
        }

        /// <summary>Online, the Raceway room is the Stadium's track: its cars are the networked ones (the room's own are
        /// single-player cars, each PC's own).</summary>
        private static void Route(ref string scene, ref string arriveAt)
        {
            if (scene == "Raceway") { scene = NetSession.PartyScene; arriveAt = TrackArrival; }
        }

        /// <summary>On the host: start the countdown for everyone, unless one runs, the room is not in the build, or a team
        /// match is on (D-0182: the doors are shut; a friend's ask sent just before the match started stops here).</summary>
        public static void Begin(string scene, string arriveAt)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || _going || NetWorld.Host == null || !Application.CanStreamedLevelBeLoaded(scene)) return;
            if (TeamMatch.Running) return;
            NetWorld.Host.GoingRpc(scene, arriveAt ?? "", CountdownSeconds);
        }

        /// <summary>Everyone: the host started the countdown. The arrival is kept for that room's load (it may come in
        /// before this PC's own countdown ends: the load is the host's).</summary>
        public static void OnGoing(string scene, string arriveAt, float seconds)
        {
            _scene = scene; _arriveAt = arriveAt;
            _going = true; _arriving = true;
            _leavesAt = Time.unscaledTime + seconds;
            Label = LabelFor(scene, arriveAt);
            UiAudio.Play(UiAudio.Kind.Toast);
        }

        /// <summary>Each frame on every PC (NetWorld): through the countdown this PC's player is kept out of cars; at zero
        /// the host loads the room for everyone.</summary>
        public static void Tick()
        {
            if (!_going) return;
            if (!NetSession.InParty) { _going = false; return; }
            LeaveVehicle();
            if (Time.unscaledTime < _leavesAt) return;
            _going = false;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            SceneEventProgressStatus status = nm.SceneManager.LoadScene(_scene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started) Debug.LogWarning($"NetTravel: the host could not load {_scene} for the party ({status}).");
        }

        /// <summary>A party world just loaded (OnlineWorld): if the party came through a door to it, where they land (that
        /// door's arrival point, or a point of that name); else null. Asked once a trip.</summary>
        public static Transform ArrivalIn(Scene scene)
        {
            if (!_arriving || scene.name != _scene) return null;
            _arriving = false;
            if (string.IsNullOrEmpty(_arriveAt)) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (HubDoor door in root.GetComponentsInChildren<HubDoor>(true))
                    if (door.Id == _arriveAt) return door.ArrivePoint != null ? door.ArrivePoint : door.transform;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == _arriveAt) return t;
            Debug.LogWarning($"NetTravel: {scene.name} has no door or point named {_arriveAt}: the party lands at the usual spawn.");
            return null;
        }

        private static string LabelFor(string scene, string arriveAt)
        {
            if (scene == NetSession.PartyScene) return arriveAt == TrackArrival ? "the race track" : "the Stadium";
            if (scene == NetSession.HouseScene) return "the playroom";
            if (scene == "ColourCorner") return "the Colour Corner";
            return scene;
        }

        /// <summary>Out of whatever this PC's player drives: a car's seat, or a toy car. Also the start of a team match
        /// (D-0182: NetPlayer.ToBase).</summary>
        public static void LeaveVehicle()
        {
            Transform vehicle = Driving.Vehicle;
            if (vehicle == null) return;
            if (vehicle.TryGetComponent(out CarSeat seat)) { if (seat.Occupied) seat.GetOut(false); }
            else if (vehicle.TryGetComponent(out ToyCar toy)) toy.Eject();
        }
    }
}
