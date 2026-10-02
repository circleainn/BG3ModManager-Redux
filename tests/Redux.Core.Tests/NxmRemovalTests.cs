using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class NxmRemovalTests
{
	public void FailedRemovalIntentSaveLeavesQueueAndFilesUnchanged()
	{
		using var fixture = new RemovalFixture();
		fixture.Store.FailOnWrite = 1;
		RegressionAssert.Throws<IOException>(() => fixture.Manager.RemoveAsync(fixture.Id, true).GetAwaiter().GetResult());
		RegressionAssert.Equal(NxmDownloadState.Downloaded, fixture.Manager.Items.Single().State);
		RegressionAssert.Equal(NxmDownloadState.Downloaded, fixture.Durable.LoadAsync().GetAwaiter().GetResult().Single().State);
		RegressionAssert.True(File.Exists(fixture.Archive));
		RegressionAssert.True(File.Exists(fixture.Partial));
	}

	public void FailedRecycleKeepsDurableRetryRecordAndBlocksDownloadActions()
	{
		using var fixture = new RemovalFixture();
		fixture.Manager.RecycleCompletedFile = _ => throw new IOException("Archive is in use");
		RegressionAssert.Throws<IOException>(() => fixture.Manager.RemoveAsync(fixture.Id, true).GetAwaiter().GetResult());
		var item = fixture.Manager.Items.Single();
		RegressionAssert.True(item.IsRemovalPending);
		RegressionAssert.Equal("Removal incomplete", item.StatusText);
		RegressionAssert.Equal("Retry Delete", item.RemoveActionText);
		RegressionAssert.False(item.CanDownloadAgain);
		fixture.Manager.ResumeAsync(fixture.Id).GetAwaiter().GetResult();
		fixture.Manager.RetryAsync(fixture.Id).GetAwaiter().GetResult();
		fixture.Manager.DownloadAgainAsync(fixture.Id).GetAwaiter().GetResult();
		fixture.Manager.PauseAsync(fixture.Id).GetAwaiter().GetResult();
		fixture.Manager.SetStateAsync(fixture.Id, NxmDownloadState.Downloaded).GetAwaiter().GetResult();
		RegressionAssert.True(item.IsRemovalPending);
		RegressionAssert.True(File.Exists(fixture.Archive));
		var restarted = fixture.Restart();
		RegressionAssert.True(restarted.Items.Single().IsRemovalPending);
		restarted.RemoveAsync(fixture.Id, true).GetAwaiter().GetResult();
		RegressionAssert.Equal(0, restarted.Items.Count);
		RegressionAssert.Equal(0, fixture.Durable.LoadAsync().GetAwaiter().GetResult().Count);
		RegressionAssert.False(File.Exists(fixture.Archive));
		RegressionAssert.False(File.Exists(fixture.Partial));
	}

	public void PartialCleanupFailureCanRetryAfterCompletedArchiveWasRemoved()
	{
		using var fixture = new RemovalFixture();
		using (var held = new FileStream(fixture.Partial, FileMode.Open, FileAccess.Read, FileShare.Read))
		{
			RegressionAssert.Throws<IOException>(() => fixture.Manager.RemoveAsync(fixture.Id, true).GetAwaiter().GetResult());
			RegressionAssert.False(File.Exists(fixture.Archive));
			RegressionAssert.True(File.Exists(fixture.Partial));
			RegressionAssert.True(fixture.Manager.Items.Single().IsRemovalPending);
		}
		fixture.Manager.RemoveAsync(fixture.Id, true).GetAwaiter().GetResult();
		RegressionAssert.Equal(0, fixture.Manager.Items.Count);
		RegressionAssert.False(File.Exists(fixture.Partial));
		RegressionAssert.False(File.Exists(fixture.Partial + ".meta"));
	}

	public void FinalRemovalSaveFailureRemainsRetryableAfterRestart()
	{
		using var fixture = new RemovalFixture();
		fixture.Store.FailOnWrite = 2;
		RegressionAssert.Throws<IOException>(() => fixture.Manager.RemoveAsync(fixture.Id, true).GetAwaiter().GetResult());
		RegressionAssert.True(fixture.Manager.Items.Single().IsRemovalPending);
		RegressionAssert.False(File.Exists(fixture.Archive));
		RegressionAssert.False(File.Exists(fixture.Partial));
		var restarted = fixture.Restart();
		RegressionAssert.True(restarted.Items.Single().IsRemovalPending);
		restarted.RemoveAsync(fixture.Id, true).GetAwaiter().GetResult();
		RegressionAssert.Equal(0, restarted.Items.Count);
		RegressionAssert.Equal(0, fixture.Durable.LoadAsync().GetAwaiter().GetResult().Count);
	}

	private sealed class RemovalFixture : IDisposable
	{
		private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReduxRemovalTests", Guid.NewGuid().ToString("N"));
		public NxmDownloadStore Durable { get; }
		public FailingStore Store { get; }
		public NxmDownloadManager Manager { get; }
		public string Id { get; } = Guid.NewGuid().ToString("N");
		public string Archive => Path.Combine(_directory, "mod.zip");
		public string Partial => Path.Combine(_directory, "mod.part");
		public RemovalFixture()
		{
			Directory.CreateDirectory(_directory);
			File.WriteAllBytes(Archive, [1, 2, 3, 4]);
			File.WriteAllText(Partial, "partial");
			File.WriteAllText(Partial + ".meta", "resume metadata");
			Durable = new NxmDownloadStore(_directory);
			Durable.SaveAsync([new NxmDownloadItem
			{
				Id = Id, SourceKind = AcquiredPackageSourceKind.LocalFile, State = NxmDownloadState.Downloaded,
				CompletedFileName = "mod.zip", PartialFileName = "mod.part", SizeBytes = 4,
				ArchiveSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Archive)))
			}]).GetAwaiter().GetResult();
			Store = new FailingStore(Durable);
			Manager = CreateManager(Store);
			Store.Writes = 0;
		}
		public NxmDownloadManager Restart() => CreateManager(Durable);
		private NxmDownloadManager CreateManager(INxmDownloadStore store)
		{
			var manager = new NxmDownloadManager(_directory, store, new NoNetwork(), new NoNetwork(), 1,
				() => false, (_, _) => Task.FromResult(true));
			manager.RecycleCompletedFile = File.Delete;
			manager.InitializeAsync().GetAwaiter().GetResult();
			return manager;
		}
		public void Dispose() => Directory.Delete(_directory, true);
	}

	private sealed class FailingStore(INxmDownloadStore inner) : INxmDownloadStore
	{
		public int FailOnWrite { get; set; }
		public int Writes { get; set; }
		public Task<IReadOnlyList<NxmDownloadItem>> LoadAsync(CancellationToken token = default) => inner.LoadAsync(token);
		public Task<IReadOnlyList<NxmDownloadItem>> ReconcileAsync(CancellationToken token = default) => inner.ReconcileAsync(token);
		public Task SaveAsync(IEnumerable<NxmDownloadItem> items, CancellationToken token = default)
		{
			if (++Writes == FailOnWrite) throw new IOException("Manifest write failed");
			return inner.SaveAsync(items, token);
		}
	}

	private sealed class NoNetwork : INxmResolverFactory, INxmTransfer
	{
		public Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken token) =>
			throw new InvalidOperationException("Deletion must not resolve a download.");
		public Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken token) =>
			throw new InvalidOperationException("Deletion must not request a download.");
		public Task<NxmTransferResult> DownloadAsync(NxmTransferRequest request, IProgress<DownloadProgress> progress, CancellationToken token) =>
			throw new InvalidOperationException("Deletion must not transfer a download.");
	}
}
