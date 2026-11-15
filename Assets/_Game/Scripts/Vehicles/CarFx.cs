using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A car's boost flames and tyre smoke (the Cyber pack's BoostFX and DriftFX, or any particle prefab), its boost and
    /// wind-up sounds and its wind-up key (D-0037). The effects are made at run time as children of the car: flames at the
    /// rear at exhaust height, smoke across the rear axle, sized by the car's length against the pack's demo car (which the
    /// effects were made for) and grown with the car's scale through the hierarchy, as the pack's own CarFxController
    /// does. A plain class: the car makes it in Start and ticks it every frame.
    /// </summary>
    public sealed class CarFx
    {
        private const string Prefix = "CarFx ";
        private const float PackCarLength = 4.36f;   // the Cyber pack's demo car (Car_01), bumper to bumper

        private readonly Transform _car, _key;
        private readonly Vector3 _keyAxis;
        private readonly Quaternion _keyRest;
        private readonly Transform _boostRoot, _driftRoot;
        private readonly Vector3 _boostScale, _driftScale;
        private readonly ParticleSystem[] _boost, _drift;
        private readonly AudioSource _loop, _shot;
        private float _keyAngle;

        public CarFx(Transform car, GameObject boostPrefab, GameObject driftPrefab, Transform windKey, Vector3 windKeyAxis, bool sounds)
        {
            _car = car;
            // A Build Mode copy arrives with the original's effects: drop those and make fresh ones.
            var old = new List<GameObject>();
            foreach (Transform child in car) if (child.name.StartsWith(Prefix)) old.Add(child.gameObject);
            foreach (var go in old) { go.SetActive(false); Object.Destroy(go); }

            _boost = Make(boostPrefab, "Boost", out _boostRoot, out _boostScale);
            _drift = Make(driftPrefab, "Drift", out _driftRoot, out _driftScale);
            if (sounds)
            {
                var audio = new GameObject(Prefix + "Audio");
                audio.transform.SetParent(car, false);
                _loop = Source(audio, true);
                _shot = Source(audio, false);
            }
            _key = windKey;
            _keyAxis = windKeyAxis.sqrMagnitude > 1e-6f ? windKeyAxis.normalized : Vector3.up;
            if (_key != null) _keyRest = _key.localRotation;
        }

        private ParticleSystem[] Make(GameObject prefab, string what, out Transform root, out Vector3 scale)
        {
            root = null; scale = Vector3.one;
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, _car, false);
            go.name = Prefix + what;
            root = go.transform;
            scale = root.localScale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);   // effects never collide
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // grows with the car
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            return systems;
        }

        private static AudioSource Source(GameObject on, bool loop)
        {
            var a = on.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = loop;
            a.spatialBlend = 0.5f;   // half heard as the driver's own, half placed at the car
            a.rolloffMode = AudioRolloffMode.Linear;
            return a;
        }

        /// <summary>The flames go to <paramref name="exhaust"/> and the smoke to <paramref name="rearAxle"/> (car space), both
        /// sized for a car <paramref name="length"/> long (car space units) times <paramref name="fxScale"/>.</summary>
        public void Place(Vector3 exhaust, Vector3 rearAxle, float length, float fxScale)
        {
            float k = Mathf.Max(0.01f, fxScale * length / PackCarLength);
            if (_boostRoot != null) { _boostRoot.localPosition = exhaust; _boostRoot.localRotation = Quaternion.identity; _boostRoot.localScale = _boostScale * k; }
            if (_driftRoot != null) { _driftRoot.localPosition = rearAxle; _driftRoot.localRotation = Quaternion.identity; _driftRoot.localScale = _driftScale * k; }
            float s = Mathf.Abs(_car.lossyScale.y) * Mathf.Max(0.1f, length);
            foreach (var a in new[] { _loop, _shot })
            {
                if (a == null) continue;
                a.minDistance = 2f * s;
                a.maxDistance = 60f * s;
            }
        }

        /// <summary>Per frame: flames while boosting, smoke while sliding on the ground, the spring's creak while it winds,
        /// and the key turning (forward while winding, back while the launch runs).</summary>
        public void Tick(float dt, bool boosting, bool sliding, bool windGrowing, float wind, bool unwinding, float keySpin, AudioClip windClip)
        {
            Switch(_boost, boosting);
            Switch(_drift, sliding);
            if (_loop != null)
            {
                if (windGrowing && windClip != null)
                {
                    if (_loop.clip != windClip) _loop.clip = windClip;
                    _loop.pitch = 0.8f + 0.6f * wind;
                    _loop.volume = 0.7f;
                    if (!_loop.isPlaying) _loop.Play();
                }
                else if (_loop.isPlaying) _loop.Stop();
            }
            if (_key != null && (windGrowing || unwinding))
            {
                _keyAngle = Mathf.Repeat(_keyAngle + (windGrowing ? keySpin : -2f * keySpin) * dt, 360f);
                _key.localRotation = _keyRest * Quaternion.AngleAxis(_keyAngle, _keyAxis);
            }
        }

        /// <summary>A one-shot at the car (a boost's whoosh, the launch).</summary>
        public void Play(AudioClip clip, float volume)
        {
            if (clip == null || _shot == null) return;
            _shot.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Flames, smoke and the creak off now (held, parked, wrecked).</summary>
        public void StopAll()
        {
            Switch(_boost, false);
            Switch(_drift, false);
            if (_loop != null && _loop.isPlaying) _loop.Stop();
        }

        /// <summary>The key back where it started (a reset).</summary>
        public void ResetKey()
        {
            _keyAngle = 0f;
            if (_key != null) _key.localRotation = _keyRest;
        }

        private static void Switch(ParticleSystem[] systems, bool on)
        {
            if (systems == null) return;
            foreach (var ps in systems)
            {
                if (ps == null) continue;
                if (on && !ps.isEmitting) ps.Play(false);
                else if (!on && ps.isEmitting) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
