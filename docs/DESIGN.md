# Shift Jam — design

GitHub Game Off 2026, theme **Shift**. Unity 6000.3.10f1, URP. Working title pending.

## The one idea
The player is a toy figure in a kid's bedroom. A **shift** swaps them between **toy size** and **kid size**.
The same room is two levels: at toy size the floor is a landscape of books, train track and castle walls;
at kid size the bed, the shelves and the window are the play space. One room, built once, played twice.

Chosen 2 Nov over a day/night world shift (every space built twice) and a kid-to-car form shift
(two motors, Voltane's territory). Voltane has scale shift as a single gag; nobody owns it as a core.

## The tutorial room (first build)
Problem before solver, every beat.
1. Spawn toy-sized beside the bed. Learn move and jump on a stack of books. The exit, a toy door in the
   skirting board, is visible and shut.
2. The problem: the bed is a cliff. On the floor sits one glowing object on a pedestal, the shift source,
   seen from anywhere in the room.
3. Press E on it. The shift: a timescale punch, a camera pull and a shake (Feel), the figure grows, the
   world drops away. The bed is now a step.
4. At kid size, a switch on the shelf opens the toy door. The door is toy-sized. Shift back small on the
   same source, walk through. Both directions learned; the door becomes the first portal.

## Sizes
Toy = 1:8 of kid (an action figure, ~22 cm). A knob in ShiftSettings; judged by the book test in the editor
(a 1:8 figure beside a Playroom book: do the textures hold?).

## Kit
ithappy Playroom (room, furniture, toys), Funny Characters (the figure, modular; Full_Body costumes share
the rig), Feel for juice, Shapes for UI lines, TextMesh Pro. Battle Stadium and Cyber Racing Cars held for
later worlds.

## Parking lot
- World 2 and beyond: what the toy door leads to (Battle Stadium arena? the attic?).
- Costume shift (Full_Body swaps) as a cosmetic.
