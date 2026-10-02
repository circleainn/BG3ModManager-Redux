using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using LSLib.LS;
using SharpCompress.Common;
using SharpCompress.Writers;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class EditorModBackupTests
{
	public void UnsafeEditorMetadataCannotReplaceFilesOrExistingBackup()
	{
		WithDirectory(root =>
		{
			foreach (var (folder, uuid, victimName) in new[]
			{
				("../victim_fixture", "fixture", "victim_fixture.pak"),
				("Example", "../../../victim", "victim.pak")
			})
			{
				var victim = Path.Combine(root, victimName);
				File.WriteAllText(victim, "unrelated file remains unchanged");
				var backup = Path.Combine(root, "backup.zip");
				File.WriteAllText(backup, "previous backup");
				var staging = Path.Combine(root, "staging");
				var mod = new DivinityModData { Folder = folder, UUID = uuid, Name = "Unsafe fixture", IsEditorMod = true };
				RegressionAssert.Throws<InvalidDataException>(() => BackupAsync(backup, mod,
					Path.Combine(root, "Data"), staging).GetAwaiter().GetResult());
				RegressionAssert.Equal("unrelated file remains unchanged", File.ReadAllText(victim));
				RegressionAssert.Equal("previous backup", File.ReadAllText(backup));
				RegressionAssert.False(Directory.Exists(staging));
				RegressionAssert.Equal(0, Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Length);
			}
		});
	}

	public void ValidEditorBackupKeepsExpectedNamesAndActualPackageContents()
	{
		WithDirectory(root =>
		{
			foreach (var folder in new[] { "Example", "Example_fixture" })
			{
				var gameData = Path.Combine(root, "Data");
				var metadataDirectory = Path.Combine(gameData, "Mods", folder);
				var publicDirectory = Path.Combine(gameData, "Public", folder, "Nested");
				Directory.CreateDirectory(metadataDirectory);
				Directory.CreateDirectory(publicDirectory);
				File.WriteAllText(Path.Combine(metadataDirectory, "meta.lsx"), "fixture metadata");
				File.WriteAllText(Path.Combine(publicDirectory, "contents.txt"), "fixture public content");
				var staging = Path.Combine(root, "staging");
				var backup = Path.Combine(root, folder + ".zip");
				var mod = new DivinityModData { Folder = folder, UUID = "fixture", Name = "Valid fixture", IsEditorMod = true };
				BackupAsync(backup, mod, gameData, staging).GetAwaiter().GetResult();
				RegressionAssert.Equal(0, Directory.GetFiles(staging).Length);
				using var zip = ZipFile.OpenRead(backup);
				var entry = zip.Entries.Single();
				RegressionAssert.Equal("Example_fixture.pak", entry.FullName);
				var extracted = Path.Combine(root, folder + ".pak");
				entry.ExtractToFile(extracted);
				using var package = new PackageReader().Read(extracted);
				RegressionAssert.Equal(2, package.Files.Count);
				using var metadata = new StreamReader(package.Files.Single(file => file.Name == $"Mods/{folder}/meta.lsx").CreateContentReader());
				RegressionAssert.Equal("fixture metadata", metadata.ReadToEnd());
				using var contents = new StreamReader(package.Files.Single(file => file.Name == $"Public/{folder}/Nested/contents.txt").CreateContentReader());
				RegressionAssert.Equal("fixture public content", contents.ReadToEnd());
			}
		});
	}

	public void MissingEditorSourcesCannotReplaceExistingBackupWithEmptyPackage()
	{
		WithDirectory(root =>
		{
			var backup = Path.Combine(root, "backup.zip");
			File.WriteAllText(backup, "previous backup");
			var staging = Path.Combine(root, "staging");
			var mod = new DivinityModData { Folder = "Missing", UUID = "fixture", Name = "Missing fixture", IsEditorMod = true };
			RegressionAssert.Throws<DirectoryNotFoundException>(() => BackupAsync(backup, mod,
				Path.Combine(root, "Data"), staging).GetAwaiter().GetResult());
			RegressionAssert.Equal("previous backup", File.ReadAllText(backup));
			RegressionAssert.False(Directory.Exists(staging));
			RegressionAssert.Equal(0, Directory.GetFiles(root, "*.tmp").Length);
		});
	}

	private static Task BackupAsync(string destination, DivinityModData mod, string gameData, string staging) =>
		AtomicFileWriter.WriteFileAsync(destination, async (temporaryPath, token) =>
		{
			await using var writer = await WriterFactory.OpenAsyncWriter(temporaryPath, ArchiveType.Zip,
				new WriterOptions(CompressionType.Deflate), token);
			await EditorModBackupService.WriteToZipAsync(writer, mod, gameData, staging, token);
		}, cancellationToken: CancellationToken.None);

	private static void WithDirectory(Action<string> action)
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxEditorModBackupTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try { action(root); }
		finally { Directory.Delete(root, true); }
	}
}
