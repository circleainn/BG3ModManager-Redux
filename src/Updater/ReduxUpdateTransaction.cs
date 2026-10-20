using System.Text.Json;

namespace ReduxUpdater;

public static class ReduxUpdateTransaction
{
	public const int RequestSchemaVersion = 1;
	private const string InventoryFileName = "Redux-Release-Files.json";
	private const int InventorySchemaVersion = 1;
	private const int MaximumManagedFiles = 4096;
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
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};
	private static readonly HashSet<string> RequestProperties = new(StringComparer.Ordinal)
	{
		"schemaVersion", "parentProcessId", "targetDirectory", "stagedDirectory", "backupDirectory",
		"resultPath", "displayVersion", "relaunchRelativePath"
	};

	public static ReduxUpdateRequest ReadRequest(string requestPath)
	{
		if (String.IsNullOrWhiteSpace(requestPath))
			throw new ArgumentException("An update request path is required.", nameof(requestPath));
		var json = File.ReadAllText(Path.GetFullPath(requestPath));
		using var document = JsonDocument.Parse(json, new JsonDocumentOptions
		{
			AllowTrailingCommas = false,
			CommentHandling = JsonCommentHandling.Disallow,
			MaxDepth = 8
		});
		RejectDuplicateProperties(document.RootElement);
		if (document.RootElement.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException("The update request must be an object.");
		foreach (var property in document.RootElement.EnumerateObject())
		{
			if (!RequestProperties.Contains(property.Name))
				throw new InvalidDataException($"The update request contains unsupported property '{property.Name}'.");
		}
		var request = JsonSerializer.Deserialize<ReduxUpdateRequest>(json, SerializerOptions)
			?? throw new InvalidDataException("The update request is empty.");
		ValidateRequest(request);
		return request;
	}

	public static ReduxUpdateResult Apply(ReduxUpdateRequest request)
	{
		ValidateRequest(request);
		var targetRoot = NormalizeRoot(request.TargetDirectory);
		var stagedRoot = NormalizeRoot(request.StagedDirectory);
		var backupRoot = NormalizeRoot(request.BackupDirectory);
		if (!Directory.Exists(targetRoot) || !File.Exists(Path.Combine(targetRoot, request.RelaunchRelativePath)))
			throw new InvalidDataException("The Redux installation directory is no longer available.");
		if (!Directory.Exists(stagedRoot))
			throw new InvalidDataException("The staged Redux update is no longer available.");
		if (RootsOverlap(targetRoot, stagedRoot) || RootsOverlap(targetRoot, backupRoot) || RootsOverlap(stagedRoot, backupRoot))
			throw new InvalidDataException("Update staging, backup, and installation directories must be separate.");
		if (!String.Equals(Path.GetDirectoryName(stagedRoot), Path.GetDirectoryName(backupRoot), StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("Update staging and backup directories must share one transaction directory.");

		var next = ReadInventory(stagedRoot, requireExactContents: true);
		var currentInventoryPath = Path.Combine(targetRoot, InventoryFileName);
		var current = File.Exists(currentInventoryPath)
			? ReadInventory(targetRoot, requireExactContents: false)
			: new ReduxReleaseInventory();
		var nextFiles = next.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var affectedFiles = current.Files.Concat(next.Files)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		var existed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		Directory.CreateDirectory(backupRoot);
		foreach (var relativePath in affectedFiles)
		{
			var target = GetContainedPath(targetRoot, relativePath);
			if (Directory.Exists(target))
				throw new InvalidDataException($"A directory blocks Redux-owned file '{relativePath}'.");
			if (!File.Exists(target)) continue;
			existed.Add(relativePath);
			var backup = GetContainedPath(backupRoot, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
			RunWithFileRetry(
				() =>
				{
					File.Copy(target, backup, overwrite: false);
					ClearReadOnlyAttribute(backup);
				},
				$"read Redux file '{relativePath}' for backup");
		}

		try
		{
			foreach (var relativePath in next.Files)
			{
				var source = GetContainedPath(stagedRoot, relativePath);
				var destination = GetContainedPath(targetRoot, relativePath);
				RunWithFileRetry(
					() => CopyAtomically(source, destination),
					$"replace Redux file '{relativePath}'",
					destination);
			}
			foreach (var relativePath in current.Files.Where(path => !nextFiles.Contains(path)))
			{
				var obsolete = GetContainedPath(targetRoot, relativePath);
				if (File.Exists(obsolete))
					RunWithFileRetry(
						() => File.Delete(obsolete),
						$"remove obsolete Redux file '{relativePath}'",
						obsolete);
				DeleteEmptyParents(Path.GetDirectoryName(obsolete), targetRoot);
			}
		}
		catch
		{
			Rollback(targetRoot, backupRoot, affectedFiles, existed);
			throw;
		}

		return new ReduxUpdateResult
		{
			Succeeded = true,
			DisplayVersion = request.DisplayVersion,
			Message = $"Redux was updated to {request.DisplayVersion}.",
			CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
		};
	}

	public static void WriteResult(string resultPath, ReduxUpdateResult result)
	{
		var path = Path.GetFullPath(resultPath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		File.WriteAllText(temporary, JsonSerializer.Serialize(result, SerializerOptions));
		File.Move(temporary, path, overwrite: true);
	}

	private static ReduxReleaseInventory ReadInventory(string root, bool requireExactContents)
	{
		var path = Path.Combine(root, InventoryFileName);
		var json = File.ReadAllText(path);
		using var document = JsonDocument.Parse(json, new JsonDocumentOptions
		{
			AllowTrailingCommas = false,
			CommentHandling = JsonCommentHandling.Disallow,
			MaxDepth = 8
		});
		RejectDuplicateProperties(document.RootElement);
		if (document.RootElement.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException("The Redux release inventory must be an object.");
		var allowed = new HashSet<string>(StringComparer.Ordinal) { "schemaVersion", "files" };
		foreach (var property in document.RootElement.EnumerateObject())
		{
			if (!allowed.Contains(property.Name))
				throw new InvalidDataException($"The Redux release inventory contains unsupported property '{property.Name}'.");
		}
		var inventory = JsonSerializer.Deserialize<ReduxReleaseInventory>(json, SerializerOptions)
			?? throw new InvalidDataException("The Redux release inventory is empty.");
		if (inventory.SchemaVersion != InventorySchemaVersion
			|| inventory.Files.Count == 0
			|| inventory.Files.Count > MaximumManagedFiles)
			throw new InvalidDataException("The Redux release inventory is invalid.");

		var files = new List<string>(inventory.Files.Count);
		var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var value in inventory.Files)
		{
			var relativePath = NormalizeRelativeFilePath(value);
			if (!unique.Add(relativePath))
				throw new InvalidDataException($"The Redux release inventory contains duplicate path '{relativePath}'.");
			files.Add(relativePath);
		}
		if (!unique.Contains(InventoryFileName)
			|| !unique.Contains("Redux.exe")
			|| !unique.Contains("Updater/ReduxUpdater.exe"))
			throw new InvalidDataException("The Redux release inventory is missing required application files.");

		if (requireExactContents)
		{
			var actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
				.Select(file => NormalizeRelativeFilePath(Path.GetRelativePath(root, file)))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (!actual.SetEquals(unique))
				throw new InvalidDataException("The staged update does not match its release inventory.");
		}
		return new ReduxReleaseInventory { SchemaVersion = InventorySchemaVersion, Files = files };
	}

	private static void ValidateRequest(ReduxUpdateRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (request.SchemaVersion != RequestSchemaVersion)
			throw new InvalidDataException("The update request schema is not supported.");
		if (request.ParentProcessId <= 0)
			throw new InvalidDataException("The update request does not identify the running Redux process.");
		_ = NormalizeRoot(request.TargetDirectory);
		_ = NormalizeRoot(request.StagedDirectory);
		_ = NormalizeRoot(request.BackupDirectory);
		_ = Path.GetFullPath(request.ResultPath);
		if (String.IsNullOrWhiteSpace(request.DisplayVersion))
			throw new InvalidDataException("The update request does not identify its release.");
		if (!String.Equals(NormalizeRelativeFilePath(request.RelaunchRelativePath), "Redux.exe", StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("The update request has an unsupported relaunch target.");
	}

	private static string NormalizeRelativeFilePath(string? value)
	{
		if (String.IsNullOrWhiteSpace(value))
			throw new InvalidDataException("Managed update paths cannot be empty.");
		var normalized = value.Trim().Replace('\\', '/');
		if (normalized.StartsWith('/') || normalized.EndsWith('/') || Path.IsPathRooted(normalized))
			throw new InvalidDataException($"Managed update path '{value}' is not relative.");
		var parts = normalized.Split('/');
		if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.Contains(':')))
			throw new InvalidDataException($"Managed update path '{value}' is unsafe.");
		if (ProtectedRootDirectories.Contains(parts[0]) || ProtectedFileNames.Contains(parts[^1]))
			throw new InvalidDataException($"Managed update path '{value}' belongs to protected user state.");
		return String.Join('/', parts);
	}

	private static string NormalizeRoot(string? value)
	{
		if (String.IsNullOrWhiteSpace(value))
			throw new InvalidDataException("An update directory is missing.");
		var root = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		if (String.Equals(root, Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("A filesystem root cannot be used as an update directory.");
		return root;
	}

	private static string GetContainedPath(string root, string relativePath)
	{
		var normalizedRoot = NormalizeRoot(root) + Path.DirectorySeparatorChar;
		var path = Path.GetFullPath(Path.Combine(normalizedRoot, NormalizeRelativeFilePath(relativePath).Replace('/', Path.DirectorySeparatorChar)));
		if (!path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException($"Managed update path '{relativePath}' leaves its directory.");
		return path;
	}

	private static bool RootsOverlap(string first, string second)
	{
		var firstRoot = NormalizeRoot(first) + Path.DirectorySeparatorChar;
		var secondRoot = NormalizeRoot(second) + Path.DirectorySeparatorChar;
		return firstRoot.StartsWith(secondRoot, StringComparison.OrdinalIgnoreCase)
			|| secondRoot.StartsWith(firstRoot, StringComparison.OrdinalIgnoreCase);
	}

	private static void CopyAtomically(string source, string destination)
	{
		if (!File.Exists(source)) throw new FileNotFoundException("A staged update file is missing.", source);
		Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
		var temporary = destination + ".redux-update-" + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			File.Copy(source, temporary, overwrite: false);
			File.Move(temporary, destination, overwrite: true);
		}
		finally
		{
			if (File.Exists(temporary)) File.Delete(temporary);
		}
	}

	private static void RunWithFileRetry(
		Action operation,
		string description,
		string? destinationPath = null)
	{
		const int attempts = 31;
		Exception? lastError = null;
		for (var attempt = 0; attempt < attempts; attempt++)
		{
			try
			{
				if (!String.IsNullOrWhiteSpace(destinationPath) && File.Exists(destinationPath))
				{
					ClearReadOnlyAttribute(destinationPath);
				}
				operation();
				return;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				lastError = ex;
				if (attempt + 1 < attempts) Thread.Sleep(200);
			}
		}

		throw new IOException($"Redux could not {description} after waiting for Windows to release it. {lastError?.Message}", lastError);
	}

	private static void ClearReadOnlyAttribute(string path)
	{
		var attributes = File.GetAttributes(path);
		if (attributes.HasFlag(FileAttributes.ReadOnly))
			File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
	}

	private static void Rollback(
		string targetRoot,
		string backupRoot,
		IEnumerable<string> affectedFiles,
		ISet<string> existed)
	{
		foreach (var relativePath in affectedFiles.Reverse())
		{
			try
			{
				var target = GetContainedPath(targetRoot, relativePath);
				if (existed.Contains(relativePath))
				{
					var backup = GetContainedPath(backupRoot, relativePath);
					CopyAtomically(backup, target);
				}
				else if (File.Exists(target))
				{
					File.Delete(target);
					DeleteEmptyParents(Path.GetDirectoryName(target), targetRoot);
				}
			}
			catch
			{
				// Continue restoring the remaining files; the result log retains the original failure.
			}
		}
	}

	private static void DeleteEmptyParents(string? directory, string root)
	{
		var normalizedRoot = NormalizeRoot(root);
		while (!String.IsNullOrWhiteSpace(directory)
			&& !String.Equals(directory, normalizedRoot, StringComparison.OrdinalIgnoreCase)
			&& Directory.Exists(directory)
			&& !Directory.EnumerateFileSystemEntries(directory).Any())
		{
			Directory.Delete(directory);
			directory = Path.GetDirectoryName(directory);
		}
	}

	private static void RejectDuplicateProperties(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			var names = new HashSet<string>(StringComparer.Ordinal);
			foreach (var property in element.EnumerateObject())
			{
				if (!names.Add(property.Name))
					throw new InvalidDataException($"JSON contains duplicate property '{property.Name}'.");
				RejectDuplicateProperties(property.Value);
			}
		}
		else if (element.ValueKind == JsonValueKind.Array)
		{
			foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child);
		}
	}
}
