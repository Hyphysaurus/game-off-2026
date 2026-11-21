using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What the player carries, in slots (D-0108, his "when picking up the light, it should become an item in my
    /// inventory that i can hot swap in and out"). One bar for everything held in a hand: the weapons (the
    /// WeaponHolder's) and the hand tools (the Carrier's one-handed carryables: the flashlight).
    /// - Size is the bag: a toy has the first two slots, a kid all four (the kid's backpack). Things in a slot the size
    ///   cannot use are kept but greyed: slots 3 and 4 at toy size, and anything kid-only (a flashlight) at toy size.
    /// - 1 to 4 take that slot's thing out (pressed again: empty hands); the mouse wheel and the pad's D-pad left and
    ///   right step through the usable ones. X still holsters a weapon or sets a tool down in the world (it leaves the
    ///   bar); Z drops a weapon.
    /// - Picking something up puts it in the first free slot and in the hand. A full bar refuses a tool; a weapon
    ///   swaps for the one in hand, as before.
    /// - A shift stows a tool in its slot instead of dropping it (it used to fall out of the hand, D-0041).
    /// Big things (planks, boxes) are not in the bar: they stay in both hands, as carried. The paper plane is not
    /// either: it is always ready (Jump in the air). Added by the WeaponHolder to a player.
    /// </summary>
    [DefaultExecutionOrder(90)]   // before the WeaponHolder (100), after the Carrier
    public sealed class Inventory : MonoBehaviour
    {
        /// <summary>One slot's thing: a weapon or a hand tool.</summary>
        public sealed class Item
        {
            public Weapon Weapon;
            public Carryable Tool;
            public string Name => Weapon != null ? Weapon.DisplayName : Tool != null ? Tool.DisplayName : "";
            public Texture2D Icon => Weapon != null ? Weapon.Icon : Tool != null ? Tool.Icon : null;
            public bool KidOnly => Tool != null && Tool.KidOnly;
            public bool Gone => Weapon == null && Tool == null;
        }

        [SerializeField, Min(1), Tooltip("Slots a toy can use.")] private int toySlots = 2;
        [SerializeField, Min(1), Tooltip("Slots a kid can use (a backpack).")] private int kidSlots = 4;
        [SerializeField, Tooltip("Said when a tool is picked up with the usable slots full.")] private string fullText = "Hands full: press a slot's key again to free a hand, or X to set a tool down";
        [SerializeField, Tooltip("Said when a slot the size cannot use is pressed.")] private string tooBigText = "Too big for a toy";

        private readonly List<Item> _items = new List<Item>();
        private PlayerInput _input;
        private Shifter _shifter;
        private WeaponHolder _holder;
        private Carrier _carrier;

        /// <summary>Raised whenever what is in the slots, or which is in hand, may have changed.</summary>
        public event Action Changed;

        public IReadOnlyList<Item> Items => _items;
        public int MostSlots => Mathf.Max(toySlots, kidSlots);
        /// <summary>How many slots the player's size can use now.</summary>
        public int Usable => _shifter != null && _shifter.Size == ShiftSize.Toy ? toySlots : kidSlots;
        /// <summary>No free slot the size can use.</summary>
        public bool Full => _items.Count >= Usable;

        /// <summary>The slot whose thing is in the hand now, or -1 with empty hands.</summary>
        public int InHand
        {
            get
            {
                if (_carrier != null && _carrier.Held != null) return _items.FindIndex(i => i.Tool == _carrier.Held);
                if (_holder != null && _holder.Armed && _holder.Current != null) return _items.FindIndex(i => i.Weapon == _holder.Current);
                return -1;
            }
        }

        /// <summary>True when the slot's thing can be taken out at the player's size.</summary>
        public bool CanUse(int slot)
        {
            if (slot < 0 || slot >= _items.Count) return false;
            if (slot >= Usable) return false;
            return !(_items[slot].KidOnly && _shifter != null && _shifter.Size != ShiftSize.Kid);
        }

        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            _shifter = GetComponent<Shifter>();
            _holder = GetComponent<WeaponHolder>();
            _carrier = GetComponent<Carrier>();
        }

        private void Start() { if (_input != null) HotbarHud.Create(this); }

        private void OnEnable()
        {
            if (_input != null) { _input.Slot += OnSlot; _input.Previous += OnPrevious; _input.Next += OnNext; }
            if (_shifter != null) _shifter.Shifted += OnShifted;
        }

        private void OnDisable()
        {
            if (_input != null) { _input.Slot -= OnSlot; _input.Previous -= OnPrevious; _input.Next -= OnNext; }
            if (_shifter != null) _shifter.Shifted -= OnShifted;
        }

        // ---------- what comes and goes (the WeaponHolder and the Carrier tell us) ----------

        /// <summary>A weapon was taken: into the first free slot (or where the one it swapped for was).</summary>
        public void Added(Weapon w) { if (w != null && _items.FindIndex(i => i.Weapon == w) < 0) _items.Add(new Item { Weapon = w }); Changed?.Invoke(); }

        /// <summary>A tool was picked up into the hand.</summary>
        public void Added(Carryable t) { if (t != null && _items.FindIndex(i => i.Tool == t) < 0) _items.Add(new Item { Tool = t }); if (_holder != null) _holder.SetDrawn(false); Changed?.Invoke(); }

        /// <summary>A weapon left for good (dropped, or carried off by a door between rooms).</summary>
        public void Removed(Weapon w) { _items.RemoveAll(i => i.Weapon == w || i.Gone); Changed?.Invoke(); }

        /// <summary>A tool left for good (set down in the world).</summary>
        public void Removed(Carryable t) { _items.RemoveAll(i => i.Tool == t || i.Gone); Changed?.Invoke(); }

        /// <summary>True when a tool picked up now has a slot to go in; says why not otherwise.</summary>
        public bool RoomForTool()
        {
            if (!Full) return true;
            Hint(fullText);
            return false;
        }

        // ---------- choosing ----------

        private bool KeysAct() => isActiveAndEnabled && Time.timeScale > 0f && GameMode.Current != GameMode.Mode.Build && !DebugConsole.IsOpen && (_holder == null || !_holder.Locked);

        private void OnSlot(int slot)
        {
            if (!KeysAct() || slot < 0 || slot >= _items.Count) return;
            if (slot == InHand) { EmptyHands(); return; }   // pressed again: put it away
            if (!CanUse(slot)) { Hint(tooBigText); return; }
            TakeOut(slot);
        }

        private void Update()
        {
            // The mouse wheel steps through the usable slots (not with a prop in first person's hands: the wheel moves it).
            float wheel = _input != null ? _input.Scroll : 0f;
            if (wheel != 0f && KeysAct() && !KeyHelp.IsOpen && !(_holder != null && _holder.HandsBusyInView)) Step(wheel < 0f ? 1 : -1);
            _items.RemoveAll(i => i.Gone);
        }

        private void OnPrevious() { if (KeysAct()) Step(-1); }
        private void OnNext() { if (KeysAct()) Step(1); }

        /// <summary>The next usable slot that way from the one in hand (from the first, with empty hands).</summary>
        private void Step(int way)
        {
            if (_items.Count == 0) return;
            if (_holder != null && _holder.Current != null && _holder.Current.Busy) return;   // mid-swing, mid-reload
            int from = InHand;
            for (int k = 1; k <= _items.Count; k++)
            {
                int slot = ((from < 0 ? (way > 0 ? -1 : 0) : from) + way * k + _items.Count * 4) % _items.Count;
                if (slot == from || !CanUse(slot)) continue;
                TakeOut(slot);
                return;
            }
        }

        /// <summary>Puts away what is in hand and takes out the slot's thing.</summary>
        public void TakeOut(int slot)
        {
            if (!CanUse(slot)) return;
            Item item = _items[slot];
            if (item.Tool != null)
            {
                if (_holder != null) _holder.SetDrawn(false);
                if (_carrier != null && _carrier.Held != item.Tool) { _carrier.Stow(); _carrier.TakeOut(item.Tool); }
            }
            else if (item.Weapon != null && _holder != null)
            {
                if (_carrier != null) _carrier.Stow();
                _holder.Select(item.Weapon);
                _holder.SetDrawn(true);
            }
            Changed?.Invoke();
        }

        /// <summary>Nothing in the hands: a tool is stowed in its slot, a weapon holstered.</summary>
        public void EmptyHands()
        {
            if (_carrier != null) _carrier.Stow();
            if (_holder != null) _holder.SetDrawn(false);
            Changed?.Invoke();
        }

        /// <summary>After a shift: what the new size cannot use goes out of the hand.</summary>
        private void OnShifted(ShiftSize _)
        {
            int inHand = InHand;
            if (inHand >= 0 && !CanUse(inHand)) EmptyHands();
            Changed?.Invoke();
        }

        private void Hint(string text)
        {
            var charge = GetComponent<ShiftCharge>();
            if (charge == null) charge = FindFirstObjectByType<ShiftCharge>();
            if (charge != null && !string.IsNullOrEmpty(text)) charge.ShowHint(text);
        }
    }
}
