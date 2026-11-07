using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Extender;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using Newtonsoft.Json;

namespace Redux.Core.Tests;

public sealed class UserPreferencePreservationTests
{
	public void ManifestRefreshPreservesExactAndUnavailablePins()
	{
		var config = new ScriptExtenderUpdateConfig { TargetVersion = "33.0.0.0", TargetResourceDigest = "saved", DisableUpdates = true };
		var latest = new ScriptExtenderUpdateVersion();
		var other = new ScriptExtenderUpdateVersion { Version = config.TargetVersion, Digest = "other" };
		var missing = ExtenderVersionSelection.Resolve(config, [other], latest);
		RegressionAssert.Equal("saved", missing.Digest);
		RegressionAssert.Equal("33.0.0.0", missing.Version);
		RegressionAssert.False(ReferenceEquals(missing, other));
		var exact = new ScriptExtenderUpdateVersion { Version = config.TargetVersion, Digest = "saved" };
		RegressionAssert.True(ReferenceEquals(exact, ExtenderVersionSelection.Resolve(config, [other, exact], latest)));
		RegressionAssert.True(config.DisableUpdates);
		config.TargetResourceDigest = "";
		ExtenderVersionSelection.Resolve(config, [other], latest);
		RegressionAssert.Equal("", config.TargetResourceDigest);
		config.TargetVersion = "";
		RegressionAssert.True(ReferenceEquals(latest, ExtenderVersionSelection.Resolve(config, [other], latest)));
	}

	public void RefreshSelectionCannotWritePinsButExplicitLatestCan()
	{
		var model = (SettingsWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsWindowViewModel));
		var config = new ScriptExtenderUpdateConfig { TargetVersion = "33", TargetResourceDigest = "saved", DisableUpdates = true };
		typeof(SettingsWindowViewModel).GetProperty("ExtenderUpdaterSettings")!.SetValue(model, config);
		var latest = new ScriptExtenderUpdateVersion();
		typeof(SettingsWindowViewModel).GetField("_emptyVersion", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, latest);
		var refreshing = typeof(SettingsWindowViewModel).GetField("_refreshingExtenderVersions", BindingFlags.Instance | BindingFlags.NonPublic)!;
		refreshing.SetValue(model, true);
		model.OnTargetVersionSelected(latest);
		RegressionAssert.Equal("saved", config.TargetResourceDigest);
		refreshing.SetValue(model, false);
		model.OnTargetVersionSelected(latest);
		RegressionAssert.Equal("", config.TargetResourceDigest);
		RegressionAssert.Equal("", config.TargetVersion);
		RegressionAssert.True(config.DisableUpdates);
	}

	public void ColumnOrderRoundTripPreservesHiddenSlotsAndPaneIndependence()
	{
		string[] defaults = ["Name", "Author", "Version", "Category"];
		var normalized = ModColumnOrder.Normalize(["Version", "Version", "Unknown", "Name"], defaults);
		RegressionAssert.Equal("Version,Name,Author,Category", String.Join(",", normalized));
		var reordered = ModColumnOrder.MergeVisible(normalized, ["Category", "Version", "Name"], defaults);
		RegressionAssert.Equal("Category,Version,Author,Name", String.Join(",", reordered));
		var settings = new DivinityModManagerSettings();
		settings.ModListColumnOrders["Active"] = reordered;
		settings.ModListColumnOrders["Inactive"] = defaults.ToList();
		var restored = JsonConvert.DeserializeObject<DivinityModManagerSettings>(JsonConvert.SerializeObject(settings))!;
		RegressionAssert.Equal("Category,Version,Author,Name", String.Join(",", restored.ModListColumnOrders["Active"]));
		RegressionAssert.Equal("Name,Author,Version,Category", String.Join(",", restored.ModListColumnOrders["Inactive"]));
		var live = new DivinityModManagerSettings();
		live.RestorePersistedSettings(restored);
		RegressionAssert.Equal("Category,Version,Author,Name", String.Join(",", live.ModListColumnOrders["Active"]));
	}
}
