using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// A team match on screen (D-0182), a part beside TeamMatch on the same object (PartyBuilder adds both to the
    /// Stadium; any scene with a TeamMatch can have one).
    ///  - Top left: the two teams' scores with the clock between them, and under them what the match is played to and
    ///    this PC's own team. In warm-up: how a match starts (the host's key, or "waiting for the host").
    ///  - The middle: the countdown's "3, 2, 1" with the team you are on, then "GO!" for a moment.
    ///  - The end: a card with who won, the score, how this PC's team did, and the seconds until free roam.
    /// The scores and the countdown stand on a dark backing sized to their words: a team's colour must read over a blue
    /// sky as well as over the field (Blue's score was all but lost on the Stadium's sky without it).
    /// It reads TeamMatch's static surface and decides nothing. Nothing shows offline or in a scene with no match part.
    /// The kill feed and the Tab board stay NetScoreboard's.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        [Header("Wording")]
        [SerializeField, Tooltip("Top left in warm-up, on the host's PC. {0} is the start key.")] private string hostHint = "Press {0} to start a team match";
        [SerializeField, Tooltip("Top left in warm-up, on everyone else's PC.")] private string friendHint = "Waiting for the host to start a team match";
        [SerializeField, Tooltip("In the middle as play starts.")] private string goWord = "GO!";
        [SerializeField, Tooltip("The card's title when the teams are level.")] private string drawWord = "DRAW";
        [SerializeField, Tooltip("The card's title when a team won. {0} is the team's name.")] private string winsWord = "{0} WINS";
        [SerializeField, Tooltip("On the card when this PC's team won.")] private string wonLine = "Your team won!";
        [SerializeField, Tooltip("On the card when this PC's team lost.")] private string lostLine = "Your team lost";
        [Header("Timing and size")]
        [SerializeField, Min(0f), Tooltip("Seconds \"GO!\" stays in the middle of the screen.")] private float goSeconds = 1.2f;
        [SerializeField, Min(8f), Tooltip("Size of the score line, top left.")] private float barSize = 40f;
        [SerializeField, Min(8f), Tooltip("Size of the countdown's number in the middle.")] private float bigSize = 150f;
        [SerializeField, Tooltip("The dark backing behind the scores and behind the countdown, so a team's colour reads over the sky and the field. Clear (alpha 0) = no backing.")] private Color backing = new Color(0.05f, 0.06f, 0.09f, 0.8f);
        [SerializeField, Tooltip("How far the backing reaches past the words, sideways and up and down.")] private Vector2 backingPad = new Vector2(18f, 12f);

        private const float RedrawSeconds = 0.1f;   // the clock and the countdown are redrawn this often
        private const int CanvasOrder = 44;         // just under the kill feed and the Tab board (45)

        [System.NonSerialized] private GameObject _root, _card;
        [System.NonSerialized] private TextMeshProUGUI _bar, _big, _cardText;
        [System.NonSerialized] private RectTransform _barBack, _bigBack;
        [System.NonSerialized] private MatchPhase _last = MatchPhase.None;
        [System.NonSerialized] private float _goUntil, _nextDraw;
        private readonly StringBuilder _sb = new StringBuilder(160);

        private void OnDisable() { if (_root != null) _root.SetActive(false); }

        private void Update()
        {
            MatchPhase phase = TeamMatch.Phase;
            bool show = phase != MatchPhase.None;
            if (show && _root == null) Build();
            if (_root == null) return;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (phase != _last)
            {
                // "GO!" only for a countdown this PC watched: a friend who joins mid-match is not told to go
                _goUntil = phase == MatchPhase.Playing && _last == MatchPhase.Countdown ? Time.unscaledTime + goSeconds : 0f;
                _last = phase;
                _nextDraw = 0f;
            }
            if (!show || Time.unscaledTime < _nextDraw) return;
            _nextDraw = Time.unscaledTime + RedrawSeconds;

            Set(_bar, Bar(phase));
            Set(_big, Big(phase));
            SizeBacks();
            bool card = phase == MatchPhase.Ended;
            if (_card.activeSelf != card) _card.SetActive(card);
            if (card) Set(_cardText, Card());
        }

        private static void Set(TextMeshProUGUI label, string text) { if (label.text != text) label.text = text; }

        /// <summary>Each backing fitted to the words it stands behind, and hidden when there are none.</summary>
        private void SizeBacks()
        {
            Fit(_barBack, _bar);
            Fit(_bigBack, _big);
        }

        private void Fit(RectTransform back, TextMeshProUGUI words)
        {
            bool any = backing.a > 0f && !string.IsNullOrEmpty(words.text);
            if (back.gameObject.activeSelf != any) back.gameObject.SetActive(any);
            if (!any) return;
            Vector2 size = words.GetPreferredValues(words.text) + backingPad * 2f;
            if ((back.sizeDelta - size).sqrMagnitude > 0.25f) back.sizeDelta = size;
        }

        // ---------------------------------------------------------------- the words

        /// <summary>Top left: the scores and the clock, or in warm-up how a match starts.</summary>
        private string Bar(MatchPhase phase)
        {
            _sb.Clear();
            if (phase == MatchPhase.Warmup)
            {
                _sb.Append("<size=62%><alpha=#BB>").Append(TeamMatch.IsHost ? string.Format(hostHint, TeamMatch.StartKeyName) : friendHint);
                return _sb.ToString();
            }
            int blue = TeamMatch.Blue, orange = TeamMatch.Orange;
            _sb.Append("<b>").Append(Tint(blue)).Append(Upper(blue)).Append(' ').Append(TeamMatch.Score(blue)).Append("</color></b>   ");
            _sb.Append(phase == MatchPhase.Playing ? Clock(TeamMatch.SecondsLeft) : phase == MatchPhase.Countdown ? "-:--" : "0:00");
            _sb.Append("   <b>").Append(Tint(orange)).Append(TeamMatch.Score(orange)).Append(' ').Append(Upper(orange)).Append("</color></b>");
            _sb.Append("\n<size=58%><alpha=#BB>First to ").Append(TeamMatch.ScoreToWin);
            int mine = TeamMatch.Mine;
            if (mine != TeamMatch.NoTeam) _sb.Append("     You: <alpha=#FF>").Append(Tint(mine)).Append(Upper(mine)).Append("</color>");
            return _sb.ToString();
        }

        /// <summary>The middle of the screen: the countdown's number with this PC's team, then "GO!" for a moment.</summary>
        private string Big(MatchPhase phase)
        {
            if (phase == MatchPhase.Countdown)
            {
                _sb.Clear();
                _sb.Append(Mathf.Max(1, Mathf.CeilToInt(TeamMatch.SecondsLeft)));
                int mine = TeamMatch.Mine;
                if (mine != TeamMatch.NoTeam) _sb.Append("\n<size=26%>You are on ").Append(Tint(mine)).Append(Upper(mine)).Append("</color>");
                return _sb.ToString();
            }
            return phase == MatchPhase.Playing && Time.unscaledTime < _goUntil ? goWord : "";
        }

        /// <summary>The end card: who won, the score, how this PC's team did, the seconds until free roam.</summary>
        private string Card()
        {
            int blue = TeamMatch.Blue, orange = TeamMatch.Orange, winner = TeamMatch.Winner, mine = TeamMatch.Mine;
            _sb.Clear();
            _sb.Append("<size=170%><b>");
            if (winner == TeamMatch.NoTeam) _sb.Append(drawWord);
            else _sb.Append(Tint(winner)).Append(string.Format(winsWord, Upper(winner))).Append("</color>");
            _sb.Append("</b></size>\n").Append(Gap);
            _sb.Append(Tint(blue)).Append(TeamMatch.Name(blue)).Append(' ').Append(TeamMatch.Score(blue)).Append("</color>");
            _sb.Append("   :   ");
            _sb.Append(Tint(orange)).Append(TeamMatch.Score(orange)).Append(' ').Append(TeamMatch.Name(orange)).Append("</color>\n");
            if (mine != TeamMatch.NoTeam && winner != TeamMatch.NoTeam) _sb.Append(mine == winner ? wonLine : lostLine).Append('\n');
            _sb.Append(Gap).Append("<size=70%><alpha=#BB>Free roam in ").Append(Mathf.Max(0, Mathf.CeilToInt(TeamMatch.SecondsLeft)));
            return _sb.ToString();
        }

        private const string Gap = "<size=45%> </size>\n";   // a low line of nothing: breathing room on the card

        private static string Tint(int team) => "<color=#" + ColorUtility.ToHtmlStringRGB(TeamMatch.Colour(team)) + ">";
        private static string Upper(int team) => TeamMatch.Name(team).ToUpperInvariant();

        /// <summary>Seconds as minutes and seconds: 161 is "2:41".</summary>
        private static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        // ---------------------------------------------------------------- the screen, made once

        private void Build()
        {
            _root = new GameObject("MatchCanvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = CanvasOrder;
            var scaler = _root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;   // by screen height (D-0133)

            // Top left: the toasts and a door's countdown own the top middle, the kill feed the top right.
            // Each backing is made before its words, so it is drawn behind them; SizeBacks fits it to them.
            var bar = BuildUiKit.Rect("Score", _root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f + backingPad.x, -24f - backingPad.y), new Vector2(900f, 130f));
            _barBack = BuildUiKit.Rect("Back", bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-backingPad.x, backingPad.y), Vector2.zero);
            BuildUiKit.Panel(_barBack, backing).raycastTarget = false;
            _bar = Words(bar, barSize, TextAlignmentOptions.TopLeft);

            var big = BuildUiKit.Rect("Countdown", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 320f));
            _bigBack = BuildUiKit.Rect("Back", big, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildUiKit.Panel(_bigBack, backing).raycastTarget = false;
            _big = Words(big, bigSize, TextAlignmentOptions.Center);

            var card = BuildUiKit.Rect("Card", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(820f, 380f));
            BuildUiKit.Panel(card, BuildUiKit.PanelColor);
            var inner = BuildUiKit.Fill("Text", card, 30f, 24f, 30f, 24f);
            _cardText = Words(inner, 40f, TextAlignmentOptions.Center);
            _card = card.gameObject;
            _card.SetActive(false);
        }

        private static TextMeshProUGUI Words(Transform parent, float size, TextAlignmentOptions align)
        {
            TextMeshProUGUI label = BuildUiKit.Label(parent, "", size, align);
            label.outlineWidth = 0.2f; label.outlineColor = new Color32(0, 0, 0, 200);
            label.raycastTarget = false;
            return label;
        }
    }
}
