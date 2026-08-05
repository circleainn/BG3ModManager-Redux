namespace DivinityModManager.Util;

/// <summary>
/// Defers keyed, synchronous startup notices until the main window explicitly
/// reports that it is visible and ready. Re-enqueuing a key replaces its data
/// without changing deterministic presentation order.
/// </summary>
public sealed class StartupNotificationQueue
{
	private readonly List<string> _order = new();
	private readonly Dictionary<string, Action> _pending = new(StringComparer.Ordinal);
	private bool _isDraining;

	public bool IsReady { get; private set; }
	public int PendingCount => _pending.Count;

	public void EnqueueOrRun(string key, Action notification)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(notification);

		if (IsReady && !_isDraining)
		{
			notification();
			return;
		}

		if (!_pending.ContainsKey(key))
		{
			_order.Add(key);
		}
		_pending[key] = notification;
	}

	public bool Cancel(string key)
	{
		if (String.IsNullOrWhiteSpace(key) || !_pending.Remove(key)) return false;
		_order.Remove(key);
		return true;
	}

	public void MarkReadyAndDrain()
	{
		if (IsReady) return;
		IsReady = true;
		_isDraining = true;
		try
		{
			while (_order.Count > 0)
			{
				var key = _order[0];
				_order.RemoveAt(0);
				if (!_pending.Remove(key, out var notification)) continue;
				notification();
			}
		}
		finally
		{
			_isDraining = false;
		}
	}
}
