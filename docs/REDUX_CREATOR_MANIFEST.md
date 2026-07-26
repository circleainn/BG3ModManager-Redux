# Redux creator manifest

`redux.mod.json` is an optional identity link for mod authors who want Redux to connect an installed
PAK directly to its Nexus Mods project. It is declarative metadata, not an installer or executable
format.

The canonical placement is:

- `redux.mod.json` at the virtual root of every distributed PAK.

Embedding the manifest keeps the metadata attached to the installed package after its original ZIP,
7z, or RAR archive has been extracted or discarded. A multi-PAK release should embed a manifest in
each PAK, and each manifest should describe only the modules contained by that PAK.

An archive may also contain a root-level copy as an import-time convenience, but that copy is
non-authoritative and does not replace the embedded manifest. If archive and PAK metadata conflict,
Redux should reject the archive claim and validate the embedded PAK metadata independently.

Redux discovers a root-level manifest while it is already scanning the PAK, then validates the
claim against the package's parsed `meta.lsx` module metadata. Discovery is read-only and does not
perform a second package scan. A valid manifest is retained as verified runtime metadata. An
invalid manifest is ignored and appears as a non-destructive Mod Health finding.

Validated source claims participate in Redux's normal provider-resolution pipeline. The embedded
file only needs to establish the stable project identity; cached or live Nexus data supplies the
name, author, description, images, version history, and other user-facing metadata when available.
Explicit manual links and manual unlinks take precedence, and source claims never alter load-order
state. Cached creator-supplied associations are rechecked against the currently installed PAK and
discarded if its manifest is removed, becomes invalid, or claims a different project.

## Trust model

A manifest is a claim supplied by the package author. Redux must validate it before use:

- module UUIDs, folders, names, and versions must agree with metadata parsed from the PAK;
- source IDs may establish a provider association, but must not replace an explicit manual link or
  manual unlink;
- dependencies remain informational unless the referenced module UUID is present and valid;
- unknown properties or future schema versions must fail closed;
- manifests cannot contain commands, absolute paths, credentials, executable hooks, deletion
  instructions, or changes to `modsettings.lsx`;
- a manifest cannot directly move mods or modify a user's load order.

Invalid or conflicting manifest claims are ignored and surface a non-destructive diagnostic.
Explicit user source choices remain unchanged.

## Recommended Nexus manifest

For most Nexus Mods releases, this compact form is all that is needed:

```json
{
  "$schema": "https://raw.githubusercontent.com/raincloudsfollow/BG3ModManager-Redux/main/docs/schemas/redux.mod.schema.json",
  "schemaVersion": 1,
  "manifestType": "bg3-redux-mod",
  "moduleUuid": "11111111-2222-3333-4444-555555555555",
  "nexus": {
    "projectId": 12345
  }
}
```

`moduleUuid` must match the primary module UUID in the PAK's `meta.lsx`. `projectId` is the number
at the end of the Nexus Mods page URL, such as `12345` in
`https://www.nexusmods.com/baldursgate3/mods/12345`. Redux uses that identity to construct the page
link and retrieve current Nexus metadata through its normal source-integration pipeline.

`fileId` may be added inside `nexus` when a release author wants to identify a specific Nexus file,
but it is not required for the project connection.

## Detailed manifest

The original detailed form remains supported for compatibility and for authors who intentionally
want to include offline fallback metadata or informational dependency claims:

```json
{
  "$schema": "https://raw.githubusercontent.com/raincloudsfollow/BG3ModManager-Redux/main/docs/schemas/redux.mod.schema.json",
  "schemaVersion": 1,
  "manifestType": "bg3-redux-mod",
  "mod": {
    "name": "Example Mod",
    "version": "1.2.0",
    "authors": ["Example Author"],
    "homepage": "https://example.invalid/mod",
    "sources": [
      {
        "service": "nexus",
        "projectId": 12345,
        "fileId": 67890
      }
    ],
    "modules": [
      {
        "uuid": "11111111-2222-3333-4444-555555555555",
        "name": "Example Mod",
        "folder": "ExampleMod",
        "version": "1.2.0",
        "pak": "ExampleMod.pak"
      }
    ],
    "dependencies": [
      {
        "uuid": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        "name": "Example Library",
        "minimumVersion": "2.0.0",
        "optional": false
      }
    ]
  }
}
```

The authoritative machine-readable definition is
[`docs/schemas/redux.mod.schema.json`](schemas/redux.mod.schema.json).
