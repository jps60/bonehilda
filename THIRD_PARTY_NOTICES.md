# Credits and asset provenance

Bonehilda is maintained by **Swiftfingers**. Project source and original distributed artwork are provided under the repository's MIT license, subject to third-party rights described below.

- **Valheim / Iron Gate:** native skeletons, animations, shaders/material templates, equipment visuals, effects, and sounds are resolved from the user's installed game at runtime. The release ZIP contains no game assemblies, extracted native models, or sound files. Repository gameplay screenshots and recordings depict Valheim.
- **The Sims / EA / Maxis:** Bonehilda, Makin' Magic, and other references acknowledge the inspiration for this unofficial fan mod. The coffin and outfit meshes are original project assets, not redistributed Sims assets. No affiliation or endorsement is implied.
- **Jötunn**, **BepInEx**, and **HarmonyX:** external dependencies, not bundled inside the release DLL/ZIP as dependency assemblies. Their licenses remain with their respective projects: [Jötunn](https://github.com/Valheim-Modding/Jotunn), [BepInEx](https://github.com/BepInEx/BepInEx), [HarmonyX](https://github.com/BepInEx/HarmonyX).
- **Mono.Cecil:** MIT-licensed dependency used only by the optional developer compatibility checker; it is not shipped with the mod. [Source and license](https://github.com/jbevain/cecil).
- **Mod icon:** generated with the imagegen tool. The original image and generation prompt are in `assets/`; the release includes the 256×256 icon and prompt in `ARTWORK.txt`.

The editable Blender scene is `assets/models/Bonehilda.blend`; `scripts/export-models.py` exports the embedded original meshes, palettes, and build-menu icons. Building the mod does not require Blender or redistribution of Unity/game libraries.
