# STATUS — Shift Jam

Truth for what is BUILT vs DESIGNED. Newest entry first. Versions log at the end.

## 19 Nov 2026 18:5x — THE LEVEL PLAN: COURSE WALK CHECK, ITS FIXES (D-0081), AND THE PLAYROOM'S SOLDIERS BEAT (D-0082) — NOT PLAYED
His "proceed with the level plan" (the three parts of the jam's level plan). Built with the editor closed the whole
time. Commits `98d6aed` (the fixes) and `358c6f7` (the soldiers beat). Nothing else from LEVEL_BRAINSTORM was built.
- **The walk check.** A hidden probe walked R1 to R6 in Greybox on today's code, on the real keys (W, Shift, Space, E,
  X), beat by beat, and read what happened (size, resets, portals, doors, what is held). It is kept outside the project:
  `%TEMP%\shiftjam-stage\course\probe\CourseWalkProbe.cs` (run with `run_probe.sh` there; `-walkOnly 1J23` / `456` /
  `S` for parts, `-walkStills` for pictures). Your save slots are put back as they were after each run.
- **The moves, measured:** toy jump 4.40 far at a walk, 6.40 at a sprint, 1.99 high; kid 121 far, 67 high. (COURSE.md's
  table still says jump 1.6 and sprint 10: it was designed before D-0039.)
- **FOUND, per room, and what was done** (the numbers are before -> after):
  - **R1 toy box:** works. Walk to the core, E: the toy grows and the box bursts.
  - **R2 bedroom:** the switch opens the toy door; the kid gets onto the bed; the bed core shrinks; the toy door takes the
    toy into the skirting run. Not fixed, for your eye: (a) the kid reaches the bed core from the floor (the kid's reach
    is 60 up), so the toy usually starts on the floor and the bed descent is skipped; (b) from the mattress, the step
    box stands about 4 off the bed's edge: the toy drops 11 onto the top book instead (under the 14 fall rule, no
    reset); (c) E on the torch shows "Pick up", but in the probe the torch never came into the hand. Not found why; it
    may be the probe (the grab takes E first). Please try it.
  - **R3 skirting run:** BROKEN, FIXED. The book stairs' risers were 2.0, 1.8, 2.3, 1.1 ... 2.4 (2.3 and 2.4 are over the
    jump) and they began at the hop gap's far edge, so the hop landed on a book 2 high and fell back in. Now: even
    risers 1.1 (1.1, 2.2 ... 12.2), the stairs start at 110 after a clear landing with its checkpoint. The 4.5 "sprint
    gap" was made at a walk (4.55): now 5.5 (a walk falls in, a sprint makes it, 6.2). Pencil bridge, pins, the pit
    rule and the exit to R4 all work.
  - **R4 playroom:** BROKEN, FIXED. The toy hopped the sandbag maze (a sandbag is 1.57 high, the toy jumps 2.0): both
    rings are now two sandbags high (3.1); the probe can no longer hop in, and walks the maze to core A in 27 s. Core A
    grows, the kid carries the safe off the hole, the kid hops onto the desk and core B shrinks him there, the toy walks
    down the three ruler ramps, the hole takes the toy to R5. Also fixed: beside the desk, core B was never the E
    target (the search read only 24 things and the keyboard's 20 keys filled it: now 64). Not fixed, for your call: the
    ruler beside the stool is hard to pick up as the kid, because core B on the desk is nearer to the chest and wins
    the E (the ruler is not needed: the ramps are there). The kid could not reach anything lying flat on the floor:
    the search now reaches the floor.
  - **R5 wardrobe:** BROKEN, FIXED, three ways. (1) The book flight ran along the back wall UNDER shelf 1, so its top
    books met the shelf's underside: it now stands in front of the shelf's edge, the toy walks up its 35 books to 40.
    (2) The trampoline pillow sat 18 in front of shelf 2's lip; launched straight up, the toy drifts about 14 at a
    sprint while above the shelf, so it always fell back (top 91, landed 41): moved to 8 from the lip, lands at 80.
    (3) The torch's beam (range 90) never reached core C (94.8 away): range 120, the core wakes. And (4) the kid was
    shut inside: grown at core C, the kid grows into the top portal while still shifting, and the portal only listened
    for the moment of entering, which it turned away; it now also lets through someone already inside once they are
    the right size. The kid comes out in front of the wardrobe and its doors open.
  - **R6 window:** works: the kid jumps onto the sill (feet 42.8 on a 41.1 sill), core D shrinks, the toy walks into
    the goal, the end card. Note: core D can also be pressed from the floor; a toy shrunk on the floor cannot reach the
    sill (41 high). Not fixed: your call (a lower sill, or the core further back on the sill).
  - Also: the toy's straight sprint from the mouse hole to the maze stops at the clutter after about 280 of 330 (it
    has to be walked round, as a player does); not a fault.
- **The soldiers beat (D-0082), as built:** a dart pistol on its stand 12 in from the mouse hole toward the Hoop
  (304, 0.7, -153.5), in the toy's first view; four toy soldiers (the Workshop's ToySoldier) in the maze, one at each
  turn of the walk round the rings and one beside core A; an ammo box at the maze's east gap. The course player now
  has a WeaponHolder (E on the stand: "Take Dart Pistol"). No Damageable: the soldiers' darts only shove the toy (0.2
  to 0.3 each), never hurt it, so core A can always be won. **Measured:** with all four soldiers up, the probe walked
  the maze to core A in 27 s and grew there; shooting from 10 away, 3 of 4 soldiers went down (the probe's aim, not
  the gun). A soldier is 1.60 tall, the kid 63: ankle-high, as planned. The sandbags (now 3.1) are cover: a soldier's
  eye is at its chest (1.0).
- **Checks:** four-scene smoke 0 errors in each scene; hold 81 of 81; first person 156 of 160 (the same four lines as
  before: the sight distances on purpose since D-0078, and the SMG support hand 7.3 cm); shot 64 of 64. Not re-run
  (no weapon code changed): ammo, dart, grenade, body-hit. The probes' runs played
  the game in a hidden editor; a shift in play writes `ShiftWarp.mat`, so it was put back to its committed state.
