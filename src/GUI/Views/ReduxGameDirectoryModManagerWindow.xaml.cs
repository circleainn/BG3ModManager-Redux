using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using Microsoft.Win32;

using System.Diagnostics;
using System.Windows;

namespace DivinityModManager.Views;

public sealed record ReduxGameDirectoryModListItem(
	ReduxGameDirectoryModEntry Entry,
	string FileSummary,
	string SourceUrl,
	string Summary,
	string DetailsText,
	string ThumbnailUrl)
{
	public bool IsYanml { get; init; }
	public string Name { get; init; } = Entry.Name;
	public ReduxGameDirectoryModStatus Status => Entry.Status;
	public string StatusText => IsExternalReplacement ? "Backup unavailable" : Status switch
	{
		ReduxGameDirectoryModStatus.Managed => "Managed",
		ReduxGameDirectoryModStatus.External => "Unmanaged",
		ReduxGameDirectoryModStatus.Changed => "Files changed",
		ReduxGameDirectoryModStatus.Missing => "Missing files",
		ReduxGameDirectoryModStatus.RecoveryRequired => "Recovery needed",
		_ => Entry.StatusText
	};
	public bool CanRestore => Entry.CanRestore;
	public bool CanAdopt => Entry.CanAdopt;
	public bool IsAdopted => Entry.ArchiveName == "Adopted external installation";
	public bool IsExternalReplacement => Entry.Status == ReduxGameDirectoryModStatus.External
		&& ReduxGameDirectoryModCatalog.Find(Entry.NexusModId)?.ReplacesExistingGameFiles == true;
	public string ManagementNote => CanAdopt
		? "Redux recognizes this exact reviewed DLL. Manage it without changing the installed file."
		: IsExternalReplacement
			? "This Redux installation has no protected original backups. If another Redux copy installed this mod, manage it there. Otherwise, remove it, verify BG3's files in Steam or GOG, then install it through this copy."
			: Status == ReduxGameDirectoryModStatus.External
				? "Redux cannot manage this installation because its DLL does not match a reviewed version. Remove it manually before installing a reviewed archive."
				: Status is ReduxGameDirectoryModStatus.Changed or ReduxGameDirectoryModStatus.Missing
					? "Redux owns this installation, but its managed DLL changed or is missing. Reinstall the reviewed release to repair it."
					: StatusText;
	public bool HasSource => !String.IsNullOrWhiteSpace(SourceUrl);
}

public partial class ReduxGameDirectoryModManagerWindow : AdonisUI.Controls.AdonisWindow
{
	private const long ScriptExtenderNexusModId = 2172;
	private readonly MainWindowViewModel _viewModel;
	private readonly ReduxGameDirectoryInstallService _installer;
	private ReduxGameDirectoryInstallService _yanmlInstaller;
	private readonly Dictionary<long, NexusModsModData> _sourceDetails = new();
	private readonly CancellationTokenSource _sourceDetailsCancellation = new();

	public ReduxGameDirectoryModManagerWindow()
	{
		InitializeComponent();
		ReduxExternalDropFeedback.Attach(this, paths => paths.Length == 1, "Drop to review a native archive", "Redux.Icon.GameController", "Redux will review the archive and its destination before installing.");
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
	}

	public ReduxGameDirectoryModManagerWindow(MainWindow owner, MainWindowViewModel viewModel, bool focusScriptExtender = false)
		: this()
	{
		Owner = owner;
		_viewModel = viewModel;
		ReduxThemeService.Apply(Resources, viewModel.Settings.ColorTheme,
			ReduxThemeService.GetActiveTheme(viewModel.Settings), viewModel.Settings.UsesGeneratedGradients);
		_installer = CreateInstaller(viewModel);
		GamePathText.Text = _installer.GameBin;
		RefreshList();
		Loaded += async (_, _) =>
		{
			await LoadSourceDetailsAsync();
			if (focusScriptExtender)
			{
				ScriptExtenderButton.BringIntoView();
				ScriptExtenderButton.Focus();
			}
		};
		Closed += (_, _) => _sourceDetailsCancellation.Cancel();
	}

