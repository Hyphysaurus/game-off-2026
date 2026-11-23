# Armory builds: is it feasible? (checked, 23 Nov 2026, 05:5x)

> Status since this was written (23 Nov, 06:0x): D-0165 (the left hand) and D-0166 (the sight look) are both IN the project now, compiled outside, not played. So option S no longer waits on a landing; it still wants your word. Where the text below says they are "staged outside", read "landed".

**Yes, with limits.** The pack is a real parts kit and our three Dart guns are already built from it. A looks bench (skins and charms, changed live at the armory) is two to three sessions and can fit before the jam closes. Parts that change how a gun aims, holds or fires are feasible too, but they belong after the jam.

A "session" is one usage window of about five hours with one builder. A second pass checked this report against the files; what it changed is at the end.

## Why it is feasible

- **The Dart guns are the pack's own ready-made builds.** Each Dart prefab holds the pack's preset whole, mount points included. A mount point is an empty child object named `Place_<Gun>_<Slot>`. All three guns have an empty charm mount today.
- **Fitting a part is simple.** The pack's tool puts a small mesh prefab under the mount point, nothing more. Parts carry no scripts, and they grow and shrink with the gun (follows from the parenting; not seen in play).
- **A skin is one material for the whole gun.** There are 10. Today the pistol is "Ash", the SMG "Sunset", the shotgun "Sandstorm". A skin never touches a sight's lens or a charm.
- **Our three guns can take:** pistol 13 parts over 4 slots, SMG 22 over 5, shotgun 17 over 4. Each also takes 24 charms and 10 skins.
- **Each gun is its own object.** What is fitted stays on it through holster, drop, pick-up and the house doors, with no new code.

## What stands in the way

1. **The pack's customiser is an editor window.** Its code and its lists do not exist in a built game. We need our own parts list. An editor menu writes it once from the pack's lists (a "bake"), so nothing is typed by hand.
2. **The game measures a gun once and remembers:** the sight point, the stock length, the support-hand spot, the magazine. There is no "measure again". Swap a sight today and the gun aims through the old one.
3. **Some "per gun" numbers are really "per part".** The SMG rolls 45 degrees on aim only because its preset carries the slanted sight. The muzzle point includes the suppressor's length, typed in per gun.
4. **Two effects borrow a gun's materials and put the old ones back:** the fade when the camera is pushed inside the body, and the bend during a shift. A skin changed in that moment would be undone. The skin code has to go through them. Small, but it must be done.
5. **Nothing about weapons is saved,** not even which guns you carry. Online, other players see a fresh copy of the default gun.
6. **There is no bench screen, and no good view of the gun.** The Garage menu is the pattern for the rows ("name < value >", mouse and pad). The pieces to hold a player on foot exist. Missing: the pad trigger and the R, G and slot keys still act, and the third-person camera only stops behind the shoulder, where a pistol is mostly hidden.
7. **The pack's icons are heavy:** about 1 MB each in a build (estimate). We may not shrink or copy them.

Worth knowing: the Dart Pistol already shows a flashlight under its barrel that does nothing (a mesh with no light). By the pack's rules only the pistol takes that flashlight and only the SMG takes a foregrip.

## Three options

| | **S: Looks bench** | **M: Working parts** | **L: Full builds** |
| --- | --- | --- | --- |
| Player gets | A workbench in the armory: skin (10) and charm (24) for the gun in hand, changed live. Every gun of that type you take later comes in your look. | S, plus sights, muzzle parts, stocks and magazines that swap and still aim, fire and reload right. Then a pistol light that works and a foregrip the hand holds. | M, plus parts that change handling, builds saved per save slot and shown to other players, more of the 54 guns, laser, launcher. |
| Built | Parts list + bake menu. A small "build" part on the gun. Bench prefab + screen. Saved look per gun type. | "Measure again" on the gun. Numbers moved from gun to part. The pack's slot rules. Dart prefabs rebuilt. Light and hand code. | A stats layer, a new save record, one synced value online, thumbnails, a second fire mode, hold tuning per gun. |
| Effort | **2 to 3 sessions** + a short fix pass after your first play | **4 more sessions** + your look at about 45 gun-and-part pairs | **6 to 10 sessions**, then about half a session per added gun |
| Main risk | Low. Changes no aim, hold or reload code. Edits the weapon holder in two small places. | Medium to high. Same files as the hand fix (D-0165) and the sight look (D-0166), neither landed nor played. | High and open-ended: balance, two-PC tests, hands clipping on each new gun. |
| Before the jam? | **Yes**, if started by 27 Nov | **No** | **No** |

