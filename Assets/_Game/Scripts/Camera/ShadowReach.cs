using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftJam
{
    /// <summary>
    /// Keeps shadows in view at every size. URP draws shadows only within one fixed distance of the camera,
    /// tuned for the toy; at kid size the camera sits 35 times further back and every shadow would vanish.
    /// This scales the live pipeline's shadow distance with the Shifter and puts the original back when it
    /// stops, since in the editor that value lives on the pipeline asset itself.
    /// </summary>
    public sealed class ShadowReach : MonoBehaviour
    {
        [SerializeField, Tooltip("Found on this object if empty.")] private Shifter shifter;
        [SerializeField, Tooltip("Shadow distance at scale 1 (toy). Pack units.")] private float toyDistance = 50f;

        private UniversalRenderPipelineAsset _pipeline;
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
            if (Mathf.Abs(wanted - _applied) < 0.01f * Mathf.Max(1f, wanted)) return;   // skip 1% jitters
            _pipeline.shadowDistance = wanted;
            _applied = wanted;
        }
    }
}
