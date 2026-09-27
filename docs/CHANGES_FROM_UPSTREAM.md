# Changes from upstream BG3 Mod Manager

BG3 Mod Manager Redux is a Windows-only fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). Redux depends on
the upstream project's mature BG3 package and load-order foundation; it is neither a clean-room
rewrite nor an attempt to obscure that lineage.

This page documents **durable product and architectural differences**. It is not a changelog or a
claim that every visible behavior originated in Redux.

> [!NOTE]
> “Retained” means Redux continues to build on upstream behavior. “Extended” means Redux adds a
> guarded workflow or presentation layer around that behavior. “Redux-owned” identifies a system
> introduced for this fork.

## At a glance

| Area | Upstream foundation retained | Redux difference |
|:--|:--|:--|
| Mod management | PAK parsing, archives, active/inactive lists, profiles, campaigns | Structured drop review, safer staged replacement, unified selection and presentation |
| Load orders | Editing, saved orders, `modsettings.lsx` import/export | Explicit unsaved working state, review-before-write, restore points, comparisons, Undo/Redo |
| Organization | List ordering and filtering | Multi-category organization and persistent separators with one sub-separator level across all mod panes |
| Mod information | Nexus metadata, package metadata, dependencies, overrides | Unified diagnostics, mod.io/manual provenance, offline recognition, richer details surfaces |
| Guidance | Parsed dependency facts | Optional Load Order Advisor and separator-aware organization previews |
| Portability | Existing order formats | `.bg3redux` Modlists for selected Redux presentation and public source data |
| Saves | Profile and save-path discovery | Profile-aware Save Game Manager with campaign grouping and guarded imports |
| Game-directory mods | Game-path and Script Extender foundations | Reviewed native-package catalog with staged placement, ownership, rollback, and a separate manager |
| Download Manager | Nexus metadata and package-inspection foundations | Shared local/NXM inbox, persistent resumable network queue, verified archives, and guarded destination routing |
| Interface | Existing WPF application and accessibility foundations | Redux design system, themes, custom appearance, Quick Access, motion controls |

## The upstream core Redux preserves

Redux continues to rely on upstream systems for:

- profiles, campaigns, active and inactive lists, saved orders, and normal list editing;
- `modsettings.lsx` and supported load-order import/export;
- PAK metadata parsing and archive workflows through LSLib;
- BG3, profile, save, order, and Mods-folder discovery;
- override and force-loaded packages, dependency metadata, UUID checks, Osiris/Mod Fixer detection,
  and Script Extender integration;
- game launch behavior and inherited update-provider foundations;
- Nexus Mods metadata, caching, images, and links; and
- configurable shortcuts, speech, and screen-reader foundations.

Unless a change is explicitly scoped and tested, Redux preserves these semantics and established
user-data locations. Upstream copyright, attribution, license terms, and third-party notices remain
intact.

## Redux-owned interface and organization

Redux introduces a shared visual system across its main window and dialogs:

- semantic success, warning, error, information, and accent colors, including contextual hover and
  selection treatments in Quick Access;
- consistent typography, spacing, corner radii, controls, menus, tooltips, window chrome, and
  notifications;
- Redux Dark, Redux Light, Parchment, and persistent custom themes;
- optional generated gradients, imported fonts, scalable text, and reusable custom PNG icons;
- a reorganized, responsive toolbar with consistent action clusters and a parity-preserving overflow
  menu, plus consolidated top-level menus and searchable Quick Access for commands, profiles,
  orders, categories, mods, themes, folders, and support links;
- richer hover information and a persistent selected-mod details drawer; and
- a unified Lucide-based icon language while preserving official provider branding where relevant.

Appearance choices propagate through Redux-owned utility and review windows. Hiding interface
icons removes their reserved layout space where appropriate; colored-text and colored-interaction
preferences also govern category and semantic presentation in Quick Access. Redux Debug Mode and
its diagnostic-information window provide a clearer troubleshooting boundary than the inherited
developer-mode presentation.

Redux list surfaces retain virtualization and logical scrolling while applying bounded render-only
wheel motion. Reduce Motion removes scrolling, sliding, scaling, and transition animation without
removing clear hover or selection feedback.

Theme switching and live custom-color preview reuse unchanged semantic resources, coalesce related
setting changes, and update the main visual tree once per rendered frame. Hidden secondary windows
receive the current theme when opened instead of participating in every palette change.

### Categories

Redux categories are a persistent presentation layer with automatic and user-created categories,
multiple assignments per mod, names, descriptions, colors, icons, display order, filtering, counts,
and category-aware selection. Missing or removed assets have explicit fallback behavior.

