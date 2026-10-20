using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReduxUpdater;

internal static class Program
{
	private const uint MessageBoxIconError = 0x00000010;
	private const uint MessageBoxIconWarning = 0x00000030;
	private const uint MessageBoxOk = 0x00000000;

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

	[STAThread]
	private static int Main(string[] args)
	{
		if (args.Length == 2 && args[0] == "--snapshot-request") return SnapshotRunner.Run(args[1]);
		ReduxUpdateRequest? request = null;
		ReduxUpdateResult? completedUpdate = null;
		try
		{
			if (args.Length != 2 || !String.Equals(args[0], "--request", StringComparison.Ordinal))
				throw new InvalidDataException("Redux Updater received an invalid request.");
			request = ReduxUpdateTransaction.ReadRequest(args[1]);
			WaitForReduxToExit(request.ParentProcessId);
			completedUpdate = ReduxUpdateTransaction.Apply(request);
			TryWriteResult(request.ResultPath, completedUpdate);
			CleanupSuccessfulTransaction(request);

			try
			{
				Process.Start(CreateRelaunchStartInfo(request, Environment.ProcessPath));
			}
			catch (Exception ex)
			{
				var restartWarning = new ReduxUpdateResult
				{
					Succeeded = true,
					DisplayVersion = request.DisplayVersion,
					Message = $"Redux was updated to {request.DisplayVersion}, but could not restart automatically. Start Redux manually.",
					CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
				};
				TryWriteResult(request.ResultPath, restartWarning);
				MessageBox(IntPtr.Zero,
					"Redux was updated successfully, but it could not restart automatically. Start Redux.exe manually.\n\n" + ex.Message,
					"Redux Updated",
					MessageBoxOk | MessageBoxIconWarning);
			}
			return 0;
		}
		catch (Exception ex)
		{
			if (request != null && completedUpdate == null)
			{
				TryWriteResult(request.ResultPath, new ReduxUpdateResult
				{
					Succeeded = false,
					DisplayVersion = request.DisplayVersion,
					Message = ex.Message,
					CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
				});
			}
			MessageBox(IntPtr.Zero,
				"Redux could not complete the update. Your previous installation was restored when possible.\n\n" + ex.Message,
				"Redux Update Failed",
				MessageBoxOk | MessageBoxIconError);
			return 1;
		}
	}

	internal static ProcessStartInfo CreateRelaunchStartInfo(ReduxUpdateRequest request, string? updaterProcessPath)
	{
		// The updater itself was launched through the parent's Windows host, so the
		// same host can restart the updated DLL without accepting an executable path
		// from the update request. Native apphost launches retain their normal path.
		var target = ReduxLaunchTarget.ForExecutable(
			Path.Combine(request.TargetDirectory, request.RelaunchRelativePath),
			ReduxLaunchTarget.IsWindowsDotNetHost(updaterProcessPath) ? updaterProcessPath : null);
		if (target.IsDotNetHosted && !File.Exists(target.ApplicationPath))
			throw new FileNotFoundException("The updated Redux managed application is missing.", target.ApplicationPath);
		return target.CreateStartInfo();
	}

	private static void TryWriteResult(string resultPath, ReduxUpdateResult result)
	{
		try
		{
			ReduxUpdateTransaction.WriteResult(resultPath, result);
		}
		catch
		{
			// Result reporting must never turn a completed file transaction into a failed update.
		}
	}

	private static void CleanupSuccessfulTransaction(ReduxUpdateRequest request)
	{
		var transactionDirectory = Path.GetDirectoryName(request.BackupDirectory);
		if (String.IsNullOrWhiteSpace(transactionDirectory)) return;
		foreach (var path in new[]
		{
			request.StagedDirectory,
			request.BackupDirectory,
			Path.Combine(transactionDirectory, "release.zip"),
			Path.Combine(transactionDirectory, "release.zip.partial"),
			Path.Combine(transactionDirectory, "staging.partial")
		})
		{
			try
			{
				if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
				else if (File.Exists(path)) File.Delete(path);
			}
			catch { }
		}
	}

	internal static void WaitForReduxToExit(int processId)
	{
		try
		{
			using var process = Process.GetProcessById(processId);
			if (!process.WaitForExit(checked((int)TimeSpan.FromMinutes(2).TotalMilliseconds)))
				throw new TimeoutException("Redux did not close before the update timeout.");
		}
		catch (ArgumentException)
		{
			// The process already exited between launch and lookup.
		}
	}
}
