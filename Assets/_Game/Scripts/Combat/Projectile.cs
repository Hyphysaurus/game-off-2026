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
    /// the small bits thrown off where it lands, and a bright streak behind it in flight. Whoever fired it is told when it lands on something with health
    /// (GameEvents.ShotLanded: the hit marker, the sound at the shooter's ear).
    /// Ours, not the test room's: a landing on something with health can throw other bits than a landing on a wall
    /// (bigger, brighter, more of them) and a short ring of bits that widens from the spot; both are off unless the
    /// ammo's prefab sets them.
    /// Another kind of ammo extends this and changes only what happens at a hit: it can glance off before the hit
    /// counts (Deflects), leave something behind after it (OnLanded: the paint ball), or stay where it landed instead
    /// of going back to the pool (Stays: the foam dart).
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
        [SerializeField, Min(0), Tooltip("Ours: bits thrown instead of the ones above when the shot lands on something with health and the hit counts. 0 = the same bits as on a wall.")] private int targetBurstAmount;
        [SerializeField, Tooltip("Ours: those bits' colour.")] private Color targetBurstColour = new Color(1f, 0.86f, 0.35f);
        [SerializeField, Tooltip("Ours: how fast the fastest of them flies, units per second at scale 1.")] private float targetBurstSpeed = 4f;
        [SerializeField, Tooltip("Ours: the radius of one of them, units at scale 1.")] private float targetBurstSize = 0.05f;
        [SerializeField, Min(0), Tooltip("Ours: bits in the ring that widens from the spot when the shot lands on something with health, flat across the shot's path so the shooter sees it face on. 0 = no ring.")] private int ringAmount;
        [SerializeField, Tooltip("Ours: the ring's colour.")] private Color ringColour = Color.white;
        [SerializeField, Tooltip("Ours: how wide the ring gets (its radius), units at scale 1.")] private float ringReach = 0.35f;
        [SerializeField, Tooltip("Ours: the radius of one bit of the ring as it starts, units at scale 1. They shrink to nothing.")] private float ringSize = 0.02f;
        [SerializeField, Min(0.01f), Tooltip("Ours: seconds the ring takes to widen and thin away.")] private float ringSeconds = 0.18f;
        [SerializeField, Tooltip("The bright streak behind the shot: a Trail Renderer on this prefab. None = no streak (paint draws its own).")] private TrailRenderer streak;
        [SerializeField, Tooltip("How long the streak gets, units at scale 1 (the test room's dart: 1.6 m). It starts at nothing and grows as the shot flies away, so it never pokes back through the gun.")] private float streakLength = 1.584f;
        [SerializeField, Tooltip("How thick the streak is, units at scale 1 (the test room's dart: 0.045 m).")] private float streakWidth = 0.0446f;
        [SerializeField, Tooltip("The streak's colour, the same from the shot to its tail; its alpha is how solid it is (the test room's dart: pale blue at 0.75).")] private Color streakColour = new Color(0.9f, 0.97f, 1f, 0.75f);

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
        private bool _flying;   // false once it has landed and stayed (a stuck dart): it no longer moves or hits

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

        /// <summary>Puts every shot back in its pool, the flying ones and the ones that stayed where they landed (the
        /// Workshop reset).</summary>
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
            _flying = true;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_velocity));
            transform.localScale = _baseScale * _scale;
            gameObject.SetActive(true);
            if (streak != null)
            {
                // The streak lasts as long as the shot takes to fly its length: it grows from nothing as the shot
                // leaves, and is gone with the shot when it lands (the shot goes back to its pool, or stays and
                // stops drawing one).
                float pace = _velocity.magnitude;
                streak.Clear();
                streak.emitting = true;
                streak.time = pace > 1e-4f ? streakLength * _scale / pace : 0f;
                streak.widthMultiplier = streakWidth * _scale;
                streak.startColor = streak.endColor = streakColour;
            }
            Live.Add(this);
            OnLaunched();
        }

        // ---------- for other kinds of ammo ----------

        /// <summary>The shot's velocity, world units per second.</summary>
        protected Vector3 Velocity { get => _velocity; set => _velocity = value; }
        /// <summary>The shooter's scale this shot was fired at.</summary>
        protected float ShotScale => _scale;
        /// <summary>The shot's own size at that scale (its local scale while it flies).</summary>
        protected Vector3 FullSize => _baseScale * _scale;
        /// <summary>True while it flies; false once it has landed and stayed, or is in the pool.</summary>
        public bool Flying => _flying;
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
        /// <summary>The hit has counted (damage and shove are done); the shot goes back to its pool right after, unless it Stays.</summary>
        protected virtual void OnLanded(in RaycastHit hit, Vector3 point) { }
        /// <summary>Asked after OnLanded. Return true to stay in the scene where it landed (a dart that sticks): the shot
        /// then stops flying and hits nothing more, and whoever kept it calls Recycle when it has had its time.
        /// <paramref name="direction"/> is the way it was flying.</summary>
        protected virtual bool Stays(in RaycastHit hit, Vector3 point, Vector3 direction) => false;
        /// <summary>The shot is going back to its pool (its time is up, it landed, or everything was cleared): an ammo
        /// that kept anything about where it stayed lets go of it here.</summary>
        protected virtual void OnRecycled() { }

        protected void Recycle()
        {
            OnRecycled();
            _flying = false;
            gameObject.SetActive(false);
            Live.Remove(this);
            if (_prefab != null && Pools.TryGetValue(_prefab, out var pool)) pool.Push(this);
            else Destroy(gameObject);
        }

        /// <summary>What a landing shows and sounds like: a few bits thrown off the surface, and a sound there.
        /// <paramref name="counted"/>: it landed on something with health and the hit counted, which can throw its own
        /// bits in place of the wall's, and a ring.</summary>
        private void Landed(in RaycastHit hit, Vector3 point, Vector3 dir, bool counted)
        {
            AudioClip clip = Pick(hitClips);
            bool marked = counted && targetBurstAmount > 0;
            bool ringed = counted && ringAmount > 0;
            if (burstAmount <= 0 && clip == null && !marked && !ringed) return;
            PaintSplats keeper = PaintSplats.InScene();   // the scene's paint keeper flies every puff of bits and plays the placed sounds
            Vector3 off = hit.distance > 0f && Vector3.Dot(hit.normal, dir) < 0f ? hit.normal : -dir;   // the side the shot came from
            Vector3 from = point + off * (0.03f * _scale);
            if (marked) keeper.Puff(from, off, targetBurstColour, targetBurstAmount, targetBurstSpeed * _scale, targetBurstSize * _scale, burstGravity * _scale, burstSeconds);
            else if (burstAmount > 0) keeper.Puff(from, off, burstColour, burstAmount, burstSpeed * _scale, burstSize * _scale, burstGravity * _scale, burstSeconds);
            if (ringed) keeper.Ring(from, -dir, ringColour, ringAmount, ringReach * _scale / ringSeconds, ringSize * _scale, ringSeconds);
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
            if (!_flying) return;   // it landed and stayed: whoever kept it looks after it now
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
                Landed(hit, point, dir, counted);
                // Whoever fired it is told that it landed on something with health, and whether that finished it.
                if (counted && _owner != null && _owner.TryGetComponent(out WeaponHolder holder)) GameEvents.RaiseShotLanded(holder, health.Dead);
                OnLanded(hit, point);
                if (Stays(hit, point, dir))
                {
                    // It stays where it landed: no longer moving, and it draws no more streak.
                    _flying = false;
                    _velocity = Vector3.zero;
                    if (streak != null) streak.emitting = false;
                    return;
                }
                Recycle();
                return;
            }
            transform.position += step;
            if (faceTravel) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
