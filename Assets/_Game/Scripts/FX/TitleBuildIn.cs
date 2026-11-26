using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using Art = Artifice;

namespace ShiftJam
{
    /// <summary>
    /// The title builds itself (D-0230, the trailer's opener): on the title screen the five SHIFT letters on the menu model
    /// (MenuTitle: one small box for every pixel of his lettering) fly in cube by cube and assemble, timed to land as the
    /// title music finishes rising (CourseSounds > Music In), and then the menu's first page comes up as it always did.
    /// Any key, click or pad button skips to the built title. It plays once per game launch (a return to the title shows
    /// the title built) unless BuildFx > Title Every Time is on.
    /// HOW (Artificer, his Asset Store pack): for the build the five letters stand under one holder that carries an
    /// Artificer, so all their cubes are cut and ordered as one title (Init: the split, done here at run time, nothing
    /// saved), hidden (SetDismantled), then flown in (StartBuild) on real time (the title holds the game's at 0). The cut
    /// keys every corner by its place AND its pixel's UV, so each pixel's box is one piece. Artificer draws a piece with
    /// its letter's material only; the letter's own colours are MenuTitle's per-slot property blocks (his sheet, the frame
    /// of its animation, the tint), so every piece is given a block of ours, copied from its letter every frame. When the
    /// last cube lands (or on a skip) the Artificer, its pieces and the holder are thrown away and the letters are
    /// exactly as MenuTitle made them: darts, pulls, paint and the shift work on them as before.
    /// On the menu model, beside it, for the build only; TitleMenu asks for it (Begin) and is told when it is built.
    /// OFF SWITCH: BuildFx > Title On.
    /// </summary>
    [DefaultExecutionOrder(-100)]   // before Artificer's own Update: a finish stops it before it draws that frame
    public sealed class TitleBuildIn : MonoBehaviour
    {
        /// <summary>One letter of the title while it builds: its object, its look, its plaything on the model, and the
        /// property blocks its pieces are drawn with (one a material slot).</summary>
        private sealed class Letter
        {
            public Transform Body;
            public MeshRenderer Look;
            public MenuModel.Control Toy;
            public MaterialPropertyBlock[] Blocks;
        }

        private static bool _played;   // the title has built itself once this launch

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetLaunch() => _played = false;

        private readonly List<Letter> _letters = new List<Letter>();
        private MenuModel _model;
        private Action _built;
        private GameObject _holder;
        private Art.Artificer _art;
        private float _startAt, _endBy;
        private int _finishOnFrame = -1;
        private bool _building, _done;

        /// <summary>
        /// Starts the title building itself on <paramref name="model"/>, whose letters MenuTitle has made, and calls
        /// <paramref name="built"/> once it stands (or is skipped). Null when it does not play: off in BuildFx, played once
        /// already this launch, or nothing to build (no letters, or their shapes are not readable); the caller then shows
        /// its menu at once. Its first frames are empty: the caller keeps its own menu hidden meanwhile.
        /// </summary>
        public static TitleBuildIn Begin(MenuModel model, Action built)
        {
            BuildFxSettings fx = BuildFxSettings.Load();
            if (model == null || built == null || !fx.titleOn || (_played && !fx.titleEveryTime)) return null;
            var maker = model.gameObject.AddComponent<TitleBuildIn>();
            if (!maker.Prepare(model, fx, built))
            {
                Destroy(maker);
                return null;
            }
            _played = true;
            return maker;
        }

