using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Redux.Core.Tests;

public sealed class CategoryPreferenceTests
{
	public void ClearedAutomaticAssignmentsPersistWithoutReplacingManualChoicesOrNewMods()
	{
		var settings = new DivinityModManagerSettings
		{
			CustomModCategories = ["My Collection"],
			ModCategoryAssignments = new(StringComparer.OrdinalIgnoreCase)
			{
				["manual"] = ["My Collection", "Armor"],
				["none"] = [MainWindowViewModel.NoCategoryAssignment]
			},
			ModCategoryOverrides = new(StringComparer.OrdinalIgnoreCase) { ["legacy"] = "Spells" }
		};
		var automatic = Mod("automatic");
		var directory = Path.Combine(Path.GetTempPath(), "ReduxCategoryPreferenceTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			var path = Path.Combine(directory, "settings.json");
			var count = ModCategoryAssignmentReset.ClearAutomaticAssignments(settings,
				[automatic, Mod("AUTOMATIC"), Mod("manual"), Mod("none"), Mod("legacy"),
				 new DivinityModData { UUID = "separator", IsVisualDivider = true }], () =>
				{
					File.WriteAllText(path, JsonConvert.SerializeObject(settings));
					return true;
				});
			RegressionAssert.Equal(1, count);
			var restored = JsonConvert.DeserializeObject<DivinityModManagerSettings>(File.ReadAllText(path))!;
			RegressionAssert.SequenceEqual(new[] { "automatic" }, restored.ClearedAutomaticCategoryModIds);
			RegressionAssert.SequenceEqual(new[] { "My Collection", "Armor" }, restored.ModCategoryAssignments["manual"]);
			RegressionAssert.Equal(MainWindowViewModel.NoCategoryAssignment, restored.ModCategoryAssignments["none"].Single());
			RegressionAssert.Equal("Spells", restored.ModCategoryOverrides["legacy"]);
			RegressionAssert.SequenceEqual(new[] { "My Collection" }, restored.CustomModCategories);
			RegressionAssert.Equal(0, Effective(restored, automatic).Count);
			RegressionAssert.False(Model(restored).UsesAutomaticCategoryAssignment(automatic));
			RegressionAssert.True(Effective(restored, Mod("new-mod")).Contains("User Interface"));
			RegressionAssert.True(Model(restored).UsesAutomaticCategoryAssignment(Mod("new-mod")));
			RegressionAssert.Equal(1, ModCategoryAssignmentReset.RestoreClearedAutomaticAssignments(restored, () => true));
			RegressionAssert.True(Effective(restored, automatic).Contains("User Interface"));
			RegressionAssert.True(Model(restored).UsesAutomaticCategoryAssignment(automatic));
			RegressionAssert.SequenceEqual(new[] { "My Collection", "Armor" }, Effective(restored, Mod("manual")));
			RegressionAssert.Equal(0, Effective(restored, Mod("none")).Count);
		}
		finally { Directory.Delete(directory, true); }
	}

