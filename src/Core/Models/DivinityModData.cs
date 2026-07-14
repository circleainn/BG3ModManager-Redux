

using DivinityModManager.Models.Github;
using DivinityModManager.Models.Metadata;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;

using System.Globalization;
using System.Windows;

namespace DivinityModManager.Models;

[ScreenReaderHelper(Name = "DisplayName", HelpText = "HelpText")]
public class DivinityModData : DivinityBaseModData, ISelectable
{
	private static readonly SortExpressionComparer<ModuleShortDesc> _moduleSort = SortExpressionComparer<ModuleShortDesc>
		.Ascending(p => !DivinityApp.IgnoredMods.Lookup(p.UUID).HasValue).ThenByAscending(p => p.Name);

	[Reactive] public int Index { get; set; }

	public string OutputPakName
	{
		get
		{
			if (!Folder.Contains(UUID))
			{
				return Path.ChangeExtension($"{Folder}_{UUID}", "pak");
			}
			else
			{
				return Path.ChangeExtension($"{FileName}", "pak");
			}
		}
	}

	[Reactive] public string ModType { get; set; }
	[Reactive] public bool IsNewlyDetected { get; set; }
	[Reactive] public string DisplayCategory { get; set; }
	[Reactive] public List<ModCategoryDisplayData> DisplayCategories { get; set; } = new();
	[Reactive] public int SourceComponentCount { get; set; } = 1;
	[Reactive] public string SourceComponentSummary { get; set; }
	[Reactive] public string SourceComponentTooltip { get; set; }
	[Reactive] public Visibility SourceComponentVisibility { get; set; } = Visibility.Collapsed;
	[Reactive] public string VisualDividerTitle { get; set; }
	[Reactive] public bool ShowVisualDivider { get; set; }
	[Reactive] public bool IsVisualDivider { get; set; }
	[Reactive] public string VisualDividerId { get; set; }
	[Reactive] public string VisualDividerColor { get; set; } = "#8A6AF1";
	[Reactive] public bool IsVisualDividerCollapsed { get; set; }
	[Reactive] public bool IsHiddenByVisualDivider { get; set; }

	[Reactive] public DateTime? LastUpdated { get; set; }

	[Reactive] public DivinityExtenderModStatus ExtenderModStatus { get; set; }
	[Reactive] public DivinityOsirisModStatus OsirisModStatus { get; set; }

	[Reactive] public int CurrentExtenderVersion { get; set; }

	private static string ExtenderStatusToToolTipText(DivinityExtenderModStatus status, int requiredVersion, int currentVersion)
	{
		var result = "";

		if (requiredVersion > -1)
		{
			result += $"Requires Script Extender v{requiredVersion} or Higher";
		}
		else
		{
			result += "Requires the Script Extender";
		}

		if (status.HasFlag(DivinityExtenderModStatus.DisabledFromConfig))
		{
			result += "\n(Enable Extensions in the Script Extender Settings)";
		}
		else if (status.HasFlag(DivinityExtenderModStatus.MissingAppData))
		{
			result += $"\n(Missing %LOCALAPPDATA%\\..\\{DivinityApp.EXTENDER_APPDATA_DLL})";
		}
		else if (status.HasFlag(DivinityExtenderModStatus.MissingUpdater))
		{
			result += $"\n(Missing {DivinityApp.EXTENDER_UPDATER_FILE})";
		}
		else if (status.HasFlag(DivinityExtenderModStatus.MissingRequiredVersion))
		{
			result += "\n(The installed SE version is older)";
		}

		if (result != "")
		{
			result += Environment.NewLine;
		}

		if (currentVersion > -1)
		{
			if(status.HasFlag(DivinityExtenderModStatus.MissingUpdater))
			{
				result += $"You are missing the Script Extender updater (DWrite.dll), which is required";
			}
			else
			{
				result += $"Currently installed version is v{currentVersion}";
			}
		}
		else
		{
			result += "No installed Script Extender version found\nIf you've already downloaded it, try opening the game once to complete the installation process";
		}
		return result;
	}

