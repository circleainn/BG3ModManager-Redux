using SharpCompress.Archives;
using SharpCompress.Common;

namespace DivinityModManager.AppServices;

/// <summary>One sequential archive pass, with siblings kept together before any package is opened.</summary>
public sealed class StagedPakArchive : IDisposable
{
	public string DirectoryPath { get; }
	public Dictionary<string, IEntry> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);
	public IReadOnlyList<string> Primaries => PakFileSet.GetPrimaries(Entries.Keys);
	private StagedPakArchive(string parent)
	{
		DirectoryPath = Path.Combine(parent, ".redux-pak-stage-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(DirectoryPath);
	}
	public static async Task<StagedPakArchive> ReadAsync(IArchive archive, string parent, bool onlyMods,
		Func<IEntry, Stream, Task> readOther, CancellationToken token, Func<string, Task> progress = null)
	{
		var staged = new StagedPakArchive(parent);
		try
		{
			await ArchivePakImport.ReadEntriesAsync(archive, onlyMods, async (entry, input) =>
			{
				if (!entry.Key.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
				{
					if (readOther != null) await readOther(entry, input);
					return;
				}
				var name = Path.GetFileName(entry.Key.Replace('\\', '/'));
				if (String.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name != name.TrimEnd(' ', '.'))
					throw new InvalidDataException("Invalid PAK filename in archive.");
				if (progress != null) await progress(name);
				var path = Path.Combine(staged.DirectoryPath, name);
				await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
				await input.CopyToAsync(output, 65536, token);
				staged.Entries.Add(path, entry);
			}, token);
			return staged;
		}
		catch { staged.Dispose(); throw; }
	}

	public void RequireMatchingPartFolders(string primary)
	{
		var parent = Path.GetDirectoryName(Entries[primary].Key.Replace('\\', '/'));
		foreach (var part in PakFileSet.GetPaths(primary))
			if (!Entries.TryGetValue(part, out var entry) || !String.Equals(parent, Path.GetDirectoryName(entry.Key.Replace('\\', '/')), StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException($"The parts of '{Path.GetFileName(primary)}' must be together in the same archive folder.");
	}
	public void Dispose()
	{
		try { Directory.Delete(DirectoryPath, true); }
		catch (IOException) { }
		catch (UnauthorizedAccessException) { }
	}
}
