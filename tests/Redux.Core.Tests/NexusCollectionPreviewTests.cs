using System;
using System.IO;
using System.Linq;
using DivinityModManager.AppServices;

namespace Redux.Core.Tests;

internal sealed class NexusCollectionPreviewTests
{
    public void CollectionBulkSelectionRespectsAvailabilityAndDefaults()
    {
        var oldShutdown = System.Windows.Application.Current.ShutdownMode;
        System.Windows.Application.Current.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        var window = new DivinityModManager.Views.ReduxCollectionWindow(null!, null!);
        DivinityModManager.Util.ReduxThemeService.Apply(window.Resources, DivinityModManager.Models.ReduxThemeType.ReduxDark);
        var required = new DivinityModManager.Views.CollectionFileChoice(new(5, 1, "Required", "", "", 100, false, true), false);
        var optional = new DivinityModManager.Views.CollectionFileChoice(new(5, 2, "Optional", "", "", 100, true, true), false);
        var queued = new DivinityModManager.Views.CollectionFileChoice(new(5, 3, "Queued", "", "", 100, false, true), true);
        var missing = new DivinityModManager.Views.CollectionFileChoice(new(0, 4, "Missing", "", "", 0, false, false), false);
        window.PresentFiles(new[] { required, optional, queued, missing });
        var save = (System.Windows.Controls.Button)window.FindName("SaveOrderButton");
        DivinityModManager.Util.ReduxActionButtonTransition.Apply(save, true, "ReduxSuccessPillBackground", "ReduxSuccessBrush", "ReduxSuccessBrush", true);
        RegressionAssert.True(save.IsEnabled);
        RegressionAssert.True(ReferenceEquals(save.Foreground, save.FindResource("ReduxSuccessBrush")));
        DivinityModManager.Util.ReduxActionButtonTransition.Apply(save, false, "ReduxSurfaceElevatedBrush", "ReduxBorderStrongBrush", "ReduxTextMutedBrush", true);
        RegressionAssert.True(!save.IsEnabled);
        RegressionAssert.True(ReferenceEquals(save.Foreground, save.FindResource("ReduxTextMutedBrush")));

        void Click(string name) => ((System.Windows.Controls.Button)window.FindName(name)).RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Click("SelectAllButton");
        RegressionAssert.True(required.Selected && optional.Selected && !queued.Selected && !missing.Selected);
        Click("DeselectAllButton");
        RegressionAssert.True(!required.Selected && !optional.Selected);
        Click("ResetButton");
        RegressionAssert.True(required.Selected && !optional.Selected && !queued.Selected && !missing.Selected);
        required.MarkQueued();
        Click("ResetButton");
        RegressionAssert.True(!required.Selected);
        window.Close();
        System.Windows.Application.Current.ShutdownMode = oldShutdown;
    }
    public void CollectionOrderUsesExplicitEnabledUuidSequence()
    {
        const string json = """
        {"info":{"domainName":"baldursgate3"},"loadOrder":[
        {"name":"Second","enabled":true,"data":{"uuid":"22222222-2222-2222-2222-222222222222","isListed":false}},
        {"name":"Inactive","enabled":false,"data":{"uuid":"33333333-3333-3333-3333-333333333333"}},
        {"name":"Override","enabled":true,"data":{"isListed":true}},
        {"name":"First","enabled":true,"data":{"uuid":"11111111-1111-1111-1111-111111111111","isListed":false}}]}
        """;
        var entries = NexusCollectionPreviewService.ParseLoadOrder(json);
        RegressionAssert.Equal(2, entries.Count);
        RegressionAssert.Equal("Second", entries[0].Name);
        RegressionAssert.Equal("First", entries[1].Name);
        try { NexusCollectionPreviewService.ParseLoadOrder("""{"info":{"domainName":"baldursgate3"},"loadOrder":[]}"""); throw new Exception("An empty collection order was accepted."); }
        catch (InvalidDataException) { }

        foreach (var invalid in new[] { json.Replace("baldursgate3", "skyrim"), json.Replace("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222"), json.Replace("11111111-1111-1111-1111-111111111111", "not-a-uuid") })
        {
            try { NexusCollectionPreviewService.ParseLoadOrder(invalid); }
            catch (InvalidDataException) { continue; }
            throw new Exception("Invalid collection order was accepted.");
        }
        RegressionAssert.Equal("", NexusCollectionPreviewService.SafeImageUrl("https://evil.test/test.png"));
        RegressionAssert.Equal("", NexusCollectionPreviewService.SafeImageUrl("file:///C:/test.png"));
        RegressionAssert.Equal("https://media.nexusmods.com/test.webp", NexusCollectionPreviewService.SafeImageUrl("https://media.nexusmods.com/test.webp"));
    }

