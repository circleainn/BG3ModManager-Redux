using DivinityModManager.Models;

using Newtonsoft.Json;

using System.Text;

namespace DivinityModManager.AppServices;

/// <summary>
/// Immutable offline facts used by Redux's existing Load Order Advisor. The advisor
/// remains read-only: this knowledge can explain a placement but cannot move a mod.
/// </summary>
public sealed class ReduxLoadOrderAdvisorKnowledge
{
	private readonly IReadOnlyDictionary<string, ReduxLoadOrderEntryKnowledge> _entriesByUuid;
	private readonly IReadOnlyDictionary<string, string> _dependencyNameAliases;
	private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _dependencySubstitutes;
	private readonly IReadOnlyDictionary<string, string> _uniqueEntryUuidsByName;
	private readonly IReadOnlyDictionary<string, int> _groupPositions;

	public static ReduxLoadOrderAdvisorKnowledge Empty { get; } = Create(
		Array.Empty<ReduxOrderingGroupKnowledge>(),
		new Dictionary<string, string>(),
		new Dictionary<string, List<string>>(),
		Array.Empty<ReduxLoadOrderEntryKnowledge>());

	public int EntryCount => _entriesByUuid.Count;
	public int GroupCount => _groupPositions.Count;
	public int DependencyAliasCount => _dependencyNameAliases.Count;
	public int DependencySubstituteCount => _dependencySubstitutes.Count;

	private ReduxLoadOrderAdvisorKnowledge(
		IReadOnlyDictionary<string, ReduxLoadOrderEntryKnowledge> entriesByUuid,
		IReadOnlyDictionary<string, string> dependencyNameAliases,
		IReadOnlyDictionary<string, IReadOnlyList<string>> dependencySubstitutes,
		IReadOnlyDictionary<string, string> uniqueEntryUuidsByName,
		IReadOnlyDictionary<string, int> groupPositions)
	{
		_entriesByUuid = entriesByUuid;
		_dependencyNameAliases = dependencyNameAliases;
		_dependencySubstitutes = dependencySubstitutes;
		_uniqueEntryUuidsByName = uniqueEntryUuidsByName;
		_groupPositions = groupPositions;
	}

