# Baldur's Gate 3 Mod Manager Redux

BG3 Mod Manager Redux is a modernized, community-driven fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager).
Redux preserves the established mod-management backend while developing a cleaner interface,
safer file operations, richer metadata, and better organization for large mod lists.

## Current version

**0.1.0-alpha.2 — work-in-progress alpha**

Redux is still experimental. Current builds are intended for careful personal use and a small
group of private testers. Features, metadata matching, themes, and interface details may be
incomplete or change between builds.

> [!WARNING]
> Keep independent backups of important profiles, save files, downloaded mod archives, and the
> BG3 Mods folder. Verify an exported load order before launching the game, and stop if a file
> operation behaves unexpectedly.

- Redux application self-updating is intentionally disabled during the alpha.
- There is currently no public Redux release, installer, or supported binary download.
- Source code is public for transparency and development, but this alpha is not yet intended for
  inexperienced users.

## Before you begin

1. Install and launch Baldur's Gate 3 at least once. This creates the game's user folders and
   initial profile data.
2. Create or select the profile/campaign you intend to mod in-game. Redux can manage existing BG3
   profiles, but it is safest to create new profiles from inside the game.
3. Back up these folders before building a large load order:
   - `%LOCALAPPDATA%\Larian Studios\Baldur's Gate 3\Mods`
   - `%LOCALAPPDATA%\Larian Studios\Baldur's Gate 3\PlayerProfiles`
4. Keep copies of the original archives you download. A removed or broken package is much easier
   to recover when its archive is still available.
5. Avoid running the original BG3 Mod Manager and Redux at the same time.

## Installation and first setup

There is no public packaged release yet. These steps apply to a private test build supplied by the
project owner.

1. Extract the complete archive into a normal, writable folder.
2. Do not run Redux from inside the archive, `Program Files`, or another protected folder.
3. Start `BG3ModManager.exe`.
4. Open **Settings > Preferences > General** and verify:
   - **Game Data Path** points to Baldur's Gate 3's `Data` folder.
   - **Game Executable Path** points to `bg3.exe` or the intended game executable.
   - The selected profile and campaign are correct.
5. If Redux cannot detect the paths automatically, set them manually and save the preferences.

The Game Data Path must be the real BG3 `Data` directory containing the game's data `.pak` files.
Selecting the installation root instead of its `Data` folder can prevent the manager from loading
game information correctly.

## Creating your first load order

1. Select the intended **Profile**, **Mod Order**, and **Campaign** in the top command bar.
2. Use **Install Mod** to import a `.pak` or supported archive.
3. Move the desired mods into **Active Mods**.
4. Drag active mods into the required order. The `#` column represents the real load order.
5. Use categories and visual separators for organization if desired. They do not change export
   behavior by themselves.
6. Select **Export to Game** to write the active order to BG3's `modsettings.lsx`.
7. Start the game and confirm the correct profile and mods load.

Add mods in small groups and test between exports when creating a large load order. If something
fails, the smaller batch makes it much easier to identify the cause.

## Important troubleshooting tips

### BG3 reset `modsettings.lsx`

BG3 may replace or reset `modsettings.lsx` when it rejects the exported configuration. Common
causes include:

- A broken or incompatible mod.
- A missing required dependency.
- An invalid or conflicting load order.
- Exporting to the wrong profile or campaign.
- Unexpected folder structures in the user Mods folder.

The normal user Mods folder should contain installed `.pak` files directly. Historically, placing
arbitrary subfolders inside `%LOCALAPPDATA%\Larian Studios\Baldur's Gate 3\Mods` has caused BG3 to
reject or reset mod settings. Keep archives, documentation, and manual backups somewhere else.

If the file continues to reset, disable the most recently added mods, export again, and test in
small groups. A reset is usually the game's response to a mod or configuration problem rather than
Redux silently changing the order.

### The game does not show the expected profile

- Launch BG3 and create/select the profile in-game first.
- Return to Redux, refresh, and select the matching profile and campaign.
- Export again after confirming the selection.

### A mod appears installed but does not work

