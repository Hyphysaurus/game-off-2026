using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Which car touched a trigger (D-0037), the way PlayerTouch finds the player. A Cyber car's colliders hang off its own
    /// Rigidbody; the wooden car is moved by a hidden physics ball that lives outside the car (ToyCar.Ball), so that one is
    /// looked up. Boost pads, jump pads and nitro pickups ask this.
    /// </summary>
    public static class BoostableTouch
    {
        /// <summary>The car behind <paramref name="other"/>, or null. <paramref name="body"/> is the Rigidbody that moves it
        /// (the car's own, or the wooden car's ball): its velocity is the car's.</summary>
        public static IBoostable Find(Collider other, out Rigidbody body)
        {
            body = other != null ? other.attachedRigidbody : null;
            if (body == null) return null;
            var car = body.GetComponentInParent<IBoostable>();
            if (Alive(car)) return car;
            var toys = ToyCar.All;
            for (int i = 0; i < toys.Count; i++)
            {
                var toy = toys[i];
                if (toy == null || toy.Ball != body.transform) continue;
                var boostable = toy.GetComponent<IBoostable>();
                return Alive(boostable) ? boostable : null;
            }
            return null;
        }

        /// <summary>The car's own transform (its body, not the wooden car's ball): its scale is the car's size.</summary>
        public static Transform TransformOf(IBoostable car) => car is Component c && c != null ? c.transform : null;

        /// <summary>The car's size: 1 at toy scale.</summary>
        public static float ScaleOf(IBoostable car)
        {
            var t = TransformOf(car);
            return t != null ? Mathf.Max(0.001f, Mathf.Abs(t.lossyScale.y)) : 1f;
        }

        // A component found through an interface is compared as a Unity object, so a destroyed one counts as gone.
        private static bool Alive(IBoostable car) => car is Object o && o != null;
    }

    /// <summary>One-shot sounds for the track pieces: a 3D source made on first use, its reach following the piece's size
    /// (about a room's width at toy scale, like the wooden car's roll).</summary>
    internal static class PieceSound
    {
        public static void Play(GameObject owner, ref AudioSource source, AudioClip clip, float volume)
        {
            if (clip == null || owner == null) return;
            if (source == null) source = owner.GetComponent<AudioSource>();
            if (source == null)
            {
                source = owner.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.7f;
                source.rolloffMode = AudioRolloffMode.Linear;
            }
            float s = Mathf.Max(0.001f, Mathf.Abs(owner.transform.lossyScale.y));
            source.minDistance = 2f * s;
            source.maxDistance = 120f * s;
            source.PlayOneShot(clip, volume);
        }
    }
}
