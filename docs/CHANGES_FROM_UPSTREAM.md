# Changes from upstream BG3ModManager

Redux is a fork of [LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager).
The core mod-management engine — load order, profiles, import/export, `.pak` parsing, and game
launch — is unchanged. Everything else has been rebuilt.

## Theming, typography, and icons

- Redux Dark, Redux Light, and Parchment themes, each with its own palette and accent color, built
  on a shared semantic color system (accent, success, warning, error, info, disabled) so every
  control looks consistent regardless of theme.
- Custom themes: pick a built-in theme as a base, edit colors live, duplicate, rename, delete, or
  share via JSON import/export. Redux derives hover/pressed/selection states automatically.
- A typography selector (Manrope, Segoe UI, Atkinson Hyperlegible, and others) with Compact,
  Default, and Large text-size presets, persisted per custom theme.
- A local font library for `.ttf`/`.otf` files up to 10MB, with live preview and safe deletion.
  Falls back to Manrope automatically if a font can't load.
- A shared vector icon system (Ionicons, one Tabler icon) across toolbar buttons, context menus,
  status badges, categories, separators, and dialogs, replacing the old bitmap icons. Legacy
  bitmaps remain only for Nexus/mod.io branding.
- Custom PNG icons for categories and separators, with optional color tinting.
- A rebuilt dialog system replacing the third-party Xceed message boxes, themed consistently
  instead of ignoring the active theme.
- Grouped command toolbar (Install/Profile, Load Order, Order Actions, Export, Campaign, Tools,
  Launch), an Open menu for common folders and links, and a general pass on rounded cards, spacing,
  and hover/pressed states.

## Categories and organization

The original manager had no persistent category system. Redux adds:

- Automatic and custom categories with best-effort auto-assignment, custom colors, curated icons or
  uploaded PNGs, and a full category editor (hue picker, RGB sliders, hex input, saved presets).
- A curated set of default categories (Gameplay, Classes, Companions, Quests, Weapons, Visuals,
  Overhauls, Patches, and more) with sensible default icons, each individually customizable and
  resettable. A dedicated "No Category" fallback replaces the old generic uncategorized label.
- Multiple categories per mod, category filtering that doesn't touch the load order, category
  counts, new-mod indicators, and draggable category ordering. Reordering the sidebar never changes
  which category wins during auto-classification.
- Visual separators inside the active list — named, colored, iconable, collapsible, drag-positioned.
  Separators are display-only: never written to `modsettings.lsx`, never exported, hidden wherever
  their position would be misleading.

## Mod metadata and diagnostics

- Nexus Mods and mod.io provider linking, with titles, authors, versions, descriptions,
  requirements, files, and changelogs shown through a resizable details drawer and a hover card.
- A bundled offline database that identifies known Nexus `.pak` files and archives by exact size and
  hash, without needing an API call (see [REDUX_MOD_DATABASE.md](REDUX_MOD_DATABASE.md)).
- Manual Nexus project linking for mods that can't be auto-matched.
- A mod.io safety warning explaining that BG3's in-game manager can restore subscribed content
  after Redux deletes the local file, with an acknowledgement flow.
- Clearer Script Extender status (supported, required and satisfied, missing, wrong version,
  missing updater, disabled), plus dedicated Osiris scripting and Mod Fixer detection.
- Override Mods and Always Loaded categorization for packages that replace built-in game files
  outside the normal load order, shown separately with proper context instead of mixed into the
  regular mod list.
- A read-only Mod Health foundation flagging missing/inactive dependencies, duplicate or invalid
  UUIDs, Script Extender requirements, declared conflicts, and mod.io safety state. Nothing is
  auto-repaired, auto-installed, or auto-reordered — the full Health tray and Load Order Advisor
  are still in progress.
- A redesigned notification system with consistent colors, icons, and animation for
  success/warning/error/info messages.

## File safety

- Atomic `settings.json` writes: write to a temp file, validate, then swap in, with a rolling
  backup. No more partial writes from an interrupted save.
- Atomic `modsettings.lsx` export using the same pattern, so a failed export can't overwrite a
  working file with garbage.
- Staged package imports — files are copied to a temp location and validated before being treated
  as installed, with a backup taken before an update replaces an existing package.
- Deletion that distinguishes recoverable vs. permanent vs. failed, and only updates the UI once
  the filesystem operation actually succeeds.

## Bugs fixed from the legacy issue tracker

Since Redux shares most of the original manager's core workflows, open issues on the upstream
tracker were audited against the current code rather than assumed fixed. Full tracking is in
[issue #11](https://github.com/raincloudsfollow/BG3ModManager-Redux/issues/11). Fixed so far:

- **Large archive imports failing** (#383) — `ImportArchiveAsync` read the whole file into a
  throwaway buffer before parsing it, and the length cast overflowed past ~2.1GB. Removed.
- **Save/export doing nothing with no error** (#464) — now shows an alert instead of failing
  silently when no profile or load order is selected.
- **Blank profile dropdown on a clean install** (#385) — profile loading returned `null` instead of
  an empty list, which crashed the startup pipeline downstream. Also added a message explaining
  that BG3 needs to run once before a profile exists.
- **Saved load orders disappearing after reopening** (#466) — saves were going to a different
  folder than the one scanned back in on launch.
- **Active/Inactive drag-and-drop breaking permanently** (#448, #463) — an exception mid-load could
  leave the list locked until restart. Now always resets properly.
- **Missing-`.pak` mods stuck in the inactive list** (#346) — the delete button required the file
  to still exist, blocking removal of exactly the entries that needed cleaning up.
- **Wrong Script Extender version reported** (#470) — version comparison only checked the major
  version number, so a newer build could lose to an older one with the same major version.
- **Active mods reverting after Refresh** (#390) — Refresh always rebuilt the list from disk with
  no warning, discarding unsaved changes. Now asks first.
- **App closing right after the splash screen with no explanation** (#471, #440) — there was no
  exception handling at all during early startup. Added a safety net so failures are visible.

## Still open

- Manager-launched game crashes that don't happen via Steam (#456) — caused by the
  `steam_appid.txt` launcher bypass. Confirmed, but low priority for now.
- "Extension not found" errors (#461) — likely a Script Extender runtime message, not something in
  the manager's own code.
- Localization (#475) — not started. Worth doing eventually since Redux has added a lot of
  hardcoded strings.
- Linux/Wine — not supported and not planned.
