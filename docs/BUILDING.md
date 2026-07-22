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

Build the complete solution with Visual Studio MSBuild:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\<version>\<edition>\MSBuild\Current\Bin\MSBuild.exe' `
  '.\BG3ModManager.sln' `
  /t:Build `
  /p:Configuration=Debug `
  /p:Platform=x64 `
  /m `
  /v:minimal
```

Use the installed Visual Studio version and edition in place of the placeholders. Building the
solution through Visual Studio with **Debug | x64** selected is equivalent.

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

## Publish build

Build the solution with `Configuration=Publish` and `Platform=x64`:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\<version>\<edition>\MSBuild\Current\Bin\MSBuild.exe' `
  '.\BG3ModManager.sln' `
  /t:Build `
  /p:Configuration=Publish `
  /p:Platform=x64 `
  /m `
  /v:minimal
```

The GUI project invokes `BuildRelease.py` after assembling `bin\Publish`. The hook expects `python`
to be available on `PATH`. If it is not, run the script directly with any Python 3 interpreter
after the Publish binaries finish compiling:

```powershell
python '.\BuildRelease.py' '0.1.0-alpha.5'
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
6. Test dialogs, source pills, category icons, custom themes, and typography.
7. Inspect the ZIP for settings, logs, keys, local paths, and development-only files.
8. Test the ZIP in a clean folder before sharing it.
