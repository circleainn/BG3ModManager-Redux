using DivinityModManager.Util;
using DivinityModManager.Extensions;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.AppServices;

using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace DivinityModManager.Models.Metadata;

/// <summary>
/// Provider-neutral metadata prepared for display in the Redux interface.
/// This class only resolves presentation fallbacks; it does not fetch, install,
/// move, or otherwise modify mods.
/// </summary>
public sealed class ModMetadataViewData : ReactiveObject
{
	private readonly DivinityModData _mod;
	private INotifyPropertyChanged _nexusMetadata;
	private INotifyPropertyChanged _modioMetadata;
	private IExternalModMetadata Provider => _mod.ModioData?.HasMetadata == true
		? _mod.ModioData
		: _mod.CanOpenNexusModsLink
			? _mod.NexusModsData
			: null;
	private bool HasOnlineMetadata => Provider?.HasMetadata == true;

	public string Title => HasOnlineMetadata && !String.IsNullOrWhiteSpace(Provider?.Name)
		? Provider.Name
		: _mod.DisplayName;

	public string Author => HasOnlineMetadata && !String.IsNullOrWhiteSpace(Provider?.Author)
		? Provider.Author
		: _mod.Author;

	public string Version => HasOnlineMetadata && !String.IsNullOrWhiteSpace(Provider?.Version)
		? Provider.Version
		: _mod.DisplayVersion;

	public string Summary => HasOnlineMetadata && !String.IsNullOrWhiteSpace(Provider?.Summary)
		? Provider.Summary
		: _mod.Description;

	public string Description => HasOnlineMetadata && !String.IsNullOrWhiteSpace(Provider?.Description)
		? Provider.Description
		: _mod.Description;

	public string ChangelogText => Provider?.ChangelogText ?? String.Empty;

	public ModSourceType SourceType => Provider?.SourceType ?? ModSourceType.NONE;

	public string SourceLabel => SourceType != ModSourceType.NONE ? SourceType.GetDescription() : "Local";
	public string SourceBadgeLabel => SourceLabel.ToUpperInvariant();
	public Visibility OnlineSourceVisibility => SourceType != ModSourceType.NONE ? Visibility.Visible : Visibility.Collapsed;
	public Visibility ModioWarningVisibility => SourceType == ModSourceType.MODIO ? Visibility.Visible : Visibility.Collapsed;
	public string SourcePageUrl => Provider?.SourcePageUrl ?? String.Empty;
	public string GalleryPageUrl => Provider?.GalleryPageUrl ?? String.Empty;
	public string ChangelogPageUrl => Provider?.ChangelogPageUrl ?? String.Empty;
	public Uri PreviewImageUri => Uri.TryCreate(Provider?.PreviewImageUrl, UriKind.Absolute, out var uri) ? uri : null;
	public Visibility PreviewImageVisibility => PreviewImageUri != null ? Visibility.Visible : Visibility.Collapsed;
	public string SourcePageButtonLabel => $"View Mod on {SourceLabel}";
	public string GalleryTooltip => $"View image gallery on {SourceLabel}";
	public string DescriptionHeading => SourceType != ModSourceType.NONE ? $"Description from {SourceLabel}" : "Local description";
	public string ChangelogHeading => SourceType != ModSourceType.NONE ? $"Changelog from {SourceLabel}" : "No online changelog linked";
	public string ChangelogButtonLabel => $"Open {SourceLabel} changelog page";
	public string ChangelogHelperText => SourceType != ModSourceType.NONE
		? $"Cached version history from the linked {SourceLabel} page."
		: "Version history will appear here after an online source is linked.";

	public string LinkStatus => HasOnlineMetadata
		? SourceType == ModSourceType.NEXUSMODS
			? _mod.NexusModsData?.MetadataOrigin switch
			{
				NexusMetadataOrigin.Manual => "Manually linked from Nexus Mods",
				NexusMetadataOrigin.BundledProvenance => "Automatically linked from the Redux mod database",
				_ => "Automatically linked from Nexus Mods"
			}
			: $"Automatically linked from {SourceLabel}"
		: "Selected mod details";

