# Architecture

Four kinds of things. Each has a test for where a new idea goes.

| Kind | Test | Here |
| --- | --- | --- |
| **Component** | Can you name its job in five words? | PlayerInput, PlayerMotor, ShoulderCamera, Interactor, Shifter, ShiftSource, Switch, Door, InteractPrompt |
| **Data asset** | Would you tune it in Play and keep it? | MovementSettings, CameraSettings, ShiftSettings (ScriptableObjects in `_Game/Data`) |
| **Director** | Must it survive a world swap? | GameManager, ShiftDirector — not yet; arrive with the first portal, in a Core scene |
| **Event** | Does the announcer need to know who cares? | `GameEvents.ShiftStarted / Shifted`; `PlayerMotor.Jumped / Landed`; `Interactor.TargetChanged` |

Dependency direction: components read data assets and raise events; directors depend on components;
nothing depends on UI. A script that searches the scene for another script is the smell: make it an
Inspector reference or an event.

## Each frame
1. `PlayerInput` reads the devices (the only script that knows about devices).
2. `PlayerMotor.Update` moves the capsule and writes `Speed` (0..1), `Grounded`, `VerticalVelocity` and `AirPhase` (where the body is in a jump's arc, D-0169) to the Animator.
3. The Animator picks the clip.
4. `ShoulderCamera.LateUpdate` follows, after the player has moved. That order is why it never jitters. It asks `PlayerInput.ConsumeLook` for the frame's look there, once (first person: `FirstPersonLook.LateUpdate`).
5. `Interactor.Update` finds the nearest `IInteractable` in reach; `InteractPrompt` shows its words.

## The shift
`Shifter` owns one number, `Scale`. Motor speeds, jump height, gravity, camera distance, near clip and
interaction reach all multiply by it. Jump timing stays the same at both sizes because height and gravity
scale together. The blend between sizes is geometric (even growth to the eye), eased by a curve.

## Animator contract (built by `PlayerPortBuilder`, D-0064; never by hand)
Rebuild with **Shift Jam > Player > Rebuild Clips + Controllers (D-0169)**: clips and the two controllers only. Never
Build Player Port (it opens and saves four scenes; it asks first now).
One controller per body (`PlayerMale`, `PlayerFemale`), from the Playroom clips' copies. Parameters: `Speed` float,
`Grounded` bool, `VerticalVelocity` float, `AirPhase` float (0 take-off, 0.5 the top, 1 landing) (the motor writes these); `MoveX`, `MoveZ` (own units a second against the
body's facing), `Crouched`, `Pace` (the legs' playback) and `MoveSpeed` (a one-shot's playback) from `PlayerAnimation`.
Base layer: **Locomotion** and **Crouch** are 2D blend trees on `MoveX` / `MoveZ`, each clip placed at the speed it
covers ground at; **Air** (the jump's arc `Jump_Arc`, cut from the old pack's running-jump clips, shown at the time `AirPhase` names: D-0169), **Fall**, **Land**; the one-shots **Roll, Punch, Kick, Block, HitLight,
HitHeavy, Death, Fidget**, started and ended by code; **Wake** (the death backwards). A take-off (`VerticalVelocity`
> 0.5) blends straight to Air, a walk-off to Fall; landing while moving blends to Locomotion, landing still plays Land
from its touch-down. Layers 1 and 2 are **Arms** (one arm holding) and **Carry** (two arms, IK pass), as on the old
body. `Grounded` sent to the Animator is steadied by `groundedGrace` (0.1 s). The old `PlayerAnimator.controller`
(D-0006, D-0007) is no longer on the Player.

## Input contract
The template `InputSystem_Actions` asset, Player map: Move, Look, Jump, Sprint, Interact, Attack, Shift, Previous, Next,
and since D-0064 Crouch, Holster, Aim, Dodge, Block. Interact carries a Hold interaction in the asset; `PlayerInput`
listens to `started`, so E is a press. `PlayerInput` is still the only script that turns them into intents.
The look (D-0174): the Look action carries the stick only. The mouse's movement is read from the pointer devices
themselves, once a frame, when a camera asks (`ConsumeLook`): the Input System hands an action a pointer's running
total again at every button or key event in a frame, so adding the action's values up counts movement twice. Whoever
asks first in a frame gets the mouse (a held thing being turned, before the camera). With aim held each camera turns
the look down by as much as its view has narrowed (Aim Look Match in `CameraSettings` / `FirstPersonSettings`).
One set of speeds (D-0175): first person takes its walk, sprint, crouch speed, aim rule, jump and fall from the
third-person motor and moves (`FirstPersonSettings` > Match Third Person), so a number is changed in one place.

## Folders
`_Game` ours (Scripts by feature, Data, Prefabs, Animation, Scenes, Input, Materials, UI). `_Imports` bought
content. `_Tools` bought tools. All of `_Imports`, Feel, Shapes, FastScriptReload and Linework Lite are
git-ignored at any depth (licences); UVCS carries them.

## Parts: drop-in pieces for any scene or section (D-0143 on)
His rule (22 Nov): every system ships as a part that works dropped into any section, its tuning on the part or in a
data asset, wired into the one player and the scene carry if the player holds it, and listed here with how to place it.
Level beats are placed parts, not coordinates in builder code.

| Part | Where | How to use it |
| --- | --- | --- |
| **Player** | `Prefabs/Player/Player.prefab` | The one player: every scene's Player is an instance of it (Greybox, Workshop, Raceway, Colour Corner), the online `Resources/Net/NetPlayer` a variant. Something every player needs goes on the prefab, once. A scene's own start or extras (Colour Corner's AmmoBelt, Loadout) stay overrides on its instance. A new scene: drag the prefab in, name it Player. Rebuilt or re-linked by Shift Jam / Player / Make One Player Prefab (safe to run again). |
| **PlayerHands** | on Player.prefab (`Player/PlayerHands.cs`) | What each hand holds. A new hand-held thing only says its hand: a Carryable's **Hand** (Auto, Right, Left; planks and GripR+GripL are Both), a Weapon is Right (Both when Two Handed). Right + Left are held together; Both puts the left-hand thing away until it goes. Code that takes something into the hands calls `PlayerHands.MakeRoomFor(...)` first. |
| **Flashlight** | `Prefabs/Tools/Flashlight.prefab` | A light to pick up, aim, focus and carry. Drag it in. For a toy-only place: set Carryable > Made At = Toy and scale it to a toy's (1/35). Tune on it: Beam Intensity (at kid size), Show Shaft / Shaft Length, Bulb Glow, Focus while aiming, Start On, a Core inside. Goes in the left hand (Carryable Hand Auto). Re-link loose pack copies: Shift Jam / Tools / Make Flashlight Prefab. |
| **LightTarget** | `Room/LightTarget.cs` (add to anything) | Reacts to the flashlight: fills while lit (faster or only when focused), drains after. Wire **On Filled** to the reaction: a ToySoldier's **Dazzle**, a ShiftSource's **Awaken**, a Door, etc.; **On Fill** to LightReveal.Show or LightGlow.GlowOn. The light check itself is `LightQuery.Lit` (use it, never a copy). |
| **LightReveal** | `Room/LightReveal.cs` | A mark only the light shows: with a LightTarget on the same object, On Fill -> Show. See-through material. |
| **LightGlow** | `Room/LightGlow.cs` | A target glows as it charges: On Fill -> GlowOn. Material with Emission on. |
| **ArcPreview** | `FX/ArcPreview.cs` | Draws any thrown thing's predicted path and landing ring (`ArcPreview.For(owner)`, then Show / Hide). The grenade uses it with `BallFlight.Predict`. |
| **DarkZone** | `Room/DarkZone.cs` (+ `KeepLit`) | A stretch that goes dark while the player is in it: add to an empty object, size its box (gizmo). It dims the lights inside and the ambient; checkpoints, core embers and flashlights stay lit; add KeepLit to a lantern to keep it. |
| **BlastRing** | `FX/BlastRing.cs` | `BlastRing.Play(at, radius, colour, width, seconds)`: a shockwave ring growing to a blast's reach. The grenade uses it. |
| **Grabbable** | `Interaction/Grabbable.cs` (+ a Rigidbody) | A heavy thing to grab (E) and steer: by view (it points where you look) or stick, about its middle or far end, push / pull (forward / back); **Anchored** for a thing that only turns on a hub (a turntable: put what rides it under it). Set who may grab it and the speeds on it. The player is held by `PlayerMotor.HoldAt` and walks round with it; both palms go on it through `CarryPose` (found on its own colliders at the grab, D-0164), a tool in hand is put away and the gun hidden while held; Anchored stops turning when the grabber is blocked. |
| **SightLook** | `Resources/SightLook.asset` (`Combat/SightLook.cs`) + `Shaders/SightOverlay.shader` + `Data/Sights/*.mat` | How every gun sight looks: one list "a material of the pack's sights -> what the game shows" (a clear lens, a self-lit reticle). A weapon asks as it wakes, a pick-up stand asks for its display, so any gun and any sight fitted later gets it. A gun's own reticle: `Weapon > Reticle`. Tune colour, size and which side shows on the materials. Keep lens material names starting `Sight_Glass` / `Sight_Projection` (`GunSight` aims by them). |
| **ShotRules** | `Combat/ShotRules.cs` (add to an empty object in a scene) | How shots land in that scene (D-0172): **Foam Dart** (they fly, fall and stick; the house) or **Instant** (a shot is decided at the click, on what is under the dot, and since D-0176 is seen as a dart flying there from the gun: `Projectile.FireAt`, `Resources/WeaponAmmo` > **Flight Speed** 110 and **Flight Most Seconds** 0.25; versus scenes: the Stadium has one). No part = foam darts. F9 flips it by hand anywhere. A flying shot is aimed for its drop (D-0174): `Projectile.AimFor` gives the direction whose arc passes through the point under the dot, out to `Resources/WeaponAmmo` > **Drop Aim Within** (40 units; 0 = fired straight, landing under the dot). Anything that fires a `Projectile` can use it; the toy soldiers do not. |
| **WeaponBench** | `Prefabs/Pieces/Weapons/WeaponBench.prefab` (`Armory/ArmoryBench.cs` + `Armory/BenchScreen.cs`; made by Shift Jam > Armory > Make Workbench Prefab) | The armory workbench (D-0167). Drag it into any scene, at the TOP LEVEL of the Hierarchy (the course and house builders wipe what sits inside Room). A toy with a gun in hand presses E: a copy of the gun turns above the bench with the look applied live, rows change its skin and charm (keyboard, pad, mouse), the player's keys are off while it is open, and the look is saved as it is changed. A kid is refused in plain words. Tune size, spin and texts on the prefab. |
| **Rope** | `Prefabs/Pieces/Traversal/Rope.prefab` (`Room/Rope.cs`; made by Shift Jam > Traversal > Make Rope Piece) | A rope a toy swings on (D-0170). Drag it in at the TOP LEVEL of the Hierarchy with its pivot where the rope is tied (under a ceiling or a beam), rotation 0, scale 1, and set **Length**. E beside it or flying into it grabs it. Leave room: a swing reaches 0.97 of the length to every side, and what it hits it kicks off. For a rope that rewards a climb, keep its low end more than 4.3 over the floor (a jumping toy's hands reach that). It bends and whips by itself (looks only) and is drawn as a tube with `Data/Rope.mat` (URP Lit: never set its material from code, the shift's bend swaps it). |
| **RopeRider** | on Player.prefab (`Player/RopeRider.cs`; added by Shift Jam > Traversal > Add Rope Rider To Player Prefab) | The player's side of a rope: the grab (E, or in the air), the swing the stick pushes, climb (Sprint) and slide (Crouch), the let-go on Jump. It moves the body through `PlayerMotor.SetPull` (so walls stop it and no landing on a rope is a fall) and lets go through `PlayerMotor.Fling` (the flight is the motor's, so the fall rule judges the landing). Hands by `CarryPose`; the gun hides (`WeaponHolder.HandsFull` asks `RopeRider.Holds`); a shift, a hit, a respawn, first person and full hands all let go. Toy only, third person, this PC's player only. |
| **RopeSettings** | `Resources/Rope.asset` (`Data/RopeSettings.cs`) | Every rope number in the toy's own units: reach, the swing's gravity share and push, the kick off a wall, climb and slide speeds, the let-go's lift and air steer, where the hands hold (`handOffset`, live), the rope's own chain and tube, and the words. One asset for every rope and rider. |
| **ArmoryCatalog** | `Resources/ArmoryCatalog.asset` (`Armory/ArmoryCatalog.cs`; written by Shift Jam > Armory > Bake Parts List, `Debug/ArmoryPieces.cs`) | The game's own list of gun skins and parts, baked from the weapons pack's config files and never typed by hand. It only points at pack files, and only lists what the game offers today (10 skins, the charms, the guns found in `Prefabs/Weapons`). `ArmoryPieces.BakedTypes` is the one list of part kinds on offer. Each part has room for fit numbers (working parts) and stat numbers (builds); nothing reads them yet. |
| **WeaponBuild** | `Armory/WeaponBuild.cs` (added to a gun by code, never on a prefab) | The look on one gun: puts the skin on by the pack's rule, hangs a charm on the pack's hook, measures the stock before anything is hung, and waits while BodyFade or WorldBend has the gun's materials. Added the first time a look is put on a gun; a gun nobody changed is not touched. |
| **ArmoryLooks** | `Armory/ArmoryLooks.cs` | The saved look per gun TYPE: one line of text in PlayerPrefs (`armory.look.<gun>`), kept the way the body choice is. `WeaponHolder.Equip` puts it on every gun that comes to a player's own hand. |
| **Toasts** | `UI/Toasts.cs` + `UI/ToastHud.cs` (+ `UI/HudShapes.cs`) | Any short message: `Toasts.Show(text, kind, title)` (Tip, Got, Refused, Warning, Core); `ShowFor(player, ...)` for one player's own screen; `Once(id, ...)` once per save. No setup: the HUD makes itself. `<b>Key</b>` = key cap. A tool's first pickup uses its `howTo`. |

