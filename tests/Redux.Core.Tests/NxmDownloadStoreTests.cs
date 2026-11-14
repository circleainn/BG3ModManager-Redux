using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;

using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Redux.Core.Tests;

internal sealed class NxmDownloadStoreTests
{
	public void LocalPackageIdentityAndInspectionSurviveRestart()
	{
		using var fixture = new StoreFixture();
		var item = new NxmDownloadItem
		{
			QueuePosition = 1,
			SourceKind = AcquiredPackageSourceKind.LocalFile,
			SourceFileName = "Example.zip",
			ProjectName = "Example",
			CompletedFileName = "Example.zip",
			SizeBytes = 4,
			BytesReceived = 4,
			ArchiveSha256 = "9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a",
			DetectedContentKind = "PAK mod archive",
			DetectedDestination = "Inactive Mods",
			InspectionSummary = "Ready for Inactive Mods",
			InspectionCompleted = true,
			State = NxmDownloadState.Downloaded
		};
		File.WriteAllBytes(Path.Combine(fixture.Directory, item.CompletedFileName), [1, 2, 3, 4]);
		fixture.Store.SaveAsync([item]).GetAwaiter().GetResult();

		var restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();

		RegressionAssert.Equal(AcquiredPackageSourceKind.LocalFile, restored.SourceKind);
		RegressionAssert.Equal("local:" + item.ArchiveSha256, restored.Identity);
		RegressionAssert.Equal("PAK mod archive", restored.DetectedContentKind);
		RegressionAssert.Equal("Inactive Mods", restored.DetectedDestination);
		RegressionAssert.True(restored.InspectionCompleted);
	}

	public void RoundTripPreservesPublicQueueStateWithoutCapabilities()
	{
		using var fixture = new StoreFixture();
		var item = fixture.Item(NxmDownloadState.Paused);
		item.Authorization = new NexusModManagerLink(10, 20, "secret", 4_000_000_000, 7);
		item.AutoInstallRequested = true;
		item.ThumbnailUrl = "https://static.example.test/mod.webp";
		item.InstallDestination = "Inactive Mods";
		item.InstalledAt = DateTimeOffset.Parse("2026-09-07T12:00:00Z");
		fixture.Store.SaveAsync([item], CancellationToken.None).GetAwaiter().GetResult();

		var json = File.ReadAllText(Path.Combine(fixture.Directory, "downloads.json"));
		var restored = fixture.Store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult().Single();
		RegressionAssert.True(restored.AutoInstallRequested);

		RegressionAssert.False(json.Contains("secret", StringComparison.Ordinal));
		RegressionAssert.False(json.Contains("DownloadUri", StringComparison.Ordinal));
		RegressionAssert.Equal(item.Id, restored.Id);
		RegressionAssert.Equal(NxmDownloadState.Paused, restored.State);
		RegressionAssert.Equal(null, restored.Authorization);
		RegressionAssert.Equal(item.ThumbnailUrl, restored.ThumbnailUrl);
		RegressionAssert.Equal(item.InstallDestination, restored.InstallDestination);
		RegressionAssert.Equal(item.InstalledAt, restored.InstalledAt);
	}

	public void InstalledHistorySurvivesWhenItsArchiveWasRemoved()
	{
		using var fixture = new StoreFixture();
		var item = fixture.Item(NxmDownloadState.Installed);
		item.CompletedFileName = "removed-after-install.zip";
		item.InstallDestination = "Inactive Mods";
		fixture.Store.SaveAsync([item]).GetAwaiter().GetResult();

		var restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();

		RegressionAssert.Equal(NxmDownloadState.Installed, restored.State);
		RegressionAssert.Equal("Installed", restored.StatusText);
	}

	public void DetailedInstallFailureSurvivesRestartAndMissingFileInvalidation()
	{
		using var fixture = new StoreFixture();
		var item = fixture.Item(NxmDownloadState.InstallFailed);
		item.CompletedFileName = "complete.zip";
		item.SizeBytes = 4;
		item.ErrorCode = "missing-dependencies";
		item.ErrorDetails = "Missing dependency: Goon's Library. Install it and retry installation.";
		File.WriteAllBytes(Path.Combine(fixture.Directory, item.CompletedFileName), [1, 2, 3, 4]);
		fixture.Store.SaveAsync([item]).GetAwaiter().GetResult();
		var restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();
		RegressionAssert.Equal(item.ErrorDetails, restored.FailureDetails);
		RegressionAssert.Equal("Blocked by dependencies", restored.StatusText);
		File.Delete(Path.Combine(fixture.Directory, item.CompletedFileName));
		restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();
		RegressionAssert.Equal("missing-file", restored.ErrorCode);
		RegressionAssert.Equal(String.Empty, restored.ErrorDetails);
		RegressionAssert.False(restored.FailureDetails.Contains("Goon's Library"));
	}

