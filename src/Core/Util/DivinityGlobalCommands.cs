

using DivinityModManager.Models;
using DivinityModManager.ViewModels;

using System.Diagnostics;
using System.Windows;

namespace DivinityModManager.Util;

public class DivinityGlobalCommands : ReactiveObject
{
	private IDivinityAppViewModel _viewModel;

	public IDivinityAppViewModel ViewModel => _viewModel;

	public void SetViewModel(IDivinityAppViewModel vm)
	{
		_viewModel = vm;
		this.RaisePropertyChanged(nameof(ViewModel));
	}

	public ReactiveCommand<string, Unit> OpenFileCommand { get; private set; }
	public ReactiveCommand<string, Unit> OpenInFileExplorerCommand { get; private set; }
	public RxCommandUnit ClearMissingModsCommand { get; private set; }
	public ReactiveCommand<DivinityModData, Unit> ToggleNameDisplayCommand { get; private set; }
	public ReactiveCommand<string, Unit> CopyToClipboardCommand { get; private set; }
	public ReactiveCommand<DivinityModData, Unit> DeleteModCommand { get; private set; }
	public ReactiveCommand<DivinityModData, Unit> MoveModToActiveCommand { get; private set; }
	public ReactiveCommand<DivinityModData, Unit> MoveModToInactiveCommand { get; private set; }
	public ReactiveCommand<DivinityModData, Unit> OpenModSourcePageCommand { get; private set; }
	public ReactiveCommand<DivinityModData, Unit> OpenNexusModsPageCommand { get; private set; }
	public ReactiveCommand<DivinityModData, Unit> OpenModGalleryPageCommand { get; private set; }
	public ReactiveCommand<DivinityModData, Unit> OpenModChangelogPageCommand { get; private set; }
	public ReactiveCommand<DivinityModData, Unit> OpenNexusModsChangelogPageCommand { get; private set; }
	public ReactiveCommand<string, Unit> OpenURLCommand { get; private set; }
	public ReactiveCommand<DivinityModData, Unit> ToggleForceAllowInLoadOrderCommand { get; private set; }

	public void OpenFile(string path)
	{
		if (File.Exists(path))
		{
			try
			{
				Process.Start(Path.GetFullPath(path));
			}
			catch (System.ComponentModel.Win32Exception) // No File Association
			{
				Process.Start("explorer.exe", $"\"{Path.GetFullPath(path)}\"");
			}
		}
		else if (Directory.Exists(path))
		{
			Process.Start("explorer.exe", $"\"{Path.GetFullPath(path)}\"");
		}
		else
		{
			_viewModel.ShowAlert($"Error opening '{path}': File does not exist!", AlertType.Danger, 10);
		}
	}

	public void OpenInFileExplorer(string path)
	{
		if (File.Exists(path))
		{
			Process.Start("explorer.exe", $"/select, \"{Path.GetFullPath(path)}\"");
		}
		else if (Directory.Exists(path))
		{
			Process.Start("explorer.exe", $"\"{Path.GetFullPath(path)}\"");
		}
		else
		{
			_viewModel.ShowAlert($"Error opening '{path}': File does not exist!", AlertType.Danger, 10);
		}
	}

	public void CopyToClipboard(string text)
	{
		try
		{
			Clipboard.SetText(text);
			_viewModel.ShowAlert("Copied text to clipboard.", 0, 10);
		}
		catch (Exception ex)
		{
			_viewModel.ShowAlert($"Error copying text to clipboard: {ex}", AlertType.Danger, 10);
		}
	}

	public void OpenURL(string url)
	{
		if (!String.IsNullOrEmpty(url))
		{
			ProcessHelper.TryOpenUrl(url);
		}
	}

	public void OpenNexusModsPage(DivinityModData mod)
	{
		var url = mod.GetURL(ModSourceType.NEXUSMODS);
		if (!String.IsNullOrEmpty(url))
		{
			ProcessHelper.TryOpenUrl(url);
		}
	}

	public void OpenModSourcePage(DivinityModData mod)
	{
		OpenURL(mod.Metadata.SourcePageUrl);
	}

	public void OpenNexusModsChangelogPage(DivinityModData mod)
	{
		var url = mod.GetURL(ModSourceType.NEXUSMODS);
		if (!String.IsNullOrEmpty(url))
		{
			ProcessHelper.TryOpenUrl($"{url}?tab=logs");
		}
	}

	public void OpenModChangelogPage(DivinityModData mod)
	{
		OpenURL(mod.Metadata.ChangelogPageUrl);
	}

	public void OpenModGalleryPage(DivinityModData mod)
	{
		OpenURL(mod.Metadata.GalleryPageUrl);
	}

