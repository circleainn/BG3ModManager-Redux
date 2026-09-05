

using DivinityModManager.Models.Updates;
using DivinityModManager.Util;
using DivinityModManager.Views;

using DynamicData;
using DynamicData.Binding;

using Ookii.Dialogs.Wpf;

using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DivinityModManager.ViewModels;

public struct CopyModUpdatesTask
{
	public List<string> NewFilesToMove;
	public List<string> UpdatesToMove;
	public string DocumentsFolder;
	public string ModPakFolder;
	public int TotalMoved;
	public int TotalFailed;
	public bool WasCancelled;
	public List<string> Errors;
}

public class ModUpdatesViewData : ReactiveObject
{
	[Reactive] public bool Unlocked { get; set; }
	[Reactive] public bool JustUpdated { get; set; }

	public SourceList<DivinityModUpdateData> Mods { get; private set; } = new SourceList<DivinityModUpdateData>();

	private readonly ReadOnlyObservableCollection<DivinityModUpdateData> _newMods;
	public ReadOnlyObservableCollection<DivinityModUpdateData> NewMods => _newMods;

	private readonly ReadOnlyObservableCollection<DivinityModUpdateData> _updatedMods;
	public ReadOnlyObservableCollection<DivinityModUpdateData> UpdatedMods => _updatedMods;

	readonly ObservableAsPropertyHelper<bool> _anySelected;
	public bool AnySelected => _anySelected.Value;

	readonly ObservableAsPropertyHelper<bool> _allNewModsSelected;
	public bool AllNewModsSelected => _allNewModsSelected.Value;

	readonly ObservableAsPropertyHelper<bool> _allModUpdatesSelected;
	public bool AllModUpdatesSelected => _allModUpdatesSelected.Value;

	readonly ObservableAsPropertyHelper<bool> _newAvailable;
	public bool NewAvailable => _newAvailable.Value;

	readonly ObservableAsPropertyHelper<bool> _updatesAvailable;
	public bool UpdatesAvailable => _updatesAvailable.Value;

	readonly ObservableAsPropertyHelper<int> _totalUpdates;
	public int TotalUpdates => _totalUpdates.Value;

	public ICommand CopySelectedModsCommand { get; private set; }
	public ICommand SelectAllNewModsCommand { get; private set; }
	public ICommand SelectAllUpdatesCommand { get; private set; }

	public Action OnLoaded { get; set; }

	public Action<bool> CloseView { get; set; }

	private readonly MainWindowViewModel _mainWindowViewModel;

	public void Clear()
	{
		Mods.Clear();
		Unlocked = true;
	}

	public void SelectAll(bool select = true)
	{
		foreach (var x in Mods.Items)
		{
			x.IsSelected = select;
		}
	}

	private IEnumerable<string> GetUpdateFiles(string directoryPath)
	{
		var files = DivinityFileUtils.EnumerateFiles(directoryPath, DivinityFileUtils.RecursiveOptions, f => Path.GetExtension(f).Equals(".pak", StringComparison.OrdinalIgnoreCase));
		return files;
	}

	private static string GetUniqueBackupPath(string backupFolder, string sourcePath)
	{
		var candidate = Path.Combine(backupFolder, Path.GetFileName(sourcePath));
		if (!File.Exists(candidate)) return candidate;
		var name = Path.GetFileNameWithoutExtension(sourcePath);
		var extension = Path.GetExtension(sourcePath);
		var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
		candidate = Path.Combine(backupFolder, $"{name}_{timestamp}{extension}");
		var suffix = 1;
		while (File.Exists(candidate))
			candidate = Path.Combine(backupFolder, $"{name}_{timestamp}_{suffix++}{extension}");
		return candidate;
	}

