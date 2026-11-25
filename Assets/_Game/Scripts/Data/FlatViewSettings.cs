using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The Flat Zones (D-0198): a stretch of the house seen side-on, played as a retro side-scroller. How the rail camera
    /// frames it, how the body is held to the lane, and how the pixel look comes on. Lengths are in the player's own units
    /// (toy units at toy size); the camera scales them. One asset for every zone: Resources/FlatView, made by
    /// Shift Jam > Flat > Land Hallway Scroller, his to tune in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Flat View Settings", fileName = "FlatView")]
    public sealed class FlatViewSettings : ScriptableObject
    {
        [Header("The rail camera (own units)")]
        [Tooltip("How tall a slice of the world fills the screen, at the lane. The hallway is 14 high: 13 frames it floor to ceiling.")]
        public float frameHeight = 13f;
        [Range(5f, 60f), Tooltip("Field of view of the rail camera, degrees. Low = a long lens: flat, nearly orthographic, like a 2D game. The distance follows from it and the frame height.")]
        public float fieldOfView = 18f;
        [Range(-20f, 20f), Tooltip("Degrees the rail camera looks down (above 0) or up. 0 = straight side-on.")]
        public float pitch = 2f;
        [Range(0f, 1f), Tooltip("Where the feet sit in the frame while standing, from the bottom (0) to the top (1).")]
        public float feetInFrame = 0.3f;
        [Tooltip("How far ahead of the player the camera looks, the way it runs (Mario's lead).")]
        public float lead = 2.5f;
        [Min(0.01f), Tooltip("Seconds the lead takes to swing to the other side when the player turns round.")]
        public float leadSeconds = 0.6f;
        [Min(0f), Tooltip("Seconds the camera trails the player along the lane. 0 = locked to it.")]
        public float followSeconds = 0.1f;
        [Min(0f), Tooltip("Seconds the camera takes to settle on a new height.")]
        public float heightSeconds = 0.3f;
        [Min(0f), Tooltip("In the air the camera only follows the height past this far from where it stands (Super Mario World: it waits for the landing).")]
        public float heightDeadZone = 2f;

        [Header("In and out")]
        [Min(0.01f), Tooltip("Seconds the camera takes to swing from behind the player to side-on, and back.")]
        public float blendSeconds = 0.6f;
        [Min(0f), Tooltip("Share of the drift off the lane taken away per second.")]
        public float laneSnap = 10f;

        [Header("The pixel look (PixelShift)")]
        [Min(16), Tooltip("Rows of pixels the picture is cut to. 224 = a Super Nintendo; 144 = a Game Boy; 240 = an NES.")]
        public int pixelRows = 224;
        [Range(0f, 1f), Tooltip("How much the picture is pulled to the palette's colours. 0 = its own colours, only pixelated.")]
        public float paletteStrength = 0.85f;
        [Range(0f, 1f), Tooltip("Ordered dither between palette colours (the Bayer pattern old games used for gradients).")]
        public float dither = 0.35f;
        [Tooltip("The palette: a strip of colours (one pixel each, left to right), point filtered. Empty = a 16-colour orb palette made by code.")]
        public Texture2D palette;
        [Min(0.01f), Tooltip("Seconds the pixel look takes to come on and to go.")]
        public float pixelSeconds = 0.4f;
    }
}
