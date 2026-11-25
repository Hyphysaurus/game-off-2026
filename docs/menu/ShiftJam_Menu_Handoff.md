# Shift Jam menu model handoff

Source: `C:\ShiftJamMenu\menu.blend`

FBX: `Assets/_Game/Art/Menu/ShiftJam_Menu.fbx`
SHA-256: `a6e48abf5641906473305bc185ac93a969eef00237df917d714490b38510400c`

**41 objects / 24 meshes / 17 empties / 3312 triangles.** No exported animation clips, texture files, text meshes, title image, backdrop, screenshot thumbnail or gameplay code.

## Integration contract

- Instantiate this art hierarchy from the existing runtime TitleMenu; do not place a menu object in a Unity scene.
- Keep the live game room and its existing RGB 8,8,15 / 72% tint behind the menu. No Backdrop was exported.
- Place the existing animated 75x35, 64-frame, 4-second title at Anchor_Title. This export does not rebuild or contain it.
- All captions, slots, hint, name input and version are live Unity TextMeshPro/uGUI content at the named empties.
- Label local +X reads right, local +Y reads up and local +Z faces the viewer. Labels and anchors rotate +90 degrees about Blender X, so TMP uses their local XY plane. Control bodies and frames have identity rotations/scales and centred origins.
- Blender coordinates: +X right, +Z up, front -Y. FBX retains Blender-native +Y forward / +Z up metadata. Unity converts this to its Y-up basis on import; do not independently rotate or offset child controls. This basis avoids a reproduced Blender Apply Transform bug that displaces children of mesh parents when preconverting to Y-up. All requested export flags remain enabled. Verify Unity import with Claude; no Unity editor instance was opened here.
- Map 1 metre to 600 UI reference pixels: a 3.2m by 1.8m frame corresponds to 1920x1080. All control geometry fits the central 2.4m by 1.8m / 4:3 safe area.
- Slots use actual SaveGame.TryLoad metadata (room, toy/kid, date/time), or empty. No pictures are required. Optional room icons can be supplied by Unity in the reserved left margin.
- The authoring preview has Slot_1 active, Btn_NewGame selected and Btn_Continue disabled. These are visual defaults, NOT real saved data. Unity must refresh them from SaveGame.Slot and valid saves.
- Recolour <ControlName>_Frame for Normal / Selected / Active / Disabled. Selected applies equally to mouse, keyboard and gamepad. State priority: Disabled, then Selected, then Active, then Normal.
- Suggested frame colours: Normal deep purple #492393; Selected cyan #73f1ff; Active pink #ff72cc; Disabled ink #0a0812. Pale #fff0fc for Unity text.
- Btn_Options retains the required object name; suggested visible caption is Settings. The exact supplied Workshop/Raceway/Colours/Online/Parties controls are included; Btn_Online supplies the visible Multiplayer entry.
- Use existing callbacks and UiAudio/UiSound. Suggested focus 100ms / press 70ms / release 120ms, with 1.8mm lift and 2.5mm depression. Run all feedback and title animation on UNSCALED time.
- No per-button animation is baked. Bodies and child frames can be moved/recoloured by Unity code; no physics is needed.

## Controls and selection order

| Order | Control | Position X,Y,Z (m) | Size X,Y,Z (m) | Initial visual state |
|---|---|---|---|---|
| 0 | Btn_Continue | [0.0, 0.0, 0.325] | [1.0, 0.024, 0.13] | disabled |
| 1 | Btn_NewGame | [0.0, 0.0, 0.16] | [1.0, 0.024, 0.13] | selected |
| 2 | Slot_1 | [0.0, 0.0, -0.015] | [1.72, 0.022, 0.12] | active |
| 3 | Slot_2 | [0.0, 0.0, -0.17] | [1.72, 0.022, 0.12] | normal |
| 4 | Slot_3 | [0.0, 0.0, -0.325] | [1.72, 0.022, 0.12] | normal |
| 5 | Btn_Workshop | [-0.78, 0.0, -0.525] | [0.35, 0.022, 0.102] | normal |
| 6 | Btn_Raceway | [-0.39, 0.0, -0.525] | [0.35, 0.022, 0.102] | normal |
| 7 | Btn_Colours | [0.0, 0.0, -0.525] | [0.35, 0.022, 0.102] | normal |
| 8 | Btn_Online | [0.39, 0.0, -0.525] | [0.35, 0.022, 0.102] | normal |
| 9 | Btn_Parties | [0.78, 0.0, -0.525] | [0.35, 0.022, 0.102] | normal |
| 10 | Btn_Options | [-0.25, 0.0, -0.69] | [0.46, 0.022, 0.11] | normal |
| 11 | Btn_Quit | [0.25, 0.0, -0.69] | [0.46, 0.022, 0.11] | normal |

## Every exported object

