# Redux Mod Database Tool

This developer utility builds and validates conservative additions to
`src/GUI/Resources/ReduxModDatabase.json`.

It deliberately separates database maintenance from Redux's runtime. The tool never contacts
Nexus Mods, stores credentials, guesses a project from a filename, or modifies the database unless
the `add` command receives an explicit `--write` flag.

## Commands

From the repository root:

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- validate
dotnet run --project tools/ReduxModDatabaseTool -- fingerprint --file "C:\Mods\Example.pak"
dotnet run --project tools/ReduxModDatabaseTool -- review-report `
  --file "C:\Reports\Redux-Mod-Database-Contribution.bg3redux-report" `
  --output "C:\Reports\Redux-Mod-Database-Review.json"
dotnet run --project tools/ReduxModDatabaseTool -- add `
  --file "C:\Mods\Example.pak" `
  --mod-id 123 `
  --file-id 456 `
  --name "Example Mod" `
  --author "Example Author" `
  --version "1.0"
```

The `add` command is preview-only by default. Review its proposed records, then repeat it with
`--write` to atomically update the database. Run `validate` again before committing the result.
Validation also checks the bundled community identity and load-order sections when present,
including UUID validity, uniqueness, project references, ordering-group references and cycles,
dependency aliases and substitutes, supported match policy, and recorded counts.

The `review-report` command validates the report's schema and privacy declaration, rejects absolute
or embedded path data, non-public provider URLs, invalid UUID fallbacks, and inconsistent
fingerprint states, then compares exact fingerprints with the current database. Its output
separates new project candidates, candidates for known projects, already-known packages, conflicts,
non-Nexus records, and packages whose fingerprints were unavailable. It never changes the database.

After independently confirming candidate Nexus projects and file records, preview one project or a
selected batch:

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- accept-report `
  --file "C:\Reports\Redux-Mod-Database-Contribution.bg3redux-report" `
  --mod-id 123

dotnet run --project tools/ReduxModDatabaseTool -- accept-report `
  --file "C:\Reports\Redux-Mod-Database-Contribution.bg3redux-report" `
  --mod-ids 123,456,789
```

This is also preview-only by default. It requires exact fingerprints and verified Nexus project IDs,
rejects fingerprint conflicts, and does not promote module UUIDs into reviewed identities. Nexus file
IDs are preserved when the report contains them; exact PAK records use `-1` when modern archive names
do not expose a file ID. A selected batch is validated and written as one atomic update. Repeat it
with `--write` only after reviewing the proposed records.

## Private desktop reviewer

The repository also contains a compact maintainer interface that wraps the same commands:

```powershell
dotnet run --project tools/ReduxModDatabaseTool.Desktop
```

It opens a contribution report, shows duplicate/conflict classifications, lets a maintainer select
independently verified Nexus projects, previews the exact batch, and exposes the write action only
after that preview succeeds. This utility is not part of Redux tester packages.

When launched from a repository checkout, the reviewer finds
`src/GUI/Resources/ReduxModDatabase.json` automatically. A portable copy can keep
`ReduxModDatabase.json` beside the executable or in a `Resources` subfolder. Paths can also be
provided explicitly:

```powershell
ReduxModDatabaseReviewer.exe `
  --report "C:\Reports\Contribution.bg3redux-report" `
  --database "C:\Redux\ReduxModDatabase.json"
```

Use `--help` for the full option list, including aliases, categories, picture URLs, and reviewed
module identities.
