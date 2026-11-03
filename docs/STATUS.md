# STATUS — Shift Jam

Truth for what is BUILT vs DESIGNED. Newest entry first. Versions log at the end.

## 3 Nov 2026 10:4x — Room 2 built through the bridge (his "proceed on room 2"; he drawing), saved, UNPLAYED
**Landed + compiled (outside csc 0 errors, then his editor):** `Scripts/FX/Billboard.cs`, `Scripts/Camera/ShadowReach.cs`,
`Scripts/Room/Locked.cs`, `ShiftWarp.cs` (growMasks/shrinkMasks lists), `ShiftSource.cs` (wrongSizePrompt).
**Placed by RunCommand (renderer bounds, saved):** bed (east wall, z 0), bedside table + night lamp (south end),
StepBox + three books on the bed's west side at z 12, ShiftCore2 on the mattress (47, 22, -39) ShrinkOnly;
shelf (west wall, z -40) + Switch_01 above it (y 56, Kid only, opens the toy door); Window_01 (north, y 55);
nine Ceiling_01 at y 130; Planet_Lamp_01 + RoomLight point (y 78, range 340, int 3.5, soft shadows);
RealDoorway + RealDoor (south, x -55, Locked); ToyDoorway + ToyDoor at 0.035 (south, x -15, Door openAngle
-100 into the room, 0.8 s), DoorGlow quad (URP Unlit HDR warm) behind it, DoorLight (off, on at Door.onOpened
via a persistent listener), Goal trigger in the frame (Toy, label = GoalText). Both cores got a Glow quad
(OrbGlow.mat = Particles/Unlit additive + orb_glow_mockup, Billboard) and a Sparks particle system (Spark.mat
+ spark_mockup). Player: ShadowReach; ShiftWarp mask lists = warp_mask_mockup. Box core = GrowOnly,
"Too big to hold". Verified by a 4-angle scene capture (layout right) and raycast probes (mattress ~20, box 19.2).
**His six checks in Play:** (1) wake, hop, E in the box → burst; (2) kid: "Too big to hold" on the box core,
"Locked" on the real door; (3) switch "Press" → toy door swings open, light comes on; (4) jump onto the bed,
E on the second core → shrink, ShiftWarp with the mask; (5) hop box → books → floor → walk into the toy door
→ GoalText; (6) shadows still drawn at kid size (ShadowReach). **Open:** mattress edge overhangs the box by
~2 u (a toy walking off lands on the bed rail, then the floor; a hop clears it); window/doorway fronts are a
guess (modules face +z; rotate 180 if the frame trim is on the wrong side); glow sprites are mockups until his
drawings replace the three PNGs; Feel layer still his. `Assets/ERP` + `.erp` (Editor Rich Presence, MIT,
his install) left uncommitted: say if it should be tracked or ignored.

## 3 Nov 2026 07:0x — Rift Apart warp built and wired (his "proceed"; he AFK), UNPLAYED
**Landed + compiled (outside csc, then his editor: ShiftWarp in Library dll, shader HasError=false):**
`Shaders/ShiftWarp.shader`, `Scripts/Shift/ShiftWarp.cs`, `ShoulderCamera.FovOffset`, `Shifter.Progress`.
**Wired by RunCommand (saved):** `_Game/Data/ShiftWarp.mat`; Full Screen Pass Renderer Feature "ShiftWarp" on
`Assets/Settings/PC_Renderer.asset` (the live renderer; injection After Rendering Post Processing, fetch colour,
inactive until a shift; Mobile_Renderer has none); ShiftWarp on the Player with material + feature;
ShiftSettings.duration 0.6. Material defaults: push 0.12, ring 0.22, fringe 1, flash 0.8 cyan-white, mask off.
**His drawing hook:** a square greyscale PNG (1024², black background, the tear in white, centred) → import
(sRGB off, Alpha none) → ShiftWarp.mat Rift Mask + Mask Strength 1. The drawing is sampled in a square the
size of the ring, so it expands with it. **Still his:** the Feel layer (Timescale, Camera Shake, URP Lens
Distortion / Chromatic Aberration / Vignette on a Global Volume, fired from ShiftSource.On Used). D-0010.

## 3 Nov 2026 00:2x — bedroom shell placed by code, build settings, commit (he AFK)
**Done (RunCommand, saved):** `Room/Bedroom`: nine Floor_01 tiles (centres -65/0/65), walls 130+65 per side centred
at ±100 (inner faces at ±97.5 = the floor edge), four Wall_05 corner pillars; every piece slid by its renderer
bounds, so the edges meet exactly (bedroom bounds ±102.5, height 130). No window, no ceiling yet (Wall_02/03/04
may carry different cut-outs: his eye picks which goes where). Build Settings = Greybox only. Committed.
**Next:** his Play of the box; window + ceiling + bedroom beats (room 2 design); the Rift Apart warp.

