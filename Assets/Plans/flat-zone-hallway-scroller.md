# Flat Zones, slice 1: "World 1-1" (the hallway goes side-on)

> Small first build of the 2D-in-3D idea: **one reusable FlatZone kit + one beat** (the book stairs, the four ropes and
> the sprint gap in the hallway, seen side-on). The later beats (Press SHIFT monitor dive, Pixel Rift, wardrobe tower,
> shadow theatre, tape mural) all reuse this kit and are listed at the end, not built here.
>
> **Assumptions (the questions were not answered, so these are my recommended defaults; say if any is wrong):**
> 2.5D lock in the 3D world (not a separate 2D minigame); the retro look made by a pixel/palette full-screen pass over the
> real toy (the DKC "pre-rendered 3D" trick, no new animation); jam scope.
> **Deadline:** LEVEL_BRAINSTORM set "no new systems after 24 Nov". This slice is a new system. Every step has an off
> switch (`FlatZone.enabled` / the zone object off), so the hallway goes back to exactly what it is today.

---

# Project Overview
- **Game Title:** Shift Jam (working title; "Outgrown" proposed 4 Nov, not adopted)
- **High-Level Concept:** A toy in a kid's house shifts between toy size and kid size to find its way through rooms built once and played twice. This slice adds a second shift: **3D to 2D**. Some stretches of the house flatten into retro side-scrollers while the 3D room stays visible around them.
- **Players:** Single player (the house also runs as an online co-op sandbox; the zones are single-player in this slice)
- **Inspiration / Reference Games:** Super Mario Odyssey (8-bit wall sections), Donkey Kong Bananza / Donkey Kong Country (vines, KONG letters, pre-rendered sprite look), Super Mario Bros. (World 1-1 card, flagpole), Ratchet & Clank: Rift Apart (the shift warp the game already has)
- **Tone / Art Direction:** Cozy toy-scale world (ithappy Playroom), his pixel title and orb palette (white > cyan > blue > purple, gold rim) for anything retro
- **Target Platform:** PC (StandaloneWindows64), itch.io
- **Screen Orientation / Resolution:** Landscape, 1920x1080 reference (HUD scales by height)
- **Render Pipeline:** URP (PC_RPAsset / PC_Renderer, Forward+)

