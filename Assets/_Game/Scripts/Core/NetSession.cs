using System;
using System.Collections.Generic;
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

        /// <summary>True whenever the network runs (host or client), from the first moment of joining: the scene the host
        /// is in arrives on a joining PC before Online is true, so a scene's online rules key off this (D-0125).</summary>
        public static bool InParty => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>The house as an online world (D-0125, co-op as a sandbox): the course scene with OnlineWorld's rules.</summary>
        public const string HouseScene = "Greybox";

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

        /// <summary>Thrown by anything that would make an object once Play has stopped. Stopping Play while online drops
        /// the connection, and the reconnect that starts then is still awaiting the network when the editor is back in
        /// edit mode: carried on, it made a "NetSession" and a "NetworkManager" in whatever scene was OPEN FOR EDITING,
        /// where the next save kept them (found 24 Nov, when a save of the house picked both up).</summary>
        private const string PlayStopped = "Play has stopped: nothing online is made outside Play.";

        private static NetSession Get()
        {
            if (_instance != null) return _instance;
            if (!Application.isPlaying) throw new InvalidOperationException(PlayStopped);
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

        /// <summary>Host a party in the house (D-0125): the course scene as a co-op sandbox.</summary>
        public static void HostHouse() => Host(HouseScene);

        /// <summary>Host a party: sign in, make the session on Relay, copy the code, take everyone to the world's scene
        /// (the Stadium unless another is given).</summary>
        public static async void Host(string world = PartyScene)
        {
            var s = Get();
            if (s._busy || s._session != null || s._local) return;
            s._busy = true;
            s._scores.Clear();
            try
            {
                Say("Starting a party...");
                await s.SignIn();
                await ShutdownDone();
                s.EnsureNetworkManager();
                // Named and public, so "Play Online" and the party list find it (D-0122); a code still joins it too.
                var options = new SessionOptions { MaxPlayers = MaxPlayers, Name = PlayerName.Get() + "'s party", IsPrivate = false }.WithRelayNetwork();
                options.SessionProperties[VersionKey] = new SessionProperty(Application.version, VisibilityPropertyOptions.Public);   // only the same version can join
                options.SessionProperties[WorldKey] = new SessionProperty(world, VisibilityPropertyOptions.Public);   // shown in the party list
                s._session = await MultiplayerService.Instance.CreateSessionAsync(options);
                s._lastId = s._session.Id;
                s.Watch();
                GUIUtility.systemCopyBuffer = s._session.Code;
                Say("Party code " + s._session.Code + " (copied: paste it to your friends)");
                Time.timeScale = 1f;
                // Already in that world (the house's title is the house): stay; the own player's arrival sets it up (D-0126).
                if (SceneManager.GetActiveScene().name != world) NetworkManager.Singleton.SceneManager.LoadScene(world, LoadSceneMode.Single);
                else OnlineWorld.EnsureApplied();
            }
            catch (Exception e)
            {
                // No Relay (not switched on for the project, no internet, signed out): play on this PC instead, so a
                // test still runs; a second window joins with LOCAL. Friends cannot join this one.
                Debug.LogWarning("NetSession: Relay failed, hosting on this PC: " + e);
                s._session = null;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
                s.HostLocal(world);
            }
            finally { s._busy = false; }
        }

        private void HostLocal(string world)
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
                if (SceneManager.GetActiveScene().name != world) NetworkManager.Singleton.SceneManager.LoadScene(world, LoadSceneMode.Single);
                else OnlineWorld.EnsureApplied();
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
                if (!await s.SameVersionOrLeave()) return;
                s._lastId = s._session.Id;
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

        // ---------------------------------------------------------------- jumping in and out (D-0122)

        /// <summary>One open party as the list shows it.</summary>
        public readonly struct PartyInfo
        {
            public readonly string Id, Name, World; public readonly int Players, Max;
            public PartyInfo(string id, string name, string world, int players, int max) { Id = id; Name = name; World = world; Players = players; Max = max; }
        }

        private string _lastId;   // the party we were in: a dropped connection tries it first
        private const string VersionKey = "v";
        private const string WorldKey = "w";

        /// <summary>The game version a party was made with ("" when it carries none).</summary>
        private static string VersionOf(IReadOnlyDictionary<string, SessionProperty> props) =>
            props != null && props.TryGetValue(VersionKey, out SessionProperty v) && v != null ? v.Value ?? "" : "";

        /// <summary>After joining by code (the list never shows other versions): a party of another version is left at once,
        /// with a line saying which to get.</summary>
        private async Task<bool> SameVersionOrLeave()
        {
            if (_session == null) return false;
            string theirs = VersionOf(_session.Properties);
            if (theirs == Application.version) return true;
            ISession wrong = _session; _session = null; _lastId = null;
            _leaving = true;
            try { await wrong.LeaveAsync(); } catch (Exception e) { Debug.LogWarning("NetSession: leaving a mismatch: " + e.Message); }
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            await ShutdownDone();   // the stop it raises must still read as ours, or Recover would start
            _leaving = false;
            Say("That party runs v" + (theirs.Length > 0 ? theirs : "?") + ", you have v" + Application.version + ": get the same version to join");
            return false;
        }

        /// <summary>The open Shift Jam parties with room (not locked, no password), fullest first.</summary>
        public static async Task<List<PartyInfo>> FindParties()
        {
            var s = Get();
            var found = new List<PartyInfo>();
            try
            {
                await s.SignIn();
                QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions { Count = 20 });
                foreach (ISessionInfo info in results.Sessions)
                {
                    if (info.IsLocked || info.HasPassword || info.AvailableSlots <= 0) continue;
                    if (VersionOf(info.Properties) != Application.version) continue;   // another version's world: it would half-work
                    string world = info.Properties != null && info.Properties.TryGetValue(WorldKey, out SessionProperty w) && w != null ? w.Value : PartyScene;
                    found.Add(new PartyInfo(info.Id, info.Name, world == HouseScene ? "House" : "Stadium", info.MaxPlayers - info.AvailableSlots, info.MaxPlayers));
                }
                found.Sort((a, b) => b.Players.CompareTo(a.Players));
            }
            catch (Exception e) { Debug.LogWarning("NetSession: finding parties: " + e.Message); }
            return found;
        }

        /// <summary>Join the party with this id (from the list).</summary>
        public static async void JoinParty(string id)
        {
            var s = Get();
            if (s._busy || s._session != null || s._local) return;
            s._busy = true;
            try { await s.JoinById(id); }
            catch (Exception e) { Fail("Could not join that party", e); }
            finally { s._busy = false; }
        }

        /// <summary>The seamless way in: an open party if there is one, else a new one with us as host. Waits a moment
        /// and looks again before hosting, so two players coming in together end up in one world, not two.</summary>
        public static async void PlayOnline()
        {
            var s = Get();
            if (s._busy || s._session != null || s._local) return;
            s._busy = true;
            try { await s.PlayOnlineCore(); }
            finally { s._busy = false; }
        }

        private async Task PlayOnlineCore()
        {
            Say("Looking for a party...");
            List<PartyInfo> open = await FindParties();
            if (open.Count == 0)
            {
                await Task.Delay(UnityEngine.Random.Range(600, 2400));
                open = await FindParties();
            }
            foreach (var party in open)
            {
                if (!Application.isPlaying) return;
                try { await JoinById(party.Id); return; }
                catch (Exception e) { if (!Application.isPlaying) return; Debug.LogWarning("NetSession: " + party.Name + " would not take us: " + e.Message); }
            }
            if (!Application.isPlaying) return;   // Play stopped while we looked: hosting would make objects in the open scene
            _busy = false;   // Host checks it
            Host();
        }

        private async Task JoinById(string id)
        {
            Say("Joining a party...");
            await SignIn();
            await ShutdownDone();
            EnsureNetworkManager();
            _scores.Clear();
            _session = await MultiplayerService.Instance.JoinSessionByIdAsync(id);
            _lastId = _session.Id;
            PlayerPrefs.SetString(LastCodeKey, _session.Code ?? ""); PlayerPrefs.Save();
            Watch();
            Time.timeScale = 1f;
            Say("Joined " + (_session.Name ?? "a party"));
        }

        /// <summary>A NetworkManager still shutting down cannot start again: wait it out.</summary>
        private static async Task ShutdownDone()
        {
            for (int i = 0; i < 100 && NetworkManager.Singleton != null && NetworkManager.Singleton.ShutdownInProgress; i++) await Task.Delay(50);
        }

        /// <summary>The connection went without our leaving (D-0122): back into the same party if it is still there, else
        /// straight into another open world, or a new one of our own. Never stranded on the title.</summary>
        private async void Recover(string why)
        {
            if (_busy || !Application.isPlaying) return;   // Play is stopping: that is why the connection went
            _busy = true;
            string last = _lastId;
            _session = null; _local = false;
            Say(why + ": reconnecting...");
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);   // out of the dead party scene while we look
            try
            {
                await ShutdownDone();
                for (int attempt = 0; attempt < 2 && !string.IsNullOrEmpty(last); attempt++)
                {
                    if (!Application.isPlaying) return;   // Play stopped while we waited: there is nothing to get back to
                    try { await JoinById(last); return; }
                    catch (Exception e)
                    {
                        if (!Application.isPlaying) return;
                        Debug.LogWarning("NetSession: rejoin " + (attempt + 1) + ": " + e.Message); await Task.Delay(1500);
                    }
                }
                if (!Application.isPlaying) return;
                await PlayOnlineCore();
            }
            catch (Exception e) { if (Application.isPlaying) Fail("Could not get back online", e); }
            finally { _busy = false; }
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
            await ShutdownDone();
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
            if (_local) { _local = false; Say("Lost the connection to the party"); Time.timeScale = 1f; SceneManager.LoadScene(0); return; }
            Recover("Lost the connection");
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
            if (_session == null || _leaving) return;   // we left ourselves
            _lastId = null;   // that party is gone: do not try it again
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            Recover("The party ended");
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
            if (!Application.isPlaying) throw new InvalidOperationException(PlayStopped);
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
            bool show = (_session != null || _local) && InParty;   // any online world: the Stadium or the house (D-0125)
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
            var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;   // by screen height (D-0133)
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
