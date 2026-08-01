

using DivinityModManager.Models;
using DivinityModManager.Util;

using DynamicData;
using DynamicData.Binding;

using System.Windows;

namespace DivinityModManager.ViewModels;

public class FileDeletionCompleteEventArgs : EventArgs
{
	public int TotalFilesDeleted => DeletedFiles?.Count ?? 0;
	public List<ModFileDeletionData> DeletedFiles { get; set; }
	public List<string> FailureMessages { get; set; }
	public bool RemoveFromLoadOrder { get; set; }
	public bool IsDeletingDuplicates { get; set; }

	public FileDeletionCompleteEventArgs()
	{
		DeletedFiles = [];
		FailureMessages = [];
	}
}

public class DeleteFilesViewData : BaseProgressViewModel
{
	[Reactive] public bool PermanentlyDelete { get; set; }
	[Reactive] public bool RemoveFromLoadOrder { get; set; }
	[Reactive] public bool IsDeletingDuplicates { get; set; }
	[Reactive] public double DuplicateColumnWidth { get; set; }

	public ObservableCollectionExtended<ModFileDeletionData> Files { get; set; } = new ObservableCollectionExtended<ModFileDeletionData>();

	private readonly ObservableAsPropertyHelper<bool> _anySelected;
	public bool AnySelected => _anySelected.Value;

	private readonly ObservableAsPropertyHelper<bool> _allSelected;
	public bool AllSelected => _allSelected.Value;

	private readonly ObservableAsPropertyHelper<string> _selectAllTooltip;
	public string SelectAllTooltip => _selectAllTooltip.Value;

	private readonly ObservableAsPropertyHelper<string> _title;
	public string Title => _title.Value;

	private readonly ObservableAsPropertyHelper<Visibility> _removeFromLoadOrderVisibility;
	public Visibility RemoveFromLoadOrderVisibility => _removeFromLoadOrderVisibility.Value;

	public RxCommandUnit SelectAllCommand { get; private set; }

	public event EventHandler<FileDeletionCompleteEventArgs> FileDeletionComplete;

	public override async Task<bool> Run(CancellationToken cts)
	{
		var targetFiles = Files.Where(x => x.IsSelected).ToList();
		if (targetFiles.Count == 0)
		{
			return false;
		}

		var eventArgs = new FileDeletionCompleteEventArgs()
		{
			IsDeletingDuplicates = IsDeletingDuplicates,
			RemoveFromLoadOrder = !IsDeletingDuplicates && RemoveFromLoadOrder,
		};

		await Observable.Start(() => IsProgressActive = true, RxApp.MainThreadScheduler);
		await UpdateProgress($"Deleting {targetFiles.Count} mod file(s)...", "", 0d);
		double progressInc = 1d / targetFiles.Count;
		foreach (var f in targetFiles)
		{
			try
			{
				if (cts.IsCancellationRequested)
				{
					DivinityApp.Log("Deletion stopped.");
					break;
				}
				if (!File.Exists(f.FilePath))
				{
					DivinityApp.Log($"Mod file was already absent: '{f.FilePath}'. Removing the stale UI entry.");
					eventArgs.DeletedFiles.Add(f);
				}
				else
				{
					await UpdateProgress("", $"Deleting {f.FilePath}...");
					var deleteReportedSuccess = RecycleBinHelper.DeleteFile(f.FilePath, false, PermanentlyDelete, out var deleteError);
					if (deleteReportedSuccess && !File.Exists(f.FilePath))
					{
						eventArgs.DeletedFiles.Add(f);
						DivinityApp.Log($"Deleted mod file '{f.FilePath}' ({(PermanentlyDelete ? "permanently" : "Recycle Bin")}).");
					}
					else
					{
						var reason = !String.IsNullOrWhiteSpace(deleteError)
							? deleteError
							: File.Exists(f.FilePath) ? "The file still exists after the delete operation." : "The delete operation failed.";
						var failure = $"{Path.GetFileName(f.FilePath)}: {reason}";
						eventArgs.FailureMessages.Add(failure);
						DivinityApp.Log($"Failed to delete mod file '{f.FilePath}': {reason}");
					}
				}
			}
			catch (Exception ex)
			{
				var failure = $"{Path.GetFileName(f.FilePath)}: {ex.Message}";
				eventArgs.FailureMessages.Add(failure);
				DivinityApp.Log($"Error deleting file '{f.FilePath}':\n{ex}");
			}
			await UpdateProgress("", "", ProgressValue + progressInc);
		}
		await UpdateProgress("", "", 1d);
		await Task.Delay(500);
		RxApp.MainThreadScheduler.Schedule(() =>
		{
			FileDeletionComplete?.Invoke(this, eventArgs);
			Close();
		});
		return true;
	}

	public override void Close()
	{
		base.Close();
		Files.Clear();
	}

	public void ToggleSelectAll()
	{
		var b = !AllSelected;
		foreach (var f in Files)
		{
			f.IsSelected = b;
		}
	}

	private bool IsClosingAllowed(bool isDeletingDupes, int totalFiles) => !isDeletingDupes || totalFiles <= 0;

	public DeleteFilesViewData() : base()
	{
		RemoveFromLoadOrder = true;
		PermanentlyDelete = false;

		//this.WhenAnyValue(x => x.IsDeletingDuplicates, x => x.Files.Count).Select(x => IsClosingAllowed(x.Item1, x.Item2)).BindTo(this, x => x.CanClose);

		_removeFromLoadOrderVisibility = this.WhenAnyValue(x => x.IsDeletingDuplicates).Select(x => x ? Visibility.Collapsed : Visibility.Visible).ToProperty(this, nameof(RemoveFromLoadOrderVisibility), true, RxApp.MainThreadScheduler);
		_title = this.WhenAnyValue(x => x.IsDeletingDuplicates).Select(b => !b ? "Files to Delete" : "Duplicate Mods to Delete").ToProperty(this, nameof(Title), true, RxApp.MainThreadScheduler);

		var filesChanged = this.Files.ToObservableChangeSet().AutoRefresh(x => x.IsSelected).ToCollection().Throttle(TimeSpan.FromMilliseconds(50)).ObserveOn(RxApp.MainThreadScheduler);
		_anySelected = filesChanged.Select(x => x.Any(y => y.IsSelected)).ToProperty(this, nameof(AnySelected));

		_allSelected = filesChanged.Select(x => x.All(y => y.IsSelected)).ToProperty(this, nameof(AllSelected), true, RxApp.MainThreadScheduler);
		_selectAllTooltip = this.WhenAnyValue(x => x.AllSelected).Select(b => $"{(b ? "Deselect" : "Select")} All").ToProperty(this, nameof(SelectAllTooltip), true, RxApp.MainThreadScheduler);

		SelectAllCommand = ReactiveCommand.Create(ToggleSelectAll, this.RunCommand.IsExecuting.Select(b => !b), RxApp.MainThreadScheduler);

		this.WhenAnyValue(x => x.AnySelected).BindTo(this, x => x.CanRun);
	}
}
