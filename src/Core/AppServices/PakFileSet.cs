using LSLib.LS;
using System.Text.Json;

namespace DivinityModManager.AppServices;

/// <summary>Discovers parts from the PAK header, never from a numbered filename alone.</summary>
public static class PakFileSet
{
	public static IReadOnlyList<string> GetPaths(string primary, bool requireComplete = true)
	{
		primary = Path.GetFullPath(primary);
		uint count;
		try
		{
			using var package = new PackageReader().Read(primary, metadataOnly: true);
			count = package.Metadata.NumParts;
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or NotAPackageException or ArgumentException)
		{
			// Let the existing loader diagnose invalid standalone files.
			return [primary];
		}
		if (count is 0 or > 65535) throw new InvalidDataException("Invalid PAK part count.");
		var paths = Enumerable.Range(0, checked((int)count))
			.Select(index => index == 0 ? primary : Package.MakePartFilename(primary, index)).ToArray();
		if (requireComplete)
		{
			var missing = paths.Where(path => !File.Exists(path)).Select(Path.GetFileName).ToArray();
			if (missing.Length > 0) throw new InvalidDataException($"'{Path.GetFileName(primary)}' requires {count} files. Missing PAK parts: {String.Join(", ", missing)}. Keep every part together.");
		}
		return paths;
	}

