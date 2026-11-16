using TMPro;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The driving HUD's parts for a race with a field (D-0061), drawn on the driving HUD's own canvas in its own style:
    /// the place ("2/4") beside the lap panel; the count on the grid (3, 2, 1, GO!) in the upper middle of the screen; the
    /// prompt that offers a race from the grid ("RACE  PRESS R") while sitting in a car with no race on; and the results
    /// card when the player finishes (place, name and time for every car; a time still being driven is shown dimmer,
    /// as the time that car is on course for, and is replaced when it crosses the line). It only reads RaceField and
    /// RaceGrid. DrivingHud makes it, ticks it and lends it its fonts, colours and panel shapes.
    /// </summary>
    internal sealed class RaceHud
    {
        private const int Rows = 4;                       // cars on the results card
        private const float RowHeight = 50f, CardWidth = 600f, CardTop = 96f;
        private const float CountSize = 190f, GoSize = 140f;

        private readonly DrivingHud _hud;
        private readonly CanvasGroup _place, _count, _prompt, _card;
        private readonly RectTransform _countRt, _promptRt, _cardRt;
        private readonly TextMeshProUGUI _placeValue, _countText, _promptText;
        private readonly TextMeshProUGUI[] _rowPlace = new TextMeshProUGUI[Rows], _rowName = new TextMeshProUGUI[Rows], _rowTime = new TextMeshProUGUI[Rows];
        private readonly GameObject[] _rows = new GameObject[Rows];
        private readonly Racer[] _rowRacer = new Racer[Rows];
        private readonly int[] _rowCs = new int[Rows];
        private readonly bool[] _rowDim = new bool[Rows];

        private int _placeA = -1, _placeB = -1, _countShown = -1;
        private float _countAt, _promptAt, _cardAt;
        private bool _promptPad, _promptSet, _cardWas;
        private RaceField.Stage _stageWas;

        /// <summary>Something of it is on the screen.</summary>
        public bool Visible => _place.alpha > 0f || _count.alpha > 0f || _prompt.alpha > 0f || _card.alpha > 0f;

        public RaceHud(DrivingHud hud, Transform canvas)
        {
            _hud = hud;
            Vector2 centre = new Vector2(0.5f, 0.5f), left = new Vector2(0f, 1f), leftPivot = new Vector2(0f, 0.5f), rightPivot = new Vector2(1f, 0.5f);

            // the place, left of the lap panel (which is 340 wide, 36 from the right edge)
            var place = DrivingHud.At("Place", canvas, Vector2.one, Vector2.one, new Vector2(-36f - 340f - 10f, -30f), new Vector2(170f, 146f));
            hud.Round(place, BuildUiKit.PanelColor, 18f);
            _place = DrivingHud.Group(place);
            hud.Label(DrivingHud.At("Label", place, left, leftPivot, new Vector2(20f, -22f), new Vector2(130f, 20f)), 16f, false, TextAlignmentOptions.CaplineLeft, hud.LabelColour).SetText("PLACE");
            _placeValue = hud.Label(DrivingHud.At("Value", place, left, leftPivot, new Vector2(18f, -84f), new Vector2(140f, 90f)), 86f, true, TextAlignmentOptions.CaplineLeft, Color.white);

            // the count, in the middle
            _countRt = DrivingHud.At("Count", canvas, centre, centre, new Vector2(0f, 250f), new Vector2(300f, 230f));   // in the upper half: the cars ahead stay in view
            hud.Round(_countRt, BuildUiKit.PanelColor, 50f);
            _count = DrivingHud.Group(_countRt);
            _countText = hud.Label(DrivingHud.At("Number", _countRt, centre, centre, new Vector2(0f, 6f), new Vector2(290f, 220f)), CountSize, true, TextAlignmentOptions.Capline, Color.white);

            // the offer of a race, where the lap panel will be
            _promptRt = DrivingHud.At("RacePrompt", canvas, Vector2.one, Vector2.one, new Vector2(-36f, -30f), new Vector2(340f, 54f));
            hud.Round(_promptRt, BuildUiKit.PanelColor, 27f);
            _prompt = DrivingHud.Group(_promptRt);
            _promptText = hud.Label(_promptRt, 30f, false, TextAlignmentOptions.Capline, hud.Accent);
            _promptText.fontStyle = FontStyles.Bold;
            _promptText.richText = true;

            // the results
            _cardRt = DrivingHud.At("Results", canvas, centre, centre, new Vector2(0f, 240f), new Vector2(CardWidth, CardTop + Rows * RowHeight + 22f));   // in the upper half, clear of the car
            hud.Round(_cardRt, BuildUiKit.PanelColor, 26f);
            _card = DrivingHud.Group(_cardRt);
            var title = hud.Label(DrivingHud.At("Title", _cardRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(CardWidth - 40f, 70f)), 60f, false, TextAlignmentOptions.Capline, hud.Gold);
            title.fontStyle = FontStyles.Bold;
            title.SetText("FINISH!");
            for (int i = 0; i < Rows; i++)
            {
                var row = DrivingHud.At("Row" + (i + 1), _cardRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -CardTop - i * RowHeight), new Vector2(CardWidth - 48f, RowHeight - 6f));
                _rows[i] = row.gameObject;
                _rowPlace[i] = hud.Label(DrivingHud.At("Place", row, new Vector2(0f, 0.5f), leftPivot, new Vector2(10f, 0f), new Vector2(50f, 40f)), 40f, true, TextAlignmentOptions.CaplineLeft, hud.LabelColour);
                _rowPlace[i].SetText("{0}", i + 1);
                _rowName[i] = hud.Label(DrivingHud.At("Name", row, new Vector2(0f, 0.5f), leftPivot, new Vector2(70f, 0f), new Vector2(280f, 32f)), 26f, false, TextAlignmentOptions.CaplineLeft, Color.white);
                _rowTime[i] = hud.Label(DrivingHud.At("Time", row, new Vector2(1f, 0.5f), rightPivot, new Vector2(-10f, 0f), new Vector2(200f, 40f)), 40f, true, TextAlignmentOptions.CaplineRight, Color.white);
                _rowCs[i] = -1;
            }
        }

        /// <summary>Once a frame, from the driving HUD. <paramref name="play"/>: Play mode with no garage menu up;
        /// <paramref name="driving"/>: the player sits in a vehicle; <paramref name="lapPanel"/>: how much of the lap panel shows.</summary>
        public void Tick(bool play, bool driving, float lapPanel, float dt, float now)
        {
            var stage = RaceField.Now;
            Racer me = RaceField.Player;
            int field = RaceField.Racers.Count;

            // place
            bool placed = play && me != null && field > 1 && (stage == RaceField.Stage.Racing || stage == RaceField.Stage.Done);
            _hud.Fade(_place, placed, dt);
            if (placed && (me.Place != _placeA || field != _placeB))
            {
                _placeA = me.Place; _placeB = field;
                _placeValue.SetText(_hud.FractionFormat, me.Place, field);
            }

            // the count, then GO! for a moment
            int count = stage == RaceField.Stage.Countdown ? RaceField.Count : 0;
            bool go = stage == RaceField.Stage.Racing && now - RaceField.WentAt < _hud.Rivals.goSeconds;
            int show = count > 0 ? count : go ? 0 : -1;
            if (show != _countShown)
            {
                _countShown = show;
                if (show >= 0)
                {
                    _countAt = now;
                    if (show > 0) { _countText.fontSize = CountSize; _countText.color = Color.white; _countText.SetText("{0}", show); }
                    else { _countText.fontSize = GoSize; _countText.color = _hud.BestColour; _countText.SetText("GO!"); }
                }
            }
            _hud.Fade(_count, play && show >= 0, dt);
            if (_count.alpha > 0f) _countRt.localScale = Vector3.one * DrivingHud.Punch(now - _countAt, 0.4f);

            // the offer: sitting in a car, no race on, nothing where the lap panel goes
            RaceGrid grid = RaceGrid.Current;
            bool offer = play && driving && !RaceState.Running && lapPanel <= 0.01f && grid != null && RaceStartKey.KeyName != null && grid.CanStart;
            if (offer)
            {
                bool pad = DrivingHud.UsingPad();
                if (!_promptSet || pad != _promptPad)
                {
                    _promptSet = true; _promptPad = pad;
                    _promptText.SetText("RACE  <size=70%>PRESS " + (pad ? RaceStartKey.PadName : RaceStartKey.KeyName) + "</size>");
                }
                if (_prompt.alpha <= 0f) _promptAt = now;
            }
            _hud.Fade(_prompt, offer, dt);
            if (_prompt.alpha > 0f) _promptRt.localScale = Vector3.one * DrivingHud.Punch(now - _promptAt, 0.6f);

            // the results, while the rest come home
            bool card = play && stage == RaceField.Stage.Done && field > 1;
            if (card)
            {
                if (!_cardWas || stage != _stageWas) _cardAt = now;
                FillCard();
            }
            _cardWas = card; _stageWas = stage;
            _hud.Fade(_card, card, dt);
            if (_card.alpha > 0f) _cardRt.localScale = Vector3.one * DrivingHud.Punch(now - _cardAt, 0.5f);
        }

        /// <summary>The rows, leader first; a row is rewritten only when what it shows has changed.</summary>
        private void FillCard()
        {
            var order = RaceField.Standings;
            for (int i = 0; i < Rows; i++)
            {
                bool used = i < order.Count;
                if (_rows[i].activeSelf != used) _rows[i].SetActive(used);
                if (!used) { _rowRacer[i] = null; continue; }
                Racer r = order[i];
                int cs = Mathf.Max(0, Mathf.FloorToInt(r.FinishTime * 100f));
                bool dim = !r.Finished;
                if (r == _rowRacer[i] && cs == _rowCs[i] && dim == _rowDim[i]) continue;
                _rowRacer[i] = r; _rowCs[i] = cs; _rowDim[i] = dim;
                Color ink = r.IsPlayer ? _hud.Gold : Color.white;
                _rowName[i].SetText(r.Name);
                _rowName[i].color = ink;
                _rowPlace[i].color = r.IsPlayer ? _hud.Gold : _hud.LabelColour;
                _rowTime[i].SetText(_hud.TimeFormat, cs / 6000, cs / 100 % 60, cs % 100);
                _rowTime[i].color = dim ? new Color(ink.r, ink.g, ink.b, 0.45f) : ink;
            }
        }
    }
}
