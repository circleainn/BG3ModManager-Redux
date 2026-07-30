using System;
using System.Threading;

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
