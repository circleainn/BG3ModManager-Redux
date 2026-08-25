# Building Redux from source

## Prerequisites

- Windows with Visual Studio 2022 or newer, or the equivalent standalone Build Tools.
- The **.NET desktop development** workload.
- The **Desktop development with C++** workload. `LSLibNative` is a C++/CLI project and cannot be
  produced by a managed-only build.
- The .NET 8 SDK and .NET 8 Desktop Runtime.
- Python 3 for release packaging.

Redux is a Windows-only WPF application. Linux, macOS, Wine, and Proton are not supported build or
runtime targets.

## Debug x64 build

Use the repository build helper. It locates the newest compatible Visual Studio installation with
`vswhere`, verifies that the managed desktop, MSVC, and C++/CLI components are installed, and then
restores dependencies before building the complete native and managed solution:

```powershell
& '.\Build-Redux.ps1' -Configuration Debug
```

This avoids hardcoding a Visual Studio release or edition. Building the solution through Visual
Studio with **Debug | x64** selected is equivalent.

On a clean clone, the helper also downloads LSLib's GPPG 1.5.2 parser tools from the location named
by upstream LSLib and verifies the archive against a pinned SHA-256 checksum. The tools generate
ignored parser source files during the build and are not included in Redux packages.

Do not use `dotnet build` as the normal Redux build path. It does not build the native project graph
the same way and can clean the C++/CLI loader shim from the final debug directory.

## Required native loader shim

After every build, verify:

```powershell
(Get-Item '.\bin\Debug\Ijwhost.dll').Length
```

The current expected result is:

```text
117520
```

The complete x64 MSBuild normally copies the correct file automatically. If another build path or
an incremental cleanup removed it, restore it from the native output:

```powershell
Copy-Item -Path '.\x64\Debug\Ijwhost.dll' -Destination '.\bin\Debug\Ijwhost.dll' -Force
```

`Ijwhost.dll` is required to load `LSLibNative.dll`. If it is missing or stale, Redux may launch
normally while `.pak` parsing fails and the installed-mod lists appear empty.

## Running a debug build

Close any running `BG3ModManager.exe` before rebuilding. MSBuild reports `MSB3027` or `MSB3021`
when the previous process still holds an output file open.

The primary executable is:

```text
bin\Debug\BG3ModManager.exe
```

Local debug data, settings, imported fonts, imported icons, caches, and logs are runtime user state.
Do not commit or distribute them.

## Redux regression checks

Run the focused, non-shipping regression suite with:

```powershell
& '.\Test-Redux.ps1'
```

The helper restores from the local NuGet package cache first and can use the official NuGet feed
for packages that are not cached. It locates the same Visual Studio C++/CLI toolchain used by the
main build and runs the checks as a standalone executable. The suite covers creator-manifest
validation, source-provider precedence, Local-only presentation, contribution-report privacy, and
cached creator association invalidation. It also verifies Mod Diagnostics identity, dependency,
conflict, Script Extender, legacy Mod Fixer, and force-loaded override findings, plus invalid
embedded-manifest reporting and the opt-in boundary around experimental load-order guidance.
It does not alter settings, installed mods, or load orders. Portable bundle checks cover exact
load-order and presentation round trips, atomic replacement, preservation after failed validation,
and the rule that `.bg3redux` archives never contain or accept `modsettings.lsx`.

## Publish build

Build the solution with `Configuration=Publish` and `Platform=x64`:

```powershell
& '.\Build-Redux.ps1' -Configuration Publish
```

The GUI project invokes `BuildRelease.py` after assembling `bin\Publish`. The hook expects `python`
to be available on `PATH`. If it is not, run the script directly with any Python 3 interpreter
after the Publish binaries finish compiling:

```powershell
python '.\BuildRelease.py' '0.1.0-alpha.10'
```

Use the actual display version from the project when producing a later build.

The release packager:

- Removes settings, logs, caches, backups, development symbols, and other runtime user data.
- Copies the public README and project license.
- Produces one consolidated `THIRD-PARTY-NOTICES.md` containing attribution and complete license
  texts.
- Verifies `LSLib.dll`, `LSLibNative.dll`, and `Ijwhost.dll`.
- Removes local workspace paths embedded in supported binaries.
- Rejects forbidden files and private build metadata.
- Creates a versioned ZIP and updates `BG3ModManager-Redux-Latest.zip`.

Redux uses a framework-dependent deployment. Test machines must already have the .NET 8 Desktop
Runtime installed.

## Validation before publishing

Before distributing a build:

1. Confirm the solution built with zero errors.
2. Confirm `bin\Debug\Ijwhost.dll` or `bin\Publish\_Lib\Ijwhost.dll` is present and correct.
3. Launch Redux and confirm installed `.pak` files are detected.
4. Test drag-and-drop, profile selection, load-order loading, and export.
5. Test Redux Dark, Redux Light, and Parchment.
6. Test dialogs, source indicators and provider actions, category icons, custom themes, and typography.
7. Inspect the ZIP for settings, logs, keys, local paths, and development-only files.
8. Test the ZIP in a clean folder before sharing it.
