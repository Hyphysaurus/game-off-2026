using DG.Tweening;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The gun snatch (D-0241), his "the dart gun should be found under the bed, we need to somehow indicate that the
    /// character has to go under the bed to get the gun" (his pick: THE SNATCH, with the GLINT as backup).
    /// - Before: the dart gun lies out on the floor by the bed's mouth (the Prop: the stand's own pistol model, no pickup).
    /// - The first time the player grows there (New Game: the box core, the box bursts), a short moment: the player's keys
    ///   are held, the view turns to the gun as it turns to a switch's door (CauseCamera's way: it only turns and zooms, so
    ///   it can never end up in a wall), one cave monster runs out (it is BORROWED: its own brain stops while DOTween runs it
    ///   along the maker's measured way), takes the gun (its attack clip, a squeak, and the player's line), and backs away
    ///   under the bed dragging it, staring back. Once the gun is under the bed the view turns back and the keys return;
    ///   the monster drags it on to its place deep under the bed, leaves it there and walks home.
    /// - After: the gun waits deep under the bed as a weapon stand that gives it once (the Pickup, toy-sized, the maker's
    ///   measured spot clear of the monsters' way). Toy Only (on): only a toy-sized player can take it. THE GLINT, always
    ///   on: a flashlight's beam on it fills its LightTarget and its LightGlow lights the gun up (the maker gives the two
    ///   guns an emissive copy of the pistol's material and wires the two together, so it needs nothing from here).
    /// Saved per save slot (Settings > Snatch Key, marked as the moment starts): Continue finds the gun under the bed; New
    /// Game puts it back out by the bed. Online, or with Settings > Snatch On unticked, there is no moment: the gun is under
    /// the bed from the start. Numbers and words: Resources/BedroomGateSettings. Placed by Shift Jam > House > Land Bedroom
    /// Gate (D-0241). OFF SWITCHES: Snatch On (no moment), Toy Only (anyone takes it), or untick this (the guns stay as the
    /// maker leaves the scene: the gun under the bed, glinting, anyone takes it; the prop hidden).
    /// Runs after the camera rigs (order 1001, just after CauseCamera): its turn of the view is the last word in a frame.
    /// </summary>
    [DefaultExecutionOrder(1001)]
    [DisallowMultipleComponent]
    public sealed class GunSnatch : MonoBehaviour
    {
        [Header("The guns (placed by the maker)")]
        [SerializeField, Tooltip("The gun lying out by the bed's mouth before the snatch: the stand's own pistol model on its side, no pickup (it cannot be taken). Hidden once the snatch has played.")]
        private Transform prop;
        [SerializeField, Tooltip("The prop's glow (LightGlow) while it is taken, so it reads in the dark room.")]
        private LightGlow propGlow;
        [SerializeField, Tooltip("A small light on the prop while it is taken (off otherwise; it sits under a KeepLit so the dark room does not dim it).")]
        private Light propLight;
        [SerializeField, Tooltip("The dart gun under the bed: a DartPistolPickup stand that gives the gun once (Respawn Seconds 0). Hidden until the snatch has played.")]
        private WeaponPickup pickup;
        [SerializeField, Tooltip("The pickup's LightTarget: a flashlight's beam on the gun fills it (the maker writes Settings > Glint's numbers onto it and wires its On Fill to the LightGlow, so the glint works with this unticked too). Reported only.")]
        private LightTarget glintTarget;
        [SerializeField, Tooltip("The pickup's LightGlow on the gun's own parts, lit as the LightTarget fills. Reported only.")]
        private LightGlow glint;

        [Header("The run (measured by the maker)")]
        [SerializeField, Tooltip("The cave monster that runs out (the one nearest the bed's mouth when the maker ran). Gone or down: the awake cave monster nearest the mouth.")]
        private DarkMonster snatcher;
        [SerializeField, Tooltip("Where the monsters' way goes under the bed (world, on the floor): the view frames the floor from here to the gun, and turns back once the gun has been dragged past it.")]
        private Vector3 mouth;
        [SerializeField, Tooltip("The run out (world, on the floor), from wherever the monster stands: through the bed's mouth to where it stops in front of the gun.")]
        private Vector3[] runOut = new Vector3[0];
        [SerializeField, Tooltip("The drag back (world, on the floor), from the gun: back past the bed's mouth (the first point) and on to where it stops, the gun ending where the Pickup stands.")]
        private Vector3[] dragBack = new Vector3[0];

        private enum Phase { Out, Moment, Under }

        private BedroomGateSettings _s;
        private Phase _phase = Phase.Out;
        private float _beginAt = -1f;
        private Sequence _run;
        private DarkMonster _monster;
        private bool _dragging;
        private Vector3 _gripOffset;            // the prop where it was taken, in the monster's own turn
        private Quaternion _gripTurn = Quaternion.identity;
        private Transform _propParent;          // the prop as placed (put back on New Game)
        private Vector3 _propAt;
        private Quaternion _propTurn = Quaternion.identity;
        private bool _propKept, _bound, _started;
        private Shifter _player;                // Toy Only: this PC's player, found by name
        private float _findAgain;

        // The view (as CauseCamera turns it).
        private Camera _cam;
        private PlayerInput _input;
        private bool _tookInput;
        private float _viewAt = -1f, _viewOutAt = -1f;
        private Vector3 _frameAt;
        private float _frameSize;

        /// <summary>True while the moment plays (the maker's report and the console can ask).</summary>
        public bool Playing => _phase == Phase.Moment;
        /// <summary>True once the gun waits under the bed.</summary>
        public bool Under => _phase == Phase.Under;

        private void Awake()
        {
            _s = BedroomGateSettings.Load();
            if (prop != null)
            {
                _propParent = prop.parent; _propAt = prop.localPosition; _propTurn = prop.localRotation;
                _propKept = true;
            }
            if (propLight != null) propLight.enabled = false;
        }

        private void OnEnable()
        {
            if (_bound) return;
            SaveGame.Changed += Read;
            GameEvents.Shifted += OnShifted;
            _bound = true;
            if (_started) Read();   // switched on again: the guns as the slot says (Start reads it the first time)
        }

        private void OnDisable()
        {
            if (!_bound) return;
            SaveGame.Changed -= Read;
            GameEvents.Shifted -= OnShifted;
            _bound = false;
            // Switched off mid-moment: the keys and the monster go back; the guns are set again on the next Start or save read.
            if (_run != null) { _run.Kill(); _run = null; }
            _dragging = false;
            if (_monster != null && _monster.Borrowed) _monster.GiveBack();
            _monster = null;
            EndView();
            if (_phase == Phase.Moment) _phase = Phase.Out;
            _beginAt = -1f;
        }

        private void Start()
        {
            _started = true;
            Read();
        }

        /// <summary>What the active slot says: the gun out by the bed, or (the snatch played, online, switched off) under
        /// it. Heard on SaveGame.Changed, never every frame; the moment itself is left to finish.</summary>
        private void Read()
        {
            if (_phase == Phase.Moment || _s == null) return;
            bool under = !_s.snatchOn || NetSession.InParty || prop == null
                || (!string.IsNullOrEmpty(_s.snatchKey) && SaveGame.Seen(SaveGame.Slot, _s.snatchKey));
            Show(under ? Phase.Under : Phase.Out);
        }

        private void Show(Phase phase)
        {
            _phase = phase;
            _beginAt = -1f;
            bool under = phase == Phase.Under;
            if (prop != null)
            {
                if (!under) PutPropBack();
                if (prop.gameObject.activeSelf == under) prop.gameObject.SetActive(!under);
            }
            if (pickup != null && pickup.gameObject.activeSelf != under) pickup.gameObject.SetActive(under);
            Glow(false);
        }

        private void PutPropBack()
        {
            if (!_propKept || prop == null) return;
            if (prop.parent != _propParent) prop.SetParent(_propParent, false);
            prop.SetLocalPositionAndRotation(_propAt, _propTurn);
        }

        /// <summary>The first growth near the gun (this PC's own player): the moment starts Start Delay later.</summary>
        private void OnShifted(Shifter who, ShiftSize size)
        {
            if (size != ShiftSize.Kid || _phase != Phase.Out || _beginAt >= 0f || who == null || prop == null || !prop.gameObject.activeInHierarchy) return;
            var charge = who.GetComponent<ShiftCharge>();   // plain form: Fast Script Reload breaks `TryGetComponent(out ...) &&`
            if (charge == null || !charge.HasHud) return;
            if ((who.transform.position - prop.position).sqrMagnitude > _s.watchRadius * _s.watchRadius) return;
            _beginAt = Time.time + _s.startDelay;
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Out:
                    if (_beginAt >= 0f && Time.time >= _beginAt) TryBegin();
                    break;
                case Phase.Moment:
                    if (_monster == null || !_monster.Borrowed) Abort();   // a New Game or "bedroom reset" took it back
                    break;
                case Phase.Under:
                    HoldForToy();
                    break;
            }
        }

        private void LateUpdate()
        {
            if (_dragging) PlaceProp();
            View();
        }

        // ------------------------------------------------------------------------------------------------ the moment

        /// <summary>Starts the moment once nothing else holds the screen; with no monster to send, the gun is simply under
        /// the bed.</summary>
        private void TryBegin()
        {
            if (Opening.Playing || TitleMenu.IsOpen || PauseMenu.IsOpen || DebugConsole.IsOpen || Time.timeScale <= 0f) return;
            if (GameMode.Current != GameMode.Mode.Play && GameMode.Current != GameMode.Mode.FirstPerson) return;
            _beginAt = -1f;
            if (runOut == null || runOut.Length == 0 || dragBack == null || dragBack.Length == 0)
            {
                Debug.LogWarning("GUN SNATCH: no measured way (run Shift Jam > House > Land Bedroom Gate (D-0241)): the gun is put under the bed without the moment.");
                Mark();
                Show(Phase.Under);
                return;
            }
            _monster = Snatcher();
            if (_monster == null || !_monster.Borrow())
            {
                Debug.LogWarning("GUN SNATCH: no cave monster free to run out: the gun is put under the bed without the moment.");
                _monster = null;
                Mark();
                Show(Phase.Under);
                return;
            }
            _phase = Phase.Moment;
            Mark();   // after Moment (Read leaves a moment alone): the slot remembers it now, so a quit mid-way finds the gun under the bed
            Playtest.PlayLog.Tell("gun snatch: " + _monster.name + " runs out for the dart gun");
            HoldKeys();
            StartView();
            Glow(true);   // the gun's light and glow from the start: the view finds it in the dark room
            Run();
        }

        private void Mark()
        {
            if (!string.IsNullOrEmpty(_s.snatchKey)) SaveGame.MarkSeen(SaveGame.Slot, _s.snatchKey);
        }

        /// <summary>The maker's monster, or (gone, down, switched off) the awake cave monster nearest the bed's mouth.</summary>
        private DarkMonster Snatcher()
        {
            if (snatcher != null && snatcher.isActiveAndEnabled && !snatcher.Down) return snatcher;
            DarkMonster best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < DarkMonster.RoamCount; i++)
            {
                DarkMonster m = DarkMonster.RoamAt(i);
                if (m == null || m.Down) continue;
                float d = (m.transform.position - mouth).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = m; }
            }
            return best;
        }

        /// <summary>The run as one DOTween sequence: the view turns first, then out, the grab, the drag back past the mouth
        /// (the view turns back after it), on to the gun's place, the drop, and home.</summary>
        private void Run()
        {
            Transform m = _monster.transform;
            Vector3 stand = runOut[runOut.Length - 1];
            float outLength = Length(m.position, runOut, runOut.Length);
            float toMouth = Vector3.Distance(stand, dragBack[0]);
            var rest = new Vector3[dragBack.Length - 1];
            for (int i = 1; i < dragBack.Length; i++) rest[i - 1] = dragBack[i];
            float restLength = Length(dragBack[0], rest, rest.Length);

            _run = DOTween.Sequence().SetLink(gameObject);
            _run.AppendInterval(_s.turnInSeconds);
            _run.AppendCallback(RunOut);
            _run.Append(m.DOPath(runOut, Seconds(outLength, _s.runSpeed), PathType.Linear, PathMode.Full3D).SetLookAt(0.01f, Vector3.forward, Vector3.up).SetEase(Ease.Linear));
            _run.AppendCallback(Grab);
            _run.AppendInterval(_s.grabSeconds);
            _run.AppendCallback(Drag);
            _run.Append(m.DOPath(new[] { dragBack[0] }, Seconds(toMouth, _s.dragSpeed), PathType.Linear, PathMode.Full3D).SetLookAt(0.01f, Vector3.back, Vector3.up).SetEase(Ease.Linear));
            _run.AppendCallback(PastMouth);
            if (rest.Length > 0)
                _run.Append(m.DOPath(rest, Seconds(restLength, _s.carrySpeed), PathType.Linear, PathMode.Full3D).SetLookAt(0.01f, Vector3.back, Vector3.up).SetEase(Ease.Linear));
            _run.AppendCallback(Drop);
            _run.AppendInterval(_s.dropSeconds);
            _run.OnComplete(Finish);
        }

        private static float Seconds(float length, float speed) => Mathf.Max(0.05f, length / Mathf.Max(0.1f, speed));

        private static float Length(Vector3 from, Vector3[] points, int count)
        {
            float sum = 0f;
            Vector3 at = from;
            for (int i = 0; i < count; i++) { sum += Vector3.Distance(at, points[i]); at = points[i]; }
            return sum;
        }

        private void RunOut() { if (_monster != null) _monster.BorrowedAct(DarkMonster.Act.Run); }

        /// <summary>At the gun: its attack clip and a squeak, the gun rides in front of it from here, the player's line.</summary>
        private void Grab()
        {
            if (_monster == null) return;
            _monster.BorrowedAct(DarkMonster.Act.Grab);
            Transform m = _monster.transform;
            Quaternion back = Quaternion.Inverse(m.rotation);
            _gripOffset = back * (prop.position - m.position);
            _gripTurn = back * prop.rotation;
            _dragging = true;
            if (!string.IsNullOrEmpty(_s.snatchLine)) Toasts.Show(_s.snatchLine, ToastKind.Say);
        }

        private void Drag() { if (_monster != null) _monster.BorrowedAct(DarkMonster.Act.Drag); }

        /// <summary>The gun is under the bed: the view turns back after Hold After Mouth.</summary>
        private void PastMouth()
        {
            if (_viewAt >= 0f && _viewOutAt < 0f) _viewOutAt = Time.time + _s.holdAfterMouth;
        }

        /// <summary>The gun is left where the Pickup stands: the prop goes, the stand appears.</summary>
        private void Drop()
        {
            _dragging = false;
            Glow(false);
            if (prop != null) prop.gameObject.SetActive(false);
            if (pickup != null) pickup.gameObject.SetActive(true);
            if (_monster != null) _monster.BorrowedAct(DarkMonster.Act.Watch);
        }

        private void Finish()
        {
            _run = null;
            if (_monster != null) _monster.GiveBack();   // it walks home on its own from here
            _monster = null;
            Playtest.PlayLog.Tell("gun snatch: the dart gun waits under the bed");
            Show(Phase.Under);
        }

        /// <summary>The moment was cut short (its monster was taken back): no tween, keys back, the guns as the slot says.</summary>
        private void Abort()
        {
            if (_run != null) { _run.Kill(); _run = null; }
            _dragging = false;
            if (_monster != null && _monster.Borrowed) _monster.GiveBack();
            _monster = null;
            EndView();
            _phase = Phase.Out;
            Read();
        }

        private void PlaceProp()
        {
            if (_monster == null || prop == null) return;
            Transform m = _monster.transform;
            prop.SetPositionAndRotation(m.position + m.rotation * _gripOffset, m.rotation * _gripTurn);
        }

        private void Glow(bool on)
        {
            if (propGlow != null) propGlow.GlowOn(on ? _s.snatchGlow : 0f);
            if (propLight == null) return;
            if (on)
            {
                propLight.color = _s.gunLightColour;
                propLight.intensity = _s.gunLightIntensity;
                propLight.range = _s.gunLightRange;
            }
            if (propLight.enabled != on) propLight.enabled = on;
        }

        // ------------------------------------------------------------------------------------------------ the view

        /// <summary>The player holds still while the view is turned (the one switch CauseCamera uses).</summary>
        private void HoldKeys()
        {
            GameObject player = GameObject.Find("Player");
            _input = player != null ? player.GetComponent<PlayerInput>() : null;
            _tookInput = _input != null && _input.enabled;
            if (_tookInput) _input.enabled = false;
        }

        private void StartView()
        {
            _cam = Camera.main;
            Vector3 gun = prop != null ? prop.position : mouth;
            _frameAt = (gun + mouth) * 0.5f + Vector3.up * 0.5f;
            _frameSize = Vector3.Distance(gun, mouth) + 2f * _s.frameMargin;
            _viewAt = Time.time; _viewOutAt = -1f;
        }

        /// <summary>Turns from the camera's own pose to look at the gun's stretch of floor and zooms to frame it; holds;
        /// turns back. It only turns and zooms, so it never ends up inside a wall.</summary>
        private void View()
        {
            if (_viewAt < 0f) return;
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
            float now = Time.time;
            float w = Smooth((now - _viewAt) / _s.turnInSeconds);
            if (_viewOutAt >= 0f && now >= _viewOutAt)
            {
                float k = (now - _viewOutAt) / _s.turnOutSeconds;
                if (k >= 1f) { EndView(); return; }
                w *= 1f - Smooth(k);
            }
            Vector3 eye = _cam.transform.position;
            Vector3 to = _frameAt - eye;
            float distance = to.magnitude;
            if (distance < 1e-3f) return;
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, Quaternion.LookRotation(to / distance, Vector3.up), w);
            float frame = Mathf.Clamp(2f * Mathf.Atan(_frameSize / (2f * distance * _s.frameShare)) * Mathf.Rad2Deg, _s.minFov, _cam.fieldOfView);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, frame, w);
        }

        private void EndView()
        {
            if (_tookInput && _input != null) _input.enabled = true;
            _tookInput = false;
            _input = null;
            _viewAt = _viewOutAt = -1f;
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // ------------------------------------------------------------------------------------------------ the gun under the bed

        /// <summary>Toy Only: the stand gives the gun only while this PC's player is toy-sized (a switched-off WeaponPickup
        /// says nothing and gives nothing: its Available asks enabled).</summary>
        private void HoldForToy()
        {
            if (pickup == null || !pickup.gameObject.activeInHierarchy) return;
            bool open = !_s.toyOnly || NetSession.InParty || PlayerIsToy();
            if (pickup.enabled != open) pickup.enabled = open;
        }

        private bool PlayerIsToy()
        {
            if (_player == null)
            {
                if (Time.unscaledTime < _findAgain) return false;
                _findAgain = Time.unscaledTime + 1f;
                GameObject own = GameObject.Find("Player");
                if (own != null) _player = own.GetComponent<Shifter>();
                if (_player == null) return false;
            }
            return _player.Size == ShiftSize.Toy;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Vector3 at = snatcher != null ? snatcher.transform.position : mouth;
            if (runOut != null) foreach (Vector3 p in runOut) { Gizmos.DrawLine(at, p); at = p; }
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            if (dragBack != null && dragBack.Length > 0)
            {
                at = runOut != null && runOut.Length > 0 ? runOut[runOut.Length - 1] : mouth;
                foreach (Vector3 p in dragBack) { Gizmos.DrawLine(at, p); at = p; }
            }
            Gizmos.DrawWireSphere(mouth, 0.5f);
        }
    }
}
