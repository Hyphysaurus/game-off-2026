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
    /// The rope's pieces (D-0170), made once and safe to run again. Four menus under Shift Jam > Traversal, each a
    /// public static method with no arguments so code can call it too, each refusing in Play with one warning:
    /// Make Rope Piece (Make): the settings asset, the rope's own material and the Rope prefab. What is already there is
    /// left alone, so his changes to any of the three are kept.
    /// Add Rope Rider To Player Prefab (AddRider): puts RopeRider on the one Player prefab, which every scene's player
    /// and the online player (a variant) take it from. It opens no scene.
    /// Set Rope Numbers (TuneSettings): writes the numbers his first play changed into the settings asset, only over
    /// the values the first build saved there (a number he changed by hand is left). It opens no scene.
    /// Place Hallway Ropes (PlaceHallRopes): hangs a row of four ropes down the hallway in Greybox, the first past the
    /// top of the book steps, at places it measures with physics rays. It is the only one that changes the open scene,
    /// and it does not save it.
    /// WHY the first three say what the open scenes' unsaved marks were before and after: they must not change them.
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

        // ---- the hallway's ropes: a row down the hallway, the first past the east end of the top of the book steps
        // (CourseBuilder.House: the hallway runs along x at z -150, floor 0 as far as the sprint gap at x 68, ceiling
        // underside 14; the box top is 12.2, the landing book's top 12.02 and its end x 21.4)
        private const string GreyboxPath = "Assets/_Game/Scenes/Greybox.unity";
        /// <summary>The hallway's ropes in Greybox, west to east. The first hangs past the top of the book steps.</summary>
        public static readonly string[] HallRopeNames = { "Rope_HallBooks", "Rope_Hall2", "Rope_Hall3", "Rope_Hall4" };
        // Each rope's length. 8: the low end is 6 over the floor, out of a jumping toy's reach (hands 3.4 up at a
        // jump's top, plus Catch Reach 1 = 4.4), and a slow walk off the books still meets rope and not air. The last
        // is 5, the length a grab settles at (Hang Hold): the toy hangs at its very end and cannot slide lower, which
        // is what keeps its longest let-go short (LastFlingMost).
        private static readonly float[] HallRopeLengths = { 8f, 8f, 8f, 5f };
        // The first rope hangs this far past the edge (his play: 1.0 was "too close to the books"). E no longer reaches
        // it from the top (E reaches 1.85): walking or running off the end is the way on. The hands lead the feet by
        // 0.36 and catch 1.0 short of the rope, so the catch comes 2.24 past the edge: 0.32 s out at a walk (7), the
        // hands 1.9 down, 2.7 of rope over them; 0.21 s out at a sprint (11), 1.0 down, at the highest hold (1.7).
        private const float RopeOut = 3.6f;
        // From one rope to the next. Held at 5 a swing of 40 degrees puts the hands 3.2 out and one of 60 4.3, so the
        // next rope is a hop of 1 to 2 beyond the swing, never inside it. Let go on the way up at 20 to 40 degrees
        // (speed 8 to 11 there, plus a lift of 2.8), the hands meet the next rope 0.2 to 0.4 s later, 3 to 4.5 of rope
        // over them: inside its span (1.7 to 8; 1.7 to 5 on the last). Worked out step by step with the rider's own
        // rules (fix1/README.txt has the tables); not played.
        private const float RopeGap = 5.2f;
        // The furthest ANY let-go of the last rope lands past where it is tied: held at its end (5), swung to the
        // widest (75 degrees), let go at 45 on the way up at 12.2 with the stick forward: 17.95. Rounded up.
        private const float LastFlingMost = 18f;
        private const float LandBeforeX = 59f;          // ...and that must be before the checkpoint that guards the sprint gap (x 59 to 65; the gap opens at 68)
        private const float MovedWithin = 0.001f;       // a rope nearer its place than this is where it should be
        private const float HallZ = -150f;              // the hallway's middle line
        private const float HallSideZ = 4f;             // the edge is measured again this far to each side of it
        private const float ScanFromX = 14f, ScanToX = 24f, ScanStep = 0.05f;   // where the top's east edge is looked for
        private const float ScanFromY = 13.9f;          // the rays start just under the ceiling (14)
        private const float TopAtLeastY = 11.9f;        // a hit this high is the top of the steps (12.02 to 12.2)
        private const int EdgeHalvings = 10;            // the edge between the last hit and the first miss is halved this often (to under a thousandth)
        private const float EdgeStraightWithin = 0.2f;  // the three measured lines may differ by this much
        private const float ExpectedEdgeX = 21.4f;      // the landing book's end (Book_01 is 16.98 long, its back end at 4.4)
        private const float ExpectedEdgeSlack = 0.3f;   // further off than this, the scene no longer matches the builder: stop
        private const float ExpectedCeilingY = 14f, CeilingSlack = 0.1f;
        private const float CeilingRayFromY = 13f, CeilingRayLength = 3f;
        private const float BodyRadius = 0.3f;          // the toy's capsule: a rope's rest line must be free this wide
        private const float RestLineGap = 0.02f;        // the free check stops this far under the ceiling (touching it is not "in the way")
        private const float BehindFromOut = 0.5f;       // the rays that read the shape behind the first rope start this far past the edge
        private const float BehindRayLength = 10f;
        private static readonly float[] BehindHeights = { 11f, 9f, 7f, 5f };

        // ---- the numbers his first play changed. The asset keeps what was saved in it, so a new default in
        // RopeSettings.cs never reaches it: TuneSettings writes them, and only over what the first build saved.
        private const float FirstCatchReach = 0.7f, FirstMinHold = 1.2f, FirstLeaveReach = 1.05f, FirstHandHeight = 1.5f;

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

        // ================================================================== the numbers

        /// <summary>Shift Jam > Traversal > Set Rope Numbers: writes the numbers his first play of the rope changed into
        /// Resources/Rope.asset. WHY a menu: a ScriptableObject keeps the values saved in its file, so the new
        /// defaults in RopeSettings.cs do not show up in an asset that already exists. Each of the four changed
        /// numbers (Catch Reach, Min Hold, Leave Reach, Hand Offset y) is written only if the asset still holds the
        /// first build's value; one he changed by hand is left and named. The two new numbers (Hang Hold, Grip Curl)
        /// come in with their defaults and are saved with the rest. It opens no scene. Run again, it changes nothing.</summary>
        [MenuItem("Shift Jam/Traversal/Set Rope Numbers (after the first play)")]
        public static void TuneSettings()
        {
            if (InPlay("Set Rope Numbers")) return;
            var settings = AssetDatabase.LoadAssetAtPath<RopeSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError($"{Tag} Set Rope Numbers: FAILED: {SettingsPath} is not there. Run Shift Jam > Traversal > Make Rope Piece first (it makes the asset with these numbers already in it). Nothing was changed.");
                return;
            }
            bool[] before = UnsavedScenes();
            var did = new List<string>();
            RopeSettings now = ScriptableObject.CreateInstance<RopeSettings>();   // the defaults as the code has them: the one place they are written
            try
            {
                Undo.RecordObject(settings, "Set rope numbers");
                settings.catchReach = Tuned("Catch Reach", settings.catchReach, FirstCatchReach, now.catchReach, did);
                settings.minHold = Tuned("Min Hold", settings.minHold, FirstMinHold, now.minHold, did);
                settings.leaveReach = Tuned("Leave Reach", settings.leaveReach, FirstLeaveReach, now.leaveReach, did);
                Vector3 hand = settings.handOffset;
                hand.y = Tuned("Hand Offset y", hand.y, FirstHandHeight, now.handOffset.y, did);
                settings.handOffset = hand;
            }
            finally { Object.DestroyImmediate(now); }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log($"{Tag} Set Rope Numbers: OK. {string.Join("; ", did)}. New with this round and saved with them: Hang Hold {Say(settings.hangHold)}, Grip Curl {Say(settings.gripCurl)}. Hand Offset is now {Say(settings.handOffset)}. File: {SettingsPath}. {ScenesLine(before, UnsavedScenes())}");
        }

        /// <summary>One number of the asset: <paramref name="now"/> if it still holds the first build's value, else
        /// left as it is. What happened is added to <paramref name="did"/>.</summary>
        private static float Tuned(string name, float has, float first, float now, List<string> did)
        {
            if (Mathf.Approximately(has, now)) { did.Add($"{name} already {Say(now)}"); return has; }
            if (!Mathf.Approximately(has, first))
            {
                did.Add($"{name} LEFT at {Say(has)} (changed by hand: the first build's was {Say(first)}, this round's is {Say(now)})");
                return has;
            }
            did.Add($"{name} {Say(first)} -> {Say(now)}");
            return now;
        }

        // ================================================================== the hallway's ropes

        /// <summary>Shift Jam > Traversal > Place Hallway Ropes: a row of four ropes down the hallway of Greybox, each
        /// tied to the ceiling on the hallway's middle line, at the TOP LEVEL of the scene (a hallway rebuild wipes
        /// everything under R3_SkirtingRun). The first, Rope_HallBooks, hangs RopeOut past the east edge of the top of
        /// the book steps; Rope_Hall2 to Rope_Hall4 follow RopeGap apart. It only works on Greybox, open and active,
        /// and out of Play.
        /// WHY it measures instead of using the builder's numbers: the top's east edge is the landing book's end, which
        /// comes from a pack mesh's length; a scene that has moved on from the builder must stop this, not get ropes in
        /// the wrong place. Rays straight down find the edge on three lines across the hallway (they must agree), a ray
        /// up finds the ceiling over each rope, each rope's rest line must be free of solid things, and the furthest
        /// let-go of the last rope must land before the checkpoint that guards the sprint gap.
        /// Safe to run again: a rope already in the scene under its name is MOVED to its place (and to the top level,
        /// and given its length), a missing one is made, and none is doubled; two objects with one of the names stop
        /// it. All the measuring comes first: a failed measure leaves the scene untouched. Every number used is
        /// logged. It marks the scene changed and does NOT save it.</summary>
        [MenuItem("Shift Jam/Traversal/Place Hallway Ropes (four, from the book steps, Greybox)")]
        public static void PlaceHallRopes()
        {
            const string Menu = "Place Hallway Ropes";
            const string Untouched = "Nothing was changed.";
            if (InPlay(Menu)) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != GreyboxPath)
            {
                Debug.LogError($"{Tag} {Menu}: NOT placed: the open scene is '{scene.path}', and these ropes belong in {GreyboxPath}. Open Greybox and run this again. {Untouched}");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<Rope>() == null)
            {
                Debug.LogError($"{Tag} {Menu}: NOT placed: {PrefabPath} is not there. Run Shift Jam > Traversal > Make Rope Piece first. {Untouched}");
                return;
            }

            // 1. The ropes already in the scene, by name, wherever they sit. Two under one name: his to sort out.
            int count = HallRopeNames.Length;
            var found = new Transform[count];
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (Transform t in sceneRoot.GetComponentsInChildren<Transform>(true))
                {
                    int i = System.Array.IndexOf(HallRopeNames, t.name);
                    if (i < 0) continue;
                    if (found[i] != null)
                    {
                        Debug.LogError($"{Tag} {Menu}: NOT placed: two objects in Greybox are named {t.name} (at {Say(found[i].position)} and at {Say(t.position)}). Delete or rename one and run this again. {Untouched}");
                        return;
                    }
                    found[i] = t;
                }
            for (int i = 0; i < count; i++)
            {
                if (found[i] == null || found[i].GetComponent<Rope>() != null) continue;
                Debug.LogError($"{Tag} {Menu}: NOT placed: the object named {HallRopeNames[i]} at {Say(found[i].position)} has no Rope script on it. Delete or rename it and run this again. {Untouched}");
                return;
            }

            Physics.SyncTransforms();
            // 2. The east edge of the top, on three lines across the hallway.
            float[] lines = { HallZ, HallZ + HallSideZ, HallZ - HallSideZ };
            var edges = new float[lines.Length];
            var tops = new float[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                if (TopEdge(lines[i], out edges[i], out tops[i])) continue;
                Debug.LogError($"{Tag} {Menu}: NOT placed: no top of the book steps was found on the line z {Say(lines[i])} (rays straight down from y {Say(ScanFromY)}, x {Say(ScanFromX)} to {Say(ScanToX)}, looking for a hit at y {Say(TopAtLeastY)} or higher). {Untouched}");
                return;
            }
            string measured = $"the top's east edge: x {Say(edges[0])} at z {Say(lines[0])} (top there y {Say(tops[0])}), x {Say(edges[1])} at z {Say(lines[1])} (y {Say(tops[1])}), x {Say(edges[2])} at z {Say(lines[2])} (y {Say(tops[2])})";
            float edgeX = edges[0];
            if (Mathf.Max(edges) - Mathf.Min(edges) > EdgeStraightWithin)
            {
                Debug.LogError($"{Tag} {Menu}: NOT placed: the edge is not straight across the hallway (more than {Say(EdgeStraightWithin)} apart): {measured}. {Untouched}");
                return;
            }
            if (Mathf.Abs(edgeX - ExpectedEdgeX) > ExpectedEdgeSlack)
            {
                Debug.LogError($"{Tag} {Menu}: NOT placed: {measured}. The landing book's end is expected at x {Say(ExpectedEdgeX)} (within {Say(ExpectedEdgeSlack)}); an edge at 15.9 would be the box with no book over it. The scene no longer matches the builder: look before placing by hand. {Untouched}");
                return;
            }

            // 3. Each rope's place: the ceiling over it (the rope is tied there), and its rest line free of solid
            // things, as wide as the toy that hangs on it. A rope already in the scene is never in the way: its handle
            // is a trigger, and triggers are not counted.
            var places = new Vector3[count];
            var ceilings = new string[count];
            for (int i = 0; i < count; i++)
            {
                float x = edgeX + RopeOut + i * RopeGap, length = HallRopeLengths[i];
                if (!Physics.Raycast(new Vector3(x, CeilingRayFromY, HallZ), Vector3.up, out RaycastHit ceiling, CeilingRayLength, ~0, QueryTriggerInteraction.Ignore)
                    || Mathf.Abs(ceiling.point.y - ExpectedCeilingY) > CeilingSlack)
                {
                    string above = ceiling.collider != null ? $"'{ceiling.collider.name}' at y {Say(ceiling.point.y)}" : "nothing";
                    Debug.LogError($"{Tag} {Menu}: NOT placed: {measured}. Over {HallRopeNames[i]}'s place (x {Say(x)}, z {Say(HallZ)}) a ray up from y {Say(CeilingRayFromY)} found {above}; the ceiling's underside is expected at y {Say(ExpectedCeilingY)} (within {Say(CeilingSlack)}). {Untouched}");
                    return;
                }
                places[i] = new Vector3(x, ceiling.point.y, HallZ);
                ceilings[i] = $"'{ceiling.collider.name}' y {Say(ceiling.point.y)}";
                Vector3 high = places[i] + Vector3.down * (BodyRadius + RestLineGap), low = places[i] + Vector3.down * (length - BodyRadius);
                Collider[] inTheWay = Physics.OverlapCapsule(high, low, BodyRadius, ~0, QueryTriggerInteraction.Ignore);
                if (inTheWay.Length == 0) continue;
                var names = new List<string>();
                foreach (Collider c in inTheWay) names.Add(c.name);
                Debug.LogError($"{Tag} {Menu}: NOT placed: {measured}. {HallRopeNames[i]}'s rest line at {Say(places[i])} (straight down {Say(length)}, {Say(BodyRadius)} wide) is not free: {string.Join(", ", names)}. {Untouched}");
                return;
            }

            // 4. Where the longest let-go of the last rope comes down.
            float lastLanding = places[count - 1].x + LastFlingMost;
            if (lastLanding >= LandBeforeX)
            {
                Debug.LogError($"{Tag} {Menu}: NOT placed: {measured}. The last rope would hang at x {Say(places[count - 1].x)}, and its longest let-go lands {Say(LastFlingMost)} on, at x {Say(lastLanding)}: that must be before x {Say(LandBeforeX)} (the checkpoint before the sprint gap). {Untouched}");
                return;
            }

            // 5. The shape behind the first rope (the landing book's end, the open air under it, the box): for STATUS.
            var behind = new List<string>();
            foreach (float y in BehindHeights)
            {
                bool hit = Physics.Raycast(new Vector3(edgeX + BehindFromOut, y, HallZ), Vector3.left, out RaycastHit wall, BehindRayLength, ~0, QueryTriggerInteraction.Ignore);
                behind.Add($"y {Say(y)}: " + (hit ? $"x {Say(wall.point.x)} ('{wall.collider.name}')" : $"nothing within {Say(BehindRayLength)}"));
            }

            // 6. Every measure held: move the ropes that are there, make the ones that are not.
            const string UndoName = "Place hallway ropes";
            var did = new List<string>();
            bool changed = false;
            GameObject firstRope = null;
            for (int i = 0; i < count; i++)
            {
                float length = HallRopeLengths[i];
                Transform t = found[i];
                string what;
                if (t == null)
                {
                    var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    placed.name = HallRopeNames[i];
                    t = placed.transform;
                    t.SetPositionAndRotation(places[i], Quaternion.identity);
                    t.localScale = Vector3.one;
                    WorkshopPieces.Set(placed.GetComponent<Rope>(), "length", length);
                    Undo.RegisterCreatedObjectUndo(placed, UndoName);
                    what = "MADE";
                    changed = true;
                }
                else
                {
                    Rope rope = t.GetComponent<Rope>();
                    Vector3 was = t.position;
                    string under = t.parent != null ? t.parent.name : null;
                    bool moved = under != null || Vector3.Distance(was, places[i]) > MovedWithin || t.rotation != Quaternion.identity || t.localScale != Vector3.one;
                    float lengthWas = new SerializedObject(rope).FindProperty("length").floatValue;
                    bool resized = !Mathf.Approximately(lengthWas, length);
                    if (moved)
                    {
                        if (under != null) Undo.SetTransformParent(t, null, UndoName);
                        Undo.RecordObject(t, UndoName);
                        t.SetPositionAndRotation(places[i], Quaternion.identity);
                        t.localScale = Vector3.one;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    }
                    if (resized) WorkshopPieces.Set(rope, "length", length);
                    what = !moved && !resized ? "already there"
                        : (moved ? $"MOVED from {Say(was)}{(under != null ? $" (it was under {under}: now at the top level)" : "")}" : "in place")
                          + (resized ? $", length {Say(lengthWas)} -> {Say(length)}" : "");
                    changed |= moved || resized;
                }
                if (firstRope == null) firstRope = t.gameObject;
                did.Add($"{HallRopeNames[i]} {what}: at {Say(places[i])}, length {Say(length)}, low end y {Say(places[i].y - length)}, ceiling {ceilings[i]}");
            }
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = firstRope;
            Debug.Log($"{Tag} {Menu}: {(changed ? "PLACED" : "OK, all four were already in place")}. {string.Join("; ", did)}. All at the top level of Greybox, rotation 0, scale 1. "
                + $"Measured: {measured}; used edge x {Say(edgeX)} + {Say(RopeOut)} out for the first, then {Say(RopeGap)} apart; every rest line is free ({Say(BodyRadius)} wide, from {Say(RestLineGap)} under the ceiling to the rope's end). "
                + $"The longest let-go of the last rope lands at x {Say(lastLanding)} at most ({Say(LastFlingMost)} past where it is tied), before x {Say(LandBeforeX)}. "
                + $"Behind the first rope, rays west from x {Say(edgeX + BehindFromOut)}: {string.Join("; ", behind)}. "
                + (changed ? "The scene is marked changed and NOT saved: save it to keep the ropes." : "The scene was not marked: nothing in it changed."));
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
