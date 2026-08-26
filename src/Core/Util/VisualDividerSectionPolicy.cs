using DivinityModManager.Models;

namespace DivinityModManager.Util;

/// <summary>
/// Projects separator rows over the authoritative mod collections. Membership is
/// a persisted cache of current visual boundaries and never controls drag payloads
/// or the underlying ActiveMods and InactiveMods order.
/// </summary>
public static class VisualDividerSectionPolicy
{
	public static bool MigrateLegacyMembership(
		IEnumerable<DivinityModData> mods,
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList,
		bool migrationReady = true)
	{
		ArgumentNullException.ThrowIfNull(mods);
		ArgumentNullException.ThrowIfNull(dividers);

		var orderedMods = mods.Where(mod => mod != null && !mod.IsVisualDivider).ToList();
		var paneDividers = GetPaneDividers(dividers, activeList);
		var legacyDividers = paneDividers.Where(divider => divider.MemberModUuids == null).ToList();
		if (legacyDividers.Count == 0) return NormalizeOwnership(paneDividers);
		if (!migrationReady) return NormalizeOwnership(paneDividers);

		var sequence = orderedMods.Select(mod => new LegacyEntry(mod, null)).ToList();
		foreach (var divider in paneDividers)
		{
			sequence.Insert(
				Math.Clamp(divider.Position, 0, sequence.Count),
				new LegacyEntry(null, divider));
		}

		var inferred = legacyDividers.ToDictionary(
			divider => divider,
			_ => new List<string>());
		ModListVisualDividerData currentDivider = null;
		foreach (var entry in sequence)
		{
			if (entry.Divider != null)
			{
				currentDivider = entry.Divider;
				continue;
			}

			var uuid = entry.Mod?.UUID;
			if (currentDivider == null || currentDivider.MemberModUuids != null ||
				String.IsNullOrWhiteSpace(uuid)) continue;
			if (!inferred[currentDivider].Contains(uuid, StringComparer.OrdinalIgnoreCase))
				inferred[currentDivider].Add(uuid);
		}

		foreach (var divider in legacyDividers) divider.MemberModUuids = inferred[divider];
		NormalizeOwnership(paneDividers);
		return true;
	}

	public static bool NormalizeOwnership(
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList) => NormalizeOwnership(GetPaneDividers(dividers, activeList));

	public static IReadOnlyList<DivinityModData> BuildVisualSequence(
		IEnumerable<DivinityModData> mods,
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList,
		Func<ModListVisualDividerData, DivinityModData> createDividerItem)
	{
		ArgumentNullException.ThrowIfNull(mods);
		ArgumentNullException.ThrowIfNull(dividers);
		ArgumentNullException.ThrowIfNull(createDividerItem);

		var result = mods.Where(mod => mod != null && !mod.IsVisualDivider).ToList();
		foreach (var divider in GetPaneDividers(dividers, activeList))
		{
			result.Insert(
				Math.Clamp(divider.Position, 0, result.Count),
				createDividerItem(divider));
		}
		return result;
	}

	/// <summary>
	/// Resolves recreated display markers back to the canonical marker in the full
	/// visual sequence. Separator drags deliberately contain no mod rows.
	/// </summary>
	public static IReadOnlyList<DivinityModData> ResolveMarkerOnlyDragPayload(
		IEnumerable<DivinityModData> visualItems,
		IEnumerable<DivinityModData> draggedItems)
	{
		ArgumentNullException.ThrowIfNull(visualItems);
		ArgumentNullException.ThrowIfNull(draggedItems);

		var sequence = visualItems.Where(item => item != null).ToList();
		var markerIds = draggedItems
			.Where(item => item?.IsVisualDivider == true &&
				!item.IsVisualDividerCollapsed &&
				!String.IsNullOrWhiteSpace(item.VisualDividerId))
			.Select(item => item.VisualDividerId)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		return sequence
			.Where(item => item.IsVisualDivider &&
				!String.IsNullOrWhiteSpace(item.VisualDividerId) &&
				markerIds.Contains(item.VisualDividerId))
			.ToList();
	}

