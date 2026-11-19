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
    /// Made by Shift Jam > Workshop > Fit Hit Shapes (Debug/HitShapePieces).
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
            TryGetComponent(out _health);
            foreach (Collider shape in shapes)
            {
                if (shape == null || _fitted.Contains(shape)) continue;
                if (shape is MeshCollider mesh && mesh.sharedMesh == null) continue;   // nothing to land on
                shape.excludeLayers = ~0;   // it touches nothing: only casts find it
                _fitted.Add(shape);
            }
            if (_fitted.Count == 0) return;
            GetComponentsInChildren(true, Scratch);
            foreach (Collider c in Scratch) if (!c.isTrigger && !_fitted.Contains(c)) _bodies.Add(c);
            Scratch.Clear();
        }

        private void Start() => _started = true;

        private void OnEnable()
        {
            if (_fitted.Count == 0) return;
            foreach (Collider c in _fitted) Fitted.Add(c);
            foreach (Collider c in _bodies) Bodies.Add(c);
            if (_health != null) { _health.Died += OnDied; _health.Restored += OnRestored; }
            // Health is only known once everything has woken: until then it stands.
            SetLive(!(_started && offWhileDown && _health != null && _health.Dead));
        }

        private void OnDisable()
        {
            foreach (Collider c in _fitted) Fitted.Remove(c);
            foreach (Collider c in _bodies) Bodies.Remove(c);
            if (_health != null) { _health.Died -= OnDied; _health.Restored -= OnRestored; }
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
