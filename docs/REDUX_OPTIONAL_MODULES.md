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

Source integrations retain BG3MM's inherited Nexus Mods metadata, links, images, and update
foundation, then extend it with mod.io, manual relinking, richer Redux presentation, and the
reviewed Redux database fallback. The complete provider layer can be disabled with **Local-only
mode** in Preferences.

While Local-only mode is enabled, Redux:

- does not request Nexus Mods or mod.io metadata;
- cancels dedicated provider metadata work already in progress;
- does not enrich imports from the bundled source database;
- hides source-linking actions and the Source column;
- disables provider API-key and provider-warning controls without clearing their saved values;
- presents installed packages as Local; and
- retains existing provider associations so they return if integrations are re-enabled.

Package scanning and core manager behavior continue normally.

The inherited **Refresh Mod Updates** operation also services Workshop and GitHub metadata. Switching
to Local-only mode does not cancel that whole shared operation: source-provider stages that have not
started are skipped, and an already-running provider request is allowed to finish disposal without
interrupting the unrelated inherited providers.

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

Current checks cover invalid or duplicate UUIDs, missing or inactive dependencies, self-dependency
metadata, installed dependencies below a declared minimum version, declared conflicts, Script
Extender availability, legacy Mod Fixer and override behavior, provider-specific safety notes, and
invalid embedded Redux creator manifests.

## Load Order Advisor

Load Order Advisor is an experimental extension of Mod Health, not an automatic sorting system. It
is disabled by default and does not run unless both **Enable Mod Health** and **Enable Load Order
Advisor** are enabled.

The advisor reports when an active package's explicitly declared dependency is positioned later in
the numbered order and when active declared dependency metadata forms a cycle that no linear order
can satisfy. It does not infer category, author, framework, patch, or compatibility ordering and
does not move mods. Advisor rules are registered separately from the general health rules so the
module can be omitted without changing the rest of Mod Health.

## Extension requirements

Optional Redux modules must remain:

- reversible through a clear preference;
- read-only unless a separate, explicit user action authorizes a change;
- absent from the interface when disabled where practical;
- independent of package discovery and load-order persistence;
- conservative when evidence is incomplete; and
- safe to remove without changing inherited backend behavior.
