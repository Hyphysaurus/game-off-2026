using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using Playtest;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace ShiftJam
{
    /// <summary>
    /// The claw machine you can play (D-0229, his "claw machine wasnt interactable"), on Room/Bedroom/ClawMachine.
    /// THE ROUND: the KID presses E at the machine's front (ClawGamePlaySpot; the toy gets no prompt). The view swings to a
    /// framed shot of the glass (ClawGameView; never inside a wall), the player holds still (the player's own keys are
    /// switched off, the one switch the pause menu and the armory bench use) and the claw takes the keys from its own copy
    /// of the input actions: Move steers the claw over the case (inside the glass), Interact or Jump drops it, Esc or the
    /// pad's B gives up. After the settings' Time Limit it drops by itself. In first person the view is the same shot. The
    /// kid, and whatever is in its hands, is hidden while the shot is up (it would stand between the camera and the glass;
    /// in first person the gun and the arms would float in the shot). Everything taken is given back exactly as it was:
    /// the keys, the Interactor, the camera's menu hold, the body fade (after the body is shown, D-0234), first person's
    /// hands. Esc and the pad's B are the claw's for the whole round: the pause menu stands aside (the rig is held as by
    /// a menu) and first person does not leave on it (<see cref="TookEsc"/>). The pause menu cannot open meanwhile.
    /// THE CLAW (ClawGameClaw, driven by DOTween tweens on the pack's own parts, eased, with a little swing): it comes down
    /// onto whatever is under it (found at run time: the highest prize under the open fingers, else the floor), the three
    /// fingers close (on a prize they stop at its sides), it rises. A prize under it with enough overlap (its middle near
    /// the claw's) is lifted at the settings' Grab Chance; held with less than Firm Overlap it may slip on the way up (a
    /// near miss). A held prize is carried to the chute and let go; the claw goes home.
    /// THE PRIZE (ClawGamePrize): it falls into the chute's hole, the flap swings open, and it comes out of the machine's
    /// front onto the floor as a plain toy you can carry (a Carryable with a collider and a Rigidbody; its DarkMonster off,
    /// held in its rest pose). A toast names it. Saved per slot (SaveGame's seen list, Settings > Save Key + its name): a
    /// reload puts it out front, never back in the case; New Game puts it back in the case.
    /// THE STAR: each win raises On Won; the maker wires the gold star's ObjectiveHook to it (the first win earns it and,
    /// like every star, refills the shift charge).
    /// Numbers, sounds and words: Resources/ClawGameSettings. The maker (Shift Jam > House > Land Claw Game (D-0229)) finds
    /// the parts, measures the case and places the play spot, the views and the exit. OFF SWITCH: ClawGameSettings > On
    /// (or untick this). Online: Settings > Online (off: no prompt in a party; nothing is networked either way).
    /// D-0241: Held Shut By (the bedroom gate, set by Land Bedroom Gate) may hold the machine shut for now: its words beside
    /// E, and E is refused with its toast and the refusal sound (as an empty machine refuses). Empty: never held.
    /// Runs at order -60, as the armory bench does: after the pause menu has looked at this frame's Esc, before first person.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ClawGameView))]
    public sealed class ClawGame : MonoBehaviour
    {
        [Header("The machine's parts (the maker finds them by name)")]
        [SerializeField, Tooltip("Toy_Claw_01, the machine's body. Its own space is the machine's (the pack's metres: 1 unit = its scale in the room); its front is its +z.")]
        private Transform machine;
        [SerializeField, Tooltip("The body's collider: the floor under the claw is found on it at run time.")] private Collider body;
        [SerializeField, Tooltip("Claw_Mechanism_01, the gantry: it slides front to back (its own z).")] private Transform rail;
        [SerializeField, Tooltip("Claw_Mechanism_02, the carriage: it slides side to side along the gantry (its own x).")] private Transform carriage;
        [SerializeField, Tooltip("Claw_Piston_01, the rod under the carriage: the claw swings about its top, and a stretched copy of its mesh (made at run time) is the rod growing as the claw goes down.")]
        private Transform rod;
        [SerializeField, Tooltip("Claw_Piston_02, the claw's head with the fingers: it moves down out of the rod.")] private Transform head;
        [SerializeField, Tooltip("The three Claw_01 fingers under the head: each turns about its own x from where the model has it.")] private Transform[] fingers = new Transform[0];
        [SerializeField, Tooltip("Cap_01, the chute's flap at the front: it swings open about its own x to let a won prize out.")] private Transform hatch;
        [SerializeField, Tooltip("Lever_01: tilts as the claw is steered.")] private Transform lever;
        [SerializeField, Tooltip("Button_01: goes in as the claw starts and drops.")] private Transform button;
        [SerializeField, Tooltip("Room/Bedroom/ClawPrizes: each child with a DarkMonster is a prize.")] private Transform prizes;

        [Header("Places (the maker puts them; move or turn them to re-shoot)")]
        [SerializeField, Tooltip("The framed view of the glass while playing: the camera stands here, turned this way (the maker fits the glass into Settings > View Field Of View at 16:9). At run time it is only pulled in toward the glass if a wall is in the way.")]
        private Transform view;
        [SerializeField, Tooltip("The view of the chute's flap while a won prize comes out.")] private Transform chuteView;
        [SerializeField, Tooltip("Just in front of the chute's flap at the opening's middle: a won prize comes out through the flap to here, then down to the floor.")]
        private Transform exit;
        [SerializeField, Tooltip("The play spot (the front's trigger): the round ends if the player is moved further than Settings > Leave Distance from it.")]
        private Transform playSpot;

        [Header("Measured by the maker (the machine's own units)")]
        [SerializeField, Tooltip("The glass case, in the machine's own space (the body's glass sub-mesh).")] private Bounds caseBox;
        [SerializeField, Tooltip("Where the claw's middle may be steered, the machine's own space: x from (x) to (y), z from (z) to (w). The open fingers, the carriage and the gantry stay off the glass.")]
        private Vector4 steerArea = new Vector4(-0.3f, 0.3f, -0.24f, 0.226f);
        [SerializeField, Tooltip("Over the chute's hole: where a carried prize is let go (the machine's own x, z). The pack's own: 0.2596, 0.2258.")]
        private Vector2 chuteAt = new Vector2(0.2596f, 0.2258f);
        [SerializeField, Tooltip("The prize floor's height, the machine's own space (used when no floor is found under the claw).")] private float floorY = 1.16f;
        [SerializeField, Tooltip("The chute hole's floor height (lower than the prize floor), the machine's own space.")] private float holeY = 1.06f;
        [SerializeField, Tooltip("A fingertip in a finger's own space: the pack's own measure of its curved finger (ClawFinger's last centreline point).")]
        private Vector3 fingerTip = new Vector3(0f, -0.183f, 0.011f);
        [SerializeField, Tooltip("+1 when a finger closes at positive angles about its own x (as in the pack), -1 the other way. The maker checks it.")]
        private float fingerClose = 1f;

        [Header("Wiring")]
        [SerializeField, Tooltip("The project's input actions (InputSystem_Actions): the claw's own copy reads Player/Move, Player/Interact, Player/Jump and UI/Cancel from it, so a change of keys there changes these too.")]
        private InputActionAsset actions;
        [SerializeField, Tooltip("Raised each time a prize drops into the chute (a win). The maker wires the gold star's ObjectiveHook here: the first win earns the star.")]
        private UnityEvent onWon = new UnityEvent();
        [SerializeField, Tooltip("D-0241: a lock that may hold the machine shut for now (a component that is an IShutLock: the bedroom gate, set by Land Bedroom Gate). While it holds: its words beside E, and E is refused with its toast and the refusal sound. Empty: never held.")]
        private MonoBehaviour heldShutBy;
        private IShutLock _lock;

        private const float MinSeconds = 0.05f;   // no tween is shorter than this (a move of nothing still takes a moment)
        private const float SoundStill = 0.12f;   // the motor's sound stops once nothing has moved for this long (no stutter between moves)

        private enum Round { None, Ready, Steer, Busy }

        private static int _escFrame = -1;

        /// <summary>True for the rest of the frame in which a round of the claw machine took the keyboard's Esc (or the
        /// pad's B). First person (FirstPersonMode, order -50) reads the same key press later in the frame and asks this
        /// first, so Esc at the claw machine never also leaves first person.</summary>
        public static bool TookEsc => _escFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _escFrame = -1; }

        private ClawGameSettings _s;
        private ClawGameClaw _claw;
        private ClawGameView _view;
        private readonly ClawGameHud _hud = new ClawGameHud();
        private readonly List<ClawGamePrize> _prizes = new List<ClawGamePrize>();
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private readonly List<Renderer> _found = new List<Renderer>();
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Collider[] _near = new Collider[24];
        private ArcPreview _aim;                                     // where the claw will come down (D-0238); made the first time
        private readonly List<Vector3> _aimLine = new List<Vector3>(2);
        private bool _ready;
        private string _whyNot = "";
        private Quaternion _hatchRest = Quaternion.identity;
        private AudioSource _shots, _motor;
        private float _still;
        private float _lastDepth, _lastFingers;
        private Vector2 _lastAt;

        // the claw's own keys: a copy of the project's input actions
        private InputActionAsset _keys;
        private InputAction _move, _interact, _jump, _cancel;

        // the round
        private Round _round = Round.None;
        private int _roundId;
        private Interactor _asked, _user;
        private Component _winner;            // who gets the toasts (kept past the round: a prize may come out after it)
        private PlayerInput _input;
        private bool _tookInput;
        private Interactor _parkedInteractor;
        private ShoulderCamera _rig;
        private bool _heldRig;
        private BodyFade _fade;
        private PhysicsGrabber _grabber;
        private Transform _fpRig;
        private GameMode.Mode _mode;
        private Camera _cam;
        private bool _bodyHidden;
        private float _timeLeft;
        private Vector2 _steerVelocity, _leverAim, _leverShown;
        private bool _moveRest;
        private Quaternion _shotTurn = Quaternion.identity;

        // the machine's run (DOTween)
        private Sequence _run, _prizeRun;
        private ClawGamePrize _target;
        private bool _grabbed, _slips;
        private float _buttonIn;
        private DOGetter<float> _getDepth, _getFingers, _getButton;
        private DOSetter<float> _setDepth, _setFingers, _setButton;
        private DOGetter<Vector2> _getAt;
        private DOSetter<Vector2> _setAt;

        // ------------------------------------------------------------------------------------------------ set up

        private void Awake()
        {
            _s = ClawGameSettings.Load();
            _lock = heldShutBy as IShutLock;   // D-0241: anything else in the field is no lock
            _view = GetComponent<ClawGameView>();
            _claw = new ClawGameClaw(machine, rail, carriage, rod, head, fingers, lever, button, fingerTip, fingerClose);
            _getDepth = () => _claw.Depth; _setDepth = v => _claw.Depth = v;
            _getFingers = () => _claw.Fingers; _setFingers = v => _claw.Fingers = v;
            _getAt = () => _claw.At; _setAt = v => _claw.At = v;
            _getButton = () => _buttonIn; _setButton = v => { _buttonIn = v; _claw.Button(v); };

            _whyNot = !_claw.Valid ? "a part of the claw (gantry, carriage, rod, head or fingers) is missing"
                : body == null ? "the body's collider is missing"
                : hatch == null ? "the chute's flap (Cap_01) is missing"
                : view == null || chuteView == null || exit == null ? "a view or the exit is missing"
                : prizes == null ? "the prizes' holder is missing"
                : actions == null ? "the input actions are missing"
                : "";
            if (_whyNot.Length == 0)
            {
                _keys = Instantiate(actions);
                _move = _keys.FindAction("Player/Move");
                _interact = _keys.FindAction("Player/Interact");
                _jump = _keys.FindAction("Player/Jump");
                _cancel = _keys.FindAction("UI/Cancel");
                if (_move == null || _interact == null || _jump == null || _cancel == null) _whyNot = "the input actions have no Player/Move, Player/Interact, Player/Jump or UI/Cancel";
            }
            _ready = _whyNot.Length == 0;
            if (!_ready)
            {
                Debug.LogWarning("CLAW GAME on " + name + " is not playable: " + _whyNot + ". Run Shift Jam > House > Land Claw Game (D-0229).");
                return;
            }

            _hatchRest = hatch.localRotation;
            // The moving parts' own colliders go: a static collider moved every frame is slow, and inside the glass nothing
            // can touch them (they would only get in the way of the claw's own looks at what is under it).
            foreach (Collider c in rail.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            _claw.MakeStretch();
            foreach (DarkMonster m in prizes.GetComponentsInChildren<DarkMonster>(true))
                if (m.transform.parent == prizes) _prizes.Add(new ClawGamePrize(m, _s.saveKey + m.name, _s.NameOf(m.Kind)));
            MakeSounds();
            _lastAt = _claw.At; _lastDepth = _claw.Depth; _lastFingers = _claw.Fingers;
        }

        /// <summary>Two sound sources at the glass's middle: one for the moments, one for the motor's round (the pack's two).</summary>
        private void MakeSounds()
        {
            var go = new GameObject("ClawGameSounds (run time)");
            go.transform.SetParent(transform, false);
            go.transform.position = machine.TransformPoint(caseBox.center);
            float unit = machine.lossyScale.y;
            _shots = go.AddComponent<AudioSource>();
            _motor = go.AddComponent<AudioSource>();
            foreach (AudioSource a in new[] { _shots, _motor })
            {
                a.playOnAwake = false; a.spatialBlend = _s.soundSpace; a.dopplerLevel = 0f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = _s.soundNear * unit; a.maxDistance = Mathf.Max(_s.soundNear + 0.1f, _s.soundFar) * unit;
            }
            _motor.loop = true;
        }

        private void OnEnable() { SaveGame.Changed += Read; }

        private void Start() { Read(); }

        private void OnDisable()
        {
            SaveGame.Changed -= Read;
            _asked = null;
            if (_round != Round.None) EndRound(true);
            if (_bodyHidden || _fade != null) ShowBody();
            StopMachine();
        }

        private void OnDestroy()
        {
            if (_keys != null) Destroy(_keys);
            _hud.Destroy();
        }

        // ------------------------------------------------------------------------------------------------ the front (E)

        private bool Playable => _ready && _s.on && (!NetSession.InParty || _s.online);
        private bool Busy => _round != Round.None || Running(_run) || Running(_prizeRun);
        private static bool Running(Tween t) => t != null && t.IsActive();

        private bool AnyLeft
        {
            get
            {
                for (int i = 0; i < _prizes.Count; i++) if (_prizes[i].Now == ClawGamePrize.State.InCase) return true;
                return false;
            }
        }

        /// <summary>The words beside the E key (ClawGamePlaySpot asks every frame): "Play" for the kid standing in front of
        /// the glass, "Empty" once every prize is won; nothing for the toy, nothing while a round or the claw is busy. No
        /// lookups or new strings here.</summary>
        public string PromptFor(Interactor who)
        {
            if (!Playable || Busy || who == null) return "";
            Shifter size = who.Shifter;
            if (size == null || size.Size != ShiftSize.Kid || size.Shifting) return "";
            if (!InFront(who.transform.position)) return "";
            if (heldShutBy != null && _lock != null && _lock.Holds(this, out string shut) && !string.IsNullOrEmpty(shut)) return shut;   // D-0241
            return AnyLeft ? _s.playPrompt : _s.emptyPrompt;
        }

        /// <summary>Only notes who pressed. WHY: this is called from inside the player's own input callback, and a round
        /// switches that input off: that is done from Update, outside the callback (as the armory bench does).</summary>
        public void Interact(Interactor who)
        {
            if (Playable && !Busy) _asked = who;
        }

        /// <summary>Standing in front of the glass (the machine's +z side), near enough to reach the panel.</summary>
        private bool InFront(Vector3 feet)
        {
            Vector3 p = machine.InverseTransformPoint(feet);
            return p.z > caseBox.max.z && p.z < caseBox.max.z + _s.playReach && Mathf.Abs(p.x - caseBox.center.x) < caseBox.extents.x + 0.5f * _s.playReach;
        }

        // ------------------------------------------------------------------------------------------------ per frame

        private void Update()
        {
            if (_asked != null)
            {
                Interactor who = _asked;
                _asked = null;
                TryStart(who);
            }
            if (_round == Round.None) { BodyFollowsView(); return; }

            // Esc and the pad's B belong to the claw for the whole round: noted first, so first person leaves them alone.
            bool cancel = _cancel.WasPressedThisFrame();
            if (cancel) _escFrame = Time.frameCount;
            if (!StillGood(out string why)) { Abort(why); return; }
            BodyFollowsView();
            bool keysFree = !KeyHelp.IsOpen && !DebugConsole.IsOpen;   // the key sheet and the console own the keys while up
            if (cancel && keysFree && (_round == Round.Ready || _round == Round.Steer)) { GiveUp(); return; }
            if (_round == Round.Steer) Steer(Time.deltaTime, keysFree);
        }

        /// <summary>After the tweens: the swing, the lever easing to the steer, the motor's sound.</summary>
        private void LateUpdate()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            _claw.Swing(dt, _s);
            if (_round != Round.Steer) _leverAim = Vector2.zero;
            if (dt > 0f) _leverShown = Vector2.Lerp(_leverShown, _leverAim, 1f - Mathf.Exp(-dt / _s.leverEase));
            if (_leverShown.sqrMagnitude > 1e-6f || _leverAim.sqrMagnitude > 0f) _claw.Lever(_leverShown, _s.leverTilt);
            else if (_leverShown != Vector2.zero) { _leverShown = Vector2.zero; _claw.Lever(Vector2.zero, _s.leverTilt); }
            Motor(dt);
        }

        /// <summary>The kid is hidden once the swing to the glass is far enough along, and shown again (then its body fade
        /// back on, D-0234) when the swing back passes the same point.</summary>
        private void BodyFollowsView()
        {
            float w = _view != null && _view.Busy ? _view.Weight : 0f;
            if (_round != Round.None) { if (!_bodyHidden && w >= _s.hideBodyAt) HideBody(); }
            else if ((_bodyHidden || _fade != null) && w < _s.hideBodyAt) ShowBody();
        }

        /// <summary>False once the player can no longer be playing: the reason, for the play log.</summary>
        private bool StillGood(out string why)
        {
            why = null;
            if (_user == null || _input == null || _cam == null) { why = "the player is gone"; return false; }
            if (GameMode.Current != _mode) { why = "the game mode changed"; return false; }
            if (DebugConsole.IsOpen) { why = "the console opened"; return false; }
            if (_tookInput && _input.enabled) { why = "something else gave the player's keys back"; return false; }
            Shifter size = _user.Shifter;
            if (size != null && (size.Size != ShiftSize.Kid || size.Shifting)) { why = "a shift"; return false; }
            Vector3 gap = _user.transform.position - (playSpot != null ? playSpot.position : transform.position);
            gap.y = 0f;
            if (gap.magnitude > _s.leaveDistance * machine.lossyScale.y) { why = "the player was moved away"; return false; }
            return true;
        }

        // ------------------------------------------------------------------------------------------------ starting a round

        private void TryStart(Interactor who)
        {
            if (!Playable || Busy || who == null || !InFront(who.transform.position)) return;
            Shifter size = who.Shifter;
            if (size == null || size.Size != ShiftSize.Kid || size.Shifting) return;
            if (GameMode.Current != GameMode.Mode.Play && GameMode.Current != GameMode.Mode.FirstPerson) return;
            if (Time.timeScale <= 0f || PauseMenu.IsOpen || DebugConsole.IsOpen || TitleMenu.IsOpen || Opening.Playing) return;
            PlayerInput input = who.GetComponent<PlayerInput>();
            if (input == null || !input.enabled) return;
            PlayerMoves moves = who.GetComponent<PlayerMoves>();   // plain form: Fast Script Reload breaks `TryGetComponent(out ...) &&`
            if (moves != null && !moves.Free) return;
            if (heldShutBy != null && _lock != null && _lock.Holds(this, out _)) { _lock.Refuse(this, who); return; }   // D-0241: the lock's toast and the refusal sound
            if (!AnyLeft)
            {
                Toasts.ShowFor(who, _s.emptyToast, ToastKind.Refused);
                UiAudio.Play(UiAudio.Kind.Denied);
                return;
            }
            Camera cam = PlayerSlot.ViewFor(who);
            if (cam == null) return;
            Begin(who, input, cam);
        }

        /// <summary>Takes the player over (keys, prompt, camera hold, body fade, first person's hands), swings the view to
        /// the glass and brings the claw into the case with its fingers opening; then the steering starts.</summary>
        private void Begin(Interactor who, PlayerInput input, Camera cam)
        {
            _roundId++;
            _user = who; _winner = who; _input = input; _cam = cam; _mode = GameMode.Current;

            // Hands off: this one switch stops every key and button of the player (the pause menu's and the bench's).
            _tookInput = input.enabled;
            if (_tookInput) input.enabled = false;
            // Nothing else in reach shows its prompt meanwhile (first person has it parked already).
            if (who.enabled) { who.enabled = false; _parkedInteractor = who; }
            // Third person: the rig is held as by a menu, so the pause menu leaves Esc and Start alone and the mouse turns
            // nothing; the cursor stays hidden (nothing here is clicked).
            _rig = PlayerSlot.RigFor(who);
            _heldRig = false;
            if (_rig != null && _rig.isActiveAndEnabled && !_rig.MenuHeld)
            {
                _rig.SetMenuHold(true);
                _heldRig = true;
                if (_rig.OwnsCursor) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            }
            // The body fade goes off first (D-0234): switched off, it shows the body fully, so what is hidden is the real body.
            BodyFade fade = who.GetComponent<BodyFade>();
            if (fade != null && fade.enabled) { fade.enabled = false; _fade = fade; }
            // First person: its hands' dot and hint go while the view is away from the eyes.
            _fpRig = null;
            if (_mode == GameMode.Mode.FirstPerson && FirstPersonMode.Active != null && FirstPersonMode.Active.Body != null)
            {
                _fpRig = FirstPersonMode.Active.Body.transform;
                PhysicsGrabber hands = _fpRig.GetComponent<PhysicsGrabber>();
                if (hands != null && hands.enabled) { hands.enabled = false; _grabber = hands; }
            }
            KeysOn(input);

            Vector3 glass = machine.TransformPoint(caseBox.center);
            _shotTurn = view.rotation;
            _view.Begin(cam, Clear(glass, view.position), _shotTurn, _s.viewFieldOfView, _s.blendIn);

            _round = Round.Ready;
            _moveRest = true;
            _steerVelocity = Vector2.zero;
            _timeLeft = _s.timeLimit;
            PressButton();
            Tell("round started (" + (_mode == GameMode.Mode.FirstPerson ? "first person" : "third person") + ")");

            // Into the case: from its rest corner to the nearest place the open claw fits, the fingers opening on the way.
            Vector2 start = Clamp(_claw.At);
            _run = NewRun();
            _run.Append(AtTo(start, Seconds(Vector2.Distance(_claw.At, start), _s.carrySpeed)));
            _run.Join(FingersTo(_s.fingerOpen, Seconds(Mathf.Abs(_s.fingerOpen - _claw.Fingers), _s.fingerSpeed)));
            _run.AppendCallback(StartSteering);
        }

        private void StartSteering()
        {
            if (_round != Round.Ready) return;
            _round = Round.Steer;
            _hud.Show(_s.hudTitle, _s.hudKeys);
            _hud.Seconds(Mathf.CeilToInt(_timeLeft), _s.hudTimeFormat);
            ShowAim();
        }

        // ------------------------------------------------------------------------------------------------ steering

        /// <summary>Move steers the claw across the case: right and away as the shot sees them, eased, kept in the steer
        /// area. A direction held from before the round (walking up with W down) counts only once let go. The clock runs
        /// down; Interact or Jump drops the claw, and so does the clock reaching 0.</summary>
        private void Steer(float dt, bool keysFree)
        {
            Vector2 stick = keysFree ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            if (stick.magnitude < _s.deadZone) { stick = Vector2.zero; _moveRest = false; }
            else if (_moveRest) stick = Vector2.zero;

            Vector3 right = machine.InverseTransformDirection(_shotTurn * Vector3.right);
            Vector3 away = machine.InverseTransformDirection(Vector3.ProjectOnPlane(_shotTurn * Vector3.forward, machine.up));
            Vector2 r = new Vector2(right.x, right.z), a = new Vector2(away.x, away.z);
            if (r.sqrMagnitude > 1e-8f) r.Normalize();
            if (a.sqrMagnitude > 1e-8f) a.Normalize();
            Vector2 want = (r * stick.x + a * stick.y) * _s.steerSpeed;
            float ease = _s.steerEase > 0f ? 1f - Mathf.Exp(-3f * dt / _s.steerEase) : 1f;
            _steerVelocity = Vector2.Lerp(_steerVelocity, want, ease);
            Vector2 next = _claw.At + _steerVelocity * dt;
            Vector2 kept = Clamp(next);
            if (kept.x != next.x) _steerVelocity.x = 0f;   // against the edge: no speed stored up to push on with
            if (kept.y != next.y) _steerVelocity.y = 0f;
            _claw.At = kept;
            _leverAim = stick;
            ShowAim();

            if (keysFree) _timeLeft -= dt;   // the key sheet or the console holding the keys does not eat the clock
            _hud.Seconds(Mathf.Max(0, Mathf.CeilToInt(_timeLeft)), _s.hudTimeFormat);
            bool drop = keysFree && (_interact.WasPressedThisFrame() || _jump.WasPressedThisFrame());
            if (drop || _timeLeft <= 0f) Drop(!drop);
        }

        private Vector2 Clamp(Vector2 at) => new Vector2(Mathf.Clamp(at.x, steerArea.x, steerArea.y), Mathf.Clamp(at.y, steerArea.z, steerArea.w));

        /// <summary>Where the claw will come down (D-0238, his "add the claw aiming" after a play of 12 drops for 1 grab, most "too far
        /// off its middle": depth is hard to judge from the front). A drop line from the claw and a ring as wide as the grab's reach
        /// on whatever is under it, worked out exactly as the drop works it out (FloorUnderClaw, Under, the overlap), so the ring and
        /// the drop never disagree: the floor's colour over nothing, Near over a prize too far off its middle, Grab when it can be taken.</summary>
        private void ShowAim()
        {
            if (!_s.aimOn) { HideAim(); return; }
            if (_aim == null) _aim = ArcPreview.For(machine);   // the throw arc's drawer (Resources/FX/AimArc): one, kept
            if (_aim == null) return;
            float unit = machine.lossyScale.y;
            Vector3 grip = _claw.GripWorld;
            float open = _s.fingerOpen;
            float floor = FloorUnderClaw(grip, open);
            ClawGamePrize target = Under(grip, _claw.TipRadius(open, true) * unit, out float top, out float offset, out float _);
            float overlap = target != null ? Mathf.Clamp01(1f - offset / Mathf.Max(1e-4f, _s.graspReach * unit)) : 0f;
            float land = target != null ? Mathf.Max(floor, top) : floor;   // the machine's own height
            Vector3 local = machine.InverseTransformPoint(grip);
            Vector3 ringAt = machine.TransformPoint(new Vector3(local.x, land + _s.aimLift, local.z));
            Color c = target == null ? _s.aimNothing : overlap >= _s.overlapNeeded ? _s.aimGrab : _s.aimNear;
            _aimLine.Clear(); _aimLine.Add(grip); _aimLine.Add(ringAt);
            _aim.Show(_aimLine, _s.aimLineWidth * unit, c, _s.aimDashes, ringAt, machine.up, _s.graspReach * unit, c);
        }

        private void HideAim() { if (_aim != null) _aim.Hide(); }

        // ------------------------------------------------------------------------------------------------ the drop

        /// <summary>The claw comes down onto what is under it, closes, rises, and (if it holds a prize) carries it to the
        /// chute; then it goes home. Everything is decided here, then played as one sequence of tweens.</summary>
        private void Drop(bool timedOut)
        {
            if (_round != Round.Steer) return;
            _round = Round.Busy;
            _hud.Hide();
            HideAim();
            _steerVelocity = Vector2.zero;
            PressButton();

            float unit = machine.lossyScale.y;
            Vector3 grip = _claw.GripWorld;
            float open = _s.fingerOpen;
            float floor = FloorUnderClaw(grip, open);
            ClawGamePrize target = Under(grip, _claw.TipRadius(open, true) * unit, out float top, out float offset, out float radius);
            float overlap = target != null ? Mathf.Clamp01(1f - offset / Mathf.Max(1e-4f, _s.graspReach * unit)) : 0f;
            float stop = target != null ? _claw.StopAngle(open, _s.fingerClosed, radius / unit + _s.fingerGap) : _s.fingerClosed;
            float lowest = _claw.LowestBetween(open, stop);
            float grip0 = floor + lowest + _s.floorGap;                        // the lowest the claw's middle may go here
            float gripTo = target != null ? Mathf.Max(grip0, top + _s.topGap) : grip0;
            float depth = Mathf.Max(0f, _claw.GripRestY - gripTo);

            _target = target;
            _grabbed = target != null && overlap >= _s.overlapNeeded && Random.value < _s.grabChance;
            _slips = _grabbed && overlap < _s.firmOverlap && Random.value < _s.slipChance;
            float slipAt = Mathf.Clamp01(Random.Range(Mathf.Min(_s.slipBetween.x, _s.slipBetween.y), Mathf.Max(_s.slipBetween.x, _s.slipBetween.y)));
            if (PlayLog.On)
                Tell((timedOut ? "time ran out, dropped" : "dropped") + " at (" + _claw.At.x.ToString("F3") + ", " + _claw.At.y.ToString("F3") + "): "
                    + (target != null ? "over " + target.Root.name + ", overlap " + overlap.ToString("F2") + (_grabbed ? (_slips ? ", taken, will SLIP" : ", TAKEN") : overlap < _s.overlapNeeded ? ", too far off its middle" : ", the grab chance said no") : "nothing under it (the floor)")
                    + ", down " + depth.ToString("F3"));

            Vector2 from = _claw.At;
            float settle = _s.settleSeconds;
            _run = NewRun();
            float t = 0f, step;
            step = Seconds(depth, _s.dropSpeed);
            _run.Append(DepthTo(depth, step)); t += step;
            _run.AppendInterval(settle); t += settle;
            step = Seconds(Mathf.Abs(stop - open), _s.fingerSpeed);
            _run.Append(FingersTo(stop, step)); t += step;
            _run.AppendCallback(Closed);
            _run.AppendInterval(settle); t += settle;
            step = Seconds(depth, _s.riseSpeed);
            if (_slips) _run.InsertCallback(t + step * slipAt, Slip);
            _run.Append(DepthTo(0f, step)); t += step;

            Vector2 homeFrom = from;
            float fingersFrom = stop;
            if (_grabbed && !_slips)
            {
                _run.AppendInterval(settle * 0.5f);
                _run.Append(AtTo(chuteAt, Seconds(Vector2.Distance(from, chuteAt), _s.carrySpeed)));
                _run.AppendInterval(settle);
                _run.AppendCallback(Release);
                _run.Append(FingersTo(open, Seconds(Mathf.Abs(open - stop), _s.fingerSpeed)));
                homeFrom = chuteAt;
                fingersFrom = open;
            }
            else
            {
                if (!_grabbed) _run.AppendCallback(Missed);
                _run.AppendInterval(_s.missHold);
                _run.AppendCallback(HandBack);
            }
            // Home: across to its rest corner, the fingers back as the model has them.
            _run.Append(AtTo(_claw.Home, Seconds(Vector2.Distance(homeFrom, _claw.Home), _s.carrySpeed)));
            _run.Join(FingersTo(0f, Seconds(Mathf.Abs(fingersFrom), _s.fingerSpeed)));
        }

        /// <summary>The fingers have closed: on a grab the prize now hangs from the claw (a small jolt).</summary>
        private void Closed()
        {
            if (!_grabbed || _target == null || _target.Now != ClawGamePrize.State.InCase) return;
            _target.Hold(_claw.Head);
            float a = Random.value * 2f * Mathf.PI;
            _claw.Kick(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _s.grabJolt);
        }

        /// <summary>A near miss: on the way up the prize slips out and drops back where it was taken from.</summary>
        private void Slip()
        {
            ClawGamePrize p = _target;
            _target = null;
            if (p == null || p.Now != ClawGamePrize.State.Held) return;
            p.LetGo(false);
            p.Root.DOMove(p.HomePosition, _s.slipFallSeconds).SetEase(Ease.InQuad).SetLink(p.Root.gameObject);
            p.Root.DORotateQuaternion(p.HomeRotation, _s.slipFallSeconds).SetEase(Ease.OutQuad).SetLink(p.Root.gameObject);
            Play(_s.slip);
            Toast(_s.slipToast, ToastKind.Refused);
            Tell("the " + p.Name + " slipped out on the way up");
        }

        /// <summary>The claw came up empty.</summary>
        private void Missed()
        {
            Play(_s.miss);
            Toast(_s.missToast, ToastKind.Tip);
            Tell("missed");
        }

        /// <summary>After a miss or a slip has been seen: the view swings back while the claw goes home.</summary>
        private void HandBack()
        {
            if (_round == Round.Busy) EndRound(false);
        }

        /// <summary>Over the chute: the claw lets go; the prize falls into the chute and comes out of the front.</summary>
        private void Release()
        {
            ClawGamePrize p = _target;
            _target = null;
            if (p == null || p.Now != ClawGamePrize.State.Held) return;
            p.LetGo(true);
            Play(_s.toyDrop);
            PrizeOut(p);
        }

        // ------------------------------------------------------------------------------------------------ the prize coming out

        /// <summary>The won prize's way out: down into the chute's hole (the hole's floor hides it as it sinks), the flap
        /// swings open, the prize (a plain toy now) comes out through it, drops to the floor and rolls on; the flap shuts.
        /// The view moves to the flap meanwhile, and swings back to the player once the prize is on the floor.</summary>
        private void PrizeOut(ClawGamePrize p)
        {
            int round = _roundId;
            float unit = machine.lossyScale.y;
            p.Size(out float tall, out float radius);
            Vector3 at = p.Root.position;
            float holeWorld = machine.TransformPoint(new Vector3(chuteAt.x, holeY, chuteAt.y)).y;
            Vector3 sunk = new Vector3(at.x, holeWorld - tall * 1.15f, at.z);
            Vector3 front = Front();
            Vector3 middle = Vector3.up * (tall * 0.5f);
            Vector3 outside = exit.position - middle;
            Vector3 inside = outside - front * (_s.insideDepth * unit);
            Vector3 land = LandingSpot(exit.position, radius, tall, p.Root);
            Quaternion facing = Quaternion.LookRotation(front, Vector3.up);

            _prizeRun = DOTween.Sequence().SetLink(gameObject).OnComplete(PrizeRunDone);
            _prizeRun.Append(p.Root.DOMove(sunk, _s.fallSeconds).SetEase(Ease.InQuad));
            float tumble = _s.fallTumble;
            _prizeRun.Join(p.Root.DORotateQuaternion(p.Root.rotation * Quaternion.Euler(Random.Range(-tumble, tumble), Random.Range(-tumble, tumble), Random.Range(-tumble, tumble)), _s.fallSeconds));
            _prizeRun.AppendCallback(() => Won(p));
            _prizeRun.Append(hatch.DOLocalRotateQuaternion(_hatchRest * Quaternion.AngleAxis(_s.hatchOpenAngle, Vector3.right), _s.hatchOpenSeconds).SetEase(Ease.OutBack));
            _prizeRun.AppendCallback(() => { p.Root.SetPositionAndRotation(inside, facing); p.Show(true); });
            _prizeRun.Append(p.Root.DOMove(outside, _s.exitSeconds).SetEase(Ease.OutQuad));
            _prizeRun.Append(p.Root.DOMove(land, _s.landSeconds).SetEase(Ease.InQuad));
            _prizeRun.AppendCallback(() => Landed(p, land));
            _prizeRun.Append(hatch.DOLocalRotateQuaternion(_hatchRest, _s.hatchCloseSeconds).SetEase(Ease.InOutSine));
            _prizeRun.AppendInterval(_s.showSeconds);
            _prizeRun.AppendCallback(() => { if (_round == Round.Busy && _roundId == round) EndRound(false); });
        }

        /// <summary>In the chute: the prize is won. Hidden (inside the machine), made a plain toy, saved for this slot; the
        /// toast names it, the win sound, On Won (the star), and the view goes to the flap.</summary>
        private void Won(ClawGamePrize p)
        {
            p.Show(false);
            p.MakeToy(_s.prizeMass);
            SaveGame.MarkSeen(SaveGame.Slot, p.Key);   // after it is a toy: MarkSeen raises SaveGame.Changed, and Read must find it out
            Toast(ClawGameSettings.Fill(_s.wonToast, p.Name), ToastKind.Got);
            Play(_s.win);
            onWon?.Invoke();
            Tell("WON the " + p.Name + " (" + p.Root.name + "), saved as " + p.Key);
            if (_round == Round.Busy && _view.Busy)
                _view.Shot(Clear(exit.position, chuteView.position), chuteView.rotation, _s.viewFieldOfView, _s.chuteBlend);
        }

        /// <summary>On the floor: the physics has it, rolling on away from the flap with a little tumble.</summary>
        private void Landed(ClawGamePrize p, Vector3 land)
        {
            float unit = machine.lossyScale.y;
            Vector3 away = land - exit.position;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Front();
            Vector3 spin = Random.insideUnitSphere * (_s.rollSpin * 2f * Mathf.PI);
            p.Loose(away * (_s.rollSpeed * unit), spin);
            Play(_s.land);
        }

        private void PrizeRunDone()
        {
            _prizeRun = null;
            if (_round == Round.Busy && !Running(_run)) EndRound(false);
        }

        private void RunDone()
        {
            _run = null;
            if (_round == Round.Busy && !Running(_prizeRun)) EndRound(false);   // nothing left to show
        }

        // ------------------------------------------------------------------------------------------------ ending a round

        /// <summary>Esc or the pad's B while steering: the claw goes home, the view swings back.</summary>
        private void GiveUp()
        {
            Tell("gave up");
            UiAudio.Play(UiAudio.Kind.Back);
            KillRun();
            GoHome();
            EndRound(false);
        }

        /// <summary>The player can no longer be playing (a mode change, the console, a warp...): everything is given back at
        /// once; a claw still being steered goes home, a claw already dropping plays on by itself.</summary>
        private void Abort(string why)
        {
            Tell("round stopped: " + why);
            if (_round == Round.Ready || _round == Round.Steer) { KillRun(); GoHome(); }
            EndRound(true);
        }

        /// <summary>Gives back everything the round took, exactly as it was. The view swings back (or at once); the body is
        /// shown when that swing passes Hide Body At (at once with it), and its fade comes back after it.</summary>
        private void EndRound(bool atOnce)
        {
            if (_round == Round.None) return;
            _round = Round.None;
            _hud.Hide();
            HideAim();
            KeysOff();
            _steerVelocity = Vector2.zero;
            if (_view != null)
            {
                if (atOnce || !_view.Busy) _view.Stop();
                else _view.End(_s.blendOut);
            }
            if (atOnce) ShowBody();
            if (_grabber != null) { _grabber.enabled = true; _grabber = null; }
            if (_heldRig && _rig != null) _rig.SetMenuHold(false);   // takes the mouse back if it is running
            _heldRig = false; _rig = null;
            if (_parkedInteractor != null) { _parkedInteractor.enabled = true; _parkedInteractor = null; }
            // Not while the console is open: it switches the input back on itself when it closes.
            if (_tookInput && _input != null && !DebugConsole.IsOpen) _input.enabled = true;
            _tookInput = false; _input = null; _user = null; _cam = null;
            BodyFollowsView();
        }

        /// <summary>The claw back to its rest corner from wherever it is, the fingers back as the model has them.</summary>
        private void GoHome()
        {
            _run = NewRun();
            _run.Append(AtTo(_claw.Home, Seconds(Vector2.Distance(_claw.At, _claw.Home), _s.carrySpeed)));
            _run.Join(FingersTo(0f, Seconds(Mathf.Abs(_claw.Fingers), _s.fingerSpeed)));
            if (_claw.Depth > 0f) _run.Join(DepthTo(0f, Seconds(_claw.Depth, _s.riseSpeed)));
        }

        private void KillRun()
        {
            if (_run != null) _run.Kill();
            _run = null;
        }

        /// <summary>Switched off or destroyed: every tween stops, the claw stands as the model has it, the flap is shut, and
        /// a prize on its way is put where the save says it belongs (out front if it was won, else back in the case).</summary>
        private void StopMachine()
        {
            KillRun();
            if (_prizeRun != null) _prizeRun.Kill();
            _prizeRun = null;
            if (!_ready) return;
            int slot = SaveGame.Slot, outIndex = 0;
            for (int i = 0; i < _prizes.Count; i++)
            {
                ClawGamePrize p = _prizes[i];
                if (p.Root == null) continue;
                p.Root.DOKill();
                if (p.Now == ClawGamePrize.State.Held || p.Now == ClawGamePrize.State.Falling)
                {
                    if (SaveGame.Seen(slot, p.Key)) p.PlaceOut(OutFront(outIndex++), Quaternion.LookRotation(Front(), Vector3.up), _s.prizeMass);   // won: out front
                    else p.BackInCase();
                }
            }
            _target = null;
            if (_claw.Valid) _claw.Rest();
            if (hatch != null) hatch.localRotation = _hatchRest;
            _buttonIn = 0f;
            if (_motor != null) _motor.Stop();
        }

        // ------------------------------------------------------------------------------------------------ the player's body

        private void HideBody()
        {
            if (_bodyHidden || _user == null) return;
            _bodyHidden = true;
            Hide(_user.transform);
            Carrier hands = _user.GetComponent<Carrier>();
            if (hands != null && hands.Held != null) Hide(hands.Held.transform);   // a carried tool is not under the player
            if (_fpRig != null) Hide(_fpRig);
        }

        /// <summary>Hides what is drawn under <paramref name="root"/> now; only that is shown again.</summary>
        private void Hide(Transform root)
        {
            root.GetComponentsInChildren(false, _found);
            foreach (Renderer r in _found)
                if (r.enabled && !r.forceRenderingOff) { r.forceRenderingOff = true; _hidden.Add(r); }
        }

        private void ShowBody()
        {
            foreach (Renderer r in _hidden) if (r != null) r.forceRenderingOff = false;
            _hidden.Clear();
            _bodyHidden = false;
            if (_fade != null) { _fade.enabled = true; _fade = null; }   // after the body is back (D-0234)
        }

        // ------------------------------------------------------------------------------------------------ the keys

        /// <summary>The claw's own keys on, driven by the same devices as the player (a party player's own pad).</summary>
        private void KeysOn(PlayerInput input)
        {
            InputDevice[] paired = input.Paired;
            _keys.devices = paired != null ? new ReadOnlyArray<InputDevice>(paired) : (ReadOnlyArray<InputDevice>?)null;
            _move.Enable(); _interact.Enable(); _jump.Enable(); _cancel.Enable();
        }

        private void KeysOff()
        {
            if (_move != null) _move.Disable();
            if (_interact != null) _interact.Disable();
            if (_jump != null) _jump.Disable();
            if (_cancel != null) _cancel.Disable();
        }

        // ------------------------------------------------------------------------------------------------ looking

        /// <summary>The prize the claw comes down on: of those whose middle is within the open fingers' reach of the claw's
        /// middle (their footprints touch), the one with the highest top (the first met); two about as high: the nearer.
        /// Its top (the machine's own height), how far its middle is from the claw's and its half width (world).</summary>
        private ClawGamePrize Under(Vector3 grip, float openWorld, out float top, out float offset, out float radius)
        {
            ClawGamePrize best = null;
            float bestTop = float.MinValue, tie = 0.01f * machine.lossyScale.y;
            top = 0f; offset = 0f; radius = 0f;
            for (int i = 0; i < _prizes.Count; i++)
            {
                ClawGamePrize p = _prizes[i];
                if (p.Now != ClawGamePrize.State.InCase) continue;
                Bounds b = p.WorldBounds;
                float r = 0.5f * Mathf.Max(b.size.x, b.size.z);
                float d = new Vector2(b.center.x - grip.x, b.center.z - grip.z).magnitude;
                if (d > openWorld + r) continue;   // the open fingers pass it by
                bool higher = b.max.y > bestTop + tie;
                bool asHigh = !higher && b.max.y > bestTop - tie;
                if (best == null || higher || (asHigh && d < offset))
                {
                    best = p; bestTop = Mathf.Max(bestTop, b.max.y); offset = d; radius = r;
                }
            }
            if (best != null) top = machine.InverseTransformPoint(new Vector3(grip.x, bestTop, grip.z)).y;
            return best;
        }

        /// <summary>The highest floor under the claw's middle and its open fingertips (the prize floor, or over the chute's
        /// hole its lower floor), the machine's own height: a fingertip over the prize floor stops the claw even when the
        /// middle is over the hole.</summary>
        private float FloorUnderClaw(Vector3 grip, float open)
        {
            float floor = FloorAt(grip);
            for (int i = 0; i < _claw.FingerCount; i++) floor = Mathf.Max(floor, FloorAt(_claw.TipWorld(i, open)));
            return floor;
        }

        /// <summary>The floor straight down from <paramref name="point"/> (never from above the glass's top), on the body's own
        /// collider (the machine's own height); the measured prize floor when the ray finds nothing.</summary>
        private float FloorAt(Vector3 point)
        {
            Vector3 local = machine.InverseTransformPoint(point);
            // From the point's own height (the claw before it drops), not the glass's top: the body has ledges up there (2.217
            // by the chute and near the sides, probed 25 Nov), and one taken for the floor kept the claw from coming down at all
            // (his 19:20 play: "down 0.000" near the case's sides).
            local.y = Mathf.Min(caseBox.max.y - 0.02f, local.y + 0.01f);
            var ray = new Ray(machine.TransformPoint(local), -machine.up);
            if (body.Raycast(ray, out RaycastHit hit, caseBox.size.y * 2f * machine.lossyScale.y))
                return machine.InverseTransformPoint(hit.point).y;
            return floorY;
        }

        /// <summary>The machine's front, level (its +z).</summary>
        private Vector3 Front()
        {
            Vector3 f = machine.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        /// <summary>A framed view's place: <paramref name="want"/>, pulled in toward <paramref name="look"/> if a wall,
        /// furniture or anything else solid is in the way (the machine, the prizes and the player aside).</summary>
        private Vector3 Clear(Vector3 look, Vector3 want)
        {
            Vector3 way = want - look;
            float far = way.magnitude;
            if (far < 1e-4f) return want;
            way /= far;
            float ball = Mathf.Max(0.001f, _s.viewClearance * machine.lossyScale.y);
            int n = Physics.SphereCastNonAlloc(look, ball, way, _hits, far, Layers.Seen(~0), QueryTriggerInteraction.Ignore);
            float free = far;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].distance <= 0f || Ignored(_hits[i].transform, null)) continue;
                free = Mathf.Min(free, _hits[i].distance - ball);
            }
            return look + way * Mathf.Max(free, ball);
        }

        /// <summary>Not in the way: the machine itself, its prizes, the player (and first person's body), and
        /// <paramref name="also"/>.</summary>
        private bool Ignored(Transform t, Transform also)
        {
            if (t == null) return true;
            if (t.IsChildOf(transform) || (prizes != null && t.IsChildOf(prizes))) return true;
            if (also != null && t.IsChildOf(also)) return true;
            if (_user != null && t.IsChildOf(_user.transform)) return true;
            if (_winner != null && t.IsChildOf(_winner.transform)) return true;
            return _fpRig != null && t.IsChildOf(_fpRig);
        }

        /// <summary>What is straight under <paramref name="point"/> (the room's floor, a rug...), the machine, the prizes and
        /// the player aside; the machine's own floor height when nothing is.</summary>
        private Vector3 FloorBelow(Vector3 point, Transform also)
        {
            float reach = 4f * machine.lossyScale.y;
            int n = Physics.RaycastNonAlloc(point, Vector3.down, _hits, reach, Layers.Seen(~0), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 at = new Vector3(point.x, machine.position.y, point.z);
            for (int i = 0; i < n; i++)
            {
                if (Ignored(_hits[i].transform, also) || _hits[i].distance >= best) continue;
                best = _hits[i].distance;
                at = _hits[i].point;
            }
            return at;
        }

        /// <summary>Where a won prize lands: on the floor under the flap's front, or (the kid or something else standing
        /// there) the first place beside it, a prize's width at a time along the machine's front, where it fits.</summary>
        private Vector3 LandingSpot(Vector3 outside, float radius, float tall, Transform prize)
        {
            Vector3 side = machine.right;
            side.y = 0f;
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
            float step = 2.2f * radius;
            Vector3 straight = FloorBelow(outside, prize);
            for (int k = 0; k < 9; k++)
            {
                int along = (k + 1) / 2 * ((k & 1) == 1 ? 1 : -1);   // 0, 1, -1, 2, -2...
                Vector3 spot = FloorBelow(outside + side * (along * step), prize);
                if (Fits(spot, radius, tall, prize)) return spot;
            }
            return straight;
        }

        /// <summary>True when a standing prize this size at <paramref name="feet"/> touches nothing (the floor under it aside).</summary>
        private bool Fits(Vector3 feet, float radius, float tall, Transform prize)
        {
            float lift = radius + 0.05f * Mathf.Max(radius, 0.01f);
            Vector3 a = feet + Vector3.up * lift, b = feet + Vector3.up * Mathf.Max(lift, tall - radius);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, radius, _near, Layers.Seen(~0), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (_near[i] != null && !_near[i].transform.IsChildOf(prize)) return false;
            return true;
        }

        /// <summary>Where the k-th prize won on an earlier play stands after a reload: out front, under the flap and to
        /// either side of it, Won Spacing apart.</summary>
        private Vector3 OutFront(int k)
        {
            float unit = machine.lossyScale.y;
            Vector3 side = machine.right;
            side.y = 0f;
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
            int along = (k + 1) / 2 * ((k & 1) == 1 ? 1 : -1);
            Vector3 spot = exit.position + Front() * (0.5f * _s.wonSpacing * unit) + side * (along * _s.wonSpacing * unit);
            return FloorBelow(spot, null);
        }

        // ------------------------------------------------------------------------------------------------ the save

        /// <summary>What the active slot says: a prize won on it stands out front as a toy (a reload never puts it back in
        /// the case); one it has not won (New Game, another slot) goes back in the case. A prize on its way is left alone.</summary>
        private void Read()
        {
            if (!_ready) return;
            int slot = SaveGame.Slot, outIndex = 0;
            Quaternion facing = Quaternion.LookRotation(Front(), Vector3.up);
            for (int i = 0; i < _prizes.Count; i++)
            {
                ClawGamePrize p = _prizes[i];
                if (p.Root == null) continue;
                bool won = SaveGame.Seen(slot, p.Key);
                if (won && p.Now == ClawGamePrize.State.InCase) p.PlaceOut(OutFront(outIndex), facing, _s.prizeMass);
                else if (!won && p.Now == ClawGamePrize.State.Out) p.BackInCase();
                if (won) outIndex++;
            }
        }

        /// <summary>For his testing in Play (right-click the component): the active slot forgets every prize won and they go
        /// back in the case (a New Game does the same).</summary>
        [ContextMenu("Put Won Prizes Back In The Case")]
        private void PutWonPrizesBack()
        {
            if (!Application.isPlaying || !_ready) return;
            int slot = SaveGame.Slot;
            for (int i = 0; i < _prizes.Count; i++) SaveGame.Forget(slot, _prizes[i].Key);
            Read();
        }

        // ------------------------------------------------------------------------------------------------ tweens, sounds, words

        private static float Seconds(float distance, float speed) => Mathf.Max(MinSeconds, Mathf.Abs(distance) / Mathf.Max(1e-4f, speed));

        private Sequence NewRun() => DOTween.Sequence().SetLink(gameObject).OnComplete(RunDone);
        private Tweener DepthTo(float depth, float seconds) => DOTween.To(_getDepth, _setDepth, depth, seconds).SetEase(Ease.InOutSine);
        private Tweener FingersTo(float angle, float seconds) => DOTween.To(_getFingers, _setFingers, angle, seconds).SetEase(Ease.OutQuad);
        private Tweener AtTo(Vector2 at, float seconds) => DOTween.To(_getAt, _setAt, at, seconds).SetEase(Ease.InOutQuad);

        /// <summary>The button goes in and out with its click.</summary>
        private void PressButton()
        {
            Play(_s.button);
            if (button == null || _s.buttonDepth <= 0f) return;
            Sequence press = DOTween.Sequence().SetLink(gameObject);
            press.Append(DOTween.To(_getButton, _setButton, _s.buttonDepth, _s.buttonSeconds / 3f).SetEase(Ease.OutQuad));        // in quickly
            press.Append(DOTween.To(_getButton, _setButton, 0f, _s.buttonSeconds * 2f / 3f).SetEase(Ease.InOutSine));            // out slower
        }

        private void Play(CourseSounds.Cue cue)
        {
            if (_shots == null || cue.clip == null || cue.volume <= 0f) return;
            _shots.pitch = 1f + Random.Range(-cue.pitchSpread, cue.pitchSpread);
            _shots.PlayOneShot(cue.clip, cue.volume);
        }

        /// <summary>The motor's round while anything of the claw moves (the pack plays its move, up-down and finger clips
        /// round the same way): up or down first, then across, then the fingers; it stops once all is still a moment.</summary>
        private void Motor(float dt)
        {
            if (_motor == null) return;
            CourseSounds.Cue cue = default;
            bool moving = true;
            if (Mathf.Abs(_claw.Depth - _lastDepth) > 1e-5f) cue = _s.dropLoop;
            else if ((_claw.At - _lastAt).sqrMagnitude > 1e-10f) cue = _s.moveLoop;
            else if (Mathf.Abs(_claw.Fingers - _lastFingers) > 1e-3f) cue = _s.fingerLoop;
            else moving = false;
            _lastDepth = _claw.Depth; _lastAt = _claw.At; _lastFingers = _claw.Fingers;
            _still = moving ? 0f : _still + dt;
            if (!moving)
            {
                if (_still >= SoundStill && _motor.isPlaying) _motor.Stop();
                return;
            }
            if (cue.clip == null || cue.volume <= 0f) { if (_motor.isPlaying) _motor.Stop(); return; }
            if (_motor.clip != cue.clip) { _motor.Stop(); _motor.clip = cue.clip; }
            _motor.volume = cue.volume;
            if (!_motor.isPlaying) _motor.Play();
        }

        private void Toast(string words, ToastKind kind)
        {
            if (string.IsNullOrEmpty(words)) return;
            if (_winner != null) Toasts.ShowFor(_winner, words, kind);
            else Toasts.Show(words, kind);
        }

        /// <summary>A line in the play log (only while a play is recorded).</summary>
        private static void Tell(string what)
        {
            if (PlayLog.On) PlayLog.Tell("claw machine: " + what);
        }
    }
}
