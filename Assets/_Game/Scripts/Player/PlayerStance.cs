using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Standing or crouched. Crouch is held; let go under something low and the player stays crouched until a standing
    /// body fits again, then stands by itself. It is the only script that sizes the player's capsule (the bottom stays
    /// at the feet), and it tells the motor the crouch speed; PlayerMoves reads NoHeadroom and holds the jump back.
    /// The Shifter scales the whole player, so the capsule's own numbers stay at toy size.
    /// </summary>
    [DefaultExecutionOrder(-20)]   // before the moves and the motor read it this frame
    [RequireComponent(typeof(CharacterController), typeof(PlayerMotor))]
    public sealed class PlayerStance : MonoBehaviour
    {
        [SerializeField, Tooltip("Resources/PlayerMoves is loaded if empty.")] private PlayerMoveSettings settings;
        [SerializeField, Tooltip("Found on this object if empty.")] private PlayerInput input;

        private CharacterController _cc;
        private PlayerMotor _motor;
        private float _standHeight, _standCentre;
        private readonly Collider[] _overlap = new Collider[16];

        /// <summary>True while crouched: the key is held, or it was let go somewhere too low to stand.</summary>
        public bool Crouched { get; private set; }
        /// <summary>Whoever runs a move (a roll, a hit) sets this: the stance then keeps whichever capsule it has.</summary>
        public bool Frozen { get; set; }
        /// <summary>True while crouched under something too low to stand up: a jump would need to stand first.</summary>
        public bool NoHeadroom { get; private set; }
        /// <summary>The standing capsule's height in world units: the "body height" every move is measured in.</summary>
        public float BodyHeight => _standHeight * transform.lossyScale.y;
        /// <summary>The capsule's height now as a share of standing (1 standing): the camera lowers its pivot by it.</summary>
        public float HeightShare => _standHeight > 0f ? _cc.height / _standHeight : 1f;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _motor = GetComponent<PlayerMotor>();
            if (input == null) input = GetComponent<PlayerInput>();
            if (settings == null) settings = PlayerMoveSettings.Load();
            _standHeight = _cc.height;
            _standCentre = _cc.center.y - _cc.height * 0.5f;   // the capsule's bottom above the feet, kept
        }

        private void OnDisable()
        {
            // Parked (driving, first person): stand, so the next owner of the capsule finds it as it was.
            Set(false);
            NoHeadroom = false;
            _motor.SpeedCap = float.PositiveInfinity;
        }

        private void Update()
        {
            if (settings == null) return;
            if (!_motor.enabled) { Set(false); NoHeadroom = false; return; }   // parked (driving, first person): standing
            if (!Frozen)
            {
                bool want = input != null && input.CrouchHeld;
                if (want) Set(true);
                else if (Crouched && StandFits()) Set(false);
            }
            _motor.SpeedCap = Crouched ? settings.crouchSpeed : float.PositiveInfinity;
            // A jump from a crouch stands up first, so it needs room overhead. Without it, no jump.
            NoHeadroom = Crouched && !StandFits();
        }

        /// <summary>Crouch or stand at once, without the headroom check (StandFits is the check).</summary>
        public void Set(bool crouched)
        {
            if (crouched == Crouched || settings == null) return;
            Crouched = crouched;
            float height = _standHeight * (crouched ? settings.crouchHeight : 1f);
            _cc.height = height;
            _cc.center = new Vector3(_cc.center.x, _standCentre + height * 0.5f, _cc.center.z);
        }

        /// <summary>True if a standing player fits where this one is. Things the player carries move with it and do not
        /// count; loose props do not count either (they are pushed aside, not a ceiling).</summary>
        public bool StandFits()
        {
            if (!Crouched) return true;
            float s = transform.lossyScale.y;
            float inset = settings.standInset * _standHeight * s;
            float radius = _cc.radius * s - inset;
            float height = _standHeight * s - inset;
            Vector3 feet = transform.position + Vector3.up * (_standCentre * s + inset);
            Vector3 a = feet + Vector3.up * radius, b = feet + Vector3.up * Mathf.Max(radius, height - radius);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, radius, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = _overlap[i];
                if (c.transform.IsChildOf(transform)) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
                if (HitShapes.IsShape(c)) continue;   // a figure's hit shape stops nothing
                return false;
            }
            return true;
        }
    }
}
