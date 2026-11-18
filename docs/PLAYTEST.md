# PLAYTEST — the course walkthrough (written 3 Nov for the first run of Rooms 3–6; nothing past Room 2 has been seen)

How to use: play it top to bottom once without fixing anything. Note the step number and one line per thing that
is wrong ("7: prompt says X", "12: fell through"). Expected prompts are in quotes; E = Interact, Shift = sprint,
Space = jump, X = put down (C crouches since D-0064), LMB = toggle the carried light.

## Before Play
- Open Greybox.unity. Hierarchy has Room/Course with R3_SkirtingRun, R4_Playroom, R5_Wardrobe, R6_Window.
- Game view Scale 1x. Pause button off. Console: only the Fast Script Reload welcome error is expected.

## R1 Toy box
1. You wake inside the box. Walk, hop (Space), sprint (Shift): the feet should drive the floor at a sprint.
2. The box core glows. E → "Shift: grow". The warp shows your swirl in colour, the box bursts.

## R2 Bedroom (kid)
3. Camera sits about 2.9 m back with a 70° view, not inside the walls. Look at the real door: E → "Locked".
4. Shelf on the west wall, switch above it: E → "Press". Across the room the toy door swings open and a light comes on.
5. Pick up the flashlight (E → "Pick up"): it sits in the right hand, head forward, LMB toggles. X puts it down.
6. Jump onto the bed, E on the core → "Shift: shrink". You are a toy on the mattress.
7. Hop box → books → floor (each drop under 14 units: no respawn). Walk to the open toy door at the south wall.

## R3 Skirting run (toy)
8. Walking into the door warps you to the lane start, facing east, lane lit by three warm lamps. Falling into the
   first pit = respawn at the start (the fall rule).
9. Pencil bridge: five pencils end to end, 1 wide. Walk it. Note if a pencil is misaligned or you fall through.
10. Hop gap (3.5): one standing jump. A checkpoint sits just after it (nothing shows; a fall now returns here).
11. Book stairs up a 12-high box and down the far side: every riser one hop. Note any riser you cannot make.
12. Three pins: walk between them. (Touching one = respawn, once the Hazard script is in; until then they are solid.)
13. Sprint gap (4.5): hold Shift, jump at the edge. Checkpoints before and after.
14. Glowing exit at the east end: walk in → you appear in the playroom's west side by the hoop.

## R4 Playroom (toy, then kid, then toy) — dressed 4 Nov (D-0019), our layout
15. Look around from the mouse hole by the hoop: desk and chair NW with a glow on the desk top (core B), a sandbag
    maze NE with a glow inside (core A), race track centre, castle south of it, sofa east, wardrobe SE with a safe in
    front and a glowing hole behind. The floor is cluttered on purpose; note anything that blocks a path you need.
16. Walk the maze to core A: E → "Shift: grow". (Sandbag walls are 2 high: a toy cannot hop them.)
17. Kid: the safe. E → "Pick up". Carry it a few steps and X to put it down. The hole behind it is now clear.
18. The ruler lies near the desk. E → "Pick up", carry, X to put it down on the desk edge. (Optional now: the way down
    exists without it; note where it lands anyway.)
19. Core B on the desk: E → "Shift: shrink". You are a toy on the desk (the desk top is a checkpoint).
20. Down: hop from the desk's south edge onto the two-box stack (24), then the single box (12), then the floor. Each
    drop is 12; none should respawn you. The stool beside the desk is a 14 drop: note if it tempts you.
21. Cross the floor to the hole under the wardrobe: walk in → inside the wardrobe.

## R5 Wardrobe (toy, then kid)
22. Dark wood box, dim lamp. Book stairs along the back wall up to shelf 1 (40), then back along to shelf 2 (80).
    Each shelf is a checkpoint; a fall below returns you to the last shelf.
23. On shelf 2: a flashlight lies pointing along the shelf at a dark core (an ember). Walk into the cap on its
    switch: it clicks, the beam comes on, after half a second the core wakes (glow + sparks).
