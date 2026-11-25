using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// The finish marker of a Flat Zone (D-0198): a paper pennant on a pencil, a thing of the toy world. A player the
    /// zone holds runs into it (its trigger) and TAGS it: the pennant swings round the pencil and pops, turns from grey
    /// paper to the orb's cyan (colours laid over the paper's material; the material is not swapped, so the size
    /// shift's bend, which lends renderers other materials for a moment, cannot undo it), a sound plays, and the zone is cleared for that player (FlatZone.Clear: its camera
    /// swings back behind it, the stretch is 3D again).
    /// WHEN IT COUNTS: only for a player the zone is holding at that moment. A tag lets that player go, and the zone
    /// does not take it again until its body has left the zone's box, so each visit tags once. Someone the zone does
    /// not hold (first person, a kid, a player walking in from the far end) passes through it and nothing happens.
    /// WHEN IT IS READY AGAIN: the pennant stays swung round and cyan until the zone next takes a player (a new
    /// visit); then it is back at rest, grey.
    /// It lives under its zone's object (so the zone's cutaway never hides it) and needs a trigger collider on this
    /// object. Shift Jam > Flat > Land Hallway Scroller makes the piece (Prefabs/Pieces/Traversal/FlatFinish) from
    /// Unity's plain shapes and materials of our own, and places one in the hallway's zone.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FlatFinish : MonoBehaviour
    {
        [SerializeField, Tooltip("The zone this finishes. Found on a parent if empty.")] private FlatZone zone;
        [SerializeField, Tooltip("What swings round on a tag: it turns about its own up axis (the pencil's line). Nothing moves if empty.")] private Transform pennant;
        [SerializeField, Tooltip("The paper: it changes colour on a tag. Its own material is its look while it waits (grey paper). Nothing changes colour if empty.")] private Renderer paper;
        [SerializeField, Tooltip("The paper's colour once tagged (the orb's cyan).")] private Color taggedColour = new Color(0.3f, 0.9f, 1f);
        [SerializeField, ColorUsage(false, true), Tooltip("The paper's glow once tagged. Black = no glow.")] private Color taggedGlow = new Color(0.1f, 0.9f, 1.2f);
        [SerializeField, Tooltip("Degrees the pennant swings round the pencil on a tag. 180 = it ends pointing the other way (onward); 540 = a turn and a half.")] private float turn = 540f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the swing and the pop take.")] private float seconds = 0.45f;
        [SerializeField, Min(1f), Tooltip("How big the pennant gets at the top of the pop (1 = no pop).")] private float pop = 1.4f;
        [SerializeField, Tooltip("The tag's sound, heard at any distance (the side-on camera stands far off). No clip: silent.")] private CourseSounds.Cue sound = new CourseSounds.Cue(null, 0.8f);
        [SerializeField, Tooltip("Raised on a tag, after the zone has been cleared.")] private UnityEvent onTagged;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _look;   // the tagged colours, laid over the paper's material (the material itself is never changed or copied)
        private Quaternion _restTurn = Quaternion.identity;
        private Vector3 _restSize = Vector3.one;
        private float _t = 1f;      // the swing, 0 to 1 (1 = at rest, or done)
        private bool _tagged;       // shown swung round and cyan
        private bool _coloured;     // this swing has changed the paper already

        /// <summary>True from a tag until the zone next takes a player.</summary>
        public bool Tagged => _tagged;

        private void Awake()
        {
            if (zone == null) zone = GetComponentInParent<FlatZone>();
            if (pennant != null) { _restTurn = pennant.localRotation; _restSize = pennant.localScale; }
            if (zone == null) Debug.LogWarning("FlatFinish '" + name + "' has no Flat Zone above it: it cannot be tagged. Put it under a FlatZone's object.", this);
        }

        private void OnEnable() { if (zone != null) zone.Entered += MakeReady; }
        private void OnDisable() { if (zone != null) zone.Entered -= MakeReady; }

        /// <summary>The zone took a player: a new visit. A pennant left swung round by the last one goes back to rest.</summary>
        private void MakeReady()
        {
            if (!_tagged) return;
            _tagged = false; _t = 1f;
            if (pennant != null) { pennant.localRotation = _restTurn; pennant.localScale = _restSize; }
            if (paper != null) paper.SetPropertyBlock(null);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (zone == null) return;
            Shifter who = PlayerTouch.Find(other, out _);
            if (who == null || !zone.Clear(who)) return;   // not a player, or not one the zone holds: no tag
            _tagged = true; _coloured = false; _t = 0f;
            UiAudio.Play(sound);
            onTagged?.Invoke();
        }

        /// <summary>The swing: fast at first and settling, with the pop over the first half and the paper changing colour
        /// at its top. Nothing is done while the pennant is at rest.</summary>
        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / seconds);
            float eased = 1f - (1f - _t) * (1f - _t) * (1f - _t);
            float swell = Mathf.Sin(_t * Mathf.PI);   // 0, 1 at the half, 0
            if (pennant != null)
            {
                pennant.localRotation = _restTurn * Quaternion.AngleAxis(turn * eased, Vector3.up);
                pennant.localScale = _restSize * Mathf.Lerp(1f, pop, swell);
            }
            if (!_coloured && _t >= 0.5f)
            {
                _coloured = true;
                if (paper != null)
                {
                    _look ??= new MaterialPropertyBlock();
                    _look.SetColor(BaseColorId, taggedColour);
                    _look.SetColor(EmissionColorId, taggedGlow);
                    paper.SetPropertyBlock(_look);
                }
            }
        }
    }
}