Positions are local to the listed parent; complete world positions, rotations, material slots and text rectangle sizes are in the JSON manifest. Empty object geometry size is zero; its intended text area is listed separately.

| Object | Type | Parent | Local position X,Y,Z (m) | Geometry size X,Y,Z (m) | Text rect W,H (m) |
|---|---|---|---|---|---|
| Anchor_Hint | EMPTY | Menu_Root | [0.0, -0.045, -0.815] | [0.0, 0.0, 0.0] | [2.15, 0.05] |
| Anchor_Name | EMPTY | Menu_Root | [0.9, -0.045, 0.78] | [0.0, 0.0, 0.0] | [0.48, 0.105] |
| Anchor_Title | EMPTY | Menu_Root | [0.0, -0.045, 0.65] | [0.0, 0.0, 0.0] | [1.0, 0.4666666666666667] |
| Anchor_Version | EMPTY | Menu_Root | [1.03, -0.045, -0.865] | [0.0, 0.0, 0.0] | [0.29, 0.035] |
| Btn_Colours | MESH | Menu_Root | [0.0, 0.0, -0.525] | [0.35, 0.022, 0.102] | — |
| Btn_Colours_Frame | MESH | Btn_Colours | [0.0, 0.0, 0.0] | [0.349, 0.002, 0.101] | — |
| Btn_Continue | MESH | Menu_Root | [0.0, 0.0, 0.325] | [1.0, 0.024, 0.13] | — |
| Btn_Continue_Frame | MESH | Btn_Continue | [0.0, 0.0, 0.0] | [0.999, 0.002, 0.129] | — |
| Btn_NewGame | MESH | Menu_Root | [0.0, 0.0, 0.16] | [1.0, 0.024, 0.13] | — |
| Btn_NewGame_Frame | MESH | Btn_NewGame | [0.0, 0.0, 0.0] | [0.999, 0.002, 0.129] | — |
| Btn_Online | MESH | Menu_Root | [0.39, 0.0, -0.525] | [0.35, 0.022, 0.102] | — |
| Btn_Online_Frame | MESH | Btn_Online | [0.0, 0.0, 0.0] | [0.349, 0.002, 0.101] | — |
| Btn_Options | MESH | Menu_Root | [-0.25, 0.0, -0.69] | [0.46, 0.022, 0.11] | — |
| Btn_Options_Frame | MESH | Btn_Options | [0.0, 0.0, 0.0] | [0.459, 0.002, 0.109] | — |
| Btn_Parties | MESH | Menu_Root | [0.78, 0.0, -0.525] | [0.35, 0.022, 0.102] | — |
| Btn_Parties_Frame | MESH | Btn_Parties | [0.0, 0.0, 0.0] | [0.349, 0.002, 0.101] | — |
| Btn_Quit | MESH | Menu_Root | [0.25, 0.0, -0.69] | [0.46, 0.022, 0.11] | — |
| Btn_Quit_Frame | MESH | Btn_Quit | [0.0, 0.0, 0.0] | [0.459, 0.002, 0.109] | — |
| Btn_Raceway | MESH | Menu_Root | [-0.39, 0.0, -0.525] | [0.35, 0.022, 0.102] | — |
| Btn_Raceway_Frame | MESH | Btn_Raceway | [0.0, 0.0, 0.0] | [0.349, 0.002, 0.101] | — |
| Btn_Workshop | MESH | Menu_Root | [-0.78, 0.0, -0.525] | [0.35, 0.022, 0.102] | — |
| Btn_Workshop_Frame | MESH | Btn_Workshop | [0.0, 0.0, 0.0] | [0.349, 0.002, 0.101] | — |
| Label_Btn_Colours | EMPTY | Btn_Colours | [0.0, -0.0135, 0.0] | [0.0, 0.0, 0.0] | [0.305, 0.074] |
| Label_Btn_Continue | EMPTY | Btn_Continue | [0.0, -0.0145, 0.0] | [0.0, 0.0, 0.0] | [0.955, 0.10200000000000001] |
| Label_Btn_NewGame | EMPTY | Btn_NewGame | [0.0, -0.0145, 0.0] | [0.0, 0.0, 0.0] | [0.955, 0.10200000000000001] |
| Label_Btn_Online | EMPTY | Btn_Online | [0.0, -0.0135, 0.0] | [0.0, 0.0, 0.0] | [0.305, 0.074] |
| Label_Btn_Options | EMPTY | Btn_Options | [0.0, -0.0135, 0.0] | [0.0, 0.0, 0.0] | [0.41500000000000004, 0.082] |
| Label_Btn_Parties | EMPTY | Btn_Parties | [0.0, -0.0135, 0.0] | [0.0, 0.0, 0.0] | [0.305, 0.074] |
| Label_Btn_Quit | EMPTY | Btn_Quit | [0.0, -0.0135, 0.0] | [0.0, 0.0, 0.0] | [0.41500000000000004, 0.082] |
| Label_Btn_Raceway | EMPTY | Btn_Raceway | [0.0, -0.0135, 0.0] | [0.0, 0.0, 0.0] | [0.305, 0.074] |
| Label_Btn_Workshop | EMPTY | Btn_Workshop | [0.0, -0.0135, 0.0] | [0.0, 0.0, 0.0] | [0.305, 0.074] |
| Label_Slot_1 | EMPTY | Slot_1 | [0.04, -0.0135, 0.0] | [0.0, 0.0, 0.0] | [1.5, 0.092] |
| Label_Slot_2 | EMPTY | Slot_2 | [0.04, -0.0135, 0.0] | [0.0, 0.0, 0.0] | [1.5, 0.092] |
| Label_Slot_3 | EMPTY | Slot_3 | [0.04, -0.0135, 0.0] | [0.0, 0.0, 0.0] | [1.5, 0.092] |
| Menu_Root | EMPTY | — | [0.0, 0.0, 0.0] | [0.0, 0.0, 0.0] | — |
| Slot_1 | MESH | Menu_Root | [0.0, 0.0, -0.015] | [1.72, 0.022, 0.12] | — |
| Slot_1_Frame | MESH | Slot_1 | [0.0, 0.0, 0.0] | [1.719, 0.002, 0.119] | — |
| Slot_2 | MESH | Menu_Root | [0.0, 0.0, -0.17] | [1.72, 0.022, 0.12] | — |
| Slot_2_Frame | MESH | Slot_2 | [0.0, 0.0, 0.0] | [1.719, 0.002, 0.119] | — |
| Slot_3 | MESH | Menu_Root | [0.0, 0.0, -0.325] | [1.72, 0.022, 0.12] | — |
| Slot_3_Frame | MESH | Slot_3 | [0.0, 0.0, 0.0] | [1.719, 0.002, 0.119] | — |