	private static ScriptExtenderIconType ExtenderModStatusToIcon(DivinityExtenderModStatus status)
	{
		var result = ScriptExtenderIconType.None;

		if (status.HasFlag(DivinityExtenderModStatus.DisabledFromConfig) || status.HasFlag(DivinityExtenderModStatus.MissingUpdater))
		{
			result = ScriptExtenderIconType.Missing;
		}
		else if (status.HasFlag(DivinityExtenderModStatus.MissingRequiredVersion) || status.HasFlag(DivinityExtenderModStatus.MissingAppData))
		{
			result = ScriptExtenderIconType.Warning;
		}
		else if (status.HasFlag(DivinityExtenderModStatus.Supports))
		{
			result = ScriptExtenderIconType.FulfilledSupports;
		}
		else if (status.HasFlag(DivinityExtenderModStatus.Fulfilled))
		{
			result = ScriptExtenderIconType.FulfilledRequired;
		}

		return result;
	}

	[Reactive] public DivinityModScriptExtenderConfig ScriptExtenderData { get; set; }

	public SourceCache<ModuleShortDesc, string> Dependencies { get; private set; } = new SourceCache<ModuleShortDesc, string>(x => x.UUID);
	public SourceCache<ModuleShortDesc, string> MissingDependencies { get; private set; } = new SourceCache<ModuleShortDesc, string>(x => x.UUID);
	public SourceCache<ModuleShortDesc, string> Conflicts { get; private set; } = new SourceCache<ModuleShortDesc, string>(x => x.UUID);


	protected ReadOnlyObservableCollection<ModuleShortDesc> displayedDependencies;
	public ReadOnlyObservableCollection<ModuleShortDesc> DisplayedDependencies => displayedDependencies;

	protected ReadOnlyObservableCollection<ModuleShortDesc> displayedConflicts;
	public ReadOnlyObservableCollection<ModuleShortDesc> DisplayedConflicts => displayedConflicts;

	public override string GetDisplayName()
	{
		if (DisplayFileForName)
		{
			if (!IsEditorMod)
			{
				return FileName;
			}
			else
			{
				return Folder + " [Editor Project]";
			}
		}
		else
		{
			if (!DivinityApp.DeveloperModeEnabled && UUID == DivinityApp.MAIN_CAMPAIGN_UUID)
			{
				return "Main";
			}
			return Name;
		}
	}

	[ObservableAsProperty] public bool HasToolTip { get; }

	[ObservableAsProperty] public int TotalDependencies { get; }
	[ObservableAsProperty] public bool HasDependencies { get; }

	[ObservableAsProperty] public int TotalConflicts { get; }
	[ObservableAsProperty] public bool HasConflicts { get; }
	[ObservableAsProperty] public bool HasInvalidUUID { get; }
	[ObservableAsProperty] public bool IsMissingDependency { get; }
	[ObservableAsProperty] public string MissingDependencyToolTip { get; }
	[ObservableAsProperty] public Visibility HasInvalidUUIDVisibility { get; }
	[ObservableAsProperty] public Visibility MissingDependencyIconVisibility { get; }
	[ObservableAsProperty] public Visibility ToolkitIconVisibility { get; }
	[ObservableAsProperty] public ScriptExtenderIconType ExtenderIcon { get; }

	[Reactive] public bool HasScriptExtenderSettings { get; set; }

	[Reactive] public bool IsEditorMod { get; set; }
	[Reactive] public bool HasColorblindSupport { get; set; }

	[Reactive] public bool IsActive { get; set; }

	private bool isSelected = false;

	public bool IsSelected
	{
		get => isSelected;
		set
		{
			if (value && Visibility != Visibility.Visible)
			{
				value = false;
			}
			this.RaiseAndSetIfChanged(ref isSelected, value);
		}
	}

