using DivinityModManager.AppServices;
using DivinityModManager.Util;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class HybridPackageInstallRunnerTests
{
	public void HybridCompletionWaitsForCompanionBeforeReleasingArchive()
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxHybridInstallTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		var path = Path.Combine(directory, "hybrid.zip");
		var bytes = new byte[] { 1, 2, 3, 4 };
		File.WriteAllBytes(path, bytes);
		var finishCompanion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var nativeCommitted = false;
		var companionStarted = false;
		try
		{
			using var lease = VerifiedPackageReadLease.OpenAsync(path, bytes.Length,
				Convert.ToHexString(SHA256.HashData(bytes))).GetAwaiter().GetResult();
			async Task InstallAndCompleteAsync()
			{
				using (lease)
					await HybridPackageInstallRunner.RunAsync(_ =>
					{
						nativeCommitted = true;
						return Task.CompletedTask;
					}, _ =>
					{
						RegressionAssert.True(nativeCommitted);
						companionStarted = true;
						return finishCompanion.Task;
					});
				File.Delete(path);
			}

			var operation = InstallAndCompleteAsync();
			RegressionAssert.True(companionStarted);
			RegressionAssert.False(operation.IsCompleted);
			RegressionAssert.True(File.Exists(path));
			RegressionAssert.Throws<IOException>(() => File.Delete(path));
			finishCompanion.SetResult(true);
			operation.GetAwaiter().GetResult();
			RegressionAssert.False(File.Exists(path));
		}
		finally { Directory.Delete(directory, true); }
	}

	public void CompanionFailureCannotReportHybridSuccess()
	{
		var nativeCommitted = false;
		RegressionAssert.Throws<InvalidDataException>(() => HybridPackageInstallRunner.RunAsync(_ =>
		{
			nativeCommitted = true;
			return Task.CompletedTask;
		}, _ => Task.FromResult(false)).GetAwaiter().GetResult());
		RegressionAssert.True(nativeCommitted);
	}

	public void HybridCancellationWaitsForCompanionCleanupAndStopsBeforeCommit()
	{
		using var cancellation = new CancellationTokenSource();
		var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var finishCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var operation = HybridPackageInstallRunner.RunAsync(_ => Task.CompletedTask, async token =>
		{
			try { await Task.Delay(Timeout.Infinite, token); }
			finally
			{
				cleanupStarted.SetResult();
				await finishCleanup.Task;
			}
			return true;
		}, cancellation.Token);
		cancellation.Cancel();
		cleanupStarted.Task.GetAwaiter().GetResult();
		RegressionAssert.False(operation.IsCompleted);
		finishCleanup.SetResult();
		RegressionAssert.Throws<OperationCanceledException>(() => operation.GetAwaiter().GetResult());
		RegressionAssert.Throws<OperationCanceledException>(() => HybridPackageInstallRunner.RunAsync(
			_ => throw new Exception("Canceled work cannot commit native files."),
			_ => throw new Exception("Canceled work cannot start companion PAKs."), cancellation.Token).GetAwaiter().GetResult());
	}
}
