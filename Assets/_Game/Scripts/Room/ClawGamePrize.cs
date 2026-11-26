using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// One prize of the claw machine (D-0229): a mini monster of D-0223 under Room/Bedroom/ClawPrizes, as it is in the case
    /// (its DarkMonster in Stay Put: idling in the dark, frozen in the light), held by the claw, on its way out, or won.
    /// Won, it is a plain toy: its DarkMonster switched off and its Animator held in its kind's rest pose (it no longer
    /// idles), its walking capsule off and a capsule collider of the same size with a Rigidbody in its place, a Carryable
    /// (the kid's grab and first person's hands take it), and no hits taken. Put back in the case (New Game, another save
    /// slot) it is exactly as it was: its parent, place, turn and size, its own components on, ours gone.
    /// Not a component: ClawGame keeps one for each prize it finds at run time (nothing is added to the prizes in the scene).
    /// </summary>
    internal sealed class ClawGamePrize
    {
        public enum State { InCase, Held, Falling, Out }

        public readonly Transform Root;
        public readonly DarkMonster Monster;
        public readonly string Key, Name;

        private readonly CharacterController _body;
        private readonly Animator _animator;
        private readonly Damageable _health;
        private readonly Transform _parent;
        private readonly Vector3 _position, _scale;
        private readonly Quaternion _rotation;
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private readonly List<Renderer> _found = new List<Renderer>();
        private readonly List<Collider> _offColliders = new List<Collider>();
        private CapsuleCollider _capsule;
        private Rigidbody _rb;
        private Carryable _carry;

        public State Now { get; private set; } = State.InCase;
        /// <summary>Its rigidbody while it is a toy, else null.</summary>
        public Rigidbody Body => _rb;

        public ClawGamePrize(DarkMonster monster, string key, string name)
        {
            Monster = monster;
            Root = monster.transform;
            Key = key; Name = name;
            _body = monster.GetComponent<CharacterController>();
            _animator = monster.GetComponentInChildren<Animator>(true);
            _health = monster.GetComponent<Damageable>();
            _parent = Root.parent;
            _position = Root.localPosition; _rotation = Root.localRotation; _scale = Root.localScale;
        }

        /// <summary>A walking capsule's box in the world, from its own numbers (it stands upright whatever the turn of its
        /// object, as a CharacterController does). Worked out, not read from the physics: right in the editor too.</summary>
        public static Bounds CapsuleBox(CharacterController cc)
        {
            Transform t = cc.transform;
            Vector3 s = t.lossyScale;
            float radius = cc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
            float tall = Mathf.Max(cc.height * Mathf.Abs(s.y), 2f * radius);
            return new Bounds(t.TransformPoint(cc.center), new Vector3(2f * radius, tall, 2f * radius));
        }

        /// <summary>Its box in the world now: its walking capsule's while that is on (it was sized to the baked body), else
        /// what it draws.</summary>
        public Bounds WorldBounds
        {
            get
            {
                if (_body != null && _body.enabled) return CapsuleBox(_body);
                if (_capsule != null && _capsule.enabled) return _capsule.bounds;
                Root.GetComponentsInChildren(false, _found);
                bool any = false; var b = new Bounds(Root.position, Vector3.zero);
                foreach (Renderer r in _found)
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                return b;
            }
        }

        /// <summary>Its walking capsule's height and radius in the world (or its box's when it has none).</summary>
        public void Size(out float tall, out float radius)
        {
            if (_body != null)
            {
                float s = Mathf.Abs(Root.lossyScale.y);
                tall = Mathf.Max(_body.height, 2f * _body.radius) * s;
                radius = _body.radius * s;
                return;
            }
            Bounds b = WorldBounds;
            tall = b.size.y; radius = 0.5f * Mathf.Max(b.size.x, b.size.z);
        }

        /// <summary>Taken by the claw: it hangs from the claw's head (swings and rises with it) until let go.</summary>
        public void Hold(Transform head)
        {
            Now = State.Held;
            Root.SetParent(head, true);
        }

        /// <summary>Let go by the claw (over the chute, or a slip): back under its own parent where it is now.</summary>
        public void LetGo(bool falling)
        {
            Root.SetParent(_parent, true);
            Now = falling ? State.Falling : State.InCase;
        }

        /// <summary>Where it stood in the case (world), to drop a slipped prize back onto.</summary>
        public Vector3 HomePosition => _parent != null ? _parent.TransformPoint(_position) : _position;
        public Quaternion HomeRotation => _parent != null ? _parent.rotation * _rotation : _rotation;

        /// <summary>Back in the case exactly as it was: its parent, place, turn and size, its own components on, ours gone,
        /// drawn again. Its DarkMonster takes up the room's light rule again on its next frame.</summary>
        public void BackInCase()
        {
            RemoveToy();
            Root.SetParent(_parent, false);
            Root.localPosition = _position; Root.localRotation = _rotation; Root.localScale = _scale;
            if (_body != null) _body.enabled = true;
            if (Monster != null) Monster.enabled = true;
            Show(true);
            Now = State.InCase;
        }

        /// <summary>A plain toy now (won): no longer a monster (switched off, held in its kind's rest pose), a capsule and a
        /// Rigidbody in place of its walking capsule (kinematic until <see cref="Loose"/>), a Carryable, no hits taken.</summary>
        public void MakeToy(float mass)
        {
            if (_rb != null) return;
            if (Monster != null) Monster.enabled = false;   // its OnDisable puts its Animator back on and its eyes out
            if (_health != null) _health.DamageScale = 0f;   // a toy, not a monster: hits are turned away
            RestPose();
            _offColliders.Clear();
            foreach (Collider c in Root.GetComponentsInChildren<Collider>(false))
                if (c.enabled && !c.isTrigger) { c.enabled = false; _offColliders.Add(c); }
            _capsule = Root.gameObject.AddComponent<CapsuleCollider>();
            if (_body != null)
            {
                _capsule.center = _body.center; _capsule.radius = _body.radius; _capsule.height = Mathf.Max(_body.height, 2f * _body.radius);
            }
            else
            {
                Bounds b = WorldBounds;
                float s = Mathf.Max(1e-4f, Mathf.Abs(Root.lossyScale.y));
                _capsule.center = Root.InverseTransformPoint(b.center); _capsule.radius = 0.5f * Mathf.Max(b.size.x, b.size.z) / s; _capsule.height = b.size.y / s;
            }
            _capsule.direction = 1;
            _rb = Root.gameObject.AddComponent<Rigidbody>();
            _rb.mass = mass;
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.None;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _carry = Root.gameObject.AddComponent<Carryable>();
            Now = State.Falling;
        }

        /// <summary>Given to the physics: it falls, rolls and settles, and the kid can pick it up.</summary>
        public void Loose(Vector3 velocity, Vector3 spin)
        {
            if (_rb == null) return;
            Physics.SyncTransforms();
            _rb.isKinematic = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = spin;
            _rb.WakeUp();
            Now = State.Out;
        }

        /// <summary>Won on an earlier play (a reload): a toy standing at <paramref name="at"/> (its feet), turned
        /// <paramref name="turn"/>, left to the physics.</summary>
        public void PlaceOut(Vector3 at, Quaternion turn, float mass)
        {
            Root.SetParent(_parent, true);
            Root.localScale = _scale;
            MakeToy(mass);
            Root.SetPositionAndRotation(at, turn);
            Show(true);
            Loose(Vector3.zero, Vector3.zero);
        }

        /// <summary>Drawn (true) or not (inside the machine, on its way to the chute's flap). Only what it drew is hidden,
        /// and only that is shown again.</summary>
        public void Show(bool on)
        {
            if (on)
            {
                foreach (Renderer r in _hidden) if (r != null) r.forceRenderingOff = false;
                _hidden.Clear();
                return;
            }
            Root.GetComponentsInChildren(false, _found);
            foreach (Renderer r in _found)
                if (r.enabled && !r.forceRenderingOff) { r.forceRenderingOff = true; _hidden.Add(r); }
        }

        /// <summary>The kind's rest pose (MonsterSettings: the pose a frozen monster takes in the light), then the Animator
        /// off so it stays: the toy no longer idles.</summary>
        private void RestPose()
        {
            if (_animator == null) return;
            if (!_animator.enabled) _animator.enabled = true;
            _animator.speed = 1f;
            MonsterSettings.Kind kind = Monster != null ? MonsterSettings.Load().Find(Monster.Kind) : null;
            if (kind != null && _animator.isInitialized)
            {
                int state = StateHash(kind.rest);
                if (state == 0) state = StateHash(kind.idle);
                if (state != 0) { _animator.Play(state, 0, kind.restAt); _animator.Update(0f); }
            }
            _animator.enabled = false;
        }

        /// <summary>The state's hash when layer 0 has it (by its own name, or under the layer's name), else 0 (as DarkMonster
        /// looks its states up).</summary>
        private int StateHash(string state)
        {
            if (string.IsNullOrEmpty(state)) return 0;
            int hash = Animator.StringToHash(state);
            if (_animator.HasState(0, hash)) return hash;
            int full = Animator.StringToHash(_animator.GetLayerName(0) + "." + state);
            return _animator.HasState(0, full) ? full : 0;
        }

        /// <summary>Our toy parts gone and its own colliders back: the rigidbody is stilled and taken out of the physics at
        /// once (the destroy waits for the frame's end), so a grab or a hand holding it lets go.</summary>
        private void RemoveToy()
        {
            if (_rb != null)
            {
                if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
                _rb.isKinematic = true;
                _rb.detectCollisions = false;
                Object.Destroy(_rb);
            }
            foreach (GrabSettle settle in Root.GetComponents<GrabSettle>()) Object.Destroy(settle);
            if (_carry != null) Object.Destroy(_carry);
            if (_capsule != null) { _capsule.enabled = false; Object.Destroy(_capsule); }
            foreach (Collider c in _offColliders) if (c != null) c.enabled = true;
            _offColliders.Clear();
            _rb = null; _carry = null; _capsule = null;
            if (_animator != null && !_animator.enabled) _animator.enabled = true;
            if (_health != null) _health.DamageScale = 1f;   // its DarkMonster sets its own again on its next frame
        }
    }
}