	/// <summary>
	/// Rebuilds section membership from the current marker positions. Each separator
	/// owns the ordinary rows after it up to the next separator; rows before the first
	/// separator remain unsectioned.
	/// </summary>
	public static bool AssignMembersByCurrentBoundaries(
		IEnumerable<DivinityModData> visualItems,
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList)
	{
		ArgumentNullException.ThrowIfNull(visualItems);
		ArgumentNullException.ThrowIfNull(dividers);

		var paneDividers = GetPaneDividers(dividers, activeList);
		var dividerById = paneDividers.ToDictionary(
			divider => divider.Id,
			StringComparer.OrdinalIgnoreCase);
		var membersById = paneDividers.ToDictionary(
			divider => divider.Id,
			_ => new List<string>(),
			StringComparer.OrdinalIgnoreCase);
		var memberIdsById = paneDividers.ToDictionary(
			divider => divider.Id,
			_ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			StringComparer.OrdinalIgnoreCase);
		ModListVisualDividerData currentDivider = null;
		foreach (var item in visualItems.Where(item => item != null))
		{
			if (item.IsVisualDivider)
			{
				dividerById.TryGetValue(item.VisualDividerId ?? String.Empty, out currentDivider);
				continue;
			}

			if (currentDivider == null || String.IsNullOrWhiteSpace(item.UUID)) continue;
			if (memberIdsById[currentDivider.Id].Add(item.UUID))
				membersById[currentDivider.Id].Add(item.UUID);
		}

		var changed = false;
		foreach (var divider in paneDividers)
		{
			var members = membersById[divider.Id];
			if (divider.MemberModUuids != null &&
				divider.MemberModUuids.SequenceEqual(members, StringComparer.OrdinalIgnoreCase)) continue;
			divider.MemberModUuids = members;
			changed = true;
		}
		return changed;
	}

	/// <summary>
	/// Returns only the explicitly owned rows immediately following a separator.
	/// The first divider or unowned row ends the block.
	/// </summary>
	public static IReadOnlyList<DivinityModData> GetContiguousMembers(
		IEnumerable<DivinityModData> visualItems,
		ModListVisualDividerData divider)
	{
		ArgumentNullException.ThrowIfNull(visualItems);
		ArgumentNullException.ThrowIfNull(divider);

		var sequence = visualItems.Where(item => item != null).ToList();
		var markerIndex = sequence.FindIndex(item => item.IsVisualDivider &&
			String.Equals(item.VisualDividerId, divider.Id, StringComparison.OrdinalIgnoreCase));
		if (markerIndex < 0) return Array.Empty<DivinityModData>();

		var memberIds = (divider.MemberModUuids ?? Enumerable.Empty<string>())
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var members = new List<DivinityModData>();
		for (var index = markerIndex + 1; index < sequence.Count; index++)
		{
			var item = sequence[index];
			if (item.IsVisualDivider || String.IsNullOrWhiteSpace(item.UUID) ||
				!memberIds.Contains(item.UUID)) break;
			members.Add(item);
		}
		return members;
	}

	public static int ResolveExpansionInsertionIndex(
		IReadOnlyList<DivinityModData> visibleItems,
		DivinityModData marker,
		ModListVisualDividerData divider)
	{
		ArgumentNullException.ThrowIfNull(visibleItems);
		ArgumentNullException.ThrowIfNull(marker);
		ArgumentNullException.ThrowIfNull(divider);

		var insertIndex = -1;
		for (var index = 0; index < visibleItems.Count; index++)
		{
			if (!ReferenceEquals(visibleItems[index], marker)) continue;
			insertIndex = index;
			break;
		}
		if (insertIndex < 0) return -1;
		insertIndex++;
		var memberIds = (divider.MemberModUuids ?? Enumerable.Empty<string>())
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		while (insertIndex < visibleItems.Count &&
			visibleItems[insertIndex] is { IsVisualDivider: false } insertedMember &&
			!String.IsNullOrWhiteSpace(insertedMember.UUID) && memberIds.Contains(insertedMember.UUID))
			insertIndex++;
		return insertIndex;
	}

