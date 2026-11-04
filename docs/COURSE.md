# COURSE — the route through the house (designed 3 Nov 2026, for his review; nothing past Room 2 is built)

One idea per room, the problem before its solver, both sizes used in every room after the box, every
interactable visible before you need it. Numbers are pack units (1 u ≈ 2 cm). Toy = 1, kid = 35.

## The numbers every room is designed against
| | toy (scale 1) | kid (scale 35) |
|---|---|---|
| height | 1.85 | 65 |
| jump (apex) | 1.6 | 56 |
| step-up without a jump | 0.3 | 10.5 |
| walk / sprint | 5.5 / 10 u/s | 190 / 350 u/s |
| reach for E (ahead / up) | 1.6 / 1.8 | 56 / 60 |
| carries | nothing | Carryable things (flashlight, and from Room 4: rulers, boxes) |

Proposed rule (not built, D-0016 when he says yes): **a toy that falls more than 12 u (six body heights)
respawns at its last checkpoint.** The kid never takes fall damage. Without this rule the toy just drops off
every cliff and the "kid builds the way down" verb has no bite. Checkpoint = a trigger that calls
`Respawn.SetPoint`; one small script.

## Overview (≈ 12–15 minutes)
```
 R1 Toy box ─► R2 Bedroom ─► R3 Skirting run ─► R4 Playroom ─► R5 Wardrobe climb ─► R6 Window
   wake, grow    both sizes,    toy only,          both sizes,     toy climbs in     one last shift,
   (built)       toy door       jumps + sprint     kid builds the   the dark, kid     the way out
                 (built)        jump, checkpoints  way, toy walks   bursts out
                                                   it
 teaches: move/jump/E   two cores, reach   precision    carry + place   height + light   payoff
```
Room 3 and 5 are toy-only lanes (fast to build, pure platforming); 4 is the big puzzle room; 6 is a vista.

---
## R3 — The skirting run (toy only, 2 min)
**The idea:** behind the toy door is the gap inside the wall: a long, low lane of lost things. Teaches the
precise jump and the sprint-jump, and introduces checkpoints, before the big room asks for them.

**Shell:** a corridor 300 long × 20 wide × 14 high, floor = Floor_01 tiles scaled, walls = Wall_01 at 0.25 scale
or plain cubes, no ceiling light: the only light is the glow from the far end plus the player's ember
(reuse the Ember point light idea on the Player) and one warm lamp half-way (Night_Lamp_02, 6×8×6, as a
giant lantern). Entry = the toy door's Goal trigger becomes a door-to-scene (or the lane sits behind the
bedroom's south wall in the same scene at z −135…−155, x −15…285, a straight run to the east).

```
 z→  (lane runs along +x; player enters at the west end)
 ┌──────────────────────────────────────────────────────────────────────────────────────────┐
 │ start  [1]pencils bridge   [2]gap 3.5   [3]matchbox stairs   [4]pins   [5]gap 4.5 sprint  exit │
 │ ■────── ═══ ═══ ═══ ───▒▒▒───  ▂▃▅▇ ────  ▲ ▲ ▲ ──── ▒▒▒▒▒ ───►  ○ mouse hole │
 │   x0      40..70       100   130..150     180..200    230..240   280   │
 └──────────────────────────────────────────────────────────────────────────────────────────┘
 ▒ = a drop into darkness (12+ u, respawn) · ═ = Pencil_01 laid as beams (7 long, 1 wide) · ▂▃▅▇ = Matchbox_01 /
 Paper_Block_01 (3 high) steps · ▲ = Pin_01 standing (touch = respawn) · ○ = exit hole, lit
```
1. **Pencil bridge (x 40–70):** three Pencil_01 laid end to end over a drop, 1 u wide. Walk, do not sprint.
2. **Gap 3.5 (x 100):** one standing jump (toy jump covers ~4 u at walk). Checkpoint after.
3. **Book stairs (x 130–150):** Book_01 steps (2.0 each, sunk 0.4 so each riser is a 1.6 hop) up to the top of a
   Cardboard_Box_06 (12 high), then down the far side. Paper_Block_01 is 3 high, too tall for one hop, so it is
   dressing only. Teaches "one book = one hop" (D-0008).
