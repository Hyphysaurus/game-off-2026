using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A sleeping core that says nothing at first (D-0215, the hallway's dark stretch as the test of what the bedroom taught):
    /// its breath and hum are the only clues. If the player lingers near it while it sleeps (Linger seconds in all, within
    /// Near of it, measured in the player's own size) the old line comes back: as the core's prompt, and once as a tip.
    /// Woken first, it never speaks. Sits beside the ShiftSource.
    /// </summary>
    [RequireComponent(typeof(ShiftSource))]
    public sealed class SleepHint : MonoBehaviour
    {
        [SerializeField, Tooltip("The line that comes back (the core's prompt while it sleeps, and a tip once).")] private string line = "Dark. Shine your light on it.";
        [SerializeField, Min(0.5f), Tooltip("Seconds, in all, the player spends near the sleeping core before the line comes back.")] private float linger = 20f;
        [SerializeField, Min(0.1f), Tooltip("How near counts, in toy units (grows with the player's size): the toy's camera hangs 6 behind it.")] private float near = 12f;
        [SerializeField, Tooltip("Also show the line once as a tip (Toasts), so a player standing back from the core sees it too.")] private bool tip = true;
        [SerializeField, Min(0.05f), Tooltip("Seconds between looks (not every frame).")] private float lookEvery = 0.25f;

        private ShiftSource _core;
        private Shifter _player;
        private float _waited, _next;
        private bool _said;

        /// <summary>True once the line has come back.</summary>
        public bool Said => _said;
        /// <summary>Seconds lingered so far (the maker's report and a tester's look).</summary>
        public float Waited => _waited;

        private void Awake() => _core = GetComponent<ShiftSource>();

        private void Update()
        {
            if (_said || _core == null || Time.time < _next) return;
            float step = lookEvery;
            _next = Time.time + lookEvery;
            if (!_core.Dormant) { enabled = false; return; }   // woken: nothing to say
            if (_player == null)
            {
                GameObject own = GameObject.Find("Player");   // this PC's own player, as the prompt HUD finds it
                if (own == null || !own.TryGetComponent(out _player)) return;
            }
            float size = Mathf.Max(0.01f, _player.Scale / Mathf.Max(0.01f, _player.ScaleOf(ShiftSize.Toy)));
            if ((_player.transform.position - transform.position).sqrMagnitude > near * size * near * size) return;
            _waited += step;
            if (_waited < linger) return;
            _said = true;
            _core.DormantPrompt = line;
            if (tip && !string.IsNullOrEmpty(line)) Toasts.Show(line, ToastKind.Tip);
            enabled = false;
        }
    }
}
