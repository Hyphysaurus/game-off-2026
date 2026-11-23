using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// What the player takes through a door between the house's rooms (D-0091): their size, the weapons in their hands
    /// (the very same guns, so the rounds in each and the reserve go with them), the stored shift charge and the paper-plane
    /// glider (D-0095), and (D-0145) the whole inventory bar in its order: the hand tools too (the flashlight, on or off),
    /// which thing was in each hand and whether the gun was out. A HubDoor
    /// calls Go; the matching HubDoor in the next scene calls Arrive. Nothing is written to disk: a closed game starts
    /// from the save slot as before. Statics, so it lives across one scene load and no longer.
    /// </summary>
    public static class HouseCarry
    {
        private static bool _pending;
        private static string _arriveAt;
        private static ShiftSize _size;
        private static int _charges;
        private static bool _glider;
        private static readonly List<Weapon> Weapons = new List<Weapon>();
        private static int _inHand;
        private static bool _drawn;                                         // the gun was out, not holstered
        private static readonly List<Object> Bar = new List<Object>();      // the bar in its order: weapons and tools (D-0145)
        private static readonly List<Carryable> Tools = new List<Carryable>();
        private static Carryable _toolInHand;

        /// <summary>True from the press of a door until the player stands at the door it leads to. The title card, the
        /// wake-up and the save slot's load stand aside meanwhile.</summary>
        public static bool Arriving => _pending;

        /// <summary>The door the player is on the way to, or null.</summary>
        public static string ArriveAt => _pending ? _arriveAt : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _pending = false; _arriveAt = null; _glider = false; Weapons.Clear(); Bar.Clear(); Tools.Clear(); _toolInHand = null; }

        /// <summary>Takes what the player carries and loads <paramref name="scene"/>; there the door named
        /// <paramref name="arriveAt"/> puts the player down.</summary>
        public static void Go(GameObject player, string scene, string arriveAt)
        {
            _arriveAt = arriveAt;
            _size = ShiftSize.Toy; _charges = 0; _glider = false; Weapons.Clear(); _inHand = -1;
            _drawn = false; Bar.Clear(); Tools.Clear(); _toolInHand = null;
            if (player != null)
            {
                // The bar's order and the tools first, while the slots still know them.
                if (player.TryGetComponent(out Inventory bar))
                    foreach (Inventory.Item item in bar.Items)
                    {
                        if (item.Weapon != null) Bar.Add(item.Weapon);
                        else if (item.Tool != null) { Bar.Add(item.Tool); Tools.Add(item.Tool); }
                    }
                if (Tools.Count > 0 && player.TryGetComponent(out Carrier hands))
                {
                    if (hands.Held != null && Tools.Contains(hands.Held)) _toolInHand = hands.Held;
                    hands.Stow();
                    foreach (Carryable tool in Tools)
                    {
                        tool.transform.SetParent(null, true);   // a root, so it can outlive the scene
                        Object.DontDestroyOnLoad(tool.gameObject);
                    }
                }
                if (player.TryGetComponent(out WeaponHolder armed)) _drawn = armed.Armed;
                if (player.TryGetComponent(out Shifter shifter)) _size = shifter.Size;
                if (player.TryGetComponent(out ShiftCharge charge)) _charges = charge.Charges;
                if (player.TryGetComponent(out Glider glider)) _glider = glider.Owned;
                if (player.TryGetComponent(out WeaponHolder holder))
                {
                    Weapons.AddRange(holder.Stow(out _inHand));
                    foreach (Weapon w in Weapons) Object.DontDestroyOnLoad(w.gameObject);
                }
            }
            _pending = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        /// <summary>Puts the player at <paramref name="at"/> with what was carried. Called by the HubDoor the player was sent
        /// to, one frame after the scene starts (after every Start, so a sandbox's full charge or a Loadout's blaster are
        /// already in place and the carried ones go on top). False if nothing was on the way here.</summary>
        public static bool Arrive(string doorId, GameObject player, Transform at)
        {
            if (!_pending || _arriveAt != doorId || player == null) return false;
            _pending = false;
            if (player.TryGetComponent(out Shifter shifter)) shifter.SetSizeNow(_size);
            if (at != null && player.TryGetComponent(out PlayerMotor motor))
            {
                Quaternion facing = Quaternion.Euler(0f, at.eulerAngles.y, 0f);
                motor.Warp(at.position + Vector3.up * 0.05f, facing);
                if (player.TryGetComponent(out Respawn respawn)) respawn.SetPoint(at.position + Vector3.up * 0.05f, facing);
                ShoulderCamera cam = Camera.main != null ? Camera.main.GetComponent<ShoulderCamera>() : null;
                if (cam != null) cam.Warp(at.eulerAngles.y);
            }
            if (player.TryGetComponent(out ShiftCharge charge)) charge.SetCharges(_charges);
            if (player.TryGetComponent(out Glider glider)) glider.SetOwned(_glider);
            Weapon inHand = null;
            player.TryGetComponent(out WeaponHolder holder);
            player.TryGetComponent(out Carrier carrier);
            // Carrying guns in, the room's own starting guns (the Colour Corner's Loadout) step aside: the hands hold what
            // came through the door, and a full holder does not drop one on the floor.
            if (holder != null && Weapons.Count > 0) foreach (Weapon own in holder.Stow(out _)) if (own != null) Object.Destroy(own.gameObject);
            // Back into the bar in the order they left it (D-0145): weapons taken, tools stored in their slots.
            var order = new List<Object>(Bar);
            foreach (Weapon w in Weapons) if (w != null && !order.Contains(w)) order.Add(w);   // no bar: the holder's own order
            foreach (Object thing in order)
            {
                if (thing is Weapon w)
                {
                    if (w == null) continue;
                    if (holder == null) { Object.Destroy(w.gameObject); continue; }
                    SceneManager.MoveGameObjectToScene(w.gameObject, SceneManager.GetActiveScene());
                    Weapon taken = holder.Equip(w);
                    if (Weapons.IndexOf(w) == _inHand) inHand = taken;
                }
                else if (thing is Carryable tool)
                {
                    if (tool == null) continue;
                    SceneManager.MoveGameObjectToScene(tool.gameObject, SceneManager.GetActiveScene());
                    if (carrier == null || !carrier.Store(tool)) { tool.gameObject.SetActive(true); tool.SetCarried(false); }   // nowhere to keep it: left where the player stands
                }
            }
            // The hands as they were: the gun that was out (or holstered), then the tool in hand (the flashlight in the left).
            if (holder != null)
            {
                if (inHand != null) holder.Equip(inHand);
                holder.SetDrawn(_drawn && inHand != null);
            }
            if (_toolInHand != null && player.TryGetComponent(out Inventory slots) && slots.Holds(_toolInHand)) slots.TakeOut(_toolInHand);
            int tools = Tools.Count;
            Weapons.Clear(); Bar.Clear(); Tools.Clear(); _toolInHand = null;
            Debug.Log($"[House] arrived at {doorId} as {_size}, charge {_charges}, glider {_glider}, {tools} tool(s)");
            return true;
        }
    }
}
