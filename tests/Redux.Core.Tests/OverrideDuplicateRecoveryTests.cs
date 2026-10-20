using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using DivinityModManager.AppServices;
using LSLib.LS;

namespace Redux.Core.Tests;

public sealed class OverrideDuplicateRecoveryTests
{
	public void IdenticalCopiesRequireReviewAndRetainTheDisplacedBytes()
	{
		WithFolders((mods, held, backups) =>
		{
			WriteCopies(mods, held, "same", "same");
			var service = new OverrideDuplicateRecoveryService(mods, held, backups);
			var review = service.ReviewAsync().GetAwaiter().GetResult();
			RegressionAssert.True(review.Conflicts.Single().Identical);
			RegressionAssert.True(File.Exists(Path.Combine(held, "Example.pak")));
			var result = service.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepMods)).GetAwaiter().GetResult();
			RegressionAssert.Equal("same", File.ReadAllText(Path.Combine(mods, "Example.pak")));
			RegressionAssert.False(File.Exists(Path.Combine(held, "Example.pak")));
			RegressionAssert.Equal("same", File.ReadAllText(Path.Combine(result.BackupDirectory, "Held", "Example.pak")));
			RegressionAssert.True(File.Exists(Path.Combine(result.BackupDirectory, "cleaned.json")));
			RegressionAssert.False(Directory.EnumerateDirectories(held).Any());
		});
	}

	public void DifferentCopiesWithEqualMetadataNeedAnExplicitChoiceAndNeverActivateHeld()
	{
		WithFolders((mods, held, backups) =>
		{
			WriteCopies(mods, held, "old!", "new!");
			var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			File.SetLastWriteTimeUtc(Path.Combine(mods, "Example.pak"), timestamp);
			File.SetLastWriteTimeUtc(Path.Combine(held, "Example.pak"), timestamp);
			var service = new OverrideDuplicateRecoveryService(mods, held, backups);
			var review = service.ReviewAsync().GetAwaiter().GetResult();
			RegressionAssert.False(review.Conflicts.Single().Identical);
			RegressionAssert.Throws<ArgumentException>(() => service.ApplyAsync(review,
				new Dictionary<string, OverrideDuplicateChoice>()).GetAwaiter().GetResult());
			var result = service.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepHeld)).GetAwaiter().GetResult();
			RegressionAssert.False(File.Exists(Path.Combine(mods, "Example.pak")));
			RegressionAssert.Equal("new!", File.ReadAllText(Path.Combine(held, "Example.pak")));
			RegressionAssert.Equal("old!", File.ReadAllText(Path.Combine(result.BackupDirectory, "Mods", "Example.pak")));
		});
	}

	public void CompletedBackupsDoNotBlockChangedFolderTargets()
	{
		WithFolders((mods, held, backups) =>
		{
			WriteCopies(mods, held, "keep", "save");
			var original = new OverrideDuplicateRecoveryService(mods, held, backups);
			var review = original.ReviewAsync().GetAwaiter().GetResult();
			var result = original.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepMods)).GetAwaiter().GetResult();
			var cleaned = Path.Combine(result.BackupDirectory, "cleaned.json");
			RegressionAssert.True(File.Exists(cleaned));
			RegressionAssert.False(File.Exists(Path.Combine(result.BackupDirectory, "resolved.json")));
			var fixture = Path.GetDirectoryName(mods)!;
			var nextMods = Directory.CreateDirectory(Path.Combine(fixture, "NextMods")).FullName;
			var nextHeld = Directory.CreateDirectory(Path.Combine(fixture, "NextHeld")).FullName;
			var next = new OverrideDuplicateRecoveryService(nextMods, nextHeld, backups);
			next.RecoverPendingAsync().GetAwaiter().GetResult();
			RegressionAssert.Equal(0, next.ReviewAsync().GetAwaiter().GetResult().Conflicts.Count);
			RegressionAssert.Equal("keep", File.ReadAllText(Path.Combine(mods, "Example.pak")));
			RegressionAssert.Equal("save", File.ReadAllText(Path.Combine(result.BackupDirectory, "Held", "Example.pak")));
			// An unfinished cleanup record still pins its original folder targets.
			File.Move(cleaned, Path.Combine(result.BackupDirectory, "resolved.json"));
			RegressionAssert.Throws<IOException>(() => next.RecoverPendingAsync().GetAwaiter().GetResult());
			RegressionAssert.Equal("save", File.ReadAllText(Path.Combine(result.BackupDirectory, "Held", "Example.pak")));
		});
	}

	public void SameLengthAndTimestampReplacementAfterReviewIsRejected()
	{
		WithFolders((mods, held, backups) =>
		{
			WriteCopies(mods, held, "same", "same");
			var service = new OverrideDuplicateRecoveryService(mods, held, backups);
			var review = service.ReviewAsync().GetAwaiter().GetResult();
			var path = Path.Combine(held, "Example.pak");
			var timestamp = File.GetLastWriteTimeUtc(path);
			File.WriteAllText(path, "edit");
			File.SetLastWriteTimeUtc(path, timestamp);
			RegressionAssert.Throws<IOException>(() => service.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepMods)).GetAwaiter().GetResult());
			RegressionAssert.Equal("edit", File.ReadAllText(path));
			RegressionAssert.True(File.Exists(Path.Combine(mods, "Example.pak")));
			RegressionAssert.False(Directory.Exists(backups));
		});
	}

	public void FailureAfterStagingRollsBackWithoutDeletingBackups()
	{
		WithFolders((mods, held, backups) =>
		{
			WriteCopies(mods, held, "keep", "save");
			var service = new OverrideDuplicateRecoveryService(mods, held, backups)
			{ AfterStagedFile = _ => throw new IOException("Injected stage failure") };
			var review = service.ReviewAsync().GetAwaiter().GetResult();
			RegressionAssert.Throws<IOException>(() => service.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepMods)).GetAwaiter().GetResult());
			RegressionAssert.Equal("keep", File.ReadAllText(Path.Combine(mods, "Example.pak")));
			RegressionAssert.Equal("save", File.ReadAllText(Path.Combine(held, "Example.pak")));
			var transaction = Directory.GetDirectories(backups).Single();
			RegressionAssert.Equal("save", File.ReadAllText(Path.Combine(transaction, "Held", "Example.pak")));
			RegressionAssert.True(File.Exists(Path.Combine(transaction, "rolled-back.json")));
		});
	}

	public void FailedRollbackRetainsBothCopiesAndRestartRecoveryRetriesSafely()
	{
		WithFolders((mods, held, backups) =>
		{
			WriteCopies(mods, held, "keep", "save");
			var service = new OverrideDuplicateRecoveryService(mods, held, backups)
			{
				AfterStagedFile = _ =>
				{
					File.WriteAllText(Path.Combine(held, "Example.pak"), "external");
					throw new IOException("Injected occupied rollback target");
				}
			};
			var review = service.ReviewAsync().GetAwaiter().GetResult();
			RegressionAssert.Throws<IOException>(() => service.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepMods)).GetAwaiter().GetResult());
			var transaction = Directory.GetDirectories(backups).Single();
			RegressionAssert.True(File.Exists(Path.Combine(transaction, "pending.json")));
			RegressionAssert.Equal("external", File.ReadAllText(Path.Combine(held, "Example.pak")));
			RegressionAssert.Equal("save", File.ReadAllText(Path.Combine(transaction, "Held", "Example.pak")));
			RegressionAssert.Throws<IOException>(() => service.ReviewAsync().GetAwaiter().GetResult());
			File.Move(Path.Combine(held, "Example.pak"), Path.Combine(Path.GetDirectoryName(backups)!, "External.pak"));
			new OverrideDuplicateRecoveryService(mods, held, backups).RecoverPendingAsync().GetAwaiter().GetResult();
			RegressionAssert.Equal("save", File.ReadAllText(Path.Combine(held, "Example.pak")));
			RegressionAssert.False(File.Exists(Path.Combine(transaction, "pending.json")));
		});
	}

	public void CancellationAfterStagingRestoresBothCopies()
	{
		WithFolders((mods, held, backups) =>
		{
			WriteCopies(mods, held, "keep", "save");
			using var cancellation = new CancellationTokenSource();
			var service = new OverrideDuplicateRecoveryService(mods, held, backups)
			{ AfterStagedFile = _ => cancellation.Cancel() };
			var review = service.ReviewAsync().GetAwaiter().GetResult();
			RegressionAssert.Throws<OperationCanceledException>(() => service.ApplyAsync(review,
				Choices(OverrideDuplicateChoice.KeepMods), cancellation.Token).GetAwaiter().GetResult());
			RegressionAssert.Equal("keep", File.ReadAllText(Path.Combine(mods, "Example.pak")));
			RegressionAssert.Equal("save", File.ReadAllText(Path.Combine(held, "Example.pak")));
			var transaction = Directory.GetDirectories(backups).Single();
			RegressionAssert.Equal("save", File.ReadAllText(Path.Combine(transaction, "Held", "Example.pak")));
			RegressionAssert.True(File.Exists(Path.Combine(transaction, "rolled-back.json")));
			RegressionAssert.False(File.Exists(Path.Combine(transaction, "pending.json")));
			RegressionAssert.False(Directory.EnumerateDirectories(held).Any());
		});
	}

	public void RestartRestoresAnInterruptedStagedMoveWithoutOverwriting()
	{
		WithFolders((mods, held, backups) =>
		{
			WriteCopies(mods, held, "keep", "save");
			var service = new OverrideDuplicateRecoveryService(mods, held, backups);
			var file = service.ReviewAsync().GetAwaiter().GetResult().Conflicts.Single().HeldFiles.Single();
			var id = Guid.NewGuid().ToString("N");
			var transaction = Path.Combine(backups, id);
			var stage = Path.Combine(held, ".redux-duplicate-" + id);
			Directory.CreateDirectory(transaction);
			Directory.CreateDirectory(stage);
			File.WriteAllText(Path.Combine(transaction, "pending.json"), JsonSerializer.Serialize(new
			{ Id = id, ModsTarget = mods, HoldingTarget = held, Files = new[] { new { File = file, FromMods = false } } }));
			File.Move(file.Path, Path.Combine(stage, file.FileName));
			service.RecoverPendingAsync().GetAwaiter().GetResult();
			RegressionAssert.Equal("save", File.ReadAllText(file.Path));
			RegressionAssert.False(Directory.Exists(stage));
		});
	}

	public void MultipartConflictMovesTheCompleteDisplacedSetAndRejectsMissingParts()
	{
		WithFolders((mods, held, backups) =>
		{
			var primary = Path.Combine(mods, "Example.pak");
			var part = PakFileSet.PartPath(primary, 1);
			WriteMultipart(primary, part);
			File.Copy(primary, Path.Combine(held, "Example.pak"));
			File.Copy(part, Path.Combine(held, Path.GetFileName(part)));
			var service = new OverrideDuplicateRecoveryService(mods, held, backups);
			var review = service.ReviewAsync().GetAwaiter().GetResult();
			RegressionAssert.Equal(1, review.Conflicts.Count);
			RegressionAssert.Equal(2, review.Conflicts.Single().HeldFiles.Count);
			var heldPart = Path.Combine(held, Path.GetFileName(part));
			File.Delete(heldPart);
			RegressionAssert.Throws<InvalidDataException>(() => service.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepMods)).GetAwaiter().GetResult());
			File.Copy(part, heldPart);
			review = service.ReviewAsync().GetAwaiter().GetResult();
			var result = service.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepMods)).GetAwaiter().GetResult();
			RegressionAssert.Equal(0, Directory.GetFiles(held, "*.pak").Length);
			RegressionAssert.Equal(2, PakFileSet.GetPaths(Path.Combine(result.BackupDirectory, "Held", "Example.pak")).Count);
			RegressionAssert.Equal(2, PakFileSet.GetPaths(primary).Count);
			// A different single-file replacement must displace the whole old set, not orphan its part.
			File.WriteAllText(Path.Combine(held, "Example.pak"), "replacement");
			review = service.ReviewAsync().GetAwaiter().GetResult();
			RegressionAssert.False(review.Conflicts.Single().Identical);
			result = service.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepHeld)).GetAwaiter().GetResult();
			RegressionAssert.Equal(0, Directory.GetFiles(mods, "*.pak").Length);
			RegressionAssert.Equal("replacement", File.ReadAllText(Path.Combine(held, "Example.pak")));
			RegressionAssert.Equal(2, PakFileSet.GetPaths(Path.Combine(result.BackupDirectory, "Mods", "Example.pak")).Count);
		});
	}

	public void UnknownInstalledCollisionIsReportedWithoutTreatingItAsAnOverride()
	{
		WithFolders((mods, held, backups) =>
		{
			WriteCopies(mods, held, "unknown installed package", "held override");
			try { new OverrideOrderFileService(mods, held).Review([], ["Example.pak"]); throw new Exception("Collision was not reported."); }
			catch (OverrideOrderFileService.DuplicateConflictException ex)
			{
				RegressionAssert.Equal(Path.Combine(mods, "Example.pak"), ex.Conflicts.Single().ModsPath);
				RegressionAssert.Equal(Path.Combine(held, "Example.pak"), ex.Conflicts.Single().HeldPath);
			}
			RegressionAssert.Equal("unknown installed package", File.ReadAllText(Path.Combine(mods, "Example.pak")));
		});
	}

	public void RetargetedModsJunctionAndLinkedBackupFoldersAreRejected()
	{
		WithFolders((mods, held, backups) =>
		{
			WriteCopies(mods, held, "keep", "save");
			var root = Path.GetDirectoryName(mods)!;
			var link = Path.Combine(root, "ModsLink");
			var other = Path.Combine(root, "OtherMods");
			Directory.CreateDirectory(other);
			CreateJunction(link, mods);
			try
			{
				var service = new OverrideDuplicateRecoveryService(link, held, backups);
				var review = service.ReviewAsync().GetAwaiter().GetResult();
				Directory.Delete(link);
				CreateJunction(link, other);
				RegressionAssert.Throws<IOException>(() => service.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepMods)).GetAwaiter().GetResult());
				RegressionAssert.True(File.Exists(Path.Combine(mods, "Example.pak")));
				RegressionAssert.True(File.Exists(Path.Combine(held, "Example.pak")));
				RegressionAssert.Throws<IOException>(() => new OverrideDuplicateRecoveryService(mods, held, Path.Combine(link, "Backups")));
				Directory.Delete(link);
				CreateJunction(link, mods);
				service.ApplyAsync(review, Choices(OverrideDuplicateChoice.KeepMods)).GetAwaiter().GetResult();
				RegressionAssert.Equal("keep", File.ReadAllText(Path.Combine(mods, "Example.pak")));
				RegressionAssert.False(File.Exists(Path.Combine(held, "Example.pak")));
			}
			finally { if (Directory.Exists(link)) Directory.Delete(link); }
		});
	}

	private static IReadOnlyDictionary<string, OverrideDuplicateChoice> Choices(OverrideDuplicateChoice choice) =>
		new Dictionary<string, OverrideDuplicateChoice>(StringComparer.OrdinalIgnoreCase) { ["Example.pak"] = choice };
	private static void WriteCopies(string mods, string held, string modsText, string heldText)
	{
		File.WriteAllText(Path.Combine(mods, "Example.pak"), modsText);
		File.WriteAllText(Path.Combine(held, "Example.pak"), heldText);
	}
	private static void WithFolders(Action<string, string, string> action)
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxDuplicateRecoveryTests", Guid.NewGuid().ToString("N"));
		var mods = Path.Combine(root, "Mods");
		var held = Path.Combine(root, "Holding");
		var backups = Path.Combine(root, "OverrideDuplicateBackups");
		Directory.CreateDirectory(mods);
		Directory.CreateDirectory(held);
		try { action(mods, held, backups); }
		finally { Directory.Delete(root, true); }
	}
	private static void CreateJunction(string link, string target)
	{
		const string script = "$ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path $env:REDUX_TEST_LINK -Target $env:REDUX_TEST_TARGET | Out-Null";
		var start = new System.Diagnostics.ProcessStartInfo("powershell.exe")
		{ UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
		start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-EncodedCommand");
		start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
		start.Environment["REDUX_TEST_LINK"] = link; start.Environment["REDUX_TEST_TARGET"] = target;
		using var process = System.Diagnostics.Process.Start(start)!;
		var error = process.StandardError.ReadToEnd(); process.WaitForExit();
		if (process.ExitCode != 0) throw new IOException(error);
	}
	private static void WriteMultipart(string primary, string part)
	{
		var build = new PackageBuildData();
		build.Files.Add(new PackageBuildInputFile { Path = "Public/Example/test.txt", Body = Encoding.UTF8.GetBytes("content") });
		using var writer = PackageWriterFactory.Create(build, primary);
		var streams = (List<Stream>)typeof(PackageWriter).GetField("Streams", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(writer)!;
		streams.Add(File.Open(part, FileMode.Create, FileAccess.ReadWrite));
		writer.Write();
	}
}
