using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Art = Artifice;

namespace ShiftJam
{
    /// <summary>
    /// The toy rebuilds itself after a fall (D-0230), instead of the black cut: at a reset the body comes apart into pieces
    /// where it fell (they burst, tumble, bounce off the floor and shrink away), the camera jumps to the checkpoint under
    /// the shift's warp, and there the pieces fly in from round it and the body stands whole again before control returns.
    /// The kid too (the same body, bigger; BuildFx > Rebuild Kid). Respawn asks for it (Play) at every reset; when it is
    /// off, or cannot (no body drawn to copy, the motor parked in a car seat or first person, a knocked-out body lying
    /// down), Respawn keeps its black cut.
    /// HOW (Artificer, his Asset Store pack): the body is skinned (8 SkinnedMeshRenderers), so at the reset its pose is
    /// baked into a still copy (one plain mesh a part, its scale baked into the points so the copy's objects all stand at
    /// scale 1, which Artificer's burst needs), the real body is hidden, and one Artificer on the copy cuts it into pieces
    /// (RebuildData, at run time: by its parts, its texture's patches or its facets), bursts them (StartDismantle, the
    /// Explode style: speed, spin, gravity, a bounce off what is under them, a shrink at the end), then, once the copy has
    /// moved to the checkpoint with the player, flies them back in (StartBuild, the Radial style). At the end the copy is
    /// switched off and the real body shown. The copy is made once and reused (its meshes baked again at each reset);
    /// the cut and its pieces' meshes are made again at each reset and the old ones thrown away (a one-off allocation at a
    /// reset, never every frame). Nothing is ever left behind: a reset cut short (the player switched off, the scene
    /// gone) puts the player at the checkpoint, shows the body and gives control back.
    /// Online: only this PC sees it (a party's other players see the player put back, as before); a party's puppets run
    /// no Respawn, so never this. It runs on game time: a pause holds it where it is.
    /// Added to the Player by Respawn at the start (nothing to place). OFF SWITCH: BuildFx > Rebuild On.
    /// </summary>
    [DefaultExecutionOrder(-50)]   // before Artificer's own Update: the swap back to the real body happens before it draws
    public sealed class RebuildOnReset : MonoBehaviour
    {
        private enum Phase { Idle, Apart, Moving, Together }

        /// <summary>One part of the still copy: its object, its shape and look, and the mesh the pose is baked into.</summary>
        private sealed class Piece
        {
            public GameObject Go;
            public MeshRenderer Look;
            public Mesh Mesh;
        }

        private BuildFxSettings _fx;
        private PlayerMotor _motor;
        private PlayerInput _input;
        private Shifter _shifter;
        private CharacterController _capsule;
        private ShiftWarp _warp;
        private PlayerMoves _moves;
        private GameObject _root;          // the still copy: its own object in the world, never under the player
        private Art.Artificer _art;
        private Transform _body;
        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly List<SkinnedMeshRenderer> _skins = new List<SkinnedMeshRenderer>();
        private readonly List<SkinnedMeshRenderer> _found = new List<SkinnedMeshRenderer>();
        private readonly List<Renderer> _drawn = new List<Renderer>();
        private readonly List<Renderer> _hidden = new List<Renderer>();     // the body's renderers this switched off
        private readonly List<Vector3> _points = new List<Vector3>(), _normals = new List<Vector3>();
        private readonly List<Vector4> _tangents = new List<Vector4>();
        private Action _warpPlayer, _moveNow;
        private Phase _phase;
        private float _phaseAt, _apartDoneAt, _apartCap, _togetherCap;
        private int _hideOnFrame;
        private bool _bodyHidden, _warped, _motorWasOn, _inputWasOn;

        /// <summary>True from the reset until control is back (Respawn is Busy meanwhile: one reset at a time).</summary>
        public bool Playing => _phase != Phase.Idle;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            TryGetComponent(out _input);
            TryGetComponent(out _shifter);
            TryGetComponent(out _capsule);
            TryGetComponent(out _moves);
            if (!TryGetComponent(out _warp)) _warp = GetComponentInChildren<ShiftWarp>();
            _moveNow = MoveNow;
        }

