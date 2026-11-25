#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Editor maker for the Flat Zones (D-0198 cut A; the gates, the fill light and the new frame D-0203). "Land Hallway
    /// Scroller" does three things:
    /// 1. makes Resources/FlatView.asset (the side-on camera's numbers) when it is missing. It never writes over his
    ///    numbers, with ONE exception: a Frame Height still at cut A's 13 (the number the toy read small at) is set to
    ///    the script's new one, and the report says so; any other number he set stays;
    /// 2. makes the gate's piece (Prefabs/Flat/FlatGate_Portal: his portal drawing, the car portal's own look (its
    ///    "Model": the turning swirl and the ripple of marks) copied from a car portal prefab, sized to the hallway, a
    ///    glow on the floor, a flash light and a trigger) and its floor glow's material (Data/FlatGateGlow.mat) when
    ///    they are missing; what is there is kept as it is. It is NOT in Pieces/Traversal: Build Catalog would list it
    ///    as a Build Mode piece, and a gate without its zone does nothing;
    /// 3. places ONE object, FlatZone_Hall, at the TOP LEVEL of the open Greybox scene (a hallway rebuild wipes
    ///    everything under R3_SkirtingRun), with its entrance gate, its exit gate and its fill light under it. A
    ///    FlatZone_Hall already there is taken out and made again (its card texts and colours and its gates' colours
    ///    are carried over); nothing else in the scene is touched.
    /// It finds the hallway's things by name (the near wall WallS to cut away, the far wall, the ceiling, the three
    /// checkpoints, the step box, the four ropes) and falls back to the numbers measured on 24 Nov where one is not
    /// found; the report says which, and every number it used.
    /// It only works with Greybox open and active, out of Play. It never opens, closes or SAVES a scene: like
    /// Place Hallway Ropes (the other maker that lands things at the top level of the open Greybox), it marks the
    /// scene changed and leaves the saving to him (Ctrl+S), so a run that turns out wrong is undone by not saving, or
    /// by Ctrl+Z.
    /// <see cref="LandHallway"/> does the same as the menu and hands the report back as text.
    /// "Rebuild Portal Gate (its look)" makes the gate's piece again in place (its ids stay, so the gates in the scene
    /// follow): run it after the car portals change; a number changed by hand on the piece's parts is written over.
    /// </summary>
    public static class FlatPieces
    {
        private const string Tag = "[Flat]";
        private const string GreyboxPath = "Assets/_Game/Scenes/Greybox.unity";
        private const string ViewPath = "Assets/_Game/Resources/FlatView.asset";
        public const string GatePath = "Assets/_Game/Prefabs/Flat/FlatGate_Portal.prefab";
        private const string Data = "Assets/_Game/Data/";
        private const string GlowPath = Data + "FlatGateGlow.mat";
        private const string GlowSource = Data + "OrbGlow.mat";   // our orb's soft glow (his mote drawing): the car portals' floor glow is a copy of it too
        private const string SoundsPath = Data + "CourseSounds.asset";
        private static readonly string[] CarPortals =
        {
            "Assets/_Game/Prefabs/Pieces/Traversal/CarPortal_Blue.prefab",
            "Assets/_Game/Prefabs/Pieces/Traversal/CarPortal_Red.prefab",
            "Assets/_Game/Prefabs/Pieces/Traversal/CarPortal_Green.prefab",
        };
        public const string ZoneName = "FlatZone_Hall";
        private const string HallPath = "Room/Course/R3_SkirtingRun";
        private static readonly string[] RopeNames = { "Rope_HallBooks", "Rope_Hall2", "Rope_Hall3", "Rope_Hall4" };

        // ---- the hallway as measured on 24 Nov (CourseBuilder.House and the scene file): used where a thing is not found
        private const float WallSouthFace = -160f, WallNorthFace = -140f;
        private const float CeilingY = 14f;               // the ceiling's underside
        private const float PitFloorTop = -24.5f;         // where a fall into the sprint gap lands
        private const float AfterHopX = -25.25f, BeforeSprintX = 62f, AfterSprintX = 79.5f;   // the three checkpoints
        private const float StepBoxTop = 12.17f;
        private static readonly float[] RopeXs = { 24.98f, 30.18f, 35.38f, 40.58f };
        private const float ToyTall = 1.8f;               // the toy's capsule (the Player prefab)

        // ---- the zone (the plan's numbers, and where this differs from them, why)
        private const float LaneX = 30f;                  // the lane's own point along the hallway: the rail's ends are counted from it
        // The entrance gate stands across the lane at x -24 (past CP_afterHop's own spot, x -25.25, so a respawn there
        // is 3D, in front of the gate). The box starts 1 past it: a toy the zone holds is then always clear of the
        // gate, which the side-on camera sees edge-on, so the gate never stands in front of the toy in that picture
        // (the toy is 0.3 round; the gate is 0.3 thick).
        private const float GateX = -24f, GateClear = 1f;
        private const float ExitPast = 1.5f;              // the exit gate stands this far past CP_afterSprint's spot: x 81
        private const float ExitTouchAlong = 2f;          // the exit's trigger along the lane: the toy is let go within 1 of the portal's middle
        private const float BoxSide = 1f;                 // the box reaches this far past each wall's face, and over the ceiling
        // The plan's box ended at y -2. A fall into the sprint gap goes down to y -24.5 before the reset comes (on the
        // landing), so the zone let go half-way down. The box goes under that floor: the toy drops out of the bottom
        // of the side-on picture and comes back at the checkpoint, still side-on.
        private const float BoxUnderPit = 2.5f;
        // Where the camera's middle may travel. Cut A's west end (x -14) left the toy in the screen's bottom-left
        // corner on the way in (it walks in at about -22); at -20 a toy that has just come through the gate stands a
        // third of the way across, the gate a quarter.
        private const float RailWest = -20f, RailEast = 76f;
        // The plan's frame floor was 0: with 13 of the 14 shown, the toy's feet were on the bottom edge of the screen,
        // under the hotbar. 2 lower shows the floor's own edge under the feet.
        private const float FrameUnderFloor = 2f;
        // Cut A's frame never went over the ceiling's underside (14): on the step box (top 12.17) the toy's head was
        // on the screen's top edge. 2 over it the frame shows the ceiling's own edge (1 thick) and the far wall
        // behind it (it reaches y 27), and the toy on the box stands clear inside.
        private const float FrameOverCeiling = 2f;
        private const float CutAFrameHeight = 13f;        // FlatView's Frame Height in cut A: replaced by the script's new one (only if still this)
        private const float CutFrom = 3f;

        // ---- the gate's piece (his drawing as the car portals have it; pack units: the toy is 1.8 tall)
        private const float CarPortalOpening = 7f;        // the car portals' opening across (RacingPieces.PortalHole): their Model is sized to it
        private const float GateOpening = 4f;             // the gate's opening across: a toy and more (it reads as a doorway from far off)
        private const float GateSink = 0.9f;              // the drawing's lowest point stands this far under the floor: inside the hallway's floor (1 thick), so the side-on picture never shows it under the floor
        private const float GlowAcross = 1.3f;            // the floor glow, across the lane: times the opening (the car portals' is 1.3 too)
        private const float GlowAlong = 2.4f;             // and along the lane: short, so the glow is an oval under the gate, not a pool round the toy
        private const float GlowLift = 0.03f;
        private const float GlowAlpha = 0.7f;             // the car portals' floor glow
        private const float TouchAcross = 9f, TouchHigh = 8f, TouchAlong = 1.6f;   // the exit's trigger: the lane and more across, up to over the drawing, a toy's stride along
        private static readonly Color EntranceColour = new Color32(0xFF, 0x4F, 0xA3, 0xFF);   // candy pink (his star's pink, star.blend)
        private static readonly Color ExitColour = new Color(0.3f, 0.9f, 1f);                  // the orb's cyan

        [MenuItem("Shift Jam/Flat/Land Hallway Scroller")]
        public static void LandHallwayMenu()
        {
            string report = LandHallway();
            if (report.StartsWith(Tag + " NOT ")) Debug.LogWarning(report); else Debug.Log(report);
        }

        [MenuItem("Shift Jam/Flat/Rebuild Portal Gate (its look)")]
        public static void RebuildGateMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning($"{Tag} NOT rebuilt: this does not run in Play. Stop the game first."); return; }
            var did = new List<string>();
            bool ok = GatePiece(true, did) != null;
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Rebuild Portal Gate: {(ok ? "OK" : "FAILED")}. {string.Join("; ", did)}.");
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
            report.AppendLine($"{Tag} Land Hallway Scroller (D-0198 cut A, D-0203 gates)");

            // ---- 1. the assets: made when missing, never written over (but cut A's frame height)
            var assets = new List<string>();
            WorkshopPieces.EnsureFolder("Assets/_Game/Resources");
            bool viewThere = AssetDatabase.LoadAssetAtPath<FlatViewSettings>(ViewPath) != null;
            FlatViewSettings view = WorkshopPieces.SettingsAsset<FlatViewSettings>(ViewPath);
            if (!viewThere) assets.Add($"{ViewPath} MADE with the script's own numbers");
            else
            {
                var fresh = ScriptableObject.CreateInstance<FlatViewSettings>();
                float newFrame = fresh.frameHeight;
                Object.DestroyImmediate(fresh);
                if (Mathf.Approximately(view.frameHeight, CutAFrameHeight) && !Mathf.Approximately(newFrame, CutAFrameHeight))
                {
                    view.frameHeight = newFrame;
                    EditorUtility.SetDirty(view);
                    assets.Add($"{ViewPath} was there: kept as it is, but its Frame Height was still cut A's {Say(CutAFrameHeight)}: now {Say(newFrame)} (the toy reads bigger)");
                }
                else assets.Add($"{ViewPath} was there: kept as it is (Frame Height {Say(view.frameHeight)})");
            }
            GameObject gatePiece = GatePiece(false, assets);
            AssetDatabase.SaveAssets();
            report.AppendLine("ASSETS: " + string.Join("; ", assets) + ".");
            if (gatePiece == null)
                return $"{Tag} NOT landed: the gate's piece could not be made ({string.Join("; ", assets)}). The scene was not changed.";

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
            found.Add(wallN != null ? $"WallN (the far wall, the backdrop): found, its inner face z {Say(northFace)}, y {Say(wallN.bounds.min.y)} to {Say(wallN.bounds.max.y)}" : $"WallN not found: inner face z {Say(northFace)} (measured)");
            found.Add(ceilingPart != null ? $"Ceiling: found, underside y {Say(ceilingY)}, top y {Say(ceilingPart.bounds.max.y)}" : $"Ceiling not found: underside y {Say(ceilingY)} (measured)");

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
            float exitX = afterX + ExitPast;
            float boxWest = GateX + GateClear;
            float boxEast = exitX;                         // the box ends at the exit gate: see the report's BOX line
            float boxBottom = pitTop - BoxUnderPit, boxTopY = ceilingY + BoxSide;
            float boxSouth = southFace - BoxSide, boxNorth = northFace + BoxSide;
            Vector3 lanePoint = new Vector3(LaneX, 0f, laneZ);
            Vector3 boxMiddle = new Vector3((boxWest + boxEast) * 0.5f, (boxBottom + boxTopY) * 0.5f, (boxSouth + boxNorth) * 0.5f);
            Vector3 boxSize = new Vector3(boxEast - boxWest, boxTopY - boxBottom, boxNorth - boxSouth);
            float frameFloor = -FrameUnderFloor, frameCeiling = ceilingY + FrameOverCeiling;
            var warn = new List<string>();
            if (hopX >= GateX) warn.Add($"CP_afterHop's own spot (x {Say(hopX)}) is at or past the entrance gate (x {Say(GateX)}): a respawn there stands in the gate");
            if (beforeX <= boxWest || beforeX >= boxEast) warn.Add($"CP_beforeSprint (x {Say(beforeX)}) is outside the box: a fall into the sprint gap comes back in 3D");
            if (boxTop + ToyTall > ceilingY) warn.Add($"a toy ({Say(ToyTall)} tall) on the step box (top y {Say(boxTop)}) does not fit under the ceiling (y {Say(ceilingY)})");

            // ---- 4. the object: an old one out (what he chose on it kept), the new one in
            const string UndoName = "Land Hallway Scroller";
            string keptCard = null, keptClear = null; Color keptCardColour = default, keptClearColour = default; bool hadOld = false;
            Color entranceColour = EntranceColour, exitColour = ExitColour; bool keptEntrance = false, keptExit = false;
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != ZoneName) continue;
                FlatZone old = root.GetComponent<FlatZone>();
                if (old != null && !hadOld)
                {
                    var so = new SerializedObject(old);
                    keptCard = so.FindProperty("card").stringValue; keptClear = so.FindProperty("clearText").stringValue;
                    keptCardColour = so.FindProperty("cardColour").colorValue; keptClearColour = so.FindProperty("clearColour").colorValue;
                    hadOld = true;
                }
                foreach (FlatGate gate in root.GetComponentsInChildren<FlatGate>(true))
                {
                    var gs = new SerializedObject(gate);
                    bool isExit = gs.FindProperty("role").enumValueIndex == (int)FlatGate.Role.Exit;
                    Color c = gs.FindProperty("colour").colorValue;
                    if (isExit && !keptExit) { exitColour = c; keptExit = true; }
                    else if (!isExit && !keptEntrance) { entranceColour = c; keptEntrance = true; }
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
            Light fill = FillLight(zoneGo.transform, view);
            var zone = zoneGo.AddComponent<FlatZone>();
            WorkshopPieces.Set(zone, "settings", view);
            WorkshopPieces.Set(zone, "centre", boxMiddle - lanePoint);
            WorkshopPieces.Set(zone, "size", boxSize);
            WorkshopPieces.Set(zone, "lane", laneMark);
            WorkshopPieces.Set(zone, "railStart", RailWest - LaneX);
            WorkshopPieces.Set(zone, "railEnd", RailEast - LaneX);
            WorkshopPieces.Set(zone, "floor", frameFloor);
            WorkshopPieces.Set(zone, "ceiling", frameCeiling);
            WorkshopPieces.Set(zone, "toyOnly", true);
            WorkshopPieces.Set(zone, "cutaway", true);
            WorkshopPieces.Set(zone, "cutFrom", CutFrom);
            WorkshopPieces.Set(zone, "fill", fill);
            var always = new List<Object>();
            if (wallS != null) always.Add(wallS);
            WorkshopPieces.SetArray(zone, "alwaysCut", always);
            if (hadOld)
            {
                WorkshopPieces.Set(zone, "card", keptCard); WorkshopPieces.Set(zone, "clearText", keptClear);
                WorkshopPieces.Set(zone, "cardColour", keptCardColour); WorkshopPieces.Set(zone, "clearColour", keptClearColour);
            }

            // The gates. The way in is met in 3D, so it stands across the lane facing the toy that comes to it (its forward,
            // the blue arrow, points east, the way the toy goes through). The way out is met side-on, so it faces the side
            // camera (its forward points to the camera's side, south): across the lane the side camera would see it edge-on,
            // a thin line, all the way to it. Its trigger is then turned too: thin along the lane, so the toy is let go as
            // it reaches the portal's middle, and wide across it.
            var sounds = AssetDatabase.LoadAssetAtPath<CourseSounds>(SoundsPath);
            GameObject entrance = Gate(gatePiece, zoneGo.transform, "Entrance", new Vector3(GateX, 0f, laneZ), Vector3.right, FlatGate.Role.Entrance, entranceColour, sounds != null ? sounds.grow : default);
            GameObject exit = Gate(gatePiece, zoneGo.transform, "Exit", new Vector3(exitX, 0f, laneZ), Vector3.back, FlatGate.Role.Exit, exitColour, sounds != null ? sounds.shrink : default);
            if (exit.TryGetComponent(out BoxCollider exitTouch))
            {
                Vector3 size = exitTouch.size;   // made as (across, up, along) for a gate across the lane: turned to face the camera, across and along swap
                exitTouch.size = new Vector3(ExitTouchAlong, size.y, size.x);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = zoneGo;

            // ---- 5. the report: every number, and what the game's own code answers for them
            var texts = new SerializedObject(zone);
            report.AppendLine($"PLACED: {ZoneName} at the top level of Greybox, at {Say(lanePoint)}, not turned, scale 1"
                + (removed > 0 ? $" ({removed} older {ZoneName} taken out first; its card texts and colours and its gates' colours were carried over, every number is the builder's again)" : " (there was none before)")
                + $". Big cards: on the way in \"{texts.FindProperty("card").stringValue}\" ({Say(texts.FindProperty("cardColour").colorValue)}), on the way out \"{texts.FindProperty("clearText").stringValue}\" ({Say(texts.FindProperty("clearColour").colorValue)}).");
            report.AppendLine($"BOX (world): x {Say(boxWest)} to {Say(boxEast)}, y {Say(boxBottom)} to {Say(boxTopY)}, z {Say(boxSouth)} to {Say(boxNorth)} (size {Say(boxSize)}, middle {Say(boxMiddle)}). "
                + $"It starts {Say(GateClear)} past the entrance gate (x {Say(GateX)}; cut A started at -24 itself), so a held toy is always clear of the gate the side-on camera sees edge-on. "
                + $"It ends AT the exit gate: going out lets the player go at once, and only a player the zone holds can go out, so walking back in from the east passes through the exit without anything happening. "
                + $"Its bottom is {Say(BoxUnderPit)} under the sprint gap's floor (the plan said y -2: the zone let go half-way down a fall).");
            report.AppendLine($"LANE: point {Say(lanePoint)}, the camera's side -Z (child 'Lane', its blue arrow south); along the lane = +X. RAIL: the camera's middle travels x {Say(RailWest)} to {Say(RailEast)} (Rail Start {Say(RailWest - LaneX)}, Rail End {Say(RailEast - LaneX)}, counted from the lane's point; cut A's west end was -14). "
                + $"FRAME: never under y {Say(frameFloor)} or over y {Say(frameCeiling)} (cut A: over {Say(ceilingY)}, the ceiling's underside: the toy's head on the screen's top edge on the step box).");
            report.AppendLine("GATES (instances of " + GatePath + " under " + ZoneName + "): " + GateLine(entrance) + " | " + GateLine(exit) + ".");
            report.AppendLine($"FILL LIGHT: 'Fill' under {ZoneName}, a spot with no shadows, off until a player is held; then it rides with that player's camera, fading in and out with the swing (FlatView > Fill Light {Say(view.fillLight)} at the lane, 0 = off).");

            FlatRail rail = zone.Rail;
            FlatViewSettings v = rail.View;
            float d = rail.Distance(1f);
            report.AppendLine($"VIEW NUMBERS ({ViewPath}): frame height {Say(v.frameHeight)}, field of view {Say(v.fieldOfView)}, pitch {Say(v.pitch)}, feet in frame {Say(v.feetInFrame)}, lead {Say(v.lead)}, blend {Say(v.blendSeconds)} s, lane snap {Say(v.laneSnap)} a second (at most {Say(v.laneSnapSpeed)}), keep stick {Say(v.keepStickAngle)} degrees, fill {Say(v.fillLight)}. "
                + $"The camera stands {Say(d)} from the point it looks at; settled, the fill light is {Say(v.fillLight * d * d)} bright (Fill Light times its distance from the lane squared).");
            var views = new List<string>();
            views.Add(ViewLine(zone, "just through the entrance gate, taken", new Vector3(boxWest, 0f, laneZ)));
            views.Add(ViewLine(zone, "on the floor at the foot of the book stairs", new Vector3(-22f, 0f, laneZ)));
            views.Add(ViewLine(zone, "on top of the step box", new Vector3(12f, boxTop, laneZ)));
            views.Add(ViewLine(zone, "on the floor before the sprint gap (CP_beforeSprint)", new Vector3(beforeX, 0f, laneZ)));
            views.Add(ViewLine(zone, "at the exit gate", new Vector3(exitX, 0f, laneZ)));
            report.AppendLine("THE CAMERA, settled, standing still (the game's own sums, FlatZone.ViewFor): " + string.Join(" | ", views) + ".");

            var cut = new List<Renderer>();
            zone.CutList(cut);
            report.AppendLine($"CUT AWAY while the zone is on (the game's own search, FlatZone.CutList): {cut.Count} renderer(s): {Names(cut)}.");
            var kept = new List<Renderer>();
            zone.KeptInTheWay(kept);
            report.AppendLine($"NOT cut although they reach the camera's side of the lane in the stretch it sees (they cross the lane, or are spared: the floor, the ceiling, the steps and the zone's own gates belong here; anything else is in front of the picture): {kept.Count}: {Names(kept)}.");
            if (sounds == null) warn.Add($"no {SoundsPath}: the gates are silent");
            if (warn.Count > 0) report.AppendLine("LOOK AT: " + string.Join("; ", warn) + ".");
            report.Append("SAVING: the scene is marked changed and NOT saved (as Place Hallway Ropes does): save Greybox (Ctrl+S) to keep the zone, or close without saving to drop it. "
                + "OFF SWITCHES: untick FlatZone_Hall in the Hierarchy (the hallway plays as before, no gates); FlatView > Fill Light 0 (no fill light); a gate's object unticked (that gate gone; with the Exit gone nobody is let go but by walking out).");
            return report.ToString();
        }

        /// <summary>One gate: an instance of the gate's piece under the zone, its own forward pointing <paramref name="facing"/>,
        /// with its role, colour and sound.</summary>
        private static GameObject Gate(GameObject piece, Transform zone, string name, Vector3 at, Vector3 facing, FlatGate.Role role, Color colour, CourseSounds.Cue sound)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(piece, zone);
            go.name = name;
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(facing, Vector3.up));
            FlatGate gate = go.GetComponent<FlatGate>();
            if (gate == null) return go;
            WorkshopPieces.Set(gate, "role", (int)role);
            WorkshopPieces.Set(gate, "colour", colour);
            var so = new SerializedObject(gate);
            so.FindProperty("sound.clip").objectReferenceValue = sound.clip;
            so.FindProperty("sound.volume").floatValue = sound.clip != null && sound.volume > 0f ? sound.volume : 0.7f;
            so.FindProperty("sound.pitchSpread").floatValue = sound.pitchSpread;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>The zone's fill light, made off (the zone switches it on and sets its brightness while a player is held).</summary>
        private static Light FillLight(Transform zone, FlatViewSettings view)
        {
            var t = new GameObject("Fill").transform;
            t.SetParent(zone, false);
            var light = t.gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = view.fillColour;
            light.intensity = 0f;
            light.range = 60f;
            light.spotAngle = 50f;
            light.innerSpotAngle = 40f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        // ================================================================== the gate's piece

        /// <summary>The gate's piece: handed back as it is when it is there (unless <paramref name="rebuild"/>), made
        /// when it is not, from a car portal prefab's own look. Null if it could not be made.</summary>
        private static GameObject GatePiece(bool rebuild, List<string> did)
        {
            var there = AssetDatabase.LoadAssetAtPath<GameObject>(GatePath);
            if (there != null && !rebuild)
            {
                bool whole = there.GetComponent<FlatGate>() != null && there.GetComponent<Collider>() != null && there.transform.Find("Model") != null;
                did.Add($"{GatePath} was there: kept as it is{(whole ? "" : " (BUT it has no FlatGate script, no trigger or no Model on its root: run Shift Jam > Flat > Rebuild Portal Gate)")}");
                return there;
            }
            GameObject car = null; string carPath = null;
            foreach (string path in CarPortals)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null && p.transform.Find("Model") != null && p.transform.Find("Model/Swirl") != null) { car = p; carPath = path; break; }
            }
            if (car == null) { did.Add($"{GatePath} NOT made: no car portal with his drawing (Model/Swirl) in Pieces/Traversal. Run Shift Jam > Workshop > Build Car Portals first"); return null; }
            WorkshopPieces.EnsureFolder("Assets/_Game/Prefabs/Flat");
            Material glowLook = GlowMaterial(did);

            GameObject root = there != null ? PrefabUtility.LoadPrefabContents(GatePath) : new GameObject("FlatGate_Portal");
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                // The look: the car portal's own Model (his swirl, its dark middle, the rings of marks with their
                // ripple), copied whole (its references to its own rings come with it), sized by the opening.
                Transform carModel = car.transform.Find("Model");
                var model = Object.Instantiate(carModel.gameObject, root.transform, false);
                model.name = "Model";
                float scale = carModel.localScale.x * GateOpening / CarPortalOpening;
                model.transform.localRotation = carModel.localRotation;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = Vector3.zero;
                Transform swirl = model.transform.Find("Swirl");
                Bounds b = WorkshopPieces.LocalBounds(root.transform, swirl.gameObject);   // the swirl alone: the marks are off between ripples
                float middle = -GateSink - b.min.y;
                model.transform.localPosition = new Vector3(0f, middle, 0f);
                b = WorkshopPieces.LocalBounds(root.transform, swirl.gameObject);
                var glow = new List<Object>();
                foreach (Renderer r in swirl.GetComponentsInChildren<Renderer>(true)) glow.Add(r);
                PortalRipple ripple = model.GetComponentInChildren<PortalRipple>(true);

                // The glow on the floor: an oval under the gate, in the gate's colour (FlatGate lays it on).
                GameObject floorGlow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(floorGlow.GetComponent<Collider>());
                floorGlow.name = "FloorGlow";
                floorGlow.transform.SetParent(root.transform, false);
                floorGlow.transform.localPosition = new Vector3(0f, GlowLift, 0f);
                floorGlow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lying flat, facing up: its own y runs along the gate's forward
                floorGlow.transform.localScale = new Vector3(GateOpening * GlowAcross, GlowAlong, 1f);
                var glowRenderer = floorGlow.GetComponent<MeshRenderer>();
                if (glowLook != null) glowRenderer.sharedMaterial = glowLook;
                glowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                glowRenderer.receiveShadows = false;

                // The flash light, in the opening's middle, off until the gate plays.
                var lightT = new GameObject("FlashLight").transform;
                lightT.SetParent(root.transform, false);
                lightT.localPosition = new Vector3(0f, middle, 0f);
                var flash = lightT.gameObject.AddComponent<Light>();
                flash.type = LightType.Point;
                flash.range = GateOpening * 2f;
                flash.intensity = 0f;
                flash.shadows = LightShadows.None;
                flash.enabled = false;

                // The trigger (the exit uses it; an entrance switches it off) on the root, from the floor up.
                if (!root.TryGetComponent(out BoxCollider touch)) touch = root.AddComponent<BoxCollider>();
                touch.isTrigger = true;
                touch.center = new Vector3(0f, TouchHigh * 0.5f, 0f);
                touch.size = new Vector3(TouchAcross, TouchHigh, TouchAlong);

                if (!root.TryGetComponent(out FlatGate gate)) gate = root.AddComponent<FlatGate>();
                WorkshopPieces.Set(gate, "ripple", ripple);
                WorkshopPieces.SetArray(gate, "glow", glow);
                WorkshopPieces.SetArray(gate, "tinted", new List<Object> { glowRenderer });
                WorkshopPieces.Set(gate, "flashLight", flash);

                PrefabUtility.SaveAsPrefabAsset(root, GatePath);
                did.Add($"{GatePath} {(there != null ? "REBUILT in place" : "MADE")} from {carPath}'s Model: opening {Say(GateOpening)} across (the drawing at {Say(scale)} of the model), its lowest point y {Say(middle - GateOpening * 0.5f)}; "
                    + $"the drawing {Say(b.size.x)} across, y {Say(b.min.y)} to {Say(b.max.y)}, {Say(b.size.z)} thick; a floor glow {Say(GateOpening * GlowAcross)} by {Say(GlowAlong)}; a trigger {Say(touch.size)} (across, high, along); ripple {(ripple != null ? "found" : "NOT FOUND: no ripple")}");
            }
            finally
            {
                if (there != null) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(GatePath);
        }

        /// <summary>The gate's floor glow: kept as it is when it is there, made as a white copy of our orb glow (the
        /// gate lays its own colour over it) when it is not.</summary>
        private static Material GlowMaterial(List<string> did)
        {
            var look = AssetDatabase.LoadAssetAtPath<Material>(GlowPath);
            if (look != null) { did.Add($"{GlowPath} was there: kept"); return look; }
            var orb = AssetDatabase.LoadAssetAtPath<Material>(GlowSource);
            if (orb == null) { did.Add($"{GlowPath} NOT made (no {GlowSource} to copy): the floor glow is Unity's plain white"); return null; }
            look = new Material(orb) { name = "FlatGateGlow" };
            look.SetColor("_BaseColor", new Color(1f, 1f, 1f, GlowAlpha));
            AssetDatabase.CreateAsset(look, GlowPath);
            did.Add($"{GlowPath} MADE (a copy of {GlowSource}, white at {Say(GlowAlpha)})");
            return look;
        }

        /// <summary>One gate for the report: where it stands, what it is, what it draws and where its trigger reaches.</summary>
        private static string GateLine(GameObject go)
        {
            var gate = go.GetComponent<FlatGate>();
            var so = gate != null ? new SerializedObject(gate) : null;
            string role = so != null ? (so.FindProperty("role").enumValueIndex == (int)FlatGate.Role.Exit ? "EXIT" : "ENTRANCE") : "NO FlatGate SCRIPT";
            string colour = so != null ? Say(so.FindProperty("colour").colorValue) : "-";
            Transform swirl = go.transform.Find("Model/Swirl");
            string drawn = swirl != null && DrawnBounds(swirl.gameObject, out Bounds b)
                ? $"the drawing x {Say(b.min.x)} to {Say(b.max.x)}, y {Say(b.min.y)} to {Say(b.max.y)}, z {Say(b.min.z)} to {Say(b.max.z)}"
                : "no drawing found";
            string touch = "no trigger";
            if (go.TryGetComponent(out BoxCollider box))
            {
                Vector3 lo = go.transform.TransformPoint(box.center - box.size * 0.5f), hi = go.transform.TransformPoint(box.center + box.size * 0.5f);
                touch = $"its trigger x {Say(Mathf.Min(lo.x, hi.x))} to {Say(Mathf.Max(lo.x, hi.x))}, y {Say(Mathf.Min(lo.y, hi.y))} to {Say(Mathf.Max(lo.y, hi.y))}, z {Say(Mathf.Min(lo.z, hi.z))} to {Say(Mathf.Max(lo.z, hi.z))}{(role == "EXIT" ? "" : " (switched off in Play: an entrance is not touched)")}";
            }
            string sound = so != null && so.FindProperty("sound.clip").objectReferenceValue != null ? so.FindProperty("sound.clip").objectReferenceValue.name : "silent";
            Vector3 f = go.transform.forward;
            string facing = Mathf.Abs(f.x) >= Mathf.Abs(f.z) ? (f.x >= 0f ? "+X" : "-X") : (f.z >= 0f ? "+Z" : "-Z (the side camera)");
            return $"'{go.name}' {role} at {Say(go.transform.position)}, facing {facing}, colour {colour}, sound {sound}: {drawn}; {touch}";
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

        /// <summary>The settled camera for feet at <paramref name="feet"/>, and where a toy standing there sits in the
        /// picture (shares of a 16:9 screen, from the bottom and from the left).</summary>
        private static string ViewLine(FlatZone zone, string where, Vector3 feet)
        {
            zone.ViewFor(feet, 0f, 1f, out Vector3 position, out Quaternion rotation, out float fov, out float frame);
            FlatRail rail = zone.Rail;
            Vector3 looksAt = position + rotation * Vector3.forward * rail.Distance(1f);
            Vector3 e = rotation.eulerAngles;
            float bottom = looksAt.y - frame * 0.5f, left = looksAt.x - frame * 8f / 9f;
            float feetUp = (feet.y - bottom) / frame, headUp = (feet.y + ToyTall - bottom) / frame, across = (feet.x - left) / (frame * 16f / 9f);
            return $"feet {Say(feet)} ({where}): camera at {Say(position)}, turned pitch {Say(Mathf.DeltaAngle(0f, e.x))} yaw {Say(Mathf.DeltaAngle(0f, e.y))} (yaw 0 = looking north, +Z), field of view {Say(fov)}, "
                + $"looks at {Say(looksAt)}, shows y {Say(bottom)} to {Say(looksAt.y + frame * 0.5f)} at the lane ({Say(frame)} top to bottom) and x {Say(left)} to {Say(looksAt.x + frame * 8f / 9f)} on a 16:9 screen; "
                + $"the toy from {Say(feetUp * 100f)}% to {Say(headUp * 100f)}% of the height, {Say(across * 100f)}% across";
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
        private static string Say(Color c) => $"#{ColorUtility.ToHtmlStringRGB(c)}";
    }
}
#endif
