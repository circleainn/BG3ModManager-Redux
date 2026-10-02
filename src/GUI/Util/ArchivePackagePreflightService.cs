using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;

using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace DivinityModManager.Util;

public enum ArchivePackagePreflightKind
{
	PakArchive,
	ReviewedGameDirectory,
	UnreviewedNative,
	SaveGame,
	Mixed
}

public sealed record ArchiveSavePreflightEntry(
	string DisplayName,
	string CampaignName,
	string FolderName,
	DateTime ModifiedUtc,
	long SizeBytes,
	Bg3SaveDifficulty Difficulty);

public sealed class ArchivePackagePreflightResult
{
	public string ArchivePath { get; }
	public int EntryCount { get; }
	public long ArchiveSize { get; }
	public IReadOnlyList<PackagePreflightReport> Packages { get; }
	public IReadOnlyList<PackagePreflightFinding> Findings { get; }
	public ArchivePackagePreflightKind Kind { get; }
	public ReduxGameDirectoryArchiveInspection GameDirectoryInspection { get; }
	public IReadOnlyList<ArchiveSavePreflightEntry> SaveGames { get; }
	public IReadOnlyList<string> EntryNames { get; }

	public ArchivePackagePreflightResult(
		string archivePath,
		int entryCount,
		long archiveSize,
		IEnumerable<PackagePreflightReport> packages,
		IEnumerable<PackagePreflightFinding> findings,
		ArchivePackagePreflightKind kind = ArchivePackagePreflightKind.PakArchive,
		ReduxGameDirectoryArchiveInspection gameDirectoryInspection = null,
		IEnumerable<ArchiveSavePreflightEntry> saveGames = null,
		IEnumerable<string> entryNames = null)
	{
		ArchivePath = archivePath ?? String.Empty;
		EntryCount = Math.Max(0, entryCount);
		ArchiveSize = Math.Max(0, archiveSize);
		Packages = (packages ?? Enumerable.Empty<PackagePreflightReport>()).ToArray();
		Findings = (findings ?? Enumerable.Empty<PackagePreflightFinding>()).ToArray();
		Kind = kind;
		GameDirectoryInspection = gameDirectoryInspection;
		SaveGames = (saveGames ?? Enumerable.Empty<ArchiveSavePreflightEntry>()).ToArray();
		EntryNames = (entryNames ?? Enumerable.Empty<string>()).ToArray();
	}
}

