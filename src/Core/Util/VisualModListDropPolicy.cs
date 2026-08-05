using DivinityModManager.Models;

using System.Collections;

namespace DivinityModManager.Util;

/// <summary>
/// Resolves visual separator drop boundaries and applies a visual-list move
/// without allowing hidden rows from collapsed sections to change ownership.
/// </summary>
public static class VisualModListDropPolicy
{
	public static int ResolveInsertionIndex(IList visualItems, int targetIndex, bool insertAfter)
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
		// though they have no visible containers. "After" the visible separator
		// therefore means after its complete hidden block, at the next separator.
		while (insertIndex < visualItems.Count &&
			visualItems[insertIndex] is DivinityModData { IsVisualDivider: false })
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
		var destination = destinationActive ? active : inactive;

		foreach (var item in dragged)
		{
			var oldIndex = destination.IndexOf(item);
			if (oldIndex >= 0 && oldIndex < insertIndex) insertIndex--;
			active.Remove(item);
			inactive.Remove(item);
		}

		insertIndex = Math.Clamp(insertIndex, 0, destination.Count);
		destination.InsertRange(insertIndex, dragged);
		return new VisualModListDropResult(active, inactive);
	}
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
