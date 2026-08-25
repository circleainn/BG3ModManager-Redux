# Private testing

This checklist applies to `0.1.0-alpha.10`. It is for controlled testing, not a public release.
Because the source repository is public, do not attach the test ZIP to a GitHub release. Share the
validated ZIP and its SHA-256 checksum directly with invited testers.

## Before testing

- Use Windows 10 or Windows 11 x64 with the .NET 8 Desktop Runtime installed.
- Back up important profiles, saves, downloaded archives, the BG3 Mods folder, and
  `modsettings.lsx`.
- Extract the supplied ZIP to a new empty folder. Do not overwrite an older Redux installation.
- Compare the ZIP's SHA-256 checksum with the checksum supplied by the maintainer.
- Record whether this is a clean first launch or an upgrade using existing Redux settings.

## Core test pass

Test with a realistic mod collection, including a large list when possible.

1. Launch Redux, complete or reopen first-run setup, and confirm the expected profile and mods load.
2. Scroll repeatedly through the active and inactive lists, including all the way to the bottom.
   Repeat with **Reduce motion** enabled. Neither mode should stutter, skip hovered rows, or freeze.
3. Create several separators. Collapse and expand sections containing both a few mods and many mods.
   Confirm that only the intended section hides and returns, with no lost, duplicated, or absorbed
   mods.
4. Drag an expanded separator between rows. Only the separator marker should move; existing mods
   must remain in their current relative order. A collapsed separator must not start a drag.
5. Filter using category pills, clear the filters, and verify the visible rows and selection remain
   coherent.
6. Use Ctrl+A in filtered, collapsed, active, and inactive views. Only visible mod rows should be
   selected, and Redux must remain responsive.
7. Drag individual and multiple selected mods within a list and between active and inactive lists.
   Save the order, restart Redux, and confirm the order, categories, separators, and collapsed states
   persist.
8. Import a representative PAK and supported archive. Confirm the preview and destination before
   accepting changes. Export and re-import a Redux Modlist, then review a restore point without
   overwriting the game's order unexpectedly.
9. Check Redux Dark, Redux Light, Parchment, the available text sizes, and any display scale used on
   the test machine.

## Stop conditions

Stop that test path and preserve the current log if Redux freezes, crashes, loses a mod, changes
unrelated load-order rows, writes an unexpected game order, or cannot reopen the same collection.
Do not keep rearranging the list after an unexplained ordering change; the first clear reproduction
is more useful than a later, more complicated state.

The candidate is ready for a wider test only when the core pass completes without a crash, freeze,
data loss, unintended load-order movement, or persistence failure. Current alpha limitations remain
listed in the main README and do not by themselves fail the candidate.

## Reporting a problem

Use the [issue tracker](https://github.com/circleainn/BG3ModManager-Redux/issues) and include:

- `0.1.0-alpha.10`, Windows version, display scale, and approximate active/inactive mod counts;
- whether **Reduce motion** was enabled;
- the shortest repeatable steps and what happened instead of the expected result;
- the latest relevant Redux log plus a screenshot or short recording when useful; and
- whether the problem also occurs after restarting Redux from the clean test folder.

Never post API keys, access tokens, save files, or private filesystem paths. Review logs before
sharing them publicly.
