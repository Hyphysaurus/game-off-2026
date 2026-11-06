using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The finish line and the race it runs (D-0037). Drive through it with no race running and a race begins: a set
    /// number of laps round every RaceGate in the scene, taken by their numbers, lowest first (gates sharing a number are
    /// alternatives: either counts). Only the next gate counts, and the line completes a lap only once every gate of that
    /// lap is passed. The race ends after the last lap, or stops when the driver gets out, on F9 (IResettable), or when
    /// Build Mode or first person takes over. The numbers live in RaceState, for the driving HUD and the stadium TV; this
    /// plays the race's sounds. The line is this object's trigger, across the opening, so it works wherever it is placed.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RaceLoop : MonoBehaviour, IResettable
    {
        [SerializeField, Min(1)] private int laps = 3;
        [SerializeField, Min(0f), Tooltip("Seconds into a lap before the line counts again: the crossing that started it, or rocking back and forth over the line, is not a lap.")]
        private float minLapSeconds = 3f;
        [SerializeField, Min(0f), Tooltip("After a race ends, seconds before a crossing starts the next one (so the finish screen stays up).")]
        private float restartDelay = 4f;
        [Header("Sounds (heard anywhere)")]
        [SerializeField] private AudioClip startClip;
        [SerializeField] private AudioClip gateClip;
        [SerializeField] private AudioClip lapClip;
        [SerializeField, Tooltip("Crossing the line with a gate still to pass.")] private AudioClip missedClip;
        [SerializeField, Tooltip("The last lap done: the race is won.")] private AudioClip finishClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        /// <summary>The line that began the race running now, or null.</summary>
        public static RaceLoop Active { get; private set; }
        /// <summary>The number of the gate to drive through next, or 0 (no race, or every gate of this lap passed).</summary>
        public static int NextGate => Active != null ? Active.NextOrder : 0;
        /// <summary>The driver crossed the line with a gate still to pass: that gate's number.</summary>
        public static event Action<int> Missed;

        private readonly List<int> _orders = new List<int>();   // this race's gate numbers, lowest first, one each
        private AudioSource _audio;
        private float _readyAt, _quietUntil;
        private bool _checkDriver;

        public int Laps => laps;
        private int NextOrder => RaceState.Running && RaceState.Gate < _orders.Count ? _orders[RaceState.Gate] : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active = null; Missed = null; }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            Driving.Changed += OnDrivingChanged;
            GameMode.Changed += OnModeChanged;
            RaceState.Finished += OnEnded;
            RaceState.Stopped += OnEnded;
        }

        private void OnDisable()
        {
            Driving.Changed -= OnDrivingChanged;
            GameMode.Changed -= OnModeChanged;
            RaceState.Finished -= OnEnded;
            RaceState.Stopped -= OnEnded;
            if (Active != this) return;
            Active = null;
            RaceState.Stop();   // hidden or deleted mid-race: nothing more can count
        }

        private void OnDrivingChanged() => _checkDriver = true;

        private void OnModeChanged(GameMode.Mode from, GameMode.Mode to)
        {
            if (to != GameMode.Mode.Play) { if (Active == this) RaceState.Stop(); return; }
            _readyAt = Mathf.Max(_readyAt, Time.time + 1f);   // a held car let go inside the line on the way back to Play is not a crossing
        }

        private void OnEnded()
        {
            _readyAt = Time.time + restartDelay;   // every line waits after a race, so a copied finish cannot restart it at once
            if (Active == this) Active = null;
        }

        public void ResetState()
        {
            if (Active == this) RaceState.Stop();
            _readyAt = Time.time + 1f;   // a car put back on a start inside the line does not begin a race
        }

        private void Update()
        {
            if (_checkDriver)
            {
                _checkDriver = false;
                // Looked at after the change, not in it: a garage body swap gets out of one car and into the next at once.
                if (Active == this && Driving.Vehicle == null) RaceState.Stop();
            }
            if (Active == this && RaceState.Running) RaceState.Tick(Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (GameMode.Current != GameMode.Mode.Play || !RaceTouch.IsDriven(other)) return;
            if (!RaceState.Running)
            {
                if (Time.time >= _readyAt) Begin();
                return;
            }
            if (Active != this || RaceState.LapTime < minLapSeconds) return;
            if (RaceState.Gate < RaceState.Gates)
            {
                if (Time.time < _quietUntil) return;
                _quietUntil = Time.time + 1.5f;
                Play(missedClip);
                Missed?.Invoke(NextOrder);
                return;
            }
            bool won = RaceState.CompleteLap();
            Play(won ? finishClip : lapClip);
        }

        private void Begin()
        {
            _orders.Clear();
            foreach (var g in RaceGate.All)
                if (g != null && g.isActiveAndEnabled && !_orders.Contains(g.Order)) _orders.Add(g.Order);
            _orders.Sort();
            Active = this;
            RaceState.Begin(laps, _orders.Count);
            Play(startClip);
        }

        /// <summary>The driver went through a gate: it counts when it is the next one; any other is ignored.</summary>
        public static void Through(RaceGate gate)
        {
            var loop = Active;
            if (loop == null || gate == null || !RaceState.Running || gate.Order != loop.NextOrder) return;
            RaceState.PassGate();
            loop.Play(loop.gateClip);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && _audio != null) _audio.PlayOneShot(clip, volume);
        }
    }
}
