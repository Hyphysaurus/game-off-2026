#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Films a footage take in play mode (D-0242, ported from Nectar Rush's rig; the shot list is a FootageClip,
    /// docs/footage/&lt;name&gt;.json). FootageRecorder arms it and enters play; it is made as the scene loads. It takes the title
    /// down the way going online does (OnlineWorld), lets the world settle, then films shot after shot through a camera of
    /// its own that looks like the game's camera (URP, its post-processing on, never the HUD), at a steady rate: the game
    /// steps one frame's worth of time per frame however long a frame takes to write (Time.captureFramerate), so nothing is
    /// dropped. Meanwhile the shots' steps drive the game's own code (New Game's opening, the turntable, the core, the
    /// gun, the darts, the kicks), never the keyboard. Each frame is a JPG under frames/&lt;shot&gt;/; at the end it writes
    /// take.txt (the shots and what happened when) and leaves play, and the recorder puts everything back and makes the
    /// videos. The toy's speech stickers and the opening's black and flash are flat screen layers that a camera does not
    /// draw: they are laid in front of the take's camera each frame so they are in the picture (clip "overlays").
    /// Editor only.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class FootageRig : MonoBehaviour
    {
        // What the recorder leaves for the take (SessionState lasts through the reload into play).
        public const string ClipKey = "ShiftJam.Footage.Clip", OutKey = "ShiftJam.Footage.Out", ArmedKey = "ShiftJam.Footage.ArmedAt";
        /// <summary>An armed take older than this, seconds, is stale (play was never entered), and a later Play stays a game.</summary>
        private const float ArmedFor = 120f;
        /// <summary>Unfilmed seconds after a shot's last setup step: the game's camera settles behind a player just put down.</summary>
        private const float Settle = 0.5f;
        /// <summary>Seconds the game's camera takes to come round onto what a look step looks at.</summary>
        private const float LookEase = 0.15f;
        /// <summary>What the play log's row and detail say about a take (its Counted column reads "... 1").</summary>
        public const string PlayLogLabel = "footage take (D-0242: not a play)";

        private static string pendingClip, pendingOut;

        private enum Phase { Warm, Before, Film, Done }

        private FootageClip clip;
        private string output, frames, shotFolder;
        private Phase phase = Phase.Warm;
        private int index = -1, frame, written, ticks, setupNext, setupDoneTick = -1, warmFrames;
        private bool[] done = System.Array.Empty<bool>();
        private readonly List<FootageClip.Step> setup = new List<FootageClip.Step>();
        private GameObject player;
        private Camera eye;
        private RenderTexture picture;
        private Texture2D still;
        private int ownMask;
        private float ownFar, volumeWas = 1f;
        private bool clockTold;
        // URP draws shadows only so far from a camera, and the game sets that for its own camera (ShadowReach: 50 at toy
        // size). The take's own camera can stand much further from what it films: it gets as far as it needs, then the
        // game's own number again. What the pipeline had before the take, what the game last set, what the take last set.
        private UniversalRenderPipelineAsset pipeline;
        private float shadowWas = -1f, shadowGame = -1f, shadowWrote = -1f, subject;
        // A shot round or after something that moves: what, and where it was last frame (smoothed).
        private Transform watching;
        private Vector3 watched;
        // The steps that last: the game's camera kept on something, a thing turned over some seconds.
        private sealed class Looking { public Transform target; public DarkMonster monster; public float until; public bool fresh; }
        private sealed class Turning { public Transform thing; public Rigidbody body; public Quaternion from; public float degrees, startAt, seconds; }
        private readonly List<Looking> looks = new List<Looking>();
        private readonly List<Turning> turns = new List<Turning>();
        // The flat screen layers laid into the picture, the HUD canvases switched off, and what happened when.
        private readonly List<Canvas> laid = new List<Canvas>();
        private readonly List<Canvas> hudOff = new List<Canvas>();
        private readonly Dictionary<Breakable, float> brokeAt = new Dictionary<Breakable, float>();
        private readonly StringBuilder events = new StringBuilder(), shotsTold = new StringBuilder();
        private readonly HashSet<FootageClip.Step> warned = new HashSet<FootageClip.Step>();

        /// <summary>Seconds of footage written so far.</summary>
        private float Filmed => written / (float)clip.fps;

        // ── Arming: before the world loads, and once it has ──────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Arm()
        {
            float armedAt = SessionState.GetFloat(ArmedKey, -1f);
            SessionState.EraseFloat(ArmedKey);
            string clipPath = SessionState.GetString(ClipKey, ""), outPath = SessionState.GetString(OutKey, "");
            if (armedAt < 0f || EditorApplication.timeSinceStartup - armedAt > ArmedFor || clipPath == "" || outPath == "") return;
            pendingClip = clipPath;
            pendingOut = outPath;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            if (pendingClip == null) return;
            string clipPath = pendingClip, outPath = pendingOut;
            pendingClip = pendingOut = null;
            new GameObject("Footage Rig").AddComponent<FootageRig>().Load(clipPath, outPath);
        }

        private void Load(string clipPath, string outPath)
        {
            output = outPath;
            frames = Path.Combine(outPath, "frames");
            Directory.CreateDirectory(frames);
            clip = FootageClip.Parse(File.ReadAllText(clipPath));
            string[] problems = clip.Problems();
            if (problems.Length > 0)
            {
                Debug.LogError("Footage: " + string.Join("; ", problems));
                Stop();
                return;
            }
            player = GameObject.Find("Player");
            if (player == null) Debug.LogWarning("Footage: nothing here is called Player: the steps that move the player do nothing.");
            MakeCamera();
            warmFrames = Mathf.Max(1, Mathf.RoundToInt(clip.warmup * clip.fps));
            // Game time moves one frame's worth per frame, however long the frame takes to write.
            Time.captureFramerate = clip.fps;
            // Nothing sounds while a take is made (the recorder mutes the editor too); put back at the end.
            volumeWas = AudioListener.volume;
            AudioListener.volume = 0f;
            pipeline = UniversalRenderPipeline.asset;
            if (pipeline != null) shadowWas = shadowGame = pipeline.shadowDistance;
            Listen(true);
            // The play log writes every editor play down as his (docs/PLAYLOG.md: "has this been played?" is a look-up). This
            // one says in its row and its detail that it is a recording, not a play.
            Playtest.PlayLog.Count(PlayLogLabel);
            Playtest.PlayLog.Tell($"{PlayLogLabel}: the footage recorder filming {clip.title}; nobody is playing");
            Debug.Log($"Footage: {clip.title}: {clip.shots.Length} shots, {clip.Length:F1} s at {clip.fps} fps, {clip.width} x {clip.height}, into {frames}.");
        }

        /// <summary>The take's own camera, drawing into a picture of the take's size with the game camera's look.</summary>
        private void MakeCamera()
        {
            var holder = new GameObject("Footage Camera");
            holder.transform.SetParent(transform, false);
            eye = holder.AddComponent<Camera>();
            Dress(eye, Camera.main, clip.near);
            eye.enabled = false;
            // No multisampling: SMAA smooths the edges, as on the game's camera.
            picture = new RenderTexture(clip.width, clip.height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            eye.targetTexture = picture;
            still = new Texture2D(clip.width, clip.height, TextureFormat.RGB24, false);
            ownMask = eye.cullingMask;
            ownFar = eye.farClipPlane;
        }

        /// <summary>
        /// Gives <paramref name="eye"/> the look of <paramref name="look"/> (the game's camera): what it clears to and draws,
        /// how far it sees, its HDR, its URP settings (shadows, depth and colour textures, which volumes it reads), with the
        /// post-processing on and SMAA, as the game's camera has. The recorder's previews use it too.
        /// </summary>
        public static void Dress(Camera eye, Camera look, float near)
        {
            if (look != null)
            {
                eye.clearFlags = look.clearFlags;
                eye.backgroundColor = look.backgroundColor;
                eye.cullingMask = look.cullingMask;
                eye.farClipPlane = look.farClipPlane;
                eye.allowHDR = look.allowHDR;
                eye.useOcclusionCulling = look.useOcclusionCulling;
            }
            eye.allowMSAA = false;
            eye.nearClipPlane = near;
            UniversalAdditionalCameraData data = eye.GetUniversalAdditionalCameraData();
            if (look != null && look.TryGetComponent(out UniversalAdditionalCameraData own))
            {
                data.renderShadows = own.renderShadows;
                data.requiresDepthOption = own.requiresDepthOption;
                data.requiresColorOption = own.requiresColorOption;
                data.volumeLayerMask = own.volumeLayerMask;
                data.stopNaN = own.stopNaN;
                data.dithering = own.dithering;
            }
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        // ── The take, frame by frame ─────────────────────────────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (clip == null || phase == Phase.Done) return;
            ticks++;
            if (phase == Phase.Warm)
            {
                if (frame == 0) { TitleDown(); HudOff(); }
                if (++frame >= warmFrames) StartShot(0);
                return;
            }
            FootageClip.Shot shot = clip.shots[index];
            if (phase == Phase.Before)
            {
                RunSetup();
                if (!ReadyToFilm(shot)) { Looks(); frame++; return; }
                BeginFilming(shot);
            }
            float into = frame / (float)clip.fps;
            RunSetup();   // a setup step that outlived its wait keeps being tried
            for (int k = 0; k < shot.steps.Length; k++)
            {
                FootageClip.Step step = shot.steps[k];
                if (step == null || step.setup || done[k] || step.after > into) continue;
                done[k] = Do(step);
            }
            Looks();
            Aim(shot, into, frame == 0);
            Lay(shot);
            Shoot();
            if (++frame >= shot.Frames(clip.fps)) EndShot(shot);
        }

        private void FixedUpdate()
        {
            for (int k = turns.Count - 1; k >= 0; k--)
            {
                Turning t = turns[k];
                if (t.thing == null) { turns.RemoveAt(k); continue; }
                float share = Mathf.Clamp01((Time.time - t.startAt) / Mathf.Max(0.01f, t.seconds));
                Quaternion now = Quaternion.AngleAxis(t.degrees * share * share * (3f - 2f * share), Vector3.up) * t.from;
                // On its hub the way the game turns it (Grabbable: a kinematic body moved by MoveRotation, riders with it).
                if (t.body != null && t.body.isKinematic) t.body.MoveRotation(now);
                else t.thing.rotation = now;
                if (share >= 1f) turns.RemoveAt(k);
            }
        }

        private void StartShot(int next)
        {
            index = next;
            phase = Phase.Before;
            frame = 0;
            FootageClip.Shot shot = clip.shots[index];
            done = new bool[shot.steps.Length];
            setup.Clear();
            foreach (FootageClip.Step step in shot.steps) if (step != null && step.setup) setup.Add(step);
            setupNext = 0;
            setupDoneTick = setup.Count == 0 ? ticks : -1;
            RunSetup();
        }

        /// <summary>The setup steps in their order: each once the one before it has happened, as many in a frame as can.</summary>
        private void RunSetup()
        {
            while (setupNext < setup.Count && Do(setup[setupNext]))
            {
                setupNext++;
                if (setupNext == setup.Count) setupDoneTick = ticks;
            }
        }

        /// <summary>Whether the shot's unfilmed seconds are over: its "before", its setup (and a moment after it for the
        /// game's camera), its wait. At most "most" seconds more, then it films anyway and says what was missing.</summary>
        private bool ReadyToFilm(FootageClip.Shot shot)
        {
            float waited = frame / (float)clip.fps;
            if (waited < shot.before) return false;
            bool setupDone = setupNext >= setup.Count && setupDoneTick >= 0 && (setup.Count == 0 || (ticks - setupDoneTick) / (float)clip.fps >= Settle);
            bool waitDone = WaitedFor(shot);
            if (setupDone && waitDone) return true;
            if (waited < shot.before + shot.most) return false;
            if (!setupDone) Debug.LogWarning($"Footage: shot {index + 1} ({shot.name}) films after {waited:F1} s without its setup step {setupNext + 1} of {setup.Count} ({Describe(setup[Mathf.Min(setupNext, setup.Count - 1)])}).");
            if (!waitDone) Debug.LogWarning($"Footage: shot {index + 1} ({shot.name}) films after {waited:F1} s: {shot.waitFor.broke} never broke.");
            return true;
        }

        /// <summary>Its wait is over: nothing to wait for, or the named breakable broke long enough ago (game time).</summary>
        private bool WaitedFor(FootageClip.Shot shot)
        {
            if (shot.waitFor == null || string.IsNullOrEmpty(shot.waitFor.broke)) return true;
            Transform thing = Find(shot.waitFor.broke, Vector3.zero);
            Breakable prop = thing != null ? thing.GetComponentInParent<Breakable>() : null;
            return prop != null && brokeAt.TryGetValue(prop, out float at) && Time.time >= at + shot.waitFor.plus;
        }

        private void BeginFilming(FootageClip.Shot shot)
        {
            phase = Phase.Film;
            frame = 0;
            shotFolder = Path.Combine(frames, ShotFolder(index, shot.name));
            Directory.CreateDirectory(shotFolder);
            watching = shot.Move == FootageClip.Orbit || shot.Move == FootageClip.Follow ? Find(shot.target, Vector3.zero) : null;
            if (!string.IsNullOrEmpty(shot.target) && watching == null) Debug.LogWarning($"Footage: shot {index + 1}: nothing here is called \"{shot.target}\"; it films its point instead.");
            shotsTold.Append($"{index + 1:D2}\t{ShotFolder(index, shot.name)}\t{Filmed:F2}\t{Time.time:F2}\t{shot.Frames(clip.fps)}\n");
            Debug.Log($"Footage: {Filmed:F1} s filmed (game {Time.time:F1} s): shot {index + 1}, {shot.name} ({shot.Move}, {shot.seconds:F1} s).");
            if (clockTold) return;
            clockTold = true;
            // The opening, the speech stickers and the camera turns count unscaled time: believed to follow the capture
            // rate as game time does. Said here so a take that proves otherwise is caught.
            float step = Time.unscaledDeltaTime, want = 1f / clip.fps;
            if (Mathf.Abs(step - want) > want * 0.25f)
                Debug.LogWarning($"Footage: unscaled time steps {step * 1000f:F0} ms a frame, not {want * 1000f:F0}: the opening, the speech stickers and the camera turns run at the PC's own pace, faster or slower than the rest.");
            else Debug.Log($"Footage: unscaled time steps with the capture rate ({step * 1000f:F1} ms a frame).");
        }

        private void EndShot(FootageClip.Shot shot)
        {
            for (int k = 0; k < shot.steps.Length; k++)
                if (shot.steps[k] != null && !shot.steps[k].setup && !done[k]) Debug.LogWarning($"Footage: shot {index + 1} ({shot.name}): its step {Describe(shot.steps[k])} never happened.");
            if (index + 1 < clip.shots.Length) StartShot(index + 1);
            else Stop();
        }

        // ── The camera ───────────────────────────────────────────────────────────────────────────────────────────────

        private void Aim(FootageClip.Shot shot, float into, bool cut)
        {
            string move = shot.Move;
            Vector3 at, look;
            subject = 0f;
            if (move == FootageClip.Game)
            {
                // The game's own camera, as the player sees it: its spot, its turn, its view and its planes.
                Camera main = Camera.main;
                if (main == null) return;
                eye.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                eye.fieldOfView = main.fieldOfView;
                eye.nearClipPlane = main.nearClipPlane;
                eye.farClipPlane = main.farClipPlane;
                eye.cullingMask = main.cullingMask;
                return;
            }
            eye.fieldOfView = shot.fov > 0f ? shot.fov : clip.fov;
            eye.nearClipPlane = clip.near;
            eye.farClipPlane = ownFar;
            eye.cullingMask = ownMask;
            if (watching != null)
            {
                // Round or after something that moves: its steps smoothed, so the camera glides rather than jerks.
                watched = cut ? watching.position : Vector3.Lerp(watched, watching.position, 1f - Mathf.Exp(-1f / (clip.fps * Mathf.Max(0.01f, shot.lag))));
            }
            FootageClip.CameraAt(shot, into, watching != null ? watched : (Vector3?)null, out at, out look);
            eye.transform.SetPositionAndRotation(at, FootageClip.Facing(at, look));
            subject = Vector3.Distance(at, look);
        }

        /// <summary>
        /// Shadows as far as the take's own camera needs: twice the way to what it looks at, never less than the game's
        /// own number (ShadowReach's, which follows the player's size). A game-camera shot gets the game's number. The
        /// pipeline's own value comes back at the end of the take (in the editor it lives on the asset).
        /// </summary>
        public static float ShadowReachFor(float game, float subjectDistance) => Mathf.Max(game, 2f * subjectDistance);

        private void Shadows()
        {
            if (pipeline == null) return;
            float now = pipeline.shadowDistance;
            if (shadowWrote < 0f || Mathf.Abs(now - shadowWrote) > 0.001f) shadowGame = now;   // the game set it since (a shift)
            float want = ShadowReachFor(shadowGame, subject);
            if (Mathf.Abs(want - now) > 0.001f) pipeline.shadowDistance = want;
            shadowWrote = want;
        }

        /// <summary>The look steps under way: the game's camera turned onto what each looks at (the light in the hand and the
        /// gun point where that camera looks). It eases round; a fresh one comes round at once.</summary>
        private void Looks()
        {
            float now = ticks / (float)clip.fps;
            for (int k = looks.Count - 1; k >= 0; k--)
            {
                Looking l = looks[k];
                if (l.target == null || now > l.until) { looks.RemoveAt(k); continue; }
                ShoulderCamera rig = player != null ? PlayerSlot.RigFor(player.transform) : null;
                if (rig == null) continue;
                Vector3 to = (l.monster != null ? l.monster.BodyMiddle : Middle(l.target)) - rig.transform.position;
                if (to.sqrMagnitude < 1e-6f) continue;
                float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, pitch = -Mathf.Asin(Mathf.Clamp(to.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                float k2 = l.fresh ? 1f : 1f - Mathf.Exp(-1f / (clip.fps * LookEase));
                l.fresh = false;
                rig.Warp(rig.Yaw + Mathf.DeltaAngle(rig.Yaw, yaw) * k2, Mathf.Lerp(rig.Pitch, pitch, k2));
            }
        }

        /// <summary>
        /// The flat screen layers that belong in the picture (the clip's "overlays": the toy's speech stickers, the opening's
        /// black and flash) laid in front of the take's camera, filling its view exactly as they fill a 1920 x 1080 screen, so
        /// the camera draws them (a screen layer is drawn on the screen, never into a camera's picture). A shot with no
        /// overlays gives them back to the screen. Each is found again every frame: they come and go (the opening's).
        /// </summary>
        private void Lay(FootageClip.Shot shot)
        {
            float tall = 1080f, wide = tall * clip.width / clip.height;
            float near = Mathf.Max(0.01f, eye.nearClipPlane * 1.5f);
            bool switched = false;
            for (int k = 0; k < clip.overlays.Length; k++)
            {
                if (string.IsNullOrEmpty(clip.overlays[k])) continue;
                GameObject root = GameObject.Find("/" + clip.overlays[k]);   // at a scene's top only (kept across loads too)
                if (root == null) continue;
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>())
                {
                    if (!canvas.isRootCanvas) continue;
                    if (shot.noOverlays) { Unlay(canvas); continue; }
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvas.renderMode = RenderMode.WorldSpace;
                        laid.Add(canvas);
                        switched = true;
                    }
                    if (!laid.Contains(canvas)) continue;   // a layer of another kind: left as it is
                    canvas.worldCamera = eye;
                    var box = (RectTransform)canvas.transform;
                    box.pivot = new Vector2(0.5f, 0.5f);
                    box.sizeDelta = new Vector2(wide, tall);
                    // The first named lies nearest, on top of the others.
                    float distance = near * (1f + 0.05f * k);
                    float high = 2f * distance * Mathf.Tan(eye.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    box.SetPositionAndRotation(eye.transform.position + eye.transform.forward * distance, eye.transform.rotation);
                    float parent = box.parent != null ? Mathf.Max(1e-6f, box.parent.lossyScale.x) : 1f;
                    box.localScale = Vector3.one * (high / tall / parent);
                }
            }
            // A layer just laid is drawn the new way from this frame's picture on, not the next one's.
            if (switched) Canvas.ForceUpdateCanvases();
        }

        private void Unlay(Canvas canvas)
        {
            if (canvas == null || !laid.Remove(canvas)) return;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        private void Shoot()
        {
            Shadows();
            eye.Render();
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = picture;
            still.ReadPixels(new Rect(0, 0, clip.width, clip.height), 0, 0, false);
            RenderTexture.active = before;
            File.WriteAllBytes(Path.Combine(shotFolder, $"frame_{frame:D5}.jpg"), still.EncodeToJPG(92));
            written++;
        }

        /// <summary>A shot's folder (and video) name: its number and its name in plain letters.</summary>
        public static string ShotFolder(int index, string name)
        {
            var safe = new StringBuilder();
            foreach (char c in (name ?? "").ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) safe.Append(c);
                else if (safe.Length > 0 && safe[safe.Length - 1] != '-') safe.Append('-');
            return $"{index + 1:D2}-{safe.ToString().Trim('-')}";
        }

        // ── Before the first shot ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The title comes down the way going online takes it down (OnlineWorld): the menu goes, time runs, the
        /// player's keys and camera are back, as its own Close leaves them. New Game's opening is a step of the first shot.</summary>
        private void TitleDown()
        {
            bool any = false;
            foreach (TitleMenu title in FindObjectsByType<TitleMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (title != null) { Destroy(title.gameObject); any = true; }
            if (!any) return;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (player != null && player.TryGetComponent(out PlayerInput keys)) keys.enabled = true;
            ShoulderCamera rig = player != null ? PlayerSlot.RigFor(player.transform) : null;
            if (rig != null) rig.enabled = true;
            Debug.Log("Footage: the title is down; the world settles before the first shot.");
        }

        /// <summary>The HUD's canvases off for the take (on again at its end). They are screen layers the take's camera
        /// never draws anyway; off, they are out of the way for good.</summary>
        private void HudOff()
        {
            GameObject hud = GameObject.Find("/HUD");
            if (hud == null) return;
            foreach (Canvas canvas in hud.GetComponentsInChildren<Canvas>())
                if (canvas.enabled) { canvas.enabled = false; hudOff.Add(canvas); }
        }

        // ── The steps: each through the game's own code ──────────────────────────────────────────────────────────────

        /// <summary>One step. True once it has happened (or cannot, said once); false to try again next frame.</summary>
        private bool Do(FootageClip.Step step)
        {
            switch (step.Does)
            {
                case "newgame": return NewGame();
                case "place": return Place(step);
                case "shift": return Shift(step);
                case "use": return Use(step);
                case "turn": return Turn(step);
                case "light": return Light(step);
                case "look": return Look(step);
                case "stun": return Stun(step);
                case "wake": return Wake(step);
                case "fire": return Fire(step);
                case "hit": return Hit(step);
                case "kick": return Strike(step, true);
                case "punch": return Strike(step, false);
                case "draw": return Draw(step);
                case "seen": return Seen(step);
                default: return true;
            }
        }

        /// <summary>New Game, as the title's button plays it (TitleMenu.NewGame): the slot emptied (the take's scratch slot:
        /// the recorder chose it and puts every slot back after), the toy back in its box, the opening.</summary>
        private bool NewGame()
        {
            if (player == null) return Cannot(null, "there is no Player to start a New Game with");
            SaveGame.Clear(SaveGame.Slot);
            if (player.TryGetComponent(out Respawn respawn)) respawn.ToStart();
            Opening.Play(player);
            Note("New Game: the toy is back in its box and the opening plays");
            return true;
        }

        /// <summary>The player put somewhere at once, as a spawn or a portal does (PlayerMotor.Warp), with its checkpoint
        /// there too and the game's camera behind it. Whatever it held on to (the turntable) is let go first.</summary>
        private bool Place(FootageClip.Step step)
        {
            if (player == null) return Cannot(step, "there is no Player to put anywhere");
            Grabbable.Release(player);
            Quaternion facing = Quaternion.Euler(0f, step.yaw, 0f);
            if (player.TryGetComponent(out PlayerMotor motor)) motor.Warp(step.at, facing);
            else player.transform.SetPositionAndRotation(step.at, facing);
            if (player.TryGetComponent(out Respawn respawn)) respawn.SetPoint(step.at, facing);
            ShoulderCamera rig = PlayerSlot.RigFor(player.transform);
            if (rig != null) rig.Warp(float.IsNaN(step.view) ? step.yaw : step.view, float.IsNaN(step.pitch) ? (float?)null : step.pitch);
            Note($"the player is put at ({step.at.x:F1}, {step.at.y:F1}, {step.at.z:F1}) facing {step.yaw:F0}");
            return true;
        }

        /// <summary>The player grows or shrinks the game's own way (Shifter.ShiftTo: its events, its warp, the box's burst).
        /// Done once the shift has ended.</summary>
        private bool Shift(FootageClip.Step step)
        {
            if (player == null || !player.TryGetComponent(out Shifter shifter)) return Cannot(step, "the player has no Shifter");
            ShiftSize want = step.size == "kid" ? ShiftSize.Kid : ShiftSize.Toy;
            if (shifter.Shifting) return false;
            if (shifter.Size == want) return true;
            shifter.ShiftTo(want);
            Note($"the player starts to {(want == ShiftSize.Kid ? "grow" : "shrink")}");
            return false;
        }

        /// <summary>Uses a thing the way E does (its own Interact, with the player's Interactor), once it offers the player
        /// something (a prompt): the turntable, the core, a flashlight, the gun's stand, a switch.</summary>
        private bool Use(FootageClip.Step step)
        {
            Transform thing = Target(step);
            if (thing == null) return Cannot(step, $"nothing here is called \"{step.target}\" to use");
            IInteractable use = Usable(thing);
            Interactor who = player != null ? player.GetComponentInChildren<Interactor>() : null;
            if (use == null || who == null) return Cannot(step, $"{step.target} cannot be used, or the player has no Interactor");
            string prompt = use.PromptFor(who);
            if (string.IsNullOrEmpty(prompt)) return false;   // it offers the player nothing yet: tried again next frame
            use.Interact(who);
            Note($"used {step.target} (\"{prompt}\")");
            return true;
        }

        /// <summary>What can be used on a thing: on it, else under it, else over it. Asked plainly, never through ??, which
        /// cannot see a destroyed component as gone.</summary>
        private static IInteractable Usable(Transform thing)
        {
            if (thing.TryGetComponent(out IInteractable own)) return own;
            IInteractable below = thing.GetComponentInChildren<IInteractable>(true);
            if (below is Object b && b != null) return below;
            IInteractable above = thing.GetComponentInParent<IInteractable>(true);
            return above is Object a && a != null ? above : null;
        }

        /// <summary>Turns a thing about its hub until its beam (the first flashlight on it, else its own front) points at a
        /// target, eased over the step's seconds: the turntable's light onto the core (moved as the game moves it).</summary>
        private bool Turn(FootageClip.Step step)
        {
            Transform thing = Target(step);
            Transform toward = Find(step.toward, Vector3.zero);
            if (thing == null || toward == null) return Cannot(step, $"nothing here is called \"{(thing == null ? step.target : step.toward)}\" for the turn");
            Flashlight torch = thing.GetComponentInChildren<Flashlight>(true);
            Transform beam = torch != null && torch.Beam != null ? torch.Beam.transform : thing;
            Rigidbody body = thing.GetComponent<Rigidbody>();
            Vector3 hub = body != null ? body.position : thing.position;
            float degrees = FootageClip.TurnToward(hub, beam.position, beam.forward, Middle(toward));
            turns.Add(new Turning { thing = thing, body = body, from = thing.rotation, degrees = degrees, startAt = Time.time, seconds = step.seconds });
            Note($"turning {step.target} {degrees:F1} degrees over {step.seconds:F1} s, its beam onto {step.toward}");
            return true;
        }

        /// <summary>Switches a flashlight on or off with its own switch (Flashlight.Set).</summary>
        private bool Light(FootageClip.Step step)
        {
            Transform thing = Target(step);
            Flashlight torch = thing != null ? thing.GetComponentInChildren<Flashlight>(true) : null;
            if (torch == null) return Cannot(step, $"no flashlight here is called \"{step.target}\"");
            torch.Set(step.on, silent: false);
            Note($"{step.target} switched {(step.on ? "on" : "off")}");
            return true;
        }

        /// <summary>Keeps the game's camera on a thing for the step's seconds (ShoulderCamera.Warp, a frame at a time): what
        /// the player's light and gun point at is what that camera looks at.</summary>
        private bool Look(FootageClip.Step step)
        {
            Transform thing = Target(step);
            if (thing == null) return Cannot(step, $"nothing here is called \"{step.target}\" to look at");
            DarkMonster monster = thing.GetComponentInParent<DarkMonster>();
            if (monster == null) monster = thing.GetComponentInChildren<DarkMonster>();
            looks.Add(new Looking { target = thing, monster = monster, until = ticks / (float)clip.fps + step.seconds, fresh = true });
            Note($"the game's camera looks at {step.target} for {step.seconds:F1} s");
            return true;
        }

        /// <summary>A monster stunned by light (DarkMonster.Stun, the star lamp's sweep): dizzy now, unless the beam already
        /// made it so. The fallback for a beam that did not reach it.</summary>
        private bool Stun(FootageClip.Step step)
        {
            Transform thing = Target(step);
            DarkMonster monster = thing != null ? thing.GetComponentInChildren<DarkMonster>(true) : null;
            if (monster == null) return Cannot(step, $"no monster here is called \"{step.target}\"");
            if (monster.Dizzy) Note($"{step.target} is already dizzy (the beam did it)");
            else if (monster.Stun()) Note($"{step.target} stunned by hand: the beam had not made it dizzy");
            else Note($"{step.target} could not be stunned (down, borrowed or not awake)");
            return true;
        }

        /// <summary>A sleeping core woken (ShiftSource.Awaken). The fallback for a light that did not wake it.</summary>
        private bool Wake(FootageClip.Step step)
        {
            Transform thing = Target(step);
            ShiftSource core = thing != null ? thing.GetComponentInChildren<ShiftSource>(true) : null;
            if (core == null) return Cannot(step, $"no core here is called \"{step.target}\"");
            if (!core.Dormant) return true;
            core.Awaken();
            Debug.LogWarning($"Footage: {Filmed:F1} s filmed: {step.target} was still asleep: woken by hand (the light had not reached it).");
            return true;
        }

        /// <summary>One pull of the trigger, as a click makes it (WeaponHolder.PullTrigger): the shot goes where the game's
        /// camera looks. Tried again while there is no gun in the hand.</summary>
        private bool Fire(FootageClip.Step step)
        {
            WeaponHolder holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            if (holder == null) return Cannot(step, "the player has no WeaponHolder");
            if (holder.Current == null || !holder.PullTrigger()) return false;
            Note($"fired the {holder.Current.DisplayName}");
            return true;
        }

        /// <summary>A blow to a thing's health (Damageable.Hit), all of it unless the step says how much, from the step's
        /// "from": a prop bursts into its pieces.</summary>
        private bool Hit(FootageClip.Step step)
        {
            Transform thing = Target(step);
            Damageable health = thing != null ? thing.GetComponentInParent<Damageable>() : null;
            if (health == null && thing != null) health = thing.GetComponentInChildren<Damageable>();
            if (health == null) return Cannot(step, $"nothing with health here is called \"{step.target}\"");
            if (health.Dead) return true;
            Bounds box = BoundsOf(health.transform);
            Vector3 from = step.from != Vector3.zero ? step.from : box.center + Vector3.up * (box.extents.magnitude + 1f);
            Vector3 way = box.center - from;
            way = way.sqrMagnitude > 1e-6f ? way.normalized : Vector3.down;
            Vector3 point = box.center - way * (box.extents.magnitude * 0.5f);
            float amount = step.damage > 0f ? step.damage : health.MaxHealth;
            if (!health.Hit(new DamageInfo(amount, way * 6f, point, player != null ? player.transform : null))) return false;   // turned away for a moment: again
            Note($"hit {step.target} for {amount:F0}{(health.Dead ? ": it breaks" : "")}");
            return true;
        }

        /// <summary>A kick or a punch, as the mouse starts one (PlayerMoves.TryKick, TryPunch). Tried again until the move
        /// before it has finished.</summary>
        private bool Strike(FootageClip.Step step, bool kick)
        {
            PlayerMoves moves = player != null ? player.GetComponent<PlayerMoves>() : null;
            if (moves == null) return Cannot(step, "the player has no PlayerMoves");
            if (!(kick ? moves.TryKick() : moves.TryPunch())) return false;
            Note(kick ? "kick" : "punch");
            return true;
        }

        /// <summary>The gun out or put away (WeaponHolder.SetDrawn).</summary>
        private bool Draw(FootageClip.Step step)
        {
            WeaponHolder holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            if (holder == null) return Cannot(step, "the player has no WeaponHolder");
            holder.SetDrawn(step.on);
            Note(step.on ? "the gun is out" : "the gun is put away");
            return true;
        }

        /// <summary>A moment marked in the take's save slot, as the game marks it (SaveGame.MarkSeen): the room's lights, the
        /// bedroom gate. What listens reads it at once.</summary>
        private bool Seen(FootageClip.Step step)
        {
            SaveGame.MarkSeen(SaveGame.Slot, step.id);
            Note($"marked \"{step.id}\" in the take's save slot");
            return true;
        }

        /// <summary>A step that cannot happen: said once, and counted as done.</summary>
        private bool Cannot(FootageClip.Step step, string why)
        {
            if (step == null || warned.Add(step)) Debug.LogWarning($"Footage: {Filmed:F1} s filmed: {why}.");
            return true;
        }

        private void Note(string what) => Debug.Log($"Footage: {Filmed:F2} s filmed (game {Time.time:F2} s): {what}.");

        private static string Describe(FootageClip.Step step) => step == null ? "(empty)" : step.Does + (string.IsNullOrEmpty(step.target) ? "" : " " + step.target);

        private Transform Target(FootageClip.Step step) => Find(step.target, step.Does == "hit" ? step.at : Vector3.zero);

        // ── Finding things ───────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A thing by its path of names from a scene's top ("Room/ToyBox/Turntable"), switched off or not; of several
        /// with the same path, the one nearest <paramref name="near"/> (the first when it is 0, 0, 0). Null when none.</summary>
        public static Transform Find(string path, Vector3 near)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string[] names = path.Split('/');
            var found = new List<Transform>();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == names[0]) Descend(root.transform, names, 1, found);
            }
            if (found.Count == 0) return null;
            if (near == Vector3.zero) return found[0];
            Transform best = found[0];
            foreach (Transform t in found) if ((t.position - near).sqrMagnitude < (best.position - near).sqrMagnitude) best = t;
            return best;
        }

        private static void Descend(Transform at, string[] names, int depth, List<Transform> found)
        {
            if (depth == names.Length) { found.Add(at); return; }
            for (int c = 0; c < at.childCount; c++)
            {
                Transform child = at.GetChild(c);
                if (child.name == names[depth]) Descend(child, names, depth + 1, found);
            }
        }

        /// <summary>The middle of what a thing draws (its own place when it draws nothing).</summary>
        public static Vector3 Middle(Transform thing) => BoundsOf(thing).center;

        private static Bounds BoundsOf(Transform thing)
        {
            var box = new Bounds(thing.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in thing.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { box = r.bounds; any = true; }
                else box.Encapsulate(r.bounds);
            }
            return box;
        }

        // ── What happened, for the log and take.txt ──────────────────────────────────────────────────────────────────

        private void Listen(bool on)
        {
            if (on)
            {
                GameEvents.OpeningLid += OnLid; GameEvents.OpeningSpark += OnSpark; GameEvents.OpeningGetUp += OnGetUp; GameEvents.OpeningEnded += OnOpeningEnded;
                GameEvents.SourceAwakened += OnAwakened; GameEvents.Shifted += OnShifted; GameEvents.PropBroke += OnBroke;
                GameEvents.MonsterSpotted += OnSpotted; GameEvents.MonsterDizzy += OnDizzy; GameEvents.MonsterDown += OnDown;
                GameEvents.WeaponTaken += OnTaken; GameEvents.ShotLanded += OnLanded; GameEvents.SwitchPressed += OnSwitch;
                GameEvents.GoalReached += OnGoal; GameEvents.PlayerReset += OnReset;
            }
            else
            {
                GameEvents.OpeningLid -= OnLid; GameEvents.OpeningSpark -= OnSpark; GameEvents.OpeningGetUp -= OnGetUp; GameEvents.OpeningEnded -= OnOpeningEnded;
                GameEvents.SourceAwakened -= OnAwakened; GameEvents.Shifted -= OnShifted; GameEvents.PropBroke -= OnBroke;
                GameEvents.MonsterSpotted -= OnSpotted; GameEvents.MonsterDizzy -= OnDizzy; GameEvents.MonsterDown -= OnDown;
                GameEvents.WeaponTaken -= OnTaken; GameEvents.ShotLanded -= OnLanded; GameEvents.SwitchPressed -= OnSwitch;
                GameEvents.GoalReached -= OnGoal; GameEvents.PlayerReset -= OnReset;
            }
        }

        private void Happened(string what, bool warn = false)
        {
            string line = $"{Filmed:F2} s filmed (game {Time.time:F2} s), shot {index + 1}: {what}";
            events.AppendLine(line);
            if (warn) Debug.LogWarning("Footage: " + line + ".");
            else Debug.Log("Footage: " + line + ".");
        }

        private void OnLid() => Happened("the box's flaps open");
        private void OnSpark(bool struck) => Happened(struck ? "the spark strikes the toy" : "the spark appears");
        private void OnGetUp() => Happened("the toy gets up");
        private void OnOpeningEnded(bool skipped) => Happened("the opening hands over" + (skipped ? " (skipped)" : ""));
        private void OnAwakened(ShiftSource core) => Happened($"{(core != null ? core.name : "a core")} wakes");
        private void OnShifted(Shifter who, ShiftSize size) { if (who != null && player != null && who.gameObject == player) Happened($"the player is a {(size == ShiftSize.Kid ? "kid" : "toy")}"); }
        private void OnBroke(Breakable prop) { if (prop == null) return; brokeAt[prop] = Time.time; Happened($"{prop.name} breaks"); }
        private void OnSpotted(DarkMonster monster) => Happened($"{(monster != null ? monster.name : "a monster")} spots the toy");
        private void OnDizzy(DarkMonster monster) => Happened($"{(monster != null ? monster.name : "a monster")} is dizzy");
        private void OnDown(DarkMonster monster) => Happened($"{(monster != null ? monster.name : "a monster")} is knocked out");
        private void OnTaken(WeaponHolder holder, Weapon weapon) => Happened($"the player takes the {(weapon != null ? weapon.DisplayName : "gun")}");
        private void OnLanded(WeaponHolder holder, bool finished) => Happened(finished ? "a shot lands and finishes what it hit" : "a shot lands on something with health");
        private void OnSwitch(Switch pressed) => Happened($"{(pressed != null ? pressed.name : "a switch")} is pressed");
        private void OnGoal(Goal goal) => Happened($"a Goal is reached ({(goal != null ? goal.name : "")}): the game has ended", true);
        private void OnReset(Respawn who) => Happened("the player is put back at a checkpoint", true);

        // ── The end ──────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The take is over (or cannot start): says what it wrote, puts back what it changed in play, and leaves
        /// play; the recorder does the rest (the saves, the sound, the videos).</summary>
        private void Stop()
        {
            if (phase == Phase.Done) return;
            phase = Phase.Done;
            int fps = clip != null ? clip.fps : 30;
            Time.captureFramerate = 0;
            PutBack();
            if (output != null)
            {
                var take = new StringBuilder();
                take.AppendLine($"clip\t{(clip != null ? clip.title : "")}");
                take.AppendLine($"fps\t{fps}");
                take.AppendLine($"frames\t{written}");
                take.AppendLine("shot\tfolder\tstarts at (seconds of footage)\tgame time\tframes");
                take.Append(shotsTold);
                take.AppendLine("what happened:");
                take.Append(events);
                File.WriteAllText(Path.Combine(output, "take.txt"), take.ToString());
            }
            Debug.Log($"Footage: {written} frames written to {frames}.");
            EditorApplication.isPlaying = false;
        }

        /// <summary>What the take changed in play, as it was: the screen layers, the HUD, the sound's level, the time step, the
        /// pipeline's shadow distance (as before the take; ShadowReach puts the same value back as play ends).</summary>
        private void PutBack()
        {
            for (int k = laid.Count - 1; k >= 0; k--) Unlay(laid[k]);
            foreach (Canvas canvas in hudOff) if (canvas != null) canvas.enabled = true;
            hudOff.Clear();
            AudioListener.volume = volumeWas;
            Time.captureFramerate = 0;
            if (pipeline != null && shadowWas >= 0f) pipeline.shadowDistance = shadowWas;
            shadowWrote = -1f;
        }

        private void OnDestroy()
        {
            Listen(false);
            if (phase != Phase.Done) PutBack();
            if (picture != null)
            {
                picture.Release();
                Destroy(picture);
            }
            if (still != null) Destroy(still);
        }
    }
}
#endif
