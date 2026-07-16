using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;

using Newtonsoft.Json;

using System.Buffers.Binary;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;

namespace DivinityModManager.AppServices;

/// <summary>
/// Resolves Nexus project identity from Redux's bundled mod database.
/// Installed pak and downloaded archive fingerprints are deliberately indexed
/// separately because they represent different bytes and hash algorithms.
/// </summary>
public static class ReduxModDatabaseService
{
	private const string DATABASE_FILE_NAME = "ReduxModDatabase.json";
	private static readonly Lazy<ReduxModDatabaseIndex> _index = new(LoadIndex, true);

	public static int ExactPakCount => _index.Value.ExactPakCount;
	public static int ExactArchiveCount => _index.Value.ExactArchiveCount;
	public static ReduxModDatabaseMatch TryResolveProject(long modId) => CreateMatch(modId, -1, ReduxOfflineMatchKind.Unknown);

	public static bool CouldMatchPak(string filePath) => CouldMatchBySize(filePath, ".pak", _index.Value.PaksBySize);
	public static bool CouldMatchArchive(string filePath) => CouldMatchBySize(filePath, null, _index.Value.ArchivesBySize);

	public static async Task<ReduxModDatabaseMatch> TryResolvePakAsync(string filePath, CancellationToken cancellationToken)
	{
		if (!CouldMatchPak(filePath)) return null;
		var file = new FileInfo(filePath);
		var originalLength = file.Length;
		if (!_index.Value.PaksBySize.TryGetValue(originalLength, out var candidates)) return null;

		var hash = await ComputeExactPakHashAsync(filePath, cancellationToken);
		file.Refresh();
		if (!file.Exists || file.Length != originalLength)
			throw new IOException($"The pak changed while Redux was identifying it: {filePath}");

		return candidates.TryGetValue(hash, out var fingerprint)
			? CreateMatch(fingerprint.ModId, fingerprint.FileId, ReduxOfflineMatchKind.ExactPak, fingerprint)
			: null;
	}

	public static async Task<ReduxModDatabaseMatch> TryResolveArchiveAsync(string filePath, CancellationToken cancellationToken)
	{
		if (!CouldMatchArchive(filePath)) return null;
		var file = new FileInfo(filePath);
		var originalLength = file.Length;
		if (!_index.Value.ArchivesBySize.TryGetValue(originalLength, out var candidates)) return null;

		var md5 = await ComputeArchiveMd5Async(filePath, cancellationToken);
		file.Refresh();
		if (!file.Exists || file.Length != originalLength)
			throw new IOException($"The archive changed while Redux was identifying it: {filePath}");

		return candidates.TryGetValue(md5, out var fingerprint)
			? CreateMatch(fingerprint.ModId, fingerprint.FileId, ReduxOfflineMatchKind.ExactArchive, fingerprint)
			: null;
	}

	/// <summary>
	/// Returns a conservative project-level association. A UUID must have a reviewed
	/// identity record, or normalized name and author must converge on one project.
	/// Name-only candidates are intentionally ignored.
	/// </summary>
	public static ReduxModDatabaseMatch TryResolveIdentity(DivinityModData mod)
	{
		if (mod == null) return null;
		if (!String.IsNullOrWhiteSpace(mod.UUID)
			&& _index.Value.ModulesByUuid.TryGetValue(mod.UUID.Trim(), out var module)
			&& _index.Value.ProjectsById.ContainsKey(module.ModId))
		{
			return CreateMatch(module.ModId, -1, ReduxOfflineMatchKind.ModuleIdentity);
		}

		var author = Normalize(mod.Author);
		if (String.IsNullOrWhiteSpace(author)) return null;

		var candidateIds = new HashSet<long>();
		foreach (var value in new[] { mod.Name, mod.DisplayName, mod.Folder, Path.GetFileNameWithoutExtension(mod.FileName) })
		{
			var normalized = Normalize(value);
			if (!String.IsNullOrWhiteSpace(normalized)
				&& _index.Value.ProjectIdsByAlias.TryGetValue(normalized, out var ids))
			{
				candidateIds.UnionWith(ids);
			}
		}

		var agreed = candidateIds
			.Where(id => _index.Value.ProjectAuthors.TryGetValue(id, out var authors) && authors.Contains(author))
			.Distinct()
			.ToList();
		return agreed.Count == 1
			? CreateMatch(agreed[0], -1, ReduxOfflineMatchKind.NameAndAuthor)
			: null;
	}

	internal static async Task<string> ComputeExactPakHashAsync(string filePath, CancellationToken cancellationToken)
	{
		await using var stream = OpenSequentialRead(filePath);
		var hasher = new XxHash64();
		await hasher.AppendAsync(stream, cancellationToken);
		var littleEndianHash = new byte[sizeof(ulong)];
		BinaryPrimitives.WriteUInt64LittleEndian(littleEndianHash, hasher.GetCurrentHashAsUInt64());
		return Convert.ToBase64String(littleEndianHash);
	}

