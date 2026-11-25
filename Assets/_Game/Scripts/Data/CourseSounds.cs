using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What the course sounds like: one cue for each moment the course announces (GameEvents) and the player's jump and
    /// landing, plus the music (the scene's and the title's, D-0211) and the room's ambience. CourseAudio plays them.
    /// Picked by name from the Playroom pack by Shift Jam / Audio / Build Course Sounds; his ear decides, here in the
    /// Inspector.
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

        [Header("The opening (New Game)")]
        [Tooltip("The toy box's flaps creaking open over the lying toy.")] public Cue lidOpen;
        [Tooltip("The spark setting off down into the box.")] public Cue sparkFall;
        [Tooltip("The spark reaching the toy: the strike.")] public Cue sparkHit;
        [Tooltip("The toy getting up.")] public Cue getUp;

        [Header("The player")]
        [Tooltip("A hop.")] public Cue jump;
        [Tooltip("A landing from higher than the player's own jump (Land At Least).")] public Cue land;
        [Tooltip("Landings softer than this (fall speed, the player's own units a second) make no sound. A jump on flat ground lands " +
                 "at about 10 (jump height 2, gravity 25), so 11 sounds only a drop from higher than your own jump.")]
        public float landAtLeast = 11f;

        // The music (D-0211): one track at a time, chosen every frame by Course Audio: the title's while the title is up,
        // none while the opening plays, else the scene's (its Course Audio's own track, or this one). A change of track
        // crossfades: the old one goes over Music Out while the new one starts from its beginning and comes in over
        // Music In. Under a room reveal's own swell the track dips to silence over Music Dip, waits in place, and comes
        // back up over Music In. An empty clip or a volume of 0 = silence there.
        [Header("Loops")]
        [Tooltip("The scene's music when its Course Audio has no track of its own: the house (D-0211: Calm_Adventure). Empty = no music.")]
        public AudioClip music;
        [Tooltip("The scene's music's volume. 0 = no music.")]
        [Range(0f, 1f)] public float musicVolume = 0.3f;
        [Tooltip("The title screen's music, while the title is up (D-0211: Toy_Room_Menu_Music_v1). Empty = a quiet title.")]
        public AudioClip titleMusic;
        [Tooltip("The title's music's volume. A little over the rooms' tracks: no play sounds under a menu. 0 = a quiet title.")]
        [Range(0f, 1f)] public float titleMusicVolume = 0.44f;
        [Tooltip("Seconds a track takes to come in from its beginning, and to come back up after a reveal's swell. 0 = at once.")]
        [Min(0f)] public float musicIn = 2.5f;
        [Tooltip("Seconds a track takes to go when another takes over or the music stops (Continue, New Game). 0 = a cut.")]
        [Min(0f)] public float musicOut = 1.5f;
        [Tooltip("Seconds the track takes to dip to silence under a room reveal's own music. It waits there, in place, and comes back up over Music In.")]
        [Min(0f)] public float musicDip = 0.8f;
        [Tooltip("The room tone, looping under everything from the scene's start.")]
        public AudioClip ambience;
        [Tooltip("The room tone's volume. 0 = none.")]
        [Range(0f, 1f)] public float ambienceVolume = 0.2f;
    }
}
