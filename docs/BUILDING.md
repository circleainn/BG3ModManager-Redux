# Building Redux from source

## Prerequisites

- Windows, with Visual Studio 2022+ (or the standalone Build Tools) including the .NET desktop
  development and C++ desktop development workloads. Redux's native `LSLibNative` project needs the
  C++ toolset even though the rest of the solution is C#.
- .NET 8 SDK.

## Build command

Build the whole solution with MSBuild directly rather than the `dotnet` CLI:

```
& 'C:\Program Files\Microsoft Visual Studio\<version>\<edition>\MSBuild\Current\Bin\MSBuild.exe' '.\BG3ModManager.sln' /t:Build /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
```

`dotnet build` will build the managed projects, but it deletes `bin\Debug\Ijwhost.dll` as part of
its own output cleanup, which silently breaks `.pak` parsing (see below). MSBuild does not have
this problem.

## Known gotcha: `Ijwhost.dll`

After **every** build, re-copy `Ijwhost.dll` from `x64\Debug\Ijwhost.dll` to `bin\Debug\Ijwhost.dll`
(should be 117,520 bytes). If this file is missing or stale, the app will start normally but every
mod list will silently come back empty — there's no error, just no mods. This is the single most
common "why are there no mods showing" cause during local development.

```
Copy-Item -Path '.\x64\Debug\Ijwhost.dll' -Destination '.\bin\Debug\Ijwhost.dll' -Force
```

## Running a locally-built debug binary

Close any previously-running `BG3ModManager.exe` before rebuilding — MSBuild will fail with
`MSB3027`/`MSB3021` (file locked) if the previous build's exe is still running.

## Release packaging

See `BuildRelease.py` for the release artifact list and packaging steps. Redux ships as a
framework-dependent build — the .NET 8 Desktop Runtime must already be installed on the target
machine. There are no current plans to switch to a self-contained deployment (see "Known alpha
limitations" in the main README).