	public void OpenRepositoryPage(DivinityModData mod)
	{
		var url = mod.GetURL(ModSourceType.GITHUB);
		if (!String.IsNullOrEmpty(url))
		{
			ProcessHelper.TryOpenUrl(url);
		}
	}

	public void ToggleForceAllowInLoadOrder(DivinityModData mod)
	{
		RxApp.MainThreadScheduler.Schedule(() =>
		{
			mod.ForceAllowInLoadOrder = !mod.ForceAllowInLoadOrder;
			if (mod.ForceAllowInLoadOrder)
			{
				ViewModel.AddActiveMod(mod);
			}
			else
			{
				ViewModel.RemoveActiveMod(mod);
			}
		});
	}

	public void ClearMissingMods()
	{
		_viewModel.ClearMissingMods();
	}

	private void MoveModsBetweenLists(DivinityModData contextMod, bool moveToActive)
	{
		if (contextMod == null || _viewModel == null)
		{
			return;
		}

		var modsToMove = contextMod.IsSelected
			? _viewModel.Mods.Where(mod => mod.IsSelected
				&& mod.CanAddToLoadOrder
				&& mod.IsActive != moveToActive).ToList()
			: new List<DivinityModData> { contextMod };

		modsToMove = modsToMove
			.Where(mod => mod.CanAddToLoadOrder && mod.IsActive != moveToActive)
			.ToList();
		if (modsToMove.Count == 0)
		{
			return;
		}

		_viewModel.MoveModsBetweenLists(modsToMove, moveToActive);

		var targetName = moveToActive ? "active" : "inactive";
		var modLabel = modsToMove.Count == 1 ? "mod" : "mods";
		_viewModel.ShowAlert($"Moved {modsToMove.Count} {modLabel} to the {targetName} mods list.", AlertType.Info, 10);
	}

	public DivinityGlobalCommands()
	{
		var canExecuteViewModelCommands = this.WhenAnyValue(x => x.ViewModel, x => x.ViewModel.IsLocked, (vm, b) => vm != null && !b);

		OpenFileCommand = ReactiveCommand.Create<string>(OpenFile, canExecuteViewModelCommands);
		OpenInFileExplorerCommand = ReactiveCommand.Create<string>(OpenInFileExplorer, canExecuteViewModelCommands);
		ClearMissingModsCommand = ReactiveCommand.Create(ClearMissingMods, canExecuteViewModelCommands);

		ToggleNameDisplayCommand = ReactiveCommand.Create<DivinityModData>((mod) =>
		{
			mod.DisplayFileForName = !mod.DisplayFileForName;
			var b = mod.DisplayFileForName;
			foreach (var m in _viewModel.Mods)
			{
				if (m.IsSelected)
				{
					m.DisplayFileForName = b;
				}
			}
		}, canExecuteViewModelCommands);

		CopyToClipboardCommand = ReactiveCommand.Create<string>(CopyToClipboard, canExecuteViewModelCommands);

		DeleteModCommand = ReactiveCommand.Create<DivinityModData>((mod) =>
		{
			if (mod.CanDelete && _viewModel != null)
			{
				_viewModel.DeleteMod(mod);
			}
		}, canExecuteViewModelCommands);

		MoveModToActiveCommand = ReactiveCommand.Create<DivinityModData>(
			mod => MoveModsBetweenLists(mod, true), canExecuteViewModelCommands);
		MoveModToInactiveCommand = ReactiveCommand.Create<DivinityModData>(
			mod => MoveModsBetweenLists(mod, false), canExecuteViewModelCommands);

		OpenURLCommand = ReactiveCommand.Create<string>(OpenURL, canExecuteViewModelCommands);
		OpenModSourcePageCommand = ReactiveCommand.Create<DivinityModData>(OpenModSourcePage, canExecuteViewModelCommands);
		OpenNexusModsPageCommand = ReactiveCommand.Create<DivinityModData>(OpenNexusModsPage, canExecuteViewModelCommands);
		OpenModGalleryPageCommand = ReactiveCommand.Create<DivinityModData>(OpenModGalleryPage, canExecuteViewModelCommands);
		OpenModChangelogPageCommand = ReactiveCommand.Create<DivinityModData>(OpenModChangelogPage, canExecuteViewModelCommands);
		OpenNexusModsChangelogPageCommand = ReactiveCommand.Create<DivinityModData>(OpenNexusModsChangelogPage, canExecuteViewModelCommands);
		ToggleForceAllowInLoadOrderCommand = ReactiveCommand.Create<DivinityModData>(ToggleForceAllowInLoadOrder, canExecuteViewModelCommands);
	}
}
