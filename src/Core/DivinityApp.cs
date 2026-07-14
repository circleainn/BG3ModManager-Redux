

using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using DynamicData;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DivinityModManager;

public static class DivinityApp
{
	public const string REDUX_DISPLAY_VERSION = "0.1.0-alpha.2";
	public const string REDUX_INTERNAL_VERSION = "0.1.0.2";
	public static readonly bool REDUX_APPLICATION_UPDATES_ENABLED = false;

	public const string DIR_DATA = "Data\\";
	public const string URL_REPO = @"https://github.com/LaughingLeader/BG3ModManager";
	public const string URL_REDUX_REPO = @"https://github.com/raincloudsfollow/BG3ModManager-Redux";
	public const string URL_REDUX_AUTHOR = @"https://github.com/raincloudsfollow";
	public const string URL_REDUX_RELEASES = @"https://github.com/raincloudsfollow/BG3ModManager-Redux/releases";
	public const string URL_REDUX_ISSUES = @"https://github.com/raincloudsfollow/BG3ModManager-Redux/issues";
	public const string URL_REDUX_LICENSE = @"https://github.com/raincloudsfollow/BG3ModManager-Redux/blob/master/LICENSE";
	public const string URL_BG3_NEXUS = @"https://www.nexusmods.com/baldursgate3";
	public const string URL_EXTENDER_REPO = @"https://github.com/Norbyte/bg3se";
	public const string URL_CHANGELOG = URL_REDUX_RELEASES;
	public const string URL_CHANGELOG_RAW = @"https://raw.githubusercontent.com/raincloudsfollow/BG3ModManager-Redux/master/README.md";
	public const string URL_UPDATE = @"https://raw.githubusercontent.com/raincloudsfollow/BG3ModManager-Redux/master/Update.xml";
	public const string URL_AUTHOR = @"https://github.com/LaughingLeader";
	public const string URL_ISSUES = URL_REDUX_ISSUES;
	public const string URL_LICENSE = URL_REDUX_LICENSE;
	public const string URL_DONATION = @"https://ko-fi.com/laughingleader";

	public const string GITHUB_USER = "raincloudsfollow";
	public const string GITHUB_REPO = "BG3ModManager-Redux";
	public const string GITHUB_RELEASE_ASSET = "BG3ModManager-Redux-Latest.zip";

	public const string XML_MOD_ORDER_MODULE = @"<node id=""Module""><attribute id=""UUID"" type=""guid"" value=""{0}""/></node>";
	public const string XML_MODULE_SHORT_DESC = @"<node id=""ModuleShortDesc""><attribute id=""Folder"" type=""LSString"" value=""{0}""/><attribute id=""MD5"" type=""LSString"" value=""{1}""/><attribute id=""Name"" type=""LSString"" value=""{2}""/><attribute id=""PublishHandle"" type=""uint64"" value=""{5}""/><attribute id=""UUID"" type=""guid"" value=""{3}""/><attribute id=""Version64"" type=""int64"" value=""{4}""/></node>";
	public const string XML_MOD_SETTINGS_TEMPLATE = @"<?xml version=""1.0"" encoding=""UTF-8""?><save><version major=""4"" minor=""8"" revision=""0"" build=""100""/><region id=""ModuleSettings""><node id=""root""><children><node id=""Mods""><children>{0}</children></node></children></node></region></save>";

	public const string PATH_RESOURCES = "Resources";
	public const string PATH_APP_FEATURES = "AppFeatures.json";
	public const string PATH_DEFAULT_PATHWAYS = "DefaultPathways.json";
	public const string PATH_IGNORED_MODS = "IgnoredMods.json";

	public const string PATH_LAST_EXPORTED_NAME = "LastExported";

	//GustavX
	public const string GUSTAV_UUID = "991c9c7a-fb80-40cb-8f0d-b92d4e80e9b1";
	public const string GUSTAVDEV_UUID = "28ac9ce2-2aba-8cda-b3b5-6e922f71b6b8";
	public const string GUSTAVX_UUID = "cb555efe-2d9e-131f-8195-a89329d218ea";
	public const string MAIN_CAMPAIGN_UUID = GUSTAVX_UUID;

	public const string NEXUSMODS_GAME_DOMAIN = "baldursgate3";
	public const long NEXUSMODS_GAME_ID = 3474;
	public const string NEXUSMODS_MOD_URL = "https://www.nexusmods.com/baldursgate3/mods/{0}";
	public const long NEXUSMODS_MOD_ID_START = 1;

