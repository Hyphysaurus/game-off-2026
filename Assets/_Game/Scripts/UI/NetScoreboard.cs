using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// Online scores (D-0118): a kill feed top right ("Sam knocked out Mario", in their colours, gone after a few
    /// seconds) and, while Tab is held, a board of every player: knock-outs, times knocked out, and this PC's ping.
    /// Every PC hears every knock-out (NetHealth.KnockedOut), so each keeps the same tally with nothing more sent; a
    /// player who joins late counts from their joining. Made by NetSession; shows only in the party scene.
    /// </summary>
    public sealed class NetScoreboard : MonoBehaviour
    {
        private const int FeedLines = 5;
        private const float FeedSeconds = 6f;

        private readonly Dictionary<ulong, int> _kos = new Dictionary<ulong, int>(), _downs = new Dictionary<ulong, int>();
        private readonly List<(string text, float until)> _feed = new List<(string, float)>();
        private GameObject _root, _board;
        private TextMeshProUGUI _feedText, _boardText;
        private float _nextBoard;

        private void OnEnable() => NetHealth.KnockedOut += OnKnockedOut;
        private void OnDisable() => NetHealth.KnockedOut -= OnKnockedOut;

        /// <summary>Forget the tally (a new party).</summary>
        public void Clear() { _kos.Clear(); _downs.Clear(); _feed.Clear(); }

        private void OnKnockedOut(ulong victim, ulong by)
        {
            _downs[victim] = (_downs.TryGetValue(victim, out int d) ? d : 0) + 1;
            string v = Coloured(victim);
            string line;
            if (by != NetHealth.NoOne && by != victim)
            {
                _kos[by] = (_kos.TryGetValue(by, out int k) ? k : 0) + 1;
                line = Coloured(by) + " knocked out " + v;
            }
            else line = v + " was knocked out";
            _feed.Add((line, Time.unscaledTime + FeedSeconds));
            while (_feed.Count > FeedLines) _feed.RemoveAt(0);
            RefreshFeed();
        }

        private static string Coloured(ulong id) => "<color=#" + ColorUtility.ToHtmlStringRGB(NetNameTag.ColourOf(id)) + ">" + NetNameTag.NameOf(id) + "</color>";

        private void Update()
        {
            bool show = NetSession.Online && NetSession.InParty;   // any online world (D-0125)
            if (show && _root == null) Build();
            if (_root == null) return;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;
            if (_feed.Count > 0 && _feed[0].until < Time.unscaledTime) { _feed.RemoveAt(0); RefreshFeed(); }
            var k = Keyboard.current;
            bool held = k != null && k.tabKey.isPressed && !PauseMenu.IsOpen;
            if (_board.activeSelf != held) _board.SetActive(held);
            if (held && Time.unscaledTime >= _nextBoard) { _nextBoard = Time.unscaledTime + 0.25f; RefreshBoard(); }
        }

        private void RefreshFeed()
        {
            if (_feedText == null) return;
            var sb = new StringBuilder();
            foreach (var f in _feed) sb.AppendLine(f.text);
            _feedText.text = sb.ToString();
        }

        private void RefreshBoard()
        {
            var nm = NetworkManager.Singleton;
            var sb = new StringBuilder();
            sb.AppendLine("<b>PARTY " + (NetSession.Code ?? "") + "</b>\n");
            sb.AppendLine("<alpha=#88>player                 knock-outs   knocked out</alpha>");
            var ids = new List<ulong>();
            foreach (var t in FindObjectsByType<NetNameTag>(FindObjectsSortMode.None)) if (t.IsSpawned) ids.Add(t.OwnerClientId);
            ids.Sort((a, b) => (_kos.TryGetValue(b, out int kb) ? kb : 0).CompareTo(_kos.TryGetValue(a, out int ka) ? ka : 0));
            foreach (ulong id in ids)
            {
                int ko = _kos.TryGetValue(id, out int a1) ? a1 : 0, down = _downs.TryGetValue(id, out int a2) ? a2 : 0;
                string me = nm != null && id == nm.LocalClientId ? "  <alpha=#88>(you)</alpha>" : "";
                sb.AppendLine(Coloured(id).PadRight(0) + me + "<pos=55%>" + ko + "<pos=80%>" + down);
            }
            sb.AppendLine("\n<alpha=#88>ping " + NetSession.PingMs + " ms</alpha>");
            _boardText.text = sb.ToString();
        }

        private void Build()
        {
            _root = new GameObject("ScoreCanvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 45;
            var scaler = _root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;   // by screen height (D-0133)

            var feed = BuildUiKit.Rect("Feed", _root.transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-30f, -24f), new Vector2(700f, 220f));
            _feedText = BuildUiKit.Label(feed, "", 26, TextAlignmentOptions.TopRight);
            _feedText.outlineWidth = 0.2f; _feedText.outlineColor = new Color32(0, 0, 0, 200);

            var board = BuildUiKit.Rect("Board", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 420f));
            BuildUiKit.Panel(board, BuildUiKit.PanelColor);
            var inner = BuildUiKit.Fill("Text", board, 30f, 24f, 30f, 24f);
            _boardText = BuildUiKit.Label(inner, "", 28, TextAlignmentOptions.TopLeft);
            _board = board.gameObject;
            _board.SetActive(false);
            RefreshFeed();
        }
    }
}
