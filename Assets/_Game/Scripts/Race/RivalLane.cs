using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Where across the road a rival should be (D-0061): its own lane, moved aside for any car in its way. It works in
    /// the road's own measure (how far round, how far right of the middle), so it is as true on the loop and in a
    /// hairpin as on a straight. A car ahead that it is closing on, or a car alongside, pushes its lane a car's
    /// clearance to the side it is already on, or to the other side when a wall is in the way; with no room on either
    /// side it holds station behind. Until it is out from behind a car it also goes no faster than it can still slow
    /// down from before reaching it, and it does not follow a car closely in line even at the same speed (the car
    /// ahead brakes harder than a rival plans for: seen, a 64-point shunt into a hairpin). Cars standing on the grid
    /// count like any other. It only chooses: RivalDriver steers there.
    /// </summary>
    public static class RivalLane
    {
        private const float DeadBehind = 0.3f;   // side to side, closer than this to a car's middle is dead behind it: no side of its own yet

        /// <summary>
        /// The lane for <paramref name="me"/>: <paramref name="own"/> (its liking, units right of the middle) unless a
        /// car is in the way. <paramref name="half"/> is how far from the middle it may go, <paramref name="scale"/> the
        /// car's size. <paramref name="limit"/> comes back as a speed not to pass while a car is in its way
        /// (float.MaxValue when the road is clear).
        /// </summary>
        public static float Choose(Racer me, IReadOnlyList<Racer> field, RacePath path, RivalSettings settings, float own, float half, float scale, out float limit)
        {
            limit = float.MaxValue;
            float lane = Mathf.Clamp(own, -half, half);
            float clearance = settings.carClearance * scale, length = settings.carLength * scale;
            // twice: moving aside for one car can put the lane in the way of another
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < field.Count; i++)
                {
                    Racer other = field[i];
                    if (other == me || other.Car == null) continue;
                    float ahead = path.Delta(me.Distance, other.Distance);   // positive: it is in front of me
                    float closing = me.Speed - other.Speed;
                    // closer in front than this, it does not sit in line behind a car even at the same speed: that car may brake
                    float tail = length + settings.followSeconds * Mathf.Max(0f, me.Speed);
                    // a car behind is not this one's to avoid: the one behind gives way (else two cars squeeze each other)
                    if (ahead < -settings.giveWay * length || ahead > Mathf.Max(tail, settings.avoidMin * scale + settings.avoidSeconds * Mathf.Max(0f, closing))) continue;
                    if (ahead > tail && closing <= 0f) continue;              // well in front and pulling away
                    if (Mathf.Abs(lane - other.Lateral) >= clearance) continue;

                    // the side I am already on; dead behind it, the side with more road
                    float apart = me.Lateral - other.Lateral;
                    float side = Mathf.Abs(apart) > DeadBehind * scale ? Mathf.Sign(apart) : (other.Lateral > 0f ? -1f : 1f);
                    float want = other.Lateral + side * clearance;
                    if (Mathf.Abs(want) > half)
                    {
                        float across = other.Lateral - side * clearance;
                        bool behind = ahead > length;   // only a car still behind may cross to the other side
                        if (behind && Mathf.Abs(across) <= half) want = across;
                        else
                        {
                            want = Mathf.Clamp(want, -half, half);
                            if (ahead > 0f) limit = Mathf.Min(limit, Mathf.Max(0f, other.Speed));   // no way past: hold station
                        }
                    }
                    lane = want;
                    // still in line behind it (their tracks overlap): no faster than it can slow from in the road left
                    if (ahead > 0f && Mathf.Abs(apart) < settings.carWidth * scale)
                    {
                        float room = Mathf.Max(0f, ahead - length - settings.followAir * scale);
                        limit = Mathf.Min(limit, Mathf.Max(0f, other.Speed) + Mathf.Sqrt(2f * settings.brakeDecel * scale * room));
                    }
                }
            return lane;
        }
    }
}
