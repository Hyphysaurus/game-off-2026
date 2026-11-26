#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// "Land Cloth (D-0233)": the maker of the Magica Cloth 2 curtains and flags, into the OPEN Greybox; it never opens or
    /// saves a scene.
    /// 1. Resources/ClothSettings: made when missing; an existing one keeps his numbers (a missing place is added).
    /// 2. Read/Write ON for every model a cloth uses (Playroom Curtain_01.fbx and Army_Flag_01.fbx): Magica builds a cloth
    ///    only from a readable mesh, in the editor and in a built game. This is a LOCAL change to git-ignored pack files; a
    ///    fresh import of the pack turns it off, so this maker turns it on again at every run, and so does every build
    ///    (this class is also a build step: it does the same for every scene in the build that has a ClothDresser).
    /// 3. A Curtain_01 at the bedroom window (Room/Bedroom/Window), the R6 window (Room/Course/R6_Window/Window) and each
    ///    downstairs window (Room/Course/Downstairs/Dressing/Window_N), as "Curtain_&lt;window&gt; (D-0233)" beside its window,
    ///    replaced at each run (no duplicates). Placed by the pack's own fit (its demo scenes, all three the same:
    ///    (0, 64.13, 0.61) in the window's own frame, the window's turn), then MEASURED: rays from the room toward the wall at
    ///    the curtain's top find the wall's face; a curtain whose back would be inside the wall is brought out to it (0.05
    ///    off it); then while any still piece of it (each separate piece of the model, its own box) overlaps something (the
    ///    window's frame) it steps out into the room, 0.25 at a time. Its own collider (the pack's MeshCollider) is switched
    ///    OFF: the player walks into the cloth and pushes it instead of bumping an invisible blind, and shots fly through
    ///    (a shot that passes poke it). A ClothDresser (Curtain, its place) goes on it.
    /// 4. A ClothDresser (Flag) on each fort flag: Room/Course/R4_Playroom/Army_Flag_Stand_01/Army_Flag_01 (PlayroomFort) and
    ///    Room/Course/Downstairs/TanSplinter/TanFlag/Army_Flag_01 (TanFort); an existing one is updated, never doubled. The
    ///    flag's own collider stays (its pole stays solid).
    /// Every cloth is built from code as the scene starts: no Magica data is written into the scene or anywhere else.
    /// The report says, for each curtain, where it went and why, what its still pieces touched, where its moving part (the
    /// pull cord) hangs and what is under its end; for each flag, its pieces; then every number on the asset and the off
    /// switches. The scene is MARKED CHANGED and NOT SAVED (Ctrl+S keeps it). A re-run of the house builders that rebuild
    /// Room drops the curtains and the flags' dressers: run this again after them.
    /// Menu: Shift Jam > House > Land Cloth (D-0233). Batch or a check: ShiftJam.ClothBuilder.Land() returns the report
    /// (a report that starts "[Cloth D-0233] NOT " changed nothing).
    /// </summary>
    public sealed class ClothBuilder : IPreprocessBuildWithReport
    {
        private const string Tag = "[Cloth D-0233]";
        private const string ScenePath = "Assets/_Game/Scenes/Greybox.unity";
        private const string SettingsFolder = "Assets/_Game/Resources";
        private const string SettingsPath = SettingsFolder + "/" + ClothSettings.ResourceName + ".asset";
        private const string CurtainPrefab = "Assets/_Imports/ithappy/Playroom/Prefabs/Room_Modules/Curtain_01.prefab";
        private const string Downstairs = "Room/Course/Downstairs/Dressing";
        private const string Suffix = " (D-0233)";

        private static readonly (string path, string place)[] Windows =
        {
            ("Room/Bedroom/Window", "Bedroom"),
            ("Room/Course/R6_Window/Window", "Window"),
        };
        private static readonly (string path, string place)[] Flags =
        {
            ("Room/Course/R4_Playroom/Army_Flag_Stand_01/Army_Flag_01", "PlayroomFort"),
            ("Room/Course/Downstairs/TanSplinter/TanFlag/Army_Flag_01", "TanFort"),
        };

        /// <summary>The pack's own fit of Curtain_01 on Window_01, in the window's own frame (its Demonstration, Demonstration
        /// Night and Built-In scenes: the curtain 64.13 over the window's pivot and 0.61 toward the room, the same turn).</summary>
        private static readonly Vector3 PackFit = new Vector3(0f, 64.13044f, 0.61006f);
        private const float WallGap = 0.05f;     // air between the curtain's back and the wall's face
        private const float PushStep = 0.25f;    // a step out into the room while a still piece overlaps something
        private const float MostPush = 12f;      // gives up past this and says so
        private const float Shrink = 0.02f;      // a piece's box is this much smaller each way when checked: touching is not overlapping
        private const float RaysFrom = 40f;      // the wall rays start this far in front of the curtain
        private const float NearSill = 10f;      // the cord's end this near over the window's own collider = over the sill

        private static readonly Collider[] Overlaps = new Collider[32];

        public int callbackOrder => 0;

        /// <summary>Before every build: Read/Write on for every model a ClothDresser uses in the build's scenes.</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            var log = new StringBuilder();
            int changed = ReadableForBuild(log);
            Debug.Log($"{Tag} Before the build: {changed} model(s) turned Read/Write.\n{log}");
        }

        [MenuItem("Shift Jam/House/Land Cloth (D-0233)")]
        private static void Menu()
        {
            string report = Land();
            if (report.StartsWith(Tag + " NOT ")) Debug.LogWarning(report); else Debug.Log(report);
        }

        /// <summary>Makes or keeps the settings, turns Read/Write on, places the curtains, dresses the flags, marks Greybox
        /// changed (never saves it). Returns the report, one fact a line.</summary>
        public static string Land()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return $"{Tag} NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                return $"{Tag} NOT landed: the active scene is '{scene.path}', and the house is {ScenePath}. Open Greybox and run this again (it never opens a scene by itself). Nothing was changed.";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CurtainPrefab) == null)
                return $"{Tag} NOT landed: no {CurtainPrefab} (the Playroom pack is git-ignored: import it). Nothing was changed.";

            var log = new StringBuilder();
            log.AppendLine($"{Tag} Land Cloth (D-0233): Magica Cloth 2 curtains and flags. Greybox is MARKED CHANGED and NOT SAVED: Ctrl+S keeps it.");
            ClothSettings s = Settings(log);

            // Read/Write first: everything measured below reads the models, and Magica needs it in a built game too.
            var models = new SortedSet<string>();
            AddModel(AssetDatabase.LoadAssetAtPath<GameObject>(CurtainPrefab), models);
            foreach (var (path, _) in Flags) AddModel(GameObject.Find(path), models);
            Collect(scene, models);
            foreach (string model in models) ReadWrite(model, log);

            Physics.SyncTransforms();
            GameObject curtain = AssetDatabase.LoadAssetAtPath<GameObject>(CurtainPrefab);   // loaded again: a reimport above may have replaced it
            int placed = 0;
            foreach (var (path, place) in Windows)
            {
                GameObject window = GameObject.Find(path);
                if (window == null) { log.AppendLine($"WINDOW {path}: NOT FOUND, no curtain there."); continue; }
                if (PlaceCurtain(window.transform, place, curtain, s, log)) placed++;
            }
            GameObject dressing = GameObject.Find(Downstairs);
            if (dressing == null) log.AppendLine($"DOWNSTAIRS {Downstairs}: NOT FOUND, no curtains there.");
            else
            {
                var downstairs = new List<Transform>();
                foreach (Transform child in dressing.transform)
                    if (child.name.StartsWith("Window_") && child.GetComponent<MeshFilter>() != null) downstairs.Add(child);
                downstairs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                if (downstairs.Count == 0) log.AppendLine($"DOWNSTAIRS {Downstairs}: no Window_N with a model under it, no curtains there.");
                foreach (Transform window in downstairs)
                    if (PlaceCurtain(window, "Downstairs", curtain, s, log)) placed++;
            }
            int flags = 0;
            foreach (var (path, place) in Flags)
                if (DressFlag(path, place, s, log)) flags++;

            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine($"DONE: {placed} curtain(s) placed, {flags} flag(s) dressed. No Magica data was written: each cloth is built from code as the scene starts (ClothDresser).");
            Summary(s, log);
            log.AppendLine("OFF SWITCHES (Resources/ClothSettings): Cloth On (all still), Curtain > On / Flag > On, a place's On, a place's Breeze at 0 (no breeze), Player Pushes, Shots Poke. To take a curtain away: delete its Curtain_<window> (D-0233); a flag: remove its ClothDresser.");
            log.AppendLine("Greybox MARKED CHANGED, not saved.");
            return log.ToString();
        }

        // ------------------------------------------------------------------------------------------------ settings

        private static ClothSettings Settings(StringBuilder log)
        {
            var s = AssetDatabase.LoadAssetAtPath<ClothSettings>(SettingsPath);
            bool made = s == null;
            if (made)
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
                s = ScriptableObject.CreateInstance<ClothSettings>();
                AssetDatabase.CreateAsset(s, SettingsPath);
            }
            int added = s.AddMissingPlaces();
            if (added > 0) EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            log.AppendLine($"{(made ? "MADE" : "KEPT")} {SettingsPath}{(added > 0 && !made ? $" ({added} missing place(s) added, his rows kept)" : "")}: cloth {(s.clothOn ? "ON" : "OFF (every curtain and flag stands still)")}.");
            return s;
        }

        // ------------------------------------------------------------------------------------------------ Read/Write

        private static void AddModel(GameObject holder, SortedSet<string> models)
        {
            if (holder == null || !holder.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) return;
            string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (!string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is ModelImporter) models.Add(path);
        }

        /// <summary>The models behind every ClothDresser in the scene.</summary>
        private static void Collect(Scene scene, SortedSet<string> models)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (ClothDresser d in root.GetComponentsInChildren<ClothDresser>(true))
                    AddModel(d.gameObject, models);
        }

        /// <summary>Read/Write on for one model. True when it was off and is now on.</summary>
        private static bool ReadWrite(string path, StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { log.AppendLine($"READ/WRITE: {path} is not a model file: not changed."); return false; }
            if (importer.isReadable) { log.AppendLine($"READ/WRITE already ON: {path}."); return false; }
            importer.isReadable = true;
            importer.SaveAndReimport();
            log.AppendLine($"READ/WRITE TURNED ON: {path}. A LOCAL change to a git-ignored pack file (its .meta): Magica builds a cloth only from a readable mesh, in the editor and in a built game. A fresh import of the pack turns it off; this maker and every build turn it on again.");
            return true;
        }

        /// <summary>The build step: every enabled build scene that holds a ClothDresser (read without opening it), the models
        /// behind them turned Read/Write. Returns how many were changed.</summary>
        private static int ReadableForBuild(StringBuilder log)
        {
            string script = DresserScript();
            if (script == null) { log.AppendLine("The ClothDresser script was not found: nothing checked."); return 0; }
            var models = new SortedSet<string>();
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled || string.IsNullOrEmpty(entry.path)) continue;
                if (System.Array.IndexOf(AssetDatabase.GetDependencies(entry.path, true), script) < 0) continue;   // no cloth in it
                Scene open = SceneManager.GetSceneByPath(entry.path);
                if (open.IsValid() && open.isLoaded) Collect(open, models);
                else
                {
                    Scene preview = EditorSceneManager.OpenPreviewScene(entry.path);
                    try { Collect(preview, models); }
                    finally { EditorSceneManager.ClosePreviewScene(preview); }
                }
            }
            int changed = 0;
            foreach (string model in models)
                if (ReadWrite(model, log)) changed++;
            return changed;
        }

        private static string DresserScript()
        {
            foreach (string guid in AssetDatabase.FindAssets("ClothDresser t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == typeof(ClothDresser)) return path;
            }
            return null;
        }

        // ------------------------------------------------------------------------------------------------ curtains

        /// <summary>One curtain at <paramref name="window"/>: placed, measured, dressed. Writes its lines; true when placed.</summary>
        private static bool PlaceCurtain(Transform window, string place, GameObject prefab, ClothSettings s, StringBuilder log)
        {
            Transform parent = window.parent;
            string name = "Curtain_" + window.name + Suffix;
            int replaced = 0;
            if (parent != null)
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Transform old = parent.GetChild(i);
                    if (old.name != name) continue;
                    Object.DestroyImmediate(old.gameObject);
                    replaced++;
                }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, window.gameObject.scene);
            go.name = name;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localScale = window.localScale;
            t.SetPositionAndRotation(window.TransformPoint(PackFit), window.rotation);
            int off = 0;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                if (c.enabled) { c.enabled = false; off++; }
            if (GameObjectUtility.GetStaticEditorFlags(go) != 0) GameObjectUtility.SetStaticEditorFlags(go, 0);   // static batching would merge its mesh away
            var dresser = go.AddComponent<ClothDresser>();
            dresser.Setup(ClothSettings.ClothKind.Curtain, place);
            EditorUtility.SetDirty(dresser);

            string where = PathOf(t);
            if (!go.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null)
            {
                log.AppendLine($"CURTAIN {where} (place {place}): PLACED at the pack's fit, but its prefab has no mesh: not measured, and it will not be a cloth.");
                return true;
            }
            Mesh mesh = mf.sharedMesh;
            Bounds mb = mesh.bounds;
            float unit = Mathf.Abs(t.lossyScale.x);
            ClothSettings.Kind k = s.curtain;
            int n = mesh.vertexCount;
            var parts = new ClothShape.Part[n];
            var pieceOf = new int[n];
            ClothShape.Counts counts = ClothShape.Sort(mesh, k.hold, k.holdDistance, k.joinPieces > 0f, parts, pieceOf);
            List<Bounds> still = Pieces(mesh, parts, pieceOf, counts.pieces, false);
            bool faces = mb.max.z > -mb.min.z;   // its body stands out on its +z side, as the pack's fit expects

            // 1. The wall behind its top.
            Physics.SyncTransforms();
            float wall = WallFace(t, mb, unit, out string wallName);
            bool outToWall = !float.IsNaN(wall) && wall + WallGap > mb.min.z;
            float toWall = outToWall ? wall + WallGap - mb.min.z : 0f;
            float shift = toWall;
            // 2. Clear of everything else.
            string firstTouch = null, lastTouch = null;
            int steps = 0;
            while (Touches(t, still, shift, unit, out lastTouch))
            {
                if (firstTouch == null) firstTouch = lastTouch;
                if (shift >= MostPush) break;
                shift += PushStep;
                steps++;
            }
            t.position += t.forward * (shift * unit);
            Physics.SyncTransforms();
            Vector3 onWindow = window.InverseTransformPoint(t.position);

            var line = new StringBuilder();
            line.Append($"CURTAIN {where} (place {place}){(replaced > 0 ? " REPLACED" : " NEW")}: ");
            line.Append($"the pack's fit (0, 64.13, 0.61 on the window)");
            if (float.IsNaN(wall)) line.Append(", no wall found behind its top (kept the pack's depth)");
            else if (outToWall) line.Append($", OUT {toWall * unit:F2} to the wall's face ({wallName}: the pack's place had its back {(wall - mb.min.z) * unit:F2} inside the wall)");
            else line.Append($", the wall ({wallName}) {(mb.min.z - wall) * unit:F2} BEHIND its back: a gap behind its brackets (the pack's depth kept: the window's frame stands out from that wall)");
            if (steps > 0) line.Append($", then {steps} step(s) ({steps * PushStep:F2}) out into the room to clear {firstTouch}");
            if (lastTouch != null) line.Append($"; STILL OVERLAPS {lastTouch} after {MostPush} out (look at it)");
            line.Append($". Now at ({t.position.x:F2}, {t.position.y:F2}, {t.position.z:F2}), on the window ({onWindow.x:F2}, {onWindow.y:F2}, {onWindow.z:F2}), its top {t.TransformPoint(new Vector3(0f, mb.max.y, 0f)).y:F1} up.");
            line.Append($" Its collider{(off == 1 ? "" : "s")} switched OFF ({off}).");
            if (!faces) line.Append(" WARNING: its model's body stands out on its -z side, not +z as the pack's fit expects: it may face the wall.");
            log.AppendLine(line.ToString());
            log.AppendLine($"  PIECES: {counts.pieces} ({counts.movingPieces} moving, {counts.loosePieces} loose ones kept still); {counts.moving} of its {n} points move, {counts.held} are held, {counts.still} stay as modelled (Hold Distance {k.holdDistance} from its top).");

            // 3. The moving part (the pull cord): where it hangs, what it touches, what is under its end.
            if (counts.moving > 0)
            {
                Bounds cord = counts.movingBounds;
                Vector3 top = t.TransformPoint(new Vector3(cord.center.x, cord.max.y, cord.center.z));
                Vector3 end = t.TransformPoint(new Vector3(cord.center.x, cord.min.y, cord.center.z));
                bool touches = Touches(t, new List<Bounds> { cord }, 0f, unit, out string cordTouch);
                string under = "nothing under it within 500";
                if (Physics.Raycast(end + Vector3.up * 0.01f, Vector3.down, out RaycastHit hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    bool sill = hit.transform.IsChildOf(window) && hit.distance <= NearSill;
                    under = $"{hit.distance:F1} over {PathOf(hit.transform)}{(sill ? " (OVER THE WINDOW'S SILL: a toy on the sill walks into it)" : "")}";
                }
                Vector3 pivot = window.position;
                log.AppendLine($"  MOVES: its part from y {top.y:F1} down to {end.y:F1} (x {end.x:F1}, z {end.z:F1}); the window's pivot (its sill's top) is at y {pivot.y:F1}; {(touches ? "it TOUCHES " + cordTouch : "it touches nothing")}; its end is {under}.");
            }
            else log.AppendLine($"  MOVES: nothing (Hold Distance {k.holdDistance} holds the whole model): it will stand still.");
            return true;
        }

        /// <summary>The wall's face behind the curtain's top, as a depth in the curtain's own frame (the largest of three rays
        /// from the room toward the wall, across its width, just under its top). NaN when no ray meets anything.</summary>
        private static float WallFace(Transform t, Bounds mb, float unit, out string what)
        {
            what = null;
            float best = float.NaN;
            float y = mb.max.y - Mathf.Min(1f, mb.size.y * 0.25f);
            float reach = (RaysFrom + mb.size.z + RaysFrom) * unit;
            foreach (float across in new[] { -0.35f, 0f, 0.35f })
            {
                Vector3 from = t.TransformPoint(new Vector3(mb.center.x + across * mb.size.x, y, mb.max.z + RaysFrom));
                if (!Physics.Raycast(from, -t.forward, out RaycastHit hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.transform.IsChildOf(t)) continue;
                float z = t.InverseTransformPoint(hit.point).z;
                if (float.IsNaN(best) || z > best) { best = z; what = PathOf(hit.transform); }
            }
            return best;
        }

        /// <summary>Does any of <paramref name="boxes"/> (the curtain's own space, moved <paramref name="shift"/> out along its
        /// +z) overlap a solid collider that is not the curtain's own? <paramref name="what"/> names the first one found.</summary>
        private static bool Touches(Transform t, List<Bounds> boxes, float shift, float unit, out string what)
        {
            what = null;
            foreach (Bounds b in boxes)
            {
                Vector3 centre = t.TransformPoint(b.center + new Vector3(0f, 0f, shift));
                Vector3 half = Vector3.Max(b.extents * unit - Vector3.one * Shrink, Vector3.one * 0.001f);
                int count = Physics.OverlapBoxNonAlloc(centre, half, Overlaps, t.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider c = Overlaps[i];
                    if (c == null || c.transform.IsChildOf(t)) continue;
                    what = PathOf(c.transform);
                    return true;
                }
            }
            return false;
        }

        /// <summary>The boxes (own space) of the model's pieces that move (<paramref name="moving"/>) or of those that do not.</summary>
        private static List<Bounds> Pieces(Mesh mesh, ClothShape.Part[] parts, int[] pieceOf, int pieces, bool moving)
        {
            var points = new List<Vector3>();
            mesh.GetVertices(points);
            var lo = new Vector3[pieces];
            var hi = new Vector3[pieces];
            var seen = new bool[pieces];
            var moves = new bool[pieces];
            for (int i = 0; i < points.Count && i < pieceOf.Length; i++)
            {
                int p = pieceOf[i];
                if (p < 0 || p >= pieces) continue;
                if (parts[i] != ClothShape.Part.Still) moves[p] = true;
                if (!seen[p]) { lo[p] = hi[p] = points[i]; seen[p] = true; }
                else { lo[p] = Vector3.Min(lo[p], points[i]); hi[p] = Vector3.Max(hi[p], points[i]); }
            }
            var boxes = new List<Bounds>();
            for (int p = 0; p < pieces; p++)
                if (seen[p] && moves[p] == moving) boxes.Add(new Bounds((lo[p] + hi[p]) * 0.5f, hi[p] - lo[p]));
            return boxes;
        }

        // ------------------------------------------------------------------------------------------------ flags

        /// <summary>A ClothDresser (Flag) on one fort flag. Writes its line; true when dressed.</summary>
        private static bool DressFlag(string path, string place, ClothSettings s, StringBuilder log)
        {
            GameObject go = GameObject.Find(path);
            if (go == null) { log.AppendLine($"FLAG {path}: NOT FOUND, not dressed."); return false; }
            if (!go.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null || !go.TryGetComponent(out MeshRenderer _))
            {
                log.AppendLine($"FLAG {path}: no MeshFilter with a mesh and MeshRenderer on it, not dressed.");
                return false;
            }
            bool had = go.TryGetComponent(out ClothDresser dresser);
            if (!had) dresser = go.AddComponent<ClothDresser>();
            dresser.Setup(ClothSettings.ClothKind.Flag, place);
            EditorUtility.SetDirty(dresser);
            if (GameObjectUtility.GetStaticEditorFlags(go) != 0) GameObjectUtility.SetStaticEditorFlags(go, 0);
            Mesh mesh = mf.sharedMesh;
            ClothSettings.Kind k = s.flag;
            var parts = new ClothShape.Part[mesh.vertexCount];
            ClothShape.Counts counts = ClothShape.Sort(mesh, k.hold, k.holdDistance, k.joinPieces > 0f, parts, null);
            bool solid = go.TryGetComponent(out Collider own) && own.enabled;
            log.AppendLine($"FLAG {path} (place {place}): ClothDresser {(had ? "KEPT (updated)" : "ADDED")}; {counts.pieces} pieces ({counts.movingPieces} moving); {counts.moving} of its {mesh.vertexCount} points move, {counts.held} held along the pole (within {k.holdDistance} of the pole line), {counts.still} stay as modelled (the pole and its knob). Its collider {(solid ? "stays ON (the pole stays solid; a dart sticks where the flag rests)" : "is off or missing")}.");
            return true;
        }

        // ------------------------------------------------------------------------------------------------ the report

        private static void Summary(ClothSettings s, StringBuilder log)
        {
            log.AppendLine($"SETTINGS: Cloth On {On(s.clothOn)}; Builds Per Frame {s.buildsPerFrame}; Far Distance {s.farDistance} (Far Fade {s.farFade}, only for places with Off When Far); Player Pushes {On(s.playerPushes)} (toy radius x{s.toyRadius}, height x{s.toyHeight}; kid radius x{s.kidRadius}, height x{s.kidHeight} of the walking capsule; a look for players every {s.lookForPlayersSeconds} s); Shots Poke {On(s.shotsPoke)} (Poke Reach {s.pokeReach}, times the square root of the shooter's size).");
            KindLine("CURTAIN", s.curtain, log);
            KindLine("FLAG", s.flag, log);
            if (s.places != null)
                foreach (ClothSettings.Place p in s.places)
                    if (p != null) log.AppendLine($"PLACE {p.id}: {On(p.on)}, breeze {p.breeze} (gusts {p.gusts}, turn {p.turn}, tilt {p.tilt}), off when far {(p.offWhenFar ? "yes" : "no")}.");
        }

        private static void KindLine(string label, ClothSettings.Kind k, StringBuilder log)
        {
            log.AppendLine($"{label}: {On(k.on)}; hold from the {k.hold} {k.holdDistance}, join pieces {k.joinPieces}, {k.unitsPerMetre} units a metre; gravity {k.gravity}, damping {k.damping}, stretch {k.stretchStiffness}, bend {k.bendStiffness}, shape keep {k.shapeKeep} (x{k.shapeKeepAtEnd} at the far end), tether squash {k.tetherSquash}, thickness {k.thickness} m, speed limit {(k.limitSpeed ? k.speedLimit + " m/s" : "off")}; {(k.edgeCollision ? "edge" : "point")} collision, friction {k.friction}; wind influence {k.windInfluence}, frequency {k.windFrequency}, turbulence {k.windTurbulence}; shot poke {k.shotPoke}.");
        }

        private static string On(bool on) => on ? "ON" : "OFF";

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
#endif
