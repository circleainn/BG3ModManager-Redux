# Changelog

This file summarizes user-visible Redux releases. The issue tracker and Git history remain the
source for individual implementation details.

## Unreleased

### Fixed

- Restore **Copy Order to Clipboard** and **Export Order As List** to the visible load-order actions menu instead of hiding them on the sync-status control.
- Keep manual Nexus and mod.io linking bound to the exact right-clicked mod after it moves between Active and Inactive Mods, including while mod.io verification is in progress.
- Stop treating UUID-like mod.io archive filenames as legacy Nexus downloads and assigning unrelated Nexus project metadata.
- Restore the remembered named load order on startup instead of always selecting Current, preserving that order's active mods and separators.
- Keep provider-key fields synchronized with credentials loaded from encrypted storage after the Preferences controls are created.
- Retry remote mod thumbnails after download or decode failures instead of retaining a broken image in the session cache.

## 0.1.0-alpha.16.4.2 — 2026-09-14

A cumulative maintenance hotfix for separator persistence, controls, nested menus, and in-app updates.

### Fixed

- Keep active separators separate for every saved load order. Existing 16.4 separators migrate to the last-used order, while inactive organization remains global and automatic.
- Keep separators attached to their recorded sections after multi-mod moves and other active-list changes.
- Hide separators during text filtering, matching category and column-sorted views without changing the underlying order.
- Restore smooth Collapse All and Expand All motion without animating the recyclable list presenter, preventing the list from remaining dimmed or invisible.
- Add the matching bulk separator chevron to Inactive Mods and keep each pane's control synchronized with its own separators.
- Remove the cursor dead zone between submenu headers and their nested menus across Redux context menus.
- Retry update replacements while Windows releases short-lived file locks, replace read-only application files safely, and identify the exact blocked Redux file when an update still cannot proceed.

### Changed

- Pin repository builds to the supported .NET 8 SDK family so newer installed SDKs do not break the C++/CLI dependency build.

## 0.1.0-alpha.16.4.1 — 2026-09-14

A focused maintenance hotfix for separators and in-app updates.

### Fixed

- Keep active separators separate for every saved load order. Existing 16.4 separators migrate to the last-used order, while inactive organization remains global and automatic.
- Keep separators attached to their recorded sections after multi-mod moves and other active-list changes.
- Hide separators during text filtering, matching category and column-sorted views without changing the underlying order.
- Apply Collapse All and Expand All directly so a recycled list presenter cannot remain dimmed or invisible.
- Retry update replacements while Windows releases short-lived file locks, replace read-only application files safely, and identify the exact blocked Redux file when an update still cannot proceed.

### Changed

- Pin repository builds to the supported .NET 8 SDK family so newer installed SDKs do not break the C++/CLI dependency build.

## 0.1.0-alpha.16.4 — 2026-09-13

A larger update featuring overhauls of Save Game Manager and Download Manager, the new Nexus Collection Importer, persistent inactive organization, and a rebuilt Welcome Setup.

### Highlights

- **Save Game Manager overhaul:** a resizable details pane, party portraits and save facts, clearer campaign/save metadata, inline mod warnings, mod review, ZIP exports, and consistent right-click actions.
- **Download Manager overhaul:** streamlined toolbars and tab-specific actions, clearer package information and thumbnails, compact NXM/archive controls, Delete All, and a complete Nexus collection import workflow.
- **Nexus Collection Importer:** browse and select collection files, compare them with installed mods, track downloads, revisit recent collections, and review/save supported BG3 load orders.
- **Organization and onboarding:** automatically saved inactive ordering, consistent sorting/filtering, live appearance previews, interactive setup tours, and optional starter separators.

### Added

