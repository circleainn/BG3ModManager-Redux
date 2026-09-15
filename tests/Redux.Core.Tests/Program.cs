using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

using DivinityModManager.Models;

namespace Redux.Core.Tests;

internal static class Program
{
	[STAThread]
	private static int Main()
	{
		// Register WPF's pack URI support before exercising GUI-owned, nonvisual
		// services such as the portable Redux bundle reader/writer.
		_ = Application.Current ?? new Application();

		var dialogLayout = new DialogLayoutTests();
		var collections = new NexusCollectionPreviewTests();
		var saveReview = new SaveModReviewTests();
		var whatsNew = new WhatsNewTests();
		var extenderExport = new ExtenderSettingsExportTests();
		var releaseFlow = new ReleaseFlowTests();
		var batchInstallUi = new BatchInstallUiTests();
		var downloadNotification = new DownloadNotificationTests();
		var processToken = new ProcessTokenTests();
		var dismissal = new WindowDismissalTests();
		var placement = new WindowPlacementPolicyTests();
		var source = new SourceAssociationTests();
		var manifest = new CreatorManifestValidationTests();
		var health = new ModHealthTests();
		var advisorKnowledge = new LoadOrderAdvisorKnowledgeTests();
		var advisorEvidence = new AdvisorEvidenceTests();
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
		var inactiveOrder = new InactiveModOrderTests();
		var visualModSelection = new VisualModSelectionPolicyTests();
		var settingsMaintenance = new SettingsMaintenanceTests();
		var smoothLogicalScroll = new SmoothLogicalScrollPolicyTests();
		var startupNotifications = new StartupNotificationQueueTests();
		var commandPaletteSearch = new CommandPaletteSearchTests();
		var fileSafety = new FileSafetyTests();
		var loadOrderWorkflow = new LoadOrderWorkflowTests();
		var undoRedoHistory = new UndoRedoHistoryTests();
		var loadOrderOrganizer = new LoadOrderAdvisorOrganizerTests();
		var saveGames = new SaveGameServiceTests();
		var nativeMods = new ReduxGameDirectoryInstallServiceTests();
		var nxmLinks = new NexusModManagerLinkTests();
		var nxmResolver = new NexusNxmResolverTests();
		var nxmActivation = new NxmActivationTests();
		var nxmAssociation = new NxmAssociationTests();
		var nxmManager = new NxmDownloadManagerTests();
		var nxmScheduler = new NxmDownloadSchedulerTests();
		var nxmStore = new NxmDownloadStoreTests();
		var nxmTransfer = new NxmTransferTests();
		var packageArchives = new RetainedPackageArchiveServiceTests();
		var downloadBatch = new DownloadBatchSafetyPlannerTests();
		var applicationUpdates = new ReduxUpdateManifestTests();
		var updateChannel = new ReduxUpdateChannelServiceTests();
		var updateWindow = new AppUpdateWindowViewModelTests();
		var updatePackages = new ReduxUpdatePackageServiceTests();
		var updateLauncher = new ReduxUpdateLaunchServiceTests();
		var updateTransaction = new ReduxUpdateTransactionTests();
		var releaseVersions = new ReleaseVersionContractTests();
		var tests = new (string Name, Action Run)[]
		{
			(nameof(collections.CollectionManifestDownloadKeepsAccountHeadersOnApiHost), collections.CollectionManifestDownloadKeepsAccountHeadersOnApiHost),
			(nameof(collections.CollectionOrderUsesExplicitEnabledUuidSequence), collections.CollectionOrderUsesExplicitEnabledUuidSequence),
			(nameof(collections.CollectionInventoryMatchesExactFilesAcrossBothPanes), collections.CollectionInventoryMatchesExactFilesAcrossBothPanes),
			(nameof(collections.CollectionGuideAdvancesOnlyMatchingFilesAwaitingAuthorization), collections.CollectionGuideAdvancesOnlyMatchingFilesAwaitingAuthorization),
			(nameof(collections.CollectionSessionsRoundTripSelectionsAndSeparateRevisions), collections.CollectionSessionsRoundTripSelectionsAndSeparateRevisions),
			(nameof(collections.CollectionSessionsRejectCorruptDataWithoutOverwritingIt), collections.CollectionSessionsRejectCorruptDataWithoutOverwritingIt),
			(nameof(collections.CollectionFiltersKeepHiddenSelectionsAndLimitBulkActions), collections.CollectionFiltersKeepHiddenSelectionsAndLimitBulkActions),
			(nameof(collections.CollectionRetrySkipsUnsafeAndUnrelatedItemsAndContinuesAfterFailure), collections.CollectionRetrySkipsUnsafeAndUnrelatedItemsAndContinuesAfterFailure),
			(nameof(collections.CollectionOrderReviewNeverSelectsModsForActivation), collections.CollectionOrderReviewNeverSelectsModsForActivation),
			(nameof(collections.CollectionLinksAreRestrictedToBg3OnNexus), collections.CollectionLinksAreRestrictedToBg3OnNexus),
			(nameof(collections.CollectionBulkSelectionRespectsAvailabilityAndDefaults), collections.CollectionBulkSelectionRespectsAvailabilityAndDefaults),
			(nameof(collections.CollectionPreviewPreservesFilesAndUnavailableEntries), collections.CollectionPreviewPreservesFilesAndUnavailableEntries),
			(nameof(collections.CollectionPreviewRejectsWrongIdentityAndPartialResponses), collections.CollectionPreviewRejectsWrongIdentityAndPartialResponses),
			(nameof(saveReview.MatchesSaveUUIDsWithoutNameFallbackOrMutation), saveReview.MatchesSaveUUIDsWithoutNameFallbackOrMutation),
			(nameof(saveReview.AmbiguousAndInvalidRequirementsCannotActivate), saveReview.AmbiguousAndInvalidRequirementsCannotActivate),
			(nameof(saveReview.ActivationKeepsSaveSequenceAndExistingActiveOrder), saveReview.ActivationKeepsSaveSequenceAndExistingActiveOrder),
			(nameof(saveReview.ParentDropBlockSurvivesNestedDialogs), saveReview.ParentDropBlockSurvivesNestedDialogs),
			(nameof(saveReview.SavePackageReviewDistinguishesEmptyMissingAndMalformedMetadata), saveReview.SavePackageReviewDistinguishesEmptyMissingAndMalformedMetadata),
			(nameof(saveReview.SaveKindsRecognizeGeneratedNamesWithoutMatchingCustomTitles), saveReview.SaveKindsRecognizeGeneratedNamesWithoutMatchingCustomTitles),
			(nameof(saveReview.EmptyReviewDoesNotOfferActivation), saveReview.EmptyReviewDoesNotOfferActivation),
			(nameof(saveReview.SaveRowMenuUsesClickedSaveAndExistingReviewState), saveReview.SaveRowMenuUsesClickedSaveAndExistingReviewState),
			(nameof(saveReview.CompanionPortraitsResolveAndUnknownOriginsRemainGeneric), saveReview.CompanionPortraitsResolveAndUnknownOriginsRemainGeneric),
			(nameof(saveReview.SaveWarningsPreserveSelectionIdentity), saveReview.SaveWarningsPreserveSelectionIdentity),
			(nameof(saveReview.DatabasePresentationKeepsRecordedIdentityAndActivationRules), saveReview.DatabasePresentationKeepsRecordedIdentityAndActivationRules),
			(nameof(releaseVersions.ApplicationAndBinaryVersionsIdentifyTheSameAlphaRelease), releaseVersions.ApplicationAndBinaryVersionsIdentifyTheSameAlphaRelease),
			(nameof(updateTransaction.TransactionReplacesOwnedFilesAndPreservesUserFiles), updateTransaction.TransactionReplacesOwnedFilesAndPreservesUserFiles),
			(nameof(updateTransaction.FailedReplacementRollsBackFilesChangedEarlierInTheTransaction), updateTransaction.FailedReplacementRollsBackFilesChangedEarlierInTheTransaction),
			(nameof(updateTransaction.ReleaseInventoryCannotClaimUserState), updateTransaction.ReleaseInventoryCannotClaimUserState),
			(nameof(updateTransaction.ReadOnlyInstalledFilesCanBeReplaced), updateTransaction.ReadOnlyInstalledFilesCanBeReplaced),
			(nameof(updateLauncher.CompletedUpdateResultIsShownOnce), updateLauncher.CompletedUpdateResultIsShownOnce),
			(nameof(updateLauncher.QueuedRunnerLivesOutsideTheInstallationAndCancellationCleansIt), updateLauncher.QueuedRunnerLivesOutsideTheInstallationAndCancellationCleansIt),
			(nameof(updatePackages.VerifiedArchiveStagesWithoutChangingAnInstallation), updatePackages.VerifiedArchiveStagesWithoutChangingAnInstallation),
			(nameof(updatePackages.TraversalEntryIsRejectedAndStagingIsRemoved), updatePackages.TraversalEntryIsRejectedAndStagingIsRemoved),
			(nameof(updatePackages.UnlistedArchiveContentIsRejected), updatePackages.UnlistedArchiveContentIsRejected),
			(nameof(updatePackages.CleanupRemovesOnlyOldReduxTransactionDirectories), updatePackages.CleanupRemovesOnlyOldReduxTransactionDirectories),
			(nameof(updateWindow.AvailableUpdateOffersVerifiedRestart), updateWindow.AvailableUpdateOffersVerifiedRestart),
			(nameof(updateWindow.CurrentReleaseStaysQuietDuringAutomaticCheck), updateWindow.CurrentReleaseStaysQuietDuringAutomaticCheck),
			(nameof(updateWindow.ManualFailureExplainsThatTheInstallationWasNotChanged), updateWindow.ManualFailureExplainsThatTheInstallationWasNotChanged),
			(nameof(updateChannel.FetchesAndEvaluatesTheOfficialChannelManifest), updateChannel.FetchesAndEvaluatesTheOfficialChannelManifest),
			(nameof(updateChannel.RejectsOversizedManifestBeforeReadingItsBody), updateChannel.RejectsOversizedManifestBeforeReadingItsBody),
			(nameof(updateChannel.RejectsUntrustedChannelEndpoint), updateChannel.RejectsUntrustedChannelEndpoint),
			(nameof(updateChannel.AutomaticChecksUseTheLastSuccessfulCheckTime), updateChannel.AutomaticChecksUseTheLastSuccessfulCheckTime),
			(nameof(updateChannel.FailedAutomaticChecksBackOffBeforeRetrying), updateChannel.FailedAutomaticChecksBackOffBeforeRetrying),
			(nameof(applicationUpdates.ValidPublicAlphaManifestSelectsPortableArtifact), applicationUpdates.ValidPublicAlphaManifestSelectsPortableArtifact),
			(nameof(applicationUpdates.SameAndNewerInstalledVersionsAreNeverOfferedAsUpdates), applicationUpdates.SameAndNewerInstalledVersionsAreNeverOfferedAsUpdates),
			(nameof(applicationUpdates.HotfixVersionsUpdateTheirBaseAndOrderBeforeTheNextAlpha), applicationUpdates.HotfixVersionsUpdateTheirBaseAndOrderBeforeTheNextAlpha),
			(nameof(applicationUpdates.MaintenanceVersionsOrderBetweenTheirHotfixAndTheNextHotfix), applicationUpdates.MaintenanceVersionsOrderBetweenTheirHotfixAndTheNextHotfix),
			(nameof(applicationUpdates.HotfixVersionsRejectZeroOverflowAndMismatchedInternalVersions), applicationUpdates.HotfixVersionsRejectZeroOverflowAndMismatchedInternalVersions),
			(nameof(applicationUpdates.ManifestRequiresExactlyOnePortableArtifact), applicationUpdates.ManifestRequiresExactlyOnePortableArtifact),
			(nameof(applicationUpdates.ManifestRejectsDuplicateAndUnknownProperties), applicationUpdates.ManifestRejectsDuplicateAndUnknownProperties),
			(nameof(applicationUpdates.ManifestRejectsTrailingContentAndWrongChannel), applicationUpdates.ManifestRejectsTrailingContentAndWrongChannel),
			(nameof(applicationUpdates.ManifestRejectsMismatchedDisplayAndInternalVersions), applicationUpdates.ManifestRejectsMismatchedDisplayAndInternalVersions),
			(nameof(applicationUpdates.ManifestRejectsUntrustedArtifactAndReleaseNotesUrls), applicationUpdates.ManifestRejectsUntrustedArtifactAndReleaseNotesUrls),
			(nameof(applicationUpdates.ArtifactVerificationRequiresMatchingLengthAndSha256), applicationUpdates.ArtifactVerificationRequiresMatchingLengthAndSha256),
			(nameof(nxmManager.LocalPackageIsCopiedHashedAndDeduplicatedInTheSharedInbox), nxmManager.LocalPackageIsCopiedHashedAndDeduplicatedInTheSharedInbox),
			(nameof(nxmManager.UnsafeLocalPackageRemainsVisibleButCannotEnterInstallState), nxmManager.UnsafeLocalPackageRemainsVisibleButCannotEnterInstallState),
			(nameof(interactionBehavior.ReduceMotionKeepsPrimaryListStoryboardsFreezeSafeAndInstant), interactionBehavior.ReduceMotionKeepsPrimaryListStoryboardsFreezeSafeAndInstant),
			(nameof(interactionBehavior.SaveCampaignAnimationReplacesFrozenTransforms), interactionBehavior.SaveCampaignAnimationReplacesFrozenTransforms),
			(nameof(interactionBehavior.ModListHeaderSpansTheGutterAndScrollbarStartsBelowIt), interactionBehavior.ModListHeaderSpansTheGutterAndScrollbarStartsBelowIt),
			(nameof(interactionBehavior.CustomThemeEditorShellsPreviewTheBackgroundRoleLive), interactionBehavior.CustomThemeEditorShellsPreviewTheBackgroundRoleLive),
			(nameof(interactionBehavior.PreferencesAndEditorActionsUseModernChromeAndLabeledIcons), interactionBehavior.PreferencesAndEditorActionsUseModernChromeAndLabeledIcons),
			(nameof(interactionBehavior.OnboardingAppearancePreviewsAndRestoresWithoutSaving), interactionBehavior.OnboardingAppearancePreviewsAndRestoresWithoutSaving),
			(nameof(interactionBehavior.OnboardingKeepsActionsVisibleAtItsMinimumSupportedSize), interactionBehavior.OnboardingKeepsActionsVisibleAtItsMinimumSupportedSize),
			(nameof(interactionBehavior.PopupPlacementPrefersRightwardGrowthWithScreenEdgeFallbacks), interactionBehavior.PopupPlacementPrefersRightwardGrowthWithScreenEdgeFallbacks),
			(nameof(interactionBehavior.MessageBoxSupportsExplicitElevationWarningActions), interactionBehavior.MessageBoxSupportsExplicitElevationWarningActions),
			(nameof(source.ReviewedModuleUuidResolvesItsProject), source.ReviewedModuleUuidResolvesItsProject),
			(nameof(source.CommunityModuleUuidResolvesItsDependencySource), source.CommunityModuleUuidResolvesItsDependencySource),
			(nameof(source.ReviewedLegacyNexusModsResolveTheirCorrectProjects), source.ReviewedLegacyNexusModsResolveTheirCorrectProjects),
			(nameof(source.ExactNexusFilesUseDistinctPackageTitlesWithoutReplacingTheProjectTitle), source.ExactNexusFilesUseDistinctPackageTitlesWithoutReplacingTheProjectTitle),
			(nameof(source.ModioManualLinkParserAcceptsOnlyBg3Projects), source.ModioManualLinkParserAcceptsOnlyBg3Projects),
			(nameof(source.ManualModioAssociationSurvivesCacheRoundTrip), source.ManualModioAssociationSurvivesCacheRoundTrip),
			(nameof(source.CommunityIdentityRequiresTheInstalledPackageNameToAgree), source.CommunityIdentityRequiresTheInstalledPackageNameToAgree),
			(nameof(source.CrossProviderCatalogNameRemainsUnresolved), source.CrossProviderCatalogNameRemainsUnresolved),
			(nameof(source.ProviderExclusiveModioCatalogIdentityResolvesConservatively), source.ProviderExclusiveModioCatalogIdentityResolvesConservatively),
			(nameof(source.CommunityUuidDoesNotRelabelAnUnrelatedLocalPackage), source.CommunityUuidDoesNotRelabelAnUnrelatedLocalPackage),
			(nameof(source.CommunityProjectNameAndAuthorDoNotBypassUuidCorroboration), source.CommunityProjectNameAndAuthorDoNotBypassUuidCorroboration),
			(nameof(source.MissingDependencyOffersReviewedSourceOnlyWhenIntegrationsAreEnabled), source.MissingDependencyOffersReviewedSourceOnlyWhenIntegrationsAreEnabled),
			(nameof(source.CurrentNexusArchiveNamesResolveTheirProject), source.CurrentNexusArchiveNamesResolveTheirProject),
			(nameof(source.TransitionalNexusArchiveNamesResolveTheirProject), source.TransitionalNexusArchiveNamesResolveTheirProject),
			(nameof(source.LegacyNexusArchiveNamesResolveTheirProjectWithoutInventingAFileId), source.LegacyNexusArchiveNamesResolveTheirProjectWithoutInventingAFileId),
			(nameof(source.UnrelatedNumberedArchiveNamesRemainUnmatched), source.UnrelatedNumberedArchiveNamesRemainUnmatched),
			(nameof(source.ModioArchiveNamesNeverImplyNexusProjects), source.ModioArchiveNamesNeverImplyNexusProjects),
			(nameof(source.MatchingNexusCreatorAndUploaderUseOneLinkedCreatorLabel), source.MatchingNexusCreatorAndUploaderUseOneLinkedCreatorLabel),
			(nameof(source.ManualNexusAssociationWinsOverCachedModioMetadata), source.ManualNexusAssociationWinsOverCachedModioMetadata),
			(nameof(source.CachedModioMetadataWinsOverAutomaticNexusMetadata), source.CachedModioMetadataWinsOverAutomaticNexusMetadata),
			(nameof(source.ReviewedNexusDatabaseMatchWinsOverNativeModioMetadata), source.ReviewedNexusDatabaseMatchWinsOverNativeModioMetadata),
			(nameof(source.ManualModioUnlinkSurvivesCacheRoundTrip), source.ManualModioUnlinkSurvivesCacheRoundTrip),
			(nameof(source.NexusArchiveImportWinsOverNativeModioMetadata), source.NexusArchiveImportWinsOverNativeModioMetadata),
			(nameof(source.NexusArchiveCacheBlocksNativeModioDiscoveryAfterRestart), source.NexusArchiveCacheBlocksNativeModioDiscoveryAfterRestart),
			(nameof(source.ReduxBundleNexusLinkOverridesOnlyWhenExplicitlyApplied), source.ReduxBundleNexusLinkOverridesOnlyWhenExplicitlyApplied),
			(nameof(source.PortableSourceLinkUsesTheDisplayedProviderWithoutPrivateUrlData), source.PortableSourceLinkUsesTheDisplayedProviderWithoutPrivateUrlData),
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
			(nameof(health.LoadOrderGuidanceAppliesOnlyToNormalActiveEntries), health.LoadOrderGuidanceAppliesOnlyToNormalActiveEntries),
			(nameof(health.InvalidUuidIsReportedAsAReadOnlyHealthError), health.InvalidUuidIsReportedAsAReadOnlyHealthError),
			(nameof(health.SelfDependencyIsReportedWithoutDuplicateMissingDependencyNoise), health.SelfDependencyIsReportedWithoutDuplicateMissingDependencyNoise),
			(nameof(health.DependencyCyclesAreReportedOnlyByOptInGuidance), health.DependencyCyclesAreReportedOnlyByOptInGuidance),
			(nameof(health.InvalidCreatorManifestIsReportedWithoutApplyingItsClaims), health.InvalidCreatorManifestIsReportedWithoutApplyingItsClaims),
			(nameof(health.DuplicateUuidsAreReportedWithoutRemovingEitherPackage), health.DuplicateUuidsAreReportedWithoutRemovingEitherPackage),
			(nameof(health.ActiveDeclaredConflictsAreReportedConservatively), health.ActiveDeclaredConflictsAreReportedConservatively),
			(nameof(health.OlderInstalledDependencyVersionsAreReportedWithoutUpdatingThem), health.OlderInstalledDependencyVersionsAreReportedWithoutUpdatingThem),
			(nameof(health.ScriptExtenderErrorsAndWarningsRemainDistinct), health.ScriptExtenderErrorsAndWarningsRemainDistinct),
			(nameof(health.ScriptExtenderFindingsUseOnlyTheDedicatedRowIndicator), health.ScriptExtenderFindingsUseOnlyTheDedicatedRowIndicator),
			(nameof(health.ForceLoadedVariantsRemainInformationalAndReadOnly), health.ForceLoadedVariantsRemainInformationalAndReadOnly),
			(nameof(health.ModioMetadataDoesNotImplyAHealthWarning), health.ModioMetadataDoesNotImplyAHealthWarning),
			(nameof(health.InactiveMcmExplainsItsInGameLoadOrderWarning), health.InactiveMcmExplainsItsInGameLoadOrderWarning),
			(nameof(advisorKnowledge.LibraryListingAliasesResolveOnlyInstalledModules), advisorKnowledge.LibraryListingAliasesResolveOnlyInstalledModules),
			(nameof(advisorKnowledge.BundledKnowledgeIncludesGroupsAliasesAndSubstitutes), advisorKnowledge.BundledKnowledgeIncludesGroupsAliasesAndSubstitutes),
			(nameof(advisorEvidence.PlacementEvidenceDoesNotClaimCompatibility), advisorEvidence.PlacementEvidenceDoesNotClaimCompatibility),
			(nameof(dialogLayout.ModlistActionsRemainReachableWithLargeTextAndWarnings), dialogLayout.ModlistActionsRemainReachableWithLargeTextAndWarnings),
			(nameof(whatsNew.SuppressionIsSavedAndOlderSettingsKeepNotesEnabled), whatsNew.SuppressionIsSavedAndOlderSettingsKeepNotesEnabled),
			(nameof(extenderExport.AchievementSettingRoundTripsInBothExportModes), extenderExport.AchievementSettingRoundTripsInBothExportModes),
			(nameof(extenderExport.ExportPreferenceSurvivesReduxRestartAndGameConfigReload), extenderExport.ExportPreferenceSurvivesReduxRestartAndGameConfigReload),
			(nameof(dialogLayout.PreferencesAndReleaseNotesUseReadableCompactLayouts), dialogLayout.PreferencesAndReleaseNotesUseReadableCompactLayouts),
			(nameof(dialogLayout.DownloadToolbarActionsRemainVisibleWithLargeText), dialogLayout.DownloadToolbarActionsRemainVisibleWithLargeText),
			(nameof(dialogLayout.ReviewDialogsKeepActionsReachableWithLargeText), dialogLayout.ReviewDialogsKeepActionsReachableWithLargeText),
			(nameof(whatsNew.ReleaseMetadataIsHiddenAndCustomBackgroundsHaveReadableText), whatsNew.ReleaseMetadataIsHiddenAndCustomBackgroundsHaveReadableText),
			(nameof(advisorKnowledge.ExactDependencyAliasesAndSubstitutesResolveInstalledMods), advisorKnowledge.ExactDependencyAliasesAndSubstitutesResolveInstalledMods),
			(nameof(advisorKnowledge.OfflineDependencyFactsExtendTheExistingAdvisor), advisorKnowledge.OfflineDependencyFactsExtendTheExistingAdvisor),
			(nameof(advisorKnowledge.AuthorProvidedPlacementExtendsTheExistingAdvisor), advisorKnowledge.AuthorProvidedPlacementExtendsTheExistingAdvisor),
			(nameof(advisorKnowledge.ExceptionalLateLoadingDependenciesDoNotCreateFalseAdvice), advisorKnowledge.ExceptionalLateLoadingDependenciesDoNotCreateFalseAdvice),
			(nameof(modules.DefaultsKeepModDiagnosticsOnAndGuidanceOptIn), modules.DefaultsKeepModDiagnosticsOnAndGuidanceOptIn),
            (nameof(modules.StarterSeparatorsPreserveExistingSectionsAndSkipMatchingNames), modules.StarterSeparatorsPreserveExistingSectionsAndSkipMatchingNames),
			(nameof(modules.FirstRunOnboardingStartsWithIntegrationsAndGuidanceOff), modules.FirstRunOnboardingStartsWithIntegrationsAndGuidanceOff),
			(nameof(modules.ReturningUsersKeepTheirOptionalFeatureChoices), modules.ReturningUsersKeepTheirOptionalFeatureChoices),
			(nameof(modules.CategoryInteractionSettingSynchronizesLegacyPresentationFlags), modules.CategoryInteractionSettingSynchronizesLegacyPresentationFlags),
			(nameof(modules.IconsOnlySettingSynchronizesLegacySourceFlag), modules.IconsOnlySettingSynchronizesLegacySourceFlag),
			(nameof(modules.CustomThemeClonePreservesUnifiedPresentationSettings), modules.CustomThemeClonePreservesUnifiedPresentationSettings),
			(nameof(modules.CustomThemePreviewRegeneratesEverySemanticPillGradient), modules.CustomThemePreviewRegeneratesEverySemanticPillGradient),
			(nameof(modules.CustomThemePreviewReusesUnchangedSemanticBrushes), modules.CustomThemePreviewReusesUnchangedSemanticBrushes),
			(nameof(modules.CustomThemePreviewRefreshesEveryOpenEditorResourceScope), modules.CustomThemePreviewRefreshesEveryOpenEditorResourceScope),
			(nameof(modules.RepeatedThemeApplicationReusesTheLoadedColorScheme), modules.RepeatedThemeApplicationReusesTheLoadedColorScheme),
			(nameof(modules.CustomThemeBackgroundEditsPreserveUntouchedBaseRoles), modules.CustomThemeBackgroundEditsPreserveUntouchedBaseRoles),
			(nameof(modules.GeneratedActionGradientsFollowThemeDefaultsAndCustomChoice), modules.GeneratedActionGradientsFollowThemeDefaultsAndCustomChoice),
			(nameof(modules.ParchmentBaseResourcesDefaultToSolidActions), modules.ParchmentBaseResourcesDefaultToSolidActions),
			(nameof(modules.LocalOnlyModeChangesOnlySourceIntegrations), modules.LocalOnlyModeChangesOnlySourceIntegrations),
			(nameof(modules.LoadOrderGuidanceFollowsItsOwnPreference), modules.LoadOrderGuidanceFollowsItsOwnPreference),
			(nameof(modules.DisposedModuleStateStopsTrackingSettings), modules.DisposedModuleStateStopsTrackingSettings),
			(nameof(modules.DisabledNexusProviderCannotInitializeItsClient), modules.DisabledNexusProviderCannotInitializeItsClient),
			(nameof(bundle.BundleRoundTripPreservesOrderAndReduxPresentation), bundle.BundleRoundTripPreservesOrderAndReduxPresentation),
			(nameof(bundle.SourceLinksCannotReferenceModsOutsideTheOrder), bundle.SourceLinksCannotReferenceModsOutsideTheOrder),
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
			(nameof(contribution.ContributionReportsPreserveKnownNexusIdentifiers), contribution.ContributionReportsPreserveKnownNexusIdentifiers),
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
			(nameof(archivePreflight.ReviewedNativeArchiveUsesTheGuardedInstallerLayout), archivePreflight.ReviewedNativeArchiveUsesTheGuardedInstallerLayout),
			(nameof(archivePreflight.UnreviewedDllArchiveIsReportedWithoutGuessingADestination), archivePreflight.UnreviewedDllArchiveIsReportedWithoutGuessingADestination),
			(nameof(archivePreflight.SaveArchiveUsesSaveManagerValidationAndMetadata), archivePreflight.SaveArchiveUsesSaveManagerValidationAndMetadata),
			(nameof(archivePreflight.LooseSaveIsInspectedWithoutUsingAnArchiveReader), archivePreflight.LooseSaveIsInspectedWithoutUsingAnArchiveReader),
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
			(nameof(interactionBehavior.NewBlankOrderContainsNoActivatedMods), interactionBehavior.NewBlankOrderContainsNoActivatedMods),
			(nameof(interactionBehavior.WorkingChangesStayDetachedUntilExplicitlySaved), interactionBehavior.WorkingChangesStayDetachedUntilExplicitlySaved),
			(nameof(interactionBehavior.SavedOrdersKeepIndependentActiveSeparators), interactionBehavior.SavedOrdersKeepIndependentActiveSeparators),
			(nameof(interactionBehavior.SavedCurrentStateRestoresIntoTheSingleCurrentEntry), interactionBehavior.SavedCurrentStateRestoresIntoTheSingleCurrentEntry),
			(nameof(interactionBehavior.DuplicateWandChoiceNormalizesToTheSingleVisibleIcon), interactionBehavior.DuplicateWandChoiceNormalizesToTheSingleVisibleIcon),
			(nameof(interactionBehavior.BuiltInIconPickerHasAUniqueExpandedCatalog), interactionBehavior.BuiltInIconPickerHasAUniqueExpandedCatalog),
			(nameof(interactionBehavior.AsyncProviderMetadataSignalsAutomaticCategoryRefresh), interactionBehavior.AsyncProviderMetadataSignalsAutomaticCategoryRefresh),
			(nameof(interactionBehavior.MenuSemanticColorDistinguishesSavingFromSaveNavigation), interactionBehavior.MenuSemanticColorDistinguishesSavingFromSaveNavigation),
			(nameof(interactionBehavior.CommandTooltipUsesLiveShortcutAndSharedDescription), interactionBehavior.CommandTooltipUsesLiveShortcutAndSharedDescription),
			(nameof(interactionBehavior.CommandPaletteItemTemplateResolvesCoreBindingsAtRuntime), interactionBehavior.CommandPaletteItemTemplateResolvesCoreBindingsAtRuntime),
			(nameof(interactionBehavior.ReduxDialogTemplatesResolveCoreBindingsAtRuntime), interactionBehavior.ReduxDialogTemplatesResolveCoreBindingsAtRuntime),
			(nameof(interactionBehavior.KeyboardShortcutGroupsAvoidVirtualizedContainerRecycling), interactionBehavior.KeyboardShortcutGroupsAvoidVirtualizedContainerRecycling),
			(nameof(interactionBehavior.ThemeCyclingIncludesValidCustomThemesInSavedOrder), interactionBehavior.ThemeCyclingIncludesValidCustomThemesInSavedOrder),
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
			(nameof(visualDividerDrag.EstablishedSectionsFollowTheirMembersAfterMultiModChanges), visualDividerDrag.EstablishedSectionsFollowTheirMembersAfterMultiModChanges),
			(nameof(inactiveOrder.SavedInactiveOrderSurvivesRestartAndDiscoveryChanges), inactiveOrder.SavedInactiveOrderSurvivesRestartAndDiscoveryChanges),
			(nameof(inactiveOrder.InactiveBlockMoveDoesNotChangeActiveOrder), inactiveOrder.InactiveBlockMoveDoesNotChangeActiveOrder),
			(nameof(inactiveOrder.InactiveControlsLoadAndColumnSortKeepsUnderlyingOrder), inactiveOrder.InactiveControlsLoadAndColumnSortKeepsUnderlyingOrder),
			(nameof(inactiveOrder.AdvisorIgnoresInactiveOrganization), inactiveOrder.AdvisorIgnoresInactiveOrganization),
			(nameof(visualDividerDrag.InactivePaneAcceptsSeparatorsAndKeepsClosedBlocksInTheirPane), visualDividerDrag.InactivePaneAcceptsSeparatorsAndKeepsClosedBlocksInTheirPane),
			(nameof(visualDividerDrag.ExpandedDividerDragContainsOnlyItsMarker), visualDividerDrag.ExpandedDividerDragContainsOnlyItsMarker),
			(nameof(visualDividerDrag.CollapsedDividerDragStartsWithLightweightMarker), visualDividerDrag.CollapsedDividerDragStartsWithLightweightMarker),
			(nameof(visualDividerDrag.CollapsedSeparatorPayloadCarriesOnlyItsSealedMembers), visualDividerDrag.CollapsedSeparatorPayloadCarriesOnlyItsSealedMembers),
			(nameof(visualDividerDrag.CollapsedSeparatorMovesAroundAnotherClosedBlockWithoutAbsorption), visualDividerDrag.CollapsedSeparatorMovesAroundAnotherClosedBlockWithoutAbsorption),
			(nameof(visualDividerDrag.ExpandedSeparatorMoveCarriesItsRecordedMembersWithoutAbsorption), visualDividerDrag.ExpandedSeparatorMoveCarriesItsRecordedMembersWithoutAbsorption),
			(nameof(visualDividerDrag.RecreatedExpandedSeparatorResolvesToCanonicalMarkerOnly), visualDividerDrag.RecreatedExpandedSeparatorResolvesToCanonicalMarkerOnly),
			(nameof(visualDividerDrag.DropAfterCollapsedSeparatorSkipsItsHiddenSection), visualDividerDrag.DropAfterCollapsedSeparatorSkipsItsHiddenSection),
			(nameof(visualDividerDrag.CollapsedSeparatorDoesNotAdoptAModDroppedBelowItsClosedBlock), visualDividerDrag.CollapsedSeparatorDoesNotAdoptAModDroppedBelowItsClosedBlock),
			(nameof(visualDividerDrag.MovingASeparatorAboveAClosedSectionCannotChangeItsContents), visualDividerDrag.MovingASeparatorAboveAClosedSectionCannotChangeItsContents),
			(nameof(visualDividerDrag.VisibleDropSlotMapsPastOmittedCollapsedMembers), visualDividerDrag.VisibleDropSlotMapsPastOmittedCollapsedMembers),
			(nameof(visualDividerDrag.VisibleDropSlotMatchesRecreatedDividerByIdentity), visualDividerDrag.VisibleDropSlotMatchesRecreatedDividerByIdentity),
			(nameof(visualDividerDrag.ProgressiveExpansionInsertsBeforeUnownedDestinationSuffix), visualDividerDrag.ProgressiveExpansionInsertsBeforeUnownedDestinationSuffix),
			(nameof(visualDividerDrag.ExpansionRestoresClosedMembersBeforeNewlyAdoptedRows), visualDividerDrag.ExpansionRestoresClosedMembersBeforeNewlyAdoptedRows),
			(nameof(visualDividerDrag.CollapseAllChangesOnlyTheRequestedPaneAndOnlyOnce), visualDividerDrag.CollapseAllChangesOnlyTheRequestedPaneAndOnlyOnce),
			(nameof(visualDividerDrag.BulkSeparatorToggleClosesMixedPanesBeforeReopeningThem), visualDividerDrag.BulkSeparatorToggleClosesMixedPanesBeforeReopeningThem),
			(nameof(visualDividerDrag.LegacyPositionsMigrateToDurableSectionMembership), visualDividerDrag.LegacyPositionsMigrateToDurableSectionMembership),
			(nameof(visualDividerDrag.LegacyMembershipWaitsForCompletedListLoading), visualDividerDrag.LegacyMembershipWaitsForCompletedListLoading),
			(nameof(visualDividerDrag.VisualSequencePreservesAuthoritativeModOrder), visualDividerDrag.VisualSequencePreservesAuthoritativeModOrder),
			(nameof(visualDividerDrag.DuplicateOwnershipKeepsFirstDividerAndMissingIds), visualDividerDrag.DuplicateOwnershipKeepsFirstDividerAndMissingIds),
			(nameof(visualDividerDrag.CollapsedVisibilityUsesExplicitMembershipOnly), visualDividerDrag.CollapsedVisibilityUsesExplicitMembershipOnly),
			(nameof(visualDividerDrag.CollapsedVisibilityStopsAtTheNextSeparator), visualDividerDrag.CollapsedVisibilityStopsAtTheNextSeparator),
			(nameof(visualModSelection.SelectAllIncludesOnlyVisibleModRows), visualModSelection.SelectAllIncludesOnlyVisibleModRows),
			(nameof(visualModSelection.TextFilteringUsesTheCleanModOnlyProjection), visualModSelection.TextFilteringUsesTheCleanModOnlyProjection),
			(nameof(visualModSelection.FilterProjectionOmitsCollapsedRowsFromTheItemsSource), visualModSelection.FilterProjectionOmitsCollapsedRowsFromTheItemsSource),
			(nameof(settingsMaintenance.BuiltInTypographyChoicesKeepTheFocusedReduxOrder), settingsMaintenance.BuiltInTypographyChoicesKeepTheFocusedReduxOrder),
			(nameof(settingsMaintenance.ParchmentUsesSegoeByDefaultAndKeepsExplicitOverrides), settingsMaintenance.ParchmentUsesSegoeByDefaultAndKeepsExplicitOverrides),
			(nameof(settingsMaintenance.BuiltInThemeCyclingChangesOnlyInheritedTypography), settingsMaintenance.BuiltInThemeCyclingChangesOnlyInheritedTypography),
			(nameof(settingsMaintenance.SaveGameCampaignCollapseStateRoundTripsWithoutDuplicates), settingsMaintenance.SaveGameCampaignCollapseStateRoundTripsWithoutDuplicates),
			(nameof(settingsMaintenance.RestoringAutomaticCategoriesClearsCurrentAndLegacyAssignmentsOnly), settingsMaintenance.RestoringAutomaticCategoriesClearsCurrentAndLegacyAssignmentsOnly),
			(nameof(settingsMaintenance.RestoringAutomaticCategoriesMakesTheClassifierAuthoritativeAgain), settingsMaintenance.RestoringAutomaticCategoriesMakesTheClassifierAuthoritativeAgain),
			(nameof(settingsMaintenance.ElevationWarningRequiresAnElevatedUnsuppressedProcessAndSchedulesOnce), settingsMaintenance.ElevationWarningRequiresAnElevatedUnsuppressedProcessAndSchedulesOnce),
			(nameof(settingsMaintenance.FailedElevationWarningSuppressionRestoresThePreviousPreference), settingsMaintenance.FailedElevationWarningSuppressionRestoresThePreviousPreference),
			(nameof(settingsMaintenance.ElevationWarningSuppressionIsRestoredIntoLiveSettings), settingsMaintenance.ElevationWarningSuppressionIsRestoredIntoLiveSettings),
			(nameof(settingsMaintenance.CurrentWindowsProcessElevationCanBeReadFromItsToken), settingsMaintenance.CurrentWindowsProcessElevationCanBeReadFromItsToken),
			(nameof(smoothLogicalScroll.PartialWheelDeltasAccumulateWithoutPrematureScrolling), smoothLogicalScroll.PartialWheelDeltasAccumulateWithoutPrematureScrolling),
			(nameof(smoothLogicalScroll.LargeWheelBurstsStayWithinTheAnimationSafetyCap), smoothLogicalScroll.LargeWheelBurstsStayWithinTheAnimationSafetyCap),
			(nameof(smoothLogicalScroll.SmoothScrollingIsStandardUnlessMotionOrInteractionSuppressesIt), smoothLogicalScroll.SmoothScrollingIsStandardUnlessMotionOrInteractionSuppressesIt),
			(nameof(smoothLogicalScroll.MixedHeightRowsProduceDirectionCorrectCompensation), smoothLogicalScroll.MixedHeightRowsProduceDirectionCorrectCompensation),
			(nameof(smoothLogicalScroll.MissingCachedRowsUseAStableFallbackWithoutChangingDirection), smoothLogicalScroll.MissingCachedRowsUseAStableFallbackWithoutChangingDirection),
			(nameof(smoothLogicalScroll.ScrollRangeIsKnownBeforeDeferredLayoutPublishesTheNewOffset), smoothLogicalScroll.ScrollRangeIsKnownBeforeDeferredLayoutPublishesTheNewOffset),
			(nameof(startupNotifications.StartupNotificationsWaitForReadinessAndDrainInOrder), startupNotifications.StartupNotificationsWaitForReadinessAndDrainInOrder),
			(nameof(startupNotifications.RepeatedStartupNotificationUsesLatestDataExactlyOnce), startupNotifications.RepeatedStartupNotificationUsesLatestDataExactlyOnce),
			(nameof(startupNotifications.StaleStartupNotificationCanBeCancelledBeforeReadiness), startupNotifications.StaleStartupNotificationCanBeCancelledBeforeReadiness),
			(nameof(startupNotifications.NotificationsQueuedDuringDrainRemainSequential), startupNotifications.NotificationsQueuedDuringDrainRemainSequential),
			(nameof(commandPaletteSearch.AliasesAndWordOrderMakeActionsDiscoverable), commandPaletteSearch.AliasesAndWordOrderMakeActionsDiscoverable),
			(nameof(commandPaletteSearch.MinimumQueryLengthStillProtectsLargeDynamicLists), commandPaletteSearch.MinimumQueryLengthStillProtectsLargeDynamicLists),
			(nameof(fileSafety.FailedStagedWritePreservesTheExistingDestination), fileSafety.FailedStagedWritePreservesTheExistingDestination),
			(nameof(fileSafety.AtomicCopyReplacesTheDestinationAndKeepsItsBackup), fileSafety.AtomicCopyReplacesTheDestinationAndKeepsItsBackup),
			(nameof(fileSafety.AsyncCopyReplacesTheDestinationAndKeepsItsBackup), fileSafety.AsyncCopyReplacesTheDestinationAndKeepsItsBackup),
			(nameof(fileSafety.ConcurrentWritesNeverExposePartialContent), fileSafety.ConcurrentWritesNeverExposePartialContent),
			(nameof(fileSafety.CancelledAsyncCopyPreservesTheExistingDestination), fileSafety.CancelledAsyncCopyPreservesTheExistingDestination),
			(nameof(fileSafety.GameLoadOrderChangeCanBeUndoneAndRedone), fileSafety.GameLoadOrderChangeCanBeUndoneAndRedone),
			(nameof(fileSafety.GameLoadOrderUndoRefusesToOverwriteANewerExternalChange), fileSafety.GameLoadOrderUndoRefusesToOverwriteANewerExternalChange),
			(nameof(fileSafety.FirstGameLoadOrderExportCanUndoBackToNoFile), fileSafety.FirstGameLoadOrderExportCanUndoBackToNoFile),
			(nameof(fileSafety.ProviderCredentialsAreEncryptedAndExcludedFromSettingsJson), fileSafety.ProviderCredentialsAreEncryptedAndExcludedFromSettingsJson),
			(nameof(loadOrderWorkflow.SaveSwitchRenameAndRestartPreservesEachOrder), loadOrderWorkflow.SaveSwitchRenameAndRestartPreservesEachOrder),
			(nameof(loadOrderWorkflow.RenameRequiresConfirmationBeforeReplacingAnotherSavedOrder), loadOrderWorkflow.RenameRequiresConfirmationBeforeReplacingAnotherSavedOrder),
			(nameof(undoRedoHistory.UndoAndRedoRestoreTheExpectedState), undoRedoHistory.UndoAndRedoRestoreTheExpectedState),
			(nameof(undoRedoHistory.ANewEditClearsTheRedoBranch), undoRedoHistory.ANewEditClearsTheRedoBranch),
			(nameof(undoRedoHistory.HistoryDropsItsOldestEntryAtCapacity), undoRedoHistory.HistoryDropsItsOldestEntryAtCapacity),
			(nameof(loadOrderOrganizer.PreserveSeparatorsSortsInsideButNeverAcrossUserBoundaries), loadOrderOrganizer.PreserveSeparatorsSortsInsideButNeverAcrossUserBoundaries),
			(nameof(loadOrderOrganizer.PreserveSeparatorsReportsRelationshipsItCannotSafelyApply), loadOrderOrganizer.PreserveSeparatorsReportsRelationshipsItCannotSafelyApply),
			(nameof(loadOrderOrganizer.PreserveSeparatorsAcceptsRelationshipsAlreadySatisfiedAcrossSeparators), loadOrderOrganizer.PreserveSeparatorsAcceptsRelationshipsAlreadySatisfiedAcrossSeparators),
			(nameof(loadOrderOrganizer.SuggestedSeparatorsUseOnlyNonemptyOfflineGroups), loadOrderOrganizer.SuggestedSeparatorsUseOnlyNonemptyOfflineGroups),
			(nameof(loadOrderOrganizer.RemoveSeparatorsGloballySortsWithoutReturningMarkers), loadOrderOrganizer.RemoveSeparatorsGloballySortsWithoutReturningMarkers),
			(nameof(loadOrderOrganizer.UnknownModsRetainTheirRelativeOrder), loadOrderOrganizer.UnknownModsRetainTheirRelativeOrder),
			(nameof(loadOrderOrganizer.PreserveSeparatorsDoesNotAdoptAVisibleRowBelowAClosedSeparator), loadOrderOrganizer.PreserveSeparatorsDoesNotAdoptAVisibleRowBelowAClosedSeparator),
			(nameof(loadOrderOrganizer.PreserveSeparatorsReportsOnlyMarkersThatActuallyMove), loadOrderOrganizer.PreserveSeparatorsReportsOnlyMarkersThatActuallyMove),
			(nameof(loadOrderOrganizer.IgnoringOneRelationshipDoesNotSuppressOtherAdvisorKnowledge), loadOrderOrganizer.IgnoringOneRelationshipDoesNotSuppressOtherAdvisorKnowledge),
			(nameof(dialogLayout.UpdateAndMessageActionsRemainReachableWithLongText), dialogLayout.UpdateAndMessageActionsRemainReachableWithLongText),
			(nameof(releaseFlow.InstanceGuardExcludesAnotherThreadAndReleasesItsLease), releaseFlow.InstanceGuardExcludesAnotherThreadAndReleasesItsLease),
			(nameof(releaseFlow.PakCountIsMetadataWithoutRepeatedPlacementInstructions), releaseFlow.PakCountIsMetadataWithoutRepeatedPlacementInstructions),
			(nameof(batchInstallUi.ToolbarPrioritizesFailuresAndClearsWhenPackagesAreInstalled), batchInstallUi.ToolbarPrioritizesFailuresAndClearsWhenPackagesAreInstalled),
			(nameof(batchInstallUi.ProgressCannotCloseDuringWorkAndReleasesAfterFailure), batchInstallUi.ProgressCannotCloseDuringWorkAndReleasesAfterFailure),
			(nameof(downloadNotification.NotificationsReuseTheirWindowWithoutTakingForeground), downloadNotification.NotificationsReuseTheirWindowWithoutTakingForeground),
			(nameof(processToken.ElevationMatchesExplicitProcessHandleEvenDuringImpersonation), processToken.ElevationMatchesExplicitProcessHandleEvenDuringImpersonation),
			(nameof(dismissal.ExitKeepsContentTransparentUntilDismissed), dismissal.ExitKeepsContentTransparentUntilDismissed),
			(nameof(dismissal.RepeatedCloseWaitsForOneDismissalEvenWhenMotionChanges), dismissal.RepeatedCloseWaitsForOneDismissalEvenWhenMotionChanges),
			(nameof(dismissal.CanceledCloseDoesNotStartExitAnimation), dismissal.CanceledCloseDoesNotStartExitAnimation),
			(nameof(placement.SavedBoundsRemainVisibleAcrossMonitorChanges), placement.SavedBoundsRemainVisibleAcrossMonitorChanges),
			(nameof(saveGames.SaveExportRoundTripPreservesFilesAndCancellationPreservesBackup), saveGames.SaveExportRoundTripPreservesFilesAndCancellationPreservesBackup),
			(nameof(saveGames.SaveDetailsReadEmbeddedNameAndVersionAndTolerateInvalidMetadata), saveGames.SaveDetailsReadEmbeddedNameAndVersionAndTolerateInvalidMetadata),
			(nameof(saveGames.CorruptSaveMetadataDoesNotAbortDiscoveryOrChangeFiles), saveGames.CorruptSaveMetadataDoesNotAbortDiscoveryOrChangeFiles),
			(nameof(saveGames.LooseArchiveSavesKeepOnlyTheirMatchingFiles), saveGames.LooseArchiveSavesKeepOnlyTheirMatchingFiles),
			(nameof(saveGames.RecognizesEveryAdvertisedSaveArchiveFormat), saveGames.RecognizesEveryAdvertisedSaveArchiveFormat),
			(nameof(saveGames.ClassifiesSaveDifficultyFromAuthoritativeRulesetValues), saveGames.ClassifiesSaveDifficultyFromAuthoritativeRulesetValues),
			(nameof(saveGames.DiscoversSaveMetadataAndMatchingThumbnail), saveGames.DiscoversSaveMetadataAndMatchingThumbnail),
			(nameof(saveGames.SaveThumbnailPreviewDoesNotKeepItsFileOrFolderLocked), saveGames.SaveThumbnailPreviewDoesNotKeepItsFileOrFolderLocked),
			(nameof(saveGames.InstallsNestedZipAsOneSaveFolder), saveGames.InstallsNestedZipAsOneSaveFolder),
			(nameof(saveGames.RejectsUnsafeArchivePathsBeforeImport), saveGames.RejectsUnsafeArchivePathsBeforeImport),
			(nameof(saveGames.ExistingSaveIsPreservedUntilReplacementIsRequested), saveGames.ExistingSaveIsPreservedUntilReplacementIsRequested),
			(nameof(nativeMods.AtomicReplacementRejectsSourceChangedSinceItsReviewedHash), nativeMods.AtomicReplacementRejectsSourceChangedSinceItsReviewedHash),
			(nameof(nativeMods.VanillaBinkIsNotAnExternalNativeLoader), nativeMods.VanillaBinkIsNotAnExternalNativeLoader),
			(nameof(nativeMods.CatalogContainsReviewedNativeProjectsAndGuardedWorkflows), nativeMods.CatalogContainsReviewedNativeProjectsAndGuardedWorkflows),
			(nameof(nativeMods.ReviewedCameraFingerprintsDistinguishLegacyAndGuiProjects), nativeMods.ReviewedCameraFingerprintsDistinguishLegacyAndGuiProjects),
			(nameof(nativeMods.ReviewedCatalogFingerprintsCoverEveryKnownDllProject), nativeMods.ReviewedCatalogFingerprintsCoverEveryKnownDllProject),
			(nameof(nativeMods.UnknownSharedCameraBinaryRemainsAnUnverifiedVariant), nativeMods.UnknownSharedCameraBinaryRemainsAnUnverifiedVariant),
			(nameof(nativeMods.ArchiveRecognitionUsesReviewedLayoutAndCorroboratesOverlappingProjects), nativeMods.ArchiveRecognitionUsesReviewedLayoutAndCorroboratesOverlappingProjects),
			(nameof(nativeMods.ArchiveRecognitionRoutesScriptExtenderToGuardedGameDirectoryWorkflow), nativeMods.ArchiveRecognitionRoutesScriptExtenderToGuardedGameDirectoryWorkflow),
			(nameof(nativeMods.ScriptExtenderUsesTheSameStagedCommitAndOwnershipRecordAsOtherGameDirectoryMods), nativeMods.ScriptExtenderUsesTheSameStagedCommitAndOwnershipRecordAsOtherGameDirectoryMods),
			(nameof(nativeMods.UnreviewedDllArchiveIsNeverTreatedAsAnOrdinaryModArchive), nativeMods.UnreviewedDllArchiveIsNeverTreatedAsAnOrdinaryModArchive),
			(nameof(nativeMods.EveryReviewedCatalogLayoutHasARecognizableFixture), nativeMods.EveryReviewedCatalogLayoutHasARecognizableFixture),
			(nameof(nativeMods.ZipValidationRejectsOtherFormatsTraversalAndUnexpectedFilesWithoutChangingGameFiles), nativeMods.ZipValidationRejectsOtherFormatsTraversalAndUnexpectedFilesWithoutChangingGameFiles),
			(nameof(nativeMods.OversizedZipEntriesAreRejectedBeforeTheyCanBeStaged), nativeMods.OversizedZipEntriesAreRejectedBeforeTheyCanBeStaged),
			(nameof(nativeMods.NativePluginsRequireAKnownSupportedGameVersionAndLoader), nativeMods.NativePluginsRequireAKnownSupportedGameVersionAndLoader),
			(nameof(nativeMods.InspectArchiveAllowsDeferredLoaderPrerequisiteButStillChecksGameVersion), nativeMods.InspectArchiveAllowsDeferredLoaderPrerequisiteButStillChecksGameVersion),
			(nameof(nativeMods.LoaderInstallStagesWithoutChangingGameFilesThenCommitsAsReduxVerified), nativeMods.LoaderInstallStagesWithoutChangingGameFilesThenCommitsAsReduxVerified),
			(nameof(nativeMods.ReduxInstalledLoaderKeepsAProtectedCopyOfTheUsersOriginalDll), nativeMods.ReduxInstalledLoaderKeepsAProtectedCopyOfTheUsersOriginalDll),
			(nameof(nativeMods.ReplacerInstallNeverBacksUpAnUnreviewedOrModdedDll), nativeMods.ReplacerInstallNeverBacksUpAnUnreviewedOrModdedDll),
			(nameof(nativeMods.ExternalLoaderPairIsPresentButUnverifiedAndUnmanagedOriginalConflicts), nativeMods.ExternalLoaderPairIsPresentButUnverifiedAndUnmanagedOriginalConflicts),
			(nameof(nativeMods.ChangedReduxOwnedLoaderBlocksDependentPluginInstallation), nativeMods.ChangedReduxOwnedLoaderBlocksDependentPluginInstallation),
			(nameof(nativeMods.ManagerStatusDistinguishesManagedChangedAndExternalFiles), nativeMods.ManagerStatusDistinguishesManagedChangedAndExternalFiles),
			(nameof(nativeMods.ReviewedAddOnlyModCanRepairItsChangedOrMissingOwnedDll), nativeMods.ReviewedAddOnlyModCanRepairItsChangedOrMissingOwnedDll),
			(nameof(nativeMods.ManagerSurfacesUnknownNativeDllsWithoutClaimingOwnership), nativeMods.ManagerSurfacesUnknownNativeDllsWithoutClaimingOwnership),
			(nameof(nativeMods.UnknownExternalDllCannotBeAdoptedOrCreateOwnershipState), nativeMods.UnknownExternalDllCannotBeAdoptedOrCreateOwnershipState),
			(nameof(nativeMods.LeftoverExternalConfigurationIsNotReportedAsAnInstalledDllMod), nativeMods.LeftoverExternalConfigurationIsNotReportedAsAnInstalledDllMod),
			(nameof(nativeMods.CommitRejectsArchiveAndDestinationChangesAfterReview), nativeMods.CommitRejectsArchiveAndDestinationChangesAfterReview),
			(nameof(nativeMods.PluginCommitRejectsRemovedLoaderAfterReview), nativeMods.PluginCommitRejectsRemovedLoaderAfterReview),
			(nameof(nativeMods.PluginCommitRejectsChangedLoaderAfterReview), nativeMods.PluginCommitRejectsChangedLoaderAfterReview),
			(nameof(nativeMods.CommitRejectsStaleOwnershipManifestFromAnotherTransaction), nativeMods.CommitRejectsStaleOwnershipManifestFromAnotherTransaction),
			(nameof(nativeMods.CommitFailureRollsBackEveryWrittenTarget), nativeMods.CommitFailureRollsBackEveryWrittenTarget),
			(nameof(nativeMods.PluginInstallPreservesExistingTomlConfiguration), nativeMods.PluginInstallPreservesExistingTomlConfiguration),
			(nameof(nativeMods.UserConfigurationEditsNeverBlockNativePluginRestore), nativeMods.UserConfigurationEditsNeverBlockNativePluginRestore),
			(nameof(nativeMods.RelatedProjectUpdateKeepsOneReduxOwnershipRecord), nativeMods.RelatedProjectUpdateKeepsOneReduxOwnershipRecord),
			(nameof(nativeMods.MixedPackageStagesOnlyNativeFilesAndReportsItsCompanionPak), nativeMods.MixedPackageStagesOnlyNativeFilesAndReportsItsCompanionPak),
			(nameof(nativeMods.TrueThirdPersonCameraRefusesLegacyCameraFiles), nativeMods.TrueThirdPersonCameraRefusesLegacyCameraFiles),
			(nameof(nativeMods.RestoreRefusesActivePluginsThenRestoresOnlyOwnedFiles), nativeMods.RestoreRefusesActivePluginsThenRestoresOnlyOwnedFiles),
			(nameof(nxmLinks.ParsesAuthenticatedBg3Link), nxmLinks.ParsesAuthenticatedBg3Link),
			(nameof(nxmLinks.ParsesPremiumBg3LinkWithoutAuthorizationQuery), nxmLinks.ParsesPremiumBg3LinkWithoutAuthorizationQuery),
			(nameof(nxmLinks.RejectsWrongSchemeOrGame), nxmLinks.RejectsWrongSchemeOrGame),
			(nameof(nxmLinks.RejectsUnsafeAuthorityAndPathForms), nxmLinks.RejectsUnsafeAuthorityAndPathForms),
			(nameof(nxmLinks.RejectsMalformedOrUnexpectedQueryData), nxmLinks.RejectsMalformedOrUnexpectedQueryData),
			(nameof(nxmLinks.RejectsExpiredAuthorization), nxmLinks.RejectsExpiredAuthorization),
			(nameof(nxmLinks.RejectsOversizedInput), nxmLinks.RejectsOversizedInput),
			(nameof(nxmLinks.RedactsAuthorizationValues), nxmLinks.RedactsAuthorizationValues),
			(nameof(nxmResolver.FreeUserResolutionUsesExactFileAndAuthorization), nxmResolver.FreeUserResolutionUsesExactFileAndAuthorization),
			(nameof(nxmResolver.PremiumResolutionDoesNotForwardShortLivedAuthorization), nxmResolver.PremiumResolutionDoesNotForwardShortLivedAuthorization),
			(nameof(nxmResolver.FreeUserRequiresFreshMatchingAuthorization), nxmResolver.FreeUserRequiresFreshMatchingAuthorization),
			(nameof(nxmResolver.ResolutionRejectsWrongFileAndUnsafeDownloadUri), nxmResolver.ResolutionRejectsWrongFileAndUnsafeDownloadUri),
			(nameof(nxmResolver.ThirdPartyFailureDoesNotExposeSignedUrl), nxmResolver.ThirdPartyFailureDoesNotExposeSignedUrl),
			(nameof(nxmActivation.SameUserPipeDeliversValidatedLink), nxmActivation.SameUserPipeDeliversValidatedLink),
			(nameof(nxmActivation.OversizedMessageIsRejectedBeforeConnection), nxmActivation.OversizedMessageIsRejectedBeforeConnection),
			(nameof(nxmActivation.ListenerSurvivesMalformedClientMessage), nxmActivation.ListenerSurvivesMalformedClientMessage),
			(nameof(nxmAssociation.EnableAndDisableRestoresPriorUserHandler), nxmAssociation.EnableAndDisableRestoresPriorUserHandler),
			(nameof(nxmAssociation.DisableRevealsMachineHandlerWhenNoUserHandlerExisted), nxmAssociation.DisableRevealsMachineHandlerWhenNoUserHandlerExisted),
			(nameof(nxmAssociation.RepairUpdatesOnlyOwnedMovedRegistration), nxmAssociation.RepairUpdatesOnlyOwnedMovedRegistration),
			(nameof(nxmAssociation.DisableNeverOverwritesAnInterveningHandler), nxmAssociation.DisableNeverOverwritesAnInterveningHandler),
			(nameof(nxmAssociation.DifferentReduxInstallationCannotRepairOrDisableOwner), nxmAssociation.DifferentReduxInstallationCannotRepairOrDisableOwner),
			(nameof(nxmAssociation.DifferentReduxInstallationCanBeReassociatedByExplicitTakeover), nxmAssociation.DifferentReduxInstallationCanBeReassociatedByExplicitTakeover),
			(nameof(nxmAssociation.ChangedCommandWithStaleMarkerCanOnlyBeReclaimedExplicitly), nxmAssociation.ChangedCommandWithStaleMarkerCanOnlyBeReclaimedExplicitly),
			(nameof(nxmAssociation.RegistrySnapshotPreservesValueKindsAndSubkeys), nxmAssociation.RegistrySnapshotPreservesValueKindsAndSubkeys),
			(nameof(nxmAssociation.ProductionRegistryStoreRoundTripsOnlyDisposableHkcuPaths), nxmAssociation.ProductionRegistryStoreRoundTripsOnlyDisposableHkcuPaths),
			(nameof(nxmAssociation.FailedEnableRestoresPriorHandler), nxmAssociation.FailedEnableRestoresPriorHandler),
			(nameof(nxmAssociation.FailedDisableKeepsOwnedHandlerAndBackup), nxmAssociation.FailedDisableKeepsOwnedHandlerAndBackup),
			(nameof(nxmAssociation.ReduxShapedInterveningCommandIsNotOwned), nxmAssociation.ReduxShapedInterveningCommandIsNotOwned),
			(nameof(nxmAssociation.MissingExecutableMarkerIsAnOwnershipConflict), nxmAssociation.MissingExecutableMarkerIsAnOwnershipConflict),
			(nameof(nxmAssociation.PreviousHandlerCommandIsParsedWithoutShell), nxmAssociation.PreviousHandlerCommandIsParsedWithoutShell),
			(nameof(nxmAssociation.PreviousHandlerRejectsEmbeddedPlaceholderAndReduxRecursion), nxmAssociation.PreviousHandlerRejectsEmbeddedPlaceholderAndReduxRecursion),
			(nameof(nxmManager.RetainedNexusPackageReentersInboxWithPublicSourceIdentity), nxmManager.RetainedNexusPackageReentersInboxWithPublicSourceIdentity),
			(nameof(nxmManager.DuplicateLinkFocusesExistingItem), nxmManager.DuplicateLinkFocusesExistingItem),
			(nameof(nxmManager.ResolvedItemsDownloadWithoutBlockingIngress), nxmManager.ResolvedItemsDownloadWithoutBlockingIngress),
			(nameof(nxmManager.RemovingResolvingItemCancelsItBeforeTransfer), nxmManager.RemovingResolvingItemCancelsItBeforeTransfer),
			(nameof(nxmManager.CancelWinsRaceWithTransferCompletion), nxmManager.CancelWinsRaceWithTransferCompletion),
			(nameof(nxmManager.HttpProgressTotalReplacesMetadataEstimate), nxmManager.HttpProgressTotalReplacesMetadataEstimate),
			(nameof(nxmManager.PauseWaitsForTheOwnedTransferAndRejectsItsLateCompletion), nxmManager.PauseWaitsForTheOwnedTransferAndRejectsItsLateCompletion),
			(nameof(nxmManager.DisablingNetworkWaitsForTransfersAndReenableDoesNotResumeThem), nxmManager.DisablingNetworkWaitsForTransfersAndReenableDoesNotResumeThem),
			(nameof(nxmManager.EnqueueSaveFailureDoesNotPublishTheItem), nxmManager.EnqueueSaveFailureDoesNotPublishTheItem),
			(nameof(nxmManager.ReservedWindowsFilenameUsesAStableSafeName), nxmManager.ReservedWindowsFilenameUsesAStableSafeName),
			(nameof(nxmManager.InitializeRestartsPersistedQueuedDownload), nxmManager.InitializeRestartsPersistedQueuedDownload),
			(nameof(nxmManager.FailedPauseSaveDoesNotPublishPausedState), nxmManager.FailedPauseSaveDoesNotPublishPausedState),
			(nameof(nxmManager.PermanentTransferFailureDoesNotEnterRetryLoop), nxmManager.PermanentTransferFailureDoesNotEnterRetryLoop),
			(nameof(nxmManager.MetadataCompletionOrderDoesNotChangeQueueFifo), nxmManager.MetadataCompletionOrderDoesNotChangeQueueFifo),
			(nameof(nxmManager.ShutdownRejectsLateEnqueue), nxmManager.ShutdownRejectsLateEnqueue),
			(nameof(nxmManager.FreshLinkSaveFailureDoesNotPublishResolvingState), nxmManager.FreshLinkSaveFailureDoesNotPublishResolvingState),
			(nameof(nxmManager.ResolvedMetadataIsDurableBeforeConfirmationCompletes), nxmManager.ResolvedMetadataIsDurableBeforeConfirmationCompletes),
			(nameof(nxmManager.InitializeHydratesLegacyPlaceholderWithoutChangingFreshLinkState), nxmManager.InitializeHydratesLegacyPlaceholderWithoutChangingFreshLinkState),
			(nameof(nxmManager.FailedShutdownCanBeRetriedUntilPausedStateIsDurable), nxmManager.FailedShutdownCanBeRetriedUntilPausedStateIsDurable),
			(nameof(nxmManager.ShutdownAfterCompletedInstallDoesNotWaitForOperationCleanup), nxmManager.ShutdownAfterCompletedInstallDoesNotWaitForOperationCleanup),
			(nameof(nxmManager.ClearingInstalledHistoryKeepsOtherQueueItems), nxmManager.ClearingInstalledHistoryKeepsOtherQueueItems),
			(nameof(nxmManager.QueueStatesHaveHumanReadableLabels), nxmManager.QueueStatesHaveHumanReadableLabels),
			(nameof(nxmManager.InstallFailuresHaveDedicatedHumanReadableState), nxmManager.InstallFailuresHaveDedicatedHumanReadableState),
			(nameof(nxmManager.RetainedPakReinstallAdvertisesPlacementPreservation), nxmManager.RetainedPakReinstallAdvertisesPlacementPreservation),
			(nameof(nxmManager.FreshPakInstallExplainsThatUpdatesKeepTheirPlacement), nxmManager.FreshPakInstallExplainsThatUpdatesKeepTheirPlacement),
			(nameof(nxmManager.DownloadAgainPreservesTheArchiveAndUsesFreshAuthorizationWhenRequired), nxmManager.DownloadAgainPreservesTheArchiveAndUsesFreshAuthorizationWhenRequired),
			(nameof(nxmManager.FailedRedownloadSavePreservesTheOriginalQueueRecord), nxmManager.FailedRedownloadSavePreservesTheOriginalQueueRecord),
			(nameof(nxmManager.DownloadAgainNeverReusesExistingPartialData), nxmManager.DownloadAgainNeverReusesExistingPartialData),
			(nameof(nxmManager.RedownloadAfterMetadataFailureUsesResolvedPakExtension), nxmManager.RedownloadAfterMetadataFailureUsesResolvedPakExtension),
			(nameof(nxmScheduler.DefaultLimitRunsFourAndQueuesTheRest), nxmScheduler.DefaultLimitRunsFourAndQueuesTheRest),
			(nameof(nxmScheduler.RaisingLimitDispatchesQueuedItemsInFifoOrder), nxmScheduler.RaisingLimitDispatchesQueuedItemsInFifoOrder),
			(nameof(nxmScheduler.CancellationRemovesSuspendedQueuedWorkImmediately), nxmScheduler.CancellationRemovesSuspendedQueuedWorkImmediately),
			(nameof(nxmStore.RoundTripPreservesPublicQueueStateWithoutCapabilities), nxmStore.RoundTripPreservesPublicQueueStateWithoutCapabilities),
			(nameof(nxmStore.LocalPackageIdentityAndInspectionSurviveRestart), nxmStore.LocalPackageIdentityAndInspectionSurviveRestart),
			(nameof(nxmStore.InstalledHistorySurvivesWhenItsArchiveWasRemoved), nxmStore.InstalledHistorySurvivesWhenItsArchiveWasRemoved),
			(nameof(nxmStore.DetailedInstallFailureSurvivesRestartAndMissingFileInvalidation), nxmStore.DetailedInstallFailureSurvivesRestartAndMissingFileInvalidation),
			(nameof(nxmStore.ReconcileRequiresFreshLinkForIncompleteFreeDownload), nxmStore.ReconcileRequiresFreshLinkForIncompleteFreeDownload),
			(nameof(nxmStore.ReconcileMarksMissingCompletedFileAsFailed), nxmStore.ReconcileMarksMissingCompletedFileAsFailed),
			(nameof(nxmStore.ReconcileRejectsCompletedFileWithWrongLength), nxmStore.ReconcileRejectsCompletedFileWithWrongLength),
			(nameof(nxmStore.ReconcileRejectsCompletedFileWithChangedContents), nxmStore.ReconcileRejectsCompletedFileWithChangedContents),
			(nameof(nxmStore.ReconcileRecoversInterruptedResolvingAndInstallingStates), nxmStore.ReconcileRecoversInterruptedResolvingAndInstallingStates),
			(nameof(nxmStore.ReconcileValidatesRetainedArchiveForInstallFailure), nxmStore.ReconcileValidatesRetainedArchiveForInstallFailure),
			(nameof(nxmStore.ReconcileRecoversLegacyRetryWhenCompletedArchiveIsIntact), nxmStore.ReconcileRecoversLegacyRetryWhenCompletedArchiveIsIntact),
			(nameof(nxmStore.ReconcilePersistsLegacyRecoveryWhenPartialCleanupFails), nxmStore.ReconcilePersistsLegacyRecoveryWhenPartialCleanupFails),
			(nameof(nxmStore.CorruptManifestIsQuarantinedWithoutBlockingStartup), nxmStore.CorruptManifestIsQuarantinedWithoutBlockingStartup),
			(nameof(nxmStore.CorruptManifestRecoversLastKnownGoodBackup), nxmStore.CorruptManifestRecoversLastKnownGoodBackup),
			(nameof(packageArchives.InitializeDoesNotCreateAnEmptyOptOutLibrary), packageArchives.InitializeDoesNotCreateAnEmptyOptOutLibrary),
			(nameof(packageArchives.PakArchiveCardDoesNotPromiseAForcedInactiveReinstall), packageArchives.PakArchiveCardDoesNotPromiseAForcedInactiveReinstall),
			(nameof(packageArchives.RetentionIsContentAddressedDeduplicatedAndRemovesManagedInboxCopy), packageArchives.RetentionIsContentAddressedDeduplicatedAndRemovesManagedInboxCopy),
			(nameof(packageArchives.ArchiveIndexContainsSafeIdentityButNoCapabilitiesOrSignedUrls), packageArchives.ArchiveIndexContainsSafeIdentityButNoCapabilitiesOrSignedUrls),
			(nameof(packageArchives.QuotaPrunesLeastRecentlyUsedPackages), packageArchives.QuotaPrunesLeastRecentlyUsedPackages),
			(nameof(downloadBatch.DuplicateArchivesAndTargetsKeepTheFirstInboxEntry), downloadBatch.DuplicateArchivesAndTargetsKeepTheFirstInboxEntry),
			(nameof(downloadBatch.DependencyMayBeInstalledOrProvidedByTheSameBatch), downloadBatch.DependencyMayBeInstalledOrProvidedByTheSameBatch),
			(nameof(downloadBatch.MissingDependencySkipsOnlyItsDependentPackage), downloadBatch.MissingDependencySkipsOnlyItsDependentPackage),
			(nameof(downloadBatch.DependencyLossAfterAConflictCascadesSafely), downloadBatch.DependencyLossAfterAConflictCascadesSafely),
			(nameof(nxmTransfer.MatchingRangeResponseResumesPartialFile), nxmTransfer.MatchingRangeResponseResumesPartialFile),
			(nameof(nxmTransfer.FullResponseRestartsInsteadOfAppendingPartialFile), nxmTransfer.FullResponseRestartsInsteadOfAppendingPartialFile),
			(nameof(nxmTransfer.SidecarValidatorEnablesResumeAfterRestart), nxmTransfer.SidecarValidatorEnablesResumeAfterRestart),
			(nameof(nxmTransfer.MismatchedResumeValidatorRestartsFromZero), nxmTransfer.MismatchedResumeValidatorRestartsFromZero),
			(nameof(nxmTransfer.ShortResponseIsNotPublishedAsComplete), nxmTransfer.ShortResponseIsNotPublishedAsComplete),
			(nameof(nxmTransfer.ApproximateMetadataSizeDoesNotRejectCompleteResponse), nxmTransfer.ApproximateMetadataSizeDoesNotRejectCompleteResponse),
			(nameof(nxmTransfer.UnsolicitedPartialResponseIsNotPublished), nxmTransfer.UnsolicitedPartialResponseIsNotPublished),
			(nameof(nxmTransfer.ResponseWithoutADeclaredLengthIsNotPublished), nxmTransfer.ResponseWithoutADeclaredLengthIsNotPublished),
			(nameof(nxmTransfer.StalledResponseBodyTimesOutWithoutPublishing), nxmTransfer.StalledResponseBodyTimesOutWithoutPublishing)
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
				Console.Error.WriteLine($"FAIL {test.Name}: {ex}");
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

	public static TException Throws<TException>(Action action) where TException : Exception
	{
		try
		{
			action();
		}
		catch (TException exception)
		{
			return exception;
		}
		throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
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
