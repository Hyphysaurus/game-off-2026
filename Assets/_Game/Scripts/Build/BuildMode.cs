using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// Owner of GameMode.Build (the Workshop's Two Point Museum / Planet Zoo style editor). F3 toggles it; Escape steps
    /// back (close window → cancel placing → clear selection → leave). While on, the player is paused (input, motor,
    /// shoulder camera and size key off; physics keeps running so placed props settle), the cursor is free, and the
    /// main camera is driven by BuildCamera. Leaving gives everything back as it was.
    /// Put one on any object in a scene with a Player. The work is split: BuildWorld (spawning, layouts), BuildPlacer
    /// (ghost), BuildSelection (editing), BuildHistory (undo), BuildUI (screen).
    /// </summary>
    public sealed class BuildMode : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional. Loaded from Resources/BuildCatalog when empty.")]
        private BuildCatalog catalog;
        [SerializeField, Tooltip("Optional. Added to the main camera when empty.")]
        private BuildCamera buildCamera;
        [SerializeField, Tooltip("Every child of these (with a renderer and a collider) becomes editable, so the room's own props can be moved too.")]
        private Transform[] buildableRoots = new Transform[0];
        [SerializeField, Tooltip("Hidden while building, shown again after: a ceiling, say, that would block the view from above.")]
        private GameObject[] hideWhileBuilding = new GameObject[0];
        [SerializeField, Tooltip("Open in Build Mode as soon as the scene starts.")]
        private bool startInBuild;
        [SerializeField, Tooltip("Key that toggles Build Mode.")]
        private Key toggleKey = Key.F3;
        [SerializeField, Tooltip("Build camera distance when opening, in player heights (so a toy and a kid both see the room around them).")]
        private float openDistance = 18f;

        private Transform _player;
        private PlayerInput _input; private PlayerMotor _motor; private Shifter _shifter; private ShoulderCamera _shoulder;
        private bool _inputWas, _motorWas, _shifterWas, _shoulderWas;
        private bool[] _hiddenWas = new bool[0];
        private bool _on;

        public BuildOptions Options { get; } = new BuildOptions();
        public BuildWorld World { get; private set; }
        public BuildHistory History { get; private set; }
        public BuildSelection Selection { get; private set; }
        public BuildPlacer Placer { get; private set; }
        public BuildCamera Camera => buildCamera;
        public bool IsOn => _on;

        private BuildOutlines _outlines;
        private BuildUI _ui;

        private void Awake()
        {
            var playerGo = GameObject.Find("Player");
            if (playerGo != null)
            {
                _player = playerGo.transform;
                _input = playerGo.GetComponent<PlayerInput>();
                _motor = playerGo.GetComponent<PlayerMotor>();
                _shifter = playerGo.GetComponent<Shifter>();
            }
            _shoulder = FindFirstObjectByType<ShoulderCamera>();
            if (buildCamera == null)
            {
                var cam = _shoulder != null ? _shoulder.GetComponent<UnityEngine.Camera>() : UnityEngine.Camera.main;
                if (cam != null && !cam.TryGetComponent(out buildCamera)) buildCamera = cam.gameObject.AddComponent<BuildCamera>();
            }
            if (buildCamera != null) buildCamera.enabled = false;
            if (catalog == null) catalog = BuildCatalog.Load();
            if (catalog == null) Debug.LogWarning("BuildMode: no Resources/BuildCatalog yet. Run Shift Jam > Workshop > Build Catalog in the editor.");
        }

        private void Start()
        {
            if (buildCamera == null) { Debug.LogError("BuildMode: no camera to drive. Build Mode is off in this scene."); enabled = false; return; }
            MarkRoots();
            World = new BuildWorld(catalog, _player);
            History = new BuildHistory();
            _outlines = new BuildOutlines(catalog != null ? catalog.GhostMaterial : null);
            _outlines.SetVisible(false);
            Selection = new BuildSelection(World, History, buildCamera, Options, _outlines);
            Placer = new BuildPlacer(World, History, Selection, buildCamera, Options);
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            _ui = new BuildUI(this, catalog);
            if (startInBuild) GameMode.Set(GameMode.Mode.Build);
            if (GameMode.Current == GameMode.Mode.Build && !_on) Enter();
        }

        private void OnEnable() { GameMode.Changed += OnModeChanged; }

        private void OnDisable()
        {
            GameMode.Changed -= OnModeChanged;
            if (_on) Exit();
        }

        private void OnDestroy()
        {
            _ui?.Destroy();
            _outlines?.Destroy();
            if (GameMode.Current == GameMode.Mode.Build) GameMode.ResetQuietly();
        }

        private void OnModeChanged(GameMode.Mode from, GameMode.Mode to)
        {
            if (World == null) return;
            // from first person, wait a frame: its own exit has to hand the player back before we pause it, or we would
            // record the player as already off and first person would switch the gameplay camera on under ours
            if (to == GameMode.Mode.Build && !_on) { if (from == GameMode.Mode.FirstPerson) _enterNextFrame = true; else Enter(); }
            else if (from == GameMode.Mode.Build && _on) Exit();
        }

        /// <summary>Gives every child of the buildable roots a Buildable, if it has something to see and something to hit.</summary>
        private void MarkRoots()
        {
            foreach (var root in buildableRoots)
            {
                if (root == null) continue;
                for (int i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i).gameObject;
                    if (child.GetComponentInChildren<Renderer>() == null || child.GetComponentInChildren<Collider>() == null) continue;
                    if (!child.TryGetComponent(out Buildable _)) child.AddComponent<Buildable>();
                }
            }
        }

        // ------------------------------------------------------------------ on / off

        private void Enter()
        {
            _on = true;
            _inputWas = Pause(_input); _motorWas = Pause(_motor); _shifterWas = Pause(_shifter); _shoulderWas = Pause(_shoulder);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            _hiddenWas = new bool[hideWhileBuilding.Length];
            for (int i = 0; i < hideWhileBuilding.Length; i++)
            {
                if (hideWhileBuilding[i] == null) continue;
                _hiddenWas[i] = hideWhileBuilding[i].activeSelf;
                hideWhileBuilding[i].SetActive(false);
            }

            float scale = _shifter != null ? _shifter.Scale : 1f;
            Vector3 focus = _player != null ? _player.position : buildCamera.transform.position + buildCamera.transform.forward * 20f;
            float yaw = _shoulder != null ? _shoulder.Yaw : buildCamera.transform.eulerAngles.y;
            buildCamera.Begin(focus, yaw, openDistance * 1.8f * scale);
            _outlines.SetVisible(true);
            _ui.SetVisible(true);
        }

        private void Exit()
        {
            _on = false;
            Selection.Interrupt();
            Placer.Cancel();
            Selection.Clear();
            _outlines.SetVisible(false);
            _ui.SetVisible(false);
            buildCamera.End();
            for (int i = 0; i < hideWhileBuilding.Length && i < _hiddenWas.Length; i++) if (hideWhileBuilding[i] != null) hideWhileBuilding[i].SetActive(_hiddenWas[i]);
            Resume(_input, _inputWas); Resume(_motor, _motorWas); Resume(_shifter, _shifterWas); Resume(_shoulder, _shoulderWas);
            // The cursor stays free: the shoulder camera takes it back on the next click in the game view.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private static bool Pause(Behaviour b)
        {
            if (b == null) return false;
            bool was = b.enabled; b.enabled = false; return was;
        }

        private static void Resume(Behaviour b, bool was) { if (b != null) b.enabled = was; }

        // ------------------------------------------------------------------ per frame

        private bool _enterNextFrame;

        private void Update()
        {
            if (_enterNextFrame) { _enterNextFrame = false; if (GameMode.Current == GameMode.Mode.Build && !_on && World != null) Enter(); }
            if (World == null) return;
            bool typing = BuildInput.Typing;
            // F3 (not while a menu has the game paused, not while typing)
            if (!typing && BuildInput.Pressed(toggleKey) && Time.timeScale > 0f)
            {
                GameMode.Set(GameMode.Current == GameMode.Mode.Build ? GameMode.Mode.Play : GameMode.Mode.Build);
                return;
            }
            if (!_on) return;
            // Something else (the debug console closing, say) may switch the player back on: keep it paused.
            if (_input != null && _input.enabled) _input.enabled = false;
            if (_motor != null && _motor.enabled) _motor.enabled = false;
            if (_shoulder != null && _shoulder.enabled) _shoulder.enabled = false;

            bool overUI = BuildInput.OverUI;
            buildCamera.KeysBlocked = typing;
            buildCamera.WheelBlocked = overUI || BuildInput.Ctrl || BuildInput.Alt;

            if (!typing)
            {
                if (BuildInput.Pressed(Key.Escape)) { Back(); return; }
                if (BuildInput.Ctrl && BuildInput.Pressed(Key.Z) && !Selection.Busy) { Undo(); }
                else if (BuildInput.Ctrl && BuildInput.Pressed(Key.Y) && !Selection.Busy) { Redo(); }
                if (BuildInput.Pressed(Key.G)) ToggleSnap();
                if (BuildInput.Pressed(Key.O)) ToggleOverlap();
                if (BuildInput.Pressed(Key.H)) _ui.ToggleSheet();
                if (BuildInput.Pressed(Key.Tab)) _ui.ToggleHidden();
            }

            if (Placer.Active)
            {
                Selection.ClearHover();
                if (buildCamera.RightClicked) Placer.Cancel();
                else if (!typing) Placer.Tick();
            }
            else if (!typing) Selection.Tick();
            else Selection.Prune();

            _ui.Refresh(Status());
        }

        private void LateUpdate()
        {
            if (_on && Selection != null) Selection.DrawOutlines();
        }

        private void Back()
        {
            if (_ui.LayoutsOpen) { _ui.CloseLayouts(); return; }
            if (Placer.Active) { Placer.Cancel(); return; }
            if (Selection.Items.Count > 0) { Selection.Interrupt(); Selection.Clear(); return; }
            GameMode.Set(GameMode.Mode.Play);
        }

        private string Status()
        {
            if (Placer.Active)
            {
                string fit = Placer.Valid ? "<color=#7f7>fits</color>" : Options.allowOverlap ? "<color=#fb5>overlaps (allowed)</color>" : "<color=#f77>blocked</color>";
                return $"Placing <b>{Placer.Entry.displayName}</b> · {fit} · grid {(Options.snap ? Placer.Grid.ToString("0.#") : "off")} · turn {Mathf.Repeat(Placer.Yaw, 360f):0}° · raise {Placer.Raise:0.##}   <alpha=#99>click place · Shift+click keep placing · right-click cancel";
            }
            int n = Selection.Items.Count;
            if (n == 1 && Selection.Items[0] != null) return $"<b>{Selection.Items[0].name}</b> selected   <alpha=#99>drag move · R turn · + − scale · [ ] raise · Ctrl+D copy · Del delete · F frame · H all keys";
            if (n > 1) return $"<b>{n}</b> selected   <alpha=#99>drag move · R turn · + − scale · [ ] raise · Ctrl+D copy · Del delete · F frame";
            return "<alpha=#bb>Pick something from the catalogue, or click a thing in the room to edit it · H shows every key · F3 / Esc back to play";
        }

        // ------------------------------------------------------------------ commands (UI and keys)

        public void PickEntry(BuildCatalog.Entry entry)
        {
            if (Placer.Active && Placer.Entry == entry) { Placer.Cancel(); return; }
            Selection.Interrupt();
            Placer.Begin(entry);
        }

        public void Undo() { Placer.Cancel(); History.Undo(); Selection.Prune(); }
        public void Redo() { Placer.Cancel(); History.Redo(); Selection.Prune(); }
        public void ToggleSnap() => Options.snap = !Options.snap;
        public void ToggleOverlap() => Options.allowOverlap = !Options.allowOverlap;

        public string SaveLayout(string name)
        {
            var file = World.Capture();
            string path = BuildLayouts.Save(name, file);
            return $"Saved '{BuildLayouts.Clean(name)}' ({file.items.Count} things) to {path}";
        }

        public string LoadLayout(string name)
        {
            if (!BuildLayouts.TryLoad(name, out var file)) return $"Could not read '{name}'.";
            Placer.Cancel(); Selection.Interrupt(); Selection.Clear();
            int missing = World.Apply(file);
            History.Clear();
            string note = file.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ? $" (made in scene '{file.scene}')" : "";
            return missing == 0 ? $"Loaded '{name}'{note}." : $"Loaded '{name}'{note}; {missing} thing(s) not found (catalogue or scene changed).";
        }

        public string StartEmpty()
        {
            Placer.Cancel(); Selection.Interrupt(); Selection.Clear();
            World.Reset();
            History.Clear();
            return "Room reset: everything placed is gone, the room's own props are back where they started.";
        }

        /// <summary>
        /// Test the room: become the given size where the camera is looking and play (or go first person).
        /// First person is entered through Play, so each mode owner starts from the normal on-foot state.
        /// </summary>
        public void TestAs(ShiftSize size, bool firstPerson)
        {
            if (_player == null || _motor == null) return;
            Vector3 spot = buildCamera.Focus;
            var cam = buildCamera.Camera;
            if (World.Surface(cam, new Vector2(Screen.width / 2f, Screen.height / 2f), null, buildCamera.Focus.y, out var point, out var normal) && normal.y > 0.5f) spot = point;
            if (_shifter != null) _shifter.SetSizeNow(size);
            float yaw = buildCamera.Yaw;
            _motor.Warp(spot + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            GameMode.Set(GameMode.Mode.Play);   // Exit restores the shoulder camera first, so the warp below sticks
            if (_shoulder != null) _shoulder.Warp(yaw, 15f);
            if (firstPerson) GameMode.Set(GameMode.Mode.FirstPerson);
        }
    }
}
