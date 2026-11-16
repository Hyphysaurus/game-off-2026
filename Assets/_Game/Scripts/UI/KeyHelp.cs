using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// H (or the pad's View/Select) shows every key, by situation: on foot, driving, first person, Build Mode, and the dev
    /// keys in the editor and dev builds. The player's actions (move, jump, E...) are read from the project's input
    /// actions, so a rebind shows here; keys that scripts read directly (the F-keys, Build Mode's, the garage's) are
    /// written below and must be kept in step when one changes. Build Mode keeps its own H sheet. A small "H  Keys" hint
    /// shows for a few seconds after each scene loads. Made by code at start, lives across scenes.
    /// </summary>
    public sealed class KeyHelp : MonoBehaviour
    {
        private const string KeyColour = "#8FE3FF", PadColour = "#7F8C99";
        private const float HintSeconds = 6f;

        private GameObject _sheet;
        private TextMeshProUGUI[] _columns;
        private CanvasGroup _hint;
        private float _hintUntil;
        private bool _open;

        /// <summary>The sheet is up (PauseMenu leaves Esc to close it).</summary>
        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<KeyHelp>() != null) return;
            var go = new GameObject("KeyHelp");
            DontDestroyOnLoad(go);
            go.AddComponent<KeyHelp>();
        }

        private void Awake()
        {
            BuildCanvas();
            _hintUntil = Time.unscaledTime + HintSeconds;
        }

        private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }
        private void OnSceneLoaded(Scene s, LoadSceneMode m) { _hintUntil = Time.unscaledTime + HintSeconds; }

        private void Update()
        {
            bool build = GameMode.Current == GameMode.Mode.Build;   // Build Mode's H is its own sheet
            var k = Keyboard.current; var pad = Gamepad.current;
            if (!DebugConsole.IsOpen && !build)
            {
                bool toggle = (k != null && k.hKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);
                if (toggle) SetOpen(!_open);
                else if (_open && k != null && k.escapeKey.wasPressedThisFrame) SetOpen(false);
            }
            if (_open && build) SetOpen(false);
            if (_hint != null) _hint.alpha = _open ? 0f : Mathf.Clamp01((_hintUntil - Time.unscaledTime) / 1.5f);
        }

        private void SetOpen(bool open)
        {
            _open = open; IsOpen = open;
            if (open) Fill();
            _sheet.SetActive(open);
            if (open) _hintUntil = 0f;
        }

        // ---------- the sheet ----------

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("KeyHelpCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;

            var hintRt = BuildUiKit.Rect("Hint", canvasGo.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24f, 20f), new Vector2(220f, 40f));
            BuildUiKit.Panel(hintRt, new Color(0f, 0f, 0f, 0.45f));
            BuildUiKit.Label(hintRt, "<color=" + KeyColour + ">H</color>  Keys", 22);
            _hint = hintRt.gameObject.AddComponent<CanvasGroup>();
            _hint.blocksRaycasts = false; _hint.interactable = false;

            var panel = BuildUiKit.Rect("Sheet", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1840f, 960f));
            BuildUiKit.Panel(panel, BuildUiKit.PanelColor).raycastTarget = false;
            _sheet = panel.gameObject;
            var title = BuildUiKit.Rect("Title", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(-48f, 50f));
            BuildUiKit.Label(title, "<b>KEYS</b>   <size=20><color=" + PadColour + ">pad buttons in grey  ·  H or Esc closes</color></size>", 34, TextAlignmentOptions.MidlineLeft);

            const int count = 5;
            const float left = 24f, top = 84f, gap = 22f;
            float width = (1840f - left * 2f - gap * (count - 1)) / count;
            _columns = new TextMeshProUGUI[count];
            for (int i = 0; i < count; i++)
            {
                var col = BuildUiKit.Rect("Column" + i, panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left + i * (width + gap), -top), new Vector2(width, 960f - top - 24f));
                var t = BuildUiKit.Label(col, "", 17, TextAlignmentOptions.TopLeft);
                t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Truncate;
                t.lineSpacing = 6f;
                _columns[i] = t;
            }
            _sheet.SetActive(false);
        }

        private void Fill()
        {
            var map = PlayerMap();
            string Kb(string action) => Keys(map, action, false);
            string Pad(string action) => Keys(map, action, true);
            var sb = new StringBuilder();

            Header(sb, "ON FOOT");
            Line(sb, Kb("Move"), "Move", Pad("Move"));
            Line(sb, Kb("Look"), "Look around", Pad("Look"));
            Line(sb, Kb("Jump"), "Jump (hold for higher)", Pad("Jump"));
            Line(sb, Kb("Sprint"), "Run", Pad("Sprint"));
            Line(sb, Kb("Interact"), "Use, pick up, get in, shift at a core", Pad("Interact"));
            Line(sb, Kb("Shift"), "Shift anywhere: uses the charge, cores refill it", Pad("Shift"));
            Line(sb, Kb("Attack"), "Use what you hold, fire", Pad("Attack"));
            Line(sb, Kb("Crouch"), "Put down what you hold", Pad("Crouch"));
            sb.Append('\n');
            Header(sb, "GRAB (aim at a prop)");
            Line(sb, Kb("Interact"), "Grab; again to set it down where the footprint shows", Pad("Interact"));
            Line(sb, Kb("Attack"), "Throw it", Pad("Attack"));
            Line(sb, "Mouse wheel", "Nearer / farther");
            Line(sb, "R or Right click + mouse", "Turn it", "LB");
            sb.Append('\n');
            Line(sb, Kb("Previous") + " / " + Kb("Next"), "Previous / next weapon", Pad("Previous") + " / " + Pad("Next"));
            Line(sb, "Q", "Drop the weapon", "D-pad down");
            Line(sb, "Esc", "Pause: resume, options, back to the title, quit", "Start");
            Line(sb, "R", "Restart, on the end card");
            Line(sb, "H", "This list", "View");
            Set(0, sb);

            Header(sb, "DRIVING");
            Line(sb, Kb("Move"), "Drive and steer", Pad("Move"));
            Line(sb, Kb("Jump"), "Handbrake and drift (Cyber cars), hop (wooden car)", Pad("Jump"));
            Line(sb, Kb("Sprint"), "Nitro: drifts, air time, stars and boost pads fill it", Pad("Sprint"));
            Line(sb, "Hold " + Kb("Jump") + " in a slide", "Charge a drift boost (two pips); let go to fire it", "Hold " + Pad("Jump"));
            Line(sb, "In the air: " + Kb("Move"), "Tip and turn the car; with " + Kb("Jump") + " held, roll it", Pad("Move"));
            Line(sb, "Stopped: W, then hold " + Kb("Jump"), "Wind up; let go of " + Kb("Jump") + " to launch", "Up, then hold " + Pad("Jump"));
            Line(sb, Kb("Interact"), "Get out", Pad("Interact"));
            Line(sb, Kb("Look"), "Look around; the camera swings back behind", Pad("Look"));
            sb.Append('\n');
            Header(sb, "RACE");
            Line(sb, "R (Raceway, in a car)", "Race the rivals: line up on the grid, 3-2-1-GO", "LB");
            Line(sb, "Drive through the finish", "Start a 3-lap race alone; gates count in number order");
            Line(sb, Kb("Interact"), "Getting out ends the race", Pad("Interact"));
            sb.Append('\n');
            Header(sb, "GARAGE (roll in slowly)");
            Line(sb, "Enter", "Drive out", "Start");
            Line(sb, Kb("Interact"), "Get out", Pad("Interact"));
            Line(sb, "Click < >", "Change body, wheels, parts");
            Line(sb, "Right drag, A / D", "Look around the car (W / S tilt)", "Left stick");
            Line(sb, "Mouse wheel", "Zoom (off the menu)");
            Set(1, sb);

            Header(sb, "FIRST PERSON (Workshop)");
            Line(sb, "F4 or Esc", "Leave first person");
            Line(sb, Kb("Interact"), "Grab a prop, use, take a weapon", Pad("Interact"));
            Line(sb, Kb("Attack"), "Throw, fire, swing", Pad("Attack"));
            Line(sb, "R or Right click + mouse", "Turn what you hold", "LB");
            Line(sb, "Mouse wheel", "Turn what you hold");
            Line(sb, Kb("Previous") + " / " + Kb("Next"), "Previous / next weapon", Pad("Previous") + " / " + Pad("Next"));
            Line(sb, "Q", "Drop the weapon", "D-pad down");
            Line(sb, Kb("Crouch"), "Crouch", Pad("Crouch"));
            Line(sb, Kb("Jump") + ", " + Kb("Sprint"), "Jump, run", Pad("Jump") + ", " + Pad("Sprint"));
            Set(2, sb);

            Header(sb, "BUILD MODE (Workshop)");
            Line(sb, "F3 or Esc", "Leave Build Mode");
            Line(sb, "H", "Build Mode's own key sheet");
            Line(sb, "WASD / Arrows", "Pan the camera");
            Line(sb, "Q / E", "Turn the camera");
            Line(sb, "Mouse wheel", "Zoom");
            Line(sb, "Middle or right drag", "Orbit the camera");
            Line(sb, "Click", "Place, select (drag to move, drag empty floor to box-select)");
            Line(sb, "R  (Shift+R)", "Turn (back)");
            Line(sb, "[  /  ]", "Lower / raise");
            Line(sb, "-  /  +", "Smaller / bigger");
            Line(sb, "X / Z held", "Drag along one axis");
            Line(sb, "Ctrl+D", "Copy");
            Line(sb, "Delete", "Delete");
            Line(sb, "Ctrl+Z / Ctrl+Y", "Undo / redo");
            Line(sb, "F", "Frame the selection");
            Line(sb, "G / O / Tab", "Snap, allow overlap, hide the panels");
            Line(sb, "1-8, C", "Shape size and steps, shape colour");
            Set(3, sb);

            if (Application.isEditor || Debug.isDebugBuild)
            {
                Header(sb, "DEV (editor and dev builds)");
                Line(sb, "`", "Console (type help)");
                Line(sb, "F1", "Debug readout on / off");
                Line(sb, "F2", "Pocket Core: call a shift core to you");
                Line(sb, "F5", "Respawn at the checkpoint");
                Line(sb, "F6", "Fly: WASD, Space up, C down, Shift fast");
                Line(sb, "F7", "God mode");
                Line(sb, "F8", "Shift size anywhere");
                Line(sb, "F9", "Reset the Workshop (targets, soldier, cars)");
                Line(sb, "F12", "Mark a playtest note here");
                Line(sb, "PgDn / PgUp", "Next / previous checkpoint");
            }
            Set(4, sb);
        }

        private void Set(int column, StringBuilder sb) { _columns[column].text = sb.ToString(); sb.Clear(); }

        private static void Header(StringBuilder sb, string text) => sb.Append("<b><size=21>").Append(text).Append("</size></b>\n");

        private static void Line(StringBuilder sb, string keys, string what, string pad = null)
        {
            if (string.IsNullOrEmpty(keys)) keys = "-";
            sb.Append("<color=").Append(KeyColour).Append('>').Append(keys).Append("</color>  ").Append(what);
            if (!string.IsNullOrEmpty(pad) && pad.Trim('/', ',', ' ').Length > 0) sb.Append("  <color=").Append(PadColour).Append('>').Append(pad).Append("</color>");
            sb.Append('\n');
        }

        // ---------- reading the bindings ----------

        private static InputActionMap PlayerMap()
        {
            var asset = InputSystem.actions;   // the project-wide actions (InputSystem_Actions), the ones PlayerInput copies
            return asset != null ? asset.FindActionMap("Player") : null;
        }

        /// <summary>The keyboard and mouse (or pad) keys bound to an action, as the Input System names them.</summary>
        private static string Keys(InputActionMap map, string actionName, bool pad)
        {
            var action = map != null ? map.FindAction(actionName) : null;
            if (action == null) return "";
            var found = new List<string>();
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isPartOfComposite) continue;
                string path = b.isComposite ? (i + 1 < bindings.Count ? bindings[i + 1].effectivePath : "") : b.effectivePath;
                if (string.IsNullOrEmpty(path)) continue;
                bool isPad = path.StartsWith("<Gamepad>");
                bool isKeys = path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>") || path.StartsWith("<Pointer>");
                if (pad ? !isPad : !isKeys) continue;
                string shown = path.EndsWith("/delta") ? "Mouse" : Tidy(action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions));
                if (!string.IsNullOrEmpty(shown) && !found.Contains(shown)) found.Add(shown);
            }
            return string.Join(" or ", found);
        }

        private static string Tidy(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.Contains("Up Arrow") || s.Contains("Up/Left/Down/Right")) return "Arrows";
            return s.Replace("LMB", "Left click").Replace("RMB", "Right click").Replace("Left Button", "Left click").Replace("Right Button", "Right click");
        }
    }
}
