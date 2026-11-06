using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A spring for cars (D-0037): a car that reaches this trigger is thrown up and on (IBoostable.Launch). It keeps its
    /// speed over the ground, at least a minimum along the pad's arrow (ramps) or along its own way (round pads), and gets
    /// the upward speed that lifts it a height counted in its own car lengths, under the gravity it really falls with:
    /// Unity's for a car body that uses it, else the car's own (the wooden car's ball pulls itself down). The visual
    /// squashes on each throw like the toy's LaunchPad and is left alone between throws, so Build Mode can scale it.
    /// A car still on it is thrown again after the cooldown, so a round pad bounces a car until it drives off.
    /// Play only; a held or parked car (kinematic: the cannon, a garage lift, Build Mode) is left alone.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CarJumpPad : MonoBehaviour, IBuildMoved
    {
        [Header("Throw")]
        [SerializeField, Tooltip("How high it throws, in car lengths above where the car leaves it.")] private float height = 1.5f;
        [SerializeField, Tooltip("A car's length at car scale 1, units (the toy's cars are 3.8).")] private float carLength = 3.8f;
        [SerializeField, Tooltip("Least speed on along the throw, units per second at car scale 1. 0 = straight up, the car keeps its own speed.")]
        private float minForwardSpeed = 12f;
        [SerializeField, Tooltip("On: thrown along this object's forward (a ramp's arrow), and a car coming the other way is left alone. Off: along the way the car is going (round pads).")]
        private bool directional = true;
        [SerializeField, Tooltip("Gravity, units per second squared, for a car whose body does not use Unity's (the wooden car's ball pulls itself down at its own 25).")]
        private float ownGravity = 25f;
        [SerializeField, Tooltip("Also throw cars nobody drives.")] private bool anyCar;
        [SerializeField, Tooltip("Seconds before the same car can be thrown again.")] private float cooldown = 0.5f;
        [Header("Feedback")]
        [SerializeField, Tooltip("The part that squashes. Nothing squashes if empty.")] private Transform visual;
        [SerializeField, Range(0.2f, 1f)] private float squash = 0.7f;
        [SerializeField] private float squashTime = 0.25f;
        [SerializeField] private AudioClip sound;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField] private UnityEvent onLaunch;

        private readonly Dictionary<int, float> _ready = new Dictionary<int, float>();   // car body id -> time it may be thrown again
        private AudioSource _audio;
        private float _squashUntil;
        private Vector3 _baseScale, _shown;   // the visual's own size, and the squashed size last written over it
        private bool _squashing;

        private void Reset() { var c = GetComponent<Collider>(); if (c != null) c.isTrigger = true; }

        private void OnTriggerEnter(Collider other) { Try(other); }
        private void OnTriggerStay(Collider other) { Try(other); }

        private void OnDisable()
        {
            // hidden or deleted mid-bounce (Build Mode): put the visual back at its own size for when it returns
            if (_squashing && visual != null) visual.localScale = _baseScale;
            _squashing = false;
        }

        private void Try(Collider other)
        {
            if (GameMode.Current != GameMode.Mode.Play) return;
            var car = BoostableTouch.Find(other, out Rigidbody body);
            if (car == null || body.isKinematic || (!anyCar && !car.Driven)) return;
            int id = body.GetInstanceID();
            float now = Time.time;
            if (_ready.TryGetValue(id, out float at) && now < at) return;

            float s = BoostableTouch.ScaleOf(car);
            float g = body.useGravity ? -Physics.gravity.y : (car is ToyCar toy ? toy.Gravity : ownGravity);
            if (g <= 0.01f) return;
            float up = Mathf.Sqrt(2f * g * Mathf.Max(0.01f, height * carLength * s));
            Vector3 v = body.linearVelocity;
            if (v.y > up * 0.8f) return;   // already flying up (thrown a moment ago): never twice in one go

            Vector3 planar = new Vector3(v.x, 0f, v.z);
            Vector3 dir;
            if (directional)
            {
                dir = Flat(transform.forward, Vector3.forward);
                if (Vector3.Dot(planar, dir) < -s) return;   // driving down the ramp from its high end
            }
            else if (planar.sqrMagnitude > 0.25f * s * s) dir = planar.normalized;
            else
            {
                var carTransform = BoostableTouch.TransformOf(car);
                dir = Flat(carTransform != null ? carTransform.forward : transform.forward, transform.forward);
            }
            float along = Vector3.Dot(planar, dir), least = minForwardSpeed * s;
            if (along < least) planar += dir * (least - along);

            _ready[id] = now + cooldown;
            car.Launch(planar + Vector3.up * up);
            Squash();
            PieceSound.Play(gameObject, ref _audio, sound, volume);
            onLaunch?.Invoke();
        }

        private static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            if (v.sqrMagnitude > 1e-6f) return v.normalized;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector3.forward;
        }

        // ---------- the squash (as LaunchPad) ----------

        private void Squash()
        {
            if (visual == null) return;
            if (!_squashing) _baseScale = _shown = visual.localScale;   // its size now (Build Mode may have scaled it)
            _squashing = true;
            _squashUntil = Time.time + squashTime;
        }

        private void Update()
        {
            if (!_squashing) return;
            if (visual == null) { _squashing = false; return; }
            float k = Mathf.Clamp01((_squashUntil - Time.time) / Mathf.Max(0.01f, squashTime));   // 1 right after the throw, 0 after squashTime
            if (k <= 0f) { visual.localScale = _baseScale; _squashing = false; return; }
            float sq = Mathf.Lerp(1f, squash, Mathf.Sin(k * Mathf.PI));
            _shown = new Vector3(_baseScale.x * (2f - sq), _baseScale.y * sq, _baseScale.z * (2f - sq));
            visual.localScale = _shown;
        }

        /// <summary>Build Mode moved or scaled the pad: the size its visual has now is its own size.</summary>
        public void OnBuildMoved()
        {
            if (visual == null) return;
            if (_squashing)
            {
                // mid-bounce: take the squash back out of whatever size Build Mode left, and stop there
                Vector3 now = visual.localScale;
                visual.localScale = new Vector3(Unsquash(now.x, _baseScale.x, _shown.x), Unsquash(now.y, _baseScale.y, _shown.y), Unsquash(now.z, _baseScale.z, _shown.z));
                _squashing = false;
            }
            _baseScale = visual.localScale;
        }

        private static float Unsquash(float now, float own, float shown) => Mathf.Abs(shown) > 1e-6f ? now * own / shown : now;
    }
}
