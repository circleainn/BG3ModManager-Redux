namespace DivinityModManager.Models;

/// <summary>
/// Minimal package identity shown by the read-only file-overlap inspector.
/// Local filesystem paths are deliberately excluded from the result model.
/// </summary>
public sealed class ModFileOverlapPackageIdentity
{
	public string UUID { get; }
	public string DisplayName { get; }
	public string PackageFileName { get; }

	public ModFileOverlapPackageIdentity(string uuid, string displayName, string packageFileName)
	{
		UUID = uuid?.Trim() ?? String.Empty;
		PackageFileName = packageFileName?.Trim() ?? String.Empty;
		DisplayName = String.IsNullOrWhiteSpace(displayName)
			? PackageFileName
			: displayName.Trim();
	}

	internal string IdentityKey => !String.IsNullOrWhiteSpace(UUID)
		? UUID
		: PackageFileName;
}

/// <summary>
/// One package's normalized internal path index. Exposed separately so overlap
/// semantics can be tested without opening real PAK files.
/// </summary>
public sealed class ModFilePathIndex
{
	public ModFileOverlapPackageIdentity Package { get; }
	public IReadOnlyCollection<string> InternalPaths { get; }

	public ModFilePathIndex(
		ModFileOverlapPackageIdentity package,
		IEnumerable<string> internalPaths)
	{
		Package = package ?? throw new ArgumentNullException(nameof(package));
		InternalPaths = (internalPaths ?? Enumerable.Empty<string>()).ToArray();
	}
}

/// <summary>
/// One internal PAK path present in two or more scanned packages.
/// This is an overlap, not proof that the mods conflict.
/// </summary>
public sealed class ModFileOverlapEntry
{
	public string InternalPath { get; }
	public IReadOnlyList<ModFileOverlapPackageIdentity> Packages { get; }
	public int PackageCount => Packages.Count;
	public string PackageCountText => PackageCount == 1
		? "1 package"
		: $"{PackageCount} packages";
	public string PackageSummary => String.Join(
		", ",
		Packages.Select(package => package.DisplayName));

	public ModFileOverlapEntry(
		string internalPath,
		IEnumerable<ModFileOverlapPackageIdentity> packages)
	{
		InternalPath = internalPath ?? String.Empty;
		Packages = (packages ?? Enumerable.Empty<ModFileOverlapPackageIdentity>())
			.ToArray();
	}
}

public sealed class ModFileOverlapScanFailure
{
	public string ModName { get; }
	public string PackageFileName { get; }
	public string Reason { get; }

	public ModFileOverlapScanFailure(
		string modName,
		string packageFileName,
		string reason)
	{
		ModName = modName?.Trim() ?? String.Empty;
		PackageFileName = packageFileName?.Trim() ?? String.Empty;
		Reason = reason?.Trim() ?? "Package could not be read.";
	}
}

public sealed class ModFileOverlapScanResult
{
	public int CandidatePackageCount { get; }
	public int ScannedPackageCount { get; }
	public int UniqueInternalPathCount { get; }
	public IReadOnlyList<ModFileOverlapEntry> Entries { get; }
	public IReadOnlyList<ModFileOverlapScanFailure> Failures { get; }
	public int OverlapPathCount => Entries.Count;
	public int AffectedPackageCount { get; }
	public bool HasOverlaps => Entries.Count > 0;
	public bool HasFailures => Failures.Count > 0;

	public ModFileOverlapScanResult(
		int candidatePackageCount,
		int scannedPackageCount,
		int uniqueInternalPathCount,
		IEnumerable<ModFileOverlapEntry> entries,
		IEnumerable<ModFileOverlapScanFailure> failures)
	{
		CandidatePackageCount = Math.Max(0, candidatePackageCount);
		ScannedPackageCount = Math.Max(0, scannedPackageCount);
		UniqueInternalPathCount = Math.Max(0, uniqueInternalPathCount);
		Entries = (entries ?? Enumerable.Empty<ModFileOverlapEntry>()).ToArray();
		Failures = (failures ?? Enumerable.Empty<ModFileOverlapScanFailure>()).ToArray();
		AffectedPackageCount = Entries
			.SelectMany(entry => entry.Packages)
			.Select(package => package.IdentityKey)
			.Where(key => !String.IsNullOrWhiteSpace(key))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Count();
	}
}

public sealed class ModFileOverlapProgress
{
	public int CompletedPackageCount { get; }
	public int TotalPackageCount { get; }
	public string CurrentModName { get; }
	public double Fraction => TotalPackageCount <= 0
		? 0d
		: Math.Clamp((double)CompletedPackageCount / TotalPackageCount, 0d, 1d);

	public ModFileOverlapProgress(
		int completedPackageCount,
		int totalPackageCount,
		string currentModName)
	{
		CompletedPackageCount = Math.Max(0, completedPackageCount);
		TotalPackageCount = Math.Max(0, totalPackageCount);
		CurrentModName = currentModName?.Trim() ?? String.Empty;
	}
}
