using System.Runtime.Serialization;

namespace DivinityModManager.Models.NexusMods;

public enum NxmDownloadState
{
	Resolving,
	Queued,
	Downloading,
	Paused,
	RetryWaiting,
	Downloaded,
	NeedsReview,
	Installing,
	Installed,
	NeedsFreshLink,
	Failed,
	InstallFailed
}

public enum AcquiredPackageSourceKind
{
	NexusMods,
	LocalFile,
	ReduxDownload
}

public sealed record NxmArchiveInspection(string FileName, long Length, DateTime LastWriteUtc,
	IReadOnlyList<ModuleShortDesc> Modules);

[DataContract]
public sealed class NxmDownloadItem : ReactiveObject
{
	public const string RemovalIncompleteErrorCode = "removal-incomplete";
	[DataMember(Order = 1), Reactive] public string Id { get; set; } = Guid.NewGuid().ToString("N");
	[DataMember(Order = 2), Reactive] public long QueuePosition { get; set; }
	[DataMember(Order = 3), Reactive] public long ModId { get; set; }
	[DataMember(Order = 4), Reactive] public long FileId { get; set; }
	[DataMember(Order = 5), Reactive] public string ProjectName { get; set; } = String.Empty;
	[DataMember(Order = 6), Reactive] public string Author { get; set; } = String.Empty;
	[DataMember(Order = 7), Reactive] public string FileDisplayName { get; set; } = String.Empty;
	[DataMember(Order = 8), Reactive] public string FileName { get; set; } = String.Empty;
	[DataMember(Order = 9), Reactive] public string Version { get; set; } = String.Empty;
	private long _sizeBytes;
	[DataMember(Order = 10)] public long SizeBytes
	{
		get => _sizeBytes;
		set
		{
			this.RaiseAndSetIfChanged(ref _sizeBytes, value);
			this.RaisePropertyChanged(nameof(MetadataText));
			this.RaisePropertyChanged(nameof(DownloadDetailText));
		}
	}
	private long _bytesReceived;
	[DataMember(Order = 11)] public long BytesReceived
	{
		get => _bytesReceived;
		set
		{
			this.RaiseAndSetIfChanged(ref _bytesReceived, value);
			this.RaisePropertyChanged(nameof(DownloadDetailText));
		}
	}
	[DataMember(Order = 12), Reactive] public string ETag { get; set; } = String.Empty;
	[DataMember(Order = 13), Reactive] public DateTimeOffset? LastModified { get; set; }
	[DataMember(Order = 14), Reactive] public string PartialFileName { get; set; } = String.Empty;
	[DataMember(Order = 15), Reactive] public string CompletedFileName { get; set; } = String.Empty;
	private NxmDownloadState _state;
	[DataMember(Order = 16)]
	public NxmDownloadState State
	{
		get => _state;
		set
		{
			this.RaiseAndSetIfChanged(ref _state, value);
			this.RaisePropertyChanged(nameof(StatusText));
			this.RaisePropertyChanged(nameof(FailureDetails));
			this.RaisePropertyChanged(nameof(CanDownloadAgain));
			this.RaisePropertyChanged(nameof(CanRetryInstall));
			this.RaisePropertyChanged(nameof(DownloadDetailText));
			this.RaisePropertyChanged(nameof(NexusActionText));
			this.RaisePropertyChanged(nameof(NexusActionToolTip));
			this.RaisePropertyChanged(nameof(InstallActionText));
			this.RaisePropertyChanged(nameof(InstallActionToolTip));
			this.RaisePropertyChanged(nameof(RemoveActionText));
			this.RaisePropertyChanged(nameof(IsInstalledHistory));
		}
	}
	[DataMember(Order = 17), Reactive] public bool RequiresAuthorization { get; set; }
	[DataMember(Order = 18), Reactive] public int RetryCount { get; set; }
	[DataMember(Order = 19), Reactive] public DateTimeOffset? RetryAfter { get; set; }
	private string _errorCode = String.Empty;
	[DataMember(Order = 20)] public string ErrorCode
	{
		get => _errorCode;
		set
		{
			this.RaiseAndSetIfChanged(ref _errorCode, value ?? String.Empty);
			this.RaisePropertyChanged(nameof(IsRemovalPending));
			this.RaisePropertyChanged(nameof(RemoveActionText));
			this.RaisePropertyChanged(nameof(StatusText));
			this.RaisePropertyChanged(nameof(FailureDetails));
			this.RaisePropertyChanged(nameof(CanDownloadAgain));
			this.RaisePropertyChanged(nameof(CanRetryInstall));
			this.RaisePropertyChanged(nameof(DownloadDetailText));
		}
	}
	[DataMember(Order = 21), Reactive] public bool IsSelected { get; set; }
	private string _errorDetails = String.Empty;
	[DataMember(Order = 22)] public string ErrorDetails
	{
		get => _errorDetails;
		set
		{
			this.RaiseAndSetIfChanged(ref _errorDetails, value ?? String.Empty);
			this.RaisePropertyChanged(nameof(FailureDetails));
			this.RaisePropertyChanged(nameof(DownloadDetailText));
		}
	}
	[DataMember(Order = 23), Reactive] public string ArchiveSha256 { get; set; } = String.Empty;
	[DataMember(Order = 24), Reactive] public string ThumbnailUrl { get; set; } = String.Empty;
	[DataMember(Order = 25), Reactive] public string InstallDestination { get; set; } = String.Empty;
	[DataMember(Order = 26), Reactive] public DateTimeOffset? InstalledAt { get; set; }
	[DataMember(Order = 27), Reactive] public AcquiredPackageSourceKind SourceKind { get; set; }
	[DataMember(Order = 28), Reactive] public string SourceFileName { get; set; } = String.Empty;
	[DataMember(Order = 29), Reactive] public string DetectedContentKind { get; set; } = String.Empty;
	[DataMember(Order = 30), Reactive] public string DetectedDestination { get; set; } = String.Empty;
	[DataMember(Order = 31), Reactive] public string InspectionSummary { get; set; } = String.Empty;
	[DataMember(Order = 32), Reactive] public bool InspectionCompleted { get; set; }

