<img src="assets/icon.png" width="128" height="128" alt="Bonehilda, skeletal maid with a hammer and a green diamond">

# Bonehilda

A little **Makin' Magic** for your mead hall. A Valheim BepInEx/Jötunn mod that adds a buildable coffin and a skeletal maid who maintains your base. Leave the chores to Bonehilda; her free will is switched on, and she has no bladder bar to worry about.

**Version 0.4.23.** Built against Valheim **0.221.4** reference assemblies and checked against the installed **Valheim 1.0.21 / Jötunn 2.30.2** libraries. On flat ground in a local test world, all three maids exited their coffins, returned, and cleared the closing door. Multiplayer and obstructed return paths still need gameplay testing.

## Playing

Build a coffin with the hammer under **Furniture** or **Storage**, near a workbench. Each coffin summons one tame Bonehilda of its corresponding star level.

| Hammer piece | Bonehilda | Supply chest | Recipe | Prefab |
| --- | --- | --- | --- | --- |
| Bonehilda's coffin (0 stars) | 0 stars; red dress | 2 slots | 20 Fine Wood, 10 Bone Fragments, 2 Ectoplasm, 1 Skeleton Trophy, 1 Simple Undyed Dress | `BH_Coffin_0Star` |
| Bonehilda's coffin (1 star) | 1 star; black dress | 4 slots | 25 Ancient Bark, 10 Bone Fragments, 5 Withered Bones, 4 Ectoplasm, 1 Skeleton Trophy, 1 Plain Brown Dress | `BH_Coffin_1Star` |
| Bonehilda's coffin (2 stars) | 2 stars; blush floral dress | 8 slots | 30 Ancient Bark, 20 Bone Fragments, 10 Withered Bones, 8 Ectoplasm, 1 Skeleton Trophy, 1 Blue Dress with Beads | `BH_Coffin` |

Each tier consumes one dress from Hildir: undyed, plain, then beaded. Ectoplasm supplies the ghostly spark; no refined eitr or wisps are required. Existing coffins keep their tier and inventory when recipes change.

Each coffin keeps a shallow side profile. The zero-star wooden case is 67 cm deep and the larger tiers scale with their maids. Inside the doorway, the skeleton, dress, and hammer ease together into a shallower pose and regain their full depth on exit. The six-sided coffin has a flat crown, broad shoulders, a tapered foot, and a withered-bone frame around a quilted lining and a hinged timber door. The doors carry linked lozenges, a three-lobed knot, and a four-loop weave in ascending tier order. Crooked, weathered bone rails frame both the front and the back. The recessed candles progress from a beeswax stub in iron to an ivory pillar in bronze and a twisted plum taper in a petalled cup. Each flame bends at the wick and its recessed light flickers with it. The hammer icons are renders of the three finished coffins. Leave clear space in front for Bonehilda to enter and return.

Bonehilda wears a fitted dress with puff sleeves, a square lace neckline, a rounded apron, and a lace cap worn behind the crown. The skirt follows her hips and thighs, the sleeves follow her arms, and the bow tails sway as she moves. The outfit follows Valheim's friendly skeleton and its existing animations. The base rig is 4% larger and retains the native 1.00, 1.10, and 1.20 star multipliers. A fuller bodice follows the ribs and grows slightly with each tier.

**Use [E] Dismiss to send her off duty.** She stops working, walks back, returns carried supplies, and walks into her coffin. While she is returning, [E] Recall cancels dismissal. Her hover status simply reads On duty or Off duty. **Open the coffin to summon her again.** She is already inside as the door swings open, then walks clear of it. Shift-use still renames her. Her health, custom name, and supply cooldown survive dismissal; dismissal does not heal her. The final approach accepts a wider staging area, steers directly near the entrance, and uses a shallow invisible threshold ramp. During entry and exit she passes through her own cabinet frame while its floor, ramp, terrain, and other structures remain solid. A blocked route can still prevent her from reaching the coffin, so she remains available to recall.

