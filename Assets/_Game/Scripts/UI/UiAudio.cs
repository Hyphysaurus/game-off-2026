using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Plays the UI's sounds (UiSounds, D-0100) from one 2D source that outlives scene changes and plays on while the game
    /// is paused or held at time 0 (the title, the pause). Any script calls <see cref="Play"/>; menu controls get theirs
    /// through UiSound.
    /// </summary>
    public static class UiAudio
    {
        public enum Kind { Move, Click, Confirm, Back, Select, Denied, Open, Toast, Win, None }

        private static AudioSource _source;
        private static float _lastMove = -1f, _hushUntil = -1f, _lastAt = -1f;
        private static Kind _last = Kind.None;

        /// <summary>A menu was just opened or rebuilt: the selection it sets for itself in the next moment is not a move.</summary>
        public static void Hush(float seconds = 0.15f) => _hushUntil = Time.unscaledTime + seconds;

        public static bool Hushed => Time.unscaledTime < _hushUntil;

        public static void Play(Kind kind)
        {
            var sounds = UiSounds.Load();
            if (sounds == null || kind == Kind.None) return;
            if (kind == _last && Time.unscaledTime - _lastAt < 0.08f) return;   // one event heard twice (a hint and a refill together) plays once
            _last = kind; _lastAt = Time.unscaledTime;
            CourseSounds.Cue cue;
            switch (kind)
            {
                case Kind.Move:
                    if (Hushed || Time.unscaledTime - _lastMove < sounds.moveGap) return;
                    _lastMove = Time.unscaledTime; cue = sounds.move; break;
                case Kind.Click: cue = sounds.click; break;
                case Kind.Confirm: cue = sounds.confirm; break;
                case Kind.Back: cue = sounds.back; break;
                case Kind.Select: cue = sounds.select; break;
                case Kind.Denied: cue = sounds.denied; break;
                case Kind.Open: cue = sounds.open; break;
                case Kind.Toast: cue = sounds.toast; break;
                default: cue = sounds.win; break;
            }
            if (cue.clip == null || cue.volume <= 0f) return;
            var s = Source();
            s.pitch = 1f + Random.Range(-cue.pitchSpread, cue.pitchSpread);
            s.PlayOneShot(cue.clip, cue.volume);
        }

        /// <summary>Plays one cue of a part's own (the title menu's toys, D-0200) from the same source: heard with the game
        /// held and the listener paused. A cue with no clip is silent, and so is everything outside Play (an editor check
        /// may run a menu's code: the source cannot be made there).</summary>
        public static void Play(CourseSounds.Cue cue)
        {
            if (cue.clip == null || cue.volume <= 0f || !Application.isPlaying) return;
            var s = Source();
            s.pitch = 1f + Random.Range(-cue.pitchSpread, cue.pitchSpread);
            s.PlayOneShot(cue.clip, cue.volume);
        }

        private static AudioSource Source()
        {
            if (_source != null) return _source;
            var go = new GameObject("UI Audio");
            Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.ignoreListenerPause = true;   // the pause menu pauses the listener: its own clicks still sound
            return _source;
        }
    }
}
