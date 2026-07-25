using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace ReduxModDatabaseTool;

internal static class Program
{
	private const string RelativeDatabasePath = "src/GUI/Resources/ReduxModDatabase.json";
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		TypeInfoResolver = new DefaultJsonTypeInfoResolver()
	};

	public static async Task<int> Main(string[] args)
	{
		try
		{
			if (args.Length == 0 || IsHelp(args[0]))
			{
				PrintHelp();
				return 0;
			}

			var command = args[0].ToLowerInvariant();
			var options = CommandOptions.Parse(args.Skip(1));
			return command switch
			{
				"validate" => ValidateCommand(options),
				"fingerprint" => await FingerprintCommandAsync(options),
				"review-report" => ReviewReportCommand(options),
				"accept-report" => AcceptReportCommand(options),
				"add" => await AddCommandAsync(options),
				_ => Fail($"Unknown command '{args[0]}'. Run with --help for usage.")
			};
		}
		catch (OperationCanceledException)
		{
			Console.Error.WriteLine("Operation cancelled.");
			return 2;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Error: {ex.Message}");
			return 1;
		}
	}

	private static int ValidateCommand(CommandOptions options)
	{
		var path = ResolveDatabasePath(options);
		var database = LoadDatabase(path);
		var result = ValidateDatabase(database);
		PrintValidation(path, result);
		return result.Errors.Count == 0 ? 0 : 1;
	}

	private static async Task<int> FingerprintCommandAsync(CommandOptions options)
	{
		var filePath = ResolveArtifactPath(options);
		var fingerprint = await FingerprintAsync(filePath);
		Console.WriteLine(JsonSerializer.Serialize(fingerprint, JsonOptions));
		return 0;
	}

	private static int ReviewReportCommand(CommandOptions options)
	{
		var reportPath = ResolveArtifactPath(options);
		var databasePath = ResolveDatabasePath(options);
		var report = LoadJsonObject(reportPath, "Contribution report");
		var database = LoadDatabase(databasePath);
		var validation = ValidateContributionReport(report);
		if (validation.Errors.Count > 0)
		{
			PrintReportValidation(reportPath, validation);
			return 1;
		}
		var databaseValidation = ValidateDatabase(database);
		if (databaseValidation.Errors.Count > 0)
		{
			PrintValidation(databasePath, databaseValidation);
			return Fail("The database contains validation errors; the report was not reviewed.");
		}

		var review = CreateContributionReview(report, database);
		var counts = review["counts"] as JsonObject ?? new JsonObject();
		Console.WriteLine($"Report: {reportPath}");
		Console.WriteLine($"Database: {databasePath}");
		Console.WriteLine(
			$"Reviewed {GetInt(counts, "total")} mod record(s): "
			+ $"{GetInt(counts, "candidateNewProject")} new project candidate(s), "
			+ $"{GetInt(counts, "candidateKnownProject")} known project candidate(s), "
			+ $"{GetInt(counts, "alreadyKnown")} already known, "
			+ $"{GetInt(counts, "conflict")} conflict(s), "
			+ $"{GetInt(counts, "nonNexus")} non-Nexus, "
			+ $"{GetInt(counts, "unavailable")} unavailable.");

		if (options.Optional("output") is { Length: > 0 } output)
		{
			var outputPath = Path.GetFullPath(output);
			WriteJsonAtomically(outputPath, review);
			Console.WriteLine($"Wrote review to {outputPath}");
		}
		else
		{
			foreach (var item in RequiredArray(review, "items").OfType<JsonObject>())
				Console.WriteLine($"  [{GetString(item, "status")}] {GetString(item, "displayName")} — {GetString(item, "reason")}");
		}

		foreach (var warning in validation.Warnings) Console.WriteLine($"Warning: {warning}");
		return GetInt(counts, "conflict") == 0 ? 0 : 1;
	}

	private static int AcceptReportCommand(CommandOptions options)
	{
		var reportPath = ResolveArtifactPath(options);
		var databasePath = ResolveDatabasePath(options);
		var modId = options.RequiredLong("mod-id");
		var report = LoadJsonObject(reportPath, "Contribution report");
		var database = LoadDatabase(databasePath);
		var reportValidation = ValidateContributionReport(report);
		if (reportValidation.Errors.Count > 0)
		{
			PrintReportValidation(reportPath, reportValidation);
			return 1;
		}

		var before = ValidateDatabase(database);
		if (before.Errors.Count > 0)
		{
			PrintValidation(databasePath, before);
			return Fail("The database already contains validation errors; the report was not accepted.");
		}

		var records = RequiredArray(report, "mods").OfType<JsonObject>()
			.Where(mod => GetNestedLong(mod, "nexus", "modId") == modId)
			.Where(mod => String.Equals(GetString(mod, "fingerprintStatus"), "exact", StringComparison.Ordinal))
			.Where(mod => GetLong(mod, "pakSize") > 0 && IsPakHash(GetString(mod, "pakHash") ?? String.Empty))
			.GroupBy(mod => $"{GetLong(mod, "pakSize")}:{GetString(mod, "pakHash")}", StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToList();
		if (records.Count == 0)
			return Fail($"The report has no exact PAK fingerprints for Nexus project {modId}.");

		var incomplete = records.Where(mod => GetNestedLong(mod, "nexus", "fileId") <= 0).ToList();
		if (incomplete.Count > 0)
			return Fail($"{incomplete.Count} selected record(s) have no Nexus file ID. Review and add those artifacts manually.");

		var first = records[0];
		var projectName = GetNestedString(first, "nexus", "name")
			?? GetString(first, "displayName")
			?? GetString(first, "name")
			?? throw new InvalidDataException("The selected report records have no project name.");
		var author = GetNestedString(first, "nexus", "author")
			?? GetString(first, "author")
			?? throw new InvalidDataException("The selected report records have no author.");
		var pictureUrl = GetNestedString(first, "nexus", "pictureUrl") ?? String.Empty;
		var projects = RequiredArray(database, "projects");
		var project = projects.OfType<JsonObject>().SingleOrDefault(item => GetLong(item, "modId") == modId);
		var changes = new List<string>();
		if (project is null)
		{
			project = new JsonObject
			{
				["modId"] = modId,
				["name"] = projectName,
				["authors"] = ToArray(new[] { author }),
				["aliases"] = ToArray(records.SelectMany(mod => new[]
				{
					GetNestedString(mod, "nexus", "name"),
					GetString(mod, "displayName"),
					GetString(mod, "name")
				}).Where(value => !String.IsNullOrWhiteSpace(value))!),
				["categories"] = new JsonArray(),
				["pictureUrl"] = pictureUrl,
				["fileIds"] = new JsonArray()
			};
			projects.Add(project);
			changes.Add($"created Nexus project {modId}");
		}
		else
		{
			MergeStrings(project, "authors", records.Select(mod =>
				GetNestedString(mod, "nexus", "author") ?? GetString(mod, "author") ?? String.Empty));
			MergeStrings(project, "aliases", records.SelectMany(mod => new[]
			{
				GetNestedString(mod, "nexus", "name"),
				GetString(mod, "displayName"),
				GetString(mod, "name")
			}).Where(value => !String.IsNullOrWhiteSpace(value))!);
			if (String.IsNullOrWhiteSpace(GetString(project, "pictureUrl")) && pictureUrl.Length > 0)
				project["pictureUrl"] = pictureUrl;
			changes.Add($"updated Nexus project {modId}");
		}

		var fingerprints = RequiredArray(database, "exactPakFingerprints");
		foreach (var record in records)
		{
			var size = GetLong(record, "pakSize");
			var hash = GetString(record, "pakHash")!;
			var fileId = GetNestedLong(record, "nexus", "fileId");
			EnsureFingerprintIsAvailable(fingerprints, "hash", hash, size, modId);
			MergeLong(project, "fileIds", fileId);
			if (ContainsFingerprint(fingerprints, "hash", hash, size, modId))
			{
				changes.Add($"fingerprint {hash} ({size} bytes) already exists; left unchanged");
				continue;
			}

			fingerprints.Add(new JsonObject
			{
				["hash"] = hash,
				["size"] = size,
				["modId"] = modId,
				["fileId"] = fileId,
				["name"] = GetNestedString(record, "nexus", "name") ?? GetString(record, "displayName") ?? projectName,
				["author"] = GetNestedString(record, "nexus", "author") ?? GetString(record, "author") ?? author,
				["version"] = GetNestedString(record, "nexus", "version") ?? GetString(record, "version") ?? String.Empty,
				["pictureUrl"] = GetNestedString(record, "nexus", "pictureUrl") ?? GetString(project, "pictureUrl") ?? String.Empty
			});
			changes.Add($"added exact PAK fingerprint {hash} ({size} bytes)");
		}

		UpdateCounts(database);
		var after = ValidateDatabase(database);
		Console.WriteLine($"Report: {reportPath}");
		Console.WriteLine($"Database: {databasePath}");
		foreach (var change in changes) Console.WriteLine($"  + {change}");
		PrintValidation(databasePath, after);
		if (after.Errors.Count > 0) return Fail("Accepted records failed validation; the database was not changed.");
		if (!options.Flag("write"))
		{
			Console.WriteLine();
			Console.WriteLine("Preview only. Re-run with --write after reviewing the values above.");
			return 0;
		}

		WriteDatabaseAtomically(databasePath, database);
		Console.WriteLine($"Updated {databasePath}");
		return 0;
	}

	private static async Task<int> AddCommandAsync(CommandOptions options)
	{
		var databasePath = ResolveDatabasePath(options);
		var artifactPath = ResolveArtifactPath(options);
		var modId = options.RequiredLong("mod-id");
		var fileId = options.RequiredLong("file-id");
		var name = options.Required("name");
		var authors = options.Csv("authors", options.Optional("author"));
		if (authors.Count == 0) throw new ArgumentException("Supply --author or --authors.");

		var database = LoadDatabase(databasePath);
		var before = ValidateDatabase(database);
		if (before.Errors.Count > 0)
		{
			PrintValidation(databasePath, before);
			return Fail("The database already contains validation errors; no proposal was created.");
		}

		var fingerprint = await FingerprintAsync(artifactPath);
		var changes = ApplyCandidate(database, fingerprint, modId, fileId, name, authors, options);
		UpdateCounts(database);

		var after = ValidateDatabase(database);
		Console.WriteLine($"Database: {databasePath}");
		Console.WriteLine($"Artifact: {artifactPath}");
		foreach (var change in changes) Console.WriteLine($"  + {change}");
		PrintValidation(databasePath, after);
		if (after.Errors.Count > 0) return Fail("Candidate failed validation; the database was not changed.");

		if (!options.Flag("write"))
		{
			Console.WriteLine();
			Console.WriteLine("Preview only. Re-run with --write after reviewing the values above.");
			return 0;
		}

		WriteDatabaseAtomically(databasePath, database);
		Console.WriteLine($"Updated {databasePath}");
		return 0;
	}

	private static List<string> ApplyCandidate(
		JsonObject database,
		ArtifactFingerprint fingerprint,
		long modId,
		long fileId,
		string name,
		IReadOnlyCollection<string> authors,
		CommandOptions options)
	{
		var changes = new List<string>();
		var projects = RequiredArray(database, "projects");
		var project = projects.OfType<JsonObject>().SingleOrDefault(item => GetLong(item, "modId") == modId);
		if (project is null)
		{
			project = new JsonObject
			{
				["modId"] = modId,
				["name"] = name,
				["authors"] = ToArray(authors),
				["aliases"] = ToArray(new[] { name }.Concat(options.Csv("aliases"))),
				["categories"] = ToArray(options.Csv("categories", options.Optional("category"))),
				["pictureUrl"] = options.Optional("picture-url") ?? String.Empty,
				["fileIds"] = new JsonArray(fileId)
			};
			projects.Add(project);
			changes.Add($"created Nexus project {modId}");
		}
		else
		{
			MergeStrings(project, "authors", authors);
			MergeStrings(project, "aliases", new[] { name }.Concat(options.Csv("aliases")));
			MergeStrings(project, "categories", options.Csv("categories", options.Optional("category")));
			MergeLong(project, "fileIds", fileId);
			if (String.IsNullOrWhiteSpace(GetString(project, "pictureUrl")) && options.Optional("picture-url") is { Length: > 0 } picture)
				project["pictureUrl"] = picture;
			changes.Add($"updated Nexus project {modId}");
		}

		if (fingerprint.Kind == "pak")
		{
			var entries = RequiredArray(database, "exactPakFingerprints");
			EnsureFingerprintIsAvailable(entries, "hash", fingerprint.Hash, fingerprint.Size, modId);
			if (!ContainsFingerprint(entries, "hash", fingerprint.Hash, fingerprint.Size, modId))
			{
				entries.Add(new JsonObject
				{
					["hash"] = fingerprint.Hash,
					["size"] = fingerprint.Size,
					["modId"] = modId,
					["fileId"] = fileId,
					["name"] = name,
					["author"] = authors.First(),
					["version"] = options.Optional("version") ?? String.Empty,
					["pictureUrl"] = options.Optional("picture-url") ?? GetString(project, "pictureUrl") ?? String.Empty
				});
				changes.Add($"added exact PAK fingerprint {fingerprint.Hash} ({fingerprint.Size} bytes)");
			}
			else changes.Add("exact PAK fingerprint already exists; left unchanged");
		}
		else
		{
			var entries = RequiredArray(database, "exactArchiveFingerprints");
			EnsureFingerprintIsAvailable(entries, "md5", fingerprint.Hash, fingerprint.Size, modId);
			if (!ContainsFingerprint(entries, "md5", fingerprint.Hash, fingerprint.Size, modId))
			{
				entries.Add(new JsonObject
				{
					["md5"] = fingerprint.Hash,
					["size"] = fingerprint.Size,
					["modId"] = modId,
					["fileId"] = fileId,
					["name"] = name,
					["logicalFileName"] = options.Optional("logical-file-name") ?? name,
					["author"] = authors.First(),
					["version"] = options.Optional("version") ?? String.Empty,
					["category"] = options.Optional("category") ?? String.Empty
				});
				changes.Add($"added exact archive fingerprint {fingerprint.Hash} ({fingerprint.Size} bytes)");
			}
			else changes.Add("exact archive fingerprint already exists; left unchanged");
		}

		if (options.Optional("module-uuid") is { Length: > 0 } uuid)
		{
			if (!Guid.TryParse(uuid, out _)) throw new ArgumentException($"Invalid module UUID '{uuid}'.");
			var modules = RequiredArray(database, "moduleIdentities");
			var existing = modules.OfType<JsonObject>()
				.FirstOrDefault(item => String.Equals(GetString(item, "uuid"), uuid, StringComparison.OrdinalIgnoreCase));
			if (existing is not null && GetLong(existing, "modId") != modId)
				throw new InvalidOperationException($"Module UUID {uuid} already points to Nexus project {GetLong(existing, "modId")}.");
			if (existing is null)
			{
				modules.Add(new JsonObject
				{
					["uuid"] = uuid,
					["name"] = options.Optional("module-name") ?? name,
					["folder"] = options.Optional("module-folder") ?? String.Empty,
					["fileNames"] = ToArray(options.Csv("module-files", Path.GetFileName(fingerprint.Path))),
					["authors"] = ToArray(authors),
					["modId"] = modId,
					["matchBasis"] = "reviewed-module-identity"
				});
				changes.Add($"added reviewed module identity {uuid}");
			}
		}

		return changes;
	}

	private static ValidationResult ValidateDatabase(JsonObject database)
	{
		var result = new ValidationResult();
		if (GetInt(database, "schemaVersion") != 1) result.Errors.Add("schemaVersion must be 1.");

		var projects = RequiredArray(database, "projects");
		var projectIds = new HashSet<long>();
		foreach (var project in projects.OfType<JsonObject>())
		{
			var modId = GetLong(project, "modId");
			if (modId <= 0) result.Errors.Add("A project has a missing or invalid modId.");
			else if (!projectIds.Add(modId)) result.Errors.Add($"Duplicate project modId {modId}.");
			if (String.IsNullOrWhiteSpace(GetString(project, "name"))) result.Warnings.Add($"Project {modId} has no name.");
		}

		ValidateFingerprints(database, "exactPakFingerprints", "hash", projectIds, result, value =>
		{
			try { return Convert.FromBase64String(value).Length == sizeof(ulong); }
			catch { return false; }
		});
		ValidateFingerprints(database, "exactArchiveFingerprints", "md5", projectIds, result,
			value => value.Length == 32 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'));

		var moduleProjects = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		foreach (var module in RequiredArray(database, "moduleIdentities").OfType<JsonObject>())
		{
			var uuid = GetString(module, "uuid")?.Trim();
			var modId = GetLong(module, "modId");
			if (!Guid.TryParse(uuid, out _)) result.Errors.Add($"Invalid module UUID '{uuid}'.");
			if (!projectIds.Contains(modId)) result.Errors.Add($"Module UUID {uuid} references missing project {modId}.");
			if (!String.IsNullOrWhiteSpace(uuid) && moduleProjects.TryGetValue(uuid, out var other) && other != modId)
				result.Errors.Add($"Module UUID {uuid} points to both {other} and {modId}.");
			else if (!String.IsNullOrWhiteSpace(uuid)) moduleProjects[uuid] = modId;
		}

		var counts = database["counts"] as JsonObject;
		if (counts is null) result.Errors.Add("counts object is missing.");
		else
		{
			CheckCount(counts, "projects", projects.Count, result);
			CheckCount(counts, "exactPakFingerprints", RequiredArray(database, "exactPakFingerprints").Count, result);
			CheckCount(counts, "exactArchiveFingerprints", RequiredArray(database, "exactArchiveFingerprints").Count, result);
			CheckCount(counts, "reviewedModuleIdentities", RequiredArray(database, "moduleIdentities").Count, result);
		}
		return result;
	}

	private static void ValidateFingerprints(
		JsonObject database,
		string arrayName,
		string hashName,
		HashSet<long> projectIds,
		ValidationResult result,
		Func<string, bool> isValidHash)
	{
		var seen = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in RequiredArray(database, arrayName).OfType<JsonObject>())
		{
			var hash = GetString(item, hashName) ?? String.Empty;
			var size = GetLong(item, "size");
			var modId = GetLong(item, "modId");
			var key = $"{size}:{hash}";
			if (size <= 0) result.Errors.Add($"{arrayName} contains an invalid size.");
			if (!isValidHash(hash)) result.Errors.Add($"{arrayName} contains invalid {hashName} '{hash}'.");
			if (!projectIds.Contains(modId)) result.Errors.Add($"{arrayName} fingerprint {key} references missing project {modId}.");
			if (seen.TryGetValue(key, out var other))
				result.Errors.Add($"{arrayName} fingerprint {key} is duplicated for projects {other} and {modId}.");
			else seen[key] = modId;
		}
	}

	private static void CheckCount(JsonObject counts, string name, int actual, ValidationResult result)
	{
		var recorded = GetInt(counts, name);
		if (recorded != actual) result.Errors.Add($"counts.{name} is {recorded}, but the array contains {actual} records.");
	}

	private static void UpdateCounts(JsonObject database)
	{
		var counts = database["counts"] as JsonObject;
		if (counts is null)
		{
			counts = new JsonObject();
			database["counts"] = counts;
		}
		counts["projects"] = RequiredArray(database, "projects").Count;
		counts["exactPakFingerprints"] = RequiredArray(database, "exactPakFingerprints").Count;
		counts["exactArchiveFingerprints"] = RequiredArray(database, "exactArchiveFingerprints").Count;
		counts["reviewedModuleIdentities"] = RequiredArray(database, "moduleIdentities").Count;
	}

	private static async Task<ArtifactFingerprint> FingerprintAsync(string filePath)
	{
		var file = new FileInfo(filePath);
		if (!file.Exists) throw new FileNotFoundException("Artifact was not found.", filePath);
		var originalLength = file.Length;
		string hash;
		string kind;
		if (file.Extension.Equals(".pak", StringComparison.OrdinalIgnoreCase))
		{
			kind = "pak";
			hash = await XxHash64.ComputeBase64LittleEndianAsync(filePath);
		}
		else
		{
			kind = "archive";
			await using var stream = OpenSequentialRead(filePath);
			hash = Convert.ToHexString(await MD5.HashDataAsync(stream)).ToLowerInvariant();
		}
		file.Refresh();
		if (!file.Exists || file.Length != originalLength) throw new IOException("Artifact changed while it was being fingerprinted.");
		return new ArtifactFingerprint(kind, filePath, originalLength, hash);
	}

	private static FileStream OpenSequentialRead(string path) => new(
		path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
		FileOptions.Asynchronous | FileOptions.SequentialScan);

	private static JsonObject LoadDatabase(string path)
	{
		return LoadJsonObject(path, "Redux mod database");
	}

	private static void WriteDatabaseAtomically(string path, JsonObject database)
	{
		WriteJsonAtomically(path, database);
	}

	private static JsonObject LoadJsonObject(string path, string description)
	{
		if (!File.Exists(path)) throw new FileNotFoundException($"{description} was not found.", path);
		return JsonNode.Parse(File.ReadAllText(path)) as JsonObject
			?? throw new InvalidDataException($"{description} root must be a JSON object.");
	}

	private static void WriteJsonAtomically(string path, JsonObject value)
	{
		var directory = Path.GetDirectoryName(path);
		if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
			throw new DirectoryNotFoundException("The output folder does not exist.");
		var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
		try
		{
			File.WriteAllText(temporaryPath, value.ToJsonString(JsonOptions) + Environment.NewLine);
			using (JsonDocument.Parse(File.ReadAllText(temporaryPath))) { }
			File.Move(temporaryPath, path, true);
		}
		finally
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
	}

	private static ValidationResult ValidateContributionReport(JsonObject report)
	{
		var result = new ValidationResult();
		if (GetInt(report, "schemaVersion") != 1) result.Errors.Add("schemaVersion must be 1.");
		if (!String.Equals(GetString(report, "reportType"), "redux-mod-database-contribution", StringComparison.Ordinal))
			result.Errors.Add("reportType is not a Redux database contribution.");

		if (report["privacy"] is not JsonObject privacy)
			result.Errors.Add("privacy declaration is missing.");
		else
		{
			foreach (var property in new[]
			{
				"containsAbsolutePaths",
				"containsLoadOrder",
				"containsProfileNames",
				"containsSettings",
				"containsCredentials"
			})
			{
				if (privacy[property]?.GetValue<bool>() != false)
					result.Errors.Add($"privacy.{property} must be false.");
			}
		}

		if (report["mods"] is not JsonArray mods)
		{
			result.Errors.Add("mods array is missing.");
			return result;
		}

		var fingerprints = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		for (var index = 0; index < mods.Count; index++)
		{
			if (mods[index] is not JsonObject mod)
			{
				result.Errors.Add($"mods[{index}] is not an object.");
				continue;
			}

			var fileName = GetString(mod, "fileName");
			if (!String.IsNullOrWhiteSpace(fileName)
				&& (!String.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
					|| ContainsPrivatePathData(fileName)))
				result.Errors.Add($"mods[{index}].fileName must be a base file name, not a path.");

			var folder = GetString(mod, "folder");
			if (!String.IsNullOrWhiteSpace(folder)
				&& (ContainsPrivatePathData(folder) || folder.Contains('/') || folder.Contains('\\')))
				result.Errors.Add($"mods[{index}].folder contains a path.");

			var uuid = GetString(mod, "uuid");
			if (!String.IsNullOrWhiteSpace(uuid) && !Guid.TryParse(uuid, out _))
				result.Errors.Add($"mods[{index}].uuid is not a valid module UUID.");

			foreach (var property in new[] { "name", "displayName", "author", "version" })
			{
				var value = GetString(mod, property);
				if (!String.IsNullOrWhiteSpace(value) && ContainsPrivatePathData(value))
					result.Errors.Add($"mods[{index}].{property} contains private path data.");
			}
			foreach (var objectProperty in new[] { "nexus", "modio" })
			{
				if (mod[objectProperty] is not JsonObject metadata) continue;
				foreach (var property in new[] { "name", "author", "version" })
				{
					var value = GetString(metadata, property);
					if (!String.IsNullOrWhiteSpace(value) && ContainsPrivatePathData(value))
						result.Errors.Add($"mods[{index}].{objectProperty}.{property} contains private path data.");
				}
			}
			ValidatePublicWebUrl(mod, "nexus", "pictureUrl", index, result);
			ValidatePublicWebUrl(mod, "modio", "pageUrl", index, result);

			var status = GetString(mod, "fingerprintStatus");
			if (String.Equals(status, "exact", StringComparison.Ordinal))
			{
				var hash = GetString(mod, "pakHash") ?? String.Empty;
				var size = GetLong(mod, "pakSize");
				if (size <= 0 || !IsPakHash(hash))
					result.Errors.Add($"mods[{index}] declares an invalid exact PAK fingerprint.");
				else
				{
					var key = $"{size}:{hash}";
					var nexusId = GetNestedLong(mod, "nexus", "modId");
					if (fingerprints.TryGetValue(key, out var otherNexusId) && otherNexusId != nexusId)
						result.Warnings.Add($"Fingerprint {key} appears with conflicting Nexus IDs in the report.");
					else fingerprints[key] = nexusId;
				}
			}
			else if (String.Equals(status, "unavailable", StringComparison.Ordinal))
			{
				if (GetLong(mod, "pakSize") > 0 || !String.IsNullOrWhiteSpace(GetString(mod, "pakHash")))
					result.Errors.Add($"mods[{index}] has fingerprint data while marked unavailable.");
			}
			else if (!String.Equals(status, "unavailable", StringComparison.Ordinal))
				result.Errors.Add($"mods[{index}].fingerprintStatus must be exact or unavailable.");
		}
		return result;
	}

	private static JsonObject CreateContributionReview(JsonObject report, JsonObject database)
	{
		var projects = RequiredArray(database, "projects").OfType<JsonObject>()
			.ToDictionary(project => GetLong(project, "modId"));
		var knownFingerprints = RequiredArray(database, "exactPakFingerprints").OfType<JsonObject>()
			.ToDictionary(
				item => $"{GetLong(item, "size")}:{GetString(item, "hash")}",
				item => GetLong(item, "modId"),
				StringComparer.OrdinalIgnoreCase);
		var items = new JsonArray();
		var statusCounts = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["candidateNewProject"] = 0,
			["candidateKnownProject"] = 0,
			["alreadyKnown"] = 0,
			["conflict"] = 0,
			["nonNexus"] = 0,
			["unavailable"] = 0
		};

		foreach (var mod in RequiredArray(report, "mods").OfType<JsonObject>())
		{
			var displayName = GetString(mod, "displayName") ?? GetString(mod, "name") ?? GetString(mod, "fileName") ?? "Unnamed mod";
			var nexusId = GetNestedLong(mod, "nexus", "modId");
			var hash = GetString(mod, "pakHash");
			var size = GetLong(mod, "pakSize");
			string status;
			string reason;
			if (!String.Equals(GetString(mod, "fingerprintStatus"), "exact", StringComparison.Ordinal)
				|| size <= 0
				|| !IsPakHash(hash ?? String.Empty))
			{
				status = "unavailable";
				reason = "No exact PAK fingerprint is available.";
			}
			else if (nexusId <= 0)
			{
				status = "nonNexus";
				reason = GetNestedLong(mod, "modio", "modId") > 0
					? "mod.io metadata is present; the current bundled database stores reviewed Nexus identities."
					: "No reviewed Nexus project ID is present.";
			}
			else if (knownFingerprints.TryGetValue($"{size}:{hash}", out var knownModId))
			{
				status = knownModId == nexusId ? "alreadyKnown" : "conflict";
				reason = knownModId == nexusId
					? $"Exact fingerprint already belongs to Nexus project {nexusId}."
					: $"Exact fingerprint belongs to Nexus project {knownModId}, but the report claims {nexusId}.";
			}
			else if (projects.ContainsKey(nexusId))
			{
				status = "candidateKnownProject";
				reason = $"New exact fingerprint candidate for existing Nexus project {nexusId}.";
			}
			else
			{
				status = "candidateNewProject";
				reason = $"New project and exact fingerprint candidate for Nexus project {nexusId}.";
			}
			statusCounts[status]++;

			items.Add(new JsonObject
			{
				["status"] = status,
				["reason"] = reason,
				["displayName"] = displayName,
				["uuid"] = GetString(mod, "uuid"),
				["folder"] = GetString(mod, "folder"),
				["fileName"] = GetString(mod, "fileName"),
				["author"] = GetString(mod, "author"),
				["version"] = GetString(mod, "version"),
				["pakSize"] = size > 0 ? size : null,
				["pakHash"] = hash,
				["nexus"] = mod["nexus"]?.DeepClone(),
				["modio"] = mod["modio"]?.DeepClone()
			});
		}

		var counts = new JsonObject { ["total"] = items.Count };
		foreach (var pair in statusCounts) counts[pair.Key] = pair.Value;
		return new JsonObject
		{
			["schemaVersion"] = 1,
			["reviewType"] = "redux-mod-database-contribution-review",
			["sourceReportCreatedAtUtc"] = report["createdAtUtc"]?.DeepClone(),
			["sourceReduxVersion"] = report["reduxVersion"]?.DeepClone(),
			["counts"] = counts,
			["items"] = items
		};
	}

	private static bool IsPakHash(string value)
	{
		try { return Convert.FromBase64String(value).Length == sizeof(ulong); }
		catch { return false; }
	}

	private static bool LooksLikeAbsolutePath(string value) =>
		Path.IsPathRooted(value)
		|| value.StartsWith(@"\\", StringComparison.Ordinal)
		|| (value.Length >= 3
			&& Char.IsLetter(value[0])
			&& value[1] == ':'
			&& value[2] is '\\' or '/');

	private static bool ContainsPrivatePathData(string value)
	{
		if (LooksLikeAbsolutePath(value)) return true;

		for (var index = 0; index <= value.Length - 3; index++)
		{
			if (Char.IsLetter(value[index])
				&& value[index + 1] == ':'
				&& value[index + 2] is '\\' or '/')
				return true;
		}

		foreach (var marker in new[]
		{
			@"\\",
			@"\Users\",
			"/Users/",
			@"\Documents and Settings\",
			"/home/",
			"%USERPROFILE%",
			"%APPDATA%",
			"%LOCALAPPDATA%",
			"%HOMEDRIVE%",
			"$HOME",
			"${HOME}",
			@"~\",
			"~/"
		})
		{
			if (value.Contains(marker, StringComparison.OrdinalIgnoreCase)) return true;
		}

		var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		return !String.IsNullOrWhiteSpace(userProfile)
			&& value.Contains(userProfile, StringComparison.OrdinalIgnoreCase);
	}

	private static void ValidatePublicWebUrl(
		JsonObject mod,
		string objectProperty,
		string urlProperty,
		int index,
		ValidationResult result)
	{
		var value = GetNestedString(mod, objectProperty, urlProperty);
		if (String.IsNullOrWhiteSpace(value)) return;
		if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
			|| uri.Scheme is not ("http" or "https")
			|| !String.IsNullOrEmpty(uri.UserInfo)
			|| !String.IsNullOrEmpty(uri.Query)
			|| !String.IsNullOrEmpty(uri.Fragment))
			result.Errors.Add($"mods[{index}].{objectProperty}.{urlProperty} is not a public provider URL.");
	}

	private static long GetNestedLong(JsonObject value, string objectProperty, string numberProperty) =>
		value[objectProperty] is JsonObject nested ? GetLong(nested, numberProperty) : 0;

	private static string? GetNestedString(JsonObject value, string objectProperty, string stringProperty) =>
		value[objectProperty] is JsonObject nested ? GetString(nested, stringProperty) : null;

	private static void PrintReportValidation(string path, ValidationResult result)
	{
		foreach (var warning in result.Warnings) Console.WriteLine($"Warning: {warning}");
		foreach (var error in result.Errors) Console.Error.WriteLine($"Error: {error}");
		Console.WriteLine($"Contribution report validation failed ({path}).");
	}

	private static string ResolveDatabasePath(CommandOptions options)
	{
		if (options.Optional("database") is { Length: > 0 } explicitPath) return Path.GetFullPath(explicitPath);
		var directory = new DirectoryInfo(Environment.CurrentDirectory);
		while (directory is not null)
		{
			var candidate = Path.Combine(directory.FullName, RelativeDatabasePath);
			if (File.Exists(candidate)) return candidate;
			directory = directory.Parent;
		}
		throw new FileNotFoundException($"Could not locate {RelativeDatabasePath}. Use --database <path>.");
	}

	private static string ResolveArtifactPath(CommandOptions options)
	{
		var value = options.Optional("file") ?? options.Positionals.FirstOrDefault();
		if (String.IsNullOrWhiteSpace(value)) throw new ArgumentException("Supply an artifact path with --file <path>.");
		return Path.GetFullPath(value);
	}

	private static void EnsureFingerprintIsAvailable(JsonArray entries, string hashName, string hash, long size, long modId)
	{
		var conflict = entries.OfType<JsonObject>().FirstOrDefault(item =>
			GetLong(item, "size") == size
			&& String.Equals(GetString(item, hashName), hash, StringComparison.OrdinalIgnoreCase)
			&& GetLong(item, "modId") != modId);
		if (conflict is not null)
			throw new InvalidOperationException($"Exact fingerprint already points to Nexus project {GetLong(conflict, "modId")}.");
	}

	private static bool ContainsFingerprint(JsonArray entries, string hashName, string hash, long size, long modId) =>
		entries.OfType<JsonObject>().Any(item =>
			GetLong(item, "size") == size
			&& GetLong(item, "modId") == modId
			&& String.Equals(GetString(item, hashName), hash, StringComparison.OrdinalIgnoreCase));

	private static void MergeStrings(JsonObject target, string property, IEnumerable<string> values)
	{
		var array = target[property] as JsonArray ?? new JsonArray();
		target[property] = array;
		var existing = array.Select(item => item?.GetValue<string>() ?? String.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var value in values.Where(value => !String.IsNullOrWhiteSpace(value)).Select(value => value.Trim()))
			if (existing.Add(value)) array.Add(value);
	}

	private static void MergeLong(JsonObject target, string property, long value)
	{
		var array = target[property] as JsonArray ?? new JsonArray();
		target[property] = array;
		if (!array.Any(item => item?.GetValue<long>() == value)) array.Add(value);
	}

	private static JsonArray ToArray(IEnumerable<string> values)
	{
		var array = new JsonArray();
		foreach (var value in values.Where(value => !String.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
			array.Add(value);
		return array;
	}

	private static JsonArray RequiredArray(JsonObject root, string property) =>
		root[property] as JsonArray ?? throw new InvalidDataException($"Required array '{property}' is missing.");

	private static string? GetString(JsonObject value, string property) => value[property]?.GetValue<string>();
	private static long GetLong(JsonObject value, string property) => value[property]?.GetValue<long>() ?? 0;
	private static int GetInt(JsonObject value, string property) => value[property]?.GetValue<int>() ?? 0;

	private static void PrintValidation(string path, ValidationResult result)
	{
		foreach (var warning in result.Warnings) Console.WriteLine($"Warning: {warning}");
		foreach (var error in result.Errors) Console.Error.WriteLine($"Error: {error}");
		Console.WriteLine(result.Errors.Count == 0
			? $"Valid Redux mod database ({path})."
			: $"Validation failed with {result.Errors.Count} error(s).");
	}

	private static bool IsHelp(string value) => value is "-h" or "--help" or "help";

	private static int Fail(string message)
	{
		Console.Error.WriteLine(message);
		return 1;
	}

	private static void PrintHelp()
	{
		Console.WriteLine("""
Redux Mod Database Tool

Commands:
  validate [--database <path>]
      Validate schema, counts, references, hashes, and exact-match collisions.

  fingerprint --file <artifact>
      Print Redux's exact PAK (xxHash64/Base64/little-endian) or archive (MD5/hex) fingerprint.

  review-report --file <report> [--database <path>] [--output <path>]
      Privacy-audit a tester contribution report and classify records against the database.

  accept-report --file <report> --mod-id <id> [--database <path>] [--write]
      Preview exact fingerprints for one reviewed Nexus project. Nothing is written without --write.

  add --file <artifact> --mod-id <id> --file-id <id> --name <name> --author <author> [options]
      Preview a validated project/fingerprint addition. Nothing is written unless --write is supplied.

Add options:
  --authors <a,b>              Multiple project authors.
  --aliases <a,b>              Additional reviewed project names.
  --category <name>            Nexus/Redux category metadata.
  --version <version>          Exact artifact version.
  --picture-url <url>          Nexus image URL.
  --logical-file-name <name>   Archive file display name.
  --module-uuid <uuid>         Optional reviewed module identity.
  --module-name <name>
  --module-folder <folder>
  --module-files <a.pak,b.pak>
  --database <path>            Override the automatically located database.
  --write                      Atomically update the database after validation.

Examples:
  dotnet run --project tools/ReduxModDatabaseTool -- validate
  dotnet run --project tools/ReduxModDatabaseTool -- fingerprint --file "C:\Mods\Example.pak"
  dotnet run --project tools/ReduxModDatabaseTool -- review-report --file "Contribution.bg3redux-report"
  dotnet run --project tools/ReduxModDatabaseTool -- accept-report `
    --file "Contribution.bg3redux-report" --mod-id 123 --write
  dotnet run --project tools/ReduxModDatabaseTool -- add --file "C:\Mods\Example.pak" `
    --mod-id 123 --file-id 456 --name "Example Mod" --author "Author" --version "1.0" --write
""");
	}

	private sealed record ArtifactFingerprint(string Kind, string Path, long Size, string Hash);
	private sealed class ValidationResult
	{
		public List<string> Errors { get; } = new();
		public List<string> Warnings { get; } = new();
	}

	private sealed class CommandOptions
	{
		private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);
		public List<string> Positionals { get; } = new();

		public static CommandOptions Parse(IEnumerable<string> arguments)
		{
			var parsed = new CommandOptions();
			var values = arguments.ToArray();
			for (var index = 0; index < values.Length; index++)
			{
				var value = values[index];
				if (!value.StartsWith("--", StringComparison.Ordinal))
				{
					parsed.Positionals.Add(value);
					continue;
				}
				var key = value[2..];
				if (index + 1 < values.Length && !values[index + 1].StartsWith("--", StringComparison.Ordinal))
					parsed._values[key] = values[++index];
				else parsed._values[key] = null;
			}
			return parsed;
		}

		public string Required(string key) => Optional(key) is { Length: > 0 } value
			? value
			: throw new ArgumentException($"Missing required option --{key}.");

		public long RequiredLong(string key) =>
			long.TryParse(Required(key), out var value) && value > 0
				? value
				: throw new ArgumentException($"--{key} must be a positive integer.");

		public string? Optional(string key) => _values.TryGetValue(key, out var value) ? value : null;
		public bool Flag(string key) => _values.ContainsKey(key);

		public List<string> Csv(string key, string? fallback = null) =>
			(Optional(key) ?? fallback ?? String.Empty)
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Where(value => value.Length > 0)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
	}
}