She walks to nearby damaged player-built structures and repairs them for free, like a hammer. For pieces that require a crafting station, she must be inside the build radius of the appropriate station type (workbench, forge, stonecutter, and so on). This uses Valheim's native station coverage check at her position, including station range upgrades. Pieces with no station requirement follow the native rule and need none. The check runs before selecting a repair and again before committing it: uncovered damage does not interrupt dusting, trophy reactions, or sitting. Supply deliveries keep their existing rules. Repairs use the native player prefab's build/repair reach (**8 meters** in the installed game), measured from her eyes to the piece's collider surface with a clear line of sight. This also permits reachable overhead repairs; supply transfers use the native player interaction reach from her eyes (**3.5 meters** in the installed game), measured to the visible hit surface of the fuel/input interaction collider, including the native hover offset if present. Approach searches also include the native hover offset when setting their vertical floor-search bounds, so hanging fixtures do not lose eligible lower standing floors merely because they extend player interaction range. Fuel switches must be visible themselves: reaching another surface on the same machine does not permit fueling through its body. The player interaction collision layers are used, so intervening characters, vehicles, and objects can block a transfer. A stalled repair approach gets at most **three recovery retries**, including searches where no reachable standing spot exists, before the piece is skipped and she selects the next repair. Replanning does not replenish that job's budget. Unreachable pieces are deferred for at least 60 seconds after exhausting their recovery budget; other skipped pieces retain the normal temporary retry cooldown (at least 10 seconds), allowing later attempts if access changes. Put supplies in the coffin and she finds a reachable standing spot for the destination before collecting one matching item. She measures interaction reach from her eyes using the native player prefab range, allowing reachable elevated braziers without walking toward the hanging collider. By default, critical damage (25% health or less) comes first, then depleted fuel/ammunition. Ordinary chores start with supply priority and alternate repair/supply preference after three successful actions of the same type. This prevents an endless top-off or rain-repair loop from monopolizing her routine. After a repair exhausts its route-retry budget or times out, new repair searches pause for at least five seconds (or ScanInterval if longer), with an immediate search for eligible supplies. This gives machines with matching coffin stock a turn even when many damaged pieces are unreachable. Successful repairs do not trigger that pause. Disabling NaturalRoutines restores supply-first ordering. If a delivery fails, she first tries another compatible destination for the carried item before returning it to the coffin. Coffin access and destination approach use separate planners and retry budgets. Before taking an item, she rechecks the destination route from her actual pickup position; a rejected route leaves supplies in the coffin. Carried items take priority over cosmetic idles during destination searches. Failed delivery destinations are deferred for 60 seconds, doubling after repeated failures up to ten minutes; a successful delivery clears that failure history. She checks the piece's actual accepted inputs and remaining capacity. Fuel deliveries leave no wasted partial slots, and she alternates a processor's fuel and input jobs.

She **runs to accepted repairs**, slows to a walk for the final two meters, uses the native player walking speed for supply pickups and deliveries, and keeps her original slow pace for idle wandering and cosmetic visits. Door crossings, route backoffs, and coffin entry remain at a careful walking pace.

Repair and supply candidates are checked in the background while she wanders, dusts, visits trophies, or sits. Only work that she can perform from here or approach by a validated route interrupts an idle activity. A blocked search preserves her idle reservation and cooldown; real reachable work still takes priority.

For repairs, she plans a **standing spot with line of sight**, rather than asking the pathfinder to reach a wall surface or an elevated collider. She first checks visible parts of the piece from her current position, including alternatives to an obstructed nearest point. When movement is needed, she samples nearby floor positions around herself and the piece, rejects spots without body/head clearance or the required station coverage, and checks the walking route. Short walks up to 4.5 m on the same level can use a capsule sweep and repeated floor-support checks, without relying on the navmesh; gaps, large steps, steep support, walls, and insufficient headroom are rejected. Longer trips require a complete native path whose endpoint is on the requested floor. Among the first few viable choices she prefers the shorter walking route, with a small preference for staying on the current level. Planned spots keep a 0.4 m reach margin; the repair itself still uses the full native player reach. Searches are spread across frames. A failed or newly obstructed stance is avoided on the next attempt; all alternative stances share the existing three-retry budget. She takes short, supported final steps to avoid the native pathfinder stopping half a metre short. Live diagnostics include `repairApproach`, `repairStandingPoint`, `repairSightPoint`, `repairProbes`, actual eye position and repair reach, and counts of candidates rejected for space/permissions/station coverage, sight, or path.

