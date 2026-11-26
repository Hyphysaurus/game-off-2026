using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Art = Artifice;

namespace ShiftJam
{
    /// <summary>
    /// A still object builds itself back in place (D-0240), in the toy-rebuild look (RebuildOnReset, D-0230): its pieces
    /// fly in from beyond their places, turning and growing, and when the last one lands the real object stands there
    /// again. Breakable uses it for a broken prop's return when the prop carries one (the backyard's Gate Panel: its slats
    /// fly in one by one, then its rails); without one, or whenever this cannot play, the prop dissolves back in as before.
    /// HOW (Artificer, his Asset Store pack): a still copy stands in for the object while it builds: its own object in the
    /// world at scale 1, one part a drawn mesh, each part at scale 1 with its scale baked into its points (Artificer's
    /// flights then read in world units, as the toy copy's do). Artificer cuts the copy at run time (RebuildData: by its
    /// parts as modelled by default; the toy's cut, RebuildOnReset.CutBy) and builds it (StartBuild, the Radial style),
    /// every number from Break Settings (Resources/Breakables), lengths in the object's size (the longest side of its
    /// box). A part whose model cannot be read in a built game (imported without Read/Write) is copied from the Fraggo
    /// pieces it broke into instead (they are made readable), when it has them. The copy is made once, at the first build,
    /// and kept (off between builds); the cut and its pieces' meshes are made at every build and thrown away at its end
    /// (a one-off allocation per return, never every frame). It cannot play, and says why once, for a skinned mesh, a
    /// mesh combined by static batching, a mesh it can neither read nor stand in for, or a cut that made no pieces.
    /// Game time: a pause holds it. Online each PC builds its own copy when its prop comes back (the host says when).
    /// Static objects only: it builds where the object stands when Play is called. Placed by Shift Jam > House > Make
    /// Gate Breakable (D-0240) on the gate panel; any breakable prop can carry one.
    /// OFF SWITCHES: Break Settings > Build Back On (every prop), or this component unticked (this prop).
    /// </summary>
    [DefaultExecutionOrder(-50)]   // before Artificer's own Update: the swap to the real object happens before it draws
    [DisallowMultipleComponent]
    public sealed class BuildBack : MonoBehaviour
    {
        private readonly List<MeshRenderer> _drawn = new List<MeshRenderer>();   // the object's own still meshes, as the scene starts
        private readonly List<Mesh> _baked = new List<Mesh>();                   // the copy's meshes (ours to throw away)
        private readonly List<Vector3> _points = new List<Vector3>(), _normals = new List<Vector3>();
        private readonly List<Vector4> _tangents = new List<Vector4>();
        private readonly List<int> _corners = new List<int>();
        private GameObject _root;          // the still copy: its own object in the world at scale 1, off between builds
        private Art.Artificer _art;
        private Action _done;
        private Vector3 _foot, _top;       // the middle of the copy's box at its foot and at its top, in the copy's own space
        private float _size;               // the longest side of that box
        private float _endBy;
        private bool _playing, _skinned;
        private string _cannot;            // why it can never build back (said once); null while it can

        /// <summary>True from the start of a build until the object stands again.</summary>
        public bool Playing => _playing;

        private void Awake()
        {
            // What it draws as the scene starts (Breakable hides these when it breaks): its still meshes.
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(false))
                if (r.enabled && !r.forceRenderingOff && r.shadowCastingMode != ShadowCastingMode.ShadowsOnly
                    && r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null) _drawn.Add(r);
            _skinned = GetComponentInChildren<SkinnedMeshRenderer>(false) != null;
        }

