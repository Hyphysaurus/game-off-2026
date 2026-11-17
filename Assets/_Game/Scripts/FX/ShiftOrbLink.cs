using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Joins a shift core's rule (ShiftSource) to its look (ShiftOrb): awake or dormant, the white flare when it is
    /// used, his yellow bolts when the shift it started has finished, and who the orb stirs for (the player, at their
    /// current size). The orb itself knows nothing about the game.
    /// <see cref="Dress"/> puts the orb on a core that was built without one (every core in the course, the Workshop
    /// and the Pocket Core): it switches the old mote, sparks and ember off and hangs Resources/FX/ShiftOrbVisual on
    /// the core, sized to the core's body. In the editor's scene view those cores still show the old mote; the orb
    /// appears in Play.
    /// </summary>
    public sealed class ShiftOrbLink : MonoBehaviour
    {
        [SerializeField] private ShiftSource source;
        [SerializeField] private ShiftOrb orb;

        private const string VisualResource = "FX/ShiftOrbVisual";
        /// <summary>The Pocket Core stays warm, so it reads apart from the course's cyan cores (D-0032).</summary>
        private static readonly Color Amber = new Color(2.0f, 1.15f, 0.3f);

        /// <summary>Time.time of the last shift any core started. ShiftEndBolts reads it: a shift that began at a
        /// core gets its bolts around that orb, any other shift gets them around the player.</summary>
        public static float LastUse { get; private set; } = -100f;

        private static GameObject _visual;
        private bool _owesBolts;
        private Shifter _player;
        private float _lookAgain;
        private bool _bound;

        /// <summary>Gives a core the orb if it has none. Safe to call on any core, any number of times.</summary>
        public static void Dress(ShiftSource core)
        {
            if (core == null || core.GetComponentInChildren<ShiftOrb>(true) != null) return;
            if (_visual == null) _visual = Resources.Load<GameObject>(VisualResource);
            if (_visual == null) return;   // not built yet (Shift Jam > Dev > Build Shift Orb): the old looks stay

            float radius = BodyRadius(core);
            // The old looks: whatever the core listed, and anything else under it that draws or lights (the Pocket
            // Core's glow and light). Only the drawing is switched off: colliders and objects stay as they were.
            core.RetireLooks();
            foreach (var r in core.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var l in core.GetComponentsInChildren<Light>(true)) l.enabled = false;

            var worn = Instantiate(_visual, core.transform);
            worn.name = "Orb";
            float parent = Mathf.Max(Mathf.Abs(core.transform.lossyScale.x), 1e-4f);
            worn.transform.localPosition = Vector3.zero;
            worn.transform.localRotation = Quaternion.identity;
            worn.transform.localScale = Vector3.one * (radius / parent);   // the visual's crystal has radius 1
            foreach (var l in worn.GetComponentsInChildren<Light>(true)) l.range *= radius;

            var look = worn.GetComponent<ShiftOrb>();
            if (look != null && core.GetComponent<PocketCore>() != null) look.SetGlow(Amber);
            var link = core.gameObject.AddComponent<ShiftOrbLink>();
            link.source = core;
            link.orb = look;
            link.Bind();
        }

        /// <summary>How big the crystal should be: the core's own body if it has one, else most of its reach.</summary>
        private static float BodyRadius(ShiftSource core)
        {
            if (core.TryGetComponent(out MeshFilter body) && body.sharedMesh != null)
                return Mathf.Max(body.sharedMesh.bounds.extents.x * Mathf.Abs(core.transform.lossyScale.x), 0.25f);
            if (core.TryGetComponent(out Collider reach))
                return Mathf.Max(reach.bounds.extents.x * (reach.isTrigger ? 0.8f : 1f), 0.25f);
            return 1.5f;
        }

        private void OnEnable() => Bind();

        private void OnDisable()
        {
            if (_bound && source != null) source.Used -= OnUsed;
            if (_bound) GameEvents.Shifted -= OnShifted;
            _bound = false;
            _owesBolts = false;
        }

        private void Bind()
        {
            if (_bound || source == null || orb == null) return;
            source.Used += OnUsed;
            GameEvents.Shifted += OnShifted;
            _bound = true;
            orb.SetAwake(source.Usable);
        }

        /// <summary>The core was used: the crystal flares now, and it owes the bolts when this shift ends.</summary>
        private void OnUsed()
        {
            LastUse = Time.time;
            _owesBolts = true;
            if (orb != null) orb.Strike();
        }

        /// <summary>A shift finished. If this core started it, the yellow bolts burst around its orb.</summary>
        private void OnShifted(Shifter who, ShiftSize size)
        {
            if (!_owesBolts) return;
            _owesBolts = false;
            if (orb != null) orb.Bolts();
        }

        private void Update()
        {
            if (source == null || orb == null) return;
            orb.SetAwake(source.Usable);
            if (_player == null)
            {
                if (Time.time < _lookAgain) return;
                _lookAgain = Time.time + 1f;
                _player = FindFirstObjectByType<Shifter>();
                if (_player == null) return;
            }
            orb.SetViewer(_player.transform, _player.Scale);
        }
    }
}
