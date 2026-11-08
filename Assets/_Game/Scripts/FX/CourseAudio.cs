using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The course's sound: plays CourseSounds' cues for what the course announces (GameEvents: the shift, a core waking,
    /// a switch, a door, a checkpoint, a reset, the goal) and the player's jump and landing, and keeps the room's music
    /// and ambience looping. One per scene, anywhere; the cues are flat (2D), heard the same at any size.
    /// </summary>
    public sealed class CourseAudio : MonoBehaviour
    {
        [SerializeField] private CourseSounds sounds;
        [SerializeField, Tooltip("The player's motor, for its jump and landing. Found in the scene if empty.")] private PlayerMotor player;

        // A few cue sources taken in turn: each play sets its own pitch without bending one still sounding.
        private readonly AudioSource[] _cues = new AudioSource[4];
        private int _next;
        private AudioSource _music, _ambience;

        private void Awake()
        {
            for (int i = 0; i < _cues.Length; i++) _cues[i] = Source("Cue " + (i + 1), false);
            _music = Source("Music", true);
            _ambience = Source("Ambience", true);
        }

        private void Start()
        {
            if (sounds == null) return;
            Loop(_music, sounds.music, sounds.musicVolume);
            Loop(_ambience, sounds.ambience, sounds.ambienceVolume);
        }

        private void OnEnable()
        {
            if (player == null) player = FindFirstObjectByType<PlayerMotor>();
            if (player != null)
            {
                player.Jumped += OnJumped;
                player.Landed += OnLanded;
            }
            GameEvents.ShiftStarted += OnShiftStarted;
            GameEvents.SourceAwakened += OnSourceAwakened;
            GameEvents.SwitchPressed += OnSwitchPressed;
            GameEvents.DoorOpening += OnDoorOpening;
            GameEvents.CheckpointClaimed += OnCheckpointClaimed;
            GameEvents.PlayerReset += OnPlayerReset;
            GameEvents.GoalReached += OnGoalReached;
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.Jumped -= OnJumped;
                player.Landed -= OnLanded;
            }
            GameEvents.ShiftStarted -= OnShiftStarted;
            GameEvents.SourceAwakened -= OnSourceAwakened;
            GameEvents.SwitchPressed -= OnSwitchPressed;
            GameEvents.DoorOpening -= OnDoorOpening;
            GameEvents.CheckpointClaimed -= OnCheckpointClaimed;
            GameEvents.PlayerReset -= OnPlayerReset;
            GameEvents.GoalReached -= OnGoalReached;
        }

        private void OnShiftStarted(Shifter who, ShiftSize size) => Play(size == ShiftSize.Kid ? sounds?.grow : sounds?.shrink);
        private void OnSourceAwakened(ShiftSource source) => Play(sounds?.coreAwake);
        private void OnSwitchPressed(Switch pressed) => Play(sounds?.switchPress);
        private void OnDoorOpening(Door door) => Play(sounds?.doorOpen);
        private void OnCheckpointClaimed(Checkpoint checkpoint) => Play(sounds?.checkpoint);
        private void OnPlayerReset(Respawn respawn) => Play(sounds?.reset);
        private void OnGoalReached(Goal goal) => Play(sounds?.goal);
        private void OnJumped() => Play(sounds?.jump);

        private void OnLanded(float fallSpeed)
        {
            if (sounds != null && fallSpeed >= sounds.landAtLeast) Play(sounds.land);
        }

        private void Play(CourseSounds.Cue? cue)
        {
            if (cue is not { } c || c.clip == null || c.volume <= 0f) return;
            AudioSource s = _cues[_next];
            _next = (_next + 1) % _cues.Length;
            s.pitch = 1f + Random.Range(-c.pitchSpread, c.pitchSpread);
            s.PlayOneShot(c.clip, c.volume);
        }

        private static void Loop(AudioSource source, AudioClip clip, float volume)
        {
            if (clip == null || volume <= 0f) return;
            source.clip = clip;
            source.volume = volume;
            source.Play();
        }

        private AudioSource Source(string label, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }
    }
}