4. **Pins (x 180–200):** three Pin_01 standing in the lane, 2 u apart, walk between them. A touch = respawn.
5. **Sprint gap 4.5 (x 230–240):** the first gap that needs Shift held. Checkpoint before it.
6. **Exit:** a lit round hole (DoorGlow quad + Goal → next room).
**Props:** Pencil_01 ×3, Book_01 ×4, Notebook_01 ×2, Cardboard_Box_06, Pin_01 ×3, Night_Lamp_02, Chalk_01 (dressing),
CD_01 leaning (dressing), Clip_01 ×n (dressing). **Scripts:** Checkpoint.cs (new, 20 lines), fall respawn rule.
**Build:** half a day by bridge.

---
## R4 — The playroom (both sizes, 5 min, the big room) — BUILT 4 Nov as the pack's showroom (D-0018); the beats below hold, the shell and dressing are the pack's own 4×5 room
**The idea:** the pack's showroom, used as a puzzle: the kid arranges, the toy traverses. Three cores.

**Shell:** 6×5 floor tiles (390 × 325), walls at ±195 / ±162.5, ceiling 130, RoomLight + the window.
Zones placed like the pack's map (so the pack's dressing just drops in): Workplace desk NW, Hoop W,
Racing Track centre, Toy Army NE, Castle SE, Couch E, Wardrobe SE corner (the exit), Safe near the bed
side. Toy arrives through a mouse hole in the **south wall at x −150** (near the Hoop).

```
                 north wall (window)
   ┌───────────────────────────────────────────────────────────┐
   │ [Workplace desk]  Table_01 86×51, 36 high            [Challenge blocks]   │
   │   Computer, Keyboard, Globe       ● core B (ShrinkOnly, ON the table)      │
   │   Chair_01 (22 high) beside it          [Toy Army]  sandbag maze           │
   │                                          ● core A (GrowOnly, inside maze)  │
   │ [Hoop]           [Racing Track 32×84, ramp top 33]                [Couch]  │
   │                                                        Sofa_01            │
   │   ○ mouse hole IN (toy)   [Castle]            Safe_01 24×13×14             │
   │                                              pushed against ▣ hole OUT     │
   │   Bed (dressing)                                   [Wardrobe 65×113] ▣     │
   └───────────────────────────────────────────────────────────┘
                 south wall (real door, locked)
```
**Beats, in order (problem before solver):**
1. **Toy arrives** by the Hoop. Across the room, the exit hole under the Wardrobe glows, but the Safe sits in
   front of it. A toy cannot move a safe. (Problem: you need to be big.)
2. **Core A** glows inside the Toy Army's sandbag maze (Army_Sandbags 9×2×1 walls, 2 high = toy cannot hop them,
   so it IS a maze; Army_WatchTower 9 high as the landmark). Walk the maze, E: grow.
3. **Kid**: carry the Safe away from the hole (Carryable, kid only, "Heavy" prompt). Now the hole is open and
   you are too big. (Problem: you need to be small again, and the only shrink core is on the table.)
4. **Core B** on the Table (36 high; the kid reaches it standing: reach up 60). Before using it the room tells
   you the table is a cliff for a toy: **Ruler_Big_01** (16 long) leans on the desk as the obvious plank. The kid
   carries it and puts it down on the table edge pointing at the Chair_01 (22 high) → a ramp 36→22 (14 drop over
   16 run = 41°: the toy slides/walks down; CharacterController slope limit 45 ✓). From the chair to the floor:
   a Cardboard_Box_06 (12 high) the kid also drops beside the chair, or the chair's rungs (Chair_01 18×22, has
   a cross-bar at ~10?). Then E on core B: shrink on the table.
5. **Toy on the table** (cliff, 36 u = respawn height): down the ruler, chair, box, floor; across the floor past
   the Castle (toy-scale landmark) to the open hole under the Wardrobe. Goal → R5.
**Optional loop (if time):** Racing Track ramp (33) as a toy shortcut to the table from the floor, so a player who
sees it can skip the ruler. **Fail states:** fall from the table without the ruler = respawn at core B (checkpoint).
**Props (all in the pack):** Table_01, Chair_01, Computer_01, Keyboard_01, Globe_01, Ruler_Big_01, Cardboard_Box_06,
Safe_01, Wardrobe_01, Sofa_01, Basketball_Hoop_01, RaceTrack_01, Castle_01 + Castle_Wall_01 ×4 + Castle_Tower_01 ×2,
Army_Sandbags_01..06 ×8, Army_WatchTower_01, Army_Tent_01 ×2, Army_Soldier ×4, Mat_01/02 (puzzle mats as floor
colour), Cube_Alphabet ×6, Drawing_xx on the walls, Poster_xx.
**Scripts:** none new beyond Checkpoint; Carryable on the Safe (gripOffset so it hangs in front), the Ruler and
the box; `Carrier.PutDown` already rests things on what is under the hand (table top at 36 ✓).
**Build:** 1.5 days (dress by bridge, then his feel pass).

