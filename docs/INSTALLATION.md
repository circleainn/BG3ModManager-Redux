# Installation, updates, and removal

Redux is a portable Windows application and does not belong inside the Baldur's Gate 3 directory.
`0.1.0-alpha.16.4.3` is the current public-alpha release. Public releases use one portable ZIP;
GitHub Releases and Nexus Mods receive the same approved archive.

## Requirements

- Windows 10 or Windows 11, x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Baldur's Gate 3

Linux, macOS, Wine, and Proton are not supported. Redux is framework-dependent rather than a
self-contained application.

## Install Redux

1. Download the complete portable archive from the official Redux Nexus Mods page or an official
   GitHub release.
2. Create a dedicated writable folder such as `C:\Modding\Redux`. Avoid the BG3 installation
   directory, Windows system folders, and running directly from a compressed archive.
3. Extract every file while preserving the archive's folder structure.
4. Run `Redux.exe`.
5. Review the detected BG3, profile, Mods, saves, and Script Extender paths before making changes.
6. Complete Welcome Setup. Online source information, Load Order Advisor guidance, NXM handling,
   and retained package archives remain optional.

Windows may warn about an unsigned or unfamiliar alpha executable. Verify that the archive came
from an official Redux channel before continuing. Never download a repackaged build from an
untrusted mirror.

## Move from a private-alpha build

Private-alpha builds are updated manually when moving to the public alpha:

1. Close BG3 and Redux. Wait for active downloads or file operations to finish.
2. Back up the entire Redux folder, especially its runtime-state directories.
3. Extract the complete new archive over the existing Redux folder.
4. Start `Redux.exe`, not the legacy `BG3ModManager.exe`, and confirm the version shown in the title
   bar or About window.
5. Verify the selected profile and saved order before syncing the game load order.
6. If the executable moved, repair Redux's NXM association from Download Manager.

The release packager deliberately excludes user state, including `Data`, `orders`, `_Logs`, caches,
downloads, retained archives, and backups. An update archive should therefore replace application
files without supplying somebody else's runtime state. Do not interpret that exclusion as
permission to delete your existing state folders.

Alpha.15 renamed the desktop runtime. A manual extract over a private-alpha folder can leave the old
`BG3ModManager.exe` and related runtime files beside the new build because those files are not in the
new release inventory. After confirming `Redux.exe` works and preserving a rollback backup, remove
only the obsolete `BG3ModManager.exe`, `BG3ModManager.dll`, `BG3ModManager.deps.json`, and
`BG3ModManager.runtimeconfig.json` files. Do not remove folders or other unlisted content.

## Update a public-alpha build

Public-alpha builds can check Redux's official GitHub update channel in the background. When a
newer release is available, Redux shows the version and official release notes before offering
**Update & Restart**. Choosing **Later** leaves the current installation unchanged.

The update is downloaded into per-user temporary staging, checked against the release's declared
byte length and SHA-256 hash, and inspected before Redux closes. A separate narrow updater then
replaces only files named in Redux's release inventory. Files outside that inventory—including
preferences, downloads, retained archives, saved orders, logs, and user-created files—are not
claimed or removed. If file replacement fails, the updater restores the files it backed up before
the transaction.

The built-in updater updates an existing Redux folder; it is not a fresh installer. For a manual
update, close Redux, back up its folder, and extract the complete newer portable archive over it. A
protected or read-only location can prevent an in-place update, so keep Redux in a writable folder.

## Move Redux

Close Redux before moving its folder. Keep the entire directory together so settings, saved orders,
download state, custom appearance assets, and backups remain available. After moving:

- launch the executable from the new folder;
- review every configured path;
- repair the NXM association if Redux owns it; and
- update shortcuts that point to the previous executable.

## Roll back

Keep a backup of the previous working Redux folder before updating. To roll back, close Redux and
restore that complete backup. Settings written by a newer alpha may not be understood by a much
older build, so restoring only old binaries into newer runtime state is not a reliable rollback.

Never use an application rollback to roll back `modsettings.lsx`, installed PAKs, saves, or managed
game-directory files. Use Redux's relevant restore, deletion, Save Game Manager, or Game-Directory
Mod Manager workflows for those items.

## Remove Redux

Removing the application does not uninstall mods, saves, Script Extender, or game-directory mods.

1. Finish or cancel active downloads and close BG3.
2. If Redux handles `nxm://` links, use **Disable NXM Links** so it can restore the previous handler
   where possible.
3. Remove Redux-managed game-directory mods through Game-Directory Mod Manager if you want those
   files removed or restored.
4. Close Redux.
5. Keep any saved orders, archives, logs, or backups you still need, then remove the Redux folder.

Older public-alpha Setup builds may still leave a **BG3 Mod Manager Redux** entry in Windows
Installed Apps. Use that entry to remove the old registration before deleting its folder. New public
releases are portable and do not create an Installed Apps entry.

If Redux cannot start and still owns the NXM association, restore the same Redux folder long enough
to disable the association, or repair the Windows default-app/protocol setting manually.

## Back up the right things

Before testing an alpha update, independently back up:

- the complete Redux folder;
- the BG3 Mods folder;
- important saves;
- `modsettings.lsx` and any profiles you cannot recreate; and
- original downloaded archives that are not retained by Redux.

Redux's restore points and Package Archive Library are useful recovery tools, but neither replaces
an independent backup.
