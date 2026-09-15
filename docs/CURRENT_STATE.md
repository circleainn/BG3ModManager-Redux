# Current project state

This page records the decisions and boundaries that define Redux today. It is the first reference
to check before changing established behavior. The [changelog](CHANGELOG.md) records what shipped,
the [issue tracker](https://github.com/circleainn/BG3ModManager-Redux/issues) tracks individual
reports and proposals, and the source and tests remain authoritative for implementation details.

Last reviewed: September 15, 2026. Release candidate: `v0.1.0-alpha.16.4.4`.

Save Manager now exports selected saves or a selected campaign to ZIP, including thumbnails,
with progress and cancellation. Export retains the import limits: 32 saves, 1 GB total and
256 MB per file; larger campaigns require smaller selections. Round-trip and cancellation
regressions pass; real campaign export/restore and cancellation still need a live check.

## Development status

Welcome Setup includes an optional starter-separator selection after the load-order tour. Chosen sections append to Active Mods through the existing separator/Undo workflow; matching active-section names are skipped and existing mods are not reordered. Canceling setup adds nothing. Downloads bulk deletion confirms once, skips installed/installing entries, and reuses the existing cancellation and package recycling path.

Collection previews support text search and status filters, with visible-row bulk selection and preserved hidden choices. Batch recovery retries eligible failed downloads only, excluding installation and rollback failures. Saving a collection order now opens the shared mod-review window in a read-only review mode before confirmation; it retains missing entries and never activates mods or changes the current order.

Collection installer groundwork now includes a BG3 collection-link parser and a bounded Nexus
GraphQL preview reader. It preserves multiple files per mod and unavailable entries, rejects
partial responses, and does not substitute the latest revision for a requested older revision.
Download Manager now opens a themed collection preview with optional-file selection and
duplicate-queue checks, and sends selected files through the existing NXM queue. Free accounts
still require file-specific Nexus authorization. Live download verification,
older revisions, collection manifest instructions, and shared preview rate-limit integration remain.
The collection window can read explicit enabled BG3 UUID ordering from collection.json and save it as a separate Redux order. It does not apply the order or modify inactive mods. Metadata preview has been verified against a live collection; authenticated manifest retrieval has now been checked against pns4qv revision 150 (its loadOrder array is empty). The same reader successfully parsed 1,589 ordered entries from DUNGEON (f3iqts), revision 45. End-to-end saving and choosing a populated order in the running app still needs a check. Installer rules are not applied.

Collection choices are stored separately from download authorization for the ten most recent collections. Reopening restores the last link; Recent collections imports a saved collection and restores selections only for the same revision. Changed revisions show a review notice and use current defaults.

Collection imports now guide queued files awaiting Nexus authorization one page at a time. The guide advances on queue-state changes, supports reopening the current file, and derives progress from existing queue records. End-to-end free-account NXM handoff still needs a live check.

Collection previews compare exact Nexus mod/file IDs against existing PAKs in both active and inactive panes and mark uncertain same-project matches separately. Game-directory detections have no exact file ID and remain unverified. No newer-version claim is inferred from file ID ordering. Installed history can be reacquired through the existing download pipeline if the installed file is no longer detected.

The accumulated work shipped in the alpha.16.4 update. Dev runs build and regression
checks but publishes no downloadable portable build or release; main owns public releases.
The Unreleased changelog is reserved for work after the 16.4.4 maintenance release.

Alpha.16.4.1 fixes separator regressions reported in #121 and #123: filtered views hide separators,
bulk collapse/expand no longer fades the whole recycled list, established sections re-anchor to their
recorded mods, and active separators are stored with each saved load order. It also hardens the
incoming updater against short-lived Windows locks and read-only application files for #122.

Alpha.16.4.2 restores safe bulk separator motion using realized rows, adds the matching bulk
collapse/expand control to Inactive Mods, and removes the cursor dead zone between nested menu levels.

Alpha.16.4.3 restores the visible load-order copy/export commands, remembered startup order,
right-click source targeting, asynchronously loaded credential fields, and retryable remote thumbnails.
It also prevents UUID-like mod.io archive names from being interpreted as legacy Nexus download IDs.

Alpha.16.4.4 is cumulative. It retains every alpha.16.4.3 correction, repairs the detailed-list
export binding, keeps hidden built-in categories manually assignable, and checks for updates once
per app launch when the automatic preference is enabled.

Inactive ordering and separators (#111), Script Extender export preference persistence (#119),
and NXM reassociation recovery (#120) shipped in 16.4 and their issues are closed. Shared window
refinement (#113) and collection importing (#108) are also closed for the accepted 16.4 scope.
Direct collection NXM activation is not implemented; it remains a possible separate enhancement.
The broader UI pass still benefits from live checks with custom themes and enlarged text. Issue
#95 is closed as resolved for now at the maintainer’s request; reopen if a current-build report recurs. Native ownership and protected backups remain
local to each Redux installation; changing to another folder does not migrate those records.

The September 15 reports cover separate causes rather than one common regression. Alpha.16.4.3
addresses the reproducible load-order action, startup selection, source-link targeting, credential,
and thumbnail retry paths. Remaining sync-specific and missing-source-image reports still require
their own evidence. See [the next-update audit](NEXT_UPDATE_AUDIT.md) for current issue status.

## Current release

Save Mod Review reads save metadata and offers reviewed activation of installed inactive mods
using existing ordering and Undo behavior. It does not verify versions, dependencies, or native
mods, and never saves or syncs automatically.

| Item | Current value |
|:--|:--|
| Product | Baldur's Gate 3 Mod Manager Redux |
| Short name | Redux |
| Latest version | `0.1.0-alpha.16.4.4` |
| Lifecycle | Public alpha |
| Supported platform | Windows 10/11 x64 |
| Required runtime | .NET 8 Desktop Runtime |
| Application | `Redux.exe` |
| Distribution | One portable ZIP |
| Public sources | GitHub Releases and Nexus Mods |
| Update channel | `public-alpha` |
| Active milestone | `v0.1.0 – Public Alpha` |

The maintenance release tag is `v0.1.0-alpha.16.4.4`. Always verify the live branches and releases before
preparing another publication.

## What Redux is

Redux is a community fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). It keeps the
proven package, profile, and load-order foundation while building a more cohesive Windows
application around organization, review, recovery, customization, and accessibility.

The current public alpha includes:

- categories, separators, filtering, configurable columns, and a resizable mod-details drawer;
- explicit working changes, saved-order comparison and history, review-before-sync, restore points,
  and bounded Undo/Redo;
- built-in Mod Diagnostics and a separately enabled, preview-first Load Order Advisor;
- a shared Download Manager for local packages and optional NXM downloads;
- a Nexus Collection Importer with selective downloads and supported BG3 saved-order review;
- Save Game Manager and a guarded Game-Directory Mod Manager;
- dark, light, parchment, and custom themes with accessibility and motion controls;
- conservative offline mod recognition and privacy-limited contribution reports; and
- safe in-app updates for existing portable installations.

See [Changes from upstream BG3 Mod Manager](CHANGES_FROM_UPSTREAM.md) for the complete feature
comparison.

## Repository layout

Keep new files with the part of the project that owns them:

| Location | What belongs there |
|:--|:--|
| Repository root | Git configuration, the main `README.md`, the project `LICENSE`, solution/build entry points, and shared MSBuild configuration |
| `docs/` | Current user, mod-author, contributor, security, support, and release-process guides |
| `docs/releases/` | Historical notes for one published version |
| `docs/assets/` | Public documentation and Nexus page media that must have stable repository URLs |
| `docs/schemas/` | Public schemas documented for creators or contributors |
| `licenses/` | Third-party license texts and the third-party notices inventory |
| `src/` | Application, core, updater, and toolbox source |
| `tests/` | Automated regression projects and their fixtures |
| `tools/` | Standalone maintainer and database utilities, with tool-specific documentation beside them |
| `.github/` | Issue forms, pull-request templates, and GitHub Actions workflows |
| `External/` | Attributable upstream or vendored dependencies and submodules |

The root `LICENSE` is intentionally separate from `licenses/`: it covers Redux itself and is the
standard location recognized by GitHub. The files under `licenses/` cover bundled third-party work.
Likewise, `src/GUI/Redux.ico` is the Windows application resource, while
`docs/assets/redux-star.svg` is the reusable public documentation mark.

Generated ZIPs, manifests, build directories, logs, user settings, downloaded packages, local
reports, and promotional working files are outputs rather than source. They stay ignored and must
not be committed. A tool-specific README can remain beside its tool; general project guidance
belongs in `docs/` and should be linked from the documentation index.

## Decisions that are already settled

### Installation and updates

- Public Redux builds are portable-only. The old Setup experiment is retired and is not a release
  artifact.
- A fresh installation is a complete ZIP extraction into a writable folder such as
  `C:\Modding\Redux`.
- The built-in updater updates an existing portable folder. It is not a fresh installer and must
  never claim user-created state.
- GitHub Releases and Nexus Mods receive the same approved ZIP. The moving update-channel manifest
  is published only after the versioned package is available and verified.
- The public executable is `Redux.exe`. `BG3ModManager.exe` is a legacy private-alpha name and must
  not appear at the root of a current release.
- Application folders, Redux state, BG3 Mods, profiles, saves, retained archives, managed
  game-directory files, and provider credentials are separate ownership domains.

Current installation and removal instructions are in [Installation and updates](INSTALLATION.md).
Older release notes are historical records and may describe artifacts that are no longer offered.

### Versioning and announcements

- Published ZIPs are immutable. A corrected build receives a higher version rather than replacing
  different bytes under an existing version.
- Two-part alpha updates such as `.16.4`, `.16.5`, and `.16.6` are normal announced releases.
- Three-part updates such as `.16.3.4` or `.16.4.1` are quiet maintenance releases. Their GitHub
  notes include `<!-- redux:no-announce -->` so the Redux Helper Bot records the version without
  posting or pinging.
- A quiet maintenance release is still a normal Latest GitHub release, Nexus upload, and update
  channel target.
- The next announced alpha is not assumed to be ready merely because a higher version number has
  appeared in a local filename or build folder.

The full publishing and recovery contract is in
[Release process and update recovery](PUBLIC_ALPHA_RELEASES.md).

### Mod intake, placement, and source identity

- A clean new PAK install enters Inactive Mods. Updating or reinstalling an installed PAK preserves
  its active or inactive state and load-order position.
- Downloading, inspecting, retaining, reinstalling, or importing a Modlist never silently
  activates, reorders, saves, or syncs a mod.
- Source recognition is conservative. When evidence is not strong enough, a mod remains **Local**;
  a similar filename or title is not enough to assign Nexus Mods or mod.io.
- Nexus Mods and mod.io are both valid sources. Users can link, change, or unlink supported source
  pages explicitly.
- Creator manifests and contribution reports provide evidence for review. They do not override
  parsed package identity or authorize automatic database changes.
- Categories and separators are Redux presentation data and never enter `modsettings.lsx`.
- Separators can organize Active and Inactive Mods. Inactive ordering is saved in Redux settings,
  shared across saved active orders, and never exported to the game. Column sorting is view-only.
  Closed separator blocks move within their current pane; invalid drops leave no drag marker.
- Active separators are stored per saved load order. Loading a game-derived order is not the same
  as reopening a named Redux order containing that presentation data. Reports of unexpected startup
  selection or separator loss are tracked in #127, not treated as permission to merge Save and Sync.

### Saving, syncing, diagnostics, and advice

- **Save** updates the selected Redux saved order. **Sync Load Order to Game** is a separate,
  reviewed write to `modsettings.lsx`.
- Mod Diagnostics is built-in and read-only. It explains known package facts but does not repair,
  download, remove, activate, reorder, or sync anything.
- Load Order Advisor is optional and experimental. Its organizer is deterministic, preview-first,
  separator-aware, undoable, and unsaved until the user chooses to save. The Advisor operates only
  on Active Mods and must leave inactive ordering and separators untouched.
- Unknown relationships remain unknown. Guidance must not imply certainty that the available data
  does not support.
- Redux Modlists can carry order and selected presentation data, but never PAKs, saves, profiles,
  credentials, or `modsettings.lsx`. Importing source links remains opt-in so existing associations
  are preserved by default.

### Native and game-directory mods

- Game-Directory Mod Manager accepts only reviewed layouts and shows the destinations it will
  change.
- Unknown or modified DLL layouts stay unverified and unmanaged.
- Redux may adopt an exact reviewed add-only plugin without rewriting it. It never adopts an
  external replacer because Redux did not preserve the original game files.
- Layout recognition is not a malware scan or a publisher signature. Users remain responsible for
  trusting the source of native code.

### Interface and visual system

- Shared semantic resources, modal chrome, menu behavior, buttons, and icons are preferred over
  one-off window styling.
- Theme colors must update live. Icons inherit the appropriate semantic or accent color unless a
  provider-specific interaction deliberately supplies one, such as Discord or Nexus hover color.
- Primary, neutral, destructive, success, warning, information, provider, and category actions
  retain distinct meaning across themes.
- Buttons use consistent spacing, icon placement, focus states, disabled states, and labels.
- Windows and popups must remain usable at common laptop resolutions, Windows scaling levels, text
  sizes, and reduced-motion settings without clipped actions or unreachable content.
- The transparent Redux star in `docs/assets/redux-star.svg` is the canonical mark. New copies of
  the same asset should not be scattered through the repository.
- Public screenshots and documentation show the real application with real data. Synthetic paths,
  fake identifiers, and UI that does not match the current build are not used as product evidence.

## Current reports and planned work

The issue tracker is the live source. The 16.4 audit closed #108, #111, #113, #119, and #120 for the
shipped scope. #95 is resolved for now based on the maintainer’s assessment, and #118 is not planned.
Earlier fixes for window placement and save archive/discovery defects (#112, #114–116) remain closed.
The separator and updater scopes in #121–123 also remain closed for their documented maintenance
fixes; a matching current-build recurrence is needed before reopening those completed scopes.

### Open bug investigations

| Issue | Current evidence and scope |
|:--|:--|
| [#127](https://github.com/circleainn/BG3ModManager-Redux/issues/127) Sync/restart order and separators | Alpha.16.4.3 restores the remembered named startup order. The separate Sync-specific report remains open for exact reproduction details. |
| [#130](https://github.com/circleainn/BG3ModManager-Redux/issues/130) Missing mod thumbnails | Alpha.16.4.3 retries temporary image download/decode failures. The issue remains open to identify mods whose metadata supplies no usable image URL. |

Issues #124, #125, #128, and #129 have focused alpha.16.4.3 fixes. Issue #126 is closed as the
documented expanded-header/collapsed-section behavior. The [troubleshooting guide](TROUBLESHOOTING.md)
provides non-destructive checks for remaining reports.

### Scoped requests awaiting evaluation

- [#131 — Assignable filename-display shortcut](https://github.com/circleainn/BG3ModManager-Redux/issues/131)
- [#132 — Optional hover-card descriptions](https://github.com/circleainn/BG3ModManager-Redux/issues/132)
- [#133 — Controls for automatic categories](https://github.com/circleainn/BG3ModManager-Redux/issues/133)
- [#134 — Compact category-tag rows](https://github.com/circleainn/BG3ModManager-Redux/issues/134)

These requests have no release commitment. #134 does not reopen the broader compact-interface
redesign in #118. PT-BR interest and a Chinese-version offer are recorded under #56; neither
establishes an integrated upstream translation. Interest in VOLO cooperation is not a partnership
or an accepted integration task.

### Open planned work

- [#110 — Improve compatibility with Wine and Linux desktops](https://github.com/circleainn/BG3ModManager-Redux/issues/110)
- [#109 — Manage Override mods when switching saved load orders](https://github.com/circleainn/BG3ModManager-Redux/issues/109)
- [#98 — Add official Nexus Mods SSO account connection](https://github.com/circleainn/BG3ModManager-Redux/issues/98)
- [#63 — Explore a docked or paged Managers workspace](https://github.com/circleainn/BG3ModManager-Redux/issues/63)
- [#56 — Expand localization and accessibility support](https://github.com/circleainn/BG3ModManager-Redux/issues/56)

An open issue is not permission to broaden its scope. Confirm its current discussion, labels, and
acceptance state before implementing it.

## Current limits and non-goals

- Nexus authentication currently uses a personal API key; official SSO remains planned work.
- Windows 10/11 x64 is the supported platform. Wine may work for some users, but Linux, macOS,
  Wine, and Proton are not currently supported release targets.
- Provider, category, dependency, and advisor knowledge is intentionally incomplete rather than
  filled with guesses.
- Redux does not promise automatic compatibility repair or a universally correct load order.
- The limited Nexus Collection Importer shipped in 16.4. Direct collection NXM activation,
  historical-revision browsing, and automatic application of collection installer rules are not
  shipped features. Follow collection-author instructions.
- Automatic management of inactive Override mods between orders and a new Managers workspace
  remain planned, not shipped.
- The retired Setup project and its dedicated tests are no longer retained in the repository.
  Installer work should not be reintroduced without a new decision about distribution and signing.

## Before changing established behavior

1. Read this page, the relevant user guide, and the current issue discussion.
2. Confirm the behavior is not an intentional safety or ownership boundary.
3. Check the current source, tests, branch state, and release rather than relying on an old build or
   document.
4. Report newly found problems with evidence before folding unrelated fixes into an active task.
5. Keep implementation changes focused. Do not publish releases, move update channels, close
   issues, or change public services unless that work is explicitly part of the task.
6. Preserve unrelated local changes and submodule state.

## Which source wins

When two references disagree, use this order:

1. current source and tests for actual application behavior;
2. current release files and update manifest for public distribution;
3. this page for accepted product decisions and boundaries;
4. focused guides for user and contributor procedures;
5. GitHub issues for active reports and proposals;
6. the changelog and release notes for historical behavior.

Update this page when a release changes the current product contract, a settled decision changes,
or an issue listed here is resolved or accepted. Keep release-by-release detail in the changelog
instead of turning this page into another release history.
