using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The Flat Zones (D-0198): a stretch of the house seen side-on, played as a side-scroller. How the rail camera
    /// frames it and how the body is held to the lane. Lengths are in the player's own units (toy units at toy size);
    /// the camera scales them. One asset for every zone: Resources/FlatView, made by
    /// Shift Jam > Flat > Land Hallway Scroller when it is missing (never overwritten), his to tune in the Inspector:
    /// the camera reads it every frame, so a number changed in Play shows at once.
    /// The rifts (D-0212): how long the way in stays open behind a player who walked through it, and the way out's
    /// link back to the way in (its rest, where the player lands).
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

        [Header("The rifts (D-0212)")]
        [Min(0f), Tooltip("Seconds a player who walked through a zone's way in (its entrance rift) moving on may still step into the zone's box and be taken. Once the stretch is done for the save slot, that is the only way the zone takes anyone.")]
        public float riftWindow = 1f;
        [Min(0f), Tooltip("Seconds both rifts of a zone rest after the way out has carried a player back to the way in: neither carries anyone back, and the way in takes nobody through it, until they are up. The way out still lets a held player go at any time.")]
        public float linkCooldown = 1.5f;
        [Min(0f), Tooltip("Where a player carried back from the way out lands: this far behind the way in's middle (on the side it is walked into from), world units, on the floor, facing away from it. The hallway's way in stands at x -24 with the hop's 3.5 gap behind it (x -32 to -28.5): 11 lands the body past that gap (x -35, solid floor to x -60), so walking on goes home, not into the drop (D-0212 fix, his 25 Nov play: at 2 the drop was 2.5 ahead and he walked into it).")]
        public float linkLanding = 11f;

        [Header("The letters (D-0208)")]
        [Min(0f), Tooltip("Seconds the S-H-I-F-T strip stays on screen after a letter is found (it also shows the whole time a zone with letters holds the player).")]
        public float stripSeconds = 3f;

        [Header("The pixel look (D-0209: only in a zone with Pixel Look ticked)")]
        [Min(0), Tooltip("How many rows of big pixels the screen is cut into while such a zone holds the player (224 = the SNES's height; fewer = chunkier). The pixels are square: the columns follow the screen's width (398 on a 16:9 screen). 0 = no pixel look anywhere.")]
        public int pixelRows = 224;
        [Range(0f, 1f), Tooltip("How far each big pixel is pulled to the nearest colour of the palette (his title's colours and a few warm ones, Art/Flat/PixelShiftPalette.png): 1 = only those colours, 0 = big pixels in the room's own colours.")]
        public float paletteStrength = 1f;
        [Range(0f, 0.25f), Tooltip("Ordered dither: each big pixel is nudged by up to half this (a share of full brightness, in a 4 by 4 pattern) before it is snapped to the palette, so smooth light breaks into the checkered steps of old games. 0 = flat bands.")]
        public float dither = 0.06f;

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
