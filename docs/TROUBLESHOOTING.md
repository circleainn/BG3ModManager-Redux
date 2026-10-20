# Troubleshooting Redux

Start with the smallest safe check. Do not delete settings, game files, or the entire Redux folder
to troubleshoot an unknown problem. Record the exact version shown by the running application;
a recent download or comment date is not proof of which build is running.

## Redux does not start

1. Confirm the full archive was extracted and `.NET 8 Desktop Runtime` is installed.
2. Confirm you are launching `Redux.exe`. `BG3ModManager.exe` is the obsolete private-alpha runtime
   name and should not be used for alpha.15 or later.
3. Move Redux to a normal writable folder if it is inside a ZIP, the BG3 directory, or a protected
   system location.
4. Look for `startup_crash.log` or the newest log under Redux's `_Logs` directory.
5. Back up runtime state before changing or removing any file.
6. Report the exact Redux version and the first relevant exception. Redact personal paths and never
   include credentials.

Do not repeatedly launch Redux while an updater, antivirus scanner, cloud-sync client, or earlier
Redux process is still replacing or locking its files.

## An application update fails

Redux verifies and extracts an update before closing. If that preparation fails, the existing
installation remains unchanged and **Try Again** is available. If Redux cannot finish closing—for
example, because its download queue cannot be saved—the queued update is cancelled rather than
being applied later without warning.

The updater changes only release-inventory files and attempts to restore those files when a
replacement fails. Check write access to the Redux folder, close tools that may lock its binaries,
and retry. If the files update successfully but Redux cannot restart automatically, launch
`Redux.exe` yourself; the completed update is still valid. Preferences, downloads,
archives, saved orders, and mods are not application-update targets. If the helper cannot run,
download the complete archive from the official release page and follow the manual update steps in
[Installation, updates, and removal](INSTALLATION.md).

The 16.4.2 maintenance notes include retries for short-lived Windows locks and handling for
read-only application files. If the problem recurs, record the exact running version and the named
blocked file, with private paths redacted. Do not delete user state to work around a locked binary.

## Paths or profiles are wrong

Open Preferences and verify the BG3 executable, Mods, profiles, saves, and Script Extender paths.
Selecting a different profile changes the active game context; confirm the profile and campaign
before installing, deleting, or syncing.

## NXM links open the wrong application

Open Download Manager and inspect the NXM association state. Use **Repair NXM Links** when Redux
owns a registration that still points to an earlier executable location. Use **Disable NXM Links**
to restore the previous per-user handler where Redux has a valid recovery snapshot. If another
manager currently owns the association, changing it requires an explicit reassociation rather than
an automatic repair that silently takes over.

An NXM link may need to be requested again when its temporary Nexus authorization has expired.
Redux never persists signed download URLs or temporary authorization values.

## Provider key fields are blank after restarting

