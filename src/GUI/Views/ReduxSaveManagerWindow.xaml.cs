using DivinityModManager.AppServices;
using DivinityModManager.Converters;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using Microsoft.VisualBasic.FileIO;

using Ookii.Dialogs.Wpf;

using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DivinityModManager.Views;

public sealed record SavePartyDisplayItem(Bg3SavePartyMember Member)
{
    public string DisplayName => Member.DisplayName;
    public string Details => Member.Details;
    public string Subregion => Member.Subregion;
    private string CompanionKey => Member.OriginKey is "gale" or "astarion" or "shadowheart" or "laezel" or "karlach" or "wyll" or "halsin" or "minthara" or "jaheira" or "minsc" ? Member.OriginKey : null;
    private string RaceKey => new string((Member.Race ?? "").Where(Char.IsLetterOrDigit).ToArray()).ToLowerInvariant() switch
    {
        "human" => "human",
        "elf" or "highelf" or "woodelf" => "elf",
        "halfelf" or "highhalfelf" or "woodhalfelf" or "drowhalfelf" or "halfhighelf" or "halfwoodelf" or "halfdrow" => "halfelf",
        "drow" or "seldarinedrow" or "lolthsworndrow" or "lolthdrow" => "drow",
        "dwarf" or "mountaindwarf" or "shielddwarf" or "hilldwarf" or "golddwarf" => "dwarf",
        "duergar" => "duergar",
        "gnome" or "rockgnome" or "forestgnome" or "deepgnome" => "gnome",
        "halfling" or "lightfoothalfling" or "stronghearthalfling" => "halfling",
        "halforc" => "halforc",
        "githyanki" => "githyanki",
        "tiefling" or "asmodeustiefling" or "mephistophelestiefling" or "zarieltiefling" => "tiefling",
        "dragonborn" or "whitedragonborn" or "blackdragonborn" or "bluedragonborn" or "brassdragonborn" or "bronzedragonborn" or "copperdragonborn" or "golddragonborn" or "greendragonborn" or "reddragonborn" or "silverdragonborn" => "dragonborn",
        _ => null
    };
    public string PortraitPath => CompanionKey is string companion
        ? $"pack://application:,,,/Redux;component/Resources/Icons/Companions/{companion}.png"
        : RaceKey is string race ? $"pack://application:,,,/Redux;component/Resources/Icons/Companions/race-{race}.png" : null;
    public string PortraitToolTip => CompanionKey != null ? DisplayName : RaceKey != null
        ? $"Representative {RaceKey} portrait · not the character’s saved appearance" : "No portrait available";

}

public sealed class ReduxSaveGameItem(Bg3SaveGameEntry save) : System.ComponentModel.INotifyPropertyChanged
{
    public Bg3SaveGameEntry Save { get; } = save;
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string ModWarning { get; private set; } = "";
    public bool HasModWarning => !String.IsNullOrEmpty(ModWarning);
    public bool CanReviewMods { get; private set; }
    public string ModSummary { get; private set; } = "Checking recorded mods…";
    public void SetModCheck(string warning, bool canReview, string summary = null)
    {
        ModWarning = warning ?? "";
        CanReviewMods = canReview;
        ModSummary = summary ?? (HasModWarning ? ModWarning : canReview ? "Recorded mods available" : "No recorded mods");
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(null));
    }
	public string FolderPath => Save.FolderPath;
	public string FolderName => Save.FolderName;
	public string DisplayName => Save.DisplayName;
	public string CampaignName => String.IsNullOrWhiteSpace(Save.CampaignName) ? "Story save" : Save.CampaignName;
	public string CampaignGroupName => GetCampaignDisplayName(Save.CampaignName);
	public string ThumbnailPath => Save.ThumbnailPath;
    public string LocationText => Save.Location == "WLD_Main_A" ? "Wilderness (Act 1)" : String.IsNullOrWhiteSpace(Save.Location) ? "Location unavailable" : Save.Location;
    public string SaveFacts => String.Join(" · ", new[] { SaveTypeLabel, DifficultyLabel, SizeText }.Where(v => !String.IsNullOrWhiteSpace(v)));
    public string SavedDateText => "Saved " + ModifiedText;
    public string GameVersionText => String.IsNullOrWhiteSpace(Save.GameVersion) ? "Game version unavailable" : "Game " + Save.GameVersion;
    public IReadOnlyList<SavePartyDisplayItem> Party => Save.Party.Select(member => new SavePartyDisplayItem(member)).ToArray();
    public string PartyHeading => Party.Count == 0 ? "Party information unavailable" : $"Party · {Party.Count}";
    public string DetailsText => String.IsNullOrWhiteSpace(Save.GameVersion) ? Save.FolderName : $"Game version {Save.GameVersion}\n{Save.FolderName}";
	public Bg3SaveDifficulty Difficulty => Save.Difficulty;
	public bool HasDifficulty => Difficulty != Bg3SaveDifficulty.Unknown;
	public bool IsHonourMode => Difficulty == Bg3SaveDifficulty.Honour;
	public bool IsTacticianMode => Difficulty == Bg3SaveDifficulty.Tactician;
	public string DifficultyLabel => Difficulty switch
	{
		Bg3SaveDifficulty.Explorer => "Explorer",
		Bg3SaveDifficulty.Balanced => "Balanced",
		Bg3SaveDifficulty.Tactician => "Tactician",
		Bg3SaveDifficulty.Honour => "Honour",
		Bg3SaveDifficulty.Custom => "Custom",
		_ => String.Empty
	};
    public string SaveTypeLabel => Bg3SaveGameService.ClassifySaveKind(Save.SaveFilePath);
    public string MetadataText => String.Join(" · ", new[] { SaveTypeLabel, DifficultyLabel, ModifiedText, SizeText }.Where(value => !String.IsNullOrWhiteSpace(value)));
	public string ModifiedText => Save.ModifiedUtc.ToLocalTime().ToString("g");
	public string SizeText => FormatSize(Save.SizeBytes);

	private static string FormatSize(long size) => size >= 1024L * 1024L
		? $"{size / (1024d * 1024d):0.0} MB"
		: $"{Math.Max(1, size / 1024d):0} KB";

	private static string GetCampaignDisplayName(string campaignName)
	{
		if (String.IsNullOrWhiteSpace(campaignName)) return "Other saves";
		var suffix = campaignName.LastIndexOf('-');
		var displayName = suffix > 0 && campaignName[(suffix + 1)..].All(Char.IsDigit)
			? campaignName[..suffix]
			: campaignName;
		return displayName.Replace('_', ' ');
	}
}

