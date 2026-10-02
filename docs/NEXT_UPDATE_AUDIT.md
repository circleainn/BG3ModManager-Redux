# Issue audit — October 1, 2026

Reviewed the supplied September Nexus comments and GitHub issues against published
`v0.1.0-alpha.16.5.4` and local baseline `fd1b973`. The latest open-issue query returned
11 issues, including the new #172 request. The fixes described as local below shipped in 16.5.5 on October 2;
this audit does not close issues or establish a reporter's exact failure was reproduced.

## Open GitHub issues

| Issue | Evidence and disposition |
|:--|:--|
| [#171](https://github.com/circleainn/BG3ModManager-Redux/issues/171) Vivid Landscape install stays at 0% | The reporter identifies 16.5.4 and the 4K download. Import has long operations without measured progress and repeated archive inspection. Local work adds phase feedback and reduces duplicate inspection. A separate tiny, valid multipart PAK fixture exposes unsupported staging behavior; it is not the reporter's archive. Keep open until the actual package completes or produces actionable diagnostics. |
| [#170](https://github.com/circleainn/BG3ModManager-Redux/issues/170) Release-note links do nothing | Markdown used a navigation command without a navigation host. The local fix opens HTTP(S) links through the browser command, with regression coverage. Verify in the packaged update window before closing after release. |
| [#157](https://github.com/circleainn/BG3ModManager-Redux/issues/157) Nexus sources become Local | The creator-manifest association correction shipped in 16.5.3. The issue's claim that it is unreleased is stale. Keep the remaining scope open for an affected current-version package and association origin. |
| [#127](https://github.com/circleainn/BG3ModManager-Redux/issues/127) Order changes and separators | Explicit active-order positions were preserved by the Sync correction shipped in 16.5.3; the issue's unreleased wording is stale. Local fixes address skipped inactive/Override settings restoration and pending saves lost on immediate Refresh. These do not establish every activation or separator report has the same cause. |
| [#110](https://github.com/circleainn/BG3ModManager-Redux/issues/110) Wine/Linux compatibility | A later discussion reports a working native-browser-to-Wine NXM prototype. The body should no longer imply this has no working route, but cold-start behavior, status feedback, paths, and Script Extender detection still need real Wine validation. |
| [#155](https://github.com/circleainn/BG3ModManager-Redux/issues/155) YANML integration | The author offers integration/API support. Prepare a concrete install/uninstall and ownership contract before replying; no agreement or new integration is implied. |
| [#133](https://github.com/circleainn/BG3ModManager-Redux/issues/133) Automatic category controls | Disabling automatic assignment is already available. Definition deletion, clearing assignments, and hiding defaults remain distinct requested actions; the broader request is only partially covered. |
| [#172](https://github.com/circleainn/BG3ModManager-Redux/issues/172) Move to a separator from the context menu | New, low-priority convenience request. Existing bulk actions move between Active and Inactive panes, but do not provide the requested separator destination submenu. Any implementation must preserve selection order, collapsed membership, and Undo/Redo. |
| [#98](https://github.com/circleainn/BG3ModManager-Redux/issues/98) Nexus SSO | Planned; official application registration remains a prerequisite. |
| [#56](https://github.com/circleainn/BG3ModManager-Redux/issues/56) Localization/accessibility | Planned broader work. Existing font, motion, and Read Load Order features are not completion of this scope. |
| [#63](https://github.com/circleainn/BG3ModManager-Redux/issues/63) Docked managers | Planned workspace change; keep separate from current bug fixes. |

## Local maintenance findings

The October 1 Nexus report from **irulannaba** narrows #171's symptoms: the Vivid Landscape
AIO 4K `.7z` extracts very slowly, installation appears stuck, and the reported `(x)` control did
not stop it; manual extraction was the workaround. Prioritize solid-archive extraction cost,
UI responsiveness, and cancellation separately from multipart PAK handling below.

- Startup copied only reactive settings, omitting the inactive and Override UUID sequences,
  window placement, and nullable appearance preferences. Restore these explicitly and retain
  the live nested settings instances.
- A single mod scan published each cache insertion separately, repeatedly rebuilding projections.
  Publish the fully initialized scan in one cache edit. A 1,400-mod regression checks one complete
  publication, duplicate version selection, and stale-entry removal; it is not a whole-app timing test.
- Startup duplicate-package prompts could run before the main window was visible. Defer them until
  it can own the dialog. Neither this nor batching proves the exact reported 70% stall is resolved.
- Local package intake lacked clear counts and progress. Show input processing, managed package
  counts, and the final added/already-present versus failed totals.
- Immediate Refresh could reload disk settings before a delayed inactive-order or separator save.
  Flush pending settings before Refresh clears the library; preserve pending work and abort Refresh
  if saving fails.
- Archive preflight and import now stage multipart PAK siblings together and inspect one primary.
  Replacements use a flushed recovery journal, preserve readable old sets, remove obsolete owned
  siblings, and reject filename collisions with another package. Startup recovers interrupted sets.
  Override holding, active backups, deletion, and backup retention include all header-declared parts.
  Loose sets entering Download Manager become one deterministic ZIP, binding every part to the
  reviewed hash and retained archive; a lone downloaded primary cannot verify external siblings.
- A synthetic solid LZMA2 `.7z` with twelve 2 MiB entries took 107.81 ms through separate
  `OpenEntryStream` calls and 16.53 ms through one sequential reader after warmup. This isolates
  repeated decompression overhead; it does not predict timing for the 1.8 GB Vivid download.
  Use sequential traversal for solid 7z archives and cancel its reader before disposing an
  interrupted entry, avoiding the archive library's default drain of unread compressed content.
- Install All's close control previously rejected closure without requesting cancellation.
  Treat close as Cancel, keep the progress window until cleanup completes, and leave unfinished
  downloads available for retry. Preserve already completed package installations.
- Further solid-7z testing found unnecessary decompression after the last required entry and after
  a callback failure. Stop the reader in both cases, and avoid opening it when no entries match.
  A fixture with real PAKs across two solid blocks verifies the full archive-preflight path; skipping
  its unrelated tail reduced compressed reads from 330,219 to 197,798 bytes. Normal mod import
  no longer selects load-order JSON that it will not apply. Compressed single-file import also no
  longer allocates and discards a buffer the size of the entire compressed input.
- Closing the application previously waited only for the download queue. Track owned archive
  work, request cancellation, and await file cleanup, source persistence, and final UI callbacks
  before closing. Reject new operations while shutdown is pending; reopen admission after a
  failed shutdown once existing operations have finished.
- A canceled archive could leave completed PAKs installed while skipping their exact Nexus source
  assignment and cache save. Apply provenance to committed results even on failure/cancellation,
  and persist local records without reusing the canceled extraction token. This is a separate
  confirmed code path relevant to #157, not verification of the original reporter's package.
- Install All now classifies and reviews one parsed inspection per package. It captures the reviewed
  identity, verifies those bytes again just before installation, and holds that package open through
  import before releasing it for retention. Recheck current findings and remaining dependencies
  so a failed earlier dependency is not still counted as available.
- Importing an archive's `Current.json` while a named order was selected copied the existing
  Current order over the named order and discarded the imported contents. Apply the imported
  data to the game-backed Current entry while preserving both entries' identities. Recognize
  only JSON documents with a valid load-order array; unrelated metadata no longer becomes an
  empty order, and malformed orders are reported within the import instead of a detached UI callback.
- Download finalization previously renamed the partial file before cancellable checksum verification.
  A cancellation could strand the archive under its final filename and block retry. Verify before
  promotion, record when the response body is fully received, and let retry finish hashing that file
  without requesting a byte range past its end. A leftover resume sidecar after promotion no longer
  invalidates a successfully verified download.
- Compressed single-PAK import used module Folder metadata directly as an output path. A valid
  synthetic PAK with `../escaped` metadata demonstrated that this could choose a path outside Mods.
  Validate destination filenames before commit and use the shared backup/install path, which also
  preserves Override identity and backup retention. Decompress directly into staging to eliminate
  the redundant temporary copy; the remaining TempFile helper now disposes its file after a failed copy.
- Hybrid native-plus-PAK review previously returned success after starting the PAK import without
  awaiting it. Completion could release/delete the source archive before the companion worker read it.
  Await both stages, forward cancellation and Nexus identity, and retain the package on partial failure.
  Download Manager removal and clear actions now share the review/install interaction guard in both
  directions, including after confirmation dialogs yield to other work.
- Archive import flattened PAK paths into Mods without enforcing its duplicate-filename finding.
  Two variants with the same basename could overwrite each other while both appeared imported.
  Check every PAK destination before reading any import entry, reject collisions during review,
  and apply the same check before a hybrid native package commits its other files.
- Hybrid companion imports still explicitly targeted Inactive after the completion fix. Use the
  existing placement-preserving import mode so updating an active companion retains its position.
- Package removal dropped its durable queue row before recycling/deleting files. A locked file
  could leave untracked data. Persist a Removal incomplete record before cleanup and remove the row
  only after cleanup succeeds. Failed cleanup or final manifest saves remain retryable after restart;
  pending removals cannot resume downloads or be overwritten by late inspection callbacks.
- Manual PAK extraction and active-mod backups were absent from the shutdown operation tracker.
  Register them before scheduling work, capture their input selection, and wait for cancellation,
  staging cleanup, and UI completion before closing. Backup ZIP writes now pass cancellation through
  asynchronous copying; a failed editor-mod package build aborts instead of silently omitting a mod.
- Manual extraction previously checked cancellation only around a complete synchronous unpack.
  Copy members with cancellation and atomically replace each destination after validating its length.
  Reject unsafe member paths before any output, retain completed members, and preserve an existing
  destination when its replacement fails or is canceled. Redux's LSLib build now checks cancellation
  inside raw LZ4 block expansion, mapped input copies, and bounded native solid-frame decoding.
  Editor-package compression remains synchronous per member; memory allocation and operating-system
  I/O are not preemptible. The decoding limitation has been removed without editing the submodule.
- Editor-mod backups used metadata Folder/UUID values to choose a temporary PAK path. Relative
  traversal could truncate/delete a file outside staging. Validate these values as single components
  and use an independently generated staging directory. Missing project sources now fail the backup;
  normal editor backups retain their archive names and contain both Mods and Public project files.
- F5 Refresh remained enabled during extraction and backups. Refresh completion could dispose the
  other operation's cancellation source, preventing a later shutdown request from stopping its work.
  Disable the command during main-progress operations and batch installs; recheck admission on direct
  execution and after modal confirmation, including shutdown. Keep Refresh disabled until cleanup ends.

The duplicate-menu, declined-shutdown, and explicit dependency-ordering reports overlap fixes
already shipped in 16.5.3. Defender reports do not establish a false positive; no security conclusion
is drawn from the pasted comments. View sorting alone does not demonstrate a changed game load order.

## Remaining release verification

Follow-up validation on October 2: multipart-set support and cancellable LSLib decoding now pass
**644/644 regression checks in both Debug and Publish**. New fixtures exercise complete/missing sets,
archive sibling ordering, stable local-intake archives, renamed replacement sets, readable recovery
backups, locked-part rollback, restart recovery, ownership collisions, Override holding, retention,
and malformed headers without losing the rest of the library. Decoder fixtures cover raw LZ4
literal/repeating/random contents, invalid blocks, a real solid v18 PAK, malformed frames, and
cancellation of 128 MiB block/frame decodes. The upstream submodule source remains unchanged;
Redux's build uses the documented adaptations under `compat/LSLib`.

The final follow-up validation ZIP has 76 files, four updater files, and 18,101,041 bytes; SHA-256
`c42f0baecdd6d588f29ff24011904db7d493e6d3cee343d36ed5dcfb1728303b`.
ZIP CRC, inventory, manifest identity, privacy-path checks, and clean extraction passed. Isolated
updater replacement, locked-file rollback, and user-state preservation passed on these bytes.
Logs and fixtures are under `.codex-artifacts/multipart-validation-20261002-final` in the outer
workspace. This build still uses the baseline 16.5.4 version and is not published. The original local
release ZIPs and channel manifest were restored byte-for-byte. The actual Vivid 4K archive and
interactive checks below remain unverified.

Earlier validation on October 2, before multipart and decoder support: Debug and Publish builds
succeeded and **627/627 regression checks passed in each configuration**. Coverage includes solid-7z entry contents and compressed-input reads,
cancellation during selected/skipped entries, stopping before unrelated archive tails, shutdown
waiting for owned operations, partial-import source persistence, canceled local-intake cleanup,
incomplete multipart rejection without replacing an installed file, changed-package rejection after review,
verified archive handle lifetime, and pending settings saves. Additional checks cover archived
Current and named-order preservation, malformed order JSON, compressed PAK destination validation,
temporary-copy cleanup, hybrid-package completion, and interrupted download finalization/retry.
Additional checks cover duplicate destination rejection before any import callback and durable
removal recovery after recycle, file-cleanup, and manifest-save failures, including restart.
New extraction/backup checks exercise real None/Zlib/LZ4 PAK contents, cancellation and read failure
after copying begins, preservation of prior files/backups, skipped later members, invalid member paths,
deletion markers, and a canceled editor-package build. ZIP cancellation is injected during output writes.
Editor-backup fixtures verify unsafe metadata cannot alter an unrelated file or a prior ZIP, missing
sources cannot replace a prior ZIP, and valid projects preserve real package contents and entry names.
Refresh regressions exercise the real command and F5 binding: disabled through cancellation cleanup,
safe when called directly during file work, and blocked during batch installs or shutdown.
`git diff --check` passed. The benchmark above compares synchronous traversal APIs; production uses
the asynchronous sequential reader, and the regression asserts reduced reads rather than elapsed time.

The earlier local Publish verification ZIP contains 76 inventoried files and exactly four updater files.
ZIP CRC, complete inventory coverage, manifest size/SHA-256, and byte-for-byte clean extraction passed;
the package checks detected no forbidden runtime-state files or private build paths. NuGet reported
no known vulnerable direct or transitive dependencies. The archive is 18,090,315 bytes with SHA-256
`cede5f5b784d3447ebd947e6c347fdeeaf07108f40823f1c98e78df1560e3491`.
This is a local validation build retaining the baseline 16.5.4 version, not a published hotfix.
The prior local release ZIPs and channel manifest were preserved and restored byte-for-byte.
Isolated updater transactions using the prior ZIP and verification ZIP replaced all 76 application
files while preserving settings, an order, a retained download, and native-install ownership data.
A locked-file failure restored every prior package file byte-for-byte. Inventory-based portable
removal preserved the same user-state fixtures. These checks do not verify public version discovery
or an interactive application launch.
Release preparation must assign the final version and repeat artifact checks on the resulting bytes.

Use the actual #171 package for installation/cancellation and retained-inbox cleanup checks. Test
immediate Refresh and restart after inactive and Override moves, separator edits, and appearance
changes in a packaged build. Verify update links, progress with Reduce Motion, and a large-library
startup. Automated fixtures do not replace those reporter-specific and interactive checks.

## Historical post-16.4.4 issue audit

This audit records the pre-16.5 triage. The [16.5 changelog](CHANGELOG.md) supersedes rows for
features and fixes that were subsequently implemented.

Updated September 15, 2026 against the published `v0.1.0-alpha.16.4.4` baseline at `e663ef9`,
current issue bodies/discussions, and the September 14–15 [Nexus reports](https://www.nexusmods.com/baldursgate3/mods/23799?tab=posts).
Alpha.16.4.4 is a silent cumulative maintenance release. Its GitHub and Nexus artifacts and the
public-alpha update channel are published; the historical 16.4 verification record remains below.

## Current bug investigations

| Issue | Assessment | Next action |
|:--|:--|:--|
| [#127](https://github.com/circleainn/BG3ModManager-Redux/issues/127) Order changes/separators after Sync or restart | The 16.5 candidate preserves the selected BG3 profile through Refresh and updates game-backed Current after a successful Sync without silently saving a named order. | Keep open for an exact current-build reproduction of any remaining Sync-specific mod movement or state change. |
| [#130](https://github.com/circleainn/BG3ModManager-Redux/issues/130) Missing thumbnails with working downloads | The 16.5 candidate also decodes WebP thumbnails when the URL has a misleading image extension; earlier builds already retry failed images. | Obtain an affected mod/provider if the image is still absent in the candidate. |

Issue #127 remains high priority because it may affect intended load-order state. Reporter versions
must not be inferred from comment dates. Preserve the remaining reports as separate investigations.

## New requests and related plans

| Issue | Disposition |
|:--|:--|
| [#131](https://github.com/circleainn/BG3ModManager-Redux/issues/131) Filename-display shortcut | Requested addition to the existing shared shortcut/setting system; no new rename behavior. |
| [#132](https://github.com/circleainn/BG3ModManager-Redux/issues/132) Hover-card descriptions | Evaluate optional descriptions/excerpts using available metadata; preserve the drawer and do not promise text for every mod. |
| [#133](https://github.com/circleainn/BG3ModManager-Redux/issues/133) Automatic category controls | Define hiding, assignment removal, definition deletion and future auto-assignment separately before implementing destructive changes. |
| [#134](https://github.com/circleainn/BG3ModManager-Redux/issues/134) Compact category-tag rows | Narrow presentation request; does not reopen the deferred whole-interface redesign in #118. |
| [#56](https://github.com/circleainn/BG3ModManager-Redux/issues/56) Localization/accessibility | Existing planned foundation now records PT-BR interest and the Chinese-version offer. No language or contributed branch is claimed integrated. |

The four new feature requests are awaiting evaluation, without a target release. UI localization
should remain independent of runtime AI translation, provider-metadata translation, repacking, or
alternative sorting experiments. A display shortcut need not wait for the entire localization effort.

The VOLO discussion expresses interest, not an agreed integration or partnership. Praise and the
Nexus report that staff removed a reupload do not require application issues.

## Existing issue dispositions

| Issue | Assessment | Next action |
|:--|:--|:--|
| [#124](https://github.com/circleainn/BG3ModManager-Redux/issues/124) Copy/export order actions | Closed: alpha.16.4.4 restores clipboard copy and correctly binds the renamed detailed-list export. | Reopen only for a current-version recurrence. |
| [#125](https://github.com/circleainn/BG3ModManager-Redux/issues/125) mod.io/Nexus misidentification | Closed: alpha.16.4.3 requires the complete legacy Nexus filename shape and rejects UUID-like mod.io names. | Use an exact current-version archive for any new identity report. |
| [#126](https://github.com/circleainn/BG3ModManager-Redux/issues/126) Expanded separator movement | Closed by design: expanded separators move only their header; collapsed separators move their sealed section. | Treat unexpected behavior outside that contract as a focused new report. |
| [#128](https://github.com/circleainn/BG3ModManager-Redux/issues/128) Wrong manual-link target | Closed: alpha.16.4.3 retains the right-clicked UUID through cross-pane moves and asynchronous verification. | Reopen only for a current-version recurrence. |
| [#129](https://github.com/circleainn/BG3ModManager-Redux/issues/129) Blank provider fields | Closed: alpha.16.4.3 synchronizes fields when encrypted credentials finish loading. | Reopen with a current-version restart result if it recurs. |
| [#135](https://github.com/circleainn/BG3ModManager-Redux/issues/135) Hidden category assignment | Closed: alpha.16.4.4 keeps hidden sidebar categories such as Libraries manually assignable. | Reopen only for a current-version recurrence. |
| [#136](https://github.com/circleainn/BG3ModManager-Redux/issues/136) Per-launch update checks | Closed: alpha.16.4.4 performs one quiet startup check when automatic checks are enabled. | Reopen only for a current-version recurrence. |
| [#121](https://github.com/circleainn/BG3ModManager-Redux/issues/121) Separator visibility/position | Remains closed for the documented 16.4.1/16.4.2 fixes. | Use a matching current-build recurrence before reopening; #127 tracks the separate sync/startup report. |
| [#122](https://github.com/circleainn/BG3ModManager-Redux/issues/122) Update replacement | Remains closed for the maintenance fix addressing temporary locks/read-only app files. | Record the exact blocked application file if failure recurs; do not delete user state. |
| [#123](https://github.com/circleainn/BG3ModManager-Redux/issues/123) Per-order separators | Remains closed for the documented per-saved-order storage fix. | Separate named-order selection from game-derived orders and investigate #127. |
| [#111](https://github.com/circleainn/BG3ModManager-Redux/issues/111) Inactive organization | Closed: shipped in 16.4 with persistence, separators, and Undo/Redo coverage. | Global Redux-only ordering; Advisor remains active-only. |
| [#113](https://github.com/circleainn/BG3ModManager-Redux/issues/113) Window consistency | Closed: the shipped 16.4 refinement pass is complete at the maintainer’s request. | Track specific future scaling/accessibility defects separately. |
| [#119](https://github.com/circleainn/BG3ModManager-Redux/issues/119) Extender settings | Closed: default-export preference persistence is fixed in 16.4. | Omitted EnableAchievements=true means the normal enabled default, not disabled achievements; explicit default export is optional. |
| [#120](https://github.com/circleainn/BG3ModManager-Redux/issues/120) NXM reassociation | Closed: explicit takeover recovers from stale Redux ownership markers in 16.4. | Automatic repair cannot replace another handler without explicit reassociation. |
| [#95](https://github.com/circleainn/BG3ModManager-Redux/issues/95) Elevation warning | Closed as resolved for now at the maintainer’s request. | Reopen with current-build diagnostics if it recurs. |
| [#98](https://github.com/circleainn/BG3ModManager-Redux/issues/98) Nexus SSO | Planned; official application registration is a prerequisite in the issue. | Confirm registration before scheduling implementation. Do not defer current credential defects merely because SSO is planned. |
| [#109](https://github.com/circleainn/BG3ModManager-Redux/issues/109) Overrides per order | Planned; requires safe file moves and recovery. | Separate feature work with opt-in migration and transaction tests. |
| [#110](https://github.com/circleainn/BG3ModManager-Redux/issues/110) Wine | Planned; platform reports need reproducible environments. | Collect/test real Wine prefixes, SE detection, and NXM routing. |
| [#108](https://github.com/circleainn/BG3ModManager-Redux/issues/108) Collections | Closed: the 16.4 importer fulfills the accepted release scope. | Direct collection NXM activation, historical revisions, and installer rules remain possible follow-ups. |
| [#118](https://github.com/circleainn/BG3ModManager-Redux/issues/118) Compact interface | Closed as not planned for now. | No compact redesign is scheduled; evaluate #134 independently. |
| [#63](https://github.com/circleainn/BG3ModManager-Redux/issues/63) Docking | Planned, substantial workspace change. | Separate design/implementation; reuse manager state. |
| [#56](https://github.com/circleainn/BG3ModManager-Redux/issues/56) Localization/accessibility | Planned foundation; current layout work is only partial coverage. | Separate resource/localization work and assistive-technology testing. |

Existing completed scopes remain closed. Issues #124, #135, and #136 are closed for their
alpha.16.4.4 fixes; #127 and #130 retain their unresolved portions. Issue #97 signing remains
explicitly deferred, not completed.

## Live verification follow-ups

These checks remain outstanding where not independently recorded as complete. Historical automated
coverage does not establish that a live check has passed.

- Fresh setup and returning-user setup: finish, cancel, restart, custom theme/font, icons hidden,
  icons-only, text sizes, gradients, Reduce Motion, and owner backdrop. Verify persisted choices.
- Real Windows scaling (100/125/150/200%), keyboard traversal, and screen-reader context across
  Preferences, Downloads, Saves, native mods, reviews, and What's New. Render tests do not replace this.
- Inactive-list persistence/Undo and unchanged game export; Advisor must not alter inactive mods.
- Named-order Save/Sync/restart, repeated Sync, separator membership, expanded/collapsed moves,
  and correct startup order selection for #126/#127.
- Current-version recurrence checks for source linking, archive identity, credential display, and
  load-order copy actions; identify an affected mod/provider for the remaining #130 investigation.
- Real NXM takeover and download/install flow, plus Script Extender default export/config reload.
- Free-account collection authorization handoff, a populated collection order saved and selected in
  the running app, and recent-revision selection behavior. Manifest rules remain unsupported.
- Native detection against clean game files; installation/reinstall/remove with protected backups.
  Switching Redux folders still does not migrate ownership records or backups.
- Real updater smoke test from the previous public build, including temporary locks and restart.
  Dev remains without release assets or portable CI downloads.
- Save Mod Review with a real save containing missing/inactive mods, selected activation and Undo,
  and corrupt metadata. Review does not verify versions, dependency chains, or native mods.
- Real save/campaign ZIP export, cancellation, and restore. Exports include thumbnails and retain
  the limits of 32 saves, 1 GB total and 256 MB per file; larger campaigns need smaller selections.

## Recommended maintenance scope

Continue #127's Sync-specific investigation and obtain an affected mod/provider for #130. Keep
small fixes independently reviewable and preserve Save/Sync, package identity, ownership, and
privacy contracts. Add regression checks for confirmed causes before assigning another fix.

16.4 already shipped the manager overhauls, collections, inactive organization, and onboarding.
Do not treat this triage as another feature release. Nexus SSO, Override management, docking,
localization, and Wine work retain their existing planning boundaries. No next version, release date,
or closure is implied by this document.

## Historical 16.4 data review checkpoint

The following is retained from the earlier release audit, not a new September 15 data review.

Removed one invalid ordering record and added four exact library-name aliases plus four
corroborated UUID category records. Remaining 137 dependency differences, ten new UUID records,
name-only additions, and placement/category changes require further evidence review. No new
ordering constraints or provider identity assignments were accepted in that pass.

Save Mod Review package fixtures cover populated, empty, absent metadata, missing UUID, and
corrupt saves, including input-file preservation and legacy empty-import behavior. Round-trip and
cancellation regressions were recorded as passing. A real gameplay save, activation/Undo, campaign
export/restore, and live cancellation remain the follow-ups listed above.

## Historical alpha.16.4 release verification

These results belong to the published 16.4 release and were not rerun during this documentation audit.

- Debug and Publish builds completed; 471/471 regression checks passed in each configuration.
- NuGet transitive dependency audit reported no known vulnerable packages at that checkpoint.
- The portable package contained 75 inventoried files, exactly four updater files, matching manifest size/hash, and no detected user state or local build paths. Clean extraction succeeded.
- Main and dev publication was authorized for 16.4. No app-control testing was performed. Live free-account collection handoff, real UI/update smoke checks, scaling and assistive-technology checks remained follow-ups; automated checks do not replace them.

## Historical published alpha.16.4 record

- Release commit: `618013f3bfdbf2c3cf9b154ad5191674e3d7c915` on dev and main; immutable tag `v0.1.0-alpha.16.4`.
- Both GitHub Windows CI runs succeeded, including regression and Publish-package checks.
- GitHub ZIP: 17,252,367 bytes; SHA-256 `58ffcf33ed8dca5b426c324da3b135ca7ec1d3ff5beb0b22431f156c3a8b1c30`. Anonymous download matched.
- Nexus publication succeeded after the configured reviewer approved it: public file `130490`, version ID `14920716516794`. API verification confirmed version, size, and all three headline feature descriptions.
- The public-alpha update channel was updated last and verified against the published ZIP.
- README and this issue audit were refined after publication. Published 16.4 assets and their tag remain unchanged.

## Published alpha.16.4.3 record

- Release commit and tag: `ad41cedd2ba8b1d6ae0dd24a458ce2a7c6fc1722`, `v0.1.0-alpha.16.4.3`.
- Dev CI [run 35026598544](https://github.com/circleainn/BG3ModManager-Redux/actions/runs/35026598544) and main CI [run 35027023987](https://github.com/circleainn/BG3ModManager-Redux/actions/runs/35027023987) passed.
- GitHub ZIP: 17,845,442 bytes; SHA-256 `f58e1b6fdda6492570db0a0ce0d2ac4818e38404ba69c68d51ab01d8233f5ce1`.
- Nexus publication [run 35027393455](https://github.com/circleainn/BG3ModManager-Redux/actions/runs/35027393455) succeeded with file-version ID `14920716516910`.
- The fixed public-alpha URL was independently fetched after publication and returned display version `0.1.0-alpha.16.4.3`, internal version `0.1.16.403`, and the matching ZIP size and digest.

See the [16.4.3 release notes](releases/0.1.0-alpha.16.4.3.md) for the shipped fixes.

## Published alpha.16.4.4 record

- Release commit and tag: `e663ef9e107d342b71adb53828975a57d4ee9187`, `v0.1.0-alpha.16.4.4`.
- Dev CI [run 35028992524](https://github.com/circleainn/BG3ModManager-Redux/actions/runs/35028992524) and main CI [run 35029763076](https://github.com/circleainn/BG3ModManager-Redux/actions/runs/35029763076) passed.
- GitHub ZIP: 17,845,610 bytes; SHA-256 `da2a00fa44ef555a5c6e3b9f8db2b71a045cfdd8c130ad5d94e926b48926847f`.
- Nexus publication [run 35030155299](https://github.com/circleainn/BG3ModManager-Redux/actions/runs/35030155299) succeeded with file-version ID `14920716516913`.
- The fixed public-alpha URL was independently fetched after publication and returned display version `0.1.0-alpha.16.4.4`, internal version `0.1.16.404`, and the matching ZIP size and digest.

See the [16.4.4 release notes](releases/0.1.0-alpha.16.4.4.md) for the cumulative fixes.
