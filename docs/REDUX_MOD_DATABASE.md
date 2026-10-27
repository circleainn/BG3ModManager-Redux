# Redux mod database

Redux ships an offline knowledge file at
`src/GUI/Resources/ReduxModDatabase.json`. It serves two related purposes:

1. conservatively recognize some installed Nexus Mods packages without a live API request; and
2. provide UUID-keyed dependency and placement rules, plus grouping evidence, to the optional
   Load Order Advisor.

These evidence sets share one validated database but remain logically separate. Recognizing a
source never changes a load order, and ordering knowledge never invents a provider identity.

> [!NOTE]
> Users do not need to edit JSON. To help improve coverage, use
> **Tools > Generate Redux Database Contribution...** and submit the resulting
> `.bg3redux-report` for review.

## Recognition is deliberately conservative

Redux prefers **Local** over a confident-looking mistake. A filename, display title, arbitrary
UUID, or approximate version is never enough by itself.

The source match order is:

1. exact installed PAK fingerprint: byte length plus xxHash64;
2. exact downloaded-archive fingerprint: byte length plus MD5;
3. a reviewed module UUID identity;
4. a community UUID candidate corroborated by an exact normalized package name, folder, or
   filename, with author agreement when both sides provide authors; then
5. one unambiguous normalized project-name/alias and author match.

Conflicting or ambiguous evidence produces no provider assignment. Manual links and manual unlinks
always outrank automatic database matches.

Redux keeps evidence collections in one database rather than treating Nexus and mod.io as competing
databases. Provider provenance is attached to a resolved association, which prevents a package from
being relabelled merely because another ecosystem uses a similar name. Exact artifact fingerprints
remain Nexus-focused. Provider-exclusive catalog matches can supply community candidates for
either Nexus or mod.io, but only with UUID and exact local-name corroboration at runtime.

## Database map

| Section | Purpose |
|:--|:--|
| `schemaVersion` | Supported database contract. Unknown versions fail closed. |
| `projects` | Reviewed Nexus project name, authors, aliases, category, and image metadata. |
| `modioProjects` | Provider-exclusive mod.io listing metadata. |
| `exactPakFingerprints` | Exact installed PAK size and xxHash64 mapped to a project and optional file ID. |
| `exactArchiveFingerprints` | Exact archive size and MD5 mapped to a project, file ID, and logical filename. |
| `moduleIdentities` | Reviewed UUID-to-project identities that are safe without extra name corroboration. |
| `communityModuleIdentities` | Broader UUID candidates that require exact package-name, folder, or filename corroboration. |
| `communityModioIdentities` | Provider-exclusive mod.io UUID candidates with the same runtime corroboration requirement. |
| `loadOrderEntries` | UUID-keyed names, groups, dependency facts, load-after rules, requirements, and evidence counts. |
| `orderingGroups` | Named placement groups and their explicit `after` relationships. |
| `dependencyNameAliases` | Exact normalized dependency names mapped to differently named module UUIDs. |
| `dependencySubstitutes` | Reviewed module UUIDs allowed to satisfy a differently keyed requirement. |
| `counts` | Integrity totals checked by the maintenance tooling. |

Installed PAK hashes use xxHash64 over the complete byte stream, encoded as Base64 from the
little-endian 64-bit value. Archive hashes use lowercase hexadecimal MD5 over the complete archive.
The hashes identify exact artifacts; they are not security signatures.

## How the Load Order Advisor uses it

When the optional advisor is enabled, Redux combines exact installed package declarations with the
offline ordering sections. It can explain reversed dependency placement, exact dependency cycles,
reviewed load-after relationships, and known substitutes. Patch-style exceptions that intentionally
load later are represented explicitly rather than guessed from names.

Grouping evidence has mixed confidence: explicit records, provider categories, existing sections,
and name-based inference can suggest placement. A UUID identifies a record; it does not prove that
every rule or grouping is independently verified. The preview labels its evidence, and mod-author
instructions take priority.

The organizer uses these rules and suggestions to create a deterministic preview:

- **Preserve my separators** sorts only within existing membership boundaries;
- **Use suggested separators** creates non-empty named groups; and
- **Remove separators** sorts the active list as one numbered sequence.

Unknown mods retain their relative order. The preview reports mod moves, actual separator changes,
and relationships blocked by the selected policy. Unchanged preserved separators do not inflate the
change count. Applying a plan is one undoable, unsaved edit and never writes to the game.

## Generate a contribution report

