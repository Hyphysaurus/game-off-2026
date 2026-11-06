using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A swept toy shot (foam dart, pellet): sphere-casts its path each frame so it never tunnels, hurts what it hits
    /// (Damageable) or shoves it (Rigidbody, player), and goes back to a pool instead of being destroyed. Everything about
    /// it (speed, radius, drop, shove) is multiplied by the shooter's scale, so a kid's shots are kid-sized.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
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
        /// are never hit: the shooter's body, and the first-person body or car it is in.</summary>
        public static Projectile Fire(Projectile prefab, Vector3 position, Vector3 direction, float damage, float knock, float scale,
            Transform owner, Transform ownerAlt = null)
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
            shot.Launch(position, direction, damage, knock, scale, owner, ownerAlt);
            return shot;
        }

        /// <summary>Puts every flying shot back in its pool (the Workshop reset).</summary>
        public static void ClearAll()
        {
            for (int i = Live.Count - 1; i >= 0; i--) if (Live[i] != null) Live[i].Recycle();
            Live.Clear();
        }

        private void Launch(Vector3 position, Vector3 direction, float damage, float knock, float scale, Transform owner, Transform ownerAlt)
        {
            _scale = Mathf.Max(0.001f, scale);
            _velocity = direction.normalized * (speed * _scale);
            _damage = damage; _knock = knock; _owner = owner; _ownerAlt = ownerAlt;
            _expires = Time.time + lifetime;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_velocity));
            transform.localScale = _baseScale * _scale;
            gameObject.SetActive(true);
            Live.Add(this);
        }

        private void Recycle()
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
                Vector3 shove = (dir + Vector3.up * 0.3f) * (_knock * _scale);
                var health = Damageable.Find(hit.collider);
                if (health != null) health.Hit(new DamageInfo(_damage, shove, point, _owner));
                else Knockback.Apply(hit.collider.gameObject, shove, point);
                Recycle();
                return;
            }
            transform.position += step;
            if (faceTravel) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
