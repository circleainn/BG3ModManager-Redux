# Changes from upstream BG3 Mod Manager

BG3 Mod Manager Redux is a Windows-only fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). Redux preserves
the upstream load-order model, profile and campaign workflows, import/export formats, `.pak`
parsing through LSLib, game-path detection, and launch behavior. Confirmed inherited defects have
received targeted correctness and safety fixes without redesigning those core formats or workflows.

This document inventories the major Redux additions and the inherited issues addressed through
version `0.1.0-alpha.5`.

## Redux interface and design system

- A modern WPF interface built around shared semantic resources for backgrounds, surfaces, borders,
  text, accent, success, information, warning, error, and disabled states.
- Redux Dark, Redux Light, and Parchment themes with theme-specific palettes and contrast behavior.
- Shared corner-radius, spacing, typography, control-height, and interaction tokens.
- A grouped command toolbar for Install/Profile, Load Order, Order Actions, Export, Campaign, Tools,
  and Launch workflows.
- A consolidated Open menu for common game, mod, save, order, log, and project locations.
- Redux-styled buttons, text fields, combo boxes, check boxes, tabs, tooltips, menus, context menus,
  notifications, cards, pills, list rows, scrollbars, and secondary windows.
- Animated hover, press, selection, tab-indicator, drawer, and category interactions designed to
  remain subtle at normal desktop scale.
- Dynamic text trimming and header-based list-column minimum widths so long filenames do not lock a
  column at an excessive size.
- Content-aware category-pane sizing based on visible labels and the active application typeface.
- Updated application branding, executable metadata, version display, and Redux iconography.

## Themes, typography, and appearance

- A Theme & Appearance page with live built-in-theme selection and semantic color previews.
- Reusable custom themes based on a Redux palette, personalized semantic colors, a preferred
  typeface, and a preferred text-size preset.
- Custom-theme creation, editing, duplication, deletion, JSON import, JSON export, and persistence
  across application restarts.
- Compact, Default, and Large text-size presets implemented through shared dynamic typography
  resources rather than per-control scaling.
- Bundled Manrope, Atkinson Hyperlegible, Monaspace Neon, Minipax, and Chivo typefaces, plus the
  Windows-provided Segoe UI option. Built-in Redux themes default to Manrope.
- A reusable local font library for `.ttf` and `.otf` files up to 10 MB.
- Immediate imported-font discovery and preview without restarting Redux.
- Safe custom-font removal. Files still held by WPF are hidden immediately and recycled on the next
  launch instead of producing a Windows retry loop.
- Manrope fallback when an imported font is missing, invalid, or unavailable on another machine.
- An Open Fonts Folder action and protection against deleting Redux-shipped fonts.
- Optional category-colored row hover, category icons in pills, and category-colored names.

## Shared icon system and branding

- A shared `ReduxIcon` control for theme-aware vector and imported bitmap icons.
- A Lucide-based vector catalog used by toolbars, menus, category markers, separators, status
  indicators, dialogs, settings, and secondary windows.
- Lucide SVG elements are preserved as independent WPF geometries. This retains the coordinate
  behavior of relative SVG path commands and prevents stray lines or off-canvas artifacts.
- Curated category-friendly glyphs covering clothing, armor, spells, races, companions, quests,
  weapons, maps, resources, utilities, libraries, patches, overrides, and other BG3 use cases.
- Official Nexus Mods and mod.io image assets remain in source pills and provider actions instead
  of being replaced by generic interface glyphs.
- The official GitHub Invertocat image is bundled separately because Lucide does not provide brand
  logos.
- High-quality downscaling for imported bitmap icons.
- Theme-dependent icon foregrounds, including Parchment's warm red identity where appropriate.

## Categories and organization

The upstream manager did not provide Redux's persistent category system. Redux adds:

- Automatic categories covering User Interface, Gameplay, Classes, Races, Spells, Companions,
  Quests, Clothing, Armor, Weapons, Accessories, Equipment, Cosmetics, Dice, Maps, Photo Mode,
  Visuals, Animations, Audio, Overhauls, Patches, Libraries, Resources, Utilities, Miscellaneous,
  Overrides, and No Category.
