#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShiftJam
{
    /// <summary>
    /// The backyard (D-0239), landed into the OPEN Greybox; it never opens or saves a scene. Menu: Shift Jam / House / Land
    /// Backyard (D-0239); <see cref="Land"/> returns the report (one fact a line; a report that starts "[Backyard] NOT"
    /// changed nothing). Everything it builds is under Room/Course/R6_Window/Backyard (D-0239), made again from nothing on
    /// each run (no duplicates). Outside that root it only switches things and writes words, each time the same way:
    /// - the sill's old Goal is switched off (kept), and the old GardenTrees inside the play area off, or all on (Inside Trees Off);
    /// - the garden sun is tuned (Tune Sun), the Curved World rest bend made flat (Flatten Bend), the Ground given the lawn
    ///   (Lawn On; its old material kept on the settings);
    /// - Resources/ObjectiveSet (the goal, the step "sill", a new last step, the star "sill", a new star, the garden's zone,
    ///   arrival card and core place), Resources/OpeningSettings (made if missing: the goal line) and the end card's title:
    ///   each word is written only where the old word still stands, never over his;
    /// - Resources/BackyardSettings is made if missing (the provisional kit filled into empty lists only), and URP Lit copies
    ///   of the pack's materials are made in Art/Garden/Materials (never the pack's own assets).
    /// The yard: the window opens (WindowOpening) when the sill's checkpoint is claimed; a ramp and a step take the toy up
    /// into its open pane; a spare paper plane lies on the sill; outside, a lawn for the glide down, a maze of tall grass
    /// (hedges either side), a pond with a log (hedges either side), a flowerbed with a lost toy (a gold star), a core by
    /// the back wall, and the back wall itself: slatted (one box per slat: the toy slips through anywhere, the kid never),
    /// its gate filled with the "Gate Panel"; past it the Goal (either size by End Any Size): GameFlow.End and the balloons.
    /// The sides are rows of blocky foliage with an invisible seal right behind, and the seals close the pen past the end.
    /// The SICS pieces wear URP Lit copies of their materials. Checkpoints on the sill, at the landing, before the pond and
    /// before the wall; the Sticker Chart zone and arrival card "garden"; blocky backdrop trees past the end.
    /// Then it checks: the toy's walk from the sill out of the window and from the landing to past the wall, the gap (toy
    /// yes, kid no), the kid over the back boundary, nothing floating or sunk, nothing in the window's or the glide's way.
    /// The scene is marked changed and not saved (Ctrl+S keeps it). A re-run of CourseBuilder's R6 part needs this again.
    /// </summary>
    public static class BackyardBuilder
    {
        public const string Tag = "[Backyard]";
        public const string RootName = "Backyard (D-0239)";
        /// <summary>The gate's filling, by name: D-0240 (a breakable section) finds and swaps it.</summary>
        public const string GatePanelName = "Gate Panel";

        private const string ScenePath = "Assets/_Game/Scenes/Greybox.unity";
        private const string R6Path = "Room/Course/R6_Window";
        private const string SettingsPath = "Assets/_Game/Resources/" + BackyardSettings.ResourceName + ".asset";
        private const string OpeningPath = "Assets/_Game/Resources/OpeningSettings.asset";
        private const string MaterialFolder = "Assets/_Game/Art/Garden/Materials";
        private const string PondMaterialPath = MaterialFolder + "/Garden_Pond.mat", SoilMaterialPath = MaterialFolder + "/Garden_Soil.mat", LawnMaterialPath = MaterialFolder + "/Garden_Lawn.mat";
        private const string LawnTexture = "TNA_Grass_1A_D", LawnTextureFolder = "Assets/Toon Series";
        private const string CheckpointGlowPath = "Assets/_Game/Data/CheckpointGlow.mat";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string PlayroomFolder = "Assets/_Imports/ithappy/Playroom";
        private static readonly string[] KitFolders = { "Assets/Toon Suburban Pack", "Assets/Toon Series", PlayroomFolder, "Assets/ithappy/Cartoon_City_Free" };

        // The words this change replaces: known only so that they, and never his, are replaced.
        private const string OldGoal = "Back on the window sill", OldStepWords = "Get onto the window sill", OldStarTitle = "Back on the sill";
        private const string OldGoalLine = "I need to get back up on that window sill.", OldEndCard = "You made it to the window.";
        private const string OldGoalPath = R6Path + "/Goal", SillId = "sill";

        private const int IgnoreRaycastLayer = 2, ModeVoid = 1, RuntimeOnly = 2, MaxSlatBoxes = 128;
        private const float Tight = 0.05f;   // how far off counts as floating or sunk

        [MenuItem("Shift Jam/House/Land Backyard (D-0239)")]
        public static void LandMenu()
        {
            string report = Land();
            if (report.StartsWith(Tag + " NOT")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>Lands (or lands again) the backyard in the OPEN Greybox and marks it changed; it does not save it. Returns
        /// the report, one fact a line; "[Backyard] NOT" at its start means nothing was changed.</summary>
        public static string Land()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return $"{Tag} NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                return $"{Tag} NOT landed: the active scene is '{scene.path}', and the backyard is in {ScenePath}. Open Greybox and run this again (it never opens a scene by itself). Nothing was changed.";
            Transform r6 = Objectives.Resolve(scene, R6Path);
            if (r6 == null) return $"{Tag} NOT landed: no {R6Path} in Greybox (run Shift Jam > Build Course first). Nothing was changed.";
            var missing = new List<string>();
            Transform window = r6.Find("Window"), sill = r6.Find("Sill"), ground = r6.Find("Ground"), coreD = r6.Find("CoreD_Window");
            if (window == null) missing.Add("Window");
            if (sill == null) missing.Add("Sill");
            if (ground == null || ground.GetComponent<Collider>() == null) missing.Add("Ground (with its collider)");
            if (coreD == null || coreD.GetComponent<ShiftSource>() == null) missing.Add("CoreD_Window (a ShiftSource)");
            MeshFilter wmf = window != null ? window.GetComponentInChildren<MeshFilter>() : null;
            if (window != null && (wmf == null || wmf.sharedMesh == null || wmf.GetComponent<MeshRenderer>() == null)) missing.Add("the Window's mesh and renderer");
            if (window != null && window.GetComponentInChildren<MeshCollider>() == null) missing.Add("the Window's MeshCollider");
            GameObject player = GameObject.Find("Player");
            if (player == null || player.GetComponent<CharacterController>() == null) missing.Add("Player (with its CharacterController)");
            if (missing.Count > 0)
                return $"{Tag} NOT landed: under {R6Path} (or at the scene's top) there is no {string.Join("; no ", missing)}. Nothing was changed.";
            return new Yard(scene, r6, player).Run();
        }

        // ================================================================================================ the yard

        private sealed class Yard
        {
            private readonly Scene _scene;
            private readonly Transform _r6, _window, _sill, _ground, _coreD;
            private readonly Collider _groundCol;
            private readonly MeshFilter _wmf;
            private readonly MeshRenderer _wmr;
            private readonly MeshCollider _wmc;
            private readonly GameObject _player;
            private readonly StringBuilder _log = new StringBuilder();
            private BackyardSettings _s;
            private System.Random _rng;
            private Transform _root;

            // The yard's frame: u across (+ to the right looking out of the window), v out from the house's outer wall.
            private Vector3 _origin, _fwd, _right;
            private Quaternion _turn;
            private float _groundY;

            // The bodies (from the Player and its settings).
            private float _toyR, _toyH, _kidR, _kidH, _kidCrouchH, _toyJump, _kidJump, _kidGravity, _kidSprint, _fatalDrop, _killHeight;

            // The layout.
            private float _halfW, _inner, _seal, _farV, _wallV, _m0, _m1, _mazeW, _cell, _pondV, _pondR, _bedV;
            private int _entryCol, _exitCol;
            private readonly List<Vector2> _mazeRoute = new List<Vector2>();
            private Vector2 _bed, _corePos;

            private readonly Dictionary<string, List<GameObject>> _placed = new Dictionary<string, List<GameObject>>();
            private readonly List<Rect> _keepOut = new List<Rect>();
            private readonly List<(string name, Bounds box)> _standable = new List<(string, Bounds)>();
            private readonly List<(string name, float top)> _boundaryTops = new List<(string, float)>();
            private readonly Dictionary<GameObject, Bounds> _prefabBoxes = new Dictionary<GameObject, Bounds>();
            private readonly Dictionary<Mesh, bool[,]> _rasters = new Dictionary<Mesh, bool[,]>();
            private readonly Dictionary<Mesh, List<Bounds>> _slats = new Dictionary<Mesh, List<Bounds>>();

            // The window and the end, for the checks.
            private WindowOpening _opening;
            private GameObject _frame;
            private readonly List<Vector3> _windowWalk = new List<Vector3>();
            private Transform _gatePanel;
            private Vector3 _gapPoint, _endPoint, _sillPoint;
            private float _gapWidth;
            private Transform _hazard;
            private float _hazardR;
            private int _swapped, _swapMade;

            public Yard(Scene scene, Transform r6, GameObject player)
            {
                _scene = scene; _r6 = r6; _player = player;
                _window = r6.Find("Window"); _sill = r6.Find("Sill"); _ground = r6.Find("Ground"); _coreD = r6.Find("CoreD_Window");
                _groundCol = _ground.GetComponent<Collider>();
                _wmf = _window.GetComponentInChildren<MeshFilter>();
                _wmr = _wmf.GetComponent<MeshRenderer>();
                _wmc = _window.GetComponentInChildren<MeshCollider>();
            }

            public string Run()
            {
                Transform old = _r6.Find(RootName);
                _log.AppendLine($"{Tag} Land Backyard (D-0239): {(old != null ? "SECOND run: the old '" + RootName + "' deleted and everything made again (no duplicates)" : "first run")}. Greybox is MARKED CHANGED and NOT SAVED: Ctrl+S keeps it.");
                _s = Settings();
                _rng = new System.Random(_s.seed);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                _root = new GameObject(RootName).transform;
                _root.SetParent(_r6, false);
                Physics.SyncTransforms();

                Bodies();
                Frame();
                Layout();
                Trees();   // first: the old trees inside the play area go before anything is placed round them
                Sill();
                Window();
                Boundaries();
                Maze();
                Pond();
                Flowerbed();
                Core();
                Checkpoints();
                End();
                Arrival();
                Dressing();
                Lawn();
                Materials();
                Words();
                LightAndBend();
                Physics.SyncTransforms();
                Checks();

                if (_frame != null) _frame.SetActive(false);   // closed in the editor: WindowOpening opens it in play
                EditorSceneManager.MarkSceneDirty(_scene);
                Summary();
                return _log.ToString();
            }

            // -------------------------------------------------------------------------------------------- the settings

            private BackyardSettings Settings()
            {
                var s = AssetDatabase.LoadAssetAtPath<BackyardSettings>(SettingsPath);
                bool made = s == null;
                if (made) { s = ScriptableObject.CreateInstance<BackyardSettings>(); AssetDatabase.CreateAsset(s, SettingsPath); }
                var filled = new List<string>();
                // The PROVISIONAL kit (the SICS Toon packs and the playroom's own pieces on disk, 25 Nov): only an EMPTY list is
                // filled, so his picks are never overwritten.
                Fill(s.grass, "grass", filled, "Reed_1A", "Reed_1B", "Reed_1C", "Reed_1D");
                Fill(s.flowers, "flowers", filled, "TSP_Flower_01A", "TSP_Flower_01B", "TSP_Flower_02A", "TSP_Flower_02B", "TSP_Flower_03A", "TSP_Flower_03B", "TSP_Flower_04A", "TSP_Flower_04B", "Flower_3A", "Flower_3C", "Flower_3E", "Flower_3G");
                Fill(s.hedges, "hedges", filled, "TSP_Bush_01A", "TSP_Bush_01B", "TSP_Bush_01C", "TSP_Bush_02A", "TSP_Bush_02B", "TSP_Bush_02C");
                Fill(s.rocks, "rocks", filled, "TSP_Rock_Small_01A", "TSP_Rock_Small_02A", "TSP_Rock_Small_03A");
                // the blocky ithappy foliage (his call): the boundary and the backdrop
                Fill(s.boundary, "boundary", filled, "Toy_Tree_01", "Toy_Bush_01", "Wooden_Tree_01");
                Fill(s.backdrop, "backdrop", filled, "Toy_Tree_01", "Wooden_Tree_01", "Toy_Bush_01", "Bush_06", "Bush_07");
                Fill(s.backWall, "back wall", filled, "TSP_Garden_Wall_Short_01A_End", "TSP_Garden_Wall_Short_01A");
                Fill(s.gate, "gate", filled, "TSP_Garden_Wall_Short_01A_Gate");
                Fill(s.gatePanel, "gate panel", filled, "TSP_Garden_Wall_Short_01A");
                Fill(s.waterPlants, "water plants", filled, "Water_Plant_1A", "Water_Plant_1B", "Water_Plant_3A", "Water_Plant_3B");
                Fill(s.crossing, "crossing", filled, "Tree_Fallen_1A");
                Fill(s.pathStones, "path stones", filled, "TSP_Pavement_01A_Round", "TSP_Pavement_01A_Small");
                Fill(s.props, "props", filled, "TSP_Grill_01A", "TSP_Wood_Table_01A", "TSP_Metal_Chair_01A", "TSP_Bench_01A", "TSP_Chaise_Lounge_01A");
                Fill(s.accents, "accents", filled, "Mushroom_1A", "Mushroom_1B", "Mushroom_1C", "Mushroom_1D", "Mushroom_1E", "Mushroom_1F");
                Fill(s.lostToy, "lost toy", filled, "Toy_Bear_01");
                Fill(s.windowRamp, "window ramp", filled, "Ruler_Big_01");
                if (s.windowOpens.clip == null) { s.windowOpens = new CourseSounds.Cue(Clip("Drawer_Open_v1"), 0.6f, 0.05f); if (s.windowOpens.clip != null) filled.Add("window sound Drawer_Open_v1"); }
                if (s.lostToyFound.clip == null) { s.lostToyFound = new CourseSounds.Cue(Clip("Toy_Squeak_v1"), 0.7f, 0.08f); if (s.lostToyFound.clip != null) filled.Add("found sound Toy_Squeak_v1"); }
                if (s.water == null) { s.water = OwnMaterial(PondMaterialPath, new Color(0.30f, 0.62f, 0.86f, 0.78f), 0.92f, true); filled.Add("water " + PondMaterialPath); }
                if (s.soil == null) { s.soil = OwnMaterial(SoilMaterialPath, new Color(0.36f, 0.25f, 0.16f, 1f), 0.08f, false); filled.Add("soil " + SoilMaterialPath); }
                if (s.lawn == null)
                {
                    s.lawn = OwnMaterial(LawnMaterialPath, Color.white, 0.07f, false);
                    Texture grass = FindTexture(LawnTexture, LawnTextureFolder);
                    if (grass != null) { s.lawn.SetTexture("_BaseMap", grass); EditorUtility.SetDirty(s.lawn); AssetDatabase.SaveAssetIfDirty(s.lawn); }
                    filled.Add($"lawn {LawnMaterialPath} ({(grass != null ? LawnTexture : "NO " + LawnTexture + " found: plain white")})");
                }
                EditorUtility.SetDirty(s);
                AssetDatabase.SaveAssetIfDirty(s);
                _log.AppendLine($"SETTINGS: {SettingsPath} {(made ? "MADE with the provisional kit" : "kept as he left it")}{(filled.Count > 0 ? "; filled where empty: " + string.Join(", ", filled) : "; nothing needed filling")}. Every word on it is a PLACEHOLDER.");
                return s;
            }

            private void Fill(BackyardSettings.Role role, string what, List<string> filled, params string[] names)
            {
                if (role.prefabs != null && role.prefabs.Length > 0) return;
                var found = new List<GameObject>();
                var lost = new List<string>();
                foreach (string n in names) { GameObject p = FindPrefab(n); if (p != null) found.Add(p); else lost.Add(n); }
                role.prefabs = found.ToArray();
                filled.Add($"{what} ({found.Count}{(lost.Count > 0 ? ", NOT FOUND: " + string.Join(" ", lost) : "")})");
            }

            private static GameObject FindPrefab(string name)
            {
                foreach (string folder in KitFolders)
                {
                    if (!AssetDatabase.IsValidFolder(folder)) continue;
                    foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { folder }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    }
                }
                return null;
            }

            private static Texture FindTexture(string name, string folder)
            {
                if (!AssetDatabase.IsValidFolder(folder)) return null;
                foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Texture>(path);
                }
                return null;
            }

            private static AudioClip Clip(string name)
            {
                foreach (string guid in AssetDatabase.FindAssets(name + " t:AudioClip", new[] { PlayroomFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
                return null;
            }

            /// <summary>One of our own URP Lit materials, made once (a later run keeps what he set on it).</summary>
            private static Material OwnMaterial(string path, Color colour, float smoothness, bool clear)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null) return m;
                EnsureFolder(MaterialFolder);
                m = new Material(Shader.Find(LitShader)) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                m.SetColor("_BaseColor", colour);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", 0f);
                if (clear)
                {
                    // URP Lit, Transparent (alpha blend), drawn after the opaque things, writes no depth
                    m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                AssetDatabase.CreateAsset(m, path);
                return m;
            }

            private static void EnsureFolder(string folder)
            {
                if (AssetDatabase.IsValidFolder(folder)) return;
                string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
            }

            // -------------------------------------------------------------------------------------------- the bodies and the frame

            private void Bodies()
            {
                var cc = _player.GetComponent<CharacterController>();
                ShiftSettings shift = null; MovementSettings move = null; float fatal = 14f, kill = -200f;
                var shifter = _player.GetComponent<Shifter>();
                if (shifter != null) shift = new SerializedObject(shifter).FindProperty("settings")?.objectReferenceValue as ShiftSettings;
                var motor = _player.GetComponent<PlayerMotor>();
                if (motor != null) move = new SerializedObject(motor).FindProperty("settings")?.objectReferenceValue as MovementSettings;
                var respawn = _player.GetComponent<Respawn>();
                if (respawn != null)
                {
                    var so = new SerializedObject(respawn);
                    SerializedProperty f = so.FindProperty("fatalFallHeight"), k = so.FindProperty("killHeight");
                    if (f != null) fatal = f.floatValue;
                    if (k != null) kill = k.floatValue;
                }
                float toy = shift != null ? shift.toyScale : 1f, kid = shift != null ? shift.kidScale : 35f, jump = move != null ? move.jumpHeight : 2f;
                PlayerMoveSettings moves = PlayerMoveSettings.Load();
                float crouch = moves != null ? moves.crouchHeight : 0.833f;
                _toyR = (cc.radius + cc.skinWidth) * toy; _toyH = cc.height * toy;
                _kidR = cc.radius * kid; _kidH = cc.height * kid; _kidCrouchH = cc.height * crouch * kid;
                _toyJump = jump * toy; _kidJump = jump * kid;
                _kidGravity = (move != null ? move.gravity : 25f) * kid; _kidSprint = (move != null ? move.sprintSpeed : 11f) * kid;
                _fatalDrop = fatal * toy; _killHeight = kill;
                _log.AppendLine($"BODIES (read from the Player): the toy {_toyH:F1} tall, {2f * _toyR:F2} across with the skin, jumps {_toyJump:F1}; the kid {_kidH:F0} tall ({_kidCrouchH:F1} crouched), {2f * _kidR:F0} across, jumps {_kidJump:F0}. Fall rule: a toy landing as fast as a free fall of {_fatalDrop:F0} goes back (so every drop on the toy's way is kept under {_fatalDrop - _toyJump:F0}, a jump on top of it included); the kid never. Kill height y {_killHeight:F0} (the Player prefab's; the script's default is -20).");
            }

            private void Frame()
            {
                Bounds sb = RenderBox(_sill.gameObject), wb = RenderBox(_window.gameObject);
                Vector3 outward = _window.position - sb.center; outward.y = 0f;
                if (outward.sqrMagnitude < 1e-4f) outward = -_window.forward;
                _fwd = outward.normalized;
                _right = Vector3.Cross(Vector3.up, _fwd).normalized;
                _turn = Quaternion.LookRotation(_fwd, Vector3.up);
                // the house's outer wall: a ray back at it from the garden, over the window's top
                float face = 0f; bool hitWall = false;
                Vector3 from = _window.position + Vector3.up * (wb.max.y + 8f - _window.position.y) + _fwd * 60f;
                foreach (RaycastHit h in Physics.RaycastAll(from, -_fwd, 80f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.transform.IsChildOf(_r6)) continue;
                    float d = Vector3.Dot(h.point - _window.position, _fwd);
                    if (!hitWall || d > face) { face = d; hitWall = true; }
                }
                if (!hitWall)
                    foreach (Vector3 c in Corners(wb)) face = Mathf.Max(face, Vector3.Dot(c - _window.position, _fwd));
                Vector3 probe = _window.position + _fwd * (face + 30f);
                _groundY = GroundAt(probe, out bool onGround);
                _origin = new Vector3(_window.position.x, 0f, _window.position.z) + _fwd * face;
                _origin.y = _groundY;
                _log.AppendLine($"FRAME: out of the window is {F(_fwd)}; the house's outer wall {(hitWall ? "found by a ray" : "NOT found by a ray (the window's own depth used)")} {face:F2} out from the window's pivot; the lawn at y {_groundY:F2}{(onGround ? " (the Ground's top)" : " (NO Ground under the yard: the window's foot used)")}. The yard's origin (the wall at the window, on the lawn) {F(_origin)}; u across (+ = {F(_right)}), v out.");
            }

            private Vector3 P(float u, float v) => _origin + _right * u + _fwd * v;
            private Vector3 P(Vector2 uv) => P(uv.x, uv.y);
            private Vector2 UV(Vector3 world) { Vector3 d = world - _origin; return new Vector2(Vector3.Dot(d, _right), Vector3.Dot(d, _fwd)); }

            private float GroundAt(Vector3 at, out bool hit)
            {
                hit = _groundCol.Raycast(new Ray(new Vector3(at.x, at.y + 400f, at.z), Vector3.down), out RaycastHit h, 1200f);
                return hit ? h.point.y : _groundY;
            }

            private float GroundAt(Vector3 at) => GroundAt(at, out _);

            private void Layout()
            {
                _halfW = _s.yardWidth * 0.5f;
                _wallV = _s.yardDepth;
                _cell = _s.mazePathWidth + _s.mazeWallThickness;
                _mazeW = _s.mazeColumns * _cell;
                _m0 = _s.landingDepth;
                _m1 = _m0 + _s.mazeRows * _cell;
                _pondR = _s.pondDiameter * 0.5f;
                _pondV = _m1 + _s.pondGap + _pondR;
                _bedV = _pondV + _pondR + _s.flowerbedPlace.y;
                _bed = new Vector2(_s.flowerbedPlace.x, _bedV);
                _corePos = new Vector2(_s.corePlace.x, _wallV - _s.corePlace.y);
                // the boundary rows stand with their backs on the side lines (u = -+half the yard), the seals right behind
                // them; inside, the deepest piece (at its largest height) is kept clear of
                float sideDepth = 0f;
                foreach (GameObject p in _s.boundary.prefabs ?? new GameObject[0])
                {
                    if (p == null) continue;
                    Bounds b = PrefabBox(p);
                    float k = Mathf.Max(_s.boundaryHeight.x, _s.boundaryHeight.y) / Mathf.Max(0.01f, b.max.y);
                    sideDepth = Mathf.Max(sideDepth, Depth(b) * k);
                }
                _inner = _halfW - sideDepth;
                _seal = _halfW + 1f;
                _farV = _wallV + _s.endDepth + 12f;
                bool fits = _bedV + _s.flowerbedSize.y * 0.5f < _wallV - 30f && _mazeW < 2f * _inner - 20f;
                _log.AppendLine($"LAYOUT (yard units from the wall at the window): yard {_s.yardWidth} across (u -{_halfW}..{_halfW}; {2f * _inner:F0} inside the side boundaries), back wall at v {_wallV}; landing lawn v 0..{_m0}; maze v {_m0}..{_m1}, u -{_mazeW * 0.5f:F0}..{_mazeW * 0.5f:F0}; pond centre v {_pondV:F0}, {_s.pondDiameter} across; flowerbed centre u {_bed.x:F0}, v {_bed.y:F0}; core u {_corePos.x:F0}, v {_corePos.y:F0}.{(fits ? "" : " WARNING: the beats do not fit in Yard Depth / Yard Width: make the yard bigger or the beats smaller.")}");
                // what scattered pieces keep out of
                float lane = _s.glideLaneWidth * 0.5f, path = _s.pathWidth * 0.5f;
                _keepOut.Add(R(-lane, 0f, lane, _m0));                                   // the glide's way
                _keepOut.Add(R(-path, 0f, path, _wallV));                                // the path
                _keepOut.Add(R(-_halfW, _m0 - 10f, _halfW, _m1 + 10f));                  // the maze and its hedges
                _keepOut.Add(R(-_halfW, _pondV - _pondR - 12f, _halfW, _pondV + _pondR + 12f));   // the pond and its hedges
                _keepOut.Add(R(_bed.x - _s.flowerbedSize.x * 0.5f - 6f, _bed.y - _s.flowerbedSize.y * 0.5f - 6f, _bed.x + _s.flowerbedSize.x * 0.5f + 6f, _bed.y + _s.flowerbedSize.y * 0.5f + 6f));
                _keepOut.Add(R(_corePos.x - 16f, _corePos.y - 16f, _corePos.x + 16f, _corePos.y + 16f));
                _keepOut.Add(R(-_halfW, _wallV - 28f, _halfW, _wallV + _s.endDepth + 20f));   // the wall and the end
                _keepOut.Add(R(-_halfW, -10f, _halfW, 12f));                             // along the house
            }

            private static Rect R(float u0, float v0, float u1, float v1) => Rect.MinMaxRect(Mathf.Min(u0, u1), Mathf.Min(v0, v1), Mathf.Max(u0, u1), Mathf.Max(v0, v1));

            private bool Free(Vector2 uv, float pad)
            {
                if (Mathf.Abs(uv.x) > _inner - pad) return false;
                foreach (Rect r in _keepOut)
                    if (uv.x > r.xMin - pad && uv.x < r.xMax + pad && uv.y > r.yMin - pad && uv.y < r.yMax + pad) return false;
                return true;
            }

            // -------------------------------------------------------------------------------------------- the sill and the window

            private void Sill()
            {
                GameObject oldGoal = _r6.Find("Goal") != null ? _r6.Find("Goal").gameObject : null;
                if (oldGoal != null)
                {
                    bool was = oldGoal.activeSelf;
                    oldGoal.SetActive(false);
                    EditorUtility.SetDirty(oldGoal);
                    _log.AppendLine($"SILL: the old Goal {Path(oldGoal.transform)} switched OFF (kept, not deleted; it {(was ? "was on" : "was already off")}). Its GameObject, not its script: Unity sends trigger messages to a switched-off script, and the game would still end on the sill.");
                }
                else _log.AppendLine("SILL: no old Goal under R6_Window (nothing to switch off).");
            }

            private void Window()
            {
                Transform wt = _wmf.transform;
                Mesh mesh = _wmf.sharedMesh;
                Bounds mb = mesh.bounds;
                Material[] mats = _wmr.sharedMaterials;
                int glass = -1;
                for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0) { glass = i; break; }
                Bounds gb = glass >= 0 && glass < mesh.subMeshCount ? mesh.GetSubMesh(glass).bounds : new Bounds(mb.center, Vector3.Scale(mb.size, new Vector3(0.75f, 0.8f, 0.1f)));
                // "in" = toward the room: the window's own +z or -z, whichever faces the room
                float sz = Vector3.Dot(wt.forward, -_fwd) >= 0f ? 1f : -1f;
                float inMin = Mathf.Min(mb.min.z * sz, mb.max.z * sz), inMax = Mathf.Max(mb.min.z * sz, mb.max.z * sz);
                float gIn0 = Mathf.Min(gb.min.z * sz, gb.max.z * sz), gIn1 = Mathf.Max(gb.min.z * sz, gb.max.z * sz);
                float probe = gIn0 - 0.5f;   // just past the glass on the garden side: inside the sash, not in the glass
                Vector3 L(float x, float y, float zin) => new Vector3(x, y, zin * sz);
                float Ray(Vector3 local, Vector3 dirLocal, float far)
                {
                    Vector3 dir = wt.TransformDirection(dirLocal).normalized;
                    float per = wt.TransformVector(dirLocal.normalized).magnitude;
                    return _wmc.Raycast(new Ray(wt.TransformPoint(local), dir), out RaycastHit h, far * per) ? h.distance / per : -1f;
                }
                float xa = gb.min.x + 0.25f * gb.size.x, xb = gb.max.x - 0.25f * gb.size.x, yMid = gb.min.y + 0.3f * gb.size.y;
                float d;
                d = Ray(L(xa, yMid, probe), Vector3.down, yMid - mb.min.y + 1f);
                float sashTop = d >= 0f ? yMid - d : gb.min.y;
                d = Ray(L(xa, sashTop - 0.6f, inMin - 1f), new Vector3(0f, 0f, sz), gIn1 - inMin + 2f);
                float sashOuter = d >= 0f ? inMin - 1f + d : gIn0;
                d = Ray(L(xa, sashTop + 2f, inMax - 0.4f), Vector3.down, sashTop + 2f - mb.min.y + 1f);
                float ledgeTop = d >= 0f ? sashTop + 2f - d : mb.min.y;
                float railFront = gIn1 + 0.5f;
                for (float z = inMax - 0.4f; z > probe; z -= 0.1f)
                {
                    d = Ray(L(xa, sashTop + 2f, z), Vector3.down, sashTop + 2f - mb.min.y + 1f);
                    if (d >= 0f && sashTop + 2f - d > ledgeTop + 0.5f) { railFront = z + 0.1f; break; }
                }
                d = Ray(L(xa, sashTop - 0.6f, railFront + 1f), new Vector3(0f, 0f, -sz), railFront + 1f - gIn0 + 1f);
                float sashInner = d >= 0f ? railFront + 1f - d : gIn1;
                d = Ray(L(xa, sashTop + 2f, Mathf.Max(inMin + 0.3f, sashOuter - 0.6f)), Vector3.down, sashTop + 2f - mb.min.y + 1f);
                float railTop = d >= 0f ? sashTop + 2f - d : sashTop;
                float y3 = sashTop + 3f;
                d = Ray(L(xa, y3, probe), Vector3.left, xa - mb.min.x + 1f); float leftInner = d >= 0f ? xa - d : gb.min.x;
                d = Ray(L(xa, y3, probe), Vector3.right, mb.max.x - xa + 1f); float mullionL = d >= 0f ? xa + d : gb.max.x;
                d = Ray(L(xb, y3, probe), Vector3.right, mb.max.x - xb + 1f); float rightInner = d >= 0f ? xb + d : gb.max.x;
                d = Ray(L(xb, y3, probe), Vector3.left, xb - mb.min.x + 1f); float mullionR = d >= 0f ? xb - d : gb.min.x;
                bool mullion = mullionR - mullionL > 0.2f && mullionL > xa && mullionR < xb;
                d = Ray(L(xa, sashTop + 0.5f, probe), Vector3.up, mb.max.y - sashTop + 1f);
                float lowerTop = d >= 0f ? sashTop + 0.5f + d : gb.max.y;
                float transomTop = lowerTop;
                if (lowerTop < gb.max.y - 1f)
                {
                    d = Ray(L(xa, gb.max.y - 0.3f, probe), Vector3.down, gb.max.y - lowerTop);
                    if (d >= 0f && gb.max.y - 0.3f - d > lowerTop + 0.1f) transomTop = gb.max.y - 0.3f - d;
                }
                bool transom = transomTop > lowerTop + 0.1f;
                d = Ray(L(xa, transomTop + 0.3f, probe), Vector3.up, mb.max.y - transomTop + 1f);
                float topUnder = d >= 0f ? transomTop + 0.3f + d : gb.max.y;
                if (!transom) topUnder = lowerTop;
                float W(float y) => wt.TransformPoint(new Vector3(0f, y, 0f)).y;
                _log.AppendLine($"WINDOW: {Path(_window)} is one mesh ({mesh.subMeshCount} parts; the glass is material {glass} '{(glass >= 0 ? mats[glass].name : "NONE FOUND")}'): no separate sash, so the glass goes. Measured on its own collider (its space; world heights in brackets): its ledge into the room top {ledgeTop:F2} ({W(ledgeTop):F1}); the rail's top {railTop:F2} ({W(railTop):F1}); the sash's bottom bar top {sashTop:F2} ({W(sashTop):F1}), from {sashOuter:F2} to {sashInner:F2} deep; the rail's front at {railFront:F2}; the panes x {leftInner:F2}..{(mullion ? mullionL.ToString("F2") + " | " + mullionR.ToString("F2") + ".." : "")}{rightInner:F2}{(mullion ? " (a mullion between)" : " (NO mullion found)")}; the lower panes up to {lowerTop:F2}{(transom ? $", a transom to {transomTop:F2}" : " (NO transom found)")}, the top's underside {topUnder:F2}. A lower pane is {(mullion ? mullionL - leftInner : rightInner - leftInner):F1} x {lowerTop - sashTop:F1}: the toy ({2f * _toyR:F2} x {_toyH:F1}) fits; the kid ({2f * _kidR:F0} x {_kidH:F0}, {_kidCrouchH:F1} crouched) does not fit any opening of this window, so the kid never goes out of it.");

                // ---- the opening: the glass fades, the window's own collider goes, the frame's boxes come
                var holder = new GameObject("Window").transform;
                holder.SetParent(_root, false);
                holder.SetPositionAndRotation(wt.position, wt.rotation);
                _frame = new GameObject("Frame");
                _frame.transform.SetParent(holder, false);
                _frame.transform.localScale = new Vector3(Mathf.Abs(wt.lossyScale.x), Mathf.Abs(wt.lossyScale.y), Mathf.Abs(wt.lossyScale.z));
                int boxes = 0;
                void Box(string n, float x0, float x1, float y0, float y1, float z0, float z1)
                {
                    if (x1 - x0 < 0.01f || y1 - y0 < 0.01f || z1 - z0 < 0.01f) return;
                    var go = new GameObject(n); go.transform.SetParent(_frame.transform, false);
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f * sz);
                    bc.size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
                    boxes++;
                }
                Box("Ledge", mb.min.x, mb.max.x, mb.min.y, ledgeTop, railFront, inMax);
                Box("Rail", mb.min.x, mb.max.x, mb.min.y, railTop, inMin, railFront);
                Box("Sash Sill", leftInner, rightInner, railTop, sashTop, sashOuter, sashInner);
                Box("Stile L", mb.min.x, leftInner, railTop, mb.max.y, inMin, railFront);
                Box("Stile R", rightInner, mb.max.x, railTop, mb.max.y, inMin, railFront);
                Box("Top", leftInner, rightInner, topUnder, mb.max.y, inMin, railFront);
                if (transom) Box("Transom", leftInner, rightInner, lowerTop, transomTop, inMin, railFront);
                if (mullion) Box("Mullion", mullionL, mullionR, sashTop, lowerTop, inMin, railFront);
                _opening = holder.gameObject.AddComponent<WindowOpening>();
                var oso = new SerializedObject(_opening);
                oso.FindProperty("window").objectReferenceValue = _wmr;
                oso.FindProperty("closedCollider").objectReferenceValue = _wmc;
                oso.FindProperty("glassSlot").intValue = Mathf.Max(0, glass);
                oso.FindProperty("frame").objectReferenceValue = _frame;
                oso.ApplyModifiedPropertiesWithoutUndo();
                _log.AppendLine($"WINDOW OPENS: {Path(holder)} (WindowOpening): closed, the window's own collider blocks (as before); open, it goes off and {boxes} boxes under Frame take over (ledge, rail, the sash's bottom bar, two stiles, the top{(transom ? ", the transom" : "")}{(mullion ? ", the mullion" : "")}), and the glass fades out over {_s.glassFadeSeconds} s{(glass < 0 ? " (NO glass material found: nothing fades)" : "")}. {(_s.openOnSill ? "It opens when the sill's checkpoint is claimed." : "Open On Sill is UNTICKED: it opens as the scene starts.")}");

                // ---- the toy's way up into a lower pane: a step on the ledge, a ramp to it (the pane away from the core)
                float paneA0 = leftInner, paneA1 = mullion ? mullionL : rightInner, paneB0 = mullion ? mullionR : leftInner, paneB1 = rightInner;
                Vector3 coreAt = _coreD.position;
                float da = Vector3.Distance(wt.TransformPoint(L((paneA0 + paneA1) * 0.5f, ledgeTop, inMax)), coreAt);
                float db = Vector3.Distance(wt.TransformPoint(L((paneB0 + paneB1) * 0.5f, ledgeTop, inMax)), coreAt);
                bool useA = da >= db;
                float p0 = useA ? paneA0 : paneB0, p1 = useA ? paneA1 : paneB1;
                float sign = useA ? -1f : 1f;                 // the ramp runs from the step toward this end of the window
                float inside = useA ? p1 : p0;                // the pane's edge by the mullion (the step stands there)
                float stepW = Mathf.Min(6f, (p1 - p0) * 0.45f), stepDepth = Mathf.Clamp(inMax - railFront - 1.2f, 1.5f, 3f);
                float sx0 = useA ? inside - 0.3f - stepW : inside + 0.3f, sx1 = sx0 + stepW;
                float rise = sashTop - ledgeTop;
                float run = rise / Mathf.Tan(_s.rampSlope * Mathf.Deg2Rad);
                float rampHigh = useA ? sx0 : sx1, rampLow = rampHigh + sign * run;
                float end = useA ? mb.min.x + 0.5f : mb.max.x - 0.5f;
                if (useA ? rampLow < end : rampLow > end) { rampLow = end; run = Mathf.Abs(rampLow - rampHigh); }
                // the step: from the ledge to the sash's top, from the sash's inner face out over the ledge
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "Window Step";
                step.transform.SetParent(_root, false);
                Renderer sillLook = _sill.GetComponent<Renderer>();
                if (sillLook != null) step.GetComponent<Renderer>().sharedMaterial = sillLook.sharedMaterial;
                Vector3 sc = L((sx0 + sx1) * 0.5f, (ledgeTop + sashTop) * 0.5f, (sashInner + railFront + stepDepth) * 0.5f);
                step.transform.SetPositionAndRotation(wt.TransformPoint(sc), wt.rotation);
                step.transform.localScale = Vector3.Scale(new Vector3(stepW, rise, railFront + stepDepth - sashInner), new Vector3(Mathf.Abs(wt.lossyScale.x), Mathf.Abs(wt.lossyScale.y), Mathf.Abs(wt.lossyScale.z)));
                Note("window step", step);
                // the ramp: its top surface from the ledge up to the step's top edge, along the window
                Vector3 lowTop = wt.TransformPoint(L(rampLow, ledgeTop, railFront + stepDepth * 0.5f));
                Vector3 highTop = wt.TransformPoint(L(rampHigh, sashTop, railFront + stepDepth * 0.5f));
                GameObject ramp = Plank(First(_s.windowRamp), "Window Ramp", lowTop, highTop, stepDepth);
                if (ramp != null) Note("window ramp", ramp);
                // the sill's checkpoint point: on the ledge's front strip at the window's middle, before the step
                float front = inMax - 1.3f, foot = rampLow + sign * 1.0f;
                _sillPoint = wt.TransformPoint(L(0f, ledgeTop, front)) + Vector3.up * 0.2f;
                // the toy's walk out, for the check: along the ledge's front past the step, round to the ramp's foot, up the
                // ramp, over the step, the sash, the rail and off its edge
                float px = (sx0 + sx1) * 0.5f;
                _windowWalk.Add(wt.TransformPoint(L(foot, ledgeTop, front)));
                _windowWalk.Add(wt.TransformPoint(L(foot, ledgeTop, railFront + stepDepth * 0.5f)));
                _windowWalk.Add(lowTop + (highTop - lowTop).normalized * 0.6f);
                _windowWalk.Add(highTop - (highTop - lowTop).normalized * 0.6f);
                _windowWalk.Add(wt.TransformPoint(L(px, sashTop, railFront + stepDepth * 0.5f)));
                _windowWalk.Add(wt.TransformPoint(L(px, sashTop, (sashOuter + sashInner) * 0.5f)));
                _windowWalk.Add(wt.TransformPoint(L(px, railTop, (inMin + sashOuter) * 0.5f)));
                _windowWalk.Add(wt.TransformPoint(L(px, railTop, inMin + 0.3f)));
                _log.AppendLine($"WINDOW WAY UP (the pane {(useA ? "A" : "B")} x {p0:F1}..{p1:F1}, the one away from core D): Window Step (a block in the sill's wood) {stepW:F1} wide, from the ledge ({W(ledgeTop):F1}) to the sash's top ({W(sashTop):F1}); Window Ramp ({(ramp != null ? ramp.name + " from " + (First(_s.windowRamp) != null ? First(_s.windowRamp).name : "?") : "NONE: no prefab on Window Ramp")}) from {F(lowTop)} up to {F(highTop)}: {run:F1} long, rising {rise:F1} ({Mathf.Atan2(rise, Mathf.Max(0.01f, run)) * Mathf.Rad2Deg:F0} degrees). Out through the pane, the toy steps down {sashTop - railTop:F1} onto the rail and off its edge {W(railTop) - _groundY:F1} over the lawn.");
                SparePlane(wt, L, ledgeTop, railFront, inMax, mb, sign);
            }

            /// <summary>A plank (a ruler) whose TOP surface runs from <paramref name="low"/> to <paramref name="high"/>,
            /// <paramref name="width"/> across: its long side stretched to fit, its width scaled, its thickness kept.</summary>
            private GameObject Plank(GameObject prefab, string name, Vector3 low, Vector3 high, float width)
            {
                if (prefab == null) return null;
                Bounds pb = PrefabBox(prefab);
                bool alongX = pb.size.x >= pb.size.z;
                float length = Vector3.Distance(low, high), thick = Mathf.Max(0.01f, pb.size.y);
                float ls = length / Mathf.Max(0.01f, alongX ? pb.size.x : pb.size.z), ws = width / Mathf.Max(0.01f, alongX ? pb.size.z : pb.size.x);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _root);
                go.name = name;
                go.transform.localScale = alongX ? new Vector3(ls, 1f, ws) : new Vector3(ws, 1f, ls);
                Vector3 dir = (high - low).normalized;
                Vector3 flat = new Vector3(dir.x, 0f, dir.z);
                Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
                go.transform.rotation = look * (alongX ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity);
                Vector3 up = look * Vector3.up;
                Vector3 want = (low + high) * 0.5f - up * (thick * 0.5f);
                go.transform.position += want - RenderBox(go).center;
                return go;
            }

            /// <summary>The second paper plane: on the window's ledge, on clear ground away from the core and the step.</summary>
            private void SparePlane(Transform wt, Func<float, float, float, Vector3> L, float ledgeTop, float railFront, float inMax, Bounds mb, float side)
            {
                if (!_s.sparePlane) { _log.AppendLine("SPARE PLANE: Spare Plane is unticked: none."); return; }
                GlideSettings glide = GlideSettings.Load();
                if (glide == null || glide.planePrefab == null) { _log.AppendLine("SPARE PLANE: NOT placed: no Resources/Glide or no plane prefab on it."); return; }
                Physics.SyncTransforms();
                var plane = (GameObject)PrefabUtility.InstantiatePrefab(glide.planePrefab);
                plane.name = "PaperPlane";
                foreach (Collider c in plane.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                plane.transform.rotation = Quaternion.LookRotation(_right, Vector3.up) * Quaternion.Euler(glide.planeEuler);   // nose along the sill
                Bounds pb = RenderBox(plane);
                plane.transform.localScale = Vector3.one * (glide.planeLength / Mathf.Max(0.01f, Mathf.Max(pb.size.x, pb.size.z)));
                pb = RenderBox(plane);
                Vector3 half = new Vector3(pb.extents.x + 0.4f, 1.5f, pb.extents.z + 0.4f);
                Vector3? spot = null; string why = "";
                var tries = new List<Vector3>();
                for (float x = mb.min.x + 2f; x <= mb.max.x - 2f; x += 1f)
                    for (float z = inMax - 0.7f; z >= railFront + 0.6f; z -= 0.5f) tries.Add(wt.TransformPoint(L(x, ledgeTop, z)));
                Vector3 want = wt.TransformPoint(L(side * 0.72f * mb.max.x, ledgeTop, inMax - 0.7f));   // by the ramp's foot: the toy walks past it
                tries.Sort((a, b) => (a - want).sqrMagnitude.CompareTo((b - want).sqrMagnitude));
                foreach (Vector3 p in tries)
                {
                    if (Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(_coreD.position.x, 0f, _coreD.position.z)) < 7f) continue;
                    if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit h, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!h.transform.IsChildOf(_window) || Mathf.Abs(h.point.y - p.y) > 0.3f) continue;   // the ledge itself, not a thing on it
                    Collider[] inWay = Physics.OverlapBox(new Vector3(p.x, h.point.y + half.y + 0.1f, p.z), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    bool clear = true;
                    foreach (Collider c in inWay) if (!c.transform.IsChildOf(plane.transform)) { clear = false; why = c.name; break; }
                    if (!clear) continue;
                    spot = new Vector3(p.x, h.point.y, p.z); break;
                }
                Vector3 at = spot ?? want;
                plane.transform.position += new Vector3(at.x - pb.center.x, at.y + 0.02f - pb.min.y, at.z - pb.center.z);
                pb = RenderBox(plane);
                var root = new GameObject("Glider_Sill").transform;
                root.SetParent(_root, false);
                root.position = pb.center;
                var box = root.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(pb.size.x + 1f, Mathf.Max(3f, pb.size.y + 2f), pb.size.z + 1f);
                var display = new GameObject("Display").transform;
                display.SetParent(root, false);
                plane.transform.SetParent(display, true);
                var pickup = root.gameObject.AddComponent<GliderPickup>();
                var pso = new SerializedObject(pickup);
                pso.FindProperty("display").objectReferenceValue = display.gameObject;
                pso.ApplyModifiedPropertiesWithoutUndo();
                Note("spare plane", root.gameObject);
                _log.AppendLine($"SPARE PLANE: {Path(root)} (GliderPickup, its own default words) on the window's ledge at {F(pb.center)}, {Vector3.Distance(pb.center, _coreD.position):F1} from core D{(spot == null ? " (NO CLEAR SPOT found: placed at the ledge's middle; last in the way: " + why + ")" : "")}. A toy that reaches the sill without a plane (it was not taken, or a Continue: the plane is not saved) takes this one; its model hides while the player has a plane.");
            }

            // -------------------------------------------------------------------------------------------- the boundaries

            private void Boundaries()
            {
                var holder = new GameObject("Boundaries").transform;
                holder.SetParent(_root, false);
                // the sides: rows of the blocky boundary pieces from the house to the back wall, sealed just outside by an
                // invisible wall (the toy cannot squeeze between two pieces, nobody jumps out)
                float sideTop = float.MaxValue, outer = _halfW;
                int pieces = 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    var row = new GameObject(side < 0 ? "Side W" : "Side E").transform;
                    row.SetParent(holder, false);
                    if (_s.boundary.prefabs == null || _s.boundary.prefabs.Length == 0) continue;
                    float spacing = Mathf.Max(4f, _s.boundary.spacing), len = _wallV - 8f;
                    int n = Mathf.Max(2, Mathf.CeilToInt(len / spacing) + 1);
                    for (int i = 0; i < n; i++)
                    {
                        GameObject p = Pick(_s.boundary); if (p == null) break;
                        Bounds pb = PrefabBox(p);
                        float tall = Mathf.Lerp(_s.boundaryHeight.x, _s.boundaryHeight.y, (float)_rng.NextDouble());
                        float k = tall / Mathf.Max(0.01f, pb.max.y);
                        // a flat cutout shows its face to the yard; a round piece any way round
                        Quaternion turn = IsCutout(pb)
                            ? Quaternion.LookRotation(_right * (_rng.Next(2) == 0 ? 1f : -1f), Vector3.up) * Quaternion.Euler(0f, ((float)_rng.NextDouble() - 0.5f) * 16f, 0f)
                            : Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
                        Vector3 at = P(side * _halfW, -2f + len * i / (n - 1));
                        GameObject g = Spawn(p, p.name, row, new Vector3(at.x, GroundAt(at), at.z), turn, k);
                        float rear = float.MinValue;
                        foreach (Vector3 c in Corners(RenderBox(g))) rear = Mathf.Max(rear, side * UV(c).x);
                        g.transform.position += _right * (side * (_halfW - rear));   // its back on the side line
                        // never through the house's wall or the back wall (a wide cutout at either end is pushed in)
                        float vMin = float.MaxValue, vMax = float.MinValue;
                        foreach (Vector3 c in Corners(RenderBox(g))) { float v = UV(c).y; vMin = Mathf.Min(vMin, v); vMax = Mathf.Max(vMax, v); }
                        if (vMin < 0.5f) g.transform.position += _fwd * (0.5f - vMin);
                        else if (vMax > _wallV - 4f) g.transform.position -= _fwd * (vMax - (_wallV - 4f));
                        Bounds b = RenderBox(g);
                        sideTop = Mathf.Min(sideTop, b.max.y);
                        foreach (Vector3 c in Corners(b)) outer = Mathf.Max(outer, Mathf.Abs(UV(c).x));
                        Note("boundary", g); pieces++;   // the boundary itself, sealed behind: not a step to jump out from
                    }
                }
                if (sideTop < float.MaxValue) _boundaryTops.Add(("the side rows", sideTop - _groundY));
                // the seals: just outside the rows, from the house to behind the end, and across behind the end
                var seals = new GameObject("Seals").transform;
                seals.SetParent(holder, false);
                float sealTop = _s.capHeight > 0f ? _s.capHeight : (sideTop < float.MaxValue ? sideTop - _groundY + 10f : 150f);
                int layer = Layers.CarGuard;
                Wall(seals, "Seal W", -_seal - 10f, -8f, -_seal, _farV + 10f, -3f, sealTop, layer);
                Wall(seals, "Seal E", _seal, -8f, _seal + 10f, _farV + 10f, -3f, sealTop, layer);
                Wall(seals, "Seal End", -_seal - 10f, _farV, _seal + 10f, _farV + 10f, -3f, sealTop, layer);
                Bounds lawn = _groundCol.bounds;
                var corners = new[] { P(-_seal - 10f, -8f), P(_seal + 10f, -8f), P(-_seal - 10f, _farV + 10f), P(_seal + 10f, _farV + 10f) };
                bool inside = true;
                foreach (Vector3 c in corners) if (c.x < lawn.min.x || c.x > lawn.max.x || c.z < lawn.min.z || c.z > lawn.max.z) inside = false;
                _log.AppendLine($"BOUNDARY: {pieces} blocky pieces in two side rows from the house to the back wall (each {_s.boundaryHeight.x:F0} to {_s.boundaryHeight.y:F0} tall; the lowest top {(sideTop < float.MaxValue ? (sideTop - _groundY).ToString("F0") : "?")}; the kid jumps {_kidJump:F0}), reaching {outer - _halfW:F1} outside the side lines. SEALS: three invisible walls on {(layer >= 0 ? "the CarGuard layer (bodies bump; the camera, shots and sight pass)" : "Default (NO CarGuard layer: the camera stops on them)")}, up to {sealTop:F0}: just outside each row (u {_seal:F1}) from the house's wall to behind the end, and across behind the end (v {_farV:F0}). The yard and its end, walls included, lie {(inside ? "INSIDE the Ground (x " + lawn.min.x.ToString("F0") + ".." + lawn.max.x.ToString("F0") + ", z " + lawn.min.z.ToString("F0") + ".." + lawn.max.z.ToString("F0") + "): nobody reaches its edge" : "PARTLY OUTSIDE the Ground: its edge can be reached (shrink the yard)")}.");
                // the back wall: the gate in the middle, sections either side of it, one box per slat
                var back = new GameObject("Back Wall").transform;
                back.SetParent(holder, false);
                GameObject gatePrefab = First(_s.gate);
                float gateHalf = 0f;
                GameObject gate = null;
                if (gatePrefab != null)
                {
                    gate = Spawn(gatePrefab, "Gate", back, P(0f, _wallV), LongTurn(gatePrefab, _right), Mathf.Lerp(_s.gate.scaleMin, _s.gate.scaleMax, 0.5f));
                    Bounds gb = RenderBox(gate);
                    float uMid = UV(gb.center).x;
                    gate.transform.position += P(0f, _wallV) - P(uMid, UV(gb.center).y) + Vector3.up * (GroundAt(gate.transform.position) - RenderBox(gate).min.y);
                    gateHalf = 0.5f * Along(RenderBox(gate), _right);
                    Note("back wall", gate);
                }
                float sectionTop = float.MaxValue;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 from = P(side * (gateHalf > 0f ? gateHalf : 0f), _wallV), to = P(side * _seal, _wallV);
                    foreach (GameObject g in LayRun(_s.backWall, side < 0 ? "Wall W" : "Wall E", back, from, to, true))
                    {
                        Slatted(g);
                        sectionTop = Mathf.Min(sectionTop, RenderBox(g).max.y);
                        Note("back wall", g);
                    }
                }
                if (sectionTop < float.MaxValue) _boundaryTops.Add(("the back wall's sections", sectionTop - _groundY));
                GatePanel(gate, back);
                Caps(holder);
            }

            /// <summary>An invisible wall (a box square to the yard) from (u0, v0) to (u1, v1), from y0 to y1 over the lawn.</summary>
            private void Wall(Transform parent, string name, float u0, float v0, float u1, float v1, float y0, float y1, int layer)
            {
                if (y1 <= y0) return;
                var go = new GameObject(name) { layer = layer >= 0 ? layer : 0 };
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(P((u0 + u1) * 0.5f, (v0 + v1) * 0.5f) + Vector3.up * ((y0 + y1) * 0.5f), _turn);
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(Mathf.Abs(u1 - u0), y1 - y0, Mathf.Abs(v1 - v0));
            }

            /// <summary>A flat cutout (the playroom's toy trees and bushes): much thinner front to back than across.</summary>
            private static bool IsCutout(Bounds b) => b.size.z < 0.35f * b.size.x;

            /// <summary>How deep a piece stands across a row's line: a cutout turned face on shows its thin side; anything else
            /// may be turned any way.</summary>
            private static float Depth(Bounds b) => IsCutout(b) ? b.size.z : Mathf.Max(b.size.x, b.size.z);

            /// <summary>Pieces of a role laid end to end from <paramref name="from"/> to <paramref name="to"/> (the longest that
            /// fits first; the last one stretched along its length to end there), on the lawn, their long side along the run.</summary>
            private List<GameObject> LayRun(BackyardSettings.Role role, string name, Transform parent, Vector3 from, Vector3 to, bool longestFirst)
            {
                var made = new List<GameObject>();
                if (role.prefabs == null || role.prefabs.Length == 0) { _log.AppendLine($"RUN {name}: NO PREFABS on its list: nothing laid."); return made; }
                Vector3 dir = to - from; dir.y = 0f;
                float total = dir.magnitude;
                if (total < 0.5f) return made;
                dir /= total;
                float scale = Mathf.Lerp(role.scaleMin, role.scaleMax, 0.5f);
                var lengths = new List<(GameObject p, float len)>();
                foreach (GameObject p in role.prefabs)
                {
                    if (p == null) continue;
                    Bounds b = PrefabBox(p);
                    lengths.Add((p, Mathf.Max(b.size.x, b.size.z) * scale));
                }
                if (lengths.Count == 0) return made;
                if (longestFirst) lengths.Sort((a, b) => b.len.CompareTo(a.len));
                float at = 0f; int n = 0, pick = 0;
                while (at < total - 0.5f && n < 64)
                {
                    float left = total - at;
                    (GameObject p, float len) choice = lengths[pick % lengths.Count];
                    if (longestFirst)
                    {
                        choice = lengths[lengths.Count - 1];
                        foreach (var c in lengths) if (c.len <= left + 0.5f) { choice = c; break; }
                    }
                    pick++;
                    GameObject g = Spawn(choice.p, name + " " + (++n), parent, from + dir * at, LongTurn(choice.p, dir), scale);
                    float len = Along(RenderBox(g), dir);
                    if (len > left + 0.5f || (left - len < 0.25f * len && left - len > 0.5f && at + len < total))
                    {
                        // the last piece: stretched or shrunk along its length to end exactly at the run's end
                        float k = left / Mathf.Max(0.01f, len);
                        Vector3 ls = g.transform.localScale;
                        bool lx = LongAxisIsX(choice.p);
                        g.transform.localScale = lx ? new Vector3(ls.x * k, ls.y, ls.z) : new Vector3(ls.x, ls.y, ls.z * k);
                        len = Along(RenderBox(g), dir);
                    }
                    // its start on the run's line, its middle on the line across, its foot on the lawn
                    Bounds gb = RenderBox(g);
                    float start = float.MaxValue; foreach (Vector3 c in Corners(gb)) start = Mathf.Min(start, Vector3.Dot(c - from, dir));
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    float across = Vector3.Dot(gb.center - from, side);
                    g.transform.position += dir * (at - start) - side * across;
                    g.transform.position += Vector3.up * (GroundAt(g.transform.position) - g.transform.position.y);
                    made.Add(g);
                    at += len - 0.2f;
                }
                return made;
            }

            /// <summary>The piece's own box made its whole height and size (fitted to its meshes): the kid's jump does not get
            /// over the hedge's foliage, and it is solid to the toy, as it looks.</summary>
            private static void FitOwnBox(GameObject g)
            {
                Bounds local = LocalBox(g.transform);
                var box = g.GetComponent<BoxCollider>();
                if (box == null) box = g.AddComponent<BoxCollider>();
                box.center = local.center; box.size = local.size; box.enabled = true;
                foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) if (c != box && !c.isTrigger) c.enabled = false;
            }

            /// <summary>A slatted piece: its own colliders switched off (kept), one box per slat and rail under "Slats" (from its
            /// mesh seen face on). A mesh that cannot be read keeps its own collider (reported: the toy cannot slip through it).</summary>
            private int Slatted(GameObject g)
            {
                MeshFilter mf = g.GetComponentInChildren<MeshFilter>();
                List<Bounds> parts = mf != null ? Slats(mf.sharedMesh, out string why) : null;
                if (parts == null)
                {
                    _log.AppendLine($"SLATS: {Path(g.transform)} kept its own collider ({(mf == null ? "no mesh" : "its mesh could not be read or has too many parts")}): the toy cannot slip through it.");
                    return 0;
                }
                foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                var slats = new GameObject("Slats").transform;
                slats.SetParent(mf.transform, false);
                foreach (Bounds b in parts)
                {
                    var bc = slats.gameObject.AddComponent<BoxCollider>();
                    bc.center = b.center; bc.size = b.size;
                }
                return parts.Count;
            }

            /// <summary>The gate's open middle, filled with the Gate Panel: the panel's piece stretched to the opening's width
            /// and Gate Panel Height, as thick as the gate, its slats one box each; a child of the gate named "Gate Panel".</summary>
            private void GatePanel(GameObject gate, Transform back)
            {
                GameObject prefab = First(_s.gatePanel);
                if (gate == null || prefab == null) { _log.AppendLine($"GATE PANEL: NOT made ({(gate == null ? "no gate prefab" : "no panel prefab")})."); return; }
                MeshFilter gmf = gate.GetComponentInChildren<MeshFilter>();
                if (gmf == null || !Opening(gmf.sharedMesh, out float o0, out float o1, out bool gx, out float gTop))
                {
                    _log.AppendLine("GATE PANEL: NOT made: the gate's opening could not be measured (its mesh unreadable, or no gap in its middle).");
                    return;
                }
                Mesh gm = gmf.sharedMesh;
                Transform gt = gmf.transform;
                Vector3 a = gt.TransformPoint(gx ? new Vector3(o0, gm.bounds.min.y, gm.bounds.center.z) : new Vector3(gm.bounds.center.x, gm.bounds.min.y, o0));
                Vector3 b = gt.TransformPoint(gx ? new Vector3(o1, gm.bounds.min.y, gm.bounds.center.z) : new Vector3(gm.bounds.center.x, gm.bounds.min.y, o1));
                float width = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
                float openTop = gt.TransformPoint(new Vector3(0f, gTop, 0f)).y - _groundY;
                Bounds pb = PrefabBox(prefab);
                bool px = pb.size.x >= pb.size.z;
                float plen = px ? pb.size.x : pb.size.z, pthick = px ? pb.size.z : pb.size.x;
                float gateScale = Mathf.Abs(gt.lossyScale.y);
                float height = Mathf.Min(_s.gatePanelHeight, openTop);
                var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, back);
                panel.name = GatePanelName;
                panel.transform.rotation = LongTurn(prefab, _right);
                float ls = width / Mathf.Max(0.01f, plen), hs = height / Mathf.Max(0.01f, pb.size.y), ts = gateScale;
                panel.transform.localScale = px ? new Vector3(ls, hs, ts) : new Vector3(ts, hs, ls);
                Vector3 mid = (a + b) * 0.5f;
                Bounds now = RenderBox(panel);
                panel.transform.position += new Vector3(mid.x - now.center.x, GroundAt(mid) - now.min.y, mid.z - now.center.z);
                panel.transform.SetParent(gate.transform, true);
                int n = Slatted(panel);
                _gatePanel = panel.transform;
                Note("gate panel", panel);
                // the widest gap between its slats near the ground: where the route goes through, and the gap check
                MeshFilter pmf = panel.GetComponentInChildren<MeshFilter>();
                if (pmf != null && Gaps(pmf, out Vector3 gapMid, out float gapW, out float gapMin, out int gaps))
                {
                    _gapPoint = gapMid; _gapWidth = gapW;
                    _log.AppendLine($"GATE PANEL: {Path(panel.transform)} ({prefab.name}) fills the gate's opening {width:F1} wide (the opening is {openTop:F0} tall, open to the sky): {height:F0} tall, as thick as the gate, {n} boxes (one per slat and rail piece). Its gaps at the foot: {gaps}, {gapMin:F1} to {gapW:F1} wide (the toy is {2f * _toyR:F2} across, the kid {2f * _kidR:F0}).");
                }
                else _log.AppendLine($"GATE PANEL: {Path(panel.transform)} fills the opening ({width:F1} wide, {height:F0} tall), {n} boxes; NO gap measured at its foot.");
                _boundaryTops.Add(("the Gate Panel", RenderBox(panel).max.y - _groundY));
                // the back wall's own gaps (its first section), for the report
                foreach (GameObject g in Placed("back wall"))
                {
                    if (g == gate) continue;
                    MeshFilter wmf = g.GetComponentInChildren<MeshFilter>();
                    if (wmf != null && Gaps(wmf, out _, out float w, out float m, out int c))
                    { _log.AppendLine($"BACK WALL GAPS: {g.name} ({PrefabName(g)}): {c} gaps at the foot, {m:F1} to {w:F1} wide: the toy slips through any of them, the kid through none."); break; }
                }
            }

            /// <summary>Invisible walls on the CarGuard layer (the camera, shots and sight pass) over the back wall and the
            /// house's wall, up to Cap Height: a kid standing on furniture cannot jump over them. (The sides have their seals.)</summary>
            private void Caps(Transform holder)
            {
                if (_s.capHeight <= 0f) { _log.AppendLine("CAPS: Cap Height 0: none over the back wall and the house (a kid on furniture may jump over them)."); return; }
                int layer = Layers.CarGuard;
                var caps = new GameObject("Caps").transform;
                caps.SetParent(holder, false);
                float backLow = float.MaxValue;
                foreach (var t in _boundaryTops) if (t.name != "the side rows") backLow = Mathf.Min(backLow, t.top);
                if (backLow == float.MaxValue) backLow = 100f;
                float houseTop = 0f;
                foreach (RaycastHit h in Physics.RaycastAll(P(_inner * 0.5f, -2.5f) + Vector3.up * 600f, Vector3.down, 700f, ~0, QueryTriggerInteraction.Ignore))
                    if (!h.transform.IsChildOf(_root)) houseTop = Mathf.Max(houseTop, h.point.y - _groundY);
                Wall(caps, "Cap Back", -_seal, _wallV - 6f, _seal, _wallV + 6f, backLow, _s.capHeight, layer);
                if (houseTop > 1f) Wall(caps, "Cap House", -_seal, -8f, _seal, 3f, houseTop, _s.capHeight, layer);
                _log.AppendLine($"CAPS: invisible walls on {(layer >= 0 ? "the CarGuard layer" : "Default (NO CarGuard layer)")} up to {_s.capHeight}: over the back wall from {backLow:F0} (its lowest part), over the house's wall from {(houseTop > 1f ? houseTop.ToString("F0") : "(no wall top found: none)")}. Off: Cap Height 0.");
            }

            // -------------------------------------------------------------------------------------------- the maze

            private void Maze()
            {
                int cols = _s.mazeColumns, rows = _s.mazeRows;
                float t = _s.mazeWallThickness, h = _s.grassHeight, u0 = -_mazeW * 0.5f;
                var east = new bool[cols, rows]; var north = new bool[cols, rows]; var seen = new bool[cols, rows];
                var mr = new System.Random(_s.mazeSeed);
                _entryCol = cols / 2;
                _exitCol = mr.Next(2) == 0 ? 0 : cols - 1;
                var stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(_entryCol, 0)); seen[_entryCol, 0] = true;
                var options = new List<Vector2Int>(4);
                Vector2Int[] steps = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
                while (stack.Count > 0)
                {
                    Vector2Int c = stack.Peek();
                    options.Clear();
                    foreach (Vector2Int d in steps) { Vector2Int k = c + d; if (k.x >= 0 && k.x < cols && k.y >= 0 && k.y < rows && !seen[k.x, k.y]) options.Add(d); }
                    if (options.Count == 0) { stack.Pop(); continue; }
                    Vector2Int go = options[mr.Next(options.Count)], next = c + go;
                    if (go.x == 1) east[c.x, c.y] = true; else if (go.x == -1) east[next.x, next.y] = true; else if (go.y == 1) north[c.x, c.y] = true; else north[next.x, next.y] = true;
                    seen[next.x, next.y] = true;
                    stack.Push(next);
                }
                // the way through (breadth first), for the route check
                var from = new Dictionary<Vector2Int, Vector2Int>();
                var queue = new Queue<Vector2Int>();
                Vector2Int start = new Vector2Int(_entryCol, 0), goal = new Vector2Int(_exitCol, rows - 1);
                queue.Enqueue(start); from[start] = start;
                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();
                    if (c == goal) break;
                    void Try(Vector2Int k, bool open) { if (open && !from.ContainsKey(k)) { from[k] = c; queue.Enqueue(k); } }
                    if (c.x + 1 < cols) Try(c + Vector2Int.right, east[c.x, c.y]);
                    if (c.x > 0) Try(c + Vector2Int.left, east[c.x - 1, c.y]);
                    if (c.y + 1 < rows) Try(c + Vector2Int.up, north[c.x, c.y]);
                    if (c.y > 0) Try(c + Vector2Int.down, north[c.x, c.y - 1]);
                }
                var cells = new List<Vector2Int>();
                for (Vector2Int c = goal; from.ContainsKey(c); c = from[c]) { cells.Add(c); if (c == start) break; }
                cells.Reverse();
                Vector2 Centre(Vector2Int c) => new Vector2(u0 + (c.x + 0.5f) * _cell, _m0 + (c.y + 0.5f) * _cell);
                _mazeRoute.Add(new Vector2(Centre(start).x, _m0 - 5f));
                foreach (Vector2Int c in cells) _mazeRoute.Add(Centre(c));
                _mazeRoute.Add(new Vector2(Centre(goal).x, _m1 + 5f));

                var holder = new GameObject("Maze").transform;
                holder.SetParent(_root, false);
                int walls = 0, clumps = 0;
                void Wall(float ua, float va, float ub, float vb)
                {
                    Vector3 a = P(ua, va), b = P(ub, vb);
                    Vector3 along = (b - a); along.y = 0f;
                    float len = along.magnitude + t;
                    var w = new GameObject("Wall " + (++walls)).transform;
                    w.SetParent(holder, false);
                    Vector3 mid = (a + b) * 0.5f;
                    w.SetPositionAndRotation(new Vector3(mid.x, GroundAt(mid), mid.z), Quaternion.LookRotation(along.normalized, Vector3.up));
                    var bc = w.gameObject.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, h * 0.5f, 0f); bc.size = new Vector3(t, h, len);
                    int n = Mathf.Max(1, Mathf.RoundToInt(len / Mathf.Max(0.5f, _s.grass.spacing)));
                    for (int i = 0; i < n; i++)
                    {
                        GameObject p = Pick(_s.grass);
                        if (p == null) break;
                        float z = -0.5f * len + (i + 0.5f) * len / n;
                        Vector3 at = w.TransformPoint(new Vector3(((float)_rng.NextDouble() - 0.5f) * t * 0.4f, 0f, z));
                        float target = h * (1f + ((float)_rng.NextDouble() * 2f - 1f) * _s.grassHeightJitter);
                        float top = Mathf.Max(0.01f, PrefabBox(p).max.y);
                        GameObject g = Spawn(p, p.name, w, new Vector3(at.x, GroundAt(at), at.z), Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), target / top);
                        Note("grass", g); clumps++;
                    }
                }
                for (int i = 0; i < cols; i++)
                    for (int j = 0; j < rows; j++)
                    {
                        if (i < cols - 1 && !east[i, j]) Wall(u0 + (i + 1) * _cell, _m0 + j * _cell, u0 + (i + 1) * _cell, _m0 + (j + 1) * _cell);
                        if (j < rows - 1 && !north[i, j]) Wall(u0 + i * _cell, _m0 + (j + 1) * _cell, u0 + (i + 1) * _cell, _m0 + (j + 1) * _cell);
                    }
                Wall(u0, _m0, u0, _m1);
                Wall(-u0, _m0, -u0, _m1);
                for (int i = 0; i < cols; i++)
                {
                    if (i != _entryCol) Wall(u0 + i * _cell, _m0, u0 + (i + 1) * _cell, _m0);
                    if (i != _exitCol) Wall(u0 + i * _cell, _m1, u0 + (i + 1) * _cell, _m1);
                }
                // the hedges either side, through the maze's middle depth, from the side boundaries to the maze's outer walls
                int hedges = HedgeRow("Maze Hedges W", (_m0 + _m1) * 0.5f, -_seal, u0) + HedgeRow("Maze Hedges E", (_m0 + _m1) * 0.5f, _seal, -u0);
                _log.AppendLine($"MAZE: {cols} x {rows} cells of {_cell} (paths {_s.mazePathWidth} wide, grass walls {t} thick, {h} tall: over the toy's {_toyJump:F1} jump, under the kid's {_kidJump * 0f + 10.5f * _kidH / 63f:F1} step), seed {_s.mazeSeed}: {walls} grass walls (one box each), {clumps} clumps. In at the front, cell {_entryCol + 1}; out at the back, cell {_exitCol + 1}; the way through is {cells.Count} cells. {hedges} hedge bushes either side close the yard round it: the toy goes through the maze (or the kid steps over it all).");
            }

            /// <summary>A row of hedge bushes at <paramref name="v"/> from <paramref name="uFrom"/> (in the side boundary) to
            /// <paramref name="uTo"/> (its last bush's inner face there), each with a box fitted to it. Returns how many.</summary>
            private int HedgeRow(string name, float v, float uFrom, float uTo)
            {
                if (_s.hedges.prefabs == null || _s.hedges.prefabs.Length == 0) { _log.AppendLine($"HEDGES {name}: NO PREFABS: the toy can walk round this beat."); return 0; }
                var holder = new GameObject(name).transform;
                holder.SetParent(_root, false);
                float sign = Mathf.Sign(uTo - uFrom), spacing = Mathf.Max(2f, _s.hedges.spacing);
                float avgHalf = 0f; int k = 0;
                foreach (GameObject p in _s.hedges.prefabs) if (p != null) { Bounds b = PrefabBox(p); avgHalf += 0.5f * Mathf.Min(b.size.x, b.size.z); k++; }
                avgHalf = k > 0 ? avgHalf / k * Mathf.Lerp(_s.hedges.scaleMin, _s.hedges.scaleMax, 0.5f) : 5f;
                float last = uTo - sign * (avgHalf - 1.5f);
                int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(last - uFrom) / spacing)) + 1;
                int made = 0;
                for (int i = 0; i < n; i++)
                {
                    float u = Mathf.Lerp(uFrom, last, n > 1 ? i / (float)(n - 1) : 1f);
                    GameObject p = Pick(_s.hedges);
                    if (p == null) break;
                    Vector3 at = P(u, v);
                    GameObject g = Spawn(p, p.name, holder, new Vector3(at.x, GroundAt(at), at.z), _turn * Quaternion.Euler(0f, 90f * _rng.Next(4), 0f), Scale(_s.hedges));
                    FitOwnBox(g);
                    // The last bush is measured, not averaged: turned and sized at random it reached past uTo (on the first
                    // landing a bush stood in the maze's way out). Its face toward the beat now ends exactly at uTo.
                    if (i == n - 1) EndAt(g, uTo, sign);
                    Note("hedges", g); _standable.Add((g.name, RenderBox(g)));
                    made++;
                }
                return made;
            }

            /// <summary>Slides a placed piece along u so its face toward +u (<paramref name="sign"/> 1) or toward -u (-1) is
            /// exactly at <paramref name="u"/>, measured on its own box.</summary>
            private void EndAt(GameObject g, float u, float sign)
            {
                Bounds b = RenderBox(g);
                float face = Vector3.Dot(b.center - _origin, _right) + sign * HalfAcross(b);
                g.transform.position += _right * (u - face);
            }

            /// <summary>Half a box's width along u, the yard's across (exact for the yard's square turns).</summary>
            private float HalfAcross(Bounds b) => Mathf.Abs(_right.x) * b.extents.x + Mathf.Abs(_right.z) * b.extents.z;

            // -------------------------------------------------------------------------------------------- the pond

            private void Pond()
            {
                var holder = new GameObject("Pond").transform;
                holder.SetParent(_root, false);
                Vector3 centre = P(0f, _pondV);
                float ground = GroundAt(centre);
                var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                water.name = "Water";
                Object.DestroyImmediate(water.GetComponent<Collider>());
                water.transform.SetParent(holder, false);
                water.transform.position = new Vector3(centre.x, ground + _s.waterLift - 0.01f, centre.z);
                water.transform.localScale = new Vector3(_s.pondDiameter, 0.01f, _s.pondDiameter);
                var look = water.GetComponent<MeshRenderer>();
                look.sharedMaterial = _s.water;
                look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // the hazard: under the log's top, so a toy on the log is safe and a toy in the water goes back
                var hz = new GameObject("Hazard") { layer = IgnoreRaycastLayer };
                hz.transform.SetParent(holder, false);
                float bottom = ground - 1f, top = ground + _s.waterLift + 0.3f;
                _hazardR = Mathf.Max(1f, _pondR - 0.5f);
                hz.transform.position = new Vector3(centre.x, (bottom + top) * 0.5f, centre.z);
                hz.transform.localScale = new Vector3(2f * _hazardR, (top - bottom) * 0.5f, 2f * _hazardR);
                var mc = hz.AddComponent<MeshCollider>();
                mc.sharedMesh = water.GetComponent<MeshFilter>().sharedMesh;
                mc.convex = true; mc.isTrigger = true;
                var hazard = hz.AddComponent<Hazard>();
                var hso = new SerializedObject(hazard);
                hso.FindProperty("anySize").boolValue = false;
                hso.FindProperty("hurts").intValue = (int)ShiftSize.Toy;
                hso.ApplyModifiedPropertiesWithoutUndo();
                _hazard = hz.transform;
                Note("pond", water);
                string crossing = Crossing(holder, ground);
                // lily pads on the water, off the crossing's line
                int pads = 0;
                for (int i = 0, tries = 0; i < _s.waterPlants.count && tries < _s.waterPlants.count * 20; tries++)
                {
                    float a = (float)_rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)_rng.NextDouble()) * (_pondR - 3f);
                    Vector2 uv = new Vector2(Mathf.Cos(a) * r, _pondV + Mathf.Sin(a) * r);
                    if (Mathf.Abs(uv.x) < 5f) continue;
                    GameObject p = Pick(_s.waterPlants); if (p == null) break;
                    Vector3 at = P(uv);
                    GameObject g = Spawn(p, p.name, holder, new Vector3(at.x, ground + _s.waterLift, at.z), Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), Scale(_s.waterPlants));
                    Note("water plants", g); pads++; i++;
                }
                // rocks round the rim, away from the crossing's ends
                int rocks = 0, want = _s.rocks.count / 2;
                for (int i = 0, tries = 0; i < want && tries < want * 20; tries++)
                {
                    float a = (float)_rng.NextDouble() * 360f;
                    if (Mathf.Abs(Mathf.DeltaAngle(a, 90f)) < 30f || Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 30f) continue;   // the log's two ends
                    Vector2 uv = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * (_pondR + 1.5f), _pondV + Mathf.Sin(a * Mathf.Deg2Rad) * (_pondR + 1.5f));
                    if (Placed("rocks").Exists(o => Vector2.Distance(UV(o.transform.position), uv) < 6f)) continue;
                    GameObject p = Pick(_s.rocks); if (p == null) break;
                    Vector3 at = P(uv);
                    GameObject g = Spawn(p, p.name, holder, new Vector3(at.x, GroundAt(at), at.z), Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), Scale(_s.rocks));
                    Note("rocks", g); _standable.Add((g.name, RenderBox(g))); rocks++; i++;
                }
                int hedges = HedgeRow("Pond Hedges W", _pondV, -_seal, -(_pondR - 2f)) + HedgeRow("Pond Hedges E", _pondV, _seal, _pondR - 2f);
                _log.AppendLine($"POND: {Path(water.transform)} at {F(water.transform.position)}, {_s.pondDiameter} across, its surface {_s.waterLift} over the lawn, {(_s.water != null ? _s.water.name + " (" + _s.water.shader.name + ")" : "NO water material")}; a Hazard (toy only) {2f * _hazardR:F1} across from under the lawn to {top - ground:F1} over it (under the crossing's top: a toy on the log is safe, a toy in the water goes back to the checkpoint; the kid wades). {crossing} {pads} water plants, {rocks} rocks round the rim. {hedges} hedge bushes either side meet the water: the toy crosses on the log.");
            }

            private string Crossing(Transform holder, float ground)
            {
                GameObject p = First(_s.crossing);
                if (p == null) return "CROSSING: NO PREFAB: the toy cannot cross (only the kid).";
                Bounds pb = PrefabBox(p);
                float len = Mathf.Max(pb.size.x, pb.size.z), wide = Mathf.Min(pb.size.x, pb.size.z);
                if (len >= 2f * wide)
                {
                    float k = (_s.pondDiameter + 2f * _s.crossingOverhang) / Mathf.Max(0.01f, len);
                    GameObject g = Spawn(p, "Crossing", holder, P(0f, _pondV), LongTurn(p, _fwd), k);
                    Bounds b = RenderBox(g);
                    Vector2 c = UV(b.center);
                    g.transform.position += P(0f, _pondV) - P(c.x, c.y);
                    g.transform.position += Vector3.up * (ground - _s.crossingSink - g.transform.position.y);
                    Physics.SyncTransforms();
                    float top = WalkTopAt(P(0f, _pondV) + Vector3.up * 40f, g.transform);
                    Note("crossing", g);
                    return $"CROSSING: {p.name} x{k:F2} lies over the pond from bank to bank ({_s.pondDiameter + 2f * _s.crossingOverhang:F0} long, sunk {_s.crossingSink} into the lawn); its walkable top {(float.IsNaN(top) ? "NOT FOUND (no collider on it?)" : (top - ground).ToString("F2") + " over the lawn")} (the toy jumps {_toyJump:F1}){(!float.IsNaN(top) && top - ground > _toyJump - 0.2f ? ": TOO HIGH for the toy (raise Crossing Sink)" : "")}{(!float.IsNaN(top) && top - ground < _s.waterLift + 0.4f ? ": TOO LOW: the water's hazard reaches over it, a toy on it goes back (lower Crossing Sink)" : "")}.";
                }
                int n = Mathf.Max(1, _s.crossing.count);
                float scale = _s.stoneWidth / Mathf.Max(0.01f, wide);
                for (int i = 0; i < n; i++)
                {
                    float v = _pondV - _pondR + (i + 1) * _s.pondDiameter / (n + 1);
                    GameObject g = Spawn(p, "Stone " + (i + 1), holder, P(0f, v), Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), scale);
                    Bounds b = RenderBox(g);
                    g.transform.position += Vector3.up * (ground + _s.waterLift + _s.stoneTop - b.max.y);
                    Note("crossing", g);
                }
                return $"CROSSING: {n} stepping stones ({p.name}, {_s.stoneWidth} wide, tops {_s.stoneTop} over the water) across the pond.";
            }

            // -------------------------------------------------------------------------------------------- the flowerbed and the lost toy

            private void Flowerbed()
            {
                var holder = new GameObject("Flowerbed").transform;
                holder.SetParent(_root, false);
                Vector3 c = P(_bed);
                float ground = GroundAt(c);
                var soil = GameObject.CreatePrimitive(PrimitiveType.Cube);
                soil.name = "Soil";
                Object.DestroyImmediate(soil.GetComponent<Collider>());
                soil.transform.SetParent(holder, false);
                soil.transform.SetPositionAndRotation(new Vector3(c.x, ground + 0.03f, c.z), _turn);
                soil.transform.localScale = new Vector3(_s.flowerbedSize.x, 0.06f, _s.flowerbedSize.y);
                soil.GetComponent<Renderer>().sharedMaterial = _s.soil;
                // the lost toy, and the box that finds it
                GameObject toyPrefab = First(_s.lostToy);
                var found = new GameObject("Lost Toy").transform;
                found.SetParent(holder, false);
                found.position = new Vector3(c.x, ground, c.z);
                GameObject toy = null;
                if (toyPrefab != null)
                {
                    toy = Spawn(toyPrefab, toyPrefab.name, found, found.position, Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), Scale(_s.lostToy));
                    toy.transform.position += Vector3.up * (ground + 0.06f - RenderBox(toy).min.y);
                }
                Bounds tb = toy != null ? RenderBox(toy) : new Bounds(found.position + Vector3.up, Vector3.one * 2f);
                var box = found.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = found.InverseTransformPoint(tb.center);
                box.size = new Vector3(tb.size.x + 4f, Mathf.Max(6f, tb.size.y + 3f), tb.size.z + 4f);
                found.gameObject.layer = IgnoreRaycastLayer;
                var lost = found.gameObject.AddComponent<LostToy>();
                var hook = found.gameObject.AddComponent<ObjectiveHook>();
                hook.Setup(_s.lostStarId, ObjectiveHook.Kind.Star, 1);
                var lso = new SerializedObject(lost);
                lso.FindProperty("model").objectReferenceValue = toy != null ? toy.transform : null;
                SerializedProperty calls = lso.FindProperty("onFound.m_PersistentCalls.m_Calls");
                calls.arraySize = 1;
                Listener(calls.GetArrayElementAtIndex(0), hook, typeof(ObjectiveHook).AssemblyQualifiedName, nameof(ObjectiveHook.Fire));
                lso.ApplyModifiedPropertiesWithoutUndo();
                if (toy != null) Note("lost toy", toy);
                // the flowers round it (none on it), and rocks round the bed
                int flowers = 0;
                float hx = _s.flowerbedSize.x * 0.5f - 2f, hz = _s.flowerbedSize.y * 0.5f - 2f;
                for (int i = 0, tries = 0; i < _s.flowers.count && tries < _s.flowers.count * 30; tries++)
                {
                    Vector2 uv = _bed + new Vector2(((float)_rng.NextDouble() * 2f - 1f) * hx, ((float)_rng.NextDouble() * 2f - 1f) * hz);
                    if (Vector2.Distance(uv, _bed) < _s.lostToyClearing) continue;
                    if (Placed("flowers").Exists(o => Vector2.Distance(UV(o.transform.position), uv) < 3.5f)) continue;
                    GameObject p = Pick(_s.flowers); if (p == null) break;
                    Vector3 at = P(uv);
                    GameObject g = Spawn(p, p.name, holder, new Vector3(at.x, ground + 0.06f, at.z), Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), Scale(_s.flowers));
                    Note("flowers", g); flowers++; i++;
                }
                int rocks = 0, want = _s.rocks.count - _s.rocks.count / 2;
                for (int i = 0, tries = 0; i < want && tries < want * 20; tries++)
                {
                    float side = _rng.Next(2) == 0 ? -1f : 1f;
                    bool alongU = _rng.Next(2) == 0;
                    Vector2 uv = alongU
                        ? _bed + new Vector2(((float)_rng.NextDouble() * 2f - 1f) * (hx + 2f), side * (hz + 4f))
                        : _bed + new Vector2(side * (hx + 4f), ((float)_rng.NextDouble() * 2f - 1f) * (hz + 2f));
                    if (Placed("rocks").Exists(o => Vector2.Distance(UV(o.transform.position), uv) < 6f)) continue;
                    GameObject p = Pick(_s.rocks); if (p == null) break;
                    Vector3 at = P(uv);
                    GameObject g = Spawn(p, p.name, holder, new Vector3(at.x, GroundAt(at), at.z), Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), Scale(_s.rocks));
                    Note("rocks", g); _standable.Add((g.name, RenderBox(g))); rocks++; i++;
                }
                _log.AppendLine($"FLOWERBED: {Path(holder)} at {F(c)} ({_s.flowerbedSize.x} x {_s.flowerbedSize.y} of soil, no collider), {flowers} flowers (no colliders: the toy walks between the stems), {rocks} rocks round it. LOST TOY: {(toy != null ? toyPrefab.name + " x" + toy.transform.localScale.x.ToString("F2") + ", " + tb.size.y.ToString("F1") + " tall" : "NONE (no prefab on Lost Toy)")} in the middle, {_s.lostToyClearing} clear round it; {Path(found)} (LostToy, a trigger {box.size.x:F0} x {box.size.y:F0} x {box.size.z:F0}): this PC's player touching it, either size, finds it (a hop, a sound) and its On Found calls the ObjectiveHook beside it: the star '{_s.lostStarId}'.");
            }

            // -------------------------------------------------------------------------------------------- the garden core

            private void Core()
            {
                // a copy of the sill's core (the same look and set-up), used from anywhere in reach
                float lift = 2.5f;
                MeshFilter body = _coreD.GetComponent<MeshFilter>();
                if (body != null && body.sharedMesh != null) lift = -body.sharedMesh.bounds.min.y * Mathf.Abs(_coreD.lossyScale.y);   // its body resting on the lawn
                var core = Object.Instantiate(_coreD.gameObject, _root);
                core.name = "CoreF_Garden";
                Vector3 at = P(_corePos);
                core.transform.SetPositionAndRotation(new Vector3(at.x, GroundAt(at) + lift, at.z), _coreD.rotation);
                var src = core.GetComponent<ShiftSource>();
                var so = new SerializedObject(src);
                so.FindProperty("standWithin").floatValue = 0f;
                so.FindProperty("mode").intValue = (int)ShiftSource.Mode.Toggle;
                so.FindProperty("startDormant").boolValue = false;
                SerializedProperty calls = so.FindProperty("onUsed.m_PersistentCalls.m_Calls");
                for (int c = calls.arraySize - 1; c >= 0; c--) if (calls.GetArrayElementAtIndex(c).FindPropertyRelative("m_Target").objectReferenceValue is ObjectiveHook) calls.DeleteArrayElementAtIndex(c);
                so.ApplyModifiedPropertiesWithoutUndo();
                var marker = core.GetComponent<CoreMarker>();
                if (marker == null) marker = core.AddComponent<CoreMarker>();
                marker.Setup(core.name, true, true);
                Note("core", core);
                _standable.Add((core.name, RenderBox(core)));
                _log.AppendLine($"CORE: {Path(core.transform)}, a copy of {_coreD.name} (Toggle, awake, used from anywhere in reach, {lift:F1} over the lawn) at {F(core.transform.position)}: a kid in the garden shrinks here for the slats, a toy grows; it refills the charge as every core does. Its CoreMarker names it for 'No charge' (the place words on ObjectiveSet).");
            }

            // -------------------------------------------------------------------------------------------- checkpoints, the end, the arrival

            private void Checkpoints()
            {
                float half = _inner - 6f;
                // the sill: over the wooden sill and the window's ledge, either size (the kid gets there first)
                Bounds sb = RenderBox(_sill.gameObject);
                Bounds wl = RenderBox(_window.gameObject);
                Vector3 sc = new Vector3(sb.center.x, sb.max.y + 5f, sb.center.z);
                Vector3 point = _sillPoint != Vector3.zero ? _sillPoint : Surface(new Vector3(sb.center.x, wl.max.y + 5f, sb.center.z), sb.max.y);
                Transform cpSill = Checkpoint("CP_sill", sc, Quaternion.LookRotation(_fwd), new Vector3(sb.size.x, 10f, sb.size.z + 2f), point, true);
                // the window opens when it is claimed
                var cso = new SerializedObject(cpSill.GetComponent<Checkpoint>());
                SerializedProperty calls = cso.FindProperty("onClaimed.m_PersistentCalls.m_Calls");
                calls.arraySize = 1;
                Listener(calls.GetArrayElementAtIndex(0), _opening, typeof(WindowOpening).AssemblyQualifiedName, nameof(WindowOpening.Open));
                cso.ApplyModifiedPropertiesWithoutUndo();
                Checkpoint("CP_landing", Mid(-half, _s.landingClaimFrom, half, _m0 - 8f, 45f), _turn, Size(2f * half, _m0 - 8f - _s.landingClaimFrom, 45f), PointOn(0f, _m0 - 28f), true);
                Checkpoint("CP_pond", Mid(-half, _m1 + 4f, half, _pondV - _pondR - 5f, 14f), _turn, Size(2f * half, _pondV - _pondR - 5f - _m1 - 4f, 14f), PointOn(0f, _pondV - _pondR - 12f), true);
                Checkpoint("CP_fence", Mid(-half, _wallV - 70f, half, _wallV - 10f, 70f), _turn, Size(2f * half, 60f, 70f), PointOn(0f, _wallV - 35f), true);
                _log.AppendLine($"CHECKPOINTS (either size, the house's CP_ look: the amber dot lights when claimed): CP_sill over the sill and the window's ledge (its On Claimed opens the window; its point on the ledge at {F(point)}); CP_landing from {_s.landingClaimFrom} out to the maze, 45 high (a gliding toy claims it on the way down; a toy that falls from the window breaks nearer the wall and goes back to the sill); CP_pond between the maze and the pond (a toy in the water comes back here); CP_fence before the back wall. A fourth was added before the pond (the brief named the landing and the fence): without it a toy in the water walked the maze again.");
            }

            private Vector3 Mid(float u0, float v0, float u1, float v1, float tall) => P((u0 + u1) * 0.5f, (v0 + v1) * 0.5f) + Vector3.up * (GroundAt(P((u0 + u1) * 0.5f, (v0 + v1) * 0.5f)) - _groundY + tall * 0.5f);
            private static Vector3 Size(float across, float deep, float tall) => new Vector3(Mathf.Abs(across), tall, Mathf.Abs(deep));
            private Vector3 PointOn(float u, float v) { Vector3 p = P(u, v); return new Vector3(p.x, GroundAt(p) + 0.2f, p.z); }

            private Vector3 Surface(Vector3 above, float fallback)
            {
                foreach (RaycastHit h in Physics.RaycastAll(above, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
                    if (!h.transform.IsChildOf(_root)) return h.point + Vector3.up * 0.2f;
                return new Vector3(above.x, fallback + 0.2f, above.z);
            }

            private Transform Checkpoint(string name, Vector3 centre, Quaternion turn, Vector3 size, Vector3 point, bool anySize)
            {
                var t = new GameObject(name) { layer = IgnoreRaycastLayer }.transform;
                t.SetParent(_root, false);
                t.SetPositionAndRotation(centre, turn);
                var bc = t.gameObject.AddComponent<BoxCollider>();
                bc.isTrigger = true; bc.size = size;
                var cp = t.gameObject.AddComponent<Checkpoint>();
                var spot = new GameObject("Spot").transform;
                spot.SetParent(t, false);
                spot.SetPositionAndRotation(point, Quaternion.LookRotation(_fwd, Vector3.up));
                var so = new SerializedObject(cp);
                so.FindProperty("anySize").boolValue = anySize;
                so.FindProperty("point").objectReferenceValue = spot;
                // the amber dot at the trigger's foot that lights when claimed (as CourseBuilder's checkpoints)
                var glow = AssetDatabase.LoadAssetAtPath<Material>(CheckpointGlowPath);
                var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = "Marker";
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.SetParent(t, false);
                marker.transform.position = point + Vector3.up * 0.6f;
                marker.transform.localScale = Vector3.one * 1.4f;
                var mr = marker.GetComponent<Renderer>();
                if (glow != null) mr.sharedMaterial = glow;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                marker.AddComponent<Billboard>();
                var lightGo = new GameObject("MarkerLight");
                lightGo.transform.SetParent(marker.transform, false);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point; l.range = 4f; l.intensity = 0.5f; l.color = new Color(1f, 0.75f, 0.3f); l.shadows = LightShadows.None;
                marker.SetActive(false);
                so.FindProperty("litWhenClaimed").objectReferenceValue = marker;
                so.ApplyModifiedPropertiesWithoutUndo();
                Note("checkpoints", t.gameObject);
                return t;
            }

            private void End()
            {
                float half = _halfW - 12f, v0 = _wallV + 4f, v1 = _wallV + 4f + _s.endDepth;
                var goalGo = new GameObject("Goal") { layer = IgnoreRaycastLayer };
                goalGo.transform.SetParent(_root, false);
                goalGo.transform.SetPositionAndRotation(Mid(-half, v0, half, v1, 12f), _turn);
                var bc = goalGo.AddComponent<BoxCollider>();
                bc.isTrigger = true; bc.size = Size(2f * half, v1 - v0, 12f);
                var goal = goalGo.AddComponent<Goal>();
                var gso = new SerializedObject(goal);
                gso.FindProperty("anySize").boolValue = _s.endAnySize;
                gso.FindProperty("requiredSize").intValue = (int)ShiftSize.Toy;
                gso.FindProperty("message").stringValue = "";
                GameFlow flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
                // the balloons: a copy of the sill's GoalBalloons (the same prefab and burst), at the end
                ParticleSystem burst = null;
                Transform oldBalloons = _r6.Find("GoalBalloons");
                GameObject balloonPrefab = oldBalloons != null ? PrefabUtility.GetCorrespondingObjectFromSource(oldBalloons.gameObject) : null;
                if (balloonPrefab != null)
                {
                    var party = (GameObject)PrefabUtility.InstantiatePrefab(balloonPrefab, _root);
                    party.name = "GoalBalloons";
                    Vector3 at = P(0f, (v0 + v1) * 0.5f);
                    party.transform.position = new Vector3(at.x, GroundAt(at) + 1f, at.z);
                    foreach (ParticleSystem ps in party.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = ps.main; main.loop = false; main.playOnAwake = false; main.maxParticles = 60; main.duration = 1f;
                        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
                        if (burst == null) burst = ps;
                    }
                }
                SerializedProperty calls = gso.FindProperty("onReached.m_PersistentCalls.m_Calls");
                calls.arraySize = (flow != null ? 1 : 0) + (burst != null ? 1 : 0);
                int ci = 0;
                if (flow != null) Listener(calls.GetArrayElementAtIndex(ci++), flow, typeof(GameFlow).AssemblyQualifiedName, nameof(GameFlow.End));
                if (burst != null) Listener(calls.GetArrayElementAtIndex(ci++), burst, typeof(ParticleSystem).AssemblyQualifiedName, "Play");
                gso.ApplyModifiedPropertiesWithoutUndo();
                _endPoint = PointOn(_gapPoint != Vector3.zero ? UV(_gapPoint).x : 0f, (v0 + v1) * 0.5f);
                Note("end", goalGo);
                _log.AppendLine($"END: {Path(goalGo.transform)} (Goal: {(_s.endAnySize ? "EITHER size (End Any Size; its Required Size Toy underneath)" : "the toy only")}) a strip {2f * half:F0} across and {_s.endDepth} deep just past the back wall: through any gap the toy reaches it (the kid cannot get there until D-0240 makes the Gate Panel breakable). Its On Reached: {(flow != null ? "GameFlow.End on " + Path(flow.transform) + " (the end card over the garden)" : "NO GameFlow in the scene: no end card!")}{(burst != null ? " and the balloons' burst (" + Path(burst.transform) + ", a copy of the sill's)" : "; NO balloons (no GoalBalloons prefab found)")}. Online the house has no end (GameFlow), as before.");
            }

            private void Arrival()
            {
                var go = new GameObject("Arrival " + _s.zoneId) { layer = IgnoreRaycastLayer };
                go.transform.SetParent(_root, false);
                go.transform.SetPositionAndRotation(Mid(-_halfW, 0f, _halfW, _wallV, 160f), _turn);
                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true; bc.size = Size(2f * _halfW, _wallV, 160f);
                go.AddComponent<ArrivalCard>().Setup(_s.zoneId);
                Note("arrival", go);
            }

            // -------------------------------------------------------------------------------------------- dressing and trees

            private void Dressing()
            {
                var holder = new GameObject("Dressing").transform;
                holder.SetParent(_root, false);
                // the path's stones: from the window to the maze, from the maze's way out to the pond, from the pond to the wall
                int stones = 0;
                float uIn = -_mazeW * 0.5f + (_entryCol + 0.5f) * _cell, uOut = -_mazeW * 0.5f + (_exitCol + 0.5f) * _cell;
                stones += Stones(holder, new Vector2(0f, 30f), new Vector2(uIn, _m0 - 10f));
                stones += Stones(holder, new Vector2(uOut, _m1 + 10f), new Vector2(0f, _pondV - _pondR - 10f));
                stones += Stones(holder, new Vector2(0f, _pondV + _pondR + 10f), new Vector2(0f, _wallV - 14f));
                // furniture either side of the glide's way, near the house
                int props = 0;
                for (int i = 0, tries = 0; i < _s.props.count && tries < _s.props.count * 60 && _s.props.prefabs != null && _s.props.prefabs.Length > 0; tries++)
                {
                    float side = (i % 2 == 0) ? 1f : -1f;
                    float u = side * Mathf.Lerp(_s.glideLaneWidth * 0.5f + 20f, _inner - 22f, (float)_rng.NextDouble());
                    float v = Mathf.Lerp(22f, _m0 - 30f, (float)_rng.NextDouble());
                    if (!Free(new Vector2(u, v), 4f) && !(Mathf.Abs(u) > _s.glideLaneWidth * 0.5f && v < _m0 - 12f)) continue;
                    GameObject p = _s.props.prefabs[i % _s.props.prefabs.Length];
                    if (p == null) { i++; continue; }
                    Vector3 at = P(u, v);
                    GameObject g = Spawn(p, p.name, holder, new Vector3(at.x, GroundAt(at), at.z), _turn * Quaternion.Euler(0f, 90f * _rng.Next(4) + ((float)_rng.NextDouble() - 0.5f) * 30f, 0f), Scale(_s.props));
                    Bounds b = RenderBox(g);
                    // Measured clear of the glide's way: only the piece's middle was kept out, and on the first landing a
                    // bench turned across the lane reached into it. Its near end now stays 4 outside the lane, or it goes.
                    float laneEdge = _s.glideLaneWidth * 0.5f + 4f;
                    float nearEnd = side * Vector3.Dot(b.center - _origin, _right) - HalfAcross(b);
                    if (nearEnd < laneEdge) { g.transform.position += _right * (side * (laneEdge - nearEnd)); b = RenderBox(g); }
                    if (side * Vector3.Dot(b.center - _origin, _right) + HalfAcross(b) > _inner - 2f) { Object.DestroyImmediate(g); continue; }
                    bool clash = false;
                    foreach (GameObject o in Placed("props")) { Bounds ob = RenderBox(o); ob.Expand(8f); if (ob.Intersects(b)) { clash = true; break; } }
                    if (clash) { Object.DestroyImmediate(g); continue; }
                    Note("props", g); _standable.Add((g.name, b)); props++; i++;
                }
                // small things in the grass, anywhere free
                int accents = 0;
                for (int i = 0, tries = 0; i < _s.accents.count && tries < _s.accents.count * 40; tries++)
                {
                    Vector2 uv = new Vector2(((float)_rng.NextDouble() * 2f - 1f) * (_inner - 10f), Mathf.Lerp(16f, _wallV - 30f, (float)_rng.NextDouble()));
                    if (!Free(uv, 3f)) continue;
                    if (Placed("accents").Exists(o => Vector2.Distance(UV(o.transform.position), uv) < 10f) || Placed("props").Exists(o => Vector2.Distance(UV(o.transform.position), uv) < 14f)) continue;
                    GameObject p = Pick(_s.accents); if (p == null) break;
                    Vector3 at = P(uv);
                    GameObject g = Spawn(p, p.name, holder, new Vector3(at.x, GroundAt(at), at.z), Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), Scale(_s.accents));
                    FitOwnBox(g);
                    Note("accents", g); _standable.Add((g.name, RenderBox(g))); accents++; i++;
                }
                _log.AppendLine($"DRESSING: {stones} path stones (no colliders) on the toy's way, {props} props by the house either side of the glide's way, {accents} accents (each with a box: they look solid to the toy) on free lawn (never on the path, the glide's way, a beat, the core, by the wall or the house).");
            }

            private int Stones(Transform holder, Vector2 a, Vector2 b)
            {
                if (_s.pathStones.prefabs == null || _s.pathStones.prefabs.Length == 0) return 0;
                float len = Vector2.Distance(a, b), spacing = Mathf.Max(4f, _s.pathStones.spacing);
                int n = Mathf.Max(1, Mathf.FloorToInt(len / spacing) + 1), made = 0;
                for (int i = 0; i < n; i++)
                {
                    Vector2 uv = Vector2.Lerp(a, b, n > 1 ? i / (float)(n - 1) : 0.5f);
                    GameObject p = Pick(_s.pathStones); if (p == null) break;
                    Vector3 at = P(uv);
                    GameObject g = Spawn(p, p.name, holder, new Vector3(at.x, GroundAt(at) + 0.01f, at.z), Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), Scale(_s.pathStones));
                    Note("path stones", g); made++;
                }
                return made;
            }

            private void Trees()
            {
                // the old GardenTrees: off inside the play area (the yard, its walls and its end), on outside
                var old = new List<GameObject>();
                foreach (Transform t in _r6) if (t.name.StartsWith("GardenTree")) old.Add(t.gameObject);
                var inside = new List<string>();
                var standing = new List<Vector3>();
                foreach (GameObject g in old)
                {
                    Vector2 uv = UV(g.transform.position);
                    bool inPlay = Mathf.Abs(uv.x) < _seal + 45f && uv.y > -30f && uv.y < _farV + 45f;
                    bool on = !(inPlay && _s.insideTreesOff);
                    if (g.activeSelf != on) { g.SetActive(on); EditorUtility.SetDirty(g); }
                    if (inPlay) inside.Add($"{g.name} u {uv.x:F0} v {uv.y:F0}");
                    if (on) standing.Add(g.transform.position);
                }
                // the backdrop: blocky trees and bushes past the end, outside the play area, clear of the old trees
                var holder = new GameObject("Backdrop").transform;
                holder.SetParent(_root, false);
                Bounds lawn = _groundCol.bounds;
                int planted = 0;
                float spacing = Mathf.Max(10f, _s.backdrop.spacing);
                for (int i = 0, tries = 0; i < _s.backdrop.count && tries < _s.backdrop.count * 40; tries++)
                {
                    Vector2 uv = new Vector2(((float)_rng.NextDouble() * 2f - 1f) * (_seal + 160f), _farV + 30f + (float)_rng.NextDouble() * 220f);
                    Vector3 at = P(uv);
                    if (at.x < lawn.min.x + 40f || at.x > lawn.max.x - 40f || at.z < lawn.min.z + 40f || at.z > lawn.max.z - 40f) continue;
                    bool near = false;
                    foreach (Vector3 s in standing) if (new Vector2(s.x - at.x, s.z - at.z).magnitude < spacing) { near = true; break; }
                    if (near) continue;
                    GameObject p = Pick(_s.backdrop); if (p == null) break;
                    Bounds pb = PrefabBox(p);
                    float k = Mathf.Lerp(_s.backdropHeight.x, _s.backdropHeight.y, (float)_rng.NextDouble()) / Mathf.Max(0.01f, pb.max.y);
                    Quaternion turn = IsCutout(pb) ? Quaternion.LookRotation(-_fwd, Vector3.up) * Quaternion.Euler(0f, ((float)_rng.NextDouble() - 0.5f) * 40f, 0f) : Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
                    GameObject t = Spawn(p, p.name, holder, new Vector3(at.x, GroundAt(at), at.z), turn, k);
                    Note("backdrop", t); standing.Add(at); planted++; i++;
                }
                _log.AppendLine($"TREES: {old.Count} old GardenTrees; {inside.Count} stand inside the play area{(inside.Count > 0 ? " (" + string.Join(", ", inside) + ")" : "")}: {(_s.insideTreesOff ? "switched OFF (kept, not deleted)" : "left ON (Inside Trees Off is unticked: they stand in the beats)")}; the rest stand as they were. BACKDROP: {planted} blocky pieces planted past the end (the cutouts facing the garden), at least {spacing:F0} from each other and from the old trees.");
            }

            /// <summary>The Ground's look: the lawn (tiled to Lawn Tile), or what it wore before.</summary>
            private void Lawn()
            {
                Renderer r = _ground.GetComponent<Renderer>();
                if (r == null) { _log.AppendLine("LAWN: the Ground has no renderer: nothing changed."); return; }
                Material now = r.sharedMaterial;
                if (_s.lawnOn && _s.lawn != null)
                {
                    if (_s.lawnWas == null && now != _s.lawn) { _s.lawnWas = now; EditorUtility.SetDirty(_s); AssetDatabase.SaveAssetIfDirty(_s); }
                    Bounds b = _groundCol.bounds;
                    _s.lawn.SetTextureScale("_BaseMap", new Vector2(b.size.x / Mathf.Max(1f, _s.lawnTile), b.size.z / Mathf.Max(1f, _s.lawnTile)));
                    _s.lawn.SetFloat("_Smoothness", 0.07f);
                    EditorUtility.SetDirty(_s.lawn);
                    AssetDatabase.SaveAssetIfDirty(_s.lawn);
                    r.sharedMaterial = _s.lawn;
                    EditorUtility.SetDirty(r);
                    _log.AppendLine($"LAWN: the Ground wears {_s.lawn.name} (URP Lit, {(_s.lawn.GetTexture("_BaseMap") != null ? _s.lawn.GetTexture("_BaseMap").name : "NO texture")}), one repeat every {_s.lawnTile} ({b.size.x / _s.lawnTile:F1} x {b.size.z / _s.lawnTile:F1} over the Ground); it wore {(_s.lawnWas != null ? _s.lawnWas.name : "?")} (kept on the settings as Lawn Was).");
                }
                else if (!_s.lawnOn && _s.lawnWas != null && now != _s.lawnWas)
                {
                    r.sharedMaterial = _s.lawnWas;
                    EditorUtility.SetDirty(r);
                    _log.AppendLine($"LAWN: Lawn On is unticked: the Ground wears {_s.lawnWas.name} again.");
                }
                else _log.AppendLine($"LAWN: left as it is ({(now != null ? now.name : "none")}).");
            }

            // -------------------------------------------------------------------------------------------- the game's materials on the pack's pieces

            private void Materials()
            {
                if (!_s.swapMaterials) { _log.AppendLine("MATERIALS: Swap Materials is unticked: the pieces keep the pack's own materials (the SICS shaders drew some invisible in this renderer)."); return; }
                var rows = new List<BackyardSettings.MaterialSwap>(_s.materialSwaps ?? new BackyardSettings.MaterialSwap[0]);
                var map = new Dictionary<Material, Material>();
                foreach (var r in rows) if (r != null && r.from != null && r.to != null && !map.ContainsKey(r.from)) map[r.from] = r.to;
                Shader lit = Shader.Find(LitShader);
                var mats = new List<Material>();
                var refreshed = new HashSet<Material>();   // each pack material's own copy is refreshed once a run
                foreach (var pair in _placed)
                    foreach (GameObject g in pair.Value)
                    {
                        // only the pack's pieces: not the game's own parts (the core's glow, the checkpoints' dots, the step)
                        if (g == null || Array.IndexOf(OwnParts, pair.Key) >= 0) continue;
                        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r is ParticleSystemRenderer) continue;
                            Material[] own = r.sharedMaterials;
                            bool changed = false;
                            for (int i = 0; i < own.Length; i++)
                            {
                                Material m = own[i];
                                if (m == null || m.shader == lit) continue;
                                if (m == _s.water || m == _s.soil) continue;
                                if (!map.TryGetValue(m, out Material to))
                                {
                                    to = LitCopy(m, lit);
                                    map[m] = to;
                                    rows.RemoveAll(x => x != null && x.from == m);
                                    rows.Add(new BackyardSettings.MaterialSwap { from = m, to = to });
                                    _swapMade++;
                                }
                                else if (to != null && !refreshed.Contains(m) && AssetDatabase.GetAssetPath(to) == CopyPath(m))
                                {
                                    // Our own copy (not one he picked by hand): its settings come from the pack's again each
                                    // run, as the summary below promises. Before 25 Nov a stored copy was never refreshed,
                                    // so the white copies of the first batch run stayed white.
                                    LitCopy(m, lit);
                                    _swapMade++;
                                }
                                refreshed.Add(m);
                                if (to != null && to != m) { own[i] = to; changed = true; }
                            }
                            if (changed) { r.sharedMaterials = own; _swapped++; }
                        }
                    }
                _s.materialSwaps = rows.ToArray();
                EditorUtility.SetDirty(_s);
                AssetDatabase.SaveAssetIfDirty(_s);
                var names = new List<string>();
                foreach (var r in rows) if (r != null && r.from != null) names.Add($"{r.from.name} -> {(r.to != null ? r.to.name : "NONE")}");
                _log.AppendLine($"MATERIALS: {_swapped} renderers wear the game's URP Lit copies ({_swapMade} copies made or refreshed now in {MaterialFolder}; the pack's own assets untouched). Swaps on the settings: {string.Join("; ", names)}.");
            }

            private static readonly string[] OwnParts = { "checkpoints", "core", "end", "arrival", "window step", "pond", "spare plane" };

            /// <summary>Our URP Lit copy of a pack material (made once, its settings refreshed on each run): the pack's texture
            /// and colour, smoothness as the kid's own material; the vegetation atlases alpha-clipped and two-sided.</summary>
            private Material LitCopy(Material src, Shader lit)
            {
                EnsureFolder(MaterialFolder);
                string path = CopyPath(src);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool made = m == null;
                if (made) m = new Material(lit) { name = src.name + "_Lit" };
                else if (m.shader != lit) m.shader = lit;
                // Read from the pack material's saved values, not through its shader: in a batch run (25 Nov) the pack's toon
                // shader did not answer for its properties and every copy came out white. The saved values never depend on it.
                SavedTexture(src, "_MainTex", out Texture tex, out Vector2 scale, out Vector2 offset);
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", scale);
                m.SetTextureOffset("_BaseMap", offset);
                m.SetColor("_BaseColor", SavedColor(src, "_Color", Color.white));
                m.SetFloat("_Smoothness", _s.swapSmoothness);
                m.SetFloat("_Metallic", 0f);
                bool leaves = src.name.IndexOf("Vegetation", StringComparison.OrdinalIgnoreCase) >= 0;
                if (leaves)
                {
                    float cut = SavedFloat(src, "_AlphaCutoff", SavedFloat(src, "_Cutoff", 0.5f));
                    m.SetFloat("_AlphaClip", 1f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", cut);
                    m.SetFloat("_Cull", 0f);
                    m.renderQueue = 2450;
                }
                else
                {
                    m.SetFloat("_AlphaClip", 0f); m.DisableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", 2f);
                    m.renderQueue = -1;
                }
                if (made) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
                AssetDatabase.SaveAssetIfDirty(m);
                if (tex == null) _log.AppendLine($"MATERIALS: {src.name} has NO saved _MainTex: its copy is plain white.");
                return m;
            }

            /// <summary>Where our own URP Lit copy of a pack material lives.</summary>
            private static string CopyPath(Material src) => MaterialFolder + "/" + src.name + "_Lit.mat";

            /// <summary>A texture slot as saved in a material's file (its m_SavedProperties), whatever its shader says.</summary>
            private static void SavedTexture(Material m, string name, out Texture tex, out Vector2 scale, out Vector2 offset)
            {
                tex = null; scale = Vector2.one; offset = Vector2.zero;
                SerializedProperty list = new SerializedObject(m).FindProperty("m_SavedProperties.m_TexEnvs");
                for (int i = 0; list != null && i < list.arraySize; i++)
                {
                    SerializedProperty pair = list.GetArrayElementAtIndex(i);
                    if (pair.FindPropertyRelative("first").stringValue != name) continue;
                    SerializedProperty slot = pair.FindPropertyRelative("second");
                    tex = slot.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
                    scale = slot.FindPropertyRelative("m_Scale").vector2Value;
                    offset = slot.FindPropertyRelative("m_Offset").vector2Value;
                    return;
                }
            }

            /// <summary>A colour as saved in a material's file, or <paramref name="fallback"/> when it has none.</summary>
            private static Color SavedColor(Material m, string name, Color fallback)
            {
                SerializedProperty list = new SerializedObject(m).FindProperty("m_SavedProperties.m_Colors");
                for (int i = 0; list != null && i < list.arraySize; i++)
                {
                    SerializedProperty pair = list.GetArrayElementAtIndex(i);
                    if (pair.FindPropertyRelative("first").stringValue == name) return pair.FindPropertyRelative("second").colorValue;
                }
                return fallback;
            }

            /// <summary>A number as saved in a material's file, or <paramref name="fallback"/> when it has none.</summary>
            private static float SavedFloat(Material m, string name, float fallback)
            {
                SerializedProperty list = new SerializedObject(m).FindProperty("m_SavedProperties.m_Floats");
                for (int i = 0; list != null && i < list.arraySize; i++)
                {
                    SerializedProperty pair = list.GetArrayElementAtIndex(i);
                    if (pair.FindPropertyRelative("first").stringValue == name) return pair.FindPropertyRelative("second").floatValue;
                }
                return fallback;
            }

            // -------------------------------------------------------------------------------------------- the words

            private void Words()
            {
                var lines = new List<string>();
                var set = AssetDatabase.LoadAssetAtPath<ObjectiveSet>(ObjectivesBuilder.AssetPath);
                string landing = Path(_root) + "/CP_landing", endGoal = Path(_root) + "/Goal";
                string gateTarget = _gatePanel != null ? Path(_gatePanel) : Path(_root) + "/Goal";
                if (set == null) lines.Add($"NO {ObjectivesBuilder.AssetPath}: run Shift Jam > Objectives > Build Objectives, then this again (no words changed)");
                else
                {
                    if (set.goal == OldGoal) { set.goal = _s.goal; lines.Add($"goal \"{OldGoal}\" -> \"{_s.goal}\""); }
                    else lines.Add($"goal kept as \"{set.goal}\"");
                    var steps = new List<ObjectiveStep>(set.steps ?? new ObjectiveStep[0]);
                    ObjectiveStep sill = steps.Find(x => x != null && x.id == SillId);
                    if (sill != null)
                    {
                        if (sill.text == OldStepWords) { sill.text = _s.stepOutWindow; lines.Add($"step 'sill' \"{OldStepWords}\" -> \"{_s.stepOutWindow}\""); }
                        else lines.Add($"step 'sill' words kept as \"{sill.text}\"");
                        if (NamesOldGoal(sill.doneWhen)) { sill.doneWhen = new[] { Rule(ObjectiveEvent.CheckpointClaimed, landing) }; lines.Add($"step 'sill' now done when {landing} is claimed (was the sill's Goal)"); }
                        if (string.IsNullOrWhiteSpace(sill.target)) { sill.target = Path(_window); lines.Add($"step 'sill' points at {sill.target}"); }
                    }
                    else lines.Add("NO step 'sill' on the set (left as it is)");
                    if (!steps.Exists(x => x != null && x.id == _s.fenceStepId))
                    {
                        var fence = new ObjectiveStep { id = _s.fenceStepId, text = _s.stepThroughFence, target = gateTarget, room = _s.room, doneWhen = new[] { Rule(ObjectiveEvent.GoalReached, endGoal) } };
                        int at = steps.FindIndex(x => x != null && x.id == SillId);
                        steps.Insert(at >= 0 ? at + 1 : steps.Count, fence);
                        lines.Add($"step '{_s.fenceStepId}' \"{_s.stepThroughFence}\" ADDED last-but-none after 'sill' (done when {endGoal} is reached; room {_s.room})");
                    }
                    else
                    {
                        ObjectiveStep fence = steps.Find(x => x != null && x.id == _s.fenceStepId);
                        fence.doneWhen = new[] { Rule(ObjectiveEvent.GoalReached, endGoal) };
                        if (string.IsNullOrWhiteSpace(fence.target)) fence.target = gateTarget;
                        lines.Add($"step '{_s.fenceStepId}' found: its words kept (\"{fence.text}\"), its rule set again");
                    }
                    set.steps = steps.ToArray();
                    var stars = new List<ObjectiveStar>(set.stars ?? new ObjectiveStar[0]);
                    ObjectiveStar sillStar = stars.Find(x => x != null && x.id == SillId);
                    if (sillStar != null)
                    {
                        if (sillStar.title == OldStarTitle) { sillStar.title = _s.sillStarTitle; lines.Add($"star 'sill' \"{OldStarTitle}\" -> \"{_s.sillStarTitle}\""); }
                        else lines.Add($"star 'sill' title kept as \"{sillStar.title}\"");
                        if (NamesOldGoal(sillStar.doneWhen)) { sillStar.doneWhen = new[] { Rule(ObjectiveEvent.CheckpointClaimed, landing) }; lines.Add($"star 'sill' now earned when {landing} is claimed (landing in the garden)"); }
                    }
                    if (!stars.Exists(x => x != null && x.id == _s.lostStarId))
                    {
                        var star = new ObjectiveStar { id = _s.lostStarId, title = _s.lostStarTitle, room = _s.room, doneWhen = new ObjectiveRule[0] };
                        int at = stars.FindLastIndex(x => x != null && ChartWords.SameRoom(x.room, _s.room));
                        stars.Insert(at >= 0 ? at + 1 : stars.Count, star);
                        lines.Add($"star '{_s.lostStarId}' \"{_s.lostStarTitle}\" ({_s.room}) ADDED: earned through the lost toy's hook (Build Objectives lists it as NO RULE, as the claw's)");
                    }
                    else lines.Add($"star '{_s.lostStarId}' found: kept");
                    set.stars = stars.ToArray();
                    // the zone, after the window's row with its order
                    var zones = new List<ChartZone>(set.zones ?? new ChartZone[0]);
                    if (!zones.Exists(z => z != null && z.id == _s.zoneId))
                    {
                        int w = zones.FindIndex(z => z != null && z.id == "window");
                        var zone = new ChartZone { id = _s.zoneId, title = _s.zoneTitle, room = _s.room, order = w >= 0 ? zones[w].order : zones.Count, scene = ObjectiveSet.House };
                        zones.Insert(w >= 0 ? w + 1 : zones.Count, zone);
                        lines.Add($"zone '{_s.zoneId}' \"{_s.zoneTitle}\" ADDED after the window's row");
                    }
                    else lines.Add($"zone '{_s.zoneId}' found: kept");
                    set.zones = zones.ToArray();
                    // the arrival card: his words kept; its box is the yard's (as the trigger under the root)
                    Bounds box = RenderlessBox(_root.Find("Arrival " + _s.zoneId));
                    var arrivals = new List<ArrivalWords>(set.arrivals ?? new ArrivalWords[0]);
                    ArrivalWords card = arrivals.Find(a => a != null && a.id == _s.zoneId);
                    if (card == null)
                    {
                        card = new ArrivalWords { id = _s.zoneId, title = _s.arrivalTitle, line = _s.arrivalLine, colour = _s.arrivalColour, onGround = true };
                        arrivals.Add(card);
                        lines.Add($"arrival card '{_s.zoneId}' \"{_s.arrivalTitle}\" / \"{_s.arrivalLine}\" ADDED (waits for the ground)");
                    }
                    else lines.Add($"arrival card '{_s.zoneId}' found: its words kept, its box set to the yard's");
                    card.centre = box.center; card.size = box.size;
                    set.arrivals = arrivals.ToArray();
                    var places = new List<CorePlace>(set.corePlaces ?? new CorePlace[0]);
                    if (!places.Exists(p => p != null && p.core == "CoreF_Garden")) { places.Add(new CorePlace { core = "CoreF_Garden", where = _s.corePlaceWords }); lines.Add($"core place 'CoreF_Garden' \"{_s.corePlaceWords}\" ADDED"); }
                    set.corePlaces = places.ToArray();
                    EditorUtility.SetDirty(set);
                    AssetDatabase.SaveAssetIfDirty(set);
                }
                // the first-minute goal line: OpeningSettings has no asset yet (the game used its code defaults)
                var opening = AssetDatabase.LoadAssetAtPath<OpeningSettings>(OpeningPath);
                bool madeOpening = opening == null;
                if (madeOpening) { opening = ScriptableObject.CreateInstance<OpeningSettings>(); AssetDatabase.CreateAsset(opening, OpeningPath); }
                if (opening.goalLine == OldGoalLine) { opening.goalLine = _s.openingGoalLine; lines.Add($"{(madeOpening ? "MADE " + OpeningPath + " (every other number and word as the code's defaults); " : "")}goal line \"{OldGoalLine}\" -> \"{_s.openingGoalLine}\""); }
                else lines.Add($"goal line kept as \"{opening.goalLine}\"");
                EditorUtility.SetDirty(opening);
                AssetDatabase.SaveAssetIfDirty(opening);
                // the end card's title
                GameFlow flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
                Transform titleT = flow != null ? flow.transform.Find("EndCard/Title") : null;
                TMP_Text title = titleT != null ? titleT.GetComponent<TMP_Text>() : null;
                if (title == null) lines.Add("NO end card title found (HUD/EndCard/Title): not changed");
                else if (string.IsNullOrEmpty(_s.endCardTitle)) lines.Add($"end card title left as \"{title.text}\" (End Card Title is empty)");
                else if (title.text == OldEndCard) { title.text = _s.endCardTitle; EditorUtility.SetDirty(title); lines.Add($"end card title \"{OldEndCard}\" -> \"{_s.endCardTitle}\" (on the HUD in Greybox)"); }
                else lines.Add($"end card title kept as \"{title.text}\"");
                _log.AppendLine("WORDS (placeholders; written only over the old words): " + string.Join("; ", lines) + ".");
            }

            private static bool NamesOldGoal(ObjectiveRule[] rules)
            {
                if (rules == null) return false;
                foreach (ObjectiveRule r in rules)
                    if (r != null && r.objects != null)
                        foreach (string o in r.objects) if (o != null && o.Trim().Trim('/') == OldGoalPath) return true;
                return false;
            }

            private static ObjectiveRule Rule(ObjectiveEvent when, string path) => new ObjectiveRule { when = when, objects = new[] { path }, scene = ObjectiveSet.House, needed = 1 };

            // -------------------------------------------------------------------------------------------- the light and the bend

            private void LightAndBend()
            {
                Transform sun = _r6.Find("GardenSun");
                Light l = sun != null ? sun.GetComponent<Light>() : null;
                if (!_s.tuneSun) _log.AppendLine("LIGHT: Tune Sun is unticked: the garden sun left alone.");
                else if (l == null) _log.AppendLine("LIGHT: NO R6_Window/GardenSun light: nothing tuned.");
                else
                {
                    string was = $"{F(sun.position)} colour {l.color} intensity {l.intensity} range {l.range}";
                    sun.position = P(0f, _wallV * 0.5f) + _right * _s.sunFromYard.x + Vector3.up * _s.sunFromYard.y + _fwd * _s.sunFromYard.z;
                    l.color = _s.sunColour; l.intensity = _s.sunIntensity; l.range = _s.sunRange;
                    EditorUtility.SetDirty(l); EditorUtility.SetDirty(sun);
                    float mid = Vector3.Distance(sun.position, P(0f, _wallV * 0.5f));
                    _log.AppendLine($"LIGHT: GardenSun was {was}; now at {F(sun.position)} (low in the east: morning), colour {_s.sunColour}, intensity {_s.sunIntensity}, range {_s.sunRange}: about {_s.sunIntensity / Mathf.Max(1f, mid * mid):F2} on the yard's middle ({mid:F0} away). No shadows (as before).");
                }
                WorldBend bend = _r6.GetComponentInChildren<WorldBend>(true);
                if (bend == null) { _log.AppendLine("BEND: no WorldBend under R6_Window: nothing to change (the garden is flat)."); return; }
                var so = new SerializedObject(bend);
                SerializedProperty rest = so.FindProperty("restCurvature");
                if (rest == null) { _log.AppendLine("BEND: WorldBend has no Rest Curvature: not changed."); return; }
                float was2 = rest.floatValue, now = _s.flattenBend ? 0f : _s.roundBend;
                rest.floatValue = now;
                so.ApplyModifiedPropertiesWithoutUndo();
                _log.AppendLine($"BEND: WorldBend > Rest Curvature {was2} -> {now} ({(_s.flattenBend ? "flat: the garden is walked on now, and the new pieces do not bend" : "ROUND (Flatten Bend unticked): the new pieces float over the bent lawn away from the window")}). The size shift's pulse is unchanged.");
            }

            // -------------------------------------------------------------------------------------------- the checks

            private void Checks()
            {
                // the window open for the walk out (put back after)
                bool wasOn = _wmc.enabled;
                _wmc.enabled = false;
                if (_frame != null) _frame.SetActive(true);
                Physics.SyncTransforms();
                var walk = new List<Vector3> { _sillPoint };
                walk.AddRange(_windowWalk);
                string outWindow = Walk(walk, "the window", out float lastY);
                float drop = lastY - _groundY;
                _wmc.enabled = wasOn;
                if (_frame != null) _frame.SetActive(false);
                Physics.SyncTransforms();
                _log.AppendLine($"CHECK THE WAY OUT (the toy, the window open): {outWindow}. Then {drop:F1} down to the lawn: a free fall from there breaks a toy (over {_fatalDrop:F0}): the toy glides (its plane, or the spare one on the sill); the kid does not fit the window.");

                // the garden: from the landing through the maze, over the log, to a gap in the back wall and past it
                var way = new List<Vector3> { PointOn(0f, _m0 - 30f) };
                foreach (Vector2 uv in _mazeRoute) way.Add(PointOn(uv.x, uv.y));
                way.Add(PointOn(0f, _m1 + 5f));   // along the maze's back to the path's clear strip
                way.Add(PointOn(0f, _pondV - _pondR - _s.crossingOverhang - 2f));
                way.Add(PointOn(0f, _pondV - _pondR - _s.crossingOverhang + 1.5f));
                way.Add(PointOn(0f, _pondV + _pondR + _s.crossingOverhang - 1.5f));
                way.Add(PointOn(0f, _pondV + _pondR + _s.crossingOverhang + 2f));
                if (_gapPoint != Vector3.zero)
                {
                    Vector2 g = UV(_gapPoint);
                    way.Add(PointOn(0f, _wallV - 20f));   // up the path's clear strip, then along the wall to the gap
                    way.Add(PointOn(g.x, _wallV - 20f));
                    way.Add(PointOn(g.x, _wallV + 6f));
                }
                way.Add(_endPoint);
                _log.AppendLine($"CHECK THE GARDEN (the toy, from the landing lawn through the maze's way, over the crossing, through the Gate Panel's widest gap, into the end): {Walk(way, "the garden", out _)}.");

                // the gap: the toy's body passes, the kid's does not (standing or crouched)
                if (_gapPoint != Vector3.zero)
                {
                    Vector3 g = new Vector3(_gapPoint.x, GroundAt(_gapPoint), _gapPoint.z);
                    string toy = Blockers(g, _toyR, _toyH), kid = Blockers(g, _kidR, _kidH), crouch = Blockers(g, _kidR, _kidCrouchH);
                    _log.AppendLine($"CHECK THE GAP at {F(g)} ({_gapWidth:F1} wide): the toy's body {(toy == null ? "PASSES" : "is BLOCKED by " + toy)}; the kid's {(kid != null ? "does not (" + kid + ")" : "PASSES: WRONG")}; crouched {(crouch != null ? "does not" : "PASSES: WRONG")}.");
                }
                else _log.AppendLine("CHECK THE GAP: NOT checked (no gap measured in the Gate Panel).");

                // the kid over the back boundary: its jump from the lawn, and from anything it could stand on
                float lowest = float.MaxValue; string lowName = "";
                foreach (var t in _boundaryTops) if (t.top < lowest) { lowest = t.top; lowName = t.name; }
                var risky = new List<string>();
                foreach (var st in _standable)
                {
                    float top = st.box.max.y - _groundY;
                    if (top + _kidJump <= lowest + 2f) continue;
                    float reach = KidReach(top, lowest);
                    float toBack = _wallV - UV(st.box.center).y, toSide = _halfW - Mathf.Abs(UV(st.box.center).x);
                    if (Mathf.Min(toBack, toSide) < reach) risky.Add($"{st.name} ({top:F0} tall, {Mathf.Min(toBack, toSide):F0} from a boundary)");
                }
                _log.AppendLine($"CHECK THE KID OVER THE BOUNDARY: the kid jumps {_kidJump:F0}; the lowest boundary is {lowName} at {(lowest < float.MaxValue ? lowest.ToString("F0") : "?")}: {(lowest > _kidJump + 1f ? "NOT jumpable from the lawn" : "JUMPABLE FROM THE LAWN: raise it")}. Things a kid could stand on and jump out from: {(risky.Count == 0 ? "none" : string.Join(", ", risky))}{(risky.Count > 0 ? (_s.capHeight > 0f ? " (the caps stop those jumps)" : " (NO caps: such a jump gets out)") : "")}.");

                // floating or sunk: every placed piece's foot on what is under it
                var off = new List<string>(); int looked = 0;
                foreach (var pair in _placed)
                {
                    if (pair.Key == "checkpoints" || pair.Key == "arrival" || pair.Key == "end" || pair.Key == "spare plane" || pair.Key == "core" || pair.Key == "window ramp" || pair.Key == "window step" || pair.Key == "crossing" || pair.Key == "water plants" || pair.Key == "pond" || pair.Key == "gate panel" || pair.Key == "lost toy" || pair.Key == "flowers") continue;
                    foreach (GameObject g in pair.Value)
                    {
                        if (g == null) continue;
                        looked++;
                        float under = GroundAt(g.transform.position);
                        float gap = g.transform.position.y - under;
                        if (pair.Key == "path stones") gap -= 0.01f;
                        if (Mathf.Abs(gap) > Tight) off.Add($"{g.name} {(gap > 0 ? "floats" : "sinks")} {Mathf.Abs(gap):F2}");
                    }
                }
                _log.AppendLine($"CHECK FLOATING OR SUNK: {looked} pieces stood by their foot (the pack's pivot) on the lawn under them: {(off.Count == 0 ? "every one on it (within " + Tight + ")" : string.Join(", ", off))}. By design: the log sunk {_s.crossingSink}, the flowers and the lost toy on the soil, the pads on the water.");

                // nothing in the window's way out, nor in the glide's
                string win = InBox(P(0f, 20f) + Vector3.up * 40f, new Vector3(70f, 70f, 38f), "the window's way out");
                string lane = InBox(P(0f, _m0 * 0.5f + 4f) + Vector3.up * 28f, new Vector3(_s.glideLaneWidth, 50f, _m0 - 12f), "the glide's way");
                _log.AppendLine($"CHECK THE WINDOW'S WAY OUT (70 across, from the wall to 38 out, the lawn +5 to +75): {win}. THE GLIDE'S WAY ({_s.glideLaneWidth} across, to the maze, up to 53): {lane}.");
            }

            /// <summary>How far out (across the lawn) a kid jumping from <paramref name="from"/> high stays over
            /// <paramref name="height"/>, at a sprint.</summary>
            private float KidReach(float from, float height)
            {
                float g = Mathf.Max(0.01f, _kidGravity), v = Mathf.Sqrt(2f * g * _kidJump), sprint = _kidSprint;
                float over = from + _kidJump - height;
                if (over <= 0f) return 0f;
                float t = v / g + Mathf.Sqrt(2f * over / g);
                return t * sprint;
            }

            private string Walk(List<Vector3> points, string what, out float lastY)
            {
                lastY = _groundY;
                if (points.Count < 2) return "NOT walked (no points)";
                float prevY = float.NaN, walked = 0f, maxUp = 0f, maxDown = 0f;
                int samples = 0;
                for (int i = 0; i + 1 < points.Count; i++)
                {
                    Vector3 a = points[i], b = points[i + 1];
                    float len = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                    int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
                    for (int k = (i == 0 ? 0 : 1); k <= n; k++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, k / (float)n);
                        float top = Mathf.Max(a.y, b.y) + 3f;
                        if (!Physics.Raycast(new Vector3(p.x, top, p.z), Vector3.down, out RaycastHit h, top - Mathf.Min(a.y, b.y) + 20f, ~0, QueryTriggerInteraction.Ignore))
                            return $"FAILS at {F(p)} after {walked:F0}: nothing under the feet";
                        if (h.transform.IsChildOf(_player.transform)) return $"FAILS at {F(p)}: the Player stands there (move it and run this again)";
                        float y = h.point.y;
                        if (h.normal.y < Mathf.Cos(45f * Mathf.Deg2Rad) + 0.01f && !float.IsNaN(prevY) && y > prevY + 0.3f) return $"FAILS at {F(p)} after {walked:F0}: a slope steeper than the toy walks ({h.collider.name})";
                        if (!float.IsNaN(prevY))
                        {
                            float d = y - prevY;
                            maxUp = Mathf.Max(maxUp, d); maxDown = Mathf.Max(maxDown, -d);
                            if (d > _toyJump - 0.15f) return $"FAILS at {F(p)} after {walked:F0}: a step up of {d:F2} onto {h.collider.name} (the toy jumps {_toyJump:F1})";
                            if (-d > _fatalDrop - _toyJump) return $"FAILS at {F(p)} after {walked:F0}: a drop of {-d:F1} (over {_fatalDrop - _toyJump:F0}: a toy may break)";
                        }
                        if (_hazard != null && h.collider == _groundCol)
                        {
                            Vector2 off = UV(p) - new Vector2(0f, _pondV);
                            if (off.magnitude < _hazardR) return $"FAILS at {F(p)} after {walked:F0}: the toy walks in the pond";
                        }
                        string blocked = Blockers(h.point, _toyR, _toyH);
                        if (blocked != null) return $"FAILS at {F(p)} after {walked:F0}: the toy's body hits {blocked}";
                        prevY = y; lastY = y; samples++;
                        if (k > 0) walked += len / n;
                    }
                }
                return $"OK: {walked:F0} walked in {samples} steps through {points.Count} points; the biggest step up {maxUp:F2} (the toy jumps {_toyJump:F1}), the biggest drop {maxDown:F1}";
            }

            /// <summary>What a standing body (its feet at <paramref name="feet"/>) would overlap, or null when it is free.</summary>
            private string Blockers(Vector3 feet, float r, float tall)
            {
                Vector3 a = feet + Vector3.up * (r + 0.06f), b = feet + Vector3.up * Mathf.Max(r + 0.06f, tall - r);
                var names = new List<string>();
                foreach (Collider c in Physics.OverlapCapsule(a, b, r, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (c == null || c.transform.IsChildOf(_player.transform) || c == _groundCol) continue;
                    if (c.gameObject.layer == Layers.CarGuard && Layers.CarGuard >= 0) continue;
                    if (!names.Contains(c.name)) names.Add(c.name);
                    if (names.Count >= 3) break;
                }
                return names.Count == 0 ? null : string.Join(", ", names) + (names.Count >= 3 ? ", ..." : "");
            }

            private string InBox(Vector3 centre, Vector3 size, string what)
            {
                var names = new List<string>();
                foreach (Collider c in Physics.OverlapBox(centre, size * 0.5f, _turn, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (c == null || c == _groundCol || c.transform.IsChildOf(_player.transform) || c.transform.IsChildOf(_window)) continue;
                    if (Layers.CarGuard >= 0 && c.gameObject.layer == Layers.CarGuard) continue;
                    string n = Path(c.transform);
                    if (!names.Contains(n)) names.Add(n);
                }
                return names.Count == 0 ? "CLEAR" : "IN THE WAY: " + string.Join(", ", names);
            }

            // -------------------------------------------------------------------------------------------- the summary

            private void Summary()
            {
                var all = new StringBuilder();
                foreach (var pair in _placed)
                {
                    all.Append(pair.Key).Append(" (").Append(pair.Value.Count).Append("): ");
                    int i = 0;
                    foreach (GameObject g in pair.Value)
                    {
                        if (g == null) continue;
                        if (i++ > 0) all.Append(", ");
                        Vector2 uv = UV(g.transform.position);
                        all.Append(g.name).Append(" u ").Append(uv.x.ToString("F0")).Append(" v ").Append(uv.y.ToString("F0"));
                    }
                    all.AppendLine(".");
                }
                _log.Append("PLACED (u across from the window's line, v out from the wall):\n").Append(all);
                _log.AppendLine($"NUMBERS: yard {_s.yardWidth} x {_s.yardDepth}, landing {_s.landingDepth}, glide's way {_s.glideLaneWidth}, path {_s.pathWidth}; grass {_s.grassHeight} (+-{_s.grassHeightJitter:P0}), maze {_s.mazeColumns} x {_s.mazeRows}, paths {_s.mazePathWidth}, walls {_s.mazeWallThickness}, seed {_s.mazeSeed}; pond gap {_s.pondGap}, pond {_s.pondDiameter}, water lift {_s.waterLift}, overhang {_s.crossingOverhang}, sink {_s.crossingSink}, stones {_s.stoneTop}/{_s.stoneWidth}; bed {_s.flowerbedPlace} size {_s.flowerbedSize}, clearing {_s.lostToyClearing}, hop {_s.lostToyHop} in {_s.lostToyHopSeconds} s; gate panel {_s.gatePanelHeight}, end {_s.endDepth} (any size {_s.endAnySize}), core {_s.corePlace}, caps and seals {_s.capHeight}; boundary {_s.boundaryHeight} every {_s.boundary.spacing}; backdrop {_s.backdrop.count} at {_s.backdropHeight}, {_s.backdrop.spacing} apart; lawn {_s.lawnTile} a repeat; glass fade {_s.glassFadeSeconds} s, ramp {_s.rampSlope} degrees; landing checkpoint from {_s.landingClaimFrom}; sun {_s.sunColour} {_s.sunIntensity} range {_s.sunRange} at {_s.sunFromYard}; seed {_s.seed}; smoothness {_s.swapSmoothness}.");
                _log.AppendLine("OFF SWITCHES (Resources/BackyardSettings): Open On Sill (unticked: open from the start), Spare Plane, Swap Materials, Lawn On, End Any Size, Tune Sun, Flatten Bend, Inside Trees Off, Cap Height 0 (the seals stay, 10 over the rows); an empty list places nothing of that role. The whole backyard: delete 'Room/Course/R6_Window/" + RootName + "' and switch the old Goal on again (its words: NOTES.md). The words: rewrite them on ObjectiveSet, OpeningSettings and the HUD's end card.");
                _log.AppendLine("TEST: Play from a save before the window (or the playroom). As the kid, jump onto the sill: the window's glass fades (CP_sill). E on core D: the toy. Walk to the window's other end, up the ruler onto the step, out through the pane; Jump past the edge to glide (no plane? take the one on the ledge: E). Land: the arrival card, the step 'Out the window' and the star. Through the maze (or grow: F), over the log (fall in: back to CP_pond), the lost toy in the flowerbed (the star), core F by the wall (shrink if a kid), between the Gate Panel's slats: the end card over the garden.");
            }

            // -------------------------------------------------------------------------------------------- helpers

            private void Note(string role, GameObject g)
            {
                if (g == null) return;
                if (!_placed.TryGetValue(role, out var list)) _placed[role] = list = new List<GameObject>();
                list.Add(g);
            }

            private List<GameObject> Placed(string role) => _placed.TryGetValue(role, out var list) ? list : new List<GameObject>();

            private static GameObject First(BackyardSettings.Role role)
            {
                if (role == null || role.prefabs == null) return null;
                foreach (GameObject p in role.prefabs) if (p != null) return p;
                return null;
            }

            private GameObject Pick(BackyardSettings.Role role)
            {
                if (role == null || role.prefabs == null || role.prefabs.Length == 0) return null;
                for (int i = 0; i < role.prefabs.Length; i++)
                {
                    GameObject p = role.prefabs[_rng.Next(role.prefabs.Length)];
                    if (p != null) return p;
                }
                return First(role);
            }

            private float Scale(BackyardSettings.Role role) => Mathf.Lerp(role.scaleMin, role.scaleMax, (float)_rng.NextDouble());

            private GameObject Spawn(GameObject prefab, string name, Transform parent, Vector3 at, Quaternion turn, float scale)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                g.name = name;
                g.transform.localScale = Vector3.one * scale;
                g.transform.SetPositionAndRotation(at, turn);
                return g;
            }

            /// <summary>The turn that lays the prefab's long side (x or z of its meshes) along <paramref name="dir"/>.</summary>
            private Quaternion LongTurn(GameObject prefab, Vector3 dir)
            {
                Quaternion look = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z).normalized, Vector3.up);
                return LongAxisIsX(prefab) ? look * Quaternion.Euler(0f, -90f, 0f) : look;
            }

            private bool LongAxisIsX(GameObject prefab) { Bounds b = PrefabBox(prefab); return b.size.x > b.size.z; }

            /// <summary>The prefab's meshes' box in its own space at scale 1 (measured once on a copy far below the world).</summary>
            private Bounds PrefabBox(GameObject prefab)
            {
                if (prefab == null) return new Bounds(Vector3.zero, Vector3.one);
                if (_prefabBoxes.TryGetValue(prefab, out Bounds b)) return b;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                g.transform.SetPositionAndRotation(new Vector3(0f, -5000f, 0f), Quaternion.identity);
                g.transform.localScale = Vector3.one;
                b = LocalBox(g.transform);
                Object.DestroyImmediate(g);
                _prefabBoxes[prefab] = b;
                return b;
            }

            private static string PrefabName(GameObject g)
            {
                GameObject src = PrefabUtility.GetCorrespondingObjectFromSource(g);
                return src != null ? src.name : g.name;
            }

            private static float Along(Bounds b, Vector3 dir)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (Vector3 c in Corners(b)) { float d = Vector3.Dot(c, dir); lo = Mathf.Min(lo, d); hi = Mathf.Max(hi, d); }
                return hi - lo;
            }

            private float WalkTopAt(Vector3 from, Transform on)
            {
                foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.transform.IsChildOf(on)) return h.point.y;
                return float.NaN;
            }

            private static Bounds RenderlessBox(Transform t)
            {
                if (t == null) return new Bounds(Vector3.zero, Vector3.one);
                var bc = t.GetComponent<BoxCollider>();
                if (bc == null) return new Bounds(t.position, Vector3.one);
                Vector3 half = Vector3.Scale(bc.size * 0.5f, t.lossyScale);
                Matrix4x4 m = Matrix4x4.Rotate(t.rotation);
                var ext = new Vector3(
                    Mathf.Abs(m.m00) * half.x + Mathf.Abs(m.m01) * half.y + Mathf.Abs(m.m02) * half.z,
                    Mathf.Abs(m.m10) * half.x + Mathf.Abs(m.m11) * half.y + Mathf.Abs(m.m12) * half.z,
                    Mathf.Abs(m.m20) * half.x + Mathf.Abs(m.m21) * half.y + Mathf.Abs(m.m22) * half.z);
                return new Bounds(t.TransformPoint(bc.center), ext * 2f);
            }

            // ---- the slats: a flat piece seen face on, its solid parts as boxes

            /// <summary>The piece's mesh seen face on (along its thin side), as a grid of solid cells; null when unreadable.</summary>
            private bool[,] Raster(Mesh mesh, out bool alongX, out float l0, out float dl, out float y0, out float dy)
            {
                Bounds mb = mesh != null ? mesh.bounds : new Bounds();
                alongX = mb.size.x >= mb.size.z;
                l0 = alongX ? mb.min.x : mb.min.z; y0 = mb.min.y;
                float len = alongX ? mb.size.x : mb.size.z;
                int nl = Mathf.Clamp(Mathf.CeilToInt(len / 0.01f), 16, 1600), ny = Mathf.Clamp(Mathf.CeilToInt(mb.size.y / 0.02f), 8, 400);
                dl = len / nl; dy = Mathf.Max(1e-4f, mb.size.y / ny);
                if (mesh == null || !mesh.isReadable) return null;
                if (_rasters.TryGetValue(mesh, out bool[,] cached)) return cached;
                var solid = new bool[nl, ny];
                Vector3[] v = mesh.vertices;
                int[] tri = mesh.triangles;
                bool ax = alongX; float L0 = l0, DL = dl, Y0 = y0, DY = dy;
                Vector2 Flat(Vector3 p) => ax ? new Vector2(p.x, p.y) : new Vector2(p.z, p.y);
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    Vector2 a = Flat(v[tri[i]]), b = Flat(v[tri[i + 1]]), c = Flat(v[tri[i + 2]]);
                    float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (Mathf.Abs(area) < 1e-9f) continue;   // seen edge on (a slat's side): it covers nothing face on
                    int i0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - L0) / DL), 0, nl - 1), i1 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - L0) / DL), 0, nl - 1);
                    int j0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - Y0) / DY), 0, ny - 1), j1 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - Y0) / DY), 0, ny - 1);
                    for (int x = i0; x <= i1; x++)
                        for (int y = j0; y <= j1; y++)
                        {
                            if (solid[x, y]) continue;
                            var p = new Vector2(L0 + (x + 0.5f) * DL, Y0 + (y + 0.5f) * DY);
                            float w0 = (b.x - p.x) * (c.y - p.y) - (b.y - p.y) * (c.x - p.x);
                            float w1 = (c.x - p.x) * (a.y - p.y) - (c.y - p.y) * (a.x - p.x);
                            float w2 = (a.x - p.x) * (b.y - p.y) - (a.y - p.y) * (b.x - p.x);
                            if ((w0 >= 0f && w1 >= 0f && w2 >= 0f) || (w0 <= 0f && w1 <= 0f && w2 <= 0f)) solid[x, y] = true;
                        }
                }
                _rasters[mesh] = solid;
                return solid;
            }

            /// <summary>The solid parts of a flat piece (a slatted wall) as boxes in its mesh's own space, each through its whole
            /// thickness: upward first (a slat), then across. Null when unreadable or too many parts.</summary>
            private List<Bounds> Slats(Mesh mesh, out string why)
            {
                why = "";
                if (mesh == null) { why = "no mesh"; return null; }
                if (_slats.TryGetValue(mesh, out var cached)) return cached;
                bool[,] solid = Raster(mesh, out bool alongX, out float l0, out float dl, out float y0, out float dy);
                if (solid == null) { why = "its mesh cannot be read"; return null; }
                int nl = solid.GetLength(0), ny = solid.GetLength(1);
                var taken = new bool[nl, ny];
                var parts = new List<Bounds>();
                Bounds mb = mesh.bounds;
                float thick = alongX ? mb.size.z : mb.size.x, mid = alongX ? mb.center.z : mb.center.x;
                for (int x = 0; x < nl; x++)
                    for (int y = 0; y < ny; y++)
                    {
                        if (!solid[x, y] || taken[x, y]) continue;
                        int y1 = y;
                        while (y1 + 1 < ny && solid[x, y1 + 1] && !taken[x, y1 + 1]) y1++;
                        int x1 = x;
                        while (x1 + 1 < nl)
                        {
                            bool full = true;
                            for (int k = y; k <= y1; k++) if (!solid[x1 + 1, k] || taken[x1 + 1, k]) { full = false; break; }
                            if (!full) break;
                            x1++;
                        }
                        for (int a = x; a <= x1; a++) for (int b = y; b <= y1; b++) taken[a, b] = true;
                        float la = l0 + x * dl, lb = l0 + (x1 + 1) * dl, ya = y0 + y * dy, yb = y0 + (y1 + 1) * dy;
                        parts.Add(alongX
                            ? new Bounds(new Vector3((la + lb) * 0.5f, (ya + yb) * 0.5f, mid), new Vector3(lb - la, yb - ya, thick))
                            : new Bounds(new Vector3(mid, (ya + yb) * 0.5f, (la + lb) * 0.5f), new Vector3(thick, yb - ya, lb - la)));
                        if (parts.Count > MaxSlatBoxes) { why = "more than " + MaxSlatBoxes + " solid parts"; return null; }
                    }
                _slats[mesh] = parts;
                return parts;
            }

            /// <summary>The gate's open middle: the empty run round its middle at a third of its height, and how high it is open.</summary>
            private bool Opening(Mesh mesh, out float o0, out float o1, out bool alongX, out float openTop)
            {
                o0 = o1 = openTop = 0f;
                bool[,] solid = Raster(mesh, out alongX, out float l0, out float dl, out float y0, out float dy);
                if (solid == null) return false;
                int nl = solid.GetLength(0), ny = solid.GetLength(1);
                int row = Mathf.Clamp(ny / 3, 0, ny - 1), middle = nl / 2;
                if (solid[middle, row]) return false;
                int a = middle, b = middle;
                while (a > 0 && !solid[a - 1, row]) a--;
                while (b < nl - 1 && !solid[b + 1, row]) b++;
                o0 = l0 + a * dl; o1 = l0 + (b + 1) * dl;
                int top = row;
                while (top < ny - 1 && !solid[middle, top + 1]) top++;
                openTop = y0 + (top + 1) * dy;
                return o1 - o0 > 0.05f;
            }

            /// <summary>The gaps between a slatted piece's slats near its foot (in the world): the widest one's middle, its width,
            /// the narrowest, and how many.</summary>
            private bool Gaps(MeshFilter mf, out Vector3 widestMid, out float widest, out float narrowest, out int count)
            {
                widestMid = Vector3.zero; widest = 0f; narrowest = float.MaxValue; count = 0;
                Mesh mesh = mf.sharedMesh;
                bool[,] solid = Raster(mesh, out bool alongX, out float l0, out float dl, out float y0, out float dy);
                if (solid == null) return false;
                int nl = solid.GetLength(0), ny = solid.GetLength(1);
                int row = Mathf.Clamp(Mathf.CeilToInt(0.12f * ny), 0, ny - 1);   // a little over the foot
                Bounds mb = mesh.bounds;
                float mid = alongX ? mb.center.z : mb.center.x;
                int x = 0;
                while (x < nl && !solid[x, row]) x++;   // the open end before the first slat is not a gap
                while (x < nl)
                {
                    while (x < nl && solid[x, row]) x++;
                    int start = x;
                    while (x < nl && !solid[x, row]) x++;
                    if (x >= nl) break;   // open to the piece's end: not between two slats
                    Vector3 la = alongX ? new Vector3(l0 + start * dl, y0 + row * dy, mid) : new Vector3(mid, y0 + row * dy, l0 + start * dl);
                    Vector3 lb = alongX ? new Vector3(l0 + x * dl, y0 + row * dy, mid) : new Vector3(mid, y0 + row * dy, l0 + x * dl);
                    Vector3 wa = mf.transform.TransformPoint(la), wb = mf.transform.TransformPoint(lb);
                    float w = Vector3.Distance(wa, wb);
                    count++;
                    narrowest = Mathf.Min(narrowest, w);
                    if (w > widest) { widest = w; widestMid = (wa + wb) * 0.5f; }
                }
                if (count == 0) narrowest = 0f;
                return count > 0;
            }

            private static void Listener(SerializedProperty call, Object target, string typeName, string method)
            {
                call.FindPropertyRelative("m_Target").objectReferenceValue = target;
                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeName;
                call.FindPropertyRelative("m_MethodName").stringValue = method;
                call.FindPropertyRelative("m_Mode").intValue = ModeVoid;
                call.FindPropertyRelative("m_CallState").intValue = RuntimeOnly;
                SerializedProperty args = call.FindPropertyRelative("m_Arguments");
                args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
                args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = "";
                args.FindPropertyRelative("m_IntArgument").intValue = 0;
                args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
                args.FindPropertyRelative("m_StringArgument").stringValue = "";
                args.FindPropertyRelative("m_BoolArgument").boolValue = false;
            }
        }

        // ================================================================================================ shared helpers

        /// <summary>The world box of what a thing draws (particles left out).</summary>
        private static Bounds RenderBox(GameObject go)
        {
            bool any = false;
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>The box of a thing's meshes in its own space (its children's meshes brought into it).</summary>
        private static Bounds LocalBox(Transform root)
        {
            bool any = false;
            var box = new Bounds(Vector3.zero, Vector3.zero);
            void Add(Transform part, Bounds local)
            {
                Matrix4x4 m = root.worldToLocalMatrix * part.localToWorldMatrix;
                foreach (Vector3 c in Corners(local))
                {
                    Vector3 p = m.MultiplyPoint3x4(c);
                    if (!any) { box = new Bounds(p, Vector3.zero); any = true; } else box.Encapsulate(p);
                }
            }
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) Add(mf.transform, mf.sharedMesh.bounds);
            foreach (SkinnedMeshRenderer s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.sharedMesh != null) Add(s.transform, s.sharedMesh.bounds);
            return box;
        }

        private static Vector3[] Corners(Bounds b)
        {
            Vector3 n = b.min, x = b.max;
            return new[]
            {
                new Vector3(n.x, n.y, n.z), new Vector3(x.x, n.y, n.z), new Vector3(n.x, x.y, n.z), new Vector3(x.x, x.y, n.z),
                new Vector3(n.x, n.y, x.z), new Vector3(x.x, n.y, x.z), new Vector3(n.x, x.y, x.z), new Vector3(x.x, x.y, x.z),
            };
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static string F(Vector3 v) => $"({v.x:F1}, {v.y:F1}, {v.z:F1})";
    }
}
#endif
