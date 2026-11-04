# PLAYTEST — the course walkthrough (written 3 Nov for the first run of Rooms 3–6; nothing past Room 2 has been seen)

How to use: play it top to bottom once without fixing anything. Note the step number and one line per thing that
is wrong ("7: prompt says X", "12: fell through"). Expected prompts are in quotes; E = Interact, Shift = sprint,
Space = jump, C = put down, LMB = toggle the carried light.

## Before Play
- Open Greybox.unity. Hierarchy has Room/Course with R3_SkirtingRun, R4_Playroom, R5_Wardrobe, R6_Window.
- Game view Scale 1x. Pause button off. Console: only the Fast Script Reload welcome error is expected.

## R1 Toy box
1. You wake inside the box. Walk, hop (Space), sprint (Shift): the feet should drive the floor at a sprint.
2. The box core glows. E → "Shift: grow". The warp shows your swirl in colour, the box bursts.

## R2 Bedroom (kid)
3. Camera sits about 2.9 m back with a 70° view, not inside the walls. Look at the real door: E → "Locked".
4. Shelf on the west wall, switch above it: E → "Press". Across the room the toy door swings open and a light comes on.
5. Pick up the flashlight (E → "Pick up"): it sits in the right hand, head forward, LMB toggles. C puts it down.
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

## R4 Playroom (toy, then kid, then toy)
15. Look around: desk and table NW with a glow on top (core B), a sandbag maze NE with a glow inside (core A),
    race track centre, castle south, sofa east, wardrobe SE with a safe in front of it and a glowing hole behind.
16. Walk the maze to core A: E → "Shift: grow". (Sandbag walls are 2 high: a toy cannot hop them.)
17. Kid: the safe. E → "Pick up". Carry it a few steps and C to put it down. The hole behind it is now clear.
18. The ruler lies near the table. E → "Pick up". Stand at the table's south edge facing the chair and the 28-box,
    C: the ruler should lie from the table edge down onto the box as a ramp. Note where it actually landed.
19. Core B on the table: E → "Shift: shrink". You are a toy on the table (a checkpoint covers the table top).
20. Down the ruler, onto the box (28), the chair (22), the small box (12), the floor. Each drop under 14.
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
