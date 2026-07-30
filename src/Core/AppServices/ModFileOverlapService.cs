using DivinityModManager.Models;

using LSLib.LS;

namespace DivinityModManager.AppServices;

/// <summary>
/// Reads PAK file tables on demand and reports shared internal paths. It never
/// extracts, edits, reorders, or classifies an overlap as a definite conflict.
/// </summary>
public static class ModFileOverlapService
{
	public static ModFileOverlapScanResult AnalyzePackages(
		IEnumerable<DivinityModData> mods,
		CancellationToken cancellationToken = default,
		IProgress<ModFileOverlapProgress> progress = null)
	{
		var candidates = (mods ?? Enumerable.Empty<DivinityModData>())
			.Where(mod => mod != null && !mod.IsVisualDivider)
			.Where(mod => !String.IsNullOrWhiteSpace(mod.FilePath))
			.GroupBy(
				mod => NormalizePackagePath(mod.FilePath),
				StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToArray();
		var indexes = new List<ModFilePathIndex>(candidates.Length);
		var failures = new List<ModFileOverlapScanFailure>();

		for (var index = 0; index < candidates.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var mod = candidates[index];
			var displayName = String.IsNullOrWhiteSpace(mod.DisplayName)
				? mod.Name
				: mod.DisplayName;
			var packageFileName = Path.GetFileName(mod.FilePath);
			progress?.Report(new ModFileOverlapProgress(index, candidates.Length, displayName));

			if (!File.Exists(mod.FilePath))
			{
				failures.Add(new ModFileOverlapScanFailure(
					displayName,
					packageFileName,
					"Package file was not found."));
				continue;
			}

			if (!String.Equals(
				Path.GetExtension(mod.FilePath),
				".pak",
				StringComparison.OrdinalIgnoreCase))
			{
				failures.Add(new ModFileOverlapScanFailure(
					displayName,
					packageFileName,
					"File is not a PAK package."));
				continue;
			}

			try
			{
				var reader = new PackageReader();
				using var package = reader.Read(mod.FilePath, metadataOnly: true);
				var identity = new ModFileOverlapPackageIdentity(
					mod.UUID,
					displayName,
					packageFileName);
				indexes.Add(new ModFilePathIndex(
					identity,
					package.Files.Select(file => file?.Name)));
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				failures.Add(new ModFileOverlapScanFailure(
					displayName,
					packageFileName,
					$"{ex.GetType().Name}: {ex.Message}"));
			}
			finally
			{
				progress?.Report(new ModFileOverlapProgress(
					index + 1,
					candidates.Length,
					displayName));
			}
		}

		return AnalyzeIndexes(
			indexes,
			candidates.Length,
			failures,
			cancellationToken);
	}

	public static ModFileOverlapScanResult AnalyzeIndexes(
		IEnumerable<ModFilePathIndex> indexes,
		int? candidatePackageCount = null,
		IEnumerable<ModFileOverlapScanFailure> failures = null,
		CancellationToken cancellationToken = default)
	{
		var packageIndexes = (indexes ?? Enumerable.Empty<ModFilePathIndex>())
			.Where(index => index?.Package != null)
			.ToArray();
		var packagesByPath = new Dictionary<
			string,
			Dictionary<string, ModFileOverlapPackageIdentity>>(
			StringComparer.OrdinalIgnoreCase);

		foreach (var packageIndex in packageIndexes)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var identityKey = packageIndex.Package.IdentityKey;
			if (String.IsNullOrWhiteSpace(identityKey))
			{
				continue;
			}

			foreach (var internalPath in packageIndex.InternalPaths
				.Select(NormalizeInternalPath)
				.Where(path => !String.IsNullOrWhiteSpace(path))
				.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (!packagesByPath.TryGetValue(internalPath, out var packages))
				{
					packages = new Dictionary<
						string,
						ModFileOverlapPackageIdentity>(
						StringComparer.OrdinalIgnoreCase);
					packagesByPath.Add(internalPath, packages);
				}
				packages.TryAdd(identityKey, packageIndex.Package);
			}
		}

		var entries = packagesByPath
			.Where(item => item.Value.Count > 1)
			.Select(item => new ModFileOverlapEntry(
				item.Key,
				item.Value.Values
					.OrderBy(package => package.DisplayName, StringComparer.OrdinalIgnoreCase)))
			.OrderByDescending(entry => entry.PackageCount)
			.ThenBy(entry => entry.InternalPath, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		return new ModFileOverlapScanResult(
			candidatePackageCount ?? packageIndexes.Length,
			packageIndexes.Length,
			packagesByPath.Count,
			entries,
			failures);
	}

	public static string NormalizeInternalPath(string path)
	{
		if (String.IsNullOrWhiteSpace(path))
		{
			return String.Empty;
		}

		return path
			.Trim()
			.Replace('\\', '/')
			.TrimStart('/');
	}

	private static string NormalizePackagePath(string path)
	{
		var trimmedPath = path?.Trim() ?? String.Empty;
		try
		{
			return Path.GetFullPath(trimmedPath);
		}
		catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
		{
			// Keep malformed paths in the candidate set so the normal per-package
			// failure handling can report them without aborting the entire scan.
			return trimmedPath;
		}
	}
}