- Import Nexus collections into Download Manager with a compact file list, resizable details pane, collection thumbnail, author/category information, and a link to browse BG3 collections.
- Search collection files and filter by Missing, Installed, Unverified, or Needs attention. Select All and Deselect All apply to visible rows; hidden choices are preserved. Reset to Defaults restores the collection selection.
- Compare collection files against Nexus-linked mods in both Active and Inactive Mods. Exact installed files are skipped by default; different files and uncertain matches are identified separately. Game-directory matches remain unverified when exact file IDs are unavailable.
- Remember choices for the ten most recent collections. Reopening the same revision restores selections; a newer revision starts with current defaults and a review notice.
- Guide collection downloads awaiting Nexus authorization one file at a time, with live queue progress, page reopening, and batch retry for eligible download failures.
- Read supported BG3 load orders from collection manifests. Review active, inactive, missing, and unmatched entries before saving a separate order to Redux's Load Order dropdown, without activating mods or changing the current order.
- Add a right-hand save-details pane with the save screenshot, recorded location, game version, party members, levels, races, classes, and mod counts when available.
- Show companion portraits and race-based placeholders for generic party members. Generic names become readable race labels; unknown metadata is preserved rather than guessed.
- Review a save's recorded mods against the installed library. Show missing/inactive warnings and enrich known mod names, authors, and categories from the Redux database. Review remains available for any save with recorded mods, even without errors.
- Activate selected installed inactive requirements from Save Mod Review with Undo support, without replacing, saving, or syncing the current order automatically.
- Export selected saves or a campaign to ZIP, including thumbnails, with progress, cancellation, and checks for files that change during export.
- Add save-list context actions for Review Mods, Export This Save, Export Campaign, and Show in Folder.
- Persist inactive-mod ordering and separators automatically, using the existing drag-and-drop and Undo/Redo system. Inactive organization stays in Redux; Load Order Advisor remains active-only.
- Rebuild Welcome Setup around game discovery, theme previews, local/Nexus setup, interactive load-order practice, and a saves/native-mod tour.
- Add live onboarding Appearance controls for icon visibility, icons-only mode, category colors, gradients, fonts, and text size. Cancel restores the previous appearance.
- Add an Organize step explaining custom categories and separators. Optionally choose from nine starter sections: Foundations, Interface, Character Creation, Classes & Subclasses, Spells, Gameplay, Equipment, Visuals, and Patches. Sections append without moving mods or duplicating existing names.
- Reopen release notes through Help > What's New, with a remembered preference for whether they appear after updates.
- Expand the UUID-backed mod catalog with 499 entries, plus four corroborated category records and four library-name aliases. Refresh Advisor category evidence without importing unverified ordering constraints.

### Changed

- Streamline Download Manager: add actions stay at the top; Install All, Pause All, Resume All, and Delete All sit together in Downloads. Installed and Archives have their own cleanup action rows.
- Rename Inbox to Downloads, Remove to Delete for packages, Open Archives to Open Folder, and archive cleanup to Delete All with red destructive styling. Bulk download deletion confirms once and preserves installed content and retained archives.
- Show NXM association and archive retention as compact status/checkbox controls at the bottom of their relevant tabs. Use Nexus source styling for Paste NXM Link and Import Nexus Collection.
- Allow explicitly requested collection files to be downloaded again when old installed history exists, retaining the previous archive.
- Unify thumbnail sizes, title/metadata fonts, status placement, action spacing, and semantic button styling across Downloads, Saves, Game-Directory Mods, and Save Mod Review.
- Show save type, difficulty, campaign, date, and size as readable metadata. Identify autosaves/quicksaves, use muted campaign counts, and match collapse controls to the rest of Redux.
- Place save warnings consistently with game-directory warnings. Match row hover and selection to warning status, and smoothly transition Review Mods between muted, available, and warning states. Save Load Order uses the same transition in green.
- Give installable child windows matching drag-and-drop cards, icons, borders, dimming, and blur. Block drops into a dimmed owner while a child window is active, and keep confirmation dialogs with their owning window.
- Use matching clickable sorted-view notices in both mod panes. Make the # column optional, visible by default in Active Mods and hidden in Inactive Mods. Column-menu order follows the user's column arrangement, with icons/checkmarks and a consistent drag indicator.
- Merge Quick Links into Help > Links & Folders. Use Ctrl+1 through Ctrl+4 for Mods, Game, Extender Logs, and Saves; retain the Redux update-availability dot.
- Replace legacy yellow/blinking Script Extender menu text with a quiet missing-loader indicator. Move Generate Redux Database Contribution to Help.
- Simplify game-directory mod cards, replace bulky status pills with concise information, enlarge thumbnails, and clarify ownership and protected-backup status.
- Merge save file/folder installation under Install Save, and label folder navigation Saves Folder. Match the install menu's hover and selection colors to its action.
- Refine Preferences grouping, responsive toolbar layouts, dialog focus, keyboard/accessibility labels, and review wording. Keep actions reachable while long content scrolls.
- Use concise What's New headings, theme-aware release-note colors, and shared dialog backdrops. Hide release-control comments and internal announcement wording.
- Preserve Redux/platform/action icons throughout onboarding, respect hidden-icon settings, and use reduced-motion-aware page and expander transitions.
- Keep dev as a validation branch without downloadable release builds. Public releases belong to main, and Nexus descriptions include changes and additions as well as fixes.

