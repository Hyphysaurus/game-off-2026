using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// The only script that knows about devices. It turns the Input System asset's "Player" map into
    /// intents the rest of the game reads: Move, Sprint, a look delta, and Jump / Interact presses. On foot the keys
    /// follow the test room: C crouch (hold), Left Ctrl dodge roll, Q block (hold), left mouse fire or punch, right mouse
    /// aim or kick, X put down what is carried or holster / draw, Z drop the weapon, R reload.
    /// A car reads it as its driver (ICarDriver: Move, JumpHeld and Sprint, as they are).
    /// </summary>
    public sealed class PlayerInput : MonoBehaviour, ICarDriver
    {
        [SerializeField, Tooltip("The project's InputSystem_Actions asset. A private copy is made at runtime.")]
        private InputActionAsset actions;

        private InputActionAsset _instance;
        private InputActionMap _player;
        private InputAction _move, _look, _jump, _sprint, _interact, _use, _drop, _previous, _next, _shift, _crouch, _aim, _dodge, _block;
        // Workshop extras the asset has no actions for: made here so this stays the only script that knows devices.
        private InputAction _dropWeapon, _scroll, _rotate, _reload;
        private Vector2 _mouseLook;   // pixels moved since the last read
        private Vector2 _stickLook;   // current right-stick value, -1..1

        /// <summary>Stick or WASD direction, clamped to length 1.</summary>
        public Vector2 Move => _move != null ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public bool Sprint => _sprint != null && _sprint.IsPressed();
        /// <summary>True while the Jump button is still down; the motor cuts a jump short when it is let go early.</summary>
        public bool JumpHeld => _jump != null && _jump.IsPressed();
        /// <summary>Raised once per press of the Jump action.</summary>
        public event Action Jump;
        /// <summary>Raised once per press of the Interact action (on the press, not after the asset's Hold).</summary>
        public event Action Interact;
        /// <summary>Raised once per press of the Attack action: use whatever is carried (the flashlight's switch).</summary>
        public event Action UseHeld;
        /// <summary>Raised once per press of the Holster action (X, pad B): put the carried thing down; with empty hands
        /// the WeaponHolder holsters or draws.</summary>
        public event Action Drop;
        /// <summary>True while Attack is held: automatic weapons keep firing.</summary>
        public bool AttackHeld => _use != null && _use.IsPressed();
        /// <summary>True while Crouch is held (C, pad R3): the player crouches, in third and first person.</summary>
        public bool CrouchHeld => _crouch != null && _crouch.IsPressed();
        /// <summary>True while Aim is held (right mouse, pad LT): a drawn weapon aims.</summary>
        public bool AimHeld => _aim != null && _aim.IsPressed();
        /// <summary>Raised once per press of the Aim action: with empty hands it is the kick.</summary>
        public event Action AimPressed;
        /// <summary>True while Block is held (Q, pad LB).</summary>
        public bool BlockHeld => _block != null && _block.IsPressed();
        /// <summary>Raised once per press of the Dodge action (Left Ctrl, pad RT): the dodge roll.</summary>
        public event Action Dodge;
        /// <summary>True while the rotate key (R or the right mouse button) is held: the first-person grab turns the held thing.</summary>
        public bool RotateHeld => _rotate != null && _rotate.IsPressed();
        /// <summary>Mouse wheel notches this frame (+1 up, -1 down), 0 when still.</summary>
        public float Scroll { get { float y = _scroll != null ? _scroll.ReadValue<float>() : 0f; return Mathf.Abs(y) < 0.01f ? 0f : Mathf.Sign(y); } }
        /// <summary>Raised on Previous (1) and Next (2): cycle weapons.</summary>
        public event Action Previous, Next;
        /// <summary>Raised once per press of the Shift action (F, pad RB): the carried shift (ShiftCharge).</summary>
        public event Action Shift;
        /// <summary>Raised on Z (pad: D-pad down): drop the weapon in hand.</summary>
        public event Action DropWeapon;
        /// <summary>Raised on R, and on the pad's Y: reload the gun in hand. The bool is true when the press came from
        /// the button that is also Interact (the pad has no button to spare): the listener leaves it alone when there
        /// is something to use.</summary>
        public event Action<bool> Reload;

        private void Awake()
        {
            _instance = Instantiate(actions);
            _player = _instance.FindActionMap("Player", throwIfNotFound: true);
            _move = _player.FindAction("Move", true);
            _look = _player.FindAction("Look", true);
            _jump = _player.FindAction("Jump", true);
            _sprint = _player.FindAction("Sprint", true);
            _interact = _player.FindAction("Interact", true);
            _use = _player.FindAction("Attack", true);
            _drop = _player.FindAction("Holster", true);
            _crouch = _player.FindAction("Crouch", true);
            _aim = _player.FindAction("Aim", true);
            _dodge = _player.FindAction("Dodge", true);
            _block = _player.FindAction("Block", true);
            _previous = _player.FindAction("Previous", true);
            _next = _player.FindAction("Next", true);
            _shift = _player.FindAction("Shift", true);
            _dropWeapon = new InputAction("DropWeapon", InputActionType.Button, "<Keyboard>/z");
            _dropWeapon.AddBinding("<Gamepad>/dpad/down");
            _scroll = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll/y");
            _rotate = new InputAction("RotateHeld", InputActionType.Button, "<Keyboard>/r");
            _rotate.AddBinding("<Mouse>/rightButton");
            _rotate.AddBinding("<Gamepad>/leftShoulder");
            _reload = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
            _reload.AddBinding("<Gamepad>/buttonNorth");
        }

        private void OnEnable()
        {
            _jump.performed += OnJump;
            _interact.started += OnInteract;
            _use.performed += OnUse;
            _drop.performed += OnDrop;
            _aim.performed += OnAim;
            _dodge.performed += OnDodge;
            _look.performed += OnLook;
            _look.canceled += OnLook;
            _previous.performed += OnPrevious;
            _next.performed += OnNext;
            _shift.performed += OnShift;
            _dropWeapon.performed += OnDropWeapon;
            _reload.performed += OnReload;
            _player.Enable();
            _dropWeapon.Enable(); _scroll.Enable(); _rotate.Enable(); _reload.Enable();
        }

        private void OnDisable()
        {
            _player.Disable();
            _dropWeapon.Disable(); _scroll.Disable(); _rotate.Disable(); _reload.Disable();
            _reload.performed -= OnReload;
            _previous.performed -= OnPrevious;
            _next.performed -= OnNext;
            _shift.performed -= OnShift;
            _dropWeapon.performed -= OnDropWeapon;
            _jump.performed -= OnJump;
            _interact.started -= OnInteract;
            _use.performed -= OnUse;
            _drop.performed -= OnDrop;
            _aim.performed -= OnAim;
            _dodge.performed -= OnDodge;
            _look.performed -= OnLook;
            _look.canceled -= OnLook;
            _mouseLook = _stickLook = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (_instance != null) Destroy(_instance);
            _dropWeapon?.Dispose(); _scroll?.Dispose(); _rotate?.Dispose(); _reload?.Dispose();
        }

        private void OnJump(InputAction.CallbackContext _) => Jump?.Invoke();
        private void OnInteract(InputAction.CallbackContext _) => Interact?.Invoke();
        private void OnUse(InputAction.CallbackContext _) => UseHeld?.Invoke();
        private void OnDrop(InputAction.CallbackContext _) => Drop?.Invoke();
        private void OnAim(InputAction.CallbackContext _) => AimPressed?.Invoke();
        private void OnDodge(InputAction.CallbackContext _) => Dodge?.Invoke();
        private void OnPrevious(InputAction.CallbackContext _) => Previous?.Invoke();
        private void OnNext(InputAction.CallbackContext _) => Next?.Invoke();
        private void OnShift(InputAction.CallbackContext _) => Shift?.Invoke();
        private void OnDropWeapon(InputAction.CallbackContext _) => DropWeapon?.Invoke();
        private void OnReload(InputAction.CallbackContext ctx) => Reload?.Invoke(ctx.control != null && ctx.control.device is Gamepad);

        private void OnLook(InputAction.CallbackContext ctx)
        {
            Vector2 value = ctx.ReadValue<Vector2>();
            // A mouse or touch gives pixels moved this frame: accumulate. A stick gives a held value: keep the latest.
            if (ctx.control.device is Pointer) _mouseLook += value;
            else _stickLook = value;
        }

        /// <summary>
        /// The look turn for this frame, in degrees: mouse pixels times sensitivity plus stick times degrees per second times dt.
        /// Reading it clears the mouse delta, so call it once per frame.
        /// </summary>
        public Vector2 ConsumeLook(float degreesPerPixel, float padDegreesPerSecond, float deltaTime)
        {
            Vector2 look = _mouseLook * degreesPerPixel + _stickLook * (padDegreesPerSecond * deltaTime);
            _mouseLook = Vector2.zero;
            return look;
        }
    }
}
