using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// Owns GameMode.FirstPerson. F4 toggles it, Escape goes back to Play. Entering builds (once) a Half-Life-2-style body
    /// where the player stands, at the player's size, looking where the shoulder camera looked; the third-person player is
    /// parked (motor, capsule, interactor, carrier and camera off, renderers hidden) and follows the body invisibly, so
    /// whatever asks "where is the player" still gets the right answer. Leaving puts the player where the body stands.
    /// Lives on the Player, next to PlayerMotor.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class FirstPersonMode : MonoBehaviour
    {
        [SerializeField] private FirstPersonSettings settings;
        [SerializeField, Tooltip("Found on this object if empty.")] private PlayerInput input;
        [SerializeField, Tooltip("The shoulder camera to hand over from. Found in the scene if empty.")] private ShoulderCamera shoulderCamera;
        [SerializeField] private Key toggleKey = Key.F4;

        /// <summary>The mode while first person is on; null otherwise. Enemies aim at its Body.</summary>
        public static FirstPersonMode Active { get; private set; }

        private PlayerMotor _motor;
        private CharacterController _cc;
        private Shifter _shifter;
        private Interactor _interactor;
        private Carrier _carrier;
        private WeaponHolder _holder;
        private Damageable _health;
        private GameObject _rig;
        private FirstPersonBody _body;
        private FirstPersonLook _look;
        private PhysicsGrabber _hands;
        private Camera _view;
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private readonly List<Behaviour> _parked = new List<Behaviour>();
        private bool _inside, _enterPending;

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

        /// <summary>On foot and free: not driving, not in a menu or a cutscene, not mid-shift.</summary>
        private bool CanEnter =>
            settings != null && _motor != null && _motor.enabled && _cc != null && _cc.enabled && input != null && input.enabled
            && (_shifter == null || !_shifter.Shifting) && Time.timeScale > 0f;

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && !DebugConsole.IsOpen)   // the console's typing is not a key press
            {
                if (k[toggleKey].wasPressedThisFrame)
                {
                    if (GameMode.Current == GameMode.Mode.FirstPerson) GameMode.Set(GameMode.Mode.Play);
                    else if (CanEnter || GameMode.Current == GameMode.Mode.Build) GameMode.Set(GameMode.Mode.FirstPerson);
                }
                else if (_inside && k.escapeKey.wasPressedThisFrame) GameMode.Set(GameMode.Mode.Play);
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
            if (shoulderCamera == null) shoulderCamera = FindFirstObjectByType<ShoulderCamera>();
            _view = shoulderCamera != null ? shoulderCamera.GetComponent<Camera>() : Camera.main;
            _rig = new GameObject("FirstPersonBody");
            _rig.SetActive(false);
            _rig.layer = gameObject.layer;
            _rig.AddComponent<CharacterController>();
            _body = _rig.AddComponent<FirstPersonBody>();
            _look = _rig.AddComponent<FirstPersonLook>();
            _hands = _rig.AddComponent<PhysicsGrabber>();
            _body.Setup(settings, input, _shifter, transform, _hands);
            _look.Setup(settings, input, _body, _view, _hands);
            _hands.Setup(settings, input, _body, _view != null ? _view.transform : null, _holder, _interactor);
        }

        private void Enter()
        {
            if (_rig == null) BuildRig();
            if (_view == null) { GameMode.Set(GameMode.Mode.Play); return; }
            float yaw = shoulderCamera != null ? shoulderCamera.Yaw : transform.eulerAngles.y;
            float pitch = shoulderCamera != null ? shoulderCamera.Pitch : 0f;

            if (_carrier != null && _carrier.Held != null) _carrier.PutDown();
            _parked.Clear();
            Park(_motor); Park(_interactor); Park(_carrier); Park(shoulderCamera);
            if (_cc.enabled) { _cc.enabled = false; }

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
            _cc.enabled = true;
            foreach (var b in _parked) if (b != null) b.enabled = true;
            _parked.Clear();
            if (_motor != null) _motor.Warp(feet, Quaternion.Euler(0f, yaw, 0f));
            else transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            if (shoulderCamera != null) shoulderCamera.Warp(yaw, pitch);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            // The parked player rides along invisibly: prompts, enemies, saves and the weapon holder read its transform.
            if (_inside) transform.SetPositionAndRotation(_body.transform.position, Quaternion.Euler(0f, _look.Yaw, 0f));
        }
    }
}
