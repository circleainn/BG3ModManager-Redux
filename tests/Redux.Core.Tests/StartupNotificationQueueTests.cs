using DivinityModManager.Util;

using System.Collections.Generic;

namespace Redux.Core.Tests;

public sealed class StartupNotificationQueueTests
{
	public void StartupNotificationsWaitForReadinessAndDrainInOrder()
	{
		var queue = new StartupNotificationQueue();
		var shown = new List<string>();
		queue.EnqueueOrRun("missing-mods", () => shown.Add("missing-mods"));
		queue.EnqueueOrRun("welcome", () => shown.Add("welcome"));

		RegressionAssert.Equal(0, shown.Count);
		RegressionAssert.Equal(2, queue.PendingCount);

		queue.MarkReadyAndDrain();

		RegressionAssert.SequenceEqual(new[] { "missing-mods", "welcome" }, shown);
		RegressionAssert.Equal(0, queue.PendingCount);
	}

	public void RepeatedStartupNotificationUsesLatestDataExactlyOnce()
	{
		var queue = new StartupNotificationQueue();
		var shown = new List<string>();
		queue.EnqueueOrRun("load-order-warning", () => shown.Add("stale"));
		queue.EnqueueOrRun("other", () => shown.Add("other"));
		queue.EnqueueOrRun("load-order-warning", () => shown.Add("latest"));

		queue.MarkReadyAndDrain();

		RegressionAssert.SequenceEqual(new[] { "latest", "other" }, shown);
	}

	public void StaleStartupNotificationCanBeCancelledBeforeReadiness()
	{
		var queue = new StartupNotificationQueue();
		var shown = false;
		queue.EnqueueOrRun("load-order-warning", () => shown = true);

		RegressionAssert.True(queue.Cancel("load-order-warning"));
		queue.MarkReadyAndDrain();

		RegressionAssert.False(shown);
	}

	public void NotificationsQueuedDuringDrainRemainSequential()
	{
		var queue = new StartupNotificationQueue();
		var shown = new List<string>();
		queue.EnqueueOrRun("first", () =>
		{
			shown.Add("first");
			queue.EnqueueOrRun("third", () => shown.Add("third"));
		});
		queue.EnqueueOrRun("second", () => shown.Add("second"));

		queue.MarkReadyAndDrain();

		RegressionAssert.SequenceEqual(new[] { "first", "second", "third" }, shown);
		queue.EnqueueOrRun("after-ready", () => shown.Add("after-ready"));
		RegressionAssert.SequenceEqual(new[] { "first", "second", "third", "after-ready" }, shown);
	}
}