- Check whether it is in **Active Mods** or is an **Override Mod**.
- Review its description and requirements in the details drawer.
- Confirm required dependencies and Script Extender versions on the mod's source page.
- Remember that automatic metadata matching and categorization can be incomplete.
- Check whether another mod overwrites the same game files.

### mod.io restored a deleted mod

BG3's in-game mod manager can redownload subscribed mod.io packages. Removing the local file in
Redux does not necessarily unsubscribe from the mod. Unsubscribe through mod.io or BG3's in-game
manager when you want the mod removed permanently.

### Paths are not detected

Use **Settings > Preferences** to set the Game Data Path and executable manually. The Data Path
must end at the game's `Data` directory, not merely the Baldur's Gate 3 installation folder.

## Redux features

### Core mod management

- Active and inactive mod management with preserved BG3 load-order behavior.
- Drag-and-drop reordering, including multi-selection where supported.
- Profile, campaign, and saved load-order management.
- Importing `.pak` files and supported archives.
- Exporting load orders to the game, text files, JSON, and archives where supported.
- Filtering and configurable list columns.
- Shortcuts to common game, mod, save, and log folders.
- Dark, Light, and Parchment themes with bundled Inter typography.
- Screen-reader and accessibility behavior inherited from the upstream manager.

### Organization

- Persistent automatic and custom categories.
- Multiple categories per mod and custom category colors.
- Category filtering without changing the underlying load order.
- Redux-only visual separators and collapsible sections.
- Draggable category ordering and optional filter-state persistence.

Categories and separators are Redux organizational metadata. Separators are not mods, are never
written to `modsettings.lsx`, and disappear from filtered or metadata-sorted views where their
positions would be misleading.

### Mod information

- Local package metadata with Nexus Mods and mod.io provider linking.
- Source-specific titles, authors, versions, dates, descriptions, requirements, files, and
  changelogs when available.
- A resizable details drawer and quick-glance hover cards.
- Separate display names and local `.pak` filenames for projects with multiple downloadable files.
- Local metadata fallback when no online source can be matched.

Provider matching is a convenience, not proof that two packages are compatible. Always read the
author's installation instructions on the source page.

### Override Mods and Mod Fixer

Pure override packages are displayed in **Override Mods** because they replace built-in game files
outside the normal numbered load order. Their `.pak` presence can keep those overrides active even
when they do not have a normal `modsettings.lsx` entry.

Redux can also detect Mod Fixer files bundled inside a package. This is compatibility information,
not an instruction to install Mod Fixer separately. Modern BG3 versions generally do not require
Mod Fixer, but older packages may still contain its legacy recompilation technique.

### Safer file operations

- Atomic `settings.json` writes with validation and a rolling backup.
- Atomic `modsettings.lsx` export with temporary-file validation and backup replacement.
- Staged imports so incomplete copies are not mistaken for installed `.pak` files.
- Backups before replacing installed packages during updates.
- Recoverable and permanent deletion paths that update the UI only after filesystem success.
- Reordering protection while a metadata column sort is active.

These safeguards reduce risk, but they do not replace independent user backups.

## Nexus Mods and mod.io

Nexus Mods and mod.io API keys can be entered in Preferences for private testing. Never publish,
share, or commit personal API keys.

- Nexus Mods is the preferred online metadata source when a reliable match is available.
- Redux includes a bundled offline Nexus `.pak` provenance database for some pre-existing installs.
  It identifies only exact known file hashes; unknown packages remain **Local**, and bundled details
  may be older than the current Nexus page until live metadata is refreshed with an API key.
- mod.io metadata is used for packages recognized as BG3 in-game/mod.io installations.
- There is no bundled offline mod.io database; a mod.io API key is required for live mod.io details.
- Local metadata remains available when neither provider can be matched.
- mod.io support displays an additional warning because subscriptions can restore removed files.

A registered Nexus SSO application slug and a reviewed authentication flow will be required before
Redux can offer a polished public Nexus sign-in experience.

## Mod Health status