- **Stills** (local only): `%TEMP%\shiftjam-stage\course\stills-before\` (the first runs) and `stills-after\`
  (r1_start, r1_grown, r2_on_bed, r3_start, r3_exit, r4_arrival, r4_maze, r4_desk_kid, r4_desk_toy, r4_hole,
  r4_soldiers, r5_start, r5_shelf2, r6_window).
- **What to try:** New Game and play the course end to end: R3's stairs and the 5.5 gap (does Shift feel needed and
  fair?), R4's maze now two bags high, the pistol at the hole and the soldiers (do the shoves feel like anything?),
  R5's flight, the pillow to shelf 2, the torch beam, growing out of the wardrobe; R2's torch in the hand.
- **His calls:** (1) soldiers: a knock only (as built) or a hurt and a respawn (needs a Damageable on the course player
  and a checkpoint at the maze); (2) R2: the kid reaches the bed core from the floor (keep, or move the core); (3) R6:
  core D works from the floor and strands a toy below the sill; (4) the ruler beside core B; (5) COURSE.md's numbers.
- Not played. Not done: no Windows build (v7 later, on your word).

## 19 Nov 2026 13:5x — CHECKPOINT: THE FOAM FINGER AND TOY KNIFE HOLDS (no change kept, nothing built)
Asked to fit the two melee weapons' hold numbers (Hand, GripCurl) the way D-0078 fitted the guns, so the fist stops
sinking into the foam hand's cuff and the knife's handle. **Tried, measured, thrown out. The game is as it was at
`346dca7`.** The editor was closed the whole time.
- **Measured now** (third person, the D-0078 probe's way, deepest point of the hand inside the weapon). Toy and Kid
  read the same within 0.03 cm. Foam Finger: rest 3.13, swing up to 3.48 cm (about 156 hand points inside). Toy
  Knife: rest 1.37, swing up to 1.82 cm (16 to 65 points inside).
- **Why the numbers alone can't do it.**
  1. A melee weapon is not turned by the hand. It points along the arm's flat heading, tipped up 35 degrees
     (`WeaponHolder.Pose`, last branch), with its grip at the palm point. The swing spins it round that point while
     the hand stays still. So the handle never lines up with the fist.
  2. The fingers never close on melee. `GripCurl` only works while `_grip` is above 0, and that happens only for a
     gun held by the arm or in first person. Melee keeps the Arms-layer clip's hand. (I tried closing it: a small
     change in `WeaponHolder`. It was not enough on its own.)
  3. The models are flat and thick. The foam hand is 6.7 cm thick and 48 cm wide at the cuff, so a closed fist can't
     go round it. The knife's handle is 3.4 cm thick. Any hand point inside reads about half that thickness, so the
     depth hardly moves until no point is inside.
- **What the sweeps found** (probe at `%TEMP%\shiftjam-stage\weapons\melee\`, logs and stills there). Finger bend,
  turn in the hand and small slides (up to 4 cm) change nothing: worst 3.2 to 3.5 cm and 1.6 to 1.8 cm. Large slides
  get 0 cm, but only by taking the weapon out of the hand. The foam (slid 8 cm up and 11 cm forward) floats above
  the fist. The knife (7 cm sideways, 7 cm back) goes behind the wrist with its ring in the belly. Sheet:
  `docs/stills/melee_holds.jpg` (local only, docs/stills is git-ignored; the knife's two pictures are from opposite sides).
- **His call / next (needs a plan and his proceed, it is code):** hold melee in the fist like the guns. The handle
  runs through the fist (`FistTurn`) with the fingers closed by the weapon's own `GripCurl`, and the swing turns the
  arm and wrist instead of spinning the weapon round the palm. Then fit Hand / GripCurl with the same probe. The foam
  finger may need its own idea (worn on the hand, the fist hidden inside the cuff), because no fist closes round 6.7 cm.
- Not played. Checks not re-run: nothing changed.

## 19 Nov 2026 12:3x — THE HANDS ON THE GUNS: LESS CLIPPING, AND THE GUN HAND SHOWS WHEN AIMED (D-0078) — NOT PLAYED
His words: "i couldnt see the hand holding either pistol or smg" (his play of 17 Nov, which was before D-0074 put
arms in first person at all) and "some of the hands and clothing clip into the gun slightly" (the first-person
stills). Started by a builder the usage limit cut at 11:13; finished by a second one. Built with the editor closed.
- **Aimed in first person, the pistol is held out further** (its sight 0.55 from the eye, was 0.307), so the hand
  on the grip shows at the bottom of the view. The SMG goes out a little (0.46). The gun looks smaller when aimed.
  The shotgun is unchanged (held out, its gun hand still sits behind the gun).
- **Fewer fingers inside the guns.** Each gun now has its own grip slide, finger bends, support-hand spot and how
  far round the barrel the support palm sits. All of it is numbers on the gun, written by the Workshop builder
  (new menu: Shift Jam > Workshop > Fit Weapon Holds). A new gun sets the same numbers (weapons NOTES, "HANDS ON THE
  GUNS").
- **MEASURED against the drawn meshes** (the probe knows none of the numbers above). Deepest point of a hand,
  sleeve or body inside the gun, cm, before -> after:
  - Third person. Pistol: rest 1.25 -> 0.97, aim 1.02 -> 0.97, walk 1.53 -> 1.02, reload 0.95.
    SMG: rest 1.93 -> 1.50, aim 1.69 -> 1.66, walk 1.94 -> 1.50, reload 1.51.
    Shotgun: rest 2.46 -> 2.05, aim 2.65 -> 1.23, walk 2.75 -> 1.54, reload 1.33.
    Foam Finger 3.13 -> 3.13, Toy Knife 1.37 -> 1.38 (not worked on: the fist goes through the foam hand's
    cuff and the knife's ring; that is the melee arm pose, not the hold).
  - First person. Pistol: every pose 1.0 or a little under (was 0.97 to 1.05). SMG: hip 1.69 -> 1.51, aim 1.64 ->
    1.50, walk 1.96 -> 1.54, run 2.07 -> 1.55; **worse: looking 45 up 1.69 -> 1.95 and aimed while walking 1.65 ->
    1.91** (the support palm into the magazine). Shotgun: hip 2.96 -> 1.21, aim 2.78 -> 1.56, walk 3.29 -> 3.05,
    run 3.30 -> 2.55. Reloads (no before reading): pistol 1.00, SMG 1.79, shotgun 1.50.
  - The gun hand in the view when aimed in first person (share of the hand in the frame / not hidden by the gun):
    pistol 0 / 0 % -> 30 / 9 %; SMG 0 / 0 % -> 10 / 7 %; shotgun 0 / 0 % (unchanged).
- **Checks:** first person 156 of 160: three are the sight-distance line (pistol toy and kid, SMG: "the sight sits
  0.307 in front of the eye", the test room's number, now each gun's own on purpose) and one is real: aimed, the
  SMG's support palm is 7.3 cm from its hold point (the check asks under 7; its arm runs out of reach). Hold 81 of
  81, ammo 210 of 212 (the same two SMG lines as before), four scenes 0 errors.
- **Seen in the stills** (`docs/stills/hands_d0078.jpg`): the pistol hand shows at the bottom when aimed. On the SMG
  only a sliver of the gun hand shows, and its folding stock comes into the bottom left. Shotgun side view: the
  support hand sits on the fore-end. Stills do not show any clipping a person would notice at the game's size.
- **His calls:** is the smaller aimed pistol worth the hand in view (0.55; set back to 0 = the test room's 0.307);
  the SMG: further out (more hand, stock in view, support hand slides back) or back to 0.307; the SMG's 45-degree
  roll on aim (unchanged, it decides where the SMG hand lands); the ammo probe's two SMG lines (unchanged);
  third person is not worse anywhere.
- Not done: nothing played; the Foam Finger and Toy Knife; the full Workshop rebuild was not run (the narrow Fit
  Weapon Holds was).

## 19 Nov 2026 10:4x — DARTS LAND ON THE DRAWN BODY, AND THE DART IS A FOAM DART (D-0080) — NOT PLAYED
His pick of 19 Nov from the D-0079 list: the hover in front of figures (way 1 of the two), and the dart's shape.
Built with the editor closed the whole time.
- **Shots land on what is drawn.** On the four figure targets (practice skittle, practice bear, toy soldier,
  mischief soldier) a dart, a shotgun pellet, a paint ball and a paint drop land on the drawn body itself and fly
  on where nothing is drawn. The crosshair does the same: a blaster aims at the drawn body, not at the capsule.
- **How.** Each of the four has a new part, Hit Shapes (`Combat/HitShapes.cs`), which names its hit shape: the
  drawn mesh itself, as a mesh collider on the model (the pack ships one on every model; our builders had it
  switched off). The test room fits a box to each body part, at 0.9 of the part's size, because its dummy bends
  at the joints. Ours are rigid toys, so the mesh itself is the fit: no box corners, nothing to tune. The hit shape
  sits on the model, so it rocks with the bear and tips over with a soldier.
- **What you bump into is the same as before.** The bear's and the skittle's capsule and the soldiers' walking
  capsule are still there, and they are all that the player, cars, crates and the soldier's own legs feel. A hit
  shape touches nothing (it is shut out of every contact on the collider itself: no new layer, nothing under
  ProjectSettings). A shot tells the two apart by the Hit Shapes part: the capsule of a thing that has hit shapes
  is "body only", and shots go through it.
- **While a target is down** its hit shapes are off (the test room's rule): a shot passes over it, as before.
  They are back when it stands up, or at F9.
- **New, and worth a look in play:** a soldier's rifle, hands and boots are drawn, so they can be hit now (a dart
  sticks in the rifle, and it counts as a hit). Before, a shot at the rifle flew through, and a shot beside the
  legs landed on thin air. On the bear the ears and the feet reach outside the capsule: they can be hit too.
- **The dart is the test room's own foam dart:** a blue foam body (0.24 long, 0.075 thick) and a round orange
  head (0.107 across, 0.079 high), both glowing a little as in the test room. It is made of Unity's plain cylinder
  and ball, so it is there in any copy of the project (the red shell came from the pack). **No toy foam dart is
  in what we own:** looked through the ithappy packs in the project and the Asset Library catalogue (bullets,
  shells, a foam finger, pencils, a wooden rocket; nothing that is a dart). Its two colours are two materials
  (`Data/FoamDartBody.mat`, `Data/FoamDartHead.mat`). The streak in flight is unchanged. The old capsule is still
  in the prefab, off; it shows only if the shape is taken away.
- **MEASURED against the drawn meshes** (probe kept: `%TEMP%\shiftjam-stage\weapons\bodyhit\probe-keep\BodyHitProbe.cs`,
  run with a graphics device; it reads each figure's triangles before play and knows nothing of the hit shapes).
  The number is where a stuck dart sits against the drawn surface on its own line, with a plain wall's own
  distance taken off (measured: its middle 0.1485 out from a wall). Above 0 = that far out in the air in front
  of the body. Four shots a figure: chest, head, low and off the middle (a leg), chest from 45 degrees.
  - Skittle: before +0.216, +0.201, -0.007, +0.215. After 0.000 on all four.
  - Bear: before +0.325, +0.293, -0.038, +0.367. After 0.000 on all four.
  - Toy soldier: before +0.035, -0.066 (the head: too deep), -0.011, +0.016. After 0.000 on all four.
  - Mischief soldier: before +0.035, -0.067, -0.011, +0.016. After 0.000 on all four.
  - A near miss, 0.11 to 0.13 beside the drawn outline (a dart's own radius is 0.06): before, on all four, the
    dart STUCK in thin air. After, on all four, it flew on.
  - The point each dart is stuck by, after: within 0.0002 of the nearest drawn triangle, all 16.
  - The whole probe: **37 of 57 before, 57 of 57 after.** (One of the after-runs read 56 of 57: the probe's own
    line test slipped between two triangles on the seam down the bear's middle and read the back of its head,
    0.539. The dart was on the surface. Fixed in the probe; the runs after that: 57 of 57.)
- **The same before and after, in the same runs:** the player, pushed straight at each target, stops 0.731
  (skittle), 0.959 (bear) and 0.587 (each soldier) from its middle. The player walks across a soldier's front
  through where the rifle is drawn (3.00 in a second, as before). A soldier's body pushed forward goes 3.30 in
  1.5 s. A toy soldier left alone for 6 s wanders 5.27 before, 5.28 after. A grenade 1.5 away takes 60.3 of the
  skittle's 120, 60.6 of the bear's, all 60 of the toy soldier's and all 45 of the mischief soldier's. One dart
  with all its health in it knocks each of the four down: down, the burst shown once, the shooter told once (the
  hit marker's event), the dart rides it to the floor. While it lies there a dart on the same line passes. Stood
  up again by the reset, a dart at the chest sticks 0.000 from the drawn surface (before: 0.03 to 0.33 out).
- **Paint on a figure:** a paint ball at the chest counts and lands on the drawn surface: 0.000 on each soldier
  (before 0.033 out), and within 0.03 on the skittle and the bear, read while they were still rocking from the
  hit (before 0.197 and 0.342 out). Paint on a figure now sits as it does on any model in the room: a thin coat
  on the surface it hit. Not looked at in a picture.
- **Kept checks after:** the dart check 50 of 50 (with a graphics device), the shot check 64 of 64, the grenade
  check 38 of 38, the four-scene smoke run 0 errors in each scene. One line of the dart check was changed with the shape: it
  now expects the new dart (nose 0.1822 ahead of its middle, 0.3010 long over all; it was the shell's 0.1683 and
  0.2376). Its copy as it was is in `bodyhit\snapshot\DartProbe.d0079.cs`.
- **SEEN IN THE STILLS** (`docs/stills/darts_d0080.jpg`, before beside after; stills are not in git): before, the
  darts hang in the air in front of the skittle and the bear, and beside a soldier's leg. After, every dart's
  orange head is in the body: forehead, nose, belly and foot of the bear; face, rifle hand, arm, belly, thigh and
  shin of a soldier. **Faults I see:** (1) a stuck dart shows mostly its blue body; only a thin ring of the orange
  head stays out of the surface (the test room's numbers do that). (2) On a soldier the dart is big: its body is
  about as thick as his arm (it is the test room's size, and the same size as on the bear). (3) The orange head
  looks dull brown-orange in the close-ups, not bright. (4) The dart's back end is a flat blue disc. (5) From
  over the shoulder, 6 away, a stuck dart is a small blue dot: easier to pick out than the red shell was, still
  small. The grey block behind the skittle in its two stills is the probe's own wall, far behind.
- **Where each thing is.** The hit shapes: each target's prefab > Hit Shapes (Shapes = the model's mesh collider;
  Off While Down). Made by Shift Jam > Workshop > Fit Hit Shapes (targets) (`Debug/HitShapePieces.cs`), which
  changes the four prefabs in place; the full builders call the same code. The dart's sizes: `Debug/DartPieces.cs`
  (BodyLength, BodyRadius, HeadAhead, HeadRadii), written to `Prefabs/Combat/FoamDart.prefab` > Shape > Body and
  Head by Build Foam Dart (a rebuild writes them over the prefab's: change both). The two colours: on the two
  materials (a rebuild keeps them).
- **NOT CHECKED:** nothing was played, seen moving, or heard. No shot through a real gun at a WALKING soldier
  (the hit shape follows it a physics step late, as its capsule always did). Shotgun pellets and the SMG on the
  new shapes only through the kept dart check's bear and soldiers. Paint on a figure not seen in a picture. A
  Windows build (a mesh collider on a pack model that is squashed for a moment has not been run in a build; the
  room's furniture uses the same kind of collider). A project without the packs (there the hit shapes are empty
  and shots land on the capsule as before: read from the code, not run). The full Workshop rebuild (it now calls
  the hit-shape fitter: compiled, not run; the two narrow builders were run). First person, kid size, a real pad.
- **KNOWN, LEFT AS IT IS, HIS CALL (read from the code, none of it run or seen):** (1) checks that are not shots
  still see a hit shape where it reaches out of the capsule (a soldier's rifle and boots, the bear's ears and
  feet): the camera can pull in for a rifle between it and the player; a grenade can bounce off a rifle; a knife
  or a fist can connect with the rifle. The player's own "is there room" checks (standing up, growing, stepping
  out of a car, first person) were told to skip hit shapes, so a rifle should never block those: one line in
  each of five files, compiled, not run. (2) A hit on the rifle counts as a hit on the soldier.
- **FOR HIS EYE (each is one number or one line):** how deep the head sits when stuck (Tip Length 0.1485 on the
  prefab; about 0.10 would leave the whole head outside); the dart's size on a soldier; blue and orange against
  the old red; the glow; whether a hit on the rifle should count.
- **TO PLAY:** Workshop or Colour Corner. Shoot the bear in the belly, the head and a foot, and just past its
  side. Shoot a soldier in the chest, the helmet and the rifle. Walk into each. Knock one down and shoot where it
  lies. T to paint and hit the bear. Throw a grenade at a soldier.

## 19 Nov 2026 09:5x — HIS PLAY OF 18 NOV 21:53 READ; THE FOAM FINGER'S DROP ERROR FIXED (`f73a895`, one file, no decision number) — COMPILED OUTSIDE, NOT RUN IN UNITY
- **The play** (`docs/PLAYLOG.md`, `Playtests/play-20261118-215304.txt`): 1:08:11 on D-0079, the window away for
  1:01:22 of it; the Workshop and 1:11 of the Greybox; 127 darts stuck, 3 grenades, 6 reloads, 4 shifts, 2 races
  started, first person once; 5.7 ms a frame on average, one frame over 100 ms; no F12 notes. He has not said how
  the stuck darts looked. So D-0079 is PLAYED; what he thought of it is not known.
- **Its one error** (66:54.7): "Concave Mesh Colliders are not supported when used with dynamic Rigidbody
  GameObjects", in the same tenth of a second as taking the Dart Shotgun with both hands full. The gun in hand was
  the Foam Finger, dropped to make room. Its pack model (`_Imports/ithappy/Battle_Stadium/Prefabs/Other/
  foam_finger_001.prefab`) carries a concave mesh collider, which the weapon prefab has switched off; but
  `WeaponLooks.SetLoose` switched on every collider under a dropped weapon. Unity ignores such a collider on a
  moving body, so the game went on as normal (the finger landed on its own box): the only harm was the error line.
- **Fix:** `Combat/WeaponLooks.cs`: on a dropped weapon a concave mesh collider stays off; a weapon with nothing
  else still gets the fallback box. The other four weapons' models carry no mesh collider.
- **Checked:** compiles outside Unity, 0 errors. Not run in Unity by itself (C: had 3.2 GB free at the time, so no
  run was started); D-0080's runs an hour later compiled it in Unity with the rest, and its four-scene smoke check
  had 0 errors, but no check drops the Foam Finger. To see it: Workshop, Foam Finger in hand with a second weapon
  carried, take a third: no red line in the Console.
- **The disk:** C: had fallen to 3.2 GB free (his OBS drawing recordings). November's recordings are now in
  `D:\Videos` and OBS records there; C: has 37 GB free. Old builds `Builds/Windows/v5` (0.5 GB) and `v6` (0.4 GB)
  are still on disk; v5 is on the list of things he may let go.

## 18 Nov 2026 13:5x — DARTS STICK, AND MORE AT THE LANDING (D-0079) — NOT PLAYED
His "the missing foam darts and lack of juice when landing darts or paintballs", "proceed". Built with the editor
closed (it was open until about 13:25; nothing was written into the project before it closed).
- **Darts, the test room's rule.** A dart stays where it lands for 8 s, its front end 2 cm into the surface,
  pointing the way it flew, and it rides with what it hit: it rocks and topples with the practice bear, tips over
  with a soldier, flies off with a loose crate. When its time is up it drops off.
- **Darts, ours on top.** It quivers as it lands (12 degrees about its tip, three swings, still after 0.4 s). It
  drops off instead of vanishing: backs out, falls and shrinks away over 0.35 s (the test room's just vanishes).
  No more than 40 are stuck at once: one more, and the oldest lets go. It lets go when what it rode is destroyed,
  switched off or hidden (a wrecked car's body). It does not stick to a player: a dart in your own body would
  hang in front of the first-person view, so on you it is gone at the hit, as before. Nothing on a stuck dart
  can be hit or stood on. F9 in the Workshop and the paint's clear take every stuck dart.
- **The dart's shape** is the pack's `Bullet_Shotgun` (`_Imports/ithappy/Cartoon_Weapons/Prefabs/Bullets`), one
  shape for every gun, as the child "Shape" of `Prefabs/Combat/FoamDart.prefab` (a per-gun shape is a prefab
  change). **Seen in Unity it is a red shell with a grey rim**, not the "grey body with a striped head" the 12:2x
  entry below said (that was a Blender render with the texture laid on by hand: wrong). Scaled 3.725 times: 0.24
  long (the test room's dart body; the capsule was 0.18), 0.05 thick, the rim 0.067 across. **The grey rim leads
  in flight and is the end in the wall**: it reads as a toy dart's rubber head; on a real shell that is the back
  end. One line flips it (`Debug/DartPieces.cs` > `WideEndLeads`, then Shift Jam > Workshop > Build Foam Dart).
  The old capsule is still in the prefab, switched off: a project without the pack (a fresh clone) shows it
  instead. It is red, not glowing orange as the capsule was: from behind the gun a dart stuck 14 away is a few
  pixels. The streak in flight is unchanged.
- **FOUND HERE, FIXED IN D-0080 (19 Nov, above): a dart hovers in front of a figure.** A dart sticks on the surface of the thing's
  hit shape. The bear's, the skittle's and both soldiers' hit shape is one capsule, wider than the drawn body.
  From the stills, by eye: about 0.25 to 0.5 in front of the bear, 0.05 to 0.2 in front of a soldier. On walls,
  floors and crates it is exact (measured: 0.0000 from the wall's face). The test room has no such gap because
  its dummy's hit boxes are fitted to each body part (`dummy.gd`, `box_fit` 0.9). Two ways: (1) port that: hit
  shapes fitted to the drawn body on the four targets, so shots also stop landing on thin air beside them; it
  changes what the player bumps into, and a soldier's walking capsule has to be kept out of the shots' way;
  (2) leave the hit shapes and give darts alone a coarse map of each drawn body. I would do 1, as its own piece.
- **More at the landing (ours, unless said).**
  1. **At your ear:** a short sound when one of your shots lands on something with health, and another, bigger
     one for the hit that knocks it down. Not placed in the room, so it is as clear at any range. A shotgun's 8
     pellets make one sound (hits less than 0.05 s apart count as one). Paint and a grenade's blast count too.
  2. **Bits:** on something with health a dart throws 14 warm yellow bits of 0.05 in place of the wall's 8 pale
     blue ones of 0.035, and a white ring of 16 small bits widens from the spot to 0.35 in 0.18 s, flat across
     the shot so you see it face on.
  3. **A target going down** (bear, skittle, toy soldier, mischief soldier; whatever knocks it down: dart, paint,
     grenade, fist): a burst of 16 yellow bits thrown up from the last hit, a squash of 0.30 in place of its
     usual 0.15 or 0.10 (one squash, not two), and a thud where it stands. **The thud is the test room's**:
     found while building, its dummy and its target crate each play a sound as they go down (`dummy.gd`,
     `target.gd`: `down_sound`). The 12:2x entry said the test room had no more at a landing than Shift Jam: wrong
     on this one.
  4. **Paint:** a splat was already spreading from 35 % to its full size in 0.08 s (the test room's), with no
     overshoot. Now it swells to 115 % and settles back in 0.12 s, and so does its shell. Paint keeps its own
     splat, bits and sounds: no ring, nothing sticks.
  5. **The camera, on a knock-down only:** third person, a brief shake (0.3 of the strongest, 0.2 s: measured up
     to 0.33 degrees off its line); first person, the view kicks up and springs back (measured 1.09 degrees; a
     pistol shot's own kick is 1.2 on the dial). No freeze-frames.
  6. **The play log** writes "ammo loaded: Paint" when T changes the kind (once a frame at most) and counts
     "darts stuck" (yours only).
- **Where each number is.**
  - The dart: `Prefabs/Combat/FoamDart.prefab` > Foam Dart: Stick Seconds 8, Tip Length 0.1485 (the test room's
    0.15 x 0.99), Quiver Degrees / Seconds / Swings, Most Stuck 40, Let Go Seconds 0.35, Let Go Fall 9; and under
    Feedback: Target Burst Amount 14 / Colour / Speed 4 / Size 0.05, Ring Amount 16 / Colour / Reach 0.35 / Size
    0.02 / Seconds 0.18. 0 switches each off. The shape's size (0.2376 long, nose 0.1683 ahead) is in the
    builder, `Debug/DartPieces.cs`. **A rebuild writes the builder's numbers over the prefab's**: change both.
  - The ear, the nudge, the knock-down: `Resources/WeaponAmmo.asset` > "A shot landing on something with health"
    (Landed Clips, Landed Volume 0.5, Downed Clips, Downed Volume 0.8, Landed Pitch Spread 1.06, Landed Gap 0.05,
    Down Nudge 0.3, Down Nudge Seconds 0.2, Down View Punch 1.5) and "A target going down" (Down Bits 16, their
    colour, speed 6, size 0.08, fall 9, seconds 0.5; Down Squash 0.3 over 0.3 s; Figure Down Clip, Target Down
    Clip, Down Volume 1).
  - The splat's pop: `Resources/PaintSettings.asset` > The splat > Spread Pop 0.15, Spread Settle 0.12 (they show
    in the Inspector; the file itself gets the two lines the next time Unity saves it).
- **Sounds: picked by name and by length, NOT by ear.** Case Portman's two packs, copied to the git-ignored
  `Assets/_Imports/CasePortman/Weapons`: `ear_hit_1`, `ear_hit_2` = Highlight_01, _02 (Cute & Cozy UI; 0.08 and
  0.09 s); `ear_down_1`, `ear_down_2` = Currency_02, _03 (the test room's score sounds; 0.37 and 0.42 s);
  `figure_down` = Impact_Body_01 (the test room's dummy_down); `target_down` = Impact_Powerful_01 (its
  crate_down). A project without them is silent at these points and runs the same.
- **CHECKED** with a temporary probe (kept: `%TEMP%\shiftjam-stage\weapons\darts\probe-keep\DartProbe.cs`), Colour
  Corner, a bare floor, hidden: **50 of 50; on the code before, 11 of 47** (3 checks need the sounds, which were
  not in the project yet). Measured: a dart still in the scene 1.05 s after landing, its middle 0.1483 back from
  the wall, its nose 0.0219 in and its tail 0.22 out, 0.17 degrees off the way it flew; the quiver up to 10.1
  degrees with the tip not moving; still stuck at 7.6 s, gone at 8.35 s after dropping 0.55 and shrinking to
  nothing; a second dart down the same line lands on the wall 0.011 from the first (its own fall); 6 shotgun
  blasts = 48 darts stuck in all, never more than 40 at once, none of the first blast's 8 left; an SMG magazine =
  20 hits, 20 stuck, 20 sounds at the ear; a blast of 8 pellets = 1 sound; on a loose thing that went 5.05 in
  0.6 s the dart went 5.05 and moved 0.00000 on it; switched off or destroyed, the dart lets go; on a thing
  (5, 4, 0.5) squashed to half its height the dart keeps its size (1, 1, 1) and its tip follows; on the bear's
  rocking part 0.0006 of drift, and 0.0001 after it toppled 90 degrees (the dart 1.56 from where it stuck); the
  same on both soldiers; ClearAll and the paint clear leave 0; a dart on the player does not stick; a kid's dart
  is 35 times the size and sits 0.1483 kid sizes back; with its shape taken away the capsule shows and it still
  sticks; 8 bits on a wall, 14 and a 16-bit ring on health (ring 0.348 wide, flat within 0.0003); the bear down:
  30 bits, squashed to 0.70, 1 sound at the ear and no hit sound, 2 in the room, 1 nudge, the marker orange;
  each soldier down by a blow: 16 bits, 0.70, 1 thud, nothing at the ear; the splat 1.150 of its size at its
  peak; 0 errors. Kept probes after: shot 64 of 64 (no check of it had to change), grenade 38 of 38, hold 81 of 81, ammo 210 of 212 (the same two SMG lines, word for word), first person 160 of 160, four-scene smoke 0 errors: each the same as before this piece (the counts of 09:4x today).
- **NOT CHECKED:** nothing was played, seen moving, or heard (every run hidden and muted). The play log's two
  lines (a hidden run records nothing: compiled only). A grenade's blast heard at the ear (the same
  listener, not run). A project without the pack: only "the shape taken away"
  was run, not Unity loading the prefab with the pack's model missing. A full rebuild through Shift Jam >
  Workshop > Build Combat & Vehicle Pieces (it now calls the dart's own maker: compiled, not run; Build Foam Dart
  itself was run). Darts on cars, on the skittle, on a grenade in flight, on a soldier walking. The ring and the
  bits at kid size. A real pad. First person beyond the view kick (a dart seen landing from the eyes). The
  leftovers probe of D-0075 (it picks "the dart in the scene" and may now pick a stuck one) was not re-run.
- **TO PLAY:** Colour Corner or the Workshop. Shoot a wall from the side and up close; empty an SMG magazine into
  the bear; knock the bear and a soldier down; shotgun a wall; T to paint and hit a soldier; first person (V) and
  knock something down.
- **FOR HIS EYE AND EAR (each is one number or one line):** which end of the shell leads (the grey rim now); the
  dart's size (0.24 long) and colour (red, no glow); the hover in front of figures (above); the cap of 40; the
  drop-off (0.35 s) against vanishing; no darts on the player; the quiver; the six clips, none heard; the two
  volumes (0.5, 0.8); three sounds at once on a knock-down (the dart's landing, the thud, the one at your ear);
  the nudge's size in both views; the yellow of the bits and whether the ring reads; the splat's 115 %.
- **Stills** (outside the repo): `%TEMP%\shiftjam-stage\weapons\darts\stills` (19): the dart in flight from the
  side, darts in a wall, the shotgun's pattern, a landing on a wall and on the bear, the bear's knock-down, darts
  on the bear and on a soldier from the side (the hover), a kid's dart.

## 18 Nov 2026 12:2x — CHECKPOINT: the usage limit cut "darts stick + more at the landing" BEFORE ANY CODE (his "proceed"; nothing built, nothing in Assets touched)
- **His play, 12:01 (the play log's first real row, `docs/PLAYLOG.md`):** 9:38, about 2 minutes active in Colour Corner; first person twice, 5 grenades, 10 SMG reloads, 5 knock-downs, 0 errors, 0 frames over 100 ms. His words: "those feel better" (read as the throw, the reload and the hands), and what stood out: "the missing foam darts and lack of juice when landing darts or paintballs".
- **Found by reading:** a dart goes back to its pool at the hit (`Projectile.Update` > `Recycle`); the test room's sticks 8 s and rides with what it hit (`foam_dart.gd`: `stick_time` 8, `tip_length` 0.15, a target's `stick_parent`). The dart's shape is Unity's capsule, 0.05 x 0.18. A landing shows 8 bits of 0.035 for 0.45 s, a placed sound, four ticks, a squash of 0.1 to 0.15. The T key (ammo kinds) IS in Colour Corner (`AmmoBelt`); only bubbles are missing.
- **The plan he said "proceed" to (A and B together, one builder):** A. darts stick 8 s, tip in, ride with what was hit, a quiver on landing (ours), a cap on stuck darts with the oldest gone first (ours), a real dart shape. B. (all ours) a sound at the player's ear for a hit on a target and a bigger one for a knock-down; bigger, brighter bits on a target than on a wall and a short ring; a burst and a pop when a target goes down; the paint splat popping in (check first whether it already does); a small camera nudge on a knock-down only; no freeze-frames. Every amount a number on the ammo.
- **His "we might have the assets already in the ithappy packs": checked.** `_Imports/ithappy/Cartoon_Weapons/Prefabs/Bullets` holds seven toy bullets (Bullet_Pistol, _SMG, _Shotgun, _AR, _MG, _SR, _FlareGun), one material `Bullets.mat`, striped orange and tan heads. Real-bullet size (the pistol's is 8.5 mm across and 7.8 long; the shotgun's 17 x 64 mm, a grey body with a striped head, the most dart-like; the flare gun's 31 x 108 mm), so they need scaling up to read. No foam dart by name in any ithappy pack; `Foam.mat` in the weapons pack is used by none of its prefabs. Rendered in Blender, not seen in Unity: `%TEMP%\claude-dart\bullets.png`.
- **NOT DONE:** everything in A and B. No probe written, no builder started. The editor was open (Workshop) at 12:11.
- **Still open behind this:** D-0078 first-person hands clipping (plan given, kept on the list); the walk and run sheet with a long gun; one set of movement numbers for both views (not compared); D-0072 rifle, sniper, bubbles.

## 18 Nov 2026 09:4x — HIDDEN CHECK RUNS ARE SILENT NOW (his "why do i keep hearing some shift jam sfx?"; no decision number, one line of the game)
- **What he heard:** the headless checks of today's grenade build, on his speakers. Each check mutes itself as the
  play starts; `PlayerOptions` sets the saved volume back in the same start-up step, and Unity runs the two in no
  fixed order. So every headless check of this game could be heard, not only today's.
- **Fixed in the game:** `Core/PlayerOptions.cs` keeps the listener at 0 in any batch run (and the smoke test, as
  before). His own plays and a build are untouched: they use the saved volume.
- **CHECKED** with a probe that does not mute itself (kept: `%TEMP%\shiftjam-stage\mute\probe-keep\MuteProbe.cs`):
  the listener's volume one second into a hidden play was 1.00 before, 0.00 after. Not checked by ear.

## 18 Nov 2026 09:4x — GRENADES, THE TEST ROOM'S (D-0077) — NOT PLAYED
His "Testing Zone ... has grenades", "1" (port them), "proceed". Built headless, no editor open.
- **What you get.** G throws a grenade with the free (left) hand. The arm swings up and forward over 0.4 s; the
  grenade leaves 0.18 s in, from just in front of the chest, tilted up a little above where you look, and it takes
  your own speed with it. 0.8 s between throws. 3 carried, 3 the most. It bounces, skids, rolls, and goes off 1.5 s
  after it leaves the hand. Works with a gun out or with empty hands, in third and first person. Not while a move
  is under way (roll, block, hit), not with a prop in the hands, not in Build Mode, not in a car.
- **The blast:** 90 at the middle (the test room's 6), falling to nothing at 4.5 away. A body made of several
  parts is hit once, by its closest part. Loose things are thrown outward and up, the same speed whatever they
  weigh (9 at the middle). Two bursts of bits (bright, then smoke), a flash of light that fades in 0.18 s, the
  boom. A hit on something with health flashes the hit marker. As in the test room: it reaches through walls, it
  hurts you too if you stand in it (you have 100), and it throws loose things only (it does not shove a player).
- **More grenades:** a supply crate fills them to 3 (the Colour Corner has one). An ammo box gives none (the test
  room's rule). F9 in the Workshop gives the 3 back (ours: the Workshop has no crate).
- **On screen:** "Grenades  3" above the rounds on the weapon read-out, dimmed at 0. **It shows only while a
  blaster is out**, like the rest of that read-out, and so does the hit marker. The test room shows the count
  always. His call.
- **Size.** A kid's grenade leaves from the kid's chest, is 35 times as big and as fast and falls 35 times as
  fast, so the arc is the same shape and takes the same time. The fuse and the damage stay. Its blast reaches 35
  times as far: 157 units (the test room's kid is 6 times, so 27 m there). Loose things are thrown the square root
  of 35 harder (5.9 times). **The 157 is flagged for his veto** (`Grenade` > Size To Thrower turns all of it off).
- **The flight is worked out in our own code, not by the physics engine** (`Combat/BallFlight.cs`). Tried the
  engine first, three ways, and measured each beside the test room's ball run headless in Godot: it went off 8.2
  away (the ball sank into the floor and came out spinning backwards), 14.3 (it bounced without the floor
  gripping it) and 12.7; the test room's goes 10.7. Now: 10.67 at toy size, 10.60 body sizes at kid size, the same
  whatever `Physics.gravity` is (-60 in play, -9.81 and -20 tried). The grenade falls by its own number (30).
  What made the test room's ball stop where it does was found by measuring: its engine lets nothing spin faster
  than 47.12 radians a second, so a ball that lands fast skids before it rolls; it adds 0.1 of drag to
  everything; it does not bounce a landing slower than 1. All of that is data on the prefab.
- **Where the numbers are.** The thrower's: `Resources/Grenades.asset` (carried, most, throw speed 16, loft 0.35,
  the release point, wait 0.8, the arm's swing, the count's text and colours). The grenade's:
  `Prefabs/Combat/Grenade.prefab`: `Grenade` (fuse, reach, damage, push, lift, the bits, the flash, the sounds) and
  `BallFlight` (fall 30, bounce 0.35, grip 0.9, drag 0.6, spin drag 2.1, most spin 47.12). Lengths on the body are
  the test room's x 0.99; speed, fall and the blast's reach are number for number (D-0071's rule for flight).
  Rebuild: Shift Jam > Workshop > Build Grenade (run in a batch for this). The player gets its thrower from the
  WeaponHolder at start: no scene was changed. Sounds (boom x2, throw) are in the git-ignored `_Imports`.
- **Pad: hold the right stick in (crouch), press the left stick in (run).** Every button was taken. This pair's
  own jobs are both "hold", so the press that throws does nothing else, but you crouch while you do it. The
  other order throws nothing. His call.
- **The play log** gets "grenade thrown, 2 left", "grenade blast hit 2 things with health, and knocked one down"
  or "hit nothing", and counts grenades thrown. The key sheet (H) has the G line.
- **CHECKED** with a temporary probe (kept: `%TEMP%\shiftjam-stage\weapons\gren\probe-keep\GrenadeProbe.cs`),
  Workshop, a bare floor: 38 of 38; on the code before, 1 of 38 (only "no errors" passed). With the camera at the
  test room's rest pitch (12 down), toy: leaves 1.436 up, peaks at 1.500, goes off 10.67 away 1.51 s after the
  hand; kid, in body sizes: 1.435, 1.500, 10.60, 1.52. The test room's ball, run again today in Godot the same
  way: 1.45, 1.517, 10.73 (its STATUS says 1.49, 1.55, 10.6; the probes that measured those are gone from disk).
  Also passing: G by a made-up key press; the release at 0.18 s; the hand forward of its shoulder at the release;
  3, 2, 1, 0 and nothing at 0; nothing inside the 0.8 s wait; the read-out's line and its dimming; box, crate,
  full crate; 90 at the middle, 45 at half reach, 0 just past the edge; one hit for a three-part body (70, not
  150); a loose crate of weight 4 leaving at 5.83 and 0.6 up for 1 out, and 5.92 times faster at kid size; the
  hit marker; the pad pair on a made-up pad; no errors. Kept probes after: hold 81 of 81, shot 64 of 64, ammo
  210 of 212 (the two SMG lines, the open call of D-0075), first person 160 of 160, the four scenes 0 errors.
  Outside compile 0 errors.
- **Stills** (`%TEMP%\shiftjam-stage\weapons\gren\stills`, 5, from the free-hand side): the hand wound up beside
  the head; the arm thrown forward with the grenade just past the fingers; the grenade a small red dot in the air
  3 to 4 ahead; the flash on the floor 10.6 away with the bits still one pale lump; a tenth of a second on, the yellow bits
  spread up and out over the smoke, the flash still on the floor. The smoke stays a tight pale ball in both.
- **NOT CHECKED:** nothing played, nothing heard (every run muted). First person (the same code, not run). A
  throw while walking, running or jumping. A bounce off a wall, a step or a slope (only a flat floor was run). The
  blast on the game's own soldiers, bears and cars, and on the player. A real pad. The throw with a long gun in
  both hands (the free hand leaves the gun and comes back: not looked at). The kid's blast seen (its flash is made
  1225 times brighter to carry 35 times further: worked out, not looked at). The play log's lines (a batch run
  records nothing). The fingers: the hand stays open through the throw, as in the test room.
- **Known, small:** the smoke does not rise (the test room's does): every falling bit in the scene shares one
  pull, so rising smoke would lift the bright bits too. In the air the grenade cannot be knocked about (a dart or
  a car does not move it; it lies there as a fixed bump until it goes off).
TO PLAY: Workshop. Take a gun from a stand (the count shows only with a blaster out). Press G, three times, at
the practice bear and skittle. Stand close once: it hurts you. F9 gives the 3 back. Then F to grow and
throw one as the kid (the 157 reach is the thing to judge). In the Colour Corner walk into the supply crate with
fewer than 3. F12 where it looks or sounds wrong.

## 18 Nov 2026 08:5x — THE RELOAD BESIDE THE TEST ROOM'S, PICTURE BY PICTURE: NO DIFFERENCE FOUND IN A STILL (no code changed)
His "Testing Zone ... has grenades, a strong reload anim, proper IK animation for hands and wrists", then "1"
(port them) and "proceed". Grenades are the entry above (D-0077). The reload and the arm code were ported on 16 and
17 Nov (D-0066 to D-0075); his last play (17 Nov 12:24 to 13:00) was before D-0074 and D-0075 landed.
- **The sheet owed since D-0070 is made:** `docs/stills/compare_godot_unity_reload.jpg` (git-ignored folder): pistol,
  shotgun, and the test room's rifle beside the SMG, from the side and the front, at 20 %, 50 % and 80 % of a reload.
  Shift Jam's side was shot today with the kept ammo probe (`-ammoStills`); the test room's on 17 Nov.
- **What the stills show:** the same pose on both sides at each moment: the gun in front of the chest, barrel
  tipped up, the free hand under it at the magazine, the magazine gone in the middle frame. The numbers are the
  same too: tip 25 degrees, roll 35, magazine out 25 to 45 %, back 55 to 75 %, gone 44 to 56 %, drop 0.198 (0.2 x
  0.99), pistol 1.3 s, shotgun 2.2 s. The arm code has the test room's wrist limits (roll 80, bend 55), forearm
  share 0.6 and stretch 0.985.
- **What differs in the pictures, none of it the reload:** the body (the test room's girl, Shift Jam's boy, whose
  head sits chin-up in every still, at rest too: the model's idle); the probe's bare stage; the test room's stills
  show its HUD (RELOADING, Grenades 3), Shift Jam's are a camera render without the HUD.
- **So what he finds weaker is not in a still.** It is in motion, in sound, or it was the state before D-0074 and
  D-0075. His play of those two is the test (the play log records it; F12 where it looks wrong). Still known and
  open: the SMG's hand holds still while its magazine slides through it; the SMG rolls 45 degrees on aim.
- **NOT DONE:** the hold-in-motion sheet (walk and run with a long gun, both engines): still owed.

## 18 Nov 2026 08:3x — THE PLAY LOG: EVERY PLAY WRITES ITSELF DOWN (D-0076) — NOT PLAYED
His "create a script that automatically logs my playtests", Shift Jam first. Built headless, no editor open.
- **What it does.** Press Play: a record starts. Stop: it closes. Nothing else to do. The row goes to
  `docs/PLAYLOG.md` (made by the first play; commit it with the work), the detail to `Playtests/` beside Assets
  (git-ignored). A row: when, how long (with idle, paused and away time), what the project was at (D-number,
  commit, uncommitted count), each scene and the time in it, notes, errors and warnings, frame time, counts.
- **What it records of the game:** shifts, checkpoints, resets, goals, switches, doors, the gun in hand, reloads,
  ammo taken, knock-downs, first person and build mode, race laps; counts of shots landed and dry clicks; the
  player's place as it moves (`Scripts/Debug/PlayLogEvents.cs`). Grenades and the rifle get their lines when they
  are ported.
- **His notes: F12.** A numbered note with the room, the size and the place, and a still beside the record. Or the
  console (`) and `note <words>`. Say the words afterwards by number.
