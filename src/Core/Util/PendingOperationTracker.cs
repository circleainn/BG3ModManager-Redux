namespace DivinityModManager.Util;

/// <summary>Waits for owned operations to finish cleanup before application shutdown.</summary>
public sealed class PendingOperationTracker
{
	private readonly object _gate = new();
	private readonly HashSet<Registration> _operations = [];
	private bool _stopping;

	public IDisposable? TryRegister(Action requestCancellation)
	{
		ArgumentNullException.ThrowIfNull(requestCancellation);
		lock (_gate)
		{
			if (_stopping) return null;
			var registration = new Registration(this, requestCancellation);
			_operations.Add(registration);
			return registration;
		}
	}

	public async Task StopAsync()
	{
		Registration[] pending;
		lock (_gate)
		{
			_stopping = true;
			pending = _operations.ToArray();
		}
		List<Exception>? failures = null;
		foreach (var registration in pending)
		{
			try { registration.RequestCancellation(); }
			catch (Exception exception) { (failures ??= []).Add(exception); }
		}
		// Cancellation is a request. An operation releases its registration only
		// after temporary files, commits and its UI completion callback are finished.
		await Task.WhenAll(pending.Select(registration => registration.Completion)).ConfigureAwait(false);
		if (failures != null) throw new AggregateException("Could not request cancellation of an active operation.", failures);
	}

	public void ResumeAfterFailedShutdown()
	{
		lock (_gate)
		{
			if (_operations.Count != 0)
				throw new InvalidOperationException("Operations must finish cleanup before shutdown can be abandoned.");
			_stopping = false;
		}
	}

	private sealed class Registration(PendingOperationTracker owner, Action requestCancellation) : IDisposable
	{
		private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _cancellationRequested;
		private int _disposed;
		public Task Completion => _completion.Task;

		public void RequestCancellation()
		{
			if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _cancellationRequested, 1) != 0) return;
			try { requestCancellation(); }
			// The operation may have disposed its token just before releasing its
			// registration. Its completion below still controls shutdown readiness.
			catch (ObjectDisposedException) { }
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
			lock (owner._gate) owner._operations.Remove(this);
			_completion.TrySetResult();
		}
	}
}
