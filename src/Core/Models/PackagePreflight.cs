using DivinityModManager.Models.Health;

namespace DivinityModManager.Models;

/// <summary>
/// Read-only inspection result for a developer-selected PAK. A preflight report
/// never installs, extracts, edits, or registers the package it describes.
/// </summary>
public sealed class PackagePreflightReport
{
	public string PackagePath { get; }
	public string PackageFileName => Path.GetFileName(PackagePath);
	public DivinityModData Mod { get; }
	public IReadOnlyList<PackagePreflightFinding> Findings { get; }
	public int InternalFileCount { get; }
	public long PackageSize { get; }
	public string PackageSizeText => PackageSize <= 0
		? "Unavailable"
		: PackageSize >= 1024L * 1024L * 1024L
			? $"{PackageSize / (1024d * 1024d * 1024d):0.##} GB"
			: PackageSize >= 1024L * 1024L
				? $"{PackageSize / (1024d * 1024d):0.##} MB"
				: $"{PackageSize / 1024d:0.##} KB";
	public string InternalFileCountText => $"{InternalFileCount:N0}";
	public string DeclaredDependencyCountText => $"{DeclaredDependencyCount:N0}";
	public int DeclaredDependencyCount => Mod?.Dependencies?.Count ?? 0;
	public int ErrorCount => Findings.Count(finding => finding.Severity == ModHealthSeverity.Error);
	public int WarningCount => Findings.Count(finding => finding.Severity == ModHealthSeverity.Warning);
	public int InfoCount => Findings.Count(finding => finding.Severity == ModHealthSeverity.Info);
	public bool HasErrors => ErrorCount > 0;
	public bool HasWarnings => WarningCount > 0;
	public bool IsReadable => Mod != null;
	public string DisplayName => IsReadable && !String.IsNullOrWhiteSpace(Mod.DisplayName)
		? Mod.DisplayName
		: Path.GetFileNameWithoutExtension(PackagePath);
	public string Author => IsReadable && !String.IsNullOrWhiteSpace(Mod.Author) ? Mod.Author : "Not provided";
	public string Version => IsReadable && !String.IsNullOrWhiteSpace(Mod.Version?.Version)
		? Mod.Version.Version
		: "Not provided";
	public string Uuid => IsReadable && !String.IsNullOrWhiteSpace(Mod.UUID) ? Mod.UUID : "Not provided";
	public string Folder => IsReadable && !String.IsNullOrWhiteSpace(Mod.Folder) ? Mod.Folder : "Not provided";
	public string StatusTitle => HasErrors
		? "Package needs attention"
		: HasWarnings
			? "Review recommended"
			: "No blocking issues found";
	public string StatusDescription => HasErrors
		? "Redux found package metadata or dependency problems that should be corrected before release."
		: HasWarnings
			? "The package is readable, but some release details are worth reviewing."
			: "Redux could read the package and did not detect a blocking metadata or dependency problem.";
	public string FindingSummary
	{
		get
		{
			var parts = new List<string>();
			if (ErrorCount > 0) parts.Add($"{ErrorCount} error{(ErrorCount == 1 ? String.Empty : "s")}");
			if (WarningCount > 0) parts.Add($"{WarningCount} warning{(WarningCount == 1 ? String.Empty : "s")}");
			if (InfoCount > 0) parts.Add($"{InfoCount} note{(InfoCount == 1 ? String.Empty : "s")}");
			return parts.Count == 0 ? "No findings" : String.Join(" · ", parts);
		}
	}
	public string DetectedFeatures
	{
		get
		{
			if (!IsReadable) return "Package could not be inspected";

			var features = new List<string>();
			if (Mod.CreatorManifest?.IsValid == true) features.Add("Redux creator metadata");
			if (Mod.HasScriptExtenderSettings) features.Add("Script Extender configuration");
			if (Mod.OsirisModStatus == DivinityOsirisModStatus.SCRIPTS) features.Add("Osiris scripting");
			if (Mod.OsirisModStatus == DivinityOsirisModStatus.MODFIXER) features.Add("Mod Fixer files");
			if (Mod.IsForceLoaded) features.Add("Always-loaded overrides");
			if (DeclaredDependencyCount > 0)
				features.Add($"{DeclaredDependencyCount} declared dependenc{(DeclaredDependencyCount == 1 ? "y" : "ies")}");
			return features.Count == 0 ? "Standard package" : String.Join(" · ", features);
		}
	}

	public PackagePreflightReport(
		string packagePath,
		DivinityModData mod,
		int internalFileCount,
		long packageSize,
		IEnumerable<PackagePreflightFinding> findings)
	{
		PackagePath = packagePath ?? String.Empty;
		Mod = mod;
		InternalFileCount = Math.Max(0, internalFileCount);
		PackageSize = Math.Max(0, packageSize);
		Findings = (findings ?? Enumerable.Empty<PackagePreflightFinding>())
			.Where(finding => finding != null)
			.DistinctBy(
				finding => $"{finding.Severity}|{finding.Title}|{finding.Message}",
				StringComparer.Ordinal)
			.OrderByDescending(finding => finding.Severity)
			.ThenBy(finding => finding.Title, StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	public PackagePreflightReport WithSource(string packagePath, long packageSize) => new(
		packagePath,
		Mod,
		InternalFileCount,
		packageSize,
		Findings);
}

public sealed record PackagePreflightFinding(
	ModHealthSeverity Severity,
	string Title,
	string Message);
