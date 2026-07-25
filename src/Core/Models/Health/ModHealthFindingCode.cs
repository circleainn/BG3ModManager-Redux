namespace DivinityModManager.Models.Health;

/// <summary>
/// Stable identifiers that future UI and settings can use without matching display text.
/// </summary>
public enum ModHealthFindingCode
{
	MissingDependency,
	InactiveDependency,
	DependencyLoadsLater,
	InvalidUuid,
	DuplicateUuid,
	ScriptExtenderUnavailable,
	ScriptExtenderVersionMismatch,
	DeclaredConflict,
	LegacyModFixerIncluded,
	AlwaysLoaded,
	ContainsFileOverrides,
	AlwaysLoadedWithLoadOrderEntry,
	ModioManagedSource
}
