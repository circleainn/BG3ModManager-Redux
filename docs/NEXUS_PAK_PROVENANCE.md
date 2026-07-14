# Bundled Nexus `.pak` provenance database

Redux includes a small offline fallback that can identify some pre-existing Nexus Mods installations without an API request. It is not an importer and does not infer a source from a filename, mod title, UUID, or approximate version.

## Match semantics

Each record in `src/GUI/Resources/NexusPakProvenance.json` describes one exact installed `.pak` file:

- `size` is the exact file length in bytes.
- `hash` is xxHash64 over the complete installed `.pak` byte stream, encoded as Base64 from the little-endian 64-bit value.
- `modId` and `fileId` identify the corresponding Nexus Mods project and downloadable file.
- `name`, `author`, `version`, and `pictureUrl` provide bundled fallback display data.

Redux first uses file size to narrow the candidates, then hashes the installed `.pak`. Both the size and hash must match. A different build or version remains **Local** until it is identified by another trusted source.

## Adding an exact `.pak` version

1. Confirm the Nexus project ID and file ID from the real Nexus Mods file page.
2. Obtain the exact installed `.pak` produced by that download.
3. Record its byte length and calculate the same xxHash64/Base64 value described above.
4. Add one entry to `entries`, keeping named entries ordered alphabetically by mod name. Records without a name belong at the end.
5. Update `entryCount`.
6. Validate that the JSON parses and that an identical `size` + `hash` pair does not point to more than one Nexus file.
7. Test the exact `.pak` with a clean Redux Debug settings file, both without and with a Nexus API key.

Do not add filename-only, title-only, UUID-only, or approximate version matches. Those can incorrectly attribute local or repackaged mods to an unrelated Nexus project.
