using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using EngineProgram = ReduxModDatabaseTool.Program;

namespace ReduxModDatabaseTool.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged
{
	private string? _approvedPreviewSignature;
	private bool _isBusy;
	private string _emptyStateText = "Choose a contribution report, then review it against the Redux database.";

	public ObservableCollection<ReviewProjectItem> ReviewItems { get; } = new();
	public bool HasReviewItems => ReviewItems.Count > 0;
	public string EmptyStateText
	{
		get => _emptyStateText;
		private set
		{
			if (_emptyStateText == value) return;
			_emptyStateText = value;
			OnPropertyChanged();
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public MainWindow()
	{
		InitializeComponent();
		ReviewItems.CollectionChanged += ReviewItems_CollectionChanged;
		DataContext = this;
		ApplyStartupPaths();
		UpdateActionState();
	}

	private void BrowseReport_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			Title = "Open Redux database contribution",
			Filter = "Redux contribution reports (*.bg3redux-report)|*.bg3redux-report|JSON files (*.json)|*.json"
		};
		if (dialog.ShowDialog(this) == true)
			ReportPathBox.Text = dialog.FileName;
	}

	private void BrowseDatabase_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			Title = "Open Redux mod database",
			Filter = "Redux mod database (ReduxModDatabase.json)|ReduxModDatabase.json|JSON files (*.json)|*.json"
		};
		if (dialog.ShowDialog(this) == true)
			DatabasePathBox.Text = dialog.FileName;
	}

	private async void Review_Click(object sender, RoutedEventArgs e)
	{
		if (!ValidateInputPaths()) return;
		await ReviewReportAsync();
	}

	private async Task ReviewReportAsync()
	{
		await WithBusyStateAsync(async () =>
		{
			var reviewPath = Path.Combine(Path.GetTempPath(), $"redux-review-{Guid.NewGuid():N}.json");
			try
			{
				var result = await RunEngineAsync(
					"review-report",
					"--file", ReportPathBox.Text,
					"--database", DatabasePathBox.Text,
					"--output", reviewPath);
				OutputBox.Text = result.Output;
				if (result.ExitCode != 0 && !File.Exists(reviewPath))
					throw new InvalidDataException("The contribution report did not pass review.");

				PopulateReviewItems(reviewPath);
				var selectable = ReviewItems.Count(item => item.CanSelect);
				var conflicts = ReviewItems.Count(item => item.Status == "conflict");
				var known = ReviewItems.Count(item => item.Status == "alreadyKnown");
				var unavailable = ReviewItems.Count(item => !item.CanSelect
					&& item.Status is not "conflict" and not "alreadyKnown");
				SummaryText.Text = String.Join(
					" \u2022 ",
					$"{ReviewItems.Sum(item => item.PackageCount)} packages",
					$"{ReviewItems.Count} projects",
					$"{selectable} eligible",
					$"{known} already known",
					$"{conflicts} conflicts",
					$"{unavailable} not eligible");
				StatusText.Text = selectable == 0
					? "No candidate project is ready for acceptance."
					: "Select only projects whose Nexus identity and file records were independently confirmed.";
				EmptyStateText = "The report passed validation but contains no project records.";
			}
			finally
			{
				if (File.Exists(reviewPath)) File.Delete(reviewPath);
			}
		});
	}

	private async void PreviewSelected_Click(object sender, RoutedEventArgs e)
	{
		var selected = SelectedModIds();
		if (selected.Count == 0) return;

		await WithBusyStateAsync(async () =>
		{
			var result = await RunEngineAsync(BuildAcceptArguments(selected, write: false));
			OutputBox.Text = result.Output;
			if (result.ExitCode != 0)
				throw new InvalidDataException("The selected batch did not pass validation.");

			_approvedPreviewSignature = CurrentSelectionSignature(selected);
			PreviewStateText.Text = $"Previewed {selected.Count} Nexus project(s)";
			StatusText.Text = "Preview succeeded. Apply remains explicit and writes the full selection atomically.";
		});
		UpdateActionState();
	}

	private async void ApplySelected_Click(object sender, RoutedEventArgs e)
	{
		var selected = SelectedModIds();
		if (_approvedPreviewSignature != CurrentSelectionSignature(selected))
		{
			InvalidatePreview();
			return;
		}

		var confirmation = MessageBox.Show(
			this,
			$"Apply {selected.Count} reviewed Nexus project(s) to the bundled Redux database?\n\n"
			+ "The complete batch will be validated and written as one atomic update.",
			"Apply approved database batch",
			MessageBoxButton.YesNo,
			MessageBoxImage.Warning,
			MessageBoxResult.No);
		if (confirmation != MessageBoxResult.Yes) return;

		await WithBusyStateAsync(async () =>
		{
			var result = await RunEngineAsync(BuildAcceptArguments(selected, write: true));
			OutputBox.Text = result.Output;
			if (result.ExitCode != 0)
				throw new InvalidDataException("The approved batch was not written.");

			PreviewStateText.Text = "Database updated";
			StatusText.Text = "The approved batch was written and the resulting database passed validation.";
		});
		await ReviewReportAsync();
	}

	private void InputPath_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
	{
		ReviewItems.Clear();
		SummaryText.Text = HasBothInputPaths()
			? "Ready to review the selected report."
			: "Choose a contribution report and Redux database to begin.";
		EmptyStateText = "Review the selected report to populate candidate projects.";
		InvalidatePreview();
		UpdateActionState();
	}

	private void ReviewItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
		OnPropertyChanged(nameof(HasReviewItems));

	private void PopulateReviewItems(string reviewPath)
	{
		using var document = JsonDocument.Parse(File.ReadAllText(reviewPath));
		var sourceItems = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
		var groups = sourceItems
			.GroupBy(
				item => ReadNexusId(item) > 0
					? $"nexus:{ReadNexusId(item)}"
					: $"other:{ReadString(item, "displayName")}:{ReadString(item, "pakHash")}",
				StringComparer.OrdinalIgnoreCase)
			.Select(group => ReviewProjectItem.Create(group.ToArray()))
			.OrderByDescending(item => item.CanSelect)
			.ThenBy(item => item.StatusSort)
			.ThenBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		ReviewItems.Clear();
		foreach (var item in groups)
		{
			item.SelectionChanged += ReviewItem_SelectionChanged;
			ReviewItems.Add(item);
		}
		InvalidatePreview();
		UpdateActionState();
	}

	private void ReviewItem_SelectionChanged(object? sender, EventArgs e)
	{
		InvalidatePreview();
		UpdateActionState();
	}

	private IReadOnlyList<long> SelectedModIds() => ReviewItems
		.Where(item => item.IsSelected && item.CanSelect && item.NexusId > 0)
		.Select(item => item.NexusId)
		.Distinct()
		.OrderBy(value => value)
		.ToArray();

	private string[] BuildAcceptArguments(IReadOnlyList<long> selected, bool write)
	{
		var arguments = new List<string>
		{
			"accept-report",
			"--file", ReportPathBox.Text,
			"--database", DatabasePathBox.Text,
			"--mod-ids", String.Join(",", selected)
		};
		if (write) arguments.Add("--write");
		return arguments.ToArray();
	}

	private string CurrentSelectionSignature(IReadOnlyList<long> selected)
	{
		try
		{
			var reportPath = Path.GetFullPath(ReportPathBox.Text);
			var databasePath = Path.GetFullPath(DatabasePathBox.Text);
			var reportStamp = File.Exists(reportPath)
				? File.GetLastWriteTimeUtc(reportPath).Ticks
				: 0;
			var databaseStamp = File.Exists(databasePath)
				? File.GetLastWriteTimeUtc(databasePath).Ticks
				: 0;
			return $"{reportPath}|{reportStamp}|{databasePath}|{databaseStamp}|{String.Join(",", selected)}";
		}
		catch
		{
			return String.Empty;
		}
	}

	private bool ValidateInputPaths()
	{
		if (!File.Exists(ReportPathBox.Text))
		{
			StatusText.Text = "Choose an existing Redux contribution report.";
			ReportPathBox.Focus();
			return false;
		}
		if (!File.Exists(DatabasePathBox.Text))
		{
			StatusText.Text = "Choose the ReduxModDatabase.json file to review against.";
			DatabasePathBox.Focus();
			return false;
		}
		return true;
	}

	private async Task WithBusyStateAsync(Func<Task> action)
	{
		if (_isBusy) return;
		_isBusy = true;
		UpdateActionState();
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			StatusText.Text = ex.Message;
			EmptyStateText = "Review failed. Correct the paths or report data, then try again.";
		}
		finally
		{
			_isBusy = false;
			UpdateActionState();
		}
	}

	private void InvalidatePreview()
	{
		_approvedPreviewSignature = null;
		PreviewStateText.Text = "Nothing previewed";
	}

	private void UpdateActionState()
	{
		var hasSelection = SelectedModIds().Count > 0;
		ReviewButton.IsEnabled = !_isBusy && HasBothInputPaths();
		PreviewButton.IsEnabled = !_isBusy && hasSelection;
		ApplyButton.IsEnabled = !_isBusy
			&& hasSelection
			&& !String.IsNullOrEmpty(_approvedPreviewSignature)
			&& _approvedPreviewSignature == CurrentSelectionSignature(SelectedModIds());
	}

	private static async Task<EngineResult> RunEngineAsync(params string[] arguments)
	{
		return await Task.Run(async () =>
		{
			var originalOut = Console.Out;
			var originalError = Console.Error;
			using var output = new StringWriter();
			using var error = new StringWriter();
			try
			{
				Console.SetOut(output);
				Console.SetError(error);
				var exitCode = await EngineProgram.Main(arguments);
				var combined = output.ToString();
				if (error.GetStringBuilder().Length > 0)
					combined += (combined.Length > 0 ? Environment.NewLine : String.Empty) + error;
				return new EngineResult(exitCode, combined.Trim());
			}
			finally
			{
				Console.SetOut(originalOut);
				Console.SetError(originalError);
			}
		});
	}

	private static string? FindDefaultDatabasePath()
	{
		var configuredPath = Environment.GetEnvironmentVariable("BG3MM_REDUX_DATABASE");
		if (File.Exists(configuredPath))
			return Path.GetFullPath(configuredPath);

		foreach (var startingPath in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory }
			         .Distinct(StringComparer.OrdinalIgnoreCase))
		{
			var directory = new DirectoryInfo(startingPath);
			while (directory is not null)
			{
				foreach (var relativePath in new[]
				         {
						"ReduxModDatabase.json",
						Path.Combine("Resources", "ReduxModDatabase.json"),
						Path.Combine("src", "GUI", "Resources", "ReduxModDatabase.json")
				         })
				{
					var candidate = Path.Combine(directory.FullName, relativePath);
					if (File.Exists(candidate)) return Path.GetFullPath(candidate);
				}
				directory = directory.Parent;
			}
		}
		return null;
	}

	private void ApplyStartupPaths()
	{
		var arguments = ParseStartupArguments(Environment.GetCommandLineArgs().Skip(1).ToArray());
		ReportPathBox.Text = arguments.ReportPath ?? String.Empty;
		DatabasePathBox.Text = arguments.DatabasePath ?? FindDefaultDatabasePath() ?? String.Empty;

		if (!String.IsNullOrWhiteSpace(ReportPathBox.Text) && File.Exists(ReportPathBox.Text))
		{
			SummaryText.Text = File.Exists(DatabasePathBox.Text)
				? "Report supplied at startup. Ready to review."
				: "Report supplied at startup. Choose a Redux database.";
		}
		else if (File.Exists(DatabasePathBox.Text))
		{
			SummaryText.Text = "Redux database found automatically. Choose a contribution report.";
		}
	}

	private bool HasBothInputPaths() =>
		File.Exists(ReportPathBox?.Text) && File.Exists(DatabasePathBox?.Text);

	private static StartupArguments ParseStartupArguments(IReadOnlyList<string> arguments)
	{
		string? reportPath = null;
		string? databasePath = null;
		for (var index = 0; index < arguments.Count; index++)
		{
			var argument = arguments[index];
			if (argument.Equals("--report", StringComparison.OrdinalIgnoreCase)
			    && index + 1 < arguments.Count)
			{
				reportPath = arguments[++index];
			}
			else if (argument.Equals("--database", StringComparison.OrdinalIgnoreCase)
			         && index + 1 < arguments.Count)
			{
				databasePath = arguments[++index];
			}
			else if (!argument.StartsWith("-", StringComparison.Ordinal)
			         && reportPath is null
			         && argument.EndsWith(".bg3redux-report", StringComparison.OrdinalIgnoreCase))
			{
				reportPath = argument;
			}
		}

		return new StartupArguments(
			NormalizePath(reportPath),
			NormalizePath(databasePath));
	}

	private static string? NormalizePath(string? path)
	{
		if (String.IsNullOrWhiteSpace(path)) return null;
		try
		{
			return Path.GetFullPath(path);
		}
		catch
		{
			return path;
		}
	}

	private static long ReadNexusId(JsonElement item)
	{
		return item.TryGetProperty("nexus", out var nexus)
			&& nexus.ValueKind == JsonValueKind.Object
			&& nexus.TryGetProperty("modId", out var value)
			&& value.TryGetInt64(out var id)
				? id
				: 0;
	}

	private static long ReadNexusFileId(JsonElement item)
	{
		return item.TryGetProperty("nexus", out var nexus)
			&& nexus.ValueKind == JsonValueKind.Object
			&& nexus.TryGetProperty("fileId", out var value)
			&& value.TryGetInt64(out var id)
				? id
				: 0;
	}

	private static string ReadString(JsonElement value, string property)
	{
		return value.TryGetProperty(property, out var nested) && nested.ValueKind == JsonValueKind.String
			? nested.GetString() ?? String.Empty
			: String.Empty;
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	private sealed record EngineResult(int ExitCode, string Output);

	public sealed class ReviewProjectItem : INotifyPropertyChanged
	{
		private bool _isSelected;

		public long NexusId { get; init; }
		public string NexusIdText => NexusId > 0 ? NexusId.ToString() : "—";
		public string ProjectName { get; init; } = String.Empty;
		public string Status { get; init; } = String.Empty;
		public string StatusLabel { get; init; } = String.Empty;
		public int StatusSort { get; init; }
		public string Reason { get; init; } = String.Empty;
		public int PackageCount { get; init; }
		public bool CanSelect { get; init; }

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				var next = CanSelect && value;
				if (_isSelected == next) return;
				_isSelected = next;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
				SelectionChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		public event EventHandler? SelectionChanged;

		public static ReviewProjectItem Create(IReadOnlyList<JsonElement> items)
		{
			var first = items[0];
			var status = SelectAggregateStatus(items.Select(item => ReadString(item, "status")));
			var nexusId = ReadNexusId(first);
			var complete = nexusId > 0 && items.All(item => ReadNexusFileId(item) > 0);
			var projectName = first.TryGetProperty("nexus", out var nexus)
				&& nexus.ValueKind == JsonValueKind.Object
				&& nexus.TryGetProperty("name", out var name)
				&& name.ValueKind == JsonValueKind.String
					? name.GetString()
					: null;
			projectName ??= ReadString(first, "displayName");
			var candidate = status is "candidateNewProject" or "candidateKnownProject";
			var reasons = items
				.Select(item => ReadString(item, "reason"))
				.Where(reason => reason.Length > 0)
				.Distinct(StringComparer.Ordinal)
				.ToArray();

			return new ReviewProjectItem
			{
				NexusId = nexusId,
				ProjectName = projectName ?? "Unnamed project",
				Status = status,
				StatusLabel = StatusDisplay(status, complete),
				StatusSort = StatusOrder(status),
				Reason = !complete && candidate
					? "A Nexus file ID is missing. " + String.Join(" ", reasons)
					: String.Join(" ", reasons),
				PackageCount = items.Count,
				CanSelect = candidate && complete
			};
		}

		private static string SelectAggregateStatus(IEnumerable<string> statuses)
		{
			return statuses
				.OrderBy(StatusOrder)
				.FirstOrDefault() ?? "unavailable";
		}

		private static int StatusOrder(string status) => status switch
		{
			"conflict" => 0,
			"candidateNewProject" => 1,
			"candidateKnownProject" => 2,
			"alreadyKnown" => 3,
			"nonNexus" => 4,
			_ => 5
		};

		private static string StatusDisplay(string status, bool complete) => status switch
		{
			"conflict" => "Conflict",
			"candidateNewProject" when !complete => "Incomplete candidate",
			"candidateNewProject" => "New project",
			"candidateKnownProject" when !complete => "Incomplete candidate",
			"candidateKnownProject" => "New package",
			"alreadyKnown" => "Already known",
			"nonNexus" => "Not Nexus",
			_ => "Unavailable"
		};
	}

	private sealed record StartupArguments(string? ReportPath, string? DatabasePath);
}
