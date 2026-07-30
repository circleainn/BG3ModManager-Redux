using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;

using DivinityModManager;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;
using DivinityModManager.Models.Metadata;
using DivinityModManager.Models.Modio;
using DynamicData;

namespace Redux.Core.Tests;

internal sealed class ModHealthTests
{
	public void MissingAndInactiveDependenciesRemainIndependentOfLoadOrderGuidance()
	{
		var inactiveDependency = CreateMod("dependency", "Dependency", isActive: false);
		var activeMod = CreateMod("active", "Active Mod", isActive: true);
		activeMod.Dependencies.AddOrUpdate(ToDependency(inactiveDependency));
		activeMod.MissingDependencies.AddOrUpdate(new ModuleShortDesc
		{
			UUID = "missing",
			Name = "Missing Dependency"
		});

		var snapshots = new ModHealthAnalyzer().AnalyzeAll(
			new[] { activeMod, inactiveDependency },
			new[] { activeMod });
		var activeSnapshot = FindSnapshot(snapshots, activeMod.UUID);

		RegressionAssert.True(HasFinding(activeSnapshot, ModHealthFindingCode.InactiveDependency));
		RegressionAssert.True(HasFinding(activeSnapshot, ModHealthFindingCode.MissingDependency));
		RegressionAssert.SequenceEqual(
			new[] { inactiveDependency.UUID },
			activeSnapshot.Findings
				.Single(finding => finding.Code == ModHealthFindingCode.InactiveDependency)
				.RelatedModUuids);
		RegressionAssert.SequenceEqual(
			new[] { "missing" },
			activeSnapshot.Findings
				.Single(finding => finding.Code == ModHealthFindingCode.MissingDependency)
				.RelatedModUuids);
		RegressionAssert.Equal(0, activeSnapshot.LoadOrderAdviceCount);
	}

	public void LoadOrderGuidanceFindingsAreAbsentUntilEnabled()
	{
		var dependent = CreateMod("dependent", "Dependent", isActive: true);
		var dependency = CreateMod("dependency", "Dependency", isActive: true);
		dependent.Dependencies.AddOrUpdate(ToDependency(dependency));
		var activeOrder = new[] { dependent, dependency };

		var disabled = new ModHealthAnalyzer().AnalyzeAll(activeOrder, activeOrder, enableLoadOrderAdvisor: false);
		var enabled = new ModHealthAnalyzer().AnalyzeAll(activeOrder, activeOrder, enableLoadOrderAdvisor: true);

		RegressionAssert.False(HasFinding(
			FindSnapshot(disabled, dependent.UUID),
			ModHealthFindingCode.DependencyLoadsLater));
		RegressionAssert.True(HasFinding(
			FindSnapshot(enabled, dependent.UUID),
			ModHealthFindingCode.DependencyLoadsLater));
		RegressionAssert.SequenceEqual(
			new[] { dependency.UUID },
			FindSnapshot(enabled, dependent.UUID).Findings
				.Single(finding => finding.Code == ModHealthFindingCode.DependencyLoadsLater)
				.RelatedModUuids);
		RegressionAssert.SequenceEqual(
			new[] { "Dependent", "Dependency" },
			activeOrder.Select(mod => mod.Name));
	}

	public void CorrectDependencyPlacementDoesNotProduceGuidanceNoise()
	{
		var dependency = CreateMod("dependency", "Dependency", isActive: true);
		var dependent = CreateMod("dependent", "Dependent", isActive: true);
		dependent.Dependencies.AddOrUpdate(ToDependency(dependency));
		var activeOrder = new[] { dependency, dependent };

		var snapshot = FindSnapshot(
			new ModHealthAnalyzer().AnalyzeAll(
				activeOrder,
				activeOrder,
				enableLoadOrderAdvisor: true),
			dependent.UUID);

		RegressionAssert.False(HasFinding(
			snapshot,
			ModHealthFindingCode.DependencyLoadsLater));
		RegressionAssert.Equal(0, snapshot.LoadOrderAdviceCount);
		RegressionAssert.SequenceEqual(
			new[] { "Dependency", "Dependent" },
			activeOrder.Select(mod => mod.Name));
	}

