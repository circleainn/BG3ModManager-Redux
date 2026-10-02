using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Util;

using Newtonsoft.Json;

namespace Redux.Core.Tests;

internal sealed class ImportOperationRunnerTests
{
	public void CanceledImportWaitsForCommittedSourceRecordsToReachDisk()
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxImportPersistenceTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			var sourceFile = Path.Combine(directory, "sources.json");
			using var cancellation = new CancellationTokenSource();
			var startedSaving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			var allowSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			var result = new ImportOperationResults();
			var operation = ImportOperationRunner.RunAsync(["partial.7z", "never.pak"], result, (_, token) =>
			{
				result.Mods.Add(new DivinityModData { UUID = "committed-mod" });
				cancellation.Cancel();
				token.ThrowIfCancellationRequested();
				return Task.CompletedTask;
			}, _ => throw new Exception("Canceled extraction must not start online metadata requests."), cancellation.Token,
				async () =>
				{
					startedSaving.SetResult();
					await allowSave.Task;
					await AtomicFileWriter.WriteAllBytesAsync(sourceFile,
						Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result.Mods.Select(mod => mod.UUID))),
						cancellationToken: CancellationToken.None);
				});
			startedSaving.Task.GetAwaiter().GetResult();
			RegressionAssert.False(operation.IsCompleted);
			allowSave.SetResult();
			operation.GetAwaiter().GetResult();
			RegressionAssert.SequenceEqual(["committed-mod"], JsonConvert.DeserializeObject<string[]>(File.ReadAllText(sourceFile))!);
			RegressionAssert.True(result.WasCancelled);
		}
		finally { Directory.Delete(directory, true); }
	}

	public void ImportFailureStillPreservesCompletedRecordsBeforeRethrowing()
	{
		var result = new ImportOperationResults();
		var saved = new List<string>();
		var failure = new IOException("Second entry failed");
		var actual = RegressionAssert.Throws<IOException>(() =>
			ImportOperationRunner.RunAsync(["partial.zip"], result, (_, _) =>
			{
				result.Mods.Add(new DivinityModData { UUID = "finished-first-entry" });
				throw failure;
			}, _ => Task.CompletedTask, CancellationToken.None, () =>
			{
				saved.AddRange(result.Mods.Select(mod => mod.UUID));
				return Task.CompletedTask;
			}).GetAwaiter().GetResult());
		RegressionAssert.True(ReferenceEquals(failure, actual));
		RegressionAssert.SequenceEqual(["finished-first-entry"], saved);
		RegressionAssert.False(result.WasCancelled);
	}

	public void CancellationWaitsForFileCleanupAndSkipsRemainingImports()
	{
		using var batchCancellation = new CancellationTokenSource();
		using var importCancellation = CancellationTokenSource.CreateLinkedTokenSource(batchCancellation.Token);
		var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var cleanupFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var started = new List<string>();
		var finalized = false;
		var result = new ImportOperationResults { TotalFiles = 2 };
		var operation = ImportOperationRunner.RunAsync(["large.7z", "next.pak"], result, async (file, token) =>
		{
			started.Add(file);
			try { await Task.Delay(Timeout.Infinite, token); }
			finally
			{
				cleanupStarted.SetResult();
				await cleanupFinished.Task;
			}
		}, _ => { finalized = true; return Task.CompletedTask; }, importCancellation.Token);

		batchCancellation.Cancel();
		cleanupStarted.Task.GetAwaiter().GetResult();
		RegressionAssert.False(operation.IsCompleted);
		RegressionAssert.False(result.WasCancelled);
		cleanupFinished.SetResult();
		operation.GetAwaiter().GetResult();

		RegressionAssert.SequenceEqual(["large.7z"], started);
		RegressionAssert.False(finalized);
		RegressionAssert.True(result.WasCancelled);
		RegressionAssert.False(result.Success);
		RegressionAssert.Equal(0, result.Errors.Count);
	}

	public void CancellationBetweenFilesKeepsCompletedModsAndStopsTheNextFile()
	{
		using var cancellation = new CancellationTokenSource();
		var result = new ImportOperationResults { TotalFiles = 2 };
		var started = new List<string>();
		ImportOperationRunner.RunAsync(["finished.pak", "next.pak"], result, (file, _) =>
		{
			started.Add(file);
			result.TotalPaks++;
			result.Mods.Add(new DivinityModData { UUID = "installed" });
			cancellation.Cancel();
			return Task.CompletedTask;
		}, _ => throw new InvalidOperationException("Canceled imports must not start optional metadata updates."),
			cancellation.Token).GetAwaiter().GetResult();

		RegressionAssert.SequenceEqual(["finished.pak"], started);
		RegressionAssert.Equal(1, result.Mods.Count);
		RegressionAssert.True(result.WasCancelled);
		RegressionAssert.False(result.Success);
	}

	public void PrecancelledImportStartsNoFilesAndFailuresRemainFailures()
	{
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var cancelled = new ImportOperationResults();
		ImportOperationRunner.RunAsync(["never.pak"], cancelled,
			(_, _) => throw new InvalidOperationException("A canceled import cannot start a file."),
			_ => Task.CompletedTask, cancellation.Token).GetAwaiter().GetResult();
		RegressionAssert.True(cancelled.WasCancelled);

		var failed = new ImportOperationResults();
		try
		{
			ImportOperationRunner.RunAsync(["unreadable.pak"], failed,
				(_, _) => throw new IOException("Simulated read failure."),
				_ => Task.CompletedTask, CancellationToken.None).GetAwaiter().GetResult();
			throw new InvalidOperationException("The read failure must propagate.");
		}
		catch (IOException) { }
		RegressionAssert.False(failed.WasCancelled);
	}
}
