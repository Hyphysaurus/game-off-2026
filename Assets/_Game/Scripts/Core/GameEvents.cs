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
        /// <summary>A dormant core lit up (a flashlight, a beam).</summary>
        public static event Action<ShiftSource> SourceAwakened;
        /// <summary>A switch was pressed and did its work.</summary>
        public static event Action<Switch> SwitchPressed;
        /// <summary>A door began to swing open.</summary>
        public static event Action<Door> DoorOpening;
        /// <summary>The player claimed a checkpoint: a reset lands here now.</summary>
        public static event Action<Checkpoint> CheckpointClaimed;
        /// <summary>The player is being put back at the checkpoint (a fatal fall, the kill height, a hazard).</summary>
        public static event Action<Respawn> PlayerReset;
        /// <summary>The player reached a room's goal.</summary>
        public static event Action<Goal> GoalReached;
        /// <summary>The Shift key was pressed and the shift could not happen; the string says why (shown on the HUD).</summary>
        public static event Action<Shifter, string> ShiftRefused;

        internal static void RaiseShiftStarted(Shifter who, ShiftSize size) => ShiftStarted?.Invoke(who, size);
        internal static void RaiseShifted(Shifter who, ShiftSize size) => Shifted?.Invoke(who, size);
        internal static void RaiseSourceAwakened(ShiftSource source) => SourceAwakened?.Invoke(source);
        internal static void RaiseSwitchPressed(Switch pressed) => SwitchPressed?.Invoke(pressed);
        internal static void RaiseDoorOpening(Door door) => DoorOpening?.Invoke(door);
        internal static void RaiseCheckpointClaimed(Checkpoint checkpoint) => CheckpointClaimed?.Invoke(checkpoint);
        internal static void RaisePlayerReset(Respawn respawn) => PlayerReset?.Invoke(respawn);
        internal static void RaiseGoalReached(Goal goal) => GoalReached?.Invoke(goal);
        internal static void RaiseShiftRefused(Shifter who, string why) => ShiftRefused?.Invoke(who, why);
    }
}
