using DivinityModManager.AppServices;
using DivinityModManager.Models;
using Newtonsoft.Json;
using System;

namespace Redux.Core.Tests;

public sealed class ScriptExtenderDetectionPathTests
{
	public void DefaultExtenderDetectionFollowsTheBg3Profile()
	{
		var result = ScriptExtenderDetectionPathResolver.Resolve(null, "",
			@"C:\GamePrefix\AppData\Local\Larian Studios\", @"C:\ManagerPrefix\AppData\Local");
		RegressionAssert.Equal(@"C:\GamePrefix\AppData\Local\BG3ScriptExtender", result.Folder);
		RegressionAssert.Equal("BG3 profile's Local AppData", result.Source);
		RegressionAssert.True(result.Error == null);
		var fallback = ScriptExtenderDetectionPathResolver.Resolve(null, null, null, @"C:\ManagerPrefix\AppData\Local");
		RegressionAssert.Equal(@"C:\ManagerPrefix\AppData\Local\BG3ScriptExtender", fallback.Folder);
		RegressionAssert.Equal("Windows Local AppData", fallback.Source);
	}

	public void ConfiguredExtenderCacheIsUsedWithoutAppendingAnotherCacheFolder()
	{
		var result = ScriptExtenderDetectionPathResolver.Resolve(" ", @"D:\SE Cache\Versions\..\CustomCache",
			@"C:\GamePrefix\AppData\Local\Larian Studios", @"C:\ManagerPrefix\AppData\Local");
		RegressionAssert.Equal(@"D:\SE Cache\CustomCache", result.Folder);
		RegressionAssert.Equal("configured updater CachePath", result.Source);
		RegressionAssert.True(result.Error == null);
	}

	public void DetectionOverrideIsPersistedSeparatelyFromTheUpdaterCache()
	{
		var settings = new DivinityModManagerSettings { ScriptExtenderDetectionPathOverride = @"Z:\home\player\game-prefix\cache" };
		settings.ExtenderUpdaterSettings.CachePath = @"C:\UpdaterCache";
		var result = ScriptExtenderDetectionPathResolver.Resolve(settings.ScriptExtenderDetectionPathOverride,
			settings.ExtenderUpdaterSettings.CachePath, null, null);
		RegressionAssert.Equal(@"Z:\home\player\game-prefix\cache", result.Folder);
		RegressionAssert.Equal("Redux detection-folder override", result.Source);
		RegressionAssert.Equal(@"C:\UpdaterCache", settings.ExtenderUpdaterSettings.CachePath);
		var restored = JsonConvert.DeserializeObject<DivinityModManagerSettings>(JsonConvert.SerializeObject(settings))!;
		RegressionAssert.Equal(settings.ScriptExtenderDetectionPathOverride, restored.ScriptExtenderDetectionPathOverride);
		RegressionAssert.False(JsonConvert.SerializeObject(settings.ExtenderUpdaterSettings)
			.Contains(nameof(DivinityModManagerSettings.ScriptExtenderDetectionPathOverride), StringComparison.Ordinal));
	}

	public void InvalidExplicitExtenderPathsDoNotFallBackToAnotherInstallation()
	{
		foreach (var invalid in new[] { "relative-cache", @"C:relative-cache", @"\root-relative", "C:\\bad\0cache", @"C:\bad*cache" })
		{
			var configured = ScriptExtenderDetectionPathResolver.Resolve(null, invalid,
				@"C:\Profile\Larian Studios", @"C:\Local");
			RegressionAssert.True(configured.Folder == null);
			RegressionAssert.True(configured.Error!.Contains("configured updater CachePath", StringComparison.Ordinal));
			RegressionAssert.True(configured.Error.Contains("Preferences > General > Paths and storage", StringComparison.Ordinal));
			var overridden = ScriptExtenderDetectionPathResolver.Resolve(invalid, @"C:\ValidCache",
				@"C:\Profile\Larian Studios", @"C:\Local");
			RegressionAssert.True(overridden.Folder == null);
			RegressionAssert.True(overridden.Error!.Contains("Redux detection-folder override", StringComparison.Ordinal));
		}
	}

	public void ExtenderPathResolutionDoesNotRequireAnExistingFolder()
	{
		// These are only strings: resolution must not create or inspect user directories.
		var missing = ScriptExtenderDetectionPathResolver.Resolve(@"X:\Redux-test-only\nonexistent-cache", null, null, null);
		RegressionAssert.Equal(@"X:\Redux-test-only\nonexistent-cache", missing.Folder);
		RegressionAssert.True(missing.Error == null);
		var absent = ScriptExtenderDetectionPathResolver.Resolve(null, null, "invalid\0profile", null);
		RegressionAssert.True(absent.Folder == null);
		RegressionAssert.True(absent.Error!.Contains("could not locate Local AppData", StringComparison.Ordinal));
	}
}