Overlapping maids coordinate their jobs. Selecting a repair, supply run, dusting visit, trophy display, chair, or acquaintance reserves that destination during the approach and action. **One maid works on a piece at a time**; the others choose different eligible pieces or idle if no unclaimed jobs remain. Recently started destinations move behind untouched alternatives for 30 seconds, then use nearest-distance ordering again; if all candidates were visited recently, the oldest visit comes first. Real work can preempt a cosmetic visit. Claims release on completion, cancellation, death, dismissal, or ownership reset, and expire after 15 seconds without renewal. A blocked job still follows its existing timeout/retry budget. Claims are stored on the maid's network data; simultaneous peer selections use the same priority, start time, and ID ordering, then recheck the claim before committing. Multiplayer handoff behavior still needs gameplay validation.

She uses Valheim's **player animation controller**, retargeted onto her own skeleton avatar. For repairs she draws a visible hammer from her belt with the player's **R-key equip animation** and swings it. If more damaged pieces remain, she keeps the hammer drawn and immediately heads to the next eligible repair after the swing settles. Travel overlaps the repair cooldown; swings still respect ActionInterval. She holsters the hammer when no further repairs qualify, or when the run is cancelled by dismissal, an invalid target, or a timeout. Coffin transfers and supplying machines use the player's interaction animation. Repairs play native build/hammer sounds, supplies use the machine's native sound with a fallback for silent consumers, and the coffin latch sounds when the hinged door reaches its closed position. Her hammer is a visual tool and adds no inventory item or death loot. It preserves its physical size on the imported rig.

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

After completing a dusting action, she rolls SittingContinueChance (80% by default) to continue onto a different eligible piece within DustingChainRadius (10 m from her current position by default). Every continuation checks reachability, permissions, the idle/work area, and the shared destination reservation. She never immediately repeats the piece just dusted; if no different piece qualifies, the chain ends. Real work interrupts every link. The normal idle cooldown starts when the chain ends.

Idle destinations are checked for a complete approach before selection. Roaming samples the full disk instead of the native outer ring, checks supported floor positions and permissions inside the work area, finishes its current walk before rolling another destination, and uses the normal door/stuck recovery. Longer cosmetic approaches receive a distance-based time budget, capped at two minutes; work and dismissal still preempt them. Failed idle targets are deferred for a minute so they do not repeatedly consume the next activity roll. The default `DustingInterval` is now **8 seconds** (8–12 seconds with variation); existing config values remain in effect unless changed.

