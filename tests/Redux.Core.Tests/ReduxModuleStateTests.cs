using System;
using System.Threading;

using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.ModUpdater.Cache;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class ReduxModuleStateTests
{
	public void DefaultsKeepHealthOnAndExperimentalFeaturesOptIn()
	{
		var settings = new DivinityModManagerSettings();
		using var modules = new ReduxModuleState(settings);

		RegressionAssert.True(modules.SourceIntegrationsEnabled);
		RegressionAssert.True(modules.ModHealthEnabled);
		RegressionAssert.False(modules.LoadOrderAdvisorEnabled);
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
		RegressionAssert.True(modules.ModHealthEnabled);
		RegressionAssert.True(modules.LoadOrderAdvisorEnabled);

		settings.LocalOnlyMode = false;

		RegressionAssert.True(modules.SourceIntegrationsEnabled);
		RegressionAssert.True(modules.ModHealthEnabled);
		RegressionAssert.True(modules.LoadOrderAdvisorEnabled);
	}

	public void AdvisorRequiresHealthWithoutLosingItsPreference()
	{
		var settings = new DivinityModManagerSettings
		{
			EnableModHealth = true,
			EnableLoadOrderAdvisor = true
		};
		using var modules = new ReduxModuleState(settings);

		settings.EnableModHealth = false;

		RegressionAssert.False(modules.ModHealthEnabled);
		RegressionAssert.False(modules.LoadOrderAdvisorEnabled);
		RegressionAssert.True(settings.EnableLoadOrderAdvisor);

		settings.EnableModHealth = true;

		RegressionAssert.True(modules.ModHealthEnabled);
		RegressionAssert.True(modules.LoadOrderAdvisorEnabled);
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
		RegressionAssert.True(modules.ModHealthEnabled);
		RegressionAssert.False(modules.LoadOrderAdvisorEnabled);
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
