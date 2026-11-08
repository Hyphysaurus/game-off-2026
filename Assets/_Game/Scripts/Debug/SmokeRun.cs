using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// A built player started with -smoketest (D-0040) opens every scene in the build for a few seconds each, counts the
    /// errors and exceptions each one logs, writes one "[smoke]" line per scene to the player log, and quits: exit code 0
    /// when nothing went wrong, 1 when anything did. Does nothing without the flag, so it is safe in every build.
    /// Run: ShiftJam.exe -batchmode -smoketest -logFile smoke.log
    /// </summary>
    public sealed class SmokeRun : MonoBehaviour
    {
        private const float SecondsPerScene = 6f;
        private int _errors;
        private string _first;

        private static bool Asked => Array.IndexOf(Environment.GetCommandLineArgs(), "-smoketest") >= 0;

        /// <summary>Silent before the first scene wakes: a hidden check must never play the rooms on his speakers.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Mute()
        {
            if (Asked) AudioListener.volume = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            if (!Asked) return;
            var go = new GameObject("Smoke run");
            DontDestroyOnLoad(go);
            go.AddComponent<SmokeRun>();
        }

        private void OnEnable() => Application.logMessageReceived += Count;
        private void OnDisable() => Application.logMessageReceived -= Count;

        private void Count(string text, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _errors++;
            if (_first == null) _first = text.Split('\n')[0];
        }

        private IEnumerator Start()
        {
            int total = 0;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                if (i > 0)
                {
                    AsyncOperation load = SceneManager.LoadSceneAsync(i);
                    while (load != null && !load.isDone) yield return null;
                }
                float until = Time.realtimeSinceStartup + SecondsPerScene;
                int frames = 0;
                while (Time.realtimeSinceStartup < until) { frames++; yield return null; }
                Debug.Log($"[smoke] scene {i} '{SceneManager.GetActiveScene().name}': {frames} frames, {_errors} errors" +
                          (_first != null ? $", first: {_first}" : ""));
                total += _errors;
                _errors = 0;
                _first = null;
            }
            Debug.Log($"[smoke] DONE: {SceneManager.sceneCountInBuildSettings} scenes, {total} errors");
            Application.Quit(total > 0 ? 1 : 0);
        }
    }
}
