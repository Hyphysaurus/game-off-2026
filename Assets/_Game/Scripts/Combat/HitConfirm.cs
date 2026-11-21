using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What the player hears and feels when one of its own shots lands on something with health: a short sound at the
    /// ear (not placed in the room, so it is as clear at any range), and for the hit that knocks the thing down a
    /// bigger sound and a small nudge of the camera (third person: a brief shake; first person: the view kicks up
    /// and springs back). A grenade's blast counts the same way, as it does for the hit marker. Hits that land together
    /// (a shotgun's pellets) make one sound. No freeze-frames.
    /// Ours, not the test room's. It only listens (GameEvents.ShotLanded and GrenadeWentOff); nothing knows it exists.
    /// Added by the WeaponHolder for a player; numbers and clips in Resources/WeaponAmmo. The clips are licensed and
    /// live under _Imports: a project without them is silent here and runs the same.
    /// </summary>
    public sealed class HitConfirm : MonoBehaviour
    {
        private WeaponHolder _holder;
        private WeaponAmmoSettings _s;
        private AudioSource _ear, _earDown;   // two, so a hit's sound never bends the pitch of a knock-down's still playing
        private ShoulderCamera _view;
        private float _hitAt = -10f, _downAt = -10f;   // when each kind of sound last played

        /// <summary>Sounds played at the ear since the scene began: for a hit, and for a knock-down (for checks).</summary>
        public int HitsHeard { get; private set; }
        public int DownsHeard { get; private set; }
        /// <summary>Camera nudges given since the scene began (for checks).</summary>
        public int Nudges { get; private set; }

        private void Awake()
        {
            _holder = GetComponent<WeaponHolder>();
            _s = WeaponAmmoSettings.Load();
            _ear = Ear();
            _earDown = Ear();
        }

        private AudioSource Ear()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // at the ear, wherever the shot landed
            return source;
        }

        private void OnEnable() { GameEvents.ShotLanded += OnShotLanded; GameEvents.GrenadeWentOff += OnGrenadeWentOff; }
        private void OnDisable() { GameEvents.ShotLanded -= OnShotLanded; GameEvents.GrenadeWentOff -= OnGrenadeWentOff; }

        private void OnShotLanded(WeaponHolder who, bool finished) { if (who != null && who == _holder) Confirm(finished); }
        private void OnGrenadeWentOff(GrenadeThrower who, int hits, bool finished) { if (hits > 0 && who != null && who.gameObject == gameObject) Confirm(finished); }

        /// <summary>One of the player's hits counted on something with health; <paramref name="finished"/>: it knocked
        /// the thing down. Hits closer together than the settings' Landed Gap make one sound (and one nudge), and a
        /// hit right after a knock-down is not heard under it.</summary>
        public void Confirm(bool finished)
        {
            float now = Time.time;
            if (finished)
            {
                if (now - _downAt < _s.landedGap) return;
                _downAt = now;
                if (Play(_earDown, _s.downedClips, _s.downedVolume)) DownsHeard++;
                Nudge();
            }
            else
            {
                if (now - _hitAt < _s.landedGap || now - _downAt < _s.landedGap) return;
                _hitAt = now;
                if (Play(_ear, _s.landedClips, _s.landedVolume)) HitsHeard++;
            }
        }

        private bool Play(AudioSource source, AudioClip[] clips, float volume)
        {
            if (volume <= 0f || clips == null || clips.Length == 0) return false;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return false;   // the licensed clip is not in this project
            source.pitch = Mathf.Pow(Mathf.Max(1f, _s.landedPitchSpread), Random.Range(-1f, 1f));
            source.PlayOneShot(clip, volume);
            return true;
        }

        /// <summary>The small camera nudge of a knock-down: the view's kick in first person, a brief shake in third.</summary>
        private void Nudge()
        {
            if (_holder != null && _holder.FirstPerson)
            {
                if (_s.downViewPunch <= 0f) return;
                _holder.Punch(new Vector2(-_s.downViewPunch, 0f));
                Nudges++;
                return;
            }
            if (_s.downNudge <= 0f || _s.downNudgeSeconds <= 0f) return;
            if (_view == null) { Camera cam = PlayerSlot.ViewFor(this); if (cam != null) cam.TryGetComponent(out _view); }
            if (_view == null) return;
            _view.Shake(_s.downNudge, _s.downNudgeSeconds);
            Nudges++;
        }
    }
}
