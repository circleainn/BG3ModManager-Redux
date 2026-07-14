using DivinityModManager.Models.NexusMods;

using Newtonsoft.Json;

using System.Buffers.Binary;
using System.IO.Hashing;

namespace DivinityModManager.AppServices;

/// <summary>
/// Resolves an installed pak to a known Nexus Mods project using an exact
/// xxHash64 match. The bundled index is only a provenance fallback; live
/// Nexus metadata remains authoritative when API access is available.
/// </summary>
public static class NexusPakProvenanceService
{
	private const string DATABASE_FILE_NAME = "NexusPakProvenance.json";

	private static readonly Lazy<NexusPakProvenanceIndex> _index = new(LoadIndex, true);

	public static int EntryCount => _index.Value.EntryCount;

	public static bool CouldMatch(string filePath)
	{
		if (String.IsNullOrWhiteSpace(filePath)
			|| !filePath.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)
			|| !File.Exists(filePath))
		{
			return false;
		}

		try
		{
			return _index.Value.EntriesBySize.ContainsKey(new FileInfo(filePath).Length);
		}
		catch
		{
			return false;
		}
	}

	public static async Task<NexusPakProvenanceEntry> TryResolveAsync(string filePath, CancellationToken cancellationToken)
	{
		if (!CouldMatch(filePath))
		{
			return null;
		}

		var file = new FileInfo(filePath);
		var originalLength = file.Length;
		if (!_index.Value.EntriesBySize.TryGetValue(originalLength, out var candidates))
		{
			return null;
		}

		var hash = await ComputeExactPakHashAsync(filePath, cancellationToken);
		file.Refresh();
		if (!file.Exists || file.Length != originalLength)
		{
			throw new IOException($"The pak changed while Redux was identifying it: {filePath}");
		}

		return candidates.TryGetValue(hash, out var match) ? match : null;
	}

	internal static async Task<string> ComputeExactPakHashAsync(string filePath, CancellationToken cancellationToken)
	{
		await using var stream = new FileStream(
			filePath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			bufferSize: 1024 * 1024,
			FileOptions.Asynchronous | FileOptions.SequentialScan);

		var hasher = new XxHash64();
		await hasher.AppendAsync(stream, cancellationToken);

		var littleEndianHash = new byte[sizeof(ulong)];
		BinaryPrimitives.WriteUInt64LittleEndian(littleEndianHash, hasher.GetCurrentHashAsUInt64());
		return Convert.ToBase64String(littleEndianHash);
	}

	private static NexusPakProvenanceIndex LoadIndex()
	{
		try
		{
			var databasePath = Path.Combine(AppContext.BaseDirectory, "Resources", DATABASE_FILE_NAME);
			if (!File.Exists(databasePath))
			{
				DivinityApp.Log($"Bundled Nexus pak provenance database was not found at '{databasePath}'.");
				return NexusPakProvenanceIndex.Empty;
			}

			var database = JsonConvert.DeserializeObject<NexusPakProvenanceDatabase>(File.ReadAllText(databasePath));
			if (database?.SchemaVersion != 1 || database.Entries == null)
			{
				DivinityApp.Log("Bundled Nexus pak provenance database has an unsupported or invalid schema.");
				return NexusPakProvenanceIndex.Empty;
			}

			var entriesBySize = database.Entries
				.Where(entry => entry.Size > 0
					&& entry.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START
					&& !String.IsNullOrWhiteSpace(entry.Hash))
				.GroupBy(entry => entry.Size)
				.ToDictionary(
					group => group.Key,
					group => group
						.GroupBy(entry => entry.Hash, StringComparer.Ordinal)
						.Where(matches => matches.Count() == 1)
						.ToDictionary(matches => matches.Key, matches => matches.Single(), StringComparer.Ordinal));

			var index = new NexusPakProvenanceIndex(entriesBySize);
			DivinityApp.Log($"Loaded {index.EntryCount} exact Nexus pak provenance entries.");
			return index;
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Failed to load the bundled Nexus pak provenance database:\n{ex}");
			return NexusPakProvenanceIndex.Empty;
		}
	}

	private sealed class NexusPakProvenanceDatabase
	{
		[JsonProperty("schemaVersion")]
		public int SchemaVersion { get; set; }

		[JsonProperty("entries")]
		public List<NexusPakProvenanceEntry> Entries { get; set; } = new();
	}

	private sealed class NexusPakProvenanceIndex
	{
		public static NexusPakProvenanceIndex Empty { get; } = new(new Dictionary<long, Dictionary<string, NexusPakProvenanceEntry>>());

		public Dictionary<long, Dictionary<string, NexusPakProvenanceEntry>> EntriesBySize { get; }
		public int EntryCount { get; }

		public NexusPakProvenanceIndex(Dictionary<long, Dictionary<string, NexusPakProvenanceEntry>> entriesBySize)
		{
			EntriesBySize = entriesBySize;
			EntryCount = entriesBySize.Values.Sum(entries => entries.Count);
		}
	}
}

public sealed class NexusPakProvenanceEntry
{
	[JsonProperty("hash")]
	public string Hash { get; set; }

	[JsonProperty("size")]
	public long Size { get; set; }

	[JsonProperty("modId")]
	public long ModId { get; set; }

	[JsonProperty("fileId")]
	public long FileId { get; set; } = -1;

	[JsonProperty("name")]
	public string Name { get; set; }

	[JsonProperty("author")]
	public string Author { get; set; }

	[JsonProperty("version")]
	public string Version { get; set; }

	[JsonProperty("pictureUrl")]
	public string PictureUrl { get; set; }

	public NexusModsModData CreateMetadata(string uuid)
	{
		Uri.TryCreate(PictureUrl, UriKind.Absolute, out var pictureUri);
		return new NexusModsModData
		{
			UUID = uuid,
			ModId = ModId,
			LastFileId = FileId,
			Name = Name,
			Author = Author,
			Version = Version,
			PictureUrl = pictureUri,
			Available = true,
			MetadataOrigin = NexusMetadataOrigin.BundledProvenance
		};
	}
}
