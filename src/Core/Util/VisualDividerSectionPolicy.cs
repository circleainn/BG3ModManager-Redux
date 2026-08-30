using DivinityModManager.Models;

namespace DivinityModManager.Util;

/// <summary>
/// Projects separator rows over the authoritative mod collections. Expanded
/// membership follows visual boundaries; collapsed membership is a persisted,
/// sealed snapshot. Neither controls drag payloads or the underlying mod order.
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
	/// visual sequence. The live drag payload deliberately contains no mod rows.
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
	/// Resolves a collapsed separator marker to one canonical, contiguous block made
	/// from its sealed members. Unowned rows after the block are never carried along.
	/// The live drag preview remains marker-only, keeping pointer movement independent
	/// of the number of hidden mods.
	/// </summary>
	public static IReadOnlyList<DivinityModData> ResolveCollapsedBlockDragPayload(
		IEnumerable<DivinityModData> visualItems,
		DivinityModData draggedMarker,
		ModListVisualDividerData divider)
	{
		ArgumentNullException.ThrowIfNull(visualItems);
		ArgumentNullException.ThrowIfNull(draggedMarker);
		ArgumentNullException.ThrowIfNull(divider);
		if (!draggedMarker.IsVisualDivider || !divider.IsCollapsed ||
			String.IsNullOrWhiteSpace(divider.Id))
			return Array.Empty<DivinityModData>();

		var sequence = visualItems.Where(item => item != null).ToList();
		var markerIndex = sequence.FindIndex(item => item.IsVisualDivider &&
			String.Equals(item.VisualDividerId, divider.Id, StringComparison.OrdinalIgnoreCase));
		if (markerIndex < 0) return Array.Empty<DivinityModData>();

		var memberIds = (divider.MemberModUuids ?? Enumerable.Empty<string>())
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var result = new List<DivinityModData> { sequence[markerIndex] };
		for (var index = markerIndex + 1; index < sequence.Count; index++)
		{
			var item = sequence[index];
			if (item.IsVisualDivider || String.IsNullOrWhiteSpace(item.UUID) ||
				!memberIds.Contains(item.UUID)) break;
			result.Add(item);
		}
		return result;
	}

	/// <summary>
	/// Rebuilds section membership from the current marker positions. Each separator
	/// owns the ordinary rows after it up to the next separator; rows before the first
	/// separator remain unsectioned.
	/// </summary>
	public static bool AssignMembersByCurrentBoundaries(
		IEnumerable<DivinityModData> visualItems,
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList) => AssignMembers(
			visualItems,
			dividers,
			activeList,
			preserveCollapsedMembership: false,
			expandingDividerId: null);

	/// <summary>
	/// Rebuilds expanded sections while treating every collapsed separator as a
	/// sealed entry. Existing collapsed members remain owned even when ordinary rows
	/// or expanded separator markers move around the closed entry. Pass the separator
	/// being expanded to deliberately resume boundary ownership for that one section.
	/// </summary>
	public static bool AssignMembersPreservingCollapsedSections(
		IEnumerable<DivinityModData> visualItems,
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList,
		string expandingDividerId = null) => AssignMembers(
			visualItems,
			dividers,
			activeList,
			preserveCollapsedMembership: true,
			expandingDividerId);

	private static bool AssignMembers(
		IEnumerable<DivinityModData> visualItems,
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList,
		bool preserveCollapsedMembership,
		string expandingDividerId)
	{
		ArgumentNullException.ThrowIfNull(visualItems);
		ArgumentNullException.ThrowIfNull(dividers);

		var sequence = visualItems.Where(item => item != null).ToList();
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
		var preservedDividerIds = preserveCollapsedMembership
			? paneDividers
				.Where(divider => divider.IsCollapsed && divider.MemberModUuids != null &&
					!String.Equals(divider.Id, expandingDividerId, StringComparison.OrdinalIgnoreCase))
				.Select(divider => divider.Id)
				.ToHashSet(StringComparer.OrdinalIgnoreCase)
			: new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var availableModIds = sequence
			.Where(item => !item.IsVisualDivider && !String.IsNullOrWhiteSpace(item.UUID))
			.Select(item => item.UUID)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var claimedMemberIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var divider in paneDividers.Where(divider => preservedDividerIds.Contains(divider.Id)))
		{
			foreach (var uuid in divider.MemberModUuids ?? Enumerable.Empty<string>())
			{
				if (String.IsNullOrWhiteSpace(uuid) || !availableModIds.Contains(uuid) ||
					!claimedMemberIds.Add(uuid)) continue;
				memberIdsById[divider.Id].Add(uuid);
				membersById[divider.Id].Add(uuid);
			}
		}

		ModListVisualDividerData currentDivider = null;
		foreach (var item in sequence)
		{
			if (item.IsVisualDivider)
			{
				dividerById.TryGetValue(item.VisualDividerId ?? String.Empty, out currentDivider);
				continue;
			}

			if (currentDivider == null || preservedDividerIds.Contains(currentDivider.Id) ||
				String.IsNullOrWhiteSpace(item.UUID) || !claimedMemberIds.Add(item.UUID)) continue;
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
		IEnumerable<DivinityModData> expandingMembers)
	{
		ArgumentNullException.ThrowIfNull(visibleItems);
		ArgumentNullException.ThrowIfNull(marker);
		ArgumentNullException.ThrowIfNull(expandingMembers);

		var insertIndex = -1;
		for (var index = 0; index < visibleItems.Count; index++)
		{
			if (!ReferenceEquals(visibleItems[index], marker)) continue;
			insertIndex = index;
			break;
		}
		if (insertIndex < 0) return -1;
		insertIndex++;
		var memberIds = expandingMembers
			.Where(member => member != null && !String.IsNullOrWhiteSpace(member.UUID))
			.Select(member => member.UUID)
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
