using DivinityModManager.Models;
using DivinityModManager.Models.Metadata;

using LSLib.LS;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DivinityModManager.AppServices;

/// <summary>
/// Discovers and validates the optional redux.mod.json file embedded at a PAK's virtual root.
/// Validation is deliberately strict and read-only: creator claims never alter parsed module data.
/// </summary>
public static class ReduxCreatorManifestService
{
	public const string ManifestFileName = "redux.mod.json";
	public const string NexusSourceService = "nexus";
	public const string ModioSourceService = "modio";
	public const long MaximumManifestBytes = 256 * 1024;
	private const int MaximumJsonDepth = 32;

	private static readonly HashSet<string> RootProperties = new(StringComparer.Ordinal)
	{
		"$schema", "schemaVersion", "manifestType", "mod"
	};
	private static readonly HashSet<string> ModProperties = new(StringComparer.Ordinal)
	{
		"name", "version", "authors", "description", "homepage", "sources", "modules", "dependencies"
	};
	private static readonly HashSet<string> SourceProperties = new(StringComparer.Ordinal)
	{
		"service", "projectId", "fileId"
	};
	private static readonly HashSet<string> ModuleProperties = new(StringComparer.Ordinal)
	{
		"uuid", "name", "folder", "version", "pak"
	};
	private static readonly HashSet<string> DependencyProperties = new(StringComparer.Ordinal)
	{
		"uuid", "name", "minimumVersion", "optional"
	};

	public static bool ContainsManifest(IEnumerable<PackagedFileInfo> packageFiles) =>
		(packageFiles ?? Enumerable.Empty<PackagedFileInfo>())
			.Any(file => IsRootManifest(file?.Name));

	public static async Task<ReduxCreatorManifestData> DiscoverAndValidateAsync(
		IEnumerable<PackagedFileInfo> packageFiles,
		string pakPath,
		IReadOnlyCollection<DivinityModData> parsedModules)
	{
		var manifests = (packageFiles ?? Enumerable.Empty<PackagedFileInfo>())
			.Where(file => IsRootManifest(file?.Name))
			.ToArray();
		if (manifests.Length == 0)
		{
			return ReduxCreatorManifestData.NotPresent;
		}
		if (manifests.Length > 1)
		{
			return Invalid("The PAK contains more than one root-level redux.mod.json file.");
		}

		var manifestFile = manifests[0];
		if (manifestFile.Size() <= 0 || manifestFile.Size() > MaximumManifestBytes)
		{
			return Invalid($"redux.mod.json must be between 1 byte and {MaximumManifestBytes / 1024} KB.");
		}

		try
		{
			using var stream = manifestFile.CreateContentReader();
			using var textReader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
			var json = await textReader.ReadToEndAsync();
			return Validate(json, Path.GetFileName(pakPath), parsedModules);
		}
		catch (Exception ex) when (ex is IOException or Newtonsoft.Json.JsonException or InvalidDataException or ArgumentException)
		{
			return Invalid($"redux.mod.json could not be read safely ({ex.GetType().Name}).");
		}
	}

