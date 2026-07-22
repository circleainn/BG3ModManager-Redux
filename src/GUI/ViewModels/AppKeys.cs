

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
	[MenuSettings("File", "Import Mod...", true)]
	public Hotkey ImportMod { get; private set; } = new Hotkey(Key.O, ModifierKeys.Control);

	[MenuSettings("File", "Add New Order", true)]
	public Hotkey NewOrder { get; private set; } = new Hotkey(Key.N, ModifierKeys.Control);

	[MenuSettings("File", "Save Order")]
	public Hotkey Save { get; private set; } = new Hotkey(Key.S, ModifierKeys.Control);

	[MenuSettings("File", "Save Order As...", true)]
	public Hotkey SaveAs { get; private set; } = new Hotkey(Key.S, ModifierKeys.Control | ModifierKeys.Alt);

	[MenuSettings("File", "Import Order from Save...")]
	public Hotkey ImportOrderFromSave { get; private set; } = new Hotkey(Key.I, ModifierKeys.Control);

	[MenuSettings("File", "Import Order from Save As New Order...")]
	public Hotkey ImportOrderFromSaveAsNew { get; private set; } = new Hotkey(Key.I, ModifierKeys.Control | ModifierKeys.Shift);

	[MenuSettings("File", "Import Order from File...")]
	public Hotkey ImportOrderFromFile { get; private set; } = new Hotkey(Key.O, ModifierKeys.Control | ModifierKeys.Shift);

	[MenuSettings("File", "Import Order & Mods from Archive...", true)]
	public Hotkey ImportOrderFromZipFile { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("File", "Export Order to Game")]
	public Hotkey ExportOrderToGame { get; private set; } = new Hotkey(Key.E, ModifierKeys.Control);

	[MenuSettings("File", "Export Order to Text File...")]
	public Hotkey ExportOrderToList { get; private set; } = new Hotkey(Key.E, ModifierKeys.Control | ModifierKeys.Shift);

	[MenuSettings("File", "Export Order to Archive (.zip)")]
	public Hotkey ExportOrderToZip { get; private set; } = new Hotkey(Key.R, ModifierKeys.Control);

	[MenuSettings("File", "Export Order to Archive As...", true)]
	public Hotkey ExportOrderToArchiveAs { get; private set; } = new Hotkey(Key.R, ModifierKeys.Control | ModifierKeys.Shift);

	[MenuSettings("File", "Reload All")]
	public Hotkey Refresh { get; private set; } = new Hotkey(Key.F5);

	//[MenuSettings("File", "Refresh Mod Updates")]
	public Hotkey RefreshModUpdates { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Edit", "Moved Selected Mods to Opposite List", true)]
	public Hotkey Confirm { get; private set; } = new Hotkey(Key.Enter);

	[MenuSettings("Edit", "Focus Active Mods List")]
	public Hotkey MoveFocusLeft { get; private set; } = new Hotkey(Key.Left);

	[MenuSettings("Edit", "Focus Inactive Mods List")]
	public Hotkey MoveFocusRight { get; private set; } = new Hotkey(Key.Right);

	[MenuSettings("Edit", "Go to Other List")]
	public Hotkey SwapListFocus { get; private set; } = new Hotkey(Key.Tab);

	[MenuSettings("Edit", "Move to Top of Active List")]
	public Hotkey MoveToTop { get; private set; } = new Hotkey(Key.PageUp, ModifierKeys.Control);

	[MenuSettings("Edit", "Move to Bottom of Active List", true)]
	public Hotkey MoveToBottom { get; private set; } = new Hotkey(Key.PageDown, ModifierKeys.Control);

	[MenuSettings("Edit", "Toggle Focus Filter for Current List", AddSeparator = true)]
	public Hotkey ToggleFilterFocus { get; private set; } = new Hotkey(Key.F, ModifierKeys.Control);

	public Hotkey ToggleFileNameDisplay { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Edit", "Delete Selected Mods...", AddSeparator = true)]
	public Hotkey DeleteSelectedMods { get; private set; } = new Hotkey(Key.Delete);

	[MenuSettings("Settings", "Open Preferences")]
	public Hotkey OpenPreferences { get; private set; } = new Hotkey(Key.P, ModifierKeys.Control);

	[MenuSettings("Settings", "Open Keyboard Shortcuts")]
	public Hotkey OpenKeybindings { get; private set; } = new Hotkey(Key.K, ModifierKeys.Control);

	[MenuSettings("Settings", "Cycle Theme")]
	public Hotkey ToggleViewTheme { get; private set; } = new Hotkey(Key.L, ModifierKeys.Control);

	//[MenuSettings("View", "Toggle Updates View")]
	public Hotkey ToggleUpdatesView { get; private set; } = new Hotkey();

	[MenuSettings("Go", "Open Mods Folder")]
	public Hotkey OpenModsFolder { get; private set; } = new Hotkey(Key.D1, ModifierKeys.Control);

	[MenuSettings("Go", "Open Game Folder")]
	public Hotkey OpenGameFolder { get; private set; } = new Hotkey(Key.D2, ModifierKeys.Control);

	[MenuSettings("Go", "Open Extender Logs Folder")]
	public Hotkey OpenLogsFolder { get; private set; } = new Hotkey(Key.D4, ModifierKeys.Control);

	[MenuSettings("Go", "Launch Game")]
	public Hotkey LaunchGame { get; private set; } = new Hotkey(Key.G, ModifierKeys.Control | ModifierKeys.Shift);

	[MenuSettings("Tools", "Extract Selected Mods To...")]
	public Hotkey ExtractSelectedMods { get; private set; } = new Hotkey(Key.OemPeriod, ModifierKeys.Control);

	[MenuSettings("Tools", "Extract Active Adventure Mod To...")]
	public Hotkey ExtractSelectedAdventure { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Tools", "Toggle Version Generator Window", Tooltip = "A tool for mod authors to generate version numbers for a mod's meta.lsx")]
	public Hotkey ToggleVersionGeneratorWindow { get; private set; } = new Hotkey(Key.G, ModifierKeys.Control);

	[MenuSettings("Tools", "Download & Extract the Script Extender...")]
	public Hotkey DownloadScriptExtender { get; private set; } = new Hotkey(Key.T, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt);

	[MenuSettings("Accessibility", "Speak Active Order")]
	public Hotkey SpeakActiveModOrder { get; private set; } = new Hotkey(Key.Home, ModifierKeys.Control);

	[MenuSettings("Accessibility", "Stop Speaking")]
	public Hotkey StopSpeaking { get; private set; } = new Hotkey(Key.Home, ModifierKeys.Control | ModifierKeys.Alt);

	[MenuSettings("Help", "Check for Updates")]
	public Hotkey CheckForUpdates { get; private set; } = new Hotkey(Key.F7);

	[MenuSettings("Help", "Open Donation Page (Ko-fi)...", Tooltip = "Open https://ko-fi.com/laughingleader to send a tip to the developer")]
	public Hotkey OpenDonationLink { get; private set; } = new Hotkey(Key.None);

	[MenuSettings("Help", "About")]
	public Hotkey OpenAboutWindow { get; private set; } = new Hotkey(Key.F1);

	[MenuSettings("Help", "Open Home Page (Github)...")]
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
			File.WriteAllText(filePath, contents);
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
			if (keyMapDict.Count > 0)
			{
				string contents = JsonConvert.SerializeObject(keyMapDict, Newtonsoft.Json.Formatting.Indented);
				File.WriteAllText(filePath, contents);
			}
			else
			{
				File.WriteAllText(filePath, "{}");
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
		// Building a list of keys / key names from properties, because lazy
		var keyProps = t.GetRuntimeProperties().Where(prop => Attribute.IsDefined(prop, typeof(MenuSettingsAttribute)) && prop.GetGetMethod() != null).ToList();
		foreach (var prop in keyProps)
		{
			var hotkey = (Hotkey)t.GetProperty(prop.Name).GetValue(this);
			hotkey.AddCanExecuteCondition(baseCanExecute);
			hotkey.ID = prop.Name;
			keyMap.AddOrUpdate(hotkey);
		}
	}
}
