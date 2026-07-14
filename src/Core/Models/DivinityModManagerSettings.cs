using DivinityModManager.Extensions;
using DivinityModManager.Models.App;
using DivinityModManager.Models.Extender;
using DivinityModManager.Util;

using DynamicData;
using DynamicData.Binding;

using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;

namespace DivinityModManager.Models;

public enum ReduxThemeType
{
	[Description("Redux Dark")]
	ReduxDark = 1,
	[Description("Redux Light")]
	ReduxLight = 2,
	[Description("Parchment")]
	Parchment = 3
}

[DataContract]
public class DivinityModManagerSettings : ReactiveObject
{
	[SettingsEntry("Game Data Path", "The path to the Data folder, for loading editor mods.\nExample: Baldur's Gate 3/Data")]
	[DataMember, Reactive] public string GameDataPath { get; set; }

	[SettingsEntry("Game Executable Path", "The path to bg3.exe")]
	[DataMember, Reactive] public string GameExecutablePath { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("DirectX 11", "If enabled, when launching the game, bg3_dx11.exe is used instead")]
	[DataMember, Reactive] public bool LaunchDX11 { get; set; }

	[DefaultValue("")]
	// REDUX RELEASE BLOCKER: Before public release, register with Nexus Mods, obtain an SSO application slug,
	// replace the personal API key testing flow with browser SSO, and re-review API usage/rate limits.
	[SettingsEntry("Nexus Mods API Key", "Personal API key used to retrieve Nexus Mods metadata and updates. This testing build stores the key locally in Data/settings.json. You can revoke the key from your Nexus Mods account at any time.")]
	[DataMember, Reactive] public string NexusModsAPIKey { get; set; }

	[DefaultValue("")]
	[SettingsEntry("mod.io API Key", "Read-only API key used to retrieve metadata for mods installed through BG3's in-game Mod Manager. The key is stored locally in Data/settings.json.")]
	[DataMember, Reactive] public string ModioAPIKey { get; set; }

