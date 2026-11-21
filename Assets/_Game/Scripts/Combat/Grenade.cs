using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A thrown popper, after the test room's grenade. It bounces, then goes off when its fuse runs out: everything
    /// with health inside its reach takes a hit, hardest at the middle and nothing at the edge, and loose things are
    /// thrown outward and up. A body made of several parts is hit once, by its closest part. The blast does not look
    /// for walls: it reaches through them, and it hurts whoever threw it too (both as in the test room).
    /// Sized to its thrower (Size To Thrower): at kid size it is as many times bigger as the kid is, falls that many
    /// times faster, so its arc keeps its shape and its timing, and its blast reaches that many times further. The fuse
    /// and the damage stay. Loose things are thrown harder by the square root of the size (the shove's size rule).
    /// How it flies, bounces and rolls is its BallFlight (its own fall, not the project's gravity, so the arc is the
    /// same whatever that is set to); this piece is the fuse and the blast.
    /// It announces nothing itself: whoever threw it listens to WentOff. The reset key takes it out of the air.
    /// </summary>
    [RequireComponent(typeof(BallFlight))]
    public sealed class Grenade : MonoBehaviour, IResettable
    {
        [Header("The fuse and the blast")]
        [SerializeField, Min(0f), Tooltip("Seconds from the throw until it goes off (the test room: 1.5).")] private float fuse = 1.5f;
        [SerializeField, Min(0f), Tooltip("How far the blast reaches, units at toy size (the test room: 4.5). It grows with the thrower.")] private float blastRadius = 4.5f;
        [SerializeField, Tooltip("On: the grenade, its fall and its blast grow with whoever throws it. Off: the toy-size grenade for everyone.")] private bool sizeToThrower = true;
        [SerializeField, Min(0f), Tooltip("Health taken at the very middle; it falls away to nothing at the edge (the test room: 6; 15 here is 1 there).")] private float damage = 90f;
        [SerializeField, Min(0f), Tooltip("The speed a loose thing at the very middle is thrown off at, whatever it weighs; less further out (the test room: 9). A kid's throws them the square root of the kid's size harder.")] private float push = 9f;
        [SerializeField, Tooltip("How much of the throw-off goes upward, as a share of the outward part (the test room: 0.6).")] private float lift = 0.6f;
        [SerializeField, Tooltip("What the blast can reach.")] private LayerMask hitMask = ~0;

        [Header("What the blast shows and sounds like")]
        [SerializeField, Tooltip("One of these plays where it goes off. Licensed clips under _Imports: a project without them is silent.")] private AudioClip[] blastClips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f), Tooltip("How loud the blast is.")] private float blastVolume = 1f;
        [SerializeField, Tooltip("The bright bits' colour, and the flash's.")] private Color blastColour = new Color(1f, 0.85f, 0.4f);
        [SerializeField, Min(0), Tooltip("Bright bits thrown up from the middle (the test room: 28).")] private int blastBits = 28;
        [SerializeField, Tooltip("The bright bits: the fastest one's speed, a bit's radius, how fast they fall, all at toy size (the test room: 9, 0.09, 6).")] private Vector3 blastSpeedSizeFall = new Vector3(9f, 0.09f, 6f);
        [SerializeField, Min(0.01f), Tooltip("Seconds a bright bit lasts (the test room: 0.5).")] private float blastSeconds = 0.5f;
        [SerializeField, Tooltip("The smoke's colour.")] private Color smokeColour = new Color(0.85f, 0.85f, 0.9f);
        [SerializeField, Min(0), Tooltip("Smoke bits (the test room: 16).")] private int smokeBits = 16;
        [SerializeField, Tooltip("The smoke: the fastest bit's speed, a bit's radius, how fast it falls, at toy size (the test room: 3.5, 0.16, and -1: it rose). Here the fall is 0: every falling bit in the scene shares one pull, so smoke that rose would lift the bright bits with it.")] private Vector3 smokeSpeedSizeFall = new Vector3(3.5f, 0.16f, 0f);
        [SerializeField, Min(0.01f), Tooltip("Seconds a smoke bit lasts (the test room: 0.9).")] private float smokeSeconds = 0.9f;
        [SerializeField, Min(0f), Tooltip("How bright the flash of light starts (the test room: 6). 0 = no flash.")] private float flashIntensity = 6f;
        [SerializeField, Min(0f), Tooltip("Seconds the flash takes to fade to nothing (the test room: 0.18).")] private float flashSeconds = 0.18f;
        [SerializeField, Tooltip("How far the flash's light reaches, as a share of the blast's reach (the test room: 1.5).")] private float flashReach = 1.5f;
        [SerializeField, Tooltip("How far above the middle the flash's light sits, units at toy size (the test room: 0.3).")] private float flashHeight = 0.3f;

        private static readonly Collider[] Found = new Collider[256];
        private static readonly Dictionary<Damageable, Part> Parts = new Dictionary<Damageable, Part>();
        private static readonly HashSet<Rigidbody> Loose = new HashSet<Rigidbody>();

        private struct Part { public float share; public Vector3 at; }

        private BallFlight _flight;
        private SphereCollider _ball;
        private Transform _thrower;
        private Vector3 _baseScale;
        private float _size = 1f, _left, _flashLeft;
        private bool _gone;
        private Light _flash;

        /// <summary>Raised once when it goes off: how many things with health the blast hit, and whether it finished any of them.</summary>
        public event Action<int, bool> WentOff;
        /// <summary>How many times the toy-size grenade this one is.</summary>
        public float Size => _size;
        /// <summary>How far this one's blast reaches, world units.</summary>
        public float Reach => blastRadius * _size;
        /// <summary>True once it has gone off (it stays a moment for its flash to fade).</summary>
        public bool Gone => _gone;
        /// <summary>The size a grenade thrown by someone of this scale has: the scale, or 1 with Size To Thrower off.</summary>
        public float SizeFor(float throwerScale) => sizeToThrower ? Mathf.Max(0.001f, throwerScale) : 1f;

        private void Awake()
        {
            _flight = GetComponent<BallFlight>();
            _ball = GetComponent<SphereCollider>();
            _baseScale = transform.localScale;
            _left = fuse;
        }

        /// <summary>Sends it on its way. <paramref name="thrower"/> and <paramref name="throwerAlt"/> (the first-person
        /// body, when there is one) are never bumped into as it leaves; <paramref name="throwerScale"/> is how big the
        /// thrower is (1 toy). Place it first; the velocity and the spin are in world units.</summary>
        public void Throw(Transform thrower, Transform throwerAlt, float throwerScale, Vector3 velocity, Vector3 spin)
        {
            _thrower = thrower;
            _size = SizeFor(throwerScale);
            transform.localScale = _baseScale * _size;   // the ball and the model grow together; the weight stays
            _flight.Launch(velocity, spin, _size, thrower, throwerAlt);
        }

        private void FixedUpdate()
        {
            if (_gone) return;
            _left -= Time.fixedDeltaTime;
            if (_left <= 0f) GoOff();
        }

        private void Update()
        {
            if (!_gone) return;
            // The flash fades to nothing, then the grenade is gone.
            _flashLeft -= Time.deltaTime;
            if (_flashLeft <= 0f) { Destroy(gameObject); return; }
            if (_flash != null) _flash.intensity = Brightness() * (_flashLeft / flashSeconds);
        }

        // A point light dims with the square of the distance: a blast as many times bigger needs that much more light
        // to look the same from as many times further away.
        private float Brightness() => flashIntensity * _size * _size;

        /// <summary>The reset key: a grenade in the air is taken out without going off.</summary>
        public void ResetState() => Destroy(gameObject);

        /// <summary>Goes off now, where it is.</summary>
        /// <summary>Online (D-0117): another player's grenade flown here as a look. It goes off with its flash, smoke and
        /// boom, but hurts and shoves nothing: that is the thrower's PC's to do.</summary>
        public bool LookOnly { get; set; }

        public void GoOff()
        {
            if (_gone) return;
            _gone = true;
            Vector3 middle = transform.position;
            float reach = Reach;
            // The shove's size rule (Knockback): a loose thing takes the square root of the size, not all of it.
            float looseShare = 1f / Mathf.Sqrt(_size);
            Parts.Clear(); Loose.Clear();
            int count = LookOnly ? 0 : Physics.OverlapSphereNonAlloc(middle, reach, Found, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = Found[i];
                if (c == null || c.transform.IsChildOf(transform)) continue;
                // Loose things are thrown outward and up, each once, the same speed whatever they weigh.
                Rigidbody body = c.attachedRigidbody;
                if (body != null && !body.isKinematic && Loose.Add(body))
                {
                    Vector3 away = body.worldCenterOfMass - middle;
                    float share = Share(away, reach);
                    if (share > 0f) Knockback.Apply(body.gameObject, (Way(away) + Vector3.up * lift) * (push * _size * share * body.mass), body.worldCenterOfMass, looseShare);
                }
                // Anything with health is hit once, by its closest part.
                Damageable health = Damageable.Find(c);
                if (health == null) continue;
                Vector3 at = c.bounds.center;
                float near = Share(at - middle, reach);
                if (!Parts.TryGetValue(health, out Part best) || near > best.share) Parts[health] = new Part { share = near, at = at };
            }
            int hits = 0; bool finished = false;
            foreach (KeyValuePair<Damageable, Part> hit in Parts)
            {
                if (hit.Value.share <= 0f || hit.Key == null) continue;
                if (!hit.Key.Hit(new DamageInfo(damage * hit.Value.share, Vector3.zero, hit.Value.at, _thrower, looseShare))) continue;
                hits++;
                if (hit.Key.Dead) finished = true;
            }
            Parts.Clear(); Loose.Clear();

            // What it shows and sounds like: the bright bits, the smoke, the boom, a short flash of light.
            PaintSplats keeper = PaintSplats.InScene();
            keeper.Puff(middle, Vector3.up, blastColour, blastBits, blastSpeedSizeFall.x * _size, blastSpeedSizeFall.y * _size, blastSpeedSizeFall.z * _size, blastSeconds);
            keeper.Puff(middle, Vector3.up, smokeColour, smokeBits, smokeSpeedSizeFall.x * _size, smokeSpeedSizeFall.y * _size, smokeSpeedSizeFall.z * _size, smokeSeconds);
            if (blastClips != null && blastClips.Length > 0)
            {
                AudioClip clip = blastClips[UnityEngine.Random.Range(0, blastClips.Length)];
                if (clip != null) keeper.Play(clip, middle, blastVolume);   // a clip that is gone is not null to C#: Unity's own test
            }
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            _ball.enabled = false;
            _flight.Halt();
            _flashLeft = flashIntensity > 0f ? flashSeconds : 0f;
            if (_flashLeft > 0f)
            {
                var lamp = new GameObject("Flash");
                lamp.transform.SetParent(transform, false);
                lamp.transform.position = middle + Vector3.up * (flashHeight * _size);
                _flash = lamp.AddComponent<Light>();
                _flash.type = LightType.Point;
                _flash.color = blastColour;
                _flash.range = reach * flashReach;
                _flash.intensity = Brightness();
                _flash.shadows = LightShadows.None;
            }
            WentOff?.Invoke(hits, finished);
            if (_flashLeft <= 0f) Destroy(gameObject);
        }

        /// <summary>1 at the middle, 0 at the edge of the reach and beyond.</summary>
        private static float Share(Vector3 away, float reach) => reach > 0f ? 1f - Mathf.Clamp01(away.magnitude / reach) : 0f;

        private static Vector3 Way(Vector3 away) => away.sqrMagnitude > 1e-10f ? away.normalized : Vector3.zero;
    }
}
