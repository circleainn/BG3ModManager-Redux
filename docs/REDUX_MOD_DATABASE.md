# Redux mod database

`src/GUI/Resources/ReduxModDatabase.json` is a bundled offline database that lets Redux identify
some pre-existing Nexus Mods installs without an API request. It's not an importer, and it never
matches a package from a filename alone, title alone, arbitrary UUID, or approximate version. It uses
exact fingerprints first, followed only by reviewed identity records with unambiguous evidence.

Loaded and queried through `ReduxModDatabaseService` (`src/Core/AppServices/ReduxModDatabaseService.cs`).

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

## Match order

1. Exact `.pak` fingerprint (size + hash) — strongest.
2. Exact archive fingerprint (size + md5).
3. Reviewed module UUID identity.
4. Normalized name + author agreement across every alias a project has, only when exactly one
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
**Tools > Export Redux Database Contribution**. Reports omit absolute paths, load-order positions,
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

Don't add filename-only, title-only, UUID-only, or approximate-version matches — those can
misattribute a local or repackaged mod to the wrong Nexus project.