- **CHECKED** with a temporary probe (kept: `%TEMP%\shiftjam-stage\playlog\probe-keep\PlayLogProbe.cs`), one play
  of 46 s in the Workshop then Colour Corner, stopped the way a play is stopped, read back from the files: 33 of
  34; run first without the logging switch, 29 lines failed and no file was written (a batch run records
  nothing, as meant). Passing: both scenes with
  their times, a shift, a knock-down, counts (shots landed 4), an error logged twice told once, a warning, note 1,
  F12 making note 2 through the console, 33 s without a key told as idle and its end, the place as the player
  walks, "D-0075, 648761f (main), 23 uncommitted", the end lines, the row's nine cells, a planted record that
  never closed getting its row, a play of 2 s leaving nothing. Outside compile 0 errors.
- **NOT CHECKED:** F12's still on disk (the 1 of 34: a hidden batch run draws no picture to save; in his editor it
  is the same call the console's `shot` has always used); the editor's pause and the window losing focus in a
  real editor (the code is there, a batch run has neither); a development build; a virtual player being skipped.
  Nothing played.
TO PLAY: any play at all. Afterwards open `docs/PLAYLOG.md`: one new row. Press F12 once in it and look for the
still in `Playtests/`.

## 17 Nov 2026 16:5x — THE THIRD-PERSON LEFTOVERS: THE RELOAD HAND IS ON THE MAGAZINE, THE DART'S STREAK, THE MOVES AT 0.99 (D-0075) — NOT PLAYED
The second half of his "polishing needed for FPS and TPS" (first person is the entry below). Built headless.
- **The free hand holds the magazine in a reload, the SMG's too.** Measured first: with the magazine pulled out, its
  bottom hangs lower than the free arm reaches, on every gun (each arm at its full length). A hand sent to a point
  it cannot reach stops short on the way there, so it hung in the air beside the magazine: 5.9 cm off the pistol's,
  6.9 off the shotgun's, 9.3 off the SMG's long one (the "12 cm" of D-0071 was measured to the magazine's bottom).
  Now the hold slides up the magazine, back towards the gun, as far as it takes for the arm to reach, never higher
  than the magazine has come out (`ArmAim.MagazinePalm`; no new number). The palm is 1.5 cm from all three
  magazines: the hand is round the magazine, with its lower end showing below (7, 10 and 15.5 cm of it). The gun
  stays where the test room holds it. First person uses the same code (there the arm reached already).
  **What it costs the SMG:** its magazine's bottom is at the end of the arm even before it comes out, so the hand
  now waits there while the long magazine slides down through it: it goes 1 cm down with the magazine, where it
  went 4 cm down beside it. The pistol's and the shotgun's hands still go down with theirs (7.9 and 5.3 cm).
- **The dart has its bright streak** (the test room's: 1.6 long, 0.045 thick, pale blue, 0.75 solid, the same from
  the dart to its tail). It starts at nothing as the dart leaves the barrel and is gone when the dart lands. It is
  data on the ammo: `Prefabs/Combat/FoamDart.prefab` > Feedback > Streak, Streak Length 1.584, Streak Width 0.0446,
  Streak Colour, drawn by a Trail Renderer on the prefab's "Streak" child with the paint streak's material
  (`Data/PaintStreak.mat`, unchanged). A kind of ammo without a Streak draws none; paint keeps its own. The toy
  soldiers fire the same dart, so theirs have it too. `WorkshopPieces` builds the dart with it.
- **D-0064's move lengths are the test room's at 0.99** (they were at 0.9375, 5 % short; `Resources/PlayerMoves`):
  a roll carries 3.37 (was 3.19): Roll Distance 1.87 body heights (was 1.77). A punch's hit ball: reach 0.4125,
  height 0.6875, width 0.2475, shove 2.2 (were 0.39, 0.65, 0.234, 2.1). A kick's: 0.495, 0.44, 0.33, 4.95 (were
  0.47, 0.417, 0.3125, 4.7). The crouch walks at 1.67 (was 1.65): the crouch clip's own pace (1.046) at the legs'
  cap of 1.6, so the feet match the floor. The defaults in `PlayerMoveSettings` say the same.
  NOT changed: Crouch Height (0.833 of the standing capsule = 1.5; the test room's 1.6 m at 0.99 would be 0.88 =
  1.58). It decides what you can crouch under, so it is his call.
- **CHECKED:** a new probe (kept: `%TEMP%\shiftjam-stage\weapons\fpb\probe-keep\LeftoversBProbe.cs`), toy size,
  measured in the world: 10 of 10; on the code before, 1 of 9 (only "no errors" passed; the streak's own numbers
  can only be read once it exists; the reload hand's before-measure was taken in a second run, with only the arm
  code put back). At 43 % of a reload, the magazine 0.195 out and still drawn, the free palm is 0.015, 0.015 and 0.016
  from the magazine of the pistol, the shotgun and the SMG (0.059, 0.069 and 0.093 before), each arm at 0.98 of
  its length; a dart in flight trails a streak 1.60 long (the middle one of 183 frames of flight; 1.58 asked),
  0.0446 thick, 0.75 solid at both ends (none before); a roll carries 3.36 (3.18 before); a crouch walks at 1.670
  (1.650); a loose block hung where only the new reach touches it is knocked away by a punch (near face 1.156
  ahead, the hit ball ends at 1.188; it ended at 1.123) and by a kick (1.447; 1.485; 1.409), and was not before.
  After it: first-person probe 160 of 160, hold probe 81 of 81, shot probe 64 of 64, camera probe 64 of 64, four scenes 0 errors; AMMO PROBE 210 OF 212: its SMG line fails at toy and kid size (the cost above: the hand goes 0.011 down with the magazine, the line asks for more than 0.03); the line was left as it is.
- **SEEN (stills, mine):** the dart from the side with its streak (a thin pale line behind the orange dart); the
  three reloads from the front at 43 %: each free hand is round its magazine, the SMG's about half way up it.
  (`%TEMP%\shiftjam-stage\weapons\fpb\stills`, not in the repo.)
- **NOT CHECKED:** nothing played; the streak at kid size and from the game's own camera (seen from behind it is a
  short line); the streak with the shotgun's 8 darts; how the longer roll fits the course's gaps.
TO PLAY: Colour Corner (T until Darts) or Workshop: fire the pistol across the room and watch the dart from the
side; the shotgun's 8 streaks. Each gun, R: watch the free hand take the magazine. X (put away), then
Left Ctrl: the roll goes a little further; left mouse and right mouse at a practice target from the edge of reach;
hold C and walk.
FOR HIS EYE: the streak (length, how solid; it is the test room's); the hand on each magazine, and the SMG's long
magazine sliding through a hand that holds still (his call: the hand could take it higher from the start and be
pulled down with it, which needs one number a gun: how much of its magazine shows below it); the roll's new length where a course gap was sized for the old one; the kick's shove (4.95, was 4.7).
OWED: the hold IN MOTION beside the test room (walk and run with the shotgun, third person, 4 frames each): not
shot in this piece; the shots are listed in the weapons NOTES.

## 17 Nov 2026 16:2x — FIRST PERSON AS THE TEST ROOM'S: THE ARMS ARE ON THE GUN, RIGHT MOUSE BRINGS THE SIGHT TO THE EYE (D-0074) — NOT PLAYED
His words after playing at 12:24 today: "the polishing needed for FPS and TPS for shift jam (still lacking some of the
finesse from Testing zone)". First person first (his "proceed"). Built headless, no editor open. The new stills
beside the test room's: `docs/stills/compare_godot_unity_first_person_arms.jpg` (Godot left of each pair).
- **The arms are on the gun.** With a blaster out in first person the player's own body is drawn again, the test
  room's way: the same body, its head shrunk to nothing, stood so the head is 0.198 under and 0.119 behind the eye.
  The gun hand is on the grip with closed fingers; the other hand is under the barrel of the SMG and the shotgun; in
  a reload it goes to the magazine. The hands come on over 0.2 s as the gun is drawn. The body casts no shadow in
  first person (it would be a headless one). With empty hands, a melee weapon or the gun put away, first person is
  as it was: no body.
- **Right mouse brings the gun's own sight to the eye** in 0.15 s: the middle of the sight sits in the middle of the
  view, 0.307 in front of the eye. Each gun's sight is found from its model the test room's way: the see-through
  part of the sight on its `Place_..._Sight` mount (a sight without glass: 0.0297 above the mount; no mount: along
  the barrel). The dot crosshair hides once the sight is 0.6 of the way up; the hit marker stays; the view narrows as
  before, and a gun's own Sight Field Of View still wins.
- **The SMG rolls over 45 degrees as it comes up.** The pack's SMG carries an offset sight: it stands at a slant on
  the gun's right side, made to be used with the gun rolled over. Held level, the tilted glass sat in the middle
  with the gun beside it; rolled, the sight stands upright (`DartSMG.prefab` > Sight Roll 45). Ours: the test room
  has no SMG.