	[IgnoreDataMember] public NexusModManagerLink Authorization { get; set; }
	[IgnoreDataMember] public NxmArchiveInspection Inspection { get; set; }
	private double _bytesPerSecond;
	[IgnoreDataMember] public double BytesPerSecond
	{
		get => _bytesPerSecond;
		set
		{
			this.RaiseAndSetIfChanged(ref _bytesPerSecond, value);
			this.RaisePropertyChanged(nameof(DownloadDetailText));
		}
	}
	[IgnoreDataMember, Reactive] public double Progress { get; set; }
	[IgnoreDataMember, Reactive] public string NativeRequirementStatus { get; set; } = String.Empty;
	[IgnoreDataMember, Reactive] public string NativeRequirementLabel { get; set; } = String.Empty;
	[IgnoreDataMember, Reactive] public bool NativeRequirementWarning { get; set; }
	[IgnoreDataMember, Reactive] public bool HasAvailableArchive { get; set; }
	private bool _preserveExistingModPlacement;
	[IgnoreDataMember] public bool PreserveExistingModPlacement
	{
		get => _preserveExistingModPlacement;
		set
		{
			this.RaiseAndSetIfChanged(ref _preserveExistingModPlacement, value);
			this.RaisePropertyChanged(nameof(InstallActionText));
			this.RaisePropertyChanged(nameof(InstallActionToolTip));
		}
	}

