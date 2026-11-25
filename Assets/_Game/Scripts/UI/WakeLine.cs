using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The toy's first words (D-0213): once it gets up in New Game's opening, it says OpeningSettings' Wake Line in a
    /// speech sticker (Toasts.Say). Then, once that sticker has gone, the first-minute goal (D-0220): OpeningSettings' Goal
    /// Line, a second speech sticker that names the whole game's goal. Made at run time, nothing in any scene. It listens
    /// for the opening's get-up, and for a skip (a skip before the spark strikes never gets the toy up through the strike,
    /// so there is no get-up to hear).
    /// Said at most once per save slot (the save's "toast:wake-line" mark covers both lines); New Game clears the slot
    /// first, so every New Game says them again. Only the opening raises these two events, and only New Game plays the
    /// opening, so Continue, a respawn, a sandbox and the other players online never hear them. An empty line says nothing
    /// (both empty: no mark either).
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
            bool wake = !string.IsNullOrWhiteSpace(s.wakeLine), goal = !string.IsNullOrWhiteSpace(s.goalLine);
            if (!wake && !goal) return;
            if (!Toasts.FirstTime(SeenId)) return;
            if (wake) Toasts.Say(s.wakeLine, s.WakeLineStyle());
            if (goal) Toasts.Say(s.goalLine, s.GoalLineStyle(wake));   // waits until the wake-up line has gone
        }
    }
}
