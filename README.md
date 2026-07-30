# Baldur's Gate 3 Mod Manager Redux

BG3 Mod Manager Redux is a Windows mod manager for Baldur's Gate 3 built on
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). It preserves
BG3MM's established package and load-order foundation while adding a redesigned interface,
stronger organization, portable Redux data, richer metadata, and safer file handling.

**Current build:** `0.1.0-alpha.8` — private testing alpha

[Nexus Mods](https://www.nexusmods.com/baldursgate3/mods/23799) ·
[Report an issue](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues) ·
[Changes from upstream](docs/CHANGES_FROM_UPSTREAM.md) ·
[Build from source](docs/BUILDING.md)

> [!IMPORTANT]
> Redux is experimental. Keep independent backups of important profiles, save files, downloaded
> archives, and the BG3 Mods folder. Verify an exported load order before launching the game.

## Highlights

- **A reworked desktop interface** with semantic themes, custom window chrome, responsive layouts,
  consistent motion, Lucide iconography, and Dark, Light, and Parchment themes.
- **Persistent mod organization** with automatic and custom categories, multiple categories per
  mod, configurable columns, collapsible separators, filtering, and a resizable details drawer.
- **Portable `.bg3redux` bundles** for saved orders, categories, assignments, separators, and
  reusable custom PNG icons—without placing Redux data in `modsettings.lsx`.
- **Custom themes and typography** with semantic color editing, bundled and imported fonts,
  Compact/Default/Large text presets, live previews, and JSON import/export.
- **Richer source information** for Nexus Mods, mod.io, and Local packages, including manual Nexus
  linking, conservative database matching, and a reversible Local-only mode.
- **Read-only mod diagnostics** with optional experimental load-order guidance.
- **Safer persistence** through a read-only game-export review, automatic pre-export restore
  points, staged imports, validated atomic writes, backups, recoverable deletion paths, and
  privacy-checked release packaging.
- **A concise optional Redux overview** available from Help that collects preview, source, and
  mod.io guidance, explains Redux's core workflow, and offers reversible starting choices without
  interrupting startup.

## Features

### Organize load orders

- Manage active and inactive mods with BG3MM's established drag-and-drop load-order workflow.
- Use profiles, campaigns, saved orders, search, filtering, and configurable columns.
- Assign multiple categories to a mod or use conservative automatic category suggestions.
- Assign or remove categories across a multi-selection from the same context menu.
- Use the selection-aware context group to move or delete several selected mods, apply or clear a
  shared note, or clear the current selection.
- Create categories with custom names, descriptions, colors, vector icons, or imported transparent
  PNG icons.
- Click category pills directly in either mod list to filter both lists; the active filter is shown
  in each list header and can be cleared there.
- Add collapsible visual separators with names, descriptions, colors, icons, remembered positions,
  and an optional text-only presentation.
- Work from the grouped command toolbar or hide it and use the complete compact Toolbar menu.
- Review activations, deactivations, placement changes, automatically added dependencies, and
  enabled Mod Diagnostics findings before Redux writes the selected order to the game profile.
- Open File > Load Order History to review the 20 newest pre-export or manually captured snapshots
  for the current profile, see how each differs from the working order, compare it with saved orders,
  and load a selected snapshot without changing game files until it is exported.
- Compare any two available load orders from File > Compare Load Orders to see user-managed mods
  that were added, removed, or meaningfully repositioned without changing either order.
- Open the selected-mod drawer for Overview, Description, Requirements, Files, and Changelog tabs,
  or use hover cards for quick information.
- Keep per-mod notes in Redux for installation reminders, compatibility context, or
  personal load-order guidance, including an atomic shared-note action for a multi-selection.
- Keep force-loaded packages visible in the dedicated Override Mods section outside the numbered
  order.

Categories and separators are presentation data. Redux never writes them to the game's
`modsettings.lsx`.

### Understand installed mods

- Distinguish Nexus Mods, mod.io, and Local packages with source-aware metadata and actions.
- View available titles, authors, versions, dates, descriptions, requirements, files, and
  changelogs.
- Keep display names separate from local `.pak` filenames.
- Associate supported pre-existing Nexus packages through the reviewed Redux database or attach a
  Nexus project manually.
- Disable source integrations with Local-only mode without deleting cached associations.
- Review dependency, UUID, Script Extender, conflict, creator-manifest, Mod Fixer, override, and
  mod.io findings through Mod Diagnostics.
- Use dependency-finding actions to reveal an installed dependency, copy its UUID, open available
  source pages, or explicitly activate an already-installed inactive dependency after confirmation.
  Redux never downloads, installs, repairs, or reorders dependencies automatically.
- Run the on-demand Active File Overlaps inspector from Tools to find internal PAK paths shared by
  active and override packages. Redux reports these as overlaps rather than definite conflicts,
  because patches and intentional overrides commonly share files.
- Optionally include experimental declared-dependency placement and cycle guidance in the same
  Advisor status and finding list.

Mod Diagnostics analysis is read-only. Its dependency assistant changes the working order only when
the user explicitly confirms activation of an already-installed dependency, and the change does not
reach game files until export. Provider matches are conveniences rather than compatibility
guarantees; always follow the mod author's instructions.

### Personalize Redux

- Switch between Redux Dark, Redux Light, and Parchment.
- Create reusable custom themes with semantic colors and saved appearance preferences.
- Choose Manrope, Atkinson Hyperlegible, Monaspace Neon, Minipax, Chivo, or Segoe UI.
- Import local `.ttf` and `.otf` files with safe fallback to Manrope.
- Use Compact, Default, or Large interface text.
- Configure category-colored labels and interaction feedback, interface icons, and a compact
  icons-only mode for category, source, and status labels.
- Reuse Redux's shared dialogs, title bars, menus, tooltips, controls, and motion language across
  built-in and custom themes.

### Move Redux data between computers

Portable `.bg3redux` bundles can include:

- a saved load order;
- custom categories, descriptions, and assignments;
- category display order;
- visual separators, descriptions, and collapsed states;
- mod notes when explicitly enabled in the export review; and
- reusable custom PNG icons.

Export and import previews explain what will change before it is applied. Bundles do not contain
mod `.pak` files or `modsettings.lsx`, and importing one does not install missing mods. Mod
notes remain local by default and are never included unless the bundle exporter checks the
dedicated option.

Private testers can also generate a reviewable `.bg3redux-report` for database maintenance.
Reports include conservative package identity evidence and fingerprints while excluding profiles,
load-order positions, settings, credentials, and private filesystem paths.

### Accessibility and keyboard use

Redux places the inherited Speak Active Order and Stop Speaking commands in a dedicated
Accessibility menu. It also provides scalable text presets, Atkinson Hyperlegible, selectable
dialog text, keyboard-accessible Redux dialogs, a rebuilt shortcut editor, reduced-motion
transitions, and an option to disable background blur and dimming.

Redux starts without informational warning dialogs. A short optional overview covering its preview
status, source and mod.io limitations, organization, safe exporting, restore points, order
comparison, personalization, accessibility, and optional diagnostics remains available from
**Help > Take the Redux Tour**. Opening it changes no installed packages or game files.

Press `F2` to open the optional Quick Access menu for finding actions, mods, profiles, saved
orders, and categories without navigating the full menu system.

CrossSpeak, Windows speech fallback, screen-reader helpers, speech commands, configurable hotkeys,
and the original Toolkit/editor-project marker come from upstream BG3MM. Redux retains those
systems and refreshes their presentation.

## Built on BG3 Mod Manager

Redux is a fork, not a from-scratch replacement. These foundations come from LaughingLeader and the
upstream BG3MM contributors:

- active/inactive load-order management, profiles, campaigns, saved orders, and filtering;
- `.pak` and archive import through LSLib, plus established game/save/archive/text/JSON workflows;
- BG3 path detection, launch behavior, folder shortcuts, and load-order export;
- override packages, dependency and UUID checks, Osiris/Mod Fixer detection, and Script Extender
  management;
- Nexus Mods API integration, caching, update checks, links, images, and metadata;
- configurable hotkeys, package extraction, metadata tools, and version generation; and
- CrossSpeak, Windows speech fallback, screen-reader helpers, and speech commands.

Redux reworks and extends many of these systems while preserving their upstream attribution. See
[Changes from upstream BG3 Mod Manager](docs/CHANGES_FROM_UPSTREAM.md) for the detailed distinction.

## Features for mod authors

Redux retains BG3MM's package extraction, UUID and folder-name copying, metadata inspection, custom
`meta.lsx` tags, and encoded version generator.

Mod authors may also place an optional root-level
[`redux.mod.json`](docs/REDUX_CREATOR_MANIFEST.md) inside a PAK. Redux validates its module claim
against parsed `meta.lsx` data before using it for Nexus Mods or mod.io identification. Invalid
claims are ignored and reported without changing user files or load orders.

## Requirements

- Windows 10 or Windows 11, x64.
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
- Baldur's Gate 3.

Redux does not support Linux, macOS, Wine, or Proton. It is framework-dependent and is not
distributed as a self-contained application.

There is no supported public installer or automatic Redux updater during the private alpha.
Developers can follow [Building Redux from source](docs/BUILDING.md).

## Current alpha limitations

- Nexus authentication uses a personal API key rather than public SSO.
- Provider matching, automatic categories, dependency data, and conflict data may be incomplete.
- mod.io author profile links cannot always be resolved reliably.
- Experimental load-order guidance in Mod Diagnostics is deliberately limited to high-confidence
  declared dependency information.
- Imported fonts may expose incomplete metadata or render differently in WPF.
- Uncommon display scales and dense layouts may still expose minor visual inconsistencies.
- Clean-machine packaging and migration behavior need broader testing.

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
reproducible bugs. Include the Redux version, reproduction steps, relevant logs, screenshots, and
affected mod names or UUIDs. Never post API keys or private filesystem information.

## Credits and license

Redux exists because of LaughingLeader's original project and retains substantial upstream code and
behavior.

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
