# Changes from upstream BG3 Mod Manager

BG3 Mod Manager Redux is a Windows-only fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). Redux preserves
the upstream load-order model, profile and campaign workflows, import/export formats, `.pak`
parsing through LSLib, game-path detection, and launch behavior. Confirmed inherited defects have
received targeted correctness and safety fixes without redesigning those core formats or workflows.

This page tracks what Redux inherits, what it reworks or extends, and what it adds through version
`0.1.0-alpha.7`.

## Inherited foundation

These core systems come from LaughingLeader's BG3 Mod Manager:

- active/inactive mod lists, multi-selection drag and drop, profiles, campaigns, saved orders, list
  filtering, and normal BG3 load-order management;
- `.pak` and archive import, LSLib-backed package parsing, game/save/archive/text/JSON load-order
  import and export, path detection, launch behavior, and common folder actions;
- override/force-loaded package behavior, missing dependencies, invalid UUID reporting, Osiris and
  Mod Fixer detection, and Script Extender installation, update, requirement, and status handling;
- Nexus Mods API, cache, update checks, links, images, metadata, and rich mod hover information;
- configurable hotkeys and mod-author utilities such as package extraction, UUID/folder copying,
  custom metadata tags, and version generation; and
- screen-reader detection and automation helpers, CrossSpeak, Windows speech fallback, Speak
  Active Order, Stop Speaking, and the original setting labeled **Colorblind Support**, whose
  implementation exposed a Toolkit/editor-project marker rather than a broad colorblind mode.

## Redux interface and design system

- A broad rewrite of the inherited WPF presentation layer around shared semantic resources for
  backgrounds, surfaces, borders, text, accent, success, information, warning, error, and disabled
  states.
- Rebuilt Dark and Light themes based on the inherited theme capability, plus the new Parchment
  theme and theme-specific contrast behavior.
- Shared corner-radius, spacing, typography, control-height, and interaction tokens.
- A full recomposition of the inherited toolbar into compact setup, load-order, export, and launch
  groups, with consistent workflow-button styling and restrained accent interaction feedback.
- A top-level Shortcuts menu that reorganizes inherited location actions and Redux links for common
  game, mod, save, order, log, project, and online destinations.
- A complete compact Toolbar menu when the visual toolbar is hidden, plus a configurable Toggle
  Toolbar shortcut.
- Redux-styled buttons, text fields, combo boxes, check boxes, tabs, tooltips, menus, context menus,
  notifications, cards, pills, list rows, scrollbars, and secondary windows.
- Animated hover, press, selection, tab-indicator, drawer, and category interactions designed to
  remain subtle at normal desktop scale.
- Dynamic text trimming and header-based list-column minimum widths so long filenames do not lock a
  column at an excessive size.
- Content-aware category-pane sizing based on visible labels and the active application typeface.
- Updated application branding, executable metadata, version display, and Redux iconography.
- A Redux-owned startup surface with live initialization status. The main window is prepared
  off-screen and revealed only after its visual tree and workspace initialization are ready,
  avoiding the legacy blank-window loading phase.

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
- Theme-aware category-colored row hover, category icons in pills, and category-colored names.
  Built-in themes provide deliberate defaults, and custom themes preserve all three preferences.

## Shared icon system and branding

- A shared `ReduxIcon` control for theme-aware vector and imported bitmap icons.
- A Lucide-based vector catalog used by toolbars, menus, category markers, separators, status
  indicators, dialogs, settings, and secondary windows.
- Lucide SVG elements are preserved as independent WPF geometries. This retains the coordinate
  behavior of relative SVG path commands and prevents stray lines or off-canvas artifacts.
- Curated category-friendly glyphs covering clothing, armor, spells, races, companions, quests,
  weapons, maps, resources, utilities, libraries, patches, overrides, and other BG3 use cases.
- Official Nexus Mods and mod.io image assets remain in source indicators and provider actions instead
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

## Portable Redux bundles

- A Redux-only `.bg3redux` archive format containing a normal saved-order description plus a
  separately versioned presentation manifest.
