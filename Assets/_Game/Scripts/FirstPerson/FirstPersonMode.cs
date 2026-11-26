using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// Owns GameMode.FirstPerson. V toggles it anywhere on foot, Escape goes back to Play. Aim (right mouse) with a
    /// weapon drawn narrows the view. Entering builds (once) a Half-Life-2-style body
    /// where the player stands, at the player's size, looking where the shoulder camera looked; the third-person player is
    /// parked (motor, capsule, interactor, carrier, camera and body fade off, renderers hidden) and follows the body
    /// invisibly, so
    /// whatever asks "where is the player" still gets the right answer. Leaving puts the player where the body stands.
    /// Since D-0194 that separate body moves only while FirstPersonSettings > Ride Player is OFF. With it on (as it
    /// ships) first person is a view on the player itself: the player's own motor and capsule stay on and move as in
    /// third person, facing wherever the eyes look, and the first-person body only stands where the player is for the
    /// view and the hands. So a rope, the glider, a launch pad or a line work in first person as they do out of it.
    /// Lives on the Player, next to PlayerMotor.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class FirstPersonMode : MonoBehaviour
    {
        [SerializeField] private FirstPersonSettings settings;
        [SerializeField, Tooltip("Found on this object if empty.")] private PlayerInput input;
        [SerializeField, Tooltip("The shoulder camera to hand over from. Found in the scene if empty.")] private ShoulderCamera shoulderCamera;
        [SerializeField] private Key toggleKey = Key.V;

        /// <summary>The mode while first person is on; null otherwise. Enemies aim at its Body.</summary>
        public static FirstPersonMode Active { get; private set; }

        private PlayerMotor _motor;
        private CharacterController _cc;
        private Shifter _shifter;
        private Interactor _interactor;
        private Carrier _carrier;
        private WeaponHolder _holder;
        private Damageable _health;
        private PlayerMoves _moves;
        private BodyFade _fade;   // parked first while first person hides the body (D-0234)
        private GameObject _rig;
        private FirstPersonBody _body;
        private FirstPersonLook _look;
        private PhysicsGrabber _hands;
        private Camera _view;
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private readonly List<Behaviour> _parked = new List<Behaviour>();
        private bool _inside, _enterPending;
        private bool _riding;   // this stay in first person is on the player itself (D-0194): read from the settings on entering

        public FirstPersonBody Body => _inside ? _body : null;
        public bool InFirstPerson => _inside;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            _motor = GetComponent<PlayerMotor>();
            _cc = GetComponent<CharacterController>();
            _shifter = GetComponent<Shifter>();
            _interactor = GetComponent<Interactor>();
            _carrier = GetComponent<Carrier>();
            _holder = GetComponent<WeaponHolder>();
            _health = GetComponent<Damageable>();
            _moves = GetComponent<PlayerMoves>();
            _fade = GetComponent<BodyFade>();
        }

        private void OnEnable()
        {
            GameMode.Changed += OnModeChanged;
            if (_motor != null) _motor.Warped += OnWarped;
            if (_health != null) _health.Damaged += OnHurt;
        }

        private void OnDisable()
        {
            GameMode.Changed -= OnModeChanged;
            if (_motor != null) _motor.Warped -= OnWarped;
            if (_health != null) _health.Damaged -= OnHurt;
            if (_inside) { Exit(); if (GameMode.Current == GameMode.Mode.FirstPerson) GameMode.ResetQuietly(); }
        }

        /// <summary>Something moved the player (a respawn, a death, the console's tp): the body goes there too, so the warp
        /// sticks and we stay in first person. Exit's own warp comes after _inside is cleared, so it is not caught here.</summary>
        private void OnWarped(Vector3 position, Quaternion rotation)
        {
            if (!_inside) return;
            _hands.Release();
            _body.Place(position);
            _look.Place(rotation.eulerAngles.y, _look.Pitch);
        }

        /// <summary>The player's own flash is on hidden renderers in here: the view kicks and the screen tints instead.
        /// The shove reaches the body through Knockback.</summary>
        private void OnHurt(DamageInfo hit)
        {
            if (_inside) _look.Hurt(hit.Amount / Mathf.Max(1f, _health.MaxHealth));
        }

        private void OnDestroy()
        {
            if (_rig != null) Destroy(_rig);
            if (Active == this) Active = null;
        }

        /// <summary>The words when the first-person key is pressed while a Flat Zone holds the player.</summary>
        public const string NotInFlatZone = "No first person in a Flat Zone";

        /// <summary>A Flat Zone holds this player: the side-on view is the zone's, so first person waits until it lets go
        /// (D-0212 fix, his 25 Nov play: "in flat zone, FPS breaks the mode").</summary>
        private bool HeldFlat => FlatZone.Current != null && _motor != null && FlatZone.Current.Holds(_motor);

        /// <summary>On foot and free: not driving, not in a menu or a cutscene, not mid-shift, not held by a Flat Zone.</summary>
        private bool CanEnter =>
            settings != null && _motor != null && _motor.enabled && _cc != null && _cc.enabled && input != null && input.enabled
            && (_shifter == null || !_shifter.Shifting) && (_moves == null || _moves.Free) && Time.timeScale > 0f && !HeldFlat;

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && !DebugConsole.IsOpen)   // the console's typing is not a key press
            {
                if (k[toggleKey].wasPressedThisFrame)
                {
                    if (GameMode.Current == GameMode.Mode.FirstPerson) GameMode.Set(GameMode.Mode.Play);
                    else if (HeldFlat) Toasts.ShowFor(_motor, NotInFlatZone, ToastKind.Refused);   // the zone keeps its side-on view
                    else if (CanEnter || GameMode.Current == GameMode.Mode.Build) GameMode.Set(GameMode.Mode.FirstPerson);
                }
                // Not on the frame the armory bench's screen closed on this same Esc (D-0167): that screen runs before
                // this script and has already used the key, so Esc at the bench only closes the bench.
                else if (_inside && !BenchScreen.TookEsc && k.escapeKey.wasPressedThisFrame) GameMode.Set(GameMode.Mode.Play);
            }

            // On the player itself (D-0194): the body faces wherever the eyes look, at once. Told every frame before the
            // motor moves (this script runs first).
            if (_inside && _riding && _motor != null) _motor.ViewYaw = _look.Yaw;

            if (_inside && _holder != null)
            {
                // Aim held with a weapon out (not while the hands hold a prop: right mouse turns that): the view narrows.
                _look.Aiming = _holder.AimHeld && _hands.Held == null;
                _look.SightFieldOfView = _holder.SightFieldOfView;
            }

            // Entering waits a frame, so whichever mode we came from has finished handing the player back.
            if (_enterPending)
            {
                _enterPending = false;
                if (GameMode.Current != GameMode.Mode.FirstPerson) return;
                if (CanEnter) Enter();
                else GameMode.Set(GameMode.Mode.Play);
            }
        }

        private void OnModeChanged(GameMode.Mode from, GameMode.Mode to)
        {
            if (to == GameMode.Mode.FirstPerson) _enterPending = true;
            else if (from == GameMode.Mode.FirstPerson) { _enterPending = false; if (_inside) Exit(); }
        }

        private void BuildRig()
        {
            if (shoulderCamera == null) shoulderCamera = PlayerSlot.RigFor(this);
            _view = shoulderCamera != null ? shoulderCamera.GetComponent<Camera>() : PlayerSlot.ViewFor(this);
            _rig = new GameObject("FirstPersonBody");
            _rig.SetActive(false);
            _rig.layer = gameObject.layer;
            _rig.AddComponent<CharacterController>();
            _body = _rig.AddComponent<FirstPersonBody>();
            _look = _rig.AddComponent<FirstPersonLook>();
            _hands = _rig.AddComponent<PhysicsGrabber>();
            // The motor and the moves' numbers go with it: matched (FirstPersonSettings), the body moves at theirs (D-0175).
            _body.Setup(settings, input, _shifter, transform, _hands, _motor, _moves != null && _moves.Settings != null ? _moves.Settings : PlayerMoveSettings.Load());
            _look.Setup(settings, input, _body, _view, _hands);
            _hands.Setup(settings, input, _body, _view != null ? _view.transform : null, _holder, _interactor);
        }

        private void Enter()
        {
            if (_rig == null) BuildRig();
            if (_view == null) { GameMode.Set(GameMode.Mode.Play); return; }
            float yaw = shoulderCamera != null ? shoulderCamera.Yaw : transform.eulerAngles.y;
            float pitch = shoulderCamera != null ? shoulderCamera.Pitch : 0f;

            // A tool in one hand stays in it: first person holds it in the view body's hand (D-0144). Only something in both
            // hands (a plank) is set down: first person's own hands carry props.
            if (_carrier != null && _carrier.Held != null && _carrier.Held.TwoHanded) _carrier.PutDown();
            // On the player itself (D-0194) the motor and the capsule stay on: the player moves, the body rides.
            _riding = settings.ridePlayer && _motor != null;
            _body.Ride(_riding);
            _riding = _body.Riding;
            _parked.Clear();
            // D-0234: the body fade goes off first. Switched off, it shows the body fully at once (its materials and shadows
            // back), so what is hidden below, and what the gun's view body later puts back "as it was", is the real body,
            // never the fade's shadow-only one. His 25 Nov play: first person with a gun out while the camera was pushed
            // into the kid, and the body stayed invisible in third person afterwards.
            Park(_fade);
            if (!_riding) Park(_motor);
            Park(_interactor); Park(shoulderCamera);
            if (!_riding && _cc.enabled) { _cc.enabled = false; }

            _hidden.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r.forceRenderingOff || r.GetComponentInParent<Weapon>() != null) continue;
                r.forceRenderingOff = true;
                _hidden.Add(r);
            }

            _body.Place(transform.position);
            _look.Place(yaw, pitch);
            _rig.SetActive(true);
            if (_holder != null) _holder.SetFirstPerson(_rig.transform, _view.transform, _look, _hands);
            _inside = true;
            Active = this;
        }

        private void Park(Behaviour b)
        {
            if (b == null || !b.enabled) return;
            b.enabled = false;
            _parked.Add(b);
        }

        private void Exit()
        {
            _inside = false;
            if (Active == this) Active = null;
            _hands.Release();
            Vector3 feet = _body.transform.position;
            float yaw = _look.Yaw, pitch = _look.Pitch;
            _rig.SetActive(false);
            if (_holder != null) _holder.SetFirstPerson(null, null, null, null);

            foreach (var r in _hidden) if (r != null) r.forceRenderingOff = false;
            _hidden.Clear();
            foreach (var b in _parked) if (b != null) b.enabled = true;
            _parked.Clear();
            if (_riding)
            {
                // The player never left: it is where it stands and keeps its speed (it may be on a rope or the glider).
                if (_motor != null) _motor.ViewYaw = null;
                _riding = false;
            }
            else
            {
                _cc.enabled = true;
                if (_motor != null) _motor.Warp(feet, Quaternion.Euler(0f, yaw, 0f));
                else transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            }
            if (shoulderCamera != null) shoulderCamera.Warp(yaw, pitch);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (!_inside) return;
            // On the player itself (D-0194): the body stands where the player is, for the view (which runs after this).
            if (_riding) { _rig.transform.position = transform.position; return; }
            // The parked player rides along invisibly: prompts, enemies, saves and the weapon holder read its transform.
            transform.SetPositionAndRotation(_body.transform.position, Quaternion.Euler(0f, _look.Yaw, 0f));
        }
    }
}
