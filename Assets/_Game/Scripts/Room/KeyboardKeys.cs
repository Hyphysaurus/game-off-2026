using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// The desk keyboard as a floor: every key the toy (or a toy car) stands on sinks and clicks, pitched along a
    /// pentatonic scale from left to right, so walking across it plays a tune. A short gamepad rumble on each press;
    /// onKeyDown is for Feel. Keys are the keyboard's children with colliders; nothing to set up per key.
    /// A key stays down while feet are over it (a short ray from the feet, grounded or not) and for a moment after, so a
    /// key never springs back up into the player standing on it: that shove read as a launch on every key (5 Nov).
    /// </summary>
    public sealed class KeyboardKeys : MonoBehaviour
    {
        [SerializeField, Tooltip("How far a key sinks, world units. Keep well under the player's step offset (0.3 at toy size).")] private float keyDepth = 0.15f;
        [SerializeField, Tooltip("Seconds a key stays down after the feet leave it.")] private float holdSeconds = 0.08f;
        [SerializeField] private float pressSeconds = 0.04f, releaseSeconds = 0.1f;
        [SerializeField] private AudioClip[] clicks;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField, Tooltip("Lowest pitch, at the left end.")] private float basePitch = 0.75f;
        [SerializeField] private float rumbleLow = 0.08f, rumbleHigh = 0.25f, rumbleSeconds = 0.05f;
        [SerializeField, Tooltip("Hook Feel here: a squash on the player, a tiny camera bump.")] private UnityEvent onKeyDown;

        private static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 };
        private readonly List<Transform> _keys = new List<Transform>();
        private readonly Dictionary<Transform, int> _index = new Dictionary<Transform, int>();
        private Vector3[] _rest; private float[] _sunk; private float[] _lastTouched; private int[] _note;
        private readonly HashSet<int> _down = new HashSet<int>(), _wasDown = new HashSet<int>();
        private AudioSource _audio; private PlayerMotor _player; private float _rumbleOff = -1f; private int _clickTurn;

        private void Awake()
        {
            foreach (Transform k in transform) if (k.GetComponent<Collider>() != null) { _index[k] = _keys.Count; _keys.Add(k); }
            _rest = new Vector3[_keys.Count]; _sunk = new float[_keys.Count]; _note = new int[_keys.Count];
            _lastTouched = new float[_keys.Count]; for (int i = 0; i < _lastTouched.Length; i++) _lastTouched[i] = -10f;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < _keys.Count; i++) { _rest[i] = _keys[i].localPosition; minX = Mathf.Min(minX, _rest[i].x); maxX = Mathf.Max(maxX, _rest[i].x); }
            for (int i = 0; i < _keys.Count; i++)
            {
                int col = Mathf.RoundToInt(Mathf.InverseLerp(minX, maxX, _rest[i].x) * 14f);   // 15 notes across, three octaves
                _note[i] = Pentatonic[col % 5] + 12 * (col / 5);
            }
            // the click plays from the pressed key: a child of its own, so moving it never moves the keyboard (it did, 5 Nov:
            // every press dragged the whole keyboard so its pivot sat on the key, and it walked up off the desk)
            var voice = new GameObject("KeyVoice"); voice.transform.SetParent(transform, false);
            _audio = voice.AddComponent<AudioSource>(); _audio.playOnAwake = false; _audio.spatialBlend = 0.6f; _audio.rolloffMode = AudioRolloffMode.Linear; _audio.maxDistance = 120f;
        }

        private void Update()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerMotor>();
            _down.Clear();
            // from just above the feet to just below a sunk key: catches the player standing, settling onto a sunk key, or a
            // frame off the ground, but not a jump passing over
            if (_player != null && _player.isActiveAndEnabled) { float up = 0.2f * _player.Scale; Probe(_player.transform.position + Vector3.up * up, up + keyDepth + 0.1f); }
            foreach (var car in ToyCar.All) if (car.Ball != null) Probe(car.Ball.position, car.BallRadius + 0.6f);

            for (int i = 0; i < _keys.Count; i++)
            {
                if (_down.Contains(i)) _lastTouched[i] = Time.time;
                bool down = Time.time - _lastTouched[i] <= holdSeconds;
                float target = down ? keyDepth : 0f;
                _sunk[i] = Mathf.MoveTowards(_sunk[i], target, keyDepth / Mathf.Max(0.005f, down ? pressSeconds : releaseSeconds) * Time.deltaTime);
                _keys[i].localPosition = _rest[i] - Vector3.up * _sunk[i];
                if (down && !_wasDown.Contains(i)) { Press(i); _wasDown.Add(i); }
                else if (!down) _wasDown.Remove(i);
            }
            if (_rumbleOff > 0f && Time.unscaledTime > _rumbleOff) { Gamepad.current?.SetMotorSpeeds(0f, 0f); _rumbleOff = -1f; }
        }

        private void Probe(Vector3 from, float length)
        {
            if (Physics.Raycast(from, Vector3.down, out var hit, length, ~0, QueryTriggerInteraction.Ignore) && _index.TryGetValue(hit.collider.transform, out int i)) _down.Add(i);
        }

        private void Press(int i)
        {
            if (clicks != null && clicks.Length > 0)
            {
                _audio.transform.position = _keys[i].position;
                _audio.pitch = basePitch * Mathf.Pow(2f, _note[i] / 12f);
                _audio.PlayOneShot(clicks[_clickTurn++ % clicks.Length], volume);
            }
            var pad = Gamepad.current; if (pad != null) { pad.SetMotorSpeeds(rumbleLow, rumbleHigh); _rumbleOff = Time.unscaledTime + rumbleSeconds; }
            onKeyDown?.Invoke();
        }

        private void OnDisable() { Gamepad.current?.SetMotorSpeeds(0f, 0f); }
    }
}