24. E on core C → "Shift: grow". You stand up inside the wardrobe as the kid and are warped out in front of it in
    the playroom; the wardrobe doors swing open behind you.

## R6 Window
25. North wall: a window with a sill at 55 and a glow on it. One kid jump onto the sill. E on core D →
    "Shift: shrink". Walk the toy into the goal zone on the sill: GoalText shows the window line. Outside: grass and
    giant trees.

## What to report
- Any step where the prompt text differs, the warp lands you facing the wrong way, or a drop respawns you that
  should not (or does not and should).
- Camera: anywhere it sits inside a wall or a prop.
- Feel: the three jumps in R3 and the ruler ramp in R4 are the two things the whole course is tuned around.

## The carried shift (D-0045, 9 Nov)
- New Game: battery dark. Box core: battery lit + the hint once. F anywhere on open floor: shift, battery empties.
- F with none: "No charge"; F mid-jump: "Feet on the ground first"; F as the toy in the skirting lane or by a wall:
  "No room to grow" (battery stays lit); F in the wardrobe: "Can't shift in here". Any core refills; a fall reset refills.

## The rides (D-0047, 10 Nov)
- Bedroom, as the toy at the foot of the books: a pink car a few steps west, nose toward the toy door. E → "Drive".
  Stick drives, Space = handbrake, Shift = nitro, E gets out (it brakes first). The toy box is on the straight line.
  At the toy door: get out and walk in (the door does not take a car).
- Playroom: the floor rides are gone; the cars wait on the expressway (below).
- As the kid: "Too big to drive". Note if walking over a parked car knocks it away from its spot.

## The expressway (D-0048, 10 Nov)
- From the mouse hole: book steps a few strides ahead and to the right, seven hops up to an orange lane with a blue
  car. E → "Drive". The lane beside it is the loop.
- Clockwise (the way the car faces): north, round past the desk (a lay-by on the left with a gold car and book steps),
  along the north side, round the north-east bend. Just after that bend the left rail is flat for one piece: stop, E,
  step off the edge (a drop of 8, no respawn), walk to the maze.
- After the desk: down the three rulers as before, then the book steps are behind the last box to the north-east; up to
  the gold car. Clockwise to the south-east: a lay-by on the left with book steps down toward the wardrobe.
- The ramps: the blue car's lane runs down to the floor at both ends (north toward the desk, south to the corner).
  Drive down one and back up.
- Note: where a car leaves the road or hits something you cannot see (the guards are invisible, 2 high); the hop at the
  top of a ramp; whether a parked car has moved; the kid walking across the track; F on or under the track; anything
  the track hides that you need to see (the reveal camera, the switch camera turns).

## The mats (D-0049, 10 Nov)
- Drive a car off a ramp and straight over a group of mats: it should roll up the edge, cross the seams between mats
  without dropping a wheel, and roll off the far side. Try an edge at speed and a corner of the group.
- Walk the toy onto a mat: no hop needed now. Note anything that looks like floating at the mat's edge.

## The raceway (D-0050, 10 Nov)
- Open `Assets/_Game/Scenes/Raceway.unity` and press Play (no title card: it is a sandbox). From the course, the title
  menu has a Raceway button, and the console has `raceway`.
- You start on the floor south of the start straight. Up the book steps (nine hops), E on a car.
- One lap, anticlockwise: finish arch, boost pad, the loop (keep the throttle down), the banked hairpin, gate 1, the
  spiral up two turns, gate 2 on the high deck, the dive, boost pad, gate 3, the second hairpin, the line.
- Note: whether the car makes the loop and where it leaves it; scraping at the loop's bottom; the spiral at speed (do
  the walls hold you); the crest into the dive (how long you fly, how hard you land); gates you miss or clip; anything
  that stops you dead. F9 puts the cars back.

## The driving pass (D-0051, 10 Nov)
- Any Cyber car, best on the raceway. Hold forward from a stop: it should pull hard, then ease toward the top; the
  view widens and sits lower as you speed up; wind rises; faint speed lines near the top.
