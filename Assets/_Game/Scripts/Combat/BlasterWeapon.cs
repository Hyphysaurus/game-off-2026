using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A toy blaster: fires pooled Projectiles at a set interval (tap or hold if automatic), several pellets with spread for
    /// a shotgun, aimed where the camera's centre ray lands (so third-person shots go to the crosshair; the holder's arm
    /// points the barrel at the same spot).
    /// Shots leave from the muzzle unless the muzzle has poked through a wall, in which case they leave from the chest and
    /// hit that wall. Each shot kicks the gun back (and the view, in first person).
    /// </summary>
    public sealed class BlasterWeapon : Weapon
    {
        [Header("Fire")]
        [SerializeField] private Projectile projectile;
        [SerializeField, Tooltip("Where shots leave. The weapon's root if empty.")] private Transform muzzle;
        [SerializeField, Tooltip("Seconds between shots.")] private float interval = 0.2f;
        [SerializeField, Tooltip("Keep firing while Attack is held.")] private bool automatic;
        [SerializeField, Min(1)] private int pellets = 1;
        [SerializeField, Tooltip("Cone half-angle, degrees.")] private float spread = 1.5f;
        [SerializeField] private float damage = 15f;
        [SerializeField, Tooltip("Shove per shot, units per second at scale 1.")] private float knockback = 3f;
        [SerializeField, Tooltip("How far the aim ray looks for a target, units at scale 1.")] private float range = 120f;
        [SerializeField] private LayerMask aimMask = ~0;
        [Header("Recoil")]
        [SerializeField, Tooltip("Muzzle climb per shot, degrees.")] private float kickAngle = 10f;
        [SerializeField, Tooltip("Slide back per shot, units at scale 1.")] private float kickBack = 0.06f;
        [SerializeField, Tooltip("How fast the kick settles, per second.")] private float recovery = 14f;
        [SerializeField, Tooltip("First person: view punch per shot (pitch up, yaw jitter), degrees.")] private Vector2 viewPunch = new Vector2(1.2f, 0.4f);
        [SerializeField] private AudioClip fireClip;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private float _nextFire, _kick;
        private WeaponHolder _beltOf;   // whose AmmoBelt was looked up
        private AmmoBelt _belt;         // the holder's choice of ammo, if it carries one

        public override Quaternion PoseRotation => Quaternion.Euler(-kickAngle * _kick, 0f, 0f);
        public override Vector3 PosePosition => Vector3.back * (kickBack * _kick);

        public override Vector3 MuzzleOffset => muzzle != null ? Quaternion.Inverse(transform.rotation) * (muzzle.position - transform.position) : Vector3.zero;

        /// <summary>Where the crosshair lands: the first thing on the camera's centre ray that is not us. In third person
        /// the ray starts level with the holder, so nothing between the camera and the player gets picked.</summary>
        public override Vector3 AimPoint()
        {
            if (Holder == null) return base.AimPoint();
            float reach = range * Scale;
            Vector3 eye = Holder.AimOrigin, look = Holder.AimForward;
            float skip = Holder.FirstPerson ? 0f : Mathf.Clamp(Vector3.Dot(Holder.AttackOrigin - eye, look), 0f, reach * 0.5f);
            Vector3 target = eye + look * reach;
            int n = Physics.RaycastNonAlloc(eye + look * skip, look, _hits, reach - skip, aimMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (Holder.IsSelf(_hits[i].transform) || _hits[i].transform.IsChildOf(transform)) continue;
                if (_hits[i].distance < nearest) { nearest = _hits[i].distance; target = _hits[i].point; }
            }
            return target;
        }

        public override void TriggerPressed() => TryFire();
        public override void TriggerHeld() { if (automatic) TryFire(); }
        public override void Cancel() { _kick = 0f; }

        private void Update()
        {
            _kick = Mathf.Lerp(_kick, 0f, 1f - Mathf.Exp(-recovery * Time.deltaTime));
        }

        private bool TryFire()
        {
            if (Holder == null || Time.time < _nextFire) return false;
            // What leaves the barrel: the holder's chosen ammo (AmmoBelt: darts, paint...), else this blaster's own.
            if (_beltOf != Holder) { _beltOf = Holder; Holder.TryGetComponent(out _belt); }
            Projectile shot = _belt != null ? _belt.Shot(projectile) : projectile;
            if (shot == null) return false;
            _nextFire = Time.time + interval;
            float scale = Scale;

            Vector3 look = Holder.AimForward;
            Vector3 target = AimPoint();

            // Leave from the muzzle, unless it is through a wall: then from the chest, into that wall.
            Vector3 from = muzzle != null ? muzzle.position : transform.position;
            Vector3 chest = Holder.AttackOrigin;
            Vector3 gap = from - chest;
            float gapLen = gap.magnitude;
            if (gapLen > 1e-4f)
            {
                int m = Physics.RaycastNonAlloc(chest, gap / gapLen, _hits, gapLen, aimMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < m; i++)
                {
                    if (Holder.IsSelf(_hits[i].transform) || _hits[i].transform.IsChildOf(transform)) continue;
                    from = chest;
                    break;
                }
            }

            Vector3 dir = target - from;
            if (dir.sqrMagnitude < 1e-6f) dir = look;
            dir.Normalize();
            for (int p = 0; p < pellets; p++)
                Projectile.Fire(shot, from, Spread(dir), damage, knockback, scale, Holder.transform, Holder.FirstPersonBodyRoot);

            _kick = 1f;
            Holder.Punch(new Vector2(-viewPunch.x, Random.Range(-viewPunch.y, viewPunch.y)));
            AudioClip ammoClip = _belt != null ? _belt.FireClip() : null;
            Holder.PlayClip(ammoClip != null ? ammoClip : fireClip, Random.Range(1.05f, 1.25f));
            return true;
        }

        private Vector3 Spread(Vector3 dir)
        {
            if (spread <= 0f) return dir;
            Vector3 axis = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.99f ? Vector3.right : Vector3.up).normalized;
            axis = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * axis;
            return Quaternion.AngleAxis(spread * Mathf.Sqrt(Random.value), axis) * dir;
        }
    }
}
