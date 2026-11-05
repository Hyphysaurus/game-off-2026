using System.Globalization;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Three save slots in PlayerPrefs (they survive stopping Play and quitting). A slot holds where the player stood at
    /// the last save point, their facing, size and the room's name. Slot is the active one; the title menu picks it,
    /// save points write to it, Respawn reads it.
    /// </summary>
    public static class SaveGame
    {
        public const int Slots = 3;
        private const string SlotKey = "ShiftJam.Slot";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string Key(int slot) => "ShiftJam.Save.v2." + slot;

        public struct Data { public Vector3 position; public float yaw; public ShiftSize size; public string room; public string when; }

        /// <summary>The active slot, 0..2.</summary>
        public static int Slot
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SlotKey, 0), 0, Slots - 1);
            set { PlayerPrefs.SetInt(SlotKey, Mathf.Clamp(value, 0, Slots - 1)); PlayerPrefs.Save(); }
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

        public static void Clear(int slot) { PlayerPrefs.DeleteKey(Key(slot)); PlayerPrefs.Save(); }
    }
}
