using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// "Drive" with E. Seats the player in an ArcadeCar: the motor, capsule, interactor, carrier and weapons are switched
    /// off and the player rides parked in the seat (hidden unless showDriver), the move stick drives, Jump is the
    /// handbrake, and the shoulder camera chases the car. E again gets out: going fast, the car brakes first and lets you
    /// out once slow; the exit spot is checked (ground under it, room to stand) on either side, behind, in front, then the
    /// last safe ground. A wreck throws the driver out at once, with a moment of protection.
    /// While seated, Sprint (Shift) burns the car's nitro and, stopped, Jump + forward winds its pull-back launch (ArcadeCar).
    /// Getting in and out sets <see cref="Driving.Vehicle"/> (the driving HUD and the race read it).
    /// A car a rival is driving cannot be got into. In a race with a field the race keeps the driver in a wrecked car
    /// (<see cref="KeepDriverInWreck"/>): it puts the car back on the road itself.
    /// </summary>
    [RequireComponent(typeof(ArcadeCar))]
    public sealed class CarSeat : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("Where the driver sits, car space. Zero = middle of the car, at hub height.")] private Vector3 seat;
        [SerializeField, Tooltip("Show the driver in the seat (open-top cars).")] private bool showDriver;

        private ArcadeCar _car;
        private Transform _driver, _driverParent;
        private PlayerInput _input;
        private PlayerMotor _motor;
        private CharacterController _cc;
        private Shifter _shifter;
        private ShoulderCamera _camera;
        private Animator _anim;
        private readonly List<Behaviour> _parked = new List<Behaviour>();
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private readonly Collider[] _overlap = new Collider[8];
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private float _enteredAt;
        private bool _wantsOut;

        public bool Occupied => _driver != null;
        /// <summary>The seated player, or null.</summary>
        public Transform Driver => _driver;
        /// <summary>Set by a race for the player's car (RaceGrid): a wreck does not throw the driver out, because the race
        /// puts the car back on the road, whole, with the driver still in it.</summary>
        public bool KeepDriverInWreck { get; set; }

        private void Awake()
        {
            _car = GetComponent<ArcadeCar>();
            if (seat == Vector3.zero)
            {
                Bounds b = CarBounds();
                seat = transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * 0.25f, b.center.z));
            }
        }

        private void OnEnable() => _car.WreckedNow += OnWreck;
        private void OnDisable() { _car.WreckedNow -= OnWreck; if (Occupied) GetOut(true); }

        private Bounds CarBounds()
        {
            var rs = GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(transform.position, Vector3.one);
            foreach (var r in rs)
                if (r != null && !(r is ParticleSystemRenderer) && !(_driver != null && r.transform.IsChildOf(_driver))) b.Encapsulate(r.bounds);   // flames trail far behind
            return b;
        }

        private bool TooBig(Interactor who)
        {
            float driver = who.Shifter != null ? who.Shifter.Scale : who.transform.lossyScale.y;
            float limit = _car.Settings != null ? _car.Settings.maxDriverScale : 1.5f;
            return driver > _car.Scale * limit;
        }

        public string PromptFor(Interactor who)
        {
            if (Occupied || _car.Occupied || who == null || GameMode.Current != GameMode.Mode.Play) return null;   // the car's own Occupied: a rival has it
            if (_car.Wrecked) return "Wrecked";
            if (TooBig(who)) return "Too big to drive";
            var carrier = who.GetComponent<Carrier>();
            if (carrier != null && carrier.Held != null) return "Hands full";
            return "Drive";
        }

        public void Interact(Interactor who)
        {
            if (Occupied || _car.Occupied || who == null || _car.Wrecked || TooBig(who) || GameMode.Current != GameMode.Mode.Play) return;
            var carrier = who.GetComponent<Carrier>();
            if (carrier != null && carrier.Held != null) return;
            _driver = who.transform;
            _driverParent = _driver.parent;
            _input = who.GetComponent<PlayerInput>();
            _motor = who.GetComponent<PlayerMotor>();
            _cc = who.GetComponent<CharacterController>();
            _shifter = who.GetComponent<Shifter>();
            _anim = who.GetComponentInChildren<Animator>();

            _parked.Clear();
            Park(_motor); Park(who); Park(carrier); Park(who.GetComponent<WeaponHolder>());
            if (_cc != null) _cc.enabled = false;
            _driver.SetParent(transform, true);
            _driver.localPosition = seat;
            _driver.localRotation = Quaternion.identity;
            if (_anim != null) { _anim.SetFloat("Speed", 0f); _anim.SetBool("Grounded", true); _anim.SetFloat("VerticalVelocity", 0f); }
            _hidden.Clear();
            if (!showDriver)
                foreach (var r in _driver.GetComponentsInChildren<Renderer>())
                    if (!r.forceRenderingOff) { r.forceRenderingOff = true; _hidden.Add(r); }

            _car.Drive(_input);
            _input.Interact += OnExitPressed;
            _enteredAt = Time.time;
            _wantsOut = false;
            _camera = PlayerSlot.RigFor(_input);   // the driver's own camera (D-0110)
            if (_camera != null) _camera.SetChase(transform, 0f, _car.Settings != null ? _car.Settings.chaseDistance : 1.8f);
            Driving.Set(transform);
        }

        private void Park(Behaviour b)
        {
            if (b == null || !b.enabled) return;
            b.enabled = false;
            _parked.Add(b);
        }

        private void OnExitPressed()
        {
            if (!Occupied || Time.time - _enteredAt < 0.25f) return;
            _wantsOut = true;
        }

        private void OnWreck() { if (Occupied && !KeepDriverInWreck) GetOut(true); }

        private void Update()
        {
            if (!Occupied) return;
            if (_driver == null) { Clear(); return; }   // the player was destroyed while seated
            if (!_wantsOut) return;
            float limit = (_car.Settings != null ? _car.Settings.maxExitSpeed : 5f) * _car.Scale;
            _car.ForceBrake = _car.Speed > limit;
            if (!_car.ForceBrake) GetOut(false);
        }

        /// <summary>Puts the driver back on foot beside the car. <paramref name="thrown"/>: a wreck's ejection, with a hop and protection.</summary>
        public void GetOut(bool thrown)
        {
            if (!Occupied) return;
            Vector3 momentum = Vector3.ClampMagnitude(_car.Body.linearVelocity, 6f * _car.Scale);
            Vector3 spot = ExitSpot();
            Transform driver = _driver;
            _input.Interact -= OnExitPressed;
            _car.Drive(null);
            // Unity refuses a reparent while this car is being switched off (Play stopping, a scene unloading): skip it then.
            if (!Driving.Quitting && gameObject.activeInHierarchy) driver.SetParent(_driverParent, true);
            foreach (var r in _hidden) if (r != null) r.forceRenderingOff = false;
            _hidden.Clear();
            if (_cc != null) _cc.enabled = true;
            foreach (var b in _parked) if (b != null) b.enabled = true;
            _parked.Clear();
            float yaw = transform.eulerAngles.y;
            if (_motor != null)
            {
                _motor.Warp(spot, Quaternion.Euler(0f, yaw, 0f));
                _motor.Knock(thrown ? new Vector3(momentum.x, 5f * _motor.Scale, momentum.z) : momentum * 0.5f);
            }
            else driver.SetPositionAndRotation(spot, Quaternion.Euler(0f, yaw, 0f));
            var health = thrown ? driver.GetComponent<Damageable>() : null;   // plain form: Fast Script Reload breaks `&& TryGetComponent(out ...)`
            if (health != null) health.Protect(1.5f);
            if (_camera != null) _camera.SetChase(null);
            Clear();
        }

        private void Clear()
        {
            if (Driving.Vehicle == transform) Driving.Set(null);
            _driver = null; _input = null; _motor = null; _cc = null; _anim = null; _shifter = null;
            _wantsOut = false; _car.ForceBrake = false;
        }

        /// <summary>A free spot to stand: beside, behind, in front, last safe ground, else the roof.</summary>
        private Vector3 ExitSpot()
        {
            Bounds b = CarBounds();
            float playerScale = _shifter != null ? _shifter.Scale : _driver.lossyScale.y;
            float radius = (_cc != null ? _cc.radius : 0.3f) * playerScale;
            float height = (_cc != null ? _cc.height : 1.8f) * playerScale;
            Vector3 c = transform.InverseTransformPoint(b.center);
            Vector3 ext = transform.InverseTransformVector(b.extents);
            float side = Mathf.Abs(ext.x) + radius * 1.5f, end = Mathf.Abs(ext.z) + radius * 1.5f;
            Vector3[] local =
            {
                new Vector3(-side, 0f, c.z), new Vector3(side, 0f, c.z),
                new Vector3(-side, 0f, c.z - end * 0.5f), new Vector3(side, 0f, c.z - end * 0.5f),
                new Vector3(c.x, 0f, c.z - end), new Vector3(c.x, 0f, c.z + end),
            };
            float probeUp = b.size.y + height;
            foreach (var l in local)
            {
                Vector3 p = transform.TransformPoint(new Vector3(l.x, 0f, l.z));
                if (!Ground(p + Vector3.up * probeUp, probeUp * 2f, out Vector3 floor)) continue;
                if (floor.y < b.min.y - height) continue;   // a drop: do not step out over an edge
                if (Fits(floor, radius, height)) return floor;
            }
            var safe = _driver.GetComponent<SafeGround>();
            if (safe != null) return safe.LastSafe;
            return new Vector3(b.center.x, b.max.y + 0.05f * playerScale, b.center.z);
        }

        private bool Ground(Vector3 from, float length, out Vector3 point)
        {
            point = default;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, _hits, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].transform.IsChildOf(transform) || _hits[i].normal.y < 0.6f) continue;
                if (_hits[i].distance < nearest) { nearest = _hits[i].distance; point = _hits[i].point; found = true; }
            }
            return found;
        }

        private bool Fits(Vector3 feet, float r, float h)
        {
            Vector3 a = feet + Vector3.up * (r + 0.05f * r), top = feet + Vector3.up * Mathf.Max(r * 1.1f, h - r);
            int n = Physics.OverlapCapsuleNonAlloc(a, top, r * 0.95f, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!_overlap[i].transform.IsChildOf(_driver) && !HitShapes.IsShape(_overlap[i])) return false;   // a figure's hit shape stops nothing
            return true;
        }
    }
}
