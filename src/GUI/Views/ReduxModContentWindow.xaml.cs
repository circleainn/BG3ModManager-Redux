using DivinityModManager.AppServices;
using DivinityModManager.Localization;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DivinityModManager.Views;

/// <summary>Read-only package inspection. Record values are never presented as a game-wide resolved result.</summary>
public partial class ReduxModContentWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly string _packagePath;
	private readonly CancellationTokenSource _cancellation = new();
	private readonly DispatcherTimer _searchDelay;
	private ModContentInspectionResult _result;
	private ModContentInspectionProgress _progress;
	private IReadOnlyDictionary<string, int> _categoryCounts = new Dictionary<string, int>();
	private bool _started;
	private bool _scanning;
	private bool _closing;
	private bool _closed;
	private bool _cancelled;
	private bool _failed;
	private int _filterVersion;
	private int _visibleRecords;
	private int _visibleFiles;

	public ReduxModContentWindow(string packagePath, string modName)
	{
		InitializeComponent();
		_packagePath = packagePath ?? String.Empty;
		_searchDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
		_searchDelay.Tick += SearchDelay_Tick;
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);

		ModNameText.Text = String.IsNullOrWhiteSpace(modName) ? Path.GetFileName(_packagePath) : modName;
		BindLabels();
		UpdateLanguage();
		PropertyChangedEventManager.AddHandler(ReduxLanguageService.Current, InterfaceLanguageChanged,
			nameof(ReduxLanguageService.Language));
		Loaded += Window_Loaded;
		Closing += (_, _) =>
		{
			_closing = true;
			if (!_closed) _cancellation.Cancel();
		};
		Closed += Window_Closed;
		PreviewKeyDown += Window_PreviewKeyDown;
	}

	private void BindLabels()
	{
		LocalizationBindings.Set(this, TitleProperty, "What this mod changes");
		LocalizationBindings.Set(HeadingText, TextBlock.TextProperty, "What this mod changes");
		LocalizationBindings.Set(ScopeText, TextBlock.TextProperty,
			"Browse this package's Stats records and file inventory. This view does not compare against the base game or other mods.");
		LocalizationBindings.Set(SearchTextBox, AutomationProperties.NameProperty, "Search records or package paths");
		LocalizationBindings.Set(SearchHintText, TextBlock.TextProperty, "Search records or package paths");
		LocalizationBindings.Set(SearchTextBox, ToolTipProperty, "Search by record name, type, parent, or package path. Ctrl+F focuses search.");
		LocalizationBindings.Set(ClearSearchText, TextBlock.TextProperty, "Clear search");
		LocalizationBindings.Set(ClearSearchButton, AutomationProperties.NameProperty, "Clear search");
		LocalizationBindings.Set(RecordsGrid, AutomationProperties.NameProperty, "Stats records");
		LocalizationBindings.Set(FieldsGrid, AutomationProperties.NameProperty, "Selected record fields");
		LocalizationBindings.Set(FilesGrid, AutomationProperties.NameProperty, "Package file inventory");
		LocalizationBindings.Set(IssuesGrid, AutomationProperties.NameProperty, "Inspection notes");
		LocalizationBindings.Set(RecordsSplitter, AutomationProperties.NameProperty, "Resize records and fields");
		foreach (var (column, label) in new (DataGridColumn, string)[]
		{
			(RecordNameColumn, "Record"), (RecordTypeColumn, "Type"), (RecordParentColumn, "Parent record"),
			(RecordSourceColumn, "Package path"), (FieldNameColumn, "Field"), (FieldValueColumn, "Value"),
			(FieldOriginColumn, "Origin"), (FieldSourceColumn, "Source record"), (FilePathColumn, "Package path"),
			(FileCategoryColumn, "Category"), (FileSizeColumn, "Bytes"), (IssueMessageColumn, "Note"),
			(IssueSourceColumn, "Package path"), (IssueLineColumn, "Line")
		}) LocalizationBindings.Set(column, DataGridColumn.HeaderProperty, label);
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		if (_started || _closed || _closing) return;
		_started = true;
		_scanning = true;
		UpdateLanguage();
		var progress = new Progress<ModContentInspectionProgress>(state =>
		{
			if (_closed || !_scanning || _cancellation.IsCancellationRequested) return;
			_progress = state;
			ScanProgress.IsIndeterminate = state.TotalFileCount <= 0;
			ScanProgress.Value = state.Fraction;
			UpdateStatus();
		});
		try
		{
			var result = await Task.Run(() => ModContentInspectionService.InspectPackage(
				_packagePath, _cancellation.Token, progress), _cancellation.Token);
			if (!_closed)
			{
				_cancellation.Token.ThrowIfCancellationRequested();
				ApplyResult(result);
			}
		}
		catch (OperationCanceledException)
		{
			_cancelled = true;
		}
		catch (Exception ex)
		{
			_failed = true;
			DivinityApp.Log($"Mod content inspection failed:\n{ex}");
		}
		finally
		{
			_scanning = false;
			if (_closed) _cancellation.Dispose();
			else
			{
				ScanProgress.Visibility = Visibility.Collapsed;
				ScanActionButton.IsEnabled = true;
				UpdateLanguage();
			}
		}
	}

	// Also used by offscreen layout checks, avoiding a real package read or visible window.
	internal void ApplyResult(ModContentInspectionResult result)
	{
		_started = true;
		_result = result ?? throw new ArgumentNullException(nameof(result));
		_cancelled = false;
		_failed = false;
		_visibleRecords = result.Records.Count;
		_visibleFiles = result.Files.Count;
		_categoryCounts = result.Files.GroupBy(file => file.Category)
			.ToDictionary(group => group.Key, group => group.Count());
		RecordsGrid.ItemsSource = result.Records;
		FilesGrid.ItemsSource = result.Files;
		IssuesGrid.ItemsSource = result.Issues;
		RecordsGrid.SelectedIndex = result.Records.Count > 0 ? 0 : -1;
		SearchTextBox.IsEnabled = true;
		ScanProgress.Visibility = Visibility.Collapsed;
		UpdateLanguage();
	}

	private void Window_Closed(object sender, EventArgs e)
	{
		_closed = true;
		_filterVersion++;
		_searchDelay.Stop();
		_cancellation.Cancel();
		if (!_scanning) _cancellation.Dispose();
		PropertyChangedEventManager.RemoveHandler(ReduxLanguageService.Current, InterfaceLanguageChanged,
			nameof(ReduxLanguageService.Language));
	}

	private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			e.Handled = true;
			ScanActionButton_Click(ScanActionButton, new RoutedEventArgs());
		}
		else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && SearchTextBox.IsEnabled)
		{
			e.Handled = true;
			SearchTextBox.Focus();
			SearchTextBox.SelectAll();
		}
	}

	private void ScanActionButton_Click(object sender, RoutedEventArgs e)
	{
		if (!_started)
		{
			_started = true;
			_cancelled = true;
			_cancellation.Cancel();
			ScanProgress.Visibility = Visibility.Collapsed;
			UpdateLanguage();
			return;
		}
		if (!_scanning) { Close(); return; }
		ScanActionButton.IsEnabled = false;
		_cancellation.Cancel();
		UpdateStatus();
	}

	private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_searchDelay == null || _result == null) return;
		_filterVersion++;
		ClearSearchButton.IsEnabled = !String.IsNullOrEmpty(SearchTextBox.Text);
		_searchDelay.Stop();
		_searchDelay.Start();
	}

	private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
	{
		SearchTextBox.Clear();
		SearchTextBox.Focus();
	}

	private async void SearchDelay_Tick(object sender, EventArgs e)
	{
		_searchDelay.Stop();
		await RefreshFilterAsync();
	}

	internal async Task RefreshFilterAsync()
	{
		if (_result == null || _closed) return;
		_searchDelay.Stop();
		var result = _result;
		var version = ++_filterVersion;
		var query = SearchTextBox.Text?.Trim() ?? String.Empty;
		var selection = RecordsGrid.SelectedItem as ModContentRecord;
		// A package may contain a large file table. Filter on a worker, then replace ItemsSource once.
		var filtered = await Task.Run(() => (
			Records: result.Records.Where(record => query.Length == 0
				|| Matches(record.Name, query) || Matches(record.Type, query)
				|| Matches(record.ParentName, query) || Matches(record.SourcePath, query)).ToArray(),
			Files: result.Files.Where(file => query.Length == 0 || Matches(file.SourcePath, query)).ToArray()));
		if (_closed || Dispatcher.HasShutdownStarted) return;
		await Dispatcher.InvokeAsync(() =>
		{
			if (_closed || version != _filterVersion) return;
			RecordsGrid.ItemsSource = filtered.Records;
			FilesGrid.ItemsSource = filtered.Files;
			RecordsGrid.SelectedItem = selection != null && filtered.Records.Contains(selection) ? selection : filtered.Records.FirstOrDefault();
			_visibleRecords = filtered.Records.Length;
			_visibleFiles = filtered.Files.Length;
			UpdateLanguage();
		});
	}

	private static bool Matches(string value, string query) => value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

	private void RecordsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectedRecord();

	private void UpdateSelectedRecord()
	{
		if (FieldsGrid == null) return;
		var language = ReduxLanguageService.Current;
		if (RecordsGrid.SelectedItem is not ModContentRecord record)
		{
			FieldsGrid.ItemsSource = null;
			RecordDetailText.Text = language.Translate("Select a record to inspect its fields.");
			EmptyFieldsText.Text = language.Translate("No record selected");
			EmptyFieldsText.Visibility = Visibility.Visible;
			return;
		}

		FieldsGrid.ItemsSource = record.Fields.Select(field => new FieldRow(
			field.Name, field.Value, field.SourceRecord, field.IsInherited ? "Inherited" : "Declared")).ToArray();
		EmptyFieldsText.Text = language.Translate("No fields were read for this record.");
		EmptyFieldsText.Visibility = record.Fields.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		var status = record.HasIncompleteInheritance
			? language.Translate("Inheritance is incomplete. Only values resolved within this package are shown.")
			: String.IsNullOrEmpty(record.ParentName)
				? language.Translate("Declared fields in this record.")
				: language.Translate("Inherited fields come from parent records inside this package.");
		RecordDetailText.Text = language.Format($"{record.SourcePath} · line {record.Line}") + Environment.NewLine + status;
	}

	private void InterfaceLanguageChanged(object sender, PropertyChangedEventArgs e)
	{
		if (_closed) return;
		UpdateLanguage();
	}

	private void UpdateLanguage()
	{
		var language = ReduxLanguageService.Current;
		ScanActionText.Text = language.Translate(_scanning || !_started ? "Cancel" : "Close");
		RecordsTab.Header = language.Translate("Stats records");
		FilesTab.Header = language.Translate("Package files");
		IssuesTab.Header = _result == null ? language.Translate("Inspection notes")
			: language.Format($"Inspection notes ({_result.Issues.Count})");
		SummaryText.Text = _result == null ? String.Empty
			: language.Format($"{_visibleRecords} of {_result.Records.Count} records · {_visibleFiles} of {_result.Files.Count} files");
		FileCategoryText.Text = String.Join(" · ", _categoryCounts.OrderBy(pair => pair.Key)
			.Select(pair => language.Format($"{language.Translate(pair.Key)}: {pair.Value}")));
		EmptyRecordsText.Visibility = _result == null || _visibleRecords == 0 ? Visibility.Visible : Visibility.Collapsed;
		EmptyRecordsText.Text = language.Translate(_result == null
			? (_cancelled ? "Inspection cancelled" : _failed ? "Package could not be inspected" : "Reading package contents...")
			: _result.Records.Count == 0
				? "No supported Stats records were read. Package files and inspection notes may have more information."
				: "No matching records. Try a different name, type, parent, or path.");
		EmptyFilesText.Visibility = _result == null || _visibleFiles == 0 ? Visibility.Visible : Visibility.Collapsed;
		EmptyFilesText.Text = language.Translate(_result == null ? "File inventory is not available."
			: "No matching package files.");
		EmptyIssuesText.Visibility = _result == null || _result.Issues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		EmptyIssuesText.Text = language.Translate(_result == null ? "Inspection notes are not available."
			: "No inspection notes. This does not establish compatibility with other mods.");
		UpdateSelectedRecord();
		UpdateStatus();
	}

	private void UpdateStatus()
	{
		var language = ReduxLanguageService.Current;
		StatusText.SetResourceReference(TextBlock.ForegroundProperty,
			_failed ? "ReduxErrorBrush" : _result?.IsPartial == true ? "ReduxWarningBrush" : "ReduxTextSecondaryBrush");
		StatusText.Text = _scanning
			? _cancellation.IsCancellationRequested ? language.Translate("Cancelling...")
				: _progress == null ? language.Translate("Reading package contents...")
					: language.Format($"Reading Stats files: {_progress.CompletedFileCount} of {_progress.TotalFileCount}")
						+ (String.IsNullOrEmpty(_progress.CurrentFilePath) ? String.Empty : " · " + _progress.CurrentFilePath)
			: _failed ? language.Translate("Inspection failed. See the Redux log for details.")
			: _cancelled ? language.Translate("Inspection cancelled. The package was not changed.")
			: _result?.IsPartial == true ? language.Translate("Partial inspection. Open Inspection notes for skipped or unresolved content.")
			: _result != null ? language.Translate("Inspection complete. Only supported Stats content was interpreted; other files are listed by path.")
			: language.Translate("Reading package contents...");
	}

	private sealed record FieldRow(string Name, string Value, string SourceRecord, string Origin);
}
