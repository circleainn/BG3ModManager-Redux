using DivinityModManager.Extensions;
using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Models.Extender;
using DivinityModManager.Util;
using DivinityModManager.Views;

using DynamicData.Binding;

using Newtonsoft.Json;

using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace DivinityModManager.ViewModels;

public enum SettingsWindowTab
{
	[Description("Mod Manager Settings")]
	Default = 0,
	[Description("Theme and Appearance")]
	Appearance = 1,
	[Description("Script Extender Settings")]
	Extender = 2,
	[Description("Script Extender Updater Settings")]
	ExtenderUpdater = 3,
	[Description("Keybindings")]
	Keybindings = 4,
	[Description("Advanced Settings")]
	Advanced = 5
}

public class GameLaunchParamEntry : ReactiveObject
{
	[Reactive] public string Name { get; set; }
	[Reactive] public string Description { get; set; }
	[Reactive] public bool DebugModeOnly { get; set; }

	private readonly ObservableAsPropertyHelper<bool> _hasTooltip;
	public bool HasToolTip => _hasTooltip.Value;

	public GameLaunchParamEntry(string name, string description, bool debug = false)
	{
		Name = name;
		Description = description;
		DebugModeOnly = debug;

		_hasTooltip = this.WhenAnyValue(x => x.Description).Select(x => !String.IsNullOrEmpty(x)).ToProperty(this, nameof(HasToolTip), true, RxApp.MainThreadScheduler);
	}
}

public class SettingsWindowViewModel : ReactiveObject
{
	private readonly MainWindowViewModel _main;
	public MainWindowViewModel Main => _main;

	public SettingsWindow View { get; private set; }

	public ObservableCollectionExtended<ScriptExtenderUpdateVersion> ScriptExtenderUpdates { get; private set; }
	[Reactive] public ScriptExtenderUpdateVersion TargetVersion { get; set; }
	[Reactive] public int TargetVersionIndex { get; set; }
	public ObservableCollectionExtended<GameLaunchParamEntry> LaunchParams { get; private set; }

	[Reactive] public SettingsWindowTab SelectedTabIndex { get; set; }
	[Reactive] public Hotkey SelectedHotkey { get; set; }
	[Reactive] public bool HasFetchedManifest { get; set; }
	[Reactive] public bool IsAlertActive { get; set; }

	private readonly ObservableAsPropertyHelper<bool> _isVisible;
	public bool IsVisible => _isVisible.Value;

	private readonly ObservableAsPropertyHelper<bool> _extenderTabIsVisible;
	public bool ExtenderTabIsVisible => _extenderTabIsVisible.Value;

	private readonly ObservableAsPropertyHelper<bool> _keybindingsTabIsVisible;
	public bool KeybindingsTabIsVisible => _keybindingsTabIsVisible.Value;

	private readonly ObservableAsPropertyHelper<Visibility> _developerModeVisibility;
	public Visibility DeveloperModeVisibility => _developerModeVisibility.Value;

	private readonly ObservableAsPropertyHelper<Visibility> _extenderUpdaterVisibility;
	public Visibility ExtenderUpdaterVisibility => _extenderUpdaterVisibility.Value;

	private readonly ObservableAsPropertyHelper<string> _resetSettingsCommandToolTip;
	public string ResetSettingsCommandToolTip => _resetSettingsCommandToolTip.Value;

	private readonly ObservableAsPropertyHelper<string> _extenderSettingsFilePath;
	public string ExtenderSettingsFilePath => _extenderSettingsFilePath.Value;

	private readonly ObservableAsPropertyHelper<string> _extenderUpdaterSettingsFilePath;
	public string ExtenderUpdaterSettingsFilePath => _extenderUpdaterSettingsFilePath.Value;

	private Visibility BoolToVisibility(bool b) => b ? Visibility.Visible : Visibility.Collapsed;

