using System.Globalization;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Three save slots in PlayerPrefs (they survive stopping Play and quitting). A slot holds where the player stood at
    /// the last save point, their facing, size and the room's name. Slot is the active one; the title menu picks it,
    /// save points write to it, Respawn reads it.
    /// <see cref="Changed"/> (D-0208) tells parts that show what a slot holds (the Flat Zone letters) to read it again.
    /// </summary>
    public static class SaveGame
    {
        public const int Slots = 3;
        private const string SlotKey = "ShiftJam.Slot";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string Key(int slot) => "ShiftJam.Save.v2." + slot;

        public struct Data { public Vector3 position; public float yaw; public ShiftSize size; public string room; public string when; }

        /// <summary>Raised when what the game in play reads from the saves may have changed: another slot made the active
        /// one, a slot cleared (New Game), a moment marked seen (D-0208: the Flat Zone letters read again what was found).
        /// Nothing in a save's format changed for it.</summary>
        public static event System.Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Changed = null;

        /// <summary>The active slot, 0..2.</summary>
        public static int Slot
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SlotKey, 0), 0, Slots - 1);
            set { PlayerPrefs.SetInt(SlotKey, Mathf.Clamp(value, 0, Slots - 1)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static bool Has(int slot) => PlayerPrefs.HasKey(Key(slot));

        public static void Save(Vector3 position, float yaw, ShiftSize size, string room) => Save(Slot, position, yaw, size, room);

        public static void Save(int slot, Vector3 position, float yaw, ShiftSize size, string room)
        {
            string clean = (room ?? "").Replace(";", " ");
            PlayerPrefs.SetString(Key(slot), string.Join(";", position.x.ToString(Inv), position.y.ToString(Inv), position.z.ToString(Inv), yaw.ToString(Inv), ((int)size).ToString(Inv), clean, System.DateTime.Now.ToString("MMM d HH:mm", Inv)));
            PlayerPrefs.Save();
        }

        public static bool TryLoad(int slot, out Data data)
        {
            data = default;
            if (!PlayerPrefs.HasKey(Key(slot))) return false;
            string[] p = PlayerPrefs.GetString(Key(slot)).Split(';');
            if (p.Length < 5) return false;
            data.position = new Vector3(float.Parse(p[0], Inv), float.Parse(p[1], Inv), float.Parse(p[2], Inv));
            data.yaw = float.Parse(p[3], Inv);
            data.size = (ShiftSize)int.Parse(p[4], Inv);
            data.room = p.Length > 5 ? p[5] : "";
            data.when = p.Length > 6 ? p[6] : "";
            return true;
        }

        /// <summary>Clears the slot, and what it has seen (reveals play again).</summary>
        public static void Clear(int slot) { PlayerPrefs.DeleteKey(Key(slot)); PlayerPrefs.DeleteKey(SeenKey(slot)); PlayerPrefs.Save(); Changed?.Invoke(); }

        private static string SeenKey(int slot) => "ShiftJam.Seen.v1." + slot;
        /// <summary>One-time moments (the playroom reveal) this slot has already played.</summary>
        public static bool Seen(int slot, string id) => ("|" + PlayerPrefs.GetString(SeenKey(slot), "") + "|").Contains("|" + id + "|");
        public static void MarkSeen(int slot, string id)
        {
            if (Seen(slot, id)) return;
            string s = PlayerPrefs.GetString(SeenKey(slot), ""); PlayerPrefs.SetString(SeenKey(slot), s.Length == 0 ? id : s + "|" + id); PlayerPrefs.Save();
            Changed?.Invoke();
        }
        /// <summary>Takes one moment off this slot's seen list, so it plays again (D-0228: the console's "bedroom reset");
        /// raises <see cref="Changed"/> when it was there. The rest of the slot is untouched.</summary>
        public static void Forget(int slot, string id)
        {
            if (string.IsNullOrEmpty(id) || !Seen(slot, id)) return;
            var kept = new System.Text.StringBuilder();
            foreach (string s in PlayerPrefs.GetString(SeenKey(slot), "").Split('|'))
                if (s.Length > 0 && s != id) kept.Append(kept.Length > 0 ? "|" : "").Append(s);
            PlayerPrefs.SetString(SeenKey(slot), kept.ToString()); PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