### Separators

Redux separators are named visual markers with descriptions, colors, icons, durable membership,
and persistent collapse state. They never enter `modsettings.lsx` and are never treated as mods.
They work in Active, Inactive, and Override Mods. A separator can contain one level of
sub-separators, and collapsing a parent hides its whole branch. Existing separators can remain
specific to a saved order or share their appearance across orders while keeping independent
placement and collapse state in each order.

- An expanded separator moves only its marker.
- A collapsed separator moves with its sealed contents.
- Moving a closed group does not absorb unrelated destination rows.
- Rows placed next to a closed separator remain visible until the group is expanded.
- Pane controls and assignable shortcuts can collapse or expand separators in Active, Inactive,
  and Override Mods.

## Deliberate load-order workflow

Redux extends upstream load-order editing with an explicit working state:

- edits remain unsaved until the user presses **Save**;
- closing or replacing a dirty working order requires confirmation;
- saved orders can be created, renamed, deleted, compared, and opened in their folder;
- bounded Undo/Redo covers activation, deactivation, movement, separators, organizer changes, and
  guarded writes to the game order;
- **Sync Load Order to Game** previews activations, deactivations, placement changes, automatically
  included dependencies, and relevant diagnostics before writing;
- per-profile restore points are created before confirmed writes and can also be created on demand;
  and
- an undo refuses to overwrite `modsettings.lsx` if BG3 or another manager changed it afterward.

Redux Modlists add a portable `.bg3redux` format for a saved order and user-selected category,
separator, custom-icon, public-source, and optional-note data. Import choices remain independent.
Bundles are validated and never contain installed PAKs, saves, profiles, API keys, or
`modsettings.lsx`.

Redux also gives Override PAKs a separate pane and a per-saved-order enabled selection. Disabled
Overrides move to managed storage through reviewed file operations, so a package can be inactive
for one order without its direct file overrides remaining in BG3's Mods folder.

## Diagnostics and optional guidance

Redux's built-in Mod Diagnostics unifies package facts that were previously scattered across
different UI paths. It reports detectable dependency, UUID, Script Extender, creator-manifest,
declared-conflict, Mod Fixer, override, and provider-safety conditions through consistent severity
and follow-up actions.

Diagnostics are read-only. A user may explicitly reveal or activate an installed dependency, open
a reviewed source, or copy a UUID, but Redux does not silently download, repair, remove, activate,
or reorder mods.

The opt-in **Load Order Advisor** is a separate experimental rule family. It combines exact package
declarations with reviewed offline dependency and ordering facts. Its organizer can preserve current
separators, create non-empty suggested separators, or remove separators. Every result is previewed;
the preview counts only separators that it creates, removes, or actually repositions. Applying it is
one undoable, unsaved action. Advice can be ignored per relationship without disabling other
diagnostics.

An on-demand **Active File Overlaps** inspector also reports shared internal paths across active and
override PAKs. It describes possible interactions, not confirmed conflicts.

## Source identity and offline knowledge

Redux extends provider metadata with explicit provenance states: manual, native, cached,
creator-supplied, reviewed-database, and Local. Resolution is conservative and honors manual user
choices. Provider metadata cannot replace a package's parsed identity or change its load order.

The bundled database supports:

- exact installed-PAK and downloaded-archive fingerprints;
- reviewed module identities;
- corroborated community identity candidates;
- reviewed missing-dependency source links; and
- exact dependency aliases, substitutes, ordering groups, and author-supplied placement facts.

Online Nexus Mods and mod.io information can be disabled without deleting cached associations.
Privacy-limited `.bg3redux-report` contributions omit credentials, private paths, profiles, notes,
settings, and load-order data and still require maintainer review.

## Save Game Manager

Redux adds a profile-aware save browser over upstream path discovery. It groups story saves by
campaign, uses available WebP thumbnails and `SaveInfo.json` difficulty metadata, remembers campaign
collapse state, and provides compact toolbar actions.

Save folders, loose LSV files, and supported ZIP/7z/RAR/TAR/GZip-family archives can be installed
through a picker or drag and drop. Save and mod drops use distinct structured reviews; mixed drops
are rejected rather than guessed. Imports are staged, unsafe or oversized archive content is
rejected, existing names require confirmation, and deletion uses the Windows Recycle Bin. Redux
does not modify save contents or include saves in portable Modlists.

## Game-directory mod management

Redux adds a separate manager for reviewed packages that place native or root-level files under the
BG3 installation. These files do not participate in PAK ordering and never appear as reorderable
Active or Inactive Mods entries.