The earlier display-state report [#129](https://github.com/circleainn/BG3ModManager-Redux/issues/129)
was fixed in 16.4.3. If the symptom returns in a current build, a blank Preferences field does not
by itself show whether an encrypted credential was lost or the control failed to display its saved
state.

Before re-entering a key, note whether an authenticated provider action still works after restart.
Report that result, whether the field was empty immediately or only after reopening Preferences,
and the exact Redux version. A failure may have other causes, so include only the visible error or
a narrow redacted log excerpt. Do not repeatedly save a blank credential field or reset settings as
a first troubleshooting step.

Personal Nexus API-key authentication is the current documented flow; official SSO is separate
planned work. Never send the key, credential-storage file, signed download URL, or an unredacted
screenshot to support.

## Mod thumbnails are missing

The earlier missing-thumbnail report
[#130](https://github.com/circleainn/BG3ModManager-Redux/issues/130) is closed after thumbnail
retry and rendering fixes. A new missing image still needs its own evidence.

Check whether the affected mod has the correct source association, whether optional online
information is enabled, and which surface is missing the image. Not every package has available
artwork. A successful download does not prove that the separate metadata/image request succeeded,
and an empty key field does not establish that all thumbnails must be hidden.

Report one affected mod's public page or UUID, the running version, whether the issue happens before
or only after restart, and a redacted screenshot. Do not erase all caches or share credentials to
investigate a missing image.

## A download is incomplete or needs a new link

Open Download Manager and read the item's current action. Free-account downloads may require a
fresh matching Mod Manager Download link. A completed file is revalidated before installation; a
missing, changed, truncated, or unsupported archive will not be treated as complete.

Clearing installed history does not uninstall content. Clearing the Package Archive Library does
not uninstall content or clear download history. Removing an unfinished item and deleting its
partial file are explicit, separate choices.

## Script Extender version appears older than expected

Redux detects version metadata from the Script Extender cache. A downloaded updater release and
the extender actually loaded by the game are different evidence. Check the game's extender version,
the selected game executable, the updater's Target Version and CachePath, and any Redux detection-folder
override before changing files. An explicit target version can intentionally select an older cached DLL.

The maintenance build clears stale updater pins/cache settings when the selected game's updater
configuration file has been removed. A locked or unreadable configuration retains the previous settings
and logs the read failure. This fixes a confirmed stale-settings path; it does not establish the cause of
every version mismatch. For a persistent mismatch, report the Redux version, warning text, cached DLL
version, and whether it changes after Refresh or restart. Do not delete caches or disable warnings as
a substitute for identifying the selected installation and version.

For experimental Wine setup, see the [native browser forwarding guide](../tools/linux/README.md).
Wine-prefix registration and Linux desktop registration are separate. Script Extender detection
honors its configured cache path; **Preferences > General > Paths and storage > Script Extender
detection folder** overrides detection without changing the updater's actual configuration.
Use an absolute path visible inside Redux's prefix. Wine, native browser forwarding, and restart
still need real-environment validation; Windows regression results do not establish Linux support.

## A package will not install

Read the install review rather than bypassing it. Redux blocks unknown game-directory DLL layouts,
unsafe archive paths, ambiguous mixed packages, invalid saves, and packages that changed after
review. Clean new PAK installs enter Inactive Mods; updates/reinstalls preserve existing placement
as documented. Intake never silently activates, reorders, or syncs.

An archive can contain loose game assets rather than a PAK. **No PAK files found** explains when
`Generated/Public` textures require the author's Data-folder instructions; normal mod import does
not install those files. For example, [Vivid Landscapes (6473)](https://www.nexusmods.com/baldursgate3/mods/6473)
uses loose textures, while [Vivid Landscapes All in One (20021)](https://www.nexusmods.com/baldursgate3/mods/20021)
is a separate PAK release. They are different inputs when investigating an installation report.

For a native replacer installed outside Redux, remove the replacer, verify BG3 through Steam or GOG,
then install the reviewed package through Redux. Redux must never preserve a modded DLL as the clean
game backup.

## Load order changes do not appear in game

Saving a Redux load order and syncing it to BG3 are separate actions. Confirm the correct profile,
save the intended order, then use **Sync Load Order to Game** and review the proposed changes.
Redux will refuse an unsafe undo when another program or the game changed `modsettings.lsx` after
Redux's write.

## An order or its separators looks different after Sync or restart

Active-order edits, named Redux saved orders, and the game's `modsettings.lsx` are separate states.
Active separators belong to their saved Redux order, not to the game file. Inactive ordering and
separators save automatically as global Redux organization.

If separators appear missing after restart, first record the selected profile and order name.
If a named order was saved previously, check whether reopening that same order restores the expected
organization. One Nexus user reported this workaround; it is not a confirmed explanation for every
report. Protect any unsaved work before switching orders, and do not save an unexpectedly empty or
changed view over a known-good named order.

For unexplained movement or mods returning to Inactive, note whether it happens immediately after
Sync, after Refresh, after restarting Redux, or only after launching BG3. Check whether a filtered
or column-sorted view is active; view-only order is not the same as the underlying load order.
Capture the reviewed changes and the selected order before and after, with private data redacted.
Do not repeatedly sync an unexpected result into the game.

[#127](https://github.com/circleainn/BG3ModManager-Redux/issues/127) tracks these reports. The 16.4.1/16.4.2
separator fixes do not prove that every sync/startup symptom is resolved. Do not delete settings,
reinstall mods, or merge Save and Sync as a workaround.

If Redux reports that sync status is unavailable, it could not read a complete `modsettings.lsx`.
The saved workspace and last verified order are retained. Let any game or other manager finish
writing the file, then Refresh. A failed read does not establish that the game reset the order.
Before Sync or empty-order recovery writes, Redux rechecks the reviewed file and stops if it has
changed or become unavailable.

## Override copies exist in both Mods and the holding folder

Open **Tools > Resolve Override Copies**. The review shows both full paths, package parts, sizes,
dates, and whether the contents match. Use **Open Mods**, **Open Holding**, or **Copy Details** to
inspect the conflict. Different copies require an explicit choice; identical copies recommend
keeping the copy already in Mods.

Applying the review retains the chosen package in its current folder and moves the displaced
package out of discovery only after creating verified backups. Backups are retained under
`Data/OverrideDuplicateBackups` beside Redux's `Data/OverrideOrderHolding` folder; the result shows
the exact backup directory. Keeping a held copy does not activate it. After the automatic Refresh,
retry the order switch or activation you intended. If Refresh was canceled or could not run, finish
Refresh before changing Override placement. Refresh itself can finish with a conflict pending.

If interrupted recovery reports that a file changed or its original location is occupied, preserve
the files and recovery records for inspection. Do not delete the holding folder or overwrite a
different package to dismiss the warning.

## Dragging a separator does not carry the expected mods

To place ordinary mods directly in a section, select the mods and use **Move to Separator** from
their right-click menu. Destinations belong to the same pane, including nested and collapsed
separators. The selection keeps its underlying order and is appended after the section's own mods,
before its first sub-separator. A collapsed destination stays collapsed; expand it to see the result.
Undo/Redo restores placement and membership. Active-order edits still require Save and Sync;
Inactive and Override organization follows their existing automatic-save behavior.

Distinguish category assignments from a collapsible separator header, and record whether the
separator was expanded or collapsed. The current drag policy treats expanded headers as marker-only
moves and collapsed sections as block moves. Unexpected membership absorption is a separate concern,
not automatically explained by that distinction.

The earlier expanded-header behavior in
[#126](https://github.com/circleainn/BG3ModManager-Redux/issues/126) is closed. For a new problem,
report the exact version, source and destination pane, expanded/collapsed state, filters/sorting,
and membership before/after the move.
Use a backed-up or disposable order for reproduction; do not keep saving an unexpected arrangement.

## Automatic categories or sidebar filters are unwanted

Open the Categories menu to choose the scope:

- **Clear automatic assignments for current mods** keeps those mods under **No Category** across
  Refresh and restart. Manual assignments and custom categories stay intact, and new mods can still
  receive automatic categories. **Restore cleared automatic assignments** reverses that choice.
- **Hide built-in category filters** and **Show built-in category filters** change sidebar visibility
  without changing assignments. They preserve custom filters and **No Category**. Showing filters
  still respects the automatic-category and empty-category preferences. **Visible category filters**
  controls individual filters.
- **Assign categories automatically** controls automatic classification globally.
- **Reset all mods to automatic categories** also clears explicit manual assignments and **No Category**
  choices. Its confirmation explains this broader scope.

Clearing, restoring, and visibility changes retain the previous choices if settings cannot be saved.

## A mod loses its Nexus link or shows the wrong source

Alpha.16.5.3 preserves a previously verified Nexus link when a refresh lacks an optional creator
manifest. Other cases where a mod changes to **Local** after update or refresh remain tracked in
[#157](https://github.com/circleainn/BG3ModManager-Redux/issues/157). Record the Redux version,
mod UUID, expected public source page, whether the mod was replaced or refreshed, and whether the
source integration is enabled. If a manual-link review identifies a different mod, cancel rather
than applying it. Do not select downloads or updates based on an association you know is wrong,
rename the package, or wipe all source data as a first response.

## A visual or interaction problem appears

Record the active theme, font, text size, Reduce Motion setting, background-effects setting,
Windows scaling, and approximate window size. Test whether the problem persists with Redux Dark,
Manrope, Default text size, and a normal window size; this narrows the report without erasing the
original appearance settings.

Recent releases include fixes for nested-menu hit testing and generated context-menu actions. A
recurrence needs its own steps and scaling details; visual symptoms do not establish the cause of
a separate source-link report.

## Prepare a useful report

Include:

- Redux and Windows versions;
- BG3 patch/hotfix when relevant;
- the smallest repeatable steps;
- expected and actual behavior;
- affected mod names or UUIDs; and
- a screenshot or the narrow relevant log excerpt.

Never attach API keys, credential files, signed URLs, or complete user directories. Do not attach
PAKs, saves, or somebody else's mod archive to an ordinary public issue; use only the smallest
redacted evidence and follow the support channel's instructions. See [Support](SUPPORT.md)
and [Privacy and local data](PRIVACY_AND_DATA.md).
