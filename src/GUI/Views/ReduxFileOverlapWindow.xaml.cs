using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DivinityModManager.Views;

public partial class ReduxFileOverlapWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly IReadOnlyList<DivinityModData> _candidates;
	private readonly CancellationTokenSource _scanCancellation = new();
	private ModFileOverlapScanResult _result;
	private bool _scanStarted;
	private bool _scanActive;

	public ReduxFileOverlapWindow(
		Window owner,
		IReadOnlyList<DivinityModData> candidates)
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
			ReduxThemeService.Apply(
				Resources,
				settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(settings),
				settings.UsesGeneratedGradients);
		}

		_candidates = candidates ?? [];
		ScanStatusText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Format($"0 of {_candidates.Count} packages scanned");
		Loaded += ReduxFileOverlapWindow_Loaded;
		Closing += ReduxFileOverlapWindow_Closing;
		PreviewKeyDown += ReduxFileOverlapWindow_PreviewKeyDown;
	}

	private void ReduxFileOverlapWindow_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Escape)
		{
			return;
		}

		e.Handled = true;
		CloseButton_Click(ScanActionButton, new RoutedEventArgs());
	}

	private async void ReduxFileOverlapWindow_Loaded(object sender, RoutedEventArgs e)
	{
		if (_scanStarted)
		{
			return;
		}

		_scanStarted = true;
		_scanActive = true;
		var progress = new Progress<ModFileOverlapProgress>(state =>
		{
			var current = Math.Min(
				state.TotalPackageCount,
				state.CompletedPackageCount + 1);
			ScanStatusText.Text = state.CompletedPackageCount >= state.TotalPackageCount
				? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Preparing overlap results...")
				: $"{current} of {state.TotalPackageCount} · {state.CurrentModName}";
		});

		try
		{
			var result = await Task.Run(
				() => ModFileOverlapService.AnalyzePackages(
					_candidates,
					_scanCancellation.Token,
					progress),
				_scanCancellation.Token);
			ApplyResult(result);
		}
		catch (OperationCanceledException)
		{
			ShowCancelledState();
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error inspecting active PAK file overlaps:\n{ex}");
			ShowFailureState(ex.Message);
		}
		finally
		{
			_scanActive = false;
		}
	}

	private void ReduxFileOverlapWindow_Closing(object sender, CancelEventArgs e)
	{
		if (_scanActive)
		{
			_scanCancellation.Cancel();
		}
	}

	private void ApplyResult(ModFileOverlapScanResult result)
	{
		_result = result ?? new ModFileOverlapScanResult(0, 0, 0, [], []);
		ScannedPackageCountText.Text = _result.ScannedPackageCount.ToString();
		OverlapPathCountText.Text = _result.OverlapPathCount.ToString();
		AffectedPackageCountText.Text = _result.AffectedPackageCount.ToString();
		ScanStatusText.Text = BuildStatusText(_result);
		SearchTextBox.IsEnabled = true;
		ScanActionButtonText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Close");
		SetEmptyStateIcon("Redux.Icon.CircleCheck", "ReduxSuccessBrush");
		RefreshFilter();
	}

	private void ShowCancelledState()
	{
		_result = null;
		OverlapList.ItemsSource = null;
		VisibleResultCountText.Text = String.Empty;
		EmptyState.Visibility = Visibility.Visible;
		EmptyStateTitle.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Inspection cancelled");
		EmptyStateDescription.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("No files or load-order data were changed.");
		ScanStatusText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Cancelled");
		ScanActionButtonText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Close");
		ScanActionButton.IsEnabled = true;
		SetEmptyStateIcon("Redux.Icon.CloseCircle", "ReduxTextMutedBrush");
	}

	private void ShowFailureState(string message)
	{
		_result = null;
		OverlapList.ItemsSource = null;
		VisibleResultCountText.Text = String.Empty;
		EmptyState.Visibility = Visibility.Visible;
		EmptyStateTitle.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Packages could not be inspected");
		EmptyStateDescription.Text = String.IsNullOrWhiteSpace(message)
			? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("See the Redux log for details.")
			: message;
		ScanStatusText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Inspection failed");
		ScanActionButtonText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Close");
		ScanActionButton.IsEnabled = true;
		SetEmptyStateIcon("Redux.Icon.CloseCircle", "ReduxErrorBrush");
	}

	private void SetEmptyStateIcon(string geometryKey, string brushKey)
	{
		if (TryFindResource(geometryKey) is Geometry geometry)
		{
			EmptyStateIcon.StrokeData = geometry;
		}
		if (TryFindResource(brushKey) is Brush brush)
		{
			EmptyStateIcon.Foreground = brush;
		}
	}

	private static string BuildStatusText(ModFileOverlapScanResult result)
	{
		var uniquePathSummary = result.UniqueInternalPathCount == 1
			? "1 unique internal path checked"
			: $"{result.UniqueInternalPathCount:N0} unique internal paths checked";
		if (!result.HasFailures)
		{
			return uniquePathSummary;
		}

		var failureSummary = result.Failures.Count == 1
			? "1 package could not be scanned"
			: $"{result.Failures.Count} packages could not be scanned";
		return $"{uniquePathSummary} · {failureSummary}";
	}

	private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshFilter();

	private void RefreshFilter()
	{
		if (_result == null)
		{
			return;
		}

		var query = SearchTextBox?.Text?.Trim() ?? String.Empty;
		var visible = String.IsNullOrWhiteSpace(query)
			? _result.Entries
			: _result.Entries
				.Where(entry =>
					entry.InternalPath.Contains(query, StringComparison.OrdinalIgnoreCase)
					|| entry.Packages.Any(package =>
						package.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
						|| package.PackageFileName.Contains(query, StringComparison.OrdinalIgnoreCase)))
				.ToArray();
		OverlapList.ItemsSource = visible;
		VisibleResultCountText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Format($"{visible.Count} of {_result.OverlapPathCount}");
		EmptyState.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		if (visible.Count == 0 && _result.HasOverlaps)
		{
			EmptyStateTitle.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("No matching overlaps");
			EmptyStateDescription.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Try a different path or package name.");
		}
		else
		{
			EmptyStateTitle.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("No shared internal paths found");
			EmptyStateDescription.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("The scanned packages do not overlap.");
		}
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		if (_scanActive)
		{
			ScanActionButton.IsEnabled = false;
			ScanStatusText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Cancelling...");
			_scanCancellation.Cancel();
			return;
		}

		Close();
	}
}