Redux contains a read-only Mod Health analysis foundation for future diagnostics. It can inspect
conditions such as missing or inactive dependencies, duplicate/invalid UUIDs, Script Extender
status, declared conflicts, bundled Mod Fixer content, override behavior, and mod.io safety state.

The final user-facing Health tray and Load Order Advisor are not implemented. Current findings are
diagnostic and conservative; Redux does not automatically repair, install, or reorder mods.

## Features for mod authors

Redux retains inherited BG3MM tools useful for mod development, including:

- Extracting selected mod packages for inspection.
- Copying mod UUIDs and folder names from context actions.
- Generating encoded BG3 version values through the Version Generator tool.
- Reading descriptions, dependencies, tags, and package metadata from `meta.lsx`.
- Exporting load-order and mod information in shareable formats.

Custom `meta.lsx` tags are separated with semicolons. Metadata quality directly affects how well
Redux and other tools can describe, categorize, and validate a mod.

Developer utilities should be used carefully. Extracted or edited projects placed in the game's
`Data` folder can behave differently from ordinary user Mods-folder packages and may directly
affect game files.

## Known alpha limitations

- No supported public binary release or automatic Redux updating.
- Nexus authentication currently relies on a personal API key rather than public SSO.
- Provider matching, automatic categories, dependency data, and conflict data may be incomplete.
- mod.io author profile links cannot always be resolved reliably.
- The complete Mod Health tray, requirement validator, and Load Order Advisor are not implemented.
- Custom theme creation/import/export is planned but not implemented.
- Some inherited dialogs or dense tab selections may still have minor visual inconsistencies.
- Packaging and clean-machine behavior still require wider private testing.

Report reproducible problems through the
[Redux issue tracker](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues). Include the
Redux version, relevant logs, screenshots, the affected mod names/UUIDs, and the steps that led to
the problem. Do not post API keys or private filesystem information.

## Building from source

Redux targets .NET 8 and Windows WPF. Building the complete solution also requires Visual Studio's
C++ build tools for LSLibNative.

### Requirements

- Windows 10 or Windows 11.
- Visual Studio with .NET desktop development and Desktop development with C++ workloads.
- .NET 8 SDK.
- Git with repository submodules/dependencies present.

### Build

1. Clone the repository and its required submodules.
2. Open `BG3ModManager.sln` in Visual Studio.
3. Select the `x64` platform.
4. Build the `Debug` or `Release` configuration.

The internal assembly and executable filename remain `BG3ModManager` for compatibility with
inherited WPF resource paths. That internal name does not indicate an upstream release.

## Project links

- [Redux repository](https://github.com/raincloudsfollow/BG3ModManager-Redux)
- [Redux issue tracker](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues)
- [Baldur's Gate 3 on Nexus Mods](https://www.nexusmods.com/baldursgate3)
- [BG3 Script Extender](https://github.com/Norbyte/bg3se)

## Upstream project and attribution

Redux exists because of LaughingLeader's original BG3 Mod Manager and retains substantial portions
of its code and core behavior. Upstream authorship, copyright, and license notices must remain
intact.

- [Original BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager)
- [LaughingLeader](https://github.com/LaughingLeader)
- [Support LaughingLeader on Ko-fi](https://ko-fi.com/LaughingLeader)

Redux also depends on third-party projects including:

- [LSLib by Norbyte](https://github.com/Norbyte/lslib)
- [BG3 Script Extender by Norbyte](https://github.com/Norbyte/bg3se)
- CrossSpeak and its bundled screen-reader integrations
- Inter, distributed under the SIL Open Font License
- AdonisUI, ReactiveUI, GongSolutions.WPF.DragDrop, and other packages listed in the project files

Baldur's Gate 3 is developed and published by Larian Studios. Redux is an unofficial community
project and is not affiliated with or endorsed by Larian Studios, Nexus Mods, or mod.io.

See the repository [license](LICENSE) and [third-party notices](licenses/Third-Party-Notices.md)
for complete terms and notices.

## License

The original project is provided under the MIT License. Redux modifications remain subject to that
license and all retained copyright notices. See [LICENSE](LICENSE).
