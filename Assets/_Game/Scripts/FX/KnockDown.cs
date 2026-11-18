using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What a target shows and sounds like as it goes down, whatever knocked it down (a dart, paint, a grenade, a
    /// fist): a burst of bits thrown up from where the last hit landed, a bigger squash than its usual one, and a
    /// thud where it stands. The practice targets and both toy soldiers call it from their own death; each still does
    /// its own toppling. The burst and the bigger squash are ours; the thud is the test room's (its dummy and its
    /// target crate each play a sound as they go down). Bits and sound go through the scene's paint keeper, like a
    /// dart's. Numbers and clips in Resources/WeaponAmmo > A target going down; the clips are licensed and live under
    /// _Imports, so a project without them goes down silently.
    /// </summary>
    public static class KnockDown
    {
        /// <summary>Knock-downs shown since the game started (for checks).</summary>
        public static int Shown { get; private set; }

        /// <summary><paramref name="body"/> is the target (its feet and its size), <paramref name="hit"/> the hit that
        /// finished it, <paramref name="part"/> the part of it that squashes (the one its Damageable squashes), and
        /// <paramref name="figure"/> says which thud: a toy soldier's, or a practice target's.</summary>
        public static void Show(Transform body, in DamageInfo hit, Transform part, bool figure)
        {
            if (body == null) return;
            WeaponAmmoSettings s = WeaponAmmoSettings.Load();
            float scale = Mathf.Max(0.001f, body.lossyScale.y);
            Shown++;
            // The Damageable has just squashed the same part by its usual amount: this takes that squash's place.
            if (s.downSquash > 0f) Squash.Hit(part, s.downSquash, s.downSquashSeconds);
            AudioClip clip = s.downVolume > 0f ? (figure ? s.figureDownClip : s.targetDownClip) : null;
            if (s.downBits <= 0 && clip == null) return;
            PaintSplats keeper = PaintSplats.InScene();
            if (s.downBits > 0)
                keeper.Puff(hit.Point, Vector3.up, s.downBitsColour, s.downBits, s.downBitsSpeed * scale, s.downBitsSize * scale, s.downBitsGravity * scale, s.downBitsSeconds);
            if (clip != null) keeper.Play(clip, body.position + Vector3.up * scale, s.downVolume);   // one above its feet, as the test room's
        }
    }
}
