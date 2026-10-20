using DivinityModManager.AppServices;
using DivinityModManager.Models.Updates;
using System;
using System.IO;

namespace Redux.Core.Tests;

public sealed class ReduxLaunchTargetTests
{
	public void NativeAndHostedLaunchesKeepAppDirectoryAndExactArgumentBoundaries()
	{
		const string app = @"D:\Mod tools & games\Redux\Redux.dll";
		const string host = @"C:\Program Files\dotnet\dotnet.exe";
		var arguments = new[] { "--nxm", "nxm://baldursgate3/mods/1/files/2?key=a%2Bb%3D&expires=123", "embedded \"quote\" and trailing slash\\" };
		var hosted = ReduxLaunchTarget.Resolve(host, app, @"D:\Mod tools & games\Redux");
		var start = hosted.CreateStartInfo(arguments);
		RegressionAssert.True(hosted.IsDotNetHosted);
		RegressionAssert.Equal(Path.GetDirectoryName(app), hosted.ApplicationDirectory);
		RegressionAssert.Equal(host, start.FileName);
		RegressionAssert.Equal(hosted.ApplicationDirectory, start.WorkingDirectory);
		RegressionAssert.False(start.UseShellExecute);
		RegressionAssert.SequenceEqual(new[] { app, arguments[0], arguments[1], arguments[2] }, start.ArgumentList);
		RegressionAssert.Equal(String.Empty, start.Arguments);

		var native = ReduxLaunchTarget.Resolve(Path.ChangeExtension(app, ".exe"), app, hosted.ApplicationDirectory);
		start = native.CreateStartInfo(arguments);
		RegressionAssert.False(native.IsDotNetHosted);
		RegressionAssert.Equal(Path.ChangeExtension(app, ".exe"), start.FileName);
		RegressionAssert.Equal(hosted.ApplicationDirectory, start.WorkingDirectory);
		RegressionAssert.SequenceEqual(arguments, start.ArgumentList);
		RegressionAssert.Equal(Path.ChangeExtension(app, ".exe"), ReduxLaunchTarget.Resolve(null, null, hosted.ApplicationDirectory).ExecutablePath);
	}

	public void HostedLaunchRejectsRelativeUnsafeOrNonWindowsHostPaths()
	{
		RegressionAssert.Throws<ArgumentException>(() => ReduxLaunchTarget.Resolve(@"C:\Runtime\dotnet.exe", null, @"C:\Redux"));
		RegressionAssert.Throws<ArgumentException>(() => ReduxLaunchTarget.Resolve(@"C:\Runtime\dotnet.exe", "Redux.dll", @"C:\Redux"));
		RegressionAssert.Throws<ArgumentException>(() => ReduxLaunchTarget.ForManagedApplication(@"C:\Redux\Redux.dll", @"C:\Windows\cmd.exe"));
		RegressionAssert.Throws<ArgumentException>(() => ReduxLaunchTarget.ForManagedApplication(@"C:\Redux\Redux.exe", @"C:\Runtime\dotnet.exe"));
		RegressionAssert.Throws<ArgumentException>(() => ReduxLaunchTarget.ForManagedApplication("C:\\Redux\\bad\"name.dll", @"C:\Runtime\dotnet.exe"));
		RegressionAssert.Throws<ArgumentException>(() => ReduxLaunchTarget.ForManagedApplication("C:\\Redux\\bad\nname.dll", @"C:\Runtime\dotnet.exe"));
		RegressionAssert.Throws<ArgumentException>(() => ReduxLaunchTarget.ForExecutable("Redux.exe"));
	}

	public void UpdateRunnerAndRestartPreserveWindowsDotNetHostWithoutChangingRequestSchema()
	{
		var root = Path.Combine(Path.GetTempPath(), "Redux launch & request " + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var target = Path.Combine(root, "installation");
			var transaction = Path.Combine(root, "transaction");
			var staged = Path.Combine(transaction, "staged");
			Directory.CreateDirectory(target);
			Directory.CreateDirectory(Path.Combine(staged, "Updater"));
			File.WriteAllText(Path.Combine(target, "Redux.exe"), "fake native app; never executed");
			File.WriteAllText(Path.Combine(target, "Redux.dll"), "fake managed app; never executed");
			foreach (var name in new[] { "ReduxUpdater.exe", "ReduxUpdater.dll", "ReduxUpdater.deps.json", "ReduxUpdater.runtimeconfig.json" })
				File.WriteAllText(Path.Combine(staged, "Updater", name), name);
			var host = Path.Combine(root, "Windows runtime", "dotnet.exe");
			var current = ReduxLaunchTarget.ForManagedApplication(Path.Combine(target, "Redux.dll"), host);
			var service = new ReduxUpdateLaunchService(current);
			service.Queue(new ReduxPreparedUpdate { DisplayVersion = "test", TransactionDirectory = transaction, StagedDirectory = staged }, target, 42);
			var runner = Path.Combine(transaction, "runner");
			var requestPath = Path.Combine(runner, "request.json");
			var start = service.CreateRunnerStartInfo(runner, requestPath);
			RegressionAssert.Equal(host, start.FileName);
			RegressionAssert.False(start.UseShellExecute);
			RegressionAssert.Equal(runner, start.WorkingDirectory);
			RegressionAssert.SequenceEqual(new[] { Path.Combine(runner, "ReduxUpdater.dll"), "--request", requestPath }, start.ArgumentList);
			var request = ReduxUpdater.ReduxUpdateTransaction.ReadRequest(requestPath);
			RegressionAssert.Equal(target, request.TargetDirectory);
			RegressionAssert.Equal("Redux.exe", request.RelaunchRelativePath);
			var restart = ReduxUpdater.Program.CreateRelaunchStartInfo(request, host);
			RegressionAssert.Equal(host, restart.FileName);
			RegressionAssert.Equal(target, restart.WorkingDirectory);
			RegressionAssert.False(restart.UseShellExecute);
			RegressionAssert.SequenceEqual(new[] { Path.Combine(target, "Redux.dll") }, restart.ArgumentList);

			var nativeService = new ReduxUpdateLaunchService(ReduxLaunchTarget.ForExecutable(Path.Combine(target, "Redux.exe")));
			var nativeRunner = nativeService.CreateRunnerStartInfo(runner, requestPath);
			RegressionAssert.Equal(Path.Combine(runner, "ReduxUpdater.exe"), nativeRunner.FileName);
			RegressionAssert.SequenceEqual(new[] { "--request", requestPath }, nativeRunner.ArgumentList);
			restart = ReduxUpdater.Program.CreateRelaunchStartInfo(request, nativeRunner.FileName);
			RegressionAssert.Equal(Path.Combine(target, "Redux.exe"), restart.FileName);
			RegressionAssert.Equal(0, restart.ArgumentList.Count);
			service.CancelPending();
			RegressionAssert.False(Directory.Exists(transaction));
		}
		finally { Directory.Delete(root, recursive: true); }
	}

	public void HostedUpdaterCannotReportRestartWithMissingManagedApplication()
	{
		var root = Path.Combine(Path.GetTempPath(), "redux-missing-managed-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var request = new ReduxUpdater.ReduxUpdateRequest { TargetDirectory = root, RelaunchRelativePath = "Redux.exe" };
			RegressionAssert.Throws<FileNotFoundException>(() => ReduxUpdater.Program.CreateRelaunchStartInfo(request, @"C:\Runtime\dotnet.exe"));
		}
		finally { Directory.Delete(root); }
	}
}
