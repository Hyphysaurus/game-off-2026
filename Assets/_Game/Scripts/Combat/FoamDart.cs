using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A foam dart: the blasters' own ammo. It flies like any shot; where it lands it sticks, tip in, for a while, and
    /// rides with what it hit (a target's rocking part, a soldier's tipping body, a loose crate), then lets go and
    /// drops off. Sized to whoever fired it, like every shot.
    /// The sticking is the Godot test room's foam_dart.gd: 8 seconds, the dart's middle 0.15 m back from the point it
    /// hit. Ours, not the test room's: the quiver as it lands, the cap on how many can be stuck at once (the oldest
    /// lets go first), and the short fall as it lets go (the test room's just vanishes).
    /// A stuck dart follows what it hit instead of becoming its child, so a squashed or stretched thing never skews
    /// it, and a thing that is destroyed, switched off, hidden or put away only makes it let go. It has no collider: later
    /// shots and the player pass through it. It does not stick to a player. The reset key and Projectile.ClearAll
    /// put every stuck dart back in the pool at once.
    /// On a figure with hit shapes (HitShapes) it lands on the drawn body itself, so it is stuck in the body and not
    /// in the air in front of it.
    /// What is drawn is a child of the prefab (Shape: the test room's foam dart, a blue foam body with a round orange
    /// head first, made of Unity's plain cylinder and ball); with that child gone the plain capsule is shown instead.
    /// Built by Shift Jam > Workshop > Build Foam Dart.
    /// </summary>
    public sealed class FoamDart : Projectile
    {
        private enum Rest { Flying, Stuck, Falling }

        [Header("Sticking (the test room's foam dart)")]
        [SerializeField, Min(0f), Tooltip("Seconds it stays where it lands (the test room: 8). 0 = it does not stick: it is gone at the hit, as before.")] private float stickSeconds = 8f;
        [SerializeField, Tooltip("How far the dart's middle (this object) sits back from the point it hit, units at scale 1 (the test room: 0.15 m). The shape's nose is a little further forward than this, so it ends up in the surface.")] private float tipLength = 0.1485f;
        [Header("Sticking (ours)")]
        [SerializeField, Min(0f), Tooltip("Degrees it swings about its tip as it lands. 0 = no quiver.")] private float quiverDegrees = 12f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the quiver takes to die away.")] private float quiverSeconds = 0.4f;
        [SerializeField, Min(0f), Tooltip("How many times it swings to and fro in that time.")] private float quiverSwings = 3f;
        [SerializeField, Min(0), Tooltip("The most darts stuck at once; one more and the oldest lets go. 0 = no limit (only their time takes them away).")] private int mostStuck = 40;
        [SerializeField, Min(0f), Tooltip("Seconds a dart takes to drop off when it lets go: it backs out, falls and shrinks to nothing. 0 = it is gone at once.")] private float letGoSeconds = 0.35f;
        [SerializeField, Tooltip("How fast it falls as it lets go, units per second squared at scale 1.")] private float letGoFall = 9f;
        [Header("Shape")]
        [SerializeField, Tooltip("The drawn dart: a child holding its body and its head, the head toward +Z.")] private Transform shape;
        [SerializeField, Tooltip("The plain capsule, shown only when the shape has nothing to draw.")] private GameObject plain;

        private static readonly List<FoamDart> Stuck = new List<FoamDart>();   // every dart stuck now, oldest first

        private Rest _rest;
        private Transform _on;          // what it rides with
        private Renderer _onDrawn;      // that thing's own renderer, if it has one: hidden, the dart lets go
        private Vector3 _tipOn;         // the point it hit, in that thing's own space
        private Quaternion _turnOn;     // the way it points, in that thing's own space
        private Vector3 _quiverAxis;    // the quiver swings about this, in the dart's own space
        private Vector3 _fall;          // its velocity as it drops off
        private float _since;           // when it stuck, or let go
        private bool _dressed;

        /// <summary>Darts stuck in the scene now (those dropping off are not counted).</summary>
        public static int StuckNow { get { Forget(); return Stuck.Count; } }
        /// <summary>True while this dart is stuck in something.</summary>
        public bool IsStuck => _rest == Rest.Stuck;
        /// <summary>What it is stuck in and rides with; null when it is not stuck.</summary>
        public Transform StuckIn => _rest == Rest.Stuck ? _on : null;

        private void OnDestroy() => Stuck.Remove(this);   // its scene closed with it still stuck

        protected override void OnLaunched()
        {
            _rest = Rest.Flying;
            if (_dressed) return;
            _dressed = true;
            // The plain capsule stands in only when the shape has nothing to draw.
            MeshFilter drawn = shape != null ? shape.GetComponentInChildren<MeshFilter>(true) : null;
            if (plain != null) plain.SetActive(drawn == null || drawn.sharedMesh == null);
        }

        protected override bool Stays(in RaycastHit hit, Vector3 point, Vector3 direction)
        {
            if (stickSeconds <= 0f) return false;
            Collider struck = hit.collider;
            // Never on a player: a dart in your own body would hang in front of the first-person view.
            if (struck.GetComponentInParent<PlayerMotor>() != null || struck.GetComponentInParent<FirstPersonBody>() != null) return false;
            // It rides with the drawn part of what it hit, as paint does: a target's rocking pivot, a soldier's body.
            Transform on = PaintSplats.InScene().HolderOf(struck, point);
            Vector3 size = on.lossyScale;
            if (Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) < 1e-6f) return false;   // flattened to nothing one way: there is no place on it
            _on = on;
            on.TryGetComponent(out _onDrawn);
            _tipOn = on.InverseTransformPoint(point);
            _turnOn = Quaternion.Inverse(on.rotation) * Quaternion.LookRotation(direction);
            _quiverAxis = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward) * Vector3.right;
            _rest = Rest.Stuck;
            _since = Time.time;
            Place(0f);
            Forget();
            Stuck.Add(this);
            if (mostStuck > 0) while (Stuck.Count > mostStuck) Stuck[0].LetGo();   // the oldest makes room
            GameEvents.RaiseDartStuck(Owner);
            return true;
        }

        protected override void OnRecycled()
        {
            Stuck.Remove(this);
            _rest = Rest.Flying;
            _on = null; _onDrawn = null;
        }

        private void LateUpdate()
        {
            // After everything has moved this frame: the target's rock, the soldier's step, the crate's tumble.
            if (_rest == Rest.Flying) return;
            float age = Time.time - _since;
            if (_rest == Rest.Stuck)
            {
                bool gone = _on == null || !_on.gameObject.activeInHierarchy || (_onDrawn != null && !_onDrawn.enabled);   // destroyed, switched off, or hidden (a wrecked car's body)
                if (gone || age >= stickSeconds) LetGo();
                else Place(age);
                return;
            }
            float left = 1f - age / letGoSeconds;
            if (left <= 0f) { Recycle(); return; }
            float dt = Time.deltaTime;
            _fall += Vector3.down * (letGoFall * ShotScale * dt);
            transform.position += _fall * dt;
            transform.localScale = FullSize * left;
        }

        /// <summary>Puts the dart where the thing it rides with now is: its tip on the point it hit, pointing the way it
        /// came in, swinging about that tip while the quiver lasts.</summary>
        private void Place(float age)
        {
            Vector3 tip = _on.TransformPoint(_tipOn);
            Quaternion turn = _on.rotation * _turnOn;
            if (quiverDegrees > 0f && age < quiverSeconds)
            {
                float done = age / quiverSeconds, left = 1f - done;
                turn *= Quaternion.AngleAxis(quiverDegrees * left * left * Mathf.Sin(done * quiverSwings * 2f * Mathf.PI), _quiverAxis);
            }
            transform.SetPositionAndRotation(tip - turn * Vector3.forward * (tipLength * ShotScale), turn);
        }

        /// <summary>Its time is up, what it rode with is gone, or a newer dart needs its place: it backs out the way it
        /// went in, falls and shrinks away, then goes back to the pool.</summary>
        private void LetGo()
        {
            Stuck.Remove(this);
            if (letGoSeconds <= 0f) { Recycle(); return; }
            _rest = Rest.Falling;
            _since = Time.time;
            _on = null; _onDrawn = null;
            _fall = -transform.forward * (tipLength * ShotScale / letGoSeconds);   // out by its own tip length while it falls
        }

        /// <summary>Drops the darts that died with a scene from the list.</summary>
        private static void Forget()
        {
            for (int i = Stuck.Count - 1; i >= 0; i--) if (Stuck[i] == null) Stuck.RemoveAt(i);
        }
    }
}
