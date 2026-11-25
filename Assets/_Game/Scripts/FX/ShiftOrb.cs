using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The shift orb's behaviour: a calm crystal that stirs when someone comes near and strikes when it is used.
    /// It knows nothing about the game's rules: whatever owns it calls SetAwake, SetViewer and Strike (in Shift Jam a
    /// small link beside the ShiftSource; in the lab, the viewer). It drives the looks: the crystal's shader values
    /// (through a property block, so the material asset is never written), the halo, the light and the bolts.
    ///   Rest    slow swirl, a faint flash of inner lightning now and then, a slow bob.
    ///   Near    the swirl quickens and the inner lightning flashes often.
    ///   Strike  the crystal flares white (when it is used).
    ///   Bolts   his yellow bolts burst around it for a moment (when the shift it started has finished).
    ///   Dormant dark glass with an ember (SetAwake(false)).
    ///   Asleep  a dormant core that can still be woken (SetSleeping(true), D-0214) gives itself away in the dark: the glass,
    ///           the halo and a small light breathe slowly, and a quiet loop hums close by (a 3D sound with a short reach
    ///           that grows with the viewer's size, as Near Range does). A spent core is dormant but not asleep: silent.
    /// The yellow bolts are no longer part of the orb's own lightning (his call, 16 Nov: they clash with the cyan);
    /// they are the full stop at the end of a shift, played by the BoltBurst on the bolt ring.
    /// </summary>
    public sealed class ShiftOrb : MonoBehaviour
    {
        [Header("Parts (set by Shift Jam > Dev > Build Shift Orb)")]
        [SerializeField] private Renderer crystal;
        [SerializeField, Tooltip("The inside-out black shell from the model. Off by default: a bright edge suits glass better.")] private GameObject inkOutline;
        [SerializeField, Tooltip("The ring that holds the yellow bolts. Its BoltBurst plays them (added at run time if the prefab has none).")] private Transform boltRing;
        [SerializeField, Tooltip("What bobs up and down: the crystal, the bolts and the halo.")] private Transform floating;
        [SerializeField] private Renderer halo;
        [SerializeField] private Light glowLight;

        [Header("Look")]
        [SerializeField, ColorUsage(false)] private Color deepColor = new Color(0.235f, 0.051f, 0.361f);
        [SerializeField, ColorUsage(false, true)] private Color glowColor = new Color(0.25f, 1.5f, 1.9f);
        [SerializeField, ColorUsage(false, true)] private Color sparkColor = new Color(2f, 1.7f, 0.25f);
        [SerializeField, Tooltip("Show the black ink ring around the crystal.")] private bool showInkOutline;

        [Header("At rest")]
        [SerializeField, Tooltip("How far it bobs, in its own units.")] private float bobHeight = 0.12f;
        [SerializeField, Tooltip("Bobs per second.")] private float bobRate = 0.3f;
        [SerializeField, Tooltip("Seconds between flashes of inner lightning.")] private float restFlashEvery = 4f;

        [Header("When the player is near")]
        [SerializeField, Tooltip("Distance at which a toy-sized viewer starts to stir it.")] private float nearRange = 16f;
        [SerializeField, Tooltip("How much of the viewer's size is added to that distance (the kid is 35 times the toy).")] private float rangePerSize = 0.25f;
        [SerializeField, Tooltip("Seconds between flashes when the player is right beside it.")] private float nearFlashEvery = 0.6f;
        [SerializeField, Tooltip("Extra swirl speed when the player is right beside it.")] private float nearSwirl = 1.4f;

        [Header("The strike (when used) and the bolts (when the shift ends)")]
        [SerializeField, Tooltip("How white the crystal goes when it is used.")] private float flareStrength = 0.95f;
        [SerializeField, Tooltip("Seconds the white takes to fade.")] private float flareTime = 0.4f;
        [SerializeField, Tooltip("Seconds the bolts stay out.")] private float strikeTime = 0.55f;
        [SerializeField, Tooltip("Bolt size at the start and end of the burst (1 = as modelled; below 0.76 they start inside the glass).")] private Vector2 strikeReach = new Vector2(0.8f, 1.3f);

        [Header("Asleep: a dormant core gives itself away (D-0214)")]
        [SerializeField, Min(0.01f), Tooltip("Slow breaths a second while asleep.")] private float sleepPulseRate = 0.45f;
        [SerializeField, Range(0f, 1f), Tooltip("How far the dark glass lifts toward its awake look at the top of a breath. 0 = the glass does not breathe.")] private float sleepCrystal = 0.12f;
        [SerializeField, Min(0f), Tooltip("How much brighter the halo is at the top of a breath, over its dormant glow (1.6 = 2.6 times). 0 = it does not breathe.")] private float sleepHalo = 1.6f;
        [SerializeField, Min(0f), Tooltip("The orb's light at the top of a breath while asleep (its awake light is the prefab's own). 0 = no light.")] private float sleepLight = 1.5f;
        [SerializeField, Tooltip("A quiet loop while asleep. Empty = silent (the maker fills it once: the Playroom pack's Star_Light_Glow_Loop, 5.44 s).")] private AudioClip sleepHum;
        [SerializeField, Range(0f, 1f), Tooltip("The hum's loudness right beside the orb.")] private float sleepHumVolume = 0.5f;
        [SerializeField, Min(0.01f), Tooltip("Within this distance of a toy-sized listener the hum is at full loudness (grows with the viewer's size, as Near Range).")] private float sleepHumNear = 2f;
        [SerializeField, Min(0.1f), Tooltip("Past this distance from a toy-sized listener the hum is silent (a short reach; the camera hangs 6 behind a toy). Grows with the viewer's size, as Near Range.")] private float sleepHumReach = 14f;

        private static readonly int DeepId = Shader.PropertyToID("_DeepColor");
        private static readonly int GlowId = Shader.PropertyToID("_GlowColor");
        private static readonly int SparkId = Shader.PropertyToID("_SparkColor");
        private static readonly int AwakeId = Shader.PropertyToID("_Awake");
        private static readonly int FlareId = Shader.PropertyToID("_Flare");
        private static readonly int LightningId = Shader.PropertyToID("_Lightning");
        private static readonly int SeedId = Shader.PropertyToID("_LightningSeed");
        private static readonly int SwirlId = Shader.PropertyToID("_SwirlPhase");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Transform _viewer;
        private float _viewerSize = 1f;
        private bool _wantAwake = true;
        private MaterialPropertyBlock _block, _haloBlock;
        private Vector3 _floatHome;
        private float _lightHome;
        private float _awake = 1f, _stir, _swirl, _lightning, _seed, _flare;
        private float _flashLeft, _nextFlash, _echo = -1f;
        private BoltBurst _burst;
        private bool _wantSleeping;               // told by the core: dormant and can still be woken
        private float _sleep, _breathPhase;       // 0 awake .. 1 asleep, eased; where in its breath it is
        private AudioSource _hum;
        private float _humSize = -1f;             // the viewer size the hum's reach was last set for

        private void Awake()
        {
            if (floating != null) _floatHome = floating.localPosition;
            if (glowLight != null) _lightHome = glowLight.intensity;
            _awake = _wantAwake ? 1f : 0f;
            _nextFlash = Random.Range(0.5f, restFlashEvery);
            _breathPhase = Random.value;   // sleeping cores in one room do not breathe in step
            // TryGetComponent, since ?? cannot see Unity's fake null. The burst hides the bolts when it wakes.
            if (boltRing != null && !boltRing.TryGetComponent(out _burst)) _burst = boltRing.gameObject.AddComponent<BoltBurst>();
        }

        private void OnEnable()
        {
            if (inkOutline != null) inkOutline.SetActive(showInkOutline);
            Paint();
        }

        /// <summary>Awake (lit, usable) or dormant (dark glass with an ember). It fades between the two.</summary>
        public void SetAwake(bool awake) => _wantAwake = awake;

        /// <summary>Dormant and can still be woken (D-0214): it breathes and hums while also not awake. A core that is spent
        /// says false: dark and silent.</summary>
        public void SetSleeping(bool sleeping) => _wantSleeping = sleeping;

        private void OnDisable()
        {
            if (_hum != null && _hum.isPlaying) _hum.Stop();
        }

        /// <summary>Who the orb stirs for, and how big they are (1 = the toy; the kid is 35). Without one it watches the main camera.</summary>
        public void SetViewer(Transform viewer, float size = 1f)
        {
            _viewer = viewer;
            _viewerSize = Mathf.Max(size, 1f);
        }

        private void OnValidate()
        {
            if (inkOutline != null && inkOutline.activeSelf != showInkOutline) inkOutline.SetActive(showInkOutline);
            if (crystal != null) Paint();
        }

        /// <summary>This orb's glow colour (edge, mist, lightning, halo, light). The Pocket Core uses it to stay amber.</summary>
        public void SetGlow(Color glow) => glowColor = glow;

        /// <summary>The discharge: the crystal flares white and the lightning inside flashes. Call it when the orb is used.</summary>
        [ContextMenu("Strike")]
        public void Strike()
        {
            _flare = flareStrength;
            _flashLeft = 0.12f;
            _seed = Random.value;
        }

        /// <summary>His yellow bolts burst out around the orb for a moment. Call it when the shift the orb started has finished.</summary>
        [ContextMenu("Bolts")]
        public void Bolts()
        {
            if (_burst != null) _burst.Play(strikeTime, strikeReach);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool awake = _wantAwake;
            _awake = Mathf.MoveTowards(_awake, awake ? 1f : 0f, dt * 2f);

            _stir = Mathf.MoveTowards(_stir, awake ? Nearness() : 0f, dt * 2.5f);
            _swirl += dt * nearSwirl * _stir;

            Flashes(dt, awake);
            _flare = Mathf.MoveTowards(_flare, 0f, dt * flareStrength / Mathf.Max(flareTime, 0.05f));

            _sleep = Mathf.MoveTowards(_sleep, _wantSleeping && !awake ? 1f : 0f, dt * 2f);
            _breathPhase = Mathf.Repeat(_breathPhase + dt * sleepPulseRate, 1f);
            Hum();

            if (floating != null)
                floating.localPosition = _floatHome + Vector3.up * (Mathf.Sin(Time.time * bobRate * Mathf.PI * 2f) * bobHeight);
            Paint();
        }

        /// <summary>0 out of breath, 1 at the top of a breath, times how asleep it is (D-0214).</summary>
        private float Breath => _sleep * (0.5f - 0.5f * Mathf.Cos(_breathPhase * Mathf.PI * 2f));

        /// <summary>The sleeping hum (D-0214): a looping 3D sound on this orb, as loud as it is asleep, its reach grown with the
        /// viewer's size (a kid hears it from as far as a toy would, measured in their own sizes). Made the first time it is needed.</summary>
        private void Hum()
        {
            if (sleepHum == null) return;
            if (_sleep <= 0f)
            {
                if (_hum != null && _hum.isPlaying) _hum.Stop();
                return;
            }
            if (_hum == null)
            {
                _hum = gameObject.AddComponent<AudioSource>();
                _hum.playOnAwake = false;
                _hum.loop = true;
                _hum.spatialBlend = 1f;
                _hum.rolloffMode = AudioRolloffMode.Linear;   // silent at its reach: a short, sure edge
                _hum.dopplerLevel = 0f;
                _hum.priority = 200;                         // under the game's own sounds when voices run short
            }
            if (_hum.clip != sleepHum) { _hum.Stop(); _hum.clip = sleepHum; }
            if (!Mathf.Approximately(_humSize, _viewerSize))
            {
                _humSize = _viewerSize;
                float grow = 1f + rangePerSize * (_viewerSize - 1f);
                _hum.maxDistance = sleepHumReach * grow;
                _hum.minDistance = Mathf.Min(sleepHumNear * grow, _hum.maxDistance * 0.9f);
            }
            _hum.volume = sleepHumVolume * _sleep;
            if (!_hum.isPlaying)
            {
                _hum.Play();
                if (sleepHum.samples > 0) _hum.timeSamples = Random.Range(0, sleepHum.samples);   // cores in one room do not hum in step
            }
        }

        /// <summary>0 when the viewer is out of range, 1 right beside the orb. The range grows with the viewer's size.</summary>
        private float Nearness()
        {
            Transform who = _viewer;
            if (who == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return 0f;
                who = cam.transform;
            }
            float range = nearRange + rangePerSize * (_viewerSize - 1f) * nearRange;
            float distance = Vector3.Distance(who.position, transform.position);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(range * 0.4f, range, distance));
        }

        /// <summary>The inner lightning: hard on for a few frames, then gone. Sometimes a second flash follows the first.</summary>
        private void Flashes(float dt, bool awake)
        {
            if (_flashLeft > 0f)
            {
                _flashLeft -= dt;
                _lightning = 1f;
            }
            else _lightning = Mathf.MoveTowards(_lightning, 0f, dt * 14f);

            if (!awake) return;
            if (_echo >= 0f)
            {
                _echo -= dt;
                if (_echo < 0f) Flash();
            }
            _nextFlash -= dt;
            if (_nextFlash > 0f) return;
            Flash();
            if (Random.value < 0.35f) _echo = Random.Range(0.08f, 0.16f);
            _nextFlash = Mathf.Lerp(restFlashEvery, nearFlashEvery, _stir) * Random.Range(0.5f, 1.5f);
        }

        private void Flash()
        {
            _flashLeft = Random.Range(0.05f, 0.12f);
            _seed = Random.value;
        }

        /// <summary>Pushes this orb's colours and state to the crystal, the halo and the light.</summary>
        private void Paint()
        {
            if (crystal != null)
            {
                _block ??= new MaterialPropertyBlock();
                _block.SetColor(DeepId, deepColor);
                _block.SetColor(GlowId, glowColor);
                _block.SetColor(SparkId, sparkColor);
                _block.SetFloat(AwakeId, Mathf.Clamp01(_awake + sleepCrystal * Breath));   // asleep: the glass breathes (D-0214)
                _block.SetFloat(FlareId, _flare);
                _block.SetFloat(LightningId, _lightning);
                _block.SetFloat(SeedId, _seed);
                _block.SetFloat(SwirlId, _swirl);
                crystal.SetPropertyBlock(_block);
            }

            // The halo and the light take the glow colour at ordinary brightness, dimmed while dormant.
            float peak = Mathf.Max(glowColor.r, glowColor.g, glowColor.b, 0.001f);
            Color tint = new Color(glowColor.r / peak, glowColor.g / peak, glowColor.b / peak, 1f);
            float breath = Breath;
            float lift = Mathf.Lerp(0.15f, 1f, _awake) * (1f + 0.5f * _lightning + _flare) * (1f + sleepHalo * breath);
            if (halo != null)
            {
                _haloBlock ??= new MaterialPropertyBlock();
                Color c = tint * lift; c.a = 1f;
                _haloBlock.SetColor(BaseColorId, c);
                _haloBlock.SetColor(ColorId, c);
                halo.SetPropertyBlock(_haloBlock);
            }
            if (glowLight != null)
            {
                glowLight.color = tint;
                if (Application.isPlaying) glowLight.intensity = _lightHome * lift + sleepLight * breath;
            }
        }
    }
}
