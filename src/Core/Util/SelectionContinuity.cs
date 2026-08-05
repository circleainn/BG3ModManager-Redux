namespace DivinityModManager.Util;

/// <summary>
/// Keeps selection-backed UI stable while an item moves between collection views.
/// </summary>
public static class SelectionContinuity
{
	public static T? ResolveDisplayedItem<T>(
		T? selectedItem,
		T? displayedItem,
		Predicate<T> canRetainDisplayedItem)
		where T : class
	{
		if (selectedItem != null)
		{
			return selectedItem;
		}

		return displayedItem != null && canRetainDisplayedItem?.Invoke(displayedItem) == true
			? displayedItem
			: null;
	}
}