        /// <summary>
        /// Builds the object back where it stands now (the caller has put it in place, hidden and not solid) and calls
        /// <paramref name="done"/> in the frame the last piece has landed, before anything is drawn: the caller then shows
        /// it. True when it plays (or is playing already); false when it does not (Build Back off, this component off, or it
        /// cannot: said once), and the caller brings the object back its own way.
        /// </summary>
        public bool Play(Action done)
        {
            if (_playing) return true;
            BreakSettings s = BreakSettings.Load();
            if (s == null || !s.buildBackOn || !isActiveAndEnabled || _cannot != null) return false;
            if (_root == null)
            {
                DropCopy();   // a copy whose object went (destroyed from outside): its meshes are still ours to throw away
                if (!MakeCopy(s)) return false;
            }
            _root.transform.SetPositionAndRotation(transform.position, transform.rotation);
            _root.SetActive(true);     // Artificer's cut skips an object that is off
            _art.enabled = true;
            Configure(s);
            Drop();                                    // a cut left from before, if any
            _art.buildMode = Art.BuildMode.Finished;   // the cut then makes every piece's mesh at once (no hitch mid-flight)
            bool cut;
            try
            {
                _art.RebuildData();
                cut = _art.buildData != null && _art.buildData.meshes != null && _art.buildData.meshes.Count > 0;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                cut = false;
            }
            if (!cut)
            {
                DropCopy();   // it will not be asked again: the copy goes for good
                return Cannot("Artificer could not cut its copy into pieces");
            }
            _art.ClearDismantle();
            _art.SetDismantled();   // every piece hidden, nothing built...
            _art.StartBuild();      // ...and from the next frame they fly in
            _done = done;
            _playing = true;
            _endBy = Time.time + _art.buildTime + Mathf.Max(s.buildBackPieceSeconds.x, s.buildBackPieceSeconds.y) + 1f;
            return true;
        }

        /// <summary>Stops a build without showing the object (it broke again meanwhile): the copy goes.</summary>
        public void Stop()
        {
            if (!_playing) return;
            _playing = false;
            _done = null;
            Off();
        }

        private void Update()
        {
            if (!_playing) return;
            if (Landed() || Time.time > _endBy) Finish();   // the time is a backstop, should a piece never say it landed
        }

        /// <summary>Every piece has set off and landed. (Artificer's own Is Built waits for each part's last piece to land
        /// after all its others; when several land in one frame one can land a frame late, and it never says so.)</summary>
        private bool Landed()
            => _art != null && _art.buildData != null && _art.buildData.meshes != null
               && _art.buildLevel >= _art.buildData.meshes.Count && _art.GetBuildQueue().Count == 0;

        /// <summary>Built: the copy goes and the caller shows the real object, in the same frame, before Artificer draws.</summary>
        private void Finish()
        {
            _playing = false;
            Off();
            Action done = _done;
            _done = null;
            done?.Invoke();
        }

        /// <summary>Cut short (switched off, the scene going): the real object shows and nothing is left behind.</summary>
        private void OnDisable()
        {
            if (_playing) Finish();
        }

        private void OnDestroy()
        {
            _playing = false;
            _done = null;
            DropCopy();
        }

        /// <summary>Artificer's numbers for this build, from Break Settings (read at every build, so a change in Play shows at
        /// the next one); lengths in the object's sizes become world units (the copy stands at scale 1).</summary>
        private void Configure(BreakSettings s)
        {
            Art.Artificer a = _art;
            a.target = _root;
            RebuildOnReset.CutBy(a, s.buildBackPieces, _size);   // the toy's cut: its parts, its texture's patches or its facets
            a.useUnscaledTime = false;   // game time: a pause holds it
            a.handleParticles = a.handleLights = a.handleLines = a.handleAudio = false;
            a.boundsMode = Art.BoundsMode.Calc;
            a.seed = UnityEngine.Random.Range(1, int.MaxValue);
            a.buildOnAwake = false;
            a.startMode = Art.StartMode.AsIs;

            // The Radial style: each piece in from beyond its place, away from a point, turning and growing.
            a.buildStyle = Art.BuildStyle.Radial;
            a.buildTime = Mathf.Max(0.05f, s.buildBackSeconds);
            a.useBuildTimeCrv = false;
            a.buildTo = 0f;
            a.placeMode = Art.PlaceMode.Time;
            a.placeTimeRange = TitleBuildIn.Range(s.buildBackPieceSeconds, 0.05f);
            a.origin = _foot + s.buildBackFrom * _size;
            a.projection = s.buildBackSpread;
            a.buildDistRange = TitleBuildIn.Range(s.buildBackDistance * _size, 0f);
            a.rotateRange = TitleBuildIn.Spin(s.buildBackSpin);
            a.rotateMode = Art.RotateMode.Normal;
            bool curve = s.buildBackLanding != null && s.buildBackLanding.length >= 2;   // an emptied curve: straight in
            a.usePlaceCurve = curve; a.placeCurve = s.buildBackLanding;
            a.useRotCurve = curve; a.placeRotCurve = s.buildBackLanding;
            a.useScaleCurve = true; a.perAxisScale = false; a.maxScale = 1f;
            a.placeScaleCurve = new AnimationCurve(new Keyframe(0f, Mathf.Clamp01(s.buildBackStartScale)), new Keyframe(1f, 1f));
            a.buildFromMode = Art.BuildFromMode.None;
            a.buildFromObjects = Array.Empty<Art.BuildFrom>();

            // The order: the lowest first (a point far under its foot, so height decides and pieces level with each other
            // go from the middle out), the highest first (far over its top), or at random.
            a.useSortSpline = false;
            a.sortMode = s.buildBackOrder == BreakSettings.BuildOrder.Random ? Art.SortMode.Random : Art.SortMode.Position;
            float far = 20f * _size;
            a.sortOrigin = s.buildBackOrder == BreakSettings.BuildOrder.TopDown ? _top + Vector3.up * far : _foot - Vector3.up * far;
            a.sortDistanceMode = Art.SortDistanceMode.Closest;
            a.reverseSortOrder = false;
        }

