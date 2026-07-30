using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public sealed class ReduxLoadOrderComparisonChangeItem
{
	public LoadOrderChangeKind Kind { get; }
	public string Name { get; }
	public string ChangeSummary { get; }
	public string PositionSummary { get; }

	public ReduxLoadOrderComparisonChangeItem(LoadOrderChange change)
	{
		Kind = change.Kind;
		Name = change.Name;
		ChangeSummary = change.Kind switch
		{
			LoadOrderChangeKind.Activated => "Added in compared order",
			LoadOrderChangeKind.Deactivated => "Only in baseline order",
			LoadOrderChangeKind.Repositioned => "Position changed",
			_ => "Changed"
		};
		PositionSummary = change.Kind switch
		{
			LoadOrderChangeKind.Activated => $"Position {change.NextPosition}",
			LoadOrderChangeKind.Deactivated => $"Baseline {change.PreviousPosition}",
			LoadOrderChangeKind.Repositioned => $"{change.PreviousPosition} to {change.NextPosition}",
			_ => String.Empty
		};
	}
}

public partial class ReduxLoadOrderComparisonWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly IReadOnlyList<DivinityLoadOrder> _orders;
	private bool _initializing;

	public ReduxLoadOrderComparisonWindow(
		Window owner,
		IReadOnlyList<DivinityLoadOrder> orders,
		int baselineIndex,
		int comparedIndex)
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

		_orders = orders ?? [];
		_initializing = true;
		BaselineComboBox.ItemsSource = _orders;
		ComparedComboBox.ItemsSource = _orders;
		BaselineComboBox.SelectedIndex = Math.Clamp(baselineIndex, 0, Math.Max(0, _orders.Count - 1));
		ComparedComboBox.SelectedIndex = Math.Clamp(comparedIndex, 0, Math.Max(0, _orders.Count - 1));
		_initializing = false;
		RefreshComparison();
	}

	private void OrderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_initializing)
		{
			RefreshComparison();
		}
	}

	private void SwapOrdersButton_Click(object sender, RoutedEventArgs e)
	{
		_initializing = true;
		var baselineIndex = BaselineComboBox.SelectedIndex;
		BaselineComboBox.SelectedIndex = ComparedComboBox.SelectedIndex;
		ComparedComboBox.SelectedIndex = baselineIndex;
		_initializing = false;
		RefreshComparison();
	}

	private void RefreshComparison()
	{
		if (BaselineComboBox.SelectedItem is not DivinityLoadOrder baseline
			|| ComparedComboBox.SelectedItem is not DivinityLoadOrder compared)
		{
			ChangeList.ItemsSource = null;
			NoChangesState.Visibility = Visibility.Visible;
			return;
		}

		var comparison = LoadOrderComparisonService.CompareSavedOrders(baseline.Order, compared.Order);
		var changes = comparison.Changes
			.Select(change => new ReduxLoadOrderComparisonChangeItem(change))
			.ToArray();
		ChangeList.ItemsSource = changes;
		NoChangesState.Visibility = changes.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		AddedCountText.Text = comparison.Activated.Count.ToString();
		RemovedCountText.Text = comparison.Deactivated.Count.ToString();
		MovedCountText.Text = comparison.Repositioned.Count.ToString();
		ComparisonTitleText.Text = $"{baseline.Name} compared with {compared.Name}";
		OrderCountText.Text =
			$"{baseline.Order.Count} vs {compared.Order.Count} {(compared.Order.Count == 1 ? "mod" : "mods")}";
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