    public void CollectionManifestDownloadKeepsAccountHeadersOnApiHost()
    {
        using var handler = new ManifestHandler();
        using var client = new System.Net.Http.HttpClient(handler);
        var preview = new NexusCollectionPreview("abc", "Example", "", 1, Array.Empty<NexusCollectionFile>()) { ManifestLink = "/v2/collections/1/revisions/2/download_link" };
        var entries = new NexusCollectionPreviewService(client).LoadOrderAsync(preview, "test-key", default).GetAwaiter().GetResult();
        RegressionAssert.Equal(1, entries.Count);
        RegressionAssert.Equal(2, handler.Calls);
    }

    private sealed class ManifestHandler : System.Net.Http.HttpMessageHandler
    {
        public int Calls;
        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken token)
        {
            Calls++;
            System.Net.Http.HttpContent content;
            if (Calls == 1)
            {
                RegressionAssert.Equal("api.nexusmods.com", request.RequestUri!.Host);
                RegressionAssert.True(request.Headers.Contains("apikey"));
                content = new System.Net.Http.StringContent("""{"download_links":[{"URI":"https://premium-files.nexus-cdn.com/collection.zip"}]}""");
            }
            else
            {
                RegressionAssert.True(!request.Headers.Contains("apikey"));
                using var data = new MemoryStream();
                using (var zip = new System.IO.Compression.ZipArchive(data, System.IO.Compression.ZipArchiveMode.Create, true))
                using (var writer = new StreamWriter(zip.CreateEntry("collection.json").Open()))
                    writer.Write("""{"info":{"domainName":"baldursgate3"},"loadOrder":[{"name":"Example","enabled":true,"data":{"uuid":"11111111-1111-1111-1111-111111111111","isListed":false}}]}""");
                content = new System.Net.Http.ByteArrayContent(data.ToArray());
            }
            return System.Threading.Tasks.Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content });
        }
    }

    public void CollectionInventoryMatchesExactFilesAcrossBothPanes()
    {
        var inventory = new NexusCollectionInstallInventory(new[] {
            new NexusInstalledFile(5, 101, "Active Mods"), new NexusInstalledFile(5, 102, "Inactive Mods"), new NexusInstalledFile(6, 0, "Game directory") });
        NexusCollectionFile File(long mod, long file) => new(mod, file, "Example", "Core", "1", 100, false, true);
        RegressionAssert.Equal(NexusCollectionInstallState.Installed, inventory.GetState(File(5, 101)));
        RegressionAssert.Equal(NexusCollectionInstallState.Installed, inventory.GetState(File(5, 102)));
        RegressionAssert.Equal("Active Mods", inventory.GetLocation(File(5, 101)));
        RegressionAssert.Equal("Inactive Mods", inventory.GetLocation(File(5, 102)));
        RegressionAssert.Equal(NexusCollectionInstallState.DifferentFile, inventory.GetState(File(5, 103)));
        RegressionAssert.Equal(NexusCollectionInstallState.Unverified, inventory.GetState(File(6, 201)));
        RegressionAssert.Equal(NexusCollectionInstallState.Missing, inventory.GetState(File(7, 301)));
        var installed = new DivinityModManager.Views.CollectionFileChoice(File(5, 102), false, inventory.GetState(File(5, 102)), inventory.GetLocation(File(5, 102)));
        installed.Selected = true;
        installed.ResetToDefault();
        RegressionAssert.True(!installed.Selected && !installed.CanSelect);
        RegressionAssert.True(installed.Status.Contains("Inactive Mods"));
        var different = new DivinityModManager.Views.CollectionFileChoice(File(5, 103), false, NexusCollectionInstallState.DifferentFile);
        RegressionAssert.True(different.Selected && different.CanSelect);
    }

    public void CollectionGuideAdvancesOnlyMatchingFilesAwaitingAuthorization()
    {
        NexusCollectionFile File(long file) => new(5, file, "Example", "", "", 0, false, true);
        var first = new DivinityModManager.Models.NexusMods.NxmDownloadItem { ModId = 5, FileId = 1, QueuePosition = 1, State = DivinityModManager.Models.NexusMods.NxmDownloadState.NeedsFreshLink };
        var second = new DivinityModManager.Models.NexusMods.NxmDownloadItem { ModId = 5, FileId = 2, QueuePosition = 2, State = DivinityModManager.Models.NexusMods.NxmDownloadState.NeedsFreshLink };
        var unrelated = new DivinityModManager.Models.NexusMods.NxmDownloadItem { ModId = 99, FileId = 1, QueuePosition = 0, State = DivinityModManager.Models.NexusMods.NxmDownloadState.NeedsFreshLink };
        var files = new[] { File(1), File(2), File(3) };
        var downloads = new[] { unrelated, second, first };
        var progress = NexusCollectionDownloadProgress.From(files, downloads);
        RegressionAssert.True(ReferenceEquals(first, progress.NextFile));
        RegressionAssert.Equal(2, progress.Total);
        RegressionAssert.Equal(2, progress.AwaitingLink);
        first.State = DivinityModManager.Models.NexusMods.NxmDownloadState.Downloading;
        progress = NexusCollectionDownloadProgress.From(files, downloads);
        RegressionAssert.True(ReferenceEquals(second, progress.NextFile));
        second.State = DivinityModManager.Models.NexusMods.NxmDownloadState.Downloaded;
        progress = NexusCollectionDownloadProgress.From(files, downloads);
        RegressionAssert.True(progress.NextFile == null);
        RegressionAssert.Equal(1, progress.Downloaded);
        first.State = DivinityModManager.Models.NexusMods.NxmDownloadState.NeedsFreshLink;
        RegressionAssert.True(ReferenceEquals(first, NexusCollectionDownloadProgress.From(files, downloads).NextFile));
    }

    public void CollectionSessionsRoundTripSelectionsAndSeparateRevisions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReduxCollectionSessionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "sessions.json");
            var store = new NexusCollectionSessionStore(path);
            var session = new NexusCollectionSession("abc123", "Example", 3, new[] { new NexusCollectionFileIdentity(5, 102) });
            store.Save(session);
            var restored = new NexusCollectionSessionStore(path).Load()[0];
            RegressionAssert.Equal(102L, restored.SelectedFiles[0].FileId);
            RegressionAssert.True(restored.Matches(new("abc123", "Example", "", 3, Array.Empty<NexusCollectionFile>())));
            RegressionAssert.True(!restored.Matches(new("abc123", "Example", "", 4, Array.Empty<NexusCollectionFile>())));
            store.Save(session with { SelectedFiles = Array.Empty<NexusCollectionFileIdentity>() });
            RegressionAssert.Equal(0, store.Load()[0].SelectedFiles.Length);
            for (var i = 0; i < 12; i++) store.Save(new($"slug{i}", $"Collection {i}", 1, Array.Empty<NexusCollectionFileIdentity>()));
            RegressionAssert.Equal(10, store.Load().Count);
            RegressionAssert.Equal("slug11", store.Load()[0].Slug);
            store.Save(new("slug5", "Updated name", 2, Array.Empty<NexusCollectionFileIdentity>()));
            RegressionAssert.Equal(10, store.Load().Count);
            RegressionAssert.Equal("Updated name", store.Load()[0].Name);
        }
        finally { Directory.Delete(directory, true); }
    }
    public void CollectionSessionsRejectCorruptDataWithoutOverwritingIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReduxCollectionSessionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "sessions.json");
            var store = new NexusCollectionSessionStore(path);
            foreach (var invalid in new[] { new NexusCollectionSession("../other", "Test", 1, Array.Empty<NexusCollectionFileIdentity>()), new NexusCollectionSession("abc", "Test", 0, Array.Empty<NexusCollectionFileIdentity>()) })
            {
                try { store.Save(invalid); throw new Exception("Invalid collection session was accepted."); }
                catch (InvalidDataException) { }
            }
            File.WriteAllText(path, "not json");
            try { store.Save(new("abc", "Test", 1, Array.Empty<NexusCollectionFileIdentity>())); throw new Exception("Corrupt sessions were overwritten."); }
            catch (System.Text.Json.JsonException) { }
            RegressionAssert.Equal("not json", File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, true); }
    }

    public void CollectionFiltersKeepHiddenSelectionsAndLimitBulkActions()
    {
        var oldShutdown = System.Windows.Application.Current.ShutdownMode;
        System.Windows.Application.Current.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        var window = new DivinityModManager.Views.ReduxCollectionWindow(null!, null!);
        try
        {
            var first = new DivinityModManager.Views.CollectionFileChoice(new(5, 1, "Core", "Main file", "1", 0, false, true) { Author = "Example author" }, false);
            var second = new DivinityModManager.Views.CollectionFileChoice(new(6, 2, "Textures", "Optional", "1", 0, false, true), false);
            window.PresentFiles(new[] { first, second });
            ((System.Windows.Controls.TextBox)window.FindName("SearchBox")).Text = "EXAMPLE AUTHOR";
            RegressionAssert.Equal(1, ((System.Windows.Controls.ListBox)window.FindName("FilesList")).Items.Count);
            ((System.Windows.Controls.Button)window.FindName("DeselectAllButton")).RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            RegressionAssert.True(!first.Selected && second.Selected);
            ((System.Windows.Controls.TextBox)window.FindName("SearchBox")).Text = "";
            RegressionAssert.Equal(2, ((System.Windows.Controls.ListBox)window.FindName("FilesList")).Items.Count);
            RegressionAssert.True(!first.Selected && second.Selected);
            var queued = new DivinityModManager.Views.CollectionFileChoice(first.File, true);
            queued.UpdateQueueStatus("Failed", DivinityModManager.Models.NexusMods.NxmDownloadState.Failed);
            RegressionAssert.True(queued.MatchesFilter("", DivinityModManager.Views.CollectionFileFilter.NeedsAttention));
            queued.UpdateQueueStatus("Installed", DivinityModManager.Models.NexusMods.NxmDownloadState.Installed);
            RegressionAssert.True(queued.MatchesFilter("", DivinityModManager.Views.CollectionFileFilter.Installed));
            RegressionAssert.True(!queued.MatchesFilter("", DivinityModManager.Views.CollectionFileFilter.Missing));
        }
        finally { window.Close(); System.Windows.Application.Current.ShutdownMode = oldShutdown; }
    }
    public void CollectionRetrySkipsUnsafeAndUnrelatedItemsAndContinuesAfterFailure()
    {
        var items = new[] {
            new DivinityModManager.Models.NexusMods.NxmDownloadItem { ModId=5, FileId=1, State=DivinityModManager.Models.NexusMods.NxmDownloadState.Failed, QueuePosition=1 },
            new DivinityModManager.Models.NexusMods.NxmDownloadItem { ModId=5, FileId=2, State=DivinityModManager.Models.NexusMods.NxmDownloadState.Failed, QueuePosition=2 },
            new DivinityModManager.Models.NexusMods.NxmDownloadItem { ModId=5, FileId=3, State=DivinityModManager.Models.NexusMods.NxmDownloadState.Failed, ErrorCode="rollback-failed" },
            new DivinityModManager.Models.NexusMods.NxmDownloadItem { ModId=5, FileId=4, State=DivinityModManager.Models.NexusMods.NxmDownloadState.InstallFailed },
            new DivinityModManager.Models.NexusMods.NxmDownloadItem { ModId=6, FileId=1, State=DivinityModManager.Models.NexusMods.NxmDownloadState.Failed }
        };
        var files = Enumerable.Range(1,4).Select(id => new NexusCollectionFile(5,id,"","","",0,false,true));
        var calls = 0;
        var result = NexusCollectionRecovery.RetryAsync(files, items, id => {
            calls++;
            if (id == items[0].Id) throw new IOException("Simulated failure");
            items[1].State = DivinityModManager.Models.NexusMods.NxmDownloadState.NeedsFreshLink;
            return System.Threading.Tasks.Task.CompletedTask;
        }, default).GetAwaiter().GetResult();
        RegressionAssert.Equal(2, calls);
        RegressionAssert.Equal(1, result.Retried);
        RegressionAssert.Equal(1, result.Failed);
    }
    public void CollectionOrderReviewNeverSelectsModsForActivation()
    {
        var oldShutdown = System.Windows.Application.Current.ShutdownMode;
        System.Windows.Application.Current.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        var requirements = new[] { new SaveModRequirement("11111111-1111-1111-1111-111111111111", "Inactive example", SaveModStatus.Inactive), new SaveModRequirement("22222222-2222-2222-2222-222222222222", "Missing example", SaveModStatus.Missing) };
        var window = new DivinityModManager.Views.ReduxSaveModReviewWindow(null!, "Collection", requirements, null!, collectionOrder: true);
        try
        {
            RegressionAssert.Equal("Collection Load Order", window.Title);
            RegressionAssert.True(((System.Windows.Controls.Button)window.FindName("ApplyButton")).IsEnabled);
            var rows = ((System.Windows.Controls.ItemsControl)window.FindName("RequirementsGrid")).Items.Cast<DivinityModManager.Views.SaveModReviewChoice>().ToArray();
            rows[0].Selected = true;
            RegressionAssert.True(!rows[0].Selected && !rows[0].NeedsAttention);
            RegressionAssert.Equal(System.Windows.Visibility.Collapsed, rows[0].SelectionVisibility);
            RegressionAssert.Equal(0, window.SelectedIds.Count);
            RegressionAssert.True(rows[1].NeedsAttention);
        }
        finally { window.Close(); System.Windows.Application.Current.ShutdownMode = oldShutdown; }
    }

    private const string Response = """
        {"data":{"collection":{"slug":"abc123","name":"Example","summary":"Test collection",
        "game":{"domainName":"baldursgate3"},"latestPublishedRevision":{"revisionNumber":3,"modFiles":[
        {"fileId":101,"optional":false,"file":{"fileId":101,"name":"Core","version":"1","sizeInBytes":"123","mod":{"modId":5,"name":"Example mod"}}},
        {"fileId":102,"optional":true,"file":{"fileId":102,"name":"Textures","mod":{"modId":5,"name":"Example mod"}}},
        {"fileId":103,"optional":false,"file":null}]}}}}
        """;

    public void CollectionLinksAreRestrictedToBg3OnNexus()
    {
        foreach (var host in new[] { "www.nexusmods.com", "nexusmods.com", "next.nexusmods.com" })
        {
            RegressionAssert.True(NexusCollectionLink.TryParse($"https://{host}/baldursgate3/collections/abc123", out var link));
            RegressionAssert.Equal("abc123", link.Slug);
        }
        RegressionAssert.True(NexusCollectionLink.TryParse("https://www.nexusmods.com/games/baldursgate3/collections/abc123/revisions/3", out var pinned));
        RegressionAssert.Equal(3, pinned.Revision!.Value);
        foreach (var invalid in new[] { "http://www.nexusmods.com/baldursgate3/collections/abc123", "https://www.nexusmods.com.evil.test/baldursgate3/collections/abc123",
            "https://www.nexusmods.com/skyrim/collections/abc123", "https://user@www.nexusmods.com/baldursgate3/collections/abc123",
            "https://www.nexusmods.com/baldursgate3/collections/abc123/revisions/0", "https://www.nexusmods.com/baldursgate3/collections/abc123?revision=2" })
            RegressionAssert.True(!NexusCollectionLink.TryParse(invalid, out _));
    }

    public void CollectionPreviewPreservesFilesAndUnavailableEntries()
    {
        var preview = NexusCollectionPreviewService.ParseResponse(Response, new("abc123", 3));
        RegressionAssert.Equal(3, preview.Files.Count);
        RegressionAssert.Equal(preview.Files[0].ModId, preview.Files[1].ModId);
        RegressionAssert.True(preview.Files[0].FileId != preview.Files[1].FileId);
        RegressionAssert.True(preview.Files[1].Optional);
        RegressionAssert.True(!preview.Files[2].Available);
        RegressionAssert.Equal(123L, preview.Files[0].SizeBytes);
    }

    public void CollectionPreviewRejectsWrongIdentityAndPartialResponses()
    {
        Reject(Response, new("abc123", 2));
        Reject(Response, new("other", null));
        Reject(Response.Replace("baldursgate3", "skyrim"), new("abc123", null));
        Reject(Response.Replace("102", "101"), new("abc123", null));
        Reject("{\"errors\":[{\"message\":\"Failed\"}],\"data\":null}", new("abc123", null));
    }

    public void CollectionPreviewCoalescesOnlyMatchingRepeatedFiles()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(Response)!;
        var rows = root["data"]!["collection"]!["latestPublishedRevision"]!["modFiles"]!.AsArray();
        var repeat = rows[0]!.DeepClone();
        rows.Add(repeat);
        rows.Add(rows[2]!.DeepClone());
        var link = new NexusCollectionLink("abc123", 3);
        var preview = NexusCollectionPreviewService.ParseResponse(root.ToJsonString(), link);
        RegressionAssert.True(preview.Files.Select(file => file.FileId).SequenceEqual(new long[] { 101, 102, 103 }));
        RegressionAssert.False(preview.Files[2].Available);
        repeat["optional"] = true;
        Reject(root.ToJsonString(), link);
    }

    private static void Reject(string json, NexusCollectionLink link)
    {
        try { NexusCollectionPreviewService.ParseResponse(json, link); }
        catch (InvalidDataException) { return; }
        throw new Exception("Unsafe collection preview was accepted.");
    }
}
