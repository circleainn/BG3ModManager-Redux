using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Reflection;
using DivinityModManager.AppServices;

namespace Redux.Core.Tests;

public sealed class ReduxSnapshotTests
{
	public void SnapshotQueueCopiesCurrentRunnerAndCancellationPreservesInstall()
	{
		using var f = new Fixture();
		foreach (var name in new[] { "ReduxUpdater.exe", "ReduxUpdater.dll", "ReduxUpdater.deps.json", "ReduxUpdater.runtimeconfig.json" })
			f.Write("Redux/Updater/" + name, "runner");
		var launch = new ReduxUpdateLaunchService(ReduxLaunchTarget.ForExecutable(f.PathFor("Redux/Redux.exe"), @"C:\Program Files\dotnet\dotnet.exe"));
		string? requestPath = null;
		try
		{
			launch.QueueSnapshot(new(1, "create", 123, f.PathFor("Redux"), f.Scopes(false), "", "test", "version", false, f.PathFor("result.json")));
			RegressionAssert.True(launch.HasPendingUpdate && launch.PendingIsSnapshot);
			var pending = typeof(ReduxUpdateLaunchService).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(launch)!;
			requestPath = (string)pending.GetType().GetProperty("RequestPath")!.GetValue(pending)!;
			var request = ReduxSnapshotStore.ReadRequest(requestPath);
			RegressionAssert.Equal("create", request.Operation);
			RegressionAssert.Equal(f.PathFor("Redux"), request.ApplicationDirectory);
			var start = launch.CreateSnapshotRunnerStartInfo(Path.GetDirectoryName(requestPath)!, requestPath);
			RegressionAssert.Equal(@"C:\Program Files\dotnet\dotnet.exe", start.FileName);
			RegressionAssert.True(start.ArgumentList.Contains("--snapshot-request"));
			RegressionAssert.True(start.ArgumentList.Contains(requestPath));
			RegressionAssert.Equal("old app", f.Read("Redux/Redux.exe"));
		}
		finally { launch.CancelPending(); }
		RegressionAssert.False(launch.HasPendingUpdate);
		RegressionAssert.False(File.Exists(requestPath));
	}

	public void SnapshotRunnerAndReduxShareTheSameInstanceGuard()
	{
		var name = "ReduxSnapshotGuardTest." + Guid.NewGuid().ToString("N");
		using var main = new ReduxInstanceGuard(name);
		RegressionAssert.True(main.TryAcquire());
		var acquired = true;
		var thread = new Thread(() => { using var runner = new ReduxUpdater.ReduxInstanceGuard(name); acquired = runner.TryAcquire(); });
		thread.Start(); thread.Join();
		RegressionAssert.False(acquired);
	}