# Game Mechanics
## Core Gameplay Loop
The house loop stays the same: explore, find the problem, shift size, traverse. A **Flat Zone** adds a change of rhythm:
1. **Enter:** the toy reaches the foot of the hallway's book stairs. The camera swings out through the near wall to a side-on view, the near wall becomes a cutaway (dollhouse), the picture drops to a 16-colour pixel look, and a **"WORLD 1-1"** card pops up.
2. **Run and jump:** left/right only. The 12 book steps are a staircase climb, the four ropes are a **DKC vine run** (the swing reads perfectly from the side), and the sprint gap is a run-up-and-leap.
3. **Collect:** five floating letters **S-H-I-F-T** (a homage to DKC's K-O-N-G letters), each placed on a line only good play reaches (top of the stairs, mid-swing between ropes, the arc over the gap). They're optional. All five gives a small reward (a shift charge and a toast).
4. **Finish:** a pencil **flagpole** (SMB) on the far side of the sprint gap. Touching it slides the paper flag down, "COURSE CLEAR!", and the camera swings back behind the toy into normal 3D.

Why it works: in MDA terms it's a short burst of a different *Aesthetic* (nostalgia, mastery of one familiar verb) that breaks up a 3D tutorial. The letters give mastery players (Bartle "achievers") a reason to redo it, and the zone is literally a SHIFT: the same space, a different dimension. It costs no new geometry. The hallway is already a straight lane.

## Controls and Input Methods
Same bindings as the rest of the game (Input System, `PlayerInput`), nothing new:
- **A / D, left stick:** move along the lane (W/S and the stick's up/down do nothing inside a zone)
- **Space / pad A:** jump; on a rope, jump lets go (as now). **Shift:** sprint (needed for the gap). **Left Ctrl:** roll
- **Mouse look:** ignored inside a zone (the rail camera owns the view)
- **F (carried shift):** unchanged (the hallway already says "No room to grow")
- Movement is already relative to the camera (`PlayerMotor` lines 264–269, `RopeRider` 426–432). With a side-on camera, "right" is +X down the hallway, so the stick maps itself. The zone only removes the depth part of the input and keeps the body on the lane's plane.

# UI
```
+--------------------------------------------------------------------------------+
|  S H I F T   (letters: grey until found, orb-cyan when found)    [top left]     |
|                                                                                 |
|                    +---------------------------+                                |
|                    |       W O R L D  1-1      |  <- pops in on entry, 1.5 s,   |
|                    +---------------------------+     pixel font / his palette   |
|                                                                                 |
|   ceiling ===========================================================          |
|        |rope |rope |rope |rope                                                  |
|   _[]_[]                       (toy, side-on, pixelated)           |> flag      |
|  /books stairs\                                                   |  pencil    |
|  floor ==============        gap         ===========================          |
|                                                                                 |
|                                      [existing toasts, top middle, unchanged]  |
+--------------------------------------------------------------------------------+
```
- Letters row: a small HUD strip (Teko font, the toast style). Shown only while in a zone or for 3 s after a letter is taken.
- World card and "COURSE CLEAR!": go through the existing `Toasts` system (one new kind) or a `TextFx` pop. No new canvas.
- The existing E prompt, health and hotbar stay as they are.

# Key Asset & Context
**What exists (read, not changed unless listed):**
- `Scripts/Player/PlayerMotor.cs`: camera-relative `wish` (264–269); `Pull` for ropes; `Warp`; `UseCamera`.
- `Scripts/Player/RopeRider.cs`: its own camera-relative stick read (426–432).
- `Scripts/Camera/ShoulderCamera.cs`: `LateUpdate` (246), early-out pattern for cars `if (_chase != null) { ...VehicleView...; return; }` (263); `Warp(yaw, pitch)` (641). The rail mode copies the chase pattern.
- `Scripts/Shift/ShiftWarp.cs`: how a Full Screen Pass Renderer Feature is switched on/off from code (`feature.SetActive`). PixelShift copies this.
- `Assets/Settings/PC_Renderer.asset`: already holds the ShiftWarp full-screen feature.
- Hallway as measured today (Greybox, `Room/Course/R3_SkirtingRun`): lane z = -150 (walls z -160..-140), ceiling 14; hop gap x -32..-28.5; book stairs x -22..21.4 up onto `StepBox` (top 12.17); ropes `Rope_HallBooks/2/3/4` at x 24.98 / 30.18 / 35.38 / 40.58 (top level of the scene); floor pit (sprint gap) x 68..73.5; checkpoints `CP_afterHop` -25.25, `CP_beforeSprint` 62, `CP_afterSprint` 79.5; the near wall is one renderer, `WallS` (z -160.5, x -60..232), plus about 34 `Lost_` dressing props along both walls.
- Rules from STATUS: anything placed inside `R3_SkirtingRun` is wiped by a hallway rebuild, so **the zone goes at the TOP LEVEL** of Greybox (like the ropes). Never run Build Player Port.

**New files:**
| File | Role |
|---|---|
| `Scripts/Flat/FlatZone.cs` | The part: a box volume, a lane plane (point + normal), a rail (start/end X), options (pixel look, cutaway, card text, toy only). Checks containment each frame (so a respawn or Continue inside it works) and turns the player's lock, the camera rail, the cutaway and the pixel pass on/off |
| `Scripts/Data/FlatViewSettings.cs` + `Resources/FlatView.asset` | The camera numbers: frame height (13), field of view (18, long lens), lead ahead (2.5 body units), vertical dead zone, blend in/out seconds (0.6), pixel height (224), palette strength |
| `Scripts/Flat/FlatLetter.cs` | One letter pickup (trigger, spin and bob, the toast, a count per zone, a reward when all are found) |
| `Scripts/Flat/FlagPole.cs` | The flag slides down the pole on touch (onReached event; the zone listens and ends with "COURSE CLEAR!") |
| `Scripts/UI/FlatHud.cs` | The S-H-I-F-T strip |
| `Shaders/PixelShift.shader` + `Data/PixelShift.mat` | Full-screen pass: pixelate to N rows (224 = SNES), quantize to a palette texture (`Art/Palettes/orb_palette.png`, 16 swatches, his), optional ordered dither; `_Weight` 0..1 |
| `Scripts/Debug/FlatPieces.cs` | Editor builder, menu **Shift Jam > Flat > Land Hallway Scroller**: makes the letter, flagpole and zone pieces and places `FlatZone_Hall` at the top level of Greybox. Idempotent (deletes and rebuilds its own object), the same way `CourseBuilder` works |

**Changed files (small, each guarded so "no zone" = today's behaviour):**
- `PlayerMotor.cs`: `public FlatLock? Flat { get; set; }` (a struct: plane point, normal). In `Update` after `wish` is computed: `if (Flat.HasValue) wish = Vector3.ProjectOnPlane(wish, Flat.Value.normal);` and after `_controller.Move`, ease the body back onto the plane (`PlaneSnap`, about 10/s, capped) and remove the planar velocity along the normal. Also used in the `Pull` branch so a rope fling stays on the lane.
- `RopeRider.cs`: one line in its stick read: the same projection when `_motor.Flat.HasValue`.
- `ShoulderCamera.cs`: `public void SetRail(FlatZone zone)` / `ClearRail()`; in `LateUpdate`, after the chase early-out: `if (_rail != null) { RailView(fov, dt); return; }` (look ignored, `InsideBody = false`). `RailView`: the camera sits on the normal side of the plane at the distance that frames `frameHeight` with the rail FOV, follows X with a lead in the facing direction, clamps to the rail ends, moves vertically only past a dead zone or on landing (SMW-style), and blends from the third-person pose over `blendSeconds`. `ClearRail` blends back and calls `Warp(yaw of the lane direction)` so the 3D view comes back behind the toy.
- `PC_Renderer.asset`: a second Full Screen Pass feature "PixelShift" (inactive by default; injection after post-processing, the same as ShiftWarp).

```csharp
// Scripts/Flat/FlatZone.cs (outline)
public sealed class FlatZone : MonoBehaviour
{
    [SerializeField] FlatViewSettings settings;          // Resources/FlatView if empty
    [SerializeField] Vector3 size = new Vector3(110f, 16f, 22f);  // box, local
    [SerializeField] Transform lane;                     // its position = plane point, its forward = plane normal (towards the camera side)
    [SerializeField] float railStart, railEnd;           // world X the camera may travel
    [SerializeField] bool toyOnly = true, pixelLook = true, cutaway = true;
    [SerializeField] string card = "WORLD 1-1";
    [SerializeField] Renderer[] alwaysCut;               // e.g. WallS; plus any renderer found between lane and camera
    [SerializeField] UnityEvent onEntered, onCleared;
    public static FlatZone Current { get; }              // the zone the local player is in (one at a time)
    void Update();                                       // containment test on the local PlayerMotor; Enter()/Exit()
    void Enter(PlayerMotor m);  void Exit(PlayerMotor m); public void Clear(); // Clear = flagpole touched
}
```

# Implementation Steps
1. **Plane lock on the player.** Add `FlatLock` and `Flat` to `PlayerMotor` (project `wish`, ease onto the plane, kill normal velocity, the same in the `Pull` branch); the one-line projection in `RopeRider`. Nothing sets it yet, so nothing changes. **Dependency:** none.
2. **Rail camera.** `FlatViewSettings` (+ asset under Resources), `ShoulderCamera.SetRail/ClearRail/RailView` with the blend in and out and the `Warp` back. **Dependency:** none (can be built alongside step 1).
3. **FlatZone part.** Containment each frame; on Enter: `motor.Flat`, `camera.SetRail`, holster a drawn gun (via `WeaponHolder`'s holster path; redraw on exit), cutaway (`alwaysCut` + renderers whose bounds sit between the lane and the camera inside the box set to `ShadowsOnly`, restored on exit; only `shadowCastingMode` is touched, never materials, so `WorldBend`'s copies are safe); suspend while not in `GameMode.Play` or in first person; `toyOnly` guard; `Current` for the HUD. **Dependency:** 1, 2.
4. **PixelShift pass.** Shader (pixelate to `pixelRows`, palette quantize, dither), material, the feature on PC_Renderer, driven by FlatZone with an eased `_Weight` (feature off at 0, the ShiftWarp way). His palette as a 16x1 PNG (start from the orb palette in his title file; a placeholder made by script until he exports one). **Dependency:** 3.
5. **Nintendo dressing for slice 1.** `FlagPole` (a pencil prop as the pole, a paper/Note prop as the flag, slides on touch, CourseSounds win jingle), the "WORLD 1-1" card and "COURSE CLEAR!" through `Toasts`. **Dependency:** 3.
6. **S-H-I-F-T letters + HUD.** `FlatLetter` (his pixel letters from the title sheet as sprites on billboards, or 3D `Lettering` letters as a fallback), count per zone saved in `SaveGame` with the slot, reward on 5/5 (one shift charge via `ShiftCharge` + toast), `FlatHud` strip. First placement (to tune in play): S above the stairs (x -8, y 7), H over the box top (x 16, y 14 minus clearance), I between rope 2 and 3 at swing height (x 32.8, y 9), F past rope 4 (x 46, y 6), T at the top of the sprint gap's arc (x 70.8, y 3.5). **Dependency:** 3 (optional inside this slice; cut first if time runs short).
7. **Builder + placement.** `Debug/FlatPieces.cs`: Land Hallway Scroller places `FlatZone_Hall` at the top level of Greybox: box x -24..84, y -2..15, z -161..-139; lane point (30, 0, -150), normal -Z (camera on the south side, looking north through the cut `WallS`); rail X -14..76; `alwaysCut = WallS`; the flagpole just past `CP_afterSprint` (x 81); letters; scene saved. Through his open editor, out of Play (his rule). **Dependency:** 3–6.
8. **Docs.** DECISIONS D-0197 (Flat Zones, slice 1), STATUS entry (built / not played, his playtest list), ARCHITECTURE parts rows (FlatZone, FlatLetter, FlagPole, PixelShift, FlatView). **Dependency:** 7.

# Verification & Testing
**Compile and safety (me):**
- 0 compile errors in his editor; Greybox, Workshop, Raceway, Colour Corner and Stadium open with 0 errors (the four-scene smoke run plus the Stadium).
- With `FlatZone_Hall` switched off: the hallway plays exactly as before (motor, ropes and camera untouched when `Flat` is null and there's no rail).

**A hidden probe (kept outside the project, like the others), measured:**
- Walk in from `CP_afterHop`: the camera is on the rail within `blendSeconds`; its forward is within 1 degree of +Z; frame height 13 ± 0.5 at the lane.
- Hold W or S for 2 s inside: the body moves less than 0.05 off the lane plane. Hold D: it runs along +X.
- Climb the 12 books to the box top (feet 12.17); catch rope 1 by walking off; rope to rope ×4 with the body within 0.1 of the plane all the way; fling, land, sprint and clear the 5.5 gap.
- Fall in the pit: Respawn at `CP_beforeSprint`, and the zone is still on (containment, not trigger events).
- Touch the flag: "COURSE CLEAR!", the camera is back in third person behind the toy (yaw within 10 degrees of +X), `motor.Flat` is null, the pixel pass weight is 0 and the feature is off, `WallS` casts and draws again.
- Draw a gun before entering: holstered inside, back on exit. V inside: first person works and the lock is off; V back out: the lock is back on.
- Letters: each one counts once, 5/5 gives one charge, and the count survives Continue.

**His playtest (Greybox, New Game or Continue at the hallway):**
1. Walk from the hop to the stairs: does the swing out to side-on feel good (0.6 s)? Does the cutaway read as a dollhouse?
2. Climb the stairs and do the four ropes side-on. Does the vine run feel like DKC? Is the lead-ahead right?
3. Sprint over the gap, touch the flag. Is the swing back to 3D clean?
4. The pixel look: 224 rows and 16 colours. Too crunchy or not enough (`FlatView` > pixel rows, palette strength)?
5. Find all five letters. Are any of them unfair?
6. F12 on anything odd.

**Edge cases to watch:** the glider opened mid-air inside a zone (heading drift: the plane snap holds the position); a knock or a grenade shove along Z (killed by the lock); the toy soldier's darts (it stands outside the zone); a party (zones act only on the local player's own motor and camera; online is untested in this slice).

---

## After slice 1 (not in this plan; each reuses FlatZone)
| Beat | Nintendo homage | New on top of the kit |
|---|---|---|
| **Press SHIFT**: stomp the keyboard's Shift key, the warp pulls the toy into the monitor, out through another screen | Odyssey's pipe-into-wall, the WarioWare/Game & Watch screen | Zone on the monitor's plane, the ShiftWarp tear as the transition, Portal to the exit screen |
| **Pixel Rift**: a 2D run inside a stalled shift | Mario Odyssey 8-bit, Kirby's dimension rifts | ShiftWarp frozen mid-shift as the frame, orb palette |
| **Wardrobe tower**: a vertical scroller with balls rolling down the shelves | Donkey Kong (1981) girders and barrels | A vertical rail (Y), a ball spawner |
| **Shadow theatre**: the toy runs on the shadows of toys the kid set up | Link's Awakening side-view rooms, Kirby's shadow worlds | Shadow-slot colliders, the flashlight as the projector |
| **Tape mural**: a doodle path wrapped round the playroom walls | Odyssey 8-bit wrapping round buildings, Yoshi's crafted look | A rail that turns corners (spline), his hand-drawn sprite |