- Conservative best-effort automatic assignment based on package metadata and reviewed aliases.
- User-created custom categories.
- Multiple categories per mod.
- Persistent category colors, icons, ordering, counts, filters, and new-mod indicators.
- Fixed built-in category names with editable colors and icons, plus Reset to Default.
- Dot and diamond fallback markers, with the dot used as the standard default.
- A color editor with hue selection, RGB sliders, hex input, Redux presets, and saved colors.
- A visual icon chooser with a reusable catalog of fantasy, utility, status, and organization
  glyphs.
- Reusable imported transparent PNG icons for categories and visual separators.
- Optional tinting of imported PNGs with the assigned category or separator color.
- Safe custom-icon removal with automatic fallback for categories or separators that referenced it.
- Category assignment context menus that reproduce the configured icon and color.
- Category filtering that does not modify or export the underlying load order.
- Draggable category ordering independent of automatic-classification precedence.
- Optional persistence of category filter state and optional hiding of empty categories.

## Visual load-order separators

- Named and colored separators inside the active mod list.
- Dot, diamond, vector, or imported PNG separator markers.
- Collapsible separator sections.
- Drag-positioned placement within the active order.
- Persistent separator titles, colors, icons, positions, and collapsed state.
- Clear disabled behavior where separators are not meaningful, including the inactive list.
- Automatic suppression in filtered or metadata-sorted views where a separator position would be
  misleading.
- Presentation-only behavior: separators are never written to `modsettings.lsx` or exported as
  mods.

## Selected-mod details and hover information

- A resizable bottom details drawer with Overview, Description, Requirements, Files, and Changelog
  tabs.
- Source image, display name, local package filename, categories, provider, author/uploader,
  version, update date, description, requirements, files, changelog, and linked-package details.
- A compact hover card for quick local and provider information without opening the drawer.
- Shared category, source, metadata, and status pill styling across list cells, hover cards, and the
  drawer.
- Responsive trimming that shows full pill text when space is available and ellipses only when
  constrained.
- Separate display titles and local `.pak` filenames for projects with multiple downloadable files.

## Nexus Mods, mod.io, and provenance

- Nexus Mods and mod.io source identification and metadata presentation.
- Manual Nexus project linking when automatic association is unavailable.
- Provider-specific source pills, colors, icons, actions, versions, authors, update dates, files,
  requirements, descriptions, and changelogs.
- A bundled Redux mod database for conservative matching of some pre-existing Nexus installs.
- Exact installed `.pak` size plus xxHash64 matching.
- Exact downloaded archive size plus MD5 matching.
- Reviewed module UUID identities and tightly constrained normalized name/author fallback.
- Unknown or ambiguous packages remain Local rather than being assigned speculatively.
- mod.io matching validated against package `PublishHandle` information.
- A mod.io warning and acknowledgement flow explaining that BG3 subscriptions may restore removed
  files.
- Manual/local metadata fallback when neither online provider can be identified.

See [REDUX_MOD_DATABASE.md](REDUX_MOD_DATABASE.md) for the database schema and matching rules.

## Mod status and diagnostics

- Script Extender requirement detection and version comparison.
- Distinct states for installed, missing, disabled, outdated, or incomplete Script Extender setups.
- Osiris scripting indicators.
- Mod Fixer content detection presented as compatibility information rather than a dependency.
- Override Mods and Always Loaded presentation for packages that operate outside the numbered load
  order.
- Missing-mod and dependency reporting.
- An optional Toolkit project marker for detected editor/project packages.
- A read-only Mod Health foundation for missing or inactive dependencies, duplicate or invalid
  UUIDs, Script Extender requirements, declared conflicts, bundled Mod Fixer content, override
  behavior, and mod.io safety state.
- No automatic repair, installation, conflict resolution, or load-order reordering. The complete
  Mod Health tray and Load Order Advisor remain future work.

## Dialogs, warnings, notifications, and help

- A Redux-owned `AdonisWindow` message-box system replacing standard Xceed confirmation and error
  dialogs.
