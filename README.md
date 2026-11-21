<img src="assets/icon.png" width="128" height="128" alt="Bonehilda, skeletal maid with a hammer and a green diamond">

# Bonehilda

A little **Makin' Magic** for your mead hall. A Valheim BepInEx/Jötunn mod that adds a buildable coffin and a skeletal maid who maintains your base. Leave the chores to Bonehilda; her free will is switched on, and she has no bladder bar to worry about.

**Version 0.4.1.** Built against Valheim **0.221.4** reference assemblies and checked against the installed **Valheim 1.0.21 / Jötunn 2.30.2** libraries. On flat ground in a local test world, all three maids exited their coffins, returned, and cleared the closing door. Multiplayer and obstructed return paths still need gameplay testing.

## Playing

Build a coffin with the hammer under **Furniture** or **Storage**, near a workbench. Each coffin summons one tame Bonehilda of its corresponding star level.

| Hammer piece | Bonehilda | Supply chest | Recipe | Prefab |
| --- | --- | --- | --- | --- |
| Bonehilda's coffin (0 stars) | 0 stars; red dress | 2 slots | 10 Wood, 5 Bone Fragments | `BH_Coffin_0Star` |
| Bonehilda's coffin (1 star) | 1 star; black dress | 4 slots | 20 Wood, 10 Bone Fragments, 1 Bronze | `BH_Coffin_1Star` |
| Bonehilda's coffin (2 stars) | 2 stars; blush floral dress | 8 slots | 20 Wood, 10 Bone Fragments, 2 Bronze | `BH_Coffin` |

Each coffin keeps a shallow side profile. The zero-star wooden case is 67 cm deep and the larger tiers scale with their maids. Inside the doorway, the skeleton, dress, and hammer ease together into a shallower pose and regain their full depth on exit. The six-sided coffin has a flat crown, broad shoulders, a tapered foot, and a withered-bone frame around a quilted lining and a hinged timber door. The doors carry linked lozenges, a three-lobed knot, and a four-loop weave in ascending tier order. Crooked, weathered bone rails frame both the front and the back. The recessed candles progress from a beeswax stub in iron to an ivory pillar in bronze and a twisted plum taper in a petalled cup. Each flame bends at the wick and its recessed light flickers with it. The hammer icons are renders of the three finished coffins. Leave clear space in front for Bonehilda to enter and return.

Bonehilda wears a fitted dress with puff sleeves, a square lace neckline, a rounded apron, and a lace cap worn behind the crown. The skirt follows her hips and thighs, the sleeves follow her arms, and the bow tails sway as she moves. The outfit follows Valheim's friendly skeleton and its existing animations. The base rig is 4% larger and retains the native 1.00, 1.10, and 1.20 star multipliers. A fuller bodice follows the ribs and grows slightly with each tier.

**Use [E] Dismiss to send her off duty.** She stops working, walks back, returns carried supplies, and walks into her coffin. While she is returning, [E] Recall cancels dismissal. Her hover status simply reads On duty or Off duty. **Open the coffin to summon her again.** She is already inside as the door swings open, then walks clear of it. Shift-use still renames her. Her health, custom name, and supply cooldown survive dismissal; dismissal does not heal her. The final approach accepts a wider staging area, steers directly near the entrance, and uses a shallow invisible threshold ramp. During entry and exit she passes through her own cabinet frame while its floor, ramp, terrain, and other structures remain solid. A blocked route can still prevent her from reaching the coffin, so she remains available to recall.

She walks to nearby damaged player-built structures and repairs them for free, like a hammer. Put supplies in the coffin and she collects one matching item, carries it to a compatible piece, and adds it. She checks the piece's actual accepted inputs and remaining capacity. Fuel deliveries leave no wasted partial slots, and she alternates a processor's fuel and input jobs.

She uses Valheim's **player animation controller**, retargeted onto her own skeleton avatar. For repairs she draws a visible hammer from her belt with the player's **R-key equip animation**, swings it, then holsters it again. Coffin transfers and supplying machines use the player's interaction animation. Repairs play native build/hammer sounds, supplies use the machine's native sound with a fallback for silent consumers, and the coffin latch sounds when the hinged door reaches its closed position. Her hammer is a visual tool and adds no inventory item or death loot. It preserves its physical size on the imported rig.

Each maid supplies **at most one item per configurable interval**, default **1 second**, shared across all her destinations. Walking, collecting one item per trip, scanning, and finishing the interaction animation can make deliveries slower. There is no catch-up burst after waiting. The accepted-delivery timestamp persists through save/load and network ownership changes. Repairs keep their separate `ActionInterval` setting.

