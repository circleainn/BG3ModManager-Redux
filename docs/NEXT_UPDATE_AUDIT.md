# Post-16.4.2 issue audit

Updated September 15, 2026 against the published `v0.1.0-alpha.16.4.2` baseline at `b09a2f4`,
current issue bodies/discussions, and the September 14–15 [Nexus reports](https://www.nexusmods.com/baldursgate3/mods/23799?tab=posts).
This update records documentation and issue triage only. It does not claim new runtime reproductions,
new passing test runs, fixes, or readiness for another release. The historical 16.4 verification
record is retained below and applies only to that release.

## Current bug investigations

| Issue | Assessment | Next action |
|:--|:--|:--|
| [#127](https://github.com/circleainn/BG3ModManager-Redux/issues/127) Order changes/separators after Sync or restart | High priority; reporter versions unspecified. One user recovered separators by reopening a named saved order. | Reproduce Save → Sync → restart; distinguish working order, saved order, game file, startup selection, and view-only sorting. Do not make Sync silently save. |
| [#128](https://github.com/circleainn/BG3ModManager-Redux/issues/128) Source link applied to the wrong mod | High priority; manual-link report after a cross-pane move, not a confirmed root cause. | Verify context-menu target identity through dialog completion and both panes. Keep separate from submenu hit testing and #125. |
| [#125](https://github.com/circleainn/BG3ModManager-Redux/issues/125) mod.io package named as unrelated Nexus mod | Reporter explicitly uses 16.4.2; independently unverified. | Trace parsed identity, provider namespaces, manual/cache/database precedence, and refresh/restart. Do not assume it is merely cosmetic. |
| [#129](https://github.com/circleainn/BG3ModManager-Redux/issues/129) Provider fields blank after restart | Persistence versus display not yet distinguished. | Test authenticated use after restart without re-entry; check PasswordBox initialization/refresh and prevent stale empty UI from clearing valid storage. |
| [#130](https://github.com/circleainn/BG3ModManager-Redux/issues/130) Missing thumbnails with working downloads | Reported even with freshly entered keys. | Test metadata/artwork retrieval and binding separately from authentication. Do not globally hide thumbnails or wipe caches as a presumed fix. |
| [#124](https://github.com/circleainn/BG3ModManager-Redux/issues/124) Existing mod-list copy behavior | Keep open as a bug. The maintainer's final disposition says the implementation is incorrect. | Establish current Ctrl+C scope/focus/output and regressions. Do not close based on the earlier acknowledgement or implement a competing export path. |
| [#126](https://github.com/circleainn/BG3ModManager-Redux/issues/126) Expanded separator movement/membership | Older 16.2 report; expanded marker-only movement is distinct from collapsed-block movement in current source. Unexpected absorption still needs checking. | Retest current expanded/collapsed membership, Save/restart, and both panes before deciding whether a bug remains or a behavior change is requested. |

The first two investigations are marked high priority because they affect intended setup/metadata.
Priority does not mean the cause has been reproduced. The Nexus commenters do not supply exact
versions for #127–130; do not infer 16.4.2 from their posting dates. Preserve original reports and
keep source observations labeled as investigation leads.

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

Existing completed scopes remain closed. No unresolved report is being marked fixed by this audit.
Issue #97 signing remains explicitly deferred, not completed. Arleau's submenu cursor-gap report
matches the 16.4.2 release note, but the wrong-target linking report is separate in #128.

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
- Source linking after cross-pane moves; identity matching for #125; credential persistence versus
  field display; available mod thumbnails; existing copy behavior.
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

Prioritize #127 and #128, then provider identity, credential/thumbnail handling, the current copy
bug, and any confirmed separator recurrence. Keep small fixes independently reviewable and preserve
Save/Sync, package identity, ownership, and privacy contracts. Add regression checks for confirmed
causes before assigning a fix to a release.

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

For the later maintenance baseline, see [16.4.2 release notes](releases/0.1.0-alpha.16.4.2.md).
The documentation audit does not replace any published artifact or move the update channel.
