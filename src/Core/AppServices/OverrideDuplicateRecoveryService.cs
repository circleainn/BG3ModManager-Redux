using System.Security.Cryptography;
using System.Text.Json;

namespace DivinityModManager.AppServices;

public enum OverrideDuplicateChoice { KeepMods, KeepHeld }
public sealed record OverrideDuplicateFile(string FileName, string Path, long Length, DateTime ModifiedUtc, string Sha256);
public sealed record OverrideDuplicateConflict(string FileName, IReadOnlyList<OverrideDuplicateFile> ModsFiles,
	IReadOnlyList<OverrideDuplicateFile> HeldFiles)
{
	public bool Identical => ModsFiles.Count == HeldFiles.Count && ModsFiles.All(file =>
		HeldFiles.Any(held => held.FileName.Equals(file.FileName, StringComparison.OrdinalIgnoreCase) && held.Sha256 == file.Sha256));
}
public sealed record OverrideDuplicateReview(string ModsTarget, string HoldingTarget, IReadOnlyList<OverrideDuplicateConflict> Conflicts);
public sealed record OverrideDuplicateResolutionResult(string BackupDirectory);

/// <summary>Retains the chosen copy in place and backs up the other complete package before removing it from discovery.</summary>
public sealed class OverrideDuplicateRecoveryService
{
	private sealed record DisplacedFile(OverrideDuplicateFile File, bool FromMods);
	private sealed record Journal(string Id, string ModsTarget, string HoldingTarget, IReadOnlyList<DisplacedFile> Files);
	private static readonly SemaphoreSlim Gate = new(1, 1);
	private readonly string _modsTarget;
	private readonly string _holdingTarget;
	private readonly string _backupTarget;
	public string ModsFolder { get; }
	public string HoldingFolder { get; }
	public string BackupRoot { get; }
	internal Action<int> AfterStagedFile { get; set; }

	public OverrideDuplicateRecoveryService(string modsFolder, string holdingFolder, string backupRoot)
	{
		ModsFolder = Path.GetFullPath(modsFolder);
		HoldingFolder = Path.GetFullPath(holdingFolder);
		BackupRoot = Path.GetFullPath(backupRoot);
		_modsTarget = ResolveDirectory(ModsFolder);
		_holdingTarget = ResolveDirectory(HoldingFolder);
		_backupTarget = ResolveDirectory(BackupRoot);
		if (!Same(HoldingFolder, _holdingTarget) || !Same(BackupRoot, _backupTarget))
			throw new IOException("Override holding and recovery backup folders must not contain directory links.");
		if (Overlaps(_modsTarget, _holdingTarget) || Overlaps(_modsTarget, _backupTarget) || Overlaps(_holdingTarget, _backupTarget))
			throw new IOException("Mods, Override holding, and recovery backups must be separate folders.");
	}

	public Task<OverrideDuplicateReview> ReviewAsync(CancellationToken token = default) => RunAsync(() => Review(token), token);
	public Task<OverrideDuplicateResolutionResult> ApplyAsync(OverrideDuplicateReview review,
		IReadOnlyDictionary<string, OverrideDuplicateChoice> choices, CancellationToken token = default) =>
		RunAsync(() => Apply(review, choices, token), token);
	public Task RecoverPendingAsync(CancellationToken token = default) => RunAsync(() =>
	{
		EnsureFolders();
		if (File.Exists(Path.Combine(_holdingTarget, "pending-switch.json"))
			&& Transactions().Any(path => File.Exists(Path.Combine(path, "pending.json"))))
			throw new IOException("Both Override recovery records are present. Inspect the retained files before recovering either operation.");
		foreach (var directory in Transactions())
		{
			token.ThrowIfCancellationRequested();
			if (File.Exists(Path.Combine(directory, "pending.json"))) RecoverTransaction(directory);
			else if (File.Exists(Path.Combine(directory, "resolved.json"))) CleanupCommitted(directory);
		}
		return true;
	}, token);

	private static async Task<T> RunAsync<T>(Func<T> action, CancellationToken token)
	{
		await Gate.WaitAsync(token).ConfigureAwait(false);
		try { return await Task.Run(action, token).ConfigureAwait(false); }
		finally { Gate.Release(); }
	}

