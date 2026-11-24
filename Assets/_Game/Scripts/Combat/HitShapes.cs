using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Where shots land on a figure: on shapes fitted to what is drawn, not on the wider shape it walks or is bumped
    /// with. The rule is the Godot test room's (dummy.gd fits a hit box to each part of its model, hitbox.gd); a rigid
    /// model can do better than boxes, so here each drawn mesh is its own hit shape (the model's own mesh collider),
    /// and a dart, a pellet or a paint ball lands on the drawn surface itself and passes where nothing is drawn.
    /// Put it on the thing's root (beside its Damageable, if it has one) and list the fitted colliders.
    /// What it does with them:
    /// - A hit shape touches nothing (it is excluded from every layer's contacts): the player, cars, crates and the
    ///   figure's own walking capsule never feel it. What a thing bumps into stays its other colliders.
    /// - Those other colliders (the capsule, the character controller) are "body only": casts that stand for a shot
    ///   skip them (HitShapes.BodyOnly: Projectile, the paint drops, a blaster's aim).
    /// - Checks that ask "is there room here" skip the hit shapes (HitShapes.IsShape), since nothing is stopped by one.
    /// - While the thing is down (its Damageable has died and not been restored) the hit shapes are off, as the test
    ///   room's are: a shot passes over a figure lying on the floor, as it did before.
    /// With no fitted collider that can be used (a project without the model's pack) it does nothing at all: shots
    /// land on the body's own colliders, as they did before there were hit shapes.
    /// Made by Shift Jam > Workshop > Fit Hit Shapes (Debug/HitShapePieces). A body that only exists at run time (a
    /// toy soldier's living body, D-0187) hands its shapes over with Add, before or after this wakes; they are then
    /// treated exactly like the listed ones.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitShapes : MonoBehaviour
    {
        [SerializeField, Tooltip("The colliders fitted to what is drawn: shots land on these and on nothing else of this thing. A mesh collider with no mesh (the pack is not in the project) is left out.")] private Collider[] shapes = System.Array.Empty<Collider>();
        [SerializeField, Tooltip("Switch the hit shapes off while this thing's health is gone, and on again when it is restored (the test room's rule: nothing to hit on a figure lying down).")] private bool offWhileDown = true;

        private static readonly HashSet<Collider> Fitted = new HashSet<Collider>();
        private static readonly HashSet<Collider> Bodies = new HashSet<Collider>();
        private static readonly List<Collider> Scratch = new List<Collider>();

        private readonly List<Collider> _fitted = new List<Collider>(), _bodies = new List<Collider>();
        private Damageable _health;
        private bool _started;
        private bool _woke;   // a plain field: a script reload in Play keeps it (Awake does not run again)
        [System.NonSerialized] private List<Collider> _early;   // shapes handed over (Add) before Awake: taken as it wakes (D-0187)

        /// <summary>True for a collider that only stands for a body being walked or bumped (the capsule of a figure
        /// that has hit shapes): a shot passes through it.</summary>
        public static bool BodyOnly(Collider collider) => collider != null && Bodies.Contains(collider);

        /// <summary>True for a hit shape: it stops nothing, so a check for room skips it.</summary>
        public static bool IsShape(Collider collider) => collider != null && Fitted.Contains(collider);

        /// <summary>How many hit shapes this thing has that can be used.</summary>
        public int Count => _fitted.Count;
        /// <summary>True while its hit shapes can be hit.</summary>
        public bool Live { get; private set; }

        private void Awake()
        {
            _woke = true;
            TryGetComponent(out _health);
            foreach (Collider shape in shapes) Take(shape);
            if (_early != null)
            {
                foreach (Collider shape in _early) Take(shape);
                _early = null;
            }
            if (_fitted.Count == 0) return;
            FindBodies();
        }

        /// <summary>Makes <paramref name="shape"/> one of the fitted shapes. False when it cannot be used or already is.</summary>
        private bool Take(Collider shape)
        {
            if (shape == null || _fitted.Contains(shape)) return false;
            if (shape is MeshCollider mesh && mesh.sharedMesh == null) return false;   // nothing to land on
            shape.excludeLayers = ~0;   // it touches nothing: only casts find it
            _fitted.Add(shape);
            return true;
        }

        /// <summary>Every other solid collider under it is "body only": shots pass through it.</summary>
        private void FindBodies()
        {
            GetComponentsInChildren(true, Scratch);
            foreach (Collider c in Scratch) if (!c.isTrigger && !_fitted.Contains(c) && !_bodies.Contains(c)) _bodies.Add(c);
            Scratch.Clear();
        }

        /// <summary>
        /// A hit shape made at run time (D-0187: a living body's shapes on its bones), treated from now on exactly as the
        /// listed ones: it touches nothing, shots land on it, it is off while the thing is down. Safe to call before this
        /// part has woken (it is taken as it wakes), again for the same shape (nothing happens), and on a thing that had
        /// no usable shape until now (its walking capsule becomes "body only" from then on).
        /// </summary>
        public void Add(Collider shape)
        {
            if (shape == null) return;
            if (!_woke)
            {
                if (_early == null) _early = new List<Collider>();
                if (!_early.Contains(shape)) _early.Add(shape);
                return;
            }
            bool first = _fitted.Count == 0;
            if (!Take(shape)) return;
            if (_bodies.Remove(shape)) Bodies.Remove(shape);   // it was counted as a body before
            if (first) FindBodies();
            if (!isActiveAndEnabled) return;   // OnEnable registers it with the rest
            Fitted.Add(shape);
            if (first)
            {
                // OnEnable found nothing to do, so this is the first registration: the bodies and the health's word.
                foreach (Collider c in _bodies) Bodies.Add(c);
                Listen();
                Live = !(_started && offWhileDown && _health != null && _health.Dead);
            }
            shape.enabled = Live;
        }

        private void Start() => _started = true;

        private void OnEnable()
        {
            if (_fitted.Count == 0) return;
            foreach (Collider c in _fitted) Fitted.Add(c);
            foreach (Collider c in _bodies) Bodies.Add(c);
            Listen();
            // Health is only known once everything has woken: until then it stands.
            SetLive(!(_started && offWhileDown && _health != null && _health.Dead));
        }

        private void OnDisable()
        {
            foreach (Collider c in _fitted) Fitted.Remove(c);
            foreach (Collider c in _bodies) Bodies.Remove(c);
            if (_health != null) { _health.Died -= OnDied; _health.Restored -= OnRestored; }
            _listening = false;
        }

        // Whether it hears its health's death and restore, so a shape added (Add) before OnEnable, as a script reload in
        // Play can order them, never signs it up twice.
        [System.NonSerialized] private bool _listening;

        private void Listen()
        {
            if (_listening || _health == null) return;
            _health.Died += OnDied; _health.Restored += OnRestored;
            _listening = true;
        }

        private void OnDied(DamageInfo _) { if (offWhileDown) SetLive(false); }
        private void OnRestored() => SetLive(true);

        private void SetLive(bool on)
        {
            Live = on;
            foreach (Collider c in _fitted) if (c != null) c.enabled = on;
        }
    }
}
