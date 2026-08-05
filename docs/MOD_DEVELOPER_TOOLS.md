# Mod developer tools

Redux includes small, focused tools for inspecting mod packages without installing or changing
them.

## Package preflight

Open **Tools > Inspect Mod Package** and select a `.pak`, ZIP, 7z, RAR, TAR, or GZip archive to
review:

- parsed module name, folder, UUID, author, and version;
- declared dependencies compared with the current Redux library;
- embedded `redux.mod.json` creator metadata;
- Script Extender configuration, Osiris scripting, Mod Fixer files, and always-loaded overrides;
- common development files that may have been included accidentally; and
- an existing installed package with the same module UUID.

For archives, Redux inspects every contained PAK and also checks the release container for duplicate
PAK filenames, unsafe paths, development debris, and an accidentally bundled `modsettings.lsx`.

The preflight is read-only. It does not install, extract, edit, register, sort, or export the
selected package. A clean result means Redux did not detect a blocking packaging or metadata issue;
it does not guarantee compatibility or correct in-game behavior.

## Creator metadata

Mod authors can optionally include a root-level [`redux.mod.json`](REDUX_CREATOR_MANIFEST.md) in a
PAK to provide a stable link to its Nexus Mods or mod.io page. Redux validates the manifest's module
claim against the package's parsed metadata before using it.