internal static class XxHash64
{
	private const ulong Prime1 = 11400714785074694791UL;
	private const ulong Prime2 = 14029467366897019727UL;
	private const ulong Prime3 = 1609587929392839161UL;
	private const ulong Prime4 = 9650029242287828579UL;
	private const ulong Prime5 = 2870177450012600261UL;

	public static async Task<string> ComputeBase64LittleEndianAsync(string path)
	{
		await using var stream = new FileStream(
			path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
			FileOptions.Asynchronous | FileOptions.SequentialScan);
		var state = new State();
		var buffer = new byte[1024 * 1024];
		int count;
		while ((count = await stream.ReadAsync(buffer)) > 0) state.Append(buffer.AsSpan(0, count));
		var bytes = new byte[sizeof(ulong)];
		BinaryPrimitives.WriteUInt64LittleEndian(bytes, state.GetHash());
		return Convert.ToBase64String(bytes);
	}

	private sealed class State
	{
		private readonly byte[] _tail = new byte[32];
		private long _length;
		private int _tailLength;
		private ulong _v1 = unchecked(Prime1 + Prime2);
		private ulong _v2 = Prime2;
		private ulong _v3;
		private ulong _v4 = unchecked(0UL - Prime1);

		public void Append(ReadOnlySpan<byte> input)
		{
			_length += input.Length;
			if (_tailLength + input.Length < 32)
			{
				input.CopyTo(_tail.AsSpan(_tailLength));
				_tailLength += input.Length;
				return;
			}

			if (_tailLength > 0)
			{
				var needed = 32 - _tailLength;
				input[..needed].CopyTo(_tail.AsSpan(_tailLength));
				ProcessStripe(_tail);
				input = input[needed..];
				_tailLength = 0;
			}
			while (input.Length >= 32)
			{
				ProcessStripe(input[..32]);
				input = input[32..];
			}
			input.CopyTo(_tail);
			_tailLength = input.Length;
		}