	[Reactive] public bool CanAddToLoadOrder { get; private set; }
	[ObservableAsProperty] public bool CanDelete { get; }
	[ObservableAsProperty] public bool CanOpenWorkshopLink { get; }
	[ObservableAsProperty] public string ScriptExtenderSupportToolTipText { get; }
	[ObservableAsProperty] public string OsirisStatusToolTipText { get; }
	[ObservableAsProperty] public string LastModifiedDateText { get; }
	[ObservableAsProperty] public string LoadOrderDisplayText { get; }
	[ObservableAsProperty] public string DisplayTitle { get; }
	[ObservableAsProperty] public string DisplayVersion { get; }
	[ObservableAsProperty] public DateTime? DisplayLastUpdated { get; }
	[ObservableAsProperty] public string DisplaySource { get; }

	[ObservableAsProperty] public Visibility DependencyVisibility { get; }
	[ObservableAsProperty] public Visibility ConflictsVisibility { get; }

	[ObservableAsProperty] public Visibility OpenWorkshopLinkVisibility { get; }
	[ObservableAsProperty] public Visibility OpenNexusModsLinkVisibility { get; }
	[ObservableAsProperty] public Visibility ToggleForceAllowInLoadOrderVisibility { get; }
	[ObservableAsProperty] public Visibility ExtenderStatusVisibility { get; }
	[ObservableAsProperty] public Visibility OsirisStatusVisibility { get; }
	[ObservableAsProperty] public Visibility HasFilePathVisibility { get; }

	#region NexusMods Properties

	[ObservableAsProperty] public bool CanOpenNexusModsLink { get; }
	[ObservableAsProperty] public Visibility NexusImageVisibility { get; }
	[ObservableAsProperty] public Visibility NexusModsInformationVisibility { get; }
	[ObservableAsProperty] public DateTime NexusModsCreatedDate { get; }
	[ObservableAsProperty] public DateTime NexusModsUpdatedDate { get; }
	[ObservableAsProperty] public string NexusModsTooltipInfo { get; }

	#endregion

	[Reactive] public bool NexusModsEnabled { get; set; }
	[Reactive] public bool CanDrag { get; set; }
	[Reactive] public bool DeveloperMode { get; set; }
	[Reactive] public bool HasColorOverride { get; set; }
	[Reactive] public string SelectedColor { get; set; }
	[Reactive] public string ListColor { get; set; }

	public HashSet<string> Files { get; set; }

	[Reactive] public DivinityModWorkshopData WorkshopData { get; set; }
	[Reactive] public NexusModsModData NexusModsData { get; set; }
	[Reactive] public ModioModData ModioData { get; set; }
	[Reactive] public GithubModData GithubData { get; set; }
	public ModMetadataViewData Metadata { get; }

	public string GetURL(ModSourceType modSourceType, bool asProtocol = false)
	{
		switch (modSourceType)
		{
			case ModSourceType.STEAM:
				if (WorkshopData != null && WorkshopData.ID != "")
				{
					if (!asProtocol)
					{
						return $"https://steamcommunity.com/sharedfiles/filedetails/?id={WorkshopData.ID}";
					}
					else
					{
						return $"steam://url/CommunityFilePage/{WorkshopData.ID}";
					}
				}
				break;
			case ModSourceType.NEXUSMODS:
				if (NexusModsData != null && NexusModsData.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START)
				{
					return String.Format(DivinityApp.NEXUSMODS_MOD_URL, NexusModsData.ModId);
				}
				break;
			case ModSourceType.GITHUB:
				if (GithubData != null)
				{
					return $"https://github.com/{GithubData.Author}/{GithubData.Repository}";
				}
				break;
		}
		return "";
	}

