# Third-party assets not in this repository

This game uses paid asset packs by [ithappy](https://ithappystudios.com): Playroom, Battle Stadium,
Cartoon Weapons, Funny Characters and Modular Cyber Racing Cars. Their licence allows the assets
inside a shipped game but not redistribution of the files, so `Assets/_Imports/` is git-ignored.
To open the project, buy the packs and import them to `Assets/_Imports/ithappy/<Pack_Name>/`.

Also git-ignored for the same reason (Asset Store EULA / the author's own licence):

| Folder | Asset | Why it stays out |
| --- | --- | --- |
| `Assets/_Tools/Feel/` | Feel (More Mountains) | "You can't redistribute the uncompiled code" |
| `Assets/_Tools/Shapes/` | Shapes (Freya Holmer) | "You may not distribute the code in open-source projects" |
| `Assets/_Tools/FastScriptReload/` | Fast Script Reload (ImmersiveVRTools) | editor-only tool; not needed to open the project |
| `Packages/dev.ameye.linework-lite/` | Linework Lite / Free Outline (Alexander Ameye) | Asset Store EULA |

Unity Version Control (the `.plastic` workspace) carries the full tree including these folders.

Everything else in the repository is either original to this project or Unity's own template
content (TextMesh Pro essentials under the Unity Companion License).
