<div align="center">

<img src="docs/assets/nexus-description/00-redux-header.png#gh-dark-mode-only" alt="Baldur's Gate 3 Mod Manager Redux" width="100%">
<img src="docs/assets/nexus-description/00-redux-header-light.png#gh-light-mode-only" alt="Baldur's Gate 3 Mod Manager Redux" width="100%">

[![Current build](https://img.shields.io/badge/build-0.1.0--alpha.16.4.4-9A7BFF?style=flat-square)](https://github.com/circleainn/BG3ModManager-Redux/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-4F86F7?style=flat-square)](#requirements-and-alpha-status)
[![License](https://img.shields.io/badge/license-MIT-42A66F?style=flat-square)](LICENSE)
[![Support on Ko-fi](https://img.shields.io/badge/Support-Ko--fi-FF5E5B?style=flat-square&logo=ko-fi&logoColor=white)](https://ko-fi.com/circleain)
[![Join Discord](https://img.shields.io/badge/Discord-Join-5865F2?style=flat-square&logo=discord&logoColor=white)](https://discord.gg/rJJF89vqFZ)

[Download on Nexus Mods](https://www.nexusmods.com/baldursgate3/mods/23799) ·
[Visit the website](https://bg3mm-redux.com) ·
[Browse the docs](docs/README.md) ·
[Report a problem](https://github.com/circleainn/BG3ModManager-Redux/issues)

</div>

> [!IMPORTANT]
> Redux is in public alpha. Keep independent backups of important profiles, saves, downloaded
> archives, and the BG3 Mods folder. Always review proposed load-order changes before applying them.

Redux is a Windows mod manager built on
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). It preserves
BG3MM's proven package, profile, and load-order foundation while adding a cohesive interface,
stronger organization, safer review workflows, and optional offline-assisted guidance.

<h3 id="new-in-16-4" align="center">New in 16.4</h3>
<hr>

- **Save Game Manager overhaul:** a right-hand details pane with screenshots, location, party portraits and save facts; inline mod warnings; mod review; save/campaign ZIP exports; and consistent context menus.
- **Download Manager overhaul:** clearer package rows, streamlined toolbars, separate actions for Downloads, Installed, and Archives, and red **Delete All** actions.
- **Nexus Collection Importer:** select and filter collection files, compare installed mods, follow download progress, reopen recent collections, and save supported BG3 load orders for later use.
- **Organization and onboarding:** automatically saved inactive ordering and separators, shared sorting/filtering behavior, live appearance previews, and optional starter separators.

Read the [full 16.4 changelog](docs/releases/0.1.0-alpha.16.4.md).

<h3 id="install-and-update" align="center">Install and update</h3>
<hr>

Redux public-alpha releases use one portable ZIP. GitHub Releases and Nexus Mods receive the same
approved archive, and Redux's built-in updater uses that archive for existing installations.

1. Install the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Obtain the complete Redux portable archive from the
   [official Nexus Mods page](https://www.nexusmods.com/baldursgate3/mods/23799) or an official
   [GitHub Releases page](https://github.com/circleainn/BG3ModManager-Redux/releases).
3. Extract the entire archive into a dedicated writable folder such as `C:\Modding\Redux`. Do not
   run Redux from inside the ZIP, the Baldur's Gate 3 installation directory, or a protected system
   folder.
4. Run `Redux.exe`. On first launch, review the detected game and profile paths before
   installing or syncing anything.

`0.1.0-alpha.16.4.4` is the current public-alpha release. The updater acts only when the official
public-alpha channel points to a newer, fully published package. You can also update manually by
backing up the Redux folder and extracting the complete newer archive over it. Release archives
exclude runtime state such as `Data`, `_Logs`, caches, downloads, retained archives, and backups.
Never delete those folders as part of a routine update. If Redux is moved, open Download Manager and
repair the NXM association if Redux previously handled `nxm://` links.

See [Installation, updates, and removal](docs/INSTALLATION.md) for safe migration, rollback, and
uninstall guidance.

<a id="redux-at-a-glance"></a>
<img src="docs/assets/nexus-description/02-main-features.png#gh-dark-mode-only" alt="Redux at a glance" width="100%">
<img src="docs/assets/nexus-description/02-main-features-light.png#gh-light-mode-only" alt="Redux at a glance" width="100%">

| Organize | Review | Personalize |
|:--|:--|:--|
| Multiple categories per mod | Built-in package diagnostics | Dark, Light, and Parchment themes |
| Named, collapsible separators | Optional Load Order Advisor | Custom themes and generated gradients |
| Saved orders and comparisons | Export previews and restore points | Adjustable text, fonts, icons, and motion |
| Context-aware <kbd>Ctrl</kbd> + <kbd>Q</kbd> Quick Access | Undo/Redo for reversible changes | Grouped shortcut editor and motion controls |

<h3 id="the-main-workflow" align="center">The main workflow</h3>
<hr>

1. **Install and inspect.** Drop a supported package anywhere on the main Redux window. Its unified
   install target inspects and routes the package. New PAK mods enter Inactive Mods, while updates
   retain the installed mod's active or inactive state and load-order position.
2. **Organize without losing intent.** Assign categories, create separators, move mods, and use
   <kbd>Ctrl</kbd> + <kbd>Z</kbd> / <kbd>Ctrl</kbd> + <kbd>Y</kbd> for reversible edits.
3. **Save deliberately.** Active load-order changes do not overwrite the selected saved order until
   **Save** is pressed. Inactive ordering and separators save automatically as Redux organization.
4. **Review the game change.** **Sync Load Order to Game** shows what will activate, deactivate, or
   move before Redux writes `modsettings.lsx`.

<h3 id="what-redux-adds" align="center">What Redux adds</h3>
<hr>

<a id="categories-separators-and-mod-details"></a>
<img src="docs/assets/nexus-description/03-organization.png#gh-dark-mode-only" alt="Categories, separators, and mod details" width="100%">
<img src="docs/assets/nexus-description/03-organization-light.png#gh-light-mode-only" alt="Categories, separators, and mod details" width="100%">

- Automatic and custom categories with names, descriptions, colors, icons, ordering, and filtering.
- Up to three visible category assignments per mod.
- Separators with persistent membership and collapse state. Closed separators move with their
  contained mods and do not absorb nearby rows unexpectedly.
- Compact Active Mods controls can collapse or expand every separator at once. The same action can
  be assigned a shortcut, while individual and context-menu controls remain available.
- A resizable details drawer and hover cards for descriptions, requirements, files, changelogs,
  source pages, diagnostics, and private notes.
- Configurable list columns and unified selection between Active and Inactive Mods. The # column
  starts visible in Active and hidden in Inactive; either can be changed in the column menu.
- Inactive ordering and separators save automatically, independently of active-order Save/Discard.
- Category filtering applies to both panes. Click a filtered/sorted-view notice to clear that view;
  column-menu order follows your column arrangement.

Categories, separators, and notes are Redux presentation data. They never enter the game's
`modsettings.lsx`.

<a id="diagnostics-and-load-order-advisor"></a>
<img src="docs/assets/nexus-description/08-diagnostics-and-load-order-advisor.png#gh-dark-mode-only" alt="Diagnostics and Load Order Advisor" width="100%">
<img src="docs/assets/nexus-description/08-diagnostics-and-load-order-advisor-light.png#gh-light-mode-only" alt="Diagnostics and Load Order Advisor" width="100%">

Mod Diagnostics is built into Redux. It brings facts already detected by BG3MM's package parser—
including dependencies, UUID problems, overrides, Mod Fixer behavior, and Script Extender
requirements—into consistent row indicators, hover details, the mod drawer, and review windows.
Diagnostics are read-only: they do not download, repair, remove, activate, or reorder mods.

The **Load Order Advisor** is the optional, experimental layer. When enabled, it adds cautious
placement checks based on exact package declarations and Redux's offline ordering knowledge.
**Organize Active Load Order** can preview one of three policies:

- preserve current separators and sort only within them;
- replace them with non-empty suggested separators; or
- remove active separators and organize the full numbered list.

Nothing is applied silently. The preview shows mod moves, actual separator changes, and
relationships that need review; unchanged preserved separators are left out of the change count.
Applying it creates one undoable, unsaved edit, and individual recommendations can be ignored and
restored later. Load Order Advisor never reorganizes inactive mods.

<a id="safer-load-order-changes"></a>
<img src="docs/assets/nexus-description/04-saving-and-syncing.png#gh-dark-mode-only" alt="Safer saving and syncing" width="100%">
<img src="docs/assets/nexus-description/04-saving-and-syncing-light.png#gh-light-mode-only" alt="Safer saving and syncing" width="100%">

- Explicit working state with an unsaved indicator and close protection.
- Named order creation, renaming, deletion, comparison, and history.
- Bounded Undo/Redo for activation, deactivation, movement, separators, organizer changes, and
  guarded game-file changes.
- Staged and validated writes for saved orders, settings, imports, backups, and `modsettings.lsx`.
- Per-profile restore points before confirmed game changes.
- External-change protection: Redux will not undo over a game file changed afterward by BG3 or
  another manager.

<a id="save-game-manager"></a>
<img src="docs/assets/nexus-description/06-save-game-manager.png#gh-dark-mode-only" alt="Save Game Manager" width="100%">
<img src="docs/assets/nexus-description/06-save-game-manager-light.png#gh-light-mode-only" alt="Save Game Manager" width="100%">

Open **Tools > Save Game Manager...** or use **Saves > Manage**. The list groups saves by campaign
and shows thumbnails, save type, difficulty, date, and size. Autosaves and quicksaves are identified;
campaign counts and collapse controls use the same styling as the other managers.

Select a save to inspect the right-hand details pane: screenshot, recorded location, game version,
party members, levels, races, classes, and mod counts when available. Known companions have
portraits; generic party members use clearly identified race-based placeholders.

**Review Mods** compares the save's recorded UUIDs with the installed library and enriches known
entries from Redux's database. Missing/inactive warnings appear on save rows. Review is available
for saves with recorded mods, even when none are missing. You can activate selected installed
inactive requirements with Undo support; review does not automatically save or sync the order.
It does not verify required versions, dependency chains, or native mods.

Use **Install Save > File or Archive / Folder**, or drop a save into Redux. Imports are staged,
archive paths and sizes are checked, and existing files require review before replacement.
**Saves Folder** opens the save location. Right-click a save for **Review Mods**, **Export This
Save**, **Export Campaign**, or **Show in Folder**. ZIP exports include thumbnails and support
progress/cancellation, with a limit of 32 saves, 1 GB total, and 256 MB per file.

> [!NOTE]
> Redux reads metadata but does not edit or guarantee the integrity of save contents. Close BG3
> before changing saves, keep independent backups, and remember that Steam Cloud may restore
> files removed locally. Deletion uses the Windows Recycle Bin.

<a id="game-directory-mod-manager"></a>
<img src="docs/assets/nexus-description/07-game-directory-mods.png#gh-dark-mode-only" alt="Game-Directory Mod Manager" width="100%">
<img src="docs/assets/nexus-description/07-game-directory-mods-light.png#gh-light-mode-only" alt="Game-Directory Mod Manager" width="100%">

Open **Tools > Game-Directory Mod Manager...**, its **Mods & Campaign** toolbar shortcut, or
**Quick Access** to review supported native and root-level mods that install beside BG3 rather than
into the ordinary Mods folder. The action is also available in the shortcut editor if you want to
assign your own key combination. These packages stay outside the PAK load-order panes. Redux shows
every managed destination before applying a change and clearly warns that native DLLs execute
inside the game.

The reviewed catalog recognizes layouts for Native Mod Loader, WASD and camera plugins,
Achievement Enabler, Baldur's Priority, Improved Camera, Best of Hands, BG3WASD Camera Follow,
True Third-Person Camera, bg3fgvk, and Script Extender. Script Extender now uses the same
staged game-directory transaction and ownership record as the rest of the reviewed catalog. Its
dedicated manager action downloads, reviews, installs, updates, or reinstalls the current release;
**Tools > Manage Script Extender...** navigates to that action instead of running a separate installer. Mixed
native-and-PAK packages send their companion PAK through the normal inactive-mod import path.
The action is contextual: a current installation is disabled, an older installation offers an
update, and changed or missing Redux-owned files offer a reviewed repair. An exact reviewed
Script Extender DLL installed outside Redux can be adopted without rewriting it; unknown DLLs stay
unmanaged and explain why adoption is unavailable.

Archives may be selected in the manager or dropped onto Redux. Installation is staged and bounded;
paths, layouts, reviewed DLL fingerprints, AMD64 DLL headers, prerequisites, the source archive, and
destination files are rechecked before commit. Exact fingerprints distinguish related projects that
share filenames and identify known versions; unknown or modified DLLs remain explicitly unverified.
Reviewed add-only plugins installed elsewhere can be adopted without rewriting them, after which
Redux removes only unchanged adopted DLLs. User-editable `.toml` and `.ini` configuration and
companion PAKs remain user-owned.

Replacer mods use stricter ownership. Redux never adopts an external replacer because it did not
preserve the files that were already replaced. To bring one under management, remove the external
replacer, verify BG3 through Steam or GOG, then install it through Redux. Before replacing anything,
Redux verifies the current targets as known clean game files and stores those originals in protected
backup storage. Unknown or already-modded targets are refused rather than backed up. Redux does not
ship copies of BG3 files, and removal restores an original only while all managed files still match
the ownership record.

> [!CAUTION]
> Layout validation is not a publisher signature or malware scan. Install native code only from a
> source you trust, close BG3 first, and use the manager's status and recovery information instead
> of manually deleting Redux-owned files.

<a id="download-manager"></a>
<img src="docs/assets/nexus-description/05-downloads.png#gh-dark-mode-only" alt="Download Manager" width="100%">
<img src="docs/assets/nexus-description/05-downloads-light.png#gh-light-mode-only" alt="Download Manager" width="100%">

**Download Manager** is Redux's shared intake inbox for local packages and optional Nexus Mod
Manager downloads. Add a package from the window or drop supported PAK, LSV, ZIP, 7z, RAR, TAR, or
GZip files onto Redux's full-window install target. Drop location never selects Active versus
Inactive Mods. Local inputs are copied into the managed Downloads folder, assigned a stable SHA-256
identity, inspected, and shown in the same persistent inbox as network acquisitions.
Known local packages reuse installed or bundled source artwork during intake, so their Download
Manager cards do not have to wait for installation before showing an available thumbnail.

For Nexus, enable online mod information, add a Nexus Mods API key, and enable NXM links during
onboarding or in Preferences. Choosing **Mod Manager Download** then sends the `nxm://` link to the
existing Redux process and opens **Download Manager**. An optional preference controls whether
protocol activations bring that window to the front.

Downloads are queued in a managed folder with bounded concurrency, visible progress,
pause/resume/retry behavior, restart recovery, and a verified SHA-256 archive identity. Free-user
downloads that lose their temporary authorization ask for a new link without saving the temporary
key or signed URL. Network state remains separate from package inspection and installation.
Completed packages are automatically classified and remain separate from installation until their
destination-aware **Install** action is chosen. Redux then verifies the archive again and routes
ordinary PAKs to Inactive Mods, reviewed native and Script Extender packages through Game-Directory
Mod Manager, and saves through Save Game Manager. Mixed, ambiguous, malformed, and unreviewed native
layouts stay blocked in the inbox without changing files. Intake never activates, reorders, or syncs
a mod automatically.

The **Downloads** tab groups **Install All**, **Pause All**, **Resume All**, and **Delete All** in
one action bar. The **Installed** tab clears history, while **Archives > Delete All** deletes retained
packages. NXM association appears at the bottom of Downloads; archive retention appears at the
bottom of Archives. Bulk download deletion cancels transfers and recycles completed packages,
while preserving installed mods and the separate archive library.

**Install All** performs one full preflight before changing any destination. Its single grouped
review identifies the packages headed to Inactive Mods, Save Games, and Game-Directory Mods, plus
anything Redux will skip. Duplicate archives, overlapping mod/destination identities, missing
dependencies, unsafe layouts, and unavailable destinations are skipped with a reason. Accepted
packages install one at a time through the same guarded destination services; an independent
failure does not stop the remaining queue. A final per-package result summary replaces repeated
confirmation dialogs. Existing saves listed as replacements are covered by the one batch review.

The PAK install review distinguishes clean new mods from updates, replacements, downgrades, and
unreadable packages. **Review clean mod installs** can be turned off from a clean review or restored
in Preferences; only entirely clean, brand-new PAK batches bypass that dialog. Anything requiring a
decision continues to stop for review.

Active transfers are paused and their queue state is saved before Redux exits. Finished downloads
and completed installation history do not keep the application open. Completed installations move
to the **Installed** tab with their destination. **Clear Installed History** removes those records
without changing installed content or the separate Package Archive Library. Unless archive
retention is enabled, a successfully installed package's managed download copy is removed; user-owned
local source files are never changed. Removing an uninstalled completed download explicitly offers
to move its downloaded archive to the Recycle Bin.

Installed-history entries can be reinstalled while their Download Manager archive remains present.
Adding the same local archive again returns that record to Downloads instead of creating a duplicate
or leaving it stranded as completed history.

**Retain installed package archives** is a separate, explicit opt-in available during onboarding and
in Preferences, and directly from Download Manager's Archives tab. After a successful install,
Redux verifies the package again and stores one
content-addressed copy in **Download Manager > Archives**. Identical files are deduplicated by
SHA-256. The default 10 GB quota is configurable from 1–100 GB; least-recently-used packages are
pruned when the limit is reached. The Archives tab shows current usage and provides **Reinstall**,
**Open Folder**, and **Delete All** actions. Clearing download history never clears this
library, clearing the library never uninstalls content, and deleting a mod never implicitly deletes
either copy. If retention is left off, Redux does not create the archive-library directory. The
archive index stores only safe public source/package identity and installation metadata—not local
source paths, credentials, authorization keys, signed URLs, or thumbnail URLs.

Reinstalling a retained PAK is placement-preserving: an installed mod keeps its current active or
inactive state and its load-order position. A mod that is no longer installed returns to Inactive
Mods. Reinstall never applies or syncs the load order.

<h3 id="nexus-collection-importer" align="center">Nexus Collection Importer</h3>
<hr>

Choose **Import Nexus Collection** in Download Manager, paste a BG3 collection page link, and
press **Import**. **Open collections page** opens the Nexus browser listing. The importer shows
collection artwork and file details beside a compact, searchable file list.

- Filter by **Missing**, **Installed**, **Unverified**, or **Needs attention**. Select All and
  Deselect All affect visible rows; hidden selections are retained. Reset to Defaults restores
  the initial collection choices.
- Exact Nexus file matches in Active or Inactive Mods are skipped by default. Other files from the
  same project are distinguished from exact matches; file IDs alone do not prove a newer version.
  Game-directory matches without exact file identities remain unverified.
- Download selected files through Redux's existing queue. Free Nexus accounts authorize each file
  through **Mod Manager Download**; the importer guides you to the next file and tracks progress.
  Eligible failed downloads can be retried together. Installation failures still require review.
- Recent collections remembers choices for ten collections. The same revision restores choices;
  changed revisions use current defaults with a notice to review them.
- If the collection provides a supported BG3 load order, **Save Load Order** opens a review and
  adds it to the main window's Load Order dropdown. Saving does not activate mods or change the
  current order; missing entries stay in the saved order for later installation.

Collection installer rules and instructions are not applied. Follow the author's setup guidance.
The importer uses collection web links and the latest published revision; direct collection NXM
activation and historical-revision browsing remain future work. Nexus API-key setup is still
required; this is not an SSO connection flow.

<h3 id="redux-modlists" align="center">Redux Modlists</h3>
<hr>

A `.bg3redux` Modlist can carry a saved order plus selected Redux presentation data:

- category definitions, descriptions, assignments, and display order;
- separators, descriptions, positions, membership, and collapse state;
- reusable custom PNG icons;
- public Nexus Mods or mod.io source references; and
- private notes only when explicitly included.

Import and export previews show what will change. A Redux Modlist never contains installed PAKs,
profiles, saves, API keys, or `modsettings.lsx`, and importing one does not install missing mods.
Source-link import is off by default so a recipient's existing provider association is preserved.

**Back Up Active Mods to ZIP** is a separate personal-backup feature. It asks where to save and
reminds users that redistributing mod files requires permission from every relevant author.

<h3 id="offline-mod-recognition" align="center">Offline mod recognition</h3>
<hr>

Redux includes a curated offline database that connects exact package fingerprints and reviewed
module identities to Nexus Mods projects. Matching is intentionally conservative: uncertain mods
remain **Local** rather than being assigned a potentially incorrect source. The same database also
contains exact dependency and ordering facts used only when Load Order Advisor is enabled.

Use **Help > Generate Redux Database Contribution...** to create a privacy-limited
`.bg3redux-report`. It contains sanitized mod identity, known provider IDs, and exact PAK
fingerprints for maintainer review. It does **not** include packages, profiles, load-order positions,
settings, API keys, notes, or private filesystem paths, and generating it changes nothing locally.

If you would like to help improve recognition, attach only the generated report—not archives or
PAKs—to an [issue](https://github.com/circleainn/BG3ModManager-Redux/issues). Reports are reviewed;
they are never imported into the bundled database automatically. The full trust and contribution
model is documented in the [Redux mod database guide](docs/REDUX_MOD_DATABASE.md).

<a id="themes-and-accessibility"></a>
<img src="docs/assets/nexus-description/09-themes-and-personalization.png#gh-dark-mode-only" alt="Themes and accessibility" width="100%">
<img src="docs/assets/nexus-description/09-themes-and-personalization-light.png#gh-light-mode-only" alt="Themes and accessibility" width="100%">

- Redux Dark, Redux Light, Parchment, and importable custom themes.
- Solid semantic action colors or theme-generated gradients.
- Compact, Default, and Large text with bundled or imported `.ttf` / `.otf` fonts.
- Optional category-colored interactions, colored text, icons, and icon-only labels.
- Contextual category and semantic colors extend through Quick Access hover and selection states;
  disabling colored interactions restores the active theme's standard treatment.
- Reduce Motion for scrolling, sliding, scaling, and animated transitions while preserving clear
  hover and selection states.
- Independently removable background blur and dimming.
- Configurable shortcuts, keyboard-operable dialogs, selectable dialog text, screen-reader helpers,
  and inherited speech commands.

Quick Access searches commands, profiles, saved orders, categories, installed mods, theme actions,
folders, and Redux support links from one keyboard-first surface. Its category results reuse saved
category icons and colors, while destructive, warning, and save/export actions retain their
semantic meaning. The shortcut editor groups related actions and provides compact group-wide
expand/collapse controls.

Welcome Setup walks through appearance, adding mods, load-order practice, organization, and the
save/native managers. Appearance settings preview live, including fonts, icons, gradients, and text
size; canceling restores the previous look. The Organize step offers nine optional starter
separators, individually selectable. They append to Active Mods without moving existing mods or
repeating existing names. Optional online and advisor features begin disabled. Provider keys are masked, encrypted for the current Windows account, and excluded from
ordinary settings and diagnostic exports.

<h3 id="built-on-bg3-mod-manager" align="center">Built on BG3 Mod Manager</h3>
<hr>

Redux is a fork, not a from-scratch replacement. It retains substantial work from LaughingLeader
and other BG3MM contributors, including:

- profiles, campaigns, active/inactive lists, saved orders, and the core load-order model;
- PAK and archive handling through LSLib;
- BG3 path detection, launch workflows, overrides, dependency parsing, Osiris and Mod Fixer
  detection, and Script Extender integration;
- Nexus Mods integration, caching, metadata, images, links, and update foundations; and
- configurable shortcuts, developer utilities, CrossSpeak, Windows speech fallback, and
  screen-reader support.

Redux reworks and extends many of these systems while retaining their credit. See
[Changes from upstream BG3 Mod Manager](docs/CHANGES_FROM_UPSTREAM.md) for a precise comparison.

<h3 id="for-mod-authors" align="center">For mod authors</h3>
<hr>

**Tools > Inspect Mod Package** performs a read-only preflight on PAKs, common release archives,
reviewed or unknown native/DLL layouts, hybrid packages, save archives, and loose `.lsv` files. It
reviews identity, expected destinations, dependencies, embedded creator metadata, Script Extender
and Osiris signals, override behavior, and common development debris without installing or
modifying the selected file. A clean result is not a guarantee of in-game compatibility.

Authors may also include an optional root-level
[`redux.mod.json`](docs/REDUX_CREATOR_MANIFEST.md) inside a PAK. Redux validates its module claim
against parsed `meta.lsx` data before using it for Nexus Mods or mod.io identification. Invalid
claims are ignored and reported without changing packages or load orders.

<h3 id="requirements-and-alpha-status" align="center">Requirements and alpha status</h3>
<hr>

- Windows 10 or Windows 11, x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Baldur's Gate 3

Linux, macOS, Wine, and Proton are not supported. Redux is framework-dependent and is not
distributed as a self-contained build.

Known public-alpha limits include personal Nexus API-key authentication instead of public SSO,
incomplete provider/category/dependency coverage, imported-font variability, incomplete mod.io
author links, and limited clean-machine testing. Uncommon scaling and extremely dense layouts may
still expose visual issues. The updater remains inactive whenever the official public-alpha channel
manifest is unavailable. Imported fonts and PNG icons remain the user's responsibility to license.

<h3 id="documentation" align="center">Documentation</h3>
<hr>

| Guide | Audience | Purpose |
|:--|:--|:--|
| [Documentation index](docs/README.md) | Everyone | Find the right user, author, or maintainer guide |
| [Current project state](docs/CURRENT_STATE.md) | Everyone | See the current release, settled decisions, known limits, and planned work |
| [Installation and updates](docs/INSTALLATION.md) | Users | Install, upgrade, move, roll back, or remove Redux safely |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Users and testers | Recover from common startup, path, download, and sync problems |
| [Privacy and local data](docs/PRIVACY_AND_DATA.md) | Everyone | Understand stored data, network requests, logs, and safe sharing |
| [Changes from upstream](docs/CHANGES_FROM_UPSTREAM.md) | Users and contributors | Understand what Redux retains and changes |
| [Architecture](docs/ARCHITECTURE.md) | Contributors | Understand project boundaries, major components, and validation paths |
| [Diagnostics and optional features](docs/REDUX_OPTIONAL_MODULES.md) | Users and contributors | Understand diagnostics, online information, and advisor boundaries |
| [Redux mod database](docs/REDUX_MOD_DATABASE.md) | Contributors and maintainers | Recognition, advisor knowledge, and reports |
| [Mod developer tools](docs/MOD_DEVELOPER_TOOLS.md) | Mod authors | Inspect releases before distribution |
| [Creator manifest](docs/REDUX_CREATOR_MANIFEST.md) | Mod authors | Add a validated source identity to a PAK |

<h3 id="reporting-problems" align="center">Reporting problems</h3>
<hr>

Use the [issue tracker](https://github.com/circleainn/BG3ModManager-Redux/issues) for reproducible
bugs. Include the Redux version, the smallest reliable reproduction steps, relevant screenshots or
logs, and affected mod names or UUIDs. Never post API keys or unreviewed private path information.
Read [Support](docs/SUPPORT.md) and [Troubleshooting](docs/TROUBLESHOOTING.md) before sharing logs or
runtime data.

<h3 id="credits-and-license" align="center">Credits and license</h3>
<hr>

Redux exists because of [LaughingLeader's original BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager).
You can also [support LaughingLeader on Ko-fi](https://ko-fi.com/LaughingLeader).

Bundled dependencies and assets include LSLib, CrossSpeak, AdonisUI, ReactiveUI,
GongSolutions.WPF.DragDrop, Lucide, and the bundled open fonts. Attribution, the packaged-runtime
license inventory, and retained license texts are in
[Third-Party Notices](licenses/Third-Party-Notices.md).

Baldur's Gate 3 is developed and published by Larian Studios. Redux is an unofficial community
project and is not affiliated with or endorsed by Larian Studios, Nexus Mods, or mod.io.

The original project and Redux modifications are distributed under the [MIT License](LICENSE),
subject to all retained copyright and third-party notices.
