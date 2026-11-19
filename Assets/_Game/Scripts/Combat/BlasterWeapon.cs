using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A toy blaster: fires pooled Projectiles at a set interval (tap or hold if automatic), several pellets with spread for
    /// a shotgun, aimed where the camera's centre ray lands (so third-person shots go to the crosshair; the holder's arm
    /// points the barrel at the same spot).
    /// Shots leave from the muzzle unless the muzzle has poked through a wall, in which case they leave from the chest and
    /// hit that wall. Each shot kicks the gun back (and the view, in first person): how hard is this gun's Recoil, and
    /// what a kick of 1 is (degrees, distance, how it settles) is the same for every gun, in WeaponHold (the test
    /// room's rule). Shot Speed multiplies the ammo's own speed. A shot is heard (the loaded ammo's sound, else the
    /// shot's own, else this gun's Fire Clip) and seen leaving (a puff of bits at the barrel: WeaponAmmo).
    /// Each shot spends a round from the gun's Magazine; empty, the trigger only clicks and the holder is told (it
    /// starts the reload). A blaster with Magazine Size 0 has no magazine and never runs dry.
    /// </summary>
    public sealed class BlasterWeapon : Weapon
    {
        [Header("Fire")]
        [SerializeField] private Projectile projectile;
        [SerializeField, Tooltip("Where shots leave. The weapon's root if empty.")] private Transform muzzle;
        [SerializeField, Tooltip("Seconds between shots (the test room counts shots a second: 0.2 is its pistol's 5).")] private float interval = 0.2f;
        [SerializeField, Tooltip("Keep firing while Attack is held.")] private bool automatic;
        [SerializeField, Min(1), Tooltip("Shots per trigger pull. A shotgun has several; a pull still spends one round.")] private int pellets = 1;
        [SerializeField, Tooltip("How far a pellet can stray from the aim, degrees.")] private float spread = 1.5f;
        [SerializeField, Tooltip("Health each pellet takes. 15 here is 1 in the test room.")] private float damage = 15f;
        [SerializeField, Tooltip("How hard each pellet shoves. A loose thing of mass 1 leaves at this speed (the test room's push); a player or a soldier is shoved at this speed, units per second at scale 1. A bigger shooter's shot shoves a loose thing harder by the square root of its size.")] private float knockback = 3f;
        [SerializeField, Min(0.05f), Tooltip("Multiplies the ammo's own speed (the test room: pistol 1, shotgun 0.9, rifle 1.2, sniper 4). Higher: a faster, flatter shot.")] private float shotSpeed = 1f;
        [SerializeField, Tooltip("How far the aim ray looks for a target, units at scale 1.")] private float range = 120f;
        [SerializeField] private LayerMask aimMask = ~0;
        [Header("Recoil")]
        [SerializeField, Min(0f), Tooltip("How hard the gun kicks back when it fires. 1 is the pistol's kick (the test room: rifle 0.7, shotgun 2.2, sniper 2.6). What a kick of 1 is (degrees up, distance back, how fast it settles) is in Resources/WeaponHold > The kick of a shot.")] private float recoil = 1f;
        [SerializeField, Tooltip("First person: view punch per shot for a gun of recoil 1 (pitch up, yaw jitter), degrees. Multiplied by Recoil.")] private Vector2 viewPunch = new Vector2(1.2f, 0.4f);
        [SerializeField] private AudioClip fireClip;
        [Header("Ammo")]
        [SerializeField, Tooltip("This gun's magazine: its size, the reserve it starts with, the most it can carry, and how long a reload takes.")] private Magazine magazine = new Magazine();

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private float _nextFire, _kick;
        private WeaponHolder _beltOf;   // whose AmmoBelt was looked up
        private AmmoBelt _belt;         // the holder's choice of ammo, if it carries one
        private MagazinePart _part;     // the magazine on the model, found once
        private WeaponHoldSettings _feel;   // what a kick of 1 is
        private WeaponAmmoSettings _shared; // the puff at the barrel

        /// <summary>The rounds in this gun and in reserve for it. They stay with the gun when it is put away or dropped.</summary>
        public Magazine Magazine { get { magazine.Fill(); return magazine; } }   // filled the first time anyone looks
        /// <summary>The magazine on this gun's model, for the reload.</summary>
        public MagazinePart MagazinePart => _part ??= new MagazinePart(transform);

        private WeaponHoldSettings Feel => _feel != null ? _feel : (_feel = WeaponHoldSettings.Load());
        /// <summary>How hard this gun kicks: 1 is the pistol's kick.</summary>
        public float Recoil => recoil;
        /// <summary>How much of the last shot's kick is left: 1 as the shot leaves, settling to 0.</summary>
        public float KickLeft => _kick;
        /// <summary>The share of normal speed its shots leave at.</summary>
        public float ShotSpeed => shotSpeed;

        // The kick: the barrel flips up and the gun jumps back along its barrel, then settles (the test room's rule).
        public override Quaternion PoseRotation => Quaternion.Euler(-Feel.recoilUpDegrees * recoil * _kick, 0f, 0f);
        public override Vector3 PosePosition => Vector3.back * (Feel.recoilBack * recoil * _kick);

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
                if (HitShapes.BodyOnly(_hits[i].collider)) continue;   // the shot will pass a figure's walking capsule: aim at what is drawn
                if (_hits[i].distance < nearest) { nearest = _hits[i].distance; target = _hits[i].point; }
            }
            return target;
        }

        public override void TriggerPressed() => TryFire(true);
        public override void TriggerHeld() { if (automatic) TryFire(false); }
        public override void Cancel() { _kick = 0f; if (Holder != null) Holder.CancelReload(this); }   // put away, swapped or dropped: the reload stops

        private void Update()
        {
            _kick *= Mathf.Exp(-Feel.recoilSettle * Time.deltaTime);
        }

        /// <summary>One shot, if the gun may fire now. <paramref name="pressed"/>: a fresh pull of the trigger (not a
        /// held automatic), which is what makes an empty gun click.</summary>
        private bool TryFire(bool pressed)
        {
            if (Holder == null || Time.time < _nextFire || Holder.Reloading) return false;
            if (Magazine.Empty)
            {
                // Empty: the trigger rests a moment, and the holder clicks and starts the reload.
                _nextFire = Time.time + Holder.Empty(this, pressed);
                return false;
            }
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
                    if (HitShapes.BodyOnly(_hits[i].collider)) continue;   // a shot is not stopped by a figure's walking capsule
                    from = chest;
                    break;
                }
            }

            Vector3 dir = target - from;
            if (dir.sqrMagnitude < 1e-6f) dir = look;
            dir.Normalize();
            for (int p = 0; p < pellets; p++)
                Projectile.Fire(shot, from, Spread(dir), damage, knockback, scale, Holder.transform, Holder.FirstPersonBodyRoot, shotSpeed);
            Magazine.Spend();   // one round a shot, however many pellets

            _kick = 1f;
            Holder.Punch(new Vector2(-viewPunch.x, Random.Range(-viewPunch.y, viewPunch.y)) * recoil);
            if (_shared == null) _shared = WeaponAmmoSettings.Load();
            // The sound: the loaded ammo's (the AmmoBelt: paint), else the shot's own (the foam dart), else this gun's.
            AudioClip ammoClip = _belt != null ? _belt.FireClip() : null;
            AudioClip own = ammoClip == null ? shot.FireClip() : null;
            if (own != null) Holder.PlayClip(own, Mathf.Pow(Mathf.Max(1f, _shared.firePitchSpread), Random.Range(-1f, 1f)));   // a little higher or lower each shot, as the test room's
            else Holder.PlayClip(ammoClip != null ? ammoClip : fireClip, Random.Range(1.05f, 1.25f));
            // A puff at the barrel, so each shot can be seen leaving.
            if (_shared.puffAmount > 0)
                PaintSplats.InScene().Puff(from + dir * (0.03f * scale), dir, _shared.puffColour, _shared.puffAmount, _shared.puffSpeed * scale, _shared.puffSize * scale, 0f, _shared.puffSeconds);
            Holder.AmmoChanged();
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
