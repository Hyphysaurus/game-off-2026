using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The menu's title as five solid letters on the model (D-0200): S, H, I, F, T, each made of one small box for every
    /// pixel of his pixel title, standing where the flat title art stood. Each letter is a plaything of the MenuModel
    /// (AddToy): the pointer brushing past stirs it, a press that pulls away stretches it, a dart or a knock tips it, and
    /// with the model's shift it goes from a drawn title (flat) to a row of blocks with sides (deep).
    /// Beside the MenuModel on its prefab. The letters' shapes and places are made from the title sheet by the editor's
    /// Shift Jam > Menu > Build Title Menu Model (MenuPieces): they are written again at every run, so do not edit them.
    /// Colours: every box shows its own pixel of the sheet, and the sheet's frames are stepped through, so the art's own
    /// animation plays on the letters. Colour Sets holds the sheet in other colours (the same picture with its hues
    /// turned, or one he repainted): a letter that is hit hard enough goes to its next set, and every hit flashes it.
    /// Paint (D-0201): a menu paint ball that bursts on a letter leaves no splat (it would hang over the gaps of the
    /// letter); the letter takes a CAST of the paint's colour instead, held a few seconds and then faded back to its own
    /// colours (it is knocked like any hit too). Cast sets how strong (0 = no cast).
    /// Without letters, without a first colour set or without the model's anchor it makes nothing and says so (Make
    /// returns false): the menu then shows the flat title as before.
    /// Everything runs on real time: a menu holds the game's.
    /// </summary>
    [RequireComponent(typeof(MenuModel))]
    public sealed class MenuTitle : MonoBehaviour
    {
        [Header("The letters (written by Shift Jam > Menu > Build Title Menu Model from the title sheet)")]
        [SerializeField, Tooltip("One shape for each letter, left to right. Slot 0 of a shape is its faces, slot 1 its sides.")] private Mesh[] letters = new Mesh[0];
        [SerializeField, Tooltip("Each letter's middle, metres across and up from the title's middle.")] private Vector2[] letterAt = new Vector2[0];
        [SerializeField, Tooltip("Where the title's middle is: the name of an Anchor_ object of the model.")] private string anchor = "Anchor_Title";
        [Header("Their colours")]
        [SerializeField, Tooltip("The title sheet in each colour set, his own first. A letter that is hit goes to the next one. Every set must be the same sheet (same size, same frames) in other colours.")] private Texture2D[] colourSets = new Texture2D[0];
        [SerializeField, Min(1), Tooltip("How many frames the sheet holds side by side (written with the letters).")] private int frames = 64;
        [SerializeField, Min(0f), Tooltip("Frames a second (his animation runs at 16). 0 = the first frame only.")] private float framesPerSecond = 16f;
        [SerializeField, Range(0f, 1f), Tooltip("How dark a letter's sides are, as a share of its face.")] private float sideShade = 0.62f;
        [Header("A hit")]
        [SerializeField, Min(0f), Tooltip("A knock at least this hard changes the letter's colour set (the blaster's dart is 1, the LMG's 0.35, a letter let go after a pull 0.2 to 1.2).")] private float changeFrom = 0.3f;
        [SerializeField, Min(0f), Tooltip("A letter changes set no more often than this, seconds (the LMG would make it flicker).")] private float changeEvery = 0.18f;
        [SerializeField, Min(0f), Tooltip("How much brighter a letter is at the moment it is hit (0.6 = 60 % brighter).")] private float flash = 0.6f;
        [SerializeField, Min(0.01f), Tooltip("Seconds that brightness takes to fade.")] private float flashSeconds = 0.25f;
        [SerializeField, Tooltip("Plays when a letter changes colour set. No clip = silent.")] private CourseSounds.Cue changeSound;
        [SerializeField, Min(0f), Tooltip("That sound plays no more often than this, seconds, whichever letter changes.")] private float changeSoundGap = 0.07f;
        [Header("Paint (D-0201)")]
        [SerializeField, Range(0f, 1f), Tooltip("How strongly a letter hit by a paint ball takes the paint's colour: 0 = never (paint only knocks it), 1 = the paint's colour over all of it. Its own picture always shows through, and the cast keeps the letter as bright as it was: a cast, not a blackout.")] private float cast = 0.55f;
        [SerializeField, Min(0f), Tooltip("Seconds the cast holds at full.")] private float castSeconds = 2.5f;
        [SerializeField, Min(0.01f), Tooltip("Seconds it then takes to fade back to the letter's own colours.")] private float castFadeSeconds = 1.5f;

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor"), BaseMap = Shader.PropertyToID("_BaseMap"), BaseMapPlace = Shader.PropertyToID("_BaseMap_ST");

        private sealed class Letter
        {
            public MenuModel.Control Toy;
            public Renderer Look;
            public int Slots, Set;
            public float ChangedAt = -10f, HitAt = -10f, PaintedAt = -100f;
            public Color Paint = Color.white;
            public int ShownFrame = -1, ShownSet = -1; public float ShownGlow = -1f, ShownCast = -1f;
        }

        private readonly List<Letter> _letters = new List<Letter>();
        private MenuModel _model;
        private MaterialPropertyBlock _block;
        private float _lastSound = -10f;
        private bool _made;

        /// <summary>How many letters stand on the model.</summary>
        public int LetterCount => _letters.Count;

        /// <summary>Builds the letters on the model, once it has been read (MenuModel.IsRead). True when they stand; false
        /// when they cannot (the owner then draws the flat title). Asked again, it answers the same and builds nothing.</summary>
        public bool Make()
        {
            if (_made) return _letters.Count > 0;
            MenuModel model = GetComponent<MenuModel>();
            if (model == null || !model.IsRead) return false;        // asked before the model was read: it may be asked again
            _made = true;
            if (letters == null || letters.Length == 0 || colourSets == null || colourSets.Length == 0 || colourSets[0] == null) return false;
            if (!model.Point(anchor, out Vector2 middle)) return false;
            middle /= model.PixelsPerMetre;
            Quaternion turn = Quaternion.LookRotation(-model.Toward, model.Up);     // a letter's own x, y, z = the model's right, its up, away from the viewer
            for (int i = 0; i < letters.Length; i++)
            {
                Mesh shape = letters[i];
                if (shape == null) continue;
                var go = new GameObject(shape.name);
                go.transform.SetParent(transform, false);        // under the model: one of its units is a metre of the model
                go.transform.SetPositionAndRotation(model.PlanePoint(middle + (i < letterAt.Length ? letterAt[i] : Vector2.zero)), turn);
                go.AddComponent<MeshFilter>().sharedMesh = shape;
                var look = go.AddComponent<MeshRenderer>();
                look.sharedMaterials = new Material[Mathf.Max(1, shape.subMeshCount)];     // none yet: Flatten gives every slot the slab's own
                model.Flatten(go);
                MenuModel.Control toy = model.AddToy(shape.name, go.transform, shape.bounds);
                if (toy == null) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); continue; }
                _letters.Add(new Letter { Toy = toy, Look = look, Slots = look.sharedMaterials.Length });
            }
            if (_letters.Count == 0) return false;
            _model = model;
            model.Knocked += OnKnocked;
            model.Splashed += OnSplashed;
            _block = new MaterialPropertyBlock();
            foreach (Letter l in _letters) Show(l, 0, 1f, 0f);
            return true;
        }

        private void OnDestroy() { if (_model != null) { _model.Knocked -= OnKnocked; _model.Splashed -= OnSplashed; } }

        /// <summary>The model says a paint ball burst on a control: if it is one of the letters, it takes a cast of the paint.</summary>
        private void OnSplashed(MenuModel.Control hit, Color paint)
        {
            if (cast <= 0f) return;
            foreach (Letter l in _letters)
            {
                if (l.Toy != hit) continue;
                l.Paint = paint; l.PaintedAt = Time.unscaledTime;
                return;
            }
        }

        /// <summary>How much of its cast a letter shows now: Cast while it holds, then fading to none.</summary>
        private float CastNow(Letter l, float now)
        {
            if (cast <= 0f) return 0f;
            float since = now - l.PaintedAt;
            if (since < castSeconds) return cast;
            return cast * Mathf.Clamp01(1f - (since - castSeconds) / castFadeSeconds);
        }

        /// <summary>The model says a control was hit: if it is one of the letters it flashes, and a hard enough hit takes it
        /// to its next colour set.</summary>
        private void OnKnocked(MenuModel.Control hit, float power)
        {
            foreach (Letter l in _letters)
            {
                if (l.Toy != hit) continue;
                float now = Time.unscaledTime;
                l.HitAt = now;
                if (power < changeFrom || now - l.ChangedAt < changeEvery) return;
                for (int tries = 1; tries < colourSets.Length; tries++)
                {
                    int next = (l.Set + tries) % colourSets.Length;
                    if (colourSets[next] == null) continue;      // a set whose picture is gone is passed over
                    l.Set = next; l.ChangedAt = now;
                    if (now - _lastSound >= changeSoundGap) { _lastSound = now; UiAudio.Play(changeSound); }
                    break;
                }
                return;
            }
        }

        private void LateUpdate()
        {
            if (_model == null || !_model.Showing) return;
            int frame = framesPerSecond > 0f ? (int)(Time.unscaledTimeAsDouble * framesPerSecond % Mathf.Max(1, frames)) : 0;
            float now = Time.unscaledTime;
            foreach (Letter l in _letters)
            {
                float glow = 1f + flash * Mathf.Clamp01(1f - (now - l.HitAt) / flashSeconds);
                float tint = CastNow(l, now);
                if (frame != l.ShownFrame || l.Set != l.ShownSet || !Mathf.Approximately(glow, l.ShownGlow) || !Mathf.Approximately(tint, l.ShownCast)) Show(l, frame, glow, tint);
            }
        }

        /// <summary>Shows a letter in its colour set at a frame of the sheet: each of its boxes points at its own pixel of the
        /// first frame, and the whole picture is slid along by whole frames. <paramref name="tint"/>: how much of its paint's
        /// cast it shows (0 none); the cast's brightest part is kept at full, so the letter keeps its light.</summary>
        private void Show(Letter l, int frame, float glow, float tint)
        {
            Texture2D sheet = colourSets[l.Set] != null ? colourSets[l.Set] : colourSets[0];
            Color paint = l.Paint;
            float top = Mathf.Max(paint.r, Mathf.Max(paint.g, paint.b));
            if (top > 1e-3f) { paint.r /= top; paint.g /= top; paint.b /= top; } else paint = Color.white;
            Color over = Color.Lerp(Color.white, paint, Mathf.Clamp01(tint));
            for (int slot = 0; slot < l.Slots; slot++)
            {
                float bright = (slot == 0 ? 1f : sideShade) * glow;
                _block.Clear();
                _block.SetTexture(BaseMap, sheet);
                _block.SetVector(BaseMapPlace, new Vector4(1f, 1f, (float)frame / Mathf.Max(1, frames), 0f));
                _block.SetColor(BaseColour, new Color(over.r * bright, over.g * bright, over.b * bright, 1f));
                l.Look.SetPropertyBlock(_block, slot);
            }
            l.ShownFrame = frame; l.ShownSet = l.Set; l.ShownGlow = glow; l.ShownCast = tint;
        }
    }
}
