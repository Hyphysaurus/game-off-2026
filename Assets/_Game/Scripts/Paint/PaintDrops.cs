using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// Everything of the paint that is in the air: the drops thrown off a break (flown here with gravity and air drag,
    /// each step checked with a ray, drawn as one particle system) and the quick bursts of bits (shell, paint mist,
    /// the Colour Corner's confetti, a dart's bits, the ring where a shot lands on something with health). A drop that
    /// meets something is handed back to the keeper to leave its spot.
    /// </summary>
    public sealed class PaintDrops
    {
        private struct Drop
        {
            public Vector3 Position, Velocity;
            public float Age, Diameter, Drag;
            public Paint Paint;
        }

        private readonly PaintSettings _settings;
        private readonly List<Drop> _drops = new List<Drop>();
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private readonly ParticleSystem _beads, _bits, _floats;
        private ParticleSystem.Particle[] _shown = new ParticleSystem.Particle[0];

        public PaintDrops(PaintSettings settings, Transform parent)
        {
            _settings = settings;
            _beads = MakeSystem("Drops in the air", parent, settings.dropMaterial, Mathf.Max(1, settings.mostDropsInAir), false);
            _bits = MakeSystem("Bits", parent, settings.bitsMaterial, Mathf.Max(1, settings.mostBits), true);
            // Bits that do not fall (the puff at a gun's barrel) fly in a system of their own: a system has one pull for
            // every bit in it, and a puff must not stop a burst's bits falling.
            _floats = MakeSystem("Bits that do not fall", parent, settings.bitsMaterial, Mathf.Max(1, settings.mostBits), true);
        }

        /// <summary>Bits in the air now (bursts and puffs).</summary>
        public int BitsInAir => (_bits != null ? _bits.particleCount : 0) + (_floats != null ? _floats.particleCount : 0);

        /// <summary>Drops in the air now.</summary>
        public int InAir => _drops.Count;

        /// <summary>Throws one drop. False when the air is full.</summary>
        public bool Throw(Paint paint, Vector3 position, Vector3 velocity, float diameter)
        {
            if (_drops.Count >= _settings.mostDropsInAir) return false;
            // The air slows a drop by drag x speed x speed, the less the bigger it is.
            float drag = 3f * _settings.airDensity * _settings.dragCoefficient / (4f * _settings.fillDensity * Mathf.Max(diameter, 1e-6f));
            _drops.Add(new Drop { Position = position, Velocity = velocity, Diameter = diameter, Drag = drag, Paint = paint });
            return true;
        }

        /// <summary>Moves every drop one step under gravity and air drag. A drop that meets something leaves its spot
        /// (through <paramref name="keeper"/>); one that flies too long is dropped.</summary>
        public void Fly(float dt, PaintSplats keeper)
        {
            int i = 0;
            while (i < _drops.Count)
            {
                Drop drop = _drops[i];
                Vector3 velocity = drop.Velocity + Vector3.down * (_settings.dropGravity * drop.Paint.Scale * dt);
                // Drag stepped as v / (1 + k |v| dt), which never overshoots however fast the drop is.
                velocity /= 1f + drop.Drag * velocity.magnitude * dt;
                Vector3 step = velocity * dt;
                float distance = step.magnitude;
                int best = -1;
                if (distance > 1e-6f)
                {
                    int count = Physics.RaycastNonAlloc(drop.Position, step / distance, _hits, distance, _settings.hitMask, QueryTriggerInteraction.Ignore);
                    for (int h = 0; h < count; h++)
                    {
                        if (drop.Paint.IsOwner(_hits[h].transform)) continue;
                        if (best < 0 || _hits[h].distance < _hits[best].distance) best = h;
                    }
                }
                drop.Age += dt;
                if (best < 0 && drop.Age < _settings.dropFlight)
                {
                    drop.Position += step; drop.Velocity = velocity;
                    _drops[i] = drop;
                    i++;
                    continue;
                }
                if (best >= 0) keeper.LandDrop(drop.Paint, _hits[best].collider, _hits[best].point, _hits[best].normal, velocity, drop.Diameter);
                // Order does not matter here: the last drop takes this one's place.
                _drops[i] = _drops[_drops.Count - 1];
                _drops.RemoveAt(_drops.Count - 1);
            }
            Show();
        }

        /// <summary>A quick burst of small round bits from a point, within a cone round <paramref name="way"/>.
        /// <paramref name="size"/> is a bit's radius.</summary>
        public void Puff(Vector3 point, Vector3 way, Color colour, int amount, float speed, float size, float gravity, float seconds)
        {
            ParticleSystem system = gravity != 0f ? _bits : _floats;
            if (amount <= 0 || system == null) return;
            if (gravity != 0f)
            {
                var main = system.main;
                float pull = Mathf.Abs(Physics.gravity.y);
                main.gravityModifier = pull > 1e-4f ? gravity / pull : 0f;
            }
            if (way.sqrMagnitude < 1e-6f) way = Vector3.up;
            way.Normalize();
            Vector3 side = Vector3.Cross(way, Mathf.Abs(way.y) > 0.99f ? Vector3.right : Vector3.up).normalized;
            var bit = new ParticleSystem.EmitParams { startColor = colour, startLifetime = seconds };
            for (int i = 0; i < amount; i++)
            {
                Vector3 axis = Quaternion.AngleAxis(Random.Range(0f, 360f), way) * side;
                Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, _settings.burstCone), axis) * way;
                bit.position = point;
                bit.velocity = dir * (speed * Random.Range(_settings.burstLeast.x, 1f));
                bit.startSize = 2f * size * Random.Range(_settings.burstLeast.y, 1f);
                system.Emit(bit, 1);
            }
        }

        /// <summary>A flat ring of bits that widens from a point, across <paramref name="axis"/>: every bit leaves at the
        /// same speed and size, evenly spaced round the ring, and none of them falls, so it reads as one ring growing
        /// and thinning away. <paramref name="size"/> is a bit's radius.</summary>
        public void Ring(Vector3 point, Vector3 axis, Color colour, int amount, float speed, float size, float seconds)
        {
            if (amount <= 0 || seconds <= 0f || _floats == null) return;
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.up;
            axis.Normalize();
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.99f ? Vector3.right : Vector3.up).normalized;
            var bit = new ParticleSystem.EmitParams { startColor = colour, startLifetime = seconds, position = point, startSize = 2f * size };
            float turn = Random.Range(0f, 360f);   // no two rings start at the same bit
            for (int i = 0; i < amount; i++)
            {
                bit.velocity = Quaternion.AngleAxis(turn + 360f * i / amount, axis) * side * speed;
                _floats.Emit(bit, 1);
            }
        }

        /// <summary>Takes every drop and bit out of the air.</summary>
        public void Clear()
        {
            _drops.Clear();
            if (_beads != null) _beads.Clear();
            if (_bits != null) _bits.Clear();
            if (_floats != null) _floats.Clear();
        }

        /// <summary>Draws the drops in the air: one round bead each, in its paint's colour.</summary>
        private void Show()
        {
            if (_beads == null) return;
            if (_shown.Length < _drops.Count) _shown = new ParticleSystem.Particle[Mathf.Max(_drops.Count, _settings.mostDropsInAir)];
            for (int i = 0; i < _drops.Count; i++)
            {
                Drop drop = _drops[i];
                _shown[i].position = drop.Position;
                _shown[i].velocity = Vector3.zero;
                _shown[i].startSize = drop.Diameter;
                _shown[i].startColor = drop.Paint.Fill;
                _shown[i].startLifetime = 1f;
                _shown[i].remainingLifetime = 1f;
            }
            _beads.SetParticles(_shown, _drops.Count);
        }

        /// <summary>A particle system that emits nothing by itself: particles are put in by code. With
        /// <paramref name="shrink"/> each one shrinks to nothing over its life.</summary>
        private ParticleSystem MakeSystem(string name, Transform parent, Material material, int most, bool shrink)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f; main.maxParticles = most; main.gravityModifier = 0f;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            if (shrink)
            {
                var size = system.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (_settings.beadMesh != null) { renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = _settings.beadMesh; }
            renderer.enabled = material != null;   // no material: fly them, draw nothing (never Unity's pink)
            system.Play();
            return system;
        }
    }
}