        /// <summary>
        /// Plays the rebuild for a reset: the body comes apart here, <paramref name="warpPlayer"/> is called to put the player
        /// at the checkpoint, the body builds itself there, control comes back. True when it plays (or is playing already);
        /// false when it does not, and the caller does its black cut instead.
        /// </summary>
        public bool Play(Action warpPlayer)
        {
            if (_phase != Phase.Idle) return true;
            _fx = BuildFxSettings.Load();
            if (!_fx.rebuildOn || !isActiveAndEnabled || warpPlayer == null) return false;
            if (_motor == null || !_motor.enabled) return false;    // parked: a car seat or first person hides the body its own way
            // Knocked out (online, PlayerHurt): the body lies down and stands up only when it is put back; a copy of it lying
            // would build itself lying at the spawn. That way back keeps its black cut.
            if (_moves != null && _moves.Current == PlayerMoves.Move.Dead) return false;
            if (_shifter != null && _shifter.Size == ShiftSize.Kid && !_fx.rebuildKid) return false;
            _body = Body();
            if (_body == null || !GatherSkins()) return false;
            float height = Height();
            if (_root == null) MakeRoot();
            if (!Copy()) { _root.SetActive(false); return false; }
            Configure(height);

            // The cut, made again for this pose; the last reset's pieces go.
            Art.BuildData old = _art.buildData;
            _art.buildMode = Art.BuildMode.Finished;     // Init then makes every piece's mesh, standing in place
            _art.enabled = true;
            bool cut;
            try
            {
                _art.RebuildData();
                cut = _art.buildData != null && _art.buildData.meshes != null && _art.buildData.meshes.Count > 0;
                if (cut) _art.StartDismantle();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                cut = false;
            }
            Forget(old);
            if (!cut)
            {
                Debug.LogWarning("RebuildOnReset (D-0230): Artificer could not cut the body into pieces; the black cut instead.", this);
                _art.enabled = false;
                _root.SetActive(false);
                return false;
            }

            // Hold the player still where it fell (no gravity, no keys) until the move.
            _motorWasOn = _motor.enabled; _motor.enabled = false;
            _inputWasOn = _input != null && _input.enabled;
            if (_input != null) _input.enabled = false;
            _warpPlayer = warpPlayer;
            _warped = false;
            _bodyHidden = false;
            _hideOnFrame = Time.frameCount + 1;   // the real body goes a frame later, when the copy is surely drawn: no empty frame
            _apartCap = _art.dismantleTime + Mathf.Max(_fx.apartPieceSeconds.x, _fx.apartPieceSeconds.y) + 1f;
            _apartDoneAt = -1f;
            _phase = Phase.Apart;
            _phaseAt = Time.time;
            return true;
        }

        /// <summary>The body in use: PlayerBody's Animator, else the first Animator under the player.</summary>
        private Transform Body()
        {
            if (TryGetComponent(out PlayerBody body) && body.Animator != null) return body.Animator.transform;
            Animator any = GetComponentInChildren<Animator>();
            return any != null ? any.transform : null;
        }

        /// <summary>The body's skinned parts that are drawn now (not off, not shadows only). False with none.</summary>
        private bool GatherSkins()
        {
            _skins.Clear();
            _body.GetComponentsInChildren(false, _found);
            foreach (SkinnedMeshRenderer s in _found)
                if (s.enabled && s.sharedMesh != null && !s.forceRenderingOff && s.shadowCastingMode != ShadowCastingMode.ShadowsOnly) _skins.Add(s);
            return _skins.Count > 0;
        }

        /// <summary>The body's height in the world: the capsule's at the player's size, else the drawn parts' box.</summary>
        private float Height()
        {
            if (_capsule != null && _capsule.height > 0f) return Mathf.Max(0.01f, _capsule.height * Mathf.Abs(transform.lossyScale.y));
            Bounds box = _skins[0].bounds;
            foreach (SkinnedMeshRenderer s in _skins) box.Encapsulate(s.bounds);
            return Mathf.Max(0.01f, box.size.y);
        }

        /// <summary>The copy's own object (once): in the world at scale 1, with the Artificer, off until a reset.</summary>
        private void MakeRoot()
        {
            DropPieces();   // a copy whose scene went took its objects with it; their meshes are still ours to throw away
            _root = new GameObject("Toy Rebuild (D-0230)");
            _root.SetActive(false);
            // A renderer with nothing to draw: Artificer's cut asks every object it walks, the root too, for its renderer's
            // type, and in a built game an object without one is a null there (the editor's stand-in hides that).
            _root.AddComponent<MeshRenderer>();
            _art = _root.AddComponent<Art.Artificer>();
            _art.enabled = false;
        }

