using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's own settings (D-0044): look speed, invert Y, master volume, fullscreen. Kept in PlayerPrefs, apart
    /// from the CameraSettings / FirstPersonSettings assets: those hold the designed values, these scale or flip them,
    /// so changing an option never edits a project asset. The cameras read LookScale and InvertY every frame; the volume
    /// is applied before the first scene wakes (except in a smoke run, which keeps itself silent).
    /// </summary>
    public static class PlayerOptions
    {
        public const float MinLook = 0.25f, MaxLook = 3f, LookStep = 0.25f, VolumeStep = 0.1f;
        private const string LookKey = "opt.lookScale", InvertKey = "opt.invertY", VolumeKey = "opt.volume";

        private static bool _loaded;
        private static float _look = 1f, _volume = 1f;
        private static bool _invert;

        /// <summary>Multiplies the mouse and stick look speed (1 = as designed).</summary>
        public static float LookScale
        {
            get { Load(); return _look; }
            set { Load(); _look = Mathf.Clamp(Mathf.Round(value / LookStep) * LookStep, MinLook, MaxLook); PlayerPrefs.SetFloat(LookKey, _look); PlayerPrefs.Save(); }
        }

        /// <summary>Flips the designed up/down look (on top of the settings asset's own invertY).</summary>
        public static bool InvertY
        {
            get { Load(); return _invert; }
            set { Load(); _invert = value; PlayerPrefs.SetInt(InvertKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Master volume, 0..1, in steps of a tenth.</summary>
        public static float Volume
        {
            get { Load(); return _volume; }
            set
            {
                Load(); _volume = Mathf.Clamp01(Mathf.Round(value / VolumeStep) * VolumeStep);
                PlayerPrefs.SetFloat(VolumeKey, _volume); PlayerPrefs.Save();
                AudioListener.volume = _volume;
            }
        }

        /// <summary>Unity remembers the window mode itself between runs; this only switches it.</summary>
        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set => Screen.fullScreen = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtStart()
        {
            _loaded = false;   // a fresh play session (the editor may keep statics between plays)
            if (!SmokeRun.Asked) AudioListener.volume = Volume;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _look = Mathf.Clamp(PlayerPrefs.GetFloat(LookKey, 1f), MinLook, MaxLook);
            _invert = PlayerPrefs.GetInt(InvertKey, 0) == 1;
            _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        }
    }
}
