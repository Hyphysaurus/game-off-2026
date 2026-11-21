using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// A friend's playtest report (D-0118), in a built game only (the editor has the play log's F12 mark): F12 saves a
    /// screenshot and a line (when, the scene, where the player stands, the version, the party code and ping) into a
    /// "Bug notes" folder beside the save files, and says on screen where it went. They zip the folder and send it.
    /// </summary>
    public sealed class BugNote : MonoBehaviour
    {
        private static string Folder => Path.Combine(Application.persistentDataPath, "Bug notes");

        private TextMeshProUGUI _toast;
        private float _toastUntil;
        private int _count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Application.isEditor || FindFirstObjectByType<BugNote>() != null) return;
            var go = new GameObject("BugNote");
            DontDestroyOnLoad(go);
            go.AddComponent<BugNote>();
        }

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.f12Key.wasPressedThisFrame) Save();
            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil) _toast.gameObject.SetActive(false);
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var player = GameObject.Find("Player");
                string where = player != null ? player.transform.position.ToString("F1") : "-";
                string line = $"{stamp}\tv{Application.version}\t{SceneManager.GetActiveScene().name}\tat {where}\tparty {NetSession.Code ?? "-"}\tping {NetSession.PingMs} ms";
                File.AppendAllText(Path.Combine(Folder, "notes.txt"), line + Environment.NewLine);
                ScreenCapture.CaptureScreenshot(Path.Combine(Folder, "shot_" + stamp + ".png"));
                _count++;
                Toast($"Note {_count} saved with a screenshot.\n<size=70%>{Folder}  (zip this folder and send it)</size>");
            }
            catch (Exception e)
            {
                Debug.LogWarning("BugNote: " + e.Message);
                Toast("Could not save the note: " + e.Message);
            }
        }

        private void Toast(string text)
        {
            if (_toast == null)
            {
                var root = new GameObject("BugNoteCanvas", typeof(RectTransform));
                root.transform.SetParent(transform, false);
                var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
                var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
                var rt = BuildUiKit.Rect("Toast", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1400f, 90f));
                _toast = BuildUiKit.Label(rt, "", 26, TextAlignmentOptions.Top);
                _toast.outlineWidth = 0.2f; _toast.outlineColor = new Color32(0, 0, 0, 220);
            }
            _toast.text = text;
            _toast.gameObject.SetActive(true);
            _toastUntil = Time.unscaledTime + 5f;
        }
    }
}
