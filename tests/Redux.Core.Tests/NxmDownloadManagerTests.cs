using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

internal sealed class NxmDownloadManagerTests
{
	public void CancelledLocalIntakeAfterCopyRemovesUncommittedInboxFile()
	{
		var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReduxLocalIntakeTests", Guid.NewGuid().ToString("N"));
		var downloads = System.IO.Path.Combine(root, "Downloads");
		System.IO.Directory.CreateDirectory(downloads);
		var source = System.IO.Path.Combine(root, "Example.pak");
		System.IO.File.WriteAllBytes(source, [1, 2, 3, 4]);
		const string sha256 = "9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a";
		using var cancellation = new CancellationTokenSource();
		var store = new ControllableStore();
		var manager = new NxmDownloadManager(downloads, store, new ResolverFactory(),
			new FakeTransfer(), 4, () => false, (_, _) => Task.FromResult(true));
		var copiedBeforeCancellation = false;
		store.BeforeWrite = token =>
		{
			var copied = System.IO.Directory.GetFiles(downloads).Single();
			RegressionAssert.SequenceEqual(new byte[] { 1, 2, 3, 4 }, System.IO.File.ReadAllBytes(copied));
			copiedBeforeCancellation = true;
			cancellation.Cancel();
			token.ThrowIfCancellationRequested();
		};
		try
		{
			RegressionAssert.Throws<OperationCanceledException>(() => manager.AddLocalPackageAsync(
				source, sha256, "Example", "PAK mod", "Inactive Mods", "Ready",
				cancellationToken: cancellation.Token).GetAwaiter().GetResult());
			RegressionAssert.True(copiedBeforeCancellation);
			RegressionAssert.Equal(0, manager.Items.Count);
			RegressionAssert.Equal(0, System.IO.Directory.GetFiles(downloads).Length);
			RegressionAssert.SequenceEqual(new byte[] { 1, 2, 3, 4 }, System.IO.File.ReadAllBytes(source));

			// Cancellation releases both intake/state gates so a later attempt works.
			store.BeforeWrite = null;
			manager.AddLocalPackageAsync(source, sha256, "Example", "PAK mod", "Inactive Mods", "Ready")
				.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
			RegressionAssert.Equal(1, manager.Items.Count);
			RegressionAssert.Equal(1, System.IO.Directory.GetFiles(downloads).Length);
		}
		finally { System.IO.Directory.Delete(root, true); }
	}

