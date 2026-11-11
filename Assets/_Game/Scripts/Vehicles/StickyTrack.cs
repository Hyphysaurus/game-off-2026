using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Marks a piece of track a car should hold on to (D-0050): a loop, a steep bank, a spiral. A car's wheels reach only
    /// a hand's width below it, so on a loop a small hop would count as "in the air" and the car would turn itself
    /// upright and fall off. Over a sticky piece the car instead keeps the way up it has and is drawn back onto the road.
    /// The raceway's builder puts one on every piece it lays; the numbers are per piece so a loop can hold harder.
    /// </summary>
    public sealed class StickyTrack : MonoBehaviour
    {
        [SerializeField, Tooltip("How far under the car's wheels the road still counts as held, in car lengths' worth of units at car scale 1.")]
        private float reach = 2.5f;
        [SerializeField, Tooltip("How hard the car is drawn back onto the road while it is within reach but its wheels are off it, units per second squared at car scale 1.")]
        private float pull = 30f;

        /// <summary>How hard a car within reach is drawn onto this piece.</summary>
        public float Pull => pull;

        /// <summary>How far under a car this piece still holds it.</summary>
        public float Reach => reach;

        /// <summary>
        /// The sticky piece under a car whose wheels have left the road, if there is one within that piece's reach.
        /// <paramref name="origin"/> is the car's middle, <paramref name="down"/> the way its wheels point,
        /// <paramref name="scale"/> the car's size, <paramref name="hits"/> a buffer the caller owns (no garbage).
        /// </summary>
        public static StickyTrack Under(Vector3 origin, Vector3 down, float scale, RaycastHit[] hits, Transform car)
        {
            const float longest = 6f;   // no piece reaches further than this; each piece's own reach is checked below
            int count = Physics.RaycastNonAlloc(origin, down, hits, longest * scale, ~0, QueryTriggerInteraction.Ignore);
            StickyTrack best = null; float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(car)) continue;
                if (hits[i].distance >= nearest) continue;
                var track = collider.GetComponentInParent<StickyTrack>();
                // the nearest thing under the car decides: something else between the car and the track means it is not held
                nearest = hits[i].distance;
                best = track != null && hits[i].distance <= track.reach * scale ? track : null;
            }
            return best;
        }
    }
}
