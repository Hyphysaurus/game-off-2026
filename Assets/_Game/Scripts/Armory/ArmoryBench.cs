using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The armory's workbench (D-0167, the looks bench). A toy with a gun in hand presses E at it: a copy of the gun
    /// turns above the bench and a short list of rows comes up (the skin, then each kind of part the gun can take).
    /// Every change goes on the gun in the hand and on the copy at once, and is saved at once for that TYPE of gun,
    /// so every gun of that type taken later comes the same way (WeaponHolder.Equip asks WeaponBuild).
    /// It works in any scene: it holds no scene reference and finds the player's parts from whoever pressed E. The
    /// player stays where they pressed E and is turned to face the copy, so the bench can be used from any side.
    /// While it is open the player's own input is switched off (that one switch stops fire, aim, reload, drop, the
    /// slot keys, the shift, the roll, the walk and the jump); the rows have their own keys (BenchScreen). It closes
    /// by itself when the player can no longer be standing at a bench: the gun is dropped or swapped, a hit lands,
    /// the size or the game mode changes, the pause menu or the console opens, or the player is moved away. Closing
    /// always gives back exactly what opening took.
    /// It runs at order -60, with its screen: after the pause menu, before first person and the key sheet.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public sealed class ArmoryBench : MonoBehaviour, IInteractable
    {
        [Header("The copy above the bench")]
        [SerializeField, Tooltip("Where the copy of the gun turns while the bench is open. This object if empty.")]
        private Transform displayPoint;
        [SerializeField, Min(0.1f), Tooltip("How big the copy is against the pack's own model (the size the gun has in a toy's hand). 1 = the same size.")]
        private float displayScale = 2f;
        [SerializeField, Tooltip("Degrees a second the copy turns.")]
        private float spin = 40f;

        [Header("While it is open")]
        [SerializeField, Tooltip("Third person: the camera's up and down angle while the bench is open, degrees. 0 is level, below 0 looks up. The camera turns to face the copy from where the player stands.")]
        private float viewPitch = 0f;
        [SerializeField, Min(0.5f), Tooltip("The bench closes if the player ends up further than this from it (a knock, a respawn), in the bench's own units.")]
        private float leaveDistance = 4f;
        [SerializeField, Tooltip("Seconds a direction is held before it repeats.")]
        private float repeatDelay = 0.4f;
        [SerializeField, Tooltip("Seconds between repeats.")]
        private float repeatEvery = 0.12f;

        [Header("Words")]
        [SerializeField, Tooltip("Shown beside the E key when a player is in reach of the bench.")]
        private string promptText = "Change your gun's look";
        [SerializeField, Tooltip("Shown when a kid presses E: only a toy can use the bench.")]
        private string kidText = "The workbench is toy sized. Shift small to use it.";
        [SerializeField, Tooltip("Shown when E is pressed with no gun drawn and in the hand.")]
        private string noGunText = "Take a gun in your hand first.";
        [SerializeField, Tooltip("Shown when the thing in the hand has nothing to change (a melee weapon). {0} is its name.")]
        private string nothingText = "There is nothing to change on the {0}.";
        [SerializeField, Tooltip("Shown when the parts list (Resources/ArmoryCatalog) is missing: run Shift Jam > Armory > Bake Parts List.")]
        private string noListText = "The workbench has no parts list yet.";
        [SerializeField, Tooltip("Shown on closing the bench when the look was changed. {0} is the gun's name.")]
        private string savedText = "Saved. Every {0} you take comes like this.";
        [SerializeField, Tooltip("The line of keys under the gun's name while the bench is open. <color=...> tints a word.")]
        private string hintText = "<color=#8FE3FF>W S</color> row   <color=#8FE3FF>A D</color> change   <color=#8FE3FF>Esc</color> done   <color=#7F8C99>pad: stick, B</color>";

        private bool _open;
        private Interactor _asked;           // who pressed E, until Update looks at it
        private Interactor _user;            // who has the bench open
        private WeaponHolder _holder;
        private Weapon _weapon;              // the gun being changed
        private WeaponBuild _build;          // the look on the gun in the hand
        private WeaponBuild _show;           // the look on the copy above the bench (null: no copy)
        private ArmoryCatalog.Gun _gun;
        private PlayerInput _input;
        private PlayerMotor _motor;
        private PlayerMoves _moves;
        private Shifter _shifter;
        private ShoulderCamera _rig;         // the user's own camera rig, only while this bench holds it
        private GameMode.Mode _mode;         // the mode it was opened in
        private string _lookAtOpen = "";
        private Transform _turn;             // what turns: the copy hangs under it
        private BenchScreen _screen;
        private System.Func<int, string> _rowLabel, _rowValue;   // made once per opening, not once per change
        private bool _tookInput;             // this bench switched the user's input off
        private bool _holding;               // this bench holds the user's body in place

        /// <summary>The words beside the E key. Nothing while the bench is in use or in Build Mode. A kid sees it too
        /// and is told why not on the press. No lookups here: the Interactor asks every frame.</summary>
        public string PromptFor(Interactor who)
        {
            if (who == null || _open || GameMode.Current == GameMode.Mode.Build) return null;
            return promptText;
        }

        /// <summary>Only notes who asked. WHY: this is called from inside the player's own input callback, and
        /// opening the bench switches that input off. That is done from Update, outside the callback.</summary>
        public void Interact(Interactor who)
        {
            if (!_open) _asked = who;
        }

        private void Update()
        {
            if (_asked != null)
            {
                Interactor who = _asked;
                _asked = null;
                TryOpen(who);
            }
            if (!_open) return;
            if (!StillGood()) { Close(); return; }
            if (_turn != null) _turn.Rotate(0f, spin * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        private void OnDisable()
        {
            _asked = null;
            if (_open) Close();   // switched off or destroyed while open: the player gets everything back
        }

        /// <summary>Opens the bench for this player, or says why not (a toast and the "no" sound).</summary>
        private void TryOpen(Interactor who)
        {
            if (who == null || _open || Time.timeScale <= 0f) return;
            PlayerMoves moves = who.GetComponent<PlayerMoves>();
            if (moves != null && !moves.Free) return;   // mid-roll, mid-hit: the press is dropped

            Shifter shifter = who.Shifter;
            if (shifter != null && (shifter.Size != ShiftSize.Toy || shifter.Shifting)) { Refuse(who, kidText); return; }
            WeaponHolder holder = who.GetComponent<WeaponHolder>();
            if (holder == null || !holder.Armed) { Refuse(who, noGunText); return; }
            if (ArmoryCatalog.Load() == null)
            {
                Refuse(who, noListText);
#if UNITY_EDITOR
                Debug.LogWarning("[Armory] No Resources/ArmoryCatalog: run Shift Jam > Armory > Bake Parts List.");
#endif
                return;
            }
            WeaponBuild build = WeaponBuild.For(holder.Current.transform);
            if (build == null) { Refuse(who, Fill(nothingText, holder.Current.DisplayName)); return; }
            Open(who, holder, build);
        }

        private static void Refuse(Interactor who, string text)
        {
            Toasts.ShowFor(who, text, ToastKind.Refused);
            UiAudio.Play(UiAudio.Kind.Denied);
        }

        /// <summary>The text with {0} replaced by the name. Not string.Format: a stray brace typed into the field
        /// in the Inspector would make that throw in the middle of opening or closing.</summary>
        private static string Fill(string text, string name)
        {
            return string.IsNullOrEmpty(text) ? "" : text.Replace("{0}", name != null ? name : "");
        }

        private void Open(Interactor who, WeaponHolder holder, WeaponBuild build)
        {
            _open = true;
            _user = who;
            _holder = holder;
            _weapon = holder.Current;
            _build = build;
            _gun = build.Gun;
            _input = who.GetComponent<PlayerInput>();
            _motor = who.GetComponent<PlayerMotor>();
            _moves = who.GetComponent<PlayerMoves>();
            _shifter = who.Shifter;
            _mode = GameMode.Current;
            _lookAtOpen = build.Look;
            _rowLabel = build.RowLabel;
            _rowValue = build.RowValue;
            holder.CancelReload();

            // The copy: the pack's ready-made build of this gun, side on, turning about its own middle.
            Transform point = displayPoint != null ? displayPoint : transform;
            _turn = new GameObject("BenchDisplay").transform;
            _turn.SetParent(point, false);
            _turn.localPosition = Vector3.zero;
            _turn.localRotation = Quaternion.Euler(0f, 90f, 0f);
            if (_gun.display != null)
            {
                GameObject copy = Instantiate(_gun.display, _turn);
                copy.name = _gun.id;   // the parts list knows a gun by the name of its model
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                // Only a picture: never shot, bumped or picked up.
                foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                foreach (Rigidbody body in copy.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
                // It is the bare model, not a weapon: its sight takes the game's look here, as a stand's display does (D-0166).
                SightLook sights = SightLook.Load();
                if (sights != null) sights.Apply(copy.transform, _weapon.Reticle);
                copy.transform.localPosition = -Middle(_turn, copy.transform);
                _show = WeaponBuild.For(copy.transform);
                if (_show != null) _show.Apply(_build.Look);
            }
            _turn.localScale = Vector3.one * displayScale;

            // Hands off. This one switch stops every key and button of the player (they all come through PlayerInput);
            // it is the switch the pause menu uses.
            _tookInput = _input != null && _input.enabled;
            if (_tookInput) _input.enabled = false;

            // Third person only: the body stays put and turns to the copy, and so does the camera. First person: the
            // view stands still by itself (its look comes through the same input), the mouse stays captured, and keys
            // or the pad drive the rows.
            if (_mode == GameMode.Mode.Play)
            {
                Vector3 to = point.position - who.transform.position;
                to.y = 0f;
                float yaw = to.sqrMagnitude > 1e-6f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : who.transform.eulerAngles.y;
                if (_motor != null && _motor.enabled)
                {
                    _motor.HoldAt = who.transform.position;
                    _motor.HoldYaw = yaw;
                    _holding = true;
                }
                // This player's own rig (a party player has their own). Held as a menu: the mouse is free for the
                // arrows, the camera stops turning, and the pause menu stands aside (Esc is "done" here).
                _rig = PlayerSlot.RigFor(who);
                if (_rig != null && _rig.isActiveAndEnabled)
                {
                    _rig.SetMenuHold(true);
                    _rig.Warp(yaw, viewPitch);
                }
                else _rig = null;
            }

            string title = _weapon.DisplayName != null ? _weapon.DisplayName.ToUpperInvariant() : "";
            _screen = BenchScreen.Show(title, hintText, _input, repeatDelay, repeatEvery, OnStep, Close);
            Refresh();
            UiAudio.Play(UiAudio.Kind.Open);
            UiAudio.Hush();
        }

        /// <summary>A row was moved a step: the gun in the hand changes, the copy follows, and the look is saved
        /// straight away (like Options), so nothing is lost if the game closes with the bench open.</summary>
        private void OnStep(int row, int direction)
        {
            if (!_open || _build == null || _gun == null) return;
            _build.Step(row, direction);
            string look = _build.Look;
            if (_show != null) _show.Apply(look);
            ArmoryLooks.Set(_gun.id, look);
            Refresh();
        }

        private void Refresh()
        {
            if (_screen != null && _build != null) _screen.SetRows(_build.RowCount, _rowLabel, _rowValue);
        }

        /// <summary>False as soon as the player can no longer be standing at the bench with that gun.</summary>
        private bool StillGood()
        {
            if (_user == null || _holder == null || _weapon == null || _build == null) return false;
            if (_holder.Current != _weapon || !_holder.Armed) return false;                    // dropped, swapped, holstered
            if (GameMode.Current != _mode || Time.timeScale <= 0f) return false;
            if (DebugConsole.IsOpen || PauseMenu.IsOpen) return false;
            if (_shifter != null && (_shifter.Size != ShiftSize.Toy || _shifter.Shifting)) return false;
            if (_moves != null && !_moves.Free) return false;                                  // a hit or a knock-out: the keys go back
            if (_tookInput && _input != null && _input.enabled) return false;                  // someone else gave the keys back (a cutscene ending)
            Vector3 gap = _user.transform.position - transform.position;
            gap.y = 0f;
            float reach = leaveDistance * transform.lossyScale.x;
            return gap.sqrMagnitude <= reach * reach;
        }

        /// <summary>Closes the bench and gives back everything opening took. Safe to call twice.</summary>
        private void Close()
        {
            if (!_open) return;
            _open = false;   // first: nothing below can come back in here

            string look = _build != null ? _build.Look : _lookAtOpen;
            bool changed = look != _lookAtOpen;
            string gunName = _weapon != null ? _weapon.DisplayName : "";
            // Every other gun carried takes its saved look: a second gun of this type gets the new one (it may have no
            // WeaponBuild yet), guns of other types are put on again as they already are.
            if (changed && _holder != null)
                foreach (Weapon carried in _holder.GetComponentsInChildren<Weapon>(true))
                    if (carried != _weapon) WeaponBuild.PutSavedLookOn(carried);

            if (_screen != null) _screen.Hide();
            if (_turn != null) Destroy(_turn.gameObject);

            if (_holding && _motor != null) _motor.HoldAt = null;
            if (_rig != null) _rig.SetMenuHold(false);   // takes the mouse back if it is running
            // Not while the console is open: it switches the input back on itself when it closes. In Build Mode and
            // under the pause menu switching it on is right: Build Mode switches it off again and gives it back on
            // leaving, and the pause menu found it off, so it will not switch it on itself.
            if (_tookInput && _input != null && !DebugConsole.IsOpen) _input.enabled = true;

            if (changed) Toasts.ShowFor(_user, Fill(savedText, gunName), ToastKind.Got);
            UiAudio.Play(UiAudio.Kind.Back);

            _user = null; _holder = null; _weapon = null; _build = null; _show = null; _gun = null;
            _input = null; _motor = null; _moves = null; _shifter = null; _rig = null;
            _turn = null; _screen = null; _rowLabel = null; _rowValue = null;
            _tookInput = false; _holding = false; _lookAtOpen = "";
        }

        /// <summary>The middle of everything drawn under <paramref name="root"/>, in <paramref name="space"/>'s own
        /// axes: the centre of the box round the eight corners of every mesh's bounds (the corner maths the weapon
        /// uses to measure its stock). The copy is moved by minus this, so it turns about its own middle.</summary>
        private static Vector3 Middle(Transform space, Transform root)
        {
            Matrix4x4 toSpace = space.worldToLocalMatrix;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            bool any = false;
            foreach (MeshFilter part in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (part.sharedMesh == null) continue;
                Bounds b = part.sharedMesh.bounds;
                Matrix4x4 m = toSpace * part.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = m.MultiplyPoint3x4(new Vector3((corner & 1) == 0 ? b.min.x : b.max.x, (corner & 2) == 0 ? b.min.y : b.max.y, (corner & 4) == 0 ? b.min.z : b.max.z));
                    if (!any) { min = max = p; any = true; }
                    else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                }
            }
            return any ? (min + max) * 0.5f : Vector3.zero;
        }

        private void OnDrawGizmosSelected()
        {
            Transform point = displayPoint != null ? displayPoint : transform;
            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(point.position, 0.25f * displayScale * point.lossyScale.x);
        }
    }
}
