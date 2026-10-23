using DivinityModManager.Localization;
using DivinityModManager.AppServices;
using DivinityModManager.Util;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public partial class ReduxOverrideDuplicateWindow : AdonisUI.Controls.AdonisWindow
{
	public sealed record ChoiceOption(string Label, OverrideDuplicateChoice? Value)
	{
		public string DisplayLabel => ReduxLanguageService.Current.Translate(Label);
	}
	public sealed class ConflictRow(OverrideDuplicateConflict conflict)
	{
		public string Name => conflict.FileName;
		public string Comparison => conflict.Identical
			? "Identical contents. Keeping the Mods copy is recommended."
			: "Different contents or package parts. Choose the version you want to retain.";
		public string ModsSummary => Summary("Mods", conflict.ModsFiles);
		public string HeldSummary => Summary("Holding", conflict.HeldFiles);
		public OverrideDuplicateChoice? Choice { get; set; } = conflict.Identical ? OverrideDuplicateChoice.KeepMods : null;
		public IReadOnlyList<ChoiceOption> Options { get; } = [new("Choose a copy…", null),
			new("Keep the Mods copy", OverrideDuplicateChoice.KeepMods), new("Keep the held copy", OverrideDuplicateChoice.KeepHeld)];
		private static string Summary(string label, IReadOnlyList<OverrideDuplicateFile> files) => ReduxLanguageService.Current.Translate(label) + ":\n"
			+ String.Join("\n", files.Select(file => ReduxLanguageService.Current.Format($"{file.Path}\n{file.Length:N0} bytes · {file.ModifiedUtc.ToLocalTime():g}")));
	}

	private readonly OverrideDuplicateRecoveryService _service;
	private readonly CancellationTokenSource _cancellation = new();
	private OverrideDuplicateReview _review;
	private ConflictRow[] _rows = [];
	private bool _busy;
	private bool _closeRequested;
	public bool Resolved { get; private set; }
	public string BackupDirectory { get; private set; }

	public ReduxOverrideDuplicateWindow(Window owner, OverrideDuplicateRecoveryService service)
	{
		_service = service ?? throw new ArgumentNullException(nameof(service));
		InitializeComponent();
		if (owner?.IsLoaded == true) Owner = owner;
		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null) ReduxThemeService.Apply(Resources, settings.ColorTheme,
			ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		Loaded += async (_, _) => await ReviewAsync();
		Closing += (_, args) =>
		{
			if (!_busy) return;
			args.Cancel = true;
			_closeRequested = true;
			_cancellation.Cancel();
			SetStatus("Waiting for file work and recovery to finish…");
		};
		Closed += (_, _) => _cancellation.Dispose();
	}

	private async Task ReviewAsync()
	{
		SetBusy(true);
		_review = null;
		_rows = [];
		ConflictItems.ItemsSource = _rows;
		SetStatus("Checking recovery records and comparing the complete package contents…");
		try
		{
			await _service.RecoverPendingAsync(_cancellation.Token);
			_review = await _service.ReviewAsync(_cancellation.Token);
			_rows = _review.Conflicts.Select(conflict => new ConflictRow(conflict)).ToArray();
			ConflictItems.ItemsSource = _rows;
			SetStatus(_rows.Length == 0 ? "No conflicting Override copies were found. Close this window and refresh the library."
				: ReduxLanguageService.Current.Format($"{_rows.Length} conflicting package group(s). Backups will be retained in {_service.BackupRoot}."));
		}
		catch (OperationCanceledException) { SetStatus("Review cancelled."); }
		catch (Exception ex) { _closeRequested = false; SetStatus(ex.Message, showInView: true); }
		finally { SetBusy(false); if (_closeRequested) Close(); }
	}

	private async void Apply_Click(object sender, RoutedEventArgs e)
	{
		if (_busy || _review == null || _rows.Length == 0 || _rows.Any(row => row.Choice == null)) return;
		SetBusy(true);
		SetStatus("Rechecking the reviewed copies and creating recovery backups…");
		try
		{
			var choices = _rows.ToDictionary(row => row.Name, row => row.Choice.Value, StringComparer.OrdinalIgnoreCase);
			var result = await _service.ApplyAsync(_review, choices, _cancellation.Token);
			BackupDirectory = result.BackupDirectory;
			Resolved = true;
			_closeRequested = true;
		}
		catch (OperationCanceledException) { SetStatus("Cancelled. Any staged file changes were rolled back."); }
		catch (Exception ex) { _closeRequested = false; SetStatus(ex.Message, showInView: true); _review = null; }
		finally { SetBusy(false); if (_closeRequested) Close(); }
	}

	private void SetStatus(string message, bool showInView = false)
	{
		LocalizationBindings.Set(StatusText, TextBlock.TextProperty, message);
		UIElementAutomationPeer.CreatePeerForElement(StatusText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
		if (showInView)
			Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => StatusText.BringIntoView()));
	}

	private void SetBusy(bool value)
	{
		_busy = value;
		BusyProgress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
		ConflictItems.IsEnabled = !value;
		ReviewButton.IsEnabled = !value && !_cancellation.IsCancellationRequested;
		UpdateApply();
	}
	private void UpdateApply() => ApplyButton.IsEnabled = !_busy && _review != null && _rows.Length > 0 && _rows.All(row => row.Choice != null);
	private void Choice_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		// SelectedValue's binding update follows SelectionChanged on some WPF templates.
		Dispatcher.BeginInvoke(new Action(UpdateApply));
	}
	private async void ReviewAgain_Click(object sender, RoutedEventArgs e) { if (!_busy) await ReviewAsync(); }
	private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
	private void OpenMods_Click(object sender, RoutedEventArgs e) => OpenFolder(_service.ModsFolder);
	private void OpenHolding_Click(object sender, RoutedEventArgs e) => OpenFolder(_service.HoldingFolder);
	private void OpenFolder(string path)
	{
		try
		{
			if (!Directory.Exists(path)) { SetStatus("This folder does not exist yet."); return; }
			Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
		}
		catch (Exception ex) { SetStatus(ex.Message, showInView: true); }
	}
	private void CopyDetails_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			var details = _review == null ? StatusText.Text : String.Join("\n\n", _review.Conflicts.Select(conflict =>
				conflict.FileName + (conflict.Identical ? " — identical contents" : " — different copies") + "\n"
				+ String.Join("\n", conflict.ModsFiles.Concat(conflict.HeldFiles).Select(file => $"{file.Path}\n{file.Length} bytes · SHA-256 {file.Sha256}"))));
			Clipboard.SetText(details);
			SetStatus("Details copied. They contain local file paths; redact them before sharing publicly.");
		}
		catch (Exception ex) { SetStatus(ex.Message, showInView: true); }
	}
}
