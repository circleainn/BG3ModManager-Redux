# Optional Redux modules

Redux preserves the inherited mod-manager core and layers newer features around it. Provider
metadata and diagnostics must not become prerequisites for scanning packages, managing the active
list, importing or exporting load orders, detecting game paths, using LSLib, or performing normal
file operations.

At runtime, `ReduxModuleState` is the central reactive contract for optional-module availability.
Provider services and source-related UI consume `SourceIntegrationsEnabled`; diagnostics consume
`ModDiagnosticsEnabled` and `LoadOrderGuidanceEnabled`. Feature code should not reinterpret the
underlying preference values independently.

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

Local-only mode does not disable Mod Diagnostics. Its checks use locally parsed package metadata and
remain useful without provider integration. Provider-specific findings, such as the mod.io
restoration notice, naturally disappear while source identities are masked.

## Mod Diagnostics

Mod Diagnostics is the single user-facing diagnostic and guidance system. It evaluates facts
Redux has already detected and never repairs, installs, removes, reorders, or rewrites anything.
**Enable mod diagnostics** is on by default and can be turned off independently.

When disabled, Redux cancels pending analysis, clears computed snapshots, and removes diagnostic
toolbar, row, drawer, hover-card, compact-menu, and debug indicators. Mod loading and core behavior
continue normally.

Checks implement `IModHealthRule` and receive an immutable `ModHealthAnalysisContext`. The
`IModHealthAnalyzer` composes those rules into display snapshots. This keeps diagnostic rules
separate from the main window and makes individual rule families removable.

The default checks cover invalid or duplicate UUIDs, missing or inactive dependencies,
self-dependency metadata, installed dependencies below a declared minimum version, declared
conflicts, Script Extender availability, legacy Mod Fixer and override behavior, provider-specific
safety notes, and invalid embedded Redux creator manifests.

### Experimental load-order guidance

Load-order guidance is an experimental, opt-in Mod Diagnostics rule family—not an automatic sorting
system. **Include experimental load-order guidance** is disabled by default and does not run unless
Mod Diagnostics is enabled.

These rules report when an active package's explicitly declared dependency is positioned later in
the numbered order and when active declared dependency metadata forms a cycle that no linear order
can satisfy. They do not infer category, author, framework, patch, or compatibility ordering and do
not move mods. The rules remain registered separately from the default checks so the experimental
family can be omitted without changing the rest of Mod Diagnostics.

All enabled findings share one toolbar status, compact top-menu indicator, grouped finding popup,
selected-mod presentation, and severity language. The unified interface does not remove the
internal rule boundary or the saved opt-in preference.

## Extension requirements

Optional Redux modules must remain:

- reversible through a clear preference;
- read-only unless a separate, explicit user action authorizes a change;
- absent from the interface when disabled where practical;
- independent of package discovery and load-order persistence;
- conservative when evidence is incomplete; and
- safe to remove without changing inherited backend behavior.
