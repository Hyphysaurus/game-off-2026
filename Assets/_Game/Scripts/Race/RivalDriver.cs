using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A rival at the wheel (D-0061). It gives an ArcadeCar the same four things a player's hands give it (steer and
    /// throttle, handbrake, nitro, "my controls are live") and nothing else: the car's own physics does the driving, so
    /// a rival can do nothing the player's car cannot. Each physics step it aims at a point further along the RacePath,
    /// in its lane (RivalLane moves the lane aside for other cars); reads the road's suggested speeds ahead and asks for
    /// the speed it can still slow down from in time; lights nitro on a straight; and backs up when it is stopped
    /// against something. Its character is a RivalProfile, every number is in RivalSettings. The grid (RaceGrid) puts it
    /// on a car for the race and takes it off after. It never uses the handbrake.
    /// </summary>
    [DefaultExecutionOrder(-10)]   // before the car's own physics step reads it
    [RequireComponent(typeof(ArcadeCar))]
    public sealed class RivalDriver : MonoBehaviour, ICarDriver
    {
        private const float LeastPace = 0.2f;      // however far the rubber band and a lap of honour ease it, it still drives
        private const float TrafficSlack = 0.5f;   // behind a car it brakes when over the wanted speed by this share of settings.blockedSpeed

        private ArcadeCar _car;
        private Racer _me;
        private RacePath _path;
        private RivalSettings _settings;
        private RivalProfile _profile;
        private Vector2 _move;
        private bool _nitro;
        private float _lane;            // where across the road it aims now, easing toward the lane it wants
        private float _blocked;         // seconds stopped while wanting to go
        private float _reverseUntil = -1f;

        public Vector2 Move => _move;
        public bool JumpHeld => false;
        public bool Sprint => _nitro;

        private void Awake() { _car = GetComponent<ArcadeCar>(); }

        /// <summary>Takes the wheel for a race: which car in the field it is, the road, the numbers and its character.</summary>
        public void Take(Racer me, RacePath path, RivalSettings settings, RivalProfile profile)
        {
            _me = me; _path = path; _settings = settings; _profile = profile;
            _lane = me != null ? me.Lateral : 0f;
            _blocked = 0f; _reverseUntil = -1f;
            _move = Vector2.zero; _nitro = false;
        }

        private void OnDisable() { _move = Vector2.zero; _nitro = false; }

        private void FixedUpdate()
        {
            _move = Vector2.zero;
            bool nitroWas = _nitro;
            _nitro = false;
            if (_me == null || _path == null || _settings == null || _me.Home || _car.Wrecked || _car.Parked) return;
            var stage = RaceField.Now;
            if (stage != RaceField.Stage.Racing && stage != RaceField.Stage.Done) return;   // on the grid the lights hold every car
            float top = _car.TopSpeed, s = _car.Scale, dt = Time.fixedDeltaTime;
            if (top <= 0f) return;

            Vector3 up = transform.up, nose = transform.forward;
            float speed = Vector3.Dot(_car.Body.linearVelocity, nose);

            // how hard it tries: its character, the rubber band, and an easy pace once it has finished
            float band = Band();
            float pace = Mathf.Clamp(_profile.pace * band, LeastPace, 1f);
            float corner = _settings.cornerSpeedFactor * _profile.cornerPace * band;
            if (_me.Finished) { pace *= _settings.coolDownPace; corner *= _settings.coolDownPace; }

            // where across the road
            float half = Mathf.Max(0f, _path.HalfWidthAt(_me.Distance) - _settings.wallMargin * s);
            float lane = RivalLane.Choose(_me, RaceField.Racers, _path, _settings, _profile.lane * half, half, s, out float limit);
            _lane = Mathf.MoveTowards(_lane, lane, _settings.laneChange * s * dt);

            // steer at a point further along the road, in that lane, measured in the car's own plane (so it is the
            // same sum upside down on the loop)
            float look = _settings.lookAhead * s + _settings.lookAheadPerSpeed * Mathf.Abs(speed);
            _path.Sample(_me.Distance + look, out Vector3 road, out Vector3 roadUp, out Vector3 roadForward);
            Vector3 aim = Vector3.ProjectOnPlane(road + Vector3.Cross(roadUp, roadForward) * _lane - transform.position, up);
            float off = aim.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(nose, aim, up) : 0f;
            float steer = Mathf.Clamp(off / Mathf.Max(1f, _settings.steerFullAngle), -1f, 1f);

            // the fastest it may go here and still be slow enough for everything it can see ahead
            float roadSpeed = float.MaxValue, slowing = 2f * _settings.brakeDecel * s;
            float step = Mathf.Max(0.5f, _path.Spacing);
            for (float d = 0f; d <= _settings.brakeLook * s; d += step)
            {
                float v = _path.SpeedAt(_me.Distance + d) * corner * s;
                roadSpeed = Mathf.Min(roadSpeed, Mathf.Sqrt(v * v + slowing * d));
            }
            float want = Mathf.Min(top * pace, Mathf.Min(roadSpeed, limit));
            if (Mathf.Abs(off) > _settings.lostAngle) want = Mathf.Min(want, _settings.turnRoundSpeed * s);

            // stopped (against a wall, or behind a car it has no room to steer round): back up, wheel the other way, try again
            float crawl = _settings.blockedSpeed * s;
            if (Time.time < _reverseUntil) { _move = new Vector2(-steer, -1f); return; }
            if (Mathf.Abs(speed) < crawl)
            {   // (GO itself is not a stop: the car is past a crawl within a step or two)
                _blocked += dt;
                if (_blocked >= _settings.blockedSeconds) { _blocked = 0f; _reverseUntil = Time.time + _settings.reverseSeconds; }
            }
            else _blocked = 0f;

            // the car's own engine holds a share of top speed, so the throttle IS the speed asked for; well over it
            // (for a car in the way: at all over it), brake
            bool traffic = limit < want + 0.01f;
            float over = traffic ? TrafficSlack * crawl : _settings.brakeOver * s;
            float throttle = speed > want + over && speed > crawl ? -1f : Mathf.Clamp01(want / top);
            _move = new Vector2(steer, throttle);

            // nitro: a straight ahead, already at speed, pointing down the road, nobody in the way, enough in the meter
            _nitro = !_me.Finished && limit == float.MaxValue
                && roadSpeed >= top * _settings.nitroRoad && speed >= top * _settings.nitroMinSpeed
                && Mathf.Abs(steer) <= _settings.nitroMaxSteer
                && _car.Nitro > (nitroWas ? 0f : _profile.nitroFrom);
        }

        /// <summary>The rubber band: a rival far ahead of the player eases off a little, one far behind pushes a little.
        /// 1 = its own pace.</summary>
        private float Band()
        {
            if (!_settings.rubberBand) return 1f;
            Racer player = RaceField.Player;
            if (player == null || player == _me || player.Finished || _me.Finished) return 1f;
            float gap = player.Odometer - _me.Odometer;   // positive: the player is ahead of this rival
            float t = Mathf.Clamp(gap / Mathf.Max(1f, _settings.rubberDistance), -1f, 1f);
            return 1f + (t > 0f ? t * _settings.rubberPush : t * _settings.rubberEase);
        }
    }
}
