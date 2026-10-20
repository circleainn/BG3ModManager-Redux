using DivinityModManager.Models.Updates;
using DivinityModManager.Util;

using Newtonsoft.Json;

using System.Diagnostics;

namespace DivinityModManager.AppServices;

/// <summary>
/// Copies the updater runner outside the installation and launches it only after Redux commits to closing.
/// </summary>
public sealed class ReduxUpdateLaunchService
{
	private static readonly string[] RunnerFiles =
	{
		"ReduxUpdater.exe",
		"ReduxUpdater.dll",
		"ReduxUpdater.deps.json",
		"ReduxUpdater.runtimeconfig.json"
	};

	private readonly object _gate = new();
	private readonly ReduxLaunchTarget _currentLaunch;
	private PendingUpdate _pending;
	public bool PendingIsSnapshot { get { lock (_gate) return _pending?.IsSnapshot == true; } }

	public ReduxUpdateLaunchService(ReduxLaunchTarget currentLaunch = null) =>
		_currentLaunch = currentLaunch ?? ReduxLaunchTarget.Current();

	public bool HasPendingUpdate
	{
		get
		{
			lock (_gate) return _pending != null;
		}
	}

	public void Queue(ReduxPreparedUpdate prepared, string targetDirectory, int parentProcessId)
	{
		ArgumentNullException.ThrowIfNull(prepared);
		if (parentProcessId <= 0) throw new ArgumentOutOfRangeException(nameof(parentProcessId));
		lock (_gate)
		{
			if (_pending != null) throw new InvalidOperationException("A Redux update is already queued.");
		}
		var transactionRoot = Path.GetFullPath(prepared.TransactionDirectory);
		var stagedRoot = Path.GetFullPath(prepared.StagedDirectory);
		var targetRoot = Path.GetFullPath(targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory)));
		if (!Directory.Exists(stagedRoot) || !File.Exists(Path.Combine(targetRoot, "Redux.exe")))
			throw new InvalidDataException("Redux could not prepare the updater for this installation.");

		try
		{
			var sourceRunner = Path.Combine(stagedRoot, "Updater");
			var runnerDirectory = Path.Combine(transactionRoot, "runner");
			Directory.CreateDirectory(runnerDirectory);
			foreach (var fileName in RunnerFiles)
			{
				var source = Path.Combine(sourceRunner, fileName);
				if (!File.Exists(source)) throw new InvalidDataException($"The update is missing runner file '{fileName}'.");
				File.Copy(source, Path.Combine(runnerDirectory, fileName), overwrite: false);
			}

			var requestPath = Path.Combine(runnerDirectory, "request.json");
			var resultPath = Path.Combine(Path.GetDirectoryName(transactionRoot)!, "last-update-result.json");
			var request = new
			{
				schemaVersion = 1,
				parentProcessId,
				targetDirectory = targetRoot,
				stagedDirectory = stagedRoot,
				backupDirectory = Path.Combine(transactionRoot, "backup"),
				resultPath,
				displayVersion = prepared.DisplayVersion,
				relaunchRelativePath = "Redux.exe"
			};
			AtomicFileWriter.WriteAllText(requestPath, JsonConvert.SerializeObject(request, Formatting.Indented),
				validateTemporaryFile: temporaryPath => new FileInfo(temporaryPath).Length > 0);

			lock (_gate)
			{
				if (_pending != null) throw new InvalidOperationException("A Redux update is already queued.");
				_pending = new PendingUpdate(
					requestPath,
					runnerDirectory,
					transactionRoot);
			}
		}
		catch
		{
			TryDeleteDirectory(transactionRoot);
			throw;
		}
	}

	public bool StartPending(out string error)
	{
		PendingUpdate pending;
		lock (_gate)
		{
			pending = _pending;
			_pending = null;
		}
		if (pending == null)
		{
			error = String.Empty;
			return false;
		}

		try
		{
			var start = pending.IsSnapshot
				? CreateSnapshotRunnerStartInfo(pending.WorkingDirectory, pending.RequestPath)
				: CreateRunnerStartInfo(pending.WorkingDirectory, pending.RequestPath);
			Process.Start(start);
			error = String.Empty;
			return true;
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Could not launch Redux Updater:\n{ex}");
			TryDeleteDirectory(pending.TransactionDirectory);
			error = "Redux could not start the updater. The current installation was not changed.";
			return false;
		}
	}

	internal ProcessStartInfo CreateRunnerStartInfo(string runnerDirectory, string requestPath) =>
		ReduxLaunchTarget.ForExecutable(Path.Combine(runnerDirectory, "ReduxUpdater.exe"), _currentLaunch.DotNetHostPath)
			.CreateStartInfo(["--request", Path.GetFullPath(requestPath)]);

	internal ProcessStartInfo CreateSnapshotRunnerStartInfo(string runnerDirectory, string requestPath) =>
		ReduxLaunchTarget.ForExecutable(Path.Combine(runnerDirectory, "ReduxUpdater.exe"), _currentLaunch.DotNetHostPath)
			.CreateStartInfo(["--snapshot-request", Path.GetFullPath(requestPath)]);

	public void QueueSnapshot(ReduxSnapshotStore.Request request, ReduxSnapshotAppearance appearance = null)
	{
		if (request.SchemaVersion != 1 || request.ParentProcessId <= 0 || request.Operation is not ("create" or "restore" or "recover")
			|| !String.Equals(Path.GetFullPath(request.ApplicationDirectory), _currentLaunch.ApplicationDirectory, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("The snapshot request does not match this Redux installation.");
		lock (_gate)
		{
			if (_pending != null) throw new InvalidOperationException("Another update or snapshot is already queued.");
			var transaction = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"BG3ModManager-Redux", "SnapshotOperations", Guid.NewGuid().ToString("N"));
			try
			{
				Directory.CreateDirectory(transaction);
				foreach (var name in RunnerFiles)
					File.Copy(Path.Combine(_currentLaunch.ApplicationDirectory, "Updater", name), Path.Combine(transaction, name));
				var path = Path.Combine(transaction, "request.json");
				ReduxSnapshotStore.WriteRequest(path, request);
				(appearance ?? new ReduxSnapshotAppearance()).Write(Path.Combine(transaction, "appearance.json"));
				File.WriteAllText(Path.Combine(transaction, "RECOVERY.txt"),
					"If a snapshot restore is interrupted, keep this folder. Run ReduxUpdater.exe --snapshot-request request.json from this folder to recover. "
					+ "If Redux opens, Preferences > Backups > Recover interrupted restore performs the same recovery. Snapshots are kept in Redux/Backups/Snapshots.");
				_pending = new PendingUpdate(path, transaction, transaction, true);
			}
			catch { TryDeleteDirectory(transaction); throw; }
		}
	}

	public void CancelPending()
	{
		PendingUpdate pending;
		lock (_gate)
		{
			pending = _pending;
			_pending = null;
		}
		if (pending == null) return;
		TryDeleteDirectory(pending.TransactionDirectory);
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Could not remove Redux update staging data:\n{ex}");
		}
	}

	private sealed record PendingUpdate(
		string RequestPath,
		string WorkingDirectory,
		string TransactionDirectory,
		bool IsSnapshot = false);
}