- Optional transfer of custom categories, explicit category assignments, category display order,
  active-list separators, collapsed states, and reusable custom PNG icons.
- Choice of importing the saved order, its Redux presentation metadata, or both.
- A pre-export review summarizing the saved order, custom categories, separators, and custom icons,
  with an explicit reminder that mod `.pak` files and `modsettings.lsx` are never included and an
  opt-in action to reveal the finished bundle in File Explorer.
- Creator-version and export-time metadata, including a compatibility warning when a bundle was
  created by a newer Redux build while still rejecting unsupported bundle schemas.
- A pre-import impact summary that reports locally available and missing mods by name, identifies
  category-name conflicts that will be renamed, and summarizes the presentation contents before
  either selected component is applied.
- Conflict-safe custom-category import that preserves an existing local category and gives a
  differing imported category a unique name instead of overwriting local styling.
- Separator anchors stored relative to neighboring mod UUIDs, with a validated fallback position
  when a neighboring mod is unavailable.
- Size, expanded-size, entry-count, duplicate-entry, path, schema, UUID, color, category-order,
  and custom-icon cross-reference validation before an archive is accepted.
- Complete separation from the game export path: Redux bundles never contain or write
  `modsettings.lsx`.

## Selected-mod details and hover information

- Upstream already exposed descriptions, dependencies, Nexus data, and status information through
  rich mod tooltips. Redux replaces that tooltip-centered presentation with a reworked quick-glance
  card and a persistent detail surface.
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

Upstream already included Nexus Mods API access, caching, update information, links, images, and
tooltip metadata. Redux reworked that presentation into a provider model and extended it with:

- Mod.io source identification and live metadata.
- Manual Nexus project relinking when automatic association is unavailable.
- Provider-specific Redux indicators, colors, icons, actions, versions, authors, update dates,
  files, requirements, descriptions, and changelogs.
- A bundled Redux mod database for conservative matching of some pre-existing Nexus installs.
- Exact installed `.pak` size plus xxHash64 matching.
- Exact downloaded archive size plus MD5 matching.
- Reviewed module UUID identities and tightly constrained normalized name/author fallback.
- Unknown or ambiguous packages remain Local rather than being assigned speculatively.
- An opt-in database-contribution report generator that exports reviewable identity evidence and
  exact PAK fingerprints without load-order positions, profiles, settings, credentials, or private
  paths.
- Standalone preview-first command-line and desktop maintainer utilities for validating the
  bundled database, reviewing contribution reports, accepting independently confirmed Nexus
  records, and writing a selected batch atomically only after an explicit confirmation.
- A schema-backed `redux.mod.json` creator-manifest format designed to live inside each PAK so
  identity metadata remains attached after installation, with read-only runtime discovery,
  strict validation against parsed package metadata, seamless provider resolution, and
  non-destructive diagnostics.
- A reversible Local-only mode that suppresses Nexus Mods and mod.io requests, pauses bundled
  database enrichment, hides the Source column and source-assignment actions, and presents
  installed packages as Local without deleting their stored associations.
- mod.io matching validated against package `PublishHandle` information.
- A mod.io warning and acknowledgement flow explaining that BG3 subscriptions may restore removed
  files.
- Manual/local metadata fallback when neither online provider can be identified.

See [REDUX_MOD_DATABASE.md](REDUX_MOD_DATABASE.md) for the database schema and matching rules.

## Mod status presentation and Redux diagnostics

Script Extender requirement detection and installation status, Osiris scripting indicators,
Mod Fixer detection, override/force-loaded behavior, and missing-mod/dependency reporting all
originate upstream. Redux retains those checks while reworking how they are interpreted and shown:

- Fuller Script Extender version comparison and clearer installed, missing, disabled, outdated, or
  incomplete states.
- Redesigned Script Extender and Osiris row indicators and status tooltips.
- Mod Fixer content presented as compatibility information rather than a missing requirement.
- A dedicated, compact Override Mods presentation for packages outside the numbered load order.
- A clearer **Show Toolkit project markers** preference name and Redux icon treatment for the
  inherited editor-project marker.

