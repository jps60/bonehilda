# Bonehilda

**Animation drafts for review.** This branch adds left/right/two-arm shoo gestures with skeleton rattles, plus three manual Kvastur sweep previews. The [review guide and motion catalog](https://github.com/jps60/bonehilda/blob/v0.4.40-experiment.1/docs/animation-drafts/README.md) lists uncertain poses, sound choices, tool calibration and stretch dusting trajectories. Automatic rug sweeping and new dusting targets are not enabled. Stable main remains on 0.4.39.

[Download draft 0.4.40](https://github.com/jps60/bonehilda/releases/download/v0.4.40-experiment.1/Bonehilda-0.4.40.zip).

**A little Makin' Magic for your mead hall.** Build a coffin, stock it with supplies, and let a tame skeletal maid handle the chores. Free will is enabled; a bladder bar is mercifully absent.

**Version 0.4.40 · Author: Swiftfingers · BepInEx / Jötunn · Work in progress**

![Bonehilda](https://raw.githubusercontent.com/jps60/bonehilda/v0.4.40-experiment.1/assets/icon.png)

## What she does

- Walks around tames when practical, then uses a moving shoo cone to clear a crowded route. Calm waiting tames choose safe retreats that avoid the maid, doors and unsupported ground. No custom shoo animation is included.
- Repairs player-built structures with a hammer, using player repair reach and the appropriate crafting station's coverage.
- Carries supplies from her own coffin to fires, torches, braziers, smelters, kilns, windmills, spinning wheels, refineries, fermenters, shield generators, and ballistae. Cooking stations and obliterators are excluded.
- Starts refueling below 25% capacity or an estimated two minutes of burn time, then continues until full. Delivers one item per trip, at most one per second by default.
- Coordinates work with other maids, uses permitted doors when enemies are absent, and closes doors she opens.
- Wanders, dusts, sits, admires trophies, and greets friendly creatures when work allows. She prefers sheltered resting places and pays her respects to Odin.
- Drops carried supplies on death. Her coffin summons a replacement after a configurable delay.

## Build and use

Find **Bonehilda's coffin** in the hammer's **Furniture** category (some build-menu mods place storage pieces under **Storage**). A workbench is required to build it. Leave clear, level space in front of its door.

| Coffin tier | Dress | Supply slots | Build materials |
| --- | --- | --- | --- |
| 0 stars | Red | 2 | 20 Fine Wood, 10 Bone Fragments, 2 Ectoplasm, 1 Skeleton Trophy, 1 Any Hildir Dress |
| 1 star | Black | 4 | 25 Ancient Bark, 10 Bone Fragments, 5 Withered Bones, 4 Ectoplasm, 1 Skeleton Trophy, 1 Any Hildir Dress |
| 2 stars | Blush floral | 8 | 30 Ancient Bark, 20 Bone Fragments, 10 Withered Bones, 8 Ectoplasm, 1 Skeleton Trophy, 1 Any Hildir Dress |

Each tier uses one unequipped dress sold by Hildir, of any of the ten vanilla styles. All tiers perform the same chores; their star level, appearance, and storage differ.

The hammer costs show a single **Any dress — 1** slot. Its icon follows the dress available in your inventory; hover it to see which dress will be used. The first eligible dress in inventory order (top row first, then left to right) is consumed. Equipped dresses are protected: unequip one if you want to use it. Dismantling returns the dress type paid, following normal building-resource refunds; this does not preserve custom item data or upgrades. Existing coffins retain their previous tier-specific dress refund.

Open the coffin and leave matching materials inside. She uses **her own coffin's inventory**, within **30 m of the coffin** by default. Interact with her using **[E] Dismiss** to send her off duty. **[E] Recall** cancels her return; opening the coffin recalls a resting maid. Shift-use renames her. Dismissal preserves health and carried supplies are returned or dropped if storage is full.

## Install

Requires **BepInExPack_Valheim 5.4.2360** and **Jötunn 2.30.2 or newer**. For a Bonehilda world, install the same version and dependencies on **every client and the server**. Vanilla clients cannot join a Bonehilda server. You can join servers without Bonehilda while leaving the mod enabled: her build pieces and gameplay are disabled for that session, with a brief fading login notice. A different installed server version still blocks joining with a mismatch message. Normal behavior returns in local or supported worlds without relaunching.

Download [the latest release ZIP](https://github.com/jps60/bonehilda/releases/latest). In r2modman, choose **Settings → Import local mod**, select the ZIP, and launch the profile with the mod enabled. For a manual installation, extract its `BepInEx` folder into the game/server directory after installing the dependencies. Restart the game/server after updating. Do not install development reference DLLs from a build folder.

The configuration file is `BepInEx/config/com.bonehilda.valheim.cfg`, generated on first launch. Gameplay settings are synchronized by Jötunn from the server; diagnostics remain local. Key options include `Radius`, `SupplyInterval`, `FuelLowFraction`, `FuelRunwaySeconds`, `IdleWander`, `DustingInterval` (all idle activities), `SocialVisits`, and `SittingContinueChance`. `Work.JobTimeout` limits time without meaningful progress along a complete walking route; long productive detours can take longer.

## Compatibility and saves

Static API checks target the installed **Valheim 1.0.21 / Jötunn 2.30.2** libraries; compilation uses **0.221.4** reference assemblies. Earlier local gameplay tests exercised the coffins, clothing, chores, and idle routines. **Dedicated-server gameplay, Linux hosting, and two-client ownership handoffs have not yet been validated.** Treat this as a testing release for multiplayer. See the [server guide and test matrix](https://github.com/jps60/bonehilda/blob/main/docs/SERVER.md).

Work occurs only where Valheim loads the relevant objects. Bonehilda does not keep areas loaded or provide offline production. Complex terrain, crowded doors, and obstructed interaction points can cause retries or deferred jobs. Mods that replace skeleton/player animations, inventories, pathfinding, or machine interactions may conflict; build-menu mods may move the coffins.

Back up worlds before installing or updating. Keep the mod installed while the world contains its coffins or maids. To uninstall, dismiss every maid, empty and dismantle every coffin in all explored locations, save, then remove the mod from server and clients together. Loading and saving a world without its custom-prefab mod can lose those objects.

Saved-base navigation checks are recorded in [0.4.29 validation](https://github.com/jps60/bonehilda/blob/main/docs/validation-0.4.29.md); session compatibility and login-notice checks are in [0.4.30 validation](https://github.com/jps60/bonehilda/blob/main/docs/validation-0.4.30.md).

## Help and development

Run `bonehilda_status` in the F5 console for a snapshot of loaded maids. Enable `Debugging.ActivityLogging` for local activity files in `BepInEx/config`; include relevant excerpts and `BepInEx/LogOutput.log` in a [bug report](https://github.com/jps60/bonehilda/issues). Review logs before sharing: diagnostics include positions and inventory contents.

Status reports include ownership, home-recovery waits, last work-scan time and age, and counts of pieces deferred by retries, reserved by another maid, restricted by wards, missing crafting station coverage, or lacking supplies. Counters describe the latest scan and may overlap; they do not mean that every piece was otherwise reachable. Remote-owner rows do not represent that owner's live queue.

See the [changelog](https://github.com/jps60/bonehilda/blob/main/CHANGELOG.md), [developer guide](https://github.com/jps60/bonehilda/blob/main/docs/DEVELOPMENT.md), and [publishing guide](https://github.com/jps60/bonehilda/blob/main/docs/RELEASING.md). Build with .NET 8: `./scripts/build.ps1`. It runs policy checks and validates the finished ZIP.

MIT licensed. Original meshes and textures are embedded in the plugin; Valheim models, animation controllers, and sounds are referenced from the installed game at runtime. The icon is AI-generated; see `ARTWORK.txt` in the ZIP or [its generation prompt](https://github.com/jps60/bonehilda/blob/main/assets/icon-prompt.txt). Bonehilda and Sims references are an unofficial fan tribute, with no affiliation to EA, Maxis, or Iron Gate. See [credits](https://github.com/jps60/bonehilda/blob/main/THIRD_PARTY_NOTICES.md).