        /// <summary>Bakes the body's pose into the copy, part by part, standing exactly where the body stands. Every part's
        /// object is at scale 1 under a root at scale 1 (Artificer's burst ignores scales above the piece), so the parts'
        /// own scale (the body's fit, the shift's size) goes into the points.</summary>
        private bool Copy()
        {
            _root.SetActive(true);   // Artificer's cut skips an object that is off
            _root.transform.SetPositionAndRotation(transform.position, transform.rotation);
            _root.transform.localScale = Vector3.one;
            for (int i = 0; i < _skins.Count; i++)
            {
                if (i >= _pieces.Count) _pieces.Add(NewPiece(i));
                Piece p = _pieces[i];
                SkinnedMeshRenderer skin = _skins[i];
                Transform t = skin.transform;
                skin.BakeMesh(p.Mesh, true);    // the pose as it stands, in the part's own space without its scale
                Reshape(p.Mesh, Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse * t.localToWorldMatrix);
                p.Go.layer = skin.gameObject.layer;
                p.Go.transform.SetPositionAndRotation(t.position, t.rotation);
                p.Go.transform.localScale = Vector3.one;
                p.Look.sharedMaterials = skin.sharedMaterials;
                p.Look.shadowCastingMode = skin.shadowCastingMode;
                p.Look.receiveShadows = skin.receiveShadows;
                p.Look.enabled = true;
                p.Go.SetActive(true);
            }
            for (int i = _skins.Count; i < _pieces.Count; i++) _pieces[i].Go.SetActive(false);
            return _skins.Count > 0;
        }

        private Piece NewPiece(int index)
        {
            var go = new GameObject("Part " + (index + 1));
            go.transform.SetParent(_root.transform, false);
            var mesh = new Mesh { name = "Toy Rebuild Part " + (index + 1) };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            return new Piece { Go = go, Look = go.AddComponent<MeshRenderer>(), Mesh = mesh };
        }