	public const string EXTENDER_REPO_URL = "Norbyte/bg3se";
	public const string EXTENDER_LATEST_URL = "https://github.com/Norbyte/bg3se/releases/latest";
	public const string EXTENDER_APPDATA_DIRECTORY = "BG3ScriptExtender";
	public const string EXTENDER_APPDATA_DLL = "BG3ScriptExtender.dll";
	public const string EXTENDER_MOD_CONFIG = "ScriptExtender/Config.json";
	public const string EXTENDER_UPDATER_FILE = "DWrite.dll";
	public const string EXTENDER_MANIFESTS_URL = "https://bg3se-updates.norbyte.dev/Channels/{0}/Manifest.json";
	public const string EXTENDER_CONFIG_FILE = "ScriptExtenderSettings.json";
	public const string EXTENDER_UPDATER_CONFIG_FILE = "ScriptExtenderUpdaterConfig.json";
	public const int EXTENDER_DEFAULT_VERSION = 6;

	public const int MAX_FILE_OVERRIDE_DISPLAY = 10;

	public const LSLib.LS.Enums.Game GAME = LSLib.LS.Enums.Game.BaldursGate3;
	public const LSLib.LS.Story.Compiler.TargetGame GAME_COMPILER = LSLib.LS.Story.Compiler.TargetGame.BG3;

	public static readonly Uri LightTheme = new("pack://application:,,,/BG3ModManager;component/Themes/Light.xaml", UriKind.Absolute);
	public static readonly Uri DarkTheme = new("pack://application:,,,/BG3ModManager;component/Themes/Dark.xaml", UriKind.Absolute);
	public static readonly Uri ParchmentTheme = new("pack://application:,,,/BG3ModManager;component/Themes/Parchment.xaml", UriKind.Absolute);

	public static Uri GetThemeUri(ReduxThemeType theme) => theme switch
	{
		ReduxThemeType.ReduxLight => LightTheme,
		ReduxThemeType.Parchment => ParchmentTheme,
		_ => DarkTheme
	};

	public static SourceCache<DivinityModData, string> IgnoredMods { get; } = new(x => x.UUID);
	public static HashSet<string> IgnoredDependencyMods { get; } = [];

	public static DivinityGlobalCommands Commands { get; private set; } = new DivinityGlobalCommands();
	public static DivinityGlobalEvents Events { get; private set; } = new DivinityGlobalEvents();

	public static event PropertyChangedEventHandler StaticPropertyChanged;

	private static void NotifyStaticPropertyChanged([CallerMemberName] string name = null)
	{
		StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(name));
	}

	private static bool developerModeEnabled = false;

	public static bool DeveloperModeEnabled
	{
		get => developerModeEnabled;
		set
		{
			developerModeEnabled = value;
			NotifyStaticPropertyChanged();
		}
	}

	private static bool _isKeyboardNavigating = false;

	public static bool IsKeyboardNavigating
	{
		get => _isKeyboardNavigating;
		set
		{
			_isKeyboardNavigating = value;
			NotifyStaticPropertyChanged();
		}
	}

	public static bool WorkshopEnabled { get; set; }
	public static bool NexusModsEnabled { get; set; }

	public static IObservable<Func<ModuleShortDesc, bool>> DependencyFilter { get; set; }

	public static string DateTimeColumnFormat { get; set; } = "MM/dd/yyyy";
	public static string DateTimeTooltipFormat { get; set; } = "MMMM dd, yyyy";
	public static string DateTimeExtenderBuildFormat { get; set; } = "MM/dd/yyyy hh:mm tt";

	public static void Log(string msg, [CallerMemberName] string mName = "", [CallerFilePath] string path = "", [CallerLineNumber] int line = 0)
	{
		System.Diagnostics.Trace.WriteLine($"[{Path.GetFileName(path)}:{mName}({line})] {msg}");
	}

	public static bool IsScreenReaderActive()
	{
		return Services.Get<IScreenReaderService>()?.IsScreenReaderActive() == true;
	}

	public static string GetAppDirectory()
	{
		//The probing path may differ is running via Proton, so try and get it via the process path instead
		var processPath = Environment.ProcessPath;
		if(!String.IsNullOrEmpty(processPath))
		{
			Path.GetFullPath(Path.GetDirectoryName(processPath));
		}
		return Path.GetFullPath(AppContext.BaseDirectory);
	}

	public static string GetAppDirectory(params string[] joinPath)
	{
		var exeDir = GetAppDirectory();
		var paths = joinPath.Prepend(exeDir).ToArray();
		return Path.Combine(paths);
	}
}
