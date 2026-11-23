# Changelog

## 0.4.29

- Plan complete routes through up to two closed doors, considering entrances near both ends of the trip. Bent routes no longer fail because their destination lies on the starting side of a door's plane.
- Sample doorway floors at the doorway's height, compare actual walking lengths, and retain validated routes in the native AI path cache while following them.
- Treat `JobTimeout` as time without meaningful progress along a complete route, rather than the total duration of a trip. Door crossings and supply collection start a fresh progress baseline; circling still times out.
- Only select hot-tub seats while their native fire is burning, and leave if it stops.
- Skip unreadable render meshes when building temporary dusting probes, retaining the physical-collider fallback.
- Add regression checks for productive detours, stalled circling, and fresh outbound walks. Validate repeated barley deliveries in an isolated copy of an actual saved base.

## 0.4.28

- All coffin tiers accept one unequipped Hildir dress, across the ten vanilla styles, instead of requiring a tier-specific dress.
- Show one "Any dress" cost with quantity 1, the available dress's icon, and a tooltip identifying the next dress to consume. Selection follows inventory row/column order; worn dresses are protected.
- Consume the selected item instance once, while native building still checks/charges the other materials. A missing selected item rolls back the just-placed coffin before other costs are charged.
- Save the paid dress type on new coffins for native dismantling refunds. New free builds have no dress refund; legacy coffins keep their previous tier-specific refunds. Refunded dresses follow vanilla resource behavior, without preserving item customization/upgrades.
- Add 42 dress eligibility/refund checks and an isolated native-asset/UI integration helper. Startup testing confirmed all ten alternatives, equipped-item protection, knowledge-scope restoration, refund requirement substitution, the native cost label and quantity, and the selected icon. A full in-world placement/payment/refund cycle and multiplayer placement remain unverified.

## 0.4.27

- Validate saved home identity and reciprocal links before a coffin retires a maid, before she works at a loaded home, and before picking up, returning, or delivering supplies. Reused runtime IDs cannot authorize actions at another home.
- Keep a dismissed maid's coffin link until retirement successfully acquires ownership and issues destruction; failed ownership acquisition can retry without clearing her link prematurely.
- Add 18 identity/recovery policy checks, including foreign IDs, legacy distance boundaries, invalid saved positions, and ownership changes during orphan confirmation. These are policy checks, not a native inventory/save-file integration test.
- Extend activity diagnostics with local ownership, home-recovery waits, last-scan age/time, and counters for deferred jobs, reservations, wards, missing station coverage, and missing supplies.
- Reuse a stock-name lookup during each scan, avoid consumer enumeration for critical-only repair searches, reuse station eligibility within selection, and observe shared recent-task history once per frame. Performance changes have not been benchmarked in a live world.
- Automatically validate upload handoff versions, author, AI labels, file inventory, checksums, gallery captions, and Nexus BBCode during upload preparation and CI.

## 0.4.26

- Require an identical Bonehilda patch version on every client and server, preventing mixed saved-data and AI behavior between releases.
- Declare the minimum Jötunn version in the plugin dependency as well as package metadata.
- Prepare a concise mod listing, installation and removal instructions, server compatibility matrix, credits, and publishing guide.
- Validate the packaged manifest, icon, versions, ZIP contents, and plugin checksum during every build. Include the changelog and credits in the package.
- Add a reproducible installed-library check covering game API references, private consumer methods, and Harmony targets. This does not substitute for dedicated-server gameplay testing.

## 0.4.25

- Keep successful supply runs moving between deliveries while stock and capacity remain.
- Recover saved coffin/maid links using persistent identities instead of relying on world-load ZDO IDs.
- Clear stale seated poses and movement state when ownership is acquired.
- Prioritize a bow or kneel before Odin until he disappears, allowing reachable critical repairs to interrupt.

## 0.4.24

- Improve door crossing and swing-clearance handling before closing doors.
- Prefer sheltered idle destinations with outdoor fallback.
- Add native skeleton departure effects for explicit despawns while retaining carried-item drops.

Earlier development releases introduced three coffin tiers, fitted clothing, animated repair and supply work, coordinated queues, door use, terrain assistance, and idle routines. See GitHub releases for their individual notes.
