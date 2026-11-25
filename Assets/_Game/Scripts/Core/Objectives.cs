using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The objective layer (D-0220, his brother's playtest: "there aren't any clear objectives"): the big goal, the main
    /// step the player is on, and the gold stars, read from the ObjectiveSet asset and kept per save slot in SaveGame's
    /// seen list (step:&lt;id&gt;, star:&lt;id&gt;): Continue keeps them, New Game clears them (SaveGame.Clear).
    /// The steps are order-agnostic: <see cref="Current"/> is the FIRST unfinished one, and a later step finished marks
    /// every earlier one done too (a player who skips ahead is never told to go back).
    /// It listens to the facts the game already announces (GameEvents) and matches them to the set's rules by the
    /// object's name or path; parts that only raise UnityEvents reach it through an ObjectiveHook the maker wires; the
    /// Raceway's finish and the Colour Corner's end are heard in code in their own scenes.
    /// A star earned: a Got toast with its title, one sound, the shift charge refilled for the player on this PC (each
    /// star refills your charge), and <see cref="StarAwarded"/> (the HUD's counter pops).
    /// Nothing is networked: online (a party is a sandbox with no ending, D-0125, and other players' copies raise the same
    /// events on every PC) nothing is marked, and batch runs leave the saves alone. Made at run time, nothing in a scene.
    /// OFF SWITCH: untick On on Resources/ObjectiveSet: nothing is marked or awarded and the HUD stays hidden (the hooks
    /// in Greybox then call into nothing).
    /// </summary>
    public static class Objectives
    {
        public const string StepPrefix = "step:", StarPrefix = "star:";

        /// <summary>A step finished or a star earned, and whenever the state was read afresh (a load, New Game, another slot).</summary>
        public static event System.Action Changed;
        /// <summary>A star was just earned: its id and title.</summary>
        public static event System.Action<string, string> StarAwarded;

        /// <summary>One rule, ready to match: its object paths split once.</summary>
        private sealed class Compiled
        {
            public ObjectiveEvent when;
            public string scene;
            public string[][] paths;
        }

        private static ObjectiveSet _set;
        private static Compiled[][] _stepRules, _starRules;
        private static string[] _stepKeys, _starKeys;
        private static bool[] _stepDone, _starHeld;
        private static int _current = -1, _starCount, _readSlot = -1;
        private static bool _writing;
        private static readonly HashSet<string> StepScenes = new HashSet<string>();
        private static readonly List<ColourCorner> Corners = new List<ColourCorner>();
        // CurrentTarget's cache: which step and which scene it was found for.
        private static Transform _target;
        private static int _targetStep = -2, _targetScene = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null; StarAwarded = null;
            _set = null; _stepRules = _starRules = null; _stepKeys = _starKeys = null; _stepDone = _starHeld = null;
            _current = -1; _starCount = 0; _readSlot = -1; _writing = false;
            StepScenes.Clear(); Corners.Clear();
            _target = null; _targetStep = -2; _targetScene = -1;
        }

        // After SubsystemRegistration: RaceState and SaveGame clear their own events there, which would take these off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Listen()
        {
            // The editor may enter Play without reloading scripts: take the old hooks off before putting them on.
            GameEvents.SourceAwakened -= OnSource; GameEvents.SourceAwakened += OnSource;
            GameEvents.SwitchPressed -= OnSwitch; GameEvents.SwitchPressed += OnSwitch;
            GameEvents.DoorOpening -= OnDoor; GameEvents.DoorOpening += OnDoor;
            GameEvents.CheckpointClaimed -= OnCheckpoint; GameEvents.CheckpointClaimed += OnCheckpoint;
            GameEvents.GoalReached -= OnGoal; GameEvents.GoalReached += OnGoal;
            GameEvents.PickedUp -= OnPickedUp; GameEvents.PickedUp += OnPickedUp;
            GameEvents.SetDown -= OnSetDown; GameEvents.SetDown += OnSetDown;
            RaceState.Finished -= OnRaceFinished; RaceState.Finished += OnRaceFinished;
            SaveGame.Changed -= OnSaveChanged; SaveGame.Changed += OnSaveChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // ------------------------------------------------------------------------------------------------ the contract

        /// <summary>The big goal (the asset's words).</summary>
        public static string Goal { get { Ensure(); return _set.goal; } }

        /// <summary>The FIRST unfinished main step; null when all are done (after the Goal) or there are none.</summary>
        public static ObjectiveStep Current { get { Ensure(); return _current >= 0 ? _set.steps[_current] : null; } }

        /// <summary>Where the current step happens in the open scene (its Target, or its first rule's first object), or
        /// null: no step, or not in this scene. Found once per step and scene, not every frame.</summary>
        public static Transform CurrentTarget
        {
            get
            {
                Ensure();
                if (_current < 0 || !Application.isPlaying) return null;
                Scene scene = SceneManager.GetActiveScene();
                if (_targetStep != _current || _targetScene != scene.handle)
                {
                    _targetStep = _current; _targetScene = scene.handle;
                    _target = FindTarget(_set.steps[_current], scene);
                }
                return _target != null ? _target : null;
            }
        }

        /// <summary>Marks step <paramref name="id"/> done, and every step before it in the route. Unknown ids, online and
        /// batch runs: nothing.</summary>
        public static void MarkStep(string id)
        {
            if (!Live) return;
            Ensure();
            int index = StepIndex(id);
            if (index < 0) { Debug.LogWarning("Objectives: no step '" + id + "' in the ObjectiveSet"); return; }
            bool any = false;
            int slot = SaveGame.Slot;
            _writing = true;
            try
            {
                for (int i = 0; i <= index; i++)
                {
                    if (_stepDone[i]) continue;
                    _stepDone[i] = true; any = true;
                    SaveGame.MarkSeen(slot, _stepKeys[i]);
                }
            }
            finally { _writing = false; }
            if (!any) return;
            int was = _current;
            _current = FirstOpen();
            Debug.Log("Objectives: step done '" + id + "'" + (_current >= 0 ? ", now '" + _set.steps[_current].id + "'" : ", all done") + (was >= 0 && was < index ? " (skipped ahead from '" + _set.steps[was].id + "')" : ""));
            Changed?.Invoke();
        }

        public static bool StepDone(string id)
        {
            Ensure();
            int i = StepIndex(id);
            return i >= 0 && _stepDone[i];
        }

        /// <summary>Earns star <paramref name="id"/>: false if it was already held, is unknown, or this is online or a
        /// batch run. Shows its toast, plays its sound, refills this PC's player's shift charge.</summary>
        public static bool AwardStar(string id)
        {
            if (!Live) return false;
            Ensure();
            int i = StarIndex(id);
            if (i < 0) { Debug.LogWarning("Objectives: no star '" + id + "' in the ObjectiveSet"); return false; }
            if (_starHeld[i]) return false;
            _writing = true;
            try { SaveGame.MarkSeen(SaveGame.Slot, _starKeys[i]); }
            finally { _writing = false; }
            _starHeld[i] = true; _starCount++;
            ObjectiveStar star = _set.stars[i];
            Debug.Log("Objectives: gold star '" + id + "' (" + _starCount + " / " + StarTotal + ")");
            Toasts.Show(Format(_set.starToast, _starCount, StarTotal), ToastKind.Got, star.title);
            UiAudio.Play(_set.starSound);
            RefillLocalCharge();
            StarAwarded?.Invoke(id, star.title);
            Changed?.Invoke();
            return true;
        }

        public static bool HasStar(string id)
        {
            Ensure();
            int i = StarIndex(id);
            return i >= 0 && _starHeld[i];
        }

        public static int StarCount { get { Ensure(); return _starCount; } }
        public static int StarTotal { get { Ensure(); return _set.stars != null ? _set.stars.Length : 0; } }

        // ------------------------------------------------------------------------------------------------ helpers others use

        /// <summary>The set in use (the asset, or its defaults).</summary>
        public static ObjectiveSet Set { get { Ensure(); return _set; } }

        /// <summary>True if a main step happens in the scene called <paramref name="sceneName"/> (the house): its HUD
        /// shows the goal and the step; any other scene shows only the star counter.</summary>
        public static bool StepsIn(string sceneName) { Ensure(); return sceneName != null && StepScenes.Contains(sceneName); }

        /// <summary>Marks and awards count only in Play, not in batch runs, not online, and while the set is On.</summary>
        public static bool Live => Application.isPlaying && !Application.isBatchMode && !NetSession.InParty && Set.on;

        /// <summary><paramref name="format"/> with the two numbers, or the words as they are if he wrote a broken {}.</summary>
        public static string Format(string format, int a, int b)
        {
            if (string.IsNullOrEmpty(format)) return "";
            try { return string.Format(format, a, b); }
            catch (System.FormatException) { return format; }
        }

        /// <summary>The object at <paramref name="path"/> in <paramref name="scene"/>, switched on or off: a path from the
        /// scene's top ("Room/Bedroom/ShelfSwitch"), or a bare name looked for everywhere in it. Null if there is none.
        /// Walks the scene: call it on a change, not every frame. The objectives maker uses it too.</summary>
        public static Transform Resolve(Scene scene, string path)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(path)) return null;
            path = path.Trim().Trim('/');
            if (path.Length == 0) return null;
            var roots = new List<GameObject>(scene.rootCount);
            scene.GetRootGameObjects(roots);
            int slash = path.IndexOf('/');
            if (slash < 0)
            {
                foreach (GameObject root in roots)
                {
                    Transform found = FindDeep(root.transform, path);
                    if (found != null) return found;
                }
                return null;
            }
            string top = path.Substring(0, slash), rest = path.Substring(slash + 1);
            foreach (GameObject root in roots)
            {
                if (root.name != top) continue;
                Transform found = root.transform.Find(rest);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform found = FindDeep(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------------------------------------------ state

        /// <summary>Loads the set and reads the active slot the first time anything asks.</summary>
        private static void Ensure()
        {
            if (_set != null) return;
            _set = ObjectiveSet.Load();
            int nSteps = _set.steps != null ? _set.steps.Length : 0, nStars = _set.stars != null ? _set.stars.Length : 0;
            _stepRules = new Compiled[nSteps][]; _stepKeys = new string[nSteps]; _stepDone = new bool[nSteps];
            _starRules = new Compiled[nStars][]; _starKeys = new string[nStars]; _starHeld = new bool[nStars];
            StepScenes.Clear();
            for (int i = 0; i < nSteps; i++)
            {
                ObjectiveStep s = _set.steps[i];
                _stepKeys[i] = StepPrefix + (s != null ? s.id : "");
                _stepRules[i] = Compile(s != null ? s.doneWhen : null);
                foreach (Compiled c in _stepRules[i]) if (c.scene.Length > 0) StepScenes.Add(c.scene);
            }
            for (int i = 0; i < nStars; i++)
            {
                ObjectiveStar s = _set.stars[i];
                _starKeys[i] = StarPrefix + (s != null ? s.id : "");
                _starRules[i] = Compile(s != null ? s.doneWhen : null);
            }
            Read();
        }

        private static Compiled[] Compile(ObjectiveRule[] rules)
        {
            if (rules == null) return new Compiled[0];
            var list = new Compiled[rules.Length];
            for (int r = 0; r < rules.Length; r++)
            {
                ObjectiveRule rule = rules[r];
                var c = new Compiled { when = rule != null ? rule.when : ObjectiveEvent.SourceAwakened, scene = rule != null && rule.scene != null ? rule.scene.Trim() : "" };
                string[] objects = rule != null && rule.objects != null ? rule.objects : new string[0];
                c.paths = new string[objects.Length][];
                for (int o = 0; o < objects.Length; o++)
                    c.paths[o] = string.IsNullOrWhiteSpace(objects[o]) ? new string[0] : objects[o].Trim().Trim('/').Split('/');
                list[r] = c;
            }
            return list;
        }

        /// <summary>Reads the active slot's marks. True if anything differs from what was held.</summary>
        private static bool Read()
        {
            int slot = SaveGame.Slot;
            bool changed = slot != _readSlot;
            _readSlot = slot;
            for (int i = 0; i < _stepDone.Length; i++)
            {
                bool done = SaveGame.Seen(slot, _stepKeys[i]);
                if (done != _stepDone[i]) { _stepDone[i] = done; changed = true; }
            }
            _starCount = 0;
            for (int i = 0; i < _starHeld.Length; i++)
            {
                bool held = SaveGame.Seen(slot, _starKeys[i]);
                if (held != _starHeld[i]) { _starHeld[i] = held; changed = true; }
                if (held) _starCount++;
            }
            _current = FirstOpen();
            return changed;
        }

        private static int FirstOpen()
        {
            for (int i = 0; i < _stepDone.Length; i++) if (!_stepDone[i]) return i;
            return -1;
        }

        /// <summary>The save changed under us (New Game cleared the slot, another slot was picked, something else was
        /// marked): read it again, and tell the HUD if anything differs. Our own marks are already held.</summary>
        private static void OnSaveChanged()
        {
            if (_writing || _set == null) return;   // not read yet: Ensure reads it when it is first asked
            if (Read()) Changed?.Invoke();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _targetScene = -1;   // CurrentTarget looks again in the new scene
            if (Application.isBatchMode) return;
            Ensure();
            // The Colour Corner's end is an event on the corner itself: listen to the corners of the scene just loaded.
            for (int i = Corners.Count - 1; i >= 0; i--) if (Corners[i] == null) Corners.RemoveAt(i);
            if (!AnyRule(ObjectiveEvent.ColourBack)) return;
            var roots = new List<GameObject>(scene.rootCount);
            scene.GetRootGameObjects(roots);
            foreach (GameObject root in roots)
                foreach (ColourCorner corner in root.GetComponentsInChildren<ColourCorner>(true))
                {
                    corner.Changed -= OnColour; corner.Changed += OnColour;
                    if (!Corners.Contains(corner)) Corners.Add(corner);
                }
        }

        private static bool AnyRule(ObjectiveEvent kind)
        {
            foreach (Compiled[] rules in _starRules) foreach (Compiled c in rules) if (c.when == kind) return true;
            foreach (Compiled[] rules in _stepRules) foreach (Compiled c in rules) if (c.when == kind) return true;
            return false;
        }

        private static int StepIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _set.steps.Length; i++) if (_set.steps[i] != null && _set.steps[i].id == id) return i;
            return -1;
        }

        private static int StarIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _set.stars.Length; i++) if (_set.stars[i] != null && _set.stars[i].id == id) return i;
            return -1;
        }

        /// <summary>Each star refills your charge: every player on this PC (the one whose charge has a HUD here).</summary>
        private static void RefillLocalCharge()
        {
            foreach (ShiftCharge charge in Object.FindObjectsByType<ShiftCharge>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (charge.HasHud) charge.Refill();
        }

        private static Transform FindTarget(ObjectiveStep step, Scene scene)
        {
            if (step == null) return null;
            string path = step.target;
            string sceneName = "";
            if (step.doneWhen != null && step.doneWhen.Length > 0 && step.doneWhen[0] != null)
            {
                sceneName = step.doneWhen[0].scene ?? "";
                if (string.IsNullOrWhiteSpace(path) && step.doneWhen[0].objects != null && step.doneWhen[0].objects.Length > 0) path = step.doneWhen[0].objects[0];
            }
            if (sceneName.Length > 0 && sceneName != scene.name) return null;
            return Resolve(scene, path);
        }

        // ------------------------------------------------------------------------------------------------ hearing it

        private static void OnSource(ShiftSource source) { if (source != null) Heard(ObjectiveEvent.SourceAwakened, source.transform); }
        private static void OnSwitch(Switch pressed) { if (pressed != null) Heard(ObjectiveEvent.SwitchPressed, pressed.transform); }
        private static void OnDoor(Door door) { if (door != null) Heard(ObjectiveEvent.DoorOpening, door.transform); }
        private static void OnCheckpoint(Checkpoint checkpoint) { if (checkpoint != null) Heard(ObjectiveEvent.CheckpointClaimed, checkpoint.transform); }
        private static void OnGoal(Goal goal) { if (goal != null) Heard(ObjectiveEvent.GoalReached, goal.transform); }
        private static void OnPickedUp(Transform thing) { if (thing != null) Heard(ObjectiveEvent.PickedUp, thing); }
        private static void OnSetDown(Transform thing) { if (thing != null) Heard(ObjectiveEvent.SetDown, thing); }

        /// <summary>The player's car finished a race: a win if it came home first (or raced alone).</summary>
        private static void OnRaceFinished()
        {
            if (!Live) return;
            Racer player = RaceField.Player;
            if (player != null && player.FinishRank != 1) return;
            HeardInScene(ObjectiveEvent.RaceWon, SceneManager.GetActiveScene().name);
        }

        private static void OnColour(ColourCorner corner)
        {
            if (corner == null || !corner.Finished || !Live) return;
            HeardInScene(ObjectiveEvent.ColourBack, corner.gameObject.scene.name);
        }

        /// <summary>A fact about <paramref name="thing"/>: every step and star with a rule of that kind naming it (or one
        /// of its parents) is done.</summary>
        private static void Heard(ObjectiveEvent kind, Transform thing)
        {
            if (!Live) return;
            Ensure();
            string sceneName = null;
            for (int i = _stepRules.Length - 1; i >= 0; i--)   // the latest step first: it marks the earlier ones too
            {
                if (_stepDone[i] || !Matches(_stepRules[i], kind, thing, ref sceneName)) continue;
                MarkStep(_set.steps[i].id);
                break;
            }
            for (int i = 0; i < _starRules.Length; i++)
                if (!_starHeld[i] && Matches(_starRules[i], kind, thing, ref sceneName)) AwardStar(_set.stars[i].id);
        }

        private static void HeardInScene(ObjectiveEvent kind, string sceneName)
        {
            Ensure();
            for (int i = _stepRules.Length - 1; i >= 0; i--)
            {
                if (_stepDone[i] || !MatchesScene(_stepRules[i], kind, sceneName)) continue;
                MarkStep(_set.steps[i].id);
                break;
            }
            for (int i = 0; i < _starRules.Length; i++)
                if (!_starHeld[i] && MatchesScene(_starRules[i], kind, sceneName)) AwardStar(_set.stars[i].id);
        }

        private static bool Matches(Compiled[] rules, ObjectiveEvent kind, Transform thing, ref string sceneName)
        {
            foreach (Compiled c in rules)
            {
                if (c.when != kind) continue;
                if (c.scene.Length > 0)
                {
                    if (sceneName == null) sceneName = thing.gameObject.scene.name;
                    if (sceneName != c.scene) continue;
                }
                foreach (string[] path in c.paths)
                    for (Transform t = thing; t != null; t = t.parent)
                        if (PathIs(t, path)) return true;
            }
            return false;
        }

        private static bool MatchesScene(Compiled[] rules, ObjectiveEvent kind, string sceneName)
        {
            foreach (Compiled c in rules)
                if (c.when == kind && (c.scene.Length == 0 || c.scene == sceneName)) return true;
            return false;
        }

        /// <summary>True if <paramref name="t"/> is the object <paramref name="path"/> names: a bare name matches at any
        /// depth, a path must run from the scene's top.</summary>
        private static bool PathIs(Transform t, string[] path)
        {
            if (path.Length == 0) return false;
            Transform at = t;
            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (at == null || at.name != path[i]) return false;
                at = at.parent;
            }
            return path.Length == 1 || at == null;
        }
    }
}
