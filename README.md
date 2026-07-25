# Baldur's Gate 3 Mod Manager Redux

BG3 Mod Manager Redux is a modernized, community-driven fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager).
Redux preserves the established mod-management backend while developing a cleaner interface,
safer file operations, richer metadata, and better organization for large mod lists.

> [!NOTE]
> Redux is a Windows-only WPF application. It does not support Linux, macOS, Wine, or Proton, and
> there are no current plans to add cross-platform support.

## Current version

**0.1.0-alpha.7 — private testing alpha**

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

## Built on BG3 Mod Manager

Redux is not a from-scratch mod manager. It inherits the following foundations from
LaughingLeader's BG3 Mod Manager:

- Active and inactive mod management, drag-and-drop multi-selection, profiles, campaigns, saved
  orders, filtering, and BG3 load-order export.
- `.pak` and archive import through the established LSLib-backed package pipeline, plus load-order
  import/export to the game, JSON, text, save files, and archives.
- Automatic game-path detection, launch behavior, folder shortcuts, override/force-loaded package
  handling, and mod-development utilities.
- Mod descriptions, dependencies, custom `meta.lsx` tags, Nexus Mods metadata/update support,
  Script Extender installation and requirement status, Osiris/Mod Fixer detection, missing
  dependency and invalid-UUID indicators, and rich hover information.
- Configurable keyboard shortcuts, screen-reader detection and automation helpers, CrossSpeak
  integration with Windows speech fallback, Speak Active Order, Stop Speaking, and the original
  narrowly scoped option labeled **Colorblind Support**, which exposed a Toolkit/editor-project
  marker instead of relying only on its row background.
- The original dark/light theme support and WPF application architecture.

These systems were built by LaughingLeader and the upstream contributors. Redux preserves them
while rebuilding and extending many of their interfaces and supporting systems.

See [Changes from upstream BG3ModManager](docs/CHANGES_FROM_UPSTREAM.md) for a fuller breakdown of
what Redux changes, adds, and fixes.

## Redux additions and extensions

### Interface and workflow

- A semantic Redux design system, rebuilt Dark and Light palettes, a Parchment theme, bundled
  typography, text-size presets, and reusable custom themes.
- A resizable selected-mod details drawer and redesigned hover information that extend the
  upstream metadata already available for mods.
- Portable `.bg3redux` Redux bundles that can carry a saved load order, custom categories,
  assignments, active-list separators, and reusable custom PNG icons between Redux installations.
- A compact export review that shows exactly which Redux organization data will be shared and
  confirms that mod `.pak` files and `modsettings.lsx` are not included, with an optional shortcut
  to reveal the completed bundle in File Explorer.
- Import previews report local mod availability, category-name conflicts, export time, and the
  originating Redux version before any selected bundle component is applied.
- Filtering and configurable list columns with reliable Redux-default visibility and sizing resets.
- A reorganized top-level Shortcuts menu that brings inherited game, mod, save, log, project, and
  online shortcuts into one consistently styled location.
- A compact grouped command toolbar with an equivalent top-menu workflow when the toolbar is
  hidden, plus a configurable Toggle Toolbar shortcut.
- A compact Redux startup surface with live initialization status; the main window remains staged
  out of sight until its layout and mod-management workspace are ready to reveal.
- Safe custom themes with a preferred bundled or locally imported typeface and text size, live
  preview, duplication, JSON import/export, and restart persistence. Missing custom fonts fall
  back to Manrope without preventing the theme from loading.
- A reusable local font library accepts `.ttf` and `.otf` files up to 10 MB. Imported fonts can be
  removed from Redux even when WPF has them loaded; locked files are recycled on the next launch.
- Theme-aware Lucide vector iconography and consistent interaction feedback across Redux-owned
  controls, with official provider logos retained for source identification.

### Accessibility presentation

- The inherited Speak Active Order and Stop Speaking commands are surfaced in a dedicated
  Accessibility menu instead of remaining under Tools.
- Redux adds scalable Compact, Default, and Large interface text, an Atkinson Hyperlegible option,
  selectable dialog text, and refreshed keyboard-accessible Redux dialogs. CrossSpeak, Windows
  speech fallback, the speech commands themselves, hotkey infrastructure, screen-reader helpers,
  and the original Toolkit-marker accessibility option originate upstream.

### Organization

- Persistent automatic and custom categories spanning common Nexus BG3 mod types, with conservative best-effort assignment.
- Multiple categories per mod with custom colors, curated vector icons, or reusable transparent
  PNG icons. Custom PNGs may retain their original colors or be tinted to the category color.
- Fixed Redux default category identities with per-category color/icon customization and reset.
- Theme-aware category presentation options for icons in pills, category-colored names, and
  category-colored mod-row hover feedback. These settings can be stored with custom themes.
