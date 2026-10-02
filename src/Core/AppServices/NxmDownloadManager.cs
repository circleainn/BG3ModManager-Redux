using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using System.Collections.ObjectModel;

namespace DivinityModManager.AppServices;

public interface INxmResolverFactory
{
	Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken);
	Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken);
}

public interface INxmDownloadManager
{
	ReadOnlyObservableCollection<NxmDownloadItem> Items { get; }
	event Action<string> FocusRequested;
	event Action<NxmDownloadItem> ItemChanged;
	Task InitializeAsync(CancellationToken cancellationToken = default);
	Task<string> EnqueueAsync(NexusModManagerLink link, CancellationToken cancellationToken = default);
	Task<string> AddLocalPackageAsync(string sourcePath, string archiveSha256, string projectName,
		string contentKind, string destination, string inspectionSummary,
		AcquiredPackageSourceKind sourceKind = AcquiredPackageSourceKind.LocalFile,
		string thumbnailUrl = "",
		long sourceModId = 0,
		long sourceFileId = 0,
		string sourceFileName = "",
		string sourceVersion = "",
		CancellationToken cancellationToken = default);
	Task PauseAsync(string itemId);
	Task ResumeAsync(string itemId);
	Task CancelAsync(string itemId);
	Task RetryAsync(string itemId);
	Task RemoveAsync(string itemId, bool deleteCompletedFile);
	Task ClearInstalledHistoryAsync();
	Task SetStateAsync(string itemId, NxmDownloadState state, string errorCode = "", string errorDetails = "");
	Task SetInspectionAsync(string itemId, string projectName, string contentKind, string destination,
		string inspectionSummary, bool installable, string thumbnailUrl = "");
	Task SetInstalledAsync(string itemId, string destination);
	Task DownloadAgainAsync(string itemId);
	Task PauseAllAsync();
	Task ResumeAllAsync();
	Task SetNetworkEnabledAsync(bool enabled);
	Task DrainAsync(CancellationToken cancellationToken = default);
	void SetActiveDownloadLimit(int limit);
	Task ShutdownAsync(CancellationToken cancellationToken = default);
}

public sealed class NxmDownloadManager : INxmDownloadManager
{
	private const long MaximumDownloadBytes = 32L * 1024 * 1024 * 1024;
	private readonly ObservableCollection<NxmDownloadItem> _items = new();
	private readonly INxmDownloadStore _store;
	private readonly INxmResolverFactory _resolverFactory;
	private readonly INxmTransfer _transfer;
	private readonly NxmDownloadScheduler _transferScheduler;
	private readonly Func<NxmDownloadDescriptor, CancellationToken, Task<bool>> _confirm;
	private readonly Func<bool> _confirmCleanDownloads;
	private readonly string _directory;
	private readonly SemaphoreSlim _stateGate = new(1, 1);
	private readonly SemaphoreSlim _localIntakeGate = new(1, 1);
	private readonly SemaphoreSlim _confirmationGate = new(1, 1);
	private readonly SemaphoreSlim _shutdownGate = new(1, 1);
	private readonly Dictionary<string, OwnedOperation> _operations = new(StringComparer.Ordinal);
	private long _nextOperationGeneration;
	private long _nextQueuePosition;
	private bool _shuttingDown;
	private bool _shutdownComplete;
	private volatile bool _networkEnabled = true;

	public ReadOnlyObservableCollection<NxmDownloadItem> Items { get; }
	public event Action<string> FocusRequested;
	public event Action<NxmDownloadItem> ItemChanged;
	internal Action<string> RecycleCompletedFile { get; set; } = path =>
	{
		if (!RecycleBinHelper.DeleteFile(path, false, false, out var error) || File.Exists(path))
			throw new IOException(error ?? "The downloaded archive could not be moved to the Recycle Bin.");
	};