## Materials and textures

| Material | Base colour (sRGB) | Texture files |
|---|---|---|
| SJMenu_Cyan | #73f1ff | None |
| SJMenu_DeepPurple | #492393 | None |
| SJMenu_Ink | #0a0812 | None |
| SJMenu_Pale | #fff0fc | None |
| SJMenu_Pink | #ff72cc | None |
| SJMenu_Purple | #b24dff | None |

Materials are plain base-colour Principled materials, roughness 0.38, metallic 0. Set up URP emission/glow in Unity if wanted. No procedural texture shader is required.

## Actions

**Exported actions: none.** The 3D controls are intentionally static. The existing pixel title animation is handled by Unity.

The native working file also preserves earlier prototype scenes. Their historical actions are excluded from this export:

| Historical action (not exported) | Range | Length in frame intervals |
|---|---|---|
| demo_continue_focus_press | 1.0–150.0 | 149.0 |
| demo_new_game_focus_press | 1.0–240.0 | 239.0 |
| demo_save_01_focus | 1.0–150.0 | 149.0 |
| demo_save_02_focus | 1.0–150.0 | 149.0 |
| demo_save_03_focus | 1.0–150.0 | 149.0 |
| title_original_light_sweep_4000ms | 1.0–121.0 | 120.0 |

## Validation

- Native file saved under the neutral path BEFORE export.
- FBX export: meshes and empties only; Apply Scalings FBX All; Apply Transform on; modifiers applied; no animation; triangulated.
- FBX bytes scanned for the username and Windows user-home paths (ASCII and UTF-16): PASS.
- Static FBX reimport: 41 objects, 24 meshes, complete expected parent hierarchy; dimensions and world positions agree within 0.1mm; local reading axes are preserved.
- All control rotations are zero and scales are one in source; frame origins share each body centre.
- All 12 controls and their exact frame/label children validated; all 16 control/anchor rectangles are non-overlapping and inside the central 4:3 safe area.
- No Unity code, scene, save data, existing prefab or setting was edited; only the requested new art output files were written. No commit, push or Unity batch launch.

## Provenance / itch attribution

Menu button and save-slot meshes were AI-generated with OpenAI ChatGPT using Blender Python; palette/material setup was also AI-assisted. Existing title art is credited separately.

Generated geometry: every body and frame mesh listed above. Generated material setup: the six palette materials. Generated textures/images: none in this deliverable. Original title artwork remains user-provided and separate. The earlier AI-generated concept mockup is a reference, not a texture in this FBX.

## Limitations and design decisions

- This is a model handoff, not an installed Unity menu or networking implementation. Claude owns runtime wiring and final import/play-mode testing.
- Save thumbnails were deliberately omitted; room icons can be assigned by Unity without changing the save format.
- Live room stays visible; no flat purple/silhouette Backdrop is included.
- The latest exact control contract supersedes the earlier simplified prototype: all requested utility and online controls are included.
- No unresolved modeling questions or missing required controls.
