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
        /// <summary>E on something that said no (a core from below it, at the wrong size, dark or spent; a switch out of
        /// reach): nothing happened. The string is what its prompt said.</summary>
        public static event Action<Interactor, string> UseRefused;
        /// <summary>Something was taken into the hands (a carried torch or ruler, a grabbed safe or box).</summary>
        public static event Action<UnityEngine.Transform> PickedUp;
        /// <summary>Something held was set down where the player put it.</summary>
        public static event Action<UnityEngine.Transform> SetDown;
        /// <summary>A holder took a weapon from a stand or off the floor.</summary>
        public static event Action<WeaponHolder, Weapon> WeaponTaken;
        /// <summary>A knocked-down toy soldier stood up again at its post.</summary>
        public static event Action<ToySoldier> SoldierUp;
        /// <summary>A choice on the end card was taken (true: the playroom; false: play again). The scene changes next.</summary>
        public static event Action<bool> EndChoice;
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
        /// <summary>A holder's shot landed on something with health and the hit counted; the bool says whether that hit
        /// finished it off. The hit marker listens.</summary>
        public static event Action<WeaponHolder, bool> ShotLanded;
        /// <summary>A foam dart stuck where it landed; the Transform is whoever fired it (a player, a toy soldier; it
        /// can be gone). The play log counts the player's.</summary>
        public static event Action<UnityEngine.Transform> DartStuck;
        /// <summary>A thrower's grenade count changed: one was thrown, a supply crate filled it up, or the reset key
        /// did. Read the number from the thrower (Carried).</summary>
        public static event Action<GrenadeThrower> GrenadesChanged;
        /// <summary>A grenade left the thrower's hand.</summary>
        public static event Action<GrenadeThrower> GrenadeThrown;
        /// <summary>A thrower's grenade went off: the int is how many things with health its blast hit, the bool whether
        /// it finished any of them off. The hit marker listens.</summary>
        public static event Action<GrenadeThrower, int, bool> GrenadeWentOff;
        /// <summary>The opening (New Game, D-0097): the toy box's flaps begin to open over the lying toy.</summary>
        public static event Action OpeningLid;
        /// <summary>The opening: the spark sets off down into the box (false) or reaches the toy (true).</summary>
        public static event Action<bool> OpeningSpark;
        /// <summary>The opening: the toy starts to get up.</summary>
        public static event Action OpeningGetUp;
        /// <summary>The opening is over and the player has control (true: it was skipped).</summary>
        public static event Action<bool> OpeningEnded;
        /// <summary>The player took the paper plane: the glider is theirs (D-0095).</summary>
        public static event Action<Glider> GliderTaken;
        /// <summary>The glider opened (true) or folded (false); Glider.LastFold says why it folded.</summary>
        public static event Action<Glider, bool> GlideChanged;

        internal static void RaiseShiftStarted(Shifter who, ShiftSize size) => ShiftStarted?.Invoke(who, size);
        internal static void RaiseShifted(Shifter who, ShiftSize size) => Shifted?.Invoke(who, size);
        internal static void RaiseSourceAwakened(ShiftSource source) => SourceAwakened?.Invoke(source);
        internal static void RaiseSwitchPressed(Switch pressed) => SwitchPressed?.Invoke(pressed);
        internal static void RaiseDoorOpening(Door door) => DoorOpening?.Invoke(door);
        internal static void RaiseCheckpointClaimed(Checkpoint checkpoint) => CheckpointClaimed?.Invoke(checkpoint);
        internal static void RaisePlayerReset(Respawn respawn) => PlayerReset?.Invoke(respawn);
        internal static void RaiseGoalReached(Goal goal) => GoalReached?.Invoke(goal);
        internal static void RaiseShiftRefused(Shifter who, string why) => ShiftRefused?.Invoke(who, why);
        internal static void RaiseUseRefused(Interactor who, string why) => UseRefused?.Invoke(who, why);
        internal static void RaisePickedUp(UnityEngine.Transform thing) => PickedUp?.Invoke(thing);
        internal static void RaiseSetDown(UnityEngine.Transform thing) => SetDown?.Invoke(thing);
        internal static void RaiseWeaponTaken(WeaponHolder holder, Weapon weapon) => WeaponTaken?.Invoke(holder, weapon);
        internal static void RaiseSoldierUp(ToySoldier soldier) => SoldierUp?.Invoke(soldier);
        internal static void RaiseEndChoice(bool playroom) => EndChoice?.Invoke(playroom);
        internal static void RaiseAmmoChanged(WeaponHolder holder) => AmmoChanged?.Invoke(holder);
        internal static void RaiseReloadStarted(WeaponHolder holder, BlasterWeapon gun) => ReloadStarted?.Invoke(holder, gun);
        internal static void RaiseReloadEnded(WeaponHolder holder, BlasterWeapon gun, bool loaded) => ReloadEnded?.Invoke(holder, gun, loaded);
        internal static void RaiseDryFired(WeaponHolder holder, BlasterWeapon gun) => DryFired?.Invoke(holder, gun);
        internal static void RaiseAmmoTaken(WeaponHolder holder, int rounds) => AmmoTaken?.Invoke(holder, rounds);
        internal static void RaiseShotLanded(WeaponHolder holder, bool finished) => ShotLanded?.Invoke(holder, finished);
        internal static void RaiseDartStuck(UnityEngine.Transform shooter) => DartStuck?.Invoke(shooter);
        internal static void RaiseGrenadesChanged(GrenadeThrower thrower) => GrenadesChanged?.Invoke(thrower);
        internal static void RaiseGrenadeThrown(GrenadeThrower thrower) => GrenadeThrown?.Invoke(thrower);
        internal static void RaiseGrenadeWentOff(GrenadeThrower thrower, int hits, bool finished) => GrenadeWentOff?.Invoke(thrower, hits, finished);
        internal static void RaiseOpeningLid() => OpeningLid?.Invoke();
        internal static void RaiseOpeningSpark(bool arrived) => OpeningSpark?.Invoke(arrived);
        internal static void RaiseOpeningGetUp() => OpeningGetUp?.Invoke();
        internal static void RaiseOpeningEnded(bool skipped) => OpeningEnded?.Invoke(skipped);
        internal static void RaiseGliderTaken(Glider glider) => GliderTaken?.Invoke(glider);
        internal static void RaiseGlideChanged(Glider glider, bool open) => GlideChanged?.Invoke(glider, open);
    }
}
