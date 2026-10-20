using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace DivinityModManager.Util;

public enum ProcessElevationState
{
	Unknown,
	Standard,
	Elevated
}

public readonly record struct ProcessElevationInfo(ProcessElevationState State, int Win32Error = 0)
{
	public bool IsElevated => State == ProcessElevationState.Elevated;
	public int? TokenElevationType { get; init; }
}

public static partial class ProcessHelper
{
	// GetCurrentProcessToken() is an inline Windows SDK helper returning this
	// pseudo-handle. It always refers to the primary process token, including
	// while the calling thread impersonates another identity. Do not close it.
	private static readonly IntPtr CurrentProcessToken = new(-4);
	private enum TokenInformationClass
	{
		TokenElevationType = 18,
		TokenElevation = 20
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct TokenElevation
	{
		public int TokenIsElevated;
	}

	[LibraryImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetTokenInformation(
		IntPtr tokenHandle,
		TokenInformationClass tokenInformationClass,
		out TokenElevation tokenInformation,
		uint tokenInformationLength,
		out uint returnLength);

	[LibraryImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetTokenInteger(IntPtr tokenHandle, TokenInformationClass informationClass,
		out int value, uint size, out uint returnedSize);

	/// <summary>
	/// Suppresses PlatformNotSupportedException
	/// </summary>
	private static void TrySetUseShellExecute(ProcessStartInfo info)
	{
		try
		{
			info.UseShellExecute = true;
		}
		catch (PlatformNotSupportedException) { }
	}

	public static bool TryRunCommand(string path, string args = "", string workingDirectory = null)
	{
		args ??= string.Empty;

		try
		{
			path = Environment.ExpandEnvironmentVariables(path);
			var info = new ProcessStartInfo(path, args);
			TrySetUseShellExecute(info);
			if (workingDirectory != null) info.WorkingDirectory = workingDirectory;
			Process.Start(info);
			return true;
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error running command:\n{ex}");
		}
		return false;
	}

	public static bool TryOpenPath(string path, Func<string, bool> existsCheck = null, string args = "", string workingDirectory = null)
	{
		args ??= string.Empty;

		try
		{
			if (!string.IsNullOrEmpty(path))
			{
				//Support using %LOCALAPPDATA% etc.
				path = Environment.ExpandEnvironmentVariables(path);
				if (!Path.IsPathRooted(path)) path = DivinityApp.GetAppDirectory(path);
				if(existsCheck != null && existsCheck.Invoke(path) == false)
				{
					return false;
				}
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					var info = new ProcessStartInfo(path, args);
					TrySetUseShellExecute(info);
					if (workingDirectory != null) info.WorkingDirectory = workingDirectory;
					Process.Start(info);
					return true;
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					Process.Start("xdg-open", path);
					return true;
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start("open", path);
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error opening path:\n{ex}");
		}
		return false;
	}

	/// <summary>Prepares a URI for the platform handler without interpreting it as shell command text.</summary>
	public static ProcessStartInfo CreateUrlStartInfo(string url, string args = "")
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
			|| RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			var start = new ProcessStartInfo(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open")
			{
				UseShellExecute = false
			};
			start.ArgumentList.Add(url);
			return start;
		}

		// ShellExecute opens the registered URI handler directly, not cmd.exe.
		// Keep query separators and percent escapes exactly as supplied.
		return new ProcessStartInfo(url, args ?? String.Empty) { UseShellExecute = true };
	}

	public static void TryOpenUrl(string url, string args = "") => Process.Start(CreateUrlStartInfo(url, args));

	/// <summary>
	/// Reads the elevation flag from the current process token. This distinguishes
	/// actual process elevation from membership in the local Administrators group.
	/// </summary>
	public static ProcessElevationInfo GetCurrentProcessElevation()
	{
		if (!OperatingSystem.IsWindows()) return new ProcessElevationInfo(ProcessElevationState.Unknown);
		try
		{
			var size = (uint)Marshal.SizeOf<TokenElevation>();
			if (!GetTokenInformation(CurrentProcessToken, TokenInformationClass.TokenElevation,
				out var elevation, size, out var returnedSize))
			{
				return new ProcessElevationInfo(ProcessElevationState.Unknown, Marshal.GetLastWin32Error());
			}
			if (returnedSize != size)
				return new ProcessElevationInfo(ProcessElevationState.Unknown, 13); // ERROR_INVALID_DATA

			// Supplementary diagnostics only: Default/Full/Limited (1/2/3) must not
			// replace the primary token's actual elevation flag when deciding to warn.
			int? elevationType = GetTokenInteger(CurrentProcessToken, TokenInformationClass.TokenElevationType,
				out var type, sizeof(int), out var typeSize) && typeSize == sizeof(int) ? type : null;
			return new ProcessElevationInfo(elevation.TokenIsElevated != 0
				? ProcessElevationState.Elevated
				: ProcessElevationState.Standard) { TokenElevationType = elevationType };
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Windows process-token elevation detection failed:\n{ex}");
			return new ProcessElevationInfo(ProcessElevationState.Unknown);
		}
	}

	public static bool IsCurrentProcessAdmin() => GetCurrentProcessElevation().IsElevated;
}
