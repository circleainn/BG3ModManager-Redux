using DivinityModManager.Models.Updates;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DivinityModManager.AppServices;

/// <summary>
/// Downloads and stages a verified Redux release without changing the running installation.
/// </summary>
public sealed class ReduxUpdatePackageService
{
	public const string ReleaseInventoryFileName = "Redux-Release-Files.json";
	public const int ReleaseInventorySchemaVersion = 1;
	public const int MaximumArchiveEntries = 4096;
	public const long MaximumExpandedBytes = 2L * 1024 * 1024 * 1024;

	private static readonly HashSet<string> InventoryProperties = new(StringComparer.Ordinal)
	{
		"schemaVersion", "files"
	};
	private static readonly HashSet<string> ProtectedRootDirectories = new(StringComparer.OrdinalIgnoreCase)
	{
		"Data", "Orders", "CurrentOrders", "_Logs", "Logs", "Cache", "_Cache", "Backup", "_Backup",
		"GameDirectoryInstalls", "RestorePoints", "Temp", "Backups"
	};
	private static readonly HashSet<string> ProtectedFileNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"settings.json", "keybindings.json", "ScriptExtenderSettings.json", "LastExported.json",
		"mod-annotations.json", "hosts.yml", ".env", "debug"
	};
	private static readonly string[] RequiredReleaseFiles =
	{
		"Redux.exe",
		"Redux.dll",
		ReleaseInventoryFileName,
		"Updater/ReduxUpdater.exe",
		"Updater/ReduxUpdater.dll",
		"Updater/ReduxUpdater.deps.json",
		"Updater/ReduxUpdater.runtimeconfig.json"
	};
	private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
	private static readonly Regex TransactionDirectoryPattern = new(
		@"^0\.1\.0-alpha\.[1-9][0-9]*(\.[1-9][0-9]*){0,2}-[a-f0-9]{32}$",
		RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

	private readonly HttpClient _client;
	private readonly string _updatesDirectory;
	public static string DefaultUpdatesDirectory => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"BG3ModManager-Redux",
		"Updates");

	public ReduxUpdatePackageService(HttpClient client, string updatesDirectory = null)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_updatesDirectory = Path.GetFullPath(updatesDirectory ?? DefaultUpdatesDirectory);
		CleanupAbandonedTransactions(DateTimeOffset.UtcNow - TimeSpan.FromDays(7));
	}

	internal void CleanupAbandonedTransactions(DateTimeOffset olderThanUtc)
	{
		try
		{
			if (!Directory.Exists(_updatesDirectory)) return;
			foreach (var directory in new DirectoryInfo(_updatesDirectory).EnumerateDirectories())
			{
				if (!TransactionDirectoryPattern.IsMatch(directory.Name)
					|| directory.Attributes.HasFlag(FileAttributes.ReparsePoint)
					|| directory.LastWriteTimeUtc >= olderThanUtc.UtcDateTime)
					continue;
				TryDeleteDirectory(directory.FullName);
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Could not clean abandoned Redux update staging data:\n{ex}");
		}
	}

	public async Task<ReduxPreparedUpdate> DownloadAndStageAsync(
		ReduxUpdateDecision decision,
		IProgress<ReduxUpdateProgress> progress = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(decision);
		if (decision.Availability != ReduxUpdateAvailability.UpdateAvailable)
			throw new InvalidOperationException("Only a newer validated Redux release can be staged.");
		ArgumentNullException.ThrowIfNull(decision.Manifest);
		ArgumentNullException.ThrowIfNull(decision.Artifact);
		ValidateDecision(decision);

		var transactionDirectory = Path.Combine(
			_updatesDirectory,
			SanitizeVersion(decision.Manifest.DisplayVersion) + "-" + Guid.NewGuid().ToString("N"));
		var archivePartialPath = Path.Combine(transactionDirectory, "release.zip.partial");
		var archivePath = Path.Combine(transactionDirectory, "release.zip");
		var stagingPartialDirectory = Path.Combine(transactionDirectory, "staging.partial");
		var stagedDirectory = Path.Combine(transactionDirectory, "staged");

		Directory.CreateDirectory(transactionDirectory);
		try
		{
			progress?.Report(new ReduxUpdateProgress { Status = "Downloading update...", Fraction = 0 });
			await DownloadArtifactAsync(decision.Artifact, archivePartialPath, progress, cancellationToken)
				.ConfigureAwait(false);
			File.Move(archivePartialPath, archivePath);

			progress?.Report(new ReduxUpdateProgress { Status = "Verifying update package...", Fraction = 0.82 });
			await ReduxUpdateManifestService.VerifyArtifactAsync(archivePath, decision.Artifact, cancellationToken)
				.ConfigureAwait(false);

			Directory.CreateDirectory(stagingPartialDirectory);
			progress?.Report(new ReduxUpdateProgress { Status = "Preparing update...", Fraction = 0.86 });
			await ExtractSafelyAsync(archivePath, stagingPartialDirectory, progress, cancellationToken)
				.ConfigureAwait(false);
			var inventory = ReadAndValidateInventory(stagingPartialDirectory, requireExactContents: true);

			Directory.Move(stagingPartialDirectory, stagedDirectory);
			progress?.Report(new ReduxUpdateProgress { Status = "Ready to restart and update.", Fraction = 1 });
			return new ReduxPreparedUpdate
			{
				DisplayVersion = decision.Manifest.DisplayVersion,
				TransactionDirectory = transactionDirectory,
				StagedDirectory = stagedDirectory,
				Inventory = inventory
			};
		}
		catch
		{
			TryDeleteDirectory(transactionDirectory);
			throw;
		}
	}

	public static ReduxReleaseInventory ReadAndValidateInventory(
		string releaseDirectory,
		bool requireExactContents)
	{
		var root = Path.GetFullPath(releaseDirectory ?? throw new ArgumentNullException(nameof(releaseDirectory)));
		var inventoryPath = Path.Combine(root, ReleaseInventoryFileName);
		if (!File.Exists(inventoryPath))
			throw new InvalidDataException("The Redux release inventory is missing.");

		JObject json;
		using (var stream = new FileStream(inventoryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
		using (var text = new StreamReader(stream))
		using (var reader = new JsonTextReader(text)
		{
			MaxDepth = 8,
			DateParseHandling = DateParseHandling.None
		})
		{
			json = JObject.Load(reader, new JsonLoadSettings
			{
				DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
			});
			if (reader.Read()) throw new InvalidDataException("The Redux release inventory contains trailing content.");
		}

		var unknown = json.Properties().FirstOrDefault(property => !InventoryProperties.Contains(property.Name));
		if (unknown != null)
			throw new InvalidDataException($"The Redux release inventory contains unsupported property '{unknown.Name}'.");
		if (json["schemaVersion"]?.Type != JTokenType.Integer
			|| json["schemaVersion"]!.Value<int>() != ReleaseInventorySchemaVersion)
			throw new InvalidDataException("The Redux release inventory schema is not supported.");
		if (json["files"] is not JArray fileTokens || fileTokens.Count == 0 || fileTokens.Count > MaximumArchiveEntries)
			throw new InvalidDataException("The Redux release inventory has an invalid file list.");

		var files = new List<string>(fileTokens.Count);
		var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var token in fileTokens)
		{
			if (token.Type != JTokenType.String)
				throw new InvalidDataException("Every Redux release inventory entry must be a path string.");
			var relativePath = NormalizeRelativeFilePath(token.Value<string>());
			if (!unique.Add(relativePath))
				throw new InvalidDataException($"The Redux release inventory contains duplicate path '{relativePath}'.");
			files.Add(relativePath);
		}

		foreach (var required in RequiredReleaseFiles)
		{
			if (!unique.Contains(required))
				throw new InvalidDataException($"The Redux release is missing required file '{required}'.");
		}

		if (requireExactContents)
		{
			var actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
				.Select(path => NormalizeRelativeFilePath(Path.GetRelativePath(root, path)))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (!actual.SetEquals(unique))
				throw new InvalidDataException("The staged Redux release does not match its file inventory.");
		}

		return new ReduxReleaseInventory
		{
			SchemaVersion = ReleaseInventorySchemaVersion,
			Files = files.AsReadOnly()
		};
	}

	public static string NormalizeRelativeFilePath(string value)
	{
		if (String.IsNullOrWhiteSpace(value))
			throw new InvalidDataException("Redux release paths cannot be empty.");
		var normalized = value.Trim().Replace('\\', '/');
		if (normalized.StartsWith('/') || normalized.EndsWith('/') || Path.IsPathRooted(normalized))
			throw new InvalidDataException($"Redux release path '{value}' is not relative.");
		var parts = normalized.Split('/');
		if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.Contains(':')))
			throw new InvalidDataException($"Redux release path '{value}' is unsafe.");
		if (ProtectedRootDirectories.Contains(parts[0]) || ProtectedFileNames.Contains(parts[^1]))
			throw new InvalidDataException($"Redux release path '{value}' belongs to protected user state.");
		return String.Join('/', parts);
	}

	private async Task DownloadArtifactAsync(
		ReduxUpdateArtifact artifact,
		string destinationPath,
		IProgress<ReduxUpdateProgress> progress,
		CancellationToken cancellationToken)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(DownloadTimeout);
		using var request = new HttpRequestMessage(HttpMethod.Get, artifact.Url);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/zip"));
		using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
			.ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		if (response.Content.Headers.ContentLength is long contentLength && contentLength != artifact.SizeBytes)
			throw new InvalidDataException("The update server returned an unexpected package size.");

		await using var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
		await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
			65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
		var buffer = new byte[65536];
		long received = 0;
		while (true)
		{
			var read = await source.ReadAsync(buffer.AsMemory(), timeout.Token).ConfigureAwait(false);
			if (read == 0) break;
			received += read;
			if (received > artifact.SizeBytes)
				throw new InvalidDataException("The downloaded update exceeded its declared size.");
			await destination.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
			progress?.Report(new ReduxUpdateProgress
			{
				Status = "Downloading update...",
				Fraction = Math.Min(0.8, received / (double)artifact.SizeBytes * 0.8)
			});
		}
		await destination.FlushAsync(timeout.Token).ConfigureAwait(false);
		if (received != artifact.SizeBytes)
			throw new InvalidDataException("The downloaded update is incomplete.");
	}

	private static void ValidateDecision(ReduxUpdateDecision decision)
	{
		var artifact = decision.Artifact;
		var declared = decision.Manifest.Artifacts.SingleOrDefault()
			?? throw new InvalidDataException("The update decision does not contain exactly one release artifact.");
		if (!String.Equals(artifact.Kind, ReduxUpdateArtifactKinds.Portable, StringComparison.Ordinal)
			|| !String.Equals(artifact.Kind, declared.Kind, StringComparison.Ordinal)
			|| !String.Equals(artifact.Url, declared.Url, StringComparison.Ordinal)
			|| artifact.SizeBytes != declared.SizeBytes
			|| !String.Equals(artifact.Sha256, declared.Sha256, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("The selected update artifact does not match its validated manifest.");

		var requiredPrefix = "/circleainn/BG3ModManager-Redux/releases/download/v"
			+ decision.Manifest.DisplayVersion + "/";
		if (!Uri.TryCreate(artifact.Url, UriKind.Absolute, out var uri)
			|| !String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
			|| !String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
			|| !uri.IsDefaultPort
			|| !String.IsNullOrEmpty(uri.UserInfo)
			|| !String.IsNullOrEmpty(uri.Query)
			|| !String.IsNullOrEmpty(uri.Fragment)
			|| !uri.AbsolutePath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase)
			|| uri.AbsolutePath.Length <= requiredPrefix.Length
			|| uri.AbsolutePath.IndexOf('/', requiredPrefix.Length) >= 0
			|| !uri.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("The selected update artifact is not an official Redux release ZIP.");
	}

	private static async Task ExtractSafelyAsync(
		string archivePath,
		string destinationDirectory,
		IProgress<ReduxUpdateProgress> progress,
		CancellationToken cancellationToken)
	{
		using var archive = ZipFile.OpenRead(archivePath);
		if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumArchiveEntries)
			throw new InvalidDataException("The update archive has an invalid number of entries.");

		var files = archive.Entries.Where(entry => !String.IsNullOrEmpty(entry.Name)).ToList();
		var totalLength = files.Sum(entry => entry.Length);
		if (totalLength <= 0 || totalLength > MaximumExpandedBytes)
			throw new InvalidDataException("The expanded update archive is outside the supported size limit.");

		var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		long extracted = 0;
		foreach (var entry in archive.Entries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (IsSymbolicLinkOrReparsePoint(entry))
				throw new InvalidDataException("The update archive contains an unsupported linked entry.");

			if (String.IsNullOrEmpty(entry.Name))
			{
				if (!String.IsNullOrWhiteSpace(entry.FullName))
					_ = NormalizeRelativeDirectoryPath(entry.FullName);
				continue;
			}

			var relativePath = NormalizeRelativeFilePath(entry.FullName);
			if (!destinations.Add(relativePath))
				throw new InvalidDataException($"The update archive contains duplicate path '{relativePath}'.");
			var destination = GetContainedPath(destinationDirectory, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
			await using var input = entry.Open();
			await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
				65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
			await input.CopyToAsync(output, 65536, cancellationToken).ConfigureAwait(false);
			extracted += entry.Length;
			progress?.Report(new ReduxUpdateProgress
			{
				Status = "Preparing update...",
				Fraction = 0.86 + (extracted / (double)totalLength * 0.12)
			});
		}
	}

	private static string NormalizeRelativeDirectoryPath(string value)
	{
		var trimmed = value.Trim().Replace('\\', '/').TrimEnd('/');
		if (trimmed.Length == 0) return String.Empty;
		return NormalizeRelativeFilePath(trimmed);
	}

	private static string GetContainedPath(string root, string relativePath)
	{
		var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		var path = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
		if (!path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException($"Redux release path '{relativePath}' leaves the staging directory.");
		return path;
	}

	private static bool IsSymbolicLinkOrReparsePoint(ZipArchiveEntry entry)
	{
		const int unixFileTypeMask = 0xF000;
		const int unixSymbolicLink = 0xA000;
		var unixMode = (entry.ExternalAttributes >> 16) & unixFileTypeMask;
		return unixMode == unixSymbolicLink
			|| ((FileAttributes)entry.ExternalAttributes).HasFlag(FileAttributes.ReparsePoint);
	}

	private static string SanitizeVersion(string displayVersion)
	{
		if (String.IsNullOrWhiteSpace(displayVersion)
			|| displayVersion.Any(character => !(Char.IsLetterOrDigit(character) || character is '.' or '-')))
			throw new InvalidDataException("The update version cannot be used for staging.");
		return displayVersion;
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
		}
		catch
		{
			// A later cleanup pass can remove abandoned staging data.
		}
	}
}
