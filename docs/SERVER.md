# Server installation and compatibility

Bonehilda uses Jötunn's `ClientMustHaveMod` compatibility rule with **Patch** strictness: all three version components must match when installed on both sides. Install it and its dependencies on a supported dedicated server and every connecting client. Clients may also join servers without Bonehilda, including servers without Jötunn: her gameplay and coffin build pieces remain unavailable for that session, and a brief login notification fades automatically. Returning to local play or a supported server restores availability without restarting. Installed version mismatches still reject the connection with a mismatch message. Bonehilda functionality requires server support. [Jötunn compatibility documentation](https://valheim-modding.github.io/Jotunn/tutorials/networkcompatibility.html).

## Install and configure

1. Back up the server world, including `.db` and `.fwl` files, and retain a matching mod-profile export.
2. Install the Valheim BepInEx pack appropriate to the host OS and Jötunn 2.30.2 or newer. The generated manifest names the tested package dependencies. Follow the loader's host-specific installation instructions.
3. Extract the release ZIP's `BepInEx` directory into the server root. There should be one `BepInEx/plugins/Bonehilda/Bonehilda.dll`, with no duplicate older copy. Install the same release in client profiles.
4. Start the server through its BepInEx-enabled launcher. Confirm `Registered Bonehilda and three upright coffin tiers` in `BepInEx/LogOutput.log`, with no registration or Harmony errors.
5. Configure `BepInEx/config/com.bonehilda.valheim.cfg`. Restart after file edits. Work and maid settings carry `IsAdminOnly` and synchronize through Jötunn; server administrators are determined by Valheim's `adminlist.txt`. `ActivityLogging` is local to each process. [Jötunn configuration documentation](https://valheim-modding.github.io/Jotunn/tutorials/config.html).
6. Join with a matching client and run the acceptance checks below before using a valuable world.

Do not copy DLLs from the project's `bin` directory except Bonehilda.dll. The build ZIP contains only the plugin and listing/support files; dependencies are installed separately. No graphics-setting changes or additional ports are required by this mod.

## Authority and persistence

Only the peer owning a maid runs her work logic. Native Valheim ownership may migrate as players move between sectors. Work reservations and carried items are stored in ZDO data; transfers claim the relevant chest/consumer and invoke native owner handlers. The server authenticates link-check requests by ZDO owner, and only server-confirmed missing links trigger replacement or orphan cleanup. Persistent home identities repair reciprocal links after saved IDs change.

Wards are checked using the **coffin builder's** permission identity. Opening a coffin pauses its maid's chest transfers. No chores run against unloaded objects; this mod is not an offline simulation or an area loader. A dedicated server does not make a whole world active by itself.

## Validation status for 0.4.28

| Environment or check | Status |
| --- | --- |
| Release build and Unity-free maintenance policies | Passed: no build warnings; 392 policy checks |
| Package structure, metadata, icon, version alignment, archived DLL | Passed; seven malformed-package rejection scenarios also passed |
| Installed Windows Valheim 1.0.21, Jötunn 2.30.2 API and private adapter signatures | Passed: 647 API/adapter/Harmony target checks, zero unresolved |
| Historical local gameplay | Chores, coffin cycles, doors and idles exercised during development; not a complete 0.4.28 regression run |
| 0.4.28 isolated headless startup | Passed: registration, native dress alternatives and cost widget, plus existing geometry/animation checks |
| 0.4.28 native world placement/payment/refund cycle | Not yet exercised; startup UI/policy tests do not cover this transaction |
| Dedicated Windows or Linux server | Not runtime validated; no dedicated-server installation available in this workspace |
| Two-client ownership migration and config enforcement | Not gameplay validated |
| Crossplay / Xbox | Not validated; all clients must be able to load the required mods |
| Other gameplay mods | No general compatibility guarantee; test with the intended profile |

API resolution proves that referenced members exist, not that networking, native assets, animation state, or save recovery behave correctly at runtime. Until the checks below pass, describe multiplayer support as **experimental** in public listings.

The 0.4.28 recovery checks cover identity/reciprocal-link authorization and orphan-confirmation transitions. They do not serialize a native world or exercise native inventory transfer between real peers. The release adds guards before transfers and retirement, but does not make separate native ZDO updates into a crash-atomic transaction. Scan timing is observable in `bonehilda_status`; allocation reductions have not been benchmarked in live multiplayer.

## Multiplayer acceptance checks still required

- Start a disposable dedicated-server world and connect two clients. Confirm all three coffin tiers register. A client missing the mod or using an older patch must be rejected clearly.
- Set an obvious server-only radius and supply interval. Confirm both clients receive them and a non-admin cannot override them. Check a permitted ward and another builder's restricted ward.
- Build one coffin per tier with overlapping ranges. Supply coal, resin, flax, ore, and a fermenter base. Confirm accepted quantities decrease once in the source, increase once in the consumer, and do not exceed capacity or the configured delivery rate.
- Alternate clients opening a coffin while its maid carries an item. Walk away, disconnect the owning client, and cross sector boundaries. Confirm no duplicate maid, double delivery, lost cargo, or stuck animation after ownership transfers.
- Save and restart while maids are active, seated, returning, dismissed, and carrying supplies. Confirm home links recover, dismissed maids remain stored, and cargo is retained. Destroy a coffin and verify orphan cleanup drops cargo once.
- Use a closed door and gate in both directions. Confirm she clears the swing before closing, does not repeatedly push herself back, and refuses to open near enemies. She intentionally does not wait for other creatures to clear the doorway.
- Check native repair station coverage and line of sight, unreachable targets' three-retry limit, work interruption of idles, and Odin audiences interrupted by critical repairs.
- Restart the dedicated server with no clients, then join again. Inspect server and both client logs for RPC errors, null references, duplicate-prefab errors, and repeated orphan messages.

Record host OS, Valheim/loader/Jötunn versions, mod list, scenario, and result in a GitHub issue. This is the remaining release qualification work, not a claim that those scenarios have passed.