        // ---------------------------------------------------------------- the still copy

        /// <summary>The copy (once): its own object in the world, off, carrying the Artificer; one part a drawn mesh. False
        /// (said once) when it cannot be copied.</summary>
        private bool MakeCopy(BreakSettings s)
        {
            if (_skinned) return Cannot("it has a skinned mesh (Build Back copies still meshes only)");
            if (_drawn.Count == 0) return Cannot("it draws no mesh of its own");
            foreach (MeshRenderer r in _drawn)
            {
                if (r == null || !r.TryGetComponent(out MeshFilter f) || f.sharedMesh == null) return Cannot("a part of it is gone");
                if (r.isPartOfStaticBatch) return Cannot(r.name + " is combined by static batching (untick Batching Static on it)");
                if (!f.sharedMesh.isReadable && !Readable(s.CutOf(f.sharedMesh.name)))
                    return Cannot(f.sharedMesh.name + " cannot be read in a built game (its model's Read/Write is off) and has no Fraggo pieces to stand in for it");
            }

            _root = new GameObject(name + " Build Back (D-0240)");
            _root.SetActive(false);
            Scene home = gameObject.scene;
            if (home.IsValid() && home.isLoaded && _root.scene != home) SceneManager.MoveGameObjectToScene(_root, home);
            _root.transform.SetPositionAndRotation(transform.position, transform.rotation);
            // A renderer with nothing to draw: Artificer's cut asks every object it walks, the root too, for its renderer's
            // type, and in a built game an object without one is a null there (the editor's stand-in hides that; D-0230).
            _root.AddComponent<MeshRenderer>();
            _art = _root.AddComponent<Art.Artificer>();
            _art.enabled = false;

            var box = new Bounds();
            bool any = false;
            foreach (MeshRenderer r in _drawn)
            {
                Mesh shape = r.GetComponent<MeshFilter>().sharedMesh;
                Matrix4x4 world = r.transform.localToWorldMatrix;
                if (shape.isReadable)
                {
                    AddPart(r, shape, world, r.sharedMaterials, ref box, ref any);
                    continue;
                }
                // Its model cannot be read in a built game: the Fraggo pieces it broke into stand in (ours: readable).
                CarDamageFxSettings.Cut cut = s.CutOf(shape.name);
                Material[] looks = Breakable.PieceLooks(s, cut, r.sharedMaterials);
                for (int i = 0; i < cut.pieces.Length && i < cut.centres.Length; i++)
                    if (cut.pieces[i] != null && cut.pieces[i].isReadable)
                        AddPart(r, cut.pieces[i], world * Matrix4x4.Translate(cut.centres[i]), looks, ref box, ref any);
            }
            if (!any)
            {
                DropCopy();
                return Cannot("nothing of it could be copied");
            }
            _size = Mathf.Max(0.01f, Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z)));
            _foot = new Vector3(box.center.x, box.min.y, box.center.z);
            _top = new Vector3(box.center.x, box.max.y, box.center.z);
            return true;
        }

        /// <summary>A cut with at least one piece that can be read (made by the makers: always).</summary>
        private static bool Readable(CarDamageFxSettings.Cut cut)
        {
            if (cut == null || cut.pieces == null || cut.centres == null) return false;
            foreach (Mesh m in cut.pieces) if (m != null && m.isReadable) return true;
            return false;
        }

        /// <summary>One part of the copy: <paramref name="shape"/> as <paramref name="world"/> draws it, standing at that place
        /// and turn at scale 1 with its scale baked into its points, drawn the way <paramref name="look"/> draws (its layer,
        /// shadows, probes) in <paramref name="materials"/>. Its corners widen the copy's box.</summary>
        private void AddPart(MeshRenderer look, Mesh shape, Matrix4x4 world, Material[] materials, ref Bounds box, ref bool any)
        {
            Vector3 at = world.GetPosition();
            Quaternion turn = look.transform.rotation;
            Matrix4x4 place = Matrix4x4.TRS(at, turn, Vector3.one);
            Mesh mesh = Instantiate(shape);
            mesh.name = shape.name + " (Build Back)";
            Reshape(mesh, place.inverse * world);
            _baked.Add(mesh);

            var go = new GameObject(shape.name);
            go.layer = look.gameObject.layer;
            go.transform.SetParent(_root.transform, false);
            go.transform.SetPositionAndRotation(at, turn);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var copy = go.AddComponent<MeshRenderer>();
            copy.sharedMaterials = materials;
            copy.shadowCastingMode = look.shadowCastingMode;
            copy.receiveShadows = look.receiveShadows;
            copy.renderingLayerMask = look.renderingLayerMask;
            copy.lightProbeUsage = look.lightProbeUsage;
            copy.reflectionProbeUsage = look.reflectionProbeUsage;
            copy.motionVectorGenerationMode = look.motionVectorGenerationMode;

            // Its box, in the copy root's own space (the root stands where the object does, at scale 1).
            Matrix4x4 inRoot = _root.transform.worldToLocalMatrix * place;
            Bounds b = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                Vector3 p = inRoot.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, sign));
                if (!any) { box = new Bounds(p, Vector3.zero); any = true; }
                else box.Encapsulate(p);
            }
        }

        /// <summary>Moves a mesh's points by <paramref name="by"/> (its normals and tangents turned with them). A mirroring
        /// move (a negative scale) also flips each triangle and the tangents' handedness, so the copy faces out as the
        /// object does.</summary>
        private void Reshape(Mesh mesh, Matrix4x4 by)
        {
            mesh.GetVertices(_points);
            for (int i = 0; i < _points.Count; i++) _points[i] = by.MultiplyPoint3x4(_points[i]);
            mesh.SetVertices(_points);
            Matrix4x4 turn = by.inverse.transpose;
            mesh.GetNormals(_normals);
            if (_normals.Count == _points.Count)
            {
                for (int i = 0; i < _normals.Count; i++) _normals[i] = turn.MultiplyVector(_normals[i]).normalized;
                mesh.SetNormals(_normals);
            }
            bool mirrored = by.determinant < 0f;
            mesh.GetTangents(_tangents);
            if (_tangents.Count == _points.Count)
            {
                for (int i = 0; i < _tangents.Count; i++)
                {
                    Vector4 t = _tangents[i];
                    Vector3 d = by.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                    _tangents[i] = new Vector4(d.x, d.y, d.z, mirrored ? -t.w : t.w);
                }
                mesh.SetTangents(_tangents);
            }
            if (mirrored)
            {
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                    mesh.GetTriangles(_corners, sub);
                    for (int i = 0; i + 2 < _corners.Count; i += 3) { int k = _corners[i + 1]; _corners[i + 1] = _corners[i + 2]; _corners[i + 2] = k; }
                    mesh.SetTriangles(_corners, sub, false);
                }
            }
            mesh.RecalculateBounds();
        }

        // ---------------------------------------------------------------- tidying

        /// <summary>The copy off, the Artificer stopped, and the cut and its pieces' meshes thrown away (made again at the
        /// next build).</summary>
        private void Off()
        {
            if (_art != null) _art.enabled = false;
            Drop();
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>Throws away Artificer's cut and its pieces' meshes.</summary>
        private void Drop()
        {
            if (_art == null || _art.buildData == null) return;
            Art.BuildData data = _art.buildData;
            _art.buildData = null;
            if (data.meshes != null) foreach (Art.MeshElement piece in data.meshes) if (piece.mesh != null) Destroy(piece.mesh);
            Destroy(data);
        }

        /// <summary>Throws away the whole copy: the cut, the baked meshes and the copy's object.</summary>
        private void DropCopy()
        {
            Drop();
            foreach (Mesh m in _baked) if (m != null) Destroy(m);
            _baked.Clear();
            if (_root != null) Destroy(_root);
            _root = null;
            _art = null;
        }

        /// <summary>It cannot build back: says why, once, and from then on the prop dissolves back in.</summary>
        private bool Cannot(string why)
        {
            if (_cannot == null) Debug.LogWarning($"BuildBack (D-0240): {name} cannot build itself back: {why}. It dissolves back in instead.", this);
            _cannot = why;
            return false;
        }
    }
}
