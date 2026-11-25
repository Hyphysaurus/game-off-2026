using UnityEngine;

namespace ShiftJam
{
    /// <summary>What a toast is about: picks its colour, icon, label and sound. Say is the toy itself talking (D-0213):
    /// a speech sticker with a tail, its words typed out (<see cref="Toasts.Say"/>).</summary>
    public enum ToastKind { Tip, Got, Refused, Warning, Core, Say }

    /// <summary>
    /// How a line the toy says is shown (D-0213): how fast its letters type, the beat after a full stop, how long it stays
    /// once typed, how long it waits before it drops in, one word that wobbles (and the Text Animator effect that does it)
    /// and its sound. The wake-up line fills it from OpeningSettings; <see cref="Default"/> serves a plain
    /// Toasts.Show(text, ToastKind.Say).
    /// </summary>
    public struct SayStyle
    {
        public float lettersPerSecond, pauseSeconds, holdSeconds, delay;
        public string wobbleWord, wobbleTag;
        public UiAudio.Kind sound;

        public static SayStyle Default => new SayStyle
        {
            lettersPerSecond = 30f, pauseSeconds = 0.3f, holdSeconds = 4.5f, delay = 0f,
            wobbleWord = "", wobbleTag = "wave", sound = UiAudio.Kind.Toast,
        };
    }

    /// <summary>
    /// The one way to put a short message on screen (D-0162): sticker cards at the top middle, up to three stacked, a
    /// repeat merged into its card (x2), each kind with its own sound. Any script, any scene: the HUD makes itself on the
    /// first message. <b>Bold</b> words show as key caps. A message about one player goes through <see cref="ShowFor"/>
    /// so only that player's own screen shows it (online, the other PCs hold copies of everyone). A moment that wants
    /// the whole screen for a breath (a Flat Zone's way in and out) is a big card instead: <see cref="CardFor"/>. The toy
    /// itself talking is a speech sticker: <see cref="Say"/> (D-0213).
    /// </summary>
    public static class Toasts
    {
        public static void Show(string text, ToastKind kind = ToastKind.Tip, string title = null)
        {
            if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(title)) return;
            if (Application.isBatchMode) return;   // batch runs draw nothing
            if (kind == ToastKind.Say) { Say(text, SayStyle.Default, title); return; }
            ToastHud.Get().Add(text, kind, title);
        }

        /// <summary>The toy says <paramref name="text"/> (D-0213): a speech sticker at the top of the stack, its letters
        /// typed out on real time, <see cref="SayStyle.wobbleWord"/> wobbling, held a while once typed. Never pushed out
        /// early by other toasts. <paramref name="title"/> (optional) is a small name over the words.</summary>
        public static void Say(string text, SayStyle style, string title = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (Application.isBatchMode) return;   // batch runs draw nothing
            ToastHud.Get().AddSay(text, style, title);
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
