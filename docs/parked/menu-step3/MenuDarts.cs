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
    /// </summary>
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

        [SerializeField, Tooltip("The guns, the first one in hand at the start. Swap goes round them.")] private Gun[] guns = new Gun[0];
        [Header("Where the gun is held")]
        [SerializeField, Tooltip("Its place in the view: 0, 0 is the bottom left corner, 1, 1 the top right.")] private Vector2 heldAt = new Vector2(0.86f, 0.2f);
        [SerializeField, Min(0f), Tooltip("While flat it is this far in front of the menu, metres.")] private float flatInFront = 0.08f;
        [SerializeField, Min(0f), Tooltip("While deep, this far toward the viewer, metres.")] private float deepInFront = 0.95f;
        [SerializeField, Min(1f), Tooltip("How quickly it comes back from a kick (higher = snappier).")] private float kickSpringiness = 320f;
        [SerializeField, Min(0f), Tooltip("How quickly that settles.")] private float kickSettle = 22f;
        [Header("Its darts")]
        [SerializeField, Tooltip("A dart's model, pointing along +Z. None = no gun at all.")] private GameObject dartModel;
        [SerializeField, Min(0.01f), Tooltip("How many times its own size a dart is drawn.")] private float dartSize = 1.3f;
        [SerializeField, Min(0.1f), Tooltip("How fast a dart flies, metres a second.")] private float dartSpeed = 11f;
        [SerializeField, Tooltip("A flight never takes less or more than this, seconds (a choice must not feel late).")] private Vector2 flightSeconds = new Vector2(0.07f, 0.2f);
        [SerializeField, Min(0f), Tooltip("From a dart's middle to its tip, metres at its own size.")] private float tipAhead = 0.18f;
        [SerializeField, Min(0f), Tooltip("How far its tip sinks into what it hits, metres.")] private float sink = 0.03f;
        [SerializeField, Min(0f), Tooltip("While flat a stuck dart lies this far above the slab, metres, so it is drawn over it.")] private float flatLift = 0.06f;
        [SerializeField, Min(1), Tooltip("The most darts stuck at once: the oldest goes for a new one.")] private int mostStuck = 40;
        [SerializeField, Min(0.1f), Tooltip("Seconds a stuck dart stays.")] private float stuckSeconds = 10f;
        [SerializeField, Min(0.01f), Tooltip("Seconds a dart takes to shrink away.")] private float goSeconds = 0.25f;
        [SerializeField, Tooltip("One of these plays where a dart lands. None = silent.")] private AudioClip[] landClips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float landVolume = 0.45f;

        private sealed class Dart
        {
            public Transform Body;
            public Vector3 From;
            public float Fired, Flight, StuckAt = -1f, GoingAt = -1f;
            public MenuModel.Control On;        // the slab it is aimed at or in; null = the wall behind
            public Vector2 At;                  // where: on a slab, metres from its middle; else metres from the menu's middle
            public float Knock;
            public Action Landed;
        }

        private MenuModel _model;
        private readonly List<Transform> _held = new List<Transform>();     // a holder for each gun, its barrel along +Z
        private readonly List<Vector3> _muzzles = new List<Vector3>();      // each gun's muzzle, in its holder
        private readonly List<Dart> _darts = new List<Dart>();
        private readonly Stack<Transform> _spare = new Stack<Transform>();
        private AudioSource _sound;
        private int _gun;
        private float _nextShot, _kick, _kickSpeed, _pop = 1f, _popSpeed;
        private bool _made;

        /// <summary>The gun was swapped.</summary>
        public event Action Swapped;
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

        private void Awake() { _model = GetComponent<MenuModel>(); }

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
                Vector3 tip = muzzle != null ? holder.InverseTransformPoint(muzzle.position) : new Vector3(0f, 0f, box.extents.z);
                _model.Flatten(shape);
                holder.gameObject.SetActive(false);
                _held.Add(holder); _muzzles.Add(tip); _usable.Add(i);
            }
            if (_held.Count == 0) return;
            _held[0].gameObject.SetActive(true);
            _sound = gameObject.AddComponent<AudioSource>();
            _sound.playOnAwake = false; _sound.spatialBlend = 0f; _sound.ignoreListenerPause = true;
        }

        private static Transform Find(Transform under, string name)
        {
            foreach (Transform t in under.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
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
            Transform body = _spare.Count > 0 ? _spare.Pop() : NewDart();
            body.gameObject.SetActive(true);
            var dart = new Dart { Body = body, From = place + turn * _muzzles[_gun] * _model.Scale, Fired = Time.unscaledTime, On = on, At = at, Knock = gun.knock, Landed = landed };
            dart.Flight = Mathf.Clamp((Target(dart) - dart.From).magnitude / (_model.Scale * dartSpeed), flightSeconds.x, Mathf.Max(flightSeconds.x, flightSeconds.y));
            _darts.Add(dart);
            if (chooses) { Choosing = true; Action then = dart.Landed; dart.Landed = () => { Choosing = false; then?.Invoke(); }; }
            _kickSpeed += gun.kick * 60f;
            Play(gun.fireClips, gun.volume, 1.05f, 1.25f);
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
                if (dart.StuckAt < 0f) { Fly(dart); continue; }
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
            if (gone < 1f)
            {
                dart.Body.SetPositionAndRotation(Vector3.Lerp(dart.From, target, gone) - way.normalized * (tipAhead * dartSize * _model.Scale), Quaternion.LookRotation(way.sqrMagnitude > 1e-6f ? way : -_model.Toward, _model.Up));
                dart.Body.localScale = Vector3.one * dartSize;
                return;
            }
            dart.StuckAt = Time.unscaledTime;
            if (dart.On != null) _model.Knock(dart.On, dart.At, dart.Knock);
            Play(landClips, landVolume, 0.9f, 1.15f);
            Action landed = dart.Landed; dart.Landed = null;
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
            float scale = dartSize * size;
            dart.Body.SetPositionAndRotation(target + way * ((sink - tipAhead * scale) * _model.Scale) + _model.Toward * (flatLift * (1f - deep) * _model.Scale), Quaternion.LookRotation(way, _model.Up));
            dart.Body.localScale = Vector3.one * Mathf.Max(0.001f, scale);
        }

        private void Drop(int i)
        {
            Dart dart = _darts[i];
            _darts.RemoveAt(i);
            if (dart.Landed != null && Choosing && dart.StuckAt < 0f) Choosing = false;   // the dart a choice waited on is gone: the choice is off
            dart.Body.gameObject.SetActive(false);
            _spare.Push(dart.Body);
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
