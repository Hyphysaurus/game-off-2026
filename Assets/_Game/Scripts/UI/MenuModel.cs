using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// A menu's look as a 3D model (D-0196): the slabs of his Blender menu, drawn over the game by a camera of their
    /// own, with the menu's live text and real buttons laid over them by name. The model is only looked at; whoever owns
    /// the menu (TitleMenu) still makes the buttons and says what they do.
    /// On a prefab under Resources (Titles/TitleMenuModel): this component beside a copy of the model file. A CONTROL is
    /// any object in the model with a child called its own name + "_Frame"; its text goes where the child "Label_" + its
    /// name is. An object called "Anchor_..." marks a place for something the menu puts there (the title, the hint).
    /// Re-export the model with the same names and nothing here or in code changes. Take the prefab away and the menu
    /// draws itself the old way.
    /// How it is seen: the model stands far below the world, enlarged, on the UI layer; an orthographic overlay camera
    /// on the game camera's stack looks at its front (which way is up, and which side the viewer is on, are read from
    /// where its controls and labels are, not from the file's axes). Model metres become screen pixels at Pixels Per
    /// Metre on a 1080-high screen, so a button made at a control's place in canvas pixels sits on its slab at any
    /// window size. The slabs are drawn unlit in the model's own colours.
    /// </summary>
    public sealed class MenuModel : MonoBehaviour
    {
        public const string PrefabPath = "Titles/TitleMenuModel";

        public enum State { Normal, Selected, Active, Disabled }

        /// <summary>A control drawn with another's slab (see Drawn Like).</summary>
        [System.Serializable] private struct Twin { public string control, sameAs; }

        /// <summary>One control of the model: its slab, its frame, and where it is on the screen.</summary>
        public sealed class Control
        {
            public string Name;
            public Transform Body;
            public Renderer BodyRenderer, FrameRenderer;
            public Color[] BodyColours;
            public Vector3 RestScale;
            /// <summary>Its middle and its size on the screen, canvas pixels from the middle of a 1080-high screen.</summary>
            public Vector2 Centre, Size;
            /// <summary>Where its text goes, the same way.</summary>
            public Vector2 Label;
        }

        [Header("The model on the screen")]
        [SerializeField, Tooltip("An unlit material; every slab is drawn with it, in its own colours. Without one the menu draws itself the old way.")] private Material slabMaterial;
        [SerializeField, Min(1f), Tooltip("Screen pixels a metre of the model takes on a 1080-high screen (the model was made at 600: a 3.2 by 1.8 m frame is 1920 by 1080).")] private float pixelsPerMetre = 600f;
        [SerializeField, Tooltip("The frame the model was laid out in, metres: its height fills the screen's.")] private Vector2 frameMetres = new Vector2(3.2f, 1.8f);
        [SerializeField, Tooltip("The room the model leaves for the title at Anchor_Title, metres.")] private Vector2 titleMetres = new Vector2(1f, 0.4667f);
        [SerializeField, Tooltip("The dark wash over the game behind the slabs.")] private Color wash = new Color(0.03f, 0.03f, 0.06f, 0.72f);
        [Header("A control's frame, by its state (the first that is true: disabled, selected, active)")]
        [SerializeField] private Color normalFrame = new Color32(0x49, 0x23, 0x93, 0xff);
        [SerializeField] private Color selectedFrame = new Color32(0x73, 0xf1, 0xff, 0xff);
        [SerializeField] private Color activeFrame = new Color32(0xff, 0x72, 0xcc, 0xff);
        [SerializeField] private Color disabledFrame = new Color32(0x0a, 0x08, 0x12, 0xff);
        [Header("A control's slab")]
        [SerializeField, Min(0f), Tooltip("Its colours are this much brighter while selected (the frame is a hairline: the slab itself must show it).")] private float selectedBright = 1.45f;
        [SerializeField, Range(0f, 1f), Tooltip("And this much of themselves while disabled.")] private float disabledBright = 0.4f;
        [SerializeField, Min(0.5f), Tooltip("Its size while selected.")] private float selectedSize = 1.04f;
        [SerializeField, Min(0.5f), Tooltip("Its size while pressed.")] private float pressedSize = 0.96f;
        [SerializeField, Min(0.01f), Tooltip("Seconds a change of size takes (real time: the menu holds the game's).")] private float sizeSeconds = 0.09f;
        [Header("Its text")]
        [SerializeField, Range(0.1f, 1f), Tooltip("A caption's letter height, as a share of its control's height.")] private float captionShare = 0.46f;
        [SerializeField, Min(0f), Tooltip("Clear space kept between a text and its control's sides, metres.")] private float textMargin = 0.03f;
        [Header("Reading the model (two controls, one straight above the other, say which way is up; the upper one's label says where the viewer stands)")]
        [SerializeField] private string upperControl = "Btn_Continue";
        [SerializeField] private string lowerControl = "Slot_3";
        [Header("Controls the model shows in a state of its own")]
        [SerializeField, Tooltip("A control whose slab was made already looking disabled or selected (the first model's Continue has a black face) is drawn with the slab of another control of the same size. Empty the list once the model is made again with every control in its plain look.")]
        private Twin[] drawnLike = { new Twin { control = "Btn_Continue", sameAs = "Btn_NewGame" } };
        [Header("Where it stands (out of the game's sight)")]
        [SerializeField, Tooltip("Far from the world, so the game's own camera never has it in view.")] private Vector3 place = new Vector3(0f, -5000f, 0f);
        [SerializeField, Min(1f), Tooltip("How many times larger than made it stands there: that far from the middle of the world, a slab's millimetres would shimmer at their own size.")] private float enlarge = 100f;

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<string, Control> _controls = new Dictionary<string, Control>();
        private readonly Dictionary<string, Transform> _named = new Dictionary<string, Transform>();
        private Camera _view, _under;
        private GameObject _wash;
        private Vector3 _origin, _right, _up;
        private MaterialPropertyBlock _paint;

        public float SelectedSize => selectedSize;
        public float PressedSize => pressedSize;
        public float SizeSeconds => sizeSeconds;
        public float CaptionShare => captionShare;
        /// <summary>The room left for the title, canvas pixels.</summary>
        public Vector2 TitleBox => titleMetres * pixelsPerMetre;
        /// <summary>The clear space kept beside a text, canvas pixels.</summary>
        public float TextMargin => textMargin * pixelsPerMetre;

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
            string missing = model.Setup(under, game, needed);
            if (missing == null) return model;
            Debug.LogWarning("MenuModel: the menu draws itself the old way (" + missing + ").", prefab);
            Destroy(model.gameObject);
            return null;
        }

        /// <summary>Null when it is ready; else what is missing.</summary>
        private string Setup(Camera under, UniversalAdditionalCameraData game, IReadOnlyList<string> needed)
        {
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
            Vector3 look = -toViewer.normalized;
            up = Vector3.ProjectOnPlane(up, look).normalized;
            if (up.sqrMagnitude < 0.5f) return "its controls do not say which way is up";
            _origin = transform.position; _up = up; _right = Vector3.Cross(up, look);

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
                var c = new Control { Name = pair.Key, Body = pair.Value, BodyRenderer = body, RestScale = pair.Value.localScale };
                frame.TryGetComponent(out c.FrameRenderer);
                c.BodyColours = Unlit(body);
                if (c.FrameRenderer != null) Unlit(c.FrameRenderer);
                Bounds b = mesh.sharedMesh.bounds;
                Vector2 low = new Vector2(float.MaxValue, float.MaxValue), high = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector2 at = OnScreen(pair.Value.TransformPoint(corner));
                    low = Vector2.Min(low, at); high = Vector2.Max(high, at);
                }
                c.Centre = (low + high) * 0.5f; c.Size = high - low;
                c.Label = _named.TryGetValue("Label_" + pair.Key, out Transform text) ? OnScreen(text.position) : c.Centre;
                _controls[pair.Key] = c;
                Paint(c, State.Normal, 1f);
            }
            if (needed != null) foreach (string name in needed) if (!_controls.ContainsKey(name)) return "the model has no control called " + name;

            // The camera that draws it over the game, and the dark wash behind the slabs (a canvas on that camera, further off).
            float far = 10f * enlarge;
            var eye = new GameObject("MenuModelCamera");
            eye.transform.SetParent(transform, true);
            eye.transform.SetPositionAndRotation(_origin - look * far, Quaternion.LookRotation(look, up));
            _view = eye.AddComponent<Camera>();
            _view.orthographic = true;
            _view.orthographicSize = frameMetres.y * 0.5f * enlarge;
            _view.nearClipPlane = far * 0.1f; _view.farClipPlane = far * 3f;
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
            var canvas = _wash.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _view; canvas.planeDistance = far * 2f;
            var fill = new GameObject("Wash", typeof(RectTransform));
            fill.layer = layer;
            var rt = (RectTransform)fill.transform;
            rt.SetParent(_wash.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var image = fill.AddComponent<Image>(); image.color = wash; image.raycastTarget = false;
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

        /// <summary>A place of the model on the screen: canvas pixels from the middle of a 1080-high screen.</summary>
        private Vector2 OnScreen(Vector3 world)
        {
            Vector3 from = world - _origin;
            return new Vector2(Vector3.Dot(from, _right), Vector3.Dot(from, _up)) * (pixelsPerMetre / enlarge);
        }

        /// <summary>The control of this name, if the model has it.</summary>
        public bool TryGet(string control, out Control found) => _controls.TryGetValue(control, out found);

        /// <summary>Where the object of this name is on the screen (an Anchor_...). False if the model has none.</summary>
        public bool Point(string anchor, out Vector2 at)
        {
            at = Vector2.zero;
            if (!_named.TryGetValue(anchor, out Transform t) || t == null) return false;
            at = OnScreen(t.position);
            return true;
        }

        /// <summary>Shows a control in a state at a size (1 = as made).</summary>
        public void Paint(Control c, State state, float size)
        {
            if (c == null || c.Body == null) return;
            c.Body.localScale = c.RestScale * size;
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
        }

        /// <summary>On the screen or off it (another panel is over the menu, or the menu is shut).</summary>
        public void Show(bool on)
        {
            if (_view != null) _view.enabled = on;
            if (_wash != null) _wash.SetActive(on);
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
