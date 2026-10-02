using System;

using DivinityModManager.Models;
using DivinityModManager.Util;

using Newtonsoft.Json;

namespace Redux.Core.Tests;

internal sealed class SettingsRestoreTests
{
	public void StartupRestoresSavedWindowAndExplicitAppearanceChoices()
	{
		var saved = new DivinityModManagerSettings {
			SaveWindowLocation = true,
			Window = new WindowSettings {
				X = 320, Y = 180, Width = 1360, Height = 820, Screen = 1, Maximized = true
			},
			ColorTheme = ReduxThemeType.Parchment,
			UseGeneratedGradientsPreference = true,
			TypographyFont = ReduxTypographyFont.Manrope,
			UseThemeDefaultTypographyPreference = false
		};
		var live = new DivinityModManagerSettings();

		live.RestorePersistedSettings(RoundTrip(saved));

		RegressionAssert.True(live.SaveWindowLocation);
		RegressionAssert.Equal(320d, live.Window.X);
		RegressionAssert.Equal(180d, live.Window.Y);
		RegressionAssert.Equal(1360d, live.Window.Width);
		RegressionAssert.Equal(820d, live.Window.Height);
		RegressionAssert.Equal(1, live.Window.Screen);
		RegressionAssert.True(live.Window.Maximized);
		RegressionAssert.True(live.UsesGeneratedGradients);
		RegressionAssert.False(live.UseThemeDefaultTypography);
		RegressionAssert.Equal(ReduxTypographyFont.Manrope, ReduxTypographyService.ResolveSelection(live).Font);

		// Startup saves the live settings again before revealing the main window.
		var savedAgain = RoundTrip(live);
		RegressionAssert.Equal(1360d, savedAgain.Window.Width);
		RegressionAssert.Equal(true, savedAgain.UseGeneratedGradientsPreference);
		RegressionAssert.Equal(false, savedAgain.UseThemeDefaultTypographyPreference);
	}

	public void ReloadClearsInheritedAppearanceOverridesAndKeepsExtenderInstances()
	{
		var live = new DivinityModManagerSettings {
			UseGeneratedGradientsPreference = true,
			UseThemeDefaultTypographyPreference = false
		};
		var extender = live.ExtenderSettings;
		var updater = live.ExtenderUpdaterSettings;
		var saved = new DivinityModManagerSettings {
			ColorTheme = ReduxThemeType.Parchment,
			TypographyFont = ReduxTypographyFont.Manrope
		};
		saved.ExtenderSettings.CreateConsole = true;
		saved.ExtenderUpdaterSettings.DisableUpdates = true;

		live.RestorePersistedSettings(RoundTrip(saved));

		RegressionAssert.True(live.UseGeneratedGradientsPreference == null);
		RegressionAssert.True(live.UseThemeDefaultTypographyPreference == null);
		RegressionAssert.False(live.UsesGeneratedGradients);
		RegressionAssert.True(live.UseThemeDefaultTypography);
		RegressionAssert.Equal(ReduxTypographyFont.SegoeUI, ReduxTypographyService.ResolveSelection(live).Font);
		RegressionAssert.True(ReferenceEquals(extender, live.ExtenderSettings));
		RegressionAssert.True(ReferenceEquals(updater, live.ExtenderUpdaterSettings));
		RegressionAssert.True(extender.CreateConsole);
		RegressionAssert.True(updater.DisableUpdates);
	}

	public void MissingWindowPlacementRestoresUsableDefaults()
	{
		var saved = JsonConvert.DeserializeObject<DivinityModManagerSettings>("{\"Window\":null}")!;
		var live = new DivinityModManagerSettings();
		live.Window.Width = 1360;

		live.RestorePersistedSettings(saved);

		RegressionAssert.True(live.Window != null);
		RegressionAssert.Equal(-1d, live.Window!.Width);
		RegressionAssert.Equal(-1d, live.Window.Height);
		RegressionAssert.Equal(-1d, live.Window.X);
		RegressionAssert.Equal(-1d, live.Window.Y);
		RegressionAssert.False(live.Window.Maximized);
	}

	private static DivinityModManagerSettings RoundTrip(DivinityModManagerSettings settings)
	{
		var serializer = new JsonSerializerSettings {
			DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
			MissingMemberHandling = MissingMemberHandling.Ignore
		};
		return JsonConvert.DeserializeObject<DivinityModManagerSettings>(
			JsonConvert.SerializeObject(settings, serializer), serializer)!;
	}
}
