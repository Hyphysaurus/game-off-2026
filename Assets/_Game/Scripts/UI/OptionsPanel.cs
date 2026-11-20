using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The Options screen (D-0044): look speed, invert Y, volume, fullscreen, the player's body, Back. One is made by each menu that offers
    /// it (the title and the pause menu); the menu hides itself, calls Open with what to do on the way back, and this
    /// panel saves every change straight into PlayerOptions. Esc, the pad's B or Start also go back. Built in code like
    /// TitleMenu, with mouse, keyboard and pad navigation through the scene's EventSystem.
    /// </summary>
    public sealed class OptionsPanel : MonoBehaviour
    {
        private GameObject _root;
        private TMP_Text _look, _invert, _volume, _fullscreen, _body;
        private Button _first;
        private Action _onClose;
        private bool _fullscreenWanted;
        private int _openedFrame;

        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>Makes a hidden Options screen on its own canvas, drawn above the menu that owns it.</summary>
        public static OptionsPanel Create(Transform parent, int sortingOrder)
        {
            var go = new GameObject("Options");
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<OptionsPanel>();
            panel.Build(sortingOrder);
            return panel;
        }

        public void Open(Action onClose)
        {
            _onClose = onClose;
            _fullscreenWanted = PlayerOptions.Fullscreen;
            _openedFrame = Time.frameCount;   // the press that opened it must not also close it
            Refresh();
            _root.SetActive(true);
            UiAudio.Hush();
            EventSystem.current?.SetSelectedGameObject(_first.gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            UiAudio.Play(UiAudio.Kind.Back);
            UiAudio.Hush();   // the menu under it selects its Options button again
            _root.SetActive(false);
            var back = _onClose; _onClose = null;
            back?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen || Time.frameCount == _openedFrame) return;
            var k = Keyboard.current; var pad = Gamepad.current;
            if ((k != null && k.escapeKey.wasPressedThisFrame) || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))) Close();
        }

        private void Refresh()
        {
            _look.text = $"{PlayerOptions.LookScale:0.00}×";
            _invert.text = PlayerOptions.InvertY ? "On" : "Off";
            _volume.text = $"{Mathf.RoundToInt(PlayerOptions.Volume * 100f)}%";
            _fullscreen.text = (_fullscreenWanted ? "On" : "Off") + (Application.isEditor ? " <size=60%><alpha=#88>(built game only)" : "");
            if (_body != null)
            {
                // The body is swapped when a scene loads: say so while the picked one is not the one on screen.
                var worn = FindFirstObjectByType<PlayerBody>();
                bool waits = worn != null && worn.Index != Mathf.Clamp(PlayerOptions.Body, 0, Mathf.Max(0, PlayerBody.Count - 1));
                _body.text = PlayerBody.NameOf(PlayerOptions.Body) + (waits ? " <size=60%><alpha=#88>(from the next load)" : "");
            }
        }

        // ------------------------------------------------------------------ building the canvas
        private void Build(int sortingOrder)
        {
            _root = new GameObject("OptionsCanvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sortingOrder;
            var scaler = _root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();
            MenuKit.Rect("Dim", _root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.06f, 0.8f);
            MenuKit.Text("Title", _root.transform, "<b>OPTIONS</b>", 64, new Vector2(0.5f, 0.78f), new Vector2(900, 90));

            float y = 0.66f;
            _first = Stepper("Mouse sensitivity", y, out _look,
                () => { PlayerOptions.LookScale -= PlayerOptions.LookStep; Refresh(); },
                () => { PlayerOptions.LookScale += PlayerOptions.LookStep; Refresh(); }); y -= 0.09f;
            Toggle("Invert Y", y, out _invert, () => { PlayerOptions.InvertY = !PlayerOptions.InvertY; Refresh(); }); y -= 0.09f;
            Stepper("Volume", y, out _volume,
                () => { PlayerOptions.Volume -= PlayerOptions.VolumeStep; Refresh(); },
                () => { PlayerOptions.Volume += PlayerOptions.VolumeStep; Refresh(); }); y -= 0.09f;
            // Screen.fullScreen changes at the end of the frame, so the label shows the asked-for state.
            Toggle("Fullscreen", y, out _fullscreen, () => { _fullscreenWanted = !_fullscreenWanted; PlayerOptions.Fullscreen = _fullscreenWanted; Refresh(); }); y -= 0.09f;
            // Only where there is more than one body to pick (after the player port's builder has run).
            if (PlayerBody.Count > 1)
            {
                Toggle("Body", y, out _body, () => { PlayerOptions.Body = (PlayerOptions.Body + 1) % PlayerBody.Count; Refresh(); }); y -= 0.09f;
            }
            y -= 0.03f;
            UiSound.On(MenuKit.Button(_root.transform, "Back", new Vector2(0.5f, y), 0f, 380f, 40f, Close)).Press = UiAudio.Kind.None;   // Close plays Back itself (Esc too)
            MenuKit.Text("Hint", _root.transform, "Esc / B to go back · changes save as you make them", 22, new Vector2(0.5f, 0.05f), new Vector2(900, 40)).alpha = 0.6f;
            _root.SetActive(false);
        }

        /// <summary>A row: the name on the left, then - value + (the first button is returned for selection).</summary>
        private Button Stepper(string name, float y, out TMP_Text value, UnityEngine.Events.UnityAction down, UnityEngine.Events.UnityAction up)
        {
            RowName(name, y);
            var minus = MenuKit.Button(_root.transform, "-", new Vector2(0.5f, y), 60f, 70f, 40f, down);
            value = MenuKit.Text("Value", _root.transform, "", 36, new Vector2(0.5f, y), new Vector2(200, 60));
            value.rectTransform.anchoredPosition = new Vector2(175f, 0f);
            MenuKit.Button(_root.transform, "+", new Vector2(0.5f, y), 290f, 70f, 40f, up);
            return minus;
        }

        private void Toggle(string name, float y, out TMP_Text value, UnityEngine.Events.UnityAction flip)
        {
            RowName(name, y);
            var b = MenuKit.Button(_root.transform, "", new Vector2(0.5f, y), 175f, 300f, 36f, flip);
            value = b.GetComponentInChildren<TMP_Text>();
        }

        private void RowName(string name, float y)
        {
            var t = MenuKit.Text(name, _root.transform, name, 36, new Vector2(0.5f, y), new Vector2(420, 60));
            t.alignment = TextAlignmentOptions.MidlineRight;
            t.rectTransform.anchoredPosition = new Vector2(-190f, 0f);
        }
    }

    /// <summary>The menu look shared by the pause menu and Options (the title menu's buttons, with pad navigation on).</summary>
    internal static class MenuKit
    {
        public static Button Button(Transform parent, string label, Vector2 anchor, float dx, float width, float size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Rect(label.Length > 0 ? label : "Toggle", parent, anchor, anchor, new Vector2(dx, 0f), new Vector2(width, size + 22f));
            var img = rt.gameObject.AddComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0.08f);
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.onClick.AddListener(onClick);
            UiSound.On(b);   // a move onto it, its press, a no when greyed out (D-0100)
            var cb = b.colors; cb.highlightedColor = new Color(1f, 1f, 1f, 2.5f); cb.selectedColor = new Color(1f, 1f, 1f, 2.5f); cb.pressedColor = new Color(0.6f, 0.9f, 1f, 3f); b.colors = cb;
            var t = Text("Label", rt, label, size, new Vector2(0.5f, 0.5f), new Vector2(width - 20f, size + 16f)); t.raycastTarget = false;
            return b;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Vector2 anchor, Vector2 box)
        {
            var rt = Rect(name, parent, anchor, anchor, Vector2.zero, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = Color.white; t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
            return t;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = min; rt.anchorMax = max; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Menus need an EventSystem for mouse, keys and pad; a scene without one gets the Input System's.</summary>
        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }
}
