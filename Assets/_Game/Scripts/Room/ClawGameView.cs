using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The claw machine's framed view (D-0229): while a round is on, the player's camera is moved to a shot of the machine
    /// (the glass, then the chute's flap when a prize comes out), with an eased swing in and out. Like CauseCamera and
    /// RoomReveal it runs after the camera's own owner each frame (the shoulder camera, or first person's look at order
    /// 50) and blends from the pose that owner has just set, so the swing back always ends exactly on the player's own
    /// view, wherever that has gone meanwhile; the owner sets the pose and the field of view again every frame, so nothing
    /// is left behind. Order 1001: after CauseCamera and RoomReveal (1000), so a framed shot is never turned by them.
    /// ClawGame tells it what to show; it does nothing else.
    /// </summary>
    [DefaultExecutionOrder(1001)]
    public sealed class ClawGameView : MonoBehaviour
    {
        private Camera _cam;
        private Vector3 _fromPos, _toPos;
        private Quaternion _fromRot = Quaternion.identity, _toRot = Quaternion.identity;
        private float _fromFov = 60f, _toFov = 60f;
        private float _shotT = 1f, _shotSeconds = 1f;      // the swing from one shot to the next
        private float _weight, _goal, _weightSeconds = 1f; // how much of the shot shows over the player's own view

        /// <summary>How much of the framed shot shows now: 0 the player's own view, 1 the shot (eased the same way as drawn).</summary>
        public float Weight => Smooth(_weight);
        /// <summary>True while a shot shows at all (a round, or its swing back).</summary>
        public bool Busy => _cam != null;

        /// <summary>Swings <paramref name="cam"/> from its own view to the shot over <paramref name="seconds"/>.</summary>
        public void Begin(Camera cam, Vector3 position, Quaternion rotation, float fov, float seconds)
        {
            if (cam == null) return;
            bool fresh = _cam != cam || _weight <= 0f;
            _cam = cam;
            if (fresh) { _fromPos = _toPos = position; _fromRot = _toRot = rotation; _fromFov = _toFov = fov; _shotT = _shotSeconds = 1f; }
            else Shot(position, rotation, fov, seconds);
            _goal = 1f; _weightSeconds = Mathf.Max(0.01f, seconds);
        }

        /// <summary>A new shot (the chute's flap): the framed view swings from where it is to it over <paramref name="seconds"/>.</summary>
        public void Shot(Vector3 position, Quaternion rotation, float fov, float seconds)
        {
            if (_cam == null) return;
            CurrentShot(out _fromPos, out _fromRot, out _fromFov);
            _toPos = position; _toRot = rotation; _toFov = fov;
            _shotT = 0f; _shotSeconds = Mathf.Max(0.01f, seconds);
        }

        /// <summary>Swings back to the player's own view over <paramref name="seconds"/>; done, it lets the camera go.</summary>
        public void End(float seconds)
        {
            if (_cam == null) return;
            _goal = 0f; _weightSeconds = Mathf.Max(0.01f, seconds);
        }

        /// <summary>The player's own view at once (a mode change, the machine switched off): nothing more is set.</summary>
        public void Stop()
        {
            _cam = null; _weight = 0f; _goal = 0f;
        }

        private void OnDisable() => Stop();

        private void LateUpdate()
        {
            if (_cam == null) return;
            float dt = Time.deltaTime;
            _weight = Mathf.MoveTowards(_weight, _goal, dt / _weightSeconds);
            if (_shotT < 1f) _shotT = Mathf.Min(1f, _shotT + dt / _shotSeconds);
            if (_weight <= 0f && _goal <= 0f) { _cam = null; return; }   // back on the player's own view: let go
            CurrentShot(out Vector3 pos, out Quaternion rot, out float fov);
            float w = Smooth(_weight);
            Transform t = _cam.transform;
            t.SetPositionAndRotation(Vector3.Lerp(t.position, pos, w), Quaternion.Slerp(t.rotation, rot, w));
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fov, w);
        }

        /// <summary>The shot now: part way from the last shot to the new one.</summary>
        private void CurrentShot(out Vector3 position, out Quaternion rotation, out float fov)
        {
            float k = Smooth(_shotT);
            position = Vector3.Lerp(_fromPos, _toPos, k);
            rotation = Quaternion.Slerp(_fromRot, _toRot, k);
            fov = Mathf.Lerp(_fromFov, _toFov, k);
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}
