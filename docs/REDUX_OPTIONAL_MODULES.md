# Optional Redux modules

Redux preserves the inherited mod-manager core and layers newer features around it. Provider
metadata and diagnostics must not become prerequisites for scanning packages, managing the active
list, importing or exporting load orders, detecting game paths, using LSLib, or performing normal
file operations.

At runtime, `ReduxModuleState` is the central reactive contract for optional-module availability.
Provider services and source-related UI consume `SourceIntegrationsEnabled`; diagnostics consume
`ModHealthEnabled` and `LoadOrderAdvisorEnabled`. Feature code should not reinterpret the underlying
preference values independently.

## Source integrations

Source integrations add Nexus Mods and mod.io metadata, links, images, update information, and the
reviewed Redux database fallback. They can be disabled with **Local-only mode** in Preferences.

While Local-only mode is enabled, Redux:

- does not request Nexus Mods or mod.io metadata;
- does not enrich imports from the bundled source database;
- hides source-linking actions and the Source column;
- presents installed packages as Local; and
- retains existing provider associations so they return if integrations are re-enabled.

Package scanning and core manager behavior continue normally.

Local-only mode does not disable Mod Health or Load Order Advisor. Those modules use locally parsed
package metadata and remain useful without provider integration. Provider-specific findings, such
as the mod.io deletion warning, naturally disappear while source identities are masked.

## Mod Health

Mod Health is a read-only diagnostics module. It evaluates facts Redux has already detected and
never repairs, installs, removes, reorders, or rewrites anything. **Enable Mod Health** is on by
default and can be turned off independently.

When disabled, Redux cancels pending health refreshes, clears computed snapshots, and removes
health header, row, drawer, and debug indicators. Mod loading and all core manager operations
continue normally.

Checks implement `IModHealthRule` and receive an immutable `ModHealthAnalysisContext`. The
`IModHealthAnalyzer` composes those rules into display snapshots. This keeps diagnostic rules
separate from the main window and makes individual rule families removable.

## Load Order Advisor

Load Order Advisor is an experimental extension of Mod Health, not an automatic sorting system. It
is disabled by default and does not run unless both **Enable Mod Health** and **Enable Load Order
Advisor** are enabled.

The current advisor rule only reports when an active package's explicitly declared dependency is
positioned later in the numbered order. It does not infer category, author, framework, patch, or
compatibility ordering and does not move mods. The advisor is registered separately from the
general health rules so it can be omitted without changing the rest of Mod Health.

## Extension requirements

Optional Redux modules must remain:

- reversible through a clear preference;
- read-only unless a separate, explicit user action authorizes a change;
- absent from the interface when disabled where practical;
- independent of package discovery and load-order persistence;
- conservative when evidence is incomplete; and
- safe to remove without changing inherited backend behavior.
