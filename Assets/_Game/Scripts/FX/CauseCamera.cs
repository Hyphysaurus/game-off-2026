using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Shows what a switch did (D-0046): when a switch opens a door, or something wakes a dormant core, the camera turns
    /// from where it stands to look at it (and narrows the view to frame it) for about a second while it happens, then
    /// turns back. The player holds still meanwhile. Once per thing per scene load; skipped when the thing is already in
    /// plain view. It only turns, never moves, so it cannot end up inside a wall. Runs after the shoulder camera each
    /// frame and blends from that camera's own pose. Made by code at start, lives across scenes.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class CauseCamera : MonoBehaviour
    {
        private const float TurnIn = 0.45f, Hold = 1.1f, TurnOut = 0.45f;
        private const float InViewDegrees = 22f, FrameShare = 0.35f, MinFov = 18f;

        private readonly HashSet<Object> _shown = new HashSet<Object>();
        private Camera _cam; private PlayerInput _input;
        private Vector3 _target; private float _targetSize;
        private float _t = -1f;
        private bool _busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<CauseCamera>() != null) return;
            var go = new GameObject("CauseCamera");
            DontDestroyOnLoad(go);
            go.AddComponent<CauseCamera>();
        }

        private void OnEnable()
        {
            GameEvents.SwitchPressed += OnSwitchPressed;
            GameEvents.SourceAwakened += OnSourceAwakened;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            GameEvents.SwitchPressed -= OnSwitchPressed;
            GameEvents.SourceAwakened -= OnSourceAwakened;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m) { StopAllCoroutines(); Release(); _shown.Clear(); }

        private void OnSwitchPressed(Switch pressed)
        {
            foreach (Door door in pressed.Doors) if (door != null) { Show(door); return; }   // the first door tells the story
        }

        private void OnSourceAwakened(ShiftSource source) => Show(source);

        private void Show(Component thing)
        {
            if (_busy || !_shown.Add(thing)) return;
            if (GameMode.Current != GameMode.Mode.Play || PauseMenu.IsOpen || TitleMenu.IsOpen) return;
            var reveal = FindFirstObjectByType<RoomReveal>();
            if (reveal != null && reveal.Running) return;
            _cam = Camera.main;
            if (_cam == null) return;

            var renderers = thing.GetComponentsInChildren<Renderer>();
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(thing.transform.position, Vector3.one);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            _target = b.center;
            _targetSize = Mathf.Max(b.size.magnitude, 0.5f);
            // Already in plain view: nothing to show.
            if (Vector3.Angle(_cam.transform.forward, _target - _cam.transform.position) < InViewDegrees) return;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _busy = true;
            // A frame later: the press that caused this is still being handled, and its input must not be switched off mid-call.
            yield return null;
            var player = GameObject.Find("Player");
            _input = player != null ? player.GetComponent<PlayerInput>() : null;
            if (_input != null) _input.enabled = false;
            _t = 0f;
            float total = TurnIn + Hold + TurnOut;
            while (_t < total) { _t += Time.deltaTime; yield return null; }
            Release();
        }

        private void Release()
        {
            if (_input != null) _input.enabled = true;
            _input = null;
            _t = -1f;
            _busy = false;
        }

        private void LateUpdate()
        {
            if (_t < 0f || _cam == null) return;
            float w = _t < TurnIn ? Smooth(_t / TurnIn) : _t < TurnIn + Hold ? 1f : 1f - Smooth((_t - TurnIn - Hold) / TurnOut);
            Vector3 eye = _cam.transform.position;
            Quaternion look = Quaternion.LookRotation(_target - eye, Vector3.up);
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, look, w);
            // Frame it: the thing fills about a third of the view's height, never wider than the camera's own view.
            float distance = Mathf.Max(0.01f, (_target - eye).magnitude);
            float frame = Mathf.Clamp(2f * Mathf.Atan(_targetSize / (2f * distance * FrameShare)) * Mathf.Rad2Deg, MinFov, _cam.fieldOfView);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, frame, w);
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}