---
## R5 — The wardrobe climb (toy, then kid; 3 min)
**The idea:** inside the wardrobe it is dark and tall. Height is the enemy, the flashlight is the friend: this is
where the parked flashlight puzzle (D-0013) earns its place, as the second half of the room, not the first.

**Shell:** the inside of Wardrobe_01 at ×3 (195 wide × 340 high × 120 deep): three shelves at 40 / 110 / 190, a
rail with hanging costumes (Full_Body costumes from Funny Characters as the "clothes"!), the top shelf has a
core. Entry: the mouse hole at the bottom-left. Dark: ambient 0.1, one ember on the Player, a dim Night_Lamp
on shelf 2.
```
   ┌────────────────────────────────────────┐ 340
   │ shelf 3 ═══════════ ● core C (GrowOnly) │ 190
   │       ▇ box 28  ▅ box 18   ▂ books      │
   │ shelf 2 ══════  lamp ══════════════     │ 110
   │   hanging costumes ░░░░ (swing? no: static, a wall)  │
   │ shelf 1 ═══ ▂▃ books ═══  ▇ Cardboard_Box_04 28      │ 40
   │ ○ in   ▂ Book stack   ▅ Paper_Block ▇ box 18         │ 0
   └────────────────────────────────────────┘
```
1. **Toy climbs** shelf to shelf. Every riser is at most one book (2.0, with the books sunk 0.4 so the step is
   1.6): a six-book stair onto Cardboard_Box_06 (12), box 18, box 28, shelf 1 (40); the same pattern again to
   shelf 2 and 3. Each shelf = a checkpoint. Falls past 12 u respawn at the shelf below.
2. **Core C** on shelf 3 is dormant (dark). The flashlight lies on shelf 2 (the kid's lost torch). The toy cannot
   carry it, but can push its switch (PushSwitch, staged in the scratchpad) → the beam points up the wardrobe
   and wakes core C. (Light as the key, from D-0013, now with a reason: it is dark.)
3. **Toy reaches core C**, E: grow. The kid stands up INSIDE the wardrobe: the doors burst open (BoxBurst again,
   with the two Wardrobe_01_Door prefabs as panels: the same script, new panels) into the Playroom, now seen
   from the wardrobe. The window is up on the north wall.
**Props:** Wardrobe_01 (scaled, as the room), Wardrobe_01_DoorLeft/Right, Full_Body costumes ×4 hanging (static),
Cardboard_Box_04/06, Book ×12, Paper_Block ×2, Night_Lamp_01, Flashlight (second instance), Pillow_01 ×2.
**Scripts:** PushSwitch (already written, never landed), ShiftSource dormant (exists), LightSensor (exists).
**Build:** 1 day.

---
## R6 — The window (1 min, the payoff)
Kid walks to the window on the north wall (sill at 55 = one kid jump), a last core on the sill (Toggle) and a
Goal on the sill: shift to toy ON the sill, the camera pulls back through the glass, and the game ends on the
toy looking out at the garden (skybox + a Toy_Tree forest outside at 1:1 = a "giant" garden tease). Title card.
**Props:** Window_01 (opened: rotate the sash child if it has one; else a second frame), Toy_Tree_01 ×20 outside,
Curtain_01. **Build:** half a day.

---
## Build order and the reasons
1. **Fall rule + Checkpoint** (half a day): makes every other room's height matter. Test in R2 (the bed).
2. **R4 greybox** (the puzzle room): proves carry-and-place as a level verb with what exists (Carrier). If the
   ruler ramp feels bad, the whole course changes, so it goes first.
3. **R3** (fast, pure platforming): tune the toy's jump distances here, then R4 inherits them.
4. **R5** (uses the parked flashlight pieces).
5. **R6** last; it is a vista.
Rooms as separate scenes loaded additively (Core + room), so each room keeps its own lighting and the bedroom
does not pay for the playroom. The toy door / mouse holes become `Goal → load next`.

## Open for his call
- The fall rule (12 u). Without it, cut the ruler ramp and make R4 about the Safe only.
- R3 and R5 as toy-only lanes vs. both-size rooms (toy-only is cheaper and sharper).
- Costumes as wardrobe clothes (free, the rig's Full_Body swaps) vs. Curtain_01 drapes.
