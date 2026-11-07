using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A spring: step on it and you are thrown up. A pillow is a trampoline for a toy. Height is in the
    /// player's own units (a toy flies 8 of its units, a kid 8 of its), size-gated so the kid walks over toy
    /// pads. Works on foot and in first person. The visual squashes on each bounce and is left alone between
    /// bounces, so Build Mode can scale it. A pad fires when you come down onto it, not while you rise through it.
    /// Side mode (D-0039): the trigger wraps a pillow's sides instead; running into it throws the toy just over the
    /// trigger's top and inward, so it lands on the pillow (and its top pad). The throw is worked out each time from
    /// where the player stands, so it fits wherever the kid puts the pillow and however Build Mode scales it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class LaunchPad : MonoBehaviour, IBuildMoved
    {
        [SerializeField] private bool anySize;
        [SerializeField] private ShiftSize requiredSize = ShiftSize.Toy;
        [SerializeField, Tooltip("How high it throws, in the player's own units (toy: 1 = half its height).")] private float height = 8f;
        [SerializeField, Tooltip("Seconds before it fires again.")] private float cooldown = 0.35f;
        [SerializeField, Tooltip("The part that squashes. This object if empty.")] private Transform visual;
        [SerializeField, Range(0.2f, 1f)] private float squash = 0.55f;
        [SerializeField] private float squashTime = 0.22f;
        [SerializeField] private UnityEvent onLaunch;
        [Header("Side bounce (a pillow's sides)")]
        [SerializeField, Tooltip("On: this trigger wraps a pillow's sides. Running into it throws the player just over the trigger's top and inward. Height above is not used.")] private bool side;
        [SerializeField, Tooltip("Side bounce: how far over the top it throws, in the player's own units.")] private float clearance = 2f;
        [SerializeField, Range(0.1f, 1f), Tooltip("Side bounce: where it lands, as a share of the way from where the player stands to the middle.")] private float landInward = 0.6f;
        [SerializeField, Tooltip("Side bounce: the player must be moving toward the middle at least this fast (own units per second), so standing beside a pillow is safe.")] private float minApproach = 1f;

        private float _readyAt, _squashUntil;
        private Vector3 _baseScale, _shown;   // the visual's own size, and the squashed size last written over it
        private bool _squashing;
        private static FirstPersonSettings _firstPerson;
        private Collider _trigger;

        private void Reset() { GetComponent<Collider>().isTrigger = true; }
        private void Awake() { if (visual == null) visual = transform; _trigger = GetComponent<Collider>(); }

        private void OnTriggerEnter(Collider other) { Try(other); }
        private void OnTriggerStay(Collider other) { Try(other); }

        private void Try(Collider other)
        {
            if (Time.time < _readyAt) return;
            Shifter shifter = PlayerTouch.Find(other, out FirstPersonBody body);
            if (shifter == null || shifter.Shifting) return;
            if (!anySize && shifter.Size != requiredSize) return;
            PlayerMotor motor = body == null ? shifter.GetComponent<PlayerMotor>() : null;
            if (body == null && motor == null) return;
            // only on the way down (or standing): rising through a pad, from a jump or its own throw, is not landing on it
            float rising = body != null ? body.Velocity.y / body.Scale : motor.Velocity.y / motor.Scale;
            if (rising > 0.5f) return;
            if (side) { if (!SideThrow(motor, body)) return; }
            else if (body != null) { if (!Launch(body)) return; }
            else motor.Launch(height);
            _readyAt = Time.time + cooldown;
            if (visual != null && !side)
            {
                if (!_squashing) _baseScale = _shown = visual.localScale;   // its size now (Build Mode may have scaled it)
                _squashing = true;
                _squashUntil = Time.time + squashTime;
            }
            onLaunch?.Invoke();
        }

        /// <summary>The same throw for the first-person body, under its own gravity.</summary>
        private bool Launch(FirstPersonBody body)
        {
            float g = FirstPersonGravity(body);
            if (g <= 0f) return false;
            float up = Mathf.Sqrt(2f * g * Mathf.Max(0.01f, height) * body.Scale);
            body.AddVelocity(Vector3.up * (up - body.Velocity.y));
            return true;
        }

        /// <summary>The first-person body's gravity at its size (its settings, read once: the body keeps them to itself).</summary>
        private static float FirstPersonGravity(FirstPersonBody body)
        {
            if (_firstPerson == null)
            {
                var loaded = Resources.FindObjectsOfTypeAll<FirstPersonSettings>();
                if (loaded.Length > 0) _firstPerson = loaded[0];
            }
            return _firstPerson != null ? _firstPerson.gravity * body.Scale : 0f;
        }

        /// <summary>Side mode: up to <see cref="clearance"/> over the trigger's top, and toward its middle just fast enough
        /// to come down <see cref="landInward"/> of the way in. False (nothing happens) when the player is already level
        /// with the top or is not moving toward the middle.</summary>
        private bool SideThrow(PlayerMotor motor, FirstPersonBody body)
        {
            if (_trigger == null) return false;
            Bounds box = _trigger.bounds;   // the pillow's footprint, floor to top
            Vector3 feet = body != null ? body.transform.position : motor.transform.position;
            float scale = body != null ? body.Scale : motor.Scale;
            float rise = box.max.y - feet.y;
            if (rise < 0.5f * scale) return false;   // already up there: the top pad's job

            Vector3 toMiddle = new Vector3(box.center.x - feet.x, 0f, box.center.z - feet.z);
            float across = toMiddle.magnitude;
            if (across < 1e-3f) return false;
            Vector3 dir = toMiddle / across;
            Vector3 velocity = body != null ? body.Velocity : motor.Velocity;
            float have = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), dir);
            if (have < minApproach * scale) return false;   // standing beside it, or walking along it

            float g = body != null ? FirstPersonGravity(body) : motor.Gravity;
            if (g <= 0f) return false;
            float over = Mathf.Max(0.1f, clearance) * scale;
            float up = Mathf.Sqrt(2f * g * (rise + over));
            float airTime = up / g + Mathf.Sqrt(2f * over / g);   // up to the top of the arc, then down to the pillow's top
            float need = across * landInward / airTime;
            Vector3 inward = dir * (need - have);
            if (body != null) body.AddVelocity(Vector3.up * (up - body.Velocity.y) + inward);
            else
            {
                motor.Launch((rise + over) / scale);
                motor.Knock(inward);
            }
            return true;
        }

        private void Update()
        {
            if (!_squashing) return;
            if (visual == null) { _squashing = false; return; }
            float k = Mathf.Clamp01((_squashUntil - Time.time) / Mathf.Max(0.01f, squashTime));   // 1 right after the bounce, 0 after squashTime
            if (k <= 0f) { visual.localScale = _baseScale; _squashing = false; return; }
            float s = Mathf.Lerp(1f, squash, Mathf.Sin(k * Mathf.PI));
            _shown = new Vector3(_baseScale.x * (2f - s), _baseScale.y * s, _baseScale.z * (2f - s));
            visual.localScale = _shown;
        }

        /// <summary>Build Mode moved or scaled the pad or its pillow: the size it has now is its own size.</summary>
        public void OnBuildMoved()
        {
            if (visual == null) return;
            if (_squashing)
            {
                // mid-bounce: take the squash back out of whatever size Build Mode left, and stop there
                Vector3 now = visual.localScale;
                visual.localScale = new Vector3(Unsquash(now.x, _baseScale.x, _shown.x), Unsquash(now.y, _baseScale.y, _shown.y), Unsquash(now.z, _baseScale.z, _shown.z));
                _squashing = false;
            }
            _baseScale = visual.localScale;
        }

        private static float Unsquash(float now, float own, float shown) => Mathf.Abs(shown) > 1e-6f ? now * own / shown : now;
    }
}