	public void ReconcileRequiresFreshLinkForIncompleteFreeDownload()
	{
		using var fixture = new StoreFixture();
		var item = fixture.Item(NxmDownloadState.Downloading);
		item.RequiresAuthorization = true;
		item.PartialFileName = "asset.zip.part";
		item.SizeBytes = 14;
		File.WriteAllText(Path.Combine(fixture.Directory, item.PartialFileName), "partial");
		fixture.Store.SaveAsync([item]).GetAwaiter().GetResult();

		var restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();

		RegressionAssert.Equal(NxmDownloadState.NeedsFreshLink, restored.State);
		RegressionAssert.Equal(7L, restored.BytesReceived);
		RegressionAssert.Equal(0.5, restored.Progress);
	}

	public void ReconcileMarksMissingCompletedFileAsFailed()
	{
		using var fixture = new StoreFixture();
		var item = fixture.Item(NxmDownloadState.Downloaded);
		item.CompletedFileName = "missing.zip";
		fixture.Store.SaveAsync([item]).GetAwaiter().GetResult();

		var restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();

		RegressionAssert.Equal(NxmDownloadState.Failed, restored.State);
		RegressionAssert.Equal("missing-file", restored.ErrorCode);
	}

	public void ReconcileRejectsCompletedFileWithWrongLength()
	{
		using var fixture = new StoreFixture();
		var item = fixture.Item(NxmDownloadState.Downloaded);
		item.CompletedFileName = "truncated.zip";
		item.SizeBytes = 20;
		File.WriteAllText(Path.Combine(fixture.Directory, item.CompletedFileName), "short");
		fixture.Store.SaveAsync([item]).GetAwaiter().GetResult();

		var restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();

		RegressionAssert.Equal(NxmDownloadState.Failed, restored.State);
		RegressionAssert.Equal("file-size-mismatch", restored.ErrorCode);
	}

	public void ReconcileRejectsCompletedFileWithChangedContents()
	{
		using var fixture = new StoreFixture();
		var item = fixture.Item(NxmDownloadState.Downloaded);
		item.CompletedFileName = "changed.zip";
		item.SizeBytes = 4;
		item.ArchiveSha256 = "9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a";
		File.WriteAllBytes(Path.Combine(fixture.Directory, item.CompletedFileName), [4, 3, 2, 1]);
		fixture.Store.SaveAsync([item]).GetAwaiter().GetResult();

		var restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();

		RegressionAssert.Equal(NxmDownloadState.Failed, restored.State);
		RegressionAssert.Equal("archive-changed", restored.ErrorCode);
	}

	public void ReconcileRecoversInterruptedResolvingAndInstallingStates()
	{
		using var fixture = new StoreFixture();
		var resolving = fixture.Item(NxmDownloadState.Resolving);
		var installing = fixture.Item(NxmDownloadState.Installing);
		installing.Id = Guid.NewGuid().ToString("N");
		installing.QueuePosition = 2;
		installing.CompletedFileName = "ready.zip";
		File.WriteAllText(Path.Combine(fixture.Directory, installing.CompletedFileName), "ready");
		fixture.Store.SaveAsync([resolving, installing]).GetAwaiter().GetResult();

		var restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult();

		RegressionAssert.Equal(NxmDownloadState.Queued, restored.Single(item => item.Id == resolving.Id).State);
		RegressionAssert.Equal(NxmDownloadState.Downloaded, restored.Single(item => item.Id == installing.Id).State);
	}

	public void ReconcileValidatesRetainedArchiveForInstallFailure()
	{
		using var fixture = new StoreFixture();
		var item = fixture.Item((NxmDownloadState)11);
		item.CompletedFileName = "missing.zip";
		fixture.Store.SaveAsync([item]).GetAwaiter().GetResult();

		var restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();

		RegressionAssert.Equal(NxmDownloadState.Failed, restored.State);
		RegressionAssert.Equal("missing-file", restored.ErrorCode);
	}

