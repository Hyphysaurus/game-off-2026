using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The chase camera's tuning while a vehicle is driven (D-0036). Sizes are in the car's own length and height, so a toy
    /// car and a kid-size car are framed alike. The camera loads Resources/VehicleCamera if none is set on it, so one asset
    /// tunes every scene (Shift Jam > Dev > Create Vehicle Camera Settings makes it). Edits apply live in Play.
    /// Speed is shown three ways: up to top speed the view widens, moves in and flattens (Speed); past top speed, which
    /// only a boost reaches, it does more of each (Boost); and knocks shake it (Shake, through ShoulderCamera.Shake).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Vehicle Camera Settings", fileName = "VehicleCamera")]
    public sealed class VehicleCameraSettings : ScriptableObject
    {
        [Header("Framing")]
        [Tooltip("How far behind the car the camera sits, in car lengths.")] public float distance = 2f;
        [Tooltip("How high above the car's middle it aims, in car heights.")] public float lookHeight = 0.6f;
        [Tooltip("Degrees the camera looks down once it settles behind.")] public float pitch = 12f;
        [Tooltip("Closest the camera comes when a wall is in the way, in car lengths.")] public float minDistance = 0.35f;

        [Header("Follow")]
        [Tooltip("Seconds the aim trails the car sideways and along (0 = rigid).")] public float positionLag = 0.06f;
        [Tooltip("Seconds the aim trails the car up and down: bumps and landings settle instead of jolting.")] public float heightLag = 0.15f;
        [Tooltip("Seconds the camera takes to swing in behind (smaller = tighter).")] public float turnLag = 0.25f;
        [Tooltip("In the air the swing is this many times slower, so a spin does not whip the view.")] public float airTurnSlowdown = 3f;
        [Tooltip("Above this share of top speed the camera looks where the car is going, below it where the nose points.")]
        [Range(0f, 1f)] public float followTravelFrom = 0.25f;

        [Header("Speed (standing still to top speed)")]
        [Tooltip("Extra field of view at top speed, degrees.")] public float speedFov = 6f;
        [Tooltip("At top speed the camera sits this share of its distance closer. With the wider view the car keeps its size and the room rushes past. (Replaces speedPullBack, which moved it away.)")]
        [Range(0f, 0.5f)] public float speedCloser = 0.1f;
        [Tooltip("At top speed the camera settles this many degrees flatter (lower behind the car).")] public float speedPitch = 3f;

        [Header("Boost (past top speed)")]
        [Tooltip("Share of top speed where the boost look is at its full (nitro is 1.5).")] public float boostAt = 1.5f;
        [Tooltip("Field of view added on top of speedFov at that speed, degrees (6 + 10 = 16 at 1.5x).")] public float boostFov = 10f;
        [Tooltip("Share of the distance the camera moves in on top of speedCloser at that speed.")] [Range(0f, 0.5f)] public float boostCloser = 0.06f;
        [Tooltip("Degrees flatter on top of speedPitch at that speed.")] public float boostPitch = 1.5f;
        [Tooltip("How fast the boost look comes and goes, per second (3 = a third of a second).")] public float boostResponse = 3f;

        [Header("Shake (ShoulderCamera.Shake, amount 0..1)")]
        [Tooltip("Sideways and up-down travel at full shake, in car lengths. Kept inside the camera's own clearance; none while a wall has pushed the camera in.")]
        public float shakeOffset = 0.04f;
        [Tooltip("Tilt at full shake, degrees.")] public float shakeAngle = 2f;
        [Tooltip("How fast it rattles (noise cycles per second).")] public float shakeFrequency = 16f;

        [Header("Look around")]
        [Tooltip("Seconds after the mouse rests before the camera swings back behind.")] public float recenterDelay = 1f;
        [Tooltip("Degrees per second the camera tilts back to its pitch.")] public float pitchReturn = 50f;

        [Header("Garage (menu up)")]
        [Tooltip("Degrees per second A/D or the left stick orbit the parked car.")] public float showroomOrbitSpeed = 90f;
        [Tooltip("Closest and furthest zoom, as multiples of the distance.")] public Vector2 showroomZoom = new Vector2(0.5f, 2.5f);
        [Tooltip("Zoom change per wheel notch.")] public float showroomZoomStep = 0.1f;

        [Header("Holding the road (loops, spirals, banked bends)")]
        [Tooltip("How fast the view moves into, and out of, riding in the road's own frame, per second (4 = a quarter of a second).")] public float holdBlend = 4f;
        [Tooltip("How closely that view follows the car's own turning, per second. Higher = stiffer and shows every bump; lower = smoother but it trails round a loop.")] public float holdFollow = 9f;

        [Header("Getting out")]
        [Tooltip("Seconds the view blends from the car back to the shoulder.")] public float leaveBlend = 0.35f;
    }
}