        /// <summary>Gathers the letters under the holder, cuts them into their cubes and hides them. False (with everything
        /// put back) when there is nothing to build.</summary>
        private bool Prepare(MenuModel model, BuildFxSettings fx, Action built)
        {
            _model = model;
            _built = built;
            // The letters: the model's playthings whose object is a child of the model with a readable shape (MenuTitle's).
            for (int i = 0; i < model.transform.childCount; i++)
            {
                Transform t = model.transform.GetChild(i);
                if (!model.TryGet(t.name, out MenuModel.Control toy) || !toy.Toy || toy.Body != t) continue;
                if (!t.TryGetComponent(out MeshRenderer look) || !t.TryGetComponent(out MeshFilter shape) || shape.sharedMesh == null || !shape.sharedMesh.isReadable) continue;
                var blocks = new MaterialPropertyBlock[Mathf.Max(1, look.sharedMaterials.Length)];
                for (int s = 0; s < blocks.Length; s++) blocks[s] = new MaterialPropertyBlock();
                _letters.Add(new Letter { Body = t, Look = look, Toy = toy, Blocks = blocks });
            }
            if (_letters.Count == 0) return false;

            // The title's middle and the menu's own directions (its plane: across, up, toward the viewer), in the world.
            Bounds all = _letters[0].Look.bounds;
            foreach (Letter l in _letters) all.Encapsulate(l.Look.bounds);
            Vector3 middle = all.center, up = model.Up, toward = model.Toward, across = Vector3.Cross(up, -toward);
            float metre = model.Scale;
            Vector3 World(Vector2 at, float towardViewer) => middle + (across * at.x + up * at.y + toward * towardViewer) * metre;

            // One holder for the five letters, standing exactly where the model does (so a letter's own place and size
            // under it are what they were under the model): one Artificer cuts and orders the whole title.
            _holder = new GameObject("TitleBuild (D-0230)");
            _holder.layer = model.gameObject.layer;
            _holder.transform.SetParent(model.transform, false);
            // A renderer with nothing to draw: Artificer's cut asks every object it walks, the holder too, for its renderer's
            // type, and in a built game an object without one is a null there (the editor's stand-in hides that).
            _holder.AddComponent<MeshRenderer>();
            foreach (Letter l in _letters) l.Body.SetParent(_holder.transform, true);
            Transform holder = _holder.transform;
            Vector3 middleHere = holder.InverseTransformPoint(middle);

            float span = fx.TitleSpan;
            float pieceMost = Mathf.Max(0.05f, Mathf.Max(fx.titlePieceSeconds.x, fx.titlePieceSeconds.y));
            float delay = Mathf.Max(0f, fx.titleDelay);
            float setOff = Mathf.Max(0.2f, span - delay - pieceMost);   // the last cube sets off in time to land as the music is up

            _art = _holder.AddComponent<Art.Artificer>();
            Art.Artificer a = _art;
            a.target = _holder;
            CutByPixel(a);
            a.useUnscaledTime = true;
            a.handleParticles = a.handleLights = a.handleLines = a.handleAudio = false;
            a.boundsMode = Art.BoundsMode.Calc;
            a.seed = UnityEngine.Random.Range(1, int.MaxValue);
            a.buildOnAwake = false;
            a.startMode = Art.StartMode.AsIs;
            a.buildMode = Art.BuildMode.Finished;      // Init then makes every piece's mesh at once (no hitch mid-flight)
            a.buildTime = setOff;
            a.useBuildTimeCrv = false;
            a.placeMode = Art.PlaceMode.Time;
            a.placeTimeRange = Range(fx.titlePieceSeconds, 0.05f);
            a.usePlaceCurve = true; a.placeCurve = fx.titleLanding;
            a.useRotCurve = true; a.placeRotCurve = fx.titleLanding;
            a.useScaleCurve = true; a.perAxisScale = false; a.maxScale = 1f;
            a.placeScaleCurve = new AnimationCurve(new Keyframe(0f, Mathf.Clamp01(fx.titleStartScale)), new Keyframe(1f, 1f));
            a.rotateRange = Spin(fx.titleSpin);
            a.rotateMode = Art.RotateMode.Normal;
            a.sortDistanceMode = Art.SortDistanceMode.Closest;
            a.reverseSortOrder = false;

            // The order the cubes set off in.
            a.useSortSpline = false;
            if (fx.titleOrder == BuildFxSettings.Order.Random) a.sortMode = Art.SortMode.Random;
            else if (fx.titleOrder == BuildFxSettings.Order.AlongPath && fx.titleOrderPath != null && fx.titleOrderPath.Length >= 2)
            {
                var line = new GameObject("BuildOrder");
                line.layer = _holder.layer;
                line.transform.SetParent(holder, false);
                var path = line.AddComponent<SplineContainer>();
                path.Spline.Clear();
                foreach (Vector2 p in fx.titleOrderPath) path.Spline.Add(new BezierKnot((float3)holder.InverseTransformPoint(World(p, 0f))), TangentMode.Linear);
                a.exclude.Add(line);     // a helper with no renderer: Artificer's cut must not walk into it
                a.sortMode = Art.SortMode.Position;
                a.useSortSpline = true;
                a.sortSpline = path;
            }
            else
            {
                a.sortMode = Art.SortMode.Position;
                a.sortOrigin = holder.InverseTransformPoint(World(fx.titleOrderPoint, 0f));
            }

            // Where they come from.
            if (fx.titleFlight == BuildFxSettings.TitleFlight.FromPoint)
            {
                var from = new GameObject("BuildFrom");
                from.layer = _holder.layer;
                from.transform.SetParent(holder, false);
                // Turned square with the world: Artificer turns the curve's first direction by this object's turn and then
                // uses it in the holder's own space, so the direction below is given in the holder's space already.
                from.transform.SetPositionAndRotation(World(fx.titleFlightPoint, fx.titleFlightToward), Quaternion.identity);
                a.exclude.Add(from);
                Vector3 fromHere = holder.InverseTransformPoint(from.transform.position);
                Vector3 head = middleHere - fromHere;
                Vector3 first = head.sqrMagnitude > 1e-10f ? head.normalized : holder.InverseTransformDirection(up).normalized;
                a.buildStyle = Art.BuildStyle.Transform;
                a.buildFromMode = Art.BuildFromMode.Nearest;
                a.buildFromObjects = new[]
                {
                    new Art.BuildFrom
                    {
                        buildFrom = from.transform, buildFromBox = Vector3.one * fx.titleSpread, buildFromOffset = Vector3.zero,
                        startDir = first, startTension = fx.titleCurveStart, endTension = fx.titleCurveEnd,
                        splineDirCalc = Art.SplineDir.Origin, splineEndProject = Vector3.one, splineMode = Art.SplineMode.BuildFrom,
                        initialized = true,
                    },
                };
                a.origin = fromHere;     // a curve's end swings in from beyond its cube, away from this point
            }
            else
            {
                a.buildStyle = Art.BuildStyle.Radial;
                a.buildFromMode = Art.BuildFromMode.None;
                a.buildFromObjects = new Art.BuildFrom[0];
                a.origin = middleHere;
                a.projection = Vector3.one;
                a.buildDistRange = Range(fx.titleAround, 0f);
            }

            // The cut (all here, at run time) and every piece's mesh; then the pieces take their letter's colours, and
            // the title is hidden until the first cube sets off.
            try { a.Init(); }
            catch (Exception e)
            {
                Debug.LogException(e, model);
                Debug.LogWarning("TitleBuildIn (D-0230): Artificer could not cut the title; it shows at once.", model);
                Abandon();
                return false;
            }
            if (a.buildData == null || a.buildData.meshes == null || a.buildData.meshes.Count == 0)
            {
                Debug.LogWarning("TitleBuildIn (D-0230): Artificer cut the title into no pieces; the title shows at once.", model);
                Abandon();
                return false;
            }
            foreach (Art.MeshElement piece in a.buildData.meshes)
            {
                Letter l = LetterOf(piece.renderer);
                if (l == null || piece.rp == null || piece.draw == null) continue;
                for (int m = 0; m < piece.rp.Length && m < piece.draw.Count; m++)
                {
                    int slot = piece.draw[m];
                    if (slot >= 0 && slot < l.Blocks.Length) piece.rp[m].matProps = l.Blocks[slot];
                }
            }
            CopyLooks();
            a.ClearDismantle();
            a.SetDismantled();

            _startAt = Time.unscaledTime + delay;
            _endBy = _startAt + setOff + pieceMost + 1.5f;   // a backstop: a piece that never reports landing does not hold the menu
            return true;
        }