	public void ReconcileRecoversLegacyRetryWhenCompletedArchiveIsIntact()
	{
		using var fixture = new StoreFixture();
		var item = fixture.Item(NxmDownloadState.Failed);
		item.CompletedFileName = "complete.zip";
		item.PartialFileName = "complete.zip.part";
		item.SizeBytes = 4;
		item.BytesReceived = 4;
		item.ErrorCode = "transfer-failed";
		File.WriteAllBytes(Path.Combine(fixture.Directory, item.CompletedFileName), [1, 2, 3, 4]);
		File.WriteAllBytes(Path.Combine(fixture.Directory, item.PartialFileName), [1, 2, 3, 4]);
		File.WriteAllText(Path.Combine(fixture.Directory, item.PartialFileName + ".meta"), "metadata");
		fixture.Store.SaveAsync([item]).GetAwaiter().GetResult();

		var restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();

		RegressionAssert.Equal(NxmDownloadState.Downloaded, restored.State);
		RegressionAssert.Equal(String.Empty, restored.ErrorCode);
		RegressionAssert.False(File.Exists(Path.Combine(fixture.Directory, item.PartialFileName)));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.Directory, item.PartialFileName + ".meta")));
	}

	public void ReconcilePersistsLegacyRecoveryWhenPartialCleanupFails()
	{
		using var fixture = new StoreFixture();
		var item = fixture.Item(NxmDownloadState.Failed);
		item.CompletedFileName = "complete.zip";
		item.PartialFileName = "complete.zip.part";
		item.SizeBytes = 4;
		item.BytesReceived = 4;
		item.ErrorCode = "transfer-failed";
		File.WriteAllBytes(Path.Combine(fixture.Directory, item.CompletedFileName), [1, 2, 3, 4]);
		File.WriteAllBytes(Path.Combine(fixture.Directory, item.PartialFileName), [1, 2, 3, 4]);
		fixture.Store.SaveAsync([item]).GetAwaiter().GetResult();

		NxmDownloadItem restored;
		using (new FileStream(Path.Combine(fixture.Directory, item.PartialFileName),
			FileMode.Open, FileAccess.Read, FileShare.Read))
		{
			restored = fixture.Store.ReconcileAsync().GetAwaiter().GetResult().Single();
		}

		RegressionAssert.Equal(NxmDownloadState.Downloaded, restored.State);
		RegressionAssert.Equal(NxmDownloadState.Downloaded,
			fixture.Store.LoadAsync().GetAwaiter().GetResult().Single().State);
	}

	public void CorruptManifestIsQuarantinedWithoutBlockingStartup()
	{
		using var fixture = new StoreFixture();
		File.WriteAllText(Path.Combine(fixture.Directory, "downloads.json"), "{not-json");

		var restored = fixture.Store.LoadAsync().GetAwaiter().GetResult();

		RegressionAssert.Equal(0, restored.Count);
		RegressionAssert.False(File.Exists(Path.Combine(fixture.Directory, "downloads.json")));
		RegressionAssert.Equal(1, Directory.GetFiles(fixture.Directory, "downloads.corrupt-*.json").Length);
	}

	public void CorruptManifestRecoversLastKnownGoodBackup()
	{
		using var fixture = new StoreFixture();
		var first = fixture.Item(NxmDownloadState.Paused);
		fixture.Store.SaveAsync([first]).GetAwaiter().GetResult();
		var second = fixture.Item(NxmDownloadState.Failed);
		second.Id = Guid.NewGuid().ToString("N");
		fixture.Store.SaveAsync([second]).GetAwaiter().GetResult();
		File.WriteAllText(Path.Combine(fixture.Directory, "downloads.json"), "{not-json");

		var restored = fixture.Store.LoadAsync().GetAwaiter().GetResult();

		RegressionAssert.Equal(first.Id, restored.Single().Id);
		RegressionAssert.Equal(first.Id, new NxmDownloadStore(fixture.Directory).LoadAsync().GetAwaiter().GetResult().Single().Id);
	}

	private sealed class StoreFixture : IDisposable
	{
		public string Directory { get; } = Path.Combine(Path.GetTempPath(), "ReduxNxmStoreTests", Guid.NewGuid().ToString("N"));
		public NxmDownloadStore Store { get; }

		public StoreFixture()
		{
			System.IO.Directory.CreateDirectory(Directory);
			Store = new NxmDownloadStore(Directory);
		}

		public NxmDownloadItem Item(NxmDownloadState state) => new()
		{
			QueuePosition = 1,
			ModId = 10,
			FileId = 20,
			ProjectName = "Project",
			FileDisplayName = "File",
			State = state
		};

		public void Dispose()
		{
			if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true);
		}
	}
}
