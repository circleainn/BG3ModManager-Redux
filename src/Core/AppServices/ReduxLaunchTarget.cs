#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

#if REDUX_UPDATER
namespace ReduxUpdater;
internal sealed class ReduxLaunchTarget
#else
namespace DivinityModManager.AppServices;
public sealed class ReduxLaunchTarget
#endif
{
	public string ExecutablePath { get; }
	public string ApplicationPath { get; }
	public string ApplicationDirectory => Path.GetDirectoryName(ApplicationPath)!;
	public string? DotNetHostPath => IsDotNetHosted ? ExecutablePath : null;
	public bool IsDotNetHosted { get; }

	private ReduxLaunchTarget(string executablePath, string applicationPath, bool hosted)
	{
		ExecutablePath = ValidatePath(executablePath);
		ApplicationPath = ValidatePath(applicationPath);
		IsDotNetHosted = hosted;
		if (hosted && (!IsWindowsDotNetHost(ExecutablePath) ||
			!String.Equals(Path.GetExtension(ApplicationPath), ".dll", StringComparison.OrdinalIgnoreCase)))
			throw new ArgumentException("A hosted launch requires Windows dotnet.exe and a managed application DLL.");
		if (!hosted && !String.Equals(Path.GetExtension(ApplicationPath), ".exe", StringComparison.OrdinalIgnoreCase))
			throw new ArgumentException("A native launch requires an application executable.");
	}

	public static ReduxLaunchTarget Current() => Resolve(Environment.ProcessPath,
		Assembly.GetEntryAssembly()?.Location, AppContext.BaseDirectory);

	public static ReduxLaunchTarget Resolve(string? processPath, string? entryAssemblyPath, string baseDirectory)
	{
		var executable = String.IsNullOrWhiteSpace(processPath) ? Path.Combine(baseDirectory, "Redux.exe") : processPath;
		return IsWindowsDotNetHost(executable)
			? new ReduxLaunchTarget(executable, entryAssemblyPath ?? String.Empty, hosted: true)
			: ForExecutable(executable);
	}

	public static ReduxLaunchTarget ForExecutable(string executablePath, string? dotNetHostPath = null) =>
		String.IsNullOrWhiteSpace(dotNetHostPath)
			? new ReduxLaunchTarget(executablePath, executablePath, hosted: false)
			: new ReduxLaunchTarget(dotNetHostPath, Path.ChangeExtension(executablePath, ".dll"), hosted: true);

	public static ReduxLaunchTarget ForManagedApplication(string applicationPath, string dotNetHostPath) =>
		new(dotNetHostPath, applicationPath, hosted: true);

	public ProcessStartInfo CreateStartInfo(IEnumerable<string>? arguments = null)
	{
		var start = new ProcessStartInfo(ExecutablePath)
		{
			UseShellExecute = false,
			WorkingDirectory = ApplicationDirectory
		};
		if (IsDotNetHosted) start.ArgumentList.Add(ApplicationPath);
		if (arguments != null)
			foreach (var argument in arguments) start.ArgumentList.Add(argument);
		return start;
	}

	// Registry commands require a string. These are validated Windows file paths,
	// so embedded quotes/control characters are rejected rather than shell-escaped.
	public string CommandPrefix => IsDotNetHosted
		? $"\"{ExecutablePath}\" \"{ApplicationPath}\"" : $"\"{ExecutablePath}\"";

	public static bool IsWindowsDotNetHost(string? path) =>
		!String.IsNullOrWhiteSpace(path) && String.Equals(Path.GetFileName(path), "dotnet.exe", StringComparison.OrdinalIgnoreCase);

	private static string ValidatePath(string path)
	{
		if (String.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.IndexOf('"') >= 0)
			throw new ArgumentException("An absolute Windows application path is required.");
		foreach (var character in path)
			if (Char.IsControl(character)) throw new ArgumentException("Application paths cannot contain control characters.");
		return Path.GetFullPath(path);
	}
}
