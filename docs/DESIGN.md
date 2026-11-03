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

## Room 2: the bedroom (beats re-ordered for a kid-sized arrival; design pending the box playing)
Problem before solver, every beat. Original beats, written for a toy-sized spawn:
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
