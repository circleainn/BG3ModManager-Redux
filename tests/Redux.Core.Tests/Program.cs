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
		var preflight = new PackagePreflightTests();
		var archivePreflight = new ArchivePackagePreflightTests();
		var interactionPerformance = new InteractionPerformanceTests();
		var interactionBehavior = new InteractionBehaviorTests();
		var automaticCategories = new AutomaticModCategoryTests();
		var visualDividerDrag = new VisualDividerDragPolicyTests();
		var visualModSelection = new VisualModSelectionPolicyTests();
		var settingsMaintenance = new SettingsMaintenanceTests();
		var smoothLogicalScroll = new SmoothLogicalScrollPolicyTests();
		var startupNotifications = new StartupNotificationQueueTests();
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
			(nameof(bundle.LegacyBundleWithoutMembershipRemainsUnmigrated), bundle.LegacyBundleWithoutMembershipRemainsUnmigrated),
			(nameof(bundle.ExplicitlyEmptySeparatorMembershipRoundTripsAsEmpty), bundle.ExplicitlyEmptySeparatorMembershipRoundTripsAsEmpty),
			(nameof(bundle.DuplicateSeparatorOwnershipIsRejected), bundle.DuplicateSeparatorOwnershipIsRejected),
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
			(nameof(overlaps.MalformedPackagePathsAreReportedWithoutAbortingTheScan), overlaps.MalformedPackagePathsAreReportedWithoutAbortingTheScan),
			(nameof(preflight.ValidPackageHasNoBlockingFindings), preflight.ValidPackageHasNoBlockingFindings),
			(nameof(preflight.MissingDependencyAndDevelopmentDebrisAreReported), preflight.MissingDependencyAndDevelopmentDebrisAreReported),
			(nameof(preflight.InstalledUpdateUuidIsAReviewWarningInsteadOfADuplicateError), preflight.InstalledUpdateUuidIsAReviewWarningInsteadOfADuplicateError),
			(nameof(preflight.MissingReleaseIdentityIsReportedConservatively), preflight.MissingReleaseIdentityIsReportedConservatively),
			(nameof(archivePreflight.OrdinaryArchiveLayoutHasNoContainerFindings), archivePreflight.OrdinaryArchiveLayoutHasNoContainerFindings),
			(nameof(archivePreflight.UnsafePathsDuplicatesAndDevelopmentDebrisAreReported), archivePreflight.UnsafePathsDuplicatesAndDevelopmentDebrisAreReported),
			(nameof(archivePreflight.ArchiveWithoutPakIsReported), archivePreflight.ArchiveWithoutPakIsReported),
			(nameof(archivePreflight.ZipPakIsStagedForInspectionWithoutChangingTheArchive), archivePreflight.ZipPakIsStagedForInspectionWithoutChangingTheArchive),
			(nameof(interactionPerformance.ReorderingOneRowEmitsOneMoveInsteadOfACollectionReset), interactionPerformance.ReorderingOneRowEmitsOneMoveInsteadOfACollectionReset),
			(nameof(interactionPerformance.RemovingOneRowDoesNotMoveOrResetTheRemainingRows), interactionPerformance.RemovingOneRowDoesNotMoveOrResetTheRemainingRows),
			(nameof(interactionPerformance.UnchangedLargeCollectionUsesLinearComparisonWork), interactionPerformance.UnchangedLargeCollectionUsesLinearComparisonWork),
			(nameof(interactionPerformance.LargeSeparatorProjectionUsesOneCollectionReset), interactionPerformance.LargeSeparatorProjectionUsesOneCollectionReset),
			(nameof(interactionPerformance.SmallSeparatorProjectionKeepsIncrementalNotifications), interactionPerformance.SmallSeparatorProjectionKeepsIncrementalNotifications),
			(nameof(interactionPerformance.AnimatedSeparatorProjectionPreservesRecyclableContainers), interactionPerformance.AnimatedSeparatorProjectionPreservesRecyclableContainers),
			(nameof(interactionPerformance.ImportProgressIsSharedAcrossFilesAndNeverExceedsOne), interactionPerformance.ImportProgressIsSharedAcrossFilesAndNeverExceedsOne),
			(nameof(interactionPerformance.EquivalentCategoryAndHealthDataCanReuseExistingRowBindings), interactionPerformance.EquivalentCategoryAndHealthDataCanReuseExistingRowBindings),
			(nameof(interactionBehavior.DrawerRetainsASelectedModDuringCrossListTransferOnly), interactionBehavior.DrawerRetainsASelectedModDuringCrossListTransferOnly),
			(nameof(interactionBehavior.SavingCurrentOrderCanNeverWriteTheGameExportFile), interactionBehavior.SavingCurrentOrderCanNeverWriteTheGameExportFile),
			(nameof(automaticCategories.NexusCategoryIdsMatchTheBg3ProviderTaxonomy), automaticCategories.NexusCategoryIdsMatchTheBg3ProviderTaxonomy),
			(nameof(automaticCategories.ExplicitNexusCategoryWinsOverContradictoryKeywords), automaticCategories.ExplicitNexusCategoryWinsOverContradictoryKeywords),
			(nameof(automaticCategories.NexusCategoryStaysFirstWhileStrongSecondaryCategoriesFillThreeSlots), automaticCategories.NexusCategoryStaysFirstWhileStrongSecondaryCategoriesFillThreeSlots),
			(nameof(automaticCategories.AutomaticCategoriesNeverExceedThree), automaticCategories.AutomaticCategoriesNeverExceedThree),
			(nameof(automaticCategories.WeakDescriptionMentionsDoNotCreateSecondaryCategoryNoise), automaticCategories.WeakDescriptionMentionsDoNotCreateSecondaryCategoryNoise),
			(nameof(automaticCategories.BundledNexusProjectPreservesItsAuthorCategoryOffline), automaticCategories.BundledNexusProjectPreservesItsAuthorCategoryOffline),
			(nameof(automaticCategories.NativeModioCategoryWinsOverASecondaryNexusMatch), automaticCategories.NativeModioCategoryWinsOverASecondaryNexusMatch),
			(nameof(automaticCategories.UnknownProviderTaxonomyFallsBackToPackageKeywords), automaticCategories.UnknownProviderTaxonomyFallsBackToPackageKeywords),
			(nameof(automaticCategories.DisabledProviderCategoryFallsBackToAnEnabledCategory), automaticCategories.DisabledProviderCategoryFallsBackToAnEnabledCategory),
			(nameof(visualDividerDrag.NormalModDragNeverIncludesASelectedDivider), visualDividerDrag.NormalModDragNeverIncludesASelectedDivider),
			(nameof(visualDividerDrag.ExpandedDividerDragContainsOnlyItsMarker), visualDividerDrag.ExpandedDividerDragContainsOnlyItsMarker),
			(nameof(visualDividerDrag.CollapsedDividerCannotStartDrag), visualDividerDrag.CollapsedDividerCannotStartDrag),
			(nameof(visualDividerDrag.ExpandedSeparatorMoveLeavesEveryModInPlace), visualDividerDrag.ExpandedSeparatorMoveLeavesEveryModInPlace),
			(nameof(visualDividerDrag.RecreatedExpandedSeparatorResolvesToCanonicalMarkerOnly), visualDividerDrag.RecreatedExpandedSeparatorResolvesToCanonicalMarkerOnly),
			(nameof(visualDividerDrag.DropAfterCollapsedSeparatorSkipsItsHiddenSection), visualDividerDrag.DropAfterCollapsedSeparatorSkipsItsHiddenSection),
			(nameof(visualDividerDrag.CollapsedSeparatorOwnsEveryInsertionSlotUntilTheNextSeparator), visualDividerDrag.CollapsedSeparatorOwnsEveryInsertionSlotUntilTheNextSeparator),
			(nameof(visualDividerDrag.VisibleDropSlotMapsPastOmittedCollapsedMembers), visualDividerDrag.VisibleDropSlotMapsPastOmittedCollapsedMembers),
			(nameof(visualDividerDrag.VisibleDropSlotMatchesRecreatedDividerByIdentity), visualDividerDrag.VisibleDropSlotMatchesRecreatedDividerByIdentity),
			(nameof(visualDividerDrag.ProgressiveExpansionInsertsBeforeUnownedDestinationSuffix), visualDividerDrag.ProgressiveExpansionInsertsBeforeUnownedDestinationSuffix),
			(nameof(visualDividerDrag.CollapseAllChangesOnlyTheRequestedPaneAndOnlyOnce), visualDividerDrag.CollapseAllChangesOnlyTheRequestedPaneAndOnlyOnce),
			(nameof(visualDividerDrag.LegacyPositionsMigrateToDurableSectionMembership), visualDividerDrag.LegacyPositionsMigrateToDurableSectionMembership),
			(nameof(visualDividerDrag.LegacyMembershipWaitsForCompletedListLoading), visualDividerDrag.LegacyMembershipWaitsForCompletedListLoading),
			(nameof(visualDividerDrag.VisualSequencePreservesAuthoritativeModOrder), visualDividerDrag.VisualSequencePreservesAuthoritativeModOrder),
			(nameof(visualDividerDrag.DuplicateOwnershipKeepsFirstDividerAndMissingIds), visualDividerDrag.DuplicateOwnershipKeepsFirstDividerAndMissingIds),
			(nameof(visualDividerDrag.CollapsedVisibilityUsesExplicitMembershipOnly), visualDividerDrag.CollapsedVisibilityUsesExplicitMembershipOnly),
			(nameof(visualDividerDrag.CollapsedVisibilityStopsAtTheNextSeparator), visualDividerDrag.CollapsedVisibilityStopsAtTheNextSeparator),
			(nameof(visualModSelection.SelectAllIncludesOnlyVisibleModRows), visualModSelection.SelectAllIncludesOnlyVisibleModRows),
			(nameof(visualModSelection.FilterProjectionOmitsCollapsedRowsFromTheItemsSource), visualModSelection.FilterProjectionOmitsCollapsedRowsFromTheItemsSource),
			(nameof(settingsMaintenance.RestoringAutomaticCategoriesClearsCurrentAndLegacyAssignmentsOnly), settingsMaintenance.RestoringAutomaticCategoriesClearsCurrentAndLegacyAssignmentsOnly),
			(nameof(settingsMaintenance.RestoringAutomaticCategoriesMakesTheClassifierAuthoritativeAgain), settingsMaintenance.RestoringAutomaticCategoriesMakesTheClassifierAuthoritativeAgain),
			(nameof(smoothLogicalScroll.PartialWheelDeltasAccumulateWithoutPrematureScrolling), smoothLogicalScroll.PartialWheelDeltasAccumulateWithoutPrematureScrolling),
			(nameof(smoothLogicalScroll.LargeWheelBurstsStayWithinTheAnimationSafetyCap), smoothLogicalScroll.LargeWheelBurstsStayWithinTheAnimationSafetyCap),
			(nameof(smoothLogicalScroll.SmoothScrollingIsStandardUnlessMotionOrInteractionSuppressesIt), smoothLogicalScroll.SmoothScrollingIsStandardUnlessMotionOrInteractionSuppressesIt),
			(nameof(smoothLogicalScroll.MixedHeightRowsProduceDirectionCorrectCompensation), smoothLogicalScroll.MixedHeightRowsProduceDirectionCorrectCompensation),
			(nameof(smoothLogicalScroll.MissingCachedRowsUseAStableFallbackWithoutChangingDirection), smoothLogicalScroll.MissingCachedRowsUseAStableFallbackWithoutChangingDirection),
			(nameof(smoothLogicalScroll.ScrollRangeIsKnownBeforeDeferredLayoutPublishesTheNewOffset), smoothLogicalScroll.ScrollRangeIsKnownBeforeDeferredLayoutPublishesTheNewOffset),
			(nameof(startupNotifications.StartupNotificationsWaitForReadinessAndDrainInOrder), startupNotifications.StartupNotificationsWaitForReadinessAndDrainInOrder),
			(nameof(startupNotifications.RepeatedStartupNotificationUsesLatestDataExactlyOnce), startupNotifications.RepeatedStartupNotificationUsesLatestDataExactlyOnce),
			(nameof(startupNotifications.StaleStartupNotificationCanBeCancelledBeforeReadiness), startupNotifications.StaleStartupNotificationCanBeCancelledBeforeReadiness),
			(nameof(startupNotifications.NotificationsQueuedDuringDrainRemainSequential), startupNotifications.NotificationsQueuedDuringDrainRemainSequential)
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
