using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The paint's splats on a menu on a model (D-0201): where a menu paint ball (MenuDarts) bursts, one of the game's
    /// splat pictures, tinted with the ball's paint colour and turned at random, is laid FLAT on what it hit. On a slab it
    /// rides the slab as a stuck dart does (its swing, its tip, its squash, pressed thin with it while flat), lies wholly
    /// on the slab's face (Fit says where and how big) and is drawn under the slab's words, so a caption stays readable.
    /// On nothing it lies on the menu's plane where it landed, like a dart in the wall. A title letter takes no splat (it
    /// would hang over the gaps of the letter): the model is told (MenuModel.Splash) and the letter takes a cast of the
    /// colour (MenuTitle). Splats spread in quickly, stay a while and fade; past Most Splats the oldest go first; a slab's
    /// splats go when its page goes.
    /// Beside the MenuModel on its prefab, with one see-through unlit material (Data/MenuSplat.mat, made by Shift Jam >
    /// Menu > Build Title Menu Model): colour and picture are given to each splat on its own. Without the material there
    /// are no splats (a ball still bursts, knocks and sounds).
    /// Everything runs on real time: a menu holds the game's.
    /// </summary>
    [DefaultExecutionOrder(60)]   // after the MenuModel has moved its slabs this frame: the splats ride them
    [RequireComponent(typeof(MenuModel))]
    public sealed class MenuSplats : MonoBehaviour
    {
        [SerializeField, Tooltip("A see-through unlit material whose render queue is before the words' (Data/MenuSplat.mat). None = no splats.")] private Material material;
        [SerializeField, Min(1), Tooltip("The most splats at once: past it the oldest fades out.")] private int mostSplats = 40;
        [SerializeField, Min(0.1f), Tooltip("Seconds a splat stays before it fades.")] private float staySeconds = 12f;
        [SerializeField, Min(0.01f), Tooltip("Seconds it takes to fade away.")] private float fadeSeconds = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("A splat spreads from this share of its size to full...")] private float spreadFrom = 0.35f;
        [SerializeField, Min(0f), Tooltip("...in this many seconds (the game's paint takes 0.08). 0 = at full size at once.")] private float spreadSeconds = 0.08f;
        [SerializeField, Range(0f, 0.9f), Tooltip("Each splat is up to this share bigger or smaller than its ammo's Splat Size.")] private float sizeVaries = 0.2f;
        [SerializeField, Min(0.1f), Tooltip("How far a picture's paint reaches from its middle, as a share of its half width, turned any way (measured on the head-on pictures: up to 1.155, the rich ones reach into the corners). Fit keeps that much on a slab's face.")] private float reach = 1.16f;
        [SerializeField, Min(0f), Tooltip("Clear space kept between a splat's paint and the edge of a slab's face, metres.")] private float margin = 0.006f;
        [SerializeField, Min(0f), Tooltip("A splat lies this far in front of what it is on, metres, so it is drawn over it.")] private float lift = 0.003f;

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor"), BaseMap = Shader.PropertyToID("_BaseMap");

        private sealed class Splat
        {
            public Transform Body;
            public Renderer Look;
            public MenuModel.Control On;     // the slab it is on; null = the menu's plane
            public Vector2 At;               // its middle: on a slab, metres from the slab's middle; else from the menu's middle
            public float Size, Spin, MadeAt, GoingAt = -1f, ShownAlpha = -1f;
            public Color Colour;
            public Texture Picture;
        }

        private MenuModel _model;
        private readonly List<Splat> _splats = new List<Splat>();
        private readonly Stack<Splat> _spare = new Stack<Splat>();
        private MaterialPropertyBlock _block;
        private Mesh _quad;

        private MenuModel Model => _model != null ? _model : (_model = GetComponent<MenuModel>());

        /// <summary>How many splats lie on the menu now.</summary>
        public int Count => _splats.Count;

        /// <summary>
        /// THE SEAM (an editor check may call it; Land uses it): for a face <paramref name="face"/> metres across and up,
        /// a landing point <paramref name="at"/> (metres from the face's middle) and a wanted size (metres, the picture's
        /// width), returns the size the splat is drawn at and where its middle goes (<paramref name="middle"/>, metres from
        /// the face's middle) so that its paint, turned any way, lies wholly on the face: within a circle of
        /// <paramref name="reach"/> x size / 2 about its middle, at least <paramref name="margin"/> inside every edge. It is
        /// as big as wanted when that fits, else as big as the face allows; its middle is the landing point moved in as
        /// little as it must. A face too small for any paint gives 0.
        /// </summary>
        public static float Fit(Vector2 face, Vector2 at, float wanted, float reach, float margin, out Vector2 middle)
        {
            middle = Vector2.zero;
            float room = Mathf.Min(face.x, face.y) - 2f * margin;
            if (room <= 0f || wanted <= 0f) return 0f;
            reach = Mathf.Max(reach, 1e-3f);
            float size = Mathf.Min(wanted, room / reach);
            float round = size * reach * 0.5f;
            float acrossRoom = Mathf.Max(0f, face.x * 0.5f - margin - round), upRoom = Mathf.Max(0f, face.y * 0.5f - margin - round);
            middle = new Vector2(Mathf.Clamp(at.x, -acrossRoom, acrossRoom), Mathf.Clamp(at.y, -upRoom, upRoom));
            return size;
        }

        /// <summary>Fit on a control's face with this part's Reach and Margin (the model must have been read).</summary>
        public float Fit(MenuModel.Control on, Vector2 at, float wanted, out Vector2 middle)
            => Fit(Model.FaceSize(on), at, wanted, reach, margin, out middle);

        /// <summary>A paint ball burst at <paramref name="at"/>: on the slab <paramref name="on"/> (metres from its middle),
        /// or, with none, on the menu's plane (metres from the menu's middle). The model is told (a letter takes a cast);
        /// a splat of <paramref name="picture"/> in <paramref name="colour"/>, about <paramref name="size"/> metres wide, is
        /// laid there unless it is on a plaything. True when a splat was laid.</summary>
        public bool Land(MenuModel.Control on, Vector2 at, float size, Color colour, Texture picture)
        {
            MenuModel model = Model;
            if (model == null || !model.Staged) return false;
            if (on != null && !on.Shown) return false;
            if (on != null) model.Splash(on, colour);
            if ((on != null && on.Toy) || material == null || picture == null || size <= 0f) return false;
            size *= Random.Range(1f - sizeVaries, 1f + sizeVaries);
            if (on != null)
            {
                size = Fit(on, at, size, out at);
                if (size <= 0f) return false;
            }
            Splat splat = _spare.Count > 0 ? _spare.Pop() : NewSplat();
            splat.Body.gameObject.SetActive(true);
            splat.On = on; splat.At = at; splat.Size = size; splat.Spin = Random.Range(0f, 360f);
            splat.MadeAt = Time.unscaledTime; splat.GoingAt = -1f; splat.ShownAlpha = -1f;
            splat.Colour = colour; splat.Picture = picture;
            _splats.Add(splat);
            Lay(splat, spreadSeconds > 0f ? spreadFrom : 1f);
            Show(splat, 1f);
            return true;
        }

        private Splat NewSplat()
        {
            if (_quad == null) _quad = Quad();
            if (_block == null) _block = new MaterialPropertyBlock();
            var go = new GameObject("MenuSplat");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);       // under the model: one of its units is a metre of the model
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var look = go.AddComponent<MeshRenderer>();
            look.sharedMaterial = material;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.receiveShadows = false;
            look.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            look.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return new Splat { Body = go.transform, Look = look };
        }

        /// <summary>A square a metre wide, facing the viewer along its -z, its picture the right way up along +y.</summary>
        private static Mesh Quad()
        {
            var mesh = new Mesh { name = "MenuSplatQuad" };
            mesh.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void LateUpdate()
        {
            MenuModel model = Model;
            if (model == null || !model.Showing || _splats.Count == 0) return;
            float now = Time.unscaledTime;
            int kept = 0;
            for (int i = _splats.Count - 1; i >= 0; i--)        // newest first: past Most Splats the oldest go
            {
                Splat splat = _splats[i];
                if (splat.On != null && !splat.On.Shown) { Drop(i); continue; }   // its slab left the screen with its page
                kept++;
                if (splat.GoingAt < 0f && (now - splat.MadeAt > staySeconds || kept > mostSplats)) splat.GoingAt = now;
                float alpha = splat.GoingAt < 0f ? 1f : 1f - (now - splat.GoingAt) / fadeSeconds;
                if (alpha <= 0f) { Drop(i); continue; }
                float spread = spreadSeconds > 0f ? Mathf.Lerp(spreadFrom, 1f, Mathf.Clamp01((now - splat.MadeAt) / spreadSeconds)) : 1f;
                Lay(splat, spread);
                if (!Mathf.Approximately(alpha, splat.ShownAlpha)) Show(splat, alpha);
            }
        }

        /// <summary>Puts a splat where it lies now: on its slab's face as the slab is turned and stretched, or on the plane.</summary>
        private void Lay(Splat splat, float spread)
        {
            MenuModel model = Model;
            Quaternion spin = Quaternion.Euler(0f, 0f, splat.Spin);
            float size = splat.Size * spread;
            if (splat.On != null)
            {
                Quaternion tipped = model.Tipped(splat.On);
                Vector2 stretch = model.Stretch(splat.On);
                Vector3 place = model.FacePoint(splat.On, Vector2.Scale(splat.At, stretch)) + tipped * model.Toward * (lift * model.Scale);
                splat.Body.SetPositionAndRotation(place, tipped * model.Facing * spin);
                size *= Mathf.Min(stretch.x, stretch.y);      // the smaller way: a squashed slab never has its splat hang over
            }
            else splat.Body.SetPositionAndRotation(model.PlanePoint(splat.At, lift), model.Facing * spin);
            splat.Body.localScale = Vector3.one * Mathf.Max(0.0001f, size);
        }

        /// <summary>Gives a splat its picture and its colour at this much of itself (fading).</summary>
        private void Show(Splat splat, float alpha)
        {
            Color colour = splat.Colour; colour.a = Mathf.Clamp01(alpha) * splat.Colour.a;
            _block.Clear();
            _block.SetTexture(BaseMap, splat.Picture);
            _block.SetColor(BaseColour, colour);
            splat.Look.SetPropertyBlock(_block);
            splat.ShownAlpha = alpha;
        }

        private void Drop(int i)
        {
            Splat splat = _splats[i];
            _splats.RemoveAt(i);
            splat.On = null; splat.Picture = null;
            splat.Body.gameObject.SetActive(false);
            _spare.Push(splat);
        }

        private void OnDestroy()
        {
            if (_quad == null) return;
            if (Application.isPlaying) Destroy(_quad); else DestroyImmediate(_quad);
        }
    }
}
