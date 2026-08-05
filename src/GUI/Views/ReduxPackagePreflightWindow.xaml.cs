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
				ReduxThemeService.GetActiveTheme(settings));
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
			StatusTitleText.Text = "Inspection cancelled";
			StatusDescriptionText.Text = "The package was not changed.";
			FindingSummaryText.Text = String.Empty;
			SetStatus("ReduxTextMutedBrush", "Redux.Icon.CloseCircle");
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Package preflight failed:\n{ex}");
			StatusTitleText.Text = "Package could not be inspected";
			StatusDescriptionText.Text = "See the Redux log for details.";
			FindingSummaryText.Text = "Inspection failed";
			SetStatus("ReduxErrorBrush", "Redux.Icon.CloseCircle");
		}
		finally
		{
			_active = false;
			ScanActionButton.Content = "Close";
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

			return new PreflightPresentation
			{
				IdentityHeader = "ARCHIVE",
				AuthorLabel = "Packages",
				VersionLabel = "Readable modules",
				UuidLabel = "Contents",
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
			StatusDescriptionText.Text = "Cancelling inspection...";
			_cancellation.Cancel();
			return;
		}

		Close();
	}
}