## 3 Nov 2026 00:1x — toy box dressed through the bridge (his ask, he AFK with remote control), saved, UNPLAYED
**Done in his open editor (out of Play, RunCommand):** Flashlight_01 (-9, 3.3, 3) rot Y 330 bell toward the back-right,
FlashlightBeam point light (-5.5, 3.5, -3) range 40 int 2 warm soft shadows; Constructor_01 (0,1,0) and _02 (4,1,-4)
resting on the floor; ShiftCore = Wooden_Sphere_01 at (10, 3.5, -8) (Ø5, rests on the floor) with new emissive
`_Game/Data/ShiftCore.mat` (URP Lit, cyan ×3) + ShiftSource Toggle; BoxBurst on ToyBox (5 panels + Lid wired);
Player: WakeUp + Respawn added, Shifter devToggleKey None, startSize Toy, world (0, 1.5, -12) rot Y 180;
Directional Light 0.2; HUD canvas (ScaleWithScreenSize 1920×1080) with PromptText (bottom) + GoalText (centre),
InteractPrompt wired, EventSystem (Input System module). `MovementSettings.jumpHeight` 1.2 → 1.6: the Constructor
brick is 1.28 high. Scene + assets saved. Console: only Fast Script Reload's own welcome-dialog exception
(button text > 64 chars, fires on every domain reload, not ours). **Open:** books are 2.0 thick vs a 1.6 jump, so
"one book = one toy jump" (D-0008) needs jumpHeight ≈ 2.2 or the books half-sunk; his feel call. Bedroom floor
tiles still not placed (nothing outside the box to land on after the burst: Respawn catches it at y -20).
**Next:** his Play of the box (get-up, hop, E, burst), then the nine Floor_01 tiles + walls, then the Rift Apart warp.

## 2 Nov 2026 23:3x — D-0009 toy box awakening: four scripts + Wake state landed (he in the editor, out of Play)
**Landed, compiled outside (0 errors), not yet seen in his editor:** `Scripts/Room/BoxBurst.cs`, `Scripts/Room/Goal.cs`,
`Scripts/Player/Respawn.cs`, `Scripts/Player/WakeUp.cs`; `PlayerAnimator.controller` v6 = v5 + a `Wake` state
(Death_1_(idle) at speed -1.5, no transitions, reached by WakeUp.Begin). The pack's Death_1_(idle).anim got
Bake Into Pose Y + XZ (backup in the scratchpad). **His editor side next:** ToyBox empty + six cube panels
(BoxBurst on the empty: Panels = the five + Lid slot), flashlight + point light, two bricks, the shift core
with ShiftSource; Player gets WakeUp + Respawn. Then Play: get up, walk, hop, E, the box bursts.
**Design:** DESIGN.md now has Room 1 (box) and Room 2 (bedroom, beats pending). D-0008 scale 1:35 recorded.