**S, the looks bench.** Skins and charms are the only choices that move no hand, no sight line, no muzzle and no magazine. That is why the line is drawn there.
- Session 1: the parts list and its bake menu. The build part, added to a gun when it is taken (the way the game already adds its "loose or held" part), so the Dart prefabs and the builder are not touched. Skin and charm by the pack's rules. The saved look per gun type, applied where every gun comes to the local player's hand. Written and compiled outside Unity; it ends with one menu click in Unity to bake.
- Session 2: the bench prefab and screen; the player held and the gun keys off while it is open; the key sheet; its ARCHITECTURE line; fixes from your first play.
- The possible third session is the view of the gun (your call 5).
- Screen: text rows, the gun itself changing in front of you. No pack icons, so no size cost.
- Left out: other players see the default look; players sharing one PC share one saved look; the hotbar picture and the stand's display gun do not change; charms hang stiff.
- To check on first play: a charm must not reach further back than the stock, because the stock measure counts every mesh on the gun.
- Depends on: the hand fix (D-0165) being landed first. It is staged outside as a whole new copy of the weapon holder file, the same file S edits. Whichever lands second would overwrite the other.

**M, working parts.** Depends on S, and on D-0165 and D-0166 being landed and played.
- After a swap the gun must measure again, the skin must go back on (each part arrives in its own colours), and the sight look must be re-applied.
- The SMG's 45 degree roll moves from the gun to the slanted sight. The builder remakes the Dart prefabs.
- Gun light (pistol only): the light check reads only the hand flashlight today. Foregrip (SMG only): no "fist round a handle" pose yet.

**L, full builds.** Every weapon number is private today, so stats need a new layer and a fairness choice for the Stadium. A build per save slot means saving the carried guns, which the game does not do at all. Online needs a two-PC test. Each new gun needs its hand numbers set. Seven of the 54 take only charms and skins.

## Recommendation

**Build S. Start it once the hand fix (D-0165) is in the project, better once you have played it, and no later than 27 Nov. If it has not started by then, it waits until after the jam.**

It answers what you saw (the armory is only a pick-up spot). It is small. It changes no aiming or hand code. Its parts list and bench are the base M and L need.

**Not before the jam closes:** working parts (same code as two fixes nobody has played, Dart prefabs rebuilt, about 45 pairs for your eye); stat changes (Stadium balance is its own design job); online sync of builds (needs a second PC); new guns (hold tuning each); pack icons in the build.

S costs two to three sessions the agreed jam roadmap does not have in it (mid-air shift and glide, the hookshot, HUD steps 2 and 3, co-op, level beats). If those run long, S is the one to drop.

## Your calls

1. **S before the jam, or park it all?** Suggested: S, with the 27 Nov cut-off.
2. **Does a look belong to the gun type or to one gun object?** Suggested: the type, saved the way your body choice is.
3. **Is the bench for the toy, the kid, or both?** Suggested: the toy. The armory is laid out at toy size.
4. **Text rows or picture cards?** Suggested: rows now; pictures later from our own thumbnail tool.
5. **How do you see the gun at the bench?** (a) Third person as it is: free, but a pistol is mostly hidden behind the body. (b) First person with the gun raised: the best view for the least work, but first person takes the mouse back on a click and Esc leaves it, so it needs its own "menu is up" hold. (c) A camera that frames the gun: later. Suggested: (b), and count the third session as likely.
6. **One skin per gun, or mixed per part?** Suggested: one per gun, the pack's rule.
7. **Who places the bench?** Suggested: you drop the prefab in by hand in the playroom, next to the armory and not inside its node. The house build wipes and remakes that node.
8. **Should parts ever change stats?** Suggested: not for the jam. Later, small changes, and equal guns in the Stadium.

## Rules the design keeps

- Nothing under `Assets/_Imports` is edited. The pack's parts, mount names, slot rules and skins come before anything of ours.
- Mounts are matched the pack's way, by "Place_" and the slot's ending. The shotgun's are misspelt ("Place_Shothun1_...").
- The bench, the build part and the parts list are drop-in pieces for any scene, each with a line in `docs/ARCHITECTURE.md`. The bench is a placed prefab, not builder coordinates. Numbers live in data.
- The repo is public and the pack is git-ignored. The baked list only points at pack files by their ids. No pack mesh, texture or icon is copied into a tracked folder.
- Your side: placing the bench, look and feel checks, art, audio. Mine: scripts, the bake, builder runs, docs.

