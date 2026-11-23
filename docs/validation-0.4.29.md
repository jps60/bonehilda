# 0.4.29 validation

Tested against installed Valheim 1.0.21 and Jotunn 2.30.2 on Windows.

## Saved-base navigation

An isolated copy of an actual saved base exercised the house entrance, closed gates, exterior corridor, two stair flights and a turning landing on the route to a windmill. All three saved maid tiers and the base's tame creatures remained present. No maid teleportation, collision disabling, enlarged interaction reach or replacement terrain was used.

The first controlled 300-second run stocked only the zero-star coffin with 50 barley and disabled repairs to isolate supply navigation. It recorded four accepted barley deliveries to the windmill, with return trips between deliveries, followed by collection of a fifth barley. Coffin stock plus carried and delivered items accounted for the original 50 barley.

The private headless harness registers its observer in Valheim's player list so native world time, machine processing and maid supply cooldowns advance normally. Earlier attempts with a frozen headless world clock were discarded as evidence of repeated-trip behavior. The harness and copied saves are private test tools and are not included in the mod.

A second run used 0.4.29 with repairs enabled, 45 barley already queued in the windmill and processing delayed to keep the input hopper occupied. It recorded three accepted barley deliveries, with return trips and collection between them, while the other maids and tame creatures remained active.

The original saved world remained unchanged: all 45 source-file SHA-256 hashes matched the pre-test manifest. Each run used a fresh private save copy and private plugins; Unity graphics preferences were restored after the probe.

The occupied-hopper run finished after 300 seconds with three deliveries, 46 barley in the coffin and one carried for the next trip, accounting for all 50 supplied barley.

## Build and compatibility

- Checked release build: zero compiler warnings or errors, 395 maintenance policy checks and seven invalid-package rejection checks.
- Package validation: matching 0.4.29 DLL, eight allowed files, Swiftfingers author metadata and a 256 × 256 icon.
- Isolated native startup probe: passed coffin recipes/inventory sizes, any-dress costs/UI, cloth binding/material checks, repair station coverage and sightlines, supply/door clearance, smooth steps, emotes, seating and skeleton voice checks.
- Installed API check: 648 references, reflection contracts and Harmony targets checked; none unresolved.

Static API and isolated client tests do not establish dedicated-server, Linux or two-client ownership-handoff compatibility. Those gameplay scenarios remain unverified.
