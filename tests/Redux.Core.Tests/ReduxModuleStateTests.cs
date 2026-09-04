using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;

using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.ModUpdater.Cache;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class ReduxModuleStateTests
{
	public void DefaultsKeepModDiagnosticsOnAndGuidanceOptIn()
	{
		var settings = new DivinityModManagerSettings();
		using var modules = new ReduxModuleState(settings);

		RegressionAssert.True(modules.SourceIntegrationsEnabled);
		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.False(modules.LoadOrderGuidanceEnabled);
	}

	public void FirstRunOnboardingStartsWithEveryOptionalFeatureOff()
	{
		var settings = new DivinityModManagerSettings
		{
			LocalOnlyMode = false,
			EnableModHealth = true,
			EnableLoadOrderAdvisor = true,
			HasSeenReduxWelcome = false
		};

		ReduxOnboardingPolicy.ApplyFirstRunDefaults(settings);

		RegressionAssert.True(settings.LocalOnlyMode);
		RegressionAssert.False(settings.EnableModHealth);
		RegressionAssert.False(settings.EnableLoadOrderAdvisor);
	}

	public void ReturningUsersKeepTheirOptionalFeatureChoices()
	{
		var settings = new DivinityModManagerSettings
		{
			LocalOnlyMode = false,
			EnableModHealth = true,
			EnableLoadOrderAdvisor = true,
			HasSeenReduxWelcome = true
		};

		ReduxOnboardingPolicy.ApplyFirstRunDefaults(settings);

		RegressionAssert.False(settings.LocalOnlyMode);
		RegressionAssert.True(settings.EnableModHealth);
		RegressionAssert.True(settings.EnableLoadOrderAdvisor);
	}

	public void LocalOnlyModeChangesOnlySourceIntegrations()
	{
		var settings = new DivinityModManagerSettings
		{
			EnableModHealth = true,
			EnableLoadOrderAdvisor = true
		};
		using var modules = new ReduxModuleState(settings);

		settings.LocalOnlyMode = true;

		RegressionAssert.False(modules.SourceIntegrationsEnabled);
		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.True(modules.LoadOrderGuidanceEnabled);

		settings.LocalOnlyMode = false;

		RegressionAssert.True(modules.SourceIntegrationsEnabled);
		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.True(modules.LoadOrderGuidanceEnabled);
	}

	public void CategoryInteractionSettingSynchronizesLegacyPresentationFlags()
	{
		var settings = new DivinityModManagerSettings
		{
			UseCategoryColorsForHover = false,
			UseCategoryColorsForSidebarSelection = true
		};

		RegressionAssert.True(settings.UseCategoryColorsForInteractions);

		settings.UseCategoryColorsForInteractions = false;
		RegressionAssert.False(settings.UseCategoryColorsForHover);
		RegressionAssert.False(settings.UseCategoryColorsForSidebarSelection);

		settings.UseCategoryColorsForInteractions = true;
		RegressionAssert.True(settings.UseCategoryColorsForHover);
		RegressionAssert.True(settings.UseCategoryColorsForSidebarSelection);
	}

	public void IconsOnlySettingSynchronizesLegacySourceFlag()
	{
		var settings = new DivinityModManagerSettings();

		settings.UseIconsOnly = true;
		RegressionAssert.True(settings.UseSourceIconsOnly);

		settings.UseSourceIconsOnly = false;
		RegressionAssert.False(settings.UseIconsOnly);
	}

	public void CustomThemeClonePreservesUnifiedPresentationSettings()
	{
		var theme = new ReduxCustomTheme
		{
			UseCategoryColorsForHover = false,
			UseCategoryColorsForSidebarSelection = true,
			ShowCategoryIconsInPills = true,
			UseIconsOnly = true
		};

		var clone = theme.Clone();

		RegressionAssert.True(clone.UseCategoryColorsForInteractions);
		RegressionAssert.True(clone.UseCategoryColorsForHover);
		RegressionAssert.True(clone.UseCategoryColorsForSidebarSelection);
		RegressionAssert.True(clone.UseIconsOnly);
		RegressionAssert.True(clone.UseSourceIconsOnly);
	}

	public void CustomThemePreviewRegeneratesEverySemanticPillGradient()
	{
		var theme = ReduxThemeService.CreateFromBase("Gradient test", ReduxThemeType.ReduxDark);
		theme.AccentColor = "#123456";
		theme.SuccessColor = "#238A57";
		theme.WarningColor = "#C07819";
		theme.ErrorColor = "#D23A4E";
		theme.InfoColor = "#367BC0";

		var resources = new ResourceDictionary();
		foreach (var key in new[]
		{
			"ReduxAccentPillBackground", "ReduxSelectionPillBackground", "ReduxSuccessPillBackground",
			"ReduxWarningPillBackground", "ReduxErrorPillBackground", "ReduxInfoPillBackground",
			"ReduxPrimaryActionBackgroundBrush", "ReduxDestructiveActionBackgroundBrush",
			"ReduxDestructiveActionForegroundBrush"
		})
		{
			resources[key] = Brushes.Transparent;
		}

		ReduxThemeService.PreviewColors(resources, theme);

		AssertPillColor(resources, "ReduxAccentPillBackground", Color.FromRgb(0x12, 0x34, 0x56));
		AssertPillColor(resources, "ReduxSelectionPillBackground", Color.FromRgb(0x15, 0x1E, 0x30));
		AssertPillColor(resources, "ReduxSuccessPillBackground", Color.FromRgb(0x23, 0x8A, 0x57));
		AssertPillColor(resources, "ReduxWarningPillBackground", Color.FromRgb(0xC0, 0x78, 0x19));
		AssertPillColor(resources, "ReduxErrorPillBackground", Color.FromRgb(0xD2, 0x3A, 0x4E));
		AssertPillColor(resources, "ReduxInfoPillBackground", Color.FromRgb(0x36, 0x7B, 0xC0));
	}

	public void CustomThemeBackgroundEditsPreserveUntouchedBaseRoles()
	{
		var theme = ReduxThemeService.CreateFromBase("Background test", ReduxThemeType.ReduxDark);
		theme.BackgroundColor = "#0C0B10";
		var resources = new ResourceDictionary();

		ReduxThemeService.PreviewColors(resources, theme);

		AssertResourceColor(resources, "ReduxWindowColor", Color.FromRgb(0x0C, 0x0B, 0x10));
		AssertResourceColor(resources, "ReduxSurfaceElevatedColor", Color.FromRgb(0x1C, 0x16, 0x23));
		AssertResourceColor(resources, "ReduxTextPrimaryColor", Color.FromRgb(0xF2, 0xED, 0xF7));
		AssertResourceColor(resources, "ReduxTextSecondaryColor", Color.FromRgb(0xC8, 0xBD, 0xD4));
		AssertResourceColor(resources, "ReduxTextMutedColor", Color.FromRgb(0xA0, 0x94, 0xAE));
	}

	private static void AssertPillColor(ResourceDictionary resources, string key, Color expected)
	{
		var brush = resources[key] as LinearGradientBrush;
		RegressionAssert.True(brush != null);
		var actual = brush!.GradientStops[0].Color;
		RegressionAssert.Equal(expected.R, actual.R);
		RegressionAssert.Equal(expected.G, actual.G);
		RegressionAssert.Equal(expected.B, actual.B);
	}

	private static void AssertResourceColor(ResourceDictionary resources, string key, Color expected)
	{
		RegressionAssert.True(resources[key] is Color);
		var actual = (Color)resources[key];
		RegressionAssert.Equal(expected.R, actual.R);
		RegressionAssert.Equal(expected.G, actual.G);
		RegressionAssert.Equal(expected.B, actual.B);
	}

	public void LoadOrderGuidanceRequiresDiagnosticsWithoutLosingItsPreference()
	{
		var settings = new DivinityModManagerSettings
		{
			EnableModHealth = true,
			EnableLoadOrderAdvisor = true
		};
		using var modules = new ReduxModuleState(settings);

		settings.EnableModHealth = false;

		RegressionAssert.False(modules.ModDiagnosticsEnabled);
		RegressionAssert.False(modules.LoadOrderGuidanceEnabled);
		RegressionAssert.True(settings.EnableLoadOrderAdvisor);

		settings.EnableModHealth = true;

		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.True(modules.LoadOrderGuidanceEnabled);
	}

	public void DisposedModuleStateStopsTrackingSettings()
	{
		var settings = new DivinityModManagerSettings
		{
			EnableModHealth = true,
			EnableLoadOrderAdvisor = false,
			LocalOnlyMode = false
		};
		var modules = new ReduxModuleState(settings);

		modules.Dispose();
		settings.LocalOnlyMode = true;
		settings.EnableModHealth = false;
		settings.EnableLoadOrderAdvisor = true;

		RegressionAssert.True(modules.SourceIntegrationsEnabled);
		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.False(modules.LoadOrderGuidanceEnabled);
	}

	public void DisabledNexusProviderCannotInitializeItsClient()
	{
		NexusModsDataLoader.Dispose();
		var provider = new NexusModsCacheHandler
		{
			IsEnabled = false,
			APIKey = "unused-test-key",
			AppName = "Redux regression tests",
			AppVersion = "0"
		};

		var changed = provider.Update(Array.Empty<DivinityModData>(), CancellationToken.None)
			.GetAwaiter()
			.GetResult();

		RegressionAssert.False(changed);
		RegressionAssert.False(NexusModsDataLoader.IsInitialized);
	}
}
