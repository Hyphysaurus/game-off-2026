using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// Online party play (D-0111): up to 4 players, each on their own PC. The host makes a session on Unity's Relay and
    /// gets a short join code (copied to the clipboard: paste it in Discord); a friend copies the code and presses Join
    /// on the title, which reads it from the clipboard. The host then takes everyone to the party scene (NGO's scene
    /// management carries the joiners along). Every player is a NetPlayer (Resources/Net/NetPlayer): its owner moves it
    /// and the others see it. One per game, kept across scenes. The course and the sandboxes stay single player.
    /// </summary>
    public sealed class NetSession : MonoBehaviour
    {
        public const string PartyScene = "Stadium";
        public const int MaxPlayers = 4;
        private const string PlayerPrefabPath = "Net/NetPlayer";

        /// <summary>True while connected (host or joined).</summary>
        public static bool Online => _instance != null && (_instance._session != null || _instance._local) && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>The code that joins a party on this PC with no Relay (a second editor or a test window).</summary>
        public const string LocalCode = "LOCAL";
        private const ushort LocalPort = 7777;

        /// <summary>The join code, or null when not hosting or joined.</summary>
        public static string Code => _instance == null ? null : _instance._session != null ? _instance._session.Code : _instance._local ? LocalCode : null;

        /// <summary>A line for the title or the HUD: what the session is doing or what went wrong.</summary>
        public static string Status { get; private set; } = "";

        /// <summary>Raised when Status changes.</summary>
        public static event Action StatusChanged;

        private static NetSession _instance;
        private const string LastCodeKey = "ShiftJam.LastPartyCode";
        private ISession _session;
        private bool _local;   // a party on this PC only: Relay could not be reached (D-0114)
        private bool _busy;
        private TMP_Text _hud;
        private float _nextHud;

        private static NetSession Get()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("NetSession");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NetSession>();
            _instance._scores = go.AddComponent<NetScoreboard>();   // the kill feed and the Tab board (D-0118)
            return _instance;
        }

        private NetScoreboard _scores;
        private bool _leaving;

        /// <summary>The round trip to the host in milliseconds (0 for the host itself or when not connected).</summary>
        public static int PingMs
        {
            get
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsListening || nm.IsServer || !nm.IsConnectedClient) return 0;
                var t = nm.GetComponent<UnityTransport>();
                return t != null ? (int)t.GetCurrentRtt(NetworkManager.ServerClientId) : 0;
            }
        }

        /// <summary>Host a party: sign in, make the session on Relay, copy the code, take everyone to the party scene.</summary>
        public static async void Host()
        {
            var s = Get();
            if (s._busy || s._session != null || s._local) return;
            s._busy = true;
            s._scores.Clear();
            try
            {
                Say("Starting a party...");
                await s.SignIn();
                s.EnsureNetworkManager();
                var options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
                s._session = await MultiplayerService.Instance.CreateSessionAsync(options);
                s.Watch();
                GUIUtility.systemCopyBuffer = s._session.Code;
                Say("Party code " + s._session.Code + " (copied: paste it to your friends)");
                Time.timeScale = 1f;
                NetworkManager.Singleton.SceneManager.LoadScene(PartyScene, LoadSceneMode.Single);
            }
            catch (Exception e)
            {
                // No Relay (not switched on for the project, no internet, signed out): play on this PC instead, so a
                // test still runs; a second window joins with LOCAL. Friends cannot join this one.
                Debug.LogWarning("NetSession: Relay failed, hosting on this PC: " + e);
                s._session = null;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
                s.HostLocal();
            }
            finally { s._busy = false; }
        }

        private void HostLocal()
        {
            try
            {
                EnsureNetworkManager();
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", LocalPort, "0.0.0.0");
                if (!NetworkManager.Singleton.StartHost()) throw new InvalidOperationException("the host did not start");
                _local = true;
                GUIUtility.systemCopyBuffer = LocalCode;
                Say("Relay unavailable: playing on this PC only (a second window joins with " + LocalCode + ")");
                Time.timeScale = 1f;
                NetworkManager.Singleton.SceneManager.LoadScene(PartyScene, LoadSceneMode.Single);
            }
            catch (Exception e) { Fail("Could not start a party", e); }
        }

        /// <summary>Join a party with the code on the clipboard (or the code given).</summary>
        public static async void Join(string code = null)
        {
            var s = Get();
            if (s._busy || s._session != null || s._local) return;
            // The clipboard's code, else the last party joined: dropping out and back in is one click (D-0121).
            code = Clean(code ?? GUIUtility.systemCopyBuffer) ?? Clean(PlayerPrefs.GetString(LastCodeKey, ""));
            if (string.IsNullOrEmpty(code)) { Say("Copy a party code first (from Discord), then press Join."); return; }
            s._scores.Clear();
            if (code == LocalCode) { s.JoinLocal(); return; }
            s._busy = true;
            try
            {
                Say("Joining " + code + "...");
                await s.SignIn();
                s.EnsureNetworkManager();
                s._session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                PlayerPrefs.SetString(LastCodeKey, code); PlayerPrefs.Save();
                s.Watch();
                Time.timeScale = 1f;
                Say("Joined " + code);   // the host's scene comes to us
            }
            catch (Exception e) { Fail("Could not join " + code, e); }
            finally { s._busy = false; }
        }

        private void JoinLocal()
        {
            try
            {
                Say("Joining the party on this PC...");
                EnsureNetworkManager();
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", LocalPort);
                if (!NetworkManager.Singleton.StartClient()) throw new InvalidOperationException("the client did not start");
                _local = true;
                Time.timeScale = 1f;
            }
            catch (Exception e) { Fail("Could not join " + LocalCode, e); }
        }

        /// <summary>Leave the party (the host's leaving ends it for everyone) and go back to the course's title.</summary>
        public static async void Leave()
        {
            var s = _instance;
            if (s == null) { SceneManager.LoadScene(0); return; }
            s._leaving = true;
            ISession session = s._session; s._session = null; s._local = false;
            try { if (session != null) await session.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning("NetSession: leaving: " + e.Message); }
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            Say("");
            Time.timeScale = 1f;
            s._leaving = false;
            SceneManager.LoadScene(0);
        }

        /// <summary>The connection stopped without our leaving: the host left, the internet dropped, we were refused.
        /// Back to the title with a line saying so (D-0118), instead of a frozen party.</summary>
        private void OnStopped(bool wasHost)
        {
            if (_leaving || (_session == null && !_local)) return;
            string reason = NetworkManager.Singleton != null ? NetworkManager.Singleton.DisconnectReason : null;
            _session = null; _local = false;
            Say("Lost the connection to the party" + (string.IsNullOrEmpty(reason) ? "" : ": " + reason));
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }

        private void Watch()
        {
            _session.RemovedFromSession += OnEnded;
            _session.Deleted += OnEnded;
            _session.PlayerJoined += _ => Say(_session != null ? "Party code " + _session.Code + " · " + _session.PlayerCount + "/" + MaxPlayers : "");
            _session.PlayerHasLeft += _ => Say(_session != null ? "Party code " + _session.Code + " · " + _session.PlayerCount + "/" + MaxPlayers : "");
        }

        private void OnEnded()
        {
            if (_session == null) return;   // we left ourselves
            _session = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            Say("The party ended.");
            SceneManager.LoadScene(0);
        }

        private async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                // Every running copy signs in as its own anonymous player: Multiplayer Play Mode's extra editors, and two
                // copies of the built game on one PC (his test before friends get it) would otherwise be one player.
                options.SetProfile("p" + System.Diagnostics.Process.GetCurrentProcess().Id);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>The NetworkManager, made here (not in a scene) so it lives across scenes and every scene can host.</summary>
        private void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return;
            GameObject prefab = Resources.Load<GameObject>(PlayerPrefabPath);
            if (prefab == null) throw new InvalidOperationException("Resources/" + PlayerPrefabPath + " is missing: run Shift Jam / Party / Build Net Player + Stadium.");
            var go = new GameObject("NetworkManager");
            go.SetActive(false);   // configured before it wakes
            DontDestroyOnLoad(go);
            var transport = go.AddComponent<UnityTransport>();
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = prefab,
                EnableSceneManagement = true,
                ConnectionApproval = false,
            };
            nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            go.SetActive(true);
            nm.OnClientStopped += OnStopped;
        }

        private static string Clean(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            code = code.Trim().ToUpperInvariant();
            int space = code.LastIndexOf(' ');
            if (space >= 0) code = code.Substring(space + 1);   // "Party code ABC123" pasted whole
            return code.Length >= 4 && code.Length <= 12 ? code : null;
        }

        private static void Say(string line)
        {
            Status = line;
            if (!string.IsNullOrEmpty(line)) Debug.Log("Party: " + line);
            StatusChanged?.Invoke();
            if (_instance != null) _instance.RefreshHud();
        }

        private static void Fail(string what, Exception e)
        {
            Debug.LogWarning("NetSession: " + what + ": " + e);
            if (_instance != null && _instance._session == null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            Say(what + ": " + e.Message);
        }

        // ------------------------------------------------------------------ the code in a corner while online

        private void Update()
        {
            bool show = (_session != null || _local) && SceneManager.GetActiveScene().name == PartyScene;
            if (show && _hud == null) BuildHud();
            if (_hud != null && _hud.transform.parent.gameObject.activeSelf != show) _hud.transform.parent.gameObject.SetActive(show);
            if (show && _hud != null && Time.unscaledTime >= _nextHud) { _nextHud = Time.unscaledTime + 0.5f; RefreshHud(); }   // joins and leaves
        }

        private void RefreshHud()
        {
            if (_hud == null) return;
            int count = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;
            if (_session != null) count = _session.PlayerCount;
            int ping = PingMs;
            _hud.text = _session != null || _local ? "<b>Party " + Code + "</b>   " + count + "/" + MaxPlayers + (_local ? "   <alpha=#88>(this PC only)" : "") + (ping > 0 ? "   <alpha=#88>" + ping + " ms" : "") + "   <alpha=#88>Tab: scores   Esc: menu" : "";
        }

        private void BuildHud()
        {
            var root = new GameObject("PartyHud", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var textGo = new GameObject("Code", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -20f); rt.sizeDelta = new Vector2(900f, 50f);
            _hud = textGo.AddComponent<TextMeshProUGUI>();
            _hud.fontSize = 28; _hud.color = Color.white; _hud.raycastTarget = false;
            _hud.outlineWidth = 0.2f; _hud.outlineColor = new Color32(0, 0, 0, 200);
            RefreshHud();
        }
    }
}