	public static ReduxCreatorManifestData Validate(
		string json,
		string pakFileName,
		IReadOnlyCollection<DivinityModData> parsedModules)
	{
		try
		{
			if (String.IsNullOrWhiteSpace(json))
				throw new InvalidDataException("redux.mod.json is empty.");

			using var stringReader = new StringReader(json);
			using var jsonReader = new JsonTextReader(stringReader)
			{
				DateParseHandling = DateParseHandling.None,
				FloatParseHandling = FloatParseHandling.Decimal,
				MaxDepth = MaximumJsonDepth
			};
			var token = JToken.ReadFrom(jsonReader, new JsonLoadSettings
			{
				DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
				LineInfoHandling = LineInfoHandling.Ignore
			});
			if (jsonReader.Read())
				throw new InvalidDataException("redux.mod.json contains trailing JSON content.");

			var root = RequireObject(token, "manifest");
			RejectUnknownProperties(root, RootProperties, "manifest");
			OptionalString(root, "$schema", 2048);
			if (RequireInteger(root, "schemaVersion") != 1)
				throw new InvalidDataException("Only creator manifest schemaVersion 1 is supported.");
			if (!String.Equals(RequireString(root, "manifestType", 64), "bg3-redux-mod", StringComparison.Ordinal))
				throw new InvalidDataException("manifestType must be 'bg3-redux-mod'.");

			var modObject = RequireObject(root["mod"], "mod");
			RejectUnknownProperties(modObject, ModProperties, "mod");
			var name = RequireString(modObject, "name", 512);
			var version = OptionalString(modObject, "version", 128);
			var authors = ReadUniqueStrings(modObject, "authors", 1, 64, 512);
			var description = OptionalString(modObject, "description", 4096);
			var homepage = ReadOptionalWebUri(modObject, "homepage");
			var sources = ReadSources(modObject);
			var modules = ReadModules(modObject);
			var dependencies = ReadDependencies(modObject, modules);

			ValidateModuleClaims(modules, pakFileName, parsedModules);

			return new ReduxCreatorManifestData
			{
				State = ReduxCreatorManifestState.Valid,
				Diagnostic = "Embedded creator metadata was validated against this PAK.",
				Name = name,
				Version = version,
				Authors = authors,
				Description = description,
				Homepage = homepage,
				Sources = sources,
				Modules = modules,
				Dependencies = dependencies
			};
		}
		catch (Exception ex) when (ex is Newtonsoft.Json.JsonException or InvalidDataException or ArgumentException)
		{
			return Invalid(ex.Message);
		}
	}

