using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Throws grenades with the free (left) hand, after the test room's thrower: the grenade key (G) starts the arm's
    /// swing, the grenade leaves part-way through it from just in front of the chest, tilted up a little above where
    /// you look, and it takes the thrower's own speed with it. A few are carried; a supply crate fills them up (Add),
    /// and so does the reset key. No throw while a move is under way, the hands are full, in Build Mode or in a car.
    /// A kid's throw leaves from the kid's chest and is as many times faster as the kid is bigger; the grenade does the
    /// rest (Grenade). How hard it is thrown is how long the key was held (D-0184): a tap is a short lob, a full hold
    /// (Charge Seconds) throws further than the old single throw did, and the arc shown while the key is held grows
    /// with it, so the landing ring is the aim. It tells the game what happened (GameEvents: GrenadesChanged, GrenadeThrown, GrenadeWentOff);
    /// the weapon read-out, the hit marker and the play log listen. The WeaponHolder of a player adds one of these;
    /// every number is in Resources/Grenades. Online (D-0130), each throw's start is told to the other PCs (Started),
    /// where the thrower on this player's copy, made look-only (UseLookOnly), swings the same arm and throws nothing.
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
        private bool _wasHeld;          // the key was held last frame
        private float _heldSince;       // when this hold began (game time: a pause does not charge)
        private float _charge;          // how far this hold has charged, 0 to 1; 0 when the key is up
        private float _thrownPower = 1f;   // the power of the throw under way, fixed as the key is let go
        private bool _look;   // another player's copy on this PC (D-0130): it swings the arm when told and throws nothing

        /// <summary>True on another player's copy on this PC: it only swings the arm, when NetCombat says.</summary>
        public bool LookOnly => _look;
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
        /// <summary>How far the hold under way has charged, 0 (just pressed, or the key is up) to 1 (the hardest throw).</summary>
        public float Charge => _charge;
        /// <summary>How hard the last throw left, as a share of Throw Speed (1 = the old single throw).</summary>
        public float LastPower => _thrownPower;

        // Another player's copy: their Shifter does not run here, but the network sizes the body with their shift.
        private float Scale => _shifter != null && !_look ? _shifter.Scale : Mathf.Max(0.001f, transform.lossyScale.y);

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

        // The key is let go: the throw is as hard as the hold was long (the charge of the frame before: this frame's
        // Update has not run yet, and it would read the key as up).
        private void OnKey() => StartThrow(PowerAt(_charge));

        /// <summary>Starts a throw at the old single speed (Throw Speed), for whatever throws without a key held. False
        /// when it cannot: none left, the last throw too recent, a move under way, the hands full, or no grenade set.</summary>
        public bool StartThrow() => StartThrow(1f);

        /// <summary>Starts a throw that leaves at <paramref name="power"/> times Throw Speed. False as above.</summary>
        public bool StartThrow(float power)
        {
            if (_look || !isActiveAndEnabled || Carried <= 0 || _wait > 0f || _s.grenade == null) return false;
            if (_holder != null && !_holder.FreeToAct) return false;
            _thrownPower = Mathf.Max(0.01f, power);
            Carried--;
            _wait = _s.cooldown;
            _swingLeft = _s.swingSeconds;
            _released = false;
            GameEvents.RaiseGrenadesChanged(this);
            Started?.Invoke(this);
            return true;
        }

        /// <summary>Raised as a throw starts, before the grenade leaves (online: NetCombat has the other PCs swing this
        /// player's arm too, D-0130).</summary>
        public static event System.Action<GrenadeThrower> Started;

        /// <summary>Raised as a grenade leaves the hand: (thrower, where, velocity, spin, thrower's scale).</summary>
        public static event System.Action<GrenadeThrower, Vector3, Vector3, Vector3, float> Released;

        /// <summary>
        /// Online (D-0130): this is another player's copy on this PC. From now on it reads no key and throws nothing; it
        /// only swings the free arm when their throw starts (SwingLook). Their grenade's flight comes from their PC
        /// (NetCombat). The key is let go first.
        /// </summary>
        public void UseLookOnly()
        {
            if (_look) return;
            enabled = false;   // OnDisable lets go of the key while _input is still set
            _input = null;
            _look = true;
            enabled = true;
        }

        /// <summary>Another player's copy: their throw started. The free arm winds up and swings through as theirs does
        /// (the grenade itself leaves from their PC at the same point in the swing). One under way starts over.</summary>
        public void SwingLook()
        {
            if (!_look) return;
            _swingLeft = _s.swingSeconds;
            _released = true;   // nothing to let go here
        }

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

        /// <summary>The throw's power for a hold charged this far (0 to 1), as a share of Throw Speed: from the shortest
        /// lob to the hardest throw along the charge curve. With charging off (Charge Seconds 0): always 1.</summary>
        private float PowerAt(float charge)
        {
            if (_s.chargeSeconds <= 0f) return 1f;
            float along = _s.chargeCurve != null && _s.chargeCurve.length > 0 ? Mathf.Clamp01(_s.chargeCurve.Evaluate(Mathf.Clamp01(charge))) : Mathf.Clamp01(charge);
            return Mathf.Lerp(_s.leastThrow, Mathf.Max(_s.leastThrow, _s.mostThrow), along);
        }

        /// <summary>The hold: it starts charging as the key goes down and stops at full; with the key up there is none.</summary>
        private void Charging()
        {
            bool held = !_look && _input != null && _input.GrenadeHeld;
            if (held && !_wasHeld) _heldSince = Time.time;
            _charge = !held ? 0f : _s.chargeSeconds > 0f ? Mathf.Clamp01((Time.time - _heldSince) / _s.chargeSeconds) : 1f;
            _wasHeld = held;
        }

        private void Update()
        {
            Charging();
            _wait = Mathf.Max(_wait - Time.deltaTime, 0f);
            if (_swingLeft <= 0f) return;
            _swingLeft = Mathf.Max(_swingLeft - Time.deltaTime, 0f);
            float done = 1f - _swingLeft / _s.swingSeconds;
            _pose.Set(done, _s);
            if (done >= _s.releaseAt && !_released) { _released = true; LetGo(); }
            if (_swingLeft <= 0f) _pose.Rest();
        }

        private void LateUpdate()
        {
            _pose.Apply(_s, Scale);
            ShowArc();
        }

        private ArcPreview _arc;
        private readonly List<Vector3> _path = new List<Vector3>();

        /// <summary>The throw arc (D-0148): while a throw is lined up (G held, or the pad's right stick in) and one could
        /// be thrown now, the path it would take from this frame's release point and speed (worked out exactly as the
        /// throw does, Release) to where it first lands, and a ring there as wide as the blast, red when the thrower
        /// stands inside it.</summary>
        private void ShowArc()
        {
            bool lining = _s.showArc && !_look && _input != null && _input.GrenadeHeld && Carried > 0 && _swingLeft <= 0f
                          && _s.grenade != null && (_holder == null || _holder.FreeToAct) && Time.timeScale > 0f;
            if (!lining) { if (_arc != null) _arc.Hide(); return; }
            if (_arc == null) _arc = ArcPreview.For(transform);
            if (_arc == null) return;
            Release(PowerAt(_charge), out Vector3 from, out Vector3 velocity, out Transform alt);   // as hard as letting go now would throw it
            float size = _s.grenade.SizeFor(Scale);
            var flight = _s.grenade.GetComponent<BallFlight>();
            if (flight == null) { _arc.Hide(); return; }
            bool lands = flight.Predict(from, velocity, size, transform, alt, _s.grenade.Fuse, _path, out Vector3 landing, out Vector3 normal);
            float blast = _s.grenade.BlastRadiusAt(size);
            Transform feet = alt != null ? alt : transform;
            bool danger = Vector3.Distance(feet.position, landing) < blast;
            _arc.Show(_path, _s.arcWidth * size, _s.arcColour, _s.arcDashes, landing, normal, lands ? blast : 0f, danger ? _s.dangerColour : _s.ringColour);
        }

        private void OnDestroy() { if (_arc != null) Destroy(_arc.gameObject); }

        private void LetGo()
        {
            Release(_thrownPower, out Vector3 from, out Vector3 velocity, out Transform alt);
            Vector3 spin = new Vector3(Random.Range(-_s.spin, _s.spin), Random.Range(-_s.spin, _s.spin), Random.Range(-_s.spin, _s.spin));
            Grenade thrown = Instantiate(_s.grenade, from, Quaternion.identity);
            thrown.Throw(transform, alt, Scale, velocity, spin);
            Released?.Invoke(this, from, velocity, spin, Scale);   // online: NetCombat flies a copy on the other PCs (D-0117)
            thrown.WentOff += OnWentOff;
            Last = thrown;
            if (_s.throwClip != null) PaintSplats.InScene().Play(_s.throwClip, from);
            AfterLetGo();
        }

        /// <summary>Where a throw leaves from and how fast, now (the throw and its arc both use it, D-0148): just in front
        /// of the chest, tilted up a little above the look, taking the thrower's own speed with it.
        /// <paramref name="power"/>: the share of Throw Speed it leaves at (D-0184: how long the key was held).</summary>
        private void Release(float power, out Vector3 from, out Vector3 velocity, out Transform alt)
        {
            Vector3 look = _holder != null ? _holder.AimForward : transform.forward;
            Vector3 way = (look + Vector3.up * _s.loft).normalized;
            alt = _holder != null ? _holder.FirstPersonBodyRoot : null;
            FirstPersonBody view = alt != null ? alt.GetComponent<FirstPersonBody>() : null;
            Transform feet = alt != null ? alt : transform;   // in first person the body under the eye is where we stand
            Vector3 flat = Vector3.ProjectOnPlane(look, Vector3.up);
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : transform.forward;
            Vector3 left = Vector3.Cross(flat, Vector3.up);
            // The grenade learns how big its thrower is as it leaves, as a shot does: a kid's leaves from the kid's
            // chest and is as many times faster as the kid is bigger.
            float size = _s.grenade.SizeFor(Scale);
            from = feet.position + (left * _s.releasePoint.x + Vector3.up * _s.releasePoint.y + flat * _s.releasePoint.z) * size;
            velocity = way * (_s.throwSpeed * power * size);
            // It takes the thrower's own speed with it. A body standing on the floor is held to it by a small
            // downward speed of its own: that is not the thrower moving, and it is left out.
            Vector3 own = Vector3.zero; bool grounded = false;
            if (view != null) { own = view.Velocity; grounded = view.Grounded; }
            else if (_motor != null && _motor.isActiveAndEnabled) { own = _motor.Velocity; grounded = _motor.Grounded; }
            if (grounded && own.y < 0f) own.y = 0f;
            velocity += own;
        }

        private void AfterLetGo() => GameEvents.RaiseGrenadeThrown(this);

        private void OnWentOff(int hits, bool finished) { if (this != null) GameEvents.RaiseGrenadeWentOff(this, hits, finished); }   // gone with its scene: nothing to tell
    }
}
