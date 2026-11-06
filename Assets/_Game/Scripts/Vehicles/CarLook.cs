using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The swappable parts of a modular car (the Cyber Racing pack builds every car from parts: Car_05_FrontBumper_Simple,
    /// Car_05_FrontBumper_Cool, Car_05_Spoiler...). Parts are found by name under the car, grouped into slots, and a slot
    /// shows one of its options (or none). Wheels swap to any of the pack's wheel sets, mounted on the car's own hubs the
    /// way the pack's demo rig mounts them, so the ArcadeCar keeps spinning and steering them. The Garage drives this;
    /// nothing else needs to.
    /// </summary>
    public sealed class CarLook : MonoBehaviour
    {
        public sealed class Slot
        {
            public string Name;
            public readonly List<GameObject> Options = new List<GameObject>();
            public readonly List<string> Labels = new List<string>();
            public int Current = -1;   // -1 = none shown
            public int Count => Options.Count + 1;   // the options plus "none"
            public string CurrentLabel => Current < 0 ? "None" : Labels[Current];
        }

        private static readonly Regex PartName = new Regex(@"^Car_\d+_(?<slot>[A-Za-z]+?)(_(?<variant>Simple|Cool))?$");

        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<Transform> _hubs = new List<Transform>();
        private readonly List<Renderer> _stockWheels = new List<Renderer>();
        private readonly List<GameObject> _fitted = new List<GameObject>();
        private bool _found;

        public IReadOnlyList<Slot> Slots { get { Find(); return _slots; } }
        /// <summary>-1 = the car's own wheels, else an index into the wheel list the garage passes in.</summary>
        public int WheelIndex { get; private set; } = -1;

        private void Find()
        {
            if (_found) return;
            _found = true;
            // Simple/Cool versions of a part share a slot; a part with no version is On/None in a slot of its own. A bare
            // part beside Simple/Cool versions of the same name is a different piece (Car 12's roof light by its headlights).
            var bySlot = new Dictionary<string, Slot>();
            var bare = new List<KeyValuePair<string, Slot>>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                var m = PartName.Match(t.name);
                if (!m.Success) continue;
                string slot = m.Groups["slot"].Value;
                if (slot == "Body" || slot.StartsWith("Wheel") || slot.StartsWith("Steering")) continue;
                bool variant = m.Groups["variant"].Success;
                string key = variant ? slot : slot + "#";
                if (!bySlot.TryGetValue(key, out var s))
                {
                    s = new Slot { Name = Pretty(slot) };
                    bySlot.Add(key, s);
                    _slots.Add(s);
                    if (!variant) bare.Add(new KeyValuePair<string, Slot>(slot, s));
                }
                s.Options.Add(t.gameObject);
                s.Labels.Add(variant ? m.Groups["variant"].Value : "On");
            }
            foreach (var b in bare) if (bySlot.ContainsKey(b.Key)) b.Value.Name += " (extra)";
            foreach (var s in _slots)
            {
                s.Current = -1;
                for (int i = 0; i < s.Options.Count; i++) if (s.Options[i].activeSelf) { if (s.Current < 0) s.Current = i; else s.Options[i].SetActive(false); }
            }
            var car = GetComponent<ArcadeCar>();
            if (car != null) _hubs.AddRange(car.Wheels);
            foreach (var h in _hubs) _stockWheels.AddRange(h.GetComponentsInChildren<Renderer>(true));
        }

        private static string Pretty(string s) => Regex.Replace(s, "(?<=[a-z])(?=[A-Z])", " ");

        /// <summary>Steps a slot to its next (or previous) option, through "none".</summary>
        public void Step(Slot slot, int dir)
        {
            Find();
            int i = slot.Current + 1;   // 0 = none
            i = ((i + dir) % slot.Count + slot.Count) % slot.Count;
            Show(slot, i - 1);
        }

        private static void Show(Slot slot, int option)
        {
            slot.Current = option;
            for (int k = 0; k < slot.Options.Count; k++) slot.Options[k].SetActive(k == option);
        }

        /// <summary>Fits a pack wheel on every hub (index into <paramref name="wheels"/>), or the car's own with -1.</summary>
        public void SetWheels(int index, GameObject[] wheels)
        {
            Find();
            foreach (var f in _fitted) if (f != null) { f.SetActive(false); Destroy(f); }   // hidden now; Destroy waits for the frame's end
            _fitted.Clear();
            bool stock = wheels == null || index < 0 || index >= wheels.Length || wheels[index] == null;
            foreach (var r in _stockWheels) if (r != null) r.enabled = stock;
            WheelIndex = stock ? -1 : index;
            if (!stock)
                foreach (var hub in _hubs)
                {
                    var w = Instantiate(wheels[index], hub, false);
                    w.name = "Fitted " + wheels[index].name;
                    foreach (var c in w.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                    // The pack's wheel sets turn on their own Z axis, the cars' hubs on X. The pack's demo rig mounts them a
                    // quarter turn round, -90 on the left and +90 on the right, which also faces the rim outward.
                    float side = transform.InverseTransformPoint(hub.position).x;
                    w.transform.localRotation = Quaternion.Euler(0f, side >= 0f ? 90f : -90f, 0f);
                    _fitted.Add(w);
                }
            var car = GetComponent<ArcadeCar>();
            if (car != null) car.RefreshParts();
        }

        /// <summary>
        /// Takes another car's choices (a garage body swap): its wheel set, and the same Simple/Cool/On/None in every slot
        /// both cars have. A choice this car has no version of leaves that slot as it is.
        /// </summary>
        public void CopyFrom(CarLook from, GameObject[] wheels)
        {
            if (from == null) return;
            Find(); from.Find();
            foreach (var s in _slots)
            {
                Slot match = null;
                foreach (var o in from._slots) if (o.Name == s.Name) { match = o; break; }
                if (match == null) continue;
                if (match.Current < 0) { Show(s, -1); continue; }
                int pick = s.Labels.IndexOf(match.Labels[match.Current]);
                if (pick >= 0) Show(s, pick);
            }
            if (from.WheelIndex >= 0) SetWheels(from.WheelIndex, wheels);
        }
    }
}