	public List<string> GetAllURLs(bool asProtocol = false)
	{
		var urls = new List<string>();
		var steamUrl = GetURL(ModSourceType.STEAM, asProtocol);
		if (!String.IsNullOrEmpty(steamUrl))
		{
			urls.Add(steamUrl);
		}
		var nexusUrl = GetURL(ModSourceType.NEXUSMODS, asProtocol);
		if (!String.IsNullOrEmpty(nexusUrl))
		{
			urls.Add(nexusUrl);
		}
		var githubUrl = GetURL(ModSourceType.GITHUB, asProtocol);
		if (!String.IsNullOrEmpty(githubUrl))
		{
			urls.Add(githubUrl);
		}
		return urls;
	}

	public override string ToString()
	{
		return $"Name({Name}) Version({Version?.Version}) Author({Author}) UUID({UUID})";
	}

	public DivinityLoadOrderEntry ToOrderEntry()
	{
		return new DivinityLoadOrderEntry
		{
			UUID = this.UUID,
			Name = this.Name
		};
	}

	public DivinityProfileActiveModData ToProfileModData()
	{
		return new DivinityProfileActiveModData()
		{
			Folder = Folder,
			MD5 = MD5,
			Name = Name,
			UUID = UUID,
			Version = Version.VersionInt
		};
	}

	public void AllowInLoadOrder(bool b)
	{
		ForceAllowInLoadOrder = b;
		IsActive = b && IsForceLoaded;
	}

	private string OsirisStatusToTooltipText(DivinityOsirisModStatus status)
	{
		switch (status)
		{
			case DivinityOsirisModStatus.SCRIPTS:
				return "Has Osiris Scripting";
			case DivinityOsirisModStatus.MODFIXER:
				return "Contains Mod Fixer. Mod Fixer files were detected inside this package. BG3 Patch 7 and newer generally do not require Mod Fixer, and it does not need to be installed separately.";
			case DivinityOsirisModStatus.NONE:
			default:
				return "";
		}
	}

	private bool CanOpenWorkshopBoolCheck(bool enabled, bool isHidden, bool isLarianMod, string workshopID)
	{
		return enabled && !isHidden & !isLarianMod & !String.IsNullOrEmpty(workshopID);
	}

	private string NexusModsInfoToTooltip(DateTime createdDate, DateTime updatedDate, long endorsements)
	{
		var lines = new List<string>();

		if (endorsements > 0)
		{
			lines.Add($"Endorsements: {endorsements}");
		}

		if (createdDate != DateTime.MinValue)
		{
			lines.Add($"Created on {createdDate.ToString(DivinityApp.DateTimeColumnFormat, CultureInfo.InstalledUICulture)}");
		}

		if (updatedDate != DateTime.MinValue)
		{
			lines.Add($"Last updated on {createdDate.ToString(DivinityApp.DateTimeColumnFormat, CultureInfo.InstalledUICulture)}");
		}

		return String.Join("\n", lines);
	}

	private static bool CheckForInvalidUUID(ValueTuple<string, bool> x)
	{
		var uuid = x.Item1;
		var canAddToLoadOrder = x.Item2;
		if (!canAddToLoadOrder) return false;
		var result = Guid.TryParse(uuid, out _);
		return !result;
	}

	private string BuildMissingDependencyToolTip()
	{
		return $"Missing Dependencies:\n{string.Join(Environment.NewLine, MissingDependencies.Items.Select(x => x.Name).Order())}";
	}

	private static bool CanAllowInLoadOrderCheck(string modType, bool isLarianMod, bool isForceLoaded, bool isMergedMod, bool forceAllowInLoadOrder)
	{
		return modType != "Adventure" && !isLarianMod && (!isForceLoaded || isMergedMod) || forceAllowInLoadOrder;
	}

