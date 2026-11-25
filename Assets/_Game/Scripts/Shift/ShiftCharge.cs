using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The shift you carry (D-0045): the Shift action (F, pad RB) grows or shrinks you anywhere and spends a charge.
    /// Cores still shift you as before and also refill the charge (cores are batteries, D-0013); a fall reset or a loaded
    /// save refills it too, so nobody is ever stuck at the wrong size with none. The rules: on foot in Play, a short wait
    /// after each shift, never inside a NoShiftZone, and growing needs room for a kid-size body ("No room to grow": the
    /// charge is kept). Shrinking always fits. On the ground the feet stay put, as always. In the air (D-0094) the body
    /// changes size round its middle: the toy appears where the kid's chest was, the kid grows round the toy (and stands on
    /// the floor below if the floor is nearer than a kid's half height), and the speed carries over in proportion. The first
    /// shrink in the air without a glider says once that falling toys break. How many charges, the wait and the refills are
    /// tuned on ShiftSettings. A refused press says why on the HUD and plays the "refused" sound.
    /// Added to the player by the Shifter at start; draws its own HUD (ShiftChargeHud).
    /// </summary>
    [RequireComponent(typeof(Shifter))]
    public sealed class ShiftCharge : MonoBehaviour
    {
        private const string HintId = "hint.shiftcharge", GliderHintId = "hint.findglider";
        private const float GroundGrace = 0.15f;

        // Placeholder wording (his to change).
        public const string NoRoom = "No room to grow", Empty = "No charge: a core refills it";
        public const string Hint = "<b>F</b> / RB  Shift anywhere. Uses the charge; cores refill it.";

        [SerializeField] private ShiftSettings settings;

        private Shifter _shifter; private PlayerMotor _motor; private PlayerInput _input; private CharacterController _controller;
        private Respawn _respawn;
        private ShiftChargeHud _hud;
        private float _readyAt, _lastGrounded = -10f;
        private bool _free;   // this scene's shift costs no charge (TitleMenu.IsFreeShift)
        /// <summary>The shift costs nothing here: a free-shift scene, or online (D-0125: the house as a sandbox; the host's
        /// charge was set up on the title, before the party, so this is read at each press).</summary>
        private bool Free => _free || NetSession.InParty;
        private static readonly Collider[] Hits = new Collider[32];
        private static readonly RaycastHit[] Below = new RaycastHit[16];

        public int Charges { get; private set; }
        public int Max => settings != null ? Mathf.Max(0, settings.maxCharges) : 1;

        /// <summary>Called by the Shifter when it adds this component, before Start.</summary>
        public void Init(ShiftSettings shiftSettings) => settings = shiftSettings;

        private void Awake()
        {
            _shifter = GetComponent<Shifter>();
            TryGetComponent(out _motor); TryGetComponent(out _input); TryGetComponent(out _controller); TryGetComponent(out _respawn);
        }

        private void Start()
        {
            // A sandbox (the Workshop, the Raceway) is full from the start. The course starts empty; the first core fills it.
            Charges = TitleMenu.IsSandbox(SceneManager.GetActiveScene().name) ? Max : 0;
            _free = TitleMenu.IsFreeShift(SceneManager.GetActiveScene().name);
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
                if (_hud != null) _hud.ShowHint(Hint, ToastKind.Tip, "Shift charge");
            }
        }

        /// <summary>A toast for this player (the glider's "how to", D-0095): only on the screen where it is played.</summary>
        public void ShowHint(string text, ToastKind kind = ToastKind.Tip, string title = null) { if (_hud != null) _hud.ShowHint(text, kind, title); }

        /// <summary>True on the PC where this player is played: its HUD exists (D-0162 toasts check it).</summary>
        public bool HasHud => _hud != null;

        /// <summary>Exactly this many (clamped to 0..Max): what a door between the house's rooms carried over (D-0091).</summary>
        public void SetCharges(int charges) { Charges = Mathf.Clamp(charges, 0, Max); if (_hud != null) _hud.Refresh(); }

        /// <summary>Down to none: New Game starts here.</summary>
        public void Drain() { Charges = 0; if (_hud != null) _hud.Refresh(); }

        private void OnPlayerReset(Respawn who) { if (who != null && who.gameObject == gameObject) Refill(); }
        private void OnShifted(ShiftSize size) => _readyAt = Time.time + (settings != null ? settings.freeShiftWait : 0.5f);

        private void OnShiftPressed()
        {
            // Not on foot in Play (driving, a menu, the console, Build Mode, first person): the press is not ours. Silent.
            if (!_shifter.enabled || _shifter.Shifting || Time.time < _readyAt || Time.timeScale <= 0f) return;
            // Not while a fall reset's black cut runs (D-0212 fix, his 25 Nov play): the reset has just refilled the charge,
            // and a grow taken in the pit he fell into put a kid-size body back into the toy-only hallway, out through its ceiling.
            if (_respawn != null && _respawn.Busy) return;
            if (GameMode.Current != GameMode.Mode.Play || PauseMenu.IsOpen || TitleMenu.IsOpen || DebugConsole.IsOpen) return;
            if (_motor == null || !_motor.enabled || _controller == null || !_controller.enabled) return;
            var moves = GetComponent<PlayerMoves>();   // no size shift mid-roll (or mid-punch, block, hit)
            if (moves != null && !moves.Free) return;

            ShiftSize to = _shifter.Size == ShiftSize.Toy ? ShiftSize.Kid : ShiftSize.Toy;
            // On the ground (with the same short grace that once refused the air): the feet stay put, as always. In the air
            // the body changes round its middle (D-0094).
            bool inAir = Time.time - _lastGrounded > GroundGrace;
            string refused =
                to == ShiftSize.Kid && TeamMatch.Running ? TeamMatch.NoGrowing   // toy size only in a team match (D-0182)
                : Charges <= 0 && !Free ? Empty
                : NoShiftZone.Covering(transform.position) is { } zone ? zone.Message
                : to == ShiftSize.Kid && !(inAir ? RoomToGrowInAir() : RoomToGrow()) ? NoRoom
                : null;
            if (refused != null)
            {
                if (_hud != null) _hud.ShowNotice(refused);
                GameEvents.RaiseShiftRefused(_shifter, refused);
                return;
            }
            if (!Free) Charges--;
            _shifter.ShiftTo(to, inAir);
            if (_hud != null) _hud.Refresh();
            // The first toy made in the air with no glider to open: it is about to fall, and falls break toys (said once a save).
            var glider = GetComponent<Glider>();   // plain form: Fast Script Reload breaks `TryGetComponent(out ...) &&`
            if (inAir && to == ShiftSize.Toy && (glider == null || !glider.Owned) && !SaveGame.Seen(SaveGame.Slot, GliderHintId))
            {
                SaveGame.MarkSeen(SaveGame.Slot, GliderHintId);
                ShowHint(Glider.FindHint, ToastKind.Warning);
            }
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

        /// <summary>
        /// In the air the kid grows round the toy: the two middles together (D-0094). If that kid would reach into what is
        /// under the toy (a floor nearer than a kid's half height), it stands on it instead, the toy's middle still inside
        /// its body. Either has to fit, or "No room to grow".
        /// </summary>
        private bool RoomToGrowInAir()
        {
            float kid = _shifter.ScaleOf(ShiftSize.Kid), centre = _controller.center.y;
            Vector3 middle = transform.position + Vector3.up * (centre * _shifter.Scale);
            Vector3 feet = middle - Vector3.up * (centre * kid);
            if (KidFits(feet, _controller, kid, transform)) return true;
            float reach = middle.y - feet.y;
            int n = Physics.RaycastNonAlloc(middle, Vector3.down, Below, reach, ~0, QueryTriggerInteraction.Ignore);
            float ground = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                Collider c = Below[i].collider;
                if (c.transform.IsChildOf(transform) || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)) continue;
                ground = Mathf.Max(ground, Below[i].point.y);
            }
            return !float.IsNegativeInfinity(ground) && KidFits(new Vector3(feet.x, ground, feet.z), _controller, kid, transform);
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
                if (HitShapes.IsShape(c)) continue;   // a figure's hit shape stops nothing
                return false;
            }
            return true;
        }
    }
}
