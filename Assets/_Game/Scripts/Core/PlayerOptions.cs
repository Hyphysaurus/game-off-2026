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
        private const string LookKey = "opt.lookScale", InvertKey = "opt.invertY", VolumeKey = "opt.volume", BodyKey = "opt.body";

        private static bool _loaded;
        private static float _look = 1f, _volume = 1f;
        private static bool _invert;
        private static int _body;

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
                AudioListener.volume = Silent ? 0f : _volume;
            }
        }

        /// <summary>Which body the player wears, by its place in PlayerBodySettings (0 = Male, 1 = Female). PlayerBody
        /// reads it when a scene loads.</summary>
        public static int Body
        {
            get { Load(); return _body; }
            set { Load(); _body = Mathf.Max(0, value); PlayerPrefs.SetInt(BodyKey, _body); PlayerPrefs.Save(); }
        }

        /// <summary>Unity remembers the window mode itself between runs; this only switches it.</summary>
        private const string GraphicsKey = "ShiftJam.GraphicsHigh";

        /// <summary>Graphics (D-0119): High = the PC quality level (ambient occlusion, full shadows, the look), Low = the
        /// lighter level for weaker PCs. Applied in a built game only (in the editor it would change his editor's own
        /// quality level).</summary>
        public static bool HighGraphics
        {
            get => PlayerPrefs.GetInt(GraphicsKey, 1) == 1;
            set { PlayerPrefs.SetInt(GraphicsKey, value ? 1 : 0); PlayerPrefs.Save(); ApplyGraphics(); }
        }

        private static void ApplyGraphics()
        {
            if (Application.isEditor) return;
            int want = -1;
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++) if (names[i] == (HighGraphics ? "PC" : "Mobile")) want = i;
            if (want >= 0 && QualitySettings.GetQualityLevel() != want) QualitySettings.SetQualityLevel(want, true);
        }

        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set => Screen.fullScreen = value;
        }

        /// <summary>A hidden run (the smoke test, or any batch run of a check) makes no sound. 18 Nov: the checks muted
        /// themselves in the same start-up step this class sets the saved volume in, in no fixed order, so he heard
        /// the game's sounds on his speakers while checks ran. Now the game itself stays silent whenever nobody is at it.</summary>
        private static bool Silent => SmokeRun.Asked || Application.isBatchMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtStart()
        {
            _loaded = false;   // a fresh play session (the editor may keep statics between plays)
            AudioListener.volume = Silent ? 0f : Volume;
            ApplyGraphics();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _look = Mathf.Clamp(PlayerPrefs.GetFloat(LookKey, 1f), MinLook, MaxLook);
            _invert = PlayerPrefs.GetInt(InvertKey, 0) == 1;
            _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            _body = Mathf.Max(0, PlayerPrefs.GetInt(BodyKey, 0));
        }
    }
}