        /// <summary>The cut that makes one piece of every pixel's box: corners are joined by their place and their pixel's UV
        /// (neighbouring boxes share corner places, never a UV). Also used by the maker to count the pieces.</summary>
        public static void CutByPixel(Art.Artificer a)
        {
            a.splitMode = Art.SplitMode.Elements;
            a.usePosition = true; a.positionTolerance = 1e-5f;
            a.useUV = true; a.uvTolerance = 1e-6f;
            a.useUV2 = false; a.useNormal = false; a.useColor = false; a.useMaterial = false;
            a.meshPivot = Art.MeshPivotMode.Center;    // a cube turns about its own middle
            a.useSplitParams = false; a.dontSplit = false; a.dontAdd = false; a.addAll = false;
        }

        /// <summary>A random time or distance between the two numbers (either order), at least <paramref name="least"/>.</summary>
        public static Art.FloatRange Range(Vector2 between, float least)
        {
            float low = Mathf.Max(least, Mathf.Min(between.x, between.y)), high = Mathf.Max(low, Mathf.Max(between.x, between.y));
            return new Art.FloatRange { mode = high > low ? Art.FloatRange.Mode.RandomBetweenTwo : Art.FloatRange.Mode.Constant, min = low, max = high };
        }

        /// <summary>A random turn up to <paramref name="most"/> degrees either way about each axis.</summary>
        public static Art.Vector3Range Spin(Vector3 most)
        {
            Vector3 m = new Vector3(Mathf.Abs(most.x), Mathf.Abs(most.y), Mathf.Abs(most.z));
            return new Art.Vector3Range { mode = Art.Vector3Range.Mode.RandomBetweenTwo, min = -m, max = m };
        }

