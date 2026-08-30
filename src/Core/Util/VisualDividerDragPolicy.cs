using DivinityModManager.Models;

namespace DivinityModManager.Util;

/// <summary>
/// Builds a visual mod-list drag payload from the row where the drag started.
/// This prevents a selected divider from hitchhiking with an ordinary mod.
/// </summary>
public static class VisualDividerDragPolicy
{
	public static IReadOnlyList<DivinityModData> ResolveDragItems(
		IEnumerable<DivinityModData> visualItems,
		DivinityModData sourceItem,
		Predicate<DivinityModData> canDragNormalItem)
	{
		ArgumentNullException.ThrowIfNull(visualItems);
		ArgumentNullException.ThrowIfNull(canDragNormalItem);

		var items = visualItems.ToList();
		var sourceIndex = items.FindIndex(item => ReferenceEquals(item, sourceItem));
		if (sourceIndex < 0) return Array.Empty<DivinityModData>();

		if (sourceItem.IsVisualDivider)
		{
			// Keep the live drag payload lightweight. The drop policy resolves a closed
			// separator's hidden members from its sealed membership snapshot only after
			// the user commits the move. Expanded separators remain marker-only moves.
			return new[] { sourceItem };
		}

		if (!canDragNormalItem(sourceItem)) return Array.Empty<DivinityModData>();

		var selectedMods = items
			.Where(item => !item.IsVisualDivider && item.IsSelected && canDragNormalItem(item))
			.ToList();

		return selectedMods.Any(item => ReferenceEquals(item, sourceItem))
			? selectedMods
			: new[] { sourceItem };
	}
}
