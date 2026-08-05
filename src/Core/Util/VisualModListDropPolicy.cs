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

		// Rows owned by a collapsed separator are not published to the list view, so
		// the row after a collapsed separator is already past its hidden block.
		return targetIndex + (insertAfter ? 1 : 0);
	}

	/// <summary>
	/// Translates a drop index addressing the visible rows into an index in the full
	/// sequence, which also contains the rows hidden by collapsed separators.
	/// </summary>
	public static int ResolveSequenceInsertIndex(
		IReadOnlyList<DivinityModData> sequence,
		IReadOnlyList<DivinityModData> visibleItems,
		int visibleIndex)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		ArgumentNullException.ThrowIfNull(visibleItems);
		if (visibleIndex <= 0) return 0;
		if (visibleIndex >= visibleItems.Count) return sequence.Count;

		var anchor = visibleItems[visibleIndex];
		for (var index = 0; index < sequence.Count; index++)
		{
			if (ReferenceEquals(sequence[index], anchor)) return index;
		}
		return sequence.Count;
	}

	/// <summary>
	/// Moves an insertion point to the nearest section boundary. Section membership is
	/// positional, so dropping a collapsed block in the middle of a run of mods would
	/// silently make the rows below it part of the dropped section. A collapsed section
	/// is a self-contained block and may only land between sections.
	/// </summary>
	public static int SnapToSectionBoundary(IReadOnlyList<DivinityModData> items, int insertIndex)
	{
		ArgumentNullException.ThrowIfNull(items);
		insertIndex = Math.Clamp(insertIndex, 0, items.Count);
		if (IsSectionBoundary(items, insertIndex)) return insertIndex;

		for (var distance = 1; distance <= items.Count; distance++)
		{
			var before = insertIndex - distance;
			if (before >= 0 && IsSectionBoundary(items, before)) return before;
			var after = insertIndex + distance;
			if (after <= items.Count && IsSectionBoundary(items, after)) return after;
		}
		return items.Count;
	}

	// The end of the list always closes the final section, so it is always a boundary.
	private static bool IsSectionBoundary(IReadOnlyList<DivinityModData> items, int index) =>
		index >= items.Count || items[index].IsVisualDivider;

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
