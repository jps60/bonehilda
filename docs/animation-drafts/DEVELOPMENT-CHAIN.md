# Following the original housekeeping development chain

Source: [AugusDogus PR #3](https://github.com/jps60/bonehilda/pull/3), its checked-in assets and its commit history. The PR discussion has no additional comments. Its description reports five review rounds; those reports are evidence of the original process, not reviews performed on these new drafts.

## What the original actually did

1. **Author the model:** `Bonehilda.blend` contains the feather-duster workshop. `scripts/export-models.py` exports the original mesh into the BHM1 format. This exporter does not export skeletal clips.
2. **Develop motion in the game:** `aa8561f` introduced runtime housekeeping. `e769b57` seated the prop in the palm; `039459c` moved the elbow and flexed the feathers; `c1b1246` corrected palm orientation; `7e67b1e` refined the grip and both passes.
3. **Key the contact motion and solve the body:** `89d318f` added keyed handle turns and two-bone IK; `5992ef3` added sideways passes; `67dc172` fitted strokes to visible furniture boards and edge clearance. The important target is the working feathers, not the wrist.
4. **Add motivated variations:** `29b5d02` built three distinct chore sequences with no immediate repeats, rather than applying one periodic motion at different speeds.
5. **Record and measure:** the PR includes silent native Valheim videos, synchronized wide/close angles, consecutive chores, multiple furniture types and all tiers. It reports millimetre-scale nearest-bristle contact measurements and acknowledges remaining stiffness and untested multiplayer handoffs.

The original was therefore a **Blender model + runtime keyed paths/IK + native captures + repeated refinement** workflow, not simply a collection of Blender actions. Human dusting references are linked in the PR. Its reported reviewer scores do not validate later modifications.

## Changes to this draft process

- Keep the existing original art; use native Kvastur renderer data privately at runtime. No extracted native geometry is added to public source.
- Use authored anticipation, unequal working beats and recovery in shooing. Keep hips/legs controlled entirely by the native controller. Fit elbow planes before judging wrist motion.
- Fit the broom shaft to palm geometry and fingers. Record residual grip error; do not call a clamped, unreachable hand a successful grip.
- Separate rug working strokes from lifted resets. Aim for contact at the bristle end, not just a generic bounding-box point.
- Record the actual native skeleton, outfit, tool and runtime solver from synchronized front and side cameras. `tests/AnimationReviewCapture.cs` is the private-world capture routine. It samples native locomotion on a stationary stage; it is not a real navigation or multiplayer test.
- Preserve baseline captures, inspect representative frames and replay timing before publishing another draft. The schematic catalog remains a planning aid, not visual evidence.
- Keep expanded stand/decoration dusting in review until real mounted geometry, feather/handle clearance and reachable poses have been captured. The 150 contact trajectories do not become 150 finished animations by being enumerated.

## Review gates

Shoos: a clear friendly invitation, relaxed elbows, soft wrist flicks, distinct second beat, natural recovery, all tiers, hammer hand, sound timing, continued walking and interruption.

Sweeps: identifiable Kvastur bristle end; contact on the rug during working strokes; visible lifted return; both hands on the shaft with believable fingers; no shoulder overextension, dress penetration or near-furniture clipping. Reach/clearance failures must remain visible in measurements and review notes.

Dusting: actual displayed-item bounds and silhouettes, wall normals, finger/tool grip, shortened strokes, safe entry/exit, low stance and overhead limits. Repeat the original multi-target contact checks before enabling automatic jobs.

Audio: tests remain muted. Native skeleton idle-rattle selection is provisional until the user hears it in normal play. Alert/attack alternatives remain earmarked for review, not silently rejected.