	internal static async Task<string> ComputeArchiveMd5Async(string filePath, CancellationToken cancellationToken)
	{
		await using var stream = OpenSequentialRead(filePath);
		var hash = await MD5.HashDataAsync(stream, cancellationToken);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	private static FileStream OpenSequentialRead(string filePath) => new(
		filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
		FileOptions.Asynchronous | FileOptions.SequentialScan);

	private static bool CouldMatchBySize<T>(string filePath, string requiredExtension, Dictionary<long, Dictionary<string, T>> index)
	{
		if (String.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
		if (requiredExtension != null && !filePath.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase)) return false;
		try { return index.ContainsKey(new FileInfo(filePath).Length); }
		catch { return false; }
	}

	private static ReduxModDatabaseMatch CreateMatch(long modId, long fileId, ReduxOfflineMatchKind kind, IReduxFingerprint fingerprint = null)
	{
		if (!_index.Value.ProjectsById.TryGetValue(modId, out var project)) return null;
		return new ReduxModDatabaseMatch(project, fileId, kind, fingerprint);
	}

	private static string Normalize(string value)
	{
		if (String.IsNullOrWhiteSpace(value)) return String.Empty;
		var builder = new StringBuilder(value.Length);
		foreach (var character in value.Normalize(NormalizationForm.FormD))
		{
			if (Char.IsLetterOrDigit(character)) builder.Append(Char.ToLowerInvariant(character));
		}
		return builder.ToString();
	}

	private static ReduxModDatabaseIndex LoadIndex()
	{
		try
		{
			var path = Path.Combine(AppContext.BaseDirectory, "Resources", DATABASE_FILE_NAME);
			if (!File.Exists(path))
			{
				DivinityApp.Log($"Bundled Redux mod database was not found at '{path}'.");
				return ReduxModDatabaseIndex.Empty;
			}
			var database = JsonConvert.DeserializeObject<ReduxModDatabase>(File.ReadAllText(path));
			if (database?.SchemaVersion != 1) return ReduxModDatabaseIndex.Empty;
			var index = new ReduxModDatabaseIndex(database);
			DivinityApp.Log($"Loaded Redux mod database: {index.ExactPakCount} exact paks, {index.ExactArchiveCount} exact archives, {index.ProjectsById.Count} Nexus projects.");
			return index;
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Failed to load the bundled Redux mod database:\n{ex}");
			return ReduxModDatabaseIndex.Empty;
		}
	}

	private sealed class ReduxModDatabaseIndex
	{
		public static ReduxModDatabaseIndex Empty { get; } = new(new ReduxModDatabase());
		public Dictionary<long, ReduxProjectRecord> ProjectsById { get; }
		public Dictionary<long, Dictionary<string, ReduxPakFingerprint>> PaksBySize { get; }
		public Dictionary<long, Dictionary<string, ReduxArchiveFingerprint>> ArchivesBySize { get; }
		public Dictionary<string, ReduxModuleIdentity> ModulesByUuid { get; }
		public Dictionary<string, HashSet<long>> ProjectIdsByAlias { get; } = new(StringComparer.Ordinal);
		public Dictionary<long, HashSet<string>> ProjectAuthors { get; } = new();
		public int ExactPakCount => PaksBySize.Values.Sum(group => group.Count);
		public int ExactArchiveCount => ArchivesBySize.Values.Sum(group => group.Count);

		public ReduxModDatabaseIndex(ReduxModDatabase database)
		{
			ProjectsById = (database.Projects ?? new()).Where(p => p.ModId > 0).GroupBy(p => p.ModId).ToDictionary(g => g.Key, g => g.First());
			PaksBySize = UniqueFingerprintIndex(database.ExactPakFingerprints, item => item.Size, item => item.Hash);
			ArchivesBySize = UniqueFingerprintIndex(database.ExactArchiveFingerprints, item => item.Size, item => item.Md5?.ToLowerInvariant());
			ModulesByUuid = (database.ModuleIdentities ?? new()).Where(m => !String.IsNullOrWhiteSpace(m.Uuid)).GroupBy(m => m.Uuid, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(m => m.ModId).Distinct().Count() == 1).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
			foreach (var project in ProjectsById.Values)
			{
				foreach (var alias in new[] { project.Name }.Concat(project.Aliases ?? new()))
				{
					var key = Normalize(alias);
					if (key.Length < 4) continue;
					if (!ProjectIdsByAlias.TryGetValue(key, out var ids)) ProjectIdsByAlias[key] = ids = new();
					ids.Add(project.ModId);
				}
				ProjectAuthors[project.ModId] = (project.Authors ?? new()).Select(Normalize).Where(a => a.Length > 0).ToHashSet(StringComparer.Ordinal);
			}
		}

		private static Dictionary<long, Dictionary<string, T>> UniqueFingerprintIndex<T>(IEnumerable<T> values, Func<T, long> size, Func<T, string> hash)
		{
			return (values ?? Enumerable.Empty<T>()).Where(v => size(v) > 0 && !String.IsNullOrWhiteSpace(hash(v)))
				.GroupBy(size).ToDictionary(g => g.Key, g => g.GroupBy(hash, StringComparer.OrdinalIgnoreCase)
				.Where(matches => matches.Count() == 1).ToDictionary(matches => matches.Key, matches => matches.Single(), StringComparer.OrdinalIgnoreCase));
		}
	}
}

public enum ReduxOfflineMatchKind { Unknown = 0, ExactPak = 1, ExactArchive = 2, ModuleIdentity = 3, NameAndAuthor = 4 }

public sealed class ReduxModDatabaseMatch
{
	public ReduxProjectRecord Project { get; }
	public long FileId { get; }
	public ReduxOfflineMatchKind Kind { get; }
	private readonly IReduxFingerprint _fingerprint;
	public long ModId => Project.ModId;

