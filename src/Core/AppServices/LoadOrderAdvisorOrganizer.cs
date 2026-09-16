using DivinityModManager.Models;
using DivinityModManager.Models.Health;

namespace DivinityModManager.AppServices;

public enum LoadOrderAdvisorSeparatorPolicy
{
	PreserveMySeparators,
	CreateSuggestedSeparators,
	RemoveSeparators
}

public sealed record LoadOrderAdvisorMove(
	string Uuid,
	string Name,
	int PreviousPosition,
	int NextPosition,
	string Reason,
	string IgnoreKey);

public sealed record LoadOrderAdvisorUnresolvedRelationship(
	string BeforeName,
	string AfterName,
	string Reason,
	string IgnoreKey);

public enum LoadOrderAdvisorSeparatorChangeKind
{
	Created,
	Repositioned,
	Removed
}

public sealed record LoadOrderAdvisorSeparatorChange(
	ModListVisualDividerData Divider,
	LoadOrderAdvisorSeparatorChangeKind Kind,
	int? PreviousPosition = null);

public sealed class LoadOrderAdvisorPlan
{
	public IReadOnlyList<DivinityModData> OrderedMods { get; init; } = [];
	public IReadOnlyList<ModListVisualDividerData> Dividers { get; init; } = [];
	public IReadOnlyList<LoadOrderAdvisorMove> Moves { get; init; } = [];
	public IReadOnlyList<LoadOrderAdvisorSeparatorChange> SeparatorChanges { get; init; } = [];
	public IReadOnlyList<LoadOrderAdvisorUnresolvedRelationship> UnresolvedRelationships { get; init; } = [];
	public LoadOrderAdvisorSeparatorPolicy SeparatorPolicy { get; init; }
	public bool HasChanges => Moves.Count > 0 || SeparatorChanges.Count > 0;
}

/// <summary>
/// Creates a deterministic, preview-only ordering proposal from the same offline
/// facts used by Redux's Load Order Advisor. Unknown mods keep their relative
/// order, dependency relationships take precedence over group suggestions, and
/// cycles remain in their existing order for the user to review.
/// </summary>
public static class LoadOrderAdvisorOrganizer
{
	private sealed record Edge(
		string BeforeUuid,
		string AfterUuid,
		string Reason,
		ModHealthFindingCode Code)
	{
		public string IgnoreKey => LoadOrderAdvisorFindingIdentity.Create(
			AfterUuid, Code, [BeforeUuid]);
	}
	private sealed record SeparatorGroup(ModListVisualDividerData Divider, List<DivinityModData> Mods);