	public void InvalidUuidIsReportedAsAReadOnlyHealthError()
	{
		var mod = CreateMod("invalid", "Invalid UUID", isActive: true);
		mod.UUID = "not-a-valid-guid";
		Dispatcher.CurrentDispatcher.Invoke(
			() => { },
			DispatcherPriority.Background);

		var snapshot = new ModHealthAnalyzer()
			.AnalyzeAll(new[] { mod }, new[] { mod })
			.Single();
		var finding = snapshot.Findings.Single(item =>
			item.Code == ModHealthFindingCode.InvalidUuid);

		RegressionAssert.Equal(ModHealthSeverity.Error, finding.Severity);
		RegressionAssert.True(snapshot.HasHealthErrors);
		RegressionAssert.Equal("not-a-valid-guid", mod.UUID);
	}

	public void SelfDependencyIsReportedWithoutDuplicateMissingDependencyNoise()
	{
		var mod = CreateMod("self", "Self Referencing Mod", isActive: true);
		var selfDependency = ToDependency(mod);
		mod.Dependencies.AddOrUpdate(selfDependency);
		mod.MissingDependencies.AddOrUpdate(selfDependency);

		var snapshot = FindSnapshot(
			new ModHealthAnalyzer().AnalyzeAll(new[] { mod }, new[] { mod }),
			mod.UUID);

		var finding = snapshot.Findings.Single(item =>
			item.Code == ModHealthFindingCode.SelfDependency);
		RegressionAssert.Equal(ModHealthSeverity.Error, finding.Severity);
		RegressionAssert.False(HasFinding(snapshot, ModHealthFindingCode.MissingDependency));
		RegressionAssert.Equal(1, mod.Dependencies.Count);
	}

	public void DependencyCyclesAreReportedOnlyByOptInGuidance()
	{
		var first = CreateMod("first", "First", isActive: true);
		var second = CreateMod("second", "Second", isActive: true);
		first.Dependencies.AddOrUpdate(ToDependency(second));
		second.Dependencies.AddOrUpdate(ToDependency(first));
		var activeOrder = new[] { first, second };

		var disabled = new ModHealthAnalyzer().AnalyzeAll(activeOrder, activeOrder, enableLoadOrderAdvisor: false);
		var enabled = new ModHealthAnalyzer().AnalyzeAll(activeOrder, activeOrder, enableLoadOrderAdvisor: true);

		RegressionAssert.False(disabled.Any(snapshot =>
			HasFinding(snapshot, ModHealthFindingCode.DependencyCycle)));
		RegressionAssert.True(enabled.All(snapshot =>
			HasFinding(snapshot, ModHealthFindingCode.DependencyCycle)));
	}

	public void InvalidCreatorManifestIsReportedWithoutApplyingItsClaims()
	{
		var mod = CreateMod("manifest", "Manifest Mod", isActive: false);
		mod.CreatorManifest = new ReduxCreatorManifestData
		{
			State = ReduxCreatorManifestState.Invalid,
			Diagnostic = "The package claim does not match this PAK.",
			Name = "Untrusted replacement name",
			Authors = new[] { "Untrusted replacement author" }
		};

		var snapshot = FindSnapshot(
			new ModHealthAnalyzer().AnalyzeAll(new[] { mod }, Array.Empty<DivinityModData>()),
			mod.UUID);
		var finding = snapshot.Findings.Single(item =>
			item.Code == ModHealthFindingCode.InvalidCreatorManifest);

		RegressionAssert.Contains(finding.Message, "does not match");
		RegressionAssert.Equal("Manifest Mod", mod.Name);
		RegressionAssert.Equal(String.Empty, mod.Author);
	}

	public void DuplicateUuidsAreReportedWithoutRemovingEitherPackage()
	{
		var first = CreateMod("duplicate", "First Copy", isActive: false);
		var second = CreateMod("duplicate", "Second Copy", isActive: false);
		var installed = new[] { first, second };

		var snapshots = new ModHealthAnalyzer().AnalyzeAll(
			installed,
			Array.Empty<DivinityModData>());

		RegressionAssert.Equal(2, snapshots.Count);
		RegressionAssert.True(snapshots.All(snapshot =>
			HasFinding(snapshot, ModHealthFindingCode.DuplicateUuid)));
		RegressionAssert.SequenceEqual(
			new[] { "First Copy", "Second Copy" },
			installed.Select(mod => mod.Name));
	}

	public void ActiveDeclaredConflictsAreReportedConservatively()
	{
		var first = CreateMod("first", "First", isActive: true);
		var second = CreateMod("second", "Second", isActive: true);
		first.Conflicts.AddOrUpdate(ToDependency(second));
		var active = new[] { first, second };

		var firstSnapshot = FindSnapshot(
			new ModHealthAnalyzer().AnalyzeAll(active, active),
			first.UUID);

		RegressionAssert.True(HasFinding(firstSnapshot, ModHealthFindingCode.DeclaredConflict));
		RegressionAssert.True(first.IsActive);
		RegressionAssert.True(second.IsActive);
	}

