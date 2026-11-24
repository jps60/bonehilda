# Next build queue

- [ ] Review the procedural animation drafts on `experiment/animation-drafts`: [guide](docs/animation-drafts/README.md), [schematic player](docs/animation-drafts/preview.html). Three shoo poses and three manual Kvastur sweep previews are playable; expanded dusting remains trajectory drafts. Keep wrist/palm, both-arm amplitude, skeleton sound, native Kvastur bristle/grip alignment, floor/overhead reach and every requested target type in the review queue. Main's automatic cleaning coverage is unchanged.

- [x] Allow hot-tub sitting only while the native fireplace is burning; leave if it stops.
- [x] Reproduce the multi-level windmill route in a private saved-base copy. Correct doorway floor sampling, bent/multiple-door routing and progress-based job timeouts without changing native interaction reach. See [0.4.29 validation](docs/validation-0.4.29.md).
- [ ] Split the generic “no reachable sightline” diagnostic into no suitable floor/standing clearance, blocked input sight/reach, and incomplete walking route. Capture candidate floor/standing points and blocking collider/door/path endpoint so a later session can identify the actual failed segment. Activity labels alone must not be reported as successful deliveries.
- [ ] Review long supply backoffs after meaningful door/floor/route-context changes. Preserve the three-retry limit and native player fueling reach; add a bounded, supported stair/landing waypoint fallback only if improved candidate selection and doorway probing still fail. Existing logs do not prove the precise landing is blocked.
- [x] Remove the global door-plane filter that rejected bent routes. Validate complete segments through doorways and retain close-behind safety.
- [x] Guard temporary dusting probes against unreadable render meshes and retain physical-collider fallback. Attribution of every historical mesh warning remains unverified.
- [x] Fix `Hud.SetupPieceInfo` overflow for six ingredients plus crafting station (0.4.37) and cache per-frame Any dress UI reflection (0.4.38). User confirmed the preview slowdown was resolved. BetterUI's separate missing `PlayerProfile.GetStat` method error is not attributed to this fix.

# Deferred animation integration

**Status: awaiting an animation PR.** These are requested future features, not capabilities of 0.4.28. The Blender-agent brief is [housekeeping-animation-handoff.prompt.txt](docs/housekeeping-animation-handoff.prompt.txt).

**Trigger:** the user merges the animation PR created from that brief. No PR number or merge SHA exists yet. Record both below when provided/verified, then use this checklist to plan runtime integration. Merging assets does not by itself complete any runtime task or certify native-game behavior.

- Animation PR: pending
- Merge commit: pending
- Integration release: pending

## Target coverage after merge

Retain existing reachable furniture dusting, player-placed-piece eligibility and the current exclusion of Bonehilda's own coffin. Add verified target mappings and suitable motions for:

