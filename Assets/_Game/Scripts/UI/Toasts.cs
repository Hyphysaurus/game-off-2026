using UnityEngine;

namespace ShiftJam
{
    /// <summary>What a toast is about: picks its colour, icon, label and sound.</summary>
    public enum ToastKind { Tip, Got, Refused, Warning, Core }

    /// <summary>
    /// The one way to put a short message on screen (D-0162): sticker cards at the top middle, up to three stacked, a
    /// repeat merged into its card (x2), each kind with its own sound. Any script, any scene: the HUD makes itself on the
    /// first message. <b>Bold</b> words show as key caps. A message about one player goes through <see cref="ShowFor"/>
    /// so only that player's own screen shows it (online, the other PCs hold copies of everyone). A moment that wants
    /// the whole screen for a breath (a Flat Zone's way in and out) is a big card instead: <see cref="CardFor"/>.
    /// </summary>
    public static class Toasts
    {
        public static void Show(string text, ToastKind kind = ToastKind.Tip, string title = null)
        {
            if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(title)) return;
            if (Application.isBatchMode) return;   // batch runs draw nothing
            ToastHud.Get().Add(text, kind, title);
        }

        /// <summary>A message about <paramref name="player"/>: shown only where that player is played (its shift charge has a HUD).</summary>
        public static void ShowFor(Component player, string text, ToastKind kind = ToastKind.Tip, string title = null)
        {
            if (player == null) return;
            var charge = player.GetComponentInParent<ShiftCharge>();
            if (charge == null || !charge.HasHud) return;
            Show(text, kind, title);
        }

        /// <summary>A moment's big card for <paramref name="player"/> (D-0203: a Flat Zone's "FLAT SHIFT" and "BACK TO
        /// 3D"): one short line, big, in the middle of the screen, on a sticker card of <paramref name="colour"/>, gone
        /// in under two seconds. Shown only where that player is played, like <see cref="ShowFor"/>; silent (the moment
        /// has its own sound). A new card takes the place of one still showing.</summary>
        public static void CardFor(Component player, string text, Color colour)
        {
            if (player == null || string.IsNullOrEmpty(text)) return;
            var charge = player.GetComponentInParent<ShiftCharge>();
            if (charge == null || !charge.HasHud) return;
            if (Application.isBatchMode) return;   // batch runs draw nothing
            ToastHud.Get().AddCard(text, colour);
        }

        /// <summary>Shown the first time only, once per save slot (a first-time tip).</summary>
        public static void Once(string id, string text, ToastKind kind = ToastKind.Tip, string title = null)
        {
            if (!FirstTime(id)) return;
            Show(text, kind, title);
        }

        /// <summary><see cref="Once"/> for a message about one player.</summary>
        public static void OnceFor(Component player, string id, string text, ToastKind kind = ToastKind.Tip, string title = null)
        {
            if (player == null) return;
            var charge = player.GetComponentInParent<ShiftCharge>();
            if (charge == null || !charge.HasHud) return;
            Once(id, text, kind, title);
        }

        /// <summary>True once <paramref name="id"/>'s first-time message has been used up in this save slot. Marks nothing.</summary>
        public static bool Told(string id) => SaveGame.Seen(SaveGame.Slot, "toast:" + id);

        /// <summary>True the first time <paramref name="id"/> is asked about in this save slot (and marks it). Ask on
        /// events, not every frame (it reads the save's list).</summary>
        public static bool FirstTime(string id)
        {
            string key = "toast:" + id;
            if (SaveGame.Seen(SaveGame.Slot, key)) return false;
            SaveGame.MarkSeen(SaveGame.Slot, key);
            return true;
        }
    }
}
