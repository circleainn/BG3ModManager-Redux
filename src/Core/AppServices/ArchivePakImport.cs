using DivinityModManager.Models.Health;
using DivinityModManager.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace DivinityModManager.AppServices;

/// <summary>Validates the flat Mods-folder destinations before reading any installable entry.</summary>
public static class ArchivePakImport
{
	public const string DuplicateNamesTitle = "Duplicate PAK filenames";

	public static PackagePreflightFinding? FindDestinationCollision(IEnumerable<string> entryNames)
	{
		var duplicates = entryNames.Where(name => name?.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) == true)
			.Select(name => Path.GetFileName(name.Replace('\\', '/')))
			.GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
			.Where(group => group.Count() > 1).Select(group => group.Key).Take(4).ToArray();
		return duplicates.Length == 0 ? null : new PackagePreflightFinding(ModHealthSeverity.Error,
			DuplicateNamesTitle,
			$"Multiple archive entries would install with the same filename: {String.Join(", ", duplicates)}. "
			+ "Redux did not install this archive. Extract it manually and choose the intended PAK variant before importing.");
	}

	public static Task ReadEntriesAsync(IArchive archive, bool onlyMods,
		Func<IEntry, Stream, Task> readEntry, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		RequireUniqueDestinations(archive.Entries.Where(entry => !entry.IsDirectory).Select(entry => entry.Key));
		return ArchiveEntryTraversal.ReadSelectedAsync(archive,
			entry => entry.Key.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)
				|| (!onlyMods && entry.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase)),
			readEntry, cancellationToken);
	}

	public static void RequireUniqueDestinations(IEnumerable<string> entryNames)
	{
		var collision = FindDestinationCollision(entryNames);
		if (collision != null) throw new InvalidDataException(collision.Message);
	}
}