	private void CopySelectedMods_Run()
	{
		string documentsFolder = _mainWindowViewModel.PathwayData.AppDataGameFolder;
		string modPakFolder = _mainWindowViewModel.PathwayData.AppDataModsPath;

		if (Directory.Exists(modPakFolder))
		{
			Unlocked = false;
			using ProgressDialog dialog = new ProgressDialog()
			{
				WindowTitle = "Updating Mods",
				Text = "Copying mods...",
				CancellationText = "Update Cancelled",
				MinimizeBox = false,
				ProgressBarStyle = ProgressBarStyle.ProgressBar
			};
			dialog.DoWork += CopyFilesProgress_DoWork;
			dialog.RunWorkerCompleted += CopyFilesProgress_RunWorkerCompleted;

			var args = new CopyModUpdatesTask()
			{
				DocumentsFolder = documentsFolder,
				ModPakFolder = modPakFolder,
				NewFilesToMove = NewMods.Where(x => x.IsSelected).Select(x => GetUpdateFiles(Path.GetDirectoryName(x.UpdateFilePath))).SelectMany(x => x).ToList(),
				UpdatesToMove = UpdatedMods.Where(x => x.IsSelected).Select(x => GetUpdateFiles(Path.GetDirectoryName(x.UpdateFilePath))).SelectMany(x => x).ToList(),
				TotalMoved = 0
			};

			dialog.ShowDialog(MainWindow.Self, args);
		}
		else
		{
			CloseView?.Invoke(false);
		}
	}

	public void CopySelectedMods()
	{
		using var dialog = new TaskDialog()
		{
			Buttons =
				{
					new TaskDialogButton(ButtonType.Yes),
					new TaskDialogButton(ButtonType.No)
				},
			WindowTitle = "Update Mods?",
			Content = "Override local mods with the selected updates?",
			MainIcon = TaskDialogIcon.Warning
		};
		var result = dialog.ShowDialog(MainWindow.Self);
		if (result.ButtonType == ButtonType.Yes)
		{
			CopySelectedMods_Run();
		}
	}

	private void CopyFilesProgress_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
	{
		Unlocked = true;
		var refresh = false;
		try
		{
			if (e.Error != null) throw e.Error;
			if (e.Result is CopyModUpdatesTask args)
			{
				JustUpdated = args.TotalMoved > 0;
				refresh = JustUpdated;
				var status = args.WasCancelled ? "cancelled" : "complete";
				DivinityApp.Log($"Mod updating {status}: {args.TotalMoved} succeeded, {args.TotalFailed} failed.");
				if (args.TotalFailed > 0)
				{
					var names = args.Errors?.Where(message => !String.IsNullOrWhiteSpace(message)).Take(3).ToArray()
						?? Array.Empty<string>();
					var details = names.Length > 0 ? $"\n{String.Join("\n", names)}" : String.Empty;
					_mainWindowViewModel.ShowAlert(
						$"{args.TotalFailed} mod file{(args.TotalFailed == 1 ? "" : "s")} could not be updated. " +
						$"{args.TotalMoved} completed successfully.{details}",
						AlertType.Danger,
						30);
				}
			}
		}
		catch (Exception ex)
		{
			string message = $"Error copying mods: {ex}";
			DivinityApp.Log(message);
			_mainWindowViewModel.ShowAlert(message, AlertType.Danger, 30);
		}
		CloseView?.Invoke(refresh);
	}