Her work radius defaults to **30 meters from the coffin**, including vertical distance. She uses normal pathfinding and needs access to the coffin and each job. She can open permitted player-built doors and gates along her route, cross, and close them before resuming any work, idle activity, or dismissal. If a closed door splits the navmesh, she can plan a supported crossing by joining complete walking paths to either side. Closed-door crossing checks use the maid body radius and the actual door thickness, with at least 0.75 m on either side, instead of requiring a long straight corridor based on gate width. Rotated box/mesh gates use local collider dimensions. Enemy checks and priority closing remain in effect. The fallback joins one closed doorway per planned route; several consecutive doors can still block a route. Planning leaves doors closed and colliders intact. She checks for enemies within **10 meters of herself or the door** before opening. Locked doors, doors that cannot close, and ward-restricted doors remain unavailable. Doors already open are left alone. She closes doors she opened even if another character or tame occupies the doorway. Failed crossings retreat and close on a bounded timer; an expiring door lease also triggers closure if she dies or unloads. Enclosed spaces and high roofs can still prevent access. Unreachable jobs time out. After **6 seconds** of attempted navigation without moving 25 cm, she checks safe back-off points about 2 meters away, clears the cached path, and retries from another angle. This back-off recovery applies to coffin entry/exit, supply destinations, and dusting. Repairs instead select another validated standing spot after a stalled or obstructed approach, with a one-second pause between failed searches. It uses walking and collision checks, including floor support; it does not teleport her or move supplies. A permanently enclosed or badly buried coffin can still be unreachable. After about 0.4 seconds without progress, small supported rises of **5–40 cm** use a short, smooth walking step with no jump animation. She tests nearby treads first, lifts over the riser, and continues onto the landing through collision-checked physics increments. Walking steps do not share the jump cooldown, so repeated stairs do not force a three-second wait. Ledges above 40 cm and up to 65 cm retain the jump animation, with height and forward speed calculated for the ledge rather than the full-strength jump. Both modes require permitted, supported landing space, body/head clearance, and the work radius. New obstructions abort a walking step. Larger walls and drops remain blocked.

When idle, she can walk throughout the **full work area** (30 m from the coffin by default), waiting **12–18 seconds after a walk** before selecting another destination, and occasionally takes a feather duster from her belt to brush nearby player-built workbenches, closed chests, tables, chairs, and item stands. Dusting is cosmetic and shares the **8–12 second idle activity cooldown** described below. Its cooldown survives save/load and dismissal; new maids begin with a short 3–4.5 second delay. She varies her routine between chores. She can sweep sideways before reaching forward, reach and retract before a quicker sideways sweep, or make two short wipes followed by a broad finishing pass. She uses all three before refilling the selection and avoids immediate repeats. Narrow furniture shortens the stroke. She bends her knees to reach lower furniture. The feathers flex on both passes and stir up occasional puffs of dust. Between chores she resumes walking, even when there is no furniture to dust. Repairs, supply runs, and dismissal interrupt her. She is a passive worker, can take damage, and returns after death with a configurable delay. Damage and nearby projectiles cannot leave her combat-alerted. Existing alert states and combat targets are cleared automatically, preventing endless running between idle destinations.

Idle activities share a shuffled deck of dusting, trophy visits, sitting, and NPC greetings. Sitting first tries a suitable, unoccupied chair. When no chair is usable nearby, she can **sit or relax on clear ground**, preferring a safe spot by a burning fire. Ground rests stay within the configured idle radius, avoid the coffin entrance, check slope, clearance, occupants, and approach paths, and reserve their fire/coffin anchor so overlapping maids coordinate. Ground sitting uses the native player sit loop or relaxed lying pose, preserving them until the same stay/get-up checks as chair sitting: every 10 seconds by default, with an 80% chance to stay. Work, dismissal, and lost reservations cancel the pose, with a short stand-up pause before travelling. Eligible activities take turns before the deck refills, with no immediate repeat when alternatives exist. The shared `DustingInterval` gives **8–12 seconds between performed activities** by default, measured after the animation ends or is interrupted. The setting keeps its original name for configuration compatibility and controls all four activities. Failed approaches, empty searches, and work interrupting an approach retry after **3–4.5 seconds**, rather than consuming a full activity cooldown. A saved cooldown is capped when the setting is lowered. Only actually attempted dusting visits influence the choice of the next piece; a lone valid piece can be reused. Trophy visits use the **full work radius** (30 m by default), while dusting, sitting, and normal wandering use `WanderRadius`: **0 by default means the full work radius**; positive values restrict them to a smaller area around the coffin. Existing positive config values are preserved; set WanderRadius to 0 to enable full-area idling. Ground sitting without a usable chair or fire searches near her current position, without returning to the coffin merely to rest. She stands about **1.6 m in front of the trophy's visible surface**, faces it, and performs one native player emote. Only permitted, player-built displays qualify; blocked or inaccessible displays are skipped.

