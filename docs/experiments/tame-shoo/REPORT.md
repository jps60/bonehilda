# Tame shoo experiment

**Branch: `experiment/tame-shoo`. Not merged into main.** No custom shoo animation or new audio is included. The normal r2modman profile stays on the stable build. This experiment does not change where the AI runs or keep areas loaded.

## Measured navigation results

Three repetitions per cell, 27 completed trials in total. Times below are medians in seconds; lower is better.

| Route | Current navigation | Bypass only | Bypass + shoo | Change with full experiment |
|---|---:|---:|---:|---:|
| Clear | 2.731 | 2.726 | 2.746 | +0.015 s (+0.5%) |
| Open space, one waiting wolf | 5.751 | 4.841 | 4.828 | 16.0% faster |
| Narrow passage, one waiting wolf | 5.756 | 5.764 | 3.633 | 36.9% faster |

Open-space trials chose one bypass each and made **zero shoo requests**. The wolf stayed in place; current navigation instead slowly pushed it forward. The bypass walked approximately **6.41 m rather than 4.00 m**, yet finished sooner because it avoided slow physical pushing.

All three full-experiment narrow trials issued and accepted exactly one shoo. Their passage times ranged from **3.632 to 3.654 seconds**, versus **5.753 to 5.792 seconds** for current navigation. The wolves subsequently finished approximately **3.71 m** of the requested 4 m walk, accounting for arrival tolerance. That later observation is excluded from the maid's passage time.

This fixture caused **slow progress, not deadlock**. Every mode completed every route within the 12-second limit; recorded stationary time was zero. These are navigation timings, not full delivery-job, frame-rate or CPU benchmarks. The clear-route difference is small, but these tests do not prove zero processing overhead.

Raw measurements: [results.csv](results.csv). Aggregates and ranges: [summary.json](summary.json). [Comparison chart](comparison.html).

## What she does

1. Inspect nearby tames along the next walking segment, at most four scans per second.
2. Try supported, body-clear waypoints around either side of the tame. Use the actual capsule's orientation and bounds; wolves have horizontal capsules.
3. Prefer a short bypass immediately. Otherwise observe sustained progress below 60% of the requested walking/running pace for 1.25 seconds. Price extra bypass distance at the maid's current pace against a 1.5-second pet-disruption penalty. A blocked or expensive bypass can justify a shoo.
4. Ask the tame's current owner to validate a short walk directly away from the maid. The owner computes the destination; the request does not provide an arbitrary destination or claim ownership.
5. Walk normally, with ordinary physics and native walking animations. Do not teleport, induce fear, change faction, replace player commands or pause the native combat update. New threats, player follow/riding commands, obstructions, ownership loss or an eight-second deadline cancel the temporary movement.

The owner rejects wild, dead, flying, swimming, sleeping, alerted, fighting, following or ridden creatures. Maids and players are excluded. The prototype targets ordinary `MonsterAI` tames such as wolves, boars and unridden lox; **only wolves were exercised in these movement trials**. `AnimalAI` creatures and custom non-capsule creatures are not supported for shooing.

Destinations must have continuous nearby floor support, body clearance, ward permission and remain within the coffin's work radius. Departure cannot cross a door/gate plane, including an open one. This prevents deliberately shooing pets through an enclosure entrance; it does not prevent all native wandering or physical pushing while a door is open. No new area loading or offline work is introduced.

There is a **20-second per-tame accepted-request cooldown**, stored in the ZDO, and a two-second request interval per maid. The pet resumes its ordinary behavior afterward and may return toward an existing stay/patrol area. Requests and accepted moves are distinct: `shooRequests` is not evidence that a pet actually moved.

## Settings and diagnostics

Under `[Experimental]`, server-synchronized settings are:

| Setting | Default | Purpose |
|---|---:|---|
| `TameCrowdEnabled` | true | Enable local avoidance. False restores the original navigation decisions. |
| `TameShooEnabled` | true | Enable shoo requests. False leaves bypasses enabled for comparison. |
| `TameCrowdDelay` | 1.25 s | Sustained slow-progress delay. |
| `TameShooDistance` | 4 m | Preferred destination, bounded to 3.5–5 m so arrival tolerance still yields roughly 3–5 m of movement. |
| `TameShooCooldown` | 20 s | Per-tame cooldown. |

