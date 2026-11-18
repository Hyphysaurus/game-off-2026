using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Feeds the body's Animator what the motor does not: which way the body moves against its facing (so it steps
    /// sideways and backwards while aiming), whether it is crouched, and how fast the legs play so the feet keep up with
    /// the ground as far as the cap in PlayerBodySettings allows. It also plays the one-shot clips the moves ask for,
    /// goes back to walking afterwards, and plays the look-around after standing still a while. In first person the
    /// motor is parked and the first-person body moves instead: the legs follow that body, so they walk and crouch
    /// under the eye when the body is in view. The controller it talks
    /// to is made by Shift Jam / Player / Build Player Port; the names below are that controller's.
    /// </summary>
    [DefaultExecutionOrder(10)]   // after the motor has moved this frame
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerAnimation : MonoBehaviour
    {
        public static readonly int Locomotion = Animator.StringToHash("Locomotion");
        public static readonly int Roll = Animator.StringToHash("Roll");
        public static readonly int Punch = Animator.StringToHash("Punch");
        public static readonly int Kick = Animator.StringToHash("Kick");
        public static readonly int Block = Animator.StringToHash("Block");
        public static readonly int HitLight = Animator.StringToHash("HitLight");
        public static readonly int HitHeavy = Animator.StringToHash("HitHeavy");
        public static readonly int Death = Animator.StringToHash("Death");
        public static readonly int Fidget = Animator.StringToHash("Fidget");
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveZ = Animator.StringToHash("MoveZ");
        private static readonly int Pace = Animator.StringToHash("Pace");
        private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
        private static readonly int CrouchedParam = Animator.StringToHash("Crouched");
        private const float Damp = 0.08f;        // seconds the move direction takes to settle, as the motor's Speed does
        private const float Still = 0.05f;       // own units a second: slower than this is standing still

        [SerializeField, Tooltip("Resources/PlayerMoves is loaded if empty.")] private PlayerMoveSettings settings;

        private PlayerMotor _motor;
        private PlayerStance _stance;
        private PlayerBody _body;
        private Animator _animator;
        private bool _ours;          // the Animator runs the port's controller (it has our parameters)
        private bool _playing;       // a one-shot clip is on; Resume ends it
        private float _idle;
        private bool _fidgeting;

        /// <summary>True while a one-shot clip asked for by Play is on.</summary>
        public bool Playing => _playing;
        /// <summary>The legs' playback this frame (1 = the clip's own speed).</summary>
        public float LegPlayback { get; private set; } = 1f;
        public Animator Animator => _animator;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _stance = GetComponent<PlayerStance>();
            _body = GetComponent<PlayerBody>();
            if (settings == null) settings = PlayerMoveSettings.Load();
        }

        private void Start()
        {
            _animator = _body != null && _body.Animator != null ? _body.Animator : GetComponentInChildren<Animator>();
            if (_animator == null) return;
            foreach (AnimatorControllerParameter p in _animator.parameters) if (p.nameHash == Pace) _ours = true;
        }

        /// <summary>Plays a one-shot state (Roll, Punch...) from its start at <paramref name="playback"/> times its own speed.</summary>
        public void Play(int state, float playback)
        {
            if (!_ours) return;
            _animator.SetFloat(MoveSpeed, Mathf.Max(0.01f, playback));
            _animator.CrossFadeInFixedTime(state, settings != null ? settings.blend : 0.1f, 0, 0f);
            _playing = true;
            _fidgeting = false;
            _idle = 0f;
        }

        /// <summary>Back to walking (or crouching: the controller follows the crouch by itself).</summary>
        public void Resume()
        {
            if (!_ours || !_playing) return;
            _animator.CrossFadeInFixedTime(Locomotion, settings != null ? settings.blend : 0.1f, 0);
            _playing = false;
            _fidgeting = false;
            _idle = 0f;
        }

        private void Update()
        {
            // First person: the body that moves is the first-person one.
            FirstPersonMode mode = FirstPersonMode.Active;
            FirstPersonBody stand = !_motor.enabled && mode != null && mode.gameObject == gameObject ? mode.Body : null;
            if (!_ours || (!_motor.enabled && stand == null)) { _idle = 0f; return; }   // parked (driving, waking up)
            float dt = Time.deltaTime;
            float scale = Mathf.Max(1e-4f, stand != null ? stand.Scale : _motor.Scale);
            Vector3 velocity = stand != null ? Vector3.ProjectOnPlane(stand.Velocity, Vector3.up) : _motor.PlanarVelocity;
            Vector3 local = transform.InverseTransformDirection(velocity) / scale;
            float speed = new Vector2(local.x, local.z).magnitude;
            bool crouched = stand != null ? stand.Crouched : _stance != null && _stance.Crouched;
            _animator.SetFloat(MoveX, local.x, Damp, dt);
            _animator.SetFloat(MoveZ, local.z, Damp, dt);
            _animator.SetBool(CrouchedParam, crouched);

            var profile = _body != null ? _body.Profile : null;
            if (profile != null && _body.Settings != null)
            {
                LegPlayback = crouched
                    ? _body.Settings.Playback(speed, profile.crouch, settings != null ? settings.crouchSpeed : speed)
                    : _body.Settings.Playback(speed, profile.run, _motor.SprintSpeed);
            }
            _animator.SetFloat(Pace, LegPlayback);

            if (stand != null) Busy();   // no look-around under the eye
            else Idle(speed, crouched, profile, dt);
        }

        /// <summary>The look-around: after standing still, upright and free for a while it plays once, then the wait starts again.</summary>
        private void Idle(float speed, bool crouched, PlayerBodySettings.Body profile, float dt)
        {
            if (settings == null || profile == null || settings.fidgetDelay <= 0f) return;
            bool resting = speed < Still && _motor.Grounded && !crouched && (!_playing || _fidgeting);
            if (!resting)
            {
                if (_fidgeting) Resume();
                _idle = 0f;
                return;
            }
            _idle += dt;
            if (!_fidgeting && _idle >= settings.fidgetDelay)
            {
                Play(Fidget, 1f);
                _fidgeting = true;
                _idle = settings.fidgetDelay;
            }
            else if (_fidgeting && _idle >= settings.fidgetDelay + profile.fidget) Resume();
        }

        /// <summary>Something else took the body (a move, the hands, a weapon): the wait for the look-around starts again.</summary>
        public void Busy()
        {
            if (_fidgeting) Resume();
            _idle = 0f;
        }
    }
}
