using DivinityModManager.Models;
using DivinityModManager.Models.Metadata;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using Newtonsoft.Json;

namespace DivinityModManager.AppServices;

/// <summary>
/// Produces an opt-in, privacy-limited inventory that can be reviewed before
/// proposing additions to the bundled Redux mod database.
/// </summary>
public static class ReduxDatabaseContributionService
{
	public const int SchemaVersion = 1;
	public const string ReportType = "redux-mod-database-contribution";

	public static async Task<ReduxDatabaseContributionResult> CreateAsync(
		IEnumerable<DivinityModData> mods,
		IProgress<ReduxDatabaseContributionProgress> progress = null,
		CancellationToken cancellationToken = default)
	{
		var candidates = (mods ?? Enumerable.Empty<DivinityModData>())
			.Where(mod => mod != null && mod.IsUserMod && !mod.IsVisualDivider)
			.GroupBy(
				mod => $"{mod.UUID}\0{mod.FilePath}",
				StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
			.ThenBy(mod => mod.FileName, StringComparer.OrdinalIgnoreCase)
			.ToList();

		var records = new List<ReduxDatabaseContributionMod>(candidates.Count);
		var fingerprinted = 0;
		var unavailable = 0;
		for (var index = 0; index < candidates.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var mod = candidates[index];
			progress?.Report(new ReduxDatabaseContributionProgress(index, candidates.Count, mod.DisplayName));

			var record = CreateRecord(mod);
			if (IsReadablePak(mod.FilePath))
			{
				try
				{
					var file = new FileInfo(mod.FilePath);
					var originalLength = file.Length;
					record.PakHash = await ReduxModDatabaseService.ComputeExactPakHashAsync(mod.FilePath, cancellationToken);
					file.Refresh();
					if (!file.Exists || file.Length != originalLength)
						throw new IOException("The PAK changed while Redux was fingerprinting it.");
					record.PakSize = originalLength;
					record.FingerprintStatus = "exact";
					fingerprinted++;
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					// Never serialize an exception: it can contain a private absolute path.
					record.FingerprintStatus = "unavailable";
					unavailable++;
				}
			}
			else
			{
				record.FingerprintStatus = "unavailable";
				unavailable++;
			}
			records.Add(record);
		}

		progress?.Report(new ReduxDatabaseContributionProgress(candidates.Count, candidates.Count, String.Empty));
		var report = new ReduxDatabaseContributionReport
		{
			SchemaVersion = SchemaVersion,
			ReportType = ReportType,
			CreatedAtUtc = DateTime.UtcNow,
			ReduxVersion = DivinityApp.REDUX_DISPLAY_VERSION,
			Privacy = new ReduxDatabaseContributionPrivacy(),
			Mods = records
		};
		return new ReduxDatabaseContributionResult(report, fingerprinted, unavailable);
	}

	public static void Save(string outputPath, ReduxDatabaseContributionReport report)
	{
		if (String.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("An output path is required.", nameof(outputPath));
		if (report == null) throw new ArgumentNullException(nameof(report));
		ValidatePrivacyContract(report);

		var fullPath = Path.GetFullPath(outputPath);
		var directory = Path.GetDirectoryName(fullPath);
		if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
			throw new DirectoryNotFoundException("The selected report folder does not exist.");

		var json = JsonConvert.SerializeObject(report, Formatting.Indented) + Environment.NewLine;
		AtomicFileWriter.WriteAllText(fullPath, json, validateTemporaryFile: temporaryPath =>
		{
			var validated = JsonConvert.DeserializeObject<ReduxDatabaseContributionReport>(File.ReadAllText(temporaryPath))
				?? throw new InvalidDataException("The generated contribution report could not be validated.");
			ValidatePrivacyContract(validated);
			return true;
		});
	}

	private static ReduxDatabaseContributionMod CreateRecord(DivinityModData mod)
	{
		var record = new ReduxDatabaseContributionMod
		{
			Uuid = Guid.TryParse(mod.UUID, out var moduleUuid) ? moduleUuid.ToString() : null,
			Name = SanitizeText(mod.Name),
			DisplayName = SanitizeText(mod.DisplayName),
			Folder = SanitizeFolder(mod.Folder),
			FileName = SanitizeFileName(mod.FilePath),
			Author = SanitizeText(mod.Author),
			Version = SanitizeText(mod.Version?.Version),
			HasModuleMetadata = mod.HasMetadata,
			IsOverride = mod.IsForceLoaded
		};

		if (mod.NexusModsData?.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START)
		{
			record.Nexus = new ReduxDatabaseContributionNexus
			{
				ModId = mod.NexusModsData.ModId,
				FileId = mod.NexusModsData.LastFileId > 0 ? mod.NexusModsData.LastFileId : null,
				Name = SanitizeText(mod.NexusModsData.Name),
				Author = SanitizeText(mod.NexusModsData.Author),
				Version = SanitizeText(mod.NexusModsData.Version),
				PictureUrl = SanitizeWebUrl(mod.NexusModsData.PictureUrl?.AbsoluteUri),
				MetadataOrigin = mod.NexusModsData.MetadataOrigin.ToString()
			};
		}

		if (mod.ModioData?.ModId > 0)
		{
			record.Modio = new ReduxDatabaseContributionModio
			{
				ModId = mod.ModioData.ModId,
				Name = SanitizeText(mod.ModioData.Name),
				Author = SanitizeText(mod.ModioData.Author),
				Version = SanitizeText(mod.ModioData.Version),
				PageUrl = SanitizeWebUrl(mod.ModioData.ProfileUrl)
			};
		}

		record.SourceType = mod.Metadata?.SourceType switch
		{
			ModSourceType.NEXUSMODS => "nexus",
			ModSourceType.MODIO => "modio",
			_ => "local"
		};
		return record;
	}

	private static bool IsReadablePak(string path) =>
		!String.IsNullOrWhiteSpace(path)
		&& path.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)
		&& File.Exists(path);

	private static string SanitizeText(string value)
	{
		if (String.IsNullOrWhiteSpace(value)) return null;
		var trimmed = value.Trim();
		return ContainsPrivatePathData(trimmed) ? null : trimmed;
	}

	private static string SanitizeFolder(string value)
	{
		var sanitized = SanitizeText(value);
		return sanitized != null && !sanitized.Contains('/') && !sanitized.Contains('\\') ? sanitized : null;
	}

	private static string SanitizeFileName(string path)
	{
		if (String.IsNullOrWhiteSpace(path)) return null;
		var fileName = Path.GetFileName(path);
		return SanitizeText(fileName);
	}

	private static string SanitizeWebUrl(string value)
	{
		if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
		if (uri.Scheme is not ("http" or "https") || !String.IsNullOrEmpty(uri.UserInfo)) return null;
		return new UriBuilder(uri)
		{
			Query = String.Empty,
			Fragment = String.Empty,
			UserName = String.Empty,
			Password = String.Empty
		}.Uri.AbsoluteUri;
	}

	private static bool LooksLikeAbsolutePath(string value) =>
		Path.IsPathRooted(value)
		|| value.StartsWith(@"\\", StringComparison.Ordinal)
		|| (value.Length >= 3
			&& Char.IsLetter(value[0])
			&& value[1] == ':'
			&& value[2] is '\\' or '/');

	private static bool ContainsPrivatePathData(string value)
	{
		if (LooksLikeAbsolutePath(value)) return true;

		for (var index = 0; index <= value.Length - 3; index++)
		{
			if (Char.IsLetter(value[index])
				&& value[index + 1] == ':'
				&& value[index + 2] is '\\' or '/')
				return true;
		}

		foreach (var marker in new[]
		{
			@"\\",
			@"\Users\",
			"/Users/",
			@"\Documents and Settings\",
			"/home/",
			"%USERPROFILE%",
			"%APPDATA%",
			"%LOCALAPPDATA%",
			"%HOMEDRIVE%",
			"$HOME",
			"${HOME}",
			@"~\",
			"~/"
		})
		{
			if (value.Contains(marker, StringComparison.OrdinalIgnoreCase)) return true;
		}

		var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		return !String.IsNullOrWhiteSpace(userProfile)
			&& value.Contains(userProfile, StringComparison.OrdinalIgnoreCase);
	}

	private static void ValidatePrivacyContract(ReduxDatabaseContributionReport report)
	{
		if (report.SchemaVersion != SchemaVersion
			|| !String.Equals(report.ReportType, ReportType, StringComparison.Ordinal))
			throw new InvalidDataException("The contribution report identity is invalid.");
		if (report.Privacy == null)
			throw new InvalidDataException("The contribution report privacy declaration is missing.");

		foreach (var mod in report.Mods ?? new List<ReduxDatabaseContributionMod>())
		{
			if (!String.IsNullOrWhiteSpace(mod.Uuid) && !Guid.TryParse(mod.Uuid, out _))
				throw new InvalidDataException("A contribution record contains an invalid module UUID.");
			if (!String.IsNullOrWhiteSpace(mod.FileName)
				&& (ContainsPrivatePathData(mod.FileName)
					|| !String.Equals(mod.FileName, Path.GetFileName(mod.FileName), StringComparison.Ordinal)))
				throw new InvalidDataException("A contribution record contains a package path.");
			if (!String.IsNullOrWhiteSpace(mod.Folder)
				&& (ContainsPrivatePathData(mod.Folder) || mod.Folder.Contains('/') || mod.Folder.Contains('\\')))
				throw new InvalidDataException("A contribution record contains a module path.");

			foreach (var value in new[]
			{
				mod.Name,
				mod.DisplayName,
				mod.Author,
				mod.Version,
				mod.Nexus?.Name,
				mod.Nexus?.Author,
				mod.Nexus?.Version,
				mod.Modio?.Name,
				mod.Modio?.Author,
				mod.Modio?.Version
			})
			{
				if (!String.IsNullOrWhiteSpace(value) && ContainsPrivatePathData(value))
					throw new InvalidDataException("A contribution record contains private path data.");
			}

			ValidatePublicWebUrl(mod.Nexus?.PictureUrl);
			ValidatePublicWebUrl(mod.Modio?.PageUrl);
		}
	}

	private static void ValidatePublicWebUrl(string value)
	{
		if (String.IsNullOrWhiteSpace(value)) return;
		if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
			|| uri.Scheme is not ("http" or "https")
			|| !String.IsNullOrEmpty(uri.UserInfo)
			|| !String.IsNullOrEmpty(uri.Query)
			|| !String.IsNullOrEmpty(uri.Fragment))
			throw new InvalidDataException("A contribution record contains a non-public provider URL.");
	}
}

