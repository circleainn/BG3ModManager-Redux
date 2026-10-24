using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;
using DivinityModManager.Util;

using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DivinityModManager.Views;

public partial class ReduxPackagePreflightWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly string _sourcePath;
	private readonly IReadOnlyList<DivinityModData> _installedMods;
	private readonly CancellationTokenSource _cancellation = new();
	private bool _started;
	private bool _active;

	public ReduxPackagePreflightWindow(
		Window owner,
		string packagePath,
		IReadOnlyList<DivinityModData> installedMods)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true) Owner = owner;

		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
		{
			ReduxThemeService.Apply(
				Resources,
				settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(settings),
				settings.UsesGeneratedGradients);
		}

		_sourcePath = packagePath ?? String.Empty;
		_installedMods = installedMods ?? [];
		PackagePathText.Text = Path.GetFileName(_sourcePath);
		PackagePathText.ToolTip = _sourcePath;
		Loaded += ReduxPackagePreflightWindow_Loaded;
		Closing += ReduxPackagePreflightWindow_Closing;
		PreviewKeyDown += ReduxPackagePreflightWindow_PreviewKeyDown;
	}

	private void ReduxPackagePreflightWindow_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Escape) return;
		e.Handled = true;
		CloseButton_Click(ScanActionButton, new RoutedEventArgs());
	}

	private async void ReduxPackagePreflightWindow_Loaded(object sender, RoutedEventArgs e)
	{
		if (_started) return;
		_started = true;
		_active = true;

		try
		{
			if (String.Equals(Path.GetExtension(_sourcePath), ".pak", StringComparison.OrdinalIgnoreCase))
			{
				var report = await Task.Run(
					() => PackagePreflightService.AnalyzeAsync(
						_sourcePath,
						_installedMods,
						_cancellation.Token),
					_cancellation.Token);
				ApplyReport(PreflightPresentation.FromPackage(report));
			}
			else
			{
				var report = await ArchivePackagePreflightService.AnalyzeAsync(
					_sourcePath,
					_installedMods,
					_cancellation.Token);
				ApplyReport(PreflightPresentation.FromArchive(report));
			}
		}
		catch (OperationCanceledException)
		{
			StatusTitleText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Inspection cancelled");
			StatusDescriptionText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("The package was not changed.");
			FindingSummaryText.Text = String.Empty;
			SetStatus("ReduxTextMutedBrush", "Redux.Icon.CloseCircle");
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Package preflight failed:\n{ex}");
			StatusTitleText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Package could not be inspected");
			StatusDescriptionText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("See the Redux log for details.");
			FindingSummaryText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Inspection failed");
			SetStatus("ReduxErrorBrush", "Redux.Icon.CloseCircle");
		}
		finally
		{
			_active = false;
			ScanActionButtonText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Close");
			ScanActionButton.IsEnabled = true;
		}
	}

	private void ApplyReport(PreflightPresentation report)
	{
		DataContext = report;
		StatusTitleText.Text = report.StatusTitle;
		StatusDescriptionText.Text = report.StatusDescription;
		FindingSummaryText.Text = report.FindingSummary;
		DetectedFeaturesText.Text = report.DetectedFeatures;
		FindingsList.ItemsSource = report.Findings;
		ClearState.Visibility = report.Findings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

		if (report.HasErrors)
			SetStatus("ReduxErrorBrush", "Redux.Icon.CloseCircle");
		else if (report.HasWarnings)
			SetStatus("ReduxWarningBrush", "Redux.Icon.Warning");
		else
			SetStatus("ReduxSuccessBrush", "Redux.Icon.CircleCheck");
	}

	private sealed class PreflightPresentation
	{
		public string IdentityHeader { get; init; } = "MODULE";
		public string AuthorLabel { get; init; } = "Author";
		public string VersionLabel { get; init; } = "Version";
		public string UuidLabel { get; init; } = "UUID";
		public string PrimaryMetricLabel { get; init; } = "Files";
		public string SecondaryMetricLabel { get; init; } = "Dependencies";
		public string SizeMetricLabel { get; init; } = "Package size";
		public string DisplayName { get; init; } = String.Empty;
		public string Author { get; init; } = String.Empty;
		public string Version { get; init; } = String.Empty;
		public string Uuid { get; init; } = String.Empty;
		public string InternalFileCountText { get; init; } = "0";
		public string DeclaredDependencyCountText { get; init; } = "0";
		public string PackageSizeText { get; init; } = "Unavailable";
		public string StatusTitle { get; init; } = String.Empty;
		public string StatusDescription { get; init; } = String.Empty;
		public string FindingSummary { get; init; } = String.Empty;
		public string DetectedFeatures { get; init; } = String.Empty;
		public IReadOnlyList<PackagePreflightFinding> Findings { get; init; } = [];
		public bool HasErrors => Findings.Any(finding => finding.Severity == ModHealthSeverity.Error);
		public bool HasWarnings => Findings.Any(finding => finding.Severity == ModHealthSeverity.Warning);

		public static PreflightPresentation FromPackage(PackagePreflightReport report) => new()
		{
			DisplayName = report.DisplayName,
			Author = report.Author,
			Version = report.Version,
			Uuid = report.Uuid,
			InternalFileCountText = report.InternalFileCountText,
			DeclaredDependencyCountText = report.DeclaredDependencyCountText,
			PackageSizeText = report.PackageSizeText,
			StatusTitle = report.StatusTitle,
			StatusDescription = report.StatusDescription,
			FindingSummary = report.FindingSummary,
			DetectedFeatures = report.DetectedFeatures,
			Findings = report.Findings
		};

		public static PreflightPresentation FromArchive(ArchivePackagePreflightResult report)
		{
			var findings = report.Findings
				.Concat(report.Packages.SelectMany(package => package.Findings.Select(finding =>
					new PackagePreflightFinding(
						finding.Severity,
						$"{package.PackageFileName}: {finding.Title}",
						finding.Message))))
				.OrderByDescending(finding => finding.Severity)
				.ThenBy(finding => finding.Title, StringComparer.OrdinalIgnoreCase)
				.ToArray();
			var errorCount = findings.Count(finding => finding.Severity == ModHealthSeverity.Error);
			var warningCount = findings.Count(finding => finding.Severity == ModHealthSeverity.Warning);
			var infoCount = findings.Count(finding => finding.Severity == ModHealthSeverity.Info);
			var packageNames = report.Packages.Count == 0
				? "No PAK files found"
				: String.Join(", ", report.Packages.Select(package => package.DisplayName));
			var summaryParts = new List<string>();
			if (errorCount > 0) summaryParts.Add($"{errorCount} error{(errorCount == 1 ? String.Empty : "s")}");
			if (warningCount > 0) summaryParts.Add($"{warningCount} warning{(warningCount == 1 ? String.Empty : "s")}");
			if (infoCount > 0) summaryParts.Add($"{infoCount} note{(infoCount == 1 ? String.Empty : "s")}");

			if (report.Kind == ArchivePackagePreflightKind.ReviewedGameDirectory
				|| report.Kind == ArchivePackagePreflightKind.UnreviewedNative
				|| report.Kind == ArchivePackagePreflightKind.Mixed
					&& report.EntryNames.Any(name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
				return FromNativeArchive(report, findings, summaryParts);
			if (report.Kind == ArchivePackagePreflightKind.SaveGame
				|| report.Kind == ArchivePackagePreflightKind.Mixed && report.SaveGames.Count > 0)
				return FromSaveArchive(report, findings, summaryParts);

			return new PreflightPresentation
			{
				IdentityHeader = "ARCHIVE",
				AuthorLabel = "Packages",
				VersionLabel = "Readable modules",
				UuidLabel = "Contents",
				PrimaryMetricLabel = "Entries",
				SecondaryMetricLabel = "PAK packages",
				DisplayName = Path.GetFileName(report.ArchivePath),
				Author = report.Packages.Count.ToString("N0"),
				Version = report.Packages.Count(package => package.IsReadable).ToString("N0"),
				Uuid = packageNames,
				InternalFileCountText = report.Packages.Sum(package => package.InternalFileCount).ToString("N0"),
				DeclaredDependencyCountText = report.Packages.Sum(package => package.DeclaredDependencyCount).ToString("N0"),
				PackageSizeText = FormatFileSize(report.ArchiveSize),
				StatusTitle = errorCount > 0
					? "Archive needs attention"
					: warningCount > 0 ? "Review recommended" : "No blocking issues found",
				StatusDescription = errorCount > 0
					? "Redux found archive or package problems that should be corrected before release."
					: warningCount > 0
						? "The archive is readable, but some release details are worth reviewing."
						: "Redux could read the archive and its contained packages without detecting a blocking issue.",
				FindingSummary = summaryParts.Count == 0 ? "No findings" : String.Join(" · ", summaryParts),
				DetectedFeatures = $"{report.EntryCount:N0} archive entries · {report.Packages.Count:N0} PAK packages",
				Findings = findings
			};
		}

		private static PreflightPresentation FromNativeArchive(
			ArchivePackagePreflightResult report,
			IReadOnlyList<PackagePreflightFinding> findings,
			IReadOnlyList<string> summaryParts)
		{
			var inspection = report.GameDirectoryInspection;
			var definition = inspection?.Definition;
			var dlls = report.EntryNames.Where(name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
				.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			var kind = definition?.Kind switch
			{
				ReduxGameDirectoryModKind.NativeLoader => "Native loader",
				ReduxGameDirectoryModKind.NativePlugin => "Native plugin",
				ReduxGameDirectoryModKind.ScriptExtender => "Script Extender",
				_ => report.Kind == ArchivePackagePreflightKind.Mixed ? "Mixed / hybrid archive" : "Unreviewed native archive"
			};
			var statusTitle = findings.Any(finding => finding.Severity == ModHealthSeverity.Error)
				? "Native package needs attention"
				: findings.Any(finding => finding.Severity == ModHealthSeverity.Warning)
					? "Native package needs review" : "Reviewed native package recognized";
			return new PreflightPresentation
			{
				IdentityHeader = definition == null ? "NATIVE ARCHIVE" : "GAME-DIRECTORY PACKAGE",
				AuthorLabel = "Type",
				VersionLabel = definition == null ? "DLLs" : "Layout",
				UuidLabel = definition == null ? "Destination" : "Source",
				PrimaryMetricLabel = "Entries",
				SecondaryMetricLabel = "Managed files",
				DisplayName = definition?.Name ?? Path.GetFileName(report.ArchivePath),
				Author = kind,
				Version = inspection?.LayoutName ?? (dlls.Length == 0 ? "None detected" : String.Join(", ", dlls)),
				Uuid = definition?.SourceUrl ?? "Not determined — inspection only",
				InternalFileCountText = report.EntryCount.ToString("N0"),
				DeclaredDependencyCountText = (inspection?.ManagedFiles.Count ?? 0).ToString("N0"),
				PackageSizeText = FormatFileSize(report.ArchiveSize),
				StatusTitle = statusTitle,
				StatusDescription = definition != null
					? "Redux matched this archive to the same reviewed layout used by the guarded game-directory installer."
					: "Redux detected native libraries but will not guess their identity or install destination.",
				FindingSummary = summaryParts.Count == 0 ? "No findings" : String.Join(" · ", summaryParts),
				DetectedFeatures = String.Join(" · ", new[]
				{
					$"{dlls.Length:N0} DLL{(dlls.Length == 1 ? String.Empty : "s")}",
					definition?.RequiresLoader == true ? "Requires Native Mod Loader" : null,
					inspection?.PackageEntries.Count > 0 ? $"{inspection.PackageEntries.Count:N0} companion PAK{(inspection.PackageEntries.Count == 1 ? String.Empty : "s")}" : null
				}.Where(value => !String.IsNullOrWhiteSpace(value))),
				Findings = findings
			};
		}

		private static PreflightPresentation FromSaveArchive(
			ArchivePackagePreflightResult report,
			IReadOnlyList<PackagePreflightFinding> findings,
			IReadOnlyList<string> summaryParts)
		{
			var saves = report.SaveGames;
			var first = saves.FirstOrDefault();
			var campaigns = saves.Select(save => save.CampaignName).Where(name => !String.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			var difficulties = saves.Select(save => save.Difficulty).Where(value => value != Bg3SaveDifficulty.Unknown)
				.Distinct().Select(value => value.ToString()).ToArray();
			var totalBytes = saves.Sum(save => save.SizeBytes);
			var hasErrors = findings.Any(finding => finding.Severity == ModHealthSeverity.Error);
			return new PreflightPresentation
			{
				IdentityHeader = "SAVE GAME",
				AuthorLabel = "Campaign",
				VersionLabel = "Difficulty",
				UuidLabel = "Contents",
				PrimaryMetricLabel = "Entries",
				SecondaryMetricLabel = "Save games",
				DisplayName = saves.Count == 1 ? first.DisplayName : Path.GetFileName(report.ArchivePath),
				Author = campaigns.Length == 0 ? "Not available" : String.Join(", ", campaigns),
				Version = difficulties.Length == 0 ? "Not available" : String.Join(", ", difficulties),
				Uuid = saves.Count == 0 ? "No validated saves" : String.Join(", ", saves.Select(save => save.FolderName)),
				InternalFileCountText = report.EntryCount.ToString("N0"),
				DeclaredDependencyCountText = saves.Count.ToString("N0"),
				PackageSizeText = FormatFileSize(report.ArchiveSize > 0 ? report.ArchiveSize : totalBytes),
				StatusTitle = hasErrors ? "Save data needs attention" : "BG3 save recognized",
				StatusDescription = hasErrors
					? "Redux found save data but could not validate it safely."
					: "This belongs in Save Game Manager rather than the normal mod-install workflow.",
				FindingSummary = summaryParts.Count == 0 ? "No findings" : String.Join(" · ", summaryParts),
				DetectedFeatures = saves.Count == 0 ? "No validated save metadata"
					: $"{saves.Count:N0} save{(saves.Count == 1 ? String.Empty : "s")} · newest {saves.Max(save => save.ModifiedUtc).ToLocalTime():g}",
				Findings = findings
			};
		}

		private static string FormatFileSize(long size) => size <= 0
			? "Unavailable"
			: size >= 1024L * 1024L * 1024L
				? $"{size / (1024d * 1024d * 1024d):0.##} GB"
				: size >= 1024L * 1024L
					? $"{size / (1024d * 1024d):0.##} MB"
					: $"{size / 1024d:0.##} KB";
	}

	private void SetStatus(string brushKey, string geometryKey)
	{
		if (TryFindResource(brushKey) is Brush brush)
		{
			StatusRail.Background = brush;
			StatusIcon.Foreground = brush;
		}
		if (TryFindResource(geometryKey) is Geometry geometry)
		{
			StatusIcon.StrokeData = geometry;
		}
	}

	private void ReduxPackagePreflightWindow_Closing(object sender, CancelEventArgs e)
	{
		if (_active) _cancellation.Cancel();
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		if (_active)
		{
			ScanActionButton.IsEnabled = false;
			StatusDescriptionText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Cancelling inspection...");
			_cancellation.Cancel();
			return;
		}

		Close();
	}
}
