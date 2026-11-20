using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The paper-plane glider (D-0095): how a toy flies on it, how it looks and how it sounds. Speeds are in the player's
    /// own units a second (toy units at toy size), as in MovementSettings; the motor scales them. One asset for every
    /// scene: Resources/Glide, made by Shift Jam's course builder (LandGlider), his to tune in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Glide Settings", fileName = "Glide")]
    public sealed class GlideSettings : ScriptableObject
    {
        [Header("Flight (own units a second)")]
        [Tooltip("The steady fall while gliding. A free fall of 14 (the fall rule) lands at about 26.5; this lands soft.")]
        public float sinkSpeed = 2.5f;
        [Tooltip("Forward speed with the stick pushed all the way (the toy walks 5.5 and sprints 8).")]
        public float glideSpeed = 9f;
        [Range(0f, 1f), Tooltip("Share of the glide speed kept with the stick at rest: a paper plane never stops in the air.")]
        public float idleShare = 0.55f;
        [Tooltip("How fast the forward speed moves toward what the stick asks, own units a second per second.")]
        public float acceleration = 10f;
        [Tooltip("Degrees a second the heading turns toward where the stick points.")]
        public float turnRate = 150f;
        [Tooltip("How fast a faster fall (or a rise) is brought to the sink speed once the glider is open, own units a second per second.")]
        public float brake = 45f;
        [Tooltip("The glider does not open nearer the ground than this (own units): a Jump pressed just before landing stays a jump.")]
        public float minOpenHeight = 3f;

        [Header("Look")]
        [Tooltip("The model the toy rides (the Playroom pack's Paper_Plane_01). Its colliders are taken off.")]
        public GameObject planePrefab;
        [Tooltip("Turns the model so its nose points ahead (+Z) and its top is up. Set by the builder from the mesh.")]
        public Vector3 planeEuler;
        [Tooltip("The plane's length in the player's own units (the toy is 1.8 tall).")]
        public float planeLength = 5.4f;
        [Tooltip("Where the feet stand along the plane, a share of its length behind its middle.")]
        public float standBack = 0.08f;
        [Tooltip("How far the feet sink into the plane's top, a share of its length (its top is the wing tips' height).")]
        public float sinkIn = 0.04f;
        [Tooltip("Bank on a turn at the full turn rate, degrees.")]
        public float bankDegrees = 24f;
        [Tooltip("The nose follows the flight path by this share of its angle...")]
        public float pitchShare = 0.5f;
        [Tooltip("...and never more than this, degrees.")]
        public float maxPitch = 18f;
        [Tooltip("The camera pulls back by this share of its distance while gliding.")]
        public float cameraPullBack = 0.35f;

        [Header("Sound")]
        [Tooltip("The paper plane opening under the toy (and taken from the desk).")]
        public CourseSounds.Cue open;
        [Tooltip("Folded: landed, or Jump pressed again.")]
        public CourseSounds.Cue fold;
        [Tooltip("Played while gliding, louder with speed.")]
        public AudioClip windLoop;
        [Range(0f, 1f)] public float windVolume = 0.35f;

        public static GlideSettings Load() => Resources.Load<GlideSettings>("Glide");
    }
}