		public ulong GetHash()
		{
			ulong hash;
			if (_length >= 32)
			{
				hash = RotateLeft(_v1, 1) + RotateLeft(_v2, 7) + RotateLeft(_v3, 12) + RotateLeft(_v4, 18);
				hash = MergeRound(hash, _v1);
				hash = MergeRound(hash, _v2);
				hash = MergeRound(hash, _v3);
				hash = MergeRound(hash, _v4);
			}
			else hash = Prime5;

			hash += (ulong)_length;
			var remaining = _tail.AsSpan(0, _tailLength);
			while (remaining.Length >= 8)
			{
				var value = Round(0, BinaryPrimitives.ReadUInt64LittleEndian(remaining));
				hash ^= value;
				hash = RotateLeft(hash, 27) * Prime1 + Prime4;
				remaining = remaining[8..];
			}
			if (remaining.Length >= 4)
			{
				hash ^= BinaryPrimitives.ReadUInt32LittleEndian(remaining) * Prime1;
				hash = RotateLeft(hash, 23) * Prime2 + Prime3;
				remaining = remaining[4..];
			}
			foreach (var value in remaining)
			{
				hash ^= value * Prime5;
				hash = RotateLeft(hash, 11) * Prime1;
			}
			hash ^= hash >> 33;
			hash *= Prime2;
			hash ^= hash >> 29;
			hash *= Prime3;
			hash ^= hash >> 32;
			return hash;
		}

		private void ProcessStripe(ReadOnlySpan<byte> stripe)
		{
			_v1 = Round(_v1, BinaryPrimitives.ReadUInt64LittleEndian(stripe));
			_v2 = Round(_v2, BinaryPrimitives.ReadUInt64LittleEndian(stripe[8..]));
			_v3 = Round(_v3, BinaryPrimitives.ReadUInt64LittleEndian(stripe[16..]));
			_v4 = Round(_v4, BinaryPrimitives.ReadUInt64LittleEndian(stripe[24..]));
		}
	}

	private static ulong Round(ulong accumulator, ulong input)
	{
		accumulator += input * Prime2;
		accumulator = RotateLeft(accumulator, 31);
		return accumulator * Prime1;
	}

	private static ulong MergeRound(ulong accumulator, ulong value)
	{
		accumulator ^= Round(0, value);
		return accumulator * Prime1 + Prime4;
	}

	private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));
}
