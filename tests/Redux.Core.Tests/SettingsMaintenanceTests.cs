using DivinityModManager.Models;
using DivinityModManager.Extensions;
using DivinityModManager.Util;

using Newtonsoft.Json;

using ReactiveUI.Fody.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Redux.Core.Tests;

public sealed class SettingsMaintenanceTests
{
	public void BuiltInTypographyChoicesKeepTheFocusedReduxOrder()
	{
		var builtIns = ReduxCustomFontService.GetChoices()
			.Where(choice => !choice.IsCustom)
			.Select(choice => choice.BuiltInFont);

		RegressionAssert.SequenceEqual(new[]
		{
			ReduxTypographyFont.Manrope,
			ReduxTypographyFont.ArchivoBlack,
			ReduxTypographyFont.IBMPlexMono,
			ReduxTypographyFont.AtkinsonHyperlegible,
			ReduxTypographyFont.SegoeUI
		}, builtIns);
	}

	public void ParchmentUsesSegoeByDefaultAndKeepsExplicitOverrides()
	{
		var settings = new DivinityModManagerSettings
		{
			ColorTheme = ReduxThemeType.Parchment
		};

		var defaultSelection = ReduxTypographyService.ResolveSelection(settings);
		RegressionAssert.Equal(ReduxTypographyFont.SegoeUI, defaultSelection.Font);
		RegressionAssert.Equal(String.Empty, defaultSelection.CustomReference);

		settings.UseThemeDefaultTypography = false;
		settings.TypographyFont = ReduxTypographyFont.Manrope;
		var explicitManrope = ReduxTypographyService.ResolveSelection(settings);
		RegressionAssert.Equal(ReduxTypographyFont.Manrope, explicitManrope.Font);

		var restored = JsonConvert.DeserializeObject<DivinityModManagerSettings>(JsonConvert.SerializeObject(settings));
		RegressionAssert.True(restored != null);
		RegressionAssert.False(restored!.UseThemeDefaultTypography);
		RegressionAssert.Equal(ReduxTypographyFont.Manrope, ReduxTypographyService.ResolveSelection(restored).Font);
	}

	public void BuiltInThemeCyclingChangesOnlyInheritedTypography()
	{
		var inherited = new DivinityModManagerSettings { ColorTheme = ReduxThemeType.ReduxLight };
		ReduxThemeService.CycleTheme(inherited);
		RegressionAssert.Equal(ReduxThemeType.Parchment, inherited.ColorTheme);
		RegressionAssert.Equal(ReduxTypographyFont.SegoeUI, ReduxTypographyService.ResolveSelection(inherited).Font);

		ReduxThemeService.CycleTheme(inherited);
		RegressionAssert.Equal(ReduxThemeType.ReduxDark, inherited.ColorTheme);
		RegressionAssert.Equal(ReduxTypographyFont.Manrope, ReduxTypographyService.ResolveSelection(inherited).Font);

		var overridden = new DivinityModManagerSettings
		{
			ColorTheme = ReduxThemeType.ReduxLight,
			TypographyFont = ReduxTypographyFont.ArchivoBlack,
			UseThemeDefaultTypography = false
		};
		ReduxThemeService.CycleTheme(overridden);
		RegressionAssert.Equal(ReduxThemeType.Parchment, overridden.ColorTheme);
		RegressionAssert.Equal(ReduxTypographyFont.ArchivoBlack, ReduxTypographyService.ResolveSelection(overridden).Font);
	}

	public void SaveGameCampaignCollapseStateRoundTripsWithoutDuplicates()
	{
		var settings = new DivinityModManagerSettings
		{
			CollapsedSaveGameCampaigns = new List<string> { "Shadowheart", "shadowheart", "  ", "Tav" }
		};

		var restored = JsonConvert.DeserializeObject<DivinityModManagerSettings>(JsonConvert.SerializeObject(settings));

		RegressionAssert.True(restored != null);
		RegressionAssert.SequenceEqual(new[] { "Shadowheart", "Tav" }, restored!.CollapsedSaveGameCampaigns);
	}

	public void IconOnlyCategoriesRemainCustomAndRequireAnIcon()
	{
		var settings = new DivinityModManagerSettings
		{
			CustomModCategories = new List<string> { "Compact", "No Icon" },
			IconOnlyModCategories = new List<string> { "Compact", "compact", "No Icon", "Libraries" },
			ModCategoryIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Compact"] = "star",
				["No Icon"] = String.Empty,
				["Libraries"] = "library"
			}
		};

		var restored = JsonConvert.DeserializeObject<DivinityModManagerSettings>(JsonConvert.SerializeObject(settings));

		RegressionAssert.True(restored != null);
		RegressionAssert.SequenceEqual(new[] { "Compact" }, restored!.IconOnlyModCategories);
	}

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

	public void ElevationWarningRequiresAnElevatedUnsuppressedProcessAndSchedulesOnce()
	{
		var standard = new ProcessElevationInfo(ProcessElevationState.Standard);
		var elevated = new ProcessElevationInfo(ProcessElevationState.Elevated);
		var unknown = new ProcessElevationInfo(ProcessElevationState.Unknown, 5);

		RegressionAssert.False(ProcessElevationWarningPolicy.ShouldShow(standard, false));
		RegressionAssert.False(ProcessElevationWarningPolicy.ShouldShow(unknown, false));
		RegressionAssert.False(ProcessElevationWarningPolicy.ShouldShow(elevated, true));
		RegressionAssert.True(ProcessElevationWarningPolicy.ShouldShow(elevated, false));

		var schedulingGate = 0;
		RegressionAssert.True(ProcessElevationWarningPolicy.TryMarkScheduled(ref schedulingGate));
		RegressionAssert.False(ProcessElevationWarningPolicy.TryMarkScheduled(ref schedulingGate));
	}

	public void FailedElevationWarningSuppressionRestoresThePreviousPreference()
	{
		var confirmations = new DivinityModManager.Models.App.ConfirmationSettings();
		RegressionAssert.False(ProcessElevationWarningPolicy.TryPersistSuppression(confirmations, () => false));
		RegressionAssert.False(confirmations.DisableAdminModeWarning);

		RegressionAssert.True(ProcessElevationWarningPolicy.TryPersistSuppression(confirmations, () => true));
		RegressionAssert.True(confirmations.DisableAdminModeWarning);
	}

	public void ElevationWarningSuppressionIsRestoredIntoLiveSettings()
	{
		var saved = new DivinityModManagerSettings();
		saved.Confirmations.DisableAdminModeWarning = true;
		var live = new DivinityModManagerSettings();

		live.SetFrom<DivinityModManagerSettings, ReactiveAttribute>(saved);

		RegressionAssert.True(live.Confirmations.DisableAdminModeWarning);
	}

	public void CurrentWindowsProcessElevationCanBeReadFromItsToken()
	{
		if (!OperatingSystem.IsWindows()) return;
		var elevation = ProcessHelper.GetCurrentProcessElevation();
		if (elevation.State == ProcessElevationState.Unknown)
			throw new InvalidOperationException($"Windows token elevation could not be read. Win32 error: {elevation.Win32Error}.");
	}
}
