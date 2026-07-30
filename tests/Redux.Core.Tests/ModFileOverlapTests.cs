using DivinityModManager.AppServices;
using DivinityModManager.Models;

using System;
using System.Linq;

namespace Redux.Core.Tests;

internal sealed class ModFileOverlapTests
{
	public void NormalizesSlashAndCaseDifferences()
	{
		var result = ModFileOverlapService.AnalyzeIndexes(
		[
			Index("a", "Alpha", "Alpha.pak", @"Public\Shared\file.lsx"),
			Index("b", "Beta", "Beta.pak", "/public/shared/FILE.lsx")
		]);

		RegressionAssert.Equal(1, result.OverlapPathCount);
		RegressionAssert.Equal("Public/Shared/file.lsx", result.Entries[0].InternalPath);
		RegressionAssert.Equal(2, result.Entries[0].PackageCount);
	}

	public void DuplicatePathsInsideOnePackageAreNotOverlaps()
	{
		var result = ModFileOverlapService.AnalyzeIndexes(
		[
			Index(
				"a",
				"Alpha",
				"Alpha.pak",
				"Mods/Alpha/meta.lsx",
				@"Mods\Alpha\meta.lsx",
				"/mods/alpha/META.lsx")
		]);

		RegressionAssert.Equal(0, result.OverlapPathCount);
		RegressionAssert.Equal(1, result.UniqueInternalPathCount);
	}

	public void ExcludesUniquePathsAndCountsAffectedPackages()
	{
		var result = ModFileOverlapService.AnalyzeIndexes(
		[
			Index("a", "Alpha", "Alpha.pak", "Shared/A.txt", "Only/Alpha.txt"),
			Index("b", "Beta", "Beta.pak", "Shared/A.txt", "Shared/B.txt"),
			Index("c", "Gamma", "Gamma.pak", "Shared/B.txt", "Only/Gamma.txt")
		]);

		RegressionAssert.Equal(2, result.OverlapPathCount);
		RegressionAssert.Equal(3, result.AffectedPackageCount);
		RegressionAssert.Equal(4, result.UniqueInternalPathCount);
		RegressionAssert.False(result.Entries.Any(entry =>
			entry.InternalPath.StartsWith("Only/", StringComparison.OrdinalIgnoreCase)));
	}

	public void OrdersBroadestOverlapsBeforePathName()
	{
		var result = ModFileOverlapService.AnalyzeIndexes(
		[
			Index("a", "Alpha", "Alpha.pak", "Shared/Two.txt", "Shared/Three.txt"),
			Index("b", "Beta", "Beta.pak", "Shared/Two.txt", "Shared/Three.txt"),
			Index("c", "Gamma", "Gamma.pak", "Shared/Three.txt")
		]);

		RegressionAssert.Equal(2, result.Entries.Count);
		RegressionAssert.Equal("Shared/Three.txt", result.Entries[0].InternalPath);
		RegressionAssert.Equal(3, result.Entries[0].PackageCount);
		RegressionAssert.Equal("Shared/Two.txt", result.Entries[1].InternalPath);
	}

	public void MalformedPackagePathsAreReportedWithoutAbortingTheScan()
	{
		var result = ModFileOverlapService.AnalyzePackages(
		[
			new RegressionModData
			{
				Name = "Malformed",
				FilePath = "bad\0package.pak"
			}
		]);

		RegressionAssert.Equal(1, result.CandidatePackageCount);
		RegressionAssert.Equal(0, result.ScannedPackageCount);
		RegressionAssert.Equal(1, result.Failures.Count);
		RegressionAssert.Equal("Malformed", result.Failures[0].ModName);
	}

	private static ModFilePathIndex Index(
		string uuid,
		string displayName,
		string packageFileName,
		params string[] paths) =>
		new(
			new ModFileOverlapPackageIdentity(uuid, displayName, packageFileName),
			paths);
}
