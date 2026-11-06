using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A toy sword / knife / foam finger. A swing has a windup (readable anticipation), an active window during which a
    /// sphere in front of the holder hits each target at most once (knockback plus a little knockup, never through walls),
    /// and a recovery. A connecting hit freezes the swing for a moment (hit-stop) so it lands with weight. Reach and shove
    /// scale with the holder. Loose props are batted too.
    /// </summary>
    public sealed class MeleeWeapon : Weapon
    {
        [Header("Swing (seconds)")]
        [SerializeField] private float windup = 0.12f;
        [SerializeField] private float active = 0.12f;
        [SerializeField] private float recovery = 0.22f;
        [Header("Hit (units at scale 1)")]
        [SerializeField] private float damage = 30f;
        [SerializeField, Tooltip("Centre of the hit sphere, in front of the chest.")] private float reach = 0.9f;
        [SerializeField] private float hitRadius = 0.8f;
        [SerializeField, Tooltip("Horizontal shove, units per second at scale 1.")] private float knockback = 8f;
        [SerializeField, Tooltip("Upward shove, units per second at scale 1.")] private float knockup = 4f;
        [SerializeField, Tooltip("Swing freeze on a connecting hit, seconds. 0 = none.")] private float hitStop = 0.06f;
        [SerializeField, Range(-1f, 1f), Tooltip("How far round to the side a target may be and still be hit (dot with facing).")] private float minFacing = 0.2f;
        [SerializeField] private LayerMask hitMask = ~0;
        [Header("Look")]
        [SerializeField, Tooltip("Blade angle at the top of the windup (pitch, yaw, roll) in the hold frame.")] private Vector3 windupPose = new Vector3(-70f, 40f, 0f);
        [SerializeField, Tooltip("Blade angle at the end of the sweep.")] private Vector3 followPose = new Vector3(60f, -50f, 0f);
        [SerializeField] private AudioClip swingClip, hitClip;

        private readonly Collider[] _overlap = new Collider[32];
        private readonly HashSet<Object> _struck = new HashSet<Object>();
        private readonly RaycastHit[] _los = new RaycastHit[8];
        private float _t = -1f;     // seconds into the swing; < 0 = idle
        private float _frozenUntil;

        private float Total => windup + active + recovery;
        public override bool Busy => _t >= 0f;

        public override void TriggerPressed()
        {
            if (_t >= 0f || Holder == null) return;
            _t = 0f;
            _struck.Clear();
            Holder.PlayClip(swingClip, 1.15f);
        }

        public override void Cancel() { _t = -1f; _struck.Clear(); }

        public override Quaternion PoseRotation
        {
            get
            {
                if (_t < 0f) return Quaternion.identity;
                Vector3 e;
                if (_t < windup) e = Vector3.Lerp(Vector3.zero, windupPose, Smooth(_t / windup));
                else if (_t < windup + active) e = Vector3.Lerp(windupPose, followPose, Smooth((_t - windup) / Mathf.Max(0.001f, active)));
                else e = Vector3.Lerp(followPose, Vector3.zero, Smooth((_t - windup - active) / Mathf.Max(0.001f, recovery)));
                return Quaternion.Euler(e);
            }
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        private void Update()
        {
            if (_t < 0f || Holder == null) return;
            if (Time.time < _frozenUntil) return;   // hit-stop
            _t += Time.deltaTime;
            if (_t >= windup && _t <= windup + active) Strike();
            if (_t >= Total) _t = -1f;
        }

        private void Strike()
        {
            float scale = Scale;
            Vector3 origin = Holder.AttackOrigin;
            Vector3 forward = Holder.AttackForward;
            Vector3 centre = origin + forward * (reach * scale);
            int count = Physics.OverlapSphereNonAlloc(centre, hitRadius * scale, _overlap, hitMask, QueryTriggerInteraction.Ignore);
            bool connected = false;
            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (flat.sqrMagnitude < 0.5f) flat = forward;
            for (int i = 0; i < count; i++)
            {
                var col = _overlap[i];
                if (Holder.IsSelf(col.transform) || col.transform.IsChildOf(transform)) continue;
                var health = Damageable.Find(col);
                var body = col.attachedRigidbody;
                Object key = health != null ? health : (Object)body;
                if (key == null || !_struck.Add(key)) continue;
                Vector3 at = col.ClosestPoint(origin);
                Vector3 to = at - origin;
                if (to.sqrMagnitude > 1e-6f && Vector3.Dot(to.normalized, forward) < minFacing) { _struck.Remove(key); continue; }
                if (Blocked(origin, at, col)) { _struck.Remove(key); continue; }
                Vector3 shove = flat * (knockback * scale) + Vector3.up * (knockup * scale);
                if (health != null) connected |= health.Hit(new DamageInfo(damage, shove, at, Holder.transform));
                else if (Knockback.Apply(col.gameObject, shove, at)) connected = true;
            }
            if (connected)
            {
                if (hitStop > 0f) _frozenUntil = Time.time + hitStop;
                Holder.PlayClip(hitClip, 1f);
                Holder.Punch(new Vector2(-2f, 1.5f));
            }
        }

        /// <summary>True if a wall stands between the swing and the target.</summary>
        private bool Blocked(Vector3 from, Vector3 to, Collider target)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-4f) return false;
            int n = Physics.RaycastNonAlloc(from, d / len, _los, len, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = _los[i].transform;
                if (Holder.IsSelf(t) || t.IsChildOf(transform) || _los[i].collider == target) continue;
                if (target.attachedRigidbody != null && _los[i].rigidbody == target.attachedRigidbody) continue;
                if (Damageable.Find(_los[i].collider) == Damageable.Find(target) && Damageable.Find(target) != null) continue;
                return true;
            }
            return false;
        }
    }
}
