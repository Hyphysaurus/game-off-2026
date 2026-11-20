using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The course's sound: plays CourseSounds' cues for what the course announces (GameEvents: the shift, a core waking,
    /// a switch, a door, a checkpoint, a reset, the goal, E refused, a pick-up and a set-down, a weapon taken, a soldier
    /// standing up, an end card choice, the opening's lid, spark and get-up) and the player's jump and landing, and keeps the room's music and ambience
    /// looping. One per scene, anywhere; the cues are flat (2D), heard the same at any size.
    /// </summary>
    public sealed class CourseAudio : MonoBehaviour
    {
        [SerializeField] private CourseSounds sounds;
        [SerializeField, Tooltip("The player's motor, for its jump and landing. Found in the scene if empty.")] private PlayerMotor player;

        // A few cue sources taken in turn: each play sets its own pitch without bending one still sounding.
        private readonly AudioSource[] _cues = new AudioSource[4];
        private int _next;
        private AudioSource _music, _ambience;

        /// <summary>Each cue as it plays: its name in CourseSounds and its clip (for checks: a batch run is muted).</summary>
        public static event System.Action<string, AudioClip> CuePlayed;

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
            GameEvents.ShiftRefused += OnShiftRefused;
            GameEvents.UseRefused += OnUseRefused;
            GameEvents.PickedUp += OnPickedUp;
            GameEvents.SetDown += OnSetDown;
            GameEvents.WeaponTaken += OnWeaponTaken;
            GameEvents.SoldierUp += OnSoldierUp;
            GameEvents.EndChoice += OnEndChoice;
            GameEvents.OpeningLid += OnOpeningLid;
            GameEvents.OpeningSpark += OnOpeningSpark;
            GameEvents.OpeningGetUp += OnOpeningGetUp;
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
            GameEvents.ShiftRefused -= OnShiftRefused;
            GameEvents.UseRefused -= OnUseRefused;
            GameEvents.PickedUp -= OnPickedUp;
            GameEvents.SetDown -= OnSetDown;
            GameEvents.WeaponTaken -= OnWeaponTaken;
            GameEvents.SoldierUp -= OnSoldierUp;
            GameEvents.EndChoice -= OnEndChoice;
            GameEvents.OpeningLid -= OnOpeningLid;
            GameEvents.OpeningSpark -= OnOpeningSpark;
            GameEvents.OpeningGetUp -= OnOpeningGetUp;
        }

        private void OnShiftStarted(Shifter who, ShiftSize size) { if (size == ShiftSize.Kid) Play("grow", sounds?.grow); else Play("shrink", sounds?.shrink); }
        private void OnSourceAwakened(ShiftSource source) => Play("coreAwake", sounds?.coreAwake);
        private void OnSwitchPressed(Switch pressed) => Play("switchPress", sounds?.switchPress);
        private void OnDoorOpening(Door door) => Play("doorOpen", sounds?.doorOpen);
        private void OnCheckpointClaimed(Checkpoint checkpoint) => Play("checkpoint", sounds?.checkpoint);
        private void OnPlayerReset(Respawn respawn) => Play("reset", sounds?.reset);
        private void OnGoalReached(Goal goal) => Play("goal", sounds?.goal);
        private void OnShiftRefused(Shifter who, string why) => Play("refused", sounds?.refused);
        private void OnUseRefused(Interactor who, string why) => Play("cantUse", sounds?.cantUse);
        private void OnPickedUp(Transform thing) => Play("pickUp", sounds?.pickUp);
        private void OnSetDown(Transform thing) => Play("putDown", sounds?.putDown);
        private void OnWeaponTaken(WeaponHolder holder, Weapon weapon) => Play("weaponTaken", sounds?.weaponTaken);
        private void OnSoldierUp(ToySoldier soldier) => Play("soldierUp", sounds?.soldierUp);
        private void OnJumped() => Play("jump", sounds?.jump);
        private void OnOpeningLid() => Play("lidOpen", sounds?.lidOpen);
        private void OnOpeningSpark(bool arrived) { if (arrived) Play("sparkHit", sounds?.sparkHit); else Play("sparkFall", sounds?.sparkFall); }
        private void OnOpeningGetUp() => Play("getUp", sounds?.getUp);

        private void OnLanded(float fallSpeed)
        {
            if (sounds != null && fallSpeed >= sounds.landAtLeast) Play("land", sounds.land);
        }

        /// <summary>The scene is about to change: the cue plays from its own object that outlives this scene, then goes.</summary>
        private void OnEndChoice(bool playroom)
        {
            if (sounds == null || sounds.endChoice.clip == null || sounds.endChoice.volume <= 0f) return;
            var go = new GameObject("End Choice Sound");
            DontDestroyOnLoad(go);
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 0f;
            s.ignoreListenerPause = true;
            s.PlayOneShot(sounds.endChoice.clip, sounds.endChoice.volume);
            Destroy(go, sounds.endChoice.clip.length + 0.1f);
            CuePlayed?.Invoke("endChoice", sounds.endChoice.clip);
        }

        private void Play(string name, CourseSounds.Cue? cue)
        {
            if (cue is not { } c || c.clip == null || c.volume <= 0f) return;
            AudioSource s = _cues[_next];
            _next = (_next + 1) % _cues.Length;
            s.pitch = 1f + Random.Range(-c.pitchSpread, c.pitchSpread);
            s.PlayOneShot(c.clip, c.volume);
            CuePlayed?.Invoke(name, c.clip);
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
