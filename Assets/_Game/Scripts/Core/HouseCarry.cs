using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// What the player takes through a door between the house's rooms (D-0091): their size, the weapons in their hands
    /// (the very same guns, so the rounds in each and the reserve go with them), the stored shift charge and the paper-plane
    /// glider (D-0095). A HubDoor
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

        /// <summary>True from the press of a door until the player stands at the door it leads to. The title card, the
        /// wake-up and the save slot's load stand aside meanwhile.</summary>
        public static bool Arriving => _pending;

        /// <summary>The door the player is on the way to, or null.</summary>
        public static string ArriveAt => _pending ? _arriveAt : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _pending = false; _arriveAt = null; _glider = false; Weapons.Clear(); }

        /// <summary>Takes what the player carries and loads <paramref name="scene"/>; there the door named
        /// <paramref name="arriveAt"/> puts the player down.</summary>
        public static void Go(GameObject player, string scene, string arriveAt)
        {
            _arriveAt = arriveAt;
            _size = ShiftSize.Toy; _charges = 0; _glider = false; Weapons.Clear(); _inHand = -1;
            if (player != null)
            {
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
            if (player.TryGetComponent(out WeaponHolder holder))
            {
                // Carrying guns in, the room's own starting guns (the Colour Corner's Loadout) step aside: the hands hold what
                // came through the door, and a full holder does not drop one on the floor.
                if (Weapons.Count > 0) foreach (Weapon own in holder.Stow(out _)) if (own != null) Object.Destroy(own.gameObject);
                Weapon inHand = null;
                for (int i = 0; i < Weapons.Count; i++)
                {
                    Weapon w = Weapons[i];
                    if (w == null) continue;
                    SceneManager.MoveGameObjectToScene(w.gameObject, SceneManager.GetActiveScene());
                    Weapon taken = holder.Equip(w);
                    if (i == _inHand) inHand = taken;
                }
                if (inHand != null) holder.Equip(inHand);   // the one that was out goes back in the hand
            }
            else foreach (Weapon w in Weapons) if (w != null) Object.Destroy(w.gameObject);
            Weapons.Clear();
            Debug.Log($"[House] arrived at {doorId} as {_size}, charge {_charges}, glider {_glider}");
            return true;
        }
    }
}
