# Baldur's Gate 3 Mod Manager Redux

BG3 Mod Manager Redux is a Windows mod manager for Baldur's Gate 3 built on
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager).
It keeps the established BG3MM load-order and package-management foundation while adding a
redesigned interface, stronger organization tools, portable Redux data, richer metadata, and safer
file handling.

**Current build:** `0.1.0-alpha.8` - private testing alpha

[Nexus Mods](https://www.nexusmods.com/baldursgate3/mods/23799) |
[Issues](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues) |
[Changes from upstream](docs/CHANGES_FROM_UPSTREAM.md) |
[Build from source](docs/BUILDING.md)

> [!IMPORTANT]
> Redux is experimental and current builds are intended for careful personal use and private
> testing. Keep independent backups of important profiles, save files, downloaded archives, and
> the BG3 Mods folder. Verify an exported load order before launching the game.

## What Redux adds

- **A fully reworked desktop interface** with a semantic design system, custom window chrome,
  responsive layouts, consistent motion, Lucide iconography, and Dark, Light, and Parchment themes.
- **Powerful mod organization** through persistent categories, multiple categories per mod,
  custom colors and icons, collapsible visual separators, configurable columns, filtering, and a
  resizable details drawer.
- **Portable Redux bundles** that can transfer saved orders, categories, assignments, separators,
  and custom PNG icons without placing Redux-only data in `modsettings.lsx`.
- **Custom themes and typography** with semantic color editing, bundled fonts, imported local
  fonts, text-size presets, live previews, and JSON import/export.
- **Expanded source information** for Nexus Mods, mod.io, and Local packages, including provider
  metadata, manual Nexus linking, conservative database matching, and an optional Local-only mode.
- **Mod Health diagnostics** for important dependency, UUID, Script Extender, conflict, manifest,
  Mod Fixer, override, and mod.io conditions, presented only when attention is useful.
- **An optional Load Order Advisor** for conservative declared-dependency placement and cycle
  checks. It is experimental, read-only, and disabled by default.
- **Safer persistence and file operations** using staged imports, validated atomic writes,
  backups, recoverable deletion paths, and privacy-checked release packaging.

## Feature overview

### Manage and organize

- Active and inactive mod lists with drag-and-drop load-order management.
- Profiles, campaigns, saved orders, search, filtering, and configurable columns.
- Automatic and custom categories with descriptions, ordering, counts, colors, vector icons, and
  reusable transparent PNG icons.
- Multiple categories per mod and category filtering that never changes the underlying load order.
- Collapsible visual separators with custom names, descriptions, colors, icons, saved positions,
  and an optional text-only presentation.
- A compact grouped command toolbar, a complete top-menu replacement when the toolbar is hidden,
  and configurable keyboard shortcuts.
- A resizable selected-mod drawer with Overview, Description, Requirements, Files, and Changelog
  tabs, plus compact hover cards for quick information.
- A dedicated Override Mods presentation for force-loaded packages outside the numbered order.

Categories and separators are Redux presentation data. They are never exported to the game's
`modsettings.lsx`.

### Personalize the interface

- Redux Dark, Redux Light, and Parchment themes.
- Reusable custom themes with semantic colors and saved appearance preferences.
- Manrope, Atkinson Hyperlegible, Monaspace Neon, Minipax, Chivo, and Segoe UI choices.
- Local `.ttf` and `.otf` font importing with safe fallback to Manrope.
- Compact, Default, and Large interface text presets.
- Theme-aware category colors, optional colored names, interface icons, source-label modes, and
  category-colored row feedback.
- Shared Redux dialogs, menus, tooltips, controls, title bars, buttons, and interaction animation.

### Understand installed mods

- Nexus Mods, mod.io, and Local source presentation.
- Source-specific titles, authors, versions, dates, descriptions, requirements, files, and
  changelogs when available.
- Separate display names and local `.pak` filenames.
- Conservative pre-existing Nexus matching through the bundled Redux database.
- Manual Nexus project linking when automatic association is unavailable.
- Local metadata fallback and a reversible Local-only mode that disables source integrations
  without deleting saved associations.
- Mod Health summaries in the toolbar and selected-mod drawer, with focused row indicators only
  for findings that need attention.

Provider matching is a convenience, not proof that packages are compatible. Always follow the mod
author's installation and load-order instructions.

### Share Redux organization

Portable `.bg3redux` bundles can include:

- a saved load order;
- custom categories and assignments;
- category display order;
- visual separators and collapsed states; and
- reusable custom PNG icons.

Export and import previews explain what will change before anything is applied. Redux bundles do
not contain mod `.pak` files or `modsettings.lsx`, and importing one does not install missing mods.

Private testers can also create a reviewable `.bg3redux-report` for Redux database maintenance.
Reports contain conservative package identity evidence and fingerprints, but exclude profiles,
load-order positions, settings, credentials, and private filesystem paths.

### Accessibility and keyboard use

Redux surfaces the inherited Speak Active Order and Stop Speaking commands in a dedicated
Accessibility menu. It also adds scalable text presets, Atkinson Hyperlegible, selectable dialog
text, refreshed keyboard-accessible dialogs, and a rebuilt shortcut editor.

CrossSpeak, Windows speech fallback, screen-reader helpers, the speech commands, configurable
hotkeys, and the original Toolkit/editor-project marker originated in upstream BG3MM. Redux
retains those systems and updates their presentation.

## Mod Health and Load Order Advisor

Mod Health runs quietly in the background when enabled. It can report:

- missing, inactive, self-referencing, or older-than-declared dependencies;
- duplicate or invalid UUIDs;
- Script Extender requirements and status;
- confirmed active declared conflicts;
- invalid embedded creator manifests;
- bundled Mod Fixer and override behavior; and
- mod.io subscription-restoration warnings.

Mod Health is diagnostic and never repairs, installs, deletes, or reorders mods. The separate Load
Order Advisor is opt-in and currently limited to exact declared dependency ordering and dependency
cycles. See [Optional Redux modules](docs/REDUX_OPTIONAL_MODULES.md) for precise boundaries and
disabled behavior.

## Built on BG3 Mod Manager

Redux is a fork, not a from-scratch replacement. The following foundations come from
LaughingLeader and the upstream BG3MM contributors:

- active/inactive load-order management, profiles, campaigns, saved orders, and filtering;
- `.pak` and archive import through LSLib, plus established game/save/archive/text/JSON workflows;
- BG3 path detection, launch behavior, folder shortcuts, and load-order export;
- override packages, dependency and UUID checks, Osiris/Mod Fixer detection, and Script Extender
  management;
- Nexus Mods API, caching, update checks, links, images, and metadata;
- configurable hotkeys, package extraction, metadata tools, and version generation; and
- CrossSpeak, Windows speech fallback, screen-reader helpers, and speech commands.

Redux reworks and extends many of these systems, but their upstream authorship remains explicit.
See [Changes from upstream BG3 Mod Manager](docs/CHANGES_FROM_UPSTREAM.md) for the detailed
feature-by-feature distinction.

## Features for mod authors

Redux retains BG3MM's package extraction, UUID and folder-name copying, metadata inspection, custom
`meta.lsx` tags, and encoded version generator.

Mod authors may also place an optional root-level
[`redux.mod.json`](docs/REDUX_CREATOR_MANIFEST.md) inside a PAK. Redux validates its module claim
against parsed `meta.lsx` data before using it for Nexus Mods or mod.io identification. Invalid
claims are ignored and reported without changing the user's files or load order.

## Requirements and availability

- Windows 10 or Windows 11, x64.
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
- Baldur's Gate 3.

Redux does not support Linux, macOS, Wine, or Proton. It is currently framework-dependent and is
not distributed as a self-contained application.

There is no supported public installer or automatic Redux updater during the private alpha.
Developers can follow [Building Redux from source](docs/BUILDING.md).

## Known alpha limitations

- Nexus authentication currently uses a personal API key rather than public SSO.
- Provider matching, automatic categories, dependency data, and conflict data may be incomplete.
- mod.io author profile links cannot always be resolved reliably.
- The Load Order Advisor does not yet include broad compatibility-, category-, framework-, or
  author-specific rules.
- Imported fonts may expose incomplete metadata or render differently in WPF.
- Uncommon display scales and dense layouts may still expose minor visual inconsistencies.
- Clean-machine packaging and migration behavior need broader private testing.

Users are responsible for permission to use or share imported fonts and PNG icons. Imported assets
are local runtime data and are not included in Redux application packages.

## Documentation

- [Changes from upstream BG3 Mod Manager](docs/CHANGES_FROM_UPSTREAM.md)
- [Building Redux from source](docs/BUILDING.md)
- [Optional Redux modules](docs/REDUX_OPTIONAL_MODULES.md)
- [Redux mod database](docs/REDUX_MOD_DATABASE.md)
- [Creator manifest reference](docs/REDUX_CREATOR_MANIFEST.md)
- [Creator manifest JSON schema](docs/schemas/redux.mod.schema.json)

## Reporting problems

Use the [Redux issue tracker](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues) for
reproducible bugs. Include the Redux version, relevant logs, screenshots, affected mod names or
UUIDs, and reproduction steps. Never post API keys or private filesystem information.

## Credits and license

Redux exists because of LaughingLeader's original project and retains substantial upstream code
and behavior.

- [Original BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager)
- [LaughingLeader](https://github.com/LaughingLeader)
- [Support LaughingLeader on Ko-fi](https://ko-fi.com/LaughingLeader)

Major bundled dependencies and assets include LSLib, CrossSpeak, AdonisUI, ReactiveUI,
GongSolutions.WPF.DragDrop, Lucide, and the bundled open fonts. Complete attribution and license
terms are maintained in [Third-Party Notices](licenses/Third-Party-Notices.md).

Baldur's Gate 3 is developed and published by Larian Studios. Redux is an unofficial community
project and is not affiliated with or endorsed by Larian Studios, Nexus Mods, or mod.io.

The original project and Redux modifications are distributed under the [MIT License](LICENSE),
subject to all retained copyright and third-party notices.
