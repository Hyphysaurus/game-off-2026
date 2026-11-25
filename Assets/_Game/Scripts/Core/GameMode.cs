using System;

namespace ShiftJam
{
    /// <summary>
    /// Which way the game is being played right now. Play = the toy/kid on foot (or driving); Build = the Workshop's
    /// management-game camera and placement tools; FirstPerson = the Half-Life-2-style body. Whoever owns a mode listens
    /// to Changed and switches itself on or off; nobody reaches into another mode's objects.
    /// Hotkeys (set by the owners): F3 toggles Build, V toggles FirstPerson; Escape leaves either back to Play.
    /// </summary>
    public static class GameMode
    {
        public enum Mode { Play, Build, FirstPerson }

        public static Mode Current { get; private set; } = Mode.Play;

        /// <summary>True in the modes where the player is on foot in the world: Play and FirstPerson (not Build). What the
        /// player can do on foot (a rope, the glider) asks this, so it works in either view (D-0194).</summary>
        public static bool OnFoot => Current != Mode.Build;

        /// <summary>Raised after Current changes: (from, to).</summary>
        public static event Action<Mode, Mode> Changed;

        public static void Set(Mode mode)
        {
            if (mode == Current) return;
            Mode from = Current;
            Current = mode;
            Changed?.Invoke(from, mode);
        }

        /// <summary>Back to Play without raising anything: a scene load starts every mode fresh.</summary>
        public static void ResetQuietly() => Current = Mode.Play;
    }
}