        private Letter LetterOf(Renderer r)
        {
            foreach (Letter l in _letters) if (l.Look == r) return l;
            return null;
        }

        private void Update()
        {
            if (_done) return;
            if (_finishOnFrame >= 0)
            {
                if (Time.frameCount >= _finishOnFrame) Finish();
                return;
            }
            // A skip: the title stands at once, a frame later, so the press that skipped is not also a choice on the menu.
            if (AnyPress())
            {
                _finishOnFrame = Time.frameCount + 1;
                return;
            }
            float now = Time.unscaledTime;
            if (!_building)
            {
                if (now >= _startAt) { _art.StartBuild(); _building = true; }
                return;
            }
            if (Landed() || now > _endBy) Finish();
        }

        /// <summary>Every cube has set off and landed. (Artificer's own Is Built waits for each letter's last cube to land
        /// after all its others; with hundreds landing in one frame one can land a frame late, and it never says so.)</summary>
        private bool Landed()
            => _art != null && _art.buildData != null && _art.buildLevel >= _art.buildData.meshes.Count && _art.GetBuildQueue().Count == 0;

        /// <summary>The pieces wear their letter's colours as they are now (MenuTitle steps the sheet's frames and flashes them).</summary>
        private void LateUpdate()
        {
            if (!_done) CopyLooks();
        }

        private void CopyLooks()
        {
            foreach (Letter l in _letters)
            {
                if (l.Look == null) continue;
                for (int s = 0; s < l.Blocks.Length; s++) l.Look.GetPropertyBlock(l.Blocks[s], s);
            }
        }

        /// <summary>A key, a mouse button or a pad button pressed this frame.</summary>
        private static bool AnyPress()
        {
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.anyKey.wasPressedThisFrame) return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)) return true;
            Gamepad pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame
                || pad.buttonNorth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame
                || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame);
        }

        /// <summary>The title stands: the letters go back under the model, whole and shown, the build's things are thrown
        /// away, and the owner is told (its first page comes up).</summary>
        private void Finish()
        {
            if (_done) return;
            _done = true;
            if (_art != null) _art.enabled = false;   // no piece is drawn again
            PutLettersBack();
            // Shown already: the first page leaves them standing where they built, instead of dropping them in again.
            foreach (Letter l in _letters) if (l.Toy != null) l.Toy.Shown = true;
            Cleanup();
            Action built = _built;
            _built = null;
            built?.Invoke();
            Destroy(this);
        }

        /// <summary>Nothing to build after all: everything back as it was.</summary>
        private void Abandon()
        {
            _done = true;
            PutLettersBack();
            Cleanup();
        }

        private void PutLettersBack()
        {
            foreach (Letter l in _letters)
            {
                if (l.Body != null && _model != null) l.Body.SetParent(_model.transform, true);
                if (l.Look != null) l.Look.enabled = true;
            }
        }

        /// <summary>Throws away the Artificer, its pieces' meshes and cut, the marks it put on the letters, and the holder.</summary>
        private void Cleanup()
        {
            if (_art != null)
            {
                Art.BuildData data = _art.buildData;
                if (data != null)
                {
                    if (data.meshes != null) foreach (Art.MeshElement piece in data.meshes) if (piece.mesh != null) Destroy(piece.mesh);
                    Destroy(data);
                }
                _art.buildData = null;
                Destroy(_art);
                _art = null;
            }
            foreach (Letter l in _letters) if (l.Body != null && l.Body.TryGetComponent(out Art.ObjectID mark)) Destroy(mark);
            if (_holder != null) Destroy(_holder);
            _holder = null;
        }

        /// <summary>Gone before it finished (the title closed, the scene changed): its things go with it.</summary>
        private void OnDestroy()
        {
            if (_done) return;
            _done = true;
            Cleanup();
        }
    }
}
