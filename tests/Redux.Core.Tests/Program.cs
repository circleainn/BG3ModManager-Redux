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
		var modules = new ReduxModuleStateTests();
		var bundle = new ReduxBundleTests();
		var contribution = new ContributionReportPrivacyTests();
		var comparison = new LoadOrderComparisonTests();
		var restorePoints = new LoadOrderRestorePointTests();
		var annotations = new ModAnnotationTests();
		var overlaps = new ModFileOverlapTests();
		var tests = new (string Name, Action Run)[]
		{
			(nameof(source.ReviewedModuleUuidResolvesItsProject), source.ReviewedModuleUuidResolvesItsProject),
			(nameof(source.MissingDependencyOffersReviewedSourceOnlyWhenIntegrationsAreEnabled), source.MissingDependencyOffersReviewedSourceOnlyWhenIntegrationsAreEnabled),
			(nameof(source.CurrentNexusArchiveNamesResolveTheirProject), source.CurrentNexusArchiveNamesResolveTheirProject),
			(nameof(source.TransitionalNexusArchiveNamesResolveTheirProject), source.TransitionalNexusArchiveNamesResolveTheirProject),
			(nameof(source.LegacyNexusArchiveNamesResolveTheirProjectWithoutInventingAFileId), source.LegacyNexusArchiveNamesResolveTheirProjectWithoutInventingAFileId),
			(nameof(source.UnrelatedNumberedArchiveNamesRemainUnmatched), source.UnrelatedNumberedArchiveNamesRemainUnmatched),
			(nameof(source.MatchingNexusCreatorAndUploaderUseOneLinkedCreatorLabel), source.MatchingNexusCreatorAndUploaderUseOneLinkedCreatorLabel),
			(nameof(source.ManualNexusAssociationWinsOverCachedModioMetadata), source.ManualNexusAssociationWinsOverCachedModioMetadata),
			(nameof(source.CachedModioMetadataWinsOverAutomaticNexusMetadata), source.CachedModioMetadataWinsOverAutomaticNexusMetadata),
			(nameof(source.NexusArchiveImportWinsOverNativeModioMetadata), source.NexusArchiveImportWinsOverNativeModioMetadata),
			(nameof(source.DeletingAnInstalledModRetiresItsRememberedSourceAssociations), source.DeletingAnInstalledModRetiresItsRememberedSourceAssociations),
			(nameof(source.LocalOnlyPresentationHidesProvidersWithoutDeletingCachedMetadata), source.LocalOnlyPresentationHidesProvidersWithoutDeletingCachedMetadata),
			(nameof(source.LocalMetadataUsesExplicitUnavailableFallbacks), source.LocalMetadataUsesExplicitUnavailableFallbacks),
			(nameof(source.CreatorManifestModioCacheRequiresTheCurrentProjectClaim), source.CreatorManifestModioCacheRequiresTheCurrentProjectClaim),
			(nameof(source.ManualSourceChoicesBlockCreatorManifestModioCache), source.ManualSourceChoicesBlockCreatorManifestModioCache),
			(nameof(source.NativeModioCacheDoesNotDependOnCreatorManifest), source.NativeModioCacheDoesNotDependOnCreatorManifest),
			(nameof(source.CreatorManifestNexusCacheRequiresTheCurrentProjectClaim), source.CreatorManifestNexusCacheRequiresTheCurrentProjectClaim),
			(nameof(source.ManualAndNativeSourceChoicesBlockCreatorManifestNexusCache), source.ManualAndNativeSourceChoicesBlockCreatorManifestNexusCache),
			(nameof(source.NonManifestNexusCacheDoesNotDependOnCreatorManifest), source.NonManifestNexusCacheDoesNotDependOnCreatorManifest),
			(nameof(source.ValidCreatorManifestNexusCacheSurvivesRestart), source.ValidCreatorManifestNexusCacheSurvivesRestart),
			(nameof(source.ChangedCreatorManifestInvalidatesReloadedNexusCache), source.ChangedCreatorManifestInvalidatesReloadedNexusCache),
			(nameof(source.NativeModioCacheWinsOverCreatorManifestNexusAfterRestart), source.NativeModioCacheWinsOverCreatorManifestNexusAfterRestart),
			(nameof(source.ManualNexusCacheBlocksCreatorManifestModioAfterRestart), source.ManualNexusCacheBlocksCreatorManifestModioAfterRestart),
			(nameof(manifest.ValidManifestPreservesCreatorAuthorOrder), manifest.ValidManifestPreservesCreatorAuthorOrder),
			(nameof(manifest.CompactNexusManifestLinksThePrimaryModule), manifest.CompactNexusManifestLinksThePrimaryModule),
			(nameof(manifest.CompactNexusManifestRejectsAnUnrelatedModule), manifest.CompactNexusManifestRejectsAnUnrelatedModule),
			(nameof(manifest.CompactNexusManifestRejectsASecondaryModule), manifest.CompactNexusManifestRejectsASecondaryModule),
			(nameof(manifest.CompactNexusManifestPreservesAnOptionalFileId), manifest.CompactNexusManifestPreservesAnOptionalFileId),
			(nameof(manifest.CompactAndDetailedManifestFormsCannotBeMixed), manifest.CompactAndDetailedManifestFormsCannotBeMixed),
			(nameof(manifest.DuplicateAuthorsAreRejected), manifest.DuplicateAuthorsAreRejected),
			(nameof(manifest.MismatchedPakClaimIsRejected), manifest.MismatchedPakClaimIsRejected),
			(nameof(manifest.DuplicateJsonPropertiesAreRejected), manifest.DuplicateJsonPropertiesAreRejected),
			(nameof(manifest.TrailingJsonContentIsRejected), manifest.TrailingJsonContentIsRejected),
			(nameof(manifest.HomepageMustUsePublicHttpOrHttps), manifest.HomepageMustUsePublicHttpOrHttps),
			(nameof(manifest.PakExtensionMatchingIsCaseInsensitive), manifest.PakExtensionMatchingIsCaseInsensitive),
			(nameof(health.MissingAndInactiveDependenciesRemainIndependentOfLoadOrderGuidance), health.MissingAndInactiveDependenciesRemainIndependentOfLoadOrderGuidance),
			(nameof(health.LoadOrderGuidanceFindingsAreAbsentUntilEnabled), health.LoadOrderGuidanceFindingsAreAbsentUntilEnabled),
			(nameof(health.CorrectDependencyPlacementDoesNotProduceGuidanceNoise), health.CorrectDependencyPlacementDoesNotProduceGuidanceNoise),
			(nameof(health.InvalidUuidIsReportedAsAReadOnlyHealthError), health.InvalidUuidIsReportedAsAReadOnlyHealthError),
			(nameof(health.SelfDependencyIsReportedWithoutDuplicateMissingDependencyNoise), health.SelfDependencyIsReportedWithoutDuplicateMissingDependencyNoise),
			(nameof(health.DependencyCyclesAreReportedOnlyByOptInGuidance), health.DependencyCyclesAreReportedOnlyByOptInGuidance),
			(nameof(health.InvalidCreatorManifestIsReportedWithoutApplyingItsClaims), health.InvalidCreatorManifestIsReportedWithoutApplyingItsClaims),
			(nameof(health.DuplicateUuidsAreReportedWithoutRemovingEitherPackage), health.DuplicateUuidsAreReportedWithoutRemovingEitherPackage),
			(nameof(health.ActiveDeclaredConflictsAreReportedConservatively), health.ActiveDeclaredConflictsAreReportedConservatively),
			(nameof(health.OlderInstalledDependencyVersionsAreReportedWithoutUpdatingThem), health.OlderInstalledDependencyVersionsAreReportedWithoutUpdatingThem),
			(nameof(health.ScriptExtenderErrorsAndWarningsRemainDistinct), health.ScriptExtenderErrorsAndWarningsRemainDistinct),
			(nameof(health.ForceLoadedVariantsRemainInformationalAndReadOnly), health.ForceLoadedVariantsRemainInformationalAndReadOnly),
			(nameof(health.LocalOnlyPresentationSuppressesProviderFindingsWithoutDeletingMetadata), health.LocalOnlyPresentationSuppressesProviderFindingsWithoutDeletingMetadata),
			(nameof(health.DisablingModioWarningsHidesOnlyThatFinding), health.DisablingModioWarningsHidesOnlyThatFinding),
			(nameof(modules.DefaultsKeepModDiagnosticsOnAndGuidanceOptIn), modules.DefaultsKeepModDiagnosticsOnAndGuidanceOptIn),
			(nameof(modules.CategoryInteractionSettingSynchronizesLegacyPresentationFlags), modules.CategoryInteractionSettingSynchronizesLegacyPresentationFlags),
			(nameof(modules.IconsOnlySettingSynchronizesLegacySourceFlag), modules.IconsOnlySettingSynchronizesLegacySourceFlag),
			(nameof(modules.CustomThemeClonePreservesUnifiedPresentationSettings), modules.CustomThemeClonePreservesUnifiedPresentationSettings),
			(nameof(modules.CustomThemePreviewRegeneratesEverySemanticPillGradient), modules.CustomThemePreviewRegeneratesEverySemanticPillGradient),
			(nameof(modules.CustomThemeBackgroundEditsPreserveUntouchedBaseRoles), modules.CustomThemeBackgroundEditsPreserveUntouchedBaseRoles),
			(nameof(modules.LocalOnlyModeChangesOnlySourceIntegrations), modules.LocalOnlyModeChangesOnlySourceIntegrations),
			(nameof(modules.LoadOrderGuidanceRequiresDiagnosticsWithoutLosingItsPreference), modules.LoadOrderGuidanceRequiresDiagnosticsWithoutLosingItsPreference),
			(nameof(modules.DisposedModuleStateStopsTrackingSettings), modules.DisposedModuleStateStopsTrackingSettings),
			(nameof(modules.DisabledNexusProviderCannotInitializeItsClient), modules.DisabledNexusProviderCannotInitializeItsClient),
			(nameof(bundle.BundleRoundTripPreservesOrderAndReduxPresentation), bundle.BundleRoundTripPreservesOrderAndReduxPresentation),
			(nameof(bundle.BundleNeverContainsModsettingsLsx), bundle.BundleNeverContainsModsettingsLsx),
			(nameof(bundle.MismatchedOrderAndPresentationAreRejected), bundle.MismatchedOrderAndPresentationAreRejected),
			(nameof(bundle.UnexpectedFilesAreRejectedDuringImport), bundle.UnexpectedFilesAreRejectedDuringImport),
			(nameof(bundle.ExistingBundleCanBeAtomicallyReplaced), bundle.ExistingBundleCanBeAtomicallyReplaced),
			(nameof(bundle.FailedReplacementPreservesTheExistingBundle), bundle.FailedReplacementPreservesTheExistingBundle),
			(nameof(bundle.PrivateNotesRoundTripOnlyWhenPresent), bundle.PrivateNotesRoundTripOnlyWhenPresent),
			(nameof(bundle.PrivateNotesCannotReferenceModsOutsideTheOrder), bundle.PrivateNotesCannotReferenceModsOutsideTheOrder),
			(nameof(contribution.ContributionReportsIncludeOnlyUniqueInstalledUserMods), contribution.ContributionReportsIncludeOnlyUniqueInstalledUserMods),
			(nameof(contribution.ContributionReportsStripPrivatePathsAndOrderingData), contribution.ContributionReportsStripPrivatePathsAndOrderingData),
			(nameof(contribution.ContributionReportsRejectCredentialBearingProviderUrls), contribution.ContributionReportsRejectCredentialBearingProviderUrls),
			(nameof(contribution.TamperedContributionReportsCannotBeSaved), contribution.TamperedContributionReportsCannotBeSaved),
			(nameof(comparison.ReportsActivationDeactivationAndAutomaticDependencies), comparison.ReportsActivationDeactivationAndAutomaticDependencies),
			(nameof(comparison.SavedOrderComparisonTreatsRightOnlyModsAsIntentionalAdditions), comparison.SavedOrderComparisonTreatsRightOnlyModsAsIntentionalAdditions),
			(nameof(comparison.AddedOrRemovedModsDoNotCreateFalsePositionChanges), comparison.AddedOrRemovedModsDoNotCreateFalsePositionChanges),
			(nameof(comparison.ReportsTheSmallestPlacementChangeForASingleMove), comparison.ReportsTheSmallestPlacementChangeForASingleMove),
			(nameof(comparison.IgnoresDuplicateAndBlankEntriesButRetainsMissingBaselineMods), comparison.IgnoresDuplicateAndBlankEntriesButRetainsMissingBaselineMods),
			(nameof(comparison.PreservesFirstExportState), comparison.PreservesFirstExportState),
			(nameof(restorePoints.RoundTripPreservesProfileReasonAndOrder), restorePoints.RoundTripPreservesProfileReasonAndOrder),
			(nameof(restorePoints.EmptyExportedOrderCanBeRestored), restorePoints.EmptyExportedOrderCanBeRestored),
			(nameof(restorePoints.RetentionKeepsOnlyTheNewestTwentySnapshots), restorePoints.RetentionKeepsOnlyTheNewestTwentySnapshots),
			(nameof(restorePoints.RestorePointsFromAnotherProfileAreRejected), restorePoints.RestorePointsFromAnotherProfileAreRejected),
			(nameof(restorePoints.InvalidSnapshotsAreIgnoredWithoutLeavingTemporaryFiles), restorePoints.InvalidSnapshotsAreIgnoredWithoutLeavingTemporaryFiles),
			(nameof(restorePoints.DeleteRemovesOnlyTheMatchingProfileSnapshot), restorePoints.DeleteRemovesOnlyTheMatchingProfileSnapshot),
			(nameof(annotations.AnnotationsRoundTripWithoutPackageOrProfileData), annotations.AnnotationsRoundTripWithoutPackageOrProfileData),
			(nameof(annotations.ClearingTheLastValueRemovesTheAnnotation), annotations.ClearingTheLastValueRemovesTheAnnotation),
			(nameof(annotations.OversizedNotesAreRejectedBeforeTheStoreChanges), annotations.OversizedNotesAreRejectedBeforeTheStoreChanges),
			(nameof(annotations.BulkNotesUpdateAtomically), annotations.BulkNotesUpdateAtomically),
			(nameof(overlaps.NormalizesSlashAndCaseDifferences), overlaps.NormalizesSlashAndCaseDifferences),
			(nameof(overlaps.DuplicatePathsInsideOnePackageAreNotOverlaps), overlaps.DuplicatePathsInsideOnePackageAreNotOverlaps),
			(nameof(overlaps.ExcludesUniquePathsAndCountsAffectedPackages), overlaps.ExcludesUniquePathsAndCountsAffectedPackages),
			(nameof(overlaps.OrdersBroadestOverlapsBeforePathName), overlaps.OrdersBroadestOverlapsBeforePathName),
			(nameof(overlaps.MalformedPackagePathsAreReportedWithoutAbortingTheScan), overlaps.MalformedPackagePathsAreReportedWithoutAbortingTheScan)
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
