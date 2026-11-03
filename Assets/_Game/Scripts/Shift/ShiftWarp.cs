using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftJam
{
    /// <summary>
    /// The look of a shift on screen: a radial tear that pushes the picture outward (grow) or inward
    /// (shrink) with colour fringing and a flash, plus a field-of-view punch on the camera. It owns the
    /// fullscreen material's Progress and the renderer feature's on/off, and follows the Shifter's own
    /// progress rather than the clock, so a Feel time dip stretches the warp along with the growth.
    /// The feature is off outside a shift, so the fullscreen pass costs nothing most of the time.
    /// </summary>
    public sealed class ShiftWarp : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int DirectionId = Shader.PropertyToID("_Direction");

        [SerializeField, Tooltip("The ShiftWarp material the Full Screen Pass feature draws with.")] private Material material;
        [SerializeField, Tooltip("The Full Screen Pass Renderer Feature on the renderer. Switched on only during a shift.")] private ScriptableRendererFeature feature;
        [SerializeField, Tooltip("Found on this object if empty.")] private Shifter shifter;
        [SerializeField, Tooltip("Found on the main camera if empty.")] private ShoulderCamera shoulderCamera;
        [SerializeField, Tooltip("Shift progress (0..1) to warp progress (0..1). Steeper early = the tear opens fast.")]
        private AnimationCurve progress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Tooltip("Degrees of field of view added at the peak of the shift; negative zooms in.")] private float fovPunch = -14f;

        private bool _active;

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
            material.SetFloat(DirectionId, size == ShiftSize.Kid ? 1f : -1f);
            material.SetFloat(ProgressId, 0f);
            if (feature != null) feature.SetActive(true);
            _active = true;
        }

        private void Update()
        {
            if (!_active) return;
            if (shifter == null || !shifter.Shifting) { Clear(); return; }
            float value = progress.Evaluate(shifter.Progress);
            material.SetFloat(ProgressId, value);
            if (shoulderCamera != null) shoulderCamera.FovOffset = fovPunch * Mathf.Sin(Mathf.PI * value);
        }

        /// <summary>Back to nothing on screen: progress 0, feature off, camera FOV untouched.</summary>
        private void Clear()
        {
            _active = false;
            if (material != null) material.SetFloat(ProgressId, 0f);
            if (feature != null) feature.SetActive(false);
            if (shoulderCamera != null) shoulderCamera.FovOffset = 0f;
        }
    }
}
