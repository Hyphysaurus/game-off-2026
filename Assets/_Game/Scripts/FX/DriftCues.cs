using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The drift boost's cues (D-0058, his "drift prompts, visual or audio or both"): a soft tick when a drift could
    /// start (fast, the wheel turned hard into a bend, the handbrake not yet down; at most every driftChanceGap seconds),
    /// a soft spark crackle that grows while a slide charges (D-0058b: was a whirr that climbed like a siren), a chime as each level banks (higher for level two), and sparks
    /// from the rear wheels while the handbrake holds a charge: pale while building, cyan at level one, orange at level
    /// two (the trails' colours). The HUD's DRIFT / RELEASE words are DrivingHud's. Sounds are flat (the driver's own)
    /// and made in code (ProceduralClips) unless DrivingFeelSettings gives a clip. The sparks are ParticleSystems made at
    /// run time under DrivingFeel (not children of the car, so its measured size never grows) and moved to the rear
    /// wheels each frame, on a copy of Resources/FX/BoxDust as CarTrails does. DrivingFeel binds and ticks it.
    /// </summary>
    public sealed class DriftCues : MonoBehaviour
    {
        private const string MaterialResource = "FX/BoxDust";
        private const float Silent = 0.002f;
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private DrivingFeelSettings _settings;
        private AudioSource _charge, _shots;
        private AudioClip _madeCharge, _madeChime, _madeTick;
        private Material _material;
        private bool _ownsMaterial;
        private Texture2D _dot;
        private readonly List<ParticleSystem> _sparks = new List<ParticleSystem>();
        private readonly List<int> _sparkWheel = new List<int>();   // which wheel each spark system follows
        private bool _wasChance;
        private float _nextTick;
        private int _sparkLevel = -1;
        private float _sparkScale = -1f;

        /// <summary>Hands over the tuning and makes the two sources (silent until needed).</summary>
        public void Setup(DrivingFeelSettings settings)
        {
            _settings = settings;
            _charge = Source("DriftCharge", true);
            _shots = Source("DriftShots", false);
        }

        /// <summary>Follows <paramref name="car"/>'s rear wheels from now on (null: none).</summary>
        public void Bind(ArcadeCar car)
        {
            _sparkWheel.Clear();
            _wasChance = false; _sparkLevel = -1; _sparkScale = -1f;
            foreach (var ps in _sparks) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (car == null || _settings == null || _settings.sparkRate <= 0f) return;
            for (int i = 0; i < car.WheelCount; i++) if (car.WheelIsRear(i)) _sparkWheel.Add(i);
            if (_sparkWheel.Count > 0 && _material == null) MakeMaterial();
            while (_sparks.Count < _sparkWheel.Count) _sparks.Add(MakeSparks("Sparks " + _sparks.Count));
        }

        /// <summary>Once a frame. <paramref name="car"/> is null when nothing should sound or show.</summary>
        public void Tick(in DriveState state, ArcadeCar car, float dt)
        {
            if (_settings == null) return;
            var s = _settings;
            bool live = car != null && state.Live;

            // "you can drift here": once as the chance opens, not more often than the gap
            bool chance = live && state.DriftChance;
            if (chance && !_wasChance && Time.unscaledTime >= _nextTick)
            {
                _nextTick = Time.unscaledTime + Mathf.Max(0.2f, s.driftChanceGap);
                Shot(s.driftChanceClip != null ? s.driftChanceClip : TickClip(), s.driftChanceVolume, 1f);
            }
            _wasChance = chance;

            // the charging crackle grows a little with the charge
            float meter = live ? Mathf.Clamp(state.DriftMeter, 0f, 2f) : 0f;
            bool charging = live && state.DriftHeld && meter > 0f;
            Follow(charging ? s.driftChargeVolume * (0.4f + 0.3f * meter) : 0f, Mathf.Lerp(s.driftChargePitch.x, s.driftChargePitch.y, meter * 0.5f), dt);

            // sparks from the rear wheels on the ground while the handbrake holds a charge
            for (int k = 0; k < _sparkWheel.Count && k < _sparks.Count; k++)
            {
                var ps = _sparks[k];
                if (ps == null) continue;
                var emission = ps.emission;
                int w = _sparkWheel[k];
                bool on = charging && w < car.WheelCount && car.WheelGrounded(w);
                emission.rateOverTime = on ? s.sparkRate * (0.35f + 0.325f * meter) : 0f;
                if (!live || w >= car.WheelCount) continue;
                if (!Mathf.Approximately(car.Scale, _sparkScale)) Size(car.Scale);
                int level = Mathf.Clamp(state.DriftLevel, 0, 2);
                if (level != _sparkLevel) Colour(level);
                Vector3 up = car.transform.up;
                ps.transform.SetPositionAndRotation(car.WheelHub(w) - up * (car.WheelRadius(w) * 0.9f),
                    Quaternion.LookRotation(-car.transform.forward + up * 0.55f, up));
            }
        }

        /// <summary>A level banked: the chime, higher and louder for level two.</summary>
        public void Banked(int level)
        {
            if (_settings == null || level <= 0) return;
            AudioClip clip = _settings.driftLevelClip != null ? _settings.driftLevelClip : ChimeClip();
            Shot(clip, _settings.driftLevelVolume * (level >= 2 ? 1f : 0.8f), level >= 2 ? 1.5f : 1f);
        }

        private void Shot(AudioClip clip, float volume, float pitch)
        {
            if (_shots == null || clip == null || volume <= 0f) return;
            _shots.pitch = pitch;
            _shots.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void Follow(float volume, float pitch, float dt)
        {
            if (_charge == null) return;
            if (volume <= 0f && !_charge.isPlaying) return;
            if (!_charge.isPlaying)
            {
                if (_charge.clip == null) _charge.clip = _settings.driftChargeClip != null ? _settings.driftChargeClip : (_madeCharge = _madeCharge != null ? _madeCharge : ProceduralClips.DriftCharge());
                _charge.volume = 0f;
                _charge.Play();
            }
            _charge.pitch = Mathf.Max(0.05f, pitch);
            _charge.volume = Mathf.MoveTowards(_charge.volume, Mathf.Clamp01(volume), 12f * dt);   // quick: a slide is short
            if (volume <= 0f && _charge.volume <= Silent) _charge.Stop();
        }

        private AudioClip ChimeClip() => _madeChime != null ? _madeChime : (_madeChime = ProceduralClips.Chime());
        private AudioClip TickClip() => _madeTick != null ? _madeTick : (_madeTick = ProceduralClips.Tick());

        private void Colour(int level)
        {
            _sparkLevel = level;
            Color c = level >= 2 ? _settings.driftTwoColour : level == 1 ? _settings.driftOneColour : _settings.sparkChargeColour;
            c.a = 1f;
            foreach (var ps in _sparks)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startColor = c;
            }
        }

        /// <summary>Spark size and speed follow the car's size (a grown car throws bigger sparks).</summary>
        private void Size(float scale)
        {
            _sparkScale = scale;
            foreach (var ps in _sparks)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f * scale, 0.11f * scale);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 7.5f * scale);
                var shape = ps.shape;
                shape.radius = 0.08f * scale;
            }
        }

        private ParticleSystem MakeSparks(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.25f;   // the props' gravity is strong (60): a quarter of it arcs them down quickly
            main.maxParticles = 160;
            main.startColor = _settings.sparkChargeColour;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.08f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var shrink = ps.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f;
            r.lengthScale = 1.2f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = _material;
            ps.Play();
            return ps;
        }

        private void MakeMaterial()
        {
            _dot = Dot();
            var source = Resources.Load<Material>(MaterialResource);
            if (source != null)
            {
                _material = new Material(source) { name = "DriftSparks" };
                _ownsMaterial = true;
                if (_material.HasProperty(BaseMapId)) _material.SetTexture(BaseMapId, _dot);
                if (_material.HasProperty(MainTexId)) _material.SetTexture(MainTexId, _dot);
                if (_material.HasProperty(BaseColorId)) _material.SetColor(BaseColorId, Color.white);
                if (_material.HasProperty(ColorId)) _material.SetColor(ColorId, Color.white);
                return;
            }
            _material = UnityEngine.UI.Graphic.defaultGraphicMaterial;   // always built in; tinted by the particle colour
            _ownsMaterial = false;
        }

        /// <summary>A small soft round dot with a hot white middle.</summary>
        private static Texture2D Dot()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SparkDot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private AudioSource Source(string label, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = loop;
            a.spatialBlend = 0f;
            a.volume = loop ? 0f : 1f;
            return a;
        }

        private void OnDestroy()
        {
            if (_madeCharge != null) Destroy(_madeCharge);
            if (_madeChime != null) Destroy(_madeChime);
            if (_madeTick != null) Destroy(_madeTick);
            if (_ownsMaterial && _material != null) Destroy(_material);
            if (_dot != null) Destroy(_dot);
        }
    }
}