	public void FullSnapshotRestoresModsAndOrdersAndCanRollForward()
	{
		using var f = new Fixture();
		var first = f.Capture(true);
		f.Write("Redux/Redux.exe", "new app"); f.Write("Mods/Example.pak", "new mod");
		f.Write("Mods/Added.pak", "added mod"); f.Write("Profiles/Public/modsettings.lsx", "new order");
		f.Write("Redux/Data/OverrideOrderHolding/held.pak", "new held"); f.Write("Orders/order.json", "new saved order");
		f.Write("Redux/Data/GameDirectoryInstalls/ownership.json", "current native ownership");
		f.Store.RestoreAsync(first, f.Scopes(true), "new version").GetAwaiter().GetResult();
		RegressionAssert.Equal("old app", f.Read("Redux/Redux.exe"));
		RegressionAssert.Equal("old mod", f.Read("Mods/Example.pak"));
		RegressionAssert.False(File.Exists(f.PathFor("Mods/Added.pak")));
		RegressionAssert.Equal("old order", f.Read("Profiles/Public/modsettings.lsx"));
		RegressionAssert.Equal("save untouched", f.Read("Profiles/Public/Savegames/save.lsv"));
		RegressionAssert.Equal("old held", f.Read("Redux/Data/OverrideOrderHolding/held.pak"));
		RegressionAssert.Equal("old saved order", f.Read("Orders/order.json"));
		RegressionAssert.Equal("current native ownership", f.Read("Redux/Data/GameDirectoryInstalls/ownership.json"));
		var back = f.Store.List().Single(x => x.Name.StartsWith("Before restoring", StringComparison.Ordinal));
		f.Store.RestoreAsync(back.FileName, f.Scopes(true), "old version").GetAwaiter().GetResult();
		RegressionAssert.Equal("new app", f.Read("Redux/Redux.exe"));
		RegressionAssert.Equal("new mod", f.Read("Mods/Example.pak"));
		RegressionAssert.Equal("added mod", f.Read("Mods/Added.pak"));
		RegressionAssert.Equal("new order", f.Read("Profiles/Public/modsettings.lsx"));
		RegressionAssert.Equal("new held", f.Read("Redux/Data/OverrideOrderHolding/held.pak"));
		RegressionAssert.False(f.Store.NeedsRecovery);
		RegressionAssert.True(File.Exists(Path.Combine(f.Store.DirectoryPath, first)));
	}

	public void ReduxOnlySnapshotExcludesModsLogsAndRecursiveBackups()
	{
		using var f = new Fixture();
		f.Write("Redux/Backups/keep.txt", "existing backup");
		f.Write("Redux/_Logs/log.txt", "private log");
		var snapshot = f.Capture(false);
		using (var archive = ZipFile.OpenRead(Path.Combine(f.Store.DirectoryPath, snapshot)))
		{
			RegressionAssert.False(archive.Entries.Any(e => e.FullName.Contains("held.pak") || e.FullName.Contains("Backups/") || e.FullName.Contains("log.txt")));
		}
		f.Write("Mods/Example.pak", "new mod"); f.Write("Redux/Data/OverrideOrderHolding/held.pak", "new held");
		f.Write("Profiles/Public/modsettings.lsx", "new order"); f.Write("Redux/Redux.exe", "new app");
		f.Store.RestoreAsync(snapshot, f.Scopes(false), "new version").GetAwaiter().GetResult();
		RegressionAssert.Equal("old app", f.Read("Redux/Redux.exe"));
		RegressionAssert.Equal("new mod", f.Read("Mods/Example.pak"));
		RegressionAssert.Equal("new held", f.Read("Redux/Data/OverrideOrderHolding/held.pak"));
		RegressionAssert.Equal("new order", f.Read("Profiles/Public/modsettings.lsx"));
		RegressionAssert.Equal("existing backup", f.Read("Redux/Backups/keep.txt"));
	}

	public void CanceledSnapshotNeverPublishesOrOverwritesAnArchive()
	{
		using var f = new Fixture();
		var saved = f.Capture(false);
		var bytes = File.ReadAllBytes(Path.Combine(f.Store.DirectoryPath, saved));
		using var cancel = new CancellationTokenSource();
		var progress = new ImmediateProgress<ReduxSnapshotStore.Progress>(_ => cancel.Cancel());
		RegressionAssert.Throws<OperationCanceledException>(() => f.Store.CreateAsync("cancel", "version", false,
			f.Scopes(false), progress, cancel.Token).GetAwaiter().GetResult());
		RegressionAssert.Equal(1, f.Store.List().Count);
		RegressionAssert.SequenceEqual(bytes, File.ReadAllBytes(Path.Combine(f.Store.DirectoryPath, saved)));
		RegressionAssert.False(Directory.EnumerateFiles(f.Store.DirectoryPath).Any(p => p.EndsWith(".partial") || p.EndsWith(".building")));
	}

