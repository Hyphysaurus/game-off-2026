using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The toy's first words (D-0213): once it gets up in New Game's opening, it says OpeningSettings' Wake Line in a
    /// speech sticker (Toasts.Say). Made at run time, nothing in any scene. It listens for the opening's get-up, and for a
    /// skip (a skip before the spark strikes never gets the toy up through the strike, so there is no get-up to hear).
    /// Said at most once per save slot (the save's "toast:wake-line" mark); New Game clears the slot first, so every New
    /// Game says it again. Only the opening raises these two events, and only New Game plays the opening, so Continue,
    /// a respawn, a sandbox and the other players online never hear it. An empty Wake Line says nothing.
    /// </summary>
    public static class WakeLine
    {
        /// <summary>Its first-time mark in the save slot (Toasts.FirstTime).</summary>
        public const string SeenId = "wake-line";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Listen()
        {
            // The editor may enter Play without reloading scripts: take the old hooks off before putting them on.
            GameEvents.OpeningGetUp -= OnGetUp;
            GameEvents.OpeningEnded -= OnEnded;
            GameEvents.OpeningGetUp += OnGetUp;
            GameEvents.OpeningEnded += OnEnded;
        }

        private static void OnGetUp() => Say();

        private static void OnEnded(bool skipped) { if (skipped) Say(); }   // after a get-up already heard, the mark stops a second line

        private static void Say()
        {
            if (Application.isBatchMode) return;   // batch runs draw nothing (and leave the save's marks alone)
            var s = OpeningSettings.Load();
            if (string.IsNullOrWhiteSpace(s.wakeLine)) return;
            if (!Toasts.FirstTime(SeenId)) return;
            Toasts.Say(s.wakeLine, s.WakeLineStyle());
        }
    }
}