	private void CopyFilesProgress_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
	{
		ProgressDialog dialog = (ProgressDialog)sender;
		if (e.Argument is CopyModUpdatesTask args)
		{
			var workItems = args.NewFilesToMove
				.Select(file => (File: file, IsUpdate: false))
				.Concat(args.UpdatesToMove.Select(file => (File: file, IsUpdate: true)))
				.ToList();
			var totalWork = workItems.Count;
			var processed = 0;
			string backupFolder = Path.Combine(_mainWindowViewModel.PathwayData.AppDataGameFolder, "Mods_Old_ModManager");
			Directory.CreateDirectory(backupFolder);
			DivinityApp.Log($"Installing {args.NewFilesToMove.Count} new mod file(s) and {args.UpdatesToMove.Count} update file(s).");

			foreach (var workItem in workItems)
			{
				if (dialog.CancellationPending)
				{
					args.WasCancelled = true;
					break;
				}

				var fileName = Path.GetFileName(workItem.File);
				dialog.ReportProgress(
					totalWork == 0 ? 0 : (int)Math.Round(processed * 100d / totalWork),
					$"{(workItem.IsUpdate ? "Updating" : "Installing")} '{fileName}'...",
					null);
				try
				{
					var destinationPath = Path.Combine(args.ModPakFolder, fileName);
					string backupPath = null;
					if (File.Exists(destinationPath))
					{
						backupPath = GetUniqueBackupPath(backupFolder, destinationPath);
					}
					AtomicFileWriter.CopyFile(workItem.File, destinationPath, backupPath);
					if (backupPath != null)
					{
						DivinityApp.Log($"Replaced '{destinationPath}' and saved the previous file to '{backupPath}'.");
					}
					args.TotalMoved++;
				}
				catch (Exception ex)
				{
					args.TotalFailed++;
					args.Errors ??= new List<string>();
					args.Errors.Add($"{fileName}: {ex.Message}");
					DivinityApp.Log($"Could not safely {(workItem.IsUpdate ? "update" : "install")} '{fileName}':\n{ex}");
				}

				processed++;
				dialog.ReportProgress(
					totalWork == 0 ? 100 : (int)Math.Round(processed * 100d / totalWork),
					$"Processed '{fileName}'.",
					null);
			}

			e.Result = args;
		}

	}

	public ModUpdatesViewData(MainWindowViewModel mainWindowViewModel)
	{
		Unlocked = true;

		_mainWindowViewModel = mainWindowViewModel;

		var modsConnection = Mods.Connect();

		_totalUpdates = modsConnection.Count().ToProperty(this, nameof(TotalUpdates));

		var splitList = modsConnection.AutoRefresh(x => x.IsNewMod);
		var newModsConnection = splitList.Filter(x => x.IsNewMod);
		var updatedModsConnection = splitList.Filter(x => !x.IsNewMod);

		newModsConnection.Bind(out _newMods).Subscribe();
		updatedModsConnection.Bind(out _updatedMods).Subscribe();

		var hasNewMods = newModsConnection.Count().Select(x => x > 0);
		var hasUpdatedMods = updatedModsConnection.Count().Select(x => x > 0);
		_newAvailable = hasNewMods.ToProperty(this, nameof(NewAvailable));
		_updatesAvailable = hasUpdatedMods.ToProperty(this, nameof(UpdatesAvailable));

		var selectedMods = modsConnection.AutoRefresh(x => x.IsSelected).ToCollection();
		_anySelected = selectedMods.Select(x => x.Any(y => y.IsSelected)).ToProperty(this, nameof(AnySelected), true, RxApp.MainThreadScheduler);

		var newModsChangeSet = NewMods.ToObservableChangeSet().AutoRefresh(x => x.IsSelected).ToCollection();
		var modUpdatesChangeSet = UpdatedMods.ToObservableChangeSet().AutoRefresh(x => x.IsSelected).ToCollection();

		_allNewModsSelected = splitList.Filter(x => x.IsNewMod).ToCollection().Select(x => x.All(y => y.IsSelected)).ToProperty(this, nameof(AllNewModsSelected), true, RxApp.MainThreadScheduler);
		_allModUpdatesSelected = splitList.Filter(x => !x.IsNewMod).ToCollection().Select(x => x.All(y => y.IsSelected)).ToProperty(this, nameof(AllModUpdatesSelected), true, RxApp.MainThreadScheduler);

		var anySelectedObservable = this.WhenAnyValue(x => x.AnySelected);

		CopySelectedModsCommand = ReactiveCommand.Create(CopySelectedMods, anySelectedObservable);

		SelectAllNewModsCommand = ReactiveCommand.Create<bool>((b) =>
		{
			foreach (var x in NewMods)
			{
				x.IsSelected = b;
			}
		}, hasNewMods);
		SelectAllUpdatesCommand = ReactiveCommand.Create<bool>((b) =>
		{
			foreach (var x in UpdatedMods)
			{
				x.IsSelected = b;
			}
		}, hasUpdatedMods);
	}
}
