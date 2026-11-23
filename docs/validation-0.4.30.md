# 0.4.30 validation

Tested against installed Valheim 1.0.21 and Jotunn 2.30.2 on Windows, using an isolated headless startup probe with private plugins and restored graphics preferences. No player world was loaded or changed.

The checked-in helper [SessionRuntimeChecks.cs](../tests/SessionRuntimeChecks.cs) exercised the installed Jotunn compatibility comparison and native patched build/placement methods. It verified:

- A client with Bonehilda is accepted when the server's mod list omits her.
- Matching versions are accepted; older server/client patch versions are rejected in both directions. A server with Bonehilda still rejects clients missing the mod.
- A missing Jotunn handshake and a server mod list without Bonehilda both disable gameplay availability.
- Native build-menu generation excludes coffins while keeping vanilla pieces, including no-cost mode. Registered piece lists are restored after filtering.
- Unsupported coffin requirements and placement are rejected before construction.
- A supported server and local hosting restore availability and coffin menu entries without restarting.
- The login notification waits for a connected session and ready player/HUD, uses the fading center-message channel, appears once rather than every frame, and appears again in a new unsupported session. Message dispatch was intercepted to count notifications; visual timing was not observed headlessly. The installed native center-message implementation schedules fade-out after four seconds.

Existing native startup checks also passed for coffin recipes, inventory sizes, any-dress costs/UI, cloth, repair station coverage and geometry, supply/door clearance, smooth steps, emotes, seating and skeleton voices. Static installed API checks resolved all 654 references, reflection contracts and Harmony targets. The checked build passed 395 policy checks and seven invalid-package rejection checks.

These are isolated native integration tests with substituted session/handshake state, not live server connections. End-to-end joins to a vanilla server, a Jotunn server without Bonehilda, mismatched servers, dedicated/Linux hosting and two-client ownership handoffs remain unverified.
