namespace DivinityModManager.Util;

/// <summary>Tracks settings waiting for a save on the owning UI scheduler.</summary>
public sealed class DeferredSettingsSave
{
	private readonly IScheduler _scheduler;
	private readonly Func<bool> _save;
	private readonly SerialDisposable _scheduled = new();

	public bool HasPendingSave { get; private set; }

	public DeferredSettingsSave(IScheduler scheduler, Func<bool> save)
	{
		_scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
		_save = save ?? throw new ArgumentNullException(nameof(save));
	}

	public void Queue(TimeSpan delay)
	{
		HasPendingSave = true;
		var scheduled = new SingleAssignmentDisposable();
		_scheduled.Disposable = scheduled;
		scheduled.Disposable = _scheduler.Schedule(delay < TimeSpan.Zero ? TimeSpan.Zero : delay,
			() => TryFlush());
	}

	public bool TryFlush()
	{
		if (!HasPendingSave) return true;
		// A failed early flush must leave the existing delayed attempt available.
		// A failed delayed attempt remains pending for the next explicit retry.
		if (!_save()) return false;
		Cancel();
		return true;
	}

	public void Cancel()
	{
		HasPendingSave = false;
		_scheduled.Disposable = null;
	}
}
