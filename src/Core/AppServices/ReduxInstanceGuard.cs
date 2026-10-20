using System.Security.Principal;
using System.Diagnostics;
using System;
using System.Threading;

#if REDUX_UPDATER
namespace ReduxUpdater;
#else
namespace DivinityModManager.AppServices;
#endif

public sealed class ReduxInstanceGuard : IDisposable
{
	private readonly Mutex _mutex;
	private bool _owned;
	public ReduxInstanceGuard(string instanceName = "BG3ModManagerRedux.Instance")
	{
		var user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
		_mutex = new Mutex(false, @"Local\" + instanceName + "." + user);
	}
	public bool TryAcquire()
	{
		try { _owned = _mutex.WaitOne(0); }
		catch (AbandonedMutexException) { _owned = true; }
		return _owned;
	}
	public static bool HasOlderRunningInstance()
	{
		using var current = Process.GetCurrentProcess();
		foreach (var process in Process.GetProcessesByName("Redux"))
		{
			using (process)
			{
				try
				{
					if (process.Id != current.Id && process.SessionId == current.SessionId && process.StartTime < current.StartTime) return true;
				}
				catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
			}
		}
		return false;
	}
	public void Dispose()
	{
		if (_owned) { _mutex.ReleaseMutex(); _owned = false; }
		_mutex.Dispose();
	}
}