### Fixed

- Restore reliable Save Manager context menus, including grouped campaign rows, standard icon/text styling, and Review Mods availability matching the toolbar.
- Keep malformed save mod records visible as Needs attention instead of dropping them. Distinguish empty saves from unreadable metadata.
- Remember the Script Extender export-default-values preference across restart and config reloads.
- Allow explicit Nexus-link reassociation after another manager takes ownership, even when Redux's old ownership marker remains.
- Stop identifying the base-game Bink DLL or an identical leftover backup as Native Mod Loader.
- Apply Parchment's default Segoe UI font during setup while retaining explicit font overrides. Fix stuck expander hover and incomplete expansion states.
- Correct clipped Nexus logos, missing menu icons, manager button-icon alignment, and theme/backdrop inconsistencies.
- Remove an invalid database ordering record and correct four library dependency-name aliases.

### Known limitations

- Collection installer rules and instructions are not applied. Only an explicit supported BG3 load order can be saved; a collection without one keeps Save Load Order unavailable. Follow the collection author's instructions.
- Free Nexus accounts still need a Mod Manager Download authorization for each file. Collection previews currently use the latest published revision; requested older revisions are rejected rather than silently substituted.
- Different Nexus file IDs do not prove that one file is a newer version. Native matches without exact file identities remain unverified.
- Save Mod Review compares recorded UUIDs; it does not verify dependency chains, required versions, or native mods. Save details and portraits depend on available metadata.
- Save exports are limited to 32 saves, 1 GB total, and 256 MB per file. Export larger campaigns in smaller selections.
- Native ownership records and protected backups remain local to each Redux installation. Switching folders does not migrate them.
- Unexpected administrator warnings (#95) still need reporter diagnostics.


## 0.1.0-alpha.16.3.5 — silent public-alpha maintenance release

### Changed

- Same-version reinstalls keep their placement and skip the clean-package review. Batch rows
  describe each package's action, and PAK counts sit with the other download metadata.
- Show a What's New window with GitHub release notes after an update.
- Prevent another Redux copy from starting for the same user session, including other versions;
  NXM links are routed to the running instance when it supports shared activation.
  Older releases cannot enforce this guard when launched after an updated copy.
- Show modal package progress throughout Install All, including preparation, and prevent
  conflicting workspace actions until the batch finishes. Download Manager's toolbar icon uses
  semantic success, warning, and error colors with explanatory tooltips.
- Nexus links can start downloads with a small notification without opening Download Manager
  or taking focus. Quiet handling is the default for new settings; saved foreground preferences
  are preserved. A quiet NXM launch starts Redux minimized.
- Download toasts appear above the taskbar for starts and completions, then fade away after
  four seconds. Click a toast to open Download Manager; reduced motion disables the fades.
- Remove routine download and clean new-mod installation confirmations. Updates, replacements,
  and packages requiring review still prompt before installation. Explain missing Nexus setup
  separately from Windows NXM link association.
- Simplify the update window with scrolling details and a consistent action footer, and avoid
  repeating its results in notification banners.
- Shorten onboarding explanations and let shared confirmation dialogs resize and wrap their
  actions when space is limited.

### Fixed

- Allow Download Manager to bring a maximized Redux window forward after a quiet startup.
- Read administrator elevation from the primary process token rather than a possible thread
  impersonation token, and log the token elevation type to help investigate unexpected warnings.
- Keep secondary-window content transparent through dismissal and prevent repeated or canceled
  close requests from starting competing transitions.
- Restore remembered window bounds on-screen, including monitors to the left of or above the
  primary display, and recover safely when saved bounds are unusable or a monitor is disconnected.
- Keep Save Game Manager discovery working when a save has an invalid package signature.
- Import loose saves from an archive into separate folders with only their matching thumbnails.
- Clean up eligible abandoned update folders for maintenance versions as well as single-part alphas.

## 0.1.0-alpha.16.3.4 — silent public-alpha maintenance release

### Fixed

- Kept an installed PAK mod's active or inactive state and load-order position when updating it
  through Download Manager. Newly installed mods still enter Inactive Mods.
- Made **Don't show again** on the administrator warning persist after restarting Redux.

### Changed

- Nexus uploads now include a short list of changes taken from the matching GitHub release notes.

## 0.1.0-alpha.16.3.3 — silent public-alpha maintenance release

- Let users deliberately reassociate NXM links with the current Redux when an older Redux install
  or another marked handler owns the Windows registration, while preserving the previous handler.
- Simplified repetitive Download Manager guidance.
- Corrected the current-theme summary to show an active custom theme's own name.
- Added the Redux star to the built-in icon library and used its theme-aware monochrome form on the
  startup screen.
- Gave the NXM reassociation action the same Nexus source styling used elsewhere in Redux.

## 0.1.0-alpha.16.3.2 — silent public-alpha maintenance release

- Prevented separators from being dragged into Inactive Mods.
- Cleared the insertion line immediately when Inactive Mods rejects a separator drag or drop.

## 0.1.0-alpha.16.3.1 — silent public-alpha maintenance release

### Fixed

- Removed the generic mod.io health warning because a linked source page does not prove that the
  installed package is subscribed through the in-game manager or managed by Steam Cloud.
- Added maintenance-release versions such as `.16.3.1` across the updater and Nexus publication
  contract, including deterministic ordering between their parent and the next hotfix.

## 0.1.0-alpha.16.3 — public-alpha hotfix

### Fixed

- Prevented an incidental mod.io `PublishHandle` from overriding a reviewed Nexus match when Redux
  is introduced to an existing mod installation.
- Added symmetric source controls: explicit Nexus/mod.io labels, persistent mod.io unlinking, and a
  **Change Source to Nexus Mods** action for mods currently displaying mod.io metadata.
- Expanded Redux's offline source catalog while refusing cross-provider and
  same-provider name collisions instead of guessing.
- Added conservative bundled mod.io recognition that requires a matching UUID and exact package
  name, folder, or filename and never overrides native or manually selected provenance.

## 0.1.0-alpha.16.2 — public-alpha hotfix

### Fixed

- Preserved the exact selected Nexus file name in the mod list when several files belong to the
  same Nexus page, while retaining the shared project title in source details.
- Added manual mod.io source linking, replacement, and clearing with URL/ID validation and explicit
  confirmation when replacing an existing Nexus link.

### Distribution

- Simplified public releases to one portable ZIP shared byte-for-byte by GitHub Releases and Nexus
  Mods. The separate Setup artifact is no longer part of future public releases.
- Added a protected Nexus Mods publishing workflow that downloads the approved GitHub release ZIP,
  refuses duplicate versions, uploads through Nexus Mods' official action, and records the resulting
  file-version ID.

## 0.1.0-alpha.16.1 — public-alpha hotfix

### Fixed

- Added an explicit public-alpha hotfix version contract (`alpha.N.H`) so corrected builds can be
  delivered through the updater without pretending to be a new feature alpha or replacing existing
  release bytes.
- Prevented an exceptional load-order rebuild during Sync—such as one involving legacy separator
  PAK state—from leaving Save, keyboard shortcuts, menus, and drag-and-drop permanently disabled.

### Distribution

- Setup now updates or repairs its existing registered Redux installation using verified,
  inventory-scoped replacement and rollback while preserving settings and other user content.

## 0.1.0-alpha.16 — public-alpha hotfix

### Fixed

- Welcome Setup now stays within the available desktop work area, can be resized, and keeps its
  navigation actions reachable on common laptop displays and non-default scaling.
- Screen-reader speech now uses the bundled Tolk bridge directly and falls back to Windows SAPI
  when the optional CrossSpeak wrapper is unavailable.
- The elevated-process warning now uses the process token's actual elevation state, appears once
  per startup, and provides working **Close** and **Don't show again** actions.
- Top-bar menus, toolbar dropdowns, nested submenus, and combo boxes now prefer rightward placement,
  with left/up placement retained only when required by a screen edge.
- Cancelling the first-run BG3 folder picker no longer lets Welcome Setup overlap Preferences;
  onboarding waits until Preferences has fully closed.

### Distribution

- Enabled Redux's verified public-alpha update checks now that the moving channel is live.
- Added short-lived CI artifacts for both the portable application and standalone Setup so release
  candidates come from the same tested commit.

## 0.1.0-alpha.15 — public alpha

### Distribution and identity

- Separate lightweight web Setup for verified fresh installation, .NET 8 prerequisite handling,
  per-user shortcuts, and inventory-scoped removal that preserves unlisted content.
- Strict public-alpha update-channel checks with quiet background scheduling, explicit release
  presentation, verified staging, restart-based replacement, transaction rollback, and post-update
  status reporting.
- Release-owned file inventories that keep application updates separate from settings, saved
  orders, downloads, retained archives, logs, backups, and user-created content.
- The desktop runtime is now `Redux.exe` / `Redux.dll`; Setup and Windows continue to present the
  installed product as **BG3 Mod Manager Redux**, and Setup recognizes legacy private-alpha
  installations that still contain `BG3ModManager.exe`.
- New transparent Redux star application icon and consistent product identity across the app,
  installer, repository, documentation, and public showcase materials.
- Direct Redux Discord access from the application and public project navigation.

### Refined

- App-wide button and modal consistency, including current semantic styling, action icons, close
  affordances, and disabled states in Preferences, category/separator editors, and manager windows.
- Load Order History styling now matches the current Redux review and organizer surfaces.
- Custom Theme Editor and color-picker shells now participate in live background-color previews.
- Expanded, deduplicated built-in category icon library and Segoe UI as Parchment's default font.
- Override Mods once again has a usable vertical resize grip after its visual refresh.
- Public installation, migration, rollback, removal, privacy, support, contribution, security,
  release, brand, community, architecture, and FAQ documentation.

### Fixed

- Save Game Manager deletion no longer keeps the selected save locked by its own thumbnail preview.
- Game-directory mod detection again evaluates the configured live game directory instead of an
  unrelated showcase path.
- Script Extender requirements use one dedicated row indicator and one complete diagnostic detail,
  removing duplicate or incomplete warning tooltips.

## 0.1.0-alpha.14 — private alpha

### Added

- Unified Download Manager intake for local packages and optional Nexus Mod Manager links.
- Guarded destination routing for ordinary PAK mods, saves, and reviewed game-directory packages.
- Queue persistence, resumable Nexus transfers, concurrent download limits, retry/recovery states,
  installed history, and source-aware thumbnails.
- Guarded **Install All** planning with dependency-aware skips and per-item results.
- Optional, quota-limited Package Archive Library with content-addressed deduplication and
  placement-preserving PAK reinstall.
- Reviewed game-directory mod catalog, archive inspection, installed-file recognition, guarded
  adoption, ownership records, repair, removal, and replacer rollback protections.
- Script Extender installation, update, reinstall, and adoption through Game-Directory Mod Manager.
- Save Game Manager campaign metadata, thumbnails, difficulty presentation, and guarded save import.
- Full-window drag-and-drop intake with destination-independent routing.

### Refined

- Main toolbar grouping, overflow parity, Quick Access coverage, action semantics, icons, spacing,
  and responsive layout.
- Shared window chrome and theme-aware move/resize feedback, modal transitions, focus restoration,
  deletion review, manager cards, semantic pills and action icons, provider actions, and
  disabled-state presentation.
- Override separator appearance, persistent membership behavior, bulk collapse/expand controls, and
  category-driven icon/color customization.
- Bundled typography choices: Manrope, Atkinson Hyperlegible, Archivo Black, IBM Plex Mono, and the
  Windows Segoe UI fallback.
- Reduce Motion coverage for lists, campaigns, windows, hover effects, and transitions.
- Live custom-theme preview and Ctrl+L theme cycling performance through coalesced, incremental
  palette updates.
- Quick Access hover and selection rails, semantic shortcut badges, and cleaner responsive
  mod-list headers now follow the same interaction language as Categories and manager lists.

### Fixed

- Download shutdown false positives, restart crashes, stale queue state, incomplete metadata, and
  foreground behavior.
- Save campaign expansion crashes and several modal close/focus issues.
- Deletion selection and disabled semantic-icon inconsistencies.
- Stuck drag/drop overlays and incomplete drop-review thumbnails.
- Hidden-toolbar parity, hover-card tag duplication, and shortcut chevron state.
- Mod-list column headers now finish cleanly across the scrollbar gutter while vertical tracks
  begin below the header strip.

### Safety

- Downloading and package intake remain separate from installation, activation, ordering, and game
  sync.
- Replacer installs back up only verified clean game files; Redux never adopts an unknown or modded
  DLL as a clean restoration source.
- Completed files and retained archives are hash-validated before use, while credentials and signed
  URLs remain outside persistent queue and archive records.

## Earlier private alphas

Earlier builds established Redux categories and separators, saved-order isolation, review-before-
sync workflows, restore points, Undo/Redo, Mod Diagnostics, the optional Load Order Advisor, Redux
Modlists, offline recognition, contribution reports, creator manifests, package preflight, custom
themes, Quick Access, and the first Redux visual system. Consult Git history for per-build detail.
