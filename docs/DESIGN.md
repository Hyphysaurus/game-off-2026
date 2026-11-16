# Shift Jam — design

GitHub Game Off 2026, theme **Shift**. Unity 6000.3.10f1, URP. Working title pending.

## The one idea
The player is a toy figure in a kid's bedroom. A **shift** swaps them between **toy size** and **kid size**.
The same room is two levels: at toy size the floor is a landscape of books, train track and castle walls;
at kid size the bed, the shelves and the window are the play space. One room, built once, played twice.

Chosen 2 Nov over a day/night world shift (every space built twice) and a kid-to-car form shift
(two motors, Voltane's territory). Voltane has scale shift as a single gag; nobody owns it as a core.

## Room 1: the toy box (the awakening; 2 Nov, replaces the bedroom as the first room)
Cores are batteries (D-0013, 3 Nov): the box starts dark, the flashlight is off with a dim ember at its tail.
E on the flashlight switches it on (the beam floods the box, its core wakes); E on the tail shifts you up and spends
that core. The flashlight stays yours: a kid picks it up, aims it by looking, LMB toggles, X puts it down; its beam
wakes the dormant core on the bed. Dark-room navigation with it is a later room.
Room of Awakening in the TotK sense: wake somewhere small and dark, find the one glowing thing, and the
shift itself is the exit. The toy wakes inside a closed cardboard box on the bedroom floor; a flashlight
left on inside is the only light. Four beats, about thirty seconds: get up (WakeUp plays the idle death
clip backwards), walk to the light, hop a toy brick, press E on the shift core. The Rift Apart warp hits,
the figure grows, the box panels burst outward (BoxBurst) and the kid stands in the bedroom, box flattened
at their feet. That is the reveal and the theme in the first minute. Greybox: six cubes as panels.

## Room 2: the bedroom (BUILT 3 Nov 10:4x by RunCommand, unplayed; beats below are what is in the scene)
Layout (pack units, room ±97.5): bed against the east wall centred z 0 (mattress ~20 high, foot and
headboards 39/27); bedside table + night lamp at its south end; the descent on its west side at z 12
(cardboard box 19 high flush with the bed, three books 8.6 high beside it); second core on the mattress
at (47, 22, -39), shrink only. Shelf on the west wall at z -40 with Switch_01 on the wall above it (y 56,
kid only: "Too small to reach"). Window on the north wall, nine ceiling tiles, planet lamp + warm point
light (y 78). South wall: real door at x -55 (Locked: "Locked", event for a rattle) and the toy door at
x -15 (Doorway_01 + Door_01 at 0.035 = 1.7 × 3.6, swings into the room, warm glow quad behind it, a
point light that comes on when it opens, Goal trigger in the frame, toy only). The first core in the box
is grow only and reads "Too big to hold" at kid size.
You stand up out of the burst box as the kid. The room is quiet, the real door is shut (locked, or just
too heavy: a push animation that fails). Problem before solver, every beat:
1. **Look around as a kid.** The bed, the shelf, the window. The only odd thing: a toy door in the
   skirting board, shut, with light behind it. You cannot fit. (Problem: the way out is toy-sized.)
2. **The switch.** On the shelf, kid-chest height, a chunky button. Press it: the toy door swings open.
   Now the way out is open and still too small. (Problem restated: you need to be small again.)
3. **The shift core is gone.** The sphere stayed in the box debris at your feet, toy-sized: at kid size the
   prompt reads "too big to hold" or it simply sits there. The second core is across the room, on the
   bed, visible from the floor, glowing. Walk over, E: shrink. (Solver: the room has more than one core,
   and each is where one size can reach it.)
4. **The floor is a landscape now.** From the bed you are a toy on a cliff. The way down: the books the
   kid left stacked against the bed (you did not stack them; they were there, a small lie the room tells
   you so beat 1 of the old design survives). Hop down the books, cross the floor past the flattened box,
   through the toy door. Goal. (Payoff: the two sizes in one room, both used, no backtracking.)
Reserve for later: the kid rearranges something the toy then traverses (D-00xx, the "kid arranges, toy
traverses" verb); for v1 the books are pre-stacked.

### Original beats (written for a toy-sized spawn; kept for reference)
1. Spawn toy-sized beside the bed. Learn move and jump on a stack of books. The exit, a toy door in the
   skirting board, is visible and shut.
2. The problem: the bed is a cliff. On the floor sits one glowing object on a pedestal, the shift source,
   seen from anywhere in the room.
3. Press E on it. The shift: a timescale punch, a camera pull and a shake (Feel), the figure grows, the
   world drops away. The bed is now a step.
4. At kid size, a switch on the shelf opens the toy door. The door is toy-sized. Shift back small on the
   same source, walk through. Both directions learned; the door becomes the first portal.

## Sizes
Toy = 1, kid = 35 (D-0008). The Playroom pack is built at 1 unit ≈ 2 cm with a ~4 cm figure in mind, so
the figure at scale 1 is the pack's own toy and the kid at 35 is a 1.3 m child. One book = one toy jump;
the bed = one kid jump; the shelf top = kid chest. (1:8 was the first guess, before the pack was measured.)

## Kit
ithappy Playroom (room, furniture, toys), Funny Characters (the figure, modular; Full_Body costumes share
the rig), Feel for juice, Shapes for UI lines, TextMesh Pro. Battle Stadium and Cyber Racing Cars held for
later worlds.

## The course after Room 2
BUILT 3 Nov 20:1x by CourseBuilder (D-0017), unplayed. See `docs/COURSE.md` (3 Nov): R3 skirting run (toy lane), R4 playroom (kid arranges, toy traverses, three cores),
R5 wardrobe climb (dark, the flashlight returns), R6 window (payoff); the toy fall rule (12 u) and Checkpoint are
proposed with it. Map: `docs/maps/course_overview.png`.

## Parking lot
- World 2 and beyond: what the toy door leads to (Battle Stadium arena? the attic?).
- Costume shift (Full_Body swaps) as a cosmetic.

## Design pass, 4 Nov 2026 10:1x (PROPOSED while he draws the title card; nothing here is built; his picks)
Six rooms exist and the loop runs end to end. What the entry lacks as a jam game: a reason for the route, a
second thing the shift does, something that moves, and a reason to look around. Four proposals, cheapest first.

### 1. The story frame: moving day. Working title "Outgrown".
Everything already in the scene gets a reason without a line of code. The toy wakes inside a sealed cardboard
box because it was PACKED. The bedroom is half packed (more Cardboard_Box_01..06, TapeRoll, Tape_01, Note_Paper
labels, bare walls where the drawings were). The real door is locked because the movers come in the morning. The
window sill is where the toy always sat, and the game ends with it back on the sill at dawn, looking at the
garden it will never see again. "Shift" reads twice: the size shift, and the house shifting. The kid-size shift
is never explained; it is the one magic thing.
- Title: **Outgrown** (the kid outgrew the toy, so it was packed; the toy outgrows its box). Alternatives: Small
  Hours, Packed.
- Page hook: *A toy packed away on moving day shifts between toy size and kid size to get back to its place on
  the window sill before morning.*
- Start card: the SHIFT lettering + "press any key". End card: "Back on the sill." + the piece count (see 3).
- Cost: dressing by the builder (boxes, tape, labels), two text lines. Half a day.

### 2. Escalation: one new wrinkle per room (audit of what is there)
| room | what the shift does here | state |
|---|---|---|
| R1 box | learn E, grow | built |
| R2 bedroom | reach: each core where only one size can get to it | built |
| R3 lane | nothing: two minutes with the theme switched off | gap |
| R4 playroom | kid arranges, toy traverses (carry + place) | built, unplayed |
| R5 wardrobe | dark, the light is the key | built, flashlight half parked |
| R6 window | the last shift, the payoff | built |
Two fixes:
- **R3**: one grow core mid-lane the player cannot use. E → "Too tight in here" (ShiftSource.wrongSizePrompt
  already exists). A joke that keeps the theme in the room and teaches "wrong size is a thing" before R4 needs it.
  Cost: one core and one string.
- **Something moves**: the house is static. The pack has a train (Train_Locomotive_01, Train_Carriage_01/02,
  Train_Logs_01, Railway_01..03) and Splines 2.8.3 is installed: a SplineAnimate loop needs no code. In R4 the
  track crosses the toy's floor route: at toy size the locomotive is a moving wall (Hazard on its nose) and the
  log carriage is a moving platform you can ride; at kid size you step over it. One prop, two meanings, in
  motion. If the ruler ramp fails his feel test, the train becomes the way up to the table instead (ride the
  logs, hop onto the chair). Cost: one day with the builder.

### 3. Lost puzzle pieces (the reason to look around)
The kid's jigsaw (Puzzle_01..20 props) is missing six pieces, scattered where only one size can see or reach
them: under the bed (toy), on top of the wardrobe (kid), behind the sofa (toy), in the matchbox in the lane
(toy), on the desk shelf (kid), inside the castle (toy). Each one is visible from somewhere the player already
stands (the interactables rule), none is required. End card: "4 of 6 pieces found." Judges replay for the
last two; the two sizes pay off in exploration, not only in traversal.
Cost: `Collectible.cs` (trigger, optional size gate, chime, count) + a counter in GameFlow, half a day; his
placement pass after.

### 4. The ending, made of what exists
R6 as designed, plus: on the last shrink the camera pulls back through the glass over three seconds, the
RoomLight warms and a sun comes in from the window (dawn), the end card fades up over the garden with the
piece count, and the last image is his SHIFT / Outgrown lettering. Cost: a camera rail + a light lerp in
GameFlow.End, half a day.

### Feel (his layer, after the art): the shift's timescale dip and lens punch, landing squash, footstep SFX
per surface (the toy hears everything louder), the box burst thump, the train's clack as it passes.

### Order I would pick
1 (frame + dressing) → 3 (pieces) → 2 (R3 core, then the train) → 4 (ending). All four fit before the week-2
art items in BACKLOG.md. Parked: Cat_01 as a roaming pet (a monster at toy size, a cat at kid size) is the
best idea in the pack and the most expensive; after the jam.

## Proposals, 6 Nov 2026 (PROPOSED while he plays; nothing here is built; his picks)

### 1. Getting the toy onto a pillow (his note 5 Nov: "pillows too tall to climb") — A BUILT 6 Nov (D-0039)
Measured: Pillow_01 lies 10.8 units tall (22 x 19 across). The toy is 1.8 tall and jumps 2.0, and every pillow's launch
pad sits on the pillow's top. No jump reaches it, and a ledge grab reaches about one body height above the jump apex
(~3.6), a third of the way. So last night's pick (a ledge grab) was wrong for pillows. Options:
- **A. Bouncy sides (my pick).** Running into a pillow's side throws the toy up and a little inward, so it lands on
  the top, and the top pad throws it high as it does now. No new art. It works wherever the kid drops the pillow, so
  "kid arranges, toy traverses" holds. Build: LaunchPad gets a side mode (height = the pillow's top + clearance, an
  inward nudge); the builder adds a side trigger to every pillow pad (Workshop pillow, PillowPad piece, couch pillows,
  wardrobe pillow). About an hour.
- **B. A springboard step.** The Cyber pack's Trampoline_01 (about 1.7 tall at car scale, a hop for the toy) set beside
  the pillow throws the toy onto it. Pack art and a clear read, but it stays put when the kid moves the pillow unless it
  is carryable too. Worth adding to the Build Mode catalogue anyway (the pack has six).
- **C. Steps the kid sets down.** Books or boxes as stairs (the BookStairs piece exists): about eight 1.3 risers per
  pillow. On theme, slow.
- **D. A mantle / ledge grab.** Not for pillows. Useful later as a climb verb for 2 to 4 unit ledges (book risers, box
  lids) if he wants one.

### 2. Car location portals (his direction 5 Nov: "warp portals that teleport in location for driving in cars instead of shifting in size - we shift in location") — BUILT 6 Nov (D-0039; my picks on his calls: keep speed, cars only, hoop frame; course spots still his)
Today the course's Portal moves only a player on foot: it looks for the player's collider, which is off while seated,
so a car drives straight through. Proposal:
- **CarPortal.** Drive through a ring and come out of its partner at the same speed, turned to the partner's facing.
  Car, driver and camera move together; a short warp flash uses his swirl drawing (Art/Portals/warp_mask_swirl); the
  exit is quiet for a moment so you do not bounce straight back.
- **Pairing by colour.** Any two portals of the same colour link (red, blue, green), so in Build Mode he places a red,
  then another red, and they connect. Works the same in the course.
- **Look.** The Cyber pack's Circle_01 hoop (a 10 m ring, about 9 units across at car scale: a car fits with room),
  his swirl art spinning inside it, a floor arrow (Arrow_01, flat) at the way in.
- **Build.** Vehicles/CarPortal.cs (trigger, colour registry, teleport via ArcadeCar.PlaceAt with the velocity turned
  into the exit's frame, ShoulderCamera.Warp + chase, exit cooldown), a flash, the builder makes three colour prefabs
  into Pieces/Vehicles, catalogue rebuild, a starter pair in the Workshop. Two to three hours.
- **His calls.** Keep speed through the portal (my pick) or stop at the exit; let the toy on foot use them too or
  cars only; where they go in the course (a shortcut off the stunt run?); the hoop or one of his drawings as the frame.

### 3. Speed boosts (PROPOSED 6 Nov, his "lets design some speed boosts")
The Cyber pack was built for boosts: its demo car has a boost multiplier that fires `Demonstration_Prefabs/BoostFX`
(exhaust flames) and `DriftFX` (tyre smoke when the car slides sideways), and its Track Props hold six Trampoline pads
with matching `_Arrow` decals and four flat floor Arrows. The Playroom pack has the sounds: Steam_Burst (nitro whoosh),
Spring_Boing (jump pad), Star_Light_Activate (meter full). Options:
- **A. Boost pads.** A floor arrow (Arrow_01..04): drive over it and the car surges along the arrow (about 1.4x top speed
  for 1.2 s, fading), flames from the exhaust, the whoosh, the vehicle camera widens with the speed. Build Mode piece,
  and the kid can grab and place one (kid builds the track, toy drives it). About 1.5 h.
- **B. Jump pads.** The pack's Trampolines: up and forward, air time for the stunt run. The pillow pad idea for cars.
  About 1 h.
- **C. Nitro on Shift.** Left Shift (the run key, unused while driving) spends a nitro meter; drifting (handbrake
  slides), air time and boost pads fill it. "Press SHIFT to boost" is the jam theme's own key. About 2 h with the meter.
- **D. Pull-back launch (toy only).** Hold brake and throttle while stopped to wind the toy's spring (the car squats,
  a creak), let go to launch, harder the longer the wind. The one boost only a toy car has. About 1.5 h.
- My pick: **A + C** (pads fill the meter, Shift spends it), then D as the toy's signature move; B when the stunt run
  wants more air.

### 4. A basic driving HUD (PROPOSED 6 Nov)
Shown only while driving (not in the garage menu), bottom right: the speed as a big number in **scale km/h** (the toy
car is about a 1:64 Hot Wheels car: top speed 26 u/s is about 120 scale km/h), an arc that fills with speed and reddens
near the top, the nitro bar under it with a Shift key icon, and a "BOOST" pop when it fires.
Assets found (owned): **fonts** Teko (numbers) and Fredoka (labels), OFL Google fonts inside ModularUI (Asset Store
cache, not imported; import just the two TTFs); **bars, frames, rings** in Kenney's UI Pack (CC0; ModularUI carries the
full set in five colours) and Kenney UI Adventure (Asset Library); **key icons** Kenney Input Prompts 1.5 (Asset
Library, CC0: the Shift key, pad buttons; also upgrades the H key list); **effects** the Cyber pack's BoostFX / DriftFX;
**sounds** the Playroom pack's Steam_Burst, Spring_Boing, Star_Light_Activate, UI_Notification; **juice** Feel (in the
project) for the punch, Text Animator for TMP (owned, not imported) for an animated BOOST. ModularUI's own HUD templates
are a full framework (about 1,700 files): use as reference only. Licences: Kenney CC0 and OFL fonts can sit in the
public repo; anything from ModularUI or the Asset Store stays git-ignored.