public partial class ReduxSaveManagerWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindowViewModel _viewModel;
	private readonly string _storyFolder;
	private bool _isImporting;
	private bool _isReadingSave;
	private readonly Dictionary<FrameworkElement, int> _campaignAnimationVersions = new();
	private readonly HashSet<Expander> _restoringCampaignExpanders = new();
	private readonly HashSet<Expander> _campaignExpanders = new();
	private string[] _campaignKeys = [];
	private bool _isBulkCampaignUpdate;
	public int ImportedSaveCount { get; private set; }

	public ReduxSaveManagerWindow(Window owner, MainWindowViewModel viewModel, IEnumerable<string> pendingImportPaths = null)
	{
		InitializeComponent();
		SaveList.ContextMenuOpening += SaveList_ContextMenuOpening;
        SaveReviewMenu.SetBinding(IsEnabledProperty, new Binding(nameof(Button.IsEnabled)) { Source = ReviewModsButton });
        SaveReviewMenu.SetBinding(ToolTipProperty, new Binding(nameof(Button.ToolTip)) { Source = ReviewModsButton });
		ReduxExternalDropFeedback.Attach(this, paths => !_isImporting && !_isReadingSave && paths.All(Bg3SaveGameService.IsSupportedSaveInput), "Drop to install saves", "Redux.Icon.Save", "Save files, folders, or archives.");
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true) Owner = owner;
		_viewModel = viewModel;
		_storyFolder = viewModel?.SelectedProfile?.Folder == null
			? null
			: Path.Combine(viewModel.SelectedProfile.Folder, "Savegames", "Story");
		if (viewModel?.Settings != null)
			ReduxThemeService.Apply(Resources, viewModel.Settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(viewModel.Settings), viewModel.Settings.UsesGeneratedGradients);
		ProfilePathText.Text = _storyFolder ?? "No player profile is selected.";
		RefreshSaves();
        Loaded += (_, _) => StartSaveChecks();
        Closed += (_, _) => _saveCheckVersion++;
		var pendingPaths = (pendingImportPaths ?? []).Where(path => !String.IsNullOrWhiteSpace(path)).ToArray();
		if (pendingPaths.Length > 0)
			Loaded += async (_, _) =>
			{
				foreach (var path in pendingPaths) await ImportPathAsync(path);
			};
	}

	private void RefreshSaves()
	{
		var selectedPath = (SaveList.SelectedItem as ReduxSaveGameItem)?.FolderPath;
		var saves = Bg3SaveGameService.Discover(_storyFolder).Select(save => new ReduxSaveGameItem(save)).ToArray();
		var groupedSaves = new ListCollectionView(saves);
		groupedSaves.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ReduxSaveGameItem.CampaignGroupName)));
		_campaignKeys = saves.Select(save => save.CampaignGroupName)
			.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		SaveList.ItemsSource = groupedSaves;
		if (!String.IsNullOrWhiteSpace(selectedPath))
			SaveList.SelectedItem = saves.FirstOrDefault(save => save.FolderPath.Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
		EmptyState.Visibility = saves.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		DeleteButton.IsEnabled = SaveList.SelectedItem != null && !_isReadingSave;
		UpdateReviewButton();
		UpdateCampaignBulkToggleButton();
        if (IsLoaded) StartSaveChecks();
	}

    private void SaveList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Let WPF open the list-owned menu for mouse, Menu key, and Shift+F10.
        // Recycled campaign rows no longer own or manually open popups.
        e.Handled = !PrepareSaveContextMenu(e.OriginalSource as DependencyObject, e.CursorLeft < 0 && e.CursorTop < 0);
    }

    public bool PrepareSaveContextMenu(DependencyObject source, bool keyboardInvocation)
    {
        if (_isReadingSave || _isImporting) return false;
        while (source != null && source is not ListBoxItem && source != SaveList)
            source = source is Visual || source is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source) : (source as FrameworkContentElement)?.Parent;
        var save = (source as ListBoxItem)?.DataContext as ReduxSaveGameItem;
        if (save == null && keyboardInvocation) save = SaveList.SelectedItem as ReduxSaveGameItem;
        if (save == null || !SaveList.Items.Contains(save)) return false;
        SaveList.SelectedItem = save;
        UpdateReviewButton();
        SaveReviewMenu.SetBinding(ReduxMenuItemExtension.UseSemanticHoverProperty,
            new Binding(nameof(ReduxSaveGameItem.HasModWarning)) { Source = save });
        SaveReviewMenu.SetResourceReference(ReduxMenuItemExtension.SemanticHoverBrushProperty, "ReduxWarningPillBackground");
        SaveReviewMenu.SetResourceReference(ReduxMenuItemExtension.SemanticRailBrushProperty, "ReduxWarningBrush");
        return true;
    }

    private void ExportThisSave_Click(object sender, RoutedEventArgs e)
    {
        if (SaveList.SelectedItem is ReduxSaveGameItem selected) ExportSaves(new[] { selected.Save });
    }

    private void ShowSelectedSaveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (SaveList.SelectedItem is not ReduxSaveGameItem selected) return;
        try { ProcessHelper.TryOpenPath(selected.FolderPath, Directory.Exists); }
        catch (Exception ex) { ShowMessage(ex.Message, "Could Not Open Save Folder", MessageBoxImage.Error); }
    }

    private void ExportCampaignSaves_Click(object sender, RoutedEventArgs e)
    {
        if (SaveList.SelectedItem is not ReduxSaveGameItem selected) return;
        try { ExportSaves(Bg3SaveGameService.Discover(_storyFolder).Where(save => save.CampaignName == selected.Save.CampaignName).ToArray()); }
        catch (Exception ex) { DivinityApp.Log(ex.ToString()); ShowMessage(ex.Message, "Could Not Export Saves", MessageBoxImage.Error); }
    }

    private void ExportSaves(IReadOnlyList<Bg3SaveGameEntry> saves)
    {
        if (_isImporting || _isReadingSave || saves.Count == 0) return;
        var chooser = new Microsoft.Win32.SaveFileDialog { Title = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Export Saves"), Filter = "ZIP archive (*.zip)|*.zip", DefaultExt = ".zip", FileName = "Redux Saves", OverwritePrompt = true };
        if (chooser.ShowDialog(this) != true) return;
        _isReadingSave = true;
        try
        {
            var progressWindow = new ReduxInstallProgressWindow(this, exportingSaves: true);
            var progress = new Progress<SaveExportProgress>(value =>
            {
                if (progressWindow.IsVisible)
                    _ = progressWindow.ReportAsync("Exporting", value.Name, Math.Min(value.Completed + 1, value.Total), value.Total);
            });
            progressWindow.Run(() => Task.Run(() => SaveArchiveExportService.ExportAsync(saves, chooser.FileName, progress, progressWindow.CancellationToken)));
            _viewModel?.ShowAlert(DivinityModManager.Localization.ReduxLanguageService.Current.Format($"Exported {saves.Count} {(saves.Count == 1 ? "save" : "saves")}."), AlertType.Success, 8);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { DivinityApp.Log(ex.ToString()); ShowMessage(ex.Message, "Could Not Export Saves", MessageBoxImage.Error); }
        finally { _isReadingSave = false; }
    }

    private void InstallSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isImporting || _isReadingSave || sender is not Button button) return;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }

	private async void ImportArchiveButton_Click(object sender, RoutedEventArgs e)
	{
		var selectedPath = ChooseSaveFile(this);
		if (!String.IsNullOrWhiteSpace(selectedPath)) await ImportPathAsync(selectedPath);
	}

	private async void ImportFolderButton_Click(object sender, RoutedEventArgs e)
	{
		var selectedPath = ChooseSaveFolder(this);
		if (!String.IsNullOrWhiteSpace(selectedPath)) await ImportPathAsync(selectedPath);
	}

	public static string ChooseSaveFile(Window owner)
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install BG3 Save"),
			Filter = "BG3 saves and archives (*.lsv;*.zip;*.7z;*.rar;*.tar;*.gz)|*.lsv;*.zip;*.7z;*.7zip;*.rar;*.tar;*.tar.gz;*.tgz;*.gz;*.gzip|BG3 saves (*.lsv)|*.lsv|Archives (*.zip;*.7z;*.rar;*.tar;*.gz)|*.zip;*.7z;*.7zip;*.rar;*.tar;*.tar.gz;*.tgz;*.gz;*.gzip",
			CheckFileExists = true,
			Multiselect = false
		};
		return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
	}

	public static string ChooseSaveFolder(Window owner)
	{
		var dialog = new VistaFolderBrowserDialog
		{
			Description = "Choose the folder containing the BG3 .lsv save.",
			UseDescriptionForTitle = true
		};
		return dialog.ShowDialog(owner) == true ? dialog.SelectedPath : null;
	}

	private async Task ImportPathAsync(string sourcePath)
	{
		if (_isImporting || _isReadingSave) return;
		if (String.IsNullOrWhiteSpace(_storyFolder))
		{
			ShowMessage("Select a BG3 player profile before installing saves.", "Save Games", MessageBoxImage.Warning);
			return;
		}
		try
		{
			var names = Bg3SaveGameService.GetImportFolderNames(sourcePath);
			if (names.Count == 0) throw new InvalidDataException("No BG3 .lsv save was found.");
			var collisions = names.Where(name => Directory.Exists(Path.Combine(_storyFolder, name))).ToArray();
			var replace = false;
			if (collisions.Length > 0)
			{
				var result = ReduxMessageBox.Show(this,
					DivinityModManager.Localization.ReduxLanguageService.Current.Format($"{collisions.Length} save{(collisions.Length == 1 ? "" : "s")} already exist. Replace the existing save folders?"),
					"Replace Existing Saves?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
				if (result != MessageBoxResult.Yes) return;
				replace = true;
			}
			_isImporting = true;
			Cursor = Cursors.Wait;
			SaveList.IsEnabled = false;
			DeleteButton.IsEnabled = false;
			var imported = await Task.Run(() => Bg3SaveGameService.Import(sourcePath, _storyFolder, replace));
			ImportedSaveCount += imported.Count;
			RefreshSaves();
			_viewModel?.ShowAlert(DivinityModManager.Localization.ReduxLanguageService.Current.Format($"Installed {imported.Count} save{(imported.Count == 1 ? "" : "s")}."), AlertType.Success, 12);
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
			or InvalidOperationException or SharpCompress.Common.InvalidFormatException)
		{
			ShowMessage(ex.Message, "Install Save", MessageBoxImage.Error);
		}
		finally
		{
			_isImporting = false;
			Cursor = null;
			SaveList.IsEnabled = true;
			DeleteButton.IsEnabled = SaveList.SelectedItem != null && !_isReadingSave;
		UpdateReviewButton();
		}
	}

	private async void ReviewModsButton_Click(object sender, RoutedEventArgs e)
	{
		if (!ReviewModsButton.IsEnabled || _isImporting || _isReadingSave || _viewModel == null || SaveList.SelectedItems.Count != 1
			|| SaveList.SelectedItem is not ReduxSaveGameItem selected) return;
		var profile = _viewModel.SelectedProfile;
		var order = _viewModel.SelectedModOrder;
		var savePath = selected.Save.SaveFilePath;
		try
		{
			_isReadingSave = true;
			ReviewModsLabel.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Reading save…");
			ReviewModsButton.IsEnabled = false;
			DeleteButton.IsEnabled = false;
			SaveList.IsEnabled = false;
			var before = new FileInfo(savePath);
			var stamp = (before.Length, before.LastWriteTimeUtc);
			var recorded = await Task.Run(() => DivinityModDataLoader.GetLoadOrderFromSave(savePath, includeEmpty: true));
			if (!IsLoaded) return;
			if (recorded == null) throw new InvalidDataException("Redux could not read the mod list from this save. The save may be incomplete or unsupported.");
			bool SaveUnchanged()
			{
				var now = new FileInfo(savePath);
				return now.Exists && stamp == (now.Length, now.LastWriteTimeUtc);
			}
			if (!SaveUnchanged()) throw new InvalidOperationException("The save changed while it was being read. Refresh and review it again.");
			var review = _viewModel.ReviewSaveMods(recorded.Order);
			var dialog = new ReduxSaveModReviewWindow(this, selected.DisplayName, review, _viewModel.Settings);
			if (dialog.ShowDialog() != true) return;
			if (!ReferenceEquals(profile, _viewModel.SelectedProfile) || !ReferenceEquals(order, _viewModel.SelectedModOrder) || !SaveUnchanged())
				throw new InvalidOperationException("The save or selected workspace changed. Review the save again before applying.");
			var count = _viewModel.ActivateReviewedSaveMods(recorded.Order, review, dialog.SelectedIds);
			_viewModel.ShowAlert(DivinityModManager.Localization.ReduxLanguageService.Current.Format($"Activated {count} recorded mod{(count == 1 ? "" : "s")}. Review your order, then save and sync when ready."), AlertType.Success, 12);
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Save mod review failed: {ex}");
			if (IsLoaded) ShowMessage(ex.Message, "Save Mod Review", MessageBoxImage.Error);
		}
		finally
		{
			_isReadingSave = false;
			ReviewModsLabel.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Review Mods...");
			SaveList.IsEnabled = true;
			DeleteButton.IsEnabled = SaveList.SelectedItem != null;
			UpdateReviewButton();
            StartSaveChecks();
            UpdateReviewButton();
		}
	}

	private void DeleteButton_Click(object sender, RoutedEventArgs e)
	{
		if (_isImporting || _isReadingSave) return;
		if (SaveList.SelectedItem is not ReduxSaveGameItem selected) return;
		var result = ReduxMessageBox.Show(this,
			DivinityModManager.Localization.ReduxLanguageService.Current.Format($"Move '{selected.DisplayName}' to the Recycle Bin?"),
			"Delete Save?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
		if (result != MessageBoxResult.Yes) return;
		try
		{
			// The preview converter closes its stream immediately. Evict the decoded
			// image as well so a later import using this folder name cannot reuse it.
			FilePathToBitmapImageConverter.EvictTree(selected.FolderPath);
			FileSystem.DeleteDirectory(selected.FolderPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
			RefreshSaves();
		}
		catch (IOException ex) when (IsSharingViolation(ex))
		{
			ShowMessage(
				"Another program is using this save. Close Baldur's Gate 3 and any save or cloud-sync tools, then try again.",
				"Delete Save", MessageBoxImage.Error);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
		{
			ShowMessage(ex.Message, "Delete Save", MessageBoxImage.Error);
		}
	}

	private static bool IsSharingViolation(IOException exception)
	{
		var errorCode = exception.HResult & 0xFFFF;
		return errorCode is 32 or 33;
	}

	private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
	{
		if (String.IsNullOrWhiteSpace(_storyFolder)) return;

		try
		{
			Directory.CreateDirectory(_storyFolder);
			ProcessHelper.TryOpenPath(_storyFolder, Directory.Exists);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			ShowMessage(ex.Message, "Open Save Folder", MessageBoxImage.Error);
		}
	}

	private void RefreshButton_Click(object sender, RoutedEventArgs e) { if (!_isReadingSave && !_isImporting) RefreshSaves(); }
    private int _saveCheckVersion;
    private async void StartSaveChecks()
    {
        var version = ++_saveCheckVersion;
        if (_viewModel == null) return;
        var items = SaveList.Items.OfType<ReduxSaveGameItem>().ToArray();
        foreach (var item in items)
        {
            if (version != _saveCheckVersion || !IsLoaded) return;
            string warning = "";
            string summary = null;
            bool canReview = false;
            try
            {
                var path = item.Save.SaveFilePath;
                var before = new FileInfo(path);
                var stamp = (before.Length, before.LastWriteTimeUtc);
                var recorded = await Task.Run(() => DivinityModDataLoader.GetLoadOrderFromSave(path, includeEmpty: true));
                if (version != _saveCheckVersion || !IsLoaded) return;
                var after = new FileInfo(path);
                if (!after.Exists || stamp != (after.Length, after.LastWriteTimeUtc)) warning = "Save changed · Refresh to check mods";
                else if (recorded == null) warning = "Mod list unavailable";
                else
                {
                    canReview = recorded.Order.Count > 0;
                    var review = _viewModel.ReviewSaveMods(recorded.Order);
                    var missing = review.Count(r => r.Status == SaveModStatus.Missing);
                    var inactive = review.Count(r => r.Status == SaveModStatus.Inactive);
                    var unresolved = review.Count(r => r.Status == SaveModStatus.Unavailable);
                    summary = $"{review.Count} recorded · {missing} missing · {inactive} inactive";
                    if (unresolved > 0) summary += $" · {unresolved} need attention";
                    var parts = new List<string>();
                    if (missing > 0) parts.Add($"{missing} missing mod{(missing == 1 ? "" : "s")}");
                    if (inactive > 0) parts.Add($"{inactive} inactive");
                    if (unresolved > 0) parts.Add($"{unresolved} need attention");
                    warning = String.Join(" · ", parts);
                }
            }
            catch (Exception ex) { DivinityApp.Log($"Save mod check failed: {ex}"); warning = "Could not check mods"; }
            if (version != _saveCheckVersion || !IsLoaded) return;
            item.SetModCheck(warning, canReview, summary);
            UpdateReviewButton();
        }
    }

    private void UpdateReviewButton()
    {
        var selected = SaveList.SelectedItems.Count == 1 ? SaveList.SelectedItem as ReduxSaveGameItem : null;
        SaveDetailsPanel.DataContext = selected;
        SaveDetailsContent.Visibility = selected == null ? Visibility.Collapsed : Visibility.Visible;
        SaveDetailsPlaceholder.Visibility = selected == null ? Visibility.Visible : Visibility.Collapsed;
        var enabled = selected != null && (selected.CanReviewMods || selected.HasModWarning) && !_isReadingSave && !_isImporting;
        var warning = selected?.HasModWarning == true;
        ReduxActionButtonTransition.Apply(ReviewModsButton, enabled,
            warning ? "ReduxWarningPillBackground" : "ReduxSurfaceElevatedBrush",
            warning ? "ReduxWarningBrush" : "ReduxBorderStrongBrush",
            warning ? "ReduxWarningBrush" : enabled ? "ReduxTextPrimaryBrush" : "ReduxTextMutedBrush",
            _viewModel?.Settings?.ReduceMotion == true);
        ReviewModsButton.ToolTip = selected == null ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Select a save to review its mods.")
            : selected.HasModWarning ? selected.ModWarning
            : selected.CanReviewMods ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Review this save’s recorded mods.") : DivinityModManager.Localization.ReduxLanguageService.Current.Translate("No recorded mods to review.");
        SaveModWarning.Visibility = Visibility.Collapsed;

    }

    private void CampaignHeader_Click(object sender, RoutedEventArgs e)
    {
        if (_isReadingSave || _isImporting) return;
        if (sender is FrameworkElement { DataContext: CollectionViewGroup group })
        {
            var saves = group.Items.OfType<ReduxSaveGameItem>().ToArray();
            if (SaveList.SelectedItem is not ReduxSaveGameItem selected || !saves.Contains(selected))
                SaveList.SelectedItem = saves.FirstOrDefault();
            UpdateReviewButton();
        }
    }

    private void SaveList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DeleteButton.IsEnabled = SaveList.SelectedItem != null && !_isReadingSave;
        UpdateReviewButton();
    }

	private void CampaignExpander_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is not Expander expander) return;
		_campaignExpanders.Add(expander);
        expander.ApplyTemplate();
        if (expander.Template.FindName("HeaderButton", expander) is System.Windows.Controls.Primitives.ToggleButton header)
        {
            header.Click -= CampaignHeader_Click;
            header.Click += CampaignHeader_Click;
        }
		var key = GetCampaignKey(expander);
		var collapsed = !String.IsNullOrWhiteSpace(key)
			&& (_viewModel?.Settings?.CollapsedSaveGameCampaigns?.Contains(key, StringComparer.OrdinalIgnoreCase) ?? false);
		_restoringCampaignExpanders.Add(expander);
		try { expander.IsExpanded = !collapsed; }
		finally { _restoringCampaignExpanders.Remove(expander); }
		if (expander.Template.FindName("ExpandSite", expander) is FrameworkElement content)
		{
			content.BeginAnimation(HeightProperty, null);
			content.BeginAnimation(OpacityProperty, null);
			var translate = EnsureWritableCampaignTransform(content);
			translate.BeginAnimation(TranslateTransform.YProperty, null);
			translate.Y = 0;
			content.Height = Double.NaN;
			content.Opacity = 1;
			content.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
		}
		UpdateCampaignBulkToggleButton();
	}

	private void CampaignExpander_Unloaded(object sender, RoutedEventArgs e)
	{
		if (sender is not Expander expander) return;
		_campaignExpanders.Remove(expander);
		if (expander.Template.FindName("ExpandSite", expander) is FrameworkElement content)
			_campaignAnimationVersions.Remove(content);
	}

	private async void CampaignExpander_Expanded(object sender, RoutedEventArgs e)
	{
		if (sender is not Expander expander || expander.Template.FindName("ExpandSite", expander) is not FrameworkElement content) return;
		RememberCampaignState(expander, collapsed: false);
		if (_restoringCampaignExpanders.Contains(expander))
		{
			content.Visibility = Visibility.Visible;
			return;
		}
		var version = NextCampaignAnimationVersion(content);
		content.BeginAnimation(HeightProperty, null);
		content.BeginAnimation(OpacityProperty, null);
		var translate = EnsureWritableCampaignTransform(content);
		translate.BeginAnimation(TranslateTransform.YProperty, null);
		translate.Y = 0;
		content.Visibility = Visibility.Visible;
		content.Opacity = 1;
		content.Height = Double.NaN;
		if (ShouldReduceCampaignMotion())
		{
			UpdateCampaignBulkToggleButton();
			return;
		}

		await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
		if (!expander.IsExpanded || !IsCurrentCampaignAnimation(content, version)) return;
		content.Measure(new Size(Math.Max(1, expander.ActualWidth), Double.PositiveInfinity));
		var targetHeight = Math.Max(content.ActualHeight, content.DesiredSize.Height);
		if (targetHeight <= 0) return;
		content.Height = 0;
		content.Opacity = 0;
		translate.Y = -5;
		var duration = TimeSpan.FromMilliseconds(165);
		var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
		var height = new DoubleAnimation(targetHeight, duration) { EasingFunction = easing };
		height.Completed += (_, _) =>
		{
			if (!expander.IsExpanded || !IsCurrentCampaignAnimation(content, version)) return;
			content.BeginAnimation(HeightProperty, null);
			content.Height = Double.NaN;
		};
		content.BeginAnimation(HeightProperty, height);
		content.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(125)));
		translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, duration) { EasingFunction = easing });
		UpdateCampaignBulkToggleButton();
	}

	private void CampaignExpander_Collapsed(object sender, RoutedEventArgs e)
	{
		if (sender is not Expander expander || expander.Template.FindName("ExpandSite", expander) is not FrameworkElement content) return;
		RememberCampaignState(expander, collapsed: true);
		if (_restoringCampaignExpanders.Contains(expander))
		{
			content.Visibility = Visibility.Collapsed;
			return;
		}
		var version = NextCampaignAnimationVersion(content);
		if (ShouldReduceCampaignMotion())
		{
			var translate = EnsureWritableCampaignTransform(content);
			translate.BeginAnimation(TranslateTransform.YProperty, null);
			translate.Y = 0;
			content.Visibility = Visibility.Collapsed;
			content.Height = Double.NaN;
			content.Opacity = 1;
			UpdateCampaignBulkToggleButton();
			return;
		}

		content.BeginAnimation(HeightProperty, null);
		content.Height = Math.Max(0, content.ActualHeight);
		var duration = TimeSpan.FromMilliseconds(145);
		var easing = new QuadraticEase { EasingMode = EasingMode.EaseIn };
		var height = new DoubleAnimation(0, duration) { EasingFunction = easing };
		height.Completed += (_, _) =>
		{
			if (expander.IsExpanded || !IsCurrentCampaignAnimation(content, version)) return;
			content.Visibility = Visibility.Collapsed;
			content.BeginAnimation(HeightProperty, null);
			content.BeginAnimation(OpacityProperty, null);
			content.Height = Double.NaN;
			content.Opacity = 1;
		};
		content.BeginAnimation(HeightProperty, height);
		content.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(110)));
		var translateTransform = EnsureWritableCampaignTransform(content);
		translateTransform.BeginAnimation(TranslateTransform.YProperty,
			new DoubleAnimation(-4, duration) { EasingFunction = easing });
		UpdateCampaignBulkToggleButton();
	}

	private static TranslateTransform EnsureWritableCampaignTransform(FrameworkElement content)
	{
		if (content.RenderTransform is TranslateTransform { IsFrozen: false } writable) return writable;
		var replacement = content.RenderTransform is TranslateTransform transform
			? transform.CloneCurrentValue()
			: new TranslateTransform();
		content.RenderTransform = replacement;
		return replacement;
	}

	private bool ShouldReduceCampaignMotion() =>
		_viewModel?.Settings?.ReduceMotion == true
		|| ReduxWindowBehavior.ReduceMotion
		|| !SystemParameters.ClientAreaAnimation;

	private void CampaignBulkToggleButton_MouseEnter(object sender, MouseEventArgs e) =>
		UpdateCampaignBulkToggleButton();

	private void CampaignBulkToggleButton_Click(object sender, RoutedEventArgs e)
	{
		if (_campaignKeys.Length < 2 || _viewModel?.Settings == null) return;
		var collapse = !AreAllCampaignsCollapsed();
		var saved = _viewModel.Settings.CollapsedSaveGameCampaigns ??= [];
		if (collapse)
		{
			foreach (var key in _campaignKeys)
				if (!saved.Contains(key, StringComparer.OrdinalIgnoreCase)) saved.Add(key);
		}
		else
		{
			saved.RemoveAll(savedKey => _campaignKeys.Contains(savedKey, StringComparer.OrdinalIgnoreCase));
		}

		_isBulkCampaignUpdate = true;
		try
		{
			foreach (var expander in _campaignExpanders.Where(expander => expander.IsLoaded).ToArray())
				expander.IsExpanded = !collapse;
		}
		finally
		{
			_isBulkCampaignUpdate = false;
		}
		_viewModel.SaveSettings();
		UpdateCampaignBulkToggleButton();
	}

	private bool AreAllCampaignsCollapsed()
	{
		var saved = _viewModel?.Settings?.CollapsedSaveGameCampaigns;
		return _campaignKeys.Length > 0 && saved != null
			&& _campaignKeys.All(key => saved.Contains(key, StringComparer.OrdinalIgnoreCase));
	}

	private void UpdateCampaignBulkToggleButton()
	{
		if (CampaignBulkToggleButton == null) return;
		var available = _campaignKeys.Length >= 2;
		CampaignBulkToggleButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
		CampaignBulkToggleButton.IsEnabled = available;
		if (!available) return;
		var expand = AreAllCampaignsCollapsed();
		var label = expand ? "Expand all campaigns" : "Collapse all campaigns";
		CampaignBulkToggleButton.ToolTip = label;
		AutomationProperties.SetName(CampaignBulkToggleButton, label);
		CampaignBulkToggleIcon.StrokeData = FindResource(expand
			? "Redux.Icon.ChevronDownStroke" : "Redux.Icon.ChevronUpStroke") as Geometry;
	}

	private int NextCampaignAnimationVersion(FrameworkElement content)
	{
		var version = _campaignAnimationVersions.TryGetValue(content, out var current) ? current + 1 : 1;
		_campaignAnimationVersions[content] = version;
		return version;
	}

	private bool IsCurrentCampaignAnimation(FrameworkElement content, int version) =>
		_campaignAnimationVersions.TryGetValue(content, out var current) && current == version;

	private static string GetCampaignKey(Expander expander) =>
		(expander?.DataContext as CollectionViewGroup)?.Name?.ToString()?.Trim() ?? String.Empty;

	private void RememberCampaignState(Expander expander, bool collapsed)
	{
		if (_isBulkCampaignUpdate || _restoringCampaignExpanders.Contains(expander) || _viewModel?.Settings == null) return;
		var key = GetCampaignKey(expander);
		if (String.IsNullOrWhiteSpace(key)) return;
		var saved = _viewModel.Settings.CollapsedSaveGameCampaigns ??= [];
		var existing = saved.FindIndex(name => name.Equals(key, StringComparison.OrdinalIgnoreCase));
		if (collapsed && existing < 0) saved.Add(key);
		else if (!collapsed && existing >= 0) saved.RemoveAt(existing);
		else return;
		_viewModel.SaveSettings();
		UpdateCampaignBulkToggleButton();
	}

	private void Window_DragOver(object sender, DragEventArgs e)
	{
		e.Effects = TryGetDroppedPaths(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private async void Window_Drop(object sender, DragEventArgs e)
	{
		if (_isImporting || _isReadingSave || ReduxWindowBehavior.HasActiveChild(this)) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
		if (TryGetDroppedPaths(e.Data, out var paths))
		{
			try
			{
				await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
		var names = paths.SelectMany(Bg3SaveGameService.GetImportFolderNames).ToArray();
				if (names.Length > 0 && ConfirmDroppedSaveInstall(this, names, _storyFolder))
					foreach (var path in paths) await ImportPathAsync(path);
			}
			catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
				or InvalidOperationException or SharpCompress.Common.InvalidFormatException)
			{
				ShowMessage(ex.Message, "Install Save", MessageBoxImage.Error);
			}
		}
		e.Handled = true;
	}

	private static bool TryGetDroppedPaths(IDataObject data, out string[] paths)
	{
		paths = [];
		if (!data.GetDataPresent(DataFormats.FileDrop) || data.GetData(DataFormats.FileDrop) is not string[] droppedPaths
			|| droppedPaths.Length == 0 || droppedPaths.Any(path => !Bg3SaveGameService.IsSupportedSaveInput(path))) return false;
		paths = droppedPaths;
		return true;
	}

	public static bool ConfirmDroppedSaveInstall(Window owner, IReadOnlyList<string> saveFolderNames, string storyFolder = null)
	{
		var names = (saveFolderNames ?? []).Where(name => !String.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		if (names.Length == 0) return false;
		var items = names.Select(name =>
		{
			var split = name.IndexOf("__", StringComparison.Ordinal);
			var campaign = split > 0 ? name[..split] : "Story save";
			var saveName = split >= 0 && split + 2 < name.Length ? name[(split + 2)..] : name;
			var exists = !String.IsNullOrWhiteSpace(storyFolder) && Directory.Exists(Path.Combine(storyFolder, name));
			return new ReduxInstallReviewItem(
				saveName.Replace('_', ' '),
				$"Campaign: {campaign.Replace('_', ' ')}",
				exists ? "Already installed" : "New save",
				exists ? ReduxInstallReviewTone.Warning : ReduxInstallReviewTone.Info);
		}).ToArray();
		var existingCount = items.Count(item => item.Tone == ReduxInstallReviewTone.Warning);
		var detail = existingCount == 0
			? "All saves are new to the selected profile"
			: $"{items.Length - existingCount} new · {existingCount} already installed";
		var dialog = new ReduxInstallReviewWindow(owner, items, true, "Save Game Manager", detail);
		return dialog.ShowDialog() == true || dialog.Accepted;
	}

	private void ShowMessage(string message, string caption, MessageBoxImage image) =>
		ReduxMessageBox.Show(this, message, caption, MessageBoxButton.OK, image, MessageBoxResult.OK);
}
