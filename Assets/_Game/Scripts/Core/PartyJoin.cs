using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ShiftJam
{
    /// <summary>
    /// Couch party play, 1 to 4 players on one screen (D-0110). The scene's own player is seat 1. A pad that is not
    /// playing yet presses Start and joins: the first player and its camera are copied together (so the copy's camera,
    /// aim and body point at each other, not at the first player's), the copy is paired with that pad, and the screen
    /// splits. Once anyone joins, seat 1 is the keyboard and mouse. Only scenes with this component do any of this:
    /// the course stays single player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyJoin : MonoBehaviour
    {
        [SerializeField, Range(1, 4), Tooltip("Most players at once.")] private int maxPlayers = 4;
        [SerializeField, Tooltip("Where joining players appear, in join order (seat 2 takes the first). Empty: beside seat 1.")]
        private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField, Tooltip("How far beside seat 1 a joining player appears when there are no spawn points, in seat 1's body heights.")]
        private float besideHeights = 1.5f;

        /// <summary>The party in the open scene, or null (single player).</summary>
        public static PartyJoin Current { get; private set; }

        /// <summary>Raised after a player sits down: (seat index, its slot).</summary>
        public static event Action<int, PlayerSlot> Joined;

        private PlayerSlot _first;
        private IDisposable _listening;
        private readonly List<Gamepad> _waiting = new List<Gamepad>();   // presses heard, joined in Update (not mid-event)
        private readonly HashSet<InputDevice> _taken = new HashSet<InputDevice>();

        public int Count => PlayerSlot.All.Count;

        private void OnEnable()
        {
            Current = this;
            _listening = InputSystem.onAnyButtonPress.Call(OnAnyButton);
        }

        private void OnDisable()
        {
            _listening?.Dispose(); _listening = null;
            if (Current == this) Current = null;
        }

        private void Start()
        {
            var motor = FindFirstObjectByType<PlayerMotor>();
            var rig = FindFirstObjectByType<ShoulderCamera>();
            if (motor == null || rig == null) { Debug.LogWarning("PartyJoin: no player or camera in the scene."); enabled = false; return; }
            if (!motor.TryGetComponent(out _first)) _first = motor.gameObject.AddComponent<PlayerSlot>();
            _first.Seat(0, rig, motor.GetComponent<PlayerInput>());
            Layout();
        }

        private void OnAnyButton(InputControl control)
        {
            if (!(control.device is Gamepad pad) || control != pad.startButton) return;
            if (_taken.Contains(pad) || _waiting.Contains(pad)) return;
            _waiting.Add(pad);
        }

        private void Update()
        {
            if (_waiting.Count == 0) return;
            var pads = _waiting.ToArray(); _waiting.Clear();
            foreach (Gamepad pad in pads)
            {
                if (_first == null || Count >= maxPlayers || _taken.Contains(pad) || !pad.added) continue;
                Join(pad);
            }
        }

        private void Join(Gamepad pad)
        {
            // Seat 1 gives up the pads: from now on it is the keyboard and mouse.
            if (_first.Input.Paired == null)
            {
                var kbm = new List<InputDevice>();
                if (Keyboard.current != null) kbm.Add(Keyboard.current);
                if (Mouse.current != null) kbm.Add(Mouse.current);
                _first.Input.PairWith(kbm.ToArray());
                foreach (var d in kbm) _taken.Add(d);
            }

            int seat = Count;
            Transform player = _first.Input.transform, cam = _first.Rig.transform;
            bool camInside = cam.IsChildOf(player);

            // Hold the pair under one parent for the copy, then put the originals back exactly where they were.
            Transform playerParent = player.parent, camParent = cam.parent;
            int playerSibling = player.GetSiblingIndex(), camSibling = cam.GetSiblingIndex();
            var pair = new GameObject("PartyPair").transform;
            player.SetParent(pair, true);
            if (!camInside) cam.SetParent(pair, true);
            var hold = new GameObject("PartyHold"); hold.SetActive(false);   // the copy wakes only once it is rewired
            Transform copy = Instantiate(pair.gameObject, hold.transform).transform;
            player.SetParent(playerParent, true); player.SetSiblingIndex(playerSibling);
            if (!camInside) { cam.SetParent(camParent, true); cam.SetSiblingIndex(camSibling); }
            Destroy(pair.gameObject);

            PlayerInput input = copy.GetComponentInChildren<PlayerInput>(true);
            ShoulderCamera rig = copy.GetComponentInChildren<ShoulderCamera>(true);
            Transform newPlayer = input.transform, newCam = rig.transform;
            newPlayer.name = "Player " + (seat + 1);
            newCam.name = "Camera " + (seat + 1);
            newCam.tag = "Untagged";   // Camera.main stays seat 1's
            foreach (var l in copy.GetComponentsInChildren<AudioListener>(true)) DestroyImmediate(l);   // one listener: seat 1's
            rig.OwnsCursor = false;

            var cc = newPlayer.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            newPlayer.SetPositionAndRotation(SpawnFor(seat, player), player.rotation);
            if (cc != null) cc.enabled = true;

            if (!newPlayer.TryGetComponent(out PlayerSlot slot)) slot = newPlayer.gameObject.AddComponent<PlayerSlot>();
            slot.Seat(seat, rig, input);

            newPlayer.SetParent(null, true);
            if (!camInside) newCam.SetParent(null, true);
            Destroy(hold);

            input.PairWith(pad);   // after Awake, which made its actions
            _taken.Add(pad);
            Layout();
            Debug.Log("Party: player " + (seat + 1) + " joined on " + pad.displayName);
            Joined?.Invoke(seat, slot);
        }

        private Vector3 SpawnFor(int seat, Transform first)
        {
            int i = seat - 1;
            if (spawnPoints != null && i < spawnPoints.Length && spawnPoints[i] != null) return spawnPoints[i].position;
            var cc = first.GetComponent<CharacterController>();
            float height = cc != null ? cc.height * first.lossyScale.y : 1f;
            Vector3 side = Quaternion.Euler(0f, 90f * seat, 0f) * first.right;
            return first.position + side * (besideHeights * height) + Vector3.up * (0.1f * height);
        }

        /// <summary>Splits the screen: one player whole, two one above the other, three or four in quarters (with three,
        /// seat 1 has the top half).</summary>
        private void Layout()
        {
            var all = PlayerSlot.All;
            int n = all.Count;
            for (int i = 0; i < n; i++)
            {
                Camera view = all[i].Rig != null ? all[i].Rig.View : null;
                if (view == null) continue;
                view.rect = RectFor(i, n);
            }
        }

        private static Rect RectFor(int i, int n)
        {
            if (n <= 1) return new Rect(0f, 0f, 1f, 1f);
            if (n == 2) return new Rect(0f, i == 0 ? 0.5f : 0f, 1f, 0.5f);
            if (n == 3)
            {
                if (i == 0) return new Rect(0f, 0.5f, 1f, 0.5f);
                return new Rect(i == 1 ? 0f : 0.5f, 0f, 0.5f, 0.5f);
            }
            return new Rect((i % 2) * 0.5f, i < 2 ? 0.5f : 0f, 0.5f, 0.5f);
        }

#if UNITY_EDITOR
        [MenuItem("Shift Jam/Party/Add Party Join To Open Scene")]
        private static void AddToScene()
        {
            if (FindFirstObjectByType<PartyJoin>() != null) { Debug.Log("PartyJoin: already in this scene."); return; }
            var go = new GameObject("PartyJoin");
            go.AddComponent<PartyJoin>();
            Undo.RegisterCreatedObjectUndo(go, "Add Party Join");
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("PartyJoin added: press Play, then Start on a pad to join (up to 4).");
        }
#endif
    }
}