#### Battle Stadium adds (his "access the battlestadium as well", 6 Nov)
Same ithappy cartoon style, so it mixes with the Cyber pack and the Playroom. Racing-ready prefabs:
- **Gates and track:** Inflatable checkpoint_001..003 arches, arch_001..006, arc_001..004, finish_002 (Other/finish_001
  too), ring_002 (a hoop to drive or fly through), road_001/002 (inflatable road), wall_* (soft barriers), slide_001 (a
  soft ramp), Stadium/race_track_circle_001/002 (a ready circular track), fence_001.
- **Launchers:** Obstacles/trampoline_1..6 and propeller_trampoline_1/2 (a fan pad), Other/cannon_001/002.
- **Pickups:** Props/star_001/002, coin_001, diamond_001, crown_001, bomb_001, dynamite (the pack's RotationScript and
  Oscillate* scripts spin and bob them).
- **Cars:** Racing_Cars car_1..10 with separate wheels; **car_4 has a wind-up key** (a ready model for D, the pull-back
  launch). Their wheels are named in lower case (car_1_wheel_001): ArcadeCar's wheel finder looks for "Wheel" and would
  need to ignore case before they can drive.
- **The finish:** Stadium/TV_001 (a big screen for lap times in the world), podium_001..007, Other/firework_001/002,
  spotlights, speakers, crowd stands.
How it shifts the boost options: **A** gains boost GATES (drive through an inflatable arch or the ring: easier to see
than a floor arrow), **B** gets the pack's trampolines and the fan pad, **C**'s nitro refills come from star pickups,
**D** has its car (car_4, the key turns while you wind), and a new **E: the cannon** (drive in, get fired across the
room) is a later treat. A race loop (checkpoint arches, the finish arch, the circle track, the TV showing lap times, the
podium and fireworks at the end) would give the boosts a reason; the HUD then adds lap and time.
