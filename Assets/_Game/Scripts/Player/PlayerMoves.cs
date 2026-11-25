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
        private Respawn _respawn;            // its fatal landing speed: that landing goes back to the checkpoint, it does not roll
        private const float Never = -10f;
        private float _rollPressedAt = Never;   // when roll was last pressed in the air (D-0171: remembered for the landing)
        private bool _landRoll;              // this roll came out of a landing: it joined the clip part-way and keeps some of the landing's speed
        private float _rollEnter;            // the share of the clip that roll joined at
        private float _rollFrom, _rollTo;    // its slide, world units a second: from the landing's speed down to the share kept
        private float _groundedAt;           // when the feet were last on the ground (a press on a one-frame ground blink is not a jump)
        private const float BlinkSeconds = 0.1f;   // off the ground for less than this is a blink on a step or slope, not a fall (the motor's own grace for the Animator)
        private const float LeastRollLeft = 0.2f;  // a landing roll runs at least this share of the clip before it may hand back to the run

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
            _respawn = GetComponent<Respawn>();
            if (settings == null) settings = PlayerMoveSettings.Load();
        }

        private void Start() => _grab = GetComponent<ThirdPersonGrab>();   // the Interactor adds it in its Awake

        private void OnEnable()
        {
            _input.Dodge += OnDodge;
            _input.UseHeld += OnAttack;
            _input.AimPressed += OnAim;
            _motor.Landed += OnLanded;
            _motor.Warped += OnWarped;
        }

        private void OnDisable()
        {
            _input.Dodge -= OnDodge;
            _input.UseHeld -= OnAttack;
            _input.AimPressed -= OnAim;
            _motor.Landed -= OnLanded;
            _motor.Warped -= OnWarped;
            _rollPressedAt = Never;
            End();
            Orders();
        }

        // ---------- when a move may start ----------

        /// <summary>Both hands on something (a prop in the arms, a tool): no roll, punch, kick or block until it is set down.</summary>
        // A left-hand tool (the flashlight) leaves the moves free: roll, block (D-0144).
        // First person's own hands with a prop in them count as the third-person grab does (D-0195).
        public bool HandsFull => (_carrier != null && _carrier.Held != null && !_carrier.InLeftHand) || (_grab != null && _grab.Holding) || (_holder != null && _holder.HandsBusyInView);
        /// <summary>True with no weapon drawn: left and right mouse punch and kick.</summary>
        public bool EmptyHanded => (_holder == null || !_holder.Armed) && (_carrier == null || _carrier.Held == null);   // a click with the light out is the light's

        /// <summary>On foot and in control: the motor runs, in Play or in first person on the player itself (D-0195; the
        /// old first-person body parks the motor, so nothing starts there), not paused.</summary>
        private bool OnFoot => settings != null && _motor.enabled && _cc != null && _cc.enabled && GameMode.OnFoot && Time.timeScale > 0f;
        private bool MayStart => Free && OnFoot && _motor.Grounded && !HandsFull;
        /// <summary>A click belongs to the game only while the mouse is captured (the click that captures it is not a punch).</summary>
        private static bool Captured => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>Roll now; in the air the press is remembered for the landing (D-0171), so a jump runs straight into a
        /// roll. Only a press that failed for being in the air is kept: full hands or a move under way still drop it.</summary>
        private void OnDodge()
        {
            if (TryRoll()) return;
            if (Free && OnFoot && !_motor.Grounded && !HandsFull) _rollPressedAt = Time.time;
        }

        /// <summary>The motor says the feet are down, with the speed they came down at (own units a second). A real drop
        /// with the stick pushed rolls out of the landing by itself, as a roll press remembered from the air does from
        /// any height (D-0171, his "a seamless jump from the top of the booksteps into a roll"). WHY not every landing: a
        /// jump on the flat must stay a jump, and a landing with the stick at rest keeps its crouch. A landing fast enough
        /// to send the toy back to the checkpoint never rolls: Respawn has that one.</summary>
        private void OnLanded(float speed)
        {
            if (settings == null) return;
            bool pressed = settings.rollBuffer > 0f && Time.time - _rollPressedAt <= settings.rollBuffer;
            _rollPressedAt = Never;
            // The roll that starts by itself gives way to what the player is doing: a Jump pressed just before touching down
            // hops as it always did, and an aim or a reload under way is not dropped by a roll nobody pressed.
            bool drop = settings.landRollFrom > 0f && speed >= settings.landRollFrom && _input.Move.magnitude >= settings.landRollStick
                && !_motor.JumpWaiting
                && !(_holder != null && (_holder.Aiming || _holder.Reloading));
            if (!pressed && !drop) return;
            if (_respawn != null && _respawn.FatalLandingSpeed > 0f && speed >= _respawn.FatalLandingSpeed) return;
            if (_motor.Glide != null) return;   // set down on the plane: the glider's own landing (PlayerMoves hears Landed before the Glider folds)
            // A press on a one-frame ground blink (steps, slopes) was never in the air: it plays as the plain roll it was meant as.
            bool fromAir = Time.time - _groundedAt > BlinkSeconds;
            TryRoll(drop || fromAir);
        }

        /// <summary>A respawn or a portal moved the body: a roll under way would slide on from the new spot with no steering,
        /// and a press remembered from before would roll there. Both end here (PlayerHurt ends a death the same way).</summary>
        private void OnWarped(Vector3 position, Quaternion rotation)
        {
            _rollPressedAt = Never;
            if (Rolling) { End(); Orders(); }
        }
        // A click that belongs to the hands is no strike: the third-person grab's, or first person's own hands with a
        // prop in them or having just thrown one (D-0195: the left mouse throws it, the right turns it).
        private bool HandsTookTheClick => (_grab != null && _grab.BusyThisFrame) || (_holder != null && _holder.ViewHandsBusy);
        private void OnAttack() { if (Captured && EmptyHanded && !HandsTookTheClick) TryPunch(); }
        private void OnAim() { if (Captured && EmptyHanded && !HandsTookTheClick) TryKick(); }

        /// <summary>Starts a dodge roll the way the stick points (the way the body faces with the stick at rest). False if it may not start.</summary>
        public bool TryRoll() => TryRoll(false);

        /// <summary>The roll itself. <paramref name="landing"/>: it comes out of a landing (D-0171): it joins the clip at
        /// the dive instead of the standing start, and its slide starts at the speed the body landed with and eases down
        /// to a share of it, so the toy comes up running instead of stopping dead.</summary>
        private bool TryRoll(bool landing)
        {
            var profile = _body != null ? _body.Profile : null;
            if (!MayStart || profile == null || Time.time < _rollReadyAt) return false;
            Vector3 travel = Vector3.ProjectOnPlane(_motor.PlanarVelocity, Vector3.up);
            // With the stick at rest a landing rolls the way it was travelling; a roll from standing the way the body faces.
            Vector3 direction = landing && travel.sqrMagnitude > 0.0001f ? travel.normalized : transform.forward;
            Vector2 stick = _input.Move;
            if (stick.sqrMagnitude > 0.0001f && _motor.View != null)
            {
                Vector3 ahead = Vector3.ProjectOnPlane(_motor.View.forward, Vector3.up).normalized;
                Vector3 wish = ahead * stick.y + Vector3.Cross(Vector3.up, ahead) * stick.x;
                if (wish.sqrMagnitude > 0.0001f) direction = wish.normalized;
            }
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            _rollEnter = landing ? Mathf.Clamp(settings.landRollEnter, 0f, 0.6f) : 0f;
            Begin(Move.Roll, PlayerAnimation.Roll, profile.roll, settings.rollPlayback, _rollEnter);
            // The slide starts fast and fades evenly to nothing, so it covers the distance in the clip's time.
            _rollDirection = direction;
            _rollSpeed = 2f * settings.rollDistance * BodyHeight / Mathf.Max(0.01f, _length);
            _landRoll = landing;
            if (landing)
            {
                // Never slower than a plain roll's average (a remembered press from a slow hop still rolls somewhere) and never
                // faster than the settings allow (past that the body skates over the clip).
                // Only the speed that already goes the roll's way: the stick picks the direction, and a stick pulled back must
                // not turn a fast fall forward into a fast roll backwards.
                float carried = Mathf.Max(0f, Vector3.Dot(travel, direction));
                _rollFrom = Mathf.Clamp(carried, 0.5f * _rollSpeed, Mathf.Max(0.5f, settings.landRollMost) * _rollSpeed);
                _rollTo = _rollFrom * Mathf.Clamp01(settings.landRollKeep);
            }
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

        /// <summary>Falls and stays down until Revive. False when not on foot (driving, the old first-person body): no clip then.</summary>
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

        /// <summary><paramref name="from"/>: the share of the clip the move joins at (0 = its start).</summary>
        private void Begin(Move move, int state, float clipSeconds, float playback, float from = 0f)
        {
            Current = move;
            _length = clipSeconds / Mathf.Max(0.01f, playback);
            _time = Mathf.Clamp01(from) * _length;
            _landed = false;
            _landRoll = false;
            if (_animation != null) _animation.Play(state, playback, _time);
        }

        private void End()
        {
            if (Free) return;
            if (Current == Move.Roll) _rollReadyAt = Time.time + (settings != null ? settings.rollCooldown : 0f);
            Current = Move.Free;
            _time = _length = 0f;
            _landRoll = false;
            if (_animation != null) _animation.Resume();
        }

        private void Update()
        {
            if (!_motor.enabled || settings == null) { if (!Free && Current != Move.Dead) End(); Orders(); return; }
            float dt = Time.deltaTime;
            if (_motor.Grounded) _groundedAt = Time.time;
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
                    // A landing roll with the stick still pushed hands back to the run before the clip has stood all the way up.
                    // A pad or a knock that throws a landing roll into the air ends it: its slide ignores the stick and locks the jump.
                    if (_time >= _length
                        || (_landRoll && Share >= Mathf.Max(settings.landRollExit, _rollEnter + LeastRollLeft) && _input.Move.sqrMagnitude > 0.01f)
                        || (_landRoll && !_motor.Grounded && _motor.Velocity.y > 0f)) End();
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

        /// <summary>The roll's slide right now, world units a second. A plain roll starts fast and fades evenly to nothing; a
        /// roll out of a landing eases from the landing's speed down to the share it keeps.</summary>
        private float RollSpeed => _landRoll
            ? Mathf.Lerp(_rollFrom, _rollTo, Mathf.InverseLerp(_rollEnter, 1f, Share))
            : _rollSpeed * (1f - Share);

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
            _motor.Drive = Current == Move.Roll ? _rollDirection * RollSpeed : (Vector3?)null;
            if (_stance != null) _stance.Frozen = !free;   // a move keeps whichever capsule the player has
            if (_holder != null) _holder.Locked = !free;   // weapons only fire in normal control
            if (!free && _animation != null) _animation.Busy();
        }
    }
}
