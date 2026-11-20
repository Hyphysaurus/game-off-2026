using System.Collections;
using System.Collections.Generic;
using TinyGiantStudio.Layout;
using TinyGiantStudio.Text;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Words in solid 3D letters (Modular 3D Text, made when the scene starts), fitted to a width and read from the
    /// object's -Z side like the card signs. Two uses in the sample (D-0098): a marker-pen sign on a strip of packing tape
    /// whose letters pop on one by one when the player comes near, and ink stamped on the toy box ("THIS WAY UP") whose
    /// letters tumble off when the box bursts (BoxBurst calls Tumble). The letters never block the player.
    /// </summary>
    public sealed class Lettering : MonoBehaviour
    {
        public enum Reveal { Always, WhenNear }

        [SerializeField] private TinyGiantStudio.Text.Font font;
        [SerializeField] private Material material;
        [SerializeField, TextArea] private string words = "WORDS";
        [SerializeField, Tooltip("The widest line fills this, in the object's own units.")] private float width = 6f;
        [SerializeField, Tooltip("The lines together are at most this tall, in the object's own units (0 = only the width counts).")] private float height;
        [SerializeField, Tooltip("Letter depth, as a share of their height.")] private float depth = 0.15f;
        [SerializeField] private Reveal reveal = Reveal.Always;
        [SerializeField, Tooltip("WhenNear: the player within this many units (world, measured across the floor) starts the letters popping on.")] private float nearRange = 14f;
        [SerializeField, Tooltip("Seconds between one letter popping on and the next.")] private float popEach = 0.045f;
        [SerializeField, Tooltip("Seconds one letter takes to pop on.")] private float popTime = 0.28f;

        private Modular3DText _text;
        private Transform _holder;
        private readonly List<Transform> _letters = new List<Transform>();
        private Vector3[] _scales = new Vector3[0];
        private Transform _player;
        private bool _ready, _shown, _tumbled;

        /// <summary>The letters are made and placed.</summary>
        public bool Ready => _ready;
        /// <summary>The letters have popped on (or always showed).</summary>
        public bool Shown => _shown;
        /// <summary>How many letters there are (spaces are not letters).</summary>
        public int Count => _letters.Count;
        public string Words => words;

        private void Start()
        {
            if (font == null) { Debug.LogWarning($"Lettering {name}: no font"); return; }
            var go = new GameObject("Letters");
            _holder = go.transform;
            _holder.SetParent(transform, false);
            var layout = go.AddComponent<GridLayoutGroup>();
            layout.bounds = new Bounds[0];          // filled only by a saved scene: made at run time it starts null
            layout.alwaysUpdateBounds = true;       // and measures its letters every update
            layout.Spacing = new Vector2(5f, 60f);  // the library's letter gap; lines further apart (5 let marker lines touch)
            _text = go.AddComponent<Modular3DText>();
            _text.Font = font;
            if (material != null) _text.Material = material;
            _text.FontSize = new Vector3(8f, 8f, 8f * depth);   // the library's own size: its letter spacing is tuned for 8 (at 1 the gaps were wide); Build scales the word to fit
            _text.Text = words;
            var p = GameObject.Find("Player");
            if (p != null) _player = p.transform;
            StartCoroutine(Build());
        }

        private IEnumerator Build()
        {
            // The text makes its letters at the end of a frame; wait for them, then a frame more for the layout.
            for (int i = 0; i < 60 && _text.characterObjectList.Count == 0; i++) yield return null;
            yield return null;
            _letters.Clear();
            foreach (var c in _text.characterObjectList)
            {
                if (c == null) continue;
                var mf = c.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0) continue;   // a space
                _letters.Add(c.transform);
            }
            if (_letters.Count == 0) { Debug.LogWarning($"Lettering {name}: no letters made for \"{words}\""); yield break; }

            // Fit: the letters' bounds in this object's space, scaled to the width and centred on it.
            Bounds b = LocalBounds();
            float s = b.size.x > 0.0001f ? width / b.size.x : 1f;
            if (height > 0f && b.size.y > 0.0001f) s = Mathf.Min(s, height / b.size.y);   // three lines on a short strip
            _holder.localScale = Vector3.one * s;
            b = LocalBounds();
            _holder.localPosition -= new Vector3(b.center.x, b.center.y, 0f);

            _scales = new Vector3[_letters.Count];
            for (int i = 0; i < _letters.Count; i++) _scales[i] = _letters[i].localScale;
            if (reveal == Reveal.WhenNear) foreach (var l in _letters) l.localScale = Vector3.zero;
            else _shown = true;
            _ready = true;
        }

        private Bounds LocalBounds()
        {
            bool any = false; Bounds b = default;
            foreach (var l in _letters)
            {
                var r = l.GetComponent<Renderer>(); if (r == null) continue;
                Bounds wb = r.bounds;
                // the eight corners into this object's space
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 local = transform.InverseTransformPoint(corner);
                    if (!any) { b = new Bounds(local, Vector3.zero); any = true; } else b.Encapsulate(local);
                }
            }
            return b;
        }

        private void Update()
        {
            if (!_ready || _shown || _tumbled || reveal != Reveal.WhenNear) return;
            if (_player == null) { var p = GameObject.Find("Player"); if (p == null) return; _player = p.transform; }
            // across the floor only: a sign high on a wall (the kid's, 62 up) pops on for whoever stands below it too
            Vector3 d = _player.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > nearRange * nearRange) return;
            PopOn();
        }

        /// <summary>Pops the letters on now, one after another.</summary>
        public void PopOn()
        {
            if (!_ready || _shown) return;
            _shown = true;
            StartCoroutine(Pop());
        }

        private IEnumerator Pop()
        {
            float t = 0f, end = (_letters.Count - 1) * popEach + popTime;
            while (t < end)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < _letters.Count; i++)
                {
                    if (_letters[i] == null) continue;
                    float k = Mathf.Clamp01((t - i * popEach) / Mathf.Max(0.01f, popTime));
                    _letters[i].localScale = _scales[i] * Overshoot(k);
                }
                yield return null;
            }
            for (int i = 0; i < _letters.Count; i++) if (_letters[i] != null) _letters[i].localScale = _scales[i];
        }

        /// <summary>0 to 1 with a springy overshoot (about 15 %) on the way.</summary>
        private static float Overshoot(float k)
        {
            if (k <= 0f) return 0f;
            if (k >= 1f) return 1f;
            const float c1 = 1.9f, c3 = c1 + 1f;
            float u = k - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>The letters come off and tumble (the box bursting): each gets its own body, a push away from
        /// <paramref name="from"/> and up, a spin. They never push the player.</summary>
        public void Tumble(Vector3 from)
        {
            if (!_ready || _tumbled) return;
            _tumbled = true;
            StopAllCoroutines();
            var playerColliders = _player != null ? _player.GetComponentsInChildren<Collider>() : new Collider[0];
            float size = _holder.lossyScale.x;
            for (int i = 0; i < _letters.Count; i++)
            {
                Transform l = _letters[i];
                if (l == null) continue;
                if (_scales.Length > i) l.localScale = _scales[i];
                l.SetParent(null, true);
                var col = l.gameObject.AddComponent<BoxCollider>();
                foreach (var pc in playerColliders) if (pc != null) Physics.IgnoreCollision(col, pc);
                var rb = l.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                Vector3 away = l.position - from; away.y = 0f;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.onUnitSphere;
                rb.linearVelocity = (away * Random.Range(1.5f, 3.5f) + Vector3.up * Random.Range(2f, 4f)) * size;
                rb.angularVelocity = Random.insideUnitSphere * 6f;
            }
        }
    }
}
