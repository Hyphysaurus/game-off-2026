using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// The cardboard box the player wakes in. When the first shift to kid size begins, the top flaps pop up first, then
    /// each wall falls outward around its bottom OUTER edge, so it lands flat on the floor (with its flap lying beyond
    /// it, still attached), with a little bounce, a puff of dust and a thump where it lands. Hinges are worked out from
    /// where each piece sits relative to the box centre, so nothing needs a pivot set by hand.
    /// </summary>
    public sealed class BoxBurst : MonoBehaviour
    {
        [SerializeField, Tooltip("The walls (a floor in the list is fine: it is left alone).")] private Transform[] panels;
        [SerializeField, Tooltip("Top flaps, each a CHILD of the wall it folds from: they pop up before the walls fall.")] private Transform[] flaps;
        [SerializeField, Tooltip("Old single lid (left for older scenes): flips over the back edge.")] private Transform lid;
        [SerializeField] private float flapAngle = 90f, flapSeconds = 0.22f;
        [SerializeField] private float wallAngle = 90f, wallSeconds = 0.55f;
        [SerializeField, Tooltip("Seconds between the flaps opening and the first wall starting to fall.")] private float wallDelay = 0.12f;
        [SerializeField, Tooltip("Each wall starts up to this many seconds after the first.")] private float stagger = 0.14f;
        [SerializeField, Tooltip("Degrees a wall bounces back up after landing.")] private float bounce = 6f;
        [SerializeField] private float lidAngle = 150f;
        [SerializeField, Tooltip("Burst on the first shift to kid size. Off = only BurstNow() fires it.")] private bool burstOnShift = true;
        [SerializeField, Tooltip("A dust puff spawned where each wall lands (the pack's FX_Dust).")] private GameObject dust;
        [SerializeField] private float dustScale = 0.35f;
        [SerializeField, Tooltip("Drawn on the dust puff. The pack's FX_Dust points at a material the pack does not ship, so it renders magenta; empty = Resources/FX/BoxDust.")]
        private Material dustMaterial;
        [SerializeField] private AudioClip popSound, landSound;
        [SerializeField, Range(0f, 1f)] private float volume = 0.9f;
        [SerializeField, Tooltip("Hook Feel feedbacks here: the crack, a camera shake.")] private UnityEvent onBurst;
        [SerializeField, Tooltip("Fires as each wall lands: Feel's thump.")] private UnityEvent onWallLanded;

        private struct Fall { public Transform t; public Vector3 point; public Vector3 axis; public float angle; public float delay; public float seconds; public float turned; public bool wall; public bool landed; }
        private readonly List<Fall> _falls = new List<Fall>();
        private float _elapsed;

        public bool Burst { get; private set; }

        private void OnEnable() { GameEvents.ShiftStarted += OnShiftStarted; }
        private void OnDisable() { GameEvents.ShiftStarted -= OnShiftStarted; }

        private void OnShiftStarted(Shifter who, ShiftSize size)
        {
            if (burstOnShift && size == ShiftSize.Kid) BurstNow();
        }

        /// <summary>Opens the box. Safe to call twice; the second call does nothing.</summary>
        public void BurstNow()
        {
            if (Burst) return;
            Burst = true;
            _elapsed = 0f;
            Vector3 centre = Centre();   // the middle of the walls, not this object's origin (it sat 20 off: walls fell inward / askew)
            bool anyFlap = false;
            if (flaps != null)
                foreach (Transform flap in flaps)
                {
                    if (flap == null) continue;
                    Vector3 outward = Flat(flap.parent != null ? flap.parent.position - centre : flap.position - centre);
                    if (outward == Vector3.zero) continue;
                    Vector3 e = Extents(flap);
                    _falls.Add(new Fall
                    {
                        t = flap,
                        // hinge one board-thickness in from its outer edge, on the wall's top: the flap stands up in the wall's own
                        // plane, so when the wall lands the flap lies flat on the floor beyond it (not under it)
                        point = flap.position + outward * (Along(e, outward) - 2f * e.y) + Vector3.down * e.y,
                        axis = Vector3.Cross(Vector3.up, outward),
                        angle = flapAngle, delay = Random.Range(0f, 0.05f), seconds = flapSeconds,
                    });
                    Off(flap); anyFlap = true;
                }
            if (anyFlap) Play(popSound, transform.position + Vector3.up * 20f);
            foreach (Transform panel in panels)
            {
                if (panel == null || panel == lid) continue;
                Vector3 outward = Flat(panel.position - centre);
                if (outward == Vector3.zero) continue;                       // the floor: leave it
                Vector3 e = Extents(panel);
                _falls.Add(new Fall
                {
                    t = panel,
                    point = panel.position + Vector3.down * e.y + outward * Along(e, outward),   // bottom OUTER edge: it lands flat
                    axis = Vector3.Cross(Vector3.up, outward),
                    angle = wallAngle, delay = (anyFlap ? flapSeconds * 0.6f + wallDelay : 0f) + Random.Range(0f, stagger), seconds = wallSeconds, wall = true,
                });
                Off(panel);
            }
            if (lid != null)
            {
                Vector3 back = -transform.forward;
                _falls.Add(new Fall { t = lid, point = lid.position + back * Extents(lid).z, axis = Vector3.Cross(transform.forward, Vector3.up), angle = lidAngle, seconds = wallSeconds });
                Off(lid);
            }
            onBurst?.Invoke();
        }

        /// <summary>The box's middle on the floor: the average of the walls' board centres.</summary>
        private Vector3 Centre()
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (Transform p in panels) { if (p == null || p == lid) continue; var r = Board(p); if (r == null) continue; sum += r.bounds.center; n++; }
            return n > 0 ? sum / n : transform.position;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude < 0.0001f ? Vector3.zero : v.normalized; }
        private static float Along(Vector3 extents, Vector3 dir) => Mathf.Abs(extents.x * dir.x) + Mathf.Abs(extents.z * dir.z);
        private static void Off(Transform t) { foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = false; }

        /// <summary>The piece's own board: its renderer, or a child named "Mesh" when the piece is an unscaled hinge
        /// (walls are: a stretched cube must never be a parent, or folding children skew, 5 Nov).</summary>
        private static Renderer Board(Transform t)
        {
            if (t.TryGetComponent(out Renderer r)) return r;
            var mesh = t.Find("Mesh"); return mesh != null && mesh.TryGetComponent(out Renderer m) ? m : null;
        }

        private static Vector3 Extents(Transform t)
        {
            Renderer r = Board(t);
            return r != null ? r.bounds.extents : t.lossyScale * 0.5f;
        }

        /// <summary>0..1 of the fall: accelerates like a falling board, hits, bounces once, settles.</summary>
        private float Curve(float k, bool wall)
        {
            if (!wall) { float u = 1f - k; return 1f - u * u * u; }          // flaps: quick pop that eases out
            const float hit = 0.72f;
            if (k < hit) { float u = k / hit; return u * u; }
            float b = (k - hit) / (1f - hit);
            return 1f - (bounce / Mathf.Max(1f, wallAngle)) * Mathf.Sin(b * Mathf.PI) * (1f - b * 0.5f);
        }

        private void Update()
        {
            if (!Burst || _falls.Count == 0) return;
            _elapsed += Time.deltaTime;
            bool done = true;
            for (int i = 0; i < _falls.Count; i++)
            {
                Fall f = _falls[i];
                float k = Mathf.Clamp01((_elapsed - f.delay) / Mathf.Max(0.01f, f.seconds));
                float target = f.angle * Curve(k, f.wall);
                float step = target - f.turned;
                if (Mathf.Abs(step) > 0.0001f) f.t.RotateAround(f.point, f.axis, step);
                f.turned = target;
                if (f.wall && !f.landed && k >= 0.72f) { f.landed = true; Landed(f.t); }
                _falls[i] = f;
                if (k < 1f) done = false;
            }
            if (done) _falls.Clear();
        }

        private void Landed(Transform wall)
        {
            Renderer board = Board(wall); Vector3 at = board != null ? board.bounds.center : wall.position;
            at.y = Centre().y - Extents(wall).y + 0.3f;
            Play(landSound, at);
            onWallLanded?.Invoke();
            if (dust == null) return;
            var puff = Instantiate(dust, at, Quaternion.identity);
            puff.transform.localScale = Vector3.one * dustScale;
            var mat = dustMaterial != null ? dustMaterial : Resources.Load<Material>("FX/BoxDust");
            foreach (var ps in puff.GetComponentsInChildren<ParticleSystem>())
            {
                if (mat != null && ps.TryGetComponent(out ParticleSystemRenderer pr)) pr.sharedMaterial = mat;
                var main = ps.main; main.loop = false; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                ps.Play();
            }
            StartCoroutine(StopSoon(puff, 0.25f));
            Destroy(puff, 4f);
        }

        private static System.Collections.IEnumerator StopSoon(GameObject puff, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (puff != null) foreach (var ps in puff.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Play(AudioClip clip, Vector3 at) { if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume); }
    }
}
