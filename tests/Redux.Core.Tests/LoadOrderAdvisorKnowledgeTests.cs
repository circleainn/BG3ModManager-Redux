using System;
using System.Collections.Generic;
using System.Linq;

using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;

using DynamicData;

namespace Redux.Core.Tests;

internal sealed class LoadOrderAdvisorKnowledgeTests
{
	private const string ImpUiUuid = "26922ba9-6018-5252-075d-7ff2ba6ed879";
	private const string BetterInventoryUiUuid = "6b585be8-ed73-7347-2c58-73146e22b7d4";
	private const string CompatibilityFrameworkUuid = "67fbbd53-7c7d-4cfa-9409-6d737b4d92a9";

	public void BundledKnowledgeIncludesGroupsAliasesAndSubstitutes()
	{
		var knowledge = ReduxModDatabaseService.LoadOrderAdvisorKnowledge;

		RegressionAssert.Equal(12017, knowledge.EntryCount);
		RegressionAssert.Equal(30, knowledge.GroupCount);
		RegressionAssert.Equal(9, knowledge.DependencyAliasCount);
		RegressionAssert.Equal(4, knowledge.DependencySubstituteCount);
		RegressionAssert.True(knowledge.TryGetGroupPosition("Resources", out var resources));
		RegressionAssert.True(knowledge.TryGetGroupPosition("Utilities", out var utilities));
		RegressionAssert.True(resources < utilities);
	}

	public void ExactDependencyAliasesAndSubstitutesResolveInstalledMods()
	{
		var knowledge = ReduxModDatabaseService.LoadOrderAdvisorKnowledge;
		var aliasTarget = CreateMod("0dd5b581-c210-4956-ab96-7682fb519de5", "Vlad's Grimoire");
		var substitute = CreateMod("7b8366bd-abc1-4f9f-ba9d-585549b4a750", "Installed Replacement");
		var installed = new[] { aliasTarget, substitute }.ToDictionary(
			mod => mod.UUID,
			mod => (DivinityModData)mod,
			StringComparer.OrdinalIgnoreCase);

		RegressionAssert.True(knowledge.TryResolveInstalledDependency(
			String.Empty,
			"Vlad's Grimoire",
			installed,
			out var resolvedAlias));
		RegressionAssert.Equal(aliasTarget.UUID, resolvedAlias);
		RegressionAssert.True(knowledge.TryResolveInstalledDependency(
			"3779a4fb-0c2c-404a-beee-879d97eb9e87",
			"Legacy Requirement",
			installed,
			out var resolvedSubstitute));
		RegressionAssert.Equal(substitute.UUID, resolvedSubstitute);
	}

    public void LibraryListingAliasesResolveOnlyInstalledModules()
    {
        var knowledge = ReduxModDatabaseService.LoadOrderAdvisorKnowledge;
        var aliases = new Dictionary<string, string>
        {
            ["Mod Configuration Menu (MCM)"] = "755a8a72-407f-4f0d-9a33-274ac0f0b53d",
            ["Baldur's Gate 3 Community Library"] = "396c5966-09b0-40a1-af3f-93a5e9ce71c0",
            ["Fade's Equipment Distribution (FED)"] = "3baef2b9-80d3-777b-a256-394f7f7be0d8",
            ["Goon's Library - Passives Functions Spells and More"] = "07fbc2f1-f359-4b9d-b243-fe28bd783e4c"
        };
        foreach (var alias in aliases)
        {
            var installed = new Dictionary<string, DivinityModData> { [alias.Value] = CreateMod(alias.Value, "Local module name") };
            RegressionAssert.True(knowledge.TryResolveInstalledDependency(String.Empty, alias.Key, installed, out var resolved));
            RegressionAssert.Equal(alias.Value, resolved);
            installed.Clear();
            RegressionAssert.False(knowledge.TryResolveInstalledDependency(String.Empty, alias.Key, installed, out _));
        }
    }

	public void OfflineDependencyFactsExtendTheExistingAdvisor()
	{
		var dependent = CreateMod(BetterInventoryUiUuid, "Better Inventory UI");
		var dependency = CreateMod(ImpUiUuid, "ImpUI");
		var activeOrder = new[] { dependent, dependency };

		var snapshots = new ModHealthAnalyzer().AnalyzeAll(
			activeOrder,
			activeOrder,
			enableLoadOrderAdvisor: true);

		var finding = FindSnapshot(snapshots, dependent.UUID).Findings.Single(item =>
			item.Code == ModHealthFindingCode.DependencyLoadsLater);
		RegressionAssert.SequenceEqual(new[] { dependency.UUID }, finding.RelatedModUuids);
		RegressionAssert.SequenceEqual(new[] { dependent, dependency }, activeOrder);
	}

	public void AuthorProvidedPlacementExtendsTheExistingAdvisor()
	{
		var mod = CreateMod("d76ff1e5-e09e-4565-a9d2-a035037f6134", "Valkrana's Skeleton Crew Feat");
		var predecessor = CreateMod("f323b958-b845-4c79-b139-d39570658fbb", "Necromancy Reanimated");
		var activeOrder = new[] { mod, predecessor };

		var snapshots = new ModHealthAnalyzer().AnalyzeAll(
			activeOrder,
			activeOrder,
			enableLoadOrderAdvisor: true);

		var finding = FindSnapshot(snapshots, mod.UUID).Findings.Single(item =>
			item.Code == ModHealthFindingCode.RecommendedPredecessorLoadsLater);
		RegressionAssert.SequenceEqual(new[] { predecessor.UUID }, finding.RelatedModUuids);
		RegressionAssert.Contains(finding.Message, "documented mod-author guidance");
	}

	public void ExceptionalLateLoadingDependenciesDoNotCreateFalseAdvice()
	{
		var dependent = CreateMod("c4fbb55d-ef4c-44d5-92db-101b6858bb54", "Compatibility Patch");
		var dependency = CreateMod(CompatibilityFrameworkUuid, "Compatibility Framework");
		dependent.Dependencies.AddOrUpdate(new ModuleShortDesc
		{
			UUID = dependency.UUID,
			Name = dependency.Name,
			Folder = dependency.Folder,
			Version = dependency.Version
		});
		var activeOrder = new[] { dependent, dependency };

		var snapshot = FindSnapshot(
			new ModHealthAnalyzer().AnalyzeAll(activeOrder, activeOrder, enableLoadOrderAdvisor: true),
			dependent.UUID);

		RegressionAssert.False(snapshot.Findings.Any(item =>
			item.Code == ModHealthFindingCode.DependencyLoadsLater));
	}

	private static RegressionModData CreateMod(string uuid, string name) => new()
	{
		UUID = uuid,
		Name = name,
		Folder = name,
		IsActive = true,
		Version = DivinityModVersion2.FromInt(1)
	};

	private static ModHealthSnapshot FindSnapshot(IEnumerable<ModHealthSnapshot> snapshots, string uuid)
	{
		return snapshots.Single(snapshot =>
			String.Equals(snapshot.Mod.UUID, uuid, StringComparison.OrdinalIgnoreCase));
	}
}
