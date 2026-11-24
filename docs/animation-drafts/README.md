# Animation drafts for review

This draft lives on `experiment/animation-drafts`. Stable main remains on 0.4.39. No animation or sound here is considered visually approved simply because a structural check passed.

Revision 0.4.41 follows [the original housekeeping development chain](DEVELOPMENT-CHAIN.md): authored runtime beats, fitted grip/contact IK and native two-angle captures. Open [native-review.html](native-review.html) for recordings of the actual dressed skeleton and Kvastur; the schematic below remains a planning aid. [Capture notes](CAPTURE-NOTES.md) give measurements and outstanding review items, including the fan's 2.8 cm maximum palm-fit residual. The capture samples the native walking controller on a stationary private stage, not live navigation. Tests are muted; audio still needs normal-play review.

## Shoo gestures: playable in game

Three procedural poses layer over the native stride: left-hand, right-hand, and broader two-hand “go on” gestures. Authored beats gather the hand, guide away, relax, offer a smaller second invitation, then recover; the right-hand timing is slightly quicker. They last 2.4 seconds with a soft entry/exit, touch only upper arms, forearms and wrists, and preserve hips, feet, locomotion parameters and speed. The owner chooses the variant and synchronizes its start time. A drawn hammer reserves the right hand, so that case uses the left-hand gesture. Repairs, supplying, seated/resting poses, other emotes, dismissal and coffin passage interrupt the gesture; owner cancellation prevents stale resumption.

The existing native `sfx_skeleton_basic_verse_idle` effect supplies the rattle, emitted only by the owner and limited to once per eight seconds. Set `Animations.ShooGestures` or `Animations.ShooGestureSounds` false to disable either part. **Review requested:** wrist/palm orientation, two-hand amplitude, friendly versus overly stern delivery, and the sound's pitch/timing. The native attack/alert verse is earmarked as an alternative for review; it is not selected by default.

Console reviews, within 8 m of an owned maid:

```
bonehilda_shoo left
bonehilda_shoo right
bonehilda_shoo both
```

Allow her to finish an interaction first. To review right/both gestures, let her stow the hammer. These commands do not redirect her path or stop walking.

## Rug sweeping: manual playable drafts

Three review motions use a **renderer-only copy of native `BogWitchKvastur` geometry**, resolved at runtime. No Kvastur creature, AI, network view, attack, physics, riding, loot or inventory item is spawned. Native meshes/audio are not copied into the repository or package. The trophy `TrophyKvastur` is not substituted for the broom.

Stand a free owned maid near a player-built rug, then use:

```
bonehilda_sweep long
bonehilda_sweep gather
bonehilda_sweep fan
bonehilda_sweep long flip
```

The five-second review holds only idle movement; legitimate work and dismissal can interrupt it. A conservative handle obstruction check can stop the review. The three paths are long draw, short gathering sweeps and diagonal fan, with a slower working stroke and lifted return. The native mesh is scaled to the maid's skeleton lengths, including star-tier size. Palm and finger fitting follows measured handle cross sections instead of putting wrists on a bounding-box centreline; a small upper-body lean and downward glance follow the work. **Review requested:** broom/maid proportions, both hand grips, fingers, dress/handle clearance, rugs at different angles, and nearby furniture. The arm solver clamps reach rather than stretching bones; residual grip errors are recorded, not treated as visually approved. These prototypes do not yet join automatic dusting runs or establish production-quality rug sweeping.

## Dusting stretch set: trajectory drafts

Open [preview.html](preview.html) for a local, self-contained schematic player with time scrubbing, views, motion/height/shape selection and a walking shoo sketch. Its broom is a proxy; the in-game sweep command uses Kvastur geometry. The cleaning hands in the schematic are not an IK or dress-clipping validation.

[motions.json](motions.json) contains three sampled shoo poses, three sweep paths, and 150 dusting trajectory examples generated from **five reusable families**, six mounts and five bounds profiles. These are task-space contact trajectories, not 150 separately authored skeletal clips. The shoo samples come from the actual compiled `ShooMotion` sampler. Regenerate with `generate.ps1` after building; run `check.ps1` for basic geometric/time checks.

| Dust family | Intended use | Review needs |
|---|---|---|
| Short flicks | Small, narrow or fragile displays | Avoid whipping the wrist; keep handle outside object |
| Broad sweep | Broad/flat horizontal displays | Stroke width and feather compression |
| Rim follow | Pots, round objects, edges and bulky outlines | Actual rim/silhouette rather than a box approximation |
| Vertical wipe | Wall stands, tall decorations, crystal faces | Wall clearance, approach side, contact normal |
| Overhead arc | High stands and taller outlines | Reach, dress shoulder binding, small-body limits |

Mount examples: vertical chest and just-overhead; horizontal floor, low table, table and head height. Bounds examples: long/narrow, broad/flat, round, irregular and bulky. Paths remain outside conservative item bounding boxes; this trades some floating for avoiding penetration. **Review requested:** floor-level crouch/body support, overhead reach, real sword/shield/armor silhouettes, duster handle/arm clearance, and shortened strokes around tight displays. None of this expands automatic dusting eligibility yet.

## Target review queue

| Requested targets | Proposed families | Status |
|---|---|---|
| Vertical item stands, chest/overhead | Short flicks, vertical wipe, overhead arc | Height/shape drafts; attachment bounds and wall clearance pending |
| Horizontal stands, four heights | Short flicks, broad sweep, rim follow | Drafts; floor stance and real item geometry pending |
| Armor stands and equipment | Rim follow, vertical wipe | Native `ArmorStand`, `ArmorStand_Female`, `ArmorStand_Male` found; equipment bounds and exposed faces pending |
| Pots | Rim follow, short flicks | Must clean rims/exteriors without traversing the opening |
| Lava lamps | Rim follow, vertical wipe | Native candidate `piece_Lavalantern` (Lava Lantern) found; confirm requested identity and glass/contents clearance |
| “Aveskin skeletons” | Short flicks, rim follow, vertical wipe | Native candidate `piece_asksvinskeleton` (Asksvin Skeleton) found; confirm requested identity rather than silently substituting |
| Wards | Rim follow, vertical wipe | Exposed faces only; no activation/ownership changes |
| Adornments | Short flicks, rim follow | Verify exact prefabs and silhouette |
| Treasure chests | Broad sweep, rim follow | Closed/not in use; no opening or looting |
| Crystal walls | Vertical wipe, overhead arc | Reachable face and safe approach side |
| Rugs / Kvastur | Long draw, short gather, diagonal fan | Manual review draft; native grip/contact calibration pending |

All uncertain combinations are retained for review. Future automatic cleaning must preserve work priorities, builder wards, target reservations, the shared dusting category, 80% chain continuation and 10 m chain range. Augie's original PR #3 is already part of the baseline. The separate future asset PR proposed by the expanded-housekeeping handoff has not been created; its trigger and production target integration remain tracked in TODO.md.

## Validation limits

The checked build passes 426 policy checks. Private native checks verify actual skeleton arm movement, the untouched opposite arm for one-arm gestures, unchanged hip/leg transforms, restoration and dismissal interruption. All three sweep variants display the native prop, restore the arms and cancel on dismissal. The renderer-only Kvastur copy is scaled down from its approximately 2.46 m native length to fit the maid and contains no gameplay components. Trajectory checks pass for 156 samples / 18,822 frames; the installed-game API check has zero unresolved references. These do not certify all three tiers, tool grips, dress clearance, multiplayer handoff or every target; those are explicit review items above.