	public ICommand SaveSettingsCommand { get; private set; }
	public ICommand OpenSettingsFolderCommand { get; private set; }
	public ICommand ExportExtenderSettingsCommand { get; private set; }
	public ICommand ExportExtenderUpdaterSettingsCommand { get; private set; }
	public ICommand ResetSettingsCommand { get; private set; }
	public ICommand ClearCacheCommand { get; private set; }
	public ICommand AddLaunchParamCommand { get; private set; }
	public ICommand ClearLaunchParamsCommand { get; private set; }
	public ReactiveCommand<DependencyPropertyChangedEventArgs, Unit> OnWindowShownCommand { get; private set; }

	private readonly ScriptExtenderUpdateVersion _emptyVersion = new();

	private readonly JsonSerializerSettings _jsonConfigExportSettings = new()
	{
		Formatting = Formatting.Indented,
		DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
		MissingMemberHandling = MissingMemberHandling.Ignore,
		Error = delegate (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args)
		{
			DivinityApp.Log(args.ErrorContext.Error.Message);
			args.ErrorContext.Handled = true;
		},
		Converters = [new Newtonsoft.Json.Converters.StringEnumConverter()]
	};

	public void ShowAlert(string message, AlertType alertType = AlertType.Info, int timeout = 0)
	{
		DivinityApp.Log(message);
		RxApp.MainThreadScheduler.Schedule(() =>
		{
			if (timeout < 0) timeout = 0;
			switch (alertType)
			{
				case AlertType.Danger:
					View.AlertBar.SetDangerAlert(message, timeout);
					break;
				case AlertType.Warning:
					View.AlertBar.SetWarningAlert(message, timeout);
					break;
				case AlertType.Success:
					View.AlertBar.SetSuccessAlert(message, timeout);
					break;
				case AlertType.Info:
				default:
					View.AlertBar.SetInformationAlert(message, timeout);
					break;
			}
		});
	}

	private string SelectedTabToResetTooltip(SettingsWindowTab tab)
	{
		var name = TabToName(tab);
		return $"Reset {name}";
	}

	private string TabToName(SettingsWindowTab tab) => tab.GetDescription();

	private void CheckForGameVersionMismatch(ExtenderUpdateChannel channel, ScriptExtenderUpdateVersion version)
	{
		//Check manually set versions vs the min game version
		if (!String.IsNullOrEmpty(version.MinGameVersion) && File.Exists(Settings.GameExecutablePath))
		{
			var gameFileVersionInfo = FileVersionInfo.GetVersionInfo(Settings.GameExecutablePath);
			var gameCurrentVersion = new Version(string.Format("{0}.{1}.{2}.{3}", gameFileVersionInfo.FileMajorPart, gameFileVersionInfo.FileMinorPart, gameFileVersionInfo.FileBuildPart, gameFileVersionInfo.FilePrivatePart));
			var gameMinVersion = new Version(version.MinGameVersion);
			if (gameCurrentVersion < gameMinVersion && _lastWarnedVersion != gameCurrentVersion)
			{
				_lastWarnedVersion = gameCurrentVersion;
				Xceed.Wpf.Toolkit.MessageBox.Show(View,
					$"Script Extender {version.Version} ({channel}) requires at minimum game version {version.MinGameVersion}. The current game version is {gameCurrentVersion}.",
					"Old Game Detected", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
					Main.View.MainWindowMessageBox_OK.Style);
			}
		}
	}

