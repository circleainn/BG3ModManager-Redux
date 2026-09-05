# Optional features

Redux preserves the inherited mod-manager core and layers newer features around it. Provider
metadata and diagnostics must not become prerequisites for scanning packages, managing the active
list, importing or exporting load orders, detecting game paths, using LSLib, or performing normal
file operations.

At runtime, `ReduxModuleState` is the central reactive contract for optional-module availability.
Provider services and source-related UI consume `SourceIntegrationsEnabled`; diagnostics consume
`ModDiagnosticsEnabled` and `LoadOrderGuidanceEnabled`. Feature code should not reinterpret the
underlying preference values independently.

The first-run setup is also available from Help. Source linking, Mod Diagnostics, and experimental
load-order guidance begin disabled so each optional feature is explicitly enabled by the user.
Returning users keep their saved choices. Theme, motion, and background effects preview live and
return to their previous values if the window is dismissed. API keys and settings are stored only
after **Save & Continue**. Provider keys are masked and encrypted for the current Windows account;
they are excluded from normal settings and diagnostic exports. The setup does not change packages
or load orders.

## Source linking and online mod information

This feature retains BG3MM's Nexus Mods metadata, links, images, and update foundation, then adds
mod.io, manual page linking, and the reviewed Redux database fallback. It can be disabled with
**Disable online mod information** in Preferences.

When online mod information is disabled, Redux:

- does not request Nexus Mods or mod.io metadata;
- cancels dedicated provider metadata work already in progress;
- does not enrich imports from the bundled source database;
- hides source-linking actions and the Source column;
- disables provider API-key and provider-warning controls without clearing their saved values;
- presents installed packages as Local; and
- retains existing provider associations so they return if integrations are re-enabled.

Package scanning and core manager behavior continue normally.

The inherited **Refresh Mod Updates** operation also services Workshop and GitHub metadata.
Disabling online mod information does not cancel the whole shared operation. Nexus Mods and mod.io
stages that have not started are skipped; unrelated update sources continue normally.

Disabling online information does not disable Mod Diagnostics. Its checks use locally parsed
package information and remain useful offline. Source-specific warnings, such as the mod.io restore
notice, disappear while online identities are hidden.

The mod.io restore notice warns that deleting a local PAK is not the same as unsubscribing. BG3 can
download subscribed mods again, and Steam Cloud can preserve an app-specific cached copy even after
the user unsubscribes. Redux therefore recommends keeping one manager authoritative rather than
mixing its exported order with the in-game/mod.io manager.

## Mod Diagnostics

Mod Diagnostics is the single user-facing diagnostic and guidance system. It evaluates facts
Redux has already detected and never repairs, installs, removes, reorders, or rewrites anything.
**Enable mod diagnostics** can be enabled independently from source linking.

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

When Mod Configuration Menu is installed but absent from the active order, diagnostics explain that
its override files can make part of MCM appear in game even though its normal module entry was not
exported. MCM's in-game reference to BG3MM includes compatible managers such as Redux; the corrective
action is to activate MCM and use **Export to Game**.

Dependency findings provide conservative follow-up actions without installing anything. Redux can
show or activate an installed inactive dependency, open a known source page, or copy the declared
UUID. For a completely missing dependency, a source-page action appears only when the reviewed
bundled database maps that exact module UUID to one Nexus project. Unknown or ambiguous UUIDs keep
the copy-only fallback, and source actions remain hidden when online mod information is disabled.

### Experimental load-order guidance

Load-order guidance is an experimental, opt-in Mod Diagnostics rule family—not an automatic sorting
system. **Include experimental load-order guidance** is disabled by default and does not run unless
Mod Diagnostics is enabled.

These rules report when an active package's explicitly declared dependency is positioned later in
the numbered order and when active declared dependency metadata forms a cycle that no linear order
can satisfy. They do not infer category, author, framework, patch, or compatibility ordering and do
not move mods. Inactive packages and always-loaded override packages are excluded because neither
has a meaningful position in the normal `modsettings.lsx` order. The rules remain registered
separately from the default checks so the experimental family can be omitted without changing the
rest of Mod Diagnostics.

All enabled findings share one toolbar status, compact top-menu indicator, grouped finding popup,
selected-mod presentation, and severity language. The unified interface does not remove the
internal rule boundary or the saved opt-in preference.

## Extension requirements

Optional features must remain:

- reversible through a clear preference;
- read-only unless a separate, explicit user action authorizes a change;
- absent from the interface when disabled where practical;
- independent of package discovery and load-order persistence;
- conservative when evidence is incomplete; and
- safe to remove without changing inherited backend behavior.
