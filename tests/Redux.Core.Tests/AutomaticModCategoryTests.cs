using DivinityModManager.Models;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.AppServices;
using DivinityModManager.Util;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Redux.Core.Tests;

public sealed class AutomaticModCategoryTests
{
	public void NexusCategoryIdsMatchTheBg3ProviderTaxonomy()
	{
		var expected = new Dictionary<long, string>
		{
			[2] = "Miscellaneous",
			[3] = "Character Customization",
			[4] = "Visuals",
			[5] = "Gameplay",
			[6] = "User Interface",
			[7] = "Utilities",
			[9] = "Audio",
			[10] = "Equipment",
			[12] = "Classes",
			[13] = "Spells",
			[15] = "Races",
			[16] = "Dice",
			[17] = "Armor",
			[18] = "Animations",
			[19] = "Quests",
			[20] = "Accessories",
			[21] = "Companions",
			[22] = "Weapons",
			[23] = "Clothing",
			[24] = "Resources",
			[25] = "Maps",
			[26] = "Photo Mode"
		};

		foreach (var pair in expected)
		{
			RegressionAssert.True(AutomaticModCategoryClassifier.TryGetNexusCategory(pair.Key, out var category));
			RegressionAssert.Equal(pair.Value, category);
		}
	}

	public void ExplicitNexusCategoryWinsOverContradictoryKeywords()
	{
		var mod = CreateMod("Better Hotbar UI");
		mod.NexusModsData.ModId = 123;
		mod.NexusModsData.CategoryId = 17;
		mod.NexusModsData.IsUpdated = true;
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.Manual;
		mod.ModioData.ModId = 456;
		mod.ModioData.Tags.Add(new ModioTagData { Name = "Spells" });

		RegressionAssert.Equal("Armor", Classify(mod));
	}

	public void NexusCategoryStaysFirstWhileStrongSecondaryCategoriesFillThreeSlots()
	{
		var mod = CreateMod("Companions Panel and Camp Chest Everywhere");
		mod.NexusModsData.ModId = 4968;
		mod.NexusModsData.CategoryId = 6;
		mod.NexusModsData.IsUpdated = true;
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.LiveApi;

		var categories = ClassifyCategories(mod);

		RegressionAssert.SequenceEqual(
			new[] { "User Interface", "Companions", "Gameplay" },
			categories);
	}

	public void AutomaticCategoriesNeverExceedThree()
	{
		var mod = CreateMod("Companion UI Spell Armor Weapon Gameplay Map");

		var categories = ClassifyCategories(mod);

		RegressionAssert.Equal(AutomaticModCategoryClassifier.MaxAutomaticCategories, categories.Count);
		RegressionAssert.Equal(categories.Count, categories.Distinct(StringComparer.OrdinalIgnoreCase).Count());
	}

	public void WeakDescriptionMentionsDoNotCreateSecondaryCategoryNoise()
	{
		var mod = CreateMod("Aegis");
		mod.NexusModsData.ModId = 123;
		mod.NexusModsData.CategoryId = 17;
		mod.NexusModsData.Description = "A quest mentions a spell and a companion in passing.";
		mod.NexusModsData.IsUpdated = true;
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.Manual;

		RegressionAssert.SequenceEqual(new[] { "Armor" }, ClassifyCategories(mod));
	}

	public void BundledNexusProjectPreservesItsAuthorCategoryOffline()
	{
		var match = ReduxModDatabaseService.TryResolveProject(4968);
		RegressionAssert.True(match != null);

		var metadata = match!.CreateMetadata(Guid.NewGuid().ToString());

		RegressionAssert.Equal(6L, metadata.CategoryId);
		RegressionAssert.True(AutomaticModCategoryClassifier.TryGetNexusCategoryId(
			"Character Customisation", out var localizedCategoryId));
		RegressionAssert.Equal(3L, localizedCategoryId);
	}

	public void NativeModioCategoryWinsOverASecondaryNexusMatch()
	{
		var mod = CreateMod("Better Hotbar UI");
		mod.NexusModsData.ModId = 123;
		mod.NexusModsData.CategoryId = 17;
		mod.NexusModsData.IsUpdated = true;
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.LiveApi;
		mod.ModioData.ModId = 456;
		mod.ModioData.Tags.Add(new ModioTagData { Name = "Spells" });

		RegressionAssert.Equal("Spells", Classify(mod));
	}

	public void UnknownProviderTaxonomyFallsBackToPackageKeywords()
	{
		var mod = CreateMod("Better Hotbar UI");
		mod.ModioData.ModId = 456;
		mod.ModioData.Tags.Add(new ModioTagData { Name = "Controller Friendly" });

		RegressionAssert.Equal("User Interface", Classify(mod));
	}

	public void DisabledProviderCategoryFallsBackToAnEnabledCategory()
	{
		var mod = CreateMod("Better Hotbar UI");
		mod.NexusModsData.ModId = 123;
		mod.NexusModsData.CategoryId = 17;
		mod.NexusModsData.IsUpdated = true;
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.Manual;

		var category = AutomaticModCategoryClassifier.Classify(
			mod,
			candidate => !candidate.Equals("Armor", StringComparison.OrdinalIgnoreCase));

		RegressionAssert.Equal("User Interface", category);
	}

	private static DivinityModData CreateMod(string name) => new()
	{
		UUID = Guid.NewGuid().ToString(),
		Name = name,
		Folder = name
	};

	private static string Classify(DivinityModData mod) =>
		AutomaticModCategoryClassifier.Classify(mod, _ => true);

	private static IReadOnlyList<string> ClassifyCategories(DivinityModData mod) =>
		AutomaticModCategoryClassifier.ClassifyCategories(mod, _ => true);
}
