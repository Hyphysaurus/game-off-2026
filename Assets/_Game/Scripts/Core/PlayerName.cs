using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The name this PC plays under online (D-0118): typed on the title (kept between runs), else the PC's user name,
    /// else "Player". At most 16 letters.
    /// </summary>
    public static class PlayerName
    {
        private const string Key = "ShiftJam.PlayerName";
        public const int MaxLength = 16;

        /// <summary>The typed name, or "" when none was typed.</summary>
        public static string Typed => Clean(PlayerPrefs.GetString(Key, ""));

        public static string Get()
        {
            string typed = Typed;
            if (typed.Length > 0) return typed;
            string user = Clean(System.Environment.UserName);
            return user.Length > 0 ? user : "Player";
        }

        public static void Set(string name)
        {
            PlayerPrefs.SetString(Key, Clean(name));
            PlayerPrefs.Save();
        }

        private static string Clean(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            name = name.Trim().Replace("<", "").Replace(">", "");   // no rich-text tags in a name tag
            return name.Length > MaxLength ? name.Substring(0, MaxLength) : name;
        }
    }
}