	public static LoadOrderAdvisorPlan CreatePlan(
		IEnumerable<DivinityModData> activeMods,
		IEnumerable<ModListVisualDividerData> dividers,
		LoadOrderAdvisorSeparatorPolicy separatorPolicy,
		ReduxLoadOrderAdvisorKnowledge knowledge = null,
		IEnumerable<string> ignoredFindingKeys = null)
	{
		var mods = (activeMods ?? []).Where(mod => mod != null && !mod.IsVisualDivider).ToList();
		var activeDividers = (dividers ?? [])
			.Where(divider => divider != null && divider.IsActiveList)
			.OrderBy(divider => divider.Position)
			.Select(CloneDivider)
			.ToList();
		var originalDividers = activeDividers.Select(CloneDivider).ToList();
		knowledge ??= ReduxModDatabaseService.LoadOrderAdvisorKnowledge;
		var installed = mods
			.Where(mod => !String.IsNullOrWhiteSpace(mod.UUID))
			.GroupBy(mod => mod.UUID, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		var ignoredKeys = (ignoredFindingKeys ?? [])
			.Where(key => !String.IsNullOrWhiteSpace(key))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var edges = BuildEdges(mods, installed, knowledge)
			.Where(edge => !ignoredKeys.Contains(edge.IgnoreKey))
			.ToList();
		var unresolved = new List<LoadOrderAdvisorUnresolvedRelationship>();
		List<DivinityModData> ordered;
		List<ModListVisualDividerData> plannedDividers;

		switch (separatorPolicy)
		{
			case LoadOrderAdvisorSeparatorPolicy.PreserveMySeparators:
				var separatorGroups = BuildSeparatorGroups(mods, activeDividers);
				var separatorByUuid = separatorGroups
					.SelectMany((segment, index) => segment.Mods
						.Where(mod => !String.IsNullOrWhiteSpace(mod.UUID))
						.Select(mod => (mod.UUID, index)))
					.GroupBy(item => item.UUID, StringComparer.OrdinalIgnoreCase)
					.ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);
				foreach (var edge in edges.Where(edge =>
					separatorByUuid.TryGetValue(edge.BeforeUuid, out var beforeSeparator)
					&& separatorByUuid.TryGetValue(edge.AfterUuid, out var afterSeparator)
					&& beforeSeparator > afterSeparator))
				{
					unresolved.Add(new LoadOrderAdvisorUnresolvedRelationship(
						installed[edge.BeforeUuid].DisplayName,
						installed[edge.AfterUuid].DisplayName,
						$"{edge.Reason}; the current separator placement prevents this change.",
						edge.IgnoreKey));
				}
				ordered = [];
				plannedDividers = [];
				var visualPosition = 0;
				foreach (var segment in separatorGroups)
				{
					var memberIds = segment.Mods
						.Where(mod => !String.IsNullOrWhiteSpace(mod.UUID))
						.Select(mod => mod.UUID)
						.ToHashSet(StringComparer.OrdinalIgnoreCase);
					var localEdges = edges.Where(edge => memberIds.Contains(edge.BeforeUuid) && memberIds.Contains(edge.AfterUuid));
					var sorted = StableTopologicalSort(segment.Mods, localEdges, knowledge, useGroups: false);
					if (segment.Divider != null)
					{
						segment.Divider.Position = visualPosition++;
						segment.Divider.MemberModUuids = sorted
							.Where(mod => !String.IsNullOrWhiteSpace(mod.UUID))
							.Select(mod => mod.UUID).ToList();
						plannedDividers.Add(segment.Divider);
					}
					ordered.AddRange(sorted);
					visualPosition += sorted.Count;
				}
				break;

			case LoadOrderAdvisorSeparatorPolicy.CreateSuggestedSeparators:
				ordered = StableTopologicalSort(mods, edges, knowledge, useGroups: true);
				plannedDividers = BuildSuggestedDividers(ordered, knowledge);
				break;

			default:
				ordered = StableTopologicalSort(mods, edges, knowledge, useGroups: true);
				plannedDividers = [];
				break;
		}

		var previousPositions = mods
			.Select((mod, index) => (mod, index))
			.Where(item => !String.IsNullOrWhiteSpace(item.mod.UUID))
			.GroupBy(item => item.mod.UUID, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);
		var moves = ordered.Select((mod, index) => (mod, index))
			.Where(item => !String.IsNullOrWhiteSpace(item.mod.UUID)
				&& previousPositions.TryGetValue(item.mod.UUID, out var oldIndex)
				&& oldIndex != item.index)
			.Select(item => new LoadOrderAdvisorMove(
				item.mod.UUID,
				item.mod.DisplayName,
				previousPositions[item.mod.UUID] + 1,
				item.index + 1,
				BuildMoveReason(item.mod, edges, knowledge),
				BuildMoveIgnoreKey(item.mod, edges)))
			.ToList();
		var separatorChanges = BuildSeparatorChanges(originalDividers, plannedDividers, separatorPolicy);

		return new LoadOrderAdvisorPlan
		{
			OrderedMods = ordered,
			Dividers = plannedDividers,
			Moves = moves,
			SeparatorChanges = separatorChanges,
			UnresolvedRelationships = unresolved,
			SeparatorPolicy = separatorPolicy
		};
	}

