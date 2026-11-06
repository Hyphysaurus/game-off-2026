using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The chase camera's tuning while a vehicle is driven (D-0036). Sizes are in the car's own length and height, so a toy
    /// car and a kid-size car are framed alike. The camera loads Resources/VehicleCamera if none is set on it, so one asset
    /// tunes every scene (Shift Jam > Dev > Create Vehicle Camera Settings makes it). Edits apply live in Play.
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

        [Header("Speed")]
        [Tooltip("Extra field of view at top speed, degrees.")] public float speedFov = 6f;
        [Tooltip("Extra distance at top speed, as a share of the distance.")] public float speedPullBack = 0.15f;

        [Header("Look around")]
        [Tooltip("Seconds after the mouse rests before the camera swings back behind.")] public float recenterDelay = 1f;
        [Tooltip("Degrees per second the camera tilts back to its pitch.")] public float pitchReturn = 50f;

        [Header("Garage (menu up)")]
        [Tooltip("Degrees per second A/D or the left stick orbit the parked car.")] public float showroomOrbitSpeed = 90f;
        [Tooltip("Closest and furthest zoom, as multiples of the distance.")] public Vector2 showroomZoom = new Vector2(0.5f, 2.5f);
        [Tooltip("Zoom change per wheel notch.")] public float showroomZoomStep = 0.1f;

        [Header("Getting out")]
        [Tooltip("Seconds the view blends from the car back to the shoulder.")] public float leaveBlend = 0.35f;
    }
}
