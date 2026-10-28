# Redux product direction and beta plan

Planning baseline: October 28, 2026. This is a working release plan, not a declaration that a
beta or additional platform is ready. The current published baseline remains
`0.1.0-alpha.16.7`, supporting Windows 10/11 x64. See [current state](CURRENT_STATE.md) for the
product contract and [release records](RELEASE_RECORDS.md) for what was actually verified.

## Product promise and scope

**Redux makes complex BG3 mod setups understandable, keeps changes deliberate, and makes recovery
straightforward.** Its core workflow is install and inspect → organize → save → review and sync →
recover when needed. A dependable beta means this workflow is coherent, responsive, and tested.
It does not mean the entire feature roadmap is finished.

- Preserve the existing package, profile, and load-order foundation. New PAKs enter Inactive;
  updates preserve state and position. Save and Sync remain distinct. Downloads, inspections,
  and imports do not silently activate, reorder, save, or sync mods.
- Retain essential libraries, including LSLib, with their notices and compatibility checks.
  The new VOLO masterlist importer is excluded. The published/main baseline does not contain
  it; any later integration of unpublished local work must preserve its removal. This plan does
  not claim that a published release changed.
- Freeze new feature scope while qualifying the Windows beta. Existing experimental features,
  including the optional Advisor, retain their stated limits. Unpublished content-inspection work
  must be explicitly included with evidence or deferred; its presence in a local checkout does
  not place it in the release.
- Defer new managers, expanded collection automation, SSO, installers, and additional speculative
  features unless a specific change is necessary to repair the agreed core workflow.
- Use one design system and interaction model across platforms: shared terminology, navigation,
  component states, and workflows. Platform-specific paths, system integration, and game-plugin
  availability require explicit behavior; they cannot be hidden behind a promise of parity.
- Keep Redux and the existing canonical star provisionally. A new name or logo is not a beta gate.
  Treat Fluent influence as clear hierarchy, readable typography, consistent controls, and useful
  feedback. Preserve dense-list efficiency, themes, customization, and Reduce Motion.

## Finite beta worklist

P0 protects the source baseline, supported runtime, or user data. P1 completes the agreed beta
experience and release evidence. All B-items must be dispositioned before a Windows beta;
historical automated passes do not count as fresh candidate acceptance.

