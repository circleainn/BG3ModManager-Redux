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
	// Values 4-6 are retained below so older settings and exported themes
	// deserialize safely. They are no longer offered by the Redux font selector.
	[Description("Segoe UI")]
	SegoeUI = 2,
	[Description("Atkinson Hyperlegible")]
	AtkinsonHyperlegible = 3,
	[Description("Monaspace Neon")]
	MonaspaceNeon = 4,
	[Description("Minipax")]
	Minipax = 5,
	[Description("Chivo")]
	Chivo = 6,
	[Description("Archivo Black")]
	ArchivoBlack = 7,
	[Description("IBM Plex Mono")]
	IBMPlexMono = 8
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
	[DefaultValue(true), DataMember, Reactive] public bool ShowActiveModIndex { get; set; } = true;
    [DataMember, Reactive] public bool ShowInactiveModIndex { get; set; } = false;

	[DataMember, Reactive] public string LastSeenWhatsNewVersion { get; set; } = String.Empty;
	// One-time upgrade choice for separators created before per-order/global scope was available.
	[DataMember, Reactive] public bool HasResolvedPersistentSeparatorUpgrade { get; set; }
	[DefaultValue(true)]
	[SettingsEntry("Show What's New after updates", "Open release notes after Redux updates. Turn this off to skip future popups.")]
	[DataMember, Reactive] public bool ShowWhatsNewAfterUpdates { get; set; } = true;
	private bool? _useGeneratedGradients;
	private bool? _useThemeDefaultTypography;

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
	[SettingsEntry("Check for updates automatically", "Check the public-alpha channel in the background after startup and notify me when a newer release is available.")]
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

	[DataMember(Name = "UseGeneratedGradients", EmitDefaultValue = false)]
	public bool? UseGeneratedGradientsPreference
	{
		get => _useGeneratedGradients;
		set
		{
			if (_useGeneratedGradients == value) return;
			this.RaiseAndSetIfChanged(ref _useGeneratedGradients, value);
			this.RaisePropertyChanged(nameof(UsesGeneratedGradients));
		}
	}

	[IgnoreDataMember]
	public bool UsesGeneratedGradients
	{
		get => UseGeneratedGradientsPreference ?? ColorTheme != ReduxThemeType.Parchment;
		set => UseGeneratedGradientsPreference = value;
	}

	[DefaultValue(ReduxTypographyFont.Manrope)]
	[SettingsEntry("App font", "Choose the font used throughout the app.", HideFromUI = true)]
	[DataMember, Reactive] public ReduxTypographyFont TypographyFont { get; set; } = ReduxTypographyFont.Manrope;

	[DefaultValue("")]
	[DataMember, Reactive] public string CustomTypographyFont { get; set; } = String.Empty;

	[DataMember(Name = "UseThemeDefaultTypography", EmitDefaultValue = false)]
	public bool? UseThemeDefaultTypographyPreference
	{
		get => _useThemeDefaultTypography;
		set
		{
			if (_useThemeDefaultTypography == value) return;
			this.RaiseAndSetIfChanged(ref _useThemeDefaultTypography, value);
			this.RaisePropertyChanged(nameof(UseThemeDefaultTypography));
		}
	}

	/// <summary>
	/// Missing values from earlier settings infer the old Manrope value as the
	/// theme default while preserving every non-default or imported font choice.
	/// An explicit false value also lets a user deliberately choose Manrope for
	/// Parchment without that choice being mistaken for an inherited default.
	/// </summary>
	[IgnoreDataMember]
	public bool UseThemeDefaultTypography
	{
		get => UseThemeDefaultTypographyPreference
			?? (String.IsNullOrWhiteSpace(CustomTypographyFont) && TypographyFont == ReduxTypographyFont.Manrope);
		set => UseThemeDefaultTypographyPreference = value;
	}

	[DefaultValue(ReduxTextSize.Default)]
	[SettingsEntry("Text size", "Choose an interface text size.", HideFromUI = true)]
	[DataMember, Reactive] public ReduxTextSize TextSize { get; set; } = ReduxTextSize.Default;

	[DefaultValue(false)]
	[SettingsEntry("Reduce motion", "Remove sliding, scaling, smooth scrolling, and animated window or menu transitions while keeping clear interface feedback.")]
	[DataMember, Reactive] public bool ReduceMotion { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Disable blur and dimming", "Keep the main window clear behind dialogs and secondary windows.")]
	[DataMember, Reactive] public bool DisableBackgroundEffects { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Welcome setup completed", "Tracks whether the compact Redux welcome and initial setup window has been shown.", HideFromUI = true)]
	[DataMember, Reactive] public bool HasSeenReduxWelcome { get; set; }

	[DefaultValue("")]
	[SettingsEntry("Nexus link registration owner", "Private ownership marker used to restore the previous Windows NXM handler.", HideFromUI = true)]
	[DataMember, Reactive] public string NxmAssociationOwnerId { get; set; } = String.Empty;

	[DefaultValue(3)]
	[SettingsEntry("Concurrent Nexus downloads", "Maximum simultaneous Nexus file transfers, from 1 through 6.")]
	[DataMember, Reactive] public int NxmActiveDownloadLimit { get; set; } = 3;

	[DefaultValue(true)]
	[SettingsEntry("Confirm Nexus downloads", "Legacy preference. Downloads no longer require a routine confirmation.", HideFromUI = true)]
	[DataMember, Reactive] public bool ConfirmCleanNxmDownloads { get; set; } = true;

	[DefaultValue(true)]
	[SettingsEntry("Review clean mod installs", "Legacy preference. Updates, replacements, and packages with warnings are always reviewed.", HideFromUI = true)]
	[DataMember, Reactive] public bool ConfirmCleanModInstalls { get; set; } = true;

	[DefaultValue(false)]
	[SettingsEntry("Open Download Manager for Nexus links", "Bring Download Manager forward when a Nexus download starts. When off, show a quiet notification.")]
	[DataMember, Reactive] public bool BringNxmDownloadsToFront { get; set; } = false;

	[DefaultValue(false)]
	[SettingsEntry("Retain installed package archives", "Keep a deduplicated copy of successfully installed packages for later reinstall. Disabled by default and may use significant disk space.")]
	[DataMember, Reactive] public bool RetainInstalledPackageArchives { get; set; }

	[DefaultValue(10)]
	[SettingsEntry("Package archive quota (GB)", "Maximum disk space for retained install packages. Redux prunes the least recently used packages when this limit is reached.")]
	[DataMember, Reactive] public int RetainedPackageArchiveQuotaGb { get; set; } = 10;

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
	// Custom categories whose visible label is suppressed while interface icons are enabled.
	// The category name remains its stable identity and is still exposed through tooltips.
	[DataMember, Reactive] public List<string> IconOnlyModCategories { get; set; } = new();
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

	[DataMember] public List<string> InactiveModOrder { get; set; } = new();

	[DefaultValue(true)]
	[DataMember, Reactive] public bool InactiveModsPanelExpanded { get; set; } = true;

	[DefaultValue(true)]
	[DataMember, Reactive] public bool AlwaysLoadedPanelExpanded { get; set; } = true;

	[DefaultValue(true)]
	[DataMember, Reactive] public bool ModDetailsPanelExpanded { get; set; } = true;

	// Presentation-only state for campaign groups in Save Game Manager.
	[DataMember, Reactive] public List<string> CollapsedSaveGameCampaigns { get; set; } = new();

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

	[DefaultValue(false), DataMember]
	public bool ExportDefaultScriptExtenderSettings
	{
		get => ExtenderSettings?.ExportDefaultExtenderSettings == true;
		set { if (ExtenderSettings != null) ExtenderSettings.ExportDefaultExtenderSettings = value; }
	}

	[DefaultValue(DivinityGameLaunchWindowAction.None)]
	[SettingsEntry("After launching the game", "Choose whether the manager stays open, minimizes, or closes.")]
	[DataMember, Reactive]
	public DivinityGameLaunchWindowAction ActionOnGameLaunch { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Suppress missing-mod warnings", "Do not display a warning when the selected load order references mods that are not installed.")]
	[DataMember, Reactive] public bool DisableMissingModWarnings { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Mod diagnostics", "Compatibility setting retained for older Redux preferences.", HideFromUI = true)]
	[DataMember(Name = "EnableModHealth")]
	public bool EnableModHealth
	{
		get => true;
		set { /* Diagnostics are a built-in Redux feature. Retain the old field for settings compatibility. */ }
	}

	[DefaultValue(false)]
	// Retained only so settings written by earlier public-alpha builds deserialize without migration errors.
	[DataMember, Reactive] public bool DisableModioWarnings { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Enable Load Order Advisor", "Add experimental placement checks and the optional load-order organizer. Redux never reorders mods automatically.")]
	[DataMember, Reactive] public bool EnableLoadOrderAdvisor { get; set; }
	[DataMember, Reactive] public List<string> IgnoredLoadOrderAdvisorFindingKeys { get; set; } = new();

	[DefaultValue(false)]
	[SettingsEntry("Debug Mode", "Show additional read-only technical details and write more diagnostic logging while troubleshooting Redux.", HideFromUI = true)]
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

	[DataMember, Reactive] public ConfirmationSettings Confirmations { get; set; }

	[DataMember, Reactive] public long LastUpdateCheck { get; set; }
	[DataMember, Reactive] public long LastUpdateCheckAttempt { get; set; }

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
		UseThemeDefaultTypographyPreference = null;
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
		IconOnlyModCategories = (IconOnlyModCategories ?? new List<string>())
			.Where(category => !String.IsNullOrWhiteSpace(category) &&
				CustomModCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
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
		IconOnlyModCategories.RemoveAll(category =>
			!ModCategoryIcons.TryGetValue(category, out var iconId) || String.IsNullOrWhiteSpace(iconId));
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
		CollapsedSaveGameCampaigns = (CollapsedSaveGameCampaigns ?? [])
			.Where(name => !String.IsNullOrWhiteSpace(name))
			.Select(name => name.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		IgnoredLoadOrderAdvisorFindingKeys = (IgnoredLoadOrderAdvisorFindingKeys ?? [])
			.Where(key => !String.IsNullOrWhiteSpace(key))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
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
