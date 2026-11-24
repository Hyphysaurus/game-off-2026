using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The line a shot leaves when it lands at once (D-0172). A foam dart is seen flying; an instant shot is at its mark
    /// before a dart could be drawn on the way, so the gun draws one thin bright line from the muzzle to the mark and
    /// lets it fade in a blink. It borrows the dart's own streak look (material, colour, width), so it reads as the same
    /// ammo. The lines are kept and used again; nothing is made per shot once a few exist.
    /// </summary>
    public sealed class ShotStreak : MonoBehaviour
    {
        private const float Seconds = 0.09f;   // how long a line shows: long enough to see, short enough for a fast gun
        private const float Wider = 1.6f;      // the line is drawn this many times the dart's own streak width, so it reads at range
        private const int Kept = 32;           // the most lines kept for reuse (a shotgun draws eight at once)

        private static readonly List<ShotStreak> Lines = new List<ShotStreak>();

        private LineRenderer _line;
        private float _until;
        private Color _colour;

        /// <summary>Draws one line from <paramref name="from"/> to <paramref name="to"/> in the look of
        /// <paramref name="like"/>'s own streak. Nothing is drawn for ammo that has no streak (paint draws its own).</summary>
        public static void Show(Projectile like, Vector3 from, Vector3 to, float scale)
        {
            if (like == null) return;
            Material look = like.StreakMaterial;
            if (look == null) return;
            ShotStreak line = Free();
            line._line.sharedMaterial = look;
            line._line.widthMultiplier = like.StreakWidth * scale * Wider;
            line._colour = like.StreakColour;
            line._line.startColor = line._line.endColor = line._colour;
            line._line.SetPosition(0, from);
            line._line.SetPosition(1, to);
            line._until = Time.time + Seconds;
            line.gameObject.SetActive(true);
        }

        private static ShotStreak Free()
        {
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (Lines[i] == null) { Lines.RemoveAt(i); continue; }   // its scene went
                if (!Lines[i].gameObject.activeSelf) return Lines[i];
            }
            if (Lines.Count >= Kept) return Lines[0];   // every line is showing: the oldest is taken over
            var holder = new GameObject("ShotStreak") { hideFlags = HideFlags.DontSave };
            holder.SetActive(false);
            var made = holder.AddComponent<ShotStreak>();
            made._line = holder.AddComponent<LineRenderer>();
            made._line.positionCount = 2;
            made._line.useWorldSpace = true;
            made._line.numCapVertices = 2;
            made._line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            made._line.receiveShadows = false;
            Lines.Add(made);
            return made;
        }

        private void Update()
        {
            float left = _until - Time.time;
            if (left <= 0f) { gameObject.SetActive(false); return; }
            Color c = _colour;
            c.a *= left / Seconds;
            _line.startColor = _line.endColor = c;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void StartClean() => Lines.Clear();
    }
}
