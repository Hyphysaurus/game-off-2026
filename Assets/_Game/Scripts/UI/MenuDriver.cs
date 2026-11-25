using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// The hands on a MenuModel (D-0197): the mouse, the keys and the pad choose among the controls on show. The mouse
    /// over a control selects it; up and down (arrows, W S, the pad's stick or cross) move the selection past what
    /// cannot be used; with the model's guns (MenuDarts) a click fires a dart and the dart chooses when it lands, G, the
    /// right mouse button or the pad's west button swaps guns, and the automatic one sprays while held and chooses
    /// nothing; a press that pulls away stretches the slab after the pointer and lets it fly home (play, not a
    /// choice); the pointer brushing over a slab stirs it; a click on the selected control, Enter, Space or the pad's south button chooses it; Esc,
    /// Backspace or the pad's east button goes back; Shift, F or the pad's north button shifts the model between flat
    /// and deep; T or the pad's D-pad up loads the guns' next kind of ammo (D-0201: darts, paint balls), as in the game,
    /// and while there is more than one kind the D-pad no longer moves the selection up (the stick still does). It only says what was chosen: its owner (TitleMenu) knows what that means. It reads the devices
    /// directly, on real time: the player's own input is off while a menu is up. Switch it off while another panel has
    /// the screen, and set Typing while a text field has the keys.
    /// Values (D-0204, the Options page): on a control that steps (MenuModel.SetSteps), left and right (arrows, A D, the
    /// stick or the D-pad across) step it down and up, with a repeat while held, and a click or the choosing dart on its
    /// left or right half steps it down or up (Enter or pad A: up); each says Stepped, not Chosen. On a control that does
    /// not step, left and right go to the control beside it on its row, if there is one (the Parties page's pairs).
    /// Owner Takes Esc: the owner reads Esc and the pad's B itself (the pause menu does, earlier in the frame than this),
    /// so one press is never two backs; Backspace is still this part's.
    /// </summary>
    public sealed class MenuDriver : MonoBehaviour
    {
        [SerializeField, Min(0.05f), Tooltip("Seconds a held direction waits before it repeats.")] private float repeatAfter = 0.38f;
        [SerializeField, Min(0.02f), Tooltip("Seconds between repeats after that.")] private float repeatEvery = 0.12f;
        [SerializeField, Range(0.1f, 0.9f), Tooltip("How far the stick must be pushed to count as a direction.")] private float stickPush = 0.5f;
        [SerializeField, Min(0.001f), Tooltip("A press that moves this far on the model, metres, becomes a pull: the slab stretches after the pointer, and letting go does not choose it.")] private float pullAfter = 0.025f;
        [SerializeField, Min(0f), Tooltip("The pointer must cross this share of the screen a second to stir the slab it brushes over.")] private float brushFrom = 0.6f;

        private MenuModel _model;
        private MenuDarts _darts;       // the model's toy guns; null or gunless = a choice is made the plain way
        private Vector2 _lastMouse = new Vector2(float.NaN, float.NaN);
        private int _held; private float _nextRepeat;
        private MenuModel.Control _pressedOn;
        private Vector2 _pressedAt;     // where on the model's plane the press began, metres
        private bool _pulling;          // the press became a pull: letting go does not choose
        private int _heldSide; private float _nextSide;    // left or right held, and when it repeats

        /// <summary>A control was chosen: its name.</summary>
        public event Action<string> Chosen;
        /// <summary>Back was asked for.</summary>
        public event Action Back;
        /// <summary>The selection moved to another control.</summary>
        public event Action Moved;
        /// <summary>A value was stepped (D-0204): its name, and -1 down or +1 up.</summary>
        public event Action<string, int> Stepped;
        /// <summary>The owner reads Esc and the pad's B itself (the pause menu): this part takes neither as Back.</summary>
        public bool OwnerTakesEsc { get; set; }
        /// <summary>A text field has the keys: letters, Shift and Backspace are its, not the menu's.</summary>
        public bool Typing { get; set; }

        public void Bind(MenuModel model) { _model = model; _darts = model != null ? model.GetComponent<MenuDarts>() : null; }

        /// <summary>The model's guns, if it has any that can fire.</summary>
        public MenuDarts Darts => _darts != null && _darts.HasGun ? _darts : null;

        /// <summary>Chooses a control: with a gun whose dart chooses, the dart is fired at it and the choice is made when it
        /// lands; else at once, with a knock.</summary>
        private void Choose(MenuModel.Control c, Vector2 at)
        {
            MenuDarts gun = Darts;
            if (gun != null && gun.Choosing) return;                 // a choice is already in the air
            int way = c.Steps ? (at.x < _model.Middle(c).x ? -1 : 1) : 0;   // a value: its left half steps it down, its right half (and its middle: Enter) up
            if (gun != null) { gun.Aim = at; gun.AimOn = c; }        // the gun turns to what is chosen (keys and the pad have no pointer)
            if (gun == null || !gun.Fire(at, c, true, () => { if (this != null && isActiveAndEnabled && c.Shown && c.Usable) Pick(c, way); }))
            {
                _model.Knock(c, at - _model.Middle(c), 0.6f);
                Pick(c, way);
            }
        }

        /// <summary>Says what a choice was: a step of a value (way -1 or +1), or a control chosen (way 0).</summary>
        private void Pick(MenuModel.Control c, int way)
        {
            if (way != 0) Stepped?.Invoke(c.Name, way);
            else Chosen?.Invoke(c.Name);
        }


        private void OnDisable()
        {
            _held = 0; _heldSide = 0;
            if (_darts != null) _darts.Trigger = false;
            if (_pressedOn != null) { _pressedOn.Pressed = false; if (_pulling && _model != null) _model.Release(_pressedOn); _pressedOn = null; }
            _pulling = false;
        }

        private void Update()
        {
            if (_model == null) return;
            Mouse mouse = Mouse.current; Keyboard keys = Keyboard.current; Gamepad pad = Gamepad.current;
            if (_darts != null) _darts.Trigger = false;     // held again below, by the mouse or the pad

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
                MenuDarts gun = Darts;
                Vector2 onPlane = _model.OnPlane(at);
                bool spray = gun != null && gun.Auto;       // the LMG: holding the button sprays, nothing is grabbed or chosen
                if (gun != null)
                {
                    if (moved) { gun.Aim = onPlane; gun.AimOn = under; }
                    if (spray && mouse.leftButton.isPressed) gun.Trigger = true;
                    if (mouse.rightButton.wasPressedThisFrame) gun.Swap();
                    // A click on nothing is a dart in the wall, at once.
                    if (!spray && mouse.leftButton.wasPressedThisFrame && under == null) { gun.Aim = onPlane; gun.AimOn = null; gun.Fire(onPlane, null, false, null); }
                }
                if (!spray && mouse.leftButton.wasPressedThisFrame && under != null)
                {
                    if (under.Usable) _model.Selected = under;
                    under.Pressed = true; _pressedOn = under; _pressedAt = onPlane; _pulling = false;
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
                    else if (under == was && was.Usable) { Choose(was, onPlane); return; }
                    else if (under == was && gun != null) { gun.Aim = onPlane; gun.AimOn = was; gun.Fire(onPlane, was, false, null); }   // a slab that cannot be used, or a plaything, still takes a dart
                    else if (under == was) _model.Knock(was, onPlane - _model.Middle(was), 0.6f);   // with no gun a tap still knocks it
                }
            }

            // Up and down, with a repeat while held.
            int way = 0;
            if (!Typing && keys != null) way += (keys.downArrowKey.isPressed || keys.sKey.isPressed ? 1 : 0) - (keys.upArrowKey.isPressed || keys.wKey.isPressed ? 1 : 0);
            bool padLoads = Darts != null && Darts.CanReload;      // the D-pad's up loads the next ammo, as in the game
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue(), cross = pad.dpad.ReadValue();
                way += (stick.y < -stickPush || cross.y < -0.5f ? 1 : 0) - (stick.y > stickPush || (!padLoads && cross.y > 0.5f) ? 1 : 0);
            }
            way = Mathf.Clamp(way, -1, 1);
            if (way == 0) _held = 0;
            else if (way != _held) { _held = way; _nextRepeat = Time.unscaledTime + repeatAfter; Step(way); }
            else if (Time.unscaledTime >= _nextRepeat) { _nextRepeat = Time.unscaledTime + repeatEvery; Step(way); }

            // Left and right, with a repeat while held: a value steps, anything else hands the selection along its row.
            int side = 0;
            if (!Typing && keys != null) side += (keys.rightArrowKey.isPressed || keys.dKey.isPressed ? 1 : 0) - (keys.leftArrowKey.isPressed || keys.aKey.isPressed ? 1 : 0);
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue(), cross = pad.dpad.ReadValue();
                bool across = Mathf.Abs(stick.x) > Mathf.Abs(stick.y);     // a stick pushed mostly up or down is not also a step
                side += ((across && stick.x > stickPush) || cross.x > 0.5f ? 1 : 0) - ((across && stick.x < -stickPush) || cross.x < -0.5f ? 1 : 0);
            }
            side = Mathf.Clamp(side, -1, 1);
            if (side == 0) _heldSide = 0;
            else if (side != _heldSide) { _heldSide = side; _nextSide = Time.unscaledTime + repeatAfter; Side(side); }
            else if (Time.unscaledTime >= _nextSide) { _nextSide = Time.unscaledTime + repeatEvery; Side(side); }

            bool choose = (pad != null && pad.buttonSouth.wasPressedThisFrame) || (!Typing && keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame));
            bool back = (!OwnerTakesEsc && ((pad != null && pad.buttonEast.wasPressedThisFrame) || (keys != null && keys.escapeKey.wasPressedThisFrame))) || (!Typing && keys != null && keys.backspaceKey.wasPressedThisFrame);
            bool shift = (pad != null && pad.buttonNorth.wasPressedThisFrame) || (!Typing && keys != null && (keys.leftShiftKey.wasPressedThisFrame || keys.rightShiftKey.wasPressedThisFrame || keys.fKey.wasPressedThisFrame));
            bool swap = (pad != null && pad.buttonWest.wasPressedThisFrame) || (!Typing && keys != null && keys.gKey.wasPressedThisFrame);
            bool reload = (padLoads && pad != null && pad.dpad.up.wasPressedThisFrame) || (!Typing && keys != null && keys.tKey.wasPressedThisFrame);
            if (shift) _model.Deep = !_model.Deep;
            if (swap && Darts != null) Darts.Swap();
            if (reload && Darts != null) Darts.Reload();
            if (pad != null && Darts != null && Darts.Auto && pad.rightTrigger.ReadValue() > 0.5f) Darts.Trigger = true;   // the pad sprays where the selection is
            if (choose && _model.Selected != null && _model.Selected.Usable) { Choose(_model.Selected, _model.Middle(_model.Selected)); return; }
            if (back) Back?.Invoke();
        }

        /// <summary>Left (-1) or right (+1): a value steps (with a knock on that side); anything else hands the selection to
        /// the nearest usable control beside it on its row that way, if there is one.</summary>
        private void Side(int way)
        {
            MenuModel.Control at = _model.Selected;
            if (at == null || !at.Usable || !at.Shown) return;
            if (at.Steps)
            {
                _model.Knock(at, new Vector2(way * at.Size.x / _model.PixelsPerMetre * 0.4f, 0f), 0.35f);
                Stepped?.Invoke(at.Name, way);
                return;
            }
            MenuModel.Control best = null; float nearest = float.MaxValue;
            var shown = _model.Shown;
            for (int i = 0; i < shown.Count; i++)
            {
                MenuModel.Control c = shown[i];
                if (c == at || !c.Usable || Mathf.Abs(c.Centre.y - at.Centre.y) > Mathf.Min(c.Size.y, at.Size.y) * 0.5f) continue;
                float along = (c.Centre.x - at.Centre.x) * way;
                if (along > 0.5f && along < nearest) { nearest = along; best = c; }
            }
            if (best == null) return;
            _model.Selected = best; Moved?.Invoke();
            if (_darts != null) { _darts.Aim = _model.Middle(best); _darts.AimOn = best; }
        }

        /// <summary>The hint line for the hands this menu has (which gun is out, the ammo loaded, the shift), with
        /// <paramref name="back"/> for how to go back and <paramref name="more"/> (or null) for what this page adds. Words
        /// made when asked (a swap, a reload, a page), not every frame.</summary>
        public string HintLine(string back, string more = null)
        {
            string extra = string.IsNullOrEmpty(more) ? "" : " · " + more;
            MenuDarts gun = Darts;
            if (gun == null) return "mouse, arrows or stick · Enter / A chooses" + extra + " · " + back + " · Shift for 3D";
            if (!gun.CanReload)      // darts only
            {
                if (!gun.Chooses) return "the " + gun.Label + " only plays: hold to spray · G for the gun that chooses" + extra + " · Shift for 3D";
                return "click: a dart chooses · hold and pull: stretch" + (gun.CanSwap ? " · G swaps guns" : "") + extra + " · Shift for 3D · " + back;
            }
            // More than one kind of ammo (D-0201): the line names the one loaded, and T for the next.
            string reload = " · T for " + gun.NextAmmoLabel;
            if (!gun.Chooses) return "the " + gun.Label + " only plays: hold to spray " + gun.AmmoLabel + reload + " · G for the gun that chooses" + extra + " · Shift for 3D";
            return "click: a " + gun.AmmoOne + " chooses · hold and pull: stretch" + (gun.CanSwap ? " · G swaps guns" : "") + reload + extra + " · Shift for 3D · " + back;
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
                if (!shown[at].Usable) continue;
                if (shown[at] != _model.Selected) { _model.Selected = shown[at]; Moved?.Invoke(); }
                if (_darts != null) { _darts.Aim = _model.Middle(shown[at]); _darts.AimOn = shown[at]; }   // the gun follows the keys
                return;
            }
        }
    }
}
