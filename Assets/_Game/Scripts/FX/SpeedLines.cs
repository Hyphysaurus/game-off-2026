using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// Speed lines: white streaks round the edge of the screen that point at its middle, faint near top speed and strong
    /// while a boost pushes (DrivingFeelSettings, "Speed lines"). One picture, drawn in code the first time it is needed
    /// (no shader, no art), on its own screen canvas under the driving HUD; several times a second it is turned to a new
    /// angle, which reads as streaks rushing past. The canvas is off whenever nothing shows, so it costs one see-through
    /// full-screen picture while the car is fast and nothing otherwise. DrivingFeel ticks it.
    /// </summary>
    public sealed class SpeedLines : MonoBehaviour
    {
        private const int SortingOrder = -20;     // under the driving HUD (-10), so the dial stays readable
        private const int TextureSize = 512;
        private const int Bins = 1024;            // directions round the circle the streaks are laid out in
        private const float Feather = 0.3f;       // how far, in screen-corner shares, a streak takes to reach full strength
        private const float Visible = 0.004f;     // below this the canvas is switched off
        private const float Breath = 0.12f;       // each jump also grows the picture by up to this share

        private DrivingFeelSettings _settings;
        private Canvas _canvas;
        private RawImage _image;
        private RectTransform _rect;
        private Texture2D _texture;
        private float _alpha, _boost, _nextJump;
        private int _width, _height;
        private uint _seed = 0x9E3779B9u;

        /// <summary>Hands over the tuning. Nothing is made until the lines first have to show.</summary>
        public void Setup(DrivingFeelSettings settings) => _settings = settings;

        /// <summary>Once a frame: fades toward what the speed asks for and keeps the streaks jumping.</summary>
        public void Tick(in DriveState state, float dt)
        {
            if (_settings == null) return;
            var s = _settings;
            float target = 0f;
            if (state.Live)
            {
                float cruise = s.linesAlpha * Mathf.InverseLerp(s.linesFrom, Mathf.Max(s.linesFrom + 0.01f, s.linesFullAt), state.Speed);
                // a boost shows them at full strength, but not while the car is still slow (a launch from standing)
                float boosted = state.Boosting ? Mathf.InverseLerp(s.linesBoostFrom, Mathf.Max(s.linesBoostFrom + 0.01f, s.linesFrom), state.Speed) : state.Over;
                target = Mathf.Lerp(cruise, Mathf.Max(cruise, s.linesBoostAlpha), boosted);
            }
            float rate = Mathf.Max(0.1f, s.linesFade) * dt;
            _alpha = Mathf.MoveTowards(_alpha, target, rate);
            _boost = Mathf.MoveTowards(_boost, state.Live && state.Boosting ? 1f : 0f, rate);

            bool show = _alpha > Visible;
            if (!show)
            {
                if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
                return;
            }
            if (_canvas == null) Build();
            if (!_canvas.enabled) _canvas.enabled = true;

            // a square as wide as the screen's diagonal, so it covers the corners at any angle
            if (Screen.width != _width || Screen.height != _height)
            {
                _width = Screen.width; _height = Screen.height;
                float side = Mathf.Sqrt((float)_width * _width + (float)_height * _height) / Mathf.Max(0.01f, _canvas.scaleFactor);
                _rect.sizeDelta = new Vector2(side, side);
            }

            float now = Time.unscaledTime;
            if (now >= _nextJump)
            {
                _nextJump = now + 1f / Mathf.Max(1f, s.linesFlicker);
                _rect.localRotation = Quaternion.Euler(0f, 0f, Next() * 360f);
                float grow = 1f + Breath * Next();   // a little in and out as well as round
                _rect.localScale = new Vector3(grow, grow, 1f);
            }
            Color c = Color.Lerp(s.linesColour, s.linesBoostColour, _boost);
            c.a = _alpha;
            _image.color = c;
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        /// <summary>A cheap random number 0..1 of its own, so the game's shared random stream is left alone.</summary>
        private float Next()
        {
            _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / (float)0x1000000;
        }

        private void Build()
        {
            var canvasGo = new GameObject("SpeedLinesCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;

            var go = new GameObject("Lines", typeof(RectTransform));
            _rect = go.GetComponent<RectTransform>();
            _rect.SetParent(canvasGo.transform, false);
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = Vector2.zero;
            _image = go.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _texture = Draw();
            _image.texture = _texture;
            _width = _height = 0;   // sized on the first shown frame
        }

        /// <summary>
        /// The streak picture: white, see-through, each streak a thin wedge that starts some way out from the middle and
        /// runs to the edge. The middle of the screen stays clear. Streaks are laid out by direction (Bins round the
        /// circle), then every pixel looks up its direction and how far out it is.
        /// </summary>
        private Texture2D Draw()
        {
            var s = _settings;
            var strength = new float[Bins];
            var inner = new float[Bins];
            float from = Mathf.Min(s.linesInner.x, s.linesInner.y), to = Mathf.Max(s.linesInner.x, s.linesInner.y);
            for (int b = 0; b < Bins; b++) inner[b] = to;
            int count = Mathf.Clamp(s.linesCount, 8, 200);
            for (int i = 0; i < count; i++)
            {
                int centre = (int)(Next() * Bins) % Bins;
                float half = 0.6f + 1.8f * Next();           // half width, in bins
                float power = 0.35f + 0.65f * Next();
                float start = Mathf.Lerp(from, to, Next());
                int reach = Mathf.CeilToInt(half) + 1;
                for (int d = -reach; d <= reach; d++)
                {
                    float a = power * Mathf.Clamp01(half - Mathf.Abs(d) + 0.5f);
                    int b = ((centre + d) % Bins + Bins) % Bins;
                    if (a <= strength[b]) continue;
                    strength[b] = a; inner[b] = start;
                }
            }

            var px = new Color32[TextureSize * TextureSize];
            float c = TextureSize * 0.5f;
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / c;   // 1 = the picture's edge = the screen's corner
                    byte alpha = 0;
                    if (r > from)
                    {
                        float at = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f) * Bins;
                        int b0 = (int)at % Bins, b1 = (b0 + 1) % Bins;
                        float f = at - Mathf.Floor(at);
                        float a = Mathf.Lerp(strength[b0], strength[b1], f);
                        float begin = Mathf.Lerp(inner[b0], inner[b1], f);
                        a *= Mathf.SmoothStep(0f, 1f, (r - begin) / Feather);
                        alpha = (byte)(Mathf.Clamp01(a) * 255f + 0.5f);
                    }
                    px[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
                }

            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
                { name = "SpeedLines", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