The reusable Core transaction layer validates canonical destinations, archive bounds, exact
catalog layouts, link and traversal safety, and AMD64 DLL headers. It stages files first, fingerprints
the archive, prerequisites, current destinations, and ownership state, then revalidates immediately
before commit. BG3 must be closed. Writes receive transaction backups and a durable journal;
partial failures roll back completed writes, while unresolved interruptions are surfaced as a
recovery-required state.

Redux's versioned ownership record distinguishes managed, externally installed, changed, and
missing files. Removal is offered only while current managed files still match what Redux installed;
files Redux added are deleted, while recoverable originals are restored without overwriting unrelated
external edits. User configuration files are placed only when missing, remain user-owned, and survive
updates or removal. Mixed native-and-PAK archives route the PAK portion through the existing normal
mod importer rather than bypassing package validation.

The catalog also carries exact reviewed DLL sizes and SHA-256 fingerprints. These distinguish related
projects that deliberately share filenames, surface known versions, and leave unknown variants
unclaimed. Exact add-only plugins installed by another tool can be adopted without changing game
files; Redux records their current DLL hashes, leaves configuration and companion PAKs user-owned,
and later deletes only unchanged adopted DLLs.

Catalog entries that replace existing game files follow a separate rule. External replacers cannot be
adopted because Redux has no trusted original to restore. Their manager status explains the recovery
order: remove the replacer, verify BG3 through Steam or GOG, then install through Redux. A Redux-led
replacer install first proves that every replacement target is a known clean game file, stores the
user's own original in protected backup storage, and refuses unknown or already-modded targets. No
BG3 binary is distributed with Redux.

The manager is available from Tools, Quick Access, the **Mods & Campaign** toolbar group, and the assignable shortcut
system. Its compact entries use the same semantic pills, source identity, destructive actions, and
hover or selection language as Redux's other mod surfaces. Available provider metadata can enrich
recognized entries with a project thumbnail, summary, author, version, and update time; thumbnail
frames use the same 16:9 language as Redux's other visual mod and save surfaces.

Inspect Mod Package reuses the guarded layout catalog to distinguish reviewed game-directory
packages, unreviewed DLL archives, and native-and-PAK hybrids without executing or installing them.
It also reuses Save Game Manager validation for save archives and loose `.lsv` files, while retaining
the existing PAK preflight behavior for ordinary mod releases.

The initial catalog is intentionally narrow and based on reviewed archive layouts. Matching a
layout and executable architecture does not establish publisher provenance or guarantee that a mod
is safe for a particular system. Script Extender uses the same guarded staged transaction and
ownership record as other reviewed game-directory packages. Its install/update action now lives in
Game-Directory Mod Manager, and the Tools shortcut opens that manager action rather than maintaining
a second download-and-install path.

## Download Manager and Nexus Mod Manager links

Redux adds optional per-user `nxm://` registration with an ownership marker, repair support, and
restoration of a previous handler where possible. Non-BG3 links are forwarded only when the saved
handler can be invoked safely. BG3 links are delivered to the existing Redux process rather than
starting a second full application instance.

The Redux-owned Download Manager combines local PAK, LSV, ZIP, 7z, RAR, TAR, and GZip intake with
optional NXM acquisition. Local files are copied into the managed Downloads folder; both local and
network entries carry a source kind, stable archive identity, inspection result, detected content
kind, destination, and independent installation state. The pipeline strictly separates link
receipt, metadata resolution, transfer, package inspection, installation, activation, ordering,
and game sync. Local intake also resolves available installed or bundled source artwork before
installation so the shared inbox can present a known package consistently across acquisition paths.
Its versioned queue uses
atomic persistence with recovery for corrupt or interrupted state, bounded FIFO concurrency,
pause/resume/retry controls, validated range requests, transfer limits, and publish-by-rename.
Completed files receive a SHA-256 identity and are revalidated before installation. Temporary NXM
authorization keys and signed download URLs remain memory-only and are excluded from settings,
queue data, logs, and error details. On application exit, Redux pauses and persists only active
queue work; finished downloads and installed-history records cannot hold shutdown open.

