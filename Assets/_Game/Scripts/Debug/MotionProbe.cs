#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Editor-only diagnostic. Added to the Player through the bridge while playing; samples body speed, the
    /// animator's Speed, the blended clips and how fast each planted foot skates over the floor, for a few
    /// seconds, then prints one summary to the console and removes itself. Nothing in a build.
    /// </summary>
    public sealed class MotionProbe : MonoBehaviour
    {
        public float seconds = 6f;

        private Animator _anim; private CharacterController _cc; private Shifter _shifter; private PlayerInput _input;
        private Transform _lf, _rf; private Vector3 _lastL, _lastR; private float _lowL = float.MaxValue, _lowR = float.MaxValue;
        private readonly List<string> _rows = new List<string>();
        private readonly List<float> _skateWalk = new List<float>(), _skateSprint = new List<float>();
        private float _start, _nextRow;

        private void Start()
        {
            _anim = GetComponentInChildren<Animator>(); _cc = GetComponent<CharacterController>(); _shifter = GetComponent<Shifter>(); _input = GetComponent<PlayerInput>();
            _lf = _anim.GetBoneTransform(HumanBodyBones.LeftFoot); _rf = _anim.GetBoneTransform(HumanBodyBones.RightFoot);
            _lastL = _lf.position; _lastR = _rf.position; _start = Time.time;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime; if (dt <= 0f) return;
            float scale = _shifter != null ? _shifter.Scale : 1f;
            float body = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude / scale;
            float yl = transform.InverseTransformPoint(_lf.position).y, yr = transform.InverseTransformPoint(_rf.position).y;
            _lowL = Mathf.Min(_lowL, yl); _lowR = Mathf.Min(_lowR, yr);
            float vl = new Vector3(_lf.position.x - _lastL.x, 0f, _lf.position.z - _lastL.z).magnitude / dt / scale;
            float vr = new Vector3(_rf.position.x - _lastR.x, 0f, _rf.position.z - _lastR.z).magnitude / dt / scale;
            _lastL = _lf.position; _lastR = _rf.position;
            float speedParam = _anim.GetFloat("Speed");
            if (body > 1f)
            {
                var list = speedParam > 0.85f ? _skateSprint : _skateWalk;
                if (yl < _lowL + 0.06f) list.Add(vl);
                if (yr < _lowR + 0.06f) list.Add(vr);
            }
            float t = Time.time - _start;
            if (t >= _nextRow)
            {
                _nextRow += 0.5f;
                var sb = new StringBuilder(string.Format("t {0:F1}s body {1:F2} u/s Speed {2:F2} sprint {3}", t, body, speedParam, _input != null && _input.Sprint));
                foreach (var ci in _anim.GetCurrentAnimatorClipInfo(0)) if (ci.weight > 0.05f) sb.Append(" | " + ci.clip.name + " " + ci.weight.ToString("F2"));
                _rows.Add(sb.ToString());
            }
            if (t >= seconds)
            {
                var sb = new StringBuilder("MOTION PROBE\n");
                foreach (var r in _rows) sb.AppendLine(r);
                sb.AppendLine("planted-foot skate while walking: " + Stat(_skateWalk) + " | while sprinting: " + Stat(_skateSprint) + " (u/s at scale 1; 0 = no slide)");
                Debug.Log(sb.ToString());
                Destroy(this);
            }
        }

        private static string Stat(List<float> l)
        {
            if (l.Count == 0) return "no samples";
            l.Sort(); return "median " + l[l.Count / 2].ToString("F2") + " n " + l.Count;
        }
    }
}
#endif
