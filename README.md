# Shift (working title)

An entry for [GitHub Game Off 2026](https://itch.io/jam/game-off-2026), theme **Shift**.

✍ **[HIS PITCH: one or two lines in your words]**

You play a toy figure that can shift between toy size and kid size. Every room is played at both sizes: at toy size
the floor is the level, at kid size the furniture is. The course runs from a cardboard toy box through the bedroom, a
run along the skirting board, the playroom and the wardrobe to the window.

**Play it:** ✍ **[itch.io link when the page is up]** (Windows).

## Controls

Keyboard and mouse, or a gamepad. In the game, **H** (pad: View) shows every key.

The main keys (the in-game list, H, is complete and shows the gamepad's too):

| Keyboard / mouse | |
| --- | --- |
| WASD / arrows | Move |
| Mouse | Look |
| Space (hold for higher) | Jump |
| Left Shift | Run |
| C (hold) | Crouch |
| E | Use, pick up, get in a car, shift at a core |
| F | Shift anywhere (uses the charge; cores refill it) |
| Left click | Fire, use what you hold, throw; punch with empty hands |
| Right click | Aim a drawn weapon; kick with empty hands |
| R | Reload |
| G | Throw a grenade |
| 1 to 4 | Inventory slots |
| X | Holster or draw the weapon; put down what you hold |
| Z | Drop the weapon |
| Left Ctrl / Q (hold) | Dodge roll / block |
| V | First person on / off |
| Tab (hold) | Online: scores |
| F12 | Save a bug note with a screenshot |
| Esc | Pause (options: look speed, invert Y, volume, fullscreen) |
| H | All keys |

## How it was made

✍ **[HIS NAME / CREDIT LINE]** designed the game: the idea, the rooms and puzzles, the feel, and the call on what
shipped. The title lettering and the portal and orb drawings are his. The code was written with Claude Code
(Anthropic), an AI coding tool he directed and reviewed, and is disclosed as such. Most of the on-screen text (menus,
hints, prompts, labels) was written by the same tool ✍ **[name any you wrote or rewrote]**. A few small pieces were
made by scripts that tool wrote, not by an image, sound or 3D generator: the shift orb, the portal ring and the nitro
star models, a spark particle texture and the arms-carry pose ✍ **[drop any you replace]**. Every other model,
character, animation and sound comes from the licensed packs below.

Made in Unity 6 (6000.3, URP).

## Credits

- **3D art, characters, animation and most sound effects:** [ithappy](https://ithappystudios.com) asset packs
  (Playroom, Battle Stadium, Cartoon Weapons, Funny Characters, Modular Cyber Racing Cars, and the free Christmas,
  Cute Furniture and Food packs), used under their licence.
- **Interface sounds:** Case Portman. **Effects:** Polygon Arsenal (Archanor VFX).
- **3D lettering:** Modular 3D Text (Tiny Giant Studio). **Text effects:** Text Animator for Unity (Febucci).
- **Fonts:** Fredoka and Teko (SIL Open Font License).
- **Key icon:** [Kenney](https://kenney.nl) (CC0).
- **Tools:** Feel (More Mountains), Shapes (Freya Holmér), Linework Lite (Alexander Ameye), DOTween (Demigiant),
  Fraggo (Unluck Software), In-game Debug Console and Dynamic Panels (yasirkula), Unity's Netcode for GameObjects and
  Multiplayer Services, Unity Splines, TextMesh Pro.

## Opening the project

The paid asset packs and some tools are not in this repository: their licences allow them inside a shipped game but not
as files in a public repo. [THIRD_PARTY.md](THIRD_PARTY.md) lists what is missing and where each goes. Open the project
with Unity 6000.3.10f1; the course is `Assets/_Game/Scenes/Greybox.unity`.
