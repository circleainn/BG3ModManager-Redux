using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ReduxUpdater;

internal partial class SnapshotProgressWindow : Window
{
	private bool _canCancel = true;
	private bool _canceling;
	private bool _finished;
	private TaskCompletionSource? _acknowledgement;
	public event Action? CancelRequested;
	public SnapshotProgressWindow(ReduxSnapshotAppearance appearance)
	{
		InitializeComponent();
		appearance = appearance.Normalize();
		foreach (var color in appearance.Colors) Resources[color.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.Value));
		Resources["Redux.CornerRadius.Control"] = new CornerRadius(7);
		Resources["Redux.CornerRadius.Pill"] = new CornerRadius(10);
		Resources["Redux.FontSize.12"] = 12 * appearance.TextScale;
		Resources["Redux.FontSize.20"] = 20 * appearance.TextScale;
		FontFamily = appearance.FontFamily == "Segoe UI" ? new FontFamily("Segoe UI")
			: new FontFamily(new Uri("pack://application:,,,/ReduxUpdater;component/Resources/Fonts/"), "./#" + appearance.FontFamily);
		if (!appearance.ShowIcons) CaptionIcon.Visibility = OperationIcon.Visibility = ActionIcon.Visibility = Visibility.Collapsed;
		Width = Math.Min(580, Math.Max(320, SystemParameters.WorkArea.Width - 32));
		Closing += (_, e) => { if (!_finished) { e.Cancel = true; RequestCancelOrAcknowledge(); } };
		PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; RequestCancelOrAcknowledge(); } };
	}
	public void SetOperation(string operation)
	{
		Heading.Text = operation switch { "restore" => "Restoring snapshot", "recover" => "Recovering previous state", _ => "Creating snapshot" };
		OperationGlyph.Data = (Geometry)FindResource(operation == "create" ? "Redux.Icon.Save" : "Redux.Icon.Undo");
		Description.Text = operation == "restore" ? "Your current state will be saved before restoring. Redux will reopen when finished."
			: "Redux will reopen when the operation finishes.";
	}
	public void Report(ReduxSnapshotStore.Progress progress)
	{
		if (_acknowledgement != null || _finished) return;
		_canCancel = progress.CanCancel;
		ActionButton.IsEnabled = CloseWindowButton.IsEnabled = _canCancel && !_canceling;
		if (!_canceling) Details.Text = progress.Message;
		Progress.IsIndeterminate = progress.Total <= 0;
		if (progress.Total > 0)
		{
			Progress.Value = Math.Clamp(100d * progress.Current / progress.Total, 0, 100);
			ProgressSummary.Text = $"{Progress.Value:0}% · {progress.Current / 1048576d:N1} of {progress.Total / 1048576d:N1} MB";
		}
		else ProgressSummary.Text = "Working…";
		SafetyHint.Text = _canCancel ? "You can cancel while the snapshot is being prepared."
			: "Keep this window open. Cancellation is unavailable while files are being restored or recovered.";
	}
	public Task ShowResultAsync(string heading, string message)
	{
		Heading.Text = heading;
		Description.Text = "Review the result below.";
		Details.Text = message;
		Progress.Visibility = ProgressSummary.Visibility = SafetyHint.Visibility = Visibility.Collapsed;
		ActionLabel.Text = "Close";
		ActionButton.IsEnabled = CloseWindowButton.IsEnabled = true;
		CloseWindowButton.ToolTip = "Close";
		System.Windows.Automation.AutomationProperties.SetName(CloseWindowButton, "Close");
		_acknowledgement = new(TaskCreationOptions.RunContinuationsAsynchronously);
		return _acknowledgement.Task;
	}
	public void Finish() { _finished = true; Close(); }
	private void RequestCancelOrAcknowledge()
	{
		if (_acknowledgement != null) { _acknowledgement.TrySetResult(); return; }
		if (!_canCancel || _canceling) return;
		_canceling = true;
		Details.Text = "Canceling safely…";
		ActionButton.IsEnabled = CloseWindowButton.IsEnabled = false;
		CancelRequested?.Invoke();
	}
	private void Action_Click(object sender, RoutedEventArgs e) => RequestCancelOrAcknowledge();
	private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
	private void Caption_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is not System.Windows.Controls.Button && e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
