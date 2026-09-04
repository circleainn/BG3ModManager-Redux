namespace DivinityModManager.Models.Health;

/// <summary>
/// Stable identifiers that future UI and settings can use without matching display text.
/// </summary>
public enum ModHealthFindingCode
{
	MissingDependency,
	InactiveDependency,
	SelfDependency,
	DependencyVersionTooOld,
	DependencyLoadsLater,
	DependencyCycle,
	InvalidUuid,
	DuplicateUuid,
	InvalidCreatorManifest,
	ScriptExtenderUnavailable,
	ScriptExtenderVersionMismatch,
	DeclaredConflict,
	LegacyModFixerIncluded,
	AlwaysLoaded,
	ContainsFileOverrides,
	AlwaysLoadedWithLoadOrderEntry,
	McmNotActive,
	ModioManagedSource
}
