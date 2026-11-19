# The jam's level: brainstorm (19 Nov 2026, written while he was away; NOTHING HERE IS BUILT OR AGREED)

The level plan given on 19 Nov still stands and still waits on his "proceed": the course (R1 toy box → R6 window,
`docs/COURSE.md`) IS the jam's game; walk check of the six rooms on today's code first; one dart-and-soldiers beat
in the playroom; no new systems after 24 Nov; v7, his play, a stranger's play; submit 30 Nov. This file is the
ideas around that plan, for him to pick from. Each idea says what already exists for it, so the cost is honest.

## What the game has now that the course never used
The course was designed on 3 Nov with walking, jumping, E, carrying and the shift. Since then the project grew:
darts and foam weapons with hit shapes (D-0074…D-0080), grenades (D-0077), knock-down toy soldiers, paint
(ColourCorner), cars and the Raceway (D-0050), a build mode (`Scripts/Build/`), rivals in races. None of it is
in R1–R6. A jam judge plays the course; the Workshop and the Raceway are side rooms a judge may never open.

**The one question:** do the toys enter the course, or does the course stay pure (walk, shift, carry) with the
toys as an "after the credits" playground? My pick: **two toys enter the course, one room each, and everything
else stays in the side rooms, reachable from the title menu.** Reason: the theme is Shift; every toy in the course
should say something about size, or it is noise. Two beats are buildable by 24 Nov; five are not.

## Ideas per room (★ = my pick for the jam, ○ = after the jam or only if a day comes free)

### R1 toy box: leave it
It is the theme in thirty seconds and it is built. Only polish: the burst's sound and the first core's glow.

### R2 bedroom
- ★ **Two ways to the switch.** The switch on the wall is kid-only ("Too small to reach"). Add: a dart pistol lies
  on the bed. A toy can climb up, take it and shoot the switch from the floor. The kid presses it. Same result,
  two sizes, and the gun is introduced as a tool before it is a weapon. Exists: pistol, darts sticking, the switch's
  event. New: the switch takes a dart hit (Damageable or a hit event on its collider: small).
- ○ The real door's rattle answered by a footstep sound outside: the parent the kid hides from (mood only).

### R3 skirting run (toy only, dark)
- ★ **Paint shows the way.** The lane is dark. A paint blaster at the lane's mouth; a splat lights the ledge it lands
  on (the paint glows faintly). The player paints ahead before jumping. Exists: paint drops and splats
  (ColourCorner), the lane. New: an emissive tint on the splat material (a material change, not a system).
  Risk: if splats only land on some surfaces, the lane's pieces need the right layer. A short check settles it.
- ○ Rats or a wind-up mouse running the lane (a moving hazard). New AI: not for the jam.

### R4 playroom (the big room)
- ★ **The dart-and-soldiers beat (already in the plan).** Core A sits in the Toy Army's sandbag maze; four soldiers
  stand guard and fire at the toy when it walks into their sight cone (their existing guard-and-fire behaviour). Knock them down with
  darts from cover, then walk the maze. At kid size the soldiers are ankle-high and the kid just steps over the
  sandbags: the shift makes the fight trivial, which is the joke and the theme. Exists: ToySoldier, MischiefSoldier (guard a post, sight cone,
  fire, tip over and get up again), knock-downs, hit shapes on the drawn body (D-0080). New: soldiers placed in R4, a "guard" spot each.
- ★ **The race track ramp as the toy's shortcut** (COURSE.md's optional loop): a toy car parked at the track's foot;
  drive up the ramp (top 33) onto the table instead of the ruler. Exists: cars, StickyTrack, the RaceTrack prop.
  New: the car placed, the ramp's top meeting the table edge. Only if the walk check shows the car handles at toy
  scale in that room; otherwise cut.
- ○ The kid uses build mode to lay the ruler ramp (instead of carrying it). Build mode exists, but teaching it inside
  the course costs a tutorial beat the jam cannot afford.

### R5 wardrobe climb
- ★ **Leave it as designed** (dark climb, flashlight wakes core C, the kid bursts the doors). It already has its idea.
- ○ Darts as climbing pegs: darts stuck in the wardrobe's wood hold the toy's weight for their 8 seconds. Very
  "shift": a kid's toy becomes a toy's ladder. New system (standing on a dart): after the jam.

### R6 window
- ★ **The payoff plus an invitation.** Toy on the sill, camera pulls back, title card, then two buttons: "Play again"
  and "The playroom is open" (into the Workshop, Raceway and ColourCorner with every toy unlocked). That turns the
  side rooms into the reward instead of hiding them. Exists: the title menu's Workshop and Raceway buttons (ColourCorner has no title button: check). New: the end
  card's second button.

## His notes the course must answer
- 10 Nov: "lacks juice, feels a bit slow, the course is very predictable and basic", "verticality as much as
  possible". The course's answer: R4 and R5 are both vertical (table, wardrobe shelves); the juice pieces are
  the shift's punch (built), the knock-down burst (D-0079), the respawn fade (backlog item 2: check it landed).
- 18 Nov: "the missing foam darts and lack of juice when landing darts" → D-0079 + D-0080, unplayed.

## A schedule that fits (to his calls)
| Day | Work |
|---|---|
| 20 Nov | Walk check of R1–R6 on today's code (hidden run, a still per room, a list of what broke). His play of D-0078/D-0080. |
| 21 Nov | Fix what the walk check found. R4 soldiers beat. |
| 22 Nov | R2 dart-the-switch, R6 end card's second button. |
| 23 Nov | R3 paint glow (or cut), the race ramp (or cut). |
| 24 Nov | **No new systems after today.** v7 build. His full play. |
| 25–28 Nov | His notes only. A stranger's play (backlog item 14), fix the three worst stumbles. Trailer + page. |
| 29 Nov | v8, final check, page copy (AI disclosure line, credits). |
| 30 Nov | Submit (a day early; closes 1 Dec 13:37 PST). |

## For his call
1. The pick above: two toys in the course (darts in R2 + R4), paint and the car only if cheap, the rest after the
   credits. Or: the course stays pure and the toys live only in the side rooms.
2. The end card's "The playroom is open" button: yes / no.
3. R3's paint glow: worth a day, or cut straight away?
4. The soldiers' shots on the toy in R4: a knock only, or a respawn on a hit (harder, needs a checkpoint at the maze)?
