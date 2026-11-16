using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's bodies (the Playroom Male and Female) as numbers: how each is fitted to the player's height, where its
    /// palm is, and what was measured from its clips: how fast each walk and run clip covers ground at normal playback,
    /// how long each one-shot clip is, and the moment a punch or kick is furthest out. Shift Jam / Player / Build Player
    /// Port measures and writes all of it; the cap on leg speed is the one number to tune by hand.
    /// Lives at Resources/PlayerBody.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Player Body Settings", fileName = "PlayerBody")]
    public sealed class PlayerBodySettings : ScriptableObject
    {
        /// <summary>One body: how it is fitted, and what its clips measure.</summary>
        [Serializable]
        public sealed class Body
        {
            [Tooltip("Shown in the Options panel.")] public string name = "Male";
            [Tooltip("The body's object under the Player.")] public string child = "Body_Male";
            [Tooltip("Scale given to the pack's character so it is drawn as tall as the body the player had before (measured; both bodies share one skeleton, so both get the first one's).")] public float fit = 1f;
            [Tooltip("How far the body is raised so its soles sit on the player's feet, own units at toy size (measured).")] public float lift;
            [Tooltip("Palm centre in the right hand bone's own space, rig metres (measured: part-way from the wrist to the middle knuckle).")] public Vector3 palm;

            [Header("Ground covered at 1x playback, own units a second at toy size (measured)")]
            public float walk; public float walkBack; public float strafe;
            public float run; public float runBack; public float runSide;
            public float crouch; public float crouchBack; public float crouchSide;

            [Header("One-shot clips: seconds at 1x playback (measured)")]
            public float roll; public float punch; public float kick; public float block;
            public float hitLight; public float hitHeavy; public float death; public float fidget;
            [Tooltip("Share of the punch clip at which the fist is furthest out (measured on the bones).")] public float punchContact = 0.6f;
            [Tooltip("Share of the kick clip at which the foot is furthest out (measured on the bones).")] public float kickContact = 0.4f;
        }

        public Body[] bodies = Array.Empty<Body>();

        [Header("Legs")]
        [Tooltip("Fastest the run clip is ever played. Sprint asks for more than this (the course's speeds are faster than the clip's stride): the legs stop here and the feet slide for the rest. Between the clip's own speed and sprint the playback rises evenly to this number.")]
        public float maxPlayback = 1.6f;
        [Tooltip("How tall the bodies are fitted to be drawn, soles to top, own units at toy size (measured once from the body the player had before).")]
        public float bodyHeight;

        /// <summary>The asset at Resources/PlayerBody, or null before the builder has run.</summary>
        public static PlayerBodySettings Load() => Resources.Load<PlayerBodySettings>("PlayerBody");

        /// <summary>The body at <paramref name="index"/>, the first one when the index is out of range, null when there are none.</summary>
        public Body At(int index) => bodies.Length == 0 ? null : bodies[Mathf.Clamp(index, 0, bodies.Length - 1)];

        /// <summary>
        /// How fast to play a clip that covers <paramref name="clipSpeed"/> at 1x while the body moves at
        /// <paramref name="speed"/>, when <paramref name="fastest"/> is the fastest the body ever moves on this clip:
        /// feet matched up to the clip's own speed, then rising evenly to the cap (or to a full match when that is lower).
        /// </summary>
        public float Playback(float speed, float clipSpeed, float fastest)
        {
            if (clipSpeed <= 0.001f || speed <= clipSpeed) return 1f;
            float top = Mathf.Min(Mathf.Max(1f, maxPlayback), fastest / clipSpeed);
            return fastest <= clipSpeed ? 1f : Mathf.Lerp(1f, top, Mathf.InverseLerp(clipSpeed, fastest, speed));
        }
    }
}
