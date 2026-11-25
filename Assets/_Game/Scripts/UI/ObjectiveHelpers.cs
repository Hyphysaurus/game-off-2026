using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Two of the objective layer's helpers (D-0221, his brother's playtest: "there aren't any clear objectives"):
    /// - ARRIVAL CARDS: a room's ArrivalCard trigger asks for its card; it shows once per save slot through Toasts.CardFor
    ///   (the room's name big, one line of what to do under it), never over another big card (the letters', a Flat Zone's):
    ///   it waits until the way is clear (no big card, no menu or key sheet or console, not the opening, not a room's reveal
    ///   camera, not in a Flat Zone, not Build Mode, on the ground if the card asks) and Arrival Settle seconds more. If the
    ///   player has left the room's box by then (a fall reset), it is not shown or marked: it shows on the next arrival.
    /// - THE STALL NUDGE: after Nudge Seconds of play in the house with no step done and no star earned, the goal card
    ///   pulses and one tip repeats the current step. Progress (a step, a star, a load) starts the clock again. The clock
    ///   stops (it does not restart) under menus, the opening, a big card, a reveal, a Flat Zone and Build Mode; nothing
    ///   after the Goal (no step left).
    /// - THE ZONE (D-0224): once a frame it asks Objectives which zone this PC's player is in (Objectives.TrackZones: the
    ///   Sticker Chart's rows and the objective card's zone line; nothing allocated).
    /// Online, in batch runs and with ObjectiveSet's On unticked: nothing (Objectives.Live, as D-0220). Words and numbers on
    /// Resources/ObjectiveSet. One for the whole game, made at the first scene, kept across loads; nothing allocates per frame.
    /// OFF SWITCHES: ObjectiveSet > Arrivals On, Nudge On.
    /// </summary>
    public sealed class ObjectiveHelpers : MonoBehaviour
    {
        /// <summary>A card shown is remembered per save slot under Toasts' key "toast:" + this + its id.</summary>
        public const string ArrivalKey = "arrival:";
        // ToastHud draws its big card as "Canvas/BigCard" (ToastHud.Build and AddCard): there while one shows.
        private const string ToastCanvas = "Canvas", BigCardName = "BigCard";

        private static ObjectiveHelpers _instance;

        // The card waiting to show: its words, for whom, in which scene, and not before when.
        private ArrivalWords _card;
        private Component _cardFor;
        private PlayerMotor _cardMotor;
        private Bounds _cardBox;
        private int _cardScene;
        private float _cardAt;

        // The nudge: seconds of quiet play since the last progress, and whether it has spoken since.
        private float _idle;
        private bool _nudged, _stepsHere;

        private RoomReveal[] _reveals = new RoomReveal[0];
        private Transform _toastCanvas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make() => Get();

        /// <summary>The helpers, made on the first call. Batch runs make none.</summary>
        public static ObjectiveHelpers Get()
        {
            if (_instance != null) return _instance;
            if (Application.isBatchMode || !Application.isPlaying) return null;
            var go = new GameObject("ObjectiveHelpers");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ObjectiveHelpers>();
            return _instance;
        }

        /// <summary>A room's trigger met this PC's player: its card waits to show, unless this save slot has seen it, the
        /// card has no words, arrivals are off, or this is online. A newer arrival takes the place of one still waiting.</summary>
        public static void Arrive(string id, Component player)
        {
            if (player == null || !Objectives.Live) return;
            ObjectiveSet set = Objectives.Set;
            if (!set.arrivalsOn) return;
            ArrivalWords words = HelperWords.Arrival(set, id);
            if (words == null || string.IsNullOrWhiteSpace(words.title)) return;
            if (Toasts.Told(ArrivalKey + id)) return;
            ObjectiveHelpers helpers = Get();
            if (helpers == null) return;
            helpers._card = words;
            helpers._cardFor = player;
            helpers._cardMotor = player.GetComponent<PlayerMotor>();
            helpers._cardBox = new Bounds(words.centre, new Vector3(Mathf.Abs(words.size.x), Mathf.Abs(words.size.y), Mathf.Abs(words.size.z)));
            helpers._cardScene = player.gameObject.scene.handle;
            helpers._cardAt = Time.unscaledTime + set.arrivalSettle;
        }

        private void OnEnable()
        {
            Objectives.Changed += Progress;
            Objectives.StarAwarded += OnStar;
            SceneManager.activeSceneChanged += OnActiveScene;
            Look(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            Objectives.Changed -= Progress;
            Objectives.StarAwarded -= OnStar;
            SceneManager.activeSceneChanged -= OnActiveScene;
        }

        private void OnActiveScene(Scene from, Scene to) => Look(to);

        /// <summary>A new scene: whether it has main steps, its reveal cameras (found once), and the nudge's clock afresh.</summary>
        private void Look(Scene scene)
        {
            _stepsHere = Objectives.StepsIn(scene.name);
            _reveals = FindObjectsByType<RoomReveal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Progress();
        }

        private void OnStar(string id, string title) => Progress();

        /// <summary>A step done, a star earned, the save read again, another scene: the nudge's clock starts again.</summary>
        private void Progress()
        {
            _idle = 0f;
            _nudged = false;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            Objectives.TrackZones();   // D-0224: the zone first, so the card shown below names the right one
            ShowCard(now);
            Nudge(now);
        }

        // ---------- the arrival card ----------

        private void ShowCard(float now)
        {
            if (_card == null) return;
            if (_cardFor == null || _cardFor.gameObject.scene.handle != _cardScene || !Objectives.Live)
            {
                _card = null;   // the player or its scene is gone, or a party began: nothing to show
                return;
            }
            ObjectiveSet set = Objectives.Set;
            bool airborne = _card.onGround && _cardMotor != null && _cardMotor.isActiveAndEnabled && !_cardMotor.Grounded;
            if (airborne || Held() || RevealRunning() || BigCardUp())
            {
                _cardAt = Mathf.Max(_cardAt, now + set.arrivalSettle);   // the way is not clear: it waits, then settles
                return;
            }
            if (now < _cardAt) return;
            ArrivalWords card = _card;
            _card = null;
            // Left the room before the way was clear (a fall reset from the glide down, a quick step back out): no card now;
            // it is not marked, so it shows the next time the player comes in.
            if (!_cardBox.Contains(_cardFor.transform.position)) return;
            if (!set.arrivalsOn || !Toasts.FirstTime(ArrivalKey + card.id)) return;
            string text = string.IsNullOrWhiteSpace(card.line)
                ? card.title
                : card.title + "\n<size=" + Mathf.RoundToInt(set.arrivalLineSize) + "%>" + card.line + "</size>";
            Color colour = card.colour; colour.a = 1f;
            Toasts.CardFor(_cardFor, text, colour);
            Debug.Log("Objectives: arrival card '" + card.id + "'");
        }

        // ---------- the stall nudge ----------

        private void Nudge(float now)
        {
            if (_nudged || !_stepsHere || !Objectives.Live) return;
            ObjectiveSet set = Objectives.Set;
            if (!set.nudgeOn) return;
            ObjectiveStep step = Objectives.Current;
            if (step == null) return;   // after the Goal: nothing left to say
            if (Held() || _card != null || RevealRunning()) return;   // the clock stops (a waiting card speaks first)
            _idle += Time.unscaledDeltaTime;
            if (_idle < set.nudgeSeconds) return;
            if (BigCardUp()) return;   // at the moment it is due: wait for the big card to go
            _nudged = true;
            ObjectiveHud.Pulse();
            if (!string.IsNullOrWhiteSpace(step.text)) Toasts.Show(HelperWords.Format(set.nudgeFormat, step.text), ToastKind.Tip);
            Debug.Log("Objectives: nudge after " + Mathf.RoundToInt(_idle) + " s on '" + step.id + "'");
        }

        // ---------- when to keep quiet ----------

        /// <summary>A menu, the key sheet or the console is open, the opening plays, Build Mode, a team match, a Flat
        /// Zone holds the player (its camera swings and its own cards show), or the Sticker Chart is up (D-0224).</summary>
        private static bool Held() =>
            PauseMenu.IsOpen || TitleMenu.IsOpen || KeyHelp.IsOpen || DebugConsole.IsOpen || Opening.Playing
            || GameMode.Current == GameMode.Mode.Build || TeamMatch.Running || FlatZone.Current != null
            || StickerChart.IsOpen;   // D-0224: the player is reading the chart

        /// <summary>A room's reveal camera has the screen (the playroom's first look).</summary>
        private bool RevealRunning()
        {
            for (int i = 0; i < _reveals.Length; i++) if (_reveals[i] != null && _reveals[i].Running) return true;
            return false;
        }

        /// <summary>A big card is on screen now (the letters', a Flat Zone's, another room's).</summary>
        private bool BigCardUp()
        {
            if (_toastCanvas == null)
            {
                ToastHud hud = ToastHud.Get();
                _toastCanvas = hud != null ? hud.transform.Find(ToastCanvas) : null;
                if (_toastCanvas == null) return false;
            }
            return _toastCanvas.Find(BigCardName) != null;
        }
    }
}
