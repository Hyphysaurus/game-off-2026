using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The course's sound: plays CourseSounds' cues for what the course announces (GameEvents: the shift, a core waking,
    /// a switch, a door, a checkpoint, a reset, the goal, E refused, a pick-up and a set-down, a weapon taken, a soldier
    /// standing up, an end card choice, the opening's lid, spark and get-up) and the player's jump and landing, keeps the
    /// room's ambience looping, and plays the music. One per scene, anywhere; everything is flat (2D), heard the same at
    /// any size.
    /// The music (D-0211) is decided every frame from what is going on: the title's track while the title is up, none
    /// while the opening plays (New Game), else the scene's track (Music here, or the shared one in CourseSounds). A change
    /// of track crossfades on two players: the old one goes over Music Out while the new one starts from its beginning and
    /// comes in over Music In. While a room reveal's own music swells the track dips to silence, waits in place, and comes
    /// back up after. The fades run on unscaled time (the title stops time); the timings are on CourseSounds.
    /// </summary>
    public sealed class CourseAudio : MonoBehaviour
    {
        [SerializeField, Tooltip("What the course sounds like (Data/CourseSounds). Empty = no sound at all here.")] private CourseSounds sounds;
        [SerializeField, Tooltip("The player's motor, for its jump and landing. Found in the scene if empty.")] private PlayerMotor player;
        [SerializeField, Tooltip("This scene's own music (D-0211: each sandbox has one). Empty = the shared music in Sounds (the house's).")]
        private AudioClip music;
        [SerializeField, Range(0f, 1f), Tooltip("This scene's own music's volume (used only with a track of its own above). 0 = no music in this scene.")]
        private float musicVolume = 0.3f;

        // A few cue sources taken in turn: each play sets its own pitch without bending one still sounding.
        private readonly AudioSource[] _cues = new AudioSource[4];
        private int _next;
        private AudioSource _ambience;

        /// <summary>One of the two music players: the track it plays (null = free), how loud that track is meant to be,
        /// and how far it has faded in (0 silent .. 1 full).</summary>
        private sealed class Track
        {
            public AudioSource source;
            public AudioClip clip;
            public float volume, level;
            public bool held;   // paused in place under a reveal's swell
        }
        private Track _now, _going;   // the track playing or coming in; the one fading out
        private int _swells;          // room reveals whose own music is sounding now
        private float _dip = 1f;      // 1 = the music at its level, 0 = dipped to silence under a swell

        /// <summary>Each cue as it plays: its name in CourseSounds and its clip (for checks: a batch run is muted).</summary>
        public static event System.Action<string, AudioClip> CuePlayed;

        private void Awake()
        {
            for (int i = 0; i < _cues.Length; i++) _cues[i] = Source("Cue " + (i + 1), false);
            _now = new Track { source = Source("Music A", true) };
            _going = new Track { source = Source("Music B", true) };
            _ambience = Source("Ambience", true);
        }

        private void Start()
        {
            if (sounds == null) return;
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
            GameEvents.RevealSwell += OnRevealSwell;
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
            GameEvents.RevealSwell -= OnRevealSwell;
            _swells = 0;   // a swell's end heard while off is missed: start over rather than stay dipped
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
        private void OnRevealSwell(RoomReveal reveal, bool on) => _swells = Mathf.Max(0, _swells + (on ? 1 : -1));

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

        // ------------------------------------------------------------------ the music (D-0211)

        private void Update()
        {
            if (sounds == null) return;
            // Unscaled: the title holds time at 0. Capped at the game's own longest step, so one long frame (a scene
            // loading) does not jump a fade.
            float dt = Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);

            Wanted(out AudioClip clip, out float volume);
            if (clip != _now.clip) Change(clip);
            _now.volume = volume;

            bool swelling = _swells > 0;
            _dip = Mathf.MoveTowards(_dip, swelling ? 0f : 1f, Step(dt, swelling ? sounds.musicDip : sounds.musicIn));
            float dip = Smooth(_dip);

            if (_now.clip != null)
            {
                _now.level = Mathf.MoveTowards(_now.level, 1f, Step(dt, sounds.musicIn));
                _now.source.volume = _now.volume * Smooth(_now.level) * dip;
                Hold(_now, swelling && _dip <= 0f);   // all the way down: wait in place, and go on from there after
            }
            if (_going.clip != null)
            {
                _going.level = Mathf.MoveTowards(_going.level, 0f, Step(dt, sounds.musicOut));
                _going.source.volume = _going.volume * Smooth(_going.level) * dip;
                if (_going.level <= 0f) Free(_going);
            }
        }

        /// <summary>The track for this moment and its volume; null = silence (an empty clip or a volume of 0 is silence too).</summary>
        private void Wanted(out AudioClip clip, out float volume)
        {
            if (TitleMenu.IsOpen) { clip = sounds.titleMusic; volume = sounds.titleMusicVolume; }
            else if (Opening.Playing) { clip = null; volume = 0f; }   // New Game: the title's goes, the scene's waits out the opening
            else if (music != null) { clip = music; volume = musicVolume; }
            else { clip = sounds.music; volume = sounds.musicVolume; }
            if (volume <= 0f) clip = null;
        }

        /// <summary>Another track (or silence): the one playing starts going, and the new one starts from its beginning on
        /// the other player. A track still going when a third comes is cut.</summary>
        private void Change(AudioClip clip)
        {
            if (_now.clip != null)
            {
                if (_going.clip != null) Free(_going);
                (_now, _going) = (_going, _now);
            }
            _now.clip = clip;
            _now.level = 0f;
            if (clip == null) return;
            _now.source.clip = clip;
            _now.source.volume = 0f;
            _now.source.Play();
        }

        private static void Hold(Track track, bool on)
        {
            if (track.held == on) return;
            track.held = on;
            if (on) track.source.Pause(); else track.source.UnPause();
        }

        private static void Free(Track track)
        {
            track.source.Stop();
            track.source.clip = null;
            track.clip = null;
            track.level = 0f;
            track.held = false;
        }

        /// <summary>How far a fade of this many seconds moves in dt (0 seconds = all the way at once).</summary>
        private static float Step(float dt, float seconds) => seconds > 0f ? dt / seconds : 1f;
        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // ------------------------------------------------------------------ cues and loops

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
