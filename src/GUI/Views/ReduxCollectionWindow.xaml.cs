using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using System.ComponentModel;
using System.Net.Http;
using System.Windows;

namespace DivinityModManager.Views;

public enum CollectionFileFilter { All, Missing, Installed, Unverified, NeedsAttention }

public sealed class CollectionFileChoice : INotifyPropertyChanged
{
    public NexusCollectionFile File { get; }
    public bool CanSelect { get; private set; }
    public string Name => String.IsNullOrWhiteSpace(File.ModName) ? $"File {File.FileId}" : File.ModName;
    public string Metadata => String.Join(" · ", new[] { File.FileName, File.Version, $"File {File.FileId}" }.Where(s => !String.IsNullOrWhiteSpace(s)));
    public string RowDetails => String.Join(" · ", new[] { Status, RequirementText, !String.Equals(File.FileName, Name, StringComparison.OrdinalIgnoreCase) ? File.FileName : "", !String.IsNullOrWhiteSpace(File.Version) ? $"v{File.Version}" : "", File.Author, File.SizeBytes > 0 ? SizeText : "" }.Where(s => !String.IsNullOrWhiteSpace(s)));
    public string AuthorCategory => String.Join(" · ", new[] { File.Author, File.Category }.Where(s => !String.IsNullOrWhiteSpace(s)));
    public string SizeText => FormatSize(File.SizeBytes);
    public bool HasSource => File.ModId > 0;
    public string VersionText => String.IsNullOrWhiteSpace(File.Version) ? "Version unavailable" : $"Version {File.Version}";
    public static string FormatSize(long bytes) => bytes <= 0 ? "Size unavailable" : bytes >= 1073741824 ? $"{bytes / 1073741824d:0.##} GB" : bytes >= 1048576 ? $"{bytes / 1048576d:0.##} MB" : $"{bytes / 1024d:0.##} KB";
    public NexusCollectionInstallState InstallState { get; }
    public string InstalledLocation { get; }
    private bool _queued;
    public bool IsQueued => _queued;
    public DivinityModManager.Models.NexusMods.NxmDownloadState? QueueState { get; private set; }
    private string _queueStatus = "In Download Manager";
    public bool UpdateQueueStatus(string status, DivinityModManager.Models.NexusMods.NxmDownloadState state)
    {
        if (!_queued || (_queueStatus == status && QueueState == state)) return false;
        _queueStatus = status; QueueState = state;
        PropertyChanged?.Invoke(this, new(nameof(Status))); PropertyChanged?.Invoke(this, new(nameof(RowDetails)));
        return true;
    }
    public bool MatchesFilter(string search, CollectionFileFilter filter)
    {
        var text = String.Join(" ", Name, File.FileName, File.Author, File.Category, File.Version);
        if (!String.IsNullOrWhiteSpace(search) && !text.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        return filter switch
        {
            CollectionFileFilter.Missing => File.Available && InstallState == NexusCollectionInstallState.Missing && QueueState != DivinityModManager.Models.NexusMods.NxmDownloadState.Installed,
            CollectionFileFilter.Installed => InstallState == NexusCollectionInstallState.Installed || QueueState == DivinityModManager.Models.NexusMods.NxmDownloadState.Installed,
            CollectionFileFilter.Unverified => InstallState is NexusCollectionInstallState.Unverified or NexusCollectionInstallState.DifferentFile,
            CollectionFileFilter.NeedsAttention => !File.Available || InstallState is NexusCollectionInstallState.Unverified or NexusCollectionInstallState.DifferentFile
                || QueueState is DivinityModManager.Models.NexusMods.NxmDownloadState.Failed or DivinityModManager.Models.NexusMods.NxmDownloadState.InstallFailed or DivinityModManager.Models.NexusMods.NxmDownloadState.NeedsFreshLink or DivinityModManager.Models.NexusMods.NxmDownloadState.NeedsReview,
            _ => true
        };
    }
    public bool DefaultSelected => CanSelect && !File.Optional && InstallState != NexusCollectionInstallState.Installed;
    public string SelectionNote => !File.Available ? "This file is unavailable on Nexus Mods." : _queued ? "This file is already in Download Manager. Continue its download or installation there."
        : InstallState == NexusCollectionInstallState.Installed ? "An installed mod records this exact Nexus file ID. It is excluded from downloads. Active and inactive mods are both checked."
        : InstallState == NexusCollectionInstallState.DifferentFile ? "A different file from this Nexus mod page is installed. This may be another version or an optional file; review the collection’s file before downloading."
        : InstallState == NexusCollectionInstallState.Unverified ? "This Nexus mod is detected, but its installed file ID is unknown. Review before downloading; it may already be installed."
        : "No matching Nexus-linked installed mod was found. Mods without source information cannot be matched.";
    public string Status => !File.Available ? "Unavailable" : InstallState == NexusCollectionInstallState.Installed ? $"Installed ({InstalledLocation})" : _queued ? _queueStatus
        : InstallState == NexusCollectionInstallState.DifferentFile ? "Different file installed" : InstallState == NexusCollectionInstallState.Unverified ? "Installed file unverified" : "Missing";
    public string RequirementText => File.Optional ? "Optional" : "Required";
    private bool _selected;
    public bool Selected { get => _selected; set { var selected = value && CanSelect; if (_selected == selected) return; _selected = selected; PropertyChanged?.Invoke(this, new(nameof(Selected))); } }
    public void ResetToDefault() => Selected = DefaultSelected;
    public event PropertyChangedEventHandler PropertyChanged;
    public CollectionFileChoice(NexusCollectionFile file, bool alreadyQueued, NexusCollectionInstallState installState = NexusCollectionInstallState.Missing, string installedLocation = "Mod library")
    { File = file; InstalledLocation = installedLocation; InstallState = installState; _queued = alreadyQueued; CanSelect = file.Available && !alreadyQueued && installState != NexusCollectionInstallState.Installed; _selected = DefaultSelected; }
    public void MarkQueued()
    { _queued = true; CanSelect = false; Selected = false; PropertyChanged?.Invoke(this, new(nameof(CanSelect))); PropertyChanged?.Invoke(this, new(nameof(Status))); PropertyChanged?.Invoke(this, new(nameof(RowDetails))); PropertyChanged?.Invoke(this, new(nameof(SelectionNote))); }
}

public partial class ReduxCollectionWindow : AdonisUI.Controls.AdonisWindow
{
    private readonly MainWindowViewModel _viewModel;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HttpClient _client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
    private CollectionFileChoice[] _choices = Array.Empty<CollectionFileChoice>();
    private System.ComponentModel.ICollectionView _fileView;
    private NexusCollectionPreview _preview;
    private IReadOnlyList<DivinityModManager.Models.DivinityLoadOrderEntry> _collectionOrder;
    private readonly System.Windows.Threading.DispatcherTimer _guideTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private NexusCollectionDownloadProgress _downloadProgress;
    private string _openedGuideItem;
    private NexusCollectionSessionStore _sessionStore;
    private IReadOnlyList<NexusCollectionSession> _sessions = Array.Empty<NexusCollectionSession>();
    private readonly System.Windows.Threading.DispatcherTimer _sessionSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _sessionDirty;
    private bool _busy;
    private bool _changingSelection;
    public ReduxCollectionWindow(Window owner, MainWindowViewModel viewModel)
    {
        InitializeComponent();
        Owner = owner; _viewModel = viewModel;
        ReduxWindowBehavior.AttachDialogTransitions(this, 40);
        ReduxWindowBehavior.AttachRoundedCorners(this);
        var settings = viewModel?.Settings;
        if (settings != null) ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);
        if (viewModel != null)
        {
            _sessionStore = new NexusCollectionSessionStore(DivinityApp.GetAppDirectory("Data", "collection-sessions.json"));
            try
            {
                _sessions = _sessionStore.Load();
                if (_sessions.Count > 0)
                {
                    LinkBox.Text = _sessions[0].Link;
                    StatusText.Text = "Import to resume your last collection, or choose another from Recent collections.";
                    RecentButton.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException or System.Text.Json.JsonException)
            { StatusText.Text = "Saved collection choices could not be read. You can still import a collection link."; }
        }
        _sessionSaveTimer.Tick += (_, _) => { _sessionSaveTimer.Stop(); PersistSession(); };
        _guideTimer.Tick += (_, _) => UpdateDownloadGuide();
        Loaded += (_, _) => _guideTimer.Start();
        Closed += (_, _) => { _sessionSaveTimer.Stop(); PersistSession(); _guideTimer.Stop(); _lifetime.Cancel(); _client.Dispose(); };
    }
    private void Refresh()
    {
        if (DownloadButton == null || _changingSelection) return;
        var canSaveOrder = !_busy && _preview != null && _collectionOrder?.Count > 0;
        ReduxActionButtonTransition.Apply(SaveOrderButton, canSaveOrder,
            canSaveOrder ? "ReduxSuccessPillBackground" : "ReduxSurfaceElevatedBrush",
            canSaveOrder ? "ReduxSuccessBrush" : "ReduxBorderStrongBrush",
            canSaveOrder ? "ReduxSuccessBrush" : "ReduxTextMutedBrush",
            _viewModel?.Settings?.ReduceMotion == true);
        DownloadButton.IsEnabled = !_busy && _choices.Any(c => c.Selected);
        var visible = (_fileView?.Cast<CollectionFileChoice>() ?? _choices).ToHashSet();
        SelectAllButton.IsEnabled = !_busy && visible.Any(c => c.CanSelect && !c.Selected);
        DeselectAllButton.IsEnabled = !_busy && visible.Any(c => c.Selected);
        ResetButton.IsEnabled = !_busy && _choices.Any(c => c.Selected != c.DefaultSelected);
        var selected = _choices.Where(c => c.Selected).ToArray();
        var hidden = selected.Count(c => !visible.Contains(c));
        var bytes = selected.Sum(c => (decimal)c.File.SizeBytes);
        SelectionText.Text = selected.Length == 0 ? "No files selected" : $"{selected.Length} selected · {CollectionFileChoice.FormatSize((long)Math.Min(bytes, Int64.MaxValue))}" + (selected.Any(c => c.File.SizeBytes <= 0) ? " (some sizes unknown)" : "") + (hidden > 0 ? $" · {hidden} hidden by filters" : "");
    }
    private void UpdateDownloadGuide()
    {
        if (_viewModel?.NxmDownloads == null || _preview == null || _busy)
        { if (NextFileButton != null) NextFileButton.IsEnabled = false; if (RetryFailedButton != null) RetryFailedButton.IsEnabled = false; return; }
        var downloads = _viewModel.NxmDownloads.ToArray();
        _downloadProgress = NexusCollectionDownloadProgress.From(_choices.Where(c => c.IsQueued).Select(c => c.File), downloads);
        DownloadGuidePanel.Visibility = _downloadProgress.Total > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextFileButton.Visibility = _downloadProgress.NextFile == null ? Visibility.Collapsed : Visibility.Visible;
        NextFileButton.IsEnabled = _downloadProgress.NextFile != null;
        var retryCount = NexusCollectionRecovery.Eligible(_choices.Where(c => c.IsQueued).Select(c => c.File), downloads).Count;
        RetryFailedButton.Visibility = retryCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        RetryFailedButton.IsEnabled = retryCount > 0;
        RetryFailedLabel.Text = $"Retry failed downloads ({retryCount})";
        var counts = $"{_downloadProgress.Downloaded} downloaded · {_downloadProgress.Installed} installed · {_downloadProgress.AwaitingLink} need a Nexus link";
        if (_downloadProgress.Failed > 0) counts += $" · {_downloadProgress.Failed} need attention";
        var next = _downloadProgress.NextFile;
        NextFileLabel.Text = next?.Id == _openedGuideItem ? "Open file again" : "Download next file";
        DownloadGuideText.Text = counts + (next == null ? ". Continue in Download Manager." :
            $"\n{(next.Id == _openedGuideItem ? "Waiting for" : "Next:")} {next.FileDisplayName}. Choose Mod Manager Download on Nexus; Redux will receive the link.");
        var lookup = downloads.GroupBy(d => (d.ModId, d.FileId)).ToDictionary(g => g.Key, g => g.First());
        var changed = false;
        foreach (var choice in _choices)
            if (lookup.TryGetValue((choice.File.ModId, choice.File.FileId), out var item)) changed |= choice.UpdateQueueStatus(item.StatusText, item.State);
        if (changed) ApplyFileFilter();
    }
    private async void RetryFailed_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _viewModel == null) return;
        Busy(true);
        try
        {
            var result = await _viewModel.RetryCollectionDownloadsAsync(_choices.Where(c => c.IsQueued).Select(c => c.File).ToArray(), _lifetime.Token);
            StatusText.Text = $"Retry started for {result.Retried} downloads." + (result.Failed > 0 ? $" {result.Failed} could not restart; check Download Manager." : "");
        }
        catch (OperationCanceledException) { }
        catch (Exception) { StatusText.Text = "Could not retry collection downloads. Check Download Manager."; }
        finally { Busy(false); }
    }
    private void FilterChanged(object sender, RoutedEventArgs e) => ApplyFileFilter();
    private void ClearFilters_Click(object sender, RoutedEventArgs e) { SearchBox.Text = ""; FilterBox.SelectedIndex = 0; }
    public void ApplyFileFilter()
    {
        if (_fileView == null || SearchBox == null || FilterBox == null) return;
        Enum.TryParse<CollectionFileFilter>(FilterBox.SelectedValue?.ToString(), out var filter);
        var search = SearchBox.Text;
        _fileView.Filter = item => item is CollectionFileChoice choice && choice.MatchesFilter(search, filter);
        ClearFiltersButton.IsEnabled = !String.IsNullOrWhiteSpace(search) || filter != CollectionFileFilter.All;
        VisibleFilesText.Text = $"{_fileView.Cast<object>().Count()} of {_choices.Length} files";
        Refresh();
    }
    private void NextFile_Click(object sender, RoutedEventArgs e)
    {
        UpdateDownloadGuide();
        if (_busy || _downloadProgress?.NextFile?.NexusPage is not { } page) return;
        _openedGuideItem = _downloadProgress.NextFile.Id;
        ProcessHelper.TryOpenUrl(page.ToString());
        UpdateDownloadGuide();
    }
    private void ChangeSelection(Action<CollectionFileChoice> action, bool visibleOnly = false)
    {
        if (_busy) return;
        _changingSelection = true;
        try { foreach (var choice in visibleOnly && _fileView != null ? _fileView.Cast<CollectionFileChoice>().ToArray() : _choices) action(choice); }
        finally { _changingSelection = false; Refresh(); ScheduleSessionSave(); }
    }
    private void SelectAll_Click(object sender, RoutedEventArgs e) => ChangeSelection(c => c.Selected = true, true);
    private void DeselectAll_Click(object sender, RoutedEventArgs e) => ChangeSelection(c => c.Selected = false, true);
    private void Reset_Click(object sender, RoutedEventArgs e) => ChangeSelection(c => c.ResetToDefault());
    public void PresentFiles(CollectionFileChoice[] choices)
    {
        foreach (var choice in _choices) choice.PropertyChanged -= ChoiceChanged;
        _choices = choices;
        foreach (var choice in _choices) choice.PropertyChanged += ChoiceChanged;
        _fileView = new System.Windows.Data.ListCollectionView(_choices);
        FilesList.ItemsSource = _fileView;
        ApplyFileFilter();
        if (_choices.Length > 0) FilesList.SelectedIndex = 0;
        Refresh();
    }
    private void ChoiceChanged(object sender, PropertyChangedEventArgs e)
    {
        Refresh();
        if (e.PropertyName == nameof(CollectionFileChoice.Selected) && !_changingSelection) ScheduleSessionSave();
    }
    private void ScheduleSessionSave()
    {
        if (_preview == null || _sessionStore == null) return;
        _sessionDirty = true;
        _sessionSaveTimer.Stop();
        _sessionSaveTimer.Start();
    }
    private void PersistSession()
    {
        if (!_sessionDirty || _preview == null || _sessionStore == null) return;
        try
        {
            var session = new NexusCollectionSession(_preview.Slug, _preview.Name, _preview.Revision,
                _choices.Where(c => c.Selected || c.IsQueued).Select(c => new NexusCollectionFileIdentity(c.File.ModId, c.File.FileId)).Distinct().ToArray());
            _sessions = _sessionStore.Save(session);
            _sessionDirty = false;
            RecentButton.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { StatusText.Text = "Your choices could not be saved for next time. Downloads can still continue."; }
    }
    private void Recent_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not System.Windows.Controls.Button button) return;
        PersistSession();
        var menu = button.ContextMenu;
        menu.Items.Clear();
        foreach (var session in _sessions)
        {
            var item = new System.Windows.Controls.MenuItem { Header = $"{session.Name} · Revision {session.Revision}", ToolTip = session.Link };
            item.Icon = new DivinityModManager.Controls.ReduxIcon { StrokeData = (System.Windows.Media.Geometry)FindResource("Redux.Icon.Clock"), Style = (Style)FindResource("ReduxOptionalInterfaceIconStyle") };
            item.Click += (_, _) => { LinkBox.Text = session.Link; Preview_Click(PreviewButton, new RoutedEventArgs()); };
            menu.Items.Add(item);
        }
        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }
    private void Files_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DetailsPanel == null) return;
        DetailsPanel.DataContext = FilesList.SelectedItem;
        DetailsPanel.Visibility = FilesList.SelectedItem == null ? Visibility.Collapsed : Visibility.Visible;
        DetailsPlaceholder.Visibility = FilesList.SelectedItem == null ? Visibility.Visible : Visibility.Collapsed;
    }
    private void BrowseCollections_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenUrl("https://www.nexusmods.com/games/baldursgate3/collections");
    private void Source_Click(object sender, RoutedEventArgs e)
    {
        if (FilesList.SelectedItem is CollectionFileChoice { File.ModId: > 0 } choice)
            ProcessHelper.TryOpenUrl($"https://www.nexusmods.com/baldursgate3/mods/{choice.File.ModId}?tab=files&file_id={choice.File.FileId}");
    }
    private void Busy(bool value) { _busy = value; PreviewButton.IsEnabled = !value; LinkBox.IsEnabled = !value; FilesList.IsEnabled = !value; RecentButton.IsEnabled = !value; Refresh(); UpdateDownloadGuide(); }
    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        PersistSession();
        _sessionSaveTimer.Stop();
        _sessionDirty = false;
        _preview = null; _collectionOrder = null; _downloadProgress = null; _openedGuideItem = null; DownloadGuidePanel.Visibility = Visibility.Collapsed; SaveOrderButton.ToolTip = "Import a collection to check whether it includes a BG3 load order."; PresentFiles(Array.Empty<CollectionFileChoice>()); Heading.Text = "Add a Nexus collection"; CollectionSummary.Text = ""; CollectionImage.Source = null; CollectionImage.Visibility = Visibility.Collapsed;
        if (!NexusCollectionLink.TryParse(LinkBox.Text, out var link)) { StatusText.Text = "Enter a Baldur’s Gate 3 Nexus collection link."; return; }
        if (!_viewModel.Modules.SourceIntegrationsEnabled) { StatusText.Text = "In Preferences > General > Optional features, turn off 'Disable online mod information' to load collections."; return; }
        Busy(true); StatusText.Text = "Loading collection…";
        try
        {
            var preview = await new NexusCollectionPreviewService(_client).LoadAsync(link, _viewModel.Settings.NexusModsAPIKey, _lifetime.Token);
            await _viewModel.EnsureNxmDownloadsInitializedAsync();
            if (_lifetime.IsCancellationRequested) return;
            StatusText.Text = "Checking installed mods…";
            var inventory = await _viewModel.GetCollectionInstallInventoryAsync(_lifetime.Token);
            if (_lifetime.IsCancellationRequested) return;
            PresentFiles(preview.Files.Select(f => new CollectionFileChoice(f,
                _viewModel.NxmDownloads.Any(d => d.ModId == f.ModId && d.FileId == f.FileId && d.State != DivinityModManager.Models.NexusMods.NxmDownloadState.Installed), inventory.GetState(f), inventory.GetLocation(f))).ToArray());
            _preview = preview;
            var previousSession = _sessions.FirstOrDefault(s => s.Slug.Equals(preview.Slug, StringComparison.OrdinalIgnoreCase));
            if (previousSession?.Matches(preview) == true)
            {
                var selected = previousSession.SelectedFiles.ToHashSet();
                _changingSelection = true;
                try { foreach (var choice in _choices) choice.Selected = selected.Contains(new(choice.File.ModId, choice.File.FileId)); }
                finally { _changingSelection = false; }
            }
            ScheduleSessionSave();
            Heading.Text = preview.Name;
            CollectionSummary.Text = preview.Summary;
            if (!String.IsNullOrEmpty(preview.ThumbnailUrl))
            {
                CollectionImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(preview.ThumbnailUrl));
                CollectionImage.Visibility = Visibility.Visible;
            }
            if (_choices.Length > 0) FilesList.SelectedIndex = 0;
            StatusText.Text = $"Revision {preview.Revision} · {preview.Files.Count} files · {_choices.Count(c => c.InstallState == NexusCollectionInstallState.Installed)} installed · {preview.Files.Count(f => f.Optional)} optional · {preview.Files.Count(f => !f.Available)} unavailable";
            if (previousSession != null)
                StatusText.Text += previousSession.Matches(preview) ? " · Previous choices restored" : " · New revision: review the default selection";
            if (!String.IsNullOrEmpty(inventory.Warning)) StatusText.Text += " · " + inventory.Warning;
            SaveOrderButton.ToolTip = "Checking whether this collection includes a BG3 load order…";
            try
            {
                _collectionOrder = await new NexusCollectionPreviewService(_client).LoadOrderAsync(preview, _viewModel.Settings.NexusModsAPIKey, _lifetime.Token);
                SaveOrderButton.ToolTip = $"Save this collection’s {_collectionOrder.Count} ordered mods to your saved load orders. Select it later from the main window’s Load Order dropdown. Saving does not activate or apply it.";
            }
            catch (OperationCanceledException) when (!_lifetime.IsCancellationRequested)
            {
                SaveOrderButton.ToolTip = "The load-order check timed out. Import the collection again to retry.";
                StatusText.Text += " · Load order could not be checked";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                SaveOrderButton.ToolTip = ex is InvalidDataException ? ex.Message : "Could not retrieve the collection load order. Import the collection again to retry.";
                StatusText.Text += ex is InvalidDataException ? " · No importable load order" : " · Load order could not be checked";
            }
        }
        catch (OperationCanceledException) { if (!_lifetime.IsCancellationRequested) StatusText.Text = "Loading timed out. Try again."; }
        catch (Exception ex) { StatusText.Text = ex is InvalidDataException or InvalidOperationException ? ex.Message : "Could not load the collection. Check your connection and try again."; }
        finally { Busy(false); }
    }
    private void SaveOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _preview == null || _collectionOrder?.Count is not > 0) return;
        Busy(true);
        StatusText.Text = "Saving collection load order…";
        try
        {
            var entries = _collectionOrder;
            if (_lifetime.IsCancellationRequested) return;
            var review = new ReduxSaveModReviewWindow(this, _preview.Name, _viewModel.ReviewSaveMods(entries), _viewModel.Settings, collectionOrder: true);
            if (ReduxWindowBehavior.ShowDialogWithOwnerBackdrop(review, this) != true) return;
            var name = _viewModel.SaveCollectionLoadOrder(_preview, entries);
            StatusText.Text = $"Saved {name} ({entries.Count} mods). Select it from the main window’s Load Order dropdown.";
            ReduxMessageBox.Show(this,
                $"Saved “{name}” with {entries.Count} mods.\n\nYou can select it from the Load Order dropdown in the main Redux window. Your current load order has not been changed.",
                "Load Order Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { if (!_lifetime.IsCancellationRequested) StatusText.Text = "Reading the load order timed out. Try again."; }
        catch (Exception ex)
        {
            StatusText.Text = ex is InvalidDataException ? ex.Message : "Could not read or save the collection load order. Try again later.";
            if (!_lifetime.IsCancellationRequested)
                ReduxMessageBox.Show(this, StatusText.Text, "Load Order Not Saved", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { Busy(false); }
    }
    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var selected = _choices.Where(c => c.Selected).ToArray();
        Busy(true); var added = 0;
        try
        {
            foreach (var choice in selected)
            {
                _lifetime.Token.ThrowIfCancellationRequested();
                await _viewModel.QueueCollectionFileAsync(choice.File, _lifetime.Token);
                choice.MarkQueued(); added++;
            }
            StatusText.Text = $"{added} files sent to Download Manager. Review their download and installation status there.";
        }
        catch (OperationCanceledException) { }
        catch (Exception) { StatusText.Text = $"{added} files sent to Download Manager. Remaining files could not be added; try again."; }
        finally { Busy(false); }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
