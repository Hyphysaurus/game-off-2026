using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A squad holds a way out (D-0103, the Tan Splinter's kitchen): once every soldier in it has been knocked down at
    /// least once (they get up again, as soldiers do; one fall each is enough) its doors open and the player hears it on
    /// the HUD. Soldiers are found under this object if none are given.
    /// </summary>
    public sealed class SquadGate : MonoBehaviour
    {
        [SerializeField] private ToySoldier[] soldiers = new ToySoldier[0];
        [SerializeField] private Door[] doors = new Door[0];
        [SerializeField, Tooltip("Said on the HUD when the last one falls. Placeholder wording.")] private string routed = "The Tan Splinter is routed. A way home is open.";
        [SerializeField, Tooltip("Said when one falls and some still stand. {0} = how many still stand.")] private string left = "{0} to go";
        [SerializeField] private UnityEvent onRouted;

        private readonly HashSet<ToySoldier> _fallen = new HashSet<ToySoldier>();
        private readonly List<(Damageable hp, System.Action<DamageInfo> on)> _hooks = new List<(Damageable, System.Action<DamageInfo>)>();
        private bool _open;

        /// <summary>Every soldier has fallen at least once.</summary>
        public bool Routed => _open;

        private void Awake()
        {
            if (soldiers == null || soldiers.Length == 0) soldiers = GetComponentsInChildren<ToySoldier>(true);
        }

        private void OnEnable()
        {
            foreach (var s in soldiers)
            {
                if (s == null || !s.TryGetComponent(out Damageable hp)) continue;
                var soldier = s;
                System.Action<DamageInfo> on = _ => Fell(soldier);
                hp.Died += on;
                _hooks.Add((hp, on));
            }
        }

        private void OnDisable()
        {
            foreach (var (hp, on) in _hooks) if (hp != null) hp.Died -= on;
            _hooks.Clear();
        }

        private void Fell(ToySoldier s)
        {
            if (_open || !_fallen.Add(s)) return;
            int standing = 0; foreach (var x in soldiers) if (x != null && !_fallen.Contains(x)) standing++;
            var charge = FindFirstObjectByType<ShiftCharge>();
            if (standing > 0) { if (charge != null && !string.IsNullOrEmpty(left)) charge.ShowHint(string.Format(left, standing)); return; }
            _open = true;
            foreach (var d in doors) if (d != null) d.Open();
            if (charge != null) charge.ShowHint(routed);
            onRouted?.Invoke();
        }
    }
}
