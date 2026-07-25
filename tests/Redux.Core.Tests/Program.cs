using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

using DivinityModManager.Models;

namespace Redux.Core.Tests;

internal static class Program
{
	private static int Main()
	{
		// Register WPF's pack URI support before exercising GUI-owned, nonvisual
		// services such as the portable Redux bundle reader/writer.
		_ = Application.Current ?? new Application();

		var source = new SourceAssociationTests();
		var manifest = new CreatorManifestValidationTests();
		var health = new ModHealthTests();
		var bundle = new ReduxBundleTests();
		var tests = new (string Name, Action Run)[]
		{
			(nameof(source.ManualNexusAssociationWinsOverCachedModioMetadata), source.ManualNexusAssociationWinsOverCachedModioMetadata),
			(nameof(source.CachedModioMetadataWinsOverAutomaticNexusMetadata), source.CachedModioMetadataWinsOverAutomaticNexusMetadata),
			(nameof(source.LocalOnlyPresentationHidesProvidersWithoutDeletingCachedMetadata), source.LocalOnlyPresentationHidesProvidersWithoutDeletingCachedMetadata),
			(nameof(source.CreatorManifestModioCacheRequiresTheCurrentProjectClaim), source.CreatorManifestModioCacheRequiresTheCurrentProjectClaim),
			(nameof(source.ManualSourceChoicesBlockCreatorManifestModioCache), source.ManualSourceChoicesBlockCreatorManifestModioCache),
			(nameof(source.NativeModioCacheDoesNotDependOnCreatorManifest), source.NativeModioCacheDoesNotDependOnCreatorManifest),
			(nameof(manifest.ValidManifestPreservesCreatorAuthorOrder), manifest.ValidManifestPreservesCreatorAuthorOrder),
			(nameof(manifest.DuplicateAuthorsAreRejected), manifest.DuplicateAuthorsAreRejected),
			(nameof(manifest.MismatchedPakClaimIsRejected), manifest.MismatchedPakClaimIsRejected),
			(nameof(health.MissingAndInactiveDependenciesRemainIndependentOfTheAdvisor), health.MissingAndInactiveDependenciesRemainIndependentOfTheAdvisor),
			(nameof(health.AdvisorFindingsAreAbsentUntilTheAdvisorIsEnabled), health.AdvisorFindingsAreAbsentUntilTheAdvisorIsEnabled),
			(nameof(health.DependencyCyclesAreReportedOnlyByTheOptInAdvisor), health.DependencyCyclesAreReportedOnlyByTheOptInAdvisor),
			(nameof(health.InvalidCreatorManifestIsReportedWithoutApplyingItsClaims), health.InvalidCreatorManifestIsReportedWithoutApplyingItsClaims),
			(nameof(health.DuplicateUuidsAreReportedWithoutRemovingEitherPackage), health.DuplicateUuidsAreReportedWithoutRemovingEitherPackage),
			(nameof(health.ActiveDeclaredConflictsAreReportedConservatively), health.ActiveDeclaredConflictsAreReportedConservatively),
			(nameof(health.OlderInstalledDependencyVersionsAreReportedWithoutUpdatingThem), health.OlderInstalledDependencyVersionsAreReportedWithoutUpdatingThem),
			(nameof(health.ScriptExtenderErrorsAndWarningsRemainDistinct), health.ScriptExtenderErrorsAndWarningsRemainDistinct),
			(nameof(health.ForceLoadedVariantsRemainInformationalAndReadOnly), health.ForceLoadedVariantsRemainInformationalAndReadOnly),
			(nameof(bundle.BundleRoundTripPreservesOrderAndReduxPresentation), bundle.BundleRoundTripPreservesOrderAndReduxPresentation),
			(nameof(bundle.BundleNeverContainsModsettingsLsx), bundle.BundleNeverContainsModsettingsLsx),
			(nameof(bundle.MismatchedOrderAndPresentationAreRejected), bundle.MismatchedOrderAndPresentationAreRejected),
			(nameof(bundle.UnexpectedFilesAreRejectedDuringImport), bundle.UnexpectedFilesAreRejectedDuringImport),
			(nameof(bundle.ExistingBundleCanBeAtomicallyReplaced), bundle.ExistingBundleCanBeAtomicallyReplaced),
			(nameof(bundle.FailedReplacementPreservesTheExistingBundle), bundle.FailedReplacementPreservesTheExistingBundle)
		};

		var failures = 0;
		foreach (var test in tests)
		{
			try
			{
				test.Run();
				Console.WriteLine($"PASS {test.Name}");
			}
			catch (Exception ex)
			{
				failures++;
				Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
			}
		}

		Console.WriteLine($"{tests.Length - failures}/{tests.Length} Redux regression checks passed.");
		return failures == 0 ? 0 : 1;
	}
}

internal static class RegressionAssert
{
	public static void Equal<T>(T expected, T actual)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
			throw new InvalidOperationException($"Expected '{expected}', received '{actual}'.");
	}

	public static void True(bool value)
	{
		if (!value) throw new InvalidOperationException("Expected true, received false.");
	}

	public static void False(bool value)
	{
		if (value) throw new InvalidOperationException("Expected false, received true.");
	}

	public static void Contains(string value, string expectedSubstring)
	{
		if (value?.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase) != true)
			throw new InvalidOperationException($"Expected '{value}' to contain '{expectedSubstring}'.");
	}

	public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
	{
		if (!expected.SequenceEqual(actual))
			throw new InvalidOperationException("Sequences are not equal.");
	}
}

internal sealed class RegressionModData : DivinityModData
{
	public override string GetDisplayName() => Name ?? String.Empty;
}
