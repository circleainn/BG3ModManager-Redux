using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Windows;

namespace DivinityModManager.Views;

public sealed class ReduxExportReviewData
{
	public string DestinationSummary { get; }
	public string BaselineSummary { get; }
	public string NoChangesSummary { get; }
	public string ChangeListTitle { get; }
	public IReadOnlyList<ReduxExportReviewChangeItem> Changes { get; }
	public bool HasChanges => Changes.Count > 0;
	public int ActivatedCount { get; }
	public int DeactivatedCount { get; }
	public int RepositionedCount { get; }
	public int AutomaticallyAddedCount { get; }
	public bool HasIncludedDependencies => AutomaticallyAddedCount > 0;
	public string IncludedDependencySummary { get; }
	public bool HasDiagnosticErrors { get; }
	public bool HasDiagnosticWarnings { get; }
	public string DiagnosticTitle { get; }
	public string DiagnosticSummary { get; }
	public string DependencySummary { get; }

	public ReduxExportReviewData(
		string orderName,
		string profileName,
		LoadOrderComparison comparison,
		int healthErrorCount,
		int healthWarningCount,
		int guidanceCount,
		int missingDependencyCount)
	{
		comparison ??= new LoadOrderComparison(false, [], 0);
		var safeOrderName = String.IsNullOrWhiteSpace(orderName) ? "Current" : orderName;
		var safeProfileName = String.IsNullOrWhiteSpace(profileName) ? "selected profile" : profileName;
		DestinationSummary =
			$"Exporting “{safeOrderName}” to “{safeProfileName}” with {FormatCount(comparison.ProposedModCount, "active mod")}.";
		ActivatedCount = comparison.Activated.Count;
		DeactivatedCount = comparison.Deactivated.Count;
		RepositionedCount = comparison.Repositioned.Count;
		AutomaticallyAddedCount = comparison.AutomaticallyAdded.Count;
		IncludedDependencySummary = AutomaticallyAddedCount == 1
			? "1 installed dependency will be included with this order."
			: $"{AutomaticallyAddedCount} installed dependencies will be included with this order.";
		Changes = comparison.Changes.Select(change => new ReduxExportReviewChangeItem(change)).ToArray();
		ChangeListTitle = comparison.HasChanges
			? $"Changes ({comparison.Changes.Count})"
			: "Changes";
		NoChangesSummary = comparison.HasPreviousOrder
			? "This order matches the load order currently exported to the selected profile."
			: "No earlier exported load order is available for comparison.";
		BaselineSummary = comparison.HasPreviousOrder
			? "Compared with the load order currently exported to this profile."
			: "No previous export is available for this profile.";

		HasDiagnosticErrors = healthErrorCount > 0;
		HasDiagnosticWarnings = healthWarningCount > 0 || missingDependencyCount > 0;
		if (HasDiagnosticErrors)
		{
			DiagnosticTitle = "Fix errors before exporting";
		}
		else if (HasDiagnosticWarnings)
		{
			DiagnosticTitle = "Review warnings before exporting";
		}
		else
		{
			DiagnosticTitle = "No issues found";
		}

		var diagnosticParts = new List<string>();
		if (healthErrorCount > 0) diagnosticParts.Add(FormatCount(healthErrorCount, "error"));
		if (healthWarningCount > 0) diagnosticParts.Add(FormatCount(healthWarningCount, "warning"));
		if (guidanceCount > 0) diagnosticParts.Add(FormatCount(guidanceCount, "Load Order Advisor note"));
		DiagnosticSummary = diagnosticParts.Count == 0
			? "No errors or warnings were found in this order."
			: String.Join(" · ", diagnosticParts) + ".";
		DependencySummary = missingDependencyCount == 0
			? "No missing dependencies detected."
			: FormatCount(missingDependencyCount, "missing dependency", "missing dependencies") + " detected.";
	}

	private static string FormatCount(int count, string singular, string plural = null) =>
		$"{Math.Max(0, count)} {(count == 1 ? singular : plural ?? $"{singular}s")}";
}

public sealed class ReduxExportReviewChangeItem
{
	public LoadOrderChangeKind Kind { get; }
	public string Name { get; }
	public string PositionSummary { get; }

	public ReduxExportReviewChangeItem(LoadOrderChange change)
	{
		Kind = change.Kind;
		Name = change.Name;
		PositionSummary = change.Kind switch
		{
			LoadOrderChangeKind.Activated or LoadOrderChangeKind.AutomaticallyAdded =>
				$"Position {change.NextPosition}",
			LoadOrderChangeKind.Deactivated =>
				$"Was {change.PreviousPosition}",
			LoadOrderChangeKind.Repositioned =>
				$"{change.PreviousPosition} → {change.NextPosition}",
			_ => String.Empty
		};
	}
}

public partial class ReduxExportReviewWindow : AdonisUI.Controls.AdonisWindow
{
	public bool Accepted { get; private set; }

	public ReduxExportReviewWindow(Window owner, ReduxExportReviewData review)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true)
		{
			Owner = owner;
		}

		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
		{
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings));
		}
		DataContext = review ?? throw new ArgumentNullException(nameof(review));
	}

	private void ExportButton_Click(object sender, RoutedEventArgs e)
	{
		Accepted = true;
		Close();
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
