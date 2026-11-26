#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A footage take's shot list (D-0242, ported from Nectar Rush's recorder): what the camera does, shot after shot, and
    /// what happens in the game while it films. One file per take, docs/footage/&lt;name&gt;.json (in git). FootageRig films
    /// it in play mode, FootageRecorder previews and films it from the editor; this part is only the data and its pure
    /// sums. Editor only: none of it ships.
    ///
    /// Every place is a world point {"x", "y", "z"} measured from the scene (the house has floors at many heights, so
    /// nothing is "over the ground"). A shot is one camera move: "line" (the camera glides from "from" to "to", each an
    /// "at" to stand at and a "look" to look at), "orbit" (it circles "around" a point, "radius" out and "height" over it,
    /// turning from one compass bearing to another: 0 is north (+z), 90 east (+x); or round a "target" that moves, by its
    /// path of names), "follow" (it keeps an "offset" from a "target" that moves, looking at it, catching up over "lag"
    /// seconds) or "game" (the game's own camera, as the player sees it: the opening's shots, the cause camera's turn).
    /// Each shot may first run the game for "before" seconds that are not filmed (a cut that hides a move), and its "steps"
    /// drive the game's own code: setup steps during those seconds, the others so many seconds into the filmed part.
    /// </summary>
    [Serializable]
    public sealed class FootageClip
    {
        /// <summary>A camera spot: where it stands and the point it looks at.</summary>
        [Serializable]
        public sealed class Pose
        {
            [Tooltip("Where the camera stands (world).")] public Vector3 at;
            [Tooltip("The point it looks at (world).")] public Vector3 look;
        }

        /// <summary>A shot waits, unfilmed, for something in the game to have happened a while ago.</summary>
        [Serializable]
        public sealed class WaitFor
        {
            [Tooltip("The path of a Breakable: the shot waits until it has broken. Empty: no wait.")] public string broke = "";
            [Tooltip("Seconds of game time after it broke before the shot is filmed (the gate's come-back is 30).")] public float plus;
        }

        /// <summary>One thing done in the game, through the game's own code.</summary>
        [Serializable]
        public sealed class Step
        {
            [Tooltip("What it does: newGame, place, shift, use, turn, light, look, stun, wake, fire, hit, kick, punch, draw or seen.")]
            public string does = "";
            [Tooltip("Seconds into the filmed part of its shot (ignored for a setup step).")] public float after;
            [Tooltip("A setup step: done in the shot's unfilmed seconds, in list order, each once the one before it has happened.")]
            public bool setup;
            [Tooltip("The thing it acts on, by its path of names from the scene's top (use, turn, light, look, stun, wake, hit).")]
            public string target = "";
            [Tooltip("turn: the thing it turns its beam toward, by its path.")] public string toward = "";
            [Tooltip("place: where the player's feet go (world). hit: when several things share the path, the one nearest this point (0, 0, 0 = the first).")]
            public Vector3 at;
            [Tooltip("place: the way the player faces, degrees (0 north, 90 east).")] public float yaw;
            [Tooltip("place: the way the game's camera looks, degrees; left out, the way the player faces.")] public float view = float.NaN;
            [Tooltip("place: how far the game's camera looks down, degrees; left out, as it was.")] public float pitch = float.NaN;
            [Tooltip("shift: \"toy\" or \"kid\".")] public string size = "";
            [Tooltip("light: on or off. draw: the gun out or put away.")] public bool on = true;
            [Tooltip("seen: the moment marked in the take's save slot (\"bedroom:lights\").")] public string id = "";
            [Tooltip("turn: seconds the turn takes. look: seconds the camera keeps looking.")] public float seconds;
            [Tooltip("hit: how much (0 = all of its health).")] public float damage;
            [Tooltip("hit: where the blow comes from (world; 0, 0, 0 = from straight above).")] public Vector3 from;

            /// <summary>What it does, in lower case.</summary>
            public string Does => (does ?? "").Trim().ToLowerInvariant();
        }

        /// <summary>One shot: a camera move, its length, and what happens meanwhile.</summary>
        [Serializable]
        public sealed class Shot
        {
            [Tooltip("Its name: the shot's frames, video and contact-sheet row are named after it.")] public string name = "";
            [Tooltip("What it shows and where its numbers came from, for people (the take does not read it).")] public string notes = "";
            [Tooltip("line, orbit, follow or game.")] public string move = Line;
            [Tooltip("Seconds filmed.")] public float seconds = 4f;
            [Tooltip("The camera's field of view, degrees (0: the take's). A game shot uses the game camera's own.")] public float fov;
            [Tooltip("An even glide instead of easing in and out.")] public bool linear;
            [Tooltip("line: where it starts.")] public Pose from = new Pose();
            [Tooltip("line: where it ends.")] public Pose to = new Pose();
            [Tooltip("orbit: the point it circles and looks at (world); with a target, an offset from the target.")] public Vector3 around;
            [Tooltip("orbit: how far out from the point.")] public float radius = 10f;
            [Tooltip("orbit: how high over the point the camera stands.")] public float height;
            [Tooltip("orbit: the compass bearing it starts at, degrees (0 north, 90 east).")] public float fromBearing;
            [Tooltip("orbit: the compass bearing it ends at.")] public float toBearing;
            [Tooltip("orbit and follow: the thing that moves, by its path of names.")] public string target = "";
            [Tooltip("follow: where the camera stands from the target (world, not turned with it).")] public Vector3 offset;
            [Tooltip("follow: how high over the target's feet it looks.")] public float lift;
            [Tooltip("follow and orbit round a target: seconds the camera takes to catch up (it smooths the target's steps).")] public float lag = 0.4f;
            [Tooltip("Seconds the game runs, unfilmed, before the shot's first frame (its setup steps happen then).")] public float before;
            [Tooltip("Something to wait for, unfilmed, before the shot's first frame.")] public WaitFor waitFor = new WaitFor();
            [Tooltip("The most unfilmed seconds the shot waits for its setup and its wait; then it films anyway.")] public float most = 30f;
            [Tooltip("This shot shows no overlays (the toy's speech stickers, the opening's black and flash).")] public bool noOverlays;
            [Tooltip("What happens in the game: setup steps before the first frame, the others so many seconds in.")] public Step[] steps = Array.Empty<Step>();

            /// <summary>Frames filmed at <paramref name="fps"/>.</summary>
            public int Frames(int fps) => Mathf.Max(1, Mathf.RoundToInt(seconds * fps));
            /// <summary>The move, in lower case.</summary>
            public string Move => (move ?? "").Trim().ToLowerInvariant();
        }

        public const string Line = "line", Orbit = "orbit", Follow = "follow", Game = "game";

        /// <summary>Every step it knows.</summary>
        public static readonly string[] Steps = { "newgame", "place", "shift", "use", "turn", "light", "look", "stun", "wake", "fire", "hit", "kick", "punch", "draw", "seen" };

        [Tooltip("The take's title (the log and the report).")] public string title = "";
        [Tooltip("The scene it is filmed in (Assets/...unity).")] public string scene = "";
        [Tooltip("Frames a second (the game steps this many times a second of game time, however long a frame takes).")] public int fps = 30;
        [Tooltip("The picture's width, pixels (even).")] public int width = 1920;
        [Tooltip("The picture's height, pixels (even).")] public int height = 1080;
        [Tooltip("The camera's field of view, degrees, for shots that give none.")] public float fov = 50f;
        [Tooltip("The camera's near plane, world units, for the take's own camera moves (the toy is 1.8 tall).")] public float near = 0.05f;
        [Tooltip("Seconds the game runs before anything happens (the title is taken down, the world settles).")] public float warmup = 1f;
        [Tooltip("Objects at the scene's top whose flat screen layers are laid into the picture: the toy's speech stickers (ToastHud), the opening's black and flash (Opening). The HUD is never in it.")]
        public string[] overlays = { "ToastHud", "Opening" };
        [Tooltip("The shots, in order.")] public Shot[] shots = Array.Empty<Shot>();

        public static FootageClip Parse(string json) => JsonUtility.FromJson<FootageClip>(json) ?? new FootageClip();

        /// <summary>Seconds filmed.</summary>
        public float Length
        {
            get
            {
                float total = 0f;
                foreach (Shot shot in shots) if (shot != null) total += shot.Frames(Mathf.Max(1, fps)) / (float)Mathf.Max(1, fps);
                return total;
            }
        }

        /// <summary>Unfilmed seconds at the least: the warm-up and every shot's "before" (a shot that waits may add more).</summary>
        public float Unfilmed
        {
            get
            {
                float total = Mathf.Max(0f, warmup);
                foreach (Shot shot in shots) if (shot != null) total += Mathf.Max(0f, shot.before);
                return total;
            }
        }

        /// <summary>What is wrong with the take, if anything (the recorder neither previews nor films it then).</summary>
        public string[] Problems()
        {
            var problems = new List<string>();
            if (shots == null || shots.Length == 0) problems.Add("it has no shots");
            if (string.IsNullOrEmpty(scene) || !scene.EndsWith(".unity")) problems.Add("it names no scene (\"scene\": \"Assets/...unity\")");
            if (fps < 1 || fps > 120) problems.Add($"{fps} frames a second");
            // The video's colour format halves the picture's size, so both sides must be even.
            if (width < 16 || height < 16 || width % 2 != 0 || height % 2 != 0) problems.Add($"a {width} x {height} picture (both even, at least 16)");
            if (near <= 0f) problems.Add("a near plane of nothing");
            for (int k = 0; shots != null && k < shots.Length; k++)
            {
                Shot shot = shots[k];
                if (shot == null) { problems.Add($"shot {k + 1} is empty"); continue; }
                string named = $"shot {k + 1} ({shot.name})";
                string move = shot.Move;
                if (string.IsNullOrWhiteSpace(shot.name)) problems.Add($"shot {k + 1} has no name");
                if (shot.seconds <= 0f) problems.Add(named + " has no time");
                if (shot.before < 0f || shot.most < 0f) problems.Add(named + " waits less than nothing");
                if (move != Line && move != Orbit && move != Follow && move != Game) problems.Add($"{named} moves \"{shot.move}\" (line, orbit, follow or game)");
                if (move == Line && shot.from.at == shot.from.look) problems.Add(named + " looks from its start spot at that same spot");
                if (move == Line && shot.to.at == shot.to.look) problems.Add(named + " looks from its end spot at that same spot");
                if (move == Orbit && shot.radius <= 0f) problems.Add(named + " orbits at no distance");
                if (move == Follow && string.IsNullOrEmpty(shot.target)) problems.Add(named + " follows nothing (give it a \"target\")");
                if (move == Follow && shot.offset == Vector3.zero) problems.Add(named + " follows from inside its target (give it an \"offset\")");
                if (shot.steps == null) continue;
                foreach (Step step in shot.steps)
                {
                    if (step == null) { problems.Add(named + " has an empty step"); continue; }
                    string does = step.Does;
                    if (Array.IndexOf(Steps, does) < 0) { problems.Add($"{named} has a step that does \"{step.does}\" ({string.Join(", ", Steps)})"); continue; }
                    if (!step.setup && (step.after < 0f || step.after > shot.seconds)) problems.Add($"{named} has a {does} step at {step.after} s, outside its {shot.seconds} s");
                    bool needsTarget = does == "use" || does == "turn" || does == "light" || does == "look" || does == "stun" || does == "wake" || does == "hit";
                    if (needsTarget && string.IsNullOrEmpty(step.target)) problems.Add($"{named} has a {does} step with no target");
                    if (does == "turn" && (string.IsNullOrEmpty(step.toward) || step.seconds <= 0f)) problems.Add($"{named} has a turn step with nothing to turn toward or no seconds");
                    if (does == "look" && step.seconds <= 0f) problems.Add($"{named} has a look step with no seconds");
                    if (does == "place" && step.at == Vector3.zero) problems.Add($"{named} has a place step with nowhere to put the player");
                    if (does == "shift" && step.size != "toy" && step.size != "kid") problems.Add($"{named} has a shift step to \"{step.size}\" (toy or kid)");
                    if (does == "seen" && string.IsNullOrEmpty(step.id)) problems.Add($"{named} has a seen step with no id");
                }
            }
            return problems.ToArray();
        }

        /// <summary>How far through a shot the camera is, 0 to 1, <paramref name="into"/> seconds in: easing in and out unless it is linear.</summary>
        public static float Progress(Shot shot, float into)
        {
            float t = shot.seconds > 0f ? Mathf.Clamp01(into / shot.seconds) : 1f;
            return shot.linear ? t : t * t * (3f - 2f * t);
        }

        /// <summary>A line shot's camera, <paramref name="into"/> seconds in.</summary>
        public static void LineAt(Shot shot, float into, out Vector3 at, out Vector3 look)
        {
            float t = Progress(shot, into);
            at = Vector3.Lerp(shot.from.at, shot.to.at, t);
            look = Vector3.Lerp(shot.from.look, shot.to.look, t);
        }

        /// <summary>An orbit shot's camera round <paramref name="centre"/>, <paramref name="into"/> seconds in: on its circle
        /// at its bearing, its height over the centre, looking at the centre.</summary>
        public static void OrbitAt(Shot shot, float into, Vector3 centre, out Vector3 at, out Vector3 look)
        {
            float bearing = Mathf.Lerp(shot.fromBearing, shot.toBearing, Progress(shot, into)) * Mathf.Deg2Rad;
            at = new Vector3(centre.x + Mathf.Sin(bearing) * shot.radius, centre.y + shot.height, centre.z + Mathf.Cos(bearing) * shot.radius);
            look = centre;
        }

        /// <summary>
        /// Where a line, orbit or follow shot's camera stands and what it looks at, <paramref name="into"/> seconds in.
        /// <paramref name="moving"/> is where its target stands just now (the take smooths it; a preview uses where the scene
        /// has it); without one an orbit circles "around" and a follow keeps its offset from "around".
        /// </summary>
        public static void CameraAt(Shot shot, float into, Vector3? moving, out Vector3 at, out Vector3 look)
        {
            string move = shot.Move;
            if (move == Orbit)
            {
                OrbitAt(shot, into, moving.HasValue ? moving.Value + shot.around : shot.around, out at, out look);
                return;
            }
            if (move == Follow)
            {
                Vector3 centre = moving ?? shot.around;
                at = centre + shot.offset;
                look = centre + Vector3.up * shot.lift;
                return;
            }
            LineAt(shot, into, out at, out look);
        }

        /// <summary>A facing from a camera spot to the point it looks at (straight ahead when they are the same).</summary>
        public static Quaternion Facing(Vector3 at, Vector3 look)
        {
            Vector3 way = look - at;
            if (way.sqrMagnitude < 1e-8f) return Quaternion.identity;
            // Straight down or up has no heading: a hair of north keeps the turn defined.
            if (Mathf.Abs(way.normalized.y) > 0.9999f) way += Vector3.forward * 1e-3f * way.magnitude;
            return Quaternion.LookRotation(way, Vector3.up);
        }

        /// <summary>
        /// The turn, degrees about the world's up through <paramref name="pivot"/> (+ is clockwise seen from above, as a
        /// heading grows), that points a beam leaving <paramref name="origin"/> along <paramref name="forward"/> at
        /// <paramref name="target"/>, seen from above (a turntable's light onto a core). The smallest such turn; 0 when it
        /// already points there or nothing can.
        /// </summary>
        public static float TurnToward(Vector3 pivot, Vector3 origin, Vector3 forward, Vector3 target)
        {
            float best = 0f, bestMiss = Miss(pivot, origin, forward, target, 0f);
            for (float a = -180f; a < 180f; a += 0.5f) Better(a);
            float around = best;
            for (float a = around - 0.5f; a <= around + 0.5f; a += 0.02f) Better(a);
            return bestMiss < 2f ? best : 0f;

            void Better(float a)
            {
                float miss = Miss(pivot, origin, forward, target, a);
                if (miss < bestMiss - 1e-4f || (Mathf.Abs(miss - bestMiss) <= 1e-4f && Mathf.Abs(a) < Mathf.Abs(best))) { bestMiss = miss; best = a; }
            }
        }

        /// <summary>Degrees between the beam's way and the way to the target, seen from above, once turned by <paramref name="turn"/>.</summary>
        private static float Miss(Vector3 pivot, Vector3 origin, Vector3 forward, Vector3 target, float turn)
        {
            Quaternion r = Quaternion.AngleAxis(turn, Vector3.up);
            Vector3 from = pivot + r * (origin - pivot), way = r * forward, to = target - from;
            way.y = 0f; to.y = 0f;
            if (way.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return 180f;
            return Vector3.Angle(way, to);
        }
    }
}
#endif
