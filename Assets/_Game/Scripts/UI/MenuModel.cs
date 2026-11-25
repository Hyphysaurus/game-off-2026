using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// A menu's look as a 3D model (D-0196, pages and the shift D-0197): the slabs of his Blender menu, drawn over the
    /// game by a camera of their own. The model is only looked at; whoever owns the menu (TitleMenu, through a
    /// MenuDriver) says what a control reads, whether it can be used and what choosing it does.
    /// On a prefab under Resources (Titles/TitleMenuModel): this component beside a copy of the model file.
    /// Read BY NAME: a CONTROL is any object with a child called its own name + "_Frame" (the plate behind it); its
    /// words go where the child "Label_" + its name is; a PAGE is an object called "Page_..." and holds its controls
    /// (one page is shown at a time; a control under no page is always shown); "Anchor_..." marks a place for
    /// something the owner draws (the title, the hint). Re-export the model under the same names and nothing here
    /// changes. Without the prefab, or with a control the owner needs missing, the owner draws its menu itself.
    /// How it is seen: the model stands far below the world, enlarged, on the UI layer, and an overlay camera on the
    /// game camera's stack looks at its front (up and the viewer's side are read from where two controls and a label
    /// are, not from the file's axes). FLAT (2D), that camera is far off with a very narrow lens and every slab is
    /// pressed thin: the menu looks drawn. DEEP (3D, the shift), the camera comes in as its lens widens, so the menu
    /// keeps its size on the screen while its depth appears, and the view leans a little with the pointer. A metre of
    /// model is Pixels Per Metre on a 1080-high screen in both, at the model's own plane.
    /// Every control is a soft toy (D-0199): Knock squashes and tips it, Brush stirs it, Hold and Release pull it on an
    /// elastic and let it fly home; its neighbours feel a knock too. Knocked tells whoever listens that one was hit.
    /// A plaything (AddToy, D-0200) is all of that and never a button: a thing of no page, on show on every page, that
    /// is not selected, chosen or painted (the title's letters, MenuTitle).
    /// Paint (D-0201): Splash tells whoever listens (Splashed) that a paint ball burst on a control, and in what colour;
    /// Tipped and Stretch say how a slab is turned and stretched now, so a splat laid on its face can ride it (MenuSplats).
    /// Menus beyond the model's four pages (D-0204: Pause, Options, Parties): AddSlab COPIES a slab the model has (its
    /// body, its frame, its label place) onto a page of any name, at a place of the menu, under a name of its own; the
    /// copy is a whole control (pages, springs, the shift, keys, darts, paint, frame colours) and is measured by the
    /// same code as the model's own. The places are rows of Made Controls on the prefab; MakePage makes a page's rows,
    /// once. A control can be left off its page (SetHidden), can be a value that left and right step (SetSteps), and
    /// can show how far a value is with a bar on its face (SetFill). A second model up at once (the pause menu's while
    /// the title's stands hidden in the scene) stands Next Place further along, out of the first one's camera.
    /// Everything moves on real time: a menu holds the game's.
    /// </summary>
    public sealed class MenuModel : MonoBehaviour
    {
        public const string PrefabPath = "Titles/TitleMenuModel";

        public enum State { Normal, Selected, Active, Disabled }

        /// <summary>A control drawn with another's slab (see Drawn Like).</summary>
        [System.Serializable] private struct Twin { public string control, sameAs; }

        /// <summary>A control made in code by copying a slab of the model (D-0204): see Made Controls.</summary>
        [System.Serializable]
        public struct Made
        {
            [Tooltip("Its name: the menu that uses it asks for it by this name.")] public string control;
            [Tooltip("The control of the model whose slab it copies (Btn_NewGame is the plain wide button, Slot_2 the long save slot, Btn_SavesBack the small back button).")] public string sameAs;
            [Tooltip("The page it is on (\"Pause\" for Page_Pause; a page the model file does not have is made).")] public string page;
            [Tooltip("Its middle, metres across and up from the menu's middle (a metre is Pixels Per Metre canvas pixels at 1080 high).")] public Vector2 at;
            public Made(string control, string sameAs, string page, float x, float y) { this.control = control; this.sameAs = sameAs; this.page = page; at = new Vector2(x, y); }
        }

        /// <summary>One control of the model: its slab, its frame, its words, and where it is on the screen when flat.</summary>
        public sealed class Control
        {
            public string Name, Page;
            public Transform Body;
            public Renderer BodyRenderer, FrameRenderer;
            public Color[] BodyColours;
            public Vector3 RestScale, RestPlace, LabelPlace;
            public Quaternion RestTurn;
            public Bounds Box;              // its slab's own box, in the slab's space
            public int DepthAxis, AcrossAxis, UpAxis;   // which of the slab's own axes run toward the viewer, across the screen and up it
            public TextMeshPro Words;
            /// <summary>Its middle and its size on the screen while flat, canvas pixels from the middle of a 1080-high screen.</summary>
            public Vector2 Centre, Size;
            public bool Usable = true, Active, Pressed, Shown;
            /// <summary>A plaything, not a button (the title's letters, D-0200): of no page, on show on every page; it takes
            /// the brush, a pull, knocks and darts like a slab and is never selected, chosen or painted.</summary>
            public bool Toy;
            /// <summary>A value (D-0204): left and right step it, and a choice on its left or right half steps it down or up.</summary>
            public bool Steps;
            /// <summary>Left off its page (SetHidden): not shown, not picked, passed over by the keys.</summary>
            public bool Hidden;
            /// <summary>Made in code from another control's slab (AddSlab), not in the model file.</summary>
            public bool Copy;
            internal Transform Bar;                  // its fill bar (SetFill): a holder on its face, the track and the fill in it
            internal Renderer BarTrack, BarFill;
            internal float BarLength;                // the track's length, world units
            internal bool Stays;     // of no page and already on show: a page change leaves it as it is
            internal float Grow = 1f, GrowSpeed, Drop, DropSpeed, Squash, SquashSpeed, Wait;
            // A soft toy (D-0199): knocked or let go of, it swings about its place and settles. Metres and radians.
            internal Vector3 Off, OffSpeed;          // across, up, toward the viewer
            internal Vector3 Lean, LeanSpeed;        // tipped about the up line, about the across line, rolled
            internal Vector2 Pull;                   // held by the pointer: how far it is pulled, metres
            internal bool Held;
        }

        [Header("The model on the screen")]
        [SerializeField, Tooltip("An unlit material; every slab is drawn with it, in its own colours. Without one the menu draws itself the old way.")] private Material slabMaterial;
        [SerializeField, Min(1f), Tooltip("Screen pixels a metre of the model takes on a 1080-high screen (the model was made at 600: a 3.2 by 1.8 m frame is 1920 by 1080).")] private float pixelsPerMetre = 600f;
        [SerializeField, Tooltip("The frame the model was laid out in, metres: its height fills the screen's.")] private Vector2 frameMetres = new Vector2(3.2f, 1.8f);
        [SerializeField, Tooltip("The room the model leaves for the title at Anchor_Title, metres.")] private Vector2 titleMetres = new Vector2(1.5f, 0.7f);   // 900 by 420: his 75 by 35 title at 12 screen pixels a pixel
        [SerializeField, Tooltip("The dark wash over the game behind the slabs.")] private Color wash = new Color(0.03f, 0.03f, 0.06f, 0.72f);
        [Header("Flat and deep (the shift, D-0197)")]
        [SerializeField, Range(1f, 20f), Tooltip("The lens while flat, degrees: narrow, from far off, so nothing shows a side.")] private float flatLens = 4f;
        [SerializeField, Range(20f, 80f), Tooltip("The lens while deep, degrees.")] private float deepLens = 42f;
        [SerializeField, Range(0.01f, 1f), Tooltip("How thick a slab is while flat, as a share of its own thickness.")] private float flatThickness = 0.08f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the shift takes.")] private float shiftSeconds = 0.45f;
        [SerializeField, Tooltip("How far the deep view leans with the pointer, metres, across and up.")] private Vector2 lean = new Vector2(0.16f, 0.09f);
        [SerializeField, Tooltip("While deep the camera sits this much lower, metres (the menu is looked up at a little).")] private float deepDrop = 0.12f;
        [Header("A control's frame, by its state (the first that is true: disabled, selected, active)")]
        [SerializeField] private Color normalFrame = new Color32(0x0a, 0x08, 0x12, 0xff);
        [SerializeField] private Color selectedFrame = new Color32(0x73, 0xf1, 0xff, 0xff);
        [SerializeField] private Color activeFrame = new Color32(0xff, 0x72, 0xcc, 0xff);
        [SerializeField] private Color disabledFrame = new Color32(0x0a, 0x08, 0x12, 0xff);
        [Header("A control's slab")]
        [SerializeField, Min(0f), Tooltip("Its colours are this much brighter while selected.")] private float selectedBright = 1.45f;
        [SerializeField, Range(0f, 1f), Tooltip("And this much of themselves while disabled.")] private float disabledBright = 0.4f;
        [SerializeField, Min(0.5f), Tooltip("Its size while selected.")] private float selectedSize = 1.04f;
        [SerializeField, Min(0.5f), Tooltip("Its size while pressed.")] private float pressedSize = 0.95f;
        [SerializeField, Min(0f), Tooltip("While deep a selected slab comes this far toward the viewer, metres.")] private float selectedLift = 0.03f;
        [SerializeField, Min(1f), Tooltip("How quickly a slab takes its size (higher = snappier).")] private float springiness = 260f;
        [SerializeField, Min(0f), Tooltip("How quickly that settles (higher = less bounce).")] private float settle = 16f;
        [SerializeField, Min(0f), Tooltip("A page's slabs drop in from this far above, metres, one after another.")] private float pageDrop = 0.22f;
        [SerializeField, Min(0f), Tooltip("Seconds between one slab of a page starting to drop in and the next.")] private float pageStagger = 0.04f;
        [Header("A control as a soft toy (D-0199)")]
        [SerializeField, Min(1f), Tooltip("How quickly a knocked or let-go slab swings back (higher = snappier).")] private float toySpringiness = 240f;
        [SerializeField, Min(0f), Tooltip("How quickly that settles (higher = less wobble).")] private float toySettle = 13f;
        [SerializeField, Min(0.01f), Tooltip("The furthest a held slab can be pulled, metres: it stretches toward this and no further.")] private float pullReach = 0.32f;
        [SerializeField, Min(0f), Tooltip("A held slab is this much longer for each metre it is pulled along itself.")] private float pullStretch = 1.6f;
        [SerializeField, Min(0f), Tooltip("How hard a knock squashes, tips and shoves a slab (1 = a dart's).")] private float knockPower = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("How much of a knock its neighbours on the page feel.")] private float neighbourShare = 0.3f;
        [SerializeField, Min(0f), Tooltip("How much the pointer brushing over a slab stirs it.")] private float brushPower = 1f;
        [SerializeField, Range(0f, 45f), Tooltip("The most a slab tips, degrees (deep only: flat, a slab does not tip).")] private float mostTip = 16f;
        [Header("Its sounds (each: a clip, its loudness, how much its pitch varies; no clip = silent)")]
        [SerializeField, Tooltip("A pulled slab or letter let go.")] private CourseSounds.Cue letGoSound;
        [SerializeField, Tooltip("The shift from flat to deep (2D to 3D).")] private CourseSounds.Cue shiftSound;
        [SerializeField, Tooltip("The shift back to flat.")] private CourseSounds.Cue shiftBackSound;
        [SerializeField, Tooltip("A page's slabs dropping in (not the first page shown).")] private CourseSounds.Cue pageSound;
        [Header("Its words")]
        [SerializeField, Tooltip("The words' font. Empty = TextMesh Pro's own.")] private TMP_FontAsset font;
        [SerializeField] private Color wordColour = new Color32(0xff, 0xf0, 0xfc, 0xff);
        [SerializeField, Range(0.1f, 1f), Tooltip("How much of a control's height its words may take.")] private float wordsShare = 0.72f;
        [SerializeField, Range(10f, 100f), Tooltip("The second line's size, percent of the first.")] private float subSize = 52f;
        [SerializeField, Tooltip("The second line's colour, as a hex word TextMesh Pro reads.")] private string subColour = "#CFC3F2";
        [SerializeField, Min(0f), Tooltip("Clear space kept between the words and the control's sides, metres.")] private float textMargin = 0.03f;
        [Header("Reading the model (two controls, one straight above the other, say which way is up; the upper one's label says where the viewer stands)")]
        [SerializeField] private string upperControl = "Btn_Continue";
        [SerializeField] private string lowerControl = "Btn_Quit";
        [Header("Controls the model shows in a state of its own")]
        [SerializeField, Tooltip("A control whose slab was made already looking disabled or selected is drawn with the slab of another control of the same size. Empty when every control is made in its plain look (the paged model's are).")]
        private Twin[] drawnLike = new Twin[0];
        [Header("Where it stands (out of the game's sight)")]
        [SerializeField, Tooltip("Far from the world, so the game's own camera never has it in view.")] private Vector3 place = new Vector3(0f, -5000f, 0f);
        [SerializeField, Min(1f), Tooltip("How many times larger than made it stands there: that far from the middle of the world, a slab's millimetres would shimmer at their own size.")] private float enlarge = 100f;
        [SerializeField, Tooltip("A second model up at once (the pause menu's, while the title's stands hidden in the scene) stands this much further along, world units, and a third twice as far: out of the other's camera (its view is about 800 units across at its furthest).")] private Vector3 nextPlace = new Vector3(3000f, 0f, 0f);
        [Header("Controls made in code (D-0204): copies of the model's own slabs, for the menus beyond its four pages")]
        [SerializeField, Tooltip("Each row is one control: its name, the slab it copies, its page and its middle (metres). A menu whose rows are missing here, or whose slab is not in the model, draws itself the old way. Pause: the pause menu's own copy of this prefab. Options: on the title's model and the pause menu's. Parties: on the title's.")]
        private Made[] madeControls =
        {
            new Made("Pause_Resume", "Btn_NewGame", "Pause", 0f, 0.24f),
            new Made("Pause_Options", "Btn_NewGame", "Pause", 0f, 0.08f),
            new Made("Pause_Title", "Btn_NewGame", "Pause", 0f, -0.08f),
            new Made("Pause_Quit", "Btn_NewGame", "Pause", 0f, -0.24f),
            new Made("Opt_Look", "Btn_NewGame", "Options", 0f, 0.30f),
            new Made("Opt_Invert", "Btn_NewGame", "Options", 0f, 0.14f),
            new Made("Opt_Volume", "Btn_NewGame", "Options", 0f, -0.02f),
            new Made("Opt_Fullscreen", "Btn_NewGame", "Options", 0f, -0.18f),
            new Made("Opt_Graphics", "Btn_NewGame", "Options", 0f, -0.34f),
            new Made("Opt_Body", "Btn_NewGame", "Options", 0f, -0.50f),
            new Made("Opt_Back", "Btn_SavesBack", "Options", 0f, -0.68f),
            new Made("Party_1", "Slot_2", "Parties", 0f, 0.30f),
            new Made("Party_2", "Slot_2", "Parties", 0f, 0.14f),
            new Made("Party_3", "Slot_2", "Parties", 0f, -0.02f),
            new Made("Parties_Refresh", "Btn_NewGame", "Parties", -0.54f, -0.20f),
            new Made("Parties_Host", "Btn_NewGame", "Parties", 0.54f, -0.20f),
            new Made("Parties_Code", "Btn_NewGame", "Parties", -0.54f, -0.36f),
            new Made("Parties_House", "Btn_NewGame", "Parties", 0.54f, -0.36f),
            new Made("Parties_More", "Btn_SavesBack", "Parties", -0.27f, -0.54f),
            new Made("Parties_Back", "Btn_SavesBack", "Parties", 0.27f, -0.54f),
        };
        [Header("A value's fill bar (SetFill: Options' look speed, volume, body)")]
        [SerializeField, Tooltip("The part filled, as far as the value goes.")] private Color fillColour = new Color32(0xff, 0x72, 0xcc, 0xff);
        [SerializeField, Tooltip("The track under it, the whole way.")] private Color trackColour = new Color32(0x1c, 0x16, 0x2c, 0xff);
        [SerializeField, Range(0.02f, 0.5f), Tooltip("Its height, as a share of the slab's face (it runs along the bottom, under the words).")] private float fillHeight = 0.14f;
        [SerializeField, Min(0f), Tooltip("Clear space between it and the face's ends, metres.")] private float fillEnds = 0.03f;
        [SerializeField, Min(0f), Tooltip("Clear space between it and the face's bottom edge, metres.")] private float fillBottom = 0.006f;
        [SerializeField, Min(0.0001f), Tooltip("It lies this far in front of the face, metres: behind the words (which are 0.002 in front).")] private float fillLift = 0.001f;

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor"), BaseMap = Shader.PropertyToID("_BaseMap"), Surface = Shader.PropertyToID("_Surface");

        private readonly Dictionary<string, Control> _controls = new Dictionary<string, Control>();
        private readonly Dictionary<string, Transform> _named = new Dictionary<string, Transform>();
        private readonly List<Control> _shown = new List<Control>();
        private Camera _view, _under;
        private Canvas _washCanvas;
        private GameObject _wash;
        private Vector3 _origin, _right, _up, _look;
        private MaterialPropertyBlock _paint;
        private float _depth, _depthSpeed;
        private bool _read, _paged, _deep;
        private Mesh _barQuad;          // the fill bars' square, made with the first bar
        private int _slot;              // which of the models up at once this is (0 = the first): it stands Next Place along that many times
        private static readonly List<MenuModel> Live = new List<MenuModel>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLive() => Live.Clear();
        private const string WordsName = "Words", BarName = "Fill";   // the children this part puts on a slab (a copy leaves its template's behind)
        // Reading order: top to bottom, and left to right within a row (half a pixel apart counts as the same row).
        private static readonly System.Comparison<Control> ReadingOrder = (a, b) => Mathf.Abs(b.Centre.y - a.Centre.y) > 0.5f ? b.Centre.y.CompareTo(a.Centre.y) : a.Centre.x.CompareTo(b.Centre.x);

        /// <summary>A control was knocked (by a dart, a tap, or being let go after a pull): which one, and how hard.</summary>
        public event System.Action<Control, float> Knocked;
        /// <summary>A paint ball burst on a control (D-0201): which one, and the paint's colour.</summary>
        public event System.Action<Control, Color> Splashed;

        /// <summary>The room left for the title, canvas pixels.</summary>
        public Vector2 TitleBox => titleMetres * pixelsPerMetre;
        /// <summary>The page on show ("Main" for the object Page_Main).</summary>
        public string Page { get; private set; } = "";
        /// <summary>The controls on show, top to bottom.</summary>
        public IReadOnlyList<Control> Shown => _shown;
        /// <summary>The control the owner's pointer or keys are on; null for none.</summary>
        public Control Selected { get; set; }
        /// <summary>The shift: false = flat (2D), true = deep (3D).</summary>
        public bool Deep
        {
            get => _deep;
            set { if (value == _deep) return; _deep = value; UiAudio.Play(value ? shiftSound : shiftBackSound); }
        }
        /// <summary>How deep it is now, 0 flat to 1 deep.</summary>
        public float Depth => _depth;
        /// <summary>Where the pointer is on the screen, -1 to 1 across and up: the deep view leans with it.</summary>
        public Vector2 Pointer { get; set; }

        /// <summary>
        /// The model over <paramref name="under"/> (the game's camera), or null when it cannot be: no prefab, no
        /// material on it, no camera to stack on, or one of <paramref name="needed"/> is not in the model. Null is not
        /// an error: the menu then draws itself.
        /// </summary>
        public static MenuModel Spawn(Camera under, IReadOnlyList<string> needed)
        {
            var prefab = Resources.Load<MenuModel>(PrefabPath);
            if (prefab == null || under == null) return null;
            UniversalAdditionalCameraData game = under.GetUniversalAdditionalCameraData();
            if (game == null || game.renderType != CameraRenderType.Base) return null;
            MenuModel model = Instantiate(prefab);
            model.name = "MenuModel";
            // Its place among the models up now: the first free one (a model gone frees its place).
            Live.RemoveAll(m => m == null);
            int slot = 0;
            for (bool taken = true; taken; )
            {
                taken = false;
                foreach (MenuModel m in Live) if (m._slot == slot) { taken = true; slot++; break; }
            }
            model._slot = slot;
            Live.Add(model);
            string missing = model.Read(needed);
            if (missing == null) missing = model.Stage(under, game);
            if (missing == null) return model;
            Debug.LogWarning("MenuModel: the menu draws itself the old way (" + missing + ").", prefab);
            Destroy(model.gameObject);
            return null;
        }

        /// <summary>Reads the model: its controls, pages and places. Null when it is whole; else what is missing. It makes
        /// no camera and shows nothing (an editor check can call it on the prefab's own contents).</summary>
        public string Read(IReadOnlyList<string> needed)
        {
            if (_read) return null;
            if (slabMaterial == null) return "no Slab Material on the prefab";
            int layer = LayerMask.NameToLayer("UI");
            if (layer < 0) return "no UI layer";
            transform.SetPositionAndRotation(place + nextPlace * _slot, transform.rotation);
            transform.localScale *= enlarge;
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = layer; _named[t.name] = t; }
            _paint = new MaterialPropertyBlock();

            // Which way is up and where the viewer stands: from the controls themselves.
            if (!_named.TryGetValue(upperControl, out Transform upper) || !_named.TryGetValue(lowerControl, out Transform lower)) return "no " + upperControl + " or " + lowerControl;
            if (!_named.TryGetValue("Label_" + upperControl, out Transform label)) return "no Label_" + upperControl;
            Vector3 toViewer = label.position - upper.position;
            Vector3 up = upper.position - lower.position;
            if (toViewer.sqrMagnitude < 1e-10f || up.sqrMagnitude < 1e-10f) return "its controls and labels do not say where its front is";
            _look = -toViewer.normalized;
            up = Vector3.ProjectOnPlane(up, _look).normalized;
            if (up.sqrMagnitude < 0.5f) return "its controls do not say which way is up";
            _origin = transform.position; _up = up; _right = Vector3.Cross(up, _look);

            // A control made in a state of its own takes its twin's slab, if the two are the same size.
            foreach (Twin twin in drawnLike)
            {
                if (string.IsNullOrEmpty(twin.control) || !_named.TryGetValue(twin.control, out Transform odd) || !_named.TryGetValue(twin.sameAs ?? "", out Transform plain)) continue;
                if (!odd.TryGetComponent(out MeshFilter oddMesh) || !plain.TryGetComponent(out MeshFilter plainMesh) || oddMesh.sharedMesh == null || plainMesh.sharedMesh == null) continue;
                if (!odd.TryGetComponent(out Renderer oddLook) || !plain.TryGetComponent(out Renderer plainLook)) continue;
                if ((oddMesh.sharedMesh.bounds.size - plainMesh.sharedMesh.bounds.size).magnitude > 0.001f) continue;
                oddMesh.sharedMesh = plainMesh.sharedMesh;
                oddLook.sharedMaterials = plainLook.sharedMaterials;
            }

            // Its controls.
            foreach (KeyValuePair<string, Transform> pair in _named)
            {
                if (!_named.TryGetValue(pair.Key + "_Frame", out Transform frame) || frame.parent != pair.Value) continue;
                if (!pair.Value.TryGetComponent(out MeshFilter mesh) || mesh.sharedMesh == null || !pair.Value.TryGetComponent(out Renderer body)) continue;
                Transform slab = pair.Value;
                var c = new Control { Name = pair.Key, Body = slab, BodyRenderer = body, RestScale = slab.localScale, RestPlace = slab.position, RestTurn = slab.rotation, Box = mesh.sharedMesh.bounds };
                for (Transform p = slab.parent; p != null && p != transform; p = p.parent) if (p.name.StartsWith("Page_")) { c.Page = p.name.Substring(5); break; }
                if (c.Page == null) c.Page = "";
                frame.TryGetComponent(out c.FrameRenderer);
                c.BodyColours = Unlit(body);
                if (c.FrameRenderer != null) Unlit(c.FrameRenderer);
                Measure(c);
                c.LabelPlace = _named.TryGetValue("Label_" + pair.Key, out Transform text) ? text.position : slab.position - _look * (0.02f * enlarge);
                _controls[pair.Key] = c;
            }
            if (needed != null) foreach (string name in needed) if (!_controls.ContainsKey(name)) return "the model has no control called " + name;
            _read = true;
            return null;
        }

        /// <summary>Reads off a control's own shape which of its axes run toward the viewer, across the screen and up it, and
        /// where it is on the screen while flat (its middle and its size). The same for a slab and for a plaything.</summary>
        private void Measure(Control c)
        {
            Transform slab = c.Body;
            float most = -1f, mostAcross = -1f, mostUp = -1f;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 way = axis == 0 ? slab.right : axis == 1 ? slab.up : slab.forward;
                float along = Mathf.Abs(Vector3.Dot(way, _look)), across = Mathf.Abs(Vector3.Dot(way, _right)), upward = Mathf.Abs(Vector3.Dot(way, _up));
                if (along > most) { most = along; c.DepthAxis = axis; }
                if (across > mostAcross) { mostAcross = across; c.AcrossAxis = axis; }
                if (upward > mostUp) { mostUp = upward; c.UpAxis = axis; }
            }
            Vector2 low = new Vector2(float.MaxValue, float.MaxValue), high = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c.Box.center + Vector3.Scale(c.Box.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector2 at = OnScreen(slab.TransformPoint(corner));
                low = Vector2.Min(low, at); high = Vector2.Max(high, at);
            }
            c.Centre = (low + high) * 0.5f; c.Size = high - low;
        }

        /// <summary>The camera that draws it over the game, and the dark wash behind the slabs (a canvas on that camera,
        /// further off). Null when done.</summary>
        private string Stage(Camera under, UniversalAdditionalCameraData game)
        {
            int layer = gameObject.layer;
            var eye = new GameObject("MenuModelCamera");
            eye.transform.SetParent(transform, true);
            _view = eye.AddComponent<Camera>();
            _view.orthographic = false;
            _view.cullingMask = 1 << layer;
            _view.useOcclusionCulling = false;
            UniversalAdditionalCameraData mine = _view.GetUniversalAdditionalCameraData();
            mine.renderType = CameraRenderType.Overlay;
            mine.renderPostProcessing = false;
            mine.renderShadows = false;
            _under = under;
            game.cameraStack.Add(_view);

            _wash = new GameObject("MenuModelWash", typeof(RectTransform));
            _wash.layer = layer;
            _washCanvas = _wash.AddComponent<Canvas>();
            _washCanvas.renderMode = RenderMode.ScreenSpaceCamera; _washCanvas.worldCamera = _view;
            _washCanvas.sortingOrder = -1;   // the back of everything: drawn before any see-through thing of the model (the paint's splats, D-0201; the words)
            var fill = new GameObject("Wash", typeof(RectTransform));
            fill.layer = layer;
            var rt = (RectTransform)fill.transform;
            rt.SetParent(_wash.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var image = fill.AddComponent<Image>(); image.color = wash; image.raycastTarget = false;
            foreach (Control c in _controls.Values) c.Body.gameObject.SetActive(c.Page.Length == 0);
            Look();
            return null;
        }

        /// <summary>Draws a renderer unlit; returns the colours its materials had, slot by slot.</summary>
        private Color[] Unlit(Renderer r)
        {
            Material[] had = r.sharedMaterials;
            var colours = new Color[had.Length];
            var plain = new Material[had.Length];
            for (int i = 0; i < had.Length; i++)
            {
                colours[i] = had[i] == null ? Color.magenta : had[i].HasProperty(BaseColour) ? had[i].GetColor(BaseColour) : had[i].color;
                plain[i] = slabMaterial;
            }
            r.sharedMaterials = plain;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return colours;
        }

        /// <summary>A place of the model on the screen while flat: canvas pixels from the middle of a 1080-high screen.</summary>
        private Vector2 OnScreen(Vector3 world)
        {
            Vector3 from = world - _origin;
            return new Vector2(Vector3.Dot(from, _right), Vector3.Dot(from, _up)) * (pixelsPerMetre / enlarge);
        }

        /// <summary>The control of this name, if the model has it.</summary>
        public bool TryGet(string control, out Control found) => _controls.TryGetValue(control, out found);

        /// <summary>Where the object of this name is on the screen while flat (an Anchor_...). False if the model has none.</summary>
        public bool Point(string anchor, out Vector2 at)
        {
            at = Vector2.zero;
            if (!_named.TryGetValue(anchor, out Transform t) || t == null) return false;
            at = OnScreen(t.position);
            return true;
        }

        /// <summary>Shows one page ("Main" for Page_Main) and the controls under no page; its slabs drop in one after
        /// another. The first control that can be used is selected.</summary>
        public void ShowPage(string page)
        {
            bool first = !_paged;
            _paged = true;
            Page = page ?? "";
            _shown.Clear();
            foreach (Control c in _controls.Values)
            {
                bool on = !c.Hidden && (c.Page.Length == 0 || c.Page == Page);
                c.Stays = on && c.Shown && c.Page.Length == 0;       // of no page and already here: it stays as it is
                c.Shown = on; c.Pressed = false;
                c.Body.gameObject.SetActive(on);
                if (on) _shown.Add(c);
            }
            _shown.Sort(ReadingOrder);
            int arriving = 0;
            foreach (Control c in _shown)
            {
                if (c.Stays) continue;
                c.Drop = pageDrop; c.DropSpeed = 0f; c.Wait = arriving++ * pageStagger; c.Grow = 1f; c.GrowSpeed = 0f;
                c.Off = c.OffSpeed = c.Lean = c.LeanSpeed = Vector3.zero; c.Pull = Vector2.zero; c.Held = false; c.Squash = c.SquashSpeed = 0f;
            }
            Selected = null;
            foreach (Control c in _shown) if (c.Usable) { Selected = c; break; }
            if (!first) UiAudio.Play(pageSound);
        }

        /// <summary>Adds a plaything to the model (the title's letters): a thing of no page that is on show on every page,
        /// takes the pointer's brush, a pull, knocks and darts like a slab, and is never selected, chosen or painted.
        /// <paramref name="body"/> stands under the model where it rests; <paramref name="box"/> is its shape's box in its
        /// own space. Null before the model is read, or when a control of that name is there already.</summary>
        public Control AddToy(string name, Transform body, Bounds box)
        {
            if (!_read || body == null || string.IsNullOrEmpty(name) || _controls.ContainsKey(name)) return null;
            var c = new Control
            {
                Name = name, Page = "", Body = body, Box = box, Toy = true, Usable = false,
                RestScale = body.localScale, RestPlace = body.position, RestTurn = body.rotation, LabelPlace = body.position, BodyColours = new Color[0],
            };
            Measure(c);
            _controls[name] = c;
            if (_paged)      // a page is already up: it joins it now, dropping in (before the first page, that page brings it in)
            {
                c.Shown = true; c.Drop = pageDrop;
                _shown.Add(c);
                _shown.Sort(ReadingOrder);
            }
            return c;
        }

        // ---- controls made in code (D-0204): menus beyond the model's four pages need no Blender export

        /// <summary>
        /// Copies the slab of the control <paramref name="sameAs"/> (its body, its frame and its label place) onto the
        /// page <paramref name="page"/>, its middle at <paramref name="at"/> (metres across and up from the menu's middle),
        /// as a control called <paramref name="name"/>. The copy is a whole control: it shows with its page, takes the
        /// keys, the pointer, knocks, darts and paint, and its frame shows its state; it is measured by the same code as
        /// the model's own slabs. Its colours are its template's. A page the model has none of is made (beside the
        /// template's own page, standing as it does: every page of the model is an empty at its middle). A copy made
        /// while its page is on show drops in at once. Copies are kept: asked again for a name it made on that page, it
        /// gives that copy back. Null before the model is read, when another control has the name, or when the model has
        /// no control <paramref name="sameAs"/> (a plaything is not a slab). It makes no camera (an editor check may call it).
        /// </summary>
        public Control AddSlab(string name, string sameAs, string page, Vector2 at)
        {
            if (!_read || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(page)) return null;
            if (_controls.TryGetValue(name, out Control made)) return made.Copy && made.Page == page ? made : null;
            if (string.IsNullOrEmpty(sameAs) || !_controls.TryGetValue(sameAs, out Control like) || like.Toy) return null;
            GameObject copy = Instantiate(like.Body.gameObject, PageFor(page, like), false);
            copy.name = name;
            Transform body = copy.transform, frame = null, label = null;
            for (int i = body.childCount - 1; i >= 0; i--)
            {
                Transform child = body.GetChild(i);
                // What this part put on the template (its words, its fill bar) stays the template's: the copy makes its own.
                if (child.name == WordsName || child.name == BarName) { child.gameObject.SetActive(false); Gone(child.gameObject); continue; }
                if (child.name == like.Name + "_Frame") { child.name = name + "_Frame"; frame = child; }
                else if (child.name == "Label_" + like.Name) { child.name = "Label_" + name; label = child; }
            }
            // It rests where its template rests, moved across the menu's plane to its own place (the template may be
            // swinging or pressed thin right now: its rest is what counts).
            Vector2 move = at - like.Centre / pixelsPerMetre;
            Vector3 shift = (_right * move.x + _up * move.y) * enlarge;
            body.SetPositionAndRotation(like.RestPlace + shift, like.RestTurn);
            body.localScale = like.RestScale;
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
            var c = new Control
            {
                Name = name, Page = page, Body = body, Copy = true, Box = like.Box,
                RestScale = like.RestScale, RestPlace = body.position, RestTurn = like.RestTurn,
                BodyColours = (Color[])like.BodyColours.Clone(), LabelPlace = like.LabelPlace + shift,
            };
            body.TryGetComponent(out c.BodyRenderer);
            if (frame != null) frame.TryGetComponent(out c.FrameRenderer);
            Measure(c);
            _controls[name] = c;
            _named[name] = body;
            if (frame != null) _named[frame.name] = frame;
            if (label != null) _named[label.name] = label;
            bool now = _paged && page == Page;      // its page is up: it joins it now, dropping in
            copy.SetActive(now);
            if (now)
            {
                c.Shown = true; c.Drop = pageDrop;
                _shown.Add(c);
                _shown.Sort(ReadingOrder);
            }
            return c;
        }

        /// <summary>The object holding a page's controls: the model's own Page_..., or one made beside the template's.</summary>
        private Transform PageFor(string page, Control like)
        {
            if (_named.TryGetValue("Page_" + page, out Transform have) && have != null) return have;
            Transform holder = like.Body.parent;
            var made = new GameObject("Page_" + page).transform;
            made.gameObject.layer = gameObject.layer;
            if (holder == null || holder == transform) made.SetParent(transform, false);
            else
            {
                made.SetParent(holder.parent, false);
                made.localPosition = holder.localPosition; made.localRotation = holder.localRotation; made.localScale = holder.localScale;
            }
            _named[made.name] = made;
            return made;
        }

        /// <summary>Makes the rows of Made Controls that are on <paramref name="page"/> (once: a row made already is kept as
        /// it is). False, with what is wrong in <paramref name="missing"/>, when no row is on that page or a row's slab is
        /// not in the model: the menu that wanted the page then draws itself the old way.</summary>
        public bool MakePage(string page, out string missing)
        {
            missing = null;
            if (!_read) { missing = "the model is not read"; return false; }
            int rows = 0;
            foreach (Made row in madeControls)
            {
                if (row.page != page) continue;
                rows++;
                if (AddSlab(row.control, row.sameAs, row.page, row.at) != null) continue;
                missing = "Made Controls: " + row.control + " (a copy of " + row.sameAs + ") could not be made";
                return false;
            }
            if (rows == 0) { missing = "no row of Made Controls is on the page " + page; return false; }
            return true;
        }

        /// <summary>Leaves a control off its page (true) or puts it back (false). While its page is up it goes, or drops in,
        /// at once; else its page's next ShowPage sees to it. A control that goes takes the selection with it to the
        /// first one that can be used.</summary>
        public void SetHidden(string control, bool hidden)
        {
            if (!_controls.TryGetValue(control, out Control c) || c.Hidden == hidden) return;
            c.Hidden = hidden;
            if (!_paged || (c.Page.Length > 0 && c.Page != Page)) return;
            if (hidden)
            {
                c.Shown = false; c.Pressed = false; c.Held = false; c.Pull = Vector2.zero;
                c.Body.gameObject.SetActive(false);
                _shown.Remove(c);
                if (Selected == c) { Selected = null; foreach (Control s in _shown) if (s.Usable) { Selected = s; break; } }
                return;
            }
            c.Shown = true; c.Body.gameObject.SetActive(true);
            c.Drop = pageDrop; c.DropSpeed = 0f; c.Wait = 0f; c.Grow = 1f; c.GrowSpeed = 0f;
            c.Off = c.OffSpeed = c.Lean = c.LeanSpeed = Vector3.zero; c.Squash = c.SquashSpeed = 0f;
            _shown.Add(c);
            _shown.Sort(ReadingOrder);
            if (Selected == null && c.Usable) Selected = c;
        }

        /// <summary>Makes a control a value (true): the MenuDriver's left and right step it, and a choice on its left or
        /// right half steps it down or up, instead of choosing it.</summary>
        public void SetSteps(string control, bool steps) { if (_controls.TryGetValue(control, out Control c)) c.Steps = steps; }

        /// <summary>A bar along the bottom of a control's face, filled this share of the way (0 to 1): how far a value is.
        /// Below 0 = no bar. Made the first time it is asked for; it rides the slab as its words do.</summary>
        public void SetFill(string control, float share)
        {
            if (!_controls.TryGetValue(control, out Control c) || c.Toy) return;
            if (share < 0f) { if (c.Bar != null) c.Bar.gameObject.SetActive(false); return; }
            if (c.Bar == null && !MakeBar(c)) return;
            c.Bar.gameObject.SetActive(true);
            share = Mathf.Clamp01(share);
            Transform fill = c.BarFill.transform;
            fill.gameObject.SetActive(share > 0.001f);
            fill.localPosition = new Vector3(c.BarLength * share * 0.5f, 0f, fill.localPosition.z);
            fill.localScale = new Vector3(Mathf.Max(1e-4f, c.BarLength * share), fill.localScale.y, 1f);
        }

        /// <summary>A control's fill bar: a holder on its face (laid there with the slab at rest, then a child of it), the
        /// track the whole way and the fill a hair in front of it. False without a slab material.</summary>
        private bool MakeBar(Control c)
        {
            if (slabMaterial == null) return false;
            if (_barQuad == null) _barQuad = BarSquare();
            Transform b = c.Body;
            Vector3 wasAt = b.position, wasSize = b.localScale; Quaternion wasTurn = b.rotation;
            b.SetPositionAndRotation(c.RestPlace, c.RestTurn); b.localScale = c.RestScale;
            Vector2 face = c.Size / pixelsPerMetre;
            float length = Mathf.Max(0.01f, face.x - 2f * fillEnds), tall = Mathf.Max(0.002f, face.y * fillHeight);
            var holder = new GameObject(BarName).transform;
            holder.gameObject.layer = gameObject.layer;
            holder.SetParent(b, false);
            holder.SetPositionAndRotation(FacePoint(c, new Vector2(-face.x * 0.5f + fillEnds, -face.y * 0.5f + fillBottom + tall * 0.5f)) - _look * (fillLift * enlarge), Facing);
            // One of the holder's units is one of the world's, along each of its own axes (each runs along one of the slab's).
            Vector3 lossy = b.lossyScale, own = Vector3.one;
            for (int k = 0; k < 3; k++)
            {
                Vector3 inSlab = b.InverseTransformDirection(holder.rotation * (k == 0 ? Vector3.right : k == 1 ? Vector3.up : Vector3.forward));
                int m = Mathf.Abs(inSlab.x) >= Mathf.Abs(inSlab.y) && Mathf.Abs(inSlab.x) >= Mathf.Abs(inSlab.z) ? 0 : Mathf.Abs(inSlab.y) >= Mathf.Abs(inSlab.z) ? 1 : 2;
                own[k] = 1f / Mathf.Max(1e-6f, Mathf.Abs(lossy[m]));
            }
            holder.localScale = own;
            c.Bar = holder;
            c.BarLength = length * enlarge;
            c.BarTrack = BarPart("Track", holder, new Vector3(c.BarLength * 0.5f, 0f, 0f), new Vector3(c.BarLength, tall * enlarge, 1f));
            c.BarFill = BarPart("Filled", holder, new Vector3(0f, 0f, -0.5f * fillLift * enlarge), new Vector3(1e-4f, tall * enlarge, 1f));
            b.SetPositionAndRotation(wasAt, wasTurn); b.localScale = wasSize;
            return true;
        }

        private Renderer BarPart(string name, Transform holder, Vector3 at, Vector3 size)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(holder, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = _barQuad;
            var look = go.AddComponent<MeshRenderer>();
            look.sharedMaterial = slabMaterial;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.receiveShadows = false;
            look.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            look.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return look;
        }

        /// <summary>A square a unit wide, facing the viewer along its -z (as a splat's is).</summary>
        private static Mesh BarSquare()
        {
            var mesh = new Mesh { name = "MenuBarSquare" };
            mesh.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) });
            mesh.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Gone(Object thing) { if (Application.isPlaying) Destroy(thing); else DestroyImmediate(thing); }

        /// <summary>The game camera it is drawn over (null before it is on a screen).</summary>
        public Camera Under => _under;

        /// <summary>What a control reads: a line, and a smaller second line under it (null for none).</summary>
        public void SetWords(string control, string line, string second = null)
        {
            if (!_controls.TryGetValue(control, out Control c)) return;
            if (c.Words == null)
            {
                var go = new GameObject(WordsName);
                go.layer = gameObject.layer;
                go.transform.SetParent(c.Body, false);
                go.transform.SetPositionAndRotation(c.LabelPlace, Quaternion.LookRotation(_look, _up));
                Vector3 s = c.Body.lossyScale;   // the words' own units are the world's: the slab's size is taken out
                go.transform.localScale = new Vector3(1f / Mathf.Max(1e-6f, s.x), 1f / Mathf.Max(1e-6f, s.y), 1f / Mathf.Max(1e-6f, s.z));
                c.Words = go.AddComponent<TextMeshPro>();
                if (font != null) c.Words.font = font;
                c.Words.alignment = TextAlignmentOptions.Center;
                c.Words.textWrappingMode = TextWrappingModes.NoWrap;
                c.Words.richText = true;
                c.Words.enableAutoSizing = true; c.Words.fontSizeMin = 1f; c.Words.fontSizeMax = 2000f;
                c.Words.rectTransform.sizeDelta = new Vector2(Mathf.Max(0.01f, c.Size.x / pixelsPerMetre - 2f * textMargin), c.Size.y / pixelsPerMetre * wordsShare) * enlarge;
            }
            c.Words.text = string.IsNullOrEmpty(second) ? line : line + "\n<size=" + subSize.ToString("F0") + "%><color=" + subColour + ">" + second + "</color></size>";
        }

        /// <summary>Whether a control can be used (a slab that cannot is dim and is passed over).</summary>
        public void SetUsable(string control, bool usable) { if (_controls.TryGetValue(control, out Control c)) c.Usable = usable; }

        /// <summary>The owner's own mark on a control (the save slot in use): its frame says so.</summary>
        public void SetActive(string control, bool active) { if (_controls.TryGetValue(control, out Control c)) c.Active = active; }

        /// <summary>The control under a point of the screen, or null. Nearest first.</summary>
        public Control Pick(Vector2 screen)
        {
            if (_view == null || !_view.enabled) return null;
            Ray ray = _view.ScreenPointToRay(screen);
            Control best = null; float nearest = float.MaxValue;
            foreach (Control c in _shown)
            {
                Matrix4x4 inside = c.Body.worldToLocalMatrix;
                var local = new Ray(inside.MultiplyPoint3x4(ray.origin), inside.MultiplyVector(ray.direction));
                if (!c.Box.IntersectRay(local, out float along)) continue;
                float far = (c.Body.TransformPoint(local.GetPoint(along)) - ray.origin).sqrMagnitude;
                if (far < nearest) { nearest = far; best = c; }
            }
            return best;
        }

        /// <summary>Where a control is on the screen now, pixels (deep, it has moved from where it is flat).</summary>
        public Vector2 ScreenPoint(Control c) => _view != null && c != null ? (Vector2)_view.WorldToScreenPoint(c.Body.position) : Vector2.zero;

        /// <summary>On the screen or off it (another panel is over the menu, or the menu is shut).</summary>
        public void Show(bool on)
        {
            if (_view != null) _view.enabled = on;
            if (_wash != null) _wash.SetActive(on);
        }

        private void LateUpdate()
        {
            if (_view == null || !_view.enabled) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _depth = Mathf.SmoothDamp(_depth, Deep ? 1f : 0f, ref _depthSpeed, shiftSeconds * 0.5f, float.PositiveInfinity, dt);
            Look();
            float thin = Mathf.Lerp(flatThickness, 1f, Mathf.Clamp01(_depth));
            foreach (Control c in _shown)
            {
                bool selected = c == Selected && c.Usable;
                float want = !c.Usable ? 1f : c.Pressed && selected ? pressedSize : selected ? selectedSize : 1f;
                Spring(ref c.Grow, ref c.GrowSpeed, want, dt);
                if (c.Wait > 0f) c.Wait -= dt; else Spring(ref c.Drop, ref c.DropSpeed, 0f, dt);
                // The soft toy: its swing about its place, its tip and its squash settle on their own springs.
                float deep = Mathf.Clamp01(_depth);
                for (int k = 0; k < 3; k++)
                {
                    float off = c.Off[k], offSpeed = c.OffSpeed[k], lean = c.Lean[k], leanSpeed = c.LeanSpeed[k];
                    Toy(ref off, ref offSpeed, dt); Toy(ref lean, ref leanSpeed, dt);
                    c.Off[k] = off; c.OffSpeed[k] = offSpeed; c.Lean[k] = lean; c.LeanSpeed[k] = leanSpeed;
                }
                Toy(ref c.Squash, ref c.SquashSpeed, dt);
                Vector2 pull = c.Held ? Rubber(c.Pull) : Vector2.zero;
                Vector3 size = c.RestScale * c.Grow;
                size[c.AcrossAxis] *= (1f + c.Squash * 0.05f) * (1f + Mathf.Abs(pull.x) * pullStretch);
                size[c.UpAxis] *= (1f - c.Squash * 0.07f) * (1f + Mathf.Abs(pull.y) * pullStretch);
                size[c.DepthAxis] = c.RestScale[c.DepthAxis] * thin;
                c.Body.localScale = size;
                float tip = mostTip * Mathf.Deg2Rad;
                Quaternion turn = Quaternion.AngleAxis(Mathf.Clamp(c.Lean.x + pull.x * 0.9f, -tip, tip) * deep * Mathf.Rad2Deg, _up)
                    * Quaternion.AngleAxis(Mathf.Clamp(c.Lean.y, -tip, tip) * deep * Mathf.Rad2Deg, _right)
                    * Quaternion.AngleAxis(Mathf.Clamp(c.Lean.z - pull.x * 0.35f, -tip, tip) * Mathf.Rad2Deg, _look);
                c.Body.rotation = turn * c.RestTurn;
                c.Body.position = c.RestPlace + (_right * (c.Off.x + pull.x) + _up * (c.Off.y + pull.y + c.Drop)
                    - _look * (((selected ? selectedLift : 0f) + (c.Held ? selectedLift * 2f : 0f) + c.Off.z) * deep)) * enlarge;
                if (!c.Toy) Paint(c, !c.Usable ? State.Disabled : selected ? State.Selected : c.Active ? State.Active : State.Normal);
            }
        }

        /// <summary>A step of a soft toy's spring back to rest (Toy Springiness, Toy Settle).</summary>
        private void Toy(ref float at, ref float speed, float dt)
        {
            speed += (-toySpringiness * at - toySettle * speed) * dt;
            at += speed * dt;
        }

        /// <summary>A pull as the slab takes it: it follows the pointer near its place and stretches toward Pull Reach, never past.</summary>
        private Vector2 Rubber(Vector2 pull)
            => new Vector2(pullReach * (float)System.Math.Tanh(pull.x / pullReach), pullReach * (float)System.Math.Tanh(pull.y / pullReach));

        /// <summary>Knocks a control: it squashes, tips away from where it was hit and is shoved back, and its neighbours on
        /// the page feel a share of it. <paramref name="at"/>: where on the slab, metres from its middle (across, up).</summary>
        public void Knock(Control c, Vector2 at, float power = 1f)
        {
            if (c == null || !c.Shown) return;
            float p = power * knockPower, halfWide = Mathf.Max(0.01f, c.Size.x / pixelsPerMetre * 0.5f), halfTall = Mathf.Max(0.01f, c.Size.y / pixelsPerMetre * 0.5f);
            c.SquashSpeed -= 9f * p;
            c.LeanSpeed.x += Mathf.Clamp(at.x / halfWide, -1f, 1f) * 5f * p;
            c.LeanSpeed.y -= Mathf.Clamp(at.y / halfTall, -1f, 1f) * 3.5f * p;
            c.LeanSpeed.z -= Mathf.Clamp(at.x / halfWide, -1f, 1f) * 0.6f * p;
            c.OffSpeed.z -= 0.5f * p;
            int here = _shown.IndexOf(c);
            for (int step = -1; step <= 1; step += 2)
            {
                int n = here + step;
                if (n < 0 || n >= _shown.Count) continue;
                Control near = _shown[n];
                near.SquashSpeed -= 9f * p * neighbourShare;
                // One beside it (the title's letters stand in a row) is nudged sideways, away from the one hit; one above it is
                // nudged up, one below it down.
                bool beside = Mathf.Abs(near.Centre.y - c.Centre.y) < Mathf.Min(near.Size.y, c.Size.y) * 0.5f;
                if (beside) near.OffSpeed.x += Mathf.Sign(near.Centre.x - c.Centre.x) * 0.25f * p * neighbourShare;
                else near.OffSpeed.y -= step * 0.25f * p * neighbourShare;
            }
            Knocked?.Invoke(c, p);
        }

        /// <summary>The pointer brushed over a control: it is stirred the way the pointer went (a share of the screen a second).</summary>
        public void Brush(Control c, Vector2 pointerSpeed)
        {
            if (c == null || !c.Shown || c.Held) return;
            c.OffSpeed.x += pointerSpeed.x * 0.12f * brushPower;
            c.LeanSpeed.z -= pointerSpeed.x * 0.35f * brushPower;
            c.SquashSpeed -= 0.5f * brushPower;
        }

        /// <summary>Holds a control by the pointer, pulled this far from its place (metres across and up). Let go with Release.</summary>
        public void Hold(Control c, Vector2 pull)
        {
            if (c == null || !c.Shown) return;
            if (!c.Held) c.SquashSpeed -= 5f * knockPower;
            c.Held = true; c.Pull = pull;
        }

        /// <summary>Lets a held control go: it flies back to its place from where it was pulled to and wobbles there.</summary>
        public void Release(Control c)
        {
            if (c == null || !c.Held) return;
            Vector2 was = Rubber(c.Pull);
            c.Held = false; c.Pull = Vector2.zero;
            c.Off.x += was.x; c.Off.y += was.y;
            c.LeanSpeed.x += was.x * 14f; c.LeanSpeed.z -= was.x * 6f;
            UiAudio.Play(letGoSound);
            Knock(c, Vector2.zero, Mathf.Clamp(was.magnitude / pullReach, 0.2f, 1.2f));
        }

        // ---- for what plays on the model (its guns and darts, D-0199)
        /// <summary>The model has been read: its controls, pages and places are known (its camera may not be up yet).</summary>
        public bool IsRead => _read;
        /// <summary>The camera is up: the model is on a screen.</summary>
        public bool Staged => _view != null;
        /// <summary>It is being drawn now (false while another panel has the screen).</summary>
        public bool Showing => _view != null && _view.enabled;
        /// <summary>World units to a metre of the model.</summary>
        public float Scale => enlarge;
        public float PixelsPerMetre => pixelsPerMetre;
        /// <summary>The way toward the viewer, and up the screen, in the world.</summary>
        public Vector3 Toward => -_look;
        public Vector3 Up => _up;

        /// <summary>A place of the menu in the world: metres across and up from its middle, and toward the viewer.</summary>
        public Vector3 PlanePoint(Vector2 at, float toward = 0f) => _origin + (_right * at.x + _up * at.y - _look * toward) * enlarge;

        /// <summary>The place in the world, now, of a point on a control's face (metres across and up from its middle): the
        /// control may be swinging, tipped or pressed thin.</summary>
        public Vector3 FacePoint(Control c, Vector2 at)
        {
            Quaternion tipped = c.Body.rotation * Quaternion.Inverse(c.RestTurn);
            float half = c.Box.extents[c.DepthAxis] * Mathf.Abs(c.Body.lossyScale[c.DepthAxis]);
            return c.Body.TransformPoint(c.Box.center) + tipped * ((_right * at.x + _up * at.y) * enlarge - _look * half);
        }

        /// <summary>How a control is turned now from the way it rests (its tip and roll); none for null.</summary>
        public Quaternion Tipped(Control c) => c == null ? Quaternion.identity : c.Body.rotation * Quaternion.Inverse(c.RestTurn);

        /// <summary>How much longer a control is now than at rest, across and up (a squash, a pull, its size while selected
        /// or pressed); 1, 1 at rest. A thing laid on its face grows and moves with it.</summary>
        public Vector2 Stretch(Control c)
        {
            if (c == null) return Vector2.one;
            Vector3 now = c.Body.localScale;
            float across = Mathf.Abs(c.RestScale[c.AcrossAxis]) > 1e-6f ? now[c.AcrossAxis] / c.RestScale[c.AcrossAxis] : 1f;
            float up = Mathf.Abs(c.RestScale[c.UpAxis]) > 1e-6f ? now[c.UpAxis] / c.RestScale[c.UpAxis] : 1f;
            return new Vector2(Mathf.Abs(across), Mathf.Abs(up));
        }

        /// <summary>A control's face while flat and at rest, metres across and up.</summary>
        public Vector2 FaceSize(Control c) => c == null ? Vector2.zero : c.Size / pixelsPerMetre;

        /// <summary>The way a thing lying flat on the menu is turned at rest: its own z away from the viewer, its y up.</summary>
        public Quaternion Facing => Quaternion.LookRotation(_look, _up);

        /// <summary>Tells whoever listens that a paint ball burst on a control, in this colour (the title's letters take a
        /// cast of it, MenuTitle).</summary>
        public void Splash(Control c, Color colour)
        {
            if (c == null || !c.Shown) return;
            Splashed?.Invoke(c, colour);
        }

        /// <summary>The control on show whose place at rest holds a point of the menu (metres from its middle); null for none.</summary>
        public Control ControlAt(Vector2 at)
        {
            Vector2 pixels = at * pixelsPerMetre;
            foreach (Control c in _shown)
                if (Mathf.Abs(pixels.x - c.Centre.x) <= c.Size.x * 0.5f && Mathf.Abs(pixels.y - c.Centre.y) <= c.Size.y * 0.5f) return c;
            return null;
        }

        /// <summary>The place in the world seen at a point of the view (0, 0 its bottom left, 1, 1 its top right), this
        /// many metres in front of the menu: a thing put there stays at that place of the screen, flat or deep.</summary>
        public Vector3 ViewPoint(Vector2 view, float toward)
        {
            if (_view == null) return _origin;
            Ray ray = _view.ViewportPointToRay(new Vector3(view.x, view.y, 0f));
            float along = Vector3.Dot(ray.direction, _look);
            if (Mathf.Abs(along) < 1e-6f) return _origin;
            return ray.GetPoint(Vector3.Dot(_origin - _look * (toward * enlarge) - ray.origin, _look) / along);
        }

        /// <summary>Draws a thing the model's own flat way (unlit, each part in its material's colour and picture) on the
        /// model's layer: a gun or a dart beside the slabs looks the same under any light of the scene.</summary>
        public void Flatten(GameObject thing)
        {
            foreach (Transform t in thing.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
            foreach (Renderer r in thing.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) { r.enabled = false; continue; }
                Material[] had = r.sharedMaterials;
                // Glass and a sight's picture need a see-through material, which the slab's look is not: a part made of
                // nothing else is left out (a part with no material at all is kept: it is given the slab's).
                bool seeThrough = had.Length > 0;
                foreach (Material m in had) if (m == null || !m.HasProperty(Surface) || m.GetFloat(Surface) < 0.5f) { seeThrough = false; break; }
                if (seeThrough) { r.enabled = false; continue; }
                var plain = new Material[had.Length];
                for (int i = 0; i < had.Length; i++) plain[i] = slabMaterial;
                r.sharedMaterials = plain;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                for (int i = 0; i < had.Length; i++)
                {
                    if (had[i] == null) continue;
                    var block = new MaterialPropertyBlock();
                    Color colour = had[i].HasProperty(BaseColour) ? had[i].GetColor(BaseColour) : had[i].color; colour.a = 1f;
                    block.SetColor(BaseColour, colour);
                    Texture picture = had[i].HasProperty(BaseMap) ? had[i].GetTexture(BaseMap) : had[i].mainTexture;
                    if (picture != null) block.SetTexture(BaseMap, picture);
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        /// <summary>A control's middle on the model's own plane, metres across and up from the model's middle.</summary>
        public Vector2 Middle(Control c) => c == null ? Vector2.zero : c.Centre / pixelsPerMetre;

        /// <summary>Where a point of the screen is on the model's own plane, metres across and up from its middle (for a pull).</summary>
        public Vector2 OnPlane(Vector2 screen)
        {
            if (_view == null) return Vector2.zero;
            Ray ray = _view.ScreenPointToRay(screen);
            float toward = Vector3.Dot(ray.direction, _look);
            if (Mathf.Abs(toward) < 1e-6f) return Vector2.zero;
            Vector3 hit = ray.GetPoint(Vector3.Dot(_origin - ray.origin, _look) / toward) - _origin;
            return new Vector2(Vector3.Dot(hit, _right), Vector3.Dot(hit, _up)) / enlarge;
        }

        /// <summary>A step of a spring toward <paramref name="to"/>: a little bounce, on the Springiness and Settle numbers.</summary>
        private void Spring(ref float at, ref float speed, float to, float dt)
        {
            speed += (-springiness * (at - to) - settle * speed) * dt;
            at += speed * dt;
        }

        /// <summary>Puts the camera where the depth says: far with a narrow lens while flat, near with a wide one while
        /// deep, the menu the same size in both; and the wash behind the slabs.</summary>
        private void Look()
        {
            float deep = Mathf.Clamp01(_depth);
            float lens = Mathf.Lerp(flatLens, deepLens, deep);
            float half = frameMetres.y * 0.5f * enlarge;
            float far = half / Mathf.Tan(lens * 0.5f * Mathf.Deg2Rad);
            Vector3 side = (_right * (Pointer.x * lean.x) + _up * (Pointer.y * lean.y - deepDrop)) * (deep * enlarge);
            _view.fieldOfView = lens;
            _view.nearClipPlane = far * 0.25f; _view.farClipPlane = far * 2.5f;
            _view.transform.SetPositionAndRotation(_origin - _look * far + side, Quaternion.LookRotation(_origin + side * 0.35f - (_origin - _look * far + side), _up));
            if (_washCanvas != null) _washCanvas.planeDistance = far * 1.6f;
        }

        /// <summary>Shows a control in a state: its slab's colours and its frame's.</summary>
        private void Paint(Control c, State state)
        {
            float bright = state == State.Disabled ? disabledBright : state == State.Selected ? selectedBright : 1f;
            if (c.BodyRenderer != null)
                for (int i = 0; i < c.BodyColours.Length; i++)
                {
                    Color colour = c.BodyColours[i] * bright; colour.a = 1f;
                    _paint.SetColor(BaseColour, colour);
                    c.BodyRenderer.SetPropertyBlock(_paint, i);
                }
            if (c.FrameRenderer != null)
            {
                _paint.SetColor(BaseColour, state == State.Disabled ? disabledFrame : state == State.Selected ? selectedFrame : state == State.Active ? activeFrame : normalFrame);
                c.FrameRenderer.SetPropertyBlock(_paint);
            }
            if (c.BarTrack != null)
            {
                float dim = state == State.Disabled ? disabledBright : 1f;
                Color track = trackColour * dim, fill = fillColour * dim; track.a = fill.a = 1f;
                _paint.SetColor(BaseColour, track); c.BarTrack.SetPropertyBlock(_paint);
                _paint.SetColor(BaseColour, fill); c.BarFill.SetPropertyBlock(_paint);
            }
            if (c.Words != null) { Color w = wordColour; w.a = state == State.Disabled ? 0.4f : 1f; if (c.Words.color != w) c.Words.color = w; }
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_barQuad != null) Gone(_barQuad);
            if (_under != null && _view != null)
            {
                UniversalAdditionalCameraData game = _under.GetUniversalAdditionalCameraData();
                if (game != null) game.cameraStack.Remove(_view);
            }
            if (_wash != null) Destroy(_wash);
        }
    }
}
