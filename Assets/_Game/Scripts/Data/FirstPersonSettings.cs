using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The first-person body's feel, Half-Life 2 style, as numbers. Every length and speed is for a body at shift scale 1
    /// (the toy, 1.8 units tall); the body multiplies by the Shifter's scale, so first person at kid size is the same body
    /// thirty-five times bigger. Defaults are HL2's own values turned into metres (72 units = 1.83 m).
    /// With Match Third Person on (D-0175) the body's top speeds, its jump and its fall are not the numbers here but the
    /// third-person toy's, so the same toy moves the same in either view; how it gets up to speed and slows down
    /// (Accelerate, Friction, the air's rules) stays HL2's.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/First Person Settings", fileName = "FirstPersonSettings")]
    public sealed class FirstPersonSettings : ScriptableObject
    {
        [Header("Body (units at scale 1)")]
        [Tooltip("Standing capsule height. Matches the toy's CharacterController.")] public float standHeight = 1.8f;
        [Tooltip("Crouched capsule height (HL2: half).")] public float crouchHeight = 0.9f;
        public float radius = 0.3f;
        [Tooltip("Eye height standing, from the feet.")] public float standEye = 1.62f;
        [Tooltip("Eye height crouched, from the feet.")] public float crouchEye = 0.75f;
        [Tooltip("Seconds the view takes to drop into or rise out of a crouch.")] public float duckTime = 0.25f;
        [Tooltip("Highest step walked up without jumping (HL2: 18 units).")] public float stepHeight = 0.45f;
        [Range(0f, 89f)] public float slopeLimit = 45f;

        [Header("The same speeds in both views (D-0175)")]
        [Tooltip("On: first person moves at the third-person toy's numbers, read from where third person keeps them, so the two views cannot drift apart: the walk, the sprint, the jump's height, gravity, the fastest fall and the shove on a loose thing (Data/MovementSettings), the crouched speed and the slower walk with no sprint while aim is held (Resources/PlayerMoves: Crouch Speed, Aim Move Share). The body's own Run Speed, Sprint Speed, Crouch Speed Multiplier, Gravity, Jump Height and Max Fall Speed below are then not used. Off: the body's own numbers below.")]
        public bool matchThirdPerson = true;

        [Header("Ground speeds (units per second at scale 1)")]
        [Tooltip("HL2's normal speed (190). Not used while Match Third Person is on.")] public float runSpeed = 4.8f;
        [Tooltip("HL2's suit sprint (320), held Sprint. Not used while Match Third Person is on.")] public float sprintSpeed = 8.1f;
        [Range(0.1f, 1f), Tooltip("Share of the run speed while crouched (HL2: a third). Not used while Match Third Person is on.")] public float crouchSpeedMultiplier = 0.34f;
        [Tooltip("How hard the body speeds up toward the wished speed (Source sv_accelerate).")] public float accelerate = 10f;
        [Tooltip("Ground friction (Source sv_friction).")] public float friction = 4f;
        [Tooltip("Below this speed friction acts as if moving at it, so stops are crisp (Source sv_stopspeed, 100 units).")] public float stopSpeed = 2.5f;

        [Header("Air")]
        [Tooltip("Air acceleration (Source sv_airaccelerate).")] public float airAccelerate = 10f;
        [Tooltip("Cap on the wished speed used in the air (HL2: 30 units): little air control, but strafing still curves the flight.")]
        public float airSpeedCap = 0.76f;
        [Tooltip("Gravity, units per second squared at scale 1 (HL2: 600 units). Not used while Match Third Person is on.")] public float gravity = 15.2f;
        [Tooltip("Jump height standing (HL2: about 21 units). Crouching in the air lifts the feet by the crouch difference on top. Not used while Match Third Person is on (the lift from crouching in the air still is).")]
        public float jumpHeight = 0.55f;
        [Tooltip("The fastest the body falls, units per second at scale 1. Not used while Match Third Person is on.")]
        public float maxFallSpeed = 50f;

        [Header("View")]
        [Tooltip("HL2-style field of view: horizontal degrees on a 4:3 screen (90). Wider screens keep the same vertical angle and see more at the sides.")]
        public float fieldOfView = 90f;
        [Tooltip("Field of view while a drawn weapon is aimed (Aim held), the same kind of degrees. A weapon's own Sight Field Of View overrides it. The test room narrows 80 to 50.")]
        public float aimFieldOfView = 56f;
        [Tooltip("How quickly the view narrows and widens again. Higher is snappier.")] public float aimBlend = 8f;
        [Range(0f, 1f), Tooltip("How much the look slows as the view narrows for aim (D-0174). 1: the mouse and the stick turn the view less by as much as the view has narrowed, so a target crosses the screen at the same speed for the same hand movement through a sight as without one (90 narrowed to the pistol's 72: the turn is 0.73 of the usual). 0: the same turn as always, which carried a target up to one and a half times faster across the screen through a sight.")]
        public float aimLookMatch = 1f;
        public float mouseSensitivity = 0.1f;
        public float padSensitivity = 200f;
        public bool invertY;
        [Range(60f, 89.9f)] public float maxPitch = 89f;
        [Tooltip("Subtle head bob while walking.")] public bool viewBob = true;
        [Tooltip("Bob height, units at scale 1.")] public float bobAmount = 0.025f;
        [Tooltip("Bobs per unit walked, at scale 1.")] public float bobPerUnit = 0.32f;
        [Tooltip("Near clip at scale 1.")] public float nearClip = 0.04f;

        [Header("Physics pickup (HL2 hands)")]
        [Tooltip("How far E reaches for a prop, units at scale 1.")] public float grabReach = 2.4f;
        [Tooltip("Heaviest Rigidbody mass the hands can lift.")] public float maxLiftMass = 20f;
        [Tooltip("Largest size (bounds' longest side) liftable, units at scale 1. Bigger things stay put.")] public float maxLiftSize = 1.6f;
        [Tooltip("Where a held thing floats in front of the eyes, units at scale 1 (pushed further out for big things).")] public float holdDistance = 1.3f;
        [Range(0.05f, 1f), Tooltip("How much of the gap to the hold point the held thing closes per physics step: the spring's stiffness.")]
        public float holdStiffness = 0.35f;
        [Tooltip("Fastest a held thing chases the hold point, units per second at scale 1.")] public float maxHoldSpeed = 25f;
        [Tooltip("If the held thing is jammed this far from the hold point (units at scale 1) for a moment, the hands let go.")]
        public float breakDistance = 1.2f;
        [Tooltip("Throw speed for light things, units per second at scale 1.")] public float throwSpeed = 12f;
        [Tooltip("Things heavier than this are thrown slower, in proportion.")] public float throwFullSpeedMass = 4f;
        [Tooltip("Degrees per mouse-wheel notch the held thing turns about the vertical.")] public float wheelRotateStep = 15f;
        [Tooltip("Degrees per mouse pixel while the rotate key is held.")] public float mouseRotateSensitivity = 0.4f;
    }
}
