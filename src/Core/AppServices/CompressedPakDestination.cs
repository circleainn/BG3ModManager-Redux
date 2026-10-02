using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

public static class CompressedPakDestination
{
	public static string Resolve(string modsDirectory, string sourcePath, DivinityModData mod = null)
	{
		var fileName = Path.GetFileNameWithoutExtension(sourcePath);
		if (!fileName.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)) fileName += ".pak";
		if (mod?.HasMetadata == true && !fileName.Contains(mod.Name ?? String.Empty, StringComparison.Ordinal))
		{
			// Preserve the legacy metadata-folder filename only when it is one safe filename.
			RequireSafeName(mod.Folder);
			fileName = mod.Folder + ".pak";
		}
		RequireSafeName(fileName);
		return Path.Combine(Path.GetFullPath(modsDirectory), fileName);
	}

	private static void RequireSafeName(string name)
	{
		var stem = name?.Split('.')[0].TrimEnd(' ') ?? String.Empty;
		var isDevice = stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
			|| (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
				|| stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9');
		if (String.IsNullOrWhiteSpace(name) || name is "." or ".." || isDevice
			|| name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
			|| name.IndexOfAny(['/', '\\', ':']) >= 0)
			throw new InvalidDataException("The compressed PAK's destination name is invalid. Its module Folder must be a filename, not a path. No installed file was replaced.");
	}
}
