using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The start grid and the race it sends off (D-0061). The cars that stand on it are the field: the one the player
    /// sits in is the player's, the others get a rival each (RivalDriver). <see cref="StartRace"/> puts every car on its
    /// slot, whole and with an empty nitro meter, stands the loose props back up, counts 3-2-1 with every car held
    /// still, and at GO hands the race to the finish line (RaceLoop: the same laps and gates as a race begun by driving
    /// over the line alone). While the race runs it follows every car along the RacePath once a physics step (RaceField
    /// has the standings). When the player finishes the results are known; a little later, or when the race is given up
    /// (the driver gets out, F9, Build Mode), every rival is put back on its slot with nobody at the wheel (never in
    /// front of a car that is about to come through the grid: it waits, or is left where it rolls to a stop). A player who
    /// arrives in a car that is not one of the grid's takes the slot of one grid car, which sits the race out.
    /// The numbers are in RivalSettings; the builder lays the grid (CourseBuilder, the raceway).
    /// </summary>
    [DefaultExecutionOrder(-20)]   // every car is followed along the road before anyone steers by it
    public sealed class RaceGrid : MonoBehaviour, IResettable
    {
        [SerializeField, Tooltip("The way round, baked by the builder.")] private RacePath path;
        [SerializeField, Tooltip("The finish line whose laps and gates the race uses.")] private RaceLoop finish;
        [SerializeField, Tooltip("The cars that stand on the grid, pole first.")] private ArcadeCar[] cars = new ArcadeCar[0];
        [SerializeField, Tooltip("Where each of those cars starts: one slot per car, same order.")] private Transform[] slots = new Transform[0];
        [SerializeField, Tooltip("Each car's name on the results card, same order.")] private string[] names = new string[0];
        [SerializeField, Tooltip("The player's name on the results card.")] private string playerName = "YOU";
        [SerializeField, Tooltip("Loose things under this are stood back up at every start (cones, tyres, the block wall).")] private Transform putBack;

        /// <summary>The grid of the scene that is open, or null: the HUD offers a race when there is one.</summary>
        public static RaceGrid Current { get; private set; }

        private readonly List<Racer> _racers = new List<Racer>();
        private readonly Collider[] _overlap = new Collider[16];
        private float[] _slotAt = new float[0];   // how far round the lap each slot is
        private RivalSettings _settings;
        private ArcadeCar _benched;        // a grid car sitting this race out (the player came in a car of their own)
        private CarSeat _seat;             // the player's seat, told to keep its driver through a wreck
        private Coroutine _count;
        private AudioSource _audio;
        private AudioClip _madeChime;
        private float _closeAt;
        private bool _checkDriver;

        public IReadOnlyList<ArcadeCar> Cars => cars;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Current = null; }

        private void Awake()
        {
            _settings = RivalSettings.Load();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            Current = this;
            RaceState.Started += OnLapRunStarted;
            RaceState.Finished += OnPlayerFinished;
            RaceState.Stopped += OnGivenUp;
            Driving.Changed += OnDrivingChanged;
            GameMode.Changed += OnModeChanged;
        }

        private void OnDisable()
        {
            RaceState.Started -= OnLapRunStarted;
            RaceState.Finished -= OnPlayerFinished;
            RaceState.Stopped -= OnGivenUp;
            Driving.Changed -= OnDrivingChanged;
            GameMode.Changed -= OnModeChanged;
            // the room going away (a scene change, quitting): there is nothing left to put back
            if (Driving.Quitting || !gameObject.scene.isLoaded) { _racers.Clear(); _seat = null; _benched = null; RaceField.Close(); }
            else EndField();
            if (Current == this) Current = null;
        }

        private void OnDestroy() { if (_madeChime != null) Destroy(_madeChime); }

        /// <summary>A race can be sent off now: the player sits in a drivable car, the road and the line are there, at
        /// least one rival can start, and no race with a field is being counted down or run.</summary>
        public bool CanStart
        {
            get
            {
                if (!isActiveAndEnabled || GameMode.Current != GameMode.Mode.Play || Garage.MenuOpen || Time.timeScale <= 0f) return false;
                if (RaceField.Now == RaceField.Stage.Countdown || RaceField.Now == RaceField.Stage.Racing) return false;
                if (path == null || !path.Ready || finish == null || !finish.isActiveAndEnabled) return false;
                ArcadeCar mine = PlayersCar();
                if (mine == null || mine.Wrecked || mine.Parked) return false;
                for (int i = 0; i < cars.Length && i < slots.Length; i++)
                    if (Usable(i) && cars[i] != mine) return true;
                return false;
            }
        }

        /// <summary>Lines the field up and starts the count. False when a race cannot start now.</summary>
        public bool StartRace()
        {
            if (!CanStart) return false;
            EndField();                                  // a field still coming home from the last race
            if (RaceState.Running) RaceState.Stop();     // a lap run alone ends here
            ArcadeCar mine = PlayersCar();

            // the player's slot: their own car's, or (an outside car) the slot of a grid car that sits out: the same
            // kind of car as theirs if there is one, so every rival is a different car, else the last on the grid
            int mySlot = Array.IndexOf(cars, mine);
            if (mySlot < 0)
            {
                int bench = -1;
                for (int i = 0; i < cars.Length && i < slots.Length; i++)
                {
                    if (!Usable(i)) continue;
                    bench = i;
                    if (SameKind(cars[i], mine)) break;
                }
                if (bench < 0) return false;
                _benched = cars[bench];
                _benched.gameObject.SetActive(false);
                mySlot = bench;
            }

            _racers.Clear();
            _slotAt = new float[slots.Length];
            for (int i = 0; i < slots.Length; i++) _slotAt[i] = slots[i] != null ? path.Nearest(slots[i].position) : 0f;
            var player = new Racer(mine, true, playerName, mySlot);
            for (int i = 0; i < cars.Length && i < slots.Length; i++)
            {
                if (i == mySlot) { _racers.Add(player); continue; }
                if (!Usable(i)) continue;
                var racer = new Racer(cars[i], false, i < names.Length && !string.IsNullOrEmpty(names[i]) ? names[i] : cars[i].name, i);
                if (!cars[i].TryGetComponent(out RivalDriver driver)) driver = cars[i].gameObject.AddComponent<RivalDriver>();
                racer.Driver = driver;
                _racers.Add(racer);
                LineUp(racer);
                driver.enabled = true;
                driver.Take(racer, path, _settings, _settings.Profile(i));   // each slot has its character
                cars[i].Drive(driver);
                cars[i].ForceBrake = true;
            }
            LineUp(player);
            mine.ForceBrake = true;
            if (mine.TryGetComponent(out _seat)) _seat.KeepDriverInWreck = true;
            if (Camera.main != null && Camera.main.TryGetComponent(out ShoulderCamera cam)) cam.Warp(slots[mySlot].eulerAngles.y);
            if (putBack != null)
                foreach (var loose in putBack.GetComponentsInChildren<IResettable>()) loose.ResetState();

            RaceField.Open(_racers, player, finish, path, finish.Laps);
            _count = StartCoroutine(CountDown());
            return true;
        }

        /// <summary>F9: a race being counted or run is over (the finish line stops the player's own).</summary>
        public void ResetState() => EndField();

        private ArcadeCar PlayersCar() => Driving.Vehicle != null ? Driving.Vehicle.GetComponent<ArcadeCar>() : null;

        private bool Usable(int i) => cars[i] != null && slots[i] != null && cars[i].gameObject.activeInHierarchy && !cars[i].Parked;

        /// <summary>Two cars made from the same piece: they wear the same first mesh.</summary>
        private static bool SameKind(ArcadeCar a, ArcadeCar b)
        {
            var ma = a.GetComponentInChildren<MeshFilter>();
            var mb = b.GetComponentInChildren<MeshFilter>();
            return ma != null && mb != null && ma.sharedMesh == mb.sharedMesh;
        }

        /// <summary>The car on its slot, whole, still, its nitro empty, and found on the road.</summary>
        private void LineUp(Racer racer)
        {
            Transform slot = slots[racer.Slot];
            racer.Car.ResetState();
            racer.Car.PlaceAt(slot.position, slot.rotation);
            racer.Seat(path);
        }

        private IEnumerator CountDown()
        {
            if (_settings.lineUpSeconds > 0f) yield return new WaitForSeconds(_settings.lineUpSeconds);
            for (int n = Mathf.Max(1, _settings.counts); n >= 1; n--)
            {
                RaceField.Show(n);
                Sound(_settings.countClip, _settings.countPitch);
                yield return new WaitForSeconds(_settings.countSeconds);
            }
            _count = null;
            foreach (var r in _racers) r.Car.ForceBrake = false;
            finish.BeginFromGrid();
            RaceField.Go();
            Sound(_settings.goClip, _settings.goPitch);
        }

        private void Sound(AudioClip clip, float pitch)
        {
            if (clip == null) clip = _madeChime != null ? _madeChime : (_madeChime = ProceduralClips.Chime());
            _audio.pitch = Mathf.Max(0.05f, pitch);
            _audio.PlayOneShot(clip, _settings.countVolume);
        }

        private void FixedUpdate()
        {
            var stage = RaceField.Now;
            if (stage == RaceField.Stage.None || _racers.Count == 0) return;
            for (int i = 0; i < _racers.Count; i++)
            {
                Racer r = _racers[i];
                if (r.Car == null) continue;
                if (stage == RaceField.Stage.Countdown) r.Car.ForceBrake = true;   // held still until GO (a seat may have let go of it)
                else r.Follow(path);                                               // a car back on the grid too: the others steer round it
            }
        }

        private void Update()
        {
            var stage = RaceField.Now;
            if (stage == RaceField.Stage.None || _racers.Count == 0) return;
            if (_checkDriver)
            {
                // looked at after the change, not in it (as the finish line does): on the grid nothing else is watching
                _checkDriver = false;
                Racer player = RaceField.Player;
                bool gone = GameMode.Current != GameMode.Mode.Play || player == null || player.Car == null || Driving.Vehicle != player.Car.transform;
                if (stage == RaceField.Stage.Countdown && gone) { EndField(); return; }
            }
            RaceField.Tick(Time.deltaTime);
            if (stage == RaceField.Stage.Countdown) return;
            RaceField.Refresh();
            float now = Time.time;
            for (int i = 0; i < _racers.Count; i++)
            {
                Racer r = _racers[i];
                if (r.IsPlayer || r.Home || !r.Finished) continue;
                if (r.HomeAt <= 0f) r.HomeAt = now + _settings.coolDownSeconds;   // drives on a while, then back to the grid
                else if (now >= r.HomeAt && CanGoHome(r)) { Release(r); PutOnSlot(r); }   // else it drives on and is asked again
            }
            if (stage == RaceField.Stage.Done && now >= _closeAt) EndField();
        }

        private void OnDrivingChanged() => _checkDriver = true;
        private void OnModeChanged(GameMode.Mode from, GameMode.Mode to) => _checkDriver = true;

        /// <summary>The player has run the last lap: the results are known, and a wreck from here on is an ordinary one.</summary>
        private void OnPlayerFinished()
        {
            if (RaceField.Now != RaceField.Stage.Racing || _racers.Count == 0) return;
            RaceField.Settle();
            _closeAt = Time.time + _settings.resultsSeconds;
            if (_seat != null) _seat.KeepDriverInWreck = false;
        }

        /// <summary>The player's race was stopped (the driver got out, F9, Build Mode): the field goes home.</summary>
        private void OnGivenUp()
        {
            if (RaceField.Now == RaceField.Stage.Racing) EndField();
        }

        /// <summary>A lap run alone begins at the line while the last field is still coming home: that field is over.</summary>
        private void OnLapRunStarted()
        {
            if (RaceField.Now == RaceField.Stage.Done) EndField();
        }

        /// <summary>The race with this field is over, however it ended: nobody is held, every rival goes home, the car
        /// that sat out is back.</summary>
        private void EndField()
        {
            if (_count != null) { StopCoroutine(_count); _count = null; }
            if (_racers.Count == 0) return;
            var leaving = new List<Racer>();
            foreach (var r in _racers)
            {
                if (r.Car == null) continue;
                if (RaceField.Now == RaceField.Stage.Countdown) r.Car.ForceBrake = false;
                if (!r.IsPlayer && !r.Home) { Release(r); leaving.Add(r); }   // every rival lets go first: none of them is "coming" any more
            }
            foreach (var r in leaving)
                if (CanGoHome(r)) PutOnSlot(r);   // else it is left where it rolls to a stop
            if (_seat != null) { _seat.KeepDriverInWreck = false; _seat = null; }
            if (_benched != null) { _benched.gameObject.SetActive(true); _benched = null; }
            _racers.Clear();
            RaceField.Close();
        }

        /// <summary>A rival's race is done: nobody at its wheel.</summary>
        private void Release(Racer r)
        {
            r.Home = true;
            r.Car.Drive(null);
            if (r.Driver != null) r.Driver.enabled = false;
        }

        /// <summary>Back on its slot, whole.</summary>
        private void PutOnSlot(Racer r)
        {
            Transform slot = slots[r.Slot];
            r.Car.ResetState();
            r.Car.PlaceAt(slot.position, slot.rotation);
            r.Seat(path);   // found on the road again where it now stands
        }

        /// <summary>Its slot is empty and no moving car is about to come through the grid: a car set down there now
        /// would not appear in front of anyone.</summary>
        private bool CanGoHome(Racer r)
        {
            if (slots[r.Slot] == null || !SlotFree(r.Car, slots[r.Slot])) return false;
            float s = r.Car.Scale;
            for (int i = 0; i < _racers.Count; i++)
            {
                Racer other = _racers[i];
                if (other == r || other.Home || other.Car == null || !other.Car.Occupied) continue;
                if (other.Speed < _settings.blockedSpeed * s) continue;   // standing: SlotFree has seen it
                float toSlot = path.Delta(other.Distance, _slotAt[r.Slot]);
                if (toSlot > -_settings.carLength * s && toSlot < (_settings.avoidMin + _settings.carLength) * s + _settings.homeNotice * other.Speed) return false;
            }
            return true;
        }

        /// <summary>No car other than <paramref name="car"/> stands on the slot.</summary>
        private bool SlotFree(ArcadeCar car, Transform slot)
        {
            float s = car.Scale;
            Vector3 half = new Vector3(_settings.carClearance * 0.5f, _settings.carClearance * 0.5f, _settings.carLength * 0.5f) * s;
            int n = Physics.OverlapBoxNonAlloc(slot.position + slot.up * half.y, half, _overlap, slot.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Rigidbody body = _overlap[i].attachedRigidbody;
                if (body != null && body != car.Body && body.GetComponent<ArcadeCar>() != null) return false;
            }
            return true;
        }
    }
}
