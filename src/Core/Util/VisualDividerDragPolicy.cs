using DivinityModManager.Models;

namespace DivinityModManager.Util;

/// <summary>
/// Builds a visual mod-list drag payload from the row where the drag started.
/// This prevents a selected divider from hitchhiking with an ordinary mod.
/// </summary>
public static class VisualDividerDragPolicy
{
	public static bool CanStartDrag(DivinityModData item, bool withinOverridePane) =>
		item != null && (withinOverridePane || item.CanDrag);

	public static bool ContainsVisualDivider(IEnumerable<DivinityModData> items)
	{
		ArgumentNullException.ThrowIfNull(items);
		return items.Any(item => item?.IsVisualDivider == true);
	}

	public static bool CanDropOnPane(IEnumerable<DivinityModData> items, bool destinationActive) =>
		items.All(item => item?.IsVisualDivider != true || !item.IsVisualDividerCollapsed || item.IsActive == destinationActive);

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
