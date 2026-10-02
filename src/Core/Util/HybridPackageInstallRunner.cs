namespace DivinityModManager.Util;

/// <summary>Completes native files and their companion PAKs before releasing the package source.</summary>
public static class HybridPackageInstallRunner
{
	public static async Task RunAsync(Func<CancellationToken, Task> commitNativeFiles,
		Func<CancellationToken, Task<bool>>? importCompanionPaks, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await commitNativeFiles(cancellationToken);
		if (importCompanionPaks == null) return;
		cancellationToken.ThrowIfCancellationRequested();
		if (!await importCompanionPaks(cancellationToken))
			throw new InvalidDataException("The game-directory files installed, but the companion PAK could not be installed. The package remains available to review and retry.");
	}
}