	public async Task GetExtenderUpdatesAsync(ExtenderUpdateChannel channel, CancellationToken token)
	{
		var url = String.Format(DivinityApp.EXTENDER_MANIFESTS_URL, channel.GetDescription());
		DivinityApp.Log($"Checking for script extender manifest info at '{url}'");
		var text = await WebHelper.DownloadUrlAsStringAsync(url, token);
		if (token.IsCancellationRequested) return;

		if (!String.IsNullOrEmpty(text))
		{
			if (DivinityJsonUtils.TrySafeDeserialize<ScriptExtenderUpdateData>(text, out var data))
			{
				var res = data.Resources.FirstOrDefault();
				if (res != null)
				{
					var lastVersion = ExtenderUpdaterSettings.TargetVersion;
					var lastDigest = ExtenderUpdaterSettings.TargetResourceDigest;
					var lastBuildDate = TargetVersion != _emptyVersion ? TargetVersion?.BuildDate : null;
					RxApp.MainThreadScheduler.Schedule(() =>
					{
						ScriptExtenderUpdateVersion nextVersion = null;
						TargetVersion = null;
						ScriptExtenderUpdates.Clear();
						ScriptExtenderUpdates.Add(_emptyVersion);
						//ScriptExtenderUpdates.AddRange(res.Versions.OrderByDescending(x => x.BuildDate));
						ScriptExtenderUpdates.AddRange(res.Versions.OrderByDescending(x => x.BuildDate));
						if (lastBuildDate != null)
						{
							nextVersion = ScriptExtenderUpdates.FirstOrDefault(x => x.BuildDate == lastBuildDate);
						}
						if (nextVersion == null && !String.IsNullOrEmpty(lastDigest))
						{
							nextVersion = ScriptExtenderUpdates.FirstOrDefault(x => x.Digest == lastDigest);
						}
						if (nextVersion == null && !String.IsNullOrEmpty(lastVersion))
						{
							nextVersion = ScriptExtenderUpdates.FirstOrDefault(x => x.Version == lastVersion);
						}
						nextVersion ??= _emptyVersion;

						if (nextVersion != _emptyVersion)
						{
							CheckForGameVersionMismatch(channel, nextVersion);
						}

						HasFetchedManifest = true;
						TargetVersion = nextVersion;
					});
				}
			}
		}
	}

	private IDisposable _manifestFetchingTask;
	private long _lastManifestCheck = -1;

	private bool CanCheckManifest => _lastManifestCheck == -1 || DateTimeOffset.Now.ToUnixTimeSeconds() - _lastManifestCheck >= 3000;

	public void StartExtenderManifestCheck(ExtenderUpdateChannel channel)
	{
		_manifestFetchingTask?.Dispose();
		_lastManifestCheck = DateTimeOffset.Now.ToUnixTimeSeconds();
		_manifestFetchingTask = RxApp.TaskpoolScheduler.ScheduleAsync(async (sch, cts) => await GetExtenderUpdatesAsync(channel, cts));
	}

	private void FetchLatestManifestData(ExtenderUpdateChannel channel, bool force = false)
	{
		if (force || CanCheckManifest)
		{
			StartExtenderManifestCheck(channel);
		}
	}

