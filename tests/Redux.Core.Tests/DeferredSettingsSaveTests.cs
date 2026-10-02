using System;
using System.Reactive.Concurrency;

using DivinityModManager.Models;
using DivinityModManager.Util;

using Newtonsoft.Json;

namespace Redux.Core.Tests;

internal sealed class DeferredSettingsSaveTests
{
	public void RefreshFlushPreservesRecentInactiveOrganizationAndAllowsLaterDiskReload()
	{
		var scheduler = new HistoricalScheduler();
		var live = new DivinityModManagerSettings {
			InactiveModOrder = ["first", "second"],
			VisualModListDividers = [new ModListVisualDividerData {
				Id = "inactive", IsActiveList = false, Title = "Original", Position = 0,
				MemberModUuids = ["first", "second"]
			}]
		};
		var diskJson = JsonConvert.SerializeObject(live);
		var saves = 0;
		var deferred = new DeferredSettingsSave(scheduler, () => {
			saves++;
			diskJson = JsonConvert.SerializeObject(live);
			return true;
		});
		live.InactiveModOrder = ["second", "first"];
		live.VisualModListDividers[0].Title = "Recently edited";
		live.VisualModListDividers[0].IsCollapsed = true;
		deferred.Queue(TimeSpan.FromMilliseconds(750));
		scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100));

		RegressionAssert.True(deferred.TryFlush());
		live.RestorePersistedSettings(JsonConvert.DeserializeObject<DivinityModManagerSettings>(diskJson)!);
		RegressionAssert.SequenceEqual(["second", "first"], live.InactiveModOrder);
		RegressionAssert.Equal("Recently edited", live.VisualModListDividers[0].Title);
		RegressionAssert.True(live.VisualModListDividers[0].IsCollapsed);
		scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
		RegressionAssert.Equal(1, saves);

		// A refresh with no pending local write still reads changes made on disk.
		diskJson = "{\"InactiveModOrder\":[\"external\"]}";
		RegressionAssert.True(deferred.TryFlush());
		live.RestorePersistedSettings(JsonConvert.DeserializeObject<DivinityModManagerSettings>(diskJson)!);
		RegressionAssert.SequenceEqual(["external"], live.InactiveModOrder);
		RegressionAssert.Equal(1, saves);
	}

	public void FailedRefreshFlushKeepsItsScheduledSaveRetry()
	{
		var scheduler = new HistoricalScheduler();
		var allowSave = false;
		var attempts = 0;
		var deferred = new DeferredSettingsSave(scheduler, () => {
			attempts++;
			return allowSave;
		});
		deferred.Queue(TimeSpan.FromMilliseconds(250));
		scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100));

		RegressionAssert.False(deferred.TryFlush());
		RegressionAssert.True(deferred.HasPendingSave);
		RegressionAssert.Equal(1, attempts);
		allowSave = true;
		scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150));
		RegressionAssert.Equal(2, attempts);
		RegressionAssert.False(deferred.HasPendingSave);
	}

	public void FailedDelayedSaveRemainsPendingUntilExplicitRetrySucceeds()
	{
		var scheduler = new HistoricalScheduler();
		var allowSave = false;
		var attempts = 0;
		var deferred = new DeferredSettingsSave(scheduler, () => {
			attempts++;
			return allowSave;
		});
		deferred.Queue(TimeSpan.FromMilliseconds(250));
		scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
		RegressionAssert.True(deferred.HasPendingSave);
		RegressionAssert.Equal(1, attempts);

		allowSave = true;
		RegressionAssert.True(deferred.TryFlush());
		RegressionAssert.False(deferred.HasPendingSave);
		RegressionAssert.Equal(2, attempts);
	}

	public void CancellingAndReplacingQueuedSaveWritesOnlyTheCurrentState()
	{
		var scheduler = new HistoricalScheduler();
		var value = "discarded";
		var persisted = String.Empty;
		var saves = 0;
		var deferred = new DeferredSettingsSave(scheduler, () => {
			saves++;
			persisted = value;
			return true;
		});
		deferred.Queue(TimeSpan.FromMilliseconds(250));
		deferred.Cancel();
		scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
		RegressionAssert.Equal(0, saves);
		value = "restored";
		deferred.Queue(TimeSpan.FromMilliseconds(250));
		scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
		RegressionAssert.Equal(1, saves);
		RegressionAssert.Equal("restored", persisted);
	}
}