| Mounted trophy | Possible player emotes |
| --- | --- |
| Eikthyr | Bow, cheer |
| The Elder | Bow, headbang |
| Bonemass | Cower, no-no-no |
| Moder | Bow, blow a kiss |
| Yagluth | Challenge, laugh |
| The Queen | Cower, challenge |
| Fader | Roar, flex |
| Brenna | Headbang, cheer |
| Geirrhafa | Cower, bow |
| Zil and Thungr | Challenge, cheer |
| Other installed bosses with trophy drops | Bow, challenge fallback |
| Skeleton / rancid remains | Point, wave / point, no-no-no |
| Ghost / wraith | Wave, bow / cower, point |
| Draugr / elite draugr | No-no-no, shrug / challenge, shrug |
| Troll | Laugh, flex |

NPC visits search a separate **10 m radius around her current position** when choosing a visit. Both the NPC and her standing spot must still be inside the work radius around her coffin. The social search radius remains independently configurable; it does not move the home boundary. She finds a supported, permitted standing spot with line of sight, pauses to face her acquaintance, and performs a short player emote. She stands at least 1.6 m away (farther for larger tames), or 4 m from Odin. Small movements are tolerated; moving more than a meter ends the visit rather than starting a chase. Nearby enemies, death, a busy/alerted/sleeping target, work, or dismissal interrupt the visit. Other maids may greet her while she is idle, but are left alone while working, carrying supplies, or returning to their coffin. Acquaintances are reserved to prevent several visitors crowding one creature. Each maid waits **90 seconds** before greeting the same acquaintance again. This is cosmetic: she never pets, commands, feeds, recruits, changes ownership of, or animates another NPC. Odin keeps his native disappearance rules.

| Acquaintance | Possible player emotes |
| --- | --- |
| Friendly summoned skeleton / skelett (including matching skelette variants) | Wave, point |
| Calm tamed creature | Blow a kiss, love-you gesture |
| Another idle Bonehilda | Wave, bow |
| Odin | Bow, kneel |

She varies her gestures instead of immediately repeating the same one. With `NaturalRoutines`, she briefly looks toward her next idle destination before walking, finishes her short gesture before choosing another idle action, and occasionally favors a familiar chair or safe ground resting spot. Favorite spots are still checked for occupancy, safety, permissions, range, reservations, and a walking route; blocked favorites do not prevent other choices. Real work, danger during a social visit, and dismissal preempt gestures. Favorite resting spots, ordinary work-bout progress, the most recent acquaintance cooldown, and voice cooldown survive dismissal/recall.

Social/trophy gestures may play native skeleton vocalizations. Dusting, sitting, bowing, and kneeling use lower chances. The default chance is 55%, with at least 12 seconds between voices per maid. Only the maid's owner creates the native networked sound effect; the inherited random ambient skeleton timer is suppressed for maids. Hammer and machine sounds keep their existing behavior. Free will, a social life, and still no bladder bar.

She occasionally sits in an unoccupied nearby chair using its native seated pose. Every **10 seconds**, she rolls **80% to stay seated / 20% to get up**. Both values are configurable. Repairs, supplies, carried items, and dismissal interrupt her break; normal work scans still run while she sits or admires trophies (within the configured `ScanInterval`, default 5 seconds). Seats are reserved while occupied, released when she stands or dies, and protected by an expiring network lease if she unloads. Ships are excluded. Free will has its limits: the chores still come first.

**Cooking stations, ovens, and obliterators never receive supplies.** Windmills and fermenters are included. She does not tap fermenters, collect production output, fire catapults, decorate item stands, or change ballista targeting filters. Repair supplies are not consumed, and the repair setting still applies to damaged player-built pieces.

New maids are named **Bonehilda**, with no random summoned-skeleton name. Existing random names are migrated where recognized, and player-set names are preserved. Her hover action shows Dismiss while on duty and Recall while off duty or returning; the alternate-use rename action is preserved.

