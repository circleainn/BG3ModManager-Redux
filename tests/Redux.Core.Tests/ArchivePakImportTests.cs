using DivinityModManager.AppServices;
using DivinityModManager.Util;
using SharpCompress.Archives;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class ArchivePakImportTests
{
	public void DuplicateDestinationsAbortBeforeAnyArchiveEntryIsImported()
	{
		WithArchive(["First.pak", "variant-a/Same.pak", "variant-b/same.PAK"], (archivePath, directory) =>
		{
			var installed = Path.Combine(directory, "First.pak");
			File.WriteAllText(installed, "previous installation");
			using var archive = ArchiveFactory.OpenArchive(archivePath);
			var imported = 0;
			var error = RegressionAssert.Throws<InvalidDataException>(() => ArchivePakImport.ReadEntriesAsync(
				archive, false, async (entry, stream) =>
				{
					imported++;
					await using var destination = File.Create(Path.Combine(directory, Path.GetFileName(entry.Key)));
					await stream.CopyToAsync(destination);
				}).GetAwaiter().GetResult());
			RegressionAssert.Equal(0, imported);
			RegressionAssert.Equal("previous installation", File.ReadAllText(installed));
			RegressionAssert.True(error.Message.Contains("Same.pak", StringComparison.Ordinal));
			RegressionAssert.False(File.Exists(Path.Combine(directory, "same.PAK")));
		});
	}

	public void DuplicateDestinationReviewDoesNotReadOrStagePakContents()
	{
		WithArchive(["first/Same.pak", "second/same.PAK"], (path, _) =>
		{
			var before = File.ReadAllBytes(path);
			var review = ArchivePackagePreflightService.AnalyzeAsync(path, []).GetAwaiter().GetResult();
			RegressionAssert.Equal(0, review.Packages.Count);
			RegressionAssert.True(review.Findings.Any(finding => finding.Title == ArchivePakImport.DuplicateNamesTitle));
			RegressionAssert.SequenceEqual(before, File.ReadAllBytes(path));
		});
		RegressionAssert.True(ArchivePakImport.FindDestinationCollision(["a\\Same.pak", "b/same.pak"]) != null);
	}

	public void UniquePakDestinationsImportNormallyAndOnlyReadOrdersWhenRequested()
	{
		WithArchive(["a/First.pak", "b/Second.pak", "Current.json", "README.md"], (path, _) =>
		{
			foreach (var onlyMods in new[] { true, false })
			{
				using var archive = ArchiveFactory.OpenArchive(path);
				var readNames = new System.Collections.Generic.List<string>();
				ArchivePakImport.ReadEntriesAsync(archive, onlyMods, async (entry, stream) =>
				{
					using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
					RegressionAssert.Equal(entry.Key, await reader.ReadToEndAsync());
					readNames.Add(entry.Key);
				}).GetAwaiter().GetResult();
				RegressionAssert.SequenceEqual(onlyMods ? new[] { "a/First.pak", "b/Second.pak" }
					: new[] { "a/First.pak", "b/Second.pak", "Current.json" }, readNames);
			}
		});
	}

	private static void WithArchive(string[] names, Action<string, string> action)
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxArchiveDestinationTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			var path = Path.Combine(directory, "variants.zip");
			using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
				foreach (var name in names)
				{
					using var writer = new StreamWriter(zip.CreateEntry(name).Open());
					writer.Write(name);
				}
			action(path, directory);
		}
		finally { Directory.Delete(directory, true); }
	}
}