	[DefaultValue(false)]
	[DataMember, Reactive] public bool ModioSupportWarningAcknowledged { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Hide mod.io source warning icons", "Hide the amber warning icon beside mod.io sources. Subscribed mod.io mods may still be restored or redownloaded by BG3 after local deletion.")]
	[DataMember, Reactive] public bool HideModioSourceWarningIcons { get; set; }

	[SettingsEntry("Reset mod.io warning acknowledgement", "Clear the saved acknowledgement so the mandatory mod.io safety warning appears again on the next metadata refresh or application launch.")]
	[Reactive] public bool ResetModioSupportWarningAcknowledgement { get; set; }

	[DefaultValue(false)]
	[DataMember, Reactive] public bool OfflineNexusDatabaseWarningAcknowledged { get; set; }

	[SettingsEntry("Show offline Nexus database notice again", "Clear the saved acknowledgement so the offline Nexus metadata notice appears again when Redux starts without a Nexus API key or uses an exact bundled .pak match.")]
	[Reactive] public bool ResetOfflineNexusDatabaseWarningAcknowledgement { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Story Log", "When launching the game, enable the Osiris story log (osiris.log)")]
	[DataMember, Reactive] public bool GameStoryLogEnabled { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Launcher - Disable Telemetry", "Disable the telemetry options in the launcher\nTelemetry is always disabled if mods are active")]
	[DataMember, Reactive] public bool DisableLauncherTelemetry { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Launcher - Disable Warnings", "Disable the mod/data mismatch warnings in the launcher")]
	[DataMember, Reactive] public bool DisableLauncherModWarnings { get; set; }

	[DefaultValue(LaunchGameType.Exe)]
	[SettingsEntry("Launch Game - Action", "Change how to launch the game")]
	[DataMember, Reactive] public LaunchGameType LaunchType { get; set; }

	[DefaultValue("")]
	[SettingsEntry("Launch Game - Custom Action", "A file path, protocol, or custom process shell command to run")]
	[DataMember, Reactive] public string CustomLaunchAction { get; set; }

	[DefaultValue("")]
	[SettingsEntry("Launch Game - Custom Arguments", "Optional additional arguments to path to the custom launch command")]
	[DataMember, Reactive] public string CustomLaunchArgs { get; set; }

	[ObservableAsProperty] public Visibility CustomLaunchVisibility { get; }

	[DefaultValue("Orders")]
	[SettingsEntry("Load Orders Path", "The folder containing mod load order .json files")]
	[DataMember, Reactive] public string LoadOrderPath { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Internal Logging", "Enable the log for the mod manager", HideFromUI = true)]
	[DataMember, Reactive] public bool LogEnabled { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Add Missing Dependencies When Exporting", "Automatically add dependency mods above their dependents in the exported load order, if omitted from the active order")]
	[DataMember, Reactive] public bool AutoAddDependenciesWhenExporting { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Automatically Check For Updates", "Reserved for future Redux releases. Application self-updating is disabled during the private alpha.")]
	[DataMember, Reactive] public bool CheckForUpdates { get; set; }

	[DefaultValue("")]
	[SettingsEntry("Override AppData Path", "[EXPERIMENTAL]\nOverride the default location to %LOCALAPPDATA%\\Larian Studios\\Baldur's Gate 3\nThis folder is used when exporting load orders, loading profiles, and loading mods.")]
	[DataMember, Reactive] public string DocumentsFolderPathOverride { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Colorblind Support", "Enables some colorblind support, such as displaying icons for toolkit projects (which normally have a green background)")]
	[DataMember, Reactive] public bool EnableColorblindSupport { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool DarkThemeEnabled { get; set; }

	[DefaultValue(ReduxThemeType.ReduxDark)]
	[SettingsEntry("Theme", "Choose the Redux color palette. Themes change colors only; layout, typography, icons, and behavior remain shared.", HideFromUI = true)]
	[DataMember, Reactive] public ReduxThemeType ColorTheme { get; set; } = ReduxThemeType.ReduxDark;

	[DefaultValue(false)]
	[DataMember, Reactive] public bool ReduxPreviewWarningAcknowledged { get; set; }

	[SettingsEntry("Show Redux preview warning again", "Clear the saved acknowledgement so the work-in-progress warning appears again the next time Redux starts.")]
	[Reactive] public bool ResetReduxPreviewWarningAcknowledgement { get; set; }

	// Redux mod-list column choices. These are managed from the column-header
	// context menu, so they stay out of the main Settings window.
	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListVersionColumn { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListFileNameColumn { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListAuthorColumn { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListLastUpdatedColumn { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListLastModifiedColumn { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListSourceColumn { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListCategoryColumn { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Hide empty mod categories", "Hide categories with no matching installed mods from the Categories sidebar.")]
	[DataMember, Reactive] public bool HideEmptyModCategories { get; set; }

	[DataMember, Reactive] public List<string> CustomModCategories { get; set; } = new();
	// Redux-only presentation order for the category sidebar. This never changes mod assignments or load order.
	[DataMember, Reactive] public List<string> ModCategoryDisplayOrder { get; set; } = new();
	// Legacy single-category assignments are retained for migration from early Redux builds.
	[DataMember, Reactive] public Dictionary<string, string> ModCategoryOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	[DataMember, Reactive] public Dictionary<string, List<string>> ModCategoryAssignments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	[DataMember, Reactive] public Dictionary<string, string> ModCategoryColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	[DataMember, Reactive] public List<string> SavedCategoryColors { get; set; } = new();
	[DataMember, Reactive] public List<string> DisabledModCategories { get; set; } = new();

	[DefaultValue(false)]
	[DataMember, Reactive] public bool SaveModCategoryFilterBetweenSessions { get; set; }

	[DefaultValue("All Mods")]
	[DataMember, Reactive] public string SavedModCategoryFilter { get; set; } = "All Mods";

	[DefaultValue(false)]
	[DataMember, Reactive] public bool DisableNewModCategoryIndicators { get; set; }
	[DefaultValue(false)]
	[DataMember, Reactive] public bool NewModCategoryIndicatorInitialized { get; set; }
	[DataMember, Reactive] public List<string> KnownCategorizedModIds { get; set; } = new();
	[DataMember, Reactive] public Dictionary<string, List<string>> UnseenCategoryModIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	// Redux visual dividers are presentation-only markers anchored above a real mod UUID.
	// They never enter the load order or exported modsettings data.
	// Retained so settings written by the first anchored-divider prototype still deserialize safely.
	[DataMember, Reactive] public Dictionary<string, string> ModListVisualDividers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	[DataMember, Reactive] public List<ModListVisualDividerData> VisualModListDividers { get; set; } = new();

	[DefaultValue(true)]
	[SettingsEntry("Shift Focus on Swap", "When moving selected mods to the opposite list with Enter, move focus to that list as well")]
	[DataMember, Reactive] public bool ShiftListFocusOnSwap { get; set; }

	[DataMember, IgnoreSetFrom] public ScriptExtenderSettings ExtenderSettings { get; set; }
	[DataMember, IgnoreSetFrom] public ScriptExtenderUpdateConfig ExtenderUpdaterSettings { get; set; }

	[DefaultValue(DivinityGameLaunchWindowAction.None)]
	[SettingsEntry("On Game Launch", "When the game launches through the mod manager, this action will be performed")]
	[DataMember, Reactive]
	public DivinityGameLaunchWindowAction ActionOnGameLaunch { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Skip Checking for Missing Mods", "If a load order is missing mods, no warnings will be displayed")]
	[DataMember, Reactive] public bool DisableMissingModWarnings { get; set; }

	[DefaultValue(false)]
	[Reactive] public bool DisplayFileNames { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Mod Developer Mode", "This enables features for mod developers, such as being able to copy a mod's UUID in context menus, and additional Script Extender options", HideFromUI = true)]
	[Reactive, DataMember] public bool DebugModeEnabled { get; set; }

	[DefaultValue("")]
	[DataMember, Reactive] public string GameLaunchParams { get; set; }

	[DataMember] public WindowSettings Window { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Save Window Location", "Save and restore the window location when the application starts.")]
	[DataMember, Reactive] public bool SaveWindowLocation { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Delete ModCrashSanityCheck", "Automatically delete the %LOCALAPPDATA%/Larian Studios/Baldur's Gate 3/ModCrashSanityCheck folder, which may make certain mods deactivate if it exists")]
	[DataMember, Reactive] public bool DeleteModCrashSanityCheck { get; set; }

	[DataMember] public ConfirmationSettings Confirmations { get; set; }

	[DataMember, Reactive] public long LastUpdateCheck { get; set; }

	[DataMember, Reactive] public string LastOrder { get; set; }

	[DataMember, Reactive] public string LastImportDirectoryPath { get; set; }
	[DataMember, Reactive] public string LastLoadedOrderFilePath { get; set; }
	[DataMember, Reactive] public string LastExtractOutputPath { get; set; }

	public bool Loaded { get; set; }

	private bool canSaveSettings = false;

	public bool CanSaveSettings
	{
		get => canSaveSettings;
		set { this.RaiseAndSetIfChanged(ref canSaveSettings, value); }
	}

	public bool SettingsWindowIsOpen { get; set; }


	[Reactive] public string DefaultExtenderLogDirectory { get; set; }
	[Reactive] public string ExtenderLogDirectory { get; set; }

	private static string GetExtenderLogsDirectory(string defaultDirectory, string logDirectory)
	{
		if (String.IsNullOrWhiteSpace(logDirectory))
		{
			return defaultDirectory;
		}
		return logDirectory;
	}

	private static bool TryGetExtraProperty<T>(IDictionary<string, object> additionalProperties, string key, out T value)
	{
		value = default;
		if(additionalProperties.TryGetValue(key, out var entryObj) && entryObj is T entry)
		{
			value = entry;
			return true;
		}
		return false;
	}

	[Newtonsoft.Json.JsonExtensionData]
	private IDictionary<string, object> AdditionalFields { get; set; } = new Dictionary<string, object>();

	[OnDeserializing]
	private void OnDeserializing(StreamingContext context)
	{
		// A zero value marks settings written before Redux added the three-theme selector.
		ColorTheme = 0;
	}

	[OnDeserialized]
	private void OnDeserialized(StreamingContext context)
	{
		if (!Enum.IsDefined(ColorTheme) || ColorTheme == 0)
		{
			ColorTheme = DarkThemeEnabled ? ReduxThemeType.ReduxDark : ReduxThemeType.ReduxLight;
		}
		DarkThemeEnabled = ColorTheme == ReduxThemeType.ReduxDark;
		CustomModCategories ??= new List<string>();
		ModCategoryDisplayOrder ??= new List<string>();
		ModCategoryOverrides = ModCategoryOverrides != null
			? new Dictionary<string, string>(ModCategoryOverrides, StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		ModCategoryAssignments = ModCategoryAssignments != null
			? new Dictionary<string, List<string>>(ModCategoryAssignments, StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		ModCategoryColors = ModCategoryColors != null
			? new Dictionary<string, string>(ModCategoryColors, StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		SavedCategoryColors ??= new List<string>();
		DisabledModCategories ??= new List<string>();
		KnownCategorizedModIds ??= new List<string>();
		UnseenCategoryModIds = UnseenCategoryModIds != null
			? new Dictionary<string, List<string>>(UnseenCategoryModIds, StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		ModListVisualDividers = ModListVisualDividers != null
			? new Dictionary<string, string>(ModListVisualDividers, StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		VisualModListDividers ??= new List<ModListVisualDividerData>();
		foreach (var legacyAssignment in ModCategoryOverrides.Where(entry => !String.IsNullOrWhiteSpace(entry.Value)))
		{
			if (!ModCategoryAssignments.ContainsKey(legacyAssignment.Key))
			{
				ModCategoryAssignments[legacyAssignment.Key] = new List<string> { legacyAssignment.Value };
			}
		}
		if (TryGetExtraProperty(AdditionalFields, "LaunchThroughSteam", out bool launchThroughSteam) && launchThroughSteam == true)
		{
			LaunchType = LaunchGameType.Steam;
		}
	}

	public void InitSubscriptions()
	{
		var properties = typeof(DivinityModManagerSettings)
		.GetRuntimeProperties()
		.Where(prop => Attribute.IsDefined(prop, typeof(DataMemberAttribute)))
		.Select(prop => prop.Name)
		.ToArray();

		this.WhenAnyPropertyChanged(properties).Subscribe((c) =>
		{
			if (SettingsWindowIsOpen) CanSaveSettings = true;
		});

		var extenderProperties = typeof(ScriptExtenderSettings)
		.GetRuntimeProperties()
		.Where(prop => Attribute.IsDefined(prop, typeof(DataMemberAttribute)))
		.Select(prop => prop.Name)
		.ToArray();

		ExtenderSettings.WhenAnyPropertyChanged(extenderProperties).Subscribe((c) =>
		{
			if (SettingsWindowIsOpen) CanSaveSettings = true;
		});

		var extenderUpdaterProperties = typeof(ScriptExtenderUpdateConfig)
		.GetRuntimeProperties()
		.Where(prop => Attribute.IsDefined(prop, typeof(DataMemberAttribute)))
		.Select(prop => prop.Name)
		.ToArray();

		ExtenderUpdaterSettings.WhenAnyPropertyChanged(extenderUpdaterProperties).Subscribe((c) =>
		{
			if (SettingsWindowIsOpen) CanSaveSettings = true;
		});

		this.WhenAnyValue(x => x.DebugModeEnabled).Subscribe(b => DivinityApp.DeveloperModeEnabled = b);

		this.WhenAnyValue(x => x.ResetModioSupportWarningAcknowledgement)
			.Where(reset => reset)
			.Subscribe(_ =>
			{
				ModioSupportWarningAcknowledged = false;
				ResetModioSupportWarningAcknowledgement = false;
				CanSaveSettings = true;
			});

		this.WhenAnyValue(x => x.ResetReduxPreviewWarningAcknowledgement)
			.Where(reset => reset)
			.Subscribe(_ =>
			{
				ReduxPreviewWarningAcknowledged = false;
				ResetReduxPreviewWarningAcknowledgement = false;
				CanSaveSettings = true;
			});

		this.WhenAnyValue(x => x.ResetOfflineNexusDatabaseWarningAcknowledgement)
			.Where(reset => reset)
			.Subscribe(_ =>
			{
				OfflineNexusDatabaseWarningAcknowledged = false;
				ResetOfflineNexusDatabaseWarningAcknowledgement = false;
				CanSaveSettings = true;
			});

		this.WhenAnyValue(x => x.DefaultExtenderLogDirectory, x => x.ExtenderSettings.LogDirectory)
		.Select(x => GetExtenderLogsDirectory(x.Item1, x.Item2))
		.BindTo(this, x => x.ExtenderLogDirectory);

		this.WhenAnyValue(x => x.LaunchType, x => x == LaunchGameType.Custom)
			.Select(PropertyConverters.BoolToVisibility)
			.ToUIProperty(this, x => x.CustomLaunchVisibility, Visibility.Collapsed);
	}

	public DivinityModManagerSettings()
	{
		Loaded = false;
		//Defaults
		ExtenderSettings = new ScriptExtenderSettings();
		ExtenderUpdaterSettings = new ScriptExtenderUpdateConfig();
		Window = new WindowSettings();
		Confirmations = new();

		DefaultExtenderLogDirectory = "";

		this.SetToDefault();
	}
}
