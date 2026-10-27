# Contributing to BG3 Mod Manager Redux

Focused bug reports, documentation improvements, tested fixes, and reviewed mod-identification data
are welcome. Redux is still in alpha, so discuss large architectural or product changes in an issue
before investing in an implementation.

## Before changing code

- Read the [documentation index](README.md) and
  [changes from upstream](CHANGES_FROM_UPSTREAM.md).
- Search existing issues and recent commits.
- Preserve inherited BG3MM behavior unless the change explicitly replaces it.
- Reuse Redux services, shared semantic resources, controls, and terminology.
- For interface changes, follow the [shared UI rules and workflow checks](UI_DESIGN.md); review the
  complete task across affected panes rather than only the edited control.
- Never include credentials, user state, private paths, downloaded mods, saves, or build outputs.

## Build and validate

Use Windows 10 or 11 x64 with .NET 8 SDK/Desktop Runtime and Visual Studio's managed-desktop,
Desktop C++, and C++/CLI components. Clone with submodules. The supported local workflow is:

```powershell
.\Build-Redux.ps1 -Configuration Debug
.\Test-Redux.ps1
git diff --check
```

`Test-Redux.ps1` uses the same solution build as `Build-Redux.ps1`, including native dependencies,
then runs every regression check. For focused diagnostics after a Debug build, pass exact check
names from the failure log to `tests\Redux.Core.Tests\bin\x64\Debug\net8.0-windows10.0.22621.0\Redux.Core.Tests.exe`.
Unknown names fail instead of reporting an empty run as successful.

Changes to the experimental native Linux forwarding helper also require
`python -m unittest discover -s tests/linux -v`. These tests simulate dispatch; they do not replace
real Wine desktop, browser, and restart checks.

Publication metadata and maintenance-tool changes also require:

```powershell
python tools/maintenance/check_project_docs.py
python -m unittest discover -s tests/maintenance -v
.\tests\maintenance\Test-DependencyReports.ps1
```

Changes affecting release packaging should also build the Publish configuration. Filesystem,
archive, download, native-mod, and load-order changes need tests for failure, cancellation, and
recovery—not only the success path. Visual changes require inspection across built-in themes,
supported text sizes, common Windows scaling, and Reduce Motion.

Release, updater, runtime-name, or packaged-document changes require the complete public gate:

```powershell
.\Build-Redux.ps1 -Configuration Debug -Rebuild
.\Test-Redux.ps1
.\Build-Redux.ps1 -Configuration Publish -Rebuild -PythonExecutable "C:\Path\To\python.exe"
git diff --check
```

The Publish build creates the portable ZIP and update-channel manifest locally; it does not publish
a GitHub release. Do not commit generated archives, build directories, logs, user settings, or local
test fixtures.

### Dependency maintenance

Run `tools/maintenance/Check-Dependencies.ps1` on Windows with the supported build toolchain.
It builds/restores the C++/CLI graph, restores the separate maintainer tools, and records both
vulnerability and available-update reports under `artifacts/maintenance/`. After a successful Debug
build in the same checkout, `-SkipBuild` reuses that restore. Reports include transitive dependencies;
failed queries and reported advisories fail the check instead of being interpreted as clean.

The Dependency maintenance workflow runs weekly and can be dispatched manually. Its reports identify
review candidates; it does not install upgrades or create automatic dependency PRs. The Windows CI
uses the same audit. This provides a Windows review path while Linux Dependabot restoration cannot
evaluate the native C++/CLI project. Dependabot's automatic PR creation remains a separate limitation.

Review each advisory and release note, update the owning `PackageReference` in a focused branch,
then rebuild Debug and Publish and run the full regression suite. Treat vendored submodule updates
separately: review the pinned revision, compatibility overrides, native output, and notices. Do not
upgrade an entire dependency graph merely because newer packages are listed.

### Merge checks

Require the successful `Build and regression checks` result for the current PR head before merging.
Recommended main/dev repository rules prohibit force pushes and branch deletion, require pull
requests and that check, and resolve review conversations. A solo-maintainer acceptance record can
state the inspected change, exact head, automated results, and remaining manual checks; it must not
claim an independent review that did not occur. Repository rules require administrator access and
are separate from this documentation; a workflow file alone does not enforce them.

## Pull requests

Keep a pull request cohesive. Explain the user-visible result, safety boundary, validation, and any
remaining manual-testing requirement. Do not combine an unrelated refactor with a focused fix.

