using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A thrown ball's flight, worked out here step by step instead of by the physics engine: it falls by its own
    /// number, bounces, is gripped by what it lands on, skids, and rolls to a stop. The grenade uses it. Written out
    /// because the engine did not land a small fast ball the way the test room's does (measured for D-0077: swept, it
    /// bounced without the floor gripping it and slid half as far again; unswept, it sank into the floor and came out
    /// spinning backwards), and because this way the flight is the same at every size and whatever the project's
    /// gravity is set to. The numbers are the test room's ball and the rules its engine follows:
    /// each physics step the ball falls and slows a little (Drag), then moves, swept, so it never goes through
    /// anything. Where it touches, it bounces back with a share of the speed it came in with (Bounce; none at all
    /// below No Bounce Under), and the surface takes hold of it: the slide of the touching point is taken off (2/7
    /// of it stops a solid ball sliding), by no more than Grip times how hard it pressed, and the same push sets it
    /// spinning, never faster than Most Spin. Landing faster than it can spin, it skids and loses speed quickly,
    /// then rolls. Lying on the floor is the same rule every step. A loose thing it hits is nudged by what the ball
    /// lost. Its body is kinematic: nothing else moves it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class BallFlight : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("How fast it falls, units per second squared at toy size (the test room's gravity: 30). Its own number: the project's gravity setting does not move it. Higher: a flatter, shorter throw.")] private float fall = 30f;
        [SerializeField, Range(0f, 1f), Tooltip("The share of its speed into a surface it bounces back with (the test room: 0.35). 0 = it lands dead.")] private float bounce = 0.35f;
        [SerializeField, Min(0f), Tooltip("Landing slower than this, units per second at toy size, it does not bounce at all (the test room's engine: 1).")] private float noBounceUnder = 1f;
        [SerializeField, Min(0f), Tooltip("How well a surface holds it (the test room: 0.9). Higher: it stops skidding sooner. 0 = it slides like ice.")] private float grip = 0.9f;
        [SerializeField, Min(0f), Tooltip("The share of its speed it loses each second, in the air and on the floor (the test room: 0.6, the grenade's own 0.5 on top of the 0.1 its engine gives everything).")] private float drag = 0.6f;
        [SerializeField, Min(0f), Tooltip("The share of its spin it loses each second (the test room: 2.1, the grenade's own 2 on top of its engine's 0.1). Higher: a rolling ball stops sooner.")] private float spinDrag = 2.1f;
        [SerializeField, Min(0f), Tooltip("The fastest it may spin, radians a second (the test room's engine lets nothing spin faster than 47.12). It decides how the ball stops: landing faster than it can spin, it skids. Higher: it rolls on further after it lands.")] private float mostSpin = 47.12f;
        [SerializeField, Tooltip("What it can land on and bounce off.")] private LayerMask hitMask = ~0;

        private const int MostTouchesAStep = 3;   // a step's move is cut at each touch; after this many the rest of the step is dropped
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Found = new Collider[16];

        private Rigidbody _rb;
        private SphereCollider _ball;
        private Transform _pass, _passAlt;
        private Vector3 _place, _velocity, _spin;
        private Quaternion _turn;
        private float _size = 1f;
        private bool _flying;

        /// <summary>The ball's speed now, world units per second.</summary>
        public Vector3 Velocity => _velocity;
        /// <summary>The ball's spin now, radians per second about each world axis.</summary>
        public Vector3 Spin => _spin;
        /// <summary>The ball's radius in the world.</summary>
        public float Radius => _ball.radius * transform.lossyScale.x;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _ball = GetComponent<SphereCollider>();
            _rb.isKinematic = true;   // moved from here, step by step
            _rb.useGravity = false;
            _place = transform.position; _turn = transform.rotation;
            _flying = true;   // one placed in a scene just drops
        }

        /// <summary>Sends it off from where it is. <paramref name="size"/> is how many times the toy-size ball it is
        /// (its fall and its no-bounce speed grow with it; set the transform's scale yourself). It passes through
        /// <paramref name="pass"/> and <paramref name="passAlt"/> (whoever threw it), always.</summary>
        public void Launch(Vector3 velocity, Vector3 spin, float size, Transform pass, Transform passAlt)
        {
            _size = Mathf.Max(0.001f, size);
            _velocity = velocity; _spin = Vector3.ClampMagnitude(spin, mostSpin);
            _pass = pass; _passAlt = passAlt;
            _place = transform.position; _turn = transform.rotation;
            _flying = true;
        }

        /// <summary>Stops it where it is.</summary>
        public void Halt() { _flying = false; _velocity = Vector3.zero; _spin = Vector3.zero; }

        /// <summary>
        /// The flight this ball would take from <paramref name="from"/> at <paramref name="velocity"/>, thrown at
        /// <paramref name="size"/> (D-0148, the throw arc): stepped exactly as FixedUpdate steps it (fall, then drag, then a
        /// swept move), up to the first thing it touches or <paramref name="seconds"/>. Called on the prefab: its own
        /// numbers and radius. <paramref name="points"/> gets the path (one point a step); true with where it lands and
        /// the surface's normal when it touches something.
        /// </summary>
        public bool Predict(Vector3 from, Vector3 velocity, float size, Transform pass, Transform passAlt, float seconds,
            List<Vector3> points, out Vector3 landing, out Vector3 normal)
        {
            points.Clear();
            landing = from; normal = Vector3.up;
            size = Mathf.Max(0.001f, size);
            var sphere = GetComponent<SphereCollider>();
            float radius = (sphere != null ? sphere.radius : 0.1f) * Mathf.Abs(transform.localScale.x) * size;
            float dt = Time.fixedDeltaTime;
            Vector3 place = from, v = velocity;
            points.Add(place);
            int steps = Mathf.CeilToInt(seconds / Mathf.Max(1e-4f, dt));
            for (int s = 0; s < steps; s++)
            {
                v += Vector3.down * (fall * size * dt);
                v *= Mathf.Max(0f, 1f - drag * dt);
                Vector3 step = v * dt;
                float distance = step.magnitude;
                if (distance < 1e-7f) continue;
                Vector3 way = step / distance;
                int count = Physics.SphereCastNonAlloc(place, radius, way, Hits, distance, Layers.Seen(hitMask), QueryTriggerInteraction.Ignore);
                int first = -1;
                for (int h = 0; h < count; h++)
                {
                    Collider c = Hits[h].collider;
                    if (Hits[h].distance <= 0f || c.isTrigger) continue;
                    if ((pass != null && c.transform.IsChildOf(pass)) || (passAlt != null && c.transform.IsChildOf(passAlt))) continue;
                    if (first < 0 || Hits[h].distance < Hits[first].distance) first = h;
                }
                if (first >= 0)
                {
                    place += way * Hits[first].distance;
                    points.Add(place);
                    landing = Hits[first].point; normal = Hits[first].normal;
                    return true;
                }
                place += step;
                points.Add(place);
            }
            landing = place;
            return false;
        }

        private bool Passes(Collider c) =>
            c == _ball || c.transform.IsChildOf(transform) || (_pass != null && c.transform.IsChildOf(_pass)) || (_passAlt != null && c.transform.IsChildOf(_passAlt));

        private void FixedUpdate()
        {
            if (!_flying) return;
            float dt = Time.fixedDeltaTime, radius = Radius;
            // It falls and slows first, then moves with the new speed (the test room's engine's order).
            _velocity += Vector3.down * (fall * _size * dt);
            _velocity *= Mathf.Max(0f, 1f - drag * dt);
            _spin *= Mathf.Max(0f, 1f - spinDrag * dt);

            // The move, swept: cut at whatever it touches first, then on with what the touch left of its speed.
            float left = dt;
            for (int i = 0; i < MostTouchesAStep && left > 0f; i++)
            {
                Vector3 step = _velocity * left;
                float distance = step.magnitude;
                if (distance < 1e-7f) break;
                Vector3 way = step / distance;
                int count = Physics.SphereCastNonAlloc(_place, radius, way, Hits, distance, Layers.Seen(hitMask), QueryTriggerInteraction.Ignore);
                int first = -1;
                for (int h = 0; h < count; h++)
                {
                    if (Hits[h].distance <= 0f || Passes(Hits[h].collider)) continue;   // touching at the start: Settle's
                    if (first < 0 || Hits[h].distance < Hits[first].distance) first = h;
                }
                if (first < 0) { _place += step; break; }
                RaycastHit hit = Hits[first];
                _place += way * hit.distance;
                left *= 1f - hit.distance / distance;
                Touch(hit.normal, hit.collider, hit.point, radius);
            }
            Settle(radius);

            float turned = _spin.magnitude * dt * Mathf.Rad2Deg;
            if (turned > 1e-4f) _turn = Quaternion.AngleAxis(turned, _spin.normalized) * _turn;
            _rb.MovePosition(_place);
            _rb.MoveRotation(_turn);
        }

        /// <summary>Whatever it has ended the step sunk into (the floor it lies on, by the step's fall) pushes it back
        /// out and counts as a touch: this is what holds a lying ball up and lets the floor grip a rolling one.</summary>
        private void Settle(float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(_place, radius, Found, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = Found[i];
                if (Passes(c)) continue;
                if (!Physics.ComputePenetration(_ball, _place, _turn, c, c.transform.position, c.transform.rotation, out Vector3 out_, out float depth)) continue;
                _place += out_ * depth;
                Touch(out_, c, _place - out_ * radius, radius);
            }
        }

        /// <summary>One touch: the bounce, the grip, the spin it gives, and the nudge to a loose thing.
        /// <paramref name="normal"/> points from the surface to the ball.</summary>
        private void Touch(Vector3 normal, Collider other, Vector3 point, float radius)
        {
            Rigidbody body = other.attachedRigidbody;
            Vector3 ground = body != null ? body.GetPointVelocity(point) : Vector3.zero;
            float into = -Vector3.Dot(_velocity - ground, normal);   // how fast it is coming into the surface
            if (into <= 0f) return;
            Vector3 before = _velocity;
            float back = into > noBounceUnder * _size ? bounce * into : 0f;
            _velocity += normal * (into + back);
            // The touching point's slide along the surface: the ball's own speed there, and its spin's.
            Vector3 slide = _velocity - ground + Vector3.Cross(_spin, -normal * radius);
            slide -= normal * Vector3.Dot(slide, normal);
            Vector3 change = Vector3.ClampMagnitude(slide * (-2f / 7f), grip * (into + back));
            _velocity += change;
            // The same push, off centre, turns it (a solid ball: 5/2 of the speed taken, over its radius).
            _spin = Vector3.ClampMagnitude(_spin + Vector3.Cross(change, normal) * (2.5f / radius), mostSpin);
            if (body != null && !body.isKinematic) body.AddForceAtPosition((before - _velocity) * _rb.mass, point, ForceMode.Impulse);
        }
    }
}
