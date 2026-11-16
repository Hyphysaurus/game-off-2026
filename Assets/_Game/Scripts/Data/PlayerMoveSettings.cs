using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's moves from the test room, as numbers: crouch, dodge roll, block, punch, kick, hit reactions, death
    /// and the idle fidget. Lengths are in BODY HEIGHTS (the standing capsule, 1.8 at toy size) so every move scales with
    /// the shift; speeds are in the player's own units a second, as MovementSettings has them. Created with these
    /// defaults by Shift Jam / Player / Build Player Port, which never overwrites it afterwards.
    /// Lives at Resources/PlayerMoves.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Player Move Settings", fileName = "PlayerMoves")]
    public sealed class PlayerMoveSettings : ScriptableObject
    {
        /// <summary>A punch or a kick: where the hit ball is, what it takes and how hard it shoves.</summary>
        [Serializable]
        public sealed class Strike
        {
            [Tooltip("How fast the clip plays.")] public float playback = 1.5f;
            [Tooltip("Centre of the hit ball in front of the body, body heights.")] public float reach = 0.39f;
            [Tooltip("Centre of the hit ball above the feet, body heights.")] public float height = 0.65f;
            [Tooltip("Radius of the hit ball, body heights.")] public float width = 0.234f;
            [Tooltip("Health taken from a Damageable.")] public float damage = 10f;
            [Tooltip("Shove, body heights a second at toy size (see shoveGrowsWithSize).")] public float shove = 2.1f;
        }

        [Header("Crouch")]
        [Tooltip("Capsule height while crouched, as a share of the standing height (the test room: 1.6 of 1.92).")]
        [Range(0.3f, 1f)] public float crouchHeight = 0.833f;
        [Tooltip("Top speed while crouched, own units a second at toy size.")] public float crouchSpeed = 1.65f;
        [Tooltip("The stand-up check is pulled in this far at the feet and sides, body heights, so the floor and walls the player rests against never count.")]
        public float standInset = 0.003f;

        [Header("Dodge roll")]
        [Tooltip("How far a roll carries, body heights (the test room: 3.4 m on a 1.92 m body).")] public float rollDistance = 1.77f;
        [Tooltip("How fast the roll clip plays.")] public float rollPlayback = 1.5f;
        [Tooltip("Seconds after a roll ends before the next may start. 0 = none, as in the test room.")] public float rollCooldown;
        [Range(0f, 1f), Tooltip("Damage is ignored from this share of the roll...")] public float rollSafeFrom = 0.2f;
        [Range(0f, 1f), Tooltip("...to this share. Equal numbers switch the protection off.")] public float rollSafeTo = 0.8f;

        [Header("Block")]
        [Range(0f, 1f), Tooltip("Share of incoming damage taken while the block is up. 1 = no protection, 0 = none gets through.")]
        public float blockDamage = 0.5f;

        [Header("Punch and kick (empty hands)")]
        public Strike punch = new Strike();
        public Strike kick = new Strike { reach = 0.47f, height = 0.417f, width = 0.3125f, damage = 20f, shove = 4.7f };
        [Tooltip("Upward part of a shove, as a share of the forward part.")] public float shoveUp = 0.35f;
        [Tooltip("On: a bigger body shoves harder, by the square root of its size (the test room's rule). Off: the toy's shove at any size.")]
        public bool shoveGrowsWithSize = true;
        public LayerMask strikeMask = ~0;

        [Header("Hit reactions")]
        [Tooltip("How fast the light reaction plays.")] public float hitLightPlayback = 1.5f;
        [Tooltip("How fast the heavy reaction plays.")] public float hitHeavyPlayback = 1.3f;
        [Range(0f, 1f), Tooltip("A hit of at least this share of full health plays the heavy reaction.")] public float heavyHitShare = 0.25f;
        [Tooltip("Seconds of a light reaction during which the player cannot act; after it any move input ends the reaction.")] public float hitLightLock = 0.3f;
        [Tooltip("The same for a heavy reaction.")] public float hitHeavyLock = 0.6f;

        [Header("Death")]
        [Tooltip("How fast the death clip plays.")] public float deathPlayback = 1.5f;
        [Tooltip("Seconds after dying before the respawn's cut.")] public float respawnAfter = 2f;

        [Header("Idle")]
        [Tooltip("Seconds standing still before the look-around plays.")] public float fidgetDelay = 8f;

        [Header("Blends")]
        [Tooltip("Seconds to blend into a move's clip and back out of it.")] public float blend = 0.1f;

        /// <summary>The asset at Resources/PlayerMoves, or null before the builder has run.</summary>
        public static PlayerMoveSettings Load() => Resources.Load<PlayerMoveSettings>("PlayerMoves");
    }
}
