

using DivinityModManager.Models.App;
using DivinityModManager.Util;

using DynamicData;

using Newtonsoft.Json;

using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;

namespace DivinityModManager.ViewModels;

public class AppKeys : ReactiveObject
{
	private string _lastSavedKeybindingsContents;

	private static readonly IReadOnlyDictionary<string, string> ShortcutCategoryNames =
		new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["File"] = "Load orders and files",
			["Edit"] = "Mod lists",
			["Settings"] = "Settings and appearance",
			["Go"] = "Folders and launch",
			["Tools"] = "Tools",
			["Accessibility"] = "Accessibility",
			["Help"] = "Help and updates"
		};

	[MenuSettings("File", "Install Mod...", true, "Install a mod from a supported package or archive.")]
	public Hotkey ImportMod { get; private set; } = new Hotkey(Key.O, ModifierKeys.Control);

	[MenuSettings("File", "Save Current Order", false, "Save changes to the selected load order.")]
	public Hotkey Save { get; private set; } = new Hotkey(Key.S, ModifierKeys.Control);

	[MenuSettings("File", "Save Load Order to File...", false, "Save the current order as a separate load-order file.")]
	public Hotkey SaveAs { get; private set; } = new Hotkey(Key.S, ModifierKeys.Control | ModifierKeys.Alt);

	[MenuSettings("File", "Save as New Load Order...")]
	public Hotkey SaveNewOrder { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("File", "Create Blank Load Order")]
	public Hotkey NewOrder { get; private set; } = new Hotkey(Key.N, ModifierKeys.Control);

	[MenuSettings("File", "Rename Load Order...")]
	public Hotkey RenameOrder { get; private set; } = new Hotkey(Key.None);

	[MenuSettings(
		"File",
		"Compare Load Orders...",
		false,
		"Compare two available load orders without changing either one.")]
	public Hotkey CompareLoadOrders { get; private set; } = new Hotkey(Key.None);

	[MenuSettings(
		"Tools",
		"Organize Active Load Order...",
		false,
		"Preview advisor-guided ordering while preserving, replacing, or removing separators.")]
	public Hotkey OrganizeLoadOrder { get; private set; } = new Hotkey(Key.None);

	[MenuSettings(
		"File",
		"Load Order History...",
		true,
		"Review, compare, capture, or load snapshots for the current profile.")]
	public Hotkey RestorePoints { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("File", "Import Load Order from Save...")]
	public Hotkey ImportOrderFromSave { get; private set; } = new Hotkey(Key.I, ModifierKeys.Control);

	[MenuSettings("File", "Import Save as New Load Order...")]
	public Hotkey ImportOrderFromSaveAsNew { get; private set; } = new Hotkey(Key.I, ModifierKeys.Control | ModifierKeys.Shift);

	[MenuSettings("File", "Import Load Order from File...")]
	public Hotkey ImportOrderFromFile { get; private set; } = new Hotkey(Key.O, ModifierKeys.Control | ModifierKeys.Shift);

	[MenuSettings(
		"File",
		"Import Redux Modlist...",
		false,
		"Import a .bg3redux modlist with optional categories, separators, icons, and notes.")]
	public Hotkey ImportReduxLoadOrder { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("File", "Import Load Order and Mods from Archive...", true)]
	public Hotkey ImportOrderFromZipFile { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("File", "Sync Load Order to Game")]
	public Hotkey ExportOrderToGame { get; private set; } = new Hotkey(Key.E, ModifierKeys.Control);

	[MenuSettings(
		"File",
		"Export Detailed Mod List...",
		false,
		"Save filenames, authors, dependencies, source links, and Override mods as TSV, text, or JSON.")]
	public Hotkey ExportOrderToList { get; private set; } = new Hotkey(Key.E, ModifierKeys.Control | ModifierKeys.Shift);

	[MenuSettings(
		"File",
		"Export Redux Modlist...",
		false,
		"Save the active load order and optional categories, separators, icons, and notes to a .bg3redux file.")]
	public Hotkey ExportReduxLoadOrder { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("File", "Back Up Active Mods to ZIP...")]
	public Hotkey ExportOrderToZip { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("File", "Refresh Mods", false, "Rescan the configured Mods folder and refresh the mod lists.")]
	public Hotkey Refresh { get; private set; } = new Hotkey(Key.F5);

	[MenuSettings("File", "Refresh Mod Updates")]
	public Hotkey RefreshModUpdates { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Edit", "Undo Last Action", false,
		"Undo the last activation, deactivation, reorder, separator, or advisor organization change.")]
	public Hotkey UndoLoadOrderChange { get; private set; } = new Hotkey(Key.Z, ModifierKeys.Control);

	[MenuSettings("Edit", "Redo Last Action", true,
		"Redo the last activation, deactivation, reorder, separator, or advisor organization change.")]
	public Hotkey RedoLoadOrderChange { get; private set; } = new Hotkey(Key.Y, ModifierKeys.Control);

	[MenuSettings("Edit", "Move Selected Mods to Other List", true)]
	public Hotkey Confirm { get; private set; } = new Hotkey(Key.Enter);

	[MenuSettings("Edit", "Focus Active Mods List")]
	public Hotkey MoveFocusLeft { get; private set; } = new Hotkey(Key.Left);

	[MenuSettings("Edit", "Focus Inactive Mods List")]
	public Hotkey MoveFocusRight { get; private set; } = new Hotkey(Key.Right);

	[MenuSettings("Edit", "Switch Between Mod Lists")]
	public Hotkey SwapListFocus { get; private set; } = new Hotkey(Key.Tab);

	[MenuSettings("Edit", "Move to Top of Active List")]
	public Hotkey MoveToTop { get; private set; } = new Hotkey(Key.PageUp, ModifierKeys.Control);

	[MenuSettings("Edit", "Move to Bottom of Active List", true)]
	public Hotkey MoveToBottom { get; private set; } = new Hotkey(Key.PageDown, ModifierKeys.Control);

	[MenuSettings("Edit", "Focus Current Mod-List Filter", AddSeparator = true)]
	public Hotkey ToggleFilterFocus { get; private set; } = new Hotkey(Key.F, ModifierKeys.Control);

	[MenuSettings("Edit", "Delete Selected Mods...", AddSeparator = true)]
	public Hotkey DeleteSelectedMods { get; private set; } = new Hotkey(Key.Delete);

	[MenuSettings(
		"Edit",
		"Expand or Collapse All Active Separators",
		false,
		"Toggle every separator in the active load order between expanded and collapsed.")]
	public Hotkey ToggleAllActiveSeparators { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Settings", "Preferences...")]
	public Hotkey OpenPreferences { get; private set; } = new Hotkey(Key.P, ModifierKeys.Control);

	[MenuSettings("Settings", "Theme & Appearance...", false,
		"Open Preferences directly to theme, typography, and interface appearance settings.")]
	public Hotkey OpenThemeAppearance { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Settings", "Keyboard Shortcuts...", true)]
	public Hotkey OpenKeybindings { get; private set; } = new Hotkey(Key.K, ModifierKeys.Control);

	[MenuSettings(
		"Tools",
		"Quick Access...",
		false,
		"Find an action, mod, profile, order, or category.")]
	public Hotkey OpenCommandPalette { get; private set; } =
		new Hotkey(Key.Q, ModifierKeys.Control);

	[MenuSettings("Settings", "Cycle Theme", false,
		"Switch to the next built-in or custom theme.")]
	public Hotkey ToggleViewTheme { get; private set; } = new Hotkey(Key.L, ModifierKeys.Control);

	[MenuSettings("Settings", "Show or Hide Toolbar")]
	public Hotkey ToggleToolbar { get; private set; } = new Hotkey(Key.T, ModifierKeys.Control);

	[MenuSettings("Tools", "Show or Hide Mod Updates")]
	public Hotkey ToggleUpdatesView { get; private set; } = new Hotkey();

	[MenuSettings(
		"Tools",
		"Save Game Manager...",
		false,
		"Browse, install, and safely remove story saves for the selected profile.")]
	public Hotkey OpenSaveGameManager { get; private set; } = new Hotkey(Key.None);

	[MenuSettings(
		"Tools",
		"Game-Directory Mod Manager...",
		false,
		"Install, review, and safely remove supported native or root-level BG3 mods outside the PAK load order.")]
	public Hotkey OpenGameDirectoryModManager { get; private set; } = new Hotkey(Key.None);

	[MenuSettings(
		"Tools",
		"Download Manager...",
		false,
		"Inspect and install local packages or files received through Nexus Mod Manager Download links.")]
	public Hotkey OpenNexusDownloads { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Go", "Open Mods Folder", false, "Open the configured Baldur's Gate 3 Mods folder.")]
	public Hotkey OpenModsFolder { get; private set; } = new Hotkey(Key.D1, ModifierKeys.Control);

	[MenuSettings("Go", "Open Game Folder")]
	public Hotkey OpenGameFolder { get; private set; } = new Hotkey(Key.D2, ModifierKeys.Control);

	[MenuSettings("Go", "Open Script Extender Logs Folder", false, "Open the folder containing Script Extender logs.")]
	public Hotkey OpenLogsFolder { get; private set; } = new Hotkey(Key.D3, ModifierKeys.Control);

	[MenuSettings("Go", "Open Save Games Folder", false, "Open the selected player profile’s save games folder.")]
	public Hotkey OpenSaveGamesFolder { get; private set; } = new Hotkey(Key.D4, ModifierKeys.Control);

	[MenuSettings("Go", "Launch Game", false, "Launch Baldur's Gate 3 using the configured launch method.")]
	public Hotkey LaunchGame { get; private set; } = new Hotkey(Key.G, ModifierKeys.Control | ModifierKeys.Shift);

	[MenuSettings("Tools", "Extract Selected Mods to...")]
	public Hotkey ExtractSelectedMods { get; private set; } = new Hotkey(Key.OemPeriod, ModifierKeys.Control);

	[MenuSettings("Tools", "Extract Active Adventure Mod to...", true)]
	public Hotkey ExtractSelectedAdventure { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Tools", "Open Version Generator", Tooltip = "A tool for mod authors to generate version numbers for a mod's meta.lsx")]
	public Hotkey ToggleVersionGeneratorWindow { get; private set; } = new Hotkey(Key.G, ModifierKeys.Control);

	[MenuSettings(
		"Tools",
		"Inspect Active File Overlaps...",
		false,
		"Read active and override PAK file tables to find shared internal paths. Overlaps are not necessarily conflicts.")]
	public Hotkey InspectFileOverlaps { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Tools", "Manage Script Extender...", Tooltip = "Open Script Extender in the Game-directory Mod Manager.")]
	public Hotkey DownloadScriptExtender { get; private set; } = new Hotkey(Key.T, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt);

	[MenuSettings("Accessibility", "Read Active Load Order Aloud")]
	public Hotkey SpeakActiveModOrder { get; private set; } = new Hotkey(Key.Home, ModifierKeys.Control);

	[MenuSettings("Accessibility", "Stop Reading Load Order")]
	public Hotkey StopSpeaking { get; private set; } = new Hotkey(Key.Home, ModifierKeys.Control | ModifierKeys.Alt);

	[MenuSettings("Help", "Check for Updates")]
	public Hotkey CheckForUpdates { get; private set; } = new Hotkey(Key.F7);

	[MenuSettings("Help", "Support circleain on Ko-fi...", Tooltip = "Support BG3 Mod Manager Redux at https://ko-fi.com/circleain")]
	public Hotkey OpenReduxDonationLink { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Help", "Support LaughingLeader on Ko-fi...", Tooltip = "Support the original BG3 Mod Manager developer at https://ko-fi.com/laughingleader")]
	public Hotkey OpenDonationLink { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Help", "About")]
	public Hotkey OpenAboutWindow { get; private set; } = new Hotkey(Key.F1);

	[MenuSettings("Help", "Open Redux on GitHub...")]
	public Hotkey OpenRepositoryPage { get; private set; } = new Hotkey(Key.None);

	private readonly SourceCache<Hotkey, string> keyMap = new((hk) => hk.ID);

	protected readonly ReadOnlyObservableCollection<Hotkey> allKeys;
	public ReadOnlyObservableCollection<Hotkey> All => allKeys;

	public void SaveDefaultKeybindings()
	{
		string filePath = DivinityApp.GetAppDirectory("Data", "keybindings-default.json");
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(filePath));
			var keyMapDict = new Dictionary<string, Hotkey>();
			foreach (var key in All)
			{
				keyMapDict.Add(key.ID, key);
			}
			string contents = JsonConvert.SerializeObject(keyMapDict, Newtonsoft.Json.Formatting.Indented);
			AtomicFileWriter.WriteAllText(filePath, contents, validateTemporaryFile: temporaryPath =>
				JsonConvert.DeserializeObject<Dictionary<string, Hotkey>>(File.ReadAllText(temporaryPath)) != null);
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error saving default keybindings at '{filePath}': {ex}");
		}
	}

	public bool SaveKeybindings(out string result)
	{
		result = "";
		var filePath = DivinityApp.GetAppDirectory("Data", "keybindings.json");
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(filePath));
			var keyMapDict = new Dictionary<string, Hotkey>();
			foreach (var key in All)
			{
				if (!key.IsDefault)
				{
					keyMapDict.Add(key.ID, key);
				}
			}
			var contents = keyMapDict.Count > 0
				? JsonConvert.SerializeObject(keyMapDict, Newtonsoft.Json.Formatting.Indented)
				: "{}";
			if (!File.Exists(filePath) || !String.Equals(contents, _lastSavedKeybindingsContents, StringComparison.Ordinal))
			{
				AtomicFileWriter.WriteAllText(filePath, contents, filePath + ".bak", temporaryPath =>
					JsonConvert.DeserializeObject<Dictionary<string, Hotkey>>(File.ReadAllText(temporaryPath)) != null);
				_lastSavedKeybindingsContents = contents;
			}
			result = $"Saved keybindings to '{filePath}'";
			return true;
		}
		catch (Exception ex)
		{
			result = $"Error saving keybindings at '{filePath}': {ex}";
		}
		return false;
	}

	public bool LoadKeybindings(MainWindowViewModel vm)
	{
		var filePath = DivinityApp.GetAppDirectory("Data", "keybindings.json");
		try
		{
			if (DivinityJsonUtils.TrySafeDeserializeFromPath<Dictionary<string, Hotkey>>(filePath, out var allKeybindings))
			{
				foreach (var kvp in allKeybindings)
				{
					var existingHotkey = All.FirstOrDefault(x => x.ID.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));
					if (existingHotkey != null)
					{
						existingHotkey.Key = kvp.Value.Key;
						existingHotkey.Modifiers = kvp.Value.Modifiers;
						existingHotkey.UpdateDisplayBindingText();
					}
				}
				return true;
			}
		}
		catch (Exception ex)
		{
			vm.ShowAlert($"Error loading keybindings at '{filePath}': {ex}", AlertType.Danger);
		}
		return false;
	}

	public void SetToDefault()
	{
		foreach (var entry in keyMap.Items)
		{
			entry.ResetToDefault();
		}
	}

	public AppKeys(MainWindowViewModel vm)
	{
		keyMap.Connect().Bind(out allKeys).Subscribe();
		var baseCanExecute = vm.WhenAnyValue(x => x.IsLocked, b => !b);
		Type t = typeof(AppKeys);
		// Every public Hotkey is user-configurable. Requiring menu metadata here prevents
		// a newly registered command from silently disappearing from the shortcut editor.
		var keyProps = t.GetRuntimeProperties()
			.Where(prop => prop.PropertyType == typeof(Hotkey) && prop.GetGetMethod() != null)
			.OrderBy(prop => prop.MetadataToken)
			.ToList();
		foreach (var prop in keyProps)
		{
			var hotkey = (Hotkey)t.GetProperty(prop.Name).GetValue(this);
			var menuSettings = prop.GetCustomAttribute<MenuSettingsAttribute>()
				?? throw new InvalidOperationException(
					$"{nameof(AppKeys)}.{prop.Name} must declare {nameof(MenuSettingsAttribute)} so it can appear in Keyboard Shortcuts.");
			hotkey.AddCanExecuteCondition(baseCanExecute);
			hotkey.ID = prop.Name;
			hotkey.DisplayName = menuSettings.DisplayName;
			hotkey.Description = menuSettings.Tooltip;
			hotkey.Category = ShortcutCategoryNames.TryGetValue(menuSettings.Parent, out var categoryName)
				? categoryName
				: menuSettings.Parent;
			keyMap.AddOrUpdate(hotkey);
		}
	}
}
