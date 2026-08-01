# Baldur's Gate 3 Mod Manager Redux

BG3 Mod Manager Redux is a Windows mod manager built on
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). It keeps the
original manager's package and load-order foundation while adding a refreshed interface and more
ways to organize, review, and share mod setups.

**Current build:** `0.1.0-alpha.8` - private testing alpha

[Nexus Mods](https://www.nexusmods.com/baldursgate3/mods/23799) | [Report an issue](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues) | [Changes from upstream](docs/CHANGES_FROM_UPSTREAM.md) | [Build from source](docs/BUILDING.md)

> [!IMPORTANT]
> Redux is still in early development. Keep backups of important profiles, saves, downloaded
> archives, and the BG3 Mods folder. Review exported orders before launching the game.

## What Redux adds

- **A redesigned interface** with Redux Dark, Redux Light, Parchment, custom themes, scalable text,
  imported fonts, shared window styling, and optional reduced motion and background effects.
- **Categories and separators** with custom names, descriptions, colors, icons, filtering,
  collapsible sections, and multiple categories per mod.
- **Mod details in one place** through hover cards and a resizable drawer for descriptions,
  requirements, files, changelogs, linked pages, and personal notes.
- **Online mod information** from Nexus Mods and mod.io, with manual page linking and a reviewed
  local database for some existing Nexus installs. It can be disabled without removing saved links.
- **Mod Diagnostics** for detectable package, dependency, Script Extender, Mod Fixer, override,
  creator-manifest, conflict, and mod.io conditions. Optional Load Order Advisor checks add cautious
  guidance based only on declared dependencies.
- **Safer order changes** with an export review, pre-export restore points, order comparison, staged
  imports, backups, and validated writes.
- **Redux Modlists** (`.bg3redux`) for moving an order, categories, separators, and optional notes
  between Redux installations without changing `modsettings.lsx` or including mod files.

## Organize and review mods

- Manage active and inactive mods with the original BG3MM drag-and-drop workflow.
- Use profiles, campaigns, saved orders, filters, configurable columns, and a compact optional
  Quick Access menu (`F2`).
- Assign categories to one mod or a selection, then click category pills to filter both lists.
- Add separators that remember their placement and collapsed state.
- Add notes to mods and optionally include them in a Redux Modlist.
- Compare saved orders or load a recent restore point without changing game files until export.
- Inspect shared internal PAK paths with **Tools > Active File Overlaps**. Overlaps are reported as
  information, not definite conflicts, because patches often share files intentionally.

Categories, separators, and notes are Redux data. They are never written to the game's
`modsettings.lsx`.

## Mod Diagnostics

Mod Diagnostics reports conditions Redux can detect from installed packages and available mod
information. It does not download, install, delete, repair, or reorder mods automatically.

When a dependency is already installed, an available action can reveal it, copy its UUID, open its
linked page, or activate it after confirmation. Activating a dependency changes only the working
order until the user exports it.

The optional Load Order Advisor is experimental and disabled by default. It checks declared
dependency placement and cycles; it does not attempt to infer a complete load order.

## Redux Modlists

A `.bg3redux` Modlist can contain:

- a saved load order;
- custom categories, descriptions, assignments, and display order;
- separators, descriptions, positions, and collapsed states;
- reusable custom PNG icons; and
- mod notes when explicitly selected during export.

Import and export previews show what will change. A Redux Modlist does not contain `.pak` files or
`modsettings.lsx`, and importing one does not install missing mods.

Private testers can also use **Generate Redux Database Contribution** to create a reviewable
`.bg3redux-report`. Reports exclude profiles, load-order positions, settings, credentials, and
private filesystem paths.

## Themes and accessibility

- Choose Redux Dark, Redux Light, or Parchment, or create and share a custom theme.
- Choose Compact, Default, or Large text and one of the bundled fonts, or import `.ttf` and `.otf`
  files. Some imported fonts may not display correctly.
- Configure category-colored selection, colored text, icons, and icon-only labels.
- Reduce motion or disable background blur and dimming.
- Use selectable dialog text, configurable shortcuts, keyboard-accessible dialogs, and the
  inherited speech commands.

The first launch opens one optional setup window for choosing a theme, online mod information,
diagnostics, API keys, and accessibility options. It can be reopened from Help.

## Built on BG3 Mod Manager

Redux is a fork, not a from-scratch replacement. These systems come from LaughingLeader and other
upstream BG3MM contributors:

- active and inactive lists, profiles, campaigns, saved orders, filtering, and load-order export;
- `.pak` and archive import through LSLib and the established file workflows;
- BG3 path detection, launch behavior, override packages, dependencies, UUID checks, Osiris and
  Mod Fixer detection, and Script Extender management;
- Nexus Mods integration, caching, links, images, metadata, and update checks;
- configurable shortcuts, package extraction, metadata tools, and version generation; and
- CrossSpeak, Windows speech fallback, screen-reader helpers, and speech commands.

Redux reworks and extends many of these systems while retaining their credit. See
[Changes from upstream BG3 Mod Manager](docs/CHANGES_FROM_UPSTREAM.md) for the detailed distinction.

## For mod authors

Redux retains BG3MM's package extraction, UUID and folder-name copying, metadata inspection, custom
`meta.lsx` tags, and encoded version generator.

A mod author may also place an optional root-level
[`redux.mod.json`](docs/REDUX_CREATOR_MANIFEST.md) inside a PAK. Redux validates its module claim
against parsed `meta.lsx` data before using it for Nexus Mods or mod.io identification. Invalid
claims are ignored and reported without changing user files or load orders.

## Requirements and current limits

- Windows 10 or Windows 11, x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Baldur's Gate 3

Linux, macOS, Wine, and Proton are not supported. The application is framework-dependent and is not
distributed as a self-contained build.

During the private alpha:

- Nexus authentication uses a personal API key rather than public SSO.
- Online matching, automatic categories, dependency data, and conflict data may be incomplete.
- mod.io author profile links cannot always be resolved.
- Imported fonts may have incomplete metadata or render differently in WPF.
- Uncommon display scales and dense layouts may still expose visual issues.
- Clean-machine packaging and migration behavior need broader testing.

Users are responsible for permission to use or share imported fonts and PNG icons. Imported assets
are local data and are not included in application packages.

## Documentation

- [Changes from upstream BG3 Mod Manager](docs/CHANGES_FROM_UPSTREAM.md)
- [Building from source](docs/BUILDING.md)
- [Optional features](docs/REDUX_OPTIONAL_MODULES.md)
- [Redux mod database](docs/REDUX_MOD_DATABASE.md)
- [Creator manifest reference](docs/REDUX_CREATOR_MANIFEST.md)
- [Creator manifest JSON schema](docs/schemas/redux.mod.schema.json)

## Reporting problems

Use the [issue tracker](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues) for
reproducible bugs. Include the Redux version, reproduction steps, relevant logs, screenshots, and
affected mod names or UUIDs. Never post API keys or private filesystem information.

## Credits and license

Redux exists because of LaughingLeader's original project and retains substantial upstream code and
behavior.

- [Original BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager)
- [LaughingLeader](https://github.com/LaughingLeader)
- [Support LaughingLeader on Ko-fi](https://ko-fi.com/LaughingLeader)

Bundled dependencies and assets include LSLib, CrossSpeak, AdonisUI, ReactiveUI,
GongSolutions.WPF.DragDrop, Lucide, and the bundled open fonts. Attribution and license terms are in
[Third-Party Notices](licenses/Third-Party-Notices.md).

Baldur's Gate 3 is developed and published by Larian Studios. Redux is an unofficial community
project and is not affiliated with or endorsed by Larian Studios, Nexus Mods, or mod.io.

The original project and Redux modifications are distributed under the [MIT License](LICENSE),
subject to all retained copyright and third-party notices.