	public NxmDownloadManager(
		string directory,
		INxmDownloadStore store,
		INxmResolverFactory resolverFactory,
		INxmTransfer transfer,
		int activeDownloadLimit,
		Func<bool> confirmCleanDownloads,
		Func<NxmDownloadDescriptor, CancellationToken, Task<bool>> confirm,
		bool networkEnabled = true)
	{
		_directory = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory)));
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_resolverFactory = resolverFactory ?? throw new ArgumentNullException(nameof(resolverFactory));
		_transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
		_transferScheduler = new NxmDownloadScheduler(activeDownloadLimit);
		_confirmCleanDownloads = confirmCleanDownloads ?? throw new ArgumentNullException(nameof(confirmCleanDownloads));
		_confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
		_networkEnabled = networkEnabled;
		Items = new ReadOnlyObservableCollection<NxmDownloadItem>(_items);
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		var restored = await _store.ReconcileAsync(cancellationToken);
		foreach (var item in restored.OrderBy(item => item.QueuePosition)) _items.Add(item);
		_nextQueuePosition = _items.Count == 0 ? 0 : _items.Max(item => item.QueuePosition);
		if (!_networkEnabled)
		{
			var queued = _items.Where(item => item.State is NxmDownloadState.Queued or NxmDownloadState.RetryWaiting
				or NxmDownloadState.Resolving or NxmDownloadState.Downloading).ToArray();
			await PersistStateChangesAsync(queued, NxmDownloadState.Paused, "paused", cancellationToken);
			return;
		}
		await RefreshMissingMetadataAsync(cancellationToken);
		await ScheduleReadyTransfersAsync();
	}

	public async Task<string> EnqueueAsync(NexusModManagerLink link, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(link);
		await _stateGate.WaitAsync(cancellationToken);
		NxmDownloadItem item;
		var startResolution = false;
		var existing = false;
		try
		{
			if (_shuttingDown) throw new InvalidOperationException("The Nexus download queue is shutting down.");
			if (!_networkEnabled) throw new InvalidOperationException("Nexus network operations are disabled.");
			item = _items.FirstOrDefault(candidate => candidate.ModId == link.ModId && candidate.FileId == link.FileId);
			if (item != null)
			{
				item.Authorization = link;
				if (item.State == NxmDownloadState.NeedsFreshLink)
				{
					var candidate = PersistentCopy(item);
					candidate.State = NxmDownloadState.Resolving;
					candidate.ErrorCode = String.Empty;
					await _store.SaveAsync(ReplaceForSave(item, candidate), cancellationToken);
					ApplyPersistentValues(candidate, item);
					ItemChanged?.Invoke(item);
					startResolution = true;
				}
				existing = true;
			}
			else
			{
				item = new NxmDownloadItem
				{
					QueuePosition = _nextQueuePosition + 1,
					ModId = link.ModId,
					FileId = link.FileId,
					RequiresAuthorization = !String.IsNullOrWhiteSpace(link.DownloadKey),
					Authorization = link,
					State = NxmDownloadState.Resolving,
					ProjectName = $"Nexus mod {link.ModId}",
					FileDisplayName = $"File {link.FileId}"
				};
				await _store.SaveAsync(_items.Concat([item]), cancellationToken);
				_nextQueuePosition = item.QueuePosition;
				_items.Add(item);
				ItemChanged?.Invoke(item);
				startResolution = true;
			}
		}
		finally
		{
			_stateGate.Release();
		}

		if (existing) FocusRequested?.Invoke(item.Id);
		if (startResolution) StartResolution(item, link, cancellationToken);
		return item.Id;
	}

	public async Task<string> AddLocalPackageAsync(string sourcePath, string archiveSha256, string projectName,
		string contentKind, string destination, string inspectionSummary,
		AcquiredPackageSourceKind sourceKind = AcquiredPackageSourceKind.LocalFile,
		string thumbnailUrl = "",
		long sourceModId = 0,
		long sourceFileId = 0,
		string sourceFileName = "",
		string sourceVersion = "",
		CancellationToken cancellationToken = default)
	{
		if (String.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("A local package path is required.", nameof(sourcePath));
		var normalizedPath = Path.GetFullPath(sourcePath);
		if (!File.Exists(normalizedPath)) throw new FileNotFoundException("The local package could not be found.", normalizedPath);
		if (String.IsNullOrWhiteSpace(archiveSha256)) throw new ArgumentException("A verified archive identity is required.", nameof(archiveSha256));
		if (sourceKind == AcquiredPackageSourceKind.NexusMods && sourceModId <= 0)
			throw new ArgumentOutOfRangeException(nameof(sourceModId), "A retained Nexus package requires its public mod identity.");
		var sourceInfo = new FileInfo(normalizedPath);
		if (sourceInfo.Length > MaximumDownloadBytes) throw new InvalidDataException("The local package exceeds Redux's 32 GB safety limit.");

		await _localIntakeGate.WaitAsync(cancellationToken);
		try
		{
			string completedFileName;
			string reusedId = null;
			NxmDownloadItem existing = null;
			await _stateGate.WaitAsync(cancellationToken);
			try
			{
				if (_shuttingDown) throw new InvalidOperationException("The download manager is shutting down.");
				existing = _items.FirstOrDefault(item => sourceKind == AcquiredPackageSourceKind.NexusMods
					? item.SourceKind == AcquiredPackageSourceKind.NexusMods
						&& item.ModId == sourceModId && item.FileId == sourceFileId
					: item.SourceKind != AcquiredPackageSourceKind.NexusMods
						&& String.Equals(item.ArchiveSha256, archiveSha256, StringComparison.OrdinalIgnoreCase));
				var retainedArchive = existing == null ? null : SafePath(existing.CompletedFileName);
				if (existing?.IsRemovalPending == true)
					throw new InvalidOperationException("Finish deleting the previous package before adding it again.");
				if (existing != null && retainedArchive != null && File.Exists(retainedArchive))
				{
					var candidate = PersistentCopy(existing);
					candidate.SourceFileName = SafeSourceFileName(sourceFileName, normalizedPath);
					candidate.FileDisplayName = candidate.SourceFileName;
					if (!String.IsNullOrWhiteSpace(sourceVersion)) candidate.Version = sourceVersion;
					if (!String.IsNullOrWhiteSpace(projectName)) candidate.ProjectName = projectName;
					candidate.DetectedContentKind = contentKind ?? String.Empty;
					candidate.DetectedDestination = destination ?? String.Empty;
					candidate.InspectionSummary = inspectionSummary ?? String.Empty;
					candidate.InspectionCompleted = true;
					if (!String.IsNullOrWhiteSpace(thumbnailUrl)) candidate.ThumbnailUrl = thumbnailUrl;
					if (candidate.State == NxmDownloadState.Installed)
					{
						candidate.State = String.IsNullOrWhiteSpace(destination)
							? NxmDownloadState.NeedsReview : NxmDownloadState.Downloaded;
						candidate.InstallDestination = String.Empty;
						candidate.InstalledAt = null;
						candidate.ErrorCode = String.IsNullOrWhiteSpace(destination) ? "unsupported-layout" : String.Empty;
						candidate.ErrorDetails = String.IsNullOrWhiteSpace(destination)
							? inspectionSummary ?? String.Empty : String.Empty;
					}
					await _store.SaveAsync(ReplaceForSave(existing, candidate), cancellationToken);
					ApplyPersistentValues(candidate, existing);
					existing.Progress = 1;
					ItemChanged?.Invoke(existing);
					reusedId = existing.Id;
				}
				completedFileName = reusedId != null ? null
					: existing != null && !String.IsNullOrWhiteSpace(existing.CompletedFileName)
						? existing.CompletedFileName
						: AllocateLocalFileName(Path.GetFileName(normalizedPath));
			}
			finally { _stateGate.Release(); }
			if (reusedId != null)
			{
				FocusRequested?.Invoke(reusedId);
				return reusedId;
			}

			var completedPath = Path.Combine(_directory, completedFileName);
			try
			{
				await using (var input = new FileStream(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read,
					65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
				await using (var output = new FileStream(completedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
					65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
					await input.CopyToAsync(output, cancellationToken);
				if (!String.Equals(archiveSha256, await ComputeFileSha256Async(completedPath, cancellationToken),
					StringComparison.OrdinalIgnoreCase))
					throw new InvalidDataException("The local package changed while Redux was adding it. Try again after the file is no longer being modified.");
			}
			catch
			{
				if (File.Exists(completedPath)) File.Delete(completedPath);
				throw;
			}

			var item = new NxmDownloadItem
			{
				SourceKind = sourceKind,
				ModId = sourceKind == AcquiredPackageSourceKind.NexusMods ? sourceModId : 0,
				FileId = sourceKind == AcquiredPackageSourceKind.NexusMods ? sourceFileId : 0,
				SourceFileName = SafeSourceFileName(sourceFileName, normalizedPath),
				ProjectName = String.IsNullOrWhiteSpace(projectName) ? Path.GetFileNameWithoutExtension(normalizedPath) : projectName,
				FileDisplayName = SafeSourceFileName(sourceFileName, normalizedPath),
				FileName = SafeSourceFileName(sourceFileName, normalizedPath),
				Version = sourceVersion ?? String.Empty,
				CompletedFileName = completedFileName,
				SizeBytes = sourceInfo.Length,
				BytesReceived = sourceInfo.Length,
				ArchiveSha256 = archiveSha256,
				ThumbnailUrl = thumbnailUrl ?? String.Empty,
				DetectedContentKind = contentKind ?? String.Empty,
				DetectedDestination = destination ?? String.Empty,
				InspectionSummary = inspectionSummary ?? String.Empty,
				InspectionCompleted = true,
				State = String.IsNullOrWhiteSpace(destination) ? NxmDownloadState.NeedsReview : NxmDownloadState.Downloaded,
				ErrorCode = String.IsNullOrWhiteSpace(destination) ? "unsupported-layout" : String.Empty,
				ErrorDetails = String.IsNullOrWhiteSpace(destination) ? inspectionSummary ?? String.Empty : String.Empty,
				Progress = 1
			};
			var stateGateHeld = false;
			try
			{
				await _stateGate.WaitAsync(cancellationToken);
				stateGateHeld = true;
				if (_shuttingDown) throw new InvalidOperationException("The download manager is shutting down.");
				if (existing != null && _items.Contains(existing))
				{
					if (existing.IsRemovalPending)
						throw new InvalidOperationException("Finish deleting the previous package before adding it again.");
					item.Id = existing.Id;
					item.QueuePosition = existing.QueuePosition;
					await _store.SaveAsync(ReplaceForSave(existing, item), cancellationToken);
					ApplyPersistentValues(item, existing);
					existing.Progress = 1;
					item = existing;
					ItemChanged?.Invoke(existing);
				}
				else
				{
					item.QueuePosition = _nextQueuePosition + 1;
					await _store.SaveAsync(_items.Concat([item]), cancellationToken);
					_nextQueuePosition = item.QueuePosition;
					_items.Add(item);
					ItemChanged?.Invoke(item);
				}
			}
			catch
			{
				if (File.Exists(completedPath)) File.Delete(completedPath);
				throw;
			}
			finally { if (stateGateHeld) _stateGate.Release(); }
			return item.Id;
		}
		finally { _localIntakeGate.Release(); }
	}

	public Task PauseAsync(string itemId) => Find(itemId)?.IsRemovalPending == true
		? Task.CompletedTask : StopOperationAsync(itemId, NxmDownloadState.Paused, "paused");

	public async Task ResumeAsync(string itemId)
	{
		if (_shuttingDown || !_networkEnabled) return;
		var item = Find(itemId);
		if (item == null || item.IsRemovalPending || item.State is not (NxmDownloadState.Paused or NxmDownloadState.Failed or NxmDownloadState.RetryWaiting)) return;
		if (item.RequiresAuthorization && item.Authorization == null)
		{
			await TransitionAsync(item, NxmDownloadState.NeedsFreshLink, "fresh-link-required");
			return;
		}
		if (String.IsNullOrWhiteSpace(item.CompletedFileName))
		{
			if (await TryTransitionAsync(item, NxmDownloadState.Resolving))
				StartResolution(item, new NexusModManagerLink(item.ModId, item.FileId, null, null, null), CancellationToken.None);
			return;
		}
		await TransitionAsync(item, NxmDownloadState.Queued);
		await ScheduleReadyTransfersAsync();
	}

	public Task RetryAsync(string itemId)
	{
		var item = Find(itemId);
		return item?.State == NxmDownloadState.Failed && SafePath(item.CompletedFileName) is { } path && File.Exists(path)
			? DownloadAgainAsync(itemId) : ResumeAsync(itemId);
	}

	public async Task DownloadAgainAsync(string itemId)
	{
		await _stateGate.WaitAsync();
		NxmDownloadItem item;
		try
		{
			if (_shuttingDown || !_networkEnabled) throw new InvalidOperationException("Nexus downloads are currently disabled.");
			item = Find(itemId);
			if (item == null || (!item.CanDownloadAgain && (item.State != NxmDownloadState.Installed || item.ErrorCode == "rollback-failed"))) return;
			var candidate = PersistentCopy(item);
			// Never overwrite or remove the archive from the previous attempt.
			AssignPaths(candidate);
			candidate.BytesReceived = 0;
			candidate.ETag = String.Empty;
			candidate.LastModified = null;
			candidate.InstalledAt = null;
			candidate.ArchiveSha256 = String.Empty;
			candidate.RetryCount = 0;
			candidate.RetryAfter = null;
			candidate.ErrorDetails = String.Empty;
			candidate.State = item.RequiresAuthorization ? NxmDownloadState.NeedsFreshLink : NxmDownloadState.Resolving;
			candidate.ErrorCode = item.RequiresAuthorization ? "fresh-link-required" : String.Empty;
			await _store.SaveAsync(ReplaceForSave(item, candidate));
			ApplyPersistentValues(candidate, item);
			item.Authorization = null;
			item.Progress = 0;
			ItemChanged?.Invoke(item);
		}
		finally { _stateGate.Release(); }
		if (item.State == NxmDownloadState.Resolving)
			StartResolution(item, new NexusModManagerLink(item.ModId, item.FileId, null, null, null), CancellationToken.None);
	}

	public Task SetStateAsync(string itemId, NxmDownloadState state, string errorCode = "", string errorDetails = "")
	{
		var item = Find(itemId);
		return item == null ? Task.CompletedTask : UpdatePersistedAsync(item, candidate =>
		{
			candidate.State = state;
			candidate.ErrorCode = errorCode;
			candidate.ErrorDetails = errorDetails;
		});
	}

	public Task SetInstalledAsync(string itemId, string destination)
	{
		var item = Find(itemId);
		return item == null ? Task.CompletedTask : UpdatePersistedAsync(item, candidate =>
		{
			candidate.State = NxmDownloadState.Installed;
			candidate.InstallDestination = destination ?? String.Empty;
			candidate.InstalledAt = DateTimeOffset.UtcNow;
			candidate.ErrorCode = String.Empty;
			candidate.ErrorDetails = String.Empty;
		});
	}

	public async Task CancelAsync(string itemId)
	{
		if (Find(itemId)?.IsRemovalPending == true) return;
		await StopOperationAsync(itemId, NxmDownloadState.Failed, "cancelled");
		var item = Find(itemId);
		var partialPath = item == null ? null : SafePath(item.PartialFileName);
		if (partialPath != null && File.Exists(partialPath)) File.Delete(partialPath);
		if (partialPath != null && File.Exists(partialPath + ".meta")) File.Delete(partialPath + ".meta");
		var completedPath = item == null ? null : SafePath(item.CompletedFileName);
		if (completedPath != null && File.Exists(completedPath)) File.Delete(completedPath);
	}

	public async Task RemoveAsync(string itemId, bool deleteCompletedFile)
	{
		await CancelAndWaitAsync(itemId);
		await RemoveTrackedItemAsync(Find(itemId), deleteCompletedFile);
	}

	public async Task ClearInstalledHistoryAsync()
	{
		await _stateGate.WaitAsync();
		try
		{
			var installed = _items.Where(item => item.State == NxmDownloadState.Installed).ToArray();
			if (installed.Length == 0) return;
			await _store.SaveAsync(_items.Except(installed));
			foreach (var item in installed) _items.Remove(item);
		}
		finally { _stateGate.Release(); }
	}

	private async Task RemoveTrackedItemAsync(NxmDownloadItem item, bool deleteCompletedFile,
		CancellationToken cancellationToken = default)
	{
		if (item == null) return;
		await _stateGate.WaitAsync(cancellationToken);
		try
		{
			if (!_items.Contains(item) || item.State == NxmDownloadState.Installing) return;
			var completedPath = deleteCompletedFile ? SafePath(item.CompletedFileName) : null;
			var partialPath = SafePath(item.PartialFileName);
			var needsCleanup = (completedPath != null && File.Exists(completedPath))
				|| (partialPath != null && (File.Exists(partialPath) || File.Exists(partialPath + ".meta")));
			if (needsCleanup && !item.IsRemovalPending)
			{
				// Keep a durable retry record before changing any files. A recycle,
				// cleanup or final manifest failure must not orphan the remaining data.
				var pending = PersistentCopy(item);
				pending.State = NxmDownloadState.Failed;
				pending.ErrorCode = NxmDownloadItem.RemovalIncompleteErrorCode;
				pending.ErrorDetails = String.Empty;
				await _store.SaveAsync(ReplaceForSave(item, pending), cancellationToken);
				ApplyPersistentValues(pending, item);
				ItemChanged?.Invoke(item);
			}
			if (completedPath != null && File.Exists(completedPath)) RecycleCompletedFile(completedPath);
			if (partialPath != null && File.Exists(partialPath)) File.Delete(partialPath);
			if (partialPath != null && File.Exists(partialPath + ".meta")) File.Delete(partialPath + ".meta");
			await _store.SaveAsync(_items.Where(candidate => !ReferenceEquals(candidate, item)), cancellationToken);
			_items.Remove(item);
		}
		finally { _stateGate.Release(); }
	}

	public async Task PauseAllAsync()
	{
		var items = _items.Where(item => item.State is NxmDownloadState.Resolving or NxmDownloadState.Downloading
			or NxmDownloadState.Queued or NxmDownloadState.RetryWaiting).ToArray();
		foreach (var item in items) CancelOperation(item.Id);
		await Task.WhenAll(items.Select(item => CancelAndWaitAsync(item.Id)));
		await PersistStateChangesAsync(items, NxmDownloadState.Paused, "paused");
	}

	public async Task ResumeAllAsync()
	{
		if (!_networkEnabled) return;
		foreach (var item in _items.Where(item => item.State == NxmDownloadState.Paused).ToArray()) await ResumeAsync(item.Id);
	}

	public async Task SetNetworkEnabledAsync(bool enabled)
	{
		await _stateGate.WaitAsync();
		try
		{
			if (_shuttingDown) return;
			_networkEnabled = enabled;
		}
		finally { _stateGate.Release(); }
		if (enabled) return;

		OwnedOperation[] operations;
		lock (_operations)
		{
			operations = _operations.Values.ToArray();
			foreach (var operation in operations) operation.Cancellation.Cancel();
		}
		try { await Task.WhenAll(operations.Select(operation => operation.Task)); }
		catch (OperationCanceledException) { }

		var paused = _items.Where(item => item.State is NxmDownloadState.Resolving or NxmDownloadState.Downloading
			or NxmDownloadState.Queued or NxmDownloadState.RetryWaiting).ToArray();
		await PersistStateChangesAsync(paused, NxmDownloadState.Paused, "paused");
	}

	public void SetActiveDownloadLimit(int limit) => _transferScheduler.SetLimit(limit);

	public async Task DrainAsync(CancellationToken cancellationToken = default)
	{
		Task[] tasks;
		lock (_operations)
		{
			// Completed operations may still be awaiting their cleanup continuations.
			// Draining the dictionary itself can spin and starve those continuations.
			tasks = _operations.Values.Select(operation => operation.Task)
				.Where(task => task != null && !task.IsCompleted).ToArray();
		}
		await DrainOperationsAsync(tasks, cancellationToken);
	}

	private static async Task DrainOperationsAsync(IEnumerable<Task> operations, CancellationToken cancellationToken)
	{
		var tasks = operations.Where(task => task != null && !task.IsCompleted).ToArray();
		if (tasks.Length == 0) return;
		try { await Task.WhenAll(tasks).WaitAsync(cancellationToken); }
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			// Shutdown deliberately cancels owned work before persisting paused states.
		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
		{
			// Individual operations own their failure transition. A stale operation fault
			// must not prevent the queue's current durable state from being saved.
			DivinityApp.Log($"NXM operation ended while draining: {ex.GetType().Name}");
		}
	}

	public async Task ShutdownAsync(CancellationToken cancellationToken = default)
	{
		await _shutdownGate.WaitAsync(cancellationToken);
		try
		{
			if (_shutdownComplete) return;
			await _stateGate.WaitAsync(cancellationToken);
			try
			{
				_shuttingDown = true;
				_networkEnabled = false;
			}
			finally { _stateGate.Release(); }
			var paused = _items.Where(item => item.State is NxmDownloadState.Resolving or NxmDownloadState.Downloading
				or NxmDownloadState.Queued or NxmDownloadState.RetryWaiting).ToArray();
			OwnedOperation[] activeOperations;
			var activeIds = paused.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
			lock (_operations)
				activeOperations = _operations.Where(pair => activeIds.Contains(pair.Key))
					.Select(pair => pair.Value).ToArray();
			foreach (var operation in activeOperations) operation.Cancellation.Cancel();
			await DrainOperationsAsync(activeOperations.Select(operation => operation.Task), cancellationToken);
			await PersistStateChangesAsync(paused, NxmDownloadState.Paused, "paused", cancellationToken);
			_shutdownComplete = true;
		}
		finally { _shutdownGate.Release(); }
	}

	private async Task ResolveAndQueueAsync(NxmDownloadItem item, NexusModManagerLink link, CancellationToken cancellationToken)
	{
		try
		{
			if (_shuttingDown || !_networkEnabled) return;
			var descriptor = await _resolverFactory.ResolveMetadataAsync(link, cancellationToken);
			if (_shuttingDown || descriptor == null) return;
			var fileName = Path.GetFileName(descriptor.FileName);
			var extension = Path.GetExtension(fileName).ToLowerInvariant();
			if (extension is not (".zip" or ".rar" or ".7z" or ".pak" or ".lsv"))
				throw new InvalidDataException("The Nexus file is not a supported archive or PAK.");
			if (!await IsTrackedInStateAsync(item, NxmDownloadState.Resolving)) return;
			// Metadata is durable before the confirmation or signed download URL is handled.
			// Free-user NXM authorization is intentionally never persisted, but the public
			// project/file identity and thumbnail should survive a close or restart.
			if (!await CacheResolvedMetadataAsync(item, descriptor, fileName, cancellationToken)) return;
			var confirmed = true;
			if (_confirmCleanDownloads())
			{
				await _confirmationGate.WaitAsync(cancellationToken);
				try
				{
					if (!await IsTrackedInStateAsync(item, NxmDownloadState.Resolving)) return;
					confirmed = await _confirm(descriptor, cancellationToken);
				}
				finally { _confirmationGate.Release(); }
			}
			if (!confirmed)
			{
				// This is the item's owned resolution operation, so it must not call the
				// public removal path that cancels and waits for that same operation.
				await RemoveTrackedItemAsync(item, false, cancellationToken);
				return;
			}
			if (!await IsTrackedInStateAsync(item, NxmDownloadState.Resolving)) return;
			if (await QueueResolvedItemAsync(item, descriptor, fileName))
				await ScheduleReadyTransfersAsync(item.Id, CurrentOperationGeneration(item.Id));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
		catch (NxmResolutionException ex)
		{
			if (ex.Kind == NxmResolutionFailureKind.RateLimited && !_shuttingDown)
			{
				await UpdatePersistedAsync(item, candidate =>
				{
					candidate.RetryAfter = ex.RetryAfter ?? DateTimeOffset.UtcNow.AddMinutes(1);
					candidate.State = NxmDownloadState.RetryWaiting;
					candidate.ErrorCode = "rate-limited";
				});
				_ = RetryResolutionAfterDelayAsync(item, link);
				return;
			}
			var state = ex.Kind is NxmResolutionFailureKind.FreshLinkRequired or NxmResolutionFailureKind.ExpiredAuthorization
				? NxmDownloadState.NeedsFreshLink : NxmDownloadState.Failed;
			await TransitionAsync(item, state, ex.Kind.ToString());
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"NXM item {item.Id} resolution failed: {ex.GetType().Name}");
			await TransitionAsync(item, NxmDownloadState.Failed, "resolution-failed");
		}
	}

	private async Task RefreshMissingMetadataAsync(CancellationToken cancellationToken)
	{
		var incomplete = _items.Where(item => item.SourceKind == AcquiredPackageSourceKind.NexusMods
			&& (String.IsNullOrWhiteSpace(item.FileName)
			|| String.Equals(item.ProjectName, $"Nexus mod {item.ModId}", StringComparison.Ordinal)
			|| String.Equals(item.FileDisplayName, $"File {item.FileId}", StringComparison.Ordinal))).ToArray();
		foreach (var item in incomplete)
		{
			try
			{
				var descriptor = await _resolverFactory.ResolveMetadataAsync(
					new NexusModManagerLink(item.ModId, item.FileId, null, null, null), cancellationToken);
				if (descriptor == null || descriptor.ModId != item.ModId || descriptor.FileId != item.FileId) continue;
				var fileName = Path.GetFileName(descriptor.FileName);
				var extension = Path.GetExtension(fileName).ToLowerInvariant();
				if (extension is not (".zip" or ".rar" or ".7z" or ".pak" or ".lsv")) continue;
				await CacheResolvedMetadataAsync(item, descriptor, fileName, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
			catch (Exception ex)
			{
				DivinityApp.Log($"Could not refresh cached Nexus metadata for {item.Identity}: {ex.GetType().Name}");
			}
		}
	}

	private void StartResolution(NxmDownloadItem item, NexusModManagerLink link, CancellationToken cancellationToken)
	{
		var operation = CreateOperation(cancellationToken);
		StartAndTrackOperation(item.Id, operation,
			() => ResolveAndQueueAsync(item, link, operation.Cancellation.Token));
	}

	private async Task RetryResolutionAfterDelayAsync(NxmDownloadItem item, NexusModManagerLink link)
	{
		var delay = item.RetryAfter.GetValueOrDefault() - DateTimeOffset.UtcNow;
		if (delay > TimeSpan.Zero) await Task.Delay(delay);
		if (_shuttingDown || !_networkEnabled || !await IsTrackedInStateAsync(item, NxmDownloadState.RetryWaiting)) return;
		if (await TryTransitionAsync(item, NxmDownloadState.Resolving))
			StartResolution(item, link, CancellationToken.None);
	}

	private bool ScheduleTransfer(NxmDownloadItem item, long? replaceGeneration = null)
	{
		var operation = CreateOperation();
		lock (_operations)
		{
			if (_operations.TryGetValue(item.Id, out var current) && current.Generation != replaceGeneration)
			{
				operation.Cancellation.Dispose();
				return false;
			}
			operation.Task = _transferScheduler.Enqueue(async () =>
		{
			operation.Cancellation.Token.ThrowIfCancellationRequested();
			if (_shuttingDown || !_networkEnabled || !await IsTrackedInStateAsync(item, NxmDownloadState.Queued)) return;
			var progressClock = System.Diagnostics.Stopwatch.StartNew();
			var previousBytes = item.BytesReceived;
			var previousElapsed = TimeSpan.Zero;
			try
			{
				await TransitionAsync(item, NxmDownloadState.Downloading);
				var authorization = item.Authorization
					?? new NexusModManagerLink(item.ModId, item.FileId, null, null, null);
				var downloadUri = await _resolverFactory.ResolveDownloadUriAsync(authorization, operation.Cancellation.Token);
				var progress = new DownloadProgressReporter(value =>
				{
					var elapsed = progressClock.Elapsed;
					var completed = value.TotalBytes > 0 && value.BytesReceived >= value.TotalBytes;
					if (!completed && elapsed - previousElapsed < TimeSpan.FromMilliseconds(250)) return;
					item.BytesReceived = value.BytesReceived;
					if (value.TotalBytes > 0) item.SizeBytes = value.TotalBytes.Value;
					item.Progress = item.SizeBytes > 0 ? Math.Clamp((double)item.BytesReceived / item.SizeBytes, 0, 1) : 0;
					var seconds = (elapsed - previousElapsed).TotalSeconds;
					if (seconds > 0)
					{
						item.BytesPerSecond = Math.Max(0, (item.BytesReceived - previousBytes) / seconds);
						previousBytes = item.BytesReceived;
						previousElapsed = elapsed;
					}
					ItemChanged?.Invoke(item);
				});
				var result = await _transfer.DownloadAsync(new NxmTransferRequest(downloadUri,
					SafePath(item.PartialFileName)!, SafePath(item.CompletedFileName)!, MaximumDownloadBytes,
					item.ETag, item.LastModified, item.SizeBytes), progress, operation.Cancellation.Token);
				if (!IsCurrentOperation(item.Id, operation) || operation.Cancellation.IsCancellationRequested)
				{
					var stalePath = SafePath(item.CompletedFileName);
					if (stalePath != null && File.Exists(stalePath)) File.Delete(stalePath);
					return;
				}
				await UpdatePersistedAsync(item, candidate =>
				{
					candidate.BytesReceived = result.SizeBytes;
					candidate.SizeBytes = result.SizeBytes;
					candidate.ETag = result.ETag ?? String.Empty;
					candidate.LastModified = result.LastModified;
					candidate.ArchiveSha256 = result.Sha256;
					candidate.State = NxmDownloadState.Downloaded;
					candidate.ErrorCode = String.Empty;
				});
				item.Progress = 1;
			}
			catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested) { }
			catch (NxmResolutionException ex)
			{
				var state = ex.Kind is NxmResolutionFailureKind.FreshLinkRequired
					or NxmResolutionFailureKind.ExpiredAuthorization or NxmResolutionFailureKind.AccountMismatch
					? NxmDownloadState.NeedsFreshLink : NxmDownloadState.Failed;
				await TransitionAsync(item, state, ex.Kind.ToString());
			}
			catch (HttpRequestException ex) when (!operation.Cancellation.IsCancellationRequested && IsTransient(ex))
			{
				DivinityApp.Log($"NXM item {item.Id} transfer failed: {ex.GetType().Name}");
				if (item.RetryCount < 3 && !_shuttingDown)
				{
					await UpdatePersistedAsync(item, candidate =>
					{
						candidate.RetryCount++;
						candidate.RetryAfter = DateTimeOffset.UtcNow.AddSeconds(Math.Pow(2, candidate.RetryCount));
						candidate.State = NxmDownloadState.RetryWaiting;
						candidate.ErrorCode = "network-retry";
					});
					_ = RetryAfterDelayAsync(item);
				}
				else await TransitionAsync(item, NxmDownloadState.Failed, "transfer-failed");
			}
			catch (Exception ex) when (!operation.Cancellation.IsCancellationRequested)
			{
				DivinityApp.Log($"NXM item {item.Id} transfer failed: {ex.GetType().Name}");
				await TransitionAsync(item, NxmDownloadState.Failed, "transfer-failed");
			}
			finally { }
		}, operation.Cancellation.Token);
			_operations[item.Id] = operation;
		}
		_ = RemoveOperationWhenCompleteAsync(item.Id, operation);
		return true;
	}

	private async Task RetryAfterDelayAsync(NxmDownloadItem item)
	{
		var retryDelay = item.RetryAfter.GetValueOrDefault() - DateTimeOffset.UtcNow;
		if (retryDelay > TimeSpan.Zero) await Task.Delay(retryDelay);
		if (!_shuttingDown && _networkEnabled && item.State == NxmDownloadState.RetryWaiting)
		{
			await TransitionAsync(item, NxmDownloadState.Queued);
			await ScheduleReadyTransfersAsync();
		}
	}

	private async Task StopOperationAsync(string itemId, NxmDownloadState state, string errorCode)
	{
		var item = Find(itemId);
		if (item == null) return;
		await CancelAndWaitAsync(itemId);
		await TransitionAsync(item, state, errorCode);
	}

	private async Task TransitionAsync(NxmDownloadItem item, NxmDownloadState state, string errorCode = "")
		=> await TryTransitionAsync(item, state, errorCode);

	private async Task<bool> TryTransitionAsync(NxmDownloadItem item, NxmDownloadState state, string errorCode = "")
		=> await UpdatePersistedAsync(item, candidate =>
		{
			candidate.State = state;
			candidate.ErrorCode = errorCode;
			candidate.ErrorDetails = String.Empty;
		});

	private async Task<bool> UpdatePersistedAsync(NxmDownloadItem item, Action<NxmDownloadItem> update)
	{
		await _stateGate.WaitAsync();
		try
		{
			if (!_items.Contains(item) || item.IsRemovalPending) return false;
			var candidate = PersistentCopy(item);
			update(candidate);
			await _store.SaveAsync(ReplaceForSave(item, candidate));
			ApplyPersistentValues(candidate, item);
			ItemChanged?.Invoke(item);
			return true;
		}
		finally { _stateGate.Release(); }
	}

	private async Task<bool> IsTrackedInStateAsync(NxmDownloadItem item, NxmDownloadState state)
	{
		await _stateGate.WaitAsync();
		try { return _items.Contains(item) && item.State == state; }
		finally { _stateGate.Release(); }
	}

	private OwnedOperation CreateOperation(CancellationToken cancellationToken = default) => new(
		Interlocked.Increment(ref _nextOperationGeneration),
		CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));

	private void StartAndTrackOperation(string itemId, OwnedOperation operation, Func<Task> work)
	{
		lock (_operations)
		{
			operation.Task = Task.Run(work);
			_operations[itemId] = operation;
		}
		_ = RemoveOperationWhenCompleteAsync(itemId, operation);
	}

	private async Task PersistStateChangesAsync(IEnumerable<NxmDownloadItem> items, NxmDownloadState state,
		string errorCode, CancellationToken cancellationToken = default)
	{
		await _stateGate.WaitAsync(cancellationToken);
		try
		{
			var changes = items.Where(_items.Contains).Distinct().ToDictionary(item => item, item =>
			{
				var candidate = PersistentCopy(item);
				candidate.State = state;
				candidate.ErrorCode = errorCode;
				candidate.ErrorDetails = String.Empty;
				return candidate;
			});
			if (changes.Count == 0) return;
			await _store.SaveAsync(_items.Select(item => changes.TryGetValue(item, out var candidate) ? candidate : item), cancellationToken);
			foreach (var change in changes)
			{
				ApplyPersistentValues(change.Value, change.Key);
				ItemChanged?.Invoke(change.Key);
			}
		}
		finally { _stateGate.Release(); }
	}

	private async Task<bool> QueueResolvedItemAsync(NxmDownloadItem item, NxmDownloadDescriptor descriptor, string fileName)
	{
		await _stateGate.WaitAsync();
		try
		{
			if (!_items.Contains(item) || item.State != NxmDownloadState.Resolving) return false;
			var candidate = PersistentCopy(item);
			candidate.ProjectName = descriptor.ProjectName;
			candidate.Author = descriptor.Author;
			candidate.FileDisplayName = descriptor.FileDisplayName;
			candidate.FileName = fileName;
			candidate.Version = descriptor.Version;
			candidate.SizeBytes = descriptor.SizeBytes;
			candidate.ThumbnailUrl = descriptor.ThumbnailUrl;
			candidate.RequiresAuthorization = descriptor.RequiresAuthorization;
			if (SafePath(candidate.CompletedFileName) is not { } completedPath
				|| File.Exists(completedPath)
				|| !Path.GetExtension(completedPath).Equals(Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase)) AssignPaths(candidate);
			candidate.State = NxmDownloadState.Queued;
			candidate.ErrorCode = String.Empty;
			candidate.ErrorDetails = String.Empty;
			await _store.SaveAsync(ReplaceForSave(item, candidate));
			ApplyPersistentValues(candidate, item);
			ItemChanged?.Invoke(item);
			return true;
		}
		finally { _stateGate.Release(); }
	}

	private async Task<bool> CacheResolvedMetadataAsync(NxmDownloadItem item, NxmDownloadDescriptor descriptor,
		string fileName, CancellationToken cancellationToken = default)
	{
		await _stateGate.WaitAsync(cancellationToken);
		try
		{
			if (!_items.Contains(item)) return false;
			var candidate = PersistentCopy(item);
			candidate.ProjectName = descriptor.ProjectName;
			candidate.Author = descriptor.Author;
			candidate.FileDisplayName = descriptor.FileDisplayName;
			candidate.FileName = fileName;
			candidate.Version = descriptor.Version;
			candidate.SizeBytes = descriptor.SizeBytes;
			candidate.ThumbnailUrl = descriptor.ThumbnailUrl;
			candidate.RequiresAuthorization = descriptor.RequiresAuthorization;
			if (SafePath(candidate.CompletedFileName) is not { } completedPath
				|| !Path.GetExtension(completedPath).Equals(Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase))
				AssignPaths(candidate);
			await _store.SaveAsync(ReplaceForSave(item, candidate), cancellationToken);
			ApplyPersistentValues(candidate, item);
			ItemChanged?.Invoke(item);
			return true;
		}
		finally { _stateGate.Release(); }
	}

	private void CancelOperation(string itemId)
	{
		lock (_operations)
			if (_operations.TryGetValue(itemId, out var operation)) operation.Cancellation.Cancel();
	}

	private async Task CancelAndWaitAsync(string itemId)
	{
		OwnedOperation operation;
		lock (_operations) _operations.TryGetValue(itemId, out operation);
		if (operation == null) return;
		operation.Cancellation.Cancel();
		try { await operation.Task; }
		catch (OperationCanceledException) { }
	}

	private bool IsCurrentOperation(string itemId, OwnedOperation operation)
	{
		lock (_operations) return _operations.TryGetValue(itemId, out var current)
			&& current.Generation == operation.Generation;
	}

	private async Task RemoveOperationWhenCompleteAsync(string itemId, OwnedOperation operation)
	{
		try { await operation.Task; }
		catch { }
		finally
		{
			lock (_operations)
				if (_operations.TryGetValue(itemId, out var current) && current.Generation == operation.Generation)
					_operations.Remove(itemId);
			operation.Cancellation.Dispose();
		}
		await ScheduleReadyTransfersAsync();
	}

	private long? CurrentOperationGeneration(string itemId)
	{
		lock (_operations) return _operations.TryGetValue(itemId, out var operation) ? operation.Generation : null;
	}

	private async Task ScheduleReadyTransfersAsync(string replacingItemId = null, long? replaceGeneration = null)
	{
		if (_shuttingDown || !_networkEnabled) return;
		NxmDownloadItem[] ready;
		await _stateGate.WaitAsync();
		try
		{
			var candidates = new List<NxmDownloadItem>();
			foreach (var item in _items.OrderBy(item => item.QueuePosition))
			{
				if (item.State == NxmDownloadState.Resolving) break;
				if (item.State == NxmDownloadState.Queued) candidates.Add(item);
			}
			ready = candidates.ToArray();
		}
		finally { _stateGate.Release(); }
		foreach (var item in ready)
			ScheduleTransfer(item, item.Id == replacingItemId ? replaceGeneration : null);
	}

	private sealed class OwnedOperation
	{
		public long Generation { get; }
		public CancellationTokenSource Cancellation { get; }
		public Task Task { get; set; } = Task.CompletedTask;

		public OwnedOperation(long generation, CancellationTokenSource cancellation)
		{
			Generation = generation;
			Cancellation = cancellation;
		}
	}

	private sealed class DownloadProgressReporter : IProgress<DownloadProgress>
	{
		private readonly Action<DownloadProgress> _report;
		public DownloadProgressReporter(Action<DownloadProgress> report) => _report = report;
		public void Report(DownloadProgress value) => _report(value);
	}

	private void AssignPaths(NxmDownloadItem item)
	{
		var baseName = SafeWindowsFileName(item.FileName, item.ModId, item.FileId);
		var name = Path.GetFileNameWithoutExtension(baseName);
		var extension = Path.GetExtension(baseName);
		var candidate = baseName;
		for (var suffix = 1; _items.Any(other => other.Id != item.Id && other.CompletedFileName.Equals(candidate, StringComparison.OrdinalIgnoreCase))
			|| File.Exists(Path.Combine(_directory, candidate))
			|| File.Exists(Path.Combine(_directory, candidate + ".part"))
			|| File.Exists(Path.Combine(_directory, candidate + ".part.meta")); suffix++)
			candidate = $"{name} ({suffix}){extension}";
		item.CompletedFileName = candidate;
		item.PartialFileName = candidate + ".part";
	}

	public Task SetInspectionAsync(string itemId, string projectName, string contentKind, string destination,
		string inspectionSummary, bool installable, string thumbnailUrl = "")
	{
		var item = Find(itemId);
		return item == null ? Task.CompletedTask : UpdatePersistedAsync(item, candidate =>
		{
			if (!String.IsNullOrWhiteSpace(projectName)) candidate.ProjectName = projectName;
			candidate.DetectedContentKind = contentKind ?? String.Empty;
			candidate.DetectedDestination = destination ?? String.Empty;
			candidate.InspectionSummary = inspectionSummary ?? String.Empty;
			if (!String.IsNullOrWhiteSpace(thumbnailUrl)) candidate.ThumbnailUrl = thumbnailUrl;
			candidate.InspectionCompleted = true;
			if (!installable && candidate.State == NxmDownloadState.Downloaded)
			{
				candidate.State = NxmDownloadState.NeedsReview;
				candidate.ErrorCode = "unsupported-layout";
				candidate.ErrorDetails = inspectionSummary ?? String.Empty;
			}
		});
	}

	private static string SafeSourceFileName(string requestedName, string sourcePath)
	{
		var fileName = Path.GetFileName(String.IsNullOrWhiteSpace(requestedName) ? sourcePath : requestedName);
		var invalid = Path.GetInvalidFileNameChars();
		fileName = new String((fileName ?? String.Empty).Select(character => invalid.Contains(character) ? '_' : character).ToArray())
			.TrimEnd(' ', '.');
		return String.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(sourcePath) : fileName;
	}

	private string AllocateLocalFileName(string value)
	{
		var fileName = Path.GetFileName(value ?? String.Empty);
		var invalid = Path.GetInvalidFileNameChars();
		fileName = new String(fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).TrimEnd(' ', '.');
		if (String.IsNullOrWhiteSpace(fileName)) fileName = $"Local-{Guid.NewGuid():N}.zip";
		var stem = Path.GetFileNameWithoutExtension(fileName);
		var extension = Path.GetExtension(fileName);
		var candidate = fileName;
		for (var suffix = 1; _items.Any(other => other.CompletedFileName.Equals(candidate, StringComparison.OrdinalIgnoreCase))
			|| File.Exists(Path.Combine(_directory, candidate)); suffix++)
			candidate = $"{stem} ({suffix}){extension}";
		return candidate;
	}

	private IEnumerable<NxmDownloadItem> ReplaceForSave(NxmDownloadItem item, NxmDownloadItem candidate) =>
		_items.Select(current => ReferenceEquals(current, item) ? candidate : current);

	private static NxmDownloadItem PersistentCopy(NxmDownloadItem item) => new()
	{
		Id = item.Id,
		QueuePosition = item.QueuePosition,
		ModId = item.ModId,
		FileId = item.FileId,
		ProjectName = item.ProjectName,
		Author = item.Author,
		FileDisplayName = item.FileDisplayName,
		FileName = item.FileName,
		Version = item.Version,
		SizeBytes = item.SizeBytes,
		BytesReceived = item.BytesReceived,
		ETag = item.ETag,
		LastModified = item.LastModified,
		PartialFileName = item.PartialFileName,
		CompletedFileName = item.CompletedFileName,
		State = item.State,
		RequiresAuthorization = item.RequiresAuthorization,
		RetryCount = item.RetryCount,
		RetryAfter = item.RetryAfter,
		ErrorCode = item.ErrorCode,
		ErrorDetails = item.ErrorDetails,
		ArchiveSha256 = item.ArchiveSha256,
		ThumbnailUrl = item.ThumbnailUrl,
		InstallDestination = item.InstallDestination,
		InstalledAt = item.InstalledAt,
		SourceKind = item.SourceKind,
		SourceFileName = item.SourceFileName,
		DetectedContentKind = item.DetectedContentKind,
		DetectedDestination = item.DetectedDestination,
		InspectionSummary = item.InspectionSummary,
		InspectionCompleted = item.InspectionCompleted,
		IsSelected = item.IsSelected
	};

	private static void ApplyPersistentValues(NxmDownloadItem source, NxmDownloadItem target)
	{
		target.ProjectName = source.ProjectName;
		target.Author = source.Author;
		target.FileDisplayName = source.FileDisplayName;
		target.FileName = source.FileName;
		target.Version = source.Version;
		target.SizeBytes = source.SizeBytes;
		target.BytesReceived = source.BytesReceived;
		target.ETag = source.ETag;
		target.LastModified = source.LastModified;
		target.PartialFileName = source.PartialFileName;
		target.CompletedFileName = source.CompletedFileName;
		target.InstallDestination = source.InstallDestination;
		target.InstalledAt = source.InstalledAt;
		target.SourceKind = source.SourceKind;
		target.SourceFileName = source.SourceFileName;
		target.DetectedContentKind = source.DetectedContentKind;
		target.DetectedDestination = source.DetectedDestination;
		target.InspectionSummary = source.InspectionSummary;
		target.InspectionCompleted = source.InspectionCompleted;
		target.State = source.State;
		target.RequiresAuthorization = source.RequiresAuthorization;
		target.RetryCount = source.RetryCount;
		target.RetryAfter = source.RetryAfter;
		target.ErrorCode = source.ErrorCode;
		target.ErrorDetails = source.ErrorDetails;
		target.ArchiveSha256 = source.ArchiveSha256;
		target.ThumbnailUrl = source.ThumbnailUrl;
		target.IsSelected = source.IsSelected;
		target.RaisePropertyChanged(nameof(target.MetadataText));
		target.RaisePropertyChanged(nameof(target.DownloadDetailText));
		target.RaisePropertyChanged(nameof(target.InstallActionText));
		target.RaisePropertyChanged(nameof(target.HasNexusSource));
	}

	private static string SafeWindowsFileName(string value, long modId, long fileId)
	{
		var fileName = Path.GetFileName(value ?? String.Empty);
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		if (extension is not (".zip" or ".rar" or ".7z" or ".pak" or ".lsv")) extension = ".zip";
		var invalid = Path.GetInvalidFileNameChars();
		fileName = new String(fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray())
			.TrimEnd(' ', '.');
		var stem = Path.GetFileNameWithoutExtension(fileName);
		var reserved = stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
			|| (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
				|| stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9');
		return String.IsNullOrWhiteSpace(stem) || reserved ? $"Nexus-{modId}-{fileId}{extension}" : fileName;
	}

	private static bool IsTransient(HttpRequestException exception) => exception.StatusCode == null
		|| exception.StatusCode is System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests
		|| (int)exception.StatusCode >= 500;

	private static async Task<string> ComputeFileSha256Async(string path, CancellationToken cancellationToken)
	{
		await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
			65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
		return Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken))
			.ToLowerInvariant();
	}

	private NxmDownloadItem Find(string itemId) => _items.FirstOrDefault(item => item.Id == itemId);

	private string SafePath(string fileName)
	{
		if (String.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName)) return null;
		return Path.Combine(_directory, fileName);
	}
}
