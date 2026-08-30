using DivinityModManager.Models;

using System.Collections;

namespace DivinityModManager.Util;

/// <summary>
/// Resolves visual separator drop boundaries and applies a visual-list move
/// without allowing hidden rows from collapsed sections to change ownership.
/// </summary>
public static class VisualModListDropPolicy
{
	public static int MapVisibleInsertionIndex(
		IReadOnlyList<DivinityModData> visibleItems,
		IReadOnlyList<DivinityModData> fullItems,
		int visibleInsertionIndex)
	{
		ArgumentNullException.ThrowIfNull(visibleItems);
		ArgumentNullException.ThrowIfNull(fullItems);
		if (visibleInsertionIndex <= 0) return 0;
		if (visibleInsertionIndex >= visibleItems.Count) return fullItems.Count;

		// Identify a visible slot by the item immediately after it. Finding that
		// anchor in the canonical sequence places the slot after any collapsed
		// members omitted between the prior visible row and the anchor.
		var anchor = visibleItems[visibleInsertionIndex];
		for (var index = 0; index < fullItems.Count; index++)
		{
			if (ItemsMatch(fullItems[index], anchor)) return index;
		}
		return fullItems.Count;
	}

	public static int ResolveInsertionIndex(
		IList visualItems,
		int targetIndex,
		bool insertAfter,
		IEnumerable<string> targetSectionMemberUuids = null)
	{
		ArgumentNullException.ThrowIfNull(visualItems);
		if (targetIndex < 0 || targetIndex >= visualItems.Count)
			return Math.Clamp(targetIndex, 0, visualItems.Count);

		var insertIndex = targetIndex + (insertAfter ? 1 : 0);
		if (!insertAfter || visualItems[targetIndex] is not DivinityModData
			{ IsVisualDivider: true, IsVisualDividerCollapsed: true })
		{
			return insertIndex;
		}

		// The rows owned by a collapsed separator remain in the collection even
		// though they have no visible containers. "After" means after that explicit
		// block, not after every row that happens to follow the marker.
		var memberIds = targetSectionMemberUuids?
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		while (insertIndex < visualItems.Count &&
			visualItems[insertIndex] is DivinityModData { IsVisualDivider: false } mod &&
			(memberIds == null || (!String.IsNullOrWhiteSpace(mod.UUID) && memberIds.Contains(mod.UUID))))
		{
			insertIndex++;
		}
		return insertIndex;
	}

	public static VisualModListDropResult Apply(
		IEnumerable<DivinityModData> activeItems,
		IEnumerable<DivinityModData> inactiveItems,
		IEnumerable<DivinityModData> draggedItems,
		bool destinationActive,
		int insertIndex)
	{
		ArgumentNullException.ThrowIfNull(activeItems);
		ArgumentNullException.ThrowIfNull(inactiveItems);
		ArgumentNullException.ThrowIfNull(draggedItems);

		var active = activeItems.ToList();
		var inactive = inactiveItems.ToList();
		var dragged = draggedItems.Where(item => item != null).Distinct().ToList();
		var draggedSet = dragged.ToHashSet();
		var destination = destinationActive ? active : inactive;
		var removedBeforeInsertion = destination
			.Take(Math.Clamp(insertIndex, 0, destination.Count))
			.Count(draggedSet.Contains);
		insertIndex -= removedBeforeInsertion;

		active.RemoveAll(draggedSet.Contains);
		inactive.RemoveAll(draggedSet.Contains);

		insertIndex = Math.Clamp(insertIndex, 0, destination.Count);
		destination.InsertRange(insertIndex, dragged);
		return new VisualModListDropResult(active, inactive);
	}

	private static bool ItemsMatch(DivinityModData first, DivinityModData second) =>
		ReferenceEquals(first, second)
		|| (first?.IsVisualDivider == true && second?.IsVisualDivider == true &&
			!String.IsNullOrWhiteSpace(first.VisualDividerId) &&
			first.VisualDividerId.Equals(second.VisualDividerId, StringComparison.OrdinalIgnoreCase));
}

public sealed class VisualModListDropResult
{
	public IReadOnlyList<DivinityModData> ActiveItems { get; }
	public IReadOnlyList<DivinityModData> InactiveItems { get; }

	internal VisualModListDropResult(
		IReadOnlyList<DivinityModData> activeItems,
		IReadOnlyList<DivinityModData> inactiveItems)
	{
		ActiveItems = activeItems;
		InactiveItems = inactiveItems;
	}
}
