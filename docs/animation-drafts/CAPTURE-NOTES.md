# Native review: 0.4.41

The six MP4 files in [captures](captures/) were rendered in an isolated Valheim session using the production draft DLL, the native skeleton/player controller, the existing fitted outfit and renderer-only Kvastur geometry. Each frame contains simultaneous front-oblique and side views. All probes were muted from startup. No extracted native models or audio are distributed with the capture assets.

Each shoo sequence has 40 frames, including preparation/recovery; each sweep has 68. Encoding is 12 fps. Shoos sample `Walk New` in place; sweeps sample `IdleTweaked`. This is a controlled pose review, not an autonomous route, crowded-tame test, multiplayer test or proof of natural motion at every speed. The test maid is zero-star; all-tier visual approval remains pending. Broom scale is derived from pose-independent bone lengths and includes the native tier scale.

The first render exposed an unsettled spawn pose, stiff elbow planes, overly periodic gestures, an oversized broom, and wrist targets that were not real grips. Subsequent passes warmed the native controller, authored unequal guiding beats, lowered the elbow planes, fitted the prop to the maid, measured the actual hooked shaft cross sections, curled the fingers and shortened the sweeping strokes. The final pose is retained for review rather than being declared finished.

`captures/measurements.csv` records the palm-fit residual returned by the runtime solver. For visible frames between 0.5 and 4.5 seconds, the largest residual is about 0.50 mm left / 0.29 mm right for the long sweep, 0.27 mm / 0.52 mm for gathering, and 0.26 mm / 28.3 mm for the fan. These are **solver palm-point errors**, before blending, not finger-surface contact, bristle-contact measurements or proof that the mesh does not clip.

**Review especially:** the fan's far-right reach, finger wrap and wrist twist, the hooked top near her head, bristles near the hem, small rug/furniture clearance, and the entry/exit. The fan is kept as a candidate despite its worse reach. Shrunken native bristles retain their original silhouette; whether that silhouette is too busy at this scale is an art-review question. Skeleton sound selection and timing remain pending audible normal-play review because tests are muted.

## Reproducing the capture

`tests/AnimationReviewCapture.cs` supplies the coroutine for an isolated BepInEx test harness after its private world/player loads. It requires a rendered graphics device, uses no foreground input, writes PNGs and measurement CSV under the harness's private config path, and cleans up its staged objects. Link this source into a private probe, then yield its `Run()` method; do not ship the probe or execute it in an established player world. The harness must mute its audio listener in `Awake` and retain zero volume every frame, disable cloud saves, use copied saves/private plugins, refuse a second running game, and preserve/restore user graphics preferences. The existing local probe provided those controls for these recordings.

After the probe exits:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File docs/animation-drafts/encode-captures.ps1 -InputDirectory <private-config>/animation-review -FfmpegPath <ffmpeg.exe>
```

This generates six silent MP4s, preserves the CSV and rebuilds the self-contained `native-review.html` player. `ShooAnimationRuntimeChecks.cs` separately verifies arm masks, lower-body preservation, restoration, dismissal, native prop safety and stature fitting. These structural checks complement the visual review.
