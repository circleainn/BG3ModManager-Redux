using DivinityModManager.Models.Metadata;

using Newtonsoft.Json;

using System.ComponentModel;

namespace DivinityModManager.Models.Modio;

public enum ModioMetadataOrigin
{
	Unknown = 0,
	NativePackage = 1,
	CreatorManifest = 2
}

/// <summary>
/// Cached read-only metadata returned by mod.io. This model contains no
/// subscription, download, installation, or file-management behavior.
/// </summary>
public class ModioModData : IExternalModMetadata
{
	[JsonProperty("uuid")]
	public string UUID { get; set; }

	[JsonProperty("id")]
	public long ModId { get; set; }

	[JsonProperty("metadata_origin", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public ModioMetadataOrigin MetadataOrigin { get; set; }

	[JsonProperty("game_id")]
	public long GameId { get; set; }

	[JsonProperty("name_id")]
	public string NameId { get; set; }

	[JsonProperty("name")]
	public string Name { get; set; }

	[JsonProperty("summary")]
	public string Summary { get; set; }

	[JsonProperty("description_plaintext")]
	public string Description { get; set; }

	[JsonProperty("profile_url")]
	public string ProfileUrl { get; set; }

	[JsonProperty("date_updated")]
	public long DateUpdated { get; set; }

	[JsonProperty("submitted_by")]
	public ModioUserData SubmittedBy { get; set; }

	[JsonProperty("logo")]
	public ModioImageData Logo { get; set; }

	[JsonProperty("media")]
	public ModioMediaData Media { get; set; }

	[JsonProperty("modfile")]
	public ModioFileData ModFile { get; set; }

	[JsonProperty("tags")]
	public List<ModioTagData> Tags { get; set; } = new();

	[JsonIgnore]
	public ModSourceType SourceType => ModSourceType.MODIO;

	[JsonIgnore]
	public bool HasMetadata => ModId > 0;

	[JsonIgnore]
	public string Author => SubmittedBy?.DisplayName ?? SubmittedBy?.Username ?? String.Empty;

	[JsonIgnore]
	public string Version => ModFile?.Version ?? String.Empty;

	[JsonIgnore]
	public string ChangelogText => ModFile?.Changelog ?? String.Empty;

	[JsonIgnore]
	public DateTime? UpdatedAt => DateUpdated > 0
		? DateTimeOffset.FromUnixTimeSeconds(DateUpdated).LocalDateTime
		: null;

	[JsonIgnore]
	public string SourcePageUrl => ProfileUrl ?? String.Empty;

	[JsonIgnore]
	public string GalleryPageUrl => ProfileUrl ?? String.Empty;

	[JsonIgnore]
	public string ChangelogPageUrl => ProfileUrl ?? String.Empty;

	[JsonIgnore]
	public string PreviewImageUrl => Logo?.Thumbnail1280
		?? Logo?.Original
		?? GalleryImageUrls.FirstOrDefault()
		?? String.Empty;

	[JsonIgnore]
	public IReadOnlyList<string> GalleryImageUrls => Media?.Images?
		.Select(image => image.Original)
		.Where(url => !String.IsNullOrWhiteSpace(url))
		.ToList() ?? new List<string>();

	public event PropertyChangedEventHandler PropertyChanged;

	public void Update(ModioModData data)
	{
		if (data == null)
		{
			return;
		}

		UUID = data.UUID;
		ModId = data.ModId;
		MetadataOrigin = data.MetadataOrigin;
		GameId = data.GameId;
		NameId = data.NameId;
		Name = data.Name;
		Summary = data.Summary;
		Description = data.Description;
		ProfileUrl = data.ProfileUrl;
		DateUpdated = data.DateUpdated;
		SubmittedBy = data.SubmittedBy;
		Logo = data.Logo;
		Media = data.Media;
		ModFile = data.ModFile;
		Tags = data.Tags ?? new List<ModioTagData>();

		foreach (var property in typeof(ModioModData).GetProperties())
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property.Name));
		}
	}
}

public class ModioUserData
{
	[JsonProperty("username")]
	public string Username { get; set; }

	[JsonProperty("display_name_portal")]
	public string DisplayName { get; set; }
}

public class ModioImageData
{
	[JsonProperty("original")]
	public string Original { get; set; }

	[JsonProperty("thumb_1280x720")]
	public string Thumbnail1280 { get; set; }
}

public class ModioMediaData
{
	[JsonProperty("images")]
	public List<ModioImageData> Images { get; set; } = new();
}

public class ModioFileData
{
	[JsonProperty("id")]
	public long FileId { get; set; }

	[JsonProperty("version")]
	public string Version { get; set; }

	[JsonProperty("changelog")]
	public string Changelog { get; set; }

	[JsonProperty("metadata_blob")]
	public string MetadataBlob { get; set; }
}

public class ModioTagData
{
	[JsonProperty("name")]
	public string Name { get; set; }

	[JsonProperty("name_localized")]
	public string LocalizedName { get; set; }
}