public sealed class ReduxDatabaseContributionReport
{
	[JsonProperty("schemaVersion", Order = 1)]
	public int SchemaVersion { get; set; }

	[JsonProperty("reportType", Order = 2)]
	public string ReportType { get; set; }

	[JsonProperty("createdAtUtc", Order = 3)]
	public DateTime CreatedAtUtc { get; set; }

	[JsonProperty("reduxVersion", Order = 4)]
	public string ReduxVersion { get; set; }

	[JsonProperty("privacy", Order = 5)]
	public ReduxDatabaseContributionPrivacy Privacy { get; set; }

	[JsonProperty("mods", Order = 6)]
	public List<ReduxDatabaseContributionMod> Mods { get; set; } = new();
}

public sealed class ReduxDatabaseContributionPrivacy
{
	[JsonProperty("containsAbsolutePaths")]
	public bool ContainsAbsolutePaths => false;

	[JsonProperty("containsLoadOrder")]
	public bool ContainsLoadOrder => false;

	[JsonProperty("containsProfileNames")]
	public bool ContainsProfileNames => false;

	[JsonProperty("containsSettings")]
	public bool ContainsSettings => false;

	[JsonProperty("containsCredentials")]
	public bool ContainsCredentials => false;
}

public sealed class ReduxDatabaseContributionMod
{
	[JsonProperty("uuid", NullValueHandling = NullValueHandling.Ignore)]
	public string Uuid { get; set; }

