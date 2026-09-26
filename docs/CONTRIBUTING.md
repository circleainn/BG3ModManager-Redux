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

## Pull requests

Keep a pull request cohesive. Explain the user-visible result, safety boundary, validation, and any
remaining manual-testing requirement. Do not combine an unrelated refactor with a focused fix.

Update durable documentation when behavior, action names, settings, public formats, trust rules, or
support boundaries change. Keep chronological release details in [CHANGELOG.md](CHANGELOG.md).

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
