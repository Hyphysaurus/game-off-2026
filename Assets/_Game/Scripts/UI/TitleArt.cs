using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// His title drawing ("Shift", Resources/Titles/title_shift_wide, cut from Shift_Title_V2) on a UI canvas (D-0099): the
    /// title menu and the end card. It comes in with a springy pop and then breathes a little, on real time (the title
    /// holds the game at time 0). On a window narrower than the box it shrinks to fit, keeping its shape.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleArt : MonoBehaviour
    {
        public const string Path = "Titles/title_shift_wide";

        [SerializeField, Tooltip("Seconds the pop-in takes.")] private float popSeconds = 0.55f;
        [SerializeField, Tooltip("Seconds before the pop starts.")] private float delay = 0.1f;
        [SerializeField, Tooltip("How much it breathes: share of its size (0 = still).")] private float breathe = 0.012f;
        [SerializeField, Tooltip("Seconds for one breath.")] private float breathSeconds = 3.6f;

        private Image _image;
        private float _t;

        /// <summary>The drawing, or null if the sprite is missing.</summary>
        public static Sprite Sprite => Resources.Load<Sprite>(Path);

        /// <summary>The drawing in a box <paramref name="box"/> (canvas units) centred on <paramref name="anchor"/>, kept
        /// inside the canvas's width less <paramref name="margin"/> each side. Null if the sprite is missing.</summary>
        public static TitleArt Place(Transform parent, Vector2 anchor, Vector2 offset, Vector2 box, float margin = 40f)
        {
            var sprite = Sprite;
            if (sprite == null) return null;
            var rt = new GameObject("TitleArt", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = offset;
            float wide = CanvasWidth(parent) - 2f * margin;
            if (wide > 0f && box.x > wide) box *= wide / box.x;   // a narrow window: the same drawing, smaller
            rt.sizeDelta = box;
            var img = rt.gameObject.AddComponent<Image>(); img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
            return rt.gameObject.AddComponent<TitleArt>();
        }

        /// <summary>The width of the canvas <paramref name="t"/> is on, in its own units (0 if it cannot be told yet).</summary>
        private static float CanvasWidth(Transform t)
        {
            var canvas = t.GetComponentInParent<Canvas>();
            if (canvas == null) return 0f;
            var root = canvas.rootCanvas;
            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                // the scaler's own sum (it sets the scale factor only on its next update)
                Vector2 r = scaler.referenceResolution;
                float lw = Mathf.Log(Screen.width / r.x, 2f), lh = Mathf.Log(Screen.height / r.y, 2f);
                float scale = Mathf.Pow(2f, Mathf.Lerp(lw, lh, scaler.matchWidthOrHeight));
                return Screen.width / Mathf.Max(0.0001f, scale);
            }
            return ((RectTransform)root.transform).rect.width;
        }

        private void Awake() { _image = GetComponent<Image>(); }

        private void OnEnable()
        {
            _t = 0f;
            Apply(0f);
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            Apply(_t);
        }

        private void Apply(float t)
        {
            float k = Mathf.Clamp01((t - delay) / Mathf.Max(0.01f, popSeconds));
            float pop = Pop(k);
            float breath = k >= 1f ? 1f + breathe * Mathf.Sin((t - delay - popSeconds) * Mathf.PI * 2f / Mathf.Max(0.1f, breathSeconds)) : 1f;
            transform.localScale = Vector3.one * (0.86f + 0.14f * pop) * breath;
            if (_image != null) { var c = _image.color; c.a = Mathf.Clamp01(k * 2.5f); _image.color = c; }
        }

        /// <summary>0 to 1 with a springy overshoot on the way.</summary>
        private static float Pop(float k)
        {
            if (k <= 0f) return 0f;
            if (k >= 1f) return 1f;
            const float c1 = 1.7f, c3 = c1 + 1f;
            float u = k - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