	public void CorruptSnapshotIsRejectedBeforeCreatingReturnSnapshotOrWritingTargets()
	{
		using var f = new Fixture();
		var saved = f.Capture(true);
		using (var zip = ZipFile.Open(Path.Combine(f.Store.DirectoryPath, saved), ZipArchiveMode.Update))
		{
			zip.GetEntry("Redux/Redux.exe")!.Delete();
			using var writer = new StreamWriter(zip.CreateEntry("Redux/Redux.exe").Open());
			writer.Write("bad app");
		}
		f.Write("Redux/Redux.exe", "current app");
		RegressionAssert.Throws<InvalidDataException>(() => f.Store.RestoreAsync(saved, f.Scopes(true), "version").GetAwaiter().GetResult());
		RegressionAssert.Equal("current app", f.Read("Redux/Redux.exe"));
		RegressionAssert.Equal(1, f.Store.List().Count);
		RegressionAssert.False(f.Store.NeedsRecovery);
	}

	public void SnapshotTraversalAndExcludedDestinationsCannotBeRestored()
	{
		foreach (var path in new[] { "../outside.txt", "Backups/Snapshots/owned.txt", "Data/OverrideOrderHolding/held.pak", "file.txt:stream", "CON.txt" })
		{
			using var f = new Fixture();
			var saved = f.Capture(false);
			using (var zip = ZipFile.Open(Path.Combine(f.Store.DirectoryPath, saved), ZipArchiveMode.Update))
			{
				JsonObject manifest;
				using (var reader = new StreamReader(zip.GetEntry("Redux-Snapshot.json")!.Open())) manifest = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
				var file = manifest["files"]!.AsArray().First(n => n!["scope"]!.GetValue<string>() == "Redux")!;
				var old = "Redux/" + file["path"]!.GetValue<string>();
				byte[] data;
				using (var input = zip.GetEntry(old)!.Open()) { using var output = new MemoryStream(); input.CopyTo(output); data = output.ToArray(); }
				zip.GetEntry(old)!.Delete();
				using (var output = zip.CreateEntry("Redux/" + path).Open()) output.Write(data);
				file["path"] = path;
				zip.GetEntry("Redux-Snapshot.json")!.Delete();
				using var writer = new StreamWriter(zip.CreateEntry("Redux-Snapshot.json").Open()); writer.Write(manifest.ToJsonString());
			}
			RegressionAssert.Throws<InvalidDataException>(() => f.Store.RestoreAsync(saved, f.Scopes(false), "version").GetAwaiter().GetResult());
			RegressionAssert.Equal("old app", f.Read("Redux/Redux.exe"));
			RegressionAssert.False(File.Exists(f.PathFor("outside.txt")));
		}
	}

	public void SnapshotRestoreRollsBackOnReadOnlyDestinationFailure()
	{
		using var f = new Fixture();
		f.Write("Redux/z-file.txt", "old settings");
		var saved = f.Capture(false);
		f.Write("Redux/Redux.exe", "new app"); f.Write("Redux/z-file.txt", "new settings");
		var blocked = f.PathFor("Redux/z-file.txt");
		File.SetAttributes(blocked, FileAttributes.ReadOnly);
		try
		{
			var failed = false;
			try { f.Store.RestoreAsync(saved, f.Scopes(false), "new version").GetAwaiter().GetResult(); }
			catch (UnauthorizedAccessException) { failed = true; }
			catch (IOException) { failed = true; }
			RegressionAssert.True(failed);
			RegressionAssert.Equal("new app", f.Read("Redux/Redux.exe"));
			RegressionAssert.Equal("new settings", f.Read("Redux/z-file.txt"));
			RegressionAssert.False(f.Store.NeedsRecovery);
			RegressionAssert.Equal(2, f.Store.List().Count);
		}
		finally { File.SetAttributes(blocked, FileAttributes.Normal); }
	}

