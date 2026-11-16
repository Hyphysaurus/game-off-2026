using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Playtest tools, on screen. F1 = the readout (speed, size, ground, last drop, what E would do); ` (backquote) = the
    /// console (log tail + commands). Hotkeys: F5 respawn, F6 fly, F7 god (no fall rule), F8 toy/kid, PgUp/PgDn previous
    /// /next checkpoint, F12 mark (a note line + a screenshot into docs). Spawns itself in the editor and in development
    /// builds; nothing to place in a scene. Type "help" in the console.
    /// </summary>
    public sealed class DebugConsole : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (FindFirstObjectByType<DebugConsole>() != null) return;
            var go = new GameObject("DebugConsole"); DontDestroyOnLoad(go); go.AddComponent<DebugConsole>();
        }

        private struct Line { public string text; public LogType type; }
        private readonly List<Line> _log = new List<Line>();
        private readonly List<string> _history = new List<string>();
        private int _historyAt = -1;
        private string _input = "";
        private bool _hud = true, _open, _fly, _god, _focusNext;
        private Vector2 _scroll;
        private float _fps, _lastDrop = -1f, _godKeep = 12f;
        private string _lastDropRoom = "";
        private CursorLockMode _cursorKeep; private bool _cursorVisibleKeep;
        private GUIStyle _box, _mono, _monoSmall;

        private GameObject _player; private PlayerMotor _motor; private Shifter _shifter; private Interactor _interactor;
        private Respawn _respawn; private CharacterController _cc; private PlayerInput _input2; private int _cpIndex = -1;

        private static string DocsDir => Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs")) : Application.persistentDataPath;

        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }

        private void OnLog(string text, string stack, LogType type)
        {
            _log.Add(new Line { text = text.Split('\n')[0], type = type });
            if (_log.Count > 300) _log.RemoveRange(0, _log.Count - 300);
            _scroll.y = float.MaxValue;
        }
        private void Say(string s) => _log.Add(new Line { text = "> " + s, type = LogType.Log });

        private bool Bind()
        {
            if (_player != null) return true;
            _player = GameObject.Find("Player"); if (_player == null) return false;
            _motor = _player.GetComponent<PlayerMotor>(); _shifter = _player.GetComponent<Shifter>(); _interactor = _player.GetComponent<Interactor>();
            _respawn = _player.GetComponent<Respawn>(); _cc = _player.GetComponent<CharacterController>(); _input2 = _player.GetComponent<PlayerInput>();
            _motor.Landed += OnLanded; _fly = false;
            return true;
        }

        private void OnLanded(float fallSpeed)
        {
            float g = 25f; var s = MotorSettings(); if (s != null) g = s.gravity;
            _lastDrop = fallSpeed * fallSpeed / (2f * g);
            _lastDropRoom = Room();
        }

        private void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            var k = Keyboard.current; if (k == null) return;
            if (k.backquoteKey.wasPressedThisFrame) SetOpen(!_open);
            if (_open) return;
            if (k.f1Key.wasPressedThisFrame) _hud = !_hud;
            if (!Bind()) return;
            if (k.f5Key.wasPressedThisFrame) Run("respawn");
            if (k.f6Key.wasPressedThisFrame) Run("fly");
            if (k.f7Key.wasPressedThisFrame) Run("god");
            if (k.f8Key.wasPressedThisFrame) Run("shift");
            if (k.pageDownKey.wasPressedThisFrame) Run("next");
            if (k.pageUpKey.wasPressedThisFrame) Run("prev");
            if (k.f12Key.wasPressedThisFrame) Run("mark");
            if (k.f2Key.wasPressedThisFrame) Run("core");
            if (_fly && GameMode.Current == GameMode.Mode.Play) Fly(k);   // Build Mode's WASD is its camera's
        }

        private void Fly(Keyboard k)
        {
            float s = (_shifter != null ? _shifter.Scale : 1f) * (k.leftShiftKey.isPressed ? 40f : 12f) * Time.unscaledDeltaTime;
            var cam = Camera.main != null ? Camera.main.transform : _player.transform;
            Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized, r = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized, d = Vector3.zero;
            if (k.wKey.isPressed) d += f; if (k.sKey.isPressed) d -= f; if (k.dKey.isPressed) d += r; if (k.aKey.isPressed) d -= r;
            if (k.spaceKey.isPressed) d += Vector3.up; if (k.leftCtrlKey.isPressed || k.cKey.isPressed) d -= Vector3.up;
            _player.transform.position += d.normalized * s;
            if (d.sqrMagnitude > 0.01f && new Vector3(d.x, 0, d.z).sqrMagnitude > 0.01f) _player.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.z));
        }

        /// <summary>True while the console is open and typing: other key readers (Build Mode, the garage) stand back.</summary>
        public static bool IsOpen { get; private set; }

        private void SetOpen(bool open)
        {
            _open = open;
            IsOpen = open;
            if (_input2 != null) _input2.enabled = !open;
            if (open) { _cursorKeep = Cursor.lockState; _cursorVisibleKeep = Cursor.visible; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; _focusNext = true; }
            else { Cursor.lockState = _cursorKeep; Cursor.visible = _cursorVisibleKeep; }
        }

        // ------------------------------------------------------------------ commands
        private void Run(string line)
        {
            line = line.Trim(); if (line.Length == 0) return;
            string[] a = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = a[0].ToLowerInvariant();
            if (cmd != "help" && cmd != "clear" && !Bind()) { Say("no Player in the scene"); return; }
            try
            {
                switch (cmd)
                {
                    case "help":
                        Say("tp list | tp <n> | tp <name> | tp <x> <y> <z>   next / prev (PgDn/PgUp): checkpoints in order");
                        Say("toy | kid | shift (F8)   fly (F6, WASD + Space/Ctrl, Shift fast)   god (F7: no fall rule)   respawn (F5)   reload");
                        Say("time <scale>   tune <MovementSettings field> [value] (no value = read; edits the ASSET in the editor)");
                        Say("set <object> <Component> <field> <value>   get <object> <Component> <field>   (live, any serialized field)");
                        Say("reveal (replays the playroom reveal from wherever you stand)");
                        Say("core (F2): calls the Pocket Core, a shift core you can use anywhere, a step in front of you");
                        Say("workshop (load the Workshop sandbox) | course (back to the course scene)");
                        Say("save (here, active slot) | save clear (empties the active slot)");
                        Say("note <text> -> docs/PLAYTEST_NOTES.md with room, size, position   mark (F12) = note + screenshot   shot   clear");
                        break;
                    case "clear": _log.Clear(); break;
                    case "toy": _shifter.ShiftTo(ShiftSize.Toy); Say("toy"); break;
                    case "kid": _shifter.ShiftTo(ShiftSize.Kid); Say("kid"); break;
                    case "shift": _shifter.Toggle(); Say("shift -> " + (_shifter.Size == ShiftSize.Toy ? "kid" : "toy")); break;
                    case "respawn": _respawn.Reset(); Say("respawn"); break;
                    case "reload": Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); break;
                    case "time": Time.timeScale = Mathf.Clamp(float.Parse(a[1]), 0f, 4f); Say("time scale " + Time.timeScale); break;
                    case "fly":
                        // only on foot in play: Build Mode, first person and the car seats own the motor and capsule
                        // while they are on, and switching them back on under those fights them (D-0030)
                        if (GameMode.Current != GameMode.Mode.Play) { Say("fly: back to play first (F3 / V off)"); break; }
                        if (!_fly && (!_motor.enabled || !_cc.enabled)) { Say("fly: not while driving or waking up"); break; }
                        _fly = !_fly; _motor.enabled = !_fly; _cc.enabled = !_fly;
                        if (!_fly) _motor.Warp(_player.transform.position, _player.transform.rotation);
                        Say("fly " + (_fly ? "ON" : "off")); break;
                    case "god":
                        _god = !_god;
                        var f = typeof(Respawn).GetField("fatalFallHeight", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (_god) { _godKeep = (float)f.GetValue(_respawn); f.SetValue(_respawn, 0f); } else f.SetValue(_respawn, _godKeep);
                        Say("god " + (_god ? "ON (no fall rule)" : "off")); break;
                    case "next": case "prev": StepCheckpoint(cmd == "next" ? 1 : -1); break;
                    case "tp": Teleport(a); break;
                    case "tune": Tune(a); break;
                    case "set": case "get": SetGet(a, cmd == "set"); break;
                    case "note": Note(line.Substring(4).Trim()); break;
                    case "mark": Note("mark"); Shot(); break;
                    case "core": Say(PocketCore.Summon()); break;
                    case "shot": Shot(); break;
                    case "workshop": Time.timeScale = 1f; SceneManager.LoadScene(TitleMenu.WorkshopScene); break;
                    case "raceway": Time.timeScale = 1f; SceneManager.LoadScene(TitleMenu.RacewayScene); break;
                    case "course": Time.timeScale = 1f; SceneManager.LoadScene("Greybox"); break;
                    case "reveal": { var rv = FindFirstObjectByType<RoomReveal>(); if (rv != null) { rv.Play(true); Say("reveal"); } else Say("no reveal in the scene"); break; }
                    case "save":
                        if (a.Length > 1 && a[1] == "clear") { SaveGame.Clear(SaveGame.Slot); Say("slot " + (SaveGame.Slot + 1) + " cleared"); }
                        else { SaveGame.Save(_player.transform.position, _player.transform.eulerAngles.y, _shifter.Size, Room()); Say("saved to slot " + (SaveGame.Slot + 1)); }
                        break;
                    default: Say("unknown: " + cmd + " (help)"); break;
                }
            }
            catch (Exception e) { Say("error: " + e.Message); }
        }

        private List<Checkpoint> Checkpoints() => FindObjectsByType<Checkpoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .OrderBy(c => PathOf(c.transform)).ToList();
        private static string PathOf(Transform t) { string p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }

        private void StepCheckpoint(int step)
        {
            var cps = Checkpoints(); if (cps.Count == 0) { Say("no checkpoints"); return; }
            _cpIndex = ((_cpIndex + step) % cps.Count + cps.Count) % cps.Count;
            Go(cps[_cpIndex].transform.position, cps[_cpIndex].transform.rotation, _cpIndex + " " + cps[_cpIndex].name + " (" + cps[_cpIndex].transform.parent.name + ")");
        }

        private void Teleport(string[] a)
        {
            if (a.Length < 2 || a[1] == "list")
            {
                var cps = Checkpoints(); for (int i = 0; i < cps.Count; i++) Say(i + "  " + cps[i].transform.parent.name + "/" + cps[i].name + "  " + cps[i].transform.position.ToString("0"));
                return;
            }
            if (a.Length >= 4) { Go(new Vector3(float.Parse(a[1]), float.Parse(a[2]), float.Parse(a[3])), _player.transform.rotation, "xyz"); return; }
            if (int.TryParse(a[1], out int n)) { var cps = Checkpoints(); _cpIndex = Mathf.Clamp(n, 0, cps.Count - 1); Go(cps[_cpIndex].transform.position, cps[_cpIndex].transform.rotation, cps[_cpIndex].name); return; }
            var go = GameObject.Find(a[1]) ?? FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name.Equals(a[1], StringComparison.OrdinalIgnoreCase))?.gameObject;
            if (go == null) { Say("nothing named " + a[1]); return; }
            var rs = go.GetComponentsInChildren<Renderer>(); Vector3 p = go.transform.position;
            if (rs.Length > 0) { Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); p = new Vector3(b.center.x, b.max.y + 0.5f, b.center.z); }
            Go(p, _player.transform.rotation, go.name);
        }

        private void Go(Vector3 p, Quaternion r, string what)
        {
            if (_fly) _player.transform.SetPositionAndRotation(p, r); else _motor.Warp(p + Vector3.up * 0.2f, r);
            Say("tp " + what + " " + p.ToString("0.0"));
        }

        private MovementSettings MotorSettings() => _motor == null ? null : typeof(PlayerMotor).GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_motor) as MovementSettings;

        private void Tune(string[] a)
        {
            var s = MotorSettings(); if (s == null) { Say("no settings"); return; }
            if (a.Length < 2) { foreach (var fi in typeof(MovementSettings).GetFields()) Say(fi.Name + " = " + fi.GetValue(s)); return; }
            var field = typeof(MovementSettings).GetField(a[1]); if (field == null) { Say("no field " + a[1]); return; }
            if (a.Length >= 3) field.SetValue(s, float.Parse(a[2]));
            Say(a[1] + " = " + field.GetValue(s));
        }

        private void SetGet(string[] a, bool set)
        {
            if (a.Length < 4 || (set && a.Length < 5)) { Say("set <object> <Component> <field> <value>"); return; }
            var go = GameObject.Find(a[1]) ?? FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == a[1])?.gameObject;
            if (go == null) { Say("nothing named " + a[1]); return; }
            var comp = go.GetComponents<Component>().FirstOrDefault(c => c.GetType().Name.Equals(a[2], StringComparison.OrdinalIgnoreCase));
            if (comp == null) { Say(a[1] + " has no " + a[2]); return; }
            FieldInfo fi = null; for (var t = comp.GetType(); t != null && fi == null; t = t.BaseType) fi = t.GetField(a[3], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) { Say("no field " + a[3]); return; }
            if (set)
            {
                object v = fi.FieldType == typeof(float) ? float.Parse(a[4]) : fi.FieldType == typeof(int) ? int.Parse(a[4]) : fi.FieldType == typeof(bool) ? (object)bool.Parse(a[4]) : a[4];
                fi.SetValue(comp, v);
            }
            Say(a[1] + "." + a[2] + "." + a[3] + " = " + fi.GetValue(comp));
        }

        private string Room()
        {
            if (_player == null) return "";
            if (Physics.Raycast(_player.transform.position + Vector3.up * 0.5f * Scale, Vector3.down, out var hit, 5f * Scale, ~0, QueryTriggerInteraction.Ignore))
                for (var t = hit.collider.transform; t != null; t = t.parent) if (t.name.Length > 2 && (t.name[0] == 'R' && char.IsDigit(t.name[1]) || t.name == "Bedroom" || t.name == "ToyBox")) return t.name;
            return "?";
        }
        private float Scale => _shifter != null ? _shifter.Scale : 1f;

        private Animator _anim;

        private void Note(string text)
        {
            Directory.CreateDirectory(DocsDir);
            string file = System.IO.Path.Combine(DocsDir, "PLAYTEST_NOTES.md");
            string line = $"- {DateTime.Now:MM-dd HH:mm:ss} [{Room()}] {(_shifter != null ? _shifter.Size.ToString() : "?")} at {_player.transform.position:0.0}: {text}   ({Footing()})";
            File.AppendAllText(file, line + Environment.NewLine);
            Say("noted: " + text);
        }

        /// <summary>How the body sits on what is under it, in the player's own units (so toy and kid read alike): "sunk" is
        /// how far the root (capsule bottom) is below the surface straight under the centre; each foot's gap is its toe height
        /// over the surface under that foot (negative = in it). For the "feet sink into cardboard / foam mats" report.</summary>
        private string Footing()
        {
            if (_player == null) return "";
            float sc = Scale; Vector3 root = _player.transform.position;
            string centre = "-";
            if (Physics.Raycast(root + Vector3.up * 0.6f * sc, Vector3.down, out var h, 2f * sc, ~0, QueryTriggerInteraction.Ignore))
                centre = $"{(h.point.y - root.y) / sc:+0.00;-0.00}";
            if (_anim == null) _anim = _player.GetComponentInChildren<Animator>();
            string Foot(HumanBodyBones bone)
            {
                var t = _anim != null && _anim.isHuman ? _anim.GetBoneTransform(bone) : null; if (t == null) return "-";
                return Physics.Raycast(t.position + Vector3.up * 0.6f * sc, Vector3.down, out var fh, 2f * sc, ~0, QueryTriggerInteraction.Ignore)
                    ? $"{(t.position.y - fh.point.y) / sc:+0.00;-0.00} on {fh.collider.name}" : "-";
            }
            return $"sunk {centre}   toe L {Foot(HumanBodyBones.LeftToes)}  R {Foot(HumanBodyBones.RightToes)}";
        }

        private void Shot()
        {
            string dir = System.IO.Path.Combine(DocsDir, "stills"); Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, $"play_{DateTime.Now:MMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(file); Say("shot " + System.IO.Path.GetFileName(file));
        }

        // ------------------------------------------------------------------ drawing
        private void Styles()
        {
            if (_box != null) return;
            var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.62f)); bg.Apply();
            _box = new GUIStyle(GUI.skin.box) { normal = { background = bg }, alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 6, 6) };
            _mono = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, normal = { textColor = Color.white } };
            _monoSmall = new GUIStyle(_mono) { fontSize = 13 };
        }

        private void OnGUI()
        {
            Styles();
            if (_hud && Bind() && GameMode.Current != GameMode.Mode.Build) DrawHud();   // Build Mode has its own toolbar and status line there
            if (_open) DrawConsole();
        }

        private void DrawHud()
        {
            Vector3 v = _motor.Velocity; float sc = Scale;
            float planar = new Vector3(v.x, 0, v.z).magnitude / sc, vert = v.y / sc;
            string ground = "-"; float slope = 0f;
            if (Physics.Raycast(_player.transform.position + Vector3.up * 0.5f * sc, Vector3.down, out var hit, 3f * sc, ~0, QueryTriggerInteraction.Ignore))
            { slope = Vector3.Angle(hit.normal, Vector3.up); ground = hit.collider.name; }
            float fatal = 12f; var f = typeof(Respawn).GetField("fatalFallHeight", BindingFlags.NonPublic | BindingFlags.Instance); if (f != null) fatal = _god ? _godKeep : (float)f.GetValue(_respawn);
            string drop = _lastDrop < 0 ? "-" : $"{_lastDrop:0.0} / {fatal:0}" + (_lastDrop >= fatal && !_god && _shifter.Size == ShiftSize.Toy ? " <color=#ff6666>RESPAWN</color>" : "");
            string target = _interactor != null && _interactor.Target != null ? ((Component)_interactor.Target).name + ": \"" + _interactor.Target.PromptFor(_interactor) + "\"" : "-";
            var cps = Checkpoints(); int claimed = cps.Count(c => c.Claimed);
            string flags = (_fly ? "<color=#7fd4ff>FLY</color> " : "") + (_god ? "<color=#ffd27f>GOD</color> " : "") + (Time.timeScale != 1f ? $"<color=#ff9f7f>x{Time.timeScale:0.##}</color>" : "");
            string text =
                $"<b>{_fps:0} fps</b>   {Room()}   <b>{_shifter.Size}</b> (scale {sc:0.##})  {flags}\n" +
                $"pos {_player.transform.position:0.0}\n" +
                $"speed {planar:0.0} u/s   vertical {vert:0.0}   {(_motor.Grounded ? "grounded" : "<color=#ffd27f>air</color>")}\n" +
                $"ground {ground}  slope {slope:0}°{(slope > (_cc != null ? _cc.slopeLimit : 45f) ? " <color=#ff6666>TOO STEEP</color>" : "")}\n" +
                $"{Footing()}\n" +
                $"last drop {drop}  {(_lastDropRoom.Length > 0 ? "(" + _lastDropRoom + ")" : "")}\n" +
                $"E: {target}\n" +
                $"checkpoints {claimed}/{cps.Count}";
            GUI.Box(new Rect(10, 10, 470, 186), GUIContent.none, _box);
            GUI.Label(new Rect(18, 14, 460, 182), text, _mono);
            GUI.Label(new Rect(18, Screen.height - 26, 900, 22), "F1 hud   ` console   F5 respawn   F6 fly   F7 god   F8 toy/kid   PgUp/PgDn checkpoints   F12 mark", _monoSmall);
        }

        private void DrawConsole()
        {
            float h = Screen.height * 0.45f;
            GUI.Box(new Rect(0, 0, Screen.width, h), GUIContent.none, _box);
            _scroll = GUI.BeginScrollView(new Rect(8, 6, Screen.width - 16, h - 40), _scroll, new Rect(0, 0, Screen.width - 40, _log.Count * 18f));
            for (int i = 0; i < _log.Count; i++)
            {
                var l = _log[i]; string c = l.type == LogType.Error || l.type == LogType.Exception ? "#ff7f7f" : l.type == LogType.Warning ? "#ffd27f" : l.text.StartsWith("> ") ? "#7fd4ff" : "#dddddd";
                GUI.Label(new Rect(0, i * 18f, Screen.width - 40, 18), $"<color={c}>{Escape(l.text)}</color>", _monoSmall);
            }
            GUI.EndScrollView();
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { if (_input.Trim().Length > 0) { _history.Add(_input); _historyAt = -1; Run(_input); } _input = ""; e.Use(); }
                else if (e.keyCode == KeyCode.UpArrow && _history.Count > 0) { _historyAt = _historyAt < 0 ? _history.Count - 1 : Mathf.Max(0, _historyAt - 1); _input = _history[_historyAt]; e.Use(); }
                else if (e.keyCode == KeyCode.DownArrow && _historyAt >= 0) { _historyAt++; _input = _historyAt < _history.Count ? _history[_historyAt] : ""; if (_historyAt >= _history.Count) _historyAt = -1; e.Use(); }
                else if (e.keyCode == KeyCode.BackQuote || e.character == '`') { e.Use(); }
            }
            GUI.SetNextControlName("DebugConsoleInput");
            _input = GUI.TextField(new Rect(8, h - 30, Screen.width - 16, 24), _input.Replace("`", ""), _mono);
            if (_focusNext) { GUI.FocusControl("DebugConsoleInput"); _focusNext = false; }
        }

        private static string Escape(string s) => s.Replace("<", "‹").Replace(">", "›");
    }
}
