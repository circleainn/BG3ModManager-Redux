# Runtime maintenance and shared-platform direction

Reviewed October 28, 2026. This is an implementation plan and source/toolchain inventory;
it does not announce a runtime migration or native Linux/macOS support. The published
application remains on .NET 8 and Windows. Follow the release requirements in
[SECURITY](SECURITY.md#runtime-lifecycle) and the product scope in [BETA_PLAN](BETA_PLAN.md).

## Two related changes with different completion criteria

| Work | Purpose | Completion evidence |
|:--|:--|:--|
| .NET 10 Windows maintenance | Move the current application onto a supported runtime | Debug/Publish builds, regression checks, native-library execution, exact-package upgrade/recovery and release records |
| Shared Windows/Linux/macOS application | Preserve one interface and core workflow across platforms | Portable package/order processing, shared UI, platform integrations, and equivalent acceptance evidence on each supported platform |

.NET 8 support ends November 10, 2026. .NET 10 LTS support runs through November 14,
2028. The maintenance migration should proceed independently of the larger interface
decision. Dates follow [Microsoft's support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).

The product direction is one shared interface and application logic, distributed in
platform-specific packages. Layout, terminology, order formats and everyday workflows
should be shared. File dialogs, key conventions, credential storage and game locations
may need platform implementations. A native manager cannot make Windows-only game
plugins portable.

## Current evidence

| Area | Observed state | Consequence |
|:--|:--|:--|
| SDK selection | `global.json` pins `8.0.100` with `latestFeature` | Installing another major SDK does not change the selected project SDK |
| Local toolchain | SDKs 8.0.401 and 9.0.319; VS 2022 17.14; .NET 10.0.0 Desktop runtime present | Runtime presence is insufficient to build the migration; SDK 10 and a qualifying VS 2026 toolchain are missing |
| GUI and Core | Both declare WPF; Core targets `net8.0-windows` | The existing Core project is not a portable application layer |
| LSLib | Core and managed LSLib reference `LSLibNative`; the native project uses C++/CLI and `v143` | Review the native toolset for .NET 10 and replace/adapt this boundary for Linux/macOS |
| Package decoding | `compat/LSLib` provides cancellation-aware overrides, including native solid-frame LZ4 decoding | A replacement must retain cancellation, bounds checking, malformed-input rejection and output equivalence |
| Credentials and OS integration | DPAPI, registry NXM registration, Win32 launch/recycle/junction helpers | Keep common behavior behind platform services, without weakening recovery or credential protection |
| Accessibility | CrossSpeak has platform implementations, while Redux packages Windows speech libraries and uses WPF accessibility | Source availability is not packaged or tested accessibility support on Linux/macOS |
| Updater and recovery runner | WPF/.NET 8 executables | Their prerequisite transition must work before a new-runtime update is offered |

The local toolchain row is a dated observation, not a permanent project requirement.
Run the read-only inventory again when the environment changes:

```powershell
.\tools\maintenance\Test-RuntimeMigrationReadiness.ps1
.\tools\maintenance\Test-RuntimeMigrationReadiness.ps1 -AsJson
.\tools\maintenance\Test-RuntimeMigrationReadiness.ps1 -RequireToolchain
```

The final command exits 1 if basic toolchain prerequisites are missing. A successful
result only detects prerequisites; it does not validate compilation, an initialized
checkout, the application, or a release. The script reports missing submodule projects
separately and never installs software, restores packages, or changes source files.
Microsoft requires [VS 2026 18.0 or later for SDK 10](https://learn.microsoft.com/en-us/dotnet/core/install/windows#install-with-visual-studio).

## Bounded Windows migration

1. Install the supported SDK and VS desktop/C++/CLI components when the maintainer is
   ready to change the development environment; retain the existing working toolchain.
   Confirm the Windows SDK and LSLib native-toolset combination with an actual build.
2. Make SDK selection and framework changes together in an isolated branch. Include
   GUI, Core, updater, tests and applicable maintainer tools. Review vendored projects
   through Redux's compatibility policy rather than silently modifying submodule pins.
3. Validate native loading and package processing first, then the full Debug/Publish
   suite, accessibility, localization, saved orders, snapshots and recovery. Review
   dependency reports; a newer version alone is not a reason to update every package.
4. Test the transition from the current public package on a clean environment without
   .NET 10. Choose and verify a prerequisite/bridge path or publish explicit manual
   migration instructions. Do not offer an updater that cannot start on the old setup.
5. Record exact candidate source/artifact identity, tested Windows versions, remaining
   observations and installation requirements. Update public requirements only when
   that candidate has passed the release gate.

## Bounded Linux feasibility proof

Retain essential LSLib functionality and its notices. Removing the new VOLO importer
does not require removing LSLib or other necessary runtime libraries. C++/CLI itself is
[Windows-only](https://learn.microsoft.com/en-us/dotnet/core/porting/cpp-cli#ccli-net-core-limitations);
renaming a DLL or adding Linux runtime identifiers does not port this bridge.

1. Identify the package/resource APIs Redux actually needs. Build a minimal package
   reader harness independent of WPF using synthetic or appropriately licensed fixtures.
   Resolve the native boundary with a portable implementation or a narrow native API.
   Preserve existing format coverage and cancellation/rejection tests.
2. On an actual Linux environment, read ordinary, solid/compressed and multipart PAKs,
   extract metadata, cancel work, and reject malformed archives. Record the architecture,
   dependency versions, fixture hashes and results. Cross-compilation is not execution.
3. Read, save and safely replace a disposable profile's order; verify restart,
   cancellation, failure and restoration. Exercise case-sensitive paths, containment,
   symlinks and permissions before connecting real game profiles.
4. Implement one representative shared UI workflow: list/search, selection, reorder,
   details, Save/Sync state and recovery. Compare Windows and Linux using the same
   workflow specification and [interface principles](UI_DESIGN.md). Evaluate keyboard
   access, scaling, large lists and responsiveness before extending it app-wide.
5. Review macOS dependency and packaging feasibility early, then execute equivalent
   native checks before promising support. Choose a framework only after the package
   and representative interface proofs identify the remaining integration work.

The output of this proof is a recorded implementation decision and measured gaps.
If the shared interface is not ready for the beta scope, continue a focused Windows
beta while the port remains experimental. Avoid a permanent separately designed Linux
edition, and avoid broad WPF-only visual rewrites before the shared-interface decision.

No Linux execution, .NET 10 application build, or release qualification is claimed by
this inventory. Track evidence and remaining work in the beta backlog and release record.
