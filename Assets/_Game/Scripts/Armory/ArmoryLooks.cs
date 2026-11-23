using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The look the player chose for each TYPE of gun (D-0167), kept on this PC the way the body choice is
    /// (PlayerOptions): one line of text per gun type in PlayerPrefs. WHY per type and not per gun: guns are handed
    /// out as fresh copies by the stands, so "my pistol" can only mean "every pistol I take".
    /// The text is pairs joined by ';', each "key=value". The key is "skin" or the id of a kind of part; the value
    /// is the id of the skin or the part. A key that is missing means "as the gun came", so an empty text is a gun
    /// nobody changed, and nothing is stored for it.
    /// Only ids from the parts list (ArmoryCatalog) go in: this script knows no gun, skin or part by name.
    /// </summary>
    public static class ArmoryLooks
    {
        /// <summary>The key of the skin in a look's text.</summary>
        public const string SkinKey = "skin";

        private const string KeyPrefix = "armory.look.";
        private const char PairEnd = ';', KeyEnd = '=';

        /// <summary>The saved look for this type of gun; "" when nothing is saved (or the id is empty).</summary>
        public static string Get(string gunId)
        {
            return string.IsNullOrEmpty(gunId) ? "" : PlayerPrefs.GetString(KeyPrefix + gunId, "");
        }

        /// <summary>Saves the look for this type of gun, at once (as Options does, so quitting never loses it). An
        /// empty look deletes the entry: the gun is back to how it comes, and is not touched when taken.</summary>
        public static void Set(string gunId, string look)
        {
            if (string.IsNullOrEmpty(gunId)) return;
            if (string.IsNullOrEmpty(look)) PlayerPrefs.DeleteKey(KeyPrefix + gunId);
            else PlayerPrefs.SetString(KeyPrefix + gunId, look);
            PlayerPrefs.Save();
        }

        /// <summary>Takes a look's text apart into <paramref name="into"/> (emptied first): key to value. A pair with
        /// no '=' or with an empty side is skipped, so a damaged entry only loses that one choice.</summary>
        public static void Read(string look, Dictionary<string, string> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(look)) return;
            int start = 0;
            while (start < look.Length)
            {
                int end = look.IndexOf(PairEnd, start);
                if (end < 0) end = look.Length;
                int split = look.IndexOf(KeyEnd, start, end - start);
                if (split > start && split < end - 1) into[look.Substring(start, split - start)] = look.Substring(split + 1, end - split - 1);
                start = end + 1;
            }
        }

        /// <summary>The look with one more choice written after it. An empty value adds nothing: "as the gun came"
        /// is said by leaving the key out.</summary>
        public static string With(string look, string key, string value)
        {
            if (look == null) look = "";
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return look;
            return look.Length == 0 ? key + KeyEnd + value : look + PairEnd + key + KeyEnd + value;
        }
    }
}
