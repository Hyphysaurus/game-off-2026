using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A swept toy shot (foam dart, pellet): sphere-casts its path each frame so it never tunnels, hurts what it hits
    /// (Damageable) or shoves it (Rigidbody, player), and goes back to a pool instead of being destroyed. Its speed,
    /// radius and drop are multiplied by the shooter's scale, so a kid's shots are kid-sized. So is its shove on a
    /// player or a soldier (a speed in the shooter's own units); a loose thing takes only the square root of the
    /// shooter's size (the test room's rule: a crate slides as far as the square of the shove, so that carries it
    /// about as many kid sizes as a toy's shot carries it toy sizes; all of it threw crates across the room).
    /// The weapon that fires it can make it faster or slower (its Shot Speed).
    /// Another kind of ammo (the paint ball) extends this and changes only what happens at a hit: it can glance off
    /// before the hit counts (Deflects) and leave something behind after it (OnLanded).
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [SerializeField, Tooltip("Units per second at scale 1.")] private float speed = 40f;
        [SerializeField, Tooltip("Hit radius, units at scale 1.")] private float radius = 0.06f;
        [SerializeField, Tooltip("Seconds before it gives up.")] private float lifetime = 2.5f;
        [SerializeField, Tooltip("Downward pull, units per second squared at scale 1. 0 = flies straight.")] private float drop = 2f;
        [SerializeField, Tooltip("Turn the visual to face the way it flies (darts). Off for round pellets.")] private bool faceTravel = true;
        [SerializeField] private LayerMask hitMask = ~0;

        private static readonly Dictionary<Projectile, Stack<Projectile>> Pools = new Dictionary<Projectile, Stack<Projectile>>();
        private static readonly List<Projectile> Live = new List<Projectile>();
        private static Transform _poolRoot;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Projectile _prefab;
        private Transform _owner, _ownerAlt;
        private Vector3 _velocity;
        private float _expires, _damage, _knock, _scale;
        private Vector3 _baseScale;

        /// <summary>Fires one shot from the pool. <paramref name="owner"/> and <paramref name="ownerAlt"/> (both optional)
        /// are never hit: the shooter's body, and the first-person body or car it is in. <paramref name="speedScale"/>
        /// multiplies the shot's own speed (a weapon's Shot Speed).</summary>
        public static Projectile Fire(Projectile prefab, Vector3 position, Vector3 direction, float damage, float knock, float scale,
            Transform owner, Transform ownerAlt = null, float speedScale = 1f)
        {
            if (prefab == null) return null;
            if (!Pools.TryGetValue(prefab, out var pool)) Pools[prefab] = pool = new Stack<Projectile>();
            Projectile shot = null;
            while (shot == null && pool.Count > 0) shot = pool.Pop();   // entries die with their scene
            if (shot == null)
            {
                if (_poolRoot == null) _poolRoot = new GameObject("Projectiles").transform;
                shot = Instantiate(prefab, _poolRoot);
                shot._prefab = prefab;
                shot._baseScale = shot.transform.localScale;
            }
            shot.Launch(position, direction, damage, knock, scale, owner, ownerAlt, speedScale);
            return shot;
        }

        /// <summary>Puts every flying shot back in its pool (the Workshop reset).</summary>
        public static void ClearAll()
        {
            for (int i = Live.Count - 1; i >= 0; i--) if (Live[i] != null) Live[i].Recycle();
            Live.Clear();
        }

        private void Launch(Vector3 position, Vector3 direction, float damage, float knock, float scale, Transform owner, Transform ownerAlt, float speedScale)
        {
            _scale = Mathf.Max(0.001f, scale);
            _velocity = direction.normalized * (speed * Mathf.Max(0.01f, speedScale) * _scale);
            _damage = damage; _knock = knock; _owner = owner; _ownerAlt = ownerAlt;
            _expires = Time.time + lifetime;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_velocity));
            transform.localScale = _baseScale * _scale;
            gameObject.SetActive(true);
            Live.Add(this);
            OnLaunched();
        }

        // ---------- for other kinds of ammo ----------

        /// <summary>The shot's velocity, world units per second.</summary>
        protected Vector3 Velocity { get => _velocity; set => _velocity = value; }
        /// <summary>The shooter's scale this shot was fired at.</summary>
        protected float ShotScale => _scale;
        /// <summary>The shot's hit radius, world units.</summary>
        protected float HitRadius => radius * _scale;
        /// <summary>Who fired it (never hit), and the first-person body or car it was in.</summary>
        protected Transform Owner => _owner;
        protected Transform OwnerAlt => _ownerAlt;
        /// <summary>Multiplies this shot's damage and shove (an ammo's own share of the weapon's).</summary>
        protected void ScaleHit(float damage, float knock) { _damage *= damage; _knock *= knock; }
        /// <summary>The shot has just been fired: position, velocity and scale are set.</summary>
        protected virtual void OnLaunched() { }
        /// <summary>Each frame before the shot moves, after its drop: an ammo may change Velocity (air drag).</summary>
        protected virtual void BeforeMove(float dt) { }
        /// <summary>Asked just before a hit counts. Return true if the shot glanced off (after setting its own Velocity and
        /// position for the way on) or is simply spent (after calling Recycle): then nothing is hit.</summary>
        protected virtual bool Deflects(in RaycastHit hit, Vector3 point) => false;
        /// <summary>The hit has counted (damage and shove are done); the shot goes back to its pool right after.</summary>
        protected virtual void OnLanded(in RaycastHit hit, Vector3 point) { }

        protected void Recycle()
        {
            gameObject.SetActive(false);
            Live.Remove(this);
            if (_prefab != null && Pools.TryGetValue(_prefab, out var pool)) pool.Push(this);
            else Destroy(gameObject);
        }

        private bool IsOwner(Transform t) =>
            (_owner != null && t.IsChildOf(_owner)) || (_ownerAlt != null && t.IsChildOf(_ownerAlt));

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Time.time >= _expires) { Recycle(); return; }
            _velocity += Vector3.down * (drop * _scale * dt);
            BeforeMove(dt);
            Vector3 step = _velocity * dt;
            float distance = step.magnitude;
            if (distance < 1e-6f) return;
            Vector3 dir = step / distance;

            int count = Physics.SphereCastNonAlloc(transform.position, radius * _scale, dir, _hits, distance, hitMask, QueryTriggerInteraction.Ignore);
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (IsOwner(_hits[i].transform)) continue;
                if (best < 0 || _hits[i].distance < _hits[best].distance) best = i;
            }
            if (best >= 0)
            {
                var hit = _hits[best];
                Vector3 point = hit.distance > 0f ? hit.point : transform.position;
                if (Deflects(hit, point)) return;
                // The shove, sized to the shooter; a loose thing takes the square root of that size, not all of it.
                Vector3 shove = (dir + Vector3.up * 0.3f) * (_knock * _scale);
                float loose = 1f / Mathf.Sqrt(_scale);
                var health = Damageable.Find(hit.collider);
                if (health != null) health.Hit(new DamageInfo(_damage, shove, point, _owner, loose));
                else Knockback.Apply(hit.collider.gameObject, shove, point, loose);
                OnLanded(hit, point);
                Recycle();
                return;
            }
            transform.position += step;
            if (faceTravel) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