**Download Manager** is available from Tools, Quick Access, the **Mods & Campaign** toolbar group,
and the assignable shortcut system. Files can also be added with its picker or dropped onto either
the manager or the unified full-window install target in the main Redux window. Pane location no
longer selects an Active or Inactive destination. Finalized packages are inspected automatically: ordinary packages
enter the established inactive-mod review and import path; reviewed native packages and Script
Extender use the guarded game-directory transaction; and save packages use Save Game Manager.
Mixed, ambiguous, corrupt, or unsupported content is blocked without filesystem changes. Installed
entries move into a separate history tab that can be cleared without changing the Package Archive
Library or uninstalling content. When archive retention is off, the successfully installed managed
inbox copy is removed while a user-owned local original remains untouched. The clear-history action is available with the Installed view
rather than consuming space inside the history list. Downloading or installing never activates,
reorders, or syncs a mod automatically.
Retained Download Manager archives can be reinstalled directly from history. Explicitly adding an
identical local archive returns its existing record to the inbox and refreshes its classification
and artwork instead of duplicating it.

The guarded **Install All** action preflights every eligible inbox entry before destination files
change, then presents one destination-grouped confirmation. It detects duplicate archive hashes,
overlapping PAK/native/save identities, missing dependencies, unsafe layouts, missing profile/game
configuration, and other blocked inputs. Safe entries are committed serially through the existing
inactive-PAK, Save Manager, or Game-Directory Manager services. Failures are isolated, skipped and
failed entries remain in the inbox, and one final per-item report replaces a dialog chain. Batch
installation never activates, orders, or syncs PAK mods.

The optional long-term **Package Archive Library** is a third, independent ownership concern. It is
off by default and creates no directory until a successfully installed package is retained. Verified
packages are stored by SHA-256, deduplicated, and indexed with safe source identity, version,
package kind, destination, and install time. The configurable 1–100 GB quota prunes least-recently-
used packages. Download Manager's Archives tab reports usage and supports reinstall, open-folder,
an inline retention preference, and explicit clear actions. Successful retention moves the managed inbox copy into the library;
local originals remain where the user put them. Clearing installed history, clearing archives, and
uninstalling content remain independent. The archive index excludes source paths, credentials,
temporary authorization, signed download URLs, and remote thumbnail URLs.

Retained PAK reinstall is contextual rather than a forced inactive transfer. Existing packages keep
their active/inactive state and active load-order position; packages no longer installed use the
safe Inactive Mods default. Redux does not sync that preserved working state automatically.

Clean, brand-new PAK batches have a distinct success review and an opt-out that can be reversed in
Preferences. The bypass is deliberately narrow: updates, replacements, downgrades, unreadable
packages, and any warning-bearing batch continue to require review.

## Filesystem and privacy hardening

Redux applies staged, validated, or atomic replacement to settings, saved orders, imports,
`modsettings.lsx`, keybindings, provider caches, Script Extender configuration, and active-mod ZIP
backups. Same-destination writes are serialized so overlapping operations cannot expose partial
content. Update and deletion results are reported only after the underlying filesystem action
succeeds.

Provider API keys are stored outside ordinary settings using Windows account-protected storage.
Diagnostic exports and release packaging checks reject credentials and other private runtime data.

## Accessibility additions

Redux retains upstream speech and screen-reader support while adding:

- a top-level Accessibility menu and first-run setup;
- a focused font set—Manrope, Archivo Black, IBM Plex Mono, Atkinson Hyperlegible, and the system-provided Segoe UI—plus imported fonts and adjustable text size;
- keyboard-operable Redux dialogs and a searchable, grouped shortcut editor with remembered group
  state and group-wide expand/collapse controls;
- selectable dialog text and consistent focus behavior;
- lightweight automation for realized rows in large virtualized lists;
- independent reduced-motion and reduced-background-effects preferences, including the unified
  full-window drop target; and
- one full-window accent frame for install drops and native move/resize feedback, including the
  custom title bar.

## Targeted upstream corrections

Redux includes focused fixes for confirmed inherited problems, including large-archive memory use,
blank-profile startup, saved-order path mismatches, stuck drag/toolbar state after failures,
deleting already-missing PAK entries, Script Extender version comparison, refresh with unsaved
changes, and startup failures without useful feedback.

The upstream tracking discussion is
[issue #11](https://github.com/circleainn/BG3ModManager-Redux/issues/11).

## Current boundaries and non-goals

Redux currently does not provide:

- public Nexus SSO authentication;
- automatic dependency downloading, compatibility repair, or conflict resolution;
- silent load-order sorting or automatic game-file export;
- application localization;
- Linux, macOS, Wine, or Proton support; or
- a self-contained .NET distribution.

This document should change only when an enduring upstream/Redux boundary changes. Version notes,
individual fixes, plans, and one-off implementation details belong in release notes, issues, or Git
history.

See [Installation and updates](INSTALLATION.md) and [Privacy and local data](PRIVACY_AND_DATA.md)
for current distribution and data-handling guidance.
