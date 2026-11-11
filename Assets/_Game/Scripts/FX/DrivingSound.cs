using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What the driver hears on top of the car's own engine and crash: wind that grows and rises with speed, tyres
    /// scrubbing while the car slides sideways on the ground, and a thump on landing scaled by how hard it came down
    /// (DrivingFeelSettings). The sounds are flat (2D): they are the driver's own. Each uses the clip in the settings
    /// when one is given, else one made in code (ProceduralClips), built the first time it has to sound. A loop stops
    /// playing once it has faded out, so nothing runs when the car is slow, parked or nobody drives. DrivingFeel ticks it.
    /// </summary>
    public sealed class DrivingSound : MonoBehaviour
    {
        private const float Silent = 0.002f;   // a loop quieter than this is stopped

        private DrivingFeelSettings _settings;
        private AudioSource _wind, _skid, _shots;
        private AudioClip _madeWind, _madeSkid, _madeThump;   // generated here, so freed here
        private uint _seed = 0x51ED270Bu;

        /// <summary>Hands over the tuning and makes the three sources (silent until needed).</summary>
        public void Setup(DrivingFeelSettings settings)
        {
            _settings = settings;
            _wind = Source("Wind", true);
            _skid = Source("Skid", true);
            _shots = Source("Thump", false);
        }

        /// <summary>Once a frame: the two loops follow speed and slip.</summary>
        public void Tick(in DriveState state, float dt)
        {
            if (_settings == null) return;
            var s = _settings;
            float wind = 0f, skid = 0f;
            if (state.Live)
            {
                wind = s.windVolume * Mathf.InverseLerp(s.windFrom, 1f, state.Speed) + s.windBoostVolume * state.Over;
                if (state.Grounded) skid = s.skidVolume * Mathf.InverseLerp(s.skidFrom, Mathf.Max(s.skidFrom + 0.01f, s.skidFull), state.Slip);
            }
            float full = Mathf.Max(1.01f, s.boostFull);
            Follow(_wind, wind, Mathf.Lerp(s.windPitch.x, s.windPitch.y, Mathf.Clamp01(state.Speed / full)), dt, true);
            Follow(_skid, skid, Mathf.Lerp(s.skidPitch.x, s.skidPitch.y, Mathf.Clamp01(state.Speed)), dt, false);
        }

        /// <summary>A landing: <paramref name="strength"/> runs 0..1 (1 = a full-strength landing).</summary>
        public void Thump(float strength)
        {
            if (_settings == null || _shots == null || strength <= 0f) return;
            AudioClip clip = _settings.landClip;
            if (clip == null)
            {
                if (_madeThump == null) _madeThump = ProceduralClips.Thump();
                clip = _madeThump;
            }
            _shots.pitch = 1f + (Next() * 2f - 1f) * _settings.landPitchSpread;
            _shots.PlayOneShot(clip, Mathf.Clamp01(_settings.landVolume * strength));
        }

        private void OnDestroy()
        {
            if (_madeWind != null) Destroy(_madeWind);
            if (_madeSkid != null) Destroy(_madeSkid);
            if (_madeThump != null) Destroy(_madeThump);
        }

        /// <summary>Eases a loop's volume toward <paramref name="volume"/>; starts it when it has to sound (making its clip
        /// the first time) and stops it once it has faded out.</summary>
        private void Follow(AudioSource source, float volume, float pitch, float dt, bool isWind)
        {
            if (source == null) return;
            volume = Mathf.Clamp01(volume);
            if (volume <= 0f && !source.isPlaying) return;
            if (!source.isPlaying)
            {
                if (source.clip == null) source.clip = isWind ? WindClip() : SkidClip();
                source.volume = 0f;
                source.Play();
            }
            source.pitch = Mathf.Max(0.05f, pitch);
            source.volume = Mathf.MoveTowards(source.volume, volume, Mathf.Max(0.1f, _settings.soundFade) * dt);
            if (volume <= 0f && source.volume <= Silent) source.Stop();
        }

        private AudioClip WindClip()
        {
            if (_settings.windClip != null) return _settings.windClip;
            if (_madeWind == null) _madeWind = ProceduralClips.Wind();
            return _madeWind;
        }

        private AudioClip SkidClip()
        {
            if (_settings.skidClip != null) return _settings.skidClip;
            if (_madeSkid == null) _madeSkid = ProceduralClips.Skid();
            return _madeSkid;
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

        /// <summary>A cheap random number 0..1 of its own, so the game's shared random stream is left alone.</summary>
        private float Next()
        {
            _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