- **Looking down, the body steps back** (by 0.34 looking straight down), so the chest stays behind the gun and out
  of the view: you see your arms, then your legs and feet. Looking up, the chest leans back by 0.6 of the look (the
  test room's). If the gun is further than the arm reaches (looking steeply up), the whole body slides towards it,
  so the hand never leaves the grip. Both are ours: the test room's view stops 55 down and 80 up, this one goes on
  to 89 each way.
- **The legs follow the first-person body:** they walk, step sideways and crouch under the eye. The body is stood by
  its head every frame, so a crouch or a step never moves the shoulders against the view.
- **The support hand's wrist is the test room's now, in third person too:** the forearm takes up 0.6 of the hand's
  roll, and the wrist stops at 80 degrees of roll and 55 of bend. It had not come over with D-0067: the sleeve
  pinched at the cuff, and first person shows that close up.
- **As they were:** where each gun sits at the hip (its View Offset), the kick, the reload's tip and roll, the hit
  marker. The hands ride the kick and the reload.
- **Where the numbers live.** Same for every gun: `Resources/WeaponHold` > First person: the sight to the eye (Sight
  Time, Sight Distance, Sight Rise, Dot Hides At); First person: the body under the eye (Eye Above Head, Eye
  Forward, Back Looking Down, Chest Follows View); under the support hand: Forearm Roll, Wrist Roll, Wrist Bend.
  Per gun, on its prefab's `BlasterWeapon`: Sight Point (zero = found from the model) and Sight Roll;
  `WorkshopPieces` writes both on a rebuild (WeaponSpec `Sight`, `SightRoll`).
- **What each new part owns:** `Combat/GunSight.cs` finds a sight on a model; `Combat/ViewBody.cs` draws the body
  for first person, hides the head, stands it under the eye and puts everything back; `ArmAim` (TurnChest,
  OutOfReach, OnGun) bends the arms to a gun the view has placed; `WeaponHolder` runs the sight from hip to eye and
  calls them; `PlayerAnimation` feeds the legs from the first-person body while the motor is parked.
- **CHECKED:** a new probe (kept: `%TEMP%\shiftjam-stage\weapons\fp\probe-keep\FpArmsProbe.cs`), pistol, SMG and
  shotgun at toy size, pistol and shotgun again at kid size, measured in the world and on screen: 160 of 160. On the
  code before: 48 of 155 (what passed there: the hip position, the kick, the reload's tip, the hit marker, the
  narrower view, and leaving first person). Measured, the same at both sizes: the wrist is 0.090 from the middle
  of the grip (0.60 before); the fingers close to 0.81 of the open hand; the support palm is 0.03 from its hold
  point at the hip and 0.027 (shotgun) and 0.051 (SMG) aimed; the head is 0.001 of its size and 0.119 behind the
  eye; with aim held the sight's glass (found by the probe itself from the model) is within 1 pixel of the middle
  of a 960 x 540 still, 0.307 in front of the eye, there 0.15 s after the press; the SMG is rolled 45.0 degrees;
  the dot is hidden with the sight up and shown at the hip; a shot flips the barrel 4.00, 3.40 and 8.80 degrees
  and the hand moves 0.0002 against the gun; looking 60 and 85 degrees down and up the wrist stays 0.090 from the
  grip, and the grip is 0.30 and 0.23 in front of the chest bone looking down; half way through a reload the free
  palm is 0.030 from the magazine's bottom; walking, the legs get the body's speed; crouched (eye 0.75) the body
  crouches and the hand stays on; back in third person all 8 parts of the body are drawn with their shadows, the
  head is its size and the body stands where it did. Third person after it: hold probe 81 of 81, camera probe 64
  of 64, ammo probe 212 of 212; four scenes: 0 errors in Greybox, Workshop, Raceway and Colour Corner (the Raceway's R still starts the race).
- **SEEN (stills, mine):** hip and aim for the three guns beside the test room's (the sheet above): the hand is on
  each grip, the sights sit in the middle as the test room's do, no head in view; looking 60 and 85 down (arms,
  then the yellow top, legs and shoes below), 60 up, the three reloads and a crouch. The first stills showed two
  things that were then fixed: the SMG's tilted sight (now rolled upright) and the shotgun's support wrist pinched
  like a sweet wrapper looking down (the wrist limits above).
- **NOT CHECKED:** nothing was played or seen in motion (the sight coming up, the roll of the SMG, the legs
  walking); the Female body; first person with a prop held by the hands while a gun is out; a shift while in first
  person with a gun; the look from inside the head of a roll or a punch (not in this piece).
TO PLAY: Workshop or Colour Corner, a gun in hand, V. Look at your hands. Hold right mouse with each gun; fire at
the hip and aimed; R; look straight down and straight up; walk and look down; C; 1 / 2 or the wheel to swap; X to
put the gun away (the arms go) and take it out; V back.
FOR HIS EYE: the arms (bare on the Male; the test room's player is the Female in a blue hoodie); the SMG rolling
over to its offset sight (Sight Roll 0 on the prefab holds it level, a Sight Point aims it over the top instead);
the body below when looking down; the shotgun's scope filling the middle; whether the body should also show with
empty hands.
NOT YET: the rifle, the sniper and bubbles (D-0072), grenades, the third-person leftovers (the entry above this
one when they land).

## 17 Nov 2026 10:4x — SHOT FEEL, WHAT A SHOT SHOWS AND SOUNDS LIKE (D-0071, second landing: hit marker, puff at the barrel, bits and squash where a dart lands, the dart's sounds) — NOT PLAYED, NOT SEEN, NOT HEARD
On top of the numbers (the entry below), built headless while he was in AoE4. No picture was taken and no sound was
heard in this piece: all of it is for his eye and ear.
- **Hit marker.** Four short ticks round the crosshair flash white when one of your shots lands on something with
  health, and bigger, longer (0.4 s against 0.2) and orange when that hit finishes it. Third and first person, darts
  and paint. A wall or a loose prop gives none. Sizes, colours and times: `Resources/WeaponAmmo` > The hit marker.
- **A puff at the barrel** on every shot: 6 pale bits along the shot, gone in 0.2 s (`WeaponAmmo` > A shot leaving).
- **Where a dart lands:** 8 pale blue bits fly off the surface, and it makes a sound. Each kind of ammo carries its
  own (`Prefabs/Combat/FoamDart.prefab` > Feedback); paint keeps its own splat, bits and sounds, untouched.
- **What is hit squashes** flat and wide for a moment and springs back: the practice bear and skittle by 0.15, the
  toy soldier and the mischief soldier by 0.1 (`Damageable` > Squash; 0 = off, which the player and the cars are).
  Any hit does it, a punch too.
- **The dart's sounds:** three for the shot and three for the landing, the test room's (Case Portman's pack, his
  licence, under `_Imports`, never in git). A copy of the project without them is silent and runs the same.
  Pellets landing in the same frame make one sound, not eight on top of each other.
- **What each new part owns:** `FX/Squash.cs` the squash and its spring; `Projectile` > Feedback an ammo's sounds
  and bits; `GameEvents.ShotLanded` says a shot landed on something with health (and whether it finished it);
  `UI/WeaponHud.cs` shows the ticks. The bits fly in the paint's own bit system (`PaintSplats.Puff`); bits that do
  not fall (the puff) got a system of their own there, so a puff cannot stop a paint burst's bits falling.
- **CHECKED:** the shot probe, all of it, after both landings: 64 of 64 (numbers 48 of 48, feedback 16 of 16; the
  feedback part stood at 2 of 14 before this landing, and two checks came with the sound files). Measured: 6 bits at
  the barrel as a shot leaves, none 0.45 s later; 8 bits off a wall, none while the dart flew; a wall raises no
  event and no ticks; a hit on something with health raises the event once and the ticks flash white for 0.20 s;
  the hit that finishes it raises it as finished and they flash orange for 0.40 s; a practice skittle squashes to
  0.850 of its height and 1.150 of its width and is back to its own shape 0.7 s later; the four pieces carry 0.15
  over 0.3 s and 0.1 over 0.25 s; the dart carries 3 and 3 sounds and one plays at a landing; a dart with a clip
  that is gone and with none still fires, lands and throws its bits, with no error. Four-scene smoke run after both
  landings: 0 errors in Greybox, Workshop, Raceway and Colour Corner (the Raceway's R still starts the race).
  The bits and the placed sounds go through the scene's paint keeper, which only the Colour Corner holds; anywhere
  else the first shot makes it. So the feedback part was run once more in the Workshop: 17 of 17, the first shot
  made the keeper, no error (Greybox and the Raceway take the same path; not run there).
- **NOT CHECKED:** nothing was seen or heard (a hidden run draws nothing and is muted); first person was not
  driven; the ammo, camera and hold probes were not re-run after D-0071 (nothing they measure was changed).
- **OWED FOR HIS EYE AND EAR** (no stills in this piece, no Godot window while he plays): the ticks on screen
  (size, how long, the orange) beside the test room's; the puff and the bits beside the test room's; the squash in
  motion; every sound, and how loud next to the paint's. And still owed from D-0070: Shift Jam's side of the reload
  stills (the test room's 27 are shot: `AppData\Roaming\Godot\app_userdata\testing-zone-view\reload`).
TO PLAY: Workshop: shoot a practice bear with the pistol until it falls (white ticks, then orange; it squashes; bits;
sounds). Shoot a wall (bits and a sound, no ticks). Watch the barrel for the puff. Shotgun into a wall close up.
V for first person: the same. Colour Corner with paint: ticks on the mischief soldier.
FOR HIS EYE: the ticks' size and time; whether the puff reads at all at 6 small bits, and with the SMG; the squash
amounts; each sound and its loudness; the SMG as a gun (the entry below).
NOT YET (the next pieces): the rifle, the sniper and bubbles (D-0072); grenades; first-person arms and sights; the
dart's bright streak; the rooms.

## 17 Nov 2026 10:3x — SHOT FEEL, THE NUMBERS (D-0071: the guns fire, kick and weigh as the test room's; the mouse wheel swaps weapons) — NOT PLAYED
The next piece of the weapons port, built headless while he was in AoE4. Every number was read from the test room's
code and weapon files; its game was not run.
- **The pistol and the shotgun fire as the test room's.** Dart Pistol: 5 shots a second (was 5.6), on the spot (its
  1 degree of stray is gone), damage 15 as before (15 here is the test room's 1), push 8 (was 3). Dart Shotgun: 1.2
  shots a second (was 1.4), 8 pellets (was 7) of damage 7.5 (was 9) and push 2.5 (was 4), within 2.5 degrees (was 6),
  at 0.9 of the dart's speed. A full blast takes 60 health (was 63).
- **The SMG stands half way between the test room's pistol and its rifle, number by number** (the test room has no
  SMG): 7 shots a second (was 12.5), damage 15 (was 7), push 7 (was 1.5), stray 0.3 degrees (was 3), darts at 1.1 of
  the dart's speed, kick 0.85, walk 0.96. It is a different gun now: a steady automatic with pistol darts, where it
  was a fast spray of weak ones. All of it is on `DartSMG.prefab`.
- **The foam dart flies as the test room's:** 34 a second (was 40), falling at 3 (was 2), gone after 4 s (was 2.5).
  The toy soldiers fire the same dart, so theirs slowed with it (0.14 lower at their longest range of 14).
- **The kick is one number a gun** (`BlasterWeapon` > Recoil, 1 = the pistol): the barrel flips up 4 degrees and the
  gun jumps back 0.03 for each 1, and it settles at 14 a second (`Resources/WeaponHold` > The kick of a shot).
  Pistol 1, SMG 0.85, shotgun 2.2. Every gun used to flip 10 degrees and jump 0.06. Third and first person; the
  first-person view punch (ours, the test room has none) is multiplied by the same number.
- **A bigger shooter shoves loose things harder by the square root of its size** (the test room's rule), no longer
  by its whole size: a kid's shot shoves a loose prop 5.9 times as hard as a toy's, where it was 35 times. The shove
  on a player or a soldier is as it was.
- **A heavy gun slows every step a little** (`Weapon` > Move Speed): shotgun 0.9, SMG 0.96, pistol 1; walking,
  running and crouched, third and first person. Put away, it slows nothing.
- **The mouse wheel swaps weapons** while one is out: down for the next, up for the one before. 1 / 2 as before.
  Not while a prop is held (the wheel moves or turns it), not with the key sheet open.
- **What stood on the old numbers, looked at first** (D-0071 has the list): the practice targets (120) still take 8
  pistol darts and 2 shotgun blasts; the toy soldier (60) 4 darts or one whole blast; the mischief soldier (45) 3
  darts or 6 of 8 pellets; the SMG needs as many darts as the pistol now (it needed twice as many and more). A
  pistol dart rocks a practice target about 5.5 degrees (was 2) and sends a loose prop off at 2 a second (was 0.75).
  Paint is counted by hits, not damage: unchanged, but the SMG now paints 7 times a second, not 12.
- **Where the numbers live:** per gun on its prefab (`BlasterWeapon`: Interval, Pellets, Spread, Damage, Knockback,
  Shot Speed, Recoil; `Weapon`: Move Speed) and in `WorkshopPieces`, so a rebuild gives the same; what a kick of 1
  is in `Resources/WeaponHold`; the dart's flight on `Prefabs/Combat/FoamDart.prefab`.
- **CHECKED:** the new shot probe, its numbers part: 48 of 48 (12 of 48 on the code before), at toy size, with
  the darts' count, speed and stray and the shove again at kid size. Measured: pistol 4.99 shots a second, SMG 6.97,
  shotgun 1.20; darts leave at 34.0, 37.4 and 30.6; they stray 0.02, 0.29 and 2.49 degrees; a pull takes 15, 15
  and 60 health; a loose thing of mass 1 leaves at 8, 7 and 20 along the shot from a toy and at 47, 41 and 117 from
  a kid (5.9 times; the code before gave 35 times); the barrel flips 4.00, 3.40 and 8.80 degrees, and measured on
  the gun in the hand 4.15, 3.54 and 9.08; the kick settles at 14.0 a second; the walk is 1.00, 0.96 and 0.90 of
  normal; the wheel swaps both ways and does nothing with the gun put away; no errors in the run. The hold probe
  was run once before the work, on the D-0070 code: 81 of 81 (the reload pose broke nothing in the hold).
- **The four-scene smoke run** came after the feedback landing (the entry above): 0 errors. **NOT RE-RUN:** the ammo
  and camera probes (nothing they measure changed) and the hold probe after this landing. First person was not driven by
  the probe: its kick reads the same two values the probe measured.
TO PLAY: Colour Corner or Workshop: fire each gun at a wall and watch the barrel; hold the SMG's trigger; walk with
the shotgun out, then put it away (X) and walk again; roll the mouse wheel with two guns; shoot a loose prop as a toy,
then as a kid.
FOR HIS EYE: the SMG's new character; the pistol's kick (4 degrees, it was 10) and the shotgun's (8.8); whether the
slower walk can be felt (10 % and 4 %); the wheel's direction; the slower dart with more drop.
NEXT: what a shot shows and sounds like landed right after (the entry above); then the rifle, the sniper and
bubbles; then grenades and first person.

## 17 Nov 2026 10:1x — THE CAR PORTAL IS HIS PORTAL DRAWING (D-0073) — NOT PLAYED: no warp, spin or ripple seen in motion
His "let's finish the portal", then "proceed, do it headless since im in aoe4". Built with Unity closed, in hidden
low-priority batch runs.
- **In the game:** `CarPortal_Red`, `_Blue` and `_Green` are now his drawn swirl standing up, with the dark purple
  middle. The two red portals in the Workshop follow the prefab (checked in the scene; the scene file is unchanged).
- **A warp:** the swirl flashes, and the drawing's small marks go outward as a ripple at both portals (three rings,
  about 3.8 s in all). Between warps the marks are off.
- **Same as before:** the opening (7 across, lowest point 0.4 under the floor), the trigger, pairing by colour,
  keeping speed, the sound, the flash light, the floor glow.
- **Gone:** the pack's hoop and its collider. Nothing solid stands there now.
- **To tune (his, in the editor):** `Data/PortalSwirl` > Spin speed (-40; 0 stops it, above 0 turns it the other way),
  Edge shade (1 = flat like the drawing), Brightness. `PortalRipple` on each prefab's `Model/Ripple`: the times, the
  sizes, and idle (seconds between ripples with no car; 0 = only after a warp). After changing the model:
  re-export (below), then Shift Jam > Workshop > Build Car Portals.
- **Files:** `Art/Portals/PortalRing.fbx`, `Shaders/PortalStroke.shader`, `Data/PortalSwirl.mat`, `Data/PortalMarks.mat`,
  `Scripts/Vehicles/PortalRipple.cs`, `Scripts/Vehicles/CarPortal.cs` (one field, one line),
  `Scripts/Debug/RacingPieces.cs` (the builder). Build Mode thumbnails of the three redrawn.
- **Re-export from Blender** (hidden, from the saved file; scrubs his Windows path from the FBX):
  `blender.exe -b portal_ring.blend -P _tools\drawing_to_tubes\export_portal_fbx.py -- <out.fbx>`, both under
  `Dev\Blender Projects`.
- **Open, his:** (1) a pair-colour cue: the three colours look alike now except for a faint floor glow and the flash;
  only a red pair is placed anywhere, so nothing is confusing yet. (2) The swirl's turn: direction and speed.
  (3) The lower third of the swirl is under the floor (the hoop's rim was too): his eye. (4) The ripple by itself
  now and then, or only after a warp (now: only after).
- **Checked:** edit-mode check 86 of 86, stills seen. Nothing in motion has been seen by anyone.

## 17 Nov 2026 08:4x — MAGAZINES, RELOADING, AMMO PICKUPS AND THE WEAPON HUD (D-0070: the test room's ammo rules; the next piece of the weapons port) — NOT PLAYED
His "let's continue the work on shift jam (including testing zone)". Built headless by a builder agent while he was in
AoE4. The run was cut by the usage limit at 08:25 with this piece in the tree and its probe passing; the probe was
taken out, one smoke run made, and the docs and the commit done by hand after.
- **Each gun has a magazine and a reserve** (on its prefab: `BlasterWeapon` > Magazine). Dart Pistol: 12 in the gun,
  48 carried at the start, 96 at most, reload 1.3 s. Dart Shotgun: 6 / 24 / 48, 2.2 s. Both are the test room's.
  Dart SMG: 20 / 60 / 120, 1.5 s (the test room has no SMG: set between its pistol and its rifle). A shot spends one
  round whatever the pellets. Darts and paint share the one magazine (the test room's rule). The rounds stay with the
  gun: put away, dropped, picked up again.
- **R reloads** (pad: Y while there is nothing to use; all 16 pad buttons were taken). An empty gun clicks once,
  waits 0.25 s and reloads by itself if there is reserve; a held automatic that runs dry just reloads. No aiming and
  no firing during a reload. A swap, putting the gun away, a drop, a roll, a block, a hit or getting into a car
  stops it.
- **The reload is shown.** The gun comes to the chest tipped up 25 degrees and rolled 35, the magazine drops out and
  comes back, and the free hand (a long gun's support hand) goes to the magazine. Where: `Resources/WeaponHold` >
  A reload. First person: the floating gun tips the same way (no arms there yet).
- **Ammo pickups.** `AmmoBox` (one magazine to each gun carried, back after 15 s) stands one step past the SMG in the
  Workshop's row. `SupplyCrate` (fills every gun carried, never used up) stands in the Colour Corner where the test
  room's does. A pickup gives only to the guns being carried.
- **Weapon HUD, bottom left:** rounds / reserve ("12 / 48"), RELOADING, the gun's name and the ammo's name, red when a
  quarter or less is left, and a dot crosshair (the old small cross is gone; first person keeps its own dot). Hidden
  with no blaster out, in Build Mode, in menus, in the console and while driving.
- **Numbers:** per gun on its prefab (and in `WorkshopPieces`, so a rebuild gives the same); the shared ones in
  `Resources/WeaponAmmo` (the dry wait, reload when empty, the reload's timeline, the HUD's sizes and colours, the
  four sounds).
- **What each new script owns:** `Combat/Magazine.cs` a gun's rounds and reserve; `Combat/Reload.cs` a reload's
  timing; `Combat/MagazinePart.cs` finds the model's magazine and moves it; `Combat/AmmoBox.cs` the two pickups;
  `Data/WeaponAmmoSettings.cs` the shared numbers; `UI/WeaponHud.cs` the read-out and the dot; `Debug/AmmoPieces.cs`
  builds the two pickup pieces. Changed: `BlasterWeapon`, `WeaponHolder`, `ArmAim` (the reload pose), `GameEvents`
  (AmmoChanged, ReloadStarted, ReloadEnded, DryFired, AmmoTaken), `PlayerInput` (Reload), `KeyHelp`, `WorkshopPieces`,
  the Colour Corner's builder.
- **CHECKED:** ammo probe 212 of 212 at toy and kid for the three guns. The same probe on the code before: 22 of 188.
  Its first run on the new code: 196 of 212 (the kid's ammo pickups gave nothing, and the free hand stopped 9 to 12 cm
  short of a long gun's magazine). Between that run and the passing one (read back from the builder's own record):
  the kid's pickups were a GAME change (a pickup's reach now stands 12.1 tall; a kid's rounded body passed over the
  1.5-tall one; the check's wait on a pickup also went from 0.5 to 1.2 s), the free hand was a CHECK change only (the
  arm code was not touched: the fixed 6 cm rule became the test room's own measured numbers, which the pistol and the
  shotgun match within 1 cm; the SMG, which the test room does not have, passes a looser bound and its hand still
  ends 12 cm from the magazine: look at it in play). Four-scene smoke run after the landing: 0 errors in every scene
  (the Raceway's R still starts the race from a grid car).
- **NOT RE-RUN after this piece** (his "avoid extra redundant testing for now"): the hold probe (81) and the camera
  probe (64). `ArmAim` was changed for the reload pose, so the hold probe is the first to run next time.
- **OWED:** stills of the reload beside the test room's (no Godot window while he plays); the four sounds are pack
  clips picked by name, unheard.
TO PLAY: Colour Corner: fire the pistol dry (click, then it reloads); press R mid-magazine; 2 for the SMG and hold fire
through a reload; walk into the supply crate. Workshop: take the shotgun, empty it, use the ammo box past the SMG.
FOR HIS EYE: the reload pose and the hand on the magazine; the HUD's size and place; Y as reload on a pad; the SMG's
numbers; whether an empty gun should reload by itself (`WeaponAmmo` > Reload When Empty).
NOT YET (the next pieces, in order): shot feel (per-gun recoil, hit marker, muzzle puff, wheel swap, slower walk with
a heavy gun), then the rifle, the sniper and bubbles, then grenades and first person.

## 16 Nov 2026 22:0x — THE NITRO STAR IS HIS STAR, IN SEVEN COLOURS (D-0068) — NOT SEEN ON THE RACEWAY, NOT DRIVEN THROUGH
His first Blender model after the orb, built by him from menu steps (`Dev\Blender Projects\star.blend`), brought in on
his "yes bring in one mesh and all of the materials". Landed beside his open editor, out of Play.
- **In the game:** `Prefabs/Pieces/Interactables/NitroStar.prefab` is his star in ORANGE over an orange floor glow.
  Every nitro star already placed (the Raceway's) follows the prefab. Same size, hover, spin, trigger and sound.
- **The seven colours:** `Data/Stars/` StarGold, StarOrange, StarPink, StarLime, StarCyan, StarViolet, StarBold.
- **To change the colour (his, in the editor):** open `NitroStar.prefab`, drag another `Data/Stars` material onto the
  star, then Shift Jam > Workshop > Build Nitro Star. The floor glow follows. Build Boost Pieces keeps the pick too.
- **Files:** `Art/Star/NitroStar.fbx` (the one mesh), `Scripts/Debug/RacingPieces.cs` (the builder), `Data/NitroGlow.mat`.
  Seven FBX files, one a colour, are in `Dev\Blender Projects\star_export` for his own use (not in the project).
- **Also:** `Art/Orb/ShiftOrb.fbx` had his Windows user path inside it (Blender writes the source path into every FBX);
  replaced in the file. 🔴 The earlier copy is still in commit `2e3013d`, and two older lines of this file carried the
  same path (now shortened): all unpushed. Taking them out of the history before a push is his call.
- **Checked:** see D-0068. Stills are in `%TEMP%\sj_star_csc` (`star2_threequarter.png`, `star3_seven_colours.png`).
- **HIS, open:** a look at the star on the Raceway; which colour stays; the glow strength of each material.

## 16 Nov 2026 22:5x — THE HAND IS ON THE GRIP AND THE GUNS ARE THE PACK'S SIZE (D-0069: measured from the test room) — NOT PLAYED
His "the animations in the testing zone look better, especially the two hand gun holding". Close-ups of both, side by
side, showed why:
- **The hand held the wrong part of every gun.** The builder put each weapon's root (where the fist goes) at the
  MAGAZINE: 8 cm above the grip's middle on the pistol, 6 cm on the shotgun, 10 cm above and 15 cm ahead on the SMG.
  The pistol grip hung unheld below the hand. Now the root is the middle of the grip, as the test room finds it from
  the model (the part hanging below the trigger): pistol (0, -0.041, -0.026), SMG (0, -0.056, -0.027), shotgun
  (0, -0.031, -0.006) in the model's own space.
- **The guns had been resized as toys** (pistol 0.89 x, SMG 0.75 x, shotgun 1.18 x the pack). The long shotgun pushed
  the arms out; the small SMG cramped them. Now all three are the pack's own size, as in the test room.
- **The gun sat in the fist at the wrong angle** (I had put the barrel straight along the knuckles). Measured on the
  test room's hand: the barrel tips 25 degrees towards the palm and 8 towards the thumb, the top 33 degrees away from
  the palm, and the grip's middle is 4.8 cm into the palm from the bone line. Those numbers are now in
  `Resources/WeaponHold` (Fist Centre, Barrel In Hand, Top In Hand).
- A long gun's carry and shoulder positions are now measured to the same point as the test room's (its hand prop
  bone, Grip From Holder), so they sit where its do.
- The muzzle is at the end of what is fitted on it (the pistol's and the SMG's suppressors), not inside it.
- `WorkshopPieces` builds the three guns this way on a rebuild (PackSize, Grip, MuzzleReach); the prefabs were edited
  to match by hand, not rebuilt.
- **CHECKED:** hold probe 81 of 81; four scenes 0 errors; stills beside the test room's: the aimed and carried
  shotgun, the aimed pistol and the hands now read the same (`docs/stills/compare_godot_unity_*.jpg`, redone).
  Measurements by `claude-test-views/probes/compare/measure.gd` (reads only).
- **23:1x, his "fix em" (the two leftovers), `D-0069` follow-up:** the three pickups show the gun at the pack's size
  (their models were still 0.89 / 0.75 / 1.18; the check failed on that first, now 1.000 against 1.000 in the hand).
  First person: each gun's `View Offset` is the test room's hip position taken to the grip's middle (pistol
  (0.139, -0.228, 0.265), long guns (0.119, -0.218, 0.206)); laid beside the test room's first-person stills the guns
  sit in the same place (`docs/stills/compare_godot_unity_first_person.jpg`). `WorkshopPieces` writes both on a
  rebuild. Four scenes 0 errors. STILL DIFFERENT in first person: the test room shows the arms on the gun and brings
  the sight to the eye on right mouse; here the gun floats and right mouse only zooms.
FOR HIS EYE: the same walk-through as below, with the SMG and the shotgun; say if the motion (walking, running,
turning with a long gun) still reads worse than the test room's, and where.

## 16 Nov 2026 22:2x — COMPARED WITH THE TEST ROOM, PICTURE BY PICTURE (D-0068): the gun-out camera corrected, the fingers close on the grip — NOT PLAYED
His play of the long guns: "needs serious work, compare with the testing zone in godot". I had ported from the test
room's code without looking at its pictures. Now the same poses are rendered in both and set side by side:
`docs/stills/compare_godot_unity_pistol.jpg`, `_shotgun.jpg`, `_rifle_vs_smg.jpg` (Godot left, Shift Jam right; the
game's own view, the side, the hands close up). Godot stills by `claude-test-views/probes/compare/shots.gd` from the
no-focus twin (reads only).
- **WHAT THE PICTURES SHOWED WRONG HERE, AND IS NOW FIXED:**
  - **The camera with a gun out was about 30 % tighter than the test room's.** D-0065 kept Shift Jam's 60 degree view
    and narrowed it to 0.73 on aim; the test room uses 75 with a gun out and 55 on aim. Now `CameraSettings` has
    `Armed Field Of View` 75 and `Aim Field Of View` 55 (either size); the wide view keeps its own.
  - **Every length taken from the test room was 5 % short.** D-0064 to D-0067 turned its metres into "units of the
    1.8 body" (x 0.9375, the capsules' ratio). The bodies are the same rig, fitted here at 0.99: the right factor is
    0.99. Corrected: gun-out camera 1.88 back, 0.79 to the side, 0.28 up, aim 1.29; the hold's body oval, shoulder
    pocket, carry position and support-hand numbers (`Resources/WeaponHold`). NOT corrected: D-0064's move lengths
    (roll, reach, crouch speed), which carry the same 5 %: his call.
  - **The hands were open.** `Combat/FingerCurl.cs` (the test room's HandGrip): the gun hand's three fingers bend 70
    degrees a bone, the trigger finger 35, the thumb folds 25; the support hand's four fingers 40 under a long gun.
    From the rest pose, so the grip is the same over every clip; 0.15 s to close. Numbers in `WeaponHold` > The fingers.
- **CHECKED:** hold probe 81 of 81 (the 10 finger checks failed first: hands 1.00 open, now 0.81 and 0.92); camera
  probe 64 of 64 on the new numbers; four scenes 0 errors; the game views now sit on the test room's (same place and
  size of the head and the gun on screen, aim and gun-out).
- **WHAT STILL DIFFERS FROM THE TEST ROOM (seen in the pictures or in its STATUS), not done:**
  1. **The guns themselves.** Here: Dart Pistol, Dart SMG, Dart Shotgun, resized as toys (the shotgun 1.18 x the
     pack's size, the SMG 0.75 x). There: pistol, rifle, shotgun, sniper at the pack's own size. The shotgun reads
     bigger here; there is no rifle or sniper here and no SMG there.
  2. **The HUD.** There: ammo count ("12 / 48"), the weapon's and the ammo's name, grenades, a dot crosshair, a hit
     marker. Here: a small cross only.
  3. **Ammo and reload** (magazines and reserves, R, the reload pose, ammo boxes): not here.
  4. **Shot feedback** (hit marker, burst of bits, squash, muzzle puff, the test room's sounds) and its recoil numbers
     (4 degrees and 0.03 x each gun's recoil; here 10 degrees and 0.06 for every gun).
  5. **Mouse wheel** swaps weapons there; here 1 / 2.
  6. **First person**: there the gun sits low in view and right mouse brings its sight to the eye; here the old
     floating view model.
  7. **Grenades** (G) and the slower walk with a heavy gun: not here.
  8. **Not comparable from stills:** how the hold moves while walking, running, crouching and turning. His eye.
TO PLAY: as the entries below; look for the wider view with a gun out, the closed hands, and say which of 1 to 8
matter most.

## 16 Nov 2026 21:5x — THE LONG GUNS ARE IN BOTH HANDS (D-0067: SMG and shotgun carried across the body, raised to the shoulder; the test room's rule) — NOT PLAYED
His "now the two handed long guns". Built outside the project, landed with the editor closed, headless only.
- **Both hands stay on a long gun while it is out** (`Weapon > Two Handed`, on for the Dart SMG and the Dart Shotgun).
- **Carried** (not aiming): low in front of the chest, the barrel 30 degrees down and 40 to the left, the chest turned
  12 degrees so the left shoulder comes forward.
- **Aimed** (right mouse, or a shot): the stock comes into the right shoulder and the barrel onto the crosshair's
  spot, up and down too; the chest turns 35 degrees, the head stays facing the aim. A shot waits for the gun to be up
  (0.12 s) and it stays up a second after, as with the pistol.
- **The support hand** holds under and beside the barrel: at the model's own under-barrel marker when it has one
  (the pack's `Place_..._BarBottom`), else a spot ahead of the grip, never further forward than 0.28; it slides back
  along the barrel when the arm cannot reach (a gun pointed steeply down).
- **The stock's length is measured from each model**, so any long gun added later sits in the shoulder by itself.
- A roll, a block or a hit takes the hands off for its length (0.2 s on and off).
- **Numbers:** `Resources/WeaponHold.asset`, the four "A long gun" groups (hold time, elbows, chest turns, shoulder
  pocket, carry position and angles, the support hand). Per gun: `Two Handed`, `Support Point` on the weapon prefab.
- **CHECKED:** hold probe 68 of 68 at toy and kid: carried barrel 30 down / 40 left with the support palm 0.05 from the
  barrel's line; aimed level, up and down the barrel 0.0 degrees off the spot with the support hand still on; a pull
  from the carry leaves 0.11 s later; carried again 2 s after. The same probe before this: the 32 long-gun checks
  failed (support palm 0.48 to 0.70 from the gun). Four-scene smoke run 0 errors. Stills looked at:
  `docs/stills/hold_toy_DartShotgun_*.png`, `hold_toy_DartSMG_*.png`. Probe: `%TEMP%\shiftjam-stage\hold2\probe-keep`.
TO PLAY: Colour Corner, 2 for the SMG: walk with it, hold right mouse, look up and down, fire. Workshop: take the
shotgun (E) and do the same. Roll (Left Ctrl) with a long gun out.
FOR HIS EYE: the SMG is small in two hands (held close to the chest; say if it should go back to one hand); the carry's
height and angle; the hands are still open (no finger curl); how the carry reads while running and crouching.
NOT YET: finger curl, reloads and magazines, the test room's feel numbers (recoil per gun, move speed with a long gun)
and ammo counts; first person is unchanged.

## 16 Nov 2026 21:2x — THE GUN IS FIXED IN THE HAND AND THE ARM AIMS IT (D-0066, the test room's rule; first part of the weapon port) — NOT PLAYED
His play of the cameras (20:5x): "im noticing the pistol rotates in my hand". Cause: a drawn blaster was put at the palm
but always turned to the camera's view, while the arm was one fixed raised pose and the body only faced the camera with
right mouse held. The far camera hid it; the over-the-shoulder view shows it. His "proceed with the recommended".
Built outside the project while he played, landed with his editor closed. Headless only.
- **The gun sits in the fist** (barrel along the knuckles, top towards the thumb, worked out from the hand's own bones)
  and never turns in it. At rest the arm is the animation's: the gun hangs at the side and swings with the walk.
- **Aim (right mouse)**: the arm comes up and points the barrel at the crosshair's spot, up and down too (60 up, 85
  down, 80 to either side of the body); the body faces the aim. `Combat/ArmAim.cs` bends the upper arm and forearm
  after the Animator each frame.
- **A click from rest**: the arm comes up first (0.12 s), then the shot leaves along the barrel; the arm stays up 1 s
  after the last shot and lowers in 0.25 s; the body faces the aim for that time too. Holding the trigger keeps it up.
- **Shots and the arm aim at the same spot** (`BlasterWeapon.AimPoint`): the crosshair's ray now starts level with the
  player, so a thing between the camera and the player is no longer picked (the test room's rule).
- **Melee** (knife, foam finger) and **first person** are as they were. The Arms layer no longer raises the arm for a blaster.
- **Numbers:** `Resources/WeaponHold.asset` (new, `Data/WeaponHoldSettings.cs`): raise / lower times, the wait after a
  shot, reach 0.9, elbow direction, the limits, the body oval the hand stays out of, Grip Euler (a turn of every gun in
  the fist). Per gun: `Hand Euler` and `Hand Offset` on the weapon prefab, as before.
- **CHECKED:** hold probe 68 of 68 (pistol, SMG, shotgun, toy and kid: gun turns 0.0 degrees in the hand over four
  camera looks, arm down at rest, barrel 0.0 degrees off the spot aiming level / up / down, a pull leaves 0.11 s
  later with the arm up, arm still up at 0.5 s, down at 2 s). The same probe on the code before: 20 of 68 (the gun
  turned 180 degrees in the hand, the arm never came down, the shot was instant). Camera probe still 64 of 64, the
  four-scene smoke run 0 errors. Stills looked at: `docs/stills/hold_toy_DartPistol_*.png`, `hold_toy_DartSMG_*`,
  `hold_toy_DartShotgun_*`. Probe kept at `%TEMP%\shiftjam-stage\hold\probe-keep`.
- **21:3x, his "still cant swap to the other guns":** 1 / 2 (swap) and Z (drop) used the rule made for clicks, which
  wants the mouse captured by the game window. A swap check in the Colour Corner showed the player carries both guns
  and the keys arrive, and the swap was refused only for that. They now follow the key rule, with X: game running,
  player in control, not in Build Mode, the console not open (`WeaponHolder.KeysAct`). A pad's d-pad swap had the same
  fault. Also `ShoulderCamera`: a click in the game view takes the mouse back whenever the cursor is really free, not
  only when the camera itself had freed it. Check after: 2 -> SMG, 1 -> pistol, 2 -> SMG with the mouse not captured.
  NOT KNOWN: why the mouse was not captured in his session (if clicks did not fire either, that is the same cause).
TO PLAY (Colour Corner): walk about with the pistol out and turn the camera: the gun stays in the hand. Hold right
mouse and look up and down. Click once from rest and watch the arm come up, fire, stay, lower. 2 for the SMG; hold the
trigger. Then the shotgun in the Workshop.
FOR HIS EYE: the 0.12 s before a first shot (Raise Time; 0 = instant); the hand is open round the grip (no finger
curl yet); the SMG and shotgun are held out in one hand like a pistol; how the gun hangs while running.
NOT IN THIS PASS (next parts of the weapon port): the second hand on the long guns and their carry across the body,
fingers closing on the grip, reloads and magazines, the test room's feel numbers and ammo counts.
🔴 FOUND, NOT MINE, LEFT ALONE: after his play the tree holds saved changes to `Data/ShiftWarp.mat` (direction and
picture), `Resources/FX/ShiftOrbVisual.prefab` (its light's intensity 1.4 -> 0) and others: these look like values the
game set during Play written back into the files (a Play session in the editor changes a shared material or a prefab
loaded from Resources for good). Another session is also landing a star model (`Art/Star`, `Data/Stars`, NitroStar,
`RacingPieces.cs`) in this tree. Neither is in the D-0066 commit.

## 16 Nov 2026 20:0x — THE CAMERAS ARE THE TEST ROOM'S (D-0065, Stage B of the player port) + THE ORB'S FAINT BOX FIXED HERE — NOT PLAYED
His "let's pick up the work". Stage B was cut by the usage limit at 14:38 with the code in the tree and its check not
yet run; finished here. Headless only (his editor closed): play probes, stills looked at, nothing played by hand.
- **Three views on foot, eased into one another** (`Camera/ShoulderCamera.cs`, numbers in `Data/CameraSettings.cs`):
  empty hands = the wide view as before (6 back, 0.3 to the side); weapon drawn = over the shoulder (1.8 back, 0.75 to
  the side, 0.28 higher); aim held (right mouse) = 1.2 back and the view narrowed to 0.73 (60 to 43.8 degrees).
  Lengths are in body units (the 1.8 body); the kid's 0.7 distance and 70 degree view apply to all three. A crouch
  lowers the camera with the head.
- **Walls:** the way to the camera is swept with a ball as big as the near plane's reach: up from the body's middle,
  sideways to the shoulder, then back. The camera stops short of a wall and never starts inside one.
- **Own body:** pushed in by a wall behind, the camera ends up in the player's head; the body and the weapon fade out
  in 0.15 s (only their shadows stay) and come back when clear, with slack so it does not flicker (`Player/BodyFade.cs`,
  on the Player in all four scenes).
- **First person (V):** aim narrows the view there too (73.7 to 43.5).
- **X (holster) works without the mouse captured** (`WeaponHolder.OnHolster`): it is a key, not a click.
- **CHECKED:** camera probe 64 of 64 at toy and kid size (views, blends, back to a wall, corner, low ceiling, crate
  beside, wall on the shoulder side, body fade, first person); the same probe sabotaged fails 34 checks; the
  four-scene smoke run passes with 0 errors (the Raceway's race still starts). Probes kept outside the project:
  `%TEMP%\shiftjam-stage\player-stage\probe-keep`. Stills: `docs/stills/player_toy_view_*.png`,
  `player_toy_first_person_aim.png`, `player_toy_armed_*.png`, `player_kid_view_shoulder.png`.
- 🔴 **A DEFECT FOUND AND FIXED ON THE WAY:** with a wall on the right (the shoulder side) and open space behind, the
  camera fell into the player's head and the body vanished (0.00 behind, both sizes, armed or not). The sideways step
  left the sweep ball resting on the wall, so the step back began in contact and got nowhere. Each leg now stops a
  skin short of what it meets (`Sweep`, `SweepSkin`). Seen failing first (4 checks), then passing.
- **One check was wrong, not the camera:** "crate beside" drew its sight line from the player's waist, so looking over
  a crate lower than the shoulder counted as "behind a wall". The line is from the head now. Over the shoulder the
  camera DOES look over a waist-high crate beside you (the legs are hidden): his eye, see `player_toy_armed_crate_beside.png`.
- **THE ORB'S FAINT BOX:** the lab's `Shaders/ShiftOrbMote.shader` is here, on a new material of the orb's own,
  `Data/ShiftOrbMote.mat` (the halo's first material in `Resources/FX/ShiftOrbVisual.prefab`; `ShiftOrbBuilder` uses
  it). `OrbGlow.mat` is NOT changed, unlike in the lab: here the pad glow, the Pocket Core, the portal swirl and the
  shield bubble are copies of it, and Greybox and the Workshop use it directly. Imports clean; not seen in the game.
TO PLAY (Workshop or the Colour Corner, where the Player starts with a dart pistol):
1. X to holster and draw: the camera eases between the wide view and over the shoulder.
2. Hold right mouse with the weapon out: closer and narrower. Let go.
3. Back into a wall and into a corner; stand under something low; stand with a wall or a crate on your right.
4. Back hard against a wall and look forward: the body fades and comes back.
5. V, then hold right mouse: first person narrows too.
TUNE: `CameraSettings` asset: Armed Distance / Shoulder Offset / Rise, Aim Distance, Aim Fov Scale, View Blend (8),
Wall Gap, Hide Body Within, Body Fade Seconds.
FOR HIS EYE: how tight the aim view is (the head fills the left edge at toy size: `player_toy_view_aim.png`); the
shoulder view's height; whether looking over a low crate is wanted.
STILL OPEN HERE, HIS: the orb's bolt colour (gold or the cyan-white it has), the room-in-the-orb pass, the 14 Nov
pack import that overwrote ProjectSettings and the package list (uncommitted), the new title art (uncommitted), the push.
NEXT PORT PIECE: weapon feel numbers and ammo types, then Castle Siege and Train Rescue.

## 16 Nov 2026 15:3x — THE ORB FOR INSTAGRAM, TWO NEW WAYS: filmed as it is, and drawn as pixel art (his "why does it look so processed?", then "1", "and then do 2") — NOT WATCHED PLAYING, NOT SEEN ON THE PHONE
Lab only (`Unity Projects/Shift Orb Lab`); nothing in this project changed. The 14:1x pixel version looked processed
because it is a shrunk render (striped glow, speckle, smeared edges, shimmer); it stays in `Export` as `shift_orb_*`.
All files below are in `Shift Orb Lab/Export`, in iCloud Drive `Shift Orb`, and (reel, 4:5 post, cover) in his Photos
library by way of `iCloudPhotos\Photos`. Each in gold bolts and in this project's cyan-white bolts.
- **THE REAL ORB** `orb_real_<gold|cyan>_reel_1080x1920.mp4`, `_post_1080x1350.mp4`, `_cover_1080x1920.png`:
  1080 x 1920, 30 fps, one 10 s loop (calm, someone near, strike at 5.4 s, bolts at 6.1 s, calm), H.264, silent AAC.
  Filmed out of Play by `ShiftJam.OrbReelCapture.Shoot` (new, `Scripts/Debug/OrbReelCapture.cs`): it steps the orb one
  video frame at a time with the rules of ShiftOrb, BoltBurst and Billboard and the numbers read off the prefab; two
  things are directed and say so in the code (the first flash after someone arrives; the bolt ring's roll, 113 deg,
  so the long bolts point up and down). Settings by env (ORB_REEL_DIR / SETS / SIZE / FPS / VIEW / STILLS); the
  stills go to %TEMP%, then `Export/make_reel.py <stills> <export> <work>` blends the loop shut (0.5 s of spare
  frames) and encodes. Launcher used: `%TEMP%\sj_orb_reel_run\run.ps1` (hidden, below normal; full run 48 s).
  Measured: the loop's join changes no more than any other frame.
- 🔴 **A FAINT BOX AROUND THE ORB, FOUND AND FIXED IN THE LAB, NOT YET HERE:** the halo's first material (`OrbGlow.mat`,
  his mote `Art/Portals/orb_mote.png`) used the stock additive shader; the drawing's ground is 1/255, not black, and
  the orb multiplies that layer on a flash or strike, so the whole square lit up 3 to 5 shades in the dark (a tilted
  box, since the halo spins). New `Shaders/ShiftOrbMote.shader` (takes that ground away, fades before the square's
  border; the drawing file is untouched) and `OrbGlow.mat` now uses it. COPY BOTH HERE with the next orb landing
  (this project's `OrbGlow.mat` is tracked and still on the stock shader).
  DONE 20:0x (top entry): the shader is here on the orb's own `ShiftOrbMote.mat`; `OrbGlow.mat` left as it is.
- **THE DRAWN ORB** `orb_drawn_<gold|cyan>_80.aseprite` (80 x 80, 16 colours, 48 frames, the same five tags), `_480.gif`,
  `_80.gif`, `_reel_1080x1920.mp4`, `_post_1080x1350.mp4`, `_square_1080.mp4`, `_cover_1080x1920.png`: drawn by
  `Export/draw_orb.py` (every shape is in that file: the ball's light and shadow, his two glints, the specks, three
  lightning forks, four hand-drawn bolt maps turned onto eight places). 25 different drawings over the 48 frames
  (the rest are holds), 1.4% lone pixels. CLAUDE-DRAWN from his orb design: any caption must not call it his pixel
  art. `make_aseprite.lua` now takes `--script-param name=a,b` and any frame size; `make_instagram.py` takes names.
- HIS, still open: which bolt colour; which version he posts; the earlier too-bright `shift_orb_square_1080.mp4` and
  the six `shift_orb_*` files in his Photos library are his to delete.

## 16 Nov 2026 14:3x — ORB TONED DOWN, here and in the GIF (his "the orb emission is slightly too bright and the yellow is stark", then "apply the fixes and polish") — NOT PLAYED
Files only, compiled outside Unity (0 errors); no batch here. Both shaders are now the lab's (so the settable clock is in too).
- Outer glow (`ShiftOrbAura`): brightness default 0.75, falloff 5 (the falloff he had set in the lab; he had raised
  brightness to 1.25 there, which is what read too bright), a far colour (violet `_FarColor`, taking over 0.45 radii out),
  and a flash/strike now lifts it by 0.8, not 1.5 (`_FlashGain`). This project's aura material carries no saved numbers,
  so it follows those defaults.
- The strike (`ShiftOrb`): no longer blanks the ball (shader: 0.55 white, was 1.0); `flareStrength` 0.95 (was 1.6) and
  `flareTime` 0.4 in the script and in `Resources/FX/ShiftOrbVisual.prefab`; lightning flash brightness 2.0 (was 2.6).
- Ink: `ShiftOrbInk.mat` black -> deep purple (0.102, 0.043, 0.18).
- 🔴 BOLT COLOUR LEFT AS FOUND HERE: `ShiftOrbBolt.mat` is (1.3, 1.9, 2.1), a pale cyan-white, not my yellow: set here
  during his test (by him or by the other session, not by me). The lab's bolts are now gold (0.949, 0.784, 0.294).
  Which one the game keeps is his call.
- GIF, two versions in `Shift Orb Lab/Export` and his iCloud `Shift Orb` folder (the earlier bright files are replaced):
  `shift_orb_gold_*` (gold bolts) and `shift_orb_cyan_*` (this project's cyan-white bolts); each = .aseprite, 512 and
  128 GIF, square and vertical MP4. Pipeline kept beside them: `make_gif.py`, `make_aseprite*.lua`; capture =
  `OrbLabBuilder.ShootGif` (env ORB_GIF_DIR, ORB_GIF_BOLTS).
- 14:4x CHECK OF THE EXPORT (a later session, his "pick up any unfinished work"; every frame looked at and measured,
  nothing watched playing): both GIFs = 48 frames, 80 ms each, loop forever, the 128 GIF equal to the frames pixel for
  pixel; the four videos = H.264 1080 px, 30 fps, 12 s; the loop's join (frame 48 to 1) changes less than an ordinary
  calm frame does; no pixel of the strike is near white any more.
  ONE DEFECT, FIXED: in both .aseprite files the whole background sat on swatch 1, not swatch 0 (65% of the pixels one
  shade off, swatch 0 unused), because `ChangePixelFormat` keeps index 0 for "transparent". `make_aseprite.lua` now
  places each pixel by an exact lookup into palette.txt and builds both loops in one run; both files rebuilt, 0 pixels
  differ from the frames, all 32 swatches in use, tags and timing as before.
  🔴 `make_aseprite_cyan.lua` is the OLD script (superseded, still has the defect): delete it, do not run it.
- 15:1x INSTAGRAM FILES (his "hard to tell the difference, lets get them instagram presentable onto my phone"):
  `Export/make_instagram.py <export dir> <work dir>` rebuilds, per bolt colour, `_reel_1080x1920.mp4` (9:16),
  `_post_1080x1350.mp4` (4:5, new), `_square_1080.mp4`, `_cover_1080x1920.png` (the bolts at full spread, for the
  Reel's cover) and one `shift_orb_compare_bolts.png` (the two colours side by side: they differ ONLY in the six
  bolt frames, 38 to 43, half a second of the loop). Each art pixel = an exact 8 x 8 block on even offsets, 24 fps
  with every art frame held for two (even pacing; the old files were 30 fps, holds of 2 and 3), H.264 CRF 12, bt709
  tags, a silent AAC track, 12 s. Measured on decoded frames: largest colour error at a block centre 10 of 255.
  The old reel and square files are replaced. Not watched playing; not seen on the phone.
  ON THE PHONE: all of it in iCloud Drive `Shift Orb` (Files app); the two reels, the two 4:5 posts and the two covers
  also copied into `iCloudPhotos\Photos` so they land in his Photos library (the folder took them; whether they
  uploaded cannot be seen from here). His library still holds the EARLIER bright `shift_orb_square_1080.mp4` (he
  saved it at 14:18): his to delete.

## 16 Nov 2026 14:3x — THE PLAYER IS THE TEST ROOM'S PLAYER: Playroom body, crouch, roll, block, punch, kick (D-0064, Stage A of the player port) — NOT PLAYED
His "lets finish the player port now". Headless only: built by one batch (`Shift Jam > Player > Build Player Port`),
checked by temporary play probes (deleted), looked at in stills. Stage B (the cameras) is the next entry up when it lands.
- **Body:** the Playroom Male (beanie, yellow top) in every scene; the Female (hair bun, blue hoodie) under Pause or
  Title > Options > Body, shown from the next scene load. The test room's own player is the Female. The Funny
  Characters body is off the Player (its files are untouched).
- **New files:** `Player/PlayerBody`, `PlayerStance` (crouch), `PlayerMoves` (roll, punch, kick, block, reactions,
  death), `PlayerStrike` (the hit), `PlayerHurt` (block and roll protection, reactions, death then Respawn),
  `PlayerAnimation` (legs, strafes, one-shots, the look-around); `Data/PlayerBodySettings`, `PlayerMoveSettings`;
  `Debug/PlayerPortBuilder*.cs`; `Animation/PlayerMale.controller`, `PlayerFemale.controller`;
  `Resources/PlayerBody.asset` (measured, rewritten by the builder) and `Resources/PlayerMoves.asset` (his to tune).
- **Keys changed:** C is crouch now (put-down moved to X); X also holsters or draws; Q is block (drop the weapon moved
  to Z); V is first person (was F4); Left Ctrl rolls; right mouse aims or kicks. H shows them all.
TO PLAY (Workshop, then the course):
1. Walk and sprint. The legs run at 1.35x and 1.6x; the feet slide (matched would be 2.7x and 3.9x). Judge the look.
2. Hold C: crouch and crouch-walk. Walk under something low, let go: he stays down, Space does nothing, he stands when clear.
3. Left Ctrl: the roll, with and without a direction held. It carries about three body widths.
4. With empty hands (X holsters a weapon): left mouse punches, right mouse kicks. Hit a practice target, a soldier, a loose prop.
5. Hold Q: block. Let a soldier hit you while blocking (half damage) and while not (a flinch; a knife hit is the heavy one).
6. Die to a soldier: he falls, and two seconds later the usual respawn cut.
7. Stand still 8 seconds: he looks around.
8. E on a prop, carry it, X sets it down. Drive a car. Shift (F). V for first person and back.
9. New Game on the course: the wake-up in the toy box (he now gets up from lying face down).
TUNE: `Resources/PlayerMoves.asset` (crouch, roll distance and protection, block share, punch and kick reach, damage and
shove, reaction locks, respawn wait, the look-around wait); `Resources/PlayerBody.asset` > Max Playback (the legs' cap;
the rest of that asset is measured and rewritten by the builder).
FOR HIS EYE: the leg speed and slide; the crouch pose (arms out, the pack's clip); the block (a 3.5 s acted clip played
once and held, as in the test room); the carry: with a 0.6 box the hands end 0.27 short of its sides (the new arms are
shorter); the wake-up; whether the Male or the Female should be the default.
KNOWN: the moves are third person only; no strike sounds or hit marker yet; a kid's punch lands at a kid's chest
height, over small props.

## 16 Nov 2026 14:1x — ORB AS PIXEL ART: an Aseprite file, a GIF and two videos (his "an aseprite version of my orb, animated, a gif i can share", "on iphone", "maybe my instagram")
He picked "from the real orb": 54 frames rendered from the lab's orb out of Play (`OrbLabBuilder.ShootGif`, 512 px,
12 a second, bloom on; calm, stirred, strike, bolts, settle), shrunk to 128 x 128, one 32-colour palette for the whole
loop, no dither, the last six frames blended into the first so it loops. All in `Unity Projects/Shift Orb Lab/Export`:
`shift_orb_128.aseprite` (48 frames, indexed, tags calm / stirred / strike / bolts / settle; built by
`make_aseprite.lua` with aseprite.exe -b), `shift_orb_512.gif` (4x), `shift_orb_128.gif`,
`shift_orb_square_1080.mp4` and `shift_orb_reel_1080x1920.mp4` (H.264, 12 s = the loop three times, no sound).
The GIF and both videos are also in his iCloud Drive, folder `Shift Orb` (the Files app on his iPhone).
What it is, for any caption: the game's orb, rendered and pixelated by script. Not hand-pixelled, by him or anyone.
- LAB SHADERS ARE AHEAD AGAIN: `ShiftOrb.shader` and `ShiftOrbAura.shader` there read a settable clock
  (`_ShiftOrbClock`, unset = the game's time, so no change in play). Copy both here with the next landing.
- 🔴 The Aseprite MCP cannot start Aseprite (its path is the Windows one, `%USERPROFILE%\Apps\aseprite\build\bin\
  aseprite.exe`, "not an executable file" from WSL); the exe itself runs fine from WSL by its /mnt/c path.
- 🔴 `Sprite()` made in Lua has an all-black palette: `ChangePixelFormat{format="indexed"}` then turns every frame
  black. Set the palette first (`spr:setPalette`).

## 16 Nov 2026 13:3x — NEW WORKING TITLE ART IN (his "lets get the new working title in, feel free to polish up") — NOT SEEN IN THE GAME
His colour title drawing (`iCloudPhotos/IMG_2950.PNG`, byte-identical to `iCloudDrive/Procreate/Shift_Title_V2.png`,
4096 x 2048 on white) replaces the white lettering at `Resources/Titles/title_shift_wide.png` (same .meta, same GUID;
2048 x 953; the old lettering is in git, `f9a283b`). Only that PNG changed: no script (he was testing in Play).
How it was cut (`docs/tools/title_cut.py`, stills `docs/stills/title_v2_cut_on_dark.png`, `title_v2_menu_mock.png`):
- The paper goes, the white-FILLED shapes stay: everything reachable from the border without crossing ink gets alpha =
  distance from white (so his pink, purple and cyan glows fade out cleanly); ink and every enclosed shape (the white
  arrows, the white letter tops) stay solid. Plain alpha-from-white would have made the arrows see-through.
- Nine stray specks on the paper removed (the largest 150 px in a 4096-wide drawing). Nothing of his lines was redrawn.
- Resized with premultiplied alpha, so the soft edges carry no dark fringe.
OPEN (wants a script change, so it waits until he is out of Play; TitleMenu.cs also has another session's edits):
(1) in today's layout (a 1300 x 440 box) the art shows at 946 x 440, about a quarter narrower than the old lettering
did, and "SHIFT" is harder to read at that size: a taller box (about 1500 x 600, buttons nudged down) would fix it;
(2) near-square windows still show the OLD white stacked lettering (`title_shift_stacked.png`): the new drawing has no
stacked form; (3) the itch page and the build's icon still use nothing of it.

## 16 Nov 2026 13:2x — THE YELLOW BOLTS NOW MARK THE END OF A SHIFT (his "yes proceed"; the 10:1x checkpoint below is finished) — NOT PLAYED
Compiled outside Unity in both projects (0 errors); both editors were closed, so one hidden batch rebuilt the orb
prefabs here and one rebuilt the lab.
- `FX/BoltBurst.cs` (new): the bolts as one short burst on their ring (flick on, shoot out, flicker in 0.05 s steps,
  gone; the ring faces the camera while it plays). The orb no longer handles bolts itself.
- `FX/ShiftOrb.cs`: `Strike()` = the white flare and an inner flash only; `Bolts()` plays the burst. The single-bolt
  flicker near the orb is deleted (not just zeroed). Bolts start at 0.8 size, so they no longer begin inside the glass.
- `FX/ShiftOrbLink.cs`: core used -> `Strike()` at once, and when THAT shift finishes (`GameEvents.Shifted`) ->
  `Bolts()` around the orb. `ShiftOrbLink.LastUse` = the time a core last started a shift.
- `FX/ShiftEndBolts.cs` (new, added to the player by `Shifter.Awake`, like ShiftCharge): a shift no core started (the
  carried shift on F, the dev keys) gets the same burst around the player, 0.65 x the player's new scale, 0.45 s. The
  ring is copied once out of `Resources/FX/ShiftOrbVisual`.
- `ShiftOrbBuilder` gives the ring its BoltBurst; prefabs rebuilt (`strikeReach` 0.8/1.3, `flareTime` 0.55).
HIS PLAYTEST: at a core press E: white flare at once, and as the grow or shrink finishes the yellow bolts burst around
the orb, about half a second. Away from any core press F (carried shift): the bolts burst around the player as it
finishes, sized to the new size. Standing near an orb: no yellow at all, only the cyan crackle. Judge: the size of the
burst on the kid (it scales with the player: it may be too big or too small from the kid camera) and its timing.
UNCOMMITTED, with the rest of the orb work.

## 16 Nov 2026 10:2x — THE ORB'S BOLTS GLOW (his "the lightning blocks look a bit awkward", option 1) — NOT SEEN, UNCOMMITTED
Done by the rivals/merge session in the orb's files (the orb session: keep this when you copy the lab's files across).
The eight strike bolts were flat yellow with a black outline (stickers round a glass ball). Now: `Data/ShiftOrbBolt.mat` is
see-through and adds light; new `Data/ShiftOrbBoltEdge.mat` replaces the black ink on the BOLTS only (the crystal's own
outline keeps ShiftOrbInk); `Resources/FX/ShiftOrbVisual.prefab`: the 8 bolts' second material switched (text edit);
`FX/ShiftOrb.cs`: `TintBolt` colours each bolt from the orb's glow (so the Pocket Core's are amber), each flicker has its
own brightness, and they fade out over the strike (`boltWhiteness` 0.55, `boltBrightness` 1.3, `boltEdge` 0.45 on the
visual); `ShiftOrbBuilder` makes the two glowing materials (`Glow`). Compiled outside Unity, 0 errors. His bolt shapes are
unchanged. NOT in the lab's copies. For his eye in Play: E at a core. Bloom is still 0 here, so the glow is modest.

## 16 Nov 2026 10:1x — CHECKPOINT (FINISHED 13:2x, entry above): usage limit cut the "bolts at the end of the shift" change mid-way
His "yes proceed" to: fire the yellow bolts from the END of the shift (around the orb for a core shift, around the
player for a carried shift on F). State when the limit hit:
- THIS PROJECT: nothing of this change landed. It still runs the 09:3x orb plus `nearBoltEvery: 0` in the visual prefab.
- THE LAB (`Unity Projects/Shift Orb Lab`), written but NOT COMPILED, NOT RUN: new `FX/BoltBurst.cs` (the bolts as one
  burst on the ring); `FX/ShiftOrb.cs` reworked (Strike = white flare only, new `Bolts()`, no single-bolt flicker,
  bolts start at 0.8 so they no longer begin inside the glass, new `flareTime`); `ShiftOrbBuilder` adds BoltBurst.
  The lab's Space key still only strikes: `OrbLabViewer` needs `Bolts()` about 0.6 s after `Strike()`.
STILL TO DO, in order: (1) compile the lab outside Unity and fix; lab viewer line above. (2) Copy BoltBurst.cs and
ShiftOrb.cs here (he must be out of Play: Fast Script Reload). (3) `ShiftOrbLink`: on Used -> Strike, remember it, and
on `GameEvents.Shifted` -> `orb.Bolts()`; keep a static time of the last core use. (4) New `FX/ShiftEndBolts.cs` on the
player (added in `Shifter.Awake` like ShiftCharge): on its Shifter's Shifted, if the shift did not start at a core,
play a BoltBurst cloned from the "Bolts" child of `Resources/FX/ShiftOrbVisual`, at the player's middle, sized
0.65 x Shifter.Scale. (5) This project's `ShiftOrbBuilder`: add BoltBurst to the ring, drop the `bolts` list wiring.
(6) Compile outside, then his play. The room-in-the-orb pass still waits on his pick (see-through, mirror, or both).

## 16 Nov 2026 10:0x — HIS FIRST LOOK AT THE ORB IN THE WORKSHOP: the yellow bolts clash; they stay for the shift only
His words: "the large yellow lightning bolts clash with the cyan lightning aesthetic, its okay, we can save the yellow
lightning bolts for something else, i do like them appearing at the end of the shrink and grow animations."
Done (no script touched: he was in the editor and Fast Script Reload patches .cs during Play): `nearBoltEvery: 0` in
`Resources/FX/ShiftOrbVisual.prefab`, so the single bolt no longer flickers out when the player stands near. The bolts
now show only on a shift at a core (0.55 s, over a 0.6 s shift, so they are what is left on screen as the warp clears).
(SETTLED 13:2x: the flicker is deleted from the script.) OWED when he is out of Play: the same 0 as the script default in `FX/ShiftOrb.cs` (the lab's copy has it), or a rebuild
by Shift Jam > Dev > Build Shift Orb brings the flicker back. OFFERED, not built: fire the bolts from the end of the
shift itself (GameEvents.Shifted), which would also give a carried shift (F) its bolts, around the player.

## 16 Nov 2026 10:0x — PREP ONLY: the room showing in the orb (his "begin preparing what we need to do the mirror reflection in the orb itself") — NOTHING BUILT IN THE ORB
Facts read from this project: PC renderer = Forward+ (`m_RenderingMode: 2`), probe blending + box projection + atlas on;
Opaque Texture ON at half size (`m_OpaqueDownsampling: 1`), OFF on the Mobile asset; NO reflection probe in Greybox,
Workshop or Raceway (a mirror would show the default sky); `realtimeReflectionProbes: 0` in QualitySettings; MSAA 4.
Two ways, to be tried side by side in the lab behind two sliders at 0 (plan sent, waiting on his word):
- SEE-THROUGH (the room behind the orb, upside down and shrunk): the orb's pass moves to queue Transparent-100 (keeps
  ZWrite) and reads `SampleSceneColor` (DeclareOpaqueTexture.hlsl) around its own screen centre, flipped. Needs nothing
  else here. Shows only what is on screen; soft (half-size copy); nothing on the Mobile asset.
- TRUE MIRROR (the room around the orb, on the glass): `GlossyEnvironmentReflection` with the URP keywords
  (`_REFLECTION_PROBE_BLENDING`, `_REFLECTION_PROBE_BOX_PROJECTION`, `_REFLECTION_PROBE_ATLAS`, `_CLUSTER_LIGHT_LOOP`),
  strongest at the glass edge. Needs a picture of the room: a live probe riding the nearest orb (128, one face a frame)
  and `realtimeReflectionProbes` turned on here, or baked probes per room (scene edits).
Done in the lab only: `OrbLabBuilder` now builds walls (inside faces only), a checkered floor, his title drawing as a
poster, two more blocks and a live probe at the orb, and turns live probes on in the lab's settings. Applied when he
clicks Shift Jam > Lab > Build Orb Lab (not clicked yet).

## 16 Nov 2026 09:4x — D-0063 PAINT: the test room's paint ball, splats and the Colour Corner are in Shift Jam — UNPLAYED
First piece of the testing-zone merge (his "lets bring it over to unity for now"). Ported from the Godot test room with
its numbers: the paint ball (a shell that breaks or skips by its speed into the surface), the splat (spread formula,
1.6x, the 1/sin stretch, spatter ring, drops with drag, runs with a bead, shell flakes, a paint mist, soaked-in splats
on soft things, budgets 560 / 300, 45 s life), grey toys that take their colour back hit by hit (Colourable), soldiers
that rub it off again, and the Colour Corner level (%, clock, the room brightening, best time). Splats are URP decals
stuck to what was hit, so they wrap the model. A new sandbox scene, ColourCorner.unity, built by script from the
Playroom pack as the Godot level has it (4 mats, 27 toys, 3 soldiers; walls and lights added).
Checked headless (hidden batch runs, no editor opened): the builder's 22 checks pass; a temporary play probe (deleted)
pulled the blaster's real trigger: 32 of 32 checks, 0 errors (the numbers are in D-0063); stills looked at
(`docs/stills/paint_*.png`): the splats sit the right way round and the right size, wrap the car's wing, run down a
wall, shell flakes lie over the paint, grey toys turn to colour, no pink. Greybox, Workshop and Raceway open and draw
with 0 errors through the changed renderer.
TO PLAY: (1) title menu > Colours (third button in the sandbox row; there is no console word for it yet);
(2) you start on the mats with the Dart Pistol and the SMG (1 / 2), paint loaded: shoot the grey toys and
mats: each hit brings some colour back, "Colour N%" climbs top centre, the room gets brighter; (3) T (pad: D-pad up)
swaps to darts and back: darts knock the three soldiers over, they get up after 6 s and rub colour off the nearest
coloured thing; (4) shoot a wall (runs trickle down), the floor at a flat angle (long streaks), a toy (paint wraps it);
(5) 100%: bursts, a best time, Enter starts over; F9 resets any time (all paint gone, everything grey).
Paint also works in the other scenes once a player there has an AmmoBelt (only the Colour Corner's player has one).
TUNE: everything is in `Resources/PaintSettings` (tooltips say what each number does). The ball's speed 32, drop 6, life
5 and hit radius are on `Prefabs/Combat/PaintBall`. Soldier health (45: three pistol darts) on
`Prefabs/Combat/MischiefSoldier`. Shift Jam > Paint > Build Paint rebuilds the scene and keeps the numbers.
FOR HIS EYE (nobody has seen any of this move): how big and how glossy the splats are (`spreadScale` 1.6, `wetGloss`
0.85); glancing floor hits are long thin streaks (`flattest` 8); the paint looks dull while the corner is grey (the
screen is at saturation 0.7 and the lights at 0.7 until colour comes back: `saturation`, `light`); the room's light
(guessed); the "Colours" button and the two beside it are in smaller letters to fit three in the row; the readout at
the top; the ball's streak and the burst in motion; the soldiers slide (no walk clip); the sounds (unheard).
STOPPED HERE at his word (he wanted the editor): all of it is committed. Left for later: one more Build Paint run
after the probe was deleted (the scene was built by the same code, so nothing should change); the kept probe is in
`%TEMP%\shiftjam-stage\paint\probe-keep\PaintProbe.cs` (put it back in Scripts/Debug to run it again:
`-executeMethod ShiftJam.PaintProbe.Run`, add `-paintStills` for pictures).

## 16 Nov 2026 09:3x — THE ORB IS IN: every shift core wears it in Play (D-0062, his "this looks good, lets get this in") — NOT PLAYED HERE
He approved the look in the lab (lightning arcs for his squiggles, the outer glow, crackles escaping). Ported, compiled
outside Unity with the other session's files (0 errors), both prefabs built and six stills rendered by one hidden batch
(`docs/stills/orb_in_shift_jam_2026-11-16.png`, no bloom: as this project is today). He had changed no numbers in the lab
(all defaults on disk), so the look is exactly what he saw.
- NO SCENE WAS EDITED (the paint-port session is working in this tree). `ShiftSource.Start` calls `ShiftOrbLink.Dress`:
  a core with no orb gets `Resources/FX/ShiftOrbVisual` hung on it, sized to its body (the wooden sphere's mesh, else
  its collider), and its old mote, sparks, ember and lights stop drawing (`ShiftSource.RetireLooks` + renderers/lights
  off; colliders untouched). So the tutorial cores, cores A-D, the Workshop cores and the Pocket Core (amber, via
  `ShiftOrb.SetGlow`) all change with no rebuild. In the editor's scene view they still show the old mote.
- `FX/ShiftOrb.cs` = the lab's decoupled one (SetAwake / SetViewer / SetGlow / Strike). `FX/ShiftOrbLink.cs` (new) joins
  it to the rule: Usable -> awake, Used -> strike, the Shifter and its Scale -> who it stirs for.
- `Shaders/ShiftOrb.shader` (lightning arcs; `_LinesDrawn` 0 keeps his drawn lines as an option),
  `Shaders/ShiftOrbAura.shader` + `Data/ShiftOrbAura.mat` (second material on the halo quad), `ShiftOrbBuilder` now makes
  the visual (radius 1) and the Build Mode piece (`Pieces/Interactables/ShiftOrb`, radius 1.5, not in the catalogue yet).
- UNCOMMITTED (mine): Art/Orb, the two shaders, Data/ShiftOrb*.mat, FX/ShiftOrb.cs, FX/ShiftOrbLink.cs,
  Debug/ShiftOrbBuilder.cs, Resources/FX/ShiftOrbVisual.prefab, Pieces/Interactables/ShiftOrb.prefab, ShiftSource.cs
  (Used event, Start, RetireLooks), docs. Left for his word, beside the other session's paint work.
HIS PLAYTEST (Greybox, New Game): the toy-box core is a purple crystal with a cyan edge, not the flat mote; stand back
(calm, a crackle now and then), walk up (it stirs, a bolt flickers out), E (white flare, eight bolts, the shift); the bed
core the same; a dormant core (wardrobe, core C) is dark glass with an ember until lit; F2: the Pocket Core is amber.
Judge: size against the old mote (crystal = the wooden sphere's size, halo 5 radii), whether the bolts read at kid size,
anything left of the old mote or sparks. Tune `Data/ShiftOrbCrystal.mat`, `Data/ShiftOrbAura.mat`, and the ShiftOrb
numbers on `Resources/FX/ShiftOrbVisual` (the lab stays the place to try things: copy the .mat files across).
NOT DONE: `CourseBuilder.Core()` and DevToolsBuilder still build the old mote (harmless: it is retired at run time);
bloom is still 0 in this project; the room-through-the-ball second pass.

## 16 Nov 2026 09:2x — D-0061 rivals: a 4-car race on the Raceway (grid, 3-2-1-GO, place, results) — UNPLAYED
Finished and committed locally (the 08:2x checkpoint below called it half-built: no longer). Four cars stand on a grid
behind the finish line: BLUE, YELLOW, GREEN, PINK. Sit in any of them and the other three are rivals. They drive the
same car through the same physics (`ICarDriver`: the player's input and `RivalDriver` both fit it), along a path the
raceway builder bakes from the circuit table (`RacePath`: 664 points, each with the road's up, width and a suggested
speed). The race is the old RaceLoop race started from the grid; `RaceField` holds the field and the standings.
Checked headless with a temporary play probe (deleted): the real player seated in the pole car and never
moving, three rivals: 3 of 3 ran 3 laps (45.6 to 49.8 s a lap, eased 10% by the rubber band because the player
stands), 0 rescues, 0 wall touches, 0 car touches, the standing car never touched, the count held all four still, the
place right at 12 of 12 line crossings, results in crossing order, 0 errors (time scale 4 and 1.0). A rival driving the
player's car too: 4 of 4 ran 3 laps (42.0 to 45.2 s), 0 rescues, 0 wall touches, 0 car touches, places right at 16 of
16, 0 errors (time scale 1.0; the same at 4). Wrecks, falls and a car on its roof were put back on the road (the
player's 8.0 past the last gate, still in the seat). The race alone over the line, the Workshop and Greybox: unchanged,
0 errors. Every check was seen failing first (D-0061 lists how).
No editor was opened on this project (hidden batch runs only, one at a time, low priority).
TO PLAY (Raceway.unity): (1) climb the steps, get in any grid car (E): "RACE  PRESS R" shows top right; (2) press R
(pad LB): all four line up, 3-2-1-GO, nobody can move before GO; (3) race 3 laps: PLACE 2/4 sits left of the lap
panel; the rivals keep to lanes, go round each other and you, slow for the spiral and the dive's crest, use nitro on
the straights; (4) finish: a results card with all four (a car still out shows a dimmer time: what it is on course
for); the rivals drive on a little, then are put back on the grid; (5) wreck your car in a race: you stay in the seat
and 2.5 s later the car is whole, just past the last gate you took; drive off the road and stay off 3 s: the same;
(6) get out, F9 or Build Mode ends the race and sends the rivals home; (7) driving over the line with no race on still
starts a lap race alone, as before; (8) pick the pole car (BLUE) for an easier race: pole is the quickest rival.
TUNE in Resources/Rivals: "Rivals" (each slot's pace, corner pace, lane, nitro mark), `cornerSpeedFactor` (the whole
pack in bends), "Rubber band" (`rubberBand` off = every rival runs its own race), `lookAhead`, "Trouble" (when a car is
put back), "The start" (the count), `resultsSeconds`. The grid's cars and lanes: `RwGrid` in CourseBuilder.RaceField.cs
(then Shift Jam > Raceway > Build Raceway Room).
FOR HIS EYE AND EAR: how hard the rivals are; the HUD in motion (stills of it: docs/stills/race_*.png); the count's
sounds (the drift chime low for 3-2-1, high for GO: unheard); whether R / LB is the right start.
LEFT AS IT IS: ramming rules are unchanged (a rival that rear-ends a car at speed costs it up to 80 of 150); rivals
do not drift; the ToyRacers are not on the grid; the Workshop and Greybox have no grid and behave as before.
STILLS (docs/stills, not in git): race_grid.png (the grid), race_results.png (the card, PLACE and the lap panel),
race_count.png and race_pack.png (the count and GO!: taken BEFORE the count's panel was made smaller and moved up; it is
now 300 x 230 in the upper middle). The HUD in them is the real one, lent to a still camera behind the car.
FOUND, NOT FROM THIS WORK, LEFT: a car that rides up over a cone or a toy block can dip its nose into the road, and
the road then counts as a crash: 40 to 49 health in one hit (seen with a cone 0.22 s before the hit; the slot-4 car
did it on lap 1 after the loop in 3 of the probe's 8 four-car races). It would hit his car the same way. His call (D-0061, last point).
NEXT: his drive of the race; then the push (his).

## 16 Nov 2026 09:1x — THE ORB HAS ITS OWN LAB PROJECT (his call: this project is held by the rivals session) — NOT PLAYED
His words: "shift jam is currently being used by another claude instance, lets try opening it up in goblin delivery
service or flight of the hummingbird or its own project". Made `Dev\Unity Projects\Shift Orb Lab` (Unity 6000.3.10f1):
this project's ProjectSettings and `Assets/Settings` copied (the ShiftWarp full-screen feature removed from PC_Renderer),
packages cut to URP + Input System + uGUI, and the orb's files copied WITH their .meta files at the same paths (shader,
fbx, cut-outs, three materials, OrbGlow.mat + orb_mote.png, Billboard.cs). One hidden batch built the prefab and
`Scenes/OrbLab.unity` (0 errors); the editor was then opened on it for him.
- Lab only: `Scripts/Lab/OrbLabViewer.cs` (drag = orbit, scroll = in/out and so "near", Space = strike, D = dormant,
  B = bloom, H = help), `Scripts/Debug/OrbLabBuilder.cs` (Shift Jam > Lab > Build Orb Lab), `Data/Lab_*.mat`, `Lab_Bloom.asset`.
- (PORTED 09:3x, entry above.) THE LAB'S `ShiftOrb.cs` WAS THE NEWER ONE: it no longer reads ShiftSource / Shifter; its owner calls `SetAwake`,
  `SetViewer(transform, size)` and `Strike()`. This project still has the first version (reads ShiftSource itself).
  When this project is free: copy the lab's ShiftOrb.cs here, add a small link beside the ShiftSource (Used -> Strike,
  Usable -> SetAwake, the Shifter -> SetViewer with its Scale), add it in ShiftOrbBuilder, rebuild the prefab. Copy back
  whatever he tunes: `ShiftOrbCrystal.mat` values and the ShiftOrb component numbers.
- 🔴 FOUND: this project's `Settings/DefaultVolumeProfile` has Bloom at intensity 0, so nothing in the jam blooms today.
  The lab has its own bloom volume (0.9, threshold 0.95) so he can judge both with B. Turning bloom on here is a
  whole-game look call: his.
- 09:3x, LAB ONLY SO FAR (port with the rest): (1) his words "i dont like my squiggles in it, those are essentially
  representing crackling lightning or charging electricity": `ShiftOrb.shader` now draws five jagged arcs from the
  core to the glass, each redrawn on its own beat (`_Crackle`), a few at rest (`_LinesRest` = charge at rest), all on a
  flash; his drawn lines stay as `_LinesDrawn` (0 = off). He saw it: "looks good". (2) his "a bit more outer emission
  glow and some of those crackles escaping it": new `Shaders/ShiftOrbAura.shader` + `Data/ShiftOrbAura.mat`, a second
  material on the halo quad (his mote stays first): outer glow + six escaping arcs, driven by the colour ShiftOrb
  already sends the halo (above 1 = flashing). NOT SEEN YET. ShiftOrbBuilder adds it on a rebuild.
- The lab is a viewer for this project, not a new game: no docs of its own, no git; this entry is its record.

## 16 Nov 2026 09:0x — SHIFT ORB FIRST PASS BUILT (D-0062): a crystal that stirs and strikes — NOT PLAYED, not in any scene yet
His brief: "an ethereal warp portal", "Manimekhala's crystal orb or Palantir"; the room-through-the-ball idea is held
back ("ambitious, lets stick with the first pass first"). Built, compiled outside Unity (0 errors), prefab made and six
check stills rendered by one hidden batch (`docs/stills/orb_first_pass_2026-11-16.png`; the stills have no bloom):
- `Shaders/ShiftOrb.shader` (ShiftJam/ShiftOrb, opaque unlit, own depth passes): dark purple glass, bright thin edge, one
  drawn highlight, mist sampled at four depths along the view ray (real parallax), his dots as stars on two inner shells,
  his lines as inner lightning (faint at rest, bright on a flash), ember when dormant, white flare on the strike.
- `Scripts/FX/ShiftOrb.cs`: rest / near / strike / dormant from the ShiftSource above it (new `ShiftSource.Used` event);
  property blocks only (no material asset writes); bolts hidden at rest, one flickers out when the player is near, all
  eight burst on use; the bolt ring faces the camera; halo (his mote) and light follow the glow colour.
- `Scripts/Debug/ShiftOrbBuilder.cs`: Shift Jam > Dev > Build Shift Orb -> `Prefabs/Pieces/Interactables/ShiftOrb.prefab`
  (trigger + ShiftSource Toggle + ShiftOrb; crystal 3 units across), materials `Data/ShiftOrbCrystal|Bolt|Ink.mat`,
  cut-outs `Art/Orb/orb_lines.png`, `orb_dots.png` (from his title art, 172 px source: soft up close).
NOT DONE: no scene holds it, the Build Mode catalogue is not rebuilt, the course cores and the Pocket Core still use the
old mote (the builders are mid-edit by the rivals work: left alone). All of this is UNCOMMITTED, beside the rivals work.
HIS PLAYTEST: drag the ShiftOrb prefab into the Workshop (about 2 units above the floor), Play; stand back (calm, a flash
every few seconds), walk up as the toy (swirl quickens, flashes come fast, a single bolt flickers out), press E (white
flare, all bolts burst, the shift). Tune on the ShiftOrb component (colours, ranges, timings, Show Ink Outline) and on
`ShiftOrbCrystal.mat` (mist, edge, highlight, lightning brightness). Judge: purple vs cyan balance, flash brightness,
whether the bolts read at kid size.
🔴 Two Claude sessions shared this project today: my batch was refused twice while the rivals probe held the project.
Check for a running Unity before every batch.

## 16 Nov 2026 08:4x — SHIFT ORB MODEL exported (his Blender work) — MODEL ONLY, nothing in the game uses it yet
He modelled the orb from his title drawing (`iCloudDrive/Procreate/Shift_Title_V2.png`) in Blender 5.2, step by step:
`Dev\Blender Projects\orb.blend` (his hand-made state before my pass: `orb_handmade_backup.blend`, same folder). On his ask
I thickened the bolts' ink ring (0.023 -> 0.045), varied the six bolts from his one traced shape, added two smaller ones
top and bottom (`Bolt_07`, `Bolt_08`: his to keep or delete), and exported `Assets/_Game/Art/Orb/ShiftOrb.fbx` (untracked,
Unity was closed: no .meta yet). Contents, checked by re-import: `Orb` (UV sphere r 1, material `Orb_Body`, his purple
~#4400AE as a placeholder), `Orb Outline` (inside-out shell r 1.06, `Ink_Black`), `Bolt_01..08` (0.15 thick, closed,
`Bolt_Yellow` face + `Ink_Black` ring and sides, every pivot at the orb's centre, all in one flat plane). Y up, 1 unit = 1.
The Unity side followed at 09:0x (entry above). He means the orb to replace the shift cores "or some type of
interactable": cores assumed first, portals not confirmed.

## 16 Nov 2026 08:2x — CHECKPOINT: the 5-hour limit cut two agents mid-work (resets 11:30) — the rivals were finished after all: see D-0061, 09:2x, at the top
1. AI RIVALS (D-0061, his "proceed"): [09:2x: FINISHED, PROVEN IN PLAY AND COMMITTED; the entry at the top replaces this item.] As it stood at 08:2x: UNCOMMITTED and UNFINISHED in the tree. New: Race/RaceField, RaceGrid, RacePath, Racer,
   RivalDriver, RivalLane, RaceRescue, RaceStartKey, UI/RaceHud, Vehicles/ICarDriver, Data/RivalSettings, Resources/Rivals.asset,
   Debug/CourseBuilder.RaceField, Debug/RivalsProbe (TEMPORARY, delete before commit). Changed: ArcadeCar, CarSeat, PlayerInput,
   RaceGate, RaceLoop, DrivingHud, KeyHelp, CourseBuilder.Raceway, and Raceway.unity (one landing run). It compiles outside
   Unity (0 errors). NOT proven: no lap probe result exists. Resume the same agent after 11:30; do not start a new one.
2. PAINT PORT (first piece of the testing-zone merge, his "lets bring it over to unity for now"): only the 65 splat images
   are exported, to %TEMP%\shiftjam-stage\paint\stage\Assets\_Game\Art\PaintSplats; no Unity code written yet. Nothing of
   it is in the project. Resume the same agent after 11:30.
3. Safe to play meanwhile: Greybox and the Workshop (damage, wreck). The v6 build (Builds/Windows/v6) is from before the rivals work.

## 16 Nov 2026 06:5x — D-0060 the wreck in pieces (Fraggo cuts every car body; the wreck breaks it up) — UNPLAYED
Part 2 of the damage plan. Shift Jam > Workshop > Build Car Wrecks cut all 25 car bodies into 9 pieces each with
Fraggo (run headless; every body's pieces add up to 100.0% of its surface). On a wreck the body breaks up: 7 pieces
fly at the car's speed, 2 stay as the dark hulk, the 4 wheels pop off and bounce, the rest of the car is hidden; the
tow (5 s), a repair or F9 puts the car back whole and the pieces shrink away. Checked headless: a play probe wrecked
all 25 car pieces in turn (25 of 25 pass, no errors; probe deleted) and stills were looked at (docs/stills/wreck_*.png).
Unity left closed. 07:1x: the paint bake made safe against no-graphics runs (D-0060, last point); the 25 chip
colours are unchanged; the tractor's chips stay orange (20% of its body is orange-red, the rest reads as grey).
TO PLAY (Workshop or Raceway): (1) wreck a car at speed: the pieces should carry on the way the car was going;
(2) wreck one standing still (darts or ram it): pieces burst outward; (3) wait 5 s: the car is towed back whole;
(4) look at the cut faces (the car's paint, darker) and at the hulk left behind. Tune in Resources/CarDamageFx under
"Wreck in pieces": `pieceBurst` (how far they fly), `hulkPieces` (0 = the whole body flies), `insideShade`,
`wheelBounce`, `wreckShake`. `cutCells` changes the piece count (then run Build Car Wrecks again).
FOR HIS EYE: the bodies are hollow shells, so some cut faces are flat panels with ragged edges; the spread is 1 to
3 car lengths; the wreck's sounds are still D-0059's (unheard).
FOUND, NOT MINE, UNCOMMITTED: a pack import on 14 Nov 10:42 overwrote ProjectSettings: physics (default gravity -9.81
to -20, max angular speed 50 to 100, the layer collision matrix), TagManager (layers 6-8 named bikebody / wheels /
drivable, the rendering layer names gone) and InputManager (its axes gone). The game sets its own gravity (60) and
every object of ours is on layer 0, so nothing of ours has changed that I can find; `git checkout -- ProjectSettings`
puts them back if he wants (his call). The post-processing package adds a scripting define on every load.
07:5x: v6 rehearsal build made and smoke-checked clean (Versions, below). The wheel counts the probe turned up are right as they are: ToyRacer03 is a hovercraft (four unseen spring points, no wheels to pop), ToyRacer06 a three-wheeler, CyberCar09 a six-wheeler.
NEXT: AI rivals for a 4-car race with a countdown and a finish (his "proceed" 16 Nov, in work); his drive of D-0058, D-0059 and this; the push (his).

## 15 Nov 2026 13:4x — D-0059 damage effects (smoke, sparks, flecks, knock sounds, loose parts, wreck burst) — UNPLAYED
The pink squares were the Playroom smoke (missing material, built-in shaders). Polygon Arsenal's URP upgrade imported
headless; new CarDamageFx on every car (D-0059), set by Resources/CarDamageFx (menu Shift Jam > Workshop > Build Car
Damage FX re-makes it). Compiled, built headless, 0 pink materials, 25 car paints baked. Unity left closed.
TO PLAY: (1) crash head-on into a wall a few times: sparks, paint chips, a metal clash; light smoke under half health,
dark under 30%, spitting sparks under 15%; (2) a really hard hit (26+): a part (spoiler, bumper...) flies off; (3) wreck
it: burst, crash, parts off; after the tow every part is back; (4) cones and glances: a soft knock only. Tune in
Resources/CarDamageFx (thresholds, fleck counts, part chance, volume; sounds swappable there).
NEXT (Part 2, plan approved): Fraggo pre-cut bodies for the wreck (Fraggo is in, at Assets/Unluck Software, ignored by
git). HIS CALLS still open: AI rivals after this.
Left alone in his tree: Packages/manifest.json + lock (animation rigging, cinemachine, postprocessing, shadergraph,
from his 14 Nov imports), fonts/mats/ERP as before.

## 14 Nov 2026 09:3x — D-0058 from his raceway drive (walls glance, cones tap, drift cues, road 18.9 wide) — UNPLAYED
His drive of D-0055..D-0057: "pretty fun"; notes = jerk on walls/cones, drift prompts, lanes cramped. All three built
(D-0058), compiled and the raceway rebuilt headless (Unity closed: he went to NectarRush): 0 in the lane, 0 clips.
TO PLAY (Raceway.unity): (1) scrape a wall at a shallow angle, then hit one head-on (glance vs crash); (2) drive
through the cones (a tap, no lurch); (3) take a hairpin fast with the wheel turned: DRIFT pill + tick, then hold
Space: sparks, rising whirr, a chime at each level, RELEASE! at two; (4) the width (18.9) and the gates' lower
arches (0.8 cap, the finish arch too). Numbers to tune: ArcadeCarSettings "Walls", DrivingFeelSettings "Drift cues".
HIS CALLS: AI rival cars for a 4-car race (none exist; online 8-player is not realistic before 1 Dec); chevron signs
before bends (left out); items 4-7. Other changes in his tree left uncommitted (fonts, PadGlow/PocketCoreGlow mats,
an ERP folder, UnityConnectSettings): not mine.

## 10 Nov 2026 21:1x — CHECKPOINT, he is in Godot, Unity closed: D-0057 (arrows, tyre stacks, F9 resets loose props, KeyHelp) — UNPLAYED
Everything from D-0050 to D-0057 is committed locally; nothing since D-0054 has been driven (he drove D-0052 to
D-0054). Raceway as it stands: road 12.5 wide, loop, two banked hairpins, spiral, dive; flags, cone slalom, block
wall, tyre stacks, road arrows, nitro stars, a heart; generated engine hum; a camera that rides the road on held track.
WAITING ON HIM: a drive of the raceway (width, hum, wind, jump thump, stars' height, the green car on the loop);
which of items 4-7 next (countdown and finish ceremony, moving scenery, a crowd, jumps); what "floated upwards".
NOT DONE: the playroom expressway is still 8.4 wide with invisible guards and was tuned at top speed 24; a coin
counter; a push of the ~100 local commits (his).

## 10 Nov 2026 21:1x — D-0056 landed headless (he had closed the editor): wider road (x1.5), generated engine hum, pickups on the road — UNPLAYED
His notes on D-0055: stars too high; driving sound still needs work; can the tracks be widened. Road 12.5 wide (one
number, `RwWiden`), loop shift 20, gates no taller than 1.6 x; engine = a hum made by code (the pack's motor loops have
nothing under 1 kHz); pickups stand on the road. Refit Cars + LandRaceway ran in batch; checks pass; stills looked at.
No build. Editor left closed (not relaunched without his word).
HIS EAR: the engine hum, the wind, the jump thump. NEXT: his drive; items 4-7 if he wants them; F9 does not put the
smashed blocks back; KeyHelp entries for drift and air control. STILL OPEN: what floated upwards.

## 10 Nov 2026 21:0x — D-0055 landed: jump sound, engine hum, the green car's clearance, raceway dressing (items 1-3) — UNPLAYED
His notes: jump squeak annoying; driving sounds like ocean waves; the green dragster catches on the loop; of the seven
additions offered, "1-3 works for now" (fast iteration, permanent passes later). Landed with Play stopped (bridge):
jump = a soft thump; every car has an engine loop and the wind is thinned; 5 car bodies raised to 0.36 (menu Refit
Cars); raceway dressed with flags, cones, a block wall, tyre stacks, nitro stars and a heart; light loose props do not
damage the car. Raceway rebuilt, checks pass, stills looked at, saved. No build.
HIS EAR decides the three sounds (none heard by me). NEXT if he wants: items 4-7 (countdown and finish ceremony,
moving scenery, a crowd, jumps), arrow boards on the bends, a coin counter. STILL OPEN: what floated upwards.

## 10 Nov 2026 20:3x — "WAY BETTER" (the slanted loop + road camera, D-0053, played); gate 1 clipped the spiral: D-0054 landed, UNPLAYED
His third drive: the loop and the camera are "way better". His still: gate 1's arch came up through the spiral's first
turn. D-0054: the build's checks now cover the whole lane, the road's body and walls, and whole pillars (and were shown
to catch the fault before it was fixed); gate 1 moved to the straight after the loop. Raceway rebuilt from the menu
through the bridge and saved: 0 in the lane, 0 clips. No build.
STILL OPEN: what floated upwards; tuning of the feel numbers by his word.

## 10 Nov 2026 20:2x — HE MADE THE LOOP ("not bad"); "didnt feel very smooth" + "camera work for the loops": D-0053 landed, UNPLAYED
The D-0052 loop fix worked in his hands. D-0053: the loop is rebuilt as a slanted strip a car drives straight through
(the old one rubbed the car along a wall), finer facets, no landing effects from hops on held road, and a chase camera
that rides in the road's frame on the loop, the spiral and the banked hairpins. Editor recompiled (0 errors), raceway
rebuilt from the menu through the bridge and saved: 45 pieces, closes to 0.001, road check passes. No build.
ASKED: if it is still rough, where (loop, spiral, hairpins, straights, or the camera's shake in general).
STILL OPEN from before: what floated upwards.

## 10 Nov 2026 19:4x — HIS FIRST RACEWAY DRIVE: the loop could not be made; fixed (D-0052), landed with Play stopped — UNPLAYED
His notes: "not getting enough speed to do a full loop"; check the other traversal ("shrinking back to toy size and
grounding on the floor rather than floating upwards"). Loop: cars fall at 60 (the props' gravity), the loop was sized
for 9.81: on sticky track the car's weight now presses onto the road (ArcadeCar). Shift: the Raceway had one charge
and no cores, so no way back to toy size: its shift is now free. Editor recompiled, 0 errors. No build.
OPEN: what floated upwards (the toy, the kid or a car, and where). On-foot code is unchanged today.

## 10 Nov 2026 19:2x — THE DRIVING PASS (D-0051) landed in his open editor (Play stopped): speed, camera, sound, rumble, trails, the loop hold — UNPLAYED
His "stopped, didnt play yet, add in the latest additions and then i test". 14 script files copied in (5 changed, 8
new, the raceway builder), editor recompiled with 0 errors, raceway rebuilt from the menu through the bridge (his
ask) and saved: 45 pieces, checks pass, 32 pieces marked sticky. No build. Not played by anyone.
HIS PLAYTEST (editor, Raceway.unity): docs/PLAYTEST.md "The driving pass". All feel numbers are first guesses.
WATCH: top speed is now 32 everywhere (playroom loop, stunt run, Workshop): jumps go further, walls hurt more.

## 10 Nov 2026 18:3x — THE RACEWAY (D-0050): a separate room, one circuit with a loop, a spiral and a dive — LANDED, UNDRIVEN
His "lacks juice, feels a bit slow, the course is very predictable and basic", "verticality as much as possible", then
"feel free to build it in a separate room". New scene `Raceway.unity` (from the Workshop's scene; in the build list;
title menu button + `raceway` console command). New: `Debug/CourseBuilder.Raceway.cs`, `TrackBender.Sweep` /
`SweepWalls`, `Vehicles/StickyTrack.cs`. Built by a hidden batch (`LandRaceway`; scene saved, checked for a cut save:
clean; stills looked at). No build. The build's own checks pass (675 road rays, lanes clear, circuit closes to 0.002).
WHAT IS THERE: an orange circuit with yellow walls on pillars in the middle of an 8 x 8 tile room: start straight (book
steps up, three cars), finish arch, boost pad, vertical loop, banked hairpin, a two-turn spiral to a deck at 42, a
dive, the second hairpin. About 1340 units a lap.
HIS PLAYTEST (editor): open Raceway.unity, Play; docs/PLAYTEST.md "The raceway".
The driving pass and the car's hold on sticky track landed the same evening (D-0051, above).

## 10 Nov 2026 14:1x — MAT BEDS (D-0049) after his first drive: the car "got stuck" on the mats — LANDED, UNDRIVEN
HE DROVE the expressway for the first time (editor): took the gold car down the entry ramp and it
beached in the gap between two mats. No other note yet (bends, guards, ramp crest, steps: not reported either way).
Fix landed headless after he closed the editor (`LandMatBeds`; Greybox + Workshop saved, both checked for a cut save:
clean): 6 beds in the playroom, 1 in the Workshop, each a smooth sloped collider under a group of 4 mats; the log's
ray check passes (289 rays a bed, 0 off). No build. Editor reopened on Greybox for him.
HIS PLAYTEST: drive a car down a ramp and across a mat group (on at one side, over the seams, off the other); walk
the toy onto a mat without jumping. Judge the 20 degree edge at speed.

## 10 Nov 2026 11:2x — THE EXPRESSWAY (D-0048): a raised track loop round the playroom, four stops, three cars — LANDED, UNDRIVEN
His "create a racetrack loop ... as an expressway, have it raised above any clearance problems" + "reproduce curvy ones
where needed" + "proceed". New: `Debug/TrackBender.cs` (bends and opens the pack's straight), `Debug/CourseBuilder.Expressway.cs`
(CourseBuilder is now a partial class). Placed by a hidden batch (`LandExpressway`; Greybox saved, checked for a cut
save: clean; stills looked at). No build. No editor was open at either check. The build log's own checks pass: joints closed,
nothing fixed in the road, 885 of 885 road rays on the deck, every lane clear.
WHAT IS THERE: orange track at height 8 on wooden pillars, 779 units round (about 32 s a lap at the car's top speed).
Entry stop (west): book steps beside the mouse hole up to a second lane, blue car; that lane's two ends are ramps to the
floor. Desk stop (north-west): lay-by + book steps, gold car. Maze stop (east, after the north-east bend): no rail on the
outside of one piece, to step off. Wardrobe stop (south-east): lay-by + book steps, dark car. The two floor rides of
D-0047 in the playroom are removed; the bedroom's pink car stays.
HIS PLAYTEST (editor, Greybox): docs/PLAYTEST.md "The expressway". Judge: the bends at speed against the invisible
guards, the hop over the ramp's crest, the kid walking across the track, F near the track, the reveal camera.
38 piles of dressing moved aside, among them: the hoop's ball (33 units) and the basketball (34), a pile of three
books from the south wall (111), the train station (7), a castle tower copy (20), three wooden cylinders, the wooden
train, alphabet cubes, and about 20 sheets of paper, cards and pencils.
Mats on foot are unchanged (still a hop each). Made meshes live in Assets/_Imports/Generated/Expressway (git-ignored,
check them in with UVCS).

## 10 Nov 2026 09:3x — RIDES (D-0047): three Cyber cars at the long toy walks — LANDED, UNPLAYED; the playroom's mats block them
His "traversing the rooms as a toy can feel like a chore" + "use the cars from the cyber car asset kit". Builder code
(`CourseBuilder.PlaceRides`, table `Rides`), placed by a hidden batch (`LandRides`; Greybox saved, checked for a cut
save: clean; stills looked at). No build. Ride_Bedroom (pink CyberCar06) west of the book stack, nose at the toy door;
Ride_PlayroomEntry (blue CyberCar14) by the mouse hole, nose at the maze; Ride_DeskFoot (gold CyberCar05) at the foot of
the last ruler ramp, nose at the wardrobe hole. E = "Drive", E again gets out; park at a door and walk in.
HIS PLAYTEST (editor, Greybox): bedroom, shrink on the bed, hop down the books: the pink car is a few steps west; drive
to the toy door (the toy box stands on the straight line: steer round it), E, walk in. Playroom: the blue car is ahead
and right of the mouse hole.
**FOUND (measured by a temporary probe, deleted):** the 24 foam mats in the playroom are 0.73 high and the cars clear
only 0.38 to 0.45, so a mat edge stops a car (4 units ahead of the entry car on the straight line); the toy on foot
steps up 0.3, so every mat is a hop too. The bedroom leg is clear. **Open, his call:** kerb slopes on the mats (my
pick), a cleared lane, or the playroom on foot. Also open: 2 shift charges (`ShiftSettings.maxCharges`), not changed.
Review 10 Nov (two Sonnet readers, read-only): 89 commits unpushed; BACKLOG's "v1-v5 done" is stale (only v5 exists).

## 9 Nov 2026 15:4x — PUZZLE PIECES SHOW (D-0046): glints, problem hints, a camera turn to what a switch did — LANDED, UNSEEN
His "its not obvious that we need to hit specific switches"; his "proceed with 1-3". Code `117f9d6` (editor + player
compile 0 errors), placed by a hidden batch (6 glints, 2 hints, 1 dark core; Greybox saved, no cut save). No build.
HIS PLAYTEST (editor, Greybox): from the bedroom's middle as the kid, the wall switch has a gold glint; walk to the toy
door: "Shut. A switch opens it." (no E); press the switch: the camera turns to the toy door as it swings, then back;
the glint fades. The flashlight in the box glints until it is on. Playroom: the safe and the ruler glint; as the toy at
the wardrobe hole: "The safe is in the way. Too heavy for a toy."; once the kid moves the safe, quiet. Wardrobe: core C
reads "Dark. It needs light."; pushing the torch's switch turns the camera to core C waking. Tune on each piece's
Highlight (minSize/maxSize/colour/bob) if a glint is too big for the toy-size beats (box, wardrobe). Reword the lines.

## 9 Nov 2026 14:4x — THE CARRIED SHIFT (D-0045): F / RB shifts anywhere for one charge — LANDED, UNPLAYED (no build)
His "go with b". Code compiled outside (editor + player, 0 errors), the wardrobe's NoShift zone + the refused sound
landed by a hidden batch (Greybox saved, checked for a cut save: clean). No Windows build (his rule): test in the editor.
HIS PLAYTEST (editor, Greybox, Play, New Game): the battery at the bottom is dark; grow at the box core: it lights and a
hint shows once. In the bedroom press F: you shrink, the battery empties; F again: "No charge"; use the bed core: full.
Jump and press F in the air: "Feet on the ground first". In the skirting lane as the toy, F: "No room to grow" (charge
kept). In the playroom, open floor: F grows; next to a wall or on the race track: no room. In the wardrobe: "Can't
shift in here", core C still works. Fall off something high: the reset refills. Workshop: starts full. Tune on the
ShiftSettings asset: maxCharges (try 2, or 99 for shift-anytime), freeShiftWait. Judge: do the bed core, maze core and
ruler ramp still earn their place now they are optional? Reword the four messages and the hint (ShiftCharge.cs top).

## 9 Nov 2026 14:2x — ASYNC: README + itch page draft, release-build trim (no build made)
`0bb3b7a`: README.md (facts, controls from the input actions, credits, the AI-tool disclosure) + docs/ITCH_PAGE.md;
his pitch, name, title and tags are marked with a pen sign. Game Off's page has no AI rule (public repo + push by
1 Dec 13:37 PST). Flag: Data/Spark.mat still uses Art/Mockups/spark_mockup.png (a scripted placeholder; BACKLOG 6).
Trim: AI Inference package removed (with its app-ui dependency; nothing used it; 0 compile errors on reopen), plain
builds drop Burst's DoNotShip folder. Both show at the next build. The 69 MB Build Mode catalogue stays until he
decides whether the Workshop ships. v5 is still the latest build and has everything gameplay-side.

## 9 Nov 2026 11:3x — WORKING RULE: NO WINDOWS BUILD PER ITERATION
His word: "i want to stop building a windows build each time we iterate". From now on changes are tested in the
editor (open Greybox or Workshop, press Play); a Windows build is made only when he asks or for a milestone (a stranger's
test, the submission). v1-v4 deleted 9 Nov (rebuildable from their commits); v5 below is the last per-change build; its play steps work the same in the editor.

## 9 Nov 2026 11:1x — v5 DEV BUILD: PAUSE MENU + OPTIONS (D-0044) — UNPLAYED, MENUS NOT YET SEEN
His "proceed" on the pause/options plan; Unity MCP busy with the hummingbird, so files + compile outside + hidden batch
only. Code `165227a` (compiled outside, 0 errors), then v5: `Builds/Windows/v5/ShiftJam.exe` (development build, 0.8 min,
smoke check clean: both scenes 0 errors). Nothing rendered headless, so the menus' layout is unseen.
HIS PLAYTEST (v5): in the course or the Workshop press Esc (or Start): PAUSED with Resume / Options / Back to Title /
Quit; sounds stop; Esc, Start or B resumes and the mouse locks again. Options: sensitivity - / + (0.25x-3x), Invert Y,
Volume - / + (tenths), Fullscreen; Esc / B goes back; quit and relaunch: the settings stay. Title: Options sits beside
Quit on the last row. Esc still leaves Build Mode / first person and closes H first (no pause on that press). Back to
Title from the Workshop lands on the course title.
Still not committed: his ERP plugin and the editor's earlier touches to two glow materials, three font assets and
UnityConnectSettings (his call).

## 8 Nov 2026 14:1x — v4 DEV BUILD (F2 Pocket Core back) + NINE SHIFT CORES IN THE WORKSHOP (D-0043) — UNPLAYED
His v3 note: "shifting is tougher than ever with such few warp shift locations"; his "begin with both" (he in AoE:
hidden low-priority batch runs only). v1-v3 were plain builds, so the dev keys (F2 Pocket Core, F8 size, F1 console)
were off outside the editor, and the Workshop had no core at all. Now: v4 is a development build (title "v4 dev",
`Builds/Windows/v4/ShiftJam.exe`, 1.8 min, smoke check clean: both scenes 0 errors) and the Workshop has nine cyan
Toggle cores spread over its floor (positions in D-0043; stills in %TEMP%\sj_d0043_stills). Course cores unchanged.
HIS PLAYTEST (v4): F2 calls the Pocket Core anywhere (course and Workshop), F8 size, F1 console; in the Workshop walk
to any cyan core and E: grow / shrink (one is a few steps from the start, one per corner and side). Say where in the
course shifting felt worst: those spots get a core next (or a Pocket Core charge, his call).
Not committed: his Editor Rich Presence plugin (Assets/ERP, .erp) and the editor's touches to two glow materials, three
font assets and UnityConnectSettings (from an earlier editor session, his to keep or drop).

## 8 Nov 2026 08:3x — v3: CARRIED IN THE ARMS (D-0042): props at the chest with both hands on them, tools at their grips, reach in and lower down — UNPLAYED
His "proceed with all" on the plan (carry in the arms). Proven headless with a temporary probe and stills (deleted): the
flashlight in a closed fist at its handle, the ruler flat across the waist on both palms, a pillow hugged at the chest
with both hands at its sides. v3: `Builds/Windows/v3/ShiftJam.exe`, smoke check clean.
HIS PLAYTEST (v3): as the kid grab a pillow, the bear, a box (E): it comes into your arms; walk; the footprint ahead shows
where E lowers it; wheel moves that spot; click throws. Pick up the flashlight (fist on the handle) and the ruler (both
hands, flat). Shrink while holding: it drops ahead (D-0041). Tune on the Player: ThirdPersonGrab carryHeight 0.8,
carryGap 0.1, handsBack 0.3, setDownGap 0.35, reachTime/lowerTime 0.25; on the body's CarryPose (added at run time):
turnWeight, elbowOut, elbowDown, elbowWeight, palmThickness; the pose in Animation/CarryPose.anim (rerunning Build Carry
Pose rewrites it). Known: props wider than the shoulders read as hugged; hands stay open on a big prop.

## 8 Nov 2026 06:2x — v2: SHRINKING WITH SOMETHING IN HAND FIXED (D-0041) + A FALSE CHECKPOINT RESET ON EVERY SHRINK FIXED — UNPLAYED
His v1 report: stuck after shrinking while holding the flashlight (and other items); grab and hold poses need work
(PLAN coming separately, nothing built); "sounds are good". Proven with a headless probe before and after (deleted):
the toy no longer keeps a kid-size tool (it is set down 19 units ahead as the shrink starts), nothing set down can box
the player in (KeepApart), and the shrink no longer warps the player to the checkpoint (it did every time the shrink lost
the ground for a frame). v2: `Builds/Windows/v2/ShiftJam.exe`, smoke check clean.
HIS PLAYTEST (v2): as the kid pick up the flashlight (wardrobe) or the ruler (playroom), shrink at a core: the tool drops
ahead of you and you stay where you were; walk around it; grab a pillow close to your chest as the kid, set it down
(E) and walk out of it; shrink and grow a few times on the floor and on furniture: no jump back to a checkpoint.

## 8 Nov 2026 05:5x — v1 WINDOWS BUILD (smoke check clean) + COURSE SOUNDS LANDED (D-0040) + portal thumbnails — UNPLAYED
His 7 Nov "begin the shift jam pass" finished with Unity closed (hidden low-priority batch runs only). Commits `1676887`
(code), `63a10d6` (sounds landed), this one (build + docs). SOUNDS: CourseSounds.asset with 11 first picks from the
Playroom pack (list in D-0040), a Course Audio object in Greybox and the Workshop; music left empty for him. BUILD v1:
`Builds/Windows/v1/ShiftJam.exe` (402 MB, first build 5.7 min, 0 errors), the title shows "v1" bottom right. SMOKE:
the built game opened both scenes for 6 s each with 0 errors (only notes: 12 props too detailed for a full convex
collider, a simplified hull is used). Portal thumbnails drawn (catalogue 543 entries, 543 pictures).
HIS PLAYTEST: run `Builds\Windows\v1\ShiftJam.exe` (the real build, not the editor): title (v1 in the corner), New
Game, the course end to end with sound: a shift (shimmer), a core waking, the switch click, a door, a checkpoint chime,
a fall reset (thump), the goal (win jingle), jump squeak, hard-landing tock, the room tone. Say which sounds to swap
(Shift Jam > Audio > the CourseSounds asset in Data/, or tell me). Then the Workshop from the title. Music: his.
NOT YET ON GITHUB: 74 commits since 2 Nov (`git push` is his: `! git.exe -C "%USERPROFILE%\Dev\Unity Projects\Shift Jam Project" push`).

## 6 Nov 2026 23:3x — BUILT + LANDED headless (D-0039): pillow side bounce, car portals, run cadence B — UNPLAYED
His "start building" on the three open picks (my recommendations). Unity closed: code compiled outside (0 errors),
committed `7dfde1c`, then a batch editor ran `CourseBuilder.LandPortalsAndPillows`: built CarPortal_Red/Blue/Green
(Traversal), catalogue 543 entries (the three portals have no thumbnail yet: run Shift Jam > Workshop > Build Catalog
once in the editor), side bounce on every pillow pad (Greybox: both couch pillows + the wardrobe trampoline pillow;
Workshop pillow; PillowPad piece), red pair placed in the Workshop at (-220,-190) facing +X and (220,220) facing -Z,
both scenes saved. Stills checked (%TEMP%\sj_d0039_stills): hoops stand with the rim sunk, red swirl inside, pillow
unchanged. New batch-safe `BuildCatalogBuilder.BuildNoWait` (previews never finish in a quitting batch).
HIS PLAYTEST: as the toy, RUN into the Workshop pillow's side (you should pop onto its top, then the top pad throws you
high); standing beside it does nothing; jumping on any pad still bounces on landing. Drive any car through a red hoop:
out of the other corner's hoop at the same speed, camera behind you; try it from the back side too. Feel the new run
(sprint 8, slower Boost legs). Open calls: where portals go in the course; hoop tint per colour (now pack purple, colour
is in the swirl and floor glow); toy on foot through car portals (off).

## 6 Nov 2026 12:5x — LANDED `cdcfe34` (D-0037 + D-0038): boosts, HUD, race, impact-only crashes, bump stop, ramp lead-ins, hearts, shields, mending — UNPLAYED
I opened Shift Jam for him (he closed NectarRush). The first launch stalled inside the startup code reload at zero CPU for
10+ min (a bridge GetState had arrived as the reload began; log kept at scratchpad Editor-hung.log): killed it, relaunched,
and stayed off the bridge until the main window showed: open in 23 s. Ran in order: UI > Make HUD Fonts; Build Combat &
Vehicle Pieces (15 Cyber cars with flames/smoke, ToyRacer01..10, CarCannon); Build Boost Pieces (9); Build Race Pieces
(11); Build Traversal Pieces (lead-ins in ClimbRamp/LandingRamp/KickerSmall); Build Catalog; with Greybox beside Workshop:
Wire Car Boost Parts (both wooden cars) + Add Ramp Lead-ins (Workshop Kicker; Greybox Climb, Kicker, Landing); both saved.
Placed in the Workshop (23 pieces, floor map checked, no clashes): race loop (finish (0,30) heading +X; gates 1-3 at
(130,90), (0,150), (-130,90); TV (0,-15); podium (50,5)), boost lane along x 190 from z -185 (life ring, two arrows, star,
arch, jump pad, star, tramp, ring, heart; fan pad (215,-110)), heart + star + life ring on the racing lines, the cannon at
(-200,-170) facing north, ToyRacer04 (wind-up key) and ToyRacer01 behind the start line. Stills: Temp\shiftjam-stage\stills4.
Known: the BoostArch and BoostRing openings (about 4 wide) fit the Cyber cars and racers, not the wooden car (4.2 wide).
HIS PLAYTEST (Workshop): ToyRacer04 through the finish arch starts 3 laps (HUD top right, the TV); Shift = nitro; stopped,
W then hold Space, let go = wind-up launch; the boost lane; stars, hearts, the life ring (8 s bubble); up the ClimbRamp
(no damage, smooth), into a wall (damage), health bar + bonnet smoke under 30%; the cannon (roll slowly into its mouth);
H for the new keys. Sounds were picked by name: his ear.

## 6 Nov 2026 12:1x — D-0038 BUILT + STAGED with D-0037; WAITING for him back in Shift Jam to land (he is playtesting NectarRush; Shift Jam's editor is closed) — CHECKPOINT
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
- v6 — 16 Nov 2026: a REHEARSAL release build (plain, no dev keys) of head `59c26f2` (D-0060): built headless in 4.1 min, 399 MB, 0 errors; smoke check of Greybox, Workshop and Raceway: 0 errors. Made to learn whether the game still builds and boots after the 14 Nov pack import overwrote ProjectSettings: it does. UNPLAYED. `Builds/Windows/v6` (local).
- v5 — 9 Nov 2026: D-0044 (pause menu + Options on pause and title; development build); smoke check clean; UNPLAYED. `Builds/Windows/v5` (local).
- v4 — 8 Nov 2026: D-0043 (DEVELOPMENT build: dev keys F1/F2/F8 on, title "v4 dev"; nine shift cores in the Workshop); smoke check clean; UNPLAYED. `Builds/Windows/v4` (local).
- v3 — 8 Nov 2026: D-0042 (carried in the arms, grips, reach and lower, the fist fixed); smoke check clean; UNPLAYED. `Builds/Windows/v3` (local).
- v2 — 8 Nov 2026: D-0041 (shrinking with a tool in hand, KeepApart, no false checkpoint reset on a shrink); smoke check clean; UNPLAYED. `Builds/Windows/v2` (local).
- v1 — 8 Nov 2026: first Windows build (Greybox course + Workshop), course sounds, version on the title; smoke check clean; UNPLAYED. `Builds/Windows/v1` (local, git-ignored).
