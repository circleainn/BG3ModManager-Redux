# Diagnostics and optional features

This contributor guide defines which Redux systems are built in, which are optional, and what must
continue working when optional features are disabled.

The short version is simple:

| System | Default boundary |
|:--|:--|
| Core mod management | Always available |
| Mod Diagnostics | Always available and read-only |
| Online mod information | Optional |
| Load Order Advisor | Optional and experimental |

Package scanning, Active and Inactive Mods, saved orders, game-path discovery, LSLib, file
operations, and normal load-order editing must never depend on a provider or advisor being enabled.

## Runtime contract

`ReduxModuleState` is the shared reactive source for feature availability:

- provider services and source UI consume `SourceIntegrationsEnabled`;
- built-in checks consume `ModDiagnosticsEnabled`; and
- ordering rules and organizer UI consume `LoadOrderGuidanceEnabled`.

Feature code should consume those values instead of interpreting preference fields independently.
That keeps menus, background work, diagnostics, and detail surfaces in agreement.

On first launch, online information and Load Order Advisor guidance begin disabled. Returning users
keep their saved choices. Mod Diagnostics has no off switch.

## Built-in Mod Diagnostics

Mod Diagnostics turns locally known package facts into consistent explanations and actions. It is
always part of Redux and does not require network access.

Findings can appear in mod rows, hover cards, the details drawer, toolbar status, Quick Access, and
relevant review windows. The default checks cover detectable conditions such as:

- missing, inactive, self-referencing, outdated, or cyclic dependencies;
- invalid or duplicate module UUIDs;
- declared package conflicts;
- Script Extender requirements and availability;
- Mod Fixer, force-loaded, and override behavior;
- invalid embedded creator manifests; and
- provider-specific safety notes when provider identity is visible.

Checks implement `IModHealthRule` and receive an immutable `ModHealthAnalysisContext`.
`IModHealthAnalyzer` composes them into display snapshots, keeping individual rule families outside
the main window coordinator.

Diagnostics never silently download, repair, remove, activate, reorder, or rewrite a package. A
user can explicitly reveal or activate an installed dependency, open a reviewed source page, or copy
a declared UUID. Missing-dependency source actions appear only when the bundled database maps that
exact identity to one reviewed project.

One inherited edge case receives a specific explanation: Mod Configuration Menu can expose some
override files in game even when its normal module is inactive. Redux advises activating MCM and
using **Sync Load Order to Game** rather than implying that the partial appearance is a complete
installation.

## Optional online mod information

**Disable online mod information** turns off Nexus Mods and mod.io enrichment while leaving local
package management intact. Redux retains existing associations so they can return if the feature is
enabled again.

While disabled, Redux:

- skips Nexus Mods and mod.io requests and cancels dedicated provider work;
- does not enrich new imports from the bundled source database;
- hides source-linking actions and the Source column;
- disables provider key and provider-warning controls without erasing saved values; and
- presents packages as **Local**.

The inherited **Refresh Mod Updates** workflow can also service non-provider sources. Disabling
online mod information skips Nexus and mod.io stages that have not started; it does not cancel
unrelated update work.

Local diagnostics remain active. Source-specific warnings disappear when their identity is hidden.
For example, the mod.io safety note explains that removing a local PAK does not unsubscribe it and
that BG3 or Steam Cloud may restore managed content.

Provider keys are masked and stored outside ordinary settings using protection tied to the current
Windows account. They are excluded from diagnostic exports and contribution reports.

NXM link handling belongs to this optional boundary. Redux registers `nxm://` only after an
explicit choice, restores the previous per-user handler where possible, and disables network queue
work when online mod information or the Nexus API key is unavailable. Public project/file identity,
safe filenames, transfer state, and archive hashes may be persisted. Temporary download keys and
signed URLs must remain memory-only and must never appear in settings, queue files, logs, crash
reports, or exported diagnostics. Receiving or completing a download never authorizes activation,
load-order changes, or game sync.

The retained Package Archive Library is a separate optional storage choice and remains off by
default. Enabling NXM links does not enable archive retention. When retention is enabled, the
archive index may store public Nexus project/file IDs, a verified SHA-256 package identity, safe
filenames, version, detected destination, and installation time. It must not store local source
paths, provider credentials, temporary keys, signed URLs, or remote thumbnail URLs. Download
history, retained archives, and installed content have independent clear/remove behavior.

## Optional Load Order Advisor

**Enable Load Order Advisor** adds an experimental guidance family to Mod Diagnostics. It uses exact
package declarations plus Redux's reviewed offline dependency and ordering knowledge. It does not
replace the built-in checks.

The advisor can report:

- a declared dependency placed later than its dependant;
- a dependency cycle that no linear order can satisfy; and
- reviewed load-after guidance from the bundled database.

Known patch-style relationships that intentionally load later are handled separately to avoid false
warnings. Inactive packages and always-loaded overrides are excluded from numbered-order advice.
Exact UUID lookup identifies the database entry, not the strength of every placement suggestion.
Suggested separator groups can use community sections, provider categories, name patterns, or
inferred placement recorded in that entry. The preview labels this evidence separately from
declared dependencies and explicit load-after rules; a grouping suggestion is not a compatibility
guarantee.

### Organize Active Load Order

The organizer is user-invoked and preview-first. It offers three separator policies:

1. **Preserve my separators** keeps each separator and its membership together, sorting only inside
   those boundaries.
2. **Use suggested separators** replaces the current layout with non-empty named groups backed by
   offline knowledge.
3. **Remove separators** organizes the full active list as one numbered sequence.

The preview separates proposed mod moves, actual separator changes, and relationships that still
need review. Existing preserved separators are not counted as changes unless their marker really
moves. Applying the plan creates one undoable, unsaved edit and never writes to the game. Individual
recommendations can be ignored locally and restored later.

When guidance is disabled, its organizer action and status indicator are absent.

Normal separator editing is not part of the advisor. Direct and context-menu controls for
collapsing or expanding active separators—and their assignable shortcut—remain available whether
or not Load Order Advisor is enabled.

## First-run and preference behavior

The first-run setup is also reachable from Help. Theme, motion, and background effects preview live
and return to their previous values if setup is dismissed. API keys and persistent settings are
stored when setup is completed with **Start using Redux**; **Continue** advances between steps.
The interface-language choice is retained even if setup is deferred. Online source information, NXM association, Load Order
Advisor guidance, and retained package archives remain independent opt-in choices. Enabling NXM
links does not enable archive retention, and enabling provider information does not authorize a
download or installation. Setup does not install packages or sync the game. Explicitly requested
starter separators can add presentation data to the working Active order.

## Rules for new feature work

Optional extensions must remain:

- reversible through a clear preference;
- conservative when evidence is incomplete;
- read-only unless a separate explicit action authorizes a change;
- absent from the interface when disabled where practical;
- independent of package discovery and load-order persistence; and
- removable without changing inherited core behavior.
