#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
    ///    everything under R3_SkirtingRun), with its entrance gate, its exit gate, its fill light and its five
    ///    S-H-I-F-T letters (D-0208) under it. A FlatZone_Hall already there is taken out and made again (its card
    ///    texts and colours, its letters' card, and its gates' colours are carried over); nothing else in the scene
    ///    is touched.
    /// The letters' two materials (Data/FlatLetter.mat, unlit, and Data/FlatLetterGhost.mat, unlit and see-through,
    /// both copies of the menu slab's) are made when missing; the letters' shapes are the title's own
    /// (Art/Title/Letters/Title_X.asset, baked by Shift Jam > Menu > Build Title Menu Model): without them the zone
    /// gets no letters and the report says so. Where each letter stands is worked out from the hallway's own things
    /// (a book of the stairs, the ropes, the sprint gap's checkpoints) and the toy's jump (Data/MovementSettings) and
    /// the ropes' hold (Resources/Rope); the report gives every number and why each letter is reachable.
    /// It finds the hallway's things by name (the near wall WallS to cut away, the far wall, the ceiling, the three
    /// checkpoints, the step box, the four ropes) and falls back to the numbers measured on 24 Nov where one is not
    /// found; the report says which, and every number it used.
    /// It only works with Greybox open and active, out of Play. It never opens, closes or SAVES a scene: like
    /// Place Hallway Ropes (the other maker that lands things at the top level of the open Greybox), it marks the
    /// scene changed and leaves the saving to him (Ctrl+S), so a run that turns out wrong is undone by not saving, or
    /// by Ctrl+Z.
    /// THE PIXEL LOOK (D-0209): "Add Pixel Look (renderer feature)" (<see cref="AddPixelLook"/>, the report as text) makes
    /// the palette (Art/Flat/PixelShiftPalette.png, one texel a colour: his title's 14 and 7 warm ones for the toy room)
    /// and Data/PixelShift.mat (Shaders/PixelShift.shader) when missing, and adds a Full Screen Pass feature "PixelShift"
    /// to Assets/Settings/PC_Renderer when it has none (made the way the renderer's own Inspector adds one, as
    /// PaintBuilder adds the Decal feature; off, after post-processing, after the shift warp's). Land Hallway Scroller
    /// then hands that feature to the zone, with Pixel Look OFF (his choice on an older FlatZone_Hall is kept).
    /// THE 3D ROUTE (D-0212: the stretch is optional once done, and walked in 3D both ways): Land Hallway Scroller also
    /// places ONE more top-level object, FlatRoute_Hall (taken out and made again on every run; nothing else touched):
    /// steps up the StepBox's east face (a wooden bar from the Playroom pack per step, each rise at most 0.6 of the toy's
    /// jump, as the book stairs' 1.2) and a plank over the sprint gap (a ruler from the pack, found by name, at least 3
    /// across), both wholly between the lane and the side camera, past Cut From, so the side view's cutaway hides them
    /// in the flat run. NOT under FlatZone_Hall: the zone's own pieces are never cut away. The report gives every piece's
    /// place and height, what each touches, the book stairs going down west, the floor under the ropes, the take rule's
    /// answers, and where the exit's link back lands.
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
        // The rifts (D-0207): the way in is a pixel rift (a sprite sheet on a quad), the way out a 3D rift (a model). Both
        // are made from their art when it is in the project and the gate prefabs are missing; without the art the
        // hallway keeps his portal at both ends. They are stand-ins until he draws his own: replace the art files (same
        // names) and run Shift Jam > Flat > Rebuild Rift Gates.
        public const string RiftInPath = "Assets/_Game/Prefabs/Flat/FlatGate_RiftIn.prefab";
        public const string RiftOutPath = "Assets/_Game/Prefabs/Flat/FlatGate_RiftOut.prefab";
        private const string RiftArt = "Assets/_Game/Art/Flat/";
        private const string RiftInSheet = RiftArt + "ShiftRiftIn_sheet.png";       // 16 frames of 64 x 128 side by side
        private const string RiftOutModel = RiftArt + "ShiftRiftOut.fbx";           // 2 tall, materials RiftEdge, RiftShardPink, RiftInside
        private const string WarpArt = "Assets/_Game/Art/Portals/warp_art_grow.png"; // his shift-warp swirl, inside the 3D rift
        private const string SlabLook = "Assets/_Game/Data/MenuSlab.mat";            // an unlit material: the rifts' materials are copies of it
        private const int RiftInFrames = 16;
        private const float RiftInPixel = 0.05f;      // world units a pixel of the pixel rift: 64 x 128 px = 3.2 x 6.4
        private const float RiftInBelow = 0.3f;       // the sheet's empty rows under the tear's tip: the tip stands on the floor
        private const float RiftOutScale = 3.1f;      // the 3D rift is 2 tall as made: 6.2 tall, as tall as the portal's drawing
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

        // ---- the letters (D-0208): his title's letters to find, side-on. Where they stand, and why:
        // S: over a tread of the book stairs (Book5, the sixth book, its tread x -10 to -7.6, top 6.64), its bottom 0.2
        //    over the head of a toy standing on the NEXT book (Book6, top 7.74): walking up never touches it; a full jump
        //    from Book5 does (the head rises 2.0 to 10.44, 0.69 into it); a tapped jump (a quarter of the height) does not.
        // H, I, F: halfway between rope 1 and 2, 2 and 3, 3 and 4 ("mid-swing between the ropes"), their bottom where the
        //    head of a toy swinging on the rope before reaches at a 30 degree swing (hands Hang Hold 5 under the tie):
        //    hanging or a small swing never touches them, a real swing or a let-go on the way up does.
        //    (The plan had H over the step box's top: the box top is 12.17 and the ceiling 14, so a toy (1.8) on it
        //    cannot jump and a 1.8 letter does not fit over it; a letter ON it would be taken by everyone walking by.)
        // T: over the middle of the sprint gap, its bottom 0.8 over a standing toy's head: the top of a jump's arc
        //    (feet 2.0 up, head 3.8) goes 1.2 into it; walking up to the edge never touches it.
        private static readonly string[] LetterShapes =
        {
            "Assets/_Game/Art/Title/Letters/Title_S.asset", "Assets/_Game/Art/Title/Letters/Title_H.asset", "Assets/_Game/Art/Title/Letters/Title_I.asset",
            "Assets/_Game/Art/Title/Letters/Title_F.asset", "Assets/_Game/Art/Title/Letters/Title_T.asset",
        };
        private const string TitleSheet = "Assets/_Game/Art/Title/ShiftJamTitlePixelRiftCandySprite.png";   // 64 frames of 75 x 35
        private const int TitleFrames = 64;
        private const string LetterLook = Data + "FlatLetter.mat", GhostLook = Data + "FlatLetterGhost.mat";
        private const string UiSoundsPath = "Assets/_Game/Resources/UiSounds.asset";
        private const string MovesPath = Data + "MovementSettings.asset", ShiftPath = Data + "ShiftSettings.asset";
        private const float LetterTall = 1.8f;            // as tall as the toy (the brief: 1.6 to 2 at toy size)
        private const float LetterClear = 0.2f;           // a letter a walk must not take has its bottom this far over the walking head
        private const float LetterBob = 0.15f;            // FlatLetter's bob: the letter's top plus this stays under the ceiling
        private const int SBook = 5;                      // S stands over this book's tread (Book5, the sixth)
        private const float BookTread = 2.4f, BookRiseMost = 1.2f;   // CourseBuilder.BookStairs: each book shows a 2.4 tread, risers at most 1.2
        private const float StairsFromX = -22f;           // CourseBuilder.House: the first book's back end (HallStairs)
        private const float GoodSwing = 30f;              // degrees: the swing on a rope at which the head reaches H, I, F
        private const float HeadOverHands = 0.38f;        // a toy on a rope: the capsule's top over the hands (RopeSettings, Min Hold)
        private const float TOver = 0.6f;                 // T's middle over the middle of a toy at the top of a jump
        private const float GapFromCheckpoint = 6f;       // CourseBuilder.House: CP_beforeSprint stands 6 before the gap, CP_afterSprint 6 after
        private const float FallbackJump = 2f, FallbackHang = 5f;   // the toy's jump and the ropes' Hang Hold as on 24 Nov, if the assets are not found
        private static readonly Color AllFoundColour = new Color32(0xFF, 0xD1, 0x66, 0xFF);   // the toasts' gold ("got it")

        // ---- the 3D route (D-0212): steps up the StepBox's east face and a plank over the sprint gap, beside the lane
        public const string RouteName = "FlatRoute_Hall";
        private const string PackPath = "Assets/_Imports/ithappy/Playroom";
        private const string PlayerPath = "Assets/_Game/Prefabs/Player/Player.prefab";
        private static readonly string[] StepPieces = { "Wooden_Bar_01", "Wooden_Bar_02" };   // a plain wooden block, sized to each step
        private static readonly string[] PlankPieces = { "Ruler_Small_01", "Ruler_01", "Ruler_Big_01", "Pencil_01" };   // the first one found is the plank
        private const float StepRiseShare = 0.6f;         // a step's rise is at most this share of the toy's full jump (the books' 1.2 of 2.0, D-0081)
        private const float StepTread = 1.6f;             // each step's top along the hallway: the toy is 0.6 round, a walking jump 4.4 long
        private const float StepsAcross = 2.6f;           // across the hallway, from the route's edge nearest the lane toward the near wall:
                                                          // the dressing against the near wall at x 28 reaches about z -156.3, the steps stop short of it
        private const float StepGap = 0.02f;              // the top step stands this far off the box's east face (no two faces in one place)
        private const float RouteCutClear = 0.3f;         // the route's edge nearest the lane stands this much past Cut From: wholly cut away side-on
        private const float PlankAcross = 3.2f;           // the plank's width (at least 3)
        private const float PlankBite = 1.5f;             // the plank lies at least this far onto the floor at each side of the gap
        private const float PlankWallGap = 0.6f;          // its far edge this far off the near wall's face: as far from the lane (and the exit rift's 9-wide trigger) as it goes
        private const float ToyRound = 0.3f;              // the toy capsule's radius (the Player prefab)
        private const float FallbackStepOffset = 0.3f;    // the toy capsule's step offset, if the Player prefab is not found

        // ---- the pixel look (D-0209)
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string PixelShaderPath = "Assets/_Game/Shaders/PixelShift.shader";
        private const string PixelMaterialPath = Data + "PixelShift.mat";
        private const string PalettePath = RiftArt + "PixelShiftPalette.png";
        public const string PixelFeatureName = "PixelShift";
        // His title sheet's 14 colours (read off the sheet: from its near-black ink to its white), then 7 warm ones so
        // the toy room's wood, cardboard and painted toys have somewhere to go. Written only when the file is missing:
        // repaint the PNG (one texel a colour, in a row; up to 32) and the look follows (the material's count with it:
        // run Add Pixel Look again).
        private static readonly Color32[] PaletteColours =
        {
            new Color32(0x0a, 0x08, 0x12, 0xff), new Color32(0x0e, 0x0d, 0x18, 0xff), new Color32(0x49, 0x23, 0x93, 0xff), new Color32(0x57, 0x34, 0x9c, 0xff),
            new Color32(0xb2, 0x4d, 0xff, 0xff), new Color32(0xff, 0x72, 0xcc, 0xff), new Color32(0xff, 0x83, 0xd2, 0xff), new Color32(0xff, 0x93, 0xd8, 0xff),
            new Color32(0xff, 0xa8, 0xe0, 0xff), new Color32(0x73, 0xf1, 0xff, 0xff), new Color32(0x94, 0xf4, 0xff, 0xff), new Color32(0xc3, 0xf9, 0xff, 0xff),
            new Color32(0xff, 0xf0, 0xfc, 0xff), new Color32(0xff, 0xff, 0xff, 0xff),
            // warm, for the toy room: dark wood, wood, cardboard, tan, cream, toy red, toy yellow
            new Color32(0x3a, 0x22, 0x20, 0xff), new Color32(0x74, 0x46, 0x2c, 0xff), new Color32(0xb9, 0x77, 0x3f, 0xff), new Color32(0xe6, 0xb3, 0x73, 0xff),
            new Color32(0xff, 0xe4, 0xb5, 0xff), new Color32(0xd9, 0x53, 0x3f, 0xff), new Color32(0xf5, 0xc8, 0x4c, 0xff),
        };

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

        [MenuItem("Shift Jam/Flat/Add Pixel Look (renderer feature)")]
        public static void AddPixelLookMenu()
        {
            string report = AddPixelLook();
            if (report.StartsWith(Tag + " NOT ")) Debug.LogWarning(report); else Debug.Log(report);
        }

        [MenuItem("Shift Jam/Flat/Rebuild Rift Gates (their look)")]
        public static void RebuildRiftsMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning($"{Tag} NOT rebuilt: this does not run in Play. Stop the game first."); return; }
            var did = new List<string>();
            GameObject portal = GatePiece(false, did);
            RiftInPiece(portal, true, did); RiftOutPiece(portal, true, did);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Rebuild Rift Gates: {string.Join("; ", did)}. Run Land Hallway Scroller to put them in the hallway.");
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
            GameObject riftIn = gatePiece != null ? RiftInPiece(gatePiece, false, assets) : null;
            GameObject riftOut = gatePiece != null ? RiftOutPiece(gatePiece, false, assets) : null;
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
            string keptAllFound = null; Color keptAllFoundColour = AllFoundColour; bool keptPixelLook = false, keptOptional = true;
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
                    SerializedProperty allFound = so.FindProperty("allFoundText");   // an older zone (before D-0208) has none
                    if (allFound != null) { keptAllFound = allFound.stringValue; keptAllFoundColour = so.FindProperty("allFoundColour").colorValue; }
                    SerializedProperty pixel = so.FindProperty("pixelLook");   // his tick, if he gave it (before D-0209 there is none)
                    if (pixel != null) keptPixelLook = pixel.boolValue;
                    SerializedProperty optional = so.FindProperty("optionalOnceDone");   // his choice, if he gave it (before D-0212 there is none)
                    if (optional != null) keptOptional = optional.boolValue;
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
            WorkshopPieces.Set(zone, "optionalOnceDone", keptOptional);
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
            if (keptAllFound != null) { WorkshopPieces.Set(zone, "allFoundText", keptAllFound); WorkshopPieces.Set(zone, "allFoundColour", keptAllFoundColour); }
            else WorkshopPieces.Set(zone, "allFoundColour", AllFoundColour);
            // The pixel look (D-0209): the renderer's feature handed to the zone when it is there; OFF for the hallway
            // (he liked it as it is) unless an older FlatZone_Hall had it ticked.
            FullScreenPassRendererFeature pixelFeature = FindPixelFeature(out string pixelWhere);
            WorkshopPieces.Set(zone, "pixelFeature", pixelFeature);
            WorkshopPieces.Set(zone, "pixelLook", keptPixelLook);
            if (pixelFeature == null) warn.Add($"no {PixelFeatureName} feature on {RendererPath}: the zone has no pixel look (run Shift Jam > Flat > Add Pixel Look, then land again)");

            // The gates. The way in is met in 3D, so it stands across the lane facing the toy that comes to it (its forward,
            // the blue arrow, points east, the way the toy goes through). The way out is met side-on, so it faces the side
            // camera (its forward points to the camera's side, south): across the lane the side camera would see it edge-on,
            // a thin line, all the way to it. Its trigger is then turned too: thin along the lane, so the toy is let go as
            // it reaches the portal's middle, and wide across it.
            var sounds = AssetDatabase.LoadAssetAtPath<CourseSounds>(SoundsPath);
            // The way in is the pixel rift and the way out the 3D rift when they are made (D-0207), else his portal.
            GameObject entrance = Gate(riftIn != null ? riftIn : gatePiece, zoneGo.transform, "Entrance", new Vector3(GateX, 0f, laneZ), Vector3.right, FlatGate.Role.Entrance, entranceColour, sounds != null ? sounds.grow : default);
            GameObject exit = Gate(riftOut != null ? riftOut : gatePiece, zoneGo.transform, "Exit", new Vector3(exitX, 0f, laneZ), Vector3.back, FlatGate.Role.Exit, exitColour, sounds != null ? sounds.shrink : default);
            if (exit.TryGetComponent(out BoxCollider exitTouch))
            {
                Vector3 size = exitTouch.size;   // made as (across, up, along) for a gate across the lane: turned to face the camera, across and along swap
                exitTouch.size = new Vector3(ExitTouchAlong, size.y, size.x);
            }
            // The link back (D-0212): the way out carries a player it does not hold, walking into it westward, to the way in.
            FlatGate entranceGate = entrance.GetComponent<FlatGate>(), exitGate = exit.GetComponent<FlatGate>();
            if (exitGate != null) WorkshopPieces.Set(exitGate, "link", entranceGate);
            // The letters (D-0208), under the zone; the reward's sound on the zone.
            var letterLines = new List<string>();
            int letterCount = PlaceLetters(zoneGo.transform, scene, hall, laneZ, ceilingY, beforeX, afterX, letterLines, warn);
            if (sounds != null)
            {
                var zs = new SerializedObject(zone);
                zs.FindProperty("allFoundSound.clip").objectReferenceValue = sounds.coreAwake.clip;
                zs.FindProperty("allFoundSound.volume").floatValue = sounds.coreAwake.clip != null && sounds.coreAwake.volume > 0f ? sounds.coreAwake.volume : 0.5f;
                zs.FindProperty("allFoundSound.pitchSpread").floatValue = 0f;
                zs.ApplyModifiedPropertiesWithoutUndo();
            }

            // The 3D route (D-0212): its own top-level object, made again on every run.
            int routesRemoved = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == RouteName) { Undo.DestroyObjectImmediate(root); routesRemoved++; }
            var routeLines = new List<string>();
            GameObject route = PlaceRoute(scene, hall, laneZ, southFace, ceilingY, beforeX, afterX, exit, wallS != null ? wallS.sharedMaterial : null, routeLines, warn);
            Undo.RegisterCreatedObjectUndo(route, UndoName);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = zoneGo;

            // ---- 5. the report: every number, and what the game's own code answers for them
            var texts = new SerializedObject(zone);
            report.AppendLine($"PLACED: {ZoneName} at the top level of Greybox, at {Say(lanePoint)}, not turned, scale 1"
                + (removed > 0 ? $" ({removed} older {ZoneName} taken out first; its card texts and colours and its gates' colours were carried over, every number is the builder's again)" : " (there was none before)")
                + $". Big cards: on the way in \"{texts.FindProperty("card").stringValue}\" ({Say(texts.FindProperty("cardColour").colorValue)}), on the way out \"{texts.FindProperty("clearText").stringValue}\" ({Say(texts.FindProperty("clearColour").colorValue)}).");
            report.AppendLine($"BOX (world): x {Say(boxWest)} to {Say(boxEast)}, y {Say(boxBottom)} to {Say(boxTopY)}, z {Say(boxSouth)} to {Say(boxNorth)} (size {Say(boxSize)}, middle {Say(boxMiddle)}). "
                + $"It starts {Say(GateClear)} past the entrance gate (x {Say(GateX)}; cut A started at -24 itself), so a held toy is always clear of the gate the side-on camera sees edge-on. "
                + $"It ends AT the exit gate: going out lets the player go at once, and only a player the zone holds can go out; walking back in from the east is never taken (D-0212): through the exit rift it carries the toy back to the entrance, beside it the stretch stays 3D. "
                + $"Its bottom is {Say(BoxUnderPit)} under the sprint gap's floor (the plan said y -2: the zone let go half-way down a fall).");
            report.AppendLine($"LANE: point {Say(lanePoint)}, the camera's side -Z (child 'Lane', its blue arrow south); along the lane = +X. RAIL: the camera's middle travels x {Say(RailWest)} to {Say(RailEast)} (Rail Start {Say(RailWest - LaneX)}, Rail End {Say(RailEast - LaneX)}, counted from the lane's point; cut A's west end was -14). "
                + $"FRAME: never under y {Say(frameFloor)} or over y {Say(frameCeiling)} (cut A: over {Say(ceilingY)}, the ceiling's underside: the toy's head on the screen's top edge on the step box).");
            report.AppendLine("GATES (under " + ZoneName + "; the way in is " + PieceName(entrance) + ", the way out " + PieceName(exit) + "): " + GateLine(entrance) + " | " + GateLine(exit) + ".");
            report.AppendLine($"FILL LIGHT: 'Fill' under {ZoneName}, a spot with no shadows, off until a player is held; then it rides with that player's camera, fading in and out with the swing (FlatView > Fill Light {Say(view.fillLight)} at the lane, 0 = off).");
            var zoneSo = new SerializedObject(zone);
            UnityEngine.Object rewardClip = zoneSo.FindProperty("allFoundSound.clip").objectReferenceValue;
            report.AppendLine($"LETTERS (D-0208): {letterCount} placed under {ZoneName} on the lane (z {Say(laneZ)}). " + string.Join(" | ", letterLines)
                + $". ALL FOUND (once per save slot): {zoneSo.FindProperty("allFoundCharges").intValue} shift charge, the big card \"{zoneSo.FindProperty("allFoundText").stringValue}\" ({Say(zoneSo.FindProperty("allFoundColour").colorValue)}), sound {(rewardClip != null ? rewardClip.name : "silent")}. "
                + $"THE STRIP: top left, shown while the zone holds the player and {Say(view.stripSeconds)} s after a letter is found (FlatView > Strip Seconds). "
                + "SEEN only in the flat run (D-0212): each letter is drawn while the zone holds a local player and while it pops (FlatLetter > Only While Held), so the stretch walked in 3D shows none.");
            report.AppendLine($"PIXEL LOOK (D-0209): {(zoneSo.FindProperty("pixelLook").boolValue ? "ON (kept: an older FlatZone_Hall had it ticked)" : "OFF (tick Pixel Look on FlatZone_Hall to see it)")}; feature: {pixelWhere}; "
                + $"FlatView > Pixel Rows {view.pixelRows} ({PixelColumns(view.pixelRows, 16f / 9f)} columns on a 16:9 screen, {PixelColumns(view.pixelRows, 21f / 9f)} on 21:9; a big pixel is {Say(1080f / Mathf.Max(1, view.pixelRows))} screen pixels at 1080 high), Palette Strength {Say(view.paletteStrength)}, Dither {Say(view.dither)}.");

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
            report.AppendLine($"3D ROUTE (D-0212): {RouteName} at the top level{(routesRemoved > 0 ? $" ({routesRemoved} older one taken out first)" : "")}. " + string.Join(" | ", routeLines) + ". " + RouteCut(route, cut, warn) + ".");
            report.AppendLine("THE BOOK STAIRS GOING DOWN WEST: " + StairsDown(hall, ceilingY, warn) + ".");
            report.AppendLine("THE FLOOR UNDER THE ROPES: " + FloorUnderRopes(hall, laneZ, beforeX, route, zoneGo, warn) + ".");
            report.AppendLine("WHO THE ZONE TAKES (D-0212; FlatZone.WouldTake, the game's own rule, asked out of Play): " + TakeRule(zone, boxWest, boxEast, boxBottom, boxTopY, laneZ, entrance) + ".");
            report.AppendLine("THE LINK BACK (D-0212): " + LinkLine(zone, entrance, exit, warn) + ".");
            if (sounds == null) warn.Add($"no {SoundsPath}: the gates are silent");
            if (warn.Count > 0) report.AppendLine("LOOK AT: " + string.Join("; ", warn) + ".");
            report.Append("SAVING: the scene is marked changed and NOT saved (as Place Hallway Ropes does): save Greybox (Ctrl+S) to keep the zone, or close without saving to drop it. "
                + "OFF SWITCHES: untick FlatZone_Hall in the Hierarchy (the hallway plays as before, no gates); FlatView > Fill Light 0 (no fill light); a gate's object unticked (that gate gone; with the Exit gone nobody is let go but by walking out); "
                + "a letter's object unticked (that letter gone; the reward counts the letters that are on; all five unticked = no strip, the zone as before D-0208); "
                + "D-0212: FlatZone_Hall > Optional Once Done off (the box takes anyone walking in at the west face, done or not), the Exit's Link emptied (no carry back), a letter's Only While Held off (drawn all the time), "
                + RouteName + " unticked (no steps, no plank: the stretch is flat-only again going west).");
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

        // ================================================================== the 3D route (D-0212)

        /// <summary>The eastmost solid end sticking out of the box's east face inside the steps' band (from the lane's side,
        /// <paramref name="north"/>, to <paramref name="south"/>) that a toy can walk from onto the box's top: its top at most one
        /// step under the box's and not more than a step offset over it. The stair books run through the box and their ends
        /// stick out (D-0212, measured 25 Nov). False when nothing sticks out: the steps meet the box's face.</summary>
        private static bool FindOverhangLanding(Bounds box, float south, float north, float boxTop, float riseMost, out float east, out float top, out string name)
        {
            east = box.max.x; top = boxTop; name = null;
            var centre = new Vector3(box.max.x + 6f, boxTop - riseMost * 0.5f, (south + north) * 0.5f);
            var half = new Vector3(6.05f, riseMost * 0.5f + 0.3f, (north - south) * 0.5f);
            foreach (Collider c in Physics.OverlapBox(centre, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.root.name == RouteName) continue;   // an older route's own steps
                Bounds b = c.bounds;
                if (b.max.x <= box.max.x + 0.05f || b.max.y > boxTop + 0.3f || b.max.y < boxTop - riseMost - 0.01f) continue;
                if (b.max.x > east) { east = b.max.x; top = b.max.y; name = c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name; }
            }
            return name != null;
        }

        /// <summary>FlatRoute_Hall at the top level of <paramref name="scene"/>: the steps up the StepBox's east face (onto
        /// the stair books' ends where they stick out of it) and the plank over the sprint gap, both in the band between the
        /// lane and the side camera that the cutaway hides (past Cut From, short of the near wall). One line per piece into
        /// <paramref name="lines"/>; anything that does not add up into <paramref name="warn"/>.</summary>
        private static GameObject PlaceRoute(Scene scene, Transform hall, float laneZ, float southFace, float ceilingY, float beforeX, float afterX, GameObject exit, Material fallbackLook, List<string> lines, List<string> warn)
        {
            var root = new GameObject(RouteName);
            if (root.scene != scene) SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            float jump = ToyJump(out string jumpFrom);
            float stepOffset = ToyStepOffset(out string offsetFrom);
            float north = laneZ - CutFrom - RouteCutClear;   // the route's edge nearest the lane
            Physics.SyncTransforms();

            // ---- the steps
            Transform stepBox = hall != null ? hall.Find("StepBox") : null;
            Bounds box;
            string boxFrom;
            if (stepBox != null && DrawnBounds(stepBox.gameObject, out box)) boxFrom = "StepBox found";
            else { box = new Bounds(new Vector3(10.16f, StepBoxTop * 0.5f, laneZ), new Vector3(11.525f, StepBoxTop, 15.08f)); boxFrom = "StepBox NOT FOUND: the measured box (x 4.4 to 15.925, z 7.54 either side of the lane)"; warn.Add("no StepBox: the steps stand where the measured box was"); }
            float floorY = FloorAt(new Vector3(box.max.x + 4f, ceilingY - 0.5f, north - StepsAcross * 0.5f), ceilingY + 2f, out string floorFrom);
            float boxTop = box.max.y, boxEast = box.max.x, south = north - StepsAcross;
            float riseMost = Mathf.Min(BookRiseMost, jump * StepRiseShare);
            // The stair books (UpStairs) run through the box and stick out of its east face (measured 25 Nov: Book9 to Book11,
            // tops 11.07 to 12.14, ends out to x 21.38, across z -156.7 to -144.1): the steps climb onto the eastmost such end,
            // so none stands inside a book or under one's overhang. From there the toy walks west onto the box's top.
            string landing = "the box's top";
            if (FindOverhangLanding(box, south, north, boxTop, riseMost, out float landEast, out float landTop, out string landName))
            {
                landing = $"{landName}'s end sticking out of the box's east face (top y {Say(landTop)}, out to x {Say(landEast)}), then the box's top";
                boxEast = landEast; boxTop = landTop;
            }
            int risers = Mathf.Max(1, Mathf.CeilToInt((boxTop - floorY) / riseMost));
            float rise = (boxTop - floorY) / risers;
            GameObject stepPrefab = PackPrefab(StepPieces, out string stepName);
            lines.Add($"STEPS up the StepBox's east face ({boxFrom}: x {Say(box.min.x)} to {Say(box.max.x)}, top y {Say(box.max.y)}, z {Say(box.min.z)} to {Say(box.max.z)}; the floor there y {Say(floorY)}, {floorFrom}): "
                + $"{risers - 1} steps and {landing}, each rise {Say(rise)} (at most {Say(StepRiseShare)} of the toy's jump {Say(jump)}, {jumpFrom}), each tread {Say(StepTread)} along, {Say(StepsAcross)} across (z {Say(south)} to {Say(north)}), "
                + $"{(stepPrefab != null ? stepName + " from the Playroom pack, sized to each step" : "plain blocks in the hallway's own material (no " + string.Join(" or ", StepPieces) + " in " + PackPath + ")")}");
            if (box.min.z > north - 1f) warn.Add($"the StepBox reaches only z {Say(box.min.z)}: the top step meets it over {Say(north - box.min.z)} across (under 1: a toy may not step from the steps onto the box)");
            for (int k = risers - 1; k >= 1; k--)
            {
                float top = floorY + k * rise;
                float x0 = boxEast + StepGap + (risers - 1 - k) * StepTread;
                var want = new Bounds(new Vector3(x0 + StepTread * 0.5f, (floorY + top) * 0.5f, (south + north) * 0.5f), new Vector3(StepTread, top - floorY, StepsAcross));
                GameObject step = FitPiece(stepPrefab, "Step_" + k, root.transform, want, false, fallbackLook);
                float head = top + ToyTall;
                lines.Add($"Step_{k}: x {Say(want.min.x)} to {Say(want.max.x)}, top y {Say(top)} ({Say(rise)} over the {(k == 1 ? "floor" : "step before")}; a full jump lifts the feet {Say(jump)}), head room {Say(ceilingY - head)} under the ceiling; {Touches(step, root.transform)}");
                if (head > ceilingY) warn.Add($"Step_{k}: a toy on it (head {Say(head)}) does not fit under the ceiling (y {Say(ceilingY)})");
            }
            if (rise > jump) warn.Add($"a step's rise {Say(rise)} is over the toy's jump {Say(jump)}");

            // ---- the plank over the sprint gap
            FindGap(beforeX, afterX, (southFace + north) * 0.5f, floorY, ceilingY, out float gapWest, out float gapEast, out string gapFrom);
            GameObject plankPrefab = PackPrefab(PlankPieces, out string plankName);
            if (plankPrefab == null) { warn.Add($"no {string.Join(", ", PlankPieces)} in {PackPath}: NO PLANK over the sprint gap (the way west needs a sprint jump)"); return root; }
            float zMin = southFace + PlankWallGap;
            var plankWant = new Bounds(new Vector3((gapWest + gapEast) * 0.5f, floorY, zMin + PlankAcross * 0.5f), new Vector3(gapEast - gapWest + 2f * PlankBite, 0f, PlankAcross));
            GameObject plank = FitPiece(plankPrefab, "Plank", root.transform, plankWant, true, fallbackLook);
            DrawnBounds(plank, out Bounds pb);
            float thick = pb.max.y - floorY;
            string exitClear = "";
            if (exit != null && TriggerBounds(exit, out Bounds et))
            {
                exitClear = $"; the exit rift's trigger (x {Say(et.min.x)} to {Say(et.max.x)}, z {Say(et.min.z)} to {Say(et.max.z)}) is {Say(et.min.x - pb.max.x)} past its east end and {Say(et.min.z - pb.max.z)} past its edge nearest the lane";
                if (pb.max.x > et.min.x - ToyRound && pb.max.z > et.min.z) warn.Add("the plank's east end reaches the exit rift's trigger: a toy walking off it east steps into the rift");
            }
            lines.Add($"PLANK: {plankName} from the Playroom pack at {Say(plank.transform.localScale.x)} times, x {Say(pb.min.x)} to {Say(pb.max.x)} ({Say(pb.size.x)} long), z {Say(pb.min.z)} to {Say(pb.max.z)} ({Say(pb.size.z)} across), top y {Say(pb.max.y)}; "
                + $"over the sprint gap x {Say(gapWest)} to {Say(gapEast)} ({gapFrom}), lying {Say(gapWest - pb.min.x)} on the floor west of it and {Say(pb.max.x - gapEast)} east; "
                + $"its top is {Say(thick)} over the floor: {(thick <= stepOffset ? "walked onto (the toy's step offset is " + Say(stepOffset) + ", " + offsetFrom + ")" : "OVER the toy's step offset " + Say(stepOffset) + ": a hop up")}{exitClear}; {Touches(plank, root.transform)}");
            if (pb.size.z < 3f) warn.Add($"the plank is only {Say(pb.size.z)} across (under 3)");
            if (pb.min.x > gapWest - 0.5f || pb.max.x < gapEast + 0.5f) warn.Add("the plank does not reach the floor on both sides of the sprint gap");
            if (pb.max.z > north + 0.001f) warn.Add($"the plank reaches z {Say(pb.max.z)}, nearer the lane than the cut band's edge {Say(north)}: the side view may see it");
            return root;
        }

        /// <summary>One route piece: an empty <paramref name="name"/> (not turned) under <paramref name="parent"/> with the
        /// pack's piece in it (a plain cube in <paramref name="fallbackLook"/> when there is none). Not <paramref name="uniform"/>:
        /// the holder is scaled per axis so the drawn piece fills <paramref name="want"/> (a block); uniform: the piece is
        /// laid with its long side along x and scaled the same every way until it is at least as long and as wide as
        /// <paramref name="want"/> (a plank: never stretched), its middle on want's middle along x and across, its bottom
        /// on want's bottom.</summary>
        private static GameObject FitPiece(GameObject prefab, string name, Transform parent, Bounds want, bool uniform, Material fallbackLook)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            GameObject look;
            if (prefab != null) look = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            else
            {
                look = GameObject.CreatePrimitive(PrimitiveType.Cube);
                look.transform.SetParent(holder, false);
                if (fallbackLook != null) look.GetComponent<Renderer>().sharedMaterial = fallbackLook;
            }
            look.transform.localPosition = Vector3.zero;
            if (!DrawnBounds(look, out Bounds b)) return holder.gameObject;
            if (uniform)
            {
                if (b.size.z > b.size.x) { look.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * look.transform.localRotation; DrawnBounds(look, out b); }
                float k = Mathf.Max(want.size.z / Mathf.Max(0.001f, b.size.z), want.size.x / Mathf.Max(0.001f, b.size.x));
                holder.localScale = Vector3.one * k;
                DrawnBounds(look, out b);
                holder.position += new Vector3(want.center.x - b.center.x, want.min.y - b.min.y, want.center.z - b.center.z);
                return holder.gameObject;
            }
            holder.localScale = new Vector3(want.size.x / Mathf.Max(0.001f, b.size.x), want.size.y / Mathf.Max(0.001f, b.size.y), want.size.z / Mathf.Max(0.001f, b.size.z));
            DrawnBounds(look, out b);
            holder.position += want.center - b.center;
            return holder.gameObject;
        }

        /// <summary>The first of <paramref name="names"/> found as a prefab in the Playroom pack (by its file's name), or null.</summary>
        private static GameObject PackPrefab(string[] names, out string found)
        {
            foreach (string n in names)
                foreach (string guid in AssetDatabase.FindAssets(n + " t:Prefab", new[] { PackPath }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) != n) continue;
                    found = n;
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            found = null;
            return null;
        }

        /// <summary>What a route piece's box (shrunk 5 cm every way, so the floor and the box it stands against do not
        /// count) overlaps that is solid and not the route's own: "clear" is what is wanted.</summary>
        private static string Touches(GameObject piece, Transform own)
        {
            if (!DrawnBounds(piece, out Bounds b)) return "nothing drawn";
            Physics.SyncTransforms();
            Vector3 half = Vector3.Max(b.extents - Vector3.one * 0.05f, Vector3.one * 0.01f);
            var names = new List<string>();
            foreach (Collider c in Physics.OverlapBox(b.center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == null || c.transform.IsChildOf(own)) continue;
                string n = c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name;
                if (!names.Contains(n)) names.Add(n);
            }
            return names.Count == 0 ? "clear of everything solid" : "IT OVERLAPS " + string.Join(", ", names) + " (move that, or this)";
        }

        /// <summary>The floor's top straight down from <paramref name="from"/>: the highest solid top (not a trigger) within
        /// 0.5 of y 0, so a lost thing lying there does not count; 0 when there is none (then <paramref name="said"/> says so).</summary>
        private static float FloorAt(Vector3 from, float reach, out string said)
        {
            Physics.SyncTransforms();
            if (FloorThere(from, reach, 0f, 0.5f, out RaycastHit hit)) { said = $"{hit.collider.name} found under it"; return hit.point.y; }
            said = "NO floor found under it at y 0: y 0 used";
            return 0f;
        }

        /// <summary>True when a ray straight down from <paramref name="from"/> meets a solid top (not a trigger) within
        /// <paramref name="tolerance"/> of <paramref name="near"/>: a floor at that height, whatever lies on it.</summary>
        private static bool FloorThere(Vector3 from, float reach, float near, float tolerance, out RaycastHit floor)
        {
            floor = default;
            bool any = false;
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Mathf.Abs(hit.point.y - near) >= tolerance || (any && hit.point.y <= floor.point.y)) continue;
                floor = hit; any = true;
            }
            return any;
        }

        /// <summary>The sprint gap along the line z = <paramref name="z"/>: the floor (a solid top within 0.25 of
        /// <paramref name="floorY"/>) looked for every 0.25 from <paramref name="fromX"/> to <paramref name="toX"/>; the gap
        /// runs from the first miss to the last. The checkpoints' numbers (6 either side) when no miss is found.</summary>
        private static void FindGap(float fromX, float toX, float z, float floorY, float ceilingY, out float west, out float east, out string said)
        {
            Physics.SyncTransforms();
            west = float.NaN; east = float.NaN;
            for (float x = fromX; x <= toX; x += 0.25f)
            {
                if (FloorThere(new Vector3(x, ceilingY - 0.5f, z), ceilingY + 2f, floorY, 0.25f, out _)) continue;
                if (float.IsNaN(west)) west = x - 0.25f;
                east = x;
            }
            if (float.IsNaN(west)) { west = fromX + GapFromCheckpoint; east = toX - GapFromCheckpoint; said = "NO gap found in the floor: the checkpoints' numbers"; return; }
            east += 0.25f;
            said = $"measured: no floor from x {Say(west + 0.25f)} to {Say(east - 0.25f)} every 0.25";
        }

        /// <summary>The toy's full jump at toy size (MovementSettings Jump Height times ShiftSettings Toy Scale).</summary>
        private static float ToyJump(out string from)
        {
            var moves = AssetDatabase.LoadAssetAtPath<MovementSettings>(MovesPath);
            var shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftPath);
            float toy = shift != null ? shift.toyScale : 1f;
            if (moves == null) { from = "NOT FOUND, the 24 Nov number"; return FallbackJump; }
            from = MovesPath + " Jump Height " + Say(moves.jumpHeight) + " x toy scale " + Say(toy);
            return moves.jumpHeight * toy;
        }

        /// <summary>The toy capsule's step offset (the Player prefab's CharacterController): a lip this high is walked onto.</summary>
        private static float ToyStepOffset(out string from)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (player != null && player.TryGetComponent(out CharacterController capsule)) { from = PlayerPath; return capsule.stepOffset; }
            from = "the Player prefab NOT FOUND, the 25 Nov number";
            return FallbackStepOffset;
        }

        /// <summary>A gate's trigger box in the world (its BoxCollider's own box: right in the editor, where the
        /// collider's bounds may not be up to date). False when it has none.</summary>
        private static bool TriggerBounds(GameObject gate, out Bounds bounds)
        {
            bounds = default;
            if (gate == null || !gate.TryGetComponent(out BoxCollider box)) return false;
            Vector3 lo = gate.transform.TransformPoint(box.center - box.size * 0.5f), hi = gate.transform.TransformPoint(box.center + box.size * 0.5f);
            bounds.SetMinMax(Vector3.Min(lo, hi), Vector3.Max(lo, hi));
            return true;
        }

        /// <summary>Whether every renderer of the route is in the cutaway's list, by piece (the route's own children: Step_1
        /// and on, Plank): the CUT AWAY line above has them too, by their renderers' names.</summary>
        private static string RouteCut(GameObject route, List<Renderer> cut, List<string> warn)
        {
            if (route == null) return "NO route";
            var inCut = new List<string>(); var missing = new List<string>();
            int renderers = 0;
            foreach (Transform piece in route.transform)
            {
                bool all = true, some = false;
                foreach (Renderer r in piece.GetComponentsInChildren<Renderer>()) { renderers++; some = true; if (!cut.Contains(r)) all = false; }
                if (!some) continue;
                (all ? inCut : missing).Add(piece.name);
            }
            if (missing.Count > 0) warn.Add($"route pieces NOT cut away side-on (they would stand in front of the flat run): {string.Join(", ", missing)}");
            return $"CUT AWAY side-on (in the list above, {renderers} renderer(s)): {(inCut.Count > 0 ? string.Join(", ", inCut) : "none")}"
                + (missing.Count > 0 ? $"; NOT CUT: {string.Join(", ", missing)}" : "; every route piece is cut (not drawn in the flat run, its shadow kept)");
        }

        /// <summary>The book stairs (UpStairs) as a toy walking DOWN west from the box's top meets them, east to west: each
        /// book's top and the stretch of it to land on (from its own west end to the west end of the one before: the
        /// books lie one over the next), the drop onto it, and the head room. Books whose west end is under the box's top
        /// (the landing books) are not met. A landing under the toy's width, or a toy that does not fit under the ceiling,
        /// goes to the warnings.</summary>
        private static string StairsDown(Transform hall, float ceilingY, List<string> warn)
        {
            Transform stairs = hall != null ? hall.Find("UpStairs") : null;
            if (stairs == null) { warn.Add("no UpStairs: the way down west is not checked"); return "UpStairs NOT FOUND"; }
            Transform stepBox = hall.Find("StepBox");
            float above, aboveWest; string start;
            if (stepBox != null && DrawnBounds(stepBox.gameObject, out Bounds sb)) { above = sb.max.y; aboveWest = sb.min.x; start = $"from the StepBox's top (y {Say(above)}, its west face x {Say(aboveWest)})"; }
            else { above = StepBoxTop; aboveWest = float.PositiveInfinity; start = $"from the measured box top (y {Say(above)}; StepBox NOT FOUND)"; }
            var books = new List<(string name, Bounds b)>();
            foreach (Transform book in stairs) if (DrawnBounds(book.gameObject, out Bounds bb)) books.Add((book.name, bb));
            if (books.Count == 0) { warn.Add("UpStairs has no books"); return "UpStairs has no books"; }
            books.Sort((a, c) => c.b.min.x.CompareTo(a.b.min.x));   // east first: the way down west
            var parts = new List<string>(); var under = new List<string>();
            float worstDrop = 0f, narrowest = float.MaxValue;
            foreach (var (name, b) in books)
            {
                if (b.min.x >= aboveWest - 0.01f) { under.Add($"{name} (top {Say(b.max.y)})"); continue; }   // under the box's top or the book before: not met
                float tread = float.IsInfinity(aboveWest) ? b.size.x : aboveWest - b.min.x;
                float drop = above - b.max.y;
                worstDrop = Mathf.Max(worstDrop, drop); narrowest = Mathf.Min(narrowest, tread);
                parts.Add($"{name} top {Say(b.max.y)} (land on x {Say(b.min.x)} to {Say(b.min.x + tread)}, {Say(tread)} long; drop {Say(drop)})");
                if (tread < ToyRound * 2f) warn.Add($"UpStairs/{name}: only {Say(tread)} of it shows to land on going down (under the toy's {Say(ToyRound * 2f)})");
                if (drop < -0.3f) warn.Add($"UpStairs/{name}: going down west it is {Say(-drop)} UP from the one before (over the step offset)");
                if (b.max.y + ToyTall > ceilingY) warn.Add($"UpStairs/{name}: a toy on it does not fit under the ceiling");
                above = b.max.y; aboveWest = b.min.x;
            }
            return $"{start}, east to west: " + string.Join(", ", parts) + $"; then the floor ({Say(above)} down)"
                + (under.Count > 0 ? $"; not met (their west end under the box's top): {string.Join(", ", under)}" : "")
                + $". Worst drop {Say(worstDrop)} (a drop is walked off), narrowest landing {Say(narrowest)} (the toy is {Say(ToyRound * 2f)} round): "
                + (narrowest >= ToyRound * 2f ? "every book can be stepped down onto" : "SEE LOOK AT");
        }

        /// <summary>The floor along the lane under the ropes, from the StepBox's east face to CP_beforeSprint: its top
        /// every 0.5 (gaps named), and a toy-sized capsule standing every 1 along it: what solid thing it touches.</summary>
        private static string FloorUnderRopes(Transform hall, float laneZ, float beforeX, GameObject route, GameObject zone, List<string> warn)
        {
            Transform stepBox = hall != null ? hall.Find("StepBox") : null;
            float from = stepBox != null && DrawnBounds(stepBox.gameObject, out Bounds sb) ? sb.max.x + 0.5f : 16.5f;
            Physics.SyncTransforms();
            float low = float.MaxValue, high = float.MinValue; var holes = new List<string>(); var touched = new List<string>();
            for (int i = 0; from + i * 0.5f <= beforeX; i++)
            {
                float x = from + i * 0.5f;
                if (FloorThere(new Vector3(x, 3f, laneZ), 6f, 0f, 0.5f, out RaycastHit hit)) { low = Mathf.Min(low, hit.point.y); high = Mathf.Max(high, hit.point.y); }
                else if (holes.Count < 8) holes.Add(Say(x));
                if (i % 2 != 0) continue;
                Vector3 feet = new Vector3(x, (low < float.MaxValue ? high : 0f) + 0.05f, laneZ);
                foreach (Collider c in Physics.OverlapCapsule(feet + Vector3.up * (ToyRound + 0.02f), feet + Vector3.up * (ToyTall - ToyRound), ToyRound, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (c == null || (route != null && c.transform.IsChildOf(route.transform)) || c.transform.IsChildOf(zone.transform)) continue;
                    string n = (c.transform.parent != null ? c.transform.parent.name + "/" : "") + c.name + " at x " + Say(x);
                    if (touched.Count < 8) touched.Add(n);
                }
            }
            if (holes.Count > 0) warn.Add($"the lane under the ropes has no floor at x {string.Join(", ", holes)}");
            if (touched.Count > 0) warn.Add($"a toy standing on the lane under the ropes touches {string.Join(", ", touched)}");
            return $"x {Say(from)} to {Say(beforeX)} on the lane (z {Say(laneZ)}): the floor's top y {(low < float.MaxValue ? Say(low) + " to " + Say(high) : "NOT FOUND")} every 0.5{(holes.Count > 0 ? "; NO FLOOR at x " + string.Join(", ", holes) : ", no hole")}; "
                + $"a toy standing there every 1 touches {(touched.Count == 0 ? "nothing solid: walkable both ways" : string.Join(", ", touched))}";
        }

        /// <summary>The take rule's answers for a toy's middle (0.9 over the floor) stepping 0.4 across each side of the box,
        /// before and after done, with and without coming through the rift; the slots' done marks as they stand.</summary>
        private static string TakeRule(FlatZone zone, float boxWest, float boxEast, float boxBottom, float boxTop, float laneZ, GameObject entrance)
        {
            const float Mid = ToyTall * 0.5f, Step = 0.2f;
            string Ask(Vector3 a, Vector3 b, bool done, bool invited) => zone.WouldTake(a, b, done, invited) ? "TAKES" : "no";
            Vector3 westOut = new Vector3(boxWest - Step, Mid, laneZ), westIn = new Vector3(boxWest + Step, Mid, laneZ);
            float besideZ = laneZ - 7f;
            if (TriggerBounds(entrance, out Bounds et)) besideZ = et.min.z - 2f;
            Vector3 besideOut = new Vector3(boxWest - Step, Mid, besideZ), besideIn = new Vector3(boxWest + Step, Mid, besideZ);
            Vector3 eastOut = new Vector3(boxEast + Step, Mid, laneZ), eastIn = new Vector3(boxEast - Step, Mid, laneZ);
            Vector3 topOut = new Vector3(30f, boxTop + Step, laneZ), topIn = new Vector3(30f, boxTop - Step, laneZ);
            Vector3 bottomOut = new Vector3(70f, boxBottom - Step, laneZ), bottomIn = new Vector3(70f, boxBottom + Step, laneZ);
            Vector3 far = new Vector3(boxWest - 12f, Mid, laneZ);
            var slots = new List<string>();
            for (int i = 0; i < SaveGame.Slots; i++) slots.Add($"slot {i + 1} {(zone.DoneFor(i) ? "DONE" : "not done")}");
            return $"through the rift's middle (x {Say(boxWest)}, z {Say(laneZ)}) walking east: before done {Ask(westOut, westIn, false, false)}, done {Ask(westOut, westIn, true, false)}, done and through the rift {Ask(westOut, westIn, true, true)} | "
                + $"beside the rift (z {Say(besideZ)}) walking east: before done {Ask(besideOut, besideIn, false, false)}, done {Ask(besideOut, besideIn, true, false)} | "
                + $"in at the east end (x {Say(boxEast)}) walking west: before done {Ask(eastOut, eastIn, false, false)}, done {Ask(eastOut, eastIn, true, true)} | "
                + $"in through the top (y {Say(boxTop)}): {Ask(topOut, topIn, false, true)} | in through the bottom (y {Say(boxBottom)}): {Ask(bottomOut, bottomIn, false, true)} | "
                + $"put inside from 12 west (a respawn, a portal): {Ask(far, westIn, false, true)}. "
                + $"Optional Once Done is {(zone.OptionalOnceDone ? "ON" : "OFF (done changes nothing)")}. Done now: {string.Join(", ", slots)} (\"{FlatZone.DoneKeyOf(zone.Id)}\" in the slot's seen list; New Game clears it)";
        }

        /// <summary>Where the exit's link back lands, and what is around that spot: the entrance's trigger, the floor, the
        /// drop ahead.</summary>
        private static string LinkLine(FlatZone zone, GameObject entrance, GameObject exit, List<string> warn)
        {
            FlatGate inGate = entrance != null ? entrance.GetComponent<FlatGate>() : null, outGate = exit != null ? exit.GetComponent<FlatGate>() : null;
            if (inGate == null || outGate == null) { warn.Add("a gate has no FlatGate script: no link back"); return "NO link (a gate has no FlatGate script)"; }
            zone.LandingFor(inGate, out Vector3 at, out Quaternion facing);
            FlatViewSettings v = zone.Settings;
            Vector3 back = facing * Vector3.forward;
            string clear = "the entrance has no trigger";
            if (TriggerBounds(entrance, out Bounds t))
            {
                // the trigger's face nearest the landing, along the way the player faces there
                float gap = Mathf.Abs(Vector3.Dot(at - t.ClosestPoint(at), back)) - ToyRound;
                clear = $"the body's edge {Say(gap)} clear of the entrance's trigger (x {Say(t.min.x)} to {Say(t.max.x)})";
                if (t.Contains(at) || gap <= 0f) warn.Add("the link back lands the toy inside the entrance's trigger: raise FlatView > Link Landing");
            }
            float floorY = FloorAt(at + Vector3.up * 3f, 6f, out string floorFrom);
            float ahead = float.NaN;
            for (float d = 0.25f; d <= 8f; d += 0.25f)
            {
                if (FloorThere(at + back * d + Vector3.up * 3f, 6f, floorY, 0.25f, out _)) continue;
                ahead = d; break;
            }
            return $"the Exit ('{exit.name}', x {Say(exit.transform.position.x)}) carries a player the zone does NOT hold that walks into it toward the Entrance to {Say(at)}, facing yaw {Say(facing.eulerAngles.y)} (away from the Entrance; 270 = west), "
                + $"FlatView > Link Landing {Say(v.linkLanding)} behind the Entrance's middle; the floor there y {Say(floorY)} ({floorFrom}); {clear}; "
                + $"{(float.IsNaN(ahead) ? "floor for 8 ahead" : $"the floor ends {Say(ahead)} ahead (the hop's drop: walking on falls in)")}. "
                + $"With the shift warp (outward, {Say(v.blendSeconds)} s), both gates' flash and sound; both rest {Say(v.linkCooldown)} s (FlatView > Link Cooldown); the way in stays open {Say(v.riftWindow)} s behind a toy walking through it (FlatView > Rift Window); its respawn point is set there";
        }

        // ================================================================== the pixel look (D-0209)

        /// <summary>What the menu item does, with the report handed back as text. A report that starts "[Flat] NOT " means
        /// the renderer was not changed. Run again, it changes nothing that is there (a palette or material he changed
        /// is kept; only the material's colour count follows the palette's width).</summary>
        public static string AddPixelLook()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return $"{Tag} NOT done: Add Pixel Look does not run in Play. Stop the game first. Nothing was changed.";
            var did = new List<string>();
            Texture2D palette = PaletteTexture(did);
            Material look = PixelMaterial(palette, did);
            if (look == null) return $"{Tag} NOT done: {string.Join("; ", did)}. The renderer was not changed.";
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null) return $"{Tag} NOT done: no URP renderer at {RendererPath} ({string.Join("; ", did)}). The renderer was not changed.";

            FullScreenPassRendererFeature feature = FindPixelFeature(out _);
            if (feature != null)
            {
                did.Add($"{RendererPath} already has the {PixelFeatureName} feature: kept as it is ({(feature.isActive ? "ON in the asset: the zone switches it itself, so it should be off; it is switched OFF now" : "off")}; its material {(feature.passMaterial != null ? feature.passMaterial.name : "NONE: set to " + look.name)})");
                if (feature.isActive) { feature.SetActive(false); EditorUtility.SetDirty(data); }
                if (feature.passMaterial == null) { feature.passMaterial = look; EditorUtility.SetDirty(feature); EditorUtility.SetDirty(data); }
            }
            else
            {
                // As the renderer's own Inspector adds a feature (and PaintBuilder.EnsureDecalFeature does): a sub-asset
                // of the renderer, listed in its features with its file id in the map.
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = PixelFeatureName;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;   // as the shift warp's: after it in the list, so a warp is pixelated too
                feature.fetchColorBuffer = true;
                feature.requirements = ScriptableRenderPassInput.None;
                feature.passMaterial = look;
                feature.passIndex = 0;
                feature.bindDepthStencilAttachment = false;
                feature.SetActive(false);   // the zone switches it on only while its look shows
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                SerializedProperty features = so.FindProperty("m_RendererFeatures"), map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++; features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                data.SetDirty();
                EditorUtility.SetDirty(data);
                did.Add($"ADDED the {PixelFeatureName} feature to {RendererPath} (Full Screen Pass, after post-processing, fetch colour on, off until a zone shows its look, material {look.name}; feature {data.rendererFeatures.Count} of {data.rendererFeatures.Count}, after ShiftWarp)");
            }
            AssetDatabase.SaveAssets();
            FullScreenPassRendererFeature check = FindPixelFeature(out string where);
            if (check == null) return $"{Tag} NOT done: the feature was not found on {RendererPath} after adding it ({string.Join("; ", did)}). Add it by hand: see the notes of D-0209.";
            return $"{Tag} Add Pixel Look (D-0209): {string.Join("; ", did)}. NOW: {where}. Run Shift Jam > Flat > Land Hallway Scroller to hand it to FlatZone_Hall (Pixel Look stays off: tick it to see).";
        }

        /// <summary>The PixelShift feature on PC_Renderer, or null; <paramref name="where"/> says which it is and its state.</summary>
        private static FullScreenPassRendererFeature FindPixelFeature(out string where)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null) { where = $"NO renderer at {RendererPath}"; return null; }
            for (int i = 0; i < data.rendererFeatures.Count; i++)
                if (data.rendererFeatures[i] is FullScreenPassRendererFeature f && f.name == PixelFeatureName)
                {
                    where = $"{RendererPath} feature {i + 1} of {data.rendererFeatures.Count} '{f.name}' ({(f.isActive ? "ON in the asset" : "off in the asset")}, {f.injectionPoint}, material {(f.passMaterial != null ? f.passMaterial.name : "NONE")})";
                    return f;
                }
            where = $"NOT on {RendererPath} (run Shift Jam > Flat > Add Pixel Look)";
            return null;
        }

        /// <summary>The palette picture: kept when it is there, written from PaletteColours when it is not; its import
        /// settings are the pixel sheet's (point, no mip maps, not rescaled, uncompressed, clamped).</summary>
        private static Texture2D PaletteTexture(List<string> did)
        {
            if (!System.IO.File.Exists(PalettePath))
            {
                WorkshopPieces.EnsureFolder(RiftArt.TrimEnd('/'));
                var picture = new Texture2D(PaletteColours.Length, 1, TextureFormat.RGBA32, false);
                picture.SetPixels32(PaletteColours); picture.Apply();
                System.IO.File.WriteAllBytes(PalettePath, picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
                AssetDatabase.ImportAsset(PalettePath, ImportAssetOptions.ForceUpdate);
                did.Add($"{PalettePath} MADE ({PaletteColours.Length} colours: his title's 14 and 7 warm ones)");
            }
            else did.Add($"{PalettePath} was there: kept");
            SheetImport(PalettePath, did);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
        }

        /// <summary>The pass's material: made from the shader when missing, kept when there; its palette set when it has
        /// none, and its colour count set to the palette's width.</summary>
        private static Material PixelMaterial(Texture2D palette, List<string> did)
        {
            var look = AssetDatabase.LoadAssetAtPath<Material>(PixelMaterialPath);
            if (look == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(PixelShaderPath);
                if (shader == null) { did.Add($"no shader at {PixelShaderPath}"); return null; }
                look = new Material(shader) { name = "PixelShift" };
                AssetDatabase.CreateAsset(look, PixelMaterialPath);
                did.Add($"{PixelMaterialPath} MADE");
            }
            else did.Add($"{PixelMaterialPath} was there: kept");
            if (look.GetTexture("_Palette") == null && palette != null) { look.SetTexture("_Palette", palette); did.Add("its palette set"); }
            Texture used = look.GetTexture("_Palette");
            if (used != null && !Mathf.Approximately(look.GetFloat("_PaletteCount"), used.width)) { look.SetFloat("_PaletteCount", used.width); did.Add($"its colour count set to {used.width}"); }
            EditorUtility.SetDirty(look);
            return look;
        }

        private static int PixelColumns(int rows, float aspect) => Mathf.Max(1, Mathf.FloorToInt(rows * aspect));

        // ================================================================== the letters (D-0208)

        /// <summary>The five letters under the zone, each where its line says (see the constants), with its shape, its
        /// materials, the sheet and its sound. One line per letter into <paramref name="lines"/> (its place, its size,
        /// what its trigger touches in the scene, why it is reachable); anything that does not add up into
        /// <paramref name="warn"/>. Returns how many were placed (0 without the title's letter shapes).</summary>
        private static int PlaceLetters(Transform zone, Scene scene, Transform hall, float laneZ, float ceilingY, float beforeX, float afterX, List<string> lines, List<string> warn)
        {
            var shapes = new Mesh[LetterShapes.Length];
            for (int i = 0; i < shapes.Length; i++)
            {
                shapes[i] = AssetDatabase.LoadAssetAtPath<Mesh>(LetterShapes[i]);
                if (shapes[i] == null) { warn.Add($"no {LetterShapes[i]}: NO LETTERS were placed (run Shift Jam > Menu > Build Title Menu Model, which bakes them, then land again)"); return 0; }
            }
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleSheet);
            if (sheet == null) warn.Add($"no {TitleSheet}: the letters show their material's own colour");
            Material solid = LetterMaterial(LetterLook, "FlatLetter", false, sheet, lines);
            Material ghost = LetterMaterial(GhostLook, "FlatLetterGhost", true, sheet, lines);
            if (solid == null) { warn.Add("no material for the letters: NO LETTERS were placed"); return 0; }

            // The toy's numbers: its jump at toy size, the ropes' hold.
            var moves = AssetDatabase.LoadAssetAtPath<MovementSettings>(MovesPath);
            var shift = AssetDatabase.LoadAssetAtPath<ShiftSettings>(ShiftPath);
            float toy = shift != null ? shift.toyScale : 1f;
            float jump = moves != null ? moves.jumpHeight * toy : FallbackJump;
            RopeSettings ropeSettings = RopeSettings.Load();
            float hang = ropeSettings != null ? ropeSettings.hangHold : FallbackHang;
            lines.Add($"the toy's numbers: a full jump lifts the feet {Say(jump)} ({(moves != null ? MovesPath + " Jump Height " + Say(moves.jumpHeight) + " x toy scale " + Say(toy) : "NOT FOUND, the 24 Nov number")}), the toy is {Say(ToyTall)} tall, a rope's grab settles {Say(hang)} under the tie ({(ropeSettings != null ? "Resources/Rope Hang Hold" : "NOT FOUND, the 24 Nov number")})");

            var sounds = AssetDatabase.LoadAssetAtPath<UiSounds>(UiSoundsPath);
            CourseSounds.Cue taken = sounds != null ? sounds.confirm : default;
            if (taken.clip == null) warn.Add($"no clip in {UiSoundsPath} > Confirm: a letter is taken in silence");

            var places = new Vector3[LetterShapes.Length];
            var why = new string[LetterShapes.Length];

            // S: over the tread of Book5, clear of a walking head on Book6; a full jump from Book5 reaches it.
            float half = LetterTall * 0.5f;
            Transform stairs = hall != null ? hall.Find("UpStairs") : null;
            Transform b5 = stairs != null ? stairs.Find("Book" + SBook) : null, b6 = stairs != null ? stairs.Find("Book" + (SBook + 1)) : null;
            float treadFrom, treadTo, top5, top6; string from;
            if (b5 != null && b6 != null && DrawnBounds(b5.gameObject, out Bounds bb5) && DrawnBounds(b6.gameObject, out Bounds bb6))
            {
                treadFrom = bb5.min.x; treadTo = bb6.min.x; top5 = bb5.max.y; top6 = bb6.max.y;
                from = $"UpStairs/Book{SBook} (x {Say(bb5.min.x)} to {Say(bb5.max.x)}, top {Say(top5)}) and Book{SBook + 1} (from x {Say(bb6.min.x)}, top {Say(top6)})";
            }
            else
            {
                float boxTop = StepBoxTop;
                int n = Mathf.CeilToInt(boxTop / BookRiseMost);
                float rise = boxTop / n;
                treadFrom = StairsFromX + SBook * BookTread; treadTo = treadFrom + BookTread;
                top5 = (SBook + 1) * rise; top6 = (SBook + 2) * rise;
                from = $"UpStairs/Book{SBook} or Book{SBook + 1} NOT FOUND: the stairs as CourseBuilder lays them (tread x {Say(treadFrom)} to {Say(treadTo)}, tops {Say(top5)} and {Say(top6)})";
            }
            float sBottom = Mathf.Max(top5, top6) + ToyTall + LetterClear;
            places[0] = new Vector3((treadFrom + treadTo) * 0.5f, sBottom + half, laneZ);
            why[0] = $"{from}: its bottom {Say(sBottom)} is {Say(LetterClear)} over the head of a toy on Book{SBook + 1} ({Say(top6 + ToyTall)}), so walking up never takes it; "
                + $"a full jump from Book{SBook} lifts the head to {Say(top5 + ToyTall + jump)} ({Say(top5 + ToyTall + jump - sBottom)} into it); a tapped jump (a quarter as high) to {Say(top5 + ToyTall + jump * 0.25f)} (not)";
            if (top5 + ToyTall + jump <= sBottom) warn.Add($"S CANNOT BE REACHED: a jump from Book{SBook} lifts the head to {Say(top5 + ToyTall + jump)}, under its bottom {Say(sBottom)}");

            // H, I, F: halfway between two ropes, the bottom at the head of a toy on the first one at a 30 degree swing.
            var ropeAt = new Vector3[RopeNames.Length];
            var ropeFound = new bool[RopeNames.Length];
            for (int i = 0; i < RopeNames.Length; i++)
            {
                Transform rope = RootNamed(scene, RopeNames[i]);
                ropeFound[i] = rope != null;
                ropeAt[i] = rope != null ? rope.position : new Vector3(RopeXs[i], ceilingY, laneZ);
            }
            float cos = Mathf.Cos(GoodSwing * Mathf.Deg2Rad), sin = Mathf.Sin(GoodSwing * Mathf.Deg2Rad);
            for (int k = 0; k < 3; k++)
            {
                Vector3 a = ropeAt[k], b = ropeAt[k + 1];
                float headAt = a.y - hang * cos + HeadOverHands;          // the head at a 30 degree swing on rope a
                float bottom = headAt;
                places[k + 1] = new Vector3((a.x + b.x) * 0.5f, bottom + half, laneZ);
                float head40 = a.y - hang * Mathf.Cos(40f * Mathf.Deg2Rad) + HeadOverHands;
                why[k + 1] = $"halfway between {RopeNames[k]} (x {Say(a.x)}, tied at y {Say(a.y)}{(ropeFound[k] ? "" : ", NOT FOUND: measured")}) and {RopeNames[k + 1]} (x {Say(b.x)}{(ropeFound[k + 1] ? "" : ", NOT FOUND: measured")}): "
                    + $"held {Say(hang)} under the tie, the hands are {Say(hang * sin)} out at a {Say(GoodSwing)} degree swing with the head at {Say(headAt)} = its bottom; at 40 degrees the head is at {Say(head40)} ({Say(head40 - bottom)} into it), {Say(hang * Mathf.Sin(40f * Mathf.Deg2Rad))} out; "
                    + $"hanging still the head is at {Say(a.y - hang + HeadOverHands)} (under it), and a let-go on the way up flies higher";
            }

            // T: over the middle of the sprint gap, at the top of a jump's arc.
            float gapWest = beforeX + GapFromCheckpoint, gapEast = afterX - GapFromCheckpoint;
            float tMiddle = jump + ToyTall * 0.5f + TOver;
            places[4] = new Vector3((gapWest + gapEast) * 0.5f, tMiddle, laneZ);
            why[4] = $"over the middle of the sprint gap (x {Say(gapWest)} to {Say(gapEast)}, from CP_beforeSprint + {Say(GapFromCheckpoint)} and CP_afterSprint - {Say(GapFromCheckpoint)}): its bottom {Say(tMiddle - half)} is {Say(tMiddle - half - ToyTall)} over a standing toy's head; "
                + $"at the top of a jump the head is at {Say(jump + ToyTall)} ({Say(jump + ToyTall - (tMiddle - half))} into it): a sprint jump that clears the gap passes through it near the top of its arc";

            int placed = 0;
            for (int i = 0; i < places.Length; i++)
            {
                Mesh shape = shapes[i];
                var go = new GameObject("Letter_" + FlatLetter.Word[i]);
                go.transform.SetParent(zone, true);
                go.transform.SetPositionAndRotation(places[i], Quaternion.identity);   // the shape's viewer side (-z) faces the side camera (south)
                go.AddComponent<BoxCollider>().isTrigger = true;
                var lookT = new GameObject("Look").transform;
                lookT.SetParent(go.transform, false);
                lookT.gameObject.AddComponent<MeshFilter>().sharedMesh = shape;
                var r = lookT.gameObject.AddComponent<MeshRenderer>();
                var mats = new Material[Mathf.Max(1, shape.subMeshCount)];
                for (int m = 0; m < mats.Length; m++) mats[m] = solid;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                var letter = go.AddComponent<FlatLetter>();
                WorkshopPieces.Set(letter, "zone", zone.GetComponent<FlatZone>());
                WorkshopPieces.Set(letter, "index", i);
                WorkshopPieces.Set(letter, "look", r);
                WorkshopPieces.Set(letter, "height", LetterTall);
                WorkshopPieces.Set(letter, "sheet", sheet);
                WorkshopPieces.Set(letter, "frames", TitleFrames);
                WorkshopPieces.Set(letter, "solid", solid);
                WorkshopPieces.Set(letter, "ghost", ghost);
                WorkshopPieces.Set(letter, "bob", LetterBob);
                var so = new SerializedObject(letter);
                so.FindProperty("sound.clip").objectReferenceValue = taken.clip;
                so.FindProperty("sound.volume").floatValue = taken.clip != null && taken.volume > 0f ? taken.volume : 0.5f;
                so.FindProperty("sound.pitchSpread").floatValue = 0.04f;
                so.ApplyModifiedPropertiesWithoutUndo();
                letter.Fit();
                placed++;

                BoxCollider touch = go.GetComponent<BoxCollider>();
                Vector3 size = touch.size;
                Vector3 lo = places[i] - size * 0.5f, hi = places[i] + size * 0.5f;
                if (hi.y + LetterBob > ceilingY) warn.Add($"{go.name}'s top ({Say(hi.y)} and its bob {Say(LetterBob)}) reaches the ceiling (y {Say(ceilingY)})");
                lines.Add($"{FlatLetter.Word[i]} at {Say(places[i])}: {Say(size.x)} wide, {Say(size.y)} tall (x {Say(lo.x)} to {Say(hi.x)}, y {Say(lo.y)} to {Say(hi.y)}), its trigger {Say(size.z)} across the lane; {Touches(touch)}; WHY: {why[i]}");
            }
            return placed;
        }

        /// <summary>What a letter's trigger box overlaps in the scene that is solid (not a trigger, not the zone's own):
        /// "clear of everything solid" is what is wanted. The editor's physics is brought up to date first.</summary>
        private static string Touches(BoxCollider touch)
        {
            Physics.SyncTransforms();
            Transform t = touch.transform;
            Collider[] hits = Physics.OverlapBox(t.TransformPoint(touch.center), touch.size * 0.5f, t.rotation, ~0, QueryTriggerInteraction.Ignore);
            var names = new List<string>();
            foreach (Collider c in hits)
            {
                if (c == null || c == touch || c.GetComponentInParent<FlatZone>() != null) continue;
                names.Add(c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name);
            }
            return names.Count == 0 ? "clear of everything solid" : "IT OVERLAPS " + string.Join(", ", names) + " (move it: a letter must not stand inside the hallway's things)";
        }

        /// <summary>A letter's material, made when missing (kept when there): a copy of the menu slab's unlit material
        /// with his title sheet on it; the ghost's is see-through (drawn after the room, no depth written, no shadow).</summary>
        private static Material LetterMaterial(string path, string name, bool seeThrough, Texture2D sheet, List<string> did)
        {
            var have = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (have != null) return have;
            var source = AssetDatabase.LoadAssetAtPath<Material>(SlabLook);
            if (source == null) { did.Add($"no {SlabLook} to copy for {path}"); return null; }
            var m = new Material(source) { name = name };
            m.SetColor("_BaseColor", Color.white);
            if (sheet != null) { m.SetTexture("_BaseMap", sheet); m.SetTextureScale("_BaseMap", Vector2.one); m.SetTextureOffset("_BaseMap", Vector2.zero); }   // the shape's boxes carry their own pixels of the first frame
            if (seeThrough)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_AlphaClip", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("MotionVectors", false);
            }
            AssetDatabase.CreateAsset(m, path);
            did.Add($"{path} MADE ({(seeThrough ? "unlit, see-through" : "unlit")}, a copy of {SlabLook})");
            return m;
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

        /// <summary>A rift gate's prefab (D-0207): the portal gate's parts (its FlatGate, trigger, floor glow and flash light)
        /// with the portal's Model taken out and the rift's look put in as the child "Look". Kept as it is when it is there
        /// (unless <paramref name="rebuild"/>); null when the rift's art is not in the project.</summary>
        private static GameObject RiftPiece(GameObject portal, string path, string label, bool rebuild, List<string> did, System.Func<Transform, List<string>, List<Renderer>> makeLook)
        {
            var there = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (there != null && !rebuild) { did.Add($"{path} was there: kept as it is"); return there; }
            Scene stage = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(portal, stage);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = System.IO.Path.GetFileNameWithoutExtension(path);
                Transform model = root.transform.Find("Model");
                if (model != null) Object.DestroyImmediate(model.gameObject);
                var look = new GameObject("Look").transform;
                look.SetParent(root.transform, false);
                List<Renderer> glow = makeLook(look, did);
                if (glow == null) { did.Add($"{path} NOT made: {label}'s art is not in the project"); return null; }
                FlatGate gate = root.GetComponent<FlatGate>();
                WorkshopPieces.Set(gate, "ripple", (Object)null);
                var glowObjects = new List<Object>(); foreach (Renderer r in glow) glowObjects.Add(r);
                WorkshopPieces.SetArray(gate, "glow", glowObjects);
                Transform lightT = root.transform.Find("FlashLight");
                if (lightT != null && DrawnBounds(look.gameObject, out Bounds lb)) lightT.localPosition = new Vector3(0f, lb.center.y - root.transform.position.y, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                did.Add(saved ? $"{path} {(there != null ? "REBUILT" : "MADE")}: {label}" : $"{path} DID NOT SAVE");
            }
            finally { EditorSceneManager.ClosePreviewScene(stage); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>The way in: the pixel rift, a quad that steps through the sheet's frames (SheetLook), its pixels kept
        /// sharp, seen from both sides, standing on the floor. It does not brighten in a flash (its pixels are the look);
        /// the floor glow and the flash light carry the gate's colour.</summary>
        private static GameObject RiftInPiece(GameObject portal, bool rebuild, List<string> did)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(RiftInSheet) == null) { did.Add($"no {RiftInSheet}: the way in stays his portal"); return null; }
            return RiftPiece(portal, RiftInPath, "the pixel rift", rebuild, did, (look, notes) =>
            {
                SheetImport(RiftInSheet, notes);
                var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(RiftInSheet);
                Material mat = RiftMaterial("RiftIn", Color.white, sheet, true, notes);
                if (mat == null) return null;
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = "Sheet";
                quad.transform.SetParent(look, false);
                float w = sheet.width / (float)RiftInFrames * RiftInPixel, h = sheet.height * RiftInPixel;
                quad.transform.localScale = new Vector3(w, h, 1f);
                quad.transform.localPosition = new Vector3(0f, h * 0.5f - RiftInBelow, 0f);
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                var steps = quad.AddComponent<SheetLook>();
                WorkshopPieces.Set(steps, "frames", RiftInFrames);
                notes.Add($"the pixel rift {Say(w)} x {Say(h)}, {RiftInFrames} frames");
                return new List<Renderer>();
            });
        }

        /// <summary>The way out: the 3D rift model, its spikes turned toward the gate's forward (the side camera), swaying
        /// and bobbing (RiftLook); its rim, spikes and inside brighten in a flash.</summary>
        private static GameObject RiftOutPiece(GameObject portal, bool rebuild, List<string> did)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RiftOutModel);
            if (modelAsset == null) { did.Add($"no {RiftOutModel}: the way out stays his portal"); return null; }
            return RiftPiece(portal, RiftOutPath, "the 3D rift", rebuild, did, (look, notes) =>
            {
                Material edge = RiftMaterial("RiftEdge", new Color(0.45f, 0.95f, 1f), null, false, notes);
                Material pink = RiftMaterial("RiftShardPink", new Color(1f, 0.45f, 0.8f), null, false, notes);
                Material inside = RiftMaterial("RiftInside", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(WarpArt), false, notes);
                if (edge == null || pink == null || inside == null) return null;
                var model = (GameObject)Object.Instantiate(modelAsset, look, false);
                model.name = "Model";
                model.transform.localScale = Vector3.one * RiftOutScale;
                var glow = new List<Renderer>();
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        string n = mats[i] != null ? mats[i].name : "";
                        mats[i] = n.StartsWith("RiftInside") ? inside : n.StartsWith("RiftShardPink") ? pink : edge;
                    }
                    r.sharedMaterials = mats; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    glow.Add(r);
                }
                // The spikes point toward the viewer as made: turn the model so they point along the gate's forward.
                Transform spikes = model.transform.Find("RiftSpikes"), face = model.transform.Find("RiftInside");
                if (spikes != null && face != null && DrawnBounds(spikes.gameObject, out Bounds sb) && DrawnBounds(face.gameObject, out Bounds fb)
                    && look.InverseTransformPoint(sb.center).z < look.InverseTransformPoint(fb.center).z)
                    model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
                if (DrawnBounds(model, out Bounds mb)) model.transform.localPosition -= new Vector3(0f, mb.min.y - look.position.y, 0f) - Vector3.up * 0.05f;
                look.gameObject.AddComponent<RiftLook>();
                notes.Add($"the 3D rift at {Say(RiftOutScale)} of its model");
                return glow;
            });
        }

        /// <summary>An unlit material for a rift (a copy of the menu slab's), made when missing, kept when there. A sheet's
        /// material clips its see-through pixels and is seen from both sides.</summary>
        private static Material RiftMaterial(string name, Color colour, Texture picture, bool sheet, List<string> did)
        {
            string path = Data + name + ".mat";
            var have = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (have != null) return have;
            var source = AssetDatabase.LoadAssetAtPath<Material>(SlabLook);
            if (source == null) { did.Add($"no {SlabLook} to copy for {path}"); return null; }
            var m = new Material(source) { name = name };
            m.SetColor("_BaseColor", colour);
            if (picture != null) m.SetTexture("_BaseMap", picture);
            if (sheet)
            {
                m.SetTextureScale("_BaseMap", new Vector2(1f / RiftInFrames, 1f));   // one frame (the first): what the editor shows; SheetLook steps them in Play
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f);   // both sides
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            AssetDatabase.CreateAsset(m, path);
            did.Add($"{path} MADE");
            return m;
        }

        /// <summary>A pixel sheet's import settings: its pixels as they are (point filter, no mip maps, not rescaled,
        /// uncompressed, clamped, see-through).</summary>
        private static void SheetImport(string path, List<string> did)
        {
            var import = AssetImporter.GetAtPath(path) as TextureImporter;
            if (import == null) return;
            if (import.textureType == TextureImporterType.Default && !import.mipmapEnabled && import.filterMode == FilterMode.Point && import.wrapMode == TextureWrapMode.Clamp
                && import.npotScale == TextureImporterNPOTScale.None && import.textureCompression == TextureImporterCompression.Uncompressed && import.alphaIsTransparency) return;
            import.textureType = TextureImporterType.Default; import.sRGBTexture = true; import.alphaIsTransparency = true; import.mipmapEnabled = false;
            import.filterMode = FilterMode.Point; import.wrapMode = TextureWrapMode.Clamp; import.npotScale = TextureImporterNPOTScale.None;
            import.textureCompression = TextureImporterCompression.Uncompressed; import.maxTextureSize = 8192;
            import.SaveAndReimport();
            did.Add($"{path}: import settings set (point, no mip maps, not rescaled)");
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

        /// <summary>The prefab a placed gate is an instance of, by its path.</summary>
        private static string PieceName(GameObject go)
        {
            GameObject source = go != null ? PrefabUtility.GetCorrespondingObjectFromSource(go) : null;
            return source != null ? AssetDatabase.GetAssetPath(source) : "no prefab";
        }

        /// <summary>One gate for the report: where it stands, what it is, what it draws and where its trigger reaches.</summary>
        private static string GateLine(GameObject go)
        {
            var gate = go.GetComponent<FlatGate>();
            var so = gate != null ? new SerializedObject(gate) : null;
            string role = so != null ? (so.FindProperty("role").enumValueIndex == (int)FlatGate.Role.Exit ? "EXIT" : "ENTRANCE") : "NO FlatGate SCRIPT";
            string colour = so != null ? Say(so.FindProperty("colour").colorValue) : "-";
            Transform swirl = go.transform.Find("Model/Swirl");
            if (swirl == null) swirl = go.transform.Find("Look");   // a rift: its look
            string drawn = swirl != null && DrawnBounds(swirl.gameObject, out Bounds b)
                ? $"the drawing x {Say(b.min.x)} to {Say(b.max.x)}, y {Say(b.min.y)} to {Say(b.max.y)}, z {Say(b.min.z)} to {Say(b.max.z)}"
                : "no drawing found";
            string touch = "no trigger";
            if (go.TryGetComponent(out BoxCollider box))
            {
                Vector3 lo = go.transform.TransformPoint(box.center - box.size * 0.5f), hi = go.transform.TransformPoint(box.center + box.size * 0.5f);
                touch = $"its trigger x {Say(Mathf.Min(lo.x, hi.x))} to {Say(Mathf.Max(lo.x, hi.x))}, y {Say(Mathf.Min(lo.y, hi.y))} to {Say(Mathf.Max(lo.y, hi.y))}, z {Say(Mathf.Min(lo.z, hi.z))} to {Say(Mathf.Max(lo.z, hi.z))}{(role == "EXIT" ? (so.FindProperty("link").objectReferenceValue != null ? ", carries back to '" + so.FindProperty("link").objectReferenceValue.name + "'" : ", NO link back") : " (the way in: a player walking through it eastward is invited in)")}";
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
