using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The toy guns of a menu on a model (D-0199): a dart blaster whose dart CHOOSES the control it lands on, and a dart
    /// LMG that only plays (its darts knock the slabs about and choose nothing). Beside the MenuModel on its prefab; the
    /// MenuDriver aims it and pulls its trigger. Without a gun model (a copy of the project without the weapon pack) it
    /// reports that it has no gun, and the driver chooses the plain way.
    /// The gun is held at one place of the view (Held At), a little in front of the menu while flat and well toward the
    /// viewer while deep, and always points from where it is at the aim: flat, that is a gun seen from its side turning
    /// in the picture; deep, a gun in the hand pointing into it. A dart flies straight from the muzzle to where it was
    /// aimed, sticks there, rides the slab it is in, and lies the way the gun would shoot it now (so the darts turn with
    /// the shift too). The oldest darts go when there are too many, and each goes after a while.
    /// A gun model is read like the game's own: its barrel runs from the child "Magazine" to the child "Muzzle" (else
    /// along its longest side) and its top is its own up. A dart model points along +Z.
    /// Ammo (D-0201): both guns fire whichever kind is loaded, and Reload (T, the pad's D-pad up, through the MenuDriver)
    /// loads the next. The first kind is always the darts above; Ammo holds the kinds after them, one row each: a paint
    /// ball flies the same flight, wears a shell colour unlike its paint, and where it lands it knocks what it hit as a
    /// dart does and BURSTS: the MenuSplats beside it lays a splat there (or, on a title letter, the letter takes a cast of
    /// the paint, MenuTitle). A row that does not burst sticks like a dart. The blaster's ball still chooses the control it
    /// lands on; the ammo never changes what a gun does. With no rows (or none with a model) there are darts only and
    /// Reload does nothing.
    /// </summary>
    [DefaultExecutionOrder(50)]   // after the MenuModel has moved its camera and its slabs this frame: the gun and the darts ride both
    [RequireComponent(typeof(MenuModel))]
    public sealed class MenuDarts : MonoBehaviour
    {
        [Serializable]
        public sealed class Gun
        {
            [Tooltip("Its name in the hint line.")] public string label = "blaster";
            [Tooltip("Its model (a prefab of the weapon pack). None = this gun is left out.")] public GameObject model;
            [Min(0.01f), Tooltip("How many times its own size it is drawn.")] public float size = 1f;
            [Tooltip("Its dart chooses the control it lands on. Off = it only plays.")] public bool chooses = true;
            [Tooltip("It fires on while the trigger is held.")] public bool auto;
            [Min(0.02f), Tooltip("Seconds between its shots.")] public float interval = 0.2f;
            [Min(0f), Tooltip("How far its darts scatter round the aim, metres on the menu.")] public float spread;
            [Min(0f), Tooltip("How hard its dart knocks a slab (1 = a firm knock).")] public float knock = 1f;
            [Min(0f), Tooltip("How far it kicks back at a shot, metres.")] public float kick = 0.06f;
            [Tooltip("One of these plays at a shot. None = silent.")] public AudioClip[] fireClips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float volume = 0.5f;
        }

        /// <summary>A kind of ammo after the darts (D-0201): what it looks like, what it does where it lands, its sounds.</summary>
        [Serializable]
        public sealed class Ammo
        {
            [Tooltip("Its name in the hint line, as many of them (\"paint balls\").")] public string label = "paint balls";
            [Tooltip("One of them, in the hint line (\"click: a paint ball chooses\").")] public string one = "paint ball";
            [Tooltip("Its model, pointing along +Z (a ball any way). None = this kind is left out.")] public GameObject model;
            [Min(0.01f), Tooltip("How many times its own size it is drawn.")] public float size = 1f;
            [Tooltip("On: it bursts where it lands and leaves a splat (paint). Off: it sticks there like a dart (the darts' Tip Ahead, Sink and Flat Lift).")] public bool bursts = true;
            [Tooltip("Its paint: each shot takes one of these. None = white.")] public Color[] colours = Array.Empty<Color>();
            [Tooltip("Its shell, seen in flight: each shot takes one that is not its paint's hue. None = the model's own colours.")] public Color[] shellColours = Array.Empty<Color>();
            [Range(0f, 0.5f), Tooltip("A shell colour counts as not the paint's hue when its hue is this far from it round the colour wheel (0 to 0.5)...")] public float shellHueApart = 0.12f;
            [Range(0f, 1f), Tooltip("...or when it has hardly any hue at all: saturation under this (cream, black).")] public float shellPlainBelow = 0.25f;
            [Tooltip("The shell when no listed one is unlike the paint.")] public Color shellFallback = new Color(0.96f, 0.93f, 0.82f);
            [Tooltip("Its splat pictures (near-white shapes made to be tinted); each splat takes one. None = it bursts with no splat.")] public Texture2D[] pictures = Array.Empty<Texture2D>();
            [Min(0f), Tooltip("A splat's width, metres, where there is room for it (on a slab it is made smaller to lie wholly on the face).")] public float splatSize = 0.24f;
            [Tooltip("One of these plays at a shot. None = the gun's own.")] public AudioClip[] fireClips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float fireVolume = 0.45f;
            [Tooltip("One of these plays where it lands. None = the darts' landing sounds.")] public AudioClip[] landClips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float landVolume = 0.5f;
            [Tooltip("Its sounds play at a pitch between these (the game's paint: 0.91 to 1.1).")] public Vector2 pitch = new Vector2(0.91f, 1.1f);
        }

        [SerializeField, Tooltip("The guns, the first one in hand at the start. Swap goes round them.")] private Gun[] guns = new Gun[0];
        [Header("Where the gun is held")]
        [SerializeField, Tooltip("Its place in the view: 0, 0 is the bottom left corner, 1, 1 the top right.")] private Vector2 heldAt = new Vector2(0.86f, 0.2f);
        [SerializeField, Min(0f), Tooltip("While flat it is this far in front of the menu, metres.")] private float flatInFront = 0.08f;
        [SerializeField, Min(0f), Tooltip("While deep, this far toward the viewer, metres.")] private float deepInFront = 0.95f;
        [SerializeField, Min(1f), Tooltip("How quickly it comes back from a kick (higher = snappier).")] private float kickSpringiness = 320f;
        [SerializeField, Min(0f), Tooltip("How quickly that settles.")] private float kickSettle = 22f;
        [Header("Its darts")]
        [SerializeField, Tooltip("A dart's model, pointing along +Z. None = no gun at all.")] private GameObject dartModel;
        [SerializeField, Min(0.01f), Tooltip("How many times its own size a dart is drawn.")] private float dartSize = 0.45f;   // the game's dart is 0.3 m long, made large to be seen in a room: on a menu whose buttons are 0.13 m high it is drawn smaller
        [SerializeField, Min(0.1f), Tooltip("How fast a dart flies, metres a second.")] private float dartSpeed = 11f;
        [SerializeField, Tooltip("A flight never takes less or more than this, seconds (a choice must not feel late).")] private Vector2 flightSeconds = new Vector2(0.07f, 0.2f);
        [SerializeField, Min(0f), Tooltip("From a dart's middle to its tip, metres at its own size.")] private float tipAhead = 0.18f;
        [SerializeField, Min(0f), Tooltip("How far its tip sinks into what it hits, metres.")] private float sink = 0.015f;
        [SerializeField, Min(0f), Tooltip("While flat a stuck dart lies this far above the slab, metres, so it is drawn over it.")] private float flatLift = 0.06f;
        [SerializeField, Min(1), Tooltip("The most darts stuck at once: the oldest goes for a new one.")] private int mostStuck = 40;
        [SerializeField, Min(0.1f), Tooltip("Seconds a stuck dart stays.")] private float stuckSeconds = 10f;
        [SerializeField, Min(0.01f), Tooltip("Seconds a dart takes to shrink away.")] private float goSeconds = 0.25f;
        [SerializeField, Tooltip("One of these plays where a dart lands. None = silent.")] private AudioClip[] landClips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float landVolume = 0.45f;
        [Header("Its ammo (D-0201): the darts first, then these rows")]
        [SerializeField, Tooltip("The darts' name in the hint line, as many of them.")] private string dartLabel = "darts";
        [SerializeField, Tooltip("One dart, in the hint line.")] private string dartOne = "dart";
        [SerializeField, Tooltip("The kinds after the darts, in the order Reload goes round them. Empty = darts only.")] private Ammo[] ammo = new Ammo[0];

        private sealed class Dart
        {
            public Transform Body;
            public Vector3 From;
            public float Fired, Flight, StuckAt = -1f, GoingAt = -1f;
            public MenuModel.Control On;        // the slab it is aimed at or in; null = the wall behind
            public Vector2 At;                  // where: on a slab, metres from its middle; else metres from the menu's middle
            public float Knock;
            public Action Landed;
            public int Kind;                    // its place in the kinds loaded (0 = the darts)
            public Color Colour;                // its paint
            public bool Burst;                  // it burst where it landed: it is dropped
        }

        private MenuModel _model;
        private readonly List<Transform> _held = new List<Transform>();     // a holder for each gun, its barrel along +Z
        private readonly List<Vector3> _muzzles = new List<Vector3>();      // each gun's muzzle, in its holder
        private readonly List<Dart> _darts = new List<Dart>();
        private readonly List<Stack<Transform>> _spare = new List<Stack<Transform>>();   // for each kind, its bodies not in use
        private readonly List<int> _kinds = new List<int>();                // the kinds that can be loaded: -1 the darts, else a place in Ammo
        private sealed class Shell { public Renderer[] Looks; public int[] Slots; }
        private readonly Dictionary<Transform, Shell> _shells = new Dictionary<Transform, Shell>();   // a body's parts that wear its shell colour, and their slots
        private MaterialPropertyBlock _shellLook;
        private MenuSplats _splats;
        private AudioSource _sound;
        private int _gun, _kind;
        private float _nextShot, _kick, _kickSpeed, _pop = 1f, _popSpeed;
        private bool _made;

        /// <summary>The gun was swapped.</summary>
        public event Action Swapped;
        /// <summary>Another kind of ammo was loaded.</summary>
        public event Action Reloaded;
        /// <summary>More than one kind of ammo: Reload does something.</summary>
        public bool CanReload { get { Make(); return _held.Count > 0 && _kinds.Count > 1; } }
        /// <summary>The loaded ammo's name, as many of them ("darts", "paint balls").</summary>
        public string AmmoLabel => HasGun ? KindLabel(_kind, false) : "";
        /// <summary>One of the loaded ammo ("dart", "paint ball").</summary>
        public string AmmoOne => HasGun ? KindLabel(_kind, true) : "";
        /// <summary>The name of the ammo Reload loads next, as many of them.</summary>
        public string NextAmmoLabel => HasGun ? KindLabel((_kind + 1) % _kinds.Count, false) : "";
        /// <summary>There is a gun to fire.</summary>
        public bool HasGun { get { Make(); return _held.Count > 0; } }
        /// <summary>The gun in hand chooses with its dart (false: it only plays).</summary>
        public bool Chooses => HasGun && guns[Index].chooses;
        /// <summary>The gun in hand fires on while its trigger is held.</summary>
        public bool Auto => HasGun && guns[Index].auto;
        /// <summary>The gun in hand's name.</summary>
        public string Label => HasGun ? guns[Index].label : "";
        /// <summary>More than one gun: Swap does something.</summary>
        public bool CanSwap { get { Make(); return _held.Count > 1; } }
        /// <summary>A dart that will choose is in flight.</summary>
        public bool Choosing { get; private set; }
        /// <summary>Where it points: metres on the menu, across and up from its middle.</summary>
        public Vector2 Aim { get; set; }
        /// <summary>The slab at the aim; null for none.</summary>
        public MenuModel.Control AimOn { get; set; }
        /// <summary>The trigger is held (an automatic gun fires on).</summary>
        public bool Trigger { get; set; }

        private int Index => _usable[_gun];
        private readonly List<int> _usable = new List<int>();   // the guns that have a model, as places in Guns

        private void Awake() { _model = GetComponent<MenuModel>(); TryGetComponent(out _splats); }

        /// <summary>The ammo row of a kind loaded; null for the darts.</summary>
        private Ammo RowOf(int kind) => kind >= 0 && kind < _kinds.Count && _kinds[kind] >= 0 ? ammo[_kinds[kind]] : null;

        private string KindLabel(int kind, bool one)
        {
            Ammo row = RowOf(kind);
            string label = row == null ? (one ? dartOne : dartLabel) : (one ? row.one : row.label);
            return string.IsNullOrEmpty(label) ? (one ? "shot" : "shots") : label;
        }

        /// <summary>How many times its own size a kind's body is drawn, and how far its middle is behind its tip (metres at its
        /// own size): a dart's, or its row's (a ball that bursts has its middle at the point it flies to).</summary>
        private float BodySize(int kind) { Ammo row = RowOf(kind); return row == null ? dartSize : row.size; }
        private float TipAhead(int kind) { Ammo row = RowOf(kind); return row == null || !row.bursts ? tipAhead : 0f; }

        /// <summary>Builds the guns once the model has been read (the first time anything is asked of it).</summary>
        private void Make()
        {
            if (_made || _model == null || !_model.Staged) return;
            _made = true;
            if (dartModel == null) return;
            for (int i = 0; i < guns.Length; i++)
            {
                if (guns[i] == null || guns[i].model == null) continue;
                var holder = new GameObject("MenuGun_" + guns[i].label).transform;
                holder.SetParent(transform, false);
                GameObject shape = Instantiate(guns[i].model, holder);
                shape.name = guns[i].model.name;
                foreach (Collider c in shape.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (MonoBehaviour b in shape.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                // Its barrel along the holder's +Z, its middle at the holder, as the game's own guns are read.
                Transform muzzle = Find(shape.transform, "Muzzle"), magazine = Find(shape.transform, "Magazine");
                shape.transform.localPosition = Vector3.zero; shape.transform.localRotation = Quaternion.identity; shape.transform.localScale = Vector3.one * guns[i].size;
                if (muzzle != null && magazine != null)
                {
                    Vector3 along = holder.InverseTransformPoint(muzzle.position) - holder.InverseTransformPoint(magazine.position);
                    along.y = 0f;
                    if (along.sqrMagnitude > 1e-8f) shape.transform.localRotation = Quaternion.FromToRotation(along.normalized, Vector3.forward);
                }
                else
                {
                    Vector3 s = Box(holder, shape).size;
                    if (s.x >= s.y && s.x >= s.z) shape.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    else if (s.y >= s.x && s.y >= s.z) shape.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                Bounds box = Box(holder, shape);
                shape.transform.localPosition = -box.center;
                // Where its darts leave it: at the socket's height and side, at the very front of whatever is fitted on the barrel.
                Vector3 tip = new Vector3(0f, 0f, box.extents.z);
                if (muzzle != null) { Vector3 socket = holder.InverseTransformPoint(muzzle.position); tip = new Vector3(socket.x, socket.y, Mathf.Max(socket.z, box.extents.z)); }
                _model.Flatten(shape);
                holder.gameObject.SetActive(false);
                _held.Add(holder); _muzzles.Add(tip); _usable.Add(i);
            }
            if (_held.Count == 0) return;
            _held[0].gameObject.SetActive(true);
            _kinds.Add(-1); _spare.Add(new Stack<Transform>());                 // the darts are always the first kind
            for (int i = 0; i < ammo.Length; i++)
                if (ammo[i] != null && ammo[i].model != null) { _kinds.Add(i); _spare.Add(new Stack<Transform>()); }
            _sound = gameObject.AddComponent<AudioSource>();
            _sound.playOnAwake = false; _sound.spatialBlend = 0f; _sound.ignoreListenerPause = true;
        }

        private static Transform Find(Transform under, string name)
        {
            foreach (Transform t in under.GetComponentsInChildren<Transform>(true)) if (t.name.Contains(name)) return t;   // as the game's own guns are read
            return null;
        }

        /// <summary>The box round a thing's meshes, in <paramref name="space"/>.</summary>
        private static Bounds Box(Transform space, GameObject thing)
        {
            bool any = false; var box = new Bounds();
            foreach (MeshFilter f in thing.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                Bounds m = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 at = space.InverseTransformPoint(f.transform.TransformPoint(corner));
                    if (!any) { box = new Bounds(at, Vector3.zero); any = true; } else box.Encapsulate(at);
                }
            }
            return box;
        }

        /// <summary>The next gun comes to hand.</summary>
        public void Swap()
        {
            if (!CanSwap) return;
            _held[_gun].gameObject.SetActive(false);
            _gun = (_gun + 1) % _held.Count;
            _held[_gun].gameObject.SetActive(true);
            _pop = 0.55f; _popSpeed = 0f; _kick = 0f; _kickSpeed = 0f;
            _nextShot = Time.unscaledTime + 0.12f;
            Swapped?.Invoke();
        }

        /// <summary>The next kind of ammo is loaded, for both guns (a dart or a ball in the air flies on as it is).</summary>
        public void Reload()
        {
            if (!CanReload) return;
            _kind = (_kind + 1) % _kinds.Count;
            _pop = 0.8f; _popSpeed = 0f;
            _nextShot = Mathf.Max(_nextShot, Time.unscaledTime + 0.12f);
            Reloaded?.Invoke();
        }

        /// <summary>Fires a dart at a place of the menu (metres from its middle), at the slab <paramref name="on"/> if
        /// there is one there. <paramref name="landed"/> is told when it lands (a dart whose slab leaves the screen first
        /// never lands). <paramref name="chooses"/>: it is the dart a choice waits on. False when the gun cannot fire yet.</summary>
        public bool Fire(Vector2 at, MenuModel.Control on, bool chooses, Action landed)
        {
            if (!HasGun || Time.unscaledTime < _nextShot || (chooses && Choosing)) return false;
            Gun gun = guns[Index];
            _nextShot = Time.unscaledTime + gun.interval;
            if (gun.spread > 0f && !chooses) at += UnityEngine.Random.insideUnitCircle * gun.spread;
            if (on != null && !on.Shown) on = null;
            if (!chooses) on = _model.ControlAt(at);          // a dart that only plays lands on whatever is there
            if (on != null)
            {
                Vector2 half = on.Size / (2f * _model.PixelsPerMetre), from = at - _model.Middle(on);
                at = new Vector2(Mathf.Clamp(from.x, -half.x, half.x), Mathf.Clamp(from.y, -half.y, half.y));   // on its face
            }
            Pose(out Vector3 place, out Quaternion turn);
            int kind = _kind;
            Ammo row = RowOf(kind);
            Transform body = _spare[kind].Count > 0 ? _spare[kind].Pop() : row == null ? NewDart() : NewBody(row);
            body.gameObject.SetActive(true);
            var dart = new Dart { Body = body, From = place + turn * _muzzles[_gun] * _model.Scale, Fired = Time.unscaledTime, On = on, At = at, Knock = gun.knock, Landed = landed, Kind = kind, Colour = Color.white };
            if (row != null)
            {
                if (row.colours != null && row.colours.Length > 0) dart.Colour = row.colours[UnityEngine.Random.Range(0, row.colours.Length)];
                if (row.shellColours != null && row.shellColours.Length > 0) Wear(body, Unlike(row, dart.Colour));
            }
            dart.Flight = Mathf.Clamp((Target(dart) - dart.From).magnitude / (_model.Scale * dartSpeed), flightSeconds.x, Mathf.Max(flightSeconds.x, flightSeconds.y));
            _darts.Add(dart);
            if (chooses) { Choosing = true; Action then = dart.Landed; dart.Landed = () => { Choosing = false; then?.Invoke(); }; }
            _kickSpeed += gun.kick * 60f;
            if (row != null && row.fireClips != null && row.fireClips.Length > 0) Play(row.fireClips, row.fireVolume, row.pitch.x, row.pitch.y);
            else Play(gun.fireClips, gun.volume, 1.05f, 1.25f);
            Fly(dart);
            return true;
        }

        private Transform NewDart()
        {
            GameObject dart = Instantiate(dartModel, transform);
            dart.name = "MenuDart";
            foreach (Collider c in dart.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (MonoBehaviour b in dart.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            _model.Flatten(dart);
            return dart.transform;
        }

        /// <summary>A body for a kind of ammo: its model with nothing that flies or hits, drawn the model's flat way.</summary>
        private Transform NewBody(Ammo row)
        {
            GameObject body = Instantiate(row.model, transform);
            body.name = row.model.name;
            foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Rigidbody r in body.GetComponentsInChildren<Rigidbody>(true)) { r.isKinematic = true; r.detectCollisions = false; }
            foreach (MonoBehaviour b in body.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            _model.Flatten(body);
            var looks = new List<Renderer>();
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true)) if (r.enabled) looks.Add(r);
            var shell = new Shell { Looks = looks.ToArray(), Slots = new int[looks.Count] };
            for (int i = 0; i < looks.Count; i++) shell.Slots[i] = looks[i].sharedMaterials.Length;
            _shells[body.transform] = shell;
            return body.transform;
        }

        /// <summary>Dresses a body in its shell colour (every part, every slot: unlit, as the model draws).</summary>
        private void Wear(Transform body, Color shell)
        {
            if (!_shells.TryGetValue(body, out Shell parts)) return;
            if (_shellLook == null) _shellLook = new MaterialPropertyBlock();
            shell.a = 1f;
            for (int k = 0; k < parts.Looks.Length; k++)
            {
                Renderer r = parts.Looks[k];
                for (int i = 0; i < parts.Slots[k]; i++)
                {
                    r.GetPropertyBlock(_shellLook, i);      // what Flatten gave it (its picture), with the shell's colour over it
                    _shellLook.SetColor(ShellColour, shell);
                    r.SetPropertyBlock(_shellLook, i);
                }
            }
        }

        private static readonly int ShellColour = Shader.PropertyToID("_BaseColor");

        /// <summary>A shell colour that is not the paint's hue, picked as the game's paint ball picks its own: a hue well
        /// away from it, or hardly any hue at all; each fitting shell as likely as the next.</summary>
        private static Color Unlike(Ammo row, Color fill)
        {
            Color.RGBToHSV(fill, out float fillHue, out _, out _);
            int choices = 0; Color pick = row.shellFallback;
            foreach (Color shell in row.shellColours)
            {
                Color.RGBToHSV(shell, out float hue, out float saturation, out _);
                float apart = Mathf.Abs(hue - fillHue);
                if (saturation >= row.shellPlainBelow && Mathf.Min(apart, 1f - apart) <= row.shellHueApart) continue;
                choices++;
                if (UnityEngine.Random.Range(0, choices) == 0) pick = shell;
            }
            return pick;
        }

        /// <summary>One of a list of pictures, at random; null when it holds none.</summary>
        private static Texture2D Pick(Texture2D[] pictures)
        {
            if (pictures == null || pictures.Length == 0) return null;
            int start = UnityEngine.Random.Range(0, pictures.Length);
            for (int i = 0; i < pictures.Length; i++) { Texture2D p = pictures[(start + i) % pictures.Length]; if (p != null) return p; }
            return null;
        }

        /// <summary>Where a dart is going, in the world, now (its slab may have moved).</summary>
        private Vector3 Target(Dart dart) => dart.On != null ? _model.FacePoint(dart.On, dart.At) : _model.PlanePoint(dart.At);

        /// <summary>Where the gun is held and how it is turned, without its kick.</summary>
        private void Pose(out Vector3 place, out Quaternion turn)
        {
            float deep = Mathf.Clamp01(_model.Depth);
            place = _model.ViewPoint(heldAt, Mathf.Lerp(flatInFront, deepInFront, deep));
            Vector3 target = AimOn != null && AimOn.Shown ? _model.FacePoint(AimOn, Vector2.zero) + (_model.PlanePoint(Aim) - _model.PlanePoint(_model.Middle(AimOn))) : _model.PlanePoint(Aim);
            Vector3 way = target - place;
            if (way.sqrMagnitude < 1e-6f) way = -_model.Toward;
            way.Normalize();
            // Its top: flat, across the barrel within the picture (the gun is seen from its side); deep, up.
            Vector3 across = Vector3.Cross(-_model.Toward, way);
            if (across.sqrMagnitude < 1e-6f) across = _model.Up;
            across.Normalize();
            if (Vector3.Dot(across, _model.Up) < 0f) across = -across;
            turn = Quaternion.LookRotation(way, Vector3.Slerp(across, _model.Up, deep));
        }

        private void LateUpdate()
        {
            if (!HasGun || !_model.Showing) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Gun gun = guns[Index];
            if (Trigger && gun.auto) Fire(Aim, AimOn, false, null);

            _kickSpeed += (-kickSpringiness * _kick - kickSettle * _kickSpeed) * dt; _kick += _kickSpeed * dt;
            _popSpeed += (-kickSpringiness * (_pop - 1f) - kickSettle * _popSpeed) * dt; _pop += _popSpeed * dt;
            Pose(out Vector3 place, out Quaternion turn);
            Transform held = _held[_gun];
            held.SetPositionAndRotation(place - turn * Vector3.forward * (_kick * _model.Scale), turn * Quaternion.Euler(-_kick * 180f, 0f, 0f));
            held.localScale = Vector3.one * Mathf.Max(0.01f, _pop);

            float now = Time.unscaledTime;
            int stuck = 0;
            for (int i = _darts.Count - 1; i >= 0; i--)
            {
                Dart dart = _darts[i];
                if (dart.On != null && !dart.On.Shown) { Drop(i); continue; }      // its slab left the screen
                if (dart.StuckAt < 0f) { Fly(dart); if (dart.Burst) Drop(i); continue; }
                if (dart.Burst) { Drop(i); continue; }
                stuck++;
                if (dart.GoingAt < 0f && (now - dart.StuckAt > stuckSeconds || stuck > mostStuck)) dart.GoingAt = now;
                float size = dart.GoingAt < 0f ? 1f : 1f - (now - dart.GoingAt) / goSeconds;
                if (size <= 0f) { Drop(i); continue; }
                Stick(dart, place, size);
            }
        }

        /// <summary>A dart in flight: along its line, and on landing it knocks its slab and tells who waits on it.</summary>
        private void Fly(Dart dart)
        {
            Vector3 target = Target(dart);
            float gone = (Time.unscaledTime - dart.Fired) / dart.Flight;
            Vector3 way = target - dart.From;
            float size = BodySize(dart.Kind);
            if (gone < 1f)
            {
                dart.Body.SetPositionAndRotation(Vector3.Lerp(dart.From, target, gone) - way.normalized * (TipAhead(dart.Kind) * size * _model.Scale), Quaternion.LookRotation(way.sqrMagnitude > 1e-6f ? way : -_model.Toward, _model.Up));
                dart.Body.localScale = Vector3.one * size;
                return;
            }
            dart.StuckAt = Time.unscaledTime;
            if (dart.On != null) _model.Knock(dart.On, dart.At, dart.Knock);
            Ammo row = RowOf(dart.Kind);
            if (row != null && row.landClips != null && row.landClips.Length > 0) Play(row.landClips, row.landVolume, row.pitch.x, row.pitch.y);
            else Play(landClips, landVolume, 0.9f, 1.15f);
            Action landed = dart.Landed; dart.Landed = null;
            if (row != null && row.bursts)
            {
                // It bursts: a splat where it landed (on a title letter, a cast of its paint instead), and it is gone.
                if (_splats != null) _splats.Land(dart.On, dart.At, row.splatSize, dart.Colour, Pick(row.pictures));
                else if (dart.On != null) _model.Splash(dart.On, dart.Colour);
                dart.Burst = true;
                dart.Body.gameObject.SetActive(false);
                landed?.Invoke();
                return;
            }
            Pose(out Vector3 place, out Quaternion _);
            Stick(dart, place, 1f);
            landed?.Invoke();
        }

        /// <summary>A stuck dart: its tip a little into what it hit, lying the way the gun would shoot it now.</summary>
        private void Stick(Dart dart, Vector3 gunAt, float size)
        {
            float deep = Mathf.Clamp01(_model.Depth);
            Vector3 target = Target(dart);
            Vector3 way = target - gunAt;
            if (way.sqrMagnitude < 1e-6f) way = -_model.Toward;
            way.Normalize();
            float scale = BodySize(dart.Kind) * size;
            dart.Body.SetPositionAndRotation(target + way * ((sink - TipAhead(dart.Kind) * scale) * _model.Scale) + _model.Toward * (flatLift * (1f - deep) * _model.Scale), Quaternion.LookRotation(way, _model.Up));
            dart.Body.localScale = Vector3.one * Mathf.Max(0.001f, scale);
        }

        private void Drop(int i)
        {
            Dart dart = _darts[i];
            _darts.RemoveAt(i);
            if (dart.Landed != null && Choosing && dart.StuckAt < 0f) Choosing = false;   // the dart a choice waited on is gone: the choice is off
            dart.Body.gameObject.SetActive(false);
            _spare[dart.Kind].Push(dart.Body);
        }

        private void Play(AudioClip[] clips, float volume, float lowPitch, float highPitch)
        {
            if (_sound == null || clips == null || clips.Length == 0 || volume <= 0f) return;
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip == null) return;
            _sound.pitch = UnityEngine.Random.Range(lowPitch, highPitch);
            _sound.PlayOneShot(clip, volume);
        }
    }
}
