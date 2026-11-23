# Public distribution

Use **Swiftfingers** as the public author/team name. Thunderstore derives the displayed author from the upload team's namespace, not the optional local `author` manifest field. Create or select the Swiftfingers team before the first upload; the namespace becomes part of the permanent package identity. [Thunderstore's first-upload guide](https://wiki.thunderstore.io/mods/your-first-upload).

## Build and deliver

1. Update `Plugin.Version`, the project `Version`, README, changelog, and the versioned validation record in `docs/SERVER.md` together. Add release notes describing actual validation and limitations.
2. Run `./scripts/build.ps1` with .NET 8. It restores pinned packages, builds, runs policy checks, and validates `dist/Bonehilda-<version>.zip`. `-SkipChecks` is for local development only; do not publish an unchecked build.
3. Check installed API compatibility when a game installation is available:

   ```powershell
   dotnet run --project tools/CompatibilityCheck -- `
     src/Bonehilda/bin/Release/net462/Bonehilda.dll `
     '<Valheim>/valheim_Data/Managed' `
     '<profile>/BepInEx/core' `
     '<profile>/BepInEx/plugins/ValheimModding-Jotunn'
   ```

   Use the dedicated server's managed directory to repeat the check against its installed binaries. Restore the checker's lockfile with `dotnet restore tools/CompatibilityCheck --locked-mode` on subsequent runs.
4. Complete the outstanding server qualification in [SERVER.md](SERVER.md) before claiming tested dedicated-server support. Import the ZIP in a clean client profile with only its dependencies and verify all three coffins in a disposable world.
5. For the existing local profile, run `scripts/install-r2modman.ps1 -ProfilePath '<profile>'`. It backs up and updates the installation while preserving its enabled state. Never stop the user's game or mod manager. A running session continues to use the previous loaded assembly until restarted by the user.
6. Commit, push, then run `scripts/publish-release.ps1 -NotesPath '<release-notes-file>'`. It publishes the matching GitHub tag and ZIP and verifies the downloaded public ZIP's SHA-256 against the local build.

## Thunderstore upload

Upload the **same validated ZIP** to the **Valheim** community as `Swiftfingers-Bonehilda`. It contains the exact-case root `manifest.json`, `README.md`, `icon.png` (256×256), changelog, license, credits, artwork prompt, and `BepInEx/plugins/Bonehilda/Bonehilda.dll`. Dependencies are declared, not redistributed. The build rejects extra DLLs or unlisted paths. [Package requirements](https://wiki.thunderstore.io/mods/creating-a-package) and [BepInEx folder routing](https://wiki.thunderstore.io/mods/packaging-your-mods).

Use the README as the full listing and the generated manifest's description as the short description. Select applicable content/build-piece/NPC categories from those currently offered by the community. Retain the experimental multiplayer wording until the server matrix is completed. Set the GitHub repository as the website and issue tracker. Upload existing project gameplay screenshots to illustrate the actual mod; distinguish the generated icon from in-game appearance.

The account/team and first upload still need to be completed on Thunderstore. No Thunderstore token is stored in this repository or package, and this build workflow does not automatically submit there.

## Other hosts

For Nexus Mods or another Valheim host, use the same release archive and README description, list BepInEx and Jötunn as required dependencies, select the MIT license, credit Swiftfingers, and include the compatibility limitations. Review that host's current fields and artwork rules at submission time. No claim is made that a listing or account already exists.

## Distribution hygiene

The package validator checks the archive, not just its staging folder. Builds stage into a unique temporary directory so stale files cannot leak into a later release. Keep `.tools`, logs, credentials, user configs, saves, dependency/game assemblies, `.pdb` files, and development media out of the ZIP. The private runtime probes are development tools and are not part of the public package. Never run a second Valheim instance beside a user's active game or let probes modify their graphics preferences.