	public void RetainedNexusPackageReentersInboxWithPublicSourceIdentity()
	{
		var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReduxLocalIntakeTests", Guid.NewGuid().ToString("N"));
		var downloads = System.IO.Path.Combine(root, "Downloads");
		System.IO.Directory.CreateDirectory(downloads);
		var source = System.IO.Path.Combine(root, "content-addressed.zip");
		System.IO.File.WriteAllBytes(source, [9, 8, 7, 6]);
		var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(source))).ToLowerInvariant();
		var manager = new NxmDownloadManager(downloads, new MemoryStore(), new ResolverFactory(),
			new FakeTransfer(), 4, () => false, (_, _) => Task.FromResult(true));
		try
		{
			manager.AddLocalPackageAsync(source, sha256, "WASD Character Movement", "Game-directory mod",
				"Game-directory Mods", "Ready", AcquiredPackageSourceKind.NexusMods,
				sourceModId: 781, sourceFileId: 4567, sourceFileName: "WASD-v1.2.zip", sourceVersion: "1.2")
				.GetAwaiter().GetResult();

			var item = manager.Items.Single();
			RegressionAssert.Equal(AcquiredPackageSourceKind.NexusMods, item.SourceKind);
			RegressionAssert.Equal(781L, item.ModId);
			RegressionAssert.Equal(4567L, item.FileId);
			RegressionAssert.Equal("WASD-v1.2.zip", item.SourceFileName);
			RegressionAssert.Equal("1.2", item.Version);
			RegressionAssert.True(item.Authorization == null);
		}
		finally
		{
			if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
		}
	}

	public void LocalPackageIsCopiedHashedAndDeduplicatedInTheSharedInbox()
	{
		var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReduxLocalIntakeTests", Guid.NewGuid().ToString("N"));
		var downloads = System.IO.Path.Combine(root, "Downloads");
		System.IO.Directory.CreateDirectory(downloads);
		var source = System.IO.Path.Combine(root, "Example Mod.pak");
		System.IO.File.WriteAllBytes(source, [1, 2, 3, 4]);
		var manager = new NxmDownloadManager(downloads, new MemoryStore(), new ResolverFactory(),
			new FakeTransfer(), 4, () => false, (_, _) => Task.FromResult(true));
		try
		{
			var first = manager.AddLocalPackageAsync(source, "9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a",
				"Example Mod", "PAK mod", "Inactive Mods", "Ready for Inactive Mods",
				thumbnailUrl: "https://static.example.test/example-mod.png").GetAwaiter().GetResult();
			manager.SetInstalledAsync(first, "Inactive Mods").GetAwaiter().GetResult();
			System.IO.File.Delete(System.IO.Path.Combine(downloads, manager.Items.Single().CompletedFileName));
			var second = manager.AddLocalPackageAsync(source, "9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a",
				"Example Mod", "PAK mod", "Inactive Mods", "Ready for Inactive Mods",
				thumbnailUrl: "https://static.example.test/example-mod-updated.png").GetAwaiter().GetResult();

			var item = manager.Items.Single();
			RegressionAssert.Equal(first, second);
			RegressionAssert.Equal(AcquiredPackageSourceKind.LocalFile, item.SourceKind);
			RegressionAssert.Equal(NxmDownloadState.Downloaded, item.State);
			RegressionAssert.Equal("Inactive Mods", item.DetectedDestination);
			RegressionAssert.Equal("https://static.example.test/example-mod-updated.png", item.ThumbnailUrl);
			RegressionAssert.True(item.InstalledAt == null);
			RegressionAssert.True(item.InspectionCompleted);
			RegressionAssert.True(System.IO.File.Exists(System.IO.Path.Combine(downloads, item.CompletedFileName)));
		}
		finally
		{
			if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
		}
	}

	public void UnsafeLocalPackageRemainsVisibleButCannotEnterInstallState()
	{
		var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReduxLocalIntakeTests", Guid.NewGuid().ToString("N"));
		var downloads = System.IO.Path.Combine(root, "Downloads");
		System.IO.Directory.CreateDirectory(downloads);
		var source = System.IO.Path.Combine(root, "Unknown Native.zip");
		System.IO.File.WriteAllBytes(source, [5, 6, 7]);
		var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
			System.IO.File.ReadAllBytes(source))).ToLowerInvariant();
		var manager = new NxmDownloadManager(downloads, new MemoryStore(), new ResolverFactory(),
			new FakeTransfer(), 4, () => false, (_, _) => Task.FromResult(true));
		try
		{
			manager.AddLocalPackageAsync(source, sha256,
				"Unknown Native", "Unreviewed native package", String.Empty,
				"Redux cannot safely determine the destination.").GetAwaiter().GetResult();

			var item = manager.Items.Single();
			RegressionAssert.Equal(NxmDownloadState.NeedsReview, item.State);
			RegressionAssert.Equal("unsupported-layout", item.ErrorCode);
			RegressionAssert.Equal(String.Empty, item.DetectedDestination);
		}
		finally
		{
			if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
		}
	}

	public void DuplicateLinkFocusesExistingItem()
	{
		var fixture = new ManagerFixture();
		var link = new NexusModManagerLink(10, 20, "key", 4_000_000_000, 7);
		var first = fixture.Manager.EnqueueAsync(link).GetAwaiter().GetResult();
		var focused = String.Empty;
		fixture.Manager.FocusRequested += id => focused = id;

		var second = fixture.Manager.EnqueueAsync(link).GetAwaiter().GetResult();

		RegressionAssert.Equal(first, second);
		RegressionAssert.Equal(first, focused);
		RegressionAssert.Equal(1, fixture.Manager.Items.Count);
	}

	public void ResolvedItemsDownloadWithoutBlockingIngress()
	{
		var fixture = new ManagerFixture();
		for (var i = 1; i <= 6; i++) fixture.Manager.EnqueueAsync(new NexusModManagerLink(i, i, null!, null, null)).GetAwaiter().GetResult();
		SpinWait.SpinUntil(() => fixture.Manager.Items.Count(item => item.State == NxmDownloadState.Downloaded) == 6, 5000);

		RegressionAssert.Equal(6, fixture.Manager.Items.Count);
		RegressionAssert.True(fixture.Transfer.MaximumActive <= 4);
		RegressionAssert.Equal(6, fixture.Manager.Items.Count(item => item.State == NxmDownloadState.Downloaded));
	}

	public void RemovingResolvingItemCancelsItBeforeTransfer()
	{
		var transfer = new FakeTransfer();
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), new BlockingResolverFactory(), transfer, 4,
			() => false, (_, _) => Task.FromResult(true));
		var id = manager.EnqueueAsync(new NexusModManagerLink(40, 50, null!, null, null)).GetAwaiter().GetResult();

		manager.RemoveAsync(id, false).GetAwaiter().GetResult();
		Thread.Sleep(50);

		RegressionAssert.Equal(0, manager.Items.Count);
		RegressionAssert.Equal(0, transfer.CallCount);
	}

	public void CancelWinsRaceWithTransferCompletion()
	{
		var transfer = new CompletingTransfer();
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), new ResolverFactory(), transfer, 4,
			() => false, (_, _) => Task.FromResult(true));
		var id = manager.EnqueueAsync(new NexusModManagerLink(60, 70, null!, null, null)).GetAwaiter().GetResult();
		RegressionAssert.True(transfer.Started.Wait(5000));

		var cancel = Task.Run(() => manager.CancelAsync(id));
		RegressionAssert.False(cancel.IsCompleted);
		transfer.Release.Set();
		cancel.GetAwaiter().GetResult();
		var item = manager.Items.Single();

		RegressionAssert.Equal(NxmDownloadState.Failed, item.State);
		RegressionAssert.False(System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.GetTempPath(), item.CompletedFileName)));
	}

	public void HttpProgressTotalReplacesMetadataEstimate()
	{
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), new ResolverFactory(),
			new ReportingTransfer(), 4, () => false, (_, _) => Task.FromResult(true));
		manager.EnqueueAsync(new NexusModManagerLink(80, 90, null!, null, null)).GetAwaiter().GetResult();
		RegressionAssert.True(SpinWait.SpinUntil(() => manager.Items.Single().State == NxmDownloadState.Downloaded, 5000));

		RegressionAssert.Equal(100L, manager.Items.Single().SizeBytes);
		RegressionAssert.Equal(100L, manager.Items.Single().BytesReceived);
	}

	public void PauseWaitsForTheOwnedTransferAndRejectsItsLateCompletion()
	{
		var transfer = new CompletingTransfer();
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), new ResolverFactory(), transfer, 4,
			() => false, (_, _) => Task.FromResult(true));
		var id = manager.EnqueueAsync(new NexusModManagerLink(81, 91, null!, null, null)).GetAwaiter().GetResult();
		RegressionAssert.True(transfer.Started.Wait(5000));

		var pause = manager.PauseAsync(id);

		RegressionAssert.False(pause.IsCompleted);
		transfer.Release.Set();
		RegressionAssert.True(SpinWait.SpinUntil(() => pause.IsCompleted, 5000));
		pause.GetAwaiter().GetResult();
		RegressionAssert.Equal(NxmDownloadState.Paused, manager.Items.Single().State);
	}

	public void DisablingNetworkWaitsForTransfersAndReenableDoesNotResumeThem()
	{
		var transfer = new CompletingTransfer();
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), new ResolverFactory(), transfer, 4,
			() => false, (_, _) => Task.FromResult(true));
		manager.EnqueueAsync(new NexusModManagerLink(82, 92, null!, null, null)).GetAwaiter().GetResult();
		RegressionAssert.True(transfer.Started.Wait(5000));

		var disable = manager.SetNetworkEnabledAsync(false);

		RegressionAssert.False(disable.IsCompleted);
		transfer.Release.Set();
		disable.GetAwaiter().GetResult();
		manager.SetNetworkEnabledAsync(true).GetAwaiter().GetResult();
		RegressionAssert.Equal(NxmDownloadState.Paused, manager.Items.Single().State);
		RegressionAssert.Equal(1, transfer.CallCount);
	}

	public void EnqueueSaveFailureDoesNotPublishTheItem()
	{
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new ThrowingStore(), new ResolverFactory(),
			new FakeTransfer(), 4, () => false, (_, _) => Task.FromResult(true));

		RegressionAssert.Throws<System.IO.IOException>(() => manager.EnqueueAsync(
			new NexusModManagerLink(83, 93, null!, null, null)).GetAwaiter().GetResult());

		RegressionAssert.Equal(0, manager.Items.Count);
	}

	public void ReservedWindowsFilenameUsesAStableSafeName()
	{
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), new UnsafeFilenameResolverFactory(),
			new FakeTransfer(), 4, () => false, (_, _) => Task.FromResult(true));

		manager.EnqueueAsync(new NexusModManagerLink(84, 94, null!, null, null)).GetAwaiter().GetResult();
		RegressionAssert.True(SpinWait.SpinUntil(() => manager.Items.Single().State == NxmDownloadState.Downloaded, 5000));

		RegressionAssert.Equal("Nexus-84-94.zip", manager.Items.Single().CompletedFileName);
	}

	public void InitializeRestartsPersistedQueuedDownload()
	{
		var queued = new NxmDownloadItem
		{
			QueuePosition = 1,
			ModId = 85,
			FileId = 95,
			State = NxmDownloadState.Queued,
			CompletedFileName = $"manager-restart-{Guid.NewGuid():N}.zip",
			PartialFileName = $"manager-restart-{Guid.NewGuid():N}.zip.part"
		};
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore([queued]), new ResolverFactory(),
			new FakeTransfer(), 4, () => false, (_, _) => Task.FromResult(true));

		manager.InitializeAsync().GetAwaiter().GetResult();

		RegressionAssert.True(SpinWait.SpinUntil(() => manager.Items.Single().State == NxmDownloadState.Downloaded, 5000));
	}

	public void FailedPauseSaveDoesNotPublishPausedState()
	{
		var store = new ControllableStore();
		var transfer = new CompletingTransfer();
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), store, new ResolverFactory(), transfer, 4,
			() => false, (_, _) => Task.FromResult(true));
		var id = manager.EnqueueAsync(new NexusModManagerLink(86, 96, null!, null, null)).GetAwaiter().GetResult();
		RegressionAssert.True(transfer.Started.Wait(5000));
		store.FailWrites = true;

		var pause = manager.PauseAsync(id);
		transfer.Release.Set();
		RegressionAssert.Throws<System.IO.IOException>(() => pause.GetAwaiter().GetResult());

		RegressionAssert.Equal(NxmDownloadState.Downloading, manager.Items.Single().State);
	}

	public void PermanentTransferFailureDoesNotEnterRetryLoop()
	{
		var transfer = new InvalidTransfer();
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), new ResolverFactory(), transfer, 4,
			() => false, (_, _) => Task.FromResult(true));

		manager.EnqueueAsync(new NexusModManagerLink(87, 97, null!, null, null)).GetAwaiter().GetResult();

		RegressionAssert.True(SpinWait.SpinUntil(() => manager.Items.Single().State is NxmDownloadState.Failed or NxmDownloadState.RetryWaiting, 5000));
		RegressionAssert.Equal(NxmDownloadState.Failed, manager.Items.Single().State);
		RegressionAssert.Equal(1, transfer.CallCount);
	}

	public void MetadataCompletionOrderDoesNotChangeQueueFifo()
	{
		var resolver = new OutOfOrderResolverFactory();
		var transfer = new RecordingTransfer();
		// One slot measures FIFO dispatch. Concurrent transfers can reach their
		// HTTP callback in either order after asynchronous preparation.
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), resolver, transfer, 1,
			() => false, (_, _) => Task.FromResult(true));

		manager.EnqueueAsync(new NexusModManagerLink(1, 101, null!, null, null)).GetAwaiter().GetResult();
		manager.EnqueueAsync(new NexusModManagerLink(2, 102, null!, null, null)).GetAwaiter().GetResult();
		RegressionAssert.True(resolver.SecondResolved.Wait(5000));
		Thread.Sleep(50);
		RegressionAssert.Equal(0, transfer.CallCount);
		resolver.ReleaseFirst.Set();
		RegressionAssert.True(SpinWait.SpinUntil(() => transfer.CallCount == 2, 5000));

		RegressionAssert.Equal("1,2", transfer.Sequence);
	}

	public void ShutdownRejectsLateEnqueue()
	{
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), new ResolverFactory(),
			new FakeTransfer(), 4, () => false, (_, _) => Task.FromResult(true));

		manager.ShutdownAsync().GetAwaiter().GetResult();

		RegressionAssert.Throws<InvalidOperationException>(() => manager.EnqueueAsync(
			new NexusModManagerLink(88, 98, null!, null, null)).GetAwaiter().GetResult());
	}

	public void FreshLinkSaveFailureDoesNotPublishResolvingState()
	{
		var item = new NxmDownloadItem
		{
			QueuePosition = 1,
			ModId = 89,
			FileId = 99,
			State = NxmDownloadState.NeedsFreshLink,
			RequiresAuthorization = true
		};
		var store = new ControllableStore([item]);
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), store, new ResolverFactory(), new FakeTransfer(), 4,
			() => false, (_, _) => Task.FromResult(true));
		manager.InitializeAsync().GetAwaiter().GetResult();
		store.FailWrites = true;

		RegressionAssert.Throws<System.IO.IOException>(() => manager.EnqueueAsync(
			new NexusModManagerLink(89, 99, "fresh", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(), 1)).GetAwaiter().GetResult());

		RegressionAssert.Equal(NxmDownloadState.NeedsFreshLink, manager.Items.Single().State);
	}

	public void ResolvedMetadataIsDurableBeforeConfirmationCompletes()
	{
		var store = new MemoryStore();
		var confirmationEntered = new ManualResetEventSlim(false);
		var confirmationResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), store, new FreeUserMetadataResolverFactory(),
			new FakeTransfer(), 4, () => true, async (_, _) =>
			{
				confirmationEntered.Set();
				return await confirmationResult.Task;
			});

		manager.EnqueueAsync(new NexusModManagerLink(91, 101, "fresh", 4_000_000_000, 7)).GetAwaiter().GetResult();
		RegressionAssert.True(confirmationEntered.Wait(5000));
		var live = manager.Items.Single();
		var saved = store.LoadAsync().GetAwaiter().GetResult().Single();

		RegressionAssert.Equal("Cached project", live.ProjectName);
		RegressionAssert.Equal("cached-file.zip", live.FileName);
		RegressionAssert.Equal("https://static.example.test/cached.webp", live.ThumbnailUrl);
		RegressionAssert.Equal("Cached project", saved.ProjectName);
		RegressionAssert.Equal("cached-file.zip", saved.FileName);
		RegressionAssert.Equal(NxmDownloadState.Resolving, saved.State);

		confirmationResult.SetResult(false);
		RegressionAssert.True(SpinWait.SpinUntil(() => manager.Items.Count == 0, 5000));
		manager.ShutdownAsync().GetAwaiter().GetResult();
	}

	public void InitializeHydratesLegacyPlaceholderWithoutChangingFreshLinkState()
	{
		var legacy = new NxmDownloadItem
		{
			QueuePosition = 1,
			ModId = 91,
			FileId = 101,
			ProjectName = "Nexus mod 91",
			FileDisplayName = "File 101",
			State = NxmDownloadState.NeedsFreshLink,
			RequiresAuthorization = true
		};
		var store = new MemoryStore([legacy]);
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), store, new FreeUserMetadataResolverFactory(),
			new FakeTransfer(), 4, () => false, (_, _) => Task.FromResult(true));

		manager.InitializeAsync().GetAwaiter().GetResult();

		var hydrated = manager.Items.Single();
		RegressionAssert.Equal(NxmDownloadState.NeedsFreshLink, hydrated.State);
		RegressionAssert.Equal("Cached project", hydrated.ProjectName);
		RegressionAssert.Equal("Cached file", hydrated.FileDisplayName);
		RegressionAssert.Equal("cached-file.zip", hydrated.FileName);
		RegressionAssert.Equal("https://static.example.test/cached.webp", hydrated.ThumbnailUrl);
		RegressionAssert.True(hydrated.RequiresAuthorization);
		manager.ShutdownAsync().GetAwaiter().GetResult();
	}

	public void FailedShutdownCanBeRetriedUntilPausedStateIsDurable()
	{
		var store = new ControllableStore();
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), store, new BlockingResolverFactory(), new FakeTransfer(), 4,
			() => false, (_, _) => Task.FromResult(true));
		manager.EnqueueAsync(new NexusModManagerLink(90, 100, null!, null, null)).GetAwaiter().GetResult();
		store.FailWrites = true;

		RegressionAssert.Throws<System.IO.IOException>(() => manager.ShutdownAsync().GetAwaiter().GetResult());
		store.FailWrites = false;
		manager.ShutdownAsync().GetAwaiter().GetResult();

		RegressionAssert.Equal(NxmDownloadState.Paused, manager.Items.Single().State);
	}

	public void ShutdownAfterCompletedInstallDoesNotWaitForOperationCleanup()
	{
		var resolver = new LingeringResolverFactory();
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), resolver, new FakeTransfer(), 4,
			() => false, (_, _) => Task.FromResult(true));
		var id = manager.EnqueueAsync(new NexusModManagerLink(92, 102, null!, null, null)).GetAwaiter().GetResult();
		RegressionAssert.True(resolver.Started.Wait(5000));
		manager.SetInstalledAsync(id, "Inactive Mods").GetAwaiter().GetResult();

		RegressionAssert.True(manager.ShutdownAsync().Wait(TimeSpan.FromSeconds(2)));

		RegressionAssert.Equal(NxmDownloadState.Installed, manager.Items.Single().State);
		RegressionAssert.Equal("Installed", manager.Items.Single().StatusText);
		resolver.Release.Set();
		manager.DrainAsync().GetAwaiter().GetResult();
	}

	public void ClearingInstalledHistoryKeepsOtherQueueItems()
	{
		var installed = new NxmDownloadItem { ModId = 93, FileId = 103, State = NxmDownloadState.Installed };
		var pending = new NxmDownloadItem { ModId = 94, FileId = 104, State = NxmDownloadState.Downloaded };
		var store = new MemoryStore([installed, pending]);
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), store, new ResolverFactory(), new FakeTransfer(), 4,
			() => false, (_, _) => Task.FromResult(true));
		manager.InitializeAsync().GetAwaiter().GetResult();

		manager.ClearInstalledHistoryAsync().GetAwaiter().GetResult();

		RegressionAssert.Equal(1, manager.Items.Count);
		RegressionAssert.Equal(94L, manager.Items.Single().ModId);
		RegressionAssert.Equal(1, store.LoadAsync().GetAwaiter().GetResult().Count);
		manager.ShutdownAsync().GetAwaiter().GetResult();
	}

	public void QueueStatesHaveHumanReadableLabels()
	{
		var item = new NxmDownloadItem { State = NxmDownloadState.RetryWaiting };
		var statusProperty = typeof(NxmDownloadItem).GetProperty("StatusText");

		RegressionAssert.True(statusProperty != null);
		RegressionAssert.Equal("Waiting to retry", statusProperty!.GetValue(item));
	}

	public void InstallFailuresHaveDedicatedHumanReadableState()
	{
		var state = (NxmDownloadState)11;
		var item = new NxmDownloadItem { State = state };

		RegressionAssert.Equal("InstallFailed", Enum.GetName(state));
		RegressionAssert.Equal("Installation failed", item.StatusText);
	}

	public void RetainedPakReinstallAdvertisesPlacementPreservation()
	{
		var item = new NxmDownloadItem
		{
			State = NxmDownloadState.Installed,
			DetectedDestination = "Inactive Mods",
			HasAvailableArchive = true
		};

		RegressionAssert.Equal("Reinstall", item.InstallActionText);
		RegressionAssert.Contains(item.InstallActionToolTip, "active or inactive state");
	}

	public void FreshPakInstallExplainsThatUpdatesKeepTheirPlacement()
	{
		var item = new NxmDownloadItem
		{
			State = NxmDownloadState.Downloaded,
			DetectedDestination = "Inactive Mods"
		};

		RegressionAssert.Equal("Install", item.InstallActionText);
		RegressionAssert.Contains(item.InstallActionToolTip, "New mods go to Inactive Mods");
		RegressionAssert.Contains(item.InstallActionToolTip, "Updates preserve");
	}

	public void DownloadAgainPreservesTheArchiveAndUsesFreshAuthorizationWhenRequired()
	{
		foreach (var originalState in new[] { NxmDownloadState.InstallFailed, NxmDownloadState.Installed })
		foreach (var requiresAuthorization in new[] { false, true })
		{
			var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReduxRedownloadTests", Guid.NewGuid().ToString("N"));
			System.IO.Directory.CreateDirectory(directory);
			var item = new NxmDownloadItem
			{
				ModId = 17535, FileId = 129059, State = originalState, InstalledAt = DateTimeOffset.UtcNow,
				FileName = "previous.zip", CompletedFileName = "previous.zip", PartialFileName = "previous.zip.part",
				RequiresAuthorization = requiresAuthorization, ErrorCode = "install-failed", ErrorDetails = "old failure",
				ETag = "old-etag", BytesReceived = 4, RetryCount = 3
			};
			var oldPath = System.IO.Path.Combine(directory, item.CompletedFileName);
			System.IO.File.WriteAllText(oldPath, "retained archive");
			var transfer = new CompletingTransfer();
			transfer.Release.Set();
			var manager = new NxmDownloadManager(directory, new MemoryStore([item]), new ResolverFactory(), transfer, 4,
				() => false, (_, _) => Task.FromResult(true));
			try
			{
				manager.InitializeAsync().GetAwaiter().GetResult();
				manager.DownloadAgainAsync(item.Id).GetAwaiter().GetResult();
				if (requiresAuthorization)
				{
					RegressionAssert.Equal(NxmDownloadState.NeedsFreshLink, item.State);
					RegressionAssert.Equal(0, transfer.CallCount);
					RegressionAssert.Equal(0L, item.BytesReceived);
					RegressionAssert.Equal(String.Empty, item.ETag);
					manager.EnqueueAsync(new NexusModManagerLink(item.ModId, item.FileId, "fresh", 4_000_000_000, 7)).GetAwaiter().GetResult();
				}
				RegressionAssert.True(SpinWait.SpinUntil(() => item.State == NxmDownloadState.Downloaded, 5000));
				RegressionAssert.Equal("retained archive", System.IO.File.ReadAllText(oldPath));
				RegressionAssert.False(item.CompletedFileName == "previous.zip");
				RegressionAssert.True(System.IO.File.Exists(System.IO.Path.Combine(directory, item.CompletedFileName)));
				RegressionAssert.Equal(1, manager.Items.Count);
				RegressionAssert.True(item.InstalledAt == null);
				RegressionAssert.Equal(String.Empty, item.ErrorDetails);
			}
			finally
			{
				manager.ShutdownAsync().GetAwaiter().GetResult();
				System.IO.Directory.Delete(directory, true);
			}
		}
	}

	public void FailedRedownloadSavePreservesTheOriginalQueueRecord()
	{
		var item = new NxmDownloadItem
		{
			ModId = 17535, FileId = 129059, State = NxmDownloadState.InstallFailed,
			CompletedFileName = "unchanged.zip", ErrorDetails = "Missing Goon's Library"
		};
		var store = new ControllableStore([item]);
		var transfer = new FakeTransfer();
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), store, new ResolverFactory(), transfer, 4,
			() => false, (_, _) => Task.FromResult(true));
		manager.InitializeAsync().GetAwaiter().GetResult();
		store.FailWrites = true;
		RegressionAssert.Throws<System.IO.IOException>(() => manager.DownloadAgainAsync(item.Id).GetAwaiter().GetResult());
		RegressionAssert.Equal(NxmDownloadState.InstallFailed, item.State);
		RegressionAssert.Equal("unchanged.zip", item.CompletedFileName);
		RegressionAssert.Equal("Missing Goon's Library", item.ErrorDetails);
		RegressionAssert.Equal(0, transfer.CallCount);
	}

	public void DownloadAgainNeverReusesExistingPartialData()
	{
		var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReduxRedownloadTests", Guid.NewGuid().ToString("N"));
		System.IO.Directory.CreateDirectory(directory);
		var item = new NxmDownloadItem
		{
			ModId = 17535, FileId = 129059, State = NxmDownloadState.Failed,
			FileName = "previous.zip", CompletedFileName = "previous.zip", PartialFileName = "previous.zip.part"
		};
		var oldPartial = System.IO.Path.Combine(directory, item.PartialFileName);
		System.IO.File.WriteAllText(oldPartial, "damaged partial data");
		System.IO.File.WriteAllText(oldPartial + ".meta", "old validator");
		var transfer = new CompletingTransfer();
		transfer.Release.Set();
		var manager = new NxmDownloadManager(directory, new MemoryStore([item]), new StableFilenameResolverFactory(), transfer, 4,
			() => false, (_, _) => Task.FromResult(true));
		try
		{
			manager.InitializeAsync().GetAwaiter().GetResult();
			manager.DownloadAgainAsync(item.Id).GetAwaiter().GetResult();
			var reserved = item.CompletedFileName;
			RegressionAssert.True(SpinWait.SpinUntil(() => item.State == NxmDownloadState.Downloaded, 5000));
			RegressionAssert.Equal(reserved, item.CompletedFileName);
			RegressionAssert.False(transfer.LastPartialPath == oldPartial);
			RegressionAssert.False(System.IO.File.Exists(transfer.LastPartialPath));
			RegressionAssert.Equal("damaged partial data", System.IO.File.ReadAllText(oldPartial));
			RegressionAssert.Equal("old validator", System.IO.File.ReadAllText(oldPartial + ".meta"));
		}
		finally
		{
			manager.ShutdownAsync().GetAwaiter().GetResult();
			System.IO.Directory.Delete(directory, true);
		}
	}

	public void RedownloadAfterMetadataFailureUsesResolvedPakExtension()
	{
		var item = new NxmDownloadItem { ModId = 17535, FileId = 129059, State = NxmDownloadState.Failed };
		var manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore([item]),
			new StableFilenameResolverFactory("standalone.pak"), new FakeTransfer(), 4,
			() => false, (_, _) => Task.FromResult(true));
		try
		{
			manager.InitializeAsync().GetAwaiter().GetResult();
			manager.DownloadAgainAsync(item.Id).GetAwaiter().GetResult();
			RegressionAssert.True(SpinWait.SpinUntil(() => item.State == NxmDownloadState.Downloaded, 5000));
			RegressionAssert.Equal(".pak", System.IO.Path.GetExtension(item.CompletedFileName));
		}
		finally { manager.ShutdownAsync().GetAwaiter().GetResult(); }
	}

	private sealed class StableFilenameResolverFactory(string filename = "previous.zip") : INxmResolverFactory
	{
		public Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new NxmDownloadDescriptor(link.ModId, link.FileId, "Mod", "Author", "File", filename, "1", 4, false));
		public Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new Uri("https://example.test/file"));
	}

	private sealed class FreeUserMetadataResolverFactory : INxmResolverFactory
	{
		public Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new NxmDownloadDescriptor(link.ModId, link.FileId, "Cached project", "Cached author",
				"Cached file", "cached-file.zip", "2.0", 4096, true, "https://static.example.test/cached.webp"));
		public Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new Uri("https://example.test/file"));
	}

	private sealed class ManagerFixture
	{
		public FakeTransfer Transfer { get; } = new();
		public NxmDownloadManager Manager { get; }
		public ManagerFixture()
		{
			Manager = new NxmDownloadManager(System.IO.Path.GetTempPath(), new MemoryStore(), new ResolverFactory(), Transfer, 4,
				() => false, (_, _) => Task.FromResult(true));
		}
	}

	private sealed class ResolverFactory : INxmResolverFactory
	{
		public Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new NxmDownloadDescriptor(link.ModId, link.FileId, $"Mod {link.ModId}", "Author", $"File {link.FileId}",
				$"manager-test-{Guid.NewGuid():N}.zip", "1", 4, false));
		public Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new Uri("https://example.test/file"));
	}

	private sealed class BlockingResolverFactory : INxmResolverFactory
	{
		public async Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken)
		{
			await Task.Delay(Timeout.Infinite, cancellationToken);
			throw new InvalidOperationException();
		}
		public Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new Uri("https://example.test/file"));
	}

	private sealed class LingeringResolverFactory : INxmResolverFactory
	{
		public ManualResetEventSlim Started { get; } = new(false);
		public ManualResetEventSlim Release { get; } = new(false);

		public async Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken)
		{
			Started.Set();
			await Task.Run(() => Release.Wait());
			return new NxmDownloadDescriptor(link.ModId, link.FileId, "Mod", "Author", "File", "mod.zip", "1", 4, false);
		}

		public Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new Uri("https://example.test/file"));
	}

	private sealed class UnsafeFilenameResolverFactory : INxmResolverFactory
	{
		public Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new NxmDownloadDescriptor(link.ModId, link.FileId, "Mod", "Author", "File",
				"CON.zip", "1", 4, false));
		public Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new Uri("https://example.test/file"));
	}

	private sealed class OutOfOrderResolverFactory : INxmResolverFactory
	{
		public ManualResetEventSlim SecondResolved { get; } = new(false);
		public ManualResetEventSlim ReleaseFirst { get; } = new(false);
		public Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken)
		{
			if (link.ModId == 1) ReleaseFirst.Wait(cancellationToken);
			else SecondResolved.Set();
			return Task.FromResult(new NxmDownloadDescriptor(link.ModId, link.FileId, $"Mod {link.ModId}", "Author", "File",
				$"fifo-{link.ModId}-{Guid.NewGuid():N}.zip", "1", 4, false));
		}
		public Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken) =>
			Task.FromResult(new Uri($"https://example.test/{link.ModId}"));
	}

	private sealed class FakeTransfer : INxmTransfer
	{
		private int _active;
		public int MaximumActive { get; private set; }
		public int CallCount => Volatile.Read(ref _callCount);
		public async Task<NxmTransferResult> DownloadAsync(NxmTransferRequest request, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _callCount);
			var active = Interlocked.Increment(ref _active);
			MaximumActive = Math.Max(MaximumActive, active);
			try { await Task.Delay(25, cancellationToken); }
			finally { Interlocked.Decrement(ref _active); }
			return new NxmTransferResult(request.CompletedPath, 4, "\"tag\"", null, false);
		}
		private int _callCount;
	}

	private sealed class CompletingTransfer : INxmTransfer
	{
		public string LastPartialPath { get; private set; } = String.Empty;
		private int _callCount;
		public ManualResetEventSlim Started { get; } = new(false);
		public ManualResetEventSlim Release { get; } = new(false);
		public int CallCount => Volatile.Read(ref _callCount);
		public Task<NxmTransferResult> DownloadAsync(NxmTransferRequest request, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _callCount);
			Started.Set();
			Release.Wait();
			LastPartialPath = request.PartialPath;
			System.IO.File.WriteAllBytes(request.CompletedPath, [1, 2, 3, 4]);
			return Task.FromResult(new NxmTransferResult(request.CompletedPath, 4, "\"tag\"", null, false));
		}
	}

	private sealed class ReportingTransfer : INxmTransfer
	{
		public Task<NxmTransferResult> DownloadAsync(NxmTransferRequest request, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
		{
			progress.Report(new DownloadProgress(50, 100));
			progress.Report(new DownloadProgress(100, 100));
			return Task.FromResult(new NxmTransferResult(request.CompletedPath, 100, "\"tag\"", null, false));
		}
	}

	private sealed class MemoryStore : INxmDownloadStore
	{
		private List<NxmDownloadItem> _items = new();
		public MemoryStore() { }
		public MemoryStore(IEnumerable<NxmDownloadItem> items) => _items = items.ToList();
		public Task<IReadOnlyList<NxmDownloadItem>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NxmDownloadItem>>(_items);
		public Task<IReadOnlyList<NxmDownloadItem>> ReconcileAsync(CancellationToken cancellationToken = default) => LoadAsync(cancellationToken);
		public Task SaveAsync(IEnumerable<NxmDownloadItem> items, CancellationToken cancellationToken = default)
		{
			_items = items.ToList();
			return Task.CompletedTask;
		}
	}

	private sealed class InvalidTransfer : INxmTransfer
	{
		private int _callCount;
		public int CallCount => Volatile.Read(ref _callCount);
		public Task<NxmTransferResult> DownloadAsync(NxmTransferRequest request, IProgress<DownloadProgress> progress,
			CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _callCount);
			return Task.FromException<NxmTransferResult>(new System.IO.InvalidDataException("invalid response"));
		}
	}

	private sealed class RecordingTransfer : INxmTransfer
	{
		private readonly List<long> _modIds = new();
		public int CallCount { get { lock (_modIds) return _modIds.Count; } }
		public string Sequence { get { lock (_modIds) return String.Join(',', _modIds); } }
		public Task<NxmTransferResult> DownloadAsync(NxmTransferRequest request, IProgress<DownloadProgress> progress,
			CancellationToken cancellationToken)
		{
			lock (_modIds) _modIds.Add(Int64.Parse(request.DownloadUri.AbsolutePath.Trim('/')));
			return Task.FromResult(new NxmTransferResult(request.CompletedPath, 4, null!, null, false));
		}
	}

	private sealed class ControllableStore : INxmDownloadStore
	{
		private List<NxmDownloadItem> _items = new();
		public ControllableStore() { }
		public ControllableStore(IEnumerable<NxmDownloadItem> items) => _items = items.ToList();
		public bool FailWrites { get; set; }
		public Action<CancellationToken>? BeforeWrite { get; set; }
		public Task<IReadOnlyList<NxmDownloadItem>> LoadAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<NxmDownloadItem>>(_items);
		public Task<IReadOnlyList<NxmDownloadItem>> ReconcileAsync(CancellationToken cancellationToken = default) => LoadAsync(cancellationToken);
		public Task SaveAsync(IEnumerable<NxmDownloadItem> items, CancellationToken cancellationToken = default)
		{
			if (FailWrites) return Task.FromException(new System.IO.IOException("simulated manifest failure"));
			BeforeWrite?.Invoke(cancellationToken);
			_items = items.ToList();
			return Task.CompletedTask;
		}
	}

	private sealed class ThrowingStore : INxmDownloadStore
	{
		public Task<IReadOnlyList<NxmDownloadItem>> LoadAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<NxmDownloadItem>>(Array.Empty<NxmDownloadItem>());
		public Task<IReadOnlyList<NxmDownloadItem>> ReconcileAsync(CancellationToken cancellationToken = default) =>
			LoadAsync(cancellationToken);
		public Task SaveAsync(IEnumerable<NxmDownloadItem> items, CancellationToken cancellationToken = default) =>
			Task.FromException(new System.IO.IOException("simulated manifest failure"));
	}
}
