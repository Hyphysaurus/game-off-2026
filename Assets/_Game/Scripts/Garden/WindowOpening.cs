using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The end window opens onto the garden (D-0239). Window_01 is one mesh with no separate sash, so its glass goes: it
    /// fades away (DOTween) and stops blocking. Closed, the window's own collider (glass and all) blocks; open, that
    /// collider is switched off and the frame's boxes (measured by the backyard maker, under Frame) take over: the frame,
    /// its ledge, the mullion and the transom still block, the two lower panes are open (a toy walks out through one; the
    /// kid is too big for any of them). Opens when the sill's checkpoint is claimed (its On Claimed calls Open), or as
    /// the scene starts with Backyard Settings > Open On Sill unticked. Nothing is saved: a load starts it closed until
    /// the sill is reached again.
    /// The size shift's bend (WorldBend) borrows the window's materials for a moment; if a bend that began before the
    /// window opened gives the old glass back, it is taken away again (watched for a short while, nothing allocated).
    /// </summary>
    public sealed class WindowOpening : MonoBehaviour
    {
        private const float WatchAfter = 2f;   // seconds after the glass has gone that a late swap-back is watched for

        [SerializeField, Tooltip("The window's renderer (Window_01's own).")] private Renderer window;
        [SerializeField, Tooltip("The window's own collider (glass and all): switched off when it opens.")] private Collider closedCollider;
        [SerializeField, Min(0), Tooltip("Which of the window's materials is the glass.")] private int glassSlot = 1;
        [SerializeField, Tooltip("The open frame's boxes (made by the backyard maker): switched on when it opens.")] private GameObject frame;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");

        private readonly List<Material> _now = new List<Material>(4);
        private Material _glass, _fading;
        private Material[] _open, _gone;   // the window's materials while the glass fades, and once it has gone
        private Color _tint;
        private float _alpha, _watchUntil;
        private bool _opened, _faded;
        private AudioSource _audio;

        /// <summary>True once the window has opened (this play).</summary>
        public bool Opened => _opened;

        private void Awake()
        {
            if (frame != null) frame.SetActive(false);
            enabled = false;   // Update only runs while the glass is watched
            // Asked here, not in Start: a script that is off never gets Start, and this one switches itself off above.
            if (!BackyardSettings.Load().openOnSill) Open();
        }

        /// <summary>Opens the window: the sill's checkpoint calls this. Once a play.</summary>
        [ContextMenu("Open now")]
        public void Open()
        {
            if (_opened) return;
            _opened = true;
            BackyardSettings s = BackyardSettings.Load();
            if (closedCollider != null) closedCollider.enabled = false;
            if (frame != null) frame.SetActive(true);
            Play(s.windowOpens);
            if (window == null) return;
            window.GetSharedMaterials(_now);
            if (glassSlot >= _now.Count || _now[glassSlot] == null) return;
            _glass = _now[glassSlot];
            _fading = new Material(_glass) { name = _glass.name + " (opening)" };
            _tint = _fading.HasProperty(BaseColorId) ? _fading.GetColor(BaseColorId) : _fading.HasProperty(ColorId) ? _fading.GetColor(ColorId) : Color.white;
            _alpha = _tint.a;
            _open = _now.ToArray();
            _open[glassSlot] = _fading;
            // The last slot can simply be left off (a renderer draws only as many parts as it has materials); any other
            // slot keeps the copy, fully clear.
            if (glassSlot == _now.Count - 1)
            {
                _gone = new Material[_now.Count - 1];
                for (int i = 0; i < _gone.Length; i++) _gone[i] = _now[i];
            }
            else _gone = _open;
            window.sharedMaterials = _open;
            enabled = true;
            DOTween.To(() => _alpha, a => { _alpha = a; Tint(a); }, 0f, Mathf.Max(0.05f, s.glassFadeSeconds))
                .SetEase(Ease.InQuad)
                .SetLink(gameObject)
                .OnComplete(Gone);
        }

        private void Tint(float alpha)
        {
            if (_fading == null) return;
            var c = new Color(_tint.r, _tint.g, _tint.b, alpha);
            if (_fading.HasProperty(BaseColorId)) _fading.SetColor(BaseColorId, c);
            if (_fading.HasProperty(ColorId)) _fading.SetColor(ColorId, c);
        }

        private void Gone()
        {
            _faded = true;
            _watchUntil = Time.time + WatchAfter;
            if (window != null) window.sharedMaterials = _gone;
        }

        private void Update()
        {
            // A bend that took the materials before the window opened puts the old glass back when it ends: take it again.
            if (window != null && _glass != null)
            {
                window.GetSharedMaterials(_now);
                if (glassSlot < _now.Count && _now[glassSlot] == _glass) window.sharedMaterials = _faded ? _gone : _open;
            }
            if (_faded && Time.time >= _watchUntil) enabled = false;
        }

        private void Play(CourseSounds.Cue cue)
        {
            if (cue.clip == null || cue.volume <= 0f) return;
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }
            _audio.pitch = 1f + Random.Range(-cue.pitchSpread, cue.pitchSpread);
            _audio.PlayOneShot(cue.clip, cue.volume);
        }

        private void OnDestroy()
        {
            if (_fading != null) Destroy(_fading);
        }
    }
}
