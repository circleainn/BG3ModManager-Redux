using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DivinityModManager.Models;
using DivinityModManager.ViewModels;
using ReactiveUI;

namespace Redux.Core.Tests;

public sealed class ExtenderConfigReloadTests
{
	public void RemovedExtenderUpdaterConfigClearsStaleVersionPinAndCache()
	{
		using var fixture = new Fixture();
		File.WriteAllText(fixture.Config, "{\"TargetVersion\":\"20.0.0.0\",\"CachePath\":\"C:\\\\OldCache\",\"DisableUpdates\":true}");
		fixture.Reload();
		var config = fixture.Settings.ExtenderUpdaterSettings;
		RegressionAssert.Equal("20.0.0.0", config.TargetVersion);
		RegressionAssert.True(config.DisableUpdates);
		File.Delete(fixture.Config);
		fixture.Reload();
		RegressionAssert.True(ReferenceEquals(config, fixture.Settings.ExtenderUpdaterSettings));
		RegressionAssert.Equal("", config.TargetVersion);
		RegressionAssert.Equal("", config.CachePath);
		RegressionAssert.False(config.DisableUpdates);
		RegressionAssert.Equal(fixture.Directory, fixture.Settings.ScriptExtenderDetectionPathOverride);
		RegressionAssert.False(File.Exists(fixture.Config));
	}

	public void ExistingExtenderVersionPinIsRespectedAndUnreadableConfigIsPreserved()
	{
		using var fixture = new Fixture();
		const string json = "{\"TargetVersion\":\"20.0.0.0\",\"DisableUpdates\":true}";
		File.WriteAllText(fixture.Config, json);
		fixture.Reload();
		using (var locked = new FileStream(fixture.Config, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
		{
			fixture.Reload();
			RegressionAssert.Equal("20.0.0.0", fixture.Settings.ExtenderUpdaterSettings.TargetVersion);
			RegressionAssert.True(fixture.Settings.ExtenderUpdaterSettings.DisableUpdates);
		}
		RegressionAssert.Equal(json, File.ReadAllText(fixture.Config));
		File.WriteAllText(fixture.Config, "null");
		fixture.Reload();
		RegressionAssert.Equal("20.0.0.0", fixture.Settings.ExtenderUpdaterSettings.TargetVersion);
		File.WriteAllText(fixture.Config, "{\"TargetVersion\":\"33.0.0.0\"}");
		fixture.Reload();
		RegressionAssert.Equal("33.0.0.0", fixture.Settings.ExtenderUpdaterSettings.TargetVersion);
		RegressionAssert.False(fixture.Settings.ExtenderUpdaterSettings.DisableUpdates);
	}

	public void SwitchingGameInstallationsDoesNotReuseAnOldExtenderPin()
	{
		using var fixture = new Fixture();
		File.WriteAllText(fixture.Config, "{\"TargetVersion\":\"20.0.0.0\"}");
		fixture.Reload();
		var other = Path.Combine(fixture.Directory, "OtherGame");
		System.IO.Directory.CreateDirectory(other);
		var executable = Path.Combine(other, "bg3.exe");
		File.WriteAllText(executable, "Test fixture only; never executed.");
		fixture.Settings.GameExecutablePath = executable;
		fixture.Reload();
		RegressionAssert.Equal("", fixture.Settings.ExtenderUpdaterSettings.TargetVersion);
		RegressionAssert.True(File.ReadAllText(fixture.Config).Contains("20.0.0.0", StringComparison.Ordinal));
		RegressionAssert.False(File.Exists(Path.Combine(other, "ScriptExtenderUpdaterConfig.json")));
		fixture.Settings.GameExecutablePath = Path.Combine(fixture.Directory, "bg3.exe");
		fixture.Reload();
		RegressionAssert.Equal("20.0.0.0", fixture.Settings.ExtenderUpdaterSettings.TargetVersion);
	}

	private sealed class Fixture : IDisposable
	{
		public string Directory { get; } = Path.Combine(Path.GetTempPath(), "ReduxExtenderReload-" + Guid.NewGuid().ToString("N"));
		public string Config => Path.Combine(Directory, "ScriptExtenderUpdaterConfig.json");
		public DivinityModManagerSettings Settings { get; }
		private readonly MainWindowViewModel _model;
		private readonly IScheduler _scheduler = RxApp.MainThreadScheduler;
		public Fixture()
		{
			System.IO.Directory.CreateDirectory(Directory);
			var executable = Path.Combine(Directory, "bg3.exe");
			File.WriteAllText(executable, "Test fixture only; never executed.");
			Settings = new() { GameExecutablePath = executable, ScriptExtenderDetectionPathOverride = Directory };
			_model = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
			typeof(MainWindowViewModel).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_model, Settings);
			typeof(MainWindowViewModel).GetProperty("PathwayData")!.SetValue(_model, new DivinityPathwayData());
			RxApp.MainThreadScheduler = ImmediateScheduler.Instance;
		}
		public void Reload() => ((Task)typeof(MainWindowViewModel)
			.GetMethod("LoadExtenderSettingsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
			.Invoke(_model, [CancellationToken.None])!).GetAwaiter().GetResult();
		public void Dispose()
		{
			RxApp.MainThreadScheduler = _scheduler;
			System.IO.Directory.Delete(Directory, true);
		}
	}
}
