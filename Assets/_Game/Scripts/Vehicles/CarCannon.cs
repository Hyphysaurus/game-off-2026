using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A circus cannon for cars (D-0037, boost E). Roll a driven car slowly into the zone under its mouth (this object's
    /// trigger): the cannon holds it (IBoostable.Hold), lifts it into the barrel facing out, burns a short fuse and fires
    /// it along the barrel so it comes down a set number of car lengths away on the cannon's own floor, with a smoke puff,
    /// a boom and a kick of the barrel. The loaded car passes through the solid barrel until it is clear of it and takes
    /// no damage until a moment after it lands. Then the cannon rests. Everything scales with the transform, so Build
    /// Mode can place, turn and scale it.
    /// While a car is held the cannon sets its pose every frame (an ArcadeCar through PlaceAt, any other car through its
    /// transform); letting go hands the car back where the cannon left it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CarCannon : MonoBehaviour, IResettable
    {
        [Header("Barrel")]
        [SerializeField, Tooltip("At the mouth, on the bore's axis. Its forward is the way the car flies (pitched up).")] private Transform muzzle;
        [SerializeField, Tooltip("How deep the bore runs behind the mouth, units at scale 1.")] private float boreDepth = 3.5f;
        [SerializeField, Tooltip("The drawn barrel, kicked back on each shot. Optional.")] private Transform barrelVisual;
        [SerializeField, Tooltip("Solid parts the loaded car passes through. Found under this object if empty.")] private Collider[] solid;

        [Header("Loading")]
        [SerializeField, Tooltip("A driven car slower than this in the zone is loaded (units per second at car scale 1).")] private float loadSpeed = 5f;
        [SerializeField, Tooltip("Largest car scale it takes, relative to the cannon's scale.")] private float maxCarScale = 1.5f;
        [SerializeField, Tooltip("Seconds to lift the car into the barrel. 0 = snap it in.")] private float loadTime = 0.5f;
        [SerializeField, Tooltip("Seconds the fuse burns before the shot.")] private float fuseTime = 0.8f;
        [SerializeField, Tooltip("Seconds after a shot before it loads again.")] private float cooldown = 1.5f;

        [Header("Shot")]
        [SerializeField, Tooltip("Where the car comes down, in car lengths (level distance from where it leaves the barrel, at the cannon's floor height).")] private float distance = 15f;
        [SerializeField, Tooltip("One car length, units at car scale 1 (the toy cars are 3.8 long).")] private float carLength = 3.8f;
        [SerializeField, Tooltip("Gravity for a body that does not use Unity's and is not the wooden car (that one reports its own).")] private float ownGravity = 25f;
        [SerializeField, Tooltip("The car takes no damage in the air and for this many seconds after it lands.")] private float landingGrace = 0.75f;

        [Header("Feedback (all optional)")]
        [SerializeField, Tooltip("Smoke puffed out of the mouth on a shot (a looping smoke is turned into one burst).")] private GameObject smokePrefab;
        [SerializeField] private int puffCount = 14;
        [SerializeField, Tooltip("Seconds the puff lasts.")] private float puffSeconds = 2.5f;
        [SerializeField, Tooltip("Puff speed out of the mouth, units per second at scale 1.")] private float puffSpeed = 6f;
        [SerializeField] private AudioClip loadClip, fuseClip, boomClip;
        [SerializeField, Tooltip("How far the barrel kicks back on a shot, units at scale 1.")] private float recoil = 0.6f;
        [SerializeField, Tooltip("Seconds the barrel takes to come back.")] private float recoilTime = 0.4f;

        private enum Phase { Ready, Loading, Fuse }

        private static readonly List<Collider> Found = new List<Collider>();

        private Phase _phase;
        private IBoostable _car;
        private Component _carBody;           // the loaded car's root object
        private ArcadeCar _arcade;            // set when the loaded car is one
        private Bounds _box;                  // its drawn box in its own axes, world units, around its root
        private Vector3 _fromPos;
        private Quaternion _fromRot;          // where it was when loaded: an abort puts it back
        private float _phaseAt, _readyAt, _clearFrom, _kickAt = -100f;
        private bool _clearing, _kicked;      // clearing: a car that left the barrel still passes through it
        private readonly List<Collider> _ignored = new List<Collider>();
        private AudioSource _audio;
        private Vector3 _barrelRest;

        private float Scale => Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            _audio = GetComponent<AudioSource>();
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false; _audio.spatialBlend = 0.7f;
            }
            if (barrelVisual != null) _barrelRest = barrelVisual.localPosition;
            if (solid == null || solid.Length == 0)
            {
                GetComponentsInChildren(false, Found);
                Found.RemoveAll(c => c.isTrigger);
                solid = Found.ToArray();
                Found.Clear();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (_phase != Phase.Ready || Time.time < _readyAt || muzzle == null || GameMode.Current != GameMode.Mode.Play) return;
            var car = FindCar(other, out Component body);
            if (car == null || !car.Driven || !body.gameObject.activeInHierarchy) return;
            var arcade = body.GetComponent<ArcadeCar>();
            if (arcade != null && (arcade.Wrecked || arcade.Parked)) return;   // a wreck, or a car on a garage lift
            float carScale = Mathf.Max(0.001f, Mathf.Abs(body.transform.lossyScale.y));
            if (carScale > Scale * maxCarScale) return;
            if (SpeedOf(car, other) > loadSpeed * carScale) return;
            Load(car, body, arcade);
        }

        /// <summary>The car behind a collider: the IBoostable on its body, or the wooden car whose hidden physics ball this is.</summary>
        private static IBoostable FindCar(Collider other, out Component body)
        {
            body = null;
            var rb = other.attachedRigidbody;
            if (rb == null) return null;
            var car = rb.GetComponentInParent<IBoostable>();
            if (car == null)
                foreach (var toy in ToyCar.All)
                    if (toy != null && toy.Ball == rb.transform) { car = (object)toy as IBoostable; break; }
            body = car as Component;
            return body != null ? car : null;
        }

        private static float SpeedOf(IBoostable car, Collider touched)
        {
            if (car is IChaseTarget chase) return chase.ChaseVelocity.magnitude;
            var rb = touched.attachedRigidbody;
            return rb != null ? rb.linearVelocity.magnitude : 0f;
        }

        private void Load(IBoostable car, Component body, ArcadeCar arcade)
        {
            if (_clearing) Restore();   // the last car is still passing through the barrel: it collides again
            _car = car; _carBody = body; _arcade = arcade;
            _fromPos = body.transform.position; _fromRot = body.transform.rotation;
            _box = CarBox(body.transform);
            Collect();                  // before the hold: a held wooden car may hide its ball
            car.Hold(true);
            Ignore(true);
            _phase = loadTime > 0f ? Phase.Loading : Phase.Fuse;
            _phaseAt = Time.time;
            Play(loadClip);
            if (_phase == Phase.Fuse) Play(fuseClip);
            PoseNow();
        }

        private void Update()
        {
            Kick();
            if (_clearing && Time.time >= _clearFrom && (Time.time - _clearFrom > 4f || !Overlapping())) Restore();
            if (_phase == Phase.Ready) return;
            if (_carBody == null || !_carBody.gameObject.activeInHierarchy || GameMode.Current != GameMode.Mode.Play) { Abort(true); return; }
            float t = Time.time - _phaseAt;
            if (_phase == Phase.Loading && t >= loadTime) { _phase = Phase.Fuse; _phaseAt = Time.time; Play(fuseClip); }
            else if (_phase == Phase.Fuse && t >= fuseTime) Fire();
        }

        // Held cars are posed before each physics step and again after everything else moved this frame.
        private void FixedUpdate() { if (_phase != Phase.Ready && _carBody != null) PoseNow(); }
        private void LateUpdate() { if (_phase != Phase.Ready && _carBody != null) PoseNow(); }

        /// <summary>Where the car's root sits in the barrel: its middle on the bore's axis, its tail at the bore's bottom,
        /// facing out of the mouth.</summary>
        private Vector3 Seat(out Quaternion rotation)
        {
            rotation = muzzle.rotation;
            Vector3 bottom = muzzle.position - muzzle.forward * (boreDepth * Scale);
            return bottom + muzzle.forward * (0.05f * Scale - _box.min.z) - rotation * new Vector3(_box.center.x, _box.center.y, 0f);
        }

        private void PoseNow()
        {
            Vector3 seat = Seat(out Quaternion seatRot);
            if (_phase == Phase.Fuse) { Pose(seat, seatRot); return; }
            const float Lift = 0.65f;   // share of the load spent lifting the car to the mouth; the rest slides it in
            float u = Mathf.Clamp01((Time.time - _phaseAt) / Mathf.Max(0.01f, loadTime));
            Vector3 outside = seat + muzzle.forward * ((boreDepth + 0.3f) * Scale);   // the car just out of the mouth
            if (u >= Lift) { Pose(Vector3.Lerp(outside, seat, Smooth((u - Lift) / (1f - Lift))), seatRot); return; }
            // Out from under the barrel first, then up in front of the mouth, turning to face out on the way.
            float a = Smooth(u / Lift);
            Vector3 under = new Vector3(outside.x, _fromPos.y, outside.z);
            Vector3 p = Vector3.Lerp(Vector3.Lerp(_fromPos, under, a), Vector3.Lerp(under, outside, a), a);
            float turn = Smooth(Mathf.Clamp01((a - 0.2f) / 0.8f));
            Vector3 e0 = _fromRot.eulerAngles, e1 = seatRot.eulerAngles;
            Pose(p, Quaternion.Euler(Mathf.LerpAngle(e0.x, e1.x, turn), Mathf.LerpAngle(e0.y, e1.y, turn), Mathf.LerpAngle(e0.z, e1.z, turn)));
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private void Pose(Vector3 position, Quaternion rotation)
        {
            if (_carBody == null) return;
            if (_arcade != null) _arcade.PlaceAt(position, rotation);
            else _carBody.transform.SetPositionAndRotation(position, rotation);
        }

        private void Fire()
        {
            Vector3 seat = Seat(out Quaternion rotation);
            Pose(seat, rotation);
            var car = _car; var body = _carBody;
            _phase = Phase.Ready; _car = null; _carBody = null; _arcade = null;
            car.Hold(false);
            Ignore(true);   // a hold may switch colliders off and on, which forgets ignored pairs
            _clearing = true; _clearFrom = Time.time + 0.2f;

            Rigidbody flying = FlyingBody(body);
            float g = flying != null && !flying.useGravity ? (car is ToyCar toy ? toy.Gravity : ownGravity) : -Physics.gravity.y;
            float damping = flying != null ? flying.linearDamping : 0f;
            float carScale = Mathf.Max(0.001f, Mathf.Abs(body.transform.lossyScale.y));
            Vector3 dir = muzzle.forward;
            float range = distance * carLength * carScale;
            float speed = LaunchSpeed(range, seat.y - transform.position.y, Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)), g, damping, out float flight);
            // The physics step adds gravity before it moves a body, which drops it g*dt/2 per second under the true arc
            // (about 1.5% short at prop gravity): half a step of lift puts it back.
            car.Launch(dir * speed + Vector3.up * (Mathf.Max(0f, g) * Time.fixedDeltaTime * 0.5f));
            var health = body.GetComponent<Damageable>();
            if (health != null) health.Protect(flight + landingGrace);

            _readyAt = Time.time + cooldown;
            if (_audio != null) _audio.Stop();   // cuts the fuse
            Play(boomClip);
            Puff();
            _kickAt = Time.time;
        }

        /// <summary>The body that flies: the car's own, or the wooden car's physics ball.</summary>
        private static Rigidbody FlyingBody(Component body)
        {
            var rb = body.GetComponent<Rigidbody>();
            if (rb != null) return rb;
            var toy = body.GetComponent<ToyCar>();
            return toy != null && toy.Ball != null ? toy.Ball.GetComponent<Rigidbody>() : null;
        }

        /// <summary>
        /// The speed along a heading <paramref name="pitch"/> (radians) above level that comes down <paramref name="range"/>
        /// away (level distance) and <paramref name="drop"/> below where it started, under gravity <paramref name="g"/> and
        /// linear damping <paramref name="damping"/> (per second). <paramref name="flight"/> is the time in the air.
        /// </summary>
        public static float LaunchSpeed(float range, float drop, float pitch, float g, float damping, out float flight)
        {
            pitch = Mathf.Clamp(pitch, 5f * Mathf.Deg2Rad, 85f * Mathf.Deg2Rad);
            g = Mathf.Max(0.01f, g); range = Mathf.Max(0.01f, range); drop = Mathf.Max(0f, drop);
            float c = Mathf.Cos(pitch), s = Mathf.Sin(pitch);
            if (damping < 1e-3f)
            {
                float v = Mathf.Sqrt(g * range * range / (2f * c * c * (drop + range * s / c)));
                flight = range / (v * c);
                return v;
            }
            // Damping has no closed form, but the distance grows with the speed: narrow the speed down.
            float lo = 0f, hi = Mathf.Sqrt(g * range / (2f * s * c)) + 1f;   // the speed for level ground without damping, and a bit
            for (int i = 0; i < 24 && Reach(hi, c, s, g, damping, drop, out _) < range; i++) hi *= 2f;
            for (int i = 0; i < 40; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Reach(mid, c, s, g, damping, drop, out _) < range) lo = mid; else hi = mid;
            }
            Reach(hi, c, s, g, damping, drop, out flight);
            return hi;
        }

        /// <summary>Level distance flown at speed <paramref name="v"/> until the body is <paramref name="drop"/> below its
        /// start, with linear damping k: x(t) = vx (1 - e^-kt) / k, y(t) = (vy + g/k)(1 - e^-kt) / k - (g/k) t.</summary>
        private static float Reach(float v, float c, float s, float g, float k, float drop, out float time)
        {
            double vx = v * c, vy = v * s, gk = (double)g / k;
            double a = System.Math.Log(1.0 + vy * k / g) / k;   // the top of the arc
            double b = System.Math.Max(2.0 * a, 0.05);
            for (int i = 0; i < 30 && Height(b) > -drop; i++) b *= 2.0;
            for (int i = 0; i < 40; i++)
            {
                double m = (a + b) * 0.5;
                if (Height(m) > -drop) a = m; else b = m;
            }
            time = (float)b;
            return (float)(vx * (1.0 - System.Math.Exp(-k * b)) / k);

            double Height(double t) => (vy + gk) * (1.0 - System.Math.Exp(-k * t)) / k - gk * t;
        }

        /// <summary>The car's drawn box in its own axes (world units, around its root): where its middle and tail are.</summary>
        private static Bounds CarBox(Transform car)
        {
            Quaternion inverse = Quaternion.Inverse(car.rotation);
            var box = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in car.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) || !r.enabled || r.forceRenderingOff) continue;   // a seated driver is hidden
                var f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null) continue;
                Bounds mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = inverse * (r.transform.TransformPoint(corner) - car.position);
                    if (!any) { box = new Bounds(p, Vector3.zero); any = true; } else box.Encapsulate(p);
                }
            }
            return any ? box : new Bounds(Vector3.up * 0.5f, Vector3.one);
        }

        // ---------- the barrel lets the loaded car through ----------

        private void Collect()
        {
            _ignored.Clear();
            if (_carBody == null) return;
            _carBody.GetComponentsInChildren(false, Found);
            foreach (var c in Found)
                if (c != null && !c.isTrigger && !(c is CharacterController) && c.GetComponentInParent<PlayerMotor>() == null) _ignored.Add(c);   // not the seated driver
            Found.Clear();
            var toy = _carBody.GetComponent<ToyCar>();
            if (toy != null && toy.Ball != null)
            {
                var ball = toy.Ball.GetComponent<Collider>();
                if (ball != null && !_ignored.Contains(ball)) _ignored.Add(ball);
            }
        }

        private void Ignore(bool ignore)
        {
            if (solid == null) return;
            foreach (var c in _ignored)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;   // switched-off colliders are refused
                foreach (var s in solid)
                    if (s != null && s.enabled && s.gameObject.activeInHierarchy) Physics.IgnoreCollision(c, s, ignore);
            }
        }

        private bool Overlapping()
        {
            if (solid == null) return false;
            foreach (var c in _ignored)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                foreach (var s in solid)
                {
                    if (s == null || !s.enabled || !s.gameObject.activeInHierarchy) continue;
                    if (Physics.ComputePenetration(c, c.transform.position, c.transform.rotation, s, s.transform.position, s.transform.rotation, out _, out _)) return true;
                }
            }
            return false;
        }

        private void Restore()
        {
            Ignore(false);
            _ignored.Clear();
            _clearing = false;
        }

        /// <summary>Lets a loaded car go without a shot (left Play, reset, the car vanished); <paramref name="putBack"/>
        /// returns it to where it was loaded from.</summary>
        private void Abort(bool putBack)
        {
            var car = _car; var body = _carBody;
            bool live = body != null && body.gameObject.activeInHierarchy;
            if (live && putBack) Pose(_fromPos, _fromRot);
            if (body != null) car.Hold(false);   // hidden (Build Mode deleted it) or not, it must not stay held
            if (live) { Ignore(true); _clearing = true; _clearFrom = Time.time; }
            else { _ignored.Clear(); _clearing = false; }   // switching colliders off forgets ignored pairs anyway
            _phase = Phase.Ready; _car = null; _carBody = null; _arcade = null;
            _readyAt = Time.time + cooldown;
            if (_audio != null) _audio.Stop();
        }

        public void ResetState()
        {
            if (_phase != Phase.Ready) Abort(_arcade == null);   // an ArcadeCar goes back to its own start; anything else out of the barrel
            if (_clearing) Restore();
            _readyAt = 0f;
            _kickAt = -100f;
            if (barrelVisual != null) barrelVisual.localPosition = _barrelRest;
            _kicked = false;
        }

        private void OnDisable()
        {
            if (_phase != Phase.Ready) Abort(true);
            if (_clearing) Restore();
            if (barrelVisual != null) barrelVisual.localPosition = _barrelRest;
            _kicked = false;
        }

        // ---------- feedback ----------

        private void Play(AudioClip clip) { if (clip != null && _audio != null) _audio.PlayOneShot(clip); }

        /// <summary>One puff of the smoke prefab out of the mouth: its looping emitter is stopped and fired as a single burst.</summary>
        private void Puff()
        {
            if (smokePrefab == null) return;
            var go = Instantiate(smokePrefab, muzzle.position, muzzle.rotation);
            go.transform.localScale = Vector3.one * Scale;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.startLifetime = puffSeconds;
                main.startSpeed = puffSpeed;
                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(puffCount, 1, 500)) });
                ps.Play(false);
            }
            Destroy(go, puffSeconds + 1f);
        }

        /// <summary>The barrel jumps back on the shot and eases forward again.</summary>
        private void Kick()
        {
            if (barrelVisual == null || muzzle == null) return;
            float t = (Time.time - _kickAt) / Mathf.Max(0.01f, recoilTime);
            if (t >= 1f)
            {
                if (_kicked) { barrelVisual.localPosition = _barrelRest; _kicked = false; }
                return;
            }
            Vector3 back = -muzzle.forward * (recoil * Scale * (1f - t) * (1f - t));
            barrelVisual.localPosition = _barrelRest + (barrelVisual.parent != null ? barrelVisual.parent.InverseTransformVector(back) : back);
            _kicked = true;
        }

        /// <summary>The shot's arc for a car at the cannon's own size, and where it comes down. The arc's shape does not
        /// depend on gravity (only its speed does), so this is what play will show, damping aside.</summary>
        private void OnDrawGizmosSelected()
        {
            if (muzzle == null) return;
            float s = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
            Vector3 dir = muzzle.forward, flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-6f || dir.y <= 0f) return;
            float level = flat.magnitude;
            flat /= level;
            float tan = dir.y / level;
            Vector3 start = muzzle.position - dir * (boreDepth * s * 0.5f);   // about where a car leaves the barrel
            float h = Mathf.Max(0f, start.y - transform.position.y), range = Mathf.Max(0.1f, distance * carLength * s);
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            Vector3 prev = start;
            for (int i = 1; i <= 32; i++)
            {
                float x = range * i / 32f;
                Vector3 p = start + flat * x + Vector3.up * (x * tan - x * x * (h + range * tan) / (range * range));
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.DrawWireSphere(prev, carLength * s * 0.5f);
        }
    }
}
