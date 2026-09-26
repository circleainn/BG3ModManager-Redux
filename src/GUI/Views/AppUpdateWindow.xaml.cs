using DivinityModManager.Controls;
using DivinityModManager.ViewModels;

using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;

using ReactiveMarbles.ObservableEvents;
namespace DivinityModManager.Views;

public class AppUpdateWindowBase : HideWindowBase<AppUpdateWindowViewModel> { }

public partial class AppUpdateWindow : AppUpdateWindowBase
{
	private readonly Lazy<Markdown> _fallbackMarkdown = new(() => new Markdown());
	private readonly Markdown _defaultMarkdown;

	private FlowDocument StringToMarkdown(string text)
	{
		var markdown = _defaultMarkdown ?? _fallbackMarkdown.Value;
		var doc = markdown.Transform(text);
		return doc;
	}

	public override void HideWindow_Closing(object sender, CancelEventArgs e)
	{
		if (ViewModel.IsBackingUp)
		{
			e.Cancel = true;
			ViewModel.CancelBackup();
			return;
		}
		base.HideWindow_Closing(sender, e);
		ViewModel.IsVisible = false;
	}

	public AppUpdateWindow()
	{
		InitializeComponent();

		ViewModel = Services.Get<AppUpdateWindowViewModel>();

		var obj = TryFindResource("DefaultMarkdown");
		if (obj != null && obj is Markdown markdown)
		{
			_defaultMarkdown = markdown;
		}

		this.WhenActivated(d =>
		{
			d(this.BindCommand(ViewModel, vm => vm.ConfirmCommand, v => v.ConfirmButton));
			d(this.BindCommand(ViewModel, vm => vm.BackupAndConfirmCommand, v => v.BackupAndConfirmButton));
			d(this.BindCommand(ViewModel, vm => vm.SkipCommand, v => v.SkipButton));
			d(this.OneWayBind(ViewModel, vm => vm.ConfirmButtonText, v => v.ConfirmButtonText.Text));
			d(this.OneWayBind(ViewModel, vm => vm.CanConfirm, v => v.ConfirmButton.Visibility,
				canConfirm => canConfirm ? Visibility.Visible : Visibility.Collapsed));
			d(this.OneWayBind(ViewModel, vm => vm.CanConfirm, v => v.BackupAndConfirmButton.Visibility,
				canConfirm => canConfirm ? Visibility.Visible : Visibility.Collapsed));
			d(this.OneWayBind(ViewModel, vm => vm.HasAvailableUpdate, v => v.BackupExplanation.Visibility,
				available => available ? Visibility.Visible : Visibility.Collapsed));
			d(this.OneWayBind(ViewModel, vm => vm.SkipButtonText, v => v.SkipButtonText.Text));
			d(this.OneWayBind(ViewModel, vm => vm.UpdateDescription, v => v.UpdateDescription.Text));
			d(this.OneWayBind(ViewModel, vm => vm.UpdateProgress, v => v.UpdateProgressBar.Value));
			d(this.OneWayBind(ViewModel, vm => vm.IsProgressVisible, v => v.UpdateProgressBar.Visibility,
				visible => visible ? Visibility.Visible : Visibility.Collapsed));
			d(this.OneWayBind(ViewModel, vm => vm.UpdateChangelogView, v => v.UpdateChangelogView.Document, StringToMarkdown));

			this.Events().IsVisibleChanged.Select(x => x.NewValue).BindTo(ViewModel, x => x.IsVisible);
		});
	}
}
