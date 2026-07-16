using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using DivinityModManager.Models.Metadata;
using DivinityModManager.AppServices;

using NexusModsNET.DataModels;

using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace DivinityModManager.Models.NexusMods;

public enum NexusMetadataOrigin
{
	Unknown = 0,
	BundledProvenance = 1,
	LiveApi = 2,
	Manual = 3,
	ManualUnlinked = 4
}

public class NexusModsModData : INotifyPropertyChanged, IExternalModMetadata
{
	[JsonIgnore]
	public ModSourceType SourceType => ModSourceType.NEXUSMODS;

	[JsonIgnore]
	public bool HasMetadata => IsUpdated;

	[JsonIgnore]
	public DateTime? UpdatedAt => UpdatedTimestamp > 0
		? DateTimeOffset.FromUnixTimeSeconds(UpdatedTimestamp).LocalDateTime
		: null;

	[JsonIgnore]
	public string SourcePageUrl => ModId >= DivinityApp.NEXUSMODS_MOD_ID_START
		? String.Format(DivinityApp.NEXUSMODS_MOD_URL, ModId)
		: String.Empty;

	[JsonIgnore]
	public string GalleryPageUrl => !String.IsNullOrWhiteSpace(SourcePageUrl)
		? $"{SourcePageUrl}?tab=images"
		: String.Empty;

	[JsonIgnore]
	public string ChangelogPageUrl => !String.IsNullOrWhiteSpace(SourcePageUrl)
		? $"{SourcePageUrl}?tab=logs"
		: String.Empty;

	[JsonIgnore]
	public string PreviewImageUrl => PictureUrl?.AbsoluteUri ?? String.Empty;

	[JsonProperty("uuid")]
	public string UUID { get; set; }

	private long _lastFileId;

	[JsonProperty("last_file_id")]
	public long LastFileId
	{
		get => _lastFileId;
		set
		{
			if (_lastFileId != value)
			{
				_lastFileId = value;
				RaisePropertyChanged(nameof(LastFileId));
			}
		}
	}

	[JsonProperty("name")]
	public string Name { get; set; }

	[JsonProperty("summary")]
	public string Summary { get; set; }

	[JsonProperty("description")]
	public string Description { get; set; }

	[JsonProperty("description_loaded")]
	public bool DescriptionLoaded { get; set; }

	private Dictionary<string, List<string>> _changelogs = new();

	[JsonProperty("changelogs")]
	public Dictionary<string, List<string>> Changelogs
	{
		get => _changelogs;
		set
		{
			_changelogs = value ?? new Dictionary<string, List<string>>();
			RaisePropertyChanged(nameof(Changelogs));
			RaisePropertyChanged(nameof(ChangelogText));
		}
	}

	[JsonProperty("changelogs_loaded")]
	public bool ChangelogsLoaded { get; set; }

	[JsonIgnore]
	public string ChangelogText
	{
		get
		{
			if (Changelogs == null || Changelogs.Count == 0)
			{
				return String.Empty;
			}

			var text = new StringBuilder();
			foreach (var changelog in Changelogs)
			{
				if (text.Length > 0)
				{
					text.AppendLine();
				}

				text.Append("Version ").AppendLine(changelog.Key);
				foreach (var entry in changelog.Value ?? Enumerable.Empty<string>())
				{
					if (!String.IsNullOrWhiteSpace(entry))
					{
						text.Append("\u2022 ").AppendLine(entry.Trim());
					}
				}
			}

			return text.ToString().Trim();
		}
	}

	[JsonProperty("picture_url")]
	public Uri PictureUrl { get; set; }

	[JsonProperty("mod_id")]
	public long ModId { get; set; }

	[JsonProperty("category_id")]
	public long CategoryId { get; set; }

	[JsonProperty("version")]
	public string Version { get; set; }

	[JsonProperty("endorsement_count")]
	public long EndorsementCount { get; set; }

	[JsonProperty("created_timestamp")]
	public long CreatedTimestamp { get; set; }

	[JsonProperty("updated_timestamp")]
	public long UpdatedTimestamp { get; set; }

	[JsonProperty("author")]
	public string Author { get; set; }

	[JsonProperty("uploaded_by")]
	public string UploadedBy { get; set; }

	[JsonProperty("uploaded_users_profile_url")]
	public Uri UploadedUsersProfileUrl { get; set; }

