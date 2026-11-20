using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What the course sounds like: one cue for each moment the course announces (GameEvents) and the player's jump and
    /// landing, plus the room's music and ambience. CourseAudio plays them. Picked by name from the Playroom pack by
    /// Shift Jam / Audio / Build Course Sounds; his ear decides, here in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Course Sounds", fileName = "CourseSounds")]
    public sealed class CourseSounds : ScriptableObject
    {
        [Serializable]
        public struct Cue
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
            [Tooltip("Each play's pitch is 1 plus or minus up to this, so a repeated sound does not drone.")]
            [Range(0f, 0.3f)] public float pitchSpread;

            public Cue(AudioClip clip, float volume, float pitchSpread = 0f)
            {
                this.clip = clip;
                this.volume = volume;
                this.pitchSpread = pitchSpread;
            }
        }

        [Header("The shift")]
        public Cue grow;
        public Cue shrink;
        [Tooltip("A dormant core lighting up.")] public Cue coreAwake;

        [Header("The room")]
        public Cue switchPress;
        public Cue doorOpen;
        public Cue checkpoint;
        [Tooltip("Put back at the checkpoint: a fatal fall, the floor's edge, a hazard.")] public Cue reset;
        public Cue goal;
        [Tooltip("The Shift key refused (no room, no charge, a no-shift room): a short no.")] public Cue refused;
        [Tooltip("E on something that says no (\"Climb up to reach it\", \"Too small to reach\", \"Too big to hold\", a dark core): softer than Refused.")] public Cue cantUse;

        [Header("Hands")]
        [Tooltip("Something taken into the hands: the torch, the ruler, the safe, a box.")] public Cue pickUp;
        [Tooltip("Something held set down.")] public Cue putDown;
        [Tooltip("A weapon taken from a stand (or off the floor).")] public Cue weaponTaken;

        [Header("The soldiers")]
        [Tooltip("A knocked-down soldier stands up again. Going down plays the target thud (Resources/WeaponAmmo).")] public Cue soldierUp;

        [Header("The end card")]
        [Tooltip("A choice taken on the end card (play again, the playroom). Plays on past the scene change.")] public Cue endChoice;

        [Header("The player")]
        [Header("The opening (New Game)")]
        [Tooltip("The toy box's flaps creaking open over the lying toy.")] public Cue lidOpen;
        [Tooltip("The spark setting off down into the box.")] public Cue sparkFall;
        [Tooltip("The spark reaching the toy: the strike.")] public Cue sparkHit;
        [Tooltip("The toy getting up.")] public Cue getUp;

        public Cue jump;
        public Cue land;
        [Tooltip("Landings softer than this (fall speed, the player's own units a second) make no sound. A jump on flat ground lands " +
                 "at about 10 (jump height 2, gravity 25), so 11 sounds only a drop from higher than your own jump.")]
        public float landAtLeast = 11f;

        [Header("Loops")]
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.3f;
        public AudioClip ambience;
        [Range(0f, 1f)] public float ambienceVolume = 0.2f;
    }
}
