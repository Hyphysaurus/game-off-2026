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
    /// Each kind of ammo carries its own feedback, as in the test room: the sounds of the shot and of the landing, and
    /// the small bits thrown off where it lands. Whoever fired it is told when it lands on something with health
    /// (GameEvents.ShotLanded: the hit marker).
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
        [Header("Feedback (each kind of ammo carries its own, as in the test room)")]
        [SerializeField, Tooltip("A gun firing this ammo plays one of these. Licensed clips under _Imports: with none, or with the clips gone, the gun's own Fire Clip plays, or nothing.")] private AudioClip[] fireClips = System.Array.Empty<AudioClip>();
        [SerializeField, Tooltip("One of these plays where the shot lands. None: it lands silently (paint plays its own).")] private AudioClip[] hitClips = System.Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f), Tooltip("How loud the landing is.")] private float hitVolume = 1f;
        [SerializeField, Min(0), Tooltip("Small bits thrown off the surface where the shot lands (the test room's dart: 8). 0 = none (paint throws its own).")] private int burstAmount;
        [SerializeField, Tooltip("The bits' colour.")] private Color burstColour = Color.white;
        [SerializeField, Tooltip("How fast the fastest bit flies, units per second at scale 1 (the test room's dart: 2.5).")] private float burstSpeed = 3f;
        [SerializeField, Tooltip("A bit's radius, units at scale 1 (the test room: 0.035).")] private float burstSize = 0.035f;
        [SerializeField, Tooltip("How fast the bits fall, units per second squared at scale 1 (the test room: 9).")] private float burstGravity = 9f;
        [SerializeField, Tooltip("Seconds a bit lasts (the test room: 0.45).")] private float burstSeconds = 0.45f;

        private static readonly Dictionary<Projectile, Stack<Projectile>> Pools = new Dictionary<Projectile, Stack<Projectile>>();
        private static readonly List<Projectile> Live = new List<Projectile>();
        private static Transform _poolRoot;
        private static int _hitSoundFrame = -1;   // the frame a landing last sounded: pellets landing together make one sound

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

        /// <summary>One of this ammo's firing sounds, for the gun that fires it; null when it has none, or the clip is
        /// not in the project.</summary>
        public AudioClip FireClip() => Pick(fireClips);

        private static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            return clip != null ? clip : null;   // a clip that is gone is not null to C#: hand back a real null
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

        /// <summary>What a landing shows and sounds like: a few bits thrown off the surface, and a sound there.</summary>
        private void Landed(in RaycastHit hit, Vector3 point, Vector3 dir)
        {
            AudioClip clip = Pick(hitClips);
            if (burstAmount <= 0 && clip == null) return;
            PaintSplats keeper = PaintSplats.InScene();   // the scene's paint keeper flies every puff of bits and plays the placed sounds
            if (burstAmount > 0)
            {
                Vector3 off = hit.distance > 0f && Vector3.Dot(hit.normal, dir) < 0f ? hit.normal : -dir;   // the side the shot came from
                keeper.Puff(point + off * (0.03f * _scale), off, burstColour, burstAmount, burstSpeed * _scale, burstSize * _scale, burstGravity * _scale, burstSeconds);
            }
            if (clip != null && _hitSoundFrame != Time.frameCount)
            {
                _hitSoundFrame = Time.frameCount;
                keeper.Play(clip, point, hitVolume);
            }
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
                bool counted = false;
                if (health != null) counted = health.Hit(new DamageInfo(_damage, shove, point, _owner, loose));
                else Knockback.Apply(hit.collider.gameObject, shove, point, loose);
                Landed(hit, point, dir);
                // Whoever fired it is told that it landed on something with health, and whether that finished it.
                if (counted && _owner != null && _owner.TryGetComponent(out WeaponHolder holder)) GameEvents.RaiseShotLanded(holder, health.Dead);
                OnLanded(hit, point);
                Recycle();
                return;
            }
            transform.position += step;
            if (faceTravel) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
