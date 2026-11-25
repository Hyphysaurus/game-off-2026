using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The game's title on a UI canvas. Two looks, both placed with <see cref="Place"/>:
    /// his title drawing ("Shift", Resources/Titles/title_shift_wide, cut from Shift_Title_V2; D-0099), on the end card;
    /// and a title PREFAB (D-0189), on the title menu: Resources/Titles/TitleMenuArt, his animated pixel lettering (an
    /// Image whose Animator flips the frames of his sprite sheet). To change the menu's title, edit that prefab: its
    /// sprite, its Animator, its Width and Height. Without the prefab the menu shows the drawing.
    /// Either look comes in with a springy pop, on real time (the title holds the game at time 0, so a prefab's
    /// Animators are put on unscaled time here). The drawing then breathes a little; a prefab does not, unless it
    /// carries its own TitleArt component with a Breathe value (scaling pixel art by a hair makes its pixels crawl).
    /// On a window narrower than its box a title shrinks to fit, keeping its shape.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleArt : MonoBehaviour
    {
        public const string Path = "Titles/title_shift_wide";
        /// <summary>The title menu's own title: a prefab under Resources (see the class summary).</summary>
        public const string MenuPrefabPath = "Titles/TitleMenuArt";

        [SerializeField, Tooltip("Seconds the pop-in takes.")] private float popSeconds = 0.55f;
        [SerializeField, Tooltip("Seconds before the pop starts.")] private float delay = 0.1f;
        [SerializeField, Tooltip("How much it breathes: share of its size (0 = still).")] private float breathe = 0.012f;
        [SerializeField, Tooltip("Seconds for one breath.")] private float breathSeconds = 3.6f;

        private Image _image;
        private float _t;

        /// <summary>The drawing, or null if the sprite is missing.</summary>
        public static Sprite Sprite => Resources.Load<Sprite>(Path);

        /// <summary>The title in a box <paramref name="box"/> (canvas units) centred on <paramref name="anchor"/>, kept
        /// inside the canvas's width less <paramref name="margin"/> each side. With a <paramref name="prefabPath"/> that
        /// exists under Resources, that prefab is the title and its own Width and Height are its box (the
        /// <paramref name="box"/> is used only if the prefab has none); otherwise the drawing. Null if neither is there.</summary>
        public static TitleArt Place(Transform parent, Vector2 anchor, Vector2 offset, Vector2 box, float margin = 40f, string prefabPath = null)
        {
            var prefab = string.IsNullOrEmpty(prefabPath) ? null : Resources.Load<GameObject>(prefabPath);
            if (prefab != null && prefab.transform is RectTransform) return PlacePrefab(prefab, parent, anchor, offset, box, margin);

            var sprite = Sprite;
            if (sprite == null) return null;
            var rt = new GameObject("TitleArt", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = offset;
            rt.sizeDelta = Fit(box, parent, margin);
            var img = rt.gameObject.AddComponent<Image>(); img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
            return rt.gameObject.AddComponent<TitleArt>();
        }

        /// <summary>A copy of the title prefab under <paramref name="parent"/>: the caller says where, the prefab says how
        /// big. Its Animators run on real time. A prefab without a TitleArt of its own gets one that does not breathe.</summary>
        private static TitleArt PlacePrefab(GameObject prefab, Transform parent, Vector2 anchor, Vector2 offset, Vector2 box, float margin)
        {
            var go = Instantiate(prefab, parent, false);
            go.name = "TitleArt";
            var rt = (RectTransform)go.transform;
            Vector2 own = rt.sizeDelta;
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = offset;
            rt.sizeDelta = Fit(own.x > 0f && own.y > 0f ? own : box, parent, margin);
            foreach (var animator in go.GetComponentsInChildren<Animator>(true)) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (!go.TryGetComponent<TitleArt>(out var art)) { art = go.AddComponent<TitleArt>(); art.breathe = 0f; }
            return art;
        }

        /// <summary><paramref name="box"/>, made smaller (same shape) if the canvas <paramref name="parent"/> is on is
        /// narrower than it plus <paramref name="margin"/> each side.</summary>
        private static Vector2 Fit(Vector2 box, Transform parent, float margin)
        {
            float wide = CanvasWidth(parent) - 2f * margin;
            return wide > 0f && box.x > wide ? box * (wide / box.x) : box;
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
