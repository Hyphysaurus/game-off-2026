# STATUS — Shift Jam

Truth for what is BUILT vs DESIGNED. Newest entry first. Versions log at the end.

## 6 Nov 2026 12:3x — D-0038 BUILT + STAGED with D-0037; WAITING for him back in Shift Jam to land (he is playtesting NectarRush; Shift Jam's editor is closed) — CHECKPOINT
His "proceed": crash = impact speed into the surface; bump stop; ramp lead-ins; repair hearts, life-ring shields,
mending after 4 s, bonnet smoke, HUD health + shield. All staged in `shiftjam-stage/race/*` (+ integ: Damageable.Blocked,
KeyHelp, CourseBuilder), 29 files compile 0/0. The review was paused at his word (tokens). LAND ORDER (out of Play, Shift
Jam open): copy all (NitroPickup.cs is gone, CarPickup/CarShield new), compile; UI > Make HUD Fonts; Workshop > Build
Combat & Vehicle Pieces; Build Boost Pieces (9 incl. RepairHeart, ShieldRing); Build Race Pieces; Build Traversal Pieces
(ramp lead-ins in ClimbRamp/LandingRamp/KickerSmall, PillowPad glow); Build Catalog; open Greybox beside Workshop: Wire Car
Boost Parts + Add Ramp Lead-ins; place the race loop, boost lane, hearts, rings, cannon, ToyRacer04 in the Workshop (check
clashes); save both; stills; commit. NOT PLAYED.

