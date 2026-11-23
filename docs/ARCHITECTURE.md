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
2. `PlayerMotor.Update` moves the capsule and writes `Speed` (0..1), `Grounded`, `VerticalVelocity` to the Animator.
3. The Animator picks the clip.
4. `ShoulderCamera.LateUpdate` follows, after the player has moved. That order is why it never jitters.
5. `Interactor.Update` finds the nearest `IInteractable` in reach; `InteractPrompt` shows its words.

## The shift
`Shifter` owns one number, `Scale`. Motor speeds, jump height, gravity, camera distance, near clip and
interaction reach all multiply by it. Jump timing stays the same at both sizes because height and gravity
scale together. The blend between sizes is geometric (even growth to the eye), eased by a curve.

## Animator contract (built by `PlayerPortBuilder`, D-0064; never by hand)
One controller per body (`PlayerMale`, `PlayerFemale`), from the Playroom clips' copies. Parameters: `Speed` float,
`Grounded` bool, `VerticalVelocity` float (the motor writes these); `MoveX`, `MoveZ` (own units a second against the
body's facing), `Crouched`, `Pace` (the legs' playback) and `MoveSpeed` (a one-shot's playback) from `PlayerAnimation`.
Base layer: **Locomotion** and **Crouch** are 2D blend trees on `MoveX` / `MoveZ`, each clip placed at the speed it
covers ground at; **Air** (the jump loop), **Fall**, **Land**; the one-shots **Roll, Punch, Kick, Block, HitLight,
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
| **Toasts** | `UI/Toasts.cs` + `UI/ToastHud.cs` (+ `UI/HudShapes.cs`) | Any short message: `Toasts.Show(text, kind, title)` (Tip, Got, Refused, Warning, Core); `ShowFor(player, ...)` for one player's own screen; `Once(id, ...)` once per save. No setup: the HUD makes itself. `<b>Key</b>` = key cap. A tool's first pickup uses its `howTo`. |

