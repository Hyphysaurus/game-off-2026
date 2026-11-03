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

## Animator contract (built by hand in the editor)
Parameters: `Speed` float, `Grounded` bool, `VerticalVelocity` float.
Base layer: **Locomotion** blend tree on `Speed` — Idle at 0, Walk at 0.5, Run at 1 (all three clips at
speed 1). Two sub-machines, JumpIdle and JumpRun, each start / middle / end. Leaving the ground always
uses the run branch (D-0007; the idle branch is kept in the file, unused); `VerticalVelocity` > 0.5 (a jump) enters
the *start* clip part-way in (offset 0.3 / 0.2, speed 2.5 / 2) so the crouch is skipped, anything else
(a fall) goes straight to the *middle* loop. Landing (`Grounded` true) while moving blends into
Locomotion in 0.15 s; landing still plays the *end* clip at speed 2, which any stick input or a new jump
cuts short. Every return is a direct transition to Locomotion (no Exit node), so the blend tree never
restarts. `Grounded` sent to the Animator is steadied by `groundedGrace` (0.1 s) against the capsule's
one-frame flicker; a jump clears it at once. Timings: D-0006.

## Input contract
The template `InputSystem_Actions` asset, Player map: Move, Look, Jump, Sprint, Interact. Interact carries a
Hold interaction in the asset; `PlayerInput` listens to `started`, so E is a press.

## Folders
`_Game` ours (Scripts by feature, Data, Prefabs, Animation, Scenes, Input, Materials, UI). `_Imports` bought
content. `_Tools` bought tools. All of `_Imports`, Feel, Shapes, FastScriptReload and Linework Lite are
git-ignored at any depth (licences); UVCS carries them.
