using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's moves from the test room, one at a time: dodge roll (Left Ctrl), punch and kick with empty hands
    /// (left and right mouse), block (Q, held), and the hit reactions and death that PlayerHurt asks for. It decides when
    /// a move may start (on foot, on the ground, in normal control, nothing carried), times it, and tells the others
    /// what to do meanwhile: the motor (no steering, the roll's slide, no jump), the weapons (no firing), the Animator
    /// (which clip). With a weapon drawn and aim held the body faces the aim and cannot sprint. It does not move the
    /// body or size the capsule itself: PlayerMotor and PlayerStance do.
    /// </summary>
    [DefaultExecutionOrder(-10)]   // after the stance, before the motor moves this frame
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerInput))]
    public sealed class PlayerMoves : MonoBehaviour
    {
        public enum Move { Free, Roll, Punch, Kick, Block, HitLight, HitHeavy, Dead }

        [SerializeField, Tooltip("Resources/PlayerMoves is loaded if empty.")] private PlayerMoveSettings settings;

        private PlayerMotor _motor;
        private PlayerInput _input;
        private PlayerStance _stance;
        private PlayerAnimation _animation;
        private PlayerBody _body;
        private WeaponHolder _holder;
        private Carrier _carrier;
        private ThirdPersonGrab _grab;
        private CharacterController _cc;
        private float _time, _length;        // seconds into the move, and how long it lasts
        private bool _landed;                // this strike has landed
        private Vector3 _rollDirection; private float _rollSpeed;
        private float _rollReadyAt;

        /// <summary>The move under way; Free in normal control.</summary>
        public Move Current { get; private set; }
        public bool Free => Current == Move.Free;
        public bool Rolling => Current == Move.Roll;
        /// <summary>How far through the move we are, 0 to 1 (0 when free or blocking).</summary>
        public float Share => Free || _length <= 0f ? 0f : Mathf.Clamp01(_time / _length);
        public PlayerMoveSettings Settings => settings;
        /// <summary>Raised when a punch or kick lands on something, with how many things it hit.</summary>
        public event Action<Move, int> StrikeLanded;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInput>();
            _stance = GetComponent<PlayerStance>();
            _animation = GetComponent<PlayerAnimation>();
            _body = GetComponent<PlayerBody>();
            _holder = GetComponent<WeaponHolder>();
            _carrier = GetComponent<Carrier>();
            _cc = GetComponent<CharacterController>();
            if (settings == null) settings = PlayerMoveSettings.Load();
        }

        private void Start() => _grab = GetComponent<ThirdPersonGrab>();   // the Interactor adds it in its Awake

        private void OnEnable()
        {
            _input.Dodge += OnDodge;
            _input.UseHeld += OnAttack;
            _input.AimPressed += OnAim;
        }

        private void OnDisable()
        {
            _input.Dodge -= OnDodge;
            _input.UseHeld -= OnAttack;
            _input.AimPressed -= OnAim;
            End();
            Orders();
        }

        // ---------- when a move may start ----------

        /// <summary>Both hands on something (a prop in the arms, a tool): no roll, punch, kick or block until it is set down.</summary>
        // A left-hand tool (the flashlight) leaves the moves free: roll, block (D-0144).
        public bool HandsFull => (_carrier != null && _carrier.Held != null && !_carrier.InLeftHand) || (_grab != null && _grab.Holding);
        /// <summary>True with no weapon drawn: left and right mouse punch and kick.</summary>
        public bool EmptyHanded => (_holder == null || !_holder.Armed) && (_carrier == null || _carrier.Held == null);   // a click with the light out is the light's

        /// <summary>On foot and in control: the motor runs, in Play, not paused.</summary>
        private bool OnFoot => settings != null && _motor.enabled && _cc != null && _cc.enabled && GameMode.Current == GameMode.Mode.Play && Time.timeScale > 0f;
        private bool MayStart => Free && OnFoot && _motor.Grounded && !HandsFull;
        /// <summary>A click belongs to the game only while the mouse is captured (the click that captures it is not a punch).</summary>
        private static bool Captured => Cursor.lockState == CursorLockMode.Locked;

        private void OnDodge() => TryRoll();
        private void OnAttack() { if (Captured && EmptyHanded && !(_grab != null && _grab.BusyThisFrame)) TryPunch(); }
        private void OnAim() { if (Captured && EmptyHanded) TryKick(); }

        /// <summary>Starts a dodge roll the way the stick points (the way the body faces with the stick at rest). False if it may not start.</summary>
        public bool TryRoll()
        {
            var profile = _body != null ? _body.Profile : null;
            if (!MayStart || profile == null || Time.time < _rollReadyAt) return false;
            Vector3 direction = transform.forward;
            Vector2 stick = _input.Move;
            if (stick.sqrMagnitude > 0.0001f && _motor.View != null)
            {
                Vector3 ahead = Vector3.ProjectOnPlane(_motor.View.forward, Vector3.up).normalized;
                Vector3 wish = ahead * stick.y + Vector3.Cross(Vector3.up, ahead) * stick.x;
                if (wish.sqrMagnitude > 0.0001f) direction = wish.normalized;
            }
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            Begin(Move.Roll, PlayerAnimation.Roll, profile.roll, settings.rollPlayback);
            // The slide starts fast and fades evenly to nothing, so it covers the distance in the clip's time.
            _rollDirection = direction;
            _rollSpeed = 2f * settings.rollDistance * BodyHeight / Mathf.Max(0.01f, _length);
            return true;
        }

        public bool TryPunch()
        {
            var profile = _body != null ? _body.Profile : null;
            if (!MayStart || profile == null) return false;
            Begin(Move.Punch, PlayerAnimation.Punch, profile.punch, settings.punch.playback);
            return true;
        }

        public bool TryKick()
        {
            var profile = _body != null ? _body.Profile : null;
            if (!MayStart || profile == null) return false;
            Begin(Move.Kick, PlayerAnimation.Kick, profile.kick, settings.kick.playback);
            return true;
        }

        /// <summary>A hit reaction: it cuts into anything but death and a held block. PlayerHurt calls it.</summary>
        public void React(bool heavy)
        {
            var profile = _body != null ? _body.Profile : null;
            if (!OnFoot || profile == null || Current == Move.Dead || Current == Move.Block) return;
            if (heavy) Begin(Move.HitHeavy, PlayerAnimation.HitHeavy, profile.hitHeavy, settings.hitHeavyPlayback);
            else Begin(Move.HitLight, PlayerAnimation.HitLight, profile.hitLight, settings.hitLightPlayback);
        }

        /// <summary>Falls and stays down until Revive. False when not on foot (driving, first person): no clip then.</summary>
        public bool Die()
        {
            var profile = _body != null ? _body.Profile : null;
            if (!OnFoot || profile == null) return false;
            Begin(Move.Dead, PlayerAnimation.Death, profile.death, settings.deathPlayback);
            return true;
        }

        /// <summary>Back on the feet after a death (the respawn has put the player back).</summary>
        public void Revive() { if (Current == Move.Dead) End(); }

        // ---------- running a move ----------

        private float BodyHeight => _stance != null ? _stance.BodyHeight : _cc.height * transform.lossyScale.y;

        private void Begin(Move move, int state, float clipSeconds, float playback)
        {
            Current = move;
            _time = 0f;
            _length = clipSeconds / Mathf.Max(0.01f, playback);
            _landed = false;
            if (_animation != null) _animation.Play(state, playback);
        }

        private void End()
        {
            if (Free) return;
            if (Current == Move.Roll) _rollReadyAt = Time.time + (settings != null ? settings.rollCooldown : 0f);
            Current = Move.Free;
            _time = _length = 0f;
            if (_animation != null) _animation.Resume();
        }

        private void Update()
        {
            if (!_motor.enabled || settings == null) { if (!Free && Current != Move.Dead) End(); Orders(); return; }
            float dt = Time.deltaTime;
            switch (Current)
            {
                case Move.Free:
                    if (_input.BlockHeld && MayStart)
                    {
                        Current = Move.Block; _time = _length = 0f;
                        if (_animation != null) _animation.Play(PlayerAnimation.Block, 1f);
                    }
                    break;
                case Move.Block:
                    if (!_input.BlockHeld || HandsFull) End();
                    break;
                case Move.Punch:
                case Move.Kick:
                    _time += dt;
                    Strike();
                    if (_time >= _length) End();
                    break;
                case Move.HitLight:
                case Move.HitHeavy:
                    _time += dt;
                    // Past the locked part any move on the stick ends the reaction; otherwise it plays out.
                    float locked = Current == Move.HitHeavy ? settings.hitHeavyLock : settings.hitLightLock;
                    if (_time >= _length || (_time >= locked && _input.Move.sqrMagnitude > 0.01f)) End();
                    break;
                case Move.Roll:
                    _time += dt;
                    if (_time >= _length) End();
                    break;
                case Move.Dead:
                    _time += dt;
                    break;
            }
            Orders();
        }

        /// <summary>A punch or kick lands once, at the share of its clip where the fist or foot is furthest out.</summary>
        private void Strike()
        {
            if (_landed) return;
            var profile = _body.Profile;
            bool punch = Current == Move.Punch;
            if (Share < (punch ? profile.punchContact : profile.kickContact)) return;
            _landed = true;
            int hits = PlayerStrike.Land(transform, settings, punch ? settings.punch : settings.kick, BodyHeight, _motor.Scale);
            if (hits > 0) StrikeLanded?.Invoke(Current, hits);
        }

        /// <summary>What everyone else must do while this move is on.</summary>
        private void Orders()
        {
            bool free = Free;
            // Aim held with a gun or the flashlight (D-0141): the body faces the aim and steps any way, slower (Alan Wake 2).
            bool aiming = free && _motor.View != null && ((_holder != null && _holder.AimHeld) || (_carrier != null && _carrier.AimHeld));
            _motor.AimShare = aiming ? settings.aimMoveShare : 1f;
            _motor.SteerLocked = !free && Current != Move.Roll;
            _motor.JumpLocked = !free || (_stance != null && _stance.NoHeadroom);
            _motor.SprintLocked = aiming;
            // The body faces the aim while aim is held and while the arm is still up after a shot (the test room's rule).
            bool facing = aiming || (free && _holder != null && _holder.Aiming && _motor.View != null);
            _motor.FaceYaw = facing ? _motor.View.eulerAngles.y : (float?)null;
            _motor.Drive = Current == Move.Roll ? _rollDirection * (_rollSpeed * (1f - Share)) : (Vector3?)null;
            if (_stance != null) _stance.Frozen = !free;   // a move keeps whichever capsule the player has
            if (_holder != null) _holder.Locked = !free;   // weapons only fire in normal control
            if (!free && _animation != null) _animation.Busy();
        }
    }
}
