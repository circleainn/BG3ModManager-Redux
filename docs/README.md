# Redux documentation

The main [project README](../README.md) introduces Redux and its major features. The guides below
cover installation, troubleshooting, privacy, mod-author tools, and project development in more
detail.

## Using Redux

| Guide | What it covers |
|:--|:--|
| [Current project state](CURRENT_STATE.md) | Current release, settled decisions, known limits, and planned work |
| [Installation and updates](INSTALLATION.md) | Install, update, move, roll back, or remove the portable app |
| [Frequently asked questions](FAQ.md) | Short answers about everyday Redux behavior |
| [Troubleshooting](TROUBLESHOOTING.md) | Startup, paths, downloads, NXM links, packages, and game sync |
| [Privacy and local data](PRIVACY_AND_DATA.md) | Stored data, network requests, credentials, logs, and safe sharing |
| [Support](SUPPORT.md) | Where to ask for help and what to include in a report |

## Understanding Redux

| Guide | What it covers |
|:--|:--|
| [Changes from BG3 Mod Manager](CHANGES_FROM_UPSTREAM.md) | What Redux keeps, changes, and adds |
| [Diagnostics and optional features](REDUX_OPTIONAL_MODULES.md) | Mod Diagnostics, online information, and Load Order Advisor boundaries |
| [Redux mod database](REDUX_MOD_DATABASE.md) | Offline recognition, advisor knowledge, and contribution reports |
| [Changelog](CHANGELOG.md) | Chronological release history |

## For mod authors

| Guide | What it covers |
|:--|:--|
| [Mod developer tools](MOD_DEVELOPER_TOOLS.md) | Inspect a PAK or release archive before publishing |
| [Creator manifest](REDUX_CREATOR_MANIFEST.md) | Add validated public source identity to a PAK |
| [Creator manifest schema](schemas/redux.mod.schema.json) | Machine-readable `redux.mod.json` format |

## For contributors and maintainers

| Guide | What it covers |
|:--|:--|
| [Contributing](CONTRIBUTING.md) | Build, test, documentation, and pull-request expectations |
| [Interface principles and workflow review](UI_DESIGN.md) | Shared UI rules, local findings, and end-to-end usability checks |
| [Architecture](ARCHITECTURE.md) | Major components, ownership boundaries, and safe write patterns |
| [Release process and update recovery](PUBLIC_ALPHA_RELEASES.md) | Versioning, packaging, publishing, verification, and recovery |
| [Security policy](SECURITY.md) | Supported versions, reporting, and security boundaries |
| [Third-party notices](../licenses/Third-Party-Notices.md) | Bundled dependencies, licenses, and attribution |
| [Database tools](../tools/ReduxModDatabaseTool/README.md) | Preview-first database maintenance tools |

Release notes in [`releases/`](releases/) are historical records for specific versions. They may
describe older distribution methods or limitations that no longer apply. Use the current guides
above for present-day instructions.

## Terms used in these guides

- **Mod Diagnostics** means the built-in, read-only checks that explain package and dependency
  conditions.
- **Load Order Advisor** means the optional guidance layer and its user-reviewed organizer.
- **Separator** means a Redux visual grouping marker. Separators never enter `modsettings.lsx`.
- **Sub-separator** means one nested visual grouping level inside a separator.
- **Redux Modlist** means the portable `.bg3redux` format. It does not contain PAKs or saves.
- **Contribution report** means a privacy-limited `.bg3redux-report` prepared for database review.
- **Game-directory mod** means a reviewed native or root-level package managed outside the normal
  PAK Mods folder.
- **Download Manager** means the shared local-package and optional NXM acquisition inbox.
- **Package Archive Library** means the separate, opt-in store of verified installation packages.

> [!IMPORTANT]
> Review logs, screenshots, and reports before sharing them. Never post API keys, temporary download
> links, private paths, saves, or somebody else's mod files.