	private void OnWindowVisibilityChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue is bool b && b == true)
		{
			StartExtenderManifestCheck(ExtenderUpdaterSettings.UpdateChannel);
		}
	}

	public DivinityModManagerSettings Settings { get; private set; }
	public ScriptExtenderSettings ExtenderSettings { get; private set; }
	public ScriptExtenderUpdateConfig ExtenderUpdaterSettings { get; private set; }

	private Version _lastWarnedVersion;

	public void OnTargetVersionSelected(ScriptExtenderUpdateVersion entry)
	{
		if (entry != _emptyVersion)
		{
			ExtenderUpdaterSettings.TargetVersion = entry.Version;
			ExtenderUpdaterSettings.TargetResourceDigest = entry.Digest;
			CheckForGameVersionMismatch(ExtenderUpdaterSettings.UpdateChannel, entry);
		}
		else
		{
			ExtenderUpdaterSettings.TargetVersion = "";
			ExtenderUpdaterSettings.TargetResourceDigest = "";
		}
	}

	public bool ExportExtenderSettings()
	{
		var gameExeDirectory = Path.GetDirectoryName(Settings.GameExecutablePath.ToRealPath());
		var outputFile = Path.Join(gameExeDirectory, DivinityApp.EXTENDER_CONFIG_FILE);
		try
		{
			_jsonConfigExportSettings.DefaultValueHandling = ExtenderSettings.ExportDefaultExtenderSettings ? DefaultValueHandling.Include : DefaultValueHandling.Ignore;
			var contents = JsonConvert.SerializeObject(Settings.ExtenderSettings, _jsonConfigExportSettings);
			File.WriteAllText(outputFile, contents);
			DivinityApp.Log($"ExtenderUpdaterSettings differs? {ExtenderUpdaterSettings == Settings.ExtenderUpdaterSettings}");
			ShowAlert($"Saved Script Extender settings to '{outputFile}'", AlertType.Success, 20);
			return true;
		}
		catch (Exception ex)
		{
			ShowAlert($"Error saving Script Extender settings to '{outputFile}':\n{ex}", AlertType.Danger);
		}
		return false;
	}

	public bool ExportExtenderUpdaterSettings()
	{
		var gameExeDirectory = Path.GetDirectoryName(Settings.GameExecutablePath.ToRealPath());
		var outputFile = Path.Join(gameExeDirectory, DivinityApp.EXTENDER_UPDATER_CONFIG_FILE);
		try
		{
			_jsonConfigExportSettings.DefaultValueHandling = ExtenderSettings.ExportDefaultExtenderSettings ? DefaultValueHandling.Include : DefaultValueHandling.Ignore;
			var contents = JsonConvert.SerializeObject(ExtenderUpdaterSettings, _jsonConfigExportSettings);
			File.WriteAllText(outputFile, contents);

			Main.UpdateExtender(true);

			return true;
		}
		catch (Exception ex)
		{
			ShowAlert($"Error saving Script Extender Updater settings to '{outputFile}':\n{ex}", AlertType.Danger);
		}
		return false;
	}

	public void SaveSettings()
	{
		try
		{
			var attr = File.GetAttributes(Settings.GameExecutablePath);
			if (attr.HasFlag(FileAttributes.Directory))
			{
				string exeName = "";
				if (!DivinityRegistryHelper.IsGOG)
				{
					exeName = Path.GetFileName(Main.AppSettings.DefaultPathways.Steam.ExePath);
				}
				else
				{
					exeName = Path.GetFileName(Main.AppSettings.DefaultPathways.GOG.ExePath);
				}

				var exe = Path.Combine(Settings.GameExecutablePath, exeName);
				if (File.Exists(exe))
				{
					Settings.GameExecutablePath = exe;
				}
			}
		}
		catch (Exception) { }

		var savedMainSettings = Main.SaveSettings();

		if (View.IsVisible)
		{
				switch (SelectedTabIndex)
				{
					case SettingsWindowTab.Default:
					case SettingsWindowTab.Appearance:
					case SettingsWindowTab.Advanced:
					if (savedMainSettings && !IsAlertActive) ShowAlert("Saved settings.", AlertType.Success, 10);
					break;
				case SettingsWindowTab.Extender:
					ExportExtenderSettings();
					break;
				case SettingsWindowTab.ExtenderUpdater:
					ExportExtenderUpdaterSettings();
					break;
				case SettingsWindowTab.Keybindings:
					var success = Main.Keys.SaveKeybindings(out var msg);
					if (!success)
					{
						ShowAlert(msg, AlertType.Danger);
					}
					else if (!String.IsNullOrEmpty(msg))
					{
						ShowAlert(msg, AlertType.Success, 10);
					}
					break;
			}
		}
		else
		{
			Main.SaveSettings();
		}
	}

	public SettingsWindowViewModel(SettingsWindow view, MainWindowViewModel main)
	{
		_main = main;
		View = view;

		_isVisible = this.WhenAnyValue(x => x.View.IsVisible).ToProperty(this, nameof(IsVisible));

		Settings = Main.Settings;
		ExtenderSettings = Main.Settings.ExtenderSettings;
		ExtenderUpdaterSettings = Main.Settings.ExtenderUpdaterSettings;

		ScriptExtenderUpdates = [_emptyVersion];
		TargetVersion = _emptyVersion;
		TargetVersionIndex = 0;

		LaunchParams =
		[
			new("-continueGame", "Automatically load the last save when loading into the main menu"),
			new("-storylog 1", "Enables the story log"),
			new(@"--logPath """, "A directory to write story logs to"),
			new("--cpuLimit x", "Limit the cpu to x amount of threads (unknown if this works)"),
			new("-asserts 1", "", true),
			new("-stats 1", "", true),
			new("-dynamicStory 1", "", true),
			new("-externalcrashhandler", "", true),
			new(@"-nametag """, "", true),
			new(@"-module """, "", true),
			new(@"+connect_lobby """, "", true),
			new("-locaupdater 1", "", true),
			new(@"-mediaPath """, "", true),
		];

		var whenTab = this.WhenAnyValue(x => x.SelectedTabIndex);
		_extenderTabIsVisible = whenTab.Select(x => x == SettingsWindowTab.Extender).ToProperty(this, nameof(ExtenderTabIsVisible));
		_keybindingsTabIsVisible = whenTab.Select(x => x == SettingsWindowTab.Keybindings).ToProperty(this, nameof(KeybindingsTabIsVisible));

		this.WhenAnyValue(x => x.TargetVersion).WhereNotNull().ObserveOn(RxApp.MainThreadScheduler).Subscribe(OnTargetVersionSelected);

		_resetSettingsCommandToolTip = this.WhenAnyValue(x => x.SelectedTabIndex).Select(SelectedTabToResetTooltip).ToProperty(this, nameof(ResetSettingsCommandToolTip), scheduler: RxApp.MainThreadScheduler);

		_developerModeVisibility = ExtenderSettings.WhenAnyValue(x => x.DeveloperMode).Select(BoolToVisibility).ToProperty(this, nameof(DeveloperModeVisibility), scheduler: RxApp.MainThreadScheduler);

		_extenderUpdaterVisibility = this.WhenAnyValue(x => x.Settings.DebugModeEnabled, x => x.ExtenderSettings.DeveloperMode)
			.Select(x => BoolToVisibility(x.Item1 || x.Item2)).ToProperty(this, nameof(ExtenderUpdaterVisibility), true, RxApp.MainThreadScheduler);

		ExtenderUpdaterSettings.WhenAnyValue(x => x.UpdateChannel).Subscribe((channel) =>
		{
			if (IsVisible)
			{
				FetchLatestManifestData(channel, true);
			}
		});

		var whenExePath = Settings.WhenAnyValue(x => x.GameExecutablePath).WhereNotNull();
		_extenderSettingsFilePath = whenExePath.Select(x => Path.Combine(Path.GetDirectoryName(x), DivinityApp.EXTENDER_CONFIG_FILE)).ToProperty(this, nameof(ExtenderSettingsFilePath), true, RxApp.MainThreadScheduler);
		_extenderUpdaterSettingsFilePath = whenExePath.Select(x => Path.Combine(Path.GetDirectoryName(x), DivinityApp.EXTENDER_UPDATER_CONFIG_FILE)).ToProperty(this, nameof(ExtenderUpdaterSettingsFilePath), true, RxApp.MainThreadScheduler);

		var settingsProperties = new HashSet<string>();
		settingsProperties.UnionWith(Settings.GetSettingsAttributes().Select(x => x.Property.Name));
		settingsProperties.UnionWith(ExtenderSettings.GetSettingsAttributes().Select(x => x.Property.Name));
		settingsProperties.UnionWith(ExtenderUpdaterSettings.GetSettingsAttributes().Select(x => x.Property.Name));

		var whenVisible = this.WhenAnyValue(x => x.IsVisible, (b) => b == true);
		var propertyChanged = nameof(ReactiveObject.PropertyChanged);
		var whenSettings = Observable.FromEventPattern<PropertyChangedEventArgs>(Settings, propertyChanged);
		var whenExtenderSettings = Observable.FromEventPattern<PropertyChangedEventArgs>(ExtenderSettings, propertyChanged);
		var whenExtenderUpdaterSettings = Observable.FromEventPattern<PropertyChangedEventArgs>(ExtenderUpdaterSettings, propertyChanged);

		SaveSettingsCommand = ReactiveCommand.Create(SaveSettings, whenVisible);
		Observable.Merge(whenSettings, whenExtenderSettings, whenExtenderUpdaterSettings)
			.Where(e => settingsProperties.Contains(e.EventArgs.PropertyName))
			.Throttle(TimeSpan.FromMilliseconds(100))
			.Do(x => DivinityApp.Log($"Autosaving due to {x.EventArgs.PropertyName} changing"))
			.Select(x => Unit.Default)
			.InvokeCommand(SaveSettingsCommand);

		OpenSettingsFolderCommand = ReactiveCommand.Create(() =>
		{
			ProcessHelper.TryOpenPath(DivinityApp.GetAppDirectory(DivinityApp.DIR_DATA));
		});

		ResetSettingsCommand = ReactiveCommand.Create(() =>
		{
			var tabName = TabToName(SelectedTabIndex);
			MessageBoxResult result = Xceed.Wpf.Toolkit.MessageBox.Show(View, $"Reset {tabName} to Default?\nCurrent settings will be lost.", $"Confirm {tabName} Reset",
				MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No, Main.View.MainWindowMessageBox_OK.Style);
			if (result == MessageBoxResult.Yes)
			{
				switch (SelectedTabIndex)
				{
					case SettingsWindowTab.Default:
						Settings.SetToDefault();
						break;
					case SettingsWindowTab.Appearance:
						Settings.ActiveCustomThemeId = String.Empty;
						Settings.ColorTheme = ReduxThemeType.ReduxDark;
						Settings.TypographyFont = ReduxTypographyFont.Manrope;
						Settings.TextSize = ReduxTextSize.Default;
						break;
					case SettingsWindowTab.Extender:
						Settings.ExtenderSettings.SetToDefault();
						break;
					case SettingsWindowTab.ExtenderUpdater:
						Settings.ExtenderUpdaterSettings.SetToDefault();
						break;
					case SettingsWindowTab.Keybindings:
						Main.Keys.SetToDefault();
						break;
					case SettingsWindowTab.Advanced:
						Settings.DebugModeEnabled = false;
						Settings.LogEnabled = false;
						Settings.GameLaunchParams = "";
						break;
				}
			}
		});

		ClearCacheCommand = ReactiveCommand.Create(() =>
		{
			MessageBoxResult result = Xceed.Wpf.Toolkit.MessageBox.Show(View, $"Delete local mod cache?\nThis cannot be undone.", "Confirm Delete Cache",
				MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No, Main.View.MainWindowMessageBox_OK.Style);
			if (result == MessageBoxResult.Yes)
			{
				try
				{
					if (Main.UpdateHandler.DeleteCache())
					{
						ShowAlert($"Deleted local cache in {DivinityApp.GetAppDirectory("Data")}", AlertType.Success, 20);
					}
					else
					{
						ShowAlert($"No cache to delete.", AlertType.Warning, 20);
					}
				}
				catch (Exception ex)
				{
					ShowAlert($"Error deleting workshop cache:\n{ex}", AlertType.Danger);
				}
			}
		});

		AddLaunchParamCommand = ReactiveCommand.Create((string param) =>
		{
			if (Settings.GameLaunchParams == null) Settings.GameLaunchParams = "";
			if (Settings.GameLaunchParams.IndexOf(param) < 0)
			{
				if (String.IsNullOrWhiteSpace(Settings.GameLaunchParams))
				{
					Settings.GameLaunchParams = param;
				}
				else
				{
					Settings.GameLaunchParams = Settings.GameLaunchParams + " " + param;
				}
			}
		});

		ClearLaunchParamsCommand = ReactiveCommand.Create(() =>
		{
			Settings.GameLaunchParams = "";
		});

		OnWindowShownCommand = ReactiveCommand.Create<DependencyPropertyChangedEventArgs>(OnWindowVisibilityChanged);

		this.WhenAnyValue(x => x.Settings.DocumentsFolderPathOverride)
		.Skip(1)
		.Throttle(TimeSpan.FromSeconds(1))
		.ObserveOn(RxApp.MainThreadScheduler).Subscribe(x =>
		{
			if (IsVisible)
			{
				if(String.IsNullOrEmpty(x))
				{
					ShowAlert($"AppData path override cleared - Make sure to refresh", AlertType.Warning, 60);
				}
				else
				{
					ShowAlert($"AppData path override changed to '{x}' - Make sure to refresh", AlertType.Warning, 60);
				}
			}
		});
	}
}