- Category filtering without changing the underlying load order.
- Redux-only visual separators and collapsible sections with optional custom icons.
- Draggable category ordering and optional filter-state persistence.

Categories and separators are Redux-only metadata, never written to `modsettings.lsx`. They can be
shared through a Redux bundle without changing the game export format. Separators
disappear from filtered or metadata-sorted views where their position would be misleading.

### Mod information

- The inherited Toolkit/editor-project marker is retained with a clearer preference name and
  Redux icon treatment; it is not described as a comprehensive colorblind mode.
- Redux extends the inherited Nexus Mods metadata/update foundation with mod.io support, manual
  Nexus project linking, Local presentation, and a bundled database for conservative
  pre-existing-install matching.
- Source-specific titles, authors, versions, dates, descriptions, requirements, files, and
  changelogs when available.
- A resizable details drawer with Overview, Description, Requirements, Files, and Changelog tabs,
  plus background health notices that stay hidden when no attention is needed and quick-glance
  hover cards using shared Redux pill and status styles.
- Separate display names and local `.pak` filenames for projects with multiple downloadable files.
- Local metadata fallback when no online source can be matched.
- An optional Local-only mode suppresses Nexus Mods and mod.io requests, hides source-linking UI,
  and presents installed packages as Local without deleting their saved source associations.

Provider matching is a convenience, not proof that two packages are compatible. Always read the
author's installation instructions on the source page.

### Override Mods and Mod Fixer

Override/force-loaded package handling and Osiris/Mod Fixer detection originate in upstream BG3MM.
Redux retains that behavior while giving it a clearer **Override Mods** presentation and treating
Mod Fixer as compatibility information rather than a missing dependency.

Pure override packages replace built-in game files outside the normal numbered load order. Their
`.pak` presence can keep those overrides active even when they do not have a normal
`modsettings.lsx` entry.

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

## Nexus Mods and mod.io

Nexus Mods and mod.io API keys can be entered in Preferences for private testing. Never publish,
share, or commit personal API keys.

Nexus Mods API, cache, update, link, image, and tooltip support existed upstream. Redux retains that
foundation and adds the bundled provenance database, manual relinking workflow, mod.io provider,
provider-rich details drawer, contribution reports, and reversible Local-only mode described
below.

- Nexus Mods is the preferred online metadata source when a reliable match is available.
- Redux includes a bundled Nexus mod database for some pre-existing installs. Exact package hashes
  are preferred; conservative reviewed identity matches may associate a package with a Nexus
  project when the evidence is unambiguous. Unknown packages remain **Local**, and database details
  may differ from the current Nexus page until live metadata is refreshed with an API key.
- A mod can be manually linked to its Nexus project when automatic association is unavailable.
- mod.io metadata is used for packages recognized as BG3 in-game/mod.io installations.
- There is no bundled mod.io database; a mod.io API key is required for live mod.io details.
- Previously cached mod.io associations remain available without an API key; removing the key stops
  live requests rather than discarding an already established source identity.
- Local metadata remains available when neither provider can be matched.
- mod.io support displays an additional warning because subscriptions can restore removed files.
- Private testers can generate a reviewable `.bg3redux-report` containing conservative mod identity
  evidence and exact PAK fingerprints for database maintenance. Reports exclude load-order
  positions, profiles, settings, credentials, and private paths; they never update the bundled
  database automatically.
- Users who do not want source integrations can enable **Local-only mode** in Preferences. Redux
  then pauses provider requests and bundled-database enrichment, hides the Source column and
  source-assignment actions, and presents packages as Local. Turning the option off restores the
  saved associations.

A registered Nexus SSO application slug and a reviewed authentication flow will be required before
Redux can offer a polished public Nexus sign-in experience.

## Mod Health

Mod Health runs quietly in the background. When the selected mod has a warning or error, Overview
shows a compact status pill whose tooltip explains missing or inactive dependencies, duplicate or
invalid UUIDs, self-dependencies, installed dependencies below declared minimum versions, Script
Extender status, confirmed active declared conflicts, embedded creator-manifest validation,
bundled Mod Fixer content, override behavior, and mod.io safety state. Mod Health is an optional
Redux module and can be disabled in Preferences; disabling it stops analysis and removes its indicators.
Healthy mods add no extra interface.

When one or more active mods need attention, the Active Mods header shows a compact load-order
summary. Opening it lists affected mods in severity order; choosing one focuses it in the list.
Duplicate UUIDs, inactive dependencies, and declared conflicts also receive a compact row-level
health indicator so they remain visible without opening the selected-mod drawer.
Health findings are diagnostic and conservative, and Redux does not automatically repair, install,
or reorder mods. The experimental Load Order Advisor is a separate opt-in extension of Mod Health
and is disabled by default. Its rules rely only on declared package dependencies; broader
category- or compatibility-based recommendations remain future work. It reports dependencies that
load later than their dependents and declared dependency cycles that cannot be satisfied by a
linear order. When Debug Mode is enabled,
each changed active order writes a `[LoadOrderAdvisor]` diagnostic summary to the Redux log,
including a clear result when no reversed dependencies are detected.

