# Post-16.4.4 issue audit

Updated September 15, 2026 against the published `v0.1.0-alpha.16.4.4` baseline at `e663ef9`,
current issue bodies/discussions, and the September 14–15 [Nexus reports](https://www.nexusmods.com/baldursgate3/mods/23799?tab=posts).
Alpha.16.4.4 is a silent cumulative maintenance release. Its GitHub and Nexus artifacts and the
public-alpha update channel are published; the historical 16.4 verification record remains below.

## Current bug investigations

| Issue | Assessment | Next action |
|:--|:--|:--|
| [#127](https://github.com/circleainn/BG3ModManager-Redux/issues/127) Order changes/separators after Sync or restart | Alpha.16.4.3 fixes startup forcing Current instead of the remembered named order. | Keep open for exact reproduction of the separate Sync-specific mod movement/state reports. Do not make Sync silently save. |
| [#130](https://github.com/circleainn/BG3ModManager-Redux/issues/130) Missing thumbnails with working downloads | Alpha.16.4.3 evicts failed remote images so temporary download/decode failures can retry. | Obtain an affected mod/provider to distinguish a failed request from metadata with no usable image URL. |

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
