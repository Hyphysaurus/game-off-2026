using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The Flat Zones (D-0198): a stretch of the house seen side-on, played as a side-scroller. How the rail camera
    /// frames it and how the body is held to the lane. Lengths are in the player's own units (toy units at toy size);
    /// the camera scales them. One asset for every zone: Resources/FlatView, made by
    /// Shift Jam > Flat > Land Hallway Scroller when it is missing (never overwritten), his to tune in the Inspector:
    /// the camera reads it every frame, so a number changed in Play shows at once.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Flat View Settings", fileName = "FlatView")]
    public sealed class FlatViewSettings : ScriptableObject
    {
        [Header("The rail camera (own units)")]
        [Tooltip("How tall a slice of the world fills the screen, at the lane. Smaller = the toy is bigger on screen. 9 (D-0203; cut A had 13, where the toy read small): a toy (1.8 tall) is a fifth of the screen's height, and the hallway (14 high) shows two thirds of its height at a time.")]
        public float frameHeight = 9f;
        [Range(5f, 60f), Tooltip("Field of view of the rail camera, degrees. Low = a long lens: flat, nearly orthographic, like a 2D game. The distance follows from it and the frame height.")]
        public float fieldOfView = 18f;
        [Range(-20f, 20f), Tooltip("Degrees the rail camera looks down (above 0) or up. 0 = straight side-on.")]
        public float pitch = 2f;
        [Range(0f, 1f), Tooltip("Where the feet sit in the frame while standing, from the bottom (0) to the top (1). The zone's Floor and Ceiling come first: where the frame would show under the one or over the other, it stops.")]
        public float feetInFrame = 0.3f;
        [Tooltip("How far ahead of the player the camera looks, the way it runs. 0 = the player stays in the middle.")]
        public float lead = 2.5f;
        [Min(0.01f), Tooltip("Seconds the lead takes to swing to the other side when the player turns round.")]
        public float leadSeconds = 0.6f;
        [Min(0f), Tooltip("Seconds the camera trails the player along the lane. 0 = locked to it. On a rope the camera follows the swing: raise this if the picture sways too much.")]
        public float followSeconds = 0.1f;
        [Min(0f), Tooltip("Seconds the camera takes to settle on a new height.")]
        public float heightSeconds = 0.3f;
        [Min(0f), Tooltip("In the air the camera only follows the height past this far from where it stands (it waits for the landing). 0 = it follows every jump.")]
        public float heightDeadZone = 2f;

        [Header("In and out")]
        [Min(0.01f), Tooltip("Seconds the camera takes to swing from behind the player to side-on, and back.")]
        public float blendSeconds = 0.6f;
        [Range(0f, 90f), Tooltip("The stick is read against the camera, and the camera turns a quarter round on the way in and out. With the stick held through that, it keeps meaning the way the player was going until it is let go or moved by more than this many degrees. 0 = off: the stick follows the camera at once (W held on the way in fades to a stop).")]
        public float keepStickAngle = 40f;

        [Header("The fill light (D-0203)")]
        [Min(0f), Tooltip("While a zone holds a player, a light the zone owns rides with that player's camera and lights the stretch from the front, so the toy and the far wall read side-on. How bright it is at the lane (it fades in and out with the camera's swing). 0 = off. The zone needs its Fill light set (the hallway builder makes one).")]
        public float fillLight = 0.7f;
        [Tooltip("The fill light's colour.")]
        public Color fillColour = new Color(1f, 0.95f, 0.88f);

        [Header("The hold on the lane")]
        [Min(0f), Tooltip("Share of the drift off the lane taken away per second. 0 = the body is not brought back (only kept from drifting further).")]
        public float laneSnap = 10f;
        [Min(0f), Tooltip("The fastest the body is slid back onto the lane, own units a second (a sprint is 11). A toy that walks in at the hallway's wall is 9 off the lane: at 12 it is on the lane in under a second. 0 = no limit.")]
        public float laneSnapSpeed = 12f;

        private static FlatViewSettings _defaults;

        /// <summary>Resources/FlatView, or the numbers above when the asset is not there (a copy of the project where the
        /// builder has not run).</summary>
        public static FlatViewSettings Load()
        {
            var asset = Resources.Load<FlatViewSettings>("FlatView");
            if (asset != null) return asset;
            if (_defaults == null) { _defaults = CreateInstance<FlatViewSettings>(); _defaults.hideFlags = HideFlags.HideAndDontSave; }
            return _defaults;
        }
    }
}
