using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftJam
{
    /// <summary>
    /// Keeps shadows in view at every size. URP draws shadows only within one fixed distance of the camera,
    /// tuned for the toy; at kid size the camera sits 35 times further back and every shadow would vanish.
    /// This scales the live pipeline's shadow distance with the Shifter and puts the original back when it
    /// stops, since in the editor that value lives on the pipeline asset itself. In Build Mode the camera can
    /// zoom out over the whole room, so the distance follows its zoom instead while that is further.
    /// </summary>
    public sealed class ShadowReach : MonoBehaviour
    {
        [SerializeField, Tooltip("Found on this object if empty.")] private Shifter shifter;
        [SerializeField, Tooltip("Shadow distance at scale 1 (toy). Pack units.")] private float toyDistance = 50f;
        [SerializeField, Tooltip("In Build Mode: shadow distance as a multiple of the build camera's zoom distance.")] private float buildReach = 2.5f;

        private UniversalRenderPipelineAsset _pipeline;
        private BuildCamera _buildCamera;
        private float _original;
        private float _applied = -1f;

        private void Awake()
        {
            if (shifter == null) shifter = GetComponent<Shifter>();
        }

        private void OnEnable()
        {
            _pipeline = UniversalRenderPipeline.asset;
            if (_pipeline != null) _original = _pipeline.shadowDistance;
            _applied = -1f;
        }

        private void OnDisable()
        {
            if (_pipeline != null) _pipeline.shadowDistance = _original;
            _applied = -1f;
        }

        private void LateUpdate()
        {
            if (_pipeline == null) return;
            float wanted = toyDistance * (shifter != null ? shifter.Scale : 1f);
            if (GameMode.Current == GameMode.Mode.Build)
            {
                if (_buildCamera == null) _buildCamera = FindFirstObjectByType<BuildCamera>();
                if (_buildCamera != null && _buildCamera.enabled) wanted = Mathf.Max(wanted, _buildCamera.Distance * buildReach);
            }
            if (Mathf.Abs(wanted - _applied) < 0.01f * Mathf.Max(1f, wanted)) return;   // skip 1% jitters
            _pipeline.shadowDistance = wanted;
            _applied = wanted;
        }
    }
}
