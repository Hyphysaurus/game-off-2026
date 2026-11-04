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

## R4 Playroom (toy, then kid, then toy) — the pack's showroom since 4 Nov (D-0018)
15. You arrive by a mouse hole in the east wall, south of the real door, by the sofa's end. Look around: the desk NE by
    the windows with a glow on its front corner (core B), a small square sandbag maze in the open floor right of centre
    with a glow inside (core A), the race track and castle west of it, the bed NW, the wardrobe SW with a safe in
    front of it. Note anything you clip through or that blocks the lane along the south wall.
16. Walk to the maze (sandbag walls 2 high: a toy cannot hop them), find the gap on its south side, then the inner gap
    on the north side, E on core A → "Shift: grow".
17. Kid: cross to the wardrobe SW. The safe: E → "Pick up". Carry it a few steps and C to put it down. The glowing
    hole at the wardrobe's foot is now clear.
18. The ruler lies on the floor west of the desk. E → "Pick up", carry it to the desk, C to put it down. (Optional: the
    way down exists without it; note where it lands anyway.)
19. Core B on the desk's front corner: E → "Shift: shrink". You are a toy on the desk (the desk top is a checkpoint).
20. Down: hop from the desk edge onto the two-box stack beside it (24), then the single box (12), then the floor. Each
    drop is 12; none should respawn you. Note if the stool beside the desk tempts a 14 drop.
21. Cross the floor SW to the hole behind where the safe was: walk in → inside the wardrobe.

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
