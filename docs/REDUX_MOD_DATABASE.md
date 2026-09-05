# Redux mod database

`src/GUI/Resources/ReduxModDatabase.json` is a bundled offline database that lets Redux identify
some pre-existing Nexus Mods installs without an API request. It's not an importer, and it never
matches a package from a filename alone, title alone, arbitrary UUID, or approximate version. It uses
exact fingerprints first, followed by identities with enough corroborating evidence to avoid silently
relabeling unrelated local packages.

Source-association records are loaded and queried through `ReduxModDatabaseService`
(`src/Core/AppServices/ReduxModDatabaseService.cs`). The optional Load Order Advisor lazily loads
the separate ordering section during its existing background analysis pass. It remains read-only
and never moves a package.

## Structure

- `schemaVersion` — must be `1`; anything else is treated as empty.
- `projects` — one entry per Nexus project (`modId`, `name`, `authors`, `aliases`, `pictureUrl`).
- `exactPakFingerprints` — one entry per exact installed `.pak` (`hash`, `size`, `modId`, `fileId`,
  plus fallback `name`/`author`/`version`/`pictureUrl` if the project record is incomplete).
  `hash` is xxHash64 over the full `.pak` byte stream, Base64-encoded from the little-endian 64-bit
  value.
- `exactArchiveFingerprints` — one entry per exact downloaded archive (`md5`, `size`, `modId`,
  `fileId`, `logicalFileName`, plus the same fallback fields). `md5` is lowercase hex over the full
  archive.
- `moduleIdentities` — reviewed UUID → `modId` links for mods whose module UUID reliably identifies
  a single Nexus project, used when no exact fingerprint is available.
- `communityModuleIdentities` — exact UUID → Nexus project candidates from the expanded offline
  dataset. At runtime these are accepted only when the installed package name, folder, or filename
  also agrees with the recorded identity. Conflicting authors reject the match, and community-only
  projects are excluded from the broader name-and-author fallback.
- `loadOrderEntries` — UUID-keyed ordering knowledge kept independently of source linking: names,
  groups, dividers, dependencies, explicit load-after rules, Script Extender requirements, and
  evidence counts. The advisor uses exact installed UUIDs and names from these records to supplement
  package metadata that may be incomplete.
- `orderingGroups` — the named ordering groups and their `after` relationships. These are retained
  as placement guidance; they are not treated as dependency requirements.
- `dependencyNameAliases` — exact normalized requirement names that identify a differently named
  module UUID. Approximate matching is deliberately excluded.
- `dependencySubstitutes` — explicitly reviewed module UUIDs that can satisfy a differently keyed
  requirement.

When enabled, the Load Order Advisor combines installed package declarations with the bundled
records. It reports reversed dependency placement, exact dependency cycles, and explicit
mod-author load-after relationships. Known patch-style dependencies that intentionally load after
their dependants do not produce a false placement warning. Category evidence does not currently
reorder the list or generate blanket warnings.

## Match order

1. Exact `.pak` fingerprint (size + hash) — strongest.
2. Exact archive fingerprint (size + md5).
3. Reviewed module UUID identity.
4. Community UUID identity corroborated by an exact normalized installed name, folder, or filename;
   author metadata must also agree when both sides provide it.
5. Normalized name + author agreement across every alias a project has, only when exactly one
   project matches. Name-only candidates are ignored.

Anything that doesn't clear one of these stays **Local**.

## Adding an exact `.pak` or archive fingerprint

The repository includes a validation-first developer utility at
`tools/ReduxModDatabaseTool`. It computes the same hashes Redux uses, validates the full database,
and previews additions without writing by default:

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- validate
dotnet run --project tools/ReduxModDatabaseTool -- fingerprint --file "C:\Mods\Example.pak"
```

See `tools/ReduxModDatabaseTool/README.md` for the guarded `add` workflow.

Private tester builds can generate a privacy-limited `.bg3redux-report` from
**Tools > Generate Redux Database Contribution...**. Reports omit absolute paths, load-order positions,
profile names, application settings, and credentials. Maintainers can audit and classify a report
without changing the database:

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- review-report `
  --file "Redux-Mod-Database-Contribution.bg3redux-report" `
  --output "Redux-Mod-Database-Review.json"
```

Contribution reports are evidence for review, not an automatic trust source. Conflicts must be
resolved manually. Confirmed Nexus projects can be previewed with `accept-report --mod-id <id>` or
as one selected batch with `accept-report --mod-ids <id,id,...>`. Exact PAK evidence requires a
verified Nexus project ID; the Nexus file ID is preserved when available and recorded as `-1` when
modern archive names do not expose it. The batch is written atomically only with an additional
`--write`; individual local artifacts still use the preview-first `add` workflow. The private desktop
reviewer in `tools/ReduxModDatabaseTool.Desktop` exposes the same
guarded sequence without requiring command-line entry and is not included in Redux tester packages.
It finds the repository database automatically when run from a checkout. Portable maintainer copies
can keep `ReduxModDatabase.json` beside the reviewer executable or in a `Resources` subfolder, and
can receive report and database paths through `--report` and `--database`.

The exporter removes embedded path-shaped metadata and strips credentials, query strings, and
fragments from provider URLs before writing. It validates the privacy contract both before and
after the temporary report is serialized. The maintainer utility independently repeats those
checks and rejects older or altered reports that contain invalid UUID fallbacks, embedded paths,
non-public provider URLs, or inconsistent fingerprint states.

Reports produced before these privacy checks were introduced should be regenerated rather than
shared.

1. Confirm the Nexus mod ID and file ID from the actual file page.
2. Get the exact file (installed `.pak`, or the downloaded archive) that produced it.
3. Record its byte length and hash (xxHash64/Base64 for a `.pak`, MD5/hex for an archive).
4. Add the entry to `exactPakFingerprints` or `exactArchiveFingerprints`, and add or update the
   matching `projects` entry if it doesn't exist yet.
5. Update the `counts` block.
6. Confirm the JSON parses and that no identical size+hash pair points at more than one project.
7. Test against a clean Redux debug settings file, with and without a Nexus API key.

Don't add filename-only, title-only, uncorroborated UUID, or approximate-version matches — those can
misattribute a local or repackaged mod to the wrong Nexus project. Community identity candidates must
remain separate from reviewed identities so the runtime corroboration requirement cannot be bypassed.
