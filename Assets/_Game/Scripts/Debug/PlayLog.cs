using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Playtest
{
    /// <summary>
    /// The play log: every play of the game writes itself down, with nothing to press. It starts with Play and closes
    /// when the play stops. In the editor the detail of a play goes to <c>Playtests/play-DATE-TIME.txt</c> beside
    /// Assets (not in version control) and one row is added to <c>docs/PLAYLOG.md</c>; a development build keeps both
    /// in its own data folder. A release build records nothing.
    /// <para>A row says when the play was, how long, what the project was at (the newest decision in
    /// docs/DECISIONS.md, the commit, how many files were uncommitted), each scene and the time in it, the notes, the
    /// errors and warnings, the frame time, and whatever the game counted. So "has this been played?" is a look-up.</para>
    /// <para>The same file sits in each project. The game tells it what happened through the calls below; a project's
    /// own small script (PlayLogEvents) listens to that game's events and passes them on.</para>
    /// <para>Not recorded: runs in batch mode (the headless checks), unless the command line has
    /// <c>-playlog FOLDER</c>, which also sends both files to that folder; a Multiplayer Play Mode virtual player
    /// (only the main editor writes); a play shorter than five seconds.</para>
    /// </summary>
    public static class PlayLog
    {
        internal static PlayLogRecorder Live;

        /// <summary>True while a play is being recorded.</summary>
        public static bool On => Live != null;
        /// <summary>The folder the detail file and its stills are in; null when nothing is recorded.</summary>
        public static string Folder => Live != null ? Live.Folder : null;
        /// <summary>One line in the detail file: something that happened, in plain words.</summary>
        public static void Tell(string what) { if (Live != null) Live.Tell(what); }
        /// <summary>Counted, not listed: for what happens too often for a line each (shots landed). The totals are in the row.</summary>
        public static void Count(string what) { if (Live != null) Live.Count(what); }
        /// <summary>His note: a numbered line with the place of whoever is followed. Returns the number, 0 when nothing is recorded.</summary>
        public static int Note(string words = null) => Live != null ? Live.Note(words) : 0;
        /// <summary>A still of the screen beside the detail file, named after the last note. Returns its path, or null.</summary>
        public static string Still() => Live != null ? Live.Still() : null;
        /// <summary>Whose place is written down as the play goes (the player). Null stops it.</summary>
        public static void Follow(Transform who) { if (Live != null) Live.Follow(who); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Forget() { Live = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Begin()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            string home = null;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-playlog");
            if (at >= 0 && at + 1 < args.Length) home = args[at + 1];
            if (Application.isBatchMode && home == null) return;
            string data = Application.dataPath.Replace('\\', '/');
            if (data.Contains("/Library/VP/")) return;   // a Multiplayer Play Mode virtual player's copy of the project
            string project = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..")) : null;
            if (home == null) home = project ?? Application.persistentDataPath;
            GameObject go = null;
            try
            {
                go = new GameObject("PlayLog");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var recorder = go.AddComponent<PlayLogRecorder>();
                recorder.Open(home, project);
                Live = recorder;
            }
            catch (Exception e)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
                Debug.LogWarning("PlayLog: this play is not recorded (" + e.Message + ")");
            }
        }
    }

    /// <summary>The object that keeps one play's record. Made by <see cref="PlayLog"/>; nothing to place in a scene.</summary>
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class PlayLogRecorder : MonoBehaviour
    {
        // Written out every FlushEvery seconds, so a crash keeps most of a play. No input for IdleAfter seconds is idle.
        // A frame over HitchMs is a hitch. A frame that began in a scene's first SettleSeconds is left out of the frame time (loading).
        // The followed place is written every FollowEvery seconds when it moved more than FollowMoved.
        private const float FlushEvery = 10f, IdleAfter = 30f, HitchMs = 100f, SettleSeconds = 3f, FollowEvery = 2f, FollowMoved = 0.5f;
        private const float Shortest = 5f;
        private const int MostErrors = 100, MostWarnings = 50, MostHitches = 60, MostRemembered = 1000;
        private const string Header = "# PLAYLOG: every play of the game, written by the game itself (PlayLog.cs)\n\n"
            + "One row per play in the editor, newest last. \"Played at\" is what the project was at when Play was pressed: the newest\n"
            + "decision in docs/DECISIONS.md, the commit, and how many files were uncommitted. The detail of a play (every event, note\n"
            + "and hitch, with its time) is the named file in `Playtests/` beside Assets, which is not in version control.\n\n"
            + "| When | Length | Played at | Scenes | Notes | Errors | Frames | Counted | Detail |\n"
            + "|---|---|---|---|---|---|---|---|---|\n";
        private static readonly UTF8Encoding Plain = new UTF8Encoding(false);
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Facts { public volatile string decision = "", commit = ""; public volatile int uncommitted = -1; }

        private readonly StringBuilder _lines = new StringBuilder();
        private readonly Dictionary<string, double> _sceneSeconds = new Dictionary<string, double>();
        private readonly List<string> _sceneOrder = new List<string>();
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private readonly List<string> _countOrder = new List<string>();
        private readonly HashSet<string> _told = new HashSet<string>();
        private readonly Facts _facts = new Facts();
        private Task _factsTask;
        private string _file, _stem, _logFile, _scene = "";
        private int _sceneHandle = int.MinValue;
        private DateTime _startedAt;
        private double _began, _last, _sceneSince, _nextFlush, _nextFollow, _lastInput, _pausedAt = -1, _awayAt = -1;
        private double _paused, _idle, _away, _frameSeconds, _worst;
        private long _frames;
        private int _hitches, _notes, _stills, _stillNote, _errors, _warnings, _errorsTold, _warningsTold;
        private bool _open, _closed, _isIdle, _skipFrame, _factsTold;
        private Transform _followed;
        private Vector3 _followedAt;
        private bool _followedTold;

        public string Folder { get; private set; }
        private static double Now => Time.realtimeSinceStartupAsDouble;

        // ------------------------------------------------------------------ start
        internal void Open(string home, string project)
        {
            Folder = Path.Combine(home, "Playtests");
            Directory.CreateDirectory(Folder);
            _logFile = Application.isEditor ? Path.Combine(home, "docs", "PLAYLOG.md") : Path.Combine(Folder, "PLAYLOG.md");
            CloseLeftOpen();
            _startedAt = DateTime.Now;
            _stem = "play-" + _startedAt.ToString("yyyyMMdd-HHmmss", Inv);
            _file = Path.Combine(Folder, _stem + ".txt");
            _began = _last = _lastInput = _sceneSince = Now;
            _nextFlush = _began + FlushEvery; _nextFollow = _began + FollowEvery;
            var scene = SceneManager.GetActiveScene();
            _sceneHandle = scene.handle; _scene = NameOf(scene); Seen(_scene);
            string where = Application.isEditor ? "editor" : "development build " + Application.version;
            File.WriteAllText(_file, "# play log: " + Application.productName + ", started " + _startedAt.ToString("yyyy-MM-dd HH:mm:ss", Inv)
                + " (" + where + ", Unity " + Application.unityVersion + ")\n# columns: time into the play, scene, what happened\n", Plain);
            Tell("scene " + _scene);
            if (project != null) _factsTask = Task.Run(() => ReadFacts(project, _facts));
            Application.logMessageReceived += Heard;
            Application.focusChanged += Focused;
            SceneManager.sceneLoaded += Loaded;
            SceneManager.activeSceneChanged += Switched;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.pauseStateChanged += Paused;
#endif
            _open = true;
        }

        /// <summary>A play that ended without a stop (the editor closed hard, or crashed) left its file without an end
        /// line. The newest such file gets its row now, with what is known, so the log has no silent gap.</summary>
        private void CloseLeftOpen()
        {
            try
            {
                string[] files = Directory.GetFiles(Folder, "play-*.txt");
                if (files.Length == 0) return;
                Array.Sort(files, StringComparer.Ordinal);
                string newest = files[files.Length - 1];
                string[] lines = File.ReadAllLines(newest);
                string lastTime = "";
                foreach (string l in lines)
                {
                    if (l.StartsWith("# end", StringComparison.Ordinal)) return;
                    int tab = l.IndexOf('\t');
                    if (tab > 0 && char.IsDigit(l[0])) lastTime = l.Substring(0, tab);
                }
                string name = Path.GetFileNameWithoutExtension(newest);
                string when = name;
                if (DateTime.TryParseExact(name.Substring("play-".Length), "yyyyMMdd-HHmmss", Inv, DateTimeStyles.None, out DateTime t))
                    when = t.ToString("d MMM yyyy HH:mm", Inv);
                string seen = lastTime.Length > 0 ? "at least " + lastTime : "not known";
                File.AppendAllText(newest, "# end not seen: the play stopped without closing its log (the editor closed hard or crashed); last line at "
                    + (lastTime.Length > 0 ? lastTime : "the start") + "\n", Plain);
                AddRow("| " + when + " | " + seen + " | not known | not known | not known | not known | not known | the play ended without a stop (the editor closed hard or crashed) | "
                    + Path.GetFileName(newest) + " |\n");
            }
            catch (Exception) { /* an old file that cannot be read is no reason to lose this play */ }
        }

        private static void ReadFacts(string project, Facts facts)
        {
            try
            {
                string decisions = Path.Combine(project, "docs", "DECISIONS.md");
                if (File.Exists(decisions))
                {
                    int most = -1;
                    foreach (Match m in Regex.Matches(File.ReadAllText(decisions), @"\bD-(\d{4})\b"))
                    {
                        int n = int.Parse(m.Groups[1].Value, Inv);
                        if (n > most) most = n;
                    }
                    if (most >= 0) facts.decision = "D-" + most.ToString("0000", Inv);
                }
            }
            catch (Exception) { }
            try { facts.commit = Head(project); } catch (Exception) { }
#if UNITY_EDITOR
            if (facts.commit.Length == 0) return;
            try
            {
                // --no-optional-locks: only reads, so a commit going on in another window is not disturbed.
                var info = new System.Diagnostics.ProcessStartInfo("git", "--no-optional-locks status --porcelain")
                { WorkingDirectory = project, UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                using (var git = System.Diagnostics.Process.Start(info))
                {
                    Task<string> read = git.StandardOutput.ReadToEndAsync();
                    if (!git.WaitForExit(20000)) { try { git.Kill(); } catch (Exception) { } return; }
                    if (git.ExitCode != 0) return;
                    int n = 0;
                    foreach (string l in read.Result.Split('\n')) if (l.Trim().Length > 0) n++;
                    facts.uncommitted = n;
                }
            }
            catch (Exception) { }
#endif
        }

        /// <summary>The commit the project is on, read from the .git folder: "648761f (main)". Empty when there is none.</summary>
        private static string Head(string project)
        {
            string git = Path.Combine(project, ".git");
            string headFile = Path.Combine(git, "HEAD");
            if (!File.Exists(headFile)) return "";
            string head = File.ReadAllText(headFile).Trim();
            if (!head.StartsWith("ref:", StringComparison.Ordinal)) return head.Length >= 7 ? head.Substring(0, 7) + " (no branch)" : "";
            string reference = head.Substring(4).Trim();
            string branch = reference.StartsWith("refs/heads/", StringComparison.Ordinal) ? reference.Substring("refs/heads/".Length) : reference;
            string hash = "";
            string loose = Path.Combine(git, reference.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(loose)) hash = File.ReadAllText(loose).Trim();
            else
            {
                string packed = Path.Combine(git, "packed-refs");
                if (File.Exists(packed))
                    foreach (string l in File.ReadAllLines(packed))
                        if (l.EndsWith(" " + reference, StringComparison.Ordinal)) { hash = l.Substring(0, l.IndexOf(' ')); break; }
            }
            return hash.Length >= 7 ? hash.Substring(0, 7) + " (" + branch + ")" : "";
        }

        // ------------------------------------------------------------------ what the game tells
        internal void Tell(string what)
        {
            if (_closed || string.IsNullOrEmpty(what)) return;
            _lines.Append(Clock(Now - _began)).Append('\t').Append(_scene).Append('\t').Append(OneLine(what)).Append('\n');
        }

        internal void Count(string what)
        {
            if (_closed || string.IsNullOrEmpty(what)) return;
            if (_counts.TryGetValue(what, out int n)) _counts[what] = n + 1;
            else { _counts[what] = 1; _countOrder.Add(what); }
        }

        internal int Note(string words)
        {
            if (_closed) return 0;
            _notes++;
            string where = _followed != null ? " at " + Place(_followed.position) : "";
            Tell("NOTE " + _notes + (string.IsNullOrEmpty(words) ? "" : ": " + words) + where);
            return _notes;
        }

        internal string Still()
        {
            if (_closed) return null;
            // Named after the note it belongs to; a second still of the same note gets a letter.
            int note = Mathf.Max(1, _notes);
            _stills = note == _stillNote ? _stills + 1 : 1;
            _stillNote = note;
            string name = _stem + "-note-" + note.ToString("00", Inv) + (_stills > 1 ? ((char)('a' + Mathf.Min(_stills - 1, 25))).ToString() : "") + ".png";
            string path = Path.Combine(Folder, name);
            ScreenCapture.CaptureScreenshot(path);
            Tell("still " + name);
            return path;
        }

        internal void Follow(Transform who) { _followed = who; _followedTold = false; }

        // ------------------------------------------------------------------ what it notices itself
        private void Update()
        {
            if (!_open || _closed) return;
            double now = Now, dt = now - _last;
            _last = now;

            Enter(SceneManager.GetActiveScene());   // the event below tells most changes; this catches any it does not
            _sceneSeconds[_scene] += dt;

            if (_skipFrame) _skipFrame = false;
            else if (now - dt - _sceneSince > SettleSeconds && dt > 0)
            {
                _frames++; _frameSeconds += dt;
                if (dt > _worst) _worst = dt;
                if (dt * 1000.0 > HitchMs && ++_hitches <= MostHitches) Tell("hitch " + (dt * 1000.0).ToString("0", Inv) + " ms");
            }

            if (AnyInput())
            {
                if (_isIdle) { _isIdle = false; _idle += now - _lastInput; Tell("back after " + Span(now - _lastInput) + " without input"); }
                _lastInput = now;
            }
            else if (!_isIdle && now - _lastInput > IdleAfter) { _isIdle = true; Tell("idle: no input for " + IdleAfter.ToString("0", Inv) + " s"); }

            if (!_factsTold && _factsTask != null && _factsTask.IsCompleted) { _factsTold = true; Tell("project at " + PlayedAt()); }

            if (now >= _nextFollow)
            {
                _nextFollow = now + FollowEvery;
                if (_followed != null)
                {
                    Vector3 p = _followed.position;
                    if (!_followedTold || (p - _followedAt).sqrMagnitude > FollowMoved * FollowMoved) { _followedAt = p; _followedTold = true; Tell("at " + Place(p)); }
                }
            }

            if (now >= _nextFlush) { _nextFlush = now + FlushEvery; Flush(); }
        }

        private static bool AnyInput()
        {
            var k = Keyboard.current;
            if (k != null && k.anyKey.isPressed) return true;
            var m = Mouse.current;
            if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed
                || m.delta.ReadValue().sqrMagnitude > 0f || m.scroll.ReadValue().sqrMagnitude > 0f)) return true;
            var g = Gamepad.current;
            if (g == null) return false;
            return g.leftStick.ReadValue().sqrMagnitude > 0.04f || g.rightStick.ReadValue().sqrMagnitude > 0.04f
                || g.dpad.ReadValue().sqrMagnitude > 0.04f || g.leftTrigger.ReadValue() > 0.1f || g.rightTrigger.ReadValue() > 0.1f
                || g.buttonSouth.isPressed || g.buttonNorth.isPressed || g.buttonEast.isPressed || g.buttonWest.isPressed
                || g.leftShoulder.isPressed || g.rightShoulder.isPressed || g.startButton.isPressed || g.selectButton.isPressed
                || g.leftStickButton.isPressed || g.rightStickButton.isPressed;
        }

        private void Switched(Scene from, Scene to) => Enter(to);

        /// <summary>The scene he is in changed: lines from here on carry its name, and its time starts counting.</summary>
        private void Enter(Scene scene)
        {
            if (scene.handle == _sceneHandle) return;
            _sceneHandle = scene.handle; _scene = NameOf(scene); Seen(_scene); _sceneSince = Now;
            Tell("scene " + _scene);
        }

        private void Loaded(Scene scene, LoadSceneMode mode)
        {
            _sceneSince = Now;
            if (mode == LoadSceneMode.Additive) Tell("loaded " + NameOf(scene) + " (added to " + _scene + ")");
        }

        private void Heard(string text, string trace, LogType type)
        {
            bool bad = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (!bad && type != LogType.Warning) return;
            if (bad) _errors++; else _warnings++;
            string first = text ?? "";
            int cut = first.IndexOf('\n');
            if (cut >= 0) first = first.Substring(0, cut);
            if (first.Length > 300) first = first.Substring(0, 300);
            if (_told.Count >= MostRemembered || !_told.Add(first)) return;   // each one once
            if (bad) { if (_errorsTold++ < MostErrors) Tell("ERROR: " + first); }
            else if (_warningsTold++ < MostWarnings) Tell("warning: " + first);
        }

        private void Focused(bool has)
        {
            double now = Now;
            if (!has) { if (_awayAt < 0) { _awayAt = now; Tell("window left"); } return; }
            if (_awayAt < 0) return;
            _away += now - _awayAt;
            Tell("window back after " + Span(now - _awayAt));
            _awayAt = -1;
            if (now - _last > 0.5) _skipFrame = true;   // the game stood still while away: that gap is not a slow frame
        }

#if UNITY_EDITOR
        private void Paused(UnityEditor.PauseState state)
        {
            double now = Now;
            if (state == UnityEditor.PauseState.Paused) { if (_pausedAt < 0) { _pausedAt = now; Tell("paused (the editor's pause)"); } return; }
            if (_pausedAt < 0) return;
            double span = now - _pausedAt;
            _paused += span; _lastInput += span; _pausedAt = -1; _skipFrame = true;
            Tell("resumed after " + Span(span));
        }
#endif

        // ------------------------------------------------------------------ end
        private void OnApplicationQuit() => Close();
        private void OnDestroy() => Close();

        private void Close()
        {
            if (!_open || _closed) return;
            double now = Now, length = now - _began;
            if (_isIdle) _idle += now - _lastInput;
            if (_pausedAt >= 0) _paused += now - _pausedAt;
            if (_awayAt >= 0) _away += now - _awayAt;
            Application.logMessageReceived -= Heard;
            Application.focusChanged -= Focused;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.activeSceneChanged -= Switched;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.pauseStateChanged -= Paused;
#endif
            if (length < Shortest)
            {
                _closed = true;
                if (PlayLog.Live == this) PlayLog.Live = null;
                try { File.Delete(_file); } catch (Exception) { }
                return;
            }
            try { if (_factsTask != null) _factsTask.Wait(3000); } catch (Exception) { }
            if (!_factsTold && _factsTask != null) { _factsTold = true; Tell("project at " + PlayedAt()); }

            string lengthText = Span(length);
            var rest = new List<string>();
            if (_idle >= 1) rest.Add("idle " + Span(_idle));
            if (_paused >= 1) rest.Add("paused " + Span(_paused));
            if (_away >= 1) rest.Add("window away " + Span(_away));
            if (rest.Count > 0) lengthText += " (" + string.Join(", ", rest) + ")";

            var scenes = new List<string>();
            foreach (string s in _sceneOrder) scenes.Add(s + " " + Span(_sceneSeconds[s]));
            string scenesText = string.Join(", ", scenes);

            string frames = _frames > 0
                ? (_frameSeconds / _frames * 1000.0).ToString("0.0", Inv) + " ms average, " + (_worst * 1000.0).ToString("0", Inv) + " worst, "
                    + _hitches + " over " + HitchMs.ToString("0", Inv) + " ms"
                : "none measured";

            var counted = new List<string>();
            foreach (string c in _countOrder) counted.Add(c + " " + _counts[c]);
            string countedText = counted.Count > 0 ? string.Join(", ", counted) : "nothing";

            string errorsText = _errors + (_errors == 1 ? " error, " : " errors, ") + _warnings + (_warnings == 1 ? " warning" : " warnings");
            string playedAt = PlayedAt();

            _lines.Append("# end ").Append(DateTime.Now.ToString("HH:mm:ss", Inv)).Append(", ").Append(lengthText).Append('\n')
                .Append("# played at: ").Append(playedAt).Append('\n')
                .Append("# scenes: ").Append(scenesText).Append('\n')
                .Append("# frames: ").Append(frames).Append(" (frames begun in the first ").Append(SettleSeconds.ToString("0", Inv)).Append(" s of a scene left out)\n")
                .Append("# counted: ").Append(countedText).Append('\n')
                .Append("# notes ").Append(_notes).Append(", ").Append(errorsText).Append('\n');
            Flush();
            _closed = true;
            if (PlayLog.Live == this) PlayLog.Live = null;

            AddRow("| " + _startedAt.ToString("d MMM yyyy HH:mm", Inv) + " | " + Cell(lengthText) + " | " + Cell(playedAt) + " | " + Cell(scenesText)
                + " | " + _notes + " | " + errorsText + " | " + Cell(frames) + " | " + Cell(countedText) + " | " + _stem + ".txt |\n");
        }

        private string PlayedAt()
        {
            var parts = new List<string>();
            if (_facts.decision.Length > 0) parts.Add(_facts.decision);
            if (_facts.commit.Length > 0) parts.Add(_facts.commit + (_facts.uncommitted < 0 ? "" : _facts.uncommitted == 0 ? ", clean" : ", " + _facts.uncommitted + " uncommitted"));
            if (parts.Count > 0) return string.Join(", ", parts);
            return Application.isEditor ? "not known (no docs/DECISIONS.md, no git)" : "build " + Application.version;
        }

        private void AddRow(string row)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFile));
                if (!File.Exists(_logFile)) File.WriteAllText(_logFile, Header, Plain);
                File.AppendAllText(_logFile, row, Plain);
            }
            catch (Exception e) { Debug.LogWarning("PlayLog: the row was not written to " + _logFile + " (" + e.Message + ")"); }
        }

        private void Flush()
        {
            if (_lines.Length == 0) return;
            try { File.AppendAllText(_file, _lines.ToString(), Plain); _lines.Length = 0; }
            catch (Exception) { /* the file is busy: kept, and tried again at the next flush */ }
        }

        // ------------------------------------------------------------------ words
        private void Seen(string scene) { if (!_sceneSeconds.ContainsKey(scene)) { _sceneSeconds[scene] = 0; _sceneOrder.Add(scene); } }
        private static string NameOf(Scene scene) => string.IsNullOrEmpty(scene.name) ? "(no scene)" : scene.name;
        private static string Place(Vector3 p) => "(" + p.x.ToString("0.0", Inv) + ", " + p.y.ToString("0.0", Inv) + ", " + p.z.ToString("0.0", Inv) + ")";
        private static string OneLine(string s) => s.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        private static string Cell(string s) => OneLine(s).Replace('|', '/');

        /// <summary>A time into the play, to the tenth: 3:07.4</summary>
        private static string Clock(double seconds)
        {
            long tenths = (long)Math.Round((seconds < 0 ? 0 : seconds) * 10.0);
            return (tenths / 600).ToString(Inv) + ":" + (tenths % 600 / 10).ToString("00", Inv) + "." + (tenths % 10).ToString(Inv);
        }

        /// <summary>A length of time: 0:42, 12:04, 1:03:20</summary>
        private static string Span(double seconds)
        {
            long s = (long)Math.Round(seconds < 0 ? 0 : seconds);
            long h = s / 3600, m = s % 3600 / 60;
            return h > 0 ? h.ToString(Inv) + ":" + m.ToString("00", Inv) + ":" + (s % 60).ToString("00", Inv)
                         : m.ToString(Inv) + ":" + (s % 60).ToString("00", Inv);
        }
    }
}
