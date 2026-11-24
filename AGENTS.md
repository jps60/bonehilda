# Project delivery instructions

The user has authorized the following standing workflow for every requested build:

- Build and check the completed change, then commit and push its source to GitHub.
- Publish a GitHub release for the matching version with the built `Bonehilda-<version>.zip` attached. Verify the public download matches the local archive.
- Install the completed build in the user's existing r2modman profile when working in their installed environment.

Use `scripts/build.ps1` for the checked build and package. Use `scripts/publish-release.ps1` after committing and pushing. Keep the plugin constant, project version, README, and release version aligned.

Use **Swiftfingers** as the author displayed in mod managers for all future versions. Keep the project Authors metadata, generated package author, and installed profile authorName aligned.

Never stop the user's Valheim or r2modman process. Never start another Valheim instance while one is running. Isolated headless probes must preserve the user's graphics settings and use private saves and plugins.

Mute every headless game probe from startup using the private probe's audio listener; preserve the user's normal audio settings. Do not run audible headless tests.
