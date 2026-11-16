using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Puts a car in trouble back on the road during a race with a field (D-0061), so nobody is out of the race for
    /// good. A rival is put back where it had got to when it has made no headway for a while, has left the road, lies
    /// on its roof, or is wrecked. The PLAYER's car is put back only when it is wrecked or has left the road, and at the
    /// last gate it took (the line when it has taken none this lap), so a rescue is never a short cut. A car is set down
    /// on the RacePath facing the way the road runs, already rolling, to the side when another car is on that spot or is
    /// about to come through it (with no spot free it waits a moment). A
    /// wreck is made whole first (the car's own reset). Counted on the Racer; the numbers are in RivalSettings.
    /// </summary>
    [DefaultExecutionOrder(-15)]   // after the grid has followed every car this step, before the rivals steer
    public sealed class RaceRescue : MonoBehaviour
    {
        /// <summary>What is being timed for one car.</summary>
        private sealed class Watch
        {
            public float Mark, MarkAt;      // the headway it had made, and when
            public float OffRoad, Flipped, Wrecked;
        }

        private static readonly float[] Sideways = { 0f, 1f, -1f, 2f, -2f };   // tried in turn, in car clearances

        private readonly Dictionary<Racer, Watch> _watches = new Dictionary<Racer, Watch>();
        private RivalSettings _settings;

        private void Awake() { _settings = RivalSettings.Load(); }
        private void OnEnable() { RaceField.Opened += Forget; }
        private void OnDisable() { RaceField.Opened -= Forget; }
        private void Forget() => _watches.Clear();

        private void FixedUpdate()
        {
            var stage = RaceField.Now;
            if (stage != RaceField.Stage.Racing && stage != RaceField.Stage.Done) return;
            RacePath path = RaceField.Path;
            if (path == null || !path.Ready) return;
            float dt = Time.fixedDeltaTime, now = Time.time;
            var racers = RaceField.Racers;
            for (int i = 0; i < racers.Count; i++)
            {
                Racer r = racers[i];
                if (r.Home || r.Car == null || r.Car.Parked) continue;
                if (r.IsPlayer && r.Finished) continue;   // the player's race is over: the car is the player's again
                if (!_watches.TryGetValue(r, out Watch w)) { w = new Watch { Mark = r.Odometer, MarkAt = now }; _watches[r] = w; }
                string trouble = Trouble(r, w, path, dt, now);
                if (trouble != null && PutBack(r, path, trouble)) { w.Mark = r.Odometer; w.MarkAt = now; w.OffRoad = w.Flipped = w.Wrecked = 0f; }
            }
        }

        /// <summary>What is wrong with a car, in a word, once it has been wrong for long enough; else null.</summary>
        private string Trouble(Racer r, Watch w, RacePath path, float dt, float now)
        {
            float s = r.Car.Scale;
            w.Wrecked = r.Car.Wrecked ? w.Wrecked + dt : 0f;
            if (r.Car.Wrecked) return w.Wrecked >= _settings.wreckSeconds ? "wrecked" : null;

            bool off = Mathf.Abs(r.Lateral) > path.HalfWidthAt(r.Distance) + _settings.offRoadMargin * s || r.Height < -_settings.offRoadDrop * s;
            w.OffRoad = off ? w.OffRoad + dt : 0f;
            if (w.OffRoad >= (r.IsPlayer ? _settings.playerOffRoadSeconds : _settings.offRoadSeconds)) return "off the road";
            if (r.IsPlayer) return null;   // the player may stop, turn round or lie on the roof as long as they like

            path.Sample(r.Distance, out _, out Vector3 roadUp, out _);
            w.Flipped = Vector3.Dot(r.Car.transform.up, roadUp) < 0f ? w.Flipped + dt : 0f;
            if (w.Flipped >= _settings.flippedSeconds) return "on its roof";

            if (r.Odometer - w.Mark >= _settings.stuckProgress * s) { w.Mark = r.Odometer; w.MarkAt = now; }
            else if (now - w.MarkAt >= _settings.stuckSeconds) return "stuck";
            return null;
        }

        /// <summary>Sets the car down on the road. False when every spot there is taken (it is tried again next step).</summary>
        private bool PutBack(Racer r, RacePath path, string trouble)
        {
            float s = r.Car.Scale;
            float distance = r.Distance;
            if (r.IsPlayer)
            {
                RaceLoop line = RaceField.Line;
                Transform gate = line != null ? line.LastPassed : null;
                distance = (gate != null ? path.Nearest(gate.position) : path.LineDistance) + _settings.rescueAhead * s;
            }
            path.Sample(distance, out Vector3 road, out Vector3 up, out Vector3 forward);
            Vector3 right = Vector3.Cross(up, forward);
            float half = Mathf.Max(0f, path.HalfWidthAt(distance) - _settings.wallMargin * s);
            float own = r.IsPlayer ? 0f : Mathf.Clamp(r.Lateral, -half, half);
            for (int k = 0; k < Sideways.Length; k++)
            {
                float lateral = own + Sideways[k] * _settings.carClearance * s;
                if (Mathf.Abs(lateral) > half) continue;
                Vector3 spot = road + right * lateral + up * (_settings.rescueLift * s);
                if (!Free(r, spot, distance, lateral, path, s)) continue;

                if (r.Car.Wrecked) r.Car.ResetState();   // whole again (it goes to its start for a moment, then here)
                float speed = Mathf.Min(r.Car.TopSpeed, path.SpeedAt(distance) * s) * _settings.rescueSpeed;
                if (!r.Car.Teleport(spot, Quaternion.LookRotation(forward, up), forward * speed)) return false;
                r.Reseat(path, distance);
                r.Lateral = lateral; r.Height = _settings.rescueLift * s; r.Forward = forward; r.Speed = speed;
                r.Rescues++;
                r.LastTrouble = trouble;
                if (r.IsPlayer && Camera.main != null && Camera.main.TryGetComponent(out ShoulderCamera cam))
                    cam.Warp(Quaternion.LookRotation(forward, up).eulerAngles.y);   // the view jumps with the car, as through a portal
                RaceField.TellRescued(r);
                return true;
            }
            return false;
        }

        /// <summary>No other car of the race is on the spot, and none is coming down that lane fast enough to be there
        /// within settings.rescueNotice.</summary>
        private bool Free(Racer who, Vector3 spot, float distance, float lateral, RacePath path, float scale)
        {
            float reach = _settings.rescueClear * scale;
            var racers = RaceField.Racers;
            for (int i = 0; i < racers.Count; i++)
            {
                Racer other = racers[i];
                if (other == who || other.Car == null || !other.Car.gameObject.activeInHierarchy) continue;
                if ((other.Car.transform.position - spot).sqrMagnitude < reach * reach) return false;
                float toSpot = path.Delta(other.Distance, distance);   // positive: the spot is ahead of that car
                if (toSpot > 0f && toSpot < _settings.carLength * scale + _settings.rescueNotice * Mathf.Max(0f, other.Speed)
                    && Mathf.Abs(other.Lateral - lateral) < _settings.carClearance * scale) return false;
            }
            return true;
        }
    }
}
