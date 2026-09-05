using DivinityModManager.Extensions;
using DivinityModManager.Models.App;
using DivinityModManager.Models.Extender;
using DivinityModManager.Util;

using DynamicData;
using DynamicData.Binding;

using System.ComponentModel;
using System.Collections.ObjectModel;
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

public enum ReduxTypographyFont
{
	[Description("Manrope")]
	Manrope = 1,
	[Description("Segoe UI")]
	SegoeUI = 2,
	[Description("Atkinson Hyperlegible")]
	AtkinsonHyperlegible = 3,
	[Description("Monaspace Neon")]
	MonaspaceNeon = 4,
	[Description("Minipax")]
	Minipax = 5,
	[Description("Chivo")]
	Chivo = 6
}

public enum ReduxTextSize
{
	[Description("Compact")]
	Compact = 1,
	[Description("Default")]
	Default = 2,
	[Description("Large")]
	Large = 3
}

[DataContract]
public class DivinityModManagerSettings : ReactiveObject
{
	[SettingsEntry("Game Data folder", "The game's Data folder, used when loading editor projects. Example: Baldur's Gate 3/Data.")]
	[DataMember, Reactive] public string GameDataPath { get; set; }

	[SettingsEntry("Game executable", "The path to bg3.exe or bg3_dx11.exe in the game's bin folder.")]
	[DataMember, Reactive] public string GameExecutablePath { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Use DirectX 11", "Launch bg3_dx11.exe instead of the default Vulkan executable.")]
	[DataMember, Reactive] public bool LaunchDX11 { get; set; }

	[DefaultValue("")]
	// Prefer browser SSO once Redux has a registered Nexus Mods application slug.
	[SettingsEntry("Nexus Mods API key", "Personal key used for mod information and update checks. It is protected for your current Windows account.")]
	[DataMember, Reactive] public string NexusModsAPIKey { get; set; }
	public bool ShouldSerializeNexusModsAPIKey() => false;

	[DefaultValue("")]
	[SettingsEntry("mod.io API key", "Read-only key used for mod information. It is protected for your current Windows account.")]
	[DataMember, Reactive] public string ModioAPIKey { get; set; }
	public bool ShouldSerializeModioAPIKey() => false;

	[DefaultValue(false)]
	[SettingsEntry("Disable online mod information", "Do not contact Nexus Mods or mod.io. Existing links and API keys are kept for later.")]
	[DataMember, Reactive] public bool LocalOnlyMode { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Enable story logging", "Enable the Osiris story log (osiris.log) when launching the game.")]
	[DataMember, Reactive] public bool GameStoryLogEnabled { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Disable launcher telemetry", "Disable telemetry options in the Larian launcher. Telemetry is already disabled when mods are active.")]
	[DataMember, Reactive] public bool DisableLauncherTelemetry { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Disable launcher mod warnings", "Disable mod and data-mismatch warnings in the Larian launcher.")]
	[DataMember, Reactive] public bool DisableLauncherModWarnings { get; set; }

	[DefaultValue(LaunchGameType.Exe)]
	[SettingsEntry("Launch method", "Choose whether the manager launches the game directly, through Steam, or with a custom target.")]
	[DataMember, Reactive] public LaunchGameType LaunchType { get; set; }

	[DefaultValue("")]
	[SettingsEntry("Custom launch target", "File path, protocol, or shell command to run when the launch method is Custom.")]
	[DataMember, Reactive] public string CustomLaunchAction { get; set; }

	[DefaultValue("")]
	[SettingsEntry("Custom launch arguments", "Optional arguments passed to the custom launch target.")]
	[DataMember, Reactive] public string CustomLaunchArgs { get; set; }

	[ObservableAsProperty] public Visibility CustomLaunchVisibility { get; }

	[DefaultValue("Orders")]
	[SettingsEntry("Load-order folder", "The folder used for saved load-order .json files.")]
	[DataMember, Reactive] public string LoadOrderPath { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Internal Logging", "Enable the log for the mod manager", HideFromUI = true)]
	[DataMember, Reactive] public bool LogEnabled { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Add missing dependencies when exporting", "Add installed dependency mods above their dependents when they were omitted from the active order.")]
	[DataMember, Reactive] public bool AutoAddDependenciesWhenExporting { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Check for Redux updates automatically", "Reserved for a future Redux release. Automatic application updates are disabled during the alpha.")]
	[DataMember, Reactive] public bool CheckForUpdates { get; set; }

	[DefaultValue("")]
	[SettingsEntry("BG3 AppData folder override", "Override %LOCALAPPDATA%\\Larian Studios\\Baldur's Gate 3 for profiles, installed mods, and exported load orders.")]
	[DataMember, Reactive] public string DocumentsFolderPathOverride { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Show Toolkit project markers", "Show a build icon beside mods detected as Toolkit or editor projects.")]
	[DataMember, Reactive] public bool EnableColorblindSupport { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool DarkThemeEnabled { get; set; }

	[DefaultValue(ReduxThemeType.ReduxDark)]
	[SettingsEntry("Theme", "Choose the app's colors.", HideFromUI = true)]
	[DataMember, Reactive] public ReduxThemeType ColorTheme { get; set; } = ReduxThemeType.ReduxDark;

	[DefaultValue(ReduxTypographyFont.Manrope)]
	[SettingsEntry("App font", "Choose the font used throughout the app.", HideFromUI = true)]
	[DataMember, Reactive] public ReduxTypographyFont TypographyFont { get; set; } = ReduxTypographyFont.Manrope;

	[DefaultValue("")]
	[DataMember, Reactive] public string CustomTypographyFont { get; set; } = String.Empty;

	[DefaultValue(ReduxTextSize.Default)]
	[SettingsEntry("Text size", "Choose an interface text size.", HideFromUI = true)]
	[DataMember, Reactive] public ReduxTextSize TextSize { get; set; } = ReduxTextSize.Default;

	[DefaultValue(false)]
	[SettingsEntry("Reduce motion", "Disable smooth scrolling and animated movement, using immediate transitions instead.")]
	[DataMember, Reactive] public bool ReduceMotion { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Disable blur and dimming", "Keep the main window clear behind dialogs and secondary windows.")]
	[DataMember, Reactive] public bool DisableBackgroundEffects { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Welcome setup completed", "Tracks whether the compact Redux welcome and initial setup window has been shown.", HideFromUI = true)]
	[DataMember, Reactive] public bool HasSeenReduxWelcome { get; set; }

	[DefaultValue("")]
	[DataMember, Reactive] public string ActiveCustomThemeId { get; set; } = String.Empty;

	[DataMember, Reactive] public ObservableCollection<ReduxCustomTheme> CustomThemes { get; set; } = new();

	[DefaultValue(true)]
	[SettingsEntry("Use category colors for selection", "Use category colors when hovering over or selecting mods.", HideFromUI = true)]
	[DataMember, Reactive] public bool UseCategoryColorsForHover { get; set; } = true;

	[DefaultValue(true)]
	[SettingsEntry("Color category names", "Use each category's color for its name in the Categories pane.", HideFromUI = true)]
	[DataMember, Reactive] public bool UseCategoryColorsForSidebarText { get; set; } = true;

	[DefaultValue(true)]
	[SettingsEntry("Legacy category selection colors", "Retained for compatibility with earlier Redux settings.", HideFromUI = true)]
	[DataMember, Reactive] public bool UseCategoryColorsForSidebarSelection { get; set; } = true;

	/// <summary>
	/// Unified presentation setting. The two serialized fields are retained so settings from
	/// earlier Redux builds continue to load without migration or data loss.
	/// </summary>
	[IgnoreDataMember]
	public bool UseCategoryColorsForInteractions
	{
		get => UseCategoryColorsForHover || UseCategoryColorsForSidebarSelection;
		set
		{
			if (UseCategoryColorsForHover == value && UseCategoryColorsForSidebarSelection == value) return;
			UseCategoryColorsForHover = value;
			UseCategoryColorsForSidebarSelection = value;
			this.RaisePropertyChanged();
		}
	}

	[DefaultValue(false)]
	[SettingsEntry("Legacy source icons only", "Retained for compatibility with earlier Redux settings.", HideFromUI = true)]
	[DataMember, Reactive] public bool UseSourceIconsOnly { get; set; }

	/// <summary>
	/// Unified compact-label setting. The serialized source-only field is retained so existing
	/// settings and custom themes continue to load without migration.
	/// </summary>
	[IgnoreDataMember]
	public bool UseIconsOnly
	{
		get => UseSourceIconsOnly;
		set
		{
			if (UseSourceIconsOnly == value) return;
			UseSourceIconsOnly = value;
			this.RaisePropertyChanged();
		}
	}

	// Redux mod-list column choices. These are managed from the column-header
	// context menu, so they stay out of the main Settings window.
	// File Name/Version/Last Modified default off: they're lookup-when-needed facts
	// (largely redundant with Name/Last Updated), not scan-at-a-glance information,
	// and showing all seven columns by default crowded out the ones that actually
	// help a decision (Category, Source).
	[DefaultValue(false)]
	[DataMember, Reactive] public bool ShowModListVersionColumn { get; set; }

	[DefaultValue(false)]
	[DataMember, Reactive] public bool ShowModListFileNameColumn { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListAuthorColumn { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListLastUpdatedColumn { get; set; }

	[DefaultValue(false)]
	[DataMember, Reactive] public bool ShowModListLastModifiedColumn { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListSourceColumn { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowModListCategoryColumn { get; set; }

	// Widths are stored independently because active and inactive lists can be sized
	// for different content. Hidden columns retain their last useful width.
	[DataMember, Reactive] public Dictionary<string, double> ActiveModListColumnWidths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	[DataMember, Reactive] public Dictionary<string, double> InactiveModListColumnWidths { get; set; } = new(StringComparer.OrdinalIgnoreCase);

	[DefaultValue(true)]
	[SettingsEntry("Hide empty categories", "Hide categories with no matching installed mods from the Categories sidebar.")]
	[DataMember, Reactive] public bool HideEmptyModCategories { get; set; }

	[DataMember, Reactive] public List<string> CustomModCategories { get; set; } = new();
	// Redux-only presentation order for the category sidebar. This never changes mod assignments or load order.
	[DataMember, Reactive] public List<string> ModCategoryDisplayOrder { get; set; } = new();
	// Legacy single-category assignments are retained for migration from early Redux builds.
	[DataMember, Reactive] public Dictionary<string, string> ModCategoryOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	[DataMember, Reactive] public Dictionary<string, List<string>> ModCategoryAssignments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	[DataMember, Reactive] public Dictionary<string, string> ModCategoryColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	// Optional Redux presentation icon per category. Empty values explicitly retain the dot fallback.
	[DataMember, Reactive] public Dictionary<string, string> ModCategoryIcons { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	// Optional user-authored sidebar tooltip. Blank values intentionally produce no tooltip.
	[DataMember, Reactive] public Dictionary<string, string> ModCategoryDescriptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	[DataMember, Reactive] public List<string> SavedCategoryColors { get; set; } = new();
	[DataMember, Reactive] public List<string> DisabledModCategories { get; set; } = new();

	[DefaultValue(false)]
	[DataMember, Reactive] public bool SaveModCategoryFilterBetweenSessions { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ShowCategoryIconsInPills { get; set; } = true;

	[SettingsEntry("Hide toolbar", "Hide the main command toolbar. Restore it with Ctrl+Shift+B or the Toolbar menu.")]
	[DefaultValue(false)]
	[DataMember, Reactive] public bool HideToolbar { get; set; }

	[DefaultValue(true)]
	[DataMember, Reactive] public bool CategoriesPanelExpanded { get; set; } = true;

	[DefaultValue(true)]
	[DataMember, Reactive] public bool InactiveModsPanelExpanded { get; set; } = true;

	[DefaultValue(true)]
	[DataMember, Reactive] public bool AlwaysLoadedPanelExpanded { get; set; } = true;

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
	[SettingsEntry("Move focus when transferring mods", "When Enter moves selected mods to the other list, move keyboard focus to that list too.")]
	[DataMember, Reactive] public bool ShiftListFocusOnSwap { get; set; }

	[DataMember, IgnoreSetFrom] public ScriptExtenderSettings ExtenderSettings { get; set; }
	[DataMember, IgnoreSetFrom] public ScriptExtenderUpdateConfig ExtenderUpdaterSettings { get; set; }

	[DefaultValue(DivinityGameLaunchWindowAction.None)]
	[SettingsEntry("After launching the game", "Choose whether the manager stays open, minimizes, or closes.")]
	[DataMember, Reactive]
	public DivinityGameLaunchWindowAction ActionOnGameLaunch { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Suppress missing-mod warnings", "Do not display a warning when the selected load order references mods that are not installed.")]
	[DataMember, Reactive] public bool DisableMissingModWarnings { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Enable mod diagnostics", "Check packages, dependencies, Script Extender requirements, overrides, and optional load-order issues. This never edits mods or the load order.")]
	[DataMember, Reactive] public bool EnableModHealth { get; set; } = true;

	[DefaultValue(false)]
	[SettingsEntry("Disable mod.io warnings", "Hide the warning that BG3 or Steam Cloud may restore mod.io files, including cached files after unsubscribing. Online mod information is unaffected.")]
	[DataMember, Reactive] public bool DisableModioWarnings { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Include Load Order Advisor", "Check whether mods load before their required dependencies. It never reorders mods automatically.")]
	[DataMember, Reactive] public bool EnableLoadOrderAdvisor { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Mod Developer Mode", "This enables features for mod developers, such as being able to copy a mod's UUID in context menus, and additional Script Extender options", HideFromUI = true)]
	[Reactive, DataMember] public bool DebugModeEnabled { get; set; }

	[DefaultValue("")]
	[DataMember, Reactive] public string GameLaunchParams { get; set; }

	[DataMember] public WindowSettings Window { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Remember window position", "Restore the main window to its previous screen position at startup.")]
	[DataMember, Reactive] public bool SaveWindowLocation { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Clear ModCrashSanityCheck", "Delete BG3's ModCrashSanityCheck folder when needed so it cannot silently deactivate installed mods.")]
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
		TypographyFont = 0;
		TextSize = 0;
	}

	[OnDeserialized]
	private void OnDeserialized(StreamingContext context)
	{
		if (!Enum.IsDefined(ColorTheme) || ColorTheme == 0)
		{
			ColorTheme = DarkThemeEnabled ? ReduxThemeType.ReduxDark : ReduxThemeType.ReduxLight;
		}
		DarkThemeEnabled = ColorTheme == ReduxThemeType.ReduxDark;
		if (!Enum.IsDefined(TypographyFont) || TypographyFont == 0)
		{
			TypographyFont = ReduxTypographyFont.Manrope;
		}
		CustomTypographyFont ??= String.Empty;
		if (!Enum.IsDefined(TextSize) || TextSize == 0)
		{
			TextSize = ReduxTextSize.Default;
		}
		CustomThemes ??= new ObservableCollection<ReduxCustomTheme>();
		foreach (var theme in CustomThemes)
		{
			theme.Id = String.IsNullOrWhiteSpace(theme.Id) ? Guid.NewGuid().ToString("N") : theme.Id;
			theme.Name = String.IsNullOrWhiteSpace(theme.Name) ? "Imported Theme" : theme.Name.Trim();
			if (!Enum.IsDefined(theme.TypographyFont) || theme.TypographyFont == 0)
			{
				theme.TypographyFont = ReduxTypographyFont.Manrope;
			}
			theme.CustomTypographyFont ??= String.Empty;
			if (!Enum.IsDefined(theme.TextSize) || theme.TextSize == 0)
			{
				theme.TextSize = ReduxTextSize.Default;
			}
		}
		if (!CustomThemes.Any(theme => theme.Id.Equals(ActiveCustomThemeId, StringComparison.OrdinalIgnoreCase)))
		{
			ActiveCustomThemeId = String.Empty;
		}
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
		ModCategoryIcons = ModCategoryIcons != null
			? new Dictionary<string, string>(ModCategoryIcons, StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		ModCategoryDescriptions = ModCategoryDescriptions != null
			? new Dictionary<string, string>(ModCategoryDescriptions, StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		ActiveModListColumnWidths = ActiveModListColumnWidths != null
			? new Dictionary<string, double>(ActiveModListColumnWidths, StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
		InactiveModListColumnWidths = InactiveModListColumnWidths != null
			? new Dictionary<string, double>(InactiveModListColumnWidths, StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
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

		// Colour-coded labels render in the mod list, the details drawer, hover-card tooltips and
		// the toolbar. Tooltips sit in their own visual tree and cannot reach this settings object
		// by ancestor lookup, so mirror the flag onto DivinityApp and let every template bind to
		// it the same way.
		this.WhenAnyValue(x => x.UseCategoryColorsForSidebarText).Subscribe(b => DivinityApp.UseCategoryColorsForText = b);
		this.WhenAnyValue(
				x => x.UseCategoryColorsForHover,
				x => x.UseCategoryColorsForSidebarSelection,
				(hover, selection) => hover || selection)
			.Subscribe(b => DivinityApp.UseCategoryColorsForInteractions = b);
		this.WhenAnyValue(x => x.ShowCategoryIconsInPills).Subscribe(b => DivinityApp.ShowInterfaceIcons = b);
		this.WhenAnyValue(x => x.UseSourceIconsOnly).Subscribe(b => DivinityApp.UseIconsOnly = b);

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
