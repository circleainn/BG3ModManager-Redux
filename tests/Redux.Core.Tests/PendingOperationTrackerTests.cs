using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class PendingOperationTrackerTests
{
	public void ShutdownWaitsForImportCleanupAndOuterReviewRelease()
	{
		var tracker = new PendingOperationTracker();
		using var cancellation = new CancellationTokenSource();
		var reviewCancelled = false;
		using var review = tracker.TryRegister(() => reviewCancelled = true)!;
		var import = tracker.TryRegister(cancellation.Cancel)!;
		var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var finishCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var finalizationFinished = false;
		async Task ImportAsync()
		{
			using (import)
			{
				try { await Task.Delay(Timeout.Infinite, cancellation.Token); }
				catch (OperationCanceledException) { }
				finally
				{
					cleanupStarted.SetResult();
					await finishCleanup.Task;
					finalizationFinished = true;
				}
			}
		}
		var work = ImportAsync();
		var shutdown = tracker.StopAsync();
		cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
		RegressionAssert.True(reviewCancelled);
		RegressionAssert.False(shutdown.IsCompleted);
		RegressionAssert.False(finalizationFinished);
		RegressionAssert.True(tracker.TryRegister(() => throw new InvalidOperationException()) == null);

		finishCleanup.SetResult();
		work.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
		RegressionAssert.True(finalizationFinished);
		RegressionAssert.False(shutdown.IsCompleted);
		// Review still owns the verified archive handle and download state update.
		review.Dispose();
		shutdown.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
	}

	public void RepeatedShutdownRequestsCancelEachOperationOnce()
	{
		var tracker = new PendingOperationTracker();
		var cancellations = 0;
		using var operation = tracker.TryRegister(() => cancellations++)!;
		var first = tracker.StopAsync();
		var second = tracker.StopAsync();
		RegressionAssert.Equal(1, cancellations);
		RegressionAssert.False(first.IsCompleted);
		RegressionAssert.False(second.IsCompleted);
		operation.Dispose();
		Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
	}

	public void DisposedCancellationSourceStillWaitsForOperationCompletion()
	{
		var tracker = new PendingOperationTracker();
		var cancellation = new CancellationTokenSource();
		using var operation = tracker.TryRegister(cancellation.Cancel)!;
		// UI completion may dispose the token before its final callback returns.
		cancellation.Dispose();
		var shutdown = tracker.StopAsync();
		RegressionAssert.False(shutdown.IsCompleted);
		operation.Dispose();
		shutdown.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
	}

	public void FailedCancellationDrainsBeforeAdmissionCanResume()
	{
		var tracker = new PendingOperationTracker();
		using var operation = tracker.TryRegister(() => throw new IOException("Simulated cancellation callback failure."))!;
		var shutdown = tracker.StopAsync();
		RegressionAssert.False(shutdown.IsCompleted);
		RegressionAssert.Throws<InvalidOperationException>(tracker.ResumeAfterFailedShutdown);
		operation.Dispose();
		RegressionAssert.Throws<AggregateException>(() => shutdown.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());

		tracker.ResumeAfterFailedShutdown();
		var cancelled = false;
		using var retry = tracker.TryRegister(() => cancelled = true);
		RegressionAssert.True(retry != null);
		var secondShutdown = tracker.StopAsync();
		RegressionAssert.True(cancelled);
		retry!.Dispose();
		secondShutdown.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
	}
}
