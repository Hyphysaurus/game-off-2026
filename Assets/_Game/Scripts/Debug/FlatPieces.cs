#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Editor maker for the Flat Zones (D-0198, cut A). "Land Hallway Scroller" does three things:
    /// 1. makes Resources/FlatView.asset (the side-on camera's numbers) when it is missing, and never writes over it;
    /// 2. makes the finish marker's piece (Prefabs/Pieces/Traversal/FlatFinish: a paper pennant on a pencil stood in a
    ///    lump of clay, from Unity's plain cylinder, ball and cube: no pack model, so it is there in any copy of the
    ///    project) and its five materials (Data/FlatFinish*.mat, recoloured copies of our own lit material) when they
    ///    are missing; what is there is kept as it is, so a colour changed on a material stays;
    /// 3. places ONE object, FlatZone_Hall, at the TOP LEVEL of the open Greybox scene (a hallway rebuild wipes
    ///    everything under R3_SkirtingRun), with the finish marker under it. A FlatZone_Hall already there is taken
    ///    out and made again (its two card texts are carried over); nothing else in the scene is touched.
    /// It finds the hallway's things by name (the near wall WallS to cut away, the far wall, the ceiling, the three
    /// checkpoints, the step box, the four ropes) and falls back to the numbers measured on 24 Nov where one is not
    /// found; the report says which, and every number it used.
    /// It only works with Greybox open and active, out of Play. It never opens, closes or SAVES a scene: like
    /// Place Hallway Ropes (the other maker that lands things at the top level of the open Greybox), it marks the
    /// scene changed and leaves the saving to him (Ctrl+S), so a run that turns out wrong is undone by not saving, or
    /// by Ctrl+Z.
    /// <see cref="LandHallway"/> does the same as the menu and hands the report back as text.
    /// "Rebuild Finish Marker" makes the piece's shape again in place (its ids stay); a number changed by hand on the
    /// piece's parts is written over by it.
    /// </summary>
    public static class FlatPieces
    {
        private const string Tag = "[Flat]";
        private const string GreyboxPath = "Assets/_Game/Scenes/Greybox.unity";
        private const string ViewPath = "Assets/_Game/Resources/FlatView.asset";
        public const string FinishPath = "Assets/_Game/Prefabs/Pieces/Traversal/FlatFinish.prefab";
        private const string Data = "Assets/_Game/Data/";
        private const string LookSource = Data + "ShiftCore.mat";   // our own lit material: the looks are recoloured copies of it
        private const string SoundsPath = Data + "CourseSounds.asset";
        public const string ZoneName = "FlatZone_Hall";
        private const string HallPath = "Room/Course/R3_SkirtingRun";
        private static readonly string[] RopeNames = { "Rope_HallBooks", "Rope_Hall2", "Rope_Hall3", "Rope_Hall4" };

        // ---- the hallway as measured on 24 Nov (CourseBuilder.House and the scene file): used where a thing is not found
        private const float LaneZ = -150f;                // the hallway's middle line
        private const float WallSouthFace = -160f, WallNorthFace = -140f;
        private const float CeilingY = 14f;               // the ceiling's underside
        private const float PitFloorTop = -24.5f;         // where a fall into the sprint gap lands
        private const float AfterHopX = -25.25f, BeforeSprintX = 62f, AfterSprintX = 79.5f;   // the three checkpoints
        private const float StepBoxTop = 12.17f;
        private static readonly float[] RopeXs = { 24.98f, 30.18f, 35.38f, 40.58f };

        // ---- the zone (the plan's numbers, and where this differs from them, why)
        private const float LaneX = 30f;                  // the lane's own point along the hallway: the rail's ends are counted from it
        private const float BoxWest = -24f;               // the box starts here: past CP_afterHop's own spot (x -25.25), so a respawn there is still 3D
        private const float FinishPast = 1.5f;            // the finish stands this far past CP_afterSprint's spot: x 81
        private const float BoxSide = 1f;                 // the box reaches this far past each wall's face, and over the ceiling
        // The plan's box ended at y -2. A fall into the sprint gap goes down to y -24.5 before the reset comes (on the
        // landing), so the zone let go half-way down. The box goes under that floor: the toy drops out of the bottom
        // of the side-on picture and comes back at the checkpoint, still side-on.
        private const float BoxUnderPit = 2.5f;
        private const float RailWest = -14f, RailEast = 76f;   // world x the camera's middle may travel between
        // The plan's frame floor was 0: with 13 of the 14 shown, the toy's feet were on the bottom edge of the screen,
        // under the hotbar. 2 lower shows the floor's own edge under the feet.
        private const float FrameUnderFloor = 2f;
        private const float CutFrom = 3f;

        // ---- the finish marker's shape (pack units: the toy is 1.8 tall). Unity's cylinder is 2 tall and 1 across.
        private const float MarkerBack = 1.3f;            // the pencil stands this far behind the lane, so the toy runs past in front of it
        private const float ClayHeight = 0.7f, ClayAcross = 1.6f;
        private const float PencilAcross = 0.36f, PencilFrom = 0.4f, PencilLength = 7f;   // sunk into the clay; its top at 7.4
        private const float BandLength = 0.4f, BandAcross = 0.4f;                          // the metal band, 7.4 to 7.8
        private const float EraserLength = 0.5f;                                            // the eraser, 7.8 to 8.3
        private const float PennantAt = 6.4f;             // the paper's middle height
        private const float PaperLong = 2.4f, PaperHigh = 1.4f, PaperThick = 0.05f;
        private static readonly Vector3 TouchSize = new Vector3(1.2f, 8f, 8f);   // the trigger: 1.2 along the lane, the hallway's lane and a little more across, up to the pencil's top
        private static readonly Color PencilColour = new Color(0.98f, 0.78f, 0.15f), BandColour = new Color(0.75f, 0.76f, 0.8f),
            EraserColour = new Color(0.95f, 0.5f, 0.6f), ClayColour = new Color(0.5f, 0.35f, 0.8f), PaperColour = new Color(0.72f, 0.72f, 0.74f);

        [MenuItem("Shift Jam/Flat/Land Hallway Scroller")]
        public static void LandHallwayMenu()
        {
            string report = LandHallway();
            if (report.StartsWith(Tag + " NOT ")) Debug.LogWarning(report); else Debug.Log(report);
        }

        [MenuItem("Shift Jam/Flat/Rebuild Finish Marker (its shape)")]
        public static void RebuildFinishMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning($"{Tag} NOT rebuilt: this does not run in Play. Stop the game first."); return; }
            var did = new List<string>();
            bool ok = FinishPiece(true, did) != null;
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Rebuild Finish Marker: {(ok ? "OK" : "FAILED")}. {string.Join("; ", did)}.");
        }

        /// <summary>What the menu item does, with the report handed back as text (one line a fact). A report that starts
        /// "[Flat] NOT " means nothing was changed.</summary>
        public static string LandHallway()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return $"{Tag} NOT landed: this does not run in Play. Stop the game first. Nothing was changed.";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != GreyboxPath)
                return $"{Tag} NOT landed: the active scene is '{scene.path}', and the hallway is in {GreyboxPath}. Open Greybox and run this again (it never opens a scene by itself). Nothing was changed.";

            var report = new StringBuilder();
            report.AppendLine($"{Tag} Land Hallway Scroller (D-0198, cut A)");

            // ---- 1. the assets: made when missing, never written over
            var assets = new List<string>();
            WorkshopPieces.EnsureFolder("Assets/_Game/Resources");
            bool viewThere = AssetDatabase.LoadAssetAtPath<FlatViewSettings>(ViewPath) != null;
            FlatViewSettings view = WorkshopPieces.SettingsAsset<FlatViewSettings>(ViewPath);
            assets.Add($"{ViewPath} {(viewThere ? "was there: kept as it is" : "MADE with the script's own numbers")}");
            GameObject finishPiece = FinishPiece(false, assets);
            AssetDatabase.SaveAssets();
            report.AppendLine("ASSETS: " + string.Join("; ", assets) + ".");
            if (finishPiece == null)
                return $"{Tag} NOT landed: the finish marker's piece could not be made ({string.Join("; ", assets)}). The scene was not changed.";

            // ---- 2. the hallway's things, by name
            var found = new List<string>();
            Transform hall = null;
            GameObject hallGo = GameObject.Find(HallPath);
            if (hallGo != null) hall = hallGo.transform;
            found.Add(hall != null ? $"{HallPath} found" : $"{HallPath} NOT FOUND: every number below is the measured one");

            Renderer wallS = PartRenderer(hall, "WallS"), wallN = PartRenderer(hall, "WallN"), ceilingPart = PartRenderer(hall, "Ceiling");
            float southFace = wallS != null ? wallS.bounds.max.z : WallSouthFace;
            float northFace = wallN != null ? wallN.bounds.min.z : WallNorthFace;
            float ceilingY = ceilingPart != null ? ceilingPart.bounds.min.y : CeilingY;
            float laneZ = (southFace + northFace) * 0.5f;
            found.Add(wallS != null ? $"WallS (the near wall, cut away): found, x {Say(wallS.bounds.min.x)} to {Say(wallS.bounds.max.x)}, y {Say(wallS.bounds.min.y)} to {Say(wallS.bounds.max.y)}, its inner face z {Say(southFace)}" : $"WallS NOT FOUND: inner face z {Say(southFace)} (measured); Always Cut is EMPTY, the cutaway takes what it finds itself");
            found.Add(wallN != null ? $"WallN (the far wall, the backdrop): found, its inner face z {Say(northFace)}" : $"WallN not found: inner face z {Say(northFace)} (measured)");
            found.Add(ceilingPart != null ? $"Ceiling: found, underside y {Say(ceilingY)}" : $"Ceiling not found: underside y {Say(ceilingY)} (measured)");

            float hopX = PartX(hall, "CP_afterHop", AfterHopX, found);
            float beforeX = PartX(hall, "CP_beforeSprint", BeforeSprintX, found);
            float afterX = PartX(hall, "CP_afterSprint", AfterSprintX, found);
            Renderer pit = PartRenderer(hall, "PitFloorWest");
            float pitTop = pit != null ? pit.bounds.max.y : PitFloorTop;
            found.Add(pit != null ? $"PitFloorWest (where a fall into the sprint gap lands): found, its top y {Say(pitTop)}" : $"PitFloorWest not found: its top y {Say(pitTop)} (measured)");
            Transform stepBox = hall != null ? hall.Find("StepBox") : null;
            float boxTop = StepBoxTop;
            if (stepBox != null && DrawnBounds(stepBox.gameObject, out Bounds sb)) { boxTop = sb.max.y; found.Add($"StepBox: found, its top y {Say(boxTop)}, x {Say(sb.min.x)} to {Say(sb.max.x)}"); }
            else found.Add($"StepBox not found: its top y {Say(boxTop)} (measured)");
            var ropes = new List<string>();
            for (int i = 0; i < RopeNames.Length; i++)
            {
                Transform rope = RootNamed(scene, RopeNames[i]);
                ropes.Add(rope != null ? $"{RopeNames[i]} found at x {Say(rope.position.x)}, z {Say(rope.position.z)}" : $"{RopeNames[i]} NOT FOUND at the top level (measured: x {Say(RopeXs[i])})");
            }
            found.Add("the ropes (not changed, only read): " + string.Join(", ", ropes));
            report.AppendLine("FOUND: " + string.Join("; ", found) + ".");

            // ---- 3. the numbers
            float finishX = afterX + FinishPast;
            float boxEast = finishX;                       // the box ends at the finish: see the report's BOX line
            float boxBottom = pitTop - BoxUnderPit, boxTopY = ceilingY + BoxSide;
            float boxSouth = southFace - BoxSide, boxNorth = northFace + BoxSide;
            Vector3 lanePoint = new Vector3(LaneX, 0f, laneZ);
            Vector3 boxMiddle = new Vector3((BoxWest + boxEast) * 0.5f, (boxBottom + boxTopY) * 0.5f, (boxSouth + boxNorth) * 0.5f);
            Vector3 boxSize = new Vector3(boxEast - BoxWest, boxTopY - boxBottom, boxNorth - boxSouth);
            float frameFloor = -FrameUnderFloor;
            var warn = new List<string>();
            if (hopX >= BoxWest) warn.Add($"CP_afterHop's own spot (x {Say(hopX)}) is INSIDE the box (it starts at x {Say(BoxWest)}): a respawn there starts side-on");
            if (beforeX <= BoxWest || beforeX >= boxEast) warn.Add($"CP_beforeSprint (x {Say(beforeX)}) is outside the box: a fall into the sprint gap comes back in 3D");
            if (boxTop + 1.8f > ceilingY) warn.Add($"a toy (1.8 tall) on the step box (top y {Say(boxTop)}) does not fit under the ceiling (y {Say(ceilingY)})");

            // ---- 4. the object: an old one out, the new one in
            const string UndoName = "Land Hallway Scroller";
            string keptCard = null, keptClear = null; bool hadOld = false;
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != ZoneName) continue;
                FlatZone old = root.GetComponent<FlatZone>();
                if (old != null && !hadOld)
                {
                    var so = new SerializedObject(old);
                    keptCard = so.FindProperty("card").stringValue; keptClear = so.FindProperty("clearText").stringValue; hadOld = true;
                }
                Undo.DestroyObjectImmediate(root);
                removed++;
            }

            var zoneGo = new GameObject(ZoneName);
            Undo.RegisterCreatedObjectUndo(zoneGo, UndoName);
            if (zoneGo.scene != scene) SceneManager.MoveGameObjectToScene(zoneGo, scene);
            zoneGo.transform.SetPositionAndRotation(lanePoint, Quaternion.identity);
            var laneMark = new GameObject("Lane").transform;
            laneMark.SetParent(zoneGo.transform, false);
            laneMark.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);   // its blue arrow points south: the camera stands on the south side and looks north
            var zone = zoneGo.AddComponent<FlatZone>();
            WorkshopPieces.Set(zone, "settings", view);
            WorkshopPieces.Set(zone, "centre", boxMiddle - lanePoint);
            WorkshopPieces.Set(zone, "size", boxSize);
            WorkshopPieces.Set(zone, "lane", laneMark);
            WorkshopPieces.Set(zone, "railStart", RailWest - LaneX);
            WorkshopPieces.Set(zone, "railEnd", RailEast - LaneX);
            WorkshopPieces.Set(zone, "floor", frameFloor);
            WorkshopPieces.Set(zone, "ceiling", ceilingY);
            WorkshopPieces.Set(zone, "toyOnly", true);
            WorkshopPieces.Set(zone, "cutaway", true);
            WorkshopPieces.Set(zone, "cutFrom", CutFrom);
            var always = new List<Object>();
            if (wallS != null) always.Add(wallS);
            WorkshopPieces.SetArray(zone, "alwaysCut", always);
            if (hadOld) { WorkshopPieces.Set(zone, "card", keptCard); WorkshopPieces.Set(zone, "clearText", keptClear); }

            var finish = (GameObject)PrefabUtility.InstantiatePrefab(finishPiece, zoneGo.transform);
            finish.name = "Finish";
            // Its own right runs along the lane, onward; its own forward points away from the camera (behind the lane).
            finish.transform.SetPositionAndRotation(new Vector3(finishX, 0f, laneZ), Quaternion.LookRotation(Vector3.forward, Vector3.up));
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = zoneGo;

            // ---- 5. the report: every number, and what the game's own code answers for them
            var texts = new SerializedObject(zone);
            report.AppendLine($"PLACED: {ZoneName} at the top level of Greybox, at {Say(lanePoint)}, not turned, scale 1"
                + (removed > 0 ? $" ({removed} older {ZoneName} taken out first; its card texts were carried over, every number is the builder's again)" : " (there was none before)")
                + $". Cards: on the way in \"{texts.FindProperty("card").stringValue}\", at the finish \"{texts.FindProperty("clearText").stringValue}\".");
            report.AppendLine($"BOX (world): x {Say(BoxWest)} to {Say(boxEast)}, y {Say(boxBottom)} to {Say(boxTopY)}, z {Say(boxSouth)} to {Say(boxNorth)} (size {Say(boxSize)}, middle {Say(boxMiddle)}). "
                + $"It ends AT the finish (the plan said x 84): a tag lets the player go at once, and a tag only counts for a player the zone holds, so walking back in from the east passes the pennant without tagging it. "
                + $"Its bottom is {Say(BoxUnderPit)} under the sprint gap's floor (the plan said y -2: the zone let go half-way down a fall).");
            report.AppendLine($"LANE: point {Say(lanePoint)}, the camera's side -Z (child 'Lane', its blue arrow south); along the lane = +X. RAIL: the camera's middle travels x {Say(RailWest)} to {Say(RailEast)} (Rail Start {Say(RailWest - LaneX)}, Rail End {Say(RailEast - LaneX)}, counted from the lane's point). "
                + $"FRAME: never under y {Say(frameFloor)} or over y {Say(ceilingY)} (the plan's floor was 0: the feet sat on the screen's bottom edge).");
            report.AppendLine($"FINISH: 'Finish' (an instance of {FinishPath}) under {ZoneName}, at {Say(new Vector3(finishX, 0f, laneZ))}: {Say(FinishPast)} past CP_afterSprint's spot; the pencil stands {Say(MarkerBack)} behind the lane (z {Say(laneZ + MarkerBack)}), its top y {Say(PencilFrom + PencilLength + BandLength + EraserLength)}; the trigger is {Say(TouchSize)} round {Say(new Vector3(finishX, TouchSize.y * 0.5f, laneZ))}.");

            FlatRail rail = zone.Rail;
            FlatViewSettings v = rail.View;
            report.AppendLine($"VIEW NUMBERS ({ViewPath}): frame height {Say(v.frameHeight)}, field of view {Say(v.fieldOfView)}, pitch {Say(v.pitch)}, feet in frame {Say(v.feetInFrame)}, lead {Say(v.lead)}, blend {Say(v.blendSeconds)} s, lane snap {Say(v.laneSnap)} a second (at most {Say(v.laneSnapSpeed)}), keep stick {Say(v.keepStickAngle)} degrees. "
                + $"The camera stands {Say(rail.Distance(1f))} from the point it looks at.");
            var views = new List<string>();
            views.Add(ViewLine(zone, "on the floor at the foot of the book stairs", new Vector3(-22f, 0f, laneZ)));
            views.Add(ViewLine(zone, "on the floor at the lane's own point", new Vector3(LaneX, 0f, laneZ)));
            views.Add(ViewLine(zone, "on top of the step box", new Vector3(12f, boxTop, laneZ)));
            views.Add(ViewLine(zone, "on the floor before the sprint gap (CP_beforeSprint)", new Vector3(beforeX, 0f, laneZ)));
            views.Add(ViewLine(zone, "at the finish", new Vector3(finishX, 0f, laneZ)));
            report.AppendLine("THE CAMERA, settled, standing still (the game's own sums, FlatZone.ViewFor): " + string.Join(" | ", views) + ".");

            var cut = new List<Renderer>();
            zone.CutList(cut);
            report.AppendLine($"CUT AWAY while the zone is on (the game's own search, FlatZone.CutList): {cut.Count} renderer(s): {Names(cut)}.");
            var kept = new List<Renderer>();
            zone.KeptInTheWay(kept);
            report.AppendLine($"NOT cut although they reach the camera's side of the lane in the stretch it sees (they cross the lane, or are spared: the floor, the ceiling, the steps and the zone's own pieces belong here; anything else is in front of the picture): {kept.Count}: {Names(kept)}.");
            if (warn.Count > 0) report.AppendLine("LOOK AT: " + string.Join("; ", warn) + ".");
            report.Append("SAVING: the scene is marked changed and NOT saved (as Place Hallway Ropes does): save Greybox (Ctrl+S) to keep the zone, or close without saving to drop it. OFF SWITCH: untick FlatZone_Hall in the Hierarchy.");
            return report.ToString();
        }

        // ================================================================== the finish marker's piece

        /// <summary>The finish marker's piece: handed back as it is when it is there (unless <paramref name="rebuild"/>),
        /// made when it is not. Null if it could not be made.</summary>
        private static GameObject FinishPiece(bool rebuild, List<string> did)
        {
            var there = AssetDatabase.LoadAssetAtPath<GameObject>(FinishPath);
            if (there != null && !rebuild)
            {
                bool whole = there.GetComponent<FlatFinish>() != null && there.GetComponent<Collider>() != null;
                did.Add($"{FinishPath} was there: kept as it is{(whole ? "" : " (BUT it has no FlatFinish script or no trigger on its root: run Shift Jam > Flat > Rebuild Finish Marker)")}");
                return there;
            }
            WorkshopPieces.EnsureFolder("Assets/_Game/Prefabs/Pieces/Traversal");
            Material pencil = Look(Data + "FlatFinishPencil.mat", PencilColour, 0.35f, 0f, did);
            Material band = Look(Data + "FlatFinishBand.mat", BandColour, 0.7f, 0.9f, did);
            Material eraser = Look(Data + "FlatFinishEraser.mat", EraserColour, 0.2f, 0f, did);
            Material clay = Look(Data + "FlatFinishClay.mat", ClayColour, 0.15f, 0f, did);
            Material paper = Look(Data + "FlatFinishPaper.mat", PaperColour, 0.05f, 0f, did);

            GameObject root = there != null ? PrefabUtility.LoadPrefabContents(FinishPath) : new GameObject("FlatFinish");
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                if (!root.TryGetComponent(out BoxCollider touch)) touch = root.AddComponent<BoxCollider>();
                touch.isTrigger = true;
                touch.center = new Vector3(0f, TouchSize.y * 0.5f, 0f);
                touch.size = TouchSize;
                if (!root.TryGetComponent(out FlatFinish finish)) finish = root.AddComponent<FlatFinish>();

                // Everything drawn stands behind the lane (the piece's own forward points away from the camera).
                Transform marker = new GameObject("Marker").transform;
                marker.SetParent(root.transform, false);
                marker.localPosition = new Vector3(0f, 0f, MarkerBack);
                Part(PrimitiveType.Sphere, "Clay", marker, clay, new Vector3(0f, ClayHeight * 0.5f, 0f), new Vector3(ClayAcross, ClayHeight, ClayAcross));
                Part(PrimitiveType.Cylinder, "Pencil", marker, pencil, new Vector3(0f, PencilFrom + PencilLength * 0.5f, 0f), new Vector3(PencilAcross, PencilLength * 0.5f, PencilAcross));
                float bandFrom = PencilFrom + PencilLength;
                Part(PrimitiveType.Cylinder, "Band", marker, band, new Vector3(0f, bandFrom + BandLength * 0.5f, 0f), new Vector3(BandAcross, BandLength * 0.5f, BandAcross));
                Part(PrimitiveType.Cylinder, "Eraser", marker, eraser, new Vector3(0f, bandFrom + BandLength + EraserLength * 0.5f, 0f), new Vector3(PencilAcross, EraserLength * 0.5f, PencilAcross));
                // The pennant turns about the pencil's line. At rest the paper hangs back the way the toy comes from
                // (the piece's own left); a tag swings it round to point onward.
                Transform pennant = new GameObject("Pennant").transform;
                pennant.SetParent(marker, false);
                pennant.localPosition = new Vector3(0f, PennantAt, 0f);
                Transform sheet = Part(PrimitiveType.Cube, "Paper", pennant, paper, new Vector3(-(PencilAcross * 0.5f + PaperLong * 0.5f), 0f, 0f), new Vector3(PaperLong, PaperHigh, PaperThick));

                WorkshopPieces.Set(finish, "pennant", pennant);
                WorkshopPieces.Set(finish, "paper", sheet.GetComponent<Renderer>());
                // The sound: the course's own "a core lights up" cue, when the course sounds are there and it has none yet.
                var so = new SerializedObject(finish);
                if (so.FindProperty("sound.clip").objectReferenceValue == null)
                {
                    var sounds = AssetDatabase.LoadAssetAtPath<CourseSounds>(SoundsPath);
                    if (sounds != null && sounds.coreAwake.clip != null)
                    {
                        so.FindProperty("sound.clip").objectReferenceValue = sounds.coreAwake.clip;
                        so.FindProperty("sound.volume").floatValue = sounds.coreAwake.volume > 0f ? sounds.coreAwake.volume : 0.6f;
                        so.FindProperty("sound.pitchSpread").floatValue = sounds.coreAwake.pitchSpread;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        did.Add("the tag's sound: the course's 'core awake' cue");
                    }
                    else did.Add($"the tag's sound: NONE ({SoundsPath} or its 'core awake' clip is not there): the finish card's own sound still plays");
                }
                PrefabUtility.SaveAsPrefabAsset(root, FinishPath);
                did.Add($"{FinishPath} {(there != null ? "REBUILT in place" : "MADE")}: a pencil {Say(PencilLength + BandLength + EraserLength)} long stood in a lump of clay, top y {Say(bandFrom + BandLength + EraserLength)}, a paper pennant {Say(PaperLong)} by {Say(PaperHigh)} at y {Say(PennantAt)}, a trigger {Say(TouchSize)}");
            }
            finally
            {
                if (there != null) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(FinishPath);
        }

        /// <summary>One of Unity's plain shapes as a part of the marker: no collider (the toy runs past in front of it).</summary>
        private static Transform Part(PrimitiveType kind, string name, Transform parent, Material look, Vector3 at, Vector3 size)
        {
            GameObject part = GameObject.CreatePrimitive(kind);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;
            if (look != null) part.GetComponent<MeshRenderer>().sharedMaterial = look;
            return part.transform;
        }

        /// <summary>One of the marker's materials: kept as it is when it is there (its colour is its own), made as a
        /// recoloured copy of our lit material, with no glow, when it is not.</summary>
        private static Material Look(string path, Color colour, float smoothness, float metallic, List<string> did)
        {
            var look = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (look != null) { did.Add($"{path} was there: kept"); return look; }
            if (!AssetDatabase.CopyAsset(LookSource, path)) { did.Add($"{path} NOT made (could not copy {LookSource}): that part keeps Unity's plain grey"); return null; }
            look = AssetDatabase.LoadAssetAtPath<Material>(path);
            look.SetColor("_BaseColor", colour);
            if (look.HasProperty("_Color")) look.SetColor("_Color", colour);
            if (look.HasProperty("_Smoothness")) look.SetFloat("_Smoothness", smoothness);
            if (look.HasProperty("_Metallic")) look.SetFloat("_Metallic", metallic);
            // The glow stays switched on in the material and black: the pennant's paper is lit up by the script on a
            // tag (a property block), which only shows on a material that has its glow switched on.
            if (look.HasProperty("_EmissionColor"))
            {
                look.EnableKeyword("_EMISSION");
                look.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                look.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(look);
            did.Add($"{path} MADE");
            return look;
        }

        // ================================================================== small helpers

        private static Renderer PartRenderer(Transform hall, string name)
        {
            Transform t = hall != null ? hall.Find(name) : null;
            return t != null ? t.GetComponent<Renderer>() : null;
        }

        /// <summary>A named part's place along the hallway (world x), or the measured number when it is not there.</summary>
        private static float PartX(Transform hall, string name, float measured, List<string> found)
        {
            Transform t = hall != null ? hall.Find(name) : null;
            if (t == null) { found.Add($"{name} not found: x {Say(measured)} (measured)"); return measured; }
            found.Add($"{name}: found at x {Say(t.position.x)}{(Mathf.Abs(t.position.x - measured) > 0.01f ? $" (measured on 24 Nov: {Say(measured)})" : "")}");
            return t.position.x;
        }

        private static Transform RootNamed(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root.transform;
            return null;
        }

        /// <summary>World bounds of everything drawn under <paramref name="go"/>. False when nothing is.</summary>
        private static bool DrawnBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        private static string ViewLine(FlatZone zone, string where, Vector3 feet)
        {
            zone.ViewFor(feet, 0f, 1f, out Vector3 position, out Quaternion rotation, out float fov, out float frame);
            FlatRail rail = zone.Rail;
            Vector3 looksAt = position + rotation * Vector3.forward * rail.Distance(1f);
            Vector3 e = rotation.eulerAngles;
            return $"feet {Say(feet)} ({where}): camera at {Say(position)}, turned pitch {Say(Mathf.DeltaAngle(0f, e.x))} yaw {Say(Mathf.DeltaAngle(0f, e.y))} (yaw 0 = looking north, +Z), field of view {Say(fov)}, "
                + $"looks at {Say(looksAt)}, shows y {Say(looksAt.y - frame * 0.5f)} to {Say(looksAt.y + frame * 0.5f)} at the lane ({Say(frame)} top to bottom) and x {Say(looksAt.x - frame * 8f / 9f)} to {Say(looksAt.x + frame * 8f / 9f)} on a 16:9 screen";
        }

        /// <summary>The renderers' names, the same names counted together, the most common first.</summary>
        private static string Names(List<Renderer> list)
        {
            if (list.Count == 0) return "none";
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (Renderer r in list)
            {
                if (r == null) continue;
                Transform t = r.transform;
                string name = t.parent != null && t.parent.parent != null ? t.parent.name + "/" + t.name : t.name;
                if (counts.TryGetValue(name, out int n)) counts[name] = n + 1; else { counts[name] = 1; order.Add(name); }
            }
            order.Sort((a, b) => counts[b] != counts[a] ? counts[b].CompareTo(counts[a]) : string.CompareOrdinal(a, b));
            var parts = new List<string>();
            foreach (string name in order) parts.Add(counts[name] > 1 ? $"{name} x{counts[name]}" : name);
            return string.Join(", ", parts);
        }

        private static string Say(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Say(Vector3 v) => $"({Say(v.x)}, {Say(v.y)}, {Say(v.z)})";
    }
}
#endif