	public DivinityModData(bool isBaseGameMod = false) : base()
	{
		Index = -1;
		CanDrag = true;

		WorkshopData = new DivinityModWorkshopData();
		NexusModsData = new NexusModsModData();
		ModioData = new ModioModData();
		Metadata = new ModMetadataViewData(this);
		//GithubData = new GithubModData();

		this.WhenAnyValue(x => x.UUID).BindTo(NexusModsData, x => x.UUID);

		this.WhenAnyValue(x => x.NexusModsData.PictureUrl)
			.Select(uri => uri != null && !String.IsNullOrEmpty(uri.AbsolutePath) ? Visibility.Visible : Visibility.Collapsed)
			.ToUIProperty(this, x => x.NexusImageVisibility, Visibility.Collapsed);

		this.WhenAnyValue(x => x.NexusModsData.IsUpdated)
			.Select(b => b ? Visibility.Visible : Visibility.Collapsed)
			.ToUIProperty(this, x => x.NexusModsInformationVisibility, Visibility.Collapsed);

		this.WhenAnyValue(x => x.NexusModsData.CreatedTimestamp)
			.SkipWhile(x => x <= 0)
			.Select(DateUtils.UnixTimeStampToDateTime)
			.ToUIProperty(this, x => x.NexusModsCreatedDate);

		this.WhenAnyValue(x => x.NexusModsData.UpdatedTimestamp)
			.SkipWhile(x => x <= 0)
			.Select(DateUtils.UnixTimeStampToDateTime)
			.ToUIProperty(this, x => x.NexusModsUpdatedDate);

		this.WhenAnyValue(x => x.NexusModsData.UpdatedTimestamp, x => x.LastUpdated)
			.Select(x => x.Item1 > 0 ? (DateTime?)DateUtils.UnixTimeStampToDateTime(x.Item1) : x.Item2)
			.ToUIProperty(this, x => x.DisplayLastUpdated);

		this.WhenAnyValue(x => x.NexusModsCreatedDate, x => x.NexusModsUpdatedDate, x => x.NexusModsData.EndorsementCount)
			.Select(x => NexusModsInfoToTooltip(x.Item1, x.Item2, x.Item3))
			.ToUIProperty(this, x => x.NexusModsTooltipInfo);

		this.WhenAnyValue(x => x.IsForceLoaded, x => x.HasMetadata, x => x.IsForceLoadedMergedMod)
			.Select(b => b.Item1 && b.Item2 && !b.Item3 ? Visibility.Visible : Visibility.Collapsed)
			.ToUIProperty(this, x => x.ToggleForceAllowInLoadOrderVisibility, Visibility.Collapsed);

		this.WhenAnyValue(x => x.NexusModsEnabled, x => x.NexusModsData.ModId, (b, id) => b && id >= DivinityApp.NEXUSMODS_MOD_ID_START)
			.ToUIProperty(this, x => x.CanOpenNexusModsLink);

		this.WhenAnyValue(x => x.CanOpenNexusModsLink)
			.Select(b => b ? Visibility.Visible : Visibility.Collapsed)
			.ToUIProperty(this, x => x.OpenNexusModsLinkVisibility, Visibility.Collapsed);

		// Presentation-only provider label used by the mod list.
		this.WhenAnyValue(x => x.Metadata.Title)
			.ToUIProperty(this, x => x.DisplayTitle, DisplayName);
		this.WhenAnyValue(x => x.IsActive, x => x.Index, x => x.IsForceLoaded, x => x.IsForceLoadedMergedMod, x => x.ForceAllowInLoadOrder)
			.Select(state => state.Item3 && !state.Item4 && !state.Item5
				? "Always loaded"
				: state.Item1 && state.Item2 >= 0
					? state.Item2.ToString(CultureInfo.CurrentCulture)
					: "Not active")
			.ToUIProperty(this, x => x.LoadOrderDisplayText, "Not active");

		this.WhenAnyValue(x => x.Metadata.SourceLabel)
			.ToUIProperty(this, x => x.DisplaySource, "Local");

		var depConn = Dependencies.Connect().ObserveOn(RxApp.MainThreadScheduler);
		depConn.SortAndBind(out displayedDependencies, _moduleSort).DisposeMany().Subscribe();
		depConn.Count().ToUIPropertyImmediate(this, x => x.TotalDependencies);
		this.WhenAnyValue(x => x.TotalDependencies, c => c > 0).ToUIPropertyImmediate(this, x => x.HasDependencies);
		this.WhenAnyValue(x => x.HasDependencies)
			.Select(PropertyConverters.BoolToVisibility)
			.ToUIProperty(this, x => x.DependencyVisibility, Visibility.Collapsed);

		var conConn = this.Conflicts.Connect().ObserveOn(RxApp.MainThreadScheduler);
		conConn.SortAndBind(out displayedConflicts, _moduleSort).DisposeMany().Subscribe();
		conConn.Count().ToUIPropertyImmediate(this, x => x.TotalConflicts);
		this.WhenAnyValue(x => x.TotalConflicts, c => c > 0).ToUIPropertyImmediate(this, x => x.HasConflicts);
		this.WhenAnyValue(x => x.HasConflicts)
			.Select(PropertyConverters.BoolToVisibility)
			.ToUIProperty(this, x => x.ConflictsVisibility, Visibility.Collapsed);

		var whenInvalidUUID = this.WhenAnyValue(x => x.UUID, x => x.CanAddToLoadOrder).Select(CheckForInvalidUUID);
		whenInvalidUUID.ToUIPropertyImmediate(this, x => x.HasInvalidUUID);
		whenInvalidUUID.Select(PropertyConverters.BoolToVisibility).ToUIProperty(this, x => x.HasInvalidUUIDVisibility);

		this.WhenAnyValue(x => x.IsEditorMod, x => x.HasColorblindSupport)
			.Select(x => PropertyConverters.BoolToVisibility(x.Item1 && x.Item2))
			.ToUIProperty(this, x => x.ToolkitIconVisibility, Visibility.Collapsed);

		var missingDepConn = MissingDependencies.Connect().ObserveOn(RxApp.MainThreadScheduler);

		missingDepConn.Count().Select(x => x > 0)
			.ToUIPropertyImmediate(this, x => x.IsMissingDependency);

		this.WhenAnyValue(x => x.IsMissingDependency).Select(PropertyConverters.BoolToVisibility)
			.ToUIProperty(this, x => x.MissingDependencyIconVisibility, Visibility.Collapsed);

		missingDepConn.Select(x => BuildMissingDependencyToolTip())
			.ToUIProperty(this, x => x.MissingDependencyToolTip, string.Empty);

		this.WhenAnyValue(x => x.IsActive, x => x.IsForceLoaded, x => x.IsForceLoadedMergedMod,
			x => x.ForceAllowInLoadOrder).Subscribe((b) =>
			{
				var isActive = b.Item1;
				var isForceLoaded = b.Item2;
				var isForceLoadedMergedMod = b.Item3;
				var forceAllowInLoadOrder = b.Item4;

				if (forceAllowInLoadOrder || isActive)
				{
					CanDrag = true;
				}
				else
				{
					CanDrag = !isForceLoaded || isForceLoadedMergedMod;
				}
			});

		this.WhenAnyValue(x => x.IsForceLoaded, x => x.IsEditorMod, x => x.HasInvalidUUID, x => x.IsMissingDependency).Subscribe((b) =>
		{
			var isForceLoaded = b.Item1;
			var isEditorMod = b.Item2;
			var hasInvalidUUID = b.Item3;
			var isMissingDependency = b.Item4;

			if (hasInvalidUUID || isMissingDependency)
			{
				SelectedColor = "#64f20000";
				ListColor = "#32c10000";
				HasColorOverride = true;
			}
			else if (isForceLoaded)
			{
				// Redux presents pure override mods in their own section. Avoid the
				// legacy full-row amber fill; the list style supplies a subtle,
				// theme-aware Always Loaded accent instead.
				HasColorOverride = false;
			}
			else if (isEditorMod)
			{
				SelectedColor = "#6400ED48";
				ListColor = "#0C00FF4D";
				HasColorOverride = true;
			}
			else
			{
				HasColorOverride = false;
			}
		});

		if (isBaseGameMod)
		{
			this.IsHidden = UUID != DivinityApp.MAIN_CAMPAIGN_UUID;
			this.IsLarianMod = true;
		}

		// If a screen reader is active, don't bother making tooltips for the mod item entry
		this.WhenAnyValue(x => x.Description, x => x.HasDependencies, x => x.UUID).
			Select(x => !DivinityApp.IsScreenReaderActive() && (
			!string.IsNullOrEmpty(x.Item1) || x.Item2 || !string.IsNullOrEmpty(x.Item3)))
			.ToUIPropertyImmediate(this, x => x.HasToolTip, initialValue: true);

		this.WhenAnyValue(x => x.IsEditorMod, x => x.IsLarianMod, x => x.FilePath,
			(isEditorMod, isLarianMod, path) => !isEditorMod && !isLarianMod && File.Exists(path))
			.ToUIPropertyImmediate(this, x => x.CanDelete);

		this.WhenAnyValue(x => x.ModType, x => x.IsLarianMod, x => x.IsForceLoaded, x => x.IsForceLoadedMergedMod, x => x.ForceAllowInLoadOrder, CanAllowInLoadOrderCheck)
			.BindTo(this, x => x.CanAddToLoadOrder);

		var whenExtenderProp = this.WhenAnyValue(x => x.ExtenderModStatus, x => x.ScriptExtenderData.RequiredVersion, x => x.CurrentExtenderVersion);

		whenExtenderProp.Select(x => ExtenderStatusToToolTipText(x.Item1, x.Item2, x.Item3))
			.ToUIProperty(this, x => x.ScriptExtenderSupportToolTipText);

		this.WhenAnyValue(x => x.ExtenderModStatus)
			.Select(x => x != DivinityExtenderModStatus.None ? Visibility.Visible : Visibility.Collapsed)
			.ToUIProperty(this, x => x.ExtenderStatusVisibility, Visibility.Collapsed);

		this.WhenAnyValue(x => x.ExtenderModStatus)
			.Select(ExtenderModStatusToIcon)
			.ToUIPropertyImmediate(this, x => x.ExtenderIcon);

		var whenOsirisStatusChanges = this.WhenAnyValue(x => x.OsirisModStatus);

		whenOsirisStatusChanges.Select(x => x != DivinityOsirisModStatus.NONE ? Visibility.Visible : Visibility.Collapsed)
			.ToUIProperty(this, x => x.OsirisStatusVisibility, Visibility.Collapsed);

		whenOsirisStatusChanges.Select(OsirisStatusToTooltipText)
			.ToUIProperty(this, x => x.OsirisStatusToolTipText);

		ExtenderModStatus = DivinityExtenderModStatus.None;
		OsirisModStatus = DivinityOsirisModStatus.NONE;

		this.WhenAnyValue(x => x.LastUpdated).SkipWhile(x => !x.HasValue)
			.Select(x => $"Last Modified on {x.Value.ToString(DivinityApp.DateTimeColumnFormat, CultureInfo.InstalledUICulture)}")
			.ToUIProperty(this, x => x.LastModifiedDateText, string.Empty);

		this.WhenAnyValue(x => x.FilePath)
			.Select(x => !String.IsNullOrEmpty(x) ? Visibility.Visible : Visibility.Collapsed)
			.ToUIProperty(this, x => x.HasFilePathVisibility, Visibility.Collapsed);

		this.WhenAnyValue(x => x.Version.Version)
			.ToUIProperty(this, x => x.DisplayVersion, "0.0.0.0");
	}
}
