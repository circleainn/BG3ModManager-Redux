using DivinityModManager.Models.Health;
using DivinityModManager.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace DivinityModManager.AppServices;

/// <summary>Validates the flat Mods-folder destinations before reading any installable entry.</summary>
public static class ArchivePakImport
{
	public const string DuplicateNamesTitle = "Duplicate PAK filenames";
	public const string NoPakFilesTitle = "No PAK files found";

	public static string GetMissingPakMessage(IEnumerable<string> entryNames)
	{
		var hasLooseTextures = entryNames.Any(name =>
		{
			var normalized = name.Replace('\\', '/');
			return normalized.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)
				&& (normalized.StartsWith("Generated/Public/", StringComparison.OrdinalIgnoreCase)
					|| normalized.Contains("/Generated/Public/", StringComparison.OrdinalIgnoreCase));
		});
		return hasLooseTextures
			? "This archive contains loose texture files under Generated/Public, but no PAK package. "
				+ "Normal mod import does not install these files into the game's Data folder. "
				+ "Follow the mod author's installation instructions, or choose a PAK version if one is provided."
			: "The archive does not contain a Baldur's Gate 3 PAK package. "
				+ "Choose an archive containing a PAK, or follow the mod author's installation instructions.";
	}

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
		var entryNames = archive.Entries.Where(entry => !entry.IsDirectory).Select(entry => entry.Key).ToArray();
		cancellationToken.ThrowIfCancellationRequested();
		RequireUniqueDestinations(entryNames);
		if (!entryNames.Any(name => name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)
			|| (!onlyMods && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))))
			throw new InvalidDataException(GetMissingPakMessage(entryNames));
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