	public static bool CanOpen(MainWindowViewModel viewModel) =>
		viewModel?.Settings != null
		&& !String.IsNullOrWhiteSpace(viewModel.Settings.GameExecutablePath)
		&& File.Exists(Environment.ExpandEnvironmentVariables(viewModel.Settings.GameExecutablePath));

	public static async Task<bool> ReviewAndInstallAsync(
		Window owner,
		MainWindowViewModel viewModel,
		string archivePath,
		bool preferYanml = false)
	{
		ReduxGameDirectoryArchiveInspection inspection;
		try
		{
			inspection = await Task.Run(() => ReduxGameDirectoryInstallService.TryInspectKnownArchive(archivePath));
			if (inspection == null)
			{
				ReduxMessageBox.Show(owner,
					"This archive does not match a reviewed game-directory package. Redux did not change any files.",
					"Package Not Recognized", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
				return false;
			}
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			ReduxMessageBox.Show(owner, ex.Message, "Could Not Inspect Archive",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
			return false;
		}

		ReduxGameDirectoryInstallService installer;
		ReduxGameDirectoryInstallTransaction transaction;
		try
		{
			installer = CreateInstallerForArchive(viewModel, inspection.Definition, preferYanml);
			transaction = await installer.StageAsync(inspection.Definition.NexusModId, archivePath);
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			ReduxMessageBox.Show(owner, ex.Message, "Could Not Stage Game-Directory Changes",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
			return false;
		}

		await using (transaction)
		{
			var existingEntry = installer.GetInstalledMods().FirstOrDefault(entry =>
				entry.Status != ReduxGameDirectoryModStatus.External
					&& String.Equals(entry.PackageId, inspection.Definition.PackageId, StringComparison.OrdinalIgnoreCase));
			var isUpdate = existingEntry != null;
			var isRepair = existingEntry?.Status is ReduxGameDirectoryModStatus.Changed or ReduxGameDirectoryModStatus.Missing;
			var reviewItems = inspection.ManagedFiles.Select(file =>
			{
				var relative = file.DestinationPath.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
					? file.DestinationPath[4..] : file.DestinationPath;
				var destination = installer.GetManagedFilePath(file.DestinationPath);
				var preserve = file.PreserveExisting && File.Exists(destination);
				var replacesExisting = inspection.Definition.ReplacesExistingGameFiles && File.Exists(destination) && !preserve;
				return new ReduxInstallReviewItem(
					Path.GetFileName(file.DestinationPath),
					installer.NativePluginDestination == ReduxNativePluginDestination.YanmlPlugins
						? $"Destination: YANML Plugins\\{relative[("NativeMods/".Length)..].Replace('/', '\\')}"
						: $"Destination: BG3\\bin\\{relative.Replace('/', '\\')}",
					preserve ? "Keep existing settings" : replacesExisting ? "Replace file · protect original" : isRepair ? "Repair managed file" : isUpdate ? "Update managed file" : "Install managed file",
					isRepair ? ReduxInstallReviewTone.Warning : preserve || replacesExisting ? ReduxInstallReviewTone.Info
						: isUpdate ? ReduxInstallReviewTone.Success : ReduxInstallReviewTone.Info);
			}).ToList();
			reviewItems.AddRange(inspection.PackageEntries.Select(package => new ReduxInstallReviewItem(
				Path.GetFileName(package),
				"Companion PAK · installs through Redux's normal Mods-folder workflow",
				"Install as inactive mod",
				ReduxInstallReviewTone.Info)));
			if (inspection.Definition.Kind == ReduxGameDirectoryModKind.NativeLoader
				&& ReduxAlternativeNativeLoader.HasYanmlConfiguration(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)))
				reviewItems.Add(new ReduxInstallReviewItem("Loader conflict",
				"A YANML configuration exists on this PC. YANML's author advises against running it alongside Native Mod Loader.",
				"Verify that YANML is fully uninstalled before continuing", ReduxInstallReviewTone.Warning));
			if (installer.NativePluginDestination == ReduxNativePluginDestination.YanmlPlugins
				&& CreateInstaller(viewModel).DetectLoader().IsPresent)
				reviewItems.Add(new ReduxInstallReviewItem("Loader conflict",
				"Native Mod Loader is also present in the game folder. YANML's author advises against running both loaders.",
				"Remove or disable one loader before launching BG3", ReduxInstallReviewTone.Warning));
			if (installer.NativePluginDestination == ReduxNativePluginDestination.YanmlPlugins
				&& inspection.ManagedFiles.Any(file => file.DestinationPath.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
					|| file.DestinationPath.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)))
				reviewItems.Add(new ReduxInstallReviewItem("Plugin settings",
				"Some plugins still read settings from BG3\\bin\\NativeMods even when YANML loads their DLL from the local Plugins folder.",
				"Check the plugin's settings path if it loads but ignores configuration", ReduxInstallReviewTone.Info));

			var summary = $"{(isRepair ? "Managed repair" : isUpdate ? "Managed update" : "New managed install")} · {inspection.Definition.Name} · {inspection.LayoutName} · {inspection.FileCount} files · "
				+ $"{FormatBytes(inspection.ExpandedBytes)} expanded";
			var dialog = new ReduxInstallReviewWindow(owner, reviewItems,
				ReduxInstallReviewKind.GameDirectory,
				installer.NativePluginDestination == ReduxNativePluginDestination.YanmlPlugins
					? installer.NativePluginDirectory : installer.GameBin, summary);
			if (dialog.ShowDialog() != true && !dialog.Accepted) return false;

			try
			{
				await transaction.CommitAsync();
				if (inspection.PackageEntries.Count > 0)
					viewModel.ImportMods([archivePath], false);
				viewModel.ShowAlert($"Installed {inspection.Definition.Name}.", AlertType.Success, 20);
				return true;
			}
			catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
			{
				var title = ex is ReduxGameDirectoryRecoveryException
					? "Recovery Required" : "Game-Directory Install Stopped";
				ReduxMessageBox.Show(owner, ex.Message, title,
					MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
				return false;
			}
		}
	}

	private static ReduxGameDirectoryInstallService CreateInstaller(MainWindowViewModel viewModel,
		ReduxNativePluginDestination destination = ReduxNativePluginDestination.GameBin)
	{
		var executable = Environment.ExpandEnvironmentVariables(viewModel.Settings.GameExecutablePath);
		if (!File.Exists(executable))
			throw new FileNotFoundException("Configure a valid Baldur's Gate 3 executable before managing game-directory mods.", executable);
		var versionInfo = FileVersionInfo.GetVersionInfo(executable);
		var version = new Version(versionInfo.FileMajorPart, versionInfo.FileMinorPart,
			versionInfo.FileBuildPart, versionInfo.FilePrivatePart);
		return new ReduxGameDirectoryInstallService(
			Path.GetDirectoryName(Path.GetFullPath(executable))!,
			DivinityApp.GetAppDirectory("Data", "GameDirectoryInstalls"),
			version, destination);
	}

	private static ReduxGameDirectoryInstallService CreateInstallerForArchive(MainWindowViewModel viewModel,
		ReduxGameDirectoryModDefinition definition, bool preferYanml = false)
	{
		var gameInstaller = CreateInstaller(viewModel);
		if (definition.Kind != ReduxGameDirectoryModKind.NativePlugin) return gameInstaller;
		var configured = ReduxAlternativeNativeLoader.HasYanmlConfiguration(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		if (preferYanml && !configured)
			throw new InvalidOperationException("Configure YANML and start it once before installing native plugins to its Plugins folder.");
		if (configured && (preferYanml || !gameInstaller.DetectLoader().IsPresent))
			return CreateInstaller(viewModel, ReduxNativePluginDestination.YanmlPlugins);
		return gameInstaller;
	}

	private void RefreshList()
	{
		UpdateAlternativeLoaderNotice();
		_viewModel.RefreshScriptExtenderMissingStatus();
		try
		{
			var entries = _installer.GetInstalledMods().Select(entry => CreateListItem(entry, false)).ToList();
			if (ReduxAlternativeNativeLoader.HasYanmlConfiguration(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
				|| ReduxGameDirectoryInstallService.HasYanmlOwnershipRecord(_installer.GameBin,
					DivinityApp.GetAppDirectory("Data", "GameDirectoryInstalls")))
			{
				try
				{
					_yanmlInstaller ??= CreateInstaller(_viewModel, ReduxNativePluginDestination.YanmlPlugins);
					entries.AddRange(_yanmlInstaller.GetInstalledMods().Select(entry => CreateListItem(entry, true)));
				}
				catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
				{
					InstallYanmlButton.IsEnabled = false;
					NativeWarningText.Text = $"Redux cannot safely manage this YANML Plugins folder: {ex.Message}";
				}
			}
			var scriptExtender = entries.FirstOrDefault(item => item.Entry.NexusModId == ScriptExtenderNexusModId);
			UpdateScriptExtenderAction(scriptExtender,
				entries.Any(item => item.Status == ReduxGameDirectoryModStatus.RecoveryRequired));
			InstalledList.ItemsSource = entries;
			InstalledList.Visibility = entries.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
			EmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
		{
			InstalledList.ItemsSource = null;
			InstalledList.Visibility = Visibility.Collapsed;
			EmptyText.Visibility = Visibility.Visible;
			EmptyText.Text = $"Redux could not safely read game-directory install state.\n{ex.Message}";
		}
	}

	private ReduxGameDirectoryModListItem CreateListItem(ReduxGameDirectoryModEntry entry, bool isYanml)
	{
		var definition = ReduxGameDirectoryModCatalog.Find(entry.NexusModId);
		var metadata = ResolveSourceDetails(entry.NexusModId);
		var sourceUrl = !String.IsNullOrWhiteSpace(entry.SourceUrl) ? entry.SourceUrl : definition?.SourceUrl ?? String.Empty;
		var summary = !String.IsNullOrWhiteSpace(metadata?.Summary) ? metadata.Summary.Trim()
			: definition?.Requirements ?? (entry.Status == ReduxGameDirectoryModStatus.RecoveryRequired
				? "Redux found an interrupted game-directory operation that needs attention."
				: "Native files detected in the game directory.");
        summary = (string)new DivinityModManager.Converters.NexusDescriptionToPlainTextConverter()
            .Convert(summary, typeof(string), "", System.Globalization.CultureInfo.CurrentCulture);
        summary = String.Join(" ", summary.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
		var kind = definition?.Kind switch
		{
			ReduxGameDirectoryModKind.NativeLoader => "Native loader",
			ReduxGameDirectoryModKind.NativePlugin => "Native plugin",
			ReduxGameDirectoryModKind.ScriptExtender => "Script Extender",
			_ => "Game-directory files"
		};
		var creator = !String.IsNullOrWhiteSpace(metadata?.Author) ? metadata.Author : metadata?.UploadedBy;
		var displayVersion = !String.IsNullOrWhiteSpace(entry.DetectedVersion)
			? entry.DetectedVersion : metadata?.Version;
		var details = String.Join(" · ", new[]
		{
			isYanml ? "YANML Plugins" : "BG3 game folder",
			kind,
			String.IsNullOrWhiteSpace(sourceUrl) ? null : "Nexus Mods",
			String.IsNullOrWhiteSpace(creator) ? null : $"by {creator}",
			String.IsNullOrWhiteSpace(displayVersion) ? null : $"v{displayVersion}",
			metadata?.UpdatedAt is DateTime updated ? $"Updated {updated:g}" : null
		}.Where(value => !String.IsNullOrWhiteSpace(value)));
        var files = entry.Files.Count == 0 ? "No installed files recorded"
            : "Files: " + String.Join(", ", entry.Files.Select(path => Path.GetFileName(path)));

		return new ReduxGameDirectoryModListItem(entry, files, sourceUrl, summary, details,
			metadata?.PreviewImageUrl ?? String.Empty)
		{
			IsYanml = isYanml,
			Name = !String.IsNullOrWhiteSpace(metadata?.Name) ? metadata.Name.Trim() : entry.Name
		};
	}

	private NexusModsModData ResolveSourceDetails(long nexusModId)
	{
		if (nexusModId < DivinityApp.NEXUSMODS_MOD_ID_START) return null;
		if (_sourceDetails.TryGetValue(nexusModId, out var loaded)) return loaded;
		return _viewModel.UpdateHandler.Nexus.CacheData.Mods.Values
			.Where(metadata => metadata?.ModId == nexusModId)
			.OrderByDescending(metadata => metadata.IsUpdated)
			.FirstOrDefault();
	}

	private async Task LoadSourceDetailsAsync()
	{
		if (!_viewModel.Modules.SourceIntegrationsEnabled || !_viewModel.UpdateHandler.Nexus.IsEnabled
			|| !NexusModsDataLoader.CanFetchData) return;
		var projectIds = (_installer.GetInstalledMods().Select(entry => entry.NexusModId)
			.Concat(_yanmlInstaller?.GetInstalledMods().Select(entry => entry.NexusModId) ?? []))
			.Where(id => id >= DivinityApp.NEXUSMODS_MOD_ID_START && ResolveSourceDetails(id) == null)
			.Distinct().ToArray();
		if (projectIds.Length == 0) return;

		var probes = projectIds.Select(id =>
		{
			var mod = new DivinityModData { UUID = $"redux-game-directory-{id}" };
			mod.NexusModsData.SetModVersion(id);
			return mod;
		}).ToArray();
		var result = await NexusModsDataLoader.LoadAllModsDataAsync(probes, _sourceDetailsCancellation.Token);
		if (_sourceDetailsCancellation.IsCancellationRequested || !IsLoaded) return;
		foreach (var mod in result.UpdatedMods.Where(mod => mod?.NexusModsData?.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START))
			_sourceDetails[mod.NexusModsData.ModId] = mod.NexusModsData;
		if (_sourceDetails.Count > 0) RefreshList();
	}

	private async void InstallButton_Click(object sender, RoutedEventArgs e) => await PickAndInstallAsync(false);

	private async void InstallYanmlButton_Click(object sender, RoutedEventArgs e) => await PickAndInstallAsync(true);

	private async Task PickAndInstallAsync(bool preferYanml)
	{
		var dialog = new OpenFileDialog
		{
			Title = "Choose a Reviewed Game-Directory Mod Archive",
			Filter = "Supported archives (*.zip;*.7z;*.7zip;*.rar)|*.zip;*.7z;*.7zip;*.rar|All files (*.*)|*.*",
			Multiselect = false,
			CheckFileExists = true
		};
		if (dialog.ShowDialog(this) != true) return;
		await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
		if (await ReviewAndInstallAsync(this, _viewModel, dialog.FileName, preferYanml)) RefreshList();
	}

	public static async Task InstallReviewedArchiveWithoutReviewAsync(
		MainWindowViewModel viewModel,
		string archivePath,
		NexusModManagerLink nexusSource = null)
	{
		var inspection = await Task.Run(() => ReduxGameDirectoryInstallService.TryInspectKnownArchive(archivePath))
			?? throw new InvalidDataException("This archive no longer matches a reviewed game-directory package.");
		var installer = CreateInstallerForArchive(viewModel, inspection.Definition);
		await using var transaction = await installer.StageAsync(inspection.Definition.NexusModId, archivePath);
		await transaction.CommitAsync();
		if (inspection.PackageEntries.Count > 0
			&& !await viewModel.ImportModsWithoutReviewAsync([archivePath], false, nexusSource))
			throw new InvalidDataException("The game-directory files installed, but the companion PAK could not be installed.");
	}

	public static async Task<ReduxNativeLoaderStatus> PreflightReviewedArchiveWithoutReviewAsync(
		MainWindowViewModel viewModel,
		long nexusModId,
		string archivePath)
	{
		var definition = ReduxGameDirectoryModCatalog.Find(nexusModId)
			?? throw new InvalidDataException("This native project is not in Redux's reviewed catalog.");
		var installer = CreateInstallerForArchive(viewModel, definition);
		await installer.InspectArchiveAsync(nexusModId, archivePath);
		var loaderStatus = installer.DetectLoader();
		if (loaderStatus.IsAlternativeLoader && !loaderStatus.IsPresent)
			throw new InvalidOperationException(loaderStatus.Description);
		return loaderStatus;
	}

	private async void ScriptExtenderButton_Click(object sender, RoutedEventArgs e)
	{
		if (String.IsNullOrWhiteSpace(_viewModel.PathwayData.ScriptExtenderLatestReleaseUrl))
		{
			ProcessHelper.TryOpenUrl(DivinityApp.EXTENDER_LATEST_URL);
			return;
		}

		string archivePath = null;
		ScriptExtenderButton.IsEnabled = false;
		try
		{
			archivePath = await _viewModel.DownloadScriptExtenderArchiveAsync();
			if (!String.IsNullOrWhiteSpace(archivePath)
				&& await ReviewAndInstallAsync(this, _viewModel, archivePath))
			{
				RefreshList();
			}
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
		{
			ReduxMessageBox.Show(this,
				$"Redux could not download Script Extender.\n\n{ex.Message}",
				"Script Extender Download Stopped", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
		finally
		{
			if (!String.IsNullOrWhiteSpace(archivePath) && File.Exists(archivePath))
			{
				try { File.Delete(archivePath); }
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					DivinityApp.Log($"Could not remove Script Extender intake file: {ex.Message}");
				}
			}
			RefreshList();
		}
	}

	private void UpdateScriptExtenderAction(ReduxGameDirectoryModListItem scriptExtender, bool recoveryRequired)
	{
		var hasRelease = !String.IsNullOrWhiteSpace(_viewModel.PathwayData.ScriptExtenderLatestReleaseUrl);
		var latestVersion = ParseLeadingVersion(_viewModel.PathwayData.ScriptExtenderLatestReleaseVersion);
		var installedVersion = ParseLeadingVersion(scriptExtender?.Entry.DetectedVersion);
		if (installedVersion < 0 && _viewModel.Settings.ExtenderUpdaterSettings.UpdaterVersion > 0)
			installedVersion = _viewModel.Settings.ExtenderUpdaterSettings.UpdaterVersion;

		if (recoveryRequired)
		{
			SetScriptExtenderAction("Resolve recovery first", false,
				"Finish the pending game-directory recovery before changing Script Extender.");
			return;
		}
		if (scriptExtender == null)
		{
			SetScriptExtenderAction("Install Script Extender", true,
				hasRelease ? "Download, review, and install the latest Script Extender release."
					: "Open the Script Extender releases page because Redux could not resolve the latest archive.");
			return;
		}
		if (scriptExtender.Status is ReduxGameDirectoryModStatus.Changed or ReduxGameDirectoryModStatus.Missing)
		{
			SetScriptExtenderAction("Reinstall Script Extender", hasRelease,
				hasRelease ? "Repair the changed or missing Redux-managed DLL with the latest reviewed release."
					: "Redux could not resolve the latest reviewed release. Try refreshing release information first.");
			return;
		}
		if (scriptExtender.Status == ReduxGameDirectoryModStatus.External)
		{
			if (!scriptExtender.CanAdopt)
			{
				SetScriptExtenderAction("Unverified Script Extender", false, scriptExtender.ManagementNote);
				return;
			}
			if (latestVersion < 0)
			{
				SetScriptExtenderAction("Manage Script Extender first", false,
					"Redux recognizes this installed version. Choose Manage while release information refreshes.");
				return;
			}
			if (latestVersion > installedVersion && installedVersion >= 0)
			{
				SetScriptExtenderAction("Manage before updating", false,
					"Choose Manage first. Redux can then update this older reviewed installation safely.");
				return;
			}
			SetScriptExtenderAction("Script Extender is up to date", false,
				$"Not managed by this Redux installation{FormatVersion(scriptExtender.Entry.DetectedVersion)}. Choose Manage if you want Redux to own its removal and future updates.");
			return;
		}
		if (latestVersion >= 0 && installedVersion >= latestVersion)
		{
			SetScriptExtenderAction("Script Extender is up to date", false,
				$"Redux manages the current release{FormatVersion(scriptExtender.Entry.DetectedVersion)}.");
			return;
		}
		if (latestVersion > installedVersion && installedVersion >= 0)
		{
			SetScriptExtenderAction("Update Script Extender", hasRelease,
				$"Update the Redux-managed installation from v{installedVersion} to v{latestVersion}.");
			return;
		}

		SetScriptExtenderAction("Reinstall Script Extender", hasRelease,
			hasRelease ? "Redux could not confirm the installed version. Reinstall the latest reviewed release."
				: "Redux could not confirm the installed version or resolve the latest reviewed release.");
	}

	private void SetScriptExtenderAction(string text, bool enabled, string toolTip)
	{
		ScriptExtenderButtonText.Text = text;
		ScriptExtenderButton.IsEnabled = enabled;
		ScriptExtenderButton.ToolTip = toolTip;
	}

	private static int ParseLeadingVersion(string value)
	{
		if (String.IsNullOrWhiteSpace(value)) return -1;
		var digits = new string(value.TrimStart().TrimStart('v', 'V').TakeWhile(Char.IsDigit).ToArray());
		return Int32.TryParse(digits, out var version) ? version : -1;
	}

	private static string FormatVersion(string version) => String.IsNullOrWhiteSpace(version)
		? String.Empty : $" · v{version}";

	private async void Window_Drop(object sender, DragEventArgs e)
	{
		if (ReduxWindowBehavior.HasActiveChild(this)) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
		if (!e.Data.GetDataPresent(DataFormats.FileDrop)
			|| e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } paths) return;
		e.Handled = true;
		await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
		if (await ReviewAndInstallAsync(this, _viewModel, paths[0])) RefreshList();
	}

	private async void DeleteButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement { DataContext: ReduxGameDirectoryModListItem item } || !item.CanRestore) return;
		var detail = item.IsAdopted
			? "Redux will remove only the adopted DLL files, and only if each still matches the version you adopted. Settings files and companion content remain untouched."
			: "Files Redux added will be removed, and any files it replaced will be restored. Redux will only continue if every managed file still matches its installation record.";
		var answer = ReduxMessageBox.Show(this,
			$"Delete Redux's managed installation of {item.Name}? {detail}",
			"Delete Game-Directory Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
		if (answer != MessageBoxResult.Yes) return;
		try
		{
			await (item.IsYanml ? _yanmlInstaller : _installer).RestoreAsync(item.Entry.NexusModId);
			_viewModel.ShowAlert($"Removed {item.Name} and restored any files it replaced.", AlertType.Success, 20);
			RefreshList();
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
		{
			ReduxMessageBox.Show(this, ex.Message, "Delete Stopped",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
	}

	private async void AdoptButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement { DataContext: ReduxGameDirectoryModListItem item } || !item.CanAdopt) return;
		try
		{
			await (item.IsYanml ? _yanmlInstaller : _installer).AdoptExternalAsync(item.Entry.NexusModId);
			_viewModel.ShowAlert($"Redux now manages {item.Name}.", AlertType.Success, 20);
			RefreshList();
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
		{
			ReduxMessageBox.Show(this, ex.Message, "Could Not Manage Installation",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
	}

	private void SourceButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: ReduxGameDirectoryModListItem item } && item.HasSource)
			ProcessHelper.TryOpenUrl(item.SourceUrl);
	}

	private void OpenGameFolderButton_Click(object sender, RoutedEventArgs e) =>
		ProcessHelper.TryOpenPath(_installer.GameBin, Directory.Exists);

	private void OpenYanmlPluginsButton_Click(object sender, RoutedEventArgs e) =>
		ProcessHelper.TryOpenPath(ReduxAlternativeNativeLoader.CurrentPluginsDirectory, Directory.Exists);

	private void UpdateAlternativeLoaderNotice()
	{
		var configured = ReduxAlternativeNativeLoader.HasYanmlConfiguration(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		var ownsYanmlPlugins = ReduxGameDirectoryInstallService.HasYanmlOwnershipRecord(_installer.GameBin,
			DivinityApp.GetAppDirectory("Data", "GameDirectoryInstalls"));
		OpenYanmlPluginsButton.Visibility = configured || ownsYanmlPlugins ? Visibility.Visible : Visibility.Collapsed;
		InstallYanmlButton.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
		InstallYanmlButton.IsEnabled = configured;
		NativeWarningText.Text = configured
			? "Native DLLs run code inside the game. YANML uses the local Plugins folder; Redux can now install reviewed plugins there. YANML itself must be started or installed separately. Some plugins still read settings from BG3\\bin\\NativeMods."
			: ownsYanmlPlugins
				? "YANML's configuration is missing. Redux can still show its previous plugin ownership, but new installs are blocked until YANML is configured again."
				: "Native DLLs run code inside the game. Install only from sources you trust.";
	}

	private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshList();

	private static string FormatBytes(long bytes) => bytes >= 1024 * 1024
		? $"{bytes / (1024d * 1024d):0.#} MB"
		: $"{Math.Max(1, bytes / 1024d):0.#} KB";
}
