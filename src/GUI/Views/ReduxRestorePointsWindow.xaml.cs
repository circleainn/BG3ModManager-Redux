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
	private readonly ObservableCollection<LoadOrderRestorePoint> _restorePoints = [];

	public LoadOrderRestorePoint SelectedRestorePoint { get; private set; }
	public bool Accepted { get; private set; }

	public ReduxRestorePointsWindow(
		Window owner,
		string restorePointsDirectory,
		string profileUuid,
		string profileName,
		string sourceOrderName,
		IReadOnlyList<DivinityLoadOrderEntry> workingOrder)
	{
		InitializeComponent();
		_restorePointsDirectory = restorePointsDirectory;
		_profileUuid = profileUuid;
		_profileName = profileName;
		_sourceOrderName = sourceOrderName;
		_workingOrder = workingOrder ?? [];
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
		if (_restorePoints.Count > 0)
		{
			RestorePointList.SelectedItem = String.IsNullOrWhiteSpace(selectedId)
				? _restorePoints[0]
				: _restorePoints.FirstOrDefault(point =>
					String.Equals(point.Id, selectedId, StringComparison.OrdinalIgnoreCase)) ?? _restorePoints[0];
		}
	}

	private async void CreateButton_Click(object sender, RoutedEventArgs e)
	{
		CreateButton.IsEnabled = false;
		StatusText.Text = "Creating restore point…";
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
			StatusText.Text = "Could not create the restore point.";
			DivinityApp.Log($"Could not create manual load-order restore point: {result.Error}");
			return;
		}

		RefreshRestorePoints(result.RestorePoint.Id);
		StatusText.Text = "Current working order captured.";
	}

	private void RestorePointList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		SelectedRestorePoint = RestorePointList.SelectedItem as LoadOrderRestorePoint;
		LoadButton.IsEnabled = SelectedRestorePoint != null;
	}

	private void RestorePointList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (RestorePointList.SelectedItem is LoadOrderRestorePoint)
		{
			AcceptSelection();
		}
	}

	private void LoadButton_Click(object sender, RoutedEventArgs e) => AcceptSelection();

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
