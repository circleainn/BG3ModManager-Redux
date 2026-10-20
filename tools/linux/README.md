# Experimental Redux forwarding under Wine

Redux remains a Windows application. These development helpers are not a native Linux build or
a supported Wine release. Automated checks cover argument construction and validation; this restored
implementation still needs end-to-end testing on Linux, including Wayland, cold launch, downloads,
installation, Sync, and update/backup restore.

## Match the running manager environment

Use the **same Wine runtime, prefix, working directory, and environment** used to open Redux.
The manager's prefix can be different from BG3's prefix. A launcher running inside a container,
Flatpak, bubblewrap namespace, or Proton wrapper may need its own forwarding adapter; launching
ordinary system Wine outside that environment is not equivalent. The helper does not create a
prefix, install .NET, configure Proton, or translate paths automatically.

For the Windows-dotnet launch mode, the command is conceptually:

```text
wine C:\path\to\dotnet.exe Z:\path\to\Redux.dll --nxm <one NXM URI argument>
```

`dotnet.exe` must be the Windows host inside this environment, not Linux `/usr/bin/dotnet`.
The helper passes an argument array without a command shell. It relies on Redux's existing
single-instance forwarding, and never calls `wineserver -k`. On cold launch it can remain alive
until Redux exits. It does not queue links or confirm that a download has completed.

## Configure the native helper

Keep `redux_nxm_forward.py` in a stable location on the Linux host and use Python 3.10 or newer.
Create a local JSON file, for example `~/.config/redux/nxm.json`, with your own absolute paths:

```json
{
  "wine": "/usr/bin/wine",
  "wine_prefix": "/home/USER/.local/share/redux-prefix",
  "working_directory": "/home/USER/Applications/Redux",
  "windows_dotnet": "C:\\Program Files\\dotnet\\dotnet.exe",
  "windows_redux_dll": "Z:\\home\\USER\\Applications\\Redux\\Redux.dll"
}
```

Use host Linux paths for `wine`, `wine_prefix`, and `working_directory`; use paths visible through
the manager's Wine prefix for the two Windows fields. A verified wrapper may replace `wine` if it
preserves the running manager environment and forwards every argument unchanged. Do not configure
a wrapper that shuts down Wine when a forwarding process exits.

Keep the configuration private to your account. It stores paths only; put Nexus credentials in
Redux, never in this file. The helper suppresses child output and does not print or persist signed
NXM links. Avoid shell tracing or wrappers that log their arguments.

## Register the Linux desktop handler

Redux's **Wine-prefix NXM** setting concerns the Windows registry inside the prefix. It cannot
prove that a native Linux browser has the right desktop handler. Configure that separately.

Before changing the desktop default, record the existing handler so you can restore it:

```sh
xdg-mime query default x-scheme-handler/nxm
```

Create `~/.local/share/applications/redux-nxm.desktop`, substituting absolute paths. The entry uses
one `%u` field for one URI, following the [Desktop Entry specification](https://xdg.pages.freedesktop.org/xdg-specs/desktop-entry/latest/exec-variables.html).
Keep this file under your account rather than installing it system-wide.

```ini
[Desktop Entry]
Type=Application
Name=Redux NXM (experimental Wine forwarding)
Exec=/usr/bin/python3 "/home/USER/Applications/redux_nxm_forward.py" --config "/home/USER/.config/redux/nxm.json" %u
MimeType=x-scheme-handler/nxm;
NoDisplay=true
Terminal=false
```

Register it when you are ready to replace the current native handler:

```sh
xdg-mime default redux-nxm.desktop x-scheme-handler/nxm
xdg-mime query default x-scheme-handler/nxm
```

This helper accepts **BG3 file links only** and rejects other games. Linux normally has one default
handler for the `nxm` scheme, so this choice also receives other games' links. If you manage several
games, use a separate dispatcher or keep your existing handler. Restore the previous desktop file
with `xdg-mime default PREVIOUS.desktop x-scheme-handler/nxm` before removing this registration.

## Check each stage

1. Open Redux in the configured environment. Enable online mod information and configure the Nexus
   API key through Redux. **Open Download Manager for Nexus links** makes receipt visible.
2. Use a fresh BG3 **Mod Manager Download** link from the browser. Verify the entry appears in
   Download Manager, then verify completion. Successful forwarding is not proof of authentication,
   download completion, or installation.
3. Review and install the package, then separately check activation, order editing, Save, and Sync
   using disposable data. Test cold launch separately from an already-running instance.
4. Test updater and backup-restore restart under the same Windows dotnet host before relying on them.
   Keep an independent copy of the portable installation and user data for recovery.

For missing Script Extender detection, Redux checks the updater's configured `CachePath`. The
**Script Extender detection folder** setting overrides detection only; it does not move the cache
or rewrite the updater configuration. Use an absolute Windows-visible folder in the correct prefix.
A relative `CachePath` depends on the game's launch directory, so provide an explicit detection
folder instead of relying on Redux's different working directory.

## Development checks

```sh
python3 -m unittest discover -s tests/linux -v
```

The tests simulate process dispatch and never launch Wine or change desktop associations. They
cover signed-URI preservation, one-argument delivery, explicit prefix selection, malformed/wrong-game
links, paths with spaces, child-output suppression, and errors that do not echo signed links.
