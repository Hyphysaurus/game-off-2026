using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The party list over the title (D-0122): every open Shift Jam party ("Mario's party 2/4") with a Join button,
    /// Refresh, Host New, Join With Code (the clipboard's) and Back. Made by the title like OptionsPanel.
    /// </summary>
    public sealed class PartiesPanel : MonoBehaviour
    {
        private const int Rows = 6;

        private GameObject _root;
        private TextMeshProUGUI _status;
        private readonly List<(TextMeshProUGUI label, Button join)> _rows = new List<(TextMeshProUGUI, Button)>();
        private readonly string[] _ids = new string[Rows];
        private Button _first;
        private Action _onBack;
        private bool _looking;

        public bool IsOpen => _root != null && _root.activeSelf;

        public static PartiesPanel Create(Transform parent, int sortingOrder)
        {
            var go = new GameObject("PartiesPanel");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<PartiesPanel>();
            p.Build(sortingOrder);
            return p;
        }

        public void Open(Action onBack)
        {
            _onBack = onBack;
            MenuKit.EnsureEventSystem();
            _root.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(_first.gameObject);
            Refresh();
        }

        private void Close()
        {
            _root.SetActive(false);
            UiAudio.Play(UiAudio.Kind.Back);
            _onBack?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen) return;
            var k = Keyboard.current; var pad = Gamepad.current;
            if ((k != null && k.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)) Close();
        }

        private async void Refresh()
        {
            if (_looking) return;
            _looking = true;
            _status.text = "Looking for parties...";
            for (int i = 0; i < Rows; i++) { _rows[i].label.text = ""; _rows[i].join.gameObject.SetActive(false); _ids[i] = null; }
            List<NetSession.PartyInfo> parties = await NetSession.FindParties();
            _looking = false;
            if (this == null || !IsOpen) return;
            for (int i = 0; i < Rows && i < parties.Count; i++)
            {
                var p = parties[i];
                _ids[i] = p.Id;
                _rows[i].label.text = $"<b>{Clip(p.Name)}</b>   <alpha=#AA>{p.World} · {p.Players}/{p.Max}";
                _rows[i].join.gameObject.SetActive(true);
            }
            _status.text = parties.Count == 0 ? "No open parties right now. Host one, or press Play Online on the title." : "";
        }

        private static string Clip(string s) => string.IsNullOrEmpty(s) ? "A party" : (s.Length > 28 ? s.Substring(0, 28) : s).Replace("<", "").Replace(">", "");

        private void JoinRow(int i)
        {
            if (_ids[i] == null) return;
            _status.text = "Joining...";
            NetSession.JoinParty(_ids[i]);
        }

        private void Build(int sortingOrder)
        {
            _root = new GameObject("PartiesCanvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sortingOrder;
            var scaler = _root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();
            MenuKit.Rect("Dim", _root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.06f, 0.85f);
            MenuKit.Text("Title", _root.transform, "<b>PARTIES</b>", 64, new Vector2(0.5f, 0.86f), new Vector2(900, 90));

            float y = 0.74f;
            for (int i = 0; i < Rows; i++)
            {
                int k = i;
                var label = MenuKit.Text("Party" + i, _root.transform, "", 30, new Vector2(0.5f, y), new Vector2(700, 50));
                label.alignment = TextAlignmentOptions.MidlineLeft;
                ((RectTransform)label.transform).anchoredPosition = new Vector2(-110f, 0f);
                var join = MenuKit.Button(_root.transform, "Join", new Vector2(0.5f, y), 330f, 160f, 30f, () => JoinRow(k));
                join.gameObject.SetActive(false);
                _rows.Add((label, join));
                y -= 0.065f;
            }
            _status = MenuKit.Text("Status", _root.transform, "", 26, new Vector2(0.5f, 0.6f), new Vector2(1200, 40));
            _status.alpha = 0.75f;

            y = 0.3f;
            _first = MenuKit.Button(_root.transform, "Refresh", new Vector2(0.5f, y), -200f, 360f, 34f, Refresh);
            MenuKit.Button(_root.transform, "Host New Party", new Vector2(0.5f, y), 200f, 360f, 34f, () => { _status.text = "Starting a party in the Stadium..."; NetSession.Host(); });
            y -= 0.075f;
            MenuKit.Button(_root.transform, "Host House Party", new Vector2(0.5f, y), 200f, 360f, 34f, () => { _status.text = "Starting a party in the house (co-op sandbox)..."; NetSession.HostHouse(); });   // D-0125
            MenuKit.Button(_root.transform, "Join With Code", new Vector2(0.5f, y), -200f, 360f, 34f, () => { _status.text = "Joining with the copied code..."; NetSession.Join(); });
            y -= 0.075f;
            MenuKit.Button(_root.transform, "Back", new Vector2(0.5f, y), 0f, 360f, 34f, Close);
            MenuKit.Text("Hint", _root.transform, "Join With Code reads a code you copied (a private party) · Esc / B to go back", 22, new Vector2(0.5f, 0.05f), new Vector2(1400, 40)).alpha = 0.6f;
            _root.SetActive(false);
        }
    }
}
