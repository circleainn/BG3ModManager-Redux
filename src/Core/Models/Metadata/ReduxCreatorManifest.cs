namespace DivinityModManager.Models.Metadata;

public enum ReduxCreatorManifestState
{
	None,
	Valid,
	Invalid
}

/// <summary>
/// Read-only result of discovering and validating a creator-supplied manifest.
/// The manifest is never allowed to mutate package identity or load-order state.
/// </summary>
public sealed class ReduxCreatorManifestData
{
	public static ReduxCreatorManifestData NotPresent { get; } = new();

	public ReduxCreatorManifestState State { get; init; }
	public bool IsPresent => State != ReduxCreatorManifestState.None;
	public bool IsValid => State == ReduxCreatorManifestState.Valid;
	public string Diagnostic { get; init; } = String.Empty;
	public string Name { get; init; }
	public string Version { get; init; }
	public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
	public string Description { get; init; }
	public string Homepage { get; init; }
	public IReadOnlyList<ReduxCreatorSourceClaim> Sources { get; init; } = Array.Empty<ReduxCreatorSourceClaim>();
	public IReadOnlyList<ReduxCreatorModuleClaim> Modules { get; init; } = Array.Empty<ReduxCreatorModuleClaim>();
	public IReadOnlyList<ReduxCreatorDependencyClaim> Dependencies { get; init; } = Array.Empty<ReduxCreatorDependencyClaim>();
}

public sealed record ReduxCreatorSourceClaim(
	string Service,
	long ProjectId,
	long? FileId);

public sealed record ReduxCreatorModuleClaim(
	string Uuid,
	string Name,
	string Folder,
	string Version,
	string Pak);

public sealed record ReduxCreatorDependencyClaim(
	string Uuid,
	string Name,
	string MinimumVersion,
	bool Optional);
