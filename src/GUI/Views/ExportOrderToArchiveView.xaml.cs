using DivinityModManager.Converters;
using DivinityModManager.ViewModels;

namespace DivinityModManager.Views;

public class ExportOrderToArchiveViewViewBase : ReactiveUserControl<ExportOrderToArchiveViewModel> { }

/// <summary>
/// Interaction logic for ExportOrderToArchiveView.xaml
/// </summary>
public partial class ExportOrderToArchiveView : ExportOrderToArchiveViewViewBase
{
	public ExportOrderToArchiveView()
	{
		InitializeComponent();

		ViewModel = new ExportOrderToArchiveViewModel();
		DataContext = ViewModel;

		this.WhenActivated(d =>
		{
			if (this.ViewModel != null)
			{
				d(this.OneWayBind(ViewModel, vm => vm.IsVisible, view => view.Visibility, BoolToVisibilityConverter.FromBool));

				d(this.OneWayBind(ViewModel, vm => vm.Entries, v => v.FilesListView.ItemsSource));

				d(this.OneWayBind(ViewModel, vm => vm.IsRunning, v => v.ProgressIndicator.IsBusy));
				d(this.OneWayBind(ViewModel, vm => vm.ProgressTitle, v => v.TaskProgressContent.Title));
				d(this.OneWayBind(ViewModel, vm => vm.ProgressWorkText, v => v.TaskProgressContent.Status));
				d(this.OneWayBind(ViewModel, vm => vm.ProgressValue, v => v.TaskProgressContent.ProgressValue));

				d(this.Bind(ViewModel, vm => vm.IncludeOverrides, view => view.IncludeOverridesCheckbox.IsChecked));
				d(this.Bind(ViewModel, vm => vm.SelectedOrderType, view => view.OrderTypeComboBox.SelectedItem));
				d(this.Bind(ViewModel, vm => vm.OrderTypes, view => view.OrderTypeComboBox.ItemsSource));

				d(this.BindCommand(ViewModel, vm => vm.RunCommand, v => v.ConfirmButton));
				d(this.OneWayBind(ViewModel, vm => vm.CancelRunCommand, v => v.TaskProgressContent.CancelCommand));
				d(this.BindCommand(ViewModel, vm => vm.CloseCommand, v => v.CancelButton));
			}
		});
	}
}