	public static ReduxLoadOrderAdvisorKnowledge Create(
		IEnumerable<ReduxOrderingGroupKnowledge> groups,
		IReadOnlyDictionary<string, string> dependencyNameAliases,
		IReadOnlyDictionary<string, List<string>> dependencySubstitutes,
		IEnumerable<ReduxLoadOrderEntryKnowledge> entries)
	{
		var entryIndex = (entries ?? Enumerable.Empty<ReduxLoadOrderEntryKnowledge>())
			.Where(entry => !String.IsNullOrWhiteSpace(entry?.Uuid))
			.GroupBy(entry => entry.Uuid.Trim(), StringComparer.OrdinalIgnoreCase)
			.Where(group => group.Count() == 1)
			.ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

		var normalizedAliases = (dependencyNameAliases ?? new Dictionary<string, string>())
			.Where(pair => !String.IsNullOrWhiteSpace(pair.Key) && !String.IsNullOrWhiteSpace(pair.Value))
			.GroupBy(pair => Normalize(pair.Key), StringComparer.Ordinal)
			.Where(group => group.Key.Length >= 4 && group.Select(pair => pair.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
			.ToDictionary(group => group.Key, group => group.First().Value.Trim(), StringComparer.Ordinal);

		var substitutes = (dependencySubstitutes ?? new Dictionary<string, List<string>>())
			.Where(pair => !String.IsNullOrWhiteSpace(pair.Key))
			.ToDictionary(
				pair => pair.Key.Trim(),
				pair => (IReadOnlyList<string>)(pair.Value ?? new List<string>())
					.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
					.Select(uuid => uuid.Trim())
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToArray(),
				StringComparer.OrdinalIgnoreCase);

		var nameCandidates = entryIndex.Values
			.SelectMany(entry => new[] { entry.Name, entry.Folder }.Concat(entry.AlternateNames ?? new List<string>())
				.Select(Normalize)
				.Where(name => name.Length >= 4)
				.Distinct(StringComparer.Ordinal)
				.Select(name => (Name: name, entry.Uuid)));
		var uniqueNames = nameCandidates
			.GroupBy(candidate => candidate.Name, StringComparer.Ordinal)
			.Where(group => group.Select(candidate => candidate.Uuid).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
			.ToDictionary(group => group.Key, group => group.First().Uuid, StringComparer.Ordinal);

		return new ReduxLoadOrderAdvisorKnowledge(
			entryIndex,
			normalizedAliases,
			substitutes,
			uniqueNames,
			BuildGroupPositions(groups));
	}

	public bool TryGetEntry(string uuid, out ReduxLoadOrderEntryKnowledge entry)
	{
		entry = null;
		return !String.IsNullOrWhiteSpace(uuid) && _entriesByUuid.TryGetValue(uuid.Trim(), out entry);
	}

	public bool TryGetGroupPosition(string groupName, out int position)
	{
		position = -1;
		return !String.IsNullOrWhiteSpace(groupName) && _groupPositions.TryGetValue(groupName.Trim(), out position);
	}

	/// <summary>
	/// Resolves a declared requirement to an installed package using exact UUIDs,
	/// curated substitutes, then exact normalized aliases. Approximate matching is
	/// intentionally excluded.
	/// </summary>
	public bool TryResolveInstalledDependency(
		string declaredUuid,
		string declaredName,
		IReadOnlyDictionary<string, DivinityModData> installedByUuid,
		out string installedUuid)
	{
		installedUuid = null;
		if (installedByUuid == null || installedByUuid.Count == 0) return false;

		if (TryResolveUuidOrSubstitute(declaredUuid, installedByUuid, out installedUuid)) return true;

		var normalizedName = Normalize(declaredName);
		if (normalizedName.Length < 4) return false;
		if (_dependencyNameAliases.TryGetValue(normalizedName, out var aliasedUuid)
			&& TryResolveUuidOrSubstitute(aliasedUuid, installedByUuid, out installedUuid))
		{
			return true;
		}
		return _uniqueEntryUuidsByName.TryGetValue(normalizedName, out var entryUuid)
			&& TryResolveUuidOrSubstitute(entryUuid, installedByUuid, out installedUuid);
	}

	public bool SuppressesDependencyOrdering(string dependencyUuid)
	{
		return TryGetEntry(dependencyUuid, out var entry) && entry.LoadsAfterDependents;
	}

	private bool TryResolveUuidOrSubstitute(
		string requiredUuid,
		IReadOnlyDictionary<string, DivinityModData> installedByUuid,
		out string installedUuid)
	{
		installedUuid = null;
		if (String.IsNullOrWhiteSpace(requiredUuid)) return false;
		var key = requiredUuid.Trim();
		if (installedByUuid.ContainsKey(key))
		{
			installedUuid = key;
			return true;
		}
		if (!_dependencySubstitutes.TryGetValue(key, out var substitutes)) return false;
		installedUuid = substitutes.FirstOrDefault(installedByUuid.ContainsKey);
		return installedUuid != null;
	}

	private static IReadOnlyDictionary<string, int> BuildGroupPositions(IEnumerable<ReduxOrderingGroupKnowledge> groups)
	{
		var ordered = (groups ?? Enumerable.Empty<ReduxOrderingGroupKnowledge>())
			.Where(group => !String.IsNullOrWhiteSpace(group?.Name))
			.GroupBy(group => group.Name.Trim(), StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToArray();
		var sourceOrder = ordered.Select((group, index) => (group.Name, index))
			.ToDictionary(item => item.Name, item => item.index, StringComparer.OrdinalIgnoreCase);
		var remainingDependencies = ordered.ToDictionary(
			group => group.Name,
			group => (group.After ?? new List<string>())
				.Where(sourceOrder.ContainsKey)
				.ToHashSet(StringComparer.OrdinalIgnoreCase),
			StringComparer.OrdinalIgnoreCase);
		var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		while (positions.Count < ordered.Length)
		{
			var next = ordered
				.Where(group => !positions.ContainsKey(group.Name))
				.Where(group => remainingDependencies[group.Name].All(positions.ContainsKey))
				.OrderBy(group => sourceOrder[group.Name])
				.FirstOrDefault();
			if (next == null) break;
			positions[next.Name] = positions.Count;
		}
		return positions;
	}

	private static string Normalize(string value)
	{
		if (String.IsNullOrWhiteSpace(value)) return String.Empty;
		var builder = new StringBuilder(value.Length);
		foreach (var character in value.Normalize(NormalizationForm.FormD))
		{
			if (Char.IsLetterOrDigit(character)) builder.Append(Char.ToLowerInvariant(character));
		}
		return builder.ToString();
	}
}

public sealed class ReduxOrderingGroupKnowledge
{
	[JsonProperty("name")] public string Name { get; set; }
	[JsonProperty("after")] public List<string> After { get; set; } = new();
	[JsonProperty("description")] public string Description { get; set; }
}

public sealed class ReduxLoadOrderEntryKnowledge
{
	[JsonProperty("uuid")] public string Uuid { get; set; }
	[JsonProperty("name")] public string Name { get; set; }
	[JsonProperty("alternateNames")] public List<string> AlternateNames { get; set; } = new();
	[JsonProperty("folder")] public string Folder { get; set; }
	[JsonProperty("group")] public string Group { get; set; }
	[JsonProperty("dependencies")] public List<ReduxLoadOrderDependencyKnowledge> Dependencies { get; set; } = new();
	[JsonProperty("loadAfter")] public List<ReduxLoadAfterKnowledge> LoadAfter { get; set; } = new();
	[JsonProperty("loadsAfterDependents")] public bool LoadsAfterDependents { get; set; }
}

public sealed class ReduxLoadOrderDependencyKnowledge
{
	[JsonProperty("uuid")] public string Uuid { get; set; }
	[JsonProperty("name")] public string Name { get; set; }
}

public sealed class ReduxLoadAfterKnowledge
{
	[JsonProperty("uuid")] public string Uuid { get; set; }
	[JsonProperty("name")] public string Name { get; set; }
	[JsonProperty("why")] public string Why { get; set; }
}

internal sealed class ReduxLoadOrderAdvisorDatabase
{
	[JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
	[JsonProperty("orderingGroups")] public List<ReduxOrderingGroupKnowledge> OrderingGroups { get; set; } = new();
	[JsonProperty("dependencyNameAliases")] public Dictionary<string, string> DependencyNameAliases { get; set; } = new(StringComparer.Ordinal);
	[JsonProperty("dependencySubstitutes")] public Dictionary<string, List<string>> DependencySubstitutes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	[JsonProperty("loadOrderEntries")] public List<ReduxLoadOrderEntryKnowledge> LoadOrderEntries { get; set; } = new();
}
