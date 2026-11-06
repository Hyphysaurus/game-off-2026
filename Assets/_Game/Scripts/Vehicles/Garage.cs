using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// A drive-in bay. Roll a car in slowly and it is parked on the lift, repaired, and a menu opens to change the body
    /// (any of the garage's car prefabs, keeping the driver in the seat), the wheels, and every swappable part (CarLook).
    /// Enter, the pad's Start or the button drives out; E gets out as usual. Wrecked cars elsewhere in the scene are towed
    /// to a free bay instead of their start (ArcadeCar asks <see cref="TowSpot"/>). The bay is this object's trigger; the
    /// car sits at <see cref="lift"/>, facing its forward (the way out).
    /// The garage never switches the player's input or camera off (Build Mode, the console and first person own those
    /// switches): the parked car cannot move, and the camera only lends the mouse to the menu while it is up.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Garage : MonoBehaviour, IResettable
    {
        private const string CatalogPrefix = "Pieces/Vehicles/";   // Build Mode's ids for the car pieces (BuildCatalogBuilder)

        [SerializeField, Tooltip("Where a docked car sits; its forward is the way out. This transform if empty.")] private Transform lift;
        [SerializeField, Tooltip("Cars the body button cycles through (ArcadeCar prefabs).")] private GameObject[] bodies;
        [SerializeField, Tooltip("Wheel sets the wheel button cycles through, after the car's own.")] private GameObject[] wheels;
        [SerializeField, Tooltip("Cars faster than this (units/s at car scale 1) roll through without docking.")] private float maxEntrySpeed = 6f;

        private static readonly List<Garage> All = new List<Garage>();

        private ArcadeCar _docked;
        private ArcadeCar _leaving;   // just drove out (or was towed in): ignored until it clears the bay
        private int _body = -1;
        private PlayerInput _input;   // the driver's; only others switch it off (the console), and then they have the keys
        private ShoulderCamera _camera;
        private GameObject _menu;
        private GraphicRaycaster _raycaster;
        private RectTransform _rows;

        private Transform Lift => lift != null ? lift : transform;
        private Quaternion LiftRotation => Quaternion.Euler(0f, Lift.eulerAngles.y, 0f);

        private void Awake() { GetComponent<BoxCollider>().isTrigger = true; }
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); if (_docked != null) Undock(); }

        /// <summary>A free bay for a recovered wreck: the garage nearest to where it wrecked, with nothing docked and nothing in the way.</summary>
        public static bool TowSpot(ArcadeCar car, Vector3 from, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            Garage best = null; float bestD = float.MaxValue;
            foreach (var g in All)
            {
                if (g._docked != null || g.CarInBay(car)) continue;
                float d = (g.Lift.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = g; }
            }
            if (best == null) return false;
            position = best.Lift.position;
            rotation = best.LiftRotation;
            best._leaving = car;   // set down in the bay, not driven in: do not dock until it is driven out and back
            return true;
        }

        /// <summary>True when a car other than <paramref name="except"/> stands in the bay.</summary>
        private bool CarInBay(ArcadeCar except)
        {
            var box = GetComponent<BoxCollider>();
            var hits = Physics.OverlapBox(transform.TransformPoint(box.center), Vector3.Scale(box.size, transform.lossyScale) * 0.5f, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var c = CarOf(h);
                if (c != null && c != except) return true;
            }
            return false;
        }

        private static ArcadeCar CarOf(Collider c) => c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<ArcadeCar>() : null;

        private void OnTriggerStay(Collider other)
        {
            if (_docked != null || GameMode.Current != GameMode.Mode.Play) return;
            var car = CarOf(other);
            if (car == null || car == _leaving || !car.Occupied || car.Wrecked) return;
            if (car.Speed > maxEntrySpeed * car.Scale) return;
            if (CarInBay(car)) return;   // another car stands on the lift: never drop this one on top of it
            Dock(car);
        }

        private void OnTriggerExit(Collider other)
        {
            var car = CarOf(other);
            if (car != null && car == _leaving && !CarStillInside(car)) _leaving = null;
        }

        private bool CarStillInside(ArcadeCar car)
        {
            var box = GetComponent<BoxCollider>();
            return new Bounds(Vector3.zero, box.size).Contains(transform.InverseTransformPoint(car.transform.position) - box.center);
        }

        private void Dock(ArcadeCar car)
        {
            _docked = car;
            car.PlaceAt(Lift.position, LiftRotation);
            car.Park(true);
            car.Repair();
            _body = BodyIndex(car);
            var seat = car.GetComponent<CarSeat>();
            _input = seat != null && seat.Driver != null ? seat.Driver.GetComponent<PlayerInput>() : null;
            OpenMenu();
        }

        private void Undock()
        {
            var car = _docked;
            _docked = null;
            CloseMenu();
            if (car == null) return;
            car.Park(false);
            _leaving = car;
        }

        public void ResetState() { if (_docked != null) Undock(); _leaving = null; }

        private int BodyIndex(ArcadeCar car)
        {
            if (bodies == null) return -1;
            string n = car.name.Replace("(Clone)", "").Replace("(copy)", "").Trim();
            for (int i = 0; i < bodies.Length; i++) if (bodies[i] != null && n == bodies[i].name) return i;
            for (int i = 0; i < bodies.Length; i++) if (bodies[i] != null && n.StartsWith(bodies[i].name)) return i;
            return -1;
        }

        private void Update()
        {
            if (_docked == null) return;
            if (!_docked.Occupied || GameMode.Current != GameMode.Mode.Play) { Undock(); return; }   // got out, or left Play
            // The console is drawn over the menu but cannot block its clicks: the menu stands down while it is open.
            bool console = DebugConsole.IsOpen;
            if (_raycaster != null && _raycaster.enabled == console) _raycaster.enabled = !console;
            if (console || (_input != null && !_input.enabled)) return;   // the console (or another menu) has the keys
            var k = Keyboard.current; var pad = Gamepad.current;
            if ((k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) || (pad != null && pad.startButton.wasPressedThisFrame)) Undock();
        }

        // ---------- swaps ----------

        private void StepBody(int dir)
        {
            if (bodies == null || bodies.Length == 0 || _docked == null) return;
            var old = _docked;
            var seat = old.GetComponent<CarSeat>();
            var who = seat != null && seat.Driver != null ? seat.Driver.GetComponent<Interactor>() : null;
            int next = _body < 0 ? (dir > 0 ? 0 : bodies.Length - 1) : ((_body + dir) % bodies.Length + bodies.Length) % bodies.Length;
            if (bodies[next] == null) return;

            // The new body takes the old one's place: same parent (so Build Mode still sees it), same scale, on the lift.
            var go = Instantiate(bodies[next], Lift.position, LiftRotation, old.transform.parent);
            go.name = bodies[next].name;
            go.transform.localScale = old.transform.localScale;
            var car = go.GetComponent<ArcadeCar>();
            var newSeat = go.GetComponent<CarSeat>();
            // Checked before anyone leaves the old car, so a refused swap changes nothing.
            if (car == null || newSeat == null || (who != null && newSeat.PromptFor(who) != "Drive")) { Destroy(go); return; }
            car.Park(true);
            car.RefreshParts();       // wheel sizes at this scale
            car.InheritStart(old);    // F9 and wreck recovery send it where the old car started, not to the lift
            var look = go.GetComponent<CarLook>();
            if (look != null) look.CopyFrom(old.GetComponent<CarLook>(), wheels);

            var placed = old.GetComponent<Buildable>();
            if (placed != null) go.AddComponent<Buildable>().InitPlaced(CatalogPrefix + bodies[next].name);
            if (seat != null) seat.GetOut(false);
            old.Park(false);
            old.gameObject.SetActive(false);
            // A car that belongs to the room is kept, hidden, the way Build Mode deletes: its Reset, Load and Undo bring it back.
            if (placed == null || !placed.IsSceneOriginal) Destroy(old.gameObject);

            if (who != null) newSeat.Interact(who);
            _docked = car;
            _body = next;
            // Build Mode's undo steps still name the old body; undoing one would bring it back beside the new one.
            if (placed != null) Buildable.ReportReplaced();
            BuildRows();
        }

        // ---------- menu ----------

        private void OpenMenu()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            _camera = FindFirstObjectByType<ShoulderCamera>();
            if (_camera != null) _camera.SetMenuHold(true);   // the mouse is the menu's; the camera keeps following the car
            else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            _menu = new GameObject("GarageMenu", typeof(RectTransform));
            var canvas = _menu.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            var scaler = _menu.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            _raycaster = _menu.AddComponent<GraphicRaycaster>();

            var panel = BuildUiKit.Rect("Panel", _menu.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(520f, 900f));
            BuildUiKit.Panel(panel, BuildUiKit.PanelColor);
            var title = BuildUiKit.Rect("Title", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-32f, 56f));
            BuildUiKit.Label(title, "GARAGE", 40).fontStyle = FontStyles.Bold;
            var note = BuildUiKit.Rect("Note", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(-32f, 30f));
            BuildUiKit.Label(note, "Repaired.  Enter: drive out   E: get out   Right drag / A D: look around", 18).color = new Color(1f, 1f, 1f, 0.6f);

            var area = BuildUiKit.Rect("Rows", panel, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), Vector2.zero);
            area.offsetMin = new Vector2(16f, 90f); area.offsetMax = new Vector2(-16f, -110f);
            _rows = BuildUiKit.ScrollView(area, out _);
            var layout = _rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f; layout.childControlHeight = false; layout.childForceExpandHeight = false; layout.childControlWidth = true;

            var outRt = BuildUiKit.Rect("DriveOut", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-32f, 56f));
            var drive = BuildUiKit.Button(outRt, "Drive out", 26, Undock, out _);
            Stretch(drive.GetComponent<RectTransform>());
            BuildUiKit.SetFill(drive, BuildUiKit.ActiveColor);

            BuildRows();
            var current = EventSystem.current;
            if (current != null) current.SetSelectedGameObject(drive.gameObject);   // a pad's A presses it
        }

        private void BuildRows()
        {
            if (_rows == null || _docked == null) return;
            BuildUiKit.Clear(_rows);
            string bodyName = _body >= 0 ? Pretty(bodies[_body].name) : Pretty(_docked.name);
            Row("Body", bodyName, StepBody);
            var look = _docked.GetComponent<CarLook>();
            if (look == null) return;
            string wheelName = look.WheelIndex < 0 || wheels == null ? "Stock" : Pretty(wheels[look.WheelIndex].name);
            Row("Wheels", wheelName, d =>
            {
                int count = wheels != null ? wheels.Length : 0;
                int i = look.WheelIndex + 1;   // 0 = stock
                i = ((i + d) % (count + 1) + count + 1) % (count + 1);
                look.SetWheels(i - 1, wheels);
                BuildRows();
            });
            foreach (var slot in look.Slots)
            {
                var s = slot;
                Row(s.Name, s.CurrentLabel, d => { look.Step(s, d); BuildRows(); });
            }
        }

        private static string Pretty(string n) => n.Replace("(Clone)", "").Replace("CyberCar", "Car ").Replace("_", " ").Trim();

        private void Row(string label, string value, System.Action<int> step)
        {
            var row = BuildUiKit.Rect(label, _rows, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 52f));
            BuildUiKit.Panel(row, new Color(1f, 1f, 1f, 0.05f));
            var name = BuildUiKit.Rect("Name", row, new Vector2(0f, 0f), new Vector2(0.38f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            name.offsetMin = new Vector2(14f, 0f); name.offsetMax = Vector2.zero;
            BuildUiKit.Label(name, label, 22, TextAlignmentOptions.MidlineLeft);
            var prev = BuildUiKit.Rect("Prev", row, new Vector2(0.38f, 0.5f), new Vector2(0.38f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            Stretch(BuildUiKit.Button(prev, "<", 26, () => step(-1), out _).GetComponent<RectTransform>());
            var val = BuildUiKit.Rect("Value", row, new Vector2(0.38f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            val.offsetMin = new Vector2(52f, 0f); val.offsetMax = new Vector2(-60f, 0f);
            BuildUiKit.Label(val, value, 22).color = new Color(0.75f, 0.92f, 1f);
            var next = BuildUiKit.Rect("Next", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(44f, 44f));
            Stretch(BuildUiKit.Button(next, ">", 26, () => step(1), out _).GetComponent<RectTransform>());
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void CloseMenu()
        {
            if (_menu != null) Destroy(_menu);
            _menu = null; _rows = null; _raycaster = null;
            if (_camera != null) _camera.SetMenuHold(false);   // takes the mouse back if running (Build Mode may have it off)
            _camera = null; _input = null;
        }
    }
}
