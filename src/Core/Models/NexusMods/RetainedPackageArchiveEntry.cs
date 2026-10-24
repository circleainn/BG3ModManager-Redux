using DivinityModManager.Localization;
using System.ComponentModel;
using System.Windows;
using Newtonsoft.Json;

namespace DivinityModManager.Models.NexusMods;

[JsonObject(MemberSerialization.OptIn)]
public sealed class RetainedPackageArchiveEntry : ReactiveObject
{
	public RetainedPackageArchiveEntry() => PropertyChangedEventManager.AddHandler(ReduxLanguageService.Current, LanguageChanged, nameof(ReduxLanguageService.Language));
	private void LanguageChanged(object sender, PropertyChangedEventArgs e)
	{
		this.RaisePropertyChanged(nameof(MetadataText));
		this.RaisePropertyChanged(nameof(DestinationText));
	}

	[JsonProperty(Order = 1), Reactive] public string Sha256 { get; set; } = String.Empty;
	[JsonProperty(Order = 2), Reactive] public string StoredFileName { get; set; } = String.Empty;
	[JsonProperty(Order = 3), Reactive] public string OriginalFileName { get; set; } = String.Empty;
	[JsonProperty(Order = 4), Reactive] public long SizeBytes { get; set; }
	[JsonProperty(Order = 5), Reactive] public AcquiredPackageSourceKind SourceKind { get; set; }
	[JsonProperty(Order = 6), Reactive] public long NexusModId { get; set; }
	[JsonProperty(Order = 7), Reactive] public long NexusFileId { get; set; }
	[JsonProperty(Order = 8), Reactive] public string ProjectName { get; set; } = String.Empty;
	[JsonProperty(Order = 9), Reactive] public string Version { get; set; } = String.Empty;
	[JsonProperty(Order = 10), Reactive] public string PackageKind { get; set; } = String.Empty;
	[JsonProperty(Order = 11), Reactive] public string InstallDestination { get; set; } = String.Empty;
	[JsonProperty(Order = 12), Reactive] public DateTimeOffset InstalledAtUtc { get; set; }
	[JsonProperty(Order = 13), Reactive] public DateTimeOffset LastUsedAtUtc { get; set; }
	// Thumbnail caching has a separate lifecycle. Never persist remote URLs in the archive index.
	[JsonIgnore, Reactive] public string ThumbnailUrl { get; set; } = String.Empty;

	[JsonIgnore] public string DisplayName => !String.IsNullOrWhiteSpace(ProjectName)
		? ProjectName
		: Path.GetFileNameWithoutExtension(OriginalFileName);
	[JsonIgnore] public string SourceText => SourceKind switch
	{
		AcquiredPackageSourceKind.NexusMods when NexusModId > 0 => $"Nexus Mods · Mod {NexusModId}",
		AcquiredPackageSourceKind.ReduxDownload => "Redux download",
		_ => "Local package"
	};
	[JsonIgnore] public string MetadataText => String.Join("  ·  ", new[]
	{
		SourceKind == AcquiredPackageSourceKind.NexusMods && NexusModId > 0
			? ReduxLanguageService.Current.Format($"Nexus Mods · Mod {NexusModId}") : ReduxLanguageService.Current.Translate(SourceText),
		String.IsNullOrWhiteSpace(Version) ? null : $"v{Version}",
		FormatBytes(SizeBytes)
	}.Where(value => !String.IsNullOrWhiteSpace(value)));
	[JsonIgnore] public string DestinationText => PackageKind?.Contains("PAK", StringComparison.OrdinalIgnoreCase) == true
		? ReduxLanguageService.Current.Translate("Reinstall · existing placement preserved")
		: String.IsNullOrWhiteSpace(InstallDestination) ? ReduxLanguageService.Current.Translate("Retained package") : ReduxLanguageService.Current.Format($"Reinstall to {InstallDestination}");
	[JsonIgnore] public Uri NexusPage => SourceKind == AcquiredPackageSourceKind.NexusMods && NexusModId > 0
		? new Uri($"https://www.nexusmods.com/baldursgate3/mods/{NexusModId}?tab=files&file_id={NexusFileId}")
		: null;
	[JsonIgnore] public bool HasNexusSource => NexusPage != null;

	private static string FormatBytes(long bytes)
	{
		string[] units = ["B", "KB", "MB", "GB"];
		double value = Math.Max(0, bytes);
		var unit = 0;
		while (value >= 1024 && unit < units.Length - 1)
		{
			value /= 1024;
			unit++;
		}
		return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.#} {units[unit]}";
	}
}
