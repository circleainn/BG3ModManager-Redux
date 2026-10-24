#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

#if REDUX_UPDATER
namespace ReduxUpdater;
#else
namespace DivinityModManager.AppServices;
#endif

// Shared with the external runner: snapshot restores never replace a running Redux process.
public sealed class ReduxSnapshotStore
{
	public sealed record Scope(string Id, string Directory, bool ProfileLoadOrdersOnly = false);
	public sealed record StoredFile(string Scope, string Path, long Length, string Sha256);
	public sealed record Manifest(int SchemaVersion, string Name, string Version, DateTimeOffset CreatedUtc,
		bool IncludeMods, Scope[] Scopes, StoredFile[] Files);
	public sealed record Item(string FileName, string Name, string Version, DateTimeOffset CreatedUtc,
		bool IncludeMods, long Bytes, string Error = "")
	{
		public string Summary => Error.Length > 0 ? Error : $"{Version} · {CreatedUtc.ToLocalTime():g} · {Bytes / 1048576d:N1} MB · {(IncludeMods ? "Redux + mods and mod lists" : "Redux only")}";
	}
	public sealed record Request(int SchemaVersion, string Operation, int ParentProcessId, string ApplicationDirectory,
		Scope[] Scopes, string SnapshotFileName, string Name, string Version, bool IncludeMods, string ResultPath);
	private sealed record Journal(int SchemaVersion, string BeforeSnapshot, Scope[] Scopes, bool IncludeMods);
	public sealed record Progress(string Message, long Current = 0, long Total = 0, bool CanCancel = true);
	private const string ManifestName = "Redux-Snapshot.json";
	private const int MaximumFiles = 250000;
	private static readonly JsonSerializerOptions Json = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 12
	};
	private static readonly string[] ExcludedReduxFolders = ["Backups", "_Logs", "Logs", "Cache", "_Cache", "Temp", ".git"];
	public string ApplicationDirectory { get; }
	public string DirectoryPath => Path.Combine(ApplicationDirectory, "Backups", "Snapshots");
	private string JournalPath => Path.Combine(DirectoryPath, "pending-restore.json");
	public bool NeedsRecovery => File.Exists(JournalPath);

	public ReduxSnapshotStore(string applicationDirectory)
	{
		ApplicationDirectory = SafeRoot(applicationDirectory);
		CheckLinks(DirectoryPath);
	}

	public static void WriteRequest(string path, Request request) => WriteJson(path, request);
	public static Request ReadRequest(string path) => ReadJson<Request>(path, 1024 * 1024);

	public IReadOnlyList<Item> List()
	{
		CheckLinks(DirectoryPath);
		if (!Directory.Exists(DirectoryPath)) return [];
		var items = new List<Item>();
		foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.zip"))
		{
			try
			{
				using var zip = OpenArchive(path);
				var manifest = ReadManifest(zip);
				items.Add(new(Path.GetFileName(path), manifest.Name, manifest.Version, manifest.CreatedUtc,
					manifest.IncludeMods, new FileInfo(path).Length));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
			{
				items.Add(new(Path.GetFileName(path), Path.GetFileName(path), "", DateTimeOffset.MinValue, false, 0,
					"Unavailable: " + ex.Message));
			}
		}
		return items.OrderByDescending(x => x.CreatedUtc).ToArray();
	}

	public async Task<string> CreateAsync(string name, string version, bool includeMods, Scope[] scopes,
		IProgress<Progress>? progress = null, CancellationToken token = default)
	{
		using var gate = Acquire();
		if (NeedsRecovery) throw new IOException("Recover the interrupted restore before creating another snapshot.");
		return await CaptureAsync(name, version, includeMods, scopes, progress, token);
	}

	private async Task<string> CaptureAsync(string name, string version, bool includeMods, Scope[] scopes,
		IProgress<Progress>? progress, CancellationToken token)
	{
		ValidateScopes(scopes, includeMods);
		if (!File.Exists(Path.Combine(ApplicationDirectory, "Redux.exe")) || !File.Exists(Path.Combine(ApplicationDirectory, "Redux.dll")))
			throw new IOException("The Redux application files are missing; a restorable snapshot cannot be created.");
		name = name.Trim();
		if (name.Length is 0 or > 100 || name.Any(Char.IsControl)) throw new InvalidDataException("Use a snapshot name of 1–100 characters.");
		if (String.IsNullOrWhiteSpace(version) || version.Length > 64) throw new InvalidDataException("The Redux version is invalid.");
		var filename = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip";
		var pending = Path.Combine(DirectoryPath, filename + ".building");
		var excluded = Exclusions(includeMods);
		try
		{
			await ReduxPreUpdateBackupService.CreateAsync(pending,
				scopes.Select(s => new ReduxPreUpdateBackupService.Source(s.Id, s.Directory, s.ProfileLoadOrdersOnly)),
				new InlineProgress<ReduxPreUpdateBackupService.Progress>(p => progress?.Report(new(Localization.ReduxLanguageService.Current.Format($"Copying {p.CurrentFile}"), p.CopiedBytes, p.TotalBytes))), token, excluded,
				async (copiedFiles, cancellation) =>
				{
					if (copiedFiles.Count > MaximumFiles) throw new IOException("The snapshot contains too many files.");
					var files = new List<StoredFile>();
					foreach (var file in copiedFiles)
					{
						cancellation.ThrowIfCancellationRequested();
						var split = file.ArchivePath.IndexOf('/');
						var scope = scopes.Single(s => s.Id == file.ArchivePath[..split]);
						var relative = file.ArchivePath[(split + 1)..];
						var source = Contained(scope.Directory, relative);
						progress?.Report(new(Localization.ReduxLanguageService.Current.Format($"Checking source {file.ArchivePath}")));
						if (new FileInfo(source).Length != file.Length || await HashAsync(source, cancellation) != file.Sha256)
							throw new IOException("A file changed during backup. Close the game and finish file operations, then retry.");
						files.Add(new(scope.Id, relative, file.Length, file.Sha256));
					}
					var actual = Enumerate(scopes, includeMods).Select(f => f.Scope + "/" + f.Relative).Order(StringComparer.OrdinalIgnoreCase);
					if (!actual.SequenceEqual(files.Select(f => f.Scope + "/" + f.Path).Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
						throw new IOException("The snapshot folders changed during backup. Retry after other operations finish.");
					return JsonSerializer.SerializeToUtf8Bytes(new Manifest(1, name, version, DateTimeOffset.UtcNow, includeMods, scopes, files.ToArray()), Json);
				});
			// Verify the completed archive without Update mode or whole-PAK memory buffers.
			using (var zip = ZipFile.OpenRead(pending))
			{
				var manifest = ReadManifest(zip);
				foreach (var file in manifest.Files)
				{
					progress?.Report(new(Localization.ReduxLanguageService.Current.Format($"Verifying archive {file.Path}")));
					var entry = zip.GetEntry(file.Scope + "/" + file.Path)!;
					await using var encoded = entry.Open();
					var digest = Convert.ToHexString(await SHA256.HashDataAsync(encoded, token));
					if (digest != file.Sha256) throw new InvalidDataException("Snapshot archive verification failed.");
				}
			}
			token.ThrowIfCancellationRequested();
			File.Move(pending, Path.Combine(DirectoryPath, filename));
			return filename;
		}
		finally { if (File.Exists(pending)) File.Delete(pending); }
	}

	public async Task RestoreAsync(string filename, Scope[] scopes, string currentVersion,
		IProgress<Progress>? progress = null, CancellationToken token = default)
	{
		using var gate = Acquire();
		if (NeedsRecovery) throw new IOException("Recover the interrupted restore before restoring another snapshot.");
		using var zip = OpenArchive(SnapshotPath(filename));
		var manifest = ReadManifest(zip);
		ValidateRestoreScopes(manifest, scopes);
		var stage = NewStage();
		try
		{
			await StageAsync(zip, manifest, stage, progress, token);
			var before = await CaptureAsync("Before restoring " + manifest.Name[..Math.Min(70, manifest.Name.Length)],
				currentVersion, manifest.IncludeMods, scopes, progress, token);
			token.ThrowIfCancellationRequested();
			WriteJson(JournalPath, new Journal(1, before, scopes, manifest.IncludeMods));
			try
			{
				progress?.Report(new(Localization.ReduxLanguageService.Current.Translate("Restoring snapshot. Keep this window open."), CanCancel: false));
				Apply(manifest, scopes, stage);
				File.Delete(JournalPath);
			}
			catch
			{
				DeleteStage(stage); // Release candidate staging space before building rollback staging.
				await RecoverCoreAsync(scopes, progress);
				throw;
			}
		}
		finally { DeleteStage(stage); }
	}

	public async Task RecoverAsync(Scope[] scopes, IProgress<Progress>? progress = null)
	{
		using var gate = Acquire();
		await RecoverCoreAsync(scopes, progress);
	}

	private async Task RecoverCoreAsync(Scope[] scopes, IProgress<Progress>? progress)
	{
		if (!NeedsRecovery) return;
		var journal = ReadJson<Journal>(JournalPath, 1024 * 1024);
		if (journal.SchemaVersion != 1 || !SameScopes(journal.Scopes, scopes))
			throw new IOException("Recovery paths differ from the interrupted restore. Use its original recovery request.");
		using var zip = OpenArchive(SnapshotPath(journal.BeforeSnapshot));
		var manifest = ReadManifest(zip);
		ValidateRestoreScopes(manifest, scopes);
		var stage = NewStage();
		try
		{
			progress?.Report(new(Localization.ReduxLanguageService.Current.Translate("Recovering the previous installation. Keep this window open."), CanCancel: false));
			await StageAsync(zip, manifest, stage, null, CancellationToken.None);
			Apply(manifest, scopes, stage);
			File.Delete(JournalPath);
		}
		finally { DeleteStage(stage); }
	}

	public Scope[] RecoveryScopes() => ReadJson<Journal>(JournalPath, 1024 * 1024).Scopes;

	public Manifest Inspect(string filename)
	{
		using var zip = OpenArchive(SnapshotPath(filename));
		return ReadManifest(zip);
	}

	private void Apply(Manifest manifest, Scope[] scopes, string stage)
	{
		ValidateRestoreScopes(manifest, scopes);
		var existing = Enumerate(scopes, manifest.IncludeMods).ToArray();
		var wanted = manifest.Files.Select(f => f.Scope + "/" + f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
		// Check all destinations before the first mutation, then recheck at every write.
		foreach (var file in manifest.Files) Contained(scopes.Single(s => s.Id == file.Scope).Directory, file.Path);
		foreach (var file in manifest.Files)
		{
			var target = Contained(scopes.Single(s => s.Id == file.Scope).Directory, file.Path);
			if (File.Exists(target) && new FileInfo(target).Length == file.Length && HashAsync(target, default).GetAwaiter().GetResult() == file.Sha256) continue;
			Directory.CreateDirectory(Path.GetDirectoryName(target)!);
			var temporary = target + ".redux-snapshot-" + Guid.NewGuid().ToString("N");
			try
			{
				File.Copy(Contained(stage, file.Scope + "/" + file.Path), temporary, false);
				CheckLinks(target);
				File.Move(temporary, target, true);
			}
			finally { if (File.Exists(temporary)) File.Delete(temporary); }
		}
		foreach (var file in existing.Where(f => !wanted.Contains(f.Scope + "/" + f.Relative)))
		{
			CheckLinks(file.FullPath);
			File.Delete(file.FullPath);
		}
	}

	private static async Task StageAsync(ZipArchive zip, Manifest manifest, string stage, IProgress<Progress>? progress, CancellationToken token)
	{
		long completed = 0;
		var total = manifest.Files.Sum(f => f.Length);
		foreach (var file in manifest.Files)
		{
			token.ThrowIfCancellationRequested();
			var path = Contained(stage, file.Scope + "/" + file.Path);
			var entry = zip.GetEntry(file.Scope + "/" + file.Path)!;
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			progress?.Report(new(Localization.ReduxLanguageService.Current.Format($"Verifying {file.Path}"), completed, total));
			await using (var source = entry.Open())
			await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
			{
				var buffer = new byte[1024 * 1024];
				long written = 0;
				int count;
				while ((count = await source.ReadAsync(buffer, token)) > 0)
				{
					written += count;
					if (written > file.Length) throw new InvalidDataException("Snapshot file exceeds its recorded size.");
					await target.WriteAsync(buffer.AsMemory(0, count), token);
				}
				if (written != file.Length) throw new InvalidDataException("Snapshot file is incomplete.");
			}
			if (await HashAsync(path, token) != file.Sha256) throw new InvalidDataException("Snapshot verification failed: " + file.Path);
			completed += file.Length;
		}
	}

	private static Manifest ReadManifest(ZipArchive zip)
	{
		if (zip.Entries.Count > MaximumFiles + 2 || zip.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != zip.Entries.Count)
			throw new InvalidDataException("The snapshot contains duplicate or excessive entries.");
		var entry = zip.GetEntry(ManifestName) ?? throw new InvalidDataException("This is not a Redux snapshot.");
		if (entry.Length > 64 * 1024 * 1024) throw new InvalidDataException("Snapshot metadata is too large.");
		using var stream = entry.Open();
		using var document = JsonDocument.Parse(ReadBounded(stream, 64 * 1024 * 1024), new JsonDocumentOptions { MaxDepth = 12 });
		RejectDuplicateProperties(document.RootElement);
		var manifest = document.RootElement.Deserialize<Manifest>(Json) ?? throw new InvalidDataException("Snapshot metadata is empty.");
		if (manifest.SchemaVersion != 1 || manifest.Files == null || manifest.Scopes == null || manifest.Scopes.Length is < 1 or > 5
			|| manifest.Files.Length is < 1 or > MaximumFiles || String.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 100
			|| String.IsNullOrWhiteSpace(manifest.Version) || manifest.Version.Length > 64
			|| manifest.Scopes.Any(s => s == null || String.IsNullOrWhiteSpace(s.Id) || String.IsNullOrWhiteSpace(s.Directory))
			|| manifest.Name.Any(Char.IsControl) || manifest.Version.Any(Char.IsControl) || manifest.Files.Any(f => f == null)
			|| manifest.Scopes.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Scopes.Length)
			throw new InvalidDataException("Snapshot metadata is invalid.");
		var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ManifestName, "RESTORE.txt" };
		foreach (var file in manifest.Files)
		{
			ValidateRelative(file.Path);
			if (!manifest.Scopes.Any(s => s.Id == file.Scope) || file.Length < 0 || file.Length > 1024L * 1024 * 1024 * 1024
				|| file.Sha256 is not { Length: 64 } || !file.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid snapshot file metadata.");
			var key = file.Scope + "/" + file.Path;
			if (!paths.Add(key) || zip.GetEntry(key)?.Length != file.Length) throw new InvalidDataException("Snapshot inventory is incomplete or duplicated.");
		}
		if (!paths.SetEquals(zip.Entries.Select(e => e.FullName))) throw new InvalidDataException("Snapshot inventory does not match the archive.");
		if (!paths.Contains("Redux/Redux.exe") || !paths.Contains("Redux/Redux.dll")) throw new InvalidDataException("The snapshot is missing the Redux application.");
		return manifest;
	}

	private void ValidateRestoreScopes(Manifest manifest, Scope[] scopes)
	{
		ValidateScopes(scopes, manifest.IncludeMods);
		if (manifest.Scopes.Length != scopes.Length || manifest.Scopes.Any(old => !scopes.Any(now => old.Id == now.Id
			&& old.ProfileLoadOrdersOnly == now.ProfileLoadOrdersOnly && (old.Id == "Redux" || String.Equals(Path.GetFullPath(old.Directory), now.Directory, StringComparison.OrdinalIgnoreCase)))))
			throw new InvalidDataException("This snapshot uses different mod or order folders. Restore the original folder settings before continuing.");
		foreach (var file in manifest.Files)
		{
			var scope = scopes.Single(s => s.Id == file.Scope);
			var path = Contained(scope.Directory, file.Path);
			if (!Included(scope, path, manifest.IncludeMods)) throw new InvalidDataException("Snapshot tries to replace excluded files.");
		}
	}

	private void ValidateScopes(Scope[] scopes, bool includeMods)
	{
		if (scopes is null || scopes.Length is < 1 or > 5 || scopes.Any(s => s == null) || scopes.Count(s => s.Id == "Redux") != 1
			|| scopes.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != scopes.Length)
			throw new InvalidDataException("Invalid snapshot folders.");
		foreach (var scope in scopes)
		{
			if (scope.Id is not ("Redux" or "Mods" or "Orders" or "Profiles") || (!includeMods && scope.Id is "Mods" or "Profiles")
				|| scope.ProfileLoadOrdersOnly != (scope.Id == "Profiles")) throw new InvalidDataException("Unsupported snapshot folder.");
			var root = SafeRoot(scope.Directory);
			if (scope.Id == "Redux" && !String.Equals(root, ApplicationDirectory, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("Snapshot belongs to a different Redux folder.");
			if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Snapshot folder is unavailable: " + root);
			if (scopes.Any(other => other != scope && (Within(root, other.Directory) || Within(other.Directory, root)
				|| String.Equals(root, other.Directory, StringComparison.OrdinalIgnoreCase)))) throw new InvalidDataException("Snapshot folders overlap.");
		}
		if (includeMods && (!scopes.Any(s => s.Id == "Mods") || !scopes.Any(s => s.Id == "Profiles")))
			throw new InvalidDataException("Choose valid BG3 Mods and profile folders before including mods.");
	}

	private IEnumerable<(string Scope, string Relative, string FullPath)> Enumerate(Scope[] scopes, bool includeMods)
	{
		foreach (var scope in scopes)
		{
			var pending = new Stack<string>();
			pending.Push(scope.Directory);
			while (pending.Count > 0)
			{
				var directory = pending.Pop();
				CheckLinks(directory);
				foreach (var child in Directory.EnumerateDirectories(directory))
				{
					if (scope.Id == "Redux" && Exclusions(includeMods).Any(x => EqualOrWithin(child, x))) continue;
					if (scope.ProfileLoadOrdersOnly && !String.Equals(directory, scope.Directory, StringComparison.OrdinalIgnoreCase)) continue;
					pending.Push(child);
				}
				foreach (var file in Directory.EnumerateFiles(directory))
				{
					if (!Included(scope, file, includeMods)) continue;
					CheckLinks(file);
					yield return (scope.Id, Path.GetRelativePath(scope.Directory, file).Replace('\\', '/'), file);
				}
			}
		}
	}

	private bool Included(Scope scope, string path, bool includeMods)
	{
		if (scope.Id == "Redux" && Exclusions(includeMods).Any(x => EqualOrWithin(path, x))) return false;
		if (!scope.ProfileLoadOrdersOnly) return true;
		var relative = Path.GetRelativePath(scope.Directory, path).Replace('\\', '/');
		return relative.Equals("playerprofiles8.lsx", StringComparison.OrdinalIgnoreCase)
			|| (relative.Split('/').Length == 2 && Path.GetFileName(path).Equals("modsettings.lsx", StringComparison.OrdinalIgnoreCase));
	}

	private string[] Exclusions(bool includeMods) => ExcludedReduxFolders.Select(x => Path.Combine(ApplicationDirectory, x))
		.Concat(new[] { Path.Combine(ApplicationDirectory, "Data", "GameDirectoryInstalls") })
		.Concat(includeMods ? [] : new[] { Path.Combine(ApplicationDirectory, "Data", "OverrideOrderHolding") }).ToArray();
	private FileStream Acquire()
	{
		CheckLinks(DirectoryPath);
		Directory.CreateDirectory(DirectoryPath);
		return new FileStream(Contained(DirectoryPath, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
	}
	private string SnapshotPath(string filename)
	{
		if (filename != Path.GetFileName(filename) || !filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("Select a snapshot from this Redux installation.");
		return Contained(DirectoryPath, filename);
	}
	private string NewStage()
	{
		var path = Contained(DirectoryPath, ".work-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
	private void DeleteStage(string path)
	{
		if (!Within(path, DirectoryPath) || !Path.GetFileName(path).StartsWith(".work-", StringComparison.Ordinal)) return;
		try { CheckLinks(path); Directory.Delete(path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
	}
	private static ZipArchive OpenArchive(string path)
	{
		CheckLinks(path);
		return new ZipArchive(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read);
	}
	private static async Task<string> HashAsync(string path, CancellationToken token)
	{
		CheckLinks(path);
		await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
		return Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
	}
	private static bool SameScopes(Scope[] a, Scope[] b) => a.Length == b.Length && a.All(x => b.Any(y => x.Id == y.Id
		&& x.ProfileLoadOrdersOnly == y.ProfileLoadOrdersOnly && String.Equals(x.Directory, y.Directory, StringComparison.OrdinalIgnoreCase)));
	private static bool Within(string path, string root) => Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	private static bool EqualOrWithin(string path, string root) => String.Equals(Path.GetFullPath(path), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || Within(path, root);
	private static string SafeRoot(string path)
	{
		if (String.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new InvalidDataException("Snapshot folders must be absolute paths.");
		var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
		if (root == Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root)!)
			|| String.Equals(root, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase)
			|| EqualOrWithin(root, Environment.GetFolderPath(Environment.SpecialFolder.Windows))) throw new InvalidDataException("Unsafe snapshot folder.");
		CheckLinks(root);
		return root;
	}
	private static string Contained(string root, string relative)
	{
		ValidateRelative(relative);
		var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
		if (!Within(path, root)) throw new InvalidDataException("Snapshot path escapes its folder.");
		CheckLinks(path);
		return path;
	}
	private static void ValidateRelative(string path)
	{
		if (String.IsNullOrWhiteSpace(path) || path.Contains('\\') || Path.IsPathRooted(path)) throw new InvalidDataException("Invalid snapshot path.");
		foreach (var part in path.Split('/'))
		{
			var stem = part.Split('.')[0].ToUpperInvariant();
			if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
				|| part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.Any(Char.IsControl)
				|| stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && Char.IsDigit(stem[3])))
				throw new InvalidDataException("Invalid snapshot filename.");
		}
	}
	private static void CheckLinks(string path)
	{
		for (var current = Path.GetFullPath(path); !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
			if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
				throw new IOException("Snapshot paths must not contain symbolic links or junctions: " + current);
	}
	private static T ReadJson<T>(string path, int maximum)
	{
		CheckLinks(path);
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		using var document = JsonDocument.Parse(ReadBounded(stream, maximum), new JsonDocumentOptions { MaxDepth = 12 });
		RejectDuplicateProperties(document.RootElement);
		return document.RootElement.Deserialize<T>(Json) ?? throw new InvalidDataException("Snapshot metadata is empty.");
	}
	private static byte[] ReadBounded(Stream stream, int maximum)
	{
		using var output = new MemoryStream();
		var buffer = new byte[8192];
		int count;
		while ((count = stream.Read(buffer)) > 0)
		{
			if (output.Length + count > maximum) throw new InvalidDataException("Snapshot metadata is too large.");
			output.Write(buffer, 0, count);
		}
		return output.ToArray();
	}
	private static void RejectDuplicateProperties(JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.Object)
		{
			var names = new HashSet<string>(StringComparer.Ordinal);
			foreach (var property in value.EnumerateObject())
			{
				if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate snapshot metadata property.");
				RejectDuplicateProperties(property.Value);
			}
		}
		else if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) RejectDuplicateProperties(child);
	}
	private static void WriteJson<T>(string path, T value)
	{
		CheckLinks(path);
		var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				JsonSerializer.Serialize(file, value, Json);
				file.Flush(true);
			}
			File.Move(temporary, path, true);
		}
		finally { if (File.Exists(temporary)) File.Delete(temporary); }
	}
	private sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
}
