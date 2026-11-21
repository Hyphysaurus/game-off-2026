using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Throws grenades with the free (left) hand, after the test room's thrower: the grenade key (G) starts the arm's
    /// swing, the grenade leaves part-way through it from just in front of the chest, tilted up a little above where
    /// you look, and it takes the thrower's own speed with it. A few are carried; a supply crate fills them up (Add),
    /// and so does the reset key. No throw while a move is under way, the hands are full, in Build Mode or in a car.
    /// A kid's throw leaves from the kid's chest and is as many times faster as the kid is bigger; the grenade does the
    /// rest (Grenade). It tells the game what happened (GameEvents: GrenadesChanged, GrenadeThrown, GrenadeWentOff);
    /// the weapon read-out, the hit marker and the play log listen. The WeaponHolder of a player adds one of these;
    /// every number is in Resources/Grenades.
    /// </summary>
    [DefaultExecutionOrder(110)]   // after the WeaponHolder has put the arms on a gun: the throw goes on top
    public sealed class GrenadeThrower : MonoBehaviour, IResettable
    {
        private GrenadeSettings _s;
        private PlayerInput _input;
        private WeaponHolder _holder;
        private Shifter _shifter;
        private PlayerMotor _motor;
        private ThrowPose _pose;
        private float _wait, _swingLeft;
        private bool _released = true;

        /// <summary>Grenades carried now.</summary>
        public int Carried { get; private set; }
        /// <summary>The most that can be carried.</summary>
        public int MostCarried => _s.mostCarried;
        /// <summary>Seconds until the next throw may start; 0 = ready.</summary>
        public float Wait => _wait;
        /// <summary>True while the arm is swinging.</summary>
        public bool Swinging => _swingLeft > 0f;
        /// <summary>The arm's pose for the throw (read-backs for checks).</summary>
        public ThrowPose Pose => _pose;
        /// <summary>The grenade thrown last, while it exists.</summary>
        public Grenade Last { get; private set; }
        /// <summary>The numbers in use.</summary>
        public GrenadeSettings Settings => _s;

        private float Scale => _shifter != null ? _shifter.Scale : Mathf.Max(0.001f, transform.lossyScale.y);

        private void Awake()
        {
            _s = GrenadeSettings.Load();
            _input = GetComponent<PlayerInput>();
            _holder = GetComponent<WeaponHolder>();
            _shifter = GetComponent<Shifter>();
            _motor = GetComponent<PlayerMotor>();
            _pose = new ThrowPose(GetComponentInChildren<Animator>(), transform);   // here, before the Animator poses the arm
            Carried = Mathf.Min(_s.carried, _s.mostCarried);
        }

        private void OnEnable() { if (_input != null) _input.Grenade += OnKey; }

        private void OnDisable()
        {
            if (_input != null) _input.Grenade -= OnKey;
            // Seated, or the player is off: a swing under way is dropped (its grenade was already counted out).
            if (!_released) { _released = true; Carried = Mathf.Min(Carried + 1, _s.mostCarried); GameEvents.RaiseGrenadesChanged(this); }
            _swingLeft = 0f;
            _pose.Rest();
        }

        private void OnKey() => StartThrow();

        /// <summary>Starts a throw, as the key does. False when it cannot: none left, the last throw too recent, a move
        /// under way, the hands full, or no grenade set.</summary>
        public bool StartThrow()
        {
            if (!isActiveAndEnabled || Carried <= 0 || _wait > 0f || _s.grenade == null) return false;
            if (_holder != null && !_holder.FreeToAct) return false;
            Carried--;
            _wait = _s.cooldown;
            _swingLeft = _s.swingSeconds;
            _released = false;
            GameEvents.RaiseGrenadesChanged(this);
            return true;
        }

        /// <summary>Raised as a grenade leaves the hand: (thrower, where, velocity, spin, thrower's scale).</summary>
        public static event System.Action<GrenadeThrower, Vector3, Vector3, Vector3, float> Released;

        /// <summary>Adds grenades, up to the most that can be carried. False when already full (a pickup then stays).</summary>
        public bool Add(int count)
        {
            if (Carried >= _s.mostCarried || count <= 0) return false;
            Carried = Mathf.Min(Carried + count, _s.mostCarried);
            GameEvents.RaiseGrenadesChanged(this);
            return true;
        }

        /// <summary>Back from a knock-out (D-0112): at least the grenades carried at the start.</summary>
        public void Restock()
        {
            int start = Mathf.Min(_s.carried, _s.mostCarried);
            if (Carried >= start) return;
            Carried = start;
            GameEvents.RaiseGrenadesChanged(this);
        }

        /// <summary>The reset key: the grenades carried at the start again.</summary>
        public void ResetState()
        {
            int start = Mathf.Min(_s.carried, _s.mostCarried);
            if (Carried == start) return;
            Carried = start;
            GameEvents.RaiseGrenadesChanged(this);
        }

        private void Update()
        {
            _wait = Mathf.Max(_wait - Time.deltaTime, 0f);
            if (_swingLeft <= 0f) return;
            _swingLeft = Mathf.Max(_swingLeft - Time.deltaTime, 0f);
            float done = 1f - _swingLeft / _s.swingSeconds;
            _pose.Set(done, _s);
            if (done >= _s.releaseAt && !_released) { _released = true; LetGo(); }
            if (_swingLeft <= 0f) _pose.Rest();
        }

        private void LateUpdate() => _pose.Apply(_s, Scale);

        private void LetGo()
        {
            Vector3 look = _holder != null ? _holder.AimForward : transform.forward;
            Vector3 way = (look + Vector3.up * _s.loft).normalized;
            Transform alt = _holder != null ? _holder.FirstPersonBodyRoot : null;
            FirstPersonBody view = alt != null ? alt.GetComponent<FirstPersonBody>() : null;
            Transform feet = alt != null ? alt : transform;   // in first person the body under the eye is where we stand
            Vector3 flat = Vector3.ProjectOnPlane(look, Vector3.up);
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : transform.forward;
            Vector3 left = Vector3.Cross(flat, Vector3.up);
            // The grenade learns how big its thrower is as it leaves, as a shot does: a kid's leaves from the kid's
            // chest and is as many times faster as the kid is bigger.
            float size = _s.grenade.SizeFor(Scale);
            Vector3 from = feet.position + (left * _s.releasePoint.x + Vector3.up * _s.releasePoint.y + flat * _s.releasePoint.z) * size;
            Vector3 velocity = way * (_s.throwSpeed * size);
            // It takes the thrower's own speed with it. A body standing on the floor is held to it by a small
            // downward speed of its own: that is not the thrower moving, and it is left out.
            Vector3 own = Vector3.zero; bool grounded = false;
            if (view != null) { own = view.Velocity; grounded = view.Grounded; }
            else if (_motor != null && _motor.isActiveAndEnabled) { own = _motor.Velocity; grounded = _motor.Grounded; }
            if (grounded && own.y < 0f) own.y = 0f;
            velocity += own;
            Vector3 spin = new Vector3(Random.Range(-_s.spin, _s.spin), Random.Range(-_s.spin, _s.spin), Random.Range(-_s.spin, _s.spin));
            Grenade thrown = Instantiate(_s.grenade, from, Quaternion.identity);
            thrown.Throw(transform, alt, Scale, velocity, spin);
            Released?.Invoke(this, from, velocity, spin, Scale);   // online: NetCombat flies a copy on the other PCs (D-0117)
            thrown.WentOff += OnWentOff;
            Last = thrown;
            if (_s.throwClip != null) PaintSplats.InScene().Play(_s.throwClip, from);
            GameEvents.RaiseGrenadeThrown(this);
        }

        private void OnWentOff(int hits, bool finished) { if (this != null) GameEvents.RaiseGrenadeWentOff(this, hits, finished); }   // gone with its scene: nothing to tell
    }
}