	[JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
	public string Name { get; set; }

	[JsonProperty("displayName", NullValueHandling = NullValueHandling.Ignore)]
	public string DisplayName { get; set; }

	[JsonProperty("folder", NullValueHandling = NullValueHandling.Ignore)]
	public string Folder { get; set; }

	[JsonProperty("fileName", NullValueHandling = NullValueHandling.Ignore)]
	public string FileName { get; set; }

	[JsonProperty("author", NullValueHandling = NullValueHandling.Ignore)]
	public string Author { get; set; }

	[JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
	public string Version { get; set; }

	[JsonProperty("pakSize", NullValueHandling = NullValueHandling.Ignore)]
	public long? PakSize { get; set; }

	[JsonProperty("pakHash", NullValueHandling = NullValueHandling.Ignore)]
	public string PakHash { get; set; }

	[JsonProperty("fingerprintStatus")]
	public string FingerprintStatus { get; set; }

	[JsonProperty("hasModuleMetadata")]
	public bool HasModuleMetadata { get; set; }

	[JsonProperty("isOverride")]
	public bool IsOverride { get; set; }

	[JsonProperty("sourceType")]
	public string SourceType { get; set; }

	[JsonProperty("nexus", NullValueHandling = NullValueHandling.Ignore)]
	public ReduxDatabaseContributionNexus Nexus { get; set; }

	[JsonProperty("modio", NullValueHandling = NullValueHandling.Ignore)]
	public ReduxDatabaseContributionModio Modio { get; set; }
}

public sealed class ReduxDatabaseContributionNexus
{
	[JsonProperty("modId")]
	public long ModId { get; set; }

	[JsonProperty("fileId", NullValueHandling = NullValueHandling.Ignore)]
	public long? FileId { get; set; }

	[JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
	public string Name { get; set; }

	[JsonProperty("author", NullValueHandling = NullValueHandling.Ignore)]
	public string Author { get; set; }

	[JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
	public string Version { get; set; }

	[JsonProperty("pictureUrl", NullValueHandling = NullValueHandling.Ignore)]
	public string PictureUrl { get; set; }

	[JsonProperty("metadataOrigin")]
	public string MetadataOrigin { get; set; }
}

public sealed class ReduxDatabaseContributionModio
{
	[JsonProperty("modId")]
	public long ModId { get; set; }

	[JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
	public string Name { get; set; }

	[JsonProperty("author", NullValueHandling = NullValueHandling.Ignore)]
	public string Author { get; set; }

	[JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
	public string Version { get; set; }

	[JsonProperty("pageUrl", NullValueHandling = NullValueHandling.Ignore)]
	public string PageUrl { get; set; }
}

public sealed record ReduxDatabaseContributionProgress(int Completed, int Total, string ModName);

public sealed record ReduxDatabaseContributionResult(
	ReduxDatabaseContributionReport Report,
	int FingerprintedCount,
	int UnavailableFingerprintCount);
