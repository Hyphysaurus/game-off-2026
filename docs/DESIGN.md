# Shift Jam — design

GitHub Game Off 2026, theme **Shift**. Unity 6000.3.10f1, URP. Working title pending.

## The one idea
The player is a toy figure in a kid's bedroom. A **shift** swaps them between **toy size** and **kid size**.
The same room is two levels: at toy size the floor is a landscape of books, train track and castle walls;
at kid size the bed, the shelves and the window are the play space. One room, built once, played twice.

Chosen 2 Nov over a day/night world shift (every space built twice) and a kid-to-car form shift
(two motors, Voltane's territory). Voltane has scale shift as a single gag; nobody owns it as a core.

## Room 1: the toy box (the awakening; 2 Nov, replaces the bedroom as the first room)
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

## Parking lot
- World 2 and beyond: what the toy door leads to (Battle Stadium arena? the attic?).
- Costume shift (Full_Body swaps) as a cosmetic.