	public static IReadOnlyList<string> GetPrimaries(IEnumerable<string> paths)
	{
		var candidates = paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		var parts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var path in candidates)
		{
			try { foreach (var part in GetPaths(path, false).Skip(1)) parts.Add(part); }
			catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
			{
				// An unreadable primary remains a candidate for the loader's diagnostic;
				// it must not prevent the rest of the library from being discovered.
			}
		}
		return candidates.Where(path => !parts.Contains(path)).ToArray();
	}

	public static string PartPath(string primary, int index) => index == 0 ? primary : Package.MakePartFilename(primary, index);

	public static async Task CopyAsync(string source, string stagedPrimary, CancellationToken token)
	{
		var paths = GetPaths(source);
		for (var index = 0; index < paths.Count; index++)
		{
			token.ThrowIfCancellationRequested();
			await using var input = new FileStream(paths[index], FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
			await using var output = new FileStream(PartPath(stagedPrimary, index), FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
			await input.CopyToAsync(output, 65536, token);
		}
	}

	private sealed record Entry(string Name, long Length, DateTime ModifiedUtc);
	private sealed record Journal(IReadOnlyList<Entry> Old, IReadOnlyList<Entry> New);
	private static readonly SemaphoreSlim InstallGate = new(1, 1);
	private const string Prefix = ".redux-pak-transaction-";

	/// <summary>Stages recovery copies before a short journaled rename transaction. Cancellation never leaves half a set.</summary>
	public static async Task<bool> InstallAsync(string stagedPrimary, string destination, string backupDirectory, CancellationToken token)
	{
		await InstallGate.WaitAsync(token);
		try { return await InstallCoreAsync(stagedPrimary, Path.GetFullPath(destination), backupDirectory, token); }
		finally { InstallGate.Release(); }
	}

	private static async Task<bool> InstallCoreAsync(string stagedPrimary, string destination, string backupDirectory, CancellationToken token)
	{
		var incoming = GetPaths(stagedPrimary);
		var folder = Path.GetFullPath(Path.GetDirectoryName(destination));
		Directory.CreateDirectory(folder);
		Recover(folder);
		var stem = Path.GetFileNameWithoutExtension(destination);
		var separator = stem.LastIndexOf('_');
		if (separator > 0 && Int32.TryParse(stem[(separator + 1)..], out var partNumber) && partNumber > 0)
		{
			var owner = Path.Combine(folder, stem[..separator] + Path.GetExtension(destination));
			if (File.Exists(owner) && GetPaths(owner, false).Skip(1).Contains(destination, StringComparer.OrdinalIgnoreCase))
				throw new IOException($"'{Path.GetFileName(destination)}' is a part of '{Path.GetFileName(owner)}'. Replace the complete owning package instead.");
		}
		var previous = File.Exists(destination) ? GetPaths(destination, false).Where(File.Exists).ToArray() : [];
		var targets = Enumerable.Range(0, incoming.Count).Select(index => PartPath(destination, index)).ToArray();
		foreach (var target in targets)
			if (File.Exists(target) && !previous.Contains(target, StringComparer.OrdinalIgnoreCase))
				throw new IOException($"'{Path.GetFileName(target)}' belongs to another file. The PAK set was not installed.");
		foreach (var path in previous.Concat(incoming)) RequireRegularFile(path);
		var transaction = Path.Combine(folder, Prefix + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(transaction);
		var oldFolder = Path.Combine(transaction, "old");
		Directory.CreateDirectory(oldFolder);
		var pendingBackups = new List<(string Temporary, string Final)>();
		string backupStaging = null;
		var journal = new Journal(previous.Select(Inspect).ToArray(), incoming.Select((path, index) => Inspect(path) with { Name = Path.GetFileName(targets[index]) }).ToArray());
		try
		{
			// Backups retain one shared prefix, so LSLib can reopen the old set directly.
			if (previous.Length > 0)
			{
				Directory.CreateDirectory(backupDirectory);
				if ((File.GetAttributes(backupDirectory) & FileAttributes.ReparsePoint) != 0) throw new IOException("The PAK backup folder is a link.");
				var backupPrefix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + "-";
				backupStaging = Path.Combine(backupDirectory, ".pending-" + Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(backupStaging);
				foreach (var path in previous)
				{
					await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
					var temporaryBackup = Path.Combine(backupStaging, backupPrefix + Path.GetFileName(path));
					pendingBackups.Add((temporaryBackup, Path.Combine(backupDirectory, Path.GetFileName(temporaryBackup))));
					await using var output = new FileStream(temporaryBackup, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
					await input.CopyToAsync(output, 65536, token);
				}
			}
			token.ThrowIfCancellationRequested();
			ModBackupRetention.CommitReplacement(() =>
			{
				foreach (var backup in pendingBackups) File.Move(backup.Temporary, backup.Final);
				// Flush the plan before the first mutation. Parts are published before their primary.
				using (var stream = new FileStream(Path.Combine(transaction, "journal.tmp"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					JsonSerializer.Serialize(stream, journal);
					stream.Flush(true);
				}
				File.Move(Path.Combine(transaction, "journal.tmp"), Path.Combine(transaction, "journal.json"));
				foreach (var path in previous) File.Move(path, Path.Combine(oldFolder, Path.GetFileName(path)));
				foreach (var index in Enumerable.Range(0, incoming.Count).Reverse()) File.Move(incoming[index], targets[index]);
				using (var marker = new FileStream(Path.Combine(transaction, "complete"), FileMode.CreateNew, FileAccess.Write, FileShare.None)) marker.Flush(true);
			});
		}
		catch
		{
			RecoverTransaction(folder, transaction);
			throw;
		}
		finally { if (backupStaging != null) TryCleanup(backupStaging); }
		TryCleanup(transaction);
		return previous.Length > 0;
	}

	public static void Recover(string folder)
	{
		if (!Directory.Exists(folder)) return;
		foreach (var transaction in Directory.EnumerateDirectories(folder, Prefix + "*", SearchOption.TopDirectoryOnly))
			RecoverTransaction(Path.GetFullPath(folder), transaction);
	}

	private static void RecoverTransaction(string folder, string transaction)
	{
		if ((File.GetAttributes(transaction) & FileAttributes.ReparsePoint) != 0) throw new IOException("The PAK recovery directory is a link.");
		var oldFolder = Path.Combine(transaction, "old");
		if (Directory.Exists(oldFolder) && (File.GetAttributes(oldFolder) & FileAttributes.ReparsePoint) != 0) throw new IOException("The PAK recovery data is a link.");
		if (!File.Exists(Path.Combine(transaction, "complete")) && File.Exists(Path.Combine(transaction, "journal.json")))
		{
			var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(Path.Combine(transaction, "journal.json"))) ?? throw new IOException("Invalid PAK recovery journal.");
			foreach (var entry in journal.Old.Concat(journal.New))
				if (entry.Name != Path.GetFileName(entry.Name) || !entry.Name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) || entry.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
					throw new IOException("The PAK recovery journal contains an invalid filename.");
			foreach (var entry in journal.New)
			{
				var target = Path.Combine(folder, entry.Name);
				var old = journal.Old.FirstOrDefault(value => value.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase));
				if (old != null && !File.Exists(Path.Combine(oldFolder, old.Name)))
				{
					if (!Matches(target, old)) throw new IOException($"Cannot recover changed PAK '{entry.Name}'. Recovery files were retained.");
					continue;
				}
				if (File.Exists(target))
				{
					if (!Matches(target, entry)) throw new IOException($"Cannot recover changed PAK '{entry.Name}'. Recovery files were retained.");
					File.Delete(target);
				}
			}
			foreach (var entry in journal.Old.Reverse())
			{
				var saved = Path.Combine(oldFolder, entry.Name);
				var target = Path.Combine(folder, entry.Name);
				if (File.Exists(saved))
				{
					if (!Matches(saved, entry) || File.Exists(target)) throw new IOException($"Cannot restore PAK '{entry.Name}'. Recovery files were retained.");
					File.Move(saved, target);
				}
				else if (!Matches(target, entry)) throw new IOException($"Missing original PAK '{entry.Name}'. Recovery files were retained.");
			}
		}
		TryCleanup(transaction);
	}

	private static Entry Inspect(string path) => new(Path.GetFileName(path), new FileInfo(path).Length, File.GetLastWriteTimeUtc(path));
	private static bool Matches(string path, Entry entry) => File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && Inspect(path) == entry;
	private static void RequireRegularFile(string path)
	{
		if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException($"PAK file '{Path.GetFileName(path)}' is a link.");
	}
	private static void TryCleanup(string path)
	{
		try { Directory.Delete(path, true); }
		catch (IOException) { }
		catch (UnauthorizedAccessException) { }
	}
}
