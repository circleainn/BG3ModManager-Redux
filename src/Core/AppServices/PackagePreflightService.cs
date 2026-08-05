using DivinityModManager.Models;
using DivinityModManager.Models.Health;
using DivinityModManager.Util;

namespace DivinityModManager.AppServices;

/// <summary>
/// Composes Redux's existing package loader and conservative diagnostics into
/// a developer-facing, read-only release preflight.
/// </summary>
public static class PackagePreflightService
{
	private static readonly HashSet<string> DevelopmentFileNames = new(StringComparer.OrdinalIgnoreCase)
	{
		".DS_Store",
		"Thumbs.db",
		"desktop.ini"
	};

	private static readonly HashSet<string> DevelopmentExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".bak",
		".blend",
		".dmp",
		".log",
		".pdb",
		".psd",
		".tmp",
		".xcf"
	};

	public static async Task<PackagePreflightReport> AnalyzeAsync(
		string packagePath,
		IEnumerable<DivinityModData> installedMods,
		CancellationToken cancellationToken = default)
	{
		if (String.IsNullOrWhiteSpace(packagePath))
			throw new ArgumentException("A package path is required.", nameof(packagePath));

		var normalizedPath = Path.GetFullPath(packagePath);
		if (!File.Exists(normalizedPath))
		{
			return Unreadable(normalizedPath, "Package file was not found.");
		}
		if (!String.Equals(Path.GetExtension(normalizedPath), ".pak", StringComparison.OrdinalIgnoreCase))
		{
			return Unreadable(normalizedPath, "The selected file is not a PAK package.");
		}

		cancellationToken.ThrowIfCancellationRequested();
		var builtins = DivinityApp.IgnoredMods.Items
			.Where(mod => mod != null && !String.IsNullOrWhiteSpace(mod.Folder))
			.GroupBy(mod => mod.Folder, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		var mod = await DivinityModDataLoader.LoadModDataFromPakAsync(
			normalizedPath,
			builtins,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();

		if (mod == null)
		{
			return Unreadable(
				normalizedPath,
				"Redux could not read usable module metadata or a supported override structure from this package.");
		}

		return AnalyzeLoadedPackage(normalizedPath, mod, installedMods);
	}

	public static PackagePreflightReport AnalyzeLoadedPackage(
		string packagePath,
		DivinityModData mod,
		IEnumerable<DivinityModData> installedMods)
	{
		if (mod == null)
			return Unreadable(packagePath, "Package metadata could not be read.");

		var installed = (installedMods ?? Enumerable.Empty<DivinityModData>())
			.Concat(DivinityApp.IgnoredMods.Items)
			.Where(candidate => candidate != null && !candidate.IsVisualDivider)
			.Where(candidate => !PathsEqual(candidate.FilePath, packagePath))
			.DistinctBy(candidate => candidate.UUID, StringComparer.OrdinalIgnoreCase)
			.ToList();
		var installedUuids = installed
			.Where(candidate => !String.IsNullOrWhiteSpace(candidate.UUID))
			.Select(candidate => candidate.UUID)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var missingDependencies = mod.Dependencies.Items
			.Where(dependency => !String.IsNullOrWhiteSpace(dependency.UUID))
			.Where(dependency => !String.Equals(
				dependency.UUID,
				mod.UUID,
				StringComparison.OrdinalIgnoreCase))
			.Where(dependency => !installedUuids.Contains(dependency.UUID))
			.DistinctBy(dependency => dependency.UUID, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var analysisSet = installed
			.Where(candidate => String.IsNullOrWhiteSpace(mod.UUID)
				|| !String.Equals(candidate.UUID, mod.UUID, StringComparison.OrdinalIgnoreCase))
			.Append(mod)
			.ToArray();
		var snapshot = new ModHealthAnalyzer()
			.AnalyzeAll(
				analysisSet,
				analysisSet,
				enableLoadOrderAdvisor: false,
				disableModioWarnings: true)
			.First(result => ReferenceEquals(result.Mod, mod));
		var findings = snapshot.Findings
			.Where(finding => finding.Code is not
				(ModHealthFindingCode.InvalidUuid or ModHealthFindingCode.DuplicateUuid))
			.Select(finding => new PackagePreflightFinding(
				finding.Severity,
				finding.Title,
				finding.Message))
			.ToList();

		AddIdentityFindings(mod, installed, findings);
		foreach (var dependency in missingDependencies)
		{
			var dependencyName = String.IsNullOrWhiteSpace(dependency.Name)
				? dependency.UUID
				: dependency.Name;
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"Missing dependency",
				$"{dependencyName} is declared by this package but is not installed in the current Redux library."));
		}
		AddContentFindings(mod.Files, findings);

		long packageSize = 0;
		try
		{
			if (File.Exists(packagePath)) packageSize = new FileInfo(packagePath).Length;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Info,
				"Package size unavailable",
				"Redux could inspect the package contents but could not read its file size."));
		}

		return new PackagePreflightReport(
			packagePath,
			mod,
			mod.Files?.Count ?? 0,
			packageSize,
			findings);
	}

	private static void AddIdentityFindings(
		DivinityModData mod,
		IReadOnlyCollection<DivinityModData> installedMods,
		ICollection<PackagePreflightFinding> findings)
	{
		if (!mod.HasMetadata && mod.IsForceLoaded)
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Info,
				"Override-only package",
				"No module metadata is required for this package because Redux detected direct game-file overrides."));
			return;
		}

		if (String.IsNullOrWhiteSpace(mod.Name))
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"Module name is missing",
				"The module metadata should provide a readable Name value."));
		}
		if (String.IsNullOrWhiteSpace(mod.Folder))
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"Module folder is missing",
				"The module metadata should provide the folder used by the package's Mods and Public paths."));
		}
		if (!Guid.TryParse(mod.UUID, out _))
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"Invalid mod UUID",
				"The module UUID is missing or is not a valid GUID."));
		}
		else if (installedMods.Any(candidate =>
			String.Equals(candidate.UUID, mod.UUID, StringComparison.OrdinalIgnoreCase)))
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Warning,
				"UUID already installed",
				"An installed package already uses this module UUID. This can be expected while validating an update, but unrelated releases must use unique UUIDs."));
		}
		if (String.IsNullOrWhiteSpace(mod.Author))
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Warning,
				"Author is not provided",
				"Adding an Author value makes the package easier to identify in mod managers and support reports."));
		}
		if (mod.Version?.VersionInt == 0)
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Warning,
				"Version is still 0.0.0.0",
				"Set a release version so updates can be distinguished reliably."));
		}
	}

	private static void AddContentFindings(
		IEnumerable<string> internalPaths,
		ICollection<PackagePreflightFinding> findings)
	{
		var paths = (internalPaths ?? Enumerable.Empty<string>())
			.Where(path => !String.IsNullOrWhiteSpace(path))
			.ToArray();
		if (paths.Length == 0)
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"Package contains no readable files",
				"Redux did not find any internal file-table entries in the package."));
			return;
		}

		var debris = paths
			.Where(path =>
				DevelopmentFileNames.Contains(Path.GetFileName(path))
				|| DevelopmentExtensions.Contains(Path.GetExtension(path)))
			.Select(path => path.Replace('\\', '/'))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(4)
			.ToArray();
		if (debris.Length == 0) return;

		findings.Add(new PackagePreflightFinding(
			ModHealthSeverity.Warning,
			"Development files are included",
			$"Review whether these files belong in the release: {String.Join(", ", debris)}"));
	}

	private static PackagePreflightReport Unreadable(string packagePath, string message) => new(
		packagePath,
		null,
		0,
		0,
		new[]
		{
			new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"Package could not be inspected",
				message)
		});

	private static bool PathsEqual(string left, string right)
	{
		if (String.IsNullOrWhiteSpace(left) || String.IsNullOrWhiteSpace(right)) return false;
		try
		{
			return String.Equals(
				Path.GetFullPath(left),
				Path.GetFullPath(right),
				StringComparison.OrdinalIgnoreCase);
		}
		catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
		{
			return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
		}
	}
}
