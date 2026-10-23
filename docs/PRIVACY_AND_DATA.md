# Privacy and local data

Redux is a local desktop mod manager. The current project does not include analytics, advertising,
or general-purpose telemetry. Optional online features make requests only for the provider and
update workflows the user enables or invokes.

Public-alpha status does not change this boundary. Redux does not require a Redux account and does
not upload a user's mod list, saves, categories, notes, settings, or diagnostics to a Redux service.

This document describes the intended product boundary. Users should still review logs and exports
before posting them publicly.

## Local state

Depending on enabled features and normal use, the Redux folder may contain:

- preferences (including per-file Nexus update skip choices), keybindings, window state, categories,
  themes, custom asset references, and saved load orders;
- provider metadata caches and public source associations;
- Download Manager queue/history state and partial or completed managed downloads;
- the optional Package Archive Library and its content-addressed index;
- load-order restore points, backups, and temporary staging data; and
- diagnostic and startup logs.

Application updating may temporarily place a verified release archive, extracted release files,
transaction backups, and a small completion result under the current Windows user's local app-data
folder. Successful transactions remove the large staging and backup content. This data contains
Redux application files, not mods, saves, profiles, provider credentials, or signed download URLs.

Redux also reads user-selected BG3 locations such as Mods, profiles, saves, the game directory, and
Script Extender configuration. It changes those locations only through an explicit workflow such as
install, delete, restore, save import, or game sync.

## Credentials and provider requests

Nexus Mods and mod.io integration are optional. When enabled, Redux may send the identifiers and
requests needed to retrieve public mod metadata, images, update information, or an explicitly
requested download. Provider behavior remains subject to that provider's own terms and privacy
policy.

Provider credentials are masked and stored separately from ordinary settings using protection tied
to the current Windows account. Temporary NXM authorization values and signed download URLs are
memory-only by design. They must not be written to settings, queue files, logs, reports, or portable
Redux Modlists.

## Portable and diagnostic files

A `.bg3redux` Modlist may contain a saved order and user-selected portable presentation data. It
does not contain PAKs, saves, profiles, API keys, or `modsettings.lsx`. Private notes are included
only when the exporting user explicitly chooses them.

A `.bg3redux-report` contribution file is designed to contain sanitized package identity and
review evidence without paths, credentials, profiles, order positions, categories, notes, settings,
or package content. Reports are never imported into the bundled database automatically.

Logs and screenshots are not guaranteed to be anonymous. They may contain filenames, mod names,
local paths, usernames embedded in paths, provider responses, or error details. Review and redact
them before sharing.

## Retained packages and history

Archive retention is a separate opt-in setting. Its index may store a verified package hash, safe
filename, public provider/project identity, package type, detected destination, version, and install
time. It must not store local source paths, credentials, signed URLs, or remote thumbnail URLs.

Installed history, the Package Archive Library, managed inbox files, and installed content have
independent removal behavior. Clearing one must not silently remove another.

## Network destinations

Normal optional network activity can include:

- Nexus Mods or mod.io for enabled source information and explicit downloads;
- the official Redux GitHub update channel and release assets for enabled application update
  checks and user-approved downloads; and
- Norbyte's official Script Extender release/update services for Script Extender workflows.

Core local package discovery, load-order editing, Mod Diagnostics, saved orders, and save browsing
must remain usable without optional provider enrichment.

## Safe sharing

Never publish API keys, credentials, signed URLs, raw save data, private notes, or unreviewed logs.
Use the smallest relevant excerpt and prefer Redux's privacy-limited report formats where they fit
the support request.