Activity snapshots add `crowdBypasses`, `shooRequests`, `crowdBlockedSeconds` and `crowdState`. Actual owner acceptance logs start with `SHOO accepted`; accepted counts are also recorded on the tame for the test helper. No extra user-facing interaction text or animation is added.

## Method and safety checks

Valheim 1.0.21 / Jotunn 2.30.2 on Windows, in a private headless process using a fresh copied saved world, private plugins and private saves. The harness preserves/restores graphics preferences and registers its observer so native world time advances. No user's running game is stopped, and no second instance is launched beside one.

The controlled arena is an elevated temporary floor with an open variant and a **1.9 m wide corridor**. Each trial creates a fresh zero-star maid and, where applicable, one wolf, facing along a 4.5 m route. Timing ends within 0.5 m of the goal. The maid's normal chore selection is disabled and its real `Navigate` entry point is driven at the supply walking pace. The wolf is configured to wait; its native AI/character physics remain active. Collider disabling, forced positions during measurement and artificial mass changes are not used. Modes rotate order across repetitions with fixed trial seeds.

The baseline disables the new crowd feature in the same binary; it exercises the original navigation decisions but is not a separate FPS comparison against an unmodified DLL. Failed calibration runs and a test-helper access failure were excluded; the CSV contains only the complete final 27-trial run.

Each shoo trial also passed eight native checks: cooldown rejection, foreign requester rejection, wild/following/alerted exclusions, a solid departure obstruction, and closed/open door-plane exclusion. The preferred 4 m walk finished between 3 and 5 m in all three trials. Seven policy regressions cover the wait, cheap bypasses, costly/blocked bypasses, pace weighting and waypoint overshoot. Bypasses may not overrun the current waypoint; completing, abandoning or externally cancelling one invalidates the original cached path so she does not backtrack to an obsolete node.

The reproducible native helper is [TameCrowdRuntimeChecks.cs](../../../tests/TameCrowdRuntimeChecks.cs). Compile it into an **isolated test plugin only**, after native prefabs, a private player/world and the mod are ready, then call `Run(origin)` as a coroutine at a supported loaded location. It creates temporary actors/geometry and changes the private observer/configuration. **Never invoke it in a live player session.** It writes per-trial JSONL under that private plugin configuration directory. The private world loader and source saves are deliberately not distributed.

## Decision and remaining work

### Saved-base smoke comparison

Fresh private copies of the same saved base were also observed for 120 seconds per mode, with all three saved maid tiers and existing tames present. The zero-star coffin received 50 barley; the windmill began with 45 inputs and delayed processing. Repairs remained enabled. **Both current navigation and the full experiment completed one zero-star barley delivery.** The enabled run logged one accepted shoo across the loaded maids. [Saved-base counts](saved-base-check.json).

This is one short run per mode, with ordinary crowd motion and observer presence, not a controlled or repeated throughput benchmark. It does not establish a whole-base job-speed improvement or exclude regressions. Earlier exploratory base runs varied and are not used for the controlled percentage claims. The deliberate fixture results remain the primary measurements.

Checked build: zero compiler warnings/errors, 402 policy checks, seven version-tool checks, four reference-archive checks and nine invalid-package checks. Installed-library checking resolved all 667 API references, reflection contracts and Harmony targets. The package remains eight files with Swiftfingers metadata; native game/dependency files, private saves and probes are excluded.

The results support trying the feature, especially short bypasses. They do **not** establish a 16–37% improvement for general jobs or large bases. Before considering a merge, test multiple crowded pets, varying orientations and terrain, following/ridden creatures, multi-client ownership changes, door closing, returning pets and CPU cost. Conservative safety checks can reject a shoo even when the route looks usable. A larger real-base A/B sample is needed for a claim about delivery throughput.

Keep main and the stable profile unchanged while deciding. A future animation should be tied to an accepted owner action rather than treating every request as a successful shoo.