Source integrations, Mod Health, and Load Order Advisor are deliberately layered over the preserved
manager core. See [Optional Redux modules](docs/REDUX_OPTIONAL_MODULES.md) for their boundaries and
disabled behavior.

## Features for mod authors

Redux retains inherited BG3MM tools useful for mod development, including:

- Extracting selected mod packages for inspection.
- Copying mod UUIDs and folder names from context actions.
- Generating encoded BG3 version values through the Version Generator tool.
- Reading descriptions, dependencies, tags, and package metadata from `meta.lsx`.
- Exporting load-order and mod information in shareable formats.

Custom `meta.lsx` tags are separated with semicolons. Metadata quality directly affects how well
Redux and other tools can describe, categorize, and validate a mod.

Mod authors may also embed an optional root-level
[`redux.mod.json`](docs/REDUX_CREATOR_MANIFEST.md) in each PAK. Redux validates its module identity
claims against the package's parsed `meta.lsx` during the normal scan. A validated source identity
feeds the same Nexus Mods or mod.io metadata pipeline used by other matches, while explicit manual
source choices retain precedence. Invalid claims are ignored, produce a read-only diagnostic, and
never change the user's load order or files.

Developer utilities should be used carefully. Extracted or edited projects placed in the game's
`Data` folder can behave differently from ordinary user Mods-folder packages and may directly
affect game files.

## Known alpha limitations

- No supported public binary release or automatic Redux updating.
- Redux ships as a framework-dependent build, so the .NET 8 Desktop Runtime must already be
  installed on the target machine. There are no current plans to switch to a self-contained
  deployment, which would substantially increase build size and update payload.
- Nexus authentication currently relies on a personal API key rather than public SSO.
- Provider matching, automatic categories, dependency data, and conflict data may be incomplete.
- mod.io author profile links cannot always be resolved reliably.
- Broader compatibility-, category-, framework-, and author-specific Load Order Advisor rules are
  not implemented. Current advice is limited to exact declared dependency placement and cycles.
- Dense layouts and uncommon Windows display scales may still expose minor visual inconsistencies.
- Some user-imported fonts may expose incomplete metadata or render differently in WPF; Redux
  falls back to Manrope when an imported font cannot be loaded.
- Packaging and clean-machine behavior still require wider private testing.

Users are responsible for ensuring they have permission to use and share any fonts or PNG icons
they import. Local imported assets are runtime user data and are not included in Redux packages.

Report reproducible problems through the
[Redux issue tracker](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues). Include the
Redux version, relevant logs, screenshots, the affected mod names/UUIDs, and the steps that led to
the problem. Do not post API keys or private filesystem information.

## Project links

- [Redux repository](https://github.com/raincloudsfollow/BG3ModManager-Redux)
- [Redux issue tracker](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues)
- [Baldur's Gate 3 on Nexus Mods](https://www.nexusmods.com/baldursgate3)
- [BG3 Script Extender](https://github.com/Norbyte/bg3se)
- [Building from source](https://github.com/raincloudsfollow/BG3ModManager-Redux/blob/main/docs/BUILDING.md)
- [Changes from upstream BG3ModManager](https://github.com/raincloudsfollow/BG3ModManager-Redux/blob/main/docs/CHANGES_FROM_UPSTREAM.md)

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
- [Manrope](https://github.com/davelab6/manrope),
  [Atkinson Hyperlegible](https://github.com/googlefonts/atkinson-hyperlegible),
  [Monaspace](https://github.com/githubnext/monaspace),
  [Minipax](https://github.com/ronotypo/Minipax), and
  [Chivo](https://github.com/Omnibus-Type/Chivo), distributed under the SIL Open Font License
- [Lucide](https://github.com/lucide-icons/lucide), distributed under the ISC License
- AdonisUI, ReactiveUI, GongSolutions.WPF.DragDrop, and other packages listed in the project files

Baldur's Gate 3 is developed and published by Larian Studios. Redux is an unofficial community
project and is not affiliated with or endorsed by Larian Studios, Nexus Mods, or mod.io.

See the repository [license](LICENSE) and
[third-party notices](https://github.com/raincloudsfollow/BG3ModManager-Redux/blob/main/licenses/Third-Party-Notices.md)
for complete terms and notices. Packaged builds combine the attribution summary and complete
bundled dependency terms into one `THIRD-PARTY-NOTICES.md` file; the repository keeps the editable
notice and original per-dependency files for provenance and maintenance.

## License

The original project is provided under the MIT License. Redux modifications remain subject to that
license and all retained copyright notices. See [LICENSE](LICENSE).