## What was checked

PACK = `Assets/_Imports/ithappy/Cartoon_Weapons`, GAME = `Assets/_Game/Scripts`. Read 05:42 to 05:51 on 23 Nov. Nothing was run, built or changed.

**Confirmed in the files**
- Pack code is editor-only: `PACK/Scripts/ithappy.Cartoon_Weapons.asmdef:5-7`, all 17 scripts.
- Dart prefabs nest the presets whole, nothing removed (`Assets/_Game/Prefabs/Weapons/Dart*.prefab:297-300`). Markers as listed; no BarLeft or BarRight.
- How the pack mounts and skins: `PACK/Scripts/PreviewManager.cs:343-351, 368-374, 387-393, 399-420, 459-480`.
- Counts: 37 / 46 / 41 entries for our guns (13 / 22 / 17 parts + 24 charms), 54 gun lists, 274 config files, 10 skins, 7 charm-only guns.
- Presets are one skin each, through overrides on the fitted parts. Charms use `Color.mat`, not a skin material; no scripts or physics on them.
- Measured once: `GAME/Combat/Weapon.cs:89-90, 96-152`, `BlasterWeapon.cs:54`, `GunSight.cs:13, 23-24, 54-58`.
- Per-part numbers in per-gun rows: `GAME/Debug/WorkshopPieces.cs:87, 89, 255, 375`.
- Nothing saved: `GAME/Core/SaveGame.cs:18`, `HouseCarry.cs:8-13`. Saved-choice pattern: `PlayerOptions.cs:49-53`.
- Online copy: `GAME/Player/NetCombat.cs:25, 157-169`, `Combat/WeaponHolder.cs:745-771`.
- Every gun comes to hand through `WeaponHolder.Equip` (277-303; five calls, in the start loadout, the pick-up and the door carry). Shadow pass: 942-947.
- Armory node rebuilt: `GAME/Debug/CourseBuilder.House.cs:315, 358, 369, 384-392`. Garage menu: `Vehicles/Garage.cs:192-274`. Light check: `Room/LightQuery.cs:21-23`.
- Roadmap: `docs/BACKLOG.md:10` against 22-48. D-0165 and D-0166 are not in the project or in DECISIONS (last entry D-0164); both are staged outside.

**Corrected**
- "S touches none of that code, depends on nothing unplayed": S edits `WeaponHolder.cs`. D-0165 is staged as whole-file copies of `WeaponHolder.cs`, `ArmAim.cs` and `WeaponHoldSettings.cs`; the project still has the old ones. S goes on top of the landed fix.
- Missed by both readers: `GAME/Player/BodyFade.cs:85-107, 110-123` and `FX/WorldBend.cs:86-104, 134-137` swap a gun's materials for copies and restore the old ones.
- "First person can only be entered from inside its own script": wrong. `Core/GameMode.cs:20-26` is public and first person listens (`FirstPerson/FirstPersonMode.cs:61, 132-136`). The real gap is the mouse (`FirstPersonLook.cs:79-81`) and Esc (`FirstPersonMode.cs:112`). The Garage's look-around is car-only (`Camera/ShoulderCamera.cs:437, 526`); on foot the camera stops (196).
- "Nothing holds the player still on foot": partly wrong. `Player/PlayerMotor.cs:82-84` holds a player; a free mouse blocks fire clicks (`WeaponHolder.cs:536`); pause stands aside for a menu that holds the mouse (`UI/PauseMenu.cs:74-79`). Left: the pad trigger and the gun keys (`WeaponHolder.cs:369`).
- The Garage menu takes the first camera in the scene (`Garage.cs:200`); party play has up to four (`Core/PlayerSlot.cs:7-11`). The bench asks for this player's own.
- How the build part gets on a gun was not said: added when taken, as `WeaponLooks` is (`WeaponHolder.cs:288-289`), because the builder remakes the Dart prefabs.
- S effort: 2 sessions became 2 to 3, for the view of the gun.

**Inferred or not checked:** what a build does with the pack's lists (from how Unity works, not a test); the pack's own window, which looks in `Assets/ithappy` and so probably finds nothing here; icon size; parts and charms at toy size and in first person in play; whether a charm reaches behind a stock; every effort figure; anything online; builders other than the house build.