	public void OlderInstalledDependencyVersionsAreReportedWithoutUpdatingThem()
	{
		var dependency = CreateMod("dependency", "Dependency", isActive: true);
		dependency.Version = DivinityModVersion2.FromInt(36028797018963968);
		var dependent = CreateMod("dependent", "Dependent", isActive: true);
		dependent.Dependencies.AddOrUpdate(new ModuleShortDesc
		{
			UUID = dependency.UUID,
			Name = dependency.Name,
			Folder = dependency.Folder,
			Version = DivinityModVersion2.FromInt(36028797018963969)
		});
		var active = new[] { dependency, dependent };
		var originalVersion = dependency.Version.VersionInt;

		var dependentSnapshot = FindSnapshot(
			new ModHealthAnalyzer().AnalyzeAll(active, active),
			dependent.UUID);

		RegressionAssert.True(HasFinding(
			dependentSnapshot,
			ModHealthFindingCode.DependencyVersionTooOld));
		RegressionAssert.SequenceEqual(
			new[] { dependency.UUID },
			dependentSnapshot.Findings
				.Single(finding => finding.Code == ModHealthFindingCode.DependencyVersionTooOld)
				.RelatedModUuids);
		RegressionAssert.Equal(originalVersion, dependency.Version.VersionInt);
	}

	public void ScriptExtenderErrorsAndWarningsRemainDistinct()
	{
		var unavailable = CreateMod("unavailable", "Unavailable", isActive: true);
		unavailable.ExtenderModStatus = DivinityExtenderModStatus.MissingUpdater;
		var mismatch = CreateMod("mismatch", "Mismatch", isActive: true);
		mismatch.ExtenderModStatus = DivinityExtenderModStatus.MissingRequiredVersion;
		var active = new[] { unavailable, mismatch };

		var snapshots = new ModHealthAnalyzer().AnalyzeAll(active, active);
		var unavailableFinding = FindSnapshot(snapshots, unavailable.UUID).Findings.Single(
			finding => finding.Code == ModHealthFindingCode.ScriptExtenderUnavailable);
		var mismatchFinding = FindSnapshot(snapshots, mismatch.UUID).Findings.Single(
			finding => finding.Code == ModHealthFindingCode.ScriptExtenderVersionMismatch);

		RegressionAssert.Equal(ModHealthSeverity.Error, unavailableFinding.Severity);
		RegressionAssert.Equal(ModHealthSeverity.Warning, mismatchFinding.Severity);
	}

	public void ForceLoadedVariantsRemainInformationalAndReadOnly()
	{
		var alwaysLoaded = CreateMod("always", "Always Loaded", isActive: true);
		alwaysLoaded.IsForceLoaded = true;
		var merged = CreateMod("merged", "Merged Override", isActive: true);
		merged.IsForceLoaded = true;
		merged.IsForceLoadedMergedMod = true;
		var allowed = CreateMod("allowed", "Allowed Override", isActive: true);
		allowed.IsForceLoaded = true;
		allowed.ForceAllowInLoadOrder = true;
		var modFixer = CreateMod("fixer", "Legacy Fixer", isActive: true);
		modFixer.OsirisModStatus = DivinityOsirisModStatus.MODFIXER;
		var installed = new[] { alwaysLoaded, merged, allowed, modFixer };

		var snapshots = new ModHealthAnalyzer().AnalyzeAll(installed, installed);

		AssertInfoFinding(snapshots, alwaysLoaded.UUID, ModHealthFindingCode.AlwaysLoaded);
		AssertInfoFinding(snapshots, merged.UUID, ModHealthFindingCode.ContainsFileOverrides);
		AssertInfoFinding(snapshots, allowed.UUID, ModHealthFindingCode.AlwaysLoadedWithLoadOrderEntry);
		AssertInfoFinding(snapshots, modFixer.UUID, ModHealthFindingCode.LegacyModFixerIncluded);
		RegressionAssert.Equal(1, FindSnapshot(snapshots, alwaysLoaded.UUID).PackageBehaviorFindings.Count);
		RegressionAssert.Equal(1, FindSnapshot(snapshots, merged.UUID).PackageBehaviorFindings.Count);
		RegressionAssert.Equal(1, FindSnapshot(snapshots, allowed.UUID).PackageBehaviorFindings.Count);
		RegressionAssert.Equal(1, FindSnapshot(snapshots, modFixer.UUID).PackageBehaviorFindings.Count);
		RegressionAssert.True(installed.All(mod => mod.IsActive));
	}