	public bool UsesBundledNexusMetadata => SourceType == ModSourceType.NEXUSMODS
		&& _mod.NexusModsData?.UsesBundledProvenance == true;
	public Visibility OfflineDatabaseNoticeVisibility => UsesBundledNexusMetadata ? Visibility.Visible : Visibility.Collapsed;
	public string OfflineDatabaseNotice => _mod.NexusModsData?.OfflineMatchKind is ReduxOfflineMatchKind.ModuleIdentity or ReduxOfflineMatchKind.NameAndAuthor
		? "Probable Nexus match"
		: "Verified offline match";
	public string LinkStatusTooltip => !UsesBundledNexusMetadata ? null : _mod.NexusModsData?.OfflineMatchKind switch
	{
		ReduxOfflineMatchKind.ExactArchive => "Redux associated this package with a Nexus project using an exact recorded package fingerprint. Live API data is used when available.",
		ReduxOfflineMatchKind.ModuleIdentity => "Redux associated this package with a Nexus project using a reviewed module identity. Local package details and load order remain unchanged.",
		ReduxOfflineMatchKind.NameAndAuthor => "Redux associated this package with a Nexus project because its normalized name and author matched one reviewed project. Local package details and load order remain unchanged.",
		_ => "Redux associated this package with a Nexus project using an exact installed .pak match. Live API data is used when available."
	};
	// Retained as compatibility aliases for existing bindings while the standalone database badge is removed.
	public string OfflineDatabaseNoticeTooltip => LinkStatusTooltip;

	public string VersionLabel => HasOnlineMetadata
		? $"{SourceLabel} version {Version}"
		: $"Version {Version}";

	public string AuthorLabel => SourceType == ModSourceType.NEXUSMODS ? $"Created by {Author}" : $"By {Author}";
	// A Nexus mod's credited creator and uploading account are separate values.
	// Only UploadedBy is safe to use when constructing a Nexus profile link.
	public string Uploader => SourceType == ModSourceType.NEXUSMODS
		? _mod.NexusModsData?.UploadedBy
		: Author;
	public string UploaderLabel => $"Uploaded by {Uploader}";
	public string AuthorPageUrl
	{
		get
		{
			if (SourceType == ModSourceType.NEXUSMODS)
			{
				if (_mod.NexusModsData?.UploadedUsersProfileUrl != null)
					return _mod.NexusModsData.UploadedUsersProfileUrl.AbsoluteUri;
				if (!String.IsNullOrWhiteSpace(Uploader))
					return $"https://next.nexusmods.com/profile/{Uri.EscapeDataString(Uploader)}";
			}
			// mod.io author profiles are intentionally presented as plain text in Redux.
			// The mod page remains the reliable clickable destination for mod.io metadata.
			return String.Empty;
		}
	}
	public Visibility AuthorPageVisibility => !String.IsNullOrWhiteSpace(AuthorPageUrl) ? Visibility.Visible : Visibility.Collapsed;
	public Visibility LocalAuthorVisibility
	{
		get
		{
			if (String.IsNullOrWhiteSpace(Author)) return Visibility.Collapsed;
			if (SourceType == ModSourceType.NEXUSMODS &&
				String.Equals(Author, Uploader, StringComparison.OrdinalIgnoreCase))
				return Visibility.Collapsed;
			return Visibility.Visible;
		}
	}

	public string UpdatedLabel
	{
		get
		{
			if (HasOnlineMetadata && Provider?.UpdatedAt != null)
			{
				var updated = Provider.UpdatedAt.Value;
				return $"{SourceLabel} updated {updated.ToString(DivinityApp.DateTimeColumnFormat, CultureInfo.InstalledUICulture)}";
			}

			return _mod.LastModifiedDateText;
		}
	}