**Tools > Generate Redux Database Contribution...** creates a privacy-limited report from installed
user mods. It includes sanitized module identity, exact PAK fingerprints, and provider IDs Redux
already knows—including Nexus project and file IDs when available.

It excludes:

- PAK or archive contents;
- absolute filesystem paths;
- profiles and load-order positions;
- categories, separators, private notes, and application settings; and
- API keys, credentials, URL query strings, and fragments.

Generation is read-only. A report is review evidence, not an automatic database import. Review the
file before sharing it and submit the report itself—not copyrighted mod archives or PAKs.

Reports created before the current privacy checks should be regenerated.

## Maintainer review workflow

The repository includes a preview-first CLI and a private desktop reviewer. From the repository
root:

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- validate
dotnet run --project tools/ReduxModDatabaseTool -- review-report `
  --file "C:\Reports\Contribution.bg3redux-report" `
  --output "C:\Reports\Contribution.review.json"
dotnet run --project tools/ReduxModDatabaseTool -- accept-report `
  --file "C:\Reports\Contribution.bg3redux-report" `
  --mod-ids 123,456
```

`review-report` repeats the privacy audit, validates the report contract, and classifies records as
new, known, conflicting, non-Nexus, or unavailable. `accept-report` requires independently verified
Nexus project IDs and exact fingerprints. Both are non-writing until `--write` is supplied; a
selected batch is validated and replaced atomically.

Exact PAK evidence requires a verified Nexus project ID. Redux preserves a known Nexus file ID and
records `-1` when a modern archive name does not expose one. Report acceptance does not promote a
community UUID into the reviewed `moduleIdentities` collection.

## Database maintenance

The bundled Advisor catalog contains 10,626 UUID-backed entries. Category suggestions carry
concise evidence labels; they do not imply compatibility. Unresolved name-only records are excluded,
and proposed dependency or ordering changes require review before inclusion.

Coverage uses distinct units that overlap; do not add them together as a number of supported mods.
These values describe the bundled JSON, not a compatibility test matrix:

| Coverage unit | Count |
|:--|--:|
| Nexus project records | 3,695 |
| mod.io project records | 1,761 |
| Advisor entries with dependencies | 630 |
| Advisor entries with explicit load-after rules | 11 |

An entry with dependencies or a load-after rule can appear in both rows. Most Advisor entries
provide grouping evidence rather than explicit ordering relationships. Run
`python tools/maintenance/check_project_docs.py` when changing the corpus or these counts.

### Legacy provenance and new contributions

The legacy catalog contains curated records, community section evidence, provider categories,
name patterns, inferred placements, author-catalogue evidence, and entries with no verified
placement. Some retained placement explanations explicitly credit VOLO. Those legacy records do
not carry pinned upstream commits or an individually reproducible source trail, so their presence
must not be described as independent verification of every relationship. Redux does not ship or
invoke the VOLO sorter. Removing the proposed maintainer importer does not remove this pre-existing
data, and it does not change essential package libraries such as LSLib.

Older public counts used less precise terminology. The current figures above count the actual
bundled records; they do not establish the size or review quality of an earlier source collection.
Do not repeat a broad historical coverage number as a current guarantee.

The public database utility supports validation and independently reviewed contribution and
fingerprint updates. For a new or changed relationship, retain the mod-author source URL, exact
package/version or source revision where available, the selected UUIDs, and the review reasoning
in the contribution or pull request. Preserve uncertainty where original evidence is unavailable;
do not manufacture provenance for legacy entries.

For one local artifact, use the guarded fingerprint/add workflow:

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- fingerprint --file "C:\Mods\Example.pak"
dotnet run --project tools/ReduxModDatabaseTool -- add `
  --file "C:\Mods\Example.pak" `
  --mod-id 123 --file-id 456 `
  --name "Example Mod" --author "Example Author" --version "1.0"
```

Review the preview, repeat with `--write`, then run `validate` again. See the
[tool reference](../tools/ReduxModDatabaseTool/README.md) for every option and the desktop reviewer.

## Review checklist

1. Confirm the provider project and file against the real release page.
2. Confirm the exact artifact that produced the fingerprint.
3. Resolve every existing fingerprint or identity conflict; never overwrite evidence to silence it.
4. Keep reviewed identities separate from community candidates.
5. Update through the tool so counts, references, cycles, and collisions are validated.
6. Test recognition with online information both enabled and disabled.
7. Confirm an unrelated package with a similar name remains **Local**.

Never add filename-only, title-only, fuzzy-version, or uncorroborated community UUID matches.
