using DivinityModManager.Models.Health;
using DivinityModManager.Util;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Redux.Core.Tests;

public sealed class ArchivePackagePreflightTests
{
	public void OrdinaryArchiveLayoutHasNoContainerFindings()
	{
		var findings = ArchivePackagePreflightService.AnalyzeEntryNames(new[]
		{
			"Mods/ExampleMod.pak",
			"README.md",
			"Images/preview.png"
		});

		RegressionAssert.Equal(0, findings.Count);
	}

	public void UnsafePathsDuplicatesAndDevelopmentDebrisAreReported()
	{
		var findings = ArchivePackagePreflightService.AnalyzeEntryNames(new[]
		{
			"Packages/Example.pak",
			"Optional/Example.pak",
			"../debug.pdb",
			"PlayerProfiles/Public/modsettings.lsx"
		});

		RegressionAssert.True(findings.Any(finding =>
			finding.Severity == ModHealthSeverity.Error
			&& finding.Title == "Duplicate PAK filenames"));
		RegressionAssert.True(findings.Any(finding => finding.Title == "Unsafe archive paths"));
		RegressionAssert.True(findings.Any(finding => finding.Title == "Development files are included in the archive"));
		RegressionAssert.True(findings.Any(finding => finding.Title == "Load-order settings are included"));
	}

	public void ArchiveWithoutPakIsReported()
	{
		var findings = ArchivePackagePreflightService.AnalyzeEntryNames(new[] { "README.md" });

		RegressionAssert.True(findings.Any(finding =>
			finding.Severity == ModHealthSeverity.Error
			&& finding.Title == "No PAK files found"));
	}

	public void ZipPakIsStagedForInspectionWithoutChangingTheArchive()
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxArchivePreflightTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		var archivePath = Path.Combine(directory, "Release.zip");
		try
		{
			using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
			{
				var entry = archive.CreateEntry("Packages/Unreadable.pak");
				using var stream = entry.Open();
				stream.Write(new byte[] { 1, 2, 3, 4 });
			}
			var originalSize = new FileInfo(archivePath).Length;

			var report = ArchivePackagePreflightService.AnalyzeAsync(
				archivePath,
				Array.Empty<DivinityModManager.Models.DivinityModData>())
				.GetAwaiter()
				.GetResult();

			RegressionAssert.Equal(1, report.Packages.Count);
			RegressionAssert.True(report.Packages[0].HasErrors);
			RegressionAssert.True(report.Packages[0].PackagePath.EndsWith(
				"Release.zip::Packages/Unreadable.pak",
				StringComparison.OrdinalIgnoreCase));
			RegressionAssert.Equal(originalSize, new FileInfo(archivePath).Length);
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, true);
		}
	}
}
