using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Catches hitches (D-0133, his 1-2 s pauses): any frame over HitchSeconds is written to "Bug notes/hitches.txt" (in
    /// the game's own data folder, beside the F12 notes) with what was going on, so a playtest says what the pauses are
    /// without the Profiler: the scene, where the player stood and at what size, memory cleanups (garbage collections)
    /// during the frame and how much it allocated, what happened in the two seconds before (a scene loaded, a shift, a
    /// pickup, a prop breaking, a knock-down, a shot, a reload, a grenade, a door, the glider) and the last lines the game
    /// logged. In the editor and in development builds it also reads Unity's own timers for shader compiling, loading,
    /// instantiating and garbage collection. A frame just after the window lost or got back focus is not a hitch. Off in
    /// batch mode (the ship check). Made before the first scene, one per run.
    /// </summary>
    public sealed class HitchCatcher : MonoBehaviour
    {
        private const float HitchSeconds = 0.3f;     // a frame this long is a hitch
        private const float RecentSeconds = 2f;      // what counts as "just before"
        private const float QuietAtStart = 3f;       // the first load is not a hitch
        private const float QuietAfterFocus = 1.5f;  // nor the frames around losing or getting back the window
        private const int MostPerRun = 200;          // a game stuck in long frames must not fill the disk
        private const int MostRecent = 16;
        private static readonly string[] DevTimers = { "Shader.CreateGPUProgram", "Loading.ReadObject", "Loading.AwakeFromLoad", "Instantiate", "GC.Collect" };

        private readonly List<(float at, string what, int times)> _recent = new List<(float, string, int)>();
        private readonly List<(float at, string line)> _logs = new List<(float, string)>();
        private readonly List<(string name, ProfilerRecorder timer)> _timers = new List<(string, ProfilerRecorder)>();
        private ProfilerRecorder _allocated;
        private int _gc0, _gc1, _gc2, _knockDowns, _written;
        private float _focusChangedAt = -100f;
        private bool _headed;
        private Transform _player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Application.isBatchMode) return;
            var go = new GameObject("HitchCatcher");
            DontDestroyOnLoad(go);
            go.AddComponent<HitchCatcher>();
        }

        private static string FilePath => Path.Combine(Application.persistentDataPath, "Bug notes", "hitches.txt");

        private void OnEnable()
        {
            _allocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");   // in every build
            if (Debug.isDebugBuild) StartTimers();   // Unity's own timers: the editor and development builds only
            _gc0 = GC.CollectionCount(0); _gc1 = GC.CollectionCount(1); _gc2 = GC.CollectionCount(2);
            _knockDowns = KnockDown.Shown;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceived += OnLog;
            Projectile.Fired += OnFired;
            GameEvents.ShiftStarted += OnShiftStarted;
            GameEvents.Shifted += OnShifted;
            GameEvents.PickedUp += OnPickedUp;
            GameEvents.SetDown += OnSetDown;
            GameEvents.WeaponTaken += OnWeaponTaken;
            GameEvents.ReloadStarted += OnReload;
            GameEvents.GrenadeThrown += OnGrenadeThrown;
            GameEvents.GrenadeWentOff += OnGrenadeWentOff;
            GameEvents.PropBroke += OnPropBroke;
            GameEvents.SoldierUp += OnSoldierUp;
            GameEvents.DoorOpening += OnDoor;
            GameEvents.GlideChanged += OnGlide;
            GameEvents.PlayerReset += OnRespawn;
            GameEvents.SourceAwakened += OnCore;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.logMessageReceived -= OnLog;
            Projectile.Fired -= OnFired;
            GameEvents.ShiftStarted -= OnShiftStarted;
            GameEvents.Shifted -= OnShifted;
            GameEvents.PickedUp -= OnPickedUp;
            GameEvents.SetDown -= OnSetDown;
            GameEvents.WeaponTaken -= OnWeaponTaken;
            GameEvents.ReloadStarted -= OnReload;
            GameEvents.GrenadeThrown -= OnGrenadeThrown;
            GameEvents.GrenadeWentOff -= OnGrenadeWentOff;
            GameEvents.PropBroke -= OnPropBroke;
            GameEvents.SoldierUp -= OnSoldierUp;
            GameEvents.DoorOpening -= OnDoor;
            GameEvents.GlideChanged -= OnGlide;
            GameEvents.PlayerReset -= OnRespawn;
            GameEvents.SourceAwakened -= OnCore;
            _allocated.Dispose();
            foreach (var (_, timer) in _timers) timer.Dispose();
            _timers.Clear();
        }

        /// <summary>Finds Unity's timers by name among those this build has (their category is not guessed).</summary>
        private void StartTimers()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(handle);
                if (Array.IndexOf(DevTimers, d.Name) < 0 || _timers.Exists(t => t.name == d.Name)) continue;
                var timer = ProfilerRecorder.StartNew(d.Category, d.Name);
                if (timer.Valid) _timers.Add((d.Name, timer)); else timer.Dispose();
            }
        }

        private void OnApplicationFocus(bool focus) => _focusChangedAt = Time.realtimeSinceStartup;
        private void OnApplicationPause(bool pause) => _focusChangedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            int knock = KnockDown.Shown;
            if (knock != _knockDowns) { Note("a knock-down (a soldier or target went down)"); _knockDowns = knock; }
            float dt = Time.unscaledDeltaTime;
            if (dt >= HitchSeconds && now > QuietAtStart && now - _focusChangedAt > QuietAfterFocus && _written < MostPerRun)
                Write(dt, now, g0 - _gc0, g1 - _gc1, g2 - _gc2);
            _gc0 = g0; _gc1 = g1; _gc2 = g2;
            _recent.RemoveAll(r => now - r.at > RecentSeconds);
            _logs.RemoveAll(l => now - l.at > RecentSeconds);
        }

        // ---------------------------------------------------------------- what was going on

        private void Note(string what)
        {
            float now = Time.realtimeSinceStartup;
            int last = _recent.Count - 1;
            if (last >= 0 && _recent[last].what == what) { _recent[last] = (now, what, _recent[last].times + 1); return; }   // a burst of shots is one line
            _recent.Add((now, what, 1));
            if (_recent.Count > MostRecent) _recent.RemoveAt(0);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Note("scene " + scene.name + " loaded");
        private void OnFired(Projectile prefab, Vector3 at, Vector3 way, float scale, Transform owner, float speed) => Note("a shot (" + (prefab != null ? prefab.name : "?") + ")");
        private void OnShiftStarted(Shifter who, ShiftSize size) => Note("a shift to " + size + " began");
        private void OnShifted(Shifter who, ShiftSize size) => Note("a shift to " + size + " finished");
        private void OnPickedUp(Transform thing) => Note("picked up " + (thing != null ? thing.name : "?"));
        private void OnSetDown(Transform thing) => Note("set down " + (thing != null ? thing.name : "?"));
        private void OnWeaponTaken(WeaponHolder holder, Weapon weapon) => Note("took " + (weapon != null ? weapon.DisplayName : "a weapon"));
        private void OnReload(WeaponHolder holder, BlasterWeapon gun) => Note("a reload");
        private void OnGrenadeThrown(GrenadeThrower thrower) => Note("a grenade thrown");
        private void OnGrenadeWentOff(GrenadeThrower thrower, int hits, bool finished) => Note("a grenade went off");
        private void OnPropBroke(Breakable prop) => Note("a prop broke (" + (prop != null ? prop.name : "?") + ")");
        private void OnSoldierUp(ToySoldier soldier) => Note("a soldier got up");
        private void OnDoor(Door door) => Note("a door opened (" + (door != null ? door.name : "?") + ")");
        private void OnGlide(Glider glider, bool open) => Note(open ? "the glider opened" : "the glider folded");
        private void OnRespawn(Respawn respawn) => Note("the player came back (respawn)");
        private void OnCore(ShiftSource core) => Note("a core woke");

        private void OnLog(string text, string stack, LogType type)
        {
            if (string.IsNullOrEmpty(text) || text.StartsWith("[hitch]")) return;
            string line = "[" + type + "] " + text.Split('\n')[0];
            if (line.Length > 180) line = line.Substring(0, 180) + "...";
            _logs.Add((Time.realtimeSinceStartup, line));
            if (_logs.Count > 3) _logs.RemoveAt(0);
        }

        // ---------------------------------------------------------------- the note

        private void Write(float dt, float now, int quick, int middle, int full)
        {
            string scene = SceneManager.GetActiveScene().name;
            var sb = new StringBuilder();
            if (!_headed)
            {
                _headed = true;
                sb.AppendLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm}  v{Application.version}{(Debug.isDebugBuild ? " (development or editor)" : "")}  " +
                              $"{Screen.width}x{Screen.height}  {SystemInfo.graphicsDeviceName}");
            }
            sb.AppendLine($"{DateTime.Now:HH:mm:ss}  {scene}  HITCH {dt:0.00} s  (frame {Time.frameCount}, {now:0} s into the run)");

            if (_player == null) { var p = GameObject.Find("Player"); if (p != null) _player = p.transform; }
            if (_player != null)
            {
                Vector3 at = _player.position;
                string size = _player.TryGetComponent(out Shifter shifter) ? ", " + shifter.Size : "";
                sb.AppendLine($"  player at ({at.x:0.0}, {at.y:0.0}, {at.z:0.0}){size}");
            }

            long bytes = _allocated.Valid ? _allocated.LastValue : 0;
            string cleanups = quick + middle + full == 0 ? "no garbage collection" : $"garbage collections: {quick} quick, {middle} middle, {full} full";
            sb.AppendLine($"  memory: {cleanups}; {bytes / 1048576f:0.0} MB allocated that frame");

            var timers = new List<string>();
            foreach (var (name, timer) in _timers)
                if (timer.Valid && timer.LastValue >= 5_000_000) timers.Add($"{name} {timer.LastValue / 1e9:0.00} s");   // 5 ms or more
            if (timers.Count > 0) sb.AppendLine("  Unity's timers: " + string.Join(", ", timers));

            if (_recent.Count > 0)
            {
                var parts = new List<string>();
                for (int i = _recent.Count - 1; i >= 0; i--)
                    parts.Add($"{now - _recent[i].at:0.0} s ago {_recent[i].what}" + (_recent[i].times > 1 ? $" (x{_recent[i].times})" : ""));
                sb.AppendLine("  just before: " + string.Join("; ", parts));
            }
            else sb.AppendLine("  just before: nothing the game reports (walking, looking round, the camera)");

            foreach (var (at, line) in _logs) sb.AppendLine($"  logged {now - at:0.0} s before: {line}");

            _written++;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.AppendAllText(FilePath, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[hitch] could not write " + FilePath + ": " + e.Message); }
            Debug.LogWarning($"[hitch] {dt:0.00} s in {scene}" + (_recent.Count > 0 ? ", just after " + _recent[_recent.Count - 1].what : "") + " (Bug notes/hitches.txt)");
        }
    }
}
