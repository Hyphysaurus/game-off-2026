using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A quick lean back when something is hit (D-0180): the part tips away from the shot, fast, then eases back
    /// upright. Nothing needs it on a prefab: Flinch.Hit puts one on the part the first time, and it sleeps between
    /// hits, like Squash. It only turns the part (Squash only sizes it), and it adds its lean on top of whatever pose
    /// the part has: if something else turns the part while it leans (a blinded sway), the lean goes on top of that,
    /// and when the lean ends the part is left exactly as that other thing posed it. A second hit mid-lean starts over
    /// from where the part is, so it never pops.
    /// </summary>
    public sealed class Flinch : MonoBehaviour
    {
        /// <summary>Seconds the lean takes to go over, before it eases back.</summary>
        private const float GoOver = 0.05f;

        private Quaternion _base = Quaternion.identity;      // the pose under the lean
        private Quaternion _written = Quaternion.identity;   // what we last set, to tell our own pose from another's
        private Quaternion _from = Quaternion.identity;      // the lean when this hit came (world turn)
        private Quaternion _peak = Quaternion.identity;      // the full lean (world turn)
        private float _seconds, _at;
        private bool _on;

        /// <summary>Leans <paramref name="part"/> back by <paramref name="degrees"/>, away from a shot travelling along
        /// <paramref name="shot"/> (world; only its level part counts), then eases it back upright over
        /// <paramref name="seconds"/>. 0 degrees or 0 seconds does nothing.</summary>
        public static void Hit(Transform part, Vector3 shot, float degrees, float seconds)
        {
            if (part == null || degrees <= 0f || seconds <= 0f || !part.gameObject.activeInHierarchy) return;
            shot.y = 0f;
            if (shot.sqrMagnitude < 1e-8f) return;   // straight up or down: no way to lean
            if (!part.TryGetComponent(out Flinch flinch)) flinch = part.gameObject.AddComponent<Flinch>();
            flinch.Begin(shot.normalized, degrees, seconds);
        }

        /// <summary>Ends a lean at once, the part back in its own pose (a fall, a reset, Build Mode).</summary>
        public static void Stop(Transform part)
        {
            if (part != null && part.TryGetComponent(out Flinch flinch) && flinch.enabled) flinch.enabled = false;
        }

        private void Begin(Vector3 away, float degrees, float seconds)
        {
            if (_on) _from = Lean(Time.time - _at);   // mid-lean: start over from where it is
            else
            {
                _from = Quaternion.identity;
                _base = transform.localRotation;
                _written = _base;
            }
            // Turning about this axis tips the top of the part along the shot, away from where it came from.
            _peak = Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.up, away).normalized);
            _seconds = seconds; _at = Time.time;
            _on = true;
            enabled = true;
        }

        private void LateUpdate()
        {
            if (!_on) { enabled = false; return; }
            Quaternion now = transform.localRotation;
            if (now != _written) _base = now;   // something else posed the part since our last frame: lean from that
            float age = Time.time - _at;
            if (age >= GoOver + _seconds) { enabled = false; return; }   // OnDisable puts its pose back
            Quaternion parent = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            transform.localRotation = Quaternion.Inverse(parent) * Lean(age) * parent * _base;
            _written = transform.localRotation;   // as stored, so the next frame's check is exact
        }

        private void OnDisable()
        {
            if (!_on) return;
            _on = false;
            if (transform.localRotation == _written) transform.localRotation = _base;   // ours: back to the pose under it
        }

        /// <summary>The lean <paramref name="age"/> seconds into a hit, as a world turn: over fast, then eased back.</summary>
        private Quaternion Lean(float age)
        {
            if (age < GoOver)
            {
                float u = age / GoOver;
                return Quaternion.Slerp(_from, _peak, 1f - (1f - u) * (1f - u));
            }
            float back = (age - GoOver) / Mathf.Max(0.01f, _seconds);
            return back >= 1f ? Quaternion.identity : Quaternion.Slerp(_peak, Quaternion.identity, Mathf.SmoothStep(0f, 1f, back));
        }
    }
}
