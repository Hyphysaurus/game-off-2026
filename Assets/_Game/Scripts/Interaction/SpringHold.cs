using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The hold both grabs share (first person's HL2 hands, third person's Skyrim-style grab): the held Rigidbody floats
    /// toward a point and a turn on a velocity spring, never parented, so it still bumps into walls and other props. Jammed
    /// for a moment, it slips. The owner says where the point is each physics step, which way it faces, and which collider
    /// is its own (what it holds never pushes it or carries it).
    /// </summary>
    public sealed class SpringHold
    {
        public struct Tuning
        {
            /// <summary>Share of the gap closed each physics step.</summary>
            public float stiffness;
            /// <summary>Fastest the thing chases the point, units per second at scale 1.</summary>
            public float maxSpeed;
            /// <summary>Further than this from the point (units at scale 1) for a quarter second, it slips.</summary>
            public float breakDistance;
        }

        public Rigidbody Held { get; private set; }
        /// <summary>The held thing's turn relative to the holder's heading (a yaw).</summary>
        public Quaternion Turn { get; set; } = Quaternion.identity;
        /// <summary>How far along the aim the hold point sits, world units.</summary>
        public float Distance { get; set; }

        private readonly List<Collider> _colliders = new List<Collider>();
        private Collider _holder;
        private Vector3 _centreLocal;
        private float _strain, _savedMaxAngular;
        private bool _savedGravity;
        private RigidbodyInterpolation _savedInterp;

        /// <summary>True when the held body was destroyed while held (Unity null, but the reference is still set).</summary>
        public bool HeldWasDestroyed => Held == null && Held is object;

        /// <summary>Takes <paramref name="rb"/>: no gravity while held, interpolated, never touching <paramref name="holder"/>.</summary>
        public void Grab(Rigidbody rb, Quaternion heading, Collider holder)
        {
            Release();
            Held = rb; _holder = holder;
            _savedGravity = rb.useGravity; _savedInterp = rb.interpolation; _savedMaxAngular = rb.maxAngularVelocity;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.maxAngularVelocity = 30f;
            Bounds b = BoundsOf(rb);
            _centreLocal = Quaternion.Inverse(rb.rotation) * (b.center - rb.position);
            Turn = Quaternion.Inverse(heading) * rb.rotation;
            _strain = 0f;
            _colliders.Clear();
            foreach (var c in rb.GetComponentsInChildren<Collider>())
                if (c.attachedRigidbody == rb) { _colliders.Add(c); if (holder != null) Physics.IgnoreCollision(c, holder, true); }
        }

        /// <summary>Lets go gently: the thing keeps its motion. Returns what was held (null if nothing, or destroyed).</summary>
        public Rigidbody Release()
        {
            var rb = Held;
            Held = null;
            if (rb != null)
            {
                rb.useGravity = _savedGravity;
                rb.interpolation = _savedInterp;
                rb.maxAngularVelocity = _savedMaxAngular;
            }
            if (_holder != null) foreach (var c in _colliders) if (c != null) Physics.IgnoreCollision(c, _holder, false);
            _colliders.Clear();
            _holder = null;
            return rb != null ? rb : null;
        }

        /// <summary>
        /// One physics step toward <paramref name="centre"/> (where the thing's middle should be), turned to heading * Turn.
        /// Returns false when nothing is held, or when it was jammed too long and slipped.
        /// </summary>
        public bool Drive(Vector3 centre, Quaternion heading, float scale, Tuning t, float dt)
        {
            var rb = Held;
            if (rb == null) return false;
            Quaternion targetRot = heading * Turn;
            Vector3 targetPos = centre - targetRot * _centreLocal;

            Vector3 gap = targetPos - rb.position;
            Vector3 want = Vector3.ClampMagnitude(gap * (t.stiffness / dt), t.maxSpeed * scale);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, want, 0.8f);

            Quaternion delta = targetRot * Quaternion.Inverse(rb.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (!float.IsNaN(axis.x) && axis.sqrMagnitude > 1e-6f)
                rb.angularVelocity = Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * t.stiffness / dt), 25f);

            // Stuck behind something for a moment: it slips from the hands.
            _strain = gap.magnitude > t.breakDistance * scale ? _strain + dt : 0f;
            if (_strain > 0.25f) { Release(); return false; }
            return true;
        }

        /// <summary>Turns the held thing by a look delta in degrees, about the world's up and the view's right.</summary>
        public void RotateBy(Vector2 degrees, Quaternion heading, Vector3 viewRight)
        {
            Quaternion world = Quaternion.AngleAxis(-degrees.x, Vector3.up) * Quaternion.AngleAxis(degrees.y, viewRight);
            Turn = Quaternion.Inverse(heading) * world * heading * Turn;
        }

        /// <summary>Turns the held thing about the vertical (a mouse-wheel notch).</summary>
        public void Spin(float degrees) => Turn = Quaternion.Euler(0f, degrees, 0f) * Turn;

        /// <summary>The box around a body's solid colliders (triggers left out).</summary>
        public static Bounds BoundsOf(Rigidbody rb)
        {
            var cols = rb.GetComponentsInChildren<Collider>();
            Bounds b = new Bounds(rb.position, Vector3.zero);
            bool any = false;
            foreach (var c in cols)
            {
                if (c.isTrigger || c.attachedRigidbody != rb) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            return b;
        }
    }
}