/// <summary>
/// Reads developer-selected release archives without installing their contents.
/// Contained PAKs are staged under an isolated temporary directory and removed
/// after the core package preflight has completed.
/// </summary>
public static class ArchivePackagePreflightService
{
	private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".7z", ".7zip", ".gz", ".gzip", ".rar", ".tar", ".tgz", ".zip"
	};

	private static readonly HashSet<string> DevelopmentFileNames = new(StringComparer.OrdinalIgnoreCase)
	{
		".DS_Store", "Thumbs.db", "desktop.ini"
	};

	private static readonly HashSet<string> DevelopmentExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".bak", ".blend", ".dmp", ".log", ".pdb", ".psd", ".tmp", ".xcf"
	};

	public static bool IsSupportedArchive(string path)
	{
		if (String.IsNullOrWhiteSpace(path)) return false;
		var lowerPath = path.ToLowerInvariant();
		return lowerPath.EndsWith(".tar.gz", StringComparison.Ordinal)
			|| SupportedExtensions.Contains(Path.GetExtension(lowerPath));
	}

	public static async Task<ArchivePackagePreflightResult> AnalyzeAsync(
		string archivePath,
		IEnumerable<DivinityModData> installedMods,
		CancellationToken cancellationToken = default)
	{
		if (String.IsNullOrWhiteSpace(archivePath))
			throw new ArgumentException("An archive path is required.", nameof(archivePath));

		var normalizedPath = Path.GetFullPath(archivePath);
		if (!File.Exists(normalizedPath))
			return Unreadable(normalizedPath, "Archive file was not found.");
		var isLooseSave = Path.GetExtension(normalizedPath).Equals(".lsv", StringComparison.OrdinalIgnoreCase);
		if (!isLooseSave && !IsSupportedArchive(normalizedPath))
			return Unreadable(normalizedPath, "The selected archive format is not supported.");

		var temporaryRoot = CreateTemporaryRoot();
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (isLooseSave)
			{
				try
				{
					var looseSaves = await InspectSaveInputAsync(normalizedPath, temporaryRoot, cancellationToken);
					return CreateSaveResult(normalizedPath, 1, new FileInfo(normalizedPath).Length,
						looseSaves, [Path.GetFileName(normalizedPath)]);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					DivinityApp.Log($"Loose save preflight failed for '{Path.GetFileName(normalizedPath)}':\n{ex}");
					return new ArchivePackagePreflightResult(
						normalizedPath, 1, new FileInfo(normalizedPath).Length,
						Array.Empty<PackagePreflightReport>(),
						[new PackagePreflightFinding(ModHealthSeverity.Error, "Save data could not be validated", ex.Message)],
						ArchivePackagePreflightKind.SaveGame,
						entryNames: [Path.GetFileName(normalizedPath)]);
				}
			}

			await using var fileStream = new FileStream(
				normalizedPath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				4096,
				FileOptions.Asynchronous | FileOptions.SequentialScan);
			using var archive = ArchiveFactory.OpenArchive(fileStream, new ReaderOptions());
			var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToArray();
			var entryNames = entries.Select(entry => NormalizeEntryPath(entry.Key)).ToArray();
			var pakEntries = entries
				.Where(entry => entry.Key.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
				.ToArray();
			var dllEntries = entryNames.Where(name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToArray();
			var hasSaveEntries = entryNames.Any(name => name.EndsWith(".lsv", StringComparison.OrdinalIgnoreCase));
			var hasNativeEntries = dllEntries.Length > 0;
			ReduxGameDirectoryArchiveInspection nativeInspection = null;
			string unreviewedNativeReason = null;
			if (hasNativeEntries)
			{
				try
				{
					nativeInspection = ReduxGameDirectoryInstallService.TryInspectKnownArchive(normalizedPath, computeArchiveHash: false);
				}
				catch (ReduxUnsupportedGameDirectoryArchiveException ex)
				{
					unreviewedNativeReason = ex.Message;
				}
				catch (InvalidDataException ex)
				{
					unreviewedNativeReason = ex.Message;
				}
			}
			var kind = nativeInspection != null
				? pakEntries.Length > 0 ? ArchivePackagePreflightKind.Mixed : ArchivePackagePreflightKind.ReviewedGameDirectory
				: hasSaveEntries && (pakEntries.Length > 0 || hasNativeEntries)
					? ArchivePackagePreflightKind.Mixed
					: hasSaveEntries ? ArchivePackagePreflightKind.SaveGame
						: hasNativeEntries && pakEntries.Length > 0 ? ArchivePackagePreflightKind.Mixed
							: hasNativeEntries ? ArchivePackagePreflightKind.UnreviewedNative
								: ArchivePackagePreflightKind.PakArchive;
			var findings = AnalyzeEntryNames(entryNames, requirePak: kind == ArchivePackagePreflightKind.PakArchive).ToList();
			if (findings.Any(finding => finding.Title == ArchivePakImport.DuplicateNamesTitle))
				return new ArchivePackagePreflightResult(normalizedPath, entries.Length, fileStream.Length,
					[], findings, kind, nativeInspection, entryNames: entryNames);
			AddNativeFindings(nativeInspection, unreviewedNativeReason, dllEntries, entryNames, findings);
			IReadOnlyList<ArchiveSavePreflightEntry> saves = [];
			if (hasSaveEntries)
			{
				try
				{
					saves = await InspectSaveInputAsync(normalizedPath, temporaryRoot, cancellationToken);
					findings.Add(new PackagePreflightFinding(
						ModHealthSeverity.Info,
						"BG3 save data recognized",
						$"Redux found {saves.Count} save{(saves.Count == 1 ? String.Empty : "s")}. Install through Save Game Manager rather than the normal mod workflow."));
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					DivinityApp.Log($"Save archive preflight failed for '{Path.GetFileName(normalizedPath)}':\n{ex}");
					findings.Add(new PackagePreflightFinding(
						ModHealthSeverity.Error,
						"Save data could not be validated",
						ex.Message));
				}
			}
			var packages = new List<PackagePreflightReport>(pakEntries.Length);

			using var staged = await StagedPakArchive.ReadAsync(archive, temporaryRoot, true, null, cancellationToken);
			foreach (var stagedPath in staged.Primaries)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var entry = staged.Entries[stagedPath];
				try
				{
					staged.RequireMatchingPartFolders(stagedPath);
					var report = await PackagePreflightService.AnalyzeAsync(stagedPath, installedMods, cancellationToken);
					packages.Add(report.WithSource($"{normalizedPath}::{NormalizeEntryPath(entry.Key)}",
						PakFileSet.GetPaths(stagedPath).Sum(path => new FileInfo(path).Length)));
					findings.AddRange(report.Findings.Where(finding => finding.Title == PakImportCompatibility.MultipartFindingTitle));
				}
				catch (OperationCanceledException) { throw; }
				catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
				{
					var finding = new PackagePreflightFinding(ModHealthSeverity.Error, PakImportCompatibility.MultipartFindingTitle, ex.Message);
					packages.Add(new PackagePreflightReport($"{normalizedPath}::{NormalizeEntryPath(entry.Key)}", null, 0, 0, [finding]));
					findings.Add(finding);
				}
			}

			return new ArchivePackagePreflightResult(
				normalizedPath,
				entries.Length,
				new FileInfo(normalizedPath).Length,
				packages,
				findings,
				kind,
				nativeInspection,
				saves,
				entryNames);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex) when (ex is InvalidFormatException
			or InvalidOperationException
			or IOException
			or UnauthorizedAccessException)
		{
			DivinityApp.Log($"Archive package preflight failed for '{Path.GetFileName(normalizedPath)}':\n{ex}");
			return Unreadable(normalizedPath, "Redux could not open or read this archive.");
		}
		finally
		{
			DeleteTemporaryRoot(temporaryRoot);
		}
	}

	public static IReadOnlyList<PackagePreflightFinding> AnalyzeEntryNames(IEnumerable<string> entryNames, bool requirePak = true)
	{
		var entries = (entryNames ?? Enumerable.Empty<string>())
			.Where(name => !String.IsNullOrWhiteSpace(name))
			.Select(NormalizeEntryPath)
			.ToArray();
		var findings = new List<PackagePreflightFinding>();
		var pakEntries = entries
			.Where(name => name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
			.ToArray();

		if (requirePak && pakEntries.Length == 0)
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"No PAK files found",
				"The archive does not contain a Baldur's Gate 3 PAK package."));
		}

		var collision = ArchivePakImport.FindDestinationCollision(pakEntries);
		if (collision != null) findings.Add(collision);

		var unsafeEntries = entries
			.Where(IsUnsafeEntryPath)
			.Take(4)
			.ToArray();
		if (unsafeEntries.Length > 0)
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"Unsafe archive paths",
				$"Archive entries contain absolute or parent-relative paths: {String.Join(", ", unsafeEntries)}"));
		}

		var debris = entries
			.Where(name => DevelopmentFileNames.Contains(Path.GetFileName(name))
				|| DevelopmentExtensions.Contains(Path.GetExtension(name)))
			.Take(4)
			.ToArray();
		if (debris.Length > 0)
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Warning,
				"Development files are included in the archive",
				$"Review whether these files belong in the release: {String.Join(", ", debris)}"));
		}

		if (entries.Any(name => name.EndsWith("modsettings.lsx", StringComparison.OrdinalIgnoreCase)))
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Warning,
				"Load-order settings are included",
				"A mod release normally should not contain a user's modsettings.lsx file."));
		}

		return findings;
	}

	private static void AddNativeFindings(
		ReduxGameDirectoryArchiveInspection inspection,
		string unreviewedReason,
		IReadOnlyList<string> dllEntries,
		IReadOnlyList<string> entryNames,
		ICollection<PackagePreflightFinding> findings)
	{
		if (inspection == null && dllEntries.Count == 0) return;
		if (inspection != null)
		{
			var definition = inspection.Definition;
			var destinations = inspection.ManagedFiles.Select(file => file.DestinationPath)
				.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToArray();
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Info,
				"Reviewed game-directory package",
				$"Redux recognizes {definition.Name} using the reviewed {inspection.LayoutName} layout."));
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Info,
				"Expected game-directory destinations",
				String.Join(", ", destinations)));
			if (definition.RequiresLoader)
			{
				findings.Add(new PackagePreflightFinding(
					ModHealthSeverity.Info,
					"Loader dependency",
					"This native plugin requires Native Mod Loader. Installation will verify the loader before changing game files."));
			}
			if (inspection.PackageEntries.Count > 0)
			{
				findings.Add(new PackagePreflightFinding(
					ModHealthSeverity.Info,
					"Hybrid native and PAK package",
					$"Game-directory files use the guarded installer; {String.Join(", ", inspection.PackageEntries.Select(Path.GetFileName))} uses Redux's normal mod workflow."));
			}
		}
		else
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Warning,
				"Unreviewed native layout",
				String.IsNullOrWhiteSpace(unreviewedReason)
					? "DLL files were detected, but Redux cannot confidently identify their package or install destinations."
					: unreviewedReason));
		}

		findings.Add(new PackagePreflightFinding(
			ModHealthSeverity.Info,
			"Native libraries detected",
			String.Join(", ", dllEntries.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Take(8))));
		var supportFiles = entryNames.Where(name => Path.GetExtension(name) is var extension
			&& extension is not null
			&& (extension.Equals(".toml", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".ini", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".config", StringComparison.OrdinalIgnoreCase)))
			.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray();
		if (supportFiles.Length > 0)
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Info,
				"Configuration and support files",
				String.Join(", ", supportFiles)));
		}
	}

	private static async Task<IReadOnlyList<ArchiveSavePreflightEntry>> InspectSaveInputAsync(
		string sourcePath,
		string temporaryRoot,
		CancellationToken cancellationToken)
	{
		var saveRoot = Path.Combine(temporaryRoot, "Saves");
		await Task.Run(() => Bg3SaveGameService.Import(sourcePath, saveRoot, replaceExisting: false), cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return Bg3SaveGameService.Discover(saveRoot).Select(save => new ArchiveSavePreflightEntry(
			save.DisplayName,
			save.CampaignName,
			save.FolderName,
			save.ModifiedUtc,
			save.SizeBytes,
			save.Difficulty)).ToArray();
	}

	private static ArchivePackagePreflightResult CreateSaveResult(
		string sourcePath,
		int entryCount,
		long sourceSize,
		IReadOnlyList<ArchiveSavePreflightEntry> saves,
		IReadOnlyList<string> entryNames) => new(
			sourcePath,
			entryCount,
			sourceSize,
			Array.Empty<PackagePreflightReport>(),
			new[]
			{
				new PackagePreflightFinding(
					ModHealthSeverity.Info,
					"BG3 save data recognized",
					$"Redux found {saves.Count} save{(saves.Count == 1 ? String.Empty : "s")}. Install through Save Game Manager rather than the normal mod workflow.")
			},
			ArchivePackagePreflightKind.SaveGame,
			null,
			saves,
			entryNames);

	private static ArchivePackagePreflightResult Unreadable(string archivePath, string message) => new(
		archivePath,
		0,
		0,
		Array.Empty<PackagePreflightReport>(),
		new[]
		{
			new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"Archive could not be inspected",
				message)
		});

	private static string CreateTemporaryRoot()
	{
		var root = Path.Combine(
			Path.GetTempPath(),
			"BG3ModManagerRedux",
			"PackagePreflight",
			Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		return root;
	}

	private static void DeleteTemporaryRoot(string path)
	{
		if (String.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
		try
		{
			var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BG3ModManagerRedux", "PackagePreflight"))
				.TrimEnd(Path.DirectorySeparatorChar)
				+ Path.DirectorySeparatorChar;
			var target = Path.GetFullPath(path);
			if (!target.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) return;
			Directory.Delete(target, true);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			DivinityApp.Log($"Could not remove a package preflight temporary directory: {ex.Message}");
		}
	}

	private static bool IsUnsafeEntryPath(string path)
	{
		if (String.IsNullOrWhiteSpace(path)) return false;
		if (path.StartsWith('/') || path.StartsWith('\\')) return true;
		if (path.Length >= 2 && Char.IsLetter(path[0]) && path[1] == ':') return true;
		return path.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == "..");
	}

	private static string NormalizeEntryPath(string path) => (path ?? String.Empty).Replace('\\', '/');
}
