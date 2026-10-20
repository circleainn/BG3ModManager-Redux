using DivinityModManager.AppServices;
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

	public void LooseTextureArchiveExplainsTheUnsupportedInstallWithoutChangingTheArchive()
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxArchivePreflightTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		var archivePath = Path.Combine(directory, "LooseTextures.zip");
		try
		{
			CreateArchive(archivePath, ("Generated/Public/Shared/Assets/ground.DDS", new byte[] { 1, 2, 3, 4 }));
			var before = File.ReadAllBytes(archivePath);
			var report = ArchivePackagePreflightService.AnalyzeAsync(archivePath, []).GetAwaiter().GetResult();
			RegressionAssert.Equal(ArchivePackagePreflightKind.PakArchive, report.Kind);
			RegressionAssert.Equal(1, report.EntryCount);
			RegressionAssert.Equal(0, report.Packages.Count);
			var finding = report.Findings.Single(item => item.Title == ArchivePakImport.NoPakFilesTitle);
			RegressionAssert.Equal(ModHealthSeverity.Error, finding.Severity);
			RegressionAssert.True(finding.Message.Contains("Generated/Public", StringComparison.Ordinal));
			RegressionAssert.True(finding.Message.Contains("Data folder", StringComparison.Ordinal));
			RegressionAssert.SequenceEqual(before, File.ReadAllBytes(archivePath));
			RegressionAssert.SequenceEqual(new[] { archivePath }, Directory.GetFiles(directory));
			RegressionAssert.Equal(0, Directory.GetDirectories(directory).Length);
		}
		finally { Directory.Delete(directory, true); }
	}

	public void MissingPakTextureHintRequiresAGameAssetPath()
	{
		foreach (var entry in new[] { "Preview.dds", "Generated/Other/ground.dds", "Generated/Public/readme.txt" })
		{
			var finding = ArchivePackagePreflightService.AnalyzeEntryNames([entry])
				.Single(item => item.Title == ArchivePakImport.NoPakFilesTitle);
			RegressionAssert.False(finding.Message.Contains("loose texture", StringComparison.Ordinal));
		}
		var wrapped = ArchivePackagePreflightService.AnalyzeEntryNames([@"Release\Data\GENERATED\PUBLIC\Shared\ground.DDS"])
			.Single(item => item.Title == ArchivePakImport.NoPakFilesTitle);
		RegressionAssert.True(wrapped.Message.Contains("loose texture", StringComparison.Ordinal));
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

	public void ReviewedNativeArchiveUsesTheGuardedInstallerLayout()
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxArchivePreflightTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		var archivePath = Path.Combine(directory, "Achievement Enabler-668-1-0.zip");
		try
		{
			CreateArchive(archivePath, ("bin/NativeMods/BG3AchievementEnabler.dll", new byte[] { 1, 2, 3, 4 }));
			var report = ArchivePackagePreflightService.AnalyzeAsync(
				archivePath, Array.Empty<DivinityModManager.Models.DivinityModData>()).GetAwaiter().GetResult();

			RegressionAssert.Equal(ArchivePackagePreflightKind.ReviewedGameDirectory, report.Kind);
			RegressionAssert.Equal(668L, report.GameDirectoryInspection.Definition.NexusModId);
			RegressionAssert.False(report.Findings.Any(finding => finding.Title == "No PAK files found"));
			RegressionAssert.True(report.Findings.Any(finding => finding.Title == "Expected game-directory destinations"));
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, true);
		}
	}

	public void UnreviewedDllArchiveIsReportedWithoutGuessingADestination()
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxArchivePreflightTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		var archivePath = Path.Combine(directory, "UnknownNative.zip");
		try
		{
			CreateArchive(archivePath,
				("bin/NativeMods/UnknownPlugin.dll", new byte[] { 1, 2, 3, 4 }),
				("bin/NativeMods/UnknownPlugin.toml", new byte[] { 5, 6 }));
			var report = ArchivePackagePreflightService.AnalyzeAsync(
				archivePath, Array.Empty<DivinityModManager.Models.DivinityModData>()).GetAwaiter().GetResult();

			RegressionAssert.Equal(ArchivePackagePreflightKind.UnreviewedNative, report.Kind);
			RegressionAssert.True(report.Findings.Any(finding => finding.Title == "Unreviewed native layout"));
			RegressionAssert.True(report.Findings.Any(finding => finding.Title == "Configuration and support files"));
			RegressionAssert.False(report.Findings.Any(finding => finding.Title == "No PAK files found"));
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, true);
		}
	}

	public void SaveArchiveUsesSaveManagerValidationAndMetadata()
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxArchivePreflightTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		var archivePath = Path.Combine(directory, "QuickSave.zip");
		try
		{
			CreateArchive(archivePath,
				("Shadowheart-123__QuickSave_0/QuickSave_0.lsv", new byte[] { 7, 7, 7, 7 }),
				("Shadowheart-123__QuickSave_0/QuickSave_0.webp", new byte[] { 1, 2, 3 }));
			var report = ArchivePackagePreflightService.AnalyzeAsync(
				archivePath, Array.Empty<DivinityModManager.Models.DivinityModData>()).GetAwaiter().GetResult();

			RegressionAssert.Equal(ArchivePackagePreflightKind.SaveGame, report.Kind);
			RegressionAssert.Equal(1, report.SaveGames.Count);
			RegressionAssert.Equal("QuickSave 0", report.SaveGames[0].DisplayName);
			RegressionAssert.True(report.Findings.Any(finding => finding.Title == "BG3 save data recognized"));
			RegressionAssert.False(report.Findings.Any(finding => finding.Title == "No PAK files found"));
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, true);
		}
	}

	public void LooseSaveIsInspectedWithoutUsingAnArchiveReader()
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxArchivePreflightTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		var savePath = Path.Combine(directory, "Manual_Save.lsv");
		try
		{
			File.WriteAllBytes(savePath, new byte[] { 1, 2, 3, 4 });
			var report = ArchivePackagePreflightService.AnalyzeAsync(
				savePath, Array.Empty<DivinityModManager.Models.DivinityModData>()).GetAwaiter().GetResult();

			RegressionAssert.Equal(ArchivePackagePreflightKind.SaveGame, report.Kind);
			RegressionAssert.Equal(1, report.SaveGames.Count);
			RegressionAssert.Equal("Manual Save", report.SaveGames[0].DisplayName);
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, true);
		}
	}

	private static void CreateArchive(string archivePath, params (string Name, byte[] Contents)[] entries)
	{
		using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
		foreach (var item in entries)
		{
			var entry = archive.CreateEntry(item.Name);
			using var stream = entry.Open();
			stream.Write(item.Contents);
		}
	}
}
