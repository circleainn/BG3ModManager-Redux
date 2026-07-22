# Baldur's Gate 3 Mod Manager Redux

BG3 Mod Manager Redux is a modernized, community-driven fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager).
Redux preserves the established mod-management backend while developing a cleaner interface,
safer file operations, richer metadata, and better organization for large mod lists.

> [!NOTE]
> Redux is a Windows-only WPF application. It does not support Linux, macOS, Wine, or Proton, and
> there are no current plans to add cross-platform support.

## Current version

**0.1.0-alpha.5 — private testing alpha**

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

### Alpha.5 highlights

- Added shared dot and diamond markers for categories and visual separators, with the dot as the
  consistent default and custom icons still supported.
- Refined the Categories pane with stronger color-aware borders and an optional setting for
  category-colored names.
- Unified secondary buttons, editor wording, spacing, and interaction feedback across category and
  custom-theme workflows.
- Replaced the stale bug-report template with a Redux-specific issue form and added direct
  **Report a Bug** links to the Help menu and About window.
- Added Compact, Default, and Large interface text sizing with shared dynamic typography tokens.
- Custom themes now preserve both their preferred bundled typeface and text-size preset across
  activation, duplication, import/export, and application restarts.
- Added reusable custom PNG icons for categories and separators, including optional category-color
  tinting, safe local storage, and explicit removal.
- Added a reusable custom-font library for local TrueType and OpenType fonts, with immediate
  preview, custom-theme defaults, safe deferred deletion, and Manrope fallback when unavailable.
- Replaced the mixed legacy icon set with a shared Lucide-based vector system, retaining official
  Nexus Mods, mod.io, and GitHub image assets where generic interface icons are inappropriate.
- Consolidated Theme & Appearance controls into a more compact theme, typography, and custom-theme
  workflow with clearer semantic color previews and more consistent Redux dialogs.
- Continued conservative UI cleanup without changing load-order behavior, package parsing,
  import/export behavior, game-path detection, or file-management semantics.

## Redux features

### Core mod management

- Active and inactive mod management with preserved BG3 load-order behavior.
- Drag-and-drop reordering, including multi-selection where supported.
- Profile, campaign, and saved load-order management.
- Importing `.pak` files and supported archives.
- Exporting load orders to the game, text files, JSON, and archives where supported.
- Filtering and configurable list columns.
- Shortcuts to common game, mod, save, and log folders.
- Dark, Light, and Parchment themes with bundled typography and text-size selectors plus refined semantic palettes.
  Redux Dark, Redux Light, and Parchment all default to Manrope.
- Safe custom themes with a preferred bundled or locally imported typeface and text size, live
  preview, duplication, JSON import/export, and restart persistence. Missing custom fonts fall
  back to Manrope without preventing the theme from loading.
- A reusable local font library accepts `.ttf` and `.otf` files up to 10 MB. Imported fonts can be
  removed from Redux even when WPF has them loaded; locked files are recycled on the next launch.
- Theme-aware Lucide vector iconography and consistent interaction feedback across Redux-owned
  controls, with official provider logos retained for source identification.

### Accessibility

- Speech controls (Speak Active Order, Stop Speaking) and keyboard-shortcut settings live in a
  dedicated Accessibility menu beside Settings, via CrossSpeak integration and Windows speech
  fallback.

### Organization

- Persistent automatic and custom categories spanning common Nexus BG3 mod types, with conservative best-effort assignment.
- Multiple categories per mod with custom colors, curated vector icons, or reusable transparent
  PNG icons. Custom PNGs may retain their original colors or be tinted to the category color.
- Fixed Redux default category identities with per-category color/icon customization and reset.
- Optional category icons in pills and optional category-colored mod-row hover feedback.
- Category filtering without changing the underlying load order.
- Redux-only visual separators and collapsible sections with optional custom icons.
- Draggable category ordering and optional filter-state persistence.

Categories and separators are Redux-only metadata, never written to `modsettings.lsx`. Separators
disappear from filtered or metadata-sorted views where their position would be misleading.

### Mod information

- An optional Toolkit project marker adds a build icon beside detected editor or project mods.
- Local package metadata with Nexus Mods and mod.io provider linking.
- Manual Nexus project linking plus a bundled Redux mod database for conservative pre-existing-install matching.
- Source-specific titles, authors, versions, dates, descriptions, requirements, files, and
  changelogs when available.
- A resizable details drawer and quick-glance hover cards using shared Redux pill and status styles.
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

## Nexus Mods and mod.io

Nexus Mods and mod.io API keys can be entered in Preferences for private testing. Never publish,
share, or commit personal API keys.

- Nexus Mods is the preferred online metadata source when a reliable match is available.
- Redux includes a bundled Nexus mod database for some pre-existing installs. Exact package hashes
  are preferred; conservative reviewed identity matches may associate a package with a Nexus
  project when the evidence is unambiguous. Unknown packages remain **Local**, and database details
  may differ from the current Nexus page until live metadata is refreshed with an API key.
- A mod can be manually linked to its Nexus project when automatic association is unavailable.
- mod.io metadata is used for packages recognized as BG3 in-game/mod.io installations.
- There is no bundled mod.io database; a mod.io API key is required for live mod.io details.
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
- Redux ships as a framework-dependent build, so the .NET 8 Desktop Runtime must already be
  installed on the target machine. There are no current plans to switch to a self-contained
  deployment, which would substantially increase build size and update payload.
- Nexus authentication currently relies on a personal API key rather than public SSO.
- Provider matching, automatic categories, dependency data, and conflict data may be incomplete.
- mod.io author profile links cannot always be resolved reliably.
- The complete Mod Health tray, requirement validator, and Load Order Advisor are not implemented.
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
