namespace DivinityModManager.AppServices;

public sealed record ScriptExtenderDetectionPath(string? Folder, string Source, string? Error);

/// <summary>Selects a read-only detection location without changing the extender updater's cache configuration.</summary>
public static class ScriptExtenderDetectionPathResolver
{
	public static ScriptExtenderDetectionPath Resolve(string? detectionOverride, string? updaterCachePath,
		string? larianStudiosFolder, string? localAppDataFolder)
	{
		if (!String.IsNullOrWhiteSpace(detectionOverride))
			return ResolveExplicit(detectionOverride, "Redux detection-folder override");
		if (!String.IsNullOrWhiteSpace(updaterCachePath))
			return ResolveExplicit(updaterCachePath, "configured updater CachePath");

		// Preserve the existing default beside the BG3 profile's Larian Studios folder,
		// including when that profile is in a different Wine prefix.
		if (TryNormalize(larianStudiosFolder, out var larianFolder)
			&& Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(larianFolder!)) is { Length: > 0 } parent)
			return new(Path.Combine(parent, DivinityApp.EXTENDER_APPDATA_DIRECTORY), "BG3 profile's Local AppData", null);
		if (TryNormalize(localAppDataFolder, out var localFolder))
			return new(Path.Combine(localFolder!, DivinityApp.EXTENDER_APPDATA_DIRECTORY), "Windows Local AppData", null);
		return new(null, "default Local AppData", "Script Extender detection could not locate Local AppData. " + OverrideHint);
	}

	private const string OverrideHint = "Set Script Extender detection folder in Preferences > General > Paths and storage "
		+ "to the existing cache folder as visible to Redux. This does not move or reconfigure Script Extender.";

	private static ScriptExtenderDetectionPath ResolveExplicit(string path, string source) =>
		TryNormalize(path, out var normalized)
			? new(normalized, source, null)
			: new(null, source, $"Script Extender detection cannot use the {source} '{path}' because it is not a valid absolute Windows folder path. "
				+ "Relative paths depend on the game's launch folder and cannot be inferred safely. " + OverrideHint);

	private static bool TryNormalize(string? path, out string? normalized)
	{
		normalized = null;
		if (String.IsNullOrWhiteSpace(path)) return false;
		try
		{
			if (!Path.IsPathFullyQualified(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
			var fullPath = Path.GetFullPath(path);
			var root = Path.GetPathRoot(fullPath)!;
			if (fullPath[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				.Any(part => part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) return false;
			normalized = Path.TrimEndingDirectorySeparator(fullPath);
			return true;
		}
		catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
		{
			return false;
		}
	}
}
