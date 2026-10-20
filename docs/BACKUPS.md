# Backups and snapshots

Open **Preferences > Backups** to create a named snapshot, browse saved versions, or restore one.
Snapshots live in `Backups/Snapshots` inside the Redux installation. **Open backup folder** opens
that location. Keep an independent copy on another drive if you need protection against disk loss.
These local archives contain private settings and original folder paths; do not upload them as
support attachments.

## Create a snapshot

1. Save any unfinished load-order edits and close Baldur's Gate 3. Finish active file operations.
2. Enter a name and optionally enable **Include mods and mod lists**.
3. Choose **Create snapshot and restart** and review the confirmation. Redux saves its settings,
   stops downloads safely, and closes. The external runner creates and verifies the archive, then
   restarts Redux. Preparation has progress and cancellation; incomplete archives are not listed.

| Scope | Included |
| --- | --- |
| Redux snapshot | Application files, saved settings, artwork, and saved orders, including a configured external order folder |
| Include mods and mod lists | Also captures BG3's Mods folder, held Overrides, and profile `modsettings.lsx` / `playerprofiles8.lsx` files |
| Always excluded | Snapshot/backup library, caches, logs, temporary folders, saved games, game-directory DLLs and loose textures |

The native game-directory manager's ownership records are preserved in place because its game
files are outside this snapshot's scope. Held Override packages are excluded when the mod option
is off. This is not a whole-game installation image. Existing mod archives/downloads outside the
Redux folder are not captured. Linked source folders or files are rejected rather than traversed.
Make sure there is space for the archive; restoring also needs staging and a return snapshot.

## Restore backward or forward

Select a snapshot and choose **Restore selected and restart**. Review its version and whether it
contains mods. Restore replaces the files in its included folders and removes newer files from
those same folders. Saves and excluded folders remain unchanged.

Before changing anything, Redux verifies the selected snapshot and saves a new **Before restoring…**
snapshot of the current state with the same scope. To roll forward again, select that return
snapshot and restore it. Neither the selected snapshot nor the return snapshot is automatically
deleted. Restoring a Redux-only snapshot does not change BG3 PAKs or profile load orders.

External mod, profile, and order folders must match the recorded locations; Redux does not silently
redirect a restore to another installation. Restore runs after Redux closes so its executable,
settings, and mod lists are not being rewritten by the running app. Cancellation is available
during verification and backup, then disabled while replacement or recovery is in progress.

## Interrupted restore

A durable journal remains in the snapshot library until restore or rollback completes. If a write
fails, the runner attempts to restore the return snapshot automatically. If recovery cannot finish,
it retains the journal and archives and does not launch the partially restored application.

Resolve the reported error (for example, a locked file or insufficient space), then retry the
original runner request. Its location appears in the error, and `RECOVERY.txt` beside the copied
runner explains the command. Runners are retained under the current user's
`AppData/Local/BG3ModManager-Redux/SnapshotOperations` folder. If Redux still opens, use
**Preferences > Backups > Recover interrupted restore**. Do not delete the library or the runner
folder while recovery is pending. Redux checks the journal before opening its workspace and offers
recovery instead of loading a partially restored state. While the external runner works, the same
single-instance guard prevents another Redux window from reopening the installation.

The existing pre-update ZIP backup remains available. Both workflows share the backup writer;
pre-update archives exclude the snapshot library to avoid recursively copying prior backups.
Older pre-update ZIPs do not contain the snapshot inventory and are not offered as automatic
snapshots. Their `RESTORE.txt` instructions remain the manual recovery route.
