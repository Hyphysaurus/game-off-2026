using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A race checkpoint arch (D-0037). Drive through its opening (this object's trigger) during a race and it counts if
    /// it is the next gate: the race takes gates by their numbers, lowest first, and gates sharing a number are
    /// alternatives (either one counts). Its signs show the number: lit while it is the gate to drive through next, green
    /// once passed this lap. Build Mode's catalogue has one piece per number (RaceGate_1, RaceGate_2, ...).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RaceGate : MonoBehaviour
    {
        private enum Look { Idle, Next, Passed }

        [SerializeField, Min(1), Tooltip("Its place in the race, 1 first. Gates with the same number are alternatives.")] private int order = 1;
        [SerializeField, Tooltip("The number signs (TextMeshPro), front and back.")] private TMP_Text[] signs;
        [SerializeField] private Color idleColour = Color.white;
        [SerializeField, Tooltip("While it is the gate to drive through next.")] private Color nextColour = new Color(1f, 0.8f, 0.15f);
        [SerializeField, Tooltip("Once passed this lap.")] private Color passedColour = new Color(0.4f, 0.95f, 0.45f);

        private static readonly List<RaceGate> Registry = new List<RaceGate>();
        private Look _shown;
        private bool _shownSet;

        /// <summary>Every gate that is switched on (Build Mode hides deleted ones, which leave the list).</summary>
        public static IReadOnlyList<RaceGate> All => Registry;
        public int Order => order;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Registry.Clear();

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            ShowNumber();
        }

        private void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
            _shownSet = false;
        }

        private void OnDisable() { Registry.Remove(this); }

        private void OnTriggerEnter(Collider other)
        {
            if (GameMode.Current != GameMode.Mode.Play) return;
            if (!RaceTouch.IsDriven(other)) { RaceLoop.RivalThrough(this, other); return; }   // a rival of the field (D-0061)
            if (RaceState.Running) RaceLoop.Through(this);
        }

        private void Update()
        {
            Look look = Look.Idle;
            if (RaceState.Running && RaceLoop.Active != null)
            {
                int next = RaceLoop.NextGate;
                if (order == next) look = Look.Next;
                else if (next == 0 || order < next) look = Look.Passed;   // 0: every gate of this lap is behind the driver
            }
            if (_shownSet && look == _shown) return;
            _shown = look; _shownSet = true;
            Color c = look == Look.Next ? nextColour : look == Look.Passed ? passedColour : idleColour;
            if (signs == null) return;
            foreach (var s in signs) if (s != null) s.color = c;
        }

        private void ShowNumber()
        {
            if (signs == null) return;
            string n = order.ToString();
            foreach (var s in signs) if (s != null && s.text != n) s.text = n;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (order < 1) order = 1;
            // TextMeshPro must not be changed inside OnValidate: the sign follows a number typed in the Inspector a moment later.
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) ShowNumber(); };
        }
#endif
    }
}
