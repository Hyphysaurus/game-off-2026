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
        /// <summary>A holder's ammo read-out changed: a shot left, a reload started, seated or ended, an ammo box was
        /// taken, or another gun came to hand. Read the numbers from the holder (Current, Reloading) and its gun's Magazine.</summary>
        public static event Action<WeaponHolder> AmmoChanged;
        /// <summary>A reload started on this gun.</summary>
        public static event Action<WeaponHolder, BlasterWeapon> ReloadStarted;
        /// <summary>A reload ended on this gun; the bool says whether the new magazine had seated (rounds were loaded).</summary>
        public static event Action<WeaponHolder, BlasterWeapon, bool> ReloadEnded;
        /// <summary>The trigger was pulled on an empty gun (the dry click).</summary>
        public static event Action<WeaponHolder, BlasterWeapon> DryFired;
        /// <summary>A holder took ammo from a box; the int is how many rounds its guns gained in all.</summary>
        public static event Action<WeaponHolder, int> AmmoTaken;

        internal static void RaiseShiftStarted(Shifter who, ShiftSize size) => ShiftStarted?.Invoke(who, size);
        internal static void RaiseShifted(Shifter who, ShiftSize size) => Shifted?.Invoke(who, size);
        internal static void RaiseSourceAwakened(ShiftSource source) => SourceAwakened?.Invoke(source);
        internal static void RaiseSwitchPressed(Switch pressed) => SwitchPressed?.Invoke(pressed);
        internal static void RaiseDoorOpening(Door door) => DoorOpening?.Invoke(door);
        internal static void RaiseCheckpointClaimed(Checkpoint checkpoint) => CheckpointClaimed?.Invoke(checkpoint);
        internal static void RaisePlayerReset(Respawn respawn) => PlayerReset?.Invoke(respawn);
        internal static void RaiseGoalReached(Goal goal) => GoalReached?.Invoke(goal);
        internal static void RaiseShiftRefused(Shifter who, string why) => ShiftRefused?.Invoke(who, why);
        internal static void RaiseAmmoChanged(WeaponHolder holder) => AmmoChanged?.Invoke(holder);
        internal static void RaiseReloadStarted(WeaponHolder holder, BlasterWeapon gun) => ReloadStarted?.Invoke(holder, gun);
        internal static void RaiseReloadEnded(WeaponHolder holder, BlasterWeapon gun, bool loaded) => ReloadEnded?.Invoke(holder, gun, loaded);
        internal static void RaiseDryFired(WeaponHolder holder, BlasterWeapon gun) => DryFired?.Invoke(holder, gun);
        internal static void RaiseAmmoTaken(WeaponHolder holder, int rounds) => AmmoTaken?.Invoke(holder, rounds);
    }
}
