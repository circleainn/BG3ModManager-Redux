# Changes from upstream BG3 Mod Manager

BG3 Mod Manager Redux is a Windows-only fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). This document
is a living description of the product and architectural differences between Redux and upstream.
It is not a release history; individual fixes and version-specific changes belong in Git history
and the issue tracker.

## Relationship to upstream

Redux retains the upstream foundations that make it a BG3 mod manager:

- profiles, campaigns, active and inactive mod lists, saved orders, and normal load-order editing;
- import and export of `modsettings.lsx` and supported load-order formats;
- `.pak` and archive handling through LSLib;
- game, profile, save, order, and mod path detection;
- game launch behavior and Script Extender integration;
- Nexus Mods and mod.io metadata capabilities;
- override and force-loaded mod handling; and
- inherited keyboard, speech, and screen-reader support.

Redux remains a fork rather than a clean-room replacement. Upstream copyright, attribution,
license terms, and third-party notices are preserved.

## Product and interface differences

Redux replaces the inherited presentation layer with a cohesive Redux interface while retaining
the underlying mod-management model. Major differences include:

- Redux branding, executable metadata, iconography, startup experience, and About/Help surfaces;
- shared semantic colors, typography, spacing, corner radii, controls, menus, tooltips, dialogs,
  notifications, scrollbars, and window chrome;
- Dark, Light, and Parchment themes plus persistent custom themes;
- bundled and imported fonts, Compact/Default/Large text sizes, and reusable custom PNG icons;
- a reorganized toolbar, compact Toolbar menu, Shortcuts menu, and optional Quick Access menu;
- a selected-mod details drawer and richer, source-aware hover information; and
- a unified Lucide-based vector icon system with retained official provider branding where
  appropriate.

Redux list surfaces use bounded render-only wheel transitions over logical item scrolling.
Virtualization and recycling remain authoritative, avoiding WPF's mixed-height pixel-anchor path.
The shared **Reduce motion** preference disables smooth scrolling together with other animated
movement.

## Categories and visual organization

Redux adds a persistent organization layer that upstream does not provide:

- automatic and user-created mod categories;
- multiple category assignments per mod;
- category colors, descriptions, icons, ordering, counts, and filtering;
- category-aware row, hover, selection, and details presentation;
- category import/export through Redux-owned formats; and
- explicit reset and fallback behavior for removed colors, icons, fonts, or categories.

Redux also adds named visual separators to the active load order. Separators support colors,
descriptions, icons, persistent collapse state, and durable section membership. They are strictly
presentation data: they are never written to `modsettings.lsx` or treated as mods. Expanded
separator drags move only the marker. Collapsed separators move with their sealed contents as one
group, remain closed after the move, and do not absorb unrelated rows at their destination. Rows
placed next to a closed separator remain visible until the separator is expanded and its section
boundaries are recalculated.

## Load-order workflow and portable data

Redux extends the inherited load-order workflow with:

- portable Redux Modlists containing a saved order, optional Redux presentation data, and public
  source references;
- independent import choices for order data, presentation data, source links, and private notes;
- validation against malformed, mismatched, or unexpected bundle contents;
- export review showing meaningful activations, deactivations, placement changes, automatically
  included dependencies, and relevant diagnostics;
- bounded per-profile restore points created before confirmed game exports or on demand;
- read-only comparison between saved orders and restore points; and
- optional private notes that remain outside game files and contribution reports.

Portable Redux data does not include `modsettings.lsx`, installed packages, profiles, saves, API
keys, logs, caches, or other machine-private data.

Source-link import is disabled by default. Enabling it explicitly replaces Nexus Mods or mod.io
associations for matching module UUIDs; leaving it disabled preserves the recipient's installed
package provenance.

## Diagnostics and dependency assistance

Redux replaces scattered status presentation with a unified, read-only Mod Diagnostics system. It
can report dependency, UUID, Script Extender, creator-manifest, declared-conflict, Mod Fixer,
override, and mod.io safety conditions without automatically changing the installation or load
order.

Optional load-order guidance is kept separate from default correctness checks. Contextual actions
may reveal an installed dependency, copy its UUID, open a reviewed source page, or explicitly add
an already-installed inactive dependency to the working order after confirmation. Redux does not
automatically download, install, repair, activate, resolve, or reorder mods.

An on-demand Active File Overlaps inspector reports shared paths across active and override PAKs.
It describes overlaps, not confirmed conflicts.

## Source metadata and provenance

Redux expands provider handling with:

- explicit manual, native, cached, reviewed-database, and local provenance states;
- conservative archive-name and package-identity matching;
- reviewed missing-dependency links;
- creator-manifest validation and guarded cache reuse;
- a reversible local-only mode that stops Nexus Mods and mod.io requests without deleting stored
  associations; and
- privacy-validated contribution reports that exclude credentials, private paths, notes, profiles,
  and load-order data.

Provider metadata is informational. It does not silently replace package identity or override an
explicit user association.

## Persistence and filesystem safety

Redux hardens state-changing operations through:

- atomic settings and `modsettings.lsx` writes with validation and rolling backups;
- staged imports so incomplete files are not presented as installed mods;
- backups before package replacement;
- recoverable and permanent deletion paths that update the interface only after filesystem
  success;
- safe fallback when referenced custom assets are missing; and
- release packaging checks that reject credentials, settings, logs, caches, backups, development
  symbols, and private local paths.

## Accessibility differences

Redux keeps upstream speech and screen-reader foundations while adding or reorganizing:

- a top-level Accessibility menu;
- first-run theme, online-feature, diagnostic, and accessibility setup;
- Atkinson Hyperlegible and adjustable interface text sizes;
- keyboard-operable Redux dialogs and a rebuilt shortcut editor;
- selectable dialog text and consistent focus behavior;
- lightweight realized-row automation for large virtualized mod lists; and
- shared reduced-motion and reduced-background-effects preferences.

## Targeted upstream corrections

Redux includes focused fixes for confirmed inherited defects, including:

- large archive imports that previously allocated the entire file at once;
- silent save/export failure when no profile or load order was selected;
- blank profile handling on clean installations;
- saved-order path mismatches across restart;
- drag state remaining locked after load failures;
- deletion of entries whose `.pak` file had already disappeared;
- incomplete Script Extender version comparison;
- refresh discarding unsaved order changes without confirmation; and
- early startup failures closing without a useful explanation.

The upstream tracking discussion is
[issue #11](https://github.com/circleainn/BG3ModManager-Redux/issues/11).

## Compatibility boundaries

Unless a change is explicitly scoped and regression-tested, Redux preserves upstream load-order
semantics, profile and campaign behavior, import/export formats, LSLib integration, `.pak` parsing,
game-path detection, launch behavior, and established user-data locations.

The following remain outside the current Redux delta or are intentionally deferred:

- public Nexus SSO authentication;
- automatic Redux self-updating during the private alpha;
- application localization;
- Linux, macOS, Wine, Proton, and self-contained .NET deployment; and
- automatic mod installation, repair, conflict resolution, or load-order reordering.

## Maintenance rule for this document

Update this page when Redux adds, removes, or materially changes an enduring difference from
upstream. Do not add version headings, patch notes, commit summaries, one-off bug narratives, or
planned ideas that do not yet describe the product.
