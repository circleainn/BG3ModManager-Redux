# Frequently asked questions

## What is Redux?

Baldur's Gate 3 Mod Manager Redux is a Windows-only community fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). It preserves
the upstream package, profile, and load-order foundation while adding a cohesive interface,
categories and separators, deliberate review workflows, managers for downloads and saves, guarded
support for reviewed game-directory mods, custom themes, and accessibility options.

## Is Redux official?

No. Redux is not affiliated with or endorsed by Larian Studios, Nexus Mods, or mod.io. Baldur's Gate
3 is developed and published by Larian Studios. Redux retains and credits substantial work from the
upstream BG3 Mod Manager project.

## Is alpha.16.4.3 stable?

`0.1.0-alpha.16.4.3` is a public alpha, not a final stable release. Its core workflows have
automated and private testing, but public use will expose more combinations of Windows versions,
display scaling, game paths, tools, and mod sets. Keep independent backups and report reproducible
problems.

## Where should I download Redux?

Use only the [official Nexus Mods page](https://www.nexusmods.com/baldursgate3/mods/23799) or
[GitHub Releases](https://github.com/circleainn/BG3ModManager-Redux/releases). Avoid mirrors,
repacked archives, and executables supplied through private messages.

## How do I install the portable ZIP?

Install the .NET 8 Desktop Runtime, download the complete Redux ZIP, and extract every file into a
dedicated writable folder such as `C:\Modding\Redux`. Run `Redux.exe` from that folder. Public Redux
releases are portable-only; they do not create an Installed Apps entry or system-wide installation.

## Why is the application called `Redux.exe`?

Alpha.15 adopted the short product name for the desktop runtime. The product and window titles use
**BG3 Mod Manager Redux**, while the application file is `Redux.exe`. The old
`BG3ModManager.exe` name belongs to private-alpha builds. Current portable releases do not create
an Installed Apps entry; one may remain only from a retired Setup build.

## Can Redux run beside upstream BG3 Mod Manager?

Separate application folders are possible, but both managers can target the same BG3 Mods folder,
profiles, and `modsettings.lsx`. Do not run competing file operations at the same time. Confirm the
selected profile and review the game sync before writing from either manager. Keep backups when
moving between tools.

## Does installing a mod activate it?

No. Ordinary PAK installs go to Inactive Mods. Downloading, retaining, reinstalling, inspecting, or
importing a Redux Modlist does not automatically activate, reorder, save, or sync a load order.

## Are saving and syncing the same thing?

No. **Save** updates the selected Redux saved order. **Sync Load Order to Game** separately previews
and writes the intended changes to BG3's `modsettings.lsx` after confirmation.

## What are categories and separators?

Categories are Redux presentation labels used for color, icons, organization, and filtering.
Separators are named visual sections with durable membership and collapse state. Neither is written
to `modsettings.lsx`, and neither is a mod.

## Does the Load Order Advisor fix my load order automatically?

No. Mod Diagnostics is built-in and read-only. Load Order Advisor is a separate optional,
experimental feature based on exact declarations and reviewed offline knowledge. Its organizer
creates a preview and applies only a user-approved, undoable, unsaved edit. It cannot guarantee mod
compatibility.

## Why does a known mod appear as Local?

Redux chooses **Local** when it lacks strong enough evidence for a provider identity. It does not
guess from a similar filename or title. You can link a source manually, use enabled provider
information, or contribute a privacy-limited database report for maintainer review.

## Does Redux require internet access?

Core local package discovery, load-order editing, Mod Diagnostics, saved orders, and save browsing
remain usable without optional provider enrichment. Nexus Mods, mod.io, NXM downloads, application
updates and Script Extender retrieval require their relevant network services.

## Does Redux collect telemetry?

Redux does not include analytics, advertising, or general-purpose telemetry. It does not require a
Redux account. Optional provider and update features make only their documented requests. Read
[Privacy and local data](PRIVACY_AND_DATA.md) for storage and network boundaries.

## How are Nexus API keys stored?

Provider credentials are masked and stored outside ordinary settings using protection tied to the
current Windows account. Temporary NXM authorization and signed download URLs are memory-only by
design and must not be written to settings, logs, reports, or archive indexes.

## What does the Package Archive Library do?

It is an optional, quota-limited store of verified packages retained after successful installation.
It is separate from the inbox, installed history, and installed content. Clearing the library does
not uninstall anything, and clearing installed history does not clear the library.

## Can Redux manage Script Extender and native DLL mods?

Game-Directory Mod Manager supports a deliberately narrow catalog of reviewed layouts, including
Script Extender. Redux stages and rechecks those files and records what it owns. Unknown or modified
DLL layouts remain unverified and unmanaged. Layout recognition is not a publisher signature or a
malware scan; use sources you trust.

## Can Redux remove externally installed native mods?

Reviewed add-only DLLs may be adopted only when their exact installed files can be recognized.
External replacers cannot be adopted because Redux did not preserve the original game files. Follow
the manager's recovery guidance rather than manually deleting Redux-owned files.

## What is a Redux Modlist?

A `.bg3redux` Modlist can carry a saved order and selected categories, separators, custom icons,
public source links, and optional private notes. It does not contain PAKs, saves, profiles, API keys,
or `modsettings.lsx`, and importing one does not install missing mods.

## What should I attach to a bug report?

Include the Redux and Windows versions, relevant BG3 patch, smallest repeatable steps, expected and
actual behavior, and a focused screenshot or log excerpt. Redact personal paths and never post API
keys, signed URLs, saves, complete PAKs, or somebody else's mod archive. See [Support](SUPPORT.md).

## Where can I get help?

Use [Discord](https://discord.gg/rJJF89vqFZ) for general community help and discussion. Use the
[GitHub issue tracker](https://github.com/circleainn/BG3ModManager-Redux/issues) for reproducible
bugs and focused feature requests. Report security-sensitive problems privately through
[GitHub Security Advisories](https://github.com/circleainn/BG3ModManager-Redux/security/advisories/new).
