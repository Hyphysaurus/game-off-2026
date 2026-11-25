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
    /// Everything moves on real time: a menu holds the game's.
    /// </summary>
    public sealed class MenuModel : MonoBehaviour
    {
        public const string PrefabPath = "Titles/TitleMenuModel";

        public enum State { Normal, Selected, Active, Disabled }

        /// <summary>A control drawn with another's slab (see Drawn Like).</summary>
        [System.Serializable] private struct Twin { public string control, sameAs; }

        /// <summary>One control of the model: its slab, its frame, its words, and where it is on the screen when flat.</summary>
        public sealed class Control
        {
            public string Name, Page;
            public Transform Body;
            public Renderer BodyRenderer, FrameRenderer;
            public Color[] BodyColours;
            public Vector3 RestScale, RestPlace, LabelPlace;
            public Bounds Box;              // its slab's own box, in the slab's space
            public int DepthAxis;           // which of the slab's own axes runs toward the viewer
            public TextMeshPro Words;
            /// <summary>Its middle and its size on the screen while flat, canvas pixels from the middle of a 1080-high screen.</summary>
            public Vector2 Centre, Size;
            public bool Usable = true, Active, Pressed, Shown;
            internal float Grow = 1f, GrowSpeed, Drop, DropSpeed, Squash, SquashSpeed, Wait;
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

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<string, Control> _controls = new Dictionary<string, Control>();
        private readonly Dictionary<string, Transform> _named = new Dictionary<string, Transform>();
        private readonly List<Control> _shown = new List<Control>();
        private Camera _view, _under;
        private Canvas _washCanvas;
        private GameObject _wash;
        private Vector3 _origin, _right, _up, _look;
        private MaterialPropertyBlock _paint;
        private float _depth, _depthSpeed;
        private bool _read;

        /// <summary>The room left for the title, canvas pixels.</summary>
        public Vector2 TitleBox => titleMetres * pixelsPerMetre;
        /// <summary>The page on show ("Main" for the object Page_Main).</summary>
        public string Page { get; private set; } = "";
        /// <summary>The controls on show, top to bottom.</summary>
        public IReadOnlyList<Control> Shown => _shown;
        /// <summary>The control the owner's pointer or keys are on; null for none.</summary>
        public Control Selected { get; set; }
        /// <summary>The shift: false = flat (2D), true = deep (3D).</summary>
        public bool Deep { get; set; }
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
            transform.SetPositionAndRotation(place, transform.rotation);
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
                var c = new Control { Name = pair.Key, Body = slab, BodyRenderer = body, RestScale = slab.localScale, RestPlace = slab.position, Box = mesh.sharedMesh.bounds };
                for (Transform p = slab.parent; p != null && p != transform; p = p.parent) if (p.name.StartsWith("Page_")) { c.Page = p.name.Substring(5); break; }
                if (c.Page == null) c.Page = "";
                frame.TryGetComponent(out c.FrameRenderer);
                c.BodyColours = Unlit(body);
                if (c.FrameRenderer != null) Unlit(c.FrameRenderer);
                float most = -1f;
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 way = axis == 0 ? slab.right : axis == 1 ? slab.up : slab.forward;
                    float along = Mathf.Abs(Vector3.Dot(way, _look));
                    if (along > most) { most = along; c.DepthAxis = axis; }
                }
                Vector2 low = new Vector2(float.MaxValue, float.MaxValue), high = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c.Box.center + Vector3.Scale(c.Box.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector2 at = OnScreen(slab.TransformPoint(corner));
                    low = Vector2.Min(low, at); high = Vector2.Max(high, at);
                }
                c.Centre = (low + high) * 0.5f; c.Size = high - low;
                c.LabelPlace = _named.TryGetValue("Label_" + pair.Key, out Transform text) ? text.position : slab.position - _look * (0.02f * enlarge);
                _controls[pair.Key] = c;
            }
            if (needed != null) foreach (string name in needed) if (!_controls.ContainsKey(name)) return "the model has no control called " + name;
            _read = true;
            return null;
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
            Page = page ?? "";
            _shown.Clear();
            foreach (Control c in _controls.Values)
            {
                bool on = c.Page.Length == 0 || c.Page == Page;
                c.Shown = on; c.Pressed = false;
                c.Body.gameObject.SetActive(on);
                if (on) _shown.Add(c);
            }
            _shown.Sort((a, b) => b.Centre.y != a.Centre.y ? b.Centre.y.CompareTo(a.Centre.y) : a.Centre.x.CompareTo(b.Centre.x));
            for (int i = 0; i < _shown.Count; i++) { Control c = _shown[i]; c.Drop = pageDrop; c.DropSpeed = 0f; c.Wait = i * pageStagger; c.Grow = 1f; c.GrowSpeed = 0f; }
            Selected = null;
            foreach (Control c in _shown) if (c.Usable) { Selected = c; break; }
        }

        /// <summary>What a control reads: a line, and a smaller second line under it (null for none).</summary>
        public void SetWords(string control, string line, string second = null)
        {
            if (!_controls.TryGetValue(control, out Control c)) return;
            if (c.Words == null)
            {
                var go = new GameObject("Words");
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
                Vector3 size = c.RestScale * c.Grow;
                size[c.DepthAxis] = c.RestScale[c.DepthAxis] * thin;
                c.Body.localScale = size;
                c.Body.position = c.RestPlace + _up * (c.Drop * enlarge) - _look * ((selected ? selectedLift : 0f) * Mathf.Clamp01(_depth) * enlarge);
                Paint(c, !c.Usable ? State.Disabled : selected ? State.Selected : c.Active ? State.Active : State.Normal);
            }
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
            if (c.Words != null) { Color w = wordColour; w.a = state == State.Disabled ? 0.4f : 1f; if (c.Words.color != w) c.Words.color = w; }
        }

        private void OnDestroy()
        {
            if (_under != null && _view != null)
            {
                UniversalAdditionalCameraData game = _under.GetUniversalAdditionalCameraData();
                if (game != null) game.cameraStack.Remove(_view);
            }
            if (_wash != null) Destroy(_wash);
        }
    }
}
