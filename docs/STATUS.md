# STATUS — Shift Jam

Truth for what is BUILT vs DESIGNED. Newest entry first. Versions log at the end.

## 2 Nov 2026 21:5x — his play: "looks decent", idle jump "still awkward, a pose thing" (Unity OPEN on Shift Jam, in Play: nothing landed)
**Read from the clips (humanoid muscle curves):** the Jump idle start clip squats to its deepest at 0.46–0.54 s (my
offset 0.3 entered it exactly there, so the figure squats in mid-air), launches at ~0.71–0.79 s, and ends in a
tight tuck (chest −0.71, legs tucked, arms up) which "Jump idle middle" then HOLDS as a static pose; the idle end
clip spends its first 0.3 s still falling (ground contact ≈0.44 s), so at offset 0 the figure "falls" while standing
on the floor. The run-jump clips have none of this: immediate launch, a gentle cycling air pose. **Prepared in the
session scratchpad, NOT landed:** `PlayerAnimator.v3.controller` = his branches kept, offsets moved to the launch
and contact frames (idle start 0.5, idle end 0.3, run end 0.65, end speeds 1.5); `PlayerAnimator.v4.controller`
(recommended) = every jump and fall uses the run-jump clips, idle clips left in the file unused. His pick: v4 LANDED 22:3x
("proceed"; previous controller backed up in the scratchpad). Unplayed. Fingers: the rig HAS finger bones (Thumb/Index/Middle/Ring/Pinky ×2 joints, both
hands) and the clips animate them; no fist pose in the pack (Fighting_stance fingers only slightly curled).

## 2 Nov 2026 21:15 — animator retimed, motor steadied (files only, Unity closed, he in AoE4)
**His report:** "the animations looked off, it was stuttering, jittery"; wants buttery Mario-64-like third person.
He had not yet played the 16:1x threshold fix (scene and data assets last saved ~13:00).
**Landed (compiled outside with Unity's csc, 0 errors, NOT yet opened in Unity):** `PlayerAnimator.controller`
regenerated (D-0006: clip speeds 1, jump vs fall split on VerticalVelocity, start clips offset + fast, moving
landings blend to Locomotion, end clips cut by input, direct transitions, Solo off, his layout kept; backup in
the session scratchpad `backup/`); `PlayerMotor` sends a steadied Grounded (`groundedGrace`) and cuts a jump
short on early release (`jumpCutMultiplier`); `PlayerInput.JumpHeld`; two new MovementSettings fields (the
asset takes the script defaults 0.1 / 0.5 until he saves it).
**Not done / his to check in Play:** foot slide at walk/sprint = tune walkSpeed/sprintSpeed on the asset to
the clips (unknown authored speed); the start-clip offsets (0.3 idle, 0.2 run) are a guess at where the
launch frame sits, nudge in the controller if the character still crouches after take-off. Still uncommitted
since 11:5x (scripts, docs, his scene, data assets, controller).
**Next:** he opens Unity (import, ~20 s compile), plays walk / sprint / jump standing / jump running / walk off
the floor edge / tap vs hold jump, reports; then commit; then STEP 3 room + shift beat.

## 2 Nov 2026 16:00 — his STEP 1 + animator, examined (Unity closed)
**Built by him (uncommitted):** Greybox.unity has Player (Basic_Character instance, CharacterController 1.8/0.3,
PlayerInput + PlayerMotor + Shifter + Interactor wired to the three settings assets), Main Camera with
ShoulderCamera wired, a 20 m TestFloor; `_Game/Data` holds MovementSettings, CameraSettings, ShiftSettings at
defaults; `_Game/Animation/PlayerAnimator.controller` = Locomotion blend tree (Idle/Walk/Run) + Jump idle and
Jump run start/middle/end on Grounded. Scripts compiled in his editor at 11:49, 0 errors; the only exceptions in
the log are Unity's own animator-transition inspector, not ours.
**Found:** blend tree thresholds are 0 / 0.1 / 6 but the motor sends Speed 0..1 (walk 0.5, run 1): Run never
plays. No ShiftSource or prompt canvas yet, so the shift could not be triggered: Shifter now has an
editor-only T key toggle (devToggleKey). Build settings list SampleScene, not Greybox. `New Terrain.asset`
still at the Assets root.
**16:1x, controller fixed in the .controller file (Unity closed, he in AoE4):** thresholds 0/0.5/1 (max 1); the
Locomotion transition that pointed at NO state now goes to Jump run start (Grounded false, Speed > 0.1) and a new
one goes to Jump idle start (Grounded false, Speed < 0.1); Jump run middle -> end lands at once (exit time off);
run start -> middle at 0.6; idle end / run end return to Locomotion at 0.6 / 0.3. Backup of his version in the
session scratchpad. **Next:** he opens Unity, plays walk/sprint/jump/T, reports feel; then STEP 3.

## 2 Nov 2026 11:5x — scripts landed, nothing wired yet
**Built (files, compiled clean outside the editor, 0 errors, not yet run in Unity):**
`Assets/_Game/Scripts/` — Data/MovementSettings, CameraSettings, ShiftSettings; Player/PlayerInput,
PlayerMotor; Camera/ShoulderCamera; Interaction/IInteractable, Interactor; Shift/Shifter, ShiftSource,
Switch, Door; UI/InteractPrompt; Core/GameEvents. Docs: DESIGN, ARCHITECTURE, DECISIONS, this file.
**Designed, not built:** the three settings assets, the Player prefab, the animator controller, the
tutorial room in Greybox.unity, Feel hooks on ShiftSource/Switch/Door, a Core scene, portals.
**Repo:** `fcc8c84` reorganisation committed; scripts + docs uncommitted. Remote = his push.
**Next:** STEP 1 (settings assets + Player prefab + camera + prompt, playable walk/jump) → STEP 2 animator
→ STEP 3 room greybox + shift beat → feel pass → commit → v1.

## Versions
- (none yet) v1 = first playable tutorial room.