| Build piece | Supplies from the coffin |
| --- | --- |
| Torches, fireplaces, hearths, braziers | Their configured fuel, such as resin or wood |
| Smelters and blast furnaces | Accepted ore/scrap and fuel, such as iron scrap and coal |
| Charcoal kilns | Accepted wood |
| Windmills | Barley |
| Spinning wheels | Flax |
| Eitr refineries | Sap and soft tissue, according to the machine's conversion/fuel configuration |
| Fermenters | Accepted mead bases, when the barrel is empty |
| Shield generators | Their accepted fuels |
| Ballistae | Accepted bolts, matching the ammo type already loaded |

Modded pieces using these same consumer components and accepted-item lists can also work. This is not an adapter for every arbitrary mod's custom machine.

Her work radius defaults to **30 meters from the coffin**, including vertical distance. She uses normal pathfinding and needs access to the coffin and each job. Closed doors, enclosed spaces, and high roofs can prevent access. Unreachable jobs time out. When idle, she walks within **10 meters of the coffin** and takes a feather duster from her belt to brush nearby player-built workbenches, closed chests, tables, chairs, and item stands. Dusting is cosmetic. She watches the surface, reaches across it, and turns the duster as she draws her elbow back. She bends her knees to reach lower furniture. The feathers flex on both passes and stir up occasional puffs of dust. Between chores she resumes walking, even when there is no furniture to dust. Repairs, supply runs, and dismissal interrupt her. She is a passive worker, can take damage, and returns after death with a configurable delay.

**Cooking stations, ovens, and obliterators never receive supplies.** Windmills and fermenters are included. She does not tap fermenters, collect production output, fire catapults, decorate item stands, or change ballista targeting filters. Repair supplies are not consumed, and the repair setting still applies to damaged player-built pieces.

New maids are named **Bonehilda**, with no random summoned-skeleton name. Existing random names are migrated where recognized, and player-set names are preserved. Her hover action shows Dismiss while on duty and Recall while off duty or returning; the alternate-use rename action is preserved.

## Installation

