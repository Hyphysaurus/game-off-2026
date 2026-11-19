using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A training dummy on a spring: every hit rocks it away from the blow and it wobbles back upright. Knocked out (health
    /// gone), it topples flat with a burst of bits and a thud (KnockDown), lies there a moment and pops back up at full
    /// health. The Workshop reset stands it up at once.
    /// The visual is a child pivot (at the base) so the root's collider and position never move. Lying flat it has no
    /// collider: the upright capsule would stop the player where nothing is drawn. Shots never land on that capsule
    /// when the target has hit shapes (HitShapes: the drawn mesh itself, riding the pivot); those are off too while it
    /// lies down.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public sealed class PracticeTarget : MonoBehaviour, IResettable
    {
        [SerializeField, Tooltip("The part that rocks, pivoting at its base. The first child if empty.")] private Transform pivot;
        [SerializeField, Tooltip("Degrees per second of rock given per unit of shove (shove is in units per second at the target's scale).")] private float kick = 9f;
        [SerializeField, Tooltip("Spring pulling it upright: higher = faster wobble.")] private float stiffness = 90f;
        [SerializeField, Tooltip("How quickly the wobble dies.")] private float damping = 5f;
        [SerializeField, Tooltip("Furthest it leans while standing, degrees.")] private float maxLean = 40f;
        [SerializeField, Tooltip("Seconds it lies flat before popping back up.")] private float downTime = 2.5f;
        [SerializeField] private AudioClip popClip;

        private Damageable _health;
        private Collider _collider;
        private Quaternion _rest;
        private Vector2 _lean, _leanVelocity;   // tilt about world X and Z, degrees
        private Vector2 _fallDir;
        private float _downAt = -1f, _upAt;
        private AudioSource _audio;

        private void Awake()
        {
            _health = GetComponent<Damageable>();
            _collider = GetComponent<Collider>();
            if (pivot == null && transform.childCount > 0) pivot = transform.GetChild(0);
            if (pivot != null) _rest = pivot.localRotation;
            if (popClip != null) { _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false; _audio.spatialBlend = 1f; }
        }

        private void OnEnable() { _health.Damaged += OnDamaged; _health.Died += OnDied; }
        private void OnDisable() { _health.Damaged -= OnDamaged; _health.Died -= OnDied; }

        private void OnDamaged(DamageInfo hit)
        {
            Vector3 push = hit.Knock.sqrMagnitude > 1e-6f ? hit.Knock : transform.position - (hit.Source != null ? hit.Source.position : hit.Point);
            push.y = 0f;
            float scale = Mathf.Max(0.001f, transform.lossyScale.y);
            float strength = Mathf.Max(2f, hit.Knock.magnitude / scale);
            Vector3 dir = push.sqrMagnitude > 1e-6f ? push.normalized : transform.forward;
            // Leaning toward +X is a turn about -Z; toward +Z is a turn about +X.
            _leanVelocity += new Vector2(dir.z, -dir.x) * (kick * strength);
        }

        private void OnDied(DamageInfo hit)
        {
            Vector3 dir = hit.Knock; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = transform.position - (hit.Source != null ? hit.Source.position : hit.Point);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
            dir.Normalize();
            _fallDir = new Vector2(dir.z, -dir.x);
            _downAt = Time.time;
            _upAt = Time.time + downTime;
            SetSolid(false);
            KnockDown.Show(transform, hit, pivot, false);   // a burst of bits, a bigger squash and the thud
        }

        public void ResetState()
        {
            _health.ResetState();
            _lean = _leanVelocity = Vector2.zero;
            _downAt = -1f;
            SetSolid(true);
            if (pivot != null) pivot.localRotation = _rest;
        }

        private void SetSolid(bool on) { if (_collider != null) _collider.enabled = on; }

        private void Update()
        {
            if (pivot == null) return;
            float dt = Time.deltaTime;
            if (_downAt >= 0f)
            {
                // Topple to flat with a little bounce, then pop back.
                float t = Mathf.Clamp01((Time.time - _downAt) / 0.35f);
                float bounce = 1f - Mathf.Abs(Mathf.Cos(t * Mathf.PI * 1.5f)) * (1f - t);
                _lean = Vector2.Lerp(_lean, _fallDir * 88f, bounce);
                if (Time.time >= _upAt)
                {
                    _health.ResetState();
                    _downAt = -1f;
                    SetSolid(true);
                    _leanVelocity = -_fallDir * 260f;   // springs up past upright and wobbles
                    if (_audio != null) _audio.PlayOneShot(popClip);
                }
            }
            else
            {
                Vector2 accel = -stiffness * _lean - damping * _leanVelocity;
                _leanVelocity += accel * dt;
                _lean += _leanVelocity * dt;
                if (_lean.magnitude > maxLean) { _lean = _lean.normalized * maxLean; _leanVelocity *= 0.5f; }
            }
            pivot.rotation = Quaternion.Euler(_lean.x, 0f, _lean.y) * transform.rotation * _rest;
        }
    }
}
