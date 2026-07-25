# Redux creator manifest

`redux.mod.json` is a proposed, optional metadata file for mod authors who want Redux to identify
their releases without relying on filenames or a network lookup. It is declarative metadata, not an
installer or executable format.

The initial placement convention is:

- at the root of a distributed ZIP, 7z, or RAR archive; or
- at the virtual root of a standalone PAK.

Archive-root manifests are the preferred form because they can describe every PAK in a multi-file
release. Runtime manifest discovery is planned work; this document and its JSON Schema establish
the format before Redux begins consuming it.

## Trust model

A manifest is a claim supplied by the package author. Redux must validate it before use:

- module UUIDs, folders, names, and versions must agree with metadata parsed from the PAK;
- source IDs may create a review candidate, but must not silently replace an existing conflicting
  source link;
- dependencies remain informational unless the referenced module UUID is present and valid;
- unknown properties or future schema versions must fail closed;
- manifests cannot contain commands, absolute paths, credentials, executable hooks, deletion
  instructions, or changes to `modsettings.lsx`;
- a manifest cannot directly move mods or modify a user's load order.

Invalid or conflicting manifests should leave the mod Local and surface a non-destructive diagnostic.

## Example

```json
{
  "$schema": "./redux.mod.schema.json",
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
