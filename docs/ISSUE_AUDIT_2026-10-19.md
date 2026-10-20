# GitHub and Nexus audit — October 19, 2026

## Coverage and evidence

Read all 12 currently open GitHub issues and their 11 comments, plus the most recently updated issue records. Checked implementation and release history against this working tree. GitHub's latest release at the audit date is [alpha.16.5.6](https://github.com/circleainn/BG3ModManager-Redux/releases/tag/v0.1.0-alpha.16.5.6), published October 2. Recovery changes are development work and remain unreleased.

Read the currently accessible [Nexus posts page](https://www.nexusmods.com/baldursgate3/mods/23799?tab=posts), including its ten displayed top-level threads and replies through October 15. Nexus lists 174 comments over eight pages; older-page navigation was unavailable through the available reader, and the in-app browser was unavailable. This is not a fresh review of every historical comment. Earlier reports were cross-checked with the October 1 audit and GitHub discussion. The [Nexus Bugs tab](https://www.nexusmods.com/baldursgate3/mods/23799?tab=bugs) contains a routing notice directing reports to GitHub.

No external comments, issue changes, releases, application changes, or new test runs were performed during this audit. Previously recorded validation was 718 regression checks plus five Linux-helper tests, with Debug and Publish builds passing; those results do not reproduce every user report.

## Open GitHub issues

| Issue | Verified status | Recommended disposition |
| --- | --- | --- |
| [#127 — Order changes and separators](https://github.com/circleainn/BG3ModManager-Redux/issues/127) | Named-order startup, explicit Sync positions, and organization persistence have shipped fixes. Local failed/incomplete `modsettings.lsx` read protection adds a separate correction. | Keep open for remaining precise reproductions. Release notes should describe the corrected read failure, not claim all order resets are solved. Test Save/Sync, Refresh, restart, named orders, and Current separately. |
| [#157 — Nexus links become Local](https://github.com/circleainn/BG3ModManager-Redux/issues/157) | Creator-manifest preservation shipped in 16.5.3. Cancellation/partial-import provenance preservation shipped in 16.5.5. Local manual-link validation is another distinct correction. | Keep remaining scope open for an affected package, association origin, and exact triggering action. Do not equate a URL-validation fix with fixing all source loss. |
| [#170 — Release-note links](https://github.com/circleainn/BG3ModManager-Redux/issues/170) | Browser-navigation correction shipped in 16.5.5 and is included in 16.5.6; maintainer comment already explains this. | Candidate for closure after checking the packaged update window. It is not a newly implemented headline fix. |
| [#171 — Vivid installation](https://github.com/circleainn/BG3ModManager-Redux/issues/171) | Reporter links Nexus mod 20021, the PAK edition. Archive performance/cancellation work shipped in 16.5.5. The archive supplied locally is mod 6473, a loose-texture edition; the local no-PAK rejection was verified against that archive. | Keep open. Obtain the exact reported 4K PAK archive and test progress, cancellation, and completion before claiming this report resolved. |
| [#172 — Move to Separator](https://github.com/circleainn/BG3ModManager-Redux/issues/172) | Implemented locally for one or multiple mods, nested/collapsed destinations, all three panes, and Undo/Redo. Destinations stay within the source pane. | Include in the maintenance update after packaged UI verification. Close the implemented scope only after release; cross-pane separator targeting is additional work. |
| [#133 — Automatic category controls](https://github.com/circleainn/BG3ModManager-Redux/issues/133) | Local clear/restore automatic assignments and hide/show built-in filters preserve manual/custom choices and roll back failed saves. Global automatic classification can already be disabled. | Release these scoped controls. Deleting built-in category definitions remains an undecided request; do not claim it is implemented. |
| [#110 — Wine/Linux](https://github.com/circleainn/BG3ModManager-Redux/issues/110) | A historical running-instance prototype worked. Local path detection, forwarding helper, host-preserving launches, and prefix-specific status have automated coverage. | Keep experimental and open. Real Wine/Wayland cold start, browser forwarding, install/Sync, and update/restart validation remains. Do not advertise native Linux support. |
| [#155 — YANML](https://github.com/circleainn/BG3ModManager-Redux/issues/155) | Plugin integration shipped in 16.5. The developer's reply proposes a supported API for loader/autostart installation, removal, status, and richer plugin operations. | Future collaboration, not missing basic integration. Decide API/distribution and elevation boundaries before implementing automatic loader setup. No API agreement is established by this audit. |
| [#181 — Patreon sources](https://github.com/circleainn/BG3ModManager-Redux/issues/181) | Request for post association/change detection; commenter suggests API routes. | Discovery work. Validate official supported access, permissions, and update semantics before promising automation; a comment is not an established integration contract. |
| [#98 — Nexus SSO](https://github.com/circleainn/BG3ModManager-Redux/issues/98) | Planned official application registration and authentication flow; manual API keys remain available. | Later feature work; not required for this maintenance release. |
| [#63 — Docked/paged managers](https://github.com/circleainn/BG3ModManager-Redux/issues/63) | Planned workspace redesign with shared state and detachable panes. | Defer until stabilization; it is a substantial layout/state project. |
| [#56 — Localization/accessibility](https://github.com/circleainn/BG3ModManager-Redux/issues/56) | Broader foundation remains planned. Existing appearance features and the separately fixed Read Load Order regression do not complete it. | Separate planned work. Preserve current accessibility behavior during the release checks. |

## Newer Nexus feedback

Source for these reported observations: [Nexus posts](https://www.nexusmods.com/baldursgate3/mods/23799?tab=posts).

| Report | Audit disposition |
| --- | --- |
| malmern2, October 15: detected SE v20 versus claimed v33 | Investigate before release; OS, paths, and version evidence are missing. |
| Nightleo, October 15: separator targeting across panes | Same-pane movement is implemented locally; cross-pane targeting remains additional scope. |
| Nightleo: inactive-only categories, editable positions, tags, dependency notes | Split into distinct proposals; category filtering currently shares one selected category. |
| DrWiFi, October 12: manual dependency/order rules | Evaluate separately from actual package dependencies. |
| MagnasCarter, October 10: artwork gallery | Later enhancement; current custom artwork is one image. |
| October reset discussion | Original reporter withdrew attribution to Redux; other reports remain separate. |
| September Defender report | Unverified detection; other users' replies do not establish a false positive. |

## Script Extender investigation

The October 15 report is the main newly identified bug investigation. Do not merge it automatically into the Wine issue: the reporter does not specify an operating system or configuration.

`MainWindowViewModel.CheckExtenderInstalledVersion` resolves a cache folder, scans cached extender DLLs, reads Windows file-version metadata, and selects an exact configured `TargetVersion` if found, otherwise the highest full version. This measures cached DLL metadata, not the extender actually loaded in a running game. The local path resolver improves cache selection but does not establish why this reporter sees the older version.

Useful next evidence: exact Redux build, warning text, detected DLL path and file version, configured cache/target version, and the extender version reported by the game. Reproduce with copies or synthetic fixtures before altering detection semantics. Inspect stale caches, explicit version pinning, alternate installations, and metadata-versus-runtime version differences as hypotheses, not confirmed causes. Add focused coverage for the proven case. Do not simply suppress the warning.

Source anchors: `src/GUI/ViewModels/MainWindowViewModel.cs` (`CheckExtenderInstalledVersion`, `ModMatchesSelectedCategory`); `src/Core/AppServices/ScriptExtenderDetectionPathResolver.cs`; `src/GUI/Views/HorizontalModLayout.xaml.cs` (Move to Separator menu); `tests/Redux.Core.Tests/ScriptExtenderDetectionPathTests.cs`; `docs/CHANGELOG.md`.

### Follow-up investigation

A regression against the actual settings reload path reproduced a stale `TargetVersion`: after loading a v20 pin and deleting the updater configuration, Redux still retained v20 in memory. The local fix now restores updater defaults when that file is absent for a valid selected game installation. It preserves existing pins and retains prior settings when the file is locked, invalid, or unreadable; it does not write game configuration. Tests also cover switching installations and returning to an installation with a valid pin. The Debug build and all 721 regression checks passed after this correction. This is a confirmed defect relevant to detection, not proof of the Nexus reporter's exact cause.

Upstream [v33 release notes](https://github.com/Norbyte/bg3se/releases/tag/v33) describe the embedded extender update. The current [DLL version resource](https://github.com/Norbyte/bg3se/blob/main/BG3Extender/resource.h) still specifies v33 metadata, and the [updater cache implementation](https://github.com/Norbyte/bg3se/blob/main/BG3Updater/Cache.cpp) still uses cached DLLs. Downloading a new updater alone must not be treated as proof that a particular runtime version is loaded.

## Maintenance release recommendation

Interactive app-control acceptance was subsequently deferred on October 20. It is not a passed
check; retain that limitation when assessing a candidate. Development commits do not authorize
publication or waive the remaining release verification.

1. Investigate the new extender-version report and complete packaged checks for order persistence, Override-copy recovery, diagnostic status, separator moves, category controls, and browser links.
2. Include the scoped local recovery fixes and restored controls. Describe ImageSharp replacement as dependency maintenance. Keep Wine explicitly experimental.
3. Keep Vivid PAK installation and remaining source/order symptoms open unless actually reproduced and verified. Deferred feature requests do not need to block the Windows maintenance release.
4. Commit and back up the reviewed changes; assign the intended release version and prepare the final package. Reconcile issue status after publication and verification, rather than closing tickets because local tests pass.

The supplied Discord screenshots about duplicate Overrides and misleading review status also remain relevant release checks. Local fixes exist, but these screenshots are separate evidence from the freshly read Nexus page.
