using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DivinityModManager.AppServices;

public static class NxmPreviousHandlerForwarder
{
	private const string ForwardDepthVariable = "BG3MM_REDUX_NXM_FORWARD_DEPTH";

	public static bool TryForward(INxmRegistryStore store, string value, string currentExecutable, out string error)
		=> TryForward(store, value, ReduxLaunchTarget.ForExecutable(currentExecutable), out error);

	public static bool TryForward(INxmRegistryStore store, string value, ReduxLaunchTarget currentLaunch, out string error)
	{
		error = null;
		if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable(ForwardDepthVariable)))
		{
			error = "The Nexus link could not be forwarded because a forwarding loop was detected.";
			return false;
		}

		var ownerId = store.ReadUserKey()?.GetString("", NxmAssociationService.OwnerValueName);
		var command = String.IsNullOrWhiteSpace(ownerId) ? null : store.ReadBackup(ownerId)?.PreviousCommand;
		if (!TryCreateStartInfo(command, value, currentLaunch, out var startInfo, out error)) return false;
		startInfo.Environment[ForwardDepthVariable] = "1";
		try
		{
			return Process.Start(startInfo) != null;
		}
		catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
		{
			error = "The previous Nexus link handler could not be started.";
			return false;
		}
	}

	public static bool TryCreateStartInfo(string command, string value, string currentExecutable,
		out ProcessStartInfo startInfo, out string error)
		=> TryCreateStartInfo(command, value, ReduxLaunchTarget.ForExecutable(currentExecutable), out startInfo, out error);

	public static bool TryCreateStartInfo(string command, string value, ReduxLaunchTarget currentLaunch,
		out ProcessStartInfo startInfo, out string error)
	{
		startInfo = null;
		error = null;
		if (String.IsNullOrWhiteSpace(command) || String.IsNullOrWhiteSpace(value))
		{
			error = "No previous Nexus link handler is available.";
			return false;
		}

		if (!TrySplitCommandLine(command, out var arguments) || arguments.Count < 2)
		{
			error = "The previous Nexus link handler command is not safe to forward.";
			return false;
		}
		var executable = Environment.ExpandEnvironmentVariables(arguments[0]);
		if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable) || IsCurrentApplication(executable, arguments, currentLaunch))
		{
			error = "The previous Nexus link handler executable is unavailable.";
			return false;
		}

		var placeholderCount = arguments.Skip(1).Count(argument => argument.Equals("%1", StringComparison.Ordinal));
		if (placeholderCount != 1 || arguments.Skip(1).Any(argument => argument.Contains("%1", StringComparison.Ordinal) && argument != "%1"))
		{
			error = "The previous Nexus link handler uses an unsupported argument template.";
			return false;
		}

		startInfo = new ProcessStartInfo
		{
			FileName = executable,
			UseShellExecute = false,
			WorkingDirectory = Path.GetDirectoryName(executable) ?? String.Empty
		};
		foreach (var argument in arguments.Skip(1)) startInfo.ArgumentList.Add(argument == "%1" ? value : argument);
		return true;
	}

	private static bool IsCurrentApplication(string executable, IReadOnlyList<string> arguments, ReduxLaunchTarget currentLaunch)
	{
		if (PathsEqual(executable, Path.ChangeExtension(currentLaunch.ApplicationPath, ".exe"))) return true;
		if (!ReduxLaunchTarget.IsWindowsDotNetHost(executable) || arguments.Count < 2) return false;
		var application = Environment.ExpandEnvironmentVariables(arguments[1]);
		return Path.IsPathFullyQualified(application)
			&& PathsEqual(application, Path.ChangeExtension(currentLaunch.ApplicationPath, ".dll"));
	}

	private static bool TrySplitCommandLine(string command, out IReadOnlyList<string> arguments)
	{
		arguments = Array.Empty<string>();
		var pointer = CommandLineToArgvW(command, out var count);
		if (pointer == IntPtr.Zero || count <= 0) return false;
		try
		{
			var values = new string[count];
			for (var index = 0; index < count; index++)
			{
				var argumentPointer = Marshal.ReadIntPtr(pointer, index * IntPtr.Size);
				values[index] = Marshal.PtrToStringUni(argumentPointer) ?? String.Empty;
			}
			arguments = values;
			return true;
		}
		finally
		{
			LocalFree(pointer);
		}
	}

	private static bool PathsEqual(string left, string right) =>
		String.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

	[DllImport("shell32.dll", SetLastError = true)]
	private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int argumentCount);

	[DllImport("kernel32.dll")]
	private static extern IntPtr LocalFree(IntPtr memory);
}
