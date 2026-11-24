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
4. `ShoulderCamera.LateUpdate` follows, after the player has moved. That order is why it never jitters.
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
| **WeaponBench** | `Prefabs/Pieces/Weapons/WeaponBench.prefab` (`Armory/ArmoryBench.cs` + `Armory/BenchScreen.cs`; made by Shift Jam > Armory > Make Workbench Prefab) | The armory workbench (D-0167). Drag it into any scene, at the TOP LEVEL of the Hierarchy (the course and house builders wipe what sits inside Room). A toy with a gun in hand presses E: a copy of the gun turns above the bench with the look applied live, rows change its skin and charm (keyboard, pad, mouse), the player's keys are off while it is open, and the look is saved as it is changed. A kid is refused in plain words. Tune size, spin and texts on the prefab. |
| **ArmoryCatalog** | `Resources/ArmoryCatalog.asset` (`Armory/ArmoryCatalog.cs`; written by Shift Jam > Armory > Bake Parts List, `Debug/ArmoryPieces.cs`) | The game's own list of gun skins and parts, baked from the weapons pack's config files and never typed by hand. It only points at pack files, and only lists what the game offers today (10 skins, the charms, the guns found in `Prefabs/Weapons`). `ArmoryPieces.BakedTypes` is the one list of part kinds on offer. Each part has room for fit numbers (working parts) and stat numbers (builds); nothing reads them yet. |
| **WeaponBuild** | `Armory/WeaponBuild.cs` (added to a gun by code, never on a prefab) | The look on one gun: puts the skin on by the pack's rule, hangs a charm on the pack's hook, measures the stock before anything is hung, and waits while BodyFade or WorldBend has the gun's materials. Added the first time a look is put on a gun; a gun nobody changed is not touched. |
| **ArmoryLooks** | `Armory/ArmoryLooks.cs` | The saved look per gun TYPE: one line of text in PlayerPrefs (`armory.look.<gun>`), kept the way the body choice is. `WeaponHolder.Equip` puts it on every gun that comes to a player's own hand. |
| **Toasts** | `UI/Toasts.cs` + `UI/ToastHud.cs` (+ `UI/HudShapes.cs`) | Any short message: `Toasts.Show(text, kind, title)` (Tip, Got, Refused, Warning, Core); `ShowFor(player, ...)` for one player's own screen; `Once(id, ...)` once per save. No setup: the HUD makes itself. `<b>Key</b>` = key cap. A tool's first pickup uses its `howTo`. |

