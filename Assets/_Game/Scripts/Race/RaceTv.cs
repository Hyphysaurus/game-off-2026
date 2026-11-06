using System.Text;
using TMPro;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The stadium TV's race readout (D-0037), the same on every screen. Before a race: how to start one. Running: the lap
    /// (and the gates passed this lap), this lap's time, big, and the best lap. After the last lap: FINISH!, the total
    /// time and the best lap, until the next race, a stop or F9. Crossing the line with a gate missed shows, for a moment,
    /// which gate comes first. It only reads the race (RaceState); the text is built without garbage every frame.
    /// </summary>
    public sealed class RaceTv : MonoBehaviour, IResettable
    {
        [SerializeField, Tooltip("The screens' text (TextMeshPro); all show the same.")] private TMP_Text[] screens;
        [SerializeField] private Color textColour = new Color(1f, 0.96f, 0.86f);
        [SerializeField, Tooltip("The finish screen.")] private Color finishColour = new Color(1f, 0.8f, 0.25f);
        [SerializeField, Tooltip("The missed-gate line.")] private Color warnColour = new Color(1f, 0.45f, 0.2f);
        [SerializeField, Min(0f), Tooltip("Seconds the missed-gate line stays up.")] private float warnSeconds = 2f;

        private readonly StringBuilder _text = new StringBuilder(160);
        private string _warnTag;
        private bool _finished, _dirty = true;
        private int _missedGate;
        private float _warnUntil;

        private void OnEnable()
        {
            _warnTag = "<color=#" + ColorUtility.ToHtmlStringRGB(warnColour) + ">";
            RaceState.Started += OnStarted;
            RaceState.Finished += OnFinished;
            RaceState.Stopped += OnStopped;
            RaceLoop.Missed += OnMissed;
            _dirty = true;
        }

        private void OnDisable()
        {
            RaceState.Started -= OnStarted;
            RaceState.Finished -= OnFinished;
            RaceState.Stopped -= OnStopped;
            RaceLoop.Missed -= OnMissed;
        }

        private void OnStarted() { _finished = false; _warnUntil = 0f; _dirty = true; }
        private void OnFinished() { _finished = true; _dirty = true; }
        private void OnStopped() { _finished = false; _dirty = true; }
        private void OnMissed(int gate) { _missedGate = gate; _warnUntil = Time.time + warnSeconds; }

        public void ResetState() { _finished = false; _warnUntil = 0f; _dirty = true; }

        private void Update()
        {
            if (screens == null || screens.Length == 0) return;
            bool running = RaceState.Running;
            if (!running && !_dirty) return;   // the start and finish screens change only on an event
            _dirty = false;
            _text.Clear();
            Color colour = textColour;
            if (running) WriteRunning();
            else if (_finished) { WriteFinish(); colour = finishColour; }
            else _text.Append("DRIVE THROUGH\n<size=160%>THE FINISH</size>\nTO START");
            foreach (var s in screens)
            {
                if (s == null) continue;
                s.SetText(_text);
                if (s.color != colour) s.color = colour;
            }
        }

        private void WriteRunning()
        {
            if (Time.time < _warnUntil)
            {
                _text.Append(_warnTag).Append("GATE ");
                AppendNumber(_missedGate);
                _text.Append(" FIRST</color>");
            }
            else
            {
                if (RaceState.Laps > 1 && RaceState.Lap >= RaceState.Laps) _text.Append("FINAL LAP");
                else
                {
                    _text.Append("LAP ");
                    AppendNumber(RaceState.Lap);
                    _text.Append('/');
                    AppendNumber(RaceState.Laps);
                }
                if (RaceState.Gates > 0)
                {
                    _text.Append("  GATE ");
                    AppendNumber(RaceState.Gate);
                    _text.Append('/');
                    AppendNumber(RaceState.Gates);
                }
            }
            _text.Append("\n<size=160%>");
            AppendTime(RaceState.LapTime);
            _text.Append("</size>\nBEST ");
            if (RaceState.BestLap > 0f) AppendTime(RaceState.BestLap);
            else _text.Append("-:--.--");
        }

        private void WriteFinish()
        {
            _text.Append("FINISH!\n<size=160%>");
            AppendTime(RaceState.TotalTime);
            _text.Append("</size>\nBEST LAP ");
            AppendTime(RaceState.BestLap);
        }

        /// <summary>m:ss.hh, digit by digit (no strings made).</summary>
        private void AppendTime(float seconds)
        {
            int hundredths = Mathf.Max(0, Mathf.FloorToInt(seconds * 100f));
            int s = hundredths / 100 % 60, h = hundredths % 100;
            AppendNumber(hundredths / 6000);
            _text.Append(':').Append((char)('0' + s / 10)).Append((char)('0' + s % 10))
                 .Append('.').Append((char)('0' + h / 10)).Append((char)('0' + h % 10));
        }

        private void AppendNumber(int n)
        {
            if (n < 0) { _text.Append('-'); n = -n; }
            if (n >= 10) AppendNumber(n / 10);
            _text.Append((char)('0' + n % 10));
        }
    }
}