	private static void ValidateModuleClaims(
		IReadOnlyList<ReduxCreatorModuleClaim> claims,
		string pakFileName,
		IReadOnlyCollection<DivinityModData> parsedModules)
	{
		var actualModules = (parsedModules ?? Array.Empty<DivinityModData>())
			.Where(module => module?.HasMetadata == true && Guid.TryParse(module.UUID, out _))
			.GroupBy(module => module.UUID, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		if (actualModules.Count == 0)
			throw new InvalidDataException("The creator manifest cannot be verified because the PAK has no valid module metadata.");

		foreach (var claim in claims)
		{
			if (!actualModules.TryGetValue(claim.Uuid, out var actual))
				throw new InvalidDataException($"Manifest module '{claim.Name}' does not exist in this PAK.");
			if (!EquivalentText(claim.Name, actual.Name))
				throw new InvalidDataException($"Manifest name for module {claim.Uuid} does not match meta.lsx.");
			if (!EquivalentText(claim.Folder, actual.Folder))
				throw new InvalidDataException($"Manifest folder for module {claim.Uuid} does not match meta.lsx.");
			if (!String.IsNullOrWhiteSpace(claim.Version)
				&& !EquivalentVersion(claim.Version, actual.Version?.Version))
				throw new InvalidDataException($"Manifest version for module {claim.Uuid} does not match meta.lsx.");
			if (!String.IsNullOrWhiteSpace(claim.Pak)
				&& !String.Equals(claim.Pak, pakFileName, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException($"Manifest PAK name for module {claim.Uuid} does not match the installed package.");
		}

		var primaryUuid = parsedModules?.FirstOrDefault(module => module?.HasMetadata == true)?.UUID;
		if (!String.IsNullOrWhiteSpace(primaryUuid)
			&& !claims.Any(claim => String.Equals(claim.Uuid, primaryUuid, StringComparison.OrdinalIgnoreCase)))
			throw new InvalidDataException("The creator manifest does not describe the primary module loaded from this PAK.");
	}

	private static IReadOnlyList<ReduxCreatorSourceClaim> ReadSources(JObject modObject)
	{
		if (modObject["sources"] == null) return Array.Empty<ReduxCreatorSourceClaim>();
		var array = RequireArray(modObject["sources"], "sources", 0, 8);
		var results = new List<ReduxCreatorSourceClaim>(array.Count);
		var services = new HashSet<string>(StringComparer.Ordinal);
		foreach (var item in array)
		{
			var source = RequireObject(item, "source");
			RejectUnknownProperties(source, SourceProperties, "source");
			var service = RequireString(source, "service", 16);
			if (service is not (NexusSourceService or ModioSourceService))
				throw new InvalidDataException("A creator source service must be 'nexus' or 'modio'.");
			if (!services.Add(service))
				throw new InvalidDataException($"Creator manifest repeats the '{service}' source service.");
			var projectId = RequirePositiveInteger(source, "projectId");
			long? fileId = source["fileId"] == null ? null : RequirePositiveInteger(source, "fileId");
			results.Add(new ReduxCreatorSourceClaim(service, projectId, fileId));
		}
		return results;
	}

	private static IReadOnlyList<ReduxCreatorModuleClaim> ReadModules(JObject modObject)
	{
		var array = RequireArray(modObject["modules"], "modules", 1, 256);
		var results = new List<ReduxCreatorModuleClaim>(array.Count);
		var uuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in array)
		{
			var module = RequireObject(item, "module");
			RejectUnknownProperties(module, ModuleProperties, "module");
			var uuid = RequireUuid(module, "uuid");
			if (!uuids.Add(uuid))
				throw new InvalidDataException($"Creator manifest repeats module UUID {uuid}.");
			var name = RequireString(module, "name", 512);
			var folder = RequireSafeName(module, "folder", 256, requirePakExtension: false);
			var version = OptionalString(module, "version", 128);
			var pak = module["pak"] == null
				? null
				: RequireSafeName(module, "pak", 260, requirePakExtension: true);
			results.Add(new ReduxCreatorModuleClaim(uuid, name, folder, version, pak));
		}
		return results;
	}

	private static IReadOnlyList<ReduxCreatorDependencyClaim> ReadDependencies(
		JObject modObject,
		IReadOnlyList<ReduxCreatorModuleClaim> modules)
	{
		if (modObject["dependencies"] == null) return Array.Empty<ReduxCreatorDependencyClaim>();
		var array = RequireArray(modObject["dependencies"], "dependencies", 0, 256);
		var moduleUuids = modules.Select(module => module.Uuid).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var dependencyUuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var results = new List<ReduxCreatorDependencyClaim>(array.Count);
		foreach (var item in array)
		{
			var dependency = RequireObject(item, "dependency");
			RejectUnknownProperties(dependency, DependencyProperties, "dependency");
			var uuid = RequireUuid(dependency, "uuid");
			if (moduleUuids.Contains(uuid))
				throw new InvalidDataException($"Creator manifest declares its own module {uuid} as a dependency.");
			if (!dependencyUuids.Add(uuid))
				throw new InvalidDataException($"Creator manifest repeats dependency UUID {uuid}.");
			results.Add(new ReduxCreatorDependencyClaim(
				uuid,
				OptionalString(dependency, "name", 512),
				OptionalString(dependency, "minimumVersion", 128),
				OptionalBoolean(dependency, "optional")));
		}
		return results;
	}

	private static bool IsRootManifest(string path)
	{
		if (String.IsNullOrWhiteSpace(path)) return false;
		var normalized = path.Replace('\\', '/').TrimStart('/');
		return !normalized.Contains('/')
			&& String.Equals(normalized, ManifestFileName, StringComparison.OrdinalIgnoreCase);
	}

	private static JObject RequireObject(JToken token, string label) =>
		token as JObject ?? throw new InvalidDataException($"{label} must be a JSON object.");

	private static JArray RequireArray(JToken token, string label, int minimum, int maximum)
	{
		if (token is not JArray array)
			throw new InvalidDataException($"{label} must be a JSON array.");
		if (array.Count < minimum || array.Count > maximum)
			throw new InvalidDataException($"{label} must contain between {minimum} and {maximum} entries.");
		return array;
	}

	private static void RejectUnknownProperties(JObject value, IReadOnlySet<string> allowed, string label)
	{
		var unknown = value.Properties().FirstOrDefault(property => !allowed.Contains(property.Name));
		if (unknown != null)
			throw new InvalidDataException($"{label} contains unsupported property '{unknown.Name}'.");
	}

	private static string RequireString(JObject value, string property, int maximumLength)
	{
		var result = OptionalString(value, property, maximumLength);
		if (String.IsNullOrWhiteSpace(result))
			throw new InvalidDataException($"{property} must be a non-empty string.");
		return result;
	}

	private static string OptionalString(JObject value, string property, int maximumLength)
	{
		if (value[property] == null) return null;
		if (value[property]?.Type != JTokenType.String)
			throw new InvalidDataException($"{property} must be a string.");
		var result = value[property]!.Value<string>()?.Trim();
		if (result?.Length > maximumLength)
			throw new InvalidDataException($"{property} exceeds {maximumLength} characters.");
		return result;
	}

	private static IReadOnlyList<string> ReadUniqueStrings(
		JObject value,
		string property,
		int minimum,
		int maximum,
		int maximumLength)
	{
		var array = RequireArray(value[property], property, minimum, maximum);
		var unique = new HashSet<string>(StringComparer.Ordinal);
		foreach (var token in array)
		{
			if (token.Type != JTokenType.String)
				throw new InvalidDataException($"{property} entries must be strings.");
			var entry = token.Value<string>()?.Trim();
			if (String.IsNullOrWhiteSpace(entry) || entry.Length > maximumLength)
				throw new InvalidDataException($"{property} contains an invalid entry.");
			if (!unique.Add(entry))
				throw new InvalidDataException($"{property} contains a duplicate entry.");
		}
		return unique.ToArray();
	}

	private static int RequireInteger(JObject value, string property)
	{
		if (value[property]?.Type != JTokenType.Integer)
			throw new InvalidDataException($"{property} must be an integer.");
		return value[property]!.Value<int>();
	}

	private static long RequirePositiveInteger(JObject value, string property)
	{
		if (value[property]?.Type != JTokenType.Integer)
			throw new InvalidDataException($"{property} must be a positive integer.");
		var result = value[property]!.Value<long>();
		if (result <= 0)
			throw new InvalidDataException($"{property} must be a positive integer.");
		return result;
	}

	private static bool OptionalBoolean(JObject value, string property)
	{
		if (value[property] == null) return false;
		if (value[property]?.Type != JTokenType.Boolean)
			throw new InvalidDataException($"{property} must be true or false.");
		return value[property]!.Value<bool>();
	}

	private static string RequireUuid(JObject value, string property)
	{
		var text = RequireString(value, property, 64);
		if (!Guid.TryParse(text, out var uuid))
			throw new InvalidDataException($"{property} must be a valid UUID.");
		return uuid.ToString();
	}

	private static string RequireSafeName(
		JObject value,
		string property,
		int maximumLength,
		bool requirePakExtension)
	{
		var text = RequireString(value, property, maximumLength);
		if (text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || text.Contains('/') || text.Contains('\\'))
			throw new InvalidDataException($"{property} must be a file or folder name, not a path.");
		if (requirePakExtension && !text.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException($"{property} must end in .pak.");
		return text;
	}

	private static string ReadOptionalWebUri(JObject value, string property)
	{
		var text = OptionalString(value, property, 2048);
		if (text == null) return null;
		if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
			|| uri.Scheme is not ("http" or "https")
			|| !String.IsNullOrWhiteSpace(uri.UserInfo))
			throw new InvalidDataException($"{property} must be a public HTTP or HTTPS URL.");
		return uri.AbsoluteUri;
	}

	private static bool EquivalentText(string left, string right) =>
		String.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

	private static bool EquivalentVersion(string left, string right)
	{
		static string Normalize(string value)
		{
			var parts = (value ?? String.Empty).Trim().Split('.');
			var last = parts.Length - 1;
			while (last > 0 && parts[last] == "0") last--;
			return String.Join(".", parts.Take(last + 1));
		}
		return String.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
	}

	private static ReduxCreatorManifestData Invalid(string diagnostic) => new()
	{
		State = ReduxCreatorManifestState.Invalid,
		Diagnostic = String.IsNullOrWhiteSpace(diagnostic)
			? "The embedded creator manifest is invalid."
			: diagnostic
	};
}
