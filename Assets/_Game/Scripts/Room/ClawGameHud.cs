using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The claw machine's small panel while it is steered (D-0229): its heading, the seconds left before the claw drops by
    /// itself, and the keys. In the screen's top right corner, which the HUD leaves free (the hotbar and the ammo are at
    /// the bottom, the objective card top left, the toasts top middle), and away from the glass's front, where the prizes
    /// are. Made in code the first time it shows (as the armory bench's screen is, with the same UI kit), kept hidden
    /// between rounds, destroyed with the machine. The seconds' words are set only when the whole second changes, so
    /// nothing is made every frame. The words are the settings' (placeholders). Not a component: ClawGame owns one.
    /// </summary>
    internal sealed class ClawGameHud
    {
        private const float Width = 680f, Height = 118f, Inset = 32f;
        private const int Sorting = 40;   // over the game, under the toasts and the key sheet

        private GameObject _root;
        private TextMeshProUGUI _title, _time, _keys;
        private int _shown = int.MinValue;

        /// <summary>Shows the panel with these words (the seconds come with <see cref="Seconds"/>).</summary>
        public void Show(string title, string keys)
        {
            if (_root == null) Build();
            _title.text = title ?? "";
            _keys.text = keys ?? "";
            _shown = int.MinValue;
            if (!_root.activeSelf) _root.SetActive(true);
        }

        /// <summary>The seconds left: written only when the whole second changes.</summary>
        public void Seconds(int seconds, string format)
        {
            if (_root == null || seconds == _shown) return;
            _shown = seconds;
            _time.text = ClawGameSettings.Fill(format, seconds);
        }

        public void Hide()
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }

        /// <summary>Gone for good (the machine is).</summary>
        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }

        /// <summary>The canvas, scaled with the screen's height like the HUD, and the panel at the bottom middle.</summary>
        private void Build()
        {
            _root = new GameObject("ClawGameHud", typeof(RectTransform));
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Sorting;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = BuildUiKit.HudMatch;

            RectTransform panel = BuildUiKit.Rect("Panel", _root.transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-Inset, -Inset), new Vector2(Width, Height));
            BuildUiKit.Panel(panel, BuildUiKit.PanelColor);
            RectTransform top = BuildUiKit.Rect("Title", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-70f, -14f), new Vector2(-180f, 44f));
            _title = BuildUiKit.Label(top, "", 30f, TextAlignmentOptions.MidlineLeft);
            _title.fontStyle = FontStyles.Bold;
            RectTransform time = BuildUiKit.Rect("Time", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -6f), new Vector2(140f, 64f));
            _time = BuildUiKit.Label(time, "", 54f, TextAlignmentOptions.MidlineRight);
            _time.fontStyle = FontStyles.Bold;
            _time.color = new Color(1f, 0.82f, 0.4f);
            RectTransform keys = BuildUiKit.Rect("Keys", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-36f, 34f));
            _keys = BuildUiKit.Label(keys, "", 18f, TextAlignmentOptions.MidlineLeft);
            _keys.color = new Color(1f, 1f, 1f, 0.8f);
            _root.SetActive(false);
        }
    }
}