        /// <summary>Moves a baked mesh's points (and turns its normals and tangents) by <paramref name="by"/>.</summary>
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
            mesh.GetTangents(_tangents);
            if (_tangents.Count == _points.Count)
            {
                for (int i = 0; i < _tangents.Count; i++)
                {
                    Vector4 t = _tangents[i];
                    Vector3 d = by.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                    _tangents[i] = new Vector4(d.x, d.y, d.z, t.w);
                }
                mesh.SetTangents(_tangents);
            }
            mesh.RecalculateBounds();
        }

        /// <summary>Artificer's numbers for this reset, from BuildFx; lengths in body heights become world units.</summary>
        private void Configure(float height)
        {
            BuildFxSettings fx = _fx;
            Art.Artificer a = _art;
            a.target = _root;
            CutBy(a, fx.rebuildPieces, height);
            a.useUnscaledTime = false;   // game time: a pause holds it
            a.handleParticles = a.handleLights = a.handleLines = a.handleAudio = false;
            a.boundsMode = Art.BoundsMode.Calc;
            a.seed = UnityEngine.Random.Range(1, int.MaxValue);
            a.buildOnAwake = false;
            a.startMode = Art.StartMode.AsIs;

            // Coming apart: the Explode style (each piece a speed away from a point, a spin, gravity, a bounce).
            a.dismantleStyle = Art.DismantleStyle.Explode;
            a.dismantleTime = Mathf.Max(0.01f, fx.apartSeconds);
            a.dismantleTo = 0f;
            a.removeTimeRange = TitleBuildIn.Range(fx.apartPieceSeconds, 0.05f);
            a.minExplodeForce = Mathf.Min(fx.apartSpeed.x, fx.apartSpeed.y) * height;
            a.maxExplodeForce = Mathf.Max(fx.apartSpeed.x, fx.apartSpeed.y) * height;
            a.explodeOrigin = fx.apartFrom * height;
            a.dismantleProjection = Vector3.one;
            a.angVelRange = new Vector3(Mathf.Abs(fx.apartSpin.x), Mathf.Abs(fx.apartSpin.y), Mathf.Abs(fx.apartSpin.z));
            float gravity = Physics.gravity.magnitude;
            a.gravityModifier = gravity > 0.01f ? fx.apartGravity * height / gravity : 0f;
            a.forceRange = new Art.Vector3Range(Vector3.zero);
            a.collisionMode = Art.CollisionMode.Raycast;
            a.layers = BounceMask();
            a.collisionY = 0f;
            a.bounce = Mathf.Clamp01(fx.apartBounce);
            a.linearDrag = 0.1f;
            a.angularDrag = 0.6f;
            a.dismantleRotate = Vector3.zero;
            a.useDisPlaceCurve = false; a.useDisPlaceRotCurve = false;
            a.useDisPlaceScaleCurve = true;
            float shrink = Mathf.Clamp(fx.apartShrinkFrom, 0.01f, 0.99f);
            a.disPlaceScaleCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(shrink, 1f), new Keyframe(1f, 0f));

            // Building again: the Radial style (each piece in from beyond its place, away from a point, turning and growing).
            a.buildStyle = Art.BuildStyle.Radial;
            a.buildTime = Mathf.Max(0.05f, fx.gatherSeconds);
            a.useBuildTimeCrv = false;
            a.buildTo = 0f;
            a.placeMode = Art.PlaceMode.Time;
            a.placeTimeRange = TitleBuildIn.Range(fx.gatherPieceSeconds, 0.05f);
            a.origin = fx.gatherFrom * height;
            a.projection = fx.gatherSpread;
            a.buildDistRange = TitleBuildIn.Range(fx.gatherDistance * height, 0f);
            a.rotateRange = TitleBuildIn.Spin(fx.gatherSpin);
            a.rotateMode = Art.RotateMode.Normal;
            a.usePlaceCurve = true; a.placeCurve = fx.gatherLanding;
            a.useRotCurve = true; a.placeRotCurve = fx.gatherLanding;
            a.useScaleCurve = true; a.perAxisScale = false; a.maxScale = 1f;
            a.placeScaleCurve = new AnimationCurve(new Keyframe(0f, Mathf.Clamp01(fx.gatherStartScale)), new Keyframe(1f, 1f));
            a.buildFromMode = Art.BuildFromMode.None;
            a.buildFromObjects = Array.Empty<Art.BuildFrom>();
            a.useSortSpline = false;
            a.sortMode = fx.gatherOrder == BuildFxSettings.ToyOrder.Random ? Art.SortMode.Random : Art.SortMode.Position;
            a.sortOrigin = fx.gatherOrder == BuildFxSettings.ToyOrder.HeadDown ? new Vector3(0f, height, 0f) : Vector3.zero;
            a.sortDistanceMode = Art.SortDistanceMode.Closest;
            a.reverseSortOrder = false;
            _togetherCap = a.buildTime + Mathf.Max(fx.gatherPieceSeconds.x, fx.gatherPieceSeconds.y) + 1f;
        }

        /// <summary>The cut of the body: its parts as modelled (corners joined by place); each patch of its texture (by place
        /// and UV); or each facet (by place, UV and normal). The place tolerance follows the body's height. Also used by the
        /// maker to count the pieces.</summary>
        public static void CutBy(Art.Artificer a, BuildFxSettings.Pieces pieces, float height)
        {
            a.splitMode = Art.SplitMode.Elements;
            a.usePosition = true; a.positionTolerance = Mathf.Max(1e-6f, 1e-4f * height);
            a.useUV = pieces != BuildFxSettings.Pieces.Parts; a.uvTolerance = 1e-4f;
            a.useNormal = pieces == BuildFxSettings.Pieces.Facets; a.normalTolerance = 1e-3f;
            a.useUV2 = false; a.useColor = false; a.useMaterial = false;
            a.meshPivot = Art.MeshPivotMode.Center;    // a piece turns about its own middle
            a.useSplitParams = false; a.dontSplit = false; a.dontAdd = false; a.addAll = false;
        }

        /// <summary>What the pieces bounce off: BuildFx's layers without the player's own (its capsule stands where it fell).</summary>
        private int BounceMask()
        {
            int mask = _fx.apartBounceOn.value & ~(1 << gameObject.layer);
            if (_capsule != null) mask &= ~(1 << _capsule.gameObject.layer);
            foreach (SkinnedMeshRenderer s in _skins) mask &= ~(1 << s.gameObject.layer);
            return mask;
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Apart:
                    if (!_bodyHidden && Time.frameCount >= _hideOnFrame) HideBody();
                    if (_apartDoneAt < 0f && (_art.IsDismantled() || Time.time - _phaseAt > _apartCap)) _apartDoneAt = Time.time;
                    if (_apartDoneAt >= 0f && Time.time - _apartDoneAt >= _fx.apartHold) Move();
                    break;
                case Phase.Moving:
                    // The dip never reached black (another cut took the screen): go on without it.
                    if (Time.time - _phaseAt > _fx.moveSeconds + 1f) MoveNow();
                    break;
                case Phase.Together:
                    if (Landed() || Time.time - _phaseAt > _togetherCap) Finish();
                    break;
            }
        }

        /// <summary>The jump to the checkpoint, under its cover: the shift's warp, a quick black dip, or a plain cut.</summary>
        private void Move()
        {
            if (!_bodyHidden) HideBody();
            float seconds = Mathf.Max(0f, _fx.moveSeconds);
            if (_fx.moveCover == BuildFxSettings.Cover.BlackDip && seconds > 0f)
            {
                ScreenFade fade = ScreenFade.Find();
                if (fade != null)
                {
                    _phase = Phase.Moving;
                    _phaseAt = Time.time;
                    fade.Cut(seconds * 0.5f, _moveNow, seconds * 0.5f);
                    return;
                }
            }
            if (_fx.moveCover == BuildFxSettings.Cover.ShiftWarp && seconds > 0f && _warp != null) _warp.Play(true, seconds);
            MoveNow();
        }

        /// <summary>The player is put at the checkpoint, the copy with it, and its pieces start flying in.</summary>
        private void MoveNow()
        {
            if (_phase != Phase.Apart && _phase != Phase.Moving) return;
            WarpPlayer();
            _root.transform.SetPositionAndRotation(transform.position, transform.rotation);
            _art.StartBuild();
            _phase = Phase.Together;
            _phaseAt = Time.time;
        }

        /// <summary>Every piece has set off and landed. (Artificer's own Is Built waits for each part's last piece to land
        /// after all its others; when several land in one frame one can land a frame late, and it never says so.)</summary>
        private bool Landed()
            => _art.buildData != null && _art.buildLevel >= _art.buildData.meshes.Count && _art.GetBuildQueue().Count == 0;

        /// <summary>Whole again: the copy goes, the real body shows, control comes back as it was.</summary>
        private void Finish()
        {
            _phase = Phase.Idle;
            if (_art != null) _art.enabled = false;
            if (_root != null) _root.SetActive(false);
            ShowBody();
            if (_motor != null) _motor.enabled = _motorWasOn;
            if (_input != null) _input.enabled = _inputWasOn;
            _warpPlayer = null;
        }

        private void WarpPlayer()
        {
            if (_warped) return;
            _warped = true;
            _warpPlayer?.Invoke();
        }

        /// <summary>The real body's drawn parts (its skin and what it holds) off while the copy stands in for it.</summary>
        private void HideBody()
        {
            _bodyHidden = true;
            _hidden.Clear();
            if (_body == null) return;
            _body.GetComponentsInChildren(false, _drawn);
            foreach (Renderer r in _drawn)
            {
                if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
                if (!r.enabled) continue;
                r.enabled = false;
                _hidden.Add(r);
            }
        }

        private void ShowBody()
        {
            foreach (Renderer r in _hidden) if (r != null) r.enabled = true;
            _hidden.Clear();
            _bodyHidden = false;
        }

        /// <summary>A reset cut short (the player switched off, the scene going): the player is put back, whole, and free.</summary>
        private void OnDisable()
        {
            if (_phase == Phase.Idle) return;
            WarpPlayer();
            Finish();
        }

        private void OnDestroy()
        {
            if (_art != null)
            {
                Forget(_art.buildData);
                _art.buildData = null;
            }
            DropPieces();
            if (_root != null) Destroy(_root);
            _root = null;
        }

        /// <summary>Throws away a cut and its pieces' meshes.</summary>
        private static void Forget(Art.BuildData data)
        {
            if (data == null) return;
            if (data.meshes != null) foreach (Art.MeshElement piece in data.meshes) if (piece.mesh != null) Destroy(piece.mesh);
            Destroy(data);
        }

        /// <summary>Throws away the copy's baked meshes (its objects go with its root).</summary>
        private void DropPieces()
        {
            foreach (Piece p in _pieces) if (p.Mesh != null) Destroy(p.Mesh);
            _pieces.Clear();
        }
    }
}
