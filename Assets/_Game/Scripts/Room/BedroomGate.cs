using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// D-0241: something that can hold a Switch or the claw machine shut for now (the bedroom gate). The thing asks it in
    /// its prompt (every frame: an answer here allocates nothing) and again when it is used. A thing with no lock set is
    /// never held, so every other switch and machine works as before.
    /// </summary>
    public interface IShutLock
    {
        /// <summary>True while it holds <paramref name="thing"/> shut; <paramref name="prompt"/> = the words beside E then
        /// (null: the thing's own words).</summary>
        bool Holds(Component thing, out string prompt);

        /// <summary><paramref name="thing"/> was tried while held: E by <paramref name="who"/>, or a shot (who is null).
        /// Shows that thing's refusal toast (its own sound comes with it).</summary>
        void Refuse(Component thing, Interactor who);
    }

    /// <summary>
    /// The bedroom gate (D-0241), his words: "small door still locked until monsters are first addressed, room lights cannot
    /// go on and claw machine cannot be used until this happens > lights are on, small door can be unlocked and traveled
    /// through". "Monsters first addressed" = every monster under the bed knocked out with darts once.
    /// - While SHUT it holds three things (each has this gate in its Held Shut By, set by the maker): the shelf switch (E is
    ///   refused, and a dart that hits it presses nothing), the wall switch (the room's lights stay off) and the claw
    ///   machine. The words beside E become Settings > Shut Prompt; E shows that thing's refusal line as a toast, with the
    ///   switch's own refusal shake and sound (the claw machine: its own refusal toast and sound).
    /// - It OPENS when every Roam monster under BedMonsters has been knocked out at least once in this visit (not all at
    ///   the same time: they get up again). Darts Only (on): a knockout counts when the shot that finished it is heard in
    ///   the same moment (GameEvents.ShotLanded right after GameEvents.MonsterDown); a punch, a kick or a grenade does not.
    ///   Each new one shows a count toast; the last one a toast of its own, and <see cref="Opened"/> (Objectives hears it:
    ///   the step "Knock out the monsters" and the gold star "monsters" listen for it).
    /// - Saved per save slot (Settings > Save Key in SaveGame's seen list): Continue finds the room open. New Game, another
    ///   slot and the console's "bedroom reset" (it forgets the key) shut it and start the count again.
    /// Online (a party is a sandbox) it is always open, as every door is. Nothing is networked.
    /// OFF SWITCHES: BedroomGateSettings > On (open), or untick this component (open).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BedroomGate : MonoBehaviour, IShutLock
    {
        [SerializeField, Tooltip("Room/Bedroom/BedMonsters: every Roam monster under it must be knocked out once. Empty: the object named Settings > Monsters Holder beside this one (Land Bedroom Monsters makes that holder new each run).")]
        private Transform monsters;
        [SerializeField, Tooltip("Room/Bedroom/ShelfSwitch (its Held Shut By is this gate): its refusal says Settings > Shelf Line. Set by the maker.")]
        private Switch shelfSwitch;
        [SerializeField, Tooltip("Room/Bedroom/DarkBedroom/LightSwitch, the room's lights (its Held Shut By is this gate): Settings > Lights Line. Set by the maker.")]
        private Switch lightSwitch;
        [SerializeField, Tooltip("Room/Bedroom/ClawMachine (its Held Shut By is this gate): Settings > Claw Line. Set by the maker.")]
        private ClawGame claw;

        /// <summary>The gate opened on this PC: every monster was knocked out once (not on a Continue that finds it open).</summary>
        public static event Action<BedroomGate> Opened;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetStatics() => Opened = null;   // Objectives puts its listener back on at BeforeSceneLoad

        private BedroomGateSettings _s;
        private DarkMonster[] _monsters = Array.Empty<DarkMonster>();
        private bool[] _knocked = Array.Empty<bool>();
        private int _count, _readSlot = -1, _pendingFrame = -1;
        private bool _cleared, _found, _bound;
        private DarkMonster _pending;            // knocked out this frame: a dart says so in the same moment
        private Component _shotRefused;          // the last thing a shot was refused at, and when
        private float _shotRefusedAt = -100f;

        /// <summary>True while nothing is held: cleared on this slot, online, switched off, or no monster to wait for.</summary>
        public bool IsOpen => !isActiveAndEnabled || _s == null || !_s.on || NetSession.InParty || _cleared || (_found && _monsters.Length == 0);
        /// <summary>How many different monsters have been knocked out in this visit (the maker and the console report it).</summary>
        public int KnockedOut => _count;
        /// <summary>How many must be (the Roam monsters found).</summary>
        public int Needed => _monsters.Length;
        /// <summary>The save key (the console's "bedroom reset" forgets it).</summary>
        public string Key => _s != null ? _s.saveKey : "";

        private void Awake() => _s = BedroomGateSettings.Load();

        private void OnEnable()
        {
            if (_bound) return;
            GameEvents.MonsterDown += OnDown;
            GameEvents.ShotLanded += OnShot;
            GameEvents.OpeningEnded += OnNewGame;
            SaveGame.Changed += Read;
            _bound = true;
        }

        private void OnDisable()
        {
            if (!_bound) return;
            GameEvents.MonsterDown -= OnDown;
            GameEvents.ShotLanded -= OnShot;
            GameEvents.OpeningEnded -= OnNewGame;
            SaveGame.Changed -= Read;
            _bound = false;
            _pending = null;
        }

        private void Start()
        {
            Find();
            Read();
        }

        /// <summary>The Roam monsters under the holder (after their own Awake: Start).</summary>
        private void Find()
        {
            Transform holder = monsters;
            if (holder == null && transform.parent != null && !string.IsNullOrEmpty(_s.monstersHolder)) holder = transform.parent.Find(_s.monstersHolder);
            var found = new List<DarkMonster>();
            if (holder != null)
                foreach (DarkMonster m in holder.GetComponentsInChildren<DarkMonster>(true))
                    if (m.Role == DarkMonster.Mode.Roam) found.Add(m);
            _monsters = found.ToArray();
            _knocked = new bool[_monsters.Length];
            _count = 0;
            _found = true;
            if (_monsters.Length == 0)
                Debug.LogWarning("BEDROOM GATE: no Roam monster under " + (holder != null ? holder.name : "'" + _s.monstersHolder + "' (not found)") + ": the gate stands open. Run Shift Jam > House > Land Bedroom Monsters, then Land Bedroom Gate (D-0241).");
        }

        /// <summary>What the active slot says: open once it has been cleared. Another slot, or this one forgetting it (New
        /// Game, the console's "bedroom reset"): the count starts again. Heard on SaveGame.Changed, never every frame.</summary>
        private void Read()
        {
            int slot = SaveGame.Slot;
            bool cleared = !string.IsNullOrEmpty(_s.saveKey) && SaveGame.Seen(slot, _s.saveKey);
            if (slot != _readSlot || (_cleared && !cleared)) ClearCount();
            _readSlot = slot;
            _cleared = cleared;
        }

        /// <summary>New Game's opening ended (the scene is not loaded again): the count starts again.</summary>
        private void OnNewGame(bool skipped) => ClearCount();

        /// <summary>The knockout count starts again (the console's "bedroom reset"; New Game and another slot do it themselves).</summary>
        public void CountAgain() => ClearCount();

        private void ClearCount()
        {
            Array.Clear(_knocked, 0, _knocked.Length);
            _count = 0;
            _pending = null;
        }

        // ------------------------------------------------------------------------------------------------ counting

        private void OnDown(DarkMonster monster)
        {
            if (monster == null || IsOpen) return;
            int i = Array.IndexOf(_monsters, monster);
            if (i < 0) return;
            if (!_s.dartsOnly) { Count(i); return; }
            _pending = monster; _pendingFrame = Time.frameCount;   // a dart's ShotLanded follows in this same moment
        }

        /// <summary>A shot landed: if it finished the monster just knocked out, that knockout was a dart's.</summary>
        private void OnShot(WeaponHolder holder, bool finished)
        {
            if (!finished || _pending == null || _pendingFrame != Time.frameCount) return;
            int i = Array.IndexOf(_monsters, _pending);
            _pending = null;
            if (i >= 0) Count(i);
        }

        /// <summary>A knockout no shot claimed by now was not a dart's (a dart's is claimed in the same call).</summary>
        private void LateUpdate()
        {
            if (_pending == null) return;
            _pending = null;
            if (!IsOpen && !string.IsNullOrEmpty(_s.notCountedLine)) Toasts.Once("bedroom-gate-darts", _s.notCountedLine, ToastKind.Tip);
        }

        private void Count(int i)
        {
            if (_knocked[i]) return;
            _knocked[i] = true; _count++;
            Playtest.PlayLog.Tell("bedroom gate: " + _monsters[i].name + " knocked out (" + _count + " of " + _monsters.Length + ")");
            if (_count >= _monsters.Length) { Open(); return; }
            if (!string.IsNullOrEmpty(_s.countFormat)) Toasts.Show(Objectives.Format(_s.countFormat, _count, _monsters.Length), ToastKind.Tip);
        }

        /// <summary>Every monster knocked out once: the slot remembers it, a toast, and Opened (the step and the star).</summary>
        private void Open()
        {
            if (_cleared) return;
            _cleared = true;   // first: MarkSeen raises SaveGame.Changed, and Read must find it open
            if (!string.IsNullOrEmpty(_s.saveKey)) SaveGame.MarkSeen(SaveGame.Slot, _s.saveKey);
            if (!string.IsNullOrEmpty(_s.openLine)) Toasts.Show(_s.openLine, ToastKind.Got, string.IsNullOrEmpty(_s.openTitle) ? null : _s.openTitle);
            Playtest.PlayLog.Tell("bedroom gate OPEN: every monster knocked out once");
            Opened?.Invoke(this);
        }

        // ------------------------------------------------------------------------------------------------ the lock

        public bool Holds(Component thing, out string prompt)
        {
            prompt = null;
            if (IsOpen) return false;
            if (!string.IsNullOrEmpty(_s.shutPrompt)) prompt = _s.shutPrompt;
            return true;
        }

        public void Refuse(Component thing, Interactor who)
        {
            if (_s == null) return;
            string line = thing == claw ? _s.clawLine : thing == lightSwitch ? _s.lightsLine : _s.shelfLine;   // anything else wired by hand: the shelf's
            if (string.IsNullOrEmpty(line)) return;
            if (who == null)
            {
                // A shot (darts hit fast): once in Refuse Every seconds for the same thing. E is never held back: a repeat
                // merges into its card (x2).
                float now = Time.unscaledTime;
                if (thing == _shotRefused && now - _shotRefusedAt < _s.refuseEvery) return;
                _shotRefused = thing; _shotRefusedAt = now;
                Toasts.Show(line, ToastKind.Refused);
                return;
            }
            Toasts.ShowFor(who, line, ToastKind.Refused);
            UiAudio.Play(UiAudio.Kind.Denied);   // the claw machine's refusal precedent (heard once with the toast's own)
        }
    }
}