Update durable documentation when behavior, action names, settings, public formats, trust rules, or
support boundaries change. Keep chronological release details in [CHANGELOG.md](CHANGELOG.md).
Keep issue triage in GitHub and work summaries in the conversation. Do not add dated audit reports,
session logs, or duplicate issue inventories to the repository documentation. Update existing guides
only when the findings change lasting product or development guidance.

## Interface translations

Bundled strings live in `src/Core/Localization/InterfaceStrings.json`, with stable keys and `en`,
`fr`, `es`, `ru`, and `zh-Hans` values. Keep keys stable when correcting translations. Native-speaker
review is welcome; preserve action meaning and distinguish saving a Redux order from syncing it to
the game. Missing translations fall back to English.

Use `loc:Text` for static XAML labels and `LocalizationBindings.Set` for UI-owned labels created in
code. Use `loc:BoundText` only to display a known UI label whose underlying English identifier must
remain stable. Never translate user content, paths, persistent identifiers, or column sort keys.
Use `loc:FormatText` for a translated sentence containing one bound count, name, or path; its
`Key` identifies the sentence template and its argument is preserved unchanged.
For messages containing names, paths, or counts, use `ReduxLanguageService.Format` on the
interpolated UI sentence so its template is translated before unchanged values are inserted. The
external snapshot runner embeds the same catalog and receives the selected language with its palette.
Test live switching in both directions, saved preferences, custom fonts, and long labels at larger
text sizes. Language selection must not change formatting culture or typography preferences.

## Mod database contributions

Do not submit mod archives or PAKs. Generate a privacy-limited `.bg3redux-report`, independently
verify its provider identity, and follow the [Redux mod database guide](REDUX_MOD_DATABASE.md).
Reports are evidence for review; they are never merged automatically.

## Attribution

Redux is a fork of LaughingLeader's BG3 Mod Manager and includes third-party components and assets.
Preserve copyright, provenance, and required notices. Do not rename copied work to conceal its
origin. Review [Third-Party Notices](../licenses/Third-Party-Notices.md) before adding a dependency,
font, icon set, dataset, or substantially adapted code.


### Button consistency

Use shared styles from `MainResourceDictionary.xaml` rather than recreating semantic colors
on a general button template:

- Install, reinstall, and download actions: `ReduxMinorSuccessActionButtonStyle`.
- Delete/remove actions: `ReduxMinorDestructiveActionButtonStyle`.
- Reset/clear-history actions: `ReduxMinorWarningActionButtonStyle`.
- Save/apply/continue actions: `ReduxPrimaryActionButtonStyle`.
- Cancel/close/navigation actions: `ReduxSecondaryActionButtonStyle`.

Keep stronger destructive confirmation styling where the decision warrants it. Dynamic buttons
must switch styles when their action changes (for example, Install becoming Close). Toolbar
variants may adjust sizing but should inherit the appropriate semantic style. The main
Install Mod toolbar action intentionally retains the shared gradient workflow style to match
its neighboring toolbar actions. Do not give every
button the same color: color communicates the action.

Center icons vertically in icon-and-label rows. Button margins belong on buttons, not their
icons; icons use only the gap before the label. Icon-only buttons need an accessible name and
padding that leaves room for the glyph. Check disabled, hover, keyboard-focus, and large-text
states when changing a shared template.

Release prose shown in Redux or on Nexus should describe changes and fixes without calling the
release "silent". Announcement suppression is publishing metadata, not a user-facing feature.
Owned review windows should use `ReduxWindowBehavior.AttachDialogTransitions` so background
blur/dimming follows the shared preference. Reusable hide-on-close windows use `HideWindowBase`;
message boxes use `ShowDialogWithOwnerBackdrop`. The custom-theme editor intentionally leaves
the preview surface undimmed so users can judge colors accurately.

Use brief inline statuses (for example, Managed, Unmanaged, or Backup unavailable). Put ownership
scope and recovery explanations in tooltips and the relevant review, rather than repeating the
app name in every row. Passive status text should not resemble an outlined action button.

## Development and release branches

`dev` is ongoing development, not a downloadable release channel. Commit work and maintain
`docs/CHANGELOG.md` under Unreleased without bumping versions or creating release tags. CI still
builds and tests dev, but portable artifacts are uploaded only for main. Public GitHub/Nexus
releases are prepared from main after explicit release approval. Never publish a dev prerelease
or repoint the public-alpha updater channel to development work.