	public string Identity => SourceKind != AcquiredPackageSourceKind.NexusMods
		? $"local:{ArchiveSha256}"
		: $"{ModId}:{FileId}";
	public bool HasNexusSource => SourceKind == AcquiredPackageSourceKind.NexusMods && ModId > 0;
	public string SourceText => SourceKind switch
	{
		AcquiredPackageSourceKind.LocalFile => "Local package",
		AcquiredPackageSourceKind.ReduxDownload => "Redux download",
		_ => "Nexus Mods"
	};
	public string StatusText => State switch
	{
		NxmDownloadState.Resolving => "Getting file details",
		NxmDownloadState.Queued => "Queued",
		NxmDownloadState.Downloading => "Downloading",
		NxmDownloadState.Paused => "Paused",
		NxmDownloadState.RetryWaiting => "Waiting to retry",
		NxmDownloadState.Downloaded => "Ready to install",
		NxmDownloadState.NeedsReview => "Needs attention",
		NxmDownloadState.Installing => "Installing",
		NxmDownloadState.Installed => "Installed",
		NxmDownloadState.NeedsFreshLink => "New link needed",
		NxmDownloadState.Failed => IsRemovalPending ? "Removal incomplete" : "Failed",
		NxmDownloadState.InstallFailed => ErrorCode switch
		{
			"missing-dependencies" => "Blocked by dependencies",
			"archive-unreadable" => "Archive unreadable",
			"archive-layout" => "Unsupported archive layout",
			"invalid-package" => "Package validation failed",
			"rollback-failed" => "Manual recovery required",
			_ => "Installation failed"
		},
		_ => String.Empty
	};
	public bool IsRemovalPending => ErrorCode == RemovalIncompleteErrorCode;
	public bool CanDownloadAgain => !IsRemovalPending && ErrorCode != "rollback-failed" && State is
		(NxmDownloadState.Downloaded or NxmDownloadState.NeedsReview or NxmDownloadState.InstallFailed or NxmDownloadState.Failed);
	public bool CanRetryInstall => State == NxmDownloadState.InstallFailed
		&& ErrorCode is not ("archive-unreadable" or "rollback-failed");
	public string FailureDetails => !String.IsNullOrWhiteSpace(ErrorDetails) ? ErrorDetails : ErrorCode switch
	{
		"missing-file" => "The downloaded archive is missing. Download it again; a fresh Nexus link may be required.",
		"file-size-mismatch" => "The archive size differs from the completed download record. Download it again before installing.",
		"archive-changed" => "The completed archive no longer matches the file Redux downloaded. Download it again before review.",
		"fresh-link-required" => "Open this file on Nexus Mods and choose Mod Manager Download to authorize a new download.",
		"install-failed" => "This older failure record did not retain its cause. Open Details to inspect the archive and check its requirements without installing it.",
		"transfer-failed" => "The transfer failed. Check the connection and available disk space, then retry the download.",
		RemovalIncompleteErrorCode => "Package removal did not finish. Close any application using its files, then choose Retry Delete.",
		_ => StatusText
	};
	public string MetadataText
	{
		get
		{
			var details = new List<string>();
			details.Add(SourceText);
			var packageSummary = InspectionSummary.Split(" · ")[0];
			if (packageSummary.Contains("PAK mod", StringComparison.Ordinal)) details.Add(packageSummary.Replace("PAK mod", "PAK", StringComparison.Ordinal));
			if (!String.IsNullOrWhiteSpace(Author)) details.Add($"by {Author}");
			if (!String.IsNullOrWhiteSpace(Version)) details.Add($"v{Version}");
			if (SizeBytes > 0) details.Add(FormatBytes(SizeBytes));
			return String.Join("  ·  ", details);
		}
	}
	public string DownloadDetailText => State switch
	{
		NxmDownloadState.Downloading when SizeBytes > 0 =>
			$"{FormatBytes(BytesReceived)} of {FormatBytes(SizeBytes)}{(BytesPerSecond > 0 ? $"  ·  {FormatBytes((long)BytesPerSecond)}/s" : String.Empty)}",
		NxmDownloadState.Paused when BytesReceived > 0 => $"{FormatBytes(BytesReceived)} downloaded",
		NxmDownloadState.RetryWaiting when BytesReceived > 0 => $"{FormatBytes(BytesReceived)} downloaded  ·  retrying shortly",
		NxmDownloadState.NeedsFreshLink => "Open its Nexus file page to request a fresh Mod Manager Download link.",
		NxmDownloadState.Downloaded when !String.IsNullOrWhiteSpace(InspectionSummary) => InspectionSummary.Contains("PAK mod", StringComparison.Ordinal) ? String.Empty : InspectionSummary,
		NxmDownloadState.Failed or NxmDownloadState.InstallFailed or NxmDownloadState.NeedsReview
			=> FailureDetails,
		_ => String.Empty
	};
	public string NexusActionText => State == NxmDownloadState.NeedsFreshLink ? "Get New Link" : "Open Nexus";
	public string InstallActionText
	{
		get
		{
			if (PreserveExistingModPlacement
				|| (State == NxmDownloadState.Installed && DetectedDestination == "Inactive Mods"))
				return "Reinstall";
			var prefix = State switch
			{
				NxmDownloadState.InstallFailed => "Try Again",
				NxmDownloadState.Installed => "Reinstall",
				_ => "Install"
			};
			return DetectedDestination switch
			{
				"Inactive Mods" => prefix,
				"Save Games" => $"{prefix} with Save Manager",
				"Game-directory Mods" => $"{prefix} with Game-directory Manager",
				_ => prefix
			};
		}
	}
	public string InstallActionToolTip => DetectedDestination == "Inactive Mods"
		? "New mods go to Inactive Mods. Updates preserve each installed mod's active or inactive state and load-order position."
		: String.Empty;
	public string RemoveActionText => IsRemovalPending ? "Retry Delete" : State == NxmDownloadState.Installed ? "Clear" : "Delete";
	public bool IsInstalledHistory => State == NxmDownloadState.Installed;
	public string NexusActionToolTip => State == NxmDownloadState.NeedsFreshLink
		? "Open this exact file on Nexus Mods, then choose Mod Manager Download again."
		: "Open this file on Nexus Mods.";
	public Uri NexusPage => ModId > 0
		? new Uri($"https://www.nexusmods.com/baldursgate3/mods/{ModId}?tab=files&file_id={FileId}")
		: null;

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