	public void LocalOnlyPresentationSuppressesProviderFindingsWithoutDeletingMetadata()
	{
		var mod = CreateMod("modio", "mod.io Mod", isActive: true);
		mod.ModioData = new ModioModData
		{
			ModId = 12345,
			Name = "Linked mod.io project",
			MetadataOrigin = ModioMetadataOrigin.NativePackage
		};
		var analyzer = new ModHealthAnalyzer();

		var linkedSnapshot = FindSnapshot(
			analyzer.AnalyzeAll(new[] { mod }, new[] { mod }),
			mod.UUID);
		RegressionAssert.True(HasFinding(
			linkedSnapshot,
			ModHealthFindingCode.ModioManagedSource));

		mod.OnlineMetadataEnabled = false;
		var localSnapshot = FindSnapshot(
			analyzer.AnalyzeAll(new[] { mod }, new[] { mod }),
			mod.UUID);

		RegressionAssert.False(HasFinding(
			localSnapshot,
			ModHealthFindingCode.ModioManagedSource));
		RegressionAssert.True(mod.ModioData.HasMetadata);
	}

	public void DisablingModioWarningsHidesOnlyThatFinding()
	{
		var mod = CreateMod("modio-suppressed", "mod.io Mod", isActive: true);
		mod.ModioData = new ModioModData
		{
			ModId = 54321,
			Name = "Linked mod.io project",
			MetadataOrigin = ModioMetadataOrigin.NativePackage
		};
		// An unrelated finding proves the suppression is targeted rather than blanket.
		mod.IsForceLoaded = true;

		var analyzer = new ModHealthAnalyzer();
		var installed = new[] { mod };

		var normal = FindSnapshot(analyzer.AnalyzeAll(installed, installed), mod.UUID);
		RegressionAssert.True(HasFinding(normal, ModHealthFindingCode.ModioManagedSource));

		var suppressed = FindSnapshot(
			analyzer.AnalyzeAll(installed, installed, enableLoadOrderAdvisor: false, disableModioWarnings: true),
			mod.UUID);

		RegressionAssert.False(HasFinding(suppressed, ModHealthFindingCode.ModioManagedSource));

		// Everything else survives: other findings, and the mod.io metadata itself.
		foreach (var finding in normal.Findings.Where(item => item.Code != ModHealthFindingCode.ModioManagedSource))
		{
			RegressionAssert.True(HasFinding(suppressed, finding.Code));
		}

		RegressionAssert.True(mod.OnlineMetadataEnabled);
		RegressionAssert.True(mod.ModioData.HasMetadata);
	}

	private static RegressionModData CreateMod(string uuid, string name, bool isActive)
	{
		return new RegressionModData
		{
			UUID = CreateStableUuid(uuid),
			Name = name,
			Folder = name,
			IsActive = isActive,
			Version = DivinityModVersion2.FromInt(1)
		};
	}

	private static string CreateStableUuid(string seed)
	{
		var hash = MD5.HashData(Encoding.UTF8.GetBytes(seed ?? String.Empty));
		return new Guid(hash).ToString();
	}

	private static ModuleShortDesc ToDependency(DivinityModData mod)
	{
		return new ModuleShortDesc
		{
			UUID = mod.UUID,
			Name = mod.Name,
			Folder = mod.Folder,
			Version = mod.Version
		};
	}

	private static ModHealthSnapshot FindSnapshot(
		IEnumerable<ModHealthSnapshot> snapshots,
		string uuid)
	{
		return snapshots.Single(snapshot =>
			String.Equals(snapshot.Mod.UUID, uuid, StringComparison.OrdinalIgnoreCase));
	}

	private static bool HasFinding(ModHealthSnapshot snapshot, ModHealthFindingCode code)
	{
		return snapshot.Findings.Any(finding => finding.Code == code);
	}

	private static void AssertInfoFinding(
		IEnumerable<ModHealthSnapshot> snapshots,
		string uuid,
		ModHealthFindingCode code)
	{
		var finding = FindSnapshot(snapshots, uuid).Findings.Single(item => item.Code == code);
		RegressionAssert.Equal(ModHealthSeverity.Info, finding.Severity);
	}
}