## 2 Nov 2026 23:0x — committed `0cc03df`; room measured; STEP 3 walkthrough given (he builds in the editor)
**Measured (headless Blender on the FBXs + the pack's Demonstration scene):** Playroom is built at 1 unit ≈ 2 cm:
floor tile 65×65, wall modules 65 / 130 long × 130 high × 5 thick (Wall_05 = 5×5 corner pillar), ceiling y 130,
Bed_01 62×109×40 high, Shelf_01 62 wide × 47.5 high, Book 12×17×2 thick, Ball_01 Ø16.5, Castle_Wall 17×9 high,
Cardboard_Box 21×19×24, Door_01 42×100 (pivot mid-height), Doorway 49×104. The pack's own Character_01 has a
1.99 u controller; Funny Characters Basic_Character is 1.85 u tall = his Player at scale 1 IS the pack's toy
(≈4 cm real, a minifig). **Proposed D-0008 (his call, pending):** toy = 1, kid = 35 (a 1.3 m child), not 1:8 with the
room scaled down: at 1:35 one book = one toy jump, the bed (40 u) = one kid jump and 33 toy jumps, the shelf top
(47.5) = kid chest, the castle walls (9 u) = 5 toy heights of landscape and a kid's step-over (stepOffset 0.3×35).
At 1:8 every toy-landscape prop becomes a pebble. Goal.cs staged in the scratchpad (compiled), lands on his "out".
Rift Apart shift look planned (Feel URP feedbacks + a fullscreen ripple shader of mine), after the room.

## 2 Nov 2026 21:5x — his play: "looks decent", idle jump "still awkward, a pose thing" (Unity OPEN on Shift Jam, in Play: nothing landed)
**Read from the clips (humanoid muscle curves):** the Jump idle start clip squats to its deepest at 0.46–0.54 s (my
offset 0.3 entered it exactly there, so the figure squats in mid-air), launches at ~0.71–0.79 s, and ends in a
tight tuck (chest −0.71, legs tucked, arms up) which "Jump idle middle" then HOLDS as a static pose; the idle end
clip spends its first 0.3 s still falling (ground contact ≈0.44 s), so at offset 0 the figure "falls" while standing
on the floor. The run-jump clips have none of this: immediate launch, a gentle cycling air pose. **Prepared in the
session scratchpad, NOT landed:** `PlayerAnimator.v3.controller` = his branches kept, offsets moved to the launch
and contact frames (idle start 0.5, idle end 0.3, run end 0.65, end speeds 1.5); `PlayerAnimator.v4.controller`
(recommended) = every jump and fall uses the run-jump clips, idle clips left in the file unused. His pick: v4 LANDED 22:3x
("proceed"; previous controller backed up in the scratchpad). Unplayed. Fingers: the rig HAS finger bones (Thumb/Index/Middle/Ring/Pinky ×2 joints, both
hands) and the clips animate them; no fist pose in the pack (Fighting_stance fingers only slightly curled).

## 2 Nov 2026 21:15 — animator retimed, motor steadied (files only, Unity closed, he in AoE4)
**His report:** "the animations looked off, it was stuttering, jittery"; wants buttery Mario-64-like third person.
He had not yet played the 16:1x threshold fix (scene and data assets last saved ~13:00).
**Landed (compiled outside with Unity's csc, 0 errors, NOT yet opened in Unity):** `PlayerAnimator.controller`
regenerated (D-0006: clip speeds 1, jump vs fall split on VerticalVelocity, start clips offset + fast, moving
landings blend to Locomotion, end clips cut by input, direct transitions, Solo off, his layout kept; backup in
the session scratchpad `backup/`); `PlayerMotor` sends a steadied Grounded (`groundedGrace`) and cuts a jump
short on early release (`jumpCutMultiplier`); `PlayerInput.JumpHeld`; two new MovementSettings fields (the
asset takes the script defaults 0.1 / 0.5 until he saves it).
**Not done / his to check in Play:** foot slide at walk/sprint = tune walkSpeed/sprintSpeed on the asset to
the clips (unknown authored speed); the start-clip offsets (0.3 idle, 0.2 run) are a guess at where the
launch frame sits, nudge in the controller if the character still crouches after take-off. Still uncommitted
since 11:5x (scripts, docs, his scene, data assets, controller).
**Next:** he opens Unity (import, ~20 s compile), plays walk / sprint / jump standing / jump running / walk off
the floor edge / tap vs hold jump, reports; then commit; then STEP 3 room + shift beat.

## 2 Nov 2026 16:00 — his STEP 1 + animator, examined (Unity closed)
**Built by him (uncommitted):** Greybox.unity has Player (Basic_Character instance, CharacterController 1.8/0.3,
PlayerInput + PlayerMotor + Shifter + Interactor wired to the three settings assets), Main Camera with
ShoulderCamera wired, a 20 m TestFloor; `_Game/Data` holds MovementSettings, CameraSettings, ShiftSettings at
defaults; `_Game/Animation/PlayerAnimator.controller` = Locomotion blend tree (Idle/Walk/Run) + Jump idle and
Jump run start/middle/end on Grounded. Scripts compiled in his editor at 11:49, 0 errors; the only exceptions in
the log are Unity's own animator-transition inspector, not ours.
**Found:** blend tree thresholds are 0 / 0.1 / 6 but the motor sends Speed 0..1 (walk 0.5, run 1): Run never
plays. No ShiftSource or prompt canvas yet, so the shift could not be triggered: Shifter now has an
editor-only T key toggle (devToggleKey). Build settings list SampleScene, not Greybox. `New Terrain.asset`
still at the Assets root.
**16:1x, controller fixed in the .controller file (Unity closed, he in AoE4):** thresholds 0/0.5/1 (max 1); the
Locomotion transition that pointed at NO state now goes to Jump run start (Grounded false, Speed > 0.1) and a new
one goes to Jump idle start (Grounded false, Speed < 0.1); Jump run middle -> end lands at once (exit time off);
run start -> middle at 0.6; idle end / run end return to Locomotion at 0.6 / 0.3. Backup of his version in the
session scratchpad. **Next:** he opens Unity, plays walk/sprint/jump/T, reports feel; then STEP 3.

## 2 Nov 2026 11:5x — scripts landed, nothing wired yet
**Built (files, compiled clean outside the editor, 0 errors, not yet run in Unity):**
`Assets/_Game/Scripts/` — Data/MovementSettings, CameraSettings, ShiftSettings; Player/PlayerInput,
PlayerMotor; Camera/ShoulderCamera; Interaction/IInteractable, Interactor; Shift/Shifter, ShiftSource,
Switch, Door; UI/InteractPrompt; Core/GameEvents. Docs: DESIGN, ARCHITECTURE, DECISIONS, this file.
**Designed, not built:** the three settings assets, the Player prefab, the animator controller, the
tutorial room in Greybox.unity, Feel hooks on ShiftSource/Switch/Door, a Core scene, portals.
**Repo:** `fcc8c84` reorganisation committed; scripts + docs uncommitted. Remote = his push.
**Next:** STEP 1 (settings assets + Player prefab + camera + prompt, playable walk/jump) → STEP 2 animator
→ STEP 3 room greybox + shift beat → feel pass → commit → v1.

## Versions
- (none yet) v1 = first playable tutorial room.
