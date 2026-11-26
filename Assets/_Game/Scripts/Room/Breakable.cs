using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A prop that breaks (D-0101): when its Damageable runs out (darts, a grenade, a punch) it is swapped for the pieces
    /// Fraggo cut its mesh into, which fly from the hit, lie about and dissolve away. After a while (BreakSettings, or the
    /// prop's own Come Back After) the prop dissolves back in where it started, once the player is not standing there,
    /// whole and full of health again, so a lesson's cover or a tower of blocks is never gone for good. A prop whose mesh
    /// was never cut dissolves away whole instead. Placed by Shift Jam / Breakables / Build Breakables.
    /// D-0240: a prop that also carries a Build Back piece (the backyard's gate panel) builds itself back with Artificer
    /// instead of dissolving in: hidden and not solid while its pieces fly in, shown with its colliders on when the last
    /// one lands. When Build Back is off or cannot play, it dissolves in as before.
    /// Online (D-0131, CoopWorld) the host's props are the real ones: one breaks on every PC (each flies its own pieces)
    /// and comes back on every PC once no player stands in its spot. On the other PCs each prop is a copy (UseMirror):
    /// a hit on it goes to the host, and it breaks and comes back when the host says (building back the same way).
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public sealed class Breakable : MonoBehaviour
    {
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly LinkedList<BreakPiece> Pieces = new LinkedList<BreakPiece>();
        private static readonly WaitForSeconds HalfSecond = new WaitForSeconds(0.5f);   // the wait between looks for a player in its spot

        [SerializeField, Tooltip("Seconds until this prop comes back once broken, its own number (the backyard's gate panel: 30, D-0240). Below 0 = Break Settings > Come Back After, every prop's. 0 = it never comes back.")]
        private float comeBackAfter = -1f;

        private Damageable _health;
        private MeshFilter _mesh;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private bool[] _rendererWasOn, _colliderWasOn;   // what hiding it turned off: showing it turns back on that, and only that
        private bool _shown = true;
        private Rigidbody _body;
        private BuildBack _build;     // D-0240: builds it back with Artificer when it has one
        private System.Action _built;
        private bool _wasKinematic;
        private Vector3 _startPos;
        private Quaternion _startRot;
        private float _reach;         // half its box's diagonal where it starts: its "own size" for the player's distance
        private Transform _player;
        private bool _mirror;        // online, not the host: a copy that breaks and comes back when the host's does
        private Coroutine _comeBack;

        /// <summary>Broken and not back yet.</summary>
        public bool Broken { get; private set; }
        /// <summary>Its number in the party's world, the same on every PC (CoopWorld); -1 offline.</summary>
        public int NetId { get; set; } = -1;

        private void Awake()
        {
            _health = GetComponent<Damageable>();
            _mesh = GetComponentInChildren<MeshFilter>();
            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
            _rendererWasOn = new bool[_renderers.Length];
            _colliderWasOn = new bool[_colliders.Length];
            _body = GetComponent<Rigidbody>();
            TryGetComponent(out _build);
            _built = Built;
            _wasKinematic = _body != null && _body.isKinematic;
            _startPos = transform.position; _startRot = transform.rotation;
            // Measured now, shown and in place (a hidden renderer is no measure of anything).
            Renderer look = _mesh != null ? _mesh.GetComponent<Renderer>() : null;
            _reach = Mathf.Max(0.5f, look != null ? look.bounds.extents.magnitude : 1f);
        }

        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        /// <summary>Seconds until it comes back: its own number, or Break Settings' when it has none (below 0).</summary>
        private float WaitOf(BreakSettings s) => comeBackAfter >= 0f ? comeBackAfter : s.comeBackAfter;

        private void OnDied(DamageInfo hit)
        {
            if (Broken) return;
            Vector3 from = hit.Point == Vector3.zero ? transform.position - Vector3.up : hit.Point;
            BreakApart(from, hit.Knock);
            CoopWorld.Broke(this, from, hit.Knock);   // online host: it breaks on every PC (D-0131)
            var s = BreakSettings.Load();
            if (s != null && WaitOf(s) > 0f) _comeBack = StartCoroutine(ComeBack(s));
        }

        /// <summary>Swapped for its pieces (or a ghost that dissolves), flying from <paramref name="from"/>, with the sound.</summary>
        private void BreakApart(Vector3 from, Vector3 knock)
        {
            if (_build != null) _build.Stop();   // broken again while it was building back: the build goes
            var s = BreakSettings.Load();
            Broken = true;
            var cut = s != null && _mesh != null && _mesh.sharedMesh != null ? s.CutOf(_mesh.sharedMesh.name) : null;
            if (cut != null) Shatter(s, cut, from, knock);
            else if (s != null && s.dissolve != null) Ghost(s);   // never cut: the whole prop dissolves away
            Show(false);
            if (s != null && s.breakSounds != null && s.breakSounds.Length > 0)
                AudioSource.PlayClipAtPoint(s.breakSounds[Random.Range(0, s.breakSounds.Length)], transform.position, s.breakVolume);
            GameEvents.RaisePropBroke(this);
        }

        private void Shatter(BreakSettings s, CarDamageFxSettings.Cut cut, Vector3 from, Vector3 knock)
        {
            var source = _mesh.GetComponent<Renderer>();
            Material[] mats = PieceLooks(s, cut, source != null ? source.sharedMaterials : null);
            Transform t = _mesh.transform;
            float size = Mathf.Max(0.1f, source != null ? source.bounds.size.magnitude : 1f);
            float k = size / 8.66f;   // a prop 5 across (5x5x5) reads 1
            Vector3 carried = _body != null ? _body.linearVelocity : Vector3.zero;
            for (int i = 0; i < cut.pieces.Length; i++)
            {
                if (cut.pieces[i] == null) continue;
                var go = new GameObject(name + " piece");
                go.transform.SetPositionAndRotation(t.TransformPoint(cut.centres[i]), t.rotation);
                go.transform.localScale = t.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = cut.pieces[i];
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = mats;
                var col = go.AddComponent<MeshCollider>(); col.sharedMesh = cut.pieces[i]; col.convex = true;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                Vector3 away = go.transform.position - from; away = away.sqrMagnitude > 1e-6f ? away.normalized : Random.onUnitSphere;
                rb.linearVelocity = carried + (away + Vector3.up * 0.4f).normalized * (Random.Range(s.burst.x, s.burst.y) * k) + knock * 0.15f;
                rb.angularVelocity = Random.insideUnitSphere * s.spin;
                var piece = go.AddComponent<BreakPiece>();
                piece.Begin(s, Pieces.AddLast(piece));
            }
            while (Pieces.Count > Mathf.Max(8, s.mostPieces)) { var old = Pieces.First.Value; Pieces.RemoveFirst(); if (old != null) old.Fade(); }
        }

        /// <summary>The materials a cut piece is drawn with: the prop's own, one a submesh, then the cut faces' (Break
        /// Settings > Inside). Also used by Build Back (D-0240) for a copy made of the pieces.</summary>
        internal static Material[] PieceLooks(BreakSettings s, CarDamageFxSettings.Cut cut, Material[] own)
        {
            if (own == null) own = new Material[0];
            var mats = new Material[cut.subMeshes + 1];
            for (int i = 0; i < cut.subMeshes; i++) mats[i] = own.Length > 0 ? own[Mathf.Min(i, own.Length - 1)] : s.inside;
            mats[cut.subMeshes] = s.inside != null ? s.inside : mats[0];
            return mats;
        }

        /// <summary>A copy of the prop that dissolves where it stood (a prop with no cut pieces).</summary>
        private void Ghost(BreakSettings s)
        {
            var go = new GameObject(name + " dissolving");
            go.transform.SetPositionAndRotation(_mesh.transform.position, _mesh.transform.rotation);
            go.transform.localScale = _mesh.transform.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = _mesh.sharedMesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = _mesh.GetComponent<Renderer>().sharedMaterials;
            var piece = go.AddComponent<BreakPiece>();
            piece.Begin(s, null);
            piece.Fade();
        }

        private IEnumerator ComeBack(BreakSettings s)
        {
            yield return new WaitForSeconds(WaitOf(s));
            float clear = s.clearOfPlayer * _reach;
            while (SomeoneNear(clear)) yield return HalfSecond;
            _comeBack = null;
            CoopWorld.CameBack(this);   // online host: back on every PC (D-0131)
            yield return Return(s);
        }

        /// <summary>True while a player stands within <paramref name="clear"/> of its spot: this PC's own player, and
        /// online on the host every player (D-0131).</summary>
        private bool SomeoneNear(float clear)
        {
            if (CoopWorld.IsHost)
            {
                foreach (var p in NetPlayer.Spawned)
                    if (p != null && (p.transform.position - _startPos).sqrMagnitude <= clear * clear) return true;
                return false;
            }
            if (_player == null) { var p = GameObject.Find("Player"); if (p != null) _player = p.transform; }
            return _player != null && (_player.position - _startPos).sqrMagnitude <= clear * clear;
        }

        /// <summary>Whole again where it started, full of health: building itself back (Build Back, D-0240), or dissolving in.</summary>
        private IEnumerator Return(BreakSettings s)
        {
            if (_body != null) { _body.linearVelocity = Vector3.zero; _body.angularVelocity = Vector3.zero; }
            transform.SetPositionAndRotation(_startPos, _startRot);
            Physics.SyncTransforms();
            _health.ResetState();
            Broken = false;
            // Its pieces fly in while it stays hidden and not solid; Built shows it, colliders on, when the last one lands.
            if (_build != null && _build.Play(_built)) yield break;
            Show(true);
            if (s != null && s.dissolve != null) yield return DissolveIn(s);
        }

        /// <summary>The build back ended (D-0240): it shows and is solid again, unless it broke again meanwhile.</summary>
        private void Built()
        {
            if (!Broken) Show(true);
        }

        // ---------------------------------------------------------------- online: a copy of the host's prop (D-0131)

        /// <summary>Online, on every PC but the host's: from now on this prop breaks and comes back when the host's does,
        /// and a hit on it goes to the host (<paramref name="relay"/>: the copy only flashes). A way back of its own that
        /// was under way stops: the host says when.</summary>
        public void UseMirror(System.Func<DamageInfo, bool> relay)
        {
            _mirror = true;
            _health.Relay = relay;
            if (_comeBack != null) { StopCoroutine(_comeBack); _comeBack = null; }
        }

        /// <summary>Back to a prop of this PC's own (the host's world turned out not to match ours).</summary>
        public void LeaveMirror()
        {
            if (!_mirror) return;
            _mirror = false;
            _health.Relay = null;
            var s = BreakSettings.Load();
            if (Broken && _comeBack == null && s != null && WaitOf(s) > 0f) _comeBack = StartCoroutine(ComeBack(s));
        }

        /// <summary>The host's prop broke: this one breaks too, its pieces this PC's own.</summary>
        public void MirrorBreak(Vector3 from, Vector3 knock)
        {
            if (_mirror && !Broken) BreakApart(from, knock);
        }

        /// <summary>It was already broken on the host when we arrived: gone, without pieces.</summary>
        public void MirrorBroken()
        {
            if (!_mirror || Broken) return;
            if (_build != null) _build.Stop();
            Broken = true;
            Show(false);
        }

        /// <summary>The host's prop came back: this one comes back too, building back or dissolving in.</summary>
        public void MirrorBack()
        {
            if (_mirror && Broken) StartCoroutine(Return(BreakSettings.Load()));
        }

        /// <summary>The prop's own materials swapped for dissolve copies, run from gone to whole, then put back.</summary>
        private IEnumerator DissolveIn(BreakSettings s)
        {
            var saved = new List<Material[]>();
            var live = new List<Material>();
            foreach (var r in _renderers)
            {
                if (r == null) { saved.Add(null); continue; }
                saved.Add(r.sharedMaterials);
                var copies = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < copies.Length; i++) { copies[i] = DissolveCopy(s, r.sharedMaterials[i]); live.Add(copies[i]); }
                r.sharedMaterials = copies;
            }
            for (float t = 0f; t < s.dissolveSeconds; t += Time.deltaTime)
            {
                float d = 1f - t / s.dissolveSeconds;
                foreach (var m in live) m.SetFloat(DissolveId, d);
                yield return null;
            }
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null && saved[i] != null) _renderers[i].sharedMaterials = saved[i];
            foreach (var m in live) Destroy(m);
        }

        /// <summary>The dissolve material with <paramref name="from"/>'s colour and texture.</summary>
        internal static Material DissolveCopy(BreakSettings s, Material from)
        {
            var m = new Material(s.dissolve);
            if (from != null)
            {
                if (from.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, from.GetTexture(BaseMapId));
                if (from.HasProperty(BaseColorId)) m.SetColor(BaseColorId, from.GetColor(BaseColorId));
            }
            m.SetColor(EdgeColorId, s.dissolveEdge);
            m.SetFloat(DissolveId, 0f);
            return m;
        }

        /// <summary>Hidden and not solid, or shown again. Hiding remembers which renderers and colliders were on, and showing
        /// turns back on those only: a part switched off on purpose stays off (D-0240: the gate panel keeps its pack box off
        /// and stands on its per-slat boxes). Hidden twice (broken again while building back) keeps the first record.</summary>
        private void Show(bool on)
        {
            if (on == _shown) return;
            _shown = on;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                if (!on) { _rendererWasOn[i] = r.enabled; r.enabled = false; }
                else if (_rendererWasOn[i]) r.enabled = true;
            }
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider c = _colliders[i];
                if (c == null) continue;
                if (!on) { _colliderWasOn[i] = c.enabled; c.enabled = false; }
                else if (_colliderWasOn[i]) c.enabled = true;
            }
            if (_body != null) _body.isKinematic = on ? _wasKinematic : true;
        }

        internal static void Forget(LinkedListNode<BreakPiece> node) { if (node != null && node.List == Pieces) Pieces.Remove(node); }
    }

    /// <summary>One piece of a broken prop: it lies about, then dissolves and goes.</summary>
    public sealed class BreakPiece : MonoBehaviour
    {
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private BreakSettings _s;
        private LinkedListNode<BreakPiece> _node;
        private float _born;
        private bool _fading;

        internal void Begin(BreakSettings s, LinkedListNode<BreakPiece> node) { _s = s; _node = node; _born = Time.time; }

        /// <summary>Dissolve now (and go).</summary>
        internal void Fade() { if (!_fading && isActiveAndEnabled) StartCoroutine(Dissolve()); }

        private void Update() { if (!_fading && Time.time - _born >= _s.pieceLife) Fade(); }

        private IEnumerator Dissolve()
        {
            _fading = true;
            Breakable.Forget(_node); _node = null;
            var r = GetComponent<Renderer>();
            if (_s.dissolve != null && r != null)
            {
                var copies = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < copies.Length; i++) copies[i] = Breakable.DissolveCopy(_s, r.sharedMaterials[i]);
                r.sharedMaterials = copies;
                for (float t = 0f; t < _s.dissolveSeconds; t += Time.deltaTime)
                {
                    foreach (var m in copies) m.SetFloat(DissolveId, t / _s.dissolveSeconds);
                    yield return null;
                }
                foreach (var m in copies) Destroy(m);
            }
            else
            {
                Vector3 start = transform.localScale;
                for (float t = 0f; t < 0.3f; t += Time.deltaTime) { transform.localScale = start * (1f - t / 0.3f); yield return null; }
            }
            Destroy(gameObject);
        }

        private void OnDestroy() => Breakable.Forget(_node);
    }
}