- [ ] Vertical/wall-mounted item stands at chest height and just over head height.
- [ ] Horizontal item stands at a few relative heights.
- [ ] Displayed items themselves: choose strokes from their visible model's outer bounds/shape, rather than only dusting the stand's support. Cover long/narrow, broad/flat, irregular and bulky shapes.
- [ ] Armor stands, including equipped armor and displayed equipment.
- [ ] Pots.
- [ ] Lava lamps.
- [ ] “Aveskin skeletons” (user's spelling): verify exact decorative/buildable prefab and source mod; do not silently substitute another skeleton. An Asksvin-related name is an unconfirmed lookup lead.
- [ ] Wards.
- [ ] Adornments.
- [ ] Treasure chests; retain the closed/not-in-use requirement and avoid cosmetic looting/opening.
- [ ] Crystal walls specifically, using reachable vertical faces.
- [ ] Rugs swept using the actual **Kvastur model**, resolved from the installed game rather than a substitute broom or spawned gameplay/flight object.

## Asset acceptance before runtime integration

- [ ] Review the PR's editable Blender source, previews, original-asset provenance, export script/schema and baked data. Verify the merge rather than assuming a branch was merged.
- [ ] Confirm **3–5 reusable stand-dusting motion families**, adapted across orientation and low/tabletop, chest and just-overhead heights. Avoid one hard-coded clip per item; record the actual baked clip count and validated coverage.
- [ ] Verify skeleton/avatar and existing dress bindings, bone names, rest transforms, export coordinate conversion, prop physical scale and all three maid tiers.
- [ ] Resolve the requested broad display names to verified prefab/component mappings and native/mod versions. Treat unavailable optional targets as unavailable, without making their mods hard dependencies.
- [ ] Check clip metadata: IDs, durations, loop/contact phases, blend times, body/grip/prop tracks, target-local contact points/normals, stance offsets, reach limits and clearance/skip conditions.
- [ ] Ensure the animation path is actually loadable: the existing BHM1 exporter handles meshes, not animation playback. Raw Blender actions/FBX or previews alone are not runtime integration.
- [ ] Keep extracted native meshes/audio out of public source, Blender packing and release archives; runtime-resolve native references such as Kvastur.

## Geometry and playback integration

- [ ] Replace the top-surface-only assumption where needed. Use the displayed item's visible bounds/orientation and exposed surface/contour to select and fit a simple motion.
- [ ] Exclude mounting walls, helper meshes, hidden LODs and unrelated VFX from displayed-item shape bounds. Account for actual attachment rotations, scaling and equipped armor geometry.
- [ ] Use cheap bounds/silhouette selection plus limited surface/clearance probes or contact IK. Compute/cache usable geometry at selection/start, not by rebuilding all target meshes every animation frame.
- [ ] Keep the duster handle, arms and body outside solid items/walls; allow modest feather compression. Shorten/reposition/skip unsafe strokes rather than deeply clipping, stretching arms or jumping to unreachable mounts.
- [ ] Detect changed/removed displayed items and destroyed targets, then cancel/replan safely. Do not keep a stale sword trajectory after another object replaces it.
- [ ] Implement visual Kvastur equip/grips/sweeping/unequip, with stable bristle contact on the rug plane and strokes inside the clear rug footprint. Avoid nearby furniture and support skipping blocked rugs.
- [ ] Do not change displayed equipment, container contents, ward/lamp state, ownership, damage or buffs for cosmetic cleaning.
- [ ] Preserve native locomotion and existing matte materials. Restore posed bones, IK, foot offsets and tool state on cancellation, real work, death, dismissal, ownership transfer and unload.

## Scheduling and multiplayer integration

- [ ] Roll rug sweeping into **`IdleDeck.Dust`**, not a new idle category or independent cooldown.
- [ ] Allow mixed runs: dust → nearby rug sweep → nearby dust/sweep as suitable. Preserve the current **80%** continuation roll (`SittingContinueChance`) and **10 m** chain search from the maid (`DustingChainRadius`), bounded by the configured idle/work area.
- [ ] Keep full-area roaming by default (`WanderRadius = 0` follows `Work.Radius`, normally 30 m from the coffin). Preserve ward checks against the coffin builder, reservations and failed-target backoff.
- [ ] Consider gentle same-tool locality without starving either dusting or sweeping. Avoid immediate repeated work on the same target and use a shuffled variant selection compatible with the existing dust-pattern bag.
- [ ] Preserve current priorities: closing an opened door, dismissal/entry/exit and legitimate work can interrupt cosmetics; retain the existing Odin/critical-repair rules.
- [ ] Have the owning peer select variant/target/contact data and synchronize start time/phase. Avoid independent client choices, duplicate sounds/props and stale resumed poses; preserve or deliberately migrate existing dust keys.
- [ ] Add diagnostics for target prefab, displayed item, cleaning kind, variant/height/shape selection, clearance/skip reason and active tool.

## Validation and delivery

- [ ] Add meaningful selection, geometry, shared Dust-chain, interruption and export/metadata checks. Keep policy tests distinct from native placement, animation and multiplayer validation.
- [ ] Review actual game playback for all three tiers, both stand orientations, chest/overhead heights, varied model sizes, equipped armor, crystal faces, different rugs and obstructed targets.
- [ ] Verify repair/supply preemption, closing doors, cancellation, target replacement, save/reload and ownership handoff. Confirm visual tools never become inventory items or extra death drops.
- [ ] Follow AGENTS.md for the requested integration build: aligned version/docs, checked `scripts/build.ps1`, Swiftfingers metadata, install the existing profile preserving its state, commit/push, publish the matching ZIP and verify the public hash.
- [ ] Refresh Thunderstore/Nexus materials and label remaining untested coverage accurately. Do not advertise pending checklist items as implemented solely because an art PR was merged.

# Ideas under consideration

Optional user idea: an upper-body shoo gesture and skeleton noise while walking, encouraging crowded tames to move aside if normal rerouting fails. Consider only if tame crowding remains an issue. No tame-control behavior is included in 0.4.29.
