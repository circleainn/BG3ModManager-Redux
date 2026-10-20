using System.Runtime.InteropServices;

namespace DivinityModManager.Util;

public static class ReduxRuntimeEnvironment
{
	private static readonly Lazy<bool> WineRuntime = new(DetectWine);

	public static bool IsWine => WineRuntime.Value;

	private static bool DetectWine()
	{
		if (!OperatingSystem.IsWindows()) return false;
		try
		{
			// Wine exposes this export on its Windows ntdll. Checking the runtime
			// avoids guessing from optional environment variables or installation paths.
			if (!NativeLibrary.TryLoad("ntdll.dll", typeof(ReduxRuntimeEnvironment).Assembly,
				DllImportSearchPath.System32, out var library)) return false;
			try { return NativeLibrary.TryGetExport(library, "wine_get_version", out _); }
			finally { NativeLibrary.Free(library); }
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
			or BadImageFormatException or PlatformNotSupportedException)
		{
			return false;
		}
	}
}
