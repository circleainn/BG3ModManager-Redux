using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.ViewModels;

using DynamicData;
using ReactiveUI;

namespace Redux.Core.Tests;

public sealed class ModSettingsReadIntegrationTests
{
	private const string ModUuid = "11111111-1111-1111-1111-111111111111";

	public void FailedGameOrderReadPreservesVerifiedStateUntilACompleteReadSucceeds()
	{
		using var fixture = new ViewModelFixture();
		var profile = fixture.SelectProfile("Public");
		var verified = ModSettingsParseResults.Success([new DivinityProfileActiveModData { UUID = ModUuid }], "verified-hash");
		fixture.Record(profile, verified);
		RegressionAssert.True(ReferenceEquals(verified, profile.ModSettingsReadResult));
		RegressionAssert.Equal(1, profile.ActiveMods.Count);
		RegressionAssert.Equal(ModUuid, profile.ActiveMods[0].UUID);

		var failed = ModSettingsParseResults.Failed("A writer temporarily owns the file.");
		fixture.Record(profile, failed);
		RegressionAssert.True(ReferenceEquals(failed, profile.ModSettingsReadResult));
		RegressionAssert.Equal(1, profile.ActiveMods.Count);
		RegressionAssert.Equal(ModUuid, profile.ActiveMods[0].UUID);
		RegressionAssert.True(fixture.Get<bool>("_hasKnownGameOrder"));
		RegressionAssert.SequenceEqual(new[] { ModUuid }, fixture.Get<IReadOnlyList<string>>("_lastKnownGameOrderUuids"));
		fixture.Invoke("UpdateExportStatus");
		RegressionAssert.False(fixture.ViewModel.IsCurrentOrderExportedToGame);
		RegressionAssert.Contains(fixture.ViewModel.ExportToGameStatusText, "could not read modsettings.lsx");

		var empty = ModSettingsParseResults.Success([], "verified-empty-hash");
		fixture.Record(profile, empty);
		RegressionAssert.True(ReferenceEquals(empty, profile.ModSettingsReadResult));
		RegressionAssert.True(fixture.Get<bool>("_hasKnownGameOrder"));
		RegressionAssert.False(fixture.Get<bool>("_gameOrderReadFailed"));
		RegressionAssert.Equal(0, fixture.Get<IReadOnlyList<string>>("_lastKnownGameOrderUuids").Count);
		RegressionAssert.Equal(0, profile.ActiveMods.Count);
		fixture.Record(profile, verified);
		RegressionAssert.Equal(1, profile.ActiveMods.Count);
		fixture.Record(profile, ModSettingsParseResults.Missing());
		RegressionAssert.False(fixture.Get<bool>("_hasKnownGameOrder"));
		RegressionAssert.Equal(ModSettingsReadStatus.Missing, profile.ModSettingsReadResult.Status);
		RegressionAssert.Equal(0, profile.ActiveMods.Count);
	}

	public void FailedReadForAnotherProfileCannotReuseOrOverwriteThePreviousProfileState()
	{
		using var fixture = new ViewModelFixture();
		var first = fixture.SelectProfile("First");
		var verified = ModSettingsParseResults.Success([new DivinityProfileActiveModData { UUID = ModUuid }]);
		fixture.Record(first, verified);
		var second = fixture.SelectProfile("Second");
		var failed = ModSettingsParseResults.Failed("The second profile is unreadable.");
		fixture.Record(second, failed);
		RegressionAssert.False(fixture.Get<bool>("_hasKnownGameOrder"));
		RegressionAssert.Equal(0, fixture.Get<IReadOnlyList<string>>("_lastKnownGameOrderUuids").Count);
		RegressionAssert.Equal(second.ModSettingsFile, fixture.Get<string>("_lastKnownGameOrderPath"));

		// A delayed completion from the old profile must not refresh the selected one.
		fixture.Record(first, verified);
		RegressionAssert.True(ReferenceEquals(failed, second.ModSettingsReadResult));
		RegressionAssert.True(fixture.Get<bool>("_gameOrderReadFailed"));
		RegressionAssert.Equal(second.ModSettingsFile, fixture.Get<string>("_lastKnownGameOrderPath"));
	}

	private sealed class ViewModelFixture : IDisposable
	{
		private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
		private readonly IScheduler _previousScheduler = RxApp.MainThreadScheduler;
		private readonly SourceCache<DivinityModData, string> _mods = new(mod => mod.UUID);
		private ObservableAsPropertyHelper<DivinityProfileData>? _profile;
		public MainWindowViewModel ViewModel { get; }

		public ViewModelFixture()
		{
			// No constructor, discovery, settings, watcher, or game-directory I/O runs.
			// Scheduling is suppressed; the failed-read status path is invoked explicitly.
			RxApp.MainThreadScheduler = new SuspendedScheduler();
			ViewModel = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
			Set("mods", _mods);
			Set("_lastKnownGameOrderUuids", Array.Empty<string>());
		}

		public DivinityProfileData SelectProfile(string name)
		{
			_profile?.Dispose();
			var profile = new DivinityProfileData(name, Path.Combine(Path.GetTempPath(), "ReduxReadIntegrationFixture", name, "modsettings.lsx"));
			_profile = new ObservableAsPropertyHelper<DivinityProfileData>(Observable.Return(profile), _ => { }, profile,
				scheduler: ImmediateScheduler.Instance);
			Set("_selectedProfile", _profile);
			return profile;
		}

		public void Record(DivinityProfileData profile, ModSettingsParseResults result) =>
			Invoke("RecordGameOrderRead", profile, result);

		public void Invoke(string method, params object[] arguments) =>
			typeof(MainWindowViewModel).GetMethod(method, PrivateInstance)!.Invoke(ViewModel, arguments);

		public T Get<T>(string field) => (T)typeof(MainWindowViewModel).GetField(field, PrivateInstance)!.GetValue(ViewModel)!;
		private void Set(string field, object value) => typeof(MainWindowViewModel).GetField(field, PrivateInstance)!.SetValue(ViewModel, value);

		public void Dispose()
		{
			_profile?.Dispose();
			_mods.Dispose();
			RxApp.MainThreadScheduler = _previousScheduler;
		}
	}

	private sealed class SuspendedScheduler : IScheduler
	{
		public DateTimeOffset Now => DateTimeOffset.UtcNow;
		public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action) => Disposable.Empty;
		public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action) => Disposable.Empty;
		public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action) => Disposable.Empty;
	}
}
