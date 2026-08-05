using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;

using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace DivinityModManager.Util;

public sealed class ArchivePackagePreflightResult
{
	public string ArchivePath { get; }
	public int EntryCount { get; }
	public long ArchiveSize { get; }
	public IReadOnlyList<PackagePreflightReport> Packages { get; }
	public IReadOnlyList<PackagePreflightFinding> Findings { get; }

	public ArchivePackagePreflightResult(
		string archivePath,
		int entryCount,
		long archiveSize,
		IEnumerable<PackagePreflightReport> packages,
		IEnumerable<PackagePreflightFinding> findings)
	{
		ArchivePath = archivePath ?? String.Empty;
		EntryCount = Math.Max(0, entryCount);
		ArchiveSize = Math.Max(0, archiveSize);
		Packages = (packages ?? Enumerable.Empty<PackagePreflightReport>()).ToArray();
		Findings = (findings ?? Enumerable.Empty<PackagePreflightFinding>()).ToArray();
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
		if (!IsSupportedArchive(normalizedPath))
			return Unreadable(normalizedPath, "The selected archive format is not supported.");

		var temporaryRoot = CreateTemporaryRoot();
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			await using var fileStream = new FileStream(
				normalizedPath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				4096,
				FileOptions.Asynchronous | FileOptions.SequentialScan);
			using var archive = ArchiveFactory.Open(fileStream, new ReaderOptions());
			var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToArray();
			var findings = AnalyzeEntryNames(entries.Select(entry => entry.Key)).ToList();
			var pakEntries = entries
				.Where(entry => entry.Key.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
				.ToArray();
			var packages = new List<PackagePreflightReport>(pakEntries.Length);

			for (var index = 0; index < pakEntries.Length; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var entry = pakEntries[index];
				var safeName = Path.GetFileName(entry.Key);
				var stagingDirectory = Path.Combine(temporaryRoot, index.ToString("D3"));
				try
				{
					Directory.CreateDirectory(stagingDirectory);
					var stagedPath = Path.Combine(stagingDirectory, safeName);
					await using (var entryStream = entry.OpenEntryStream())
					await using (var output = new FileStream(
						stagedPath,
						FileMode.CreateNew,
						FileAccess.Write,
						FileShare.None,
						4096,
						FileOptions.Asynchronous | FileOptions.SequentialScan))
					{
						await entryStream.CopyToAsync(output, cancellationToken);
					}

					var report = await PackagePreflightService.AnalyzeAsync(
						stagedPath,
						installedMods,
						cancellationToken);
					var sourcePath = $"{normalizedPath}::{NormalizeEntryPath(entry.Key)}";
					packages.Add(report.WithSource(sourcePath, Math.Max(0, entry.Size)));
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					DivinityApp.Log($"Could not stage '{entry.Key}' for package preflight:\n{ex}");
					findings.Add(new PackagePreflightFinding(
						ModHealthSeverity.Error,
						$"{safeName}: Package could not be inspected",
						"Redux could not extract this PAK from the selected archive."));
				}
			}

			return new ArchivePackagePreflightResult(
				normalizedPath,
				entries.Length,
				new FileInfo(normalizedPath).Length,
				packages,
				findings);
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

	public static IReadOnlyList<PackagePreflightFinding> AnalyzeEntryNames(IEnumerable<string> entryNames)
	{
		var entries = (entryNames ?? Enumerable.Empty<string>())
			.Where(name => !String.IsNullOrWhiteSpace(name))
			.Select(NormalizeEntryPath)
			.ToArray();
		var findings = new List<PackagePreflightFinding>();
		var pakEntries = entries
			.Where(name => name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
			.ToArray();

		if (pakEntries.Length == 0)
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"No PAK files found",
				"The archive does not contain a Baldur's Gate 3 PAK package."));
		}

		var duplicateNames = pakEntries
			.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
			.Where(group => group.Count() > 1)
			.Select(group => group.Key)
			.Take(4)
			.ToArray();
		if (duplicateNames.Length > 0)
		{
			findings.Add(new PackagePreflightFinding(
				ModHealthSeverity.Error,
				"Duplicate PAK filenames",
				$"Multiple archive entries would install with the same filename: {String.Join(", ", duplicateNames)}"));
		}

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
