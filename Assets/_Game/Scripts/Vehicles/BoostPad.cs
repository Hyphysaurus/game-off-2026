using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A boost (D-0037): a car someone drives through this trigger surges (IBoostable.Boost) and its nitro meter fills a
    /// little. Floor arrows, inflatable arches and rings all use it. Each car takes it once per pass (a short per-car wait,
    /// so a car's several colliders count once). The glow renderers brighten, an optional light flashes and a sound plays.
    /// Play only; a held or parked car (kinematic: the cannon, a garage lift, Build Mode) is left alone.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class BoostPad : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Boost")]
        [SerializeField, Tooltip("Top speed times this while the burst lasts.")] private float multiplier = 1.4f;
        [SerializeField, Tooltip("How long the burst lasts, seconds.")] private float seconds = 1.2f;
        [SerializeField, Range(0f, 1f), Tooltip("Added to the car's nitro meter (full = 1).")] private float nitro = 0.15f;
        [SerializeField, Tooltip("Seconds before the same car can take it again.")] private float cooldown = 0.5f;
        [SerializeField, Tooltip("A gliding toy flying through (D-0104): speed added along its flight, its own units a second (it eases back to the glide speed after).")] private float glideBoost = 14f;
        [SerializeField, Tooltip("...and a lift, its own units a second.")] private float glideLift = 4f;
        [Header("Feedback")]
        [SerializeField, Tooltip("What brightens when a car takes it. This object's renderers (and its children's) if empty.")] private Renderer[] glow;
        [SerializeField] private Color flashColour = new Color(0.35f, 0.9f, 1f);
        [SerializeField, Tooltip("Seconds the flash takes to fade.")] private float flashTime = 0.4f;
        [SerializeField, Tooltip("Share of the flash colour added to the surface colour at the peak.")] private float brighten = 0.6f;
        [SerializeField, Tooltip("Share of the flash colour added to the glow at the peak (materials with emission on).")] private float emission = 3f;
        [SerializeField, Tooltip("Optional light, on only while it flashes.")] private Light flashLight;
        [SerializeField] private float lightIntensity = 3f;
        [SerializeField, Tooltip("The light's reach at scale 1, units (it follows the piece's scale; a light's range ignores its transform).")] private float lightRange = 6f;
        [SerializeField] private AudioClip sound;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
        [SerializeField] private UnityEvent onBoost;

        /// <summary>One material slot that flashes, with the colours it has when calm.</summary>
        private struct Slot
        {
            public Renderer renderer;
            public int index;
            public bool hasBase, hasEmission;
            public Color baseColour, emissionColour;
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private readonly Dictionary<int, float> _ready = new Dictionary<int, float>();   // car body id -> time it may boost again
        private MaterialPropertyBlock _block;
        private AudioSource _audio;
        private float _flashUntil;
        private bool _flashing;

        private void Reset() { var c = GetComponent<Collider>(); if (c != null) c.isTrigger = true; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            var renderers = glow != null && glow.Length > 0 ? glow : GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    var s = new Slot { renderer = r, index = i };
                    s.hasBase = m.HasProperty(BaseColorId);
                    s.baseColour = s.hasBase ? m.GetColor(BaseColorId) : Color.white;
                    s.hasEmission = m.HasProperty(EmissionColorId) && m.IsKeywordEnabled("_EMISSION");
                    s.emissionColour = s.hasEmission ? m.GetColor(EmissionColorId) : Color.black;
                    if (s.hasBase || s.hasEmission) _slots.Add(s);
                }
            }
            if (flashLight != null) { flashLight.intensity = 0f; flashLight.enabled = false; }
        }

        private void OnDisable() { if (_flashing) StopFlash(); }

        private void OnTriggerEnter(Collider other)
        {
            if (GameMode.Current != GameMode.Mode.Play) return;
            // a toy on the paper plane takes the ring too (D-0104)
            var glider = other.GetComponentInParent<Glider>();
            if (glider != null && glider.Gliding && glider.TryGetComponent(out PlayerMotor motor))
            {
                int pid = motor.GetInstanceID(); float t = Time.time;
                if (_ready.TryGetValue(pid, out float pat) && t < pat) return;
                _ready[pid] = t + cooldown;
                float scale = Mathf.Max(1e-4f, motor.Scale);
                Vector3 flat = Vector3.ProjectOnPlane(motor.Velocity, Vector3.up);
                Vector3 dir = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.ProjectOnPlane(motor.transform.forward, Vector3.up).normalized;
                motor.Knock(dir * (glideBoost * scale) + Vector3.up * (glideLift * scale));
                Flash();
                PieceSound.Play(gameObject, ref _audio, sound, volume);
                onBoost?.Invoke();
                return;
            }
            var car = BoostableTouch.Find(other, out Rigidbody body);
            if (car == null || body.isKinematic || !car.Driven) return;
            int id = body.GetInstanceID();
            float now = Time.time;
            if (_ready.TryGetValue(id, out float at) && now < at) return;
            _ready[id] = now + cooldown;
            car.Boost(multiplier, seconds);
            if (nitro > 0f) car.AddNitro(nitro);
            Flash();
            PieceSound.Play(gameObject, ref _audio, sound, volume);
            onBoost?.Invoke();
        }

        // ---------- the flash ----------

        private void Flash()
        {
            _flashUntil = Time.time + Mathf.Max(0.01f, flashTime);
            _flashing = true;
            if (flashLight != null)
            {
                flashLight.range = lightRange * Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));   // Build Mode may have scaled it
                flashLight.enabled = true;
            }
            Apply(1f);
        }

        private void Update()
        {
            if (!_flashing) return;
            float k = Mathf.Clamp01((_flashUntil - Time.time) / Mathf.Max(0.01f, flashTime));   // 1 at the start, 0 at the end
            if (k <= 0f) { StopFlash(); return; }
            Apply(k * k);   // quick peak, soft tail
        }

        private void Apply(float k)
        {
            foreach (var s in _slots)
            {
                if (s.renderer == null) continue;
                _block.Clear();
                if (s.hasBase) _block.SetColor(BaseColorId, Add(s.baseColour, flashColour, brighten * k));
                if (s.hasEmission) _block.SetColor(EmissionColorId, Add(s.emissionColour, flashColour, emission * k));
                s.renderer.SetPropertyBlock(_block, s.index);
            }
            if (flashLight != null) flashLight.intensity = lightIntensity * k;
        }

        private void StopFlash()
        {
            _flashing = false;
            foreach (var s in _slots) if (s.renderer != null) s.renderer.SetPropertyBlock(null, s.index);
            if (flashLight != null) { flashLight.intensity = 0f; flashLight.enabled = false; }
        }

        private static Color Add(Color c, Color add, float k) => new Color(c.r + add.r * k, c.g + add.g * k, c.b + add.b * k, c.a);
    }
}
