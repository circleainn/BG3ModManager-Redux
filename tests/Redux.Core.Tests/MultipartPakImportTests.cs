using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using LSLib.LS;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class MultipartPakImportTests
{
	public void CompleteMultipartPackagePassesPreflight()
	{
		WithFixture((root, primary, part) =>
		{
			using (var package = new PackageReader().Read(primary))
			{
				RegressionAssert.Equal(2u, package.Metadata.NumParts);
				RegressionAssert.Equal(1, package.Files.Count);
				using var contents = package.Files.Single().CreateContentReader();
				using var reader = new StreamReader(contents);
				RegressionAssert.True(reader.ReadToEnd().Contains("Multipart Fixture", StringComparison.Ordinal));
			}
			var original = File.ReadAllBytes(primary);
			var report = PackagePreflightService.AnalyzeAsync(primary, []).GetAwaiter().GetResult();
			RegressionAssert.True(report.IsReadable);
			RegressionAssert.False(report.Findings.Any(finding => finding.Title == PakImportCompatibility.MultipartFindingTitle));
			RegressionAssert.SequenceEqual(original, File.ReadAllBytes(primary));
			RegressionAssert.True(File.Exists(part));
		});
	}

	public void ArchiveWithAllMultipartSiblingsIsOneReadablePackage()
	{
		WithFixture((root, primary, part) =>
		{
			var archivePath = Path.Combine(root, "Multipart.zip");
			using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
			{
				archive.CreateEntryFromFile(primary, Path.GetFileName(primary));
				archive.CreateEntryFromFile(part, Path.GetFileName(part));
			}
			var original = File.ReadAllBytes(archivePath);
			var report = ArchivePackagePreflightService.AnalyzeAsync(archivePath, []).GetAwaiter().GetResult();
			RegressionAssert.Equal(1, report.Packages.Count);
			RegressionAssert.True(report.Packages.Single().IsReadable);
			RegressionAssert.False(report.Findings.Any(finding => finding.Title == PakImportCompatibility.MultipartFindingTitle));
			RegressionAssert.SequenceEqual(original, File.ReadAllBytes(archivePath));
		});
	}

	public void MissingMultipartSiblingCannotReplaceInstalledPackage()
	{
		WithFixture((root, primary, part) =>
		{
			var staged = Path.Combine(root, ".Multipart.redux-import-fixture.pak.tmp");
			File.Copy(primary, staged);
			var installed = Path.Combine(root, "Installed.pak");
			var installedBytes = Encoding.UTF8.GetBytes("existing package remains unchanged");
			File.WriteAllBytes(installed, installedBytes);
			var viewModel = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
			var validate = typeof(MainWindowViewModel).GetMethod("ValidateAndCommitImportedPakAsync",
				BindingFlags.Instance | BindingFlags.NonPublic)!;
			var task = (Task<DivinityModData>)validate.Invoke(viewModel,
				[staged, installed, new Dictionary<string, DivinityModData>(), CancellationToken.None, null])!;
			var error = RegressionAssert.Throws<InvalidDataException>(() => task.GetAwaiter().GetResult());
			RegressionAssert.True(error.Message.Contains("Missing PAK parts", StringComparison.Ordinal));
			RegressionAssert.SequenceEqual(installedBytes, File.ReadAllBytes(installed));
			RegressionAssert.SequenceEqual(File.ReadAllBytes(primary), File.ReadAllBytes(staged));
		});
	}

	public void NumberedSingleFilePackageRemainsSupported()
	{
		WithFixture((root, primary, part) =>
		{
			var numbered = Path.Combine(root, "Standalone_1.pak");
			WritePackage(numbered, null);
			PakImportCompatibility.RequireSupportedLayout(numbered);
			RegressionAssert.True(PakImportCompatibility.GetUnsupportedLayoutFinding(numbered) == null);
			var report = PackagePreflightService.AnalyzeAsync(numbered, []).GetAwaiter().GetResult();
			RegressionAssert.True(report.IsReadable);
			RegressionAssert.False(report.Findings.Any(finding => finding.Title == PakImportCompatibility.MultipartFindingTitle));
		});
	}

	public void InstallRenamesEveryPartAndBacksUpReadableOldSet()
	{
		WithFixture((root, primary, part) =>
		{
			var mods = Path.Combine(root, "Mods");
			var backups = Path.Combine(root, "Backups");
			Directory.CreateDirectory(mods);
			var installed = Path.Combine(mods, "Renamed.pak");
			var installedPart = PakFileSet.PartPath(installed, 1);
			WritePackage(installed, installedPart);
			var oldBytes = File.ReadAllBytes(installedPart);
			RegressionAssert.True(PakFileSet.InstallAsync(primary, installed, backups, CancellationToken.None).GetAwaiter().GetResult());
			AssertReadable(installed);
			var backupPrimary = PakFileSet.GetPrimaries(Directory.GetFiles(backups, "*.pak")).Single();
			AssertReadable(backupPrimary);
			RegressionAssert.SequenceEqual(oldBytes, File.ReadAllBytes(PakFileSet.PartPath(backupPrimary, 1)));
			RegressionAssert.False(File.Exists(primary));
			RegressionAssert.False(File.Exists(part));
			RegressionAssert.Equal(0, Directory.GetDirectories(mods).Length);
		});
	}

	public void LockedOldPartRollsBackEntireInstall()
	{
		WithFixture((root, primary, part) =>
		{
			var mods = Path.Combine(root, "Mods");
			Directory.CreateDirectory(mods);
			var installed = Path.Combine(mods, "Installed.pak");
			var installedPart = PakFileSet.PartPath(installed, 1);
			WritePackage(installed, installedPart);
			var oldPrimary = File.ReadAllBytes(installed);
			var oldPart = File.ReadAllBytes(installedPart);
			using (var locked = File.Open(installedPart, FileMode.Open, FileAccess.Read, FileShare.Read))
				RegressionAssert.Throws<IOException>(() => PakFileSet.InstallAsync(primary, installed,
					Path.Combine(root, "Backups"), CancellationToken.None).GetAwaiter().GetResult());
			RegressionAssert.SequenceEqual(oldPrimary, File.ReadAllBytes(installed));
			RegressionAssert.SequenceEqual(oldPart, File.ReadAllBytes(installedPart));
			AssertReadable(installed);
			RegressionAssert.Equal(0, Directory.GetDirectories(mods).Length);
		});
	}

	public void ReplacingMultipartWithSingleRemovesOnlyOwnedParts()
	{
		WithFixture((root, primary, part) =>
		{
			var single = Path.Combine(root, "Single.pak");
			WritePackage(single, null);
			var unrelated = Path.Combine(root, "Multipart_2.pak");
			WritePackage(unrelated, null);
			PakFileSet.InstallAsync(single, primary, Path.Combine(root, "Backups"), CancellationToken.None).GetAwaiter().GetResult();
			RegressionAssert.False(File.Exists(part));
			RegressionAssert.True(File.Exists(unrelated));
			AssertReadable(primary);
		});
	}

	public void PartCollisionNeverOverwritesStandaloneNumberedPak()
	{
		WithFixture((root, primary, part) =>
		{
			var mods = Path.Combine(root, "Mods");
			Directory.CreateDirectory(mods);
			var destination = Path.Combine(mods, "Example.pak");
			var collision = PakFileSet.PartPath(destination, 1);
			WritePackage(collision, null);
			var original = File.ReadAllBytes(collision);
			RegressionAssert.Throws<IOException>(() => PakFileSet.InstallAsync(primary, destination,
				Path.Combine(root, "Backups"), CancellationToken.None).GetAwaiter().GetResult());
			RegressionAssert.SequenceEqual(original, File.ReadAllBytes(collision));
			RegressionAssert.False(File.Exists(destination));
		});
	}

	public void OverrideHoldingMovesWholePakSet()
	{
		WithFixture((root, primary, part) =>
		{
			var mods = Path.Combine(root, "Mods");
			var holding = Path.Combine(root, "Held");
			Directory.CreateDirectory(mods);
			var installed = Path.Combine(mods, Path.GetFileName(primary));
			File.Move(primary, installed);
			File.Move(part, Path.Combine(mods, Path.GetFileName(part)));
			var service = new OverrideOrderFileService(mods, holding);
			var plan = service.Review([installed], []);
			RegressionAssert.Equal(2, plan.ToHold.Count);
			service.Apply(plan);
			AssertReadable(Path.Combine(holding, Path.GetFileName(primary)));
			plan = service.Review([], [Path.GetFileName(primary)]);
			RegressionAssert.Equal(2, plan.ToActivate.Count);
			service.Apply(plan);
			AssertReadable(installed);
		});
	}

	public void BackupRetentionKeepsAndDeletesWholeSets()
	{
		WithFixture((root, primary, part) =>
		{
			var reviewed = ModBackupRetention.Snapshot(root);
			RegressionAssert.Equal(0, ModBackupRetention.DeleteReviewed(root, [reviewed.First(file => file.Path == part)]).Deleted);
			RegressionAssert.Equal(2, ModBackupRetention.Prune(root, 1).Deleted);
			RegressionAssert.False(File.Exists(primary));
			RegressionAssert.False(File.Exists(part));
		});
	}

	private static void AssertReadable(string primary)
	{
		using var package = new PackageReader().Read(primary);
		using var reader = new StreamReader(package.Files.Single().CreateContentReader());
		RegressionAssert.Contains(reader.ReadToEnd(), "Multipart Fixture");
	}

	public void LocalIntakePreservesAllPartsAsOneStableArchive()
	{
		WithFixture((root, primary, part) =>
		{
			string temporary;
			using (var prepared = PreparedPakInput.OpenAsync(primary, CancellationToken.None).GetAwaiter().GetResult())
			using (var again = PreparedPakInput.OpenAsync(primary, CancellationToken.None).GetAwaiter().GetResult())
			{
				temporary = prepared.Path;
				RegressionAssert.SequenceEqual(File.ReadAllBytes(prepared.Path), File.ReadAllBytes(again.Path));
				using var archive = ZipFile.OpenRead(prepared.Path);
				RegressionAssert.Equal(2, archive.Entries.Count);
				var report = ArchivePackagePreflightService.AnalyzeAsync(prepared.Path, []).GetAwaiter().GetResult();
				RegressionAssert.True(report.Packages.Single().IsReadable);
			}
			RegressionAssert.False(File.Exists(temporary));
			RegressionAssert.True(File.Exists(primary) && File.Exists(part));
		});
	}

	public void StandaloneImportCannotReplaceAnotherPackagesSibling()
	{
		WithFixture((root, primary, part) =>
		{
			var incoming = Path.Combine(root, "Standalone.pak");
			WritePackage(incoming, null);
			var original = File.ReadAllBytes(part);
			RegressionAssert.Throws<IOException>(() => PakFileSet.InstallAsync(incoming, part,
				Path.Combine(root, "Backups"), CancellationToken.None).GetAwaiter().GetResult());
			RegressionAssert.SequenceEqual(original, File.ReadAllBytes(part));
			AssertReadable(primary);
		});
	}

	public void InvalidPartCountCannotHideOtherLibraryPackages()
	{
		WithFixture((root, primary, part) =>
		{
			var invalid = Path.Combine(root, "Invalid.pak");
			WritePackage(invalid, null);
			var bytes = File.ReadAllBytes(invalid);
			// The v18 header stores its part count in the final two header bytes.
			bytes[38] = bytes[39] = 0;
			File.WriteAllBytes(invalid, bytes);
			RegressionAssert.SequenceEqual(new[] { primary, invalid }, PakFileSet.GetPrimaries([primary, part, invalid]));
			RegressionAssert.False(PackagePreflightService.AnalyzeAsync(invalid, []).GetAwaiter().GetResult().IsReadable);
			AssertReadable(primary);
		});
	}

	public void IncompleteArchiveReportsTheMissingSibling()
	{
		WithFixture((root, primary, part) =>
		{
			var zip = Path.Combine(root, "incomplete.zip");
			using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
				archive.CreateEntryFromFile(primary, Path.GetFileName(primary));
			var report = ArchivePackagePreflightService.AnalyzeAsync(zip, []).GetAwaiter().GetResult();
			RegressionAssert.False(report.Packages.Single().IsReadable);
			RegressionAssert.True(report.Findings.Any(finding => finding.Message.Contains("Multipart_1.pak", StringComparison.Ordinal)));
		});
	}

	public void InterruptedInstallRecoversOriginalSetBeforeScanning()
	{
		WithFixture((root, primary, part) =>
		{
			var originalPrimary = File.ReadAllBytes(primary);
			var originalPart = File.ReadAllBytes(part);
			var transaction = Path.Combine(root, ".redux-pak-transaction-fixture");
			var oldFolder = Path.Combine(transaction, "old");
			Directory.CreateDirectory(oldFolder);
			var old = new[] { primary, part }.Select(path => new { Name = Path.GetFileName(path), Length = new FileInfo(path).Length, ModifiedUtc = File.GetLastWriteTimeUtc(path) }).ToArray();
			File.Move(primary, Path.Combine(oldFolder, Path.GetFileName(primary)));
			File.Move(part, Path.Combine(oldFolder, Path.GetFileName(part)));
			// Simulate a crash after a new sibling was published, before its primary.
			File.WriteAllText(part, "new incomplete installation");
			var incoming = new[] { new { Name = Path.GetFileName(primary), Length = 111L, ModifiedUtc = DateTime.UtcNow },
				new { Name = Path.GetFileName(part), Length = new FileInfo(part).Length, ModifiedUtc = File.GetLastWriteTimeUtc(part) } };
			File.WriteAllText(Path.Combine(transaction, "journal.json"), System.Text.Json.JsonSerializer.Serialize(new { Old = old, New = incoming }));
			PakFileSet.Recover(root);
			RegressionAssert.SequenceEqual(originalPrimary, File.ReadAllBytes(primary));
			RegressionAssert.SequenceEqual(originalPart, File.ReadAllBytes(part));
			RegressionAssert.False(Directory.Exists(transaction));
			AssertReadable(primary);
		});
	}

	public void ArchiveSiblingOrderDoesNotAffectSetValidation()
	{
		WithFixture((root, primary, part) =>
		{
			var zip = Path.Combine(root, "reverse.zip");
			using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
			{
				archive.CreateEntryFromFile(part, "nested/" + Path.GetFileName(part));
				archive.CreateEntryFromFile(primary, "nested/" + Path.GetFileName(primary));
			}
			var report = ArchivePackagePreflightService.AnalyzeAsync(zip, []).GetAwaiter().GetResult();
			RegressionAssert.Equal(1, report.Packages.Count);
			RegressionAssert.True(report.Packages[0].IsReadable);
		});
	}

	private static void WithFixture(Action<string, string, string> action)
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxMultipartPakTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var primary = Path.Combine(root, "Multipart.pak");
			var part = Path.Combine(root, "Multipart_1.pak");
			WritePackage(primary, part);
			action(root, primary, part);
		}
		finally { Directory.Delete(root, true); }
	}

	private static void WritePackage(string primary, string? part)
	{
		const string metadata = """
			<save><version major="4" minor="8" revision="0" build="0"/><region id="Config"><node id="root"><children><node id="ModuleInfo"><attribute id="UUID" type="FixedString" value="d18b1759-04a2-47f1-9288-0c7f63eec093"/><attribute id="Name" type="LSString" value="Multipart Fixture"/><attribute id="Author" type="LSString" value="Audit"/><attribute id="Folder" type="LSString" value="Example"/><attribute id="Type" type="FixedString" value="Add-on"/><attribute id="Version64" type="int64" value="36028797018963968"/></node></children></node></region></save>
			""";
		var build = new PackageBuildData();
		build.Files.Add(new PackageBuildInputFile { Path = "Mods/Example/meta.lsx", Body = Encoding.UTF8.GetBytes(metadata) });
		using var writer = PackageWriterFactory.Create(build, primary);
		if (part != null)
		{
			// Force the writer's normal multipart layout without a multi-gigabyte first part.
			var streams = (List<Stream>)typeof(PackageWriter).GetField("Streams", BindingFlags.Instance | BindingFlags.NonPublic)!
				.GetValue(writer)!;
			streams.Add(File.Open(part, FileMode.Create, FileAccess.ReadWrite));
		}
		writer.Write();
	}
}
