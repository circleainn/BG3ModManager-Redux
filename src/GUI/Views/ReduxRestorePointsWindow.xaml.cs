using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DivinityModManager.Views;

public partial class ReduxRestorePointsWindow : AdonisUI.Controls.AdonisWindow
{
	public LoadOrderRestorePoint SelectedRestorePoint { get; private set; }
	public bool Accepted { get; private set; }

	public ReduxRestorePointsWindow(
		Window owner,
		IReadOnlyList<LoadOrderRestorePoint> restorePoints)
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

		var points = restorePoints ?? [];
		RestorePointList.ItemsSource = points;
		EmptyState.Visibility = points.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		if (points.Count > 0)
		{
			RestorePointList.SelectedIndex = 0;
		}
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