| ID | Priority / current status | Required outcome and exit evidence |
|:--|:--|:--|
| B01 — Consolidate the baseline | P0 · Partial; local changes are snapshotted, unpublished feature integration remains deferred | Preserve a recoverable copy and inventory before integration. Record the chosen source commit, included/deferred local changes, VOLO-importer removal, and this release's feature scope. Review changes in small groups without overwriting the original checkout. |
| B02 — Supported runtime | P0 · Open; published 16.7 requires .NET 8 | Validate the .NET 10 target and all native/toolchain dependencies under the [runtime policy](SECURITY.md#runtime-lifecycle). Retarget the app, Core, updater, snapshot runner, and tools together; record Debug/Publish results and exact-package checks. Prove a migration path from the public app on a machine without .NET 10, with recovery and accurate prerequisites. |
| B03 — State and recovery | P0 · Partial; automated coverage and historical backup acceptance exist, remaining real cases are unverified | Record install/update, filtered movement, separator membership, saved-order switching, restart, Save/Sync, undo/redo, failed/cancelled writes, and restore/roll-forward on the candidate. Complete real campaign export/import and cancellation within documented limits. Confirm unrelated user data and excluded snapshot content remain intact. Resolve any reproduced data-loss or unsafe-write defect before release. |
| B04 — One usable interface | P1 · Partial; shared rules exist, full candidate usability acceptance is not recorded | First validate a representative core-workflow prototype; then apply the approved patterns to its real screens and everyday dialogs. Follow [UI acceptance](UI_DESIGN.md#acceptance-workflow). Record uncoached task completion, corrections, keyboard/screen-reader behavior, themes, larger text/scaling, narrow layouts, and reduced motion. Select representative list sizes and performance targets before measuring startup, filtering, dragging, switching, and sync; record results and fix freezes or stale controls. |
| B05 — Install and update confidence | P0 · Partial; package/provenance checks exist, public in-app observation remains outstanding | On the exact candidate, record clean extraction, upgrade from the current public app, manual replacement, rollback, and preserved settings/orders. Verify the runtime prerequisite path from B02. Decide and test beta version/channel discovery against already-published clients before changing any channel. Retain source and artifact hashes, CI links, and public-download/update observations under the [publication gate](PUBLIC_ALPHA_RELEASES.md#evidence-for-the-public-alpha-gate). |
| B06 — Accurate support and public copy | P1 · Partial; maintenance checks exist, beta-specific material is not prepared | Reconcile README, user docs, Nexus description, screenshots, issue statuses, requirements, and limitations with the tested candidate. Record which audit findings are fixed, awaiting reproduction, accepted limitations, or deferred. Keep initial-translation/native-speaker review limitations visible until reviewed; source or automated checks alone do not establish wording quality. |

For B03, [#127](https://github.com/circleainn/BG3ModManager-Redux/issues/127) and
[#171](https://github.com/circleainn/BG3ModManager-Redux/issues/171) remain open awaiting evidence
at this planning baseline. The broader state/separator report and the exact Vivid Landscape 4K
archive case are not claimed fixed or established as reproducible current defects. Seek the
specific reproduction or document its absence and risk; related regression coverage is not an
issue-closure criterion. Apply the same rule to other reporter-specific cases.

The October 28 foundation work starts from merged main commit `9dc72a62`. A byte-verified local
snapshot preserves the original development checkout, including the unpublished content inspector
and VOLO removal. That inspector is deferred from this foundation change; copying the original
checkout wholesale would also reintroduce documentation and maintenance drift. Its later
integration requires a separate reviewed diff and fresh evidence. No new public version or update
channel is selected by this planning work.

## Platform sequence and decision gates

1. **Windows maintenance and beta preparation.** Start B01 and B02 now; .NET 8 support ends
   November 10, 2026 ([Microsoft policy](https://dotnet.microsoft.com/en-us/platform/support/policy)).
   This is an external support deadline, not a promised Redux release date. Run B03–B06 against the
   resulting candidate. A Windows beta does not wait for every additional platform.
2. **X01 — Shared UI and Linux feasibility; open, before extensive UI replacement.** In parallel
   with maintenance, prove essential package parsing, order persistence, reviewed sync, and recovery
   on Linux using disposable data. Exercise paths/case sensitivity, filesystem permissions, native
   dependencies, cancellation, and platform integration. Prototype the same representative UI
   workflow on Windows and Linux. Record viable/rejected approaches, remaining gaps, and a bounded
   migration decision. No framework selection or native Linux support is established by this plan.
3. **X02 — Shared application rollout; gated by X01.** If a replacement UI is selected, port one
   complete workflow with shared commands/state and components, validate Windows behavior, then
   expand deliberately. Avoid a full WPF redesign that will immediately be repeated. A replacement
   UI must pass its own B03–B05 evidence; a previous Windows beta does not qualify new code. Release
   Linux support only after the declared common feature set, installation/update path, accessibility,
   and recovery pass on named supported configurations. Experimental forwarding/Wine checks do not
   satisfy this native-release gate.
4. **X03 — macOS qualification; deferred until the shared foundation is proven.** Reuse the same
   application and design system. Verify game-file behavior, architecture/native dependencies,
   permissions, distribution, updates, and the same acceptance workflow on macOS before announcing
   support. Record unavoidable game/plugin differences explicitly. Do not maintain a separate product
   design or silently call incomplete platform behavior parity.

## Evidence and release decision

For each ID, keep a dated result with source commit, exact package hash where applicable, OS/runtime
and relevant BG3 version, test data description, observed outcome, issue links, and remaining gaps.
Use **passed, failed, not performed, or explicitly excepted**, following the existing
[exception rules](PUBLIC_ALPHA_RELEASES.md#incomplete-observations-and-exceptions). Do not mark work
complete from a prototype, target-framework edit, old test count, or unavailable reporter archive.

Known crashes, data loss, unsafe writes, broken core workflows, broken installation/update paths,
and credential/privacy leaks remain release blockers. A specific missing observation needs the
recorded evidence and exception required by the publication policy; general beta enthusiasm is
not acceptance. Keep historical release records intact and append new evidence for the candidate.

Record the Windows beta decision only after B01–B06 are resolved under those rules. X01 informs
large UI investment; X02 and X03 are later platform gates, not reasons to leave the Windows beta
scope permanently open. Reassess branding after the product and representative workflow are coherent.
