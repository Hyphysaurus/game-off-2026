using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>Small helpers that make the Build Mode UI in code (same approach as TitleMenu: no prefabs, no scene UI).</summary>
    public static class BuildUiKit
    {
        public static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.1f, 0.88f);
        public static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.1f);
        public static readonly Color ActiveColor = new Color(0.35f, 0.85f, 1f, 0.45f);
        public static readonly Color WarnColor = new Color(1f, 0.45f, 0.3f, 0.6f);

        /// <summary>How the in-game HUD scales with the screen (D-0133): by its height alone, so a 21:9 screen shows it at
        /// the size a 16:9 screen of the same height does (half by width, half by height made it about 16% bigger there).
        /// Menus keep 0.5: matched by height, their wide layouts would run off a narrow screen.</summary>
        public const float HudMatch = 1f;

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Fills the parent, inset by the given margins (left, bottom, right, top).</summary>
        public static RectTransform Fill(string name, Transform parent, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Fill("Label", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string label, float fontSize, UnityAction onClick, out TextMeshProUGUI text)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = ButtonColor;
            var b = go.AddComponent<Button>(); b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = new Color(1f, 1f, 1f, 2.2f); cb.selectedColor = Color.white; cb.pressedColor = new Color(0.6f, 0.9f, 1f, 3f);
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) b.onClick.AddListener(onClick);
            text = Label(go.transform, label, fontSize);
            return b;
        }

        public static void SetFill(Button b, Color color) { if (b != null && b.targetGraphic is Image img) img.color = color; }

        /// <summary>A one-line TMP text box. Built switched off and wired before it wakes, so it never runs half set up.</summary>
        public static TMP_InputField Input(Transform parent, string placeholder, float fontSize)
        {
            var go = new GameObject("Input", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>(); bg.color = new Color(1f, 1f, 1f, 0.12f);
            var area = Fill("Text Area", go.transform, 12f, 4f, 12f, 4f);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Label(area, placeholder, fontSize, TextAlignmentOptions.MidlineLeft);
            ph.fontStyle = FontStyles.Italic; ph.color = new Color(1f, 1f, 1f, 0.4f);
            var tx = Label(area, "", fontSize, TextAlignmentOptions.MidlineLeft);
            tx.overflowMode = TextOverflowModes.Overflow;
            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = area; field.textComponent = tx; field.placeholder = ph; field.targetGraphic = bg;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            go.SetActive(true);
            return field;
        }

        /// <summary>A vertical scroll view; returns the content to fill (anchored to the top, grows downward).</summary>
        public static RectTransform ScrollView(RectTransform area, out ScrollRect scroll)
        {
            var view = Fill("Viewport", area);
            var hit = view.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0.01f);   // something to catch the wheel and drags
            view.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", view, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = view;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
            return content;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
