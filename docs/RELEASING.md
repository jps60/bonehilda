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

Generate the host-specific handoff from the existing checked build:

```powershell
./scripts/prepare-upload.ps1 -VerifyPublicDownload
```

This writes `dist/upload/Bonehilda-<version>/` containing the unchanged release ZIP, Thunderstore metadata, Nexus descriptions in BBCode and plain text, copyable field values, an ordered gallery with captions, and SHA-256 checksums. Public verification downloads the GitHub asset without authentication and compares it with the local ZIP. Without `-VerifyPublicDownload`, preparation works offline and reports that the public download was not checked. CI prepares the same materials as a separate artifact.

Preparation runs `scripts/test-upload.ps1` before promoting the handoff directory. It checks release-version and author alignment, Nexus AI labels, exact file inventory, all listed checksums, captioned gallery copies, resolved description templates, and balanced BBCode. To recheck a handoff independently, run `./scripts/test-upload.ps1 -Directory 'dist/upload/Bonehilda-<version>'`.

**Upload only `Bonehilda-<version>.zip` as the mod file on each host.** The surrounding directory is a submission handoff, not a second game-mod package. Gallery captures are separate image uploads. Their original source files remain in `assets/models`; they were copied without altering the images. The captures were made during development and do not certify this release's multiplayer testing.

Upload the **same validated ZIP** to the **Valheim** community as `Swiftfingers-Bonehilda`. It contains the exact-case root `manifest.json`, `README.md`, `icon.png` (256×256), changelog, license, credits, artwork prompt, and `BepInEx/plugins/Bonehilda/Bonehilda.dll`. Dependencies are declared, not redistributed. The build rejects extra DLLs or unlisted paths. [Package requirements](https://wiki.thunderstore.io/mods/creating-a-package) and [BepInEx folder routing](https://wiki.thunderstore.io/mods/packaging-your-mods).

Use the README as the full listing and the generated manifest's description as the short description. Select applicable content/build-piece/NPC categories from those currently offered by the community. Retain the experimental multiplayer wording until the server matrix is completed. Set the GitHub repository as the website and issue tracker. Upload existing project gameplay screenshots to illustrate the actual mod; distinguish the generated icon from in-game appearance.

The account/team and first upload still need to be completed on Thunderstore. No Thunderstore token is stored in this repository or package, and this build workflow does not automatically submit there.

## Other hosts

For Nexus Mods, use the same release archive and the generated `nexus/summary.txt`, `nexus/description.bbcode` (BBCode/source editor) or `nexus/description.txt` (plain-text alternative), `nexus/file-description.txt`, and `nexus/fields.json`. The description includes dependencies, recipes, installation, configuration, removal, support links, and experimental multiplayer status. Use **Swiftfingers** in the Author field and choose the intended upload account; no account has been authenticated by preparation.

List BepInExPack_Valheim as an external requirement and Jötunn as a requirement using its [Nexus page](https://www.nexusmods.com/valheim/mods/1138). Require Jötunn 2.30.2 or newer; if that mirror offers an older file, direct users to the upstream Thunderstore package. Manual extraction and r2modman import are documented; Vortex deployment has not been validated. Choose the closest currently available gameplay/NPC/building category rather than treating suggested labels as verified category IDs.

The code and listing text contain substantial OpenAI Codex output, and the illustrative icon was generated with imagegen. Apply Nexus's **AI-Generated Content** and **AI Media** classifications and retain the prepared disclosure. Do not classify this as limited AI assistance only. Nexus's [AI policy](https://help.nexusmods.com/article/180-ai-policy-changes-2026) and [submission guidelines](https://help.nexusmods.com/article/28-file-submission-guidelines) were reviewed on 2026-11-22; recheck them at submission time.

Keep reuse/fork permissions consistent with the existing MIT license and the third-party rights notice. AI-training and other new account-specific permission preferences are deliberately not selected by preparation; review them in the Nexus form before submission. No license change is implied by these draft form suggestions. Donation settings, ownership declarations, and any terms acceptance remain account actions, not values a packaging script should submit automatically.

For another Valheim host, adapt the prepared text to its current form, preserve the dependency and compatibility statements, and credit Swiftfingers. No claim is made that a listing or account already exists.

## First-upload sequence

1. Review `dist/upload/Bonehilda-<version>/README.txt` and `verification.json`. A package can be ready for a testing release while dedicated-server qualification remains outstanding; do not label that support as tested.
2. Choose or create the **Swiftfingers** Thunderstore team and confirm it is the intended permanent namespace. On Nexus, confirm the intended uploader and Author field. Preparation does not check availability or create either account.
3. Review the final descriptions, requirements, AI disclosures, current category choices, license/permission fields, gallery captions, and archive version on each form.
4. Upload the unchanged mod ZIP. Add the separate gallery PNGs and captions where the site permits images. Keep private drafts unpublished until a functional file is attached and all required fields are complete; public placeholders are not appropriate.
5. After the first publication, record the actual listing URLs here. Download each host's public mod ZIP and compare it to `verification.json` before announcing the release. No listing URLs are recorded yet because neither submission has occurred.
6. Test installation from each host in a fresh profile with its dependencies. To switch an existing r2modman local import to the official package, disable/remove the old imported entry before enabling the downloaded one, keep exactly one enabled Bonehilda DLL, and avoid opening the world during the switch. Preserve the configuration if desired.

## Distribution hygiene

The package validator checks the archive, not just its staging folder. Builds stage into a unique temporary directory so stale files cannot leak into a later release. Keep `.tools`, logs, credentials, user configs, saves, dependency/game assemblies, `.pdb` files, and development media out of the ZIP. The private runtime probes are development tools and are not part of the public package. Never run a second Valheim instance beside a user's active game or let probes modify their graphics preferences.
