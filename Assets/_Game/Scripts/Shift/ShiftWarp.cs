using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftJam
{
    /// <summary>
    /// The look of a shift on screen: a radial tear that pushes the picture outward (grow) or inward
    /// (shrink) with colour fringing and a flash, plus a field-of-view punch on the camera. It owns the
    /// fullscreen material's Progress and the renderer feature's on/off, and follows the Shifter's own
    /// progress rather than the clock, so a Feel time dip stretches the warp along with the growth.
    /// It can also play once on its own clock (<see cref="Play"/>): a Flat Zone (D-0203) plays it outward as the
    /// camera swings side-on and inward as it swings back, over the zone's blend. That one has no field-of-view
    /// punch (the camera's own field of view is swinging then), and a size shift always wins: none starts during a
    /// shift, and a shift that starts during one takes the screen over.
    /// The feature is off when nothing plays, so the fullscreen pass costs nothing most of the time.
    /// </summary>
    public sealed class ShiftWarp : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int DirectionId = Shader.PropertyToID("_Direction");
        private static readonly int MaskId = Shader.PropertyToID("_Mask");
        private static readonly int ArtId = Shader.PropertyToID("_Art");

        [SerializeField, Tooltip("The ShiftWarp material the Full Screen Pass feature draws with.")] private Material material;
        [SerializeField, Tooltip("The Full Screen Pass Renderer Feature on the renderer. Switched on only while the warp plays.")] private ScriptableRendererFeature feature;
        [SerializeField, Tooltip("Found on this object if empty.")] private Shifter shifter;
        [SerializeField, Tooltip("Found on the main camera if empty.")] private ShoulderCamera shoulderCamera;
        [SerializeField, Tooltip("Shift progress (0..1) to warp progress (0..1). Steeper early = the tear opens fast. A warp on its own clock (a Flat Zone's swing) runs through the same curve.")]
        private AnimationCurve progress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Tooltip("Degrees of field of view added at the peak of the shift; negative zooms in.")] private float fovPunch = -14f;
        [SerializeField, Tooltip("Rift drawings used when growing; one is picked at random per shift. Empty = keep the material's own mask.")]
        private Texture2D[] growMasks;
        [SerializeField, Tooltip("Rift drawings used when shrinking; one is picked at random per shift. Empty = keep the material's own mask.")]
        private Texture2D[] shrinkMasks;
        [SerializeField, Tooltip("Colour rift drawings (RGBA) laid over the ring when growing; one picked at random per shift. Empty = keep the material's own art.")]
        private Texture2D[] growArts;
        [SerializeField, Tooltip("Colour rift drawings (RGBA) laid over the ring when shrinking; one picked at random per shift. Empty = keep the material's own art.")]
        private Texture2D[] shrinkArts;

        private bool _active;
        private bool _onClock;          // playing on its own clock (Play), not following a shift
        private float _clockFrom, _clockSeconds;

        private void Awake()
        {
            if (shifter == null) shifter = GetComponent<Shifter>();
            if (shoulderCamera == null && Camera.main != null) shoulderCamera = Camera.main.GetComponent<ShoulderCamera>();
        }

        private void OnEnable()
        {
            if (shifter != null) shifter.ShiftStarted += OnShiftStarted;
            Clear();
        }

        private void OnDisable()
        {
            if (shifter != null) shifter.ShiftStarted -= OnShiftStarted;
            Clear();
        }

        private void OnShiftStarted(ShiftSize size)
        {
            if (material == null) return;
            Begin(size == ShiftSize.Kid);
            _onClock = false;   // a warp on its own clock gives way: the shift has the screen now
        }

        /// <summary>Plays the warp once on its own clock (real time, as the camera's swing runs), outward
        /// (<paramref name="grow"/>) or inward, over <paramref name="seconds"/>. A size shift running now wins: nothing
        /// plays and this answers false. A warp on its own clock already playing starts again with this one.</summary>
        public bool Play(bool grow, float seconds)
        {
            if (material == null || !isActiveAndEnabled) return false;
            if ((_active && !_onClock) || (shifter != null && shifter.Shifting)) return false;
            Begin(grow);
            _onClock = true;
            _clockFrom = Time.unscaledTime;
            _clockSeconds = Mathf.Max(0.01f, seconds);
            if (shoulderCamera != null) shoulderCamera.FovOffset = 0f;
            return true;
        }

        /// <summary>The material set for one warp (its direction, a drawing from each list) and the feature on.</summary>
        private void Begin(bool grow)
        {
            material.SetFloat(DirectionId, grow ? 1f : -1f);
            material.SetFloat(ProgressId, 0f);
            Pick(grow ? growMasks : shrinkMasks, MaskId);
            Pick(grow ? growArts : shrinkArts, ArtId);
            if (feature != null) feature.SetActive(true);
            _active = true;
        }

        /// <summary>One random drawing from the list onto the material slot; an empty list leaves the slot alone.</summary>
        private void Pick(Texture2D[] list, int slot)
        {
            if (list == null || list.Length == 0) return;
            Texture2D pick = list[Random.Range(0, list.Length)];
            if (pick != null) material.SetTexture(slot, pick);
        }

        private void Update()
        {
            if (!_active) return;
            if (_onClock)
            {
                float t = (Time.unscaledTime - _clockFrom) / _clockSeconds;
                if (t >= 1f) { Clear(); return; }
                material.SetFloat(ProgressId, progress.Evaluate(t));
                return;
            }
            if (shifter == null || !shifter.Shifting) { Clear(); return; }
            float value = progress.Evaluate(shifter.Progress);
            material.SetFloat(ProgressId, value);
            if (shoulderCamera != null) shoulderCamera.FovOffset = fovPunch * Mathf.Sin(Mathf.PI * value);
        }

        /// <summary>Back to nothing on screen: progress 0, feature off, camera FOV untouched.</summary>
        private void Clear()
        {
            _active = false;
            _onClock = false;
            if (material != null) material.SetFloat(ProgressId, 0f);
            if (feature != null) feature.SetActive(false);
            if (shoulderCamera != null) shoulderCamera.FovOffset = 0f;
        }
    }
}
