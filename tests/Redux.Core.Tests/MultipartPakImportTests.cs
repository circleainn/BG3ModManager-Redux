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
	public void ValidMultipartPackageGetsActionablePreflightFinding()
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
			RegressionAssert.False(report.IsReadable);
			RegressionAssert.Equal(PakImportCompatibility.MultipartFindingTitle, report.Findings.Single().Title);
			RegressionAssert.True(report.Findings.Single().Message.Contains("requires 2 files", StringComparison.Ordinal));
			RegressionAssert.True(report.Findings.Single().Message.Contains("manual installation", StringComparison.Ordinal));
			RegressionAssert.SequenceEqual(original, File.ReadAllBytes(primary));
			RegressionAssert.True(File.Exists(part));
		});
	}

	public void ArchiveWithAllMultipartSiblingsReportsUnsupportedLayout()
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
			RegressionAssert.True(report.Findings.Any(finding =>
				finding.Title == PakImportCompatibility.MultipartFindingTitle));
			RegressionAssert.True(report.Packages.Any(package => package.Findings.Any(finding =>
				finding.Title == PakImportCompatibility.MultipartFindingTitle)));
			RegressionAssert.SequenceEqual(original, File.ReadAllBytes(archivePath));
		});
	}

	public void StagedMultipartImportCannotReplaceInstalledPackage()
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
			RegressionAssert.True(error.Message.Contains("Installed.pak", StringComparison.Ordinal));
			RegressionAssert.True(error.Message.Contains("multipart", StringComparison.Ordinal));
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
