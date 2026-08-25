using DivinityModManager.Models;
using DivinityModManager.Util;

using System;
using System.Collections.Generic;

namespace Redux.Core.Tests;

public sealed class SettingsMaintenanceTests
{
	public void RestoringAutomaticCategoriesClearsCurrentAndLegacyAssignmentsOnly()
	{
		var settings = new DivinityModManagerSettings
		{
			CustomModCategories = new List<string> { "My Category" },
			ModCategoryAssignments = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
			{
				["first-mod"] = new List<string> { "Armor" },
				["second-mod"] = new List<string> { "__ReduxNoCategory__" }
			},
			ModCategoryOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["FIRST-MOD"] = "Legacy Armor"
			},
			ModCategoryColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["My Category"] = "#123456"
			}
		};

		var affected = ModCategoryAssignmentReset.ClearManualAssignments(settings);

		RegressionAssert.Equal(2, affected);
		RegressionAssert.Equal(0, settings.ModCategoryAssignments.Count);
		RegressionAssert.Equal(0, settings.ModCategoryOverrides.Count);
		RegressionAssert.SequenceEqual(new[] { "My Category" }, settings.CustomModCategories);
		RegressionAssert.Equal("#123456", settings.ModCategoryColors["My Category"]);
	}

	public void RestoringAutomaticCategoriesMakesTheClassifierAuthoritativeAgain()
	{
		var mod = new DivinityModData
		{
			UUID = "better-hotbar",
			Name = "Better Hotbar 2",
			Folder = "BetterHotbar2"
		};
		var settings = new DivinityModManagerSettings
		{
			ModCategoryAssignments = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
			{
				[mod.UUID] = new List<string> { "Armor" }
			}
		};

		ModCategoryAssignmentReset.ClearManualAssignments(settings);
		var automaticCategory = AutomaticModCategoryClassifier.Classify(mod, _ => true);

		RegressionAssert.False(settings.ModCategoryAssignments.ContainsKey(mod.UUID));
		RegressionAssert.Equal("User Interface", automaticCategory);
	}
}
