# BACKLOG — to the Game Off deadline (written 3 Nov; Game Off 2026 closes 1 Dec, four weeks)

Order = priority. "His" = editor / feel / art / audio; "mine" = scripts, builder runs, docs. One playable loop
first, then juice, then the page. Nothing here is a deadline pressure, it is the order to pick from.


## ROADMAP AGREED 20 Nov 2026 (his "yes" to all; supersedes the order below where they differ)
His words: shift in mid-air "sounds cool. Even moreso if we can get Fortnite or TOTK style glide and parachute down";
the dart hookshot with zip lines to bespoke targets "sounds AMAZING. Yes. Full yes"; the paper-plane glider "Yes";
"Yes to the rest as well. If needed, we can expand this to a whole HOUSE and even a backyard" (the PS1 Toy Story and
Rugrats games as the touchstone). More packs and tools are still coming in (Modular 3D Text among them).

The jam entry (submit 30 Nov) is a snapshot of this, not the end of it. One builder at a time, in this order:
1. **One house:** the tunnel (old R3) becomes the tutorial hallway (movement, then the gun taught there with ammo and
   targets, then the shift charge formally: a core fills it, F at the tunnel's tall end); the bedroom pistol stand
   comes out; the playroom is the hub with doors to the Workshop, the Raceway and Colour Corner (and back); size,
   gun, ammo and charge carry between rooms. First check each lab's player scale against the course's.
2. **Shift in mid-air + glide:** shrink while falling to drift, grow at a jump's top for reach; a glide / parachute
   (the paper plane as the toy's glider) to come down from height, TotK / Fortnite style.
3. **Dart hookshot + zip lines:** a device that fires a foam dart with a line; on a bespoke target (anchor) the line
   becomes a zip line or a pull-up. Targets placed for verticality (wardrobe, shelves, desk, bed).
4. **Modular 3D Text** once imported: one information service (prompts, refusals, hints, toasts, end card) and
   in-world signs for the hallway lessons and the hub doors.
5. Then, in his order of interest: the toy train as the hub's ride (stops at the lab doors), ladders and ropes
   (Goblin's climbing reused), the toy tank (drive + foam balls), the claw machine (kid plays it / toy rides the claw),
   bouncy / sticky paint, the kid's gun as a toy's turret, D-0072 rifle / sniper / bubbles, a boss (truck or
   helicopter at kid scale).
6. After the jam: the whole house and a backyard.

## Week 1 (to 10 Nov): the loop works end to end
1. His first walk of the course with docs/PLAYTEST.md; my fixes to the builder from his notes (re-run, re-walk).
2. Mine: Hazard on the pins (done 3 Nov evening, lands on the next builder run), a visible checkpoint marker (a small
   glow quad that lights when claimed), a respawn fade (0.2 s black) so a fall reads as a reset, not a glitch.
3. Mine: the end: GoalText on the sill + a fade to a title card ("Shift", his art) + restart. Also a start card.
4. His: tune jump (1.6), sprint (10), fall limit (14) by feel on R3; tune the ruler drop so the ramp reads.
5. His: Feel on the shift (timescale dip, shake, the URP lens punch on a Global Volume from ShiftSource.On Used).

## Week 2 (to 17 Nov): it looks like a game
6. His art: a proper orb mote (in), the three mockup sparks replaced, a warp art per direction (in), a title card.
7. Mine: walls/ceilings for R3 and R5 from pack modules instead of cubes where the pack has a fit (R5 = Wardrobe
   interior wood; R3 = inside-the-wall plaster). The window bay as a pack Wall with a cut-out if one exists.
8. Audio: SFX from the Asset Library (Case Portman Cute&Cozy UI for E/press/switch; a soft thump for the fall
   respawn; a whoosh for the warp). Music his. Mine: an AudioSource per event through the UnityEvents already there.
9. Mine: Mixamo-free fix for the walk slide: match walk speed to the Walk clip (4) or keep 5.5 and accept it. His call.
10. His: lighting pass per room (bedroom warm, lane cold, wardrobe dark, playroom bright, garden sunlit).

## Week 3 (to 24 Nov): polish and a build
11. Windows builds (v1-v5 made; v1-v4 deleted 9 Nov, only v5 on disk). WebGL check dropped 8 Nov (his: Unity builds only).
12. DONE 9 Nov (D-0044, v5): Options (sensitivity, invert Y, volume, fullscreen) + Pause (Esc / Start).
13. His: a 30-second trailer cut from a Play recording; screenshots for the page (both sizes in one frame).
14. Both: a stranger plays it once (a friend, no hints). Fix the three worst stumbles only.

## Week 4 (to 1 Dec): ship
15. Page copy (itch + the Game Off repo README): what it is, controls, credits (ithappy packs by licence, Feel/Shapes
    as tools, fonts), AI disclosure line as Game Off and itch require. No "hand-built" claims anywhere.
16. Submission 24 h early. The repo public (it is), packs stay out of it (they are).

## Parked (after the jam)
- Additive scenes per room; dark-room navigation with the carried light; the "kid arranges" verb with more
  carryables (boxes); the racing-track shortcut in R4; costumes as wardrobe clothes; a second house floor.