## 6 Nov 2026 11:3x — BOOSTS / HUD / RACE: 5 builders DONE, integrated, compiles (0/0), UNDER REVIEW — CHECKPOINT
Workflow wf_2309ad83-1f5 done (38 min, 2.1M tokens): staged in `shiftjam-stage/race/{contract,cars,pieces,carsandcannon,
hud,race_loop}` + my `integ/` (KeyHelp: nitro, wind-up, RACE lines; CourseBuilder: wooden ToyCars get boost parts).
Integration edits: Driving.Quitting + CarSeat/ToyCar skip the reparent while the car is switched off; ToyCar.Gravity read by
CarJumpPad/CarCannon (no copied 25); boost pads silent (the car's own whoosh plays once); wind sound Safe_Dial_v1 on all cars
(his ear); menu "Build Racing Pieces" renamed "Build Boost Pieces"; WorkshopPieces "Wire Car Boost Parts (open scenes)".
LAND ORDER (out of Play): copy all, compile; UI > Make HUD Fonts; Workshop > Build Combat & Vehicle Pieces; Build Boost
Pieces; Build Race Pieces; Build Catalog; Wire Car Boost Parts with Greybox + Workshop open; place the race loop + boost
lane + cannon + ToyRacer04 in the Workshop (builders' placement notes); save; stills; commit.

## 6 Nov 2026 11:0x — PAD GLOW LANDED (he stopped Play), UNSEEN by him; boosts / HUD / race still building
Copied in, compiled in the editor, Refit Pad Glows run with Greybox open beside Workshop: Greybox 3 + Workshop 1 + the
PillowPad piece now use PadGlow.mat (warm soft disc on the crown, 0.65 of the narrower side, no shadows), both scenes
saved. Stills (Temp\shiftjam-stage\stills3): from above a soft warm spot on the pillow, from the side a faint line on
its top edge; nothing floats. Console notes: CarSeat.OnDisable -> GetOut -> SetParent is refused when the car is being
switched off (seen at Play exit while seated in CyberCar07; harmless there, guard it with the cars integration); Fast
Script Reload's auto-refresh dialog throws on every reload (button text over Unity 6.3's 64 characters): noise, his call.

## 6 Nov 2026 10:5x — STAGED: pillow pad glow fix (LANDED 11:0x, above) + boosts / HUD / race (his "proceed with all of those") — CHECKPOINT
**Pad glow (his "floating square above it glowing"):** `CourseBuilder.PillowPad` made the pad mark with DoorGlow.mat (the
door strips' opaque, HDR, untextured material) on a quad 0.9 of the pillow wide, 0.3 over its crown: a hard glowing
square in the air. Staged fix (compiles, 0 errors): `shiftjam-stage/padglow/Debug/CourseBuilder.cs` (Windows Temp):
`FitPadGlow` = PadGlow.mat (a copy of OrbGlow.mat: the cores' soft round mote, additive) tinted warm, flat on the crown
(+0.05), 0.65 of the narrower side, no shadows; menu **Shift Jam > Refit Pad Glows** re-fits every PadGlow in the open
scenes + the PillowPad piece. To land after he stops Play: copy in, compile, open Greybox (3: both couch pillows + the
shelf Trampoline) and Workshop (1), run the menu, save both, still of a pillow, commit.
**Boosts, HUD, race (DESIGN §3/§4):** contracts in `shiftjam-stage/race/contract` (IBoostable, Driving, RaceState,
Garage.MenuOpen); workflow `boosts-hud-race` (run wf_2309ad83-1f5) has five builders staging into
`shiftjam-stage/race/{cars,pieces,carsandcannon,hud,race_loop}`: nitro on Shift + bursts + wind-up launch + FX on both
cars; boost arrow/arch/ring, jump pad, bounce tramp, fan pad, nitro star; Battle Stadium racers 1-10 + the cannon; the
driving HUD (Teko/Fredoka OFL fonts, Kenney Shift key); race gates, finish, TV, podium. Nothing in Assets yet. If the
run dies: read its journal.jsonl, keep what compiled, resume with the same script.

## 6 Nov 2026 10:1x — LANDED `e8a046f` (he stopped Play): the 44-file set, builders re-run, UNPLAYED
Copied, compiled in the editor (0 errors), Build Combat & Vehicle Pieces (24), Dev > Build Pocket Core, Dev > Create
Vehicle Camera Settings, Build Catalog (509, Interactables 4), ThirdPersonGrab on the Player in Workshop + Greybox, both
scenes saved. Stills: the soldier stands (rifleman, muzzle), the Pocket Core glows amber. Left uncommitted on purpose:
ShiftWarp.mat and TMP's LiberationSans fallback atlas (both rewritten by Play).
HIS PLAYTEST (Workshop): H (key list); F2 (Pocket Core) and E on it; as the kid aim at the couch pillow + E (grab), wheel,
hold R, E to set down on the footprint, click to throw; drive a Cyber car (camera frames the car, swings behind at speed,
blends back on getting out; wooden car hides the toy); in the garage right-drag / A D / wheel orbit; F3 while seated
(the car is not selectable, status line says so); console open + Build keys (nothing happens); the soldier walks back to
where you move it; F5 / falling in first person respawns; knife held by the handle; shotgun on the soldier.

## 6 Nov 2026 09:1x — STAGED, NOT LANDED: sweep fixes + Pocket Core + key list + grab + vehicle camera — CHECKPOINT (landed 10:1x, see above)
Everything below sits in `%TEMP%\shiftjam-stage\landing\` at its Assets/_Game/Scripts path (35 .cs: 30 changed, 5 new),
compiled outside Unity together (0 errors). Nothing is in Assets yet. 44 .cs now (34 changed, 10 new) with the grab and the vehicle camera.
- SWEEP of the unplayed Workshop systems (4 agents, 30 findings, 29 confirmed): fixed by three fixers on separate files +
  my review of every diff. Course-wide: the kill-height net now fires (Respawn guard). Build Mode: the car you sit in is
  off-limits and undo/load/Start empty/Play-as eject you first (was a softlock), the console stops Build Mode's keys and
  clicks, moved pieces take their new spot as home (IBuildMoved: soldier, cars, wooden car, pillow pad), the wooden car
  moves/turns/scales in Build Mode, the pillow scales, F3 mid-shift no longer freezes the tear, re-baking keeps earlier
  bakes. First person: warps stick (PlayerMotor.Warped), hit kick + red flash + knockback, pads/fans/checkpoints/hazards
  work (PlayerTouch), no grabbing the kid's carried things or the wooden car's ball, SMG waits after a throw, held prop
  drops on a shift. Combat: knife held by the handle, soldier = standing rifleman Army_Soldier_06 with a muzzle, every
  pellet/dart counts (no i-frames on targets, soldier, cars), toppled targets lose their collider, soldier aims at the
  kid's legs. Console: F1 readout hidden in Build, fly only on foot.
- NEW: Pocket Core (D-0032 to write: F2 / console `core` calls a warm-glow Toggle core a step ahead; Build Mode piece);
  KeyHelp (H or pad View: every key by situation, player actions read from the project-wide input actions).
- LANDING STEPS (when he is back, out of Play): copy landing/ into Assets/_Game/Scripts → Refresh → Shift Jam > Workshop >
  Build Combat & Vehicle Pieces (knife, soldier, i-frames) → Shift Jam > Dev > Build Pocket Core → Shift Jam > Workshop >
  Build Catalog → check the Workshop soldier stands at its NE spot → save → commit.
- GRAB (D-0033) staged: SpringHold shared with first person, ThirdPersonGrab (E aim-grab, wheel, R turn, E/C set down on
  a footprint, click throw), GrabSettle, Carryable.KeepInHand, Interactor/InteractPrompt/WeaponHolder wired; the key list
  shows it. Landing extra: add ThirdPersonGrab to the Player in both scenes so its values are tunable (it is auto-added
  at run time either way). HIS PLAYTEST then: kid grabs a couch pillow, sets it under the sofa arm, shifts (F2 Pocket
  Core), drops onto it as the toy.
- VEHICLE CAMERA (D-0036) staged: vehicle mode in ShoulderCamera, garage orbit (right drag / A D / wheel), IChaseTarget on
  both car types, wooden car hides its driver. Landing extra: Shift Jam > Dev > Create Vehicle Camera Settings (makes
  Resources/VehicleCamera.asset; built-in defaults work without it). His 09:0x live garage look: works (Car 01, Wheel 03,
  part rows right); the stale "E  Drive" prompt under the menu is the sweep's Interactor fix (staged).

## 6 Nov 2026 06:1x — GARAGE + RAMPS LANDED (D-0029/30/31), reviewed before landing, COMMITTED, UNPLAYED
Last night's checkpoint list is done. Commits: `22e4dd3` (checkpoint), `bf7b714` (placed ramps refit), `6c32ccd`
(garage + fixes). A six-agent review of the staged garage (three readers, three skeptics) confirmed 17 defects before
anything reached his editor, all fixed in the landing (D-0030): F3 while docked froze input + camera; E could not get
out; pack wheel sets would have mounted sideways; Car 03 had no wheels (pack's bare shell); the "arch" was a flat strip
fitted into a 500 km plane; Build Mode would have lifted sunk ramp toes back up (D-0031); plus smaller ones. Stills
checked: garage gate + above, a car on pack wheels (Temp/shiftjam-stage/stills1, not kept).
- RAMPS: all seven placed ramps flush at the toe, tops unchanged (Workshop Kicker 4.0, ClimbRamp/LandingRamp 12.2;
  stunt run climb 12.2, deck kicker 3.0, landing 12.172); RestSink keeps it in Build Mode.
- WORKSHOP: Garage at (120, 0, -200), exit (+Z) facing the three Cyber cars; all 15 Cyber cars + Garage in the Build Mode
  catalogue (508 entries, Vehicles 16). The stray "SHIFT / press any key" card: fixed at its cause (GameFlow).
- HIS PLAYTEST (Workshop): (1) walk the toy up the Kicker and the climb/landing ramps (no step at the toe); (2) drive a
  Cyber car slowly into the GARAGE gate: menu opens, car repaired; try Body < > (stays seated), Wheels, a few parts;
  Enter drives out; dock again and press E to get out; (3) F3 while docked, then F3 back: car + controls normal;
  (4) wreck a car (hard crashes) and wait: it is towed into the bay; (5) F3, place a ClimbRamp from the catalogue:
  green ghost, no step at the toe; (6) no start card on entering the Workshop. Main room: drive the stunt run, the
  landing ramp's toe should no longer bump.
- STILL OPEN (his calls): pillows too tall for the toy to get onto: measured 10.8 tall vs a 2.0 jump, so a ledge grab
  cannot reach (corrected 6 Nov); options in DESIGN "Proposals, 6 Nov" (A bouncy sides = my pick, B pack trampoline
  step, C kid-set steps); car location portals (plan in the same section)
  (his direction, D-0029, not built); run cadence A/B; the 4 Nov design-pass picks; idle-jump ending; run legs inward;
  Feel on the shift; Toy Land removal; `Assets/ERP` + `.erp` (his Editor Rich Presence) untracked.

## 5 Nov 2026 21:1x — RAMP LIP + GARAGE (D-0029), staged/written, waiting for him to stop Play — CHECKPOINT
- His playtest notes (Workshop, live): stray "SHIFT / press any key" card on screen (the HUD's StartCard came over with the
  HUD; hidden live, to switch off in Workshop.unity); can't get onto ramps (0.6 lip at the toe, fix in CourseBuilder.Wedge,
  landed); wrecked his car → GARAGE (staged in %TEMP%/shiftjam-stage/garage: Garage.cs, CarLook.cs, ArcadeCar.cs,
  CarSeat.cs, WorkshopPieces.cs; compiles outside); car + shift warp → his call: location portals for cars (not built);
  pillows too tall to climb → options given (ledge grab recommended; pack has 6 Trampolines), awaiting his pick.
- AFTER HE STOPS PLAY: land staged files → Refresh → Build Combat & Vehicle Pieces (15 cars + Garage) → Make traversal
  pieces → fix placed Ramp_02 instances in Workshop + Greybox course → StartCard off → place a Garage in the Workshop →
  Build Catalog → save scenes → stills → commit.

## 5 Nov 2026 19:2x — WORK ORDER DONE (his "proceed with all", he AFK): Workshop systems landed (D-0028), UNPLAYED
Landed + wired + committed `d6a3859`, `722b2bc`, `de46794`: Build Mode (F3), first person (F4), weapons, practice
targets, toy soldier, Cyber cars, traversal pieces (menu Shift Jam > Workshop > Build Traversal Pieces), F9 reset; the
Workshop has a starter set of each; catalogue built (495 entries; Art/Thumbs + Resources/BuildCatalog.asset are
git-ignored renders of the packs: rebuild via Build Catalog). Fixes on the way: ShoulderCamera.OnEnable re-locks the
cursor; Build Mode waits a frame when entered straight from first person. Console: the "less than 64 characters" red
error = Fast Script Reload's welcome popup (harmless); FSR's hot-patch rewriter breaks `&& x.TryGetComponent(out T v)`
(BoxBurst rewritten). HIS PLAYTEST, in order: (1) course: drive the stunt run (start z -216 in R4), bounce a couch
pillow as toy, walk the keyboard; (2) title menu → Workshop: F3 build (place, move, undo, save a layout), Play as toy,
F4 first person (pick up + throw a box), take a dart pistol at the stands (west wall) and shoot the practice targets,
the soldier (NE), drive a Cyber car (south); (3) feet sinking → F12 on the spot. Doubts to watch: knife/foam finger may
sit backwards in the hand (fix handEuler/viewEuler on Prefabs/Weapons), soldier facing, car springs untuned, HL2 speeds
at toy size untuned (settings assets in Data/). Toy Land stays until he checks the ports.

## 5 Nov 2026 19:0x — PROGRESS on the work order (he AFK) — CHECKPOINT
DONE + COMMITTED: keyboard walk-off fix `d7ee9b8`; pillow pads in the scene `bc5dfda`; CAR (D-0026) `477a37e`: body on a
fixed offset, turns slow/stopped, air steer, toy gravity 25, chase camera (`ShoulderCamera.SetChase`), and the STUNT RUN
in R4 (corridor x 505..535: start z -216 → 20° climb → 2x2 box deck 12.2 → 17° kicker → 20-unit gap → landing ramp to
z -82; nothing removed, 11 small props moved aside; stills docs/stills/car_course_*.png); WORKSHOP scene (D-0027)
`b9c18f4`: title-menu button + console `workshop`/`course`; footing HUD line `25321af` (sunk / toe gaps, written into
every F12 note) for the sinking-feet report — HIS STEP: when feet sink, press F12 there.
RUNNING (staged outside Assets, landed by me after review): agent A = Build Mode; agent B = first person + weapons +
targets/soldier + arcade cars. NOT PLAYED: everything above.

## 5 Nov 2026 18:5x — WORK ORDER (his "proceed with all", he AFK, checks in by remote) — CHECKPOINT
Fixed + committed `d7ee9b8`: the keyboard walking off the desk = KeyboardKeys' click AudioSource sat on the keyboard
itself and each press moved it to the pressed key (measured live: 34.5 up, ~35 across; back in place after Stop).
Grounding report (feet sink on cardboard + foam mats): surfaces measured = colliders match the visible tops (mats flat
0.73, box lids within 0.3); kid's feet clear (toes 1.6 above floor). Toy-size measure still to do.
ORDER, each committed separately: (1) car fix (body follow, slow turning, chase cam, hop gravity, parking) + a CAR
COURSE in R4 with verticality and air time; pillow pads applied to the scene; (2) Workshop scene (own scene, title
menu button) ; (3) Build Mode (Two Point Museum / Planet Zoo: RTS camera, catalogue, ghost placement, move/rotate/
scale/duplicate/delete, undo, shape pieces, save/load layouts, test-as toy/kid/FP) — subagent A, staged outside
Assets; (4) First person (Half-Life 2 body + physics pickup/throw) + Toy Land ports (sword, blaster, projectiles,
damage, targets, defender, arcade car/seat) — subagent B, staged; (5) integrate + palette pieces; (6) Toy Land
removal = HIS call after checking. Shared contract: `Core/GameMode.cs` (Play/Build/FirstPerson; F3 build, F4 FP).

## 5 Nov 2026 18:2x — PILLOW PADS + KEYBOARD POP (D-0025), files landed, COMMITTED, UNPLAYED
After his recorded playtest (OBS, `Videos/Shift Jam/2026-11-05 13-37-06.mp4`, 21 min): couch pillows are now carryable
(kid) toy launch pads; keyboard keys hold while feet are on them (the pop on every key). HIS STEPS: let Unity compile,
Shift Jam > Make Pillows Pads, save the scene, Play: bounce on a couch pillow as the toy, carry one as the kid and put it
down, bounce again; walk the keyboard (notes, no hop). If the keyboard still pops: MotionProbe while he walks it.

## 5 Nov 2026 13:1x — PINK PUFFS in the toy box fixed (his screenshot), files landed while he plays, UNSEEN
Root cause: the pack's `FX_Dust.prefab` points at a material the pack does not ship (guid 3feca5f0… found nowhere), so
each wall-landing puff from BoxBurst drew in error magenta. Fix in OUR code, pack untouched: `Resources/FX/BoxDust.mat`
(URP particle shader as Spark.mat, pack FX_Smoke.png, alpha blend, tan) put on every puff renderer by BoxBurst; optional
`dustMaterial` slot overrides it. Compiled outside (0 errors). The new slot arrives on Stop (Fast Script Reload).
Any other FX_Dust use elsewhere would show the same pink: only BoxBurst spawns it today.

## 5 Nov 2026 13:0x — HIS TITLE LETTERING IN the title menu (files only; Unity was open on NectarRush), COMMITTED, UNSEEN
His two Procreate drawings (iCloudDrive `shift_v2_instagram.jpg` wide, `Shift_Title.png` stacked; originals untouched)
converted for a dark menu: paper outside the letters made transparent, letter insides kept WHITE (ink-only would vanish
on the dimmed screen), pen specks dropped, cropped; the stacked arrow's open tip sealed for the fill. Landed as sprites in
`Assets/_Game/Resources/Titles/` (no mips, no NPOT scaling). `TitleMenu` loads them itself (it is spawned by code, so an
Inspector slot could never be filled): wide above the buttons on landscape screens; stacked on the left with the buttons
shifted right when the window is under 1.2:1. Compiled outside (0 errors). The HUD StartCard still says "SHIFT" in text
(hidden whenever the menu runs). Not yet seen in the editor: check size/placement on first open.

## 5 Nov 2026 09:2x — docs caught up: D-0023 (playroom reveal) and D-0024 (toy box hinges) BUILT and COMMITTED (`4796de8` 00:53, `5bfc44d` 01:24), UNPLAYED
The entry below lists the corridor reveal as open: it is built (D-0023, `Room/RoomReveal.cs`, once per save slot; the
console's `reveal` replays it). D-0024: the toy box's boards are unscaled hinges, a plain shipping label replaces the
green sticky note, BoxBurst measures from the walls' middle. Nothing played since the 00:36 rebuild. OPEN for him: walk
the rebuilt course (reveal, box burst, car, keyboard floor, save point); run cadence A/B (since 4 Nov); the 4 Nov
design-pass picks (Outgrown frame, R3 grow core, train, six puzzle pieces, dawn ending: none adopted in DECISIONS);
idle-jump ending; run legs angled inward; Feel on the
shift (BACKLOG 5). Untracked: `Assets/ERP` + `.erp` (Editor Rich Presence, his install).

## 5 Nov 2026 — live playtest session: tools, menu, saves, car, keyboard, box, clean dressing (D-0021, D-0022), COMMITTED
His live walk with the bridge on. Fixed live then in the builder: desk ramps (were 50°, now 30° and flush), the balloon
swarm (pack emitters), clipping rulers. Built: debug HUD + console, title menu with three save slots, a save point at the
playroom entry, the keyboard floor, a drivable wooden car, a sofa updraft fan, a real cardboard toy box with a proper
collapse, one balloon burst at the end. Strict prop scan + fixes: 1 intended overlap left. Toy box rebuilt and course
rebuilt + saved by bridge. UNPLAYED since the rebuild. OPEN: his cinematic reveal for the first corridor (R3 → playroom),
idle-jump ending, run legs angled inward, his title lettering into TitleMenu.titleArt.

## 4 Nov 2026 13:3x — INTERACTABLE PROPS landed (D-0020): shove, E, carry; his re-run of the builder pending
His first walk of the dressed R4: "looking good so far". Landed on his "stopped playing": `Room/Pushable.cs`,
`Room/Reactor.cs`, the motor's shove hook, `MovementSettings.pushSpeed` (3) + `propGravity` (60, also the box burst),
builder tagging (Light clutter, Heavy toys, spinning globe/pinwheel/fan, bobbing rabbit/bear, pettable cat, three
switchable lights, carryable bear/pillow/boxes, a kid-only wall switch for the fan). Compiled outside (0 errors).
HIS STEPS: Shift Jam > Build Course (Rooms 3-6), save, Play: shove a pencil then a box as the toy, E on globe/cat/lamp,
pull the fan switch as the kid, bowl the ball into the skittles. Uncommitted until his run.

## 4 Nov 2026 13:1x — R4 DRESSED in our own composition from the pack's props (D-0019), headless, COMMITTED, UNPLAYED
His "begin dressing the larger room with the selected beats" (he draws the title card; Unity closed all day). First
answer (D-0018, 11:5x) moved the Playroom pack's whole showroom scene in and seated the beats on it; his "i dont want
to copy demo room and call it mine" reverted it within the hour. Now: our 6×5 room and COURSE.md layout, dressed by
`CourseBuilder` zone by zone: desk clutter (lamp, mouse, mug, markers, calculator), shelf + books + rabbit by the hoop,
skateboard/ball/dumbbell, cars + ramp + road loop + station at the track, a camp around the maze (truck, helicopter,
flag, ladder, wire, loose sandbags, soldiers on stands), two more towers + skittles + ball at the castle, pillows +
bear + chessboard with pieces on the sofa, a cat, shelf + book stack by the wardrobe, wooden toys + blocks + puzzle
pieces in the SW corner, six 2×2 mat clusters, posters/paintings/clock/sockets on all four walls, a ceiling fan, book
stacks, open boxes in corners, and ~230 seeded clutter props that skip the reserved beat volumes (maze, the way down,
the safe, the entry). Shell: the north-wall cube bay is now the pack's Wall_03 window piece (130 wide) with Window_01
in its opening, sill 41 (one kid jump), wood sill. Lights: RoomLight 4.5 + two warm fills. Descent from the desk:
two closed Cardboard_Box_05 stacked (24) then a Box_06 (12): the look-book showed every taller box has open flaps.
🔑 Drawing_xx prefabs are loose paper, not wall art (they floated on the walls): posters/paintings on walls only.
New tool: `CourseBuilder.Stills` (menu Shift Jam / Render R4 Stills; batch `BuildAndRender`, needs a GPU) →
`docs/stills/r4_*.png` + a look-book shot, git-ignored; six passes today at ~1 min each. PLAYTEST.md 15–21 updated.
Open for his walk: density/taste, the stool's 14 drop, lighting, the ruler (optional now). Title card mockup + run
cadence A/B still waiting on him.

## 4 Nov 2026 10:1x — design pass written while he draws the title card (PROPOSED, nothing built)
`docs/DESIGN.md` "Design pass, 4 Nov": (1) story frame = moving day, working title **Outgrown**, page hook, card
lines; (2) escalation audit: R3 has no shift (fix: one "Too tight" grow core) and nothing in the house moves (fix:
the pack's train on a Spline across R4, hazard + ride at toy size, a step at kid size); (3) six lost puzzle pieces as
size-gated collectibles with a count on the end card; (4) the dawn ending (camera through the glass, light warms).
Title card: `Art/Mockups/title_card_mockup.png` + `title_card_canvas_guide.png` (growing SHIFT lettering, toy and
kid figures at the ends); his PNG lands as `Art/UI/title_shift.png` → StartCard title becomes an Image.
Run animation: all three locomotion clips loop cleanly (seams 0.00); the flail is cadence: Run Boost ×2.5 on a
0.5 s clip = 5 cycles/s. Proposed A (Walk ×2 / Run ×1.6 / Boost ×1.4, keep speeds) or B (sprint 8 + Boost ×1.6,
my pick). Awaiting his letter. Unity closed all morning.

## 4 Nov 2026 00:3x — his first walk of the course: two notes fixed live, a booster pad built (his idea)
Walk notes so far: (1) "the shift core in the corridor won't prompt" = the checkpoint MARKER used the same orb sprite as
a core. Fixed: markers are small amber dots (`CheckpointGlow.mat`, scale 1.4, amber light), cores stay cyan. (2) "no
mantle or double jump, can't climb the book ladder" = risers 1.6 against a 1.6 jump apex. Fixed: jumpHeight 1.6 → 2.0
(MovementSettings, live) and book risers 1.3 in the builder. (3) His idea "a booster pad": `PlayerMotor.Launch(height)`
(no jump cut, no coyote re-jump) + `Room/LaunchPad.cs` (trigger, size-gated, cooldown, squash on the visual, onLaunch).
Builder: the wardrobe's second flight is now a Pillow_01 trampoline on shelf 1's open front (shelf 2 is back-half only
so the launch clears its lip; height = the gap + 10; a glow disc marks the pad). Course rebuilt from inside the editor
(out of Play) and saved: 1 pad, 32 stair books. Committed. Still to see: the pad feel (height, squash), the lane's
pins, the ruler ramp.

## 3 Nov 2026 21:1x — backlog week 1, mine: fade, start/end cards, checkpoint markers, pin hazards (headless, UNSEEN)
Editor was closed again after his short launch (log ends 20:58, no crash marker). Landed + compiled: `UI/ScreenFade.cs`
(black cut, unscaled time), `UI/GameFlow.cs` (start card gone on first press, End() from the last Goal → fade + end
card, R restarts the scene), Respawn.Put() (every reset goes through the cut), Checkpoint.litWhenClaimed, Hazard (pins).
Builder: HUD gets Fade + StartCard ("SHIFT / press any key") + EndCard ("You made it to the window. / R to play
again") + GameFlow; every checkpoint gets a Marker glow + light that lights on claim; the R6 Goal's onReached →
GameFlow.End. Two headless runs (33 s, 37 s, 0 errors): 8 markers, 3 hazards, cards and fade in the scene. Committed.
**Still mine this week:** visible feedback on a portal (a flash), audio hooks. **His:** the walk (docs/PLAYTEST.md).

## 3 Nov 2026 20:1x — ROOMS 3–6 BUILT headless (his "proceed", he in AoE4, editor closed), saved, UNPLAYED, UNSEEN
Editor was closed (log: graceful shutdown 19:51), so the course went in as `Scripts/Debug/CourseBuilder.cs` (editor-only)
run by Unity batch mode (-nographics, 35 s, 0 errors, no missing prefabs): `Room/Course/{R3_SkirtingRun, R4_Playroom,
R5_Wardrobe, R6_Window}` in Greybox.unity. Landed + compiled: Checkpoint, Portal, Respawn fall rule (fatalFallHeight 14),
PushSwitch, Flashlight toyUsesE, Carried, Carryable.plank + Carrier.LayAlongGround. **Layout:** R3 lane x −20..281 at
z −150 (south of the bedroom; pits at 38–72 pencil bridge, 100–103.5 hop, 230–234.5 sprint; book stairs over a 12-box at
142; three pins; 4 checkpoints; exit portal → R4). R4 centre (480, 0, −140), walls x 285..675 / z −302.5..22.5: desk+table
NW (core B shrink on it, table checkpoint, ruler plank + Box28 + chair + Box12 as the way down), sandbag maze NE (core A
grow), race track centre, castle S, sofa E, hoop W by the entry, wardrobe SE with the Safe (carryable) in front of the toy
hole (portal → R5), planet lamp, mats/blocks/drawings. R6 = north-wall window bay (cube wall with a 47×64 hole), sill,
core D (toggle), Goal (toy) → GoalText; garden outside: grass ground + 24 Toy_Tree ×35, a sun light. R5 interior at
(480, 0, −700): 195×120×170 wood box, shelves 40/80, two book-stair flights (each riser 1.6), checkpoints per shelf,
torch lying on shelf 2 (head → core C 0° off axis; PushSwitch cap on its bump → Flashlight.Toggle), core C dormant +
LightSensor, TopPortal (kid) → WardrobeFront in R4, opening the two wardrobe doors (Door on the prefab's door children).
Bedroom toy-door Goal replaced by a Portal → R3 start. **Not verified by eye** (no GPU while he games): his open + walk is
the test. **Likely rough edges:** pin orientation, torch roll, sandbag ring gaps, book-stair overlap look, R6 wall seams.
**Re-run after any tweak to the builder:** menu Shift Jam / Build Course (replaces Room/Course), or the batch line in
the session notes. D-0016, D-0017.

## 3 Nov 2026 15:5x — COURSE DESIGNED (his "help me design a course and layout", he AFK): docs/COURSE.md + map
Rooms 3–6 proposed with beats, top-down maps, exact pack props and the numbers (toy/kid jump, reach, step; table
36 / chair 22 / boxes 12-18-28 measured): R3 skirting run (toy lane: pencil bridge, gaps 3.5 and 4.5, book stairs,
pins, checkpoints), R4 playroom (kid carries the Safe off the exit hole and lays Ruler_Big as a table→chair ramp, then
shrinks on the table; three cores), R5 wardrobe climb (dark, PushSwitch + LightSensor from D-0013 earn their place),
R6 window payoff. Proposed rules: toy falls > 12 u respawn at a checkpoint (Checkpoint.cs), rooms as additive scenes.
Build order in the doc. Nothing built. **Also this session:** Jump run middle ×0.3 at offset 0.33 (his "legs pedal"),
committed. **Open for him:** fall rule yes/no; toy-only lanes vs both-size rooms for R3/R5; Run Boost ×2.5 vs ×3.2.

## 3 Nov 2026 15:1x — THE GLIDE FOUND: the pack's Run Boost clip did not loop (fixed on the clip; git-ignored path)
Live MotionProbe (editor-only, `Scripts/Debug/MotionProbe.cs`, attached by RunCommand in Play, summary in Editor.log):
at sprint the body moved 10 u/s and the planted foot ALSO moved 10 u/s = legs frozen; Speed 1, Run Boost weight 1.
Cause: `_Imports/.../Run Boost.anim` had Loop Time OFF (Walk/Run/Idle/Jump loops on), so the blend tree played its
0.5 s once and held the last frame. Set loopTime + loopBlend true on the clip (pack file, not in git: REDO after any
re-import of Funny Characters). Walk-speed skate measured 3.5 u/s against the predicted 2.2 (Walk ×3). Clip speeds from
13:1x stand. **Bridge lesson:** Unity_ReadConsole never returned runtime Debug.Log lines; read
`%LOCALAPPDATA%/Unity/Editor/Editor.log` from WSL instead. His earlier "run is slow" + "moonslide" + "same after relaunch"
were all this one flag.

## 3 Nov 2026 13:1x — his play notes fixed live: grip along the forearm, clip speeds vs foot slide, kid camera 0.7
"Holding it like a microphone": Carrier now aims the carried thing along the forearm (elbow → hand) instead of the
hand's thumb axis. "Run looks like a moonslide": measured on a rig copy (planted-foot speed) Walk 1.1 / Run 1.6 /
Run Boost 3.7 u/s against body 5.5 / 7.75 / 10 → blend tree clip speeds Walk ×3, Run ×2.5, Run Boost ×2.5 (sprint feet
9.2 u/s; walk still slides ~40%, the clip is a stroll; alternative = lower walk/sprint toward 4/8). gen_controller_v8.py
carries the timescales. "Camera on top of the arms": kidDistanceMultiplier 0.5 → 0.7 (2.9 m back). Committed.

## 3 Nov 2026 12:5x — bedroom 4×4 + kid camera + hold pose (his "proceed"; he in AoE4), saved, UNPLAYED
**Room:** shell rebuilt by RunCommand: 16 Floor_01, 16 Ceiling_01 (y 130), 16 Wall_01 (inner faces ±130; the first pass
used Wall_02 and gave eight doorway holes, replaced), 4 Wall_05 pillars at ±132.5. Bed/table/lamp/StepBox/books/
ShiftCore2 +32.5 x; Shelf/Switch -32.5 x; Window +32.5 z; doors, DoorGlow, DoorLight, Goal -32.5 z; RoomLight range 440.
Four-angle capture checked (solid walls, bed east, shelf west, window north, doors south). Toy walk books → toy door
now ≈ 150 units (15 s sprint). **Camera:** `CameraSettings.kidDistanceMultiplier` 0.5, `kidFieldOfView` 70, blended by
log-scale in ShoulderCamera. **Hold:** `Animation/HoldPose.anim` + `RightArmMask.mask` + `Arms` layer (weight 0,
Override) on PlayerAnimator (added via the API: the controller generator does NOT know this layer, regenerate = re-add);
Carrier fades the layer, holds from the RightHand bone with Carryable grip offsets (palm (0, 0.08, 0), slide (0,0,-1.5),
turn 0): fit is by eye in Play (his), the thumb side of the fist = the flashlight's head. D-0015.
**His checks:** kid size: camera about 2 m behind, 70° view, not in the walls; toy: the floor crossing; pick up the
flashlight as the kid: arm out, fingers closed on the barrel, head forward (nudge Grip values on the Flashlight_01
Carryable if the barrel sits off the palm). **Note:** Fast Script Reload logs one error when a .cs lands mid-Play
(it cannot add fields); harmless.

## 3 Nov 2026 12:2x — PARKED: flashlight puzzle + marble (his call: "messy"); back to the plain glowing orb
Scene reverted to the simple read: both cores awake from the start (glow + sparks, no marble, no ember), ShiftCore back
on the box floor at (10, 3.5, -28), unlimited uses; flashlight a lit prop at (-9, 3.3, -17) yaw 60 with the beam out
of its wide head (+Z: the vertex profile shows the bell there, my earlier "-Z lens" note was wrong), ambient back to
1. Scripts stay in the tree, unused for now: Flashlight (E still toggles it; a kid can still pick it up and carry it),
Carryable/Carrier, LightSensor (disabled on ShiftCore2), ShiftSource dormant/uses/marble fields (all off).
`PushSwitch.cs` + the toy-E gate were staged and compiled but never landed (scratchpad stage9). D-0013 stands as a
design we can return to; D-0014 records the park. **Open from his play:** the room feels cramped (proposal: 4×4 tiles +
kid camera ×0.5 / FOV 70, awaiting his word).

## 3 Nov 2026 12:0x — his orb mote in, glossy marble inside the halo, flashlight re-aimed (his play notes), saved, UNPLAYED
His orb drawing → `Art/Portals/orb_mote.png` on OrbGlow.mat (tint white). His note "a bit flat, needs depth and gloss":
the hidden Wooden_Sphere is back as a glossy marble (scale 0.8) inside the halo: `ShiftCoreMarbleDark.mat` while
dormant, `ShiftCoreMarble.mat` (emissive cyan, smoothness 0.96) once awake, swapped by ShiftSource (new `marble`,
`marbleAwake`, `marbleDark` fields in ApplyLooks). Flashlight moved to (-5, 3.3, -16) yaw 290 so the lens points across
the box centre; its core tail at (-10.7, 3.3, -13.9), clear of the walls. Committed. **Open from his play:** none new.

## 3 Nov 2026 11:4x — flashlight puzzle built (his "fully functional light"; out of Play), saved, UNPLAYED
**Landed + compiled (outside csc, then his editor, 0 errors):** `Interaction/Carryable.cs`, `Interaction/Carrier.cs`,
`Room/Flashlight.cs`, `Room/LightSensor.cs`; edits: `PlayerInput` (UseHeld = Attack, Drop = Crouch), `Interactor`
(skips the carried thing and empty-prompt targets), `InteractPrompt` (held hint "LMB Light / C Put down"),
`ShiftSource` (startDormant, dormantPrompt "Dark", uses, spentPrompt, awakeOnly/dormantOnly lists, onAwaken/onSleep/
onSpent). **Wired (RunCommand, saved):** ShiftCore moved into Flashlight_01's tail (local z 6), its sphere hidden,
GrowOnly, dormant, 1 use; FlashlightBeam → `Beam` spot at the lens (50°, range 90, int 5, soft, off); Flashlight +
Carryable (hold rotation 0/180/0) on Flashlight_01; both cores: Ember quad (OrbGlow ×5) + EmberLight while dormant,
Glow + Sparks while awake; ShiftCore2 dormant + LightSensor; Player: `Hold` child (0.3, 0.95, 0.5) + Carrier;
InteractPrompt.carrier; ambient intensity 1 → 0.3. D-0013. **Note:** the core sphere's collider in the tail touches
the box's left wall by ~0.5 (reachable from the room side).
**His checks:** (1) box is dark, a cyan ember at the flashlight's tail; E on the body "Switch on" → beam + the core
glows; E on the tail "Shift: grow" → burst, core goes dark (spent); (2) kid: E on the flashlight "Pick up", it sits in
the right hand and points where you look; LMB toggles it; (3) aim at the bed core ("Dark") for half a second → it
wakes (glow + sparks); (4) C puts it down facing your way; jump on the bed, E → shrink; (5) toy: E on the flashlight
body still toggles it. **Still proposed:** bedroom 4×4 + kid camera; dark-room navigation later (his idea).

## 3 Nov 2026 11:1x — his portal drawings in, warp shows colour, speeds raised (his "proceed"; he out of Play), UNPLAYED
**His four drawings** converted by script into `Art/Portals/`: `warp_mask_swirl.png` (ring-only drawing → white on
black, 1024, sRGB off), `warp_art_grow.png` (purple void) + `warp_art_shrink.png` (dark disc + swirl) as 2048 RGBA
overlays (alpha from white), `orb_glow.png` (ring + bolt, as drawn). Mockups kept beside them. **Shader v2:**
`_Art/_ArtStrength 0.85/_ArtScale 1.4/_Spin 60`, mask spins with the art; `ShiftWarp.cs` growArts/shrinkArts lists.
Warp lists and ShiftWarp.mat point at the new files; OrbGlow.mat uses orb_glow. **Speed:** MovementSettings walk
5.5 / sprint 10 / acceleration 60; controller v7 = blend tree Idle 0, Walk 0.5 ×1.15, Run 0.75, Run Boost 1
(gen_controller_v7.py; v6 backed up). Editor compiled clean, shader HasError false. D-0012.
**His checks:** sprint feel (Shift held); the run pose at full sprint = Run Boost; a shift shows the swirl tear
in colour with a brief void at mid-shift (Art Strength on ShiftWarp.mat: 1 = solid void, 0 = off); the orb glow
is his ring. **Still proposed, his word:** bedroom 4×4 tiles + kid camera distance ×0.5 / FOV 70; flashlight
puzzle (cores drink light). `Assets/ERP` still untracked (his call).

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