	internal ReduxModDatabaseMatch(ReduxProjectRecord project, long fileId, ReduxOfflineMatchKind kind, IReduxFingerprint fingerprint)
	{ Project = project; FileId = fileId; Kind = kind; _fingerprint = fingerprint; }

	public NexusModsModData CreateMetadata(string uuid)
	{
		Uri.TryCreate(_fingerprint?.PictureUrl ?? Project.PictureUrl, UriKind.Absolute, out var picture);
		return new NexusModsModData
		{
			UUID = uuid, ModId = ModId, LastFileId = FileId,
			Name = !String.IsNullOrWhiteSpace(_fingerprint?.Name) ? _fingerprint.Name : Project.Name,
			Author = !String.IsNullOrWhiteSpace(_fingerprint?.Author) ? _fingerprint.Author : Project.Authors?.FirstOrDefault(),
			UploadedBy = Project.UploadedBy,
			Version = _fingerprint?.Version, PictureUrl = picture, Available = true,
			MetadataOrigin = NexusMetadataOrigin.BundledProvenance,
			OfflineMatchKind = Kind
		};
	}
}

internal interface IReduxFingerprint { string Name { get; } string Author { get; } string Version { get; } string PictureUrl { get; } }
internal sealed class ReduxModDatabase { [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } [JsonProperty("projects")] public List<ReduxProjectRecord> Projects { get; set; } = new(); [JsonProperty("exactPakFingerprints")] public List<ReduxPakFingerprint> ExactPakFingerprints { get; set; } = new(); [JsonProperty("exactArchiveFingerprints")] public List<ReduxArchiveFingerprint> ExactArchiveFingerprints { get; set; } = new(); [JsonProperty("moduleIdentities")] public List<ReduxModuleIdentity> ModuleIdentities { get; set; } = new(); }
public sealed class ReduxProjectRecord { [JsonProperty("modId")] public long ModId { get; set; } [JsonProperty("name")] public string Name { get; set; } [JsonProperty("authors")] public List<string> Authors { get; set; } = new(); [JsonProperty("uploadedBy")] public string UploadedBy { get; set; } [JsonProperty("aliases")] public List<string> Aliases { get; set; } = new(); [JsonProperty("pictureUrl")] public string PictureUrl { get; set; } }
internal sealed class ReduxPakFingerprint : IReduxFingerprint { [JsonProperty("hash")] public string Hash { get; set; } [JsonProperty("size")] public long Size { get; set; } [JsonProperty("modId")] public long ModId { get; set; } [JsonProperty("fileId")] public long FileId { get; set; } [JsonProperty("name")] public string Name { get; set; } [JsonProperty("author")] public string Author { get; set; } [JsonProperty("version")] public string Version { get; set; } [JsonProperty("pictureUrl")] public string PictureUrl { get; set; } }
internal sealed class ReduxArchiveFingerprint : IReduxFingerprint { [JsonProperty("md5")] public string Md5 { get; set; } [JsonProperty("size")] public long Size { get; set; } [JsonProperty("modId")] public long ModId { get; set; } [JsonProperty("fileId")] public long FileId { get; set; } [JsonProperty("name")] public string Name { get; set; } [JsonProperty("author")] public string Author { get; set; } [JsonProperty("version")] public string Version { get; set; } public string PictureUrl => null; }
internal sealed class ReduxModuleIdentity { [JsonProperty("uuid")] public string Uuid { get; set; } [JsonProperty("modId")] public long ModId { get; set; } }