1. Install [BepInEx for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jötunn 2.30.2](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
2. Import `dist/Bonehilda-0.4.1.zip` as a local mod in r2modman, or extract it into the Valheim installation directory for a manual install. It contains **only `Bonehilda.dll`** under `BepInEx/plugins/Bonehilda/`, plus the package manifest, icon, README, and license. Update the profile you actually launch.
3. Install the same mod version and dependencies on **every client and the dedicated server**.
4. Launch once to generate `BepInEx/config/com.bonehilda.valheim.cfg`.

For an existing local r2modman installation, `scripts/install-r2modman.ps1 -ProfilePath <profile-directory>` updates the DLL, version, description, and icon together. It uses the latest `dist/package` built in this checkout, backs up the old files, and preserves whether the mod is enabled or disabled. Restart r2modman afterward to refresh its cached display.

For a manual install, copy only `src/Bonehilda/bin/Release/net462/Bonehilda.dll` to `BepInEx/plugins/Bonehilda/`. The other DLLs in the build folder are development references and must not be installed.

## Configuration

Jötunn synchronizes these admin-only settings from the server. Edit the generated configuration while the game/server is stopped, then restart. A compatible configuration manager can also expose them in game.

| Section | Setting | Default | Range / effect |
| --- | --- | --- | --- |
| Work | Radius | 30 | 5–100 meters from the coffin |
| Work | ScanInterval | 5 | 1–60 seconds between searches |
| Work | ActionInterval | 2 | 0.5–30 seconds minimum between repairs; animations also finish |
| Work | SupplyInterval | 1 | 0.5–60 seconds minimum between accepted items per maid |
| Work | JobTimeout | 30 | 5–120 seconds before skipping a job |
| Work | RepairStructures | true | Enable repairs |
| Work | RefuelStructures | true | Enable fuel delivery |
| Work | SupplyMachines | true | Enable machine inputs, fuel, and ammunition |
| Maid | IdleWander | true | Enable free will when there are no chores |
| Maid | WanderRadius | 10 | 2–50 meters, clamped within the work radius |
| Maid | RespawnDelay | 60 | 5–3600 seconds after the server detects a missing maid |

## Persistence and permissions

The coffin and maid store each other's network IDs. Only the server confirms that a missing creature or coffin has actually been destroyed; an unloaded sector does not count as a death. Detection can take approximately 10 seconds before the respawn delay starts. No work occurs in unloaded areas.

Carried supplies are serialized with their item data in the maid's ZDO. Cancelled deliveries return them to the coffin, or drop them beside her if the coffin is full. **If she dies carrying an item, it drops at her position** and is removed from her saved cargo. Coffin destruction also drops carried supplies. She pauses chest transfers while a player uses the coffin. She checks wards against the **coffin builder's** permissions, including on a dedicated server. Only player-placed pieces qualify for repairs and supply runs.

Existing `BH_Coffin` pieces become upright 2-star coffins without changing their network IDs. Old 24-slot contents are reflowed into the narrower grid and remain accessible in temporary overflow rows. Remove the excess stacks to return the chest to eight slots. New coffins start with exactly 2, 4, or 8 slots. Dismissed state is saved with the coffin, and returning state is saved with the maid.

Keep coffins on stationary ground with a clear exit. Ships and moving modded structures are not supported. Uninstalling custom-prefab mods can remove their pieces and creatures; empty the coffins and remove them before uninstalling.

## Building and checks

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). No game DLLs are committed or included in the package; NuGet provides pinned compile-time references. The source targets .NET Framework 4.6.2 for Valheim's Mono runtime.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
```

The script restores the dependency lockfile, builds the plugin, runs the checks, and writes `dist/Bonehilda-0.4.1.zip`. It derives the ZIP name and mod-manager version from the project version, verifies the assembly version matches, and includes the icon and description. Use `-Dotnet C:/path/to/dotnet.exe` for a private SDK. CI runs the same script.

```powershell
dotnet build src/Bonehilda/Bonehilda.csproj -c Release
dotnet run --project tests/Bonehilda.Checks/Bonehilda.Checks.csproj -c Release
```

To edit the models, open `assets/models/Bonehilda.blend`. The **Coffin workshop** scene contains the cabinet and moving door. The **Garment fitting** scene includes the poseable rig and the bodice shape key. The **Feather duster workshop** contains the handle and feather vanes. Run the following from the repository root after editing the art, then rebuild the DLL. Ordinary builds use the checked-in meshes, textures, and icons.

```sh
blender -b assets/models/Bonehilda.blend -P scripts/export-models.py
```

The automated policy checks exercise fuel/input capacity, ammo compatibility, rejected deliveries, fermenter item hashes, exclusions, repair eligibility, supply cooldown boundaries, and radius/wandering limits. An isolated startup probe can check materials, hammer registration, recipe references, native consumer handlers, and player animation playback on the skeleton avatar without entering a world. It does not exercise AI pathfinding, multiplayer ownership handoffs, or save/load serialization. Before using this in an established world, verify the following in a test world:

- Build all three tiers: each produces exactly one tame maid with 0/1/2 stars and 2/4/8 chest slots. Check the upright door's inner faces, opening sequence, cap, apron, sleeves, skirt, and skeleton palette while she walks.
- Damage player-built walls within and beyond the radius. Only reachable pieces within the radius should be repaired; generated ruins should be ignored.
- Watch a repair: the hammer should move from belt to hand during the equip motion, swing, then return to the belt. Supplies and coffin transfers should use the interaction gesture. Check this from a second client too.
- Listen for the hammer and machine supply sounds. Check that each coffin plays its latch when the door reaches the frame.
- Pet her during a delivery, pet again to cancel, then dismiss her all the way inside. Opening the coffin should bring her out with the same health/name. Repeat after save/load and while another client owns the area.
- Set `SupplyInterval` to 15 seconds with several nearby consumers. Accepted deliveries by a single maid must stay at least 15 seconds apart, including after save/load. Restore it to 1 for the default pacing.
- Put resin and wood in the coffin. A low resin torch must consume one resin per trip; a hearth must use wood. Full fires and incompatible resources must be untouched.
- Add iron scrap and coal: the smelter should receive both, up to its input/fuel limits. Repeat with flax, barley, mead bases, and the eitr refinery's materials. Full or busy consumers must not take another item.
- Add ballista bolts and shield fuel. Bolts must match any type already loaded, and trophy filters must remain unchanged. No resources should be delivered to cooking stations, ovens, or obliterators.
- Empty the task queue and place a chest or workbench nearby. She should walk over, dust it, holster the duster, and leave when a job appears. Check the default name and the revised outfit on a newly summoned maid.
- Block a job behind a closed door and put another outside. She must eventually skip the blocked job and work outside. A job on a different floor must not be serviced through that floor.
- Open the coffin during pickup/return. Verify the item count stays correct. Fill the chest before a cancelled delivery and verify the carried item drops.
- Kill her during a delivery, destroy her coffin, and save/reload while she carries fuel. Verify the resource is either carried, returned, consumed, or dropped exactly once.
- With two clients and a dedicated server, alternate opening the coffin and travel across sector boundaries. Check that inventory and ownership handoffs do not duplicate maids or supplies.
- Test a permitted ward and another player's unpermitted ward, then change the radius on the server and verify clients receive it.

The mod follows Jötunn's [custom creature](https://valheim-modding.github.io/Jotunn/tutorials/creatures.html) and [custom piece](https://valheim-modding.github.io/Jotunn/api/Jotunn.Entities.CustomPiece.html) APIs. The plugin embeds the original clothing, cabinet geometry, painted textures, and recipe icons.

The [mod artwork](assets/bonehilda-artwork.png) was made with the built-in imagegen tool; its full [generation prompt](assets/icon-prompt.txt) is included. Sims references are a fan tribute, with no affiliation implied.