- Shift (nitro) or a boost pad: the view kicks wider, the camera shakes a little, lines turn stronger, the dial
  rattles and runs into its red stretch.
- Drift: at speed hold Space and steer. One pip lights by NITRO after about half a second, the second after a second
  and a half; let go of Space and the car bursts forward. Skid sound and marks while sliding.
- The dive: off the crest, steer in the air (stick tips and turns it; with Space held it rolls). Land flat with the
  throttle on for a small burst; a thump and a shake by how hard you land.
- The loop and the spiral: the car should stay on even if it hops. Note where it lets go.
- Pad: hum with speed, buzz on boost and in a slide, a jolt on landing and on a hit. It must stop when you get out
  and when you pause.
- Note anything too strong or too weak, and say which; every number is in one of three settings files.

## Ammo and reload (D-0070, 17 Nov)
- Colour Corner: fire the pistol until it clicks; it reloads by itself. Press R mid-magazine. Watch the count bottom left.
- 2 for the SMG: hold fire through a reload. Walk into the supply crate: every gun carried is filled.
- Workshop: take the shotgun, empty it, use the ammo box one step past the SMG (one magazine to each gun carried).
- Say: how the reload looks (the hand on the magazine), the HUD's size and place, Y as reload on a pad.

## Shot feel: the numbers (D-0071, 17 Nov)
- Colour Corner (T for darts) or Workshop. Pistol at a wall: 5 shots a second at the fastest click, each on the dot;
  the barrel flips a little (4 degrees; it was 10). Shotgun: a slower pump (1.2 a second), 8 darts in a tight
  bunch, a big flip (8.8). SMG (2): hold the trigger: 7 a second, almost no stray.
- Walk with the shotgun out, then X (put away) and walk again: a little faster without it. The same with the SMG.
- Two guns carried: roll the mouse wheel down and up. Then grab a prop: the wheel moves the prop, not the guns.
- Shoot a loose prop as a toy, then grow and shoot one as a kid: it should fly, not cross the room.
- Say: the SMG (a steady automatic now, it was a fast spray), each gun's kick, whether the slower walk is felt, the
  wheel's direction, the dart's drop at range.

## Shot feel: what a shot shows and sounds like (D-0071, 17 Nov)
- Workshop: shoot a practice bear or skittle with the pistol. Each hit: white ticks flash round the dot, the target
  squashes and springs back, pale blue bits fly off, the hit sounds. The dart that knocks it over: orange ticks,
  bigger and longer.
- Shoot a wall: bits and a sound, no ticks. Watch the barrel: a small pale puff on every shot.
- Shotgun into a wall close up: a cloud of bits, one landing sound.
- V (first person): ticks, puff and sounds the same. Colour Corner with paint loaded: ticks when paint hits the
  mischief soldier; the paint's own splat and sounds as before.
- Say: the ticks (size, how long they stay, the orange), the puff (seen at all? too much with the SMG?), the squash
  (0.15 on targets, 0.1 on soldiers), each sound and how loud it is.

## First person: arms on the gun, the sight to the eye (D-0074, 17 Nov)
- Workshop or Colour Corner, the pistol in hand, V. Your hand holds the grip, fingers closed. No head, no shadow.
- Hold right mouse: the sight comes up to the middle of the view in a blink (0.15 s) and the dot goes; let go: back
  to the hip. Fire at the hip and aimed: the hand rides the kick. R: the other hand comes to the magazine.
- 2 for the SMG, then the shotgun from its stand: both hands on the gun. Right mouse: the shotgun's scope in the
  middle; the SMG rolls over 45 degrees to its offset sight.
- Look straight down: arms, then your top, legs and shoes. Walk while looking down: the legs walk. C: crouch.
  Look straight up: the hand stays on the grip.
- X puts the gun away: the arms go (empty hands show no body). X again. V back to third person: the body is whole.
- Say: the arms and hands, each sight picture, the SMG's roll, the body seen looking down, whether the body should
  show with empty hands too.
