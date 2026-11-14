namespace DivinityModManager.Models;

public enum ModContentInheritanceStatus
{
	None,
	ResolvedWithinPackage,
	ExternalParent,
	AmbiguousParent,
	Cycle,
	LimitReached
}

public sealed record ModContentField(
	string Name, string Value, string SourceRecord, bool IsInherited,
	string SourcePath = "", int Line = 0);

/// <summary>A declaration in this package; not a comparison with vanilla or the active load order.</summary>
public sealed record ModContentRecord(
	string Type, string Name, string ParentName, string SourcePath, int Line,
	IReadOnlyList<ModContentField> Fields,
	ModContentInheritanceStatus InheritanceStatus = ModContentInheritanceStatus.None)
{
	public bool HasIncompleteInheritance => InheritanceStatus is not
		(ModContentInheritanceStatus.None or ModContentInheritanceStatus.ResolvedWithinPackage);
}

public sealed record ModContentInspectionIssue(string Message, string SourcePath = "", int Line = 0);

public sealed record ModContentFile(
	string SourcePath, ulong SizeBytes, string Category, bool IsInspected, int RecordCount);

public sealed record ModContentInspectionResult(
	string PackageFileName,
	IReadOnlyList<ModContentRecord> Records,
	IReadOnlyList<ModContentInspectionIssue> Issues,
	IReadOnlyList<ModContentFile> Files,
	bool IsPartial);

public sealed record ModContentInspectionProgress(int CompletedFileCount, int TotalFileCount, string CurrentFilePath)
{
	public double Fraction => TotalFileCount <= 0 ? 0 : Math.Clamp((double)CompletedFileCount / TotalFileCount, 0, 1);
}