	public ModMetadataViewData(DivinityModData mod)
	{
		_mod = mod;
		_mod.PropertyChanged += OnModPropertyChanged;
		AttachOnlineMetadata(ref _nexusMetadata, _mod.NexusModsData);
		AttachOnlineMetadata(ref _modioMetadata, _mod.ModioData);
	}

	private void OnModPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DivinityModData.NexusModsData))
		{
			AttachOnlineMetadata(ref _nexusMetadata, _mod.NexusModsData);
		}
		else if (e.PropertyName == nameof(DivinityModData.ModioData))
		{
			AttachOnlineMetadata(ref _modioMetadata, _mod.ModioData);
		}

		RaiseDisplayPropertiesChanged();
	}

	private void AttachOnlineMetadata(ref INotifyPropertyChanged currentMetadata, INotifyPropertyChanged metadata)
	{
		if (currentMetadata != null)
		{
			currentMetadata.PropertyChanged -= OnOnlineMetadataPropertyChanged;
		}

		currentMetadata = metadata;
		if (currentMetadata != null)
		{
			currentMetadata.PropertyChanged += OnOnlineMetadataPropertyChanged;
		}
	}

	private void OnOnlineMetadataPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		RaiseDisplayPropertiesChanged();
	}

	private void RaiseDisplayPropertiesChanged()
	{
		this.RaisePropertyChanged(nameof(Title));
		this.RaisePropertyChanged(nameof(Author));
		this.RaisePropertyChanged(nameof(Version));
		this.RaisePropertyChanged(nameof(Summary));
		this.RaisePropertyChanged(nameof(Description));
		this.RaisePropertyChanged(nameof(ChangelogText));
		this.RaisePropertyChanged(nameof(SourceType));
		this.RaisePropertyChanged(nameof(SourceLabel));
		this.RaisePropertyChanged(nameof(SourceBadgeLabel));
		this.RaisePropertyChanged(nameof(OnlineSourceVisibility));
		this.RaisePropertyChanged(nameof(ModioWarningVisibility));
		this.RaisePropertyChanged(nameof(SourcePageUrl));
		this.RaisePropertyChanged(nameof(GalleryPageUrl));
		this.RaisePropertyChanged(nameof(ChangelogPageUrl));
		this.RaisePropertyChanged(nameof(PreviewImageUri));
		this.RaisePropertyChanged(nameof(PreviewImageVisibility));
		this.RaisePropertyChanged(nameof(SourcePageButtonLabel));
		this.RaisePropertyChanged(nameof(GalleryTooltip));
		this.RaisePropertyChanged(nameof(DescriptionHeading));
		this.RaisePropertyChanged(nameof(ChangelogHeading));
		this.RaisePropertyChanged(nameof(ChangelogButtonLabel));
		this.RaisePropertyChanged(nameof(ChangelogHelperText));
		this.RaisePropertyChanged(nameof(LinkStatus));
		this.RaisePropertyChanged(nameof(LinkStatusTooltip));
		this.RaisePropertyChanged(nameof(UsesBundledNexusMetadata));
		this.RaisePropertyChanged(nameof(OfflineDatabaseNoticeVisibility));
		this.RaisePropertyChanged(nameof(OfflineDatabaseNotice));
		this.RaisePropertyChanged(nameof(OfflineDatabaseNoticeTooltip));
		this.RaisePropertyChanged(nameof(VersionLabel));
		this.RaisePropertyChanged(nameof(AuthorLabel));
		this.RaisePropertyChanged(nameof(Uploader));
		this.RaisePropertyChanged(nameof(UploaderLabel));
		this.RaisePropertyChanged(nameof(AuthorPageUrl));
		this.RaisePropertyChanged(nameof(AuthorPageVisibility));
		this.RaisePropertyChanged(nameof(LocalAuthorVisibility));
		this.RaisePropertyChanged(nameof(UpdatedLabel));
	}
}
