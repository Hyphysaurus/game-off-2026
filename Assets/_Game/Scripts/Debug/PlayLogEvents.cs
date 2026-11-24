using Playtest;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam's side of the play log (<see cref="PlayLog"/>): listens to the game's own events and passes them on in
    /// plain words, so a play's record says what was done, not only that the scene was open. One line each for what
    /// happens now and then (a shift, a checkpoint, a reload, a lap, another kind of ammo loaded); a count for what
    /// happens all the time (shots landed, darts stuck, grenades thrown). The player's place is followed. Only the
    /// player's own gun, ammo, darts and size are recorded, never a toy soldier's.
    /// Spawns itself whenever a play is being recorded; nothing to place in a scene. F12 (mark) and the console's
    /// "note" put his notes in the same record (DebugConsole).
    /// </summary>
    public sealed class PlayLogEvents : MonoBehaviour
    {
        private const float LookEvery = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!PlayLog.On) return;
            var go = new GameObject("PlayLogEvents"); DontDestroyOnLoad(go); go.AddComponent<PlayLogEvents>();
        }

        private GameObject _player;
        private WeaponHolder _holder;
        private AmmoBelt _belt;          // the player's kinds of ammo, when it carries any
        private string _gun = "";
        private float _nextLook;
        private bool _gunChanged, _ammoChanged;

        private void OnEnable()
        {
            GameEvents.Shifted += OnShifted; GameEvents.ShiftRefused += OnShiftRefused; GameEvents.SourceAwakened += OnSourceAwakened;
            GameEvents.SwitchPressed += OnSwitchPressed; GameEvents.DoorOpening += OnDoorOpening;
            GameEvents.CheckpointClaimed += OnCheckpointClaimed; GameEvents.PlayerReset += OnPlayerReset; GameEvents.GoalReached += OnGoalReached;
            GameEvents.AmmoChanged += OnAmmoChanged; GameEvents.ReloadStarted += OnReloadStarted; GameEvents.ReloadEnded += OnReloadEnded;
            GameEvents.DryFired += OnDryFired; GameEvents.AmmoTaken += OnAmmoTaken; GameEvents.ShotLanded += OnShotLanded;
            GameEvents.DartStuck += OnDartStuck;
            GameEvents.GrenadeThrown += OnGrenadeThrown; GameEvents.GrenadeWentOff += OnGrenadeWentOff;
            GameEvents.GliderTaken += OnGliderTaken; GameEvents.GlideChanged += OnGlideChanged;
            GameEvents.RopeChanged += OnRopeChanged;
            GameMode.Changed += OnModeChanged;
            RaceState.Started += OnRaceStarted; RaceState.LapDone += OnLapDone; RaceState.Finished += OnRaceFinished; RaceState.Stopped += OnRaceStopped;
        }

        private void OnDisable()
        {
            GameEvents.Shifted -= OnShifted; GameEvents.ShiftRefused -= OnShiftRefused; GameEvents.SourceAwakened -= OnSourceAwakened;
            GameEvents.SwitchPressed -= OnSwitchPressed; GameEvents.DoorOpening -= OnDoorOpening;
            GameEvents.CheckpointClaimed -= OnCheckpointClaimed; GameEvents.PlayerReset -= OnPlayerReset; GameEvents.GoalReached -= OnGoalReached;
            GameEvents.AmmoChanged -= OnAmmoChanged; GameEvents.ReloadStarted -= OnReloadStarted; GameEvents.ReloadEnded -= OnReloadEnded;
            GameEvents.DryFired -= OnDryFired; GameEvents.AmmoTaken -= OnAmmoTaken; GameEvents.ShotLanded -= OnShotLanded;
            GameEvents.DartStuck -= OnDartStuck;
            GameEvents.GrenadeThrown -= OnGrenadeThrown; GameEvents.GrenadeWentOff -= OnGrenadeWentOff;
            GameEvents.GliderTaken -= OnGliderTaken; GameEvents.GlideChanged -= OnGlideChanged;
            GameEvents.RopeChanged -= OnRopeChanged;
            GameMode.Changed -= OnModeChanged;
            if (_belt != null) _belt.Changed -= OnBeltChanged;
            _belt = null;
            RaceState.Started -= OnRaceStarted; RaceState.LapDone -= OnLapDone; RaceState.Finished -= OnRaceFinished; RaceState.Stopped -= OnRaceStopped;
        }

        private void Update()
        {
            // The gun in hand is told once a frame at most: a scene's start hands the player its guns one after
            // another, and only the one left in the hand is worth a line.
            if (_gunChanged) { _gunChanged = false; TellGun(); }
            // So is the ammo loaded: T pressed three times in a frame is one line, for the kind it ended on.
            if (_ammoChanged) { _ammoChanged = false; if (_belt != null) PlayLog.Tell("ammo loaded: " + _belt.CurrentName); }
            if (_player != null || Time.unscaledTime < _nextLook) return;
            _nextLook = Time.unscaledTime + LookEvery;
            Bind();
        }

        /// <summary>Finds the player of the scene now open (each scene has its own) and follows it.</summary>
        private bool Bind()
        {
            if (_player != null) return true;
            _player = GameObject.Find("Player");
            if (_player == null) return false;
            _holder = _player.GetComponent<WeaponHolder>();
            if (_belt != null) _belt.Changed -= OnBeltChanged;   // the scene before's player
            _belt = _player.GetComponent<AmmoBelt>();
            if (_belt != null) _belt.Changed += OnBeltChanged;
            _gun = "";
            PlayLog.Follow(_player.transform);
            _gunChanged = true;
            return true;
        }

        private bool Mine(Component who) => who != null && Bind() && who.gameObject == _player;

        private void TellGun()
        {
            if (_holder == null) return;
            string now = _holder.Armed ? Name(_holder.Current) : "";
            if (now == _gun) return;
            _gun = now;
            PlayLog.Tell(now.Length > 0 ? "gun in hand: " + now : "no gun in hand");
        }

        private static string Name(Weapon weapon) => weapon == null ? "" : string.IsNullOrEmpty(weapon.DisplayName) ? weapon.name : weapon.DisplayName;
        private static string Named(Component what) => what == null ? "?" : what.transform.parent != null ? what.transform.parent.name + "/" + what.name : what.name;

        private void OnShifted(Shifter who, ShiftSize size) { if (!Mine(who)) return; PlayLog.Tell("shift to " + (size == ShiftSize.Kid ? "kid" : "toy")); PlayLog.Count("shifts"); }
        private void OnShiftRefused(Shifter who, string why) { if (Mine(who)) PlayLog.Tell("shift refused: " + why); }
        private void OnSourceAwakened(ShiftSource source) => PlayLog.Tell("core lit: " + Named(source));
        private void OnSwitchPressed(Switch pressed) => PlayLog.Tell("switch pressed: " + Named(pressed));
        private void OnDoorOpening(Door door) => PlayLog.Tell("door opens: " + Named(door));
        private void OnCheckpointClaimed(Checkpoint checkpoint) => PlayLog.Tell("checkpoint: " + Named(checkpoint));
        private void OnPlayerReset(Respawn respawn) { PlayLog.Tell("reset to the checkpoint"); PlayLog.Count("resets"); }
        private void OnGoalReached(Goal goal) => PlayLog.Tell("GOAL: " + Named(goal));
        private void OnGliderTaken(Glider glider) => PlayLog.Tell("took the paper plane (glider)");
        private void OnGlideChanged(Glider glider, bool open)
        {
            if (glider == null) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (open) { PlayLog.Tell("glide opened " + glider.transform.position.y.ToString("0.0", inv) + " up"); PlayLog.Count("glides"); return; }
            Vector3 from = glider.OpenedAt, to = glider.transform.position;
            float far = new Vector2(to.x - from.x, to.z - from.z).magnitude;
            PlayLog.Tell("glide folded (" + glider.LastFold + ") after " + (Time.time - glider.OpenedTime).ToString("0.0", inv) + " s, " + far.ToString("0", inv) + " far, " + (from.y - to.y).ToString("0.0", inv) + " down");
        }

        private void OnRopeChanged(RopeRider rider, bool on)
        {
            if (!Mine(rider)) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (on) { PlayLog.Tell("rope grabbed " + rider.HandPoint.y.ToString("0.0", inv) + " up"); PlayLog.Count("rope rides"); return; }
            PlayLog.Tell("rope let go (" + rider.LastLetGo + ") after " + (Time.time - rider.GrabbedTime).ToString("0.0", inv) + " s, speed " + rider.Velocity.magnitude.ToString("0.0", inv));
        }

        private void OnAmmoChanged(WeaponHolder holder) { if (Mine(holder)) _gunChanged = true; }
        private void OnReloadStarted(WeaponHolder holder, BlasterWeapon gun) { if (!Mine(holder)) return; PlayLog.Tell("reload: " + Name(gun)); PlayLog.Count("reloads"); }
        private void OnReloadEnded(WeaponHolder holder, BlasterWeapon gun, bool loaded) { if (!Mine(holder) || loaded) return; PlayLog.Tell("reload cut short: " + Name(gun)); PlayLog.Count("reloads cut short"); }
        private void OnDryFired(WeaponHolder holder, BlasterWeapon gun) { if (Mine(holder)) PlayLog.Count("dry clicks"); }
        private void OnAmmoTaken(WeaponHolder holder, int rounds) { if (Mine(holder)) PlayLog.Tell("ammo taken: +" + rounds + " rounds"); }
        private void OnShotLanded(WeaponHolder holder, bool finished)
        {
            if (!Mine(holder)) return;
            PlayLog.Count("shots landed");
            if (finished) { PlayLog.Count("knock-downs"); PlayLog.Tell("knocked a target down"); }
        }

        private void OnBeltChanged(AmmoBelt belt) => _ammoChanged = true;
        private void OnDartStuck(Transform shooter) { if (Mine(shooter)) PlayLog.Count("darts stuck"); }

        private void OnGrenadeThrown(GrenadeThrower who) { if (!Mine(who)) return; PlayLog.Tell("grenade thrown at " + Mathf.RoundToInt(who.LastPower * 100f) + "% power, " + who.Carried + " left"); PlayLog.Count("grenades thrown"); }
        private void OnGrenadeWentOff(GrenadeThrower who, int hits, bool finished)
        {
            if (!Mine(who)) return;
            PlayLog.Tell(hits <= 0 ? "grenade blast hit nothing" : "grenade blast hit " + hits + (hits == 1 ? " thing" : " things") + " with health" + (finished ? ", and knocked one down" : ""));
            if (finished) PlayLog.Count("knock-downs");
        }

        private void OnModeChanged(GameMode.Mode from, GameMode.Mode to) =>
            PlayLog.Tell(to == GameMode.Mode.Build ? "build mode on" : to == GameMode.Mode.FirstPerson ? "first person on" : "back to play from " + (from == GameMode.Mode.Build ? "build mode" : "first person"));

        private void OnRaceStarted() { PlayLog.Tell("race started: " + RaceState.Laps + (RaceState.Laps == 1 ? " lap" : " laps")); PlayLog.Count("races started"); }
        private void OnLapDone() => PlayLog.Tell("lap " + RaceState.Lap + " done in " + RaceState.LastLap.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s");
        private void OnRaceFinished() { PlayLog.Tell("race finished in " + RaceState.TotalTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s, best lap " + RaceState.BestLap.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s"); PlayLog.Count("races finished"); }
        private void OnRaceStopped() => PlayLog.Tell("race left before the finish");
    }
}
