using DivinityModManager.Models;
using DivinityModManager.Util;

using SharpCompress.Writers;

namespace DivinityModManager.AppServices;

public static class EditorModBackupService
{
	public static async Task<int> WriteToZipAsync(IAsyncWriter writer, DivinityModData mod, string gameDataFolder,
		string stagingDirectory, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		RequireSafeComponent(mod.Folder, "Folder");
		RequireSafeComponent(mod.UUID, "UUID");
		var gameDataRoot = Path.GetFullPath(gameDataFolder);
		var sourceFolders = new[]
		{
			Path.Combine(gameDataRoot, "Mods", mod.Folder),
			Path.Combine(gameDataRoot, "Public", mod.Folder)
		}.Where(Directory.Exists).ToList();
		if (sourceFolders.Count == 0)
			throw new DirectoryNotFoundException($"The source folders for editor mod '{mod.Name}' are missing. The backup was not replaced.");

		var packageName = Path.ChangeExtension(mod.Folder.Contains(mod.UUID, StringComparison.Ordinal)
			? mod.Folder : mod.Folder + "_" + mod.UUID, "pak");
		// Metadata controls the archive label only; it never chooses a staging path.
		Directory.CreateDirectory(stagingDirectory);
		var ownedStaging = Path.Combine(Path.GetFullPath(stagingDirectory), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(ownedStaging);
		var outputPackage = Path.Combine(ownedStaging, packageName);
		try
		{
			if (!await DivinityFileUtils.CreatePackageAsync(gameDataRoot, sourceFolders, outputPackage,
				cancellationToken, DivinityFileUtils.IgnoredPackageFiles))
				throw new IOException($"Could not package editor mod '{mod.Name}'. The backup was not replaced.");
			var paths = PakFileSet.GetPaths(outputPackage);
			foreach (var path in paths)
			{
				await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
					65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
				await writer.WriteAsync(Path.GetFileName(path), input, File.GetLastWriteTime(path), cancellationToken);
			}
			cancellationToken.ThrowIfCancellationRequested();
			return paths.Count;
		}
		finally
		{
			if (Directory.Exists(ownedStaging)) Directory.Delete(ownedStaging, true);
		}
	}

	private static void RequireSafeComponent(string value, string field)
	{
		var stem = value?.Split('.')[0].TrimEnd(' ') ?? String.Empty;
		var deviceName = stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
			|| (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
				|| stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9');
		if (String.IsNullOrWhiteSpace(value) || value is "." or ".." || deviceName
			|| value != value.TrimEnd(' ', '.') || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
			|| value.IndexOfAny(['/', '\\', ':']) >= 0)
			throw new InvalidDataException($"The editor mod's {field} must be a single valid name, not a path. The backup was not replaced.");
	}
}
