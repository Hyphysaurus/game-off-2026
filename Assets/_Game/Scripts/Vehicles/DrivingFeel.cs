using UnityEngine;

namespace ShiftJam
{
    /// <summary>What the driven car is doing this frame, as the feel parts need it (DrivingFeel fills it once a frame).</summary>
    public struct DriveState
    {
        /// <summary>A car is driven and its feedback should run: Play mode, not paused, no garage menu, not parked or wrecked.</summary>
        public bool Live;
        /// <summary>Speed as a share of the car's top speed: 1 = top speed, more while boosted.</summary>
        public float Speed;
        /// <summary>How far past top speed, 0..1 (1 = a full boost's speed, DrivingFeelSettings.boostFull).</summary>
        public float Over;
        /// <summary>A burst or nitro pushes now.</summary>
        public bool Boosting;
        /// <summary>A wheel is on the ground.</summary>
        public bool Grounded;
        /// <summary>Sideways speed as a share of top speed (0 in the air).</summary>
        public float Slip;
        /// <summary>Sliding hard enough for tyre smoke.</summary>
        public bool Sliding;
        /// <summary>The drift charge banked: 0, 1 or 2.</summary>
        public int DriftLevel;
        /// <summary>The drift charge as a number, 0..2: above 0 as soon as a slide starts to charge.</summary>
        public float DriftMeter;
    }

    /// <summary>
    /// The juice of driving, in one place that owns none of it: while the player drives an ArcadeCar it reads the car
    /// (speed, boost, slip, wheels) and listens to what the car reports (a boost starting, a landing, a hit), and hands
    /// that to small parts that each do one thing: the chase camera's kick and shake (ShoulderCamera), the speed lines
    /// (SpeedLines), wind, skid and thump (DrivingSound), the pad's rumble (DrivingRumble) and the tyre trails
    /// (CarTrails). Numbers: DrivingFeelSettings. Made by code at start like the driving HUD and lives across scenes; it
    /// follows <see cref="Driving.Vehicle"/>, so it works for any car with an ArcadeCar and costs next to nothing when
    /// nobody drives. Everything stops in Build Mode, in the garage menu, while paused, when the driver gets out and when
    /// the car is wrecked, parked or destroyed. The wooden ToyCar is not an ArcadeCar: it gets the camera's speed look
    /// (through IChaseTarget) and nothing from here.
    /// </summary>
    public sealed class DrivingFeel : MonoBehaviour
    {
        private DrivingFeelSettings _settings;
        private SpeedLines _lines;
        private DrivingSound _sound;
        private DrivingRumble _rumble;
        private CarTrails _trails;

        private Transform _vehicle;
        private ArcadeCar _car;
        private ShoulderCamera _camera;
        private bool _live;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<DrivingFeel>() != null) return;
            var go = new GameObject("DrivingFeel");
            DontDestroyOnLoad(go);
            go.AddComponent<DrivingFeel>();
        }

        private void Awake()
        {
            _settings = DrivingFeelSettings.Load();
            _lines = Part<SpeedLines>("SpeedLines");
            _sound = Part<DrivingSound>("DrivingSound");
            _rumble = Part<DrivingRumble>("DrivingRumble");
            _trails = Part<CarTrails>("CarTrails");
            _lines.Setup(_settings);
            _sound.Setup(_settings);
            _rumble.Setup(_settings);
            _trails.Setup(_settings);
        }

        private T Part<T>(string label) where T : Component
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        private void OnDisable() => Bind(null);

        private void Update()
        {
            Transform vehicle = Driving.Vehicle;
            if (vehicle != _vehicle) Bind(vehicle);

            var state = default(DriveState);
            // _car turns null (Unity's null) when the car is destroyed while driven: everything below simply stops
            _live = _car != null && GameMode.Current == GameMode.Mode.Play && !Garage.MenuOpen && Time.timeScale > 0f
                && _car.Occupied && !_car.Wrecked && !_car.Parked;
            if (_live)
            {
                float top = _car.ChaseTopSpeed;
                state.Live = true;
                state.Speed = top > 1e-3f ? _car.ChaseVelocity.magnitude / top : 0f;
                state.Over = Mathf.InverseLerp(1f, Mathf.Max(1.01f, _settings.boostFull), state.Speed);
                state.Boosting = _car.Boosting;
                state.Grounded = _car.ChaseGrounded;
                state.Slip = _car.Slip;
                state.Sliding = _car.Sliding;
                state.DriftLevel = _car.DriftLevel;
                state.DriftMeter = _car.DriftMeter;
            }

            float dt = Time.unscaledDeltaTime;
            _lines.Tick(state, dt);
            _sound.Tick(state, dt);
            _rumble.Tick(state, dt);
            _trails.Tick(state, _live ? _car : null);
        }

        /// <summary>Follows a new driven vehicle (null: nobody drives). Only an ArcadeCar gets the feel.</summary>
        private void Bind(Transform vehicle)
        {
            if (!ReferenceEquals(_car, null))   // also when it was destroyed: the handlers must go either way
            {
                _car.BoostStarted -= OnBoostStarted;
                _car.Landed -= OnLanded;
                _car.Crashed -= OnCrashed;
            }
            _vehicle = vehicle;
            _car = vehicle != null ? vehicle.GetComponentInParent<ArcadeCar>() : null;
            _camera = null;
            _live = false;
            if (_rumble != null) _rumble.Stop();   // the driver got out (or changed car): the motors rest
            if (_trails != null) _trails.Bind(_car);
            if (_car == null) return;
            _camera = FindFirstObjectByType<ShoulderCamera>();   // once per getting in, as the seat does
            _car.BoostStarted += OnBoostStarted;
            _car.Landed += OnLanded;
            _car.Crashed += OnCrashed;
        }

        // The car raises these from its physics step; they only set a few numbers, the parts play them out in Update.

        private void OnBoostStarted(float multiplier)
        {
            if (!_live) return;
            float k = Mathf.InverseLerp(1f, Mathf.Max(1.01f, _settings.boostFull), multiplier);
            if (k <= 0f) return;
            if (_camera != null)
            {
                _camera.PunchFov(_settings.boostFovPunch * k, _settings.boostFovSeconds);
                _camera.Shake(_settings.boostShake * k, _settings.boostShakeSeconds);
            }
            _rumble.Kick(_settings.rumbleBoostKick * k);
        }

        private void OnLanded(float fallSpeed)
        {
            if (!_live) return;
            float k = Mathf.InverseLerp(_settings.landFrom, Mathf.Max(_settings.landFrom + 0.01f, _settings.landFull), fallSpeed);
            if (k <= 0f) return;
            if (_camera != null) _camera.Shake(_settings.landShake * k, _settings.landShakeSeconds);
            _rumble.Kick(_settings.rumbleLand * k);
            _sound.Thump(k);
        }

        private void OnCrashed(float impact)
        {
            if (!_live) return;
            float k = Mathf.InverseLerp(_settings.crashFrom, Mathf.Max(_settings.crashFrom + 0.01f, _settings.crashFull), impact);
            if (k <= 0f) return;
            if (_camera != null) _camera.Shake(_settings.crashShake * k, _settings.crashShakeSeconds);
            _rumble.Kick(_settings.rumbleCrash * k);
        }
    }
}