	private static IReadOnlyList<LoadOrderAdvisorSeparatorChange> BuildSeparatorChanges(
		IReadOnlyList<ModListVisualDividerData> currentDividers,
		IReadOnlyList<ModListVisualDividerData> plannedDividers,
		LoadOrderAdvisorSeparatorPolicy policy)
	{
		if (policy == LoadOrderAdvisorSeparatorPolicy.CreateSuggestedSeparators)
			return plannedDividers.Select(divider => new LoadOrderAdvisorSeparatorChange(
				divider, LoadOrderAdvisorSeparatorChangeKind.Created)).ToArray();

		if (policy == LoadOrderAdvisorSeparatorPolicy.RemoveSeparators)
			return currentDividers.Select(divider => new LoadOrderAdvisorSeparatorChange(
				divider, LoadOrderAdvisorSeparatorChangeKind.Removed, divider.Position)).ToArray();

		var currentById = currentDividers
			.Where(divider => !String.IsNullOrWhiteSpace(divider.Id))
			.GroupBy(divider => divider.Id, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		var changes = new List<LoadOrderAdvisorSeparatorChange>();
		for (var index = 0; index < plannedDividers.Count; index++)
		{
			var planned = plannedDividers[index];
			var current = !String.IsNullOrWhiteSpace(planned.Id)
				&& currentById.TryGetValue(planned.Id, out var identified)
					? identified
					: index < currentDividers.Count ? currentDividers[index] : null;
			if (current != null && current.Position != planned.Position)
				changes.Add(new LoadOrderAdvisorSeparatorChange(
					planned, LoadOrderAdvisorSeparatorChangeKind.Repositioned, current.Position));
		}
		return changes;
	}

	private static List<Edge> BuildEdges(
		IReadOnlyList<DivinityModData> mods,
		IReadOnlyDictionary<string, DivinityModData> installed,
		ReduxLoadOrderAdvisorKnowledge knowledge)
	{
		var edges = new List<Edge>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var mod in mods.Where(mod => !String.IsNullOrWhiteSpace(mod.UUID)))
		{
			var dependencies = mod.Dependencies.Items.Select(dependency =>
				new ReduxLoadOrderDependencyKnowledge { Uuid = dependency.UUID, Name = dependency.Name }).ToList();
			if (knowledge.TryGetEntry(mod.UUID, out var entry))
				dependencies.AddRange(entry.Dependencies ?? []);
			foreach (var dependency in dependencies)
			{
				if (!knowledge.TryResolveInstalledDependency(dependency.Uuid, dependency.Name, installed, out var uuid)
					|| String.Equals(uuid, mod.UUID, StringComparison.OrdinalIgnoreCase)
					|| knowledge.SuppressesDependencyOrdering(uuid)
					|| !seen.Add($"{uuid}>{mod.UUID}")) continue;
				edges.Add(new Edge(
					uuid, mod.UUID, "Dependency placement",
					ModHealthFindingCode.DependencyLoadsLater));
			}
			if (entry == null) continue;
			foreach (var predecessor in entry.LoadAfter ?? [])
			{
				if (!knowledge.TryResolveInstalledDependency(predecessor.Uuid, predecessor.Name, installed, out var uuid)
					|| String.Equals(uuid, mod.UUID, StringComparison.OrdinalIgnoreCase)
					|| !seen.Add($"{uuid}>{mod.UUID}")) continue;
				edges.Add(new Edge(
					uuid,
					mod.UUID,
					String.IsNullOrWhiteSpace(predecessor.Why)
						? "Documented placement"
						: predecessor.Why.Trim(),
					ModHealthFindingCode.RecommendedPredecessorLoadsLater));
			}
		}
		return edges;
	}

	private static List<DivinityModData> StableTopologicalSort(
		IReadOnlyList<DivinityModData> mods,
		IEnumerable<Edge> edges,
		ReduxLoadOrderAdvisorKnowledge knowledge,
		bool useGroups)
	{
		var original = mods.Select((mod, index) => (mod, index)).ToDictionary(item => item.mod, item => item.index);
		var byUuid = mods.Where(mod => !String.IsNullOrWhiteSpace(mod.UUID))
			.GroupBy(mod => mod.UUID, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		var outgoing = mods.ToDictionary(mod => mod, _ => new List<DivinityModData>());
		var indegree = mods.ToDictionary(mod => mod, _ => 0);
		foreach (var edge in edges)
		{
			if (!byUuid.TryGetValue(edge.BeforeUuid, out var before) || !byUuid.TryGetValue(edge.AfterUuid, out var after)
				|| outgoing[before].Contains(after)) continue;
			outgoing[before].Add(after);
			indegree[after]++;
		}
		var remaining = mods.ToHashSet();
		var result = new List<DivinityModData>(mods.Count);
		while (remaining.Count > 0)
		{
			var next = remaining.Where(mod => indegree[mod] == 0)
				.OrderBy(mod => useGroups ? GetGroupPosition(mod, knowledge) : Int32.MaxValue)
				.ThenBy(mod => original[mod])
				.FirstOrDefault();
			if (next == null)
			{
				// Keep cycles stable instead of inventing an unsafe resolution.
				next = remaining.OrderBy(mod => original[mod]).First();
			}
			remaining.Remove(next);
			result.Add(next);
			foreach (var target in outgoing[next]) indegree[target]--;
		}
		return result;
	}

	private static int GetGroupPosition(DivinityModData mod, ReduxLoadOrderAdvisorKnowledge knowledge)
	{
		var group = knowledge.GetGroupName(mod.UUID);
		return knowledge.TryGetGroupPosition(group, out var position) ? position : Int32.MaxValue;
	}

	private static List<SeparatorGroup> BuildSeparatorGroups(
		IReadOnlyList<DivinityModData> mods,
		IReadOnlyList<ModListVisualDividerData> dividers)
	{
		var sequence = mods.Select(mod => (Mod: mod, Divider: (ModListVisualDividerData)null)).ToList();
		foreach (var divider in dividers)
			sequence.Insert(Math.Clamp(divider.Position, 0, sequence.Count), (null, divider));
		var ownerByUuid = dividers
			.SelectMany(divider => (divider.MemberModUuids ?? []).Select(uuid => (uuid, divider)))
			.Where(item => !String.IsNullOrWhiteSpace(item.uuid))
			.GroupBy(item => item.uuid, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First().divider, StringComparer.OrdinalIgnoreCase);
		var segments = new List<SeparatorGroup> { new(null, []) };
		var segmentByDividerId = new Dictionary<string, SeparatorGroup>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in sequence)
		{
			if (item.Divider != null)
			{
				var separator = new SeparatorGroup(item.Divider, []);
				segments.Add(separator);
				segmentByDividerId[item.Divider.Id] = separator;
				continue;
			}
			if (item.Mod == null) continue;
			if (!String.IsNullOrWhiteSpace(item.Mod.UUID)
				&& ownerByUuid.TryGetValue(item.Mod.UUID, out var owner)
				&& segmentByDividerId.TryGetValue(owner.Id, out var ownerSeparator))
			{
				ownerSeparator.Mods.Add(item.Mod);
				continue;
			}
			// Rows outside explicit membership stay outside the separator, including
			// visible rows placed immediately below a sealed collapsed separator.
			if (segments[^1].Divider != null) segments.Add(new SeparatorGroup(null, []));
			segments[^1].Mods.Add(item.Mod);
		}
		return segments;
	}

