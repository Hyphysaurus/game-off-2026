using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// Draws <see cref="Toasts"/> (D-0162): the "Toy Box" sticker cards from the HUD mockup (cream card, thick dark
    /// outline, a drop shadow, a coloured badge with the kind's icon, a small label over the line). They drop in from the
    /// top with a slight tilt, stay longer for longer lines, a refusal shakes, a repeat bumps its x-count, and the oldest
    /// leaves early when a fourth arrives. One for the whole game (kept across scene loads), hidden under menus.
    /// </summary>
    public sealed class ToastHud : MonoBehaviour
    {
        private const string FontPath = "HUD/Fredoka SDF";
        private const int MaxShown = 3;
        private const float Top = 26f, Gap = 14f, MaxTextWidth = 640f, Badge = 52f, Pad = 16f, Outline = 4f, Shadow = 6f;
        private const float InSeconds = 0.35f, OutSeconds = 0.35f;
        private static readonly Color Ink = new Color32(0x1f, 0x1b, 0x2e, 0xff);

        private struct Look { public Color fill, badge, label; public string name; public UiAudio.Kind sound; public float seconds; }

        private static Look LookOf(ToastKind kind)
        {
            switch (kind)
            {
                case ToastKind.Got: return new Look { fill = Hex(0xfff7e0), badge = Hex(0xffd166), label = Hex(0x92400e), name = "GOT IT", sound = UiAudio.Kind.Confirm, seconds = 4f };
                case ToastKind.Refused: return new Look { fill = Hex(0xffe1dc), badge = Hex(0xf87171), label = Hex(0xb42318), name = "CAN'T", sound = UiAudio.Kind.Denied, seconds = 2.6f };
                case ToastKind.Warning: return new Look { fill = Hex(0xfef3c7), badge = Hex(0xfbbf24), label = Hex(0x92400e), name = "WATCH OUT", sound = UiAudio.Kind.Toast, seconds = 4f };
                case ToastKind.Core: return new Look { fill = Hex(0xede9fe), badge = Hex(0xc4b5fd), label = Hex(0x6d28d9), name = "CORE", sound = UiAudio.Kind.Win, seconds = 4f };
                default: return new Look { fill = Hex(0xe0f2fe), badge = Hex(0x8ecae6), label = Hex(0x0369a1), name = "TIP", sound = UiAudio.Kind.Toast, seconds = 5f };
            }
        }

        private sealed class Card
        {
            public RectTransform rt;
            public CanvasGroup group;
            public TextMeshProUGUI count;
            public string text, title;
            public ToastKind kind;
            public int times = 1;
            public float born, until, bumped = -1f, height, y, tilt;
        }

        private static ToastHud _instance;
        private readonly List<Card> _cards = new List<Card>();
        private RectTransform _root;
        private CanvasGroup _group;
        private TMP_FontAsset _font;
        private int _made;

        public static ToastHud Get()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("ToastHud");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ToastHud>();
            _instance.Build();
            return _instance;
        }

        public void Add(string text, ToastKind kind, string title)
        {
            text ??= ""; title ??= "";
            float now = Time.unscaledTime;
            var look = LookOf(kind);
            foreach (var c in _cards)
                if (c.kind == kind && c.text == text && c.title == title && now < c.until - OutSeconds)
                {
                    c.times++;
                    c.count.text = "x" + c.times;
                    c.until = now + Life(look, text);
                    c.bumped = now;
                    UiAudio.Play(look.sound);
                    return;
                }
            if (_cards.Count >= MaxShown) { var oldest = _cards[0]; oldest.until = Mathf.Min(oldest.until, now + OutSeconds); }
            var card = Make(text, kind, title, look);
            card.born = now; card.until = now + Life(look, text);
            card.tilt = (_made++ % 2 == 0 ? -1.4f : 1f);
            card.y = -1f;   // placed on its first frame (Update), then the drop-in comes from the offset
            _cards.Add(card);
            UiAudio.Play(look.sound);
        }

        private static float Life(Look look, string text) => look.seconds + Mathf.Clamp(text.Length * 0.03f, 0f, 3f);

        private void Update()
        {
            bool hidden = PauseMenu.IsOpen || TitleMenu.IsOpen;
            _group.alpha = hidden ? 0f : 1f;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            float y = Top;
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                if (now >= c.until) { Destroy(c.rt.gameObject); _cards.RemoveAt(i); i--; continue; }
                c.y = c.y < 0f ? y : Mathf.Lerp(c.y, y, 1f - Mathf.Exp(-14f * dt));   // slides up when one above leaves
                float tIn = Mathf.Clamp01((now - c.born) / InSeconds);
                float drop = (1f - EaseOutBack(tIn)) * (c.height + Top);
                float shake = c.kind == ToastKind.Refused && now - c.born < 0.4f ? Mathf.Sin((now - c.born) * 60f) * 10f * (1f - (now - c.born) / 0.4f) : 0f;
                float tOut = Mathf.Clamp01((c.until - now) / OutSeconds);
                c.group.alpha = Mathf.Min(tIn * 2f, 1f) * tOut;
                float bump = c.bumped >= 0f ? 1f + 0.12f * Mathf.Clamp01(1f - (now - c.bumped) / 0.25f) : 1f;
                c.rt.anchoredPosition = new Vector2(shake, -(c.y - drop) + (1f - tOut) * 20f);
                c.rt.localScale = Vector3.one * bump;
                c.rt.localRotation = Quaternion.Euler(0f, 0f, c.tilt);
                y += c.height + Gap + Shadow;
            }
        }

        private static float EaseOutBack(float t) { const float k = 1.70158f; t -= 1f; return 1f + (k + 1f) * t * t * t + k * t * t; }

        // ---------- building ----------

        private void Build()
        {
            _font = Resources.Load<TMP_FontAsset>(FontPath);
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false;
            _root = (RectTransform)canvasGo.transform;
        }

        private Card Make(string text, ToastKind kind, string title, Look look)
        {
            var card = new Card { text = text, title = title, kind = kind };
            string label = string.IsNullOrEmpty(title) ? look.name : title;
            bool bigTitle = !string.IsNullOrEmpty(title);
            string body = KeyCaps(text);

            // Measure first: the card fits its words, up to MaxTextWidth.
            var probe = Label(_root, "Probe", body, 26f, Ink, false);
            Vector2 bodySize = string.IsNullOrEmpty(text) ? Vector2.zero : probe.GetPreferredValues(body, MaxTextWidth, 0f);
            probe.text = label; probe.fontSize = bigTitle ? 26f : 18f; probe.fontStyle = FontStyles.Bold;
            Vector2 labelSize = probe.GetPreferredValues(label, MaxTextWidth, 0f);
            Destroy(probe.gameObject);
            float textW = Mathf.Min(Mathf.Max(bodySize.x, labelSize.x) + 2f, MaxTextWidth);
            float textH = labelSize.y + (bodySize.y > 0f ? 2f + bodySize.y : 0f);
            float w = Pad + Badge + 14f + textW + Pad + 40f;   // 40: room for the x-count
            float h = Mathf.Max(Badge + Pad * 2f, textH + Pad * 2f);
            card.height = h;

            var rt = Rect("Toast", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(w, h));
            card.rt = rt;
            card.group = rt.gameObject.AddComponent<CanvasGroup>(); card.group.alpha = 0f;

            HudShapes.RoundedImage(Rect("Shadow", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -Shadow), Vector2.zero), Ink, 22f);
            HudShapes.RoundedImage(Rect("Outline", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Ink, 22f);
            HudShapes.RoundedImage(Rect("Fill", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-Outline * 2f, -Outline * 2f)), look.fill, 22f - Outline);

            var badge = Rect("Badge", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Pad, 0f), new Vector2(Badge, Badge));
            HudShapes.RoundedImage(badge, Ink, 14f);
            var badgeFill = Rect("Fill", badge, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6f, -6f));
            HudShapes.RoundedImage(badgeFill, look.badge, 11f);
            Sprite icon = kind == ToastKind.Got ? HudShapes.Check : kind == ToastKind.Refused ? HudShapes.Cross : kind == ToastKind.Core ? HudShapes.Ring : null;
            if (icon != null)
            {
                var iconRt = Rect("Icon", badgeFill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
                var img = iconRt.gameObject.AddComponent<Image>(); img.sprite = icon; img.color = Ink; img.raycastTarget = false;
            }
            else
            {
                var glyph = Label(badgeFill, "Glyph", kind == ToastKind.Warning ? "!" : "?", 32f, Ink, true);
                glyph.alignment = TextAlignmentOptions.Center;
                Stretch(glyph.rectTransform);
            }

            float textX = Pad + Badge + 14f;
            var textBox = Rect("Words", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(textX, 0f), new Vector2(textW, textH));
            var head = Label(textBox, "Label", label, bigTitle ? 26f : 18f, bigTitle ? Ink : look.label, true);
            head.characterSpacing = bigTitle ? 0f : 6f;
            Pin(head.rectTransform, 0f, labelSize.y);
            if (bodySize.y > 0f)
            {
                var line = Label(textBox, "Line", body, 26f, Ink, false);
                line.fontWeight = FontWeight.SemiBold;
                Pin(line.rectTransform, labelSize.y + 2f, bodySize.y);
            }

            card.count = Label(rt, "Count", "", 24f, Ink, true);
            var countRt = card.count.rectTransform;
            countRt.anchorMin = countRt.anchorMax = new Vector2(1f, 0.5f); countRt.pivot = new Vector2(1f, 0.5f);
            countRt.anchoredPosition = new Vector2(-Pad, 0f); countRt.sizeDelta = new Vector2(48f, 32f);
            card.count.alignment = TextAlignmentOptions.Right;
            return card;
        }

        /// <summary>Bold words (the keys in "<b>F</b> shift") get a key-cap tint behind them.</summary>
        private static string KeyCaps(string text) =>
            string.IsNullOrEmpty(text) ? "" : text.Replace("<b>", "<mark=#1F1B2E24 padding=\"8,8,3,3\"><b>").Replace("</b>", "</b></mark>");

        private TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text; t.fontSize = size; t.color = color; t.raycastTarget = false;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static void Pin(RectTransform rt, float fromTop, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -fromTop); rt.sizeDelta = new Vector2(0f, height);
        }

        private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
