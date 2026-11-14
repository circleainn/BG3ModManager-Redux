# Security policy

## Supported versions

Only the newest published Redux public alpha receives security fixes. Older public alphas, private
test builds, and source snapshots are unsupported once a newer build is available.

### Runtime lifecycle

The published 16.7 application requires .NET 8 Desktop Runtime. Microsoft ends .NET 8 support on
November 10, 2026; the current release does not yet provide a supported-runtime migration.
The maintenance target is .NET 10 LTS, subject to validation of the Windows desktop and C++/CLI
dependencies. Installing a newer major runtime alone does not retarget the existing application.
See Microsoft's [support policy](https://dotnet.microsoft.com/en-us/platform/support/policy) and
[Windows SDK/Visual Studio requirements](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

Before publishing a migration:

Use `tools/maintenance/Test-RuntimeMigrationReadiness.ps1` for a read-only prerequisite inventory.
Its output distinguishes an installed runtime from the SDK/native build toolchain; it is not
evidence that the migration or release passed. The [implementation plan](RUNTIME_PORTABILITY_PLAN.md)
records the bounded maintenance and portability work.

1. Validate the .NET 10 SDK and Visual Studio 2026 toolchain locally and in CI, including C++/CLI,
   Windows SDK, LSLib, CrossSpeak, and native hosting. Verify the supported Windows editions and
   versions; do not assume the current Windows 10/11 wording transfers unchanged.
2. Retarget and test the application, Core, updater/snapshot runner, tests, and maintainer tools
   together. Review submodule targets through the existing compatibility policy and retain notices.
3. Exercise the full Debug/Publish regressions and exact candidate package, especially package
   parsing, accessibility, localization, native DLL loading, snapshots, rollback, and saved-state
   compatibility. Preserve a copy of the previous runtime's installation for recovery.
4. Validate an update from the current public app on a machine without .NET 10. A framework-dependent
   new updater or app may fail to start there. Provide a tested prerequisite/bridge path or explicit
   manual migration instructions before offering that candidate through the public channel.
5. Update SDK pinning, CI runners, runtime requirements, release notes and public installation
   guidance only when the selected configuration passes. Record remaining real-user observations
   under the release gate; do not claim a completed migration from a target-framework edit alone.

## Report a vulnerability

Do not open a public issue for a vulnerability that could expose credentials, overwrite files
outside an intended destination, execute untrusted content, bypass archive validation, or disclose
private user data.

Use GitHub's [private vulnerability-reporting form](https://github.com/circleainn/BG3ModManager-Redux/security/advisories/new).
If GitHub does not make that form available to you, contact the maintainer through a private
channel listed on the official Redux project pages and include only the information needed to
reproduce the problem.

Please include:

- affected Redux version and Windows version;
- the smallest safe reproduction;
- expected and actual security boundary;
- whether credentials, arbitrary paths, executable content, or user files are involved; and
- suggested mitigation, if known.

Do not send live credentials, signed download URLs, copyrighted mod archives, private saves, or
personal data. Use synthetic fixtures wherever possible.

## Security boundaries

Redux treats archives, manifests, mod metadata, provider responses, paths, and downloaded files as
untrusted input. Reviewed package recognition is not permission to execute native code. A package
must still pass its destination-specific inspection and the user must authorize installation.

The project prioritizes path containment, staged writes, atomic replacement, verified downloads,
credential redaction, conservative native-file ownership, and recoverable deletion. See
[Privacy and local data](PRIVACY_AND_DATA.md) for the intended data boundary.

Application updates use a fixed official GitHub channel document. Redux rejects unexpected
channels, versions, properties, hosts, release paths, archive sizes, hashes, archive paths, and
release contents before launching its updater. The updater runs outside the installation, waits
for Redux to exit, and mutates only paths declared by the new or previously installed release
inventory. Unlisted local files are outside its ownership boundary.

Public releases are distributed as portable ZIPs. Fresh installations are explicit extractions into
a user-chosen writable folder; no public bootstrapper receives authority to choose or register an
installation path. The retired installer source is not retained in the repository.
