using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Windows;

namespace DivinityModManager.Views;

public partial class ReduxLoadOrderImportWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly int _missingModCount;
	private readonly IReadOnlyList<string> _missingModNames;
	private readonly IReadOnlyList<string> _categoryConflictNames;
	private readonly bool _createdByNewerRedux;
	private readonly string _creatorVersion;

	public bool ImportLoadOrder => ImportLoadOrderCheckBox.IsChecked == true;
	public bool ImportPresentation => ImportPresentationCheckBox.IsChecked == true;
	public bool Accepted { get; private set; }

	public ReduxLoadOrderImportWindow(
		Window owner,
		ReduxLoadOrderBundleContents contents,
		IReadOnlyList<string> missingModNames,
		IReadOnlyList<string> categoryConflictNames)
	{
		InitializeComponent();
		_missingModCount = missingModNames?.Count ?? 0;
		_missingModNames = NormalizeNames(missingModNames);
		_categoryConflictNames = NormalizeNames(categoryConflictNames);
		_creatorVersion = !String.IsNullOrWhiteSpace(contents?.Presentation?.CreatorVersion)
			? contents.Presentation.CreatorVersion.Trim()
			: contents?.Presentation?.CreatorInternalVersion?.Trim() ?? String.Empty;
		_createdByNewerRedux = IsCreatedByNewerRedux(contents?.Presentation?.CreatorInternalVersion);
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		if (owner?.IsLoaded == true) Owner = owner;

		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings));

		var orderCount = contents?.LoadOrder?.Order?.Count ?? 0;
		var categoryCount = contents?.Presentation?.CustomCategories?.Count ?? 0;
		var dividerCount = contents?.Presentation?.Dividers?.Count ?? 0;
		var iconCount = contents?.Presentation?.CustomIconAssets?.Count ?? 0;
		var installedCount = Math.Max(0, orderCount - _missingModCount);
		BundleSummaryText.Text = contents?.LoadOrder?.Name ?? "Redux order";
		var exportedAt = contents?.Presentation?.ExportedAtUtc ?? default;
		var exportedLabel = exportedAt == default
			? String.Empty
			: exportedAt.ToLocalTime().ToString("g");
		BundleMetadataText.Text = BuildMetadataText(_creatorVersion, exportedLabel);
		LoadOrderAvailabilityText.Text =
			$"{FormatCount(installedCount, "mod")} available locally • " +
			$"{FormatCount(_missingModCount, "missing", "missing")}";
		PresentationContentsText.Text =
			$"{FormatCount(categoryCount, "custom category", "custom categories")} • " +
			$"{FormatCount(dividerCount, "separator")} • {FormatCount(iconCount, "custom icon")}";
		UpdateImportButton();
	}

	private static IReadOnlyList<string> NormalizeNames(IEnumerable<string> names) =>
		(names ?? Enumerable.Empty<string>())
			.Where(name => !String.IsNullOrWhiteSpace(name))
			.Select(name => name.Trim())
			.Select(name => name.Length > 64 ? $"{name[..61]}…" : name)
			.Distinct(StringComparer.CurrentCultureIgnoreCase)
			.ToList();

	private static string FormatCount(int count, string singular, string plural = null) =>
		$"{Math.Max(0, count)} {(count == 1 ? singular : plural ?? $"{singular}s")}";

	private static string BuildMetadataText(string creatorVersion, string exportedLabel)
	{
		var parts = new List<string>();
		if (!String.IsNullOrWhiteSpace(creatorVersion))
			parts.Add($"Created with Redux {creatorVersion}");
		if (!String.IsNullOrWhiteSpace(exportedLabel))
			parts.Add($"exported {exportedLabel}");
		return parts.Count > 0 ? String.Join(" • ", parts) : "Creator version unavailable";
	}

	private static bool IsCreatedByNewerRedux(string creatorInternalVersion)
	{
		if (!Version.TryParse(creatorInternalVersion, out var creator) ||
			!Version.TryParse(DivinityApp.REDUX_INTERNAL_VERSION, out var current))
			return false;
		return creator > current;
	}

	private static string FormatNamePreview(IReadOnlyList<string> names, int visibleCount = 3)
	{
		if (names == null || names.Count == 0) return String.Empty;
		var visible = names.Take(Math.Max(1, visibleCount)).ToList();
		var preview = String.Join(", ", visible);
		var remaining = names.Count - visible.Count;
		return remaining > 0 ? $"{preview} (+{remaining} more)" : preview;
	}

	private void ImportOption_Changed(object sender, RoutedEventArgs e) => UpdateImportButton();

	private void UpdateImportButton()
	{
		if (ImportButton != null)
			ImportButton.IsEnabled = ImportLoadOrder || ImportPresentation;

		if (ImportImpactBorder == null || ImportImpactText == null) return;
		var notices = new List<string>();
		if (_createdByNewerRedux)
		{
			notices.Add(
				$"This bundle was created with newer Redux {_creatorVersion}. " +
				"Review its contents before importing; unsupported bundle schemas remain blocked.");
		}
		if (ImportLoadOrder && _missingModNames.Count > 0)
		{
			notices.Add(
				$"Missing locally: {FormatNamePreview(_missingModNames)}. " +
				"The saved order will preserve the missing entries so Redux can report them normally.");
		}
		if (ImportPresentation && _categoryConflictNames.Count > 0)
		{
			notices.Add(
				$"Renamed on import: {FormatNamePreview(_categoryConflictNames)}. " +
				"Redux will create copies instead of overwriting the existing categories.");
		}

		ImportImpactText.Text = String.Join(Environment.NewLine, notices);
		ImportImpactBorder.Visibility = notices.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
	}

	private void ImportButton_Click(object sender, RoutedEventArgs e)
	{
		Accepted = true;
		Close();
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
