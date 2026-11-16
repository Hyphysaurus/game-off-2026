using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// One car in a race with a field (D-0061): the player's or a rival's. It holds what the race knows about that car:
    /// how often it has crossed the line and which gates it has passed this lap, its lap times and its finish, and where
    /// it is on the RacePath (how far round, how far off the middle, how far it has come since the start). The race
    /// (RaceField, RaceLoop, RaceGate) writes the counts; the grid follows every car on the path once a physics step.
    /// </summary>
    public sealed class Racer
    {
        public readonly ArcadeCar Car;
        public readonly bool IsPlayer;
        /// <summary>The name on the results card.</summary>
        public readonly string Name;
        /// <summary>Its place on the grid, 0 = pole.</summary>
        public readonly int Slot;
        /// <summary>The rival at the wheel; null for the player's car.</summary>
        public RivalDriver Driver;

        // ---- the race ----
        /// <summary>Times over the line since GO. The first only starts lap one, so laps done is one less.</summary>
        public int Crossings;
        /// <summary>Gates passed this lap, in order.</summary>
        public int Gate;
        /// <summary>The race clock when this lap began.</summary>
        public float LapStart;
        public readonly List<float> LapTimes = new List<float>();
        public bool Finished;
        /// <summary>The race clock at its last crossing; for a car still out when the player finished, the time it is
        /// on course for (<see cref="Estimated"/>).</summary>
        public float FinishTime;
        public bool Estimated;
        /// <summary>Its turn over the line at the end, 1 = the first car home (0: not home yet).</summary>
        public int FinishRank;
        /// <summary>1 = leading. Set by RaceField.Refresh.</summary>
        public int Place;
        /// <summary>Its race is over and nobody is at its wheel (back on its grid slot, when the slot was free).</summary>
        public bool Home;
        /// <summary>The clock time (Time.time) at which a finished rival stops its lap of honour.</summary>
        public float HomeAt;

        // ---- on the path ----
        /// <summary>How far round the lap, from the path's first point.</summary>
        public float Distance;
        /// <summary>Right of the road's middle (negative: left) and above the road.</summary>
        public float Lateral, Height;
        /// <summary>The way the road runs where the car is.</summary>
        public Vector3 Forward = Vector3.forward;
        /// <summary>Speed along the road, units per second (negative: going the wrong way).</summary>
        public float Speed;
        /// <summary>How far it has come past the finish line since GO, never wrapped: negative on the grid.</summary>
        public float Odometer;

        // ---- trouble ----
        /// <summary>Times it was put back on the path, and why the last time.</summary>
        public int Rescues;
        public string LastTrouble;

        public int LapsDone => Mathf.Max(0, Crossings - 1);

        public Racer(ArcadeCar car, bool isPlayer, string name, int slot)
        {
            Car = car; IsPlayer = isPlayer; Name = name; Slot = slot;
        }

        /// <summary>Set down on the road (the grid): where it is, measured from the finish line.</summary>
        public void Seat(RacePath path)
        {
            Distance = path.Nearest(Car.transform.position);
            path.Track(Car.transform.position, Distance, out Forward, out Lateral, out Height);
            if (!Finished) Odometer = path.Delta(path.LineDistance, Distance);   // a finished car keeps what it drove
            Speed = 0f;
        }

        /// <summary>One physics step further along the road.</summary>
        public void Follow(RacePath path)
        {
            Vector3 at = Car.transform.position;
            float now = path.Track(at, Distance, out Forward, out Lateral, out Height);
            Odometer += path.Delta(Distance, now);
            Distance = now;
            Speed = Vector3.Dot(Car.Body.linearVelocity, Forward);
        }

        /// <summary>Moved by hand to another part of the road (a rescue): its progress moves with it.</summary>
        public void Reseat(RacePath path, float distance)
        {
            Odometer += path.Delta(Distance, distance);
            Distance = Mathf.Repeat(distance, path.Length);
        }

        /// <summary>
        /// Its standing as one number, bigger is further on: the distance come since the line, held inside the lap its
        /// crossings say it is on. So the moment a car crosses the line it stands ahead of every car that has not, however
        /// close they are and whatever their lengths.
        /// </summary>
        public float Standing(float lap)
        {
            const float hair = 0.001f;
            if (Crossings <= 0) return Mathf.Min(Odometer, -hair);
            return Mathf.Clamp(Odometer, (Crossings - 1) * lap, Crossings * lap - hair);
        }
    }
}