- Theme-aware vector severity icons and shared Redux surfaces, borders, typography, and buttons.
- Selectable read-only message text for copying error details.
- Standard OK, OK/Cancel, Yes/No, and Yes/No/Cancel behavior plus contextual auxiliary actions.
- Consistent keyboard default, cancellation, Enter, and Escape behavior.
- Dedicated Redux preview, mod.io support, and offline Nexus database warning windows.
- A unified notification system for success, information, warning, and error messages.
- A Redux-styled Help window with Markdown rendering.
- A Redux-styled Version Generator and updated About window.
- Direct Report a Bug actions in the Help menu and About window.
- A Redux-specific GitHub issue form requesting useful reproduction information while warning users
  not to publish API keys or private paths.

## Accessibility

- A top-level Accessibility menu beside Settings so speech tools are not buried in Preferences.
- Speak Active Order and Stop Speaking commands.
- CrossSpeak integration with Windows speech fallback.
- Configurable keyboard shortcuts, including direct accessibility navigation.
- Colorblind-friendly status indicators that add shape/toolkit information rather than relying only
  on color.
- Atkinson Hyperlegible as a bundled typeface option.
- Compact, Default, and Large interface text sizes.
- Selectable dialog text, keyboard-operable dialogs, and theme-aware contrast resources.

## Safer persistence and file operations

- Atomic `settings.json` writes using temporary output, validation, replacement, and a rolling
  backup.
- Atomic `modsettings.lsx` export using temporary validation and backup replacement.
- Staged package imports so incomplete copies are not treated as installed mods.
- Backups before an update replaces an existing package.
- Recoverable and permanent deletion paths that update the interface only after filesystem success.
- Persistent custom themes, imported fonts, imported icons, categories, category order, and visual
  separators.
- Safe fallback when a custom theme references a missing font or a category references a removed
  icon.
- Reordering protection while metadata sorting makes visual position differ from load-order
  position.
- Privacy-validated release packaging that rejects settings, logs, caches, backups, development
  symbols, private paths, and other local runtime data.
- A consolidated packaged `THIRD-PARTY-NOTICES.md` containing both attribution and complete license
  terms.

## Inherited issues corrected in Redux

Open upstream issues were audited against Redux's inherited code rather than assumed fixed. The
tracking discussion is [issue #11](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues/11).

- **Large archive imports failing** (#383): removed an unnecessary whole-file allocation whose
  length cast overflowed for archives larger than roughly 2.1 GB.
- **Save/export doing nothing without feedback** (#464): added a clear alert when no profile or load
  order is selected.
- **Blank profile selection on a clean installation** (#385): missing profile directories now
  produce an empty collection instead of `null`, with guidance to launch BG3 once.
- **Saved orders disappearing after restart** (#466): saving and loading now use the same orders
  directory.
- **Drag-and-drop remaining locked after a load error** (#448, #463): loading state is reset through
  `try/finally`.
- **Missing-file mod entries that could not be removed** (#346): deletion is no longer blocked only
  because the referenced `.pak` has already disappeared.
- **Incorrect Script Extender version selection** (#470): comparison now uses full version values
  instead of only the major component.
- **Refresh discarding unsaved order changes** (#390): Refresh now requests confirmation before
  rebuilding lists from disk.
- **Early startup failures closing without explanation** (#471, #440): an early exception safety
  net reports failures that occur before the main window installs its handlers.

## Deferred or open upstream items

- Manager-launched game crashes that do not occur through Steam (#456).
- "Extension not found" reports that appear to originate from the Script Extender runtime (#461).
- Application localization (#475).
- The complete Mod Health tray and Load Order Advisor.
- Public Nexus SSO authentication.
- Automatic Redux self-updating during the private alpha.
- Linux, macOS, Wine, Proton, and self-contained .NET deployment are not planned targets.

## Compatibility boundaries

Future work should continue to preserve load-order semantics, profile and campaign behavior,
import/export formats, LSLib integration, `.pak` parsing, game-path detection, and established file
locations unless a change is explicitly scoped, reviewed for data safety, and regression-tested.
