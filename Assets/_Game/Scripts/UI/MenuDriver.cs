using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// The hands on a MenuModel (D-0197): the mouse, the keys and the pad choose among the controls on show. The mouse
    /// over a control selects it; up and down (arrows, W S, the pad's stick or cross) move the selection past what
    /// cannot be used; a press that pulls away stretches the slab after the pointer and lets it fly home (play, not a
    /// choice); the pointer brushing over a slab stirs it; a click on the selected control, Enter, Space or the pad's south button chooses it; Esc,
    /// Backspace or the pad's east button goes back; Shift, F or the pad's north button shifts the model between flat
    /// and deep. It only says what was chosen: its owner (TitleMenu) knows what that means. It reads the devices
    /// directly, on real time: the player's own input is off while a menu is up. Switch it off while another panel has
    /// the screen, and set Typing while a text field has the keys.
    /// </summary>
    public sealed class MenuDriver : MonoBehaviour
    {
        [SerializeField, Min(0.05f), Tooltip("Seconds a held direction waits before it repeats.")] private float repeatAfter = 0.38f;
        [SerializeField, Min(0.02f), Tooltip("Seconds between repeats after that.")] private float repeatEvery = 0.12f;
        [SerializeField, Range(0.1f, 0.9f), Tooltip("How far the stick must be pushed to count as a direction.")] private float stickPush = 0.5f;
        [SerializeField, Min(0.001f), Tooltip("A press that moves this far on the model, metres, becomes a pull: the slab stretches after the pointer, and letting go does not choose it.")] private float pullAfter = 0.025f;
        [SerializeField, Min(0f), Tooltip("The pointer must cross this share of the screen a second to stir the slab it brushes over.")] private float brushFrom = 0.6f;

        private MenuModel _model;
        private Vector2 _lastMouse = new Vector2(float.NaN, float.NaN);
        private int _held; private float _nextRepeat;
        private MenuModel.Control _pressedOn;
        private Vector2 _pressedAt;     // where on the model's plane the press began, metres
        private bool _pulling;          // the press became a pull: letting go does not choose

        /// <summary>A control was chosen: its name.</summary>
        public event Action<string> Chosen;
        /// <summary>Back was asked for.</summary>
        public event Action Back;
        /// <summary>The selection moved to another control.</summary>
        public event Action Moved;
        /// <summary>A text field has the keys: letters, Shift and Backspace are its, not the menu's.</summary>
        public bool Typing { get; set; }

        public void Bind(MenuModel model) { _model = model; }

        private void OnDisable()
        {
            _held = 0;
            if (_pressedOn != null) { _pressedOn.Pressed = false; if (_pulling && _model != null) _model.Release(_pressedOn); _pressedOn = null; }
            _pulling = false;
        }

        private void Update()
        {
            if (_model == null) return;
            Mouse mouse = Mouse.current; Keyboard keys = Keyboard.current; Gamepad pad = Gamepad.current;

            // The mouse: where it is leans the deep view; moving it onto a control selects that control.
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                _model.Pointer = new Vector2(Mathf.Clamp(at.x / Mathf.Max(1f, Screen.width) * 2f - 1f, -1f, 1f), Mathf.Clamp(at.y / Mathf.Max(1f, Screen.height) * 2f - 1f, -1f, 1f));
                MenuModel.Control under = _model.Pick(at);
                bool first = float.IsNaN(_lastMouse.x);
                bool moved = first || (at - _lastMouse).sqrMagnitude > 1f;
                Vector2 speed = first || Time.unscaledDeltaTime <= 0f ? Vector2.zero : (at - _lastMouse) / (Mathf.Max(1f, Screen.height) * Time.unscaledDeltaTime);
                _lastMouse = at;
                if (moved && !_pulling && under != null && under.Usable && under != _model.Selected) { _model.Selected = under; Moved?.Invoke(); }
                if (!_pulling && under != null && speed.magnitude >= brushFrom) _model.Brush(under, speed);   // the pointer brushes it in passing
                if (mouse.leftButton.wasPressedThisFrame && under != null)
                {
                    if (under.Usable) _model.Selected = under;
                    under.Pressed = true; _pressedOn = under; _pressedAt = _model.OnPlane(at); _pulling = false;
                }
                // A press that pulls away stretches the slab after the pointer (D-0199): play, not a choice.
                if (_pressedOn != null && mouse.leftButton.isPressed)
                {
                    Vector2 pull = _model.OnPlane(at) - _pressedAt;
                    if (!_pulling && pull.magnitude >= pullAfter) { _pulling = true; _pressedOn.Pressed = false; }
                    if (_pulling) _model.Hold(_pressedOn, pull);
                }
                if (mouse.leftButton.wasReleasedThisFrame && _pressedOn != null)
                {
                    MenuModel.Control was = _pressedOn; was.Pressed = false; _pressedOn = null;
                    if (_pulling) { _pulling = false; _model.Release(was); }
                    else if (under == was && was.Usable) { _model.Knock(was, _model.OnPlane(at) - _model.Middle(was), 0.6f); Chosen?.Invoke(was.Name); return; }
                }
            }

            // Up and down, with a repeat while held.
            int way = 0;
            if (!Typing && keys != null) way += (keys.downArrowKey.isPressed || keys.sKey.isPressed ? 1 : 0) - (keys.upArrowKey.isPressed || keys.wKey.isPressed ? 1 : 0);
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue(), cross = pad.dpad.ReadValue();
                way += (stick.y < -stickPush || cross.y < -0.5f ? 1 : 0) - (stick.y > stickPush || cross.y > 0.5f ? 1 : 0);
            }
            way = Mathf.Clamp(way, -1, 1);
            if (way == 0) _held = 0;
            else if (way != _held) { _held = way; _nextRepeat = Time.unscaledTime + repeatAfter; Step(way); }
            else if (Time.unscaledTime >= _nextRepeat) { _nextRepeat = Time.unscaledTime + repeatEvery; Step(way); }

            bool choose = (pad != null && pad.buttonSouth.wasPressedThisFrame) || (!Typing && keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame));
            bool back = (pad != null && pad.buttonEast.wasPressedThisFrame) || (keys != null && keys.escapeKey.wasPressedThisFrame) || (!Typing && keys != null && keys.backspaceKey.wasPressedThisFrame);
            bool shift = (pad != null && pad.buttonNorth.wasPressedThisFrame) || (!Typing && keys != null && (keys.leftShiftKey.wasPressedThisFrame || keys.rightShiftKey.wasPressedThisFrame || keys.fKey.wasPressedThisFrame));
            if (shift) _model.Deep = !_model.Deep;
            if (choose && _model.Selected != null && _model.Selected.Usable) { _model.Knock(_model.Selected, Vector2.zero, 0.6f); Chosen?.Invoke(_model.Selected.Name); return; }
            if (back) Back?.Invoke();
        }

        /// <summary>The selection, one usable control down (+1) or up (-1), round the ends.</summary>
        private void Step(int way)
        {
            var shown = _model.Shown;
            if (shown.Count == 0) return;
            int at = -1;
            for (int i = 0; i < shown.Count; i++) if (shown[i] == _model.Selected) { at = i; break; }
            for (int tries = 0; tries < shown.Count; tries++)
            {
                at = ((at + way) % shown.Count + shown.Count) % shown.Count;
                if (shown[at].Usable) { if (shown[at] != _model.Selected) { _model.Selected = shown[at]; Moved?.Invoke(); } return; }
            }
        }
    }
}