	private static List<ModListVisualDividerData> BuildSuggestedDividers(
		IReadOnlyList<DivinityModData> ordered,
		ReduxLoadOrderAdvisorKnowledge knowledge)
	{
		var result = new List<ModListVisualDividerData>();
		var visualPosition = 0;
		var offset = 0;
		while (offset < ordered.Count)
		{
			var currentGroup = knowledge.GetGroupName(ordered[offset].UUID) ?? "Needs Review";
			var members = ordered.Skip(offset)
				.TakeWhile(mod => String.Equals(
					knowledge.GetGroupName(mod.UUID) ?? "Needs Review",
					currentGroup,
					StringComparison.OrdinalIgnoreCase))
				.ToList();
			result.Add(new ModListVisualDividerData
			{
				Title = currentGroup,
				Description = currentGroup == "Needs Review"
					? "Mods without reliable offline placement guidance."
					: "Suggested by the Redux Load Order Advisor.",
				IsActiveList = true,
				Position = visualPosition++,
				MemberModUuids = members.Where(mod => !String.IsNullOrWhiteSpace(mod.UUID)).Select(mod => mod.UUID).ToList()
			});
			visualPosition += members.Count;
			offset += members.Count;
		}
		return result;
	}

	private static string BuildMoveReason(
		DivinityModData mod,
		IReadOnlyList<Edge> edges,
		ReduxLoadOrderAdvisorKnowledge knowledge)
	{
		var relationship = edges.FirstOrDefault(edge =>
			String.Equals(edge.BeforeUuid, mod.UUID, StringComparison.OrdinalIgnoreCase)
			|| String.Equals(edge.AfterUuid, mod.UUID, StringComparison.OrdinalIgnoreCase));
		if (relationship != null) return relationship.Reason;
		var group = knowledge.GetGroupName(mod.UUID);
		if (String.IsNullOrWhiteSpace(group)) return "Stable placement around advised mods";
		return knowledge.TryGetEntry(mod.UUID, out var entry) && entry.Evidence != null
			? $"{group} · {entry.Evidence.Label}"
			: $"Suggested {group} separator";
	}

	private static string BuildMoveIgnoreKey(DivinityModData mod, IReadOnlyList<Edge> edges) =>
		edges.FirstOrDefault(edge => String.Equals(
			edge.AfterUuid, mod.UUID, StringComparison.OrdinalIgnoreCase))?.IgnoreKey ?? String.Empty;

	private static ModListVisualDividerData CloneDivider(ModListVisualDividerData divider) => new()
	{
		Id = divider.Id,
		Title = divider.Title,
		Color = divider.Color,
		IconId = divider.IconId,
		Description = divider.Description,
		IsActiveList = divider.IsActiveList,
		Position = divider.Position,
		IsCollapsed = divider.IsCollapsed,
		HideLine = divider.HideLine,
		IsGlobal = divider.IsGlobal,
		MemberModUuids = divider.MemberModUuids?.ToList()
	};
}