	public void HiddenCategoryFiltersDoNotChangeClassificationOrManualAssignments()
	{
		var settings = new DivinityModManagerSettings
		{
			CustomModCategories = ["My Collection"],
			DisabledModCategories = ["My Collection", MainWindowViewModel.UncategorizedModsCategory],
			ModCategoryAssignments = new(StringComparer.OrdinalIgnoreCase) { ["manual"] = ["Armor"] },
			HideEmptyModCategories = true
		};
		var model = Model(settings);
		var builtIns = model.GetBuiltInCategoryFilters();
		RegressionAssert.False(builtIns.Contains("My Collection"));
		RegressionAssert.False(builtIns.Contains(MainWindowViewModel.UncategorizedModsCategory));
		RegressionAssert.True(ModCategoryAssignmentReset.SetFiltersVisible(settings, builtIns, false, () => true));
		RegressionAssert.False(model.IsModCategoryEnabled("Armor"));
		RegressionAssert.SequenceEqual(new[] { "Armor" }, Effective(settings, Mod("manual")));
		RegressionAssert.True(Effective(settings, Mod("automatic")).Contains("User Interface"));
		settings.EnableAutomaticModCategories = false;
		RegressionAssert.True(ModCategoryAssignmentReset.SetFiltersVisible(settings, builtIns, true, () => true));
		RegressionAssert.SequenceEqual(new[] { "My Collection", MainWindowViewModel.UncategorizedModsCategory }, settings.DisabledModCategories);
		RegressionAssert.True(settings.HideEmptyModCategories);
		RegressionAssert.False(settings.EnableAutomaticModCategories);
		RegressionAssert.Equal(0, Effective(settings, Mod("automatic")).Count);
		var sidebar = (IReadOnlyList<string>)typeof(MainWindowViewModel).GetMethod("GetSidebarCategoryOrder", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(model, null)!;
		RegressionAssert.True(sidebar.Contains("Armor")); // Explicitly assigned even while automatic rules are off.
		RegressionAssert.False(sidebar.Contains("User Interface"));
	}

	public void CategoryPreferenceSaveFailuresRestoreEveryPreviousChoice()
	{
		var settings = new DivinityModManagerSettings
		{
			ClearedAutomaticCategoryModIds = ["earlier"], DisabledModCategories = ["Armor"],
			ModCategoryAssignments = new(StringComparer.OrdinalIgnoreCase) { ["manual"] = ["My Collection"] },
			ModCategoryOverrides = new(StringComparer.OrdinalIgnoreCase) { ["legacy"] = "Armor" }
		};
		var original = JsonConvert.SerializeObject(settings);
		RegressionAssert.Throws<IOException>(() => ModCategoryAssignmentReset.ClearAutomaticAssignments(settings, [Mod("new")], () => false));
		RegressionAssert.Equal(original, JsonConvert.SerializeObject(settings));
		RegressionAssert.Throws<IOException>(() => ModCategoryAssignmentReset.RestoreClearedAutomaticAssignments(settings, () => throw new IOException("locked")));
		RegressionAssert.Equal(original, JsonConvert.SerializeObject(settings));
		RegressionAssert.Throws<IOException>(() => ModCategoryAssignmentReset.SetFiltersVisible(settings, ["Armor", "Spells"], false, () => false));
		RegressionAssert.Equal(original, JsonConvert.SerializeObject(settings));
		RegressionAssert.Throws<IOException>(() => ModCategoryAssignmentReset.SetFiltersVisible(settings, ["Armor"], true, () => false));
		RegressionAssert.Equal(original, JsonConvert.SerializeObject(settings));
		RegressionAssert.Throws<IOException>(() => ModCategoryAssignmentReset.ClearManualAssignments(settings, () => false));
		RegressionAssert.Equal(original, JsonConvert.SerializeObject(settings));
	}

	public void RestoringClearedAssignmentsHonorsGlobalDisableAndExplicitChoices()
	{
		var settings = new DivinityModManagerSettings
		{
			EnableAutomaticModCategories = false, ClearedAutomaticCategoryModIds = ["automatic", "manual"],
			ModCategoryAssignments = new(StringComparer.OrdinalIgnoreCase) { ["manual"] = ["Armor"] }
		};
		RegressionAssert.Equal(2, ModCategoryAssignmentReset.RestoreClearedAutomaticAssignments(settings, () => true));
		RegressionAssert.False(settings.EnableAutomaticModCategories);
		RegressionAssert.Equal(0, Effective(settings, Mod("automatic")).Count);
		RegressionAssert.SequenceEqual(new[] { "Armor" }, Effective(settings, Mod("manual")));
		settings.EnableAutomaticModCategories = true;
		RegressionAssert.True(Effective(settings, Mod("automatic")).Contains("User Interface"));
	}

	public void FullCategoryResetRestoresClearedAssignmentsAndNormalizesPersistedIds()
	{
		var settings = JsonConvert.DeserializeObject<DivinityModManagerSettings>("{\"ClearedAutomaticCategoryModIds\":[\"first\",\"FIRST\",null,\"\"],\"ModCategoryAssignments\":{\"second\":[\"Armor\"]}}")!;
		RegressionAssert.SequenceEqual(new[] { "first" }, settings.ClearedAutomaticCategoryModIds);
		RegressionAssert.Equal(2, ModCategoryAssignmentReset.ClearManualAssignments(settings, () => true));
		RegressionAssert.Equal(0, settings.ClearedAutomaticCategoryModIds.Count);
		RegressionAssert.Equal(0, settings.ModCategoryAssignments.Count);
	}

	private static DivinityModData Mod(string uuid) => new() { UUID = uuid, Name = "Better Hotbar", Folder = "BetterHotbar" };
	private static MainWindowViewModel Model(DivinityModManagerSettings settings)
	{
		// Invoke only category calculation; no discovery, scheduling, or user-data I/O.
		var model = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
		typeof(MainWindowViewModel).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, settings);
		return model;
	}
	private static IReadOnlyList<string> Effective(DivinityModManagerSettings settings, DivinityModData mod) =>
		(IReadOnlyList<string>)typeof(MainWindowViewModel).GetMethod("GetEffectiveModCategories", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Model(settings), [mod])!;
}
