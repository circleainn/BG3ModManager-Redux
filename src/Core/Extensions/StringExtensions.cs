namespace DivinityModManager.Extensions;

public static class StringExtensions
{
	public static bool IsExistingDirectory(this string path)
	{
		return !String.IsNullOrWhiteSpace(path) && Directory.Exists(path);
	}

	public static bool IsExistingFile(this string path)
	{
		return !String.IsNullOrWhiteSpace(path) && File.Exists(path);
	}

	private static readonly Dictionary<string, string> _specialPaths = new()
	{
		{ "%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) },
		{ "%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) },
		{ "%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) },
	};

	public static string ReplaceSpecialPaths(this string path)
	{
		if (string.IsNullOrEmpty(path)) return path;

		foreach (var kvp in _specialPaths)
		{
			if(!string.IsNullOrEmpty(kvp.Value))
			{
				path = path.Replace(kvp.Value, kvp.Key);
			}
		}
		return path;
	}

	/// <summary>
	/// Checks File.Exists after expanding environment variables.
	/// </summary>
	public static bool FileExists(this string path)
	{
		if (string.IsNullOrEmpty(path)) return false;

		return File.Exists(Environment.ExpandEnvironmentVariables(path));
	}

	/// <summary>
	/// Checks Directory.Exists after expanding environment variables.
	/// </summary>
	public static bool DirectoryExists(this string path)
	{
		if (string.IsNullOrEmpty(path)) return false;

		return Directory.Exists(Environment.ExpandEnvironmentVariables(path));
	}

	/// <summary>
	/// Expands environment variables and makes the path relative to the app directory if not rooted.
	/// </summary>
	public static string ToRealPath(this string path)
	{
		if (string.IsNullOrEmpty(path)) return path;

		var finalPath = Environment.ExpandEnvironmentVariables(path);
		if(!Path.IsPathRooted(finalPath))
		{
			finalPath = DivinityApp.GetAppDirectory(finalPath);
		}
		return finalPath;
	}
}
