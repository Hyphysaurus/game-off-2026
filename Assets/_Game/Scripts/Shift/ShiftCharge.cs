using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The shift you carry (D-0045): the Shift action (F, pad RB) grows or shrinks you anywhere and spends a charge.
    /// Cores still shift you as before and also refill the charge (cores are batteries, D-0013); a fall reset or a loaded
    /// save refills it too, so nobody is ever stuck at the wrong size with none. The rules: feet on the ground, on foot in
    /// Play, a short wait after each shift, never inside a NoShiftZone, and growing needs room for a kid-size body where
    /// you stand ("No room to grow": the charge is kept). Shrinking always fits. How many charges, the wait and the
    /// refills are tuned on ShiftSettings. A refused press says why on the HUD and plays the "refused" sound.
    /// Added to the player by the Shifter at start; draws its own HUD (ShiftChargeHud).
    /// </summary>
    [RequireComponent(typeof(Shifter))]
    public sealed class ShiftCharge : MonoBehaviour
    {
        private const string HintId = "hint.shiftcharge";
        private const float GroundGrace = 0.15f;

        // Placeholder wording (his to change).
        public const string NoRoom = "No room to grow", NotGrounded = "Feet on the ground first", Empty = "No charge: a core refills it";
        public const string Hint = "<b>F</b> / RB  Shift anywhere. Uses the charge; cores refill it.";

        [SerializeField] private ShiftSettings settings;

        private Shifter _shifter; private PlayerMotor _motor; private PlayerInput _input; private CharacterController _controller;
        private ShiftChargeHud _hud;
        private float _readyAt, _lastGrounded = -10f;
        private static readonly Collider[] Hits = new Collider[32];

        public int Charges { get; private set; }
        public int Max => settings != null ? Mathf.Max(0, settings.maxCharges) : 1;

        /// <summary>Called by the Shifter when it adds this component, before Start.</summary>
        public void Init(ShiftSettings shiftSettings) => settings = shiftSettings;

        private void Awake()
        {
            _shifter = GetComponent<Shifter>();
            TryGetComponent(out _motor); TryGetComponent(out _input); TryGetComponent(out _controller);
        }

        private void Start()
        {
            // The Workshop is a sandbox: full from the start. The course starts empty; the first core fills it.
            Charges = SceneManager.GetActiveScene().name == TitleMenu.WorkshopScene ? Max : 0;
            _hud = ShiftChargeHud.Create(this);
        }

        private void OnEnable()
        {
            if (_input != null) _input.Shift += OnShiftPressed;
            if (_shifter != null) _shifter.Shifted += OnShifted;
            GameEvents.PlayerReset += OnPlayerReset;
        }

        private void OnDisable()
        {
            if (_input != null) _input.Shift -= OnShiftPressed;
            if (_shifter != null) _shifter.Shifted -= OnShifted;
            GameEvents.PlayerReset -= OnPlayerReset;
        }

        private void OnDestroy() { if (_hud != null) Destroy(_hud.gameObject); }

        private void Update()
        {
            if (_motor != null && _motor.Grounded) _lastGrounded = Time.time;
        }

        /// <summary>Back to full: a core used, a fall reset, a save loaded. The first refill of a save shows the hint once.</summary>
        public void Refill()
        {
            bool wasEmpty = Charges == 0;
            Charges = Max;
            if (_hud != null) _hud.Refresh();
            if (wasEmpty && Max > 0 && !SaveGame.Seen(SaveGame.Slot, HintId))
            {
                SaveGame.MarkSeen(SaveGame.Slot, HintId);
                if (_hud != null) _hud.ShowHint(Hint);
            }
        }

        /// <summary>Down to none: New Game starts here.</summary>
        public void Drain() { Charges = 0; if (_hud != null) _hud.Refresh(); }

        private void OnPlayerReset(Respawn who) { if (who != null && who.gameObject == gameObject) Refill(); }
        private void OnShifted(ShiftSize size) => _readyAt = Time.time + (settings != null ? settings.freeShiftWait : 0.5f);

        private void OnShiftPressed()
        {
            // Not on foot in Play (driving, a menu, the console, Build Mode, first person): the press is not ours. Silent.
            if (!_shifter.enabled || _shifter.Shifting || Time.time < _readyAt || Time.timeScale <= 0f) return;
            if (GameMode.Current != GameMode.Mode.Play || PauseMenu.IsOpen || TitleMenu.IsOpen || DebugConsole.IsOpen) return;
            if (_motor == null || !_motor.enabled || _controller == null || !_controller.enabled) return;

            ShiftSize to = _shifter.Size == ShiftSize.Toy ? ShiftSize.Kid : ShiftSize.Toy;
            string refused =
                Charges <= 0 ? Empty
                : Time.time - _lastGrounded > GroundGrace ? NotGrounded
                : NoShiftZone.Covering(transform.position) is { } zone ? zone.Message
                : to == ShiftSize.Kid && !RoomToGrow() ? NoRoom
                : null;
            if (refused != null)
            {
                if (_hud != null) _hud.ShowNotice(refused);
                GameEvents.RaiseShiftRefused(_shifter, refused);
                return;
            }
            Charges--;
            _shifter.ShiftTo(to);
            if (_hud != null) _hud.Refresh();
        }

        /// <summary>
        /// Whether a kid-size body fits where the toy stands: the controller's capsule at kid scale, its bottom lifted half a
        /// kid step so floor clutter a kid would step over does not count. Loose props (a moving Rigidbody) are pushed aside
        /// by a growing kid, so they do not count either; walls, furniture, shelves and ceilings do.
        /// </summary>
        private bool RoomToGrow()
        {
            Vector3 feet = transform.position + Vector3.up * ((_controller.center.y - _controller.height * 0.5f) * _shifter.Scale);
            return KidFits(feet, _controller, _shifter.ScaleOf(ShiftSize.Kid), transform);
        }

        /// <summary>The room-to-grow test on its own: does this controller's capsule at the given scale fit standing at
        /// <paramref name="feet"/>? Colliders under <paramref name="self"/> and loose props are left out.</summary>
        public static bool KidFits(Vector3 feet, CharacterController controller, float scale, Transform self)
        {
            float radius = controller.radius * scale * 0.95f;
            float height = Mathf.Max(controller.height * scale, radius * 2f);
            Vector3 up = Vector3.up;
            float lift = Mathf.Max(controller.stepOffset * scale * 0.5f, radius * 0.1f);
            Vector3 bottom = feet + up * (radius + lift);
            Vector3 top = feet + up * (height - radius);
            if (top.y < bottom.y) top = bottom;
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = Hits[i];
                if (self != null && c.transform.IsChildOf(self)) continue;
                Rigidbody body = c.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                return false;
            }
            return true;
        }
    }
}