Clothing and the feather duster share Hildir's native clothing shader with a matte material, retaining their authored atlas and meshes. The duster uses the zero-star palette and no longer inherits the chest's building shader or rain/noise sheen. It disables inherited rain/snow sheen, value noise, triplanar mapping, metallic/gloss-map variants, and uses an albedo multiplier of 0.8. The cabinet keeps its own material. These controls are documented in [Jötunn's shader list](https://valheim-modding.github.io/Jotunn/data/prefabs/shader-list.html) and [material keyword list](https://valheim-modding.github.io/Jotunn/data/prefabs/material-list.html); [Unity's shading guidance](https://docs.unity.cn/2019.1/Documentation/Manual/BestPracticeMakingBelievableVisuals5.html) explains the effect of overly bright albedo. This mitigates clothing shimmer; firelight validation remains necessary. Unity also documents [flicker caused by real-time light limits](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/ts-lights-flicker-disappear), which a material adjustment cannot universally fix. The mod does not change global graphics settings.

## Installation

1. Install [BepInEx for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jötunn 2.30.2](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
2. Download the ZIP from the [latest GitHub release](https://github.com/jps60/bonehilda/releases/latest), then import it as a local mod in r2modman, or extract it into the Valheim installation directory for a manual install. It contains **only `Bonehilda.dll`** under `BepInEx/plugins/Bonehilda/`, plus the package manifest, icon, README, and license. Update the profile you actually launch.
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
| Maid | WanderRadius | 0 | 0 follows the full work radius; 1–100 m restricts idle activities around the coffin, capped by work radius |
| Maid | WanderInterval | 12 | 5–120 seconds between idle destinations; randomized up to 50% longer |
| Maid | DustingChainRadius | 10 | 1–50 m around her current position to find another piece in a dusting chain; clipped to idle/work area |
| Maid | DustingInterval | 8 | 3–600 seconds between performed idle activities; randomized up to 50% longer. Failed approaches retry after 3–4.5 seconds |
| Maid | TrophyReactions | true | Include mounted trophy reactions within the full work radius |
| Maid | TrophyInteractionDistance | 1.6 | 1–2.5 meters in front of the visible trophy surface |
| Maid | IdleSitting | true | Use nearby chairs, or sit/relax on clear ground near a fire when none is suitable |
| Maid | SocialVisits | true | Include cosmetic NPC greetings in the idle deck |
| Maid | SocialRadius | 10 | 2–50 m around her position at search time; NPC and standing spot remain inside the coffin work area |
| Maid | SocialTargetCooldown | 90 | 15–600 seconds between greetings to the same acquaintance per maid |
| Maid | NaturalRoutines | true | Idle destination glances, familiar resting spots, and short work bouts |
| Maid | IdleVoiceChance | 0.55 | Chance of a native skeleton sound at a gesture; dusting/rest use lower chances; 0 disables |
| Maid | IdleVoiceCooldown | 12 | 5–120 seconds between animation-linked skeleton noises per maid |
| Maid | SittingCheckInterval | 10 | 5–120 seconds between rolls to get up |
| Maid | SittingContinueChance | 0.8 | 80% chance to stay seated at each check or continue a dusting chain |
| Work | NavigationStuckTime | 6 | 2–30 seconds of attempted movement without progress before retrying |
| Maid | UseDoors | true | Open permitted player-built doors/gates and close them before continuing |
| Maid | DoorEnemyCheckRadius | 10 | 5-50 meters around the maid and door; enemies prevent opening |
| Debugging | ActivityLogging | false | Local activity snapshot/history every two seconds; not synchronized |
| Maid | RespawnDelay | 60 | 5–3600 seconds after the server detects a missing maid |

For live diagnosis, enable `[Debugging] ActivityLogging = true`. The local profile writes `BepInEx/config/Bonehilda.activity.json` and a bounded `Bonehilda.activity.log`. Version 0.4.11 fixes an export bug where the file contained a timestamp but omitted the maid array; empty areas now explicitly contain `maids: []`. These contain each loaded maid's job, target, distance, reach/station eligibility, cargo, path nodes, recovery count, idle activity, door stage, blocked-door reason, planned doorway route, opening/closing counters, supply standing spot/retry count, effective supply reach including native offsets and supply probe/rejection counts, cached coffin stock, accepted delivery count and last item/destination, repair-search delay, walking-step and ledge-jump counts, active smooth stepping, ground resting, task reservation, current animator clips, dusting pose, and chair attachment. The console command `bonehilda_status` also captures a snapshot immediately. Non-owner clients cannot inspect another peer's private job queue; inspect the owning client or server. Loading this feature requires restarting from an older mod version.

## Persistence and permissions

The coffin and maid store each other's network IDs. Only the server confirms that a missing creature or coffin has actually been destroyed; an unloaded sector does not count as a death. Detection can take approximately 10 seconds before the respawn delay starts. No work occurs in unloaded areas. Maids validate their reciprocal coffin link every 10 seconds, including legacy/unbound maids with no home ID. After a 10-second startup/ownership grace, a broken link must be observed twice by the server, at least five seconds apart. Normal orphan cleanup therefore takes about 20 seconds after loading. A missing scene object alone never counts as an orphan: valid unloaded network data keeps the maid alive. Missing/destroyed coffins, invalid home references, and duplicate maids whose coffin points to another maid are cleaned up by despawning the orphan and dropping its carried item. Coffins also check the reverse link before scheduling a replacement. A link repaired before the confirmation arrives is preserved.

Carried supplies are serialized with their item data in the maid's ZDO. Cancelled deliveries return them to the coffin, or drop them beside her if the coffin is full. **If she dies carrying an item, it drops at her position** and is removed from her saved cargo. Coffin destruction also drops carried supplies. She pauses chest transfers while a player uses the coffin. She checks wards against the **coffin builder's** permissions, including on a dedicated server. Only player-placed pieces qualify for repairs and supply runs.

Existing `BH_Coffin` pieces become upright 2-star coffins without changing their network IDs. Old 24-slot contents are reflowed into the narrower grid and remain accessible in temporary overflow rows. Remove the excess stacks to return the chest to eight slots. New coffins start with exactly 2, 4, or 8 slots. Dismissed state is saved with the coffin, and returning state is saved with the maid.

Keep coffins on stationary ground with a clear exit. Ships and moving modded structures are not supported. Uninstalling custom-prefab mods can remove their pieces and creatures; empty the coffins and remove them before uninstalling.

## Building and checks

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). No game DLLs are committed or included in the package; NuGet provides pinned compile-time references. The source targets .NET Framework 4.6.2 for Valheim's Mono runtime.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
```

The script restores the dependency lockfile, builds the plugin, runs the checks, and writes `dist/Bonehilda-0.4.23.zip`. It derives the ZIP name and mod-manager version from the project version, verifies the assembly version matches, and includes the icon and description. Use `-Dotnet C:/path/to/dotnet.exe` for a private SDK. CI runs the same script.

Every requested build is delivered by committing and pushing the completed source and publishing the matching ZIP on GitHub Releases. After pushing, `scripts/publish-release.ps1` derives the version from the project, uploads the ZIP, and verifies its public download by SHA-256. It uses the existing Git Credential Manager login and accepts an optional `-NotesPath` file for release notes.

```powershell
dotnet build src/Bonehilda/Bonehilda.csproj -c Release
dotnet run --project tests/Bonehilda.Checks/Bonehilda.Checks.csproj -c Release
```

To edit the models, open `assets/models/Bonehilda.blend`. The **Coffin workshop** scene contains the cabinet and moving door. The **Garment fitting** scene includes the poseable rig and the bodice shape key. The **Feather duster workshop** contains the handle and feather vanes. Run the following from the repository root after editing the art, then rebuild the DLL. Ordinary builds use the checked-in meshes, textures, and icons.

```sh
blender -b assets/models/Bonehilda.blend -P scripts/export-models.py
```

The automated policy checks exercise fuel/input capacity, ammo compatibility, rejected deliveries, fermenter item hashes, exclusions, repair eligibility, supply cooldown boundaries, radius/wandering limits, coffin recipe progression, shuffled idle choices, trophy mappings, sitting roll boundaries, short retries for failed idle visits, and saved cooldown changes. An isolated startup probe can check materials, hammer registration, recipe references, native consumer handlers, and player animation playback on the skeleton avatar without entering a world. It does not exercise AI pathfinding, multiplayer ownership handoffs, or save/load serialization. Before using this in an established world, verify the following in a test world:

- Build all three tiers: each produces exactly one tame maid with 0/1/2 stars and 2/4/8 chest slots. Check the upright door's inner faces, opening sequence, cap, apron, sleeves, skirt, and skeleton palette while she walks.
- Damage player-built walls within and beyond the radius. Only reachable pieces within the radius should be repaired; generated ruins should be ignored.
- Damage several pieces: she should draw the hammer once, repair them in sequence, and keep it in hand while walking between them. She should holster it after the last repair or when dismissed mid-route. Repair swings must still respect `ActionInterval`. Supplies and coffin transfers should use the interaction gesture. Check this from a second client too.
- Listen for the hammer and machine supply sounds. Check that each coffin plays its latch when the door reaches the frame.
- Pet her during a delivery, pet again to cancel, then dismiss her all the way inside. Opening the coffin should bring her out with the same health/name. Repeat after save/load and while another client owns the area.
- Set `SupplyInterval` to 15 seconds with several nearby consumers. Accepted deliveries by a single maid must stay at least 15 seconds apart, including after save/load. Restore it to 1 for the default pacing.
- Put resin and wood in the coffin. A low resin torch must consume one resin per trip; a hearth must use wood. Full fires and incompatible resources must be untouched.
- Add iron scrap and coal: the smelter should receive both, up to its input/fuel limits. Repeat with flax, barley, mead bases, and the eitr refinery's materials. Full or busy consumers must not take another item.
- Add ballista bolts and shield fuel. Bolts must match any type already loaded, and trophy filters must remain unchanged. No resources should be delivered to cooking stations, ovens, or obliterators.
- Empty the task queue and place a chest or workbench nearby. She should walk over, dust it, holster the duster, and leave when a job appears. Check the default name and the revised outfit on a newly summoned maid.
- Mount boss and skeleton trophies beyond 5 m but within the work radius. She should stand close in front, face the display, and use its mapped emote. Block the display or remove its trophy during a visit; she should stop without emoting through walls.
- Place unoccupied chairs and thrones throughout the configured idle area. Check seated alignment and the 10-second stay/leave rolls. Add work while she sits, dismiss her, destroy the chair, and transfer ownership between clients; the pose and seat reservation should clear. A player or another maid must not occupy the same seat.
- Block a job behind a closed door and put another outside. She must eventually skip the blocked job and work outside. A job on a different floor must not be serviced through that floor.
- Open the coffin during pickup/return. Verify the item count stays correct. Fill the chest before a cancelled delivery and verify the carried item drops.
- Kill her during a delivery, destroy her coffin, and save/reload while she carries fuel. Verify the resource is either carried, returned, consumed, or dropped exactly once.
- With two clients and a dedicated server, alternate opening the coffin and travel across sector boundaries. Check that inventory and ownership handoffs do not duplicate maids or supplies.
- Test a permitted ward and another player's unpermitted ward, then change the radius on the server and verify clients receive it.

The mod follows Jötunn's [custom creature](https://valheim-modding.github.io/Jotunn/tutorials/creatures.html) and [custom piece](https://valheim-modding.github.io/Jotunn/api/Jotunn.Entities.CustomPiece.html) APIs. The plugin embeds the original clothing, cabinet geometry, painted textures, and recipe icons.

The [mod artwork](assets/bonehilda-artwork.png) was made with the built-in imagegen tool; its full [generation prompt](assets/icon-prompt.txt) is included. Sims references are a fan tribute, with no affiliation implied.
