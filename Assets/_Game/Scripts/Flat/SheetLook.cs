using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Plays a sprite sheet on a renderer (D-0207): the frames stand side by side in one picture, and the renderer shows
    /// one of them at a time by sliding its material's _BaseMap tiling and offset (through a property block, so the
    /// material asset is never written). The pixel rift (the Flat Zone's way in) is drawn this way: a quad that steps
    /// through the rift's 16 frames. Tune Frames to the sheet and Frames Per Second to taste.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SheetLook : MonoBehaviour
    {
        private static readonly int BaseMapPlace = Shader.PropertyToID("_BaseMap_ST");

        [SerializeField, Tooltip("What shows the sheet. Found on this object if empty.")] private Renderer look;
        [SerializeField, Min(1), Tooltip("How many frames stand side by side in the sheet.")] private int frames = 16;
        [SerializeField, Min(0f), Tooltip("Frames a second. 0 = the first frame only.")] private float framesPerSecond = 14f;
        [SerializeField, Tooltip("On: it plays while the game is held (a pause). Off: it stops with the game.")] private bool realTime;

        private MaterialPropertyBlock _block;
        private int _shown = -1;

        private void Awake()
        {
            if (look == null) look = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable() => _shown = -1;

        private void LateUpdate()
        {
            if (look == null) return;
            double t = realTime ? Time.unscaledTimeAsDouble : Time.timeAsDouble;
            int frame = framesPerSecond > 0f ? (int)(t * framesPerSecond % frames) : 0;
            if (frame == _shown) return;
            _shown = frame;
            look.GetPropertyBlock(_block);
            _block.SetVector(BaseMapPlace, new Vector4(1f / frames, 1f, (float)frame / frames, 0f));
            look.SetPropertyBlock(_block);
        }
    }
}
