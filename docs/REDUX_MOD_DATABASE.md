# Redux mod database

`src/GUI/Resources/ReduxModDatabase.json` is a bundled offline database that lets Redux identify
some pre-existing Nexus Mods installs without an API request. It's not an importer, and it never
infers a source from a filename, title, UUID, or approximate version — only from exact fingerprints
or reviewed identity records.

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
