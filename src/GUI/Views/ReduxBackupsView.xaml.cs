using DivinityModManager.Localization;
using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public partial class ReduxBackupsView : UserControl
{
	private MainWindowViewModel _main;
	private Window _owner;
	private ReduxSnapshotStore _store;
	private bool _refreshing;
	public ReduxBackupsView()
	{
		InitializeComponent();
		IsVisibleChanged += async (_, _) => { if (IsVisible && _main != null) await RefreshAsync(); };
	}
	public void Init(MainWindowViewModel main, Window owner)
	{
		_main = main;
		_owner = owner;
		IncludeMods.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(main.Settings.SnapshotIncludeMods))
			{ Source = main.Settings, Mode = System.Windows.Data.BindingMode.TwoWay });
		SnapshotName.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Format($"Snapshot {DateTime.Now:yyyy-MM-dd HH:mm}");
		try
		{
			_store = new ReduxSnapshotStore(ReduxLaunchTarget.Current().ApplicationDirectory);
			SnapshotFolderText.Text = _store.DirectoryPath;
		}
		catch (Exception ex) { SnapshotStatus.Text = ex.Message; CreateSnapshotButton.IsEnabled = false; }
	}
	private async Task RefreshAsync()
	{
		if (_store == null || _refreshing) return;
		_refreshing = true;
		try
		{
			var items = await Task.Run(() => _store.List());
			Snapshots.ItemsSource = items;
			CreateSnapshotButton.IsEnabled = !_store.NeedsRecovery;
			RecoverSnapshotButton.Visibility = _store.NeedsRecovery ? Visibility.Visible : Visibility.Collapsed;
			SnapshotStatus.Text = _store.NeedsRecovery ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("An interrupted restore needs recovery before other snapshot operations.")
				: items.Count == 0 ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("No snapshots yet. Create one to save your current version.") : ReduxLanguageService.Current.Format($"{items.Count} saved snapshots.");
		}
		catch (Exception ex) { SnapshotStatus.Text = ReduxLanguageService.Current.Format($"Could not read snapshots: {ex.Message}"); }
		finally { _refreshing = false; Selection_Changed(null, null); }
	}
	private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
	private void Selection_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (RestoreSnapshotButton != null) RestoreSnapshotButton.IsEnabled = _store != null && !_store.NeedsRecovery
			&& Snapshots.SelectedItem is ReduxSnapshotStore.Item { Error.Length: 0 };
	}
	private void OpenFolder_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (_store == null) return;
			Directory.CreateDirectory(_store.DirectoryPath);
			Process.Start(new ProcessStartInfo(_store.DirectoryPath) { UseShellExecute = true });
		}
		catch (Exception ex) { SnapshotStatus.Text = ex.Message; }
	}
	private ReduxSnapshotStore.Scope[] Scopes(bool includeMods)
	{
		var root = _store.ApplicationDirectory;
		var scopes = new List<ReduxSnapshotStore.Scope> { new("Redux", root) };
		var orderPath = _main.Settings.LoadOrderPath;
		var orders = Path.GetFullPath(String.IsNullOrWhiteSpace(orderPath) ? Path.Combine(root, "Orders") : Path.Combine(root, orderPath));
		if (!orders.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			scopes.Add(new("Orders", orders));
		if (includeMods)
		{
			scopes.Add(new("Mods", Path.GetFullPath(_main.PathwayData.AppDataModsPath)));
			scopes.Add(new("Profiles", Path.GetFullPath(_main.PathwayData.AppDataProfilesPath), true));
		}
		return scopes.ToArray();
	}
	private void Create_Click(object sender, RoutedEventArgs e) => Queue("create");
	private void Restore_Click(object sender, RoutedEventArgs e) => Queue("restore");
	private void Recover_Click(object sender, RoutedEventArgs e) => Queue("recover");
	private void Queue(string operation)
	{
		try
		{
			if (_store == null || _main == null) return;
			if (_main.IsLocked || _main.MainProgressIsActive || _main.DownloadManagerInstallIsActive || !_main.IsInitialized)
				throw new IOException(ReduxLanguageService.Current.Translate("Finish active file and load-order operations before using snapshots."));
			if (_main.HasUnsavedLoadOrderChanges) throw new IOException(ReduxLanguageService.Current.Translate("Save or discard your unfinished load-order edits before using snapshots."));
			var include = IncludeMods.IsChecked == true;
			var filename = "";
			var name = SnapshotName.Text.Trim();
			var prompt = ReduxLanguageService.Current.Translate("Redux will save its settings, close, create the snapshot, and restart. Close Baldur's Gate 3 first. Continue?");
			if (operation == "restore")
			{
				if (Snapshots.SelectedItem is not ReduxSnapshotStore.Item { Error.Length: 0 } item) return;
				filename = item.FileName;
				include = item.IncludeMods;
				prompt = ReduxLanguageService.Current.Format($"Restore '{item.Name}' ({item.Version})?\n\nRedux will save a return snapshot, close, replace the saved folders, and restart. ")
					+ ReduxLanguageService.Current.Translate("Newer files in those folders will be removed. You can restore the return snapshot to roll forward.\n\n")
					+ (include ? ReduxLanguageService.Current.Translate("Includes BG3 Mods, held Overrides, and profile load orders. ") : ReduxLanguageService.Current.Translate("BG3 Mods and profile load orders will not be restored. "))
					+ ReduxLanguageService.Current.Translate("Saved games and game-directory mods are excluded. Close Baldur's Gate 3 first.");
			}
			if (operation == "create" && (name.Length is 0 or > 100 || name.Any(Char.IsControl))) throw new IOException(ReduxLanguageService.Current.Translate("Enter a snapshot name of 1–100 characters."));
			var scopes = operation == "recover" ? _store.RecoveryScopes() : Scopes(include);
			if (operation == "recover") prompt = ReduxLanguageService.Current.Translate("Recover the interrupted restore using its saved return snapshot, then restart Redux? Close Baldur's Gate 3 first.");
			prompt += ReduxLanguageService.Current.Translate("\n\nFolders:\n") + String.Join("\n", scopes.Select(s => s.Id + ": " + s.Directory));
			if (ReduxMessageBox.Show(_owner, prompt, "Redux Backups", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
			_main.Settings.SnapshotIncludeMods = IncludeMods.IsChecked == true;
			if (!_main.SaveSettings()) throw new IOException(ReduxLanguageService.Current.Translate("Settings could not be saved. Snapshot operation canceled."));
			var launcher = Services.Get<ReduxUpdateLaunchService>() ?? throw new IOException(ReduxLanguageService.Current.Translate("The external backup runner is unavailable."));
			launcher.QueueSnapshot(new(1, operation, Environment.ProcessId, _store.ApplicationDirectory, scopes, filename, name,
				DivinityApp.REDUX_DISPLAY_VERSION, include, ReduxUpdateResultService.DefaultResultPath), ReduxSnapshotPresentation.Capture(this));
			SnapshotStatus.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Waiting for Redux to close safely…");
			MainWindow.Self.RequestExitForUpdate();
		}
		catch (Exception ex) { SnapshotStatus.Text = ex.Message; }
	}
}
