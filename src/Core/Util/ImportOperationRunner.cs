using DivinityModManager.Models.App;

namespace DivinityModManager.Util;

/// <summary>Stops a sequential import only after the current file has finished cleaning up.</summary>
public static class ImportOperationRunner
{
	public static async Task RunAsync(IEnumerable<string> files, ImportOperationResults result,
		Func<string, CancellationToken, Task> importFile, Func<CancellationToken, Task> finishImport,
		CancellationToken cancellationToken, Func<Task> preserveCompletedImports = null)
	{
		try
		{
			foreach (var file in files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				await importFile(file, cancellationToken);
			}
			cancellationToken.ThrowIfCancellationRequested();
			await finishImport(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			result.WasCancelled = true;
		}
		finally
		{
			// Committed files survive cancellation. Their local source records must survive too;
			// this callback deliberately does not receive the canceled import token.
			if (preserveCompletedImports != null) await preserveCompletedImports();
		}
	}
}
