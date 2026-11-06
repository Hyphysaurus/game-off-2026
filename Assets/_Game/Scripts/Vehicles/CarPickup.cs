using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A pickup a driven car runs through (D-0037, D-0038): a nitro star fills the nitro meter (IBoostable.AddNitro), a
    /// repair heart mends the car (its Damageable), a life ring puts a shield bubble round it (CarShield). Taken, it
    /// disappears and pops back after a while; between pickups it spins and bobs over its pivot (in this object's own
    /// space, so Build Mode's moves, turns and scales carry it). Build Mode, F9 and every Build Mode edit bring a taken one
    /// straight back. Play only; a held or parked car (kinematic) does not take it, and a car with nothing to gain leaves it
    /// for later (a full meter, full health, a car that takes no damage such as the wooden one).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CarPickup : MonoBehaviour, IBuildMoved, IResettable
    {
        public enum Kind { Nitro, Repair, Shield }

        [SerializeField] private Kind kind = Kind.Nitro;
        [SerializeField, Range(0f, 1f), Tooltip("Nitro: added to the meter (full = 1). Repair: share of the car's full health mended.")] private float amount = 0.35f;
        [SerializeField, Tooltip("Seconds until it comes back after being taken.")] private float respawnSeconds = 8f;
        [SerializeField, Tooltip("The trigger cars touch; switched off while it is away. The first trigger on this object if empty.")] private Collider trigger;
        [Header("Shield")]
        [SerializeField, Tooltip("Seconds the bubble lasts.")] private float shieldSeconds = 8f;
        [SerializeField, Tooltip("The life ring that circles the car while the bubble is up.")] private GameObject shieldRing;
        [SerializeField, Tooltip("The bubble's mesh (Unity's sphere).")] private Mesh shieldMesh;
        [SerializeField, Tooltip("The bubble's material.")] private Material shieldBubble;
        [SerializeField, Tooltip("A hit the bubble turns away.")] private AudioClip shieldBlocked;
        [SerializeField, Tooltip("The bubble going.")] private AudioClip shieldEnd;
        [Header("Look")]
        [SerializeField, Tooltip("The model that spins, bobs and hides. Nothing moves if empty.")] private Transform visual;
        [SerializeField, Tooltip("Height of the model's middle above the pivot, in this object's units.")] private float hover = 1.1f;
        [SerializeField, Tooltip("Degrees per second.")] private float spinSpeed = 120f;
        [SerializeField, Tooltip("How far it bobs up and down, in this object's units.")] private float bobHeight = 0.12f;
        [SerializeField, Tooltip("Bobs per second.")] private float bobRate = 0.8f;
        [SerializeField, Tooltip("Seconds it takes to grow back in.")] private float popTime = 0.3f;
        [SerializeField] private AudioClip sound;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField] private UnityEvent onTaken;

        private AudioSource _audio;
        private bool _away;
        private float _backAt, _shownAt = -10f, _spin, _phase;

        public Kind Type => kind;
        /// <summary>False while it is away after being taken.</summary>
        public bool Ready => !_away;

        private void Reset()
        {
            foreach (var c in GetComponents<Collider>()) if (c.isTrigger) return;
            var first = GetComponent<Collider>();
            if (first != null) first.isTrigger = true;
        }

        private void Awake()
        {
            if (trigger == null)
                foreach (var c in GetComponents<Collider>()) if (c.isTrigger) { trigger = c; break; }
            _phase = Random.value * Mathf.PI * 2f;   // a row of pickups does not bob in step
            _spin = Random.value * 360f;
            Show(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_away || GameMode.Current != GameMode.Mode.Play) return;
            var car = BoostableTouch.Find(other, out Rigidbody body);
            if (car == null || body.isKinematic || !car.Driven || !Give(car)) return;
            _away = true;
            _backAt = Time.time + respawnSeconds;
            if (trigger != null) trigger.enabled = false;
            if (visual != null) visual.gameObject.SetActive(false);
            PieceSound.Play(gameObject, ref _audio, sound, volume);
            onTaken?.Invoke();
        }

        /// <summary>What the car gets. False when it has nothing to gain, so the pickup stays for later.</summary>
        private bool Give(IBoostable car)
        {
            switch (kind)
            {
                case Kind.Nitro:
                    if (car.Nitro >= 0.999f) return false;
                    car.AddNitro(amount);
                    return true;
                case Kind.Repair:
                {
                    var owner = car as Component;
                    var health = owner != null ? owner.GetComponent<Damageable>() : null;
                    if (health == null || health.Dead || health.Health >= health.MaxHealth - 0.01f) return false;
                    health.Heal(health.MaxHealth * amount);
                    return true;
                }
                case Kind.Shield:
                {
                    var owner = car as Component;
                    return owner != null && CarShield.Give(owner, shieldSeconds, shieldRing, shieldMesh, shieldBubble, shieldBlocked, shieldEnd) != null;
                }
            }
            return false;
        }

        private void Update()
        {
            if (_away && (Time.time >= _backAt || GameMode.Current == GameMode.Mode.Build)) Show(true);   // Build Mode sees every pickup
            if (_away || visual == null) return;
            _spin = Mathf.Repeat(_spin + spinSpeed * Time.deltaTime, 360f);
            float bob = Mathf.Sin(Time.time * bobRate * Mathf.PI * 2f + _phase) * bobHeight;
            visual.localPosition = Vector3.up * (hover + bob);
            visual.localRotation = Quaternion.Euler(0f, _spin, 0f);
            float grow = popTime > 0f ? Mathf.Clamp01((Time.time - _shownAt) / popTime) : 1f;
            visual.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, grow);
        }

        /// <summary>Back in place and ready: with a pop when it comes back on its own, at once otherwise.</summary>
        private void Show(bool pop)
        {
            _away = false;
            _shownAt = pop ? Time.time : -10f;
            if (trigger != null) trigger.enabled = true;
            if (visual == null) return;
            visual.gameObject.SetActive(true);
            visual.localPosition = Vector3.up * hover;
            visual.localScale = pop ? Vector3.zero : Vector3.one;
        }

        /// <summary>Build Mode placed, moved or scaled it: the pickup is back where it now stands.</summary>
        public void OnBuildMoved() { Show(false); }

        public void ResetState() { Show(false); }
    }
}
