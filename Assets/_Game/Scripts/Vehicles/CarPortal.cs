using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>The colours portals pair by: any two of the same colour link.</summary>
    public enum PortalColour { Red, Blue, Green }

    /// <summary>
    /// A car portal (D-0039, "we shift in location"): drive through it and come out of its partner at the same speed,
    /// turned to the partner's facing. The partner is the other portal of the same colour, so in Build Mode a red placed
    /// beside another red links to it. Car, driver and chase camera move together (IBoostable.Teleport, ShoulderCamera.Warp).
    /// Entering from the back works too: you come out of the partner's front either way. Cars only; the toy on foot uses
    /// the course's Portal. With three or more of one colour, each sends you on to the next one placed.
    /// The look is his portal drawing as a model (D-0073): the swirl turns, and a warp flashes it and sends the
    /// drawing's small marks outward as a ripple (PortalRipple) at both ends.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CarPortal : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly List<CarPortal> All = new List<CarPortal>();
        private static readonly Dictionary<int, float> Ready = new Dictionary<int, float>();   // car id -> time it may warp again

        [SerializeField] private PortalColour colour;
        [SerializeField, Tooltip("Gap left between the exit's trigger and the car's tail, world units at scale 1.")] private float exitGap = 1f;
        [SerializeField, Tooltip("Seconds before the same car can warp again (any portal), so it never bounces straight back.")] private float cooldown = 0.75f;
        [Header("Look")]
        [SerializeField, Tooltip("A transform to spin (the stand-in hoop's flat swirl). Empty on his drawn portal: that one turns in its material, Data/PortalSwirl.")] private Transform swirl;
        [SerializeField, Tooltip("Degrees a second, for the transform above.")] private float spinSpeed = 90f;
        [SerializeField, Tooltip("The small marks around the drawing: sent outward after a warp, at both ends (D-0073). May be empty.")] private PortalRipple ripple;
        [SerializeField, Tooltip("What brightens on a warp. The swirl's renderers if empty.")] private Renderer[] glow;
        [SerializeField, Tooltip("Share of its colour added at the peak of a flash.")] private float flashBoost = 2.5f;
        [SerializeField] private float flashTime = 0.5f;
        [SerializeField, Tooltip("Optional light, on only while it flashes.")] private Light flashLight;
        [SerializeField] private float lightIntensity = 4f;
        [SerializeField] private AudioClip sound;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField] private UnityEvent onUsed;

        private readonly List<(Renderer r, Color calm)> _slots = new List<(Renderer, Color)>();
        private MaterialPropertyBlock _block;
        private AudioSource _audio;
        private Collider _trigger;
        private float _flashUntil;
        private bool _flashing;

        public PortalColour Colour => colour;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { All.Clear(); Ready.Clear(); }

        private void Reset() { GetComponent<Collider>().isTrigger = true; }

        private void Awake()
        {
            _trigger = GetComponent<Collider>();
            _block = new MaterialPropertyBlock();
            var renderers = glow != null && glow.Length > 0 ? glow : (swirl != null ? swirl.GetComponentsInChildren<Renderer>(true) : new Renderer[0]);
            foreach (var r in renderers)
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId))
                    _slots.Add((r, r.sharedMaterial.GetColor(BaseColorId)));
            if (flashLight != null) { flashLight.enabled = false; flashLight.intensity = 0f; }
        }

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        /// <summary>The portal this one sends to: the next one of the same colour after it, in the order they woke up.</summary>
        public CarPortal Partner()
        {
            int me = All.IndexOf(this);
            if (me < 0) return null;
            for (int step = 1; step < All.Count; step++)
            {
                var p = All[(me + step) % All.Count];
                if (p != null && p != this && p.colour == colour && p.isActiveAndEnabled) return p;
            }
            return null;
        }

        private void OnTriggerEnter(Collider other)
        {
            IBoostable car = BoostableTouch.Find(other, out Rigidbody body);
            if (car == null || body == null || body.isKinematic) return;
            Transform ct = BoostableTouch.TransformOf(car);
            if (ct == null) return;
            int id = ct.GetInstanceID();
            if (Ready.TryGetValue(id, out float readyAt) && Time.time < readyAt) return;
            CarPortal exit = Partner();
            if (exit == null) return;

            // the car's pose and motion in this portal's frame (yaw only: a portal tilted in Build Mode still sends you level)
            Quaternion inRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Quaternion outRot = Quaternion.Euler(0f, exit.transform.eulerAngles.y, 0f);
            Quaternion toLocal = Quaternion.Inverse(inRot);
            Vector3 v = toLocal * body.linearVelocity;
            Vector3 pos = toLocal * (ct.position - transform.position);
            Quaternion facing = toLocal * ct.rotation;
            // through the back: turn it all round, so you still come out of the partner's front
            bool backwards = Mathf.Abs(v.z) > 0.1f ? v.z < 0f : pos.z > 0f;
            if (backwards)
            {
                Quaternion turn = Quaternion.Euler(0f, 180f, 0f);
                v = turn * v; pos = turn * pos; facing = turn * facing;
            }

            // out past the partner's trigger, the car's tail clear of it
            float carHalf = BuildBounds.World(ct.gameObject, out Bounds cb) ? Mathf.Max(cb.extents.x, cb.extents.z) : 2f;
            pos.z = exit.Depth() * 0.5f + carHalf + exitGap * Mathf.Abs(exit.transform.lossyScale.y);
            pos.x = Mathf.Clamp(pos.x, -exit.Width() * 0.5f, exit.Width() * 0.5f);
            Vector3 to = exit.transform.position + outRot * pos;
            Quaternion rot = outRot * facing;
            Vector3 velocity = outRot * v;
            if (v.z < 0.5f) velocity += outRot * Vector3.forward * (0.5f - v.z);   // nudged out, never left standing in the hoop

            if (!car.Teleport(to, rot, velocity)) return;
            Ready[id] = Time.time + cooldown;

            if (car.Driven && Camera.main != null && Camera.main.TryGetComponent(out ShoulderCamera cam))
                cam.Warp(cam.Yaw + Mathf.DeltaAngle(inRot.eulerAngles.y + (backwards ? 180f : 0f), outRot.eulerAngles.y));
            Flash();
            exit.Flash();
            exit.onUsed?.Invoke();
            onUsed?.Invoke();
        }

        /// <summary>The trigger's depth along the portal's facing, world units.</summary>
        private float Depth() => _trigger is BoxCollider box ? box.size.z * Mathf.Abs(transform.lossyScale.z) : 2f;
        /// <summary>The trigger's width across the portal, world units.</summary>
        private float Width() => _trigger is BoxCollider box ? box.size.x * Mathf.Abs(transform.lossyScale.x) : 4f;

        private void Flash()
        {
            _flashUntil = Time.time + flashTime;
            _flashing = true;
            if (flashLight != null) flashLight.enabled = true;
            if (ripple != null) ripple.Play();
            PieceSound.Play(gameObject, ref _audio, sound, volume);
        }

        private void Update()
        {
            if (swirl != null) swirl.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
            if (!_flashing) return;
            float k = Mathf.Clamp01((_flashUntil - Time.time) / Mathf.Max(0.01f, flashTime));   // 1 at the warp, 0 when calm
            foreach (var (r, calm) in _slots)
            {
                if (r == null) continue;
                if (k <= 0f) { r.SetPropertyBlock(null); continue; }
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, calm * (1f + flashBoost * k));
                r.SetPropertyBlock(_block);
            }
            if (flashLight != null)
            {
                flashLight.intensity = lightIntensity * k;
                if (k <= 0f) flashLight.enabled = false;
            }
            if (k <= 0f) _flashing = false;
        }
    }
}
