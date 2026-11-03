using System;

namespace ShiftJam
{
    /// <summary>
    /// Facts announced to whoever listens. The announcer never knows who cares.
    /// Raise through the Raise* methods; subscribe with +=, and always -= in OnDisable.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>A shift began: the player starts growing or shrinking toward the given size.</summary>
        public static event Action<Shifter, ShiftSize> ShiftStarted;
        /// <summary>A shift finished: the player is now the given size.</summary>
        public static event Action<Shifter, ShiftSize> Shifted;

        internal static void RaiseShiftStarted(Shifter who, ShiftSize size) => ShiftStarted?.Invoke(who, size);
        internal static void RaiseShifted(Shifter who, ShiftSize size) => Shifted?.Invoke(who, size);
    }
}
