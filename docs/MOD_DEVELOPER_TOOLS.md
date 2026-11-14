# Mod developer tools

Redux includes a read-only release preflight for mod authors and maintainers. It helps catch common
packaging and metadata problems before a file is published, without installing or changing the
selected artifact.

## Inspect a package

Open **Tools > Inspect Mod Package...** and choose a PAK, loose BG3 save, or supported ZIP, 7z,
RAR, TAR, or GZip-family archive.

Redux reports:

- parsed module name, folder, UUID, author, and version;
- declared dependencies and whether matching packages are installed;
- Script Extender configuration, Osiris scripting, Mod Fixer files, and always-loaded overrides;
- a validated or rejected embedded [`redux.mod.json`](REDUX_CREATOR_MANIFEST.md);
- development files that may have been included accidentally; and
- installed packages that use the same module UUID.

For a release archive, Redux inspects every contained PAK and also checks the outer archive for
unsafe paths, duplicate PAK filenames, development debris, and an accidentally bundled
`modsettings.lsx`.

The same read-only preflight recognizes reviewed native and game-directory layouts using the
guarded install catalog. It reports DLL and configuration files, expected destinations, loader
requirements, source identity, and companion PAKs. Unknown DLL layouts are described without
guessing an identity or destination.

Save archives and loose `.lsv` files reuse Save Game Manager's validation and metadata reader.
Recognized saves show campaign, difficulty, timestamp, and contents, together with a clear route to
Save Game Manager rather than the normal mod installer.

## What this mod changes

Select one installed PAK mod and open **Tools > What this mod changes...**, the same action in
**Quick Access**, its right-click menu, or its details **Files** tab. The inspector reads the
selected package on demand without extraction or modification.

The **Stats records** tab shows supported declarations from `Public/.../Stats/Generated/Data/*.txt`.
Select a record to see its fields, parent, source path and line, and whether each value was declared
there or inherited from a parent in the same package. Search by record name, type, parent or path.
**Package files** inventories other content, including textures, scripts and localization;
**Inspection notes** explains malformed, skipped or unresolved content.

LSLibStats parses the declarations. The package-local inheritance resolver adapts BG3 Forge's
iterative resolver and retains provenance. An external parent, uncertain definition order or cycle
is reported as incomplete. This view does not compare with the base game, interpret every file
format, run scripts, or determine compatibility and winning values across the active load order.

Inspection supports non-solid PAK versions 15, 16 and 18. It limits Stats reads to 2 MiB per file,
16 MiB total, 512 Stats files and 10,000 records, with separate field and file-table limits.
Unsupported or oversized content produces a partial result instead of an unrestricted read.
Cancel stops the inspection and leaves the package untouched.

## Understand the result

| Result | Meaning |
|:--|:--|
| Blocking issue | The package or archive has a structural or identity problem that should be fixed before release. |
| Review recommended | Redux found something intentional in some mods but worth confirming. |
| No blocking issue | The inspected checks passed. This is not a compatibility guarantee. |

The preflight uses an isolated temporary staging directory when it must read contained PAK or save
metadata. It never installs into the Mods, game, or profile folders; edits, registers, sorts,
activates, or exports the selected package; runs native code; or proves in-game compatibility.

## Add stable source identity

Authors may place a root-level `redux.mod.json` inside a PAK. Redux validates the claim against the
PAK's parsed `meta.lsx` before using it to associate the installed file with Nexus Mods or mod.io.
The compact Nexus form needs only a module UUID and Nexus project ID; the detailed form supports
multiple modules, mod.io, fallback metadata, and informational dependencies.

See the [creator manifest guide](REDUX_CREATOR_MANIFEST.md) for examples, placement rules, and the
trust model. The [machine-readable schema](schemas/redux.mod.schema.json) is authoritative.

## Before publishing

1. Inspect the exact PAK or archive you plan to upload.
2. Confirm every UUID, dependency, and provider ID against the release itself.
3. Remove development debris and user-specific files.
4. Rebuild, then inspect the final artifact again.
5. Test the release in a clean game profile; preflight cannot replace in-game testing.
