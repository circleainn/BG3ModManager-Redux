# GitHub issue audit after 16.6 — October 20, 2026

## Coverage and evidence

Reviewed all 12 open GitHub issues and their 11 comments against the published
[0.1.0-alpha.16.6 release](https://github.com/circleainn/BG3ModManager-Redux/releases/tag/v0.1.0-alpha.16.6).
Also reviewed the 12 most recently updated closed issue records and the discussions on #92, #147,
and #148. No new 16.6 regression report was present in the reviewed records at the time of the audit.
This pass covers GitHub; the [October 19 audit](ISSUE_AUDIT_2026-10-19.md) retains the earlier Nexus
review and its access limitations.

The published tag points to `83df8e4d7cbaf57ecd28af97048ea8f0e97a2990`. Source and tests in the
audit checkout match that release. Checked the separator context-menu wiring and movement tests,
the browser-link regression tests, release notes, and the [release record](RELEASE_RECORDS.md).
The release previously passed all 734 regression checks in both Debug and Publish configurations;
this audit did not rerun those suites or perform independent interactive app testing.

## GitHub actions completed

| Issue | Action | Evidence and remaining boundary |
| --- | --- | --- |
| [#172 — Move to Separator](https://github.com/circleainn/BG3ModManager-Redux/issues/172) | Closed as completed; removed `status: planned`. | The requested right-click action is published in 16.6. It supports one or multiple mods, nested/collapsed destinations, and Undo/Redo in all three panes. Targets stay within the source pane; cross-pane targeting is additional scope. |
| [#127 — Order changes and separators](https://github.com/circleainn/BG3ModManager-Redux/issues/127) | Updated the maintainer-owned report to identify the 16.6 read-failure protection as shipped. Retained high priority, partial resolution, and needs-info status. | Failed or incomplete `modsettings.lsx` reads no longer imply a valid empty order. This does not establish the cause of every remaining Sync/import/reset report. |
| [#157 — Nexus links become Local](https://github.com/circleainn/BG3ModManager-Redux/issues/157) | Updated the maintainer-owned report with the shipped association fixes and current 16.6 reproduction target. Retained partial resolution and needs-info status. | Creator-manifest preservation shipped in 16.5.3; partial/canceled-import preservation shipped in 16.5.5. Manual URL validation in 16.6 is a separate correction. |
| [#133 — Automatic category controls](https://github.com/circleainn/BG3ModManager-Redux/issues/133) | Replaced the unreleased status in the maintainer-owned report with the shipped 16.6 scope. Kept open with partial resolution. | Clear/restore assignments and hide/show built-in filters are available. Deleting built-in category definitions remains undecided. |
| [#110 — Wine/Linux](https://github.com/circleainn/BG3ModManager-Redux/issues/110) | Updated the maintainer-owned report to distinguish published compatibility work from outstanding validation; added `resolution: partial`. Retained planned/low-priority labels for the remainder. | Real Wine/Wayland cold start, browser forwarding, installation/Sync, and update/restore/restart still need validation. This is experimental Windows-under-Wine support. |
| [#171 — Vivid installation](https://github.com/circleainn/BG3ModManager-Redux/issues/171) | Added `status: needs info`; kept open. | The report concerns mod 20021's 4K PAK edition. The loose-texture archive from mod 6473 used in local testing does not reproduce that report. |

Read back the issue states, labels, and edited bodies after applying these changes. The tracker now
has **11 open issues**. No reporter-authored issue bodies or historical comments were rewritten,
and no new discussion comments were posted.

## Issues retained without changes

| Issue | Reason |
| --- | --- |
| [#170 — Release-note links](https://github.com/circleainn/BG3ModManager-Redux/issues/170) | The browser fix shipped in 16.5.5. The existing maintainer reply already requests confirmation from the packaged update window; there is no new reply or observed browser-launch result to justify closing it. |
| [#155 — YANML](https://github.com/circleainn/BG3ModManager-Redux/issues/155) | Plugin management already shipped in 16.5. The remaining request is official loader/autostart installation and API collaboration, including distribution and elevation decisions. No API agreement or delivery date is established. |
| [#181 — Patreon sources](https://github.com/circleainn/BG3ModManager-Redux/issues/181) | Discovery work. The suggested API routes in the comment are unverified proposals; supported access and change-detection semantics need research before implementation. |
| [#98 — Nexus SSO](https://github.com/circleainn/BG3ModManager-Redux/issues/98) | Remains planned; official registration and authentication work is separate from existing manual API-key support. |
| [#63 — Docked/paged managers](https://github.com/circleainn/BG3ModManager-Redux/issues/63) | Remains a planned workspace redesign after stabilization. |
| [#56 — Localization/accessibility](https://github.com/circleainn/BG3ModManager-Redux/issues/56) | Remains planned; existing appearance/accessibility features do not complete the requested foundation. |

## Related closed reports

[#148](https://github.com/circleainn/BG3ModManager-Redux/issues/148) concerns a linked Mods-folder
target and was fixed in 16.5.1. [#147](https://github.com/circleainn/BG3ModManager-Redux/issues/147)
covers held-Override activation, a confirmed mixed-package correction, and the removed unreachable
configuration dialog. Their discussions do not establish a recurrence requiring reopening.

The supplied Discord duplicate-copy report is a separate case. In 16.6, **Tools > Resolve Override
Copies** identifies both paths and offers explicit copy selection with backups; Refresh can finish
while copies await review. Existing duplicate copies still require that review before switching
orders. Do not describe the tool as silently deleting duplicates or fixing every switching error.

The most recently updated closed issue, [#92](https://github.com/circleainn/BG3ModManager-Redux/issues/92),
ends with a reporter confirming that aliases work in 16.5.4. That is not blanket confirmation of
automatic file-level naming for every source association; no additional closure claim was made.

## Next investigation priorities

1. **#127:** reproduce a remaining order change on 16.6 with the selected profile/order and exact
   transition. Compare working UUID order, saved Current/named orders, and game output from copies.
   Keep presentation sorting separate from actual exported order.
2. **#171:** obtain the exact reported 4K PAK archive filename/file ID and current-version result.
   Test progress, cancellation, and completion in a disposable destination. No claim of resolution
   is supported by the different loose-texture archive.
3. **#157:** identify an affected package, how its source was assigned, and the action that removes
   it. Compare public source IDs before and after without exposing credentials or signed links.
4. **#170:** confirm the release-notes link opens the default browser from the packaged update
   window. Existing command-dispatch tests do not observe the external browser.

Wine validation and the deferred feature requests retain their individual scope. These findings
do not establish a new release blocker or justify changing the already-published 16.6 artifacts.
