using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// One of a Flat Zone's five letters to find (D-0208, after DKC's K-O-N-G): S, H, I, F or T, his title's own letter
    /// (the baked shape Art/Title/Letters/Title_X, a box for every pixel of his title), standing in the stretch at a size
    /// a toy reads, drawn with his title sheet (its frames stepped through, so it shimmers as the title does), spinning
    /// slowly and bobbing.
    /// - When the toy the zone holds touches it (its trigger, on this object: it stays still while the look spins), it
    ///   pops (a scale punch and a flash), plays its sound and is counted: saved per zone (the zone's Id) and per save
    ///   slot (SaveGame's seen list, so Continue keeps it and New Game clears it), and the HUD's strip lights it.
    /// - A letter already found shows as a ghost (see-through, faint): still there to see, not to take again.
    /// - When the last letter of the zone is found the zone gives its reward, once per slot (FlatZone.LetterFound).
    /// - D-0230: when it is taken it bursts into its pixel cubes (LetterBurst, with Artificer) and its ghost shows at once;
    ///   with BuildFx > Letter Burst On off (or nothing to cut) it pops as before.
    /// - It is SEEN only during the flat run (D-0212): its look is drawn while its zone holds a local player (and while
    ///   it pops), so a player walking the stretch in 3D sees no letters hanging in the hallway.
    /// Only a player the zone is holding takes it (side-on, at toy size, in Play): anyone else runs through it. The
    /// look is a child ("Look", its mesh and renderer); this script sizes it from the shape at start (Height), centres
    /// it on this object, and sizes the trigger to it.
    /// OFF SWITCHES: untick the letter's object: that letter is gone, and the zone's reward counts the letters that are on.
    /// Untick Only While Held: the letter is drawn all the time again (before D-0212).
    /// Shift Jam > Flat > Land Hallway Scroller places the hallway's five under FlatZone_Hall.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FlatLetter : MonoBehaviour
    {
        /// <summary>The word the letters spell, in order: a letter's Index is its place in it.</summary>
        public const string Word = "SHIFT";

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor"), BaseMap = Shader.PropertyToID("_BaseMap"), BaseMapPlace = Shader.PropertyToID("_BaseMap_ST");
        private static readonly List<FlatLetter> Live = new List<FlatLetter>();

        [SerializeField, Tooltip("The zone this letter belongs to (its saves and its reward). Found on a parent if empty.")] private FlatZone zone;
        [SerializeField, Range(0, 4), Tooltip("Its place in S-H-I-F-T: 0 = S, 1 = H, 2 = I, 3 = F, 4 = T. Each zone has one of each.")] private int index;

        [Header("Its look")]
        [SerializeField, Tooltip("The renderer that draws the letter's shape (the child Look). Found under this object if empty.")] private Renderer look;
        [SerializeField, Min(0.1f), Tooltip("How tall the letter stands, world units (1.8 = as tall as the toy). The shape is scaled to it and the trigger follows.")] private float height = 1.8f;
        [SerializeField, Min(0.1f), Tooltip("How deep the trigger reaches across the lane, world units: the held body is kept on the lane, so a little either side of it is enough.")] private float touchDepth = 2f;
        [SerializeField, Tooltip("His title sheet (64 frames side by side). The shape's boxes point at their own pixels of its first frame.")] private Texture2D sheet;
        [SerializeField, Min(1), Tooltip("How many frames the sheet holds side by side.")] private int frames = 64;
        [SerializeField, Min(0f), Tooltip("Frames a second (his animation runs at 16). 0 = the first frame only.")] private float framesPerSecond = 16f;
        [SerializeField, Range(0f, 1f), Tooltip("How dark the letter's sides are, as a share of its face (the menu title's is 0.62).")] private float sideShade = 0.62f;
        [SerializeField, Tooltip("The letter's material while it is to be found (unlit, so his colours stay his). Slot 0 of the shape is its faces, slot 1 its sides.")] private Material solid;
        [SerializeField, Tooltip("The ghost's material once it is found (unlit and see-through).")] private Material ghost;
        [SerializeField, Tooltip("The ghost's colour over the sheet; its alpha is how see-through it is (0 = gone, 1 = solid).")] private Color ghostColour = new Color(0.8f, 0.9f, 1f, 0.22f);
        [SerializeField, Tooltip("Drawn only while its zone holds a local player (the flat run) and while it pops (D-0212). Off: drawn all the time.")] private bool onlyWhileHeld = true;

        [Header("Its motion")]
        [SerializeField, Tooltip("Degrees a second it turns about its up. 0 = it faces the camera and stays still.")] private float spinDegreesPerSecond = 60f;
        [SerializeField, Min(0f), Tooltip("How far it bobs up and down, world units (the trigger does not bob).")] private float bob = 0.15f;
        [SerializeField, Min(0.1f), Tooltip("Seconds for one bob.")] private float bobSeconds = 2.4f;

        [Header("Taken")]
        [SerializeField, Min(0.01f), Tooltip("Seconds the pop lasts (the punch and the flash), before it turns to its ghost.")] private float popSeconds = 0.45f;
        [SerializeField, Min(0f), Tooltip("How much bigger it is at the pop's peak (0.6 = 60 % bigger).")] private float popScale = 0.6f;
        [SerializeField, Min(0f), Tooltip("How much brighter it is at the moment it is taken (1.5 = two and a half times as bright: the bloom catches it).")] private float flash = 1.5f;
        [SerializeField, Min(0f), Tooltip("Degrees a second it spins faster while it pops.")] private float popSpin = 720f;
        [SerializeField, Tooltip("Plays when it is taken (the last letter plays the zone's reward sound instead). No clip = silent.")] private CourseSounds.Cue sound = new CourseSounds.Cue(null, 0.5f);
        [SerializeField, Tooltip("Raised when a player takes it.")] private UnityEvent onTaken;

        private BoxCollider _touch;
        private MaterialPropertyBlock _block;
        private Material[] _solidSlots, _ghostSlots;
        private Transform _lookT;
        private Vector3 _lookRest;     // the look's place under this object: the shape's middle on this object's middle
        private float _lookScale = 1f;
        private float _turn;           // degrees turned so far (its spin)
        private float _popAt = float.NegativeInfinity;
        private string _key;
        private bool _found, _ghostShown, _shownOnce;
        private int _shownFrame = -1;
        private float _shownGlow = -1f;

        /// <summary>Its place in S-H-I-F-T (0 = S).</summary>
        public int Index => Mathf.Clamp(index, 0, Word.Length - 1);
        /// <summary>Its zone, or null (then it does nothing).</summary>
        public FlatZone Zone => zone;
        /// <summary>True when the active save slot has found it.</summary>
        public bool Found => _found;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Live.Clear();

        /// <summary>The save's name for letter <paramref name="letter"/> of the zone named <paramref name="zoneId"/> (a
        /// moment in SaveGame's seen list: "flat:FlatZone_Hall:0" is the hallway's S).</summary>
        public static string KeyOf(string zoneId, int letter) => "flat:" + Clean(zoneId) + ":" + letter;
        /// <summary>The save's name for the zone's reward (all its letters found).</summary>
        public static string RewardKeyOf(string zoneId) => "flat:" + Clean(zoneId) + ":all";
        private static string Clean(string id) => (id ?? "").Replace("|", "/");   // the seen list is split on "|"

        /// <summary>The letters of <paramref name="of"/> that are switched on: <paramref name="present"/> has bit i set
        /// for each letter whose Index is i, <paramref name="found"/> for each of those the active slot has found. No search:
        /// the letters keep a list of themselves.</summary>
        public static void Mask(FlatZone of, out int present, out int found)
        {
            present = 0; found = 0;
            if (of == null) return;
            for (int i = 0; i < Live.Count; i++)
            {
                FlatLetter l = Live[i];
                if (!ReferenceEquals(l.zone, of)) continue;
                int bit = 1 << l.Index;
                present |= bit;
                if (l._found) found |= bit;
            }
        }

        private void Awake()
        {
            if (zone == null) zone = GetComponentInParent<FlatZone>();
            if (zone == null) Debug.LogWarning("FlatLetter '" + name + "' has no Flat Zone above it: it does nothing. Put it under a FlatZone's object.", this);
            _block = new MaterialPropertyBlock();
            Fit();
            int slots = look != null ? Mathf.Max(1, look.sharedMaterials.Length) : 1;
            _solidSlots = new Material[slots]; _ghostSlots = new Material[slots];
            for (int i = 0; i < slots; i++) { _solidSlots[i] = solid; _ghostSlots[i] = ghost != null ? ghost : solid; }
            if (look != null) { _lookT = look.transform; _lookRest = _lookT.localPosition; _lookScale = _lookT.localScale.x; }
        }

        private void OnEnable()
        {
            if (onlyWhileHeld && look != null) look.enabled = false;   // LateUpdate shows it while the zone holds a player
            Live.Add(this);
            SaveGame.Changed += Read;
            _popAt = float.NegativeInfinity;
            Read();
        }

        private void OnDisable()
        {
            Live.Remove(this);
            SaveGame.Changed -= Read;
        }

        /// <summary>Sizes the look to Height (the shape's own size is read from its mesh), puts the shape's middle on this
        /// object's middle, and makes the trigger the letter's size (Touch Depth across the lane). The game does this at
        /// start; the maker calls it so the letter stands in the editor as it will in Play.</summary>
        public void Fit()
        {
            if (look == null) look = GetComponentInChildren<Renderer>(true);
            _touch = GetComponent<BoxCollider>();
            _touch.isTrigger = true;
            Mesh shape = look != null && look.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (shape == null) { _touch.center = Vector3.zero; _touch.size = new Vector3(height, height, touchDepth); return; }
            Bounds b = shape.bounds;
            float scale = height / Mathf.Max(0.001f, b.size.y);
            Transform t = look.transform;
            if (t != transform)
            {
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one * scale;
                t.localPosition = -b.center * scale;
            }
            _touch.center = Vector3.zero;
            _touch.size = new Vector3(b.size.x * scale, height, touchDepth);
        }

        /// <summary>What the active slot says about this letter, and the look to match (the solid letter or its ghost).
        /// Asked at start and whenever the saves change (SaveGame.Changed): another slot, New Game, a letter taken.</summary>
        private void Read()
        {
            if (zone != null) _key ??= KeyOf(zone.Id, Index);
            _found = _key != null && SaveGame.Seen(SaveGame.Slot, _key);
            if (!Popping) ShowGhost(_found);
        }

        private bool Popping => Time.time - _popAt < popSeconds;

        /// <summary>The solid letter (false) or its ghost (true) onto the renderer; the frame is drawn again.</summary>
        private void ShowGhost(bool on)
        {
            if (look == null) return;
            if (_shownOnce && _ghostShown == on) return;
            _shownOnce = true; _ghostShown = on;
            if (solid != null) look.sharedMaterials = on ? _ghostSlots : _solidSlots;
            _shownFrame = -1;
        }

        private void OnTriggerEnter(Collider other) => Touched(other);

        /// <summary>Also while the body stays in it: a toy that came in while it was not held (first person, mid-shift)
        /// takes it as soon as the zone holds it again. Nothing is asked once it is found.</summary>
        private void OnTriggerStay(Collider other) { if (!_found) Touched(other); }

        private void Touched(Collider other)
        {
            if (_found || zone == null || !isActiveAndEnabled) return;
            Shifter who = PlayerTouch.Find(other, out _);
            if (who == null || !zone.Holds(who)) return;   // not a player, or not one the zone holds (first person, a kid): nothing
            Take(who.GetComponent<PlayerMotor>());
        }

        /// <summary>Taken by <paramref name="by"/>: the pop starts, it is saved for the active slot, the zone hears of it
        /// (the HUD's strip, and the reward when it was the last), and its sound plays unless the reward's does.</summary>
        private void Take(PlayerMotor by)
        {
            _popAt = Time.time;
            _found = true;
            if (_key != null) SaveGame.MarkSeen(SaveGame.Slot, _key);   // raises SaveGame.Changed: Read keeps the solid look while it pops
            // D-0230: a copy in the solid look bursts into its cubes, and the letter goes to its ghost at once (no pop).
            if (LetterBurst.Play(look, by != null ? by.gameObject.layer : -1)) _popAt = float.NegativeInfinity;
            bool rewarded = zone.LetterFound(this, by);
            if (!rewarded) UiAudio.Play(sound);
            onTaken?.Invoke();
        }

        /// <summary>The spin, the bob and the pop on the look; the sheet's frame and the brightness onto its slots when
        /// either changes (nothing is made: the block and the slot lists were made at start).</summary>
        private void LateUpdate()
        {
            if (look == null || _solidSlots == null) return;
            bool show = !onlyWhileHeld || Popping || (zone != null && zone.AnyInside);
            if (look.enabled != show) { look.enabled = show; _shownFrame = -1; }   // shown again: its frame is laid on afresh
            if (!show) return;   // not drawn: nothing to turn or lay on
            float since = Time.time - _popAt;
            float pop = since < popSeconds ? 1f - since / popSeconds : 0f;   // 1 at the moment it is taken, 0 when done
            if (pop <= 0f && _found && !_ghostShown) ShowGhost(true);
            _turn = Mathf.Repeat(_turn + (spinDegreesPerSecond + popSpin * pop) * Time.deltaTime, 360f);
            if (_lookT != null && _lookT != transform)
            {
                float punch = 1f + popScale * Mathf.Sin(Mathf.PI * Mathf.Clamp01(since / popSeconds)) * (pop > 0f ? 1f : 0f);
                float s = _lookScale * punch;
                Quaternion turn = Quaternion.AngleAxis(_turn, Vector3.up);
                float lift = bob * Mathf.Sin(Time.time * (2f * Mathf.PI / bobSeconds));
                // the shape's middle stays on this object's middle as it turns and swells
                _lookT.localRotation = turn;
                _lookT.localScale = Vector3.one * s;
                _lookT.localPosition = turn * (_lookRest * (s / Mathf.Max(0.0001f, _lookScale))) + Vector3.up * lift;
            }
            int frame = framesPerSecond > 0f ? (int)(Time.timeAsDouble * framesPerSecond % Mathf.Max(1, frames)) : 0;
            float glow = 1f + flash * pop;
            if (frame == _shownFrame && Mathf.Approximately(glow, _shownGlow)) return;
            _shownFrame = frame; _shownGlow = glow;
            Color tint = _ghostShown ? ghostColour : Color.white;
            for (int slot = 0; slot < _solidSlots.Length; slot++)   // the slot count read at start (asking the renderer makes a list)
            {
                float bright = (slot == 0 ? 1f : sideShade) * glow;
                _block.Clear();
                if (sheet != null) _block.SetTexture(BaseMap, sheet);
                _block.SetVector(BaseMapPlace, new Vector4(1f, 1f, (float)frame / Mathf.Max(1, frames), 0f));
                _block.SetColor(BaseColour, new Color(tint.r * bright, tint.g * bright, tint.b * bright, tint.a));
                look.SetPropertyBlock(_block, slot);
            }
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = _found ? new Color(0.6f, 0.8f, 1f, 0.35f) : new Color(1f, 0.45f, 0.8f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
