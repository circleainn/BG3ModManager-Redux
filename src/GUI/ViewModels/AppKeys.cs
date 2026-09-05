

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

	[MenuSettings("File", "Import Mod...", true)]
	public Hotkey ImportMod { get; private set; } = new Hotkey(Key.O, ModifierKeys.Control);

	[MenuSettings("File", "Save Current Order")]
	public Hotkey Save { get; private set; } = new Hotkey(Key.S, ModifierKeys.Control);

	[MenuSettings("File", "Save Load Order to File...")]
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

	[MenuSettings("File", "Export Load Order to Game")]
	public Hotkey ExportOrderToGame { get; private set; } = new Hotkey(Key.E, ModifierKeys.Control);

	[MenuSettings("File", "Export Load Order to Text File...")]
	public Hotkey ExportOrderToList { get; private set; } = new Hotkey(Key.E, ModifierKeys.Control | ModifierKeys.Shift);

	[MenuSettings(
		"File",
		"Export Redux Modlist...",
		false,
		"Save the active load order and optional categories, separators, icons, and notes to a .bg3redux file.")]
	public Hotkey ExportReduxLoadOrder { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("File", "Back Up Active Mods to ZIP...")]
	public Hotkey ExportOrderToZip { get; private set; } = new Hotkey(Key.R, ModifierKeys.Control);

	[MenuSettings("File", "Refresh Mods")]
	public Hotkey Refresh { get; private set; } = new Hotkey(Key.F5);

	[MenuSettings("File", "Refresh Mod Updates")]
	public Hotkey RefreshModUpdates { get; private set; } = new Hotkey(Key.None);

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

	[MenuSettings("Settings", "Preferences...")]
	public Hotkey OpenPreferences { get; private set; } = new Hotkey(Key.P, ModifierKeys.Control);

	[MenuSettings("Settings", "Keyboard Shortcuts...", true)]
	public Hotkey OpenKeybindings { get; private set; } = new Hotkey(Key.K, ModifierKeys.Control);

	[MenuSettings(
		"Shortcuts",
		"Quick Access...",
		false,
		"Find an action, mod, profile, order, or category.")]
	public Hotkey OpenCommandPalette { get; private set; } =
		new Hotkey(Key.Q, ModifierKeys.Control);

	[MenuSettings("Settings", "Change Theme")]
	public Hotkey ToggleViewTheme { get; private set; } = new Hotkey(Key.L, ModifierKeys.Control);

	[MenuSettings("Settings", "Show or Hide Toolbar")]
	public Hotkey ToggleToolbar { get; private set; } = new Hotkey(Key.T, ModifierKeys.Control);

	[MenuSettings("Settings", "Show or Hide Updates")]
	public Hotkey ToggleUpdatesView { get; private set; } = new Hotkey();

	[MenuSettings("Go", "Open Mods Folder")]
	public Hotkey OpenModsFolder { get; private set; } = new Hotkey(Key.D1, ModifierKeys.Control);

	[MenuSettings("Go", "Open Game Folder")]
	public Hotkey OpenGameFolder { get; private set; } = new Hotkey(Key.D2, ModifierKeys.Control);

	[MenuSettings("Go", "Open Script Extender Logs Folder")]
	public Hotkey OpenLogsFolder { get; private set; } = new Hotkey(Key.D4, ModifierKeys.Control);

	[MenuSettings("Go", "Launch Game")]
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

	[MenuSettings("Tools", "Install Script Extender...")]
	public Hotkey DownloadScriptExtender { get; private set; } = new Hotkey(Key.T, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt);

	[MenuSettings("Accessibility", "Read Active Load Order Aloud")]
	public Hotkey SpeakActiveModOrder { get; private set; } = new Hotkey(Key.Home, ModifierKeys.Control);

	[MenuSettings("Accessibility", "Stop Reading Load Order")]
	public Hotkey StopSpeaking { get; private set; } = new Hotkey(Key.Home, ModifierKeys.Control | ModifierKeys.Alt);

	[MenuSettings("Help", "Check for Updates")]
	public Hotkey CheckForUpdates { get; private set; } = new Hotkey(Key.F7);

	[MenuSettings("Help", "Open Donation Page (Ko-fi)...", Tooltip = "Open https://ko-fi.com/laughingleader to send a tip to the developer")]
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
			hotkey.Category = ShortcutCategoryNames.TryGetValue(menuSettings.Parent, out var categoryName)
				? categoryName
				: menuSettings.Parent;
			keyMap.AddOrUpdate(hotkey);
		}
	}
}