	private OverrideDuplicateReview Review(CancellationToken token)
	{
		EnsureFolders();
		RequireNoPending();
		var mods = Groups(_modsTarget);
		var held = Groups(_holdingTarget);
		var groups = mods.Select(files => (Files: files, Mods: true)).Concat(held.Select(files => (Files: files, Mods: false))).ToList();
		var conflicts = new List<OverrideDuplicateConflict>();
		while (groups.Count > 0)
		{
			token.ThrowIfCancellationRequested();
			var component = new List<(IReadOnlyList<string> Files, bool Mods)> { groups[0] };
			groups.RemoveAt(0);
			var names = component[0].Files.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
			bool expanded;
			do
			{
				expanded = false;
				for (var index = groups.Count - 1; index >= 0; index--)
					if (groups[index].Files.Any(path => names.Contains(Path.GetFileName(path))))
					{
						component.Add(groups[index]);
						names.UnionWith(groups[index].Files.Select(Path.GetFileName));
						groups.RemoveAt(index);
						expanded = true;
					}
			} while (expanded);
			if (!component.Any(group => group.Mods) || !component.Any(group => !group.Mods)) continue;
			var modsFiles = component.Where(group => group.Mods).SelectMany(group => group.Files)
				.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(path => Inspect(path, token)).ToArray();
			var heldFiles = component.Where(group => !group.Mods).SelectMany(group => group.Files)
				.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(path => Inspect(path, token)).ToArray();
			conflicts.Add(new(names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).First(), modsFiles, heldFiles));
		}
		EnsureFolders();
		return new(_modsTarget, _holdingTarget, conflicts.OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase).ToArray());
	}

	private OverrideDuplicateResolutionResult Apply(OverrideDuplicateReview review,
		IReadOnlyDictionary<string, OverrideDuplicateChoice> choices, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(review);
		ArgumentNullException.ThrowIfNull(choices);
		EnsureFolders();
		RequireNoPending();
		// Rebuild groups and hash every member again, including the copy that will stay in place.
		// This catches replacement with equal lengths/timestamps and changed multipart membership.
		var current = Review(token);
		if (JsonSerializer.Serialize(current) != JsonSerializer.Serialize(review))
			throw new IOException("The Override copies changed since review. Review them again; no files were moved.");
		if (review.Conflicts.Count == 0 || choices.Count != review.Conflicts.Count)
			throw new ArgumentException("Choose a copy to keep for every conflicting package.");
		var displaced = new List<DisplacedFile>();
		foreach (var conflict in review.Conflicts)
		{
			if (!choices.TryGetValue(conflict.FileName, out var choice) || !Enum.IsDefined(choice))
				throw new ArgumentException("Choose a copy to keep for every conflicting package.");
			displaced.AddRange((choice == OverrideDuplicateChoice.KeepMods ? conflict.HeldFiles : conflict.ModsFiles)
				.Select(file => new DisplacedFile(file, choice == OverrideDuplicateChoice.KeepHeld)));
		}
		var id = Guid.NewGuid().ToString("N");
		var directory = Path.Combine(_backupTarget, id);
		Directory.CreateDirectory(directory);
		var journal = new Journal(id, _modsTarget, _holdingTarget, displaced);
		WriteJournal(Path.Combine(directory, "pending.json"), journal);
		try
		{
			// Fully flushed external backups exist before any source is renamed.
			foreach (var entry in displaced)
			{
				EnsureFolders();
				token.ThrowIfCancellationRequested();
				var backup = BackupPath(directory, entry);
				Directory.CreateDirectory(Path.GetDirectoryName(backup));
				using (var source = OpenRegular(entry.File.Path))
				using (var output = new FileStream(backup + ".tmp", FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					var buffer = new byte[131072];
					int read;
					while ((read = source.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); }
					output.Flush(true);
				}
				RequireContent(backup + ".tmp", entry.File, token);
				File.Move(backup + ".tmp", backup);
			}
			var staged = 0;
			foreach (var entry in displaced)
			{
				EnsureFolders();
				token.ThrowIfCancellationRequested();
				RequireContent(entry.File.Path, entry.File, token);
				var stage = StagePath(journal, entry);
				Directory.CreateDirectory(Path.GetDirectoryName(stage));
				RequireUnlinkedPath(Path.GetDirectoryName(stage));
				File.Move(entry.File.Path, stage);
				RequireContent(stage, entry.File, token);
				AfterStagedFile?.Invoke(++staged);
			}
			foreach (var file in review.Conflicts.SelectMany(conflict => conflict.ModsFiles.Concat(conflict.HeldFiles))
				.Where(file => !displaced.Any(entry => Same(entry.File.Path, file.Path)))) RequireContent(file.Path, file, token);
			EnsureFolders();
			token.ThrowIfCancellationRequested();
			File.Move(Path.Combine(directory, "pending.json"), Path.Combine(directory, "resolved.json"));
		}
		catch (Exception failure)
		{
			try { RecoverTransaction(directory); }
			catch (Exception recoveryFailure)
			{
				throw new IOException($"Recovery stopped without overwriting files. Copies and the recovery record are retained in '{directory}'.", new AggregateException(failure, recoveryFailure));
			}
			throw;
		}
		// A cleanup failure must not turn a committed resolution into a second transaction.
		try { CleanupCommitted(directory); } catch (IOException) { } catch (UnauthorizedAccessException) { }
		return new(directory);
	}

	private void RecoverTransaction(string directory)
	{
		var journal = ReadJournal(directory, "pending.json");
		foreach (var entry in journal.Files.Reverse())
		{
			EnsureFolders();
			var stage = StagePath(journal, entry);
			if (File.Exists(stage))
			{
				RequireContent(stage, entry.File, default);
				if (File.Exists(entry.File.Path)) throw new IOException("The original location is occupied. Recovery copies were retained.");
				File.Move(stage, entry.File.Path);
			}
			else RequireContent(entry.File.Path, entry.File, default);
		}
		File.Move(Path.Combine(directory, "pending.json"), Path.Combine(directory, "rolled-back.json"));
		RemoveEmptyStages(journal);
	}

	private void CleanupCommitted(string directory)
	{
		var journal = ReadJournal(directory, "resolved.json");
		foreach (var entry in journal.Files)
		{
			EnsureFolders();
			var stage = StagePath(journal, entry);
			if (!File.Exists(stage)) continue;
			RequireContent(BackupPath(directory, entry), entry.File, default);
			RequireContent(stage, entry.File, default);
			File.Delete(stage);
		}
		RemoveEmptyStages(journal);
		// Fully cleaned backups are historical records. They must remain portable
		// without being mistaken for work against the application's current folders.
		File.Move(Path.Combine(directory, "resolved.json"), Path.Combine(directory, "cleaned.json"));
	}

	private Journal ReadJournal(string directory, string fileName)
	{
		EnsureFolders();
		RequireUnlinkedPath(directory);
		var path = Path.Combine(directory, fileName);
		using var input = OpenRegular(path);
		var journal = JsonSerializer.Deserialize<Journal>(input) ?? throw new IOException("Invalid Override recovery record.");
		if (!Guid.TryParseExact(journal.Id, "N", out _) || !Same(directory, Path.Combine(_backupTarget, journal.Id))
			|| !Same(journal.ModsTarget, _modsTarget) || !Same(journal.HoldingTarget, _holdingTarget) || journal.Files == null)
			throw new IOException("Override recovery folders no longer match the record. All copies were retained.");
		var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var entry in journal.Files)
		{
			var file = entry?.File;
			if (file == null || !ValidName(file.FileName) || !Same(file.Path, Path.Combine(entry.FromMods ? _modsTarget : _holdingTarget, file.FileName))
				|| !paths.Add(file.Path) || file.Length < 0 || file.Sha256?.Length != 64 || !file.Sha256.All(Uri.IsHexDigit))
				throw new IOException("Invalid paths or file identity in the Override recovery record.");
		}
		return journal;
	}

	private void RequireNoPending()
	{
		if (File.Exists(Path.Combine(_holdingTarget, "pending-switch.json")))
			throw new IOException("Recover the interrupted Override order switch before reviewing duplicate copies.");
		if (Transactions().Any(path => File.Exists(Path.Combine(path, "pending.json"))))
			throw new IOException("An interrupted duplicate resolution must be recovered before continuing.");
	}
	private IEnumerable<string> Transactions() => Directory.Exists(_backupTarget)
		? Directory.EnumerateDirectories(_backupTarget).Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _)).ToArray() : [];
	private void EnsureFolders()
	{
		if (!Same(ResolveDirectory(ModsFolder), _modsTarget) || !Same(ResolveDirectory(HoldingFolder), _holdingTarget)
			|| !Same(ResolveDirectory(BackupRoot), _backupTarget)) throw new IOException("A managed folder link changed. No further files were moved.");
	}
	private static IReadOnlyList<IReadOnlyList<string>> Groups(string folder)
	{
		if (!Directory.Exists(folder)) return [];
		var files = Directory.GetFiles(folder, "*.pak");
		foreach (var file in files) RequireUnlinkedPath(file);
		var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var result = new List<IReadOnlyList<string>>();
		foreach (var primary in PakFileSet.GetPrimaries(files).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
		{
			var parts = PakFileSet.GetPaths(primary);
			foreach (var part in parts)
				if (!Same(Path.GetDirectoryName(part), folder) || !owners.Add(part)) throw new IOException("Ambiguous multipart PAK ownership; inspect these files manually.");
			result.Add(parts);
		}
		return result;
	}
	private static OverrideDuplicateFile Inspect(string path, CancellationToken token)
	{
		using var file = OpenRegular(path);
		var length = file.Length;
		var modified = File.GetLastWriteTimeUtc(path);
		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		var buffer = new byte[131072];
		int count;
		while ((count = file.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, count); }
		if (file.Length != length || File.GetLastWriteTimeUtc(path) != modified) throw new IOException("A package changed while it was being reviewed.");
		return new(Path.GetFileName(path), path, length, modified, Convert.ToHexString(hash.GetHashAndReset()));
	}
	private static void RequireContent(string path, OverrideDuplicateFile expected, CancellationToken token)
	{
		RequireUnlinkedPath(path);
		var actual = Inspect(path, token);
		if (actual.Length != expected.Length || actual.Sha256 != expected.Sha256) throw new IOException($"'{expected.FileName}' changed. Recovery copies were retained.");
	}
	private static FileStream OpenRegular(string path)
	{
		RequireUnlinkedPath(path);
		return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.SequentialScan);
	}
	private static void RequireUnlinkedPath(string path)
	{
		if (!Same(ResolveDirectory(Path.GetDirectoryName(path)), Path.GetDirectoryName(path))
			|| (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("A recovery file or folder is a link. Inspect it manually.");
	}
	private static string ResolveDirectory(string path)
	{
		path = Path.GetFullPath(path);
		var current = Path.GetPathRoot(path);
		foreach (var segment in path[current.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
		{
			if (segment.Length == 0) continue;
			current = Path.Combine(current, segment);
			if (!Directory.Exists(current)) continue;
			var info = new DirectoryInfo(current);
			if ((info.Attributes & FileAttributes.ReparsePoint) == 0) continue;
			var target = info.ResolveLinkTarget(true);
			if (target is not DirectoryInfo || !target.Exists) throw new IOException("A managed directory link is unavailable.");
			current = Path.GetFullPath(target.FullName);
		}
		return Path.TrimEndingDirectorySeparator(current);
	}
	private static string StagePath(Journal journal, DisplacedFile entry) => Path.Combine(
		entry.FromMods ? journal.ModsTarget : journal.HoldingTarget, ".redux-duplicate-" + journal.Id, entry.File.FileName);
	private static string BackupPath(string directory, DisplacedFile entry) => Path.Combine(directory, entry.FromMods ? "Mods" : "Held", entry.File.FileName);
	private static void RemoveEmptyStages(Journal journal)
	{
		foreach (var folder in journal.Files.Select(entry => Path.GetDirectoryName(StagePath(journal, entry))).Distinct())
			if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
	}
	private static bool Same(string first, string second) => String.Equals(Path.TrimEndingDirectorySeparator(first), Path.TrimEndingDirectorySeparator(second), StringComparison.OrdinalIgnoreCase);
	private static bool Overlaps(string first, string second) => Same(first, second)
		|| first.StartsWith(Path.TrimEndingDirectorySeparator(second) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
		|| second.StartsWith(Path.TrimEndingDirectorySeparator(first) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	private static bool ValidName(string name) => !String.IsNullOrWhiteSpace(name) && name == Path.GetFileName(name)
		&& name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
	private static void WriteJournal(string path, Journal journal)
	{
		using (var file = new FileStream(path + ".tmp", FileMode.CreateNew, FileAccess.Write, FileShare.None))
		{
			JsonSerializer.Serialize(file, journal);
			file.Flush(true);
		}
		File.Move(path + ".tmp", path);
	}
}
