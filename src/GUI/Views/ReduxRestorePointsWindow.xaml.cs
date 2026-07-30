using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DivinityModManager.Views;

public partial class ReduxRestorePointsWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly string _restorePointsDirectory;
	private readonly string _profileUuid;
	private readonly string _profileName;
	private readonly string _sourceOrderName;
	private readonly IReadOnlyList<DivinityLoadOrderEntry> _workingOrder;
	private readonly IReadOnlyList<DivinityLoadOrder> _availableOrders;
	private readonly ObservableCollection<LoadOrderRestorePoint> _restorePoints = [];

	public LoadOrderRestorePoint SelectedRestorePoint { get; private set; }
	public bool Accepted { get; private set; }

	public ReduxRestorePointsWindow(
		Window owner,
		string restorePointsDirectory,
		string profileUuid,
		string profileName,
		string sourceOrderName,
		IReadOnlyList<DivinityLoadOrderEntry> workingOrder,
		IReadOnlyList<DivinityLoadOrder> availableOrders)
	{
		InitializeComponent();
		_restorePointsDirectory = restorePointsDirectory;
		_profileUuid = profileUuid;
		_profileName = profileName;
		_sourceOrderName = sourceOrderName;
		_workingOrder = workingOrder ?? [];
		_availableOrders = availableOrders ?? [];
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

		RestorePointList.ItemsSource = _restorePoints;
		RefreshRestorePoints();
	}

	private void RefreshRestorePoints(string selectedId = null)
	{
		var points = LoadOrderRestorePointService.Load(_restorePointsDirectory, _profileUuid);
		_restorePoints.Clear();
		foreach (var point in points)
		{
			_restorePoints.Add(point);
		}

		EmptyState.Visibility = _restorePoints.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		SnapshotCountText.Text = _restorePoints.Count == 1
			? "1 saved"
			: $"{_restorePoints.Count} saved";
		if (_restorePoints.Count > 0)
		{
			RestorePointList.SelectedItem = String.IsNullOrWhiteSpace(selectedId)
				? _restorePoints[0]
				: _restorePoints.FirstOrDefault(point =>
					String.Equals(point.Id, selectedId, StringComparison.OrdinalIgnoreCase)) ?? _restorePoints[0];
		}
		else
		{
			RestorePointList.SelectedItem = null;
			RefreshSelectedComparison();
		}
	}

	private async void CreateButton_Click(object sender, RoutedEventArgs e)
	{
		CreateButton.IsEnabled = false;
		StatusText.Text = "Capturing the current working order…";
		var order = _workingOrder
			.Where(entry => entry != null)
			.Select(entry => entry.Clone())
			.ToArray();
		var result = await Task.Run(() =>
		{
			var saved = LoadOrderRestorePointService.TryCreate(
				_restorePointsDirectory,
				_profileUuid,
				_profileName,
				_sourceOrderName,
				"Manual snapshot",
				order,
				out var restorePoint,
				out var error);
			return (Saved: saved, RestorePoint: restorePoint, Error: error);
		});

		CreateButton.IsEnabled = true;
		if (!result.Saved)
		{
			StatusText.Text = "Could not capture the current working order.";
			DivinityApp.Log($"Could not create manual load-order restore point: {result.Error}");
			return;
		}

		RefreshRestorePoints(result.RestorePoint.Id);
		StatusText.Text = "Current working order captured in history.";
	}

	private void RestorePointList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		SelectedRestorePoint = RestorePointList.SelectedItem as LoadOrderRestorePoint;
		LoadButton.IsEnabled = SelectedRestorePoint != null;
		DeleteButton.IsEnabled = SelectedRestorePoint != null;
		RefreshSelectedComparison();
	}

	private void RestorePointList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (ItemsControl.ContainerFromElement(
				RestorePointList,
				e.OriginalSource as DependencyObject) is ListBoxItem)
		{
			AcceptSelection();
		}
	}

	private void LoadButton_Click(object sender, RoutedEventArgs e) => AcceptSelection();

	private void CompareOrdersButton_Click(object sender, RoutedEventArgs e)
	{
		var orders = BuildComparisonOrders();
		if (orders.Count < 2)
		{
			StatusText.Text = "At least two orders or snapshots are needed for comparison.";
			return;
		}

		var dialog = new ReduxLoadOrderComparisonWindow(
			this,
			orders,
			0,
			1);
		dialog.ShowDialog();
	}

	private IReadOnlyList<DivinityLoadOrder> BuildComparisonOrders()
	{
		var orders = new List<DivinityLoadOrder>();
		var selected = RestorePointList.SelectedItem as LoadOrderRestorePoint;
		if (selected != null)
		{
			orders.Add(CreateSnapshotOrder(selected));
		}

		orders.Add(new DivinityLoadOrder
		{
			Name = $"Current · {_sourceOrderName}",
			Order = _workingOrder
				.Where(entry => entry != null)
				.Select(entry => entry.Clone())
				.ToList()
		});

		foreach (var order in _availableOrders.Where(order => order != null))
		{
			orders.Add(new DivinityLoadOrder
			{
				Name = $"Saved · {order.Name}",
				FilePath = order.FilePath,
				IsModSettings = order.IsModSettings,
				LastModifiedDate = order.LastModifiedDate,
				Order = (order.Order ?? [])
					.Where(entry => entry != null)
					.Select(entry => entry.Clone())
					.ToList()
			});
		}

		foreach (var snapshot in _restorePoints.Where(snapshot =>
			selected == null
			|| !String.Equals(snapshot.Id, selected.Id, StringComparison.OrdinalIgnoreCase)))
		{
			orders.Add(CreateSnapshotOrder(snapshot));
		}

		return orders;
	}

	private static DivinityLoadOrder CreateSnapshotOrder(LoadOrderRestorePoint snapshot) =>
		new()
		{
			Name = $"Snapshot · {snapshot.CreatedSummary} · {snapshot.SourceOrderName}",
			Order = (snapshot.Order ?? [])
				.Where(entry => entry != null)
				.Select(entry => entry.Clone())
				.ToList()
		};

	private void RefreshSelectedComparison()
	{
		var restorePoint = RestorePointList.SelectedItem as LoadOrderRestorePoint;
		if (restorePoint == null)
		{
			SelectedCreatedText.Text = "Select a snapshot";
			SelectedReasonText.Text = "Choose a snapshot to review its changes.";
			SelectedSourceText.Text = "Source order";
			SelectedModCountText.Text = "0 mods";
			AddedCountText.Text = "0";
			RemovedCountText.Text = "0";
			MovedCountText.Text = "0";
			DifferenceCountText.Text = "0 changes";
			ChangeList.ItemsSource = null;
			NoChangesState.Visibility = Visibility.Collapsed;
			return;
		}

		SelectedCreatedText.Text = restorePoint.CreatedSummary;
		SelectedReasonText.Text = String.IsNullOrWhiteSpace(restorePoint.Reason)
			? "Redux snapshot"
			: restorePoint.Reason;
		SelectedSourceText.Text = $"Source order: {restorePoint.SourceOrderName}";
		SelectedModCountText.Text = restorePoint.ModCountSummary;

		var comparison = LoadOrderComparisonService.CompareSavedOrders(
			restorePoint.Order ?? [],
			_workingOrder);
		var changes = comparison.Changes
			.Select(change => new ReduxLoadOrderComparisonChangeItem(change))
			.ToArray();
		ChangeList.ItemsSource = changes;
		AddedCountText.Text = comparison.Activated.Count.ToString();
		RemovedCountText.Text = comparison.Deactivated.Count.ToString();
		MovedCountText.Text = comparison.Repositioned.Count.ToString();
		DifferenceCountText.Text = changes.Length == 1
			? "1 change"
			: $"{changes.Length} changes";
		NoChangesState.Visibility = changes.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
	}

	private async void DeleteButton_Click(object sender, RoutedEventArgs e)
	{
		var restorePoint = RestorePointList.SelectedItem as LoadOrderRestorePoint;
		if (restorePoint == null)
		{
			return;
		}

		var result = ReduxMessageBox.Show(
			this,
			$"Delete the snapshot from {restorePoint.CreatedSummary}?\n\nThis removes only the Redux history entry. It does not change the working order, installed mods, or game files.",
			"Delete Snapshot",
			MessageBoxButton.YesNo,
			MessageBoxImage.Warning,
			MessageBoxResult.No);
		if (result != MessageBoxResult.Yes)
		{
			return;
		}

		DeleteButton.IsEnabled = false;
		LoadButton.IsEnabled = false;
		StatusText.Text = "Deleting snapshot…";
		var deletion = await Task.Run(() =>
		{
			var deleted = LoadOrderRestorePointService.TryDelete(
				_restorePointsDirectory,
				_profileUuid,
				restorePoint.Id,
				out var error);
			return (Deleted: deleted, Error: error);
		});

		if (!deletion.Deleted)
		{
			StatusText.Text = "Could not delete the snapshot.";
			DivinityApp.Log($"Could not delete load-order restore point: {deletion.Error}");
			RefreshRestorePoints();
			return;
		}

		RefreshRestorePoints();
		StatusText.Text = "Snapshot deleted.";
	}

	private void AcceptSelection()
	{
		SelectedRestorePoint = RestorePointList.SelectedItem as LoadOrderRestorePoint;
		if (SelectedRestorePoint == null)
		{
			return;
		}

		Accepted = true;
		Close();
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