	public static IReadOnlySet<string> GetCollapsedMemberIds(
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList)
	{
		ArgumentNullException.ThrowIfNull(dividers);
		return GetPaneDividers(dividers, activeList)
			.Where(divider => divider.IsCollapsed)
			.SelectMany(divider => divider.MemberModUuids ?? Enumerable.Empty<string>())
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Resolves only the explicitly owned, contiguous rows of collapsed separators.
	/// A loose row ends the section even when no later separator follows it, so moving
	/// a collapsed separator cannot adopt unrelated rows at its destination. Limiting
	/// the lookup to the rendered block also prevents stale membership from hiding a
	/// distant row after an interrupted or legacy edit.
	/// </summary>
	public static IReadOnlySet<string> GetCollapsedMemberIds(
		IEnumerable<DivinityModData> visualItems,
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList)
	{
		ArgumentNullException.ThrowIfNull(visualItems);
		ArgumentNullException.ThrowIfNull(dividers);

		var paneDividers = GetPaneDividers(dividers, activeList)
			.ToDictionary(divider => divider.Id, StringComparer.OrdinalIgnoreCase);
		var collapsedMemberIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> currentMemberIds = null;
		foreach (var item in visualItems.Where(item => item != null))
		{
			if (item.IsVisualDivider)
			{
				currentMemberIds = null;
				if (!String.IsNullOrWhiteSpace(item.VisualDividerId) &&
					paneDividers.TryGetValue(item.VisualDividerId, out var divider) &&
					divider.IsCollapsed)
				{
					currentMemberIds = (divider.MemberModUuids ?? Enumerable.Empty<string>())
						.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
						.ToHashSet(StringComparer.OrdinalIgnoreCase);
				}
				continue;
			}

			if (currentMemberIds == null) continue;
			if (String.IsNullOrWhiteSpace(item.UUID) || !currentMemberIds.Contains(item.UUID))
			{
				currentMemberIds = null;
				continue;
			}
			collapsedMemberIds.Add(item.UUID);
		}
		return collapsedMemberIds;
	}

	/// <summary>
	/// Resolves collapsed rows from the same visual boundaries the user sees. Saved
	/// membership remains useful for drag persistence, but it may lag behind divider
	/// positions after older builds or interrupted edits and must not hide rows from a
	/// later section.
	/// </summary>
	public static IReadOnlySet<string> GetCollapsedMemberIds(
		IEnumerable<DivinityModData> visualItems)
	{
		ArgumentNullException.ThrowIfNull(visualItems);

		var collapsedMemberIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var insideCollapsedSection = false;
		foreach (var item in visualItems.Where(item => item != null))
		{
			if (item.IsVisualDivider)
			{
				insideCollapsedSection = item.IsVisualDividerCollapsed;
				continue;
			}

			if (insideCollapsedSection && !String.IsNullOrWhiteSpace(item.UUID))
				collapsedMemberIds.Add(item.UUID);
		}
		return collapsedMemberIds;
	}

	private static List<ModListVisualDividerData> GetPaneDividers(
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList) => dividers
		.Where(divider => divider != null && divider.IsActiveList == activeList)
		.OrderBy(divider => divider.Position)
		.ToList();

	private static bool NormalizeOwnership(IReadOnlyList<ModListVisualDividerData> paneDividers)
	{
		var changed = false;
		var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var divider in paneDividers)
		{
			if (divider.MemberModUuids == null) continue;
			var normalized = divider.MemberModUuids
				.Where(uuid => !String.IsNullOrWhiteSpace(uuid) && claimed.Add(uuid))
				.ToList();
			if (!divider.MemberModUuids.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase))
			{
				divider.MemberModUuids = normalized;
				changed = true;
			}
		}
		return changed;
	}

	private sealed record LegacyEntry(DivinityModData Mod, ModListVisualDividerData Divider);
}
