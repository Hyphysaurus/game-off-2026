#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The rope's pieces (D-0170), made once and safe to run again. Three menus under Shift Jam > Traversal, each a
    /// public static method with no arguments so code can call it too, each refusing in Play with one warning:
    /// Make Rope Piece (Make): the settings asset, the rope's own material and the Rope prefab. What is already there is
    /// left alone, so his changes to any of the three are kept.
    /// Add Rope Rider To Player Prefab (AddRider): puts RopeRider on the one Player prefab, which every scene's player
    /// and the online player (a variant) take it from. It opens no scene.
    /// Place First Rope (PlaceHallRope): hangs the first rope past the top of the hallway's book steps in Greybox, at a
    /// place it measures with physics rays. It is the only one that changes the open scene, and it does not save it.
    /// WHY the first two say what the open scenes' unsaved marks were before and after: they must not change them.
    /// </summary>
    public static class RopePieces
    {
        private const string Tag = "[Rope]";
        private const string SettingsPath = "Assets/_Game/Resources/Rope.asset";
        private const string MaterialPath = "Assets/_Game/Data/Rope.mat";
        /// <summary>The Rope piece: drag it into any scene at the top level of the Hierarchy.</summary>
        public const string PrefabPath = "Assets/_Game/Prefabs/Pieces/Traversal/Rope.prefab";
        private const string NetPlayerPath = "Assets/_Game/Resources/Net/NetPlayer.prefab";   // the online player, a variant of the Player prefab (PartyBuilder)
        private const string LitShader = "Universal Render Pipeline/Lit";   // the only shader the size shift's bend swaps (WorldBend)
        private static readonly Color RopeColour = new Color(0.86f, 0.72f, 0.5f);   // undyed hemp
        private const float RopeSmoothness = 0.1f;
        private const float PieceLength = 8f;           // the piece's own length; each rope placed sets its own
        private const string TubeName = "Tube";

        // ---- the first rope: past the east end of the top of the hallway's book steps (CourseBuilder.House: the
        // hallway runs along x at z -150, floor 0, ceiling underside 14; the box top is 12.2, the landing book 12.15)
        private const string GreyboxPath = "Assets/_Game/Scenes/Greybox.unity";
        /// <summary>The first rope's name in Greybox.</summary>
        public const string HallRopeName = "Rope_HallBooks";
        private const float HallZ = -150f;              // the hallway's middle line
        private const float HallSideZ = 4f;             // the edge is measured again this far to each side of it
        private const float ScanFromX = 14f, ScanToX = 24f, ScanStep = 0.05f;   // where the top's east edge is looked for
        private const float ScanFromY = 13.9f;          // the rays start just under the ceiling (14)
        private const float TopAtLeastY = 11.9f;        // a hit this high is the top of the steps (12.15 to 12.2)
        private const int EdgeHalvings = 10;            // the edge between the last hit and the first miss is halved this often (to under a thousandth)
        private const float EdgeStraightWithin = 0.2f;  // the three measured lines may differ by this much
        private const float ExpectedEdgeX = 21.4f;      // the landing book's end (Book_01 is 16.98 long, its back end at 4.4)
        private const float ExpectedEdgeSlack = 0.3f;   // further off than this, the scene no longer matches the builder: stop
        private const float RopeOut = 1f;               // the rope hangs this far past the edge: E reaches it from the last 0.85 of the top
        private const float ExpectedCeilingY = 14f, CeilingSlack = 0.1f;
        private const float CeilingRayFromY = 13f, CeilingRayLength = 3f;
        private const float HallRopeLength = 8f;        // the contract's length: its low end 6 over the floor, out of a jumping toy's reach
        private const float BodyRadius = 0.3f;          // the toy's capsule: the rope's rest line must be free this wide
        private const float RestLineGap = 0.02f;        // the free check stops this far under the ceiling (touching it is not "in the way")
        private const float BehindFromOut = 0.5f;       // the rays that read the shape behind the rope start this far past the edge
        private const float BehindRayLength = 10f;
        private static readonly float[] BehindHeights = { 11f, 9f, 7f, 5f };

        // ================================================================== the piece

        /// <summary>Shift Jam > Traversal > Make Rope Piece: Resources/Rope.asset (the numbers), Data/Rope.mat (URP Lit,
        /// our own tracked file: no pack file is used or copied) and Prefabs/Pieces/Traversal/Rope.prefab (a root with
        /// the Rope script and its thin trigger handle, and a Tube child the rope is drawn into). Each is made only when
        /// missing. Then the prefab is checked. One [Rope] line says what happened.</summary>
        [MenuItem("Shift Jam/Traversal/Make Rope Piece (prefab, material, settings)")]
        public static void Make()
        {
            if (InPlay("Make Rope Piece")) return;
            bool[] before = UnsavedScenes();
            var did = new List<string>();

            // 1. The numbers. An asset already there is never rewritten.
            bool hadSettings = AssetDatabase.LoadAssetAtPath<RopeSettings>(SettingsPath) != null;
            WorkshopPieces.EnsureFolder(Folder(SettingsPath));
            RopeSettings settings = WorkshopPieces.SettingsAsset<RopeSettings>(SettingsPath);
            if (settings == null) { Debug.LogError($"{Tag} Make Rope Piece: FAILED: {SettingsPath} could not be made. Nothing else was done."); return; }
            if (!hadSettings) AssetDatabase.SaveAssetIfDirty(settings);
            did.Add(hadSettings ? "settings already there (left alone)" : "settings made with their defaults");

            // 2. The material: URP Lit, so the size shift's bend takes the rope with the room.
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader lit = Shader.Find(LitShader);
                if (lit == null) { Debug.LogError($"{Tag} Make Rope Piece: FAILED: the shader '{LitShader}' was not found, so {MaterialPath} was not made. The prefab was not made either."); return; }
                material = new Material(lit) { name = "Rope" };
                material.SetColor("_BaseColor", RopeColour);
                material.SetFloat("_Smoothness", RopeSmoothness);
                WorkshopPieces.EnsureFolder(Folder(MaterialPath));
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SaveAssetIfDirty(material);
                did.Add("material made");
            }
            else did.Add("material already there (left alone)");

            // 3. The prefab. One already there is left alone, so changes made to it by hand are kept.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) did.Add("prefab already there (left alone; delete it to have it made again)");
            else
            {
                // Built in a preview scene, so nothing of it is ever in the open scene. A new object cannot be born in
                // a preview scene: it is made and moved there on the next line.
                Scene preview = EditorSceneManager.NewPreviewScene();
                bool saved;
                try
                {
                    var root = new GameObject("Rope");
                    SceneManager.MoveGameObjectToScene(root, preview);
                    var grip = root.AddComponent<CapsuleCollider>();
                    grip.isTrigger = true;
                    grip.direction = 1;   // along Y
                    grip.radius = settings.gripRadius;
                    grip.height = PieceLength;
                    grip.center = new Vector3(0f, -PieceLength * 0.5f, 0f);
                    var rope = root.AddComponent<Rope>();
                    var tube = new GameObject(TubeName);
                    tube.transform.SetParent(root.transform, false);
                    var filter = tube.AddComponent<MeshFilter>();   // no mesh: the rope makes its own in Play
                    var renderer = tube.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    WorkshopPieces.Set(rope, "length", PieceLength);
                    WorkshopPieces.Set(rope, "settings", settings);
                    WorkshopPieces.Set(rope, "tube", filter);
                    WorkshopPieces.Set(rope, "grip", grip);
                    WorkshopPieces.EnsureFolder(Folder(PrefabPath));
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out saved);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
                if (!saved) { Debug.LogError($"{Tag} Make Rope Piece: FAILED: {PrefabPath} could not be saved ({string.Join("; ", did)})."); return; }
                did.Add("prefab made");
            }

            // 4. The check, on what is on disk now (also when the prefab was already there).
            string wrong = PieceFaults();
            string scenes = ScenesLine(before, UnsavedScenes());
            if (wrong.Length == 0)
                Debug.Log($"{Tag} Make Rope Piece: OK. {string.Join("; ", did)}. Checked {PrefabPath}: one Rope on the root, one trigger capsule on the root, a {TubeName} child drawn with {LitShader}, and tube, grip and settings are set. Files: {SettingsPath}, {MaterialPath}, {PrefabPath}. {scenes}");
            else
                Debug.LogError($"{Tag} Make Rope Piece: FAILED the check of {PrefabPath}: {wrong}. ({string.Join("; ", did)}.) {scenes}");
        }

        /// <summary>What is wrong with the saved Rope prefab, in words; empty when it is as the rope needs it.</summary>
        private static string PieceFaults()
        {
            var faults = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return "the prefab is not there";
            Rope[] ropes = prefab.GetComponentsInChildren<Rope>(true);
            if (ropes.Length != 1 || ropes[0].gameObject != prefab) faults.Add($"it has {ropes.Length} Rope scripts (one is wanted, on the root)");
            int triggers = 0;
            foreach (CapsuleCollider capsule in prefab.GetComponents<CapsuleCollider>()) if (capsule.isTrigger) triggers++;
            if (triggers != 1) faults.Add($"the root has {triggers} trigger capsules (one is wanted)");
            Transform tube = prefab.transform.Find(TubeName);
            var renderer = tube != null ? tube.GetComponent<MeshRenderer>() : null;
            Material drawn = renderer != null ? renderer.sharedMaterial : null;
            if (tube == null) faults.Add($"no {TubeName} child");
            else if (drawn == null || drawn.shader == null || drawn.shader.name != LitShader)
                faults.Add($"the {TubeName} is not drawn with {LitShader} (it has {(drawn == null ? "no material" : drawn.shader != null ? drawn.shader.name : "no shader")}): the size shift would not bend it");
            if (ropes.Length > 0)
            {
                var so = new SerializedObject(ropes[0]);
                foreach (string field in new[] { "tube", "grip", "settings" })
                {
                    SerializedProperty p = so.FindProperty(field);
                    if (p == null || p.objectReferenceValue == null) faults.Add($"Rope's '{field}' is not set");
                }
            }
            return string.Join("; ", faults);
        }

        // ================================================================== the rider

        /// <summary>Shift Jam > Traversal > Add Rope Rider To Player Prefab: opens the Player prefab's contents by
        /// themselves (never a scene), adds RopeRider to its root when it has none, saves it to the same file and checks
        /// that nothing else on it changed. Then it says whether the Player prefab, the online player (a variant: it
        /// takes the part from the Player prefab, its own file is not written) and the open scene's Player each have
        /// exactly one. Run again, it adds nothing and only checks. NEVER use Shift Jam > Player > Make One Player
        /// Prefab or Build Player Port for this: those rewrite scenes.</summary>
        [MenuItem("Shift Jam/Traversal/Add Rope Rider To Player Prefab")]
        public static void AddRider()
        {
            if (InPlay("Add Rope Rider To Player Prefab")) return;
            string path = PlayerPrefabBuilder.PrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogError($"{Tag} Add Rope Rider: FAILED: {path} is not there. Nothing was changed.");
                return;
            }
            bool[] before = UnsavedScenes();
            string did;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponents<RopeRider>().Length > 0) did = "the Player prefab already had it (nothing was written)";
                else
                {
                    List<string> partsBefore = Parts(root);
                    int allBefore = root.GetComponentsInChildren<Component>(true).Length;
                    root.AddComponent<RopeRider>();
                    // Nothing but the one new part may differ, or the file is not written.
                    List<string> partsAfter = Parts(root);
                    int allAfter = root.GetComponentsInChildren<Component>(true).Length;
                    partsAfter.Remove(nameof(RopeRider));
                    bool onlyThat = allAfter == allBefore + 1 && partsAfter.Count == partsBefore.Count;
                    for (int i = 0; onlyThat && i < partsBefore.Count; i++) onlyThat = partsBefore[i] == partsAfter[i];
                    if (!onlyThat)
                    {
                        Debug.LogError($"{Tag} Add Rope Rider: FAILED: adding the part changed more than itself on {path} ({allBefore} parts before, {allAfter} after). The prefab was NOT written.");
                        return;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    if (!saved)
                    {
                        Debug.LogError($"{Tag} Add Rope Rider: FAILED: {path} could not be saved (a missing script on it stops a save). The prefab was NOT written.");
                        return;
                    }
                    did = $"added to the Player prefab's root ({allBefore} parts before, {allAfter} after, the rest as they were)";
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // The check: the prefab, the online player, and the Player of the scene that is open.
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            int onPlayer = player != null ? player.GetComponents<RopeRider>().Length : 0;
            var net = AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPath);
            int onNet = net != null ? net.GetComponents<RopeRider>().Length : 0;
            bool variant = net != null && PrefabUtility.GetPrefabAssetType(net) == PrefabAssetType.Variant;
            string netSays = net == null ? $"{NetPlayerPath} is not there (not checked)"
                : $"NetPlayer has {onNet}" + (variant ? " (a variant: it takes it from the Player prefab)" : " and is NOT a variant of the Player prefab");
            int scenePlayers = 0;
            bool sceneOk = true;
            var counts = new List<string>();
            foreach (PlayerMotor motor in Object.FindObjectsByType<PlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (motor.name != "Player" || motor.transform.parent != null) continue;   // the scene's player: a root object named Player (PlayerPrefabBuilder's rule)
                int has = motor.GetComponents<RopeRider>().Length;
                scenePlayers++;
                sceneOk &= has == 1;
                counts.Add(has.ToString(CultureInfo.InvariantCulture));
            }
            string sceneSays = scenePlayers == 0 ? "no Player in the open scene (not checked)" : $"the open scene's Player has {string.Join(" and ", counts)}";
            bool ok = onPlayer == 1 && (net == null || (onNet == 1 && variant)) && sceneOk;
            string line = $"Add Rope Rider: {(ok ? "OK" : "FAILED")}. {did}. RopeRider scripts: the Player prefab has {onPlayer}; {netSays}; {sceneSays}. One each is wanted. {ScenesLine(before, UnsavedScenes())}";
            if (ok) Debug.Log($"{Tag} {line}");
            else Debug.LogError($"{Tag} {line} If only the open scene's Player is short, run this again once the editor has taken the prefab in: without the part on it, E shows no prompt at a rope and nothing says why.");
        }

        /// <summary>The names of the parts on <paramref name="go"/> itself, in order ("(missing script)" for one whose script is gone).</summary>
        private static List<string> Parts(GameObject go)
        {
            var names = new List<string>();
            foreach (Component c in go.GetComponents<Component>()) names.Add(c != null ? c.GetType().Name : "(missing script)");
            return names;
        }

        // ================================================================== the first rope

        /// <summary>Shift Jam > Traversal > Place First Rope: hangs Rope_HallBooks from the hallway's ceiling, one unit past
        /// the east edge of the top of the book steps, at the TOP LEVEL of Greybox (a hallway rebuild wipes everything
        /// under R3_SkirtingRun). It only works on Greybox, open and active, and out of Play.
        /// WHY it measures instead of using the builder's numbers: the top's east edge is the landing book's end, which
        /// comes from a pack mesh's length; a scene that has moved on from the builder must stop this, not get a rope in
        /// the wrong place. Rays straight down find the edge on three lines across the hallway (they must agree), a ray
        /// up finds the ceiling, rays west read the shape behind the rope (for STATUS), and the rope's rest line must be
        /// free of solid things. Every number used is logged. It places, selects and marks the scene changed; it does
        /// NOT save. If the rope is already there it moves nothing and says so. Any failed measure stops it with
        /// nothing placed.</summary>
        [MenuItem("Shift Jam/Traversal/Place First Rope (hallway book steps, Greybox)")]
        public static void PlaceHallRope()
        {
            if (InPlay("Place First Rope")) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != GreyboxPath)
            {
                Debug.LogError($"{Tag} Place First Rope: NOT placed: the open scene is '{scene.path}', and the first rope belongs in {GreyboxPath}. Open Greybox and run this again. Nothing was changed.");
                return;
            }
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (Transform t in sceneRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != HallRopeName) continue;
                    Debug.Log($"{Tag} Place First Rope: {HallRopeName} is already in Greybox at {Say(t.position)}{(t.parent != null ? " under " + t.parent.name + " (it should be at the top level)" : " (top level)")}. Nothing was moved or changed.");
                    return;
                }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<Rope>() == null)
            {
                Debug.LogError($"{Tag} Place First Rope: NOT placed: {PrefabPath} is not there. Run Shift Jam > Traversal > Make Rope Piece first. Nothing was changed.");
                return;
            }

            Physics.SyncTransforms();
            // 1. The east edge of the top, on three lines across the hallway.
            float[] lines = { HallZ, HallZ + HallSideZ, HallZ - HallSideZ };
            var edges = new float[lines.Length];
            var tops = new float[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                if (TopEdge(lines[i], out edges[i], out tops[i])) continue;
                Debug.LogError($"{Tag} Place First Rope: NOT placed: no top of the book steps was found on the line z {Say(lines[i])} (rays straight down from y {Say(ScanFromY)}, x {Say(ScanFromX)} to {Say(ScanToX)}, looking for a hit at y {Say(TopAtLeastY)} or higher). Nothing was changed.");
                return;
            }
            string measured = $"the top's east edge: x {Say(edges[0])} at z {Say(lines[0])} (top there y {Say(tops[0])}), x {Say(edges[1])} at z {Say(lines[1])} (y {Say(tops[1])}), x {Say(edges[2])} at z {Say(lines[2])} (y {Say(tops[2])})";
            float edgeX = edges[0];
            if (Mathf.Max(edges) - Mathf.Min(edges) > EdgeStraightWithin)
            {
                Debug.LogError($"{Tag} Place First Rope: NOT placed: the edge is not straight across the hallway (more than {Say(EdgeStraightWithin)} apart): {measured}. Nothing was changed.");
                return;
            }
            if (Mathf.Abs(edgeX - ExpectedEdgeX) > ExpectedEdgeSlack)
            {
                Debug.LogError($"{Tag} Place First Rope: NOT placed: {measured}. The landing book's end is expected at x {Say(ExpectedEdgeX)} (within {Say(ExpectedEdgeSlack)}); an edge at 15.9 would be the box with no book over it. The scene no longer matches the builder: look before placing by hand. Nothing was changed.");
                return;
            }

            // 2. The ceiling over the rope's place: the rope is tied to it.
            float x = edgeX + RopeOut;
            if (!Physics.Raycast(new Vector3(x, CeilingRayFromY, HallZ), Vector3.up, out RaycastHit ceiling, CeilingRayLength, ~0, QueryTriggerInteraction.Ignore)
                || Mathf.Abs(ceiling.point.y - ExpectedCeilingY) > CeilingSlack)
            {
                string found = ceiling.collider != null ? $"'{ceiling.collider.name}' at y {Say(ceiling.point.y)}" : "nothing";
                Debug.LogError($"{Tag} Place First Rope: NOT placed: {measured}. Over the rope's place (x {Say(x)}, z {Say(HallZ)}) a ray up from y {Say(CeilingRayFromY)} found {found}; the ceiling's underside is expected at y {Say(ExpectedCeilingY)} (within {Say(CeilingSlack)}). Nothing was changed.");
                return;
            }
            var at = new Vector3(x, ceiling.point.y, HallZ);

            // 3. The shape behind the rope (the landing book's end, the open air under it, the box): for STATUS.
            var behind = new List<string>();
            foreach (float y in BehindHeights)
            {
                bool hit = Physics.Raycast(new Vector3(edgeX + BehindFromOut, y, HallZ), Vector3.left, out RaycastHit wall, BehindRayLength, ~0, QueryTriggerInteraction.Ignore);
                behind.Add($"y {Say(y)}: " + (hit ? $"x {Say(wall.point.x)} ('{wall.collider.name}')" : $"nothing within {Say(BehindRayLength)}"));
            }

            // 4. The rope's rest line must be free of solid things, as wide as the toy that hangs on it.
            Vector3 high = at + Vector3.down * (BodyRadius + RestLineGap), low = at + Vector3.down * (HallRopeLength - BodyRadius);
            Collider[] inTheWay = Physics.OverlapCapsule(high, low, BodyRadius, ~0, QueryTriggerInteraction.Ignore);
            if (inTheWay.Length > 0)
            {
                var names = new List<string>();
                foreach (Collider c in inTheWay) names.Add(c.name);
                Debug.LogError($"{Tag} Place First Rope: NOT placed: {measured}. The rope's rest line at {Say(at)} (straight down {Say(HallRopeLength)}, {Say(BodyRadius)} wide) is not free: {string.Join(", ", names)}. Nothing was changed.");
                return;
            }

            // 5. Place it: an instance of the piece at the top level, the contract's length, selected, the scene marked.
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            placed.name = HallRopeName;
            placed.transform.SetPositionAndRotation(at, Quaternion.identity);
            placed.transform.localScale = Vector3.one;
            Rope rope = placed.GetComponent<Rope>();
            var so = new SerializedObject(rope);
            float pieceLength = so.FindProperty("length").floatValue;
            bool ownLength = !Mathf.Approximately(pieceLength, HallRopeLength);
            if (ownLength) WorkshopPieces.Set(rope, "length", HallRopeLength);   // the piece was changed by hand: this rope keeps the length it was measured for
            Undo.RegisterCreatedObjectUndo(placed, "Place first rope");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = placed;
            Debug.Log($"{Tag} Place First Rope: PLACED {HallRopeName} at {Say(at)}, top level of Greybox, rotation 0, scale 1, length {Say(HallRopeLength)}{(ownLength ? $" (its own: the piece's is {Say(pieceLength)})" : " (the piece's own)")}; its low end at y {Say(at.y - HallRopeLength)}. "
                + $"Measured: {measured}; used edge x {Say(edgeX)} + {Say(RopeOut)} out; the ceiling ('{ceiling.collider.name}') at y {Say(ceiling.point.y)}; the rest line is free ({Say(BodyRadius)} wide, y {Say(high.y + BodyRadius)} to {Say(low.y - BodyRadius)}). "
                + $"Behind the rope, rays west from x {Say(edgeX + BehindFromOut)}: {string.Join("; ", behind)}. "
                + "The scene is marked changed and NOT saved: save it to keep the rope.");
        }

        /// <summary>The east edge of the steps' top on the line <paramref name="z"/>: the largest x in the scan whose ray
        /// straight down lands at the top's height, then halved in toward the first miss past it. False when no ray
        /// landed that high. <paramref name="topY"/>: the height the ray at the edge landed at.</summary>
        private static bool TopEdge(float z, out float edgeX, out float topY)
        {
            edgeX = topY = 0f;
            bool found = false;
            int steps = Mathf.RoundToInt((ScanToX - ScanFromX) / ScanStep);
            for (int i = 0; i <= steps; i++)
            {
                float x = ScanFromX + i * ScanStep;
                if (!TopAt(x, z, out float y)) continue;
                edgeX = x; topY = y; found = true;
            }
            if (!found) return false;
            float inside = edgeX, outside = edgeX + ScanStep;
            for (int i = 0; i < EdgeHalvings; i++)
            {
                float middle = (inside + outside) * 0.5f;
                if (TopAt(middle, z, out float y)) { inside = middle; topY = y; }
                else outside = middle;
            }
            edgeX = inside;
            return true;
        }

        /// <summary>True if a ray straight down at (x, z) from just under the ceiling lands on something solid at the
        /// top's height (triggers never count).</summary>
        private static bool TopAt(float x, float z, out float y)
        {
            y = 0f;
            if (!Physics.Raycast(new Vector3(x, ScanFromY, z), Vector3.down, out RaycastHit hit, ScanFromY - TopAtLeastY, ~0, QueryTriggerInteraction.Ignore)) return false;
            y = hit.point.y;
            return true;
        }

        // ================================================================== shared

        /// <summary>True (and one warning) in Play: none of these menus does anything there.</summary>
        private static bool InPlay(string menu)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;
            Debug.LogWarning($"{Tag} {menu} does not run in Play: stop the game first. Nothing was done.");
            return true;
        }

        /// <summary>Whether each open scene has unsaved changes.</summary>
        private static bool[] UnsavedScenes()
        {
            var marks = new bool[SceneManager.sceneCount];
            for (int i = 0; i < marks.Length; i++) marks[i] = SceneManager.GetSceneAt(i).isDirty;
            return marks;
        }

        /// <summary>The open scenes' unsaved marks before and after a run that must not change them, in words.</summary>
        private static string ScenesLine(bool[] before, bool[] after)
        {
            bool same = before.Length == after.Length;
            for (int i = 0; same && i < before.Length; i++) same = before[i] == after[i];
            return $"Open scenes' unsaved marks before [{Marks(before)}], after [{Marks(after)}]: " + (same ? "as they were." : "CHANGED, which this menu must not do: look before saving.");
        }

        private static string Marks(bool[] marks)
        {
            var words = new string[marks.Length];
            for (int i = 0; i < marks.Length; i++) words[i] = (i < SceneManager.sceneCount ? SceneManager.GetSceneAt(i).name : "?") + (marks[i] ? ": unsaved" : ": saved");
            return string.Join(", ", words);
        }

        private static string Folder(string path) => path.Substring(0, path.LastIndexOf('/'));
        private static string Say(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Say(Vector3 v) => $"({Say(v.x)}, {Say(v.y)}, {Say(v.z)})";
    }
}
#endif
