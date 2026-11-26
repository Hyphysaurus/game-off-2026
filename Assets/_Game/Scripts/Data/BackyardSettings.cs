using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The backyard (D-0239): the garden past the end window, and the game's end at its far wall ("get to the end of the
    /// garden"). Every garden piece is placed from the lists here, by role, never from names in code: he picks the kit
    /// after a picture sheet, swaps the lists here and runs the maker again (Shift Jam > House > Land Backyard (D-0239)).
    /// The lists start PROVISIONAL (the SICS Toon packs on disk on 25 Nov). Sizes are world units: the house is about 50 a
    /// metre, the toy stands about 2 and the kid 63 (the kid jumps 70, the toy 2). Every word is a PLACEHOLDER for him to
    /// rewrite (in-game text written by Claude counts for Steam's AI disclosure). The window and the lost toy read their
    /// numbers here at run time (Resources/BackyardSettings); the rest is the maker's.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Backyard Settings", fileName = "BackyardSettings")]
    public sealed class BackyardSettings : ScriptableObject
    {
        public const string ResourceName = "BackyardSettings";

        /// <summary>One kind of garden piece: what it can be, how big, and how many or how far apart.</summary>
        [Serializable]
        public sealed class Role
        {
            [Tooltip("The prefabs this role picks from (seeded, so a second run places the same). Empty = this role places nothing.")]
            public GameObject[] prefabs = new GameObject[0];
            [Tooltip("The smallest uniform scale. The SICS kit is in metres: about 50 is true to the house.")]
            public float scaleMin = 50f;
            [Tooltip("The largest uniform scale (each piece takes a seeded value between the two).")]
            public float scaleMax = 50f;
            [Tooltip("How many, where this role is scattered. 0 = none.")]
            [Min(0)] public int count;
            [Tooltip("The distance between neighbours, where this role is laid in a line (hedges, grass walls, path stones), world units.")]
            [Min(0f)] public float spacing;
        }

        [Header("The kit (PROVISIONAL: his pick replaces these lists)")]
        [Tooltip("The maze's tall grass at toy size (reeds, ferns): each clump is scaled to Grass Height, not by its scale range. Spacing = between clumps along a maze wall. No colliders of their own: the maker gives each maze wall one box.")]
        public Role grass = new Role { scaleMin = 1f, scaleMax = 1f, spacing = 2.4f };
        [Tooltip("The flowerbed's flowers. Count = how many on the bed. No colliders (stems the toy walks between).")]
        public Role flowers = new Role { scaleMin = 30f, scaleMax = 45f, count = 26 };
        [Tooltip("The low hedges across the yard the toy cannot cross (only through the maze, only over the log); the kid steps over them. Spacing = between bushes along a row. Each gets a box fitted to it.")]
        public Role hedges = new Role { scaleMin = 11f, scaleMax = 13f, spacing = 22f };
        [Tooltip("Small rocks round the pond and the flowerbed. Count = how many.")]
        public Role rocks = new Role { scaleMin = 14f, scaleMax = 22f, count = 14 };
        [Tooltip("THE BOUNDARY (blocky, the house's own toy look): the garden's two sides, rows of bushes and trees from the house to the back wall. Spacing = between pieces along a side; each piece is scaled to a height in Boundary Height (its scale range is not used). An invisible wall just outside the row seals it (Seals).")]
        public Role boundary = new Role { scaleMin = 1f, scaleMax = 1f, spacing = 44f };
        [Tooltip("The boundary pieces' height, smallest and largest (the kid jumps 70: keep them over 90).")]
        public Vector2 boundaryHeight = new Vector2(95f, 140f);
        [Tooltip("THE BACKDROP (blocky): trees and bushes planted past the garden's end, outside the play area, where the old GardenTrees leave room. Count = how many; Spacing = the least distance between two (and from an old tree); each is scaled to a height in Backdrop Height (its scale range is not used).")]
        public Role backdrop = new Role { scaleMin = 1f, scaleMax = 1f, count = 14, spacing = 70f };
        [Tooltip("The backdrop pieces' height, smallest and largest.")]
        public Vector2 backdropHeight = new Vector2(110f, 170f);
        [Tooltip("The back wall's sections either side of the gate, laid end to end (the first that fits, then the next). Their slats get one box each: the toy slips between them anywhere, the kid never.")]
        public Role backWall = new Role { scaleMin = 50f, scaleMax = 50f };
        [Tooltip("The gate in the middle of the back wall (the first prefab). Its own collider is kept; its open middle is filled with the Gate Panel.")]
        public Role gate = new Role { scaleMin = 50f, scaleMax = 50f };
        [Tooltip("The slatted section that fills the gate's opening, the object \"Gate Panel\" (the first prefab): fitted to the opening's width and Gate Panel Height, one box per slat. D-0240 may swap it for a breakable one.")]
        public Role gatePanel = new Role { scaleMin = 50f, scaleMax = 50f };
        [Tooltip("Lily pads and pond plants on the water. Count = how many.")]
        public Role waterPlants = new Role { scaleMin = 9f, scaleMax = 13f, count = 6 };
        [Tooltip("The way over the pond at toy size (the first prefab): a long piece (a log) is scaled to span the pond; a short one (a stump) makes Count stepping stones. Its scale range is not used.")]
        public Role crossing = new Role { scaleMin = 1f, scaleMax = 1f, count = 5 };
        [Tooltip("The garden path's stepping stones, from the window to the gate. Spacing = between stones.")]
        public Role pathStones = new Role { scaleMin = 22f, scaleMax = 26f, spacing = 46f };
        [Tooltip("Garden furniture on the lawn by the house, either side of the glide's way. Count = how many.")]
        public Role props = new Role { scaleMin = 50f, scaleMax = 50f, count = 5 };
        [Tooltip("Small things in the grass (mushrooms, little flowers). Count = how many. Each gets a box fitted to it (they look solid to the toy).")]
        public Role accents = new Role { scaleMin = 16f, scaleMax = 24f, count = 22 };
        [Tooltip("The lost toy hidden in the flowerbed (the first prefab, at its scale): finding it earns the new gold star.")]
        public Role lostToy = new Role { scaleMin = 0.12f, scaleMax = 0.12f };
        [Tooltip("The ramp the toy walks up from the window's ledge to its open pane (the first prefab: the playroom's ruler). Scaled to fit; its scale range is not used.")]
        public Role windowRamp = new Role { scaleMin = 1f, scaleMax = 1f };
        [Tooltip("The pond's water on a flat disc. Empty: the maker makes Art/Garden/Materials/Garden_Pond.mat (URP Lit, a clear blue). The SICS Toon water (Toon/CustomWaterUnlit) can go here if it draws in this renderer.")]
        public Material water;
        [Tooltip("The flowerbed's soil. Empty: the maker makes Art/Garden/Materials/Garden_Soil.mat (URP Lit, plain brown).")]
        public Material soil;

        [Header("The lawn (the Ground's look)")]
        [Tooltip("Ticked: the Ground wears Lawn. Unticked: the maker puts back the material it wore before (Lawn Was).")]
        public bool lawnOn = true;
        [Tooltip("The lawn (URP Lit). Empty: the maker makes Art/Garden/Materials/Garden_Lawn.mat on the Toon Nature grass texture (TNA_Grass_1A_D).")]
        public Material lawn;
        [Tooltip("One repeat of the lawn's texture across this many world units.")]
        [Min(1f)] public float lawnTile = 60f;
        [Tooltip("The Ground's own material before the lawn (kept by the maker the first time; put back when Lawn On is unticked).")]
        public Material lawnWas;

        /// <summary>One pack material and the game's own material drawn in its place on the garden's pieces.</summary>
        [Serializable]
        public sealed class MaterialSwap
        {
            [Tooltip("A material of the pack (never edited).")] public Material from;
            [Tooltip("Drawn in its place on every garden piece. The maker makes a URP Lit copy (Art/Garden/Materials) the first time it meets a pack material; point it elsewhere to change it.")]
            public Material to;
        }

        [Header("Materials: the game's shader on the pack's pieces")]
        [Tooltip("Ticked: every placed piece wears the materials in Material Swaps (URP Lit, like every other model in the game): the SICS shaders drew pieces invisible in this renderer. Unticked: the pack's own materials.")]
        public bool swapMaterials = true;
        [Tooltip("Pack material -> the game's material. The maker adds a row (and its URP Lit copy) for each pack material it meets; it never changes a row's To once set.")]
        public MaterialSwap[] materialSwaps = new MaterialSwap[0];
        [Tooltip("The copies' smoothness (the kid's own Color material: 0.07).")]
        [Range(0f, 1f)] public float swapSmoothness = 0.07f;

        [Header("The yard (world units)")]
        [Tooltip("The yard's width across, centred on the window. 400 = the playroom's whole north wall.")]
        [Min(60f)] public float yardWidth = 400f;
        [Tooltip("From the house's outer wall to the middle of the back wall.")]
        [Min(120f)] public float yardDepth = 470f;
        [Tooltip("The lawn in front of the window before the grass: a glide from the sill lands 170 to 190 out, so this keeps the maze past it.")]
        [Min(20f)] public float landingDepth = 200f;
        [Tooltip("Kept clear in front of the window (no trees, props or accents), across: the glide's way out.")]
        [Min(0f)] public float glideLaneWidth = 120f;
        [Tooltip("The yard's middle strip kept clear for the path (no accents, props or flowers), across.")]
        [Min(0f)] public float pathWidth = 30f;

        [Header("The tall grass maze (toy size)")]
        [Tooltip("How tall the grass stands. The toy is about 2 and jumps 2; the kid's ankle is about 6, its knee 18.")]
        [Min(2.5f)] public float grassHeight = 8f;
        [Tooltip("Each clump's height varies by up to this share either way.")]
        [Range(0f, 0.5f)] public float grassHeightJitter = 0.15f;
        [Tooltip("The maze's cells across.")]
        [Min(2)] public int mazeColumns = 7;
        [Tooltip("The maze's cells deep.")]
        [Min(2)] public int mazeRows = 6;
        [Tooltip("The clear width of a maze path between grass walls (the toy is 0.7 across).")]
        [Min(1.5f)] public float mazePathWidth = 5f;
        [Tooltip("A grass wall's thickness (its box).")]
        [Min(0.5f)] public float mazeWallThickness = 3f;
        [Tooltip("The maze's layout seed: another number, another maze.")]
        public int mazeSeed = 239;

        [Header("The pond (toy size)")]
        [Tooltip("The lawn between the maze's back and the pond's front.")]
        [Min(10f)] public float pondGap = 48f;
        [Tooltip("The pond's width (a disc). The kid crosses it in one step; the toy falls in and goes back to the checkpoint.")]
        [Min(8f)] public float pondDiameter = 34f;
        [Tooltip("The water's surface over the lawn.")]
        [Min(0.05f)] public float waterLift = 0.3f;
        [Tooltip("The log rests this far on each bank past the water.")]
        [Min(0.5f)] public float crossingOverhang = 4f;
        [Tooltip("The log is sunk this far into the ground, so its top is an easy hop for the toy.")]
        [Min(0f)] public float crossingSink = 0.5f;
        [Tooltip("Stepping stones only: each stone's top over the water, and its width.")]
        [Min(0.1f)] public float stoneTop = 0.8f;
        [Tooltip("Stepping stones only: each stone's width.")]
        [Min(1f)] public float stoneWidth = 3f;

        [Header("The flowerbed and the lost toy")]
        [Tooltip("The bed's middle: x across from the window's line (- = west), y = distance past the pond's far bank.")]
        public Vector2 flowerbedPlace = new Vector2(-105f, 55f);
        [Tooltip("The bed's size: across and deep.")]
        public Vector2 flowerbedSize = new Vector2(70f, 40f);
        [Tooltip("No flower closer than this to the lost toy (so it can be seen and reached).")]
        [Min(0f)] public float lostToyClearing = 6f;
        [Tooltip("How high the found toy hops.")]
        [Min(0f)] public float lostToyHop = 2.5f;
        [Tooltip("Seconds the hop takes.")]
        [Min(0.05f)] public float lostToyHopSeconds = 0.6f;
        [Tooltip("Played when the lost toy is found.")]
        public CourseSounds.Cue lostToyFound;

        [Header("The back wall and the end")]
        [Tooltip("The Gate Panel's height (the kid jumps 70: keep it over 90).")]
        [Min(10f)] public float gatePanelHeight = 95f;
        [Tooltip("The end (the Goal, toy size) past the back wall: how deep its strip is.")]
        [Min(4f)] public float endDepth = 24f;
        [Tooltip("Ticked: the end Goal takes either size (D-0240 will let the kid break through the Gate Panel). Unticked: the toy only (its Required Size, Toy, underneath).")]
        public bool endAnySize = true;
        [Tooltip("The garden core (Toggle, a copy of the sill's core) by the back wall: x across from the window's line, y = distance before the wall. It lets a kid shrink for the slats (and refills the charge).")]
        public Vector2 corePlace = new Vector2(55f, 40f);
        [Tooltip("How high the invisible walls reach (the CarGuard layer: the camera, shots and sight pass, bodies bump): the seals just outside the side rows and behind the end (always there: they also stop the toy between two boundary pieces), and over the back wall and the house's wall (so a kid standing on furniture cannot jump out). 0 = no caps over the back wall and the house; the seals then stop 10 over the boundary pieces.")]
        [Min(0f)] public float capHeight = 400f;

        [Header("The window")]
        [Tooltip("Ticked: the window opens when the player reaches the sill. Unticked: it is open from the start.")]
        public bool openOnSill = true;
        [Tooltip("Seconds the glass takes to fade away.")]
        [Min(0.05f)] public float glassFadeSeconds = 1.2f;
        [Tooltip("Played as the window opens.")]
        public CourseSounds.Cue windowOpens;
        [Tooltip("Ticked: a second paper plane lies on the sill (GliderPickup), for a toy that reaches it without one (the plane is not saved).")]
        public bool sparePlane = true;
        [Tooltip("The ramp's slope into the window, degrees (the toy walks up to 45).")]
        [Range(10f, 40f)] public float rampSlope = 28f;

        [Header("Checkpoints")]
        [Tooltip("The landing checkpoint starts this far out from the wall: a toy that falls from the window (and breaks) lands nearer and never claims it.")]
        [Min(0f)] public float landingClaimFrom = 45f;

        [Header("Light, the bend and the old trees")]
        [Tooltip("Ticked: the maker sets the garden sun (R6_Window/GardenSun) to the numbers below. Unticked: it is left alone.")]
        public bool tuneSun = true;
        [Tooltip("The sun's colour: warm, early morning.")]
        public Color sunColour = new Color(1f, 0.78f, 0.52f);
        [Tooltip("The sun's brightness (a point light: about this over the distance squared lands on the lawn).")]
        [Min(0f)] public float sunIntensity = 76000f;
        [Tooltip("The sun's reach.")]
        [Min(1f)] public float sunRange = 900f;
        [Tooltip("Where the sun is from the yard's middle: x east, y up, z north (low in the east: morning).")]
        public Vector3 sunFromYard = new Vector3(280f, 150f, 0f);
        [Tooltip("Ticked: the garden's Curved World rest bend is made flat (WorldBend > Rest Curvature 0), so the garden can be walked. Unticked: the maker writes Round Bend back.")]
        public bool flattenBend = true;
        [Tooltip("The rest bend written back when Flatten Bend is unticked (D-0102's was 0.8). The new pieces do not bend: they float over a bent lawn.")]
        [Min(0f)] public float roundBend = 0.8f;
        [Tooltip("Ticked: the old GardenTrees standing inside the play area (the yard and its end) are switched off (kept, not deleted); those outside stay as they are. Unticked: all of them are switched on.")]
        public bool insideTreesOff = true;
        [Tooltip("Every random choice of the maker (pieces, turns, scatter) follows this seed.")]
        public int seed = 2390;

        [Header("Words (PLACEHOLDERS, his to rewrite; the maker writes them only over the old words, never over his)")]
        [Tooltip("The big goal on ObjectiveSet (was \"Back on the window sill\").")]
        public string goal = "Get to the end of the garden";
        [Tooltip("The toy's first-minute goal line on OpeningSettings (was \"I need to get back up on that window sill.\").")]
        public string openingGoalLine = "I need to get to the end of the garden.";
        [Tooltip("The window step's words (the step 'sill', was \"Get onto the window sill\").")]
        public string stepOutWindow = "Out the window";
        [Tooltip("The new last step's id (saved per slot as step:<id>; never reuse one).")]
        public string fenceStepId = "back-fence";
        [Tooltip("The new last step's words.")]
        public string stepThroughFence = "Through the back fence";
        [Tooltip("The star 'sill' (kept), retitled (was \"Back on the sill\"); now earned on landing in the garden.")]
        public string sillStarTitle = "Out in the garden";
        [Tooltip("The new gold star's id (saved per slot as star:<id>; never reuse one).")]
        public string lostStarId = "lost-toy";
        [Tooltip("The new gold star's title.")]
        public string lostStarTitle = "Lost in the garden";
        [Tooltip("The garden's Room word (the new star's and step's zone).")]
        public string room = "Garden";
        [Tooltip("The Sticker Chart zone's id (also the arrival card's id and its box's).")]
        public string zoneId = "garden";
        [Tooltip("The zone's name on the chart.")]
        public string zoneTitle = "Garden";
        [Tooltip("The arrival card's big name.")]
        public string arrivalTitle = "THE GARDEN";
        [Tooltip("The arrival card's line.")]
        public string arrivalLine = "Find a way through the back wall";
        [Tooltip("The arrival card's colour.")]
        public Color arrivalColour = new Color32(0x95, 0xD5, 0xB2, 0xFF);
        [Tooltip("Where the garden core is, for 'No charge. The nearest core is ...'.")]
        public string corePlaceWords = "by the back wall";
        [Tooltip("The end card's title (was \"You made it to the window.\"). Empty = leave the card's title alone.")]
        public string endCardTitle = "You made it to the end of the garden.";

        private static BackyardSettings _loaded;

        /// <summary>Resources/BackyardSettings, or the defaults.</summary>
        public static BackyardSettings Load()
        {
            if (_loaded == null) _loaded = Resources.Load<BackyardSettings>(ResourceName);
            if (_loaded == null) _loaded = CreateInstance<BackyardSettings>();
            return _loaded;
        }
    }
}
