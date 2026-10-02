using DivinityModManager;
using DivinityModManager.Util;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class TempFileTests
{
	public void FailedCopyClosesAndRemovesTemporaryFileBeforeReturning()
	{
		CheckFailedCopy(cancel: false);
	}

	public void CanceledCopyClosesAndRemovesTemporaryFileBeforeReturning()
	{
		CheckFailedCopy(cancel: true);
	}

	private static void CheckFailedCopy(bool cancel)
	{
		var name = "redux-temp-regression-" + Guid.NewGuid().ToString("N") + ".pak.xz";
		var path = DivinityApp.GetAppDirectory("Temp", name);
		using var cancellation = new CancellationTokenSource();
		using var source = new FailingCopyStream(cancel ? cancellation : null);
		try
		{
			if (cancel)
				RegressionAssert.Throws<OperationCanceledException>(() => TempFile.CreateAsync(name, source, cancellation.Token).GetAwaiter().GetResult());
			else
				RegressionAssert.Throws<IOException>(() => TempFile.CreateAsync(name, source, cancellation.Token).GetAwaiter().GetResult());
			RegressionAssert.False(File.Exists(path));
			// Retrying immediately must not encounter the old exclusively locked handle.
			using var retrySource = new MemoryStream(new byte[] { 1, 2, 3 });
			using (var retry = TempFile.CreateAsync(name, retrySource, CancellationToken.None).GetAwaiter().GetResult())
				RegressionAssert.Equal(3L, retry.Stream.Length);
			RegressionAssert.False(File.Exists(path));
		}
		finally { if (File.Exists(path)) File.Delete(path); }
	}

	private sealed class FailingCopyStream(CancellationTokenSource? cancellation) : MemoryStream
	{
		public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
		{
			await destination.WriteAsync(new byte[] { 1, 2, 3 }, token);
			cancellation?.Cancel();
			token.ThrowIfCancellationRequested();
			throw new IOException("Simulated decompression failure after writing output.");
		}
	}
}