	public void InterruptedSnapshotRestoreRecoversFromDurableReturnSnapshot()
	{
		using var f = new Fixture();
		var before = f.Capture(true);
		File.WriteAllText(Path.Combine(f.Store.DirectoryPath, "pending-restore.json"), JsonSerializer.Serialize(new
		{
			schemaVersion = 1, beforeSnapshot = before,
			scopes = f.Scopes(true).Select(s => new { id = s.Id, directory = s.Directory, profileLoadOrdersOnly = s.ProfileLoadOrdersOnly }), includeMods = true
		}));
		f.Write("Redux/Redux.exe", "interrupted app"); f.Write("Mods/Extra.pak", "interrupted mod");
		RegressionAssert.True(f.Store.NeedsRecovery);
		RegressionAssert.Throws<IOException>(() => f.Capture(false));
		// A fresh instance models a different process after interruption.
		var reopened = new ReduxSnapshotStore(f.PathFor("Redux"));
		reopened.RecoverAsync(f.Scopes(true)).GetAwaiter().GetResult();
		RegressionAssert.Equal("old app", f.Read("Redux/Redux.exe"));
		RegressionAssert.False(File.Exists(f.PathFor("Mods/Extra.pak")));
		RegressionAssert.False(reopened.NeedsRecovery);
	}

	public void SnapshotRejectsMovedModRootsAndLinkedFiles()
	{
		using var f = new Fixture();
		var saved = f.Capture(true);
		Directory.CreateDirectory(f.PathFor("OtherMods"));
		var changed = f.Scopes(true).Select(s => s.Id == "Mods" ? s with { Directory = f.PathFor("OtherMods") } : s).ToArray();
		RegressionAssert.Throws<InvalidDataException>(() => f.Store.RestoreAsync(saved, changed, "version").GetAwaiter().GetResult());
		// A junction must not be traversed even when its target is inside the fixture.
		var link = f.PathFor("Redux/LinkedMods");
		var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
		start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(link); start.ArgumentList.Add(f.PathFor("Mods"));
		using var process = System.Diagnostics.Process.Start(start)!; process.WaitForExit();
		RegressionAssert.Equal(0, process.ExitCode);
		try { RegressionAssert.Throws<IOException>(() => f.Capture(false)); }
		finally { Directory.Delete(link); }
		RegressionAssert.Equal("old mod", f.Read("Mods/Example.pak"));
	}

	private sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
	private sealed class Fixture : IDisposable
	{
		private readonly string _root = Path.Combine(Path.GetTempPath(), "ReduxSnapshotTests", Guid.NewGuid().ToString("N"));
		public ReduxSnapshotStore Store { get; }
		public Fixture()
		{
			Write("Redux/Redux.exe", "old app"); Write("Redux/Redux.dll", "old managed"); Write("Redux/Data/settings.json", "settings");
			Write("Redux/Data/OverrideOrderHolding/held.pak", "old held"); Write("Mods/Example.pak", "old mod");
			Write("Orders/order.json", "old saved order"); Write("Profiles/Public/modsettings.lsx", "old order");
			Write("Profiles/Public/Savegames/save.lsv", "save untouched");
			Store = new ReduxSnapshotStore(PathFor("Redux"));
		}
		public ReduxSnapshotStore.Scope[] Scopes(bool mods) => mods
			? [new("Redux", PathFor("Redux")), new("Orders", PathFor("Orders")), new("Mods", PathFor("Mods")), new("Profiles", PathFor("Profiles"), true)]
			: [new("Redux", PathFor("Redux")), new("Orders", PathFor("Orders"))];
		public string Capture(bool mods) => Store.CreateAsync("Example", "0.1.0-alpha.16.6", mods, Scopes(mods)).GetAwaiter().GetResult();
		public string PathFor(string relative) => Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		public void Write(string path, string text) { var full = PathFor(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllText(full, text); }
		public string Read(string path) => File.ReadAllText(PathFor(path));
		public void Dispose() => Directory.Delete(_root, true);
	}
}
