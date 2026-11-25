using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The playroom's first look: a camera move that takes over for a few seconds when the toy comes through the mouse
    /// hole. It flies through its child keys in order (each child = a camera pose: move and turn them in the editor to
    /// re-shoot it), letterboxes the screen, brings the room's lights up from dim, swells the music, then hands back
    /// to the shoulder camera with a blend. Plays once per save slot; any key skips after the first second.
    /// While its own music sounds (from the start until its fade-out ends) the scene's music dips under it
    /// (GameEvents.RevealSwell, D-0211).
    /// Runs after the shoulder camera each frame, so the hand-back blends from its own pose into the camera's.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class RoomReveal : MonoBehaviour
    {
        [SerializeField, Tooltip("Remembered per save slot: plays once.")] private string id = "playroom";
        [SerializeField, Tooltip("Seconds along the keys, first to last.")] private float duration = 6f;
        [SerializeField, Tooltip("Seconds blending from the last key into the player's camera.")] private float handBack = 1.1f;
        [SerializeField] private float skipAfter = 1f;
        [SerializeField, Tooltip("These rise from dim to their own brightness during the move.")] private Light[] lights;
        [SerializeField, Range(0f, 1f)] private float lightsFrom = 0.15f;
        [SerializeField] private float lightsDelay = 0.5f, lightsSeconds = 2.8f;
        [SerializeField, Tooltip("Music for the swell (a track's opening); fades out after.")] private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.55f;
        [SerializeField] private float musicFadeOut = 5f;
        [SerializeField, Range(0f, 0.2f)] private float letterbox = 0.11f;
        [SerializeField, Tooltip("Feel: a sting, a lens punch.")] private UnityEvent onStart;
        [SerializeField] private UnityEvent onEnd;

        private Transform[] _keys; private float[] _base; private Camera _cam; private PlayerInput _input;
        private bool _running; private float _t; private RectTransform _top, _bottom; private AudioSource _audio;
        private Coroutine _fade;
        private bool _swelling;   // this reveal's music is sounding: GameEvents.RevealSwell said true and owes a false

        public bool Running => _running;

        /// <summary>Switched off or destroyed mid-way (a scene change, the console): the swell is over, so the scene's
        /// music never stays dipped.</summary>
        private void OnDisable() { _fade = null; Swell(false); }   // switching off stops the coroutines too

        private void Awake()
        {
            _keys = new Transform[transform.childCount];
            for (int i = 0; i < _keys.Length; i++) _keys[i] = transform.GetChild(i);
            _base = new float[lights != null ? lights.Length : 0];
            for (int i = 0; i < _base.Length; i++) if (lights[i] != null) _base[i] = lights[i].intensity;
        }

        /// <summary>Plays the reveal (the mouse hole's portal calls this). Force = even if this slot has seen it.</summary>
        public void Play() => Play(false);
        public void Play(bool force)
        {
            if (_running || _keys.Length < 2) return;
            if (!force && SaveGame.Seen(SaveGame.Slot, id)) return;
            SaveGame.MarkSeen(SaveGame.Slot, id);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _cam = Camera.main; if (_cam == null) yield break;
            var player = GameObject.Find("Player"); _input = player != null ? player.GetComponent<PlayerInput>() : null;
            if (_input != null) _input.enabled = false;
            Bars();
            if (music != null)
            {
                // A replay (the console's "reveal") while the last one's music is still fading: that one goes at once.
                if (_fade != null) { StopCoroutine(_fade); _fade = null; }
                if (_audio != null) Destroy(_audio);
                _audio = gameObject.AddComponent<AudioSource>(); _audio.clip = music; _audio.volume = 0f; _audio.spatialBlend = 0f; _audio.Play();
                Swell(true);
            }
            onStart?.Invoke();
            _running = true; _t = 0f;
            float total = duration + handBack;
            while (_t < total)
            {
                float dt = Time.deltaTime; _t += dt;
                if (_t > skipAfter && _t < duration && AnyKey()) _t = duration;   // skip: straight to the hand-back
                for (int i = 0; i < _base.Length; i++)
                    if (lights[i] != null) lights[i].intensity = _base[i] * Mathf.Lerp(lightsFrom, 1f, Smooth((_t - lightsDelay) / lightsSeconds));
                if (_audio != null) _audio.volume = musicVolume * Smooth(_t / 1.5f);
                float bar = letterbox * Smooth(Mathf.Min(_t / 0.45f, (total - _t) / 0.6f));
                _top.anchorMin = new Vector2(0f, 1f - bar); _bottom.anchorMax = new Vector2(1f, bar);
                if (_input != null && !_input.enabled && _t > duration + handBack * 0.6f) _input.enabled = true;
                yield return null;
            }
            for (int i = 0; i < _base.Length; i++) if (lights[i] != null) lights[i].intensity = _base[i];
            if (_input != null) _input.enabled = true;
            _running = false;
            Destroy(_top.parent.gameObject);
            onEnd?.Invoke();
            if (_audio != null) _fade = StartCoroutine(FadeMusic(_audio));
        }

        private void LateUpdate()
        {
            if (!_running || _cam == null) return;
            // the path: Catmull-Rom through the keys' positions, eased at both ends; turns slerped key to key
            float u = Smooth(Mathf.Clamp01(_t / duration)) * (_keys.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(u), _keys.Length - 2); float f = u - i;
            Vector3 p0 = _keys[Mathf.Max(0, i - 1)].position, p1 = _keys[i].position, p2 = _keys[i + 1].position, p3 = _keys[Mathf.Min(_keys.Length - 1, i + 2)].position;
            Vector3 pos = 0.5f * (2f * p1 + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f);
            Quaternion rot = Quaternion.Slerp(_keys[i].rotation, _keys[i + 1].rotation, Smooth(f));
            float back = Smooth((_t - duration) / handBack);   // 0 on the path, 1 = wholly the player's camera (already placed this frame)
            _cam.transform.SetPositionAndRotation(Vector3.Lerp(pos, _cam.transform.position, back), Quaternion.Slerp(rot, _cam.transform.rotation, back));
        }

        private IEnumerator FadeMusic(AudioSource source)
        {
            float v = source.volume;
            for (float k = 0f; k < 1f; k += Time.deltaTime / musicFadeOut) { source.volume = v * (1f - k); yield return null; }
            Destroy(source);
            if (_audio == source) _audio = null;
            _fade = null;
            Swell(false);
        }

        /// <summary>Tells the scene's music (Course Audio) this reveal's own music is sounding or over: once each way.</summary>
        private void Swell(bool on)
        {
            if (_swelling == on) return;
            _swelling = on;
            GameEvents.RaiseRevealSwell(this, on);
        }

        private void Bars()
        {
            var go = new GameObject("RevealBars", typeof(RectTransform)); go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            _top = Bar(go.transform, "Top", new Vector2(0f, 1f), Vector2.one);
            _bottom = Bar(go.transform, "Bottom", Vector2.zero, new Vector2(1f, 0f));
        }
        private static RectTransform Bar(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>(); img.color = Color.black; img.raycastTarget = false; return rt;
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        private static bool AnyKey()
        {
            var k = UnityEngine.InputSystem.Keyboard.current; var g = UnityEngine.InputSystem.Gamepad.current; var m = UnityEngine.InputSystem.Mouse.current;
            return (k != null && k.anyKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame) || (m != null && m.leftButton.wasPressedThisFrame);
        }
    }
}
