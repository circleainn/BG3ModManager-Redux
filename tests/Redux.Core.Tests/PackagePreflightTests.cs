using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;

using DynamicData;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Redux.Core.Tests;

public sealed class PackagePreflightTests
{
	public void ValidPackageHasNoBlockingFindings()
	{
		var report = PackagePreflightService.AnalyzeLoadedPackage(
			"Example.pak",
			CreateValidMod(),
			Array.Empty<DivinityModData>());

		RegressionAssert.False(report.HasErrors);
		RegressionAssert.False(report.HasWarnings);
		RegressionAssert.Equal(2, report.InternalFileCount);
	}

	public void MissingDependencyAndDevelopmentDebrisAreReported()
	{
		var mod = CreateValidMod();
		mod.Dependencies.AddOrUpdate(new ModuleShortDesc
		{
			UUID = "710f439a-fbb3-4c08-a124-571905f0d60f",
			Name = "Required Library",
			Version = DivinityModVersion2.FromInt(1)
		});
		mod.Files.Add("Tools/release-notes.pdb");

		var report = PackagePreflightService.AnalyzeLoadedPackage(
			"Example.pak",
			mod,
			Array.Empty<DivinityModData>());

		RegressionAssert.True(report.Findings.Any(finding =>
			finding.Severity == ModHealthSeverity.Error
			&& finding.Title == "Missing dependency"));
		RegressionAssert.True(report.Findings.Any(finding =>
			finding.Severity == ModHealthSeverity.Warning
			&& finding.Title == "Development files are included"));
	}

	public void InstalledUpdateUuidIsAReviewWarningInsteadOfADuplicateError()
	{
		var mod = CreateValidMod();
		var installed = CreateValidMod();
		installed.FilePath = "Installed.pak";

		var report = PackagePreflightService.AnalyzeLoadedPackage(
			"Candidate.pak",
			mod,
			new[] { installed });

		RegressionAssert.True(report.Findings.Any(finding =>
			finding.Severity == ModHealthSeverity.Warning
			&& finding.Title == "UUID already installed"));
		RegressionAssert.False(report.Findings.Any(finding =>
			finding.Severity == ModHealthSeverity.Error
			&& finding.Title == "Duplicate mod UUID"));
	}

	public void MissingReleaseIdentityIsReportedConservatively()
	{
		var mod = CreateValidMod();
		mod.Author = String.Empty;
		mod.Version = DivinityModVersion2.Empty;

		var report = PackagePreflightService.AnalyzeLoadedPackage(
			"Example.pak",
			mod,
			Array.Empty<DivinityModData>());

		RegressionAssert.True(report.Findings.Any(finding => finding.Title == "Author is not provided"));
		RegressionAssert.True(report.Findings.Any(finding => finding.Title == "Version is still 0.0.0.0"));
	}

	private static DivinityModData CreateValidMod() => new()
	{
		FilePath = "Example.pak",
		HasMetadata = true,
		Name = "Example Mod",
		Folder = "ExampleMod",
		UUID = "1f06c8c3-f582-4d29-8f91-638d3d3c9e22",
		Author = "Example Author",
		Description = "Example description",
		Version = DivinityModVersion2.FromInt(1),
		Files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Mods/ExampleMod/meta.lsx",
			"Public/ExampleMod/Stats/Generated/Data/example.txt"
		}
	};
}
