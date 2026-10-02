namespace DivinityModManager.AppServices;

/// <summary>Retention for replaced PAKs only; never traverses directories or touches installed mods.</summary>
public static class ModBackupRetention
{
	public sealed record Backup(string Path, long Bytes, DateTime CreatedUtc, DateTime ModifiedUtc);
	public sealed record Result(int Deleted, long FreedBytes, int Failed);
	private static readonly object Gate = new();

	public static void CommitReplacement(Action action)
	{
		lock (Gate) action();
	}

	public static IReadOnlyList<Backup> Snapshot(string directory)
	{
		if (!Directory.Exists(directory)) return [];
		if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
			throw new IOException("The backup directory is a link. Open it manually to manage its contents.");
		return new DirectoryInfo(directory).EnumerateFiles("*.pak", SearchOption.TopDirectoryOnly)
			.Where(file => (file.Attributes & FileAttributes.ReparsePoint) == 0)
			.Select(file => new Backup(file.FullName, file.Length, file.CreationTimeUtc, file.LastWriteTimeUtc))
			.OrderBy(file => file.CreatedUtc).ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToList();
	}

	// Call only AFTER the replacement has committed. A failed install retains its recovery copy.
	public static Result Prune(string directory, long maximumBytes)
	{
		lock (Gate)
		{
			var files = Snapshot(directory);
			var remaining = files.Sum(file => file.Bytes);
			var candidates = new List<Backup>();
			var groups = PakFileSet.GetPrimaries(files.Select(file => file.Path))
				.Select(primary => PakFileSet.GetPaths(primary, false).Select(path => files.FirstOrDefault(file => file.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
					.Where(file => file != null).ToArray())
				.OrderBy(group => group.Min(file => file.CreatedUtc));
			foreach (var group in groups)
			{
				if (maximumBytes > 0 && remaining <= maximumBytes) break;
				candidates.AddRange(group);
				remaining -= group.Sum(file => file.Bytes);
			}
			return DeleteReviewed(directory, candidates);
		}
	}

	public static Result DeleteReviewed(string directory, IEnumerable<Backup> reviewed)
	{
		lock (Gate)
		{
			var current = Snapshot(directory).ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
			var deleted = 0; long freed = 0; var failed = 0;
			var selected = reviewed.DistinctBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
			var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var primary in PakFileSet.GetPrimaries(current.Keys))
			{
				var group = PakFileSet.GetPaths(primary, false).Where(current.ContainsKey).ToArray();
				if (group.Any(path => !selected.TryGetValue(path, out var item) || item != current[path]))
					blocked.UnionWith(group);
			}
			foreach (var file in selected.Values)
			{
				// Delete only the same top-level files that were reviewed, never newly created replacements.
				if (blocked.Contains(file.Path) || !current.TryGetValue(file.Path, out var actual) || actual != file) continue;
				try { File.Delete(file.Path); deleted++; freed += file.Bytes; }
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed++; }
			}
			return new(deleted, freed, failed);
		}
	}
}