	[JsonProperty("contains_adult_content")]
	public bool ContainsAdultContent { get; set; }

	[JsonProperty("status")]
	public string Status { get; set; }

	[JsonProperty("available")]
	public bool Available { get; set; }

	private NexusMetadataOrigin _metadataOrigin;

	[JsonProperty("metadata_origin", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public NexusMetadataOrigin MetadataOrigin
	{
		get => _metadataOrigin;
		set
		{
			if (_metadataOrigin != value)
			{
				_metadataOrigin = value;
				RaisePropertyChanged(nameof(MetadataOrigin));
				RaisePropertyChanged(nameof(UsesBundledProvenance));
			}
		}
	}

	[JsonIgnore]
	public bool UsesBundledProvenance => MetadataOrigin == NexusMetadataOrigin.BundledProvenance;

	private ReduxOfflineMatchKind _offlineMatchKind;

	[JsonProperty("offline_match_kind", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public ReduxOfflineMatchKind OfflineMatchKind
	{
		get => _offlineMatchKind;
		set
		{
			if (_offlineMatchKind != value)
			{
				_offlineMatchKind = value;
				RaisePropertyChanged(nameof(OfflineMatchKind));
			}
		}
	}

	public void ResetSourceAssociation()
	{
		ModId = -1;
		LastFileId = -1;
		Name = Summary = Description = Version = Author = UploadedBy = Status = null;
		PictureUrl = UploadedUsersProfileUrl = null;
		Available = DescriptionLoaded = ChangelogsLoaded = IsUpdated = false;
		Changelogs = new Dictionary<string, List<string>>();
		MetadataOrigin = NexusMetadataOrigin.Unknown;
		OfflineMatchKind = ReduxOfflineMatchKind.Unknown;
		foreach (var property in _lazySerializedProperties) RaisePropertyChanged(property.Name);
		RaisePropertyChanged(nameof(IsUpdated));
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void RaisePropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void SetModVersion(NexusModFileVersionData info)
	{
		if (info.Success)
		{
			SetModVersion(info.ModId, info.FileId);
		}
	}

	public void SetModVersion(long modId, long fileId = -1)
	{
		if (ModId != modId)
		{
			ModId = modId;
			RaisePropertyChanged(nameof(ModId));
		}

		if (fileId > -1 && LastFileId != fileId)
		{
			LastFileId = fileId;
		}
	}

	public void SetChangelogs(Dictionary<string, IEnumerable<string>> changelogs)
	{
		Changelogs = changelogs?.ToDictionary(
			entry => entry.Key,
			entry => entry.Value?.Where(value => !String.IsNullOrWhiteSpace(value)).ToList() ?? new List<string>())
			?? new Dictionary<string, List<string>>();
		ChangelogsLoaded = true;
		RaisePropertyChanged(nameof(ChangelogsLoaded));
	}

	private static readonly IEnumerable<PropertyInfo> _lazySerializedProperties = typeof(NexusModsModData)
		.GetProperties(BindingFlags.Public | BindingFlags.Instance)
		.Where(prop => prop.GetCustomAttribute<JsonPropertyAttribute>(true) != null);

	public void Update(NexusModsModData data)
	{
		foreach (var prop in _lazySerializedProperties)
		{
			var value = prop.GetValue(data);
			if (value != null)
			{
				prop.SetValue(this, value);
				RaisePropertyChanged(prop.Name);
			}
		}
		IsUpdated = true;
		RaisePropertyChanged(nameof(IsUpdated));
	}

	public void Update(NexusMod data)
	{
		var t = typeof(NexusMod);
		foreach (var prop in _lazySerializedProperties)
		{
			var nexusProp = t.GetProperty(prop.Name);
			if (nexusProp != null)
			{
				var value = nexusProp.GetValue(data);
				if (value != null)
				{
					prop.SetValue(this, value);
					RaisePropertyChanged(prop.Name);
				}
			}
		}
		DescriptionLoaded = true;
		RaisePropertyChanged(nameof(DescriptionLoaded));
		MetadataOrigin = NexusMetadataOrigin.LiveApi;
		IsUpdated = true;
		RaisePropertyChanged(nameof(IsUpdated));
	}

	[JsonIgnore]
	public bool IsUpdated { get; set; }

	public NexusModsModData()
	{
		ModId = -1;
		LastFileId = -1;
	}
}