Redux additionally adds:

- Background Mod Health checks for missing, inactive, self-referencing, or older-than-declared
  dependencies; duplicate or invalid UUIDs; Script Extender requirements; confirmed active
  declared conflicts; embedded creator-manifest validation; bundled Mod Fixer content; override
  behavior; and mod.io safety state.
- A master Mod Health preference that stops scheduled analysis and removes health indicators
  without affecting installed mods, load orders, exports, or core manager behavior.
- An opt-in, disabled-by-default Load Order Advisor that can show conservative read-only warnings
  when an active mod's declared dependency is positioned later in the numbered load order or when
  active declared dependency metadata forms a cycle. Advisor checks are registered separately from
  general health checks and do not run when Mod Health is off.
- A compact warning or error pill in the selected-mod Overview when attention is needed, with
  severity-ranked details in its tooltip. Healthy mods add no extra interface.
- A load-order-wide Active Mods health summary that appears only when active mods need attention
  and focuses an affected mod when selected.
- A compact row-level health indicator for duplicate UUIDs, inactive dependencies, and declared
  conflicts that otherwise have no dedicated status icon.
- No automatic repair, installation, conflict resolution, or load-order reordering. Broader
  category- and compatibility-based load-order recommendations remain future work.
- Rule-level Mod Health extension boundaries (`IModHealthAnalyzer` and `IModHealthRule`) so
  diagnostics can be added, disabled, or removed without coupling them to list loading or export.

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

## Accessibility presentation and extensions

CrossSpeak, Windows speech fallback, Speak Active Order, Stop Speaking, screen-reader detection and
automation helpers, configurable hotkeys, and the narrowly scoped Toolkit-marker option originally
labeled **Colorblind Support** all originate in LaughingLeader's BG3 Mod Manager. Redux keeps that
foundation and reworks or extends its presentation through:

- A top-level Accessibility menu beside Settings so the inherited speech tools are no longer buried
  under Tools.
- A rebuilt keyboard-shortcut editor and direct accessibility navigation.
- A renamed and visually rebuilt Toolkit-project marker that more accurately describes its scope.
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

The following upstream issues were confirmed in the inherited code and fixed in Redux. The tracking
discussion is [issue #11](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues/11).

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
- Broader category-, compatibility-, and author-rule-based Load Order Advisor guidance beyond the
  current opt-in declared-dependency placement check.
- An optional built-in Redux onboarding and feature tour. On first start, Redux should ask
  "Would you like a tour of Redux and its features?" with clear Yes and No choices; declining must
  immediately continue into the app without additional prompts. The tour should remain replayable
  from Help and introduce active/inactive lists, safe exporting, categories and separators, the
  selected-mod drawer, Mod Health, themes and typography, accessibility tools, source warnings,
  and Redux-specific import/export without changing the user's load order or files. The tour should
  fold the current Redux preview, mod.io support, and offline Nexus database explanations into a
  coherent guided sequence instead of stacking separate startup dialogs, while preserving any
  acknowledgement that is still required for an enabled feature.
  The tour should
  also explain optional source linking and offer a clear choice between enabling integrations or
  starting in **Local-only mode**. It should identify Mod Health as optional, keep the experimental
  Load Order Advisor disabled unless the user deliberately enables it, and make every choice
  reversible from Preferences.
- Public Nexus SSO authentication.
- Automatic Redux self-updating during the private alpha.
- Linux, macOS, Wine, Proton, and self-contained .NET deployment are not planned targets.

## Low-priority ideas

- Optional Coolors URL import for custom themes. A future implementation could parse palette colors
  from a shared `coolors.co` URL, suggest mappings to Redux's semantic theme tokens, preserve
  accessible status colors by default, and let the user review the result before saving.

## Compatibility boundaries

Future work should continue to preserve load-order semantics, profile and campaign behavior,
import/export formats, LSLib integration, `.pak` parsing, game-path detection, and established file
locations unless a change is explicitly scoped, reviewed for data safety, and regression-tested.
